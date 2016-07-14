
#define leftTrigPin 9
#define leftEchoPin 8

#define centerTrigPin 11
#define centerEchoPin 10

#define rightTrigPin 12
#define rightEchoPin 13


void setup()
{
	pinMode(leftTrigPin, OUTPUT); //trig
	pinMode(leftEchoPin, INPUT); //echo

	pinMode(centerTrigPin, OUTPUT); //trig
	pinMode(centerEchoPin, INPUT); //echo

	pinMode(rightTrigPin, OUTPUT); //trig
	pinMode(rightEchoPin, INPUT); //echo

	Serial.begin(57600);

	Serial1.begin(57600);

	Serial2.begin(57600);

	delay(100);

	Serial2.println("#o0"); //Disable streaming
}

String center = "";
String left = "";
String right = "";

String inYpr = "";
String inSensorData = "";

void loop()
{
	center = String("#C" + Ping(2));
	Serial.println(center);
	Serial1.println(center);

	GetYpr();
	GetSensorData();

	left = String("#L" + Ping(1));
	Serial.println(left);
	Serial1.println(left);

	GetYpr();
	GetSensorData();

	right = String("#R" + Ping(3));
	Serial.println(right);
	Serial1.println(right);

	GetYpr();
	GetSensorData();
}

void GetYpr()
{
	Serial2.println("#ot"); //Request YPR
	Serial2.println("#f"); //Request frame 
	inYpr = Serial2.readStringUntil('\n');
	Serial.println(String(inYpr));
	Serial1.println(String(inYpr));
	delay(2);
}

void GetSensorData()
{
	Serial2.println("#osct"); //Sensor data
	Serial2.println("#f"); //Request frame 
	inSensorData = Serial2.readStringUntil('\n');
	inSensorData += Serial2.readStringUntil('\n');
	inSensorData += Serial2.readStringUntil('\n');
	Serial.println(String(inSensorData));
	Serial1.println(String(inSensorData));
	delay(23);
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

	digitalWrite(trigPin, LOW);
	delayMicroseconds(2);
	digitalWrite(trigPin, HIGH);
	delayMicroseconds(10);
	digitalWrite(trigPin, LOW);

	long duration = pulseIn(echoPin, HIGH);

	String r = String(duration, DEC);

	return r;
}

