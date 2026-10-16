# Baga's volcano, a Mayon-like cone, modelled and rendered to pixel art (see pixelrender.py).
# Run:  blender -b --python tools/blender/volcano.py -- <out_dir> [cs_path]
#
# The cone is revolved from the old drawing's own profile (Magayon.cs ConeHalfAt: 5 px across the summit, 64 at the
# foot, concave), with gullies carved down it and a crater at the top. Seen at a low angle (PITCH) so its footprint
# on the ground is the 28 rows deep the collision box already has. Bare rock high up, scrub, then green below, the
# boundaries wandering. The south-west gully is "special": the game paints it as rock, or as lava from Level 3.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from pixelrender import Renderer, args, SPECIAL_KEYS

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)
random.seed(4)

PITCH = 13.0
CONE_H = 70.0          # the old drawing's height in rows, summit to foot
HALF = 64.0            # half width at the foot
HEIGHT = 76.0          # the mountain's own height in rows (the summit stands this plus the footprint's depth above the foot)
GULLIES = [-0.62, -0.27, 0.12, 0.46, 0.78]   # where the gullies cross the face, as fractions of the half width
LAVA = -0.27

TONES = {
    "crater":  ("#262426", "#2e2c2e", "#45413f"),
    "rock":    ("#4f4a45", "#7a746c", "#958e85"),
    "rockgul": ("#36332f", "#4a4642", "#5a5550"),
    "scrub":   ("#4a5240", "#6c7258", "#8a9070"),
    "green":   ("#28452a", "#3b6436", "#568a48"),     # forest, a little darker than the grass round the foot
    "greengul": ("#26402a", "#2f5230", "#3d6a3a"),
}
W, H = 136, 112
r = Renderer(W, H, pitch=PITCH, tones=TONES, special="lava")
M = r.ids


def half_at(t):
    """The old drawing's half width at a fraction t of the way down from the summit."""
    return 5 + (HALF - 5) * t ** 2.0     # more concave than the old drawing (1.7): Mayon's sweep


def angle_of(u):
    return math.asin(max(-1, min(1, u)))


GA = [angle_of(g) for g in GULLIES]


def groove(theta):
    """How deep the nearest gully cuts at this angle (0..1), and whether it's the lava gully."""
    best, lava = 0.0, False
    for g, gu in zip(GA, GULLIES):
        d = (theta - g) / 0.05
        k = math.exp(-d * d)
        if k > best:
            best, lava = k, gu == LAVA
    return best, lava


NT, NA = 90, 360
bm = bmesh.new()
rings = []
for i in range(NT + 1):
    t = 0.035 + (1 - 0.035) * i / NT                 # leave the summit open for the crater
    row = []
    for j in range(NA):
        th = 2 * math.pi * j / NA - math.pi           # 0 faces the viewer (-y)
        g, _ = groove(th)
        mid = min(1, max(0, (t - 0.08) / 0.2))
        rad = half_at(t) * (1 - 0.07 * g * mid) * (1 + 0.012 * math.sin(th * 31 + t * 9) + 0.01 * math.sin(th * 13 - t * 21))
        row.append(bm.verts.new((rad * math.sin(th), -rad * math.cos(th), r.Z(HEIGHT * (1 - t)))))
    rings.append(row)
# The crater: a shallow dish inside the summit ring.
centre = bm.verts.new((0, 0, r.Z(HEIGHT * (1 - 0.035) - 3)))
faces_mat = []
for j in range(NA):
    f = bm.faces.new((rings[0][(j + 1) % NA], rings[0][j], centre))
    faces_mat.append((f, "crater"))
for i in range(NT):
    t = 0.035 + (1 - 0.035) * (i + 0.5) / NT
    for j in range(NA):
        th = 2 * math.pi * (j + 0.5) / NA - math.pi
        f = bm.faces.new((rings[i][j], rings[i][(j + 1) % NA], rings[i + 1][(j + 1) % NA], rings[i + 1][j]))
        x = half_at(t) * math.sin(th)
        green = 0.6 + 0.1 * math.sin(x * 0.29) + 0.05 * math.sin(th * 7 + 1.3) + random.random() * 0.03
        g, lava = groove(th)
        core = g > 0.45 and t > 0.1
        if t < 0.07:
            m = "crater"
        elif lava and core and t < 0.5:
            m = "lava"
        elif t < green - 0.08:
            m = "rockgul" if core else "rock"
        elif t < green:
            m = "rockgul" if core else "scrub"
        else:
            m = "greengul" if core else "green"
        faces_mat.append((f, m))
me = bpy.data.meshes.new("cone")
order = list(M)
for f, m in faces_mat:
    f.material_index = order.index(m)
    f.smooth = True
bm.to_mesh(me)
bm.free()
cone = bpy.data.objects.new("cone", me)
r.scene.collection.objects.link(cone)
for k in order:
    cone.data.materials.append(M[k])

r.aim((0, 0, HEIGHT * 0.45))
# Anchored at the front of the foot, so the summit lands where the old drawing's was and the footprint matches.
ax, ay = r.anchor((0, -HALF, 0))
top = ay - r.anchor((0, 0, HEIGHT))[1]
grid = r.render(OUT)
# The flanks get their darkest tone along the edge, so the cone stands out against the ground behind it (not along
# the foot, which should run into the ground).
for j in range(len(grid)):
    for i in range(len(grid[j])):
        c = grid[j][i]
        if c and c[0] != "lava" and j < ay - 12 and any(not (0 <= i + d < len(grid[j])) or grid[j][i + d] is None for d in (-1, 1)):
            grid[j][i] = (c[0], 0)
r.preview(os.path.join(OUT, "volcano-preview.png"), [grid], special_colours=("#c8502a", "#e0582a", "#ffb347"), back=(120, 150, 110))

if CS:
    pal = r.colours([grid])
    lines = [
        "// Generated by tools/blender/volcano.py from a 3D model of Baga's volcano: don't edit by hand, change the model",
        "// and re-run the script. '.' is clear, letters index Palette, and " + "/".join(SPECIAL_KEYS) + " are the south-west gully",
        "// (shade, base, light), which the game paints as rock or lava. The anchor is the front of the foot; Summit is how",
        "// many rows above it the crater is.",
        "namespace Fesh;",
        "",
        "static class VolcanoArt",
        "{",
        "    public static readonly string[] Palette = { " + ", ".join(f'"{c}"' for c in pal) + " };",
        f"    public const int AnchorX = {ax}, AnchorY = {ay}, Summit = {top};",
        "    public static readonly string[] Grid =",
        "    {",
    ]
    lines += [f'        "{row}",' for row in r.rows(grid, pal)]
    lines += ["    };", "}"]
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", ax, ay, top)
