using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage;
using Windows.Storage.Streams;

namespace HexapiBackground
{
    //Tested as 100% working with the Adafruit Ultimate GPS
    //Completely custom code for parsing the basic GPS data that we need for navigation. Calculates drift/accuracy over time. Seems to work fairly well
    internal class LatLon
    {
        internal LatLon()
        {
            DateTime = DateTime.Now;
            Quality = GpsFixQuality.NoFix;
        }

        internal double Lat { get; set; }
        internal double Lon { get; set; }
        internal GpsFixQuality Quality { get; set; }
        internal double Heading { get; set; }
        internal float Altitude { get; set; }
        internal double FeetPerSecond { get; set; }
        internal DateTime DateTime { get; set; }
        internal double DistanceToAvgCenter { get; set; }
        internal double CorrectedDistanceToCenter { get; set; }
    }

    internal enum GpsFixQuality
    {
        NoFix,
        StandardGps,
        DiffGps,
    }

    internal sealed class Gps
    {
        private readonly Stopwatch _sw = new Stopwatch();
        internal DateTime Time { get; set; }
        internal double Lat { get; set; }
        internal double Lon { get; set; }
        internal GpsFixQuality Quality { get; set; }
        internal double Heading { get; set; }
        internal float Altitude { get; set; }
        internal double FeetPerSecond { get; set; }

        private SerialDevice _serialPort;
        private DataWriter _dataWriter;
        private DataReader _dataReader;

        #region GPS Configuration constants
        private const string PmtkSetBaud115200 = "$PMTK251,115200*1F";
        private const string PmtkSetBaud57600 = "$PMTK251,57600*2C";
        private const string PmtkSetBaud9600 = "$PMTK251,9600*17";

        private const string PmtkSetNmeaOutputRmconly = "$PMTK314,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0*29";
        private const string PmtkSetNmeaOutputRmcgga = "$PMTK314,0,1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0*28";
        private const string PmtkSetNmeaOutputAlldata = "$PMTK314,1,1,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0*28";
        
        private const string PmtkSetNmeaUpdate5Hz = "$PMTK220,200*2C";
        private const string PmtkSetNmeaUpdate10Hz = "$PMTK220,100*2F";
        private const string PmtkApiSetFixCtl5Hz = "$PMTK300,200,0,0,0,0*2F";
        private const string PmtkApiSetFixCtl10Hz = "$PMTK300,100,0,0,0,0*2C";

        private const string EnableSbas = "$PMTK313,1*2E";
        private const string SbasModeWaas = "$PMTK301,2*2E"; 
        #endregion

        public Action<LatLon> GpsData { get; set; }

        public LatLon Waypoints { get; set; }

        internal Gps()
        {
            Time = DateTime.MinValue;
            Lat = 0.0f;
            Lon = 0.0f;
            Quality = GpsFixQuality.NoFix;
            Heading = 0.0f;
            Altitude = 0.0f;
            FeetPerSecond = 0.0f;
        }

        #region Serial Communication
        public async void Open()
        {
            var sel = SerialDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(sel);
            var selectedPort = dis.FirstOrDefault(d => d.Name.Equals("FT232R USB UART"));

            if (selectedPort == null)
            {
                Debug.WriteLine("No FTDI adapter detected");
                return;
            }

            _serialPort = await SerialDevice.FromIdAsync(selectedPort.Id);
            _serialPort.ReadTimeout = TimeSpan.MaxValue;
            _serialPort.WriteTimeout = TimeSpan.FromSeconds(30);
            _serialPort.BaudRate = 115200;
            _serialPort.Parity = SerialParity.None;
            _serialPort.StopBits = SerialStopBitCount.One;
            _serialPort.DataBits = 8;
            _serialPort.Handshake = SerialHandshake.None;

            Write(PmtkSetNmeaOutputRmcgga);
            await Task.Delay(1400);
            Write(PmtkSetNmeaUpdate10Hz);
            await Task.Delay(1400);
            Write(PmtkApiSetFixCtl10Hz);
            await Task.Delay(1400);
            Write(EnableSbas);
            await Task.Delay(1400);
            Write(SbasModeWaas);
            await Task.Delay(1400);

            Read();
        }

        private void Read()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var sentence = await ReadSentence();

                    if (sentence.Length > 15)
                        Parse(sentence);
                    else
                        Debug.WriteLine(sentence);

                    if (_sw.ElapsedMilliseconds <= 2000)
                        continue;

                    Debug.WriteLine($"{DateTime.Now} - {Lat} {Lon} Angle: {Heading} FPS: {FeetPerSecond} {Quality}");
                    _sw.Restart();
                }
            }, TaskCreationOptions.LongRunning);
        }

        private async void Write(string data)
        {
            try
            {
                _dataWriter = new DataWriter(_serialPort.OutputStream);
                _dataWriter.WriteString(data + '\r' + '\n');
                await _dataWriter.StoreAsync();
                _dataWriter.DetachStream();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to write - {e}");
            }
        }

        //Only read complete sentences
        private async Task<string> WaitForStartOfSentence()
        {
            var r = string.Empty;

            try
            {
                _dataReader = new DataReader(_serialPort.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

                while (!r.Equals("$"))
                {
                    var br = await _dataReader.LoadAsync(1).AsTask();
                    r = _dataReader.ReadString(br);
                }

                _dataReader.DetachStream();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return r;
        }

        private async Task<string> ReadSentence()
        {
            await WaitForStartOfSentence();

            var r = string.Empty;
            var t = string.Empty;

            try
            {
                _dataReader = new DataReader(_serialPort.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

                while (!t.Equals("\r"))
                {
                    var br = await _dataReader.LoadAsync(1).AsTask();
                    t = _dataReader.ReadString(br);

                    if (!t.Equals("\r"))
                        r += t;
                }

                _dataReader.DetachStream();
            }
            catch (Exception e)
            {
                Debug.WriteLine(r + " - " + e);
            }

            return r;
        }
        #endregion


        #region Configure GPS
        //Sets up GPS to opperate at 115200
        public async Task SetGpsBaudRate()
        {
            var sel = SerialDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(sel);
            var selectedPort = dis.FirstOrDefault(d => d.Name.Equals("FT232R USB UART")); //This may change depending on the USB/Serial convertor used.

            if (selectedPort == null)
            {
                Debug.WriteLine("No FTDI adapter detected");
                return;
            }

            _serialPort = await SerialDevice.FromIdAsync(selectedPort.Id);

            _serialPort.ReadTimeout = TimeSpan.MaxValue;
            _serialPort.WriteTimeout = TimeSpan.FromSeconds(30);
            _serialPort.BaudRate = 9600;
            _serialPort.Parity = SerialParity.None;
            _serialPort.StopBits = SerialStopBitCount.One;
            _serialPort.DataBits = 8;
            _serialPort.Handshake = SerialHandshake.None;

            Write(PmtkSetBaud115200);

            await Task.Delay(1000);

            _serialPort.Dispose();

            _serialPort = null;
        }
        #endregion

        //The fourth decimal place is worth up to 11 m: it can identify a parcel of land.It is comparable to the typical accuracy of an uncorrected GPS unit with no interference.
        //The fifth decimal place is worth up to 1.1 m: it distinguish trees from each other.Accuracy to this level with commercial GPS units can only be achieved with differential correction.
        //The sixth decimal place is worth up to 0.11 m: you can use this for laying out structures in detail, for designing landscapes, building roads. 
        //It should be more than good enough for tracking movements of glaciers and rivers. This can be achieved by taking painstaking measures with GPS, such as differentially corrected GPS.
        //The seventh decimal place is worth up to 11 mm: this is good for much surveying and is near the limit of what GPS-based techniques can achieve.

        #region Helpers
        internal void Parse(string data)
        {
            try
            {
                var tokens = data.Split(',');
                var type = tokens[0];

                double lat = 0;
                double lon = 0;

                switch (type)
                {
                    case "GPGGA": //Global Positioning System Fix Data
                        if (tokens.Length < 15)
                            return;

                        if (Time == DateTime.MinValue)
                        {
                            var st = tokens[1];
                            Time = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, Convert.ToInt32(st.Substring(0, 2)), Convert.ToInt32(st.Substring(2, 2)), Convert.ToInt32(st.Substring(4, 2)), DateTimeKind.Local);
                        }

                        lat = Latitude2Double(tokens[2], tokens[3]);
                        lon = Longitude2Double(tokens[4], tokens[5]);

                        var quality = 0;
                        if (int.TryParse(tokens[6], out quality))
                        {
                            Quality = (GpsFixQuality)quality;
                        }

                        float altitude = 0;
                        if (float.TryParse(tokens[9], out altitude))
                            Altitude = altitude * 3.28084f;

                        break;
                    case "GPRMC": //Recommended minimum specific GPS/Transit data
                        if (tokens.Length < 13)
                            return;

                        lat = Latitude2Double(tokens[3], tokens[4]);
                        lon = Longitude2Double(tokens[5], tokens[6]);

                        double fps = 0;
                        if (double.TryParse(tokens[7], out fps))
                            FeetPerSecond = Math.Round(fps * 1.68781, 2); //Convert knots to feet per second or "Speed over ground"

                        double dir = 0;
                        if (double.TryParse(tokens[8], out dir))
                            Heading = dir; //angle from true north that you are traveling or "Course made good"

                        break;
                }

                if (!(lat > 0) || !(Math.Abs(lon) > .01))
                    return;

                Lat = lat;
                Lon = lon;

                var newLatLon = new LatLon {Lat = lat, Lon = lon};

                Add(newLatLon);
            }
            catch (Exception e)
            {
                if (Quality == GpsFixQuality.NoFix)
                {
                    Debug.WriteLine(data);
                }
                else
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine(data);
                }
            }
        }

        internal static double Latitude2Double(string lat, string ns)
        {
            var med = 0d;

            if (!double.TryParse(lat.Substring(2), out med))
                return 0d;

            med = med / 60.0d;

            var temp = 0d;

            if (!double.TryParse(lat.Substring(0, 2), out temp))
                return 0d;

            med += temp;

            if (ns.StartsWith("S"))
            {
                med = -med;
            }

            return Math.Round(med, 7);
        }

        internal static double Longitude2Double(string lon, string we)
        {
            var med = 0d;

            if (!double.TryParse(lon.Substring(3), out med))
                return 0;

            med = med / 60.0d;

            var temp = 0d;

            if (!double.TryParse(lon.Substring(0, 3), out temp))
                return 0d;

            med += temp;

            if (we.StartsWith("W"))
            {
                med = -med;
            }

            return Math.Round(med, 7);
        }
        #endregion

        //http://www.x-io.co.uk/open-source-ahrs-with-x-imu/
        //https://electronics.stackexchange.com/questions/16707/imu-adxl345-itg3200-triple-axis-filter
        //https://github.com/xioTechnologies/Open-Source-AHRS-With-x-IMU
        //http://diydrones.com/forum/topics/using-the-mpu6050-for-quadcopter-orientation-control
        //http://www.nuclearprojects.com/ins/gps.shtml

        internal double DeviationLon { get; set; }
        internal double DeviationLat { get; set; }

        private readonly List<LatLon> _latLons = new List<LatLon>();
        private readonly List<LatLon> _latLonsAvg = new List<LatLon>();
        private readonly List<double> _correctors = new List<double>();

        private double AverageLat()
        {
            return _latLons.Select(lat => lat.Lat).Sum() / _latLons.Count;
        }

        private double AverageLon()
        {
            return _latLons.Select(lon => lon.Lon).Sum() / _latLons.Count;
        }

        internal void Add(LatLon latLon)
        {
            _latLons.Add(latLon);

            Task.Factory.StartNew(() => GpsData?.Invoke(latLon));

            if (_latLons.Count <= 1000)
                return;

            CalculateDistancesFromAverage();
        }

        public double DriftCutoff { get; set; }

        internal void CalculateDistancesFromAverage()
        {
            var avgLat = AverageLat(); //X1
            var avgLon = AverageLon(); //Y1

            foreach (var c in _latLons)
            {
                c.DistanceToAvgCenter = GetDistanceAndHeadingToDestination(c.Lat, c.Lon, avgLat, avgLon)[0];
            }

            _latLonsAvg.Add(new LatLon { Lat = avgLat, Lon = avgLon });

            var maxDistance = _latLons.Max(c => c.DistanceToAvgCenter);
            var minDistance = _latLons.Min(c => c.DistanceToAvgCenter);

            Debug.WriteLine($"Distance - Min {minDistance}in. Max {maxDistance}in.");

            DriftCutoff = (maxDistance + minDistance) / 2;

            _correctors.Add(DriftCutoff);

            foreach (var c in _latLons)
            {
                if (c.DistanceToAvgCenter > DriftCutoff)
                {
                    var dc = c.DistanceToAvgCenter - DriftCutoff;

                    c.CorrectedDistanceToCenter = c.DistanceToAvgCenter - dc;
                }
                else if (c.DistanceToAvgCenter < DriftCutoff)
                {
                    var dc = DriftCutoff - c.DistanceToAvgCenter;

                    c.CorrectedDistanceToCenter = c.DistanceToAvgCenter + dc;
                }
                else
                {
                    c.CorrectedDistanceToCenter = c.DistanceToAvgCenter;
                }
            }
            Debug.WriteLine($"Corrector / accuracy {Math.Round(DriftCutoff, 1)}in.");

            _latLons.RemoveRange(0, _latLons.Count / 2);

            var d = (_correctors.Sum() / Math.Round((double)_correctors.Count, 2));
            Debug.WriteLine($"Average drift { Math.Round(d,1) }in.");

            Debug.WriteLine($"Lat, Lon avg {Math.Round(_latLonsAvg.Sum(l => l.Lat) / _latLonsAvg.Count, 7) }, {Math.Round(_latLonsAvg.Sum(l => l.Lon) / _latLonsAvg.Count, 7)} over {_latLonsAvg.Count}");
        }

        /// <summary>
        /// Returns double[] [0] = distance to heading in inches. [1] = heading to destination waypoint
        /// </summary>
        /// <param name="currentLat"></param>
        /// <param name="currentLon"></param>
        /// <param name="destinationLat"></param>
        /// <param name="destinationLon"></param>
        /// <returns>distance to waypoint, and heading to waypoint</returns>
        public static double[] GetDistanceAndHeadingToDestination(double currentLat, double currentLon, double destinationLat, double destinationLon)
        {
            try
            {
                var diflat = ToRadians(destinationLat - currentLat);

                currentLat = ToRadians(currentLat);                             //convert current latitude to radians
                destinationLat = ToRadians(destinationLat);                     //convert waypoint latitude to radians

                var diflon = ToRadians((destinationLon) - (currentLon));        //subtract and convert longitude to radians

                var distCalc = (Math.Sin(diflat / 2.0) * Math.Sin(diflat / 2.0));
                var distCalc2 = Math.Cos(currentLat);

                distCalc2 = distCalc2 * Math.Cos(destinationLat);
                distCalc2 = distCalc2 * Math.Sin(diflon / 2.0);
                distCalc2 = distCalc2 * Math.Sin(diflon / 2.0);                 //and again, why?
                distCalc += distCalc2;
                distCalc = (2 * Math.Atan2(Math.Sqrt(distCalc), Math.Sqrt(1.0 - distCalc)));
                distCalc = distCalc * 6371000.0;                                //Converting to meters. 6371000 is the magic number,  3959 is average Earth radius in miles
                distCalc = Math.Round(distCalc * 39.3701, 1);                     // and then to inches.

                currentLon = ToRadians(currentLon);
                destinationLon = ToRadians(destinationLon);

                var heading = Math.Atan2(Math.Sin(destinationLon - currentLon) * Math.Cos(destinationLat),
                                        Math.Cos(currentLat) * Math.Sin(destinationLat) - Math.Sin(currentLat) * Math.Cos(destinationLat) * Math.Cos(destinationLon - currentLon));

                heading = FromRadians(heading);

                if (heading < 0)
                    heading += 360;

                return new[] { distCalc, heading };
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new double[] { 0, 0 };
            }
        }

        public async void LoadWaypoints()
        {
            var config = string.Empty;

            try
            {
                var folder = Package.Current.InstalledLocation;
                var file = await folder.GetFileAsync("waypoints.txt");
                config = await FileIO.ReadTextAsync(file);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Cannot read waypoints.txt" + e);
                return;
            }

            if (string.IsNullOrEmpty(config))
            {
                Debug.WriteLine("Empty waypoints.txt file");
                return;
            }

            try
            {
                //TODO : Waypoints.Add(new LatLon .... blah blah
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private static double ToRadians(double conversionValue)
        {
            return conversionValue * Math.PI / 180;
        }

        private static double FromRadians(double conversionValue)
        {
            return conversionValue * 180 / Math.PI;
        }
    }
}
