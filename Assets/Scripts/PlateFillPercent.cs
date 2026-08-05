using UnityEngine;
using TMPro;

// Reads the slider knob's world X position and turns it into a 0-100 %
// value that other scripts (DumbbellWeight, GrabDetector -> ESP32) read.
//
// Also drives the visual polish on the slider itself:
//   * `percentLabel` shows "42%" floating above the knob (optional)
//   * `fillBar`      is a coloured strip along the track whose X-scale grows
//                    with `percent`, giving the slider an actual "fill" look
//                    instead of just a flat plate
//   * fill colour lerps low->high so the bar reads at a glance
public class PlateFillPercent : MonoBehaviour
{
    [Header("Plate X Range (knob world X at 0% and 100%)")]
    public float maxX = -0.213f; // 0%
    public float minX = -4.106f; // 100%

    [Header("Percent readout (optional)")]
    public TMP_Text percentLabel;
    public Vector3 labelOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Track fill bar (optional)")]
    [Tooltip("A stretched cube/quad parented to the slider. Its X scale is driven from 0 to fillBarMaxScaleX to visualise the current fill.")]
    public Transform fillBar;
    public float fillBarMaxScaleX = 4f;
    public Color fillColorLow  = new Color(0.35f, 0.75f, 1f);   // cool blue
    public Color fillColorHigh = new Color(1f, 0.35f, 0.25f);   // warm red

    // Accessible from other scripts (0..100).
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
        percent = Mathf.InverseLerp(maxX, minX, transform.position.x) * 100f;
        float t = Mathf.Clamp01(percent / 100f);

        if (percentLabel != null)
        {
            percentLabel.text = Mathf.RoundToInt(percent) + "%";
            percentLabel.transform.position = transform.position + labelOffset;
        }

        if (fillBar != null)
        {
            // Only change X — leave Y (thickness) and Z (depth) alone so
            // it stays overlaid on the plate.
            var s = fillBar.localScale;
            s.x = Mathf.Max(0.001f, t * fillBarMaxScaleX);
            fillBar.localScale = s;

            if (_fillRenderer != null)
            {
                _fillRenderer.GetPropertyBlock(_mpb);
                Color c = Color.Lerp(fillColorLow, fillColorHigh, t);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);   // built-in fallback
                _mpb.SetColor("_EmissionColor", c * 0.6f);
                _fillRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
