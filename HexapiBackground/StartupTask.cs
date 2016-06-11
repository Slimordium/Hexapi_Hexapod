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
#pragma warning disable 4014
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
            
            var lcd = new SfSerial16X2Lcd();
            lcd.Start();

            var display = new Display(lcd);

            var gps = new Gps.Gps(true);
            gps.Start();

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
