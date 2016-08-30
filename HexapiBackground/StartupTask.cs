/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
using HexapiBackground.Iot;
using HexapiBackground.Navigation;

namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        internal static readonly SerialDeviceHelper SerialDeviceHelper = new SerialDeviceHelper();

        private BackgroundTaskDeferral _deferral;

        private readonly SparkFunSerial16X2Lcd _display = new SparkFunSerial16X2Lcd();
        private readonly XboxController _xboxController = new XboxController();
        private IkController _ikController;
        private InverseKinematics _inverseKinematics;
        private Hexapi _hexapi;
        private GpsNavigator _navigator;
        private IoTClient _ioTClient;

        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly List<Task> _startTasks = new List<Task>();
        private Hardware.Gps _gps;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            _ioTClient = new IoTClient(_display);
            _gps = new Hardware.Gps( _display, _ioTClient);
            _inverseKinematics = new InverseKinematics(_display);
            _ikController = new IkController(_inverseKinematics, _display, _ioTClient, _gps); //Range and yaw/pitch/roll data from Arduino and SparkFun Razor IMU
            _navigator = new GpsNavigator(_ikController, _display, _gps);
            _hexapi = new Hexapi(_ikController, _xboxController, _navigator, _display, _gps, _ioTClient);

            _initializeTasks.Add(_display.InitializeAsync());
            _initializeTasks.Add(_xboxController.InitializeAsync());
            _initializeTasks.Add(_ikController.InitializeAsync());
            _initializeTasks.Add(_gps.InitializeAsync());
            _initializeTasks.Add(_inverseKinematics.InitializeAsync());
            //_initializeTasks.Add(_ioTClient.InitializeAsync());

            _startTasks.Add(_ikController.StartAsync());
            _startTasks.Add(_gps.StartAsync());
            _startTasks.Add(_inverseKinematics.StartAsync());
            //_startTasks.Add(_ioTClient.StartAsync());//only needed if expecting messages from the server

            //foreach (var d in await SerialDeviceHelper.ListAvailablePorts())
            //{
            //    await _ioTClient.SendEvent(d);
            //}

            await Task.WhenAll(_initializeTasks.ToArray());

            await Task.WhenAll(_startTasks.ToArray());
        }

        internal void Complete()
        {
            _deferral.Complete();
        }
    }
}
