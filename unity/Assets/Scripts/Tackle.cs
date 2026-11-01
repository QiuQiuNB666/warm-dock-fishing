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
    Vector3 castFrom, fishFrom, hookPos, target;   // target：自己的浮漂落点（抛竿那一刻按两人左右定，见 Lane），浮漂、鱼、水花、鱼线终点都跟它
    float splashTimer;
    FishSchool[] schools;
    int me;   // 第几名玩家
    static readonly Tackle[] all = new Tackle[KeyMap.Players.Length];   // 每名玩家的钓具：抛竿时看另一人站哪、浮漂在不在水里（loop-10）

    // 浮漂落点：钓点前方 CastDistance，按玩家左右错开 CastSpread（P1 往 -x，P2 往 +x），两人并排钓一眼分得清谁的浮漂（loop-8）；= 两人都站钓点正中时 Lane 的结果，开局浮漂摆这（BuildCharacter）
    public static Vector3 CastTarget(int player) => Lane(player, Config.FishSpot.x, Config.FishSpot.x, null);

    // loop-10 抛竿那一刻按两人实际左右定道 → 浮漂落点。otherX：另一人站在钓点（或浮漂在外）时他的 x，否则 null；otherLane：他的浮漂已在水里时落点的 x，否则 null。
    // 单人 / 另一人不在钓点 → 自己正前方，x 夹在 ±CastSpread；另一人在钓点 → 靠左的走左道 -CastSpread、靠右的走右道（一样左时 P1 走左）；
    // 他的浮漂已在水里 → 取自己这侧空着的那边，离他的浮漂至少 CastSpread（他单人时抛在了自己这侧也往外让，两根线不交叉）
    public static Vector3 Lane(int player, float myX, float? otherX, float? otherLane)
    {
        float s = Config.CastSpread, c = Config.FishSpot.x, l = (otherLane ?? c) - c;   // l：他的浮漂相对中线（没在水里当 0，左右道正好 ∓s）
        float x = otherX is not float o ? Mathf.Clamp(myX - c, -s, s) : myX < o || myX == o && player == 0 ? Mathf.Min(-s, l - s) : Mathf.Max(s, l + s);
        return new Vector3(c + x, Config.SeaY, Config.FishSpot.z + Config.CastDistance);
    }

    // 收线时浮漂不越过码头中线：落点在哪侧就夹在哪侧（落点正在中线上 = 单人站正中，不夹）（loop-10）
    public static Vector3 Keep(Vector3 p, float laneX) { float c = Config.FishSpot.x; if (laneX != c) p.x = laneX < c ? Mathf.Min(p.x, c) : Mathf.Max(p.x, c); return p; }

    // 抛物线：t=0 起点，t=1 终点，中点比两端连线高 h
    public static Vector3 Arc(Vector3 a, Vector3 b, float t, float h) => Vector3.Lerp(a, b, t) + Vector3.up * (4f * h * t * (1f - t));

    // 钓上的鱼：钓上 t 秒时从浮漂 hook 沿 Arc（弧高 FishLeapArc）跃了多远，FishLeapTime 秒落到手位 Hand（根节点上方 FishHandUp、前方 FishHandFwd）；loop-20 修补从这里提进 Config，Hints 按同一条弧框出鱼还会占的屏幕范围，字条让开（Hints.Dodge）
    public static Vector3 Hand(Transform t) => t.position + Vector3.up * Config.FishHandUp + t.forward * Config.FishHandFwd;
    public static Vector3 Leap(Vector3 hook, Vector3 hand, float t) => Arc(hook, hand, Mathf.Clamp01(t / Config.FishLeapTime), Config.FishLeapArc);
    public static Vector3? Hook(int player) => all[player] != null ? all[player].hookPos : (Vector3?)null;   // 这名玩家这一条从哪跃起（钓上那一刻的浮漂位置，Enter(Caught) 记下；Caught 以外是上一条的，调用方自己看状态）

    // 二次贝塞尔下垂：控制点在中点下方 sag 米
    public static Vector3 Sag(Vector3 a, Vector3 b, float t, float sag)
    {
        Vector3 c = (a + b) * 0.5f + Vector3.down * sag;
        return Vector3.Lerp(Vector3.Lerp(a, c, t), Vector3.Lerp(c, b, t), t);
    }

    void Start()
    {
        schools = FindObjectsByType<FishSchool>(FindObjectsSortMode.None);
        var ctx = GetComponent<PlayerContext>();
        me = ctx != null ? ctx.playerIndex : 0; all[me] = this;
        target = CastTarget(me);
    }

    bool Out => last >= FishState.Cast && last <= FishState.Reel;   // 浮漂在外面（这一竿已定道，还没收回）

    Vector3 PickLane()   // 抛竿那一刻：另一人站在钓点（或浮漂在外）就把他的 x、他浮漂的 x 交给 Lane（loop-10）
    {
        var o = all[1 - me];
        bool on = o != null && o.isActiveAndEnabled && (o.Out || Config.InFishSpot(o.transform.position));
        return Lane(me, transform.position.x, on ? o.transform.position.x : (float?)null, on && o.Out ? o.target.x : (float?)null);
    }

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
            case FishState.Cast: castFrom = rodTip.position; target = PickLane(); break;   // 抛竿那一刻定道（loop-10）
            case FishState.Wait:
                Splash(target, 16);   // 落水
                fishFrom = target + new Vector3(target.x > Config.FishSpot.x ? 3f : -3f, -1.5f, 2f);   // 从自己外侧游来：落点在右侧的从右边来（loop-10）
                foreach (var sc in schools) if ((sc.transform.position - target).sqrMagnitude < (fishFrom - target).sqrMagnitude) fishFrom = sc.transform.position;
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
        if (s == FishState.Cast) bobber.transform.position = Arc(castFrom, target, Mathf.Clamp01(t / Config.CastTime), 2.5f);
        else if (s == FishState.Wait || s == FishState.Bite) { bobber.SetAnchor(target); bobber.dip = s == FishState.Bite ? 1f : 0f; }
        else if (s == FishState.Reel)
        {
            Vector3 near = transform.position + transform.forward * 1.2f;
            near.y = Config.SeaY;
            Vector3 drag = new Vector3(Mathf.Sin(Time.time * 3.1f + target.x) * Config.CastSpread, 0f, 0.8f) * (1f - L.Progress) * (0.5f + L.Tension);   // 鱼往外拖、左右挣：摆幅跟 CastSpread，落点 x 当相位两人错开（loop-10）
            bobber.SetAnchor(Keep(Vector3.Lerp(target, near, L.Progress) + drag, target.x));   // 不越过码头中线
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
                p = Leap(hookPos, Hand(transform), t); dir = Vector3.up;
            }
            else
            {
                float w = Mathf.Sin(Time.time * 18f + target.x);   // 落点 x 当相位，两条鱼不同步扭（loop-10）
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
