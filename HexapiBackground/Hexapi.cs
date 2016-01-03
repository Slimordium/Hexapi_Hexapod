using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace HexapiBackground
{
    sealed class Hexapi
    {
        public Hexapi()
        {
            //var asdf = new MPU6050();
            //asdf.InitHardware();
            //asdf.SensorInterruptEvent += Asdf_SensorInterruptEvent;
        }

        private int _samples = 10;
        private int _progress = 1;

        float base_x_gyro = 0;
        float base_y_gyro = 0;
        float base_z_gyro = 0;
        float base_x_accel = 0;
        float base_y_accel = 0;
        float base_z_accel = 0;

        //private void Asdf_SensorInterruptEvent(object sender, MpuSensorEventArgs e)
        //{
        //    Debug.WriteLine("-----------------------");
        //    Debug.WriteLine(e.Values[0].GyroX);
        //    Debug.WriteLine(e.Values[0].GyroY);
        //    Debug.WriteLine(e.Values[0].GyroZ);

        //    //var gx = e.Values[0].GyroX;
        //    //var gy = e.Values[0].GyroY;
        //    //var gz = e.Values[0].GyroZ;

        //    //var ax = e.Values[0].AccelerationX;
        //    //var ay = e.Values[0].AccelerationY;
        //    //var az = e.Values[0].AccelerationZ;

        //    //if (_progress <= _samples)
        //    //{
        //    //    _progress++;

        //    //    base_x_gyro += gx;
        //    //    base_y_gyro += gy;
        //    //    base_z_gyro += gz;
        //    //    base_x_accel += ax;
        //    //    base_y_accel += ay;
        //    //    base_y_accel += az;


        //    //    return;
        //    //}



        //    //float gyro_x = (gx - base_x_gyro) / GYRO_FACTOR;
        //    //float gyro_y = (gy - base_y_gyro) / GYRO_FACTOR;
        //    //float gyro_z = (gz - base_z_gyro) / GYRO_FACTOR;
        //    //float accel_x = ax; // - base_x_accel;
        //    //float accel_y = ay; // - base_y_accel;
        //    //float accel_z = az; // - base_z_accel;


        //    //float accel_angle_y = atan(-1 * accel_x / sqrt(pow(accel_y, 2) + pow(accel_z, 2))) * RADIANS_TO_DEGREES;
        //    //float accel_angle_x = atan(accel_y / sqrt(pow(accel_x, 2) + pow(accel_z, 2))) * RADIANS_TO_DEGREES;
        //    //float accel_angle_z = 0;

        //    //// Compute the (filtered) gyro angles
        //    //float dt = (t_now - get_last_time()) / 1000.0;
        //    //float gyro_angle_x = gyro_x * dt + get_last_x_angle();
        //    //float gyro_angle_y = gyro_y * dt + get_last_y_angle();
        //    //float gyro_angle_z = gyro_z * dt + get_last_z_angle();

        //    //// Compute the drifting gyro angles
        //    //float unfiltered_gyro_angle_x = gyro_x * dt + get_last_gyro_x_angle();
        //    //float unfiltered_gyro_angle_y = gyro_y * dt + get_last_gyro_y_angle();
        //    //float unfiltered_gyro_angle_z = gyro_z * dt + get_last_gyro_z_angle();

        //    //// Apply the complementary filter to figure out the change in angle - choice of alpha is
        //    //// estimated now.  Alpha depends on the sampling rate...
        //    //float alpha = 0.96;
        //    //float angle_x = alpha * gyro_angle_x + (1.0 - alpha) * accel_angle_x;
        //    //float angle_y = alpha * gyro_angle_y + (1.0 - alpha) * accel_angle_y;
        //    //float angle_z = gyro_angle_z;  //Accelerometer doesn't give z-angle


        //}

        private readonly LegConfiguration _legConfiguration = new LegConfiguration();
        private XboxController _xboxController;
        private SerialDevice _serialPort;
        private DataWriter _dataWriter;
        private DataReader _dataReader;
        private readonly Stopwatch _sw = new Stopwatch();
        //private bool _balanceMode;

        public async void XboxControllerInit()
        {
            //USB\VID_045E&PID_0719\E02F1950 - receiver
            //USB\VID_045E & PID_02A1 & IG_00\6 & F079888 & 0 & 00  - XboxController
            //0x01, 0x05 = game controllers

            var deviceInformationCollection = await DeviceInformation.FindAllAsync(HidDevice.GetDeviceSelector(0x01, 0x05));

            if (deviceInformationCollection.Count == 0)
            {
                Debug.WriteLine("No Xbox360 XboxController found! Perhaps an appxmanifest issue?");
                return;
            }

            foreach (var d in deviceInformationCollection)
            {
                Debug.WriteLine("Device ID: " + d.Id);

                var hidDevice = await HidDevice.FromIdAsync(d.Id, Windows.Storage.FileAccessMode.Read);

                if (hidDevice == null)
                {
                    Debug.WriteLine("Failed to connect to the XboxController");
                    return;
                }

                _xboxController = new XboxController(hidDevice);

                _xboxController.LeftDirectionChanged += XboxControllerLeftDirectionChanged;
                _xboxController.RightDirectionChanged += XboxControllerRightDirectionChanged;
                _xboxController.DpadDirectionChanged += _xboxController_DpadDirectionChanged;
                _xboxController.LeftTriggerChanged += _xboxController_LeftTriggerChanged;
                _xboxController.RightTriggerChanged += _xboxController_RightTriggerChanged;
                break;
            }
        }

        private void _xboxController_RightTriggerChanged(int trigger)
        {
            _travelLengthX = Map(trigger, 0, 32640, 0, 90);
        }

        private void _xboxController_LeftTriggerChanged(int trigger)
        {
            _travelLengthX = -Map(trigger, 0, 32640, 0, 90);
        }

        private void _xboxController_ButtonChanged(int[] buttons)
        {
            for (var i = 0; i < buttons.Length;i++)
                Debug.WriteLine("Buttons changed " + i + " value " + buttons[i]);
        }

        private void _xboxController_DpadDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.UpRight:
                    if (_nomGaitSpeed < 250)
                        _nomGaitSpeed = _nomGaitSpeed + 10;
                    break;
                case ControllerDirection.UpLeft:
                    if (_nomGaitSpeed > 40)
                        _nomGaitSpeed = _nomGaitSpeed - 10;
                    break;
                case ControllerDirection.DownRight:
                    if (_legLiftHeight < 70)
                        _legLiftHeight = _legLiftHeight + 5;
                    break;
                case ControllerDirection.DownLeft:
                    if (_legLiftHeight > 15)
                        _legLiftHeight = _legLiftHeight - 5;
                    break;
                case ControllerDirection.Left:
                    if (_gaitType > 0)
                    {
                        _gaitType--;
                        GaitSelect();
                    }
                    break;
                case ControllerDirection.Right:
                    if (_gaitType < 4)
                    {
                        _gaitType++;
                        GaitSelect();
                    }
                    break;
                case ControllerDirection.Up:
                    if (_bodyPosY < 100)
                        _bodyPosY = _bodyPosY + 5;
                    break;
                case ControllerDirection.Down:
                    if (_bodyPosY > 30)
                        _bodyPosY = _bodyPosY - 5;
                    break;
            }
        }

        private void XboxControllerRightDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _travelRotationY = -(double)Map(sender.Magnitude, 0, 10000, 0, 4);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _travelRotationY = -(double)Map(sender.Magnitude, 0, 10000, 0, 2);
                    _travelLengthZ = -(double)Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.DownLeft:
                    _travelRotationY = -(double)Map(sender.Magnitude, 0, 10000, 0, 2);
                    _travelLengthZ = (double)Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.Right:
                    _travelRotationY = (double)Map(sender.Magnitude, 0, 10000, 0, 4);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpRight:
                    _travelRotationY = (double)Map(sender.Magnitude, 0, 10000, 0, 2);
                    _travelLengthZ = -(double)Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.DownRight:
                    _travelRotationY = (double)Map(sender.Magnitude, 0, 10000, 0, 2);
                    _travelLengthZ = (double)Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.Up:
                    _travelLengthZ = -(double)Map(sender.Magnitude, 0, 10000, 0, 130);
                    _travelRotationY = 0;
                    break;
                case ControllerDirection.Down:
                    _travelLengthZ = (double)Map(sender.Magnitude, 0, 10000, 0, 130);
                    _travelRotationY = 0;
                    break;
            }
        }

        private void XboxControllerLeftDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = -(double)Map(sender.Magnitude, 0, 10000, 0, 8);


                    break;
                case ControllerDirection.UpLeft:
                    _bodyRotX1 = (double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = -(double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.UpRight:
                    _bodyRotX1 = (double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = (double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.Right:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = (double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.Up:
                    _bodyRotX1 = (double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = 0;

                    break;
                case ControllerDirection.Down:
                    _bodyRotX1 = -(double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotZ1 = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyRotZ1 = -(double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotX1 = -(double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
                case ControllerDirection.DownRight:
                    _bodyRotZ1 = (double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    _bodyRotX1 = -(double)Map(sender.Magnitude, 0, 10000, 0, 8);
                    break;
            }
        }

        private async Task WriteSerial(string data)
        {
            if (_dataWriter == null)
            {
                Debug.WriteLine("Data writer is null, serial port not configured yet");
                return;
            }

            try
            {
                _dataWriter.WriteString(data);
                await _dataWriter.StoreAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private async Task<string> ReadSerial()
        {
            await WriteSerial("Q" + Convert.ToChar(13));

            var data = _dataReader.ReadString(2);
            return data;
        }

        private async void SerialSetup()
        {
            try
            {
                var dis = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                var selectedPort = dis.First();

                _serialPort = await SerialDevice.FromIdAsync(selectedPort.Id);

                _serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                _serialPort.BaudRate = 38400;
                _serialPort.Parity = SerialParity.None;
                _serialPort.StopBits = SerialStopBitCount.One;
                _serialPort.DataBits = 8;

                _dataReader = new DataReader(_serialPort.InputStream);
                _dataWriter = new DataWriter(_serialPort.OutputStream);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private const short CRrFemurHornOffset1 = 0;
        private const short CRmFemurHornOffset1 = 0;
        private const short CRfFemurHornOffset1 = 0;
        private const short CLrFemurHornOffset1 = 0;
        private const short CLmFemurHornOffset1 = 0;
        private const short CLfFemurHornOffset1 = 0;
        private const int CPfConst = 592; //old 650 ; 900*(1000/cPwmDiv)+cPFConst must always be 1500
        private const int CPwmDiv = 991; //old 1059, new 991;
        private const double CTravelDeadZone = 1;
        private const double C1Dec = 10;
        private const double C2Dec = 100;
        private const double C4Dec = 10000;
        private const double C6Dec = 1000000;

        private const int CLf = 5;
        private const int CLm = 4;
        private const int CLr = 3;
        private const int CRf = 2;
        private const int CRm = 1;
        private const int CRr = 0;

        private const int CoxaMin = -650; //-650
        private const int CoxaMax = 650; //650
        private const int FemurMin = -2850; //-1050
        private const int FemurMax = 2850; //150
        private const int TibiaMin = -2250; //-450
        private const int TibiaMax = 2250; //350

        private const int CRrCoxaMin1 = CoxaMin; //Mechanical limits of the Right Rear Leg
        private const int CRrCoxaMax1 = CoxaMax;
        private const int CRrFemurMin1 = FemurMin;
        private const int CRrFemurMax1 = FemurMax;
        private const int CRrTibiaMin1 = TibiaMin;
        private const int CRrTibiaMax1 = TibiaMax;
        private const int CRmCoxaMin1 = CoxaMin; //Mechanical limits of the Right Middle Leg
        private const int CRmCoxaMax1 = CoxaMax;
        private const int CRmFemurMin1 = FemurMin;
        private const int CRmFemurMax1 = FemurMax;
        private const int CRmTibiaMin1 = TibiaMin;
        private const int CRmTibiaMax1 = TibiaMax;
        private const int CRfCoxaMin1 = CoxaMin; //Mechanical limits of the Right Front Leg
        private const int CRfCoxaMax1 = CoxaMax;
        private const int CRfFemurMin1 = FemurMin;
        private const int CRfFemurMax1 = FemurMax;
        private const int CRfTibiaMin1 = TibiaMin;
        private const int CRfTibiaMax1 = TibiaMax;
        private const int CLrCoxaMin1 = CoxaMin; //Mechanical limits of the Left Rear Leg
        private const int CLrCoxaMax1 = CoxaMax;
        private const int CLrFemurMin1 = FemurMin;
        private const int CLrFemurMax1 = FemurMax;
        private const int CLrTibiaMin1 = TibiaMin;
        private const int CLrTibiaMax1 = TibiaMax;
        private const int CLmCoxaMin1 = CoxaMin; //Mechanical limits of the Left Middle Leg
        private const int CLmCoxaMax1 = CoxaMax;
        private const int CLmFemurMin1 = FemurMin;
        private const int CLmFemurMax1 = FemurMax;
        private const int CLmTibiaMin1 = TibiaMin;
        private const int CLmTibiaMax1 = TibiaMax;
        private const int CLfCoxaMin1 = CoxaMin; //Mechanical limits of the Left Front Leg
        private const int CLfCoxaMax1 = CoxaMax;
        private const int CLfFemurMin1 = FemurMin;
        private const int CLfFemurMax1 = FemurMax;
        private const int CLfTibiaMin1 = TibiaMin;
        private const int CLfTibiaMax1 = TibiaMax;

        private const double CRrCoxaAngle1 = -450;
        private const double CRmCoxaAngle1 = 0;
        private const double CRfCoxaAngle1 = 450;
        private const double CLrCoxaAngle1 = -450;
        private const double CLmCoxaAngle1 = 0;
        private const double CLfCoxaAngle1 = 450;

        private const double CRfOffsetX = -70;
        private const double CRfOffsetZ = -120;
        private const double CLfOffsetZ = -120;
        private const double CLfOffsetX = 70;
        private const double CRrOffsetZ = 120;
        private const double CRrOffsetX = -70;
        private const double CLrOffsetZ = 120;
        private const double CLrOffsetX = 70;
        private const double CRmOffsetX = -120;
        private const double CRmOffsetZ = 0;
        private const double CLmOffsetX = 120;
        private const double CLmOffsetZ = 0;

        private const double CoxaLength = 30; //30
        private const double FemurLength = 74; //60
        private const double TibiaLength = 125; //70
        private const double CRrCoxaLength = CoxaLength;
        private const double CRmCoxaLength = CoxaLength;
        private const double CRfCoxaLength = CoxaLength;
        private const double CLrCoxaLength = CoxaLength;
        private const double CLmCoxaLength = CoxaLength;
        private const double CLfCoxaLength = CoxaLength;
        private const double CRrFemurLength = FemurLength;
        private const double CRmFemurLength = FemurLength;
        private const double CRfFemurLength = FemurLength;
        private const double CLrFemurLength = FemurLength;
        private const double CLmFemurLength = FemurLength;
        private const double CLfFemurLength = FemurLength;
        private const double CLfTibiaLength = TibiaLength;
        private const double CLmTibiaLength = TibiaLength;
        private const double CLrTibiaLength = TibiaLength;
        private const double CRfTibiaLength = TibiaLength;
        private const double CRmTibiaLength = TibiaLength;
        private const double CRrTibiaLength = TibiaLength;
        private const double CHexInitXz = 105;
        private const double CHexInitXzCos45 = 74; // COS(45) = .7071
        private const double CHexInitXzSin45 = 74; // sin(45) = .7071
        private const double CHexInitY = 36;
        private const double CRfInitPosX = CHexInitXzCos45;
        private const double CRfInitPosY = CHexInitY;
        private const double CRfInitPosZ = -CHexInitXzSin45;
        private const double CLrInitPosX = CHexInitXzCos45;
        private const double CLrInitPosY = CHexInitY;
        private const double CLrInitPosZ = CHexInitXzCos45;
        private const double CLmInitPosX = CHexInitXz;
        private const double CLmInitPosY = CHexInitY;
        private const double CLmInitPosZ = 0;
        private const double CLfInitPosX = CHexInitXzCos45;
        private const double CLfInitPosY = CHexInitY;
        private const double CLfInitPosZ = -CHexInitXzSin45;
        private const double CRmInitPosX = CHexInitXz;
        private const double CRmInitPosY = CHexInitY;
        private const double CRmInitPosZ = 0;
        private const double CRrInitPosX = CHexInitXzCos45;
        private const double CRrInitPosY = CHexInitY;
        private const double CRrInitPosZ = CHexInitXzSin45;

        private readonly double[] _cCoxaAngle1 =
        {
            CRrCoxaAngle1, CRmCoxaAngle1, CRfCoxaAngle1, CLrCoxaAngle1, CLmCoxaAngle1, CLfCoxaAngle1
        };

        private readonly double[] _cCoxaLength =
        {
            CRrCoxaLength, CRmCoxaLength, CRfCoxaLength, CLrCoxaLength, CLmCoxaLength, CLfCoxaLength
        };

        private readonly int[] _cCoxaMax1 =
        {
            CRrCoxaMax1, CRmCoxaMax1, CRfCoxaMax1, CLrCoxaMax1, CLmCoxaMax1, CLfCoxaMax1
        };

        private readonly int[] _cCoxaMin1 =
        {
            CRrCoxaMin1, CRmCoxaMin1, CRfCoxaMin1, CLrCoxaMin1, CLmCoxaMin1, CLfCoxaMin1
        };

        private readonly short[] _cFemurHornOffset1 =
        {
            CRrFemurHornOffset1, CRmFemurHornOffset1, CRfFemurHornOffset1, CLrFemurHornOffset1, CLmFemurHornOffset1, CLfFemurHornOffset1
        };

        private readonly double[] _cFemurLength =
        {
            CRrFemurLength, CRmFemurLength, CRfFemurLength, CLrFemurLength, CLmFemurLength, CLfFemurLength
        };

        private readonly int[] _cFemurMax1 =
        {
            CRrFemurMax1, CRmFemurMax1, CRfFemurMax1, CLrFemurMax1, CLmFemurMax1, CLfFemurMax1
        };

        private readonly int[] _cFemurMin1 =
        {
            CRrFemurMin1, CRmFemurMin1, CRfFemurMin1, CLrFemurMin1, CLmFemurMin1, CLfFemurMin1
        };

        private readonly double[] _cInitPosX =
        {
            CRrInitPosX, CRmInitPosX, CRfInitPosX, CLrInitPosX, CLmInitPosX, CLfInitPosX
        };

        private readonly double[] _cInitPosY =
        {
            CRrInitPosY, CRmInitPosY, CRfInitPosY, CLrInitPosY, CLmInitPosY, CLfInitPosY
        };

        private readonly double[] _cInitPosZ =
        {
            CRrInitPosZ, CRmInitPosZ, CRfInitPosZ, CLrInitPosZ, CLmInitPosZ, CLfInitPosZ
        };

        private readonly double[] _cOffsetX = { CRrOffsetX, CRmOffsetX, CRfOffsetX, CLrOffsetX, CLmOffsetX, CLfOffsetX };
        private readonly double[] _cOffsetZ = { CRrOffsetZ, CRmOffsetZ, CRfOffsetZ, CLrOffsetZ, CLmOffsetZ, CLfOffsetZ };
        private readonly double[] _coxaAngle1 = new double[6];

        private readonly double[] _cTibiaLength =
        {
            CRrTibiaLength, CRmTibiaLength, CRfTibiaLength, CLrTibiaLength, CLmTibiaLength, CLfTibiaLength
        };

        private readonly int[] _cTibiaMax1 =
        {
            CRrTibiaMax1, CRmTibiaMax1, CRfTibiaMax1, CLrTibiaMax1, CLmTibiaMax1, CLfTibiaMax1
        };

        private readonly int[] _cTibiaMin1 =
        {
            CRrTibiaMin1, CRmTibiaMin1, CRfTibiaMin1, CLrTibiaMin1, CLmTibiaMin1, CLfTibiaMin1
        };

        private readonly double[] _femurAngle1 = new double[6]; //Actual Angle of the vertical hip, decimals = 1
        private readonly byte[] _gaitLegNr = new byte[6]; //Init position of the leg
        private readonly double[] _gaitPosX = new double[6]; //Array containing Relative X position corresponding to the Gait
        private readonly double[] _gaitPosY = new double[6]; //Array containing Relative Y position corresponding to the Gait
        private readonly double[] _gaitPosZ = new double[6]; //Array containing Relative Z position corresponding to the Gait
        private readonly double[] _gaitRotY = new double[6]; //Array containing Relative Y rotation corresponding to the Gait
        private readonly double[] _legPosX = new double[6]; //Actual X Position of the Leg should be length of 6
        private readonly double[] _legPosY = new double[6]; //Actual Y Position of the Leg
        private readonly double[] _legPosZ = new double[6]; //Actual Z Position of the Leg
        private readonly double[] _tibiaAngle1 = new double[6]; //Actual Angle of the knee, decimals = 1

        private int _gaitStep;
        private int _halfLiftHeigth; //If TRUE the outer positions of the ligted legs will be half height    
        private int _ikSolution; //Output true if the solution is possible
        private int _ikSolutionError; //Output true if the solution is NOT possible
        private int _ikSolutionWarning; //Output true if the solution is NEARLY possible
        private int _lastLeg; //TRUE when the current leg is the last leg of the sequence
        private byte _liftDivFactor; //Normaly: 2, when NrLiftedPos=5: 4
        private long _nrLiftedPos; //Number of positions that a single leg is lifted [1-3]
        private byte _stepsInGait; //Number of steps in gait
        private double _tlDivFactor; //Number of steps that a leg is on the floor while walking
        private bool _travelRequest; //Temp to check if the gait is in motion
        private double _bodyPosX; //Global Input for the position of the body
        private double _bodyPosY;
        private double _bodyPosZ;
        private double _bodyRotOffsetX; //Input X offset value to adjust centerpoint of rotation
        private double _bodyRotOffsetY; //Input Y offset value to adjust centerpoint of rotation
        private double _bodyRotOffsetZ; //Input Z offset value to adjust centerpoint of rotation
        private double _bodyRotX1; //Global Input pitch of the body
        private double _bodyRotY1; //Global Input rotation of the body
        private double _bodyRotZ1; //Global Input roll of the body
        private int _gaitType;
        private double _legLiftHeight; //Current Travel height
        private int _nomGaitSpeed = 40; //Nominal speed of the gait
        private double _travelLengthX; //Current Travel length X
        private double _travelLengthZ; //Current Travel length Z
        private double _travelRotationY; //Current Travel Rotation Y

        //Used to store start values of gyro when in balance mode
        private double _yawLevel;
        private double _pitchLevel;
        private double _rollLevel;

        private static double Map(double x, double inMin, double inMax, double outMin, double outMax)
        {
            var r = (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
            return r;
        }

        /// <summary>
        /// This does not work yet
        /// </summary>
        //void Balance()
        //{
        //    if (!BalanceMode) return;

        //    if (_sensor.Roll < 2 && _sensor.Roll > -2)
        //    {
        //        if (_sensor.Roll > RollLevel + .8)
        //        {
        //            BodyRotZ1 = Math.Round((Map(_sensor.Roll, RollLevel, 8, 0, 8)));
        //        }
        //        else if (_sensor.Roll < -(RollLevel - .8))
        //        {
        //            BodyRotZ1 = -Math.Abs(Math.Round((Map(_sensor.Roll, RollLevel, 8, 0, 8))));
        //        }
        //        else
        //            BodyRotZ1 = 0;
        //    }

        //    if (_sensor.Pitch > 2 || _sensor.Pitch < -2) return;

        //    if (_sensor.Pitch > (PitchLevel + .6))
        //    {
        //        BodyRotX1 = Math.Round((Map(_sensor.Pitch, PitchLevel, 8, 0, 8)));
        //    }
        //    else if (_sensor.Pitch < -(PitchLevel - .6))
        //    {
        //        BodyRotX1 = -Math.Abs(Math.Round((Map(_sensor.Pitch, PitchLevel, 8, 0, 8))));
        //    }
        //    else
        //        BodyRotX1 = 0;
        //}

        public async void Run()
        {
            SerialSetup();

            //Tars Init Positions
            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _legPosX[legIndex] = (_cInitPosX[legIndex]); //Set start positions for each leg
                _legPosY[legIndex] = (_cInitPosY[legIndex]);
                _legPosZ[legIndex] = (_cInitPosZ[legIndex]);
            }

            _gaitStep = 0;

            XboxControllerInit();

            _legLiftHeight = 15;
            _bodyPosY = 70;
            _gaitType = 1;

            GaitSelect();

            //RollLevel = _sensor.Roll;
            //PitchLevel = _sensor.Pitch;

            while (true)
            {
                //while (true)
                //{
                //    var r = await ReadSerial();
                //    if (r.Equals("."))
                //        break;
                //}

                //Balance();

                //Debug.WriteLine("TravelLengthZ " + TravelLengthZ);
                //Debug.WriteLine("TravelLengthX " + TravelLengthX);
                //Debug.WriteLine("TravelRotationY " + TravelRotationY);
                //Gait
                GaitSeq();

                //Reset IKsolution indicators 
                _ikSolution = 0;
                _ikSolutionWarning = 0;
                _ikSolutionError = 0;

                double bodyFkPosX;
                double bodyFkPosY;
                double bodyFkPosZ;

                //Do IK for all Right legs
                for (var legIndex = 0; legIndex <= 2; legIndex++)
                {
                    BodyFk(-_legPosX[legIndex] + _bodyPosX + _gaitPosX[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ + _gaitPosZ[legIndex],
                        _legPosY[legIndex] + _bodyPosY + _gaitPosY[legIndex],
                        _gaitRotY[legIndex], legIndex,
                        out bodyFkPosX, out bodyFkPosZ, out bodyFkPosY);

                    LegIk(_legPosX[legIndex] - _bodyPosX + bodyFkPosX - (_gaitPosX[legIndex]),
                        _legPosY[legIndex] + _bodyPosY - bodyFkPosY + _gaitPosY[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ - bodyFkPosZ + _gaitPosZ[legIndex],
                        legIndex);
                }

                //Do IK for all Left legs  
                for (var legIndex = 3; legIndex <= 5; legIndex++)
                {
                    BodyFk(_legPosX[legIndex] - _bodyPosX + _gaitPosX[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ + _gaitPosZ[legIndex],
                        _legPosY[legIndex] + _bodyPosY + _gaitPosY[legIndex],
                        _gaitRotY[legIndex], legIndex,
                        out bodyFkPosX, out bodyFkPosZ, out bodyFkPosY);

                    LegIk(_legPosX[legIndex] + _bodyPosX - bodyFkPosX + _gaitPosX[legIndex],
                        _legPosY[legIndex] + _bodyPosY - bodyFkPosY + _gaitPosY[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ - bodyFkPosZ + _gaitPosZ[legIndex],
                        legIndex);
                }

                //Debug.WriteLine("IKSolution " + _ikSolution);
                //Debug.WriteLine("IKSolutionWarning " + _ikSolutionWarning);
                //Debug.WriteLine("IKSolutionError " + _ikSolutionError);

                if (_ikSolutionError == 1 || _ikSolution == 0)
                    continue;

                //Task.Delay(NomGaitSpeed).Wait();

                _sw.Restart();
                while (_sw.ElapsedMilliseconds < _nomGaitSpeed)
                { }

                CheckAngles();
                UpdateServoDriver();


            }
        }

        public void GaitSelect()
        {
            //Gait selector
            switch (_gaitType)
            {
                case 0:
                    //Ripple Gait 12 steps
                    _gaitLegNr[CLr] = 1;
                    _gaitLegNr[CRf] = 3;
                    _gaitLegNr[CLm] = 5;
                    _gaitLegNr[CRr] = 7;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 11;

                    _nrLiftedPos = 3;
                    _halfLiftHeigth = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    //NomGaitSpeed = 110;
                    break;
                case 1:
                    //Tripod 8 steps
                    _gaitLegNr[CLr] = 5;
                    _gaitLegNr[CRf] = 1;
                    _gaitLegNr[CLm] = 1;
                    _gaitLegNr[CRr] = 1;
                    _gaitLegNr[CLf] = 5;
                    _gaitLegNr[CRm] = 5;

                    _nrLiftedPos = 3;
                    _halfLiftHeigth = 3;
                    _tlDivFactor = 4;
                    _stepsInGait = 8;
                    //NomGaitSpeed = 80;
                    break;
                case 2:
                    //Triple Tripod 12 step
                    _gaitLegNr[CRf] = 3;
                    _gaitLegNr[CLm] = 4;
                    _gaitLegNr[CRr] = 5;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 10;
                    _gaitLegNr[CLr] = 11;

                    _nrLiftedPos = 3;
                    _halfLiftHeigth = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    //NomGaitSpeed = 100;
                    break;
                case 3:
                    // Triple Tripod 16 steps, use 5 lifted positions
                    _gaitLegNr[CRf] = 4;
                    _gaitLegNr[CLm] = 5;
                    _gaitLegNr[CRr] = 6;
                    _gaitLegNr[CLf] = 12;
                    _gaitLegNr[CRm] = 13;
                    _gaitLegNr[CLr] = 14;

                    _nrLiftedPos = 5;
                    _halfLiftHeigth = 1;
                    _tlDivFactor = 10;
                    _stepsInGait = 16;
                    //NomGaitSpeed = 100;
                    break;
                case 4:
                    //Wave 24 steps
                    _gaitLegNr[CLr] = 1;
                    _gaitLegNr[CRf] = 21;
                    _gaitLegNr[CLm] = 5;

                    _gaitLegNr[CRr] = 13;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 17;

                    _nrLiftedPos = 3;
                    _halfLiftHeigth = 3;
                    _tlDivFactor = 20;
                    _stepsInGait = 24;
                    //NomGaitSpeed = 110;
                    break;
            }
        }

        private void GaitSeq()
        {
            //Check if the Gait is in motion
            _travelRequest = ((Math.Abs(_travelLengthX) > CTravelDeadZone) || (Math.Abs(_travelLengthZ) > CTravelDeadZone) || (Math.Abs(_travelRotationY) > CTravelDeadZone));

            if (_nrLiftedPos == 5)
                _liftDivFactor = 4;
            else
                _liftDivFactor = 2;

            //Calculate Gait sequence
            _lastLeg = 0;
            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                // for all legs
                if (legIndex == 5) // last leg
                    _lastLeg = 1;

                Gait(legIndex);
            } // next leg
        }

        private void Gait(int gaitCurrentLegNr)
        {
            //Clear values under the cTravelDeadZone
            if (!_travelRequest)
            {
                _travelLengthX = 0;
                _travelLengthZ = 0;
                _travelRotationY = 0;
            }
            //Leg middle up position
            //Gait in motion														  									
            //Gait NOT in motion, return to home position
            if ((_travelRequest && (_nrLiftedPos == 1 || _nrLiftedPos == 3 || _nrLiftedPos == 5) && _gaitStep == _gaitLegNr[gaitCurrentLegNr]) || (!_travelRequest && _gaitStep == _gaitLegNr[gaitCurrentLegNr] && ((Math.Abs(_gaitPosX[gaitCurrentLegNr]) > 2) || (Math.Abs(_gaitPosZ[gaitCurrentLegNr]) > 2) || (Math.Abs(_gaitRotY[gaitCurrentLegNr]) > 2))))
            {
                //Up
                _gaitPosX[gaitCurrentLegNr] = 0;
                _gaitPosY[gaitCurrentLegNr] = -_legLiftHeight;
                _gaitPosZ[gaitCurrentLegNr] = 0;
                _gaitRotY[gaitCurrentLegNr] = 0;
            }
            //Optional Half height Rear (2, 3, 5 lifted positions)
            else if (((_nrLiftedPos == 2 && _gaitStep == _gaitLegNr[gaitCurrentLegNr]) || (_nrLiftedPos >= 3 && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] - 1 || _gaitStep == _gaitLegNr[gaitCurrentLegNr] + (_stepsInGait - 1)))) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = -_travelLengthX / _liftDivFactor;
                _gaitPosY[gaitCurrentLegNr] = -3 * _legLiftHeight / (3 + _halfLiftHeigth);
                //Easier to shift between div factor: /1 (3/3), /2 (3/6) and 3/4
                _gaitPosZ[gaitCurrentLegNr] = -_travelLengthZ / _liftDivFactor;
                _gaitRotY[gaitCurrentLegNr] = -_travelRotationY / _liftDivFactor;
            }

            // Optional Half height front (2, 3, 5 lifted positions)
            else if ((_nrLiftedPos >= 2) && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] + 1 || _gaitStep == _gaitLegNr[gaitCurrentLegNr] - (_stepsInGait - 1)) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = _travelLengthX / _liftDivFactor;
                _gaitPosY[gaitCurrentLegNr] = -3 * _legLiftHeight / (3 + _halfLiftHeigth);
                // Easier to shift between div factor: /1 (3/3), /2 (3/6) and 3/4
                _gaitPosZ[gaitCurrentLegNr] = _travelLengthZ / _liftDivFactor;
                _gaitRotY[gaitCurrentLegNr] = _travelRotationY / _liftDivFactor;
            }

            //Optional Half heigth Rear 5 LiftedPos (5 lifted positions)
            else if (((_nrLiftedPos == 5 && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] - 2))) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = -_travelLengthX / 2;
                _gaitPosY[gaitCurrentLegNr] = -_legLiftHeight / 2;
                _gaitPosZ[gaitCurrentLegNr] = -_travelLengthZ / 2;
                _gaitRotY[gaitCurrentLegNr] = -_travelRotationY / 2;
            }

            //Optional Half heigth Front 5 LiftedPos (5 lifted positions)
            else if ((_nrLiftedPos == 5) && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] + 2 || _gaitStep == _gaitLegNr[gaitCurrentLegNr] - (_stepsInGait - 2)) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = _travelLengthX / 2;
                _gaitPosY[gaitCurrentLegNr] = -_legLiftHeight / 2;
                _gaitPosZ[gaitCurrentLegNr] = _travelLengthZ / 2;
                _gaitRotY[gaitCurrentLegNr] = _travelRotationY / 2;
            }

            //Leg front down position
            else if ((_gaitStep == _gaitLegNr[gaitCurrentLegNr] + _nrLiftedPos || _gaitStep == _gaitLegNr[gaitCurrentLegNr] - (_stepsInGait - _nrLiftedPos)) && _gaitPosY[gaitCurrentLegNr] < 0)
            {
                _gaitPosX[gaitCurrentLegNr] = _travelLengthX / 2;
                _gaitPosZ[gaitCurrentLegNr] = _travelLengthZ / 2;
                _gaitRotY[gaitCurrentLegNr] = _travelRotationY / 2;
                _gaitPosY[gaitCurrentLegNr] = 0;
                //Only move leg down at once if terrain adaption is turned off
            }

            //Move body forward      
            else
            {
                _gaitPosX[gaitCurrentLegNr] = _gaitPosX[gaitCurrentLegNr] - (_travelLengthX / _tlDivFactor);
                _gaitPosY[gaitCurrentLegNr] = 0;
                _gaitPosZ[gaitCurrentLegNr] = _gaitPosZ[gaitCurrentLegNr] - (_travelLengthZ / _tlDivFactor);
                _gaitRotY[gaitCurrentLegNr] = _gaitRotY[gaitCurrentLegNr] - (_travelRotationY / _tlDivFactor);
            }

            //Advance to the next step
            if (_lastLeg == 1)
            {
                //The last leg in this step
                _gaitStep = _gaitStep + 1;
                if (_gaitStep > _stepsInGait)
                    _gaitStep = 1;
            }
        }

        //[GETSINCOS] Get the sinus and cosinus from the angle +/- multiple circles
        //AngleDeg1     - Input Angle in degrees
        //sin4        - Output Sinus of AngleDeg
        //cos4          - Output Cosinus of AngleDeg
        private static void GetSinCos(double angleDeg1, out double sin, out double cos)
        {
            var angle = Math.PI * angleDeg1 / 180.0; //Convert to raidans

            sin = Math.Sin(angle) * 10000;
            cos = Math.Cos(angle) * 10000;
        }

        //(GETARCCOS) Get the sinus and cosinus from the angle +/- multiple circles
        //cos4        - Input Cosinus
        //AngleRad4     - Output Angle in AngleRad4
        private double GetArcCos(double cos4)
        {
            var c = cos4 / 10000; //Wont work right unless you do / 10000 then * 10000
            return (Math.Abs(c) == 1.0 ? (1 - c) * Math.PI / 2.0 : Math.Atan(-c / Math.Sqrt(1 - c * c)) + 2 * Math.Atan(1)) * 10000; ;
        }

        //(GETATAN2) Simplyfied ArcTan2 function based on fixed point ArcCos
        //ArcTanX         - Input X
        //ArcTanY         - Input Y
        //ArcTan4          - Output ARCTAN2(X/Y)
        //XYhyp2            - Output presenting Hypotenuse of X and Y
        private double GetATan2(double atanX, double atanY, out double xyhyp2)
        {
            double atan4;

            xyhyp2 = Math.Sqrt((atanX * atanX * C4Dec) + (atanY * atanY * C4Dec));

            var angleRad4 = GetArcCos((atanX * C6Dec) / xyhyp2);

            if (atanY < 0) // removed overhead... Atan4 = AngleRad4 * (AtanY/abs(AtanY));  
                atan4 = -angleRad4;
            else
                atan4 = angleRad4;

            return atan4;
        }

        //(BODY INVERSE KINEMATICS) 
        //BodyRotX         - Global Input pitch of the body 
        //BodyRotY         - Global Input rotation of the body 
        //BodyRotZ         - Global Input roll of the body 
        //RotationY         - Input Rotation for the gait 
        //PosX            - Input position of the feet X 
        //PosZ            - Input position of the feet Z 
        //SinB                  - Sin buffer for BodyRotX
        //CosB               - Cos buffer for BodyRotX 
        //SinG                  - Sin buffer for BodyRotZ
        //CosG               - Cos buffer for BodyRotZ
        //BodyFKPosX         - Output Position X of feet with Rotation 
        //BodyFKPosY         - Output Position Y of feet with Rotation 
        //BodyFKPosZ         - Output Position Z of feet with Rotation
        private void BodyFk(double posX, double posZ, double posY, double rotationY, int bodyIkLeg, out double bodyFkPosX, out double bodyFkPosZ, out double bodyFkPosY)
        {
            double sinA4; //Sin buffer for BodyRotX calculations
            double cosA4; //Cos buffer for BodyRotX calculations
            double sinB4; //Sin buffer for BodyRotX calculations
            double cosB4; //Cos buffer for BodyRotX calculations
            double sinG4; //Sin buffer for BodyRotZ calculations
            double cosG4; //Cos buffer for BodyRotZ calculations

            //Calculating totals from center of the body to the feet 
            var cprX = (_cOffsetX[bodyIkLeg]) + posX + _bodyRotOffsetX;
            var cprY = posY + _bodyRotOffsetY;
            var cprZ = (_cOffsetZ[bodyIkLeg]) + posZ + _bodyRotOffsetZ;

            //Successive global rotation matrix: 
            //Math shorts for rotation: Alfa [A] = Xrotate, Beta [B] = Zrotate, Gamma [G] = Yrotate 
            //Sinus Alfa = SinA, cosinus Alfa = cosA. and so on... 

            //First calculate sinus and cosinus for each rotation: 
            GetSinCos(_bodyRotX1, out sinG4, out cosG4);

            GetSinCos(_bodyRotZ1, out sinB4, out cosB4);

            GetSinCos(_bodyRotY1 + (rotationY * C1Dec), out sinA4, out cosA4);

            //Calculation of rotation matrix: 
            bodyFkPosX = (cprX * C2Dec - ((cprX * C2Dec * cosA4 / C4Dec * cosB4 / C4Dec) - (cprZ * C2Dec * cosB4 / C4Dec * sinA4 / C4Dec) + (cprY * C2Dec * sinB4 / C4Dec))) / C2Dec;
            bodyFkPosZ = (cprZ * C2Dec - ((cprX * C2Dec * cosG4 / C4Dec * sinA4 / C4Dec) + (cprX * C2Dec * cosA4 / C4Dec * sinB4 / C4Dec * sinG4 / C4Dec) + (cprZ * C2Dec * cosA4 / C4Dec * cosG4 / C4Dec) - (cprZ * C2Dec * sinA4 / C4Dec * sinB4 / C4Dec * sinG4 / C4Dec) - (cprY * C2Dec * cosB4 / C4Dec * sinG4 / C4Dec))) / C2Dec;
            bodyFkPosY = (cprY * C2Dec - ((cprX * C2Dec * sinA4 / C4Dec * sinG4 / C4Dec) - (cprX * C2Dec * cosA4 / C4Dec * cosG4 / C4Dec * sinB4 / C4Dec) + (cprZ * C2Dec * cosA4 / C4Dec * sinG4 / C4Dec) + (cprZ * C2Dec * cosG4 / C4Dec * sinA4 / C4Dec * sinB4 / C4Dec) + (cprY * C2Dec * cosB4 / C4Dec * cosG4 / C4Dec))) / C2Dec;
        }

        //[LEG INVERSE KINEMATICS] Calculates the angles of the coxa, femur and tibia for the given position of the feet
        //IKFeetPosX            - Input position of the Feet X
        //IKFeetPosY            - Input position of the Feet Y
        //IKFeetPosZ            - Input Position of the Feet Z
        //IKSolution            - Output true if the solution is possible
        //IKSolutionWarning     - Output true if the solution is NEARLY possible
        //IKSolutionError    - Output true if the solution is NOT possible
        //FemurAngle1           - Output Angle of Femur in degrees
        //TibiaAngle1           - Output Angle of Tibia in degrees
        //CoxaAngle1            - Output Angle of Coxa in degrees
        private void LegIk(double ikFeetPosX, double ikFeetPosY, double ikFeetPosZ, int legIkLegNr)
        {
            double xyhyp2;

            //Calculate IKCoxaAngle and IKFeetPosXZ
            var getatan = GetATan2(ikFeetPosX, ikFeetPosZ, out xyhyp2);
            _coxaAngle1[legIkLegNr] = ((getatan * 180) / 3141) + (_cCoxaAngle1[legIkLegNr]);

            var ikFeetPosXz = xyhyp2 / C2Dec;
            var ika14 = GetATan2(ikFeetPosY, ikFeetPosXz - (_cCoxaLength[legIkLegNr]), out xyhyp2);
            var iksw2 = xyhyp2;
            var temp1 = ((((_cFemurLength[legIkLegNr]) * (_cFemurLength[legIkLegNr])) - ((_cTibiaLength[legIkLegNr]) * (_cTibiaLength[legIkLegNr]))) * C4Dec + (iksw2 * iksw2));
            var temp2 = 2 * (_cFemurLength[legIkLegNr]) * C2Dec * iksw2;
            var ika24 = GetArcCos(temp1 / (temp2 / C4Dec));

            _femurAngle1[legIkLegNr] = -(ika14 + ika24) * 180 / 3141 + 900 + _cFemurHornOffset1[legIkLegNr];

            temp1 = ((((_cFemurLength[legIkLegNr]) * (_cFemurLength[legIkLegNr])) + ((_cTibiaLength[legIkLegNr]) * (_cTibiaLength[legIkLegNr]))) * C4Dec - (iksw2 * iksw2));
            temp2 = (2 * (_cFemurLength[legIkLegNr]) * (_cTibiaLength[legIkLegNr]));

            _tibiaAngle1[legIkLegNr] = -(900 - GetArcCos(temp1 / temp2) * 180 / 3141);

            if (iksw2 < ((_cFemurLength[legIkLegNr]) + (_cTibiaLength[legIkLegNr]) - 30) * C2Dec)
                _ikSolution = 1;
            else
            {
                if (iksw2 < ((_cFemurLength[legIkLegNr]) + (_cTibiaLength[legIkLegNr])) * C2Dec)
                    _ikSolutionWarning = 1;
                else
                    _ikSolutionError = 1;
            }
        }

        //[CHECK ANGLES] Checks the mechanical limits of the servos
        private void CheckAngles()
        {
            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _coxaAngle1[legIndex] = Math.Min(Math.Max(_coxaAngle1[legIndex], (_cCoxaMin1[legIndex])), (_cCoxaMax1[legIndex]));
                _femurAngle1[legIndex] = Math.Min(Math.Max(_femurAngle1[legIndex], (_cFemurMin1[legIndex])), (_cFemurMax1[legIndex]));
                _tibiaAngle1[legIndex] = Math.Min(Math.Max(_tibiaAngle1[legIndex], (_cTibiaMin1[legIndex])), (_cTibiaMax1[legIndex]));
            }
        }

        // A PWM/deg factor of 10,09 give cPwmDiv = 991 and cPFConst = 592
        // For a modified 5645 (to 180 deg travel): cPwmDiv = 1500 and cPFConst = 900.
        //private void UpdateServoDriver()
        //{
        //    for (var legIndex = 0; legIndex <= 5; legIndex++)
        //    {
        //        if (legIndex < 3)
        //        {
        //            var wCoxaSscv = Math.Round((-_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            var wFemurSscv = Math.Round((-_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            var wTibiaSscv = Math.Round((-_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);

        //            _legConfiguration.SetCoxaTarget((LegPosition)legIndex, wCoxaSscv);
        //            _legConfiguration.SetFemurTarget((LegPosition)legIndex, wFemurSscv);
        //            _legConfiguration.SetTibiaTarget((LegPosition)legIndex, wTibiaSscv);
        //        }
        //        else
        //        {
        //            var wCoxaSscv = Math.Round((_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            var wFemurSscv = Math.Round(((_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst));
        //            var wTibiaSscv = Math.Round((_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);

        //            _legConfiguration.SetCoxaTarget((LegPosition)legIndex, wCoxaSscv);
        //            _legConfiguration.SetFemurTarget((LegPosition)legIndex, wFemurSscv);
        //            _legConfiguration.SetTibiaTarget((LegPosition)legIndex, wTibiaSscv);
        //        }
        //    }
        //}

        private void UpdateServoDriver()
        {
            var sscString = string.Empty;

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                if (legIndex < 3)
                {
                    var wCoxaSscv = Math.Round((-_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
                    var wFemurSscv = Math.Round((-_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
                    var wTibiaSscv = Math.Round((-_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);

                    if (legIndex == 0)
                    {
                        sscString += "#" + _legConfiguration.LegOne.CoxaServo + "P" + wCoxaSscv;
                        sscString += "#" + _legConfiguration.LegOne.FemurServo + "P" + wFemurSscv;
                        sscString += "#" + _legConfiguration.LegOne.TibiaServo + "P" + wTibiaSscv;
                    }
                    if (legIndex == 1)
                    {
                        sscString += "#" + _legConfiguration.LegTwo.CoxaServo + "P" + wCoxaSscv;
                        sscString += "#" + _legConfiguration.LegTwo.FemurServo + "P" + wFemurSscv;
                        sscString += "#" + _legConfiguration.LegTwo.TibiaServo + "P" + wTibiaSscv;
                    }
                    if (legIndex == 2)
                    {
                        sscString += "#" + _legConfiguration.LegThree.CoxaServo + "P" + wCoxaSscv;
                        sscString += "#" + _legConfiguration.LegThree.FemurServo + "P" + wFemurSscv;
                        sscString += "#" + _legConfiguration.LegThree.TibiaServo + "P" + wTibiaSscv;
                    }
                }
                else
                {
                    var wCoxaSscv = Math.Round((_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
                    var wFemurSscv = Math.Round(((_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst));
                    var wTibiaSscv = Math.Round((_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);

                    if (legIndex == 5)
                    {
                        sscString += "#" + _legConfiguration.LegFour.CoxaServo + "P" + wCoxaSscv;
                        sscString += "#" + _legConfiguration.LegFour.FemurServo + "P" + wFemurSscv;
                        sscString += "#" + _legConfiguration.LegFour.TibiaServo + "P" + wTibiaSscv;
                    }
                    if (legIndex == 4)
                    {
                        sscString += "#" + _legConfiguration.LegFive.CoxaServo + "P" + wCoxaSscv;
                        sscString += "#" + _legConfiguration.LegFive.FemurServo + "P" + wFemurSscv;
                        sscString += "#" + _legConfiguration.LegFive.TibiaServo + "P" + wTibiaSscv;
                    }
                    if (legIndex == 3)
                    {
                        sscString += "#" + _legConfiguration.LegSix.CoxaServo + "P" + wCoxaSscv;
                        sscString += "#" + _legConfiguration.LegSix.FemurServo + "P" + wFemurSscv;
                        sscString += "#" + _legConfiguration.LegSix.TibiaServo + "P" + wTibiaSscv;
                    }
                }
            }

            sscString += "T" + (_nomGaitSpeed);

            WriteSerial(sscString + Convert.ToChar(13));
        }

        //private void UpdateServoDriver()
        //{
        //    var builder = new StringBuilder();

        //    for (var legIndex = 0; legIndex <= 5; legIndex++)
        //    {
        //        double wCoxaSscv;
        //        double wFemurSscv;
        //        double wTibiaSscv;

        //        var leg = _legConfiguration.Legs.First(l => l.LegPosition == (LegPosition)legIndex);

        //        if (legIndex < 3)
        //        {
        //            wCoxaSscv = Math.Round((-_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            wFemurSscv = Math.Round((-_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            wTibiaSscv = Math.Round((-_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //        }
        //        else
        //        {
        //            wCoxaSscv = Math.Round((_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            wFemurSscv = Math.Round((_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //            wTibiaSscv = Math.Round((_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
        //        }

        //        builder.Append(string.Format("#{0}{1}{2}", leg.CoxaServo, "P", wCoxaSscv));
        //        builder.Append(string.Format("#{0}{1}{2}", leg.FemurServo, "P", wFemurSscv));
        //        builder.Append(string.Format("#{0}{1}{2}", leg.TibiaServo, "P", wTibiaSscv));
        //    }

        //    builder.Append(string.Format("T{0}", _nomGaitSpeed));

        //    WriteSerial(builder.ToString() + Convert.ToChar(13));
        //}
    }
}
