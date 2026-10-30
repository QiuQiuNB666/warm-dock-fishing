using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Animator animator;
    public FishingStateMachine fishing;
    public float Speed01 { get; private set; }

    static readonly int SpeedId = Animator.StringToHash("Speed"), WalkScaleId = Animator.StringToHash("WalkScale");
    CharacterController cc;
    Camera cam;
    int owner;   // 第几名玩家（PlayerContext.playerIndex），决定读哪套键

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cam = Camera.main;
        var ctx = GetComponent<PlayerContext>();
        owner = ctx != null ? ctx.playerIndex : 0;
        if (animator != null) animator.applyRootMotion = false;
    }

    void Update()
    {
        Vector3 dir = fishing != null && fishing.Busy ? Vector3.zero : ReadInput();
        Vector3 step = dir * Config.PlayerSpeed * Time.deltaTime;
        step = Constrain(transform.position, step);
        cc.Move(step + Vector3.down * 0.01f);   // 地面同高，只给一点贴地力
        Speed01 = step.sqrMagnitude > 1e-8f ? 1f : 0f;
        Vector3 face = Speed01 > 0f ? dir : FishingStateMachine.DemoDrives(this) && DemoArrived(transform.position) ? Vector3.forward : Vector3.zero;
        if (face != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 12f * Time.deltaTime);
        if (animator == null) return;
        animator.SetFloat(SpeedId, Speed01, 0.1f, Time.deltaTime);
        animator.SetFloat(WalkScaleId, step.magnitude / Mathf.Max(Time.deltaTime, 1e-4f) / Config.WalkAnimSpeed);   // 步频跟实际速度走，不滑步
    }

    public static bool DemoArrived(Vector3 p) => new Vector2(p.x - Config.FishSpot.x, p.z - Config.FishSpot.z).magnitude < 0.3f;

    Vector3 ReadInput()
    {
        if (FishingStateMachine.DemoDrives(this))
        {
            Vector3 go = Config.FishSpot - transform.position;
            go.y = 0f;
            return DemoArrived(transform.position) ? Vector3.zero : go.normalized;
        }
        var key = InputManager2P.Move(owner);   // P1 WASD，P2 方向键，对角线已归一化
        if (key != Vector3.zero) return key;
        if (!InputManager2P.PointerAllowed || owner != 0) return Vector3.zero;   // 双人时关掉鼠标/触摸移动
        bool pressed = Input.touchCount > 0 || Input.GetMouseButton(0);
        if (!pressed || cam == null) return Vector3.zero;
        if (Config.InFishSpot(transform.position) && (Input.GetMouseButtonDown(0) || TouchBegan())) return Vector3.zero;   // 单击留给抛竿
        Vector2 screen = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        var ray = cam.ScreenPointToRay(screen);
        var ground = new Plane(Vector3.up, new Vector3(0f, Config.GroundY, 0f));
        if (!ground.Raycast(ray, out float d)) return Vector3.zero;
        Vector3 to = ray.GetPoint(d) - transform.position;
        to.y = 0f;
        return to.sqrMagnitude > 0.04f ? to.normalized : Vector3.zero;
    }

    static bool TouchBegan() => Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;

    // 码头与沙滩以外不可走：整步不行就分轴滑动。
    public static Vector3 Constrain(Vector3 pos, Vector3 step)
    {
        if (Config.IsWalkable(pos + step)) return step;
        var sx = new Vector3(step.x, 0f, 0f);
        if (Config.IsWalkable(pos + sx)) return sx;
        var sz = new Vector3(0f, 0f, step.z);
        if (Config.IsWalkable(pos + sz)) return sz;
        return Vector3.zero;
    }
}
