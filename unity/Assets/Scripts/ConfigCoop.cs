using UnityEngine;

// 双人合作的常量（子阶段 0 起）。原有常量仍在 Config.cs；这里只放新增的，都是数据，本阶段不在 Update 里读。
public static partial class Config
{
    public static int PlayerCount = 2;   // 故意不用 const：单人/双人运行时可切换

    public const Gender GenderDefault = Gender.Male, GenderP1 = Gender.Male, GenderP2 = Gender.Female;   // 只是数据，不改体型

    public const float CamMaxZoomOut = 1.2f;   // 相机距离 = CamDistance × zoom，zoom ∈ [1, CamMaxZoomOut]
    public const float CamFrameMargin = 0.75f;   // 双人时两人都要落在画面半宽/半高的 75% 以内，否则拉远

    // 床：小屋门前的沙滩上，两人出生在床边。CabinPos + (1.2, 0, 0.8) 落在小屋网格里，沿沙滩 +x 平移 1.6 m 出来。
    // 用属性现算：CabinPos 在另一个 partial 文件里，跨文件的静态字段初始化顺序不保证。
    public static Vector3 BedPos => CabinPos + new Vector3(1.2f + 1.6f, 0f, 0.8f);
    public static readonly Vector3 P2BedOffset = new Vector3(0.7f, 0f, 0f);
    public static readonly Vector3 BedFromSpawn = new Vector3(0.35f, 0f, 0.95f);   // 床摆在两人身后（离相机更远的一侧）
    public static readonly Vector3 BedSize = new Vector3(2f, 0.3f, 1f);
    public static readonly Color Bedding = new Color(0.95f, 0.93f, 0.86f);
    public static readonly Color HatBandP2 = new Color32(0x7F, 0xA8, 0x98, 0xFF);
    public static readonly Color HatStraw = new Color(0.89f, 0.76f, 0.48f);   // blender/rig.py 的草帽色
    public static readonly Color[] HatDot = { HatStraw, HatBandP2 };   // 屏外指示点：P1 草帽色，P2 帽带色

    // 键位：只做常量，本阶段不接线。下标 0 = P1，1 = P2。
    public static readonly KeyCode[][] MoveKeys =   // 上、下、左、右
    {
        new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D },
        new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow },
    };
    public static readonly KeyCode[] FishKey = { KeyCode.Space, KeyCode.Keypad5 };
    public static readonly KeyCode[] CtrlKey = { KeyCode.LeftControl, KeyCode.Keypad0 };   // 用途在后续子阶段定
    public static readonly KeyCode[] InteractKey = { KeyCode.E, KeyCode.Keypad1 };
    public static readonly KeyCode[] BuildKey = { KeyCode.B, KeyCode.Keypad2 };
    public static readonly KeyCode[] HugKey = { KeyCode.H, KeyCode.Keypad3 };
    public static readonly KeyCode[] RKey = { KeyCode.R, KeyCode.Keypad4 };   // 用途在后续子阶段定
    public static readonly KeyCode[][] BaitKeys =
    {
        new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 },
        new[] { KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9 },
    };
    public const KeyCode NextPhaseKey = KeyCode.T, HudKey = KeyCode.F3;   // 全局，两人共用

    public const float AutosaveSec = 30f;
    public const float CellSize = 1f;
    public const int MaxHeight = 4;
    public const int MaxBrick = 999, MaxWood = 999, MaxStone = 999;
    public const int MaxFish = 64, MaxCloth = 64;   // 生鱼、熟鱼、布
    public const float InteractRange = 2f, HugRange = 1.5f;

    // 新开局（没有存档）库存里给 TestBricks 块砖。play 起为 0：一块都不送，砖只来自用鱼换。
    public const int TestBricks = 0;
    public const float BrickSize = 0.9f;
    public static readonly Color BrickColor = new Color32(0xC4, 0x78, 0x5A, 0xFF);   // 陶土色
}
