#include <Arduino.h>

void setup() {
    // Initialize the physical USB Serial hardware line
    Serial.begin(115200);
    Serial.setTimeout(10);

    pinMode(2, OUTPUT); // Right arm pulse UP
    pinMode(5, OUTPUT); // Right arm pulse DOWN
    pinMode(4, OUTPUT); // Left arm tracking

    Serial.println("Wired ESP32 Connection Ready!");
}

void loop() {
    // Listen directly to the physical USB cable data stream
    if (Serial.available()) {

        String input = Serial.readStringUntil('\n');
        input.replace(" ", "");

        // --- Parsing Schema: "Lift,30,Left" ---
        int commandIndex = input.indexOf(",");
        if (commandIndex == -1) return;

        String command = input.substring(0, commandIndex);
        command.toLowerCase();

        String remainder = input.substring(commandIndex + 1);
        int weightIndex = remainder.indexOf(",");
        if (weightIndex == -1) return;

        // Extract weight and cast to integer for loop counts
        int weight = remainder.substring(0, weightIndex).toInt();

        String hand = remainder.substring(weightIndex + 1);
        hand.trim();

        // --- Logic Execution ---

        // Channel 1: Left hand tracking
        if (hand == "Left") {
            if (command == "lift") {
                digitalWrite(4, HIGH);
            } else if (command == "release") {
                digitalWrite(4, LOW);
            }
        }

        // Channel 2: Right hand tracking
        else if (hand == "Right") {
            if (command == "lift") {
                // Pulse Pin 2 UP based on weight value
                for (int i = 0; i < weight; i++) {
                    digitalWrite(2, HIGH);
                    delay(67);
                    digitalWrite(2, LOW);
                    delay(67);
                }
            }
            else if (command == "release") {
                // Unity's GrabDetector.cs now sends back the weight this hand
                // was actually lifted with (see _activeGrabs there), not a
                // fixed 0, so this loop runs and pulses back down to match.
                for (int i = weight; i > 0; i--) {
                    digitalWrite(5, HIGH);
                    delay(67);
                    digitalWrite(5, LOW);
                    delay(67);
                }
            }
        }
    }
}
