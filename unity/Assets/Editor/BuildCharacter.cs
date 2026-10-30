using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 角色 + 钓具：骨骼人偶、8 态 Animator、挂在 rod_mount 上的三节鱼竿、下垂鱼线、浮漂、水花粒子、上钩的鱼、脚下影子。
// 两名玩家同一套层级：Player 在 BedPos，Player2 在 BedPos + P2BedOffset，钓具物体名加后缀 "2"。
public static class BuildCharacter
{
    public const string Fbx = "Assets/Art/SK_character.fbx";
    const string MatDir = "Assets/Art/Materials/";

    // 返回 Player 的 FishingStateMachine（相机、BaitSelector 仍指向它）
    public static FishingStateMachine Build(Transform root, Material wood, Material woodDark)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        AnimatorController ac = null;   // 两人共用同一个控制器资源
        var p1 = BuildOne(0, Config.BedPos, Config.GenderP1, root, wood, woodDark, clips, ref ac);
        BuildOne(1, Config.BedPos + Config.P2BedOffset, Config.GenderP2, root, wood, woodDark, clips, ref ac);
        return p1;
    }

    // 建一名玩家：index 0 叫 "Player"，1 叫 "Player2"
    static FishingStateMachine BuildOne(int index, Vector3 pos, Gender gender, Transform root, Material wood, Material woodDark, AnimationClip[] clips, ref AnimatorController ac)
    {
        string sfx = index == 0 ? "" : (index + 1).ToString();
        var player = new GameObject("Player" + sfx);
        player.transform.position = pos;
        var cc = player.AddComponent<CharacterController>();
        cc.center = Vector3.up * 0.9f; cc.height = 1.8f; cc.radius = 0.35f;
        var body = BuildScene.Model("SK_character", player.transform, pos, new Vector3(0.7f, 1.8f, 0.7f), wood);
        var anim = body.GetComponent<Animator>();
        foreach (var col in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);   // 别和 CharacterController 打架
        Transform Bone(string n) => body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == n);
        var mount = Bone("rod_mount");
        var wait = clips.FirstOrDefault(c => c.name == "AN_wait");
        if (anim != null)
        {
            anim.runtimeAnimatorController = ac ??= MakeAnimator(clips);
            anim.applyRootMotion = false;
            if (wait != null) wait.SampleAnimation(body, 0f);
            if (mount != null && CharacterRig.Facing(player.transform, Bone("hips"), mount) < 0f) body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 模型背对 +Z 就掉头
            if (wait != null) wait.SampleAnimation(body, 0f);
        }
        if (index == 1) HatBand(Bone("hat"));

        // 鱼竿：三节，挂到右手 rod_mount（没有骨骼就挂角色身上），朝前上方 40°
        var rod = new GameObject("Rod").transform;
        rod.SetParent(player.transform, false);
        rod.position = mount != null ? mount.position : pos + new Vector3(0.3f, 1f, 0.3f);
        rod.rotation = player.transform.rotation * Quaternion.Euler(50f, 0f, 0f);
        if (mount != null) rod.SetParent(mount, true);
        var joints = new Transform[3];
        Transform parent = rod;
        for (int i = 0; i < joints.Length; i++)
        {
            joints[i] = new GameObject("RodJoint").transform;
            joints[i].SetParent(parent, false);
            joints[i].localPosition = i == 0 ? Vector3.zero : Vector3.up * Config.RodSegment;
            var seg = BuildScene.Prim(PrimitiveType.Cylinder, "RodSegment", joints[i], Vector3.zero, new Vector3(0.04f - i * 0.01f, Config.RodSegment * 0.5f, 0.04f - i * 0.01f), woodDark);
            seg.transform.localPosition = Vector3.up * Config.RodSegment * 0.5f;
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            parent = joints[i];
        }
        var tip = new GameObject("RodTip").transform;
        tip.SetParent(parent, false); tip.localPosition = Vector3.up * Config.RodSegment;

        var bobber = BuildScene.Prim(PrimitiveType.Sphere, "Bobber" + sfx, root, Config.FishSpot + Vector3.forward * Config.CastDistance, Vector3.one * 0.18f, BuildNature.Mat("M_bobber", Config.Bobber));
        Object.DestroyImmediate(bobber.GetComponent<Collider>());
        var fish = BuildScene.Prim(PrimitiveType.Capsule, "HookedFish" + sfx, root, Config.FishSpot, new Vector3(0.2f, 0.4f, 0.2f), BuildNature.Mat("M_fish", Config.Fish));
        Object.DestroyImmediate(fish.GetComponent<Collider>());
        var line = player.AddComponent<LineRenderer>();
        line.widthMultiplier = 0.015f; line.positionCount = Tackle.LinePoints;
        line.sharedMaterial = BuildNature.Mat("M_line", new Color(0.95f, 0.95f, 0.9f));
        var shadow = Reuse("M_shadow", () => { var m = new Material(Shader.Find("Warm/Water"));
            m.SetColor("_Color", new Color(0f, 0f, 0f, 0.35f)); m.SetFloat("_WaveAmp", 0f); m.SetFloat("_Lift", 0f); m.SetFloat("_Spec", 0f); return m; });
        var disc = BuildScene.Prim(PrimitiveType.Cylinder, "ShadowDisc" + sfx, player.transform, pos + Vector3.up * 0.01f, new Vector3(0.9f, 0.002f, 0.9f), shadow);   // 网页端的圆片影子（桌面运行时关掉）
        Object.DestroyImmediate(disc.GetComponent<Collider>());

        var fishing = player.AddComponent<FishingStateMachine>(); fishing.font = AssetDatabase.LoadAssetAtPath<Font>(BuildScene.CjkFont);   // 「生鱼 n」用 WarmCJK
        var pc = player.AddComponent<PlayerController>();
        pc.animator = anim; pc.fishing = fishing;
        var rig = player.AddComponent<CharacterRig>();
        rig.fishing = fishing; rig.player = pc; rig.animator = anim; rig.rodJoints = joints;
        if (anim == null) rig.body = body.transform;   // 绑骨失败退路：纯程序动画
        var tackle = player.AddComponent<Tackle>();
        tackle.fishing = fishing; tackle.rodTip = tip; tackle.bobber = bobber.AddComponent<Floater>(); tackle.line = line;
        tackle.fish = fish.transform; tackle.splash = Splash(root, "Splash" + sfx);
        var ctx = player.AddComponent<PlayerContext>();
        ctx.playerIndex = index; ctx.gender = gender; ctx.controller = pc; ctx.fishing = fishing; ctx.animator = anim;
        return fishing;
    }

    // P2 帽带：hat 骨骼下的细圆柱，盖住人偶原来那圈帽带（blender/rig.py：半径 0.155、高 1.75–1.78 m，hat 骨骼起点 1.72 m）
    static void HatBand(Transform hat)
    {
        if (hat == null) return;
        Vector3 axis = hat.up.y > 0.7f ? hat.up : Vector3.up;   // 骨骼 Y 轴顺着帽子向上；不确定时按世界向上
        var band = BuildScene.Prim(PrimitiveType.Cylinder, "HatBand", null, hat.position + axis * 0.045f, new Vector3(0.324f, 0.018f, 0.324f), BuildNature.Mat("M_hatband", Config.HatBandP2));
        band.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
        Object.DestroyImmediate(band.GetComponent<Collider>());
        band.transform.SetParent(hat, true);
    }

    // 材质已存在就加载复用，不重复 CreateAsset（第二名玩家也走这里）
    static Material Reuse(string name, System.Func<Material> make)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = make();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 水花：一个粒子系统反复 Emit，即对象池（不 Instantiate）
    static ParticleSystem Splash(Transform root, string name)
    {
        var go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(root, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = false; m.playOnAwake = false; m.startLifetime = 0.7f; m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f); m.gravityModifier = 0.9f; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 200;
        var e = ps.emission; e.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 30f; sh.radius = 0.1f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Reuse("M_splash", () => new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = new Color(0.9f, 0.97f, 1f) });
        return ps;
    }

    // Animator：idle/walk 由 Speed 切换（walk 播放速度 = WalkScale）；钓鱼各态由 CharacterRig 交叉淡入
    public static AnimatorController MakeAnimator(AnimationClip[] clips)
    {
        var ac = AnimatorController.CreateAnimatorControllerAtPath("Assets/Art/AC_character.controller");
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("WalkScale", AnimatorControllerParameterType.Float);
        var sm = ac.layers[0].stateMachine;
        var states = CharacterRig.Clips.ToDictionary(n => n, n => { var s = sm.AddState(n); s.motion = clips.FirstOrDefault(c => c.name == "AN_" + n); return s; });
        sm.defaultState = states["idle"];
        states["walk"].speedParameter = "WalkScale"; states["walk"].speedParameterActive = true;
        var go = states["idle"].AddTransition(states["walk"]); go.hasExitTime = false; go.duration = 0.15f; go.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
        var back = states["walk"].AddTransition(states["idle"]); back.hasExitTime = false; back.duration = 0.15f; back.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
        return ac;
    }
}
