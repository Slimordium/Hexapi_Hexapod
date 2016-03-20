using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using HexapiBackground.Enums;

namespace HexapiBackground{
    internal sealed class InverseKinematics
    {
        private bool _movementStarted;
        private readonly SerialPort _serialPort;
        private readonly Stopwatch _sw = new Stopwatch();

        internal InverseKinematics()
        {
            for (var i = 0; i < 6; i++)
                LegServos[i] = new int[3];

            _movementStarted = false;

            _serialPort = new SerialPort("AI041V40", 38400, 200, 200); //UART0 Does not seem to be enabled for the PI 3 and Windows 10 IoT core

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _legPosX[legIndex] = _cInitPosX[legIndex]; //Set start positions for each leg
                _legPosY[legIndex] = _cInitPosY[legIndex];
                _legPosZ[legIndex] = _cInitPosZ[legIndex];
            }

            LoadLegDefaults();
        }

        #region Request movement
        internal void RequestMovement(double nominalGaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            _nominalGaitSpeed = nominalGaitSpeed; 
            _travelLengthX = travelLengthX;
            _travelLengthZ = travelLengthZ;
            _travelRotationY = travelRotationY;
        }

        internal void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ, double bodyPosY)
        {
            _bodyRotX1 = bodyRotX1;
            _bodyRotZ1 = bodyRotZ1;

            _bodyPosX = bodyPosX;
            _bodyPosZ = bodyPosZ;
            _bodyPosY = bodyPosY;
        }

        internal void RequestSetGaitOptions(double nominalGaitSpeed, double legLiftHeight)
        {
            _nominalGaitSpeed = nominalGaitSpeed;
            _legLiftHeight = legLiftHeight;
        }

        internal void RequestSetGaitType(GaitType gaitType)
        {
            _gaitType = gaitType;
            GaitSelect();
        }

        internal void RequestSetMovement(bool enabled)
        {
            if (!enabled)
                TurnOffServos();

            _movementStarted = enabled;
 
        }
        #endregion

        #region Main logic loop 
        internal void Start()
        {
            _gaitStep = 0;
            _nominalGaitSpeed = 60;
            _legLiftHeight = 30;
            _gaitType = GaitType.TripleTripod12Steps;
            _bodyPosY = 60;

            GaitSelect();
            _sw.Start();

            var startMs = _sw.ElapsedMilliseconds;

            while (true)
            {
                //Avc.CheckForObstructions(ref _travelLengthX, ref _travelRotationY, ref _travelLengthZ, ref _nominalGaitSpeed);

                if (!_movementStarted)
                {
                    Task.Delay(500).Wait();
                    continue;
                }

                _travelRequest = (Math.Abs(_travelLengthX) > TravelDeadZone) || 
                                 (Math.Abs(_travelLengthZ) > TravelDeadZone) ||
                                 (Math.Abs(_travelRotationY) > TravelDeadZone);

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
                                        _cOffsetX[legIndex], _cOffsetZ[legIndex],
                                        _bodyRotX1, _bodyRotZ1, _bodyRotY1, _cCoxaAngle1[legIndex]);

                    _coxaAngle1[legIndex] = angles[0];
                    _femurAngle1[legIndex] = angles[1];
                    _tibiaAngle1[legIndex] = angles[2];
                }

                var positions = UpdateServoPositions(_coxaAngle1, _femurAngle1, _tibiaAngle1);

                _serialPort.Write(positions);

                while (_sw.ElapsedMilliseconds < (startMs + _nominalGaitSpeed)) {  }
                //_sw.Restart();
                startMs = _sw.ElapsedMilliseconds;
            }
        }
        #endregion

        #region Inverse Kinematics setup
        private const double CPfConst = 592; //old 650 ; 900*(1000/cPwmDiv)+cPFConst must always be 1500
        private const double PwmDiv = 991; //old 1059, new 991;

        private const double TravelDeadZone = 1;

        private const double OneHundred = 100;
        private const double OneThousand = 1000;
        private const double TenThousand = 10000;
        private const double OneMillion = 1000000;

        private const int CLf = 5;
        private const int CLm = 4;
        private const int CLr = 3;
        private const int CRf = 2;
        private const int CRm = 1;
        private const int CRr = 0;

        //All legs being equal, all legs will have the same values
        private const double CoxaMin = -640; //-650 
        private const double CoxaMax = 640; //650
        private const double FemurMin = -640; //-1050
        private const double FemurMax = 640; //150
        private const double TibiaMin = -640; //-450
        private const double TibiaMax = 640; //350 I think this is the "down" angle limit, meaning how far in relation to the femur can it point towards the center of the bot

        private const double CRrCoxaAngle = -450;
        private const double CRmCoxaAngle = 0;
        private const double CRfCoxaAngle = 450;
        private const double CLrCoxaAngle = -450;
        private const double CLmCoxaAngle = 0;
        private const double CLfCoxaAngle = 450;

        private const double CRfOffsetX = -70;
        private const double CRfOffsetZ = -120;
        private const double CLfOffsetZ = -120;
        private const double CLfOffsetX = 70;
        private const double CRrOffsetZ = 120;
        private const double CRrOffsetX = -70;
        private const double CLrOffsetZ = 120;
        private const double CLrOffsetX = 70;
        private const double CRmOffsetX = -120;
        private const double CRmOffsetZ = 0;
        private const double CLmOffsetX = 120;
        private const double CLmOffsetZ = 0;

        private const double CoxaLength = 30; //30
        private const double FemurLength = 74; //60
        private const double TibiaLength = 139; //70

        private const double CHexInitXz = 105;
        private const double CHexInitXzCos45 = 74; // COS(45) = .7071
        private const double CHexInitXzSin45 = 84; // sin(45) = .7071
        private const double CHexInitY = 36;

        private const double CRfInitPosX = CHexInitXzCos45;
        private const double CRfInitPosY = CHexInitY;
        private const double CRfInitPosZ = -CHexInitXzSin45;
        private const double CLrInitPosX = CHexInitXzCos45;
        private const double CLrInitPosY = CHexInitY;
        private const double CLrInitPosZ = CHexInitXzCos45;
        private const double CLmInitPosX = CHexInitXz;
        private const double CLmInitPosY = CHexInitY;
        private const double CLmInitPosZ = 0;
        private const double CLfInitPosX = CHexInitXzCos45;
        private const double CLfInitPosY = CHexInitY;
        private const double CLfInitPosZ = -CHexInitXzSin45;
        private const double CRmInitPosX = CHexInitXz;
        private const double CRmInitPosY = CHexInitY;
        private const double CRmInitPosZ = 0;
        private const double CRrInitPosX = CHexInitXzCos45;
        private const double CRrInitPosY = CHexInitY;
        private const double CRrInitPosZ = CHexInitXzSin45;

        private readonly double[] _cInitPosX = { CRrInitPosX, CRmInitPosX, CRfInitPosX, CLrInitPosX, CLmInitPosX, CLfInitPosX };
        private readonly double[] _cInitPosY = { CRrInitPosY, CRmInitPosY, CRfInitPosY, CLrInitPosY, CLmInitPosY, CLfInitPosY };
        private readonly double[] _cInitPosZ = { CRrInitPosZ, CRmInitPosZ, CRfInitPosZ, CLrInitPosZ, CLmInitPosZ, CLfInitPosZ };

        private readonly double[] _cOffsetX = { CRrOffsetX, CRmOffsetX, CRfOffsetX, CLrOffsetX, CLmOffsetX, CLfOffsetX };
        private readonly double[] _cOffsetZ = { CRrOffsetZ, CRmOffsetZ, CRfOffsetZ, CLrOffsetZ, CLmOffsetZ, CLfOffsetZ };

        private readonly double[] _cCoxaAngle1 = { CRrCoxaAngle, CRmCoxaAngle, CRfCoxaAngle, CLrCoxaAngle, CLmCoxaAngle, CLfCoxaAngle };

        private readonly double[] _coxaAngle1 = new double[6];
        private readonly double[] _femurAngle1 = new double[6]; //Actual Angle of the vertical hip, decimals = 1
        private readonly double[] _tibiaAngle1 = new double[6]; //Actual Angle of the knee, decimals = 1

        private readonly int[] _gaitLegNr = new int[6]; //Init position of the leg

        private double[] _gaitPosX = new double[6];//Array containing Relative X position corresponding to the Gait
        private double[] _gaitPosY = new double[6];//Array containing Relative Y position corresponding to the Gait
        private double[] _gaitPosZ = new double[6];//Array containing Relative Z position corresponding to the Gait
        private double[] _gaitRotY = new double[6];//Array containing Relative Y rotation corresponding to the Gait

        private readonly double[] _legPosX = new double[6]; //Actual X Position of the Leg should be length of 6
        private readonly double[] _legPosY = new double[6]; //Actual Y Position of the Leg
        private readonly double[] _legPosZ = new double[6]; //Actual Z Position of the Leg

        private static volatile int _lastLeg; //TRUE when the current leg is the last leg of the sequence

        private int _liftDivFactor; //Normaly: 2, when NrLiftedPos=5: 4
        private int _numberOfLiftedPositions; //Number of positions that a single leg is lifted [1-3]
        private int _stepsInGait; //Number of steps in gait
        private int _tlDivFactor; //Number of steps that a leg is on the floor while walking
        private bool _travelRequest; //Temp to check if the gait is in motion

        private double _bodyPosX; //Global Input for the position of the body
        private double _bodyPosY = 60; //Controls height of the body from the ground
        private double _bodyPosZ;

        private double _bodyRotX1; //Global Input pitch of the body
        private double _bodyRotY1; //Global Input rotation of the body
        private double _bodyRotZ1; //Global Input roll of the body

        private int _halfLiftHeight; //If TRUE the outer positions of the ligted legs will be half height    
        private double _legLiftHeight; //Current Travel height

        private static volatile int _gaitStep;
        private GaitType _gaitType;
        private static double _nominalGaitSpeed = 40; //Nominal speed of the gait in MS

        private double _travelLengthX; //Current Travel length X
        private double _travelLengthZ; //Current Travel length Z - Negative numbers = "forward" movement.
        private double _travelRotationY; //Current Travel Rotation Y

        private static readonly int[][] LegServos = new int[6][];

        #endregion

        #region Gait calculations and logic
        public void GaitSelect()
        {
            switch (_gaitType)
            {
                case GaitType.RippleGait12Steps:
                    //Ripple Gait 12 steps
                    _gaitLegNr[CLr] = 1;
                    _gaitLegNr[CRf] = 3;
                    _gaitLegNr[CLm] = 5;
                    _gaitLegNr[CRr] = 7;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 11;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    //_nominalGaitSpeed = 110;
                    break;
                case GaitType.Tripod8Steps:
                    //Tripod 8 steps
                    _gaitLegNr[CLr] = 5;
                    _gaitLegNr[CRf] = 1;
                    _gaitLegNr[CLm] = 1;
                    _gaitLegNr[CRr] = 1;
                    _gaitLegNr[CLf] = 5;
                    _gaitLegNr[CRm] = 5;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 4;
                    _stepsInGait = 8;
                    //_nominalGaitSpeed = 80;
                    break;
                case GaitType.TripleTripod12Steps:
                    //Triple Tripod 12 step
                    _gaitLegNr[CRf] = 3;
                    _gaitLegNr[CLm] = 4;
                    _gaitLegNr[CRr] = 5;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 10;
                    _gaitLegNr[CLr] = 11;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    //_nominalGaitSpeed = 100;
                    break;
                case GaitType.TripleTripod16Steps:
                    // Triple Tripod 16 steps, use 5 lifted positions
                    _gaitLegNr[CRf] = 4;
                    _gaitLegNr[CLm] = 5;
                    _gaitLegNr[CRr] = 6;
                    _gaitLegNr[CLf] = 12;
                    _gaitLegNr[CRm] = 13;
                    _gaitLegNr[CLr] = 14;

                    _numberOfLiftedPositions = 5;
                    _halfLiftHeight = 1;
                    _tlDivFactor = 10;
                    _stepsInGait = 16;
                    //_nominalGaitSpeed = 100;
                    break;
                case GaitType.Wave24Steps:
                    //Wave 24 steps
                    _gaitLegNr[CLr] = 1;
                    _gaitLegNr[CRf] = 21;
                    _gaitLegNr[CLm] = 5;

                    _gaitLegNr[CRr] = 13;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 17;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 20;
                    _stepsInGait = 24;
                    //_nominalGaitSpeed = 110;
                    break;
            }
        }

        private static double[][] Gait(int legIndex, bool travelRequest, double travelLengthX, double travelLengthZ, double travelRotationY,
                                    double[] gaitPosX, double[] gaitPosY, double[] gaitPosZ, double[] gaitRotY,
                                    int numberOfLiftedPositions, int gaitLegNr, 
                                    double legLiftHeight, int liftDivFactor, double halfLiftHeight, int stepsInGait, int tlDivFactor)
        {
            var gaitXyZrotY = new double[4][];

            //Leg middle up position
            //Gait in motion														  									
            //Gait NOT in motion, return to home position
            if ((travelRequest && (numberOfLiftedPositions == 1 || numberOfLiftedPositions == 3 || numberOfLiftedPositions == 5) && _gaitStep == gaitLegNr) || (!travelRequest && _gaitStep == gaitLegNr && ((Math.Abs(gaitPosX[legIndex]) > 2) || (Math.Abs(gaitPosZ[legIndex]) > 2) || (Math.Abs(gaitRotY[legIndex]) > 2))))
            {
                //Up
                gaitPosX[legIndex] = 0;
                gaitPosY[legIndex] = -legLiftHeight;
                gaitPosZ[legIndex] = 0;
                gaitRotY[legIndex] = 0;
            }
            //Optional Half height Rear (2, 3, 5 lifted positions)
            else if (((numberOfLiftedPositions == 2 && _gaitStep == gaitLegNr) ||
                      (numberOfLiftedPositions >= 3 &&
                       (_gaitStep == gaitLegNr - 1 || _gaitStep == gaitLegNr + (stepsInGait - 1)))) &&
                     travelRequest)
            {
                gaitPosX[legIndex] = -travelLengthX / liftDivFactor;
                gaitPosY[legIndex] = -3 * legLiftHeight / (3 + halfLiftHeight);
                //Easier to shift between div factor: /1 (3/3), /2 (3/6) and 3/4
                gaitPosZ[legIndex] = -travelLengthZ / liftDivFactor;
                gaitRotY[legIndex] = -travelRotationY / liftDivFactor;
            }

            // Optional Half height front (2, 3, 5 lifted positions)
            else if ((numberOfLiftedPositions >= 2) &&
                     (_gaitStep == gaitLegNr + 1 || _gaitStep == gaitLegNr - (stepsInGait - 1)) &&
                     travelRequest)
            {
                gaitPosX[legIndex] = travelLengthX / liftDivFactor;
                gaitPosY[legIndex] = -3 * legLiftHeight / (3 + halfLiftHeight);
                // Easier to shift between div factor: /1 (3/3), /2 (3/6) and 3/4
                gaitPosZ[legIndex] = travelLengthZ / liftDivFactor;
                gaitRotY[legIndex] = travelRotationY / liftDivFactor;
            }

            //Optional Half heigth Rear 5 LiftedPos (5 lifted positions)
            else if (((numberOfLiftedPositions == 5 && (_gaitStep == gaitLegNr - 2))) && travelRequest)
            {
                gaitPosX[legIndex] = -travelLengthX / 2;
                gaitPosY[legIndex] = -legLiftHeight / 2;
                gaitPosZ[legIndex] = -travelLengthZ / 2;
                gaitRotY[legIndex] = -travelRotationY / 2;
            }

            //Optional Half heigth Front 5 LiftedPos (5 lifted positions)
            else if ((numberOfLiftedPositions == 5) &&
                     (_gaitStep == gaitLegNr + 2 || _gaitStep == gaitLegNr - (stepsInGait - 2)) &&
                     travelRequest)
            {
                gaitPosX[legIndex] = travelLengthX / 2;
                gaitPosY[legIndex] = -legLiftHeight / 2;
                gaitPosZ[legIndex] = travelLengthZ / 2;
                gaitRotY[legIndex] = travelRotationY / 2;
            }

            //Leg front down position
            else if ((_gaitStep == gaitLegNr + numberOfLiftedPositions ||
                      _gaitStep == gaitLegNr - (stepsInGait - numberOfLiftedPositions)) &&
                     gaitPosY[legIndex] < 0)
            {
                gaitPosX[legIndex] = travelLengthX / 2;
                gaitPosZ[legIndex] = travelLengthZ / 2;
                gaitRotY[legIndex] = travelRotationY / 2;
                gaitPosY[legIndex] = 0;
                //Only move leg down at once if terrain adaption is turned off
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

            //Advance to the next step
            if (_lastLeg != 1)
                return gaitXyZrotY;

            //The last leg in this step
            _gaitStep = _gaitStep + 1;
            if (_gaitStep > stepsInGait)
                _gaitStep = 1;

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
                                            double cCoxaAngle1)
        {
            var posX = 0d;
            if (legIndex <= 2)
                posX = -legPosX + bodyPosX + gaitPosX;
            else
                posX = legPosX - bodyPosX + gaitPosX;

            var posY = legPosY + bodyPosY + gaitPosY;
            var posZ = legPosZ + bodyPosZ + gaitPosZ;

            double sinA4; //Sin buffer for BodyRotX calculations
            double cosA4; //Cos buffer for BodyRotX calculations
            double sinB4; //Sin buffer for BodyRotX calculations
            double cosB4; //Cos buffer for BodyRotX calculations
            double sinG4; //Sin buffer for BodyRotZ calculations
            double cosG4; //Cos buffer for BodyRotZ calculations

            //Calculating totals from center of the body to the feet 
            var cprX = cOffsetX + posX;
            var cprZ = cOffsetZ + posZ;

            //Successive global rotation matrix: 
            //Math shorts for rotation: Alfa [A] = Xrotate, Beta [B] = Zrotate, Gamma [G] = Yrotate 
            //Sinus Alfa = SinA, cosinus Alfa = cosA. and so on... 

            //First calculate sinus and cosinus for each rotation: 
            GetSinCos(bodyRotX1, out sinG4, out cosG4);

            GetSinCos(bodyRotZ1, out sinB4, out cosB4);

            GetSinCos(bodyRotY1 + (gaitRotY * 10), out sinA4, out cosA4);

            //Calculation of rotation matrix: 
            var bodyFkPosX = (cprX * OneHundred -
                          ((cprX * OneHundred * cosA4 / TenThousand * cosB4 / TenThousand) - (cprZ * OneHundred * cosB4 / TenThousand * sinA4 / TenThousand) +
                           (posY * OneHundred * sinB4 / TenThousand))) / OneHundred;
            var bodyFkPosZ = (cprZ * OneHundred -
                          ((cprX * OneHundred * cosG4 / TenThousand * sinA4 / TenThousand) + (cprX * OneHundred * cosA4 / TenThousand * sinB4 / TenThousand * sinG4 / TenThousand) +
                           (cprZ * OneHundred * cosA4 / TenThousand * cosG4 / TenThousand) - (cprZ * OneHundred * sinA4 / TenThousand * sinB4 / TenThousand * sinG4 / TenThousand) -
                           (posY * OneHundred * cosB4 / TenThousand * sinG4 / TenThousand))) / OneHundred;
            var bodyFkPosY = (posY * OneHundred -
                          ((cprX * OneHundred * sinA4 / TenThousand * sinG4 / TenThousand) - (cprX * OneHundred * cosA4 / TenThousand * cosG4 / TenThousand * sinB4 / TenThousand) +
                           (cprZ * OneHundred * cosA4 / TenThousand * sinG4 / TenThousand) + (cprZ * OneHundred * cosG4 / TenThousand * sinA4 / TenThousand * sinB4 / TenThousand) +
                           (posY * OneHundred * cosB4 / TenThousand * cosG4 / TenThousand))) / OneHundred;

            var coxaFemurTibiaAngle = new double[3];

            var ikFeetPosX = 0d;
            if (legIndex <= 2)
                ikFeetPosX = legPosX - bodyPosX + bodyFkPosX - gaitPosX;
            else
                ikFeetPosX = legPosX + bodyPosX - bodyFkPosX + gaitPosX;

            var ikFeetPosY = legPosY + bodyPosY - bodyFkPosY + gaitPosY;
            var ikFeetPosZ = legPosZ + bodyPosZ - bodyFkPosZ + gaitPosZ;

            double xyhyp2;

            //Calculate IKCoxaAngle and IKFeetPosXZ
            var getatan = GetATan2(ikFeetPosX, ikFeetPosZ, out xyhyp2);

            coxaFemurTibiaAngle[0] = ((getatan * 180) / 3141) + cCoxaAngle1;

            var ikFeetPosXz = xyhyp2 / OneHundred;
            var ika14 = GetATan2(ikFeetPosY, ikFeetPosXz - CoxaLength, out xyhyp2);
            var iksw2 = xyhyp2;
            var temp1 = (((FemurLength * FemurLength) - (TibiaLength * TibiaLength)) * TenThousand + (iksw2 * iksw2));
            var temp2 = 2 * FemurLength * OneHundred * iksw2;
            var ika24 = GetArcCos(temp1 / (temp2 / TenThousand));

            coxaFemurTibiaAngle[1] = -(ika14 + ika24) * 180 / 3141 + 900;

            temp1 = (((FemurLength * FemurLength) + (TibiaLength * TibiaLength)) * TenThousand - (iksw2 * iksw2));
            temp2 = (2 * FemurLength * TibiaLength);

            coxaFemurTibiaAngle[2] = -(900 - GetArcCos(temp1 / temp2) * 180 / 3141);

            return coxaFemurTibiaAngle;
        }
        #endregion

        private static readonly StringBuilder _stringBuilder = new StringBuilder();

        #region Servo related, build various servo controller strings and read values
        private static string UpdateServoPositions(double[] coxaAngles, double[] femurAngles, double[] tibiaAngles)
        {
            _stringBuilder.Clear();

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
                    coxaPosition = Math.Round((-coxaAngles[legIndex] + 900) * 1000 / PwmDiv + CPfConst);
                    femurPosition = Math.Round((-femurAngles[legIndex] + 900) * 1000 / PwmDiv + CPfConst);
                    tibiaPosition = Math.Round((-tibiaAngles[legIndex] + 900) * 1000 / PwmDiv + CPfConst);
                }
                else
                {
                    coxaPosition = Math.Round((coxaAngles[legIndex] + 900) * 1000 / PwmDiv + CPfConst);
                    femurPosition = Math.Round((femurAngles[legIndex] + 900) * 1000 / PwmDiv + CPfConst);
                    tibiaPosition = Math.Round((tibiaAngles[legIndex] + 900) * 1000 / PwmDiv + CPfConst);
                }

                _stringBuilder.Append($"#{LegServos[legIndex][0]}P{coxaPosition}");
                _stringBuilder.Append($"#{LegServos[legIndex][1]}P{femurPosition}");
                _stringBuilder.Append($"#{LegServos[legIndex][2]}P{tibiaPosition}");
            }

            _stringBuilder.Append($"T{_nominalGaitSpeed}\r");

            return _stringBuilder.ToString();
        }

        private void TurnOffServos()
        {
            var stringBuilder = new StringBuilder();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                stringBuilder.Append($"#{LegServos[legIndex][0]}P0");
                stringBuilder.Append($"#{LegServos[legIndex][1]}P0");
                stringBuilder.Append($"#{LegServos[legIndex][2]}P0");
            }

            stringBuilder.Append($"T0\r");

            _serialPort.Write(stringBuilder.ToString());
        }

        public async void LoadLegDefaults()
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

            sin = Math.Sin(angle) * TenThousand;
            cos = Math.Cos(angle) * TenThousand;
        }

        private static double GetArcCos(double cos)
        {
            var c = cos / TenThousand; //Wont work right unless you do / 10000 then * 10000
            return (Math.Abs(Math.Abs(c) - 1.0) < .001
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