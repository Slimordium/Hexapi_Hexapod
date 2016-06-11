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

        public void Start()
        { 
            Task.Run(async() =>
            {
                _serialPortForGps = await SerialDeviceHelper.GetSerialDevice("AH03F3RY", 57600, new TimeSpan(0, 0, 0, 5), new TimeSpan(0, 0, 0, 5));

                var inputStream = new DataReader(_serialPortForGps.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

                if (_useRtk)
                {
                    _serialPortForRtkCorrectionData = await SerialDeviceHelper.GetSerialDevice("A104OHRX", 57600, new TimeSpan(0, 0, 0, 5), new TimeSpan(0, 0, 0, 5));  //FTDIBUS\VID_0403+PID_6001+A104OHRXA\0000

                    var ntripClient = new NtripClientTcp("172.16.0.225", 8000, "", "", "");
                    ntripClient.NtripDataArrivedEvent += NtripClient_NtripDataArrivedEvent;
                }

                Display.Write("RTK GPS Started...");

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
                            Display.Write(latLon.Quality.ToString(), 2);

                        CurrentLatLon = latLon;
                    }
                }
            });
        }

        private async void NtripClient_NtripDataArrivedEvent(object sender, NtripEventArgs e)
        {
            using (var outputStream = new DataWriter(_serialPortForRtkCorrectionData.OutputStream))
            {
                outputStream.WriteBytes(e.NtripBytes);
                await outputStream.StoreAsync();
            }
        }

        #endregion
    }
}