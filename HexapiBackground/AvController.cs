using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Gps;

namespace HexapiBackground{
    internal sealed class AvController
    {
        private static LatLon _currentLatLon;

        private readonly List<int> _centerAvg = new List<int>();

        private readonly Stopwatch _gpsStopwatch = new Stopwatch();
        private readonly List<int> _leftAvg = new List<int>();
        private readonly Stopwatch _pingStopwatch = new Stopwatch();

        private readonly Random _random = new Random(DateTime.Now.Millisecond);
        private readonly List<int> _rightAvg = new List<int>();

        private bool _obstacleAvoidanceEnabled;

        internal AvController()
        {
            _gpsStopwatch.Start();
            _pingStopwatch.Start();

            LeftInches = 0;
            RightInches = 0;
            CenterInches = 0;

            Waypoints = new List<LatLon>();

            LoadWaypoints();
        }

        internal static double LeftInches { get; set; }

        internal static double CenterInches { get; set; }

        internal static double RightInches { get; set; }

        internal List<LatLon> Waypoints { get; set; }

        internal void ObstacleAvoidance(bool enabled)
        {
            _obstacleAvoidanceEnabled = enabled;

            if (!enabled)
                return;

            Task.Factory.StartNew(() =>
            {
                var sw = new Stopwatch();
                sw.Start();

                while (_obstacleAvoidanceEnabled)
                {
                    if (sw.ElapsedMilliseconds < 250)
                        return;

                    sw.Restart();

                    if (OnNavRequest == null)
                        return;

                    if (LeftInches < 5 && RightInches < 5 && CenterInches < 5)
                    {
                        LookForOpenArea();
                        return;
                    }

                    if (LeftInches < 5)
                    {
                        OnNavRequest?.Invoke(Direction.Right, 1000);
                        return;
                    }

                    if (RightInches < 5)
                    {
                        OnNavRequest?.Invoke(Direction.Left, 1000);
                        return;
                    }

                    if (LeftInches < 10)
                        OnNavRequest?.Invoke(Direction.ForwardRight, 1000);

                    if (RightInches < 10)
                        OnNavRequest?.Invoke(Direction.ForwardLeft, 1000);
                }
            });
        }

        private void LookForOpenArea()
        {
            var sw = new Stopwatch();
            sw.Start();

            var randomNumber = _random.Next(0, 10);

            if (randomNumber < 5)
            {
                OnNavRequest?.Invoke(Direction.Right, 1000);
            }
            else if (randomNumber >= 5)
            {
                OnNavRequest?.Invoke(Direction.Left, 1000);
            }

            while (CenterInches < 15)
            {
                if (sw.ElapsedMilliseconds > 5000)
                    break;
            }

            if (sw.ElapsedMilliseconds > 5000)
                while (CenterInches < 10)
                {
                    if (sw.ElapsedMilliseconds > 10000)
                        break;
                }

            OnNavRequest?.Invoke(Direction.Forward, 1000);
        }

        private bool Reverse()
        {
            var sw = new Stopwatch();
            sw.Start();

            var randomNumber = _random.Next(0, 10);

            if (randomNumber < 5)
            {
                OnNavRequest?.Invoke(Direction.ReverseRight, 1000);
            }
            else if (randomNumber >= 5)
            {
                OnNavRequest?.Invoke(Direction.ReverseLeft, 1000);
            }


            while (sw.ElapsedMilliseconds < 2000)
            {
            }

            if (LeftInches < 5 && RightInches < 5 && CenterInches < 5)
            {
                return false;
            }

            return true;
        }

        internal void EnableGpsNavigation()
        {
        }

        internal void RangeUpdate(int[] data)
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

            LeftInches = GetInchesFromPingDuration(_leftAvg.Sum()/_leftAvg.Count);
            _leftAvg.RemoveAt(0);

            RightInches = GetInchesFromPingDuration(_rightAvg.Sum()/_rightAvg.Count);
            _rightAvg.RemoveAt(0);

            CenterInches = GetInchesFromPingDuration(_centerAvg.Sum()/_centerAvg.Count);
            _centerAvg.RemoveAt(0);

            //if (_pingStopwatch.ElapsedMilliseconds > 500)
            //{
            //    Debug.WriteLine($"Range: {RightInches}, {CenterInches}, {LeftInches}");
            //    _pingStopwatch.Restart();
            //}
        }

        internal void PrintDistanceHeadingToWaypoints()
        {
            foreach (var wp in Waypoints)
            {
                Debug.WriteLine($"From current location, Distance: {wp.DistanceHeadingFromCurrent[0]}in. Heading: {wp.DistanceHeadingFromCurrent[1]}");
            }
        }

        private static double GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Math.Round((duration/73.746)/2, 1);
        }

        internal void LatLonUpdate(LatLon latLon)
        {
            _currentLatLon = latLon;

            //if (_gpsStopwatch.ElapsedMilliseconds > 2000)
            //{
            //    Debug.WriteLine($"{latLon.Lat}, {latLon.Lon}, {latLon.Heading}");
            //    _gpsStopwatch.Restart();
            //}
        }

        internal static void SaveWaypoint()
        {
            if (_currentLatLon == null) return;

            Debug.WriteLine($"Saving to file : {_currentLatLon}");

            Helpers.SaveStringToFile("waypoints.config", _currentLatLon.ToString());
        }

        internal void LoadWaypoints()
        {
            Waypoints = new List<LatLon>();

            var config = Helpers.ReadStringFromFile("waypoints.config").Result;

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

        internal static event NavRequestHandler OnNavRequest;

        internal delegate void NavRequestHandler(Direction requestedDirection, int magnitude);
    }
}