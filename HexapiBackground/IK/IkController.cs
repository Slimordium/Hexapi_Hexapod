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

namespace HexapiBackground.IK
{
    internal class IkController
    {
        private Behavior _behavior;
        private bool _behaviorRunning;
        private PingDataEventArgs _pingDataEventArgs = new PingDataEventArgs(15, 20, 20, 20);

        private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(false);

        private readonly InverseKinematics _inverseKinematics;
        private readonly SparkFunSerial16X2Lcd _display;
        private readonly IoTClient _ioTClient;
        private readonly Gps.Gps _gps;

        private SerialDevice _serialDevice;
        private DataReader _arduinoDataReader;

        private int _perimeterInInches;

        private double _leftInches;
        private double _centerInches;
        private double _rightInches;

        internal static event EventHandler<PingDataEventArgs> CollisionEvent;
        internal static event EventHandler<YprDataEventArgs> YprEvent;

        private bool _isCollisionEvent;

        private double _yaw;
        private double _pitch;
        private double _roll;

        private SelectedIkFunction _selectedIkFunction;

        internal IkController(InverseKinematics inverseKinematics, 
                              SparkFunSerial16X2Lcd display, 
                              IoTClient ioTClient,
                              Gps.Gps gps)
        {
            _inverseKinematics = inverseKinematics;
            _display = display;
            _ioTClient = ioTClient;
            _gps = gps;

            _perimeterInInches = 15;

            _selectedIkFunction = SelectedIkFunction.BodyHeight;
            _behavior = Behavior.Bounce;
        }

        internal async Task<bool> Initialize()
        {
            _serialDevice = await StartupTask.SerialDeviceHelper.GetSerialDevice("AH03FK33", 57600, new TimeSpan(0, 0, 0, 1), new TimeSpan(0, 0, 0, 1));

            if (_serialDevice == null)
                return false;

            _arduinoDataReader = new DataReader(_serialDevice.InputStream);

            return true;
        }

        internal async Task Start()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (true)
            {
                if (_arduinoDataReader == null)
                {
                    continue;
                }

                var bytesRead = await _arduinoDataReader.LoadAsync(30).AsTask();

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

                    if (stopwatch.ElapsedMilliseconds >= 60)
                    {
                        var e = YprEvent;
                        e?.Invoke(null, new YprDataEventArgs {Yaw = _yaw, Pitch = _pitch, Roll = _roll});

                        stopwatch.Restart();
                    }

                    if (_selectedIkFunction == SelectedIkFunction.DisplayPing)
                        await _display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);

                    if (_selectedIkFunction == SelectedIkFunction.DisplayYPR)
                        await _display.Write($"{_yaw} {_pitch} {_roll}", 2);

                    if (_leftInches <= _perimeterInInches || 
                        _centerInches <= _perimeterInInches || 
                        _rightInches <= _perimeterInInches)
                    {
                        //await _display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);

                        _isCollisionEvent = true;

                        _pingDataEventArgs = new PingDataEventArgs(_perimeterInInches, _leftInches, _centerInches, _rightInches);

                        var e = CollisionEvent;
                        e?.Invoke(null, _pingDataEventArgs);
                    }
                    else
                    {
                        if (!_isCollisionEvent)
                            continue;

                        _isCollisionEvent = false;

                        _pingDataEventArgs = new PingDataEventArgs(_perimeterInInches, _leftInches, _centerInches, _rightInches);

                        var e = CollisionEvent;
                        e?.Invoke(null, _pingDataEventArgs);
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
            
            if (start)
                _manualResetEventSlim.Reset();
            else
                _manualResetEventSlim.Set();

            if (!start)
                return;

            switch (behavior)
            {
                case Behavior.Offensive:
                    break;
                case Behavior.Defensive:
                    break;
                case Behavior.Bounce:
                    await BehaviorBounce();
                    break;
                case Behavior.Balance:
                    break;
            }
        }

        private async Task BehaviorBounce()
        {
            if (_behaviorRunning)
                return;

            _behaviorRunning = true;

            var randomNumber = new Random(DateTime.Now.Millisecond);

            await _display.Write("Bounce started", 2);

            RequestSetMovement(true);
            RequestSetGaitType(GaitType.Tripod8);

            double travelLengthZ = 40;
            double travelLengthX = 0;
            double travelRotationY = 0;
            double gaitSpeed = 50;

            while (!_manualResetEventSlim.IsSet)
            {
                await Task.Delay(100);

                if (_leftInches > _perimeterInInches && _centerInches > _perimeterInInches && _rightInches > _perimeterInInches)
                {
                    await _display.Write("Forward", 2);

                    travelLengthZ = -50;
                    travelLengthX = 0;
                    travelRotationY = 0;
                }

                if (_leftInches <= _perimeterInInches && _rightInches > _perimeterInInches)
                {
                    await _display.Write("Turn Left", 2);

                    travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;
                    travelLengthX = 0;
                    travelRotationY = -30;
                }

                if (_leftInches > _perimeterInInches && _rightInches <= _perimeterInInches)
                {
                    await _display.Write("Turn Right", 2);

                    travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;
                    travelLengthX = 0;
                    travelRotationY = 30;
                }

                if (_leftInches <= _perimeterInInches && _rightInches <= _perimeterInInches)
                {
                    travelLengthX = 0;
                    travelRotationY = 0;

                    if (_manualResetEventSlim.IsSet)
                    {
                        RequestMovement(50, 0, 0, 0);
                        _behaviorRunning = false;
                        return;
                    }

                    if (_centerInches < _perimeterInInches)
                    {
                        if (_manualResetEventSlim.IsSet)
                        {
                            RequestMovement(50, 0, 0, 0);
                            _behaviorRunning = false;
                            return;
                        }

                        await _display.Write("Reverse", 2);

                        travelLengthZ = 30; //Reverse
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        if (_manualResetEventSlim.IsSet)
                        {
                            RequestMovement(50, 0, 0, 0);
                            _behaviorRunning = false;
                            return;
                        }

                        travelLengthZ = 0;

                        if (randomNumber.Next(0, 5) >= 3)
                        {
                            await _display.Write("Turn Right", 2);
                            travelRotationY = 30;
                        }
                        else
                        {
                            await _display.Write("Turn Left", 2);
                            travelRotationY = -30;
                        }

                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
                        await Task.Delay(2000);

                        if (_manualResetEventSlim.IsSet)
                        {
                            RequestMovement(50, 0, 0, 0);
                            _behaviorRunning = false;
                            return;
                        }

                        await _display.Write("Stop", 2);

                        travelLengthX = 0;
                        travelRotationY = 0;

                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        continue;
                    }
                }

                if (_manualResetEventSlim.IsSet)
                {
                    RequestMovement(50, 0, 0, 0);
                    _behaviorRunning = false;
                    return;
                }

                RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
            }

            _behaviorRunning = false;
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

            _selectedIkFunction = selectedIkFunction;

            if (_selectedIkFunction == SelectedIkFunction.DisplayCoordinate)
                _gps.DisplayCoordinates();
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
            await _display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);
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
                    double ping;

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

                        if (double.TryParse(data, out ping))
                            _leftInches = GetInchesFromPingDuration(ping);

                        continue;
                    }
                    if (data.Contains('C'))
                    {
                        data = data.Replace("C", "");

                        if (double.TryParse(data, out ping))
                            _centerInches = GetInchesFromPingDuration(ping);

                        continue;
                    }
                    if (data.Contains('R'))
                    {
                        data = data.Replace("R", "");

                        if (double.TryParse(data, out ping))
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

        private static int GetInchesFromPingDuration(double duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round(duration / 73.746 / 2, 1));
        }

    }
}