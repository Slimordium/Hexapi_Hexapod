using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;
#pragma warning disable 4014

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

        private readonly InverseKinematics _inverseKinematics;
        private readonly Mpu9150 _mpu9150;
        private SerialDevice _arduino;
        private DataReader _dataReader;

        internal IkController(InverseKinematics inverseKinematics, Mpu9150 mpu9150 = null)
        {
            _inverseKinematics = inverseKinematics;
            _mpu9150 = mpu9150;

            _perimeterInInches = 14;

            _leftInches = _perimeterInInches + 5;
            _centerInches = _perimeterInInches + 5;
            _rightInches = _perimeterInInches + 5;
        }

        internal async Task Start()
        {
            _arduino = await SerialDeviceHelper.GetSerialDevice("AH03FK33", 57600);
            await Task.Delay(500);
            _dataReader = new DataReader(_arduino.InputStream);
            await Task.Delay(500);

            while (true)
            {
                try
                {
                    var r = await _dataReader.LoadAsync(21);

                    if (r <= 0)
                        continue;

                    var incoming = _dataReader.ReadString(r);

                    if (string.IsNullOrEmpty(incoming))
                        continue;

                    var ranges = incoming.Split('!');

                    foreach (var d in ranges)
                    {
                        if (string.IsNullOrEmpty(d) || !d.Contains('?'))
                            continue;

                        var data = d.Replace("?", "");
                        var ping = 0;

                        try
                        {
                            if (data.Contains('L'))
                            {
                                data = data.Replace("L", "");

                                if (int.TryParse(data, out ping))
                                    _leftInches = GetInchesFromPingDuration(ping);
                            }
                            if (data.Contains('C'))
                            {
                                data = data.Replace("C", "");

                                if (int.TryParse(data, out ping))
                                    _centerInches = GetInchesFromPingDuration(ping);
                            }
                            if (data.Contains('R'))
                            {
                                data = d.Replace("R", "");

                                if (int.TryParse(data, out ping))
                                    _rightInches = GetInchesFromPingDuration(ping);
                            }
                        }
                        catch
                        {
                            //
                        }
                    }

                    if (_leftInches < _perimeterInInches || _centerInches < _perimeterInInches || _rightInches < _perimeterInInches)
                        await Display.Write($"{_leftInches} {_centerInches} {_rightInches}");
                }
                catch (Exception)
                {

                }

            }
        }

        internal void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            if (_centerInches <= _perimeterInInches && travelLengthZ < 0)
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

        internal void RequestSetMovement(bool enabled)
        {
            _inverseKinematics.RequestSetMovement(enabled);
            Display.Write(enabled ? "Servos on" : "Servos off", 2);
        }

        internal void RequestSetFunction(SelectedIkFunction selectedIkFunction)
        {
            _inverseKinematics.RequestSetFunction(selectedIkFunction);
        }

        internal void RequestLegYHeight(int leg, double yPos)
        {
            _inverseKinematics.RequestLegYHeight(leg, yPos);
            Display.Write($"Leg {leg} - {yPos}", 1);
        }

        internal void RequestNewPerimeter(bool increase)
        {
            if (increase)
                _perimeterInInches++;
            else
                _perimeterInInches--;

            if (_perimeterInInches < 1)
                _perimeterInInches = 1;

            Display.Write($"Perimeter {_perimeterInInches}", 1);
            Display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round(duration / 73.746 / 2, 1));
        }
    }
}