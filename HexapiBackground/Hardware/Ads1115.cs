using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.I2c;

namespace HexapiBackground.Hardware
{
    internal class Ads1115
    {
        private readonly I2CDevice _ads1115;
        private const byte DefaultAddress = 0x48;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        internal Ads1115()
        {
            _ads1115 = new I2CDevice(DefaultAddress, I2cBusSpeed.StandardMode);
        }

        /// <summary>
        /// Testing detecting vibration when a foot contacts an object.
        /// </summary>
        /// <param name="channel"></param>
        internal void Start(int channel)
        {
            Task.Factory.StartNew(() =>
            {
                var avg = new double[10];
                int i = 0;

                while (true)
                {
                    var config = Enums.Ads1115.Config.CQUE_NONE |
                    Enums.Ads1115.Config.CLAT_NONLAT |
                    Enums.Ads1115.Config.CPOL_ACTVLOW |
                    Enums.Ads1115.Config.CMODE_TRAD |
                    Enums.Ads1115.Config.DR_128SPS |
                    Enums.Ads1115.Config.MODE_SINGLE;

                    var newconfig = ((uint)config) | ((uint)Enums.Ads1115.Gain.Sixteen) | ((uint)Enums.Ads1115.Config.MUX_SINGLE_0) | ((uint)Enums.Ads1115.Config.OS_SINGLE);

                    var r = _ads1115.Write(new byte[] { 0x01, (byte)(newconfig >> 8), (byte)(newconfig & 0xff) });

                    _stopwatch.Restart();

                    while (_stopwatch.ElapsedMilliseconds < 4) { }

                    r = _ads1115.Write(new byte[] { 0x00, 0x00 });

                    byte[] toRead;
                    r = _ads1115.Read(2, out toRead);

                    var val = BitConverter.ToUInt16(toRead, 0);

                    if (val == 256)
                        val = 0;

                    avg[i] = val;

                    if (i == 9)
                    {
                        i = 0;
                        var outVal = avg.Sum()/10;

                        if (outVal > 30000) //When a foot hits the floor, you get some value above this.
                            Debug.WriteLine("Value " + outVal);

                        continue;
                    }

                    i++;
                }


            }, TaskCreationOptions.LongRunning);

        }


    }
}