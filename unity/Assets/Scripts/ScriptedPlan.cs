using System;
using UnityEngine;

// 实玩计划（ScriptedPlay 的另一半，loop-10 从 ScriptedPlay.cs 搬来，它满了）：计划和 play_input.ps1 同一套词：wait / down / up / tap / until（press / every / timeout）/ shot，
// 另加 save（PierSave.Save）、quit、teleport（实玩摆人，见 Teleport；少于 3 个数打 SCRIPT-SKIP teleport）；click 不支持，打 SCRIPT-SKIP click 跳过。键码是 Windows 虚拟键，由 Key() 映射成 KeyCode。
public partial class ScriptedPlay
{
    [Serializable]
    public class Step
    {
        public int wait, tap, press, every, timeout;
        public int[] down, up;
        public string until, shot;
        public bool save, quit;
        public float[] click, teleport;   // teleport = [玩家号, x, z, 朝向°（可省）]
    }

    [Serializable] class Plan { public Step[] s; }

    // 计划 = 步骤数组；没写的字段取默认值（0 / null / false），空串当空计划
    public static Step[] Parse(string json) =>
        string.IsNullOrWhiteSpace(json) ? new Step[0] : JsonUtility.FromJson<Plan>("{\"s\":" + json + "}").s ?? new Step[0];

    // Windows 虚拟键 → KeyCode：字母、主键盘数字、小键盘 0–9、方向键、空格、Esc；别的是 None
    public static KeyCode Key(int vk) => vk switch
    {
        >= 65 and <= 90 => KeyCode.A + (vk - 65),
        >= 48 and <= 57 => KeyCode.Alpha0 + (vk - 48),
        >= 96 and <= 105 => KeyCode.Keypad0 + (vk - 96),
        37 => KeyCode.LeftArrow, 38 => KeyCode.UpArrow, 39 => KeyCode.RightArrow, 40 => KeyCode.DownArrow,
        32 => KeyCode.Space, 27 => KeyCode.Escape,
        _ => KeyCode.None,
    };
}
