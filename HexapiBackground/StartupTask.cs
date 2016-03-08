using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

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

            var gps = new NavSparkGps(true);
            //var pingSensors = new PingSensors();
            //var hexapi = new Hexapi(gps);

            gps.Start();
            //pingSensors.Start();
            //hexapi.Start();
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
