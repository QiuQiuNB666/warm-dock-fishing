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
}
