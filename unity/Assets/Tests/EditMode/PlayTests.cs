using NUnit.Framework;
using UnityEngine;

// 自迭代环的玩法纯逻辑测试（CoopTests 已满 150 行）。不开场景、不调 Input。
public class PlayTests
{
    static readonly Vector3[] Grove = { new Vector3(-5.84f, Config.GroundY, -10.16f), new Vector3(-8.23f, Config.GroundY, -10.2f) };   // 场景里最靠近床的两棵

    // loop-1：够不着不算（只看 XZ，差 5 cm 也不行）；两棵都够得着时砍最近的那棵
    [Test] public void ChopNeedsReach()
    {
        var s = new Stock();
        Vector3 far = Grove[0] + new Vector3(Config.ChopRange + 0.05f, 0f, 0f);
        Assert.AreEqual(-1, Trees.Nearest(Grove, far), "差 5 cm 够不着");
        Assert.AreEqual(-1, Trees.Interact(s, 0, far, Grove), "够不着、也不在门口：E 什么都不做");
        Assert.AreEqual(0, Trees.Nearest(Grove, Grove[0] + new Vector3(0f, 3f, Config.ChopRange - 0.05f)), "高度不算");
        Assert.AreEqual(1, Trees.Nearest(Grove, Grove[1] + new Vector3(1.1f, 0f, 0f)), "两棵都够得着：最近的");
        Assert.AreEqual(new[] { 0, 0 }, new[] { s.wood, s.brick });
    }

    // loop-1：砍满三下得一木，别的树不受影响
    [Test] public void ThreeHitsOneWood()
    {
        var s = new Stock(); var hits = new int[Grove.Length];
        Assert.AreEqual(new[] { 1, 2 }, new[] { Trees.Chop(s, hits, 0), Trees.Chop(s, hits, 0) });
        Assert.AreEqual(0, s.wood, "两下还没有木");
        Assert.AreEqual(3, Trees.Chop(s, hits, 0));
        Assert.AreEqual(1, s.wood, "第三下得一木");
        Assert.AreEqual(0, hits[1], "别的树不受影响");
    }

    // loop-1：同一天第二次不出木：出过木的树再砍返回 0（提示明天再砍、不晃），不加木
    [Test] public void OneWoodPerTreePerDay()
    {
        var s = new Stock(); var hits = new int[Grove.Length];
        for (int i = 0; i < Config.ChopHits; i++) Trees.Chop(s, hits, 0);
        Assert.AreEqual(new[] { 0, 0 }, new[] { Trees.Chop(s, hits, 0), Trees.Chop(s, hits, 0) });
        Assert.AreEqual(1, s.wood);
    }

    // loop-1：R 睡醒（PierSave.Sleep）后所有树重置，又能砍
    [Test] public void SleepResetsTrees()
    {
        int[] keepHits = Trees.Hits; Stock keep = Stock.Current;
        string old = PlayerPrefs.HasKey(PierSave.Key) ? PlayerPrefs.GetString(PierSave.Key) : null;
        try
        {
            Stock.Current = new Stock(); Trees.Hits = new[] { Config.ChopHits, 1 };
            PierSave.Sleep(null, null);
            Assert.AreEqual(new[] { 0, 0 }, Trees.Hits, "睡醒后所有树重置");
            Assert.AreEqual(1, Trees.Chop(Stock.Current, Trees.Hits, 0), "又能砍了");
        }
        finally
        {
            Trees.Hits = keepHits; Stock.Current = keep;
            if (old != null) PlayerPrefs.SetString(PierSave.Key, old); else PlayerPrefs.DeleteKey(PierSave.Key);
            PlayerPrefs.Save();
        }
    }

    // loop-1：E 同一时刻只做一件事：门口有鱼先换砖，没鱼才砍树（场景里门口 6 m 内没有树，这里假放一棵，只看先后）
    [Test] public void DoorTradeBeforeChop()
    {
        var s = new Stock(); var trees = new[] { Config.DoorPos + new Vector3(0f, 0f, -1f) };
        Assert.AreEqual(0, Trees.Interact(s, 0, Config.DoorPos, trees), "没鱼：砍树");
        s.Add(ItemType.RawFish, 1, 0);
        Assert.AreEqual(Trees.Traded, Trees.Interact(s, 0, Config.DoorPos, trees), "有鱼：先换砖");
        Assert.AreEqual(new[] { 0, 1 }, new[] { s.Count(ItemType.RawFish, 0), s.brick });
    }
}
