using UnityEngine;
using TMPro;

public class PlateFillPercent : MonoBehaviour
{
    [Header("Plate X Range")]
    public float maxX = -0.213f; // 0%
    public float minX = -4.106f;  // 100%

    [Header("UI")]
    public TMP_Text percentLabel;

    [Header("Label Offset")]
    public Vector3 labelOffset = new Vector3(0f, 0.5f, 0f);

    // 0-100, kept in sync every frame. Read by GrabDetector (weight sent to the ESP32)
    // and WeightScaleCube (invisible-weight visualization).
    public float percent { get; private set; }

    private void Update()
    {
        // Calculate percentage
        float t = Mathf.InverseLerp(maxX, minX, transform.position.x);

        // Clamp and convert to 0-100
        int displayPercent = Mathf.RoundToInt(t * 100f);
        percent = displayPercent;

        // Update text
        if (percentLabel != null)
        {
            percentLabel.text = displayPercent + "%";

            // Keep label above the plate
            percentLabel.transform.position = transform.position + labelOffset;
        }
    }
}
