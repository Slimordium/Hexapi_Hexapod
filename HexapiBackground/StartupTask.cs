/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Diagnostics;
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
        public async void Run(IBackgroundTaskInstance taskInstance)
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

            //Task.Factory.StartNew(async() =>
            //{
            //    var mpu = new Mpu9150New();
            //    await mpu.InitializeHardware();
            //    //mpu.SensorInterruptEvent += Mpu_SensorInterruptEvent;
            //});
         
            //var lcd = new SfSerial16X2Lcd();
            //await lcd.Start();
            //await lcd.Write("Booting...");

            //var pca9685 = new Pca9685();
            //await pca9685.Start();

            var ik = new InverseKinematics();
            ik.Start();

            var hexapi = new Hexapi(ik);//new Hexapi(gps, avc)
            hexapi.Start();
        }

        private void Mpu_SensorInterruptEvent(object sender, MpuSensorEventArgs e)
        {
            Debug.WriteLine(e.Values[0].GyroX);
            Debug.WriteLine(e.Values[0].AccelerationX);
        }

        internal static void Complete()
        {
            _deferral.Complete();
        }
    }
}
