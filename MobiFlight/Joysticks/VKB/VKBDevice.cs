using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;
using System;
using System.Collections.Generic;

namespace MobiFlight.Joysticks.VKB
{
    internal class VKBDevice : Joystick
    {
        private HidStream Stream;
        private readonly HidDevice Device;
        private readonly JoystickDefinition Definition;
        private readonly VKBLedContainer Lights = new VKBLedContainer();
        private HidDeviceInputReceiver InputReceiver;
        private readonly byte[] InputReportBuffer = new byte[64];
        private readonly SortedList<byte, VKBEncoder> Encoders = new SortedList<byte, VKBEncoder>();
        int lastSeqNo = -1;

        public VKBDevice(SharpDX.DirectInput.Joystick joystick, JoystickDefinition definition) : base(joystick, definition)
        {
            Definition = definition;
            if (Device == null)
            {
                Device = GetMatchingHidDevice(joystick);
                if (Device == null) return;
            }
        }
        public override void Connect(IntPtr handle)
        {
            base.Connect(handle);
            if (Stream == null)
            {
                Stream = Device.Open();
                Stream.ReadTimeout = System.Threading.Timeout.Infinite;
            }

            if (InputReceiver == null)
            {
                InputReceiver = new ReportDescriptor(VKBHidReport.Descriptor).CreateHidDeviceInputReceiver();
                InputReceiver.Received += OnHidReportReceived;
                InputReceiver.Start(Stream);
            }
        }
        protected override void SendData(byte[] data)
        {
            // Don't try and send data if no outputs are defined.
            if (Definition?.Outputs == null || Definition?.Outputs.Count == 0)
            {
                return;
            }
            Stream?.SetFeature(data);

        }
        protected override void EnumerateDevices()
        {
            base.EnumerateDevices();
            SortedList<byte, JoystickDevice> EncoderDecList = new SortedList<byte, JoystickDevice>();
            SortedList<byte, JoystickDevice> EncoderIncList = new SortedList<byte, JoystickDevice>();
            foreach (var input in Definition.Inputs)
            {
                if (input.Id >= 1000 && input.Id < 2000 && input.Type == JoystickDeviceType.Button)
                {
                    byte encoderIndex = GetEncoderIndex(input);
                    VKBEncoder.EncoderAction encoderAction = GetEncoderAction(input);
                    if (encoderAction == VKBEncoder.EncoderAction.DEC)
                    {
                        EncoderDecList.Add(encoderIndex, new JoystickDevice { Name = $"Button {1000 + 10 * encoderIndex + (int)encoderAction}", Label = input.Label, Type = DeviceType.Button, JoystickDeviceType = input.Type });
                    }
                    if (encoderAction == VKBEncoder.EncoderAction.INC)
                    {
                        EncoderIncList.Add(encoderIndex, new JoystickDevice { Name = $"Button {1000 + 10 * encoderIndex + (int)encoderAction}", Label = input.Label, Type = DeviceType.Button, JoystickDeviceType = input.Type });
                    }
                    Buttons.FindAll(but => but.Label == input.Label).ForEach(but => but.Label += " (Legacy DirectInput)");
                }
            }
            foreach (var encdec in EncoderDecList)
            {
                if (EncoderIncList.ContainsKey(encdec.Key))
                {
                    Encoders.Add(encdec.Key, new VKBEncoder(EncoderIncList[encdec.Key], encdec.Value));
                    Buttons.Add(Encoders[encdec.Key].DeviceDec);
                    Buttons.Add(Encoders[encdec.Key].DeviceInc);
                }
            }
        }

        private static byte GetEncoderIndex(JoystickInput input)
        {
            return (byte)((input.Id - 1000) / 10);
        }
        private static VKBEncoder.EncoderAction GetEncoderAction(JoystickInput input)
        {
            return (VKBEncoder.EncoderAction)(input.Id % 10);
        }
        protected override void EnumerateOutputDevices()
        {
            Definition?.Outputs?.ForEach(output => Lights.AddChannel(output));
            base.EnumerateOutputDevices();
        }

        public override void SetOutputDeviceState(string name, byte state)
        {
            Lights.UpdateState(name, state);
        }


        public override void UpdateOutputDeviceStates()
        {
            var data = Lights.CreateMessage();
            if (data[7] == 0) return;
            try
            {
                SendData(data);
            }
            catch (System.IO.IOException)
            {
                base.OnDeviceRemoved();
            }
        }
        private void OnHidReportReceived(object sender, System.EventArgs e)
        {
            var inputReceiver = sender as HidDeviceInputReceiver;
            while (inputReceiver.TryRead(InputReportBuffer, 0, out _))
            {
                byte ReportId = InputReportBuffer[0];
                if (ReportId != 0x08) // 0x08 = Monitoring channel / virtual bus
                    continue;
                byte MessageType = InputReportBuffer[1];
                if (MessageType != 0x13) // 0x13 = Encoder status
                    continue;
                ParseEncoderReport(InputReportBuffer);
            }
        }
        private void ParseEncoderReport(byte[] Report)
        {
            byte sequenceNo = Report[2];
            if (((lastSeqNo + 1) & 0xFF) != sequenceNo)
            {
                Log.Instance.log("Some VKB encoder messages may have been missed", LogSeverity.Debug);
            }
            lastSeqNo = sequenceNo;
            byte encoderCount = Report[3];
            int maxEncoders = (Report.Length - 4) / 2;
            if (encoderCount > maxEncoders)
            {
                Log.Instance.log($"Log message reports {encoderCount} encoders, but only has space for {maxEncoders}. Some encoders were ignored.", LogSeverity.Warn);
                encoderCount = (byte)maxEncoders;
            }
            List<InputEventArgs> events = new List<InputEventArgs>();
            for (byte i = 0; i < encoderCount; i++)
            {
                ushort newPos = (ushort)(Report[5 + 2 * i] << 8 | Report[4 + 2 * i]);
                if (!Encoders.ContainsKey(i))
                {
                    Encoders.Add(i, new VKBEncoder(i, newPos));
                    Buttons.Add(Encoders[i].DeviceDec);
                    Buttons.Add(Encoders[i].DeviceInc);
                }
                else
                {
                    events.AddRange(Encoders[i].Update(newPos));
                }
            }
            foreach (InputEventArgs e in events)
            {
                e.Name = Name;
                e.Serial = SerialPrefix + DIJoystick.Information.InstanceGuid.ToString();
                TriggerButtonPressed(this, e);
            }
        }
        //private static JoystickDefinition FilterDefinition(JoystickDefinition definition)
        //{
        //    JoystickDefinition filteredDefinition = new JoystickDefinition();
        //    List<string> encoderOverrides = new List<string>();
        //    filteredDefinition.InstanceName = definition.InstanceName;
        //    filteredDefinition.VendorId = definition.VendorId;
        //    filteredDefinition.ProductId = definition.ProductId;
        //    filteredDefinition.Outputs = definition.Outputs;
        //    filteredDefinition.Inputs = new List<JoystickInput>();
        //    foreach (var input in definition.Inputs)
        //    {
        //        if (input.Id >= 1000 && input.Id < 2000)
        //        {
        //            //encoderOverrides.Add(input.Label);
        //            filteredDefinition.Inputs.Add(input);
        //        }
        //    }
        //    foreach (var input in definition.Inputs)
        //    {
        //        if (input.Id < 1000 && !encoderOverrides.Contains(input.Label))
        //        {
        //            filteredDefinition.Inputs.Add(input);
        //        }
        //    }
        //    return filteredDefinition;
        //}
        public static HidDevice GetMatchingHidDevice(SharpDX.DirectInput.Joystick joystick)
        {
            var DevList = DeviceList.Local.GetHidDevices(joystick.Properties.VendorId, joystick.Properties.ProductId);
            foreach (HidDevice dev in DevList)
                if (dev.DevicePath == joystick.Properties.InterfacePath)
                    return dev;
            return null;
        }
    }
}