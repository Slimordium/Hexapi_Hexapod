//using System;
//using System.Diagnostics;
//using HexapiBackground.Enums;
//using HexapiBackground.IK;
//using Microsoft.AspNet.SignalR.Client;

//// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

//namespace HexapiBackground.SignalR
//{
//    internal class HexapiLeapMotionClient
//    {
//        private readonly IHubProxy _hubProxy;
//        private readonly HubConnection _hexapiControllerConnection;
//        private readonly InverseKinematics _inverseKinematics;

//        internal HexapiLeapMotionClient(InverseKinematics inverseKinematics)
//        {
//            _inverseKinematics = inverseKinematics;
//            _hexapiControllerConnection = new HubConnection("http://localhost:8080/signalr");

//            try
//            {
//                _hubProxy = _hexapiControllerConnection.CreateHubProxy("HexapiController");
//                _hubProxy.On<double, double, double, double> ("RequestMovement", RequestMovement);
//                _hubProxy.On<double, double, double, double>("RequestBodyPosition", RequestBodyPosition);
//                _hubProxy.On<double, double>("RequestSetGaitOptions", RequestSetGaitOptions);
//                _hubProxy.On<int>("RequestSetGaitType", RequestSetGaitType);
//                _hubProxy.On<bool>("RequestSetMovement", RequestSetMovement);

//                _hexapiControllerConnection.StateChanged += HexapiControllerConnectionStateChanged;

//                _hexapiControllerConnection.Start();
//            }
//            catch (Exception e)
//            {

//            }
//        }

//        private void HexapiControllerConnectionStateChanged(StateChange obj)
//        {

//        }

//        public void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
//        {
//            //_inverseKinematics.RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
//            Debug.WriteLine("RequestMovement");
//        }

//        public void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ)//, double bodyPosY, double bodyRotY1)
//        {
//            //_inverseKinematics.RequestBodyPosition(bodyRotX1, bodyRotZ1, bodyPosX, bodyPosZ, 0, 0);
//            Debug.WriteLine("RequestBodyPosition");
//        }

//        public void RequestSetGaitOptions(double gaitSpeed, double legLiftHeight)
//        {
//            //_inverseKinematics.RequestSetGaitOptions(gaitSpeed, legLiftHeight);
//            Debug.WriteLine("RequestSetGaitOptions");
//        }

//        public void RequestSetGaitType(int gaitType)
//        {
//            //_inverseKinematics.RequestSetGaitType((GaitType)gaitType);
//            Debug.WriteLine("RequestSetGaitType");
//        }

//        public void RequestSetMovement(bool enabled)
//        {
//            //_inverseKinematics.RequestSetMovement(enabled);
//            Debug.WriteLine("RequestSetMovement");
//        }
//    }
//}