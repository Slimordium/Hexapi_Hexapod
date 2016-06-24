using System;
using System.Collections.Generic;
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
        internal class DigitalPinEventArgs : EventArgs
        {
            public int Pin { get; set; }
            public PinState PinState { get; set; }
        }


        private IStream _connection;
        private RemoteDevice _arduino;

        internal event EventHandler<DigitalPinEventArgs> DigitalPinUpdated;
        internal event EventHandler<string> StringReceived;

        internal RemoteArduino()
        {

        }

        internal async Task Start()
        {
            if (_connection != null)
                return;

            var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
            var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains("AH03FK33"));

            if (selectedPort == null)
            {
                await Display.Write($"Arduino not found");
                return;
            }

            await Display.Write($"Arduino at AH03FK33");

            _connection = new UsbSerial(selectedPort); //Arduino MEGA is VID_2341 and PID_0042
            _connection.ConnectionEstablished += Connection_ConnectionEstablished;
            _connection.ConnectionFailed += Connection_ConnectionFailed;
            _connection.ConnectionLost += Connection_ConnectionLost;
            _connection.begin(57600, SerialConfig.SERIAL_8N1);
        }
        
        private async void Connection_ConnectionLost(string message)
        {
            await Display.Write("Arduino - " + message);
        }

        private async void Connection_ConnectionFailed(string message)
        {
            await Display.Write("Arduino - " + message);
        }

        private async void Arduino_DeviceConnectionFailed(string message)
        {
            await Display.Write("Arduino - " + message);
        }

        private async void Connection_ConnectionEstablished()
        {
            await Display.Write("Arduino connected");
            _arduino = new RemoteDevice(_connection);
            _arduino.DeviceConnectionFailed += Arduino_DeviceConnectionFailed;
            _arduino.DeviceReady += Arduino_DeviceReady;
        }

        private async void Arduino_DeviceReady()
        {
            await Display.Write("Arduino ready");

            _arduino.DigitalPinUpdated += Arduino_DigitalPinUpdated;
            _arduino.StringMessageReceived += Arduino_StringMessageReceived;
        }

        private void Arduino_StringMessageReceived(string message)
        {
            var e = StringReceived;
            e?.Invoke(null, message);
        }

        private async void Arduino_DigitalPinUpdated(byte pin, PinState state)
        {
            var e = DigitalPinUpdated;
            e?.Invoke(null, new DigitalPinEventArgs() {Pin = pin, PinState = state});
        }
    }
}
