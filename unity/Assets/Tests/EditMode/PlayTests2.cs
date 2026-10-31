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
        string Hint(int player, Vector3 p) => Hints.Text(s, player, p, Vector3.forward, new Vector3[0], new int[0], FishState.Idle, true, true);   // loop-6：兜底关掉 = 原规则
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

    // loop-6 教学提示：钓鱼时只在咬钩、收线两步提示，键名各用各的；兜底排在能做的事后面补位，做过一次就不再出
    static Vector3 Sand => PlayTests.Open;   // 床和树之间的空沙地：没砖、没树，什么都做不成
    static string Teach(Stock s, int player, Vector3 p, FishState f = FishState.Idle, bool fished = false, bool traded = false) =>
        Hints.Text(s, player, p, Vector3.forward, new Vector3[0], new int[0], f, fished, traded);

    [Test] public void HintFishingP1()
    {
        Assert.AreEqual("空格 提竿！", Teach(new Stock(), 0, Config.FishSpot, FishState.Bite));
        Assert.AreEqual("按住空格收线，张力快满就松手", Teach(new Stock(), 0, Config.FishSpot, FishState.Reel));
        foreach (var f in new[] { FishState.Cast, FishState.Wait, FishState.Caught, FishState.Lost })
            Assert.AreEqual("", Teach(new Stock(), 0, Config.FishSpot, f), $"{f} 不提示");
    }

    [Test] public void HintFishingP2()
    {
        Assert.AreEqual("Num5 提竿！", Teach(new Stock(), 1, Config.FishSpot, FishState.Bite));
        Assert.AreEqual("按住 Num5 收线，张力快满就松手", Teach(new Stock(), 1, Config.FishSpot, FishState.Reel));
    }

    [Test] public void HintFallbackOnce()
    {
        var s = new Stock();
        Assert.AreEqual("去码头尽头钓鱼", Teach(s, 0, Sand), "没鱼");
        Assert.AreEqual("", Teach(s, 0, Sand, fished: true), "钓上过一条：不再出");
        s.Add(ItemType.RawFish, 1, 0);
        Assert.AreEqual("拿生鱼回小屋门口换砖", Teach(s, 0, Sand, fished: true), "有鱼");
        Assert.AreEqual("", Teach(s, 0, Sand, fished: true, traded: true), "换过砖：不再出");
        Assert.AreEqual("去码头尽头钓鱼", Teach(s, 1, Sand), "鱼是 P1 的，P2 还是没鱼");
    }

    [Test] public void HintActionBeatsFallback()
    {
        var s = new Stock(); s.Add(ItemType.RawFish, 1, 0);
        Assert.AreEqual("空格 抛竿", Teach(new Stock(), 0, Config.FishSpot), "钓点上：只列抛竿，不再补「去码头尽头钓鱼」");
        Assert.AreEqual("E 换砖", Teach(s, 0, Config.DoorPos), "门口有鱼：只列换砖，不再补「拿生鱼回小屋门口换砖」");
    }

    // loop-6 修补：出生点（床边，新开局没砖）还没钓上过 → 睡觉后面补位「去码头尽头钓鱼」
    [Test] public void HintSpawnFallback()
    {
        Assert.AreEqual("R 睡觉　去码头尽头钓鱼", Teach(new Stock(), 0, Config.BedPos), "P1 出生点");
        Assert.AreEqual("Num4 睡觉　去码头尽头钓鱼", Teach(new Stock(), 1, PlayTests.P2Bed), "P2 出生点");
    }

    // loop-6 修补：钓鱼状态变化 → 底部说哪句（Hints.Update 每帧拿上一帧比）
    [Test] public void FishFailToast()
    {
        Assert.AreEqual("鱼跑了，咬钩就按", Hints.Fail(FishState.Bite, FishState.Idle), "咬钩没按");
        Assert.AreEqual("线断了，快满就松手", Hints.Fail(FishState.Reel, FishState.Lost), "张力过满");
        Assert.IsNull(Hints.Fail(FishState.Bite, FishState.Reel), "提了竿");
        Assert.IsNull(Hints.Fail(FishState.Reel, FishState.Caught), "钓上");
        Assert.IsNull(Hints.Fail(FishState.Lost, FishState.Idle), "断线后回空闲");
        Assert.IsNull(Hints.Fail(FishState.Bite, FishState.Bite), "没变");
    }
}
