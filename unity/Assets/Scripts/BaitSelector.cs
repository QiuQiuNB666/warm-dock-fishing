using UnityEngine;

public class BaitSelector : MonoBehaviour
{
    public FishingStateMachine fishing;
    public int Index { get; private set; }

    void Start() { Select(0); }

    void Update()
    {
        for (int i = 0; i < Config.Baits.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
    }

    public void Select(int i)
    {
        Index = Mathf.Clamp(i, 0, Config.Baits.Length - 1);
        if (fishing == null || fishing.Logic == null) return;
        fishing.Logic.WaitFactor = Config.Baits[Index].WaitFactor;
        fishing.Logic.Weights = Config.Baits[Index].Weights;
    }

    void OnGUI()
    {
        float y = Screen.height - 40;
        for (int i = 0; i < Config.Baits.Length; i++)
        {
            string label = (i == Index ? "> " : "") + $"{i + 1} {Config.Baits[i].Name}";
            if (GUI.Button(new Rect(10 + i * 110, y, 100, 30), label)) Select(i);
        }
    }
}
