/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
using HexapiBackground.Navigation;
using HexapiBackground.SignalR;

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
            //var remoteArduino = new RemoteArduino(); //Only need this if using the PingSensors, RemoteArduinoRtkGps or Rocket Launcher
            //var gps = new RemoteArduinoRtkGps(); //NavSparkRtkGps, UltimateGps
            //var gps = new NavSparkGps(true); //NavSparkRtkGps, UltimateGps
            //var pingSensors = new PingSensors();
            //var avc = new Avc();
            //gps.Start();
            //remoteArduino.Start();
            //--------------------------

            //var piezo = new Ads1115();
            //piezo.Start(0);  

            //var ntrip = new RtkGps();

            var ik = new InverseKinematics();
            ik.Start();

            var hexapi = new Hexapi(ik);//new Hexapi(gps, avc)
            hexapi.Start();

            
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
