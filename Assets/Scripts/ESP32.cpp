#include <Arduino.h>

void setup() {
    Serial.begin(115200); // Required to initialize serial communication
    Serial.setTimeout(20);
    pinMode(2, OUTPUT);
}

void loop() {
    if (Serial.available()) {

        String command = Serial.readString();
        command.trim(); // Removes hidden \r or \n newline characters

        if (command == "Lift") {
            Serial.println("Activated");
            digitalWrite(2, HIGH);
        } else if (command == "Release"){
            Serial.println("Released");
            digitalWrite(2, LOW);
        }

    }
}
