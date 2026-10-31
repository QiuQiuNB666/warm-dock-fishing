using UnityEngine;

public enum FishState { Idle, Cast, Wait, Bite, Reel, Caught, Lost }

// 纯逻辑：不碰 UnityEngine 运行时对象，测试直接 new，注入种子即可复现。
public class FishingLogic
{
    readonly System.Random rng;
    public FishState State { get; private set; } = FishState.Idle;
    public float Timer { get; private set; }
    public float WaitTime { get; private set; }
    public float Tension { get; private set; }
    public float Progress { get; private set; }
    public int CaughtCount { get; private set; }
    public string LastFish { get; private set; }
    public float WaitFactor = 1f;
    public float[] Weights = Config.Baits[0].Weights;

    public FishingLogic(int seed) { rng = new System.Random(seed); }

    // Space / 单击按下
    public void Press()
    {
        if (State == FishState.Idle) Enter(FishState.Cast);
        else if (State == FishState.Bite) { Tension = Config.StartTension; Progress = 0f; Enter(FishState.Reel); }
    }

    public void Tick(float dt, bool holding)
    {
        Timer += dt;
        switch (State)
        {
            case FishState.Cast:
                if (Timer >= Config.CastTime)
                {
                    WaitTime = Mathf.Lerp(Config.WaitMin, Config.WaitMax, (float)rng.NextDouble()) * WaitFactor;
                    Enter(FishState.Wait);
                }
                break;
            case FishState.Wait:
                if (Timer >= WaitTime) Enter(FishState.Bite);
                break;
            case FishState.Bite:
                if (Timer >= Config.BiteWindow) Enter(FishState.Idle);   // 没按住，鱼跑了
                break;
            case FishState.Reel:
                Tension = Mathf.Max(0f, Tension + (holding ? Config.PullTension : -Config.RelaxTension) * dt);
                Progress = Mathf.Max(0f, Progress + (holding ? Config.PullProgress : -Config.SlipProgress) * dt);
                if (Tension > 1f) Enter(FishState.Lost);
                else if (Progress >= 1f) { LastFish = PickFish(); CaughtCount++; Enter(FishState.Caught); }
                break;
            case FishState.Caught:
            case FishState.Lost:
                if (Timer >= Config.ResultTime) Enter(FishState.Idle);
                break;
        }
    }

    public string PickFish()
    {
        float sum = 0f;
        foreach (var w in Weights) sum += w;
        float r = (float)rng.NextDouble() * sum;
        for (int i = 0; i < Weights.Length; i++)
        {
            r -= Weights[i];
            if (r < 0f) return Config.FishNames[i];
        }
        return Config.FishNames[Weights.Length - 1];
    }

    void Enter(FishState s) { State = s; Timer = 0f; }
}

// 薄外壳：读输入（或 -demo 自动操作）；浮漂/鱼线表现在 Tackle。
public class FishingStateMachine : MonoBehaviour
{
    public static readonly bool Demo = GameBootstrap.Arg("-demo") != null;
    public static bool DemoDrives(Component c) => Demo && c.name == "Player";   // 演示只驱动名为 Player 的那一个
    public FishingLogic Logic { get; private set; }
    public bool Busy => Logic.State != FishState.Idle;
    public Font font;   // 「生鱼 n」用 WarmCJK（和暂停菜单同一份），网页端才显示得出中文
    int owner;   // 第几名玩家：P1 Space，P2 Keypad5
    float atSpot;
    int logged;   // 已经记进 Stock 的条数
    GUIStyle cjk;

    // 钓上就记账：CaughtCount 比已记的多几条，就给这名玩家的生鱼加几条（每人各算各的）。FishingLogic 本身不碰 Stock。
    public static int Credit(Stock s, int owner, int logged, int caught)
    {
        if (caught > logged) s.Add(ItemType.RawFish, caught - logged, owner);
        return Mathf.Max(logged, caught);
    }

    void Awake()
    {
        var ctx = GetComponent<PlayerContext>();
        owner = ctx != null ? ctx.playerIndex : 0;
        Logic = new FishingLogic((Demo ? 7 : System.Environment.TickCount) + owner * 10007);
    }

    void Update()
    {
        if (Pause.On) return;   // 暂停时不读钓鱼，也不走钓鱼计时
        bool pointer = InputManager2P.PointerAllowed && owner == 0;   // 双人时鼠标不抛竿
        bool down = InputManager2P.FishDown(owner) || (pointer && Input.GetMouseButtonDown(0));
        bool hold = InputManager2P.FishHeld(owner) || (pointer && Input.GetMouseButton(0));
        bool demo = DemoDrives(this);
        if (demo) DemoInput(out down, out hold);
        if (down && (Busy || Config.InFishSpot(transform.position))) Logic.Press();
        var was = Logic.State;
        Logic.Tick(Time.deltaTime, hold);
        if (was != FishState.Bite && Logic.State == FishState.Bite) Debug.Log($"EVT bite p{owner} t={Time.timeSinceLevelLoad:0.0}");   // 咬钩那一刻（实玩脚本 until 等它）
        if (Logic.CaughtCount > logged)   // 钓上的瞬间：这条鱼进这名玩家的生鱼
        {
            Debug.Log($"{(demo ? "DEMO-" : "")}CAUGHT p{owner} {Logic.LastFish} t={Time.timeSinceLevelLoad:0.0}");   // 实玩脚本靠这行判断钓上
            logged = Credit(Stock.Current, owner, logged, Logic.CaughtCount);
        }
    }

    // 演示：走到码头尽头站稳 1 s 后抛竿，咬钩 0.4 s 后提竿，收线时张力 <0.7 才拉；钓上一条就停。
    void DemoInput(out bool down, out bool hold)
    {
        var s = Logic.State;
        bool there = PlayerController.DemoArrived(transform.position);
        atSpot = there && s == FishState.Idle ? atSpot + Time.deltaTime : 0f;
        if (atSpot > 1f && Logic.CaughtCount == 0) Logic.WaitFactor = 0.5f;
        down = (atSpot > 1f && Logic.CaughtCount == 0) || (s == FishState.Bite && Logic.Timer > 0.4f);
        hold = s == FishState.Reel && Logic.Tension < 0.7f;
    }

    void OnGUI()
    {
        if (Pause.On) return;   // 暂停时让开
        float x = owner == 0 ? 10f : Screen.width - 310f, bx = owner == 0 ? 10f : Screen.width - 210f;   // P1 左上，P2 右上
        string head = $"Fish: {Logic.CaughtCount}   {Logic.State}  {Logic.LastFish}", fish = $"生鱼 {Stock.Current.Count(ItemType.RawFish, owner)}";   // 原来的字不动，后面接「生鱼 n」
        cjk ??= new GUIStyle(GUI.skin.label) { font = font, fontSize = 13 };
        float hw = GUI.skin.label.CalcSize(new GUIContent(head)).x;
        GUI.Box(new Rect(x - 4f, 8f, hw + 14f + cjk.CalcSize(new GUIContent(fish)).x, 26f), GUIContent.none);   // 深色半透明底（GUI.skin.box，同顶部「砖 n　木 n」），白天沙地上也看得清
        GUI.Label(new Rect(x, 10, 300, 22), head);
        GUI.Label(new Rect(x + hw + 6f, 10, 120, 22), fish, cjk);
        if (Logic.State != FishState.Reel) return;
        for (int i = 0; i < 2; i++)   // 张力条、收线条：整条底框 + 按比例填充，WarmCJK 的字画在整条上（不跟着填充宽度被裁掉）
        {
            var r = new Rect(bx, 36f + 22f * i, 200f, 18f);
            GUI.Box(r, GUIContent.none); GUI.Box(new Rect(r.x, r.y, 200f * Mathf.Clamp01(i == 0 ? Logic.Tension : Logic.Progress), r.height), GUIContent.none);
            GUI.Label(new Rect(r.x + 6f, r.y, 194f, 22f), i == 0 ? "张力" : "收线", cjk);
        }
    }
}
