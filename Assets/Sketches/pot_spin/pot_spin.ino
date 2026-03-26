int potPin = A3;

int readSmoothPot(int pin) {
  long total = 0;
  for (int i = 0; i < 8; i++) {
    total += analogRead(pin);
    delay(1);
  }
  return total / 8;
}

void setup() {
  Serial.begin(9600);
}

void loop() {
  int rawPot = readSmoothPot(potPin);

  // flip this if your knob direction is backwards
  //rawPot = 1023 - rawPot;

  Serial.println(rawPot);
  delay(10);
}