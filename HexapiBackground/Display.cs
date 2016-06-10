using System.Threading.Tasks;
using HexapiBackground.Hardware;

namespace HexapiBackground{
    internal class Display
    {
        private static SfSerial16X2Lcd _lcd;

        internal Display(SfSerial16X2Lcd lcd)
        {
            _lcd = lcd;
        }

        internal static void Write(string text, int line)
        {
            if (_lcd == null) return;

            Task.Run(async () =>
            {
                if (line == 1)
                    await _lcd.WriteToFirstLine(text);

                if (line == 2)
                    await _lcd.WriteToSecondLine(text);
            });
        }

        internal static void Write(string text)
        {
            if (_lcd == null) return;

            Task.Run(async () =>
            {
                await _lcd.Write(text);
            });
        }
    }
}