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

    [Header("Jump (right \"A\" button)")]
    [Tooltip("How high a jump goes, in metres.")]
    public float jumpHeight = 1.0f;

    [Tooltip("How strong gravity pulls you back down after a jump. Higher = faster fall.")]
    public float gravity = -9.81f;

    [Tooltip("Layers considered 'ground' for jumping/landing. Defaults to Everything - " +
             "narrow this to your floor layer if jumping feels wrong near other objects.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Comfort note: vertical camera motion that doesn't match your real body can cause " +
             "motion sickness for some players. Test carefully; consider making jump optional.")]
    public bool jumpEnabled = true;

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
        HandleTurn();
        HandleJumpAndGravity();
        HandleCrouch();
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
    /// Simple gravity + jump without requiring a CharacterController: a downward
    /// raycast from the rig root checks for ground, and we integrate a vertical
    /// velocity by hand. Pressing the right controller's "A" button jumps.
    /// </summary>
    void HandleJumpAndGravity()
    {
        // Raycast a little above the feet so it starts outside the floor itself.
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        _isGrounded = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 0.2f, groundMask);

        if (_isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -1f; // small downward value keeps us stuck to the ground
        }

        if (jumpEnabled && _isGrounded &&
            OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            // v = sqrt(2 * g * h)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        _verticalVelocity += gravity * Time.deltaTime;
        transform.position += Vector3.up * (_verticalVelocity * Time.deltaTime);

        // If we fell below where the ground-check said we should be, snap back up
        // onto it so we don't sink through the floor over many frames.
        if (_isGrounded && _verticalVelocity <= -1f)
        {
            float snapUp = rayOrigin.y - hit.point.y - 0.1f;
            if (snapUp > 0f)
                transform.position -= Vector3.up * snapUp;
        }
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
