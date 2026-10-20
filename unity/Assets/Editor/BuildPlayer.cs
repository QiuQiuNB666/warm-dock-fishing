using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// -executeMethod BuildPlayer.Windows / BuildPlayer.Web；失败退出码 1。
public static class BuildPlayer
{
    const string Desktop = "Build/Desktop/Warm.exe";
    const string WebDir = "Build/Web";
    const string FillCss = "<style>html,body{margin:0;height:100%;overflow:hidden}#unity-container,#unity-canvas{position:fixed;inset:0;width:100%!important;height:100%!important}</style>";
    const string Dpr = "config.devicePixelRatio = Math.min(window.devicePixelRatio, 2); ";

    public static void Windows() => Run(BuildTarget.StandaloneWindows64, Desktop, null);

    public static void Web() => Run(BuildTarget.WebGL, WebDir, () => PatchIndex(Path.Combine(WebDir, "index.html")));

    static void Run(BuildTarget target, string path, System.Action after)
    {
        var opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        bool ok = report.summary.result == BuildResult.Succeeded;
        try { if (ok) after?.Invoke(); }
        catch (System.Exception e) { Debug.LogError(e); ok = false; }
        Debug.Log(ok ? $"BUILD {target} OK {report.summary.totalSize / (1024 * 1024)} MB" : $"BUILD {target} FAIL {report.summary.result}");
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    // 只注入两处：canvas 满窗 CSS、devicePixelRatio 上限 2；不建自定义模板。
    public static string Inject(string html)
    {
        int head = html.IndexOf("</head>", System.StringComparison.OrdinalIgnoreCase);
        int call = html.IndexOf("createUnityInstance(", System.StringComparison.Ordinal);
        if (head < 0 || call < 0) throw new System.Exception("index.html: </head> or createUnityInstance( not found");
        html = html.Insert(call, Dpr);
        return html.Insert(head, FillCss);
    }

    static void PatchIndex(string file) => File.WriteAllText(file, Inject(File.ReadAllText(file)));
}
