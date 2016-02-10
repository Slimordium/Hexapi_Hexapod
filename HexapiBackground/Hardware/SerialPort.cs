using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace HexapiBackground
{
    internal class SerialPort
    {
        private static IBuffer _singleByteBuffer = new Buffer(1);
        private static readonly ASCIIEncoding AsciiEncoding = new ASCIIEncoding();
        private static IBuffer _buffer;
        private SerialDevice _serialPort;
        internal SerialError LastError { get; private set; }

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

                    _serialPort.ReadTimeout = TimeSpan.FromMilliseconds(readTimeoutMs);
                    _serialPort.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeoutMs);
                    _serialPort.BaudRate = (uint) baudRate;
                    _serialPort.Parity = SerialParity.None;
                    _serialPort.StopBits = SerialStopBitCount.One;
                    _serialPort.DataBits = 8;
                    _serialPort.Handshake = SerialHandshake.None;
                    _serialPort.ErrorReceived += _serialPort_ErrorReceived;
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

        internal async Task<byte> ReadByte()
        {
            if (_serialPort == null)
                return new byte();

            _singleByteBuffer = new Buffer(1);

            var r = await _serialPort.InputStream.ReadAsync(_singleByteBuffer, 1, InputStreamOptions.Partial).AsTask();

            return r.GetByte(0u);
        }

        internal string ReadString()
        {
            if (_serialPort == null)
                return string.Empty;

            _buffer = new Buffer(128);

            Task.Factory.StartNew(() =>
            {
                _serialPort.InputStream.ReadAsync(_buffer, 128, InputStreamOptions.None).AsTask().Wait();
            }).Wait();

            return AsciiEncoding.GetString(_buffer.ToArray());
        }
    }
}