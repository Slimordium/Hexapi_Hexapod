using System;
using System.Diagnostics;
using HexapiBackground.Enums;

namespace HexapiBackground.Gps
{
    internal class LatLon
    {
        internal LatLon()
        {
            Lat = 0;
            Lon = 0;
            DateTime = DateTime.MinValue;
            Quality = GpsFixQuality.NoFix;
            Heading = 0;
            Altitude = 0;
            FeetPerSecond = 0;
            DistanceToAvgCenter = 0;
            CorrectedDistanceToCenter = 0;
        }

        internal LatLon(string rawData)
        {
            var aParsed = rawData.Split(',');

            if (aParsed.Length < 5)
            {
                Display.Write($"Could not parse waypoint");

                Lat = 0;
                Lon = 0;
                DateTime = DateTime.MinValue;
                Quality = GpsFixQuality.NoFix;
                Heading = 0;
                Altitude = 0;
                FeetPerSecond = 0;
                DistanceToAvgCenter = 0;
                CorrectedDistanceToCenter = 0;
                return;
            }

            DateTime = Convert.ToDateTime(aParsed[0]);
            Lat = double.Parse(aParsed[1]);
            Lon = double.Parse(aParsed[2]);
            Heading = double.Parse(aParsed[3]);
            FeetPerSecond = double.Parse(aParsed[4]);
            Quality = (GpsFixQuality)Enum.Parse(typeof(GpsFixQuality), aParsed[5]);

            Altitude = 0;
            DistanceToAvgCenter = 0;
            CorrectedDistanceToCenter = 0;
            SatellitesInView = 0;
        }

        internal double Lat { get; set; }
        internal double Lon { get; set; }
        internal GpsFixQuality Quality { get; set; }
        internal double Heading { get; set; }
        internal float Altitude { get; set; }
        internal double FeetPerSecond { get; set; }
        internal DateTime DateTime { get; set; }
        internal double DistanceToAvgCenter { get; set; }
        internal double CorrectedDistanceToCenter { get; set; }
        internal int SatellitesInView { get; set; }
        internal int SignalToNoiseRatio { get; set; }

        public override string ToString()
        {
            return $"{DateTime},{Lat},{Lon},{Heading},{FeetPerSecond},{Quality}{'\n'}";
        }
    }
}