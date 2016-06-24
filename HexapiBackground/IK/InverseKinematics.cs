using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Devices.Perception;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.Navigation;

#pragma warning disable 4014

namespace HexapiBackground.IK
{



    /// <summary>
    ///     This is a port of the "Phoenix" 3DOF Hexapod code in C#. Uses CH3-R body from Lynxmotion/robotshop.com
    ///     https://github.com/KurtE/Arduino_Phoenix_Parts/tree/master/Phoenix
    ///     http://www.robotshop.com/en/lynxmotion-aexapod-ch3-r-combo-kit-body-frame.html
    /// </summary>
    internal sealed class InverseKinematics{
        private const double Pi = 3.1415926535897932384626433832795028841971693993751058209749445923078164; //This seemed to help 
        private static readonly StringBuilder StringBuilder = new StringBuilder();
        internal static ManualResetEventSlim SscCommandCompleteEvent = new ManualResetEventSlim(false);
        private readonly GpioPin[] _legGpioPins = new GpioPin[6];

        private readonly StringBuilder _pinChangedStringBuilder = new StringBuilder();
        private readonly byte[] _querySsc = {0x51, 0x0d}; //0x51 = Q, 0x0d = carriage return
        private bool _calibrated;
        private GpioController _gpioController;
        private SelectedIkFunction _lastSelectedIkFunction = SelectedIkFunction.Translate3D;
        private int _lastSelectedFunctionLeg = -1;
        private bool _movementStarted;
        private SelectedIkFunction _selectedFunction = SelectedIkFunction.Translate3D;
        private int _selectedFunctionLeg = -1;
        private static SerialDevice _serialDevice;


        #region Inverse Kinematics setup

        private const double CoxaLengthInMm = 33; //mm
        private const double FemurLengthInMm = 70; //mm
        private const double TibiaLengthInMm = 130; //mm

        private const double HexInitXz = CoxaLengthInMm + FemurLengthInMm;
        private const double HexInitXzCos45 = HexInitXz * .7071; //http://www.math.com/tables/trig/tables.htm
        private const double HexInitXzSin45 = HexInitXz * .7071;
        private const double HexInitY = 25;

        //For the Solar 772 or 771, PwmDiv = 1500 and PfConst = 900 works well. Not sure what this should be on any other servo
        private const double PfConst = 900;
        private const double PwmDiv = 1500;

        private const int Lf = 5;
        private const int Lm = 4;
        private const int Lr = 3;
        private const int Rf = 2;
        private const int Rm = 1;
        private const int Rr = 0;

        private const double CoxaMin = -650;
        private const double CoxaMax = 650;
        private const double FemurMin = -670;
        private const double FemurMax = 670;
        private const double TibiaMin = -670;
        private const double TibiaMax = 670; //I think this is the "down" angle limit, meaning how far in relation to the femur can it point towards the center of the bot

        private const double RrCoxaAngle = -450; //450 = 45 degrees off center
        private const double RmCoxaAngle = 0;
        private const double RfCoxaAngle = 450;
        private const double LrCoxaAngle = -450;
        private const double LmCoxaAngle = 0;
        private const double LfCoxaAngle = 450;

        private const double RfOffsetZ = -126; //Distance Z from center line that crosses from front/back of the body to the coxa (Z front/back)
        private const double RfOffsetX = -70; //Distance X from center line that crosses left/right of the body to the coxa (X side to side)
        private const double LfOffsetZ = -126;
        private const double LfOffsetX = 70;
        private const double RrOffsetZ = 126;
        private const double RrOffsetX = -70;
        private const double LrOffsetZ = 126;
        private const double LrOffsetX = 70;
        private const double RmOffsetZ = 0;
        private const double RmOffsetX = -135;
        private const double LmOffsetZ = 0;
        private const double LmOffsetX = 135;

        private const double TravelDeadZone = 1;

        private const double TenThousand = 10000;
        private const double OneMillion = 1000000;

        private const double RfInitPosX = HexInitXzCos45;
        private const double RfInitPosY = HexInitY;
        private const double RfInitPosZ = -HexInitXzSin45;

        private const double LrInitPosX = HexInitXzCos45;
        private const double LrInitPosY = HexInitY;
        private const double LrInitPosZ = HexInitXzCos45;

        private const double LmInitPosX = HexInitXz;
        private const double LmInitPosY = HexInitY;
        private const double LmInitPosZ = 0;

        private const double LfInitPosX = HexInitXzCos45;
        private const double LfInitPosY = HexInitY;
        private const double LfInitPosZ = -HexInitXzSin45;

        private const double RmInitPosX = HexInitXz;
        private const double RmInitPosY = HexInitY;
        private const double RmInitPosZ = 0;

        private const double RrInitPosX = HexInitXzCos45;
        private const double RrInitPosY = HexInitY;
        private const double RrInitPosZ = HexInitXzSin45;

        private readonly double[] _initPosX = { RrInitPosX, RmInitPosX, RfInitPosX, LrInitPosX, LmInitPosX, LfInitPosX };
        private readonly double[] _initPosY = { RrInitPosY, RmInitPosY, RfInitPosY, LrInitPosY, LmInitPosY, LfInitPosY };
        private readonly double[] _initPosZ = { RrInitPosZ, RmInitPosZ, RfInitPosZ, LrInitPosZ, LmInitPosZ, LfInitPosZ };

        private readonly double[] _offsetX = { RrOffsetX, RmOffsetX, RfOffsetX, LrOffsetX, LmOffsetX, LfOffsetX };
        private readonly double[] _offsetZ = { RrOffsetZ, RmOffsetZ, RfOffsetZ, LrOffsetZ, LmOffsetZ, LfOffsetZ };

        private readonly double[] _calculatedCoxaAngle = { RrCoxaAngle, RmCoxaAngle, RfCoxaAngle, LrCoxaAngle, LmCoxaAngle, LfCoxaAngle };

        private readonly double[] _coxaAngle = new double[6];
        private readonly double[] _femurAngle = new double[6]; //Actual Angle of the vertical hip, decimals = 1
        private readonly double[] _tibiaAngle = new double[6]; //Actual Angle of the knee, decimals = 1

        private static readonly double[] CoxaServoAngles = new double[6];
        private static readonly double[] FemurServoAngles = new double[6];
        private static readonly double[] TibiaServoAngles = new double[6];

        private readonly int[] _gaitLegNumber = new int[6]; //Initial position of the leg

        private double[] _gaitPosX = new double[6]; //Array containing Relative X position corresponding to the Gait
        private double[] _gaitPosY = new double[6]; //Array containing Relative Y position corresponding to the Gait
        private double[] _gaitPosZ = new double[6]; //Array containing Relative Z position corresponding to the Gait
        private double[] _gaitRotY = new double[6]; //Array containing Relative Y rotation corresponding to the Gait

        private readonly double[] _legPosX = new double[6]; //Actual X Position of the Leg 
        private readonly double[] _legPosY = new double[6]; //Actual Y Position of the Leg
        private readonly double[] _legPosZ = new double[6]; //Actual Z Position of the Leg

        private static bool _lastLeg = true;

        private int _liftDivisionFactor; //Normaly: 2, when NrLiftedPos=5: 4
        private int _numberOfLiftedPositions; //Number of positions that a single leg is lifted [1-3]
        private int _stepsInGait; //Number of steps in gait
        private int _tlDivisionFactor; //Number of steps that a leg is on the floor while walking
        private bool _travelRequest; //is the gait is in motion

        private double _bodyPosX;
        private double _bodyPosY = 42; //Controls height of the body from the ground
        private double _lastBodyPosY;
        private double _bodyPosZ;

        private double _bodyRotX; //Global Input pitch of the body
        private double _bodyRotY; //Global Input rotation of the body
        private double _bodyRotZ; //Global Input roll of the body

        private int _halfLiftHeight; //If true the outer positions of the lifted legs will be half height    
        private double _legLiftHeight = 35; //Current Travel height

        private static int _gaitStep = 1;
        private GaitType _gaitType = GaitType.Tripod8;
        private GaitType _lastGaitType = GaitType.Tripod8;
        private static double _gaitSpeedInMs = 40; //Nominal speed of the gait in ms

        private double _travelLengthX; //Current Travel length X - Left/Right
        private double _travelLengthZ; //Current Travel length Z - Negative numbers = "forward" movement.
        private double _travelRotationY; //Current Travel Rotation Y 

        private static readonly int[][] LegServos = new int[6][]; //Leg index,
        private static readonly double[] LegYHeightCorrector = new double[6]; //Leg index,

        private static double _pi1K;
        private bool _calibrating;

        #endregion

        internal InverseKinematics()
        {
            _pi1K = Pi*1000D;

            for (var i = 0; i < 6; i++)
                LegServos[i] = new int[3];

            _movementStarted = false;

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _legPosX[legIndex] = _initPosX[legIndex]; //Set start positions for each leg
                _legPosY[legIndex] = _initPosY[legIndex];
                _legPosZ[legIndex] = _initPosZ[legIndex];

                LegYHeightCorrector[legIndex] = 0;
            }

            
        }

        private void ConfigureFootSwitches()
        {
            try
            {
                _gpioController = GpioController.GetDefault();
            }
            catch (Exception e)
            {
                //Debug.WriteLine(e);
            }

            if (_gpioController != null)
            {
                _legGpioPins[0] = _gpioController.OpenPin(26);
                _legGpioPins[1] = _gpioController.OpenPin(19);
                _legGpioPins[2] = _gpioController.OpenPin(13);
                _legGpioPins[3] = _gpioController.OpenPin(16);
                _legGpioPins[4] = _gpioController.OpenPin(20);
                _legGpioPins[5] = _gpioController.OpenPin(21);

                foreach (var legGpioPin in _legGpioPins)
                {
                    legGpioPin.DebounceTimeout = new TimeSpan(0, 0, 0, 0, 1);
                    legGpioPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                }
            }
            else
            {
                Display.Write("Could not find Gpio Controller", 1);
            }
        }

        #region Body and Leg Inverse Kinematics

        private static double[] BodyLegIk(int legIndex,
            double legPosX, double legPosY, double legPosZ,
            double bodyPosX, double bodyPosY, double bodyPosZ,
            double gaitPosX, double gaitPosY, double gaitPosZ, double gaitRotY,
            double offsetX, double offsetZ,
            double bodyRotX, double bodyRotZ, double bodyRotY,
            double coxaAngle)
        {
            var posX = 0D;
            if (legIndex <= 2)
                posX = -legPosX + bodyPosX + gaitPosX;
            else
                posX = legPosX - bodyPosX + gaitPosX;

            var posY = (legPosY + bodyPosY + gaitPosY)*100;
            var posZ = legPosZ + bodyPosZ + gaitPosZ;

            var centerOfBodyToFeetX = (offsetX + posX)*100;
            var centerOfBodyToFeetZ = (offsetZ + posZ)*100;

            double bodyRotYSin, bodyRotYCos, bodyRotZSin, bodyRotZCos, bodyRotXSin, bodyRotXCos;

            GetSinCos(bodyRotY + gaitRotY, out bodyRotYSin, out bodyRotYCos);
            GetSinCos(bodyRotZ, out bodyRotZSin, out bodyRotZCos);
            GetSinCos(bodyRotX, out bodyRotXSin, out bodyRotXCos);

            //Calculation of rotation matrix: 
            var bodyFkPosX = (centerOfBodyToFeetX -
                              ((centerOfBodyToFeetX*bodyRotYCos*bodyRotZCos) -
                               (centerOfBodyToFeetZ*bodyRotZCos*bodyRotYSin) +
                               (posY*bodyRotZSin)))/100;

            var bodyFkPosZ = (centerOfBodyToFeetZ -
                              ((centerOfBodyToFeetX*bodyRotXCos*bodyRotYSin) +
                               (centerOfBodyToFeetX*bodyRotYCos*bodyRotZSin*bodyRotXSin) +
                               (centerOfBodyToFeetZ*bodyRotYCos*bodyRotXCos) -
                               (centerOfBodyToFeetZ*bodyRotYSin*bodyRotZSin*bodyRotXSin) -
                               (posY*bodyRotZCos*bodyRotXSin)))/100;

            var bodyFkPosY = (posY -
                              ((centerOfBodyToFeetX*bodyRotYSin*bodyRotXSin) -
                               (centerOfBodyToFeetX*bodyRotYCos*bodyRotXCos*bodyRotZSin) +
                               (centerOfBodyToFeetZ*bodyRotYCos*bodyRotXSin) +
                               (centerOfBodyToFeetZ*bodyRotXCos*bodyRotYSin*bodyRotZSin) +
                               (posY*bodyRotZCos*bodyRotXCos)))/100;

            var coxaFemurTibiaAngle = new double[3];

            var feetPosX = 0D;
            if (legIndex <= 2)
                feetPosX = legPosX - bodyPosX + bodyFkPosX - gaitPosX;
            else
                feetPosX = legPosX + bodyPosX - bodyFkPosX + gaitPosX;

            var feetPosY = legPosY + bodyPosY - bodyFkPosY + gaitPosY;
            var feetPosZ = legPosZ + bodyPosZ - bodyFkPosZ + gaitPosZ;

            double xyhyp;
            var atan2 = GetATan2(feetPosX, feetPosZ, out xyhyp);

            coxaFemurTibiaAngle[0] = ((atan2*180)/_pi1K) + coxaAngle;

            var ikFeetPosXz = xyhyp/100;
            var ika14 = GetATan2(feetPosY, ikFeetPosXz - CoxaLengthInMm, out xyhyp);
            var ika24 = GetArcCos((((FemurLengthInMm*FemurLengthInMm) - (TibiaLengthInMm*TibiaLengthInMm))*TenThousand + (xyhyp*xyhyp))/((2*FemurLengthInMm*100*xyhyp)/TenThousand));

            coxaFemurTibiaAngle[1] = -(ika14 + ika24)*180/_pi1K + 900;

            coxaFemurTibiaAngle[2] = -(900 - GetArcCos((((FemurLengthInMm*FemurLengthInMm) + (TibiaLengthInMm*TibiaLengthInMm))*TenThousand - (xyhyp*xyhyp))/(2*FemurLengthInMm*TibiaLengthInMm))*180/_pi1K);

            return coxaFemurTibiaAngle;
        }

        #endregion

        #region Request movement

        internal void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            _gaitSpeedInMs = gaitSpeed;
            _travelLengthX = travelLengthX;
            _travelLengthZ = travelLengthZ;
            _travelRotationY = travelRotationY;
        }

        internal void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ, double bodyPosY, double bodyRotY1)
        {
            _bodyRotX = bodyRotX1;
            _bodyRotZ = bodyRotZ1;

            _bodyPosX = bodyPosX;
            _bodyPosZ = bodyPosZ;
            _bodyPosY = bodyPosY;
            _bodyRotY = bodyRotY1; //body rotation
        }

        internal void RequestSetGaitOptions(double gaitSpeed, double legLiftHeight)
        {
            _gaitSpeedInMs = gaitSpeed;
            _legLiftHeight = legLiftHeight;
        }

        internal void RequestSetGaitType(GaitType gaitType)
        {
            _lastGaitType = _gaitType;
            _gaitType = gaitType;

            GaitSelect();
        }

        internal void RequestSetMovement(bool enabled)
        {
            _movementStarted = enabled;
        }

        internal void RequestSetFunction(SelectedIkFunction selectedIkFunction)
        {
            _selectedFunction = selectedIkFunction;
        }

        internal void CalibrateFootHeight()
        {
            _selectedFunction = SelectedIkFunction.SetFootHeightOffset;

            Task.Factory.StartNew(() =>
            {
                var height = 0;

                for (var i = 0; i < 6; i++)
                {
                    while (_legGpioPins[i].Read() != GpioPinValue.Low)
                    {
                        height += 2;

                        RequestLegYHeight(i, height);
                    }
                }

                _calibrated = true;
            });
        }

        internal void RequestLegYHeight(int leg, double yPos)
        {
            _selectedFunctionLeg = leg;

            LegYHeightCorrector[leg] = _bodyPosY + yPos;
        }

        //The idea here, is that if a foot hits an object, the corrector is set to the negative value of the current foot height,
        //then for that leg, the body height is adjusted accordingly. 
        //So if a foot is half-way to the floor when it contacts something, it would adjust the body height by half for that leg.
        //Not event sure if this will work!
        //The value will be stored in LegYHeightCorrector
        //IK Calculations will need to be modified to use this.
        internal void RequestSaveLegYHeightCorrector()
        {
            LegYHeightCorrector[_selectedFunctionLeg] = _bodyPosY - _lastBodyPosY;
        }

        #endregion

        #region Main loop  

        private DataReader _inputStream;
        private DataWriter _outputStream;

        internal async Task Start()
        {
            if (!await LoadLegDefaults())
            {
                await Display.Write("Could not setup IK. Exiting...");
                return;
            }

            await Task.Delay(500);

            _serialDevice = await SerialDeviceHelper.GetSerialDevice("BCM2836", 115200);

            await Task.Delay(500);

            _inputStream = new DataReader(_serialDevice.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            _outputStream = new DataWriter(_serialDevice.OutputStream);

            await Task.Delay(500);

            while (true)
            {
                if (_movementStarted)
                {
                    _travelRequest = (Math.Abs(_travelLengthX) > TravelDeadZone) || (Math.Abs(_travelLengthZ) > TravelDeadZone) || (Math.Abs(_travelRotationY) > TravelDeadZone);

                    IkLoop();

                    _lastLeg = false;

                    _outputStream.WriteString(GetServoPositions(_coxaAngle, _femurAngle, _tibiaAngle));
                    await _outputStream.StoreAsync();
                }
                else
                {
                    _outputStream.WriteString(TurnOffServos());
                    await _outputStream.StoreAsync();
                }

                while (true)
                {
                    var bytesIn = await _inputStream.LoadAsync(1);
                    if (bytesIn > 0)
                    {
                        if (_inputStream.ReadByte() == 0x2e)
                            break;
                    }
                    _outputStream.WriteBytes(_querySsc);
                        await _outputStream.StoreAsync();
                }
            }
        }

        private void IkCalculation(int legIndex)
        {
            if (legIndex == 5)
                _lastLeg = true;

            var gaitPosXyZrotY = Gait(legIndex, _travelRequest, _travelLengthX,
                _travelLengthZ, _travelRotationY,
                _gaitPosX, _gaitPosY, _gaitPosZ, _gaitRotY,
                _numberOfLiftedPositions, _gaitLegNumber[legIndex],
                _legLiftHeight, _liftDivisionFactor, _halfLiftHeight, _stepsInGait, _tlDivisionFactor);

            _gaitPosX = gaitPosXyZrotY[0];
            _gaitPosY = gaitPosXyZrotY[1];
            _gaitPosZ = gaitPosXyZrotY[2];
            _gaitRotY = gaitPosXyZrotY[3];

            var angles = BodyLegIk(legIndex,
                _legPosX[legIndex], _legPosY[legIndex], _legPosZ[legIndex],
                _bodyPosX, _bodyPosY + LegYHeightCorrector[legIndex], _bodyPosZ,
                _gaitPosX[legIndex], _gaitPosY[legIndex], _gaitPosZ[legIndex], _gaitRotY[legIndex],
                _offsetX[legIndex], _offsetZ[legIndex],
                _bodyRotX, _bodyRotZ, _bodyRotY, _calculatedCoxaAngle[legIndex]);

            _coxaAngle[legIndex] = angles[0];
            _femurAngle[legIndex] = angles[1];
            _tibiaAngle[legIndex] = angles[2];
        }

        private void IkLoop()
        {
            for (var legIndex = 0; legIndex < 6; legIndex++)
            {
                IkCalculation(legIndex);
            }
        }

        #endregion


        #region Gait calculations and logic

        public bool GaitSelect()
        {
            switch (_gaitType)
            {
                case GaitType.RippleGait12:
                    _gaitLegNumber[Lr] = 1;
                    _gaitLegNumber[Rf] = 3;
                    _gaitLegNumber[Lm] = 5;
                    _gaitLegNumber[Rr] = 7;
                    _gaitLegNumber[Lf] = 9;
                    _gaitLegNumber[Rm] = 11;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 8;
                    _stepsInGait = 12;
                    break;
                case GaitType.Tripod8:
                    _gaitLegNumber[Lr] = 5;
                    _gaitLegNumber[Rf] = 1;
                    _gaitLegNumber[Lm] = 1;
                    _gaitLegNumber[Rr] = 1;
                    _gaitLegNumber[Lf] = 5;
                    _gaitLegNumber[Rm] = 5;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 4;
                    _stepsInGait = 8;
                    break;
                case GaitType.TripleTripod12:
                    _gaitLegNumber[Rf] = 3;
                    _gaitLegNumber[Lm] = 4;
                    _gaitLegNumber[Rr] = 5;
                    _gaitLegNumber[Lf] = 9;
                    _gaitLegNumber[Rm] = 10;
                    _gaitLegNumber[Lr] = 11;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 8;
                    _stepsInGait = 12;
                    break;
                case GaitType.TripleTripod16:
                    _gaitLegNumber[Rf] = 4;
                    _gaitLegNumber[Lm] = 5;
                    _gaitLegNumber[Rr] = 6;
                    _gaitLegNumber[Lf] = 12;
                    _gaitLegNumber[Rm] = 13;
                    _gaitLegNumber[Lr] = 14;

                    _numberOfLiftedPositions = 5;
                    _halfLiftHeight = 1;
                    _tlDivisionFactor = 10;
                    _stepsInGait = 16;
                    break;
                case GaitType.Wave24:
                    _gaitLegNumber[Lr] = 1;
                    _gaitLegNumber[Rf] = 21;
                    _gaitLegNumber[Lm] = 5;

                    _gaitLegNumber[Rr] = 13;
                    _gaitLegNumber[Lf] = 9;
                    _gaitLegNumber[Rm] = 17;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 20;
                    _stepsInGait = 24;
                    break;
            }

            _liftDivisionFactor = _numberOfLiftedPositions == 5 ? 4 : 2;

            return true;
        }

        private static double[][] Gait(int legIndex, bool travelRequest, double travelLengthX, double travelLengthZ, double travelRotationY,
            double[] gaitPosX, double[] gaitPosY, double[] gaitPosZ, double[] gaitRotY,
            int numberOfLiftedPositions, int gaitLegNr,
            double legLiftHeight, int liftDivFactor, double halfLiftHeight, int stepsInGait, int tlDivFactor)
        {
            var gaitXyZrotY = new double[4][];

            if ((travelRequest &&
                 (numberOfLiftedPositions == 1 || numberOfLiftedPositions == 3 || numberOfLiftedPositions == 5) && _gaitStep == gaitLegNr) ||
                (!travelRequest &&
                 _gaitStep == gaitLegNr && ((Math.Abs(gaitPosX[legIndex]) > 2) || (Math.Abs(gaitPosZ[legIndex]) > 2) || (Math.Abs(gaitRotY[legIndex]) > 2))))
            {
                gaitPosX[legIndex] = 0;
                gaitPosY[legIndex] = -legLiftHeight;
                gaitPosZ[legIndex] = 0;
                gaitRotY[legIndex] = 0;
            }
            //Optional Half height Rear (2, 3, 5 lifted positions)
            else if (travelRequest &&
                     ((numberOfLiftedPositions == 2 && _gaitStep == gaitLegNr) || (numberOfLiftedPositions >= 3 && (_gaitStep == gaitLegNr - 1 || _gaitStep == gaitLegNr + (stepsInGait - 1)))))
            {
                gaitPosX[legIndex] = -travelLengthX/liftDivFactor;
                gaitPosY[legIndex] = -3*legLiftHeight/(3 + halfLiftHeight);
                gaitPosZ[legIndex] = -travelLengthZ/liftDivFactor;
                gaitRotY[legIndex] = -travelRotationY/liftDivFactor;
            }
            // Optional Half height front (2, 3, 5 lifted positions)
            else if (travelRequest &&
                     (numberOfLiftedPositions >= 2) &&
                     (_gaitStep == gaitLegNr + 1 || _gaitStep == gaitLegNr - (stepsInGait - 1)))
            {
                gaitPosX[legIndex] = travelLengthX/liftDivFactor;
                gaitPosY[legIndex] = -3*legLiftHeight/(3 + halfLiftHeight);
                gaitPosZ[legIndex] = travelLengthZ/liftDivFactor;
                gaitRotY[legIndex] = travelRotationY/liftDivFactor;
            }
            //Optional Half heigth Rear 5 LiftedPos (5 lifted positions)
            else if (travelRequest &&
                     ((numberOfLiftedPositions == 5 && (_gaitStep == gaitLegNr - 2))))
            {
                gaitPosX[legIndex] = -travelLengthX/2;
                gaitPosY[legIndex] = -legLiftHeight/2;
                gaitPosZ[legIndex] = -travelLengthZ/2;
                gaitRotY[legIndex] = -travelRotationY/2;
            }
            //Optional Half heigth Front 5 LiftedPos (5 lifted positions)
            else if (travelRequest &&
                     (numberOfLiftedPositions == 5) &&
                     (_gaitStep == gaitLegNr + 2 || _gaitStep == gaitLegNr - (stepsInGait - 2)))
            {
                gaitPosX[legIndex] = travelLengthX/2;
                gaitPosY[legIndex] = -legLiftHeight/2;
                gaitPosZ[legIndex] = travelLengthZ/2;
                gaitRotY[legIndex] = travelRotationY/2;
            }
            //Leg front down position
            else if ((_gaitStep == gaitLegNr + numberOfLiftedPositions || _gaitStep == gaitLegNr - (stepsInGait - numberOfLiftedPositions)) &&
                     gaitPosY[legIndex] < 0)
            {
                gaitPosX[legIndex] = travelLengthX/2;
                gaitPosZ[legIndex] = travelLengthZ/2;
                gaitRotY[legIndex] = travelRotationY/2;
                gaitPosY[legIndex] = 0;
            }
            //Move body forward      
            else
            {
                gaitPosX[legIndex] = gaitPosX[legIndex] - (travelLengthX/tlDivFactor);
                gaitPosY[legIndex] = 0;
                gaitPosZ[legIndex] = gaitPosZ[legIndex] - (travelLengthZ/tlDivFactor);
                gaitRotY[legIndex] = gaitRotY[legIndex] - (travelRotationY/tlDivFactor);
            }

            gaitXyZrotY[0] = gaitPosX;
            gaitXyZrotY[1] = gaitPosY;
            gaitXyZrotY[2] = gaitPosZ;
            gaitXyZrotY[3] = gaitRotY;

            if (!_lastLeg)
                return gaitXyZrotY;

            _gaitStep = _gaitStep + 1;

            if (_gaitStep > stepsInGait) //The last leg in this step
                _gaitStep = 1;

            return gaitXyZrotY;
        }

        #endregion

        #region Servo related, build various servo controller strings and read values

        private static string GetServoPositions(IList<double> coxaAngles, IList<double> femurAngles, IList<double> tibiaAngles)
        {
            StringBuilder.Clear();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                coxaAngles[legIndex] = Math.Min(Math.Max(coxaAngles[legIndex], CoxaMin), CoxaMax);
                femurAngles[legIndex] = Math.Min(Math.Max(femurAngles[legIndex], FemurMin), FemurMax);
                tibiaAngles[legIndex] = Math.Min(Math.Max(tibiaAngles[legIndex], TibiaMin), TibiaMax);

                double coxaPosition;
                double femurPosition;
                double tibiaPosition;

                if (legIndex < 3)
                {
                    coxaPosition = Math.Round((-coxaAngles[legIndex] + 900)*1000/PwmDiv + PfConst);
                    femurPosition = Math.Round((-femurAngles[legIndex] + 900)*1000/PwmDiv + PfConst);
                    tibiaPosition = Math.Round((-tibiaAngles[legIndex] + 900)*1000/PwmDiv + PfConst);
                }
                else
                {
                    coxaPosition = Math.Round((coxaAngles[legIndex] + 900)*1000/PwmDiv + PfConst);
                    femurPosition = Math.Round((femurAngles[legIndex] + 900)*1000/PwmDiv + PfConst);
                    tibiaPosition = Math.Round((tibiaAngles[legIndex] + 900)*1000/PwmDiv + PfConst);
                }

                CoxaServoAngles[legIndex] = coxaPosition;
                FemurServoAngles[legIndex] = femurPosition;
                TibiaServoAngles[legIndex] = tibiaPosition;

                StringBuilder.Append($"#{LegServos[legIndex][0]}P{coxaPosition}");
                StringBuilder.Append($"#{LegServos[legIndex][1]}P{femurPosition}");
                StringBuilder.Append($"#{LegServos[legIndex][2]}P{tibiaPosition}");
            }

            StringBuilder.Append($"T{_gaitSpeedInMs}\rQ\r");

            return StringBuilder.ToString();
        }

        private static string TurnOffServos()
        {
            StringBuilder.Clear();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                StringBuilder.Append($"#{LegServos[legIndex][0]}P0");
                StringBuilder.Append($"#{LegServos[legIndex][1]}P0");
                StringBuilder.Append($"#{LegServos[legIndex][2]}P0");
            }

            StringBuilder.Append("T0\rQ\r");

            return StringBuilder.ToString();
        }

        internal async Task<bool> LoadLegDefaults()
        {
            var config = await "hexapod.config".ReadStringFromFile();

            if (string.IsNullOrEmpty(config))
            {
                await Display.Write("Empty hexapod.config");
                return false;
            }

            config = config.Replace("\n", "");

            try
            {
                var allLegDefaults = config.Split('\r');

                for (var i = 0; i < 6; i++)
                {
                    var jointDefaults = allLegDefaults[i].Split('|');

                    LegServos[i][0] = Convert.ToInt32(jointDefaults[0]);
                    LegServos[i][1] = Convert.ToInt32(jointDefaults[1]);
                    LegServos[i][2] = Convert.ToInt32(jointDefaults[2]);
                }
            }
            catch (Exception e)
            {
                await Display.Write(e.Message, 1);
                return false;
            }

            GaitSelect();

            return true;
        }

        #endregion

        #region MathHelpers, and static methods

        private static void GetSinCos(double angleDeg, out double sin, out double cos)
        {
            var angle = Pi*angleDeg/180.0;

            sin = Math.Sin(angle);
            cos = Math.Cos(angle);
        }

        private static double GetArcCos(double cos)
        {
            var c = cos/TenThousand;

            if ((Math.Abs(Math.Abs(c) - 1.0) < 1e-60)) //Why does this make a difference if there is only 15/16 decimal places in regards to precision....?
            {
                return (1 - c)*Pi/2.0;
            }

            return (Math.Atan(-c/Math.Sqrt(1 - c*c)) + 2*Math.Atan(1))*TenThousand;

            //return (Math.Abs(Math.Abs(c) - 1.0) < 1e-60
            //    ? (1 - c) * Pi / 2.0
            //    : Math.Atan(-c / Math.Sqrt(1 - c * c)) + 2 * Math.Atan(1)) * TenThousand;
        }

        private static double GetATan2(double atanX, double atanY, out double xyhyp2)
        {
            double atan4;

            xyhyp2 = Math.Sqrt((atanX*atanX*TenThousand) + (atanY*atanY*TenThousand));

            var angleRad4 = GetArcCos((atanX*OneMillion)/xyhyp2);

            if (atanY < 0)
                atan4 = -angleRad4;
            else
                atan4 = angleRad4;

            return atan4;
        }

        #endregion
    }
}