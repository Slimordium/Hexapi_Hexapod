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

            //var gps = new NavSparkGps(true);
            //var pingSensors = new PingSensors();
            var hexapi = new Hexapi(null);

            //gps.Start();
            //pingSensors.Start();
            hexapi.Start();

            //var p = new AdafruitFona();

            //Task.Delay(5000).Wait();

            //p.Start();

            //p.OpenTcpTransparentConnection("69.44.86.36", 2101); //

            //var auth = p.CreateAuthRequest();

            //p.WriteTcpData(auth);
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
