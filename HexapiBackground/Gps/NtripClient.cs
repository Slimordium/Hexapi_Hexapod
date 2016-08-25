using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using HexapiBackground.Hardware;

namespace HexapiBackground.Gps.Ntrip
{
    internal class NtripClient
    {
        private readonly IPEndPoint _endPoint;
        private readonly string _ntripMountPoint; //P041_RTCM3
        private readonly string _password;
        private readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly string _username;
        private readonly SparkFunSerial16X2Lcd _display;
        private readonly IPAddress _ip;
        private readonly int _ntripPort;

        //rtgpsout.unavco.org:2101
        //69.44.86.36 

        /// <summary>
        /// </summary>
        /// <param name="ntripIpAddress"></param>
        /// <param name="ntripPort"></param>
        /// <param name="ntripMountPoint"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="display"></param>
        public NtripClient(string ntripIpAddress, int ntripPort, string ntripMountPoint, string userName, string password, SparkFunSerial16X2Lcd display)
        {
            _username = userName;
            _password = password;
            _ntripPort = ntripPort;
            _ntripMountPoint = ntripMountPoint;
            _display = display;

            try
            {
                IPAddress.TryParse(ntripIpAddress, out _ip);

                if (_ip == null)
                    return;

                _endPoint = new IPEndPoint(_ip, _ntripPort);
            }
            catch (Exception)
            {
                //
            }
        }

        internal Task<bool> InitializeAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint
            };

            args.Completed += async (sender, eventArgs) =>
            {
                if (((Socket)sender).Connected)
                {
                    await _display.WriteAsync("NTRIP Connected");
                    tcs.SetResult(true);
                }
                else
                {
                    await _display.WriteAsync("NTRIP Connection failed");
                    tcs.SetResult(false);
                }
            };

            _socket.ConnectAsync(args);

            return tcs.Task;
        }

        private Task<bool> Authenticate()
        {
            var tcs = new TaskCompletionSource<bool>();

            var buffer = new ArraySegment<byte>(CreateAuthRequest());

            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint,
                BufferList = new List<ArraySegment<byte>> { buffer }
            };

            args.Completed += async (sender, eventArgs) =>
            {
                await _display.WriteAsync($"NTRIP {eventArgs.SocketError}");
                
                tcs.SetResult(true);
            };

            _socket.SendAsync(args);

            return tcs.Task;
        }

        private byte[] CreateAuthRequest()
        {
            var msg = "GET /" + _ntripMountPoint + " HTTP/1.1\r\n"; //P041 is the mountpoint for the NTRIP station data
            msg += "User-Agent: Hexapi\r\n";

            var auth = ToBase64(_username + ":" + _password);
            msg += "Authorization: Basic " + auth + "\r\n";
            msg += "Accept: */*\r\nConnection: close\r\n";
            msg += "\r\n";

            return Encoding.ASCII.GetBytes(msg);
        }

        internal Task<byte[]> ReadNtripAsync()
        {
            var tcs = new TaskCompletionSource<byte[]>();
            var buffer = new ArraySegment<byte>(new byte[100]);

            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint,
                BufferList = new List<ArraySegment<byte>> { buffer }
            };

            args.Completed += (sender, e) =>
            {
                tcs.SetResult(e.BufferList[0].Array);
            };

            _socket.ReceiveAsync(args);

            return tcs.Task;
        }

        internal static string ToBase64(string str)
        {
            var byteArray = Encoding.ASCII.GetBytes(str);
            return Convert.ToBase64String(byteArray, 0, byteArray.Length);
        }
    }
}