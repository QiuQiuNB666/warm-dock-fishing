using UnityEditor;
using UnityEngine;

// 海滩、低多边形树群（圆锥+圆柱）、河流、瀑布，全部原生几何体；网格存为资源以便场景引用。
public static class BuildNature
{
    public const string GenDir = "Assets/Art/Generated";

    public static void Build(Transform root, Material waterMat)
    {
        var nature = new GameObject("Nature").transform;
        nature.SetParent(root, false);
        var sand = Mat("M_sand", Config.Sand);

        // 沙滩台地：顶面 = GroundY，与码头同高
        var b = Config.Beach;
        Box("Beach", nature, new Vector3(b.center.x, Config.GroundY - 1.5f, b.center.y), new Vector3(b.width + 10f, 3f, b.height + 4f), sand);

        // 树群：固定种子，避开码头入口与河道
        var cone = SaveMesh(Cone(7, 1f, 1f), "cone");
        var leaf = Mat("M_leaf", Config.Leaf);
        var trunk = Mat("M_trunk", Config.Trunk);
        var rng = new System.Random(Config.TreeSeed);
        for (int i = 0, placed = 0; placed < Config.TreeCount && i < 500; i++)
        {
            var p = new Vector3(Mathf.Lerp(b.xMin, b.xMax, (float)rng.NextDouble()), Config.GroundY, Mathf.Lerp(b.yMin, b.yMin + 9f, (float)rng.NextDouble()));
            if (Mathf.Abs(p.x) < 4f || Mathf.Abs(p.x - Config.RiverStart.x) < Config.RiverWidth + 1f || (p - Config.CabinPos).magnitude < 4f) continue;
            float s = 0.8f + (float)rng.NextDouble() * 0.6f;
            var tree = new GameObject("Tree").transform;
            tree.SetParent(nature, false);
            tree.position = p;
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Trunk"; t.transform.SetParent(tree, false);
            t.transform.localPosition = new Vector3(0f, 0.6f * s, 0f);
            t.transform.localScale = new Vector3(0.25f * s, 0.6f * s, 0.25f * s);
            t.GetComponent<Renderer>().sharedMaterial = trunk;
            for (int k = 0; k < 2; k++)
            {
                var c = new GameObject("Leaves", typeof(MeshFilter), typeof(MeshRenderer));
                c.transform.SetParent(tree, false);
                c.transform.localPosition = new Vector3(0f, (1.1f + k * 0.9f) * s, 0f);
                c.transform.localScale = new Vector3(1.4f - k * 0.4f, 1.6f, 1.4f - k * 0.4f) * s;
                c.GetComponent<MeshFilter>().sharedMesh = cone;
                c.GetComponent<MeshRenderer>().sharedMaterial = leaf;
            }
            placed++;
        }

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
        m.color = c;
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

    // 低多边形圆锥：底面半径 r、高 h，硬边（每面独立顶点）。
    public static Mesh Cone(int sides, float r, float h)
    {
        var v = new Vector3[sides * 6];
        var tri = new int[sides * 6];
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            int k = i * 6;
            v[k] = p0; v[k + 1] = Vector3.up * h; v[k + 2] = p1;   // 侧面
            v[k + 3] = p0; v[k + 4] = p1; v[k + 5] = Vector3.zero;  // 底面
        }
        for (int i = 0; i < tri.Length; i++) tri[i] = i;
        var m = new Mesh { name = "Cone", vertices = v, triangles = tri };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
