using UnityEngine;

// 场景搭建、玩法、测试共用的唯一一份常量。颜色是占位色卡，Phase 1 读参考图后按 decisions.md 色卡改这里。
public static partial class Config
{
    // 世界：海岸线在 z=0，沙滩台地 z<0，码头沿 +Z 伸入海。地面/码头顶面同高，角色无需上下台阶。
    public const float GroundY = 1f;
    public const float SeaY = 0f;
    public const float SeabedY = -3f;
    public static readonly Vector2 SeaSize = new Vector2(80f, 60f);   // x 宽, z 深（从 z=0 起）

    public const float DockLength = 24f;
    public const float DockWidth = 3f;
    public const float PlankDepth = 0.5f;   // 每块木板沿 Z 的长度；码头可按块纵向拼接
    public const float PlankGap = 0.05f;
    public static int PlankCount => Mathf.RoundToInt(DockLength / PlankDepth);
    public static readonly Vector3 DockMid = new Vector3(0f, GroundY, DockLength * 0.5f);
    public static readonly Vector3 FishSpot = new Vector3(0f, GroundY, DockLength - 1.5f);
    public const float FishSpotRadius = 2.5f;
    public const float CastDistance = 4f;   // 浮漂落点：钓点前方
    public const float CastSpread = 1.2f;   // 两人浮漂沿码头横向左右各偏多少（P1 -x，P2 +x，见 Tackle.CastTarget），并排钓一眼分得清（loop-8）

    // 沙滩可走范围
    public static readonly Rect Beach = new Rect(-25f, -18f, 50f, 18f);   // x, z, 宽, 深
    public static readonly Vector3 PlayerStart = new Vector3(0f, GroundY, -3f);
    public const float PlayerSpeed = 2.4f;   // loop-8：1.8 → 2.4；步频由 PlayerController 按实际速度 / WalkAnimSpeed 缩放，不滑步
    public const float WalkAnimSpeed = 1.72f;   // AN_walk 原速对应的移动速度（blender/rig.py：一个循环 1.44 m / 20 帧）
    public const float RodSegment = 0.55f;      // 鱼竿三节，每节长度

    // 道具位置
    public static readonly Vector3 BoatPos = new Vector3(DockWidth * 0.5f + 2.5f, SeaY - 0.2f, 14f);   // 吃水 0.2 m
    public static readonly Vector3 CabinPos = new Vector3(-8f, GroundY, -3.5f);
    public static readonly float[] LanternZ = { 2f, 10f, 18f };   // 码头左舷灯笼
    public const int WebLamps = 2;   // 网页端最多亮几盏点光源（灯笼 + 小屋窗）
    public static readonly Color LampColor = new Color(1f, 0.72f, 0.42f);
    public static readonly Vector3[] RockPos = { new Vector3(-8f, SeaY, 2f), new Vector3(9f, SeaY, 3f), new Vector3(-18f, SeaY, 6f) };
    public static readonly float[] RockScale = { 1.6f, 1f, 2.4f };
    public static readonly Vector3[] CratePos = { new Vector3(-1f, GroundY, 6f), new Vector3(1f, GroundY, 18f) };
    public static readonly Vector3[] BarrelPos = { new Vector3(-1.1f, GroundY, 7.2f), new Vector3(1.1f, GroundY, 20f) };
    public static readonly Vector3 LadderPos = new Vector3(-DockWidth * 0.5f - 0.1f, SeaY, DockLength - 0.6f);

    // 相机
    public const float CamPitch = 60f;
    public const float CamFov = 35f;
    public const float CamDistance = 18f;
    public const float CamSmooth = 0.25f;
    public static readonly Vector3 ShotFocus = new Vector3(-3f, GroundY, 1f);   // 时段截图的固定机位焦点

    // 水面波形：Water.shader 与 Floater 共用同一公式
    public const float WaveAmp = 0.08f, WaveLen = 6f, WaveSpeed = 1.2f;
    public static readonly Rect CamBounds = new Rect(-25f, -15f, 50f, 55f);

    // 自然
    public static readonly Vector3 RiverStart = new Vector3(15f, GroundY, -18f);
    public const float RiverWidth = 3f;
    public static readonly Vector3 CliffPos = new Vector3(15f, GroundY, -22f);   // 斜崖中心；坡脚 z = -22 + CliffHeight/2 = RiverStart.z
    public const float CliffHeight = 8f;
    public const int TreeCount = 18;
    public const int TreeSeed = 7;

    // 色卡（占位）
    public static readonly Color Sea = new Color(0.25f, 0.80f, 0.78f, 0.55f);
    public static readonly Color Sand = new Color(0.93f, 0.84f, 0.63f);
    public static readonly Color Seabed = new Color(0.86f, 0.78f, 0.58f);
    public static readonly Color Wood = new Color(0.62f, 0.43f, 0.27f);
    public static readonly Color WoodDark = new Color(0.45f, 0.30f, 0.19f);
    public static readonly Color Coral = new Color(0.96f, 0.50f, 0.45f);
    public static readonly Color Weed = new Color(0.35f, 0.62f, 0.33f);
    public static readonly Color Leaf = new Color(0.36f, 0.66f, 0.38f);
    public static readonly Color Trunk = new Color(0.50f, 0.35f, 0.22f);
    public static readonly Color Cliff = new Color(0.58f, 0.55f, 0.50f);
    public static readonly Color River = new Color(0.40f, 0.78f, 0.90f, 0.7f);
    public static readonly Color Fish = new Color(0.98f, 0.72f, 0.30f);
    public static readonly Color Bobber = new Color(0.92f, 0.22f, 0.20f);

    // 钓鱼
    public const float WaitMin = 2f, WaitMax = 6f, BiteWindow = 1.5f, CastTime = 0.6f, ResultTime = 1.2f;
    public const float StartTension = 0.3f, PullTension = 0.6f, RelaxTension = 0.4f, PullProgress = 0.5f, SlipProgress = 0.05f;
    public static readonly string[] FishNames = { "sardine", "snapper", "puffer" };
    public const string LanternZh = "灯笼鱼";   // loop-17 第 4 种鱼：只在记账外壳里出现（FishingStateMachine.Credit；FishNames / PickFish 不动），字条上说的中文名
    public static readonly string[] FishNamesZh = { "沙丁鱼", "鲷鱼", "河豚" };   // loop-16 和 FishNames 一一对应，只用于显示（钓上那一刻字条上说的那句）；角上那行英文照旧用 FishNames
    public static readonly Bait[] Baits =
    {
        new Bait("bread", 1.0f, new[] { 0.7f, 0.2f, 0.1f }),
        new Bait("worm", 0.7f, new[] { 0.3f, 0.5f, 0.2f }),
        new Bait("corn", 0.85f, new[] { 0.4f, 0.2f, 0.4f }),
    };

    // 时段：晨/午/昏/夜，一天 DayLength 秒。太阳(仰角,方位) + 环境光天空/地平线/地面三色 + 水色 + 夜灯亮度
    public const float DayLength = 240f;
    public static readonly string[] TimeNames = { "morning", "noon", "dusk", "night" };
    public static readonly TimeKey[] Times =
    {
        new TimeKey(new Color(1.00f, 0.72f, 0.48f), 0.9f, 14f, -70f, new Color(0.42f, 0.48f, 0.60f), new Color(0.60f, 0.47f, 0.37f), new Color(0.30f, 0.25f, 0.22f), new Color(0.22f, 0.64f, 0.70f, 0.55f), 0f),
        new TimeKey(new Color(1.00f, 0.95f, 0.86f), 0.72f, 62f, -25f, new Color(0.40f, 0.48f, 0.58f), new Color(0.46f, 0.46f, 0.42f), new Color(0.30f, 0.27f, 0.22f), new Color(0.15f, 0.68f, 0.70f, 0.55f), 0f),
        new TimeKey(new Color(1.00f, 0.48f, 0.30f), 0.95f, 12f, 70f, new Color(0.52f, 0.44f, 0.62f), new Color(0.92f, 0.55f, 0.40f), new Color(0.35f, 0.25f, 0.25f), new Color(0.30f, 0.44f, 0.58f, 0.6f), 0.5f),
        new TimeKey(new Color(0.50f, 0.62f, 0.95f), 0.35f, 38f, 30f, new Color(0.10f, 0.14f, 0.28f), new Color(0.14f, 0.18f, 0.32f), new Color(0.06f, 0.06f, 0.10f), new Color(0.07f, 0.20f, 0.34f, 0.7f), 1f),
    };

    public static bool OnDock(Vector3 p) => Mathf.Abs(p.x) <= DockWidth * 0.5f - 0.3f && p.z >= -0.5f && p.z <= DockLength - 0.3f;
    public static bool OnBeach(Vector3 p) => Beach.Contains(new Vector2(p.x, p.z));
    public static bool IsWalkable(Vector3 p) => OnDock(p) || OnBeach(p);
    public static bool InFishSpot(Vector3 p) => new Vector2(p.x - FishSpot.x, p.z - FishSpot.z).magnitude <= FishSpotRadius;
}

public readonly struct Bait
{
    public readonly string Name; public readonly float WaitFactor; public readonly float[] Weights;
    public Bait(string name, float waitFactor, float[] weights) { Name = name; WaitFactor = waitFactor; Weights = weights; }
}

public readonly struct TimeKey
{
    public readonly Color Light; public readonly float Intensity, Elevation, Yaw; public readonly Color Sky, Equator, Ground, Water; public readonly float Glow;
    public TimeKey(Color light, float intensity, float elevation, float yaw, Color sky, Color equator, Color ground, Color water, float glow)
    { Light = light; Intensity = intensity; Elevation = elevation; Yaw = yaw; Sky = sky; Equator = equator; Ground = ground; Water = water; Glow = glow; }

    public static TimeKey Lerp(TimeKey a, TimeKey b, float k) => new TimeKey(Color.Lerp(a.Light, b.Light, k), Mathf.Lerp(a.Intensity, b.Intensity, k),
        Mathf.Lerp(a.Elevation, b.Elevation, k), Mathf.Lerp(a.Yaw, b.Yaw, k), Color.Lerp(a.Sky, b.Sky, k), Color.Lerp(a.Equator, b.Equator, k),
        Color.Lerp(a.Ground, b.Ground, k), Color.Lerp(a.Water, b.Water, k), Mathf.Lerp(a.Glow, b.Glow, k));
}
