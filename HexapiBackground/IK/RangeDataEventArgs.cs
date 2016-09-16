using System;

// ReSharper disable once CheckNamespace
namespace HexapiBackground
{
    internal class RangeDataEventArgs : EventArgs{

        private static double _lastLeft = 30;
        private static double _lastCenter = 30;
        private static double _lastRight = 30;

        internal RangeDataEventArgs(int perimeterInInches, double left, double center, double right, double farLeft, double farRight)
        {
            if (left == 0)
                left = _lastLeft;
            else
                _lastLeft = left;

            if (center == 0)
                center = _lastCenter;
            else
                _lastCenter = center;

            if (right == 0)
                right = _lastRight;
            else
                _lastRight = right;

            LeftWarning = left <= perimeterInInches + 5;
            CenterWarning = center <= perimeterInInches + 5;
            RightWarning = right <= perimeterInInches + 5;

            LeftBlocked = left <= perimeterInInches;
            CenterBlocked = center <= perimeterInInches;
            RightBlocked = right <= perimeterInInches;

            LeftInches = left;
            FarLeftInches = farLeft;
            CenterInches = center;
            RightInches = right;
            FarRightInches = farRight;
        }

        public bool LeftWarning { get; private set; }
        public bool CenterWarning { get; private set; }
        public bool RightWarning { get; private set; }

        public bool LeftBlocked { get; private set; }
        public bool CenterBlocked { get; private set; }
        public bool RightBlocked { get; private set; }

        public double LeftInches { get; private set; }
        public double FarLeftInches { get; private set; }
        public double CenterInches { get; private set; }
        public double RightInches { get; private set; }
        public double FarRightInches { get; private set; }
    }
}