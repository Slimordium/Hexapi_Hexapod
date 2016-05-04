using System;

namespace HexapiBackground.Enums.Ads1115
{
    [Flags]
    public enum Gain : uint
    {
        /// <summary>
        /// +/-6.144V range = Gain 2/3
        /// </summary>
        TwoThirds = 0x0000,

        /// <summary>
        /// +/-4.096V range = Gain 1
        /// </summary>
        One = 0x0200,

        /// <summary>
        /// +/-2.048V range = Gain 2 (default)
        /// </summary>
        Two = 0x0400,

        /// <summary>
        /// +/-1.024V range = Gain 4
        /// </summary>
        Four = 0x0600,

        /// <summary>
        /// +/-0.512V range = Gain 8
        /// </summary>
        Eight = 0x0800,

        /// <summary>
        /// +/-0.256V range = Gain 16
        /// </summary>
        Sixteen = 0x0A00
    }
}