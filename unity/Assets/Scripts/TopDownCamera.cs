using UnityEngine;

// 固定俯角 60°（非 90°）平滑跟随；焦点限制在场景边界内。
public class TopDownCamera : MonoBehaviour
{
    public Transform target;
    Vector3 focus, vel;

    public static Vector3 Offset => Quaternion.Euler(Config.CamPitch, 0f, 0f) * Vector3.back * Config.CamDistance;

    public static Vector3 ClampFocus(Vector3 f)
    {
        var b = Config.CamBounds;
        return new Vector3(Mathf.Clamp(f.x, b.xMin, b.xMax), f.y, Mathf.Clamp(f.z, b.yMin, b.yMax));
    }

    public static void Place(Transform cam, Vector3 focus)
    {
        cam.position = ClampFocus(focus) + Offset;
        cam.rotation = Quaternion.Euler(Config.CamPitch, 0f, 0f);
    }

    void Start()
    {
        var c = GetComponent<Camera>();
        if (c != null) c.fieldOfView = Config.CamFov;
        focus = ClampFocus(target != null ? target.position : Config.DockMid);
    }

    void LateUpdate()
    {
        if (target == null) return;
        focus = Vector3.SmoothDamp(focus, ClampFocus(target.position), ref vel, Config.CamSmooth);
        Place(transform, focus);
    }
}
