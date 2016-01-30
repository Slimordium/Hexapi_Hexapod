using System;
using HexapiBackground.Enums;

namespace HexapiBackground.Gps
{
    internal class LatLon
    {
        internal LatLon()
        {
            DateTime = DateTime.Now;
            Quality = GpsFixQuality.NoFix;
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
    }
}