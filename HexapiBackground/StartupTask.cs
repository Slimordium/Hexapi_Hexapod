/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using HexapiBackground.Gps.Ntrip;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
using HexapiBackground.Navigation;

namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        private readonly SerialDeviceHelper _serialDeviceHelper = new SerialDeviceHelper();

        private BackgroundTaskDeferral _deferral;
        private SparkFunSerial16X2Lcd _display;
        private XboxController _xboxController;
        private Gps.Gps _gps;
        private IkController _ikController;
        private InverseKinematics _inverseKinematics;
        private Hexapi _hexapi;
        private Navigator _navigator;
        private NtripClientTcp _ntripClient;

        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly List<Task> _startTasks = new List<Task>();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            SerialDeviceHelper.ListAvailablePorts();

            _display = new SparkFunSerial16X2Lcd(_serialDeviceHelper);
            _xboxController = new XboxController(_display);
            _ntripClient = new NtripClientTcp("172.16.0.226", 8000, "", "", "", _display);
            _gps = new Gps.Gps(true, _serialDeviceHelper, _display, _ntripClient);
            _inverseKinematics = new InverseKinematics(_serialDeviceHelper, _display);
            _ikController = new IkController(_inverseKinematics, _display, _serialDeviceHelper);
            _navigator = new Navigator(_ikController, _display, _gps);
            _hexapi = new Hexapi(_ikController, _xboxController, _navigator, _display, _gps);

            _initializeTasks.Add(_display.Initialize());
            _initializeTasks.Add(_ikController.Initialize());
            _initializeTasks.Add(_xboxController.Initialize());
            _initializeTasks.Add(_gps.Initialize());
            _initializeTasks.Add(_inverseKinematics.Initialize());

            _startTasks.Add(_ikController.Start());
            _startTasks.Add(_gps.Start());
            _startTasks.Add(_inverseKinematics.Start());
            _startTasks.Add(_hexapi.Start());

            await Task.WhenAll(_initializeTasks.ToArray());

            await Task.WhenAll(_startTasks.ToArray());
        }

        internal void Complete()
        {
            _deferral.Complete();
        }
    }
}
