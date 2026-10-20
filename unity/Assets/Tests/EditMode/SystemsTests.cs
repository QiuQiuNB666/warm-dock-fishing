using NUnit.Framework;
using UnityEngine;

// 只测纯 C# 逻辑：状态流转、超时、断线、饵料系数、时段插值端点。
public class SystemsTests
{
    static FishingLogic Run(FishingLogic f, float seconds, bool hold, float dt = 0.02f)
    {
        for (float t = 0f; t < seconds; t += dt) f.Tick(dt, hold);
        return f;
    }

    static FishingLogic ToBite(int seed = 1)
    {
        var f = new FishingLogic(seed);
        f.Press();
        Assert.AreEqual(FishState.Cast, f.State);
        Run(f, Config.CastTime + 0.05f, false);
        Assert.AreEqual(FishState.Wait, f.State);
        Run(f, f.WaitTime + 0.05f, false);
        Assert.AreEqual(FishState.Bite, f.State);
        return f;
    }

    [Test] public void IdleIgnoresTicks() => Assert.AreEqual(FishState.Idle, Run(new FishingLogic(1), 10f, true).State);

    [Test] public void WaitWithinRange()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var f = new FishingLogic(seed);
            f.Press();
            Run(f, Config.CastTime + 0.05f, false);
            Assert.That(f.WaitTime, Is.InRange(Config.WaitMin, Config.WaitMax));
        }
    }

    [Test] public void BiteTimeoutReturnsIdle()
    {
        var f = ToBite();
        Run(f, Config.BiteWindow + 0.05f, false);
        Assert.AreEqual(FishState.Idle, f.State);
        Assert.AreEqual(0, f.CaughtCount);
    }

    [Test] public void HoldingTooLongBreaksLine()
    {
        var f = ToBite();
        f.Press();
        Assert.AreEqual(FishState.Reel, f.State);
        Run(f, 1.5f, true);   // 张力 0.3→1 约 1.17 s，结果停留 1.2 s 内
        Assert.AreEqual(FishState.Lost, f.State);
        Run(f, Config.ResultTime + 0.05f, false);
        Assert.AreEqual(FishState.Idle, f.State);
    }

    [Test] public void PulsingReelCatchesFish()
    {
        var f = ToBite();
        f.Press();
        for (int i = 0; i < 3000 && f.State == FishState.Reel; i++) f.Tick(0.02f, i % 10 < 4);   // 40% 占空比：张力不涨、进度在涨
        Assert.AreEqual(FishState.Caught, f.State);
        Assert.AreEqual(1, f.CaughtCount);
        CollectionAssert.Contains(Config.FishNames, f.LastFish);
    }

    [Test] public void BaitFactorScalesWait()
    {
        var a = new FishingLogic(5); a.Press(); Run(a, Config.CastTime + 0.05f, false);
        var b = new FishingLogic(5) { WaitFactor = 0.5f }; b.Press(); Run(b, Config.CastTime + 0.05f, false);
        Assert.AreEqual(a.WaitTime * 0.5f, b.WaitTime, 1e-4f);
    }

    [Test] public void BaitWeightsPickOnlyAllowedFish()
    {
        var f = new FishingLogic(9) { Weights = new[] { 0f, 1f, 0f } };
        for (int i = 0; i < 50; i++) Assert.AreEqual(Config.FishNames[1], f.PickFish());
    }

    [Test] public void BaitTableMatchesFish()
    {
        foreach (var bait in Config.Baits)
        {
            Assert.AreEqual(Config.FishNames.Length, bait.Weights.Length, bait.Name);
            Assert.That(bait.WaitFactor, Is.GreaterThan(0f));
        }
    }

    [Test] public void TimeKeysAreEndpoints()
    {
        int n = Config.Times.Length;
        for (int i = 0; i < n; i++)
        {
            var s = TimeOfDay.Evaluate(i / (float)n);
            Assert.AreEqual(Config.Times[i].Intensity, s.Intensity, 1e-4f);
            Assert.AreEqual(Config.Times[i].Light, s.Light);
        }
        Assert.AreEqual(Config.Times[0].Intensity, TimeOfDay.Evaluate(1f).Intensity, 1e-4f);   // 首尾循环
    }

    [Test] public void TimeMidpointInterpolates()
    {
        var s = TimeOfDay.Evaluate(0.125f);
        Assert.AreEqual((Config.Times[0].Intensity + Config.Times[1].Intensity) * 0.5f, s.Intensity, 1e-4f);
    }

    [Test] public void NextPhaseSteps()
    {
        Assert.AreEqual(0.25f, TimeOfDay.NextPhase(0f), 1e-4f);
        Assert.AreEqual(0.5f, TimeOfDay.NextPhase(0.3f), 1e-4f);
        Assert.AreEqual(0f, TimeOfDay.NextPhase(0.8f), 1e-4f);
    }

    [Test] public void WalkableOnlyDockAndBeach()
    {
        Assert.IsTrue(Config.IsWalkable(Config.FishSpot));
        Assert.IsTrue(Config.IsWalkable(Config.PlayerStart));
        Assert.IsFalse(Config.IsWalkable(new Vector3(5f, Config.GroundY, 10f)));   // 码头旁的海里
        Assert.AreEqual(Vector3.zero, PlayerController.Constrain(new Vector3(0f, 1f, 10f), new Vector3(5f, 0f, 0f)));
    }
}
