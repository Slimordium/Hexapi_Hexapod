using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.Devices.SerialCommunication;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;

namespace HexapiBackground
{
    sealed internal class RemoteArduino
    {
        IStream _connection;
        RemoteDevice _arduino;
        private bool _isInitialized;

        internal Action<int[]> RangeUpdate { private get; set; }

        internal void Start()
        {
            Task.Factory.StartNew(async () =>
            {
                if (_isInitialized) return;

                _isInitialized = true;

                var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains("AI041V40A"));

                Debug.WriteLine($"Found - Arduino UNO at {selectedPort.Id}");

                _connection = new UsbSerial(selectedPort); //Arduino MEGA is VID_2341 and PID_0042
                _connection.ConnectionEstablished += _connection_ConnectionEstablished;
                _connection.ConnectionFailed += _connection_ConnectionFailed;
                _connection.ConnectionLost += _connection_ConnectionLost;
                _connection.begin(57600, SerialConfig.SERIAL_8N1);
            });
        }

        private void _connection_ConnectionLost(string message)
        {
            Debug.WriteLine("Connection to the Arduino was lost : " + message);
        }

        private void _connection_ConnectionFailed(string message)
        {
            Debug.WriteLine("Serial connection to the Arduino failed. Probably a USB problem");
        }

        private void _arduino_DeviceConnectionFailed(string message)
        {
            Debug.WriteLine("Arduino connection failed - " + message);
        }

        private void _connection_ConnectionEstablished()
        {
            Debug.WriteLine("Serial connection for the Arduino UNO to the FTDI UART established");
            _arduino = new RemoteDevice(_connection);
            _arduino.DeviceConnectionFailed += _arduino_DeviceConnectionFailed;
            _arduino.DeviceReady += _arduino_DeviceReady;
        }

        private void _arduino_DeviceReady()
        {
            Debug.WriteLine("Arduino UNO communication successfully negotiated");

            //_arduino.DigitalPinUpdated += _arduino_DigitalPinUpdated;
            _arduino.StringMessageReceived += _arduino_StringMessageReceived;
        }

        private void _arduino_StringMessageReceived(string message)
        {
            var array = (message.Split(',').Select(int.Parse).ToArray());

            RangeUpdate?.Invoke(array);

            //Debug.WriteLine($"{message}");
        }

        private void _arduino_DigitalPinUpdated(byte pin, PinState state)
        {
            Debug.WriteLine($"Digital pin state changed - pin: {pin}, state: {state}");
        }
    }
}
