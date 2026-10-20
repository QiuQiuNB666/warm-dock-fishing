using UnityEngine;

public class TimeOfDay : MonoBehaviour
{
    public Light sun;
    public Material water;
    public float t;   // 0..1 一天；0=晨 0.25=午 0.5=昏 0.75=夜

    static readonly int ColorId = Shader.PropertyToID("_Color");

    // 纯函数：在相邻两个时段之间线性插值，首尾循环。
    public static TimeKey Evaluate(float t01)
    {
        int n = Config.Times.Length;
        float f = Mathf.Repeat(t01, 1f) * n;
        int i = Mathf.FloorToInt(f) % n;
        float k = f - Mathf.Floor(f);
        TimeKey a = Config.Times[i], b = Config.Times[(i + 1) % n];
        return new TimeKey(Color.Lerp(a.Light, b.Light, k), Mathf.Lerp(a.Intensity, b.Intensity, k),
                           Color.Lerp(a.Ambient, b.Ambient, k), Color.Lerp(a.Water, b.Water, k));
    }

    public static float NextPhase(float t01)
    {
        int n = Config.Times.Length;
        return Mathf.Repeat((Mathf.Floor(Mathf.Repeat(t01, 1f) * n + 1e-4f) + 1f) / n, 1f);
    }

    void Update()
    {
        t = Input.GetKeyDown(KeyCode.T) ? NextPhase(t) : Mathf.Repeat(t + Time.deltaTime / Config.DayLength, 1f);
        Apply(Evaluate(t));
    }

    void Apply(TimeKey s)
    {
        if (sun != null)
        {
            sun.color = s.Light;
            sun.intensity = s.Intensity;
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(20f, 160f, Mathf.Repeat(t, 1f)), -30f, 0f);
        }
        RenderSettings.ambientLight = s.Ambient;
        if (water != null) water.SetColor(ColorId, s.Water);
    }
}
