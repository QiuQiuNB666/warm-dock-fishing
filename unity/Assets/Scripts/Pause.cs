using UnityEngine;

// Esc 暂停：标志为真时不读移动和钓鱼，并冻结 TimeOfDay（不靠 timeScale）。全屏暖纸底菜单：继续 / 保存并退出。
// 保存后停在暂停里，显示「已保存，可关闭页面」，不调 Application.Quit（网页端关不了标签页，桌面端也统一不退）。
public class Pause : MonoBehaviour
{
    public static bool On { get; private set; }
    static readonly Color Paper = new Color(0.97f, 0.92f, 0.82f, 0.55f);   // 暖纸色，半透明：菜单后面的场景（放下的砖）还看得清
    static readonly Color Ink = new Color(0.36f, 0.24f, 0.15f);
    public Font font;   // 网页端没有系统字体回退：中文用 Assets/Fonts 的思源黑体子集（WarmCJK）
    bool saved, wasFrozen;
    TimeOfDay tod;
    GUIStyle button, note;

    void Start() { tod = FindAnyObjectByType<TimeOfDay>(); }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Set(!On);
    }

    void OnDestroy() { if (On) Set(false); }

    void Set(bool on)
    {
        On = on;
        saved = false;
        if (tod == null) return;
        if (on) { wasFrozen = tod.frozen; tod.frozen = true; }
        else tod.frozen = wasFrozen;
    }

    void OnGUI()
    {
        if (!On) return;
        GUI.depth = -10;
        button ??= new GUIStyle(GUI.skin.button) { fontSize = 26, font = font };
        note ??= new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink }, font = font };
        var old = GUI.color;
        GUI.color = Paper;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
        float w = 260f, h = 56f, x = (Screen.width - w) * 0.5f, y = Screen.height * 0.5f;
        if (GUI.Button(new Rect(x, y - h - 14f, w, h), "继续", button)) Set(false);
        if (GUI.Button(new Rect(x, y, w, h), "保存并退出", button)) { PierSave.Save(); saved = true; }
        if (saved) GUI.Label(new Rect(0f, y + h + 24f, Screen.width, 44f), "已保存，可关闭页面", note);
    }
}
