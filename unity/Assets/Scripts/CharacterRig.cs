using UnityEngine;

// FishingStateMachine 状态 → Animator 交叉淡入；LateUpdate 按张力弯鱼竿；无骨骼时退回整身起伏/摇摆。
public class CharacterRig : MonoBehaviour
{
    public FishingStateMachine fishing;
    public PlayerController player;
    public Animator animator;
    public Transform[] rodJoints;   // 鱼竿三节，从根到梢
    public Transform body;          // 静态角色退路：整体起伏的网格根

    static readonly string[] StateClip = { "idle", "cast", "wait", "bite", "reel", "catch", "lose" };   // 按 FishState 顺序
    static readonly float[] BendShare = { 0.2f, 0.3f, 0.5f };   // 越靠梢弯得越多
    FishState last;
    Vector3 bodyBase;

    public static string ClipFor(FishState s) => StateClip[(int)s];
    public static readonly string[] Clips = { "idle", "walk", "cast", "wait", "bite", "reel", "catch", "lose" };   // SK_character 的 AN_* 动作

    // 角色朝向：持竿姿势下鱼竿挂点在髋部前方（玩家 +Z）为正
    public static float Facing(Transform player, Transform hips, Transform mount) =>
        hips == null ? 1f : Vector3.Dot(mount.position - hips.position, player.forward);

    // 鱼竿总弯曲角（度）：收线时随张力加大并抖动
    public static float RodBend(FishState s, float timer, float tension)
    {
        switch (s)
        {
            case FishState.Bite: return 20f + Mathf.Sin(timer * 40f) * 6f;
            case FishState.Reel: return 15f + tension * 45f + Mathf.Sin(timer * 14f) * 4f;
            case FishState.Caught: return 30f;
            default: return 4f;
        }
    }

    void Start() { if (body != null) bodyBase = body.localPosition; }

    void Update()
    {
        var s = fishing.Logic.State;
        if (s == last) return;
        last = s;
        if (animator != null) animator.CrossFadeInFixedTime(ClipFor(s), 0.15f);
    }

    void LateUpdate()
    {
        var logic = fishing.Logic;
        float bend = RodBend(logic.State, logic.Timer, logic.Tension);
        if (rodJoints != null)
            for (int i = 0; i < rodJoints.Length && i < BendShare.Length; i++)
                rodJoints[i].localRotation = Quaternion.Euler(bend * BendShare[i], 0f, 0f);
        if (body != null)
        {
            float moving = player != null ? player.Speed01 : 0f;
            float t = Time.time;
            body.localPosition = bodyBase + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 8f)) * 0.06f * moving + Mathf.Sin(t * 1.5f) * 0.01f);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 8f) * 4f * moving);
        }
    }
}
