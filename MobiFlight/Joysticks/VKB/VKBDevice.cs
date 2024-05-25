using HidSharp.Reports;
using HidSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MobiFlight.Joysticks.VKB
{
    internal class VKBDevice : Joystick
    {
        private HidStream Stream;
        private readonly HidDevice Device;
        private readonly JoystickDefinition Definition;
        private readonly VKBLedContainer Lights = new VKBLedContainer();

        public VKBDevice(SharpDX.DirectInput.Joystick joystick, JoystickDefinition definition) : base(joystick, definition)
        {
            this.Definition = definition;
            if (Device == null)
            {
                var DevList = DeviceList.Local.GetHidDevices(joystick.Properties.VendorId, joystick.Properties.ProductId);
                foreach (HidDevice dev in DevList)
                {
                    if (dev.DevicePath == joystick.Properties.InterfacePath)
                    {
                        Device = dev;
                        break;
                    }
                }
                if (Device == null) return;
            }
        }
        //public override void Connect(IntPtr handle)
        //{
        //    base.Connect(handle);
        //}

        private void Connect()
        {
            if (Stream == null)
            {
                Stream = Device.Open();
                Stream.ReadTimeout = System.Threading.Timeout.Infinite;
            }
        }
        protected override void SendData(byte[] data)
        {
            // Don't try and send data if no outputs are defined.
            if (Definition?.Outputs == null || Definition?.Outputs.Count == 0)
            {
                return;
            }

            if (Stream == null)
            {
                Connect();
            };
            Stream.SetFeature(data);

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
    }
}