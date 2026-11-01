using NUnit.Framework;
using UnityEngine;

// loop-16 的纯逻辑测试（PlayTests5 快满了另开）：中文鱼名与钓上说的那句、黄昏前不列睡觉、时间转过早晨重置树、原来底部 Toast 的几句改说在各人字条上。不开场景、不调 Input。
public class PlayTests6
{
    // 中文鱼名表和 FishNames 一一对应：长度一致、每条鱼都有中文名、不重名
    [Test] public void FishNamesZhMatch()
    {
        Assert.AreEqual(Config.FishNames.Length, Config.FishNamesZh.Length);
        foreach (var n in Config.FishNamesZh) Assert.IsFalse(string.IsNullOrEmpty(n));
        CollectionAssert.AllItemsAreUnique(Config.FishNamesZh);
    }

    // 钓上那一刻在自己字条位置说的那句：按英文名查中文名
    [Test] public void CaughtSay() =>
        Assert.AreEqual(new[] { "钓上一条沙丁鱼！", "钓上一条鲷鱼！", "钓上一条河豚！" }, System.Array.ConvertAll(Config.FishNames, Hints.Caught));

    // 黄昏前（t < Config 里黄昏那一档的起点 0.5）床边不列睡觉，黄昏起（含夜里）列；P2 同理
    [Test] public void SleepHintFromDusk()
    {
        string Hint(int player, Vector3 p, float t) => Hints.Text(new Stock(), player, p, Vector3.forward, new Vector3[0], new int[0], FishState.Idle, true, true, evening: Sleep.Evening(t));
        Assert.AreEqual(0.5f, Sleep.Dusk, 1e-6f);
        foreach (var t in new[] { 0f, 0.25f, 0.49f })
            Assert.AreEqual(new[] { "", "" }, new[] { Hint(0, Config.BedPos, t), Hint(1, PlayTests.P2Bed, t) }, $"白天 t={t}");
        foreach (var t in new[] { 0.5f, 0.75f, 0.99f })
            Assert.AreEqual(new[] { "R 睡觉", "Num4 睡觉" }, new[] { Hint(0, Config.BedPos, t), Hint(1, PlayTests.P2Bed, t) }, $"黄昏起 t={t}");
    }

    // 时间自己转过早晨（从夜里绕回 t≈0）：和睡醒一样所有树重置；白天往前走不重置
    [Test] public void DawnResetsTrees()
    {
        int[] keep = Trees.Hits;
        try
        {
            Trees.Hits = new[] { Config.ChopHits, 1 };
            Assert.IsFalse(TimeOfDay.Dawn(0.3f, 0.31f), "白天往前走");
            Assert.AreEqual(new[] { Config.ChopHits, 1 }, Trees.Hits, "没跨早晨不重置");
            Assert.IsTrue(TimeOfDay.Dawn(0.995f, 0.002f), "夜里绕回早晨");
            Assert.AreEqual(new[] { 0, 0 }, Trees.Hits, "跨过早晨：所有树重置");
        }
        finally { Trees.Hits = keep; }
    }

    // 原来走底部 Toast 的几句改由 Hints.Say 说在各人自己的字条位置，字一个不改：换砖、砍树；睡醒两人都说「天亮了」
    [Test] public void SayInsteadOfToast()
    {
        Assert.AreEqual(new[] { "换得一砖", "换得两砖" }, new[] { Trees.TradeSay(1), Trees.TradeSay(Config.CookedBricks) });
        Assert.AreEqual(new[] { "这棵明天再砍", null, "得一木" }, new[] { Trees.ChopSay(0), Trees.ChopSay(1), Trees.ChopSay(Config.ChopHits) });
        int[] keepHits = Trees.Hits; Stock keep = Stock.Current;
        string old = PlayerPrefs.HasKey(PierSave.Key) ? PlayerPrefs.GetString(PierSave.Key) : null;
        try
        {
            Stock.Current = new Stock(); Hints.Say(0, "x"); Hints.Say(1, "y");
            Sleep.ToMorning(null, null);
            Assert.AreEqual(new[] { "天亮了", "天亮了" }, new[] { Hints.Said(0), Hints.Said(1) }, "睡醒两人都说");
        }
        finally
        {
            Trees.Hits = keepHits; Stock.Current = keep;
            if (old != null) PlayerPrefs.SetString(PierSave.Key, old); else PlayerPrefs.DeleteKey(PierSave.Key);
            PlayerPrefs.Save();
        }
    }
}
