using System;
// ReSharper disable InconsistentNaming

namespace HexapiBackground.Enums
{
    [Flags]
    internal enum GpsFixQuality
    {
        NoFix,
        StandardGps,
        DiffGps,
        PPS,
        RTK,
        FloatRTK,
        Estimated,
        Manual,
        Simulation
    }
}