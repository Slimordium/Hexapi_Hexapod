using System;
using System.Collections.Generic;
using System.Diagnostics;
using HexapiBackground.Enums;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    internal static class GpsHelpers
    {
        internal static void SaveWaypoint(LatLon latLon)
        {
            Debug.WriteLine($"Saving to file : {latLon}");

            FileHelpers.SaveStringToFile("waypoints.config", latLon.ToString());
        }

        internal static List<LatLon> LoadWaypoints()
        {
            var waypoints = new List<LatLon>();

            var config = FileHelpers.ReadStringFromFile("waypoints.config").Result;

            if (string.IsNullOrEmpty(config))
            {
                Debug.WriteLine("Empty waypoints.config file");
                return waypoints;
            }

            var wps = config.Split('\n');

            foreach (var wp in wps)
            {
                try
                {
                    if (!string.IsNullOrEmpty(wp))
                        waypoints.Add(new LatLon(wp));
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
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
                var diflat = MathHelpers.ToRadians(destinationLat - currentLat);

                currentLat = MathHelpers.ToRadians(currentLat); //convert current latitude to radians
                destinationLat = MathHelpers.ToRadians(destinationLat); //convert waypoint latitude to radians

                var diflon = MathHelpers.ToRadians(destinationLon - currentLon);
                    //subtract and convert longitude to radians

                var distCalc = Math.Sin(diflat/2.0)*Math.Sin(diflat/2.0);
                var distCalc2 = Math.Cos(currentLat);

                distCalc2 = distCalc2*Math.Cos(destinationLat);
                distCalc2 = distCalc2*Math.Sin(diflon/2.0);
                distCalc2 = distCalc2*Math.Sin(diflon/2.0); //and again, why?
                distCalc += distCalc2;
                distCalc = 2*Math.Atan2(Math.Sqrt(distCalc), Math.Sqrt(1.0 - distCalc));
                distCalc = distCalc*6371000.0;
                //Converting to meters. 6371000 is the magic number,  3959 is average Earth radius in miles
                distCalc = Math.Round(distCalc*39.3701, 1); // and then to inches.

                currentLon = MathHelpers.ToRadians(currentLon);
                destinationLon = MathHelpers.ToRadians(destinationLon);

                var heading = Math.Atan2(Math.Sin(destinationLon - currentLon)*Math.Cos(destinationLat),
                    Math.Cos(currentLat)*Math.Sin(destinationLat) -
                    Math.Sin(currentLat)*Math.Cos(destinationLat)*Math.Cos(destinationLon - currentLon));

                heading = MathHelpers.FromRadians(heading);

                if (heading < 0)
                    heading += 360;

                return new[] {Math.Round(distCalc, 1), Math.Round(heading, 1)};
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new double[] {0, 0};
            }
        }

        internal static double Latitude2Double(string lat, string ns)
        {
            var med = 0d;

            if (!double.TryParse(lat.Substring(2), out med))
                return 0d;

            med = med/60.0d;

            var temp = 0d;

            if (!double.TryParse(lat.Substring(0, 2), out temp))
                return 0d;

            med += temp;

            if (ns.StartsWith("S"))
            {
                med = -med;
            }

            return Math.Round(med, 7);
        }

        internal static double Longitude2Double(string lon, string we)
        {
            var med = 0d;

            if (!double.TryParse(lon.Substring(3), out med))
                return 0;

            med = med/60.0d;

            var temp = 0d;

            if (!double.TryParse(lon.Substring(0, 3), out temp))
                return 0d;

            med += temp;

            if (we.StartsWith("W"))
            {
                med = -med;
            }

            return Math.Round(med, 7);
        }

        internal static LatLon NmeaParse(string data)
        {
            var latLon = new LatLon();

            try
            {
                var tokens = data.Split(',');
                var type = tokens[0];

                double lat = 0;
                double lon = 0;

                switch (type)
                {
                    case "GPGGA": //Global Positioning System Fix Data
                        if (latLon.DateTime == DateTime.MinValue)
                        {
                            var st = tokens[1];
                            latLon.DateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                Convert.ToInt32(st.Substring(0, 2)), Convert.ToInt32(st.Substring(2, 2)),
                                Convert.ToInt32(st.Substring(4, 2)), DateTimeKind.Local);
                        }

                        lat = Latitude2Double(tokens[2], tokens[3]);
                        lon = Longitude2Double(tokens[4], tokens[5]);

                        var quality = 0;
                        if (int.TryParse(tokens[6], out quality))
                        {
                            latLon.Quality = (GpsFixQuality) quality;
                        }

                        float altitude = 0;
                        if (float.TryParse(tokens[9], out altitude))
                            latLon.Altitude = altitude*3.28084f;

                        break;
                    case "GPRMC": //Recommended minimum specific GPS/Transit data
                        lat = Latitude2Double(tokens[3], tokens[4]);
                        lon = Longitude2Double(tokens[5], tokens[6]);

                        double fps = 0;
                        if (double.TryParse(tokens[7], out fps))
                            latLon.FeetPerSecond = Math.Round(fps*1.68781, 2);
                        //Convert knots to feet per second or "Speed over ground"

                        double dir = 0;
                        if (double.TryParse(tokens[8], out dir))
                            latLon.Heading = dir; //angle from true north that you are traveling or "Course made good"

                        break;
                }

                //if (!(lat > 0) || !(Math.Abs(lon) > .01))
                //    return latLon;

                latLon.Lat = lat;
                latLon.Lon = lon;

                //_latLons.Add(new LatLon { Lat = lat, Lon = lon, FeetPerSecond = CurrentFeetPerSecond, Altitude = CurrentAltitude });

                //if (_latLons.Count > 30000)
                //    _latLons.RemoveAt(0);
            }
            catch (ArgumentOutOfRangeException)
            {
                //No fix yet
            }
            catch (Exception e)
            {
                if (latLon.Quality == GpsFixQuality.NoFix)
                {
                    Debug.WriteLine(data);
                }
                else
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine(data);
                }
            }

            return latLon;
        }
    }
}