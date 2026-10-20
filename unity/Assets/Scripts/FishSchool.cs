using UnityEngine;

// 对象池：Awake 一次建好 count 条鱼，之后只改位置，不再 Instantiate/Destroy。
public class FishSchool : MonoBehaviour
{
    public int count = 8;
    public float radius = 4f;
    public float speed = 0.5f;
    public Material material;

    Transform[] fish;
    float[] offset;

    void Awake()
    {
        fish = new Transform[count];
        offset = new float[count];
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(go.GetComponent<Collider>());
            go.name = "Fish";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.18f, 0.35f, 0.18f);
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            fish[i] = go.transform;
            offset[i] = i * Mathf.PI * 2f / count;
        }
    }

    void Update()
    {
        float t = Time.time * speed;
        for (int i = 0; i < count; i++)
        {
            float a = t + offset[i];
            float r = radius * (0.7f + 0.3f * Mathf.Sin(a * 1.7f + i));
            var p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 2f + i) * 0.3f, Mathf.Sin(a) * r);
            var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            fish[i].localPosition = p;
            fish[i].localRotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
