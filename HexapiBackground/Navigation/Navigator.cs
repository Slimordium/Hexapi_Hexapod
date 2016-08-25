using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using HexapiBackground.Enums;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;

namespace HexapiBackground.Navigation
{
    internal sealed class GpsNavigator
    {
        private readonly IkController _controller;
        private List<GpsFixData> _waypoints;
        private readonly SparkFunSerial16X2Lcd _display;
        private double _travelLengthX = 0;
        private double _travelLengthZ;
        private double _gaitSpeed = 30;

        internal GpsNavigator(IkController controller, SparkFunSerial16X2Lcd display)
        {
            _controller = controller;
            _display = display;
        }

        internal async Task StartAsync(CancellationToken token)
        {
            _waypoints = await GpsExtensions.LoadWaypoints();

            await _display.WriteAsync($"{_waypoints.Count} waypoints");

            _controller.RequestSetGaitType(GaitType.Tripod8);
            _gaitSpeed = 30;

            foreach (var wp in _waypoints)
            {
                if (Math.Abs(wp.Lat) < 1 || Math.Abs(wp.Lon) < 1)
                    continue;

                await NavigateToWaypoint(wp, token);

                if (token.IsCancellationRequested)
                    break;
            }
        }

        internal async Task<bool> NavigateToWaypoint(GpsFixData currentWaypoint, CancellationToken cancelationToken)
        {
            var distanceToWaypoint = GpsExtensions.GetDistanceToDestination(Gps.Gps.CurrentGpsFixData.Lat, Gps.Gps.CurrentGpsFixData.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var headingToWaypoint = GpsExtensions.GetHeadingToDestination(Gps.Gps.CurrentGpsFixData.Lat, Gps.Gps.CurrentGpsFixData.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var startDistanceToWp = 0d;
            int currentHeading = 0;

            while (distanceToWaypoint > 36) //Inches
            {
                if (Math.Abs(startDistanceToWp) < .1)
                    startDistanceToWp = distanceToWaypoint;

                if (cancelationToken.IsCancellationRequested)
                    return false;

                currentHeading = Gps.Gps.CurrentGpsFixData.Heading;

                var turnMagnitude = Convert.ToInt32(distanceToWaypoint.Map(0, startDistanceToWp, 1, 3)); //How much should we turn
                _travelLengthZ = Convert.ToInt32(distanceToWaypoint.Map(0, startDistanceToWp, 50, 180)); //Forward movement

                await _display.WriteAsync($"Tm:{turnMagnitude} Z:{_travelLengthZ}", 1);
                await _display.WriteAsync($"D:{distanceToWaypoint} H:{headingToWaypoint}", 2);

                if (headingToWaypoint + 1 > 359 && Math.Abs(headingToWaypoint - currentHeading) >= 1)
                {
                    var tempHeading = (headingToWaypoint + 1) - 359;
                    await RequestMove(currentHeading, tempHeading, turnMagnitude);
                }
                else if (headingToWaypoint - 1 < 1 && Math.Abs(headingToWaypoint - currentHeading) >= 1)
                {
                    var tempHeading = (headingToWaypoint + 359) - 1;
                    await RequestMove(currentHeading, tempHeading, turnMagnitude);
                }
                else if (currentHeading > headingToWaypoint - 3 && currentHeading < headingToWaypoint + 3) //Make a run for it! 
                {
                    _travelLengthZ = 180;
                    _controller.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, 0);
                }
                else if (headingToWaypoint > currentHeading)
                {
                    await RequestMove(currentHeading, headingToWaypoint, turnMagnitude);
                }
                else if (headingToWaypoint < currentHeading)
                {
                    await RequestMove(Gps.Gps.CurrentGpsFixData.Heading, headingToWaypoint, turnMagnitude);
                }

                await Task.Delay(50);

                distanceToWaypoint = GpsExtensions.GetDistanceToDestination(Gps.Gps.CurrentGpsFixData.Lat, Gps.Gps.CurrentGpsFixData.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
                headingToWaypoint = GpsExtensions.GetHeadingToDestination(Gps.Gps.CurrentGpsFixData.Lat, Gps.Gps.CurrentGpsFixData.Lon, currentWaypoint.Lat, currentWaypoint.Lon);

                if (cancelationToken.IsCancellationRequested)
                    return false;
            }

            await _display.WriteAsync("At waypoint...", 1);
            await _display.WriteAsync($"D:{distanceToWaypoint} H:{headingToWaypoint}", 2);

            return true;
        }

        private async Task RequestMove(double currentHeading, double headingToWaypoint, int turnMagnitude)
        {
            int travelRotationY;

            if (currentHeading - headingToWaypoint < 180)
                travelRotationY = -turnMagnitude;
            else
                travelRotationY = turnMagnitude;

            await _display.WriteAsync($"rotY {travelRotationY}", 1);
            await _display.WriteAsync($"Z {_travelLengthZ}", 2);

            _controller.RequestMovement(_gaitSpeed, _travelLengthX, _travelLengthZ, travelRotationY);
        }
    }
}