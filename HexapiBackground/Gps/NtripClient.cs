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
        private static readonly Encoding Encoding = new ASCIIEncoding();
        private readonly IPEndPoint _endPoint;
        private readonly string _ntripMountPoint; //P041_RTCM3
        private readonly string _password;
        private readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly string _username;
        private readonly SparkFunSerial16X2Lcd _display;
        internal event EventHandler<NtripEventArgs> NtripDataArrivedEvent;

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

            _ntripMountPoint = ntripMountPoint;

            _display = display;

            try
            {
                IPAddress ip;
                IPAddress.TryParse(ntripIpAddress, out ip);

                if (ip == null)
                {
                    return;
                }

                _endPoint = new IPEndPoint(ip, ntripPort);
            }
            catch (Exception e)
            {
                return;
            }

            Connect();
        }

        private void Connect()
        {
            var args = new SocketAsyncEventArgs
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint
            };

            args.Completed += async (sender, eventArgs) =>
            {
                if (((Socket)sender).Connected)
                {
                    await _display.Write("NTRIP Connected");

                    await Task.Delay(500);

                    Authenticate();
                }
                else
                {
                    await _display.Write("NTRIP Connection failed");
                }
            };

            _socket.ConnectAsync(args);
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

        private void Authenticate()
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
                await _display.Write($"NTRIP {eventArgs.SocketError}");

                await Task.Delay(1500);

                ReadData();
            };

            _socket.SendAsync(args);
        }

        private void ReadData()
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
            if (e.BytesTransferred > 0)
            {
                var data = new byte[e.BytesTransferred];

                Array.Copy(e.BufferList[0].Array, data, e.BytesTransferred);
                await SendToGps(data);
            }

            ReadData();
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