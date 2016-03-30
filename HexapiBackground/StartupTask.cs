/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;

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

            //These are all optional----
            var remoteArduino = new RemoteArduino(); //Only need this if using the PingSensors or the RemoteArduinoRtkGps
            var gps = new RemoteArduinoRtkGps(); //NavSparkRtkGps, UltimateGps
            var pingSensors = new PingSensors();
            var avc = new Avc();
            gps.Start();
            //--------------------------

            var hexapi = new Hexapi(gps, avc);
            hexapi.Start();
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
