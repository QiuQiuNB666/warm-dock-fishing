using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 双人合作子阶段的纯逻辑测试（不开场景、不调 Input.GetKey）。钓鱼与时段的用例仍在 SystemsTests。
public class CoopTests
{
    // 子阶段 1：只断言键表。两人的键没有交集，T/F3 是全局键、不在任何一人的表里。
    [Test] public void KeyTablesDisjoint()
    {
        KeyCode[] p1 = KeyMap.Players[0].All(), p2 = KeyMap.Players[1].All();
        CollectionAssert.IsEmpty(p1.Intersect(p2).Except(KeyMap.Global));
        foreach (var g in KeyMap.Global) { CollectionAssert.DoesNotContain(p1, g); CollectionAssert.DoesNotContain(p2, g); }
    }

    [Test] public void SinglePlayerP2DoesNotMove()
    {
        int keep = Config.PlayerCount;
        try
        {
            Config.PlayerCount = 1;
            Assert.AreEqual(Vector3.zero, InputManager2P.Move(1, k => true));
            Config.PlayerCount = 2;
            Assert.AreEqual(1f, InputManager2P.Move(1, k => k == KeyCode.UpArrow || k == KeyCode.RightArrow).magnitude, 1e-5f);   // 对角线归一化
        }
        finally { Config.PlayerCount = keep; }
    }

    // 子阶段 2：两人间距 → 拉远倍数，夹在 [1, CamMaxZoomOut]；小屋到钓点框不住时封顶
    [Test] public void CameraZoomClamped()
    {
        float aspect = 16f / 9f;
        Vector3 a = Config.BedPos;
        Assert.AreEqual(1f, TopDownCamera.ZoomFor(a, a + Vector3.right, aspect), 1e-5f);
        Assert.That(TopDownCamera.ZoomFor(a, a + Vector3.forward * 11f, aspect), Is.GreaterThan(1f).And.LessThan(Config.CamMaxZoomOut));
        Assert.AreEqual(Config.CamMaxZoomOut, TopDownCamera.ZoomFor(Config.CabinPos, Config.FishSpot, aspect), 1e-5f);
    }
}
