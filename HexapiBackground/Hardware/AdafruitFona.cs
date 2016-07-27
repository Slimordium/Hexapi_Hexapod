//using System;
//using System.Diagnostics;
//using System.Threading.Tasks;

//namespace HexapiBackground.Hardware
//{
//    internal class AdafruitFona
//    {
//        private readonly SerialPort _serialPort;

//        internal AdafruitFona()
//        {
//            _serialPort = new SerialPort(); //FTDIBUS\VID_0403+PID_6001+AH03F3RYA\0000
//        }

//        internal async Task StartAsync()
//        {
//            await _serialPort.Open("AH03F3RYA", 115200, 1000, 1000);

//            await _serialPort.Write($"ATE0\r");
//            Debug.WriteLine($"Turn echo off: {_serialPort.ReadFonaLine()}");

//            //GetSignalStrength();
//            //CloseTcpConnection(); 
//        }

//        internal async Task<string> ReadSms()
//        {
//            await _serialPort.Write("AT+CMGR=0,0\r");
//            var sms = await _serialPort.ReadString();

//            return sms;
//        }

//        internal async Task<int> GetSignalStrength()
//        {
//            try
//            {
//                await _serialPort.Write("at+csq\r");

//                var r = await _serialPort.ReadString();
//                var n = r.Split(':')[1].Trim();

//                Debug.WriteLine($"Signal Strength: {n}");

//                return int.Parse(n);
//            }
//            catch (Exception)
//            {
//                return 0;
//            }
//        }

//        internal async Task SendSms(string sms, string phoneNumber)
//        {
//            await _serialPort.Write($"AT+CMGS={phoneNumber}\r");
//            var r = _serialPort.ReadString();

//            await _serialPort.Write(sms);
//            r = _serialPort.ReadString();

//            await _serialPort.Write(char.ConvertFromUtf32(26)); //Ctrl+Z
//            r = _serialPort.ReadString();

//            Debug.WriteLine($"SMS to {phoneNumber} response : {r}");
//        }

//        internal async Task<bool> OpenTcpConnection(string ipAddress, int port)
//        {
//            await _serialPort.Write($"AT+CGATT?\r"); //Get GPRS Service status
//            Debug.WriteLine($"GPRS Status: {_serialPort.ReadFonaLine()}");

//            await _serialPort.Write("AT+CIPMODE=0\r");
//            Debug.WriteLine($"Mode set: {_serialPort.ReadFonaLine()}");

//            await _serialPort.Write($"at+cstt=\"wholesale\",\"\",\"\"\r"); //Set APN and start task
//            Debug.WriteLine($"APN Command: {_serialPort.ReadFonaLine()}");

//            await _serialPort.Write("AT+CIICR\r"); //Bring up wireless connection
//            Task.Delay(250).Wait();
//            Debug.WriteLine($"Bring up wireless connection: {_serialPort.ReadFonaLine()}");

//            await _serialPort.Write("AT+CIFSR\r"); //Get IP address
//            Task.Delay(500).Wait();
//            Debug.WriteLine($"GPRS IP Address: {_serialPort.ReadFonaLine()}");

//            await _serialPort.Write($"AT+CIPSTART=\"TCP\",\"{ipAddress}\",\"{port}\"\r");
//            Task.Delay(1500).Wait();

//            Debug.WriteLine($"TCP Connection status: {_serialPort.ReadFonaLine()}");

//            return true;
//        }

//        internal async Task CloseTcpConnection()
//        {
//            await _serialPort.Write("AT+CIPCLOSE=1\r");
//            Task.Delay(250).Wait();
//            Debug.WriteLine($"Connection Close: {_serialPort.ReadFonaLine()}");
//        }

//        internal async Task WriteTcpData(string data)
//        {
//            await _serialPort.Write($"AT+CIPSEND\r");
//            Debug.WriteLine($"Transmit: {_serialPort.ReadFonaLine()}");
//            await _serialPort.Write($"{data}"); //Queue data
//            await _serialPort.Write(new byte[1] {0x1a}); //Send data
//            Debug.WriteLine($"Transmit status: {_serialPort.ReadFonaLine()}");
//        }

//        internal async Task<byte[]> ReadTcpData()
//        {
//            var r = await _serialPort.ReadBytes();
//            Debug.WriteLine($"Incoming bytes : {r.Length}");
//            return r;
//        }
//    }
//}