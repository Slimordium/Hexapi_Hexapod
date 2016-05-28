using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace HexapiBackground.Hardware
{
    internal sealed class I2CDevice
    {
        private I2cDevice _i2CDevice;
        private readonly I2cBusSpeed _busSpeed;

        public I2CDevice(byte baseAddress, I2cBusSpeed busSpeed)
        {
            BaseAddress = baseAddress;
            _busSpeed = busSpeed;
        }

        public async Task Open()
        {
            var settings = new I2cConnectionSettings(BaseAddress) { BusSpeed = _busSpeed };
            var aqs = I2cDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(aqs);

            if (!devices.Any())
                Debug.WriteLine($"Could not find I2C device at {BaseAddress}");

            _i2CDevice = await I2cDevice.FromIdAsync(devices[0].Id, settings);

            if (_i2CDevice == null)
                Debug.WriteLine($"Could not create I2C conection from device at {BaseAddress}");
        }

        internal byte BaseAddress { get; }

        internal bool Write(byte dataByte)
        {
            try
            {
                var r = _i2CDevice.WritePartial(new [] { dataByte});

                return r.BytesTransferred == 1;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

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

        internal bool Write(byte register, byte[] dataBytes)
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

        internal bool Write(byte register, byte dataByte)
        {
            try
            {
                var r = _i2CDevice.WritePartial(new [] { dataByte });

                return r.BytesTransferred == 1;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        internal byte ReadRegisterSingle(byte register)
        {
            try
            {
                var readBuffer = new byte[1];
                _i2CDevice.WriteRead(new[] { register }, readBuffer);
                return readBuffer[0];
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return 0x00;
            }
        }

        internal byte[] ReadRegister(byte register)
        {
            try
            {
                var readBuffer = new byte[1];
                _i2CDevice.WriteRead(new [] { register }, readBuffer);
                return readBuffer;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new byte[1];
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

        internal bool Read(int byteCount, byte address, out byte[] data)
        {
            _i2CDevice.Write(new[] { address });

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

        public ushort ReadUshort(byte address)
        {
            _i2CDevice.Write(new [] { address });//0x72

            byte[] buffer;
            Read(2, out buffer);
            
            return (ushort)((buffer[0] << 8) | buffer[1]);
        }

        internal bool WriteBit(I2CDevice device, byte regAddr, byte bitNum, byte data)
        {
            byte[] b;
            device.Write(new[] { regAddr });

            device.Read(1, out b);

            if (data != 0)
            {
                b[0] = (byte)(1 << bitNum);
            }
            else
            {
                b[0] = (byte)(b[0] & (byte)(~(1 << bitNum)));
            }

            return device.Write(new[] { regAddr, b[0] });
        }

        internal bool WriteBits(I2CDevice device, byte regAddr, byte bitStart, byte length, byte data)
        {
            //      010 value to write
            // 76543210 bit numbers
            //    xxx   args: bitStart=4, length=3
            // 00011100 mask byte
            // 10101111 original value (sample)
            // 10100011 original & ~mask
            // 10101011 masked | value
            byte[] b;

            device.Write(new[] { regAddr });

            if (device.Read(regAddr, out b))
            {
                var mask = (byte)(((1 << length) - 1) << (bitStart - length + 1));
                data <<= (bitStart - length + 1); // shift data into correct position
                data &= mask; // zero all non-important bits in data
                b[0] &= (byte)(~(mask)); // zero all important bits in existing byte
                b[0] |= data; // combine data with existing byte
                return device.Write(new[] { regAddr, b[0] });
            }
            return false;
        }
    }
}