/*
 * This program uses the ESP8266 Development Board to establish a Wi-Fi connection with a
 * computer on the same LAN network. The ESP8266 receives movement commands over the network,
 * and moves the RC car in the corresponding direction.
 */

#include <ESP8266WiFi.h>
#include <WiFiClient.h>
#include <ESP8266WebServer.h>
#include <ESP8266mDNS.h>

void handleRoot();
void handleNotFound();

// Define terminals that steering motor and driving motor are connected to
#define DRIVE_MOTOR_POWER D2 // Motor B
#define DRIVE_MOTOR_DIRECTION D4

#define STEER_MOTOR_POWER D1 // Motor A
#define STEER_MOTOR_DIRECTION D3

// drivePower sets how fast the car goes, can be set between 0 and 1023
int drivePower = 512;

// steeringPower sets how fast the car turns, can be set between 0 and 1023
int steeringPower = 512;

// Set Wi-Fi SSID and password here
const char* ssid = "INSERT_SSID_HERE";
const char* password = "INSERT_WIFI_PASSWORD_HERE";

// Create server
ESP8266WebServer server(80);

const char *webpage =
#include "motorPage.h"
;

void handleRoot() {
  server.send(200, "text/html", webpage);
}

// Handle error creating connection
void handleNotFound(){
  String message = "File Not Found\n\n";
  message += "URI: ";
  message += server.uri();
  message += "\nMethod: ";
  message += (server.method() == HTTP_GET)?"GET":"POST";
  message += "\nArguments: ";
  message += server.args();
  message += "\n";
  for (uint8_t i=0; i<server.args(); i++){
    message += " " + server.argName(i) + ": " + server.arg(i) + "\n";
  }
  server.send(404, "text/plain", message);
}

void setup(void){

  // Define motor terminals as outputs
  pinMode(DRIVE_MOTOR_POWER, OUTPUT);
  pinMode(DRIVE_MOTOR_DIRECTION, OUTPUT);
  pinMode(STEER_MOTOR_POWER, OUTPUT);
  pinMode(STEER_MOTOR_DIRECTION, OUTPUT);

  WiFi.mode(WIFI_STA);

  /* Begin transmitting data and establish Wi-Fi connection.
   * When connection is established, display success message
   * and IP address of ESP8266 for use with Kinect.
   */
  Serial.begin(115200);
  
  WiFi.begin(ssid, password);

  // Wait for connection
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("");
  Serial.print("Connected to ");
  Serial.println(ssid);
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());

  if(MDNS.begin("esp8266")) {
    Serial.println("MDNS responder started");
  }

  // Begin receiving movement commands
  server.on("/", handleRoot);

  // Move car forward if command received
  server.on("/forward", [](){
    Serial.println("forward");
    analogWrite(DRIVE_MOTOR_POWER, drivePower);
    digitalWrite(DRIVE_MOTOR_DIRECTION, HIGH);
    server.send(200, "text/plain", "forward");
  });

  // Stop car if command received
  server.on("/stopped", [](){
    Serial.println("stopped");
    analogWrite(DRIVE_MOTOR_POWER, 0);
    server.send(200, "text/plain", "stopped");
  });

  // Move car backward if command received
  server.on("/back", [](){
    Serial.println("back");
    analogWrite(DRIVE_MOTOR_POWER, drivePower);
    digitalWrite(DRIVE_MOTOR_DIRECTION, LOW);
    server.send(200, "text/plain", "back");
  });

  // Turn car right if command received
  server.on("/right", [](){
    Serial.println("right");
    analogWrite(STEER_MOTOR_POWER, steeringPower);
    digitalWrite(STEER_MOTOR_DIRECTION, LOW);
    server.send(200, "text/plain", "right");
  });

  // Turn car left if command received
  server.on("/left", [](){
    Serial.println("left");
    analogWrite(STEER_MOTOR_POWER, steeringPower);
    digitalWrite(STEER_MOTOR_DIRECTION, HIGH);
    server.send(200, "text/plain", "left");
  });

  // Re-center car direction if command received
  server.on("/center", [](){
    Serial.println("center");
    analogWrite(STEER_MOTOR_POWER, 0);
    server.send(200, "text/plain", "center");
  });

  // If connection could not be established, use error handling defined above
  server.onNotFound(handleNotFound);

  server.begin();
  Serial.println("HTTP Server Started");
}

/* Continue listening for HTTP requests from client until ESP8266 is powered off
 * or connection is lost
 */
void loop(void){
  server.handleClient();
  MDNS.update();
}
