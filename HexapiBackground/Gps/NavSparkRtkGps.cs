using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    internal class NavSparkGps : IGps
    {
        private readonly List<double> _correctors = new List<double>();
        private readonly List<LatLon> _latLons = new List<LatLon>();
        private readonly List<LatLon> _latLonsAvg = new List<LatLon>();

        //So we don't have to add another USB/Serial adapter, we are not going to configure the GPS from here. 
        //We will pre-configure while connected to a PC. The RX will be connected to the TX pin of the GPS.
        //The TX will be connected to the RX2 pin on the GPS. NTRIP data will be sent over this 
        private readonly SerialPort _serialPort;

        public NavSparkGps(bool useRtk)
        {
            _serialPort = new SerialPort("A104OHRXA", 115200, 2000, 2000);

            if (useRtk)
            {
                Task.Factory.StartNew(async () =>
                {
                    var config = await FileHelpers.ReadStringFromFile("rtkGps.config");

                    if (string.IsNullOrEmpty(config))
                    {
                        Debug.WriteLine("rtkGps.config file is empty. Trying defaults.");

                        config = "69.44.86.36,2101,P041_RTCM,username,password";
                    }

                    try
                    {
                        var settings = config.Split(',');
                        var ntripClient = new NtripClient(settings[0], int.Parse(settings[1]), settings[2], settings[3], settings[4], _serialPort);
                        ntripClient.Start();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Creating NTRIP client failed - {e}");
                    }
                });
            }

            CurrentLatLon = new LatLon();
        }

        public LatLon CurrentLatLon { get; private set; }
        public double DeviationLon { get; private set; }
        public double DeviationLat { get; private set; }
        public double DriftCutoff { get; private set; }

        #region Serial Communication

        public void Start()
        {
            Task.Factory.StartNew(() =>
            {
                Debug.WriteLine("RTK GPS Started...");
                
                while (true)
                {
                    var sentences = _serialPort.ReadString();

                    foreach (var s in sentences.Split('$').Where(s => s.Length > 15))
                    {
                        var latLon = GpsHelpers.NmeaParse(s);
                        if (Math.Abs(latLon.Lon) < 1 || Math.Abs(latLon.Lat) < 1 || latLon.Quality == GpsFixQuality.NoFix)
                            continue;

                        CurrentLatLon = latLon;
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        #endregion
    }
}