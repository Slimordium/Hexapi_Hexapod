using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using HexapiBackground.Enums;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;
using HexapiBackground.Navigation;
using HexapiBackground.SignalR;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground{
    internal sealed class Hexapi
    {
        private readonly InverseKinematics _ik;
        private readonly XboxController _xboxController;

        private bool _isMovementStarted;

        internal static double LegLiftHeightUpperLimit { get; set; }
        internal static double LegLiftHeightLowerLimit { get; set; }

        internal static double GaitSpeedMax { get; set; }
        internal static double GaitSpeedMin { get; set; }

        private double _legLiftHeight = 30;
        internal double GaitSpeedUpperLimit { get; set; }
        internal double GaitSpeedLowerLimit { get; set; }
        private double _gaitSpeed;
        private SelectedFunction _selectedFunction = SelectedFunction.GaitSpeed;

        private double _travelLengthX; 
        private double _travelLengthZ; 
        private double _travelRotationY;

        internal static double TravelLengthZupperLimit { get; set; }
        internal static double TravelLengthZlowerLimit { get; set; }
        internal static double TravelLengthXlimit { get; set; }
        internal static double TravelRotationYlimit { get; set; }

        private GaitType _gaitType = GaitType.Tripod8Steps;

        private double _bodyPosY = 45;
        private double _bodyRotX1;
        private double _bodyRotZ1;
        private double _bodyRotY1;
        private double _bodyPosX;
        private double _bodyPosZ;

        private RouteFinder _routeFinder;

        private readonly IGps _gps;

        internal Hexapi(IGps gps = null)
        {
            _gps = gps;

            _ik = new InverseKinematics();

            //var asdf = new HexapiLeapMotionClient(_ik);

            if (_gps != null)
                _routeFinder = new RouteFinder(_ik, gps);

            _xboxController = new XboxController();
            _xboxController.Open();
            
            _xboxController.LeftDirectionChanged += XboxController_LeftDirectionChanged;
            _xboxController.RightDirectionChanged += XboxController_RightDirectionChanged;
            _xboxController.DpadDirectionChanged += XboxController_DpadDirectionChanged;
            _xboxController.LeftTriggerChanged += XboxController_LeftTriggerChanged;
            _xboxController.RightTriggerChanged += XboxController_RightTriggerChanged;
            _xboxController.FunctionButtonChanged += XboxController_FunctionButtonChanged;
            _xboxController.BumperButtonChanged += XboxController_BumperButtonChanged;

            _gaitSpeed = 50;
            GaitSpeedUpperLimit = 250;
            GaitSpeedLowerLimit = 25;
            TravelLengthZupperLimit = 160;
            TravelLengthZlowerLimit = 80;
            TravelLengthXlimit = 40;
            TravelRotationYlimit = 2.5;
            LegLiftHeightUpperLimit = 100;
            LegLiftHeightLowerLimit = 20;
        }

        private Mpr121 _mpr121;

        public void Start()
        {
            Task.Factory.StartNew(() =>
            {
                _mpr121 = new Mpr121();
                _mpr121.Start();
            }
            , TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                _ik.Start(); 
            }
            , TaskCreationOptions.LongRunning);
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
                            _gaitSpeed = _gaitSpeed + 5;
                        }
                    }
                    else
                    {
                        if (_gaitSpeed > GaitSpeedLowerLimit) //45
                        {
                            _gaitSpeed = _gaitSpeed - 5;
                        }
                    }
                    break;
                case SelectedFunction.LegHeight: //B
                    if (button == 5)
                    {
                        if (_legLiftHeight < LegLiftHeightUpperLimit) //90
                            _legLiftHeight = _legLiftHeight + 5;
                    }
                    else
                    {
                        if (_legLiftHeight > LegLiftHeightLowerLimit) //20
                            _legLiftHeight = _legLiftHeight - 5;
                    }
                    break;
            }

            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        }

        private void XboxController_FunctionButtonChanged(int button)
        {
            switch (button)
            {
                case 0: //A
                    if (_selectedFunction == SelectedFunction.GaitSpeed)
                        _selectedFunction = SelectedFunction.LegHeight;
                    else
                        _selectedFunction = SelectedFunction.GaitSpeed;
                    break;
                case 1: //B
                    if (_selectedFunction == SelectedFunction.TranslateHorizontal)
                        _selectedFunction = SelectedFunction.Translate3D;
                    else
                        _selectedFunction = SelectedFunction.TranslateHorizontal;
                    break;
                case 2: //X

                    break;
                case 3: //Y

                    break;
                case 7: //Start button
                    _isMovementStarted = !_isMovementStarted;

                    if (_isMovementStarted)
                    {
                        _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
                        _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                        _ik.RequestSetGaitType(GaitType.RippleGait12Steps);
                    }
                    else
                        _ik.RequestMovement(_gaitSpeed, 0, 0, 0);

                    _ik.RequestSetMovement(_isMovementStarted);
                    Debug.WriteLine("setting movement to  " + _isMovementStarted);
                    break;
                case 6: //back button
                    if (_gps != null)
                        GpsHelpers.SaveWaypoint(_gps.CurrentLatLon);
                    else
                        Task.Factory.StartNew(async () => //This fires a dart from the Dream Cheeky (thinkgeek) usb nerf dart launcher. 
                        {
                            //RemoteArduino.Arduino.digitalWrite(7, PinState.HIGH);
                            InverseKinematics.SerialPort.Write("#5H\r"); //On the SSC-32U, it sets channel 5 HIGH for 3 seconds

                            await Task.Delay(3000);

                            InverseKinematics.SerialPort.Write("#5L\r"); //On the SSC-32U, it sets channel 5 LOW
                                                                         //RemoteArduino.Arduino.digitalWrite(7, PinState.LOW);
                        });
                    break;
                default:
                    Debug.WriteLine("button? " + button);
                    break;
            }
        }

        private void XboxController_RightTriggerChanged(int trigger)
        {
            _travelLengthX = MathHelpers.Map(trigger, 0, 10000, 0, TravelLengthXlimit);
            _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void XboxController_LeftTriggerChanged(int trigger)
        {
            _travelLengthX = -MathHelpers.Map(trigger, 0, 10000, 0, TravelLengthXlimit);
            _ik.RequestMovement(_gaitSpeed,  _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void XboxController_DpadDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    if ( _gaitType > 0 && _selectedFunction != SelectedFunction.TranslateHorizontal)
                    {
                        _gaitType--;
                        _ik.RequestSetGaitType(_gaitType);
                    }
                    else if (_selectedFunction == SelectedFunction.TranslateHorizontal && _bodyRotY1 > -6)
                    {
                        _bodyRotY1--;
                        _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY1);
                    }
                    break;
                case ControllerDirection.Right:
                    if ((int) _gaitType < 4 && _selectedFunction != SelectedFunction.TranslateHorizontal)
                    {
                        _gaitType++;
                        _ik.RequestSetGaitType(_gaitType);
                    }
                    else if (_selectedFunction == SelectedFunction.TranslateHorizontal && _bodyRotY1 < 6)
                    {
                        _bodyRotY1++;
                        _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY1);
                    }
                    break;
                case ControllerDirection.Up:
                    if (_bodyPosY < 90)
                    {
                        _bodyPosY = _bodyPosY + 5;
                        _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY1);
                    }
                    break;
                case ControllerDirection.Down:
                    if (_bodyPosY > 15)
                    {
                        _bodyPosY = _bodyPosY - 5;
                        _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY1);
                    }
                    break;
            }
        }

        private void XboxController_RightDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _travelRotationY = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _travelRotationY = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelLengthZlowerLimit);
                    break;
                case ControllerDirection.DownLeft:
                    _travelRotationY = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelLengthZlowerLimit); //110
                    break;
                case ControllerDirection.Right:
                    _travelRotationY = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelRotationYlimit); //3
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpRight:
                    _travelRotationY = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelLengthZupperLimit);//190
                    break;
                case ControllerDirection.DownRight:
                    _travelRotationY = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelRotationYlimit);
                    _travelLengthZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelLengthZlowerLimit);
                    break;
                case ControllerDirection.Up:
                    _travelLengthZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelLengthZupperLimit);
                    _travelRotationY = 0;
                    break;
                case ControllerDirection.Down:
                    _travelLengthZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, TravelLengthZupperLimit);
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
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.UpLeft:
                    _bodyRotX1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.UpRight:
                    _bodyRotX1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.Right:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.Up:
                    _bodyRotX1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = 0;
                    break;
                case ControllerDirection.Down:
                    _bodyRotX1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyRotZ1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotX1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.DownRight:
                    _bodyRotZ1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotX1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
            }

            _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY1);
        }

        private void SetBodyHorizontalOffset(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyPosX = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30); ;
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _bodyPosX = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    break;
                case ControllerDirection.UpRight:
                    _bodyPosX = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    break;
                case ControllerDirection.Right:
                    _bodyPosX = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.Up:
                    _bodyPosX = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.Down:
                    _bodyPosX = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyPosZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosX = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    break;
                case ControllerDirection.DownRight:
                    _bodyPosZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    _bodyPosX = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
                    break;
            }

            _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY1);
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