using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Gps.Ntrip;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;

namespace HexapiBackground.Gps
{
    internal class Gps
    {
        private SerialDevice _serialPortForGps;
        public LatLon CurrentLatLon { get; private set; }

        private readonly SerialDeviceHelper _serialDeviceHelper;
        private readonly SparkFunSerial16X2Lcd _display;
        private readonly NtripClientTcp _ntripClientTcp;

        public Gps(bool useRtk, SerialDeviceHelper serialDeviceHelper, SparkFunSerial16X2Lcd display, NtripClientTcp ntripClientTcp)
        {
            _serialDeviceHelper = serialDeviceHelper;
            _display = display;
            _ntripClientTcp = ntripClientTcp;

            _ntripClientTcp.NtripDataArrivedEvent += NtripClient_NtripDataArrivedEvent;

            CurrentLatLon = new LatLon();
        }

        private DataReader _inputStream;

        public async Task<bool> Initialize()
        {
            _serialPortForGps = await _serialDeviceHelper.GetSerialDevice("AH03F3RY", 57600, new TimeSpan(0, 0, 0, 1), new TimeSpan(0, 0, 0, 1));

            if (_serialPortForGps == null)
                return false;

            _inputStream = new DataReader(_serialPortForGps.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            return true;
        }
        
        #region Serial Communication

        public async Task Start()
        {
            while (true)
            {
                if (_inputStream == null)
                {
                    continue;
                }

                var bytesIn = await _inputStream.LoadAsync(32).AsTask();

                if (bytesIn == 0)
                    continue;

                var sentences = _inputStream.ReadString(bytesIn);

                foreach (var sentence in sentences.Split('$').Where(s => s.Contains('\r') && s.Length > 16))
                {
                    var latLon = sentence.ParseNmea();

                    if (latLon == null)
                        continue;

                    if (CurrentLatLon.Quality != latLon.Quality)
                        await _display.Write(latLon.Quality.ToString(), 2);

                    CurrentLatLon = latLon;
                }
            }

        }

        private async void NtripClient_NtripDataArrivedEvent(object sender, NtripEventArgs e)
        {
            try
            {
                using (var outputStream = new DataWriter(_serialPortForGps.OutputStream))
                {
                    outputStream.WriteBytes(e.NtripBytes);
                    await outputStream.StoreAsync().AsTask();
                }
            }
            catch
            {
                await _display.Write("NTRIP update failed");
            }
        }

        #endregion
    }
}