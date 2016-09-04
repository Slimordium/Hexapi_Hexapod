using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using HexapiBackground.Gps;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;
using HexapiBackground.IK;

namespace HexapiBackground.Navigation
{
    internal sealed class GpsNavigator
    {
        private readonly IkController _ikController;
        private readonly Hardware.Gps _gps;
        private List<GpsFixData> _waypoints;
        private readonly SparkFunSerial16X2Lcd _display;

        internal GpsNavigator(IkController ikController, SparkFunSerial16X2Lcd display, Hardware.Gps gps)
        {
            _ikController = ikController;
            _gps = gps;
            _display = display;
        }

        internal async Task StartAsync(CancellationToken token)
        {
            _waypoints = await GpsExtensions.LoadWaypoints();

            await _display.WriteAsync($"{_waypoints.Count} waypoints");

            foreach (var wp in _waypoints)
            {
                if (Math.Abs(wp.Lat) < 1 || Math.Abs(wp.Lon) < 1)
                    continue;

                await NavigateToWaypoint(wp, token);

                if (token.IsCancellationRequested)
                    break;
            }

            //await _ikController.RequestTravel(TravelDirection.Stop, 0);
        }

        private Timer _statusTimer = new Timer((o) =>
        {

        }, null, 0, 500);

        internal async Task<bool> NavigateToWaypoint(GpsFixData currentWaypoint, CancellationToken cancelationToken)
        {
            var gpsFixData = Hardware.Gps.CurrentGpsFixData;
            var distanceAndHeading = GpsExtensions.GetDistanceAndHeadingToDestination(gpsFixData.Lat, gpsFixData.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var distanceToWaypoint = distanceAndHeading[0];
            var headingToWaypoint = distanceAndHeading[1];

            var travelLengthZ = -130;

            while (distanceToWaypoint > 15) 
            {
                if (distanceToWaypoint > 20)
                {
                    
                }

                if (cancelationToken.IsCancellationRequested)
                    return false;

                var degDiff = Math.Abs(headingToWaypoint - gpsFixData.Heading); //How far do we need to turn?
                var turnMagnitude = degDiff.Map(0, 359, 0, 3); //Map to magnitude was 30000

                if (turnMagnitude > 3)
                    turnMagnitude = 3;

                await RequestMove(gpsFixData.Heading, headingToWaypoint, turnMagnitude, travelLengthZ);

                await Task.Delay(50);

                gpsFixData = Hardware.Gps.CurrentGpsFixData;

                distanceAndHeading = GpsExtensions.GetDistanceAndHeadingToDestination(gpsFixData.Lat, gpsFixData.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
                distanceToWaypoint = distanceAndHeading[0];
                headingToWaypoint = distanceAndHeading[1];

                if (cancelationToken.IsCancellationRequested)
                    return false;

                Debug.WriteLine($"Distance to WP{distanceToWaypoint} Heading to WP {headingToWaypoint} Car Heading {gpsFixData.Heading}");
            }

            //await _ikController.RequestTravel(TravelDirection.Stop, 0);

            await _display.WriteAsync("At waypoint...", 1);
            await _display.WriteAsync($"D:{distanceToWaypoint} H:{headingToWaypoint}", 2);

            return true;
        }

        private async Task RequestMove(double currentHeading, double headingToWaypoint, double turnMagnitude, double travelLengthZ)
        {

            if (currentHeading < 180 && headingToWaypoint > 270)
            {
                _ikController.RequestMovement(20, 0, travelLengthZ, -turnMagnitude);
                //await _ikController.RequestTurn(TurnDirection.Left, turnMagnitude);
                return;
            }

            if (currentHeading > 180 && headingToWaypoint < 90)
            {
                _ikController.RequestMovement(20, 0, travelLengthZ, turnMagnitude);
                //await _ikController.RequestTurn(TurnDirection.Right, turnMagnitude);
                return;
            }


            if (currentHeading > headingToWaypoint)
            {
                _ikController.RequestMovement(20, 0, travelLengthZ, -turnMagnitude);
                //await _ikController.RequestTurn(TurnDirection.Left, turnMagnitude);
            }
            else
            {
                _ikController.RequestMovement(20, 0, travelLengthZ, turnMagnitude);
                //await _ikController.RequestTurn(TurnDirection.Right, turnMagnitude);
            }
        }
    }
}