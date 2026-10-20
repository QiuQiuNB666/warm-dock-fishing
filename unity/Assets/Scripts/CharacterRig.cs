using UnityEngine;

// 在 Animator 之后（LateUpdate）叠加程序动画：抛竿/收线的右臂与鱼竿；无骨骼时退回整身起伏/摇摆。
public class CharacterRig : MonoBehaviour
{
    public FishingStateMachine fishing;
    public PlayerController player;
    public Transform rightArm;   // 上臂骨骼，BuildScene 按名字找；找不到为 null
    public Transform rod;
    public Transform body;       // 静态角色退路：整体起伏的网格根

    Quaternion rodBase;
    Vector3 bodyBase;

    void Start()
    {
        if (rod != null) rodBase = rod.localRotation;
        if (body != null) bodyBase = body.localPosition;
    }

    public static float RodPitch(FishState s, float timer, float tension)
    {
        switch (s)
        {
            case FishState.Cast: return Mathf.Lerp(-70f, 30f, Mathf.Clamp01(timer / Config.CastTime));   // 后扬再甩出
            case FishState.Wait: return 30f;
            case FishState.Bite: return 30f + Mathf.Sin(timer * 40f) * 4f;
            case FishState.Reel: return Mathf.Lerp(10f, -30f, tension) + Mathf.Sin(timer * 12f) * 3f;
            case FishState.Caught: return -45f;
            default: return 0f;
        }
    }

    void LateUpdate()
    {
        var logic = fishing != null ? fishing.Logic : null;
        float pitch = logic != null ? RodPitch(logic.State, logic.Timer, logic.Tension) : 0f;
        if (rod != null) rod.localRotation = rodBase * Quaternion.Euler(pitch, 0f, 0f);
        if (rightArm != null) rightArm.localRotation *= Quaternion.Euler(pitch * 0.6f, 0f, 0f);
        if (body != null)
        {
            float moving = player != null ? player.Speed01 : 0f;
            float t = Time.time;
            body.localPosition = bodyBase + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 8f)) * 0.06f * moving + Mathf.Sin(t * 1.5f) * 0.01f);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 8f) * 4f * moving);
        }
    }
}
