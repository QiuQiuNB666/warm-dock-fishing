using NUnit.Framework;
using UnityEngine;

// loop-13 拥抱的纯逻辑测试（PlayTests 满了另开）：能不能抱、面对面的朝向、HugTime 后结束与进出平滑、抱着不读移动、「H 抱抱」排最后只补空位。不开场景、不调 Input。
public class PlayTests5
{
    static readonly Vector3 Sand = new Vector3(2f, Config.GroundY, -4f);   // 沙滩空地：离床、门口、篝火、码头钓点都远，近处没树（树都在 z ≤ -9）

    [Test]
    public void HugNeedsCloseIdleUnpaused()
    {
        var b = Sand + new Vector3(1f, 0.3f, 0f);   // 相距 1 m，高度差不算
        Assert.IsTrue(Hug.Can(Sand, b, FishState.Idle, FishState.Idle, false));
        Assert.IsTrue(Hug.Can(Sand, Sand + new Vector3(0f, 0f, Config.HugRange), FishState.Idle, FishState.Idle, false), "正好 HugRange 也行");
        Assert.IsFalse(Hug.Can(Sand, Sand + new Vector3(Config.HugRange + 0.1f, 0f, 0f), FishState.Idle, FishState.Idle, false), "太远");
        foreach (FishState f in System.Enum.GetValues(typeof(FishState)))
            if (f != FishState.Idle)
            {
                Assert.IsFalse(Hug.Can(Sand, b, f, FishState.Idle, false), $"P1 {f} 时不能抱");
                Assert.IsFalse(Hug.Can(Sand, b, FishState.Idle, f, false), $"P2 {f} 时不能抱");
            }
        Assert.IsFalse(Hug.Can(Sand, b, FishState.Idle, FishState.Idle, true), "暂停时不能抱");
    }

    [Test]
    public void HugFacesEachOther()
    {
        var b = Sand + new Vector3(0.6f, 0.2f, 0.8f);
        Vector3 fa = Hug.Face(Sand, b), fb = Hug.Face(b, Sand);
        Assert.AreEqual(1f, fa.magnitude, 1e-4f);
        Assert.AreEqual(0f, fa.y, "只在水平面上转");
        Assert.AreEqual(1f, Vector3.Dot(fa, new Vector3(0.6f, 0f, 0.8f)), 1e-4f, "朝着对方");
        Assert.AreEqual(-1f, Vector3.Dot(fa, fb), 1e-4f, "面对面");
        Assert.AreEqual(Vector3.zero, Hug.Face(Sand, Sand), "重合时不转");
    }

    [Test]
    public void HugEndsAfterHugTimeAndLocksMove()
    {
        const float t0 = 10f;
        Assert.AreEqual(1.5f, Config.HugTime);
        Assert.IsTrue(Hug.Active(t0, t0)); Assert.IsTrue(Hug.Active(t0, t0 + 1.49f));
        Assert.IsFalse(Hug.Active(t0, t0 + Config.HugTime), "1.5 s 后结束");
        Assert.IsFalse(Hug.Active(t0, t0 - 0.1f));
        Assert.AreEqual(0f, Hug.Weight(t0, t0), 1e-4f);
        Assert.AreEqual(1f, Hug.Weight(t0, t0 + 0.75f), 1e-4f);
        float rise = Hug.Weight(t0, t0 + Config.HugBlend * 0.5f), fall = Hug.Weight(t0, t0 + Config.HugTime - Config.HugBlend * 0.5f);
        Assert.That(rise, Is.InRange(0.1f, 0.9f), "进：平滑，不瞬切");
        Assert.AreEqual(rise, fall, 1e-4f, "出：同样平滑");
        Assert.AreEqual(0f, Hug.Weight(t0, t0 + Config.HugTime), 1e-4f);
        float keep = Hug.Since;   // loop-14：Still 自己读 Hug.Since（PlayerController 的接线，删掉这两条就挂）
        try { Hug.Since = t0; Assert.IsTrue(PlayerController.Still(false, false, t0 + 0.75f), "抱着不读移动"); Assert.IsFalse(PlayerController.Still(false, false, t0 + 1.6f), "抱完照旧能走"); }
        finally { Hug.Since = keep; }
    }

    // 开局床边两人挨着仍是「R 睡觉　WASD 走…」（没有 H 抱抱）；钓过鱼、空着一格才补；P2 叫 Num3；不能抱就不列
    [Test]
    public void HugHintOnlyFillsFreeSlot()
    {
        var s = new Stock();
        string Hint(int player, Vector3 p, bool fished, bool hug = true) => Hints.Text(s, player, p, Vector3.forward, new Vector3[0], new int[0], FishState.Idle, fished, false, hug: hug);
        Vector3 bed = Sleep.Bed, bed2 = Sleep.Bed + Config.P2BedOffset;
        Assert.AreEqual("R 睡觉　WASD 走去码头尽头钓鱼", Hint(0, bed, false));
        Assert.AreEqual("Num4 睡觉　方向键走去码头尽头钓鱼", Hint(1, bed2, false));
        Assert.AreEqual("R 睡觉　H 抱抱", Hint(0, bed, true));
        Assert.AreEqual("Num4 睡觉　Num3 抱抱", Hint(1, bed2, true));
        Assert.AreEqual("WASD 走去码头尽头钓鱼　H 抱抱", Hint(0, Sand, false));
        Assert.AreEqual("H 抱抱", Hint(0, Sand, true));
        Assert.AreEqual("", Hint(0, Sand, true, false));
    }

    // loop-14 冷却：抱着、抱完 HugCooldown 秒内都不能再抱（读键和「H 抱抱」都走 Ready → Can），连按 H 锁不住对方
    [Test]
    public void HugCooldownBlocksAgain()
    {
        var b = Sand + Vector3.right;
        Assert.IsFalse(Hug.Can(Sand, b, FishState.Idle, FishState.Idle, false, 0.5f), "抱着不能再抱");
        Assert.IsFalse(Hug.Can(Sand, b, FishState.Idle, FishState.Idle, false, Config.HugTime + 1f), "冷却中不能抱");
        Assert.IsTrue(Hug.Can(Sand, b, FishState.Idle, FishState.Idle, false, Config.HugTime + Config.HugCooldown), "冷却完能抱");
    }

    // loop-14 接线：抱着按 Space 一律不算（FishingStateMachine.WillPress）
    [Test]
    public void HugBlocksCast()
    {
        foreach (bool busy in new[] { false, true })
            foreach (bool spot in new[] { false, true })
                Assert.IsFalse(FishingStateMachine.WillPress(true, true, busy, spot), $"抱着不抛竿 busy={busy} spot={spot}");
        Assert.IsTrue(FishingStateMachine.WillPress(true, false, false, true), "不抱照旧能抛竿");
        Assert.IsFalse(FishingStateMachine.WillPress(false, false, true, true), "没按不算");
    }

    // loop-14 抱得像抱：间距收到 HugGap、HugClose 秒走完不弹回；抱前就朝着对方的转向和连线垂直、离镜头近的一侧（并排 = 镜头），否则转回去；南北站位挨着时靠下那人的字条到脚下（抱完不动也算）
    [Test]
    public void HugClosesInAndTurnsBack()
    {
        Assert.AreEqual(0.05f, Hug.Close(1f), 1e-4f, "相距 1 m 各走 0.05 m，收到 0.9 m");
        Assert.AreEqual(0f, Hug.Close(0.6f), "本来就够近不挪");
        Assert.AreEqual(0f, Hug.Walked(10f, 10f));
        Assert.AreEqual(1f, Hug.Walked(10f, 10f + Config.HugClose), 1e-4f);
        Assert.AreEqual(1f, Hug.Walked(10f, 30f), "走完停住，不弹回");
        Assert.AreEqual(Vector3.back, Hug.After(Vector3.right, Vector3.right), "本来就面对面：转向镜头");
        Assert.AreEqual(Vector3.forward, Hug.After(Vector3.forward, Vector3.right), "否则转回抱前的朝向");
        Vector3 s = Hug.After(Vector3.forward, Vector3.forward), n = Hug.After(Vector3.back, Vector3.back);   // 南北站位面对面
        Assert.AreNotEqual(Vector3.back, s, "南北面对面：不转向镜头（北边那根竿会横过南边那人）");
        Assert.AreEqual(s, n, "两人转同一侧，竿平行"); Assert.AreEqual(0f, Vector3.Dot(s, Vector3.forward), 1e-4f, "和两人连线垂直");
        var north = Sand + Vector3.forward;
        Assert.IsTrue(Hug.Below(Sand, new[] { Sand, north }), "南边那人在下");
        Assert.IsFalse(Hug.Below(north, new[] { Sand, north }), "北边那人不在下");
        Assert.IsFalse(Hug.Below(Sand, new[] { Sand, Sand + Vector3.right }), "左右并排都不算");
        Vector3 ns = Sand + Vector3.forward * Config.HugGap, ew = Sand + Vector3.right * Config.HugGap;   // 抱完两人留在 HugGap 不动（Hug.On 已为假）
        Assert.IsTrue(Hints.Low(false, Sand, new[] { Sand, ns }), "抱完南北站位：靠南那人的字条仍走脚下");
        Assert.IsFalse(Hints.Low(false, Sand, new[] { Sand, ew }), "左右并排不走脚下");
        Assert.IsFalse(Hints.Low(false, Sand, new[] { Sand, Sand + Vector3.forward * (Config.HugRange + 0.5f) }), "南北离得远（> HugRange）不走脚下");
    }

    // loop-14 修补二：南北为主时两人都转西（握竿的右手在各自北侧、藏在自己帽檐下）；北边那人偏东 0.1 m 也一样（原来取 z 小的一侧，一偏东两人就都转东）
    [Test]
    public void HugNorthSouthTurnsWest()
    {
        foreach (float dx in new[] { 0.1f, 0f, -0.1f })   // 北边那人偏东 / 正北 / 偏西：facing 带一点 x
        {
            var north = Sand + new Vector3(dx, 0f, 1f);
            Vector3 s = Hug.After(Vector3.forward, Hug.Face(Sand, north)), n = Hug.After(Vector3.back, Hug.Face(north, Sand));
            Assert.Less(s.x, 0f, $"南边那人转西 dx={dx}"); Assert.Less(n.x, 0f, $"北边那人转西 dx={dx}");
            Assert.AreEqual(s, n, $"两人转同一侧，竿平行 dx={dx}");
        }
    }
}
