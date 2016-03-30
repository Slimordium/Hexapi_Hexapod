using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HexapiBackground
{
    internal sealed class PingSensors
    {
        private readonly List<int> _centerAvg = new List<int>();
        private readonly List<int> _leftAvg = new List<int>();
        private readonly List<int> _rightAvg = new List<int>();

        internal static int LeftInches { get; private set; }
        internal static int CenterInches { get; private set; }
        internal static int RightInches { get; private set; }

        private readonly RemoteArduino _remoteArduino;

        internal PingSensors(RemoteArduino remoteArduino)
        {
            _remoteArduino = remoteArduino;

            _remoteArduino.StringReceivedAction.Add(StringMessageReceived);

            LeftInches = 0;
            CenterInches = 0;
            RightInches = 0;
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round((duration / 73.746) / 2, 1));
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

            LeftInches = GetInchesFromPingDuration(_leftAvg.Sum() / _leftAvg.Count);
            _leftAvg.RemoveAt(0);

            RightInches = GetInchesFromPingDuration(_rightAvg.Sum() / _rightAvg.Count);
            _rightAvg.RemoveAt(0);

            CenterInches = GetInchesFromPingDuration(_centerAvg.Sum() / _centerAvg.Count);
            _centerAvg.RemoveAt(0);
        }

        private void StringMessageReceived(string message)
        {
            if (!message.Contains("$Ping:"))
                return;

            try
            {
                RangeUpdate((message.Split(',').Select(int.Parse).ToArray()));
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Range Update exception : {e.Message}");
            }
        }
    }
}
