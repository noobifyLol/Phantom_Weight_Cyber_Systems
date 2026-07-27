using UnityEngine;
using Oculus.Interaction;

public class GrabDetector : MonoBehaviour
{
    private Grabbable grabbable;

    void Start()
    {
        grabbable = GetComponent<Grabbable>();

        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += OnPointerEvent;
        }
        else
        {
            Debug.LogError("No Grabbable component found!");
        }
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            Debug.Log("Cube grabbed!");
        }

        if (evt.Type == PointerEventType.Unselect)
        {
            Debug.Log("Cube released!");
        }
    }
}