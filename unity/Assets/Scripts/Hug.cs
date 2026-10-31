using UnityEngine;

// loop-13 拥抱：两人 XZ 相距 ≤ HugRange、都没在钓鱼（Idle）、没暂停，任一人按 H（P2 Num3）→ 两人一起抱 HugTime 秒。不加数值、不加奖励；条件不满足按 H 什么都不做（不说话）。
// 抱的时候：PlayerController 不读移动、转身面对面（Facing；沿用它 12/s 的朝向平滑，约 0.2 s 转过去）；FishingStateMachine 不抛竿；
// CharacterRig 在 LateUpdate 给两侧大臂/小臂叠「双臂往前环抱」（权重 Weight，进出各 HugBlend 秒）、藏鱼竿；两人头顶各说「抱一下」（Hints.Say，留 HintFail = 1.5 s）。
// 挂在 Game 上（BuildScene）。状态是静态的：只有两个人，抱总是两人一起。
public class Hug : MonoBehaviour
{
    public static float Since = -99f;   // 这次抱开始的 Time.time
    public static readonly Vector3[] Facing = new Vector3[KeyMap.Players.Length];   // 每人抱的时候朝哪（开始那一刻算好：抱着谁都不动）
    public static bool On => Active(Since, Time.time);
    public static float Now => Weight(Since, Time.time);
    PlayerManager pm;

    public static bool Can(Vector3 a, Vector3 b, FishState fa, FishState fb, bool paused) =>
        !paused && fa == FishState.Idle && fb == FishState.Idle && Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) <= Config.HugRange;

    public static Vector3 Face(Vector3 me, Vector3 other) { var d = other - me; d.y = 0f; return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero; }   // 重合时不转

    public static bool Active(float since, float now) => now >= since && now < since + Config.HugTime;

    // 双臂叠加的权重：进出各 HugBlend 秒 SmoothStep，中间 1，抱完 0
    public static float Weight(float since, float now) => Active(since, now) ? Mathf.SmoothStep(0f, 1f, Mathf.Min(now - since, since + Config.HugTime - now) / Config.HugBlend) : 0f;

    // 场景里这两个人现在能不能抱（Hints 判「H 抱抱」也用它）
    public static bool Ready(PlayerContext[] ps)
    {
        if (ps == null || ps.Length < 2) return false;
        PlayerContext a = ps[0], b = ps[1];
        return a != null && b != null && a.isActiveAndEnabled && b.isActiveAndEnabled && a.fishing != null && b.fishing != null
            && Can(a.transform.position, b.transform.position, a.fishing.Logic.State, b.fishing.Logic.State, Pause.On);
    }

    void Start() => pm = FindAnyObjectByType<PlayerManager>();

    void Update()
    {
        if (pm == null || On || !Ready(pm.players)) return;   // 暂停时 Ready 为假：不读键
        foreach (var p in pm.players)
            if (InputManager2P.Pressed(KeyMap.Players[p.playerIndex].Hug)) { Begin(p.playerIndex); return; }
    }

    void Begin(int who)
    {
        Since = Time.time;
        var ps = pm.players;
        for (int i = 0; i < 2; i++) { Facing[ps[i].playerIndex] = Face(ps[i].transform.position, ps[1 - i].transform.position); Hints.Say(ps[i].playerIndex, "抱一下"); }
        Debug.Log($"EVT hug p{who} t={Time.timeSinceLevelLoad:0.0}");
    }
}
