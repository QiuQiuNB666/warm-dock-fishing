using UnityEngine;

// 角上的钓鱼字（loop-10 从 FishingStateMachine.OnGUI 搬来，它满了）：P1 左上、P2 右上一行「Fish: n 状态 鱼名　生鱼 n　熟鱼 n」，深色半透明底；
// 收线时下面两根条（张力、收线）：整条底框 + 按比例填充，WarmCJK 的「张力」「收线」画在整条上（不跟着填充宽度被裁掉）、纵向居中进条里。
public static class FishHud
{
    static GUIStyle cjk, bar;

    public static void Draw(FishingLogic logic, int owner, Font font)
    {
        float x = owner == 0 ? 10f : Screen.width - 310f, bx = owner == 0 ? 10f : Screen.width - 210f;   // P1 左上，P2 右上
        string head = $"Fish: {logic.CaughtCount}   {logic.State}  {logic.LastFish}", fish = $"生鱼 {Stock.Current.Count(ItemType.RawFish, owner)}　熟鱼 {Stock.Current.Count(ItemType.CookedFish, owner)}";   // 原来的字不动，后面接「生鱼 n　熟鱼 n」（loop-9 加熟鱼）
        cjk ??= new GUIStyle(GUI.skin.label) { font = font, fontSize = 13 };
        bar ??= new GUIStyle(cjk) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 0, 0, 0), clipping = TextClipping.Overflow };   // loop-10：字在 18 px 高的条里上下居中（原来 22 px 高的字框从条顶往下排，字压在条下沿）
        float hw = GUI.skin.label.CalcSize(new GUIContent(head)).x;
        GUI.Box(new Rect(x - 4f, 8f, hw + 14f + cjk.CalcSize(new GUIContent(fish)).x, 26f), GUIContent.none);   // 深色半透明底（GUI.skin.box，同顶部「砖 n　木 n」），白天沙地上也看得清
        GUI.Label(new Rect(x, 10, 300, 22), head);
        GUI.Label(new Rect(x + hw + 6f, 10, 120, 22), fish, cjk);
        if (logic.State != FishState.Reel) return;
        for (int i = 0; i < 2; i++)   // 张力条、收线条
        {
            var r = new Rect(bx, 36f + 22f * i, 200f, 18f);
            GUI.Box(r, GUIContent.none); GUI.Box(new Rect(r.x, r.y, 200f * Mathf.Clamp01(i == 0 ? logic.Tension : logic.Progress), r.height), GUIContent.none);
            GUI.Label(r, i == 0 ? "张力" : "收线", bar);
        }
    }
}
