using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Enums;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    internal sealed class RouteFinder
    {
        private readonly InverseKinematics _inverseKinematics;
        private bool _gpsNavigationEnabled;
        private List<LatLon> _waypoints;

        private double _travelLengthX = 0;
        private double _travelLengthZ = 0;
        private double _travelRotationY = 0;
        private double _nomGaitSpeed = 50;

        internal RouteFinder(InverseKinematics inverseKinematics)
        {
            _inverseKinematics = inverseKinematics;
        }

        internal void EnableGpsNavigation()
        {
            _waypoints = Helpers.LoadWaypoints();

            _gpsNavigationEnabled = true;

            foreach (var wp in _waypoints)
            {
                NavigateToWaypoint(wp);

                if (!_gpsNavigationEnabled)
                    break;
            }
        }

        internal void DisableGpsNavigation()
        {
            _gpsNavigationEnabled = false;
        }

        internal void PrintDistanceHeadingToWaypoints()
        {
            foreach (var wp in _waypoints)
            {
                Debug.WriteLine($"From current location, Distance: {wp.DistanceHeadingFromCurrent[0]}in. Heading: {wp.DistanceHeadingFromCurrent[1]}");
            }
        }

        internal void NavigateToWaypoint(LatLon currentWaypoint)
        {
            var distanceHeading = currentWaypoint.DistanceHeadingFromCurrent;
            var distanceToWaypoint = distanceHeading[0];
            var headingToWaypoint = distanceHeading[1];

            var sw = new Stopwatch();
            sw.Start();

            while (distanceToWaypoint > 36) //Inches
            {
                if (headingToWaypoint + 5 > 359 && Math.Abs(headingToWaypoint - UltimateGps.CurrentHeading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 5) - 359;

                    //mode = "Greater than 359 TempHeading " + tempHeading;

                    if (UltimateGps.CurrentHeading > tempHeading)
                    {
                        _travelRotationY = -1;
                    }
                    else
                    {
                        _travelRotationY = 1;
                    }
                }
                else if (headingToWaypoint - 5 < 1 && Math.Abs(headingToWaypoint - UltimateGps.CurrentHeading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 359) - 5;

                    //mode = "Less than 1 TempHeading " + tempHeading;

                    if (UltimateGps.CurrentHeading < tempHeading)
                    {
                        _travelRotationY = 1;
                    }
                    else
                    {
                        _travelRotationY = -1;
                    }
                }
                else if (UltimateGps.CurrentHeading > headingToWaypoint - 5 && UltimateGps.CurrentHeading < headingToWaypoint + 5)
                {
                    _travelRotationY = 0;
                }
                else if (headingToWaypoint > UltimateGps.CurrentHeading + 20)
                {
                    if (UltimateGps.CurrentHeading - headingToWaypoint > 180)
                        _travelRotationY = -2;
                    else
                        _travelRotationY = 2;
                }
                else if (headingToWaypoint > UltimateGps.CurrentHeading)
                {
                    if (UltimateGps.CurrentHeading - headingToWaypoint > 180)
                        _travelRotationY = -1;
                    else
                        _travelRotationY = 1;
                }
                else if (headingToWaypoint < UltimateGps.CurrentHeading - 20) //If it has a long ways to turn, go fast!
                {
                    if (UltimateGps.CurrentHeading - headingToWaypoint < 180)
                        _travelRotationY = -2;
                    else
                        _travelRotationY = 2; //Turn towards its right
                }
                else if (headingToWaypoint < UltimateGps.CurrentHeading)
                {
                    if (UltimateGps.CurrentHeading - headingToWaypoint < 180)
                        _travelRotationY = -1;
                    else
                        _travelRotationY = 1;
                }

                _inverseKinematics.RequestMovement(_nomGaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);

                while (sw.ElapsedMilliseconds < 150) { } // only correct heading every 150ms. This may need to be shorter.
                sw.Restart();

                distanceHeading = currentWaypoint.DistanceHeadingFromCurrent;
                distanceToWaypoint = distanceHeading[0];
                headingToWaypoint = distanceHeading[1];
            }
        }
    }
}