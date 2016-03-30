using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HexapiBackground.Gps
{
    internal class NtripClientFona
    {
        private static readonly Encoding Encoding = new ASCIIEncoding();
        private readonly string _ntripIpAddress;
        private readonly string _ntripMountPoint; //P041_RTCM3
        private readonly int _ntripPort;
        private readonly string _password;
        private readonly SerialPort _serialPort;
        private readonly string _username;
        private readonly AdafruitFona _adafruitFona; 

        internal NtripClientFona(string ntripIpAddress, int ntripPort, string ntripMountPoint, string userName, string password, SerialPort serialPort)
        {
            Debug.WriteLine($"Using http:\\\\{ntripIpAddress}:{ntripPort}\\{ntripMountPoint}");

            _serialPort = serialPort;

            _username = userName;
            _password = password;

            _ntripIpAddress = ntripIpAddress;
            _ntripMountPoint = ntripMountPoint;
            _ntripPort = ntripPort;

            _adafruitFona = new AdafruitFona();
        }

        internal void Start()
        {
            Task.Factory.StartNew(async() =>
            {
                await Task.Delay(2000);
                _adafruitFona.Start();
                await Task.Delay(2000);

                if (!_adafruitFona.OpenTcpConnection(_ntripIpAddress, _ntripPort))//Request Fona to connect to IP and port
                {
                    return;
                }

                _adafruitFona.WriteTcpData(CreateAuthRequest()); //Authenticate and connect to feed

                while (true)
                {
                    await Task.Delay(800);
                    var r = _adafruitFona.ReadTcpData();

                    if (r.Length > 140)
                        _serialPort.Write(r); //Write to GPS
                }
            }, TaskCreationOptions.LongRunning);
        }

        private string CreateAuthRequest()
        {
            var msg = $"GET /{_ntripMountPoint} HTTP/1.1\r\n"; //P041 is the mountpoint for the NTRIP station data
            msg += "User-Agent: Hexapi\r\n";

            var auth = ToBase64($"{_username}:{_password}");
            msg += "Authorization: Basic " + auth + "\r\n";
            msg += "Accept: */*\r\nConnection: close\r\n";
            msg += "\r\n";

            return msg;
        }

        private static string ToBase64(string str)
        {
            var byteArray = Encoding.GetBytes(str);
            return Convert.ToBase64String(byteArray, 0, byteArray.Length);
        }
    }
}
