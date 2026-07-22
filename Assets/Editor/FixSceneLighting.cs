using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Resets the blown-out lighting values that ship with the Furniture_ges1
/// asset pack.
///
/// The pack was authored for the Built-in Render Pipeline in Gamma color space,
/// and relied on its own Post Processing Stack v1 profile (PostProcessing.asset)
/// to tonemap a deliberately over-bright scene back into range. This project is
/// URP + Linear, which ignores that profile entirely, so the inflated values
/// clip straight to white - including on brand new primitives, because ambient
/// light applies to every object regardless of material.
/// </summary>
static class FixSceneLighting
{
    const float TargetAmbientIntensity = 1f;
    const float TargetIndirectOutputScale = 1f;
    const float TargetAlbedoBoost = 1f;
    const float TargetDirectionalIntensity = 1f;

    [MenuItem("Tools/Fix Scene Lighting (Furniture_ges1 blowout)")]
    static void Fix()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var report = new StringBuilder();
        report.AppendLine($"Fix Scene Lighting - scene '{scene.name}'");

        FixAmbient(report);
        FixGlobalIllumination(report);
        FixDirectionalLights(scene, report);

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine();
        report.AppendLine("Scene marked dirty - press Ctrl+S to persist. Ctrl+Z undoes this.");
        Debug.Log(report.ToString());
    }

    static void FixAmbient(StringBuilder report)
    {
        // Ambient is the dominant cause: it lights every object uniformly, so an
        // 8x multiplier blows out even an untouched default material.
        var before = RenderSettings.ambientIntensity;
        if (Mathf.Approximately(before, TargetAmbientIntensity))
        {
            report.AppendLine($"  Ambient Intensity   already {before}");
            return;
        }

        var renderSettings = GetRenderSettingsObject();
        if (renderSettings != null)
            Undo.RecordObject(renderSettings, "Fix Scene Lighting");

        RenderSettings.ambientIntensity = TargetAmbientIntensity;
        report.AppendLine($"  Ambient Intensity   {before} -> {TargetAmbientIntensity}");

        if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Skybox)
        {
            report.AppendLine($"    note: ambient mode is {RenderSettings.ambientMode}; the intensity " +
                              "multiplier only applies in Skybox mode.");
        }
    }

    static void FixGlobalIllumination(StringBuilder report)
    {
        var lightmapSettings = GetLightmapSettingsObject();
        if (lightmapSettings == null)
        {
            report.AppendLine("  GI settings         SKIPPED - could not resolve the LightmapSettings " +
                              "object. Set Albedo Boost and Indirect Intensity to 1 by hand in " +
                              "Window > Rendering > Lighting > Lightmapping Settings.");
            return;
        }

        var so = new SerializedObject(lightmapSettings);
        ApplyFloat(so, "m_GISettings.m_IndirectOutputScale", TargetIndirectOutputScale,
                   "Indirect Intensity", report);
        ApplyFloat(so, "m_GISettings.m_AlbedoBoost", TargetAlbedoBoost,
                   "Albedo Boost", report);
        so.ApplyModifiedProperties();
    }

    static void ApplyFloat(SerializedObject so, string path, float target, string label, StringBuilder report)
    {
        var prop = so.FindProperty(path);
        if (prop == null)
        {
            report.AppendLine($"  {label,-19} SKIPPED - property '{path}' not found on this Unity version.");
            return;
        }

        var before = prop.floatValue;
        if (Mathf.Approximately(before, target))
        {
            report.AppendLine($"  {label,-19} already {before}");
            return;
        }

        prop.floatValue = target;
        report.AppendLine($"  {label,-19} {before} -> {target}");
    }

    static void FixDirectionalLights(UnityEngine.SceneManagement.Scene scene, StringBuilder report)
    {
        var directionals = new List<Light>();
        foreach (var root in scene.GetRootGameObjects())
        {
            directionals.AddRange(root.GetComponentsInChildren<Light>(true)
                                      .Where(l => l.type == LightType.Directional));
        }

        var active = directionals.Where(l => l.enabled && l.gameObject.activeInHierarchy).ToList();
        if (active.Count == 0)
        {
            report.AppendLine("  Directional lights  none active");
            return;
        }

        // Keep the first active one at a sane intensity; disable the rest rather
        // than deleting, so the change stays reversible.
        var keep = active[0];
        Undo.RecordObject(keep, "Fix Scene Lighting");
        var beforeIntensity = keep.intensity;
        keep.intensity = TargetDirectionalIntensity;
        report.AppendLine($"  Directional '{keep.name}'  intensity {beforeIntensity} -> {TargetDirectionalIntensity} (kept)");

        foreach (var extra in active.Skip(1))
        {
            Undo.RecordObject(extra, "Fix Scene Lighting");
            extra.enabled = false;
            report.AppendLine($"  Directional '{extra.name}'  disabled (duplicate; intensity was {extra.intensity})");
        }
    }

    static Object GetRenderSettingsObject()
    {
        return Resources.FindObjectsOfTypeAll<Object>()
                        .FirstOrDefault(o => o.GetType().Name == "RenderSettings");
    }

    static Object GetLightmapSettingsObject()
    {
        // LightmapSettings is a scene singleton with no public accessor to the
        // underlying Object. Prefer the type scan, fall back to the internal
        // editor method if the type name ever changes.
        var found = Resources.FindObjectsOfTypeAll<Object>()
                             .FirstOrDefault(o => o.GetType().Name == "LightmapSettings");
        if (found != null)
            return found;

        var method = typeof(LightmapEditorSettings)
            .GetMethod("GetLightmapSettings", BindingFlags.Static | BindingFlags.NonPublic);
        return method?.Invoke(null, null) as Object;
    }
}
