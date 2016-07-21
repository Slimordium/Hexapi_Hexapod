
#define leftTrigPin 9
#define leftEchoPin 8

#define farLeftTrigPin 4
#define farLeftEchoPin 5

#define centerTrigPin 10
#define centerEchoPin 11

#define rightTrigPin 12
#define rightEchoPin 13

#define farRightTrigPin 7
#define farRightEchoPin 6

String center = "";
String left = "";
String farLeft = "";
String right = "";
String farRight = "";

String inYpr = "";
String inSensorData = "";

void setup()
{
	pinMode(leftTrigPin, OUTPUT); //trig
	pinMode(leftEchoPin, INPUT); //echo

	pinMode(farLeftTrigPin, OUTPUT); //trig
	pinMode(farLeftEchoPin, INPUT); //echo

	pinMode(centerTrigPin, OUTPUT); //trig
	pinMode(centerEchoPin, INPUT); //echo

	pinMode(rightTrigPin, OUTPUT); //trig
	pinMode(rightEchoPin, INPUT); //echo

	pinMode(farRightTrigPin, OUTPUT); //trig
	pinMode(farRightEchoPin, INPUT); //echo

	Serial.begin(57600); //TX debug

	Serial1.begin(57600); //TX - Raspberry PI 3 - Stream all data here, PI will parse and act

	Serial2.begin(57600); //RX/TX - SparkFun Razor IMU

	delay(100);

	//Serial2.println("#o0"); //Disable streaming from Razor IMU, send frames only as they are requested.

	//delay(100);
}

void loop()
{
	Serial2.println("#o0"); //Disable streaming from Razor IMU, send frames only as they are requested.

	center = String("#C" + Ping(2));
	Serial.println(center);
	Serial1.println(center);

	GetYpr();
	GetRawData();

	left = String("#FL" + Ping(5));
	Serial.println(left);
	Serial1.println(left);

	GetYpr();
	GetRawData();

	right = String("#R" + Ping(3));
	Serial.println(right);
	Serial1.println(right);

	GetYpr();
	GetRawData();

	left = String("#L" + Ping(1));
	Serial.println(left);
	Serial1.println(left);

	GetYpr();
	GetRawData();

	right = String("#FR" + Ping(4));
	Serial.println(right);
	Serial1.println(right);

	GetYpr();
	GetRawData();
}

void GetYpr()
{
	Serial2.println("#ot"); //Ask for YPR
	Serial2.println("#f"); //Request frame from Razor
	inYpr = Serial2.readStringUntil('\n');
	Serial.println(String(inYpr));
	Serial1.println(String(inYpr));
	delay(12);
}

void GetRawData()
{
	Serial2.println("#osct"); //Ask for data from accelerometer, gyroscope, magnometer
	Serial2.println("#f"); //Request frame from Razor
	inSensorData = Serial2.readStringUntil('\n');
	inSensorData += Serial2.readStringUntil('\n');
	inSensorData += Serial2.readStringUntil('\n');
	Serial.println(String(inSensorData));
	Serial1.println(String(inSensorData));
	delay(12);
}

String Ping(int sensor)
{
	int trigPin;
	int echoPin;

	if (sensor == 1)
	{
		trigPin = leftTrigPin;
		echoPin = leftEchoPin;
	}
	else if (sensor == 2)
	{
		trigPin = centerTrigPin;
		echoPin = centerEchoPin;
	}
	else if (sensor == 3)
	{
		trigPin = rightTrigPin;
		echoPin = rightEchoPin;
	}
	else if (sensor == 4)
	{
		trigPin = farRightTrigPin;
		echoPin = farRightEchoPin;
	}
	else if (sensor == 5)
	{
		trigPin = farLeftTrigPin;
		echoPin = farLeftEchoPin;
	}

	digitalWrite(trigPin, LOW);
	delayMicroseconds(2);
	digitalWrite(trigPin, HIGH);
	delayMicroseconds(10);
	digitalWrite(trigPin, LOW);

	long duration = pulseIn(echoPin, HIGH);

	String r = String(duration, DEC);

	return r;
}

