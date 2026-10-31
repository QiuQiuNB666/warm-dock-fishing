using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// -script <plan.json>：游戏自己按计划注入按键、自己截图，锁屏、窗口不在前台也能实玩（loop-2）。
// 计划和 play_input.ps1 同一套词：wait / down / up / tap / until（press / every / timeout）/ shot，另加 save（PierSave.Save）、quit；
// click 不支持，打 SCRIPT-SKIP click 跳过。键码是 Windows 虚拟键，由 Key() 映射成 KeyCode。
// 脚本模式下 InputManager2P.Held/Pressed 只认脚本自己的状态，真键盘全部忽略
// （Esc 暂停、T 跳时段、F3 是各自直接读 Input，不走 InputManager2P，脚本也发不了）。
// 日志：SCRIPT-READY、每步 SCRIPT-STEP i、SCRIPT-SHOT <路径>、SCRIPT-DONE 后退出；until 超时打 SCRIPT-ABORT until <正则>，退出码 5。
// 网页端拿不到命令行，什么都不做。
[DefaultExecutionOrder(-1000)]   // 每帧最先跑：帧首清掉上一帧的「按下」再推进计划，所以 Pressed 只在按下的那一帧为真
public class ScriptedPlay : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public int wait, tap, press, every, timeout;
        public int[] down, up;
        public string until, shot;
        public bool save, quit;
        public float[] click;
    }

    [Serializable] class Plan { public Step[] s; }

    const float TapSec = 0.08f;   // 同 play_input.ps1：点一下按住 80 ms
    readonly HashSet<KeyCode> held = new HashSet<KeyCode>(), pressed = new HashSet<KeyCode>();
    Step[] steps;
    IEnumerator<float> run;   // 每次 yield 的是要等的秒数（0 = 下一帧）
    float resumeAt;
    bool ready, seen;
    Regex until;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        string file = GameBootstrap.Arg("-script");
        if (string.IsNullOrEmpty(file)) return;
        Application.runInBackground = true;
        new GameObject("ScriptedPlay").AddComponent<ScriptedPlay>().Load(file);
#endif
    }

    void Load(string file)
    {
        Application.logMessageReceived += OnLog;
        InputManager2P.Held = held.Contains;
        InputManager2P.Pressed = pressed.Contains;
        try { steps = Parse(System.IO.File.ReadAllText(file)); }
        catch (Exception e) { Abort("plan " + e.Message); }
    }

    // 计划 = 步骤数组；没写的字段取默认值（0 / null / false），空串当空计划
    public static Step[] Parse(string json) =>
        string.IsNullOrWhiteSpace(json) ? new Step[0] : JsonUtility.FromJson<Plan>("{\"s\":" + json + "}").s ?? new Step[0];

    // Windows 虚拟键 → KeyCode：字母、主键盘数字、小键盘 0–9、方向键、空格、Esc；别的是 None
    public static KeyCode Key(int vk) => vk switch
    {
        >= 65 and <= 90 => KeyCode.A + (vk - 65),
        >= 48 and <= 57 => KeyCode.Alpha0 + (vk - 48),
        >= 96 and <= 105 => KeyCode.Keypad0 + (vk - 96),
        37 => KeyCode.LeftArrow, 38 => KeyCode.UpArrow, 39 => KeyCode.RightArrow, 40 => KeyCode.DownArrow,
        32 => KeyCode.Space, 27 => KeyCode.Escape,
        _ => KeyCode.None,
    };

    void OnLog(string msg, string trace, LogType type)
    {
        if (msg == "SCENE-READY") ready = true;
        if (until != null && until.IsMatch(msg)) seen = true;
    }

    void Update()
    {
        pressed.Clear();
        if (!ready || steps == null || Time.unscaledTime < resumeAt) return;   // 等 SCENE-READY（读完档、两人站好）再开始，同 play_input.ps1
        if (run == null) { Debug.Log("SCRIPT-READY"); run = Run(); }
        if (run.MoveNext()) resumeAt = Time.unscaledTime + run.Current;
        else steps = null;
    }

    IEnumerator<float> Run()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i] ?? new Step();
            Debug.Log("SCRIPT-STEP " + i);
            foreach (int k in s.down ?? new int[0]) Down(Key(k));
            foreach (int k in s.up ?? new int[0]) held.Remove(Key(k));
            if (s.tap != 0) foreach (float w in Tap(Key(s.tap))) yield return w;
            if (!string.IsNullOrEmpty(s.until))   // 每 every 毫秒点一下 press，直到日志里新出现匹配的行
            {
                seen = false; until = new Regex(s.until);
                float end = Time.unscaledTime + (s.timeout > 0 ? s.timeout : 60000) / 1000f;
                while (!seen)
                {
                    if (Time.unscaledTime > end) { Abort("until " + s.until); yield break; }
                    if (s.press != 0) foreach (float w in Tap(Key(s.press))) yield return w;
                    yield return Mathf.Max(s.every / 1000f - TapSec, 0f);
                }
                until = null;
            }
            if (s.wait > 0) yield return s.wait / 1000f;
            if (s.click != null && s.click.Length > 0) Debug.Log("SCRIPT-SKIP click");
            if (!string.IsNullOrEmpty(s.shot))
            {
                ScreenCapture.CaptureScreenshot(s.shot);   // 本帧末尾截，OnGUI 的字也在里面
                Debug.Log("SCRIPT-SHOT " + s.shot);
                yield return 0f;   // 截完至少等一帧再走下一步
            }
            if (s.save) PierSave.Save();
            if (s.quit) break;
        }
        Debug.Log("SCRIPT-DONE");
        Application.Quit();
    }

    void Down(KeyCode k) { if (held.Add(k)) pressed.Add(k); }   // 已经按着的键不再算一次按下

    IEnumerable<float> Tap(KeyCode k)
    {
        Down(k);
        yield return TapSec;
        held.Remove(k);
    }

    void Abort(string why)
    {
        Debug.Log("SCRIPT-ABORT " + why);
        Application.Quit(5);
    }
}
