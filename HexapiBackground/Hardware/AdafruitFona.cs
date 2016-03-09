using System.Diagnostics;
using System.Threading.Tasks;

namespace HexapiBackground
{
    internal class AdafruitFona
    {
        private SerialPort _serialPort;

        internal AdafruitFona()
        {


        }

        internal void Start()
        {
            _serialPort = new SerialPort("AH03F3RYA", 115200, 500, 500); //FTDIBUS\VID_0403+PID_6001+AH03F3RYA\0000
        
            _serialPort.Write($"at+cstt=\"wholesale\",\"\",\"\"\r"); //Set APN
            Task.Delay(500).Wait();
            //_serialPort.Write($"at+ciicr\r"); //Connect
            //Task.Delay(500).Wait();
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

        internal bool OpenTcpTransparentConnection(string ipAddress, int port)
        {
            //Get GSM / GPRS connection status

            _serialPort.Write($"AT+CGATT?\r"); //Get GPRS Service status
            var r = _serialPort.ReadString();
            Debug.WriteLine($"GSM/GPRS Status {r}");

            _serialPort.Write("AT+CIPMODE=1\r"); //Transparent mode
            r = _serialPort.ReadString();

            //_serialPort.Write("AT+CIPCCFG=1\r");

            _serialPort.Write("AT+CSTT=\"wholesale\"\r"); //Start task and set APN
            r = _serialPort.ReadString();

            _serialPort.Write("AT+CIICR\r"); //Bring up wireless connection
            r = _serialPort.ReadString();

            _serialPort.Write("AT+CIFSR\r"); //Get IP address
            r = _serialPort.ReadString();

            Debug.WriteLine($"GSM/GPRS IP Address {r}");

            _serialPort.Write($"AT+CIPSTART=\"TCP\",\"{ipAddress}\",\"{port}\"\r");
            r = _serialPort.ReadString();

            Debug.WriteLine($"TCP Connection status {r}");

            return true;
        }

        internal bool CloseTcpTransparentConnection()
        {
            Task.Delay(1000).Wait();
            _serialPort.Write("+++");
            Task.Delay(1000).Wait();

            return true;
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

        internal void WriteTcpData(string data)
        {
            if (!_tcpConnectionOpen)
                return;

            _serialPort.Write("AT+CIPSEND\r");

            //?
            _serialPort.Write($"{data}\r");

            var r = _serialPort.ReadString();
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