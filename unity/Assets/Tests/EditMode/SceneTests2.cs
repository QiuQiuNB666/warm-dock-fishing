using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static BuildSceneTests;

// 场景断言第二份（loop-15 从 BuildSceneTests 搬来，它满了）：loop 以来加的几条。同样打开 BuildScene 产出的 Main.unity，常量全部取自 Config，All / One 用 BuildSceneTests 的。
public class SceneTests2
{
    [OneTimeSetUp] public void Open() => new BuildSceneTests().Open();

    // loop-9 篝火：一个、带碰撞体（挡人也挡砖）、上方一盏点光源；离每棵树都比 InteractRange + ChopRange 远（烤鱼和砍树的 E 范围不相交）
    [Test] public void CampfireClearOfTrees()
    {
        var f = One("Campfire");
        Assert.IsNotNull(f.GetComponent<BoxCollider>(), "碰撞体");
        Assert.IsNotNull(f.GetComponentInChildren<Light>(), "暖光");
        foreach (var t in All().Where(g => g.name == "Tree"))
            Assert.Greater(Vector2.Distance(new Vector2(t.transform.position.x, t.transform.position.z), new Vector2(Config.FirePos.x, Config.FirePos.z)), Config.InteractRange + Config.ChopRange, t.transform.position.ToString());
    }

    // loop-12 修补（逃逸缺陷）：读档重建的小灯塔顶块恰好一片子 Renderer 用 glowTop，是方块自己的网格压扁、子物体不带碰撞体
    //（Quad 图元自带 MeshCollider，场景里一个都没有，网页端剥引擎代码会剥掉它，读档 Spawn 出错）；侧面比顶面暗。新建的 Bricks，不用场景里那个
    [Test] public void LighthouseTopSlab()
    {
        Stock keep = Stock.Current; var b = new GameObject("t").AddComponent<Bricks>(); b.glowTop = new Material(b.glow = new Material(Shader.Find("Universal Render Pipeline/Lit"))); Transform block = null;
        try
        {
            Stock.Current = new Stock(); for (int k = 0; k < Config.MaxHeight; k++) Stock.Current.bricks.Add(new BrickRecord { x = 0, z = -7, layer = k });
            b.Rebuild(); block = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).First(x => x.sharedMaterial == b.glow).transform;
            var tops = block.GetComponentsInChildren<Renderer>().Where(x => x.sharedMaterial == b.glowTop).ToArray();
            Assert.AreEqual(1, tops.Length, "顶块恰好一片子 Renderer 用 glowTop"); Assert.AreEqual(0, block.GetComponentsInChildren<Collider>().Count(c => c.transform != block), "子物体不带碰撞体");
            Assert.AreEqual(block.GetComponent<MeshFilter>().sharedMesh, tops[0].GetComponent<MeshFilter>().sharedMesh, "顶面用方块自己的网格，不是 Quad 图元"); Assert.Less(Config.TowerSide, 1f, "侧面比顶面暗");
        }
        finally { Stock.Current = keep; if (block != null) Object.DestroyImmediate(block.parent.gameObject); Object.DestroyImmediate(b.gameObject); }
    }
}
