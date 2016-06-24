using System;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;
using HexapiBackground.Navigation;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground
{
    internal sealed class Hexapi
    {
        private readonly Gps.Gps _gps;
        private readonly IkController _ik;
        private readonly Navigator _navigator;
        private readonly XboxController _xboxController;

        private double _bodyPosX;
        private double _bodyPosY; //45
        private double _bodyPosZ;
        private double _bodyRotX;
        private double _bodyRotY;
        private double _bodyRotZ;
        private double _gaitSpeed;

        private GaitType _gaitType = GaitType.Tripod8;

        private bool _isMovementStarted;
        private double _legLiftHeight;
        private double _legPosY;

        private int _posture;
        private SelectedIkFunction _selectedFunction = SelectedIkFunction.GaitSpeed;
        private SelectedGpsFunction _selectedGpsFunction = SelectedGpsFunction.GpsDisabled;
        private int _selectedLeg;

        private double _travelLengthX;
        private double _travelLengthZ;
        private double _travelRotationY;

        internal Hexapi(IkController ikController, XboxController xboxController, Gps.Gps gps, Navigator navigator)
        {
            _ik = ikController;
            _xboxController = xboxController;
            _gps = gps;
            _navigator = navigator;

            SetGaitOptions();

            _xboxController.LeftDirectionChanged += XboxController_LeftDirectionChanged;
            _xboxController.RightDirectionChanged += XboxController_RightDirectionChanged;
            _xboxController.DpadDirectionChanged += XboxController_DpadDirectionChanged;
            _xboxController.LeftTriggerChanged += XboxController_LeftTriggerChanged;
            _xboxController.RightTriggerChanged += XboxController_RightTriggerChanged;
            _xboxController.FunctionButtonChanged += XboxController_FunctionButtonChanged;
            _xboxController.BumperButtonChanged += XboxController_BumperButtonChanged;
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
        private async void XboxController_BumperButtonChanged(int button)
        {
            switch (_selectedFunction)
            {
                case SelectedIkFunction.TranslateHorizontal:
                case SelectedIkFunction.Translate3D:
                case SelectedIkFunction.GaitSpeed: //A
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
                case SelectedIkFunction.LegHeight: //B
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

            await Display.Write($"Speed : {_gaitSpeed}", 1);
            await Display.Write($"Lift : {_legLiftHeight}", 2);
        }

        private async void XboxController_FunctionButtonChanged(int button)
        {
            switch (button)
            {
                case 0: //A
                    _selectedFunction--;
                    break;
                case 1: //B
                    _selectedFunction++;
                    break;
                case 2: //X

                    if (_selectedGpsFunction == SelectedGpsFunction.GpsDisabled)
                    {
                        await _navigator.EnableGpsNavigation().ConfigureAwait(false);
                        await Display.Write("GPS Nav Enabled");
                        _selectedGpsFunction = SelectedGpsFunction.GpsEnabled;
                    }
                    else
                    {
                        await Display.Write("GPS Nav Disabled");
                        _navigator.DisableGpsNavigation();
                        _selectedGpsFunction = SelectedGpsFunction.GpsDisabled;
                    }

                    break;
                case 3: //Y


                    break;
                case 7: //Start button
                    _isMovementStarted = !_isMovementStarted;

                    if (_isMovementStarted)
                    {
                        _ik.RequestSetFunction(SelectedIkFunction.GaitSpeed);
                        _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                        _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                        _ik.RequestSetGaitType(GaitType.TripleTripod16);
                        _ik.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);
                    }
                    else
                        _ik.RequestMovement(_gaitSpeed, 0, 0, 0);

                    SetGaitOptions();
                    _ik.RequestSetMovement(_isMovementStarted);
                    break;
                case 6: //back button
                    await _gps.CurrentLatLon.SaveWaypoint();

                    break;
                default:
                    await Display.Write($"Unknown button {button}");
                    break;
            }

            await Display.Write($"{Enum.GetName(typeof (SelectedIkFunction), _selectedFunction)}");
        }

        private async void SetPosture()
        {
            if (_posture > 8)
                _posture = 8;

            if (_posture < 0)
                _posture = 0;

            await Display.Write($"Posture {_posture}");

            switch (_posture)
            {
                case 0:
                    _ik.RequestBodyPosition(7, 0, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                    break;
                case 1:
                    _ik.RequestBodyPosition(-7, 0, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                    break;
                case 2:
                    _ik.RequestBodyPosition(-7, 0, 0, -30, _bodyPosY, _bodyRotY);
                    break;
                case 3:
                    _ik.RequestBodyPosition(7, 0, 0, 30, _bodyPosY, _bodyRotY);
                    break;
                case 4:
                    _ik.RequestBodyPosition(-7, 0, 0, 30, _bodyPosY, _bodyRotY);
                    break;
                case 5:
                    _ik.RequestBodyPosition(7, 0, 0, -30, _bodyPosY, _bodyRotY);
                    break;
                case 6:
                    _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                    for (; _bodyPosY < 90; _bodyPosY++)
                    {
                        _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                        await Task.Delay(20);
                    }
                    break;
                case 7:
                    _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                    for (; _bodyPosY > 20; _bodyPosY--)
                    {
                        _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                        await Task.Delay(20);
                    }
                    break;
                case 8:
                    _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                    _posture = 0;
                    return;
            }
        }

        private void SetGaitOptions()
        {
            switch (_gaitType)
            {
                case GaitType.Tripod8:
                    _legLiftHeight = 35;
                    _gaitSpeed = 55;
                    GaitSpeedUpperLimit = 500;
                    GaitSpeedLowerLimit = 50;
                    LegLiftHeightUpperLimit = 45;
                    LegLiftHeightLowerLimit = 30;
                    TravelLengthZupperLimit = 130;
                    TravelLengthZlowerLimit = 80;
                    TravelLengthXlimit = 25;
                    break;
                default:
                    _gaitSpeed = 45;
                    _bodyPosY = 65;
                    _legLiftHeight = 35;
                    GaitSpeedUpperLimit = 500;
                    GaitSpeedLowerLimit = 30;
                    TravelLengthZupperLimit = 180;
                    TravelLengthZlowerLimit = 80;
                    TravelLengthXlimit = 35;
                    TravelRotationYlimit = 36;
                    LegLiftHeightUpperLimit = 110;
                    LegLiftHeightLowerLimit = 30;
                    break;
            }

            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
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

        private async void XboxController_DpadDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:

                    switch (_selectedFunction)
                    {
                        case SelectedIkFunction.GaitType:
                            _gaitType--;

                            SetGaitOptions();
                            await Display.Write(Enum.GetName(typeof (GaitType), _gaitType));
                            break;
                        case SelectedIkFunction.GaitSpeed:
                            _gaitSpeed = _gaitSpeed - 2;
                            if (_gaitSpeed < GaitSpeedMin)
                                _gaitSpeed = GaitSpeedMin;
                            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            await Display.Write($"_gaitSpeed = {_gaitSpeed}");
                            break;
                        case SelectedIkFunction.SetFootHeightOffset:
                            _selectedLeg--;
                            if (_selectedLeg < 0)
                                _selectedLeg = 0;
                            await Display.Write($"_selectedLeg = {_selectedLeg}");
                            break;
                    }
                    break;
                case ControllerDirection.Right:
                    switch (_selectedFunction)
                    {
                        case SelectedIkFunction.GaitType:
                            _gaitType++;

                            await Display.Write(Enum.GetName(typeof (GaitType), _gaitType));
                            if (_gaitType == GaitType.Tripod8)
                            {
                                _legLiftHeight = 35;
                                _gaitSpeed = 55;
                                _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            }
                            _ik.RequestSetGaitType(_gaitType);
                            break;
                        case SelectedIkFunction.GaitSpeed:
                            _gaitSpeed = _gaitSpeed + 2;
                            if (_gaitSpeed > GaitSpeedMax)
                                _gaitSpeed = GaitSpeedMax;
                            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            await Display.Write($"_gaitSpeed = {_gaitSpeed}");
                            break;
                        case SelectedIkFunction.SetFootHeightOffset:
                            _selectedLeg++;
                            if (_selectedLeg == 5)
                                _selectedLeg = 5;

                            await Display.Write($"_selectedLeg = {_selectedLeg}");
                            break;
                    }
                    break;
                case ControllerDirection.Up:

                    switch (_selectedFunction)
                    {
                        case SelectedIkFunction.SetFootHeightOffset:
                            _legPosY = _legPosY + 2;
                            _ik.RequestLegYHeight(_selectedLeg, _legPosY);
                            break;
                        case SelectedIkFunction.PingSetup:
                            _ik.RequestNewPerimeter(true);
                            break;
                        case SelectedIkFunction.BodyHeight:
                            _bodyPosY = _bodyPosY + 5;
                            if (_bodyPosY > 110)
                                _bodyPosY = 110;
                            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                            await Display.Write($"_bodyPosY = {_bodyPosY}");
                            break;
                        case SelectedIkFunction.Posture:
                            _posture++;
                            SetPosture();
                            break;
                    }
                    break;
                case ControllerDirection.Down:

                    switch (_selectedFunction)
                    {
                        case SelectedIkFunction.SetFootHeightOffset:
                            _legPosY = _legPosY - 2;
                            _ik.RequestLegYHeight(_selectedLeg, _legPosY);
                            break;
                        case SelectedIkFunction.PingSetup:
                            _ik.RequestNewPerimeter(false);
                            break;
                        case SelectedIkFunction.BodyHeight:
                            _bodyPosY = _bodyPosY - 5;
                            if (_bodyPosY < 10)
                                _bodyPosY = 10;
                            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                            await Display.Write($"_bodyPosY = {_bodyPosY}");
                            break;
                        case SelectedIkFunction.Posture:
                            _posture--;
                            SetPosture();
                            break;
                    }
                    break;
            }
        }

        private void XboxController_RightDirectionChanged(ControllerVector sender)
        {
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
                case SelectedIkFunction.TranslateHorizontal:
                    SetBodyHorizontalOffset(sender);
                    break;
                case SelectedIkFunction.Translate3D:
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