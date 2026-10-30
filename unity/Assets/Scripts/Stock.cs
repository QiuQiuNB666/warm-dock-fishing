using System;
using System.Collections.Generic;
using UnityEngine;

// 每人一份：生鱼、熟鱼、布（存档的一部分）
[Serializable] public class PlayerStock { public int rawFish, cookedFish, cloth; }

// 已放下的一块砖：格子坐标（一格 Config.CellSize）+ 第几层（0 起）
[Serializable] public class BrickRecord { public int x, z, layer; }

// 库存：砖/木/石两人共用，生鱼/熟鱼/布每人一份，外加已放下的砖。只用 public 字段，不用 Dictionary、不用属性。
[Serializable]
public class Stock
{
    public int brick, wood, stone;
    public PlayerStock[] players = { new PlayerStock(), new PlayerStock() };
    public List<BrickRecord> bricks = new List<BrickRecord>();

    [NonSerialized] public bool dirty;   // 有改动：失焦时据此决定要不要存
    public static Stock Current = new Stock();

    public static int Max(ItemType t) =>
        t == ItemType.Brick ? Config.MaxBrick : t == ItemType.Wood ? Config.MaxWood : t == ItemType.Stone ? Config.MaxStone :
        t == ItemType.Cloth ? Config.MaxCloth : Config.MaxFish;

    public int Count(ItemType t, int player = 0)
    {
        switch (t)
        {
            case ItemType.Brick: return brick;
            case ItemType.Wood: return wood;
            case ItemType.Stone: return stone;
            case ItemType.RawFish: return Player(player).rawFish;
            case ItemType.CookedFish: return Player(player).cookedFish;
            default: return Player(player).cloth;
        }
    }

    public void Set(ItemType t, int n, int player = 0)
    {
        n = Mathf.Clamp(n, 0, Max(t));
        switch (t)
        {
            case ItemType.Brick: brick = n; break;
            case ItemType.Wood: wood = n; break;
            case ItemType.Stone: stone = n; break;
            case ItemType.RawFish: Player(player).rawFish = n; break;
            case ItemType.CookedFish: Player(player).cookedFish = n; break;
            default: Player(player).cloth = n; break;
        }
        dirty = true;
    }

    public void Add(ItemType t, int n = 1, int player = 0) => Set(t, Count(t, player) + n, player);

    public bool Take(ItemType t, int n = 1, int player = 0)
    {
        if (Count(t, player) < n) return false;
        Set(t, Count(t, player) - n, player);
        return true;
    }

    PlayerStock Player(int i)
    {
        if (players == null || players.Length <= i)
        {
            var grown = new PlayerStock[i + 1];
            for (int k = 0; k < grown.Length; k++) grown[k] = players != null && k < players.Length && players[k] != null ? players[k] : new PlayerStock();
            players = grown;
        }
        return players[i] ??= new PlayerStock();
    }
}
