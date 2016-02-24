using System;
using System.Collections.Generic;
using System.Diagnostics;
using HexapiBackground.Gps;

namespace HexapiBackground
{
    internal sealed class RouteFinder
    {
        private readonly InverseKinematics _inverseKinematics;
        private readonly IGps _gps;
        private bool _gpsNavigationEnabled;
        private List<LatLon> _waypoints;

        private double _travelLengthX = 0;
        private double _travelLengthZ = 0;
        private double _travelRotationY = 0;
        private double _nomGaitSpeed = 50;

        internal RouteFinder(InverseKinematics inverseKinematics, IGps gps)
        {
            _inverseKinematics = inverseKinematics;
            _gps = gps;
        }

        internal void EnableGpsNavigation()
        {
            _waypoints = GpsHelpers.LoadWaypoints();

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
        
        internal void NavigateToWaypoint(LatLon currentWaypoint)
        {
            var distanceHeading = GpsHelpers.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var distanceToWaypoint = distanceHeading[0];
            var headingToWaypoint = distanceHeading[1];

            var sw = new Stopwatch();
            sw.Start();

            while (distanceToWaypoint > 36) //Inches
            {
                if (headingToWaypoint + 5 > 359 && Math.Abs(headingToWaypoint - _gps.CurrentLatLon.Heading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 5) - 359;

                    //mode = "Greater than 359 TempHeading " + tempHeading;

                    if (_gps.CurrentLatLon.Heading > tempHeading)
                    {
                        _travelRotationY = -1;
                    }
                    else
                    {
                        _travelRotationY = 1;
                    }
                }
                else if (headingToWaypoint - 5 < 1 && Math.Abs(headingToWaypoint - _gps.CurrentLatLon.Heading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 359) - 5;

                    //mode = "Less than 1 TempHeading " + tempHeading;

                    if (_gps.CurrentLatLon.Heading < tempHeading)
                    {
                        _travelRotationY = 1;
                    }
                    else
                    {
                        _travelRotationY = -1;
                    }
                }
                else if (_gps.CurrentLatLon.Heading > headingToWaypoint - 5 && _gps.CurrentLatLon.Heading < headingToWaypoint + 5)
                {
                    _travelRotationY = 0;
                }
                else if (headingToWaypoint > _gps.CurrentLatLon.Heading + 20)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint > 180)
                        _travelRotationY = -2;
                    else
                        _travelRotationY = 2;
                }
                else if (headingToWaypoint > _gps.CurrentLatLon.Heading)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint > 180)
                        _travelRotationY = -1;
                    else
                        _travelRotationY = 1;
                }
                else if (headingToWaypoint < _gps.CurrentLatLon.Heading - 20) //If it has a long ways to turn, go fast!
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint < 180)
                        _travelRotationY = -2;
                    else
                        _travelRotationY = 2; //Turn towards its right
                }
                else if (headingToWaypoint < _gps.CurrentLatLon.Heading)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint < 180)
                        _travelRotationY = -1;
                    else
                        _travelRotationY = 1;
                }

                _inverseKinematics.RequestMovement(_nomGaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);

                while (sw.ElapsedMilliseconds < 150) { } // only correct heading every 150ms. This may need to be shorter.
                sw.Restart();

                distanceHeading = GpsHelpers.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
                distanceToWaypoint = distanceHeading[0];
                headingToWaypoint = distanceHeading[1];
            }
        }
    }
}