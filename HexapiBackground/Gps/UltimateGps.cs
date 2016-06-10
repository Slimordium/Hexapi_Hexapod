using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;

namespace HexapiBackground.Gps
{
    //Tested as 100% working with the Adafruit Ultimate GPS
    //Parses the basic GPS data that is needed for navigation. Calculates drift/accuracy over time. Seems to work fairly well

    internal sealed class UltimateGps : IGps
    {
        private readonly List<double> _correctors = new List<double>();
        private readonly List<LatLon> _latLons = new List<LatLon>();
        private readonly List<LatLon> _latLonsAvg = new List<LatLon>();
        private readonly Stopwatch _sw = new Stopwatch();
        private SerialPort _serialPort;

        internal UltimateGps()
        {
            CurrentLatLon = new LatLon();
            SetGpsBaudRate();
        }

        public double DeviationLon { get; private set; }
        public double DeviationLat { get; private set; }
        public double DriftCutoff { get; private set; }

        public LatLon CurrentLatLon { get; private set; }

        #region Configure GPS

        //Sets up GPS to opperate at 115200
        internal void SetGpsBaudRate()
        {
            _serialPort = new SerialPort();
            _serialPort.Open("A104OHRXA", 115200, 2000, 2000).Wait();

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

        public void Start()
        {
            Task.Run(async() =>
            {
                if (_serialPort == null)
                {
                    _serialPort = new SerialPort();
                    await _serialPort.Open("A104OHRXA", 115200, 2000, 2000);
                }

                //Debug.WriteLine("Configuring GPS, please wait...");

                //_serialPort.Write(PmtkSetNmeaOutputRmcgga);
                //Task.Delay(1400).Wait();
                //_serialPort.Write(PmtkSetNmeaUpdate10Hz);
                //Task.Delay(1400).Wait();
                //_serialPort.Write(PmtkApiSetFixCtl10Hz);
                //Task.Delay(1400).Wait();
                //_serialPort.Write(EnableSbas);
                //Task.Delay(1400).Wait();
                //_serialPort.Write(SbasModeWaas);
                //Task.Delay(1400).Wait();

                Debug.WriteLine("Ultimate GPS Started...");
                
                while (true)
                {
                    var sentences = await _serialPort.ReadString();

                    foreach (var sentence in sentences.Split('$').Where(s => s.Length > 15))
                    {
                        var latLon = sentence.ParseNmea();

                        if (latLon == null)
                            continue;

                        CurrentLatLon = latLon;
                    }
                }
            });
        }

        #endregion

        //The fourth decimal place is worth up to 11 m: it can identify a parcel of land.It is comparable to the typical accuracy of an uncorrected GPS unit with no interference.
        //The fifth decimal place is worth up to 1.1 m: it distinguish trees from each other.Accuracy to this level with commercial GPS units can only be achieved with differential correction.
        //The sixth decimal place is worth up to 0.11 m: you can use this for laying out structures in detail, for designing landscapes, building roads. 
        //It should be more than good enough for tracking movements of glaciers and rivers. This can be achieved by taking painstaking measures with GPS, such as differentially corrected GPS.
        //The seventh decimal place is worth up to 11 mm: this is good for much surveying and is near the limit of what GPS-based techniques can achieve.

    }
}