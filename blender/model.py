"""$BLENDER -b -P blender/model.py -- [--dst assets_raw] [--only cabin,boat]
程序化低多边形道具 → <dst>/<name>.glb，再由 blender/clean.py 转成 assets/SM_<name>.fbx。
- 平直着色，一种颜色一个材质；材质名就是场景里同名的 Config 色材质（M_wood、M_wood_dark…），
  Unity 的 BuildScene.Model 按名字换成 Assets/Art/Materials/ 下的同名材质，颜色只认 Config。
  Base Color 照 rig.py 的做法直接填 Config 的 sRGB 数值；粗糙度 0.9；不用贴图。
- 坐标：Blender X = Unity X，Blender Y = Unity Z（远离相机），Z 朝上；原点 = 底面中心（clean.py 会再对一次）。
- 面数按三角形数，超上限打印 MODEL FAIL。"""
import math, os, random, sys

import bmesh
import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PALETTE = {   # Config.cs 已有的颜色；M_glow 是 BuildFx 现有的发光材质（灯笼、窗）
    "M_wood": (0.62, 0.43, 0.27), "M_wood_dark": (0.45, 0.30, 0.19), "M_coral": (0.96, 0.50, 0.45),
    "M_cliff": (0.58, 0.55, 0.50), "M_leaf": (0.36, 0.66, 0.38), "M_trunk": (0.50, 0.35, 0.22),
    "M_bedding": (0.95, 0.93, 0.86), "M_glow": (1.0, 0.9, 0.7),
}
LIMIT = {"cabin": 1500, "boat": 800, "rock": 400}   # 其余每件 ≤200


def arg(flag, default=None):
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return a[a.index(flag) + 1] if flag in a else default


class Part:
    """顶点/面/材质累加器。每个面自带顶点（不共享），整体平直着色。"""

    def __init__(self):
        self.v, self.f, self.m = [], [], []

    def face(self, pts, mat):
        i = len(self.v)
        self.v += [tuple(float(c) for c in p) for p in pts]
        self.f.append(tuple(range(i, i + len(pts))))
        self.m.append(mat)

    def box(self, lo, hi, mat, skip=""):
        """轴对齐长方体；skip 里的面不建：b 底 t 顶 f 前(-Y) k 后(+Y) l 左(-X) r 右(+X)。"""
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        for k, idx in (("b", (0, 3, 2, 1)), ("t", (4, 5, 6, 7)), ("f", (0, 1, 5, 4)),
                       ("k", (2, 3, 7, 6)), ("l", (3, 0, 4, 7)), ("r", (1, 2, 6, 5))):
            if k not in skip:
                self.face([c[j] for j in idx], mat)

    def tube(self, rings, mats, top=None):
        """自下而上的截面环（同点数、逆时针）连成侧面，mats[i] 是第 i 段材质；top 给材质就封顶。"""
        for r0, r1, m in zip(rings, rings[1:], mats):
            n = len(r0)
            for i in range(n):
                j = (i + 1) % n
                self.face([r0[i], r0[j], r1[j], r1[i]], m)
        if top:
            self.face(rings[-1], top)

    def cone(self, base, apex, mat):
        n = len(base)
        for i in range(n):
            self.face([base[i], base[(i + 1) % n], apex], mat)

    def mirror_face(self, pts, mat, sx):
        """sx=-1 时按 X 镜像并反转绕序（法线仍朝外）。"""
        self.face(pts if sx > 0 else [(-x, y, z) for x, y, z in reversed(pts)], mat)

    def tris(self):
        return sum(len(f) - 2 for f in self.f)


def ring(n, r, z, cx=0.0, cy=0.0, rot=0.0):
    """绕 Z 的 n 边形截面，从上往下看逆时针。"""
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), cy + r * math.sin(rot + 2 * math.pi * i / n), z) for i in range(n)]


def rect(x0, y0, x1, y1, z):
    return [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]


def material(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*PALETTE[name], 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    m.diffuse_color = (*PALETTE[name], 1)
    return m


# ---------- 小屋：山墙朝相机（-Y）带两扇窗，门在 +X 朝床，门口一级石阶；不做室内 ----------
def cabin():
    P = Part()
    W, D, H, F = 1.275, 0.95, 2.05, 0.18          # 墙半宽(X)、半深(Y)、檐高、石基高
    k = math.tan(math.radians(38))
    R = H + W * k                                  # 屋脊高
    OE, OG, T, S = 0.25, 0.2, 0.07, 0.05           # 檐口外挑、山墙外挑、瓦厚、每排瓦下缘的错台

    def roof(x):                                   # 屋面底：过墙顶和屋脊
        return R - abs(x) * k

    P.box((-W - .05, -D - .05, 0), (W + .05, D + .05, F), "M_cliff", "b")          # 石基
    pent = [(-W, F), (W, F), (W, H), (0, R), (-W, H)]
    P.face([(x, -D, z) for x, z in pent], "M_wood")                                 # 前山墙（朝相机）
    P.face([(x, D, z) for x, z in reversed(pent)], "M_wood")                       # 后山墙
    P.face([(-W, D, F), (-W, -D, F), (-W, -D, H), (-W, D, H)], "M_wood")           # -X 墙

    dy, dz, rv = 0.42, 1.78, 0.1                   # 门半宽、门顶高、门洞进深
    for y0, y1, z0, z1 in ((-D, -dy, F, H), (dy, D, F, H), (-dy, dy, dz, H)):     # +X 墙，留出门洞
        P.face([(W, y0, z0), (W, y1, z0), (W, y1, z1), (W, y0, z1)], "M_wood")
    P.face([(W, -dy, F), (W - rv, -dy, F), (W - rv, -dy, dz), (W, -dy, dz)], "M_wood_dark")   # 门洞两壁、顶
    P.face([(W, dy, F), (W, dy, dz), (W - rv, dy, dz), (W - rv, dy, F)], "M_wood_dark")
    P.face([(W, -dy, dz), (W - rv, -dy, dz), (W - rv, dy, dz), (W, dy, dz)], "M_wood_dark")
    P.face([(W - rv, -dy, F), (W - rv, dy, F), (W - rv, dy, dz), (W - rv, -dy, dz)], "M_wood_dark")   # 门板
    P.box((W, -dy - .08, F), (W + .045, -dy, dz + .08), "M_wood_dark", "b")       # 门框
    P.box((W, dy, F), (W + .045, dy + .08, dz + .08), "M_wood_dark", "b")
    P.box((W, -dy, dz), (W + .045, dy, dz + .08), "M_wood_dark", "b")
    P.box((W + .045, -.6, 0), (W + .35, .6, F - .03), "M_cliff", "b")             # 门口一级矮阶

    for sx in (-1, 1):                                                              # 四根深色角柱
        for sy in (-1, 1):
            P.box((sx * W - .06, sy * D - .06, F), (sx * W + .06, sy * D + .06, H), "M_wood_dark", "bt")
    for z in (.62, 1.86):                                                           # 前墙两道深色板缝
        P.box((-W, -D - .012, z), (W, -D, z + .035), "M_wood_dark", "bk")

    z0, z1, hw = 1.2, 1.66, .23                                                     # 两扇窗（-Y 面）
    for cx in (-.6, .6):
        P.face([(cx - hw, -D - .01, z0), (cx + hw, -D - .01, z0), (cx + hw, -D - .01, z1), (cx - hw, -D - .01, z1)], "M_glow")
        P.box((cx - hw - .06, -D - .05, z0 - .06), (cx + hw + .06, -D, z0), "M_wood_dark", "k")   # 下框
        P.box((cx - hw - .06, -D - .05, z1), (cx + hw + .06, -D, z1 + .06), "M_wood_dark", "k")   # 上框
        P.box((cx - hw - .06, -D - .05, z0), (cx - hw, -D, z1), "M_wood_dark", "kbt")             # 左右框
        P.box((cx + hw, -D - .05, z0), (cx + hw + .06, -D, z1), "M_wood_dark", "kbt")
        P.box((cx - .02, -D - .035, z0), (cx + .02, -D - .01, z1), "M_wood_dark", "kbt")          # 十字窗棂
        zm = (z0 + z1) / 2
        P.box((cx - hw, -D - .035, zm - .02), (cx + hw, -D - .01, zm + .02), "M_wood_dark", "k")
        P.box((cx - hw - .08, -D - .2, z0 - .22), (cx + hw + .08, -D, z0 - .06), "M_wood_dark", "k")  # 窗下花箱
        for i, ox in enumerate((-.17, 0, .17)):
            P.cone(ring(5, .1, z0 - .06, cx + ox, -D - .1, rot=i), (cx + ox, -D - .1, z0 + .07 + .03 * (i % 2)), "M_leaf")
        for s in (-1, 1):                                                           # 两边开着的百叶
            xa, xb = sorted((cx + s * (hw + .06), cx + s * (hw + .26)))
            P.box((xa, -D - .03, z0), (xb, -D, z1), "M_coral", "k")

    xs = [0.0, 0.52, 1.04, W + OE]                                                  # 三排瓦：屋脊 → 檐口
    y0, y1 = -D - OG, D + OG
    for sx in (-1, 1):
        for j in range(3):
            a, b = xs[j], xs[j + 1]
            za, zb, bb = roof(a) + T, roof(b) + T + S, roof(b)                     # 下缘多翘 S，排与排之间露出一道台阶
            ba = roof(a)
            P.mirror_face([(a, y0, za), (b, y0, zb), (b, y1, zb), (a, y1, za)], "M_coral", sx)   # 瓦面
            P.mirror_face([(b, y0, bb), (b, y1, bb), (b, y1, zb), (b, y0, zb)], "M_coral", sx)   # 下缘立面
            P.mirror_face([(a, y0, ba), (b, y0, bb), (b, y0, zb), (a, y0, za)], "M_coral", sx)   # 两端
            P.mirror_face([(a, y1, za), (b, y1, zb), (b, y1, bb), (a, y1, ba)], "M_coral", sx)
    zc, top = roof(.14) + T + .01, R + T + .08                                      # 屋脊压条
    ry0, ry1 = y0 - .02, y1 + .02
    P.face([(-.14, ry0, zc), (.14, ry0, zc), (0, ry0, top)], "M_wood_dark")
    P.face([(0, ry1, top), (.14, ry1, zc), (-.14, ry1, zc)], "M_wood_dark")
    for sx in (-1, 1):
        P.mirror_face([(.14, ry0, zc), (.14, ry1, zc), (0, ry1, top), (0, ry0, top)], "M_wood_dark", sx)
    P.box((-.78, .25, roof(.78) - .2), (-.46, .57, R + .3), "M_cliff", "bt")       # 石烟囱（背坡）
    P.box((-.82, .21, R + .3), (-.42, .61, R + .38), "M_cliff", "b")
    return P


# ---------- 船：空舱、船舷外翻、一根横座；船头收成一条线，船尾方板 ----------
def boat():
    P = Part()
    st = [(-1.70, .50, .10, .60), (-1.15, .64, .04, .57), (-.55, .71, 0, .56), (.05, .69, 0, .57),
          (.65, .58, .03, .60), (1.20, .36, .11, .67), (1.68, 0, .30, .78)]       # 站位 y、舷缘半宽 b、龙骨高 k、舷缘高 g
    t = .05                                                                         # 板厚

    def outer(y, b, kz, g, s):                                                      # 龙骨 → 舭 → 舷侧 → 舷缘（最外，外翻）
        return [(0, y, kz), (s * .55 * b, y, kz + .07), (s * .86 * b, y, kz + .32 * (g - kz)), (s * b, y, g)]

    def inner(y, b, kz, g, s, i):
        y = y + t if i == 0 else y                                                  # 船尾内板往里收一个板厚
        return [(0, y, kz + t), (s * max(0, .55 * b - t), y, kz + .07 + t),
                (s * max(0, .86 * b - t), y, kz + .32 * (g - kz)), (s * max(0, b - t), y, g)]

    paint = ["M_wood", "M_wood", "M_coral"]                                         # 外壳上沿刷一道珊瑚色
    for s in (1, -1):
        O = [outer(*q, s) for q in st]
        I = [inner(*q, s, i) for i, q in enumerate(st)]
        for j in range(len(st) - 1):
            for c in range(3):
                q = [O[j][c], O[j + 1][c], O[j + 1][c + 1], O[j][c + 1]]
                P.face(q if s > 0 else q[::-1], paint[c])
                q = [I[j][c], I[j + 1][c], I[j + 1][c + 1], I[j][c + 1]]
                P.face(q[::-1] if s > 0 else q, "M_wood")
            q = [O[j][3], O[j + 1][3], I[j + 1][3], I[j][3]]                        # 舷缘顶条
            P.face(q if s > 0 else q[::-1], "M_wood_dark")
    r, l = outer(*st[0], 1), outer(*st[0], -1)
    P.face([r[0]] + r[1:] + l[1:][::-1], "M_coral")                                # 船尾方板（朝相机）
    ri, li = inner(*st[0], 1, 0), inner(*st[0], -1, 0)
    P.face(([ri[0]] + ri[1:] + li[1:][::-1])[::-1], "M_wood")                       # 船尾内板
    P.face([l[3], r[3], ri[3], li[3]], "M_wood_dark")                               # 船尾顶条
    P.box((-.6, .1, .37), (.6, .32, .42), "M_wood_dark", "b")                      # 横座
    P.box((-.035, 1.6, .7), (.035, 1.72, .86), "M_wood_dark", "b")                 # 船头柱
    return P


# ---------- 礁石：两团凸包拼成一块，底面压平坐在海面；一块石头缩放用在三处 ----------
def rock():
    P = Part()
    rnd = random.Random(7)

    def hull(cx, cy, sx, sy, sz, n):
        bm = bmesh.new()
        for i in range(n):
            z = 1 - 2 * (i + .5) / n                                                # 球面均匀取点（Fibonacci）再抖半径
            rr, a, j = math.sqrt(1 - z * z), i * 2.39996, rnd.uniform(.8, 1.1)
            bm.verts.new((cx + sx * rr * math.cos(a) * j, cy + sy * rr * math.sin(a) * j, max(0.0, sz * (z + .3) * j)))
        bmesh.ops.convex_hull(bm, input=list(bm.verts))
        for f in bm.faces:
            f.normal_update()
            if f.normal.z > -.9:                                                    # 朝下的底面看不见，不要
                P.face([v.co[:] for v in f.verts], "M_cliff")
        bm.free()

    hull(0, 0, .95, .75, .62, 30)
    hull(.62, .28, .5, .42, .42, 16)
    return P


# ---------- 场景小件（每件 ≤200 面；名字、数量、位置都由 BuildScene/BuildNature/BuildFx 照旧给） ----------
def tree():   # 三层尖树冠 + 六棱树干，大小对着原来的圆锥树（s=1 时高约 3.6 m、下层冠径约 2.6 m）
    P = Part()
    rnd = random.Random(3)
    P.tube([ring(6, .16, 0), ring(6, .11, 1.3)], ["M_trunk"])                                  # 顶被树冠盖住，不封
    for r, z, h, rot in ((1.3, .9, 1.5, 0.0), (1.0, 1.7, 1.4, .45), (.65, 2.45, 1.2, .2)):
        base = [(x * rnd.uniform(.9, 1.1), y * rnd.uniform(.9, 1.1), zz + rnd.uniform(-.08, .05)) for x, y, zz in ring(7, r, z, rot=rot)]
        P.cone(base, (rnd.uniform(-.05, .05), rnd.uniform(-.05, .05), z + h), "M_leaf")       # 冠底朝下，俯视看不见，不封
    return P


def crate():   # 0.8 m 木箱：深色角条和上下两圈横条，顶面两道板缝
    P = Part()
    s, b = .4, .07
    P.box((-s + .03, -s + .03, 0), (s - .03, s - .03, 2 * s - .03), "M_wood", "b")
    for x0, x1 in ((-s, -s + b), (s - b, s)):
        for y0, y1 in ((-s, -s + b), (s - b, s)):
            P.box((x0, y0, 0), (x1, y1, 2 * s), "M_wood_dark", "b")
    for z0 in (0, 2 * s - b):
        for lo, hi in (((-s + b, -s, z0), (s - b, -s + b, z0 + b)), ((-s + b, s - b, z0), (s - b, s, z0 + b)),
                       ((-s, -s + b, z0), (-s + b, s - b, z0 + b)), ((s - b, -s + b, z0), (s, s - b, z0 + b))):
            P.box(lo, hi, "M_wood_dark", "b")
    for y in (-.12, .12):
        P.box((-s + b, y - .012, 2 * s - .03), (s - b, y + .012, 2 * s - .025), "M_wood_dark", "b")
    return P


def barrel():   # 高 1 m、鼓肚，两道深色铁箍，顶上深色箍沿 + 稍凹的木盖
    P = Part()
    rs = [ring(8, r, z, rot=math.pi / 8) for z, r in ((0, .265), (.06, .29), (.14, .295), (.5, .325), (.86, .295), (.94, .29), (1.0, .265))]
    P.tube(rs, ["M_wood", "M_wood_dark", "M_wood", "M_wood", "M_wood_dark", "M_wood"])
    P.tube([rs[-1], ring(8, .22, 1.0, rot=math.pi / 8), ring(8, .22, .975, rot=math.pi / 8)], ["M_wood_dark", "M_wood_dark"], top="M_wood")
    return P


def lantern():   # 发光灯身 + 深色底座、角柱、四坡小顶（原来是 0.28×0.34×0.28 的发光方块）
    P = Part()
    P.box((-.15, -.15, 0), (.15, .15, .05), "M_wood_dark", "b")
    P.box((-.13, -.13, .05), (.13, .13, .34), "M_glow", "bt")
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.box((sx * .13 - .02, sy * .13 - .02, .05), (sx * .13 + .02, sy * .13 + .02, .34), "M_wood_dark", "bt")
    P.cone(rect(-.16, -.16, .16, .16, .34), (0, 0, .44), "M_wood_dark")
    P.box((-.025, -.025, .44), (.025, .025, .5), "M_wood_dark", "b")
    return P


def bedframe():   # 2 m × 1 m（Config.BedSize），床头板在 -X（靠小屋）
    P = Part()
    L, Wd, w = 1.0, .5, .07
    for x0, x1 in ((-L, -L + w), (L - w, L)):
        for y0, y1 in ((-Wd, -Wd + w), (Wd - w, Wd)):
            P.box((x0, y0, 0), (x1, y1, .12), "M_wood_dark", "b")                                # 四条腿
    P.box((-L, -Wd, .12), (L, -Wd + w, .28), "M_wood_dark", "b")                                # 两条侧板
    P.box((-L, Wd - w, .12), (L, Wd, .28), "M_wood_dark", "b")
    P.box((-L + w, -Wd + w, .12), (L - w, Wd - w, .16), "M_wood_dark", "b")                     # 床板
    P.box((-L, -Wd, .12), (-L + w, Wd, .78), "M_wood_dark", "b")                                # 床头板
    P.box((L - w, -Wd, .12), (L, Wd, .46), "M_wood_dark", "b")                                  # 床尾板
    return P


def bedding():   # 白床垫（上沿倒角）+ 枕头；BuildScene 把它放到床板顶（离地 0.16 m）
    P = Part()
    P.tube([rect(-.93, -.43, .93, .43, 0), rect(-.93, -.43, .93, .43, .13), rect(-.9, -.4, .9, .4, .16)], ["M_bedding", "M_bedding"], top="M_bedding")
    P.tube([rect(-.88, -.3, -.56, .3, .16), rect(-.86, -.28, -.58, .28, .23), rect(-.83, -.24, -.61, .24, .26)], ["M_bedding", "M_bedding"], top="M_bedding")
    return P


def plank():   # 码头木板：Config.DockWidth × (PlankDepth − PlankGap) × 0.2，顶面木色、四个侧面深木色（板缝里露出来）
    P = Part()
    lo, hi = (-1.5, -.225, 0), (1.5, .225, .2)
    P.box(lo, hi, "M_wood", "bflkr")   # 先放顶面：材质槽 0 = M_wood（BuildSceneTests 读码头第一块板的 sharedMaterial）
    P.box(lo, hi, "M_wood_dark", "bt")
    return P


MODELS = {"cabin": cabin, "boat": boat, "rock": rock, "tree": tree, "crate": crate, "barrel": barrel,
          "lantern": lantern, "bedframe": bedframe, "bedding": bedding, "plank": plank}


def build(name, part):
    me = bpy.data.meshes.new(name)
    me.from_pydata(part.v, [], part.f)
    order = list(dict.fromkeys(part.m))                                             # 材质槽顺序 = 首次出现顺序
    for m in order:
        me.materials.append(material(m))
    for p, m in zip(me.polygons, part.m):
        p.material_index = order.index(m)
        p.use_smooth = False
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def main():
    dst = arg("--dst", os.path.join(ROOT, "assets_raw"))
    only = arg("--only")
    os.makedirs(dst, exist_ok=True)
    bad = []
    for name, fn in MODELS.items():
        if only and name not in only.split(","):
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        part = fn()
        o = build(name, part)
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.export_scene.gltf(filepath=os.path.join(dst, name + ".glb"), export_format="GLB", use_selection=True)
        n, cap = part.tris(), LIMIT.get(name, 200)
        print(f"MODEL {name}: {n} tris, {len(set(part.m))} mats")
        if n > cap:
            bad.append(f"{name} {n}>{cap}")
    print("MODEL " + (f"FAIL: {bad}" if bad else "PASS"))
    return 1 if bad else 0


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    os._exit(code)
