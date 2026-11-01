using NUnit.Framework;
using UnityEngine;

// loop-20 的纯逻辑测试（PlayTests5 / PlayTests7 满了另开）：放砖预览什么时候画（有砖 / 没砖、站定 / 走动、钓鱼中、抱着、面前不能放）、画在哪（下一层那块的底面：贴地 / 贴下层砖顶），
// 钓上那句说着时字条不压跃出的鱼、不压别人的帽子（Hints.Cheer / Dodge）。不开场景、不调 Input；Bricks.Blockers 是空表（原规则）。
public class PlayTests8
{
    static Vector2Int Sand => Bricks.Cell(PlayTests.Open);   // 床和树之间的空沙地 (-6,-7) 格，手里有砖放得下
    static readonly Vector3 Me = PlayTests.Open + Vector3.forward * 1.3f;   // 站在那格北边、面朝南：面前那格（Front 往前 1.3 m）正是 Sand，胶囊压不到它

    static Vector3? Ghost(Stock s = null, float still = 1f, FishState fish = FishState.Idle, bool hug = false, Vector3[] at = null, bool open = true) =>
        Bricks.Ghost(s ?? new Stock { brick = 2 }, Me, Vector3.back, still, fish, hug, at ?? new[] { Me }, open);

    static float Bottom(int layer) => Config.GroundY + layer * Config.BrickSize + Config.GhostThick * 0.5f;   // 第 layer 层那块砖的底面，再上半片厚

    [Test] public void GhostOnSand()   // 有砖、站定、没钓没抱、面前能放：画在面前那格正中，贴地
    {
        Assert.AreEqual(Sand, Bricks.Front(Me, Vector3.back));
        var g = Ghost();
        Assert.IsTrue(g.HasValue);
        var c = Bricks.Center(Sand, 0);
        Assert.AreEqual(c.x, g.Value.x, 1e-4f); Assert.AreEqual(c.z, g.Value.z, 1e-4f);
        Assert.AreEqual(Bottom(0), g.Value.y, 1e-4f, "贴地");
    }

    [Test] public void GhostNeedsBricksAndStill()
    {
        Assert.IsNull(Ghost(new Stock()), "没砖");
        Assert.IsNull(Ghost(still: 0f), "走动");
        Assert.IsNull(Ghost(still: Config.GhostStill - 0.05f), "刚停下");
        Assert.IsNotNull(Ghost(still: Config.GhostStill), "站定 GhostStill 秒就画");
    }

    [Test] public void GhostHiddenWhileBusy()   // 钓鱼的每一步（抛竿到跑了）、抱着都不画
    {
        foreach (var f in new[] { FishState.Cast, FishState.Wait, FishState.Bite, FishState.Reel, FishState.Caught, FishState.Lost }) Assert.IsNull(Ghost(fish: f), f.ToString());
        Assert.IsNull(Ghost(hug: true), "抱着");
    }

    [Test] public void GhostHiddenWhenCannotPlace()   // 面前那格现在放不下（同按 B 的 BrickWhy.Of）：有人、会堵路、垒满、码头口保留区
    {
        Assert.IsNull(Ghost(at: new[] { Me, Bricks.Center(Sand, 0) }), "面前那格有人");
        Assert.IsNull(Ghost(open: false), "放了会把路堵死");
        var full = new Stock { brick = 2 };
        for (int k = 0; k < Config.MaxHeight; k++) full.bricks.Add(new BrickRecord { x = Sand.x, z = Sand.y, layer = k });
        Assert.IsNull(Ghost(full), "已垒满 MaxHeight 层");
        var p = new Vector3(0.5f, Config.GroundY, -4.2f);   // 实玩 l20 的站位：面朝北是码头口保留区 (0,-3)，面朝南是空沙地 (0,-6)
        Assert.IsNull(Bricks.Ghost(new Stock { brick = 2 }, p, Vector3.forward, 1f, FishState.Idle, false, new[] { p }, true), "面朝码头口");
        Assert.IsNotNull(Bricks.Ghost(new Stock { brick = 2 }, p, Vector3.back, 1f, FishState.Idle, false, new[] { p }, true), "转身朝沙地");
    }

    [Test] public void GhostClimbs()   // 放成一块：预览挪到第 2 层，贴在下层砖顶 = 下一块（Height 层）的底面；砖用完不画
    {
        var s = new Stock { brick = 2 };
        Assert.IsNotNull(Bricks.TryPlace(s, Sand, 0));
        var g = Ghost(s);
        Assert.IsTrue(g.HasValue, "还剩 1 块：照画");
        Assert.AreEqual(Bottom(1), g.Value.y, 1e-4f, "贴在第 1 层砖顶");
        Assert.AreEqual(Bricks.Center(Sand, Bricks.Height(s, Sand)).y - Config.BrickSize * 0.5f, g.Value.y - Config.GhostThick * 0.5f, 1e-4f, "= 下一层那块的底面");
        Bricks.TryPlace(s, Sand, 1);
        Assert.IsNull(Ghost(s), "砖用完：不画");
    }

    [Test] public void CheerWords()   // 让开只管钓上那句（各鱼名 / 灯笼鱼）；OnGUI 那一行正说着才算，说完了回原位
    {
        foreach (var f in Config.FishNames) Assert.IsTrue(Hints.Cheer(Hints.Caught(f)), f);
        Assert.IsTrue(Hints.Cheer(Hints.Lantern), "灯笼鱼");
        foreach (var t in new[] { "", "放下一砖", "天亮了", "空格 提竿！", Hints.Tower, Hints.Fail(FishState.Bite, FishState.Idle) }) Assert.IsFalse(Hints.Cheer(t), t);
        string c = Hints.Caught(Config.FishNames[0]); var none = new Vector3[0]; var no = new int[0];
        Assert.IsTrue(Hints.Cheer(Hints.Line(new Stock(), 0, Config.FishSpot, Vector3.forward, none, no, FishState.Caught, true, true, said: c, saying: true)), "正说着就让");
        Assert.IsFalse(Hints.Cheer(Hints.Line(new Stock(), 0, Config.FishSpot, Vector3.forward, none, no, FishState.Idle, true, true, said: c, saying: false)), "说完了回原位");
    }

    // 照 960×540 一帧量的样子（l20f1p1）：本人头顶那张 HeadR、脚下那张 FeetR（宽 120、高 27），鱼从北边水里跃到头顶上方的手上（FishR 竖长条，压着头顶那张）
    static readonly Rect HeadR = new Rect(420, 150, 120, 27), FeetR = new Rect(420, 300, 120, 27), FishR = new Rect(470, 60, 20, 112);
    static Rect Dodge(Rect? fish, params Rect[] hats) => Hints.Dodge(FeetR, HeadR, fish, hats, Config.HintTop, 960, 540);
    static void Clear(Rect r, Rect? fish, Rect[] hats, string why)
    {
        Assert.IsFalse(fish is Rect f && r.Overlaps(f), why + "：压着鱼");
        foreach (var o in hats) Assert.IsFalse(r.Overlaps(o), why + "：压着帽子 " + o);
    }

    [Test] public void CheerDodge()   // 说钓上那句：字条和鱼、和别人的帽子都不相交（loop-20 修补）
    {
        Assert.IsTrue(HeadR.Overlaps(FishR), "样子对：头顶那张原地就压着鱼");
        Assert.AreEqual(FeetR, Dodge(FishR), "没人挨着：走脚下（鱼总在本人上方）");
        var south = new Rect(455, 290, 50, 44);   // 南边一两米那人的帽子压着脚下那张
        var r = Dodge(FishR, south);
        Clear(r, FishR, new[] { south }, "南边有人");
        Assert.AreEqual(HeadR.y, r.y, "横挪：还在头顶高度");
        Assert.AreEqual(FishR.xMin - Config.HintGap, r.xMax, 1e-3f, "两边一样近：挪到鱼的左边");
        var wide = new Rect(380, 60, 110, 112);   // 浮漂在左（另一人在钓点、本人走左道）：鱼的范围往左宽，挪到右边离本人近
        Assert.AreEqual(wide.xMax + Config.HintGap, Dodge(wide, south).x, 1e-3f, "浮漂在左：挪到鱼的右边");
        Assert.Less(Dodge(new Rect(466, 60, 20, 112), south).x, HeadR.x, "两边差不到 HintGap：一律先左边，不按浮点误差左右跳");
        var low = new Rect(470, 110, 20, 62); Rect[] three = { south, new Rect(300, 140, 60, 44), new Rect(500, 140, 60, 44) };
        r = Dodge(low, three); Clear(r, low, three, "南、左、右都有帽子");
        Assert.AreEqual(low.yMin - Config.HintGap, r.yMax, 1e-3f, "提到鱼顶之上");
        Assert.AreEqual(FeetR, Dodge(null), "鱼收走了：脚下");
        Assert.AreEqual(HeadR, Dodge(null, south), "鱼收走了、南边有人：回头顶");
        for (float x = 0; x < 960; x += 24) for (float y = 0; y < 540; y += 24)   // 别人的帽子摆在画面任何一处：字条都不压它、不压鱼
        {
            var hat = new Rect(x, y, 50, 44);
            Clear(Dodge(FishR, hat), FishR, new[] { hat }, "帽子在 " + hat);
        }
    }
}
