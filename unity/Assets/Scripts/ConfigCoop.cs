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
    // 小屋门口：门在 +X 墙（blender/model.py 的 SM_cabin 对中后墙在 CabinPos.x + 1.225，石阶外沿 +1.575），取石阶上一点。
    public static Vector3 DoorPos => CabinPos + new Vector3(1.4f, 0f, 0f);
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
    // loop-13 拥抱：两人 XZ ≤ HugRange 按 H 一起抱 HugTime 秒；双臂进出各 HugBlend 秒平滑。姿势 = 大臂朝 HugUpper、小臂朝 HugFore（角色本地：x 往外为正、y 上、z 前；左右臂 x 镜像）
    // loop-14：开始后 HugClose 秒内两人沿连线各往中点挪，间距收到 HugGap（帽檐半径 0.44 m（rig.py BRIM）×2 = 0.88 m 以外，两顶帽檐不交叠；胶囊 PlayerRadius×2 = 0.7 m 自然也不撞）；抱着和抱完 HugCooldown 秒内不能再抱；大臂少张、几乎朝前，小臂朝前略偏内（HugGap 0.9 m 时手越过中线约 6 cm，两人小臂在镜头那一侧交叉；原来的往外张在 0.9 m 只够两只拳头在中线碰头，像碰拳）；
    // 抱前就朝着对方（点积 > HugFaced）的人抱完转向和两人连线垂直的一侧（左右为主取离镜头近的，并排即镜头；南北为主两人都取西），不再转回去对着（两根竿交叉）；抱着时两人头顶中间 HugHeart 高处画一个「♥」（南北站位不画）
    public const float HugTime = 1.5f, HugBlend = 0.15f, HugClose = 0.25f, HugGap = 0.9f, HugCooldown = 2f, HugFaced = 0.7f, HugHeart = 2.1f;
    public static readonly Vector3 HugUpper = new Vector3(0.3f, -0.15f, 0.94f), HugFore = new Vector3(-0.1f, 0f, 1f);
    // loop-1 砍树：人离树干（树的 XZ 位置）≤ ChopRange 按 E 砍一下（树干碰撞盒挡着人，站旁边要够得着），满 ChopHits 下得 1 木；
    // 每砍一下树绕树根摆 ChopShakeSec 秒、最大 ChopShakeDeg 度，幅度衰减到 0
    public const float ChopRange = 1.6f, ChopShakeSec = 0.3f, ChopShakeDeg = 5f;
    // loop-7 只认站在树南侧（离相机近的一侧）的人：人的 z ≤ 树干 z + ChopSouth，人不会钻进树冠里看不见
    public const float ChopSouth = 0.3f;
    public const int ChopHits = 3;
    // loop-3 就近提示：画在角色根节点上方 HintHeight，HintFont 号字，最多列 HintMax 个动作。
    // loop-4：2.2 → 2.6，俯角 60° 下帽檐远端（半径 0.44 m）投到屏幕上比帽顶还高，2.2 时字条压着帽檐。
    // 字色 = 各自的帽子色（HatDot）往白提亮 HintLighten：原色直接放在半透明底上，P2 的帽带绿跟底几乎一样亮
    public const float HintHeight = 2.6f, HintLighten = 0.4f;
    public const int HintFont = 16, HintMax = 2;
    // loop-6 修补：字条上沿不高于 HintTop（顶部正中「砖 n　木 n」框 Bricks.OnGUI y 8–TopBarBottom 的下沿再留 4 px；loop-10 两边共用 TopBarBottom），头顶放不下就画到脚下 HintFeetGap px，
    // 从脚下换回头顶要比 HintTop 再低 HintBack px（loop-10 回差，免得在边界上来回跳）；兜底（走去码头尽头钓鱼 / 拿鱼去换砖、去烤）站定 HintStill 秒才画；
    // 钓鱼失败那句在失败那人自己的字条位置留 HintFail 秒（同底部 Toast 的 1.5 s）；收线时字条下沿一道 HintBar px 高的张力条，张力 > HintRed 变 Bobber 红（loop-10）
    public const float TopBarBottom = 48f, HintTop = TopBarBottom + 4f, HintFeetGap = 4f, HintStill = 1f, HintFail = 1.5f, HintBack = 8f, HintBar = 4f, HintRed = 0.8f;

    // 新开局（没有存档）库存里给 TestBricks 块砖。play 起为 0：一块都不送，砖只来自用鱼换。
    public const int TestBricks = 0;
    public const float BrickSize = 0.9f;
    // loop-4 挡砖：格子四边各内缩 BlockInset 再和场景物体的 XZ 矩形比（只擦边不算）；碰撞体顶面高出地面不到 BlockInset 的（沙滩台地、木板）不挡
    public const float BlockInset = 0.05f;
    // loop-7 别把路堵死：码头根部（z = DockMid.z − DockLength/2）朝沙滩 DockEntryDepth 格深、比码头左右各宽 1 格的矩形不放砖（Bricks.Reserved，小屋门口那一格也保留）。
    // 人的胶囊半径 PlayerRadius（BuildCharacter 的 CharacterController 也用它）：人站的格子和胶囊压进砖位的相邻格不放砖（Bricks.UnderFoot）
    public const int DockEntryDepth = 3;
    public static Rect DockEntry => new Rect(DockMid.x - DockWidth * 0.5f - CellSize, DockMid.z - DockLength * 0.5f - DockEntryDepth * CellSize, DockWidth + 2f * CellSize, DockEntryDepth * CellSize);
    public const float PlayerRadius = 0.35f;
    // loop-7 修补：放砖不许切断床点和各人去码头入口的路（BrickPath.Open：B 按下时算一次；头顶「B 放砖」每 PathEvery 秒算一次）
    public const float PathEvery = 0.2f;
    public static readonly Color BrickColor = new Color32(0xC4, 0x78, 0x5A, 0xFF);   // 陶土色
    // loop-9 篝火：床与码头之间的空沙地（离门口 4.6 m、床心 4.4 m、码头入口保留区 2.5 m、最近的树 5.7 m）。人离它 ≤ InteractRange 按 E：
    // 这人有生鱼、两人共用木 ≥1 → 木 −1、生鱼 −1、熟鱼 +1（Trees.Cook）；门口一条熟鱼换 CookedBricks 块砖（Bricks.Trade，熟鱼优先）
    public static Vector3 FirePos => new Vector3(-2.5f, GroundY, -5.5f);
    public static readonly Vector3 FireSize = new Vector3(1.1f, 0.7f, 1.1f);   // SM_campfire 的外框（缺模型时的方块占位；测试里当占地）
    public const int CookedBricks = 2;
    // loop-10 篝火点光源离地 FireLampY（帽顶以上；原来 1.2 m 比帽顶低，从上往下看的帽顶照不亮）、照 FireLampRange：夜里火边两顶帽子的颜色看得清
    public const float FireLampY = 2.3f, FireLampRange = 7f;
    // loop-11 火苗自发光 = LampColor ×（0.05 + Glow × FireGlow），用自己的一份 M_glow（TimeOfDay.fire）：灯笼/窗是 ×4，火苗用 ×4 夜里过曝成一团白星。实玩：只压倍数（0.8、0.3）火苗仍是米白——它自己的点光源照在 M_glow 的奶油底色上；所以火苗那份底色改用 LampColor，倍数取 0.6
    public const float FireGlow = 0.6f;
    // loop-12 小灯塔顶块同火苗的办法：自己的一份 M_glow（TimeOfDay.towerGlow，底色 LampColor），自发光 = LampColor ×（0.05 + Glow × TowerGlow）。原来共用 ×4，夜里一整块没边的纯白（像 UI 故障）；比火苗略高，夜里读得出暖黄、看得出方块的边
    public const float TowerGlow = 0.8f;
    // loop-12 修补：顶面和正面同一份材质，夜里两面只差 3–5 级（红通道顶满），看不出棱。顶面另贴一片 Quad 用 towerGlow，方块本身（侧面）用第二份，发光再 ×TowerSide。
    // 不靠降 TowerGlow：Gamma 色彩空间、没有色调映射，两面的差只来自漫反射（夜里月光从镜头这侧斜照，正面受光反比顶面多），跟发光倍数无关
    public const float TowerSide = 0.7f;
    // loop-11 开局两张字条叠在一起时左右分开，中间留 HintGap px（原来 4 px，一眼分不出是两张）
    public const float HintGap = 12f;
}
