using System;
using System.Linq;
using UnityEngine;

// 存档：单键 pier.save.v1，只用 JsonUtility。存 version、TimeOfDay.t、床点、砖/木/石、每人的生鱼/熟鱼/布、带层高的砖记录；
// 不存坐标、不存钓鱼中的状态（读档后两人站回床边）。何时存：失焦且有改动、改动攒够 AutosaveSec 秒、床边按 R 睡觉、
// 暂停菜单「保存并退出」；每次都 PlayerPrefs.Save()。不用 System.IO，也不等 OnApplicationQuit。坏档直接丢弃、照常开局。
public class PierSave : MonoBehaviour
{
    public const string Key = "pier.save.v1";
    public const int Version = 1;

    [Serializable]
    public class Data
    {
        public int version;
        public float timeOfDay;
        public Vector3 bed;
        public int brick, wood, stone;
        public PlayerStock[] players;
        public BrickRecord[] bricks;
    }

    public static Vector3 Bed = Config.BedPos;   // 当前床点
    public static float Morning => Array.IndexOf(Config.TimeNames, "morning") / (float)Config.Times.Length;   // Config 里早晨那一档（t = 0）
    public static Stock NewGame() => new Stock { brick = Config.TestBricks };   // 新开局：TestBricks = 0，一块砖都不送

    TimeOfDay tod;
    PlayerManager pm;
    Bricks bricks;
    float dirtyFor;   // Stock 连着「有改动」多久了

    public static Data Capture(float t, Vector3 bed, Stock s, int playerCount) => new Data
    {
        version = Version, timeOfDay = t, bed = bed, brick = s.brick, wood = s.wood, stone = s.stone,
        players = Enumerable.Range(0, playerCount).Select(i => new PlayerStock
            { rawFish = s.Count(ItemType.RawFish, i), cookedFish = s.Count(ItemType.CookedFish, i), cloth = s.Count(ItemType.Cloth, i) }).ToArray(),
        bricks = s.bricks.Select(b => new BrickRecord { x = b.x, z = b.z, layer = b.layer }).ToArray(),
    };

    public static Stock Restore(Data d, out float t, out Vector3 bed)
    {
        t = d.timeOfDay; bed = d.bed;
        var s = new Stock { brick = d.brick, wood = d.wood, stone = d.stone };
        var ps = d.players ?? new PlayerStock[0];
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i] == null) continue;
            s.Set(ItemType.RawFish, ps[i].rawFish, i); s.Set(ItemType.CookedFish, ps[i].cookedFish, i); s.Set(ItemType.Cloth, ps[i].cloth, i);
        }
        if (d.bricks != null) s.bricks.AddRange(d.bricks.Where(b => b != null));
        s.dirty = false;
        return s;
    }

    public static string ToJson(Data d) => JsonUtility.ToJson(d);

    // 解析失败、版本不对都算坏档 → null
    public static Data Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { var d = JsonUtility.FromJson<Data>(json); return d != null && d.version == Version ? d : null; }
        catch (Exception) { return null; }
    }

    public static void Write(Data d)
    {
        PlayerPrefs.SetString(Key, ToJson(d));
        PlayerPrefs.Save();
    }

    // 没档返回 null；坏档删掉并返回 null
    public static Data Read()
    {
        if (!PlayerPrefs.HasKey(Key)) return null;
        var d = Parse(PlayerPrefs.GetString(Key));
        if (d != null) return d;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        Debug.LogWarning("SAVE-DISCARDED 坏档已丢弃，照常开局");
        return null;
    }

    public static Data Save(TimeOfDay tod = null)
    {
        tod ??= FindAnyObjectByType<TimeOfDay>();
        var d = Capture(tod != null ? tod.t : 0f, Bed, Stock.Current, Config.PlayerCount);
        PlayerPrefs.SetString(Key, ToJson(d));
        Stock.Current.dirty = false;
        PlayerPrefs.Save();
        return d;
    }

    // R 睡觉：时间跳到早晨那一档，两人回床边（P2 偏开 P2BedOffset），立刻存档。不 Application.Quit。
    public static Data Sleep(TimeOfDay tod, PlayerManager pm)
    {
        if (tod != null) tod.t = Morning;
        PlaceAtBed(pm);
        return Save(tod);
    }

    public static bool NearBed(Vector3 p) =>   // 离床（床点 + BedFromSpawn 就是床的中心）不超过 InteractRange
        Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Bed.x + Config.BedFromSpawn.x, Bed.z + Config.BedFromSpawn.z)) <= Config.InteractRange;

    // 开局：读档（没档、坏档都新开），两人站到床边（P2 偏开 P2BedOffset）
    void Start()
    {
        tod = FindAnyObjectByType<TimeOfDay>(); pm = FindAnyObjectByType<PlayerManager>(); bricks = FindAnyObjectByType<Bricks>();
        Data d = null;
        try { d = Read(); } catch (Exception e) { Debug.LogWarning("SAVE-DISCARDED " + e.Message); }
        if (d != null)
        {
            Stock.Current = Restore(d, out float t, out Bed);
            if (tod != null) tod.t = t;
        }
        else Stock.Current = NewGame();
        if (bricks != null) bricks.Rebuild();   // 按记录把砖摆回去
        PlaceAtBed(pm);
    }

    void Update()
    {
        dirtyFor = Stock.Current.dirty ? dirtyFor + Time.unscaledDeltaTime : 0f;
        if (dirtyFor > Config.AutosaveSec) { Save(tod); dirtyFor = 0f; }   // 改动攒了 30 秒：自己存一次，不等失焦
        if (Pause.On || pm == null) return;
        foreach (var p in pm.players)   // 床边按 R（P2 小键盘 4）
            if (p != null && p.isActiveAndEnabled && InputManager2P.Pressed(KeyMap.Players[p.playerIndex].R) && NearBed(p.transform.position))
            {
                Sleep(tod, pm);
                if (bricks != null) bricks.Toast("天亮了");
                break;
            }
    }

    void OnApplicationFocus(bool focus) { if (!focus && Stock.Current.dirty) Save(tod); }

    static void PlaceAtBed(PlayerManager pm)
    {
        if (pm == null) return;
        pm.Bind();
        foreach (var p in pm.players)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.transform.position = Bed + (p.playerIndex == 0 ? Vector3.zero : Config.P2BedOffset);
            if (cc != null) cc.enabled = true;
        }
    }
}
