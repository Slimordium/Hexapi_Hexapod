using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Gps;
using HexapiBackground.Helpers;

namespace HexapiBackground
{
    internal static class GpsExtensions
    {
        private static double _lat;
        private static double _lon;
        private static GpsFixQuality _quality;
        private static double _heading;
        private static float _altitude;
        private static double _feetPerSecond;
        private static DateTime _dateTime;
        private static int _satellitesInView;
        private static int _signalToNoiseRatio;
        private static double _rtkAge;
        private static double _rtkRatio;
        private static double _hdop;

        internal static async Task SaveWaypoint(this LatLon latLon)
        {
            Debug.WriteLine($"Saving to file : {latLon}");

            await FileExtensions.SaveStringToFile("waypoints.txt", latLon.ToString());
        }

        internal static async Task<List<LatLon>> LoadWaypoints()
        {
            var waypoints = new List<LatLon>();

            var config = await "waypoints.txt".ReadStringFromFile();

            if (string.IsNullOrEmpty(config))
            {
                Debug.WriteLine("Empty waypoints.txt file");//Write to display insetad
                return waypoints;
            }

            var wps = config.Split('\n');

            foreach (var wp in wps)
            {
                try
                {
                    if (string.IsNullOrEmpty(wp))
                        continue;

                    var newWp = new LatLon(wp);
                    if (newWp.DateTime > DateTime.MinValue)
                    {
                        waypoints.Add(new LatLon(wp));
                    }
                    else
                    {
                        Debug.WriteLine("Invalid date/time, not loading waypoint");//Write to display insetad
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e); //Write to display insetad
                }
            }

            return waypoints;
        }

        /// <summary>
        ///     Returns double[] [0] = distance to heading in inches. [1] = heading to destination waypoint
        /// </summary>
        /// <param name="currentLat"></param>
        /// <param name="currentLon"></param>
        /// <param name="destinationLat"></param>
        /// <param name="destinationLon"></param>
        /// <returns>distance to waypoint, and heading to waypoint</returns>
        internal static double[] GetDistanceAndHeadingToDestination(double currentLat, double currentLon,
            double destinationLat, double destinationLon)
        {
            try
            {
                var diflat = (destinationLat - currentLat).ToRadians();

                currentLat = currentLat.ToRadians(); //convert current latitude to radians
                destinationLat = destinationLat.ToRadians(); //convert waypoint latitude to radians

                var diflon = (destinationLon - currentLon).ToRadians();
                //subtract and convert longitude to radians

                var distCalc = Math.Sin(diflat / 2.0) * Math.Sin(diflat / 2.0);
                var distCalc2 = Math.Cos(currentLat);

                distCalc2 = distCalc2 * Math.Cos(destinationLat);
                distCalc2 = distCalc2 * Math.Sin(diflon / 2.0);
                distCalc2 = distCalc2 * Math.Sin(diflon / 2.0); //and again, why?
                distCalc += distCalc2;
                distCalc = 2 * Math.Atan2(Math.Sqrt(distCalc), Math.Sqrt(1.0 - distCalc));
                distCalc = distCalc * 6371000.0;
                //Converting to meters. 6371000 is the magic number,  3959 is average Earth radius in miles
                distCalc = Math.Round(distCalc * 39.3701, 1); // and then to inches.

                currentLon = currentLon.ToRadians();
                destinationLon = destinationLon.ToRadians();

                var heading = Math.Atan2(Math.Sin(destinationLon - currentLon) * Math.Cos(destinationLat),
                    Math.Cos(currentLat) * Math.Sin(destinationLat) -
                    Math.Sin(currentLat) * Math.Cos(destinationLat) * Math.Cos(destinationLon - currentLon));

                heading = heading.ToDegrees();

                if (heading < 0)
                    heading += 360;

                return new[] { Math.Round(distCalc, 1), Math.Round(heading, 1) };
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new double[] { 0, 0 };
            }
        }

        internal static double Latitude2Double(this string lat, string ns)
        {
            if (lat.Length < 2 || string.IsNullOrEmpty(ns))
                return 0;

            var med = 0d;

            if (!double.TryParse(lat.Substring(2), out med))
                return 0d;

            med = med / 60.0d;

            var temp = 0d;

            if (!double.TryParse(lat.Substring(0, 2), out temp))
                return 0d;

            med += temp;

            if (ns.StartsWith("S"))
            {
                med = -med;
            }

            return Math.Round(med, 8);
        }

        internal static double Longitude2Double(this string lon, string we)
        {
            if (lon.Length < 2 || string.IsNullOrEmpty(we))
                return 0;

            var med = 0d;

            if (!double.TryParse(lon.Substring(3), out med))
                return 0;

            med = med / 60.0d;

            var temp = 0d;

            if (!double.TryParse(lon.Substring(0, 3), out temp))
                return 0d;

            med += temp;

            if (we.StartsWith("W"))
            {
                med = -med;
            }

            return Math.Round(med, 8);
        }

        internal static LatLon ParseNmea(this string data)
        {
            try
            {
                var tokens = data.Split(',');
                var type = tokens[0];

                switch (type)
                {
                    case "GPGGA": //Global Positioning System Fix Data
                        if (tokens.Length < 10)
                            return null;

                        var st = tokens[1];

                        _dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                            Convert.ToInt32(st.Substring(0, 2)), Convert.ToInt32(st.Substring(2, 2)),
                            Convert.ToInt32(st.Substring(4, 2)), DateTimeKind.Local);

                        _lat = Latitude2Double(tokens[2], tokens[3]);
                        _lon = Longitude2Double(tokens[4], tokens[5]);

                        int quality;
                        if (int.TryParse(tokens[6], out quality))
                            _quality = (GpsFixQuality)quality;

                        if (float.TryParse(tokens[9], out _altitude))
                            _altitude = _altitude * 3.28084f;

                        double.TryParse(tokens[8], out _hdop);

                        break;
                    case "GPGLL": //Global Positioning System Fix Data
                        if (tokens.Length < 8)
                            return null;

                        _lat = Latitude2Double(tokens[1], tokens[2]);
                        _lon = Longitude2Double(tokens[3], tokens[4]);

                        break;
                    case "GPRMC": //Recommended minimum specific GPS/Transit data

                        if (tokens.Length < 9)
                            return null;

                        _lat = Latitude2Double(tokens[3], tokens[4]);
                        _lon = Longitude2Double(tokens[5], tokens[6]);

                        double fps = 0;
                        if (double.TryParse(tokens[7], out fps))
                            _feetPerSecond = Math.Round(fps * 1.68781, 2); //Convert knots to feet per second or "Speed over ground"

                        double dir = 0;
                        if (double.TryParse(tokens[8], out dir))
                            _heading = dir; //angle from true north that you are traveling or "Course made good"

                        break;
                    case "GPGSV": //Satellites in View

                        if (tokens.Length < 8)
                            return null;

                        int satellitesInView;
                        if (int.TryParse(tokens[3], out satellitesInView))
                            _satellitesInView = satellitesInView;

                        int signalToNoiseRatio;
                        if (int.TryParse(tokens[7], out signalToNoiseRatio))
                            _signalToNoiseRatio = signalToNoiseRatio;

                        break;
                    case "PSTI":
                        if (!tokens[1].Equals("030") || tokens.Length < 15)
                            break;

                        //tokens[12] 
                        //                        Mode indicator
                        //‘N’ = Data not valid 
                        //‘A’ = Autonomous mode
                        //‘D’ = Differential mode
                        //‘E’ = Estimated(dead reckoning) mode
                        //‘M’ = Manual input mode 
                        //‘S’ = Simulator mode
                        //‘F’ = Float RTK.Satellite syst
                        //em used in RTK mode, floating
                        //integers
                        //‘R’ = Real Time Kinematic. System used in RTK mode with fixed
                        //integers

                        double.TryParse(tokens[13], out _rtkAge);
                        double.TryParse(tokens[14], out _rtkRatio);

                        break;
                    default:
                        return null;
                }

                if (Math.Abs(_lat) < .1 || Math.Abs(_lon) < .1)
                    return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                //No fix yet
            }
            catch (IndexOutOfRangeException)
            {
                //No fix yet
            }
            catch (Exception e)
            {
                if (_quality != GpsFixQuality.NoFix)
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine(data);
                }
            }

            var latLon = new LatLon
            {
                Lat = _lat,
                Lon = _lon,
                Altitude = _altitude,
                FeetPerSecond = _feetPerSecond,
                Quality = _quality,
                SatellitesInView = _satellitesInView,
                SignalToNoiseRatio = _signalToNoiseRatio,
                Heading = _heading,
                DateTime = _dateTime,
                RtkAge = _rtkAge,
                RtkRatio = _rtkRatio,
                Hdop = _hdop
            };

            //if (_quality > GpsFixQuality.NoFix)
            //    Debug.WriteLine($"Lat, Lon : {_lat}, {_lon}, {_quality}, Heading {_heading}, Alt {_altitude}, Sats {_satellitesInView}, SignalToNoise {_signalToNoiseRatio}");

            return latLon;
        }
    }
}