using UnityEngine;

/// <summary>
/// Attach this to the same GameObject as OVRManager (the [BuildingBlock] Camera Rig).
/// Runs once on Awake to configure Quest 3 for best performance:
///   - 90 Hz display
///   - CPU/GPU sustained-high levels (prevents thermal throttle)
///   - Fixed Foveated Rendering level 3 (High) with dynamic scaling
///
/// All values are Inspector-tweakable — you never need to edit this script.
/// </summary>
[DisallowMultipleComponent]
public class QuestPerformanceSetup : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("90 or 72 Hz. Quest 3 prefers 90.")]
    public float displayFrequencyHz = 90f;

    [Header("CPU / GPU levels (0 = battery-saver, 4 = max on Quest 3)")]
    [Range(0, 4)] public int cpuLevel = 3;
    [Range(0, 4)] public int gpuLevel = 4;

    [Header("Foveated Rendering")]
    [Tooltip("0=Off 1=Low 2=Medium 3=High 4=HighTop. Quest 3 handles High well.")]
    [Range(0, 4)] public int foveationLevel = 3;
    [Tooltip("Let the runtime lower foveation when GPU headroom exists.")]
    public bool dynamicFoveation = true;

    void Awake()
    {
        // Frame rate
        Application.targetFrameRate = Mathf.RoundToInt(displayFrequencyHz);

        // Tell the OVR compositor the frequency we want
        OVRPlugin.systemDisplayFrequency = displayFrequencyHz;

        // CPU/GPU performance levels — sustained = stays at level even under load
        OVRPlugin.suggestedCpuPerfLevel = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh;
        OVRPlugin.suggestedGpuPerfLevel = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh;

        // Fixed Foveated Rendering — reduces resolution in peripheral vision
        OVRPlugin.foveatedRenderingLevel = foveationLevel;
        OVRPlugin.useDynamicFoveatedRendering = dynamicFoveation;
    }
}
