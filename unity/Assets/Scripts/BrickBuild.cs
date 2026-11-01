using UnityEngine;

// 砖的另一半（loop-15 从 Bricks.cs 搬来，它满了）：按记录生成方块（Rebuild / Spawn，小灯塔顶面另贴一片）和按 B 放砖的读键流程（Update）。同一个类的 partial（同 ScriptedPlay / ScriptedPlan），字段和放砖规则在 Bricks.cs。
public partial class Bricks
{
    public void Rebuild()
    {
        if (root != null) Destroy(root.gameObject);
        root = null;
        foreach (var r in Stock.Current.bricks) Spawn(r);
    }

    void Spawn(BrickRecord r)
    {
        if (root == null) root = new GameObject("Bricks").transform;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Brick";
        g.transform.SetParent(root, false);
        g.transform.position = Center(new Vector2Int(r.x, r.z), r.layer);
        g.transform.localScale = Vector3.one * Config.BrickSize;
        g.GetComponent<Renderer>().sharedMaterial = Lighthouse(r) ? glow : material;
        if (!Lighthouse(r) || glowTop == null) return;   // loop-12 小灯塔顶面另贴一层薄片（glowTop 比侧面 glow 亮，夜里看得出顶面和正面之间那条棱）。不用 Quad 图元：它自带 MeshCollider，工程别处没用到，网页端 IL2CPP 剥引擎代码会剥掉这个类，读档 Spawn 时出错
        var q = new GameObject("top", typeof(MeshFilter), typeof(MeshRenderer)); q.GetComponent<MeshFilter>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; q.GetComponent<Renderer>().sharedMaterial = glowTop;   // 方块自己的网格压扁，不带碰撞体、不进 Blockers
        q.transform.SetParent(g.transform, false); q.transform.localPosition = Vector3.up * 0.505f; q.transform.localScale = new Vector3(1f, 0.01f, 1f);   // 贴在方块顶面上（本地 0.50–0.51）；随方块在 Rebuild 时一起销毁
    }

    void Update()
    {
        if (Pause.On || players == null) return;
        foreach (var p in players.players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            var k = KeyMap.Players[p.playerIndex];
            if (Hug.On || !InputManager2P.Pressed(k.Build)) continue;   // loop-14 抱着不读 B
            int i = p.playerIndex; var c = Front(p.transform.position, p.transform.forward); var at = players.Positions(); int h = Height(Stock.Current, c);
            var why = BrickWhy.Of(Stock.Current, c, h, at, BrickPath.Open(Stock.Current, c, at), p.transform.position);   // loop-7：面前那格有人、放了会切断去码头的路都不放；同格往上垒一层（loop-11 原因码；loop-12 传自己的位置：只有自己压到砖位说「退后半步再放」）
            var r = why == BrickWhy.Code.Ok ? TryPlace(Stock.Current, c, h) : null;
            Debug.Log($"EVT build p{i} cell={c.x},{c.y} {(r != null ? "ok" : "no " + why)}");   // 实玩查面前是哪一格、放没放、为什么
            if (r == null) { Hints.Say(i, BrickWhy.Say(why, p.fishing != null ? p.fishing.Logic.State : FishState.Idle)); continue; }   // loop-11：放不下按原因说一句，在自己字条位置；loop-12 正在钓鱼不说（空串，Hints.Say 不记），免得把「空格 提竿！」换掉
            Spawn(r);
            Hints.Say(i, "放下一砖");   // loop-16 说在自己的字条位置（原来底部 Toast）；垒成小灯塔下一行那句盖过它
            if (Lighthouse(r)) { Hints.Say(i, "垒成一座小灯塔"); Debug.Log($"EVT lighthouse p{i} cell={c.x},{c.y}"); }
        }
    }
}
