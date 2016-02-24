
namespace HexapiBackground.Gps
{
    //http://www.x-io.co.uk/open-source-ahrs-with-x-imu/
    //https://electronics.stackexchange.com/questions/16707/imu-adxl345-itg3200-triple-axis-filter
    //https://github.com/xioTechnologies/Open-Source-AHRS-With-x-IMU
    //http://diydrones.com/forum/topics/using-the-mpu6050-for-quadcopter-orientation-control
    //http://www.nuclearprojects.com/ins/gps.shtml

    internal interface IGps
    {
        double DeviationLon { get; }
        double DeviationLat { get; }
        double DriftCutoff { get; }
        LatLon CurrentLatLon { get; }
        void Start();
    }
}
