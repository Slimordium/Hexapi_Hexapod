using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Gps.Ntrip;
using HexapiBackground.Hardware;
using HexapiBackground.Iot;

namespace HexapiBackground.Gps
{
    internal sealed class Gps
    {
        private SerialDevice _gpsSerialDevice;

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

        private static SparkFunSerial16X2Lcd _display;
        private readonly NtripClient _ntripClientTcp;
        private static IoTClient _ioTClient;

        private DataReader _inputStream;
        private DataWriter _outputStream;

        //private Timer _iotUpdateTimer = new Timer(o => { SendToIotHub();  }, null, 0, 1000);

        private static async void SendToIotHub()
        {
            if (CurrentGpsFixData == null)
                return;

            await _ioTClient.SendEventAsync("GPS", CurrentGpsFixData.ToString());
        }

        internal Gps(SparkFunSerial16X2Lcd display, NtripClient ntripClientTcp, IoTClient ioTClient)
        {
            _display = display;
            _ntripClientTcp = ntripClientTcp;
            _ioTClient = ioTClient;
        }

        internal async Task<bool> InitializeAsync()
        {
            _gpsSerialDevice = await StartupTask.SerialDeviceHelper.GetSerialDeviceAsync("AH03F3RY", 57600, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

            if (_gpsSerialDevice == null)
                return false;

            _outputStream = new DataWriter(_gpsSerialDevice.OutputStream);
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

        internal async Task DisplayCoordinates()
        {
            await _display.WriteAsync($"{CurrentGpsFixData.Lat}", 1);
            await _display.WriteAsync($"{CurrentGpsFixData.Lon}", 2);
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

                var byteCount = await _inputStream.LoadAsync(512).AsTask();
                var bytes = new byte[byteCount];
                _inputStream.ReadBytes(bytes);

                var sentences = Encoding.ASCII.GetString(bytes).Replace("\0", "").Replace("\r", "").Replace("\n","");

                foreach (var sentence in sentences.Split('$'))
                {
                    if (string.IsNullOrEmpty(sentence))
                        continue;

                    var parsedData = sentence.ParseNmea();
                    if (parsedData == null)
                        continue;

                    lock (_fixDataLock)
                        _gpsFixData = parsedData;
                }

                var rtkBytes = await _ntripClientTcp.ReadNtripAsync();

                _outputStream.WriteBytes(rtkBytes);
                await _outputStream.StoreAsync().AsTask();
            }
        }

        private Timer _statusTimer = new Timer(async sender =>
        {
            if (CurrentGpsFixData == null)
                return;

            await _display.WriteAsync($"{CurrentGpsFixData.Quality} S{CurrentGpsFixData.SatellitesInView}", 1);
            await _display.WriteAsync($"RR{CurrentGpsFixData.RtkRatio}, RV{CurrentGpsFixData.RtkAge}, NR{CurrentGpsFixData.SignalToNoiseRatio}", 2);
        }, null, 0, 5000);
    }

    internal class GpsArgs : EventArgs
    {
        internal GpsFixData GpsFixData { get; set; }
    }
}