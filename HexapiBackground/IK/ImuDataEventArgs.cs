namespace HexapiBackground.IK{
    internal sealed class ImuDataEventArgs{
        internal double Yaw { get; set; } = 0;
        internal double Pitch { get; set; } = 0;
        internal double Roll { get; set; } = 0;

        internal double AccelX { get; set; } = 0;
        internal double AccelY { get; set; } = 0;
        internal double AccelZ { get; set; } = 0;
    }
}