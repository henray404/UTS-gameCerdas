# The Warden pass 2: strip the cartoon uniform and face from warden_pilot.blend, then add horror features.
# Headless (renders Art/Blender/renders/warden_v2_sheet.png, saves warden_v2.blend, exports export/warden.fbx):
#   blender -b Art/Blender/warden_pilot.blend --python Art/Blender/scripts/warden_sculpt.py
# GUI, one step at a time:
#   open -na /Applications/Blender.app --args "$PWD/Art/Blender/warden_pilot.blend" --python "$PWD/Art/Blender/scripts/warden_sculpt.py"
# The source model is built from separate primitive islands, so parts are picked by material, main bone and size.
import math, os
import bpy, bmesh
from mathutils import Vector, Matrix

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))


def objects():
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    return rig, mesh


def islands(mesh):
    """Return (verts, face count, material, main bone, min corner, max corner) for every connected piece."""
    bm = bmesh.new(); bm.from_mesh(mesh.data); bm.faces.ensure_lookup_table()
    mats = [m.name if m else "" for m in mesh.data.materials]
    groups = {vg.index: vg.name for vg in mesh.vertex_groups}
    seen = set(); result = []
    for f in bm.faces:
        if f.index in seen: continue
        stack, faces = [f], []; seen.add(f.index)
        while stack:
            g = stack.pop(); faces.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); stack.append(h)
        verts = {v.index for g in faces for v in g.verts}
        cos = [mesh.data.vertices[i].co for i in verts]
        lo = Vector((min(c.x for c in cos), min(c.y for c in cos), min(c.z for c in cos)))
        hi = Vector((max(c.x for c in cos), max(c.y for c in cos), max(c.z for c in cos)))
        dv = mesh.data.vertices[next(iter(verts))]
        bone = groups.get(max(dv.groups, key=lambda x: x.weight).group, "") if dv.groups else ""
        result.append((verts, len(faces), mats[faces[0].material_index], bone, lo, hi))
    bm.free()
    return result


def delete(mesh, test):
    doomed = set()
    for verts, faces, mat, bone, lo, hi in islands(mesh):
        if test(faces, mat, bone, lo, hi): doomed |= verts
    bm = bmesh.new(); bm.from_mesh(mesh.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in doomed], context="VERTS")
    bm.to_mesh(mesh.data); bm.free(); mesh.data.update()


def strip_uniform():
    _, mesh = objects()
    accessory = ("gold", "navylight", "silver")
    delete(mesh, lambda n, m, b, lo, hi:
           any(a in m for a in accessory)
           or (b == "chest" and n < 82)                       # badge, radio, pocket, antenna
           or (b == "pelvis" and n in (18, 54)))              # belt strap and holster


def strip_face():
    _, mesh = objects()
    delete(mesh, lambda n, m, b, lo, hi:
           b.startswith(("face_eye", "face_pupil", "face_brow"))
           or "paper" in m or "brown" in m
           or (b == "head" and "black" in m)                                   # nostrils
           or (b == "head" and "grey." in m and n == 160 and hi.x - lo.x < .12)  # eye sockets
           or (b == "head" and "greydark" in m and lo.z > 1.66))               # hair tufts


def transform_island(mesh, test, matrix_for):
    for verts, n, m, b, lo, hi in islands(mesh):
        if not test(n, m, b, lo, hi): continue
        mtx = matrix_for(verts, lo, hi)
        for i in verts: mesh.data.vertices[i].co = mtx @ mesh.data.vertices[i].co
    mesh.data.update()


def about(pivot, scale):
    return Matrix.Translation(pivot) @ Matrix.Diagonal((*scale, 1)) @ Matrix.Translation(-pivot)


def reshape_head():
    _, mesh = objects()
    head_centre = Vector((0, -.12, 1.58))
    # Horns: stretch 1.7x away from the point nearest the skull and thin them, so they become long and sharp.
    def horn(verts, lo, hi):
        base = min((mesh.data.vertices[i].co for i in verts), key=lambda c: (c - head_centre).length)
        tip = max((mesh.data.vertices[i].co for i in verts), key=lambda c: (c - base).length)
        axis = (tip - base).normalized()
        along = Matrix([[axis[r] * axis[c] for c in range(3)] for r in range(3)])  # outer product
        stretch = Matrix.Identity(3) + .7 * along - .25 * (Matrix.Identity(3) - along)
        return Matrix.Translation(base) @ stretch.to_4x4() @ Matrix.Translation(-base)
    transform_island(mesh, lambda n, m, b, lo, hi: "cream" in m and b == "head", horn)
    # Snout: longer and narrower, like a skull.
    transform_island(mesh, lambda n, m, b, lo, hi: "greydark" in m and b == "head" and n == 160,
                     lambda v, lo, hi: about((lo + hi) / 2, (.72, 1.35, .82)))
    # Mouth slit: wider grin, moved onto the front of the longer snout (it would otherwise poke out under the chin).
    snout = next((lo, hi) for v, n, m, b, lo, hi in islands(mesh) if "greydark" in m and b == "head" and n == 160)
    target = Vector((0, snout[0].y + .03, snout[0].z + .035))
    transform_island(mesh, lambda n, m, b, lo, hi: b.startswith("face_mouth") or "FaceDark" in m,
                     lambda v, lo, hi: Matrix.Translation(target - (lo + hi) / 2) @ about((lo + hi) / 2, (1.5, 1, 1)))
    # Torso and shoulders: narrower so the long arms read as gaunt.
    transform_island(mesh, lambda n, m, b, lo, hi: b == "chest" and n == 82, lambda v, lo, hi: about((lo + hi) / 2, (.8, .85, 1)))
    transform_island(mesh, lambda n, m, b, lo, hi: b.startswith("upper_arm") and n == 160,
                     lambda v, lo, hi: about((lo + hi) / 2, (.72, .8, .9)))


def add_part(mesh, bm, bone, material):
    """Join a bmesh (built in mesh-local space) into the mesh, fully weighted to one bone."""
    data = bpy.data.meshes.new("part"); bm.to_mesh(data); bm.free()
    slot = next(i for i, m in enumerate(mesh.data.materials) if m and material in m.name)
    data.materials.append(mesh.data.materials[slot])
    part = bpy.data.objects.new("part", data); bpy.context.scene.collection.objects.link(part)
    part.matrix_world = mesh.matrix_world.copy()
    part.vertex_groups.new(name=bone).add(range(len(data.vertices)), 1.0, "REPLACE")
    for o in bpy.context.selected_objects: o.select_set(False)
    part.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.join()


def add_eyes():
    _, mesh = objects()
    # Pinpoint eyes on the FaceDark material so MonsterSkin makes them glow red in game.
    for side, bone in ((-1, "face_eye.R"), (1, "face_eye.L")):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=.016,
                                  matrix=Matrix.Translation((side * .07, -.27, 1.555)))
        add_part(mesh, bm, bone, "FaceDark")


def add_claws():
    _, mesh = objects()
    for verts, n, m, b, lo, hi in islands(mesh):
        if not (b.startswith("hand") and n == 160 and hi.x - lo.x > .15): continue  # palm, not thumb
        side = 1 if lo.x > 0 else -1
        centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z + .05))
        bm = bmesh.new()
        for k, (dx, dy) in enumerate(((-.05, -.02), (0, -.04), (.05, -.02))):
            tilt = Matrix.Rotation(math.radians(180 + 8 * side * (k - 1)), 4, "Y") @ Matrix.Rotation(math.radians(-12), 4, "X")
            bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=.02, radius2=.002, depth=.26,
                                  matrix=Matrix.Translation(centre + Vector((dx, dy, -.1))) @ tilt)
        add_part(mesh, bm, b, "grey.")


STEPS = [("1/5 Aksesori seragam dibuang", strip_uniform), ("2/5 Wajah kartun dibuang", strip_face),
         ("3/5 Tanduk dipanjangkan, moncong jadi tengkorak", reshape_head),
         ("4/5 Mata titik merah ditambah", add_eyes), ("5/5 Cakar ditambah", add_claws)]


def play(clip, frame=None):
    rig, _ = objects()
    action = next(a for a in bpy.data.actions if a.name.endswith("|" + clip) and "Rig" in a.name)
    if rig.animation_data is None: rig.animation_data_create()
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = int(action.frame_range[0]), int(action.frame_range[1])
    scene.frame_set(frame if frame is not None else scene.frame_start)


def rest(rig):
    if rig.animation_data: rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)


CLIPS = ("Idle", "Walk", "Run", "LookAround", "FaceActing")  # GuardEncounterBuilder looks these up by exact name


def save_and_export():
    rig, mesh = objects(); rest(rig)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "warden_v2.blend"))
    os.makedirs(os.path.join(OUT, "export"), exist_ok=True)
    # One NLA strip per clip so the FBX takes are named exactly like the original model ("Idle", not "Rig|Idle").
    if rig.animation_data is None: rig.animation_data_create()
    rig.animation_data.action = None
    for track in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(track)
    for clip in CLIPS:
        action = next(a for a in bpy.data.actions if a.name.endswith("|" + clip) and "Rig" in a.name)
        track = rig.animation_data.nla_tracks.new(); track.name = clip
        track.strips.new(clip, int(action.frame_range[0]), action)
    # Only the model goes out: the scene's preview cameras and lights must not end up inside the guard prefab.
    keep = [rig, mesh] + ([rig.parent] if rig.parent else [])
    for o in bpy.data.objects: o.select_set(o in keep)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "export", "warden.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH", "EMPTY"}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")


def render_sheet():
    import numpy as np
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    s = scene.display.shading; s.light = "STUDIO"; s.color_type = "MATERIAL"; s.show_shadows = True; s.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 420, 640
    scene.world = scene.world or bpy.data.worlds.new("Night"); scene.world.color = (.012, .014, .02)
    cam = bpy.data.objects.get("Cam") or bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    if cam.name not in scene.collection.objects: scene.collection.objects.link(cam)
    scene.camera = cam; cam.data.type = "ORTHO"
    rig, mesh = objects()
    for slot in mesh.material_slots:
        if slot.material: slot.material.diffuse_color = (1, .14, .07, 1) if "FaceDark" in slot.material.name else (.09, .085, .1, 1)
    head = rig.matrix_world @ rig.data.bones["head"].head_local
    parts_dir = os.path.join(OUT, "renders", "_parts"); os.makedirs(parts_dir, exist_ok=True)
    parts = []

    def shot(name, side=False, z=1.1, size=2.5):
        cam.data.ortho_scale = size
        cam.location = (6, head.y, z) if side else (head.x, -6, z)
        cam.rotation_euler = (math.radians(90), 0, math.radians(90) if side else 0)
        scene.render.filepath = os.path.join(parts_dir, name + ".png"); bpy.ops.render.render(write_still=True)
        parts.append(scene.render.filepath)

    rest(rig); shot("v2_front"); shot("v2_side", True)
    shot("v2_face", z=head.z + .15, size=.75)
    play("Walk", 10); shot("v2_walk", True); rest(rig)
    imgs = [bpy.data.images.load(p) for p in parts]
    w, h = imgs[0].size
    sheet = np.zeros((h, w * len(imgs), 4), dtype=np.float32)
    for i, img in enumerate(imgs): sheet[:, i * w:(i + 1) * w] = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    out = bpy.data.images.new("sheet", w * len(imgs), h, alpha=True)
    out.pixels = sheet.ravel(); out.filepath_raw = os.path.join(OUT, "renders", "warden_v2_sheet.png"); out.file_format = "PNG"; out.save()


def run_gui():
    state = {"i": 0}

    def view3d():
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                if area.type == "VIEW_3D": return window, area, next(r for r in area.regions if r.type == "WINDOW")
        return None, None, None

    def tick():
        window, area, region = view3d()
        if area is None: return .5
        i = state["i"]
        with bpy.context.temp_override(window=window, area=area, region=region):
            if i == 0:
                space = area.spaces.active
                space.shading.type = "SOLID"; space.shading.color_type = "MATERIAL"
                space.shading.background_type = "VIEWPORT"; space.shading.background_color = (.012, .014, .02)
                space.overlay.show_floor = False
                rest(objects()[0]); bpy.ops.view3d.view_axis(type="FRONT"); bpy.ops.view3d.view_all()
                area.header_text_set("The Warden pass 2  |  mulai dari hasil pilot")
            elif i <= len(STEPS):
                title, step = STEPS[i - 1]; step()
                area.header_text_set("The Warden pass 2  |  " + title)
            else:
                play("Walk"); bpy.ops.screen.animation_play()
                rig, _ = objects(); action = rig.animation_data.action
                rig.animation_data.action = None
                bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "warden_v2.blend"))
                rig.animation_data.action = action
                area.header_text_set("Selesai. Tersimpan di Art/Blender/warden_v2.blend")
                return None
        area.tag_redraw(); state["i"] += 1
        return 2.5

    bpy.app.timers.register(tick, first_interval=1.5)


if bpy.app.background:
    for _, step in STEPS: step()
    render_sheet(); save_and_export()
else:
    run_gui()
