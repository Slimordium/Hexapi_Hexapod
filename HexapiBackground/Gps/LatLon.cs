using System;
using HexapiBackground.Enums;

namespace HexapiBackground.Gps
{
    internal class LatLon
    {
        internal LatLon()
        {
        }

        internal LatLon(string rawData)
        {
            if (string.IsNullOrEmpty(rawData))
                return;

            var aParsed = rawData.Split(',');

            if (aParsed.Length < 5)
                return;

            DateTime = Convert.ToDateTime(aParsed[0]);
            Lat = double.Parse(aParsed[1]);
            Lon = double.Parse(aParsed[2]);
            Heading = double.Parse(aParsed[3]);
            FeetPerSecond = double.Parse(aParsed[4]);
            Quality = (GpsFixQuality)Enum.Parse(typeof(GpsFixQuality), aParsed[5]);
        }

        internal double Lat { get; set; } = 0;
        internal double Lon { get; set; } = 0;
        internal GpsFixQuality Quality { get; set; } = GpsFixQuality.NoFix;
        internal double Heading { get; set; } = 0;
        internal float Altitude { get; set; } = 0;
        internal double FeetPerSecond { get; set; } = 0;
        internal DateTime DateTime { get; set; } = DateTime.MinValue;
        internal double DistanceToAvgCenter { get; set; } = 0;
        internal double CorrectedDistanceToCenter { get; set; } = 0;
        internal int SatellitesInView { get; set; } = 0;
        internal int SignalToNoiseRatio { get; set; } = 0;

        public override string ToString()
        {
            return $"{DateTime},{Lat},{Lon},{Heading},{FeetPerSecond},{Quality}{'\n'}";
        }
    }
}