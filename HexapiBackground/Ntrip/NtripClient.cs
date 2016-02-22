using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HexapiBackground
{
    internal class NtripClient
    {
        private readonly Socket _socket;
        private readonly string _username;
        private readonly string _password;
        private readonly int _ntripPort;
        private readonly string _ntripIpAddress;
        private readonly string _ntripMountPoint; //P041

        //rtgpsout.unavco.org:2101
        //69.44.86.36
        public NtripClient(string userName, string password, string ntripIpAddress, int ntripPort, string ntripMountPoint)
        {
            _username = userName; 
            _password = password; 

            _ntripIpAddress = ntripIpAddress;
            _ntripPort = ntripPort;
            _ntripMountPoint = ntripMountPoint;

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _socketAsyncEventArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_ntripIpAddress), _ntripPort),
                UserToken = _socket
            };

            _socketAsyncEventArgs.Completed += E_Completed;
        }

        private byte[] CreateRequest()
        {
            var msg = "GET /" + _ntripMountPoint + " HTTP/1.1\r\n"; //P041 is the mountpoint for the NTRIP station data
            msg += "User-Agent: Hexapi\r\n";
            
            var auth = ToBase64(_username + ":" + _password);
            msg += "Authorization: Basic " + auth + "\r\n";
            msg += "Accept: */*\r\nConnection: close\r\n";
            msg += "\r\n";

            return Encoding.ASCII.GetBytes(msg);
        }

        private SocketAsyncEventArgs _socketAsyncEventArgs;


        private void Connect()
        {
            _socket.ConnectAsync(_socketAsyncEventArgs);
        }

        private void E_Completed(object sender, SocketAsyncEventArgs e)
        {
            _socketAsyncEventArgs = e;
            _wait.Set();
        }

        ManualResetEvent _wait = new ManualResetEvent(false);

        private void Close()
        {
            _socket.Shutdown(SocketShutdown.Both);
        }

        public void Start()
        {
            var request = CreateRequest();

            _socketAsyncEventArgs.SetBuffer(request, 0, request.Length);

            _socket.SendAsync(_socketAsyncEventArgs);

            _wait.WaitOne();
            _wait.Reset();

            var dataForGps = Encoding.ASCII.GetString(_socketAsyncEventArgs.Buffer, _socketAsyncEventArgs.Offset, _socketAsyncEventArgs.BytesTransferred);
        }

        readonly Encoding _encoding = new ASCIIEncoding();

        private string ToBase64(string str)
        {
            var byteArray = _encoding.GetBytes(str);
            return Convert.ToBase64String(byteArray, 0, byteArray.Length);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
