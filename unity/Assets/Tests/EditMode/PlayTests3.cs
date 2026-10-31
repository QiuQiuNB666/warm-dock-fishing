using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 玩法纯逻辑测试，接着 PlayTests2（快满了）往下放。不开场景、不调 Input；Bricks.Blockers 是空表（原规则）。
public class PlayTests3
{
    // loop-7 修补：人贴着自己格子前沿（离前沿 0.2 m）面朝前。面前那格按胶囊前沿取（Bricks.Front），自己的胶囊压不到：照样出「B 放砖」、按 B 放得下
    [Test] public void FrontEdgeStillPlaces()
    {
        var s = new Stock { brick = 1 }; var p = new Vector3(-5.5f, Config.GroundY, -6.2f); var at = new[] { p };   // 床和树之间的空沙地，(-6,-7) 格，离前沿 z=-6 只有 0.2 m
        var c = Bricks.Front(p, Vector3.forward);
        Assert.IsTrue(Bricks.UnderFoot(Bricks.Cell(p + Vector3.forward * Config.CellSize), at), "原来往前 1 m 取：判成自己压着");
        Assert.IsFalse(Bricks.UnderFoot(c, at), "按胶囊前沿取：自己压不到");
        Assert.AreEqual("B 放砖", Hints.Text(s, 0, p, Vector3.forward, new Vector3[0], new int[0], FishState.Idle, true, true, at, BrickPath.Open(s, c, at)));
        Assert.IsNotNull(BrickPath.Open(s, c, at) ? Bricks.TryPlace(s, c) : null, "按 B 放得下");
    }

    // loop-7 修补：沿码头入口保留区外沿围一圈（西 x=-4、东 x=3 各 3 格，南 z=-4 一排 6 格；北边 z=0 以外不是沙滩）：
    // 人在圈外，前 11 块都能放，合口的第 12 块放不了。空格子围上没事；人站在里面（自己围自己）第四面合口放不了
    [Test] public void NoSealingTheDock()
    {
        var s = new Stock { brick = 20 }; var at = new[] { PlayTests.Open }; var ring = new List<Vector2Int>();
        for (int z = -3; z <= -1; z++) { ring.Add(new Vector2Int(-4, z)); ring.Add(new Vector2Int(3, z)); }
        for (int x = -3; x <= 2; x++) ring.Add(new Vector2Int(x, -4));
        for (int i = 0; i < 11; i++) Assert.IsNotNull(BrickPath.Open(s, ring[i], at) ? Bricks.TryPlace(s, ring[i]) : null, $"第 {i + 1} 块 {ring[i]}");
        Assert.IsTrue(Bricks.CanPlace(s, ring[11], 0), "合口的第 12 块：别的规则都许");
        Assert.IsFalse(BrickPath.Open(s, ring[11], at), "合口的第 12 块：码头入口整个被围住");
        var me = new Vector3(-10.5f, Config.GroundY, -12.5f); var c = Bricks.Cell(me);
        foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down }) Assert.IsNotNull(Bricks.TryPlace(s, c + d));
        Assert.IsTrue(BrickPath.Open(s, c + Vector2Int.up, at), "没人在里面：空格子围上没事");
        Assert.IsFalse(BrickPath.Open(s, c + Vector2Int.up, new[] { me }), "自己围自己：第四面合口放不了");
    }

    // loop-8：两人浮漂按玩家左右错开（P1 -x、P2 +x），相距 2×CastSpread，都落在码头尽头外的水面上
    [Test] public void CastTargetsSplitByPlayer()
    {
        Vector3 a = Tackle.CastTarget(0), b = Tackle.CastTarget(1);
        Assert.AreEqual(2f * Config.CastSpread, Vector3.Distance(a, b), 1e-4f, "相距 2×CastSpread");
        Assert.Less(a.x, b.x, "P1 在左（-x）、P2 在右（+x）");
        foreach (var p in new[] { a, b })
        {
            Assert.AreEqual(Config.SeaY, p.y, 1e-4f, "落在水面");
            Assert.IsFalse(Config.IsWalkable(p), "不在码头、沙滩上");
            Assert.Greater(p.z, Config.DockLength, "在码头尽头外的水里");
        }
    }

    // loop-8：实玩计划的 teleport 步 = [玩家号, x, z, 朝向°（可省）]；没写的步不摆人
    [Test] public void ParseTeleportStep()
    {
        var s = ScriptedPlay.Parse("[{\"teleport\":[1,0.6,22.8,90]},{\"wait\":100}]");
        Assert.AreEqual(4, s[0].teleport.Length);
        Assert.AreEqual(1f, s[0].teleport[0]); Assert.AreEqual(0.6f, s[0].teleport[1], 1e-4f); Assert.AreEqual(22.8f, s[0].teleport[2], 1e-4f); Assert.AreEqual(90f, s[0].teleport[3]);
        Assert.IsTrue(s[1].teleport == null || s[1].teleport.Length == 0, "没写 teleport 的步不摆人");
        Assert.AreEqual(100, s[1].wait);
    }

    // loop-11 小灯塔：同一格垒满 MaxHeight 层，顶块（layer = MaxHeight−1）发光；只垒 3 层的顶块不发光；存档读回来（Rebuild 按记录判断）照样只有顶块发光
    [Test] public void LighthouseTop()
    {
        var s = new Stock { brick = Config.MaxHeight }; var c = Bricks.Cell(PlayTests.Open);
        for (int k = 0; k < Config.MaxHeight - 1; k++) Bricks.TryPlace(s, c, k);
        Assert.IsFalse(Bricks.Lighthouse(s.bricks[^1]), "三层：顶块不发光");
        Assert.IsTrue(Bricks.Lighthouse(Bricks.TryPlace(s, c, Config.MaxHeight - 1)), "四层：顶块发光");
        var back = PierSave.Restore(PierSave.Parse(PierSave.ToJson(PierSave.Capture(0f, Config.BedPos, s, 2))), out _, out _);
        Assert.AreEqual(Config.MaxHeight, back.bricks.Count);
        Assert.AreEqual(1, back.bricks.FindAll(Bricks.Lighthouse).Count, "读档重建：只有顶块发光");
    }

    // loop-11 按 B 放不下要说为什么（BrickWhy.Of → Say），每种原因一条。Sand = 床和树之间的空沙地，手里 1 块砖放得下（Ok 说空串）
    static Vector2Int Sand => Bricks.Cell(PlayTests.Open);
    static string Why(Stock s, Vector2Int c, Vector3[] at = null, bool open = true) => BrickWhy.Say(BrickWhy.Of(s, c, Bricks.Height(s, c), at, open));

    [Test] public void WhyPlaceable() => Assert.AreEqual("", Why(new Stock { brick = 1 }, Sand));
    [Test] public void WhyNoBrick() => Assert.AreEqual("没砖了，拿鱼去门口换", Why(new Stock(), Sand));
    [Test] public void WhyDockEntry() => Assert.AreEqual("码头口要留路", Why(new Stock { brick = 1 }, Bricks.Cell(new Vector3(Config.DockEntry.center.x, Config.GroundY, Config.DockEntry.center.y))));
    [Test] public void WhyDoor() => Assert.AreEqual("门口要留路", Why(new Stock { brick = 1 }, Bricks.Cell(Config.DoorPos)));
    [Test] public void WhyPerson() => Assert.AreEqual("前面有人", Why(new Stock { brick = 1 }, Sand, new[] { Bricks.Center(Sand, 0) }));

    [Test] public void WhyFull()
    {
        var s = new Stock { brick = Config.MaxHeight + 1 };
        for (int k = 0; k < Config.MaxHeight; k++) Bricks.TryPlace(s, Sand, k);
        Assert.AreEqual("最高四层", Why(s, Sand));
    }

    [Test] public void WhyPath()   // 同 NoSealingTheDock：人在里面、三面已围上，第四面合口
    {
        var s = new Stock { brick = 4 }; var me = new[] { new Vector3(-10.5f, Config.GroundY, -12.5f) }; var c = Bricks.Cell(me[0]);
        foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down }) Bricks.TryPlace(s, c + d);
        Assert.AreEqual("这样会把路堵死", Why(s, c + Vector2Int.up, me, BrickPath.Open(s, c + Vector2Int.up, me)));
    }

    [Test] public void WhyBlocked()   // 海里、码头上（被床/屋/树/火占着同一句，见 PlayTests2/4 的 Blockers 用例）；CanPlace 同样不许
    {
        var s = new Stock { brick = 1 };
        foreach (var c in new[] { Bricks.Cell(new Vector3(5f, Config.GroundY, 10f)), Bricks.Cell(new Vector3(0.2f, Config.GroundY, 5f)) })
        {
            Assert.AreEqual("这里放不下", Why(s, c), $"{c}");
            Assert.IsFalse(Bricks.CanPlace(s, c, 0), $"{c}");
        }
    }

    // loop-12 自己的胶囊压到砖位（站在斜对角格的前角、面朝那格）说「退后半步再放」；压到的是别人、或自己和别人都压到，照旧「前面有人」；不传 me 照旧
    [Test] public void WhySelf()
    {
        var s = new Stock { brick = 1 }; var me = new Vector3(Sand.x * Config.CellSize - 0.1f, Config.GroundY, Sand.y * Config.CellSize - 0.1f);   // 西南斜对角那格的东北角
        Assert.AreEqual(Sand, Bricks.Front(me, new Vector3(1f, 0f, 1f).normalized), "面朝东北，面前就是 Sand");
        var far = Bricks.Center(Sand, 0) + new Vector3(3f, 0f, 0f);   // 压不到 Sand 的另一个人
        string Who(Vector3? m, params Vector3[] at) => BrickWhy.Say(BrickWhy.Of(s, Sand, 0, at, true, m));
        Assert.AreEqual("退后半步再放", Who(me, me, far), "只有自己压到");
        Assert.AreEqual("前面有人", Who(far, me, far), "压到的是别人");
        Assert.AreEqual("前面有人", Who(me, me, Bricks.Center(Sand, 0)), "自己和别人都压到");
        Assert.AreEqual("前面有人", Who(null, me), "不传 me：照旧");
    }

    // loop-12 正在钓鱼（不是 Idle）按 B 放不下不说：说了会把头顶「空格 提竿！」整句换掉 1.5 s（正好是咬钩窗口）
    [Test] public void WhyQuietWhileFishing()
    {
        foreach (FishState f in System.Enum.GetValues(typeof(FishState)))
            Assert.AreEqual(f == FishState.Idle ? "这里放不下" : "", BrickWhy.Say(BrickWhy.Code.Blocked, f), $"{f}");
    }

    // loop-11 「拿生鱼去篝火烤」只看烤过没有（Trees.CookedOnce）：换过砖、有生鱼有木、不在火边照样出；烤过一次（也换过砖）兜底都关
    [Test] public void CookHintAfterTrade()
    {
        var s = new Stock { wood = 1 }; s.Set(ItemType.RawFish, 1, 0);
        string Hint(bool cooked) => Hints.Text(s, 0, PlayTests.Open, Vector3.forward, new Vector3[0], new int[0], FishState.Idle, true, true, null, true, cooked);
        Assert.AreEqual("拿生鱼去篝火烤", Hint(false), "换过砖、没烤过");
        Assert.AreEqual("", Hint(true), "烤过、也换过砖");
    }

    // loop-11 两张字条叠在一起时左右分开，中间留 HintGap（12 px）
    [Test] public void HintLabelsGap()
    {
        Rect a = new Rect(150f, 50f, 140f, 26f), b = new Rect(100f, 50f, 120f, 26f);
        Hints.Apart(ref a, ref b);
        Assert.AreEqual(Config.HintGap, a.x - b.xMax, 1e-3f);
    }
}
