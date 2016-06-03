using System;

namespace HexapiBackground.Helpers
{
    internal static class MathExtensions
    {
        internal static double Map(double valueToMap, double valueToMapMin, double valueToMapMax, double outMin, double outMax)
        {
            return (valueToMap - valueToMapMin)*(outMax - outMin)/(valueToMapMax - valueToMapMin) + outMin;
        }

        internal static double ToRadians(double conversionValue)
        {
            return conversionValue*Math.PI/180;
        }

        internal static double FromRadians(double conversionValue)
        {
            return conversionValue*180/Math.PI;
        }
    }
}