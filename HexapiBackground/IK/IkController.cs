using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
// ReSharper disable FunctionNeverReturns
#pragma warning disable 4014

namespace HexapiBackground.IK
{

    internal class PingData : EventArgs
    {
        internal PingData(int perimeterInInches, int left, int center, int right)
        {
            LeftWarning = left <= perimeterInInches + 5;
            CenterWarning = center <= perimeterInInches + 5;
            RightWarning = right <= perimeterInInches + 5;

            LeftBlocked = left <= perimeterInInches;
            CenterBlocked = center <= perimeterInInches;
            RightBlocked = right <= perimeterInInches;

            LeftInches = left;
            CenterInches = center;
            RightInches = right;
        }

        public bool LeftWarning { get; private set; }
        public bool CenterWarning { get; private set; }
        public bool RightWarning { get; private set; }

        public bool LeftBlocked { get; private set; }
        public bool CenterBlocked { get; private set; }
        public bool RightBlocked { get; private set; }

        public int LeftInches { get; private set; }
        public int CenterInches { get; private set; }
        public int RightInches { get; private set; }
    }

    /// <summary>
    /// This semi-protects against running the robot into things
    /// </summary>
    internal class IkController
    {
        private int _perimeterInInches; 
        private int _leftInches;
        private int _centerInches;
        private int _rightInches;

        private double _yaw;
        private double _pitch;
        private double _roll;

        private Behavior _behavior = Behavior.Avoid;
        private bool _behaviorStarted;

        private readonly InverseKinematics _inverseKinematics;
        private SerialDevice _arduino;
        private SerialDevice _sparkFunRazorMpu;
        private DataReader _arduinoDataReader;
        private DataReader _razorDataReader;

        public static event EventHandler<PingData> CollisionEvent;

        internal IkController(InverseKinematics inverseKinematics)
        {
            _inverseKinematics = inverseKinematics;
            _perimeterInInches = 18;

            _leftInches = _perimeterInInches + 5;
            _centerInches = _perimeterInInches + 5;
            _rightInches = _perimeterInInches + 5;
        }

        internal async Task Start()
        {
            _inverseKinematics.Start().ConfigureAwait(false);

            _sparkFunRazorMpu = await SerialDeviceHelper.GetSerialDevice("DN01E09J", 57600);

            _arduino = await SerialDeviceHelper.GetSerialDevice("AH03FK33", 57600);

            if (_arduino != null)
                _arduinoDataReader = new DataReader(_arduino.InputStream) {InputStreamOptions = InputStreamOptions.Partial};

            if (_sparkFunRazorMpu != null)
                _razorDataReader = new DataReader(_sparkFunRazorMpu.InputStream) {InputStreamOptions = InputStreamOptions.Partial};

            var collisionEvent = false;

            if (_arduino == null && _sparkFunRazorMpu == null)
                return;

            while (true)
            {

                //Arduino Ping ------------------------------------------
                if (_arduino != null && _behavior != Behavior.None)
                    try
                    {
                        var r = await _arduinoDataReader.LoadAsync(30);

                        if (r <= 0)
                            continue;

                        var incoming = _arduinoDataReader.ReadString(r);

                        if (string.IsNullOrEmpty(incoming))
                            continue;

                        if (ParseRanges(incoming.Split('!')) <= 0)
                            continue;

                        if (_leftInches <= _perimeterInInches || _centerInches <= _perimeterInInches || _rightInches <= _perimeterInInches)
                        {
                            await Display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);

                            collisionEvent = true;

                            if (_behavior == Behavior.Avoid)
                            {
                                var e = CollisionEvent;
                                e?.Invoke(null, new PingData(_perimeterInInches, _leftInches, _centerInches, _rightInches));
                            }
                        }
                        else
                        {
                            if (!collisionEvent)
                                continue;

                            collisionEvent = false;

                            if (_behavior != Behavior.Avoid)
                                continue;

                            var e = CollisionEvent;
                            e?.Invoke(null, new PingData(_perimeterInInches, _leftInches, _centerInches, _rightInches));
                        }
                    }
                    catch
                    {
                        //
                    }
                // ------------------------------------------


                if (_sparkFunRazorMpu == null)
                    continue;
                
                try
                {
                    var incoming = await _razorDataReader.LoadAsync(28);

                    if (incoming <= 0)
                        continue;

                    var yprData = _razorDataReader.ReadString(incoming);

                    yprData = yprData.Replace("#YPR=", "");

                    var yprArray = yprData.Split(',');

                    double.TryParse(yprArray[0], out _yaw);
                    double.TryParse(yprArray[1], out _pitch);
                    double.TryParse(yprArray[2], out _roll);
                }
                catch
                {
                    //
                }
            }
        }

        internal int ParseRanges(string[] ranges)
        {
            var success = ranges.Length;

            foreach (var d in ranges)
            {
                if (string.IsNullOrEmpty(d) || !d.Contains('?'))
                    continue;

                var data = d.Replace("?", "");

                try
                {
                    int ping;

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
                        data = d.Replace("R", "");

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
            await Display.Write("Bounce started", 2);

            RequestSetMovement(true);
            RequestSetGaitType(GaitType.Tripod8);

            double travelLengthZ = 40;
            double travelLengthX = 0;
            double travelRotationY = 0;
            double gaitSpeed = 45;

            while (_behaviorStarted)
            {
                await Task.Delay(100);

                if (_leftInches > _perimeterInInches && _centerInches > _perimeterInInches && _rightInches > _perimeterInInches)
                {
                    await Display.Write("Forward", 2);

                    travelLengthZ = -50;
                    travelLengthX = 0;
                    travelRotationY = 0;
                }

                if (_leftInches <= _perimeterInInches && _rightInches > _perimeterInInches)
                {
                    await Display.Write("Turn Right", 2);

                    travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;

                    travelLengthX = 0;
                    travelRotationY = -30;
                }

                if (_leftInches > _perimeterInInches && _rightInches <= _perimeterInInches)
                {
                    await Display.Write("Turn Left", 2);

                    travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;

                    travelLengthX = 0;
                    travelRotationY = 30;
                }

                if (_leftInches <= _perimeterInInches && _rightInches <= _perimeterInInches)
                {
                    travelLengthX = 0;
                    travelRotationY = 0;

                    if (_centerInches < _perimeterInInches)
                    {
                        await Display.Write("Reverse", 2);

                        travelLengthZ = 30; //Reverse
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        await Display.Write("Turn Left", 2);
                        travelLengthZ = 0;
                        travelRotationY = 30;
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        await Display.Write("Stop", 2);

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
            await Display.Write(enabled ? "Servos on" : "Servos off", 2);
        }

        internal void RequestSetFunction(SelectedIkFunction selectedIkFunction)
        {
            _inverseKinematics.RequestSetFunction(selectedIkFunction);
        }

        internal async void RequestLegYHeight(int leg, double yPos)
        {
            _inverseKinematics.RequestLegYHeight(leg, yPos);
            await Display.Write($"Leg {leg} - {yPos}", 2);
        }

        internal async void RequestNewPerimeter(bool increase)
        {
            if (increase)
                _perimeterInInches++;
            else
                _perimeterInInches--;

            if (_perimeterInInches < 1)
                _perimeterInInches = 1;

            await Display.Write($"Perimeter {_perimeterInInches}", 1);
            await Display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round(duration / 73.746 / 2, 1));
        }
    }
}