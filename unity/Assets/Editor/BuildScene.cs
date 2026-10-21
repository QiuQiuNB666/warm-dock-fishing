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

        // Tripo 模型（缺了就用方块占位，场景照样能搭）
        var boat = Model("SM_boat", root, Config.BoatPos, new Vector3(1.5f, 0.6f, 3.5f), wood);
        boat.AddComponent<Floater>();
        var cabin = Model("SM_cabin", root, Config.CabinPos, new Vector3(4f, 3f, 4f), woodDark);
        for (int i = 0; i < Config.RockPos.Length; i++)
            Model("SM_rock", root, Config.RockPos[i], Vector3.one, BuildNature.Mat("M_cliff", Config.Cliff)).transform.localScale = Vector3.one * Config.RockScale[i];

        // 角色 + 钓具
        var player = new GameObject("Player");
        player.transform.position = Config.PlayerStart;
        var cc = player.AddComponent<CharacterController>();
        cc.center = Vector3.up * 0.9f; cc.height = 1.8f; cc.radius = 0.35f;
        var body = Model("SK_character", player.transform, Config.PlayerStart, new Vector3(0.7f, 1.8f, 0.7f), wood);
        var anim = body.GetComponent<Animator>();
        foreach (var col in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);   // 别和 CharacterController 打架
        if (anim != null) anim.runtimeAnimatorController = ProjectSetup.MakeAnimator();
        var rod = Prim(PrimitiveType.Cylinder, "Rod", player.transform, Config.PlayerStart + new Vector3(0.35f, 1.1f, 0.3f), new Vector3(0.04f, 1.2f, 0.04f), woodDark).transform;
        rod.localRotation = Quaternion.Euler(60f, 0f, 0f);
        Object.DestroyImmediate(rod.GetComponent<Collider>());
        var tip = new GameObject("RodTip").transform;
        tip.SetParent(rod, false); tip.localPosition = Vector3.up;
        var bobber = Prim(PrimitiveType.Sphere, "Bobber", root, Config.FishSpot + Vector3.forward * Config.CastDistance, Vector3.one * 0.18f, BuildNature.Mat("M_bobber", Config.Bobber));
        Object.DestroyImmediate(bobber.GetComponent<Collider>());
        var line = player.AddComponent<LineRenderer>();
        line.widthMultiplier = 0.015f; line.positionCount = 2;
        var shadow = new Material(Shader.Find("Warm/Water"));
        shadow.SetColor("_Color", new Color(0f, 0f, 0f, 0.35f)); shadow.SetFloat("_WaveAmp", 0f); shadow.SetFloat("_Lift", 0f); shadow.SetFloat("_Spec", 0f);
        AssetDatabase.CreateAsset(shadow, Art + "Materials/M_shadow.mat");
        var disc = Prim(PrimitiveType.Quad, "ShadowDisc", player.transform, Config.PlayerStart + Vector3.up * 0.01f, Vector3.one * 0.9f, shadow);
        disc.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Object.DestroyImmediate(disc.GetComponent<Collider>());

        var fishing = player.AddComponent<FishingStateMachine>();
        fishing.rodTip = tip; fishing.bobber = bobber.AddComponent<Floater>(); fishing.line = line;
        var pc = player.AddComponent<PlayerController>();
        pc.animator = anim; pc.fishing = fishing;
        var rig = player.AddComponent<CharacterRig>();
        rig.fishing = fishing; rig.player = pc; rig.rod = rod;
        rig.rightArm = body.GetComponentsInChildren<Transform>().FirstOrDefault(t => { var n = t.name.ToLower(); return n.Contains("arm") && !n.Contains("fore") && (n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r")); });
        if (anim == null) rig.body = body.transform;   // 绑骨失败退路：纯程序动画

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        cam.GetComponent<Camera>().fieldOfView = Config.CamFov;
        cam.AddComponent<TopDownCamera>().target = player.transform;
        TopDownCamera.Place(cam.transform, Config.DockMid);

        var game = new GameObject("Game");
        game.AddComponent<GameBootstrap>();
        var tod = game.AddComponent<TimeOfDay>(); tod.sun = sun; tod.water = water;
        BuildFx.Build(root, cabin, tod, cam.GetComponent<Camera>());
        game.AddComponent<BaitSelector>().fishing = fishing;

        BuildNature.Build(root, water);
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("BUILD-SCENE OK");
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false);
        g.transform.position = pos; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static GameObject Model(string name, Transform parent, Vector3 pos, Vector3 fallbackSize, Material fallback)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx");
        if (fbx == null) return BuildNature.Box(name, parent, pos + Vector3.up * fallbackSize.y * 0.5f, fallbackSize, fallback);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
        g.transform.position = pos;
        return g;
    }
}
