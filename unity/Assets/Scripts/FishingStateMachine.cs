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

// 薄外壳：读输入、驱动浮漂和鱼线。
public class FishingStateMachine : MonoBehaviour
{
    public Transform rodTip;
    public Floater bobber;
    public LineRenderer line;
    public FishingLogic Logic { get; private set; }
    public bool Busy => Logic.State != FishState.Idle;

    void Awake() { Logic = new FishingLogic(System.Environment.TickCount); }

    void Update()
    {
        bool down = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
        bool hold = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
        if (down && (Busy || Config.InFishSpot(transform.position))) Logic.Press();
        Logic.Tick(Time.deltaTime, hold);
        UpdateTackle();
    }

    void UpdateTackle()
    {
        bool wet = Logic.State >= FishState.Wait && Logic.State <= FishState.Reel;
        if (bobber == null) return;
        bobber.gameObject.SetActive(wet);
        if (line != null) line.enabled = wet;
        if (!wet) return;
        Vector3 cast = Config.FishSpot + Vector3.forward * Config.CastDistance;
        Vector3 near = transform.position + transform.forward * 1.5f;
        Vector3 p = Vector3.Lerp(cast, near, Logic.State == FishState.Reel ? Logic.Progress : 0f);
        bobber.SetAnchor(new Vector3(p.x, Config.SeaY, p.z));
        bobber.dip = Logic.State == FishState.Bite ? 1f : Logic.State == FishState.Reel ? Logic.Tension : 0f;
        if (line != null && rodTip != null) { line.SetPosition(0, rodTip.position); line.SetPosition(1, bobber.transform.position); }
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 22), $"Fish: {Logic.CaughtCount}   {Logic.State}  {Logic.LastFish}");
        if (Logic.State != FishState.Reel) return;
        GUI.Box(new Rect(10, 36, 200, 14), GUIContent.none);
        GUI.Box(new Rect(10, 36, 200 * Mathf.Clamp01(Logic.Tension), 14), "tension");
        GUI.Box(new Rect(10, 54, 200 * Mathf.Clamp01(Logic.Progress), 14), "reel");
    }
}
