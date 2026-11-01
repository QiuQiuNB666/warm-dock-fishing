using NUnit.Framework;
using UnityEngine;

// loop-16 的纯逻辑测试（PlayTests5 快满了另开）：中文鱼名与钓上说的那句、黄昏前不列睡觉、时间转过早晨重置树、原来底部 Toast 的几句改说在各人字条上；loop-19 按玩家号抄各人钓鱼状态、睡醒不拽走正在钓鱼的人。不开场景、不调 Input。
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

    // loop-19 按玩家号抄各人钓鱼状态（TimeOfDay.ByPlayer；Fish(pm) 只把在场各人映成对子）：乱序照玩家号放、缺人算 Idle、P2 正在钓 → busy 只有 P2
    [Test] public void ByPlayerIndex()
    {
        Assert.AreEqual(new[] { FishState.Bite, FishState.Wait }, TimeOfDay.ByPlayer(new[] { (1, FishState.Wait), (0, FishState.Bite) }), "乱序");
        Assert.AreEqual(new[] { FishState.Idle, FishState.Reel }, TimeOfDay.ByPlayer(new[] { (1, FishState.Reel) }), "缺 P1：算 Idle");
        Assert.AreEqual(new[] { false, true }, TimeOfDay.Busy(TimeOfDay.ByPlayer(new[] { (0, FishState.Idle), (1, FishState.Wait) })), "P2 正在钓");
    }

    // loop-19 睡醒不拽走正在钓鱼的人（Sleep.Wake，ToMorning 调它）：P2 在码头等鱼、P1 睡 → P2 不挪、不说「天亮了」，P1 回床边、说天亮了（同自然天亮）
    [Test] public void WakeLeavesFisher()
    {
        var pm = new GameObject("pm").AddComponent<PlayerManager>(); pm.Bind();   // EditMode 不跑 Awake：先 Bind，再换成两个空物体当人
        PlayerContext P(int i, Vector3 at) { var c = new GameObject("p" + i).AddComponent<PlayerContext>(); c.playerIndex = i; c.transform.position = at; return c; }
        pm.players = new[] { P(0, Sleep.Bed + Vector3.right), P(1, Config.FishSpot) };
        try
        {
            Hints.Say(0, "x"); Hints.Say(1, "y");
            Sleep.Wake(pm, TimeOfDay.Busy(TimeOfDay.ByPlayer(new[] { (1, FishState.Wait) })));
            Assert.Less(Vector3.Distance(Config.FishSpot, pm.players[1].transform.position), 1e-4f, "P2 在钓：不挪");
            Assert.Less(Vector3.Distance(Sleep.Bed, pm.players[0].transform.position), 1e-4f, "P1 回床边");
            Assert.AreEqual(new[] { "天亮了", "y" }, new[] { Hints.Said(0), Hints.Said(1) }, "只有 P1 说天亮了");
        }
        finally { foreach (var c in pm.players) Object.DestroyImmediate(c.gameObject); Object.DestroyImmediate(pm.gameObject); }
    }

    // loop-19 接线：Sleep.ToMorning 自己按各人此刻的钓鱼状态挑人（TimeOfDay.Fish 按玩家号映 → Busy → Wake）：P2 身上真有一份 FishingStateMachine、已抛竿（不 Idle），P1 睡
    // → P2 不挪、不说，P1 回床边说「天亮了」。ToMorning 会存档：照 SayInsteadOfToast 先备份、事后还原存档 / Stock.Current / Trees.Hits
    [Test] public void ToMorningLeavesFisher()
    {
        var pm = new GameObject("pm").AddComponent<PlayerManager>(); pm.Bind();
        PlayerContext P(int i, Vector3 at) { var c = new GameObject("p" + i).AddComponent<PlayerContext>(); c.playerIndex = i; c.transform.position = at; return c; }
        pm.players = new[] { P(0, Sleep.Bed + Vector3.right), P(1, Config.FishSpot) };
        var fsm = pm.players[1].gameObject.AddComponent<FishingStateMachine>();   // EditMode 不跑 Awake：Logic 走私有 setter 塞进去，Press 一下 Idle → Cast（不是 Idle 就算在钓）
        typeof(FishingStateMachine).GetProperty("Logic").SetValue(fsm, new FishingLogic(1)); fsm.Logic.Press(); pm.players[1].fishing = fsm;
        int[] keepHits = Trees.Hits; Stock keep = Stock.Current;
        string old = PlayerPrefs.HasKey(PierSave.Key) ? PlayerPrefs.GetString(PierSave.Key) : null;
        try
        {
            Stock.Current = new Stock(); Hints.Say(0, "x"); Hints.Say(1, "y");
            Sleep.ToMorning(null, pm);
            Assert.Less(Vector3.Distance(Config.FishSpot, pm.players[1].transform.position), 1e-4f, "P2 在钓：不挪");
            Assert.Less(Vector3.Distance(Sleep.Bed, pm.players[0].transform.position), 1e-4f, "P1 回床边");
            Assert.AreEqual(new[] { "天亮了", "y" }, new[] { Hints.Said(0), Hints.Said(1) }, "只有 P1 说天亮了");
        }
        finally
        {
            Trees.Hits = keepHits; Stock.Current = keep;
            if (old != null) PlayerPrefs.SetString(PierSave.Key, old); else PlayerPrefs.DeleteKey(PierSave.Key);
            PlayerPrefs.Save();
            foreach (var c in pm.players) Object.DestroyImmediate(c.gameObject); Object.DestroyImmediate(pm.gameObject);
        }
    }
}
