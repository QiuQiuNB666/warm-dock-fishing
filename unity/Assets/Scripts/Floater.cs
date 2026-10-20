using UnityEngine;

// 船和浮漂共用：绕锚点上下浮动 + 轻微摇晃；dip=1 时整体下沉（咬钩）。
public class Floater : MonoBehaviour
{
    public float amplitude = 0.08f;
    public float speed = 1.6f;
    public float roll = 3f;
    public float dipDepth = 0.25f;
    [Range(0, 1)] public float dip;

    Vector3 anchor;
    Quaternion baseRot;
    float phase;

    void Awake()
    {
        anchor = transform.position;
        baseRot = transform.rotation;
        phase = (anchor.x * 0.37f + anchor.z * 0.11f) % 6.28f;
    }

    public void SetAnchor(Vector3 p) { anchor = p; }

    void Update()
    {
        float s = Mathf.Sin(Time.time * speed + phase);
        transform.position = anchor + Vector3.up * (s * amplitude - dip * dipDepth);
        transform.rotation = baseRot * Quaternion.Euler(s * roll, 0f, Mathf.Cos(Time.time * speed * 0.7f + phase) * roll);
    }
}
