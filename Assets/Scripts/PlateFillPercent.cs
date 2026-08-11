using UnityEngine;
using TMPro;

public class PlateFillPercent : MonoBehaviour
{
    [Header("Plate Local X Range (knob local X at 0% and 100%)")]
    public float maxX = 0.30228f; // 0%
    public float minX = -3.575f;   // 100%

    [Header("Percent readout (optional)")]
    public TMP_Text percentLabel;
    public Vector3 labelOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Track fill bar (optional)")]
    public Transform fillBar;
    public float fillBarMaxScaleX = 4f;
    public Color fillColorLow  = new Color(0.35f, 0.75f, 1f);   
    public Color fillColorHigh = new Color(1f, 0.35f, 0.25f);   

    [System.NonSerialized] public float percent;

    private Renderer _fillRenderer;
    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        if (fillBar != null) _fillRenderer = fillBar.GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        // 1. Use localPosition so world translation doesn't offset the math
        float currentX = transform.localPosition.x;

        // 2. InverseLerp handles the reverse min/max mapping
        percent = Mathf.InverseLerp(maxX, minX, currentX) * 100f;

        // 3. Deadzone snapping to guarantee solid 0% and 100% readouts
        if (percent <= 0.5f) percent = 0f;
        if (percent >= 99.5f) percent = 100f;

        float t = Mathf.Clamp01(percent / 100f);

        if (percentLabel != null)
        {
            percentLabel.text = Mathf.RoundToInt(percent) + "%";
            percentLabel.transform.position = transform.position + labelOffset;
        }

        if (fillBar != null)
        {
            var s = fillBar.localScale;
            s.x = Mathf.Max(0.001f, t * fillBarMaxScaleX);
            fillBar.localScale = s;

            if (_fillRenderer != null)
            {
                _fillRenderer.GetPropertyBlock(_mpb);
                Color c = Color.Lerp(fillColorLow, fillColorHigh, t);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);
                _mpb.SetColor("_EmissionColor", c * 0.6f);
                _fillRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}