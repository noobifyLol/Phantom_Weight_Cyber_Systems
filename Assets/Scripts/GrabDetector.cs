using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
[RequireComponent(typeof(Collider))]
public class GrabDetector : MonoBehaviour
{
    [Header("Optional overrides")]
    [Tooltip("Leave blank to auto-find.")]
    public PlateFillPercent plateFillPercent;

    [Header("Rig Reference")]
    public OVRCameraRig rig;

    [Header("Weight Based on Slider")]
    [Range(0f, 2f)]
    public float multiper = 1.0f;

    private Grabbable grabbable;
    private Collider blockCollider;

    private Transform leftHandAnchor;
    private Transform rightHandAnchor;

    // Tracks which hand is holding each pointer ID
    private readonly Dictionary<int, string> activeGrabs = new Dictionary<int, string>();

    private bool leftHeld = false;
    private bool rightHeld = false;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        blockCollider = GetComponent<Collider>();

        if (plateFillPercent == null)
            plateFillPercent = GetComponent<PlateFillPercent>();

        if (plateFillPercent == null)
            plateFillPercent = FindAnyObjectByType<PlateFillPercent>();

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
                if (blockCollider != null) blockCollider.isTrigger = true;

                string hand = ClosestHand(evt.Pose.position);
                bool isLeft = (hand == "left");
                
                activeGrabs[evt.Identifier] = hand;

                if (isLeft) leftHeld = true;
                else rightHeld = true;

                int rawWeight = plateFillPercent != null
                    ? Mathf.RoundToInt(plateFillPercent.percent * multiper)
                    : 0;
                
                int weight = Mathf.Clamp(rawWeight, 0, 100);

                // --- ROUTE THROUGH STATE MANAGER ---
                Esp32Bridge.SetGrabState(isLeft, true, weight);

                Debug.Log($"[GrabDetector:{name}] Select -> Hand: {hand}, Weight: {weight}");
                break;
            }

            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
            {
                if (blockCollider != null) blockCollider.isTrigger = false;

                if (activeGrabs.TryGetValue(evt.Identifier, out string hand))
                {
                    activeGrabs.Remove(evt.Identifier);
                    bool isLeft = (hand == "left");

                    if (isLeft) leftHeld = false;
                    else rightHeld = false;

                    // --- ROUTE THROUGH STATE MANAGER ---
                    Esp32Bridge.SetGrabState(isLeft, false, 0);

                    Debug.Log($"[GrabDetector:{name}] Unselect -> Hand: {hand}");
                }

                break;
            }
        }
    }

    private string ClosestHand(Vector3 point)
    {
        if (leftHandAnchor == null || rightHandAnchor == null)
            return "left";

        float leftDistance = (point - leftHandAnchor.position).sqrMagnitude;
        float rightDistance = (point - rightHandAnchor.position).sqrMagnitude;

        return leftDistance <= rightDistance ? "left" : "right";
    }

    // Public getters for debugging
    public bool IsLeftHeld() => leftHeld;
    public bool IsRightHeld() => rightHeld;
    public bool AreBothHeld() => leftHeld && rightHeld;
}