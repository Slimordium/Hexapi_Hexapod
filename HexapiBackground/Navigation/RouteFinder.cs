using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using HexapiBackground.Gps;
using HexapiBackground.Helpers;
using HexapiBackground.IK;

namespace HexapiBackground.Navigation
{
    internal sealed class RouteFinder
    {
        private readonly InverseKinematics _inverseKinematics;
        private readonly Gps.Gps _gps;
        private bool _gpsNavigationEnabled;
        private List<LatLon> _waypoints;

        private double _travelLengthX = 0;
        private double _travelLengthZ = 0;
        private double _travelRotationY = 0;
        private double _nomGaitSpeed = 50;

        internal RouteFinder(InverseKinematics inverseKinematics, Gps.Gps gps)
        {
            _inverseKinematics = inverseKinematics;
            _gps = gps;
        }

        internal async Task EnableGpsNavigation()
        {
            if (_gpsNavigationEnabled)
                return;

            _waypoints = GpsExtensions.LoadWaypoints();

            _gpsNavigationEnabled = true;

            await Display.Write($"{_waypoints.Count} waypoints");

            foreach (var wp in _waypoints)
            {
                Debug.WriteLine(wp);

                if (wp.Lat == 0 || wp.Lon == 0)
                    continue;

                await NavigateToWaypoint(wp);

                if (!_gpsNavigationEnabled)
                    break;
            }

            //_inverseKinematics.RequestSetMovement(false);
        }

        internal void DisableGpsNavigation()
        {
            _gpsNavigationEnabled = false;
        }
        
        internal async Task<bool> NavigateToWaypoint(LatLon currentWaypoint)
        {
            var distanceHeading = GpsExtensions.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var distanceToWaypoint = distanceHeading[0];
            var headingToWaypoint = distanceHeading[1];

            var sw = new Stopwatch();
            sw.Start();

            _travelLengthZ = -50;

            await Display.Write($"D/H to WP {distanceToWaypoint}, {headingToWaypoint}", 1);
            await Display.Write($"Heading {_gps.CurrentLatLon.Heading}", 2);

            while (distanceToWaypoint > 10) //Inches
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

                while (sw.ElapsedMilliseconds < 100) { } // only correct heading every 150ms. This may need to be shorter.
                sw.Restart();

                distanceHeading = GpsExtensions.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
                distanceToWaypoint = distanceHeading[0];
                headingToWaypoint = distanceHeading[1];
            }

            await Display.Write($"D/H to WP {distanceToWaypoint}, {headingToWaypoint}", 1);
            await Display.Write($"Heading {_gps.CurrentLatLon.Heading}", 2);

            return true;
        }
    }
}