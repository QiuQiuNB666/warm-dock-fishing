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
    public static bool WillPress(bool down, bool hug, bool busy, bool inSpot) => down && !hug && (busy || inSpot);   // 这一帧按下算不算数：抱着一律不算（loop-14 提成静态，测试抓得到）
    public Font font;   // 角上「生鱼 n」用 WarmCJK（和暂停菜单同一份，FishHud 画），网页端才显示得出中文
    int owner;   // 第几名玩家：P1 Space，P2 Keypad5
    float atSpot;
    int logged;   // 已经记进 Stock 的条数

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
        if (WillPress(down, Hug.On, Busy, Config.InFishSpot(transform.position))) Logic.Press();   // 抱着不抛竿（loop-13；抱要两人都 Idle，所以抱着时 Busy 恒假）
        Logic.Tick(Time.deltaTime, hold);   // 咬钩那一刻的 EVT bite 在 Hints.Update 的前后帧比对里打（loop-10）
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

    void OnGUI() { if (!Pause.On) FishHud.Draw(Logic, owner, font); }   // 暂停时让开；角上的字和收线时的两根条在 FishHud（loop-10 搬出）
}
