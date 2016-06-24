using System;
using System.Collections.Generic;
using System.Linq;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;

namespace HexapiBackground.IK
{
    /// <summary>
    /// This semi-protects against running the robot into things
    /// </summary>
    internal class IkController
    {
        private int _perimeterInInches;
        private int _leftInches;
        private int _centerInches;
        private int _rightInches;

        private bool _leftBlocked;
        private bool _centerBlocked;
        private bool _rightBlocked;

        private readonly InverseKinematics _inverseKinematics;
        private readonly Mpu9150 _mpu9150;

        internal IkController(InverseKinematics inverseKinematics, RemoteArduino remoteArduino, Mpu9150 mpu9150 = null)
        {
            _inverseKinematics = inverseKinematics;
            _mpu9150 = mpu9150;

            _perimeterInInches = 14;

            remoteArduino.StringReceived += RemoteArduino_StringReceived;

            _leftInches = _perimeterInInches + 5;
            _centerInches = _perimeterInInches + 5;
            _rightInches = _perimeterInInches + 5;
        }

        private async void RemoteArduino_StringReceived(object sender, string e)
        {
            try
            {
                RangeUpdate(e.Split(':')[1].Split(',').Select(int.Parse).ToArray());
            }
            catch (Exception)
            {
                await Display.Write($"Range failed");
            }
        }

        internal void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            if (_centerBlocked && travelLengthZ < 0)
                travelLengthZ = 0;

            _inverseKinematics.RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
        }

        internal void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ, double bodyPosY, double bodyRotY1)
        {
            _inverseKinematics.RequestBodyPosition(bodyRotX1, bodyRotZ1, bodyPosX, bodyPosZ, bodyPosY, bodyRotY1);
        }

        internal void RequestSetGaitOptions(double gaitSpeed, double legLiftHeight)
        {
            _inverseKinematics.RequestSetGaitOptions(gaitSpeed, legLiftHeight);
        }

        internal void RequestSetGaitType(GaitType gaitType)
        {
            _inverseKinematics.RequestSetGaitType(gaitType);
        }

        internal async void RequestSetMovement(bool enabled)
        {
            _inverseKinematics.RequestSetMovement(enabled);
            await Display.Write(enabled ? "Servos on" : "Servos off", 2);
        }

        internal void RequestSetFunction(SelectedIkFunction selectedIkFunction)
        {
            _inverseKinematics.RequestSetFunction(selectedIkFunction);
        }

        internal async void RequestLegYHeight(int leg, double yPos)
        {
            _inverseKinematics.RequestLegYHeight(leg, yPos);
            await Display.Write($"Leg {leg} - {yPos}", 1);
        }

        internal async void RequestNewPerimeter(bool increase)
        {
            if (increase)
                _perimeterInInches++;
            else
                _perimeterInInches--;

            await Display.Write($"Perimeter {_perimeterInInches}", 1);
            await Display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round(duration / 73.746 / 2, 1));
        }

        private async void RangeUpdate(IReadOnlyList<int> data)
        {
            if (data.Count < 3)
                return;

            try
            {
                _leftInches = GetInchesFromPingDuration(data[0]);
                _centerInches = GetInchesFromPingDuration(data[1]);
                _rightInches = GetInchesFromPingDuration(data[2]);

                _leftBlocked = _leftInches <= _perimeterInInches;
                _centerBlocked = _centerInches <= _perimeterInInches;
                _rightBlocked = _rightInches <= _perimeterInInches;
            }
            catch (Exception)
            {
                await Display.Write("Range update failed");
            }

            if (_leftBlocked || _centerBlocked || _rightBlocked)
                await Display.Write($"{_leftInches} {_centerInches} {_rightInches}");
        }

    }
}