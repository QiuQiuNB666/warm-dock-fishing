"""Phase 2: $BLENDER -b -P blender/clean.py -- [--src assets_raw] [--dst assets] [--placeholders <dir>]
glb -> apply transforms -> origin bottom-center -> M_<name> + textures <=1024 -> AN_idle/AN_walk -> FBX,
then re-import every FBX and assert; prints one line CLEAN PASS / CLEAN FAIL: <first reason>.
--placeholders <dir>: only write test glbs (rock/boat/cabin/character.glb in <dir>, split character in <dir>/split)."""
import glob, math, os, shutil, sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STATIC = ("rock", "boat", "cabin")
ANIMS = ("idle", "walk")
TEX_MAX = 1024


def arg(flag, default=None):
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return a[a.index(flag) + 1] if flag in a else default


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def select(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def tex_material(name, size=2048):
    img = bpy.data.images.new(name + "_tex", size, size)
    img.generated_type = "COLOR_GRID"
    img.pack()
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def export_glb(path, objs, mode="ACTIONS"):
    select(objs)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_animation_mode=mode)


# ---------- placeholders ----------
def make_placeholders(d):
    os.makedirs(os.path.join(d, "split"), exist_ok=True)
    for i, name in enumerate(STATIC):
        reset()
        add = (bpy.ops.mesh.primitive_ico_sphere_add, bpy.ops.mesh.primitive_cube_add,
               bpy.ops.mesh.primitive_cylinder_add)[i]
        add(location=(0.3, -0.2, 0.7))  # deliberately off-origin / rotated / scaled
        o = bpy.context.object
        o.rotation_euler = (0.2, 0, 0.5)
        o.scale = (1.5, 1, 0.8)
        o.data.materials.append(tex_material("Material.%03d" % i))
        export_glb(os.path.join(d, name + ".glb"), [o])
        shutil.copy(os.path.join(d, name + ".glb"), os.path.join(d, "split"))

    reset()
    bpy.ops.object.armature_add(location=(0.2, 0.1, 0.4))
    arm = bpy.context.object
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    root = eb[0]
    root.name, root.head, root.tail = "root", (0, 0, 0), (0, 0, 0.8)
    spine = eb.new("spine")
    spine.head, spine.tail, spine.parent = (0, 0, 0.8), (0, 0, 1.6), root
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=1.6, location=(0.2, 0.1, 1.2))
    body = bpy.context.object
    body.data.materials.append(tex_material("Material"))
    for bone in ("root", "spine"):
        body.vertex_groups.new(name=bone)
    for v in body.data.vertices:
        body.vertex_groups["spine" if v.co.z > 0 else "root"].add([v.index], 1.0, "REPLACE")
    body.parent = arm
    body.matrix_parent_inverse = arm.matrix_world.inverted()
    body.modifiers.new("Armature", "ARMATURE").object = arm

    acts = {}
    for name, length, amp in (("idle", 24, 0.1), ("walk", 20, 0.4)):
        arm.animation_data_create().action = None
        pb = arm.pose.bones["spine"]
        pb.rotation_mode = "XYZ"
        for f, a in ((1, 0), (1 + length // 2, amp), (1 + length, 0)):  # closed loop
            pb.rotation_euler = (a, 0, 0)
            pb.keyframe_insert("rotation_euler", frame=f)
        acts[name] = arm.animation_data.action
        acts[name].name = name
        acts[name].use_fake_user = True
    arm.animation_data.action = None
    export_glb(os.path.join(d, "character.glb"), [arm, body])
    for name, act in acts.items():
        arm.animation_data.action = act
        for other in acts.values():
            other.use_fake_user = other is act
        export_glb(os.path.join(d, "split", f"character_{name}.glb"), [arm, body], "ACTIVE_ACTIONS")
    print(f"PLACEHOLDERS written to {d}")


# ---------- clean ----------
def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    shapes = {pb.custom_shape for o in new if o.type == "ARMATURE" for pb in o.pose.bones} - {None}
    for o in new:  # importer adds an icosphere as bone display shape; it is not part of the model
        if o.type == "ARMATURE":
            for pb in o.pose.bones:
                pb.custom_shape = None
    keep = [o for o in new if o not in shapes]
    for s in shapes:
        bpy.data.objects.remove(s)
    return keep


def flatten(objs):
    """Unparent everything except mesh->armature, drop empties, apply all transforms."""
    for o in objs:
        if o.parent and not (o.type == "MESH" and o.parent.type == "ARMATURE"):
            mw = o.matrix_world.copy()
            o.parent = None
            o.matrix_world = mw
    for o in [o for o in objs if o.type == "EMPTY"]:
        bpy.data.objects.remove(o)
    objs = [o for o in objs if o.name in bpy.data.objects]
    select(objs)
    # ponytail: scale on an animated armature does not rescale bone location keys; Tripo exports scale 1
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return objs


def bottom_center(meshes):
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    lo = Vector((min(p[i] for p in pts) for i in range(3)))
    hi = Vector((max(p[i] for p in pts) for i in range(3)))
    return Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z)), (hi - lo).length


def fix_materials(name):
    mats = {m for o in bpy.data.objects for m in getattr(o.data, "materials", []) if m}
    for i, m in enumerate(sorted(mats, key=lambda m: m.name)):
        m.name = f"M_{name}" if i == 0 else f"M_{name}_{i}"
    for img in bpy.data.images:
        w, h = img.size
        if max(w, h) > TEX_MAX:
            k = TEX_MAX / max(w, h)
            img.scale(max(1, int(w * k)), max(1, int(h * k)))
            img.pack()
    bpy.data.orphans_purge(do_recursive=True)


def export_fbx(path, objs, armature):
    select(objs)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        # no bake_space_transform: Unity side sets ModelImporter.bakeAxisConversion=true for every FBX
        path_mode="COPY", embed_textures=True, add_leaf_bones=False,
        bake_anim=armature, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True)


def clean_static(src, dst, name):
    reset()
    objs = flatten(import_glb(os.path.join(src, name + ".glb")))
    select(objs)
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.object
    o.name = o.data.name = f"SM_{name}"
    pivot, _ = bottom_center([o])
    o.data.transform(Matrix.Translation(-pivot))
    fix_materials(name)
    export_fbx(os.path.join(dst, f"SM_{name}.fbx"), [o], False)


def close_loop(act):
    for fc in act.fcurves:  # ponytail: snaps last key to first; real fix is a crossfade if Tripo loops pop
        k = fc.keyframe_points
        if len(k) > 1:
            k[-1].co.y = k[0].co.y
            k[-1].handle_left.y = k[-1].handle_right.y = k[0].co.y
            fc.update()


def clean_character(src, dst):
    reset()
    files = [os.path.join(src, "character.glb")]
    if not os.path.exists(files[0]):
        files = sorted(glob.glob(os.path.join(src, "character_*.glb")))
    objs = None
    for f in files:
        before = set(bpy.data.actions)
        new = import_glb(f)
        tag = os.path.basename(f)[len("character_"):-4]  # split files name actions generically ("Animation")
        for a in set(bpy.data.actions) - before:
            a.name = f"{tag}_{a.name}" if tag else a.name
        if objs is None:
            objs = flatten(new)
        else:  # split input: keep only its actions
            for o in new:
                bpy.data.objects.remove(o)
    arm = next(o for o in objs if o.type == "ARMATURE")
    meshes = [o for o in objs if o.type == "MESH"]
    arm.name, arm.data.name = "SK_character", "SK_character"
    for i, m in enumerate(meshes):
        m.name = "SK_character_body" if i == 0 else f"SK_character_body_{i}"
    pivot, _ = bottom_center(meshes)
    shift = Matrix.Translation(-pivot)  # world == local here: flatten() left identity matrices
    arm.data.transform(shift)
    for m in meshes:
        m.data.transform(shift)

    if arm.animation_data:
        arm.animation_data.action = None
        for t in list(arm.animation_data.nla_tracks):
            arm.animation_data.nla_tracks.remove(t)
    kept = {}
    for act in list(bpy.data.actions):
        tag = next((a for a in ANIMS if a in act.name.lower()), None)
        if tag and tag not in kept:
            act.name, act.use_fake_user = f"AN_{tag}", True
            close_loop(act)
            kept[tag] = act
        else:
            bpy.data.actions.remove(act)
    fix_materials("character")
    export_fbx(os.path.join(dst, "SK_character.fbx"), [arm] + meshes, True)
    return sorted(kept)


# ---------- verify ----------
def verify(dst):
    reset()
    for p in sorted(glob.glob(os.path.join(dst, "*.fbx"))):
        bpy.ops.import_scene.fbx(filepath=p)
    names = {os.path.basename(p)[:-4] for p in glob.glob(os.path.join(dst, "*.fbx"))}
    want = {f"SM_{n}" for n in STATIC} | {"SK_character"}
    if names != want:
        return f"fbx set {sorted(names)}"
    for o in bpy.data.objects:
        if not o.name.startswith(("SM_", "SK_")):
            return f"object name {o.name}"
        if any(abs(s - 1) > 1e-4 for s in o.scale) or any(abs(r) > 1e-4 for r in o.rotation_euler):
            return f"{o.name} scale={tuple(round(s, 4) for s in o.scale)} rot={tuple(round(r, 4) for r in o.rotation_euler)}"
    for m in bpy.data.materials:
        if not m.name.startswith("M_") or "." in m.name:
            return f"material name {m.name}"
        if m.users == 0:
            return f"orphan material {m.name}"
    for img in bpy.data.images:
        if max(img.size) > TEX_MAX:
            return f"texture {img.name} {tuple(img.size)}"
    for n in want:
        meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(n)]
        if not meshes:
            return f"{n}: no mesh"
        pivot, size = bottom_center(meshes)
        if pivot.length > 1e-3 * max(size, 1):
            return f"{n}: origin off bottom-center by {tuple(round(c, 4) for c in pivot)}"
    takes = {a.name.rsplit("|", 1)[-1] for a in bpy.data.actions}
    if takes != {"AN_idle", "AN_walk"}:
        return f"actions {sorted(a.name for a in bpy.data.actions)}"
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.animation_data_create()
    checked = 0
    for act in bpy.data.actions:
        if not any(fc.data_path.startswith("pose.bones") for fc in act.fcurves):
            continue
        checked += 1
        arm.animation_data.action = act
        if getattr(arm.animation_data, "action_slot", 1) is None and act.slots:
            arm.animation_data.action_slot = act.slots[0]
        s, e = (int(round(f)) for f in act.frame_range)
        poses = []
        for f in (s, e):
            bpy.context.scene.frame_set(f)
            poses.append([pb.matrix_basis.copy() for pb in arm.pose.bones])
        diff = max(abs(a[i][j] - b[i][j]) for a, b in zip(*poses) for i in range(4) for j in range(4))
        if diff > 1e-3:
            return f"{act.name}: first/last frame differ by {diff:.4f}"
    return None if checked == 2 else f"loop check ran on {checked} actions"


def main():
    ph = arg("--placeholders")
    if ph:
        make_placeholders(ph)
        return 0
    src = arg("--src", os.path.join(ROOT, "assets_raw"))
    dst = arg("--dst", os.path.join(ROOT, "assets"))
    os.makedirs(dst, exist_ok=True)
    try:
        for n in STATIC:
            clean_static(src, dst, n)
        kept = clean_character(src, dst)
        err = None if kept == list(ANIMS) else f"character actions found {kept}"
        err = err or verify(dst)
    except Exception as e:
        err = f"{type(e).__name__}: {e}"
    print("CLEAN " + (f"FAIL: {err}" if err else "PASS"))
    return 1 if err else 0


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    os._exit(code)  # sys.exit inside -P is swallowed by some Blender builds
