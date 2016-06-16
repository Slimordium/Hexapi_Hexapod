using System;
using System.Diagnostics;
using Windows.Devices.Gpio;
using HexapiBackground.Enums;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;
using HexapiBackground.Navigation;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground{
    internal sealed class Hexapi{
        private readonly Gps.Gps _gps;
        private readonly InverseKinematics _ik;

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly XboxController _xboxController;
        private double _bodyPosX;

        private double _bodyPosY; //45
        private double _bodyPosZ;
        private double _bodyRotX;
        private double _bodyRotY;
        private double _bodyRotZ;
        private double _gaitSpeed;

        private GaitType _gaitType = GaitType.TripleTripod16Steps;

        private bool _isMovementStarted;

        private double _legLiftHeight;
        private double _legPosY;

        private Mpr121 _mpr121;

        private int _posture;

        private RouteFinder _routeFinder;
        private SelectedFunction _selectedFunction = SelectedFunction.GaitSpeed;

        private int _selectedLeg;

        private double _travelLengthX;
        private double _travelLengthZ;
        private double _travelRotationY;

        internal Hexapi(InverseKinematics inverseKinematics = null, Gps.Gps gps = null, RouteFinder routeFinder = null)
        {
            _gps = gps;
            _ik = inverseKinematics;
            _routeFinder = routeFinder;

            _xboxController = new XboxController();
            _xboxController.Open();

            _xboxController.LeftDirectionChanged += XboxController_LeftDirectionChanged;
            _xboxController.RightDirectionChanged += XboxController_RightDirectionChanged;
            _xboxController.DpadDirectionChanged += XboxController_DpadDirectionChanged;
            _xboxController.LeftTriggerChanged += XboxController_LeftTriggerChanged;
            _xboxController.RightTriggerChanged += XboxController_RightTriggerChanged;
            _xboxController.FunctionButtonChanged += XboxController_FunctionButtonChanged;
            _xboxController.BumperButtonChanged += XboxController_BumperButtonChanged;

            _gaitSpeed = 80;
            _bodyPosY = 42;
            _legLiftHeight = 42;
            GaitSpeedUpperLimit = 500;
            GaitSpeedLowerLimit = 30;
            TravelLengthZupperLimit = 180;
            TravelLengthZlowerLimit = 80;
            TravelLengthXlimit = 38;
            TravelRotationYlimit = 31;
            LegLiftHeightUpperLimit = 140;
            LegLiftHeightLowerLimit = 30;
        }

        internal static double LegLiftHeightUpperLimit { get; set; }
        internal static double LegLiftHeightLowerLimit { get; set; }

        internal static double GaitSpeedMax { get; set; }
        internal static double GaitSpeedMin { get; set; }
        internal static double GaitSpeedUpperLimit { get; set; }
        internal static double GaitSpeedLowerLimit { get; set; }

        internal static double TravelLengthZupperLimit { get; set; }
        internal static double TravelLengthZlowerLimit { get; set; }
        internal static double TravelLengthXlimit { get; set; }
        internal static double TravelRotationYlimit { get; set; }

        public void Start()
        {
        }

        #region XBox 360 Controller related...

        //4 = Left bumper, 5 = Right bumper
        private void XboxController_BumperButtonChanged(int button)
        {
            switch (_selectedFunction)
            {
                case SelectedFunction.TranslateHorizontal:
                case SelectedFunction.Translate3D:
                case SelectedFunction.GaitSpeed: //A
                    if (button == 5)
                    {
                        if (_gaitSpeed < GaitSpeedUpperLimit) //200
                        {
                            _gaitSpeed = _gaitSpeed + 3;
                        }
                    }
                    else
                    {
                        if (_gaitSpeed > GaitSpeedLowerLimit) //45
                        {
                            _gaitSpeed = _gaitSpeed - 3;
                        }
                    }

                    break;
                case SelectedFunction.LegHeight: //B
                    if (button == 5)
                    {
                        if (_legLiftHeight < LegLiftHeightUpperLimit) //90
                            _legLiftHeight = _legLiftHeight + 4;
                    }
                    else
                    {
                        if (_legLiftHeight > LegLiftHeightLowerLimit) //20
                            _legLiftHeight = _legLiftHeight - 4;
                    }
                    break;
            }

            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);

            Display.Write($"Speed : {_gaitSpeed}", 1);
            Display.Write($"Lift : {_legLiftHeight}", 2);
        }

        private void XboxController_FunctionButtonChanged(int button)
        {
            switch (button)
            {
                case 0: //A
                    if (_selectedFunction == SelectedFunction.GaitSpeed)
                    {
                        _selectedFunction = SelectedFunction.LegHeight;
                        Display.Write($"Leg height");
                    }
                    else
                    {
                        _selectedFunction = SelectedFunction.GaitSpeed;
                        Display.Write($"Gait speed");
                    }
                    break;
                case 1: //B
                    //if (_selectedFunction == SelectedFunction.TranslateHorizontal)
                    //    _selectedFunction = SelectedFunction.Translate3D;
                    //else
                    //    _selectedFunction = SelectedFunction.TranslateHorizontal;
                    if (_selectedFunction == SelectedFunction.Translate3D)
                    {
                        _selectedFunction = SelectedFunction.SetFootHeightOffset;
                        _legPosY = 0;
                        _ik.RequestSetFunction(_selectedFunction);
                    }
                    else
                    {
                        _selectedFunction = SelectedFunction.Translate3D;
                        _ik.RequestSetFunction(_selectedFunction);
                    }

                    break;
                case 2: //X

                    // _routeFinder?.DisableGpsNavigation();

                    break;
                case 3: //Y

                    //_routeFinder?.EnableGpsNavigation();

                    _ik.RequestSaveLegYHeightCorrector();

                    break;
                //switch (_posture)
                //{
                //    case 0:
                //        _ik.RequestBodyPosition(7, 0, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                //        break;
                //    case 1:
                //        _ik.RequestBodyPosition(-7, 0, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                //        break;
                //    case 2:
                //        _ik.RequestBodyPosition(-7, 0, 0, -30, _bodyPosY, _bodyRotY);
                //        break;
                //    case 3:
                //        _ik.RequestBodyPosition(7, 0, 0, 30, _bodyPosY, _bodyRotY);
                //        break;
                //    case 4:
                //        _ik.RequestBodyPosition(-7, 0, 0, 30, _bodyPosY, _bodyRotY);
                //        break;
                //    case 5:
                //        _ik.RequestBodyPosition(7, 0, 0, -30, _bodyPosY, _bodyRotY);
                //        break;
                //    case 6:
                //        _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                //        Task.Factory.StartNew(async() =>
                //        {
                //            for (; _bodyPosY < 90; _bodyPosY++)
                //            {
                //                _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                //                await Task.Delay(50);
                //            }
                //        });
                //        break;
                //    case 7:
                //        _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                //        Task.Factory.StartNew(async() =>
                //        {
                //            for (; _bodyPosY > 20; _bodyPosY--)
                //            {
                //                _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                //                await Task.Delay(50);
                //            }
                //        });
                //        break;
                //    case 8:
                //        _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                //        _posture = 0;
                //        return;
                //}

                //_posture++;
                //break;
                case 7: //Start button
                    _isMovementStarted = !_isMovementStarted;

                    _ik.RequestSetMovement(_isMovementStarted);

                    if (_isMovementStarted)
                    {
                        _ik.RequestSetFunction(SelectedFunction.GaitSpeed);
                        _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                        _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                        _ik.RequestSetGaitType(GaitType.TripleTripod16Steps);
                        _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
                    }
                    else
                        _ik.RequestMovement(_gaitSpeed, 0, 0, 0);

                    Debug.WriteLine("setting movement to  " + _isMovementStarted);
                    break;
                case 6: //back button
                    _gps?.CurrentLatLon.SaveWaypoint();
                    //else
                    //    Task.Factory.StartNew(async () => //This fires a dart from the Dream Cheeky (thinkgeek) usb nerf dart launcher. 
                    //    {
                    //        //RemoteArduino.Arduino.digitalWrite(7, PinState.HIGH);
                    //        await InverseKinematics.SerialPort.Write("#5H\r"); //On the SSC-32U, it sets channel 5 HIGH for 3 seconds

                    //        await Task.Delay(3000);

                    //        await InverseKinematics.SerialPort.Write("#5L\r"); //On the SSC-32U, it sets channel 5 LOW
                    //        //RemoteArduino.Arduino.digitalWrite(7, PinState.LOW);
                    //    });
                    break;
                default:
                    Debug.WriteLine("button? " + button);
                    break;
            }
        }

        private void XboxController_RightTriggerChanged(int trigger)
        {
            _travelLengthX = trigger.Map(0, 10000, 0, TravelLengthXlimit);
            _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void XboxController_LeftTriggerChanged(int trigger)
        {
            _travelLengthX = -trigger.Map(0, 10000, 0, TravelLengthXlimit);
            _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void XboxController_DpadDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    if (_gaitType > 0 && _selectedFunction != SelectedFunction.TranslateHorizontal && _selectedFunction != SelectedFunction.SetFootHeightOffset)
                    {
                        _gaitType--;
                        _ik.RequestSetGaitType(_gaitType);
                        Display.Write(Enum.GetName(typeof(GaitType), _gaitType));
                    }
                    else if (_selectedFunction == SelectedFunction.TranslateHorizontal && _bodyRotY > -30)
                    {
                        _bodyRotY = _bodyRotY - 2;
                        _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                    }
                    else if (_selectedFunction == SelectedFunction.SetFootHeightOffset)
                    {
                        _selectedLeg--;
                        if (_selectedLeg < 0)
                            _selectedLeg = 5;

                        _ik.RequestLegYHeight(_selectedLeg, 0);
                        _ik.RequestSetFunction(_selectedFunction);
                    }

                    break;
                case ControllerDirection.Right:
                    if ((int) _gaitType < 4 && _selectedFunction != SelectedFunction.TranslateHorizontal && _selectedFunction != SelectedFunction.SetFootHeightOffset)
                    {
                        _gaitType++;
                        _ik.RequestSetGaitType(_gaitType);
                        Display.Write(Enum.GetName(typeof(GaitType), _gaitType));
                    }
                    else if (_selectedFunction == SelectedFunction.TranslateHorizontal && _bodyRotY < 30)
                    {
                        _bodyRotY = _bodyRotY + 2;
                        _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                    }
                    else if (_selectedFunction == SelectedFunction.SetFootHeightOffset)
                    {
                        _selectedLeg++;
                        if (_selectedLeg > 5)
                            _selectedLeg = 0;

                        _ik.RequestLegYHeight(_selectedLeg, 0);
                        _ik.RequestSetFunction(_selectedFunction);
                    }
                    break;
                case ControllerDirection.Up:
                    if (_bodyPosY < 95)
                    {
                        if (_selectedFunction == SelectedFunction.SetFootHeightOffset)
                        {
                            _legPosY = _legPosY + 2;
                            _ik.RequestLegYHeight(_selectedLeg, _legPosY);
                        }
                        else
                        {
                            _bodyPosY = _bodyPosY + 5;
                            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                        }

                        Display.Write($"_bodyPosY = {_bodyPosY}");
                    }
                    break;
                case ControllerDirection.Down:
                    if (_bodyPosY > 15)
                    {
                        if (_selectedFunction == SelectedFunction.SetFootHeightOffset)
                        {
                            _legPosY = _legPosY - 2;
                            _ik.RequestLegYHeight(_selectedLeg, _legPosY);
                        }
                        else
                        {
                            _bodyPosY = _bodyPosY - 5;
                            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                        }
                        Display.Write($"_bodyPosY = {_bodyPosY}");
                    }
                    break;
            }
        }

        private void XboxController_RightDirectionChanged(ControllerVector sender)
        {
            if (_selectedFunction == SelectedFunction.LegHeight)
                return;

            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _travelRotationY = -sender.Magnitude.Map(0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _travelRotationY = -sender.Magnitude.Map(0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = -sender.Magnitude.Map(0, 10000, 0, TravelLengthZlowerLimit);
                    break;
                case ControllerDirection.DownLeft:
                    _travelRotationY = -sender.Magnitude.Map(0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = sender.Magnitude.Map(0, 10000, 0, TravelLengthZlowerLimit); //110
                    break;
                case ControllerDirection.Right:
                    _travelRotationY = sender.Magnitude.Map(0, 10000, 0, TravelRotationYlimit); //3
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpRight:
                    _travelRotationY = sender.Magnitude.Map(0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = -sender.Magnitude.Map(0, 10000, 0, TravelLengthZupperLimit); //190
                    break;
                case ControllerDirection.DownRight:
                    _travelRotationY = sender.Magnitude.Map(0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = sender.Magnitude.Map(0, 10000, 0, TravelLengthZlowerLimit);
                    break;
                case ControllerDirection.Up:
                    _travelLengthZ = -sender.Magnitude.Map(0, 10000, 0, TravelLengthZupperLimit);
                    _travelRotationY = 0;
                    break;
                case ControllerDirection.Down:
                    _travelLengthZ = sender.Magnitude.Map(0, 10000, 0, TravelLengthZupperLimit);
                    _travelRotationY = 0;
                    break;
            }

            _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void SetBodyRot(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyRotX = 0;
                    _bodyRotZ = -sender.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case ControllerDirection.UpLeft:
                    _bodyRotX = sender.Magnitude.Map(0, 10000, 0, 10);
                    _bodyRotZ = -sender.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case ControllerDirection.UpRight:
                    _bodyRotX = sender.Magnitude.Map(0, 10000, 0, 10);
                    _bodyRotZ = sender.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case ControllerDirection.Right:
                    _bodyRotX = 0;
                    _bodyRotZ = sender.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case ControllerDirection.Up:
                    _bodyRotX = sender.Magnitude.Map(0, 10000, 0, 10);
                    _bodyRotZ = 0;
                    break;
                case ControllerDirection.Down:
                    _bodyRotX = -sender.Magnitude.Map(0, 10000, 0, 10);
                    _bodyRotZ = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyRotZ = -sender.Magnitude.Map(0, 10000, 0, 10);
                    _bodyRotX = -sender.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case ControllerDirection.DownRight:
                    _bodyRotZ = sender.Magnitude.Map(0, 10000, 0, 10);
                    _bodyRotX = -sender.Magnitude.Map(0, 10000, 0, 10);
                    break;
            }

            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
        }

        private void SetBodyHorizontalOffset(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosZ = -sender.Magnitude.Map(0, 10000, 0, 30);
                    break;
                case ControllerDirection.UpRight:
                    _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosZ = sender.Magnitude.Map(0, 10000, 0, 30);
                    break;
                case ControllerDirection.Right:
                    _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.Up:
                    _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.Down:
                    _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyPosZ = -sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
                    break;
                case ControllerDirection.DownRight:
                    _bodyPosZ = sender.Magnitude.Map(0, 10000, 0, 30);
                    _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
                    break;
            }

            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
        }

        private void XboxController_LeftDirectionChanged(ControllerVector sender)
        {
            switch (_selectedFunction)
            {
                case SelectedFunction.TranslateHorizontal:
                    SetBodyHorizontalOffset(sender);
                    break;
                case SelectedFunction.Translate3D:
                    SetBodyRot(sender);
                    break;
                default:
                    SetBodyRot(sender);
                    break;
            }
        }

        #endregion
    }
}