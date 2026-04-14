#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

#include "USB.h"
#include "USBHIDGamepad.h"

Adafruit_MPU6050 mpu;
USBHIDGamepad Gamepad;

// --- Pins ---
const int potPin = A7;
const int buttonPin = 12;
const int actionButtonPin = 11;
const int ledPin = LED_BUILTIN; 

// --- Tuning Variables ---
float pitchOffset = 0.0f;
float rollOffset = 0.0f;
bool invertRoll = false;
float pitchDeadzone = 4.0f;
float rollDeadzone = 4.0f;
bool enableDebugSerial = true;

// --- State Variables ---
bool hidActive = false;
bool wasDisarmed = false;
bool lastButtonState = HIGH;
unsigned long buttonPressStartTime = 0;
bool longHoldHandled = false;

const unsigned long armHoldMs = 250;
const unsigned long disarmHoldMs = 1500;

// --- HYBRID MATH FIX ---
// Left Stick (Spatula X/Y) expects Unsigned 0-255
const uint8_t hidLeftCenter = 127;
const uint8_t hidLeftMin = 0;
const uint8_t hidLeftMax = 255;

// Right Stick (Pot Z/RZ) expects Signed -127 to 127
const int8_t hidRightCenter = 0;
const int8_t hidRightMin = -127;
const int8_t hidRightMax = 127;

void setup() {
  Serial.begin(115200);
  Wire.begin(); 
  Gamepad.begin();
  USB.begin();

  pinMode(potPin, INPUT);
  pinMode(buttonPin, INPUT_PULLUP);
  pinMode(actionButtonPin, INPUT_PULLUP);
  pinMode(ledPin, OUTPUT);
  digitalWrite(ledPin, LOW); 

  if (!mpu.begin()) {
    Serial.println("Failed to find MPU6050 chip");
  } else {
    mpu.setAccelerometerRange(MPU6050_RANGE_2_G);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
  }
}

void loop() {
  int buttonState = digitalRead(buttonPin);
  if (buttonState == LOW && lastButtonState == HIGH) {
    buttonPressStartTime = millis();
    longHoldHandled = false;
  }
  
  if (buttonState == LOW && !longHoldHandled) {
    unsigned long pressDuration = millis() - buttonPressStartTime;
    if (hidActive && pressDuration >= disarmHoldMs) {
      hidActive = false;
      longHoldHandled = true;
      digitalWrite(ledPin, LOW); 
      Serial.println("HID Disarmed");
    } 
    else if (!hidActive && pressDuration >= armHoldMs) {
      hidActive = true;
      longHoldHandled = true;
      digitalWrite(ledPin, HIGH); 
      Serial.println("HID Armed");
    }
  }
  lastButtonState = buttonState;

  if (!hidActive) {
    if (!wasDisarmed) {
      sendNeutralState(); 
      wasDisarmed = true; 
    }
    delay(10); 
    return;
  }
  wasDisarmed = false;

  sensors_event_t a, g, temp;
  if (!mpu.getEvent(&a, &g, &temp)) {
     delay(10);
     return; 
  }

  int potRaw = readSmoothPotRaw(potPin);
  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  if (invertRoll) roll = -roll;

  pitch = applyAngleDeadzone(pitch, pitchDeadzone);
  roll = applyAngleDeadzone(roll, rollDeadzone);

  int actionButtonPressed = (digitalRead(actionButtonPin) == LOW) ? 1 : 0;

  // Spatula mapped to 0-255
  uint8_t mappedPitch = convertAngleToLeftStickRange(pitch);
  uint8_t mappedRoll = convertAngleToLeftStickRange(roll);
  
  // Pot mapped to -127 to 127
  int8_t mappedPot = convertPotToRightStickRange(potRaw);

  // Send HID Commands
  Gamepad.leftStick((int8_t)mappedRoll, (int8_t)mappedPitch);
  Gamepad.rightStick(hidRightCenter, mappedPot);

  if (actionButtonPressed == 1) {
    Gamepad.pressButton(1);
  } else {
    Gamepad.releaseButton(1);
  }
}

void sendNeutralState() {
  Gamepad.leftStick((int8_t)hidLeftCenter, (int8_t)hidLeftCenter);
  Gamepad.rightStick(hidRightCenter, hidRightMin); // Pulls pot down to 0% flame when disarmed
  Gamepad.releaseButton(1); 
}

float calculatePitch(float ax, float ay, float az) {
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0 / PI;
}

float calculateRoll(float ax, float ay, float az) {
  return atan2(-ax, az) * 180.0 / PI;
}

int readSmoothPotRaw(int pin) {
  long total = 0;
  for (int i = 0; i < 5; i++) {
    total += analogRead(pin);
  }
  return total / 5;
}

float applyAngleDeadzone(float angle, float deadzone) {
  if (abs(angle) < deadzone) return 0;
  if (angle > 0) return angle - deadzone;
  return angle + deadzone;
}

uint8_t convertAngleToLeftStickRange(float angleDegrees) {
  long mapped = map(constrain(angleDegrees, -90, 90), -90, 90, hidLeftMin, hidLeftMax);
  return (uint8_t)constrain(mapped, hidLeftMin, hidLeftMax);
}

int8_t convertPotToRightStickRange(int potRaw) {
  long mapped = map(constrain(potRaw, 0, 4095), 0, 4095, hidRightMin, hidRightMax);
  return (int8_t)constrain(mapped, hidRightMin, hidRightMax);
}