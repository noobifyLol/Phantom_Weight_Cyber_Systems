#include <Arduino.h>

// Wired (USB serial) firmware for the Phantom Weight EMS wearable.
//
// Unity's Esp32Bridge.cs sends one line per state change:
//   "Lift,<weight 0-100>,<left|right|both>"
//   "Release,0,<left|right|both>"
// Hand and command are matched case-insensitively.

const int PIN_RIGHT_UP   = 2;  // Right arm: pulse UP
const int PIN_RIGHT_DOWN = 5;  // Right arm: pulse DOWN
const int PIN_LEFT       = 4;  // Left arm: on/off
const int PULSE_MS       = 67;

// How many "up" pulses the right-arm unit is currently at. Unity sends
// Release with a weight of 0, so the firmware has to remember how far it
// pulsed up in order to pulse back down by the same amount.
int rightLevel = 0;

void pulse(int pin, int count) {
    for (int i = 0; i < count; i++) {
        digitalWrite(pin, HIGH);
        delay(PULSE_MS);
        digitalWrite(pin, LOW);
        delay(PULSE_MS);
    }
}

// Move the right arm from its current level to `target` pulses.
void setRightLevel(int target) {
    target = constrain(target, 0, 100);
    if (target > rightLevel) {
        pulse(PIN_RIGHT_UP, target - rightLevel);
    } else if (target < rightLevel) {
        pulse(PIN_RIGHT_DOWN, rightLevel - target);
    }
    rightLevel = target;
}

void setup() {
    Serial.begin(115200);
    Serial.setTimeout(10);

    pinMode(PIN_RIGHT_UP, OUTPUT);
    pinMode(PIN_RIGHT_DOWN, OUTPUT);
    pinMode(PIN_LEFT, OUTPUT);

    Serial.println("Wired ESP32 Connection Ready!");
}

void loop() {
    if (!Serial.available()) return;

    String input = Serial.readStringUntil('\n');
    input.replace(" ", "");
    input.trim();

    // --- Parsing schema: "Command,Weight,Hand" ---
    int commandIndex = input.indexOf(',');
    if (commandIndex == -1) return;

    String command = input.substring(0, commandIndex);
    command.toLowerCase();

    String remainder = input.substring(commandIndex + 1);
    int weightIndex = remainder.indexOf(',');
    if (weightIndex == -1) return;

    int weight = remainder.substring(0, weightIndex).toInt();

    String hand = remainder.substring(weightIndex + 1);
    hand.trim();
    hand.toLowerCase();

    bool left  = (hand == "left"  || hand == "both");
    bool right = (hand == "right" || hand == "both");

    if (command == "lift") {
        if (left)  digitalWrite(PIN_LEFT, HIGH);
        if (right) setRightLevel(weight);
    } else if (command == "release") {
        if (left)  digitalWrite(PIN_LEFT, LOW);
        if (right) setRightLevel(0);
    }
}
