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
    [Tooltip("Combined hand-speed (m/s) that has to be exceeded before running kicks in — CONTROLLER mode. Higher = less jitter.")]
    public float minSwing = 0.6f;
    [Tooltip("Combined hand-speed (m/s) required to trigger running when the user is on bare HAND TRACKING (no controllers, no grip). Set noticeably higher than 'minSwing' since with hand tracking every gesture, pointing, or natural body motion registers as a swing.")]
    public float minSwingHands = 2.0f;
    [Tooltip("Low-pass smoothing on the raw hand-speed measurement. 0=raw, 1=frozen. Higher = smoother but laggier.")]
    [Range(0f, 0.99f)]
    public float swingInputSmoothing = 0.75f;
    [Tooltip("Time (s) to ramp movement speed toward the target. Higher = softer starts/stops.")]
    public float swingRampTime = 0.18f;

    // Filtered / smoothed run state (used by CalculateArmSwingMovement).
    private float _filteredSwingSpeed;
    private float _currentRunSpeed;
    private float _runSpeedVelocity;

    [Header("Gravity")]
    // Jump was removed on purpose — the A-button "fly" bug came from stacked
    // jump impulses when isGrounded briefly re-triggered off cube contacts.
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

    [Header("Physical Height Gain (crouching/standing)")]
    [Range(1f, 4f)]
    [Tooltip("Max amplification for REAL vertical head movement, applied while standing/tall — same idea as Physical Move Gain but vertical. Fades to 1x (unamplified real crouch) as the headset nears the ground, see the two Y thresholds below.")]
    public float heightGain = 2.0f;
    [Tooltip("Headset world height (m) above which the FULL heightGain applies.")]
    public float heightGainFullAboveY = 1.1f;
    [Tooltip("Headset world height (m) at/below which heightGain fades to 1x so reaching for objects near the floor stays precise.")]
    public float heightGainFadeToOneBelowY = 0.5f;

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
    private float _lastHeadLocalY; // raw headTransform.localPosition.y, used only by height gain

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

        Vector3 totalHorizontalMove = CalculateArmSwingMovement()
                                    + CalculateJoystickMovement()
                                    + CalculatePhysicalGainMovement();

        HandleGravity(ref totalHorizontalMove);

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

        Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (primaryAxis.magnitude < 0.1f) return Vector3.zero;

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
        // Motion is filtered twice — see minSwing / swingInputSmoothing tooltips.
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

        // Frame-rate-independent EMA. `swingInputSmoothing` = "fraction to retain per 1/60 s".
        float retain = Mathf.Pow(swingInputSmoothing, dt * 60f);
        _filteredSwingSpeed = Mathf.Lerp(rawTotal, _filteredSwingSpeed, retain);

        // Controllers: require BOTH grips so single-hand grip stays for grab.
        // Hands: no grip button exists at all, so gate on a much higher swing speed
        //        (minSwingHands) — otherwise ordinary gestures/pointing register as running.
        bool usingHands = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;

        bool isSwingingActive = usingHands ||
                                (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger) &&
                                 OVRInput.Get(OVRInput.Button.SecondaryHandTrigger));

        float threshold = usingHands ? minSwingHands : minSwing;

        float targetSpeed = 0f;
        if (isSwingingActive && _filteredSwingSpeed > threshold)
        {
            targetSpeed = Mathf.Min((_filteredSwingSpeed - threshold) * swingSensitivity, maxSpeed);
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
            delta.y = 0f;

            Vector3 worldDelta = _trackingSpace.TransformVector(delta);
            gainVelocity = (worldDelta * (physicalMoveGain - 1.0f)) / Time.deltaTime;
        }

        _lastHeadLocal = headLocal;
        _hasLastHead = true;

        return gainVelocity;
    }

    // Gravity-only. Jump intentionally removed — the A-button "fly" behaviour
    // came from stacked jump impulses when isGrounded briefly re-triggered
    // during cube contacts. If jump is needed later, gate it on a real ground
    // check (SphereCast down onto a Floor layer) rather than CC.isGrounded.
    private void HandleGravity(ref Vector3 currentMove)
    {
        if (_characterController.isGrounded && _currentVerticalSpeed < 0f)
        {
            _currentVerticalSpeed = -2.0f;
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
    // configured eye-height. Fixes the "boot up too tall / too high" bug.
    private void HandleRecenter()
    {
        if (_trackingSpace == null || headTransform == null) return;
        if (!OVRInput.GetDown(recenterButton)) return;

        Vector3 headTs = _trackingSpace.InverseTransformPoint(headTransform.position);
        Vector3 ts = _trackingSpace.localPosition;
        ts.y += (recenterEyeHeight - headTs.y);
        _trackingSpace.localPosition = ts;
        _trackingSpaceBaseLocalY = _trackingSpace.localPosition.y;
        _hasLastHead = false;
        _lastHeadLocalY = headTransform.localPosition.y;
        Debug.Log($"[Locomotion] Recentered — head localY now ~{recenterEyeHeight:0.00} m.");
    }

    private void HandleCrouch()
    {
        if (_trackingSpace == null || headTransform == null) return;

        if (!_hasTrackingSpaceBaseY)
        {
            _trackingSpaceBaseLocalY = _trackingSpace.localPosition.y;
            _hasTrackingSpaceBaseY = true;
            _lastHeadLocalY = headTransform.localPosition.y;
        }

        // Physical height gain: standing up amplifies your in-game height upward,
        // crouching amplifies downward. Was inverted with -= (which cancelled the
        // real motion instead of amplifying it, so standing up made you appear
        // shorter and vice-versa). Correct sign is +=: real head Y delta gets
        // added on top of what you already get for free from the tracking system.
        //
        // Fade back to 1x near the floor (heightGainFadeToOneBelowY) so reaching
        // for objects on the ground stays precise.
        float headLocalY = headTransform.localPosition.y;
        float headWorldY = headTransform.position.y;
        float fadeT = Mathf.InverseLerp(heightGainFadeToOneBelowY, heightGainFullAboveY, headWorldY);
        float effectiveHeightGain = Mathf.Lerp(1f, heightGain, fadeT);

        if (effectiveHeightGain > 1.0f)
        {
            float verticalDelta = headLocalY - _lastHeadLocalY;
            _trackingSpaceBaseLocalY += verticalDelta * (effectiveHeightGain - 1.0f);
        }
        _lastHeadLocalY = headLocalY;

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
