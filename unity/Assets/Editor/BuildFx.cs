using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 光影：URP 阴影/点光源档位、码头灯笼 + 小屋窗（夜里亮）、全局后期 Volume（暖色调色 + 柔和 Bloom + 轻暗角）。
public static class BuildFx
{
    const string Settings = "Assets/Settings";

    // ProjectSetup 调用：桌面软阴影 + 阴影距离按俯视范围收紧；网页无实时阴影、点光源每物体 ≤WebLamps。
    public static void TuneUrp(RenderPipelineAsset high, RenderPipelineAsset medium)
    {
        Set(high, so =>
        {
            P(so, "m_SoftShadowsSupported").boolValue = true;
            P(so, "m_ShadowDistance").floatValue = 40f;   // 相机 18 m、俯角 60°，画面最远约 30 m
            P(so, "m_ShadowCascadeCount").intValue = 2;
            P(so, "m_MainLightShadowmapResolution").intValue = 2048;
            P(so, "m_AdditionalLightsPerObjectLimit").intValue = 4;
        });
        Set(medium, so => P(so, "m_AdditionalLightsPerObjectLimit").intValue = Config.WebLamps);
        foreach (var a in new[] { high, medium }) Set(a, so => { P(so, "m_AdditionalLightsRenderingMode").intValue = 1; P(so, "m_SupportsHDR").boolValue = true; });
    }

    static void Set(Object o, System.Action<SerializedObject> f)
    {
        var so = new SerializedObject(o); f(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o);
    }

    static SerializedProperty P(SerializedObject so, string n) =>
        so.FindProperty(n) ?? throw new System.Exception($"missing serialized field {n} on {so.targetObject.GetType().Name}");

    public static void Build(Transform root, GameObject cabin, TimeOfDay tod, Camera cam)
    {
        var glow = BuildNature.Mat("M_glow", new Color(1f, 0.9f, 0.7f));
        glow.EnableKeyword("_EMISSION");
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        glow.SetColor("_EmissionColor", Config.LampColor);
        var pole = BuildNature.Mat("M_wood_dark", Config.WoodDark);
        var lamps = new List<Light>();

        var lanterns = new GameObject("Lanterns").transform;
        lanterns.SetParent(root, false);
        float x = -Config.DockWidth * 0.5f + 0.15f;
        foreach (float z in Config.LanternZ)
        {
            BuildNature.Box("LanternPole", lanterns, new Vector3(x, Config.GroundY + 0.75f, z), new Vector3(0.1f, 1.5f, 0.1f), pole);
            var lamp = BuildScene.Model("SM_lantern", lanterns, new Vector3(x, Config.GroundY + 1.45f, z), new Vector3(0.28f, 0.34f, 0.28f), glow);   // 低模灯笼，发光部分仍是 M_glow
            lamp.name = "Lantern";
            if (lamp.TryGetComponent<Collider>(out var col)) Object.DestroyImmediate(col);
            lamps.Add(Lamp(lamp.transform, Vector3.up * 0.2f, 7f));   // 点光源在灯身中间
        }

        // 小屋窗：窗玻璃已做进 SM_cabin（朝相机的 -Z 面，材质 M_glow，随时段一起亮）；这里只在窗前补一盏点光源
        var b = cabin.GetComponentInChildren<Renderer>().bounds;
        lamps.Insert(1, Lamp(cabin.transform, new Vector3(b.center.x, b.center.y, b.min.z - 0.8f) - cabin.transform.position, 6f));   // 网页只留前两盏：最近的灯笼 + 小屋

        tod.glow = glow;
        tod.lamps = lamps.ToArray();
        PostFx(root, cam);
    }

    static Light Lamp(Transform parent, Vector3 local, float range)
    {
        var l = new GameObject("LampLight", typeof(Light)).GetComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.position = parent.position + local;
        l.type = LightType.Point; l.range = range; l.color = Config.LampColor; l.intensity = 0f; l.shadows = LightShadows.None;
        return l;
    }

    static void PostFx(Transform root, Camera cam)
    {
        string path = $"{Settings}/PostFx.asset";
        AssetDatabase.DeleteAsset(path);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, path);
        var ca = prof.Add<ColorAdjustments>(true);
        prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);   // HDR + Bloom 不加色调映射会过曝
        ca.saturation.Override(8f); ca.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
        prof.Add<WhiteBalance>(true).temperature.Override(8f);
        var bloom = prof.Add<Bloom>(true);
        bloom.threshold.Override(1f); bloom.intensity.Override(0.6f); bloom.scatter.Override(0.65f);
        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(0.22f); vig.smoothness.Override(0.5f);
        foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
        EditorUtility.SetDirty(prof);

        var go = new GameObject("PostFx", typeof(Volume));
        go.transform.SetParent(root, false);
        var v = go.GetComponent<Volume>();
        v.isGlobal = true; v.sharedProfile = prof;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }
}
