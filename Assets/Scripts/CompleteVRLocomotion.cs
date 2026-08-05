using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CompleteVRLocomotion : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign CenterEyeAnchor here.")]
    public Transform headTransform;
    [Tooltip("Assign LeftHandAnchor (or LeftControllerAnchor) here.")]
    public Transform leftHandTransform;
    [Tooltip("Assign RightHandAnchor (or RightControllerAnchor) here.")]
    public Transform rightHandTransform;

    [Header("Joystick Movement Settings")]
    public bool useJoystickMove = true;
    public float joystickMoveSpeed = 3.5f;

    [Header("Arm Swing Run Settings")]
    public float swingSensitivity = 2.5f;
    public float maxSpeed = 8.0f;
    [Tooltip("Combined hand-speed (m/s) that has to be exceeded before running kicks in. Higher = less jitter.")]
    public float minSwing = 0.6f;
    [Tooltip("Low-pass smoothing on the raw hand-speed measurement. 0=raw, 1=frozen. Higher = smoother but laggier.")]
    [Range(0f, 0.99f)]
    public float swingInputSmoothing = 0.75f;
    [Tooltip("Time (s) to ramp movement speed toward the target. Higher = softer starts/stops.")]
    public float swingRampTime = 0.18f;

    // Filtered / smoothed run state (used by CalculateArmSwingMovement).
    private float _filteredSwingSpeed;
    private float _currentRunSpeed;
    private float _runSpeedVelocity;

    [Header("Jump & Gravity")]
    public float jumpVelocity = 5.0f;
    public float gravity = -9.81f;

    [Header("Turn Settings (Right Stick)")]
    public bool useSmoothTurn = true;
    public float smoothTurnSpeed = 120f;
    public float snapTurnAngle = 45f;
    public float turnThreshold = 0.3f;

    [Header("Crouch Settings (Right Stick Down)")]
    public float crouchDepth = 0.5f;
    public float crouchTransitionTime = 0.15f;
    public float crouchThreshold = 0.6f;

    [Header("Physical Walking Gain")]
    [Range(1f, 6f)]
    [Tooltip("Amplifies physical steps in your room safely through collisions. 1 = 1:1 real, 3 = each real step covers 3× the ground.")]
    public float physicalMoveGain = 3.0f;

    [Header("Recenter")]
    [Tooltip("Button to snap the tracking space back to the standard eye height (fixes 'boot up too tall' bugs).")]
    public OVRInput.Button recenterButton = OVRInput.Button.Two;   // left-controller Y
    [Tooltip("Eye height (m) used when recentering. Standing avg ~1.7 m.")]
    public float recenterEyeHeight = 1.7f;

    // Internal State Variables
    private CharacterController _characterController;
    private OVRCameraRig _rig;
    private Transform _trackingSpace;

    private Vector3 _previousLeftPos;
    private Vector3 _previousRightPos;
    private Vector3 _lastHeadLocal;
    private bool _hasLastHead;

    private float _currentVerticalSpeed;
    private bool _turnArmed = true;

    private float _trackingSpaceBaseLocalY;
    private bool _hasTrackingSpaceBaseY;
    private float _crouchOffsetCurrent;
    private float _crouchVelocity;

    void Start()
    {
        _characterController = GetComponent<CharacterController>();

        _rig = GetComponentInChildren<OVRCameraRig>(true);
        if (_rig != null)
        {
            if (headTransform == null) headTransform = _rig.centerEyeAnchor;
            _trackingSpace = _rig.trackingSpace;
        }

        if (leftHandTransform != null) _previousLeftPos = leftHandTransform.localPosition;
        if (rightHandTransform != null) _previousRightPos = rightHandTransform.localPosition;
    }

    void Update()
    {
        if (headTransform == null) return;

        SyncColliderToHeadset();
        HandleTurn();
        HandleCrouch();
        HandleRecenter();

        // Combines Arm Swing, Joystick input, and Physical Room Scale Gain
        Vector3 totalHorizontalMove = CalculateArmSwingMovement() 
                                    + CalculateJoystickMovement() 
                                    + CalculatePhysicalGainMovement();

        HandleGravityAndJump(ref totalHorizontalMove);

        // Single atomic call ensures all movement respects scene physics/colliders
        _characterController.Move(totalHorizontalMove * Time.deltaTime);
    }

    private void SyncColliderToHeadset()
    {
        float actualY = headTransform.localPosition.y;
        float headHeight = (actualY < 0.2f) ? 1.75f : Mathf.Clamp(actualY, 1.0f, 2.2f);

        _characterController.height = headHeight;

        Vector3 newCenter = Vector3.zero;
        newCenter.x = headTransform.localPosition.x;
        newCenter.z = headTransform.localPosition.z;
        newCenter.y = headHeight / 2f; 

        _characterController.center = newCenter;
    }

    private Vector3 CalculateJoystickMovement()
    {
        if (!useJoystickMove) return Vector3.zero;

        // Reads Left Thumbstick input (X = Strafe, Y = Forward/Back)
        Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (primaryAxis.magnitude < 0.1f) return Vector3.zero;

        // Align movement relative to where the headset is pointing
        Vector3 forward = headTransform.forward;
        Vector3 right = headTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * primaryAxis.y) + (right * primaryAxis.x);
        return moveDir * joystickMoveSpeed;
    }

    private Vector3 CalculateArmSwingMovement()
    {
        // Motion is filtered twice:
        //   1. the raw per-frame hand speed goes through an exponential low-pass
        //      so a single dropped tracking frame doesn't spike the reading, and
        //   2. the output speed is SmoothDamp'd toward its target so the character
        //      accelerates smoothly instead of teleporting between speeds each
        //      frame. Both stages are what makes running "not choppy".
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        float rawTotal = 0f;
        if (leftHandTransform != null && rightHandTransform != null)
        {
            Vector3 leftHandDelta = leftHandTransform.localPosition - _previousLeftPos;
            Vector3 rightHandDelta = rightHandTransform.localPosition - _previousRightPos;

            _previousLeftPos = leftHandTransform.localPosition;
            _previousRightPos = rightHandTransform.localPosition;

            rawTotal = (leftHandDelta.magnitude + rightHandDelta.magnitude) / dt;
        }

        // Frame-rate-independent EMA. `swingInputSmoothing` is expressed as
        // "fraction to retain per 1/60 s" so the feel is the same on 72/90/120 Hz.
        float retain = Mathf.Pow(swingInputSmoothing, dt * 60f);
        _filteredSwingSpeed = Mathf.Lerp(rawTotal, _filteredSwingSpeed, retain);

        // Require BOTH grips to trigger the run. Single-grip is reserved for grabbing
        // (Meta's ControllerGrabInteractor uses grip); if we listened to either grip,
        // reaching for a cube with one hand would silently start you running and shove
        // the cube out of range before the grab could land.
        bool isSwingingActive = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger) &&
                                OVRInput.Get(OVRInput.Button.SecondaryHandTrigger);

        float targetSpeed = 0f;
        if (isSwingingActive && _filteredSwingSpeed > minSwing)
        {
            // Subtract the deadband so we ramp up from zero, not from minSwing.
            targetSpeed = Mathf.Min((_filteredSwingSpeed - minSwing) * swingSensitivity, maxSpeed);
        }

        _currentRunSpeed = Mathf.SmoothDamp(
            _currentRunSpeed, targetSpeed, ref _runSpeedVelocity, swingRampTime);

        if (_currentRunSpeed < 0.01f) return Vector3.zero;

        Vector3 forwardDir = headTransform.forward;
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 1e-6f) return Vector3.zero;
        forwardDir.Normalize();

        return forwardDir * _currentRunSpeed;
    }

    private Vector3 CalculatePhysicalGainMovement()
    {
        if (_trackingSpace == null || physicalMoveGain <= 1.0f)
            return Vector3.zero;

        Vector3 headLocal = _trackingSpace.InverseTransformPoint(headTransform.position);
        Vector3 gainVelocity = Vector3.zero;

        if (_hasLastHead)
        {
            Vector3 delta = headLocal - _lastHeadLocal;
            delta.y = 0f; // Handled separately by headset height sync

            Vector3 worldDelta = _trackingSpace.TransformVector(delta);
            gainVelocity = (worldDelta * (physicalMoveGain - 1.0f)) / Time.deltaTime;
        }

        _lastHeadLocal = headLocal;
        _hasLastHead = true;

        return gainVelocity;
    }

    private void HandleGravityAndJump(ref Vector3 currentMove)
    {
        if (_characterController.isGrounded)
        {
            if (_currentVerticalSpeed < 0)
            {
                _currentVerticalSpeed = -2.0f;
            }

            if (OVRInput.GetDown(OVRInput.Button.One)) // "A" Button
            {
                _currentVerticalSpeed = jumpVelocity;
            }
        }
        else
        {
            _currentVerticalSpeed += gravity * Time.deltaTime;
        }

        currentMove.y = _currentVerticalSpeed;
    }

    private void HandleTurn()
    {
        float x = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        if (useSmoothTurn)
        {
            if (Mathf.Abs(x) < turnThreshold) return;
            float angle = smoothTurnSpeed * x * Time.deltaTime;
            transform.RotateAround(headTransform.position, Vector3.up, angle);
            return;
        }

        if (Mathf.Abs(x) < turnThreshold)
        {
            _turnArmed = true;
            return;
        }

        if (!_turnArmed) return;

        _turnArmed = false;
        float snapAngle = snapTurnAngle * Mathf.Sign(x);
        transform.RotateAround(headTransform.position, Vector3.up, snapAngle);
    }

    // Snaps the tracking space so the current headset position reports the
    // configured eye-height. Fixes the "boot up too tall / too high" bug when
    // the guardian origin isn't where the user actually is at start.
    private void HandleRecenter()
    {
        if (_trackingSpace == null || headTransform == null) return;
        if (!OVRInput.GetDown(recenterButton)) return;

        // Head world -> tracking-space local
        Vector3 headTs = _trackingSpace.InverseTransformPoint(headTransform.position);
        Vector3 ts = _trackingSpace.localPosition;
        ts.y += (recenterEyeHeight - headTs.y);   // lift/lower so head reports recenterEyeHeight
        _trackingSpace.localPosition = ts;
        _trackingSpaceBaseLocalY = _trackingSpace.localPosition.y; // reset crouch base
        _hasLastHead = false;                                     // avoid a huge gain kick
        Debug.Log($"[Locomotion] Recentered — head localY now ~{recenterEyeHeight:0.00} m.");
    }

    private void HandleCrouch()
    {
        if (_trackingSpace == null) return;

        if (!_hasTrackingSpaceBaseY)
        {
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