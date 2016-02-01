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

        internal SerialPort(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            if (_serialPort != null)
            {
                Debug.WriteLine($"SerialPort is already opened for {identifier}");
                return;
            }

            while (_serialPort == null)
            {
                var deviceInformationCollection = DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()).GetAwaiter().GetResult();
                var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier)); //Onboard is "UART0"

                if (selectedPort == null)
                {
                    Debug.WriteLine($"Could not find {identifier}. Retrying in 2 seconds... ");

                    Task.Delay(2000).Wait();
                    return;
                }

                _serialPort = SerialDevice.FromIdAsync(selectedPort.Id).GetAwaiter().GetResult();

                Debug.WriteLine($"Found - {identifier} as {selectedPort.Id}");

                _serialPort.ReadTimeout = TimeSpan.FromMilliseconds(readTimeoutMs);
                _serialPort.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeoutMs);
                _serialPort.BaudRate = (uint) baudRate;
                _serialPort.Parity = SerialParity.None;
                _serialPort.StopBits = SerialStopBitCount.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = SerialHandshake.None;
                //_serialPort.ErrorReceived += _serialPort_ErrorReceived;
            }
        }

        internal static void ListAvailablePorts()
        {
            var deviceInformationCollection = DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()).GetAwaiter().GetResult();
            foreach (var d in deviceInformationCollection)
            {
                Debug.WriteLine($"Port - ID: {d.Id}");
            }
        }

        private static void _serialPort_ErrorReceived(SerialDevice sender, ErrorReceivedEventArgs args)
        {
            Debug.WriteLine($"SerialPort Error on {sender.PortName}, {args.Error}");
        }

        internal void Close()
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        internal uint Write(string data)
        {
            var buffer = AsciiEncoding.GetBytes(data).AsBuffer();

            var r = 0u;

            Task.Factory.StartNew(() =>
            {
                 _serialPort.OutputStream.WriteAsync(buffer).AsTask().Wait();
            }).Wait();

            return r;
        }

        internal async Task<byte> ReadByte()
        {
            _singleByteBuffer = new Buffer(1);
            
            var r = await _serialPort.InputStream.ReadAsync(_singleByteBuffer, 1, InputStreamOptions.Partial).AsTask();

            return r.GetByte(0u);
        }
        
        internal byte[] ReadBytes()
        {
            _buffer = new Buffer(_serialPort.BytesReceived);

            Task.Factory.StartNew(async () =>
            {
                _serialPort.InputStream.ReadAsync(_buffer, _buffer.Length, InputStreamOptions.Partial).AsTask().Wait();
            }).Wait();

            return _buffer.ToArray();
        }

        internal string ReadString()
        {
            _buffer = new Buffer(128);

            Task.Factory.StartNew(() =>
            {
                _serialPort.InputStream.ReadAsync(_buffer, 128, InputStreamOptions.None).AsTask().Wait();
            }).Wait();

            return AsciiEncoding.GetString(_buffer.ToArray());
        }

        internal string ReadUntil(string lastCharacter)
        {
            _singleByteBuffer = new Buffer(1);
            var readString = string.Empty;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    _serialPort.InputStream.ReadAsync(_singleByteBuffer, 1, InputStreamOptions.Partial).AsTask().Wait();

                    var c = AsciiEncoding.GetString(_singleByteBuffer.ToArray());

                    readString += AsciiEncoding.GetString(_singleByteBuffer.ToArray());

                    if (c.Equals(lastCharacter))
                        break;
                }
            }).Wait();

            return readString;
        }
    }
}