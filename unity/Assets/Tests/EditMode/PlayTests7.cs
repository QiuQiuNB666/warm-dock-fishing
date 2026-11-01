using NUnit.Framework;
using UnityEngine;

// loop-17 的纯逻辑测试（PlayTests 满了另开）：Say 不盖咬钩提示、天亮跳过正在钓鱼的人、南北站位的 ♥ 以北边那人为基准、灯笼鱼的天黑窗口 / 几率 / 灯塔计数 / 记账。不开场景、不调 Input。
public class PlayTests7
{
    // 每格垒几层：第 i 格在沙滩 (2i, -7)，只造记录，不判能不能放
    static Stock Built(params int[] layers)
    {
        var s = new Stock();
        for (int i = 0; i < layers.Length; i++)
            for (int l = 0; l < layers[i]; l++) s.bricks.Add(new BrickRecord { x = 2 * i, z = -7, layer = l });
        return s;
    }

    // 0a 刚说过一句就整句换掉提示，但这人咬钩时照常画「提竿」（P1 空格、P2 Num5）；收线照换（张力条另画）；说完了画回原来的
    [Test] public void SayNeverHidesBite()
    {
        for (int i = 0; i < 2; i++)
        {
            string bite = Hints.Text(new Stock(), i, Vector3.zero, Vector3.forward, new Vector3[0], new int[0], FishState.Bite, true, true);
            Assert.AreEqual(new[] { "空格 提竿！", "Num5 提竿！" }[i], bite);
            Assert.AreEqual(bite, Hints.Shown(bite, "天亮了", true, FishState.Bite), $"P{i + 1} 咬钩：天亮了不许盖住");
        }
        Assert.AreEqual("天亮了", Hints.Shown("", "天亮了", true, FishState.Idle));
        Assert.AreEqual("天亮了", Hints.Shown("按住空格收线，张力快满就松手", "天亮了", true, FishState.Reel), "收线照换");
        Assert.AreEqual("WASD 走去码头尽头钓鱼", Hints.Shown("WASD 走去码头尽头钓鱼", "天亮了", false, FishState.Idle), "说完了");
    }

    // 0b 时间自己转过早晨：没在钓鱼的人说「天亮了」，正在钓鱼的人跳过（字条上原来那句不动）；没跨早晨不说；不传 busy 两人都说
    [Test] public void DawnSkipsFisher()
    {
        int[] keep = Trees.Hits;
        try
        {
            Trees.Hits = new int[2];
            Hints.Say(0, "x"); Hints.Say(1, "y");
            Assert.IsTrue(TimeOfDay.Dawn(0.995f, 0.002f, new[] { false, true }));
            Assert.AreEqual(new[] { "天亮了", "y" }, new[] { Hints.Said(0), Hints.Said(1) }, "P2 在钓鱼：跳过");
            Hints.Say(0, "x");
            Assert.IsFalse(TimeOfDay.Dawn(0.3f, 0.31f, new[] { false, false }));
            Assert.AreEqual("x", Hints.Said(0), "白天往前走不说");
            Assert.IsTrue(TimeOfDay.Dawn(0.995f, 0.002f));
            Assert.AreEqual(new[] { "天亮了", "天亮了" }, new[] { Hints.Said(0), Hints.Said(1) }, "不传 busy：两人都说");
        }
        finally { Trees.Hits = keep; }
    }

    // 0c 抱着时的「♥」：南北站位（含斜站，如 P1 (2,-4)、P2 (2.4,-3.1)）x = 北边那人.x + HugHeartSide，谁先谁后一样；左右并排画在中点上方
    [Test] public void HeartBesideNorth()
    {
        Vector3 p1 = new Vector3(2f, 0f, -4f), p2 = new Vector3(2.4f, 0f, -3.1f);
        foreach (var h in new[] { Hints.Heart(p1, p2), Hints.Heart(p2, p1) }) Assert.AreEqual(2.4f + Config.HugHeartSide, h.x, 1e-4f, "斜站");
        Assert.AreEqual(1.6f + Config.HugHeartSide, Hints.Heart(new Vector3(2f, 0f, -4f), new Vector3(1.6f, 0f, -3.1f)).x, 1e-4f, "北边那人偏西");
        Assert.Less(Vector3.Distance(new Vector3(0.5f, Config.HugHeart, 0f), Hints.Heart(Vector3.zero, Vector3.right)), 1e-4f, "左右并排：中点");
        Assert.Greater(Config.HugHeartSide, 0.44f, "落在北边那人的帽檐（rig.py BRIM 0.44 m）外");
    }

    // 1 天黑 = 灯笼亮过一半：黄昏那一档起（t = 0.5）到夜里往早晨走到一半（t = 0.875），两头都算；白天、天快亮都不算
    [Test] public void DarkWindow()
    {
        foreach (var t in new[] { 0.5f, 0.6f, 0.75f, 0.875f }) Assert.IsTrue(TimeOfDay.Dark(t), $"t={t}");
        foreach (var t in new[] { 0f, 0.25f, 0.49f, 0.88f, 0.99f }) Assert.IsFalse(TimeOfDay.Dark(t), $"t={t}");
    }

    // 1 几率：0 座 → 0；天黑 n 座 → min(n × 0.15, 0.6)；白天 → 0
    [Test] public void LanternChance()
    {
        var want = new[] { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.6f, 0.6f };
        for (int n = 0; n < want.Length; n++) Assert.AreEqual(want[n], FishingStateMachine.Chance(n, true), 1e-5f, $"天黑 {n} 座");
        Assert.AreEqual(0f, FishingStateMachine.Chance(4, false), "白天 4 座");
    }

    // 1 灯塔计数：3 层不算，4 层（顶块 layer = MaxHeight−1）算一座，两格各 4 层算两座
    [Test] public void TowerCount()
    {
        Assert.AreEqual(4, Config.MaxHeight);
        Assert.AreEqual(new[] { 0, 1, 2, 1 }, new[] { Bricks.Towers(Built(3)), Bricks.Towers(Built(4)), Bricks.Towers(Built(4, 4)), Bricks.Towers(Built(4, 3, 1)) });
    }

    // 2 记账：注入必中（几率 1）→ 熟鱼 +1、生鱼不变；必不中（几率 0）→ 生鱼 +1；只记这名玩家，返回已记条数照旧；几率 0.6 掷 1000 次约六成中（没掷反）
    [Test] public void LanternCredit()
    {
        var rng = new System.Random(17); var s = new Stock();
        Assert.IsTrue(FishingStateMachine.Roll(1f, rng)); Assert.IsFalse(FishingStateMachine.Roll(0f, rng));
        Assert.AreEqual(1, FishingStateMachine.Credit(s, 1, 0, 1, FishingStateMachine.Roll(1f, rng)));
        Assert.AreEqual(new[] { 1, 0 }, new[] { s.Count(ItemType.CookedFish, 1), s.Count(ItemType.RawFish, 1) }, "必中：熟鱼 +1");
        Assert.AreEqual(2, FishingStateMachine.Credit(s, 1, 1, 2, FishingStateMachine.Roll(0f, rng)));
        Assert.AreEqual(new[] { 1, 1 }, new[] { s.Count(ItemType.CookedFish, 1), s.Count(ItemType.RawFish, 1) }, "必不中：生鱼 +1");
        Assert.AreEqual(new[] { 0, 0 }, new[] { s.Count(ItemType.CookedFish, 0), s.Count(ItemType.RawFish, 0) }, "另一人不动");
        int hit = 0; for (int k = 0; k < 1000; k++) if (FishingStateMachine.Roll(0.6f, rng)) hit++;
        Assert.That(hit, Is.InRange(540, 660));
    }

    // 2 灯笼鱼那句带中文名
    [Test] public void LanternSay() => Assert.AreEqual("灯塔引来一条灯笼鱼！", Hints.Lantern);
}
