﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HexapiBackground
{
    internal class NtripClient
    {
        private static readonly Encoding _encoding = new ASCIIEncoding();
        private readonly IPEndPoint _endPoint;

        private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(false);
        private readonly string _ntripMountPoint; //P041_RTCM
        private readonly string _password;
        private readonly SerialPort _serialPort;
        private readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly string _username;

        //rtgpsout.unavco.org:2101
        //69.44.86.36

        /// <summary>
        /// </summary>
        /// <param name="ntripIpAddress"></param>
        /// <param name="ntripPort"></param>
        /// <param name="ntripMountPoint"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="serialPort"></param>
        public NtripClient(string ntripIpAddress, int ntripPort, string ntripMountPoint, string userName, string password, SerialPort serialPort)
        {
            Debug.WriteLine($"Using http:\\\\{ntripIpAddress}:{ntripPort}\\{ntripMountPoint}");

            _serialPort = serialPort;

            _username = userName;
            _password = password;

            _ntripMountPoint = ntripMountPoint;

            try
            {
                IPAddress ip;
                IPAddress.TryParse(ntripIpAddress, out ip);

                if (ip == null)
                {
                    Debug.WriteLine("Ntrip IP was null?");
                    return;
                }

                _endPoint = new IPEndPoint(IPAddress.Parse(ntripIpAddress), ntripPort);

                Connect();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        internal byte[] CreateAuthRequest()
        {
            var msg = "GET /" + _ntripMountPoint + " HTTP/1.1\r\n"; //P041 is the mountpoint for the NTRIP station data
            msg += "User-Agent: Hexapi\r\n";

            var auth = ToBase64(_username + ":" + _password);
            msg += "Authorization: Basic " + auth + "\r\n";
            msg += "Accept: */*\r\nConnection: close\r\n";
            msg += "\r\n";

            return Encoding.ASCII.GetBytes(msg);
        }

        internal void Connect()
        {
            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint
            };

            args.Completed += (sender, eventArgs) =>
            {
                if (((Socket) sender).Connected)
                {
                    Debug.WriteLine($"Connected to NTRIP feed at {_ntripMountPoint}");
                    Authenticate();
                }
                else
                {
                    Debug.WriteLine("NTRIP connection failed");
                }
            };

            _socket.ConnectAsync(args);
        }

        internal void Authenticate()
        {
            var buffer = new ArraySegment<byte>(CreateAuthRequest());

            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint,
                BufferList = new List<ArraySegment<byte>> {buffer}
            };

            args.Completed += (sender, eventArgs) =>
            {
                Debug.WriteLine($"NTRIP Authentication : {eventArgs.SocketError.ToString()}");

                Task.Delay(1000).Wait();

                _manualResetEventSlim.Set();
            };

            _socket.SendAsync(args);
        }

        internal void ReadData()
        {
            var buffer = new ArraySegment<byte>(new byte[512]);

            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint,
                BufferList = new List<ArraySegment<byte>> {buffer}
            };

            args.Completed += (sender, eventArgs) =>
            {
                var data = new byte[eventArgs.BytesTransferred];

                Array.Copy(eventArgs.BufferList[0].Array, data, eventArgs.BytesTransferred);

                //var stringData = Encoding.ASCII.GetString(eventArgs.BufferList[0].Array, eventArgs.Offset, eventArgs.BytesTransferred);

                SendToGps(data);

                //Debug.WriteLine(stringData);

                Debug.WriteLine($"Bytes : {eventArgs.BytesTransferred}");

                _manualResetEventSlim.Set();
            };

            _socket.ReceiveAsync(args);
        }

        public void Start()
        {
            Task.Factory.StartNew(() =>
            {
                _stopwatch.Start();

                while (true)
                {
                    _manualResetEventSlim.Wait();
                    _manualResetEventSlim.Reset();

                    ReadData();
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void SendToGps(byte[] data)
        {
            if (data.Length < 10)
                return;

            _serialPort.Write(data);
        }

        internal static string ToBase64(string str)
        {
            var byteArray = _encoding.GetBytes(str);
            return Convert.ToBase64String(byteArray, 0, byteArray.Length);
        }
    }
}