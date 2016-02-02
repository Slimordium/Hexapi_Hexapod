using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Gps;

namespace HexapiBackground{
    internal sealed class AvController{
        private static LatLon _currentLatLon;

        private readonly Stopwatch _gpsStopwatch = new Stopwatch();
        private readonly Stopwatch _pingStopwatch = new Stopwatch();
        private readonly List<int> _centerAvg = new List<int>();

        private readonly List<int> _leftAvg = new List<int>();
        private readonly List<int> _rightAvg = new List<int>();

        internal AvController()
        {
            _gpsStopwatch.Start();
            _pingStopwatch.Start();

            LeftInches = 0;
            RightInches = 0;
            CenterInches = 0;

            LoadWaypoints();
        }

        internal static double LeftInches { get; private set; }
        internal static double CenterInches { get; private set; }
        internal static double RightInches { get; private set; }


        internal void PingData(int[] data)
        {
            if (data.Length < 3)
            {
                Debug.WriteLine("Bad ping data");
                return;
            }

            _leftAvg.Add(data[0]);
            _centerAvg.Add(data[1]);
            _rightAvg.Add(data[2]);

            if (_leftAvg.Count <= 3)
                return;

            LeftInches = GetInchesFromDuration(_leftAvg.Sum()/_leftAvg.Count);
            _leftAvg.RemoveAt(0);

            RightInches = GetInchesFromDuration(_rightAvg.Sum()/_rightAvg.Count);
            _rightAvg.RemoveAt(0);

            CenterInches = GetInchesFromDuration(_centerAvg.Sum()/_centerAvg.Count);
            _centerAvg.RemoveAt(0);

            Debug.WriteLine($"{LeftInches}, {CenterInches}, {RightInches}");
        }

        internal void FindNearestWaypoint()
        {
            foreach (var wp in Waypoints)
            {
                Debug.WriteLine($"From current loacaion, Distance: {Math.Round(wp.DistanceFromCurrent, 1)}in., Heading: {Math.Round(wp.HeadingFromCurrent, 2)}");
            }
        }

        private static double GetInchesFromDuration(int duration) //73.746 microseconds per inch
        {
            return Math.Round((duration/73.746)/2, 2);
        }

        internal void GpsData(LatLon latLon)
        {
            _currentLatLon = latLon;

            if (_gpsStopwatch.ElapsedMilliseconds <= 4000)
                return;

            _gpsStopwatch.Restart();

            FindNearestWaypoint();

            Debug.WriteLine(latLon.ToString());

            
        }

        internal static void SaveWaypointToFile()
        {
            if (_currentLatLon == null) return;

            Debug.WriteLine($"Saving to file : {_currentLatLon}");

            Helpers.SaveStringToFile("waypoints.config", _currentLatLon.ToString());
        }

        internal void LoadWaypoints()
        {
            Waypoints = new List<LatLon>();

            var config = Helpers.ReadStringFromFile("waypoints.config");

            if (string.IsNullOrEmpty(config))
            {
                Debug.WriteLine("Empty waypoints.config file");
                return;
            }

            var wps = config.Split('\n');

            foreach (var wp in wps)
            {
                try
                {
                    if (!string.IsNullOrEmpty(wp))
                        Waypoints.Add(new LatLon(wp));
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    //Partial string usually
                }
            }
        }

        internal event WarningHandler OnWarning;

        internal delegate void WarningHandler(Direction requestedDirection, int magnitude);

        public List<LatLon> Waypoints { get; set; }

        internal enum Direction
        {
            Unknown,
            FullStop,
            Forward,
            ForwardLeft,
            ForwardRight,
            Left,
            Right,
            Reverse,
            ReverseLeft,
            ReverseRight
        }
    }
}