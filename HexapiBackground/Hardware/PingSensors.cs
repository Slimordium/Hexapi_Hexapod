using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HexapiBackground.Hardware
{
    internal class PingSensors
    {
        internal int PerimeterInInches { get; set; }

        internal int LeftInches { get; private set; }
        internal int CenterInches { get; private set; }
        internal int RightInches { get; private set; }

        internal bool LeftBlocked => LeftInches < PerimeterInInches;
        internal bool CenterBlocked => CenterInches < PerimeterInInches;
        internal bool RightBlocked => RightInches < PerimeterInInches;

        public event EventHandler<bool> Left;
        public event EventHandler<bool> Center;
        public event EventHandler<bool> Right;

        internal PingSensors(RemoteArduino remoteArduino)
        {
            PerimeterInInches = 14;

            remoteArduino.StringReceivedActions.Add(StringMessageReceived);

            LeftInches = 0;
            CenterInches = 0;
            RightInches = 0;
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round((duration / 73.746) / 2, 1));
        }

        private async void RangeUpdate(int[] data)
        {
            if (data.Length < 3)
                return;

            var left = Left;
            var center = Center;
            var right = Right;

            LeftInches = GetInchesFromPingDuration(data[0]);
            left?.Invoke(null, LeftBlocked);

            CenterInches = GetInchesFromPingDuration(data[1]);
            center?.Invoke(null, CenterBlocked);

            RightInches = GetInchesFromPingDuration(data[2]);
            right?.Invoke(null, RightBlocked);

            if (LeftBlocked || CenterBlocked || RightBlocked)
                await Display.Write($"{LeftInches}, {CenterInches}, {RightInches}");
        }

        private async void StringMessageReceived(string message)
        {
            try
            {
                message = message.Split(':')[1];

                RangeUpdate(message.Split(',').Select(int.Parse).ToArray());
            }
            catch (Exception)
            {
                await Display.Write($"Range failed");
            }
        }
    }
}
