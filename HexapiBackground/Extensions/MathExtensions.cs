using System;

namespace HexapiBackground.Helpers
{
    internal static class MathExtensions
    {
        internal static double Map(this double valueToMap, double valueToMapMin, double valueToMapMax, double outMin, double outMax)
        {
            return (valueToMap - valueToMapMin)*(outMax - outMin)/(valueToMapMax - valueToMapMin) + outMin;
        }

        internal static double Map(this int valueToMap, double valueToMapMin, double valueToMapMax, double outMin, double outMax)
        {
            return (valueToMap - valueToMapMin) * (outMax - outMin) / (valueToMapMax - valueToMapMin) + outMin;
        }

        internal static double ToRadians(this double angle)
        {
            return Math.PI * angle / 180;
        }

        internal static double ToDegrees(this double angle)
        {
            return angle * (180.0 / Math.PI);
        }
    }
}