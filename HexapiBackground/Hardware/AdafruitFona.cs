using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HexapiBackground
{
    internal class AdafruitFona
    {
        private SerialPort _serialPort;

        internal AdafruitFona()
        {
            _serialPort = new SerialPort("AH03F3RYA", 115200, 1000, 1000); //FTDIBUS\VID_0403+PID_6001+AH03F3RYA\0000
        }

        internal void Start()
        {
            _serialPort.Write($"ATE0\r");

            Debug.WriteLine(_serialPort.ReadFonaLine());


        }

        internal string ReadSms()
        {
            _serialPort.Write("AT+CMGR=0,0\r");
            var sms = _serialPort.ReadString();

            return sms;
        }

        internal int GetSignalStrength()
        {
            _serialPort.Write("at+csq\r");

            var r = _serialPort.ReadString();
            var n = r.Split(':')[1].Trim();

            Debug.WriteLine(n);

            return int.Parse(n);
        }

        internal void SendSms(string sms, string phoneNumber)
        {
            _serialPort.Write($"AT+CMGS={phoneNumber}\r");
            var r = _serialPort.ReadString();

            _serialPort.Write(sms);
            r = _serialPort.ReadString();

            _serialPort.Write(char.ConvertFromUtf32(26)); //Ctrl+Z
            r = _serialPort.ReadString();

            Debug.WriteLine($"SMS to {phoneNumber} response : {r}");
        }

        internal void OpenTcpTransparentConnection(string ipAddress, int port)
        {
            //Get GSM / GPRS connection status

            _serialPort.Write($"AT+CGATT?\r"); //Get GPRS Service status
            var r = _serialPort.ReadFonaLine();
            Debug.WriteLine($"GSM/GPRS Status {r}");

            Debug.WriteLine($"{r}");

            _serialPort.Write("AT+CIPMODE=0\r");
            r = _serialPort.ReadFonaLine();

            Debug.WriteLine($"{r}");

            _serialPort.Write($"at+cstt=\"wholesale\",\"\",\"\"\r"); //Set APN and start task

            Debug.WriteLine($"APN Command: {_serialPort.ReadFonaLine()}");


            _serialPort.Write("AT+CIICR\r"); //Bring up wireless connection

            Task.Delay(250).Wait();

            r = _serialPort.ReadFonaLine();

            Debug.WriteLine($"{r}");

            _serialPort.Write("AT+CIFSR\r"); //Get IP address

            Task.Delay(250).Wait();
            r = _serialPort.ReadFonaLine();

            Debug.WriteLine($"GSM/GPRS IP Address {r}");

            _serialPort.Write($"AT+CIPSTART=\"TCP\",\"{ipAddress}\",\"{port}\"\r");

            Task.Delay(1500).Wait();

            r = _serialPort.ReadString();

            Debug.WriteLine($"TCP Connection status {r}");

            //r = _serialPort.ReadString();

            //Debug.WriteLine($"{r}");

            _tcpConnectionOpen = true;
        }

        internal bool CloseTcpTransparentConnection()
        {
            Task.Delay(1000).Wait();
            _serialPort.Write("+++");
            Task.Delay(1000).Wait();

            return true;
        }

        internal byte[] CreateAuthRequest()
        {
            var msg = "GET /P041_RTCM HTTP/1.1\r\n"; //P041 is the mountpoint for the NTRIP station data
            msg += "User-Agent: Hexapi\r\n";

            var auth = NtripClient.ToBase64("lwatkins:D2q02425");
            msg += "Authorization: Basic " + auth + "\r\n";
            msg += "Accept: */*\r\nConnection: close\r\n";
            msg += "\r\n";

            return Encoding.ASCII.GetBytes(msg);
        }


        private bool _tcpConnectionOpen;

        internal void OpenTcpConnection(string ipAddress, int port)
        {
            //Get GSM / GPRS connection status

            _serialPort.Write($"AT+CGATT?\r"); //Get GPRS Service status
            var r = _serialPort.ReadString();
            Debug.WriteLine($"GSM/GPRS Status {r}");

            _serialPort.Write("AT+CIPMODE=0\r"); //Transparent mode

            //_serialPort.Write("AT+CIPCCFG=1\r");

            _serialPort.Write("AT+CSTT=\"wholesale\"\r"); //Start task and set APN

            _serialPort.Write("AT+CIICR\r"); //Bring up wireless connection
            _serialPort.Write("AT+CIFSR\r"); //Get IP address
            r = _serialPort.ReadString();
            Debug.WriteLine($"GSM/GPRS IP Address {r}");

            _serialPort.Write($"AT+CIPSTART=\"TCP\",\"{ipAddress}\",\"{port}\"\r");
            r = _serialPort.ReadString();
            Debug.WriteLine($"TCP Connection status {r}");

            _tcpConnectionOpen = true;
        }

        internal void WriteTcpData(byte[] data)
        {
            if (!_tcpConnectionOpen)
                return;

            _serialPort.Write($"AT+CIPSEND\r");
            Debug.WriteLine(_serialPort.ReadString());

            //?
            _serialPort.Write($"{data}");

            //var q = _serialPort.ReadString();
            //Debug.WriteLine(q);


            _serialPort.Write(new byte[1] { 0x1a });

            _serialPort.Write($"\r");

            Debug.WriteLine(_serialPort.ReadString());


            //var q = _serialPort.ReadString();
            //Debug.WriteLine(q);





            Task.Delay(1000).Wait();

            var r = _serialPort.ReadBytes();

            Debug.WriteLine($"{Encoding.ASCII.GetString(r)}");
        }

        internal string ReadTcpData(int length)
        {
            if (!_tcpConnectionOpen)
                return string.Empty;

            _serialPort.Write($"AT+CIPSTATUS\r");

            var r = _serialPort.ReadString();
            Debug.WriteLine($"TCP Read : {r}");

            return r;
        }

    }
}