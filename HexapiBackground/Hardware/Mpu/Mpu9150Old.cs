using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace HexapiBackground.Hardware
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
        private readonly I2CDevice _ak8975A;// = new I2CDevice(0x0c, I2cBusSpeed.FastMode);

        private readonly Ascale _Ascale = Ascale.AFS_2G; // AFS_2G, AFS_4G, AFS_8G, AFS_16G
        private readonly Gscale _Gscale = Gscale.GFS_250DPS; // GFS_250DPS, GFS_500DPS, GFS_1000DPS, GFS_2000DPS
        private GpioPin _intPin;

        private I2CDevice _mpu9150;

        private readonly ushort[] accelBias = {0, 0, 0}; // Bias corrections for gyro and accelerometer
        private double aRes, gRes, mRes; // scale resolutions per LSB for the sensors

        private int delt_t = 0; // used to control display output rate

        private readonly ushort[] gyroBias = {0, 0, 0};

        // Pin definitions
        private int intPin = 12; // These can be changed, 2 and 3 are the Arduinos ext int pins

        private double[] magCalibration = {0, 0, 0}; // Factory mag calibration and mag bias
        private readonly double[] magbias = {0, 0, 0}; // Factory mag calibration and mag bias

 

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

        /// <summary>
        ///     return is ax, ay, az
        /// </summary>
        /// <returns></returns>
        internal double[] ReadAccelData()
        {
            byte[] rawData = {0, 0, 0, 0, 0, 0}; // x/y/z accel register data stored here
            _mpu9150.Read(6, ACCEL_XOUT_H, out rawData); // Read the six raw data registers into data array

            if (rawData.Length == 1)
                return new double[2];

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
            _mpu9150.Read(6, GYRO_XOUT_H, out rawData);
            // Read the six raw data registers sequentially into data array

            if (rawData.Length == 1)
                return new double[2];

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

            _mpu9150.Write(INT_PIN_CFG, 0x02);
            var w = WaitMs(10);

            _ak8975A.Write(0x0A, 0x01);
            w = WaitMs(10);

            //var rb = ReadByte(_ak8975A, AK8975A_ST1);
            //Only accept a new magnetometer data read if the data ready bit is set and
            // if there are no sensor overflow or data read errors
            //if (ReadByte(_ak8975A, AK8975A_ST1) & 0x01) > 0)) //So the return for read byte should be a byte?
            //{ // wait for magnetometer data ready bit to be set
            _ak8975A.Read(6, AK8975A_XOUT_L, out rawData);
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
            _ak8975A.Write(AK8975A_CNTL, 0x00); // Power down
            var w = WaitMs(10);

            _ak8975A.Write(AK8975A_CNTL, 0x0F); // Enter Fuse ROM access mode
            w = WaitMs(10);

            _ak8975A.Read(3, AK8975A_ASAX, out rawData); // Read the x-, y-, and z-axis calibration values

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
            _mpu9150.Read(2, TEMP_OUT_H, out rawData);
                // Read the two raw data registers sequentially into data array 
            var t = (rawData[0] << 8) | rawData[1]; // Turn the MSB and LSB into a 16-bit value

            return ((t/340.0f + 36.53f)/10);
                //Supposed to be C, but the values do not look correct? Oh just need to shift decimal to the left. Would help if someone mentioned that somewhere.
        }

        internal bool ResetMpu9150()
        {
            // reset device
            var r = _mpu9150.Write(PWR_MGMT_1, 0x80); // Write a one to bit 7 reset bit; toggle reset device
            Task.Delay(100).Wait();
            return r;
        }

        private GpioController _ioController;
        private GpioPin _interruptPin;

        internal byte InitMpu() //Lots of stuff in here that doesnt seem to be needed.
        {
            _ioController = GpioController.GetDefault();
            _interruptPin = _ioController.OpenPin(17);
            _interruptPin.Write(GpioPinValue.Low);
            _interruptPin.SetDriveMode(GpioPinDriveMode.Input);
            _interruptPin.ValueChanged += _interruptPin_ValueChanged;

            // Initialize MPU9150 device
            // wake up device
            _mpu9150.Write(PWR_MGMT_1, 0x00); // Clear sleep mode bit (6), enable all sensors 
            Task.Delay(100).Wait();
            // Delay 100 ms for PLL to get established on x-axis gyro; should check for PLL ready interrupt  

            // get stable time source
            _mpu9150.Write(PWR_MGMT_1, 0x01);// Set clock source to be PLL with x-axis gyroscope reference, bits 2:0 = 001

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

            _mpu9150.WriteBits(_mpu9150, GYRO_CONFIG, MPU6050_GCONFIG_FS_SEL_BIT, MPU6050_GCONFIG_FS_SEL_LENGTH,
                MPU6050_GYRO_FS_250);

            var w = WaitMs(10);

            byte MPU6050_RA_ACCEL_CONFIG = 0x1C;

            byte MPU6050_ACONFIG_AFS_SEL_BIT = 4;

            byte MPU6050_ACCEL_FS_2 = 0x00;

            _mpu9150.WriteBits(_mpu9150, MPU6050_RA_ACCEL_CONFIG, MPU6050_ACONFIG_AFS_SEL_BIT, MPU6050_GCONFIG_FS_SEL_LENGTH,
                MPU6050_ACCEL_FS_2);
            w = WaitMs(10);

            byte MPU6050_PWR1_SLEEP_BIT = 6;

            _mpu9150.WriteBit(_mpu9150, PWR_MGMT_1, MPU6050_PWR1_SLEEP_BIT, 0x00);
            w = WaitMs(10);

            return 0x00;
        }

        internal async Task InitHardware()
        {
            try
            {
                _ioController = GpioController.GetDefault();
                _interruptPin = _ioController.OpenPin(17);
                _interruptPin.Write(GpioPinValue.Low);
                _interruptPin.SetDriveMode(GpioPinDriveMode.Input);
                _interruptPin.ValueChanged += _interruptPin_ValueChanged;

                _mpu9150 = new I2CDevice((byte)Mpu9150Setup.Address, I2cBusSpeed.FastMode);
                await _mpu9150.Open();

                await Task.Delay(100); // power up 

                _mpu9150.Write((byte)Mpu9150Setup.PowerManagement1, 0x80);// reset the device

                await Task.Delay(100);

                _mpu9150.Write((byte)Mpu9150Setup.PowerManagement1, 0x2);
                _mpu9150.Write((byte)Mpu9150Setup.UserCtrl, 0x04);//reset fifo

                _mpu9150.Write((byte)Mpu9150Setup.PowerManagement1, 1); // clock source = gyro x
                _mpu9150.Write((byte)Mpu9150Setup.GyroConfig, 0); // +/- 250 degrees sec, max sensitivity
                _mpu9150.Write((byte)Mpu9150Setup.AccelConfig, 0); // +/- 2g, max sensitivity

                _mpu9150.Write((byte)Mpu9150Setup.Config, 1);// 184 Hz, 2ms delay
                _mpu9150.Write((byte)Mpu9150Setup.SampleRateDiv, 19); // set rate 50Hz
                _mpu9150.Write((byte)Mpu9150Setup.FifoEnable, 0x78); // enable accel and gyro to read into fifo
                _mpu9150.Write((byte)Mpu9150Setup.UserCtrl, 0x40); // reset and enable fifo
                _mpu9150.Write((byte)Mpu9150Setup.InterruptEnable, 0x1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async void _interruptPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {

            var ad = ReadAccelData();

            //Debug.WriteLine($"Accel {ad[0]} {ad[1]} {ad[2]}");

            var gd = ReadGyroData();

            //Debug.WriteLine($"Gyrol {gd[0]} {gd[1]} {gd[2]}");



            var sv = new MpuSensorValue
            {
                AccelerationX = ad[0] / 16384d,
                AccelerationY = ad[1] / 16384d,
                AccelerationZ = ad[2] / 16384d,
                GyroX = gd[0] / 131d,
                GyroY = gd[1] / 131d,
                GyroZ = gd[2] / 131d
            };



            var gain = 0.00875;

            var xRotationPerSecond = sv.GyroX * gain;//xRotationPerSecond is the rate of rotation per second.

            var loopPeriod = 0.025;//loop period - 0.02

            _gyroXangle += xRotationPerSecond * loopPeriod;

            //var M_PI = 3.14159265358979323846;
            var radToDeg = 57.29578;

            var accXangle = (Math.Atan2(sv.AccelerationY, sv.AccelerationZ) + 3.14159265358979323846) * radToDeg;

            var complementaryFilterConstant = 0.98;

            _cFangleX = complementaryFilterConstant * (_cFangleX + xRotationPerSecond * loopPeriod) + (1 - complementaryFilterConstant) * accXangle;

            //Debug.WriteLine("X: " + sv.GyroX + ", Y: " + sv.GyroY + ", Z: " + sv.GyroZ);
            Debug.WriteLine("CFangleX: " + _cFangleX);
            //Debug.WriteLine("AccelX: " + sv.AccelerationX + ", AccelY: " + sv.AccelerationY + ", AccelZ: " + sv.AccelerationZ);







            Debug.WriteLine("---------------------------");

            await Task.Delay(5);
        }

        private double _gyroXangle = 0;
        private double _cFangleX;

        

        internal void EnableFifo()
        {
            _mpu9150.Write(USER_CTRL, 0x40); // Enable FIFO  
            _mpu9150.Write(FIFO_EN, 0x78);
                // Enable gyro and accelerometer sensors for FIFO (max size 1024 bytes in MPU9150)
            Task.Delay(200).Wait(); //Was 80?
            _mpu9150.Write(INT_ENABLE, 0x01); // Enable data ready (bit 0) interrupt
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
            _mpu9150.Write(PWR_MGMT_1, 0x80); // Write a one to bit 7 reset bit; toggle reset device
            Task.Delay(200).Wait();

            // get stable time source
            // Set clock source to be PLL with x-axis gyroscope reference, bits 2:0 = 001
            _mpu9150.Write(PWR_MGMT_1, 0x01);
            _mpu9150.Write(PWR_MGMT_2, 0x00);
            Task.Delay(200).Wait();

            // Configure device for bias calculation
            _mpu9150.Write(INT_ENABLE, 0x00); // Disable all interrupts
            _mpu9150.Write(FIFO_EN, 0x00); // Disable FIFO
            _mpu9150.Write(PWR_MGMT_1, 0x00); // Turn on internal clock source
            _mpu9150.Write(I2C_MST_CTRL, 0x00); // Disable I2C master
            _mpu9150.Write(USER_CTRL, 0x00); // Disable FIFO and I2C master modes
            _mpu9150.Write(USER_CTRL, 0x0C); // Reset FIFO and DMP
            Task.Delay(50).Wait();

            // Configure MPU9150 gyro and accelerometer for bias calculation
            _mpu9150.Write(CONFIG, 0x01); // Set low-pass filter to 188 Hz
            _mpu9150.Write(SMPLRT_DIV, 0x00); // Set sample rate to 1 kHz
            _mpu9150.Write(GYRO_CONFIG, 0x00);
            // Set gyro full-scale to 250 degrees per second, maximum sensitivity
            _mpu9150.Write(ACCEL_CONFIG, 0x00); // Set accelerometer full-scale to 2 g, maximum sensitivity

            ushort gyrosensitivity = 131; // = 131 LSB/degrees/sec
            ushort accelsensitivity = 16384; // = 16384 LSB/g

            // Configure FIFO to capture accelerometer and gyro data for bias calculation
            _mpu9150.Write(USER_CTRL, 0x40); // Enable FIFO  
            _mpu9150.Write(FIFO_EN, 0x78);
                // Enable gyro and accelerometer sensors for FIFO (max size 1024 bytes in MPU9150)
            Task.Delay(200).Wait(); //Was 80?

            // At end of sample accumulation, turn off FIFO sensor read
            _mpu9150.Write(FIFO_EN, 0x00); // Disable gyro and accelerometer sensors for FIFO
            _mpu9150.Read(2, FIFO_COUNTH, out data); // read FIFO sample count

            fifoCount = (ushort) ((data[0] << 8) | data[1]);
            packetCount = (ushort) (fifoCount/12); // How many sets of full gyro and accelerometer data for averaging

            for (ii = 0; ii < packetCount; ii++)
            {
                ushort[] accelTemp = {0, 0, 0}, gyroTemp = {0, 0, 0};

                _mpu9150.Read(12, FIFO_R_W, out data); // read data for averaging

                accelTemp[0] = (ushort) ((data[0] << 8) | data[1]);
                    // Form unsigned 16-bit integer for each sample in FIFO
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
            _mpu9150.Write(XG_OFFS_USRH, data[0]);
            _mpu9150.Write(XG_OFFS_USRL, data[1]);
            _mpu9150.Write(YG_OFFS_USRH, data[2]);
            _mpu9150.Write(YG_OFFS_USRL, data[3]);
            _mpu9150.Write(ZG_OFFS_USRH, data[4]);
            _mpu9150.Write(ZG_OFFS_USRL, data[5]);

            dest1[0] = gyroBias[0]/(float) gyrosensitivity; // construct gyro bias in deg/s for later manual subtraction
            dest1[1] = gyroBias[1]/(float) gyrosensitivity;
            dest1[2] = gyroBias[2]/(float) gyrosensitivity;

            // Construct the accelerometer biases for push to the hardware accelerometer bias registers. These registers contain
            // factory trim values which must be added to the calculated accelerometer biases; on boot up these registers will hold
            // non-zero values. In addition, bit 0 of the lower byte must be preserved since it is used for temperature
            // compensation calculations. Accelerometer bias registers expect bias input as 2048 LSB per g, so that
            // the accelerometer biases calculated above must be divided by 8.

            uint[] accelBiasReg = {0, 0, 0}; // A place to hold the factory accelerometer trim biases

            // Read factory accelerometer trim values
            accelBiasReg[0] = _mpu9150.ReadUshort(XA_OFFSET_H);
            accelBiasReg[1] = _mpu9150.ReadUshort(YA_OFFSET_H);
            accelBiasReg[2] = _mpu9150.ReadUshort(ZA_OFFSET_H);

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
            _mpu9150.Write(XA_OFFSET_H, data[0]);
            _mpu9150.Write(XA_OFFSET_L_TC, data[1]);
            _mpu9150.Write(YA_OFFSET_H, data[2]);
            _mpu9150.Write(YA_OFFSET_L_TC, data[3]);
            _mpu9150.Write(ZA_OFFSET_H, data[4]);
            _mpu9150.Write(ZA_OFFSET_L_TC, data[5]);

            // Output scaled accelerometer biases for manual subtraction in the main program
            dest2[0] = accelBias[0]/(float) accelsensitivity;
            dest2[1] = accelBias[1]/(float) accelsensitivity;
            dest2[2] = accelBias[2]/(float) accelsensitivity;
        }


        //This works
        // Accelerometer and gyroscope self test; check calibration wrt factory settings
        // Should return percent deviation from factory trim values, +/- 14 or less deviation is a pass
        internal bool Mpu9150SelfTest(out double[] destination)
        {
            destination = new double[7];
            byte[] rawData = {0, 0, 0, 0};
            byte[] selfTest = {0, 0, 0, 0, 0, 0};
            double[] factoryTrim = {0, 0, 0, 0, 0, 0};

            // Configure the accelerometer for self-test
            _mpu9150.Write(ACCEL_CONFIG, 0xF0);
            // Enable self test on all three axes and set accelerometer range to +/- 8 g
            _mpu9150.Write(GYRO_CONFIG, 0xE0);
                // Enable self test on all three axes and set gyro range to +/- 250 degrees/s
            Task.Delay(250); // Delay a while to let the device execute the self-test
            rawData[0] = _mpu9150.ReadRegisterSingle(SELF_TEST_X); // X-axis self-test results
            rawData[1] = _mpu9150.ReadRegisterSingle(SELF_TEST_Y); // Y-axis self-test results
            rawData[2] = _mpu9150.ReadRegisterSingle(SELF_TEST_Z); // Z-axis self-test results
            rawData[3] = _mpu9150.ReadRegisterSingle(SELF_TEST_A); // Mixed-axis self-test results

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

            if (destination[0] < 1.0f && destination[1] < 1.0f && destination[2] < 1.0f && destination[3] < 1.0f && destination[4] < 1.0f && destination[5] < 1.0f)
            {
                Debug.WriteLine("MPU Self test passed.");
                return true;
            }

            return false;
        }


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