"""$BLENDER -b -P blender/rig.py -- [--dst assets_raw]
正式骨架 + 简模人偶（胶囊身体 + 大草帽）+ 8 段关键帧动作（原地、烘焙）→ <dst>/character.glb。
骨架以后正式模型沿用：hips/spine/chest/neck/head(+hat)，左右 shoulder/upperarm/forearm/hand，左右 thigh/shin/foot，rod_mount 挂在 hand.R。
角色面朝 -Y（Blender 正视）；姿势角度一律按「骨架空间轴」给，与骨骼自身朝向无关：
  绕 X 正角 = 朝上的骨头前倾 / 朝下的骨头后摆；绕 Z 正角 = 向角色左侧扭；绕 Y 正角 = 朝下的骨头摆向 -X（角色右侧）。"""
import math, os, sys

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FPS = 24
LEG = 0.85          # 髋到踝
STRIDE_DEG = 25     # 走路大腿摆幅 ±25°：一个循环前进 4·LEG·sin25° ≈ 1.44 m，20 帧 → 1.72 m/s（Config.WalkAnimSpeed）
LOOPS = ("idle", "walk", "wait", "reel")

# name: (head, tail, parent)
BONES = {
    "hips": ((0, 0, 0.95), (0, 0, 1.05), None),
    "spine": ((0, 0, 1.05), (0, 0, 1.22), "hips"),
    "chest": ((0, 0, 1.22), (0, 0, 1.42), "spine"),
    "neck": ((0, 0, 1.42), (0, 0, 1.52), "chest"),
    "head": ((0, 0, 1.52), (0, 0, 1.74), "neck"),
    "hat": ((0, 0, 1.72), (0, 0, 1.86), "head"),
}
for s, x in (("L", 1), ("R", -1)):
    BONES.update({
        f"shoulder.{s}": ((0.04 * x, 0, 1.40), (0.18 * x, 0, 1.40), "chest"),
        f"upperarm.{s}": ((0.18 * x, 0, 1.40), (0.20 * x, 0, 1.13), f"shoulder.{s}"),
        f"forearm.{s}": ((0.20 * x, 0, 1.13), (0.21 * x, 0, 0.89), f"upperarm.{s}"),
        f"hand.{s}": ((0.21 * x, 0, 0.89), (0.21 * x, 0, 0.80), f"forearm.{s}"),
        f"thigh.{s}": ((0.10 * x, 0, 0.95), (0.10 * x, 0, 0.52), "hips"),
        f"shin.{s}": ((0.10 * x, 0, 0.52), (0.10 * x, 0, 0.10), f"thigh.{s}"),
        f"foot.{s}": ((0.10 * x, 0, 0.10), (0.10 * x, -0.14, 0.02), f"shin.{s}"),
    })
BONES["rod_mount"] = ((-0.21, 0, 0.83), (-0.21, -0.10, 0.83), "hand.R")

COLORS = {  # 简模人偶色（与 decisions.md 色卡一致）
    "shirt": (0.95, 0.90, 0.81), "pants": (0.24, 0.25, 0.32), "skin": (0.91, 0.71, 0.56),
    "straw": (0.89, 0.76, 0.48), "band": (0.71, 0.32, 0.23), "boots": (0.35, 0.23, 0.16),
}


def arg(flag, default=None):
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return a[a.index(flag) + 1] if flag in a else default


def material(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*COLORS[name], 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    m.diffuse_color = (*COLORS[name], 1)
    return m


# ---------- armature ----------
def build_armature():
    bpy.ops.object.armature_add(location=(0, 0, 0))
    arm = bpy.context.object
    arm.name = arm.data.name = "rig"
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    eb.remove(eb[0])
    for name, (h, t, parent) in BONES.items():
        b = eb.new(name)
        b.head, b.tail = h, t
        b.align_roll((0, -1, 0) if abs(Vector(t)[1] - Vector(h)[1]) < 1e-6 else (0, 0, 1))
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


# ---------- mannequin ----------
def part(op, bone, mat, **kw):
    op(**kw)
    o = bpy.context.object
    o.data.materials.append(mats[mat])
    o.vertex_groups.new(name=bone).add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    return o


def limb(a, b, r, bone, mat, verts=8):
    a, b = Vector(a), Vector(b)
    d = b - a
    o = part(bpy.ops.mesh.primitive_cylinder_add, bone, mat, vertices=verts, radius=r, depth=d.length,
             location=(a + b) / 2)
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return o


def build_body(arm):
    parts = [
        limb((0, 0, 0.84), (0, 0, 1.02), 0.16, "hips", "pants"),
        limb((0, 0, 1.0), (0, 0, 1.24), 0.165, "spine", "shirt"),
        limb((0, 0, 1.22), (0, 0, 1.46), 0.18, "chest", "shirt"),
        limb((0, 0, 1.44), (0, 0, 1.54), 0.05, "neck", "skin", 6),
        part(bpy.ops.mesh.primitive_uv_sphere_add, "head", "skin", segments=8, ring_count=6, radius=0.12, location=(0, 0, 1.63)),
        limb((0, 0, 1.73), (0, 0, 1.755), 0.44, "hat", "straw", 12),          # 大帽檐：俯拍主要看见它
        limb((0, 0, 1.75), (0, 0, 1.78), 0.155, "hat", "band", 10),
        part(bpy.ops.mesh.primitive_cone_add, "hat", "straw", vertices=10, radius1=0.15, radius2=0.07, depth=0.13, location=(0, 0, 1.84)),
    ]
    for s, x in (("L", 1), ("R", -1)):
        parts += [
            limb((0.06 * x, 0, 1.40), (0.18 * x, 0, 1.40), 0.07, f"shoulder.{s}", "shirt", 6),
            limb((0.18 * x, 0, 1.40), (0.20 * x, 0, 1.13), 0.06, f"upperarm.{s}", "shirt", 6),
            limb((0.20 * x, 0, 1.13), (0.21 * x, 0, 0.90), 0.05, f"forearm.{s}", "shirt", 6),
            part(bpy.ops.mesh.primitive_uv_sphere_add, f"hand.{s}", "skin", segments=6, ring_count=4, radius=0.055, location=(0.21 * x, 0, 0.85)),
            limb((0.10 * x, 0, 0.95), (0.10 * x, 0, 0.52), 0.08, f"thigh.{s}", "pants"),
            limb((0.10 * x, 0, 0.52), (0.10 * x, 0, 0.12), 0.065, f"shin.{s}", "pants"),
            part(bpy.ops.mesh.primitive_cube_add, f"foot.{s}", "boots", size=1, location=(0.10 * x, -0.05, 0.05), scale=(0.11, 0.24, 0.1)),
        ]
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = body.data.name = "body"
    for poly in body.data.polygons:
        poly.use_smooth = False   # 平直着色
    body.parent = arm
    body.modifiers.new("Armature", "ARMATURE").object = arm
    return body


# ---------- poses ----------
def q(rx=0, ry=0, rz=0):
    r = math.radians
    return (Quaternion((0, 0, 1), r(rz)) @ Quaternion((0, 1, 0), r(ry)) @ Quaternion((1, 0, 0), r(rx)))


def key(arm, f, pose):
    """pose: {bone: (rx, ry, rz)} 骨架空间角度；特殊键 'lift' = 髋部上下位移（米）。"""
    for pb in arm.pose.bones:
        rest = pb.bone.matrix_local.to_quaternion()
        rot = pose.get(pb.name, (0, 0, 0))
        pb.rotation_quaternion = rest.inverted() @ q(*rot) @ rest
        pb.keyframe_insert("rotation_quaternion", frame=f)
    hips = arm.pose.bones["hips"]
    rest = hips.bone.matrix_local.to_3x3()
    hips.location = rest.inverted() @ Vector((0, 0, pose.get("lift", 0)))
    hips.keyframe_insert("location", frame=f)


def merge(*poses):
    out = {}
    for p in poses:
        for k, v in p.items():
            if k == "lift":
                out[k] = out.get(k, 0) + v
            else:
                a = out.get(k, (0, 0, 0))
                out[k] = tuple(x + y for x, y in zip(a, v))
    return out


REST = {"upperarm.L": (0, -6, 0), "upperarm.R": (0, 6, 0)}   # 手臂离开身体一点
HOLD = merge(REST, {"upperarm.R": (-35, 0, 0), "forearm.R": (-45, 0, 0), "upperarm.L": (-20, 0, 0), "forearm.L": (-50, 0, 0),
                    "chest": (4, 0, 0)})                       # 双手持竿在身前
REEL = merge(HOLD, {"chest": (-16, 0, 0), "spine": (-6, 0, 0), "head": (10, 0, 0), "thigh.L": (-10, 0, 0), "thigh.R": (6, 0, 0)})


def tri(p):  # 三角波：大腿匀角速度 → 脚接近匀速，减少滑步
    return 2 / math.pi * math.asin(math.sin(p))


def sampled(arm, frames, fn, step=2):
    for f in range(0, frames + 1, step):
        key(arm, f + 1, fn(2 * math.pi * f / frames))


def act_idle(arm):
    sampled(arm, 48, lambda p: merge(REST, {"chest": (2 * math.sin(p), 0, 0), "spine": (math.sin(p), 0, 0),
                                            "hat": (0, 3 * math.sin(p), 2 * math.cos(p)), "lift": 0.005 * math.sin(p)}), 4)


def act_walk(arm):
    def pose(p):
        t, sw = tri(p), max(0.0, -math.cos(p))
        sw2 = max(0.0, math.cos(p))
        return merge(REST, {
            "thigh.L": (STRIDE_DEG * t, 0, 0), "thigh.R": (-STRIDE_DEG * t, 0, 0),
            "shin.L": (45 * sw, 0, 0), "shin.R": (45 * sw2, 0, 0),
            "upperarm.L": (-18 * t, 0, 0), "upperarm.R": (18 * t, 0, 0), "forearm.L": (-15, 0, 0), "forearm.R": (-15, 0, 0),
            "chest": (4, 0, 5 * t), "hat": (0, 2 * math.sin(2 * p), 0), "lift": 0.025 * math.cos(2 * p) - 0.02})
    sampled(arm, 20, pose, 1)


def act_wait(arm):
    def pose(p):
        bump = max(0.0, math.sin(p - math.pi)) ** 2   # 后半段调整一下姿势：扭身、歪头、换重心
        return merge(HOLD, {"chest": (1.5 * math.sin(2 * p), 0, 8 * bump), "head": (0, -8 * bump, 10 * bump),
                            "hips": (0, 3 * bump, 0), "hat": (0, 2 * math.sin(2 * p), 0), "lift": -0.01 * bump})
    sampled(arm, 96, pose, 4)


def act_reel(arm):
    sampled(arm, 16, lambda p: merge(REEL, {"upperarm.L": (8 * math.sin(p), 0, 0), "forearm.L": (20 * math.cos(p), 0, 0),
                                            "chest": (2 * math.sin(2 * p), 0, 3 * math.sin(p)), "lift": 0.01 * math.sin(2 * p)}), 2)


def act_keys(arm, keys):
    for f, pose in keys:
        key(arm, f, pose)


def act_cast(arm):   # 0.6 s：蓄力后甩竿
    act_keys(arm, [(1, HOLD),
                   (7, merge(HOLD, {"upperarm.R": (-115, 0, 0), "forearm.R": (-20, 0, 0), "chest": (-12, 0, 18), "spine": (-4, 0, 6), "head": (-6, 0, 0)})),
                   (11, merge(HOLD, {"upperarm.R": (-20, 0, 0), "forearm.R": (15, 0, 0), "chest": (14, 0, -8), "spine": (4, 0, 0)})),
                   (15, HOLD)])


def act_bite(arm):   # 咬钩一惊
    act_keys(arm, [(1, HOLD),
                   (4, merge(HOLD, {"chest": (-12, 0, 0), "head": (-14, 0, 0), "upperarm.R": (-30, 0, 0), "upperarm.L": (-30, 0, 0),
                                    "shoulder.L": (0, 0, 0), "lift": 0.05})),
                   (12, REEL)])


def act_catch(arm):  # 把鱼举起来
    up = merge(REST, {"upperarm.L": (-165, -10, 0), "upperarm.R": (-165, 10, 0), "forearm.L": (-15, 0, 0), "forearm.R": (-15, 0, 0),
                      "chest": (-8, 0, 0), "head": (-15, 0, 0)})
    act_keys(arm, [(1, REEL), (10, up), (16, merge(up, {"lift": 0.06})), (22, up), (29, up)])


def act_lose(arm):   # 失落
    slump = merge(REST, {"upperarm.R": (-15, 0, 0), "forearm.R": (-20, 0, 0), "chest": (18, 0, 0), "spine": (6, 0, 0), "head": (28, 0, 0),
                         "hat": (8, 0, 0), "lift": -0.05})
    act_keys(arm, [(1, REEL), (12, slump), (20, merge(slump, {"head": (0, 0, 10)})), (29, slump)])


ACTIONS = {"idle": act_idle, "walk": act_walk, "cast": act_cast, "wait": act_wait,
           "bite": act_bite, "reel": act_reel, "catch": act_catch, "lose": act_lose}


def main():
    dst = arg("--dst", os.path.join(ROOT, "assets_raw"))
    os.makedirs(dst, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    global mats
    mats = {n: material(n) for n in COLORS}
    arm = build_armature()
    body = build_body(arm)
    arm.animation_data_create()
    for name, fn in ACTIONS.items():
        arm.animation_data.action = None
        fn(arm)
        a = arm.animation_data.action
        a.name, a.use_fake_user = name, True
    arm.animation_data.action = None
    for pb in arm.pose.bones:   # 回到静止姿势再导出
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    out = os.path.join(dst, "character.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True, export_animation_mode="ACTIONS")
    n = len(body.data.polygons)
    ok = n < 10000 and len(bpy.data.actions) == len(ACTIONS)
    print(f"RIG {'PASS' if ok else 'FAIL'}: {len(arm.data.bones)} bones, {n} faces, {len(bpy.data.actions)} actions -> {out}")
    return 0 if ok else 1


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    os._exit(code)
