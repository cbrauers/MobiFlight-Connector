namespace MobiFlight.Joysticks.VKB
{
    internal class VKBLed
    {
        public enum ColorMode : byte
        {
            Color1 = 0,
            Color2 = 1,
            Color1_2 = 2,
            Color2_1 = 3,
            Color1plus2 = 4
        }
        public enum FlashPattern : byte
        {
            Off = 0,
            Constantly = 1,
            Slow = 2,
            Fast = 3,
            UltraFast = 4
        };
        private readonly JoystickOutputDevice[] LedChannels = new JoystickOutputDevice[3];
        private bool dirty = true;
        private byte LedId = 0;
        private const byte defaultBrightness = 5;
        public VKBLed(byte id)
        {
            LedId = id;
        }
        public void AddChannel(JoystickOutput output)
        {
            if (LedChannels[output.Bit] != null) return;
            LedChannels[output.Bit] = new JoystickOutputDevice { Label = output.Label, Name = output.Id, Byte = output.Byte, Bit = output.Bit };
        }
        public void SetState(byte channel, byte state)
        {
            if (LedChannels[channel].State != state) dirty = true;
            LedChannels[channel].State = state;
        }
        public byte[] Serialize()
        {
            byte[] LedBlock = new byte[] { 0, 0, 0, 0 };
            FlashPattern pattern = FlashPattern.Off;
            ColorMode colmode = ColorMode.Color1;
            byte[,] ColorIntensity = new byte[2, 3] { { 0, 0, 0 }, { 0, 0, 0 } };
            int activeLeds = 0;
            foreach (JoystickOutputDevice channel in LedChannels)
            {
                if ((channel?.State ?? 0) != 0)
                {
                    pattern = FlashPattern.Constantly;
                    activeLeds++;
                }
            }
            if (LedChannels[2] != null)
            {
                // RGB LED, use Color1 with RGB values;
                foreach (JoystickOutputDevice channel in LedChannels)
                {
                    ColorIntensity[0, channel.Bit] = (byte)(channel.State * defaultBrightness);
                }
            }
            else
            {
                byte brightness = defaultBrightness;
                if (activeLeds > 1) brightness = 7;
                // Single or bicolor LED
                for (int col = 0; col < ColorIntensity.GetLength(0); col++)
                {
                    byte[] channelstate = new byte[2] { (LedChannels[0]?.State ?? 0), (LedChannels[1]?.State ?? 0) };
                    if (channelstate[0] != 0)
                    {
                        ColorIntensity[0, col] = (byte)(channelstate[0] * brightness);
                    }
                    if (channelstate[1] != 0)
                    {
                        ColorIntensity[1, col] = (byte)(channelstate[1] * brightness);
                        if (channelstate[0] != 0)
                        {
                            colmode = ColorMode.Color1plus2;
                        }
                        else
                        {
                            colmode = ColorMode.Color2;
                        }

                    }
                    ColorIntensity[0, col] = (byte)((LedChannels[0]?.State ?? 0) * brightness);
                    ColorIntensity[1, col] = (byte)((LedChannels[1]?.State ?? 0) * brightness);
                }
            }
            LedBlock[0] = LedId;
            LedBlock[1] = (byte)(ColorIntensity[0, 0] | ColorIntensity[0, 1] << 3 | (ColorIntensity[0, 2] & 0x03) << 6);
            LedBlock[2] = (byte)(ColorIntensity[0, 2] >> 2 | ColorIntensity[1, 0] << 1 | ColorIntensity[1, 1] << 4 | (ColorIntensity[1, 2] & 0x01) << 7);
            LedBlock[3] = (byte)(ColorIntensity[1, 2] >> 1 | (byte)pattern << 2 | (byte)colmode << 5);
            dirty = false;
            return LedBlock;
        }
        public static (ColorMode,FlashPattern) decodeLED(byte B0, byte B1, byte B2)
        {
            ColorMode colmode = (ColorMode)(B2 >> 5);
            FlashPattern pattern = (FlashPattern)((B2 >> 2) & 0x07);
            return (colmode, pattern);

        }
        public bool IsChanged()
        {
            return dirty;
        }
    }
}
