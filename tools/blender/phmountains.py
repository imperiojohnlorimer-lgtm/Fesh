# The Philippines map's mountains and volcanoes (1.48), modelled and rendered to pixel art (see pixelrender.py), the way
# Baga's cone is (volcano.py): Mayon's perfect cone, Apo, Kanlaon, Pinatubo's caldera lake, Taal's little island volcano,
# Malindig, and forested massifs for the non-volcanic peaks (Pulag, Halcon, Kitanglad...). The game draws each over its
# peak's real place (PhTerrain.cs), fading it when you walk in behind it, as the trees do.
# Run:  blender -b --python tools/blender/phmountains.py -- <out_dir> [cs_path]
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from pixelrender import Renderer, args

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

PITCH = 13.0
TONES = {
    "crater":  ("#262426", "#2e2c2e", "#45413f"),
    "rock":    ("#5a544d", "#7d766c", "#9a9287"),
    "ash":     ("#7c766f", "#9c958c", "#b9b2a6"),
    "rockgul": ("#3c3833", "#4f4a45", "#605a54"),
    "scrub":   ("#55603f", "#76805a", "#949d74"),
    "grass":   ("#6a7d45", "#8a9c5a", "#a6b671"),     # the high grassland on a summit (Pulag's)
    "green":   ("#28452a", "#3b6436", "#568a48"),
    "greengul": ("#22392a", "#2d4c30", "#3a6238"),
    "lake":    ("#2f6f78", "#3d8a90", "#63b2b0"),
}


def build(r, half, height, prof, top, bands, gullies, crater=0.0, lake=False, bumps=(), seed=1, rough=0.012):
    """A mountain revolved from a profile: half width `half` at the foot, `height` rows tall. prof(t) is the half width at
    t (0 summit .. 1 foot) as a fraction of `half`; top is where the mesh starts (an open summit for a crater); bands(t, th,
    noise) names the material; bumps are extra peaks (angle, distance 0..1, height 0..1, width) raised on the surface."""
    random.seed(seed)
    M = r.ids
    NT, NA = 80, 300
    gang = [math.asin(max(-1, min(1, g))) for g in gullies]

    def groove(th):
        best = 0.0
        for g in gang:
            d = (th - g) / 0.055
            best = max(best, math.exp(-d * d))
        return best

    def lift(t, th):
        # extra height from the bumps (a massif's several summits)
        h = 0.0
        x, y = (1 - t) * 0 + t * math.sin(th), t * -math.cos(th)
        for (ang, dist, bh, w) in bumps:
            bx, by = dist * math.sin(ang), -dist * math.cos(ang)
            d2 = (x - bx) ** 2 + (y - by) ** 2
            h += bh * math.exp(-d2 / (w * w))
        return h

    bm = bmesh.new()
    rings = []
    for i in range(NT + 1):
        t = top + (1 - top) * i / NT
        row = []
        for j in range(NA):
            th = 2 * math.pi * j / NA - math.pi
            g = groove(th)
            mid = min(1, max(0, (t - 0.08) / 0.2))
            rad = half * prof(t) * (1 - 0.07 * g * mid) * (1 + rough * math.sin(th * 31 + t * 9 + seed) + rough * 0.8 * math.sin(th * 13 - t * 21 + seed))
            z = height * (1 - t) + height * 0.35 * lift(t, th) * (1 - t * 0.4)
            row.append(bm.verts.new((rad * math.sin(th), -rad * math.cos(th), r.Z(z))))
        rings.append(row)
    faces = []
    if crater > 0:
        centre = bm.verts.new((0, 0, r.Z(height * (1 - top) - (2 if lake else 4))))
        for j in range(NA):
            faces.append((bm.faces.new((rings[0][(j + 1) % NA], rings[0][j], centre)), "lake" if lake else "crater"))
    else:
        centre = bm.verts.new((0, 0, r.Z(height * (1 - top) + 1)))
        for j in range(NA):
            faces.append((bm.faces.new((rings[0][(j + 1) % NA], rings[0][j], centre)), bands(top, 0, 0.5, 0)))
    for i in range(NT):
        t = top + (1 - top) * (i + 0.5) / NT
        for j in range(NA):
            th = 2 * math.pi * (j + 0.5) / NA - math.pi
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % NA], rings[i + 1][(j + 1) % NA], rings[i + 1][j]))
            nz = 0.5 + 0.25 * math.sin(th * 7 + 1.3 + seed) + 0.15 * math.sin(th * 17 - seed) + random.random() * 0.1
            faces.append((f, bands(t, th, nz, groove(th))))
    me = bpy.data.meshes.new("m")
    order = list(M)
    for f, m in faces:
        f.material_index = order.index(m)
        f.smooth = True
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("m", me)
    r.scene.collection.objects.link(o)
    for k in order:
        o.data.materials.append(M[k])
    return o


def volcano_bands(tree, bare="rock"):
    def b(t, th, nz, g):
        core = g > 0.45 and t > 0.1
        line = tree + (nz - 0.5) * 0.12
        if t < line - 0.1:
            return "rockgul" if core else bare
        if t < line:
            return "rockgul" if core else "scrub"
        # Forest: clumps of darker canopy, so the slopes read as trees, not a smooth green sheet.
        clump = math.sin(th * 23 + t * 41) * math.sin(th * 11 - t * 67 + 1.7) + random.random() * 0.6
        return "greengul" if core or clump > 0.7 else "green"
    return b


def massif_bands(t, th, nz, g):
    core = g > 0.45 and t > 0.15
    if t < 0.05 + (nz - 0.5) * 0.06:
        return "grass"
    if t < 0.2 + (nz - 0.5) * 0.12:
        return "rock" if nz > 0.62 else "scrub"
    clump = math.sin(th * 23 + t * 41) * math.sin(th * 11 - t * 67 + 1.7) + random.random() * 0.6
    return "greengul" if core or clump > 0.7 else "green"


SPRITES = []


def sprite(name, half, height, **kw):
    W = int(half * 2 + 8)
    H = int(height * 1.5 + half * 0.45 + 12)
    r = Renderer(W, H, pitch=PITCH, tones=TONES)
    build(r, half, height, **kw)
    r.aim((0, 0, height * 0.45))
    cx, cy = r.anchor((0, 0, 0))          # the middle of the foot, on the ground: the peak's real place
    fx, fy = r.anchor((0, -half, 0))      # the front of the foot (for drawing order)
    sx, sy = r.anchor((0, 0, height))     # the summit
    grid = r.render(OUT)
    # A darker edge down the flanks so the mountain stands out against the ground behind it.
    for j in range(len(grid)):
        for i in range(len(grid[j])):
            c = grid[j][i]
            if c and j < fy - 10 and any(not (0 <= i + d < len(grid[j])) or grid[j][i + d] is None for d in (-1, 1)):
                grid[j][i] = (c[0], 0)
    SPRITES.append((name, r, grid, cx, cy, fy - cy, cy - sy, half))
    print("rendered", name, W, H)


def concave(p):
    return lambda t: 0.05 + 0.95 * t ** p


# Mayon: the near-perfect cone, steep and concave, bare and grey high up, a small crater, gullies down every side.
sprite("Mayon", 92, 118, prof=concave(2.0), top=0.03, bands=volcano_bands(0.58, "ash"), gullies=[-0.75, -0.45, -0.18, 0.1, 0.36, 0.62, 0.85], crater=0.03, seed=2)
# Apo: broad and massive, a ragged top of twin summits, forest high up its sides.
sprite("Apo", 104, 98, prof=lambda t: 0.1 + 0.9 * t ** 1.45, top=0.0, bands=volcano_bands(0.3), gullies=[-0.6, -0.2, 0.25, 0.6], bumps=[(-0.6, 0.08, 0.35, 0.12), (0.7, 0.1, 0.25, 0.1)], seed=3, rough=0.02)
# Kanlaon: a big active cone, bare and steaming at the top.
sprite("Kanlaon", 86, 96, prof=concave(1.8), top=0.05, bands=volcano_bands(0.45), gullies=[-0.55, -0.15, 0.3, 0.7], crater=0.05, seed=4)
# A medium forested volcano (Banahaw, Isarog, Matutum, Malindang, Talinis, Ragang...).
sprite("Forested", 70, 74, prof=concave(1.6), top=0.04, bands=volcano_bands(0.18), gullies=[-0.5, 0.0, 0.5], crater=0.04, seed=5)
# Malindig: Marinduque's dormant volcano, forested almost to the top.
sprite("Malindig", 52, 56, prof=concave(1.5), top=0.0, bands=volcano_bands(0.1), gullies=[-0.4, 0.3], seed=6)
# A small cone (Hibok-Hibok, Iraya, Arayat, Iriga, Bulusan, Biliran...).
sprite("Small", 40, 46, prof=concave(1.7), top=0.05, bands=volcano_bands(0.35), gullies=[-0.3, 0.35], crater=0.05, seed=7)
# Pinatubo: what's left after 1991, a low broad ring round a crater lake.
sprite("Pinatubo", 80, 40, prof=lambda t: 0.42 + 0.58 * t ** 1.3, top=0.12, bands=volcano_bands(0.55, "ash"), gullies=[-0.5, 0.1, 0.6], crater=0.12, lake=True, seed=8)
# Taal: a small island volcano with its own crater lake, in the middle of Taal Lake.
sprite("Taal", 30, 20, prof=lambda t: 0.35 + 0.65 * t ** 1.2, top=0.15, bands=volcano_bands(0.5, "ash"), gullies=[-0.3, 0.4], crater=0.15, lake=True, seed=9)
# Forested massifs for the mountains that aren't volcanoes: several summits on a long ridge, grassland on top.
sprite("Massif", 112, 70, prof=lambda t: 0.06 + 0.94 * t ** 1.05, top=0.0, bands=massif_bands, gullies=[-0.7, -0.35, 0.05, 0.4, 0.75], bumps=[(-1.25, 0.42, 1.1, 0.16), (1.15, 0.45, 0.95, 0.15), (0.35, 0.22, 0.8, 0.12), (-0.5, 0.5, 0.6, 0.12)], seed=10, rough=0.03)
sprite("Ridge", 72, 46, prof=lambda t: 0.06 + 0.94 * t ** 1.05, top=0.0, bands=massif_bands, gullies=[-0.5, 0.0, 0.5], bumps=[(-1.1, 0.42, 1.0, 0.17), (1.2, 0.4, 0.85, 0.16)], seed=11, rough=0.03)

# Previews and the C# file.
for name, r, grid, cx, cy, foot, summit, half in SPRITES:
    r.preview(os.path.join(OUT, f"mtn-{name}.png"), [grid], back=(110, 150, 90), scale=4)
if CS:
    L = [
        "// Generated by tools/blender/phmountains.py from 3D models: don't edit by hand, change the models and re-run the script.",
        "// Each mountain is a grid of palette letters ('.' clear); Anchor is the middle of its foot on the ground (the peak's real",
        "// place), Foot how many rows below that the front of its foot is, Summit how many rows above it the top is, Half the",
        "// half width of its foot.",
        "namespace Fesh;",
        "",
        "static class PhMountainArt",
        "{",
    ]
    for name, r, grid, cx, cy, foot, summit, half in SPRITES:
        pal = r.colours([grid])
        L.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{c}"' for c in pal) + " };")
        L.append(f"    public const int {name}AnchorX = {cx}, {name}AnchorY = {cy}, {name}Foot = {foot}, {name}Summit = {summit}, {name}Half = {int(half)};")
        L.append(f"    public static readonly string[] {name} =")
        L.append("    {")
        L += [f'        "{row}",' for row in r.rows(grid, pal)]
        L.append("    };")
    L.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(L) + "\n")
print("DONE")
