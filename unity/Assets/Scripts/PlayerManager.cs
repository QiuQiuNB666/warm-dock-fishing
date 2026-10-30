using System.Linq;
using UnityEngine;

// 运行时按名字找到场景里已有的 Player / Player2（不生成角色）。单人模式关掉 Player2 整棵，
// 连同它挂在 World 下的浮漂、上钩的鱼、水花，免得水里多漂一个浮漂。
public class PlayerManager : MonoBehaviour
{
    public PlayerContext[] players = new PlayerContext[0];
    public bool Bound { get; private set; }

    void Awake() => Bind();

    public void Bind()
    {
        if (Bound) return;
        PlayerContext p1 = Find("Player"), p2 = Find("Player2");
        if (p2 != null && Config.PlayerCount == 1) { Hide(p2); p2 = null; }
        players = new[] { p1, p2 }.Where(p => p != null).ToArray();
        Bound = true;
    }

    static PlayerContext Find(string name)
    {
        var g = GameObject.Find(name);
        return g != null ? g.GetComponent<PlayerContext>() : null;
    }

    static void Hide(PlayerContext p)
    {
        var t = p.GetComponent<Tackle>();
        if (t != null)
            foreach (var c in new Component[] { t.bobber, t.fish, t.splash })
                if (c != null) c.gameObject.SetActive(false);
        p.gameObject.SetActive(false);
    }
}
