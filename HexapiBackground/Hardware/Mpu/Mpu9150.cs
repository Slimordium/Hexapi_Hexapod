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
    internal class MpuSensorValue
    {
        public double AccelerationX { get; set; }
        public double AccelerationY { get; set; }
        public double AccelerationZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
    }

    internal class MpuSensorEventArgs : EventArgs
    {
        public byte Status { get; set; }
        public double SamplePeriod { get; set; }
        public MpuSensorValue[] Values { get; set; }
    }

    [Flags]
    internal enum Mpu9150Setup : byte
    {
        Address = 0x68,
        PowerManagement1 = 0x6B,
        SampleRateDiv = 0x19,
        Config = 0x1A,
        GyroConfig = 0x1B,
        AccelConfig = 0x1C,
        FifoEnable = 0x23,
        InterruptEnable = 0x38,
        InterruptStatus = 0x3A,
        UserCtrl = 0x6A,
        FifoCount = 0x72,
        FifoReadWrite = 0x74
    }

    internal class Mpu9150New
    {
        public event EventHandler<MpuSensorEventArgs> SensorInterruptEvent;
        
        private const int SensorBytes = 12;

        I2CDevice _mpu9150 ;

        private GpioController _ioController;
        private GpioPin _interruptPin;
        private double _gyroXangle = 0;
        private double _cFangleX;

        internal async Task InitializeHardware()
        {
            try
            {
                _ioController = GpioController.GetDefault();
                _interruptPin = _ioController.OpenPin(17);
                _interruptPin.Write(GpioPinValue.Low);
                _interruptPin.SetDriveMode(GpioPinDriveMode.Input);
                _interruptPin.ValueChanged += Interrupt;
                
                _mpu9150 = new I2CDevice((byte)Mpu9150Setup.Address, I2cBusSpeed.FastMode);
                await _mpu9150.Open();

                await Task.Delay(5); // power up 

                _mpu9150.Write((byte)Mpu9150Setup.PowerManagement1, 0x80);// reset the device

                await Task.Delay(100);

                _mpu9150.Write((byte)Mpu9150Setup.PowerManagement1, 0x2 );
                _mpu9150.Write((byte)Mpu9150Setup.UserCtrl, 0x04);//reset fifo

                _mpu9150.Write((byte)Mpu9150Setup.PowerManagement1, 1 ); // clock source = gyro x
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

        private async void Interrupt(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            await Task.Delay(10);

            if (_mpu9150 == null)
                return;

            int interruptStatus = _mpu9150.ReadRegisterSingle((byte)Mpu9150Setup.InterruptStatus);

            if ((interruptStatus & 0x10) != 0)
                _mpu9150.Write((byte)Mpu9150Setup.UserCtrl, 0x44);// reset - enable fifo

            if ((interruptStatus & 0x1) == 0)
                return;

            var ea = new MpuSensorEventArgs();
            ea.Status = (byte)interruptStatus;
            ea.SamplePeriod = 0.02f;
            var l = new List<MpuSensorValue>();

            int count = _mpu9150.ReadUshort((byte)Mpu9150Setup.FifoCount);

            while (count >= SensorBytes)
            {
                _mpu9150.Write((byte)Mpu9150Setup.FifoReadWrite);

                byte[] buffer;

                _mpu9150.Read(SensorBytes, out buffer);
                count -= SensorBytes;

                var xa = (short)(buffer[0] << 8 | buffer[1]);
                var ya = (short)(buffer[2] << 8 | buffer[3]);
                var za = (short)(buffer[4] << 8 | buffer[5]);

                var xg = (short)(buffer[6] << 8 | buffer[7]);
                var yg = (short)(buffer[8] << 8 | buffer[9]);
                var zg = (short)(buffer[10] << 8 | buffer[11]);

                var sv = new MpuSensorValue
                {
                    AccelerationX = xa / 16384d,
                    AccelerationY = ya / 16384d,
                    AccelerationZ = za / 16384d,
                    GyroX = xg / 131d,
                    GyroY = yg / 131d,
                    GyroZ = zg / 131d
                }; 
                l.Add(sv);

                var gain = 0.00875;
                    
                var xRotationPerSecond = sv.GyroX * gain;//xRotationPerSecond is the rate of rotation per second.

                var loopPeriod = 0.015;//loop period - 0.02

                _gyroXangle += xRotationPerSecond * loopPeriod;

                var radToDeg = 57.29578;

                var accXangle = (Math.Atan2(sv.AccelerationY, sv.AccelerationZ) + Math.PI) * radToDeg;
                    
                var complementaryFilterConstant = 0.98;

                _cFangleX = complementaryFilterConstant * (_cFangleX + xRotationPerSecond * loopPeriod) + (1 - complementaryFilterConstant) * accXangle;

                //Debug.WriteLine("X: " + sv.GyroX + ", Y: " + sv.GyroY + ", Z: " + sv.GyroZ);
                //Debug.WriteLine("CFangleX: " + _cFangleX);
                //Debug.WriteLine("AccelX: " + sv.AccelerationX + ", AccelY: " + sv.AccelerationY + ", AccelZ: " + sv.AccelerationZ);
            }
            ea.Values = l.ToArray();

            if (SensorInterruptEvent == null) return;

            if (ea.Values.Length > 0)
            {
                SensorInterruptEvent(this, ea);
            }
        }
    }
}