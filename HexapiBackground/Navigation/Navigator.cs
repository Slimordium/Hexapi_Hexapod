using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;
using System.Threading;

namespace HexapiBackground.Navigation
{
    internal sealed class Navigator
    {
        private readonly IkController _ikController;
        private readonly Gps.Gps _gps;
        private List<LatLon> _waypoints;
        private readonly SparkFunSerial16X2Lcd _display;

        private bool _navRunning;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        internal Navigator(IkController ikController,  SparkFunSerial16X2Lcd display, Gps.Gps gps)
        {
            _ikController = ikController;
            _gps = gps;
            _display = display;
        }
            
        internal async Task InitializeAsync()
        {
            if (_gps == null)
                return;

            _waypoints = await GpsExtensions.LoadWaypoints();

            await _display.WriteAsync($"{_waypoints.Count} waypoints");
        }

        internal async Task StartAsync()
        {
            if (_gps == null || _navRunning)
                return;

            _navRunning = true;

            foreach (var wp in _waypoints)
            {
                if (wp.Lat == 0 || wp.Lon == 0)
                    continue;

                await NavigateToWaypoint(wp, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.IsCancellationRequested || !_navRunning)
                    break;
            }
        }

        internal void Stop()
        {
            _cancellationTokenSource.Cancel();
            _navRunning = false;
        }
        
        internal async Task<bool> NavigateToWaypoint(LatLon currentWaypoint, CancellationToken cancelationToken)
        {
            var distanceHeading = GpsExtensions.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var distanceToWaypoint = distanceHeading[0];
            var headingToWaypoint = distanceHeading[1];

            var travelLengthX = 0D;
            var travelLengthZ = 0D;
            var travelRotationY = 0D;
            var nomGaitSpeed = 50D;
            
            travelLengthZ = -50;

            var turnDirection = "None";

            while (distanceToWaypoint > 10) //Inches
            {
                if (cancelationToken.IsCancellationRequested)
                    return false;

                await _display.WriteAsync($"WP D/H {distanceToWaypoint}, {headingToWaypoint}", 1);
                await _display.WriteAsync($"{turnDirection} {_gps.CurrentLatLon.Heading}", 2);

                if (headingToWaypoint + 5 > 359 && Math.Abs(headingToWaypoint - _gps.CurrentLatLon.Heading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 5) - 359;

                    if (_gps.CurrentLatLon.Heading > tempHeading)
                    {
                        turnDirection = "Right";
                        travelRotationY = -1;
                    }
                    else
                    {
                        turnDirection = "Left";
                        travelRotationY = 1;
                    }
                }
                else if (headingToWaypoint - 5 < 1 && Math.Abs(headingToWaypoint - _gps.CurrentLatLon.Heading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 359) - 5;

                    

                    if (_gps.CurrentLatLon.Heading < tempHeading)
                    {
                        turnDirection = "Right";
                        travelRotationY = 1;
                    }
                    else
                    {
                        turnDirection = "Left";
                        travelRotationY = -1;
                    }
                }
                else if (_gps.CurrentLatLon.Heading > headingToWaypoint - 5 && _gps.CurrentLatLon.Heading < headingToWaypoint + 5)
                {
                    travelRotationY = 0;
                    turnDirection = "None";
                }
                else if (headingToWaypoint > _gps.CurrentLatLon.Heading + 20)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint > 180)
                    {
                        turnDirection = "Left+";
                        travelRotationY = -2;
                    }
                    else
                    {
                        turnDirection = "Right+";
                        travelRotationY = 2;
                    }
                }
                else if (headingToWaypoint > _gps.CurrentLatLon.Heading)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint > 180)
                    {
                        turnDirection = "Left";
                        travelRotationY = -1;
                    }
                    else
                    {
                        turnDirection = "Right";
                        travelRotationY = 1;
                    }
                }
                else if (headingToWaypoint < _gps.CurrentLatLon.Heading - 20) //If it has a long ways to turn, go fast!
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint < 180)
                    {
                        turnDirection = "Left+";
                        travelRotationY = -2;
                    }
                    else
                    {
                        turnDirection = "Right+";
                        travelRotationY = 2; //Turn towards its right
                    }
                }
                else if (headingToWaypoint < _gps.CurrentLatLon.Heading)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint < 180)
                    {
                        turnDirection = "Left";
                        travelRotationY = -1;
                    }
                    else
                    {
                        turnDirection = "Right";
                        travelRotationY = 1;
                    }
                }

                _ikController.RequestMovement(nomGaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                await Task.Delay(50, cancelationToken);

                distanceHeading = GpsExtensions.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
                distanceToWaypoint = distanceHeading[0];
                headingToWaypoint = distanceHeading[1];

                if (cancelationToken.IsCancellationRequested)
                    return false;
            }

            await _display.WriteAsync($"WP D/H {distanceToWaypoint}, {headingToWaypoint}", 1);
            await _display.WriteAsync($"Heading {_gps.CurrentLatLon.Heading}", 2);

            return true;
        }
    }
}