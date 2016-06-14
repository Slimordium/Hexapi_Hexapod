using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;
using Microsoft.Maker.Serial;

namespace HexapiBackground.Hardware
{
    internal class SerialPort
    {
        private static IBuffer _singleByteBuffer = new Buffer(1);
        private static readonly ASCIIEncoding AsciiEncoding = new ASCIIEncoding();
        private static IBuffer _buffer;
        private SerialDevice _serialPort;
        internal SerialError LastError { get; private set; }

        private DataReader _dataReader;
        private DataReader _dataWriter;

        internal async Task Open(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            while (_serialPort == null)
            {
                var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier));

                if (selectedPort == null)
                {
                    Debug.WriteLine($"Could not find device information for {identifier}. Retrying in 2 seconds... ");

                    await Task.Delay(2000);
                    continue;
                }

                _serialPort = await SerialDevice.FromIdAsync(selectedPort.Id);

                if (_serialPort == null)
                {
                    Debug.WriteLine($"Could not open serial port at {identifier}. Usually an app manifest issue. Retrying in 2 seconds...  ");

                    await Task.Delay(2000);
                    continue;
                }

                Debug.WriteLine($"Found - {identifier} as {selectedPort.Id}");

                //_serialPort.ReadTimeout = TimeSpan.FromMilliseconds(readTimeoutMs);
                _serialPort.ReadTimeout = TimeSpan.MaxValue;
                _serialPort.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeoutMs);
                _serialPort.BaudRate = (uint) baudRate;
                _serialPort.Parity = SerialParity.None;
                _serialPort.StopBits = SerialStopBitCount.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = SerialHandshake.None;
                //_serialPort.ErrorReceived += _serialPort_ErrorReceived;

                _dataReader = new DataReader(_serialPort.InputStream);
            }
        }

        

        private void _serialPort_ErrorReceived(SerialDevice sender, ErrorReceivedEventArgs args)
        {
            if (LastError != args.Error)
                LastError = args.Error;   

            //Debug.WriteLine($"SerialPort Error on {sender.PortName}, {args.Error}");
        }

        internal void Close()
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        internal async Task Write(string data)
        {
            if (_serialPort == null)
                return;

            var buffer = AsciiEncoding.GetBytes(data).AsBuffer();
            await _serialPort.OutputStream.WriteAsync(buffer).AsTask();
        }

        internal async Task Write(byte[] data)
        {
            await _serialPort.OutputStream.WriteAsync(data.AsBuffer());
        }

        internal Action<byte> ReplyCallback { get; set; }

        internal async Task ListenForSscCommandComplete()
        {
            var buffer = new byte[1];

            while (true)
            {
                await _serialPort.InputStream.ReadAsync(buffer.AsBuffer(), 1, InputStreamOptions.Partial);

                ReplyCallback?.Invoke(buffer[0]);
            }
        }

        internal async Task<byte[]> ReadBytes()
        {
            if (_serialPort == null)
                return new byte[1];

            var buffer = new Buffer(256);

            await _serialPort.InputStream.ReadAsync(buffer, 256, InputStreamOptions.Partial).AsTask();

            return buffer.ToArray();
        }

        internal async Task<string> ReadString()
        {
            if (_serialPort == null)
                return string.Empty;

            _buffer = new Buffer(256);

            await _serialPort.InputStream.ReadAsync(_buffer, 256, InputStreamOptions.Partial);

            return AsciiEncoding.GetString(_buffer.ToArray());
        }

        internal string ReadFonaLine()
        {
            if (_serialPort == null)
                return string.Empty;

            var buffer = new Buffer(512);
            var returnString = string.Empty;

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    try
                    {
                        Task.Delay(250).Wait();

                        _serialPort.InputStream.ReadAsync(buffer, 512, InputStreamOptions.Partial).AsTask().Wait();
                    }
                    catch
                    {
                        break;
                    }

                    returnString += Encoding.UTF8.GetString(buffer.ToArray()).Trim();

                    if (returnString.Contains("OK"))
                        break;
                    if (returnString.Contains("ERROR"))
                        break;
                    if (returnString.Count( x => x == '.') >= 3)
                        break;
                    if (returnString.Count(x => x == '>') >= 1)
                        break;
                }
            }).Wait();

            return returnString;
        }
    }
}