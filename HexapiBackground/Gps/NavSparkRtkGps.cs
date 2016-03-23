using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly bool _useRtk;

        public NavSparkGps(bool useRtk)
        {
            _serialPort = new SerialPort("AI041RYGA", 57600, 2000, 2000); //FTDIBUS\VID_0403+PID_6001+AI041RYGA\0000 --AH03F3RYA

            _useRtk = useRtk;

            CurrentLatLon = new LatLon();
        }

        public LatLon CurrentLatLon { get; private set; }
        public double DeviationLon { get; private set; }
        public double DeviationLat { get; private set; }
        public double DriftCutoff { get; private set; }
        internal int SatellitesInView { get; set; }
        internal int SignalToNoiseRatio { get; set; }

        #region Serial Communication

        public void Start()
        {
            if (_useRtk)
            {
                var config = FileHelpers.ReadStringFromFile("rtkGps.config").GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(config))
                {
                    Debug.WriteLine("rtkGps.config file is empty. Trying defaults.");
                    config = "69.44.86.36,2101,P041_RTCM,user,passw,serial";
                }

                try
                {
                    var settings = config.Split(',');

                    if (settings[5] != null && settings[5].Equals("serial", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var ntripClient = new NtripClientFona(settings[0], int.Parse(settings[1]), settings[2], settings[3], settings[4], _serialPort);
                        ntripClient.Start();
                    }
                    else
                    {
                        var ntripClient = new NtripClientTcp(settings[0], int.Parse(settings[1]), settings[2], settings[3], settings[4], _serialPort);
                        ntripClient.Start();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Creating NTRIP client failed - {e}");
                }
            }

            Task.Factory.StartNew(() =>
            {
                Debug.WriteLine("NavSpark RTK GPS Started...");
                
                while (true)
                {
                    var sentences = _serialPort.ReadString();

                    foreach (var s in sentences.Split('$').Where(s => s.Contains('\r') && s.Length > 16))
                    {
                        var latLon = GpsHelpers.NmeaParse(s);

                        if (latLon == null)
                            continue;
                        
                        CurrentLatLon = latLon;
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        #endregion
    }
}