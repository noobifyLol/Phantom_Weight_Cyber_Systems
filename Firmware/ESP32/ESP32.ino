#include <Arduino.h>

void setup() {
    Serial.begin(115200); // Required to initialize serial communication
    Serial.setTimeout(10);
    pinMode(2, OUTPUT); // GPIO pin 1 for Channel 1

    pinMode(4, OUTPUT);
}

// prereq, unity sends back a conversion ratio and a weight for the esp32
// the schema would be command and then the weight for the serial terminal and then which hand it is, (later position
// for the object would be needed for the best experience)

// Serial.print

/* THis the serial print schema we can code a table for all of this
Serial.print("Lift"); // command Lift/Release
Serial.print("30"); // weight put into a varaible and throught a equation
Serial.println("Left"); // Which hand Left/Right
Serial.println("Position "); // implment later

*/

// So this is the final schemua
String example = "Lift,30,Left";
// - -----------------------------------------------------------------

void loop() {

    if (Serial.available()) {

        String input = Serial.readStringUntil('\n');
        input.replace(" ","");

        //lift,30,left
        int commandIndex = input.indexOf(",");
        String command = input.substring(0, commandIndex);

        input = input.substring(commandIndex + 1, input.length());
        int weightIndex = input.indexOf(",");

        double weight = input.substring(0, weightIndex).toDouble();

        input = input.substring(weightIndex + 1, input.length());

        String hand = input;
        hand.trim();

        // Channel 1 one is triceps left hand

        // Channel 2 is biceps left hand

        if (hand == "Left" && (command == "Lift" || command == "lift")){
            digitalWrite(4, HIGH);
        }
        else if (hand == "Left"){
            digitalWrite(4, LOW);
        }

        else if (hand == "Right" && (command == "Lift" || command == "lift")){
            digitalWrite(2, HIGH);
        }
        else if (hand == "Right"){
            digitalWrite(2, LOW);
        }

    }
}
