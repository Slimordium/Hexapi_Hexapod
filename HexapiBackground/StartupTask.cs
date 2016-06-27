/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Threading.Tasks;
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

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            SerialDeviceHelper.ListAvailablePorts();

            _display = new Display();
            _display.Start();

            _xboxController = new XboxController();
            _xboxController.Open();

            _gps = new Gps.Gps(true);
            _gps.Start();

            _inverseKinematics = new InverseKinematics();
            _inverseKinematics.Start();

            _ikController = new IkController(_inverseKinematics);
            _ikController.Start();

            _navigator = new Navigator(_ikController, _gps);

            _hexapi = new Hexapi(_ikController, _xboxController, _gps, _navigator);
            Task.Delay(1000).Wait();
            _hexapi.Start();
        }

        internal void Complete()
        {
            _deferral.Complete();
        }
    }
}
