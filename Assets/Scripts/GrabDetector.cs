using UnityEngine;
using Oculus.Interaction;
using System.IO.Ports;


public class CubeGrabDetector : MonoBehaviour
{

    SerialPort esp = new SerialPort("COM4", 115200);
    private Grabbable grabbable;
    private bool isGrabbed = false;

    void Start()
    {
        grabbable = GetComponent<Grabbable>();

        esp.Open();
        esp.ReadTimeout = 100;
        Debug.Log("ESP32 Connected!");
    }

    public void SendLiftCommand() {
        if (esp.IsOpen) {
            esp.WriteLine("Lift");
            Debug.Log("Sent Lift");
        }
    }

    public void SendReleaseCommand()
    {
        if (esp.IsOpen)
        {
            esp.WriteLine("Release");
            Debug.Log("Sent Release");
        }
    }

    void Update()
    {
        if (grabbable != null)
        {
            // Check if the pointable has any active selecting interactors (is grabbed)
            bool currentlyGrabbed = grabbable.SelectingPointsCount > 0;

            if (currentlyGrabbed && !isGrabbed)
            {
                isGrabbed = true;
                OnGrabbed();
            }
            else if (!currentlyGrabbed && isGrabbed)
            {
                isGrabbed = false;
                OnReleased();
            }
        }
    }

    void OnGrabbed()
    {
        Debug.Log("Cube has been grabbed!");
        SendLiftCommand();
    }

    void OnReleased()
    {
        Debug.Log("Cube has been released!");
        SendReleaseCommand();
    }

    void OnApplicationQuit() {
        if (esp.IsOpen) {
            esp.Close();
        }
    }
}
