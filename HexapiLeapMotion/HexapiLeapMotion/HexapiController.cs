using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Leap;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace HexapSignalRServer
{
    [HubName("HexapiController")]
    public class HexapiController : Hub
    {
        public HexapiController()
        {
            //if (LeapHardware.LeapEventListener != null)
            //    LeapHardware.LeapController.RemoveListener(LeapHardware.LeapEventListener);

            //LeapHardware.LeapEventListener = new LeapEventListener(RequestMovement, RequestBodyPosition, RequestSetGaitOptions, RequestSetGaitType, RequestSetMovement);
            //LeapHardware.LeapController.AddListener(LeapHardware.LeapEventListener);

        }

        private void PollLeap()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);

                    var frame = LeapHardware.LeapController.Frame();

                    if (frame.Fingers.Count > 0 && frame.Hands.Count > 0 && frame.IsValid)
                    {
                        if (!LeapHardware.RequestMovement)
                            RequestSetMovement(true);

                        LeapHardware.RequestMovement = true;

                        HandList hands = frame.Hands;

                        var leftmost = hands.FirstOrDefault(h => h.IsLeft);
                        var rightmost = hands.FirstOrDefault(h => h.IsRight);

                        //var travelDirection = rightmost?.Direction;

                        //Debug.WriteLine($"Direction {travelDirection}");

                        if (rightmost != null)
                        {
                            var asdf = rightmost.Fingers[0].Type;

                            float pitch = rightmost.PalmNormal.Pitch;
                            float yaw = rightmost.Direction.Yaw;
                            float roll = rightmost.PalmNormal.Roll;

                            //Debug.WriteLine("-----------------------------");

                            //Debug.WriteLine($"Pitch {pitch}");

                            if (pitch < minPitch)
                            {
                                minPitch = pitch;
                                Debug.WriteLine($"min pitch {minPitch}");
                            }

                            if (pitch > maxPitch)
                            {
                                maxPitch = pitch;
                                Debug.WriteLine($"max pitch {maxPitch}");
                            }

                            if (roll < minRoll)
                            {
                                minRoll = roll;
                                Debug.WriteLine($"min roll {minRoll}");
                            }

                            if (roll > maxRoll)
                            {
                                maxRoll = roll;
                                Debug.WriteLine($"max roll {maxRoll}");
                            }

                            if (yaw < minYaw)
                            {
                                minYaw = yaw;
                                Debug.WriteLine($"min yaw {minYaw}");
                            }

                            if (yaw > maxYaw)
                            {
                                maxYaw = yaw;
                                Debug.WriteLine($"max yaw {maxYaw}");
                            }

                            //Debug.WriteLine($"Yaw {yaw}");
                            //Debug.WriteLine($"Roll {roll}");

                            var mPitch = Map(pitch, minPitch, maxPitch, -6, 6);
                            var mYaw = Map(yaw, minYaw, maxYaw, -6, 6);
                            var mRoll = Map(roll, minRoll, maxRoll, -6, 6);

                            Debug.WriteLine($"Mapped yaw {mYaw}");
                            Debug.WriteLine($"Mapped pitch {mPitch}");
                            Debug.WriteLine($"Mapped roll {mRoll}");
                        }

                    }
                    else
                    {
                        LeapHardware.RequestMovement = false;
                        RequestSetMovement(false);
                    }
                }


            });
        }





        private float minPitch;
        private float maxPitch;

        private float minRoll;
        private float maxRoll;

        private float minYaw;
        private float maxYaw;

        internal async void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            await Clients.All.RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
        }

        internal async void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ)//, double bodyPosY, double bodyRotY1)
        {
            await Clients.All.RequestBodyPosition(bodyRotX1, bodyRotZ1, bodyPosX, bodyPosZ);
        }

        internal async void RequestSetGaitOptions(double gaitSpeed, double legLiftHeight)
        {
            await Clients.All.RequestSetGaitOptions(gaitSpeed, legLiftHeight);
        }

        internal async void RequestSetGaitType(int gaitType)
        {
            await Clients.All.RequestSetGaitType(gaitType);
        }

        internal async void RequestSetMovement(bool enabled)
        {
            await Clients.All.RequestSetMovement(enabled);
        }

        internal static double Map(double valueToMap, double valueToMapMin, double valueToMapMax, double outMin, double outMax)
        {
            return (valueToMap - valueToMapMin) * (outMax - outMin) / (valueToMapMax - valueToMapMin) + outMin;
        }
    }

    public class LeapEventListener : Listener
    {

        private readonly Action<double, double, double, double> _requestMovement;
        private readonly Action<double, double, double, double> _requestBodyPosition;
        private readonly Action<double, double> _requestSetGaitOptions;
        private readonly Action<int> _requestSetGaitType;
        private readonly Action<bool> _requestSetMovement;

        internal LeapEventListener(Action<double, double, double, double> requestMovement,
                                    Action<double, double, double, double> requestBodyPosition,
                                    Action<double, double> requestSetGaitOptions,
                                    Action<int> requestSetGaitType,
                                    Action<bool> requestSetMovement)
        {
            _requestMovement = requestMovement;
            _requestBodyPosition = requestBodyPosition;
            _requestSetGaitOptions = requestSetGaitOptions;
            _requestSetGaitType = requestSetGaitType;
            _requestSetMovement = requestSetMovement;
        }

        internal LeapEventListener()
        {

        }

        private bool isEnabled;

        private float minPitch;
        private float maxPitch;

        private float minRoll;
        private float maxRoll;

        private float minYaw;
        private float maxYaw;

        public override void OnFrame(Controller controller)
        {
            var frame = controller.Frame();

            if (frame.Fingers.Count > 0 && frame.Hands.Count > 0 && frame.IsValid)
            {
                if (!isEnabled)
                    _requestSetMovement(true);

                isEnabled = true;

                HandList hands = frame.Hands;

                var leftmost = hands.FirstOrDefault(h => h.IsLeft);
                var rightmost = hands.FirstOrDefault(h => h.IsRight);

                var travelDirection = rightmost?.Direction;

                //Debug.WriteLine($"Direction {travelDirection}");

                if (rightmost != null)
                {
                    var asdf = rightmost.Fingers[0].Type;

                    float pitch = rightmost.PalmNormal.Pitch;
                    float yaw = rightmost.Direction.Yaw;
                    float roll = rightmost.PalmNormal.Roll;

                    //Debug.WriteLine("-----------------------------");

                    //Debug.WriteLine($"Pitch {pitch}");

                    if (pitch < minPitch)
                    {
                        minPitch = pitch;
                        Debug.WriteLine($"min pitch {minPitch}");
                    }

                    if (pitch > maxPitch)
                    {
                        maxPitch = pitch;
                        Debug.WriteLine($"max pitch {maxPitch}");
                    }

                    if (roll < minRoll)
                    {
                        minRoll = roll;
                        Debug.WriteLine($"min roll {minRoll}");
                    }

                    if (roll > maxRoll)
                    {
                        maxRoll = roll;
                        Debug.WriteLine($"max roll {maxRoll}");
                    }

                    if (yaw < minYaw)
                    {
                        minYaw = yaw;
                        Debug.WriteLine($"min yaw {minYaw}");
                    }

                    if (yaw > maxYaw)
                    {
                        maxYaw = yaw;
                        Debug.WriteLine($"max yaw {maxYaw}");
                    }

                    //Debug.WriteLine($"Yaw {yaw}");
                    //Debug.WriteLine($"Roll {roll}");

                    var mPitch = Map(pitch, minPitch, maxPitch, -6, 6);
                    var mYaw = Map(yaw, minYaw, maxYaw, -6, 6);
                    var mRoll = Map(roll, minRoll, maxRoll, -6, 6);

                    Debug.WriteLine($"Mapped yaw {mYaw}");
                    Debug.WriteLine($"Mapped pitch {mPitch}");
                    Debug.WriteLine($"Mapped roll {mRoll}");
                }

            }
            else
            {
                isEnabled = false;
                _requestSetMovement(false);
            }
        }

        internal static double Map(double valueToMap, double valueToMapMin, double valueToMapMax, double outMin, double outMax)
        {
            return (valueToMap - valueToMapMin) * (outMax - outMin) / (valueToMapMax - valueToMapMin) + outMin;
        }

        public override void OnInit(Controller controller)
        {
        }

        public override void OnConnect(Controller controller)
        {
        }

        //Not dispatched when running in debugger
        public override void OnDisconnect(Controller controller)
        {
        }
    }
}