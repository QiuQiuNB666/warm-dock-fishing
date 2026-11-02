using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// -executeMethod BuildScene.Build → Assets/Scenes/Main.unity。坐标/颜色全部取自 Config。
public static class BuildScene
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    const string Art = "Assets/Art/";
    public const string CjkFont = "Assets/Fonts/WarmCJK-Regular.otf";   // 思源黑体子集（WarmCJK）：中文 OnGUI 文本都用这一份

    public static void Build()
    {
        try { Make(); }
        catch (System.Exception e) { Debug.LogError(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void Make()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        System.IO.Directory.CreateDirectory(Art + "Materials");
        var root = new GameObject("World").transform;
        var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 海面 + 海底
        var water = new Material(Shader.Find("Warm/Water"));
        water.SetColor("_Color", Config.Sea); water.SetFloat("_WaveAmp", Config.WaveAmp); water.SetFloat("_WaveLen", Config.WaveLen); water.SetFloat("_WaveSpeed", Config.WaveSpeed); water.SetFloat("_Foam", 1f);   // loop-22 岸边泡沫线
        AssetDatabase.CreateAsset(water, Art + "Materials/M_water.mat");
        var sea = new GameObject("Sea", typeof(MeshFilter), typeof(MeshRenderer));
        sea.transform.SetParent(root, false);
        sea.transform.position = new Vector3(0f, Config.SeaY, Config.SeaSize.y * 0.5f);
        sea.GetComponent<MeshFilter>().sharedMesh = BuildNature.SaveMesh(BuildNature.Grid(Config.SeaSize.x, Config.SeaSize.y, 80, 60), "sea");
        sea.GetComponent<MeshRenderer>().sharedMaterial = water;
        BuildNature.Box("Seabed", root, new Vector3(0f, Config.SeabedY - 0.5f, Config.SeaSize.y * 0.5f), new Vector3(Config.SeaSize.x, 1f, Config.SeaSize.y), BuildNature.Mat("M_seabed", Config.Seabed));
        Material coral = BuildNature.Mat("M_coral", Config.Coral), weed = BuildNature.Mat("M_weed", Config.Weed);
        var rng = new System.Random(3);
        for (int i = 0; i < 24; i++)
        {
            var p = new Vector3(Mathf.Lerp(-20f, 20f, (float)rng.NextDouble()), Config.SeabedY, Mathf.Lerp(3f, 35f, (float)rng.NextDouble()));
            bool isCoral = i % 2 == 0;
            var g = GameObject.CreatePrimitive(isCoral ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            g.name = isCoral ? "Coral" : "Weed";
            g.transform.SetParent(root, false);
            g.transform.position = p + Vector3.up * (isCoral ? 0.3f : 0.8f);
            g.transform.localScale = isCoral ? new Vector3(1.2f, 0.7f, 1f) : new Vector3(0.15f, 0.8f, 0.15f);
            g.GetComponent<Renderer>().sharedMaterial = isCoral ? coral : weed;
        }
        var fishMat = BuildNature.Mat("M_fish", Config.Fish);
        foreach (var c in new[] { new Vector3(-5f, Config.SeabedY + 1.2f, 12f), new Vector3(6f, Config.SeabedY + 1.5f, 22f) })
        {
            var school = new GameObject("FishSchool", typeof(FishSchool));
            school.transform.SetParent(root, false); school.transform.position = c;
            school.GetComponent<FishSchool>().material = fishMat;
        }

        // 码头：沿 +Z 一块块木板，可纵向拼接
        var dock = new GameObject("Dock").transform;
        dock.SetParent(root, false);
        var wood = BuildNature.Mat("M_wood", Config.Wood);
        var woodDark = BuildNature.Mat("M_wood_dark", Config.WoodDark);
        for (int i = 0; i < Config.PlankCount; i++)   // 低模板：顶面木色、侧面深木色（板数、尺寸不变，侧面在板缝里露出来）
            BuildProps.Solid(Model("SM_plank", dock, new Vector3(0f, Config.GroundY - 0.2f, (i + 0.5f) * Config.PlankDepth), new Vector3(Config.DockWidth, 0.2f, Config.PlankDepth - Config.PlankGap), wood)).name = "Plank";
        for (float z = 2f; z < Config.DockLength; z += 4f)
            foreach (float x in new[] { -1f, 1f })
                BuildNature.Box("Post", dock, new Vector3(x * Config.DockWidth * 0.5f, (Config.GroundY + Config.SeabedY) * 0.5f, z), new Vector3(0.25f, Config.GroundY - Config.SeabedY, 0.25f), woodDark);
        new GameObject("FishSpot").transform.position = Config.FishSpot;
        var cabin = BuildProps.Build(root, dock, wood, woodDark);   // 箱、桶、梯、船、小屋、岩石、床（loop-5 拆到 BuildProps，顺序不变）

        // 角色 + 钓具：Player 与 Player2（返回 Player 的钓鱼状态机）
        var fishing = BuildCharacter.Build(root, wood, woodDark);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        cam.GetComponent<Camera>().fieldOfView = Config.CamFov;
        var follow = cam.AddComponent<TopDownCamera>();
        follow.target = fishing.transform; follow.target2 = GameObject.Find("Player2").transform;   // 双人：看两人中点、按间距拉远
        TopDownCamera.Place(cam.transform, Config.DockMid);

        var game = new GameObject("Game");
        game.AddComponent<GameBootstrap>();
        var tod = game.AddComponent<TimeOfDay>(); tod.sun = sun; tod.water = water;
        BuildFx.Build(root, cabin, tod, cam.GetComponent<Camera>());
        game.AddComponent<BaitSelector>().fishing = fishing;
        var fishing2 = GameObject.Find("Player2").GetComponent<FishingStateMachine>();   // P2 的饵挂在 Player2 身上：单人模式随它一起关掉
        var bait2 = fishing2.gameObject.AddComponent<BaitSelector>();
        bait2.fishing = fishing2; bait2.owner = 1;
        game.AddComponent<PlayerManager>();
        game.AddComponent<PierSave>();   // 开局读档（没档/坏档新开），两人站到床边
        var cjk = AssetDatabase.LoadAssetAtPath<Font>(CjkFont);   // 暂停菜单、底部提示
        var bricks = game.AddComponent<Bricks>(); bricks.material = BuildNature.Mat("M_brick", Config.BrickColor); bricks.glow = tod.glow; bricks.font = cjk; bricks.cabin = cabin;   // 砖：门口 E 拿鱼换、B 放；小屋没碰撞体，挡砖另给
        bricks.ghost = Prim(PrimitiveType.Cube, "BrickGhost", game.transform, Vector3.zero, new Vector3(Config.GhostSide, Config.GhostThick, Config.GhostSide), GameObject.Find("ShadowDisc").GetComponent<Renderer>().sharedMaterial);   // loop-20 放砖预览的样板：半透明 M_shadow（网页圆片影子那份，现成）
        Object.DestroyImmediate(bricks.ghost.GetComponent<Collider>()); bricks.ghost.SetActive(false);   // 碰撞体在编辑器里就删（会挡人、会被 Bricks.Start 收进挡砖表），运行时一个都没有；藏着，Bricks 每人复制一份
        game.AddComponent<Pause>().font = cjk;
        game.AddComponent<Hints>().font = cjk;   // 头顶就近提示（loop-3）
        game.AddComponent<Hug>();   // 两人凑近按 H 抱一下（loop-13）

        BuildNature.Build(root, water);
        BuildShore.Build(root);   // loop-22 沙地贴图、岸边湿沙带、码头桩和礁石入水处的泡沫环
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("BUILD-SCENE OK");
    }

    public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false);
        g.transform.position = pos; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    public static GameObject Model(string name, Transform parent, Vector3 pos, Vector3 fallbackSize, Material fallback)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx");
        if (fbx == null) return BuildNature.Box(name, parent, pos + Vector3.up * fallbackSize.y * 0.5f, fallbackSize, fallback);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
        g.transform.position = pos;
        foreach (var r in g.GetComponentsInChildren<Renderer>())   // 模型材质按名字换成 Materials/ 下同名的 Config 色材质（M_wood、M_glow…），颜色只认 Config
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && AssetDatabase.LoadAssetAtPath<Material>($"{Art}Materials/{m.name}.mat") is Material s ? s : m).ToArray();
        return g;
    }
}
