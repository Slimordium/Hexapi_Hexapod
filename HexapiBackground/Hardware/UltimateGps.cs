using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using HexapiBackground.Enums;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    //Tested as 100% working with the Adafruit Ultimate GPS
    //Parses the basic GPS data that is needed for navigation. Calculates drift/accuracy over time. Seems to work fairly well

    internal sealed class UltimateGps
    {
        private readonly List<double> _correctors = new List<double>();
        private readonly List<LatLon> _latLons = new List<LatLon>();
        private readonly List<LatLon> _latLonsAvg = new List<LatLon>();
        private readonly Stopwatch _sw = new Stopwatch();
        private SerialPort _serialPort;

        internal UltimateGps()
        {
            TimeFomGps = DateTime.MinValue;
            CurrentLatitude = 0.0f;
            CurrentLongitude = 0.0f;
            Quality = GpsFixQuality.NoFix;
            CurrentHeading = 0.0f;
            CurrentAltitude = 0.0f;
            CurrentFeetPerSecond = 0.0f;
            
            SetGpsBaudRate();
        }

        internal static DateTime TimeFomGps { get; private set; }
        internal static double CurrentLatitude { get; private set; }
        internal static double CurrentLongitude { get; private set; }
        internal static GpsFixQuality Quality { get; private set; }
        internal static double CurrentHeading { get; private set; }
        internal static float CurrentAltitude { get; private set; }
        internal static double CurrentFeetPerSecond { get; private set; }

        //http://www.x-io.co.uk/open-source-ahrs-with-x-imu/
        //https://electronics.stackexchange.com/questions/16707/imu-adxl345-itg3200-triple-axis-filter
        //https://github.com/xioTechnologies/Open-Source-AHRS-With-x-IMU
        //http://diydrones.com/forum/topics/using-the-mpu6050-for-quadcopter-orientation-control
        //http://www.nuclearprojects.com/ins/gps.shtml

        internal double DeviationLon { get; private set; }
        internal double DeviationLat { get; private set; }
        internal double DriftCutoff { get; private set; }

        #region Configure GPS

        //Sets up GPS to opperate at 115200
        internal void SetGpsBaudRate()
        {
            _serialPort = new SerialPort("A104OHRXA", 115200, 2000, 2000);

            //Task.Delay(500).Wait();

            //if (_serialPort.LastError != SerialError.Frame)
            //{
            //    Debug.WriteLine("GPS Serial port already setup for 115,200");
            //    return;
            //}

            //_serialPort = new SerialPort("A104OHRXA", 9600, 5000, 5000);//A104OHRXA is the serial number of the FTDI chip on the SparkFun USB/ Serial adapter

            //_serialPort.Write(PmtkSetBaud115200);
            //Task.Delay(1000).Wait();

            //_serialPort.Close();
            //_serialPort = null;

            //Task.Delay(500).Wait();
        }

        #endregion

        private double AverageLat()
        {
            return _latLons.Select(lat => lat.Lat).Sum()/_latLons.Count;
        }

        private double AverageLon()
        {
            return _latLons.Select(lon => lon.Lon).Sum()/_latLons.Count;
        }

        internal void CalculateDistancesFromAverage()
        {
            var avgLat = AverageLat(); //X1
            var avgLon = AverageLon(); //Y1

            foreach (var c in _latLons)
            {
                c.DistanceToAvgCenter = GetDistanceAndHeadingToDestination(c.Lat, c.Lon, avgLat, avgLon)[0];
            }

            _latLonsAvg.Add(new LatLon {Lat = avgLat, Lon = avgLon});

            var maxDistance = _latLons.Max(c => c.DistanceToAvgCenter);
            var minDistance = _latLons.Min(c => c.DistanceToAvgCenter);

            Debug.WriteLine($"Distance - Min {minDistance}in. Max {maxDistance}in.");

            DriftCutoff = (maxDistance + minDistance)/2;

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
            
            var d = (_correctors.Sum()/Math.Round((double) _correctors.Count, 2));

            Debug.WriteLine($"Average drift {Math.Round(d, 1)}in.");
            Debug.WriteLine($"Lat, Lon avg {Math.Round(_latLonsAvg.Sum(l => l.Lat)/_latLonsAvg.Count, 7)}, {Math.Round(_latLonsAvg.Sum(l => l.Lon)/_latLonsAvg.Count, 7)} over {_latLonsAvg.Count}");
        }

        /// <summary>
        ///     Returns double[] [0] = distance to heading in inches. [1] = heading to destination waypoint
        /// </summary>
        /// <param name="currentLat"></param>
        /// <param name="currentLon"></param>
        /// <param name="destinationLat"></param>
        /// <param name="destinationLon"></param>
        /// <returns>distance to waypoint, and heading to waypoint</returns>
        internal static double[] GetDistanceAndHeadingToDestination(double currentLat, double currentLon, double destinationLat, double destinationLon)
        {
            try
            {
                var diflat = ToRadians(destinationLat - currentLat);

                currentLat = ToRadians(currentLat); //convert current latitude to radians
                destinationLat = ToRadians(destinationLat); //convert waypoint latitude to radians

                var diflon = ToRadians((destinationLon) - (currentLon)); //subtract and convert longitude to radians

                var distCalc = (Math.Sin(diflat/2.0)*Math.Sin(diflat/2.0));
                var distCalc2 = Math.Cos(currentLat);

                distCalc2 = distCalc2*Math.Cos(destinationLat);
                distCalc2 = distCalc2*Math.Sin(diflon/2.0);
                distCalc2 = distCalc2*Math.Sin(diflon/2.0); //and again, why?
                distCalc += distCalc2;
                distCalc = (2*Math.Atan2(Math.Sqrt(distCalc), Math.Sqrt(1.0 - distCalc)));
                distCalc = distCalc*6371000.0;
                    //Converting to meters. 6371000 is the magic number,  3959 is average Earth radius in miles
                distCalc = Math.Round(distCalc*39.3701, 1); // and then to inches.

                currentLon = ToRadians(currentLon);
                destinationLon = ToRadians(destinationLon);

                var heading = Math.Atan2(Math.Sin(destinationLon - currentLon)*Math.Cos(destinationLat),
                    Math.Cos(currentLat)*Math.Sin(destinationLat) -
                    Math.Sin(currentLat)*Math.Cos(destinationLat)*Math.Cos(destinationLon - currentLon));

                heading = FromRadians(heading);

                if (heading < 0)
                    heading += 360;

                return new[] {Math.Round(distCalc, 1), Math.Round(heading, 1)};
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new double[] {0, 0};
            }
        }

        private static double ToRadians(double conversionValue)
        {
            return conversionValue*Math.PI/180;
        }

        private static double FromRadians(double conversionValue)
        {
            return conversionValue*180/Math.PI;
        }

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

        #region Serial Communication

        internal void Start()
        {
            Task.Factory.StartNew(() =>
            {
                if (_serialPort == null)
                    _serialPort = new SerialPort("A104OHRXA", 115200, 2000, 2000);

                Debug.WriteLine("Configuring GPS, please wait...");

                _serialPort.Write(PmtkSetNmeaOutputRmcgga);
                Task.Delay(1400).Wait();
                _serialPort.Write(PmtkSetNmeaUpdate10Hz);
                Task.Delay(1400).Wait();
                _serialPort.Write(PmtkApiSetFixCtl10Hz);
                Task.Delay(1400).Wait();
                _serialPort.Write(EnableSbas);
                Task.Delay(1400).Wait();
                _serialPort.Write(SbasModeWaas);
                Task.Delay(1400).Wait();


                Debug.WriteLine("GPS Started...");
                
                while (true)
                {
                    var sentences = _serialPort.ReadString();

                    foreach (var s in sentences.Split('$').Where(s => s.Length > 15)) { Parse(s); }
                }
            }, TaskCreationOptions.LongRunning);
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

                        if (TimeFomGps == DateTime.MinValue)
                        {
                            var st = tokens[1];
                            TimeFomGps = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                Convert.ToInt32(st.Substring(0, 2)), Convert.ToInt32(st.Substring(2, 2)),
                                Convert.ToInt32(st.Substring(4, 2)), DateTimeKind.Local);
                        }

                        lat = Latitude2Double(tokens[2], tokens[3]);
                        lon = Longitude2Double(tokens[4], tokens[5]);

                        var quality = 0;
                        if (int.TryParse(tokens[6], out quality))
                        {
                            Quality = (GpsFixQuality) quality;
                        }

                        float altitude = 0;
                        if (float.TryParse(tokens[9], out altitude))
                            CurrentAltitude = altitude*3.28084f;

                        break;
                    case "GPRMC": //Recommended minimum specific GPS/Transit data
                        if (tokens.Length < 13)
                            return;

                        lat = Latitude2Double(tokens[3], tokens[4]);
                        lon = Longitude2Double(tokens[5], tokens[6]);

                        double fps = 0;
                        if (double.TryParse(tokens[7], out fps))
                            CurrentFeetPerSecond = Math.Round(fps*1.68781, 2);
                        //Convert knots to feet per second or "Speed over ground"

                        double dir = 0;
                        if (double.TryParse(tokens[8], out dir))
                            CurrentHeading = dir; //angle from true north that you are traveling or "Course made good"

                        break;
                }

                if (!(lat > 0) || !(Math.Abs(lon) > .01))
                    return;

                CurrentLatitude = lat;
                CurrentLongitude = lon;

                _latLons.Add(new LatLon { Lat = lat, Lon = lon, FeetPerSecond = CurrentFeetPerSecond, Altitude = CurrentAltitude});

                if (_latLons.Count > 30000)
                    _latLons.RemoveAt(0);
            }
            catch (ArgumentOutOfRangeException)
            {
                //No fix yet
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

            med = med/60.0d;

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

            med = med/60.0d;

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
    }
}