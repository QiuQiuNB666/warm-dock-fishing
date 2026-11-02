using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 光影：URP 阴影/点光源档位、码头灯笼 + 小屋窗 + 篝火（夜里亮）、全局后期 Volume（暖色调色 + 柔和 Bloom + 轻暗角）。
public static class BuildFx
{
    const string Settings = "Assets/Settings";

    // ProjectSetup 和 Build 调用（loop-22：闸只跑 BuildScene，每次搭场景都对 Settings/ 下现成的资产重套一遍）：
    // 桌面软阴影 High、30 m 内 4 级联 4096、SSAO；网页无实时阴影、无 SSAO、点光源每物体 ≤WebLamps。
    public static void TuneUrp(RenderPipelineAsset high, RenderPipelineAsset medium)
    {
        Set(high, so =>
        {
            P(so, "m_SoftShadowsSupported").boolValue = true;
            P(so, "m_SoftShadowQuality").intValue = 3;   // High
            P(so, "m_ShadowDistance").floatValue = 30f;   // 相机 18 m、俯角 60°，画面最远约 25 m
            P(so, "m_ShadowCascadeCount").intValue = 4;
            P(so, "m_Cascade4Split").vector3Value = new Vector3(0.5f, 0.64f, 0.8f);   // 15 / 19 / 24 / 30 m：画面最近处也在 14 m 外，默认 2 / 6 / 14 m 前三级全落空
            P(so, "m_MainLightShadowmapResolution").intValue = 4096;
            P(so, "m_AdditionalLightsPerObjectLimit").intValue = 4;
        });
        Set(medium, so => P(so, "m_AdditionalLightsPerObjectLimit").intValue = Config.WebLamps);
        foreach (var a in new[] { high, medium }) Set(a, so => { P(so, "m_AdditionalLightsRenderingMode").intValue = 1; P(so, "m_SupportsHDR").boolValue = true; });
        Ssao(high);
        var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);   // High 档原来沿用 Very Low 的贴图半分辨率、关各向异性：沙地贴图要全分辨率、按贴图开各向异性
        var hq = P(qs, "m_QualitySettings").GetArrayElementAtIndex(0);
        hq.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0; hq.FindPropertyRelative("anisotropicTextures").intValue = 1;
        qs.ApplyModifiedPropertiesWithoutUndo();
    }

    // SSAO 只挂桌面 renderer（强度 0.8、半径 0.4 m：床、小屋、箱子、人贴地处压暗一圈）。URP 的 SSAO 类型是 internal：按类型名建，和 Inspector 的 Add Renderer Feature 一样补 m_RendererFeatureMap
    static void Ssao(RenderPipelineAsset high)
    {
        var data = (ScriptableRendererData)new SerializedObject(high).FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
        var type = System.Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime", true);
        var ao = data.rendererFeatures.FirstOrDefault(f => f != null && f.GetType() == type);
        if (ao == null)
        {
            ao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            ao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ao, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ao, out string _, out long id);
            Set(data, so => { Grow(P(so, "m_RendererFeatures")).objectReferenceValue = ao; Grow(P(so, "m_RendererFeatureMap")).longValue = id; });
        }
        Set(ao, so =>
        {
            P(so, "m_Settings.Intensity").floatValue = 0.8f; P(so, "m_Settings.Radius").floatValue = 0.4f;
            var sh = so.FindProperty("m_Shader");   // 老版本 URP 的 SSAO 自带 shader 引用：空着打包就没效果
            if (sh != null && sh.objectReferenceValue == null) sh.objectReferenceValue = Shader.Find("Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion");
        });
        data.SetDirty();
    }

    static SerializedProperty Grow(SerializedProperty list) { list.arraySize++; return list.GetArrayElementAtIndex(list.arraySize - 1); }

    static RenderPipelineAsset Urp(string n) => AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>($"{Settings}/{n}.asset");

    static void Set(Object o, System.Action<SerializedObject> f)
    {
        var so = new SerializedObject(o); f(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o);
    }

    static SerializedProperty P(SerializedObject so, string n) =>
        so.FindProperty(n) ?? throw new System.Exception($"missing serialized field {n} on {so.targetObject.GetType().Name}");

    public static void Build(Transform root, GameObject cabin, TimeOfDay tod, Camera cam)
    {
        TuneUrp(Urp("URP_High"), Urp("URP_Medium"));   // loop-22 闸不跑 ProjectSetup：搭场景时对现成的 URP 资产和 High 档重套一遍
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

        // loop-9 篝火：火苗上方一盏暖光（贴着沙地会照出一块白斑），同灯笼随时段 Glow 亮；loop-10 抬到帽顶以上 FireLampY、照 FireLampRange，夜里火边两顶帽子的颜色看得清。
        // 排在最后：网页端只亮前 WebLamps 盏（最近的灯笼 + 小屋窗），篝火没有点光源；网页端运行时也关了 Bloom（GameBootstrap），篝火在网页端只剩 M_glow 自发光
        lamps.Add(Lamp(root.Find("Campfire"), Vector3.up * Config.FireLampY, Config.FireLampRange));

        tod.glow = glow;
        tod.fire = root.Find("Campfire").GetComponentInChildren<Renderer>();   // loop-11 火苗运行时另用一份 M_glow（TimeOfDay.Start 换上），夜里单独压低
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
