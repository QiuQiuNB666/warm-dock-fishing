using System.Collections.Generic;
using UnityEngine;

// 砖：在小屋门口按 E（P2 小键盘 1）用自己的一条生鱼换一块（loop-9：有熟鱼先用熟鱼，一条换两块），进两人共用的 Stock；没鱼、不在门口都不做事。E 由 Trees 统一读（门口有鱼先换砖，否则火边烤鱼，否则砍树）。
// B（P2 小键盘 2）往面前那一格放一块：只放在沙滩上、IsWalkable 为真、不在码头上、没被床/小屋/树干等占着、不在码头入口和门口的保留区、没人站着、放下不切断去码头的路（BrickPath）的格子；先从库存扣 1 块砖，同格往上垒，最多 MaxHeight 层。
// 边长 0.9、陶土色（垒满 MaxHeight 层的顶块用 M_glow = 小灯塔，loop-11）。成功后底部出现 1.5 秒提示，放不下按原因在自己字条位置说一句（BrickWhy，loop-11）；顶部正中常驻一行两人共用的「砖 n　木 n」。读档后由 PierSave 调 Rebuild 按记录重建。
public class Bricks : MonoBehaviour
{
    public Material material, glow;   // 陶土色；glow = M_glow（BuildScene 从 TimeOfDay 拿，亮度跟时段走）：小灯塔的顶块（loop-11）
    public Font font;   // 网页端没有系统字体回退：「放下一砖」用 Assets/Fonts 的思源黑体子集（WarmCJK）
    public GameObject cabin;   // BuildScene 赋值：小屋占着的格子也不能放砖
    PlayerManager players;
    Transform root;
    float toast;
    string toastText;
    GUIStyle style;

    public static bool AtDoor(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Config.DoorPos.x, Config.DoorPos.z)) <= Config.InteractRange;

    // 人在小屋门口：有熟鱼先用熟鱼，一条换 CookedBricks 块（loop-9）；没熟鱼一条生鱼换一块（砖两人共用）。都没有、不在门口什么都不做，返回 false。
    public static bool Trade(Stock s, int player, Vector3 pos)
    {
        int n = !AtDoor(pos) ? 0 : s.Take(ItemType.CookedFish, 1, player) ? Config.CookedBricks : s.Take(ItemType.RawFish, 1, player) ? 1 : 0;
        if (n > 0) s.Add(ItemType.Brick, n);
        return n > 0;
    }

    public static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Config.CellSize), Mathf.FloorToInt(p.z / Config.CellSize));

    // loop-7 修补：「面前那格」按胶囊前沿取，往前 CellSize + PlayerRadius − BlockInset（1.3 m）：人朝正方向时自己的胶囊压不到这格的砖位
    //（原来往前 1 m，人站在自己格子靠前 0.3 m 里时面前那格判成有人）。同格往上垒照旧：被面前的砖挡住时人离格子前沿至少 0.3 m
    public static Vector2Int Front(Vector3 p, Vector3 forward) => Cell(p + forward * (Config.CellSize + Config.PlayerRadius - Config.BlockInset));

    public static Vector3 Center(Vector2Int c, int layer) =>
        new Vector3((c.x + 0.5f) * Config.CellSize, Config.GroundY + (layer + 0.5f) * Config.BrickSize, (c.y + 0.5f) * Config.CellSize);

    public static bool Has(Stock s, Vector2Int c, int layer) => s.bricks.Exists(b => b.x == c.x && b.z == c.y && b.layer == layer);

    public static int Height(Stock s, Vector2Int c)   // 这一格从下往上连着垒了几层 = 下一块该放的层号
    {
        int h = 0;
        while (Has(s, c, h)) h++;
        return h;
    }

    // 挡砖的 XZ 矩形（loop-4）：床、小屋、树干等场景物体占着的地方，开局由 Start 收集。格子内缩 BlockInset 再判相交，只擦边不算
    public static readonly List<Rect> Blockers = new List<Rect>();
    public static void Block(Bounds b) => Blockers.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
    public static bool Occupied(Vector2Int c) => Blockers.Exists(b => b.Overlaps(Inset(c)));
    public static Rect Inset(Vector2Int c)   // 格子四边各内缩 BlockInset（0.05 = 边长 BrickSize 的砖在格子里居中留的边，即砖的占地）
    {
        float k = Config.CellSize, e = Config.BlockInset;
        return new Rect(c.x * k + e, c.y * k + e, k - 2f * e, k - 2f * e);
    }

    // loop-7 保留：码头入口（Config.DockEntry）和小屋门口那一格永远不放砖，去码头、进门的路堵不死。不进 Blockers（开局 Start 和测试都会清它）；老档里已放在这儿的砖照样读回
    public static bool Reserved(Vector2Int c) => c == Cell(Config.DoorPos) || Config.DockEntry.Overlaps(Inset(c));

    // loop-7 砖不砸人：有人站着的格子不放——人的 XZ 位置所在格，和胶囊（半径 PlayerRadius）压进砖位的相邻格（所在格离砖位不到 0.08 m，同一个判据就包含了）。
    // people = 在场各人的位置（含放砖的自己），null = 没人
    public static bool UnderFoot(Vector2Int c, Vector3[] people)
    {
        var r = Inset(c);
        return people != null && System.Array.Exists(people, q =>
            new Vector2(Mathf.Clamp(q.x, r.xMin, r.xMax) - q.x, Mathf.Clamp(q.z, r.yMin, r.yMax) - q.z).sqrMagnitude < Config.PlayerRadius * Config.PlayerRadius);
    }

    // 只放沙滩上、IsWalkable、不在码头上、没被场景物体占着、不在保留区；最多 MaxHeight 层；没有下层不能放上层，同格同层再放失败；没砖失败（loop-11 判断搬进 BrickWhy，不判人、不判路）
    public static bool CanPlace(Stock s, Vector2Int c, int layer) => BrickWhy.Of(s, c, layer) == BrickWhy.Code.Ok;

    // loop-11 小灯塔：一格垒到第 MaxHeight 层，顶上那块用 M_glow（Spawn；读档 Rebuild 同样按记录判断）
    public static bool Lighthouse(BrickRecord r) => r.layer == Config.MaxHeight - 1;

    // 成功返回新记录（已扣砖、已记入 s.bricks、dirty），失败返回 null
    public static BrickRecord TryPlace(Stock s, Vector2Int c, int layer)
    {
        if (!CanPlace(s, c, layer) || !s.Take(ItemType.Brick)) return null;
        var r = new BrickRecord { x = c.x, z = c.y, layer = layer };
        s.bricks.Add(r);
        s.dirty = true;
        return r;
    }

    public static BrickRecord TryPlace(Stock s, Vector2Int c) => TryPlace(s, c, 0);   // 放第一层（子阶段 4 的接口）

    public void Rebuild()
    {
        if (root != null) Destroy(root.gameObject);
        root = null;
        foreach (var r in Stock.Current.bricks) Spawn(r);
    }

    void Spawn(BrickRecord r)
    {
        if (root == null) root = new GameObject("Bricks").transform;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Brick";
        g.transform.SetParent(root, false);
        g.transform.position = Center(new Vector2Int(r.x, r.z), r.layer);
        g.transform.localScale = Vector3.one * Config.BrickSize;
        g.GetComponent<Renderer>().sharedMaterial = Lighthouse(r) ? glow : material;
    }

    // 挡砖的矩形 = 场景碰撞体里伸出地面的（顶面高出 GroundY 不到 BlockInset 的是沙滩台地、海底、木板、码头桩，不算）；人（CharacterController）和砖自己不算。
    // 小屋模型没有碰撞体（这圈不给它加，免得改了人的走位），按它的网格包围盒另算。
    void Start()
    {
        players = FindAnyObjectByType<PlayerManager>();
        Blockers.Clear();
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (!(c is CharacterController) && c.name != "Brick" && c.bounds.max.y > Config.GroundY + Config.BlockInset) Block(c.bounds);
        if (cabin != null) foreach (var r in cabin.GetComponentsInChildren<Renderer>()) Block(r.bounds);
        Debug.Log($"BRICK-BLOCKERS {Blockers.Count}");
    }

    void Update()
    {
        if (toast > 0f) toast -= Time.deltaTime;
        if (Pause.On || players == null) return;
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            var k = KeyMap.Players[p.playerIndex];
            if (!InputManager2P.Pressed(k.Build)) continue;
            int i = p.playerIndex; var c = Front(p.transform.position, p.transform.forward); var at = players.Positions(); int h = Height(Stock.Current, c);
            var why = BrickWhy.Of(Stock.Current, c, h, at, BrickPath.Open(Stock.Current, c, at));   // loop-7：面前那格有人、放了会切断去码头的路都不放；同格往上垒一层（loop-11 原因码）
            var r = why == BrickWhy.Code.Ok ? TryPlace(Stock.Current, c, h) : null;
            Debug.Log($"EVT build p{i} cell={c.x},{c.y} {(r != null ? "ok" : "no " + why)}");   // 实玩查面前是哪一格、放没放、为什么
            if (r == null) { Hints.Say(i, BrickWhy.Say(why)); continue; }   // loop-11：放不下按原因说一句，在自己字条位置
            Spawn(r);
            Toast("放下一砖");
            if (Lighthouse(r)) { Hints.Say(i, "垒成一座小灯塔"); Debug.Log($"EVT lighthouse p{i} cell={c.x},{c.y}"); }
        }
    }

    public void Toast(string text) { toastText = text; toast = 1.5f; }   // 底部 1.5 秒提示（PierSave 睡醒、Trees 换砖砍树也用它）

    void OnGUI()
    {
        if (Pause.On) return;   // 暂停时顶部库存和底部提示都不画
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, font = font };
        GUI.Box(new Rect((Screen.width - 200f) * 0.5f, 8f, 200f, Config.TopBarBottom - 8f), $"砖 {Stock.Current.brick}　木 {Stock.Current.wood}", style);   // 顶部正中，左上/右上的字让开
        if (toast > 0f) GUI.Box(new Rect((Screen.width - 220f) * 0.5f, Screen.height - 110f, 220f, 44f), toastText, style);
    }
}
