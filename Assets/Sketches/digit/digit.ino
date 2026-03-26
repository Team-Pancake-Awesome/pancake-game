int seven = 7;
int six = 6;
int five = 5;
int eleven = 11;
int ten = 10;
int eight = 8;
int nine = 9;
int four = 4;
//display number 1
void display1(void) {

  digitalWrite(five, HIGH);
  digitalWrite(six, HIGH);
}
//display number2  5 and 8 off
void display2(void) {
  digitalWrite(eleven, HIGH);
  digitalWrite(ten, HIGH);

  digitalWrite(four, HIGH);
  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);
}

// display number3
void display3(void) {
  digitalWrite(ten, HIGH);

  digitalWrite(eleven, HIGH);

  digitalWrite(four, HIGH);
  digitalWrite(eight, HIGH);

  digitalWrite(seven, HIGH);
}
// display number4
void display4(void) {

  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);
  digitalWrite(eleven, HIGH);

  digitalWrite(eight, HIGH);
}
// display number5
void display5(void)

{
  digitalWrite(ten, HIGH);
  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);
  
  digitalWrite(eight, HIGH);
  digitalWrite(seven, HIGH);
}
// display number6
void display6(void) {
  digitalWrite(five, HIGH);
  digitalWrite(four, HIGH);

  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);
  digitalWrite(eight, HIGH);

  digitalWrite(ten, HIGH);
}
// display number7
void display7(void)

{
  digitalWrite(ten, HIGH);
  digitalWrite(eleven, HIGH);
  digitalWrite(eight, HIGH);
}

// display number8
void display8(void) {
  digitalWrite(four, HIGH);

  digitalWrite(five, HIGH);
  digitalWrite(six, HIGH);
  digitalWrite(seven, HIGH);

  digitalWrite(eight, HIGH);
  digitalWrite(ten, HIGH);
  digitalWrite(eleven, HIGH);
}
void clearDisplay(void) {
  digitalWrite(seven, LOW);
  digitalWrite(six, LOW);

  digitalWrite(nine, LOW);
  digitalWrite(five, LOW);
  digitalWrite(eleven, LOW);

  digitalWrite(ten, LOW);
  digitalWrite(eight, LOW);
  digitalWrite(four, LOW);
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
void display0(void) {
  digitalWrite(five, HIGH);
  digitalWrite(six, HIGH);

  digitalWrite(seven, HIGH);
  digitalWrite(eight, HIGH);
  digitalWrite(eleven, HIGH);

  digitalWrite(ten, HIGH);
}
void setup() {
  int i;
  for (i = 4; i <= 11; i++)

    pinMode(i, OUTPUT);
}
void loop() {
  while (1)

  {
    clearDisplay();
    display0();
    delay(2000);
    clearDisplay();

    display1();
    delay(2000);
    clearDisplay();
    display2();

    delay(2000);
    clearDisplay();
    display3();

    delay(2000);
    clearDisplay();
    display4();
    delay(2000);

    clearDisplay();
    display5();
    delay(2000);
    clearDisplay();

    display6();
    delay(2000);
    clearDisplay();

    display7();
    delay(2000);
    clearDisplay();
    display8();

    delay(2000);
    clearDisplay();
    display9();

    delay(2000);
  }
}