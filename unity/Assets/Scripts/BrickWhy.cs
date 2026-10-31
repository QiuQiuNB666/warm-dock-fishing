using UnityEngine;

// loop-11 放不下要说为什么：面前那格为什么放不下（原因码）。Bricks.CanPlace（不判人、不判路）、头顶「B 放砖」（Hints.Text）、按 B（Bricks.Update）三处都看它，结果一致。
// 先看手里有没有砖，再看保留区（门口那格、码头口）、场景（海里、码头上、被床/屋/树/火占着）、层高，最后看面前有没有人、放了会不会把路堵死（BrickPath.Open 的结果传进来，BFS 只在按 B 时算）。
// 只加说明，不改能不能放：返回 Ok 的条件和 loop-10 的 CanPlace + UnderFoot + BrickPath.Open 逐条相同。Bricks.cs 满了，单放这里。
public static class BrickWhy
{
    public enum Code { Ok, NoBrick, Full, Dock, Door, Person, Path, Blocked, Self }

    // people = 在场各人的位置（null = 不判人）；open = 放下去不切断去码头的路（默认 true = 不判）；me = 放砖人自己的位置（loop-12，可省）：
    // 压到砖位的只有自己（斜站在格子前角时胶囊压进斜对角那格）→ Self「退后半步再放」，有别人 → Person；不传 me 照旧 Person。只分原因，能不能放不变
    public static Code Of(Stock s, Vector2Int c, int layer, Vector3[] people = null, bool open = true, Vector3? me = null)
    {
        Vector3 p = Bricks.Center(c, 0);
        if (s.Count(ItemType.Brick) < 1) return Code.NoBrick;
        if (Bricks.Reserved(c)) return c == Bricks.Cell(Config.DoorPos) ? Code.Door : Code.Dock;
        if (!Config.OnBeach(p) || !Config.IsWalkable(p) || Config.OnDock(p) || Bricks.Occupied(c)) return Code.Blocked;
        if (layer >= Config.MaxHeight) return Code.Full;
        if (layer < 0 || Bricks.Has(s, c, layer) || layer > 0 && !Bricks.Has(s, c, layer - 1)) return Code.Blocked;   // 同格同层、悬空（按 B 时 layer 就是 Height，碰不到）
        if (!Bricks.UnderFoot(c, people)) return open ? Code.Ok : Code.Path;
        return me is Vector3 m && !Bricks.UnderFoot(c, System.Array.FindAll(people, q => q != m)) ? Code.Self : Code.Person;
    }

    // 按 B 没放成时，在放的人自己字条位置说的那句（Hints.Say）；Ok 返回空串。loop-12：这人正在钓鱼（fish 不是 Idle）也返回空串、不说——
    // 说了会把头顶「空格 提竿！」整句换掉 HintFail 秒（正好是咬钩窗口），手滑按到 B 鱼就跑了
    public static string Say(Code w, FishState fish = FishState.Idle) => w switch
    {
        _ when fish != FishState.Idle => "",
        Code.Ok => "",
        Code.NoBrick => "没砖了，拿鱼去门口换",
        Code.Full => "最高四层",
        Code.Dock => "码头口要留路",
        Code.Door => "门口要留路",
        Code.Person => "前面有人",
        Code.Self => "退后半步再放",
        Code.Path => "这样会把路堵死",
        _ => "这里放不下",
    };
}
