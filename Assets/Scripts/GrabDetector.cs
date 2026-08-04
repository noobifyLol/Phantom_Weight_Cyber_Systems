using UnityEngine;
using Oculus.Interaction;
using System.Collections.Generic;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

public class CubeGrabDetector : MonoBehaviour
{
    [Header("Block Settings")]
    public double blockWeight = 30.0;
    public string defaultPort = "COM4";

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    private SerialPort esp;
#endif

    private Grabbable grabbable;
    private OVRCameraRig rig;
    private Transform leftHandAnchor;
    private Transform rightHandAnchor;

    // Track active grabbing hands independently for multi-hand grabs
    private HashSet<string> activeGrabbingHands = new HashSet<string>();

    void Start()
    {
        grabbable = GetComponent<Grabbable>();
        rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            leftHandAnchor = rig.leftHandAnchor;
            rightHandAnchor = rig.rightHandAnchor;
        }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        esp = new SerialPort(defaultPort, 115200);
        try
        {
            esp.Open();
            esp.ReadTimeout = 100;
            Debug.Log($"ESP32 Connected on {defaultPort}!");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ESP32 not connected ({defaultPort} unavailable): {e.Message}");
        }
#endif
    }

    void Update()
    {
        if (grabbable == null) return;

        HashSet<string> currentFrameHands = new HashSet<string>();

        // Each active grab point (one per selecting hand/controller) becomes a hand label
        foreach (Pose point in grabbable.SelectingPoints)
        {
            string hand = GetHandFromPose(point);
            if (!string.IsNullOrEmpty(hand))
            {
                currentFrameHands.Add(hand);
            }
        }

        // 1. Send Lift command for newly grabbed hand(s)
        foreach (string hand in currentFrameHands)
        {
            if (!activeGrabbingHands.Contains(hand))
            {
                activeGrabbingHands.Add(hand);
                SendLiftCommand(hand);
            }
        }

        // 2. Send Release command for hand(s) let go this frame
        List<string> handsToRelease = new List<string>();
        foreach (string hand in activeGrabbingHands)
        {
            if (!currentFrameHands.Contains(hand))
            {
                handsToRelease.Add(hand);
            }
        }

        foreach (string hand in handsToRelease)
        {
            activeGrabbingHands.Remove(hand);
            SendReleaseCommand(hand);
        }
    }

    /// <summary>
    /// Grabbable.SelectingPoints only gives grab-point poses, not which hand/interactor
    /// produced them. Identify the hand by whichever tracked hand anchor is closest to
    /// this grab point at the moment of the check.
    /// </summary>
    private string GetHandFromPose(Pose point)
    {
        if (leftHandAnchor == null || rightHandAnchor == null)
        {
            return "Left"; // Fallback if rig wasn't found
        }

        float leftDist = Vector3.Distance(point.position, leftHandAnchor.position);
        float rightDist = Vector3.Distance(point.position, rightHandAnchor.position);

        return leftDist <= rightDist ? "Left" : "Right";
    }

    public void SendLiftCommand(string hand)
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (esp != null && esp.IsOpen)
        {
            // Matches ESP32 parsing: Command,Weight,Hand -> "Lift,30,Left"
            string payload = $"Lift,{blockWeight},{hand}";
            esp.WriteLine(payload);
            Debug.Log($"Sent: {payload}");
        }
#endif
    }

    public void SendReleaseCommand(string hand)
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (esp != null && esp.IsOpen)
        {
            // Matches ESP32 parsing: Command,Weight,Hand -> "Release,0,Left"
            string payload = $"Release,0,{hand}";
            esp.WriteLine(payload);
            Debug.Log($"Sent: {payload}");
        }
#endif
    }

    void OnApplicationQuit()
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (esp != null && esp.IsOpen)
        {
            esp.Close();
        }
#endif
    }
}