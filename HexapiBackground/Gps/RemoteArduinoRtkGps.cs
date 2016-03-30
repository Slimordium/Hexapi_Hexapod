using System.Collections.Generic;
using System.Linq;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    internal class RemoteArduinoRtkGps : IGps
    {
        private readonly List<double> _correctors = new List<double>();
        private readonly List<LatLon> _latLons = new List<LatLon>();
        private readonly List<LatLon> _latLonsAvg = new List<LatLon>();
      
        public RemoteArduinoRtkGps()
        {
            CurrentLatLon = new LatLon();
        }

        internal int SatellitesInView { get; set; }
        internal int SignalToNoiseRatio { get; set; }

        public LatLon CurrentLatLon { get; private set; }
        public double DeviationLon { get; private set; }
        public double DeviationLat { get; private set; }
        public double DriftCutoff { get; private set; }

        public void Start()
        {
            RemoteArduino.StringReceivedActions.Add(NmeaReceived);
        }

        private void NmeaReceived(string sentences)
        {
            if (sentences.Contains("$Ping:"))
                return;

            foreach (var s in sentences.Split('$').Where(s => s.Contains('\r') && s.Length > 16))
            {
                var latLon = GpsHelpers.NmeaParse(s);

                if (latLon == null)
                    continue;

                CurrentLatLon = latLon;
            }
        }
    }
}