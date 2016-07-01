/*
    3DOF Hexapod - Hexapi startup 
*/

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
        private Display _display;
        private XboxController _xboxController;
        private Gps.Gps _gps;
        private IkController _ikController;
        private InverseKinematics _inverseKinematics;
        private Hexapi _hexapi;
        private Navigator _navigator;

        ~StartupTask()
        {
            Complete();
        }

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            SerialDeviceHelper.ListAvailablePorts();

            _display = new Display();
            await _display.Start();

            _xboxController = new XboxController();
            await _xboxController.Open();

            var sparkFunRazorMpu = await SerialDeviceHelper.GetSerialDevice("DN01E09J", 57600);
            var arduino = await SerialDeviceHelper.GetSerialDevice("AH03FK33", 57600);

            _gps = new Gps.Gps(true);
            _gps.Start().ConfigureAwait(false);

            _inverseKinematics = new InverseKinematics();
            _ikController = new IkController(_inverseKinematics, arduino, sparkFunRazorMpu);
            _navigator = new Navigator(_ikController, _gps);
            _hexapi = new Hexapi(_ikController, _xboxController, _gps, _navigator);

            _hexapi.Start();

            await _ikController.Start();
        }

        internal void Complete()
        {
            _inverseKinematics?.RequestSetMovement(false);

            _deferral.Complete();
        }
    }
}
