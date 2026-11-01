using UnityEngine;

// loop-13 拥抱：两人 XZ 相距 ≤ HugRange、都没在钓鱼（Idle）、没暂停，任一人按 H（P2 Num3）→ 两人一起抱 HugTime 秒。不加数值、不加奖励；条件不满足按 H 什么都不做（不说话）。
// 抱的时候：PlayerController 不读移动、转身面对面（Facing；沿用它 12/s 的朝向平滑，约 0.2 s 转过去）；FishingStateMachine 不抛竿；Trees / Bricks / PierSave 不读 E / B / R（loop-14）；
// CharacterRig 在 LateUpdate 给两侧大臂/小臂叠「双臂环抱」（权重 Weight，进出各 HugBlend 秒）、藏鱼竿；两人头顶各说「抱一下」（Hints.Say，留 HintFail = 1.5 s），左右并排时中间画「♥」（Hints）。
// loop-14：开始后 HugClose 秒内两人沿连线各往中点挪（Pull；PlayerController 照旧走 CharacterController.Move，碰撞照旧挡），间距收到 HugGap，抱完留在原地；
// 抱着和抱完 HugCooldown 秒内不能再抱（Can）；抱完 PlayerController 转向 Back（抱前的朝向；抱前就朝着对方的转向和连线垂直的一侧：左右为主取离镜头近的、南北为主两人都取西，免得两根竿交叉，见 After）；「H 抱抱」抱过一次（Once）就不再提示。
// 挂在 Game 上（BuildScene）。状态是静态的：只有两个人，抱总是两人一起。
public class Hug : MonoBehaviour
{
    public static float Since = -99f;   // 这次抱开始的 Time.time
    public static readonly Vector3[] Facing = new Vector3[KeyMap.Players.Length];   // 每人抱的时候朝哪（开始那一刻算好：两人只沿连线挪，朝向不变）
    public static readonly Vector3[] Pull = new Vector3[KeyMap.Players.Length];     // 每人这次往中点挪多少（开始那一刻算好）
    public static readonly Vector3[] Back = new Vector3[KeyMap.Players.Length];     // 每人抱完转向哪（PlayerController 自己一走就清零）
    public static bool Once;   // 这一局抱过没有（只在内存，同 Trees.CookedOnce）：抱过就不再提示「H 抱抱」
    public static bool On => Active(Since, Time.time);
    public static float Now => Weight(Since, Time.time);
    PlayerManager pm;

    // 能不能抱；sinceHug = 离上次抱开始几秒：抱着、抱完冷却 HugCooldown 秒内都不行（默认 = 没抱过）
    public static bool Can(Vector3 a, Vector3 b, FishState fa, FishState fb, bool paused, float sinceHug = float.MaxValue) =>
        !paused && sinceHug >= Config.HugTime + Config.HugCooldown && fa == FishState.Idle && fb == FishState.Idle && Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) <= Config.HugRange;

    public static Vector3 Face(Vector3 me, Vector3 other) { var d = other - me; d.y = 0f; return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero; }   // 重合时不转

    // 每人往中点挪多远：两人 XZ 距离 d 收到 HugGap，各走一半；本来就够近不挪
    public static float Close(float d) => Mathf.Max(0f, (d - Config.HugGap) * 0.5f);

    // 到 now 为止挪完了几成：开始后 HugClose 秒匀速走完，之后停住（不弹回）
    public static float Walked(float since, float now) => Mathf.Clamp01((now - since) / Config.HugClose);

    // 这一帧挪多少（PlayerController 加进 step）：两帧 Walked 之差，各帧加起来正好 Pull
    public static Vector3 Step(int i, float now, float dt) => Pull[i] * (Walked(Since, now) - Walked(Since, now - dt));

    // 抱完转向哪：抱前就朝着对方（点积 > HugFaced，约 45° 内）就转向和两人连线垂直的一侧，免得转回去又对着、两根竿交叉；没朝着对方的转回抱前的朝向。
    // 左右为主取离镜头近（z 小）的一侧，并排算出来就是镜头 Vector3.back；南北为主（|facing.z| > |facing.x|，即 Below 成立的站位）两人都取西（x<0）：握竿的右手都在各自北侧、藏在自己帽檐下，
    // 两根竿平行（原来取 z 小的一侧，北边那人稍偏东两人就都转东，他的右手落在南侧、被南边那人的帽子挡住，竿像从南边那人帽沿伸出来）
    public static Vector3 After(Vector3 before, Vector3 facing)
    {
        if (Vector3.Dot(before, facing) <= Config.HugFaced) return before;
        var side = new Vector3(facing.z, 0f, -facing.x);
        if (Mathf.Abs(facing.z) > Mathf.Abs(facing.x)) return side.x < 0f ? side : -side;
        return side.z < 0f ? side : -side;
    }

    // 南北站位时屏幕上靠下（z 小、离镜头近）的那人：对方在北边，且南北差大于东西差（Hints 把他的字条画到脚下，不压对方的帽子）
    public static bool Below(Vector3 me, Vector3[] all) { foreach (var o in all) if (o.z - me.z > Mathf.Abs(o.x - me.x)) return true; return false; }

    public static bool Active(float since, float now) => now >= since && now < since + Config.HugTime;

    // 双臂叠加的权重：进出各 HugBlend 秒 SmoothStep，中间 1，抱完 0
    public static float Weight(float since, float now) => Active(since, now) ? Mathf.SmoothStep(0f, 1f, Mathf.Min(now - since, since + Config.HugTime - now) / Config.HugBlend) : 0f;

    // 场景里这两个人现在能不能抱（读键和 Hints 判「H 抱抱」都用它：抱着、冷却中都为假）
    public static bool Ready(PlayerContext[] ps)
    {
        if (ps == null || ps.Length < 2) return false;
        PlayerContext a = ps[0], b = ps[1];
        return a != null && b != null && a.isActiveAndEnabled && b.isActiveAndEnabled && a.fishing != null && b.fishing != null
            && Can(a.transform.position, b.transform.position, a.fishing.Logic.State, b.fishing.Logic.State, Pause.On, Time.time - Since);
    }

    void Start() => pm = FindAnyObjectByType<PlayerManager>();

    void Update()
    {
        if (pm == null || !Ready(pm.players)) return;   // 暂停、抱着、冷却中 Ready 都为假：不读键
        foreach (var p in pm.players)
            if (InputManager2P.Pressed(KeyMap.Players[p.playerIndex].Hug)) { Begin(p.playerIndex); return; }
    }

    void Begin(int who)
    {
        Since = Time.time; Once = true;
        var ps = pm.players;
        var gap = ps[1].transform.position - ps[0].transform.position; gap.y = 0f;
        for (int i = 0; i < 2; i++)
        {
            int k = ps[i].playerIndex;
            Facing[k] = Face(ps[i].transform.position, ps[1 - i].transform.position);
            Pull[k] = Facing[k] * Close(gap.magnitude);
            Back[k] = After(ps[i].transform.forward, Facing[k]);
            Hints.Say(k, "抱一下");
        }
        Debug.Log($"EVT hug p{who} t={Time.timeSinceLevelLoad:0.0} d={gap.magnitude:0.00}");
    }
}
