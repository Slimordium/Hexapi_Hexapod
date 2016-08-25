using System;
using System.Diagnostics;
using HexapiBackground.Enums;

namespace HexapiBackground.Gps
{
    internal class GpsFixData
    {
        internal GpsFixData() { }

        internal GpsFixData(string rawData)
        {
            var aParsed = rawData.Split(',');

            if (aParsed.Length < 5)
            {
                Debug.WriteLine($"Could not parse waypoint data - {rawData}");
                return;
            }

            DateTime = Convert.ToDateTime(aParsed[0]);
            Lat = double.Parse(aParsed[1]);
            Lon = double.Parse(aParsed[2]);
            Heading = int.Parse(aParsed[3]);
            FeetPerSecond = double.Parse(aParsed[4]);
            Quality = (GpsFixQuality)Enum.Parse(typeof(GpsFixQuality), aParsed[5]);
        }

        internal double Lat { get; set; } = 0;
        internal double Lon { get; set; } = 0;
        internal GpsFixQuality Quality { get; set; } = GpsFixQuality.NoFix;
        internal int Heading { get; set; } = 0;
        internal float Altitude { get; set; } = 0;
        internal double FeetPerSecond { get; set; } = 0;
        internal DateTime DateTime { get; set; } = DateTime.MinValue;
        internal int SatellitesInView { get; set; } = 0;
        internal int SignalToNoiseRatio { get; set; } = 0;
        internal double RtkAge { get; set; } = 0;
        internal double RtkRatio { get; set; } = 0;
        internal double Hdop { get; set; } = 0;
        internal double EastProjectionOfBaseLine { get; set; } = 0;
        internal double NorthProjectionOfBaseLine { get; set; } = 0;
        internal double UpProjectionOfBaseLine { get; set; } = 0;

        public override string ToString()
        {
            return $"{DateTime},{Lat},{Lon},{Heading},{FeetPerSecond},{Quality},{SatellitesInView},{SignalToNoiseRatio},{RtkAge},{RtkRatio}{'\n'}";
        }
    }
}