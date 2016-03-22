using System;
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
            Debug.WriteLine($"Turn echo off: {_serialPort.ReadFonaLine()}");

            //GetSignalStrength();
            //CloseTcpConnection(); 
        }

        internal string ReadSms()
        {
            _serialPort.Write("AT+CMGR=0,0\r");
            var sms = _serialPort.ReadString();

            return sms;
        }

        internal int GetSignalStrength()
        {
            try
            {
                _serialPort.Write("at+csq\r");

                var r = _serialPort.ReadString();
                var n = r.Split(':')[1].Trim();

                Debug.WriteLine($"Signal Strength: {n}");

                return int.Parse(n);
            }
            catch (Exception)
            {
                return 0;
            }
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

        internal bool OpenTcpConnection(string ipAddress, int port)
        {
            _serialPort.Write($"AT+CGATT?\r"); //Get GPRS Service status
            Debug.WriteLine($"GPRS Status: {_serialPort.ReadFonaLine()}");

            _serialPort.Write("AT+CIPMODE=0\r");
            Debug.WriteLine($"Mode set: {_serialPort.ReadFonaLine()}");

            _serialPort.Write($"at+cstt=\"wholesale\",\"\",\"\"\r"); //Set APN and start task
            Debug.WriteLine($"APN Command: {_serialPort.ReadFonaLine()}");

            _serialPort.Write("AT+CIICR\r"); //Bring up wireless connection
            Task.Delay(250).Wait();
            Debug.WriteLine($"Bring up wireless connection: {_serialPort.ReadFonaLine()}");

            _serialPort.Write("AT+CIFSR\r"); //Get IP address
            Task.Delay(500).Wait();
            Debug.WriteLine($"GPRS IP Address: {_serialPort.ReadFonaLine()}");

            _serialPort.Write($"AT+CIPSTART=\"TCP\",\"{ipAddress}\",\"{port}\"\r");
            Task.Delay(1500).Wait();

            Debug.WriteLine($"TCP Connection status: {_serialPort.ReadFonaLine()}");

            return true;
        }

        internal void CloseTcpConnection()
        {
            _serialPort.Write("AT+CIPCLOSE=1\r");
            Task.Delay(250).Wait();
            Debug.WriteLine($"Connection Close: {_serialPort.ReadFonaLine()}");
        }

        internal void WriteTcpData(string data)
        {
            _serialPort.Write($"AT+CIPSEND\r");
            Debug.WriteLine($"Transmit: {_serialPort.ReadFonaLine()}");
            _serialPort.Write($"{data}"); //Queue data
            _serialPort.Write(new byte[1] {0x1a}); //Send data
            Debug.WriteLine($"Transmit status: {_serialPort.ReadFonaLine()}");
        }

        internal byte[] ReadTcpData()
        {
            var r = _serialPort.ReadBytes();
            Debug.WriteLine($"Incoming bytes : {r.Length}");
            return r;
        }

    }
}