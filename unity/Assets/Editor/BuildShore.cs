using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// loop-22 画面①：沙地程序贴图（M_sand、M_seabed 共用一张）、岸边湿沙带、沙上泡沫、码头桩和礁石入水处的泡沫环。
// 湿沙和泡沫环都走 Warm/Water（_Mode 2 乘法湿沙 / _Mode 1 泡沫环）；薄片只有 MeshFilter + MeshRenderer，不带碰撞体：不进可走判定、不挡路。
public static class BuildShore
{
    public const string SandPath = "Assets/Art/Tex/T_sand.png";
    const int N = 512;

    public static void Build(Transform root)
    {
        var tex = SandTex();
        var beach = GameObject.Find("Beach").transform;
        Tile(BuildNature.Mat("M_sand", Config.Sand), tex, new Vector2(beach.localScale.x, beach.localScale.z) / Config.SandTile);   // 按沙滩方块尺寸，约 4 m 一块
        var bed = GameObject.Find("Seabed").transform.localScale;
        Tile(BuildNature.Mat("M_seabed", Config.Seabed), tex, new Vector2(bed.x, bed.z) / Config.SeabedTile);   // 水下看不清细颗粒，块大一点

        var shore = new GameObject("Shore").transform;
        shore.SetParent(root, false);
        var wet = WaterMat("M_wetsand", 2f, 0f);
        wet.SetColor("_Color", Config.WetSand); wet.SetFloat("_WetW", Config.WetWidth);
        wet.SetFloat("_SrcBlend", (float)BlendMode.DstColor); wet.SetFloat("_DstBlend", (float)BlendMode.Zero);   // 乘在干沙上：影子、AO 原样留着
        wet.renderQueue = (int)RenderQueue.Transparent - 2;   // 地面这几层的画序：湿沙 → 沙上泡沫 → 落砖预览、网页圆片影子（M_shadow，Transparent）、河（l22f1p2：泡沫画在预览之后，把方块上半截盖白）
        Sheet("WetSand", shore, BuildNature.SaveMesh(BuildNature.Grid(beach.localScale.x, Config.WetWidth, 1, 1), "wetsand"),
            new Vector3(beach.position.x, Config.GroundY + 0.005f, -Config.WetWidth * 0.5f), wet);   // 贴着沙面，北沿正好在海岸线 z = 0
        var sandFoam = WaterMat("M_sandfoam", 3f, 0f);   // 沙上泡沫（_Mode 3）：浪漫过台沿压到沙上，看得见的岸线是泡沫碎开的内沿，不再是台沿那条直线（l22p2）
        sandFoam.renderQueue = (int)RenderQueue.Transparent - 1;   // 湿沙之后（不被乘暗）、预览和影子圆片之前；人影由 shader 采主光阴影（FoamLit）
        Sheet("SandFoam", shore, BuildNature.SaveMesh(BuildNature.Grid(beach.localScale.x, Config.SandFoamW, 1, 1), "sandfoam"),
            new Vector3(beach.position.x, Config.GroundY + 0.006f, -Config.SandFoamW * 0.5f), sandFoam);

        var foam = WaterMat("M_foam", 1f, Config.WaveAmp);   // 顶点走同一条波形：环跟着浪起伏
        foam.renderQueue = (int)RenderQueue.Transparent + 1;   // 画在海面之后
        var post = BuildNature.SaveMesh(Ring(0.12f, 0.12f + Config.FoamRing), "foam_post");   // 桩 0.25 m 见方，内沿藏在桩里
        foreach (var p in Named(root, "Post")) Sheet("Foam", shore, post, new Vector3(p.position.x, Config.SeaY + 0.01f, p.position.z), foam);
        int k = 0;
        foreach (var r in Named(root, "SM_rock"))
        {
            var b = r.GetComponentInChildren<Renderer>().bounds;
            float rad = (b.extents.x + b.extents.z) * 0.5f;   // 礁石底座按外接盒的平均半径，内沿收进石头底下
            Sheet("Foam", shore, BuildNature.SaveMesh(Ring(rad * 0.7f, rad + Config.FoamRing * 1.5f), $"foam_rock{k++}"), new Vector3(b.center.x, Config.SeaY + 0.01f, b.center.z), foam);
        }
    }

    static Transform[] Named(Transform root, string n) => root.GetComponentsInChildren<Transform>(true).Where(t => t.name == n).ToArray();

    static void Tile(Material m, Texture2D t, Vector2 tiling)
    {
        m.SetTexture("_BaseMap", t); m.SetTextureScale("_BaseMap", tiling);
        EditorUtility.SetDirty(m);
    }

    static void Sheet(string name, Transform parent, Mesh mesh, Vector3 pos, Material m)
    {
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));   // 不用 CreatePrimitive：图元自带碰撞体
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Material WaterMat(string name, float mode, float amp)
    {
        string path = $"Assets/Art/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Warm/Water")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Mode", mode); m.SetFloat("_WaveAmp", amp); m.SetFloat("_WaveLen", Config.WaveLen); m.SetFloat("_WaveSpeed", Config.WaveSpeed);
        m.SetFloat("_Lift", 0f); m.SetFloat("_Spec", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 圆环薄片（XZ 平面，中心在原点，正面朝上）：uv.x 0 = 内沿、1 = 外沿
    static Mesh Ring(float rIn, float rOut)
    {
        const int Seg = 24;
        var v = new Vector3[Seg * 2 + 2]; var uv = new Vector2[v.Length]; var tri = new int[Seg * 6];
        for (int i = 0; i <= Seg; i++)
        {
            var d = new Vector3(Mathf.Cos(i * Mathf.PI * 2f / Seg), 0f, Mathf.Sin(i * Mathf.PI * 2f / Seg));
            v[i * 2] = d * rIn; v[i * 2 + 1] = d * rOut;
            uv[i * 2] = new Vector2(0f, i / (float)Seg); uv[i * 2 + 1] = new Vector2(1f, i / (float)Seg);
            if (i == Seg) break;
            int b = i * 2, t = i * 6;
            tri[t] = b; tri[t + 1] = b + 2; tri[t + 2] = b + 1; tri[t + 3] = b + 2; tri[t + 4] = b + 3; tri[t + 5] = b + 1;
        }
        var m = new Mesh { name = "Ring", vertices = v, uv = uv, triangles = tri };
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 512² 可无缝平铺的沙地细节（固定种子）：低频明暗斑块约 ±4%、细颗粒和零星暗点、一片片淡淡的斜向风纹（相位被噪声扭弯）。
    // 近白的中性灰（暗斑略偏暖），均值约 0.98：乘 _BaseColor = Config.Sand 后颜色基本不变（VisualTests 卡 3%）
    static Texture2D SandTex()
    {
        var rng = new System.Random(Config.SandSeed);
        float[,] l4 = Lat(rng, 4), l8 = Lat(rng, 8), l3 = Lat(rng, 3), l5 = Lat(rng, 5);
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float blot = 0.6f * Noise(l4, u, v) + 0.4f * Noise(l8, u, v);
                float wind = Mathf.Clamp01(Noise(l3, u, v) * 2f - 0.2f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (13f * u + 8f * v) + 2.5f * Noise(l5, u, v)));   // 平滑正弦：l22p1 取 4 次幂成了细暗线，沙上像刷痕、隔着水看海底像划痕
                float g = 0.985f + 0.05f * blot + 0.04f * ((float)rng.NextDouble() - 0.5f) - 0.012f * wind - (rng.NextDouble() < 0.012 ? 0.05f : 0f);
                byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f), b = (byte)Mathf.RoundToInt(Mathf.Clamp01(g - 0.02f * Mathf.Max(0f, -blot)) * 255f);
                px[y * N + x] = new Color32(c, c, b, 255);
            }
        var t = new Texture2D(N, N, TextureFormat.RGB24, false);
        t.SetPixels32(px);
        Directory.CreateDirectory(Path.GetDirectoryName(SandPath));
        File.WriteAllBytes(SandPath, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(SandPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(SandPath);
        ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8;
        ti.maxTextureSize = N; ti.textureCompression = TextureImporterCompression.CompressedHQ;   // 桌面 BC7：±4% 的细节经不起 DXT1 的 565 量化
        ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "WebGL", overridden = true, maxTextureSize = N, format = TextureImporterFormat.DXT5 });
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SandPath);
    }

    static float[,] Lat(System.Random r, int p)
    {
        var a = new float[p, p];
        for (int i = 0; i < p; i++) for (int j = 0; j < p; j++) a[i, j] = (float)r.NextDouble() * 2f - 1f;
        return a;
    }

    static float Noise(float[,] a, float u, float v)   // 周期值噪声 -1..1：u、v ∈ [0,1) 正好一个周期，贴图首尾接得上
    {
        int p = a.GetLength(0), i = (int)(u * p), j = (int)(v * p);
        float fx = u * p - i, fy = v * p - j;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int i1 = (i + 1) % p, j1 = (j + 1) % p;
        return Mathf.Lerp(Mathf.Lerp(a[i, j], a[i1, j], fx), Mathf.Lerp(a[i, j1], a[i1, j1], fx), fy);
    }
}
