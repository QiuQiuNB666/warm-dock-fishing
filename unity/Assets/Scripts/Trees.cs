using System.Linq;
using UnityEngine;

// 砍树（挂在 Nature 上，子物体里名叫 Tree 的就是树，生成顺序 = 树号）：人离树干（树的 XZ 位置）≤ ChopRange 按 E（P2 小键盘 1）砍一下，
// 树绕树根晃一下；满 ChopHits 下得 1 木，进两人共用的 Stock.wood。每棵每天只出 1 木，再砍只提示「这棵明天再砍」；睡醒（Sleep.ToMorning）全部重置。
// 砍的状态不进存档。E 同一时刻只做一件事：门口有鱼先换砖（Bricks.Trade，熟鱼优先），否则火边有生鱼就烤（loop-9 Cook），否则砍够得着的最近一棵，都不是就什么都不做。
public class Trees : MonoBehaviour
{
    public const int Traded = -2, Cooked = -3, NoWood = -4;   // Interact 的返回值：这一下换了砖 / 烤好一条鱼 / 火边有生鱼但没木（loop-9）
    public static readonly bool[] TradedOnce = new bool[KeyMap.Players.Length];   // 这一局各人换过砖没有（Hints 兜底用；只在内存，不进存档）
    public static int[] Hits = new int[0];   // 每棵今天砍了几下，到 ChopHits = 今天已出过木
    Transform[] trees = new Transform[0];
    public static Vector3[] Pos = new Vector3[0];   // 每棵的位置（树号顺序），Hints 只读
    Quaternion[] rest;
    float[] shake;
    PlayerManager players;
    Bricks bricks;

    public static void NewDay() => System.Array.Clear(Hits, 0, Hits.Length);   // 睡醒：所有树重置

    // 够得着（XZ 距离 ≤ ChopRange）、人站在树南侧（z ≤ 树干 z + ChopSouth，loop-7：北侧会被树冠挡住）的最近一棵的下标；都不是返回 -1
    public static int Nearest(Vector3[] trees, Vector3 p)
    {
        int best = -1;
        float bestD = Config.ChopRange;
        for (int k = 0; k < trees.Length; k++)
        {
            if (p.z > trees[k].z + Config.ChopSouth) continue;
            float d = Vector2.Distance(new Vector2(trees[k].x, trees[k].z), new Vector2(p.x, p.z));
            if (d <= bestD) { best = k; bestD = d; }
        }
        return best;
    }

    public static bool NearFire(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Config.FirePos.x, Config.FirePos.z)) <= Config.InteractRange;

    // loop-9 火边烤一条：这名玩家有生鱼、两人共用木 ≥1 → 木 −1、他的生鱼 −1、他的熟鱼 +1，返回 Cooked；有鱼没木返回 NoWood（说「没柴了」）；不在火边或没生鱼返回 -1（不说话）
    public static int Cook(Stock s, int player, Vector3 p)
    {
        if (!NearFire(p) || s.Count(ItemType.RawFish, player) < 1) return -1;
        if (!s.Take(ItemType.Wood)) return NoWood;
        s.Take(ItemType.RawFish, 1, player); s.Add(ItemType.CookedFish, 1, player);
        return Cooked;
    }

    // E：门口有鱼先换砖（Traded）；否则火边有生鱼就烤（Cooked / NoWood）；否则返回够得着的最近一棵的下标（交给 Chop）；都不是返回 -1
    public static int Interact(Stock s, int player, Vector3 p, Vector3[] trees)
    {
        if (Bricks.Trade(s, player, p)) return Traded;
        int c = Cook(s, player, p);
        return c != -1 ? c : Nearest(trees, p);
    }

    // 砍第 k 棵一下：今天已出过木返回 0（不加木、不晃）；否则返回这是第几下，第 ChopHits 下木 +1
    public static int Chop(Stock s, int[] hits, int k)
    {
        if (hits[k] >= Config.ChopHits) return 0;
        if (++hits[k] == Config.ChopHits) s.Add(ItemType.Wood);
        return hits[k];
    }

    void Start()
    {
        players = FindAnyObjectByType<PlayerManager>(); bricks = FindAnyObjectByType<Bricks>();
        trees = transform.Cast<Transform>().Where(t => t.name == "Tree").ToArray();
        Pos = trees.Select(t => t.position).ToArray();
        rest = trees.Select(t => t.rotation).ToArray();
        shake = new float[trees.Length];
        Hits = new int[trees.Length];
    }

    void Update()
    {
        for (int k = 0; k < trees.Length; k++)   // 晃：模型原点在树根，绕它左右摆、幅度衰减到 0，最后回到原来的旋转；位置、缩放不动
        {
            if (shake[k] <= 0f) continue;
            shake[k] -= Time.deltaTime;
            float u = 1f - Mathf.Max(shake[k], 0f) / Config.ChopShakeSec;
            trees[k].rotation = Quaternion.AngleAxis(Config.ChopShakeDeg * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u), Vector3.forward) * rest[k];
        }
        if (Pause.On || players == null) return;
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled || !InputManager2P.Pressed(KeyMap.Players[p.playerIndex].Interact)) continue;
            int i = p.playerIndex, n = Stock.Current.Count(ItemType.CookedFish, i) > 0 ? Config.CookedBricks : 1;   // 门口先用熟鱼：有熟鱼这一下换两砖
            int k = Interact(Stock.Current, i, p.transform.position, Pos);
            if (k == Traded) { Toast(n > 1 ? "换得两砖" : "换得一砖"); TradedOnce[i] = true; Debug.Log($"EVT trade p{i} bricks={n}"); }
            if (k == Cooked) { Hints.Say(i, "烤好一条"); Debug.Log($"EVT cook p{i}"); }   // loop-9：说在自己的字条位置（同钓鱼失败那句），不走底部 Toast
            if (k == NoWood) Hints.Say(i, "没柴了，去砍树");
            if (k < 0) continue;   // 换了砖、烤了鱼（或没柴），或不在树旁也不在门口
            int h = Chop(Stock.Current, Hits, k);
            if (h == 0) { Toast("这棵明天再砍"); continue; }
            shake[k] = Config.ChopShakeSec;
            Debug.Log($"EVT chop p{p.playerIndex} tree={k} hits={h}");
            if (h == Config.ChopHits) Toast("得一木");
        }
    }

    void Toast(string text) { if (bricks != null) bricks.Toast(text); }
}
