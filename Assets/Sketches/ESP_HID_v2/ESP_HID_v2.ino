#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

#include "USB.h"
#include "USBHIDGamepad.h"

Adafruit_MPU6050 mpu;
USBHIDGamepad Gamepad;

const int potPin = A7;
const int buttonPin = 12;
const int actionButtonPin = 11;

float pitchOffset = 0.0f;
float rollOffset = 0.0f;
bool lastButtonState = HIGH;

bool invertRoll = false;
float pitchDeadzone = 4.0f;
float rollDeadzone = 4.0f;
bool enableDebugSerial = true;

float calculatePitch(float ax, float ay, float az)
{
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0 / PI;
}

float calculateRoll(float ax, float ay, float az)
{
  return atan2(-ax, az) * 180.0 / PI;
}

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

float applyAngleDeadzone(float value, float deadzone)
{
  if (fabs(value) < deadzone)
    return 0.0f;

  return value;
}

int8_t convertAngleToHIDRange(float angleDegrees)
{
  // ESP32 Gamepad library expects 8-bit signed integers (-127 to 127)
  long mapped = map(constrain(angleDegrees, -90, 90), -90, 90, -127, 127);
  return (int8_t)constrain(mapped, -127, 127);
}

int8_t convertPotToHIDRange(int potRaw)
{
  // Map 12-bit ESP32 ADC (0 to 4095) to 8-bit signed integer (-127 to 127)
  long mapped = map(constrain(potRaw, 0, 4095), 0, 4095, -127, 127);
  return (int8_t)constrain(mapped, -127, 127);
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
  if (enableDebugSerial)
  {
    Serial.begin(115200);
    delay(250);
  }

  Gamepad.begin();
  USB.begin();

  if (!mpu.begin())
  {
    while (1) { delay(10); }
  }

  mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
  mpu.setGyroRange(MPU6050_RANGE_500_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

  pinMode(potPin, INPUT);
  pinMode(buttonPin, INPUT_PULLUP);
  pinMode(actionButtonPin, INPUT_PULLUP);

  analogReadResolution(12);

  delay(1000);
  calibrateSensor();

  if (enableDebugSerial)
  {
    Serial.println("HID ready");
    Serial.print("pitchOffset: ");
    Serial.println(pitchOffset);
    Serial.print("rollOffset: ");
    Serial.println(rollOffset);
  }
}

void loop()
{
  int potRaw = readSmoothPotRaw(potPin);

  bool currentState = digitalRead(buttonPin);
  if (lastButtonState == HIGH && currentState == LOW)
  {
    calibrateSensor();

    if (enableDebugSerial)
      Serial.println("CALIBRATED");
  }
  lastButtonState = currentState;

  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  if (invertRoll)
    roll = -roll;

  pitch = applyAngleDeadzone(pitch, pitchDeadzone);
  roll = applyAngleDeadzone(roll, rollDeadzone);

  int actionButtonPressed = (digitalRead(actionButtonPin) == LOW) ? 1 : 0;

 // Map everything to 8-bit
  int8_t mappedPitch = convertAngleToHIDRange(pitch);
  int8_t mappedRoll = convertAngleToHIDRange(roll);
  int8_t mappedPot = convertPotToHIDRange(potRaw);

  // Send to Unity
  Gamepad.leftStick(mappedRoll, mappedPitch);
  Gamepad.rightStick(mappedPot, 0);

  if (actionButtonPressed == 1)
    Gamepad.pressButton(1);
  else
    Gamepad.releaseButton(1);

  if (enableDebugSerial)
  {
    static unsigned long lastDebugTime = 0;
    if (millis() - lastDebugTime >= 200)
    {
      lastDebugTime = millis();

      Serial.print("pitch: ");
      Serial.print(pitch, 2);
      Serial.print(" | roll: ");
      Serial.print(roll, 2);
      Serial.print(" | potRaw: ");
      Serial.print(potRaw);
      Serial.print(" | action: ");
      Serial.println(actionButtonPressed);
    }
  }

  delay(10);
}