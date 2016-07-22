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
        private readonly SparkFunSerial16X2Lcd _display;
        private readonly IoTClient _ioTClient;

        private double _bodyPosX;
        private double _bodyPosY; //45
        private double _bodyPosZ;
        private double _bodyRotX;
        private double _bodyRotY;
        private double _bodyRotZ;
        private double _gaitSpeed;

        private GaitType _gaitType = GaitType.TripleTripod12;

        private bool _isMovementStarted;
        private double _legLiftHeight;
        private double _legPosY;

        private int _posture;
        private SelectedIkFunction _selectedIkFunction = SelectedIkFunction.GaitSpeed;
        private SelectedGpsFunction _selectedGpsFunction = SelectedGpsFunction.GpsDisabled;
        private Behavior _selectedBehavior = Behavior.Avoid;
        private int _selectedLeg;

        private double _travelLengthX;
        private double _travelLengthZ;
        private double _travelRotationY;
        
        internal Hexapi(IkController ikController, 
                        XboxController xboxController, 
                        Navigator navigator, 
                        SparkFunSerial16X2Lcd display, 
                        Gps.Gps gps,
                        IoTClient ioTClient)
        {
            _ik = ikController;
            _xboxController = xboxController;
            _gps = gps;
            _navigator = navigator;
            _display = display;
            _ioTClient = ioTClient;
        }

        internal static double LegLiftHeightUpperLimit { get; set; }
        internal static double LegLiftHeightLowerLimit { get; set; }

        internal static double GaitSpeedMax { get; set; }
        internal static double GaitSpeedMin { get; set; }

        internal static double TravelLengthZupperLimit { get; set; }
        internal static double TravelLengthZlowerLimit { get; set; }
        internal static double TravelLengthXlimit { get; set; }
        internal static double TravelRotationYlimit { get; set; }

        public async Task Start()
        {
            SetGaitOptions();

            await Task.Run(() =>
            {
                _xboxController.LeftDirectionChanged += XboxController_LeftDirectionChanged;
                _xboxController.RightDirectionChanged += XboxController_RightDirectionChanged;
                _xboxController.DpadDirectionChanged += XboxController_DpadDirectionChanged;
                _xboxController.LeftTriggerChanged += XboxController_LeftTriggerChanged;
                _xboxController.RightTriggerChanged += XboxController_RightTriggerChanged;
                _xboxController.FunctionButtonChanged += XboxController_FunctionButtonChanged;
                _xboxController.BumperButtonChanged += XboxController_BumperButtonChanged;
            });
        }

        #region XBox 360 Controller related...

        //4 = Left bumper, 5 = Right bumper
        private void XboxController_BumperButtonChanged(int button)
        {
            
        }

        private async void XboxController_FunctionButtonChanged(int button)
        {
            switch (button)
            {
                case 0: //A
                    _selectedIkFunction--;
                    if (_selectedIkFunction < 0)
                        _selectedIkFunction = 0;

                    await _display.Write($"{Enum.GetName(typeof(SelectedIkFunction), _selectedIkFunction)}", 1);

                    await _ik.RequestSetFunction(_selectedIkFunction);
                    break;
                case 1: //B
                    _selectedIkFunction++;
                    if ((int) _selectedIkFunction > 14)
                        _selectedIkFunction = (SelectedIkFunction) 14;

                    await _display.Write($"{Enum.GetName(typeof(SelectedIkFunction), _selectedIkFunction)}", 1);

                    await _ik.RequestSetFunction(_selectedIkFunction);
                    break;
                case 2: //X

                    if (_selectedGpsFunction == SelectedGpsFunction.GpsDisabled)
                    {
                        await _navigator.Start();
                        await _display.Write("GPS Nav Enabled", 1);
                        _selectedGpsFunction = SelectedGpsFunction.GpsEnabled;
                    }
                    else
                    {
                        await _display.Write("GPS Nav Disabled", 1);
                        _navigator.Stop();
                        _selectedGpsFunction = SelectedGpsFunction.GpsDisabled;
                    }

                    break;
                case 3: //Y


                    break;
                case 7: //Start button
                    _isMovementStarted = !_isMovementStarted;

                    if (_isMovementStarted)
                    {
                        await _ik.RequestSetFunction(SelectedIkFunction.GaitSpeed);
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
                    await _display.Write($"Unknown button {button}", 1);
                    break;
            }
        }

        private async Task SetPosture()
        {
            if (_posture > 8)
                _posture = 8;

            if (_posture < 0)
                _posture = 0;

            await _display.Write($"Posture {_posture}", 2);

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
                    _bodyPosY = 100;
                    _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                    break;
                case 7:
                    _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
                    _bodyPosY = 80;
                    _ik.RequestBodyPosition(0, 0, 0, 0, _bodyPosY, _bodyRotY);
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
                    _bodyPosY = 80;
                    _legLiftHeight = 40;
                    _gaitSpeed = 45;
                    GaitSpeedMax = 500;
                    GaitSpeedMin = 40;
                    LegLiftHeightUpperLimit = 45;
                    LegLiftHeightLowerLimit = 30;
                    TravelLengthZupperLimit = 100;
                    TravelLengthZlowerLimit = 80;
                    TravelLengthXlimit = 25;
                    TravelRotationYlimit = 25;
                    break;
                case GaitType.TripleTripod12:
                    _bodyPosY = 75;
                    _legLiftHeight = 60;
                    _gaitSpeed = 25;
                    GaitSpeedMax = 500;
                    GaitSpeedMin = 25;
                    TravelLengthZupperLimit = 140;
                    TravelLengthZlowerLimit = 80;
                    TravelLengthXlimit = 25;
                    LegLiftHeightUpperLimit = 110;
                    LegLiftHeightLowerLimit = 30;
                    TravelRotationYlimit = 30;
                    break;
                case GaitType.TripleTripod16:
                    _bodyPosY = 75;
                    _legLiftHeight = 80;
                    _gaitSpeed = 25;
                    GaitSpeedMax = 500;
                    GaitSpeedMin = 20;
                    TravelLengthZupperLimit = 140;
                    TravelLengthZlowerLimit = 80;
                    TravelLengthXlimit = 25;
                    LegLiftHeightUpperLimit = 110;
                    LegLiftHeightLowerLimit = 30;
                    TravelRotationYlimit = 30;
                    break;
                default:
                    _gaitSpeed = 40;
                    _bodyPosY = 90;
                    _legLiftHeight = 35;
                    GaitSpeedMax = 500;
                    GaitSpeedMin = 25;
                    TravelLengthZupperLimit = 140;
                    TravelLengthZlowerLimit = 80;
                    TravelLengthXlimit = 35;
                    TravelRotationYlimit = 30;
                    LegLiftHeightUpperLimit = 110;
                    LegLiftHeightLowerLimit = 30;
                    break;
            }

            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
            _ik.RequestSetGaitType(_gaitType);
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

                    switch (_selectedIkFunction)
                    {
                        case SelectedIkFunction.Behavior:
                            _selectedBehavior--;

                            if ((int)_selectedBehavior <= 0)
                                _selectedBehavior = (Behavior)0;

                            await _display.Write($"{Enum.GetName(typeof(Behavior), _selectedBehavior)}", 2);
                            break;
                        case SelectedIkFunction.GaitType:
                            _gaitType--;

                            if (_gaitType < 0)
                                _gaitType = (GaitType)4;

                            await _display.Write(Enum.GetName(typeof(GaitType), _gaitType), 2);
                            SetGaitOptions();
                            break;
                        case SelectedIkFunction.GaitSpeed:
                            _gaitSpeed = _gaitSpeed - 2;
                            if (_gaitSpeed < GaitSpeedMin)
                                _gaitSpeed = GaitSpeedMin;
                            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            await _display.Write($"_gaitSpeed = {_gaitSpeed}", 2);
                            break;
                        case SelectedIkFunction.SetFootHeightOffset:
                            _selectedLeg--;
                            if (_selectedLeg < 0)
                                _selectedLeg = 0;
                            await _display.Write($"_selectedLeg = {_selectedLeg}", 2);
                            break;
                    }
                    break;
                case ControllerDirection.Right:
                    switch (_selectedIkFunction)
                    {
                        case SelectedIkFunction.Behavior:
                            _selectedBehavior++;

                            if ((int)_selectedBehavior > 4)
                                _selectedBehavior = (Behavior)4;

                            await _display.Write($"{Enum.GetName(typeof(Behavior), _selectedBehavior)}", 2);
                            break;
                        case SelectedIkFunction.GaitType:
                            _gaitType++;

                            if ((int)_gaitType > 4)
                                _gaitType = 0;

                            await _display.Write(Enum.GetName(typeof (GaitType), _gaitType), 2);
                            SetGaitOptions();
                            break;
                        case SelectedIkFunction.GaitSpeed:
                            _gaitSpeed = _gaitSpeed + 2;
                            if (_gaitSpeed > GaitSpeedMax)
                                _gaitSpeed = GaitSpeedMax;
                            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            await _display.Write($"_gaitSpeed = {_gaitSpeed}", 2);
                            break;
                        case SelectedIkFunction.SetFootHeightOffset:
                            _selectedLeg++;
                            if (_selectedLeg == 5)
                                _selectedLeg = 5;

                            await _display.Write($"_selectedLeg = {_selectedLeg}", 2);
                            break;
                    }
                    break;
                case ControllerDirection.Up:
                    switch (_selectedIkFunction)
                    {
                        case SelectedIkFunction.LegLiftHeight:
                            _legLiftHeight++;

                            if (_legLiftHeight > LegLiftHeightUpperLimit)
                                _legLiftHeight = LegLiftHeightUpperLimit;

                            await _display.Write($"Height = {_legLiftHeight}", 2);

                            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            break;
                        case SelectedIkFunction.SetFootHeightOffset:
                            _legPosY = _legPosY + 1;
                            _ik.RequestLegYHeight(_selectedLeg, _legPosY);
                            break;
                        case SelectedIkFunction.PingSetup:
                            _ik.RequestNewPerimeter(true);
                            break;
                        case SelectedIkFunction.BodyHeight:
                            _bodyPosY = _bodyPosY + 5;
                            if (_bodyPosY > 100)
                                _bodyPosY = 100;
                            _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
                            await _display.Write($"_bodyPosY = {_bodyPosY}", 2);
                            break;
                        case SelectedIkFunction.Posture:
                            _posture++;
                            await SetPosture();
                            break;
                        case SelectedIkFunction.Behavior:
                            _ik.RequestBehavior(_selectedBehavior, true);
                            await _display.Write($"{Enum.GetName(typeof(Behavior), _selectedBehavior)} start");
                            break;
                    }
                    break;
                case ControllerDirection.Down:
                    switch (_selectedIkFunction)
                    {
                        case SelectedIkFunction.LegLiftHeight:
                            _legLiftHeight--;

                            if (_legLiftHeight < LegLiftHeightLowerLimit)
                                _legLiftHeight = LegLiftHeightLowerLimit;

                            await _display.Write($"Height = {_legLiftHeight}", 2);

                            _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
                            break;
                        case SelectedIkFunction.SetFootHeightOffset:
                            _legPosY = _legPosY - 1;
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
                            await _display.Write($"_bodyPosY = {_bodyPosY}", 2);
                            break;
                        case SelectedIkFunction.Posture:
                            _posture--;
                            await SetPosture();
                            break;
                        case SelectedIkFunction.Behavior:
                            _ik.RequestBehavior(_selectedBehavior, false);
                            await _display.Write($"{Enum.GetName(typeof(Behavior), _selectedBehavior)} stop");
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
            switch (_selectedIkFunction)
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