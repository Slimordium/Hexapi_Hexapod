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

        internal SerialPort(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            Task.Factory.StartNew(async () =>
            {
                while (_serialPort == null)
                {
                    var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                    var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier)); //Onboard is "UART0"

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
            });
        }

        internal static void ListAvailablePorts()
        {
            Debug.WriteLine("Available Serial Ports------------------");
            foreach (var d in DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()).GetAwaiter().GetResult())
            {
                Debug.WriteLine($"Port - ID: {d.Id}");
            }
            Debug.WriteLine("----------------------------------------");
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

        internal async void Write(string data)
        {
            if (_serialPort == null)
                return;

            var buffer = AsciiEncoding.GetBytes(data).AsBuffer();
            await _serialPort.OutputStream.WriteAsync(buffer).AsTask();
        }

        internal async void Write(byte[] data)
        {
            if (_serialPort == null)
                return;

            var buffer = data.AsBuffer();
            await _serialPort.OutputStream.WriteAsync(buffer).AsTask();
        }

        internal Action<byte> ListenAction { get; set; }

        internal async Task Listen()
        {
            while (true)
            {
                var incomingByte = await _dataReader.LoadAsync(1).AsTask();

                var buffer = new byte[incomingByte];
                _dataReader.ReadBytes(buffer);

                ListenAction?.Invoke(buffer[0]);
            }
        }

        internal byte[] ReadBytes()
        {
            if (_serialPort == null)
                return new byte[1];

            var buffer = new Buffer(256);

            _serialPort.InputStream.ReadAsync(buffer, 256, InputStreamOptions.Partial).AsTask().Wait();

            return buffer.ToArray();
        }

        internal string ReadString()
        {
            if (_serialPort == null)
                return string.Empty;

            _buffer = new Buffer(256);

            Task.Factory.StartNew(() =>
            {
                try
                {
                    _serialPort.InputStream.ReadAsync(_buffer, 256, InputStreamOptions.Partial).AsTask().Wait();
                }
                catch (TimeoutException)
                {
                    
                }
            }).Wait();

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