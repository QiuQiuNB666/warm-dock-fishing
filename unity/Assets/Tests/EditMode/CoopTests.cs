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

    // 子阶段 3：存档往返。内存里的 JSON 走一遍（1 人、2 人各一次），再用真实键走一次 PlayerPrefs 并删掉。
    static Stock Sample()
    {
        var s = new Stock { brick = 12, wood = 3, stone = 999 };
        s.Set(ItemType.RawFish, 5, 0); s.Set(ItemType.CookedFish, 2, 1); s.Set(ItemType.Cloth, 64, 1);
        s.bricks.Add(new BrickRecord { x = -6, z = -6, layer = 0 });
        s.bricks.Add(new BrickRecord { x = -6, z = -6, layer = 1 });
        return s;
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SaveRoundTrip(int players)
    {
        Stock s = Sample();
        float t = 0.6180339f;
        Vector3 bed = Config.BedPos + Vector3.right;
        var d = PierSave.Parse(PierSave.ToJson(PierSave.Capture(t, bed, s, players)));
        Assert.IsNotNull(d);
        Assert.AreEqual(1, d.version);
        Assert.AreEqual(players, d.players.Length);
        Stock r = PierSave.Restore(d, out float t2, out Vector3 bed2);
        Assert.AreEqual(t, t2, 1e-4f);
        Assert.That(Vector3.Distance(bed, bed2), Is.LessThan(1e-4f));
        Assert.AreEqual(new[] { 12, 3, 999 }, new[] { r.brick, r.wood, r.stone });
        foreach (var item in new[] { ItemType.RawFish, ItemType.CookedFish, ItemType.Cloth })
            for (int i = 0; i < players; i++) Assert.AreEqual(s.Count(item, i), r.Count(item, i), $"{item} p{i}");
        Assert.AreEqual(2, r.bricks.Count);
        Assert.AreEqual(1, r.bricks[1].layer);
        Assert.IsFalse(r.dirty);
    }

    [Test] public void CorruptSaveIsDiscarded()
    {
        foreach (var bad in new[] { "", "{not json", "{\"version\":2}", "null" }) Assert.IsNull(PierSave.Parse(bad), bad);
    }

    // 子阶段 4：测试砖。库存有砖才能放；只放沙滩上可走、不在码头的格子；同格再放失败；存、清内存、读，砖还在。
    [Test] public void TestBrickRules()
    {
        var s = new Stock();
        Vector2Int beach = Bricks.Cell(Config.BedPos + new Vector3(0f, 0f, -3f));
        Assert.IsNull(Bricks.TryPlace(s, beach), "没砖不能放");
        s.Add(ItemType.Brick, 2);
        Assert.IsNull(Bricks.TryPlace(s, Bricks.Cell(new Vector3(0.2f, Config.GroundY, -0.3f))), "码头上不准放砖");
        Assert.IsNull(Bricks.TryPlace(s, Bricks.Cell(new Vector3(5f, Config.GroundY, 10f))), "海里不能放");
        Assert.IsNotNull(Bricks.TryPlace(s, beach));
        Assert.IsNull(Bricks.TryPlace(s, beach), "同格再放必须失败");
        Assert.AreEqual(1, s.brick);
        Vector3 c = Bricks.Center(beach, 0);
        Assert.IsTrue(Config.OnBeach(c) && Config.IsWalkable(c));
        string json = PierSave.ToJson(PierSave.Capture(0f, Config.BedPos, s, 2));
        s = null;   // 清内存
        Stock r = PierSave.Restore(PierSave.Parse(json), out _, out _);
        Assert.AreEqual(1, r.bricks.Count);
        Assert.AreEqual(new[] { beach.x, beach.y }, new[] { r.bricks[0].x, r.bricks[0].z });
    }

    [Test] public void PlayerPrefsRoundTripThenDelete()
    {
        string old = PlayerPrefs.HasKey(PierSave.Key) ? PlayerPrefs.GetString(PierSave.Key) : null;
        try
        {
            PierSave.Write(PierSave.Capture(0.25f, Config.BedPos, Sample(), 2));
            var d = PierSave.Read();
            Assert.IsNotNull(d);
            Assert.AreEqual(0.25f, d.timeOfDay, 1e-4f);
            PlayerPrefs.SetString(PierSave.Key, "{broken");
            Assert.IsNull(PierSave.Read());
            Assert.IsFalse(PlayerPrefs.HasKey(PierSave.Key), "坏档应被删掉");
        }
        finally { PutBack(old); }
    }

    static void PutBack(string old)   // 测试前存档原样放回（没有就删掉）
    {
        if (old != null) PlayerPrefs.SetString(PierSave.Key, old); else PlayerPrefs.DeleteKey(PierSave.Key);
        PlayerPrefs.Save();
    }

    // play-1：钓上一条，该玩家生鱼 +1、另一人不变（记账在 FishingStateMachine.Credit，FishingLogic 不碰 Stock）
    [Test] public void CatchCreditsOwnerOnly()
    {
        var f = new FishingLogic(3); var s = new Stock();
        f.Press();
        for (int i = 0; i < 9000 && f.State != FishState.Caught; i++) { if (f.State == FishState.Bite) f.Press(); f.Tick(0.02f, i % 10 < 4); }
        Assert.AreEqual(1, FishingStateMachine.Credit(s, 1, 0, f.CaughtCount));
        Assert.AreEqual(new[] { 0, 1 }, new[] { s.Count(ItemType.RawFish, 0), s.Count(ItemType.RawFish, 1) });
    }

    // play-2：小屋门口有鱼才换：鱼 -1、砖 +1；没鱼、或人在钓点，数量都不变
    [Test] public void TradeAtDoorOnly()
    {
        var s = new Stock();
        Assert.IsFalse(Bricks.Trade(s, 0, Config.DoorPos), "没鱼"); s.Add(ItemType.RawFish, 1, 0);
        Assert.IsFalse(Bricks.Trade(s, 0, Config.FishSpot), "人在钓点");
        Assert.AreEqual(new[] { 1, 0 }, new[] { s.Count(ItemType.RawFish, 0), s.brick });
        Assert.IsTrue(Bricks.Trade(s, 0, Config.DoorPos));
        Assert.AreEqual(new[] { 0, 1 }, new[] { s.Count(ItemType.RawFish, 0), s.brick });
    }
}
