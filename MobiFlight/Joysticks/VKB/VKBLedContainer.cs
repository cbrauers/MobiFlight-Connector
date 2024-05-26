using System;
using System.Collections;
using System.Collections.Generic;

namespace MobiFlight.Joysticks.VKB
{
    internal class VKBLedContainer
    {
        private readonly SortedList<byte, VKBLed> Leds = new SortedList<byte, VKBLed>();
        private readonly Dictionary<String, (byte, byte)> Labels = new Dictionary<string, (byte, byte)>();
        public void AddChannel(JoystickOutput output)
        {
            if (!Leds.ContainsKey(output.Byte))
                Leds.Add(output.Byte, new VKBLed(output.Byte));
            Leds[output.Byte].AddChannel(output);
            Labels.Add(output.Label, (output.Byte, output.Bit));
        }
        public void UpdateState (string Label, byte State)
        {
            String[] updateLabels = Label.Split('|');
            foreach (String updateLabel in updateLabels)
            {
                if (Labels.ContainsKey(updateLabel))
                {
                    (byte Byte, byte Bit) = Labels[updateLabel];
                    Leds[Byte]?.SetState(Bit, State);
                }
            }
        }
        public byte[] CreateMessage()
        {
            int LedCount = 0;
            foreach(KeyValuePair<byte,VKBLed> entry in Leds)
            {
                if (entry.Value.IsChanged()) LedCount++;
            }
            byte[] buffer = new byte[129];
            buffer[0] = 0x59;
            buffer[1] = 0xA5;
            buffer[2] = 0x0A;
            buffer[7] = (byte)LedCount;
            int bufferoffset = 8;
            foreach (KeyValuePair<byte, VKBLed> entry in Leds)
            {
                if (!entry.Value.IsChanged()) continue;
                Buffer.BlockCopy(entry.Value.Serialize(), 0, buffer, bufferoffset, 4);
                bufferoffset += 4;
                if (bufferoffset > 126) break;
            }
            return buffer;
        }
    }
}
