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

    [Header("Arm Swing Run Settings")]
    public float swingSensitivity = 2.5f;
    public float maxSpeed = 8.0f;
    public float minSwing = 0.15f;

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
    [Range(1f, 5f)]
    [Tooltip("Amplifies physical steps in your room safely through collisions.")]
    public float physicalMoveGain = 1.5f;

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

        Vector3 totalHorizontalMove = CalculateArmSwingMovement() + CalculatePhysicalGainMovement();
        HandleGravityAndJump(ref totalHorizontalMove);

        // Single atomic call ensures all movement respects scene physics/colliders
        _characterController.Move(totalHorizontalMove * Time.deltaTime);
    }

    private void SyncColliderToHeadset()
    {
        float headHeight = Mathf.Clamp(headTransform.localPosition.y, 1.0f, 2.2f);
        _characterController.height = headHeight;

        Vector3 newCenter = headTransform.localPosition;
        newCenter.y = _characterController.height / 2f;
        _characterController.center = newCenter;
    }

    private Vector3 CalculateArmSwingMovement()
    {
        if (leftHandTransform == null || rightHandTransform == null)
            return Vector3.zero;

        Vector3 leftHandDelta = leftHandTransform.localPosition - _previousLeftPos;
        Vector3 rightHandDelta = rightHandTransform.localPosition - _previousRightPos;

        float leftSpeed = leftHandDelta.magnitude / Time.deltaTime;
        float rightSpeed = rightHandDelta.magnitude / Time.deltaTime;

        _previousLeftPos = leftHandTransform.localPosition;
        _previousRightPos = rightHandTransform.localPosition;

        bool isSwingingActive = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger) || 
                                OVRInput.Get(OVRInput.Button.SecondaryHandTrigger);

        if (isSwingingActive)
        {
            float totalSwingSpeed = leftSpeed + rightSpeed;

            if (totalSwingSpeed > minSwing)
            {
                Vector3 forwardDir = headTransform.forward;
                forwardDir.y = 0f;
                forwardDir.Normalize();

                float calculatedSpeed = Mathf.Min(totalSwingSpeed * swingSensitivity, maxSpeed);
                return forwardDir * calculatedSpeed;
            }
        }

        return Vector3.zero;
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