using System;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Storage;

namespace HexapiBackground
{
    sealed class LegConfiguration
    {
        public LegConfiguration()
        {
            LoadDefaults();
        }

        public Leg LegOne { get; private set; }
        public Leg LegTwo { get; private set; }
        public Leg LegThree { get; private set; }
        public Leg LegFour { get; private set; }
        public Leg LegFive { get; private set; }
        public Leg LegSix { get; private set; }

        public async void LoadDefaults()
        {
            var config = string.Empty;

            try
            {
                var folder = Package.Current.InstalledLocation;
                var file = await folder.GetFileAsync("walkerDefaults.config");
                config = await FileIO.ReadTextAsync(file);
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"Cannot read walkerDefaults.config");
                return;
            }


            if (String.IsNullOrEmpty(config))
            {
                Debug.WriteLine(@"Empty config file. walkerDefaults.config");
                return;
            }

            try
            {
                var allLegDefaults = config.Split('\n');

                for (var i = 0; i != 6; i++)
                {
                    var t = allLegDefaults[i].Replace('\r'.ToString(), "");

                    var jointDefaults = t.Split('|');

                    if (i == 0)
                        LegOne = new Leg(LegPosition.One)
                        {
                            CoxaServo = Convert.ToInt32(jointDefaults[0].Split(',')[2]),
                            CoxaMin = Convert.ToInt32(jointDefaults[0].Split(',')[0]),
                            CoxaMax = Convert.ToInt32(jointDefaults[0].Split(',')[1]),
                            FemurServo = Convert.ToInt32(jointDefaults[1].Split(',')[2]),
                            FemurMin = Convert.ToInt32(jointDefaults[1].Split(',')[0]),
                            FemurMax = Convert.ToInt32(jointDefaults[1].Split(',')[1]),
                            TibiaServo = Convert.ToInt32(jointDefaults[2].Split(',')[2]),
                            TibiaMin = Convert.ToInt32(jointDefaults[2].Split(',')[0]),
                            TibiaMax = Convert.ToInt32(jointDefaults[2].Split(',')[1])
                        };

                    if (i == 1)
                        LegTwo = new Leg(LegPosition.Two)
                        {
                            CoxaServo = Convert.ToInt32(jointDefaults[0].Split(',')[2]),
                            CoxaMin = Convert.ToInt32(jointDefaults[0].Split(',')[0]),
                            CoxaMax = Convert.ToInt32(jointDefaults[0].Split(',')[1]),
                            FemurServo = Convert.ToInt32(jointDefaults[1].Split(',')[2]),
                            FemurMin = Convert.ToInt32(jointDefaults[1].Split(',')[0]),
                            FemurMax = Convert.ToInt32(jointDefaults[1].Split(',')[1]),
                            TibiaServo = Convert.ToInt32(jointDefaults[2].Split(',')[2]),
                            TibiaMin = Convert.ToInt32(jointDefaults[2].Split(',')[0]),
                            TibiaMax = Convert.ToInt32(jointDefaults[2].Split(',')[1])
                        };

                    if (i == 2)
                        LegThree = new Leg(LegPosition.Three)
                        {
                            CoxaServo = Convert.ToInt32(jointDefaults[0].Split(',')[2]),
                            CoxaMin = Convert.ToInt32(jointDefaults[0].Split(',')[0]),
                            CoxaMax = Convert.ToInt32(jointDefaults[0].Split(',')[1]),
                            FemurServo = Convert.ToInt32(jointDefaults[1].Split(',')[2]),
                            FemurMin = Convert.ToInt32(jointDefaults[1].Split(',')[0]),
                            FemurMax = Convert.ToInt32(jointDefaults[1].Split(',')[1]),
                            TibiaServo = Convert.ToInt32(jointDefaults[2].Split(',')[2]),
                            TibiaMin = Convert.ToInt32(jointDefaults[2].Split(',')[0]),
                            TibiaMax = Convert.ToInt32(jointDefaults[2].Split(',')[1])
                        };

                    if (i == 3)
                        LegFour = new Leg(LegPosition.Four)
                        {
                            CoxaServo = Convert.ToInt32(jointDefaults[0].Split(',')[2]),
                            CoxaMin = Convert.ToInt32(jointDefaults[0].Split(',')[0]),
                            CoxaMax = Convert.ToInt32(jointDefaults[0].Split(',')[1]),
                            FemurServo = Convert.ToInt32(jointDefaults[1].Split(',')[2]),
                            FemurMin = Convert.ToInt32(jointDefaults[1].Split(',')[0]),
                            FemurMax = Convert.ToInt32(jointDefaults[1].Split(',')[1]),
                            TibiaServo = Convert.ToInt32(jointDefaults[2].Split(',')[2]),
                            TibiaMin = Convert.ToInt32(jointDefaults[2].Split(',')[0]),
                            TibiaMax = Convert.ToInt32(jointDefaults[2].Split(',')[1])
                        };

                    if (i == 4)
                        LegFive = new Leg(LegPosition.Five)
                        {
                            CoxaServo = Convert.ToInt32(jointDefaults[0].Split(',')[2]),
                            CoxaMin = Convert.ToInt32(jointDefaults[0].Split(',')[0]),
                            CoxaMax = Convert.ToInt32(jointDefaults[0].Split(',')[1]),
                            FemurServo = Convert.ToInt32(jointDefaults[1].Split(',')[2]),
                            FemurMin = Convert.ToInt32(jointDefaults[1].Split(',')[0]),
                            FemurMax = Convert.ToInt32(jointDefaults[1].Split(',')[1]),
                            TibiaServo = Convert.ToInt32(jointDefaults[2].Split(',')[2]),
                            TibiaMin = Convert.ToInt32(jointDefaults[2].Split(',')[0]),
                            TibiaMax = Convert.ToInt32(jointDefaults[2].Split(',')[1])
                        };

                    if (i == 5)
                        LegSix = new Leg(LegPosition.Six)
                        {
                            CoxaServo = Convert.ToInt32(jointDefaults[0].Split(',')[2]),
                            CoxaMin = Convert.ToInt32(jointDefaults[0].Split(',')[0]),
                            CoxaMax = Convert.ToInt32(jointDefaults[0].Split(',')[1]),
                            FemurServo = Convert.ToInt32(jointDefaults[1].Split(',')[2]),
                            FemurMin = Convert.ToInt32(jointDefaults[1].Split(',')[0]),
                            FemurMax = Convert.ToInt32(jointDefaults[1].Split(',')[1]),
                            TibiaServo = Convert.ToInt32(jointDefaults[2].Split(',')[2]),
                            TibiaMin = Convert.ToInt32(jointDefaults[2].Split(',')[0]),
                            TibiaMax = Convert.ToInt32(jointDefaults[2].Split(',')[1])
                        };

                }
            }
            catch
            {
                //crash and burn
                return;
            }
        }
    }

    sealed class Leg
    {
        private int _coxaMax;
        private int _coxaMin;
        private int _femurMax;
        private int _femurMin;
        private int _tibiaMax;
        private int _tibiaMin;

        public Leg(LegPosition legPosition)
        {
            LegPosition = legPosition;

            CoxaServo = 0;
            FemurServo = 0;
            TibiaServo = 0;

            CoxaMax = 0;
            CoxaMin = 0;
            CoxaCurrent = 0;
            CoxaOffset = 0;

            FemurMax = 0;
            FemurMin = 0;
            FemurCurrent = 0;
            FemurOffset = 0;

            TibiaMax = 0;
            TibiaMin = 0;
            TibiaCurrent = 0;
            TibiaOffset = 0;
        }

        public LegPosition LegPosition { get; private set; }
        public int CoxaServo { get; set; }
        public int FemurServo { get; set; }
        public int TibiaServo { get; set; }

        public int CoxaMax
        {
            get { return _coxaMax - CoxaOffset; }
            set { _coxaMax = value; }
        }

        public int CoxaMin
        {
            get { return _coxaMin + CoxaOffset; }
            set { _coxaMin = value; }
        }

        public double CoxaCurrent { get; set; }
        public double CoxaTarget { get; set; }
        public int CoxaOffset { get; set; }

        public int FemurMax
        {
            get { return _femurMax - FemurOffset; }
            set { _femurMax = value; }
        }

        public int FemurMin
        {
            get { return _femurMin + FemurOffset; }
            set { _femurMin = value; }
        }

        public double FemurCurrent { get; set; }
        public double FemurTarget { get; set; }
        public int FemurOffset { get; set; }

        public int TibiaMax
        {
            get { return _tibiaMax - TibiaOffset; }
            set { _tibiaMax = value; }
        }

        public int TibiaMin
        {
            get { return _tibiaMin + TibiaOffset; }
            set { _tibiaMin = value; }
        }

        public double TibiaCurrent { get; set; }
        public double TibiaTarget { get; set; }
        public int TibiaOffset { get; set; }
    }
}
