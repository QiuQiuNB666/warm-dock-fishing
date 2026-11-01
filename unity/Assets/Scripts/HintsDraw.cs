using System.Linq;
using UnityEngine;

// 就近提示的画法（loop-15 从 Hints.cs 搬来，它满了）：OnGUI 画每人的字条（loop-16 左端一道帽子色竖条）、收线张力条、抱着时的「♥」；两张字条叠了左右分开（Apart），头顶 / 脚下的回差（Feet、Low），钓上那句不压跃出的鱼、不压别人的帽子（Cheer / Dodge，loop-20）。同一个类的 partial（同 ScriptedPlay / ScriptedPlan），文字、读键、失败那句在 Hints.cs。
public partial class Hints
{
    GUIStyle style, ink;
    readonly float[] pathAt = new float[KeyMap.Players.Length];   // loop-7 修补：每人面前那格的连通判断（BrickPath.Open，BFS）每 PathEvery 秒算一次，免得每帧算
    static readonly bool[] pathOk = new bool[KeyMap.Players.Length];   // loop-20 改静态：放砖预览也看它（PathOk），BFS 不另算
    public static bool PathOk(int player) => pathOk[player];
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

    // 字条走脚下吗（loop-14 修补）：Feet 的回差，或南北站位挨着——北边 HugRange 内有人（Hug.Below；南北 = Hug.NorthSouth，同 ♥）。抱着、抱完两人留在 HugGap 不动都算，靠南那人头顶的字条不压北边那人的帽子；左右并排、离得远都不算
    // loop-21 或面朝北（forward.z > GhostNorth）且放砖预览正显示（ghost：Bricks.Ghost 不为 null，同 Bricks.Preview 的判据）：预览在人北边一格，头顶字条的底边正压着它、下半截还被自己帽檐挡住；朝南、没预览照旧
    public static bool Low(bool feet, Vector3 me, Vector3[] all, Vector3 forward = default, bool ghost = false) => feet || (ghost && forward.z > Config.GhostNorth) || Hug.Below(me, all.Where(o => Vector2.Distance(new Vector2(o.x, o.z), new Vector2(me.x, me.z)) <= Config.HugRange).ToArray());

    // loop-17 所有 Say 共用的这一处：刚说过一句（saying）就整句换掉提示，只有这人正咬钩时照常画「空格 提竿！」——另一人睡醒 / 天亮说的「天亮了」原会把它换掉 1.5 s（= BiteWindow），鱼必跑。
    // 收线时照换：张力条按 ten[] 另画，不受影响
    public static string Shown(string text, string said, bool saying, FishState fish) => saying && fish != FishState.Bite ? said : text;

    // loop-20 庆祝不挡鱼：显示的那行正是钓上那句（各鱼名的 Caught / 灯笼鱼 Lantern）吗
    public static bool Cheer(string shown) => shown == Lantern || System.Array.Exists(Config.FishNames, f => Caught(f) == shown);

    // 说钓上那句时字条放哪（loop-20 修补，GUI 坐标 y 朝下）：依次试脚下 feet → 头顶高度横挪到鱼的一边（先试挪得少的那边，差不到 HintGap 先左边）→ 提到鱼顶之上，都隔 HintGap；鱼已收走（fish = null）就脚下 → 头顶 head。
    // 取头一个不压鱼（fish：这一跃整条弧占的屏幕范围，见 LeapBox）、不压别人的帽子（hats）、整个在 [0, w] × [top, h] 里的；都不行退回脚下（鱼总在本人上方，脚下挡不到）。
    // 沿革：先是一律往上提一个字条高，再是默认走脚下、南边 HugRange 内有人才提——提起的那张仍挡跃起的鱼，脚下那张仍压南边 1.5–2 m 那人的帽子，于是改按屏幕上的鱼和帽子算
    public static Rect Dodge(Rect feet, Rect head, Rect? fish, Rect[] hats, float top, float w, float h)
    {
        var c = new[] { feet, head };
        if (fish is Rect f)
        {
            float g = Config.HintGap; Rect lt = new Rect(f.xMin - g - head.width, head.y, head.width, head.height), rt = new Rect(f.xMax + g, head.y, head.width, head.height);
            bool left = head.x - lt.x <= rt.x - head.x + g;   // 浮漂偏哪边，鱼的范围就往哪边宽：先挪到另一边，离本人近；两边差不到 HintGap（浮漂正前方）一律先左边，不按浮点误差左右跳
            c = new[] { feet, left ? lt : rt, left ? rt : lt, new Rect(head.x, f.yMin - g - head.height, head.width, head.height) };
        }
        foreach (var r in c) if (r.xMin >= 0f && r.xMax <= w && r.yMin >= top && r.yMax <= h && !(fish is Rect k && k.Overlaps(r)) && !hats.Any(o => o.Overlaps(r))) return r;
        return feet;
    }

    // 几个世界里的轴对齐盒子（中心 cs、半边 e）投到屏幕上合起来的外接矩形（GUI 坐标）：框跃起的鱼（沿弧各点）、框别人的帽子
    Rect ScreenBox(System.Collections.Generic.IEnumerable<Vector3> cs, Vector3 e)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var c in cs) for (int i = 0; i < 8; i++)
        {
            var s = cam.WorldToScreenPoint(c + Vector3.Scale(e, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1)));
            x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, Screen.height - s.y); y1 = Mathf.Max(y1, Screen.height - s.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    // 钓上那条鱼这一跃占的屏幕范围：沿 Tackle.Leap（Tackle 画鱼的同一条弧）从浮漂到手上取 9 点，每点框一条竖着的鱼（半长 FishHalf、半径 FishRadius）；没在 Caught → null。
    // 整个 Caught 都框整条弧、不只框剩下那段：范围不随时间缩，字条不跟着滑、不中途换边（l20f2p1 只框剩下那段时，鱼落进手里后两边一样远，一次挪左一次挪右）
    Rect? LeapBox(PlayerContext p, FishingLogic f) => f != null && f.State == FishState.Caught && Tackle.Hook(p.playerIndex) is Vector3 k
        ? ScreenBox(Enumerable.Range(0, 9).Select(j => Tackle.Leap(k, Tackle.Hand(p.transform), Config.FishLeapTime * j / 8f)), new Vector3(Config.FishRadius, Config.FishHalf, Config.FishRadius)) : (Rect?)null;

    // 抱着时「♥」的世界位置：两人中点上方 HugHeart；南北站位放在北边那人背对南边那人的一侧 HugHeartSide（x = 北.x − sign(南.x − 北.x)·HugHeartSide，正南北取东）；南北 = Hug.NorthSouth（loop-19 和字条走脚下 Low 共用一套）。
    // 沿革：loop-16 中点 + 0.6 m；loop-17 北边那人身旁；loop-18 背对南边那人，南北从 45° 放宽到约 75°（|dx|/2 只到 63.4° 时 ♥ 退回中点，离两人恒 HugGap/2 = 0.45 m，仍压北边那人 0.44 m 的帽檐）
    public static Vector3 Heart(Vector3 a, Vector3 b)
    {
        var m = (a + b) * 0.5f + Vector3.up * Config.HugHeart;
        Vector3 n = a.z > b.z ? a : b, s = a.z > b.z ? b : a;
        if (Hug.NorthSouth(a, b)) m.x = n.x + (s.x > n.x ? -Config.HugHeartSide : Config.HugHeartSide);
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
            string text = Line(Stock.Current, p.playerIndex, p.transform.position, p.transform.forward, Trees.Pos, Trees.Hits, f?.State ?? FishState.Idle, walk || f != null && f.CaughtCount > 0, walk || Trees.TradedOnce[p.playerIndex], at, pathOk[p.playerIndex], walk || Trees.CookedOnce[p.playerIndex], !walk && !Hug.Once && Hug.Ready(players.players), tod == null || Sleep.Evening(tod.t), fail[p.playerIndex], Time.time < failUntil[p.playerIndex]);   // loop-14「H 抱抱」抱过一次（Hug.Once）就不再出；loop-16 黄昏前不列睡觉；刚说过一句（钓鱼失败、烤鱼、小灯塔、放不下砖、天亮了……）整句换掉，这人咬钩时除外（loop-17 Shown，loop-18 和 Text 合成 Line）
            Vector3 v = cam.WorldToViewportPoint(p.transform.position + Vector3.up * 1.8f);   // 人在屏幕外不画（同 TopDownCamera 屏外指示点的判据）
            if (text == "" || v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
            Vector3 h = cam.WorldToScreenPoint(p.transform.position + Vector3.up * Config.HintHeight);
            Vector2 size = style.CalcSize(new GUIContent(text)) + Vector2.right * Config.HintStripe;   // loop-16 左端加宽一道帽子色竖条，字照旧居中在其余部分
            float y = Screen.height - h.y - size.y, fy = Screen.height - cam.WorldToScreenPoint(p.transform.position).y + Config.HintFeetGap;   // 头顶：字的底边在头顶点上；脚下：脚下几像素。头顶会压到顶部「砖 n　木 n」就改画到脚下（不往下夹去压帽子），回差见 Feet
            feet[p.playerIndex] = Feet(feet[p.playerIndex], y);
            bool ghost = Bricks.Ghost(Stock.Current, p.transform.position, p.transform.forward, still[p.playerIndex], f?.State ?? FishState.Idle, Hug.On, at, pathOk[p.playerIndex]) != null;   // loop-21 这人的放砖预览这会儿显示着吗（同 Bricks.Preview 的判据，纯函数）
            var r = new Rect(h.x - size.x * 0.5f, Low(feet[p.playerIndex], p.transform.position, at, p.transform.forward, ghost) ? fy : y, size.x, size.y);   // loop-14 南北站位挨着（抱完不动也算，Low）：屏幕上靠下那人走脚下这一支，不压对方的帽子；loop-21 面朝北、放砖预览显示着也走这一支
            if (Cheer(text)) r = Dodge(new Rect(r.x, fy, size.x, size.y), new Rect(r.x, y, size.x, size.y), LeapBox(p, f), players.players.Where(o => o != null && o != p && o.isActiveAndEnabled).Select(o => ScreenBox(new[] { o.transform.position + Vector3.up * Config.HatMid }, new Vector3(Config.HatBrim, Config.HatHalf, Config.HatBrim))).ToArray(), Config.HintTop, Screen.width, Screen.height);   // loop-20 修补：正说钓上那句，不压跃出的鱼、不压别人的帽子（Dodge）
            rs[n] = r; who[n] = p.playerIndex; ten[n] = f != null && f.State == FishState.Reel ? f.Tension : -1f; ts[n++] = text;
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
        if (Hug.On && at.Length == 2)   // loop-14 抱着时两人头顶中间一个小「♥」（思源黑体有 U+2665）；南北站位放在北边那人背对南边那人的一侧 HugHeartSide；南北 = Hug.NorthSouth（见 Heart）
        {
            var m = cam.WorldToScreenPoint(Heart(at[0], at[1]));
            ink.normal.textColor = ink.hover.textColor = Config.Bobber; GUI.Label(new Rect(m.x - 20f, Screen.height - m.y - 14f, 40f, 28f), "♥", ink);
        }
    }
}
