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

    // Accessible from other scripts
    public float percent;

    private void Update()
    {
        percent = Mathf.InverseLerp(maxX, minX, transform.position.x) * 100f;

        int displayPercent = Mathf.RoundToInt(percent);

        if (percentLabel != null)
        {
            percentLabel.text = displayPercent + "%";
            percentLabel.transform.position = transform.position + labelOffset;
        }
    }
}
