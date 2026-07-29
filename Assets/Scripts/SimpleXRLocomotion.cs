using UnityEngine;

/// <summary>
/// Basic VR locomotion for a Meta Building Blocks camera rig.
///
///   >>> ALL THE NUMBERS YOU WANT TO TWEAK ARE PUBLIC FIELDS BELOW. <<<
///   Select the GameObject this script is on (the camera rig) in the Hierarchy,
///   and every setting below shows up in the Inspector with a description.
///   Change a number, press Play, feel the difference - no coding needed.
///
///   CONTROLS (Meta Touch controllers):
///   Left thumbstick          = move (smooth walk, relative to where your head looks)
///   Right thumbstick left/right = turn the camera (smooth, continuous - like a normal game)
///   Right thumbstick down     = crouch (hold it down; release to stand back up)
///   Right "A" button          = jump
///   Physical walking          = also moves you; Physical Move Gain amplifies real steps.
///
/// Attach this to the player root that parents the OVRCameraRig (the object with
/// OVRManager + OVRCameraRig on it - e.g. "[BuildingBlock] Camera Rig").
/// Moving that root moves the whole rig - head, hands and controllers - together,
/// so tracking is preserved. Uses OVRInput, which the Meta core SDK provides.
/// </summary>
[DisallowMultipleComponent]
public class SimpleXRLocomotion : MonoBehaviour
{
    [Header("Move (left thumbstick)")]
    [Tooltip("Metres per second for smooth (thumbstick) locomotion. Higher = faster walk.")]
    public float moveSpeed = 2.5f;

    [Range(0f, 0.9f)]
    [Tooltip("Ignore thumbstick input below this magnitude, so it doesn't drift at rest.")]
    public float deadzone = 0.15f;

    [Tooltip("Move relative to head yaw when true (walk where you're looking); " +
             "relative to the rig root when false.")]
    public bool moveRelativeToHead = true;

    [Header("Turn (right thumbstick left/right)")]
    [Tooltip("ON = smooth continuous turning while you hold the stick left/right, like a normal " +
             "game camera. OFF = comfort snap-turn (jumps by Snap Turn Angle instead).")]
    public bool useSmoothTurn = true;

    [Tooltip("Degrees per second turned while the stick is held over, when Smooth Turn is ON.")]
    public float smoothTurnSpeed = 120f;

    [Tooltip("Degrees turned per snap, when Smooth Turn is OFF.")]
    public float snapTurnAngle = 45f;

    [Range(0.1f, 0.95f)]
    [Tooltip("How far you must push the right stick sideways before it registers as turning input.")]
    public float turnThreshold = 0.3f;

    [Header("Crouch (right thumbstick down, held)")]
    [Tooltip("How far (metres) the view drops while crouching.")]
    public float crouchDepth = 0.5f;

    [Tooltip("How quickly the view moves into/out of the crouch, in seconds.")]
    public float crouchTransitionTime = 0.15f;

    [Range(0.1f, 0.95f)]
    [Tooltip("How far down you must push the right stick before it registers as crouch input.")]
    public float crouchThreshold = 0.6f;

    [Header("Physical walking amplification (Option B)")]
    [Range(1f, 5f)]
    [Tooltip("Multiplier on real-world walking. 1 = natural 1:1. " +
             "2 = every real step moves you twice as far in-game. " +
             "Raise this if the room feels too big and you barely move.")]
    public float physicalMoveGain = 2.0f;

    [Range(1f, 6f)]
    [Tooltip("Multiplier on real-world CROUCHING (physically moving your head up/down). " +
             "1 = natural 1:1 (a 3cm real crouch = 3cm in-game, which feels like barely " +
             "anything). Raise this so a small real crouch produces a much bigger dip in-game.")]
    public float physicalCrouchGain = 3.0f;

    [Header("Fall recovery (safety net)")]
    [Tooltip("If you fall further than this many metres below where you started, you get " +
             "teleported back to your starting spot. Protects against walking off the edge " +
             "of the map or through a gap in the level's colliders.")]

    OVRCameraRig _rig;
    Transform _head;
    Transform _trackingSpace;
    bool _turnArmed = true;
    float _trackingSpaceBaseLocalY;
    bool _hasTrackingSpaceBaseY;

    Vector3 _lastHeadLocal;
    bool _hasLastHead;

    // Jump/fall state
    float _verticalVelocity;
    bool _isGrounded = true;

    // Crouch state
    float _crouchOffsetCurrent;   // how far down we currently are (0 = standing)
    float _crouchVelocity;        // used by SmoothDamp

    // Fall recovery state
    Vector3 _spawnPosition;
    bool _hasSpawnPosition;

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

        if (!_hasSpawnPosition)
        {
            _spawnPosition = transform.position;
            _hasSpawnPosition = true;
        }

        HandlePhysicalGain();
        HandleThumbstickMove();
        HandleTurn();
        HandleCrouch();
      
    }

    /// <summary>
    /// Option B: adds extra world movement proportional to how far the headset
    /// physically moved this frame, so real walking covers more ground, and real
    /// crouching/standing covers more height. Head position is measured inside
    /// TrackingSpace, which is the pure tracked pose - unaffected by the rig root
    /// moving - so this reads only real motion.
    /// </summary>
    void HandlePhysicalGain()
    {
        Vector3 headLocal = _trackingSpace.InverseTransformPoint(_head.position);

        if (_hasLastHead)
        {
            Vector3 delta = headLocal - _lastHeadLocal;

            if (physicalMoveGain > 1f)
            {
                Vector3 horizontalDelta = delta;
                horizontalDelta.y = 0f;
                Vector3 worldExtra = _trackingSpace.TransformVector(horizontalDelta) * (physicalMoveGain - 1f) * 3.0f;
                transform.position += worldExtra ;
            }

            if (physicalCrouchGain > 1f)
            {
                // Vertical head motion is already "up" in world space regardless of
                // rig yaw, so this doesn't need TransformVector like the horizontal case.
                float verticalExtra = delta.y * (physicalCrouchGain - 1f);
                transform.position += Vector3.up * verticalExtra;
            }
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

    void HandleTurn()
    {
        float x = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        if (useSmoothTurn)
        {
            // Continuous turn while the stick is held past the threshold - a normal
            // game-style camera turn, driven by how far the stick is pushed over.
            if (Mathf.Abs(x) < turnThreshold)
                return;

            float angle = smoothTurnSpeed * x * Time.deltaTime;
            transform.RotateAround(_head.position, Vector3.up, angle);
            return;
        }

        // Comfort snap-turn: one flick = one fixed-size snap.
        if (Mathf.Abs(x) < turnThreshold)
        {
            _turnArmed = true;
            return;
        }

        if (!_turnArmed)
            return;

        _turnArmed = false;
        float snapAngle = snapTurnAngle * Mathf.Sign(x);

        // Rotate about the head so the player does not visually slide while turning.
        transform.RotateAround(_head.position, Vector3.up, snapAngle);
    }

    

    /// <summary>
    /// Pushing the right thumbstick down lowers the view by Crouch Depth while held,
    /// and smoothly returns to standing height on release. This offsets the tracking
    /// space (not the rig root), so it stacks cleanly with move/turn/jump above.
    /// </summary>
    void HandleCrouch()
    {
        if (!_hasTrackingSpaceBaseY)
        {
            // Remember whatever height the tracking space started at, so we offset
            // from it instead of assuming it was exactly 0.
            _trackingSpaceBaseLocalY = _trackingSpace.localPosition.y;
            _hasTrackingSpaceBaseY = true;
        }

        float y = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y;
        bool wantsCrouch = y < -crouchThreshold;

        float target = wantsCrouch ? crouchDepth : 0f;
        _crouchOffsetCurrent = Mathf.SmoothDamp(
            _crouchOffsetCurrent, target, ref _crouchVelocity, crouchTransitionTime);

        Vector3 local = _trackingSpace.localPosition;
        local.y = _trackingSpaceBaseLocalY - _crouchOffsetCurrent;
        _trackingSpace.localPosition = local;
    }

    
}
