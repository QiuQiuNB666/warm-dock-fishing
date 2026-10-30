using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// -executeMethod BuildScene.Build → Assets/Scenes/Main.unity。坐标/颜色全部取自 Config。
public static class BuildScene
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    const string Art = "Assets/Art/";

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
        water.SetColor("_Color", Config.Sea); water.SetFloat("_WaveAmp", Config.WaveAmp); water.SetFloat("_WaveLen", Config.WaveLen); water.SetFloat("_WaveSpeed", Config.WaveSpeed);
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
        for (int i = 0; i < Config.PlankCount; i++)
            BuildNature.Box("Plank", dock, new Vector3(0f, Config.GroundY - 0.1f, (i + 0.5f) * Config.PlankDepth), new Vector3(Config.DockWidth, 0.2f, Config.PlankDepth - Config.PlankGap), wood);
        for (float z = 2f; z < Config.DockLength; z += 4f)
            foreach (float x in new[] { -1f, 1f })
                BuildNature.Box("Post", dock, new Vector3(x * Config.DockWidth * 0.5f, (Config.GroundY + Config.SeabedY) * 0.5f, z), new Vector3(0.25f, Config.GroundY - Config.SeabedY, 0.25f), woodDark);
        new GameObject("FishSpot").transform.position = Config.FishSpot;
        foreach (var p in Config.CratePos) BuildNature.Box("Crate", dock, p + Vector3.up * 0.4f, Vector3.one * 0.8f, woodDark);
        foreach (var p in Config.BarrelPos) Prim(PrimitiveType.Cylinder, "Barrel", dock, p + Vector3.up * 0.5f, new Vector3(0.6f, 0.5f, 0.6f), wood);
        var ladder = new GameObject("Ladder").transform;
        ladder.SetParent(dock, false);
        for (int i = 0; i < 4; i++) BuildNature.Box("Rung", ladder, Config.LadderPos + Vector3.up * (i * 0.3f - 0.2f), new Vector3(0.1f, 0.06f, 0.8f), woodDark);

        // 模型（缺了就用方块占位，场景照样能搭）
        var boat = Model("SM_boat", root, Config.BoatPos, new Vector3(1.5f, 0.6f, 3.5f), wood);
        boat.AddComponent<Floater>();
        var cabin = Model("SM_cabin", root, Config.CabinPos, new Vector3(4f, 3f, 4f), woodDark);
        for (int i = 0; i < Config.RockPos.Length; i++)
            Model("SM_rock", root, Config.RockPos[i], Vector3.one, BuildNature.Mat("M_cliff", Config.Cliff)).transform.localScale = Vector3.one * Config.RockScale[i];

        // 床：小屋门前的沙滩上，摆在两名玩家身后（两人出生在 BedPos 床边）
        var bed = new GameObject("Bed").transform;
        bed.SetParent(root, false);
        bed.position = Config.BedPos + Config.BedFromSpawn;
        BuildNature.Box("BedFrame", bed, bed.position + Vector3.up * Config.BedSize.y * 0.5f, Config.BedSize, woodDark);
        BuildNature.Box("Bedding", bed, bed.position + Vector3.up * (Config.BedSize.y + 0.05f), new Vector3(Config.BedSize.x - 0.1f, 0.1f, Config.BedSize.z - 0.1f), BuildNature.Mat("M_bedding", Config.Bedding));

        // 角色 + 钓具：Player 与 Player2（返回 Player 的钓鱼状态机）
        var fishing = BuildCharacter.Build(root, wood, woodDark);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        cam.GetComponent<Camera>().fieldOfView = Config.CamFov;
        cam.AddComponent<TopDownCamera>().target = fishing.transform;
        TopDownCamera.Place(cam.transform, Config.DockMid);

        var game = new GameObject("Game");
        game.AddComponent<GameBootstrap>();
        var tod = game.AddComponent<TimeOfDay>(); tod.sun = sun; tod.water = water;
        BuildFx.Build(root, cabin, tod, cam.GetComponent<Camera>());
        game.AddComponent<BaitSelector>().fishing = fishing;
        game.AddComponent<PlayerManager>();

        BuildNature.Build(root, water);
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
        return g;
    }
}
