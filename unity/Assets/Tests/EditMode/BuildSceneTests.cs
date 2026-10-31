using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 打开 BuildScene 产出的 Main.unity 做断言；常量全部取自 Config。
public class BuildSceneTests
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [OneTimeSetUp] public void Open()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), "run BuildScene.Build first");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    static GameObject[] All() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.gameObject).ToArray();
    static GameObject One(string name) => All().Single(g => g.name == name);
    static int Count(string name) => All().Count(g => g.name == name);

    [Test] public void KeyObjectsExist()
    {
        foreach (var n in new[] { "Sun", "Sea", "Seabed", "Dock", "FishSpot", "Player", "Main Camera", "Game", "Nature", "Beach", "River", "Cliff", "Waterfall", "Bobber" })
            Assert.AreEqual(1, Count(n), n);
        Assert.AreEqual(Config.PlankCount, Count("Plank"));
        Assert.AreEqual(Config.TreeCount, Count("Tree"));
        Assert.AreEqual(2, Count("FishSchool"));
        Assert.AreEqual(Config.RockPos.Length, One("World").transform.Cast<Transform>().Count(t => t.name == "SM_rock"));
    }

    [Test] public void KeyPositions()
    {
        Assert.That(Vector3.Distance(Config.FishSpot, One("FishSpot").transform.position), Is.LessThan(1e-3f));
        Assert.That(Vector3.Distance(Config.BedPos, One("Player").transform.position), Is.LessThan(1e-3f));
        var planks = All().Where(g => g.name == "Plank").Select(g => g.GetComponent<Renderer>().bounds).ToArray();
        Assert.AreEqual(Config.DockLength, planks.Max(b => b.max.z) - planks.Min(b => b.min.z), 0.1f);
        Assert.AreEqual(Config.GroundY, planks[0].max.y, 1e-3f);   // 码头顶面 = 地面
        var plank = All().First(g => g.name == "Plank").GetComponent<BoxCollider>();   // 低模木板补的碰撞体，顶面也要和地面齐平才能走
        Assert.AreEqual(Config.GroundY, plank.transform.TransformPoint(plank.center + Vector3.up * plank.size.y * 0.5f).y, 1e-3f);
        Assert.AreEqual(Config.GroundY, One("Beach").GetComponent<Renderer>().bounds.max.y, 1e-3f);
        Assert.IsTrue(Config.InFishSpot(Config.FishSpot) && Config.OnDock(Config.FishSpot));
    }

    [Test] public void CameraPitchAndFov()
    {
        var cam = One("Main Camera");
        Assert.AreEqual(Config.CamPitch, cam.transform.eulerAngles.x, 0.01f);
        Assert.AreEqual(Config.CamFov, cam.GetComponent<Camera>().fieldOfView, 0.01f);
        Assert.IsNotNull(cam.GetComponent<TopDownCamera>().target);
    }

    [Test] public void MaterialColors()
    {
        Assert.AreEqual(Config.Wood, One("Dock").GetComponentInChildren<Renderer>().sharedMaterial.color);
        Assert.AreEqual(Config.Sand, One("Beach").GetComponent<Renderer>().sharedMaterial.color);
        Assert.AreEqual(Config.Sea, One("Sea").GetComponent<Renderer>().sharedMaterial.GetColor("_Color"));
    }

    [Test] public void PlayerWiring()
    {
        var p = One("Player");
        var fishing = p.GetComponent<FishingStateMachine>();
        Assert.IsNotNull(p.GetComponent<CharacterController>());
        var tackle = p.GetComponent<Tackle>();
        Assert.IsNotNull(tackle.bobber); Assert.IsNotNull(tackle.rodTip); Assert.IsNotNull(tackle.splash); Assert.IsNotNull(tackle.fish);
        Assert.AreSame(fishing, p.GetComponent<PlayerController>().fishing);
        Assert.AreSame(fishing, p.GetComponent<CharacterRig>().fishing);
        Assert.AreSame(fishing, One("Game").GetComponent<BaitSelector>().fishing);
        var anim = p.GetComponentInChildren<Animator>();
        if (anim != null) Assert.IsFalse(anim.applyRootMotion && p.GetComponent<PlayerController>().animator == anim);
    }

    // 子阶段 0：第二名玩家 + 床。床与两人都在沙滩可走处，且不在小屋包围盒里。
    [Test] public void TwoPlayersAndBed()
    {
        foreach (var n in new[] { "Player", "Player2", "Bobber", "Bobber2", "Bed" }) Assert.AreEqual(1, Count(n), n);
        Assert.AreSame(One("Player").GetComponent<FishingStateMachine>(), One("Game").GetComponent<BaitSelector>().fishing);
        Assert.IsNotNull(One("Game").GetComponent<PlayerManager>());
        PlayerContext p1 = One("Player").GetComponent<PlayerContext>(), p2 = One("Player2").GetComponent<PlayerContext>();
        Assert.AreEqual(0, p1.playerIndex); Assert.AreEqual(1, p2.playerIndex);
        Assert.AreEqual(Config.GenderP1, p1.gender); Assert.AreEqual(Config.GenderP2, p2.gender);
        Assert.That(Vector3.Distance(Config.BedPos + Config.P2BedOffset, p2.transform.position), Is.LessThan(1e-3f));
        var cabin = One("World").transform.Cast<Transform>().First(t => t.name == "SM_cabin").GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        bool InCabin(Vector3 p, float r) => p.x + r > cabin.min.x && p.x - r < cabin.max.x && p.z + r > cabin.min.z && p.z - r < cabin.max.z;
        var bed = One("Bed").transform.position;
        foreach (var p in new[] { bed, Config.BedPos, p2.transform.position })
        {
            Assert.IsTrue(Config.OnBeach(p) && Config.IsWalkable(p), $"{p} 不在沙滩可走处");
            Assert.IsFalse(InCabin(p, 0.35f), $"{p} 在小屋里");
        }
        Assert.IsFalse(InCabin(bed, Config.BedSize.x * 0.5f), "床压到小屋");
    }

    [Test] public void FbxScaleAndRotation()
    {
        foreach (var name in new[] { "SM_rock", "SM_boat", "SM_cabin", "SK_character" })
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/{name}.fbx");
            if (fbx == null) { Assert.Ignore($"{name}.fbx not imported yet"); return; }
            Assert.AreEqual(Vector3.one, fbx.transform.localScale, name);
            Assert.That(Quaternion.Angle(Quaternion.identity, fbx.transform.localRotation), Is.LessThan(0.5f), name);
            foreach (Transform child in fbx.transform)
                if (child.GetComponent<Renderer>() != null)
                    Assert.That(Quaternion.Angle(Quaternion.identity, child.localRotation), Is.LessThan(0.5f), $"{name}/{child.name}");
        }
    }

    // 8 段动作都在、循环/一次性设置对；持竿姿势下鱼竿挂点在髋部前方（角色没背对走路方向）
    [Test] public void CharacterClipsAndFacing()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/SK_character.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        foreach (var n in CharacterRig.Clips) Assert.IsTrue(clips.ContainsKey("AN_" + n), n);
        foreach (var n in new[] { "idle", "walk", "wait", "reel" }) Assert.IsTrue(clips["AN_" + n].isLooping, n);
        foreach (var n in new[] { "cast", "bite", "catch", "lose" }) Assert.IsFalse(clips["AN_" + n].isLooping, n);
        var p = One("Player");
        var anim = p.GetComponentInChildren<Animator>();
        Transform Bone(string n) => anim.GetComponentsInChildren<Transform>().Single(t => t.name == n);
        clips["AN_wait"].SampleAnimation(anim.gameObject, 0f);
        Assert.Greater(CharacterRig.Facing(p.transform, Bone("hips"), Bone("rod_mount")), 0.05f);
        Assert.IsTrue(Bone("rod_mount").GetComponentsInChildren<Transform>().Any(t => t.name == "RodTip"));
    }

    // loop-9 篝火：一个、带碰撞体（挡人也挡砖）、上方一盏点光源；离每棵树都比 InteractRange + ChopRange 远（烤鱼和砍树的 E 范围不相交）
    [Test] public void CampfireClearOfTrees()
    {
        var f = One("Campfire");
        Assert.IsNotNull(f.GetComponent<BoxCollider>(), "碰撞体");
        Assert.IsNotNull(f.GetComponentInChildren<Light>(), "暖光");
        foreach (var t in All().Where(g => g.name == "Tree"))
            Assert.Greater(Vector2.Distance(new Vector2(t.transform.position.x, t.transform.position.z), new Vector2(Config.FirePos.x, Config.FirePos.z)), Config.InteractRange + Config.ChopRange, t.transform.position.ToString());
    }

    // loop-12 修补（逃逸缺陷）：读档重建的小灯塔顶块恰好一片子 Renderer 用 glowTop，是方块自己的网格压扁、子物体不带碰撞体
    //（Quad 图元自带 MeshCollider，场景里一个都没有，网页端剥引擎代码会剥掉它，读档 Spawn 出错）；侧面比顶面暗。新建的 Bricks，不用场景里那个
    [Test] public void LighthouseTopSlab()
    {
        Stock keep = Stock.Current; var b = new GameObject("t").AddComponent<Bricks>(); b.glowTop = new Material(b.glow = new Material(Shader.Find("Universal Render Pipeline/Lit"))); Transform block = null;
        try
        {
            Stock.Current = new Stock(); for (int k = 0; k < Config.MaxHeight; k++) Stock.Current.bricks.Add(new BrickRecord { x = 0, z = -7, layer = k });
            b.Rebuild(); block = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).First(x => x.sharedMaterial == b.glow).transform;
            var tops = block.GetComponentsInChildren<Renderer>().Where(x => x.sharedMaterial == b.glowTop).ToArray();
            Assert.AreEqual(1, tops.Length, "顶块恰好一片子 Renderer 用 glowTop"); Assert.AreEqual(0, block.GetComponentsInChildren<Collider>().Count(c => c.transform != block), "子物体不带碰撞体");
            Assert.AreEqual(block.GetComponent<MeshFilter>().sharedMesh, tops[0].GetComponent<MeshFilter>().sharedMesh, "顶面用方块自己的网格，不是 Quad 图元"); Assert.Less(Config.TowerSide, 1f, "侧面比顶面暗");
        }
        finally { Stock.Current = keep; if (block != null) Object.DestroyImmediate(block.parent.gameObject); Object.DestroyImmediate(b.gameObject); }
    }
}
