#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

Adafruit_MPU6050 mpu;

const int potPin = A3;


int seven = 7;
int six = 6;
int five = 5;
int eleven = 11;
int ten = 10;
int eight = 8;
int nine = 9;
int four = 4;

float pitchOffset = 0.0f;
float rollOffset = 0.0f;

float calculatePitch(float ax, float ay, float az)
{
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0 / PI;
}

float calculateRoll(float ax, float ay, float az)
{
  return atan2(-ax, az) * 180.0 / PI;
}

int readSmoothPot(int pin)
{
  long total = 0;
  for (int i = 0; i < 8; i++)
  {
    total += analogRead(pin);
    delay(1);
  }
  return total / 8;
}

void calibrateSensor()
{
  const int samples = 200;
  float pitchSum = 0.0f;
  float rollSum = 0.0f;

  for (int i = 0; i < samples; i++)
  {
    sensors_event_t a, g, temp;
    mpu.getEvent(&a, &g, &temp);

    float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z);
    float roll = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z);

    pitchSum += pitch;
    rollSum += roll;

    delay(10);
  }

  pitchOffset = pitchSum / samples;
  rollOffset = rollSum / samples;
}

// -------------------- 7-segment display code --------------------

void clearDisplay(void)
{
  digitalWrite(seven, LOW);
  digitalWrite(six, LOW);
  digitalWrite(nine, LOW);
  digitalWrite(five, LOW);
  digitalWrite(eleven, LOW);
  digitalWrite(ten, LOW);
  digitalWrite(eight, LOW);
  digitalWrite(four, LOW);
}

void display0(void)
{
  digitalWrite(five, HIGH);
  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(eleven, HIGH);
  digitalWrite(ten, HIGH);
}

void display1(void)
{
  digitalWrite(five, HIGH);
  digitalWrite(six, HIGH);
}

void display2(void)
{
  digitalWrite(eleven, HIGH);
  digitalWrite(ten, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);
}

void display3(void)
{
  digitalWrite(ten, HIGH);
  digitalWrite(eleven, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(seven, HIGH);
}

void display4(void)
{
  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(eleven, HIGH);
  digitalWrite(eight, HIGH);
}

void display5(void)
{
  digitalWrite(ten, HIGH);
  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(seven, HIGH);
}

void display6(void)
{
  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(ten, HIGH);
}

void display7(void)
{
  digitalWrite(ten, HIGH);
  digitalWrite(eleven, HIGH);
  digitalWrite(eight, HIGH);
}

void display8(void)
{
  digitalWrite(four, HIGH);
  digitalWrite(five, HIGH);
  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(ten, HIGH);
  digitalWrite(eleven, HIGH);
}

void display9(void)
{
  digitalWrite(ten, HIGH);
  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(eleven, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(nine, HIGH);
}

void displayDigit(int digit)
{
  clearDisplay();

  switch (digit)
  {
    case 0: display0(); break;
    case 1: display1(); break;
    case 2: display2(); break;
    case 3: display3(); break;
    case 4: display4(); break;
    case 5: display5(); break;
    case 6: display6(); break;
    case 7: display7(); break;
    case 8: display8(); break;
    case 9: display9(); break;
  }
}



void setup()
{
  Serial.begin(115200);

  while (!Serial)
  {
    delay(10);
  }

  if (!mpu.begin())
  {
    Serial.println("Failed to find MPU6050");
    while (1)
    {
      delay(10);
    }
  }

  mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
  mpu.setGyroRange(MPU6050_RANGE_500_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

  pinMode(potPin, INPUT);

  for (int i = 4; i <= 11; i++)
  {
    pinMode(i, OUTPUT);
  }

  clearDisplay();

  delay(1000);
  calibrateSensor();
}

void loop()
{
  int potValue = readSmoothPot(potPin);

  // turn pot range 14 1013 into digit 0..9
  //int constrainedPot = constrain(potValue, 14, 1013);
  //int potDigit = map(constrainedPot, 14, 1013, 0, 9);

  int constrainedPot = constrain(potValue, 60, 1013);
  int potDigit = map(constrainedPot, 60, 1013, 0, 9);


  displayDigit(potDigit);

  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  // Keep your Unity serial format the same: pot,pitch,roll
  Serial.print(potValue);
  Serial.print(",");
  Serial.print(pitch, 2);
  Serial.print(",");
  Serial.println(roll, 2);

  delay(20);
}