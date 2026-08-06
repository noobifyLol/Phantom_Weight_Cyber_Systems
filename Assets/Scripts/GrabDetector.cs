using UnityEngine;
using System.Collections.Generic;
using System.IO.Ports; // Added to resolve SerialPort namespace
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class GrabDetector : MonoBehaviour
{
    [Header("Optional overrides")]
    [Tooltip("Leave blank to auto-find.")]
    public PlateFillPercent plateFillPercent;
    [Header("Block Settings")]
    public string defaultPort = "COM4";

    [Header("Rig Reference")]
    public OVRCameraRig rig;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    private SerialPort esp;
#endif

    private Grabbable grabbable;

    private Transform leftHandAnchor;
    private Transform rightHandAnchor;

    // Tracks which hand is holding each pointer ID.
    private readonly Dictionary<int, string> activeGrabs = new Dictionary<int, string>();

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();

        // 1. Only auto-find if not assigned via Inspector
        if (plateFillPercent == null)
            plateFillPercent = GetComponent<PlateFillPercent>();

        if (plateFillPercent == null)
            plateFillPercent = FindAnyObjectByType<PlateFillPercent>();

        // 2. Resolve Rig early in Awake instead of Start
        if (rig == null)
            rig = FindAnyObjectByType<OVRCameraRig>();

        if (rig != null)
        {
            leftHandAnchor = rig.leftHandAnchor;
            rightHandAnchor = rig.rightHandAnchor;
        }
    }

    void OnEnable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    void OnDisable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
            {
                string hand = ClosestHand(evt.Pose.position);
                activeGrabs[evt.Identifier] = hand;

                int weight = plateFillPercent != null
                    ? Mathf.RoundToInt(plateFillPercent.percent)
                    : 0;

                string payload = $"Lift,{weight},{hand}";
                Esp32Bridge.Send(payload);

                Debug.Log($"[GrabDetector:{name}] {payload}");
                break;
            }

            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
            {
                if (activeGrabs.TryGetValue(evt.Identifier, out string hand))
                {
                    activeGrabs.Remove(evt.Identifier);

                    int weight = plateFillPercent != null
                    ? Mathf.RoundToInt(plateFillPercent.percent)
                    : 0;

                    string payload = $"Release,{weight},{hand}";
                    Esp32Bridge.Send(payload);

                    Debug.Log($"[GrabDetector:{name}] {payload}");
                }

                break;
            }
        }
    }

    private string ClosestHand(Vector3 point)
    {
        if (leftHandAnchor == null || rightHandAnchor == null)
            return "Left";

        float leftDistance = (point - leftHandAnchor.position).sqrMagnitude;
        float rightDistance = (point - rightHandAnchor.position).sqrMagnitude;

        return leftDistance <= rightDistance ? "Left" : "Right";
    }
}