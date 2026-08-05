using UnityEngine;

// Turns the calibration slider's 0-100 % into a real, physical weight on the
// dumbbell so it visibly *and* physically feels heavier as the slider goes up.
//
// What changes based on the slider:
//   * Rigidbody.mass         -> Meta's Grabbable holds objects with a spring;
//                              a heavier mass sags in the hand and swings
//                              slower, so the illusion lines up with the EMS
//                              signal we send.
//   * Emission color / tint  -> darker/redder as it gets heavier so a person
//                              looking at the dumbbell can *see* the setting.
//   * On-screen weight label -> if a TMP_Text is assigned we push
//                              "12 lb" (etc.) into it.
//
// The EMS button-press count on the ESP32 is already handled by GrabDetector
// reading PlateFillPercent.percent at grab time. This script is only about
// the visual + haptic feel of the object itself.
[DisallowMultipleComponent]
public class DumbbellWeight : MonoBehaviour
{
    [Header("Weight source")]
    [Tooltip("Slider that reports 0-100 %. Auto-found if left blank.")]
    public PlateFillPercent slider;

    [Header("Weight mapping")]
    [Tooltip("Rigidbody mass (kg) at slider = 0 %.")]
    public float minMassKg = 0.5f;
    [Tooltip("Rigidbody mass (kg) at slider = 100 %.")]
    public float maxMassKg = 12f;
    [Tooltip("Displayed weight (lb) at slider = 0 %.")]
    public float minDisplayLb = 2f;
    [Tooltip("Displayed weight (lb) at slider = 100 %.")]
    public float maxDisplayLb = 25f;

    [Header("Visual feedback (optional)")]
    [Tooltip("Renderer(s) whose material tints darker/redder as weight rises. Leave empty to skip.")]
    public Renderer[] tintTargets;
    public Color lightColor = new Color(0.85f, 0.85f, 0.90f);
    public Color heavyColor = new Color(0.28f, 0.08f, 0.10f);
    [Tooltip("Which material property to write. '_BaseColor' for URP Lit, '_Color' for built-in.")]
    public string colorProperty = "_BaseColor";

    [Header("Readout (optional)")]
    public TMPro.TMP_Text weightLabel;
    public string labelFormat = "{0:0} lb";

    private Rigidbody _rb;
    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (slider == null) slider = FindAnyObjectByType<PlateFillPercent>();
        Apply();
    }

    void Update()
    {
        Apply();
    }

    private void Apply()
    {
        float t = 0f;
        if (slider != null) t = Mathf.Clamp01(slider.percent / 100f);

        if (_rb != null)
        {
            float mass = Mathf.Lerp(minMassKg, maxMassKg, t);
            // Only write if the change is meaningful — avoids waking the
            // physics engine every single frame with an identical value.
            if (!Mathf.Approximately(_rb.mass, mass)) _rb.mass = mass;
        }

        if (tintTargets != null && tintTargets.Length > 0)
        {
            Color c = Color.Lerp(lightColor, heavyColor, t);
            foreach (var r in tintTargets)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(colorProperty, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        if (weightLabel != null)
        {
            float lb = Mathf.Lerp(minDisplayLb, maxDisplayLb, t);
            weightLabel.text = string.Format(labelFormat, lb);
        }
    }
}
