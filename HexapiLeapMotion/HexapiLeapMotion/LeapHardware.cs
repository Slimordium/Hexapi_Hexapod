using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Leap;

namespace HexapiLeapMotion
{
    internal static class LeapHardware
    {
        private static Controller _leapController;

        internal static Controller LeapController
        {
            get
            {
                if (_leapController == null)
                    _leapController = new Controller();

                return _leapController; 
            }
        }

        internal static bool RequestMovement { get; set; }
       
        internal static Task PollTask { get; set; }

        internal static LeapEventListener LeapEventListener { get; set; }
    }
}
