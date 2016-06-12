using System.Threading.Tasks;
using HexapiBackground.Hardware;

namespace HexapiBackground{
    internal class Display
    {
        private static SparkFunSerial16X2Lcd _lcd = new SparkFunSerial16X2Lcd();

        internal Display()
        {
            _lcd.Start();
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