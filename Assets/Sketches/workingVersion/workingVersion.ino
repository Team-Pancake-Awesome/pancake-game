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
const int ledPin = LED_BUILTIN; // Uses the onboard yellow/orange LED

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

// --- Button Timing ---
const unsigned long armHoldMs = 250;
const unsigned long disarmHoldMs = 1500;

// --- 8-bit HID center/range ---
const uint8_t hidCenter = 127;
const uint8_t hidMin = 0;
const uint8_t hidMax = 255;

// ==========================================
// SETUP
// ==========================================
void setup() {
  Serial.begin(115200);

  // Initialize I2C for the gyro
  Wire.begin(); 

  Gamepad.begin();
  USB.begin();

  pinMode(potPin, INPUT);
  pinMode(buttonPin, INPUT_PULLUP);
  pinMode(actionButtonPin, INPUT_PULLUP);
  
  // Setup LED and ensure it matches initial state (Disarmed = OFF)
  pinMode(ledPin, OUTPUT);
  digitalWrite(ledPin, LOW); 

  if (!mpu.begin()) {
    Serial.println("Failed to find MPU6050 chip");
  } else {
    mpu.setAccelerometerRange(MPU6050_RANGE_2_G);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
  }
}

// ==========================================
// MAIN LOOP
// ==========================================
void loop() {
  // --- 1. Arm / Disarm Button Logic ---
  int buttonState = digitalRead(buttonPin);
  if (buttonState == LOW && lastButtonState == HIGH) {
    buttonPressStartTime = millis();
    longHoldHandled = false;
  }
  
  if (buttonState == LOW && !longHoldHandled) {
    unsigned long pressDuration = millis() - buttonPressStartTime;
    
    // Disarm (Long Press)
    if (hidActive && pressDuration >= disarmHoldMs) {
      hidActive = false;
      longHoldHandled = true;
      digitalWrite(ledPin, LOW); // LED OFF
      Serial.println("HID Disarmed");
    } 
    // Arm (Short Press)
    else if (!hidActive && pressDuration >= armHoldMs) {
      hidActive = true;
      longHoldHandled = true;
      digitalWrite(ledPin, HIGH); // LED ON
      Serial.println("HID Armed");
    }
  }
  lastButtonState = buttonState;

  // --- 2. The Disarm Spam Fix ---
  if (!hidActive) {
    if (!wasDisarmed) {
      sendNeutralState(); // Send center command exactly ONE time
      wasDisarmed = true; 
    }
    delay(10); // CRITICAL: Keeps USB driver alive!
    return;
  }
  wasDisarmed = false;

  // --- 3. The Garbage Memory Fix ---
  sensors_event_t a, g, temp;
  if (!mpu.getEvent(&a, &g, &temp)) {
     Serial.println("MPU read failed! Waiting for recovery...");
     delay(10);
     return; // Abort loop to prevent garbage math from causing right-drift
  }

  // --- 4. Read & Map Data ---
  int potRaw = readSmoothPotRaw(potPin);
  float pitch = calculatePitch(a.acceleration.x, a.acceleration.y, a.acceleration.z) - pitchOffset;
  float roll  = calculateRoll(a.acceleration.x, a.acceleration.y, a.acceleration.z) - rollOffset;

  if (invertRoll) roll = -roll;

  pitch = applyAngleDeadzone(pitch, pitchDeadzone);
  roll = applyAngleDeadzone(roll, rollDeadzone);

  int actionButtonPressed = (digitalRead(actionButtonPin) == LOW) ? 1 : 0;

  uint8_t mappedPitch = convertAngleToHIDRange(pitch);
  uint8_t mappedRoll = convertAngleToHIDRange(roll);
  uint8_t mappedPot = convertPotToHIDRange(potRaw);

  // Send HID Commands
  Gamepad.leftStick((int8_t)mappedRoll, (int8_t)mappedPitch);
  
  // FIX: Anchor the right stick to center, and map the Pot to the Trigger!
  Gamepad.rightStick((int8_t)hidCenter, (int8_t)hidCenter);
  Gamepad.leftTrigger((int8_t)mappedPot);

  if (actionButtonPressed == 1) {
    Gamepad.pressButton(1);
  } else {
    Gamepad.releaseButton(1);
  }

  // --- 5. Debug ---
  if (enableDebugSerial) {
    static unsigned long lastDebugTime = 0;
    if (millis() - lastDebugTime >= 250) {
      lastDebugTime = millis();
      Serial.print("pitch: "); Serial.print(pitch, 2);
      Serial.print(" | roll: "); Serial.print(roll, 2);
      Serial.print(" | pot: "); Serial.println(potRaw);
    }
  }
}

// ==========================================
// HELPER FUNCTIONS
// ==========================================

void sendNeutralState() {
  Gamepad.leftStick(hidCenter, hidCenter);
  Gamepad.rightStick(hidCenter, hidCenter);
  Gamepad.leftTrigger(0); 
  Gamepad.releaseButton(1); 
}

float calculatePitch(float ax, float ay, float az) {
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0 / PI;
}

float calculateRoll(float ax, float ay, float az) {
  return atan2(-ax, az) * 180.0 / PI;
}

int readSmoothPotRaw(int pin) {
  // Simple averaging for smoother potentiometer reads
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

uint8_t convertAngleToHIDRange(float angleDegrees) {
  long mapped = map(constrain(angleDegrees, -90, 90), -90, 90, hidMin, hidMax);
  return (uint8_t)constrain(mapped, hidMin, hidMax);
}

uint8_t convertPotToHIDRange(int potRaw) {
  long mapped = map(constrain(potRaw, 0, 4095), 0, 4095, hidMin, hidMax);
  return (uint8_t)constrain(mapped, hidMin, hidMax);
}