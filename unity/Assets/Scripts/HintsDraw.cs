using System.Linq;
using UnityEngine;

// 就近提示的画法（loop-15 从 Hints.cs 搬来，它满了）：OnGUI 画每人的字条（loop-16 左端一道帽子色竖条）、收线张力条、抱着时的「♥」；两张字条叠了左右分开（Apart），头顶 / 脚下的回差（Feet、Low）。同一个类的 partial（同 ScriptedPlay / ScriptedPlan），文字、读键、失败那句在 Hints.cs。
public partial class Hints
{
    GUIStyle style, ink;
    readonly float[] pathAt = new float[KeyMap.Players.Length];   // loop-7 修补：每人面前那格的连通判断（BrickPath.Open，BFS）每 PathEvery 秒算一次，免得每帧算
    readonly bool[] pathOk = new bool[KeyMap.Players.Length];
    readonly bool[] feet = new bool[KeyMap.Players.Length];   // 每人的字条现在画在脚下吗（loop-10 回差，见 Feet）

    // 两张字条叠在一起（开局两人在床边只隔 0.7 m）：左右分开，各让一半，中间留 HintGap（loop-11 从 4 px 加到 12 px，一眼分得出是两张）；没叠就不动
    public static void Apart(ref Rect a, ref Rect b)
    {
        if (!a.Overlaps(b)) return;
        bool aLeft = a.center.x <= b.center.x;
        float push = ((aLeft ? a.xMax - b.x : b.xMax - a.x) + Config.HintGap) * 0.5f * (aLeft ? 1f : -1f);
        a.x -= push; b.x += push;
    }

    // 字条画到脚下吗（loop-10 回差）：头顶位置 headY（字条上沿）高过 HintTop 就到脚下；已在脚下的要比 HintTop 再低 HintBack px 才回头顶，免得在边界上来回跳
    public static bool Feet(bool was, float headY) => headY < Config.HintTop + (was ? Config.HintBack : 0f);

    // 字条走脚下吗（loop-14 修补）：Feet 的回差，或南北站位挨着——北边 HugRange 内有人（Hug.Below）。抱着、抱完两人留在 HugGap 不动都算，靠南那人头顶的字条不压北边那人的帽子；左右并排、离得远都不算
    public static bool Low(bool feet, Vector3 me, Vector3[] all) => feet || Hug.Below(me, all.Where(o => Vector2.Distance(new Vector2(o.x, o.z), new Vector2(me.x, me.z)) <= Config.HugRange).ToArray());

    // loop-17 所有 Say 共用的这一处：刚说过一句（saying）就整句换掉提示，只有这人正咬钩时照常画「空格 提竿！」——另一人睡醒 / 天亮说的「天亮了」原会把它换掉 1.5 s（= BiteWindow），鱼必跑。
    // 收线时照换：张力条按 ten[] 另画，不受影响
    public static string Shown(string text, string said, bool saying, FishState fish) => saying && fish != FishState.Bite ? said : text;

    // loop-17 抱着时「♥」的世界位置：两人中点上方 HugHeart；南北站位（Hug.Below，含斜站 45° 内）x 改取北边那人.x + HugHeartSide，不压他的帽檐（loop-16 取中点 + 0.6 m，北边那人偏东时又压回去）
    public static Vector3 Heart(Vector3 a, Vector3 b)
    {
        var m = (a + b) * 0.5f + Vector3.up * Config.HugHeart;
        if (Hug.Below(a, new[] { b }) || Hug.Below(b, new[] { a })) m.x = (a.z > b.z ? a.x : b.x) + Config.HugHeartSide;
        return m;
    }

    void OnGUI()
    {
        if (Pause.On || players == null || cam == null) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = Config.HintFont, alignment = TextAnchor.MiddleCenter, font = font };   // 半透明底，同底部提示
        if (ink == null) { ink = new GUIStyle(style); ink.normal.background = ink.hover.background = null; }   // 同字号、同内边距，只写字不画底
        var rs = new Rect[players.players.Length]; var ts = new string[rs.Length]; var who = new int[rs.Length]; var ten = new float[rs.Length]; int n = 0; var at = players.Positions();
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            var f = p.fishing != null ? p.fishing.Logic : null;   // 钓鱼状态；CaughtCount > 0 = 这一局钓上过
            if (Stock.Current.brick > 0 && Time.time >= pathAt[p.playerIndex])   // 手里有砖才算（没砖 CanPlace 本来就不过）
            { pathAt[p.playerIndex] = Time.time + Config.PathEvery; pathOk[p.playerIndex] = BrickPath.Open(Stock.Current, Bricks.Front(p.transform.position, p.transform.forward), at); }
            bool walk = still[p.playerIndex] < Config.HintStill;   // 没站定：兜底当作做过、不画（免得后面那人的字条盖住前面那人）
            string text = Text(Stock.Current, p.playerIndex, p.transform.position, p.transform.forward, Trees.Pos, Trees.Hits, f?.State ?? FishState.Idle, walk || f != null && f.CaughtCount > 0, walk || Trees.TradedOnce[p.playerIndex], at, pathOk[p.playerIndex], walk || Trees.CookedOnce[p.playerIndex], !walk && !Hug.Once && Hug.Ready(players.players), tod == null || Sleep.Evening(tod.t));   // loop-14「H 抱抱」抱过一次（Hug.Once）就不再出；loop-16 黄昏前不列睡觉
            text = Shown(text, fail[p.playerIndex], Time.time < failUntil[p.playerIndex], f?.State ?? FishState.Idle);   // 刚说过一句（钓鱼失败、烤鱼、小灯塔、放不下砖、天亮了……）整句换掉 Text，这人咬钩时除外（loop-17 Shown）
            Vector3 v = cam.WorldToViewportPoint(p.transform.position + Vector3.up * 1.8f);   // 人在屏幕外不画（同 TopDownCamera 屏外指示点的判据）
            if (text == "" || v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
            Vector3 h = cam.WorldToScreenPoint(p.transform.position + Vector3.up * Config.HintHeight);
            Vector2 size = style.CalcSize(new GUIContent(text)) + Vector2.right * Config.HintStripe;   // loop-16 左端加宽一道帽子色竖条，字照旧居中在其余部分
            float y = Screen.height - h.y - size.y;   // 字的底边在头顶点上；这样会压到顶部「砖 n　木 n」就改画到脚下几像素（不往下夹去压帽子），回差见 Feet
            feet[p.playerIndex] = Feet(feet[p.playerIndex], y);
            if (Low(feet[p.playerIndex], p.transform.position, at)) y = Screen.height - cam.WorldToScreenPoint(p.transform.position).y + Config.HintFeetGap;   // loop-14 南北站位挨着（抱完不动也算，Low）：屏幕上靠下那人走脚下这一支，不压对方的帽子
            rs[n] = new Rect(h.x - size.x * 0.5f, y, size.x, size.y); who[n] = p.playerIndex; ten[n] = f != null && f.State == FishState.Reel ? f.Tension : -1f; ts[n++] = text;
        }
        if (n == 2) Apart(ref rs[0], ref rs[1]);
        for (int i = 0; i < n; i++)   // 夹在屏幕内，不出屏幕；上沿不高于 HintTop（顶部「砖 n　木 n」框的下沿）
        {
            var r = new Rect(Mathf.Clamp(rs[i].x, 0f, Screen.width - rs[i].width), Mathf.Clamp(rs[i].y, Config.HintTop, Screen.height - rs[i].height), rs[i].width, rs[i].height);
            GUI.Box(r, GUIContent.none, style); GUI.Box(r, GUIContent.none, style);   // 同一个半透明底叠两遍：形状同顶部「砖 n　木 n」，深一倍，浅色字才读得出
            GUI.DrawTexture(new Rect(r.x, r.y, Config.HintStripe, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Config.HatDot[who[i]], 0f, 0f);   // loop-16 认得出自己：左端一道帽子色实色竖条（同屏外指示点）
            var tr = new Rect(r.x + Config.HintStripe, r.y, r.width - Config.HintStripe, r.height);   // 字和张力条画在竖条右边
            ink.normal.textColor = ink.hover.textColor = Color.black;   // 先在右下偏 1 px 画一遍深色字当阴影
            GUI.Label(new Rect(tr.x + 1f, tr.y + 1f, tr.width, tr.height), ts[i], ink);
            ink.normal.textColor = ink.hover.textColor = Color.Lerp(Config.HatDot[who[i]], Color.white, Config.HintLighten);   // 再画字：帽子色（P1 草帽、P2 帽带，同屏外指示点）往白提亮
            GUI.Label(tr, ts[i], ink);
            if (ten[i] >= 0f) GUI.DrawTexture(new Rect(tr.x, r.yMax - Config.HintBar, tr.width * Mathf.Clamp01(ten[i]), Config.HintBar), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, ten[i] > Config.HintRed ? Config.Bobber : ink.normal.textColor, 0f, 0f);   // 收线：字条下沿一道张力条，宽 = 字条宽 × 张力，快满变 Bobber 红（loop-10）
        }
        if (Hug.On && at.Length == 2)   // loop-14 抱着时两人头顶中间一个小「♥」（思源黑体有 U+2665）；南北站位挪到北边那人右侧（loop-16 起画，loop-17 以北边那人为基准，见 Heart）
        {
            var m = cam.WorldToScreenPoint(Heart(at[0], at[1]));
            ink.normal.textColor = ink.hover.textColor = Config.Bobber; GUI.Label(new Rect(m.x - 20f, Screen.height - m.y - 14f, 40f, 28f), "♥", ink);
        }
    }
}
