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
        Assert.That(Vector3.Distance(Config.PlayerStart, One("Player").transform.position), Is.LessThan(1e-3f));
        var planks = All().Where(g => g.name == "Plank").Select(g => g.GetComponent<Renderer>().bounds).ToArray();
        Assert.AreEqual(Config.DockLength, planks.Max(b => b.max.z) - planks.Min(b => b.min.z), 0.1f);
        Assert.AreEqual(Config.GroundY, planks[0].max.y, 1e-3f);   // 码头顶面 = 地面
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
}
