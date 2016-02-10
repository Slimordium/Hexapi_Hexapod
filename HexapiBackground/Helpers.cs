using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using HexapiBackground.Gps;

namespace HexapiBackground{
    internal static class Helpers
    {
        internal static double Map(double valueToMap, double valueToMapMin, double valueToMapMax, double outMin, double outMax)
        {
            return (valueToMap - valueToMapMin) * (outMax - outMin) / (valueToMapMax - valueToMapMin) + outMin;
        }

        internal static async Task<string> ReadStringFromFile(string filename)
        {
            var text = string.Empty;
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists).AsTask();

                using (var stream = await file.OpenStreamForReadAsync())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        text = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return text;
        }

        internal static void SaveStringToFile(string filename, string content)
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var bytesToAppend = System.Text.Encoding.UTF8.GetBytes(content.ToCharArray());
                    var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists).AsTask();

                    using (var stream = await file.OpenStreamForWriteAsync())
                    {
                        stream.Position = stream.Length;
                        stream.Write(bytesToAppend, 0, bytesToAppend.Length);
                    }
                }
                catch
                {
                    Debug.WriteLine("Save failed for " + filename);
                }
            });
        }

        internal static void SaveWaypoint()
        {
            var latLon = new LatLon
            {
                Lat = UltimateGps.CurrentLatitude,
                Lon = UltimateGps.CurrentLongitude,
                FeetPerSecond = UltimateGps.CurrentFeetPerSecond,
                Heading = UltimateGps.CurrentHeading,
                Quality = UltimateGps.Quality
            };

            Debug.WriteLine($"Saving to file : {latLon}");

            SaveStringToFile("waypoints.config", latLon.ToString());
        }

        internal static List<LatLon> LoadWaypoints()
        {
            var waypoints = new List<LatLon>();

            var config = Helpers.ReadStringFromFile("waypoints.config").Result;

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
    }
}