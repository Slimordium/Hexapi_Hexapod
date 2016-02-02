using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HexapiBackground.Gps;

namespace HexapiBackground{
    internal sealed class AvController{
        private static LatLon _currentLatLon;

        private readonly Stopwatch _gpsStopwatch = new Stopwatch();
        private readonly Stopwatch _pingStopwatch = new Stopwatch();
        private List<int> _centerAvg = new List<int>();

        private List<int> _leftAvg = new List<int>();
        private List<int> _rightAvg = new List<int>();

        internal AvController()
        {
            _gpsStopwatch.Start();
            _pingStopwatch.Start();

            LeftInches = 0;
            RightInches = 0;
            CenterInches = 0;
        }

        internal static double LeftInches { get; private set; }
        internal static double CenterInches { get; private set; }
        internal static double RightInches { get; private set; }

        internal void Start()
        {
        }

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

            if (_leftAvg.Count > 3)
            {
                LeftInches = GetInchesFromDuration(_leftAvg.Sum()/_leftAvg.Count);
                _leftAvg = new List<int>();

                Debug.WriteLine($"{LeftInches}, {CenterInches}, {RightInches}");
            }

            if (_rightAvg.Count > 3)
            {
                RightInches = GetInchesFromDuration(_rightAvg.Sum()/_rightAvg.Count);
                _rightAvg = new List<int>();
            }

            if (_centerAvg.Count > 3)
            {
                CenterInches = GetInchesFromDuration(_centerAvg.Sum()/_centerAvg.Count);
                _centerAvg = new List<int>();
            }

            
        }

        private static double GetInchesFromDuration(int duration) //73.746 microseconds per inch
        {
            return Math.Round((duration/73.746)/2, 2);
        }

        internal void GpsData(LatLon latLon)
        {
            _currentLatLon = latLon;

            if (_gpsStopwatch.ElapsedMilliseconds <= 2000)
                return;

            Debug.WriteLine(latLon.ToString());
            _gpsStopwatch.Restart();
        }

        internal static void SaveWaypointToFile()
        {
            Debug.WriteLine($"Saving to file : {_currentLatLon}");

            UltimateGps.SaveWaypointToFile(_currentLatLon);
        }

        internal event WarningHandler OnWarning;

        internal delegate void WarningHandler(TravelDirection requestedDirection, int magnitude);
    }

    internal enum TravelDirection{
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