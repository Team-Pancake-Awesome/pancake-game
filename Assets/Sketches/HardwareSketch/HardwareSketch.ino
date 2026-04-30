#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

// Change to true for Bluetooth, false for USB
#define USE_BLUETOOTH true

#if USE_BLUETOOTH
  #include <BleGamepad.h>
  BleGamepad Gamepad("Spatula");
#else
  #include "USB.h"
  #include "USBHIDGamepad.h"
  USBHIDGamepad Gamepad;
#endif

Adafruit_MPU6050 mpu;

// Rotary Encoder Pins
const int clkPin = 10;  // Interrupt pin
const int dtPin = 9;

// Buttons
const int buttonPin = 8;        // Encoder SW pin: arm/disarm
const int actionButtonPin = 6;  // Thumb flipper button
const int ledPin = LED_BUILTIN;

// Encoder Virtual Potentiometer
volatile int virtualPot = 0;
int encoderStep = 250; // Higher/lower changes how fast the flame turns up
volatile unsigned long lastInterruptTime = 0;

// Tuning Variables
float pitchOffset = 0.0f;
float rollOffset = 0.0f;
bool invertRoll = false;
float pitchDeadzone = 4.0f;
float rollDeadzone = 4.0f;
bool enableDebugSerial = true;

// State Variables
bool hidActive = false;
bool wasDisarmed = false;
bool lastButtonState = HIGH;
unsigned long buttonPressStartTime = 0;
bool longHoldHandled = false;

const unsigned long armHoldMs = 500;
const unsigned long disarmHoldMs = 1500;

// Left Stick expects unsigned 0-255
const uint8_t hidLeftCenter = 127;
const uint8_t hidLeftMin = 0;
const uint8_t hidLeftMax = 255;

// Right Stick expects signed -127 to 127
const int8_t hidRightCenter = 0;
const int8_t hidRightMin = -127;
const int8_t hidRightMax = 127;

// Function declarations
void updateEncoder();
void sendNeutralState();
bool calibrateNeutralPose(int sampleCount = 80);
float calculatePitch(float ax, float ay, float az);
float calculateRoll(float ax, float ay, float az);
float applyAngleDeadzone(float angle, float deadzone);
uint8_t convertAngleToLeftStickRange(float angleDegrees);
int8_t convertPotToRightStickRange(int potRaw);

void updateEncoder() {
  unsigned long interruptTime = millis();

  // Mechanical bounce guard. Ignore interrupts faster than 5ms.
  if (interruptTime - lastInterruptTime > 5) {
    if (digitalRead(dtPin) != digitalRead(clkPin)) {
      virtualPot += encoderStep;
    } else {
      virtualPot -= encoderStep;
    }

    virtualPot = constrain(virtualPot, 0, 4095);
  }

  lastInterruptTime = interruptTime;
}

void setup() {
  Serial.begin(115200);
  delay(1500);

  Serial.println();
  Serial.println("BOOTING HARDWARE SKETCH");
  Serial.println("Before Wire / USB setup");

  Wire.begin();

  #if USE_BLUETOOTH
    BleGamepadConfiguration bleConfig;

    bleConfig.setButtonCount(1);
    bleConfig.setHatSwitchCount(0);

    // Turn off unused inputs.
    bleConfig.setIncludeRxAxis(false);
    bleConfig.setIncludeRyAxis(false);
    bleConfig.setIncludeSlider1(false);
    bleConfig.setIncludeSlider2(false);

    // X/Y/Z/Rz remain enabled for left stick and right stick.
    Gamepad.begin(&bleConfig);
    Serial.println("Starting in BLUETOOTH Mode...");
  #else
    Gamepad.begin();
    USB.begin();
    Serial.println("Starting in USB Mode...");
  #endif

  pinMode(clkPin, INPUT_PULLUP);
  pinMode(dtPin, INPUT_PULLUP);
  pinMode(buttonPin, INPUT_PULLUP);
  pinMode(actionButtonPin, INPUT_PULLUP);
  pinMode(ledPin, OUTPUT);
  digitalWrite(ledPin, LOW);

  attachInterrupt(digitalPinToInterrupt(clkPin), updateEncoder, FALLING);

  if (!mpu.begin()) {
    Serial.println("Failed to find MPU6050 chip");
  } else {
    mpu.setAccelerometerRange(MPU6050_RANGE_2_G);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
    Serial.println("MPU6050 ready");
  }

  sendNeutralState();
  wasDisarmed = true;
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
      longHoldHandled = true;

      digitalWrite(ledPin, LOW);

      bool calibrated = calibrateNeutralPose();

      if (calibrated) {
        hidActive = true;
        wasDisarmed = false;
        digitalWrite(ledPin, HIGH);
        Serial.println("HID Armed");
      } else {
        hidActive = false;
        digitalWrite(ledPin, LOW);
        Serial.println("HID stayed disarmed because calibration failed.");
      }
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

  int potRaw = virtualPot;

  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  if (invertRoll) {
    roll = -roll;
  }

  pitch = applyAngleDeadzone(pitch, pitchDeadzone);
  roll = applyAngleDeadzone(roll, rollDeadzone);

  int actionButtonPressed = digitalRead(actionButtonPin) == LOW ? 1 : 0;

  uint8_t mappedPitch = convertAngleToLeftStickRange(pitch);
  uint8_t mappedRoll = convertAngleToLeftStickRange(roll);
  int8_t mappedPot = convertPotToRightStickRange(potRaw);

  #if USE_BLUETOOTH
    int16_t bleRoll = map(mappedRoll, 0, 255, 0, 32767);
    int16_t blePitch = map(mappedPitch, 0, 255, 0, 32767);
    int16_t blePot = map(mappedPot, -127, 127, 0, 32767);

    Gamepad.setLeftThumb(bleRoll, blePitch);
    Gamepad.setRightThumb(16384, blePot);

    if (actionButtonPressed == 1) {
      Gamepad.press(BUTTON_1);
    } else {
      Gamepad.release(BUTTON_1);
    }
  #else
    Gamepad.leftStick((int8_t)mappedRoll, (int8_t)mappedPitch);
    Gamepad.rightStick(hidRightCenter, mappedPot);

    if (actionButtonPressed == 1) {
      Gamepad.pressButton(1);
    } else {
      Gamepad.releaseButton(1);
    }
  #endif

  if (enableDebugSerial) {
    Serial.print("armed=");
    Serial.print(hidActive);
    Serial.print(" pitch=");
    Serial.print(pitch);
    Serial.print(" roll=");
    Serial.print(roll);
    Serial.print(" pot=");
    Serial.print(potRaw);
    Serial.print(" action=");
    Serial.println(actionButtonPressed);
  }

  delay(10);
}

void sendNeutralState() {
  #if USE_BLUETOOTH
    Gamepad.setLeftThumb(16384, 16384);
    Gamepad.setRightThumb(16384, 0);
    Gamepad.release(BUTTON_1);
  #else
    Gamepad.leftStick((int8_t)hidLeftCenter, (int8_t)hidLeftCenter);
    Gamepad.rightStick(hidRightCenter, hidRightMin);
    Gamepad.releaseButton(1);
  #endif
}

bool calibrateNeutralPose(int sampleCount) {
  float pitchSum = 0.0f;
  float rollSum = 0.0f;
  int validSamples = 0;

  Serial.println("Calibrating neutral pose... hold spatula still.");

  for (int i = 0; i < sampleCount; i++) {
    sensors_event_t a, g, temp;

    if (mpu.getEvent(&a, &g, &temp)) {
      float rawPitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z);
      float rawRoll = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z);

      pitchSum += rawPitch;
      rollSum += rawRoll;
      validSamples++;
    }

    delay(5);
  }

  if (validSamples <= 0) {
    Serial.println("Calibration failed: no valid MPU samples.");
    return false;
  }

  pitchOffset = pitchSum / validSamples;
  rollOffset = rollSum / validSamples;

  Serial.print("Calibration complete. pitchOffset=");
  Serial.print(pitchOffset);
  Serial.print(" rollOffset=");
  Serial.println(rollOffset);

  return true;
}

float calculatePitch(float ax, float ay, float az) {
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0f / PI;
}

float calculateRoll(float ax, float ay, float az) {
  return atan2(-ax, az) * 180.0f / PI;
}

float applyAngleDeadzone(float angle, float deadzone) {
  if (abs(angle) < deadzone) {
    return 0.0f;
  }

  if (angle > 0.0f) {
    return angle - deadzone;
  }

  return angle + deadzone;
}

uint8_t convertAngleToLeftStickRange(float angleDegrees) {
  long mapped = map(constrain(angleDegrees, -90.0f, 90.0f), -90, 90, hidLeftMin, hidLeftMax);
  return (uint8_t)constrain(mapped, hidLeftMin, hidLeftMax);
}

int8_t convertPotToRightStickRange(int potRaw) {
  long mapped = map(constrain(potRaw, 0, 4095), 0, 4095, hidRightMin, hidRightMax);
  return (int8_t)constrain(mapped, hidRightMin, hidRightMax);
}