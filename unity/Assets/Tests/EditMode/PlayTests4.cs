using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// loop-9 篝火烤鱼 + 熟鱼换两砖的纯逻辑测试（接着 PlayTests3 放）。不开场景、不调 Input；往 Bricks.Blockers 塞的矩形进出都清空。
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

    // E 的顺序：门口换砖 > 烤鱼 > 砍树。门口和火边的 E 范围不相交（火离每棵树也比 InteractRange + ChopRange 远，见 BuildSceneTests），实际不会同时成立；
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
}
