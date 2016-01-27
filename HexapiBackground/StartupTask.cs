using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace HexapiBackground
{
    public sealed class StartupTask : IBackgroundTask
    {
        //TODO : Make the various devices that are enabled to be configurable in a settings file
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            taskInstance.GetDeferral();

            //var gps = new Gps();

            //Task.Factory.StartNew(async () =>
            //{
            //    await gps.SetGpsBaudRate();
            //    await Task.Delay(7000);
            //    gps.Open();
            //});

            //var mpu = new Mpu9150();

            //var im = mpu.InitMpu();
            //var ic = mpu.InitCompass();

            //double[] dest1;
            //double[] dest2;

            //mpu.Mpu9150SelfTest(out dest1);

            //if (dest1[0] < 1.0f && dest1[1] < 1.0f && dest1[2] < 1.0f && dest1[3] < 1.0f && dest1[4] < 1.0f && dest1[5] < 1.0f)
            //{
            //    Debug.WriteLine("MPU Self test passed.");
            //}

            //mpu.CalibrateMpu9150(out dest1, out dest2);

            //Debug.WriteLine($"{dest1[0]}, {dest2[0]}");

            //mpu.StartReading();

            //Task.Factory.StartNew(() =>
            //{
            //    var arduino = new Arduino();
            //    arduino.Initialize();
            //}, TaskCreationOptions.LongRunning);


            var hexapi = new Hexapi();

            //gps.GpsData = hexapi.GpsData;

            var r = hexapi.Run();
        }
    }
}
