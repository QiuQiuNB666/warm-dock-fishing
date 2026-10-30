using System;
using System.Linq;
using UnityEngine;

// 存档：单键 pier.save.v1，只用 JsonUtility。存 version、TimeOfDay.t、床点、砖/木/石、每人的生鱼/熟鱼/布、带层高的砖记录；
// 不存坐标、不存钓鱼中的状态（读档后两人站回床边）。Save() 结尾和「失焦且有改动」时都 PlayerPrefs.Save()；
// 不用 System.IO，也不等 OnApplicationQuit。坏档直接丢弃、照常开局。
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

    public static void Save()
    {
        var tod = FindAnyObjectByType<TimeOfDay>();
        PlayerPrefs.SetString(Key, ToJson(Capture(tod != null ? tod.t : 0f, Bed, Stock.Current, Config.PlayerCount)));
        Stock.Current.dirty = false;
        PlayerPrefs.Save();
    }

    // 开局：读档（没档、坏档都新开），两人站到床边（P2 偏开 P2BedOffset）
    void Start()
    {
        Data d = null;
        try { d = Read(); } catch (Exception e) { Debug.LogWarning("SAVE-DISCARDED " + e.Message); }
        if (d != null)
        {
            Stock.Current = Restore(d, out float t, out Bed);
            var tod = FindAnyObjectByType<TimeOfDay>();
            if (tod != null) tod.t = t;
        }
        PlaceAtBed();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus && Stock.Current.dirty) Save();
    }

    static void PlaceAtBed()
    {
        var pm = FindAnyObjectByType<PlayerManager>();
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
