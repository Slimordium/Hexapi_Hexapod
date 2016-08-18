using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Gps.Ntrip;
using HexapiBackground.Hardware;
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground.Gps
{
    internal sealed class Gps
    {
        private SerialDevice _gpsSerialDevice;
        internal static LatLon CurrentLatLon { get; private set; } = new LatLon();

        private readonly SparkFunSerial16X2Lcd _display;
        private readonly NtripClient _ntripClientTcp;

        private DataReader _inputStream;
        private DataWriter _outputStream;

        private Timer _statusTimer = new Timer((o) =>
        {
            if (CurrentLatLon != null)
                Debug.WriteLine(CurrentLatLon);   

        }, null, 0, 1000);

        internal Gps(SparkFunSerial16X2Lcd display, NtripClient ntripClientTcp = null)
        {
            _display = display;
            _ntripClientTcp = ntripClientTcp;
        }

        internal async Task<bool> InitializeAsync()
        {
            _gpsSerialDevice = await StartupTask.SerialDeviceHelper.GetSerialDeviceAsync("AH03F3RY", 57600, new TimeSpan(0, 0, 0, 1), new TimeSpan(0, 0, 0, 1));

            if (_gpsSerialDevice == null)
                return false;

            _outputStream = new DataWriter(_gpsSerialDevice.OutputStream);
            _inputStream = new DataReader(_gpsSerialDevice.InputStream);

            return true;
        }

        internal async Task DisplayCoordinates()
        {
            await _display.WriteAsync(CurrentLatLon.Lat.ToString(CultureInfo.InvariantCulture), 1);
            await _display.WriteAsync(CurrentLatLon.Lon.ToString(CultureInfo.InvariantCulture), 2);
        }

        #region Serial Communication

        internal async Task StartAsync()
        {
            if (_ntripClientTcp != null)
                _ntripClientTcp.NtripDataArrivedEvent += NtripClient_NtripDataArrivedEvent;

            while (true)
            {
                if (_inputStream == null)
                {
                    continue;
                }

                while (true)
                {
                    try
                    {
                        await _inputStream.LoadAsync(1).AsTask();
                        if (_inputStream.ReadString(1) == "$")
                            break;
                    }
                    catch
                    {
                        //It happens
                    }

                }

                var byteList = new List<byte> {0x00};
                while (byteList.Last() != 0x0d)
                {
                    try
                    {
                        await _inputStream.LoadAsync(1).AsTask();
                        byteList.Add(_inputStream.ReadByte());
                    }
                    catch
                    {
                        //
                    }
                }

                var sentence = Encoding.ASCII.GetString(byteList.ToArray()).Replace("\0","").Replace("\r","");

                var latLon = sentence.ParseNmea();

                if (latLon == null)
                    continue;

                if (CurrentLatLon.Quality != latLon.Quality)
                {
                    Debug.WriteLine(CurrentLatLon);
                    await _display.WriteAsync(latLon.Quality.ToString(), 2);
                }

                CurrentLatLon = latLon;
            }
        }

        private async void NtripClient_NtripDataArrivedEvent(object sender, NtripEventArgs e)
        {
            if (_gpsSerialDevice == null)
                return;

            try
            {
                _outputStream.WriteBytes(e.NtripBytes);
                await _outputStream.StoreAsync().AsTask();
            }
            catch
            {
                await _display.WriteAsync("NTRIP update failed");
            }
        }

        #endregion
    }
}