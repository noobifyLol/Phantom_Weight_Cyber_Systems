using UnityEngine;

/// <summary>
/// Basic VR locomotion for a Meta Building Blocks camera rig.
///
///   Left thumbstick   = smooth move (relative to where the head is looking).
///   Right thumbstick  = comfort snap-turn.
///   Physical Move Gain = amplifies REAL-WORLD walking so a small step in your
///                        room covers more ground in-game (this is "Option B").
///                        1 = true 1:1, 2 = every real step moves you twice as
///                        far, 3 = triple, etc.
///
/// Attach to the player root that parents the OVRCameraRig (here, CameraPlayer).
/// Moving that root moves the whole rig - head, hands and controllers - together,
/// so tracking is preserved. Uses OVRInput, which the Meta core SDK provides.
/// </summary>
[DisallowMultipleComponent]
public class SimpleXRLocomotion : MonoBehaviour
{
    [Header("Thumbstick move")]
    [Tooltip("Metres per second for smooth (thumbstick) locomotion.")]
    public float moveSpeed = 2.5f;

    [Range(0f, 0.9f)]
    [Tooltip("Ignore thumbstick input below this magnitude.")]
    public float deadzone = 0.15f;

    [Tooltip("Move relative to head yaw when true; relative to the rig root when false.")]
    public bool moveRelativeToHead = true;

    [Header("Snap turn")]
    [Tooltip("Degrees turned per snap.")]
    public float snapTurnAngle = 45f;

    [Range(0.1f, 0.95f)]
    [Tooltip("Right-stick X magnitude that triggers a snap.")]
    public float snapTurnThreshold = 0.7f;

    [Header("Physical walking amplification (Option B)")]
    [Range(1f, 5f)]
    [Tooltip("Multiplier on real-world walking. 1 = natural 1:1. " +
             "2 = every real step moves you twice as far in-game. " +
             "Raise this if the room feels too big and you barely move.")]
    public float physicalMoveGain = 2.0f;

    OVRCameraRig _rig;
    Transform _head;
    Transform _trackingSpace;
    bool _snapArmed = true;

    Vector3 _lastHeadLocal;
    bool _hasLastHead;

    void Awake()
    {
        CacheRig();
    }

    void CacheRig()
    {
        if (_rig == null)
            _rig = GetComponentInChildren<OVRCameraRig>(true);
        if (_rig != null)
        {
            _head = _rig.centerEyeAnchor;
            _trackingSpace = _rig.trackingSpace;
        }
    }

    void Update()
    {
        if (_head == null || _trackingSpace == null)
        {
            CacheRig();
            if (_head == null || _trackingSpace == null)
                return;
        }

        HandlePhysicalGain();
        HandleThumbstickMove();
        HandleSnapTurn();
    }

    /// <summary>
    /// Option B: adds extra world movement proportional to how far the headset
    /// physically moved this frame, so real walking covers more ground.
    /// Head position is measured inside TrackingSpace, which is the pure tracked
    /// pose - unaffected by the rig root moving - so this reads only real motion.
    /// </summary>
    void HandlePhysicalGain()
    {
        Vector3 headLocal = _trackingSpace.InverseTransformPoint(_head.position);

        if (_hasLastHead && physicalMoveGain > 1f)
        {
            Vector3 delta = headLocal - _lastHeadLocal;
            delta.y = 0f; // horizontal only - don't amplify ducking/standing
            Vector3 worldExtra = _trackingSpace.TransformVector(delta) * (physicalMoveGain - 1f);
            transform.position += worldExtra;
        }

        // Head-local is unchanged by moving the rig root (head and tracking space
        // move together), so this stays a pure physical reading next frame.
        _lastHeadLocal = headLocal;
        _hasLastHead = true;
    }

    void HandleThumbstickMove()
    {
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (input.magnitude < deadzone)
            return;

        Transform dir = moveRelativeToHead ? _head : transform;

        Vector3 forward = dir.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = dir.right;
        right.y = 0f;
        right.Normalize();

        Vector3 move = forward * input.y + right * input.x;
        transform.position += move * (moveSpeed * Time.deltaTime);
    }

    void HandleSnapTurn()
    {
        float x = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        // Re-arm only when the stick returns near centre, so one flick = one snap.
        if (Mathf.Abs(x) < snapTurnThreshold)
        {
            _snapArmed = true;
            return;
        }

        if (!_snapArmed)
            return;

        _snapArmed = false;
        float angle = snapTurnAngle * Mathf.Sign(x);

        // Rotate about the head so the player does not visually slide while turning.
        transform.RotateAround(_head.position, Vector3.up, angle);
    }
}
