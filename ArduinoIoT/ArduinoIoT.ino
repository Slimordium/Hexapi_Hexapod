
#define leftTrigPin 9
#define leftEchoPin 8

#define centerTrigPin 11
#define centerEchoPin 10

#define rightTrigPin 12
#define rightEchoPin 13

char pingData[8];

void setup()
{
	pinMode(leftTrigPin, OUTPUT); //trig
	pinMode(leftEchoPin, INPUT); //echo

	pinMode(centerTrigPin, OUTPUT); //trig
	pinMode(centerEchoPin, INPUT); //echo

	pinMode(rightTrigPin, OUTPUT); //trig
	pinMode(rightEchoPin, INPUT); //echo

	Serial.begin(57600);

	while (!Serial) {
		// wait for serial port to connect. Needed for ATmega32u4-based boards and Arduino 101
	}
}

void loop()
{
	String center = String("!C" + Ping(2) + "?");
	center.toCharArray(pingData, 8);
	Serial.write(pingData);
	delay(30);

	String left = String("!L" + Ping(1) +"?");
	left.toCharArray(pingData, 8);
	Serial.write(pingData);
	delay(30);

	String right = String("!R" + Ping(3) + "?");
	right.toCharArray(pingData, 8);
	Serial.write(pingData);
	delay(30);
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

