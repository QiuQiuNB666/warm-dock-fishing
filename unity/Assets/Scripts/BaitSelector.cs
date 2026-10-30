using UnityEngine;

// 选饵：P1 用 1/2/3（挂在 Game 上），P2 用小键盘 7/8/9（挂在 Player2 上，单人模式随它一起关掉）；各改各的 FishingLogic。
public class BaitSelector : MonoBehaviour
{
    public FishingStateMachine fishing;
    public int owner;   // 第几名玩家
    public int Index { get; private set; }

    void Start() { Select(0); }

    void Update()
    {
        int i = InputManager2P.BaitDown(owner);
        if (i >= 0) Select(i);
    }

    public void Select(int i)
    {
        Index = Mathf.Clamp(i, 0, Config.Baits.Length - 1);
        if (fishing == null || fishing.Logic == null) return;
        fishing.Logic.WaitFactor = Config.Baits[Index].WaitFactor;
        fishing.Logic.Weights = Config.Baits[Index].Weights;
    }

    static string KeyName(KeyCode k) => k.ToString().Replace("Alpha", "").Replace("Keypad", "Num");

    void OnGUI()
    {
        var keys = KeyMap.Players[owner].Bait;
        float y = Screen.height - 40, x0 = owner == 0 ? 10f : Screen.width - 10f - Config.Baits.Length * 110f;   // P1 左下，P2 右下
        for (int i = 0; i < Config.Baits.Length; i++)
        {
            string label = (i == Index ? "> " : "") + $"{KeyName(keys[i])} {Config.Baits[i].Name}";
            if (GUI.Button(new Rect(x0 + i * 110, y, 100, 30), label)) Select(i);
        }
    }
}
