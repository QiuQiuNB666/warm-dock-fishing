using UnityEngine;

// 晨/午/昏/夜平滑插值：太阳角度与颜色、三色环境光、水面颜色、夜灯（灯笼/小屋窗的点光源 + 发光材质）。
public class TimeOfDay : MonoBehaviour
{
    public Light sun;
    public Material water;
    public Material glow;     // 灯笼/窗户共用的发光材质
    public Light[] lamps;
    public Renderer fire;     // loop-11 篝火：Start 给它的火苗换上自己的一份 M_glow（fireGlow，底色用色卡里的 LampColor），夜里单独压低发光倍数（Config.FireGlow），不再过曝成一团白星
    Material fireGlow;
    public float t;   // 0..1 一天；0=晨 0.25=午 0.5=昏 0.75=夜
    public bool frozen;   // 截图模式：不随时间走

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    // 纯函数：相邻两个时段之间 smoothstep 插值（端点与中点不变），首尾循环。
    public static TimeKey Evaluate(float t01)
    {
        int n = Config.Times.Length;
        float f = Mathf.Repeat(t01, 1f) * n;
        int i = Mathf.FloorToInt(f) % n;
        float k = f - Mathf.Floor(f);
        return TimeKey.Lerp(Config.Times[i], Config.Times[(i + 1) % n], k * k * (3f - 2f * k));
    }

    public static float NextPhase(float t01)
    {
        int n = Config.Times.Length;
        return Mathf.Repeat((Mathf.Floor(Mathf.Repeat(t01, 1f) * n + 1e-4f) + 1f) / n, 1f);
    }

    void Start()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        if (fire != null && glow != null) { fireGlow = new Material(glow) { color = Config.LampColor }; fire.sharedMaterials = System.Array.ConvertAll(fire.sharedMaterials, m => m == glow ? fireGlow : m); }
#if UNITY_WEBGL && !UNITY_EDITOR
        for (int i = Config.WebLamps; i < lamps.Length; i++) lamps[i].enabled = false;   // 网页端控制点光源数量
        System.Array.Resize(ref lamps, Mathf.Min(lamps.Length, Config.WebLamps));
#endif
    }

    void Update()
    {
        if (!frozen) t = Input.GetKeyDown(KeyCode.T) ? NextPhase(t) : Mathf.Repeat(t + Time.deltaTime / Config.DayLength, 1f);
        Apply(Evaluate(t));
    }

    public void Apply(TimeKey s)
    {
        if (sun != null)
        {
            sun.color = s.Light;
            sun.intensity = s.Intensity;
            sun.transform.rotation = Quaternion.Euler(s.Elevation, s.Yaw, 0f);
        }
        RenderSettings.ambientSkyColor = s.Sky;
        RenderSettings.ambientEquatorColor = s.Equator;
        RenderSettings.ambientGroundColor = s.Ground;
        if (water != null) water.SetColor(ColorId, s.Water);
        if (glow != null) glow.SetColor(EmissionId, Config.LampColor * (0.05f + s.Glow * 4f));
        if (fireGlow != null) fireGlow.SetColor(EmissionId, Config.LampColor * (0.05f + s.Glow * Config.FireGlow));   // 火苗：同色同底，只是夜里倍数小
        if (lamps != null) foreach (var l in lamps) if (l != null) l.intensity = s.Glow * 3f;
    }
}
