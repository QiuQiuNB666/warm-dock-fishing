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
                Assert.IsFalse(Bricks.CanPlace(s, Bricks.Front(p, Vector3.forward), 0), $"{p} 面前是床");
            Assert.AreEqual("Num4 睡觉", Hint(1, p2));
            Assert.AreEqual("B 放砖", Hint(0, open));
        }
        finally { Bricks.Blockers.Clear(); }
    }

    // loop-6 教学提示：钓鱼时只在咬钩、收线两步提示，键名各用各的；兜底排在能做的事后面补位，做过一次就不再出
    static Vector3 Sand => PlayTests.Open;   // 床和树之间的空沙地：没砖、没树，什么都做不成
    static string Teach(Stock s, int player, Vector3 p, FishState f = FishState.Idle, bool fished = false, bool traded = false, bool evening = true) =>
        Hints.Text(s, player, p, Vector3.forward, new Vector3[0], new int[0], f, fished, traded, evening: evening);

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
        Assert.AreEqual("WASD 走去码头尽头钓鱼", Teach(s, 0, Sand), "没鱼");
        Assert.AreEqual("", Teach(s, 0, Sand, fished: true), "钓上过一条：不再出");
        s.Add(ItemType.RawFish, 1, 0);
        Assert.AreEqual("拿生鱼回小屋门口换砖", Teach(s, 0, Sand, fished: true), "有鱼");
        Assert.AreEqual("", Teach(s, 0, Sand, fished: true, traded: true), "换过砖：不再出");
        Assert.AreEqual("方向键走去码头尽头钓鱼", Teach(s, 1, Sand), "鱼是 P1 的，P2 还是没鱼");
    }

    [Test] public void HintActionBeatsFallback()
    {
        var s = new Stock(); s.Add(ItemType.RawFish, 1, 0);
        Assert.AreEqual("空格 抛竿", Teach(new Stock(), 0, Config.FishSpot), "钓点上：只列抛竿，不再补「去码头尽头钓鱼」");
        Assert.AreEqual("E 换砖", Teach(s, 0, Config.DoorPos), "门口有鱼：只列换砖，不再补「拿生鱼回小屋门口换砖」");
    }

    // loop-6 修补：出生点（床边，新开局没砖）还没钓上过 → 补位「去码头尽头钓鱼」。loop-16 有意改规则：开局是早晨，黄昏前床边不列睡觉，只剩这一句（原断言「R 睡觉　WASD 走…」；入夜床边、两人挨着能抱（Teach 不传 hug）见 PlayTests5）
    [Test] public void HintSpawnFallback()
    {
        Assert.AreEqual("WASD 走去码头尽头钓鱼", Teach(new Stock(), 0, Config.BedPos, evening: false), "P1 出生点");
        Assert.AreEqual("方向键走去码头尽头钓鱼", Teach(new Stock(), 1, PlayTests.P2Bed, evening: false), "P2 出生点");
    }

    // loop-7 别把路堵死：码头入口保留区（码头根部朝沙滩 3 格深、比码头左右各宽 1 格）和小屋门口那一格不能放，保留区外紧挨着的一格能放
    //（修补：列的都是只被保留区挡住的格子；(0,-1) 格心在码头上，原规则就挡，换成 (0,-2)）
    [Test] public void DockEntryAndDoorReserved()
    {
        var s = new Stock { brick = 1 };
        foreach (var c in new[] { new Vector2Int(0, -2), new Vector2Int(1, -1), new Vector2Int(-3, -1), new Vector2Int(2, -3) })
            Assert.IsFalse(Bricks.CanPlace(s, c, 0), $"{c} 在保留区里");
        foreach (var c in new[] { new Vector2Int(0, -4), new Vector2Int(-4, -1), new Vector2Int(3, -2) })
            Assert.IsTrue(Bricks.CanPlace(s, c, 0), $"{c} 保留区外一格");
        Assert.IsFalse(Bricks.CanPlace(s, Bricks.Cell(Config.DoorPos), 0), "门口那一格");
        Assert.AreEqual("", Teach(s, 0, new Vector3(0.5f, Config.GroundY, -2.5f), fished: true), "码头入口前面朝码头：不出「B 放砖」");
    }

    // loop-7 砖不砸人：有人站着的格子、胶囊压进砖位的相邻格不能放，离得够远的照样能放；提示跟着：P2 站在 P1 面前一格就不出「B 放砖」
    [Test] public void NoBrickOnPeople()
    {
        Vector3 p1 = new Vector3(-5.5f, Config.GroundY, -6.5f), p2 = p1 + Vector3.forward * Config.CellSize;   // 床和树之间的空沙地，两人都站在格子正中
        var c = Bricks.Cell(p2); var s = new Stock { brick = 1 };
        Vector3 Side(float d) => new Vector3((c.x + 1) * Config.CellSize + d, Config.GroundY, p2.z);   // 右边隔壁格里、离这格 d m
        string Hint(params Vector3[] at) => Hints.Text(s, 0, p1, Vector3.forward, new Vector3[0], new int[0], FishState.Idle, true, true, at);
        Assert.AreEqual(c, Bricks.Front(p1, Vector3.forward), "P2 就站在 P1 面前那一格");
        Assert.IsTrue(Bricks.UnderFoot(c, new[] { p1, p2 }), "有人站着");
        Assert.IsFalse(Bricks.UnderFoot(c, new[] { p1 }), "P2 走开了（P1 自己的胶囊够不着面前那格）");
        Assert.IsTrue(Bricks.UnderFoot(c, new[] { Side(0.2f) }), "隔壁格的人胶囊压进这格的砖位");
        Assert.IsFalse(Bricks.UnderFoot(c, new[] { Side(0.45f) }), "隔壁格离得够远");
        Assert.AreEqual("", Hint(p1, p2), "面前有人：不出「B 放砖」");
        Assert.AreEqual("B 放砖", Hint(p1));
    }

    // loop-7 砍树只认站在树南侧（人的 z ≤ 树干 z + ChopSouth）：北侧够得着也不算，南侧算，提示跟着
    [Test] public void ChopSouthOnly()
    {
        var tree = new[] { new Vector3(-5.84f, Config.GroundY, -10.16f) };   // 场景里离床最近的那棵
        string Hint(Vector3 p) => Hints.Text(new Stock(), 0, p, Vector3.forward, tree, new int[1], FishState.Idle, true, true);
        Assert.AreEqual(-1, Trees.Nearest(tree, tree[0] + new Vector3(0f, 0f, 1f)), "北侧 1 m：够得着也不算");
        Assert.AreEqual(-1, Trees.Nearest(tree, tree[0] + new Vector3(0.8f, 0f, Config.ChopSouth + 0.05f)), "东边偏北，超出 5 cm");
        Assert.AreEqual(0, Trees.Nearest(tree, tree[0] + new Vector3(0.8f, 0f, Config.ChopSouth - 0.05f)), "东边偏北，还在 ChopSouth 以内");
        Assert.AreEqual(0, Trees.Nearest(tree, tree[0] + new Vector3(0f, 0f, -1f)), "南侧 1 m");
        Assert.AreEqual("E 砍树", Hint(tree[0] + new Vector3(0f, 0f, -1f)));
        Assert.AreEqual("", Hint(tree[0] + new Vector3(0f, 0f, 1f)), "北侧不提示");
    }

    // loop-6 修补：钓鱼状态变化 → 底部说哪句（Hints.Update 每帧拿上一帧比）
    [Test] public void FishFailToast()
    {
        Assert.AreEqual("鱼溜走啦，下回一咬钩就按空格", Hints.Fail(FishState.Bite, FishState.Idle), "咬钩没按");
        Assert.AreEqual("线断啦，张力快满就松手", Hints.Fail(FishState.Reel, FishState.Lost), "张力过满");
        Assert.IsNull(Hints.Fail(FishState.Bite, FishState.Reel), "提了竿");
        Assert.IsNull(Hints.Fail(FishState.Reel, FishState.Caught), "钓上");
        Assert.IsNull(Hints.Fail(FishState.Lost, FishState.Idle), "断线后回空闲");
        Assert.IsNull(Hints.Fail(FishState.Bite, FishState.Bite), "没变");
    }
}
