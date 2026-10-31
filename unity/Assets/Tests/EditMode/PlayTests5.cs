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
        Assert.IsTrue(PlayerController.Still(false, false, Hug.Active(t0, t0 + 0.75f)), "抱着不读移动");
        Assert.IsFalse(PlayerController.Still(false, false, Hug.Active(t0, t0 + 1.6f)), "抱完照旧能走");
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
}
