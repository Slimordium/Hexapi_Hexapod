/*
    3DOF Hexapod - Hexapi startup 
*/

using System.Diagnostics;
using Windows.ApplicationModel.Background;
using HexapiBackground.Hardware;
using HexapiBackground.IK;
#pragma warning disable 4014
namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        private Display _display = new Display();

        //TODO : Make the various devices that are enabled to be configurable in a settings file
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            

            SerialDeviceHelper.ListAvailablePorts();
            
            //var ping = new RemoteArduino();
            //ping.Start();

            //var gps = new Gps.Gps(true);
            //gps.Start();

            var ik = new InverseKinematics();
            ik.Start();

            var hexapi = new Hexapi(ik);//new Hexapi(gps, avc)
            hexapi.Start();

            _deferral = taskInstance.GetDeferral();
        }

        private void Mpu_SensorInterruptEvent(object sender, MpuSensorEventArgs e)
        {
            Debug.WriteLine(e.Values[0].GyroX);
            Debug.WriteLine(e.Values[0].AccelerationX);
        }

        internal void Complete()
        {
            _deferral.Complete();
        }
    }
}
