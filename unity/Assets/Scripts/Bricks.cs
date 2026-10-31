using System.Collections.Generic;
using UnityEngine;

// 砖：在小屋门口按 E（P2 小键盘 1）用自己的一条生鱼换一块，进两人共用的 Stock；没鱼、不在门口都不做事。E 由 Trees 统一读（门口有鱼先换砖，否则砍树）。
// B（P2 小键盘 2）往面前那一格放一块：只放在沙滩上、IsWalkable 为真、不在码头上、没被床/小屋/树干等占着的格子；先从库存扣 1 块砖，同格往上垒，最多 MaxHeight 层。
// 边长 0.9、陶土色。成功后底部出现 1.5 秒提示；顶部正中常驻一行两人共用的「砖 n　木 n」。读档后由 PierSave 调 Rebuild 按记录重建。
public class Bricks : MonoBehaviour
{
    public Material material;
    public Font font;   // 网页端没有系统字体回退：「放下一砖」用 Assets/Fonts 的思源黑体子集（WarmCJK）
    public GameObject cabin;   // BuildScene 赋值：小屋占着的格子也不能放砖
    PlayerManager players;
    Transform root;
    float toast;
    string toastText;
    GUIStyle style;

    public static bool AtDoor(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Config.DoorPos.x, Config.DoorPos.z)) <= Config.InteractRange;

    // 人在小屋门口、手里有生鱼：鱼 -1，砖 +1（砖两人共用）。否则什么都不做，返回 false。
    public static bool Trade(Stock s, int player, Vector3 pos)
    {
        if (!AtDoor(pos) || !s.Take(ItemType.RawFish, 1, player)) return false;
        s.Add(ItemType.Brick);
        return true;
    }

    public static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Config.CellSize), Mathf.FloorToInt(p.z / Config.CellSize));

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
    public static bool Occupied(Vector2Int c)
    {
        float k = Config.CellSize, e = Config.BlockInset;
        var r = new Rect(c.x * k + e, c.y * k + e, k - 2f * e, k - 2f * e);
        return Blockers.Exists(b => b.Overlaps(r));
    }

    // 只放沙滩上、IsWalkable、不在码头上、没被场景物体占着；最多 MaxHeight 层；没有下层不能放上层，同格同层再放失败；没砖失败
    public static bool CanPlace(Stock s, Vector2Int c, int layer)
    {
        Vector3 p = Center(c, 0);
        if (!Config.OnBeach(p) || !Config.IsWalkable(p) || Config.OnDock(p) || Occupied(c)) return false;   // 码头上不准放砖
        if (s.Count(ItemType.Brick) < 1 || layer < 0 || layer >= Config.MaxHeight) return false;
        return !Has(s, c, layer) && (layer == 0 || Has(s, c, layer - 1));
    }

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
        g.GetComponent<Renderer>().sharedMaterial = material;
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
            var c = Cell(p.transform.position + p.transform.forward * Config.CellSize);
            var r = TryPlace(Stock.Current, c, Height(Stock.Current, c));   // 同格往上垒一层
            if (r == null) continue;
            Spawn(r);
            Toast("放下一砖");
        }
    }

    public void Toast(string text) { toastText = text; toast = 1.5f; }   // 底部 1.5 秒提示（PierSave 睡醒、Trees 换砖砍树也用它）

    void OnGUI()
    {
        if (Pause.On) return;   // 暂停时顶部库存和底部提示都不画
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, font = font };
        GUI.Box(new Rect((Screen.width - 200f) * 0.5f, 8f, 200f, 40f), $"砖 {Stock.Current.brick}　木 {Stock.Current.wood}", style);   // 顶部正中，左上/右上的字让开
        if (toast > 0f) GUI.Box(new Rect((Screen.width - 220f) * 0.5f, Screen.height - 110f, 220f, 44f), toastText, style);
    }
}
