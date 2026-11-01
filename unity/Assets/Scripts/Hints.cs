using System.Linq;
using UnityEngine;

// 就近提示（loop-3）：每名玩家头顶一行小字，只列「现在按了就会成功」的动作，最多 HintMax 个，全角空格隔开。
// 顺序：换砖 > 烤鱼 > 砍树 > 睡觉 > 放砖 > 抛竿 > 兜底 > 抱抱（loop-13：排在兜底之后，只补空位；站定 HintStill 秒才画，同兜底；loop-16 床边还没钓过鱼不补）；E 只列它实际会做的那一个（门口有鱼只列换砖；火边有生鱼、有木列烤鱼（loop-9）；否则看 Trees.Nearest 那一棵今天还能不能出木）。
// loop-6 教学：钓鱼过程中只在咬钩（提竿）、收线两步提示，抛竿/等鱼/钓上/跑了不画；兜底指路排在现有动作后面补位（不足 HintMax 才露出来）：
// 没鱼「WASD 走去码头尽头钓鱼」（P2「方向键走…」，loop-10）、有生鱼「拿生鱼回小屋门口换砖」（有熟鱼说「拿熟鱼…」，loop-9；有生鱼有木说「拿生鱼去篝火烤」，火边能烤时让给「E 烤鱼」，loop-10），这名玩家这一局钓上过一条（FishingLogic.CaughtCount）/ 换过一次砖（Trees.TradedOnce）后
// 各自不再出（只记在内存；「拿生鱼去篝火烤」改看烤过一次没有 Trees.CookedOnce，loop-11），人已在那儿（列了抛竿 / 换砖）不重复，站定 HintStill 秒才画。钓鱼失败（鱼跑了、线断了）在失败那人自己的字条位置说 HintFail 秒（Say；loop-9 烤鱼那句也用它）。
// 判断全用 Bricks / Trees / PierSave / Config 现成的方法，只读，不改规则。暂停、人在屏幕外都不画；头顶会压到顶部「砖 n　木 n」就画到脚下（回差见 Feet）；收线时字条下沿画一道张力条（loop-10）。
// 键名同屏幕下方饵按钮（BaitSelector.KeyName：P2 是 Num1/Num2/Num4/Num5），P1 的 Space 叫「空格」。
public partial class Hints : MonoBehaviour
{
    public Font font;   // WarmCJK（BuildScene 赋值），网页端才显示得出中文
    PlayerManager players;
    TimeOfDay tod;   // loop-16 黄昏前头顶不列睡觉（Sleep.Evening）
    Camera cam;
    readonly FishState[] last = new FishState[KeyMap.Players.Length];   // 每人上一帧的钓鱼状态（Update 比对，失败时说一句）
    static readonly string[] fail = new string[KeyMap.Players.Length];   // 每人刚说的那句（钓鱼失败；loop-9 烤鱼，见 Say）：Time.time < failUntil 时代替 Text 画在自己的字条位置（不走底部 Toast：正中会压住落后那人，也不分人）
    static readonly float[] failUntil = new float[KeyMap.Players.Length];
    readonly float[] still = new float[KeyMap.Players.Length];   // 每人站定了几秒（控制器 Speed01 为 0 就累计）

    public static string KeyName(KeyCode k) => k == KeyCode.Space ? "空格" : BaitSelector.KeyName(k);
    public static void Say(int player, string text, float secs = Config.HintFail) { if (string.IsNullOrEmpty(text)) return; fail[player] = text; failUntil[player] = Time.time + secs; }   // 在这人自己的字条位置说 secs 秒（默认 HintFail：钓鱼失败、loop-9 烤鱼、loop-11 放不下砖；loop-18 垒成小灯塔那句长，传 HintLong）；空串不说（loop-12：钓鱼中按 B 放不下给空串，记下来会把字条整个清掉）
    public static string Said(int player) => fail[player];   // 这人最后说的那句（不管过没过时；测试看「天亮了」两人都说）

    // loop-18「天亮了」只一套规矩：自然天亮（TimeOfDay.Dawn）和睡醒（Sleep.ToMorning）都走这里，busy[k] = 第 k 人正在钓鱼就跳过（null = 都说）
    public static void Morning(bool[] busy) { for (int k = 0; k < fail.Length; k++) if (busy == null || !busy[k]) Say(k, "天亮了"); }

    // 纯逻辑：库存、玩家号、位置、朝向、树的位置与今天各砍了几下、钓鱼状态、这一局钓上过 / 换过砖没有、在场各人的位置（loop-7，null = 不判人）、
    // 面前那格放下去不切断去码头的路（loop-7 修补：BrickPath.Open 的结果，OnGUI 每 PathEvery 秒算一次传进来；默认 true = 不判）、这一局烤过鱼没有（loop-11 Trees.CookedOnce）、
    // 到黄昏没有（loop-16 Sleep.Evening：黄昏前床边不列睡觉，R 照旧能睡；默认 true = 不判时段）→ 提示文字；不提示返回空串
    public static string Text(Stock s, int player, Vector3 p, Vector3 forward, Vector3[] trees, int[] hits, FishState fish, bool fished, bool traded, Vector3[] people = null, bool open = true, bool cookedOnce = false, bool hug = false, bool evening = true)
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
            evening && Sleep.NearBed(p) ? $"{KeyName(k.R)} 睡觉" : null,
            BrickWhy.Of(s, c, Bricks.Height(s, c), people, open) == BrickWhy.Code.Ok ? $"{KeyName(k.Build)} 放砖" : null,   // 同 Bricks.Update（同一个 BrickWhy.Of）：面前那格有人、会切断路都不放
            cast,
            raw || cooked ? (e is "换砖" or "烤鱼" ? null : raw && s.Count(ItemType.Wood) > 0 && !cookedOnce ? "拿生鱼去篝火烤" : traded ? null : cooked ? "拿熟鱼回小屋门口换砖" : "拿生鱼回小屋门口换砖")   // 兜底：排最后补位，人已在那儿（门口换砖 / 火边能烤 / 抛竿）不重复；去烤那句烤过一次才关（loop-11，不再被换过砖关掉），回门口换那两句换过砖就关
                : fished || cast != null ? null : (k.Up == KeyCode.W ? "WASD " : "方向键") + "走去码头尽头钓鱼",   // loop-10 说怎么走：P1 WASD，P2 方向键
            hug && (fished || !Sleep.NearBed(p)) ? $"{KeyName(k.Hug)} 抱抱" : null,   // loop-13 两人能抱（Hug.Ready）：排最后，前面不满 HintMax 才露出来；loop-16 修补：床边还没钓过鱼不补（白天不列睡觉空出的那格不给它，开局只剩走路兜底）
        };
        return string.Join("　", all.Where(a => a != null).Take(Config.HintMax));
    }

    // loop-18 OnGUI 那一行合成静态（Text → Shown），删了接线测试会挂：刚说过一句（saying）整句换成 said，这人咬钩时照样是「提竿」
    public static string Line(Stock s, int player, Vector3 p, Vector3 forward, Vector3[] trees, int[] hits, FishState fish, bool fished, bool traded, Vector3[] people = null, bool open = true, bool cookedOnce = false, bool hug = false, bool evening = true, string said = null, bool saying = false)
        => Shown(Text(s, player, p, forward, trees, hits, fish, fished, traded, people, open, cookedOnce, hug, evening), said, saying, fish);

    // 钓鱼失败时这人字条位置说的那句（loop-10 绘本口吻、带这人的键名）：咬钩没按、鱼溜走（Bite→Idle）；张力过满、线断（Reel→Lost）。别的变化返回 null
    public static string Fail(FishState was, FishState now, int player = 0)
    {
        var f = KeyMap.Players[player].Fish;   // 「空格」直接连着中文，Num5 前空一格（同 Text）
        return was == FishState.Bite && now == FishState.Idle ? $"鱼溜走啦，下回一咬钩就按{(f == KeyCode.Space ? "" : " ")}{KeyName(f)}" : was == FishState.Reel && now == FishState.Lost ? "线断啦，张力快满就松手" : null;
    }

    // loop-16 钓上那一刻在这人自己的字条位置说的那句（FishingStateMachine 记账处调 Say）：中文鱼名查 Config.FishNamesZh（和 FishNames 一一对应）
    public static string Caught(string fish) => $"钓上一条{Config.FishNamesZh[System.Array.IndexOf(Config.FishNames, fish)]}！";
    public static string Lantern => $"灯塔引来一条{Config.LanternZh}！";   // loop-17 夜里被灯塔引来的那条（记熟鱼）代替上面那句

    void Start() { players = FindAnyObjectByType<PlayerManager>(); tod = FindAnyObjectByType<TimeOfDay>(); cam = Camera.main; }

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
}
