using System;
using System.Diagnostics;

namespace HexapiBackground{
    internal static class Avc
    {
        internal static void CheckForObstructions(ref double travelLengthX, ref double travelRotationY, ref double travelLengthZ, ref double nominalGaitSpeed)
        {
            var randomNumber = new Random(DateTime.Now.Millisecond);
            var turnDirection = randomNumber.Next(0, 10);
            var turnDuration = randomNumber.Next(1000, 1500);
            var reverseDuration = randomNumber.Next(1000, 1500);

            var sw = new Stopwatch();
            sw.Start();

            if (PingSensors.LeftInches < 15 && PingSensors.CenterInches < 15 && PingSensors.RightInches < 15)
            {
                Debug.WriteLine($"Path blocked. Reversing/Turning.");

                travelLengthZ = 25;

                while (sw.ElapsedMilliseconds < reverseDuration) { }

                if (turnDirection < 5)
                    travelRotationY = 2;
                else
                    travelRotationY = -1;

                sw.Restart();
                while (sw.ElapsedMilliseconds < turnDuration) { }
            }
            else if (PingSensors.RightInches < 15 && PingSensors.LeftInches > 17)
            {
                Debug.WriteLine($"Turning left");

                if (travelLengthZ < 0)
                    travelLengthZ = 0;

                travelRotationY = -2;
            }
            else if (PingSensors.LeftInches < 15 && PingSensors.RightInches > 17)
            {
                Debug.WriteLine($"Turning right");

                if (travelLengthZ < 0)
                    travelLengthZ = 0;

                travelRotationY = 2;
            }
            //else if (PingSensors.CenterInches > 15 && PingSensors.RightInches > 15 && PingSensors.LeftInches > 15)
            //{
            //    Debug.WriteLine($"Path clear");

            //    travelLengthZ = -Helpers.Map(PingSensors.CenterInches, 10, 300, 20, 100);
            //}
            else
            {
                Debug.WriteLine($"Should not get here L :{PingSensors.LeftInches} C :{PingSensors.CenterInches} R :{PingSensors.RightInches}");
            }
        }
    }
}