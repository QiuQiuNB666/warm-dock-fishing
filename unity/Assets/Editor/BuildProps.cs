using UnityEngine;

// 道具和床的摆放（loop-5 从 BuildScene 拆出，只搬代码）：箱、桶、梯、船、小屋、岩石、床、篝火（loop-9，排最后），生成顺序和原来一样。
public static class BuildProps
{
    // BuildScene 搭完码头（木板、桩、FishSpot）就调；返回小屋（BuildFx 挂窗灯、Bricks 拿它挡砖）
    public static GameObject Build(Transform root, Transform dock, Material wood, Material woodDark)
    {
        foreach (var p in Config.CratePos) Solid(BuildScene.Model("SM_crate", dock, p, Vector3.one * 0.8f, woodDark)).name = "Crate";
        foreach (var p in Config.BarrelPos) Solid(BuildScene.Model("SM_barrel", dock, p, new Vector3(0.6f, 1f, 0.6f), wood)).name = "Barrel";
        var ladder = new GameObject("Ladder").transform;
        ladder.SetParent(dock, false);
        for (int i = 0; i < 4; i++) BuildNature.Box("Rung", ladder, Config.LadderPos + Vector3.up * (i * 0.3f - 0.2f), new Vector3(0.1f, 0.06f, 0.8f), woodDark);

        // 模型（缺了就用方块占位，场景照样能搭）
        var boat = BuildScene.Model("SM_boat", root, Config.BoatPos, new Vector3(1.5f, 0.6f, 3.5f), wood);
        boat.AddComponent<Floater>();
        var cabin = BuildScene.Model("SM_cabin", root, Config.CabinPos, new Vector3(4f, 3f, 4f), woodDark);
        for (int i = 0; i < Config.RockPos.Length; i++)
            BuildScene.Model("SM_rock", root, Config.RockPos[i], Vector3.one, BuildNature.Mat("M_cliff", Config.Cliff)).transform.localScale = Vector3.one * Config.RockScale[i];

        // 床：小屋门前的沙滩上，摆在两名玩家身后（两人出生在 BedPos 床边）
        var bed = new GameObject("Bed").transform;
        bed.SetParent(root, false);
        bed.position = Config.BedPos + Config.BedFromSpawn;
        Solid(BuildScene.Model("SM_bedframe", bed, bed.position, Config.BedSize, woodDark)).name = "BedFrame";
        Solid(BuildScene.Model("SM_bedding", bed, bed.position + Vector3.up * 0.16f, new Vector3(Config.BedSize.x - 0.1f, 0.1f, Config.BedSize.z - 0.1f), BuildNature.Mat("M_bedding", Config.Bedding))).name = "Bedding";   // 床垫放在床板顶
        // loop-9 篝火：床与码头之间的沙地上（Config.FirePos）；小碰撞体挡人，也自然挡砖（Bricks.Start 把伸出地面的碰撞体都收进 Blockers）；暖光在 BuildFx
        Solid(BuildScene.Model("SM_campfire", root, Config.FirePos, Config.FireSize, BuildNature.Mat("M_cliff", Config.Cliff))).name = "Campfire";
        return cabin;
    }

    // 模型不带碰撞体：按网格包围盒补一个 BoxCollider（木板能踩、箱子桶子床挡人，和原来的方块一样）
    public static GameObject Solid(GameObject g)
    {
        if (g.GetComponent<Collider>() != null) return g;   // 没有 FBX 时的方块占位自带碰撞体
        var b = g.GetComponent<MeshFilter>().sharedMesh.bounds;
        var c = g.AddComponent<BoxCollider>();
        c.center = b.center; c.size = b.size;
        return g;
    }
}
