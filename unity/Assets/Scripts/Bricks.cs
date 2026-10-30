using UnityEngine;

// 砖：在小屋门口按 E（P2 小键盘 1）用自己的一条生鱼换一块，进两人共用的 Stock；没鱼、不在门口都不做事。
// B（P2 小键盘 2）往面前那一格放一块：只放在沙滩上、IsWalkable 为真、不在码头上的格子；先从库存扣 1 块砖，同一格再放失败。
// 边长 0.9、陶土色。成功后底部出现 1.5 秒提示，不常驻库存面板。读档后由 PierSave 调 Rebuild 按记录重建。
public class Bricks : MonoBehaviour
{
    public Material material;
    public Font font;   // 网页端没有系统字体回退：「放下一砖」用 Assets/Fonts 的思源黑体子集（WarmCJK）
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

    public static bool CanPlace(Stock s, Vector2Int c)
    {
        Vector3 p = Center(c, 0);
        if (!Config.OnBeach(p) || !Config.IsWalkable(p) || Config.OnDock(p)) return false;   // 码头上不准放砖
        if (s.Count(ItemType.Brick) < 1) return false;
        foreach (var b in s.bricks) if (b.x == c.x && b.z == c.y) return false;   // 同一格再放失败
        return true;
    }

    // 成功返回新记录（已扣砖、已记入 s.bricks），失败返回 null
    public static BrickRecord TryPlace(Stock s, Vector2Int c)
    {
        if (!CanPlace(s, c) || !s.Take(ItemType.Brick)) return null;
        var r = new BrickRecord { x = c.x, z = c.y, layer = 0 };
        s.bricks.Add(r);
        s.dirty = true;
        return r;
    }

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

    void Start() { players = FindAnyObjectByType<PlayerManager>(); }

    void Update()
    {
        if (toast > 0f) toast -= Time.deltaTime;
        if (Pause.On || players == null) return;
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            var k = KeyMap.Players[p.playerIndex];
            if (InputManager2P.Pressed(k.Interact) && Trade(Stock.Current, p.playerIndex, p.transform.position)) Toast("换得一砖");
            if (!InputManager2P.Pressed(k.Build)) continue;
            var r = TryPlace(Stock.Current, Cell(p.transform.position + p.transform.forward * Config.CellSize));
            if (r == null) continue;
            Spawn(r);
            Toast("放下一砖");
        }
    }

    public void Toast(string text) { toastText = text; toast = 1.5f; }   // 底部 1.5 秒提示（PierSave 睡醒也用它）

    void OnGUI()
    {
        if (toast <= 0f || Pause.On) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, font = font };
        GUI.Box(new Rect((Screen.width - 220f) * 0.5f, Screen.height - 110f, 220f, 44f), toastText, style);
    }
}
