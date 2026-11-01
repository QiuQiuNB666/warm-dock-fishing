using System;
using UnityEngine;

// 床和睡觉（loop-5 从 PierSave 拆出，只搬代码）：当前床点、早晨那一档、床边判定、睡到天亮、两人回床边。床边按 R 仍由 PierSave.Update 读。
public static class Sleep
{
    public static Vector3 Bed = Config.BedPos;   // 当前床点
    public static float Morning => Array.IndexOf(Config.TimeNames, "morning") / (float)Config.Times.Length;   // Config 里早晨那一档（t = 0）
    public static float Dusk => Array.IndexOf(Config.TimeNames, "dusk") / (float)Config.Times.Length;   // Config 里黄昏那一档的起点（t = 0.5）
    public static bool Evening(float t) => Mathf.Repeat(t, 1f) >= Dusk;   // loop-16 黄昏起（含夜里）头顶才列睡觉；白天 R 照旧能睡，只是不提示

    // R 睡觉：时间跳到早晨那一档，所有树重置（砍的状态不进存档），两人回床边（P2 偏开 P2BedOffset），两人各在自己字条位置说「天亮了」（loop-16，原来底部 Toast；loop-18 正在钓鱼的跳过，同自然天亮），立刻存档。不 Application.Quit。
    public static PierSave.Data ToMorning(TimeOfDay tod, PlayerManager pm)
    {
        if (tod != null) tod.t = Morning;
        Trees.NewDay(); PlaceAtBed(pm);
        Hints.Morning(TimeOfDay.Busy(TimeOfDay.Fish(pm)));   // loop-18 同自然天亮一套规矩（Hints.Morning）：正在钓鱼的人跳过
        return PierSave.Save(tod);
    }

    public static bool NearBed(Vector3 p) =>   // 离床（床点 + BedFromSpawn 就是床的中心）不超过 InteractRange
        Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Bed.x + Config.BedFromSpawn.x, Bed.z + Config.BedFromSpawn.z)) <= Config.InteractRange;

    // 两人站到床点（P2 偏开 P2BedOffset）：开局读档后、睡醒后
    public static void PlaceAtBed(PlayerManager pm)
    {
        if (pm == null) return;
        pm.Bind();
        foreach (var p in pm.players)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.transform.position = Bed + (p.playerIndex == 0 ? Vector3.zero : Config.P2BedOffset);
            if (cc != null) cc.enabled = true;
        }
    }
}
