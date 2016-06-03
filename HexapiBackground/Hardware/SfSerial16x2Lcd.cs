using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace HexapiBackground.Hardware{
    internal class SfSerial16X2Lcd
    {
        private readonly byte[] _startOfFirstLine = {0xfe, 0x80};
        private readonly byte[] _startOfSecondLine = {0xfe, 0xc0};
        private readonly SerialPort _serialPort = new SerialPort();

        internal async Task Start()
        {
            await _serialPort.Open("AJ030T2QA", 9600, 1000, 1000);
        }

        internal async Task WriteToFirstLine(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);

            await _serialPort.Write(_startOfFirstLine);
            await _serialPort.Write(bytes);

            var count = 16 - bytes.Length - 1;

            if (count > 0)
            {
                for (var i = 0; i <= count; i++)
                {
                    await _serialPort.Write(new byte[] { 0x20 });
                }
            }
        }

        internal async Task WriteToSecondLine(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            
            await _serialPort.Write(_startOfSecondLine);
            await _serialPort.Write(bytes);

            var count = 16 - bytes.Length - 1;

            if (count > 0)
            {
                for (var i = 0; i <= count; i++)
                {
                    await _serialPort.Write(new byte[] { 0x20 });
                }
            }
        }

        internal async Task Write(string text)
        {
            await Clear();

            var bytes = Encoding.ASCII.GetBytes(text);

            await _serialPort.Write(_startOfFirstLine);
            await _serialPort.Write(bytes);
        }

        internal async Task EraseFirstLine()
        {
            await _serialPort.Write(_startOfFirstLine);
            await _serialPort.Write(new byte [] {0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20});
        }

        internal async Task EraseSecondLine()
        {
            await _serialPort.Write(_startOfSecondLine);
            await _serialPort.Write(new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 });
        }

        internal async Task Clear()
        {
            await EraseFirstLine();
            await EraseSecondLine();
        }
    }
}