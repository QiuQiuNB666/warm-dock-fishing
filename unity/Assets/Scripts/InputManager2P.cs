using System;
using UnityEngine;

// 双人键盘：每人只读自己的键表。按键状态通过 Held / Pressed 读取，测试可以传入自己的函数，不碰 Input.GetKey。
public static class InputManager2P
{
    public static Func<KeyCode, bool> Held = Input.GetKey;
    public static Func<KeyCode, bool> Pressed = Input.GetKeyDown;

    // 鼠标 / 触摸只在单人时用（双人时移动、抛竿都只认键盘）
    public static bool PointerAllowed => Config.PlayerCount == 1;

    // XZ 平面上的移动方向：单位长度（对角线归一化）或零；第 p 名玩家不在场（单人时的 P2）恒为零
    public static Vector3 Move(int p, Func<KeyCode, bool> held = null)
    {
        if (p < 0 || p >= Config.PlayerCount) return Vector3.zero;
        held ??= Held;
        var k = KeyMap.Players[p];
        var v = new Vector3(Axis(held, k.Left, k.Right), 0f, Axis(held, k.Down, k.Up));
        return v.sqrMagnitude > 0f ? v.normalized : Vector3.zero;
    }

    static float Axis(Func<KeyCode, bool> held, KeyCode neg, KeyCode pos) => (held(pos) ? 1f : 0f) - (held(neg) ? 1f : 0f);

    public static bool FishDown(int p) => Pressed(KeyMap.Players[p].Fish);
    public static bool FishHeld(int p) => Held(KeyMap.Players[p].Fish);

    // 本帧按下的饵键下标，没按为 -1
    public static int BaitDown(int p)
    {
        var keys = KeyMap.Players[p].Bait;
        for (int i = 0; i < keys.Length; i++) if (Pressed(keys[i])) return i;
        return -1;
    }
}
