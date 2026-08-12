using UnityEngine;

public class FeetFollower : MonoBehaviour
{
    [Header("VR Tracking Settings")]
    public Transform centerEyeAnchor;
    public float eyeHeightOffset = 1.6f;

    [Header("Animation Settings")]
    public Animator avatarAnimator;
    public float speedThreshold = 0.15f; // Minimum ground speed to trigger walking

    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        if (centerEyeAnchor == null) return;

        // 1. Move feet anchor directly beneath VR headset
        Vector3 targetPosition = new Vector3(
            centerEyeAnchor.position.x,
            centerEyeAnchor.position.y - eyeHeightOffset,
            centerEyeAnchor.position.z
        );
        transform.position = targetPosition;

        // 2. Rotate body to face forward (horizontal rotation only)
        Vector3 forward = centerEyeAnchor.forward;
        forward.y = 0;
        if (forward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(forward);
        }

        // 3. Calculate movement speed across the floor
        float currentSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
        lastPosition = transform.position;

        // 4. Drive 'IsWalking' parameter in Animator Controller
        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool("IsWalking", currentSpeed > speedThreshold);
        }
    }
}