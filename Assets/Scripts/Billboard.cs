using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        // Cache the main camera's transform for performance
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // Forces the text object to mirror the exact rotation of the camera
            transform.rotation = mainCameraTransform.rotation;
        }
    }
}
