using System;

namespace HexapiBackground.Enums
{
    [Flags]
    internal enum GpsFixQuality
    {
        NoFix,
        StandardGps,
        DiffGps
    }
}