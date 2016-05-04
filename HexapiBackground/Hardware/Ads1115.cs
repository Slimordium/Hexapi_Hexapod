using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.I2c;

namespace HexapiBackground.Hardware{
    internal class Ads1115
    {
        private I2CDevice _ads1115;

        private const byte ConfigMuxOffset = 12;
        private const ushort ConfigOsSingle = 0x8000;

        private const byte DefaultAddress = 0x48;
        private const byte PointerConfig = 0x01;
        private const byte PointerConversion = 0x00;
        private const byte PointerHighThreshold = 0x03;
        private const byte PointerLowThreshold = 0x02;

        private const ushort ConfigModeContinuous = 0x0000;
        private const ushort ConfigModeSingle = 0x0100;

        private const ushort ConfigCompWindow = 0x0010;
        private const ushort ConfigCompActiveHigh = 0x0008;
        private const ushort ConfigCompLatching = 0x0004;

        private const ushort ConfigCompQueDisable = 0x0003;

        internal Ads1115()
        {
            _ads1115 = new I2CDevice(DefaultAddress, I2cBusSpeed.StandardMode);

            //var config = ConfigCompQueDisable
        }

        Stopwatch _stopwatch = new Stopwatch();

        internal void Start(int channel)
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    var config = Enums.Ads1115.Config.CQUE_NONE |
                    Enums.Ads1115.Config.CLAT_NONLAT |
                    Enums.Ads1115.Config.CPOL_ACTVLOW |
                    Enums.Ads1115.Config.CMODE_TRAD |
                    Enums.Ads1115.Config.DR_250SPS |
                    Enums.Ads1115.Config.MODE_SINGLE;

                    var newconfig = ((uint)config) | ((uint)Enums.Ads1115.Gain.Two) | ((uint)Enums.Ads1115.Config.MUX_SINGLE_0) | ((uint)Enums.Ads1115.Config.OS_SINGLE);

                    var r = _ads1115.Write(new byte[] { 0x01, (byte)(newconfig >> 8), (byte)(newconfig & 0xff) });

                    _stopwatch.Restart();

                    while (_stopwatch.ElapsedMilliseconds < 50) { }

                    r = _ads1115.Write(new byte[] { 0x00, 0x00 });

                    byte[] toRead;
                    r = _ads1115.Read(4, out toRead);

                    var val = BitConverter.ToInt32(toRead, 0);

                    if (val != 0 && val != 1280 )
                        Debug.WriteLine("Change " + BitConverter.ToUInt16(toRead, 0));
                }


            }, TaskCreationOptions.LongRunning);

        }


    }
}