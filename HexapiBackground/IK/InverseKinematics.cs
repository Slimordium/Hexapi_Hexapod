using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;

namespace HexapiBackground.IK{
    /// <summary>
    /// This is a port of the "Phoenix" 3DOF Hexapod code in C#. Uses CH3-R body from Lynxmotion/robotshop.com
    /// https://github.com/KurtE/Arduino_Phoenix_Parts/tree/master/Phoenix
    /// http://www.robotshop.com/en/lynxmotion-aexapod-ch3-r-combo-kit-body-frame.html
    /// </summary>
    internal sealed class InverseKinematics
    {
        private bool _movementStarted;
        internal static SerialPort SerialPort { get; private set; }
        private readonly Stopwatch _sw = new Stopwatch();
        private static readonly StringBuilder StringBuilder = new StringBuilder();

        internal InverseKinematics()
        {
            _pi1K = Math.PI*1000;

            for (var i = 0; i < 6; i++)
                LegServos[i] = new int[3];

            _movementStarted = false;

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _legPosX[legIndex] = _initPosX[legIndex]; //Set start positions for each leg
                _legPosY[legIndex] = _initPosY[legIndex];
                _legPosZ[legIndex] = _initPosZ[legIndex];
            }

            LoadLegDefaults();

            GaitSelect();
        }

        #region Request movement
        internal void RequestMovement(double gaitSpeed,double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            _gaitSpeedInMs = gaitSpeed; 
            _travelLengthX = travelLengthX;
            _travelLengthZ = travelLengthZ;
            _travelRotationY = travelRotationY;
        }

        internal void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ, double bodyPosY, double bodyRotY1)
        {
            _bodyRotX1 = bodyRotX1;
            _bodyRotZ1 = bodyRotZ1;

            _bodyPosX = bodyPosX;
            _bodyPosZ = bodyPosZ;
            _bodyPosY = bodyPosY;
            _bodyRotY1 = bodyRotY1; //body rotation
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
        }

        internal void RequestSetMovement(bool enabled)
        {
            _movementStarted = enabled;

            if (!enabled)
            {
                Task.Delay(500).Wait();
                TurnOffServos();
            }
        }
        #endregion

        #region Main logic loop 
        internal void Start()
        {
            SerialPort = new SerialPort("BCM2836", 115200, 200, 200); //BCM2836 = Onboard UART on PI3 and IoT Core build 14322 (AI041V40A is the USB/Serial dongle)

            Task.Factory.StartNew(() =>
            {
                _sw.Start();
                var startMs = _sw.ElapsedMilliseconds;

                while (true)
                {
                    //_avc.CheckForObstructions(ref _travelLengthX, ref _travelRotationY, ref _travelLengthZ, ref _nominalGaitSpeed);

                    if (!_movementStarted)
                    {
                        //SerialPort.Write("VH\r");
                        //var r = SerialPort.ReadByte().Result;

                        //var level = MathHelpers.Map(r, 255, 0, 0, 255);

                        //if (level > 5)
                        //    Debug.WriteLine("Level " + level);

                        while (_sw.ElapsedMilliseconds < (startMs + 10)) { }
                        startMs = _sw.ElapsedMilliseconds;
                        continue;
                    }

                    _travelRequest = (Math.Abs(_travelLengthX) > TravelDeadZone) ||
                                     (Math.Abs(_travelLengthZ) > TravelDeadZone) ||
                                     (Math.Abs(_travelRotationY) > TravelDeadZone);

                    //Only switch the gait after the previous one is complete
                    if (_gaitStep == 1 && _lastGaitType != _gaitType)
                        GaitSelect();

                    _liftDivFactor = _numberOfLiftedPositions == 5 ? 4 : 2;

                    _lastLeg = 0;

                    for (var legIndex = 0; legIndex < 6; legIndex++)
                    {
                        if (legIndex == 5)
                            _lastLeg = 1;

                        var gaitPosXyZrotY = Gait(legIndex, _travelRequest, _travelLengthX,
                                             _travelLengthZ, _travelRotationY,
                                             _gaitPosX, _gaitPosY, _gaitPosZ, _gaitRotY,
                                             _numberOfLiftedPositions, _gaitLegNr[legIndex],
                                             _legLiftHeight, _liftDivFactor, _halfLiftHeight, _stepsInGait, _tlDivFactor);

                        _gaitPosX = gaitPosXyZrotY[0];
                        _gaitPosY = gaitPosXyZrotY[1];
                        _gaitPosZ = gaitPosXyZrotY[2];
                        _gaitRotY = gaitPosXyZrotY[3];

                        var angles = BodyLegIk(legIndex,
                                            _legPosX[legIndex], _legPosY[legIndex], _legPosZ[legIndex],
                                            _bodyPosX, _bodyPosY, _bodyPosZ,
                                            _gaitPosX[legIndex], _gaitPosY[legIndex], _gaitPosZ[legIndex], _gaitRotY[legIndex],
                                            _offsetX[legIndex], _offsetZ[legIndex],
                                            _bodyRotX1, _bodyRotZ1, _bodyRotY1, _cCoxaAngle1[legIndex]);

                        _coxaAngle1[legIndex] = angles[0];
                        _femurAngle1[legIndex] = angles[1];
                        _tibiaAngle1[legIndex] = angles[2];
                    }

                    SerialPort.Write(UpdateServoPositions(_coxaAngle1, _femurAngle1, _tibiaAngle1));

                    while (_sw.ElapsedMilliseconds <= (startMs + _gaitSpeedInMs)) { }

                    startMs = _sw.ElapsedMilliseconds;
                }
            }, TaskCreationOptions.LongRunning);
        }
        #endregion

        #region Inverse Kinematics setup
        
        private const double PfConst = 592; //old 650 ; 900*(1000/PwmDiv)+cPFConst must always be 1500 was 592
        private const double PwmDiv = 991; //old 1059, new 991;

        private const double TravelDeadZone = 0;

        private const double TenThousand = 10000;
        private const double OneMillion = 1000000;

        private const int Lf = 5;
        private const int Lm = 4;
        private const int Lr = 3;
        private const int Rf = 2;
        private const int Rm = 1;
        private const int Rr = 0;

        private const double CoxaMin = -590; //
        private const double CoxaMax = 590; //
        private const double FemurMin = -590; //
        private const double FemurMax = 590; //
        private const double TibiaMin = -590; //
        private const double TibiaMax = 590; //I think this is the "down" angle limit, meaning how far in relation to the femur can it point towards the center of the bot

        private const double RrCoxaAngle = -450; //45 degrees
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

        private const double CoxaLength = 33; //mm
        private const double FemurLength = 70; //mm
        private const double TibiaLength = 130; //mm

        //Foot start positions
        private const double HexInitXz = CoxaLength + FemurLength;
        private const double HexInitXzCos45 = HexInitXz * .7071; //http://www.math.com/tables/trig/tables.htm
        private const double HexInitXzSin45 = HexInitXz * .7071; 
        private const double HexInitY = 70; 
         
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

        private readonly double[] _cCoxaAngle1 = { RrCoxaAngle, RmCoxaAngle, RfCoxaAngle, LrCoxaAngle, LmCoxaAngle, LfCoxaAngle };

        private readonly double[] _coxaAngle1 = new double[6];
        private readonly double[] _femurAngle1 = new double[6]; //Actual Angle of the vertical hip, decimals = 1
        private readonly double[] _tibiaAngle1 = new double[6]; //Actual Angle of the knee, decimals = 1

        private readonly int[] _gaitLegNr = new int[6]; //Initial position of the leg

        private double[] _gaitPosX = new double[6];//Array containing Relative X position corresponding to the Gait
        private double[] _gaitPosY = new double[6];//Array containing Relative Y position corresponding to the Gait
        private double[] _gaitPosZ = new double[6];//Array containing Relative Z position corresponding to the Gait
        private double[] _gaitRotY = new double[6];//Array containing Relative Y rotation corresponding to the Gait

        private readonly double[] _legPosX = new double[6]; //Actual X Position of the Leg 
        private readonly double[] _legPosY = new double[6]; //Actual Y Position of the Leg
        private readonly double[] _legPosZ = new double[6]; //Actual Z Position of the Leg

        private static int _lastLeg; //true = the current leg is the last leg of the sequence

        private int _liftDivFactor; //Normaly: 2, when NrLiftedPos=5: 4
        private int _numberOfLiftedPositions; //Number of positions that a single leg is lifted [1-3]
        private int _stepsInGait; //Number of steps in gait
        private int _tlDivFactor; //Number of steps that a leg is on the floor while walking
        private bool _travelRequest; //Temp to check if the gait is in motion

        private double _bodyPosX; //Global Input for the position of the body
        private double _bodyPosY; //Controls height of the body from the ground
        private double _bodyPosZ;

        private double _bodyRotX1; //Global Input pitch of the body
        private double _bodyRotY1; //Global Input rotation of the body
        private double _bodyRotZ1; //Global Input roll of the body

        private int _halfLiftHeight; //If true the outer positions of the ligted legs will be half height    
        private double _legLiftHeight; //Current Travel height

        private static int _gaitStep = 1;
        private GaitType _gaitType = GaitType.TripleTripod12Steps;
        private GaitType _lastGaitType = GaitType.TripleTripod12Steps;
        private static double _gaitSpeedInMs = 50; //Nominal speed of the gait in ms

        private double _travelLengthX; //Current Travel length X - Left/Right
        private double _travelLengthZ; //Current Travel length Z - Negative numbers = "forward" movement.
        private double _travelRotationY; //Current Travel Rotation Y 

        private static readonly int[][] LegServos = new int[6][];

        private static double _pi1K;

        #endregion

        #region Gait calculations and logic
        public void GaitSelect()
        {
            switch (_gaitType)
            {
                case GaitType.RippleGait12Steps:
                    _gaitLegNr[Lr] = 1;
                    _gaitLegNr[Rf] = 3;
                    _gaitLegNr[Lm] = 5;
                    _gaitLegNr[Rr] = 7;
                    _gaitLegNr[Lf] = 9;
                    _gaitLegNr[Rm] = 11;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    break;
                case GaitType.Tripod8Steps:
                    _gaitLegNr[Lr] = 5;
                    _gaitLegNr[Rf] = 1;
                    _gaitLegNr[Lm] = 1;
                    _gaitLegNr[Rr] = 1;
                    _gaitLegNr[Lf] = 5;
                    _gaitLegNr[Rm] = 5;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 4;
                    _stepsInGait = 8;
                    break;
                case GaitType.TripleTripod12Steps:
                    _gaitLegNr[Rf] = 3;
                    _gaitLegNr[Lm] = 4;
                    _gaitLegNr[Rr] = 5;
                    _gaitLegNr[Lf] = 9;
                    _gaitLegNr[Rm] = 10;
                    _gaitLegNr[Lr] = 11;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    break;
                case GaitType.TripleTripod16Steps:
                    _gaitLegNr[Rf] = 4;
                    _gaitLegNr[Lm] = 5;
                    _gaitLegNr[Rr] = 6;
                    _gaitLegNr[Lf] = 12;
                    _gaitLegNr[Rm] = 13;
                    _gaitLegNr[Lr] = 14;

                    _numberOfLiftedPositions = 5;
                    _halfLiftHeight = 1;
                    _tlDivFactor = 10;
                    _stepsInGait = 16;
                    break;
                case GaitType.Wave24Steps:
                    _gaitLegNr[Lr] = 1;
                    _gaitLegNr[Rf] = 21;
                    _gaitLegNr[Lm] = 5;

                    _gaitLegNr[Rr] = 13;
                    _gaitLegNr[Lf] = 9;
                    _gaitLegNr[Rm] = 17;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 20;
                    _stepsInGait = 24;
                    break;
            }
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
                gaitPosX[legIndex] = -travelLengthX / liftDivFactor;
                gaitPosY[legIndex] = -3 * legLiftHeight / (3 + halfLiftHeight);
                gaitPosZ[legIndex] = -travelLengthZ / liftDivFactor;
                gaitRotY[legIndex] = -travelRotationY / liftDivFactor;
            }
            // Optional Half height front (2, 3, 5 lifted positions)
            else if (travelRequest && 
                    (numberOfLiftedPositions >= 2) &&
                    (_gaitStep == gaitLegNr + 1 || _gaitStep == gaitLegNr - (stepsInGait - 1)))
            {
                gaitPosX[legIndex] = travelLengthX / liftDivFactor;
                gaitPosY[legIndex] = -3 * legLiftHeight / (3 + halfLiftHeight);
                gaitPosZ[legIndex] = travelLengthZ / liftDivFactor;
                gaitRotY[legIndex] = travelRotationY / liftDivFactor;
            }
            //Optional Half heigth Rear 5 LiftedPos (5 lifted positions)
            else if (travelRequest && 
                    ((numberOfLiftedPositions == 5 && (_gaitStep == gaitLegNr - 2))))
            {
                gaitPosX[legIndex] = -travelLengthX / 2;
                gaitPosY[legIndex] = -legLiftHeight / 2;
                gaitPosZ[legIndex] = -travelLengthZ / 2;
                gaitRotY[legIndex] = -travelRotationY / 2;
            }
            //Optional Half heigth Front 5 LiftedPos (5 lifted positions)
            else if (travelRequest && 
                    (numberOfLiftedPositions == 5) &&
                    (_gaitStep == gaitLegNr + 2 || _gaitStep == gaitLegNr - (stepsInGait - 2)))
            {
                gaitPosX[legIndex] = travelLengthX / 2;
                gaitPosY[legIndex] = -legLiftHeight / 2;
                gaitPosZ[legIndex] = travelLengthZ / 2;
                gaitRotY[legIndex] = travelRotationY / 2;
            }
            //Leg front down position
            else if ((_gaitStep == gaitLegNr + numberOfLiftedPositions || _gaitStep == gaitLegNr - (stepsInGait - numberOfLiftedPositions)) &&
                     gaitPosY[legIndex] < 0)
            {
                gaitPosX[legIndex] = travelLengthX / 2;
                gaitPosZ[legIndex] = travelLengthZ / 2;
                gaitRotY[legIndex] = travelRotationY / 2;
                gaitPosY[legIndex] = 0;
            }
            //Move body forward      
            else
            {
                gaitPosX[legIndex] = gaitPosX[legIndex] - (travelLengthX / tlDivFactor);
                gaitPosY[legIndex] = 0;
                gaitPosZ[legIndex] = gaitPosZ[legIndex] - (travelLengthZ / tlDivFactor);
                gaitRotY[legIndex] = gaitRotY[legIndex] - (travelRotationY / tlDivFactor);
            }

            gaitXyZrotY[0] = gaitPosX;
            gaitXyZrotY[1] = gaitPosY;
            gaitXyZrotY[2] = gaitPosZ;
            gaitXyZrotY[3] = gaitRotY;

            if (_lastLeg != 1)
                return gaitXyZrotY;

            //The last leg in this step
            _gaitStep = _gaitStep + 1;
            if (_gaitStep > stepsInGait)
            {
                _gaitStep = 1;
            }

            return gaitXyZrotY;
        }

        #endregion

        #region Body and Leg Inverse Kinematics
        private static double[] BodyLegIk(int legIndex, 
                                            double legPosX, double legPosY, double legPosZ, 
                                            double bodyPosX, double bodyPosY, double bodyPosZ, 
                                            double gaitPosX, double gaitPosY, double gaitPosZ, double gaitRotY, 
                                            double cOffsetX, double cOffsetZ,
                                            double bodyRotX1, double bodyRotZ1, double bodyRotY1,
                                            double coxaAngle)
        {
            var posX = 0D;
            if (legIndex <= 2)
                posX = -legPosX + bodyPosX + gaitPosX;
            else
                posX = legPosX - bodyPosX + gaitPosX;

            var posY = legPosY + bodyPosY + gaitPosY;
            var posZ = legPosZ + bodyPosZ + gaitPosZ;

            double sinA; //Sin buffer for BodyRotX calculations
            double cosA; //Cos buffer for BodyRotX calculations
            double sinB; //Sin buffer for BodyRotX calculations
            double cosB; //Cos buffer for BodyRotX calculations
            double sinG; //Sin buffer for BodyRotZ calculations
            double cosG; //Cos buffer for BodyRotZ calculations

            //Calculating totals from center of the body to the feet 
            var cprX = (cOffsetX + posX) * 100;
            var cprZ = (cOffsetZ + posZ) * 100;

            posY = posY * 100;

            //Math shorts for rotation: Alfa [A] = Xrotate, Beta [B] = Zrotate, Gamma [G] = Yrotate 
            //Sinus Alfa = SinA, cosinus Alfa = cosA. and so on... 

            GetSinCos(bodyRotY1 + (gaitRotY * 10), out sinA, out cosA);
            GetSinCos(bodyRotZ1, out sinB, out cosB);
            GetSinCos(bodyRotX1, out sinG, out cosG);

            //Calculation of rotation matrix: 
            var bodyFkPosX = (cprX -
                          ((cprX * cosA * cosB) - 
                           (cprZ * cosB * sinA) +
                           (posY * sinB))) / 100;

            var bodyFkPosZ = (cprZ -
                          ((cprX * cosG * sinA) + 
                           (cprX * cosA * sinB * sinG) +
                           (cprZ * cosA * cosG) - 
                           (cprZ * sinA * sinB * sinG) -
                           (posY * cosB * sinG))) / 100;

            var bodyFkPosY = (posY -
                          ((cprX * sinA * sinG) - 
                           (cprX * cosA * cosG * sinB) +
                           (cprZ * cosA * sinG) + 
                           (cprZ * cosG * sinA * sinB) +
                           (posY * cosB * cosG))) / 100;

            var coxaFemurTibiaAngle = new double[3];

            var ikFeetPosX = 0D;
            if (legIndex <= 2)
                ikFeetPosX = legPosX - bodyPosX + bodyFkPosX - gaitPosX;
            else
                ikFeetPosX = legPosX + bodyPosX - bodyFkPosX + gaitPosX;

            var ikFeetPosY = legPosY + bodyPosY - bodyFkPosY + gaitPosY;
            var ikFeetPosZ = legPosZ + bodyPosZ - bodyFkPosZ + gaitPosZ;

            double xyhyp2;
            var getatan = GetATan2(ikFeetPosX, ikFeetPosZ, out xyhyp2);

            coxaFemurTibiaAngle[0] = ((getatan * 180) / _pi1K) + coxaAngle;

            var ikFeetPosXz = xyhyp2 / 100;
            var ika14 = GetATan2(ikFeetPosY, ikFeetPosXz - CoxaLength, out xyhyp2);
            var ika24 = GetArcCos((((FemurLength * FemurLength) - (TibiaLength * TibiaLength)) * TenThousand + (xyhyp2 * xyhyp2)) / ((2 * FemurLength * 100 * xyhyp2) / TenThousand));

            coxaFemurTibiaAngle[1] = -(ika14 + ika24) * 180 / _pi1K + 900;

            coxaFemurTibiaAngle[2] = -(900 - GetArcCos((((FemurLength * FemurLength) + (TibiaLength * TibiaLength)) * TenThousand - (xyhyp2 * xyhyp2)) / (2 * FemurLength * TibiaLength)) * 180 / _pi1K);

            return coxaFemurTibiaAngle;
        }
        #endregion

        #region Servo related, build various servo controller strings and read values
        private static string UpdateServoPositions(IList<double> coxaAngles, IList<double> femurAngles, IList<double> tibiaAngles)
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
                    coxaPosition = Math.Round((-coxaAngles[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    femurPosition = Math.Round((-femurAngles[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    tibiaPosition = Math.Round((-tibiaAngles[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                }
                else
                {
                    coxaPosition = Math.Round((coxaAngles[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    femurPosition = Math.Round((femurAngles[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    tibiaPosition = Math.Round((tibiaAngles[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                }

                StringBuilder.Append($"#{LegServos[legIndex][0]}P{coxaPosition}");
                StringBuilder.Append($"#{LegServos[legIndex][1]}P{femurPosition}");
                StringBuilder.Append($"#{LegServos[legIndex][2]}P{tibiaPosition}");
            }

            StringBuilder.Append($"T{_gaitSpeedInMs}\r");

            return StringBuilder.ToString();
        }

        private static void TurnOffServos()
        {
            StringBuilder.Clear();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                StringBuilder.Append($"#{LegServos[legIndex][0]}P0");
                StringBuilder.Append($"#{LegServos[legIndex][1]}P0");
                StringBuilder.Append($"#{LegServos[legIndex][2]}P0");
            }

            StringBuilder.Append("T0\r");

            SerialPort.Write(StringBuilder.ToString());
        }

        private static async void LoadLegDefaults()
        {
            var config = await FileHelpers.ReadStringFromFile("hexapod.config");
            
            if (string.IsNullOrEmpty(config))
            {
                Debug.WriteLine("Empty config file. hexapod.config");
                return;
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
                Debug.WriteLine(e);
            }
        }
        #endregion

        #region MathHelpers, and static methods
        private static void GetSinCos(double angleDeg, out double sin, out double cos)
        {
            var angle = Math.PI * angleDeg / 180.0;

            sin = Math.Sin(angle);
            cos = Math.Cos(angle);
        }

        private static double GetArcCos(double cos)
        {
            var c = cos / TenThousand; 
            return (Math.Abs(Math.Abs(c) - 1.0) < .00000000000001
                ? (1 - c) * Math.PI / 2.0
                : Math.Atan(-c / Math.Sqrt(1 - c * c)) + 2 * Math.Atan(1)) * TenThousand;
        }

        private static double GetATan2(double atanX, double atanY, out double xyhyp2)
        {
            double atan4;

            xyhyp2 = Math.Sqrt((atanX * atanX * TenThousand) + (atanY * atanY * TenThousand));

            var angleRad4 = GetArcCos((atanX * OneMillion) / xyhyp2);

            if (atanY < 0)
                atan4 = -angleRad4;
            else
                atan4 = angleRad4;

            return atan4;
        }

        #endregion
    }
}