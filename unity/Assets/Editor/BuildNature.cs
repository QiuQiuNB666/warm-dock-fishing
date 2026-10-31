using UnityEditor;
using UnityEngine;

// 海滩、低多边形树群（blender/model.py 的 SM_tree）、河流、瀑布；生成的网格存为资源以便场景引用。
public static class BuildNature
{
    public const string GenDir = "Assets/Art/Generated";
    public const float Matte = 0.1f;   // 光滑度 0.1 = 粗糙度 0.9：俯视下木头、石头是干的，不发亮

    public static void Build(Transform root, Material waterMat)
    {
        var nature = new GameObject("Nature").transform;
        nature.SetParent(root, false);
        var sand = Mat("M_sand", Config.Sand);

        // 沙滩台地：顶面 = GroundY，与码头同高
        var b = Config.Beach;
        Box("Beach", nature, new Vector3(b.center.x, Config.GroundY - 1.5f, b.center.y), new Vector3(b.width + 10f, 3f, b.height + 4f), sand);

        // 树群：固定种子，避开码头入口与河道。model.py 的低模树；随机数抽取顺序不变，位置、大小和原来一样，朝向按位置算
        var leaf = Mat("M_leaf", Config.Leaf);
        Mat("M_trunk", Config.Trunk);
        var rng = new System.Random(Config.TreeSeed);
        for (int i = 0, placed = 0; placed < Config.TreeCount && i < 500; i++)
        {
            var p = new Vector3(Mathf.Lerp(b.xMin, b.xMax, (float)rng.NextDouble()), Config.GroundY, Mathf.Lerp(b.yMin, b.yMin + 9f, (float)rng.NextDouble()));
            if (Mathf.Abs(p.x) < 4f || Mathf.Abs(p.x - Config.RiverStart.x) < Config.RiverWidth + 1f || (p - Config.CabinPos).magnitude < 4f) continue;
            float s = 0.8f + (float)rng.NextDouble() * 0.6f;
            var tree = BuildScene.Model("SM_tree", nature, p, new Vector3(2.6f, 3.6f, 2.6f) * s, leaf);
            tree.name = "Tree";
            tree.transform.localScale = Vector3.one * s;
            tree.transform.rotation = Quaternion.Euler(0f, (p.x * 53f + p.z * 31f) % 360f, 0f);
            var trunk = tree.AddComponent<BoxCollider>();   // 只挡树干，和原来的圆柱一样粗
            trunk.center = Vector3.up * 0.6f; trunk.size = new Vector3(0.25f, 1.2f, 0.25f);
            placed++;
        }
        nature.gameObject.AddComponent<Trees>();   // 砍树（E；门口有鱼先换砖）：上面的生成顺序就是树号

        // 河流：从崖底流到海岸线
        var r = Config.RiverStart;
        var river = new GameObject("River", typeof(MeshFilter), typeof(MeshRenderer));
        river.transform.SetParent(nature, false);
        river.transform.position = new Vector3(r.x, Config.GroundY + 0.02f, r.z * 0.5f);
        river.GetComponent<MeshFilter>().sharedMesh = SaveMesh(Grid(Config.RiverWidth, -r.z, 4, 24), "river");
        river.GetComponent<MeshRenderer>().sharedMaterial = waterMat;

        // 瀑布：45° 斜崖 + 贴面水流（相机朝 +Z 俯视，竖直崖面看不见；斜面法线朝上偏 +Z 才在画面里）
        var cl = Config.CliffPos;
        float len = Config.CliffHeight * 1.4142f;
        var slope = Quaternion.Euler(45f, 0f, 0f);
        var mid = cl + Vector3.up * (Config.CliffHeight * 0.5f);
        Box("Cliff", nature, mid, new Vector3(10f, 1f, len), Mat("M_cliff", Config.Cliff)).transform.rotation = slope;
        var fall = new GameObject("Waterfall", typeof(MeshFilter), typeof(MeshRenderer));
        fall.transform.SetParent(nature, false);
        fall.transform.position = mid + slope * Vector3.up * 0.55f;
        fall.transform.rotation = slope;
        fall.GetComponent<MeshFilter>().sharedMesh = SaveMesh(Grid(Config.RiverWidth, len, 4, 16), "waterfall");
        fall.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
    }

    public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    public static Material Mat(string name, Color c)
    {
        string path = $"Assets/Art/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh);
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.color = c; m.SetFloat("_Smoothness", Matte);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Mesh SaveMesh(Mesh mesh, string name)
    {
        System.IO.Directory.CreateDirectory(GenDir);
        string path = $"{GenDir}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // 平铺网格（XZ 平面，中心在原点），水面顶点动画需要足够细分。
    public static Mesh Grid(float w, float d, int nx, int nz)
    {
        var v = new Vector3[(nx + 1) * (nz + 1)];
        var tri = new int[nx * nz * 6];
        for (int z = 0, i = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++, i++)
                v[i] = new Vector3((x / (float)nx - 0.5f) * w, 0f, (z / (float)nz - 0.5f) * d);
        for (int z = 0, t = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x, c = a + nx + 1;
                tri[t++] = a; tri[t++] = c; tri[t++] = a + 1;
                tri[t++] = a + 1; tri[t++] = c; tri[t++] = c + 1;
            }
        var m = new Mesh { name = "Grid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, triangles = tri };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
