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
        internal static readonly SerialDeviceHelper SerialDeviceHelper = new SerialDeviceHelper();

        private BackgroundTaskDeferral _deferral;

        private SparkFunSerial16X2Lcd _display;
        private XboxController _xboxController;
        private Gps.Gps _gps;
        private IkController _ikController;
        private InverseKinematics _inverseKinematics;
        private Hexapi _hexapi;
        private Navigator _navigator;
        private NtripClient _ntripClient;
        private IoTClient _ioTClient;

        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly List<Task> _startTasks = new List<Task>();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            //_ioTClient = new IoTClient();
            //await _ioTClient.Start();

            //await _ioTClient.SendEvent("Initializing...");

            //foreach (var d in await SerialDeviceHelper.ListAvailablePorts())
            //{
            //    await _ioTClient.SendEvent(d);
            //}

            _display = new SparkFunSerial16X2Lcd();
            _xboxController = new XboxController(_display);
            _ntripClient = new NtripClient("172.16.0.227", 8000, "", "", "", _display); //172.16.0.227
            _gps = new Gps.Gps( _display, _ntripClient);
            _inverseKinematics = new InverseKinematics(_display);
            _ikController = new IkController(_inverseKinematics, _display, _ioTClient, _gps); //Range and yaw/pitch/roll data from Arduino and SparkFun Razor IMU
            _navigator = new Navigator(_ikController, _display, _gps);
            _hexapi = new Hexapi(_ikController, _xboxController, _navigator, _display, _gps, _ioTClient);

            _initializeTasks.Add(_display.InitializeAsync());
            _initializeTasks.Add(_xboxController.InitializeAsync());
            _initializeTasks.Add(_ikController.InitializeAsync());
            _initializeTasks.Add(_ntripClient.InitializeAsync());
            _initializeTasks.Add(_gps.InitializeAsync());
            _initializeTasks.Add(_inverseKinematics.InitializeAsync());

            _startTasks.Add(_ntripClient.StartAsync());
            _startTasks.Add(_ikController.StartAsync());
            _startTasks.Add(_gps.StartAsync());
            _startTasks.Add(_inverseKinematics.StartAsync());
            _startTasks.Add(_hexapi.StartAsync());

            await Task.WhenAll(_initializeTasks.ToArray());

            await Task.WhenAll(_startTasks.ToArray());
        }

        internal void Complete()
        {
            _deferral.Complete();
        }
    }
}
