using System.Linq;
using UnityEngine;

// 键表（纯数据）：每名玩家一套，取自 Config 的键位常量。T 与 F3 是全局键，不属于任何一人。
public sealed class KeyMap
{
    public readonly KeyCode Up, Down, Left, Right, Fish, Ctrl, Interact, Build, Hug, R;
    public readonly KeyCode[] Bait;

    public static readonly KeyMap[] Players = { new KeyMap(0), new KeyMap(1) };
    public static readonly KeyCode[] Global = { Config.NextPhaseKey, Config.HudKey };

    KeyMap(int p)
    {
        var m = Config.MoveKeys[p];
        Up = m[0]; Down = m[1]; Left = m[2]; Right = m[3];
        Fish = Config.FishKey[p]; Ctrl = Config.CtrlKey[p]; Interact = Config.InteractKey[p];
        Build = Config.BuildKey[p]; Hug = Config.HugKey[p]; R = Config.RKey[p];
        Bait = Config.BaitKeys[p];
    }

    public KeyCode[] All() => new[] { Up, Down, Left, Right, Fish, Ctrl, Interact, Build, Hug, R }.Concat(Bait).ToArray();
}
