using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using HexapiBackground.Hardware;

namespace HexapiBackground.Gps.Ntrip
{
    internal class NtripClientTcp
    {
        private static readonly Encoding Encoding = new ASCIIEncoding();
        private readonly IPEndPoint _endPoint;
        private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(false);
        private readonly string _ntripMountPoint; //P041_RTCM3
        private readonly string _password;
        private readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
        public NtripClientTcp(string ntripIpAddress, int ntripPort, string ntripMountPoint, string userName, string password)
        {
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

                _endPoint = new IPEndPoint(ip, ntripPort);

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

            var r = Encoding.ASCII.GetBytes(msg);

            Debug.WriteLine(r);

            return r;
        }

        internal void Connect()
        {
            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint
            };

            args.Completed += async (sender, eventArgs) =>
            {
                await Task.Run(async () =>
                {
                    if (((Socket) sender).Connected)
                    {
                        Display.Write("NTRIP Connected");

                        await Task.Delay(500);

                        Authenticate();
                    }
                    else
                    {
                        Display.Write("NTRIP Connection failed");
                    }
                });
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

            args.Completed += async (sender, eventArgs) =>
            {
                await Task.Run(async () =>
                {
                    Debug.WriteLine($"NTRIP Authentication : {eventArgs.SocketError.ToString()}");

                    Display.Write($"NTRIP {eventArgs.SocketError.ToString()}");

                    await Task.Delay(1500);

                    _manualResetEventSlim.Set();
                });
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

            args.Completed += Args_Completed;

            _socket.ReceiveAsync(args);
        }

        private async void Args_Completed(object sender, SocketAsyncEventArgs e)
        {
            await Task.Run(async () =>
            {
                var data = new byte[e.BytesTransferred];

                Array.Copy(e.BufferList[0].Array, data, e.BytesTransferred);

                if (e.BytesTransferred == 0)
                {
                    _manualResetEventSlim.Set();
                    return;
                }

                await SendToGps(data);

                _manualResetEventSlim.Set();
            });
        }

        public void Start()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    _manualResetEventSlim.Wait(5000);
                    _manualResetEventSlim.Reset();

                    ReadData();
                }
            });
        }

        private async Task SendToGps(byte[] data)
        {
            await Task.Run(() =>
            {
                var handler = NtripDataArrivedEvent;

                if (handler != null && data.Length > 1)
                {
                   handler.Invoke(this, new NtripEventArgs(data));
                }
            });
        }

        internal static string ToBase64(string str)
        {
            var byteArray = Encoding.GetBytes(str);
            return Convert.ToBase64String(byteArray, 0, byteArray.Length);
        }

        internal event EventHandler<NtripEventArgs> NtripDataArrivedEvent;
    }

    internal class NtripEventArgs : EventArgs
    {
        internal NtripEventArgs(byte[] data)
        {
            NtripBytes = data;
        }

        internal byte[] NtripBytes { get; set; }    
    }
}