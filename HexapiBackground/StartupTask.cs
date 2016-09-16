/*
    3DOF Hexapod - Hexapi startup 
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
//using HexapiBackground.Iot;
using HexapiBackground.Navigation;

namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        internal static readonly SerialDeviceHelper SerialDeviceHelper = new SerialDeviceHelper();

        private BackgroundTaskDeferral _deferral;

        private SparkFunSerial16X2Lcd _display;
        private readonly XboxController _xboxController = new XboxController();
        private IkController _ikController;
        private InverseKinematics _inverseKinematics;
        private Hexapi _hexapi;
        private GpsNavigator _navigator;
        //private IoTClient _ioTClient;

        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly List<Task> _startTasks = new List<Task>();
        private Hardware.Gps _gps;

        private GpioController _gpioController;
        private GpioPin _startButton;
        private static GpioPin _resetGps1;
        private static GpioPin _resetGps2;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private static bool _navRunning;

        private bool _ignoreDisconnect;
        private SerialDevice _serialDevice;
        private DataReader _arduinoDataReader;
        private DataWriter _arduinoDataWriter;


        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            _serialDevice = await StartupTask.SerialDeviceHelper.GetSerialDeviceAsync("AH03FK33", 57600, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
            
            _arduinoDataReader = new DataReader(_serialDevice.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            _arduinoDataWriter = new DataWriter(_serialDevice.OutputStream);

            _display = new SparkFunSerial16X2Lcd(_arduinoDataWriter);

            //ioTClient = new IoTClient(_display);
            _gps = new Hardware.Gps( _display);
            _inverseKinematics = new InverseKinematics(_display);

            _ikController = new IkController(_inverseKinematics, _display, _gps, _arduinoDataReader); //Range and yaw/pitch/roll data from Arduino and SparkFun Razor IMU
            _navigator = new GpsNavigator(_ikController, _display, _gps);
            _hexapi = new Hexapi(_ikController, _xboxController, _navigator, _display, _gps);

            //_initializeTasks.Add(_display.InitializeAsync());
            _initializeTasks.Add(_xboxController.InitializeAsync());
            //_initializeTasks.Add(_ikController.InitializeAsync());
            _initializeTasks.Add(_gps.InitializeAsync());
            _initializeTasks.Add(_inverseKinematics.InitializeAsync());
            _initializeTasks.Add(InitGpioAsync());
            //_initializeTasks.Add(_ioTClient.InitializeAsync());

            _startTasks.Add(_ikController.StartAsync());
            _startTasks.Add(_gps.StartAsync());
            _startTasks.Add(_gps.StartRtkUdpFeedAsync());
            _startTasks.Add(_inverseKinematics.StartAsync());
            //_startTasks.Add(_ioTClient.StartAsync());//only needed if expecting messages from the server

            await Task.WhenAll(_initializeTasks.ToArray());

            await Task.WhenAll(_startTasks.ToArray());
        }

        private async Task InitGpioAsync()
        {
            try
            {
                _gpioController = GpioController.GetDefault();

                if (_gpioController == null)
                {
                    await _display.WriteAsync("GPIO ?");
                    return;
                }
            }
            catch
            {
                await _display.WriteAsync("GPIO Exception");
                return;
            }

            _resetGps1 = _gpioController.OpenPin(24);
            _resetGps2 = _gpioController.OpenPin(25);

            _resetGps1.SetDriveMode(GpioPinDriveMode.Output);
            _resetGps2.SetDriveMode(GpioPinDriveMode.Output);

            _startButton = _gpioController.OpenPin(5);
            _startButton.SetDriveMode(GpioPinDriveMode.Input);
            _startButton.DebounceTimeout = TimeSpan.FromMilliseconds(500);
            await Task.Delay(500);
            _startButton.ValueChanged += StartButton_ValueChanged;
        }

        internal static async Task ResetGps()
        {
            _resetGps1.Write(GpioPinValue.Low);
            _resetGps2.Write(GpioPinValue.Low);

            await Task.Delay(1000);

            _resetGps1.Write(GpioPinValue.High);
            _resetGps2.Write(GpioPinValue.High);
        }

        private async void StartButton_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (sender.PinNumber != 5 || args.Edge != GpioPinEdge.FallingEdge)
                return;

            //XboxController.Disconnected -= XboxControllerDisconnected;

            await _display.WriteAsync("Start pushed").ConfigureAwait(false);

            if (_navRunning)
            {
                await _display.WriteAsync("Busy", 2);
                return;
            }

            _navRunning = true;

            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource = new CancellationTokenSource();

            _ignoreDisconnect = true;
            await _navigator.StartAsync(_cancellationTokenSource.Token, () =>
            {
                _navRunning = false;
                _cancellationTokenSource.Cancel();
                _display.WriteAsync("Completed", 2).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
