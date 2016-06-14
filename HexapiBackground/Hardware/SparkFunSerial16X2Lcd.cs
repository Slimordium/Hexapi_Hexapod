using System;
using System.Collections.Generic;
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

        internal async void Start()
        {
            _serialDevice = await SerialDeviceHelper.GetSerialDevice("AJ030T2QA", 9600);
            _outputStream = new DataWriter(_serialDevice.OutputStream);
        }

        internal async Task WriteToFirstLine(string text)
        {
            _outputStream.WriteBytes(_startOfFirstLine);
            _outputStream.WriteString(text);
            
            var count = 16 - text.Length - 1;

            if (count > 0)
            {
                var spaces = new List<byte>();

                for (var i = 0; i < count; i++)
                {
                    spaces.Add(0x20);
                }
                _outputStream.WriteBytes(spaces.ToArray());
            }

            await _outputStream.StoreAsync();
        }

        internal async Task WriteToSecondLine(string text)
        {
            _outputStream.WriteBytes(_startOfSecondLine);
            _outputStream.WriteString(text);

            var count = 16 - text.Length - 1;

            if (count > 0)
            {
                var spaces = new List<byte>();

                for (var i = 0; i < count; i++)
                {
                    spaces.Add(0x20);
                }
                _outputStream.WriteBytes(spaces.ToArray());
            }

            await _outputStream.StoreAsync();
        }

        internal async Task Write(string text)
        {
            await Clear();

            var bytes = Encoding.ASCII.GetBytes(text);

            _outputStream.WriteBytes(_startOfFirstLine);
            _outputStream.WriteBytes(bytes);
            await _outputStream.StoreAsync();
        }

        internal async Task EraseFirstLine()
        {
            _outputStream.WriteBytes(_startOfFirstLine);
            _outputStream.WriteBytes(new byte [] {0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20});
            await _outputStream.StoreAsync();
        }

        internal async Task EraseSecondLine()
        {
            _outputStream.WriteBytes(_startOfSecondLine);
            _outputStream.WriteBytes(new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 });
            await _outputStream.StoreAsync();
        }

        internal async Task Clear()
        {
            await EraseFirstLine();
            await EraseSecondLine();
        }
    }
}