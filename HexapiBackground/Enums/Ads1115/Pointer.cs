using System;

namespace HexapiBackground.Enums.Ads1115
{
    [Flags]
    public enum Pointer
    {
        ADS1015_REG_POINTER_MASK = 0x03,
        ADS1015_REG_POINTER_CONVERT = 0x00,
        ADS1015_REG_POINTER_CONFIG = 0x01,
        ADS1015_REG_POINTER_LOWTHRESH = 0x02,
        ADS1015_REG_POINTER_HITHRESH = 0x03
    }
}