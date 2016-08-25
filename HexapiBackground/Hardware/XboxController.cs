using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace HexapiBackground.Hardware
{
    internal sealed class XboxController
    {
        internal delegate void ButtonChangedHandler(int button);
        internal delegate void DirectionChangedHandler(ControllerVector sender);
        internal delegate void TriggerChangedHandler(int trigger);
        internal static event ButtonChangedHandler FunctionButtonChanged;
        internal static event ButtonChangedHandler BumperButtonChanged;
        internal static event DirectionChangedHandler LeftDirectionChanged;
        internal static event DirectionChangedHandler RightDirectionChanged;
        internal static event DirectionChangedHandler DpadDirectionChanged;
        internal static event TriggerChangedHandler LeftTriggerChanged;
        internal static event TriggerChangedHandler RightTriggerChanged;
        private static double _deadzoneTolerance = 5000; //Was 1000
        private static HidDevice _deviceHandle;
        private static ControllerVector _dpadDirectionVector = new ControllerVector();
        private static ControllerVector _leftStickDirectionVector = new ControllerVector();
        private static ControllerVector _rightStickDirectionVector = new ControllerVector();
        private static int _rightTrigger;
        private static int _leftTrigger;
        private static SparkFunSerial16X2Lcd _display;

        //private Timer _disconnectTimer = new Timer(DisconnectCheckTimer, null, 0, 500);

        /// <summary>
        /// True when connected
        /// </summary>
        internal static event EventHandler<DisconnectedEventArgs> DisconnectedEvent;

        private static bool _isConnected;

        internal XboxController(SparkFunSerial16X2Lcd display)
        {
            _display = display;
        }

        internal async Task<bool> InitializeAsync()
        {
            //USB\VID_045E&PID_0719\E02F1950 - receiver
            //USB\VID_045E & PID_02A1 & IG_00\6 & F079888 & 0 & 00  - XboxController
            //0x01, 0x05 = game controllers

            DeviceInformationCollection deviceInformationCollection = null;

            while (true)
            {
                deviceInformationCollection = await GetDeviceInformationCollection();

                if (deviceInformationCollection != null && deviceInformationCollection.Count > 0)
                    break;

                await Task.Delay(5000);
            }

            await ConnectToController(deviceInformationCollection);

            return true;
        }

        private static async Task ConnectToController(DeviceInformationCollection deviceInformationCollection)
        {
            foreach (var d in deviceInformationCollection)
            {
                _deviceHandle = await HidDevice.FromIdAsync(d.Id, FileAccessMode.Read);

                if (_deviceHandle == null)
                {
                    await _display.WriteAsync("No Xbox controller");
                    continue;
                }

                _deviceHandle.InputReportReceived += InputReportReceived;

                _isConnected = true;
                break;
            }
        }

        private async Task<DeviceInformationCollection> GetDeviceInformationCollection()
        {
            var deviceInformationCollection = await DeviceInformation.FindAllAsync(HidDevice.GetDeviceSelector(0x01, 0x05));

            if (deviceInformationCollection.Count == 0)
                await _display.WriteAsync("No Xbox controller");

            return deviceInformationCollection;
        }

        private static async void DisconnectCheckTimer(object sender)
        {
            var deviceInformationCollection = await DeviceInformation.FindAllAsync(HidDevice.GetDeviceSelector(0x01, 0x05));

            if (_isConnected && deviceInformationCollection.Count == 0)
            {
                _deviceHandle.Dispose();
                _deviceHandle = null;

                _isConnected = false;
                DisconnectedEvent?.Invoke(null, new DisconnectedEventArgs { IsConnected = false });
            }

            if (!_isConnected && deviceInformationCollection.Count > 0)
            {
                await ConnectToController(deviceInformationCollection);

                DisconnectedEvent?.Invoke(null, new DisconnectedEventArgs { IsConnected = true });
            }
        }

        private static void InputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            var dPad = (int)args.Report.GetNumericControl(0x01, 0x39).Value;

            var lstickX = args.Report.GetNumericControl(0x01, 0x30).Value - 32768;
            var lstickY = args.Report.GetNumericControl(0x01, 0x31).Value - 32768;

            var rstickX = args.Report.GetNumericControl(0x01, 0x33).Value - 32768;
            var rstickY = args.Report.GetNumericControl(0x01, 0x34).Value - 32768;

            var lt = (int)Math.Max(0, args.Report.GetNumericControl(0x01, 0x32).Value - 32768);
            var rt = (int)Math.Max(0, -1 * (args.Report.GetNumericControl(0x01, 0x32).Value - 32768));

            foreach (var btn in args.Report.ActivatedBooleanControls) //StartAsync = 7, Back = 6
            {
                var id = (int)(btn.Id - 5);

                if (id < 4)
                    FunctionButtonChanged?.Invoke(id);
                else if (id >= 4 && id < 6)
                    BumperButtonChanged?.Invoke(id);
                else
                    FunctionButtonChanged?.Invoke(id);
            }

            if (_leftTrigger != lt)
            {
                LeftTriggerChanged?.Invoke(lt);
                _leftTrigger = lt;
                return;
            }

            if (_rightTrigger != rt)
            {
                RightTriggerChanged?.Invoke(rt);
                _rightTrigger = rt;
                return;
            }

            var lStickMagnitude = GetMagnitude(lstickX, lstickY);
            var rStickMagnitude = GetMagnitude(rstickX, rstickY);

            var vector = new ControllerVector
            {
                Direction = CoordinatesToDirection(lstickX, lstickY),
                Magnitude = lStickMagnitude
            };

            if (!_leftStickDirectionVector.Equals(vector) && LeftDirectionChanged != null)
            {
                _leftStickDirectionVector = vector;
                LeftDirectionChanged(_leftStickDirectionVector);
                return;
            }

            vector = new ControllerVector
            {
                Direction = CoordinatesToDirection(rstickX, rstickY),
                Magnitude = rStickMagnitude
            };

            if (!_rightStickDirectionVector.Equals(vector) && RightDirectionChanged != null)
            {
                _rightStickDirectionVector = vector;
                RightDirectionChanged(_rightStickDirectionVector);
                return;
            }

            vector = new ControllerVector
            {
                Direction = (ControllerDirection)dPad,
                Magnitude = 10000
            };

            if (_dpadDirectionVector.Equals(vector) || DpadDirectionChanged == null)
                return;

            _dpadDirectionVector = vector;
            DpadDirectionChanged(vector);
        }

        /// <summary>
        ///     Gets the magnitude of the vector formed by the X/Y coordinates
        /// </summary>
        /// <param name="x">Horizontal coordinate</param>
        /// <param name="y">Vertical coordinate</param>
        /// <returns>True if the coordinates are inside the dead zone</returns>
        internal static int GetMagnitude(double x, double y)
        {
            var magnitude = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));

            if (magnitude < _deadzoneTolerance)
                magnitude = 0;
            else
            {
                // Scale so deadzone is removed, and max value is 10000
                magnitude = (magnitude - _deadzoneTolerance) / (32768 - _deadzoneTolerance) * 10000;
                if (magnitude > 10000)
                    magnitude = 10000;
            }

            return (int)magnitude;
        }

        /// <summary>
        ///     Converts thumbstick X/Y coordinates centered at (0,0) to a direction
        /// </summary>
        /// <param name="x">Horizontal coordinate</param>
        /// <param name="y">Vertical coordinate</param>
        /// <returns>Direction that the coordinates resolve to</returns>
        internal static ControllerDirection CoordinatesToDirection(double x, double y)
        {
            var radians = Math.Atan2(y, x);
            var orientation = radians * (180 / Math.PI);

            orientation = orientation
                          + 180 // adjust so values are 0-360 rather than -180 to 180
                          + 22.5 // offset so the middle of each direction has a +/- 22.5 buffer
                          + 270; // adjust so when dividing by 45, up is 1

            orientation = orientation % 360;

            // Dividing by 45 should chop the orientation into 8 chunks, which 
            // maps 0 to Up.  Shift that by 1 since we need 1-8.
            var direction = (int)(orientation / 45) + 1;

            return (ControllerDirection)direction;
        }
    }

    internal sealed class ControllerVector
    {
        public ControllerVector()
        {
            Direction = ControllerDirection.None;
            Magnitude = 0;
        }

        /// <summary>
        ///     Get what direction the XboxController is pointing
        /// </summary>
        public ControllerDirection Direction { get; set; }

        /// <summary>
        ///     Gets a value indicating the magnitude of the direction
        /// </summary>
        public int Magnitude { get; set; }

        public new bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var otherVector = obj as ControllerVector;

            return otherVector != null && Magnitude == otherVector.Magnitude && Direction == otherVector.Direction;
        }
    }

    internal class DisconnectedEventArgs : EventArgs
    {
        internal bool IsConnected { get; set; } = false;
    }

}