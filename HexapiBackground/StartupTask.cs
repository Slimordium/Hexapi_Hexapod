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

            //SerialPort.ListAvailablePorts();
            
            var avc = new AvController();
            var arduino = new RemoteArduino { RangeUpdate = avc.RangeUpdate };
            var gps = new UltimateGps { LatLonUpdate = avc.LatLonUpdate };
            var hexapi = new Hexapi();

            arduino.Start();
            gps.Start();
            hexapi.Start();
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
