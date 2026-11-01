using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// loop-9 篝火烤鱼 + 熟鱼换两砖的纯逻辑测试（接着 PlayTests3 放）；loop-10 烤鱼/兜底提示、失败句、定道、字条回差。不开场景、不调 Input；往 Bricks.Blockers 塞的矩形进出都清空。
public class PlayTests4
{
    // P1 生鱼 raw、熟鱼 cooked，两人共用木 wood；P2 手里 3 条生鱼、1 条熟鱼（看它动没动）
    static Stock Fish(int raw, int cooked, int wood)
    {
        var s = new Stock { wood = wood };
        s.Set(ItemType.RawFish, raw, 0); s.Set(ItemType.CookedFish, cooked, 0); s.Set(ItemType.RawFish, 3, 1); s.Set(ItemType.CookedFish, 1, 1);
        return s;
    }

    // P1 生、熟，木，砖，P2 生、熟
    static int[] Count(Stock s) => new[] { s.Count(ItemType.RawFish, 0), s.Count(ItemType.CookedFish, 0), s.wood, s.brick, s.Count(ItemType.RawFish, 1), s.Count(ItemType.CookedFish, 1) };

    static float Dist(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    // 烤鱼四种情况，只动这名玩家的鱼：够料（木 −1、生鱼 −1、熟鱼 +1）；没木（NoWood，说没柴，什么都不动）；没鱼、离太远（-1，不说话、不动）
    [Test] public void CookFourCases()
    {
        var near = Config.FirePos + new Vector3(0f, 0f, 0.05f - Config.InteractRange); var far = near + new Vector3(0f, 0f, -0.1f);
        var s = Fish(1, 0, 1);
        Assert.AreEqual(Trees.Cooked, Trees.Cook(s, 0, near), "够料");
        CollectionAssert.AreEqual(new[] { 0, 1, 0, 0, 3, 1 }, Count(s), "木 −1、他的生鱼 −1、熟鱼 +1，P2 不动");
        s = Fish(1, 0, 0);
        Assert.AreEqual(Trees.NoWood, Trees.Cook(s, 0, near), "有鱼没木");
        CollectionAssert.AreEqual(new[] { 1, 0, 0, 0, 3, 1 }, Count(s), "没木：什么都不动");
        s = Fish(0, 0, 1);
        Assert.AreEqual(-1, Trees.Cook(s, 0, near), "没鱼（P2 的鱼不算）");
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 3, 1 }, Count(s), "没鱼：什么都不动");
        s = Fish(1, 0, 1);
        Assert.AreEqual(-1, Trees.Cook(s, 0, far), "差 5 cm 够不着");
        CollectionAssert.AreEqual(new[] { 1, 0, 1, 0, 3, 1 }, Count(s), "离太远：什么都不动");
    }

    // 门口先用熟鱼：一条熟鱼换两砖；没熟鱼才一条生鱼换一砖；都只扣这名玩家的
    [Test] public void DoorTradesCookedFirst()
    {
        var s = Fish(1, 1, 0);
        Assert.IsTrue(Bricks.Trade(s, 0, Config.DoorPos));
        CollectionAssert.AreEqual(new[] { 1, 0, 0, Config.CookedBricks, 3, 1 }, Count(s), "熟鱼换两砖，生鱼留着");
        Assert.IsTrue(Bricks.Trade(s, 0, Config.DoorPos));
        CollectionAssert.AreEqual(new[] { 0, 0, 0, Config.CookedBricks + 1, 3, 1 }, Count(s), "没熟鱼：生鱼换一砖");
        Assert.IsFalse(Bricks.Trade(s, 0, Config.DoorPos), "两样都没了：不换（P2 的鱼不算）");
        Assert.AreEqual(2, Config.CookedBricks, "规格：一条熟鱼换两砖");
    }

    // E 的顺序：门口换砖 > 烤鱼 > 砍树。门口和火边的 E 范围不相交（火离每棵树也比 InteractRange + ChopRange 远，见 SceneTests2），实际不会同时成立；
    // 这里把一棵假树摆在人脚下看谁先：门口有鱼先换砖（不烧柴）、火边有生鱼有木先烤、鱼烤完了才轮到砍树
    [Test] public void InteractOrder()
    {
        Assert.Greater(Dist(Config.FirePos, Config.DoorPos), 2f * Config.InteractRange, "门口和火边的 E 范围不相交");
        var s = Fish(1, 0, 1);
        Assert.AreEqual(Trees.Traded, Trees.Interact(s, 0, Config.DoorPos, new[] { Config.DoorPos }), "门口：先换砖");
        Assert.AreEqual(1, s.wood, "换砖不烧柴");
        s = Fish(1, 0, 1); var fire = new[] { Config.FirePos };
        Assert.AreEqual(Trees.Cooked, Trees.Interact(s, 0, Config.FirePos, fire), "火边：先烤鱼");
        Assert.AreEqual(0, Trees.Interact(s, 0, Config.FirePos, fire), "鱼烤完了：才砍树");
        Assert.AreEqual(-1, Trees.Interact(s, 0, Config.FirePos, new Vector3[0]), "火边没生鱼、没树：什么都不做");
    }

    // FirePos：在沙滩上（IsWalkable）；离门口、床心 ≥3 m，离码头入口保留区 ≥2 m；火的占地挡砖，放好后两张床边照样走得到码头入口（BrickPath）
    [Test] public void FireSpotKeepsPathOpen()
    {
        Vector3 f = Config.FirePos; var e = Config.DockEntry;
        Assert.IsTrue(Config.OnBeach(f) && Config.IsWalkable(f) && !Config.OnDock(f), "在沙滩上");
        Assert.GreaterOrEqual(Dist(f, Config.DoorPos), 3f, "离门口");
        Assert.GreaterOrEqual(Dist(f, Config.BedPos + Config.BedFromSpawn), 3f, "离床心");
        Assert.GreaterOrEqual(new Vector2(Mathf.Max(e.xMin - f.x, 0f, f.x - e.xMax), Mathf.Max(e.yMin - f.z, 0f, f.z - e.yMax)).magnitude, 2f, "离码头入口保留区");
        try
        {
            Bricks.Blockers.Clear();
            Bricks.Block(new Bounds(f + Vector3.up * Config.FireSize.y * 0.5f, Config.FireSize));
            Bricks.Block(new Bounds(Config.BedPos + Config.BedFromSpawn, Config.BedSize));
            Assert.IsFalse(Bricks.CanPlace(new Stock { brick = 1 }, Bricks.Cell(f), 0), "火上不放砖");
            foreach (var q in new[] { Config.BedPos, Config.BedPos + Config.P2BedOffset })
                Assert.IsTrue(BrickPath.Reach(Bricks.Cell(q), new HashSet<Vector2Int>(), null), $"床边 {q} 走得到码头入口");
        }
        finally { Bricks.Blockers.Clear(); }
    }

    // 存档里每人的 cookedFish（字段本来就有）读写往返
    [Test] public void CookedFishSurvivesSave()
    {
        var back = PierSave.Restore(PierSave.Parse(PierSave.ToJson(PierSave.Capture(0.3f, Config.BedPos, Fish(0, 2, 0), 2))), out _, out _);
        Assert.AreEqual(2, back.Count(ItemType.CookedFish, 0)); Assert.AreEqual(1, back.Count(ItemType.CookedFish, 1));
    }

    // loop-10 提示（换砖记为没换过；fished 默认 true = 「走去码头」那句关掉）：火边能烤只列「E 烤鱼」（P2「Num1 烤鱼」，兜底让位）；没木不列烤鱼；只有熟鱼时门口列换砖；
    // 兜底按手里的鱼和木指路；两个动作占满时兜底被挤掉；钓点有生鱼时抛竿后面补的那句按有没有木
    static string Hint(Stock s, int player, Vector3 p, bool fished = true, params Vector3[] trees) =>
        Hints.Text(s, player, p, Vector3.forward, trees, new int[trees.Length], FishState.Idle, fished, false);

    [Test] public void CookHints()
    {
        Assert.AreEqual("E 烤鱼", Hint(Fish(1, 0, 1), 0, Config.FirePos), "P1 火边有生鱼有木：只列烤鱼");
        Assert.AreEqual("Num1 烤鱼", Hint(Fish(0, 0, 1), 1, Config.FirePos), "P2 键名");
        Assert.AreEqual("拿生鱼回小屋门口换砖", Hint(Fish(1, 0, 0), 0, Config.FirePos), "没木不列烤鱼");
        Assert.AreEqual("E 换砖", Hint(Fish(0, 1, 0), 0, Config.DoorPos), "只有熟鱼：门口列换砖");
        Assert.AreEqual("拿熟鱼回小屋门口换砖", Hint(Fish(0, 1, 1), 0, PlayTests.Open), "只有熟鱼：回门口换");
        Assert.AreEqual("拿生鱼去篝火烤", Hint(Fish(1, 0, 1), 0, PlayTests.Open), "有生鱼有木、不在火边：去烤");
        Assert.AreEqual("E 砍树　R 睡觉", Hint(new Stock(), 0, Config.BedPos, false, Config.BedPos), "两个动作占满：兜底挤掉");
        Assert.AreEqual("空格 抛竿　拿生鱼去篝火烤", Hint(Fish(1, 0, 1), 0, Config.FishSpot), "钓点有生鱼有木");
        Assert.AreEqual("空格 抛竿　拿生鱼回小屋门口换砖", Hint(Fish(1, 0, 0), 0, Config.FishSpot), "钓点有生鱼没木");
    }

    [Test] public void FishFailWithKeys()
    {
        Assert.AreEqual("鱼溜走啦，下回一咬钩就按空格", Hints.Fail(FishState.Bite, FishState.Idle, 0));
        Assert.AreEqual("鱼溜走啦，下回一咬钩就按 Num5", Hints.Fail(FishState.Bite, FishState.Idle, 1));
        for (int p = 0; p < 2; p++) Assert.AreEqual("线断啦，张力快满就松手", Hints.Fail(FishState.Reel, FishState.Lost, p));
    }

    // loop-10 定道：两人站反（P1 在右、P2 在左）同时抛竿，谁先定都一样：P1 右道、P2 左道，浮漂左右顺序同站位 → 两根线不交叉；
    // 单人正前方（夹在 ±CastSpread）；他的浮漂已在自己这侧的水里 → 往外让，离它 CastSpread；收线时浮漂不越过中线
    [Test] public void LanesFollowStanding()
    {
        float s = Config.CastSpread, p1 = 0.6f, p2 = -0.6f;
        float a = Tackle.Lane(0, p1, p2, null).x, b = Tackle.Lane(1, p2, p1, a).x;   // P1 先定
        float b2 = Tackle.Lane(1, p2, p1, null).x, a2 = Tackle.Lane(0, p1, p2, b2).x;   // P2 先定
        Assert.AreEqual(s, a, 1e-4f); Assert.AreEqual(-s, b, 1e-4f); Assert.AreEqual(a, a2, 1e-4f); Assert.AreEqual(b, b2, 1e-4f);
        Assert.Greater((a - b) * (p1 - p2), 0f, "浮漂左右顺序同站位：不交叉");
        Assert.AreEqual(0.5f, Tackle.Lane(0, 0.5f, null, null).x, 1e-4f, "单人：正前方");
        Assert.AreEqual(-s, Tackle.Lane(1, -2f, null, null).x, 1e-4f, "单人：夹在 ±CastSpread");
        Assert.AreEqual(-0.5f - s, Tackle.Lane(0, -0.9f, -0.3f, -0.5f).x, 1e-4f, "他正前方抛在左边、我站他左边：往外让");
        Assert.AreEqual(0f, Tackle.Keep(new Vector3(0.5f, 0f, 0f), -s).x, 1e-4f, "左道不越过中线");
        Assert.AreEqual(-0.5f, Tackle.Keep(new Vector3(-0.5f, 0f, 0f), 0f).x, 1e-4f, "单人站正中：不夹");
    }

    // loop-10 字条回差：头顶位置高过 HintTop 到脚下；回头顶要比 HintTop 再低 HintBack
    [Test] public void HintFeetHysteresis()
    {
        Assert.IsTrue(Hints.Feet(false, Config.HintTop - 1f)); Assert.IsFalse(Hints.Feet(false, Config.HintTop));
        Assert.IsTrue(Hints.Feet(true, Config.HintTop + Config.HintBack - 1f)); Assert.IsFalse(Hints.Feet(true, Config.HintTop + Config.HintBack));
    }
}
