using System.Diagnostics;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;
using HexapiBackground.Navigation;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground{
    internal sealed class Hexapi
    {
        private readonly InverseKinematics _ik;
        private readonly XboxController _xboxController;

        private bool _isMovementStarted = false;

        private double _legLiftHeight = 20;
        private double _nomGaitSpeed = 65;
        private SelectedFunction _selectedFunction = SelectedFunction.GaitSpeed;

        private double _travelLengthX; 
        private double _travelLengthZ; 
        private double _travelRotationY;

        private GaitType _gaitType = GaitType.Tripod8Steps;
        private double _bodyPosY = 0;
        private double _bodyRotX1;
        private double _bodyRotZ1;
        private double _bodyPosX;
        private double _bodyPosZ;

        private RouteFinder _routeFinder;

        private readonly IGps _gps;

        internal Hexapi(IGps gps = null, Avc avc = null)
        {
            _gps = gps;

            _ik = new InverseKinematics(avc);

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
        }

        public void Start()
        {
            Task.Factory.StartNew(() =>
            { _ik.Start(); }, TaskCreationOptions.LongRunning);
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
                        if (_nomGaitSpeed < 200)
                        {
                            _nomGaitSpeed = _nomGaitSpeed + 5;
                        }
                    }
                    else
                    {
                        if (_nomGaitSpeed > 40)
                        {
                            _nomGaitSpeed = _nomGaitSpeed - 5;
                        }
                    }
                    break;
                case SelectedFunction.LegHeight: //B
                    if (button == 5)
                    {
                        if (_legLiftHeight < 90)
                            _legLiftHeight = _legLiftHeight + 5;
                    }
                    else
                    {
                        if (_legLiftHeight > 20)
                            _legLiftHeight = _legLiftHeight - 5;
                    }
                    break;
            }

            _ik.RequestSetGaitOptions(_nomGaitSpeed, _legLiftHeight);
        }

        private void XboxController_FunctionButtonChanged(int button)
        {
            switch (button)
            {
                case 0: //A
                    _selectedFunction = SelectedFunction.GaitSpeed;
                    break;
                case 1: //B
                    _selectedFunction = SelectedFunction.LegHeight;
                    break;
                case 2: //X
                    _selectedFunction = SelectedFunction.TranslateHorizontal;
                    break;
                case 3: //Y
                    _selectedFunction = SelectedFunction.Translate3D;
                    break;
                case 7: //Start button
                    _isMovementStarted = !_isMovementStarted;

                    _ik.RequestSetMovement(_isMovementStarted);

                    Debug.WriteLine("setting movement to  " + _isMovementStarted);
                    break;
                case 6: //back button
                    if (_gps != null)
                        GpsHelpers.SaveWaypoint(_gps.CurrentLatLon);

                    break;
                default:
                    Debug.WriteLine("button? " + button);
                    break;
            }
        }

        private void XboxController_RightTriggerChanged(int trigger)
        {
            _travelLengthX = MathHelpers.Map(trigger, 0, 10000, 0, 90);
            _ik.RequestMovement(_nomGaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void XboxController_LeftTriggerChanged(int trigger)
        {
            _travelLengthX = -MathHelpers.Map(trigger, 0, 10000, 0, 90);
            _ik.RequestMovement(_nomGaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void XboxController_DpadDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    if ( _gaitType > 0)
                    {
                        _gaitType--;
                        _ik.RequestSetGaitType(_gaitType);
                    }
                    break;
                case ControllerDirection.Right:
                    if ((int) _gaitType < 4)
                    {
                        _gaitType++;
                        _ik.RequestSetGaitType(_gaitType);
                    }
                    break;
                case ControllerDirection.Up:
                    if (_bodyPosY < 90)
                    {
                        _bodyPosY = _bodyPosY + 5;
                        _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY);
                    }
                    break;
                case ControllerDirection.Down:
                    if (_bodyPosY > 5)
                    {
                        _bodyPosY = _bodyPosY - 5;
                        _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY);
                    }
                    break;
            }
        }

        private void XboxController_RightDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _travelRotationY = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _travelRotationY = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 1);
                    _travelLengthZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 130);
                    break;
                case ControllerDirection.DownLeft:
                    _travelRotationY = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 1);
                    _travelLengthZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 130);
                    break;
                case ControllerDirection.Right:
                    _travelRotationY = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpRight:
                    _travelRotationY = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 1);
                    _travelLengthZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 130);
                    break;
                case ControllerDirection.DownRight:
                    _travelRotationY = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 1);
                    _travelLengthZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 130);
                    break;
                case ControllerDirection.Up:
                    _travelLengthZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 170);
                    _travelRotationY = 0;
                    break;
                case ControllerDirection.Down:
                    _travelLengthZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 170);
                    _travelRotationY = 0;
                    break;
            }

            _ik.RequestMovement(_nomGaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
        }

        private void SetBodyRot(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    break;
                case ControllerDirection.UpLeft:
                    _bodyRotX1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    _bodyRotZ1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    break;
                case ControllerDirection.UpRight:
                    _bodyRotX1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    _bodyRotZ1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    break;
                case ControllerDirection.Right:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    break;
                case ControllerDirection.Up:
                    _bodyRotX1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    _bodyRotZ1 = 0;
                    break;
                case ControllerDirection.Down:
                    _bodyRotX1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    _bodyRotZ1 = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyRotZ1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    _bodyRotX1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    break;
                case ControllerDirection.DownRight:
                    _bodyRotZ1 = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    _bodyRotX1 = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 9);
                    break;
            }

            _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY);
        }

        private void SetBodyHorizontalOffset(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyPosX = 0;
                    _bodyPosZ = -MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
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
                    _bodyPosX = 0;
                    _bodyPosZ = MathHelpers.Map(sender.Magnitude, 0, 10000, 0, 30);
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

            _ik.RequestBodyPosition(_bodyRotX1, _bodyRotZ1, _bodyPosX, _bodyPosZ, _bodyPosY);
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