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

    private void Update()
    {
        // Calculate percentage
        float percent = Mathf.InverseLerp(maxX, minX, transform.position.x);

        // Clamp and convert to 0-100
        int displayPercent = Mathf.RoundToInt(percent * 50f);

        // Update text
        if (percentLabel != null)
        {
            percentLabel.text = displayPercent + "%";

            // Keep label above the plate
            percentLabel.transform.position = transform.position + labelOffset;
        }
    }
}