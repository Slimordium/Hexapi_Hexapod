using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;

// ReSharper disable FunctionNeverReturns
#pragma warning disable 4014

namespace HexapiBackground.IK
{
    internal class IkController
    {
        private Behavior _behavior = Behavior.Avoid;
        private bool _behaviorStarted;
        private PingDataEventArgs _pingDataEventArgs = new PingDataEventArgs(15, 20, 20, 20);

        private readonly InverseKinematics _inverseKinematics;
        private readonly SparkFunSerial16X2Lcd _display;

        private readonly SerialDevice _serialDevice;
        private DataReader _arduinoDataReader;

        private int _perimeterInInches;
        private int _leftInches;
        private int _centerInches;
        private int _rightInches;

        public static event EventHandler<PingDataEventArgs> CollisionEvent;
        public static event EventHandler<YprDataEventArgs> YprEvent;

        private SerialDevice _arduinoSerialDevice;

        private readonly SerialDeviceHelper _serialDeviceHelper;

        private bool _isCollisionEvent;

        double _yaw = 0;
        double _pitch = 0;
        double _roll = 0;

        internal IkController(InverseKinematics inverseKinematics, SparkFunSerial16X2Lcd display, SerialDeviceHelper serialDeviceHelper)
        {
            _inverseKinematics = inverseKinematics;
            _display = display;

            _perimeterInInches = 15;

            _leftInches = _perimeterInInches + 5;
            _centerInches = _perimeterInInches + 5;
            _rightInches = _perimeterInInches + 5;
        }

        internal async Task<bool> Initialize()
        {
             _arduinoSerialDevice = await _serialDeviceHelper.GetSerialDevice("AH03FK33", 57600, new TimeSpan(0, 0, 0, 1), new TimeSpan(0, 0, 0, 1));

            if (_serialDevice == null)
                return false;

            _arduinoDataReader = new DataReader(_serialDevice.InputStream);

            return true;
        }

        internal async Task Start()
        {
            while (true)
            {
                if (_arduinoDataReader == null)
                {
                    continue;
                }

                var bytesRead = await _arduinoDataReader.LoadAsync(60).AsTask();

                if (bytesRead == 0)
                    continue;

                try
                {
                    var inbytes = new byte[bytesRead];
                    _arduinoDataReader.ReadBytes(inbytes);

                    var pingData = Encoding.ASCII.GetString(inbytes);

                    if (string.IsNullOrEmpty(pingData))
                        continue;

                    if (Parse(pingData.Split('!')) <= 0)
                        continue;

                    if (_leftInches <= _perimeterInInches || _centerInches <= _perimeterInInches || _rightInches <= _perimeterInInches)
                    {
                        await _display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);

                        _isCollisionEvent = true;

                        var e = CollisionEvent;
                        e?.Invoke(null, new PingDataEventArgs(_perimeterInInches, _leftInches, _centerInches, _rightInches));
                    }
                    else
                    {
                        if (!_isCollisionEvent)
                            continue;

                        _isCollisionEvent = false;

                        var e = CollisionEvent;
                        e?.Invoke(null, new PingDataEventArgs(_perimeterInInches, _leftInches, _centerInches, _rightInches));
                    }
                }
                catch
                {
                    //
                }
            }
        }

        internal async void RequestBehavior(Behavior behavior, bool start)
        {
            _behavior = behavior;
            _behaviorStarted = start;

            switch (behavior)
            {
                case Behavior.Offensive:
                    break;
                case Behavior.Defensive:
                    break;
                case Behavior.Bounce:
                    await BehaviorBounce().ConfigureAwait(false);
                    break;
                default:
                    _behaviorStarted = false;

                    break;
            }
        }

        private async Task BehaviorBounce()
        {
            await _display.Write("Bounce started", 2);

            RequestSetMovement(true);
            RequestSetGaitType(GaitType.Tripod8);

            double travelLengthZ = 40;
            double travelLengthX = 0;
            double travelRotationY = 0;
            double gaitSpeed = 45;

            while (_behaviorStarted)
            {
                await Task.Delay(100);

                if (_pingDataEventArgs.LeftInches > _perimeterInInches && _pingDataEventArgs.CenterInches > _perimeterInInches && _pingDataEventArgs.RightInches > _perimeterInInches)
                {
                    await _display.Write("Forward", 2);

                    travelLengthZ = -50;
                    travelLengthX = 0;
                    travelRotationY = 0;
                }

                if (_pingDataEventArgs.LeftInches <= _perimeterInInches && _pingDataEventArgs.RightInches > _perimeterInInches)
                {
                    await _display.Write("Turn Right", 2);

                    travelLengthZ = _pingDataEventArgs.CenterInches > _perimeterInInches ? -20 : 0;

                    travelLengthX = 0;
                    travelRotationY = -30;
                }

                if (_pingDataEventArgs.LeftInches > _perimeterInInches && _pingDataEventArgs.RightInches <= _perimeterInInches)
                {
                    await _display.Write("Turn Left", 2);

                    travelLengthZ = _pingDataEventArgs.CenterInches > _perimeterInInches ? -20 : 0;

                    travelLengthX = 0;
                    travelRotationY = 30;
                }

                if (_pingDataEventArgs.LeftInches <= _perimeterInInches && _pingDataEventArgs.RightInches <= _perimeterInInches)
                {
                    travelLengthX = 0;
                    travelRotationY = 0;

                    if (_pingDataEventArgs.CenterInches < _perimeterInInches)
                    {
                        await _display.Write("Reverse", 2);

                        travelLengthZ = 30; //Reverse
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        await _display.Write("Turn Left", 2);
                        travelLengthZ = 0;
                        travelRotationY = 30;
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        await _display.Write("Stop", 2);

                        travelLengthZ = 0;
                        travelLengthX = 0;
                        travelRotationY = 0;

                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
                        
                        continue;
                    }
                }
                
                RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gaitSpeed"></param>
        /// <param name="travelLengthX"></param>
        /// <param name="travelLengthZ">Negative numbers equals forward movement</param>
        /// <param name="travelRotationY"></param>
        internal void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            _inverseKinematics.RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
        }

        internal void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ, double bodyPosY, double bodyRotY1)
        {
            _inverseKinematics.RequestBodyPosition(bodyRotX1, bodyRotZ1, bodyPosX, bodyPosZ, bodyPosY, bodyRotY1);
        }

        internal void RequestSetGaitOptions(double gaitSpeed, double legLiftHeight)
        {
            _inverseKinematics.RequestSetGaitOptions(gaitSpeed, legLiftHeight);
        }

        internal void RequestSetGaitType(GaitType gaitType)
        {
            _inverseKinematics.RequestSetGaitType(gaitType);
        }

        internal async void RequestSetMovement(bool enabled)
        {
            _inverseKinematics.RequestSetMovement(enabled);
            await _display.Write(enabled ? "Servos on" : "Servos off", 2);
        }

        internal void RequestSetFunction(SelectedIkFunction selectedIkFunction)
        {
            _inverseKinematics.RequestSetFunction(selectedIkFunction);
        }

        internal async void RequestLegYHeight(int leg, double yPos)
        {
            _inverseKinematics.RequestLegYHeight(leg, yPos);
            await _display.Write($"Leg {leg} - {yPos}", 2);
        }

        internal async void RequestNewPerimeter(bool increase)
        {
            if (increase)
                _perimeterInInches++;
            else
                _perimeterInInches--;

            if (_perimeterInInches < 1)
                _perimeterInInches = 1;

            await _display.Write($"Perimeter {_perimeterInInches}", 1);
            await _display.Write($"{_pingDataEventArgs.LeftInches} {_pingDataEventArgs.CenterInches} {_pingDataEventArgs.RightInches}", 2);
        }

        internal int Parse(string[] ranges)
        {
            var success = ranges.Length;

            foreach (var d in ranges)
            {
                if (string.IsNullOrEmpty(d) || !d.Contains('?'))
                    continue;

                var data = d.Replace('?', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

                try
                {
                    int ping;

                    if (data.Contains("YPR")) //"#YPR=58.29,1.00,-7.29"
                    {
                        data = data.Replace("#YPR=", "");

                        var yprArray = data.Split(',');

                        if (yprArray.Length >= 1)
                            double.TryParse(yprArray[0], out _yaw);

                        if (yprArray.Length >= 2)
                            double.TryParse(yprArray[1], out _pitch);

                        if (yprArray.Length >= 3)
                            double.TryParse(yprArray[2], out _roll);

                        continue;
                    }

                    if (data.Contains('L'))
                    {
                        data = data.Replace("L", "");

                        if (int.TryParse(data, out ping))
                            _leftInches = GetInchesFromPingDuration(ping);

                        continue;
                    }
                    if (data.Contains('C'))
                    {
                        data = data.Replace("C", "");

                        if (int.TryParse(data, out ping))
                            _centerInches = GetInchesFromPingDuration(ping);

                        continue;
                    }
                    if (data.Contains('R'))
                    {
                        data = data.Replace("R", "");

                        if (int.TryParse(data, out ping))
                            _rightInches = GetInchesFromPingDuration(ping);
                    }
                }
                catch
                {
                    success--;
                }
            }

            return success;
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round(duration / 73.746 / 2, 1));
        }

    }
}