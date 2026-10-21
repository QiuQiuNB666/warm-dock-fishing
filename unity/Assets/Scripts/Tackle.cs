using UnityEngine;

// 钓具表现：下垂鱼线、抛物线抛投、浮漂随浪/咬钩下沉、水花（一个粒子系统反复 Emit＝对象池）、鱼游近/挣扎/跃出。
public class Tackle : MonoBehaviour
{
    public const int LinePoints = 16;
    public FishingStateMachine fishing;
    public Transform rodTip, fish;
    public Floater bobber;
    public LineRenderer line;
    public ParticleSystem splash;

    FishState last;
    Vector3 castFrom, fishFrom, hookPos;
    float splashTimer;
    FishSchool[] schools;

    public static Vector3 CastTarget => new Vector3(Config.FishSpot.x, Config.SeaY, Config.FishSpot.z + Config.CastDistance);

    // 抛物线：t=0 起点，t=1 终点，中点比两端连线高 h
    public static Vector3 Arc(Vector3 a, Vector3 b, float t, float h) => Vector3.Lerp(a, b, t) + Vector3.up * (4f * h * t * (1f - t));

    // 二次贝塞尔下垂：控制点在中点下方 sag 米
    public static Vector3 Sag(Vector3 a, Vector3 b, float t, float sag)
    {
        Vector3 c = (a + b) * 0.5f + Vector3.down * sag;
        return Vector3.Lerp(Vector3.Lerp(a, c, t), Vector3.Lerp(c, b, t), t);
    }

    void Start() { schools = FindObjectsByType<FishSchool>(FindObjectsSortMode.None); }

    void Splash(Vector3 p, int n)
    {
        if (splash == null) return;
        splash.transform.position = new Vector3(p.x, Config.SeaY, p.z);
        splash.Emit(n);
    }

    void Enter(FishState s)
    {
        Vector3 b = bobber.transform.position;
        switch (s)
        {
            case FishState.Cast: castFrom = rodTip.position; break;
            case FishState.Wait:
                Splash(CastTarget, 16);   // 落水
                fishFrom = CastTarget + new Vector3(-3f, -1.5f, 2f);
                foreach (var sc in schools) if ((sc.transform.position - CastTarget).sqrMagnitude < (fishFrom - CastTarget).sqrMagnitude) fishFrom = sc.transform.position;
                break;
            case FishState.Bite: Splash(b, 24); break;
            case FishState.Caught: hookPos = b; Splash(b, 30); break;
            case FishState.Lost: Splash(b, 12); break;
        }
    }

    void LateUpdate()
    {
        var L = fishing.Logic;
        FishState s = L.State;
        float t = L.Timer;
        if (s != last) { Enter(s); last = s; }

        bool wet = s >= FishState.Cast && s <= FishState.Reel;
        bobber.gameObject.SetActive(wet);
        bobber.enabled = s != FishState.Cast;
        if (s == FishState.Cast) bobber.transform.position = Arc(castFrom, CastTarget, Mathf.Clamp01(t / Config.CastTime), 2.5f);
        else if (s == FishState.Wait || s == FishState.Bite) { bobber.SetAnchor(CastTarget); bobber.dip = s == FishState.Bite ? 1f : 0f; }
        else if (s == FishState.Reel)
        {
            Vector3 near = transform.position + transform.forward * 1.2f;
            near.y = Config.SeaY;
            Vector3 drag = new Vector3(Mathf.Sin(Time.time * 3.1f) * 1.2f, 0f, 0.8f) * (1f - L.Progress) * (0.5f + L.Tension);   // 鱼往外拖、左右挣
            bobber.SetAnchor(Vector3.Lerp(CastTarget, near, L.Progress) + drag);
            bobber.dip = 0.3f + 0.7f * L.Tension;
            if ((splashTimer -= Time.deltaTime) < 0f) { splashTimer = 0.35f; Splash(bobber.transform.position, 6); }
        }

        // 鱼：咬钩前游近浮漂 → 咬钩/收线时在浮漂下挣扎 → 钓上时跃出水面落到手里
        Vector3 under = bobber.transform.position + Vector3.down * 0.5f;
        bool show = (s == FishState.Wait && t > L.WaitTime - 2f) || s == FishState.Bite || s == FishState.Reel || s == FishState.Caught;
        fish.gameObject.SetActive(show);
        if (show)
        {
            Vector3 p, dir;
            if (s == FishState.Wait)
            {
                float k = Mathf.SmoothStep(0f, 1f, 1f - (L.WaitTime - t) / 2f);
                p = Vector3.Lerp(fishFrom, under, k); dir = under - fishFrom;
            }
            else if (s == FishState.Caught)
            {
                Vector3 hand = transform.position + Vector3.up * 2.1f + transform.forward * 0.3f;
                float k = Mathf.Clamp01(t / 0.6f);
                p = Arc(hookPos, hand, k, 1.5f); dir = Vector3.up;
            }
            else
            {
                float w = Mathf.Sin(Time.time * 18f);
                p = under + transform.right * w * 0.15f; dir = Quaternion.Euler(0f, w * 35f, 0f) * (under - transform.position);
            }
            fish.position = p;
            if (dir.sqrMagnitude > 1e-4f) fish.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
        }

        line.enabled = wet || s == FishState.Caught;
        if (!line.enabled) return;
        Vector3 a = rodTip.position, e = s == FishState.Caught ? fish.position : bobber.transform.position;
        float slack = s == FishState.Cast || s == FishState.Caught ? 0.05f : 0.12f * Vector3.Distance(a, e) * (1f - L.Tension) * (s == FishState.Reel ? 0.4f : 1f);
        for (int i = 0; i < LinePoints; i++) line.SetPosition(i, Sag(a, e, i / (LinePoints - 1f), slack));
    }
}
