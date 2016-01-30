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

        private object _rwLock = new object();

        internal SerialPort(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            if (_serialPort != null)
            {
                Debug.WriteLine($"SerialPort is already opened - {_serialPort.PortName}");
                return;
            }

            while (_serialPort == null)
            {
                var deviceInformationCollection =
                    DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()).GetAwaiter().GetResult();
                var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier));
                    //Onboard is "UART0"

                if (selectedPort == null)
                {
                    Debug.WriteLine($"Could not find {identifier}. Retrying in 2 seconds... ");

                    Task.Delay(2000).Wait();
                    return;
                }

                _serialPort = SerialDevice.FromIdAsync(selectedPort.Id).GetAwaiter().GetResult();

                Debug.WriteLine($"Found - Port name {_serialPort.PortName} for {identifier}");

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

        internal async void Write(string data, Action writeCompleteAction = null)
        {
            var buffer = AsciiEncoding.GetBytes(data).AsBuffer();
            var r = await _serialPort.OutputStream.WriteAsync(buffer);

            if (r < buffer.Length)
                Debug.WriteLine($"Write failed, wrote {r} of {buffer.Length}");

            writeCompleteAction?.Invoke();
        }

        internal byte ReadByte(Action readCompleteAction = null)
        {
            _singleByteBuffer = new Buffer(1);
            byte readByte = 0x00;

            Task.Factory.StartNew(async () =>
            {
                while (_singleByteBuffer.Length == 0)
                {
                    var ra = await _serialPort.InputStream.ReadAsync(_singleByteBuffer, 1, InputStreamOptions.Partial);
                }
            }).Wait();

            readByte = _singleByteBuffer.ToArray()[0];

            readCompleteAction?.Invoke();

            return readByte;
        }

        internal byte[] ReadBytes(int count, Action readCompleteAction = null)
        {
            _buffer = new Buffer((uint) count);

            Task.Factory.StartNew(async () =>
            {
                while (_buffer.Length < count)
                {
                    var ra = await _serialPort.InputStream.ReadAsync(_buffer, (uint) count, InputStreamOptions.Partial);
                }
            }).Wait();

            readCompleteAction?.Invoke();

            return _buffer.ToArray();
        }

        internal string ReadString(int characterCount, Action readCompleteAction = null)
        {
            _buffer = new Buffer((uint)characterCount);

            Task.Factory.StartNew(async () =>
            {
                while (_buffer.Length < characterCount)
                {
                    var ra = await _serialPort.InputStream.ReadAsync(_buffer, (uint)characterCount, InputStreamOptions.Partial);
                }
            }).Wait();

            readCompleteAction?.Invoke();

            return AsciiEncoding.GetString(_buffer.ToArray());
        }

        internal string ReadUntil(string lastCharacter, Action readCompleteAction = null)
        {
            _singleByteBuffer = new Buffer(1);
            var readString = string.Empty;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var ra = await _serialPort.InputStream.ReadAsync(_singleByteBuffer, 1, InputStreamOptions.Partial);

                    var c = AsciiEncoding.GetString(_singleByteBuffer.ToArray());

                    readString += AsciiEncoding.GetString(_singleByteBuffer.ToArray());

                    if (c.Equals(lastCharacter))
                        break;
                }
            }).Wait();

            readCompleteAction?.Invoke();

            return readString;
        }
    }
}