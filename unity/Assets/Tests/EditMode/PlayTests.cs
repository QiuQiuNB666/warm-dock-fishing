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

    // loop-2：-script 计划里的键码是 Windows 虚拟键（同 play_input.ps1），映射成 KeyCode
    [TestCase(87, KeyCode.W), TestCase(69, KeyCode.E), TestCase(32, KeyCode.Space), TestCase(27, KeyCode.Escape),
     TestCase(37, KeyCode.LeftArrow), TestCase(38, KeyCode.UpArrow), TestCase(39, KeyCode.RightArrow), TestCase(40, KeyCode.DownArrow),
     TestCase(97, KeyCode.Keypad1)]
    public void ScriptKeyMap(int vk, KeyCode key) => Assert.AreEqual(key, ScriptedPlay.Key(vk));

    // loop-2：计划缺字段不炸：没写的取默认值，空串是空计划
    [Test] public void ScriptPlanMissingFields()
    {
        var s = ScriptedPlay.Parse("[{\"wait\":800},{\"until\":\"CAUGHT p0\"},{},{\"click\":[0.5,0.53]},{\"save\":true,\"shot\":\"a.png\"}]");
        Assert.AreEqual(5, s.Length);
        Assert.AreEqual(800, s[0].wait);
        Assert.AreEqual(new[] { 0, 0, 0 }, new[] { s[1].press, s[1].every, s[1].timeout }, "until 只写了正则");
        Assert.IsTrue((s[2].down?.Length ?? 0) == 0 && s[2].tap == 0 && !s[2].save && string.IsNullOrEmpty(s[2].until), "空步骤");
        Assert.AreEqual(2, s[3].click.Length);
        Assert.IsTrue(s[4].save && s[4].shot == "a.png" && !s[4].quit);
        Assert.AreEqual(0, ScriptedPlay.Parse("").Length, "空计划");
    }

    // loop-3 就近提示：面朝 +Z；树用 Grove（hits 不给就是今天都没砍过）
    static string Hint(Stock s, int player, Vector3 p, int[] hits = null, bool idle = true) =>
        Hints.Text(s, player, p, Vector3.forward, Grove, hits ?? new int[Grove.Length], idle);
    static readonly Vector3 P2Bed = Config.BedPos + Config.P2BedOffset, Open = new Vector3(-5.2f, Config.GroundY, -6.3f);   // P2 出生点；床和树之间的空沙地

    [Test] public void HintDoorNeedsFish()
    {
        var s = new Stock();
        Assert.AreEqual("", Hint(s, 0, Config.DoorPos), "门口没鱼：E 什么都不做，不提示");
        s.Add(ItemType.RawFish, 1, 0);
        Assert.AreEqual("E 换砖", Hint(s, 0, Config.DoorPos));
        Assert.AreEqual("", Hint(s, 1, Config.DoorPos), "鱼是 P1 的，P2 在门口换不成");
    }

    [Test] public void HintTreeOnlyWhenFresh()
    {
        Vector3 p = Grove[0] + new Vector3(1f, 0f, 0f);
        Assert.AreEqual("E 砍树", Hint(new Stock(), 0, p));
        Assert.AreEqual("", Hint(new Stock(), 0, p, new[] { Config.ChopHits, 0 }), "今天出过木的树不提示");
    }

    [Test] public void HintBed() => Assert.AreEqual("R 睡觉", Hint(new Stock(), 0, Config.BedPos), "床边（也在门口，但没鱼）");

    [Test] public void HintP2Keys()   // 照实玩塞上床的矩形（loop-4）：P2 开局面朝 +Z，面前那一格是床，不提示放砖
    {
        var s = new Stock { brick = 1 }; s.Add(ItemType.RawFish, 1, 1);
        Bricks.Blockers.Clear(); Bricks.Block(new Bounds(Config.BedPos + Config.BedFromSpawn, Config.BedSize));
        try
        {
            Assert.AreEqual("Num1 换砖　Num2 放砖", Hint(s, 1, Config.DoorPos));
            Assert.AreEqual("Num4 睡觉", Hint(s, 1, P2Bed), "面前是床");
            Assert.AreEqual("Num5 抛竿", Hint(s, 1, Config.FishSpot));
        }
        finally { Bricks.Blockers.Clear(); }   // 清回空表（EditMode 里本来就是空的），别的测试照旧是原规则
    }

    [Test] public void HintAtMostTwo()
    {
        var s = new Stock { brick = 3 }; s.Add(ItemType.RawFish, 2, 0);   // P1 在床边也在门口；没塞床的矩形，面前那格算能放（实玩里那是床，见 HintP2Keys）
        Assert.AreEqual("E 换砖　R 睡觉", Hint(s, 0, Config.BedPos), "换砖、睡觉、放砖都成：只列前两个");
        Assert.AreEqual("B 放砖", Hint(s, 0, Open));
    }

    [Test] public void HintNothingNear()
    {
        Assert.AreEqual("", Hint(new Stock(), 0, Open), "空地、没砖");
        Assert.AreEqual("空格 抛竿", Hint(new Stock(), 0, Config.FishSpot));
        Assert.AreEqual("", Hint(new Stock(), 0, Config.FishSpot, idle: false), "钓鱼过程中不提示");
    }

    [Test] public void HintLabelsApart()   // 两人挨着时两张字条左右分开，左边的还在左边
    {
        Rect a = new Rect(150f, 50f, 140f, 26f), b = new Rect(100f, 50f, 120f, 26f);
        Hints.Apart(ref a, ref b);
        Assert.IsTrue(!a.Overlaps(b) && b.xMax < a.x, $"{a} {b}");
    }
}
