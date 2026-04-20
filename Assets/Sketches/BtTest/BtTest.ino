#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

// Change to 'true' for Bluetooth, 'false' for USB
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




//  Rotary Encoder Pins 
const int clkPin = 7;  // Interrupt pin
const int dtPin = 8;   

//  Buttons
const int buttonPin = 10;       // Encoder SW pin (Now acts as Arm/Disarm)
const int actionButtonPin = 2;  // Thumb flipper button
const int ledPin = LED_BUILTIN; 

// ENCODER VIRTUAL POTENTIOMETER 

volatile int virtualPot = 0; 
int encoderStep = 250; // Adjust higher/lower to change how fast the flame turns up
volatile unsigned long lastInterruptTime = 0;


// Tuning Variables
float pitchOffset = 0.0f;
float rollOffset = 0.0f;
bool invertRoll = false;
float pitchDeadzone = 4.0f;
float rollDeadzone = 4.0f;
bool enableDebugSerial = true;

//State Variables
bool hidActive = false;
bool wasDisarmed = false;
bool lastButtonState = HIGH;
unsigned long buttonPressStartTime = 0;
bool longHoldHandled = false;

const unsigned long armHoldMs = 250;
const unsigned long disarmHoldMs = 1500;


// Left Stick (Spatula X/Y) expects Unsigned 0-255
const uint8_t hidLeftCenter = 127;
const uint8_t hidLeftMin = 0;
const uint8_t hidLeftMax = 255;
// Right Stick (Pot Z/RZ) expects Signed -127 to 127
const int8_t hidRightCenter = 0;
const int8_t hidRightMin = -127;
const int8_t hidRightMax = 127;


// Interrupt
void updateEncoder() {
  unsigned long interruptTime = millis();
  
  //  mechanical bounce. Ignore it interrupts faster than 5 milliseconds,
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
  Wire.begin();

  #if USE_BLUETOOTH
    //  Create a custom configuration
    BleGamepadConfiguration bleConfig;

    // set inputs
    bleConfig.setButtonCount(1); // We only have 1 button
    bleConfig.setHatSwitchCount(0); // No D-Pads
    
    // Turn off unused 
    bleConfig.setIncludeRxAxis(false);
    bleConfig.setIncludeRyAxis(false);
    bleConfig.setIncludeSlider1(false);
    bleConfig.setIncludeSlider2(false);
    // (Note: We leave X, Y, Z, and Rz as 'true' because Left/Right stick use those!)

    // Start the gamepad 
    Gamepad.begin(&bleConfig);
    Serial.println("Starting in BLUETOOTH Mode...");
  #else
    Gamepad.begin();
    USB.begin();
    Serial.println("Starting in USB Mode...");
  #endif

  // Initialize Pins 
  pinMode(clkPin, INPUT_PULLUP);
  pinMode(dtPin, INPUT_PULLUP);
  pinMode(buttonPin, INPUT_PULLUP);
  pinMode(actionButtonPin, INPUT_PULLUP);
  pinMode(ledPin, OUTPUT);
  digitalWrite(ledPin, LOW); 

  // attach rncoder interrupt to pin
  attachInterrupt(digitalPinToInterrupt(clkPin), updateEncoder, FALLING);

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

  // Read the simulated potentiometer value
  int potRaw = virtualPot; 
  
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
  #if USE_BLUETOOTH
    // Spatula is 0 to 255. Map it to 0 to 32767.
    int16_t bleRoll = map(mappedRoll, 0, 255, 0, 32767);
    int16_t blePitch = map(mappedPitch, 0, 255, 0, 32767);
    
    // Pot is -127 to 127. Map it to 0 to 32767.
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
}

void sendNeutralState() {
  #if USE_BLUETOOTH
    Gamepad.setLeftThumb(16384, 16384);
    Gamepad.setRightThumb(16384, 0); // 0 is minimum pot value for BLE
    Gamepad.release(BUTTON_1);
  #else
    Gamepad.leftStick((int8_t)hidLeftCenter, (int8_t)hidLeftCenter);
    Gamepad.rightStick(hidRightCenter, hidRightMin); // Pulls pot down to 0% flame when disarmed
    Gamepad.releaseButton(1); 
  #endif
}

float calculatePitch(float ax, float ay, float az) {
  return atan2(ay, sqrt(ax * ax + az * az)) * 180.0 / PI;
}

float calculateRoll(float ax, float ay, float az) {
  return atan2(-ax, az) * 180.0 / PI;
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