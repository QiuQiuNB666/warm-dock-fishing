using System.Linq;
using UnityEngine;

// 就近提示（loop-3）：每名玩家头顶一行小字，只列「现在按了就会成功」的动作，最多 HintMax 个，全角空格隔开。
// 顺序：换砖 > 砍树 > 睡觉 > 放砖 > 抛竿；E 只列它实际会做的那一个（门口有鱼只列换砖，否则看 Trees.Nearest 那一棵今天还能不能出木）。
// 判断全用 Bricks / Trees / PierSave / Config 现成的方法，只读，不改规则。暂停、钓鱼过程中（非 Idle）、人在屏幕外都不画。
// 键名同屏幕下方饵按钮（BaitSelector.KeyName：P2 是 Num1/Num2/Num4/Num5），P1 的 Space 叫「空格」。
public class Hints : MonoBehaviour
{
    public Font font;   // WarmCJK（BuildScene 赋值），网页端才显示得出中文
    PlayerManager players;
    Camera cam;
    GUIStyle style;

    public static string KeyName(KeyCode k) => k == KeyCode.Space ? "空格" : BaitSelector.KeyName(k);

    // 纯逻辑：库存、玩家号、位置、朝向、树的位置与今天各砍了几下、钓鱼是否空闲 → 提示文字；什么都做不成返回空串
    public static string Text(Stock s, int player, Vector3 p, Vector3 forward, Vector3[] trees, int[] hits, bool idle)
    {
        if (!idle) return "";   // 钓鱼过程中不提示
        var k = KeyMap.Players[player];
        int t = Trees.Nearest(trees, p);
        var c = Bricks.Cell(p + forward * Config.CellSize);   // 同 Bricks.Update：面前那一格，往上垒一层
        string e = Bricks.AtDoor(p) && s.Count(ItemType.RawFish, player) > 0 ? "换砖" : t >= 0 && hits[t] < Config.ChopHits ? "砍树" : null;
        var all = new[]
        {
            e != null ? $"{KeyName(k.Interact)} {e}" : null,
            PierSave.NearBed(p) ? $"{KeyName(k.R)} 睡觉" : null,
            Bricks.CanPlace(s, c, Bricks.Height(s, c)) ? $"{KeyName(k.Build)} 放砖" : null,
            Config.InFishSpot(p) ? $"{KeyName(k.Fish)} 抛竿" : null,
        };
        return string.Join("　", all.Where(a => a != null).Take(Config.HintMax));
    }

    // 两张字条叠在一起（开局两人在床边只隔 0.7 m）：左右分开，各让一半，中间留 4 px；没叠就不动
    public static void Apart(ref Rect a, ref Rect b)
    {
        if (!a.Overlaps(b)) return;
        bool aLeft = a.center.x <= b.center.x;
        float push = ((aLeft ? a.xMax - b.x : b.xMax - a.x) * 0.5f + 2f) * (aLeft ? 1f : -1f);
        a.x -= push; b.x += push;
    }

    void Start() { players = FindAnyObjectByType<PlayerManager>(); cam = Camera.main; }

    void OnGUI()
    {
        if (Pause.On || players == null || cam == null) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = Config.HintFont, alignment = TextAnchor.MiddleCenter, font = font };   // 半透明底，同底部提示
        var rs = new Rect[players.players.Length]; var ts = new string[rs.Length]; int n = 0;
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            string text = Text(Stock.Current, p.playerIndex, p.transform.position, p.transform.forward, Trees.Pos, Trees.Hits, p.fishing == null || !p.fishing.Busy);
            Vector3 v = cam.WorldToViewportPoint(p.transform.position + Vector3.up * 1.8f);   // 人在屏幕外不画（同 TopDownCamera 屏外指示点的判据）
            if (text == "" || v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
            Vector3 h = cam.WorldToScreenPoint(p.transform.position + Vector3.up * Config.HintHeight);
            Vector2 size = style.CalcSize(new GUIContent(text));
            rs[n] = new Rect(h.x - size.x * 0.5f, Screen.height - h.y - size.y, size.x, size.y); ts[n++] = text;   // 字的底边在头顶点上
        }
        if (n == 2) Apart(ref rs[0], ref rs[1]);
        for (int i = 0; i < n; i++)   // 夹在屏幕内，不出屏幕
            GUI.Box(new Rect(Mathf.Clamp(rs[i].x, 0f, Screen.width - rs[i].width), Mathf.Clamp(rs[i].y, 0f, Screen.height - rs[i].height), rs[i].width, rs[i].height), ts[i], style);
    }
}
