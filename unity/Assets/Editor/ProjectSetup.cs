using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

// -executeMethod ProjectSetup.Run：两个 URP Asset + 两档 Quality + PlayerSettings（第 4 节）。
// URP 类型走反射：本文件不直接引用 URP 程序集，未装包时也能编译，缺字段时直接报错退出。
public static class ProjectSetup
{
    const string Dir = "Assets/Settings";
    const string Urp = "Unity.RenderPipelines.Universal.Runtime";

    public static void Run()
    {
        try { Setup(); Debug.Log("PROJECT-SETUP OK"); }
        catch (System.Exception e) { Debug.LogError(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void Setup()
    {
        Directory.CreateDirectory(Dir);
        var high = MakeUrp("URP_High", 4, 1f, true);
        var medium = MakeUrp("URP_Medium", 1, 0.8f, false);
        GraphicsSettings.defaultRenderPipeline = high;
        BuildFx.TuneUrp(high, medium);

        var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = Prop(qs, "m_QualitySettings");
        levels.arraySize = 2;
        Level(levels.GetArrayElementAtIndex(0), "High", high, 4); Level(levels.GetArrayElementAtIndex(1), "Medium", medium, 0);
        Prop(qs, "m_CurrentQuality").intValue = 0;
        var per = Prop(qs, "m_PerPlatformDefaultQuality");
        bool web = false;
        for (int i = 0; i < per.arraySize; i++)
        {
            var e = per.GetArrayElementAtIndex(i);
            bool isWeb = e.FindPropertyRelative("first").stringValue == "WebGL";
            e.FindPropertyRelative("second").intValue = isWeb ? 1 : 0;
            web |= isWeb;
        }
        if (!web)
        {
            per.arraySize++;
            var e = per.GetArrayElementAtIndex(per.arraySize - 1);
            e.FindPropertyRelative("first").stringValue = "WebGL"; e.FindPropertyRelative("second").intValue = 1;
        }
        qs.ApplyModifiedPropertiesWithoutUndo();

        PlayerSettings.companyName = "Warm"; PlayerSettings.productName = "Warm";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true; PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli; PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false); PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });   // = WebGL 2.0
        var ps = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
        Prop(ps, "activeInputHandler").intValue = 0;   // 旧 Input Manager
        ps.ApplyModifiedPropertiesWithoutUndo();

        foreach (var fbx in Directory.GetFiles("Assets/Art", "*.fbx"))
            AssetDatabase.ImportAsset(fbx.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);   // 让下面的导入规则生效
        AssetDatabase.SaveAssets();
    }

    static RenderPipelineAsset MakeUrp(string name, int msaa, float scale, bool shadows)
    {
        var rendererType = System.Type.GetType($"UnityEngine.Rendering.Universal.UniversalRendererData, {Urp}", true);
        var assetType = System.Type.GetType($"UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, {Urp}", true);
        var renderer = ScriptableObject.CreateInstance(rendererType);
        AssetDatabase.CreateAsset(renderer, $"{Dir}/{name}_Renderer.asset");
        var create = assetType.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == "Create" && m.GetParameters().Length == 1);
        var asset = (RenderPipelineAsset)create.Invoke(null, new object[] { renderer });
        AssetDatabase.CreateAsset(asset, $"{Dir}/{name}.asset");
        var so = new SerializedObject(asset);
        Prop(so, "m_MSAA").intValue = msaa;
        Prop(so, "m_RenderScale").floatValue = scale;
        Prop(so, "m_MainLightShadowsSupported").boolValue = shadows;
        so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    static void Level(SerializedProperty l, string name, RenderPipelineAsset rp, int msaa)
    {
        l.FindPropertyRelative("name").stringValue = name; l.FindPropertyRelative("customRenderPipeline").objectReferenceValue = rp;
        l.FindPropertyRelative("antiAliasing").intValue = msaa; l.FindPropertyRelative("vSyncCount").intValue = 1;
    }

    static SerializedProperty Prop(SerializedObject so, string name) =>
        so.FindProperty(name) ?? throw new System.Exception($"missing serialized field {name} on {so.targetObject.GetType().Name}");

    // 角色 Animator：idle/walk 两态，Speed>0.5 走路。
    public static AnimatorController MakeAnimator()
    {
        const string fbx = "Assets/Art/SK_character.fbx";
        var ac = AnimatorController.CreateAnimatorControllerAtPath("Assets/Art/AC_character.controller");
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        var sm = ac.layers[0].stateMachine;
        var idle = sm.AddState("idle"); idle.motion = clips.FirstOrDefault(c => c.name == "AN_idle");
        var walk = sm.AddState("walk"); walk.motion = clips.FirstOrDefault(c => c.name == "AN_walk");
        sm.defaultState = idle;
        var go = idle.AddTransition(walk); go.hasExitTime = false; go.duration = 0.1f; go.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
        var back = walk.AddTransition(idle); back.hasExitTime = false; back.duration = 0.1f; back.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
        return ac;
    }
}

// FBX：轴向烘焙（decisions.md：Blender 侧不 bake_space_transform，Unity 侧必须开）、1 单位=1m；
// 动画全部循环、根节点 XZ 位移烘进姿势；贴图 WebGL 覆盖 1024 + DXT。
public class WarmImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        var mi = (ModelImporter)assetImporter;
        mi.bakeAxisConversion = true;
        mi.useFileScale = true;
        mi.globalScale = 1f;
        bool skinned = Path.GetFileName(assetPath).StartsWith("SK_");
        mi.animationType = skinned ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        mi.importAnimation = skinned;
    }

    void OnPreprocessAnimation()
    {
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = true;
            c.lockRootPositionXZ = true;
            string n = c.name.ToLower();
            c.name = n.Contains("idle") ? "AN_idle" : n.Contains("walk") ? "AN_walk" : c.name;
        }
        mi.clipAnimations = clips;
    }

    void OnPreprocessTexture()
    {
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = Mathf.Min(ti.maxTextureSize, 1024);
        ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            { name = "WebGL", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.DXT5 });
    }
}
