using System.Collections.Generic;
using static MobiFlight.Joysticks.VKB.VKBEncoder;

namespace MobiFlight.Joysticks.VKB
{
    internal class VKBEncoder
    {
        public enum EncoderAction
        {
            DEC = 0,
            INC = 5
        }
        public JoystickDevice DeviceInc = null;
        public JoystickDevice DeviceDec = null;
        private ushort value = 0;
        public VKBEncoder(byte index, ushort initialValue = 0)
        {
            value = initialValue;
            CreateDevices(index + 1);
        }
        public VKBEncoder(JoystickDevice inc, JoystickDevice dec, ushort initialValue = 0)
        {
            DeviceInc = inc;
            DeviceDec = dec;
            value = initialValue;
        }
        private void CreateDevices(int index)
        {
            DeviceInc = new JoystickDevice { Name = $"Button {1000 + 10 * index + (int)EncoderAction.INC}", Label = $"Encoder {index} INC", Type = DeviceType.Button, JoystickDeviceType = JoystickDeviceType.Button };
            DeviceDec = new JoystickDevice { Name = $"Button {1000 + 10 * index + (int)EncoderAction.DEC}", Label = $"Encoder {index} DEC", Type = DeviceType.Button, JoystickDeviceType = JoystickDeviceType.Button };
        }
        public IEnumerable<InputEventArgs> Update(ushort newPosition)
        {
            List<InputEventArgs> events = new List<InputEventArgs>();
            short deltaCount = (short)((newPosition - value) & 0xFFFF);
            if (deltaCount > 0)
            {
                while (value != newPosition)
                {
                    value++;
                    events.Add(new InputEventArgs { DeviceId = DeviceInc.Name, DeviceLabel = DeviceInc.Label, Type = DeviceType.Button, Value = (int)MobiFlightButton.InputEvent.PRESS });
                }
            }
            else if (deltaCount < 0)
            {
                while (value != newPosition)
                {
                    value--;
                    events.Add(new InputEventArgs { DeviceId = DeviceDec.Name, DeviceLabel = DeviceDec.Label, Type = DeviceType.Button, Value = (int)MobiFlightButton.InputEvent.PRESS });
                }
            }
            return events;
        }
    }
}
