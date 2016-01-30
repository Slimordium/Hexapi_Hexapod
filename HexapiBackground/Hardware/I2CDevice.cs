using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace HexapiBackground
{
    internal sealed class I2CDevice
    {
        private I2cDevice _i2CDevice;

        public I2CDevice(byte baseAddress, I2cBusSpeed busSpeed)
        {
            BaseAddress = baseAddress;
            var settings = new I2cConnectionSettings(baseAddress) {BusSpeed = busSpeed};
            DeviceInformationCollection devices = null;

            Task.Run(async () =>
            {
                var aqs = I2cDevice.GetDeviceSelector();

                devices = await DeviceInformation.FindAllAsync(aqs);

                if (!devices.Any())
                    Debug.WriteLine($"Could not find I2C device at {baseAddress}");

                _i2CDevice = await I2cDevice.FromIdAsync(devices[0].Id, settings);

                if (_i2CDevice == null)
                    Debug.WriteLine($"Could not find I2C device at {baseAddress}");
            }).Wait();
        }

        internal byte BaseAddress { get; }

        internal bool Write(byte[] dataBytes)
        {
            try
            {
                var r = _i2CDevice.WritePartial(dataBytes);

                return r.BytesTransferred == dataBytes.Length;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        internal byte[] WriteRead(byte[] dataBytes)
        {
            try
            {
                var readBuffer = new byte[1];
                _i2CDevice.WriteRead(dataBytes, readBuffer);
                return readBuffer;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new byte[1];
            }
        }

        internal bool Read(int byteCount, out byte[] data)
        {
            data = new byte[byteCount];

            if (byteCount == 0)
            {
                data = new byte[1];
                return false;
            }

            try
            {
                var r = _i2CDevice.ReadPartial(data);

                return r.BytesTransferred == byteCount;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }
    }
}