using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
//using HexapiBackground.Iot;

// ReSharper disable FunctionNeverReturns

namespace HexapiBackground.IK
{
    internal class IkController
    {
        private Behavior _behavior;
        private bool _behaviorRunning;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly InverseKinematics _inverseKinematics;
        private readonly SparkFunSerial16X2Lcd _display;
        //private readonly IoTClient _ioTClient;
        private readonly Hardware.Gps _gps;

        private SerialDevice _serialDevice;
        private DataReader _arduinoDataReader;

        private int _perimeterInInches = 20;

        private double _leftInches;
        private double _farLeftInches;
        private double _centerInches;
        private double _rightInches;
        private double _farRightInches;

        internal static event EventHandler<RangeDataEventArgs> RangingEvent;
        internal static event EventHandler<ImuDataEventArgs> ImuEvent;

        private double _yaw;
        private double _pitch;
        private double _roll;

        private double _accelX;
        private double _accelY;
        private double _accelZ;

        private SelectedIkFunction _selectedIkFunction;

        internal IkController(InverseKinematics inverseKinematics, 
                              SparkFunSerial16X2Lcd display, 
                              Gps gps,
                              DataReader dataReader)
        {
            _inverseKinematics = inverseKinematics;
            _display = display;
            //_ioTClient = ioTClient;
            _gps = gps;

            _selectedIkFunction = SelectedIkFunction.BodyHeight;
            _behavior = Behavior.Bounce;

            _arduinoDataReader = dataReader;
        }

        internal async Task<bool> InitializeAsync()
        {
            _serialDevice = await StartupTask.SerialDeviceHelper.GetSerialDeviceAsync("AH03FK33", 57600, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

            if (_serialDevice == null)
                return false;

            _arduinoDataReader = new DataReader(_serialDevice.InputStream) {InputStreamOptions = InputStreamOptions.Partial};

            return true;
        }

        internal async Task StartAsync()
        {
            var imuEventTimer = new Timer(ImuEventTimerCallback, null, 0, 20);
            var displayTimer = new Timer(DisplayTimerCallback, null, 0, 50);
            var rangeTimer = new Timer(RangeTimerCallback, null, 0, 20);

            while (true)
            {
                if (_arduinoDataReader == null)
                {
                    await Task.Delay(500);
                    continue;
                }

                var numBytesIn = await _arduinoDataReader.LoadAsync(32).AsTask();
                var bytesIn = new byte[numBytesIn];
                _arduinoDataReader.ReadBytes(bytesIn);

                try
                {
                    var rawData = Encoding.ASCII.GetString(bytesIn).Replace("\0", "").Replace("\r", "");

                    foreach (var ds in rawData.Split('#'))
                    {
                        if (string.IsNullOrEmpty(ds))
                            continue;

                        Parse(ds);
                    }
                }
                catch
                {
                    //
                }
            }
        }

        private void ImuEventTimerCallback(object state)
        {
            ImuEvent?.Invoke(null, new ImuDataEventArgs { Yaw = _yaw, Pitch = _pitch, Roll = _roll, AccelX = _accelX, AccelY = _accelY, AccelZ = _accelZ });
        }

        private async void DisplayTimerCallback(object state)
        {
            if (_selectedIkFunction == SelectedIkFunction.DisplayPing)
            {
                await _display.WriteAsync($"{_leftInches} {_centerInches} {_rightInches}", 2);
            }

            //if (_selectedIkFunction == SelectedIkFunction.)
            //{
            //    await _display.WriteAsync($"{_yaw} {_pitch} {_roll}", 2);
            //}

            //if (_selectedIkFunction == SelectedIkFunction.)
            //{
            //    await _display.WriteAsync($"{_accelY}", 2);
            //}
        }

        private void RangeTimerCallback(object state)
        {
            lock (_lock)
                RangingEvent?.Invoke(null, new RangeDataEventArgs(_perimeterInInches, _leftInches, _centerInches, _rightInches, _farLeftInches, _farRightInches));
        }

        //internal async void RequestBehavior(Behavior behavior, bool start)
        //{
        //    _behavior = behavior;

        //    if (start)
        //    {
        //        _cancellationTokenSource = new CancellationTokenSource();
        //    }
        //    else
        //    {
        //        _cancellationTokenSource.Cancel();
        //    }

        //    if (!start)
        //        return;

        //    switch (behavior)
        //    {
        //        case Behavior.Offensive:
        //            break;
        //        case Behavior.Defensive:
        //            break;
        //        case Behavior.Bounce:
        //            await BehaviorBounceAsync(_cancellationTokenSource.Token);
        //            break;
        //        case Behavior.Balance:
        //            break;
        //    }
        //}

        //private async Task BehaviorBounceAsync(CancellationToken cancellationToken)
        //{
        //    if (_behaviorRunning)
        //        return;

        //    _behaviorRunning = true;

        //    var randomNumber = new Random(DateTime.Now.Millisecond);

        //    await _display.WriteAsync("Bounce started", 2);

        //    RequestSetMovement(true);
        //    RequestSetGaitType(GaitType.Tripod8);

        //    double travelLengthZ = 40;
        //    double travelLengthX = 0;
        //    double travelRotationY = 0;
        //    double gaitSpeed = 50;

        //    while (!cancellationToken.IsCancellationRequested)
        //    {
        //        await Task.Delay(100, cancellationToken);

        //        if (_leftInches > _perimeterInInches && _centerInches > _perimeterInInches && _rightInches > _perimeterInInches)
        //        {
        //            await _display.WriteAsync("Forward", 2);

        //            travelLengthZ = -50;
        //            travelLengthX = 0;
        //            travelRotationY = 0;
        //        }

        //        if (_leftInches <= _perimeterInInches && _rightInches > _perimeterInInches)
        //        {
        //            await _display.WriteAsync("Left", 2);

        //            travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;
        //            travelLengthX = 0;
        //            travelRotationY = -30;
        //        }

        //        if (_leftInches > _perimeterInInches && _rightInches <= _perimeterInInches)
        //        {
        //            await _display.WriteAsync("Right", 2);

        //            travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;
        //            travelLengthX = 0;
        //            travelRotationY = 30;
        //        }
        //        else if (_leftInches > _perimeterInInches && _rightInches <= _perimeterInInches)
        //        {
        //            await _display.WriteAsync("Right", 2);

        //            travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;
        //            travelLengthX = 0;
        //            travelRotationY = 30;
        //        }

        //        if (_leftInches <= _perimeterInInches && _rightInches <= _perimeterInInches)
        //        {
        //            travelLengthX = 0;
        //            travelRotationY = 0;

        //            if (cancellationToken.IsCancellationRequested)
        //            {
        //                RequestMovement(50, 0, 0, 0);
        //                _behaviorRunning = false;
        //                return;
        //            }

        //            if (_centerInches < _perimeterInInches)
        //            {
        //                if (cancellationToken.IsCancellationRequested)
        //                {
        //                    RequestMovement(50, 0, 0, 0);
        //                    _behaviorRunning = false;
        //                    return;
        //                }

        //                await _display.WriteAsync("Reverse", 2);

        //                travelLengthZ = 30; //Reverse
        //                RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

        //                await Task.Delay(6000, cancellationToken);

        //                if (cancellationToken.IsCancellationRequested)
        //                {
        //                    RequestMovement(50, 0, 0, 0);
        //                    _behaviorRunning = false;
        //                    return;
        //                }

        //                travelLengthZ = 0;

        //                if (randomNumber.Next(0, 5) >= 3)
        //                {
        //                    await _display.WriteAsync("Turn Right", 2);
        //                    travelRotationY = 30;

        //                    RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

        //                    var targetYaw = _yaw + 5;

        //                    if (targetYaw > 359)
        //                        targetYaw = targetYaw - 359;
                            
        //                    while (_yaw < targetYaw)
        //                    {
                                
        //                    }
        //                }
        //                else
        //                {
        //                    await _display.WriteAsync("Turn Left", 2);
        //                    travelRotationY = -30;

        //                    RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

        //                    var targetYaw = _yaw - 5;

        //                    if (targetYaw < 0)
        //                        targetYaw = targetYaw + 359;

        //                    while (_yaw > targetYaw)
        //                    {

        //                    }
        //                }

        //                //RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
        //                //await Task.Delay(2000);

        //                if (cancellationToken.IsCancellationRequested)
        //                {
        //                    RequestMovement(50, 0, 0, 0);
        //                    _behaviorRunning = false;
        //                    return;
        //                }

        //                await _display.WriteAsync("Stop", 2);

        //                travelLengthX = 0;
        //                travelRotationY = 0;

        //                RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

        //                continue;
        //            }
        //        }

        //        if (cancellationToken.IsCancellationRequested)
        //        {
        //            RequestMovement(50, 0, 0, 0);
        //            _behaviorRunning = false;
        //            return;
        //        }

        //        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
        //    }

        //    _behaviorRunning = false;
        //}

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
            await _display.WriteAsync(enabled ? "Servos on" : "Servos off", 2);
        }

        internal async Task RequestSetFunctionAsync(SelectedIkFunction selectedIkFunction)
        {
            _inverseKinematics.RequestSetFunction(selectedIkFunction);

            _selectedIkFunction = selectedIkFunction;

            //if (_selectedIkFunction == SelectedIkFunction.DisplayCoordinate)
            //    await _gps.DisplayCoordinates();
        }

        internal async void RequestLegYHeight(int leg, double yPos)
        {
            _inverseKinematics.RequestLegYHeight(leg, yPos);
            await _display.WriteAsync($"Leg {leg} - {yPos}", 2);
        }

        internal async void RequestNewPerimeter(bool increase)
        {
            if (increase)
                _perimeterInInches++;
            else
                _perimeterInInches--;

            if (_perimeterInInches < 5)
                _perimeterInInches = 5;

            await _display.WriteAsync($"Perimeter {_perimeterInInches}", 1);
            await _display.WriteAsync($"{_leftInches} {_centerInches} {_rightInches}", 2);
        }

        object _lock = new object();

        internal void Parse(string data)
        {
            lock (_lock)
            try
            {

                if (!data.EndsWith("\n"))
                    return;

                var newData = data.Replace("L", string.Empty).Replace("C", string.Empty).Replace("R", string.Empty).Replace("\n", string.Empty);

                double ping;

                if (!double.TryParse(newData, out ping))
                    return;

                if (ping <= 1)
                    return;

                var inches = GetInchesFromPingDuration(ping);

                if (data.StartsWith("R"))
                {
                    _rightInches = inches;
                }
                else if (data.StartsWith("C"))
                {
                    _centerInches = inches;
                }
                else if (data.StartsWith("L"))
                {
                    _leftInches = inches;
                }
            }
            catch
            {
                //
            }
        }

        private static double GetInchesFromPingDuration(double duration) //73.746 microseconds per inch
        {
            return Math.Round(duration / 73.746 / 2, 1);
        }

    }
}