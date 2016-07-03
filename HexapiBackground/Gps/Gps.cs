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
        private SerialDevice _serialPortForRtkCorrectionData;
        private SerialDevice _serialPortForGps;
        private readonly bool _useRtk;
        public LatLon CurrentLatLon { get; private set; }

        public Gps(bool useRtk)
        {
            _useRtk = useRtk;
            CurrentLatLon = new LatLon();
        }

        #region Serial Communication

        public async Task Start()
        {
            _serialPortForGps = await SerialDeviceHelper.GetSerialDevice("AH03F3RY", 57600);

            if (_serialPortForGps == null)
                return;

            await Task.Delay(500);

            var inputStream = new DataReader(_serialPortForGps.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            if (_useRtk)
            {
                _serialPortForRtkCorrectionData = await SerialDeviceHelper.GetSerialDevice("A104OHRX", 57600);  //FTDIBUS\VID_0403+PID_6001+A104OHRXA\0000

                await Task.Delay(500);

                if (_serialPortForRtkCorrectionData != null)
                {
                    var ntripClient = new NtripClientTcp("172.16.0.226", 8000, "", "", "");
                    ntripClient.NtripDataArrivedEvent += NtripClient_NtripDataArrivedEvent;
                }
            }

            if (_serialPortForGps == null)
                return;

            await Display.Write("RTK GPS Started");

            while (true)
            {
                var bytesIn = await inputStream.LoadAsync(32);

                if (bytesIn == 0)
                    continue;

                var sentences = inputStream.ReadString(bytesIn);

                foreach (var sentence in sentences.Split('$').Where(s => s.Contains('\r') && s.Length > 16))
                {
                    var latLon = sentence.ParseNmea();

                    if (latLon == null)
                        continue;

                    if (CurrentLatLon.Quality != latLon.Quality)
                        await Display.Write(latLon.Quality.ToString(), 2);

                    CurrentLatLon = latLon;
                }
            }

        }

        private async void NtripClient_NtripDataArrivedEvent(object sender, NtripEventArgs e)
        {
            try
            {
                using (var outputStream = new DataWriter(_serialPortForRtkCorrectionData.OutputStream))
                {
                    outputStream.WriteBytes(e.NtripBytes);
                    await outputStream.StoreAsync();
                }
            }
            catch
            {
                await Display.Write("NTRIP update failed");
            }
        }

        #endregion
    }
}