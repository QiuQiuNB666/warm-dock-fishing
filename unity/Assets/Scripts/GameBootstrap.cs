using System.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameBootstrap : MonoBehaviour
{
    static bool announced;
    bool hud;
    float fps;
    string frameDir;   // -frames <dir>：演示录像，按 30 fps 游戏时间逐帧截图（不受别的窗口遮挡）
    int frame;

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.targetFrameRate = -1;   // 交给浏览器
        var v = FindAnyObjectByType<Volume>();
        if (v != null && v.profile.TryGet(out Bloom bloom)) bloom.active = false;   // Medium 档：关 Bloom 保帧率
#else
        foreach (var name in new[] { "ShadowDisc", "ShadowDisc2" })
        {
            var disc = GameObject.Find(name);
            if (disc != null) disc.SetActive(false);   // 桌面有实时软阴影，圆片只给网页
        }
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
#endif
    }

    public static string Arg(string name)
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, name);
        return i < 0 ? null : i + 1 < a.Length ? a[i + 1] : "";
    }

    IEnumerator Start()
    {
        yield return null;   // 等所有 Start 跑完一帧
        if (announced) yield break;
        announced = true;
        var pm = FindAnyObjectByType<PlayerManager>();
        if (pm != null) pm.Bind();   // 保证两名玩家已绑好再宣布就绪（Bind 幂等）
        Debug.Log("SCENE-READY");
        string dir = Arg("-shots");
        if (!string.IsNullOrEmpty(dir)) StartCoroutine(Shots(dir));
        frameDir = Arg("-frames");
        if (!string.IsNullOrEmpty(frameDir)) Time.captureFramerate = 30;
    }

    // -shots <dir>：固定机位，四个时段各截一张 light_<时段>.png 后退出。
    IEnumerator Shots(string dir)
    {
        var tod = FindAnyObjectByType<TimeOfDay>();
        tod.frozen = true;
        var cam = Camera.main;
        cam.GetComponent<TopDownCamera>().enabled = false;
        TopDownCamera.Place(cam.transform, Config.ShotFocus);
        for (int i = 0; i < Config.TimeNames.Length; i++)
        {
            tod.t = i / (float)Config.TimeNames.Length;
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"light_{Config.TimeNames[i]}.png"));
            yield return new WaitForSeconds(0.5f);
        }
        Debug.Log("SHOTS-DONE");
        Application.Quit();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) hud = !hud;
        if (!string.IsNullOrEmpty(frameDir) && Time.captureFramerate > 0)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(frameDir, $"f{frame:0000}.png"));
            if (++frame >= 900) Application.Quit();   // 30 s
        }
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.1f);
    }

    void OnGUI()
    {
        if (!hud || Pause.On) return;   // 暂停时让开
        long mb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        GUI.Label(new Rect(Screen.width - 220, 10, 210, 44),
                  $"FPS {fps:0}\nMem {mb} MB  Q:{QualitySettings.names[QualitySettings.GetQualityLevel()]}");
    }
}
