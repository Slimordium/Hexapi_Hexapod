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

            var lcd = new SfSerial16X2Lcd();
            await lcd.Start();
            await lcd.Write("Booting...");

            var gps = new NavSparkGps(true, lcd);
            await gps.Start();

            var pca9685 = new Pca9685();
            await pca9685.Start();

            var ik = new InverseKinematics(pca9685, null, lcd);
            ik.Start();

            var hexapi = new Hexapi(ik, gps, null, lcd);//new Hexapi(gps, avc)
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
