using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace HexapiBackground.Hardware{
    internal class Mpr121{
        internal const byte Baseline0 = 0x1E;

        internal const byte FilterData_0L = 0x04;
        internal const byte FilterData_0H = 0x05;
        internal const byte Baseline_0 = 0x1E;

        internal const byte MHD_R = 0x2B;
        internal const byte NHD_R = 0x2C;
        internal const byte NCL_R = 0x2D;
        internal const byte FDL_R = 0x2E;

        internal const byte MHD_F = 0x2F;
        internal const byte NHD_F = 0x30;
        internal const byte NCL_F = 0x31;
        internal const byte FDL_F = 0x32;

        internal const byte NHD_T = 0x33;
        internal const byte NCL_T = 0x34;
        internal const byte FDL_T = 0x35;


        internal const byte ElectrodeTouch = 0x41;
        internal const byte ElectrodeRelease = 0x42;

        internal const byte Debounce = 0x5B;

        internal const byte Config1 = 0x5C;
        internal const byte Config2 = 0x5D;

        internal const byte ECR = 0x5E;

        internal const byte ChargeCurrent0 = 0x5F;
        internal const byte ChargeTime1 = 0x6C;

        internal const byte GPIO_CTRL0 = 0x73;
        internal const byte GPIO_CTRL1 = 0x74;
        internal const byte GPIO_DATA = 0x75;
        internal const byte GPIO_DIR = 0x76;
        internal const byte GPIO_EN = 0x77;
        internal const byte GPIO_SET = 0x78;
        internal const byte GPIO_CLEAR = 0x79;
        internal const byte GPIO_TOGGLE = 0x7A;

        internal const byte AutoConfig0 = 0x7B;
        internal const byte AutoConfig1 = 0x7C;

        internal const byte UpLimit = 0x7D;
        internal const byte LowLimit = 0x7E;
        internal const byte TargetLimit = 0x7F;

        internal const byte SoftReset = 0x80; 

        private GpioController _gpioController;
        private GpioPin _gpioPin;
        private readonly I2CDevice _i2CDevice;

        internal Mpr121()
        {
            _i2CDevice = new I2CDevice(0x5A, I2cBusSpeed.StandardMode);

            Task.Delay(50).Wait();
        }

        internal void Start()
        {
            _gpioController = GpioController.GetDefault();
            _gpioPin = _gpioController.OpenPin(17);
            _gpioPin.SetDriveMode(GpioPinDriveMode.InputPullUp);

            ConfigureMpr121();
        }

        private void ConfigureMpr121()
        {
            _i2CDevice.Write(new byte[] {SoftReset, 0x63});
            Task.Delay(10).Wait();

            _i2CDevice.Write(new byte[] {ECR, 0x00});

            SetThresholds(4, 3);

            _i2CDevice.Write(new byte[] {MHD_R, 0x01});
            _i2CDevice.Write(new byte[] {NHD_R, 0x01});
            _i2CDevice.Write(new byte[] {NCL_R, 0x0E});
            _i2CDevice.Write(new byte[] {FDL_R, 0x00});

            _i2CDevice.Write(new byte[] {MHD_F, 0x01});
            _i2CDevice.Write(new byte[] {NHD_F, 0x05});
            _i2CDevice.Write(new byte[] {NCL_F, 0x01});
            _i2CDevice.Write(new byte[] {FDL_F, 0x00});

            _i2CDevice.Write(new byte[] {NHD_T, 0x00});
            _i2CDevice.Write(new byte[] {NCL_T, 0x00});
            _i2CDevice.Write(new byte[] {FDL_T, 0x00});

            _i2CDevice.Write(new byte[] {Debounce, 0x00});

            _i2CDevice.Write(new byte[] {Config1, 0x10});
            _i2CDevice.Write(new byte[] {Config2, 0x20});

            _i2CDevice.Write(new byte[] {ECR, 0x03});

            //_i2CDevice.Write(new byte[] { Mpr121.GPIO_EN, 0xff });
            //_i2CDevice.Write(new byte[] { Mpr121.GPIO_DIR, 0xff });
            //_i2CDevice.Write(new byte[] { Mpr121.GPIO_CTRL0, 0xff });
            //_i2CDevice.Write(new byte[] { Mpr121.GPIO_CTRL1, 0xff });
            //_i2CDevice.Write(new byte[] { Mpr121.GPIO_CLEAR, 0xff });

            _gpioPin.ValueChanged += Pin_ValueChanged;
        }

        private void SetThresholds(uint touch, uint release)
        {
            for (var i = 0; i < 3; i++) //i = number of electrodes
            {
                _i2CDevice.Write(new[] {(byte) (ElectrodeTouch + 2*i), (byte) touch});
                _i2CDevice.Write(new[] {(byte) (ElectrodeRelease + 2*i), (byte) release});
            }
        }

        private void Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            byte[] r;

            if (!_i2CDevice.Read(2, out r))
                return;

            var touched = BitConverter.ToUInt16(r, 0);

            for (var i = 0; i <= 15; i++)
            {
                if ((touched & (1 << i)) != 0)
                    Debug.WriteLine("Touched " + i);
            }
        }
    }
}