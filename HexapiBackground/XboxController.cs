using System;
using System.Diagnostics.CodeAnalysis;
using Windows.Devices.HumanInterfaceDevice;

namespace HexapiBackground
{
    internal sealed class XboxController
    {
        public delegate void ButtonChangedHandler(int button);

        public delegate void DirectionChangedHandler(ControllerVector sender);

        public delegate void TriggerChangedHandler(int trigger);

        private const double DeadzoneTolerance = 6000;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly HidDevice _deviceHandle;

        private ControllerVector _dpadDirectionVector = new ControllerVector();
        private ControllerVector _leftStickDirectionVector = new ControllerVector();

        private int _leftTrigger;
        private ControllerVector _rightStickDirectionVector = new ControllerVector();
        private int _rightTrigger;

        /// <summary>
        ///     Initializes a new instance of the XboxHidController class from a
        ///     HidDevice handle
        /// </summary>
        /// <param name="deviceHandle">Handle to the HidDevice</param>
        public XboxController(HidDevice deviceHandle)
        {
            _deviceHandle = deviceHandle;

            _deviceHandle.InputReportReceived -= InputReportReceived;
            _deviceHandle.InputReportReceived += InputReportReceived;
        }

        private void InputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            var dPad = (int) args.Report.GetNumericControl(0x01, 0x39).Value;

            var lstickX = args.Report.GetNumericControl(0x01, 0x30).Value - 32768;
            var lstickY = args.Report.GetNumericControl(0x01, 0x31).Value - 32768;

            var rstickX = args.Report.GetNumericControl(0x01, 0x33).Value - 32768;
            var rstickY = args.Report.GetNumericControl(0x01, 0x34).Value - 32768;

            var lt = (int) Math.Max(0, args.Report.GetNumericControl(0x01, 0x32).Value - 32768);
            var rt = (int) Math.Max(0, -1*(args.Report.GetNumericControl(0x01, 0x32).Value - 32768));

            foreach (var btn in args.Report.ActivatedBooleanControls) //Start = 7, Back = 6
            {
                var id = (int) (btn.Id - 5);

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
            }

            if (_rightTrigger != rt)
            {
                RightTriggerChanged?.Invoke(rt);
                _rightTrigger = rt;
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
            }

            vector = new ControllerVector
            {
                Direction = (ControllerDirection) dPad,
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
        private static int GetMagnitude(double x, double y)
        {
            var magnitude = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));

            if (magnitude < DeadzoneTolerance)
                magnitude = 0;
            else
            {
                // Scale so deadzone is removed, and max value is 10000
                magnitude = (magnitude - DeadzoneTolerance)/(32768 - DeadzoneTolerance)*10000;
                if (magnitude > 10000)
                    magnitude = 10000;
            }

            return (int) magnitude;
        }

        /// <summary>
        ///     Converts thumbstick X/Y coordinates centered at (0,0) to a direction
        /// </summary>
        /// <param name="x">Horizontal coordinate</param>
        /// <param name="y">Vertical coordinate</param>
        /// <returns>Direction that the coordinates resolve to</returns>
        private static ControllerDirection CoordinatesToDirection(double x, double y)
        {
            var radians = Math.Atan2(y, x);
            var orientation = radians*(180/Math.PI);

            orientation = orientation
                          + 180 // adjust so values are 0-360 rather than -180 to 180
                          + 22.5 // offset so the middle of each direction has a +/- 22.5 buffer
                          + 270; // adjust so when dividing by 45, up is 1

            orientation = orientation%360;

            // Dividing by 45 should chop the orientation into 8 chunks, which 
            // maps 0 to Up.  Shift that by 1 since we need 1-8.
            var direction = (int) (orientation/45) + 1;

            return (ControllerDirection) direction;
        }

        public event ButtonChangedHandler FunctionButtonChanged;

        public event ButtonChangedHandler BumperButtonChanged;

        public event DirectionChangedHandler LeftDirectionChanged;

        public event DirectionChangedHandler RightDirectionChanged;

        public event DirectionChangedHandler DpadDirectionChanged;

        public event TriggerChangedHandler LeftTriggerChanged;

        public event TriggerChangedHandler RightTriggerChanged;
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
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

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var otherVector = obj as ControllerVector;

            return otherVector != null && Magnitude == otherVector.Magnitude && Direction == otherVector.Direction;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            // disable overflow
            unchecked
            {
                var hash = 27;
                hash = 13*hash + Direction.GetHashCode();
                hash = 13*hash + Magnitude.GetHashCode();
                return hash;
            }
        }
    }

    public enum ControllerDirection
    {
        None = 0,
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft
    }
}