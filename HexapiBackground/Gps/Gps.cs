using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Gps;
using HexapiBackground.Iot;

namespace HexapiBackground.Hardware
{
    internal sealed class Gps
    {
        private SerialDevice _gpsSerialDevice;
        private static SparkFunSerial16X2Lcd _display;
        private static IoTClient _ioTClient;

        private DataReader _inputStream;

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

        private Timer _iotUpdateTimer = new Timer(async o => { await SendToIotHub(); }, null, 0, 1000);

        private static async Task SendToIotHub()
        {
            if (CurrentGpsFixData == null)
                return;

            await _ioTClient.SendEventAsync("GPS", CurrentGpsFixData.ToString());
        }

        internal Gps(SparkFunSerial16X2Lcd display, IoTClient ioTClient)
        {
            _display = display;
            _ioTClient = ioTClient;
        }

        internal async Task<bool> InitializeAsync()
        {
            _gpsSerialDevice = await StartupTask.SerialDeviceHelper.GetSerialDeviceAsync("BCM2836", 57600, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));

            if (_gpsSerialDevice == null)
                return false;

            _inputStream = new DataReader(_gpsSerialDevice.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            return true;
        }

        internal async Task SaveWaypoint()
        {
            await _display.WriteAsync("Saving wp");
            await Task.Delay(500);
            await _display.WriteAsync($"{CurrentGpsFixData.Lat}", 1);
            await _display.WriteAsync($"{CurrentGpsFixData.Lon}", 2);
            await CurrentGpsFixData.SaveWaypoint();
            await Task.Delay(1500);
            await _display.WriteAsync("Saved");
        }

        internal async Task StartAsync()
        {
            while (true)
            {
                if (_inputStream == null)
                {
                    await Task.Delay(500);
                    continue;
                }

                var byteCount = await _inputStream.LoadAsync(256).AsTask();
                var bytes = new byte[byteCount];
                _inputStream.ReadBytes(bytes);

                foreach (var sentence in Encoding.ASCII.GetString(bytes).Split('$'))
                {
                    if (string.IsNullOrEmpty(sentence))
                        continue;

                    var parsedData = sentence.ParseNmea();
                    if (parsedData == null)
                        continue;

                    lock (_fixDataLock)
                        _gpsFixData = parsedData;
                }
            }
        }

        internal async Task StartNtripAsync()
        {
            var ipEndPoint = new IPEndPoint(IPAddress.Any, 8000);
            var udpClient = new UdpClient(ipEndPoint);
            var outputStream = new DataWriter(_gpsSerialDevice.OutputStream);

            while (true)
            {
                try
                {
                    var udpReceive = await udpClient.ReceiveAsync();
                    outputStream.WriteBytes(udpReceive.Buffer);
                    await outputStream.StoreAsync().AsTask();
                }
                catch (Exception)
                {
                    //
                }
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