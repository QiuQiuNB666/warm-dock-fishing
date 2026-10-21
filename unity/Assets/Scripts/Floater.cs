using UnityEngine;

// 船和浮漂共用：跟着水面波形（与 Water.shader 同一公式）起伏 + 按坡度摇晃；dip=1 时整体下沉（咬钩）。
public class Floater : MonoBehaviour
{
    public float roll = 1f;       // 坡度 → 倾角的倍数
    public float dipDepth = 0.25f;
    [Range(0, 1)] public float dip;

    Vector3 anchor;
    Quaternion baseRot;

    void Awake()
    {
        anchor = transform.position;
        baseRot = transform.rotation;
    }

    public void SetAnchor(Vector3 p) { anchor = p; }

    // 与 Water.shader 顶点函数逐项一致；t 用 Time.timeSinceLevelLoad（= shader 的 _Time.y）
    public static float Wave(float x, float z, float t)
    {
        float k = 2f * Mathf.PI / Config.WaveLen, s = t * Config.WaveSpeed;
        return (Mathf.Sin(x * k + s) * 0.6f + Mathf.Sin(z * k * 0.7f + s * 1.3f) * 0.4f) * Config.WaveAmp;
    }

    void Update()
    {
        float t = Time.timeSinceLevelLoad;
        float h = Wave(anchor.x, anchor.z, t);
        float sx = (Wave(anchor.x + 0.5f, anchor.z, t) - h) * 2f, sz = (Wave(anchor.x, anchor.z + 0.5f, t) - h) * 2f;
        transform.position = anchor + Vector3.up * (h - dip * dipDepth);
        transform.rotation = Quaternion.Euler(sz * Mathf.Rad2Deg * roll, 0f, -sx * Mathf.Rad2Deg * roll) * baseRot;
    }
}
