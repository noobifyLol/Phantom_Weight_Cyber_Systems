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
    public float joystickMoveSpeed = 3.0f;

    [Header("Arm Swing Run Settings")]
    public float swingSensitivity = 2.5f;
    public float maxSpeed = 8.0f;
    public float minSwing = 0.6f;
    public float minSwingHands = 2.0f;
    [Range(0f, 0.99f)]
    public float swingInputSmoothing = 0.75f;
    public float swingRampTime = 0.18f;

    private float _filteredSwingSpeed;
    private float _currentRunSpeed;
    private float _runSpeedVelocity;

    [Header("Gravity & Jump")]
    public float gravity = -18.0f;
    public float jumpHeight = 1.5f;
    public OVRInput.Button jumpButton = OVRInput.Button.One;
    [Tooltip("Select ONLY ground/environment layers here (e.g. Default, Environment). DO NOT include Grabbables!")]
    public LayerMask groundLayer; 

    [Header("Turn Settings (Right Stick)")]
    public bool useSmoothTurn = true;
    public float smoothTurnSpeed = 120f;
    public float snapTurnAngle = 45f;
    public float turnThreshold = 0.3f;

    [Header("Crouch Settings (Right Stick Down)")]
    public float crouchDepth = 0.5f;
    public float crouchTransitionTime = 0.25f;
    public float crouchThreshold = 0.6f;

    [Header("Physical Walking Gain")]
    [Range(1f, 6f)]
    public float physicalMoveGain = 3.0f;

    [Header("Physical Height Gain")]
    [Range(1f, 4f)]
    public float heightGain = 2.0f;
    public float heightGainFullAboveY = 1.1f;
    public float heightGainFadeToOneBelowY = 0.5f;

    [Header("Recenter - Up ")]
    public OVRInput.Button recenterButton = OVRInput.Button.Two;
    public float recenterEyeHeight = 1.7f;

    [Header("Recenter - Down")]
    public OVRInput.Button recenterButton2 = OVRInput.Button.Three;
    public float recenterEyeHeightB = 0.2f;

    // Internal State
    private CharacterController _characterController;
    private OVRCameraRig _rig;
    private Transform _trackingSpace;

    private Vector3 _previousLeftPos;
    private Vector3 _previousRightPos;
    private Vector3 _lastHeadLocal;
    private bool _hasLastHead;
    private float _lastHeadLocalY;

    private float _currentVerticalSpeed;
    private bool _turnArmed = true;

    private float _trackingSpaceBaseLocalY;
    private bool _hasTrackingSpaceBaseY;
    private float _crouchOffsetCurrent;
    private float _crouchVelocity;
    private bool _isGroundedCustom;

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

        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
        }
    }

    void Update()
    {
        if (headTransform == null) return;

        SyncColliderToHeadset();
        CheckGroundStatus();
        HandleTurn();
        HandleCrouch();
        HandleJump();
        HandleRecenter();
        HandleRecenter2();

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

 private void CheckGroundStatus()
{
    // 1. If actively moving upwards from a jump, forcefully unground
    if (_currentVerticalSpeed > 0.1f)
    {
        _isGroundedCustom = false;
        return;
    }

    // 2. CRITICAL FIX: Exclude the player's own layer from the ground check
    // This prevents the sphere from colliding with your own CharacterController mid-air
    int safeGroundMask = groundLayer.value & ~(1 << gameObject.layer);

    // 3. Position the check sphere at the bottom center of the CharacterController
    Vector3 bottomCenter = transform.position + _characterController.center;
    bottomCenter.y -= (_characterController.height / 2f) - _characterController.radius;

    // 4. Perform sphere check
    bool sphereGrounded = Physics.CheckSphere(
        bottomCenter, 
        _characterController.radius * 0.9f, 
        safeGroundMask, // Using the fixed mask
        QueryTriggerInteraction.Ignore
    );

    _isGroundedCustom = _characterController.isGrounded || sphereGrounded;
}
private void HandleJump()
{
    // Check if the jump button is pressed this frame (A button / Primary Button)
    bool jumpPressed = OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.RawButton.A);

    if (jumpPressed && _isGroundedCustom)
    {
        // Calculate vertical jump velocity using physics equation: v = sqrt(2 * g * h)
        // jumpHeight should be a float variable (e.g., public float jumpHeight = 1.5f;)
        _currentVerticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        _isGroundedCustom = false;
    }
}
private void HandleGravity(ref Vector3 currentMove)
{
    if (_isGroundedCustom && _currentVerticalSpeed <= 0f)
    {
        // Stick to the ground when actually landed
        _currentVerticalSpeed = -2.0f;
    }
    else
    {
        // Apply full gravity when falling
        _currentVerticalSpeed += gravity * Time.deltaTime;
    }

    currentMove.y = _currentVerticalSpeed;
}

    private Vector3 CalculateJoystickMovement()
    {
        if (!useJoystickMove) return Vector3.zero;

        Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (primaryAxis.sqrMagnitude < 0.01f) return Vector3.zero;

        // Determine forward/right relative to headset orientation
        Vector3 forward = headTransform.forward;
        Vector3 right = headTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * primaryAxis.y) + (right * primaryAxis.x);
        return moveDirection * joystickMoveSpeed;
    }

    private Vector3 CalculateArmSwingMovement()
    {
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

        float retain = Mathf.Pow(swingInputSmoothing, dt * 60f);
        _filteredSwingSpeed = Mathf.Lerp(rawTotal, _filteredSwingSpeed, retain);

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

    private void HandleCrouch()
    {
        if (_trackingSpace == null || headTransform == null) return;

        if (!_hasTrackingSpaceBaseY)
        {
            _trackingSpaceBaseLocalY = _trackingSpace.localPosition.y;
            _hasTrackingSpaceBaseY = true;
            _lastHeadLocalY = headTransform.localPosition.y;
        }

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
    }

    private void HandleRecenter2()
    {
        if (_trackingSpace == null || headTransform == null) return;
        if (!OVRInput.GetDown(recenterButton2)) return;
        bool buttonPressed = OVRInput.GetDown(recenterButton2) || OVRInput.GetDown(OVRInput.RawButton.X);
        if (!buttonPressed) return;
        Vector3 currhead = _trackingSpace.InverseTransformPoint(headTransform.position);
        Vector3 ts = _trackingSpace.localPosition;
        float heightOffset = recenterEyeHeightB - currhead.y;
        
        ts.y += heightOffset; // Properly shift tracking space up or down
        _trackingSpace.localPosition = ts; // Fixed CS1612 by assigning whole vector

        _trackingSpaceBaseLocalY = _trackingSpace.localPosition.y;
        _hasLastHead = false;
        _lastHeadLocalY = headTransform.localPosition.y;
    }
}