namespace HexapiBackground.IK{
    internal sealed class ImuDataEventArgs
    {
        internal double Yaw { get; set; }
        internal double Pitch { get; set; }
        internal double Roll { get; set; }

        internal double AccelX { get; set; }
        internal double AccelY { get; set; }
        internal double AccelZ { get; set; }
    }
}