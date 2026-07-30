using UnityEngine;
using Oculus.Interaction;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif


public class CubeGrabDetector : MonoBehaviour
{
    [Header("Block Settings")]
    public int blockWeight = 0;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    SerialPort esp = new SerialPort("COM4", 115200);
#endif
    private Grabbable grabbable;
    private bool isGrabbed = false;

    void Start()
    {
        grabbable = GetComponent<Grabbable>();

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        try
        {
            esp.Open();
            esp.ReadTimeout = 100;
            Debug.Log("ESP32 Connected!");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ESP32 not connected (COM4 unavailable): {e.Message}");
        }
#endif
    }

    public void SendLiftCommand() {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (esp.IsOpen) {
            esp.WriteLine("Lift");
            Debug.Log("Sent Lift");
        }
#endif
    }

    public void SendReleaseCommand()
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (esp.IsOpen)
        {
            esp.WriteLine("Release");
            Debug.Log("Sent Release");
        }
#endif
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
        Debug.Log(blockWeight);
        SendLiftCommand();
    }

    void OnReleased()
    {
        Debug.Log(blockWeight);
        SendReleaseCommand();
    }

    void OnApplicationQuit() {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (esp.IsOpen) {
            esp.Close();
        }
#endif
    }
}
