using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Gps.Ntrip;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;

namespace HexapiBackground.Gps
{
    internal class Gps
    {
        private readonly SerialPort _serialPortForRtkCorrectionData;
        private readonly SerialPort _serialPortForGps;
        private readonly bool _useRtk;

        public Gps(bool useRtk)
        {
            _useRtk = useRtk;
            _serialPortForRtkCorrectionData = new SerialPort();  //FTDIBUS\VID_0403+PID_6001+A104OHRXA\0000
            _serialPortForGps = new SerialPort();

            CurrentLatLon = new LatLon();
        }

        public LatLon CurrentLatLon { get; private set; }

        #region Serial Communication

        public void Start()
        { 
            Task.Run(async() =>
            {
                await _serialPortForRtkCorrectionData.Open("A104OHRX", 57600, 2000, 2000);
                await _serialPortForGps.Open("AH03F3RY", 57600, 2000, 2000);

                if (_useRtk)
                {
                    var ntripClient = new NtripClientTcp("172.16.0.225", 8000, "", "", "");
                    ntripClient.NtripDataArrivedEvent += NtripClient_NtripDataArrivedEvent;
                    ntripClient.Start();
                }

                Display.Write("RTK GPS Started...");

                while (true)
                {
                    var sentences = await _serialPortForGps.ReadString();

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
            await Task.Run(async () =>
            {
                await _serialPortForRtkCorrectionData.Write(e.NtripBytes);
            });
        }

        #endregion
    }
}