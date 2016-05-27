using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.Devices.Gpio;
using System.Diagnostics;

namespace HexapiBackground.Hardware
{
    public sealed class MpuSensorValue
    {
        public float AccelerationX { get; set; }
        public float AccelerationY { get; set; }
        public float AccelerationZ { get; set; }
        public float GyroX { get; set; }
        public float GyroY { get; set; }
        public float GyroZ { get; set; }
    }

    internal class MpuSensorEventArgs : EventArgs
    {
        public byte Status { get; set; }
        public float SamplePeriod { get; set; }
        public MpuSensorValue[] Values { get; set; }
    }

    internal class Mpu9150New : IDisposable
    {
        public event EventHandler<MpuSensorEventArgs> SensorInterruptEvent;

        #region Constants

        private const byte ADDRESS = 0x68;
        private const byte PWR_MGMT_1 = 0x6B;
        private const byte SMPLRT_DIV = 0x19;
        private const byte CONFIG = 0x1A;
        private const byte GYRO_CONFIG = 0x1B;
        private const byte ACCEL_CONFIG = 0x1C;
        private const byte FIFO_EN = 0x23;
        private const byte INT_ENABLE = 0x38;
        private const byte INT_STATUS = 0x3A;
        private const byte USER_CTRL = 0x6A;
        private const byte FIFO_COUNT = 0x72;
        private const byte FIFO_R_W = 0x74;

        private const int SensorBytes = 12;

        #endregion

        private const Int32 INTERRUPT_PIN = 17;
        I2cDevice _mpu6050Device = null;
        private GpioController IoController;
        private GpioPin InterruptPin;
        private double gyroXangle = 0;
        private double CFangleX;

        #region 12c

        private byte ReadByte(byte regAddr)
        {
            byte[] buffer = new byte[1];
            buffer[0] = regAddr;
            byte[] value = new byte[1];
            _mpu6050Device.WriteRead(buffer, value);
            return value[0];
        }

        private byte[] ReadBytes(byte regAddr, int length)
        {
            byte[] values = new byte[length];
            byte[] buffer = new byte[1];
            buffer[0] = regAddr;
            _mpu6050Device.WriteRead(buffer, values);
            return values;
        }

        public ushort ReadWord(byte address)
        {
            byte[] buffer = ReadBytes(FIFO_COUNT, 2);
            return (ushort)(((int)buffer[0] << 8) | (int)buffer[1]);
        }

        void WriteByte(byte regAddr, byte data)
        {
            byte[] buffer = new byte[2];
            buffer[0] = regAddr;
            buffer[1] = data;
            _mpu6050Device.Write(buffer);
        }

        void writeBytes(byte regAddr, byte[] values)
        {
            byte[] buffer = new byte[1 + values.Length];
            buffer[0] = regAddr;
            Array.Copy(values, 0, buffer, 1, values.Length);
            _mpu6050Device.Write(buffer);
        }

        #endregion

        public async void InitHardware()
        {
            try
            {
                IoController = GpioController.GetDefault();
                InterruptPin = IoController.OpenPin(INTERRUPT_PIN);
                //InterruptPin.DebounceTimeout = new TimeSpan(0,0,0,0,10);
                InterruptPin.Write(GpioPinValue.Low);
                InterruptPin.SetDriveMode(GpioPinDriveMode.Input);
                InterruptPin.ValueChanged += Interrupt;

                string aqs = I2cDevice.GetDeviceSelector();
                DeviceInformationCollection collection = await DeviceInformation.FindAllAsync(aqs);

                I2cConnectionSettings settings = new I2cConnectionSettings(ADDRESS);
                settings.BusSpeed = I2cBusSpeed.FastMode; // 400kHz clock
                settings.SharingMode = I2cSharingMode.Exclusive;
                _mpu6050Device = await I2cDevice.FromIdAsync(collection[0].Id, settings);

                await Task.Delay(3); // wait power up sequence

                WriteByte(PWR_MGMT_1, 0x80);// reset the device
                await Task.Delay(100);
                WriteByte(PWR_MGMT_1, 0x2);
                WriteByte(USER_CTRL, 0x04); //reset fifo

                WriteByte(PWR_MGMT_1, 1); // clock source = gyro x
                WriteByte(GYRO_CONFIG, 0); // +/- 250 degrees sec
                WriteByte(ACCEL_CONFIG, 0); // +/- 2g

                WriteByte(CONFIG, 1); // 184 Hz, 2ms delay
                WriteByte(SMPLRT_DIV, 19);  // set rate 50Hz
                WriteByte(FIFO_EN, 0x78); // enable accel and gyro to read into fifo
                WriteByte(USER_CTRL, 0x40); // reset and enable fifo
                WriteByte(INT_ENABLE, 0x1);
            }
            catch (Exception ex)
            {
                string error = ex.ToString();
            }
        }

        private async void Interrupt(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            await Task.Delay(10);

            if (_mpu6050Device != null)
            {
                int interrupt_status = ReadByte(INT_STATUS);
                if ((interrupt_status & 0x10) != 0)
                {
                    WriteByte(USER_CTRL, 0x44); // reset and enable fifo
                }
                if ((interrupt_status & 0x1) != 0)
                {
                    MpuSensorEventArgs ea = new MpuSensorEventArgs();
                    ea.Status = (byte)interrupt_status;
                    ea.SamplePeriod = 0.02f;
                    List<MpuSensorValue> l = new List<MpuSensorValue>();

                    int count = ReadWord(FIFO_COUNT);

                    while (count >= SensorBytes)
                    {
                        byte[] data = ReadBytes(FIFO_R_W, (byte)SensorBytes);
                        count -= SensorBytes;

                        short xa = (short)((int)data[0] << 8 | (int)data[1]);
                        short ya = (short)((int)data[2] << 8 | (int)data[3]);
                        short za = (short)((int)data[4] << 8 | (int)data[5]);

                        short xg = (short)((int)data[6] << 8 | (int)data[7]);
                        short yg = (short)((int)data[8] << 8 | (int)data[9]);
                        short zg = (short)((int)data[10] << 8 | (int)data[11]);

                        MpuSensorValue sv = new MpuSensorValue();
                        sv.AccelerationX = (float)xa / (float)16384;
                        sv.AccelerationY = (float)ya / (float)16384;
                        sv.AccelerationZ = (float)za / (float)16384;
                        sv.GyroX = (float)xg / (float)131;
                        sv.GyroY = (float)yg / (float)131;
                        sv.GyroZ = (float)zg / (float)131;
                        l.Add(sv);

                        double G_GAIN = 0.00875;
                        //xRotationPerSecond is the rate of rotation per second.
                        double xRotationPerSecond = (double)sv.GyroX * G_GAIN;
                        //DT is the loop period
                        double DT = 0.02;

                        gyroXangle += xRotationPerSecond * DT;

                        double M_PI = 3.14159265358979323846;
                        double RAD_TO_DEG = 57.29578;

                        double AccXangle = (Math.Atan2(sv.AccelerationY, sv.AccelerationZ) + M_PI) * RAD_TO_DEG;
                        // Complementary filter constant
                        double AA = 0.98;
                        CFangleX = AA * (CFangleX + xRotationPerSecond * DT) + (1 - AA) * AccXangle;

                        //Debug.WriteLine("X: " + sv.GyroX + ", Y: " + sv.GyroY + ", Z: " + sv.GyroZ);
                        Debug.WriteLine("CFangleX: " + CFangleX);
                        //Debug.WriteLine("AccelX: " + sv.AccelerationX + ", AccelY: " + sv.AccelerationY + ", AccelZ: " + sv.AccelerationZ);
                    }
                    ea.Values = l.ToArray();

                    if (SensorInterruptEvent != null)
                    {
                        if (ea.Values.Length > 0)
                        {
                            SensorInterruptEvent(this, ea);
                        }
                    }
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                InterruptPin.Dispose();
                if (_mpu6050Device != null)
                {
                    _mpu6050Device.Dispose();
                    _mpu6050Device = null;
                }
                disposedValue = true;

            }
        }

        ~Mpu9150New()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}