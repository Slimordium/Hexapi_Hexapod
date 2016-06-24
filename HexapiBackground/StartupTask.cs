/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Diagnostics;
using Windows.ApplicationModel.Background;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
using HexapiBackground.Navigation;

#pragma warning disable 4014
namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private readonly Display _display = new Display();
        private XboxController _xboxController;
        private RemoteArduino _remoteArduino;
        private Gps.Gps _gps;
        private IkController _ikController;
        private InverseKinematics _inverseKinematics;
        private Hexapi _hexapi;
        private Navigator _navigator;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            SerialDeviceHelper.ListAvailablePorts();

            _display.Start();

            _remoteArduino = new RemoteArduino();
            _remoteArduino.Start();

            _xboxController = new XboxController();
            _xboxController.Open();

            _gps = new Gps.Gps(true);
            _gps.Start();

            _inverseKinematics = new InverseKinematics();
            _inverseKinematics.Start();

            _ikController = new IkController(_inverseKinematics, _remoteArduino);

            _navigator = new Navigator(_ikController, _gps);

            _hexapi = new Hexapi(_ikController, _xboxController, _gps, _navigator);
            _hexapi.Start();

            _deferral = taskInstance.GetDeferral();
        }

        internal void Complete()
        {
            _deferral.Complete();
        }
    }
}
