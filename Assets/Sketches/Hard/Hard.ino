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

// HID safety state
bool hidActive = false;
unsigned long buttonPressStartTime = 0;
bool longHoldHandled = false;

// Button timing
const unsigned long armHoldMs = 250;
const unsigned long disarmHoldMs = 1500;

// 8-bit HID center/range
const int8_t hidCenter = 0;
const int8_t hidMin = -127;
const int8_t hidMax = 127;

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
  long mapped = map(constrain(angleDegrees, -90, 90), -90, 90, hidMin, hidMax);
  return (int8_t)constrain(mapped, hidMin, hidMax);
}

int8_t convertPotToHIDRange(int potRaw)
{
  long mapped = map(constrain(potRaw, 0, 4095), 0, 4095, hidMin, hidMax);
  return (int8_t)constrain(mapped, hidMin, hidMax);
}

void sendNeutralState()
{
  Gamepad.leftStick(0,0);
  Gamepad.rightStick(0,0);
  Gamepad.releaseButton(1);
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

void armHID()
{
  calibrateSensor();
  hidActive = true;

  if (enableDebugSerial)
  {
    Serial.println("HID ARMED");
    Serial.print("pitchOffset: ");
    Serial.println(pitchOffset, 2);
    Serial.print("rollOffset: ");
    Serial.println(rollOffset, 2);
  }
}

void disarmHID()
{
  hidActive = false;
  sendNeutralState();

  if (enableDebugSerial)
    Serial.println("HID DISARMED");
}

void setup()
{
    Serial.begin(115200);
    delay(250);
  

  Gamepad.begin();
  USB.begin();

  if (!mpu.begin())
  {
    if (enableDebugSerial)
      Serial.println("ERROR: Could not find MPU6050!");

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
  sendNeutralState();

  if (enableDebugSerial)
  {
    Serial.println("SAFE HID READY");
    Serial.println("Short hold calibrate button to arm.");
    Serial.println("Long hold calibrate button to disarm.");
  }
}

void loop()
{
  bool currentState = digitalRead(buttonPin);

  if (lastButtonState == HIGH && currentState == LOW)
  {
    buttonPressStartTime = millis();
    longHoldHandled = false;
  }

  if (currentState == LOW)
  {
    unsigned long heldMs = millis() - buttonPressStartTime;

    if (!longHoldHandled && hidActive && heldMs >= disarmHoldMs)
    {
      disarmHID();
      longHoldHandled = true;
    }
  }

  if (lastButtonState == LOW && currentState == HIGH)
  {
    unsigned long heldMs = millis() - buttonPressStartTime;

    if (!longHoldHandled && heldMs >= armHoldMs)
    {
      armHID();
    }
  }

  lastButtonState = currentState;

  if (!hidActive)
  {
    sendNeutralState();
    delay(10);
    return;
  }

  int potRaw = readSmoothPotRaw(potPin);

  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  if (invertRoll)
    roll = -roll;

  pitch = applyAngleDeadzone(pitch, pitchDeadzone);
  roll = applyAngleDeadzone(roll, rollDeadzone);

  int actionButtonPressed = (digitalRead(actionButtonPin) == LOW) ? 1 : 0;

  int8_t mappedPitch = convertAngleToHIDRange(pitch);
  int8_t mappedRoll = convertAngleToHIDRange(roll);
  int8_t mappedPot = convertPotToHIDRange(potRaw);

  Gamepad.leftStick(mappedRoll, mappedPitch);
  Gamepad.rightStick(mappedPot, hidCenter);

  if (actionButtonPressed == 1)
    Gamepad.pressButton(1);
  else
    Gamepad.releaseButton(1);

  if (enableDebugSerial)
  {
    static unsigned long lastDebugTime = 0;
    if (millis() - lastDebugTime >= 250)
    {
      lastDebugTime = millis();

      Serial.print("pitch: ");
      Serial.print(pitch, 2);
      Serial.print(" | roll: ");
      Serial.print(roll, 2);
      Serial.print(" | mappedPitch: ");
      Serial.print(mappedPitch);
      Serial.print(" | mappedRoll: ");
      Serial.print(mappedRoll);
      Serial.print(" | mappedPot: ");
      Serial.print(mappedPot);
      Serial.print(" | action: ");
      Serial.println(actionButtonPressed);
    }
  }

  delay(10);
}