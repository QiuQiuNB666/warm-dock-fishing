using System.Collections;
using UnityEngine;
using UnityEngine.Profiling;

public class GameBootstrap : MonoBehaviour
{
    static bool announced;
    bool hud;
    float fps;

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.targetFrameRate = -1;   // 交给浏览器
#else
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
#endif
    }

    IEnumerator Start()
    {
        yield return null;   // 等所有 Start 跑完一帧
        if (announced) yield break;
        announced = true;
        Debug.Log("SCENE-READY");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) hud = !hud;
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.1f);
    }

    void OnGUI()
    {
        if (!hud) return;
        long mb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        GUI.Label(new Rect(Screen.width - 220, 10, 210, 44),
                  $"FPS {fps:0}\nMem {mb} MB  Q:{QualitySettings.names[QualitySettings.GetQualityLevel()]}");
    }
}
