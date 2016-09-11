using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
//using HexapiBackground.Iot;

namespace HexapiBackground.Hardware
{
    internal sealed class Gps
    {
        private SerialDevice _gpsSerialDevice;
        private static SparkFunSerial16X2Lcd _display;
        //private static IoTClient _ioTClient;

        private DataReader _inputStream;
        private DataWriter _outputStream;

        private static readonly object _fixDataLock = new object();

        private static GpsFixData _gpsFixData;

        internal static GpsFixData CurrentGpsFixData
        {
            get
            {
                lock (_fixDataLock)
                    return _gpsFixData;
            }
        }

        //private Timer _iotUpdateTimer = new Timer(async o => { await SendToIotHub(); }, null, 0, 1000);

        //private static async Task SendToIotHub()
        //{
        //    if (CurrentGpsFixData == null)
        //        return;

        //    //await _ioTClient.SendEventAsync("GPS", CurrentGpsFixData.ToString());
        //}

        internal Gps(SparkFunSerial16X2Lcd display)
        {
            _display = display;
            //_ioTClient = ioTClient;
        }

        internal async Task<bool> InitializeAsync()
        {
            _gpsSerialDevice = await StartupTask.SerialDeviceHelper.GetSerialDeviceAsync("AH03F3RYA", 57600, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

            if (_gpsSerialDevice == null)
                return false;

            _inputStream = new DataReader(_gpsSerialDevice.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            _outputStream = new DataWriter(_gpsSerialDevice.OutputStream);

            return true;
        }

        internal async Task StartAsync()
        {
            //if (!_gpsReset)
            //{
            //    await StartupTask.ResetGps();
            //    _gpsReset = true;
            //}

            while (true)
            {
                if (_inputStream == null)
                {
                    await Task.Delay(500);
                    continue;
                }

                await ReadGps();
            }
        }

        private async Task ReadGps() 
        {
            var byteCount = await _inputStream.LoadAsync(1024).AsTask();
            var bytes = new byte[byteCount];
            _inputStream.ReadBytes(bytes);

            var sentences = Encoding.ASCII.GetString(bytes).Split('\n');

            if (sentences.Length == 0)
                return;

            foreach (var sentence in sentences)
            {
                if (!sentence.StartsWith("$"))
                    continue;

                var data = sentence.ParseNmea();

                if (data == null)
                    continue;

                lock (_fixDataLock)
                    _gpsFixData = data;
            }

        }

        internal async Task StartRtkUdpFeedAsync()
        {
            var receivingUdpClient = new UdpClient(8000);

            while (true)
            {
                var udpReceiveResult = await receivingUdpClient.ReceiveAsync();
                _outputStream.WriteBytes(udpReceiveResult.Buffer);
                await _outputStream.StoreAsync();
            }
        }

        private Timer _statusTimer = new Timer(async sender =>
        {
            if (CurrentGpsFixData == null)
                return;

            await _display.WriteAsync($"{CurrentGpsFixData.Quality} S{CurrentGpsFixData.SatellitesInView}", 1);
            await _display.WriteAsync($"RR{CurrentGpsFixData.RtkRatio}, RA{CurrentGpsFixData.RtkAge}, SNR{CurrentGpsFixData.SignalToNoiseRatio}", 2);

            //Debug.WriteLine($"{CurrentGpsFixData.Quality} fix, satellites {CurrentGpsFixData.SatellitesInView}");
            //Debug.WriteLine($"{CurrentGpsFixData.Lat},{CurrentGpsFixData.Lon}");
            //Debug.WriteLine($"Rtk ratio {CurrentGpsFixData.RtkRatio}, rtk age {CurrentGpsFixData.RtkAge}, SNR {CurrentGpsFixData.SignalToNoiseRatio}");
            //Debug.WriteLine($"{byteCount}");
            //byteCount = 0;
        }, null, 0, 3000);
    }
}