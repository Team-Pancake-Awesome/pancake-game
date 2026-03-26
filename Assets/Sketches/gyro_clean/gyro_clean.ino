#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

Adafruit_MPU6050 mpu;

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

void calibrateSensor()
{
  const int samples = 200;
  float pitchSum = 0.0f;
  float rollSum = 0.0f;

  Serial.println("Calibrating... keep sensor still");

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

  Serial.print("Pitch offset: ");
  Serial.println(pitchOffset);
  Serial.print("Roll offset: ");
  Serial.println(rollOffset);
  Serial.println("Calibration done");
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

  delay(1000);
  calibrateSensor();
}

void loop()
{
  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  // Send clean CSV for Unity: pitch,roll
  Serial.print(pitch, 2);
  Serial.print(",");
  Serial.println(roll, 2);

  delay(20); // about 50 updates/sec
}