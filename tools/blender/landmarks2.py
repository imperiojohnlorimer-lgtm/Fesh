# More landmarks, modelled and rendered to pixel art (see pixelrender.py), all in LandmarkArt2.cs:
#   Marilaya's limestone cave mouth, Bantay Joy's watch hut and a sanctuary buoy, the Starwell's rim stones (three),
#   Ironbill leaping (rising, level, falling), and in Frostfang Caverns: ore rocks (three, their flecks coloured by the ore),
#   a crystal cluster, an Ancient Floor pillar (its runes lit by the game), the ladder up and the hole down.
# Run:  blender -b --python tools/blender/landmarks2.py -- <out_dir> [cs_path]
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix
from pixelrender import Renderer, args, SPECIAL_KEYS

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)
PITCH = 16
results = []


def box(r, x0, x1, y0, y1, z0, z1, mat):
    return r.box((x0 + x1) / 2, (y0 + y1) / 2, z0, x1 - x0, y1 - y0, z1 - z0, mat)


def blob(r, x, y, z, rad, mat, sx=1.0, sy=1.0, sz=1.0, rough=0.0, seed=0, sub=3, noise=1.4):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=rad, location=(x, y, r.Z(z)))
    o = bpy.context.object
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rough:
        tex = bpy.data.textures.get("rough%d" % seed) or bpy.data.textures.new("rough%d" % seed, "CLOUDS")
        tex.noise_scale = noise
        d = o.modifiers.new("rough", "DISPLACE")
        d.texture = tex
        d.strength = rough
        d.texture_coords = "GLOBAL"
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def flat(r, pts, y, mat, thick=0.4):
    """A flat shape in the x-z plane at depth y (z in rows): sails, fins."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, r.Z(z))) for x, z in pts]
    bm.faces.new(vs)
    me = bpy.data.meshes.new("flat")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("flat", me)
    r.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    o.modifiers.new("t", "SOLIDIFY").thickness = thick
    return o


def finish(name, r, extra=None, special=("#7a6a40", "#a89060", "#c8b080"), back=(120, 160, 100)):
    grid = r.render(OUT)
    ax, ay = r.anchor((0, 0, 0))
    r.preview(os.path.join(OUT, f"{name}.png"), [grid], special_colours=special, back=back, scale=6)
    results.append((name, grid, ax, ay, r, extra or {}))


# ---- Marilaya's limestone cave (MarilayaPlaces.cs DrawLimestoneCaveMouth). Anchor: the middle of the opening on the
# ground, MlPlace("cave"). A pale limestone hill about 60 wide and 26 high, fluted and grassy on top, a dark opening
# 12 wide and 15 high with stalactites at its lip, the walkway's first boards going in, and a lamp post on the right
# (the game lights the lamp and draws the drip).
r = Renderer(70, 44, pitch=PITCH, tones={"lime": ("#a8a28a", "#d2ccb6", "#eee8d6"), "dark": ("#06050a", "#0e0b10", "#15110f"),
    "grass": ("#3e6a32", "#5d8a42", "#7aa85a"), "plank": ("#7a5230", "#9a6a3a", "#b58250"), "post": ("#4e3420", "#65452e", "#86603a"),
    "drip": ("#bab49e", "#e8e2cc", "#ffffff")}, lit_at=(0.62, 0.0))
M = r.ids
r.water()
hill = blob(r, 0, 6, 0, 1.0, M["lime"], sx=30, sy=12, sz=27, rough=1.8, seed=11, sub=5, noise=1.0)
hill.data.materials.append(M["dark"])
bpy.context.view_layer.objects.active = hill
cut = hill.modifiers.new("cut", "BOOLEAN")
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=1.0, depth=40, location=(0, 0, 0), rotation=(math.radians(90), 0, 0))
tun = bpy.context.object
tun.scale = (6.2, 15.5 * r.stretch, 1)
cut.object = tun
cut.operation = "DIFFERENCE"
bpy.context.view_layer.objects.active = hill
bpy.ops.object.modifier_apply(modifier="rough")
bpy.ops.object.modifier_apply(modifier="cut")
bpy.data.objects.remove(tun, do_unlink=True)
for p in hill.data.polygons:
    c = p.center
    if (c.x / 6.2) ** 2 + (c.z / (15.5 * r.stretch)) ** 2 < 1.08 and c.y > -10:
        p.material_index = 1
box(r, -9, 9, 8, 10, -1, 18, M["dark"])
for k, x in enumerate((-4.5, -1.5, 1.5, 4.0)):                     # stalactites along the lip
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.6, radius2=0, depth=r.Z(2.5 + k % 2), location=(x, -4.5, r.Z(13.6 - abs(x) * 0.5)), rotation=(math.pi, 0, 0))
    bpy.context.object.data.materials.append(M["drip"])
for i in range(3):                                                 # the walkway's first boards
    box(r, -4.5, 4.5, -4 + i * 3, -2 + i * 3, 0, 0.8, M["plank"])
r.rod((9.5, -3, 0), (9.5, -3, 12), 0.5, M["post"])
random.seed(21)
for k in range(16):                                                # grass and shrubs over the top
    ang = random.uniform(-1.2, 1.2)
    blob(r, 22 * math.sin(ang), 6 + random.uniform(-3, 4), 23 * math.cos(ang) ** 0.8 + 1.5, random.uniform(1.6, 2.8), M["grass"], sz=0.7, sub=2)
r.aim((0, 4, 16))
finish("LimeCave", r)

# ---- Bantay Joy's watch hut (Sanctuary.cs DrawWatchHut). Anchor: the middle of its front on the deck, (2202, 682). A
# third bigger, like the houses: bamboo walls on a low floor, a door, a thatched roof, and the flagpole at x 10 (the game
# flies the flag at FlagTop rows above the anchor).
ZW = 1.3
r = Renderer(int(36 * ZW), int(32 * ZW), pitch=PITCH, zoom=ZW, tones={"wall": ("#a88048", "#c9a06a", "#dcb880"),
    "slat": ("#c8a666", "#e0c088", "#efd6a2"), "door": ("#2c261f", "#4a3a2a", "#5e4c3a"), "thatch": ("#a68449", "#d0b170", "#e8cf8e"),
    "band": ("#6e5230", "#93723f", "#ab8a52"), "post": ("#4e3420", "#6b4a2b", "#86603a")}, lit_at=(0.66, 0.0))
M = r.ids
box(r, -9, 9, 0, 9, 0, 1, M["post"])
box(r, -9, 9, 0, 9, 1, 12, r.banded("weave", "X", 3.0, "wall", "slat", 0.62))
box(r, -3, 3, -0.4, 0.6, 1, 8, M["door"])
bm = bmesh.new()
Z = r.Z
e = [bm.verts.new((x, y, Z(12))) for x, y in ((-13, -3), (13, -3), (13, 12), (-13, 12))]
ra, rb = bm.verts.new((-5, 4.5, Z(20))), bm.verts.new((5, 4.5, Z(20)))
f = [bm.faces.new((e[0], e[1], rb, ra)), bm.faces.new((e[2], e[3], ra, rb)), bm.faces.new((e[3], e[0], ra)), bm.faces.new((e[1], e[2], rb))]
f[2].material_index = f[3].material_index = 1
me = bpy.data.meshes.new("wroof")
bm.to_mesh(me)
bm.free()
ro = bpy.data.objects.new("wroof", me)
r.scene.collection.objects.link(ro)
ro.data.materials.append(r.banded("rows", "Z", r.Z(3.0), "thatch", "band", 0.7))
ro.data.materials.append(M["thatch"])
ro.modifiers.new("t", "SOLIDIFY").thickness = 1.0
r.rod((10, -1, 0), (10, -1, 24), 0.4, M["post"])
r.aim((0, 4, 12))
finish("WatchHut", r, extra={"Zoom": ZW, "FlagTop": round(24 * ZW)})

# ---- A sanctuary buoy (Sanctuary.cs DrawBuoys). Anchor: where it floats. A yellow float with a red top mark.
r = Renderer(10, 12, pitch=PITCH, tones={"yellow": ("#c8901a", "#f3c25b", "#ffe8a0"), "red": ("#a8342a", "#e04b3a", "#f07060")}, lit_at=(0.6, 0.0))
M = r.ids
r.water()
blob(r, 0, 0, 0.6, 2.4, M["yellow"], sz=0.9, sub=3)
r.rod((0, 0, 1.5), (0, 0, 5), 0.5, M["red"])
blob(r, 0, 0, 5.2, 1.0, M["red"], sub=2)
r.aim((0, 0, 3))
finish("Buoy", r, back=(40, 110, 140))

# ---- The Starwell's rim stones (World.cs carves the glade; the game sets them round the blue hole). Pale coral
# limestone, pitted, with a little pink coral on them. Three shapes.
for k in range(3):
    r = Renderer(16, 14, pitch=PITCH, tones={"coral": ("#9c958a", "#cfc8b8", "#f0eadc"), "pink": ("#b86a68", "#e08a86", "#f6b0a8")}, lit_at=(0.62, 0.0))
    M = r.ids
    r.water()
    random.seed(30 + k)
    for j in range(2 + k % 2):
        blob(r, random.uniform(-3, 3), random.uniform(-1, 1), random.uniform(0.5, 1.5), random.uniform(2.2, 3.6) - j * 0.6, M["coral"],
             sx=1.2, sz=0.75, rough=0.5, seed=40 + k, sub=3, noise=0.6)
    blob(r, random.uniform(-2, 2), -1.5, 3.0, 0.8, M["pink"], sub=2)
    r.aim((0, 0, 3))
    finish(f"RimStone{k}", r, back=(230, 210, 160))

# ---- Ironbill leaping (World.cs DrawFishing): a black marlin side on, facing east, its tall dorsal fin up, its long bill,
# a crescent tail, about 34 px. Three frames: nose up as it rises, level at the top, nose down as it falls. The anchor is
# the middle of its body.
for name, ang in (("MarlinUp", 30), ("MarlinTop", 0), ("MarlinDown", -32)):
    r = Renderer(46, 34, pitch=0.01, tones={"back": ("#141c2c", "#22304a", "#3a4c6e"), "side": ("#7a8ea4", "#b4c6d4", "#e2ecf2"),
        "fin": ("#1a2438", "#2a3a58", "#41567c"), "eye": ("#101010", "#202020", "#303030")}, lit_at=(0.6, 0.0))
    M = r.ids
    root = bpy.data.objects.new("root", None)
    r.scene.collection.objects.link(root)
    parts = []
    mb = bpy.data.metaballs.new("marlin")
    mb.resolution = 0.25
    mb.render_resolution = 0.2
    for i in range(24):
        k = i / 23
        x = 12 - 28 * k
        rad = 4.2 * math.sin(min(1, k * 1.5 + 0.15) * math.pi) ** 0.6 + 1.5
        e = mb.elements.new(type="ELLIPSOID")
        e.co = (x, 0, 0.2 * math.sin(k * 3))
        e.radius = rad
        e.size_x, e.size_y, e.size_z = 1.0, 0.6, 1.0
    body = bpy.data.objects.new("marlin", mb)
    r.scene.collection.objects.link(body)
    body.data.materials.append(M["side"])
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.convert(target="MESH")
    body = bpy.context.object
    body.data.materials.clear()
    body.data.materials.append(M["side"])
    body.data.materials.append(M["back"])
    for p in body.data.polygons:
        p.use_smooth = True
        if p.center.z > 0.4:
            p.material_index = 1                                      # the dark blue back above the silver flank
    parts.append(body)
    parts.append(r.rod((12.5, 0, 0.3), (21, 0, 0.6), 0.35, M["back"]))   # the bill
    parts.append(flat(r, [(8, 2.8), (6, 9.5), (2, 8.0), (-4, 4.0), (-6, 2.4)], 0, M["fin"]))           # the dorsal sail
    parts.append(flat(r, [(-14, 0.3), (-20, 7.0), (-17.5, 0.4), (-20, -6.2)], 0, M["fin"]))            # the crescent tail
    parts.append(flat(r, [(6, -1.5), (1, -5.0), (2.5, -1.8)], -2.0, M["fin"]))                         # the pectoral fin
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.7, location=(9.2, -2.0, 0.9))
    parts.append(bpy.context.object)
    bpy.context.object.data.materials.append(M["eye"])
    for o in parts:
        o.parent = root
    root.rotation_euler = (0, math.radians(-ang), 0)
    r.aim((0, 0, 0))
    finish(name, r, back=(40, 110, 140))

# ---- Frostfang Caverns. Ore rocks (Cave.cs DrawNode): a knobbly boulder filling its tile, anchor its foot (x + 5,
# y + 9), with flecks of ore in it ("special": the game colours them by the ore). Three shapes.
for k in range(3):
    r = Renderer(14, 16, pitch=PITCH, tones={"rock": ("#4a423e", "#665c56", "#8f847c")}, special="ore", lit_at=(0.62, 0.0))
    M = r.ids
    random.seed(50 + k)
    rock = blob(r, 0, 0, 3.2, 4.4, M["rock"], sx=1.0 + 0.1 * k, sz=0.9 - 0.08 * k, rough=0.9, seed=60 + k, sub=2, noise=0.8)
    rock.data.materials.append(M["ore"])
    for p in rock.data.polygons:
        if random.random() < 0.22 and p.center.y < 0.5:
            p.material_index = 1
    r.aim((0, 0, 4))
    finish(f"OreRock{k}", r, special=("#a06030", "#d9823f", "#f0a868"), back=(30, 28, 34))

# A crystal cluster on top of a crystal or abyssite rock ("special": the game colours it).
r = Renderer(12, 14, pitch=PITCH, tones={}, special="crystal", lit_at=(0.55, 0.0))
M = r.ids
for x, y, h, tilt in ((0, 0, 7, 0), (-2, 0.5, 5, 25), (2.2, 0.3, 4.5, -28), (0.8, 1.5, 3.5, 8)):
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=1.1, radius2=0.1, depth=r.Z(h), location=(x, y, r.Z(h / 2)), rotation=(0, math.radians(tilt), 0))
    bpy.context.object.data.materials.append(M["crystal"])
r.aim((0, 0, 3.5))
finish("Crystal", r, special=("#3fa8c8", "#9fe8ff", "#e0faff"), back=(30, 28, 34))

# The Ancient Floor's pillar (Cave.cs DrawPillar): a carved stone column on a plinth, 25 rows tall with a capital, its
# rune bands ("special") lit by the game. Anchor its foot (x + 5, y + 9).
r = Renderer(14, 34, pitch=PITCH, tones={"stone": ("#3e5456", "#5a7476", "#86a2a4")}, special="rune", lit_at=(0.62, 0.0))
M = r.ids
r.cylinder(0, 0, 0, 2.5, 4.4, 4.2, M["stone"], verts=8, smooth=False)
r.cylinder(0, 0, 2.5, 23, 3.0, 2.8, M["stone"], verts=16)
r.cylinder(0, 0, 23, 25.5, 4.2, 4.4, M["stone"], verts=8, smooth=False)
for z in (8, 13, 18):
    r.cylinder(0, 0, z, z + 1, 3.08, 3.08, M["rune"], verts=16)
r.aim((0, 0, 13))
finish("Pillar", r, special=("#1e7a70", "#5fe8d8", "#c0fff6"), back=(30, 28, 34))

# The ladder back up (Cave.cs, tile 'U'): two rails and rungs rising into the dark rock. Anchor its foot (x + 5, y + 10).
r = Renderer(12, 26, pitch=PITCH, tones={"rail": ("#5e4024", "#8a6440", "#a87c52"), "rung": ("#80603c", "#b08458", "#c89a6c")}, lit_at=(0.62, 0.0))
M = r.ids
for x in (-2.6, 2.6):
    r.rod((x, 0, -1), (x, 1.5, 21), 0.5, M["rail"])
for k in range(7):
    z = 1 + k * 3
    r.rod((-2.6, z * 1.5 / 22, z), (2.6, z * 1.5 / 22, z), 0.35, M["rung"])
r.aim((0, 0, 10))
finish("Ladder", r, back=(30, 28, 34))

# The hole down (Cave.cs, tile 'H'): a dark shaft with a rim of stones and the top of a ladder going down. Anchor the
# middle of its tile (x + 5, y + 5).
r = Renderer(14, 14, pitch=50, tones={"rock": ("#3a3430", "#56504a", "#7a726a"), "dark": ("#030303", "#060505", "#0a0808"),
    "rail": ("#5e4024", "#8a6440", "#a87c52")}, lit_at=(0.62, 0.0))
M = r.ids
box(r, -4, 4, -4, 4, -6, 0, M["dark"])
random.seed(70)
for k in range(10):
    ang = k / 10 * 2 * math.pi
    blob(r, 4.6 * math.cos(ang), 4.6 * math.sin(ang), 0.2, random.uniform(1.0, 1.5), M["rock"], sz=0.6, rough=0.2, seed=71, sub=2)
for x in (-1.6, 1.6):
    r.rod((x, -1, -4), (x, 0.5, 1.5), 0.35, M["rail"])
for z in (-2, 0.5):
    r.rod((-1.6, -0.2, z), (1.6, -0.2, z), 0.3, M["rail"])
r.aim((0, 0, 0))
finish("Hole", r, back=(60, 54, 50))


if CS:
    lines = [
        "// Generated by tools/blender/landmarks2.py from 3D models: don't edit by hand, change the models and re-run the",
        "// script. Each is a grid of palette letters ('.' clear) with its anchor as the script says; " + "/".join(SPECIAL_KEYS) + " are a",
        "// special material (shade, base, light) the game colours: the ore in an ore rock, a crystal, a pillar's runes.",
        "namespace Fesh;",
        "",
        "static class LandmarkArt2",
        "{",
    ]
    for name, grid, ax, ay, rr, extra in results:
        pal = rr.colours([grid]) or ["#000000"]
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
