using System;

// ReSharper disable once CheckNamespace
namespace HexapiBackground
{
    internal class RangeDataEventArgs : EventArgs
    {
        internal RangeDataEventArgs(int perimeterInInches, double left, double center, double right)
        {
            LeftWarning = left <= perimeterInInches + 5;
            CenterWarning = center <= perimeterInInches + 5;
            RightWarning = right <= perimeterInInches + 5;

            LeftBlocked = left <= perimeterInInches;
            CenterBlocked = center <= perimeterInInches;
            RightBlocked = right <= perimeterInInches;

            LeftInches = left;
            CenterInches = center;
            RightInches = right;
        }

        public bool LeftWarning { get; private set; }
        public bool CenterWarning { get; private set; }
        public bool RightWarning { get; private set; }

        public bool LeftBlocked { get; private set; }
        public bool CenterBlocked { get; private set; }
        public bool RightBlocked { get; private set; }

        public double LeftInches { get; private set; }
        public double CenterInches { get; private set; }
        public double RightInches { get; private set; }
    }
}