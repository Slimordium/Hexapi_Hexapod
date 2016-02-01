using System.Threading.Tasks;
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
            
            var hexapi = new Hexapi();

            Task.Factory.StartNew(() =>
            {
                var arduino = new RemoteArduino {PingData = hexapi.PingData};
                arduino.Start();
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                var gps = new UltimateGps {GpsData = hexapi.GpsData};
                gps.Start();
            }, TaskCreationOptions.LongRunning);

            var thisIsTheOnlyWayThatWasReliableInKeepingTheTaskRunning = hexapi.Start(); //Always started last
        }
    }
}
