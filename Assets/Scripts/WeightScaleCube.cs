using UnityEngine;

// Visualizes the "invisible weight" illusion: as the calibration plate slides from
// 0 to 100 (the same 0-100 range as the EMS button-press count sent to the ESP32),
// this cube grows from minScale to maxScale so heavier settings visibly look heavier.
public class WeightScaleCube : MonoBehaviour
{
    [Header("Weight Source")]
    public PlateFillPercent weightPlate;

    [Header("Scale Range")]
    public float minScale = 0.3f;
    public float maxScale = 2.5f;

    void Update()
    {
        if (weightPlate == null) return;

        float t = Mathf.Clamp01(weightPlate.percent / 100f);
        float scale = Mathf.Lerp(minScale, maxScale, t);
        transform.localScale = Vector3.one * scale;
    }
}
