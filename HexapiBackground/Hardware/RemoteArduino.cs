using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;

namespace HexapiBackground.Hardware
{
    internal sealed class RemoteArduino
    {
        private IStream _connection;
        private RemoteDevice _arduino;

        public static List<Action<string>> StringReceivedActions { get; set; }
        public static List<Action<byte, PinState>>  DigitalPinUpdatedActions { get; set; }

        internal RemoteArduino()
        {
            StringReceivedActions = new List<Action<string>>();
            DigitalPinUpdatedActions = new List<Action<byte, PinState>>();
        }

        internal void Start()
        {
            Task.Factory.StartNew(async () =>
            {
                if (_connection != null)
                    return;

                while (_connection == null)
                {
                    var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                    var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains("AI041V40A"));//AI041V40A is the serial number of the FTDI chip on the SparkFun USB/Serial adapter

                    if (selectedPort == null)
                    {
                        Debug.WriteLine($"Could not find FTDI UART for Arduino at AI041V40A. Retrying in 2 seconds... ");

                        await Task.Delay(2000);
                        continue;
                    }

                    Debug.WriteLine($"Found - Arduino at {selectedPort.Id}");

                    _connection = new UsbSerial(selectedPort); //Arduino MEGA is VID_2341 and PID_0042
                    _connection.ConnectionEstablished += Connection_ConnectionEstablished;
                    _connection.ConnectionFailed += Connection_ConnectionFailed;
                    _connection.ConnectionLost += Connection_ConnectionLost;
                    _connection.begin(57600, SerialConfig.SERIAL_8N1);
                }
            });
        }

        private void Connection_ConnectionLost(string message)
        {
            Debug.WriteLine("Connection to the Arduino was lost : " + message);
        }

        private void Connection_ConnectionFailed(string message)
        {
            Debug.WriteLine("Serial connection to the Arduino failed. Probably a USB/Serial problem.");
        }

        private void Arduino_DeviceConnectionFailed(string message)
        {
            Debug.WriteLine("Arduino connection failed - " + message);
        }

        private void Connection_ConnectionEstablished()
        {
            Debug.WriteLine("Serial connection for the Arduino to the FTDI UART established");
            _arduino = new RemoteDevice(_connection);
            _arduino.DeviceConnectionFailed += Arduino_DeviceConnectionFailed;
            _arduino.DeviceReady += Arduino_DeviceReady;
        }

        private void Arduino_DeviceReady()
        {
            Debug.WriteLine("Arduino communication successfully negotiated");

            _arduino.DigitalPinUpdated += Arduino_DigitalPinUpdated;
            _arduino.StringMessageReceived += Arduino_StringMessageReceived;
        }

        private void Arduino_StringMessageReceived(string message)
        {
            foreach (var a in StringReceivedActions)
            {
                a.Invoke(message);
            }
        }

        private void Arduino_DigitalPinUpdated(byte pin, PinState state)
        {
            Debug.WriteLine($"Digital pin state changed - pin: {pin}, state: {state}");

            foreach (var a in DigitalPinUpdatedActions)
            {
                a.Invoke(pin, state);
            }
        }
    }
}
