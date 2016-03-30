using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        //TODO : Make the various devices that are enabled to be configurable in a settings file
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            SerialPort.ListAvailablePorts();

            var remoteArduino = new RemoteArduino();

            var gps = new RemoteArduinoRtkGps(remoteArduino);

            var pingSensors = new PingSensors(remoteArduino);

            var hexapi = new Hexapi();

            gps.Start();

            hexapi.Start();
             
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
