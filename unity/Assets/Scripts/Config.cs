using UnityEngine;

// 场景搭建、玩法、测试共用的唯一一份常量。颜色是占位色卡，Phase 1 读参考图后按 decisions.md 色卡改这里。
public static class Config
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

    // 沙滩可走范围
    public static readonly Rect Beach = new Rect(-25f, -18f, 50f, 18f);   // x, z, 宽, 深
    public static readonly Vector3 PlayerStart = new Vector3(0f, GroundY, -3f);
    public const float PlayerSpeed = 3.5f;

    // 道具位置
    public static readonly Vector3 BoatPos = new Vector3(DockWidth * 0.5f + 2.5f, SeaY, 14f);
    public static readonly Vector3 CabinPos = new Vector3(-12f, GroundY, -10f);
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
    public static readonly Bait[] Baits =
    {
        new Bait("bread", 1.0f, new[] { 0.7f, 0.2f, 0.1f }),
        new Bait("worm", 0.7f, new[] { 0.3f, 0.5f, 0.2f }),
        new Bait("corn", 0.85f, new[] { 0.4f, 0.2f, 0.4f }),
    };

    // 时段：晨/午/昏/夜，一天 DayLength 秒
    public const float DayLength = 240f;
    public static readonly TimeKey[] Times =
    {
        new TimeKey(new Color(1.00f, 0.85f, 0.70f), 0.9f, new Color(0.60f, 0.62f, 0.70f), new Color(0.35f, 0.78f, 0.80f, 0.55f)),
        new TimeKey(new Color(1.00f, 0.97f, 0.90f), 1.2f, new Color(0.70f, 0.75f, 0.80f), new Color(0.25f, 0.80f, 0.78f, 0.55f)),
        new TimeKey(new Color(1.00f, 0.60f, 0.40f), 0.8f, new Color(0.60f, 0.45f, 0.45f), new Color(0.45f, 0.55f, 0.65f, 0.6f)),
        new TimeKey(new Color(0.45f, 0.55f, 0.85f), 0.3f, new Color(0.15f, 0.18f, 0.30f), new Color(0.10f, 0.20f, 0.35f, 0.7f)),
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
    public readonly Color Light; public readonly float Intensity; public readonly Color Ambient; public readonly Color Water;
    public TimeKey(Color light, float intensity, Color ambient, Color water) { Light = light; Intensity = intensity; Ambient = ambient; Water = water; }
}
