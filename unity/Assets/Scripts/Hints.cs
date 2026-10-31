using System.Linq;
using UnityEngine;

// 就近提示（loop-3）：每名玩家头顶一行小字，只列「现在按了就会成功」的动作，最多 HintMax 个，全角空格隔开。
// 顺序：换砖 > 烤鱼 > 砍树 > 睡觉 > 放砖 > 抛竿；E 只列它实际会做的那一个（门口有鱼只列换砖；火边有生鱼、有木列烤鱼（loop-9）；否则看 Trees.Nearest 那一棵今天还能不能出木）。
// loop-6 教学：钓鱼过程中只在咬钩（提竿）、收线两步提示，抛竿/等鱼/钓上/跑了不画；兜底指路排在现有动作后面补位（不足 HintMax 才露出来）：
// 没鱼「WASD 走去码头尽头钓鱼」（P2「方向键走…」，loop-10）、有生鱼「拿生鱼回小屋门口换砖」（有熟鱼说「拿熟鱼…」，loop-9；有生鱼有木说「拿生鱼去篝火烤」，火边能烤时让给「E 烤鱼」，loop-10），这名玩家这一局钓上过一条（FishingLogic.CaughtCount）/ 换过一次砖（Trees.TradedOnce）后
// 各自不再出（只记在内存），人已在那儿（列了抛竿 / 换砖）不重复，站定 HintStill 秒才画。钓鱼失败（鱼跑了、线断了）在失败那人自己的字条位置说 HintFail 秒（Say；loop-9 烤鱼那句也用它）。
// 判断全用 Bricks / Trees / PierSave / Config 现成的方法，只读，不改规则。暂停、人在屏幕外都不画；头顶会压到顶部「砖 n　木 n」就画到脚下（回差见 Feet）；收线时字条下沿画一道张力条（loop-10）。
// 键名同屏幕下方饵按钮（BaitSelector.KeyName：P2 是 Num1/Num2/Num4/Num5），P1 的 Space 叫「空格」。
public class Hints : MonoBehaviour
{
    public Font font;   // WarmCJK（BuildScene 赋值），网页端才显示得出中文
    PlayerManager players;
    Camera cam;
    GUIStyle style, ink;
    readonly FishState[] last = new FishState[KeyMap.Players.Length];   // 每人上一帧的钓鱼状态（Update 比对，失败时说一句）
    static readonly string[] fail = new string[KeyMap.Players.Length];   // 每人刚说的那句（钓鱼失败；loop-9 烤鱼，见 Say）：Time.time < failUntil 时代替 Text 画在自己的字条位置（不走底部 Toast：正中会压住落后那人，也不分人）
    static readonly float[] failUntil = new float[KeyMap.Players.Length];
    readonly float[] still = new float[KeyMap.Players.Length];   // 每人站定了几秒（控制器 Speed01 为 0 就累计）
    readonly float[] pathAt = new float[KeyMap.Players.Length];   // loop-7 修补：每人面前那格的连通判断（BrickPath.Open，BFS）每 PathEvery 秒算一次，免得每帧算
    readonly bool[] pathOk = new bool[KeyMap.Players.Length];
    readonly bool[] feet = new bool[KeyMap.Players.Length];   // 每人的字条现在画在脚下吗（loop-10 回差，见 Feet）

    public static string KeyName(KeyCode k) => k == KeyCode.Space ? "空格" : BaitSelector.KeyName(k);
    public static void Say(int player, string text) { fail[player] = text; failUntil[player] = Time.time + Config.HintFail; }   // 在这人自己的字条位置说 HintFail 秒（钓鱼失败、loop-9 烤鱼）

    // 纯逻辑：库存、玩家号、位置、朝向、树的位置与今天各砍了几下、钓鱼状态、这一局钓上过 / 换过砖没有、在场各人的位置（loop-7，null = 不判人）、
    // 面前那格放下去不切断去码头的路（loop-7 修补：BrickPath.Open 的结果，OnGUI 每 PathEvery 秒算一次传进来；默认 true = 不判）→ 提示文字；不提示返回空串
    public static string Text(Stock s, int player, Vector3 p, Vector3 forward, Vector3[] trees, int[] hits, FishState fish, bool fished, bool traded, Vector3[] people = null, bool open = true)
    {
        var k = KeyMap.Players[player];
        string f = KeyName(k.Fish), gap = k.Fish == KeyCode.Space ? "" : " ";   // 「空格」直接连着中文，Num5 两边空一格
        if (fish == FishState.Bite) return $"{f} 提竿！";
        if (fish == FishState.Reel) return $"按住{gap}{f}{gap}收线，张力快满就松手";
        if (fish != FishState.Idle) return "";   // 抛竿、等鱼、钓上、跑了：不提示
        bool raw = s.Count(ItemType.RawFish, player) > 0, cooked = s.Count(ItemType.CookedFish, player) > 0;   // 自己手里有生鱼 / 熟鱼（loop-9）
        int t = Trees.Nearest(trees, p);
        var c = Bricks.Front(p, forward);   // 同 Bricks.Update：面前那一格（按胶囊前沿取），往上垒一层
        string e = Bricks.AtDoor(p) && (raw || cooked) ? "换砖" : Trees.CanCook(s, player, p) ? "烤鱼" : t >= 0 && hits[t] < Config.ChopHits ? "砍树" : null;   // 同 Trees.Interact 的顺序
        string cast = Config.InFishSpot(p) ? $"{f} 抛竿" : null;
        var all = new[]
        {
            e != null ? $"{KeyName(k.Interact)} {e}" : null,
            Sleep.NearBed(p) ? $"{KeyName(k.R)} 睡觉" : null,
            Bricks.CanPlace(s, c, Bricks.Height(s, c)) && !Bricks.UnderFoot(c, people) && open ? $"{KeyName(k.Build)} 放砖" : null,   // 同 Bricks.Update：面前那格有人、会切断路都不放
            cast,
            raw || cooked ? (traded || e is "换砖" or "烤鱼" ? null : raw && s.Count(ItemType.Wood) > 0 ? "拿生鱼去篝火烤" : cooked ? "拿熟鱼回小屋门口换砖" : "拿生鱼回小屋门口换砖")   // 兜底：排最后补位，人已在那儿（门口换砖 / 火边能烤 / 抛竿）不重复
                : fished || cast != null ? null : (k.Up == KeyCode.W ? "WASD " : "方向键") + "走去码头尽头钓鱼",   // loop-10 说怎么走：P1 WASD，P2 方向键
        };
        return string.Join("　", all.Where(a => a != null).Take(Config.HintMax));
    }

    // 钓鱼失败时这人字条位置说的那句（loop-10 绘本口吻、带这人的键名）：咬钩没按、鱼溜走（Bite→Idle）；张力过满、线断（Reel→Lost）。别的变化返回 null
    public static string Fail(FishState was, FishState now, int player = 0)
    {
        var f = KeyMap.Players[player].Fish;   // 「空格」直接连着中文，Num5 前空一格（同 Text）
        return was == FishState.Bite && now == FishState.Idle ? $"鱼溜走啦，下回一咬钩就按{(f == KeyCode.Space ? "" : " ")}{KeyName(f)}" : was == FishState.Reel && now == FishState.Lost ? "线断啦，张力快满就松手" : null;
    }

    // 两张字条叠在一起（开局两人在床边只隔 0.7 m）：左右分开，各让一半，中间留 4 px；没叠就不动
    public static void Apart(ref Rect a, ref Rect b)
    {
        if (!a.Overlaps(b)) return;
        bool aLeft = a.center.x <= b.center.x;
        float push = ((aLeft ? a.xMax - b.x : b.xMax - a.x) * 0.5f + 2f) * (aLeft ? 1f : -1f);
        a.x -= push; b.x += push;
    }

    // 字条画到脚下吗（loop-10 回差）：头顶位置 headY（字条上沿）高过 HintTop 就到脚下；已在脚下的要比 HintTop 再低 HintBack px 才回头顶，免得在边界上来回跳
    public static bool Feet(bool was, float headY) => headY < Config.HintTop + (was ? Config.HintBack : 0f);

    void Start() { players = FindAnyObjectByType<PlayerManager>(); cam = Camera.main; }

    void Update()   // 每人：站定计时；钓鱼状态和上一帧比，失败就记下那句、OnGUI 画在这人自己的字条位置（打 EVT fishfail，实玩 until 等它）
    {
        if (players == null) return;
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled || p.fishing == null) continue;
            int i = p.playerIndex; var now = p.fishing.Logic.State;
            still[i] = p.controller != null && p.controller.Speed01 > 0f ? 0f : still[i] + Time.deltaTime;
            string say = Fail(last[i], now, i);
            if (say != null) { Debug.Log($"EVT fishfail p{i} {last[i]} t={Time.timeSinceLevelLoad:0.0}"); Say(i, say); }
            if (last[i] != FishState.Bite && now == FishState.Bite) Debug.Log($"EVT bite p{i} t={Time.timeSinceLevelLoad:0.0}");   // 咬钩那一刻（loop-10 从 FishingStateMachine 挪来；实玩脚本 until 等它）
            last[i] = now;
        }
    }

    void OnGUI()
    {
        if (Pause.On || players == null || cam == null) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = Config.HintFont, alignment = TextAnchor.MiddleCenter, font = font };   // 半透明底，同底部提示
        if (ink == null) { ink = new GUIStyle(style); ink.normal.background = ink.hover.background = null; }   // 同字号、同内边距，只写字不画底
        var rs = new Rect[players.players.Length]; var ts = new string[rs.Length]; var who = new int[rs.Length]; var ten = new float[rs.Length]; int n = 0; var at = players.Positions();
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            var f = p.fishing != null ? p.fishing.Logic : null;   // 钓鱼状态；CaughtCount > 0 = 这一局钓上过
            if (Stock.Current.brick > 0 && Time.time >= pathAt[p.playerIndex])   // 手里有砖才算（没砖 CanPlace 本来就不过）
            { pathAt[p.playerIndex] = Time.time + Config.PathEvery; pathOk[p.playerIndex] = BrickPath.Open(Stock.Current, Bricks.Front(p.transform.position, p.transform.forward), at); }
            bool walk = still[p.playerIndex] < Config.HintStill;   // 没站定：兜底当作做过、不画（免得后面那人的字条盖住前面那人）
            string text = Text(Stock.Current, p.playerIndex, p.transform.position, p.transform.forward, Trees.Pos, Trees.Hits, f?.State ?? FishState.Idle, walk || f != null && f.CaughtCount > 0, walk || Trees.TradedOnce[p.playerIndex], at, pathOk[p.playerIndex]);
            if (Time.time < failUntil[p.playerIndex]) text = fail[p.playerIndex];   // 刚失败：说失败那句（Lost 那 1.2 s 里 Text 本来就是空串；下一次咬钩最早在 CastTime + WaitMin 后，盖不住「提竿」）
            Vector3 v = cam.WorldToViewportPoint(p.transform.position + Vector3.up * 1.8f);   // 人在屏幕外不画（同 TopDownCamera 屏外指示点的判据）
            if (text == "" || v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
            Vector3 h = cam.WorldToScreenPoint(p.transform.position + Vector3.up * Config.HintHeight);
            Vector2 size = style.CalcSize(new GUIContent(text));
            float y = Screen.height - h.y - size.y;   // 字的底边在头顶点上；这样会压到顶部「砖 n　木 n」就改画到脚下几像素（不往下夹去压帽子），回差见 Feet
            feet[p.playerIndex] = Feet(feet[p.playerIndex], y);
            if (feet[p.playerIndex]) y = Screen.height - cam.WorldToScreenPoint(p.transform.position).y + Config.HintFeetGap;
            rs[n] = new Rect(h.x - size.x * 0.5f, y, size.x, size.y); who[n] = p.playerIndex; ten[n] = f != null && f.State == FishState.Reel ? f.Tension : -1f; ts[n++] = text;
        }
        if (n == 2) Apart(ref rs[0], ref rs[1]);
        for (int i = 0; i < n; i++)   // 夹在屏幕内，不出屏幕；上沿不高于 HintTop（顶部「砖 n　木 n」框的下沿）
        {
            var r = new Rect(Mathf.Clamp(rs[i].x, 0f, Screen.width - rs[i].width), Mathf.Clamp(rs[i].y, Config.HintTop, Screen.height - rs[i].height), rs[i].width, rs[i].height);
            GUI.Box(r, GUIContent.none, style); GUI.Box(r, GUIContent.none, style);   // 同一个半透明底叠两遍：形状同顶部「砖 n　木 n」，深一倍，浅色字才读得出
            ink.normal.textColor = ink.hover.textColor = Color.black;   // 先在右下偏 1 px 画一遍深色字当阴影
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), ts[i], ink);
            ink.normal.textColor = ink.hover.textColor = Color.Lerp(Config.HatDot[who[i]], Color.white, Config.HintLighten);   // 再画字：帽子色（P1 草帽、P2 帽带，同屏外指示点）往白提亮
            GUI.Label(r, ts[i], ink);
            if (ten[i] >= 0f) GUI.DrawTexture(new Rect(r.x, r.yMax - Config.HintBar, r.width * Mathf.Clamp01(ten[i]), Config.HintBar), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, ten[i] > Config.HintRed ? Config.Bobber : ink.normal.textColor, 0f, 0f);   // 收线：字条下沿一道张力条，宽 = 字条宽 × 张力，快满变 Bobber 红（loop-10）
        }
    }
}
