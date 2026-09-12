using UnityEngine;

public class FeetFollower : MonoBehaviour
{
    [Header("VR Tracking Settings")]
    public Transform centerEyeAnchor;
    public float eyeHeightOffset = 1.6f;

    [Header("Locomotion Source")]
    [Tooltip("Drag your PlayerController (or CharacterController) here to read movement speed directly.")]
    public CharacterController characterController;

    [Header("Animation Settings")]
    public Animator avatarAnimator;
    public float speedThreshold = 0.15f; // Minimum locomotion speed to trigger walking animation

    void LateUpdate()
{
    if (centerEyeAnchor == null) return;

    // Maintain floor Y coordinate while pinning X/Z directly under the VR headset
    float groundY = characterController != null ? characterController.transform.position.y : transform.position.y;
    transform.position = new Vector3(centerEyeAnchor.position.x, groundY, centerEyeAnchor.position.z);

    // Rotate body with head yaw
    Vector3 forward = centerEyeAnchor.forward;
    forward.y = 0f;
    if (forward.sqrMagnitude > 0.001f)
    {
        transform.rotation = Quaternion.LookRotation(forward);
    }
}
}