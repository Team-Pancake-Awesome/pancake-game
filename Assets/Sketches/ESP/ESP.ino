#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

Adafruit_MPU6050 mpu;

const int potPin = A7;
const int buttonPin = 12;
const int actionButtonPin = 11;

float pitchOffset = 0.0f;
float rollOffset = 0.0f;
bool lastButtonState = HIGH;

float calculatePitch(float ax, float ay, float az)
{
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0 / PI;
}

float calculateRoll(float ax, float ay, float az)
{
  return atan2(-ax, az) * 180.0 / PI;
}

// Read raw ESP32 ADC value
int readSmoothPotRaw(int pin)
{
  long total = 0;
  const int samples = 8;

  for (int i = 0; i < samples; i++)
  {
    total += analogRead(pin);
    delay(1);
  }

  return total / samples;
}

// Convert ESP32 12-bit ADC range (0-4095) to Uno-style 10-bit range (0-1023)
int convertPotToUnoRange(int rawValue)
{
  int mapped = map(rawValue, 0, 4095, 0, 1023);
  return constrain(mapped, 0, 1023);
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
    float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z);

    pitchSum += pitch;
    rollSum += roll;

    delay(10);
  }

  pitchOffset = pitchSum / samples;
  rollOffset  = rollSum / samples;
}

void setup()
{
  Serial.begin(115200);
  //while (!Serial) { delay(10); }

  Serial.println("Nano ESP32 Awake! Searching for Gyro...");

  if (!mpu.begin())
  {
    Serial.println("ERROR: Could not find MPU6050!");
    while (1) { delay(10); }
  }

  Serial.println("MPU6050 Found! Starting data stream...");

  mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
  mpu.setGyroRange(MPU6050_RANGE_500_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

  pinMode(potPin, INPUT);
  pinMode(buttonPin, INPUT_PULLUP);
  pinMode(actionButtonPin, INPUT_PULLUP);

  // Important: ESP32 ADC is usually 12-bit by default, but set it explicitly
  analogReadResolution(12);

  delay(1000);
  calibrateSensor();
}

void loop()
{
  int potRaw = readSmoothPotRaw(potPin);
  int potValue = convertPotToUnoRange(potRaw);

  bool currentState = digitalRead(buttonPin);
  if (lastButtonState == HIGH && currentState == LOW)
  {
    calibrateSensor();
    Serial.println("CALIBRATED");
  }
  lastButtonState = currentState;

  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  // Keep these matching the old Unity expectations
  float gyroY = g.gyro.y;          // rad/s
  float accelZ = a.acceleration.z; // m/s^2

  int actionButtonPressed = (digitalRead(actionButtonPin) == LOW) ? 1 : 0;

  // Exact CSV format Unity expects:
  // pot,pitch,roll,gyroY,accelZ,actionButton
  Serial.print(potValue);
  Serial.print(",");
  Serial.print(pitch, 2);
  Serial.print(",");
  Serial.print(roll, 2);
  Serial.print(",");
  Serial.print(gyroY, 2);
  Serial.print(",");
  Serial.print(accelZ, 2);
  Serial.print(",");
  Serial.println(actionButtonPressed);

  delay(20);
}