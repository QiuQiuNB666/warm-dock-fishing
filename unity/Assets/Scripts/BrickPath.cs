using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// loop-7 修补 别把路堵死：砖不许切断去码头的路。假设格子 c 放上第 0 层砖，从床点（Sleep.Bed，P2 偏开 P2BedOffset）和在场各人所在格出发，
// 沿沙滩 4 邻接走（格心 OnBeach、没被 Bricks.Occupied、第 0 层没砖；斜角两块砖之间只剩约 0.14 m，人过不去，所以不走斜线），
// 走不到码头入口（Config.DockEntry）任一格就不放。只拦切断路的那一块：本来就走不到的起点不算；起点格本身算能走（人可能贴着床、树干站），
// 起点格就是 c（往床点上放）算走不到。码头上的人不算起点（码头只连着入口）。往上垒（第 0 层已有砖）不改连通，永远不拦。
// 沙滩 50×18 = 900 格：B 按下时算一次（Bricks.Update），头顶「B 放砖」每 Config.PathEvery 秒算一次（Hints.OnGUI）。
public static class BrickPath
{
    static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // people = 在场各人的位置（null = 没人，只看床点）
    public static bool Open(Stock s, Vector2Int c, Vector3[] people)
    {
        var wall = new HashSet<Vector2Int>(s.bricks.Where(b => b.layer == 0).Select(b => new Vector2Int(b.x, b.z)));
        var starts = (people ?? new Vector3[0]).Append(Sleep.Bed).Append(Sleep.Bed + Config.P2BedOffset).Select(q => Bricks.Cell(q)).Distinct();
        return starts.All(f => !Config.OnBeach(Bricks.Center(f, 0)) || Reach(f, wall, c) || !Reach(f, wall, null));   // 码头上的人不算；放了照样走得到，或本来就走不到
    }

    // 从 start 沿能走的格子走得到码头入口吗；cut = 假设放了砖的格子（null = 不假设）
    static bool Reach(Vector2Int start, HashSet<Vector2Int> wall, Vector2Int? cut)
    {
        var seen = new HashSet<Vector2Int> { start }; var todo = new Queue<Vector2Int>();
        if (start != cut) todo.Enqueue(start);
        while (todo.Count > 0)
        {
            var c = todo.Dequeue();
            if (Config.DockEntry.Overlaps(Bricks.Inset(c))) return true;   // 同 Bricks.Reserved 的判据
            foreach (var d in Steps)
            {
                var n = c + d;   // 先记进 seen：走不了的格子也只判一次
                if (seen.Add(n) && n != cut && !wall.Contains(n) && Config.OnBeach(Bricks.Center(n, 0)) && !Bricks.Occupied(n)) todo.Enqueue(n);
            }
        }
        return false;
    }
}
