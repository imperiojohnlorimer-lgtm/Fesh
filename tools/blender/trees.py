# Trees and bushes, modelled and rendered to pixel art (see pixelrender.py): three oaks, three palms, three snowy firs and
# four berry bushes. All in TreeArt.cs.
# Run:  blender -b --python tools/blender/trees.py -- <out_dir> [cs_path]
#
# Each is built round its trunk's foot on the ground (the anchor), x across, y back from the viewer, heights in rows.
# Every pixel also keeps its height above the ground (a letter, 'a' = 0, two rows a letter), so the game can sway the
# crown in the wind and keep the trunk still. The bushes' berries are a special material (X/Y/Z): the game colours them
# (red berries, or Habagat's calamansi) and paints them as leaves on a day they've been picked.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from pixelrender import Renderer, args

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)
PITCH = 16

LEAF = ("#14382a", "#1d5a38", "#2b7a40", "#4a9a42", "#8fcf58")       # outline-dark to sunlit, like the 1.25 oaks
LEAF2 = ("#123226", "#1a5034", "#266e3b", "#408c3e", "#7cbf50")
BARK = ("#3f2a19", "#5a3d24", "#7a5531", "#94683d")
PALM = ("#123c27", "#1f6e37", "#3f9c42", "#7cc556", "#b4e27a")
PALM2 = ("#0f3020", "#195a2e", "#2f7f38", "#5ea548", "#8cc862")
PALMBARK = ("#5b4128", "#7a5a38", "#9a7a50", "#b89a6a")
FIR = ("#10291f", "#183a2c", "#24503a", "#336a4a", "#4a8660")
SNOW = ("#a9bccb", "#d4e2ec", "#eef5f8", "#ffffff")


def blob(r, x, y, z, rad, mat, sx=1.0, sy=1.0, sz=1.0, rough=0.0, seed=0, sub=3, scale=1.4):
    """A lumpy leaf clump or rock: an ico sphere at height z (rows), squashed, roughened by a cloud noise."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=rad, location=(x, y, r.Z(z)))
    o = bpy.context.object
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rough:
        name = "rough%d" % seed
        tex = bpy.data.textures.get(name) or bpy.data.textures.new(name, "CLOUDS")
        tex.noise_scale = scale
        d = o.modifiers.new("rough", "DISPLACE")
        d.texture = tex
        d.strength = rough
        d.texture_coords = "GLOBAL"
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


results = []


def finish(name, r, aim):
    r.aim(aim)
    grid = r.render(OUT, heights=True)
    hgt = r.last_heights
    ax, ay = r.anchor((0, 0, 0))
    r.preview(os.path.join(OUT, f"{name}.png"), [grid], special_colours=("#8a1f1a", "#c0392b", "#ff7a6a"), back=(84, 143, 64), scale=6)
    results.append((name, grid, hgt, ax, ay, r))


# ---- Oaks: a trunk flaring into roots, two or three limbs, and a crown of leaf clumps in a rough dome (deeper clumps
# underneath and round the back, so the shade sits under the crown and the top catches the light).
def oak(name, seed, size):
    random.seed(seed)
    k = size
    W, H = int(44 * k), int(64 * k)
    r = Renderer(W, H, pitch=PITCH, tones={"bark": BARK, "leaf": LEAF, "leaf2": LEAF2}, lit_at=(0.66, 0.0))
    M = r.ids
    trunk = 12 * k
    r.cylinder(0, 0, 0, trunk + 4, 2.8 * k, 1.7 * k, M["bark"], verts=14)
    for i in range(5):                                       # roots
        ang = i / 5 * 2 * math.pi + random.random()
        r.rod((0, 0, 1.5), (3.8 * k * math.cos(ang), 2.4 * k * math.sin(ang), 0), 1.0 * k, M["bark"], verts=6)
    for i in range(3):                                       # limbs into the crown
        ang = (i - 1) * 0.9 + random.uniform(-0.25, 0.25)
        tip = (math.sin(ang) * 9 * k, random.uniform(-1, 2) * k, trunk + 9 * k)
        r.rod((0, 0, trunk - 2), tip, 1.0 * k, M["bark"], verts=6)
    cz = trunk + 15 * k
    # A dome of clumps: a big heart, then a ring of clumps round its middle and a few on top, lopsided per tree.
    clumps = [(0, 0, cz, 11.0)]
    for i in range(20):
        ang = random.uniform(0, 2 * math.pi)
        up = random.uniform(-0.45, 0.9)
        d = random.uniform(8.0, 12.5) * math.sqrt(1 - up * up * 0.6)
        clumps.append((math.cos(ang) * d, math.sin(ang) * d * 0.75, up * 10, random.uniform(5.0, 7.5)))
    for i, (x, y, z, rad) in enumerate(clumps):
        dark = i > 0 and (z < -3 or y > 4 or i % 5 == 4)
        blob(r, x * k, y * k, cz + (0 if i == 0 else z * k), rad * k, M["leaf2" if dark else "leaf"],
             sz=0.8, rough=1.8 * k, seed=seed * 10 + i % 4, sub=4, scale=0.7)
    finish(name, r, (0, 0, cz * 0.62))


# ---- Palms: a tall ringed trunk that leans and curves, coconuts under a crown of long arching fronds, each a flat
# strip that droops to its tip with its leaflets fanned to either side.
def palm(name, seed, size):
    random.seed(seed)
    k = size
    W, H = int(66 * k), int(62 * k)
    r = Renderer(W, H, pitch=PITCH, tones={"bark": PALMBARK, "frond": PALM, "frond2": PALM2, "nut": ("#3a2614", "#5b3e24", "#7a5634", "#94704a")}, lit_at=(0.82, 0.3))
    LEAFLETS = r.banded("leaflets", "X", 2.0, "frond", "frond2", 0.55)
    M = r.ids
    n = 36 * k
    lean = random.choice((-1, 1)) * random.uniform(5, 9) * k
    pts = []
    for i in range(13):
        t = i / 12
        pts.append((lean * t * t, 0, n * t))
    for i, (p0, p1) in enumerate(zip(pts, pts[1:])):
        rad = (1.7 - 0.5 * i / 12) * k
        r.rod(p0, p1, rad, M["bark"], verts=10)
    top = pts[-1]
    for i in range(4):
        ang = i / 4 * 2 * math.pi + 0.4
        blob(r, top[0] + 1.6 * k * math.cos(ang), 1.0 * math.sin(ang), top[2] - 2.0, 1.4 * k, M["nut"], sub=2)
    nf = 7
    for f in range(nf):
        ang = f / nf * 2 * math.pi + random.uniform(-0.2, 0.2)
        length = random.uniform(19, 24) * k
        dx, dy = math.cos(ang), math.sin(ang) * 0.85
        segs = 9
        prev = (top[0], top[1], top[2] + 1)
        for s in range(1, segs + 1):
            t = s / segs
            x = top[0] + dx * length * t
            y = top[1] + dy * length * t
            z = top[2] + 1 + 7 * k * t - 16 * k * t * t
            w = (2.4 * math.sin(math.pi * min(1.0, t * 1.1)) + 0.9) * k
            # Two flat strips folded down from the midrib like a roof, so one side of each frond catches the light.
            mx, my, mz = (prev[0] + x) / 2, (prev[1] + y) / 2, (prev[2] + z) / 2
            seg_len = math.dist((prev[0], prev[1], r.Z(prev[2])), (x, y, r.Z(z))) + 0.8
            pitch_a = -math.atan2(r.Z(z) - r.Z(prev[2]), math.hypot(x - prev[0], y - prev[1]))
            yaw = math.atan2(y - prev[1], x - prev[0])
            for side in (-1, 1):
                bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
                o = bpy.context.object
                o.scale = (seg_len, w, 0.3)
                bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
                for v in o.data.vertices:                      # hinge each half on the midrib, folded 30 degrees down
                    v.co.y += side * w / 2
                    v.co.z -= abs(v.co.y) * 0.55
                o.location = (mx, my, r.Z(mz))
                o.rotation_euler = (0, pitch_a, yaw)
                o.data.materials.append(LEAFLETS)
            prev = (x, y, z)
    finish(name, r, (top[0] * 0.7, 0, top[2] * 0.6))


# ---- Firs: a short trunk and five or six drooping tiers, each a ragged cone of boughs, with snow lying on the tops.
def fir(name, seed, size):
    random.seed(seed)
    k = size
    W, H = int(34 * k), int(64 * k)
    r = Renderer(W, H, pitch=PITCH, tones={"bark": BARK, "fir": FIR, "snow": SNOW}, lit_at=(0.62, -0.05))
    M = r.ids
    r.cylinder(0, 0, 0, 10 * k, 1.6 * k, 1.2 * k, M["bark"], verts=10)
    tiers = random.choice((5, 6))
    for t in range(tiers):
        f = t / (tiers - 1)
        z0 = (6 + t * 7.5) * k
        rad = (12.5 - 8.8 * f) * k
        h = (11 - 2 * f) * k
        bpy.ops.mesh.primitive_cone_add(vertices=14, radius1=rad, radius2=0.3, depth=r.Z(h), location=(0, 0, r.Z(z0) + r.Z(h) / 2))
        o = bpy.context.object
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.subdivide(number_cuts=2)
        bpy.ops.object.mode_set(mode="OBJECT")
        name_t = "firr%d" % (seed * 10 + t)
        tex = bpy.data.textures.new(name_t, "CLOUDS")
        tex.noise_scale = 1.0
        d = o.modifiers.new("rough", "DISPLACE")
        d.texture = tex
        d.strength = 1.3 * k
        d.texture_coords = "GLOBAL"
        bpy.ops.object.modifier_apply(modifier="rough")
        o.data.materials.append(M["fir"])
        o.data.materials.append(M["snow"])
        for p in o.data.polygons:
            if p.normal.z > 0.2 and (p.center.z / r.Z(h) + 0.5) > 0.3 + 0.25 * random.random():
                p.material_index = 1
        bpy.ops.object.shade_smooth()
    finish(name, r, (0, 0, 26 * k))


# ---- Bushes: a mound of leaf clumps, knee to waist high, with berries set into the outside of the clumps.
def bush(name, seed, size, shape):
    random.seed(seed)
    k = size
    W, H = int(30 * k), int(24 * k)
    r = Renderer(W, H, pitch=PITCH, tones={"leaf": LEAF, "leaf2": LEAF2}, special="berry", lit_at=(0.66, 0.0))
    M = r.ids
    berry = M["berry"]
    lay = {
        0: [(0, 0, 5, 6.5, 0.8), (-6, 1, 3.5, 4.5, 0.75), (6, 0.5, 3.5, 4.8, 0.75), (-2, -2, 4, 4.5, 0.7), (3, 2, 7.5, 4.2, 0.8)],
        1: [(0, 0, 6, 6.0, 0.85), (-3, 1, 10, 4.5, 0.85), (3, 0, 9, 4.5, 0.85), (-5, -1, 3.5, 4.0, 0.7), (5, -1, 3.5, 3.8, 0.7)],
        2: [(-7, 0, 3.5, 4.6, 0.75), (0, 0, 5, 5.5, 0.8), (7, 0, 3.5, 4.6, 0.75), (-3, 1, 8, 4.0, 0.8), (3.5, 1, 7.5, 3.8, 0.8)],
        3: [(-3, 0, 5, 6.0, 0.8), (4, 0, 4, 4.8, 0.75), (-1, 1, 9, 4.5, 0.8), (6, 1, 7.5, 3.4, 0.8)],
    }[shape]
    for i, (x, y, z, rad, sz) in enumerate(lay):
        blob(r, x * k, y * k, z * k, rad * k, M["leaf2" if i % 3 == 2 else "leaf"], sz=sz, rough=1.5 * k, seed=seed * 10 + i, sub=4, scale=0.65)
    for i in range(13):
        x, y, z, rad, sz = random.choice(lay)
        ang = random.uniform(-2.6, -0.5)                    # on the side facing the viewer
        bx, by = x + math.cos(ang) * rad * 1.0, y + math.sin(ang) * rad * 1.0
        bz = z + random.uniform(-0.7, 0.4) * rad * sz
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1.1 * k, location=(bx * k, by * k, r.Z(bz * k)), segments=8, ring_count=6)
        bpy.context.object.data.materials.append(berry)
    finish(name, r, (0, 0, 5 * k))


for i, (seed, size) in enumerate(((3, 1.0), (7, 1.12), (11, 0.9), (15, 0.7))):
    oak(f"Oak{i}", seed, size)
for i, (seed, size) in enumerate(((4, 1.0), (8, 1.1), (12, 0.92), (16, 0.72))):
    palm(f"Palm{i}", seed, size)
for i, (seed, size) in enumerate(((5, 1.0), (9, 1.1), (13, 0.9), (17, 0.7))):
    fir(f"Fir{i}", seed, size)
for i in range(4):
    bush(f"Bush{i}", 20 + i, 1.0 + 0.06 * (i % 2), i)

if CS:
    lines = [
        "// Generated by tools/blender/trees.py from 3D models: don't edit by hand, change the models and re-run the script.",
        "// Each is a grid of palette letters ('.' clear, X/Y/Z the bushes' berries) with its anchor at the trunk's foot, and a",
        "// grid of the same size giving each pixel's height above the ground ('a' = 0, two rows a letter), for the sway.",
        "namespace Fesh;",
        "",
        "static class TreeArt",
        "{",
    ]
    for name, grid, hgt, ax, ay, rr in results:
        pal = rr.colours([grid])
        lines.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{c}"' for c in pal) + " };")
        lines.append(f"    public const int {name}AnchorX = {ax}, {name}AnchorY = {ay};")
        lines.append(f"    public static readonly string[] {name} =")
        lines.append("    {")
        lines += [f'        "{row}",' for row in rr.rows(grid, pal)]
        lines.append("    };")
        lines.append(f"    public static readonly string[] {name}Height =")
        lines.append("    {")
        for j, row in enumerate(grid):
            s = "".join("." if c is None else chr(ord("a") + min(25, int(hgt[j][i]) // 2)) for i, c in enumerate(row))
            lines.append(f'        "{s}",')
        lines.append("    };")
    lines.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", [(n, ax, ay) for n, _, _, ax, ay, _ in results])
