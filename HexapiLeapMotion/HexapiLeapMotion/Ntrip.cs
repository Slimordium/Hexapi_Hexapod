using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace HexapSignalRServer
{
    [HubName("Ntrip")]
    public class Ntrip : Hub
    {
        private readonly Gps _gps = Gps.Instance;

        public Ntrip()
        {
        }

        public override Task OnConnected()
        {
            Debug.WriteLine("Client connected");
            _gps.UpdateClient = UpdateGps;
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            Debug.WriteLine("Client disconnected");
            return base.OnDisconnected(stopCalled);
        }

        private async void UpdateGps(byte[] bytes)
        {
            Debug.WriteLine($"SignalR client update called with {bytes.Length} to send");

            var methodToCall = "updateGps";
            IClientProxy proxy = Clients.All;
            await proxy.Invoke(methodToCall, bytes);

            //await Clients.All.updateGps(bytes);
        }
    }

    internal class Gps
    {
        private static Gps _instance;
        private readonly SerialPort _serialPort;
        private readonly ManualResetEventSlim _mre = new ManualResetEventSlim(false);
        private static readonly object InstanceLock = new object();

        private Gps()
        {
            lock (InstanceLock)
            {
                try
                {
                    _serialPort = new SerialPort("COM5", 57600);
                    _serialPort.Open();

                    Debug.WriteLine($"SerialPort IsOpen : {_serialPort.IsOpen}");
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine("SerialPort open failed, exiting...");
                    return;
                }

                StartReading();
            }
        }

        private void StartReading()
        {
            Task.Factory.StartNew(() =>
            {
                _serialPort.ReceivedBytesThreshold = 200;
                _serialPort.DataReceived += _serialPort_DataReceived;

                var buffer = new List<byte>();

                while (true)
                {
                    _mre.Wait(15000);
                    _mre.Reset();

                    try
                    {
                        var readBuffer = new byte[_serialPort.BytesToRead];

                        _serialPort.Read(readBuffer, 0, readBuffer.Length);
                        buffer.AddRange(readBuffer);

                        if (buffer.Count > 200)
                        {
                            Debug.WriteLine($"Sending {buffer.Count} to client");
                            UpdateClient?.Invoke(buffer.ToArray());
                            buffer = new List<byte>();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _mre.Set();
        }

        internal Action<byte[]> UpdateClient { get; set; }

        internal static Gps Instance => _instance ?? (_instance = new Gps());
    }
}