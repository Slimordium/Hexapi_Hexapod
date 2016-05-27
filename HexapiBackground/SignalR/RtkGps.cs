using System;
using System.Diagnostics;
using System.Linq;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
using Microsoft.AspNet.SignalR.Client;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground.SignalR
{
    internal class RtkGps
    {
        private readonly IHubProxy _hubProxy;
        private readonly HubConnection _rtkGps;
        private readonly SerialPort _serialPort;

        internal RtkGps()
        {
            _rtkGps = new HubConnection("http://172.16.0.224:8080/signalr");

            try
            {
                _hubProxy = _rtkGps.CreateHubProxy("Ntrip");
                _hubProxy.On<byte[]> ("UpdateGps", RtkUpdate);

                _rtkGps.StateChanged += HexapiControllerConnectionStateChanged;

                _rtkGps.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            _serialPort = new SerialPort();
            _serialPort.Open("A104OHRX", 57600, 2000, 2000).Wait();
        }

        private void HexapiControllerConnectionStateChanged(StateChange obj)
        {
            Debug.WriteLine($"Ntrip OldState : {obj.OldState}");
            Debug.WriteLine($"Ntrip NewState : {obj.NewState}");
        }

        public void RtkUpdate(byte[] bytes)
        {
            Debug.WriteLine($"Incoming {bytes.Length} from Ntrip server");

            try
            {
                _serialPort?.Write(bytes);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }
}