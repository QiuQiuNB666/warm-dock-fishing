using NUnit.Framework;
using UnityEngine;

// 玩法纯逻辑测试，接着 PlayTests（满了）往下放。不开场景、不调 Input。
// 往 Bricks.Blockers（静态）里塞矩形的测试，进出都清空：别的测试照旧看到空列表，即原规则。
public class PlayTests2
{
    // loop-4：假的一根树干压着 (-8,-8) 格，往右只擦进 (-7,-8) 格 2 cm：压着的不能放、B 放不下也不扣砖；擦边的（格子内缩 5 cm 再判）和旁边的能放
    [Test] public void BlockedCellNoBrick()
    {
        var s = new Stock { brick = 1 }; var c = new Vector2Int(-8, -8);
        Bricks.Blockers.Clear(); Bricks.Blockers.Add(Rect.MinMaxRect(-7.3f, -7.6f, -6.98f, -7.4f));
        try
        {
            Assert.IsFalse(Bricks.CanPlace(s, c, 0), "压着的格子");
            Assert.IsNull(Bricks.TryPlace(s, c), "B 放不下");
            Assert.AreEqual(1, s.brick, "没扣砖");
            Assert.IsTrue(Bricks.CanPlace(s, c + Vector2Int.right, 0), "只擦边 2 cm");
            Assert.IsTrue(Bricks.CanPlace(s, c + Vector2Int.up, 0), "旁边的格子");
        }
        finally { Bricks.Blockers.Clear(); }
    }

    // loop-4：床（Config：中心 BedPos + BedFromSpawn，尺寸 BedSize）压着的格子不能放。开局两人都面朝 +Z，面前那一格就是床：
    // P2 头顶只剩「Num4 睡觉」（loop-3 实玩开局的「Num2 放砖」就是往床里放）；床和树之间的空沙地照样能放
    [Test] public void BedCellsNoBrick()
    {
        var s = new Stock { brick = 3 };
        Vector3 p2 = Config.BedPos + Config.P2BedOffset, open = new Vector3(-5.2f, Config.GroundY, -6.3f);
        string Hint(int player, Vector3 p) => Hints.Text(s, player, p, Vector3.forward, new Vector3[0], new int[0], true);
        Bricks.Blockers.Clear(); Bricks.Block(new Bounds(Config.BedPos + Config.BedFromSpawn, Config.BedSize));
        try
        {
            foreach (var p in new[] { Config.BedPos, p2 })
                Assert.IsFalse(Bricks.CanPlace(s, Bricks.Cell(p + Vector3.forward * Config.CellSize), 0), $"{p} 面前是床");
            Assert.AreEqual("Num4 睡觉", Hint(1, p2));
            Assert.AreEqual("B 放砖", Hint(0, open));
        }
        finally { Bricks.Blockers.Clear(); }
    }
}
