using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace HexapiBackground.Hardware{

    /// <summary>
    /// SparkFun Serial 16x2 LCD
    /// </summary>
    internal class SparkFunSerial16X2Lcd
    {
        private readonly byte[] _startOfFirstLine = {0xfe, 0x80};
        private readonly byte[] _startOfSecondLine = {0xfe, 0xc0};
        private SerialDevice _serialDevice;
        private DataWriter _outputStream;

        internal async Task Start()
        {
            _serialDevice = await SerialDeviceHelper.GetSerialDevice("DN01E099A", 9600);
            _outputStream = new DataWriter(_serialDevice.OutputStream);
        }

        private async Task Write(string text, byte[] line, bool clear)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(text);

            var count = 0;

            if (!clear)
                count = 16 - text.Length;
            else
                count = 32 - text.Length;

            if (count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    stringBuilder.Append(' ');
                }
            }

            if (_outputStream == null || _serialDevice == null)
            {
                Debug.WriteLine(text);
                return;
            }

            _outputStream.WriteBytes(line);
            _outputStream.WriteString(stringBuilder.ToString());
            await _outputStream.StoreAsync();
        }

        internal async Task WriteToFirstLine(string text)
        {
            await Write(text, _startOfFirstLine, false);
        }

        internal async Task WriteToSecondLine(string text)
        {
            await Write(text, _startOfSecondLine, false);
        }

        internal async Task Write(string text)
        {
            await Write(text, _startOfFirstLine, true);
        }
    }
}