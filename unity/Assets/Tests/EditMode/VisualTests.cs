using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static BuildSceneTests;

// loop-22 画面断言：沙地贴图、湿沙带和泡沫环薄片、URP 阴影与 SSAO。同样打开 BuildScene 产出的 Main.unity，常量取自 Config，All 用 BuildSceneTests 的。
public class VisualTests
{
    const string SandTex = "Assets/Art/Tex/T_sand.png";   // BuildShore.SandPath（测试程序集引用不到 Editor 脚本）

    [OneTimeSetUp] public void Open() => new BuildSceneTests().Open();

    static Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/{n}.mat");
    static SerializedObject Asset(string n) => new SerializedObject(AssetDatabase.LoadMainAssetAtPath($"Assets/Settings/{n}.asset"));

    // 沙地贴图在，M_sand、M_seabed 都用它；可平铺、带 mipmap、各向异性 4–8、网页最多 512
    [Test] public void SandTextureUsed()
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(SandTex);
        Assert.IsNotNull(t, SandTex);
        foreach (var n in new[] { "M_sand", "M_seabed" }) Assert.AreSame(t, Mat(n).GetTexture("_BaseMap"), n);
        var ti = (TextureImporter)AssetImporter.GetAtPath(SandTex);
        Assert.AreEqual(TextureWrapMode.Repeat, ti.wrapMode); Assert.IsTrue(ti.mipmapEnabled);
        Assert.That(ti.anisoLevel, Is.InRange(4, 8));
        Assert.LessOrEqual(ti.GetPlatformTextureSettings("WebGL").maxTextureSize, 512);
    }

    // 贴图平均色乘 M_sand 的 _BaseColor ≈ Config.Sand（每通道差 ≤3%）：贴图只加明暗细节，不改沙子的颜色
    [Test] public void SandTextureKeepsColor()
    {
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(SandTex));
        var px = t.GetPixels();
        var c = Mat("M_sand").GetColor("_BaseColor") * new Color(px.Average(p => p.r), px.Average(p => p.g), px.Average(p => p.b));
        Object.DestroyImmediate(t);
        for (int i = 0; i < 3; i++) Assert.AreEqual(Config.Sand[i], c[i], Config.Sand[i] * 0.03f, $"通道 {i}");
    }

    // 湿沙带和泡沫环只是薄片：一个碰撞体都没有（不进可走判定、不挡路）；湿沙一片，码头每根桩、每块礁石各一圈泡沫
    [Test] public void SheetsHaveNoCollider()
    {
        var sheets = All().Where(g => g.name == "WetSand" || g.name == "SandFoam" || g.name == "Foam").ToArray();
        Assert.AreEqual(1, sheets.Count(g => g.name == "WetSand"));
        Assert.AreEqual(1, sheets.Count(g => g.name == "SandFoam"));
        Assert.AreEqual(All().Count(g => g.name == "Post") + Config.RockPos.Length, sheets.Count(g => g.name == "Foam"));
        foreach (var g in sheets)
        {
            Assert.AreEqual(0, g.GetComponentsInChildren<Collider>(true).Length, g.name);
            Assert.IsNotNull(g.GetComponent<MeshFilter>().sharedMesh, g.name);
        }
    }

    // 桌面 URP_High：阴影 4096、renderer 上挂着 SSAO；网页 URP_Medium 的 renderer 没有 SSAO
    [Test] public void UrpShadowsAndSsao()
    {
        Assert.AreEqual(4096, Asset("URP_High").FindProperty("m_MainLightShadowmapResolution").intValue);
        Assert.IsTrue(HasSsao("URP_High_Renderer"), "桌面有 SSAO");
        Assert.IsFalse(HasSsao("URP_Medium_Renderer"), "网页没有 SSAO");
    }

    static bool HasSsao(string renderer)
    {
        var list = Asset(renderer).FindProperty("m_RendererFeatures");
        return Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue is Object o && o != null && o.GetType().Name == "ScreenSpaceAmbientOcclusion");
    }
}
