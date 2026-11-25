# Pilot The Warden: reshape the Bogo cast model into a gaunt, long-armed creature (plans/monster-character-design.md).
# Headless (renders a preview sheet and exports FBX):
#   blender -b --factory-startup --python Art/Blender/scripts/warden_pilot.py
# GUI (runs the same steps one by one so the process can be watched):
#   open -na /Applications/Blender.app --args --factory-startup --python Art/Blender/scripts/warden_pilot.py
# Rig stays Generic with the same bone names and the five clips, so GuardEncounterBuilder can read the result.
import math, os
import bpy
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SOURCE = os.path.join(ROOT, "Assets/SneakHouse/Models/Actors/01_bogo_guard.fbx")
OUT = os.path.join(ROOT, "Art/Blender")

# Pose-space reshape: bone -> (scale x, scale along bone, scale z). Tuned by eye against the concept v2 sheet.
TORSO = {"pelvis": (.78, 1, .8), "spine": (.62, 1.1, .68), "chest": (.72, 1.05, .72)}
ARMS = {"upper_arm.L": (.62, 1.38, .62), "upper_arm.R": (.62, 1.38, .62),
        "forearm.L": (.58, 1.38, .58), "forearm.R": (.58, 1.38, .58),
        "hand.L": (.8, 1.45, .8), "hand.R": (.8, 1.45, .8)}
LEGS = {"thigh.L": (.7, 1.08, .7), "thigh.R": (.7, 1.08, .7), "shin.L": (.62, 1.08, .62), "shin.R": (.62, 1.08, .62)}
HEAD = {"neck": (.7, 2.2, .7), "head": (.56, .56, .56), "face_eye.L": (.55,) * 3, "face_eye.R": (.55,) * 3}
NECK_PITCH = 28  # degrees; head juts forward


def objects():
    rig = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    return rig, mesh


def load():
    for o in list(bpy.context.scene.objects):
        if o.type in ("MESH", "ARMATURE", "EMPTY"): bpy.data.objects.remove(o)  # keep cameras and lights
    bpy.ops.import_scene.fbx(filepath=SOURCE)
    rig, mesh = objects()
    # Approximates MonsterSkin: dark body, FaceDark slots glowing red.
    for slot in mesh.material_slots:
        if slot.material: slot.material.diffuse_color = (1, .14, .07, 1) if "FaceDark" in slot.material.name else (.09, .085, .1, 1)
    rest_pose(rig)
    for b in rig.data.bones: b["inherit_scale_saved"] = b.inherit_scale; b.inherit_scale = "NONE"  # each bone gets exactly its own scale


def rest_pose(rig):
    if rig.animation_data: rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)


def scale(table):
    rig, _ = objects()
    for name, s in table.items(): rig.pose.bones[name].scale = s


def head():
    scale(HEAD)
    neck = objects()[0].pose.bones["neck"]; neck.rotation_mode = "XYZ"; neck.rotation_euler = (math.radians(NECK_PITCH), 0, 0)


def bake():
    # Bake the pose into the mesh, then make it the new rest pose; clips keep working on top of it.
    rig, mesh = objects()
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = mesh
    mod = next(m for m in mesh.modifiers if m.type == "ARMATURE"); name = mod.name
    bpy.ops.object.modifier_apply(modifier=name)
    bpy.context.view_layer.objects.active = rig; rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in rig.data.bones: b.inherit_scale = b["inherit_scale_saved"]; del b["inherit_scale_saved"]
    new = mesh.modifiers.new(name, "ARMATURE"); new.object = rig


def play(clip, frame=None):
    rig, _ = objects()
    action = next(a for a in bpy.data.actions if a.name.endswith("|" + clip) and "Rig" in a.name)
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = int(action.frame_range[0]), int(action.frame_range[1])
    scene.frame_set(frame if frame is not None else scene.frame_start)


def save():
    rig, _ = objects()
    saved = rig.animation_data.action
    rest_pose(rig)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "warden_pilot.blend"))
    rig.animation_data.action = saved


def export():
    os.makedirs(os.path.join(OUT, "export"), exist_ok=True)
    rest_pose(objects()[0])
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "export", "warden.fbx"), add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")


STEPS = [("1/6 Model dasar diimpor", load), ("2/6 Badan dikuruskan", lambda: scale(TORSO)),
         ("3/6 Lengan diperpanjang sampai lutut", lambda: scale(ARMS)), ("4/6 Kaki dikuruskan", lambda: scale(LEGS)),
         ("5/6 Kepala dikecilkan, leher maju, mata titik", head), ("6/6 Bentuk dikunci, clip Walk diputar", bake)]


# ---------- headless: render a before/after sheet ----------
def render_sheet():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"; shading.color_type = "MATERIAL"; shading.show_shadows = True; shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 420, 640
    world = bpy.data.worlds.new("Night"); scene.world = world; world.color = (.012, .014, .02)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 2.5
    parts_dir = os.path.join(OUT, "renders", "_parts"); os.makedirs(parts_dir, exist_ok=True)
    parts = []

    def shot(name, side=False):
        cam.location = (6, 0, 1.1) if side else (0, -6, 1.1)
        cam.rotation_euler = (math.radians(90), 0, math.radians(90) if side else 0)
        scene.render.filepath = os.path.join(parts_dir, name + ".png"); bpy.ops.render.render(write_still=True)
        parts.append(scene.render.filepath)

    load(); shot("0_before")
    for _, step in STEPS[1:]: step()
    shot("1_front"); shot("2_side", True)
    play("Walk", 10); shot("3_walk_side", True)
    stitch(parts, os.path.join(OUT, "renders", "warden_sheet.png"))


def stitch(paths, out):
    imgs = [bpy.data.images.load(p) for p in paths]
    w, h = imgs[0].size
    sheet = np.zeros((h, w * len(imgs), 4), dtype=np.float32)
    for i, img in enumerate(imgs): sheet[:, i * w:(i + 1) * w] = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    result = bpy.data.images.new("sheet", w * len(imgs), h, alpha=True)
    result.pixels = sheet.ravel(); result.filepath_raw = out; result.file_format = "PNG"; result.save()


# ---------- GUI: same steps on a timer so each change is visible ----------
def view3d():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "VIEW_3D":
                return window, area, next(r for r in area.regions if r.type == "WINDOW")
    return None, None, None


def frame_front(window, area, region):
    space = area.spaces.active
    space.shading.type = "SOLID"; space.shading.color_type = "MATERIAL"
    space.shading.background_type = "VIEWPORT"; space.shading.background_color = (.012, .014, .02)
    space.overlay.show_floor = False; space.overlay.show_axis_x = space.overlay.show_axis_y = False
    with bpy.context.temp_override(window=window, area=area, region=region):
        bpy.ops.view3d.view_axis(type="FRONT")
        bpy.ops.view3d.view_all()


def run_gui():
    state = {"i": 0}

    def tick():
        window, area, region = view3d()
        if area is None: return .5
        i = state["i"]
        if i >= len(STEPS):
            play("Walk")
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.screen.animation_play()
            save()
            area.header_text_set("Selesai. Tersimpan di Art/Blender/warden_pilot.blend")
            return None
        title, step = STEPS[i]
        with bpy.context.temp_override(window=window, area=area, region=region):
            step()
        if i == 0: frame_front(window, area, region)
        area.header_text_set("The Warden pilot  |  " + title)
        area.tag_redraw()
        state["i"] += 1
        return 2.5

    bpy.app.timers.register(tick, first_interval=1.5)


if bpy.app.background:
    render_sheet(); save(); export()
else:
    run_gui()
