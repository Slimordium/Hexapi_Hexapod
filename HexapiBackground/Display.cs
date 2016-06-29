using System.Diagnostics;
using System.Threading.Tasks;
using HexapiBackground.Hardware;

namespace HexapiBackground{
    internal class Display
    {
        private static readonly SparkFunSerial16X2Lcd Lcd = new SparkFunSerial16X2Lcd();

        internal async Task Start()
        {
            await Lcd.Start();
        }

        internal static async Task Write(string text, int line)
        {
            await Task.Run(async () =>
            {
                if (line == 1)
                    await Lcd.WriteToFirstLine(text);

                if (line == 2)
                    await Lcd.WriteToSecondLine(text);
            }).ConfigureAwait(false);
        }

        internal static async Task Write(string text)
        {
            await Task.Run(async () =>
            {
                await Lcd.Write(text);
            }).ConfigureAwait(false);
        }
    }
}