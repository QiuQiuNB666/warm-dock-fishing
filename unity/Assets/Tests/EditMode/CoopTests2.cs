using NUnit.Framework;
using UnityEngine;

// 双人合作纯逻辑测试第二份（loop-18 从 CoopTests 整条搬来，它正好 150 行）：play-1~3 钓上记账、门口换砖、同格垒砖。断言一字不改。
public class CoopTests2
{
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

    // play-3：沙滩上同格往上垒，最多 MaxHeight 层；没有第一层时第二层失败，同格同层、第 5 层、码头上都失败
    [Test] public void StackBricks()
    {
        var s = new Stock { brick = 9 }; Vector2Int c = Bricks.Cell(Config.BedPos + new Vector3(0f, 0f, -3f));
        Assert.IsNull(Bricks.TryPlace(s, c, 1), "没有第一层时第二层失败");
        for (int k = 0; k < Config.MaxHeight; k++) Assert.IsNotNull(Bricks.TryPlace(s, c, k), $"第 {k + 1} 层");
        Assert.IsNull(Bricks.TryPlace(s, c, Config.MaxHeight), "第 5 层失败"); Assert.IsNull(Bricks.TryPlace(s, c, 1), "同格同层再放失败");
        Assert.IsNull(Bricks.TryPlace(s, Bricks.Cell(new Vector3(0.2f, Config.GroundY, 5f)), 0), "码头上失败");
        Assert.AreEqual(new[] { 4, 5 }, new[] { Bricks.Height(s, c), s.brick });
    }
}
