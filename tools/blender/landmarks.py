# Landmarks, modelled and rendered to pixel art (see pixelrender.py): Pip's stall (open and covered for the night), the
# old wreck on Saltmere's islet, Frostfang's cave mouth, Bakawan's pagatpat (the firefly trees), and Marilaya's tide
# post, survey cairn, spring-water dryer and limestone rock face. All in LandmarkArt.cs.
# Run:  blender -b --python tools/blender/landmarks.py -- <out_dir> [cs_path]
#
# Each is laid out on its old hand drawing round an anchor on the ground (given per model below), x across, y back from
# the viewer, heights in rows, so the game's own overlays (Pip, the hatch, the dryer's glow, the tide's waterline, the
# fireflies) still land where they did, scaled by the model's zoom.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector
from pixelrender import Renderer, args, SPECIAL_KEYS

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)
PITCH = 16


def box(r, x0, x1, y0, y1, z0, z1, mat, rot=None):
    o = r.box((x0 + x1) / 2, (y0 + y1) / 2, z0, x1 - x0, y1 - y0, z1 - z0, mat)
    if rot:
        o.rotation_euler = [math.radians(v) for v in rot]
    return o


def blob(r, x, y, z, rad, mat, sx=1.0, sy=1.0, sz=1.0, rough=0.0, seed=0, sub=3):
    """A lumpy rock or leaf clump: an ico sphere at height z (rows) squashed by sx/sy/sz, roughened by a noise."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=rad, location=(x, y, r.Z(z)))
    o = bpy.context.object
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)   # so the roughness is in pixels, not stretched
    if rough:
        tex = bpy.data.textures.get("rough%d" % seed) or bpy.data.textures.new("rough%d" % seed, "CLOUDS")
        tex.noise_scale = 1.4
        d = o.modifiers.new("rough", "DISPLACE")
        d.texture = tex
        d.strength = rough
        d.texture_coords = "GLOBAL"
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


results = []


def finish(name, r, anchor=(0, 0, 0), extra=None):
    grid = r.render(OUT)
    ax, ay = r.anchor(anchor)
    r.preview(os.path.join(OUT, f"{name}.png"), [grid], special_colours=("#7a6a40", "#a89060", "#c8b080"), back=(120, 160, 100), scale=6)
    results.append((name, grid, ax, ay, r, extra or {}))


WOOD = {"post": ("#4e3420", "#6b4a2b", "#86603a"), "plank": ("#7a5230", "#9a6a3a", "#b58250"),
        "seam": ("#5e3e22", "#7a5230", "#8a6038")}

# ---- Pip's stall (World.cs DrawStall). Anchor: the middle of the counter's front on the ground, (PipX, PipY + 9). Posts at
# x -10..-8 and 8..10 up to row 22, the counter x -10..10 rows 0..8 with a gold sign, goods on top, and a red and white
# awning (rows 19..24) sloping down toward you. Pip stands behind the counter (the game draws him first).
for covered in (False, True):
    r = Renderer(int(30 * 1.25), int(34 * 1.25), pitch=PITCH, zoom=1.25, tones={**WOOD,
        "red": ("#a8342a", "#d8483a", "#f06a58"), "white": ("#c9c3b4", "#ece7dc", "#faf8f2"),
        "sign": ("#b88a2a", "#f3c25b", "#ffe08a"), "jar": ("#6a98aa", "#9fc3d1", "#cfe6ee"),
        "crate": ("#a87a2a", "#e8b04a", "#f6d07a"), "scale": ("#6e7478", "#9aa0a5", "#c4cacd"),
        "fish": ("#c06a78", "#e8939a", "#f6c0c4"), "canvas": ("#8f7a54", "#b9a27a", "#d8c6a0")}, lit_at=(0.66, 0.0))
    M = r.ids
    for x in (-10, 8):
        for y in (0.5, 6.5):
            box(r, x, x + 2, y, y + 1.5, 0, 22, M["post"])
    box(r, -10.5, 10.5, 0, 6, 0, 8, r.banded("planks", "Z", r.Z(2.6), "plank", "seam", 0.62))
    box(r, -11, 11, -0.4, 6.4, 8, 9, M["plank"])                               # the counter top
    box(r, -5, 5, -0.6, 0.0, 2, 5, M["sign"])                                 # the price board on the front
    if covered:
        box(r, -10, 10, -0.2, 6.2, 9, 12, M["canvas"])
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=1.6, depth=r.Z(3), location=(-6, 2.5, r.Z(10.5)))
        bpy.context.object.data.materials.append(M["jar"])
        box(r, -2, 2, 1.5, 4.5, 9, 11.5, M["crate"])
        box(r, 4, 8, 1.5, 4.5, 9, 11, M["scale"])
        blob(r, 6, 2, 12, 1.0, M["fish"], sx=1.8, sy=0.7, sz=0.6)
    # The awning: a striped canvas sloping toward you, scalloped along its front edge.
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in ((-12.5, 8.5, r.Z(25)), (12.5, 8.5, r.Z(25)), (12.5, -2.5, r.Z(19.5)), (-12.5, -2.5, r.Z(19.5)))]
    bm.faces.new(vs)
    me = bpy.data.meshes.new("awning")
    bm.to_mesh(me)
    bm.free()
    aw = bpy.data.objects.new("awning", me)
    r.scene.collection.objects.link(aw)
    aw.data.materials.append(r.banded("stripes", "X", 4.0, "red", "white", 0.5))
    aw.modifiers.new("t", "SOLIDIFY").thickness = 0.8
    for k in range(6):
        blob(r, -10.4 + k * 4.2, -2.6, 19.2, 1.2, M["red" if k % 2 == 0 else "white"], sx=1.6, sy=0.5, sz=1.0, sub=2)
    r.aim((0, 3, 13))
    finish("StallCovered" if covered else "Stall", r)

# ---- The wreck (World.cs DrawWreck). Anchor: the middle of its hull on the sand, (27, 156). An old boat listing on its
# side half sunk in the sand, its planks sprung, ribs showing at the stern, and a broken mast with a rag of sail. Its
# deck is flat at row 9 over x 0..7, where the game draws the hatch.
r = Renderer(40, 32, pitch=PITCH, tones={**WOOD,
    "hull": ("#3e2618", "#5b3a24", "#7a5230"), "hulldk": ("#2e1c12", "#4a2f1d", "#5e3c26"),
    "rag": ("#a89c86", "#d9d2bf", "#efe8d8"), "hole": ("#120b07", "#1b120c", "#26180f")}, lit_at=(0.66, 0.0))
M = r.ids
r.water()                                            # the sand hides what's under it
bm = bmesh.new()
L, B = 14.0, 5.5
rings = []
for i in range(31):
    x = -L + 2 * L * i / 30
    k = abs(x / L)
    w = B * max(0.05, 1 - k ** 2.4) ** 0.6
    top = 9.0 + 2.5 * k ** 3
    row = []
    for j in range(9):
        t = math.pi * j / 8
        row.append(bm.verts.new((x, -math.cos(t) * w, r.Z(top) - (r.Z(top) + 3) * math.sin(t) ** 0.8)))
    rings.append(row)
for i in range(30):
    for j in range(8):
        bm.faces.new((rings[i][j], rings[i + 1][j], rings[i + 1][j + 1], rings[i][j + 1]))
me = bpy.data.meshes.new("hull")
bm.to_mesh(me)
bm.free()
hull = bpy.data.objects.new("hull", me)
r.scene.collection.objects.link(hull)
hull.data.materials.append(r.banded("strakes", "Z", r.Z(2.0), "hull", "hulldk", 0.7))
hull.modifiers.new("t", "SOLIDIFY").thickness = 0.6
hull.rotation_euler = (math.radians(10), math.radians(-6), math.radians(-18))   # listing toward you, bow up, turned a little
box(r, -9, 9, -4, 4, 8.6, 9.2, M["plank"])           # what's left of the deck
for x, z, w in ((-8, 5, 3), (4, 4, 4)):
    box(r, x, x + w, -6.2, -5.4, z, z + 2, M["hole"])  # sprung planks, dark holes in the side
for k in range(4):                                   # ribs standing bare at the stern
    x = -12.5 + k * 1.6
    bpy.ops.mesh.primitive_torus_add(major_radius=4.6 - k * 0.5, minor_radius=0.35, location=(x, 0, r.Z(7)), rotation=(0, math.radians(90), 0))
    bpy.context.object.scale = (1, 1, 1.3)
    bpy.context.object.data.materials.append(M["post"])
r.rod((-1, 0, 9), (6, 0, 24), 0.55, M["post"])       # the mast, leaning
r.rod((3.5, 0, 20), (9, 0, 20), 0.3, M["post"])      # its broken yard
bm = bmesh.new()
vs = [bm.verts.new((x, -0.3, r.Z(z))) for x, z in ((5, 19.6), (9, 19.6), (8.6, 15.5), (7.4, 16.8), (6.6, 14.6), (5.4, 16))]
bm.faces.new(vs)
me = bpy.data.meshes.new("rag")
bm.to_mesh(me)
bm.free()
rag = bpy.data.objects.new("rag", me)
r.scene.collection.objects.link(rag)
rag.data.materials.append(M["rag"])
rag.modifiers.new("t", "SOLIDIFY").thickness = 0.3
r.aim((0, 0, 12))
finish("Wreck", r)

# ---- Frostfang's cave mouth (World.cs DrawCaveMouth). Anchor: the middle of the foot of the hill, (MouthX * 10 + 10,
# MouthY * 10 + 20). A rocky hill a quarter bigger than the old drawing, with a ragged arched opening, snow on its crown
# and ledges, icicles along the lip and boulders at its foot. The cold air is still the game's.
ZC = 1.25
r = Renderer(int(44 * ZC), int(40 * ZC), pitch=PITCH, zoom=ZC, tones={
    "rock": ("#475a67", "#6c7f8c", "#a4b6c1"), "snow": ("#b7ccda", "#dbe8ef", "#f4f9fb"),
    "dark": ("#04060a", "#080d12", "#0e161d"), "ice": ("#7fb4cc", "#bfe4f2", "#e8f8ff"), "floor": ("#1c2a34", "#26384a", "#3a5164")},
    lit_at=(0.62, 0.0))
M = r.ids
r.water()                                            # the ground hides the hill's lower half
hill = blob(r, 0, 4, 0, 1.0, M["rock"], sx=16, sy=11, sz=24, rough=2.2, seed=1, sub=5)
hill.data.materials.append(M["snow"])
bpy.context.view_layer.objects.active = hill
mod = hill.modifiers.new("cut", "BOOLEAN")
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=1.0, depth=40, location=(0, 0, r.Z(0)), rotation=(math.radians(90), 0, 0))
tunnel = bpy.context.object
tunnel.scale = (7.0, 15.5 * r.stretch, 1)
tunnel.display_type = "WIRE"
mod.object = tunnel
mod.operation = "DIFFERENCE"
bpy.context.view_layer.objects.active = hill
bpy.ops.object.modifier_apply(modifier="rough")
bpy.ops.object.modifier_apply(modifier="cut")
bpy.data.objects.remove(tunnel, do_unlink=True)
hill.data.materials.append(M["dark"])
TW, TH = 7.0, 15.5 * r.stretch
for p in hill.data.polygons:
    c = p.center
    if (c.x / TW) ** 2 + (c.z / TH) ** 2 < 1.08 and c.y > -12:
        p.material_index = 2                          # the tunnel's own walls and roof: dark, so the opening reads as a hole
    elif p.normal.z > 0.55 and c.z > r.Z(9):
        p.material_index = 1                          # snow wherever the rock faces the sky, above the lower ledges
# The dark inside (a back wall deep in the tunnel) and the icy floor at the lip.
box(r, -9, 9, 6, 8, -1, 17, M["dark"])
box(r, -5.5, 5.5, -5, -2, -0.4, 0.3, M["floor"])          # just a lip of ice at the mouth
for i in range(-5, 6):                               # icicles hanging from the arch
    if (i * 7) % 3 == 1:
        continue
    zt = 15.4 - (i * i) / 2.2
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.5, radius2=0.0, depth=r.Z(2.5 + (i % 3)), location=(i * 1.3, -6.5, r.Z(zt - 1.4)), rotation=(math.pi, 0, 0))
    bpy.context.object.data.materials.append(M["ice"])
for x, y, s in ((-12, -6, 3.0), (11.5, -5, 3.5), (-8.5, -9, 1.8)):   # boulders at the foot, snow on top
    b = blob(r, x, y, s * 0.4, s, M["rock"], sz=0.8, rough=0.5, seed=2, sub=3)
    b.data.materials.append(M["snow"])
    for p in b.data.polygons:
        if p.normal.z > 0.6:
            p.material_index = 1
r.aim((0, 0, 14))
finish("CaveMouth", r, extra={"Zoom": ZC})

# ---- A pagatpat, Bakawan's firefly tree (Bakawan.cs DrawPagatpat). Anchor: the foot of its trunk, (x, y). A third
# bigger than the old drawing: arching stilt roots in the mud, pencil roots poking up all round, a short grey trunk and a
# broad dark glossy crown (the game lights the crown with the fireflies at CrownRise rows above the anchor).
ZP = 1.3
r = Renderer(int(34 * ZP), int(34 * ZP), pitch=PITCH, zoom=ZP, tones={
    "bark": ("#5e4c3a", "#7a6a58", "#9a8a74"), "root": ("#5e4c3a", "#8a7660", "#a8987c"),
    "leaf": ("#1d3f26", "#27512e", "#356a3a", "#4f8a4a"), "leaf2": ("#173521", "#22482a", "#2e5e34", "#447a40")},
    lit_at=(0.66, 0.0))
M = r.ids
random.seed(5)
r.rod((0, 0, 0), (0, 0, 13), 1.4, M["bark"], verts=10)
r.rod((0, 0, 11), (-5, 0, 15), 0.7, M["bark"])
for k in range(6):                                   # arching stilt roots, out and down into the mud
    ang = k / 6 * 2 * math.pi + 0.3
    pts = [(0.6 * math.cos(ang) * t * 9, 0.6 * math.sin(ang) * t * 9, 7 - 7 * t ** 1.6 + 1.2 * math.sin(t * math.pi)) for t in (0, 0.25, 0.5, 0.75, 1.0)]
    for p0, p1 in zip(pts, pts[1:]):
        r.rod(p0, p1, 0.45, M["root"], verts=6)
for k in range(12):                                  # pencil roots
    ang = random.random() * 2 * math.pi
    d = random.uniform(6, 11)
    x, y = d * math.cos(ang), d * math.sin(ang) * 0.7
    r.rod((x, y, 0), (x, y, random.uniform(1.2, 2.4)), 0.3, M["root"], verts=5)
for i, (ox, oy, z, rad) in enumerate([(0, 0, 19, 8.0), (-7, 0.5, 16, 5.5), (7, 0.5, 16.5, 5.5), (-3, 2, 23, 5.0), (4, 2, 22.5, 4.5), (0, -3, 15.5, 5.0)]):
    blob(r, ox, oy, z, rad, M["leaf2" if i % 3 == 2 else "leaf"], sz=0.75, rough=1.1, seed=3, sub=4)
r.water()
r.aim((0, 0, 14))
finish("Pagatpat", r, extra={"Zoom": ZP, "CrownRise": round(18 * ZP)})

# ---- Marilaya's tide post (MarilayaPlaces.cs DrawTidePost). Anchor: the post's foot at (tidePostX, tidePostY), the
# post itself one pixel west of its two columns. A rock with a pole set in it, painted in bands of two rows (white,
# red) from row 3 to 21 so the game's waterline reads the same, and a dark cap.
r = Renderer(14, 28, pitch=PITCH, tones={"rock": ("#43403c", "#5a5652", "#76716a"), "white": ("#c9c3b4", "#f2efe6", "#ffffff"),
    "red": ("#8e2a20", "#c0392b", "#e05a48"), "cap": ("#2c261f", "#3c342a", "#4d4437")}, lit_at=(0.66, 0.0))
M = r.ids
blob(r, 0.5, 1, 1.2, 4.2, M["rock"], sy=0.8, sz=0.55, rough=0.6, seed=4, sub=3)
pole = r.cylinder(1, 0, 3, 21, 1.0, 1.0, None, verts=12)
h = r.Z(18)
from mathutils import Matrix
pole.data.transform(Matrix.Translation((0, 0, h / 2)))
pole.location.z -= h / 2
pole.data.materials.clear()
pole.data.materials.append(r.banded("bands", "Z", r.Z(4.0), "white", "red", 0.5))
r.cylinder(1, 0, 21, 22, 2.0, 2.0, M["cap"], verts=12)
r.aim((1, 0, 12))
finish("TidePost", r)

# ---- The survey cairn on Bundok Tindig (MarilayaPlaces.cs DrawCairn). Anchor: its foot, CairnAt. Flat stones piled
# into a cone half again as big as the old drawing, so it reads at the summit.
ZK = 1.5
r = Renderer(int(16 * ZK), int(16 * ZK), pitch=PITCH, zoom=ZK, tones={"stone": ("#6e6a62", "#8f8a80", "#c9c4b8"),
    "stone2": ("#615d56", "#7d786f", "#a8a398")}, lit_at=(0.62, 0.0))
M = r.ids
random.seed(9)
for layer, (n, rad, z) in enumerate(((7, 3.4, 0.8), (6, 2.6, 2.6), (5, 1.8, 4.4), (3, 1.0, 6.0), (1, 0.0, 7.4))):
    for k in range(n):
        ang = k / max(n, 1) * 2 * math.pi + layer
        blob(r, rad * math.cos(ang), rad * math.sin(ang) * 0.8, z, 1.5 - layer * 0.15, M["stone" if (k + layer) % 3 else "stone2"],
             sx=1.3, sy=1.1, sz=0.6, rough=0.25, seed=5, sub=2)
r.aim((0, 0, 4))
finish("Cairn", r, extra={"Zoom": ZK})

# ---- The spring-water dryer (MarilayaPlaces.cs DrawDryer). Anchor: its foot, DryerAt. A small bamboo shed (walls 24
# wide rows 1..14) under a thatched roof, with the steel drying cabinet at its front (x -5..5, rows 2..11): the game
# draws the trays' glow and the fish in it.
r = Renderer(int(34 * 1.15), int(28 * 1.15), pitch=PITCH, zoom=1.15, tones={"wall": ("#94693e", "#b98b52", "#cfa466"),
    "slat": ("#bf9758", "#dfbc78", "#ecd096"), "thatch": ("#a68449", "#d0b170", "#e8cf8e"), "band": ("#6e5230", "#93723f", "#ab8a52"),
    "steel": ("#6e767b", "#8f969b", "#c7ced2"), "tray": ("#7a8085", "#9aa0a5", "#b4babd"), "pipe": ("#4e5458", "#6e767b", "#8a9296")},
    lit_at=(0.66, 0.0))
M = r.ids
box(r, -12, 12, 0, 10, 1, 14, r.banded("weave", "X", 4.0, "wall", "slat", 0.72))
box(r, -5, 5, -1.2, 0.2, 2, 11, M["steel"])
for k in range(3):
    box(r, -4, 4, -1.4, -1.0, 4 + k * 2, 4.6 + k * 2, M["tray"])
r.rod((-9, -1, 4), (-9, -1, 10), 0.5, M["pipe"])
r.rod((-9, -1, 10), (-5.2, -1, 10), 0.5, M["pipe"])
bm = bmesh.new()
Z = r.Z
e = [bm.verts.new((x, y, Z(14))) for x, y in ((-14, -3), (14, -3), (14, 13), (-14, 13))]
ra, rb = bm.verts.new((-6, 5, Z(20))), bm.verts.new((6, 5, Z(20)))
f1 = bm.faces.new((e[0], e[1], rb, ra)); f2 = bm.faces.new((e[2], e[3], ra, rb))
f3 = bm.faces.new((e[3], e[0], ra)); f4 = bm.faces.new((e[1], e[2], rb))
f3.material_index = f4.material_index = 1
me = bpy.data.meshes.new("droof")
bm.to_mesh(me)
bm.free()
ro = bpy.data.objects.new("droof", me)
r.scene.collection.objects.link(ro)
ro.data.materials.append(r.banded("thatchrows", "Z", r.Z(3.0), "thatch", "band", 0.7))
ro.data.materials.append(M["thatch"])
ro.modifiers.new("t", "SOLIDIFY").thickness = 1.0
r.aim((0, 5, 11))
finish("Dryer", r, extra={"Zoom": 1.15})

# ---- The limestone rock face (MarilayaPlaces.cs DrawCliff). Anchor: the middle of its foot, MlPlace("cliff"). A cut
# hillside 72 wide and 42 rows high, modelled as one rough face rather than slabs: it bulges and folds in and out, with a
# buttress or two standing proud, dark volcanic rock at the bottom (rows 0..11), pale limestone above it with the old wave
# notch cut back into it (rows 25..29) under a lip, soil, and a ragged edge of grass and shrubs along the top. Shells sit
# in the limestone.
r = Renderer(84, 58, pitch=PITCH, tones={"basalt": ("#34312e", "#4a4643", "#625d58"), "lime": ("#a8a28a", "#cfc9b2", "#ece6d2"),
    "limeband": ("#999380", "#bcb6a0", "#d8d2bc"), "notch": ("#4e4a44", "#6e6a60", "#8f8a7e"),
    "soil": ("#5e4430", "#7e6044", "#9a7a56"), "grass": ("#3e6a32", "#5d8a42", "#7aa85a"), "shell": ("#d8cfba", "#f6efe0", "#ffffff")},
    lit_at=(0.6, -0.1))
M = r.ids
random.seed(13)
order = ["basalt", "lime", "limeband", "notch", "soil"]
NX, NZ = 96, 56
bm = bmesh.new()
grid = []
for j in range(NZ + 1):
    z = 38.0 * j / NZ                                 # rows, foot to the soil
    row = []
    for i in range(NX + 1):
        x = -36 + 72.0 * i / NX
        # How far back the face is here: folds and bulges, buttresses, and the notch cut in.
        y = 1.6 * math.sin(x * 0.31 + 1.0) + 1.1 * math.sin(x * 0.83 + z * 0.21) + 0.7 * math.sin(z * 0.9 + x * 0.17)
        y -= 2.2 * math.exp(-((x + 16) / 4.0) ** 2) + 1.8 * math.exp(-((x - 19) / 5.0) ** 2)     # two buttresses
        if 25 <= z <= 29:
            y += 3.2 * math.sin((z - 25) / 4 * math.pi) ** 0.5                                 # the notch, set back
        elif 29 < z <= 30.5:
            y -= 1.0                                                                            # its overhanging lip
        y += 0.6 * (random.random() - 0.5)
        y += z * 0.06                                 # the face leans back a little as it rises
        row.append(bm.verts.new((x, y, r.Z(z))))
    grid.append(row)
mats = []
for j in range(NZ):
    zc = 38.0 * (j + 0.5) / NZ
    for i in range(NX):
        f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
        m = "basalt" if zc < 11 + 1.2 * math.sin(i * 0.4) else "notch" if 25 <= zc <= 29 else "soil" if zc > 35 else \
            ("limeband" if int(zc) % 4 == 0 else "lime")
        f.material_index = order.index(m)
        f.smooth = True
me = bpy.data.meshes.new("cliff")
bm.to_mesh(me)
bm.free()
cl = bpy.data.objects.new("cliff", me)
r.scene.collection.objects.link(cl)
for k in order:
    cl.data.materials.append(M[k])
cl.modifiers.new("t", "SOLIDIFY").thickness = 6
# The grass along the top: a ragged row of clumps, and a few shrubs hanging over the edge.
for k in range(30):
    x = -35 + k * 2.45 + random.uniform(-0.6, 0.6)
    blob(r, x, 3 + random.uniform(0, 3), 38.5 + random.uniform(-0.6, 1.2), random.uniform(1.6, 2.6), M["grass"], sz=0.7, sub=2)
for x, z in ((-25, 33), (8, 34), (27, 32)):
    blob(r, x, -1.5, z, 1.8, M["grass"], sz=0.8, rough=0.3, seed=7, sub=2)
for x, z in ((-29, 20), (-17, 16), (-3, 21), (14, 15), (-8, 33), (24, 22)):
    blob(r, x, -2.2 + 0.06 * z, z, 1.3, M["shell"], sx=1.3, sy=0.5, sz=0.8, sub=2)
r.aim((0, 4, 21))
finish("Cliff", r)

# ---- The harbour board (Bounties.cs DrawHarbourBoard, 1.45). Anchor: between its two posts on the ground. Two weathered
# posts, a plank board leaning back a little under a small shingled roof, three notices pinned to it (the game paints a
# gold dot over one when something is ready), a coil of rope on a post and a crate at its foot.
r = Renderer(22, 30, pitch=PITCH, tones={**WOOD, "board": ("#6b4a2b", "#8a6038", "#a87a48"),
    "roof": ("#5a2e22", "#7e4030", "#9c5a40"), "paper": ("#b9ad8e", "#e9e0c6", "#fbf6e6"),
    "paper2": ("#a8a080", "#d8d0b0", "#efe8cc"), "pin": ("#8e2a20", "#c0392b", "#e05a48"),
    "rope": ("#8a7448", "#b49a64", "#d2bc86"), "crate": ("#7a5a2a", "#a87a3a", "#c89a58")}, lit_at=(0.66, 0.0))
M = r.ids
for x in (-8, 6):
    box(r, x, x + 2, 0, 2, 0, 20, M["post"])
box(r, -8.5, 8.5, 0.6, 1.6, 7, 18, r.banded("boardplanks", "Z", r.Z(2.8), "board", "seam", 0.7), rot=(-6, 0, 0))
box(r, -10, 10, -1.5, 3.5, 19.5, 20.6, M["roof"], rot=(-18, 0, 0))
box(r, -9.5, 9.5, -1.0, 3.0, 20.6, 21.6, M["roof"], rot=(-18, 0, 0))
for (x0, x1, z0, z1, mat) in ((-7, -2.5, 11.5, 16.5, "paper"), (-1.5, 3, 12.5, 17, "paper2"), (3.6, 7.4, 8.5, 13, "paper"), (-5.5, -0.5, 8, 10.6, "paper2")):
    box(r, x0, x1, 0.2, 0.55, z0, z1, M[mat], rot=(-6, 0, 0))
    r.cylinder((x0 + x1) / 2, 0.0, z1 - 1.0, z1 - 0.4, 0.5, 0.5, M["pin"], verts=8)
r.cylinder(7, 1, 4, 6.5, 1.6, 1.6, M["rope"], verts=12)
box(r, -11, -6.5, -1.5, 2.5, 0, 4, M["crate"])
r.aim((0, 0, 11))
finish("HarbourBoard", r)


if CS:
    lines = [
        "// Generated by tools/blender/landmarks.py from 3D models: don't edit by hand, change the models and re-run the",
        "// script. Each landmark is a grid of palette letters ('.' clear) with its anchor on the ground as the script says;",
        "// Zoom is how much bigger than the old drawing it's shown, for the overlays the game still draws.",
        "namespace Fesh;",
        "",
        "static class LandmarkArt",
        "{",
    ]
    for name, grid, ax, ay, rr, extra in results:
        pal = rr.colours([grid])
        lines.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{c}"' for c in pal) + " };")
        consts = [f"{name}AnchorX = {ax}", f"{name}AnchorY = {ay}"] + [f"{name}{k} = {v}" for k, v in extra.items() if isinstance(v, int)]
        lines.append(f"    public const int {', '.join(consts)};")
        for k, v in extra.items():
            if isinstance(v, float):
                lines.append(f"    public const float {name}{k} = {v}f;")
        lines.append(f"    public static readonly string[] {name} =")
        lines.append("    {")
        lines += [f'        "{row}",' for row in rr.rows(grid, pal)]
        lines.append("    };")
    lines.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", [(n, ax, ay) for n, _, ax, ay, _, _ in results])
