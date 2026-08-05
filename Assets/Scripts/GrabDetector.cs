// Sits on each grabbable object and reports Lift/Release events to the
// ESP32 (via the shared Esp32Bridge). Which hand did the grabbing is
// decided by proximity of the grab-point pose to the OVR hand anchors,
// because Meta's PointableElement doesn't expose per-hand identity.
//
// This version is EVENT-DRIVEN instead of the old per-frame polling loop:
// we subscribe to `Grabbable.WhenPointerEventRaised` and only do work on
// Select / Unselect. Four scripts polling every Update was measurable on
// Quest 3, and the polling also meant a hand that grabbed and let go
// inside a single frame could be missed — the event API doesn't drop it.

using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class GrabDetector : MonoBehaviour
{
<<<<<<< Updated upstream
    [Header("Block Settings")]
    public int blockWeight = 0;

=======
    [Header("Optional overrides")]
    [Tooltip("Leave blank to auto-find. Use it if you need to point at a specific slider.")]
    public PlateFillPercent weightSource;
>>>>>>> Stashed changes

    private Grabbable _grabbable;
    private Transform _leftAnchor;
    private Transform _rightAnchor;

    // pointer id -> hand string that owns the current select, so we can
    // emit exactly one Release per Select even if hover events come between.
    private readonly Dictionary<int, string> _activeGrabs = new Dictionary<int, string>();

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
    }

    void Start()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            _leftAnchor = rig.leftHandAnchor;
            _rightAnchor = rig.rightHandAnchor;
        }

        if (weightSource == null) weightSource = FindAnyObjectByType<PlateFillPercent>();
    }

    void OnEnable()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    void OnDisable()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        // We only care about the moment a grab starts and the moment it ends.
        // Hover / move / cancel don't drive EMS.
        switch (evt.Type)
        {
            case PointerEventType.Select:
            {
                string hand = ClosestHand(evt.Pose.position);
                _activeGrabs[evt.Identifier] = hand;
                int weight = weightSource != null
                    ? Mathf.RoundToInt(weightSource.percent)
                    : 0;
                var payload = $"Lift,{weight},{hand}";
                Esp32Bridge.Send(payload);
                Debug.Log($"[GrabDetector:{name}] {payload}");
                break;
            }

            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
            {
                if (_activeGrabs.TryGetValue(evt.Identifier, out string hand))
                {
                    _activeGrabs.Remove(evt.Identifier);
                    var payload = $"Release,0,{hand}";
                    Esp32Bridge.Send(payload);
                    Debug.Log($"[GrabDetector:{name}] {payload}");
                }
                break;
            }
        }
    }

    private string ClosestHand(Vector3 point)
    {
<<<<<<< Updated upstream
        Debug.Log(blockWeight);
        SendLiftCommand();
    }

    void OnReleased()
    {
        Debug.Log(blockWeight);
        SendReleaseCommand();
    }

    void OnApplicationQuit() {
        if (esp.IsOpen) {
            esp.Close();
        }
=======
        if (_leftAnchor == null || _rightAnchor == null) return "Left";
        float l = (point - _leftAnchor.position).sqrMagnitude;
        float r = (point - _rightAnchor.position).sqrMagnitude;
        return l <= r ? "Left" : "Right";
>>>>>>> Stashed changes
    }
}
