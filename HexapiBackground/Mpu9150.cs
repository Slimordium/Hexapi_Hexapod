using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace HexapiBackground
{
    internal class Mpu9150
    {
        //This code has been patched together from quite a few arduino examples and libraries.
        //
        // Define registers per MPU6050, Register Map and Descriptions, Rev 4.2, 08/19/2013 6 DOF Motion sensor fusion device
        // Invensense Inc., www.invensense.com
        // See also MPU-9150 Register Map and Descriptions, Revision 4.0, RM-MPU-9150A-00, 9/12/2012 for registers not listed in 
        // above document; the MPU6050 and MPU 9150 are virtually identical but the latter has an on-board magnetic sensor
        //
        //Magnetometer Registers
        private const byte WHO_AM_I_AK8975A = 0x00; // should return 0x48
        private const byte INFO = 0x01;
        private const byte AK8975A_ST1 = 0x02; // data ready status bit 0
        //const byte AK8975A_ADDRESS = 0x0C;
        private const byte AK8975A_XOUT_L = 0x03; // data
        private const byte AK8975A_XOUT_H = 0x04;
        private const byte AK8975A_YOUT_L = 0x05;
        private const byte AK8975A_YOUT_H = 0x06;
        private const byte AK8975A_ZOUT_L = 0x07;
        private const byte AK8975A_ZOUT_H = 0x08;
        private const byte AK8975A_ST2 = 0x09; // Data overflow bit 3 and data read error status bit 2

        private const byte AK8975A_CNTL = 0x0A;
            // Power down (0000), single-measurement (0001), self-test (1000) and Fuse ROM (1111) modes on bits 3:0

        private const byte AK8975A_ASTC = 0x0C; // Self test control
        private const byte AK8975A_ASAX = 0x10; // Fuse ROM x-axis sensitivity adjustment value
        private const byte AK8975A_ASAY = 0x11; // Fuse ROM y-axis sensitivity adjustment value
        private const byte AK8975A_ASAZ = 0x12; // Fuse ROM z-axis sensitivity adjustment value

        private const byte XGOFFS_TC = 0x00; // Bit 7 PWR_MODE, bits 6:1 XG_OFFS_TC, bit 0 OTP_BNK_VLD                 
        private const byte YGOFFS_TC = 0x01;
        private const byte ZGOFFS_TC = 0x02;
        private const byte X_FINE_GAIN = 0x03; // [7:0] fine gain
        private const byte Y_FINE_GAIN = 0x04;
        private const byte Z_FINE_GAIN = 0x05;
        private const byte XA_OFFSET_H = 0x06; // User-defined trim values for accelerometer
        private const byte XA_OFFSET_L_TC = 0x07;
        private const byte YA_OFFSET_H = 0x08;
        private const byte YA_OFFSET_L_TC = 0x09;
        private const byte ZA_OFFSET_H = 0x0A;
        private const byte ZA_OFFSET_L_TC = 0x0B;
        private const byte SELF_TEST_X = 0x0D;
        private const byte SELF_TEST_Y = 0x0E;
        private const byte SELF_TEST_Z = 0x0F;
        private const byte SELF_TEST_A = 0x10;

        private const byte XG_OFFS_USRH = 0x13;
            // User-defined trim values for gyroscope, populate with calibration routine

        private const byte XG_OFFS_USRL = 0x14;
        private const byte YG_OFFS_USRH = 0x15;
        private const byte YG_OFFS_USRL = 0x16;
        private const byte ZG_OFFS_USRH = 0x17;
        private const byte ZG_OFFS_USRL = 0x18;
        private const byte SMPLRT_DIV = 0x19;
        private const byte CONFIG = 0x1A;
        private const byte GYRO_CONFIG = 0x1B;
        private const byte ACCEL_CONFIG = 0x1C;
        private const byte FF_THR = 0x1D; // Free-fall
        private const byte FF_DUR = 0x1E; // Free-fall
        private const byte MOT_THR = 0x1F; // Motion detection threshold bits [7:0]

        private const byte MOT_DUR = 0x20;
            // Duration counter threshold for motion interrupt generation, 1 kHz rate, LSB = 1 ms

        private const byte ZMOT_THR = 0x21; // Zero-motion detection threshold bits [7:0]

        private const byte ZRMOT_DUR = 0x22;
            // Duration counter threshold for zero motion interrupt generation, 16 Hz rate, LSB = 64 ms

        private const byte FIFO_EN = 0x23;
        private const byte I2C_MST_CTRL = 0x24;
        private const byte I2C_SLV0_ADDR = 0x25;
        private const byte I2C_SLV0_REG = 0x26;
        private const byte I2C_SLV0_CTRL = 0x27;
        private const byte I2C_SLV1_ADDR = 0x28;
        private const byte I2C_SLV1_REG = 0x29;
        private const byte I2C_SLV1_CTRL = 0x2A;
        private const byte I2C_SLV2_ADDR = 0x2B;
        private const byte I2C_SLV2_REG = 0x2C;
        private const byte I2C_SLV2_CTRL = 0x2D;
        private const byte I2C_SLV3_ADDR = 0x2E;
        private const byte I2C_SLV3_REG = 0x2F;
        private const byte I2C_SLV3_CTRL = 0x30;
        private const byte I2C_SLV4_ADDR = 0x31;
        private const byte I2C_SLV4_REG = 0x32;
        private const byte I2C_SLV4_DO = 0x33;
        private const byte I2C_SLV4_CTRL = 0x34;
        private const byte I2C_SLV4_DI = 0x35;
        private const byte I2C_MST_STATUS = 0x36;
        private const byte INT_PIN_CFG = 0x37;
        private const byte INT_ENABLE = 0x38;
        private const byte DMP_INT_STATUS = 0x39; // Check DMP interrupt
        private const byte INT_STATUS = 0x3A;
        private const byte ACCEL_XOUT_H = 0x3B;
        private const byte ACCEL_XOUT_L = 0x3C;
        private const byte ACCEL_YOUT_H = 0x3D;
        private const byte ACCEL_YOUT_L = 0x3E;
        private const byte ACCEL_ZOUT_H = 0x3F;
        private const byte ACCEL_ZOUT_L = 0x40;
        private const byte TEMP_OUT_H = 0x41;
        private const byte TEMP_OUT_L = 0x42;
        private const byte GYRO_XOUT_H = 0x43;
        private const byte GYRO_XOUT_L = 0x44;
        private const byte GYRO_YOUT_H = 0x45;
        private const byte GYRO_YOUT_L = 0x46;
        private const byte GYRO_ZOUT_H = 0x47;
        private const byte GYRO_ZOUT_L = 0x48;
        private const byte EXT_SENS_DATA_00 = 0x49;
        private const byte EXT_SENS_DATA_01 = 0x4A;
        private const byte EXT_SENS_DATA_02 = 0x4B;
        private const byte EXT_SENS_DATA_03 = 0x4C;
        private const byte EXT_SENS_DATA_04 = 0x4D;
        private const byte EXT_SENS_DATA_05 = 0x4E;
        private const byte EXT_SENS_DATA_06 = 0x4F;
        private const byte EXT_SENS_DATA_07 = 0x50;
        private const byte EXT_SENS_DATA_08 = 0x51;
        private const byte EXT_SENS_DATA_09 = 0x52;
        private const byte EXT_SENS_DATA_10 = 0x53;
        private const byte EXT_SENS_DATA_11 = 0x54;
        private const byte EXT_SENS_DATA_12 = 0x55;
        private const byte EXT_SENS_DATA_13 = 0x56;
        private const byte EXT_SENS_DATA_14 = 0x57;
        private const byte EXT_SENS_DATA_15 = 0x58;
        private const byte EXT_SENS_DATA_16 = 0x59;
        private const byte EXT_SENS_DATA_17 = 0x5A;
        private const byte EXT_SENS_DATA_18 = 0x5B;
        private const byte EXT_SENS_DATA_19 = 0x5C;
        private const byte EXT_SENS_DATA_20 = 0x5D;
        private const byte EXT_SENS_DATA_21 = 0x5E;
        private const byte EXT_SENS_DATA_22 = 0x5F;
        private const byte EXT_SENS_DATA_23 = 0x60;
        private const byte MOT_DETECT_STATUS = 0x61;
        private const byte I2C_SLV0_DO = 0x63;
        private const byte I2C_SLV1_DO = 0x64;
        private const byte I2C_SLV2_DO = 0x65;
        private const byte I2C_SLV3_DO = 0x66;
        private const byte I2C_MST_DELAY_CTRL = 0x67;
        private const byte SIGNAL_PATH_RESET = 0x68;
        private const byte MOT_DETECT_CTRL = 0x69;
        private const byte USER_CTRL = 0x6A; // Bit 7 enable DMP, bit 3 reset DMP
        private const byte PWR_MGMT_1 = 0x6B; // Device defaults to the SLEEP mode
        private const byte PWR_MGMT_2 = 0x6C;
        private const byte DMP_BANK = 0x6D; // Activates a specific bank in the DMP

        private const byte DMP_RW_PNT = 0x6E;
            // Set read/write pointer to a specific start address in specified DMP bank

        private const byte DMP_REG = 0x6F; // Register in DMP from which to read or to which to write
        private const byte DMP_REG_1 = 0x70;
        private const byte DMP_REG_2 = 0x71;
        private const byte FIFO_COUNTH = 0x72;
        private const byte FIFO_COUNTL = 0x73;
        private const byte FIFO_R_W = 0x74;
        private const byte WHO_AM_I_MPU9150 = 0x75; // Should return 0x68

        // parameters for 6 DoF sensor fusion calculations
        private const double GyroMeasError = Math.PI*(60.0f/180.0f);
            // gyroscope measurement error in rads/s (start at 60 deg/s), then reduce after ~10 s to 3

        private const double GyroMeasDrift = Math.PI*(1.0f/180.0f);
            // gyroscope measurement drift in rad/s/s (start at 0.0 deg/s/s)

        private readonly GpioController _gpioController = GpioController.GetDefault();
        private readonly I2CDevice _ak8975A = new I2CDevice(0x0c, I2cBusSpeed.FastMode);

        private readonly Ascale _Ascale = Ascale.AFS_2G; // AFS_2G, AFS_4G, AFS_8G, AFS_16G
        private readonly Gscale _Gscale = Gscale.GFS_250DPS; // GFS_250DPS, GFS_500DPS, GFS_1000DPS, GFS_2000DPS
        private GpioPin _intPin;

        private readonly I2CDevice _mpu9150 = new I2CDevice(0x68, I2cBusSpeed.FastMode);

        private readonly ushort[] accelBias = {0, 0, 0}; // Bias corrections for gyro and accelerometer
        private double aRes, gRes, mRes; // scale resolutions per LSB for the sensors

        private int delt_t = 0; // used to control display output rate

        private readonly ushort[] gyroBias = {0, 0, 0};

        // Pin definitions
        private int intPin = 12; // These can be changed, 2 and 3 are the Arduinos ext int pins

        private double[] magCalibration = {0, 0, 0}; // Factory mag calibration and mag bias
        private readonly double[] magbias = {0, 0, 0}; // Factory mag calibration and mag bias

        //TODO : I have read quite a few places that this needs to be sent on startup. However if I do send it, the MPU stops working. Not sure on that
        internal byte[] Mpu9150DmpCode =
        {
            // bank 0, 256 
            0x00, 0x00, 0x70, 0x00, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x00,
            0x00, 0x65, 0x00, 0x54, 0xff, 0xef, 0x00, 0x00, 0xfa, 0x80, 0x00, 0x0b, 0x12, 0x82, 0x00, 0x01,
            0x03, 0x0c, 0x30, 0xc3, 0x0e, 0x8c, 0x8c, 0xe9, 0x14, 0xd5, 0x40, 0x02, 0x13, 0x71, 0x0f, 0x8e,
            0x38, 0x83, 0xf8, 0x83, 0x30, 0x00, 0xf8, 0x83, 0x25, 0x8e, 0xf8, 0x83, 0x30, 0x00, 0xf8, 0x83,
            0xff, 0xff, 0xff, 0xff, 0x0f, 0xfe, 0xa9, 0xd6, 0x24, 0x00, 0x04, 0x00, 0x1a, 0x82, 0x79, 0xa1,
            0x00, 0x00, 0x00, 0x3c, 0xff, 0xff, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x38, 0x83, 0x6f, 0xa2,
            0x00, 0x3e, 0x03, 0x30, 0x40, 0x00, 0x00, 0x00, 0x02, 0xca, 0xe3, 0x09, 0x3e, 0x80, 0x00, 0x00,
            0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x00,
            0x00, 0x0c, 0x00, 0x00, 0x00, 0x0c, 0x18, 0x6e, 0x00, 0x00, 0x06, 0x92, 0x0a, 0x16, 0xc0, 0xdf,
            0xff, 0xff, 0x02, 0x56, 0xfd, 0x8c, 0xd3, 0x77, 0xff, 0xe1, 0xc4, 0x96, 0xe0, 0xc5, 0xbe, 0xaa,
            0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0b, 0x2b, 0x00, 0x00, 0x16, 0x57, 0x00, 0x00, 0x03, 0x59,
            0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1d, 0xfa, 0x00, 0x02, 0x6c, 0x1d, 0x00, 0x00, 0x00, 0x00,
            0x3f, 0xff, 0xdf, 0xeb, 0x00, 0x3e, 0xb3, 0xb6, 0x00, 0x0d, 0x22, 0x78, 0x00, 0x00, 0x2f, 0x3c,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x19, 0x42, 0xb5, 0x00, 0x00, 0x39, 0xa2, 0x00, 0x00, 0xb3, 0x65,
            0xd9, 0x0e, 0x9f, 0xc9, 0x1d, 0xcf, 0x4c, 0x34, 0x30, 0x00, 0x00, 0x00, 0x50, 0x00, 0x00, 0x00,
            0x3b, 0xb6, 0x7a, 0xe8, 0x00, 0x64, 0x00, 0x00, 0x00, 0xc8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            /* bank # 1 */
            0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0xfa, 0x92, 0x10, 0x00, 0x22, 0x5e, 0x00, 0x0d, 0x22, 0x9f,
            0x00, 0x01, 0x00, 0x00, 0x00, 0x32, 0x00, 0x00, 0xff, 0x46, 0x00, 0x00, 0x63, 0xd4, 0x00, 0x00,
            0x10, 0x00, 0x00, 0x00, 0x04, 0xd6, 0x00, 0x00, 0x04, 0xcc, 0x00, 0x00, 0x04, 0xcc, 0x00, 0x00,
            0x00, 0x00, 0x10, 0x72, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x06, 0x00, 0x02, 0x00, 0x05, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x05, 0x00, 0x64, 0x00, 0x20, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x03, 0x00,
            0x00, 0x00, 0x00, 0x32, 0xf8, 0x98, 0x00, 0x00, 0xff, 0x65, 0x00, 0x00, 0x83, 0x0f, 0x00, 0x00,
            0xff, 0x9b, 0xfc, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00,
            0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0xb2, 0x6a, 0x00, 0x02, 0x00, 0x00,
            0x00, 0x01, 0xfb, 0x83, 0x00, 0x68, 0x00, 0x00, 0x00, 0xd9, 0xfc, 0x00, 0x7c, 0xf1, 0xff, 0x83,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x65, 0x00, 0x00, 0x00, 0x64, 0x03, 0xe8, 0x00, 0x64, 0x00, 0x28,
            0x00, 0x00, 0x00, 0x25, 0x00, 0x00, 0x00, 0x00, 0x16, 0xa0, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00,
            0x00, 0x00, 0x10, 0x00, 0x00, 0x2f, 0x00, 0x00, 0x00, 0x00, 0x01, 0xf4, 0x00, 0x00, 0x10, 0x00,
            /* bank # 2 */
            0x00, 0x28, 0x00, 0x00, 0xff, 0xff, 0x45, 0x81, 0xff, 0xff, 0xfa, 0x72, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x44, 0x00, 0x05, 0x00, 0x05, 0xba, 0xc6, 0x00, 0x47, 0x78, 0xa2,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x14,
            0x00, 0x00, 0x25, 0x4d, 0x00, 0x2f, 0x70, 0x6d, 0x00, 0x00, 0x05, 0xae, 0x00, 0x0c, 0x02, 0xd0,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x1b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x64, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x1b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0e, 0x00, 0x0e,
            0x00, 0x00, 0x0a, 0xc7, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x32, 0xff, 0xff, 0xff, 0x9c,
            0x00, 0x00, 0x0b, 0x2b, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x64,
            0xff, 0xe5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            /* bank # 3 */
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x80, 0x00, 0x00, 0x01, 0x80, 0x00, 0x00, 0x01, 0x80, 0x00, 0x00, 0x24, 0x26, 0xd3,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x10, 0x00, 0x96, 0x00, 0x3c,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x0c, 0x0a, 0x4e, 0x68, 0xcd, 0xcf, 0x77, 0x09, 0x50, 0x16, 0x67, 0x59, 0xc6, 0x19, 0xce, 0x82,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x17, 0xd7, 0x84, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xc7, 0x93, 0x8f, 0x9d, 0x1e, 0x1b, 0x1c, 0x19,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x03, 0x18, 0x85, 0x00, 0x00, 0x40, 0x00,
            0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x67, 0x7d, 0xdf, 0x7e, 0x72, 0x90, 0x2e, 0x55, 0x4c, 0xf6, 0xe6, 0x88,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            /* bank # 4 */
            0xd8, 0xdc, 0xb4, 0xb8, 0xb0, 0xd8, 0xb9, 0xab, 0xf3, 0xf8, 0xfa, 0xb3, 0xb7, 0xbb, 0x8e, 0x9e,
            0xae, 0xf1, 0x32, 0xf5, 0x1b, 0xf1, 0xb4, 0xb8, 0xb0, 0x80, 0x97, 0xf1, 0xa9, 0xdf, 0xdf, 0xdf,
            0xaa, 0xdf, 0xdf, 0xdf, 0xf2, 0xaa, 0xc5, 0xcd, 0xc7, 0xa9, 0x0c, 0xc9, 0x2c, 0x97, 0xf1, 0xa9,
            0x89, 0x26, 0x46, 0x66, 0xb2, 0x89, 0x99, 0xa9, 0x2d, 0x55, 0x7d, 0xb0, 0xb0, 0x8a, 0xa8, 0x96,
            0x36, 0x56, 0x76, 0xf1, 0xba, 0xa3, 0xb4, 0xb2, 0x80, 0xc0, 0xb8, 0xa8, 0x97, 0x11, 0xb2, 0x83,
            0x98, 0xba, 0xa3, 0xf0, 0x24, 0x08, 0x44, 0x10, 0x64, 0x18, 0xb2, 0xb9, 0xb4, 0x98, 0x83, 0xf1,
            0xa3, 0x29, 0x55, 0x7d, 0xba, 0xb5, 0xb1, 0xa3, 0x83, 0x93, 0xf0, 0x00, 0x28, 0x50, 0xf5, 0xb2,
            0xb6, 0xaa, 0x83, 0x93, 0x28, 0x54, 0x7c, 0xf1, 0xb9, 0xa3, 0x82, 0x93, 0x61, 0xba, 0xa2, 0xda,
            0xde, 0xdf, 0xdb, 0x81, 0x9a, 0xb9, 0xae, 0xf5, 0x60, 0x68, 0x70, 0xf1, 0xda, 0xba, 0xa2, 0xdf,
            0xd9, 0xba, 0xa2, 0xfa, 0xb9, 0xa3, 0x82, 0x92, 0xdb, 0x31, 0xba, 0xa2, 0xd9, 0xba, 0xa2, 0xf8,
            0xdf, 0x85, 0xa4, 0xd0, 0xc1, 0xbb, 0xad, 0x83, 0xc2, 0xc5, 0xc7, 0xb8, 0xa2, 0xdf, 0xdf, 0xdf,
            0xba, 0xa0, 0xdf, 0xdf, 0xdf, 0xd8, 0xd8, 0xf1, 0xb8, 0xaa, 0xb3, 0x8d, 0xb4, 0x98, 0x0d, 0x35,
            0x5d, 0xb2, 0xb6, 0xba, 0xaf, 0x8c, 0x96, 0x19, 0x8f, 0x9f, 0xa7, 0x0e, 0x16, 0x1e, 0xb4, 0x9a,
            0xb8, 0xaa, 0x87, 0x2c, 0x54, 0x7c, 0xba, 0xa4, 0xb0, 0x8a, 0xb6, 0x91, 0x32, 0x56, 0x76, 0xb2,
            0x84, 0x94, 0xa4, 0xc8, 0x08, 0xcd, 0xd8, 0xb8, 0xb4, 0xb0, 0xf1, 0x99, 0x82, 0xa8, 0x2d, 0x55,
            0x7d, 0x98, 0xa8, 0x0e, 0x16, 0x1e, 0xa2, 0x2c, 0x54, 0x7c, 0x92, 0xa4, 0xf0, 0x2c, 0x50, 0x78,
            /* bank # 5 */
            0xf1, 0x84, 0xa8, 0x98, 0xc4, 0xcd, 0xfc, 0xd8, 0x0d, 0xdb, 0xa8, 0xfc, 0x2d, 0xf3, 0xd9, 0xba,
            0xa6, 0xf8, 0xda, 0xba, 0xa6, 0xde, 0xd8, 0xba, 0xb2, 0xb6, 0x86, 0x96, 0xa6, 0xd0, 0xf3, 0xc8,
            0x41, 0xda, 0xa6, 0xc8, 0xf8, 0xd8, 0xb0, 0xb4, 0xb8, 0x82, 0xa8, 0x92, 0xf5, 0x2c, 0x54, 0x88,
            0x98, 0xf1, 0x35, 0xd9, 0xf4, 0x18, 0xd8, 0xf1, 0xa2, 0xd0, 0xf8, 0xf9, 0xa8, 0x84, 0xd9, 0xc7,
            0xdf, 0xf8, 0xf8, 0x83, 0xc5, 0xda, 0xdf, 0x69, 0xdf, 0x83, 0xc1, 0xd8, 0xf4, 0x01, 0x14, 0xf1,
            0xa8, 0x82, 0x4e, 0xa8, 0x84, 0xf3, 0x11, 0xd1, 0x82, 0xf5, 0xd9, 0x92, 0x28, 0x97, 0x88, 0xf1,
            0x09, 0xf4, 0x1c, 0x1c, 0xd8, 0x84, 0xa8, 0xf3, 0xc0, 0xf9, 0xd1, 0xd9, 0x97, 0x82, 0xf1, 0x29,
            0xf4, 0x0d, 0xd8, 0xf3, 0xf9, 0xf9, 0xd1, 0xd9, 0x82, 0xf4, 0xc2, 0x03, 0xd8, 0xde, 0xdf, 0x1a,
            0xd8, 0xf1, 0xa2, 0xfa, 0xf9, 0xa8, 0x84, 0x98, 0xd9, 0xc7, 0xdf, 0xf8, 0xf8, 0xf8, 0x83, 0xc7,
            0xda, 0xdf, 0x69, 0xdf, 0xf8, 0x83, 0xc3, 0xd8, 0xf4, 0x01, 0x14, 0xf1, 0x98, 0xa8, 0x82, 0x2e,
            0xa8, 0x84, 0xf3, 0x11, 0xd1, 0x82, 0xf5, 0xd9, 0x92, 0x50, 0x97, 0x88, 0xf1, 0x09, 0xf4, 0x1c,
            0xd8, 0x84, 0xa8, 0xf3, 0xc0, 0xf8, 0xf9, 0xd1, 0xd9, 0x97, 0x82, 0xf1, 0x49, 0xf4, 0x0d, 0xd8,
            0xf3, 0xf9, 0xf9, 0xd1, 0xd9, 0x82, 0xf4, 0xc4, 0x03, 0xd8, 0xde, 0xdf, 0xd8, 0xf1, 0xad, 0x88,
            0x98, 0xcc, 0xa8, 0x09, 0xf9, 0xd9, 0x82, 0x92, 0xa8, 0xf5, 0x7c, 0xf1, 0x88, 0x3a, 0xcf, 0x94,
            0x4a, 0x6e, 0x98, 0xdb, 0x69, 0x31, 0xda, 0xad, 0xf2, 0xde, 0xf9, 0xd8, 0x87, 0x95, 0xa8, 0xf2,
            0x21, 0xd1, 0xda, 0xa5, 0xf9, 0xf4, 0x17, 0xd9, 0xf1, 0xae, 0x8e, 0xd0, 0xc0, 0xc3, 0xae, 0x82,
            /* bank # 6 */
            0xc6, 0x84, 0xc3, 0xa8, 0x85, 0x95, 0xc8, 0xa5, 0x88, 0xf2, 0xc0, 0xf1, 0xf4, 0x01, 0x0e, 0xf1,
            0x8e, 0x9e, 0xa8, 0xc6, 0x3e, 0x56, 0xf5, 0x54, 0xf1, 0x88, 0x72, 0xf4, 0x01, 0x15, 0xf1, 0x98,
            0x45, 0x85, 0x6e, 0xf5, 0x8e, 0x9e, 0x04, 0x88, 0xf1, 0x42, 0x98, 0x5a, 0x8e, 0x9e, 0x06, 0x88,
            0x69, 0xf4, 0x01, 0x1c, 0xf1, 0x98, 0x1e, 0x11, 0x08, 0xd0, 0xf5, 0x04, 0xf1, 0x1e, 0x97, 0x02,
            0x02, 0x98, 0x36, 0x25, 0xdb, 0xf9, 0xd9, 0x85, 0xa5, 0xf3, 0xc1, 0xda, 0x85, 0xa5, 0xf3, 0xdf,
            0xd8, 0x85, 0x95, 0xa8, 0xf3, 0x09, 0xda, 0xa5, 0xfa, 0xd8, 0x82, 0x92, 0xa8, 0xf5, 0x78, 0xf1,
            0x88, 0x1a, 0x84, 0x9f, 0x26, 0x88, 0x98, 0x21, 0xda, 0xf4, 0x1d, 0xf3, 0xd8, 0x87, 0x9f, 0x39,
            0xd1, 0xaf, 0xd9, 0xdf, 0xdf, 0xfb, 0xf9, 0xf4, 0x0c, 0xf3, 0xd8, 0xfa, 0xd0, 0xf8, 0xda, 0xf9,
            0xf9, 0xd0, 0xdf, 0xd9, 0xf9, 0xd8, 0xf4, 0x0b, 0xd8, 0xf3, 0x87, 0x9f, 0x39, 0xd1, 0xaf, 0xd9,
            0xdf, 0xdf, 0xf4, 0x1d, 0xf3, 0xd8, 0xfa, 0xfc, 0xa8, 0x69, 0xf9, 0xf9, 0xaf, 0xd0, 0xda, 0xde,
            0xfa, 0xd9, 0xf8, 0x8f, 0x9f, 0xa8, 0xf1, 0xcc, 0xf3, 0x98, 0xdb, 0x45, 0xd9, 0xaf, 0xdf, 0xd0,
            0xf8, 0xd8, 0xf1, 0x8f, 0x9f, 0xa8, 0xca, 0xf3, 0x88, 0x09, 0xda, 0xaf, 0x8f, 0xcb, 0xf8, 0xd8,
            0xf2, 0xad, 0x97, 0x8d, 0x0c, 0xd9, 0xa5, 0xdf, 0xf9, 0xba, 0xa6, 0xf3, 0xfa, 0xf4, 0x12, 0xf2,
            0xd8, 0x95, 0x0d, 0xd1, 0xd9, 0xba, 0xa6, 0xf3, 0xfa, 0xda, 0xa5, 0xf2, 0xc1, 0xba, 0xa6, 0xf3,
            0xdf, 0xd8, 0xf1, 0xba, 0xb2, 0xb6, 0x86, 0x96, 0xa6, 0xd0, 0xca, 0xf3, 0x49, 0xda, 0xa6, 0xcb,
            0xf8, 0xd8, 0xb0, 0xb4, 0xb8, 0xd8, 0xad, 0x84, 0xf2, 0xc0, 0xdf, 0xf1, 0x8f, 0xcb, 0xc3, 0xa8,
            /* bank # 7 */
            0xb2, 0xb6, 0x86, 0x96, 0xc8, 0xc1, 0xcb, 0xc3, 0xf3, 0xb0, 0xb4, 0x88, 0x98, 0xa8, 0x21, 0xdb,
            0x71, 0x8d, 0x9d, 0x71, 0x85, 0x95, 0x21, 0xd9, 0xad, 0xf2, 0xfa, 0xd8, 0x85, 0x97, 0xa8, 0x28,
            0xd9, 0xf4, 0x08, 0xd8, 0xf2, 0x8d, 0x29, 0xda, 0xf4, 0x05, 0xd9, 0xf2, 0x85, 0xa4, 0xc2, 0xf2,
            0xd8, 0xa8, 0x8d, 0x94, 0x01, 0xd1, 0xd9, 0xf4, 0x11, 0xf2, 0xd8, 0x87, 0x21, 0xd8, 0xf4, 0x0a,
            0xd8, 0xf2, 0x84, 0x98, 0xa8, 0xc8, 0x01, 0xd1, 0xd9, 0xf4, 0x11, 0xd8, 0xf3, 0xa4, 0xc8, 0xbb,
            0xaf, 0xd0, 0xf2, 0xde, 0xf8, 0xf8, 0xf8, 0xf8, 0xf8, 0xf8, 0xf8, 0xf8, 0xd8, 0xf1, 0xb8, 0xf6,
            0xb5, 0xb9, 0xb0, 0x8a, 0x95, 0xa3, 0xde, 0x3c, 0xa3, 0xd9, 0xf8, 0xd8, 0x5c, 0xa3, 0xd9, 0xf8,
            0xd8, 0x7c, 0xa3, 0xd9, 0xf8, 0xd8, 0xf8, 0xf9, 0xd1, 0xa5, 0xd9, 0xdf, 0xda, 0xfa, 0xd8, 0xb1,
            0x85, 0x30, 0xf7, 0xd9, 0xde, 0xd8, 0xf8, 0x30, 0xad, 0xda, 0xde, 0xd8, 0xf2, 0xb4, 0x8c, 0x99,
            0xa3, 0x2d, 0x55, 0x7d, 0xa0, 0x83, 0xdf, 0xdf, 0xdf, 0xb5, 0x91, 0xa0, 0xf6, 0x29, 0xd9, 0xfb,
            0xd8, 0xa0, 0xfc, 0x29, 0xd9, 0xfa, 0xd8, 0xa0, 0xd0, 0x51, 0xd9, 0xf8, 0xd8, 0xfc, 0x51, 0xd9,
            0xf9, 0xd8, 0x79, 0xd9, 0xfb, 0xd8, 0xa0, 0xd0, 0xfc, 0x79, 0xd9, 0xfa, 0xd8, 0xa1, 0xf9, 0xf9,
            0xf9, 0xf9, 0xf9, 0xa0, 0xda, 0xdf, 0xdf, 0xdf, 0xd8, 0xa1, 0xf8, 0xf8, 0xf8, 0xf8, 0xf8, 0xac,
            0xde, 0xf8, 0xad, 0xde, 0x83, 0x93, 0xac, 0x2c, 0x54, 0x7c, 0xf1, 0xa8, 0xdf, 0xdf, 0xdf, 0xf6,
            0x9d, 0x2c, 0xda, 0xa0, 0xdf, 0xd9, 0xfa, 0xdb, 0x2d, 0xf8, 0xd8, 0xa8, 0x50, 0xda, 0xa0, 0xd0,
            0xde, 0xd9, 0xd0, 0xf8, 0xf8, 0xf8, 0xdb, 0x55, 0xf8, 0xd8, 0xa8, 0x78, 0xda, 0xa0, 0xd0, 0xdf,
            /* bank # 8 */
            0xd9, 0xd0, 0xfa, 0xf8, 0xf8, 0xf8, 0xf8, 0xdb, 0x7d, 0xf8, 0xd8, 0x9c, 0xa8, 0x8c, 0xf5, 0x30,
            0xdb, 0x38, 0xd9, 0xd0, 0xde, 0xdf, 0xa0, 0xd0, 0xde, 0xdf, 0xd8, 0xa8, 0x48, 0xdb, 0x58, 0xd9,
            0xdf, 0xd0, 0xde, 0xa0, 0xdf, 0xd0, 0xde, 0xd8, 0xa8, 0x68, 0xdb, 0x70, 0xd9, 0xdf, 0xdf, 0xa0,
            0xdf, 0xdf, 0xd8, 0xf1, 0xa8, 0x88, 0x90, 0x2c, 0x54, 0x7c, 0x98, 0xa8, 0xd0, 0x5c, 0x38, 0xd1,
            0xda, 0xf2, 0xae, 0x8c, 0xdf, 0xf9, 0xd8, 0xb0, 0x87, 0xa8, 0xc1, 0xc1, 0xb1, 0x88, 0xa8, 0xc6,
            0xf9, 0xf9, 0xda, 0x36, 0xd8, 0xa8, 0xf9, 0xda, 0x36, 0xd8, 0xa8, 0xf9, 0xda, 0x36, 0xd8, 0xa8,
            0xf9, 0xda, 0x36, 0xd8, 0xa8, 0xf9, 0xda, 0x36, 0xd8, 0xf7, 0x8d, 0x9d, 0xad, 0xf8, 0x18, 0xda,
            0xf2, 0xae, 0xdf, 0xd8, 0xf7, 0xad, 0xfa, 0x30, 0xd9, 0xa4, 0xde, 0xf9, 0xd8, 0xf2, 0xae, 0xde,
            0xfa, 0xf9, 0x83, 0xa7, 0xd9, 0xc3, 0xc5, 0xc7, 0xf1, 0x88, 0x9b, 0xa7, 0x7a, 0xad, 0xf7, 0xde,
            0xdf, 0xa4, 0xf8, 0x84, 0x94, 0x08, 0xa7, 0x97, 0xf3, 0x00, 0xae, 0xf2, 0x98, 0x19, 0xa4, 0x88,
            0xc6, 0xa3, 0x94, 0x88, 0xf6, 0x32, 0xdf, 0xf2, 0x83, 0x93, 0xdb, 0x09, 0xd9, 0xf2, 0xaa, 0xdf,
            0xd8, 0xd8, 0xae, 0xf8, 0xf9, 0xd1, 0xda, 0xf3, 0xa4, 0xde, 0xa7, 0xf1, 0x88, 0x9b, 0x7a, 0xd8,
            0xf3, 0x84, 0x94, 0xae, 0x19, 0xf9, 0xda, 0xaa, 0xf1, 0xdf, 0xd8, 0xa8, 0x81, 0xc0, 0xc3, 0xc5,
            0xc7, 0xa3, 0x92, 0x83, 0xf6, 0x28, 0xad, 0xde, 0xd9, 0xf8, 0xd8, 0xa3, 0x50, 0xad, 0xd9, 0xf8,
            0xd8, 0xa3, 0x78, 0xad, 0xd9, 0xf8, 0xd8, 0xf8, 0xf9, 0xd1, 0xa1, 0xda, 0xde, 0xc3, 0xc5, 0xc7,
            0xd8, 0xa1, 0x81, 0x94, 0xf8, 0x18, 0xf2, 0xb0, 0x89, 0xac, 0xc3, 0xc5, 0xc7, 0xf1, 0xd8, 0xb8,
            /* bank # 9 */
            0xb4, 0xb0, 0x97, 0x86, 0xa8, 0x31, 0x9b, 0x06, 0x99, 0x07, 0xab, 0x97, 0x28, 0x88, 0x9b, 0xf0,
            0x0c, 0x20, 0x14, 0x40, 0xb0, 0xb4, 0xb8, 0xf0, 0xa8, 0x8a, 0x9a, 0x28, 0x50, 0x78, 0xb7, 0x9b,
            0xa8, 0x29, 0x51, 0x79, 0x24, 0x70, 0x59, 0x44, 0x69, 0x38, 0x64, 0x48, 0x31, 0xf1, 0xbb, 0xab,
            0x88, 0x00, 0x2c, 0x54, 0x7c, 0xf0, 0xb3, 0x8b, 0xb8, 0xa8, 0x04, 0x28, 0x50, 0x78, 0xf1, 0xb0,
            0x88, 0xb4, 0x97, 0x26, 0xa8, 0x59, 0x98, 0xbb, 0xab, 0xb3, 0x8b, 0x02, 0x26, 0x46, 0x66, 0xb0,
            0xb8, 0xf0, 0x8a, 0x9c, 0xa8, 0x29, 0x51, 0x79, 0x8b, 0x29, 0x51, 0x79, 0x8a, 0x24, 0x70, 0x59,
            0x8b, 0x20, 0x58, 0x71, 0x8a, 0x44, 0x69, 0x38, 0x8b, 0x39, 0x40, 0x68, 0x8a, 0x64, 0x48, 0x31,
            0x8b, 0x30, 0x49, 0x60, 0x88, 0xf1, 0xac, 0x00, 0x2c, 0x54, 0x7c, 0xf0, 0x8c, 0xa8, 0x04, 0x28,
            0x50, 0x78, 0xf1, 0x88, 0x97, 0x26, 0xa8, 0x59, 0x98, 0xac, 0x8c, 0x02, 0x26, 0x46, 0x66, 0xf0,
            0x89, 0x9c, 0xa8, 0x29, 0x51, 0x79, 0x24, 0x70, 0x59, 0x44, 0x69, 0x38, 0x64, 0x48, 0x31, 0xa9,
            0x88, 0x09, 0x20, 0x59, 0x70, 0xab, 0x11, 0x38, 0x40, 0x69, 0xa8, 0x19, 0x31, 0x48, 0x60, 0x8c,
            0xa8, 0x3c, 0x41, 0x5c, 0x20, 0x7c, 0x00, 0xf1, 0x87, 0x98, 0x19, 0x86, 0xa8, 0x6e, 0x76, 0x7e,
            0xa9, 0x99, 0x88, 0x2d, 0x55, 0x7d, 0xd8, 0xb1, 0xb5, 0xb9, 0xa3, 0xdf, 0xdf, 0xdf, 0xae, 0xd0,
            0xdf, 0xaa, 0xd0, 0xde, 0xf2, 0xab, 0xf8, 0xf9, 0xd9, 0xb0, 0x87, 0xc4, 0xaa, 0xf1, 0xdf, 0xdf,
            0xbb, 0xaf, 0xdf, 0xdf, 0xb9, 0xd8, 0xb1, 0xf1, 0xa3, 0x97, 0x8e, 0x60, 0xdf, 0xb0, 0x84, 0xf2,
            0xc8, 0xf8, 0xf9, 0xd9, 0xde, 0xd8, 0x93, 0x85, 0xf1, 0x4a, 0xb1, 0x83, 0xa3, 0x08, 0xb5, 0x83,
            /* bank # 10 */
            0x9a, 0x08, 0x10, 0xb7, 0x9f, 0x10, 0xd8, 0xf1, 0xb0, 0xba, 0xae, 0xb0, 0x8a, 0xc2, 0xb2, 0xb6,
            0x8e, 0x9e, 0xf1, 0xfb, 0xd9, 0xf4, 0x1d, 0xd8, 0xf9, 0xd9, 0x0c, 0xf1, 0xd8, 0xf8, 0xf8, 0xad,
            0x61, 0xd9, 0xae, 0xfb, 0xd8, 0xf4, 0x0c, 0xf1, 0xd8, 0xf8, 0xf8, 0xad, 0x19, 0xd9, 0xae, 0xfb,
            0xdf, 0xd8, 0xf4, 0x16, 0xf1, 0xd8, 0xf8, 0xad, 0x8d, 0x61, 0xd9, 0xf4, 0xf4, 0xac, 0xf5, 0x9c,
            0x9c, 0x8d, 0xdf, 0x2b, 0xba, 0xb6, 0xae, 0xfa, 0xf8, 0xf4, 0x0b, 0xd8, 0xf1, 0xae, 0xd0, 0xf8,
            0xad, 0x51, 0xda, 0xae, 0xfa, 0xf8, 0xf1, 0xd8, 0xb9, 0xb1, 0xb6, 0xa3, 0x83, 0x9c, 0x08, 0xb9,
            0xb1, 0x83, 0x9a, 0xb5, 0xaa, 0xc0, 0xfd, 0x30, 0x83, 0xb7, 0x9f, 0x10, 0xb5, 0x8b, 0x93, 0xf2,
            0x02, 0x02, 0xd1, 0xab, 0xda, 0xde, 0xd8, 0xf1, 0xb0, 0x80, 0xba, 0xab, 0xc0, 0xc3, 0xb2, 0x84,
            0xc1, 0xc3, 0xd8, 0xb1, 0xb9, 0xf3, 0x8b, 0xa3, 0x91, 0xb6, 0x09, 0xb4, 0xd9, 0xab, 0xde, 0xb0,
            0x87, 0x9c, 0xb9, 0xa3, 0xdd, 0xf1, 0xb3, 0x8b, 0x8b, 0x8b, 0x8b, 0x8b, 0xb0, 0x87, 0xa3, 0xa3,
            0xa3, 0xa3, 0xb2, 0x8b, 0xb6, 0x9b, 0xf2, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3,
            0xa3, 0xf1, 0xb0, 0x87, 0xb5, 0x9a, 0xa3, 0xf3, 0x9b, 0xa3, 0xa3, 0xdc, 0xba, 0xac, 0xdf, 0xb9,
            0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3, 0xa3,
            0xd8, 0xd8, 0xd8, 0xbb, 0xb3, 0xb7, 0xf1, 0xaa, 0xf9, 0xda, 0xff, 0xd9, 0x80, 0x9a, 0xaa, 0x28,
            0xb4, 0x80, 0x98, 0xa7, 0x20, 0xb7, 0x97, 0x87, 0xa8, 0x66, 0x88, 0xf0, 0x79, 0x51, 0xf1, 0x90,
            0x2c, 0x87, 0x0c, 0xa7, 0x81, 0x97, 0x62, 0x93, 0xf0, 0x71, 0x71, 0x60, 0x85, 0x94, 0x01, 0x29,
            /* bank # 11 */
            0x51, 0x79, 0x90, 0xa5, 0xf1, 0x28, 0x4c, 0x6c, 0x87, 0x0c, 0x95, 0x18, 0x85, 0x78, 0xa3, 0x83,
            0x90, 0x28, 0x4c, 0x6c, 0x88, 0x6c, 0xd8, 0xf3, 0xa2, 0x82, 0x00, 0xf2, 0x10, 0xa8, 0x92, 0x19,
            0x80, 0xa2, 0xf2, 0xd9, 0x26, 0xd8, 0xf1, 0x88, 0xa8, 0x4d, 0xd9, 0x48, 0xd8, 0x96, 0xa8, 0x39,
            0x80, 0xd9, 0x3c, 0xd8, 0x95, 0x80, 0xa8, 0x39, 0xa6, 0x86, 0x98, 0xd9, 0x2c, 0xda, 0x87, 0xa7,
            0x2c, 0xd8, 0xa8, 0x89, 0x95, 0x19, 0xa9, 0x80, 0xd9, 0x38, 0xd8, 0xa8, 0x89, 0x39, 0xa9, 0x80,
            0xda, 0x3c, 0xd8, 0xa8, 0x2e, 0xa8, 0x39, 0x90, 0xd9, 0x0c, 0xd8, 0xa8, 0x95, 0x31, 0x98, 0xd9,
            0x0c, 0xd8, 0xa8, 0x09, 0xd9, 0xff, 0xd8, 0x01, 0xda, 0xff, 0xd8, 0x95, 0x39, 0xa9, 0xda, 0x26,
            0xff, 0xd8, 0x90, 0xa8, 0x0d, 0x89, 0x99, 0xa8, 0x10, 0x80, 0x98, 0x21, 0xda, 0x2e, 0xd8, 0x89,
            0x99, 0xa8, 0x31, 0x80, 0xda, 0x2e, 0xd8, 0xa8, 0x86, 0x96, 0x31, 0x80, 0xda, 0x2e, 0xd8, 0xa8,
            0x87, 0x31, 0x80, 0xda, 0x2e, 0xd8, 0xa8, 0x82, 0x92, 0xf3, 0x41, 0x80, 0xf1, 0xd9, 0x2e, 0xd8,
            0xa8, 0x82, 0xf3, 0x19, 0x80, 0xf1, 0xd9, 0x2e, 0xd8, 0x82, 0xac, 0xf3, 0xc0, 0xa2, 0x80, 0x22,
            0xf1, 0xa6, 0x2e, 0xa7, 0x2e, 0xa9, 0x22, 0x98, 0xa8, 0x29, 0xda, 0xac, 0xde, 0xff, 0xd8, 0xa2,
            0xf2, 0x2a, 0xf1, 0xa9, 0x2e, 0x82, 0x92, 0xa8, 0xf2, 0x31, 0x80, 0xa6, 0x96, 0xf1, 0xd9, 0x00,
            0xac, 0x8c, 0x9c, 0x0c, 0x30, 0xac, 0xde, 0xd0, 0xde, 0xff, 0xd8, 0x8c, 0x9c, 0xac, 0xd0, 0x10,
            0xac, 0xde, 0x80, 0x92, 0xa2, 0xf2, 0x4c, 0x82, 0xa8, 0xf1, 0xca, 0xf2, 0x35, 0xf1, 0x96, 0x88,
            0xa6, 0xd9, 0x00, 0xd8, 0xf1, 0xff
        };

        //double ax, ay, az, gx, gy, gz, mx, my, mz; // variables to hold latest sensor data values 

        private short tempCount; // Stores the real internal chip temperature in degrees Celsius
        private double temperature;

        internal double GetGres
        {
            get
            {
                switch (_Gscale)
                {
                    // Possible gyro scales (and their register bit settings) are:
                    // 250 DPS (00), 500 DPS (01), 1000 DPS (10), and 2000 DPS  (11). 
                    // Here's a bit of an algorith to calculate DPS/(ADC tick) based on that 2-bit value:
                    case Gscale.GFS_250DPS:
                        gRes = 250.0/32768.0;
                        break;
                    case Gscale.GFS_500DPS:
                        gRes = 500.0/32768.0;
                        break;
                    case Gscale.GFS_1000DPS:
                        gRes = 1000.0/32768.0;
                        break;
                    case Gscale.GFS_2000DPS:
                        gRes = 2000.0/32768.0;
                        break;
                }
                return gRes;
            }
        }

        internal double GetAres
        {
            get
            {
                switch (_Ascale)
                {
                    // Possible accelerometer scales (and their register bit settings) are:
                    // 2 Gs (00), 4 Gs (01), 8 Gs (10), and 16 Gs  (11). 
                    // Here's a bit of an algorith to calculate DPS/(ADC tick) based on that 2-bit value:
                    case Ascale.AFS_2G:
                        aRes = 2.0/32768.0;

                        break;
                    case Ascale.AFS_4G:
                        aRes = 4.0/32768.0;
                        break;
                    case Ascale.AFS_8G:
                        aRes = 8.0/32768.0;
                        break;
                    case Ascale.AFS_16G:
                        aRes = 16.0/32768.0;
                        break;
                }

                return aRes;
            }
        }

        private void _intPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
            //TODO : This does not work correctly, need to research why
        {
            //if (args.Edge == GpioPinEdge.RisingEdge)
            //    return;

            //var ad = ReadAccelData();
            //var gd = ReadGyroData();
            //var cd = ReadMagData();

            //if (ad.Length >= 2 && gd.Length >= 2)
            //    Debug.WriteLine($"ax: {ad[0]} ay: {ad[1]} az: {ad[2]}  -  gx: {gd[0]} gy: {gd[1]} gz: {gd[2]}  -  mx: {cd[0]} my: {cd[1]} mz: {cd[2]}");
        }

        private static bool WriteByte(I2CDevice device, byte subAddress, byte data)
        {
            if (!device.Write(new[] {subAddress, data}))
            {
                Debug.WriteLine($"Failed to write to {device.BaseAddress}");
                return false;
            }

            return true;
        }

        private void WriteBytes(I2CDevice device, byte[] data)
        {
            device.Write(data);
        }

        private static byte ReadByte(I2CDevice device, byte subAddress)
        {
            device.Write(new[] {subAddress});

            //var c = GetFifoCount(device);
            //if (c == 0)
            //    return 0x00;

            byte[] buffer;
            if (!device.Read(1, out buffer))
                return 0x00;

            return buffer[0];
        }

        private int GetFifoCount(I2CDevice device)
        {
            if (!device.Write(new[] {FIFO_COUNTH}))
                return 0;

            byte[] buffer;
            var r = device.Read(2, out buffer);

            if (r)
                return (buffer[0] << 8) | buffer[1]; //Get byte count    

            return 0;
        }

        private static void ReadBytes(I2CDevice device, byte subAddress, byte count, out byte[] dest)
        {
            if (!device.Write(new[] {subAddress}))
            {
                dest = new byte[1];
                return;
            }

            if (!device.Read(count, out dest))
                dest = new byte[1];
        }

        private static void ReadBytes(I2CDevice device, byte count, out byte[] dest)
        {
            var r = device.Read(count, out dest);
            if (!r)
                dest = new byte[1];
        }

        internal void StartReading()
            //TODO : This will return Accel / Gyro data properly. However the sensor fusion does not work yet.
        {
            Task.Factory.StartNew(() =>
            {
                double[] q = {1.0d, 0.0d, 0.0d, 0.0d}; // vector to hold quaternion
                //double[] eInt = { 0.0f, 0.0f, 0.0f };              // vector to hold integral error for Mahony method
                //double kp = 2.0f * 5.0f;//these are the free parameters in the Mahony filter and fusion scheme, Kp for proportional feedback,
                //double ki = 0.0f; // Ki for integral feedback
                //var sw = new Stopwatch();
                //var mRes = 10* 1229/ 4096;
                //long lastUpdateMs = 0;
                //sw.Start();

                while (true)
                {
                    //var deltat = (sw.ElapsedMilliseconds - lastUpdateMs)/1000000.0d;
                    //lastUpdateMs = sw.ElapsedMilliseconds;

                    var ad = ReadAccelData();
                    var gd = ReadGyroData();
                    //var cd = ReadMagData();
                    //var t = ReadTempData();

                    //this doesnt work either
                    ////MahonyQuaternionUpdate(ad[0], ad[1], ad[2], gd[0] * Math.PI / 180.0f, gd[1] * Math.PI / 180.0f, gd[2] * Math.PI / 180.0f, cd[0], cd[1], cd[2], ref q, ref ki, ref kp, ref eInt, ref deltat);
                    ////yaw = (Math.Atan2(2.0f * (q[1] * q[2] + q[0] * q[3]), q[0] * q[0] + q[1] * q[1] - q[2] * q[2] - q[3] * q[3])) * 180.0f / Math.PI;
                    ////pitch = (-Math.Asin(2.0f * (q[1] * q[3] - q[0] * q[2]))) * 180.0f / Math.PI;
                    ////roll = (Math.Atan2(2.0f * (q[0] * q[1] + q[2] * q[3]), q[0] * q[0] - q[1] * q[1] - q[2] * q[2] + q[3] * q[3])) * 180.0f / Math.PI;
                    ////yaw -= 40.0f; // Declination at Danville, California is 13 degrees 48 minutes and 47 seconds on 2014-04-04

                    //if (ad.Length >= 2 && gd.Length >= 2)
                    //    Debug.WriteLine($"ax: {ad[0]} ay: {ad[1]} az: {ad[2]}  -  gx: {gd[0]} gy: {gd[1]} gz: {gd[2]}");//  -  mx: {cd[0]} my: {cd[1]} mz: {cd[2]}");


                    var r = WaitMs(250);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static double deg2rad(double degrees)
        {
            return (Math.PI/180)*degrees;
        }

        /// <summary>
        ///     return is ax, ay, az
        /// </summary>
        /// <returns></returns>
        internal double[] ReadAccelData()
        {
            byte[] rawData = {0, 0, 0, 0, 0, 0}; // x/y/z accel register data stored here
            ReadBytes(_mpu9150, ACCEL_XOUT_H, 6, out rawData); // Read the six raw data registers into data array

            var axAyAz = new double[3];
            axAyAz[0] = Math.Round((((rawData[0] << 8) | rawData[1]))*GetAres, 2);
                // Turn the MSB and LSB into a signed 16-bit value
            axAyAz[1] = Math.Round((((rawData[2] << 8) | rawData[3]))*GetAres, 2);
            axAyAz[2] = Math.Round((((rawData[4] << 8) | rawData[5]))*GetAres, 2);

            return axAyAz;
        }

        /// <summary>
        ///     return is gx, gy, gz
        /// </summary>
        /// <returns></returns>
        internal double[] ReadGyroData()
        {
            byte[] rawData = {0, 0, 0, 0, 0, 0}; // x/y/z gyro register data stored here
            ReadBytes(_mpu9150, GYRO_XOUT_H, 6, out rawData);
                // Read the six raw data registers sequentially into data array

            var gxGyGz = new double[3];
            gxGyGz[0] = Math.Round(((rawData[0] << 8) | rawData[1])*GetGres, 2);
                // Turn the MSB and LSB into a signed 16-bit value
            gxGyGz[1] = Math.Round(((rawData[2] << 8) | rawData[3])*GetGres, 2);
            gxGyGz[2] = Math.Round(((rawData[4] << 8) | rawData[5])*GetGres, 2);

            return gxGyGz;
        }

        private static bool WaitMs(int ms)
        {
            var sw = new Stopwatch();
            sw.Start();
            while (sw.ElapsedMilliseconds <= ms)
            {
            }

            return true;
        }

        internal short[] ReadMagData() //TODO : When using the INT pin, this does not work at all.
        {
            byte[] rawData = {0, 0, 0, 0, 0, 0}; // x/y/z gyro register data stored here
            //WriteByte(_ak8975A, AK8975A_CNTL, 0x01); // toggle enable data read from magnetometer, no continuous read mode!
            //var w = WaitMs(20);

            WriteByte(_mpu9150, INT_PIN_CFG, 0x02);
            var w = WaitMs(10);
            WriteByte(_ak8975A, 0x0A, 0x01);
            w = WaitMs(10);

            //var rb = ReadByte(_ak8975A, AK8975A_ST1);
            //Only accept a new magnetometer data read if the data ready bit is set and
            // if there are no sensor overflow or data read errors
            //if (ReadByte(_ak8975A, AK8975A_ST1) & 0x01) > 0)) //So the return for read byte should be a byte?
            //{ // wait for magnetometer data ready bit to be set
            ReadBytes(_ak8975A, AK8975A_XOUT_L, 6, out rawData);
                // Read the six raw data registers sequentially into data array

            var magData = new short[3];
            magData[0] = (short) ((rawData[1] << 8) | rawData[0]); // Turn the MSB and LSB into a signed 16-bit value
            magData[1] = (short) ((rawData[3] << 8) | rawData[2]);
            magData[2] = (short) ((rawData[5] << 8) | rawData[4]);
            //}
            return magData;
        }

        internal double[] InitCompass()
        {
            mRes = 3;

            magbias[0] = -5; // User environmental x-axis correction in milliGauss
            magbias[1] = -95; // User environmental y-axis correction in milliGauss
            magbias[2] = -260; // User environmental z-axis correction in milliGauss

            byte[] rawData = {0, 0, 0}; // x/y/z gyro register data stored here
            WriteByte(_ak8975A, AK8975A_CNTL, 0x00); // Power down
            var w = WaitMs(10);

            WriteByte(_ak8975A, AK8975A_CNTL, 0x0F); // Enter Fuse ROM access mode
            w = WaitMs(10);

            ReadBytes(_ak8975A, AK8975A_ASAX, 3, out rawData); // Read the x-, y-, and z-axis calibration values

            var xyzSensitivityAdjValues = new double[3];
            xyzSensitivityAdjValues[0] = (rawData[0] - 128)/256.0f + 1.0f;
                // Return x-axis sensitivity adjustment values
            xyzSensitivityAdjValues[1] = (rawData[1] - 128)/256.0f + 1.0f;
            xyzSensitivityAdjValues[2] = (rawData[2] - 128)/256.0f + 1.0f;

            magCalibration = xyzSensitivityAdjValues;

            return xyzSensitivityAdjValues;
        }

        internal double ReadTempData() //This works properly
        {
            byte[] rawData = {0, 0}; // x/y/z gyro register data stored here
            ReadBytes(_mpu9150, TEMP_OUT_H, 2, out rawData);
                // Read the two raw data registers sequentially into data array 
            var t = (rawData[0] << 8) | rawData[1]; // Turn the MSB and LSB into a 16-bit value

            return ((t/340.0f + 36.53f)/10);
                //Supposed to be C, but the values do not look correct? Oh just need to shift decimal to the left. Would help if someone mentioned that somewhere.
        }

        internal bool ResetMpu9150()
        {
            // reset device
            var r = WriteByte(_mpu9150, PWR_MGMT_1, 0x80); // Write a one to bit 7 reset bit; toggle reset device
            Task.Delay(100).Wait();
            return r;
        }

        internal byte InitMpu() //Lots of stuff in here that doesnt seem to be needed.
        {
            //WriteBytes(_mpu9150, Mpu9150DmpCode);

            //Task.Delay(100);

            //var buffer = new byte[48];
            //ReadBytes(_mpu9150, 48, out buffer);

            // Initialize MPU9150 device
            // wake up device
            WriteByte(_mpu9150, PWR_MGMT_1, 0x00); // Clear sleep mode bit (6), enable all sensors 
            Task.Delay(100).Wait();
                // Delay 100 ms for PLL to get established on x-axis gyro; should check for PLL ready interrupt  

            // get stable time source
            WriteByte(_mpu9150, PWR_MGMT_1, 0x01);
                // Set clock source to be PLL with x-axis gyroscope reference, bits 2:0 = 001

            // Configure Gyro and Accelerometer
            // Disable FSYNC and set accelerometer and gyro bandwidth to 44 and 42 Hz, respectively; 
            // DLPF_CFG = bits 2:0 = 010; this sets the sample rate at 1 kHz for both
            // Maximum delay is 4.9 ms which is just over a 200 Hz maximum rate
            // WriteByte(_mpu9150, CONFIG, 0x03);

            // Set sample rate = gyroscope output rate/(1 + SMPLRT_DIV)
            //WriteByte(_mpu9150, SMPLRT_DIV, 0x04);  // Use a 200 Hz rate; the same rate set in CONFIG above

            // Set gyroscope full scale range
            // Range selects FS_SEL and AFS_SEL are 0 - 3, so 2-bit values are left-shifted into positions 4:3
            //byte c = ReadByte(_mpu9150, GYRO_CONFIG);
            //WriteByte(_mpu9150, GYRO_CONFIG, (byte)(c & ~0xE0)); // Clear self-test bits [7:5] 
            //WriteByte(_mpu9150, GYRO_CONFIG, (byte)(c & ~0x18)); // Clear AFS bits [4:3]
            //WriteByte(_mpu9150, GYRO_CONFIG, (byte)(c | (byte)_Gscale << 3)); // Set full scale range for the gyro

            //// Set accelerometer configuration
            //c = ReadByte(_mpu9150, ACCEL_CONFIG);
            //WriteByte(_mpu9150, ACCEL_CONFIG, (byte)(c & ~0xE0)); // Clear self-test bits [7:5] 
            //WriteByte(_mpu9150, ACCEL_CONFIG, (byte)(c & ~0x18)); // Clear AFS bits [4:3]
            //WriteByte(_mpu9150, ACCEL_CONFIG, (byte)(c | (byte)_Ascale << 3)); // Set full scale range for the accelerometer 

            // The accelerometer, gyro, and thermometer are set to 1 kHz sample rates, 
            // but all these rates are further reduced by a factor of 5 to 200 Hz because of the SMPLRT_DIV setting


            //WriteByte(_mpu9150, USER_CTRL, 0x40);   // Enable FIFO  
            //WriteByte(_mpu9150, FIFO_EN, 0x78);     // Enable gyro and accelerometer sensors for FIFO (max size 1024 bytes in MPU9150)
            //Task.Delay(200).Wait(); //Was 80?


            //// Configure Interrupts and Bypass Enable
            //// Set interrupt pin active high, push-pull, and clear on read of INT_STATUS, enable I2C_BYPASS_EN so additional chips 
            //// can join the I2C bus and all can be controlled by the Arduino as master
            //WriteByte(_mpu9150, INT_PIN_CFG, 0x22);
            //WriteByte(_mpu9150, INT_ENABLE, 0x01);  // Enable data ready (bit 0) interrupt

            //Task.Delay(200).Wait();

            //_ak8975A = new I2CDevice(0x0c, I2cBusSpeed.StandardMode);
            //Task.Delay(1000).Wait();
            //InitCompass();//Compass

            byte MPU6050_GCONFIG_FS_SEL_BIT = 4;
            byte MPU6050_GCONFIG_FS_SEL_LENGTH = 2;

            byte MPU6050_GYRO_FS_250 = 0x00;
            byte MPU6050_GYRO_FS_500 = 0x01;
            byte MPU6050_GYRO_FS_1000 = 0x02;
            byte MPU6050_GYRO_FS_2000 = 0x03;

            WriteBits(_mpu9150, GYRO_CONFIG, MPU6050_GCONFIG_FS_SEL_BIT, MPU6050_GCONFIG_FS_SEL_LENGTH,
                MPU6050_GYRO_FS_250);

            var w = WaitMs(10);

            byte MPU6050_RA_ACCEL_CONFIG = 0x1C;

            byte MPU6050_ACONFIG_AFS_SEL_BIT = 4;

            byte MPU6050_ACCEL_FS_2 = 0x00;

            WriteBits(_mpu9150, MPU6050_RA_ACCEL_CONFIG, MPU6050_ACONFIG_AFS_SEL_BIT, MPU6050_GCONFIG_FS_SEL_LENGTH,
                MPU6050_ACCEL_FS_2);
            w = WaitMs(10);

            byte MPU6050_PWR1_SLEEP_BIT = 6;

            WriteBit(_mpu9150, PWR_MGMT_1, MPU6050_PWR1_SLEEP_BIT, 0x00);
            w = WaitMs(10);

            return 0x00;
        }

        private bool WriteBit(I2CDevice device, byte regAddr, byte bitNum, byte data)
        {
            byte[] b;
            device.Write(new[] {regAddr});

            device.Read(1, out b);

            if (data != 0)
            {
                b[0] = (byte) (1 << bitNum);
            }
            else
            {
                b[0] = (byte) (b[0] & (byte) (~(1 << bitNum)));
            }

            return device.Write(new[] {regAddr, b[0]});
        }

        private bool WriteBits(I2CDevice device, byte regAddr, byte bitStart, byte length, byte data)
        {
            //      010 value to write
            // 76543210 bit numbers
            //    xxx   args: bitStart=4, length=3
            // 00011100 mask byte
            // 10101111 original value (sample)
            // 10100011 original & ~mask
            // 10101011 masked | value
            byte[] b;

            device.Write(new[] {regAddr});

            if (device.Read(regAddr, out b))
            {
                var mask = (byte) (((1 << length) - 1) << (bitStart - length + 1));
                data <<= (bitStart - length + 1); // shift data into correct position
                data &= mask; // zero all non-important bits in data
                b[0] &= (byte) (~(mask)); // zero all important bits in existing byte
                b[0] |= data; // combine data with existing byte
                return device.Write(new[] {regAddr, b[0]});
            }
            return false;
        }

        internal void EnableFifo()
        {
            WriteByte(_mpu9150, USER_CTRL, 0x40); // Enable FIFO  
            WriteByte(_mpu9150, FIFO_EN, 0x78);
                // Enable gyro and accelerometer sensors for FIFO (max size 1024 bytes in MPU9150)
            Task.Delay(200).Wait(); //Was 80?
            WriteByte(_mpu9150, INT_ENABLE, 0x01); // Enable data ready (bit 0) interrupt
            Task.Delay(200).Wait();
        }

        // Function which accumulates gyro and accelerometer data after device initialization. It calculates the average
        // of the at-rest readings and then loads the resulting offsets into accelerometer and gyro bias registers.
        internal void CalibrateMpu9150(out double[] dest1, out double[] dest2) //This seems to work
        {
            dest1 = new double[3];
            dest2 = new double[3];

            byte[] data = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
                // data array to hold accelerometer and gyro x, y, z, data
            ushort ii, packetCount, fifoCount;

            // reset device, reset all registers, clear gyro and accelerometer bias registers
            WriteByte(_mpu9150, PWR_MGMT_1, 0x80); // Write a one to bit 7 reset bit; toggle reset device
            Task.Delay(200).Wait();

            // get stable time source
            // Set clock source to be PLL with x-axis gyroscope reference, bits 2:0 = 001
            WriteByte(_mpu9150, PWR_MGMT_1, 0x01);
            WriteByte(_mpu9150, PWR_MGMT_2, 0x00);
            Task.Delay(200).Wait();

            // Configure device for bias calculation
            WriteByte(_mpu9150, INT_ENABLE, 0x00); // Disable all interrupts
            WriteByte(_mpu9150, FIFO_EN, 0x00); // Disable FIFO
            WriteByte(_mpu9150, PWR_MGMT_1, 0x00); // Turn on internal clock source
            WriteByte(_mpu9150, I2C_MST_CTRL, 0x00); // Disable I2C master
            WriteByte(_mpu9150, USER_CTRL, 0x00); // Disable FIFO and I2C master modes
            WriteByte(_mpu9150, USER_CTRL, 0x0C); // Reset FIFO and DMP
            Task.Delay(50).Wait();

            // Configure MPU9150 gyro and accelerometer for bias calculation
            WriteByte(_mpu9150, CONFIG, 0x01); // Set low-pass filter to 188 Hz
            WriteByte(_mpu9150, SMPLRT_DIV, 0x00); // Set sample rate to 1 kHz
            WriteByte(_mpu9150, GYRO_CONFIG, 0x00);
                // Set gyro full-scale to 250 degrees per second, maximum sensitivity
            WriteByte(_mpu9150, ACCEL_CONFIG, 0x00); // Set accelerometer full-scale to 2 g, maximum sensitivity

            ushort gyrosensitivity = 131; // = 131 LSB/degrees/sec
            ushort accelsensitivity = 16384; // = 16384 LSB/g

            // Configure FIFO to capture accelerometer and gyro data for bias calculation
            WriteByte(_mpu9150, USER_CTRL, 0x40); // Enable FIFO  
            WriteByte(_mpu9150, FIFO_EN, 0x78);
                // Enable gyro and accelerometer sensors for FIFO (max size 1024 bytes in MPU9150)
            Task.Delay(200).Wait(); //Was 80?

            // At end of sample accumulation, turn off FIFO sensor read
            WriteByte(_mpu9150, FIFO_EN, 0x00); // Disable gyro and accelerometer sensors for FIFO
            ReadBytes(_mpu9150, FIFO_COUNTH, 2, out data); // read FIFO sample count

            fifoCount = (ushort) ((data[0] << 8) | data[1]);
            packetCount = (ushort) (fifoCount/12); // How many sets of full gyro and accelerometer data for averaging

            for (ii = 0; ii < packetCount; ii++)
            {
                ushort[] accelTemp = {0, 0, 0}, gyroTemp = {0, 0, 0};

                ReadBytes(_mpu9150, FIFO_R_W, 12, out data); // read data for averaging

                accelTemp[0] = (ushort) ((data[0] << 8) | data[1]);
                    // Form signed 16-bit integer for each sample in FIFO
                accelTemp[1] = (ushort) ((data[2] << 8) | data[3]);
                accelTemp[2] = (ushort) ((data[4] << 8) | data[5]);
                gyroTemp[0] = (ushort) ((data[6] << 8) | data[7]);
                gyroTemp[1] = (ushort) ((data[8] << 8) | data[9]);
                gyroTemp[2] = (ushort) ((data[10] << 8) | data[11]);

                accelBias[0] += accelTemp[0];
                    // Sum individual signed 16-bit biases to get accumulated signed 32-bit biases
                accelBias[1] += accelTemp[1];
                accelBias[2] += accelTemp[2];
                gyroBias[0] += gyroTemp[0];
                gyroBias[1] += gyroTemp[1];
                gyroBias[2] += gyroTemp[2];
            }

            accelBias[0] /= packetCount; // Normalize sums to get average count biases
            accelBias[1] /= packetCount;
            accelBias[2] /= packetCount;
            gyroBias[0] /= packetCount;
            gyroBias[1] /= packetCount;
            gyroBias[2] /= packetCount;

            if (accelBias[2] > 0L)
                accelBias[2] -= accelsensitivity; // Remove gravity from the z-axis accelerometer bias calculation
            else
                accelBias[2] += accelsensitivity;

            //There was a "-" in front of the gyro_bias in this section?
            // Construct the gyro biases for push to the hardware gyro bias registers, which are reset to zero upon device startup
            data[0] = (byte) ((gyroBias[0]/4 >> 8) & 0xFF);
                // Divide by 4 to get 32.9 LSB per deg/s to conform to expected bias input format
            data[1] = (byte) ((gyroBias[0]/4) & 0xFF);
                // Biases are additive, so change sign on calculated average gyro biases
            data[2] = (byte) ((gyroBias[1]/4 >> 8) & 0xFF);
            data[3] = (byte) ((gyroBias[1]/4) & 0xFF);
            data[4] = (byte) ((gyroBias[2]/4 >> 8) & 0xFF);
            data[5] = (byte) ((gyroBias[2]/4) & 0xFF);

            // Push gyro biases to hardware registers
            WriteByte(_mpu9150, XG_OFFS_USRH, data[0]);
            WriteByte(_mpu9150, XG_OFFS_USRL, data[1]);
            WriteByte(_mpu9150, YG_OFFS_USRH, data[2]);
            WriteByte(_mpu9150, YG_OFFS_USRL, data[3]);
            WriteByte(_mpu9150, ZG_OFFS_USRH, data[4]);
            WriteByte(_mpu9150, ZG_OFFS_USRL, data[5]);

            dest1[0] = gyroBias[0]/(float) gyrosensitivity; // construct gyro bias in deg/s for later manual subtraction
            dest1[1] = gyroBias[1]/(float) gyrosensitivity;
            dest1[2] = gyroBias[2]/(float) gyrosensitivity;

            // Construct the accelerometer biases for push to the hardware accelerometer bias registers. These registers contain
            // factory trim values which must be added to the calculated accelerometer biases; on boot up these registers will hold
            // non-zero values. In addition, bit 0 of the lower byte must be preserved since it is used for temperature
            // compensation calculations. Accelerometer bias registers expect bias input as 2048 LSB per g, so that
            // the accelerometer biases calculated above must be divided by 8.

            uint[] accelBiasReg = {0, 0, 0}; // A place to hold the factory accelerometer trim biases
            byte[] accelBytes;

            ReadBytes(_mpu9150, XA_OFFSET_H, 2, out accelBytes); // Read factory accelerometer trim values
            accelBiasReg[0] = (ushort) ((accelBytes[0] << 8) | accelBytes[1]);

            ReadBytes(_mpu9150, YA_OFFSET_H, 2, out accelBytes);
            accelBiasReg[1] = (ushort) ((accelBytes[0] << 8) | accelBytes[1]);

            ReadBytes(_mpu9150, ZA_OFFSET_H, 2, out data);
            accelBiasReg[2] = (ushort) ((accelBytes[0] << 8) | accelBytes[1]);

            var mask = 1u;
                // Define mask for temperature compensation bit 0 of lower byte of accelerometer bias registers
            byte[] maskBit = {0, 0, 0}; // Define array to hold mask bit for each accelerometer bias axis

            for (ii = 0; ii < 3; ii++)
            {
                if ((accelBiasReg[ii] & mask) != 0) //not sure if this should be 0x01 or 0x00?
                    maskBit[ii] = 0x01; // If temperature compensation bit is set, record that fact in mask_bit
            }

            // Construct total accelerometer bias, including calculated average accelerometer bias from above
            accelBiasReg[0] -= (ushort) (accelBias[0]/8);
                // Subtract calculated averaged accelerometer bias scaled to 2048 LSB/g (16 g full scale)
            accelBiasReg[1] -= (ushort) (accelBias[1]/8);
            accelBiasReg[2] -= (ushort) (accelBias[2]/8);

            data = new byte[6];

            data[0] = (byte) ((accelBiasReg[0] >> 8) & 0xFF);
            data[1] = (byte) ((accelBiasReg[0]) & 0xFF);
            data[1] = (byte) (data[1] | maskBit[0]);
                // preserve temperature compensation bit when writing back to accelerometer bias registers
            data[2] = (byte) ((accelBiasReg[1] >> 8) & 0xFF);
            data[3] = (byte) ((accelBiasReg[1]) & 0xFF);
            data[3] = (byte) (data[3] | maskBit[1]);
                // preserve temperature compensation bit when writing back to accelerometer bias registers
            data[4] = (byte) ((accelBiasReg[2] >> 8) & 0xFF);
            data[5] = (byte) ((accelBiasReg[2]) & 0xFF);
            data[5] = (byte) (data[5] | maskBit[2]);
                // preserve temperature compensation bit when writing back to accelerometer bias registers

            // Apparently this is not working for the acceleration biases in the MPU-9250
            // Are we handling the temperature correction bit properly?
            // Push accelerometer biases to hardware registers
            WriteByte(_mpu9150, XA_OFFSET_H, data[0]);
            WriteByte(_mpu9150, XA_OFFSET_L_TC, data[1]);
            WriteByte(_mpu9150, YA_OFFSET_H, data[2]);
            WriteByte(_mpu9150, YA_OFFSET_L_TC, data[3]);
            WriteByte(_mpu9150, ZA_OFFSET_H, data[4]);
            WriteByte(_mpu9150, ZA_OFFSET_L_TC, data[5]);

            // Output scaled accelerometer biases for manual subtraction in the main program
            dest2[0] = accelBias[0]/(float) accelsensitivity;
            dest2[1] = accelBias[1]/(float) accelsensitivity;
            dest2[2] = accelBias[2]/(float) accelsensitivity;
        }


        //This works
        // Accelerometer and gyroscope self test; check calibration wrt factory settings
        // Should return percent deviation from factory trim values, +/- 14 or less deviation is a pass
        internal void Mpu9150SelfTest(out double[] destination)
        {
            destination = new double[7];
            byte[] rawData = {0, 0, 0, 0};
            byte[] selfTest = {0, 0, 0, 0, 0, 0};
            double[] factoryTrim = {0, 0, 0, 0, 0, 0};

            // Configure the accelerometer for self-test
            WriteByte(_mpu9150, ACCEL_CONFIG, 0xF0);
                // Enable self test on all three axes and set accelerometer range to +/- 8 g
            WriteByte(_mpu9150, GYRO_CONFIG, 0xE0);
                // Enable self test on all three axes and set gyro range to +/- 250 degrees/s
            Task.Delay(250); // Delay a while to let the device execute the self-test
            rawData[0] = ReadByte(_mpu9150, SELF_TEST_X); // X-axis self-test results
            rawData[1] = ReadByte(_mpu9150, SELF_TEST_Y); // Y-axis self-test results
            rawData[2] = ReadByte(_mpu9150, SELF_TEST_Z); // Z-axis self-test results
            rawData[3] = ReadByte(_mpu9150, SELF_TEST_A); // Mixed-axis self-test results

            // Extract the acceleration test results first
            selfTest[0] = (byte) ((rawData[0] >> 3) | (rawData[3] & 0x30) >> 4);
                // XA_TEST result is a five-bit unsigned integer
            selfTest[1] = (byte) ((rawData[1] >> 3) | (rawData[3] & 0x0C) >> 4);
                // YA_TEST result is a five-bit unsigned integer
            selfTest[2] = (byte) ((rawData[2] >> 3) | (rawData[3] & 0x03) >> 4);
                // ZA_TEST result is a five-bit unsigned integer
            // Extract the gyration test results first
            selfTest[3] = (byte) (rawData[0] & 0x1F); // XG_TEST result is a five-bit unsigned integer
            selfTest[4] = (byte) (rawData[1] & 0x1F); // YG_TEST result is a five-bit unsigned integer
            selfTest[5] = (byte) (rawData[2] & 0x1F); // ZG_TEST result is a five-bit unsigned integer   
            // Process results to allow final comparison with factory set values
            factoryTrim[0] = (4096.0f*0.34f)*(Math.Pow((0.92f/0.34f), ((selfTest[0] - 1.0f)/30.0f)));
                // FT[Xa] factory trim calculation
            factoryTrim[1] = (4096.0f*0.34f)*(Math.Pow((0.92f/0.34f), ((selfTest[1] - 1.0f)/30.0f)));
                // FT[Ya] factory trim calculation
            factoryTrim[2] = (4096.0f*0.34f)*(Math.Pow((0.92f/0.34f), ((selfTest[2] - 1.0f)/30.0f)));
                // FT[Za] factory trim calculation
            factoryTrim[3] = (25.0f*131.0f)*(Math.Pow(1.046f, (selfTest[3] - 1.0f))); // FT[Xg] factory trim calculation
            factoryTrim[4] = (-25.0f*131.0f)*(Math.Pow(1.046f, (selfTest[4] - 1.0f)));
                // FT[Yg] factory trim calculation
            factoryTrim[5] = (25.0f*131.0f)*(Math.Pow(1.046f, (selfTest[5] - 1.0f))); // FT[Zg] factory trim calculation

            //  Output self-test results and factory trim calculation if desired
            //  Serial.println(selfTest[0]); Serial.println(selfTest[1]); Serial.println(selfTest[2]);
            //  Serial.println(selfTest[3]); Serial.println(selfTest[4]); Serial.println(selfTest[5]);
            //  Serial.println(factoryTrim[0]); Serial.println(factoryTrim[1]); Serial.println(factoryTrim[2]);
            //  Serial.println(factoryTrim[3]); Serial.println(factoryTrim[4]); Serial.println(factoryTrim[5]);

            // Report results as a ratio of (STR - FT)/FT; the change from Factory Trim of the Self-Test Response
            // To get to percent, must multiply by 100 and subtract result from 100
            for (var i = 0; i < 6; i++)
            {
                destination[i] = (100.0f + 100.0f*(selfTest[i] - factoryTrim[i])/factoryTrim[i]);
                    // Report percent differences
            }
        }

        //TODO : Change parameters on this method to match the MahonyQuaternionUpdate method
        //Not tested.
        // Implementation of Sebastian Madgwick's "...efficient orientation filter for... inertial/magnetic sensor arrays"
        // (see http://www.x-io.co.uk/category/open-source/ for examples and more details)
        // which fuses acceleration, rotation rate, and magnetic moments to produce a quaternion-based estimate of absolute
        // device orientation -- which can be converted to yaw, pitch, and roll. Useful for stabilizing quadcopters, etc.
        // The performance of the orientation filter is at least as good as conventional Kalman-based filtering algorithms
        // but is much less computationally intensive---it can be performed on a 3.3 V Pro Mini operating at 8 MHz!
        //internal void MadgwickQuaternionUpdate(double ax, double ay, double az, double gx, double gy, double gz, double mx, double my, double mz)
        //{
        //    double q1 = q[0], q2 = q[1], q3 = q[2], q4 = q[3];   // short name local variable for readability
        //    double norm;
        //    double hx, hy, _2bx, _2bz;
        //    double s1, s2, s3, s4;
        //    double qDot1, qDot2, qDot3, qDot4;

        //    // Auxiliary variables to avoid repeated arithmetic
        //    double _2Q1Mx;
        //    double _2q1my;
        //    double _2q1mz;
        //    double _2q2mx;
        //    double _4bx;
        //    double _4bz;
        //    double _2q1 = 2.0f * q1;
        //    double _2q2 = 2.0f * q2;
        //    double _2q3 = 2.0f * q3;
        //    double _2q4 = 2.0f * q4;
        //    double _2q1q3 = 2.0f * q1 * q3;
        //    double _2q3q4 = 2.0f * q3 * q4;
        //    double q1q1 = q1 * q1;
        //    double q1q2 = q1 * q2;
        //    double q1q3 = q1 * q3;
        //    double q1q4 = q1 * q4;
        //    double q2q2 = q2 * q2;
        //    double q2q3 = q2 * q3;
        //    double q2q4 = q2 * q4;
        //    double q3q3 = q3 * q3;
        //    double q3q4 = q3 * q4;
        //    double q4q4 = q4 * q4;

        //    // Normalise accelerometer measurement
        //    norm = Math.Sqrt(ax * ax + ay * ay + az * az);

        //    if (double.IsNaN(norm))
        //        return; // handle NaN

        //    norm = 1.0f / norm;
        //    ax *= norm;
        //    ay *= norm;
        //    az *= norm;

        //    // Normalise magnetometer measurement
        //    norm = Math.Sqrt(mx * mx + my * my + mz * mz);

        //    if (double.IsNaN(norm))
        //        return; // handle NaN

        //    norm = 1.0f / norm;
        //    mx *= norm;
        //    my *= norm;
        //    mz *= norm;

        //    // Reference direction of Earth's magnetic field
        //    _2Q1Mx = 2.0f * q1 * mx;
        //    _2q1my = 2.0f * q1 * my;
        //    _2q1mz = 2.0f * q1 * mz;
        //    _2q2mx = 2.0f * q2 * mx;

        //    hx = mx * q1q1 - _2q1my * q4 + _2q1mz * q3 + mx * q2q2 + _2q2 * my * q3 + _2q2 * mz * q4 - mx * q3q3 - mx * q4q4;
        //    hy = _2Q1Mx * q4 + my * q1q1 - _2q1mz * q2 + _2q2mx * q3 - my * q2q2 + my * q3q3 + _2q3 * mz * q4 - my * q4q4;

        //    _2bx = Math.Sqrt(hx * hx + hy * hy);
        //    _2bz = -_2Q1Mx * q3 + _2q1my * q2 + mz * q1q1 + _2q2mx * q4 - mz * q2q2 + _2q3 * my * q4 - mz * q3q3 + mz * q4q4;
        //    _4bx = 2.0f * _2bx;
        //    _4bz = 2.0f * _2bz;

        //    // Gradient decent algorithm corrective step
        //    s1 = -_2q3 * (2.0f * q2q4 - _2q1q3 - ax) + _2q2 * (2.0f * q1q2 + _2q3q4 - ay) - _2bz * q3 * (_2bx * (0.5f - q3q3 - q4q4) + _2bz * (q2q4 - q1q3) - mx) + (-_2bx * q4 + _2bz * q2) * (_2bx * (q2q3 - q1q4) + _2bz * (q1q2 + q3q4) - my) + _2bx * q3 * (_2bx * (q1q3 + q2q4) + _2bz * (0.5f - q2q2 - q3q3) - mz);
        //    s2 = _2q4 * (2.0f * q2q4 - _2q1q3 - ax) + _2q1 * (2.0f * q1q2 + _2q3q4 - ay) - 4.0f * q2 * (1.0f - 2.0f * q2q2 - 2.0f * q3q3 - az) + _2bz * q4 * (_2bx * (0.5f - q3q3 - q4q4) + _2bz * (q2q4 - q1q3) - mx) + (_2bx * q3 + _2bz * q1) * (_2bx * (q2q3 - q1q4) + _2bz * (q1q2 + q3q4) - my) + (_2bx * q4 - _4bz * q2) * (_2bx * (q1q3 + q2q4) + _2bz * (0.5f - q2q2 - q3q3) - mz);
        //    s3 = -_2q1 * (2.0f * q2q4 - _2q1q3 - ax) + _2q4 * (2.0f * q1q2 + _2q3q4 - ay) - 4.0f * q3 * (1.0f - 2.0f * q2q2 - 2.0f * q3q3 - az) + (-_4bx * q3 - _2bz * q1) * (_2bx * (0.5f - q3q3 - q4q4) + _2bz * (q2q4 - q1q3) - mx) + (_2bx * q2 + _2bz * q4) * (_2bx * (q2q3 - q1q4) + _2bz * (q1q2 + q3q4) - my) + (_2bx * q1 - _4bz * q3) * (_2bx * (q1q3 + q2q4) + _2bz * (0.5f - q2q2 - q3q3) - mz);
        //    s4 = _2q2 * (2.0f * q2q4 - _2q1q3 - ax) + _2q3 * (2.0f * q1q2 + _2q3q4 - ay) + (-_4bx * q4 + _2bz * q2) * (_2bx * (0.5f - q3q3 - q4q4) + _2bz * (q2q4 - q1q3) - mx) + (-_2bx * q1 + _2bz * q3) * (_2bx * (q2q3 - q1q4) + _2bz * (q1q2 + q3q4) - my) + _2bx * q2 * (_2bx * (q1q3 + q2q4) + _2bz * (0.5f - q2q2 - q3q3) - mz);

        //    norm = Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3 + s4 * s4);    // normalise step magnitude
        //    norm = 1.0f / norm;

        //    s1 *= norm;
        //    s2 *= norm;
        //    s3 *= norm;
        //    s4 *= norm;

        //    // Compute rate of change of quaternion
        //    qDot1 = 0.5f * (-q2 * gx - q3 * gy - q4 * gz) - beta * s1;
        //    qDot2 = 0.5f * (q1 * gx + q3 * gz - q4 * gy) - beta * s2;
        //    qDot3 = 0.5f * (q1 * gy - q2 * gz + q4 * gx) - beta * s3;
        //    qDot4 = 0.5f * (q1 * gz + q2 * gy - q3 * gx) - beta * s4;

        //    // Integrate to yield quaternion
        //    q1 += qDot1 * deltat;
        //    q2 += qDot2 * deltat;
        //    q3 += qDot3 * deltat;
        //    q4 += qDot4 * deltat;

        //    norm = Math.Sqrt(q1 * q1 + q2 * q2 + q3 * q3 + q4 * q4);    // normalise quaternion
        //    norm = 1.0f / norm;

        //    q[0] = q1 * norm;
        //    q[1] = q2 * norm;
        //    q[2] = q3 * norm;
        //    q[3] = q4 * norm;

        //}

        // Similar to Madgwick scheme but uses proportional and integral filtering on the error between estimated reference vectors and
        // measured ones. 
        internal static void MahonyQuaternionUpdate(double ax, double ay, double az, double gx, double gy, double gz,
            double mx, double my, double mz, ref double[] q,
            ref double Ki, ref double Kp, ref double[] eInt, ref double deltat)
        {
            double q1 = q[0], q2 = q[1], q3 = q[2], q4 = q[3]; // short name local variable for readability
            double norm;
            double hx, hy, bx, bz;
            double vx, vy, vz, wx, wy, wz;
            double ex, ey, ez;
            double pa, pb, pc;

            // Auxiliary variables to avoid repeated arithmetic
            var q1q1 = q1*q1;
            var q1q2 = q1*q2;
            var q1q3 = q1*q3;
            var q1q4 = q1*q4;
            var q2q2 = q2*q2;
            var q2q3 = q2*q3;
            var q2q4 = q2*q4;
            var q3q3 = q3*q3;
            var q3q4 = q3*q4;
            var q4q4 = q4*q4;

            // Normalise accelerometer measurement
            norm = Math.Sqrt(ax*ax + ay*ay + az*az);

            if (double.IsNaN(norm))
                return; // handle NaN

            norm = 1.0f/norm; // use reciprocal for division
            ax *= norm;
            ay *= norm;
            az *= norm;

            // Normalise magnetometer measurement
            norm = Math.Sqrt(mx*mx + my*my + mz*mz);

            if (double.IsNaN(norm))
                return; // handle NaN

            norm = 1.0f/norm; // use reciprocal for division
            mx *= norm;
            my *= norm;
            mz *= norm;

            // Reference direction of Earth's magnetic field
            hx = 2.0f*mx*(0.5f - q3q3 - q4q4) + 2.0f*my*(q2q3 - q1q4) + 2.0f*mz*(q2q4 + q1q3);
            hy = 2.0f*mx*(q2q3 + q1q4) + 2.0f*my*(0.5f - q2q2 - q4q4) + 2.0f*mz*(q3q4 - q1q2);
            bx = Math.Sqrt((hx*hx) + (hy*hy));
            bz = 2.0f*mx*(q2q4 - q1q3) + 2.0f*my*(q3q4 + q1q2) + 2.0f*mz*(0.5f - q2q2 - q3q3);

            // Estimated direction of gravity and magnetic field
            vx = 2.0f*(q2q4 - q1q3);
            vy = 2.0f*(q1q2 + q3q4);
            vz = q1q1 - q2q2 - q3q3 + q4q4;
            wx = 2.0f*bx*(0.5f - q3q3 - q4q4) + 2.0f*bz*(q2q4 - q1q3);
            wy = 2.0f*bx*(q2q3 - q1q4) + 2.0f*bz*(q1q2 + q3q4);
            wz = 2.0f*bx*(q1q3 + q2q4) + 2.0f*bz*(0.5f - q2q2 - q3q3);

            // Error is cross product between estimated direction and measured direction of gravity
            ex = (ay*vz - az*vy) + (my*wz - mz*wy);
            ey = (az*vx - ax*vz) + (mz*wx - mx*wz);
            ez = (ax*vy - ay*vx) + (mx*wy - my*wx);

            if (Ki > 0)
            {
                eInt[0] += ex; // accumulate integral error
                eInt[1] += ey;
                eInt[2] += ez;
            }
            else
            {
                eInt[0] = 0.0f; // prevent integral wind up
                eInt[1] = 0.0f;
                eInt[2] = 0.0f;
            }

            // Apply feedback terms
            gx = gx + Kp*ex + Ki*eInt[0];
            gy = gy + Kp*ey + Ki*eInt[1];
            gz = gz + Kp*ez + Ki*eInt[2];

            // Integrate rate of change of quaternion
            pa = q2;
            pb = q3;
            pc = q4;
            q1 = q1 + (-q2*gx - q3*gy - q4*gz)*(0.5f*deltat);
            q2 = pa + (q1*gx + pb*gz - pc*gy)*(0.5f*deltat);
            q3 = pb + (q1*gy - pa*gz + pc*gx)*(0.5f*deltat);
            q4 = pc + (q1*gz + pa*gy - pb*gx)*(0.5f*deltat);

            // Normalise quaternion
            norm = Math.Sqrt(q1*q1 + q2*q2 + q3*q3 + q4*q4);
            norm = 1.0f/norm;
            q[0] = q1*norm;
            q[1] = q2*norm;
            q[2] = q3*norm;
            q[3] = q4*norm;
        }


        // Using the GY-9150 breakout board, ADO is set to 0 
        // Seven-bit device address is 110100 for ADO = 0 and 110101 for ADO = 1
        // mbed uses the eight-bit device address, so shift seven-bit addresses left by one!
        // const byte ADO 0
        // #if ADO
        // const byte MPU9150_ADDRESS 0x69<<1  // Device address when ADO = 1
        // #else
        // const byte MPU9150_ADDRESS 0x68<<1  // Device address when ADO = 0
        // #endif  

        // Set initial input parameters
        private enum Ascale
        {
            AFS_2G = 0,
            AFS_4G,
            AFS_8G,
            AFS_16G
        };

        private enum Gscale
        {
            GFS_250DPS = 0,
            GFS_500DPS,
            GFS_1000DPS,
            GFS_2000DPS
        };
    }
}