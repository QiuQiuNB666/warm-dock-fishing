using UnityEngine;

// 固定俯角 60°（非 90°）平滑跟随；焦点限制在场景边界内。
// 双人：焦点取两人中点，按两人间距把距离拉到 CamDistance × zoom（zoom ∈ [1, CamMaxZoomOut]）。
// 拉到顶还框不住（比如一人在小屋、一人在钓点）就照样看中点，屏外那人在屏幕边缘画一个帽色点。
public class TopDownCamera : MonoBehaviour
{
    public Transform target, target2;
    Vector3 focus, vel;
    float zoom = 1f, zoomVel;
    Camera cam;
    Texture2D dot;

    public static Vector3 Offset => OffsetAt(1f);
    public static Vector3 OffsetAt(float zoom) => Quaternion.Euler(Config.CamPitch, 0f, 0f) * Vector3.back * (Config.CamDistance * zoom);

    public static Vector3 ClampFocus(Vector3 f)
    {
        var b = Config.CamBounds;
        return new Vector3(Mathf.Clamp(f.x, b.xMin, b.xMax), f.y, Mathf.Clamp(f.z, b.yMin, b.yMax));
    }

    public static void Place(Transform cam, Vector3 focus) => Place(cam, focus, 1f);

    public static void Place(Transform cam, Vector3 focus, float zoom)
    {
        cam.position = ClampFocus(focus) + OffsetAt(zoom);
        cam.rotation = Quaternion.Euler(Config.CamPitch, 0f, 0f);
    }

    // 两人都留在画面里（含边距）要拉远多少倍，夹到 [1, CamMaxZoomOut]。aspect = 宽 / 高。
    // zoom=1 时焦点处可见半高 h = CamDistance·tan(FOV/2)，半宽 h·aspect，地面上纵深方向的半长 h / sin(俯角)。
    public static float ZoomFor(Vector3 a, Vector3 b, float aspect)
    {
        float h = Config.CamDistance * Mathf.Tan(Config.CamFov * 0.5f * Mathf.Deg2Rad) * Config.CamFrameMargin;
        float needX = Mathf.Abs(a.x - b.x) * 0.5f / (h * aspect);
        float needZ = Mathf.Abs(a.z - b.z) * 0.5f * Mathf.Sin(Config.CamPitch * Mathf.Deg2Rad) / h;
        return Mathf.Clamp(Mathf.Max(needX, needZ), 1f, Config.CamMaxZoomOut);
    }

    bool Two => target2 != null && target2.gameObject.activeInHierarchy;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = Config.CamFov;
        focus = ClampFocus(target != null ? target.position : Config.DockMid);
        dot = Disc(32);
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = Two ? (target.position + target2.position) * 0.5f : target.position;
        float want = Two && cam != null ? ZoomFor(target.position, target2.position, cam.aspect) : 1f;
        focus = Vector3.SmoothDamp(focus, ClampFocus(goal), ref vel, Config.CamSmooth);
        zoom = Mathf.SmoothDamp(zoom, want, ref zoomVel, Config.CamSmooth);
        Place(transform, focus, zoom);
    }

    // 屏外指示：双人时谁跑出画面，就在屏幕边缘朝他的方向画一个帽色点（不是库存面板）
    void OnGUI()
    {
        if (!Two || cam == null) return;
        Edge(target, Config.HatDot[0]);
        Edge(target2, Config.HatDot[1]);
    }

    void Edge(Transform t, Color c)
    {
        Vector3 v = cam.WorldToViewportPoint(t.position + Vector3.up * 1.8f);   // 帽子高度
        if (v.z < 0f) { v.x = 1f - v.x; v.y = 1f - v.y; }   // 在相机背后：方向翻过来
        else if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) return;   // 在画面里，不画
        Vector2 d = new Vector2(v.x - 0.5f, v.y - 0.5f);
        float mx = 20f / Screen.width, my = 20f / Screen.height;
        float k = Mathf.Min((0.5f - mx) / Mathf.Max(Mathf.Abs(d.x), 1e-4f), (0.5f - my) / Mathf.Max(Mathf.Abs(d.y), 1e-4f));
        float px = (0.5f + d.x * k) * Screen.width, py = (0.5f - d.y * k) * Screen.height;
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(px - 12f, py - 12f, 24f, 24f), dot);   // 深色描边，沙地上也看得清
        GUI.color = c;
        GUI.DrawTexture(new Rect(px - 9f, py - 9f, 18f, 18f), dot);
        GUI.color = old;
    }

    static Texture2D Disc(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n * 0.5f - new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f).magnitude)));
        tex.Apply();
        return tex;
    }
}
