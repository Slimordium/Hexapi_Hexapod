using Windows.ApplicationModel.Background;

namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        //TODO : Make the various devices that are enabled to be configurable in a settings file
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            taskInstance.GetDeferral();

            SerialPort.ListAvailablePorts();

            var gps = new UltimateGps();
           
            //Task.Factory.StartNew(() =>
            //{
            //    var arduino = new Arduino();
            //    arduino.Initialize();
            //}, TaskCreationOptions.LongRunning);

            var hexapi = new Hexapi();

            //gps.GpsData = hexapi.GpsData;
            gps.Start();

            var r = hexapi.Start(); //Always started last
        }
    }
}
