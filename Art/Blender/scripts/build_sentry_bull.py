"""Sentry Bull / Banteng Satpam: mascot-horror security guard, built procedurally in Blender 4.2 LTS+ (checked on 5.3).

Scripting workspace: open this file and press Run Script. Headless:
    blender -b --factory-startup --python build_sentry_bull.py -- [--render OUT_DIR] [--save FILE.blend] [--preview]
  --render  renders Cam_Hero, Cam_Face_Closeup and Cam_Turnaround (night) plus a day turnaround into OUT_DIR
  --preview lower samples and half resolution for quick checks
  --save    writes the built scene to a .blend file

Every part is authored in the WebGL reference's space (Y up, +Z forward, metres) and mapped once to Blender
(Z up, -Y forward) through C4, so numbers can be compared line by line with sentry_bull_showcase.html.
Timeline: frame 1 = Day Shift (stiff mascot), frame 72 = Night Shift (hunched horror), held to frame 240.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

# ----------------------------------------------------------------------------- settings
USE_EEVEE = False          # True: fast viewport-style renders with EEVEE (Next) instead of Cycles
NIGHT_SHIFT = 0.85         # corruption on the hero frame: 0 = day mascot, 1 = full night horror
SEED = 404
HERO_SAMPLES = 256
FRAME_DAY, FRAME_NIGHT, FRAME_END = 1, 72, 240
FINGER_STRETCH = 1.22      # gaunt fingers, longer than the reference

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
rng = random.Random(SEED)

# reference space (x, y-up, z-forward) -> Blender (x, -z, y)
C4 = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def arg(flag, default=None):
    return ARGS[ARGS.index(flag) + 1] if flag in ARGS and ARGS.index(flag) + 1 < len(ARGS) else default


def lerp(a, b, t):
    return a + (b - a) * t


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(rx=0.0, ry=0.0, rz=0.0):
    # three.js Euler 'XYZ' is Rx @ Ry @ Rz, which is mathutils order 'ZYX'
    return Euler((rx, ry, rz), "ZYX").to_matrix().to_4x4()


def S(sx=1.0, sy=1.0, sz=1.0):
    return Matrix.Diagonal((sx, sy, sz, 1.0))


def TRS(p=(0, 0, 0), r=(0, 0, 0), s=(1, 1, 1)):
    return T(*p) @ R(*r) @ S(*s)


def to_blender(p):
    return (C4 @ Vector((*p, 1.0))).to_3d()


# ----------------------------------------------------------------------------- scene reset and collections
def reset_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)
    for store in (bpy.data.meshes, bpy.data.curves, bpy.data.metaballs, bpy.data.armatures, bpy.data.lights,
                  bpy.data.cameras, bpy.data.materials, bpy.data.textures, bpy.data.actions):
        for block in list(store):
            store.remove(block)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = FRAME_DAY, FRAME_END
    return scene


SCENE = reset_scene()
COLL = {}
for _name in ("01_Character", "02_Uniform_Props", "03_Rig_Morphs", "04_Environment", "05_Lighting_Cameras", "_build_scratch"):
    COLL[_name] = bpy.data.collections.new(_name)
    SCENE.collection.children.link(COLL[_name])
CHAR, PROPS, RIGC, ENV, LIGHTS, SCRATCH = (COLL[n] for n in COLL)


def update():
    bpy.context.view_layer.update()


def enable_nodes(block):
    """Materials and worlds are always node based from 5.0; the flag only matters (and only exists cleanly) before."""
    if bpy.app.version < (5, 0, 0):
        block.use_nodes = True


# ----------------------------------------------------------------------------- shader helpers (guarded for 4.2 .. 5.x)
def new_material(name):
    mat = bpy.data.materials.new(name)
    enable_nodes(mat)
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat, nt, bsdf, out


def sock(node, *names):
    for n in names:
        if n in node.inputs:
            return node.inputs[n]
    return None


def setv(node, value, *names):
    s = sock(node, *names)
    if s is not None:
        s.default_value = value


def link(nt, a, b):
    if a is not None and b is not None:
        nt.links.new(a, b)


def by_id(sockets, identifier):
    for s in sockets:
        if s.identifier == identifier:
            return s
    return None


def rgba(c):
    return (*c, 1.0) if len(c) == 3 else c


def n_noise(nt, coord, scale, detail=6.0, rough=0.55, distortion=0.0):
    n = nt.nodes.new("ShaderNodeTexNoise")
    if hasattr(n, "noise_type"):
        n.noise_type = "FBM"
    if hasattr(n, "normalize"):
        n.normalize = True
    setv(n, scale, "Scale")
    setv(n, detail, "Detail")
    setv(n, rough, "Roughness")
    setv(n, distortion, "Distortion")
    link(nt, coord, sock(n, "Vector"))
    return n.outputs["Fac"]


def n_voronoi(nt, coord, scale, feature="F1"):
    n = nt.nodes.new("ShaderNodeTexVoronoi")
    n.feature = feature
    setv(n, scale, "Scale")
    link(nt, coord, sock(n, "Vector"))
    return n.outputs["Distance"]


def n_wave(nt, coord, scale, direction="X", distortion=0.0):
    n = nt.nodes.new("ShaderNodeTexWave")
    n.wave_type = "BANDS"
    n.bands_direction = direction
    setv(n, scale, "Scale")
    setv(n, distortion, "Distortion")
    link(nt, coord, sock(n, "Vector"))
    return n.outputs["Fac"]


def n_ramp(nt, fac, stops):
    n = nt.nodes.new("ShaderNodeValToRGB")
    els = n.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    els[0].position, els[0].color = stops[0][0], rgba(stops[0][1])
    for pos, col in stops[1:]:
        e = els.new(pos)
        e.color = rgba(col)
    link(nt, fac, n.inputs["Fac"])
    return n.outputs["Color"]


def n_math(nt, op, a, b=0.0, clamp=False):
    n = nt.nodes.new("ShaderNodeMath")
    n.operation = op
    n.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = v
        else:
            link(nt, v, n.inputs[i])
    return n.outputs[0]


def n_mix(nt, fac, a, b, blend="MIX"):
    """ShaderNodeMix in RGBA mode (replaces the removed ShaderNodeMixRGB)."""
    n = nt.nodes.new("ShaderNodeMix")
    n.data_type = "RGBA"
    n.blend_type = blend
    f, ia, ib = by_id(n.inputs, "Factor_Float"), by_id(n.inputs, "A_Color"), by_id(n.inputs, "B_Color")
    for s, v in ((f, fac), (ia, a), (ib, b)):
        if isinstance(v, (int, float)):
            s.default_value = v
        elif isinstance(v, tuple):
            s.default_value = rgba(v)
        else:
            link(nt, v, s)
    return by_id(n.outputs, "Result_Color")


def n_maprange(nt, value, lo, hi, from_lo=0.25, from_hi=0.75):
    n = nt.nodes.new("ShaderNodeMapRange")
    link(nt, value, n.inputs["Value"])
    n.inputs["From Min"].default_value, n.inputs["From Max"].default_value = from_lo, from_hi
    n.inputs["To Min"].default_value, n.inputs["To Max"].default_value = lo, hi
    n.clamp = True
    return n.outputs["Result"]


def n_mapping(nt, coord, scale=(1, 1, 1), loc=(0, 0, 0)):
    n = nt.nodes.new("ShaderNodeMapping")
    link(nt, coord, n.inputs["Vector"])
    n.inputs["Scale"].default_value = scale
    n.inputs["Location"].default_value = loc
    return n.outputs["Vector"]


def n_bump(nt, height, strength, distance=0.02, normal=None):
    n = nt.nodes.new("ShaderNodeBump")
    setv(n, strength, "Strength")
    setv(n, distance, "Distance")
    link(nt, height, n.inputs["Height"])
    link(nt, normal, sock(n, "Normal"))
    return n.outputs["Normal"]


def layered_material(name, base, dark, rough=(0.55, 0.85), metal=0.0, noise_scale=8.0, grime=0.6,
                     grime_color=(0.006, 0.005, 0.004), edge=0.25, edge_color=None, bump=0.25, sheen=0.0,
                     coat=0.0, sss=0.0, weave=False, spec=0.5):
    """Mottled base + AO crevice grime + pointiness edge wear + patchy roughness + multi-scale bump."""
    mat, nt, bsdf, _ = new_material(name)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    mottled = n_noise(nt, co, noise_scale, 8.0, 0.62)
    col = n_ramp(nt, mottled, [(0.32, dark), (0.72, base)])
    if grime > 0:
        ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
        ao.samples = 12
        setv(ao, 0.14, "Distance")
        cavity = n_math(nt, "SUBTRACT", 1.0, ao.outputs["AO"])
        dirt = n_math(nt, "MULTIPLY", cavity, n_noise(nt, co, noise_scale * 2.7, 5.0, 0.7))
        dirt = n_math(nt, "MULTIPLY", n_math(nt, "POWER", dirt, 0.6), grime * 2.2, clamp=True)
        col = n_mix(nt, dirt, col, grime_color)
    if edge > 0:
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        wear = n_ramp(nt, geo.outputs["Pointiness"], [(0.5, (0, 0, 0)), (0.58, (1, 1, 1))])
        wear = n_math(nt, "MULTIPLY", wear, n_math(nt, "MULTIPLY", n_noise(nt, co, noise_scale * 4), edge * 2.0), clamp=True)
        col = n_mix(nt, wear, col, edge_color or tuple(min(1.0, c * 2.4) for c in base))
    link(nt, col, sock(bsdf, "Base Color"))
    grease = n_noise(nt, co, noise_scale * 0.6, 3.0, 0.5)
    link(nt, n_maprange(nt, grease, rough[0], rough[1], 0.3, 0.7), sock(bsdf, "Roughness"))
    setv(bsdf, metal, "Metallic")
    setv(bsdf, spec, "Specular IOR Level", "Specular")
    setv(bsdf, sheen, "Sheen Weight", "Sheen")
    setv(bsdf, 0.6, "Sheen Roughness")
    setv(bsdf, coat, "Coat Weight", "Clearcoat")
    setv(bsdf, 0.08, "Coat Roughness", "Clearcoat Roughness")
    setv(bsdf, sss, "Subsurface Weight", "Subsurface")
    height = n_math(nt, "ADD", n_noise(nt, co, noise_scale * 9, 10.0, 0.66),
                    n_math(nt, "MULTIPLY", n_voronoi(nt, co, noise_scale * 3), 0.5))
    if weave:
        twill = n_math(nt, "MULTIPLY", n_wave(nt, n_mapping(nt, co, (1, 1, 1)), 260, "X", 2.0),
                       n_wave(nt, n_mapping(nt, co, (1, 1, 1)), 260, "Y", 2.0))
        height = n_math(nt, "ADD", height, n_math(nt, "MULTIPLY", twill, 1.4))
    link(nt, n_bump(nt, height, bump, 0.012), sock(bsdf, "Normal"))
    return mat


def emissive_material(name, color, strength, base=None):
    mat, nt, bsdf, _ = new_material(name)
    setv(bsdf, rgba(base or color), "Base Color")
    setv(bsdf, rgba(color), "Emission Color", "Emission")
    setv(bsdf, strength, "Emission Strength")
    setv(bsdf, 0.15, "Roughness")
    return mat


def emission_socket(mat):
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    return sock(bsdf, "Emission Strength"), sock(bsdf, "Emission Color", "Emission")


def horn_material():
    """Keratin: ivory base fading to charcoal tips, growth-ring grooves and longitudinal streaks (attribute driven)."""
    mat, nt, bsdf, _ = new_material("M_Horn_Keratin")
    attr_u = nt.nodes.new("ShaderNodeAttribute")
    attr_u.attribute_name = "horn_u"
    attr_v = nt.nodes.new("ShaderNodeAttribute")
    attr_v.attribute_name = "horn_v"
    u, v = attr_u.outputs["Fac"], attr_v.outputs["Fac"]
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    along = n_ramp(nt, u, [(0.0, (0.42, 0.37, 0.28)), (0.45, (0.30, 0.25, 0.18)), (0.85, (0.07, 0.06, 0.05)),
                           (1.0, (0.03, 0.025, 0.022))])
    streak_coord = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, n_math(nt, "MULTIPLY", u, 2.0), streak_coord.inputs["X"])
    link(nt, n_math(nt, "MULTIPLY", v, 40.0), streak_coord.inputs["Y"])
    streaks = n_noise(nt, streak_coord.outputs["Vector"], 2.5, 6.0, 0.6)
    col = n_mix(nt, n_math(nt, "MULTIPLY", streaks, 0.55), along, (0.05, 0.04, 0.03), "MULTIPLY")
    rings = n_math(nt, "SINE", n_math(nt, "MULTIPLY", u, math.pi * 32))
    groove = n_math(nt, "MULTIPLY", n_math(nt, "ADD", rings, 1.0), 0.5)
    col = n_mix(nt, n_math(nt, "MULTIPLY", groove, 0.25), col, (0.02, 0.018, 0.015))
    link(nt, col, sock(bsdf, "Base Color"))
    link(nt, n_maprange(nt, n_noise(nt, co, 30, 4), 0.45, 0.7), sock(bsdf, "Roughness"))
    setv(bsdf, 0.35, "Coat Weight", "Clearcoat")
    height = n_math(nt, "ADD", n_math(nt, "MULTIPLY", groove, 0.8), n_math(nt, "MULTIPLY", streaks, 0.6))
    link(nt, n_bump(nt, height, 0.35, 0.01), sock(bsdf, "Normal"))
    return mat


def concrete_material(name, wet=True):
    """Voronoi cracks, mottled cement, and (floor only) puddles that drop roughness for reflections."""
    mat, nt, bsdf, _ = new_material(name)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    cracks = n_ramp(nt, n_voronoi(nt, n_mapping(nt, co, (1, 1, 1)), 1.7, "DISTANCE_TO_EDGE"), [(0.0, (0, 0, 0)), (0.025, (1, 1, 1))])
    cement = n_ramp(nt, n_noise(nt, co, 2.6, 10.0, 0.62), [(0.3, (0.045, 0.045, 0.046)), (0.75, (0.11, 0.105, 0.1))])
    if not wet:
        streak = n_noise(nt, n_mapping(nt, co, (3.0, 3.0, 0.25)), 2.0, 6.0, 0.6)
        cement = n_mix(nt, n_math(nt, "MULTIPLY", streak, 0.7), cement, (0.015, 0.016, 0.014), "MULTIPLY")
    col = n_mix(nt, n_math(nt, "SUBTRACT", 1.0, cracks), cement, (0.008, 0.008, 0.008))
    rough = n_maprange(nt, n_noise(nt, co, 6.0, 4.0), 0.62, 0.92)
    height = n_math(nt, "ADD", n_math(nt, "MULTIPLY", cracks, 0.8), n_noise(nt, co, 40.0, 8.0, 0.7))
    if wet:
        puddle = n_ramp(nt, n_noise(nt, co, 0.55, 3.0, 0.5), [(0.5, (0, 0, 0)), (0.56, (1, 1, 1))])
        col = n_mix(nt, n_math(nt, "MULTIPLY", puddle, 0.65), col, (0.01, 0.011, 0.013))
        rough = n_mix(nt, puddle, rough, (0.035, 0.035, 0.035))
        height = n_math(nt, "MULTIPLY", height, n_math(nt, "SUBTRACT", 1.0, n_math(nt, "MULTIPLY", puddle, 0.9)))
    link(nt, col, sock(bsdf, "Base Color"))
    link(nt, rough, sock(bsdf, "Roughness"))
    link(nt, n_bump(nt, height, 0.45, 0.03), sock(bsdf, "Normal"))
    return mat


def volume_material():
    mat = bpy.data.materials.new("M_Basement_Fog")
    enable_nodes(mat)
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    density = n_math(nt, "ADD", n_math(nt, "MULTIPLY", n_noise(nt, co, 0.7, 3.0, 0.5), 0.006), 0.0025)
    link(nt, density, vol.inputs["Density"])
    setv(vol, (0.62, 0.7, 0.8, 1.0), "Color")
    setv(vol, 0.35, "Anisotropy")
    nt.links.new(vol.outputs["Volume"], out.inputs["Volume"])
    return mat


MAT = {
    "skin": layered_material("M_Mascot_Fur", (0.058, 0.047, 0.041), (0.024, 0.02, 0.018), (0.45, 0.88), noise_scale=9,
                             grime=0.75, edge=0.28, edge_color=(0.13, 0.11, 0.09), bump=0.35, sheen=0.35),
    "skinDark": layered_material("M_Mascot_Fur_Dark", (0.022, 0.018, 0.017), (0.01, 0.009, 0.008), (0.6, 0.92),
                                 noise_scale=11, grime=0.6, edge=0.18, bump=0.35, sheen=0.25),
    "snout": layered_material("M_Snout_Vinyl", (0.048, 0.038, 0.034), (0.02, 0.016, 0.015), (0.38, 0.75), noise_scale=7,
                              grime=0.85, edge=0.3, edge_color=(0.12, 0.095, 0.08), bump=0.22, coat=0.18),
    "uniform": layered_material("M_Guard_Twill", (0.032, 0.042, 0.058), (0.016, 0.02, 0.028), (0.75, 0.95),
                                noise_scale=5, grime=0.85, edge=0.15, bump=0.18, weave=True),
    "uniformDark": layered_material("M_Guard_Twill_Dark", (0.014, 0.018, 0.026), (0.008, 0.009, 0.013), (0.78, 0.95),
                                    noise_scale=5, grime=0.7, edge=0.12, bump=0.18, weave=True),
    "leather": layered_material("M_Boot_Leather", (0.032, 0.028, 0.027), (0.012, 0.011, 0.011), (0.32, 0.65),
                                noise_scale=10, grime=0.6, edge=0.4, edge_color=(0.09, 0.075, 0.065), bump=0.3, coat=0.22),
    "rubber": layered_material("M_Rubber", (0.009, 0.01, 0.011), (0.004, 0.004, 0.005), (0.8, 0.95), noise_scale=14,
                               grime=0.3, edge=0.1, bump=0.2),
    "plastic": layered_material("M_Radio_Plastic", (0.02, 0.025, 0.03), (0.01, 0.012, 0.015), (0.33, 0.55),
                                metal=0.1, noise_scale=12, grime=0.5, edge=0.35, bump=0.12),
    "brass": layered_material("M_Tarnished_Brass", (0.42, 0.3, 0.11), (0.11, 0.12, 0.065), (0.26, 0.62), metal=0.92,
                              noise_scale=18, grime=0.55, grime_color=(0.03, 0.035, 0.02), edge=0.5,
                              edge_color=(0.75, 0.58, 0.27), bump=0.15),
    "pipe": layered_material("M_Painted_Pipe", (0.05, 0.055, 0.06), (0.09, 0.045, 0.025), (0.4, 0.82), metal=0.55,
                             noise_scale=6, grime=0.6, edge=0.4, bump=0.2),
    "cavity": layered_material("M_Cavity_Void", (0.002, 0.002, 0.002), (0.0, 0.0, 0.0), (0.95, 1.0), grime=0, edge=0,
                               bump=0.05),
    "gums": layered_material("M_Wet_Gums", (0.17, 0.022, 0.03), (0.06, 0.008, 0.012), (0.18, 0.35), noise_scale=20,
                             grime=0.5, edge=0.0, bump=0.2, coat=0.6, sss=0.25),
    "teeth": layered_material("M_Stained_Teeth", (0.56, 0.5, 0.36), (0.24, 0.19, 0.09), (0.3, 0.5), noise_scale=25,
                              grime=0.95, grime_color=(0.08, 0.05, 0.02), edge=0.0, bump=0.15, coat=0.35),
    "stitch": layered_material("M_Stitch_Thread", (0.24, 0.2, 0.16), (0.11, 0.09, 0.07), (0.9, 1.0), noise_scale=40,
                               grime=0.5, edge=0.0, bump=0.3),
    "idCard": layered_material("M_ID_Laminate", (0.6, 0.58, 0.5), (0.32, 0.27, 0.18), (0.3, 0.6), noise_scale=30,
                               grime=0.9, grime_color=(0.12, 0.09, 0.05), edge=0.3, bump=0.08, coat=0.4),
    "ink": layered_material("M_Ink", (0.015, 0.015, 0.016), (0.01, 0.01, 0.01), (0.5, 0.7), grime=0, edge=0, bump=0.02),
    "inkRed": layered_material("M_Ink_Red", (0.32, 0.02, 0.02), (0.18, 0.01, 0.01), (0.5, 0.7), grime=0, edge=0, bump=0.02),
    "eyeball": layered_material("M_Wet_Eyeball", (0.012, 0.01, 0.011), (0.004, 0.004, 0.004), (0.03, 0.05),
                                noise_scale=40, grime=0, edge=0, bump=0.02, coat=1.0, spec=0.8),
    "horn": horn_material(),
    "floor": concrete_material("M_Wet_Concrete_Floor", True),
    "wall": concrete_material("M_Concrete_Wall", False),
    "fog": volume_material(),
    "iris": emissive_material("M_Iris_Glow", (1.0, 0.72, 0.4), 6.0, (1.0, 0.86, 0.62)),
    "led": emissive_material("M_Status_LED", (1.0, 0.06, 0.06), 14.0),
    "tube": emissive_material("M_Fluorescent_Tube", (0.78, 0.89, 1.0), 9.0),
}


# ----------------------------------------------------------------------------- geometry helpers (reference space)
def bm_sphere(r, u=28, v=18):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    return bm


def bm_icosphere(r, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
    return bm


def bm_cylinder(r_top, r_bottom, h, seg=20):
    """Along +Y like THREE.CylinderGeometry."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r_bottom, radius2=r_top, depth=h,
                          matrix=R(-math.pi / 2, 0, 0))
    return bm


def bm_box(w, h, d):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=S(w, h, d))
    return bm


def bm_torus(major, minor, seg=28, tube_seg=10, arc=2 * math.pi):
    """Ring in the XY plane like THREE.TorusGeometry (optionally an open arc)."""
    bm = bmesh.new()
    closed = arc >= 2 * math.pi - 1e-6
    count = seg if closed else seg + 1
    rings = []
    for i in range(count):
        a = arc * i / seg
        centre = Vector((math.cos(a), math.sin(a), 0.0))
        ring = []
        for j in range(tube_seg):
            b = 2 * math.pi * j / tube_seg
            ring.append(bm.verts.new(centre * (major + minor * math.cos(b)) + Vector((0, 0, minor * math.sin(b)))))
        rings.append(ring)
    for i in range(seg):
        a_ring, b_ring = rings[i], rings[(i + 1) % count]
        for j in range(tube_seg):
            bm.faces.new((a_ring[j], b_ring[j], b_ring[(j + 1) % tube_seg], a_ring[(j + 1) % tube_seg]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def bm_lathe(profile, seg=28):
    """Revolve (radius, y) points around +Y, like THREE.LatheGeometry."""
    bm = bmesh.new()
    rings = [[bm.verts.new((r * math.sin(2 * math.pi * i / seg), y, r * math.cos(2 * math.pi * i / seg))) for i in range(seg)]
             for r, y in profile]
    for a_ring, b_ring in zip(rings, rings[1:]):
        for i in range(seg):
            bm.faces.new((a_ring[i], a_ring[(i + 1) % seg], b_ring[(i + 1) % seg], b_ring[i]))
    bm.faces.new(rings[0][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def bm_shield(depth=0.012):
    """Security shield outline from the reference, filled and extruded."""
    def quad(p0, c, p1, n=7):
        return [tuple((1 - t) ** 2 * p0[k] + 2 * (1 - t) * t * c[k] + t ** 2 * p1[k] for k in range(2))
                for t in (i / n for i in range(1, n + 1))]
    pts = [(0, 0.055)]
    pts += quad((0, 0.055), (0.035, 0.052), (0.048, 0.032))
    pts += [(0.042, -0.015)]
    pts += quad((0.042, -0.015), (0.032, -0.048), (0, -0.062))
    pts += quad((0, -0.062), (-0.032, -0.048), (-0.042, -0.015))
    pts += [(-0.048, 0.032)]
    pts += quad((-0.048, 0.032), (-0.035, 0.052), (0, 0.055))[:-1]
    bm = bmesh.new()
    face = bm.faces.new([bm.verts.new((x, y, -depth / 2)) for x, y in pts])
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, vec=(0, 0, depth), verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def mesh_object(name, bm, collection, mat=None, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    o = bpy.data.objects.new(name, me)
    collection.objects.link(o)
    if mat:
        me.materials.append(mat)
    return o


def skin_object(name, points, radii, collection, mat=None, levels=2):
    """Organic tube: vertex chain + Skin + Subdivision (shoulder->elbow, finger joints, ...)."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([Vector(p) for p in points], [(i, i + 1) for i in range(len(points) - 1)], [])
    o = bpy.data.objects.new(name, me)
    collection.objects.link(o)
    skin = o.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    if len(me.skin_vertices) == 0 and hasattr(me.skin_vertices, "new"):
        me.skin_vertices.new()
    layer = me.skin_vertices[0].data
    for i, r in enumerate(radii):
        layer[i].radius = (r, r) if isinstance(r, (int, float)) else r
    layer[0].use_root = True
    sub = o.modifiers.new("Subdivision", "SUBSURF")
    sub.levels = sub.render_levels = levels
    if mat:
        me.materials.append(mat)
    return o


_TEX = {}


def texture(kind, size, depth=2):
    key = (kind, round(size, 4), depth)
    if key not in _TEX:
        t = bpy.data.textures.new(f"TX_{kind}_{size:g}", type=kind)
        t.noise_scale = size
        if hasattr(t, "noise_depth"):
            t.noise_depth = depth
        _TEX[key] = t
    return _TEX[key]


def bake(o, subsurf=0, displace=(), smooth_iter=0, jitter=True):
    """Subdivision -> Smooth -> Displace (unique global noise per part, so twins are never identical), then applied.
    jitter=False keeps the object in place, needed when Boolean cutters must still overlap it."""
    if subsurf:
        m = o.modifiers.new("Subdivision", "SUBSURF")
        m.levels = m.render_levels = subsurf
    if smooth_iter:
        m = o.modifiers.new("Smooth", "SMOOTH")
        m.factor, m.iterations = 0.5, smooth_iter
    for strength, size, kind in displace:
        m = o.modifiers.new("Displace", "DISPLACE")
        m.texture, m.strength, m.mid_level, m.texture_coords = texture(kind, size), strength, 0.5, "GLOBAL"
    saved = o.matrix_world.copy()
    if jitter:
        o.matrix_world = T(rng.uniform(-40, 40), rng.uniform(-40, 40), rng.uniform(-40, 40))
    update()
    evaluated = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    baked = bpy.data.meshes.new_from_object(evaluated)
    old = o.data
    o.modifiers.clear()
    o.data = baked
    baked.polygons.foreach_set("use_smooth", [True] * len(baked.polygons))
    if old.users == 0:
        bpy.data.meshes.remove(old)
    o.matrix_world = saved
    return o


def hard_surface(o, width=0.003, segments=2):
    """Live Bevel + WeightedNormal for props (non-destructive)."""
    b = o.modifiers.new("Bevel", "BEVEL")
    b.width, b.segments, b.limit_method = width, segments, "ANGLE"
    o.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL").keep_sharp = True
    return o


# ----------------------------------------------------------------------------- rig layout (reference space, day-shift rest)
FINGERS = [
    {"z": 0.044, "len": 0.088, "r": 0.0145, "spread": 0.14},
    {"z": 0.015, "len": 0.102, "r": 0.015, "spread": 0.04},
    {"z": -0.014, "len": 0.094, "r": 0.014, "spread": -0.05},
    {"z": -0.042, "len": 0.076, "r": 0.0125, "spread": -0.14},
]
for _f in FINGERS:
    _f["len"] *= FINGER_STRETCH
SIDES = ((1, "L"), (-1, "R"))
PIVOTS = {}
ORDER = []


def pivot(name, parent, pos):
    PIVOTS[name] = (parent, Vector(pos))
    ORDER.append(name)


pivot("root", None, (0, 0.06, 0))  # boots rest on the floor
pivot("pelvis", "root", (0, 0.98, 0))
pivot("spine", "pelvis", (0, 0.08, 0))
pivot("torso_morph", "spine", (0, 0, 0))
pivot("hunch_morph", "spine", (0, 0.64, -0.08))
pivot("neck", "spine", (0, 0.85, 0.03))
pivot("head", "neck", (0, 0.22, 0.03))
pivot("jaw", "head", (0, -0.067, 0.195))
for _s, _x in SIDES:
    pivot(f"shoulder.{_x}", "spine", (0.42 * _s, 0.73, -0.01))
    pivot(f"upperarm.{_x}", f"shoulder.{_x}", (0, 0, 0))
    pivot(f"elbow.{_x}", f"upperarm.{_x}", (0, -0.42, 0))
    pivot(f"hand.{_x}", f"elbow.{_x}", (0, -0.42, 0))
    for _i, _f in enumerate(FINGERS):
        pivot(f"finger{_i}.{_x}", f"hand.{_x}", (0, -0.148, _f["z"]))
        pivot(f"finger{_i}_tip.{_x}", f"finger{_i}.{_x}", (0, -_f["len"] * 0.65, 0))
    pivot(f"hip.{_x}", "pelvis", (0.175 * _s, -0.06, 0))
    pivot(f"knee.{_x}", f"hip.{_x}", (0, -0.44, 0))
    pivot(f"boot.{_x}", f"knee.{_x}", (0, -0.42, 0.03))


def pivot_world(name):
    p = Vector((0, 0, 0))
    while name:
        parent, pos = PIVOTS[name]
        p += pos
        name = parent
    return p


ATTACH = []  # (object, bone)


def place(o, bone, local=Matrix.Identity(4)):
    """Put a part (authored in its bone's reference space) into the world and remember which bone carries it."""
    o.matrix_world = C4 @ T(*pivot_world(bone)) @ local
    ATTACH.append((o, bone))
    return o


# Torso props were laid out by the reference on the night-scaled torso; divide by that scale so they sit on the
# nominal torso surface and ride the torso_morph bone between day and night.
NIGHT_TORSO = (lerp(1.16, 0.94, NIGHT_SHIFT), lerp(0.95, 1.08, NIGHT_SHIFT), lerp(0.86, 0.72, NIGHT_SHIFT))


def torso_space(p):
    return (p[0] / NIGHT_TORSO[0], p[1] / NIGHT_TORSO[1], p[2] / NIGHT_TORSO[2])


# ----------------------------------------------------------------------------- body
def build_body():
    torso_profile = [(0.01, 0.0), (0.31, 0.02), (0.33, 0.16), (0.37, 0.38), (0.39, 0.58), (0.35, 0.74), (0.21, 0.83), (0.13, 0.85)]
    torso = bake(mesh_object("Torso_Shirt", bm_lathe(torso_profile, 32), CHAR, MAT["uniform"]), 2,
                 [(0.016, 0.25, "CLOUDS"), (0.006, 0.05, "STUCCI")])
    place(torso, "torso_morph")
    hunch = bake(mesh_object("Hunch_Trapezius", bm_sphere(0.28), CHAR, MAT["uniform"]), 1,
                 [(0.02, 0.2, "CLOUDS"), (0.005, 0.05, "STUCCI")])
    place(hunch, "hunch_morph")
    hips = bake(mesh_object("Hips_Trousers", bm_cylinder(0.31, 0.29, 0.24, 24), CHAR, MAT["uniformDark"]), 2,
                [(0.012, 0.2, "CLOUDS")])
    place(hips, "pelvis", S(1.04, 1.0, 0.78))
    neck = bake(mesh_object("Neck", bm_cylinder(0.13, 0.155, 0.2, 20), CHAR, MAT["skinDark"]), 2, [(0.012, 0.12, "CLOUDS")])
    place(neck, "neck", T(0, 0.07, 0))
    inner = bake(mesh_object("Collar_Undershirt", bm_cylinder(0.145, 0.17, 0.14, 20), PROPS, MAT["skinDark"]), 1,
                 [(0.008, 0.1, "CLOUDS")])
    place(inner, "torso_morph", T(*torso_space((0, 0.83, 0.02))))


def build_uniform_props():
    placket = hard_surface(mesh_object("Shirt_Placket", bm_box(0.055, 0.68, 0.025), PROPS, MAT["uniformDark"]))
    place(placket, "torso_morph", T(*torso_space((0, 0.43, 0.295))) @ R(-0.05))
    for b in range(4):
        btn = hard_surface(mesh_object(f"Shirt_Button_{b}", bm_cylinder(0.011, 0.011, 0.01, 14), PROPS, MAT["brass"]), 0.002)
        place(btn, "torso_morph", T(*torso_space((0, 0.66 - b * 0.145, 0.312 - b * 0.005))) @ R(math.pi / 2))
    for s, x in SIDES:
        flap = bake(mesh_object(f"Collar_Flap.{x}", bm_box(0.14, 0.09, 0.022), PROPS, MAT["uniform"]), 2, [(0.004, 0.05, "STUCCI")])
        place(flap, "torso_morph", T(*torso_space((s * 0.105, 0.79, 0.155))) @ R(-0.35, s * -0.42, s * 0.38))
        ep = hard_surface(mesh_object(f"Epaulette.{x}", bm_box(0.16, 0.018, 0.065), PROPS, MAT["uniformDark"]))
        place(ep, "torso_morph", T(*torso_space((s * 0.27, 0.79, -0.01))) @ R(0, 0, s * -0.26))
        ep_btn = hard_surface(mesh_object(f"Epaulette_Button.{x}", bm_cylinder(0.009, 0.009, 0.01, 12), PROPS, MAT["brass"]), 0.002)
        place(ep_btn, "torso_morph", T(*torso_space((s * 0.21, 0.812, -0.01))))
        pocket = hard_surface(mesh_object(f"Breast_Pocket.{x}", bm_box(0.13, 0.14, 0.022), PROPS, MAT["uniformDark"]))
        place(pocket, "torso_morph", T(*torso_space((s * 0.17, 0.49, 0.275))) @ R(0, s * 0.26))
        pflap = hard_surface(mesh_object(f"Pocket_Flap.{x}", bm_box(0.136, 0.042, 0.028), PROPS, MAT["uniform"]))
        place(pflap, "torso_morph", T(*torso_space((s * 0.17, 0.555, 0.28))) @ R(0, s * 0.26))

    badge = T(*torso_space((-0.175, 0.635, 0.275))) @ R(-0.12, -0.28, 0.04)
    place(hard_surface(mesh_object("Shield_Badge", bm_shield(), PROPS, MAT["brass"]), 0.003, 3), "torso_morph", badge)
    emblem = mesh_object("Badge_Emblem", bm_icosphere(0.018, 1), PROPS, MAT["brass"], smooth=False)
    place(emblem, "torso_morph", badge @ T(0, 0.003, 0.011) @ S(1, 1, 0.35))

    card = T(*torso_space((0.175, 0.615, 0.282))) @ R(-0.1, 0.28, -0.14)  # crooked on purpose
    place(hard_surface(mesh_object("ID_Card", bm_box(0.125, 0.072, 0.008), PROPS, MAT["idCard"]), 0.002), "torso_morph", card)
    place(hard_surface(mesh_object("ID_Clip", bm_box(0.024, 0.022, 0.016), PROPS, MAT["brass"]), 0.002), "torso_morph",
          card @ T(0, 0.04, 0))
    place(mesh_object("ID_Photo", bm_box(0.03, 0.036, 0.001), PROPS, MAT["ink"]), "torso_morph", card @ T(-0.042, -0.004, 0.0045))
    for text, size, pos, mat in (("SENTRY SECURITY", 0.0062, (0.014, 0.018, 0.0043), MAT["inkRed"]),
                                 ("BANTENG // SEC-04", 0.0078, (0.014, -0.004, 0.0043), MAT["ink"]),
                                 ("NIGHT SHIFT", 0.0055, (0.014, -0.02, 0.0043), MAT["ink"])):
        cu = bpy.data.curves.new(f"TXT_{text}", "FONT")
        cu.body, cu.size, cu.extrude, cu.align_x, cu.align_y = text, size, 0.0003, "CENTER", "CENTER"
        cu.materials.append(mat)
        txt = bpy.data.objects.new(f"ID_Text_{text.split()[0]}", cu)
        PROPS.objects.link(txt)
        place(txt, "torso_morph", card @ T(*pos))

    mic = T(*torso_space((0.24, 0.77, 0.155))) @ R(-0.22, 0.38, -0.18)
    place(hard_surface(mesh_object("Walkie_Mic", bm_box(0.072, 0.105, 0.044), PROPS, MAT["plastic"]), 0.004, 3), "torso_morph", mic)
    for g in range(4):
        place(mesh_object(f"Mic_Grille_{g}", bm_box(0.048, 0.006, 0.008), PROPS, MAT["cavity"]), "torso_morph",
              mic @ T(0, 0.022 - g * 0.014, 0.022))
    led = mesh_object("Mic_Status_LED", bm_sphere(0.008, 12, 8), PROPS, MAT["led"])
    place(led, "torso_morph", mic @ T(-0.02, 0.036, 0.023))
    lamp = bpy.data.lights.new("LED_Glow", "POINT")
    lamp.color, lamp.energy, lamp.shadow_soft_size = (1.0, 0.12, 0.12), 0.6, 0.004
    lamp_obj = bpy.data.objects.new("LED_Glow", lamp)
    LIGHTS.objects.link(lamp_obj)
    place(lamp_obj, "torso_morph", mic @ T(-0.02, 0.036, 0.05))

    # True helical coiled cord from shoulder mic to belt radio (reference buildCoiledWireGeometry)
    pts = []
    for i in range(301):
        t = i / 300
        base = Vector((lerp(0.21, 0.30, t ** 0.8) + math.sin(t * math.pi) * 0.04,
                       lerp(0.46, -0.26, t), lerp(0.18, 0.14, t) + math.sin(t * math.pi) * 0.05))
        coil = 0.015 * min(1.0, math.sin(t * math.pi) * 3.5)
        ang = t * 15 * 2 * math.pi
        pts.append(base + Vector((0.03 + math.cos(ang) * coil, 0.27, math.sin(ang) * coil)))
    cu = bpy.data.curves.new("Coiled_Cord", "CURVE")
    cu.dimensions, cu.bevel_depth, cu.bevel_resolution, cu.use_fill_caps = "3D", 0.0048, 3, True
    spline = cu.splines.new("POLY")
    spline.points.add(len(pts) - 1)
    for p, v in zip(spline.points, pts):
        p.co = (*torso_space(v), 1.0)
    cu.materials.append(MAT["rubber"])
    cord = bpy.data.objects.new("Coiled_Cord", cu)
    PROPS.objects.link(cord)
    place(cord, "torso_morph")

    belt = T(0, 0.055, 0)
    place(bake(mesh_object("Utility_Belt", bm_cylinder(0.345, 0.34, 0.095, 40), PROPS, MAT["leather"]), 1,
               [(0.004, 0.08, "CLOUDS")]), "spine", belt @ S(1.06, 1.0, 0.80))
    place(hard_surface(mesh_object("Belt_Buckle", bm_box(0.115, 0.11, 0.028), PROPS, MAT["brass"]), 0.006, 3), "spine",
          belt @ T(0, 0, 0.276))
    place(hard_surface(mesh_object("Belt_Buckle_Inner", bm_box(0.072, 0.068, 0.031), PROPS, MAT["leather"])), "spine",
          belt @ T(0, 0, 0.277))
    radio = belt @ T(0.355, 0.01, 0.06) @ R(0, 0.2)
    place(hard_surface(mesh_object("Belt_Radio", bm_box(0.075, 0.16, 0.055), PROPS, MAT["plastic"]), 0.005, 3), "spine", radio)
    place(mesh_object("Radio_Antenna", bm_cylinder(0.004, 0.007, 0.24, 10), PROPS, MAT["rubber"]), "spine",
          radio @ T(0.015, 0.19, 0))
    place(hard_surface(mesh_object("Holster_Pouch", bm_box(0.07, 0.13, 0.095), PROPS, MAT["leather"]), 0.008, 3), "spine",
          belt @ T(-0.36, -0.01, 0.08))
    baton = belt @ T(-0.345, -0.06, -0.06) @ R(0.14, 0, 0.12)
    place(hard_surface(mesh_object("Security_Baton", bm_cylinder(0.022, 0.025, 0.34, 16), PROPS, MAT["plastic"])), "spine",
          baton @ T(0, -0.12, 0))
    place(hard_surface(mesh_object("Flashlight_Head", bm_cylinder(0.033, 0.025, 0.055, 18), PROPS, MAT["brass"])), "spine",
          baton @ T(0, -0.29, 0))


# ----------------------------------------------------------------------------- head
_MB_FACTOR = None


def metaball_factor():
    """Surface radius of an isolated metaball element relative to its radius (calibrated once)."""
    global _MB_FACTOR
    if _MB_FACTOR is None:
        mb = bpy.data.metaballs.new("MBCalibrate")
        mb.resolution = 0.02
        el = mb.elements.new(type="BALL")
        el.radius = 1.0
        o = bpy.data.objects.new("MBCalibrate", mb)
        SCRATCH.objects.link(o)
        update()
        me = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        _MB_FACTOR = max(v.co.length for v in me.vertices) or 1.0
        bpy.data.objects.remove(o)
        bpy.data.meshes.remove(me)
        bpy.data.metaballs.remove(mb)
    return _MB_FACTOR


def metaball_mesh(name, elements, resolution=0.008):
    """elements: (centre, radius, (sx, sy, sz), (rx, ry, rz)); returns a mesh object in local reference space."""
    k = metaball_factor()
    mb = bpy.data.metaballs.new(f"MB_{name}")
    mb.resolution = mb.render_resolution = resolution
    for centre, radius, size, rot in elements:
        el = mb.elements.new(type="ELLIPSOID")
        el.co, el.radius = centre, radius / k
        el.size_x, el.size_y, el.size_z = size
        el.rotation = R(*rot).to_quaternion()
    o = bpy.data.objects.new(f"MB_{name}", mb)
    SCRATCH.objects.link(o)
    update()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    me.name = name
    bpy.data.objects.remove(o)
    bpy.data.metaballs.remove(mb)
    obj = bpy.data.objects.new(name, me)
    CHAR.objects.link(obj)
    return obj


def cutter(name, centre, radii, rot=(0, 0, 0)):
    o = mesh_object(name, bm_sphere(1.0, 24, 16), SCRATCH, smooth=False)
    o.matrix_world = TRS(centre, rot, radii)
    o.hide_render = True
    return o


SNOUT = Vector((0, -0.025, 0.215))
SOCKETS = [Vector((s * 0.095, 0.095, 0.168)) for s, _ in SIDES]  # z is replaced by the sculpted surface depth


def surface(obj, origin, direction):
    """First hit on a mesh object (object at identity, so local == reference space)."""
    update()
    hit, loc, _, _ = obj.ray_cast(Vector(origin), Vector(direction).normalized())
    if not hit:
        raise RuntimeError(f"no surface from {tuple(origin)} on {obj.name}")
    return loc


def build_head():
    s_ = SNOUT
    elements = [
        ((0, 0.09, -0.02), 0.245, (1.06, 1.0, 1.02), (0, 0, 0)),                        # cranium
        ((0.155, -0.01, 0.11), 0.115, (0.95, 0.82, 0.9), (0, 0, 0)),                     # cheek / jowl pads
        ((-0.152, -0.016, 0.112), 0.112, (0.95, 0.84, 0.9), (0, 0, 0)),                  # (slightly uneven)
        ((0, 0.155, 0.205), 0.056, (1.15, 0.62, 0.8), (0, 0, 0)),                         # brow centre (night-low)
        ((0.098, 0.172, 0.208), 0.1, (1.0, 0.36, 0.42), (0.15, -0.25, 0.30)),           # furrowed brow arches
        ((-0.094, 0.168, 0.21), 0.1, (1.0, 0.38, 0.42), (0.15, 0.25, -0.33)),
        (tuple(s_ + Vector((0, 0.028, 0.02))), 0.145, (1.36, 0.68, 0.88), (0, 0, 0)),   # wide upper muzzle
        (tuple(s_ + Vector((0.11, 0.02, 0.095))), 0.054, (1.05, 0.8, 0.9), (0, 0, 0)),  # nostril flares
        (tuple(s_ + Vector((-0.112, 0.018, 0.093))), 0.056, (1.05, 0.8, 0.9), (0, 0, 0)),
    ]
    head = metaball_mesh("Head_Sculpt", elements)
    # Fused metaballs swell past their nominal radii, so cut features relative to the real surface.
    for c in SOCKETS:
        c.z = surface(head, (c.x, c.y, 1.0), (0, 0, -1)).z
    nostrils = [surface(head, s_ + Vector((s * 0.095, 0.012, 1.0)), (0, 0, -1)) for s, _ in SIDES]
    mouth_floor = surface(head, s_ + Vector((0, -1.0, 0.07)), (0, 1, 0))
    remesh = head.modifiers.new("VoxelRemesh", "REMESH")
    remesh.mode, remesh.voxel_size, remesh.adaptivity = "VOXEL", 0.0055, 0.0
    cutters = [cutter(f"CUT_Socket_{i}", (c.x, c.y, c.z - 0.03), (0.068, 0.058, 0.07)) for i, c in enumerate(SOCKETS)]
    cutters += [cutter(f"CUT_Nostril_{i}", tuple(n - Vector((0, 0, 0.012))), (0.03, 0.021, 0.05),
                       (0.2, s * 0.35, s * -0.25)) for i, ((s, _), n) in enumerate(zip(SIDES, nostrils))]
    cutters.append(cutter("CUT_Mouth", tuple(mouth_floor + Vector((0, 0.012, 0))), (0.15, 0.045, 0.14)))
    for c in cutters:
        b = head.modifiers.new(f"Bool_{c.name}", "BOOLEAN")
        b.operation, b.object = "DIFFERENCE", c
        try:
            b.solver = "EXACT"
        except TypeError:
            pass
    smooth = head.modifiers.new("Smooth", "SMOOTH")
    smooth.factor, smooth.iterations = 0.45, 3
    bake(head, 0, [(0.007, 0.07, "CLOUDS"), (0.0025, 0.02, "STUCCI")], jitter=False)
    for c in cutters:
        bpy.data.objects.remove(c)

    # material zones: vinyl snout and dark brow skin
    me = head.data
    me.materials.clear()  # metaball meshes carry an empty slot that would shift every index
    for mat in (MAT["skin"], MAT["snout"], MAT["skinDark"]):
        me.materials.append(mat)
    muzzle_c, muzzle_r = s_ + Vector((0, 0.028, 0.02)), Vector((0.145 * 1.36, 0.145 * 0.68, 0.145 * 0.88))
    for poly in me.polygons:
        c = poly.center
        d = Vector(((c.x - muzzle_c.x) / muzzle_r.x, (c.y - muzzle_c.y) / muzzle_r.y, (c.z - muzzle_c.z) / muzzle_r.z)).length
        if d < 1.12 or (abs(c.x) > 0.06 and c.y < 0.06 and c.z > 0.26):
            poly.material_index = 1
        elif c.y > 0.13 and c.z > 0.16:
            poly.material_index = 2
    place(head, "head")
    return head


def project(head, p, lift):
    ok, loc, normal, _ = head.closest_point_on_mesh(Vector(p))
    return (loc + normal * lift, normal) if ok else (Vector(p), Vector((0, 0, 1)))


def build_face_details(head):
    for i, sc in enumerate(SOCKETS):
        s = 1 if sc.x > 0 else -1
        lid = bake(mesh_object(f"Eyelid_Rim.{'L' if s > 0 else 'R'}", bm_torus(0.056, 0.015, 28, 10), CHAR, MAT["skinDark"]), 1,
                   [(0.006, 0.03, "CLOUDS")])
        place(lid, "head", T(*(sc + Vector((0, 0.002 * s, -0.004)))) @ R(0, s * 0.18) @ S(1.1, 0.92, 0.9))
        eye = T(*(sc + Vector((0, -0.003, -0.05))))  # wet eyeball seated ~6 mm behind the socket rim
        place(mesh_object(f"Eyeball.{'L' if s > 0 else 'R'}", bm_sphere(0.044, 32, 20), CHAR, MAT["eyeball"]), "head", eye)
        place(mesh_object(f"Iris_Ring.{'L' if s > 0 else 'R'}", bm_torus(0.008, 0.0035, 24, 8), CHAR, MAT["iris"]), "head",
              eye @ T(0, 0, 0.0445))
        place(mesh_object(f"Iris_Pinprick.{'L' if s > 0 else 'R'}", bm_sphere(0.0038, 12, 8), CHAR, MAT["iris"]), "head",
              eye @ T(0.004, 0.004, 0.0442))

    # stitched mascot seam across the snout bridge: ridge curve + cross stitches, projected onto the sculpt
    seam_pts = []
    for i in range(41):
        t = -1 + 2 * i / 40
        seam_pts.append(project(head, SNOUT + Vector((t * 0.185, 0.068 - abs(t) * 0.035, 0.118 - abs(t) * 0.065)), 0.0015))
    cu = bpy.data.curves.new("Snout_Seam_Ridge", "CURVE")
    cu.dimensions, cu.bevel_depth, cu.bevel_resolution = "3D", 0.0042, 2
    spline = cu.splines.new("POLY")
    spline.points.add(len(seam_pts) - 1)
    for p, (loc, _) in zip(spline.points, seam_pts):
        p.co = (*loc, 1.0)
    cu.materials.append(MAT["snout"])
    ridge = bpy.data.objects.new("Snout_Seam_Ridge", cu)
    CHAR.objects.link(ridge)
    place(ridge, "head")
    for i in range(2, 39, 3):
        loc, normal = project(head, seam_pts[i][0], 0.0035)
        tangent = (seam_pts[i + 1][0] - seam_pts[i - 1][0]).normalized()
        side = normal.cross(tangent).normalized()
        basis = Matrix((side, tangent, normal)).transposed().to_4x4()  # columns: x=side, y=tangent, z=normal
        for k, tilt in enumerate((0.55, -0.55)):
            st = mesh_object(f"Stitch_{i}_{k}", bm_box(0.0055, 0.024, 0.005), CHAR, MAT["stitch"])
            place(st, "head", T(*loc) @ basis @ R(0, 0, tilt + rng.uniform(-0.12, 0.12)))

    # mouth interior, gums and crooked stained teeth (hidden behind the mascot smile until the jaw drops)
    place(mesh_object("Mouth_Interior", bm_sphere(0.11, 24, 16), CHAR, MAT["cavity"]), "head",
          T(*(SNOUT + Vector((0, -0.045, 0.025)))) @ S(1.22, 0.85, 0.85))
    place(bake(mesh_object("Upper_Gums", bm_torus(0.095, 0.016, 20, 8, math.pi), CHAR, MAT["gums"]), 1,
               [(0.004, 0.02, "CLOUDS")]), "head", T(*(SNOUT + Vector((0, -0.026, 0.03)))) @ R(math.pi / 2))
    for i in range(-3, 4):
        ang = (i / 3.5) * 1.15
        h = 0.034 if abs(i) == 2 else 0.024 + (abs(i) % 2) * 0.005
        tooth = bake(mesh_object(f"Tooth_Upper_{i + 3}", bm_cylinder(0.0, 0.012, h * rng.uniform(0.85, 1.2), 8), CHAR,
                                 MAT["teeth"]), 1, [(0.0015, 0.01, "CLOUDS")])
        place(tooth, "head", T(*(SNOUT + Vector((math.sin(ang) * 0.095 + rng.uniform(-0.003, 0.003), -0.041,
                                                0.035 + math.cos(ang) * 0.075))))
              @ R(math.pi + 0.12 + rng.uniform(-0.15, 0.15), rng.uniform(-0.2, 0.2), i * 0.09 + rng.uniform(-0.14, 0.14)))
    smile = mesh_object("Mascot_Smile_Line", bm_torus(0.115, 0.006, 32, 8, math.pi * 0.72), CHAR, MAT["gums"])
    place(smile, "head", T(*(SNOUT + Vector((0, 0.015, 0.105)))) @ R(0.35, 0, math.pi * 1.14))

    # tufts of matted crown fur
    for idx, (x, y, z, sx, sy, sz, rz) in enumerate(((0.0, 0.32, 0.06, 0.075, 0.052, 0.085, 0.05),
                                                    (-0.075, 0.305, 0.04, 0.062, 0.045, 0.075, 0.32),
                                                    (0.078, 0.30, 0.045, 0.058, 0.042, 0.07, -0.35),
                                                    (0.015, 0.335, -0.02, 0.08, 0.042, 0.09, -0.1))):
        tuft = bake(mesh_object(f"Fur_Tuft_{idx}", bm_icosphere(1.0, 3), CHAR, MAT["skinDark"]), 0,
                    [(0.35, 0.6, "CLOUDS"), (0.12, 0.2, "STUCCI")])
        place(tuft, "head", T(x, y, z) @ R(0, 0, rz) @ S(sx, sy, sz))

    # floppy asymmetric ears
    for s, x in SIDES:
        ear = T(s * 0.235, 0.125, -0.015) @ R(0.1, s * -0.22, s * (-0.35 if s == 1 else -0.52))
        place(bake(mesh_object(f"Ear_Outer.{x}", bm_sphere(0.085), CHAR, MAT["skin"]), 1, [(0.012, 0.08, "CLOUDS")]), "head",
              ear @ T(s * 0.065, 0, 0) @ S(1.38, 0.56, 0.45))
        place(bake(mesh_object(f"Ear_Inner.{x}", bm_sphere(0.058, 20, 12), CHAR, MAT["skinDark"]), 1, [(0.008, 0.06, "CLOUDS")]),
              "head", ear @ T(s * 0.065, -0.005, 0.025) @ S(1.2, 0.42, 0.25))


def catmull_rom(points, samples):
    pts = [points[0]] + points + [points[-1]]
    out = []
    for seg in range(len(points) - 1):
        p0, p1, p2, p3 = pts[seg], pts[seg + 1], pts[seg + 2], pts[seg + 3]
        for i in range(samples):
            t = i / samples
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(points[-1])
    return out


def build_horn(side, chipped):
    """Curve with taper (point radius) and bevel, converted to mesh, then growth rings and a fractured tip."""
    ctrl = [Vector(p) for p in ((0, 0, 0), (side * 0.13, 0.02, 0.04), (side * 0.27, 0.08, 0.09), (side * 0.34, 0.21, 0.11),
                                (side * 0.31, 0.32 if chipped else 0.38, 0.08))]
    path = catmull_rom(ctrl, 12)
    lengths = [0.0]
    for a, b in zip(path, path[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    total = lengths[-1]
    cu = bpy.data.curves.new(f"Horn_Curve_{side}", "CURVE")
    cu.dimensions, cu.bevel_depth, cu.bevel_resolution, cu.use_fill_caps = "3D", 0.068, 6, True
    spline = cu.splines.new("POLY")
    spline.points.add(len(path) - 1)
    for p, pos, length in zip(spline.points, path, lengths):
        u = length / total
        p.co = (*pos, 1.0)
        p.radius = max(0.06, (1 - u * (0.72 if chipped else 0.94)) ** 0.78)
    tmp = bpy.data.objects.new(f"Horn_Curve_{side}", cu)
    SCRATCH.objects.link(tmp)
    update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    # growth rings, jagged fracture and shading attributes
    u_attr = me.attributes.new("horn_u", "FLOAT", "POINT")
    v_attr = me.attributes.new("horn_v", "FLOAT", "POINT")
    for v in me.vertices:
        i = min(range(len(path)), key=lambda k: (path[k] - v.co).length_squared)
        u = lengths[i] / total
        tangent = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        radial = v.co - path[i]
        radial -= tangent * radial.dot(tangent)
        ref = tangent.cross(Vector((0, 1, 0)) if abs(tangent.y) < 0.9 else Vector((1, 0, 0))).normalized()
        angle = math.atan2(radial.dot(tangent.cross(ref)), radial.dot(ref))
        if radial.length > 1e-5:
            n = radial.normalized()
            offset = math.sin(u * math.pi * 16) * 0.0045 * (1 - u * 0.7)
            if chipped and u > 0.86:
                offset += radial.length * (0.28 * math.sin(angle * 3.0 + i) - 0.12)
            v.co += n * offset
        u_attr.data[v.index].value = u
        v_attr.data[v.index].value = angle / (2 * math.pi) + 0.5
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.materials.append(MAT["horn"])
    horn = bpy.data.objects.new(f"Horn.{'L' if side > 0 else 'R'}", me)
    CHAR.objects.link(horn)
    disp = horn.modifiers.new("Keratin_Ridges", "DISPLACE")
    disp.texture, disp.strength, disp.texture_coords = texture("STUCCI", 0.03), 0.0025, "LOCAL"
    place(horn, "head", T(side * 0.175, 0.215, 0.01))
    ring = bake(mesh_object(f"Horn_Base_Ring.{'L' if side > 0 else 'R'}", bm_torus(0.068, 0.016, 24, 10), CHAR, MAT["skinDark"]),
                1, [(0.006, 0.03, "CLOUDS")])
    place(ring, "head", T(side * 0.175, 0.215, 0.01) @ R(0, math.pi / 2))


def build_jaw():
    place(bake(mesh_object("Lower_Jaw", bm_sphere(0.122), CHAR, MAT["snout"]), 2, [(0.008, 0.08, "CLOUDS")]), "jaw",
          T(0, -0.022, 0.055) @ S(1.26, 0.45, 0.92))
    place(bake(mesh_object("Lower_Gums", bm_torus(0.088, 0.015, 20, 8, math.pi), CHAR, MAT["gums"]), 1,
               [(0.004, 0.02, "CLOUDS")]), "jaw", T(0, 0.004, 0.052) @ R(math.pi / 2))
    for i in range(-3, 4):
        ang = (i / 3.5) * 1.1
        h = 0.032 if abs(i) == 2 else 0.022
        tooth = bake(mesh_object(f"Tooth_Lower_{i + 3}", bm_cylinder(0.0, 0.011, h * rng.uniform(0.85, 1.25), 8), CHAR,
                                 MAT["teeth"]), 1, [(0.0015, 0.01, "CLOUDS")])
        place(tooth, "jaw", T(math.sin(ang) * 0.088 + rng.uniform(-0.003, 0.003), 0.018, 0.056 + math.cos(ang) * 0.068)
              @ R(-0.1 + rng.uniform(-0.15, 0.15), rng.uniform(-0.2, 0.2), -i * 0.1 + rng.uniform(-0.15, 0.15)))
    strands = []
    for k, sx in enumerate((-0.055, 0.015, 0.065)):
        strand = mesh_object(f"Drool_Strand_{k}", bm_cylinder(0.0022, 0.0014, 0.09 + 0.02 * k, 8), CHAR, MAT["gums"])
        strands.append(place(strand, "jaw", T(sx, 0.035, 0.105)))
    return strands


# ----------------------------------------------------------------------------- limbs
def build_arm(s, x):
    place(bake(mesh_object(f"Deltoid_Sleeve.{x}", bm_sphere(0.135), CHAR, MAT["uniform"]), 1, [(0.012, 0.12, "CLOUDS")]),
          f"shoulder.{x}", S(0.95, 1.15, 0.95))
    place(bake(skin_object(f"Upper_Sleeve.{x}", [(0, 0, 0), (0, -0.21, 0), (0, -0.42, 0)], [0.105, 0.1, 0.088], CHAR,
                           MAT["uniform"]), 0, [(0.012, 0.09, "CLOUDS"), (0.004, 0.03, "STUCCI")]), f"upperarm.{x}")
    place(bake(mesh_object(f"Elbow.{x}", bm_sphere(0.086, 20, 14), CHAR, MAT["uniformDark"]), 1, [(0.006, 0.06, "CLOUDS")]),
          f"elbow.{x}")
    place(bake(skin_object(f"Forearm_Sleeve.{x}", [(0, 0, 0), (0, -0.2, 0), (0, -0.4, 0)], [0.084, 0.078, 0.066], CHAR,
                           MAT["uniform"]), 0, [(0.01, 0.08, "CLOUDS"), (0.003, 0.03, "STUCCI")]), f"elbow.{x}")
    place(bake(mesh_object(f"Cuff.{x}", bm_cylinder(0.072, 0.07, 0.065, 24), CHAR, MAT["uniformDark"]), 1,
               [(0.003, 0.05, "CLOUDS")]), f"elbow.{x}", T(0, -0.375, 0))
    hand = f"hand.{x}"
    place(bake(skin_object(f"Wrist.{x}", [(0, 0.01, 0), (0, -0.04, 0)], [0.05, 0.046], CHAR, MAT["skinDark"]), 0,
               [(0.003, 0.03, "CLOUDS")]), hand)
    palm = skin_object(f"Palm.{x}", [(0, -0.03, 0.005), (0, -0.09, 0.008), (0, -0.142, 0.004)], [0.05, 0.058, 0.055], CHAR,
                       MAT["skin"])
    place(bake(palm, 0, [(0.006, 0.04, "CLOUDS")]), hand, S(0.6, 1.0, 1.0))
    for i, f in enumerate(FINGERS):
        base, tip = f"finger{i}.{x}", f"finger{i}_tip.{x}"
        place(bake(mesh_object(f"Knuckle_{i}.{x}", bm_sphere(f["r"] * 1.2, 14, 10), CHAR, MAT["skinDark"]), 0,
                   [(0.0025, 0.02, "CLOUDS")]), base)
        place(bake(skin_object(f"Finger_{i}_Proximal.{x}", [(0, 0, 0), (0, -f["len"] * 0.32, 0), (0, -f["len"] * 0.65, 0)],
                               [f["r"], f["r"] * 0.88, f["r"] * 1.05], CHAR, MAT["skin"]), 0, [(0.002, 0.02, "CLOUDS")]), base)
        place(bake(skin_object(f"Finger_{i}_Distal.{x}", [(0, 0, 0), (0, -f["len"] * 0.25, 0), (0, -f["len"] * 0.48, 0)],
                               [f["r"] * 0.9, f["r"] * 0.78, f["r"] * 0.62], CHAR, MAT["skinDark"]), 0,
                   [(0.0018, 0.02, "CLOUDS")]), tip)
        place(mesh_object(f"Claw_{i}.{x}", bm_cylinder(0.0, f["r"] * 0.7, 0.03, 8), CHAR, MAT["horn"]), tip,
              T(s * -0.004, -f["len"] * 0.52, 0) @ R(math.pi))
    thumb = T(s * -0.022, -0.088, 0.058) @ R(0.45, s * -0.35, s * -0.35)
    place(bake(skin_object(f"Thumb_Proximal.{x}", [(0, 0, 0), (0, -0.056, 0)], [0.018, 0.016], CHAR, MAT["skin"]), 0,
               [(0.002, 0.02, "CLOUDS")]), hand, thumb)
    thumb_tip = thumb @ T(0, -0.058, 0) @ R(0, 0, s * -0.42)
    place(bake(skin_object(f"Thumb_Distal.{x}", [(0, 0, 0), (0, -0.042, 0)], [0.015, 0.011], CHAR, MAT["skinDark"]), 0,
               [(0.0015, 0.02, "CLOUDS")]), hand, thumb_tip)
    place(mesh_object(f"Thumb_Claw.{x}", bm_cylinder(0.0, 0.009, 0.026, 8), CHAR, MAT["horn"]), hand,
          thumb_tip @ T(0, -0.05, 0) @ R(math.pi))


def build_leg(s, x):
    hip, knee, boot = f"hip.{x}", f"knee.{x}", f"boot.{x}"
    place(bake(skin_object(f"Thigh_Trouser.{x}", [(0, 0.02, 0), (0, -0.22, 0.005), (0, -0.44, 0)], [0.135, 0.13, 0.112], CHAR,
                           MAT["uniformDark"]), 0, [(0.014, 0.1, "CLOUDS"), (0.004, 0.03, "STUCCI")]), hip)
    place(bake(mesh_object(f"Knee_Cap.{x}", bm_sphere(0.106, 20, 14), CHAR, MAT["uniformDark"]), 1, [(0.008, 0.08, "CLOUDS")]), knee)
    place(bake(skin_object(f"Shin_Trouser.{x}", [(0, 0, 0), (0, -0.18, 0), (0, -0.36, 0)], [0.108, 0.1, 0.095], CHAR,
                           MAT["uniformDark"]), 0, [(0.01, 0.09, "CLOUDS"), (0.003, 0.03, "STUCCI")]), knee)
    place(bake(mesh_object(f"Trouser_Fold.{x}", bm_torus(0.095, 0.022, 24, 10), CHAR, MAT["uniform"]), 1,
               [(0.006, 0.04, "CLOUDS")]), knee, T(0, -0.34, 0) @ R(math.pi / 2))
    place(bake(mesh_object(f"Boot_Shaft.{x}", bm_cylinder(0.088, 0.094, 0.16, 24), CHAR, MAT["leather"]), 1,
               [(0.005, 0.05, "CLOUDS")]), boot, T(0, 0.02, -0.02))
    place(bake(mesh_object(f"Boot_Vamp.{x}", bm_box(0.18, 0.105, 0.28), CHAR, MAT["leather"]), 2,
               [(0.008, 0.06, "CLOUDS"), (0.003, 0.02, "STUCCI")]), boot, T(0, -0.045, 0.03))
    place(bake(mesh_object(f"Boot_Toe_Cap.{x}", bm_sphere(0.092, 24, 14), CHAR, MAT["leather"]), 1, [(0.005, 0.05, "CLOUDS")]),
          boot, T(0, -0.048, 0.155) @ S(0.98, 0.62, 1.05))
    place(hard_surface(mesh_object(f"Boot_Sole.{x}", bm_box(0.194, 0.042, 0.34), CHAR, MAT["rubber"]), 0.01, 3), boot,
          T(0, -0.098, 0.048))
    lugs = mesh_object(f"Boot_Tread.{x}", bm_box(0.18, 0.012, 0.02), CHAR, MAT["rubber"])
    arr = lugs.modifiers.new("Tread_Array", "ARRAY")
    arr.count, arr.use_relative_offset, arr.use_constant_offset = 9, False, True
    arr.constant_offset_displace = (0, 0, 0.036)
    hard_surface(lugs, 0.003, 2)
    place(lugs, boot, T(0, -0.124, 0.048 - 0.144))


# ----------------------------------------------------------------------------- armature and day/night poses
def build_rig():
    arm = bpy.data.armatures.new("SentryBull_Rig")
    rig = bpy.data.objects.new("SentryBull_Rig", arm)
    RIGC.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit = {}
    for name in ORDER:
        eb = arm.edit_bones.new(name)
        head = to_blender(pivot_world(name))
        length = 0.02 if "finger" in name else 0.06
        eb.head, eb.tail = head, head + Vector((0, 0, length))
        eb.align_roll(Vector((0, -1, 0)))  # bone axes == reference axes (x, y-up, z-forward)
        parent = PIVOTS[name][0]
        if parent:
            eb.parent = edit[parent]
        edit[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "ZYX"  # same Euler convention as the reference
    arm.display_type = "STICK"
    rig.show_in_front = True
    update()
    m = rig.data.bones["spine"].matrix_local.to_3x3()
    assert (m.col[0] - Vector((1, 0, 0))).length < 1e-4 and (m.col[2] - Vector((0, -1, 0))).length < 1e-4, "bone axes"
    for o, bone in ATTACH:
        world = o.matrix_world.copy()
        o.parent, o.parent_type, o.parent_bone = rig, "BONE", bone
        o.matrix_world = world
    return rig


def pose_values(c):
    """Reference rig.update(c) idle pose as {bone: (location offset, euler, scale)}."""
    jo = max(0.0, (c - 0.18) / 0.82)
    head_s = lerp(1.06, 0.96, c)
    P = {
        "pelvis": ((0, lerp(0.98, 0.94, c) - 0.98, 0), (lerp(0, 0.05, c), 0, 0), (1, 1, 1)),
        "spine": ((0, 0, 0), (lerp(0, 0.26, c), 0, lerp(0, -0.03, c)), (1, 1, 1)),
        "torso_morph": ((0, 0, 0), (0, 0, 0), (lerp(1.16, 0.94, c), lerp(0.95, 1.08, c), lerp(0.86, 0.72, c))),
        "hunch_morph": ((0, 0, lerp(-0.08, -0.14, c) + 0.08), (0, 0, 0), (lerp(1.0, 1.28, c), lerp(0.75, 0.98, c), lerp(0.62, 0.85, c))),
        "neck": ((0, 0, 0), (lerp(0, -0.15, c), 0, lerp(0, 0.06, c)), (1, 1, 1)),
        "head": ((0, 0, 0), (lerp(0, -0.06, c), lerp(0, -0.06, c), lerp(0, 0.12, c)), (head_s, head_s, head_s)),
        "jaw": ((0, lerp(-0.042, -0.068, jo) + 0.042, 0), (lerp(0.02, 0.56, jo), 0, 0), (1, 1, 1)),
    }
    for s, x in SIDES:
        left = s == 1
        P[f"shoulder.{x}"] = ((s * (lerp(0.42, 0.36, c) - 0.42), lerp(0.73, 0.71, c) - 0.73, lerp(-0.01, 0.04, c) + 0.01),
                              (lerp(0.04, -0.18 if left else -0.14, c), 0, s * lerp(0.18, 0.11 if left else 0.12, c)), (1, 1, 1))
        thick, length = lerp(1.06, 0.88, c), lerp(0.88, 1.24, c)
        P[f"upperarm.{x}"] = ((0, 0, 0), (0, 0, 0), (thick, length, thick))
        P[f"elbow.{x}"] = ((0, 0, 0), (lerp(-0.18, -0.34 if left else -0.38, c), 0, s * lerp(-0.08, -0.14, c)), (1, 1, 1))
        P[f"hand.{x}"] = ((0, 0, 0), (lerp(0, 0.14 if left else 0.16, c), 0, s * lerp(0, -0.12, c)), (1, 1, 1))
        for i, f in enumerate(FINGERS):
            crook = 0.05 * math.sin(i * 2.3 + s)  # asymmetric crooked fingers
            P[f"finger{i}.{x}"] = ((0, 0, 0), (f["spread"], 0, s * -(lerp(0.12, 0.34, c) + crook)), (1, 1, 1))
            P[f"finger{i}_tip.{x}"] = ((0, 0, 0), (0, 0, s * -(lerp(0.24, 0.62, c) + crook * 1.2)), (1, 1, 1))
        P[f"hip.{x}"] = ((0, 0, 0), (lerp(0, -0.09 if left else -0.05, c), 0, s * lerp(0.02, 0.05, c)), (1, 1, 1))
        P[f"knee.{x}"] = ((0, 0, 0), (lerp(0, 0.14 if left else 0.10, c), 0, 0), (1, 1, 1))
        P[f"boot.{x}"] = ((0, 0, 0), (lerp(0, -0.05, c), 0, 0), (1, 1, 1))
    return P


def key_pose(rig, c, frame):
    values = pose_values(c)
    for pb in rig.pose.bones:
        loc, rot, scl = values.get(pb.name, ((0, 0, 0), (0, 0, 0), (1, 1, 1)))
        pb.location, pb.rotation_euler, pb.scale = loc, rot, scl
        for path in ("location", "rotation_euler", "scale"):
            pb.keyframe_insert(path, frame=frame)


def fcurves(id_block):
    ad = id_block.animation_data
    if not ad or not ad.action:
        return []
    action = ad.action
    try:
        return list(action.fcurves)  # up to 4.x
    except AttributeError:
        pass
    try:
        from bpy_extras import anim_utils
        bag = anim_utils.action_get_channelbag_for_slot(action, ad.action_slot)
        return list(bag.fcurves) if bag else []
    except (ImportError, AttributeError):
        out = []
        for layer in getattr(action, "layers", []):
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out += list(bag.fcurves)
        return out


def key_toggle(objects, visible_at_day, visible_at_night):
    for o in objects:
        for frame, visible in ((FRAME_DAY, visible_at_day), (FRAME_NIGHT, visible_at_night)):
            o.hide_render = o.hide_viewport = not visible
            o.keyframe_insert("hide_render", frame=frame)
            o.keyframe_insert("hide_viewport", frame=frame)


def key_eye_glow():
    strength, color = emission_socket(MAT["iris"])
    for frame, c in ((FRAME_DAY, 0.0), (FRAME_NIGHT, NIGHT_SHIFT)):
        strength.default_value = lerp(0.05, 2.8, 0.45) * lerp(0.22, 1.15, c) * 5.0
        color.default_value = (1.0, 0.70, 0.28, 1.0) if c > 0.4 else (0.48, 0.59, 0.72, 1.0)
        strength.keyframe_insert("default_value", frame=frame)
        color.keyframe_insert("default_value", frame=frame)


def key_led_blink(lamp):
    strength, _ = emission_socket(MAT["led"])
    for frame, on in ((1, True), (9, False)):
        strength.default_value = 14.0 if on else 0.6
        lamp.energy = 0.6 if on else 0.03
        strength.keyframe_insert("default_value", frame=frame)
        lamp.keyframe_insert("energy", frame=frame)
    for block in (MAT["led"].node_tree, lamp):
        for fc in fcurves(block):
            for kp in fc.keyframe_points:
                kp.interpolation = "CONSTANT"
            fc.modifiers.new("CYCLES")


# ----------------------------------------------------------------------------- environment, lights, cameras, render
def build_environment():
    floor = mesh_object("Basement_Floor", bm_box(16, 0.02, 16), ENV, MAT["floor"])
    floor.matrix_world = C4 @ T(0, -0.01, 0)
    wall = mesh_object("Back_Wall", bm_box(10, 5.2, 0.3), ENV, MAT["wall"])
    wall.matrix_world = C4 @ T(0, 2.6, -2.6)
    for s in (-1, 1):
        pillar = hard_surface(mesh_object(f"Pillar_{s}", bm_box(0.45, 5.2, 0.45), ENV, MAT["wall"]), 0.02)
        pillar.matrix_world = C4 @ T(s * 2.4, 2.6, -2.2)
    for k, (y, z) in enumerate(((3.7, -2.35), (3.86, -2.28), (0.35, -2.36))):
        pipe = mesh_object(f"Pipe_{k}", bm_cylinder(0.035, 0.035, 7.5, 16), ENV, MAT["pipe"])
        pipe.matrix_world = C4 @ T(0, y, z) @ R(0, 0, math.pi / 2)
    door = hard_surface(mesh_object("Door_Frame", bm_box(1.55, 2.85, 0.25), ENV, MAT["pipe"]), 0.01)
    door.matrix_world = C4 @ T(1.55, 1.425, -2.42)
    void = mesh_object("Door_Void", bm_box(1.32, 2.68, 0.28), ENV, MAT["cavity"])
    void.matrix_world = C4 @ T(1.55, 1.34, -2.42)
    fixture = T(0, 4.15, 0.25)
    housing = hard_surface(mesh_object("Fixture_Housing", bm_box(1.45, 0.09, 0.32), ENV, MAT["pipe"]), 0.01)
    housing.matrix_world = C4 @ fixture
    for k, z in enumerate((0.06, -0.06)):
        tube = mesh_object(f"Fluorescent_Tube_{k}", bm_cylinder(0.022, 0.022, 1.3, 16), ENV, MAT["tube"])
        tube.matrix_world = C4 @ fixture @ T(0, -0.055, z) @ R(0, 0, math.pi / 2)
    for k, rx in enumerate((-0.6, 0.6)):
        rod = mesh_object(f"Fixture_Rod_{k}", bm_cylinder(0.012, 0.012, 1.2, 8), ENV, MAT["pipe"])
        rod.matrix_world = C4 @ fixture @ T(rx, 0.62, 0)
    fog = mesh_object("Volumetric_Fog", bm_box(12, 6.4, 10), ENV, MAT["fog"], smooth=False)
    fog.matrix_world = C4 @ T(0, 3.2, 1.5)
    fog.visible_shadow = False
    return fog


def light(name, kind, color, energy, pos, target=None, **settings):
    data = bpy.data.lights.new(name, kind)
    data.color, data.energy = color, energy
    for key, value in settings.items():
        setattr(data, key, value)
    obj = bpy.data.objects.new(name, data)
    LIGHTS.objects.link(obj)
    obj.location = to_blender(pos)
    if target is not None:
        aim(obj, to_blender(target))
    return obj


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def hex_rgb(h):
    return tuple(((h >> s) & 255) / 255 for s in (16, 8, 0))


def build_lights():
    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    SCENE.world = world
    enable_nodes(world)
    bg =next((n for n in world.node_tree.nodes if n.type == "BACKGROUND"), None)
    if bg:
        bg.inputs["Color"].default_value = (0.002, 0.0026, 0.004, 1.0)
        bg.inputs["Strength"].default_value = 1.0
    key = light("Key_Fluorescent", "AREA", hex_rgb(0xC8E2FF), 320.0, (0, 4.05, 0.25), (0, 0, 0.25),
                shape="RECTANGLE", size=1.35, size_y=0.28)
    light("Key_Spot_Pool", "SPOT", hex_rgb(0xCFE8FF), 300.0, (0.18, 4.05, 0.55), (0, 1.15, 0),
          spot_size=math.radians(52), spot_blend=0.68, shadow_soft_size=0.15)
    light("Rim_Sodium", "AREA", hex_rgb(0xFF9442), 260.0, (-1.6, 2.5, -1.9), (0, 1.8, 0), size=0.8)
    light("Rim_Cold_Teal", "AREA", hex_rgb(0x2C5D72), 140.0, (2.3, 1.9, -2.0), (0, 1.5, 0), size=1.2)
    light("Under_Red", "POINT", hex_rgb(0x8A1C1C), 45.0, (0.85, 0.42, 1.05), shadow_soft_size=0.2)
    light("Fill_Bounce", "AREA", (0.55, 0.66, 0.85), 22.0, (0.4, 1.7, 4.2), (0, 1.5, 0), size=3.0)
    # flickering fluorescent: keyed energy + noise modifier
    key.data.keyframe_insert("energy", frame=1)
    for fc in fcurves(key.data):
        noise = fc.modifiers.new("NOISE")
        noise.strength, noise.scale, noise.phase = 260.0, 3.0, 7.0
    return key


def camera(name, lens, pos, target, ortho=None):
    data = bpy.data.cameras.new(name)
    data.lens, data.clip_start = lens, 0.05
    if ortho:
        data.type, data.ortho_scale = "ORTHO", ortho
    obj = bpy.data.objects.new(name, data)
    LIGHTS.objects.link(obj)
    obj.location = pos
    aim(obj, target)
    return obj


def build_cameras(rig):
    SCENE.frame_set(FRAME_NIGHT)
    update()
    head = rig.matrix_world @ rig.pose.bones["head"].matrix.translation
    face_target = head + Vector((0, -0.24, 0.06))  # between sunken eyes and stitched snout
    hero = camera("Cam_Hero", 35, to_blender((0.95, 0.72, 3.25)), to_blender((0, 1.5, 0.15)))
    camera("Cam_Face_Closeup", 85, face_target + Vector((0.3, -1.75, -0.12)), face_target)
    camera("Cam_Turnaround", 50, to_blender((0, 1.55, 8.0)), to_blender((0, 1.55, 0)), ortho=3.2)
    SCENE.camera = hero


def setup_render(preview):
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    if USE_EEVEE:
        SCENE.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    else:
        SCENE.render.engine = "CYCLES"
        cycles = SCENE.cycles
        cycles.samples = 64 if preview else HERO_SAMPLES
        cycles.use_adaptive_sampling = True
        cycles.use_denoising = True
        cycles.volume_step_rate = 2.0 if preview else 1.0
        cycles.device = "CPU"
        try:
            prefs = bpy.context.preferences.addons["cycles"].preferences
            for backend in ("OPTIX", "CUDA", "HIP", "METAL", "ONEAPI"):
                try:
                    prefs.compute_device_type = backend
                except TypeError:
                    continue
                prefs.get_devices()
                gpus = [d for d in prefs.devices if d.type != "CPU"]
                if gpus:
                    for d in prefs.devices:
                        d.use = True
                    cycles.device = "GPU"
                    break
        except (KeyError, AttributeError):
            pass
    SCENE.render.resolution_x, SCENE.render.resolution_y = 1080, 1350
    SCENE.render.resolution_percentage = 50 if preview else 100
    view = SCENE.view_settings
    view.view_transform = "AgX"
    for look in ("AgX - Punchy", "AgX - High Contrast", "Punchy", "High Contrast"):
        try:
            view.look = look
            break
        except TypeError:
            continue
    view.exposure = 0.0


def render_stills(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    shots = [("Cam_Hero", FRAME_NIGHT, "hero"), ("Cam_Face_Closeup", FRAME_NIGHT, "face"),
             ("Cam_Turnaround", FRAME_NIGHT, "night_turnaround"), ("Cam_Turnaround", FRAME_DAY, "day_turnaround")]
    for cam, frame, label in shots:
        SCENE.camera = bpy.data.objects[cam]
        SCENE.frame_set(frame)
        SCENE.render.filepath = os.path.join(out_dir, f"sentry_bull_{label}.png")
        bpy.ops.render.render(write_still=True)
    SCENE.camera = bpy.data.objects["Cam_Hero"]
    SCENE.frame_set(FRAME_NIGHT)


# ----------------------------------------------------------------------------- game export (--unity FILE.fbx)
# Hollow House's GuardEncounterBuilder needs: a Generic rig with bones "head", "face_eye*", thigh/shin/foot .L/.R,
# and clips named exactly Idle, Walk, Run, LookAround, FaceActing. MonsterSkin paints every slot black except
# materials whose name contains "FaceDark", which glow red, so the game mesh only needs those two materials.
GAME_BONES = {f"{old}.{x}": f"{new}.{x}" for _, x in SIDES for old, new in (("hip", "thigh"), ("knee", "shin"), ("boot", "foot"))}
GAME_FPS = 24
GAME_MAX_FACES = 4000  # per part, after decimation


def game_pose(c, deltas=None):
    """Night-shift pose (renamed bones) with additive per-frame offsets {bone: (dloc, drot)}."""
    pose = {GAME_BONES.get(k, k): v for k, v in pose_values(c).items()}
    for bone, (dloc, drot) in (deltas or {}).items():
        loc, rot, scl = pose.get(bone, ((0, 0, 0), (0, 0, 0), (1, 1, 1)))
        pose[bone] = (tuple(a + b for a, b in zip(loc, dloc)), tuple(a + b for a, b in zip(rot, drot)), scl)
    return pose


def clip_deltas(clip, phase):
    """Procedural loops on top of the hunched night pose; phase runs 0..2pi over the clip."""
    s, c2 = math.sin(phase), math.sin(2 * phase)
    d = {}
    if clip == "Idle":
        d["spine"] = ((0, 0, 0), (0.025 * s, 0, 0))
        d["head"] = ((0, 0, 0), (0.02 * c2, 0.03 * s, 0.04 * math.sin(3 * phase)))
        d["jaw"] = ((0, 0, 0), (0.05 * abs(s), 0, 0))
        for _, x in SIDES:
            for i in range(len(FINGERS)):
                d[f"finger{i}_tip.{x}"] = ((0, 0, 0), (0, 0, 0.08 * math.sin(3 * phase + i)))
    elif clip in ("Walk", "Run"):
        k = 1.0 if clip == "Walk" else 1.6
        lean = 0.0 if clip == "Walk" else 0.22
        d["pelvis"] = ((0, 0.02 * k * math.cos(2 * phase), 0), (0, 0.06 * k * s, 0))
        d["spine"] = ((0, 0, 0), (lean + 0.03 * math.cos(2 * phase), -0.08 * k * s, 0))
        d["head"] = ((0, 0, 0), (-lean * 0.6, 0.05 * k * s, 0.05 * c2))
        d["jaw"] = ((0, 0, 0), (0.1 * (k - 1), 0, 0))
        for side, x in SIDES:
            swing = math.sin(phase + (0 if side > 0 else math.pi))
            d[f"thigh.{x}"] = ((0, 0, 0), (0.42 * k * swing, 0, 0))
            d[f"shin.{x}"] = ((0, 0, 0), (0.5 * k * max(0.0, -math.cos(phase + (0 if side > 0 else math.pi))), 0, 0))
            d[f"shoulder.{x}"] = ((0, 0, 0), (-0.35 * k * swing - lean, 0, 0))
            d[f"elbow.{x}"] = ((0, 0, 0), (-0.15 * k * (1 + swing), 0, 0))
    elif clip == "LookAround":
        d["neck"] = ((0, 0, 0), (0, 0.6 * s, 0))
        d["head"] = ((0, 0, 0), (0.05 * c2, 0.35 * s, 0.18 * math.sin(3 * phase)))
        d["spine"] = ((0, 0, 0), (0, 0.15 * s, 0))
    elif clip == "FaceActing":
        d["head"] = ((0, 0, 0), (-0.08 * abs(s), 0, 0.3 * s))
        d["neck"] = ((0, 0, 0), (-0.1, 0, 0.1 * c2))
        d["jaw"] = ((0, 0, 0), (0.35 * abs(c2), 0, 0))
        for _, x in SIDES:
            d[f"hand.{x}"] = ((0, 0, 0), (0.2 * abs(s), 0, 0))
    return d


GAME_CLIPS = {"Idle": 48, "Walk": 32, "Run": 20, "LookAround": 96, "FaceActing": 72}  # frames per loop


def game_export(rig, path):
    SCENE.render.fps = GAME_FPS
    if rig.animation_data:
        rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_euler, pb.scale = (0, 0, 0), (0, 0, 0), (1, 1, 1)
    for old, new in GAME_BONES.items():
        rig.data.bones[old].name = new
    # face_eye bones at the eyeballs: the builder reads them for facing and vision height
    update()
    eyes = {x: bpy.data.objects[f"Eyeball.{x}"].matrix_world.translation.copy() for _, x in SIDES}
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for _, x in SIDES:
        eb = rig.data.edit_bones.new(f"face_eye.{x}")
        eb.head, eb.tail = eyes[x], eyes[x] + Vector((0, 0, 0.02))
        eb.align_roll(Vector((0, -1, 0)))
        eb.parent = rig.data.edit_bones["head"]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "ZYX"
    update()

    body_mat = bpy.data.materials.new("SentryBull_Body")
    body_mat.diffuse_color = (0.03, 0.03, 0.035, 1)
    glow_mat = bpy.data.materials.new("SentryBull_FaceDark")
    glow_mat.diffuse_color = (1, 0.15, 0.08, 1)
    glowing = {MAT["iris"].name, MAT["led"].name}
    dg = bpy.context.evaluated_depsgraph_get()
    parts = []
    for o in [o for o in bpy.data.objects if o.parent == rig and o.parent_type == "BONE"]:
        if o.type not in {"MESH", "CURVE", "FONT"} or o.name == "Mascot_Smile_Line":
            bpy.data.objects.remove(o)
            continue
        if o.type == "MESH" and len(o.data.polygons) > GAME_MAX_FACES:
            dec = o.modifiers.new("Game_Decimate", "DECIMATE")
            dec.ratio = GAME_MAX_FACES / len(o.data.polygons)
            update()
            dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        for i, m in enumerate(me.materials):
            me.materials[i] = glow_mat if m and m.name in glowing else body_mat
        if len(me.materials) == 0:
            me.materials.append(body_mat)
        part = bpy.data.objects.new(o.name + "_game", me)
        CHAR.objects.link(part)
        part.matrix_world = o.matrix_world.copy()
        part.vertex_groups.new(name=GAME_BONES.get(o.parent_bone, o.parent_bone)).add(range(len(me.vertices)), 1.0, "REPLACE")
        parts.append(part)
        bpy.data.objects.remove(o)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = body.data.name = "SentryBull_Mesh"
    world = body.matrix_world.copy()
    body.parent = rig
    body.matrix_world = world
    body.modifiers.new("Armature", "ARMATURE").object = rig

    # one action per clip, played as named NLA strips so the FBX takes are "Idle", "Walk", ...
    if rig.animation_data is None:
        rig.animation_data_create()
    for clip, frames in GAME_CLIPS.items():
        action = bpy.data.actions.new(clip)
        rig.animation_data.action = action
        for f in range(0, frames + 1, 2):
            values = game_pose(NIGHT_SHIFT, clip_deltas(clip, 2 * math.pi * f / frames))
            for pb in rig.pose.bones:
                loc, rot, scl = values.get(pb.name, ((0, 0, 0), (0, 0, 0), (1, 1, 1)))
                pb.location, pb.rotation_euler, pb.scale = loc, rot, scl
                for prop in ("location", "rotation_euler", "scale"):
                    pb.keyframe_insert(prop, frame=f + 1)
        rig.animation_data.action = None
        track = rig.animation_data.nla_tracks.new()
        track.name = clip
        track.strips.new(clip, 1, action)
    for o in bpy.data.objects:
        o.select_set(o in (rig, body))
    bpy.context.view_layer.objects.active = rig
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(path), use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=True, bake_anim_simplify_factor=0.5,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
    print(f"Sentry Bull game FBX: {len(body.data.polygons)} faces, {len(body.data.materials)} materials, "
          f"{len(rig.data.bones)} bones -> {path}")


# ----------------------------------------------------------------------------- build
def main():
    build_body()
    build_uniform_props()
    head = build_head()
    build_face_details(head)
    build_horn(1, False)
    build_horn(-1, True)
    drool = build_jaw()
    smile = bpy.data.objects["Mascot_Smile_Line"]
    for s, x in SIDES:
        build_arm(s, x)
        build_leg(s, x)
    rig = build_rig()
    if arg("--unity"):
        bpy.data.collections.remove(SCRATCH)
        game_export(rig, arg("--unity"))
        return
    key_pose(rig, 0.0, FRAME_DAY)
    key_pose(rig, NIGHT_SHIFT, FRAME_NIGHT)
    key_toggle([smile], True, NIGHT_SHIFT < 0.38)
    key_toggle(drool, False, max(0.0, (NIGHT_SHIFT - 0.18) / 0.82) > 0.35)
    key_eye_glow()
    key_led_blink(bpy.data.objects["LED_Glow"].data)
    build_environment()
    build_lights()
    build_cameras(rig)
    bpy.data.collections.remove(SCRATCH)
    setup_render("--preview" in ARGS)
    SCENE.frame_set(FRAME_NIGHT)
    print(f"Sentry Bull built: {len(CHAR.objects)} character parts, {len(PROPS.objects)} props, "
          f"{len(rig.pose.bones)} bones, engine {SCENE.render.engine}")
    if arg("--save"):
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(arg("--save")))
    if arg("--render"):
        render_stills(os.path.abspath(arg("--render")))


main()
