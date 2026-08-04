using UnityEngine;
using Oculus.Interaction;
using System.Collections.Generic;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

public class GrabDetector : MonoBehaviour
{
    [Header("Block Settings")]
    public string defaultPort = "COM4";

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    private SerialPort esp;
#endif

    private Grabbable grabbable;
    private OVRCameraRig rig;
    private PlateFillPercent plateFillPercent;

    private Transform leftHandAnchor;
    private Transform rightHandAnchor;

    // Track active grabbing hands independently for multi-hand grabs
    private HashSet<string> activeGrabbingHands = new HashSet<string>();

    void Start()
    {
        grabbable = GetComponent<Grabbable>();

        rig = FindAnyObjectByType<OVRCameraRig>();
        plateFillPercent = FindAnyObjectByType<PlateFillPercent>();

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

        // Detect all hands currently grabbing
        foreach (Pose point in grabbable.SelectingPoints)
        {
            string hand = GetHandFromPose(point);

            if (!string.IsNullOrEmpty(hand))
            {
                currentFrameHands.Add(hand);
            }
        }

        // Send Lift command for newly grabbing hands
        foreach (string hand in currentFrameHands)
        {
            if (!activeGrabbingHands.Contains(hand))
            {
                activeGrabbingHands.Add(hand);
                SendLiftCommand(hand);
            }
        }

        // Send Release command for hands that let go
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

    private string GetHandFromPose(Pose point)
    {
        if (leftHandAnchor == null || rightHandAnchor == null)
        {
            return "Left"; // Fallback
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
            int weight = 0;

            if (plateFillPercent != null)
            {
                weight = Mathf.RoundToInt(plateFillPercent.percent);
            }

            // ESP32 format: Command,Weight,Hand
            string payload = $"Lift,{weight},{hand}";

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
            // ESP32 format: Command,Weight,Hand
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