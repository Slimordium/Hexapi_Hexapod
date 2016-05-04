// ReSharper disable InconsistentNaming

using System;

namespace HexapiBackground.Enums.Ads1115
{
    [Flags]
    public enum Config : uint
    {
        OS_MASK = 0x8000,
        OS_SINGLE = 0x8000,  // Write: Set to start a single-conversion
        OS_BUSY = 0x0000,  // Read: Bit = 0 when conversion is in progress
        OS_NOTBUSY = 0x8000,  // Read: Bit = 1 when device is not performing a conversion

        MUX_MASK = 0x7000,
        MUX_DIFF_0_1 = 0x0000,  // Differential P = AIN0, N = AIN1 (default)
        MUX_DIFF_0_3 = 0x1000,  // Differential P = AIN0, N = AIN3
        MUX_DIFF_1_3 = 0x2000,  // Differential P = AIN1, N = AIN3
        MUX_DIFF_2_3 = 0x3000,  // Differential P = AIN2, N = AIN3
        MUX_SINGLE_0 = 0x4000,  // Single-ended AIN0
        MUX_SINGLE_1 = 0x5000,  // Single-ended AIN1
        MUX_SINGLE_2 = 0x6000,  // Single-ended AIN2
        MUX_SINGLE_3 = 0x7000,  // Single-ended AIN3

        PGA_MASK = 0x0E00,
        PGA_6_144V = 0x0000,  // +/-6.144V range = Gain 2/3
        PGA_4_096V = 0x0200,  // +/-4.096V range = Gain 1
        PGA_2_048V = 0x0400,  // +/-2.048V range = Gain 2 (default)
        PGA_1_024V = 0x0600,  // +/-1.024V range = Gain 4
        PGA_0_512V = 0x0800,  // +/-0.512V range = Gain 8
        PGA_0_256V = 0x0A00,  // +/-0.256V range = Gain 16

        MODE_MASK = 0x0100,
        MODE_CONTIN = 0x0000,  // Continuous conversion mode
        MODE_SINGLE = 0x0100,  // Power-down single-shot mode (default)

        DR_MASK = 0x00E0,
        DR_128SPS = 0x0000,  // 128 samples per second
        DR_250SPS = 0x0020,  // 250 samples per second
        DR_490SPS = 0x0040,  // 490 samples per second
        DR_920SPS = 0x0060,  // 920 samples per second
        DR_1600SPS = 0x0080,  // 1600 samples per second (default)
        DR_2400SPS = 0x00A0,  // 2400 samples per second
        DR_3300SPS = 0x00C0,  // 3300 samples per second

        CMODE_MASK = 0x0010,
        CMODE_TRAD = 0x0000,  // Traditional comparator with hysteresis (default)
        CMODE_WINDOW = 0x0010,  // Window comparator

        CPOL_MASK = 0x0008,
        CPOL_ACTVLOW = 0x0000,  // ALERT/RDY pin is low when active (default)
        CPOL_ACTVHI = 0x0008,  // ALERT/RDY pin is high when active

        CLAT_MASK = 0x0004,  // Determines if ALERT/RDY pin latches once asserted
        CLAT_NONLAT = 0x0000,  // Non-latching comparator (default)
        CLAT_LATCH = 0x0004,  // Latching comparator

        CQUE_MASK = 0x0003,
        CQUE_1CONV = 0x0000,  // Assert ALERT/RDY after one conversions
        CQUE_2CONV = 0x0001,  // Assert ALERT/RDY after two conversions
        CQUE_4CONV = 0x0002,  // Assert ALERT/RDY after four conversions
        CQUE_NONE = 0x0003  // Disable the comparator and put ALERT/RDY in high state (default)

    }
}