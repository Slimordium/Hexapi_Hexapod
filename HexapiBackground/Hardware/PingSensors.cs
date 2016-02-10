using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;

namespace HexapiBackground
{
    internal sealed class PingSensors
    {
        private IStream _connection;
        private RemoteDevice _arduino;

        private readonly List<int> _centerAvg = new List<int>();
        private readonly List<int> _leftAvg = new List<int>();
        private readonly List<int> _rightAvg = new List<int>();

        internal static int LeftInches { get; private set; }
        internal static int CenterInches { get; private set; }
        internal static int RightInches { get; private set; }

        internal PingSensors()
        {
            LeftInches = 0;
            CenterInches = 0;
            RightInches = 0;
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
                        Debug.WriteLine($"Could not find FTDI UART for Arduino UNO at AI041V40A. Retrying in 2 seconds... ");

                        await Task.Delay(2000);
                        continue;
                    }

                    Debug.WriteLine($"Found - Arduino UNO at {selectedPort.Id}");

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
            Debug.WriteLine("Serial connection for the Arduino UNO to the FTDI UART established");
            _arduino = new RemoteDevice(_connection);
            _arduino.DeviceConnectionFailed += Arduino_DeviceConnectionFailed;
            _arduino.DeviceReady += Arduino_DeviceReady;
        }

        private void Arduino_DeviceReady()
        {
            Debug.WriteLine("Arduino UNO communication successfully negotiated");

            //_arduino.DigitalPinUpdated += _arduino_DigitalPinUpdated;
            _arduino.StringMessageReceived += Arduino_StringMessageReceived;
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round((duration / 73.746) / 2, 1));
        }

        internal void RangeUpdate(int[] data)
        {
            if (data.Length < 3)
            {
                Debug.WriteLine("Bad ping data");
                return;
            }

            _leftAvg.Add(data[0]);
            _centerAvg.Add(data[1]);
            _rightAvg.Add(data[2]);

            if (_leftAvg.Count <= 3)
                return;

            LeftInches = GetInchesFromPingDuration(_leftAvg.Sum() / _leftAvg.Count);
            _leftAvg.RemoveAt(0);

            RightInches = GetInchesFromPingDuration(_rightAvg.Sum() / _rightAvg.Count);
            _rightAvg.RemoveAt(0);

            CenterInches = GetInchesFromPingDuration(_centerAvg.Sum() / _centerAvg.Count);
            _centerAvg.RemoveAt(0);
        }

        private void Arduino_StringMessageReceived(string message)
        {
            try
            {
                RangeUpdate((message.Split(',').Select(int.Parse).ToArray()));
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Range Update exception : {e.Message}");
            }
        }

        private void Arduino_DigitalPinUpdated(byte pin, PinState state)
        {
            Debug.WriteLine($"Digital pin state changed - pin: {pin}, state: {state}");
        }
    }
}
