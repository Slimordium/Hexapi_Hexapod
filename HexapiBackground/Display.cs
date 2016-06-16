using System.Threading.Tasks;
using HexapiBackground.Hardware;

namespace HexapiBackground{
    internal class Display
    {
        private static readonly SparkFunSerial16X2Lcd Lcd = new SparkFunSerial16X2Lcd();

        internal Display()
        {
            Lcd.Start();
        }

        internal static void Write(string text, int line)
        {
            if (Lcd == null) return;

            Task.Run(async () =>
            {
                if (line == 1)
                    await Lcd.WriteToFirstLine(text);

                if (line == 2)
                    await Lcd.WriteToSecondLine(text);
            });
        }

        internal static void Write(string text)
        {
            if (Lcd == null) return;

            Task.Run(async () =>
            {
                await Lcd.Write(text);
            });
        }
    }
}