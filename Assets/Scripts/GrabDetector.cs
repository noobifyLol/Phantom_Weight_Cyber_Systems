using UnityEngine;
using Oculus.Interaction;

public class CubeGrabDetector : MonoBehaviour
{
    private Grabbable grabbable;
    private bool isGrabbed = false;

    void Start()
    {
        grabbable = GetComponent<Grabbable>();
    }

    void Update()
    {
        if (grabbable != null)
        {
            // Check if the pointable has any active selecting interactors (is grabbed)
            bool currentlyGrabbed = grabbable.SelectingPointsCount > 0;

            if (currentlyGrabbed && !isGrabbed)
            {
                isGrabbed = true;
                OnGrabbed();
            }
            else if (!currentlyGrabbed && isGrabbed)
            {
                isGrabbed = false;
                OnReleased();
            }
        }
    }

    void OnGrabbed()
    {
        Debug.Log("Cube has been grabbed!");
    }

    void OnReleased()
    {
        Debug.Log("Cube has been released!");
    }
}
