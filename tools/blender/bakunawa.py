# Bakunawa, the moon-eating serpent of the eclipse at Parola, modelled and rendered to pixel art (see pixelrender.py).
# Run:  blender -b --python tools/blender/bakunawa.py -- <out_dir> [cs_path]
#
# A long serpent swimming along the channel (x), its back breaking the water in humps that roll forward from the tail
# frame by frame, then its neck rearing out of the sea to a dragon's head turned toward the island, jaws open (the
# game draws the moon in them). FRAMES frames loop the humps once. Every pixel also stores its height above the water,
# so the game can sink the whole serpent by pushing it down and dropping what goes under (BakunawaArt.cs).
import os, sys, math, base64, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from mathutils import Vector, Euler
from pixelrender import Renderer, args

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)
FRAMES = 8

TONES = {
    "body":  ("#12343c", "#1f5058", "#367f7f"),
    "ring":  ("#0d2a31", "#173f46", "#28666a"),     # the dark edge of each band of scales
    "belly": ("#4f7f6c", "#79a88a", "#a9d0a8"),
    "fin":   ("#2f9a8f", "#55cfc1", "#9cf0e2"),
    "horn":  ("#8a7a55", "#c9b98a", "#efe3bc"),
    "mouth": ("#3e1019", "#6d1e2c", "#9a3445"),
    "tooth": ("#bdb7a8", "#e6e1d4", "#faf8f2"),
    "eye":   ("#d9a020", "#ffd76a", "#fff2b0"),
}
W, H = 240, 104
r = Renderer(W, H, pitch=30, tones=TONES)
M = r.ids
r.water()
SCALES = r.banded("scales", "X", 5.0, "body", "ring", 0.78)   # bands of scales every 5 px along the body


def Zw(rows):
    return r.Z(rows)


TAIL_X, NECK_X = -128.0, 22.0


def centre(s, ph):
    """The body's centre line, tail (s = 0) to where the neck rises (s = 1): humps rolling forward, bigger toward the
    head, and dipping under at the end so the neck comes up out of the water."""
    x = TAIL_X + (NECK_X - TAIL_X) * s
    amp = 12 + 15 * s
    z = amp * math.sin(2 * math.pi * 2.2 * s - ph) - 3
    end = max(0.0, (s - 0.86) / 0.14)
    z = z * (1 - end) + (-10) * end
    y = 5 * math.sin(2 * math.pi * 1.1 * s + 0.6)
    return x, y, z


def radius(s):
    return 1.6 + 6.4 * s ** 0.8


def tube(points, radii, mat, name):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 1.0
    cu.bevel_resolution = 6
    cu.use_fill_caps = True
    sp = cu.splines.new("POLY")
    sp.points.add(len(points) - 1)
    for p, rr, pt in zip(points, radii, sp.points):
        pt.co = (p[0], p[1], Zw(p[2]), 1)
        pt.radius = rr
    o = bpy.data.objects.new(name, cu)
    r.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    return o


def fin(at, size, tilt, mat=M["fin"]):
    """A fin standing up out of the back, swept toward the tail."""
    bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=size * 0.7, depth=size * 0.25, location=at)
    f = bpy.context.object
    f.rotation_euler = (math.radians(90), 0, math.radians(tilt))
    f.scale = (1.0, 1.0, 1.0)
    f.data.materials.append(mat)
    return f


def ellipsoid(parent, loc, scale, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=24, ring_count=12, location=loc)
    o = bpy.context.object
    o.scale = scale
    o.rotation_euler = [math.radians(v) for v in rot]
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    o.parent = parent
    return o


def cone(parent, loc, r0, depth, mat, rot):
    bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=r0, radius2=0, depth=depth, location=loc)
    o = bpy.context.object
    o.rotation_euler = [math.radians(v) for v in rot]
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    o.parent = parent
    return o


def build(ph):
    # The body, from the thin tail to the thick root of the neck.
    n = 120
    pts = [centre(i / n, ph) for i in range(n + 1)]
    tube(pts, [radius(i / n) for i in range(n + 1)], SCALES, "body")
    # Fins along the back where it rides above the water.
    for i in range(6, n, 7):
        s = i / n
        x, y, z = centre(s, ph)
        if z + radius(s) > 2:
            fin((x, y, Zw(z + radius(s) * 0.85)), 3 + 5 * s, 0)
    # The neck: up out of the water, arching forward to the head.
    bob = 2.2 * math.sin(ph)
    p0 = Vector((NECK_X, pts[-1][1], -10))
    p1 = Vector((NECK_X + 22, 0, 18))
    p2 = Vector((NECK_X + 14, -2, 36 + bob))
    p3 = Vector((NECK_X + 30, -4, 48 + bob))
    neck = []
    for i in range(41):
        t = i / 40
        u = 1 - t
        p = u ** 3 * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t ** 3 * p3
        neck.append((p.x, p.y, p.z))
    tube(neck, [8.0 - 1.8 * (i / 40) for i in range(41)], SCALES, "neck")
    # The pale throat down the front of the neck, a slightly smaller tube pushed toward the viewer.
    throat = [(x + 1.2, y - 2.6, z - 0.5) for (x, y, z) in neck[8:39]]
    tube(throat, [5.8 - 1.4 * (i / 30) for i in range(len(throat))], M["belly"], "throat")
    for i in range(10, 38, 6):
        x, y, z = neck[i]
        fin((x - 4.5, y + 1, Zw(z + 4)), 7, 25)

    # The head, turned toward the island (the viewer) and down a little, jaws wide.
    head = bpy.data.objects.new("head", None)
    r.scene.collection.objects.link(head)
    head.location = (p3.x + 3, p3.y, Zw(p3.z + 2))
    head.rotation_euler = (math.radians(-6), math.radians(14), math.radians(-28))
    ellipsoid(head, (0, 0, 0), (9.5, 7.0, 7.5), M["body"])                          # skull
    ellipsoid(head, (11, 0, 1.6), (12, 5.6, 3.6), M["body"], (0, -6, 0))           # upper jaw
    ellipsoid(head, (9, 0, -7.5), (11, 4.8, 2.6), M["body"], (0, 24, 0))           # lower jaw, dropped open
    ellipsoid(head, (9.5, 0, -3.2), (9, 4.4, 3.6), M["mouth"])                     # the mouth inside
    ellipsoid(head, (6, 0, -9.0), (8, 3.6, 1.6), M["belly"], (0, 24, 0))           # pale under the jaw
    for side in (-1, 1):
        for k in range(5):                                                        # teeth, down from the upper jaw
            cone(head, (6 + k * 3.2, side * (4.2 - k * 0.5), -1.6), 0.9, 3.0, M["tooth"], (180, 0, 0))
        for k in range(4):                                                        # and up from the lower
            cone(head, (6 + k * 3.2, side * (3.8 - k * 0.5), -6.2), 0.8, 2.6, M["tooth"], (0, -24, 0))
        ellipsoid(head, (3.5, side * 6.2, 3.6), (2.3, 1.2, 1.9), M["eye"])         # eyes
        ellipsoid(head, (2.0, side * 6.0, 5.6), (4.0, 1.6, 1.2), M["body"], (0, -12, 0))   # a heavy brow
        cone(head, (-5, side * 4.2, 6), 2.0, 16, M["horn"], (0, -62, side * 14))   # horns sweeping back
        cone(head, (-7, side * 7.4, -1.5), 3.5, 10, M["fin"], (90 * side, -70, 0))  # gill frills
        cone(head, (19, side * 3.2, 0), 0.6, 14, M["fin"], (0, 128, side * 30))    # whiskers trailing back
    # Where the moon sits in its mouth, in world space (for the game).
    bpy.context.view_layer.update()
    mouth = head.matrix_world @ Vector((10.5, 0, -3.5))
    return mouth, pts


# The splash where the body leaves and enters the water: the centre line crossing the surface.
def crossings(pts):
    out = []
    for (x0, y0, z0), (x1, y1, z1) in zip(pts, pts[1:]):
        if (z0 <= 0) != (z1 <= 0):
            k = z0 / (z0 - z1)
            out.append((x0 + (x1 - x0) * k, y0 + (y1 - y0) * k))
    return out


r.aim((-26, 0, 30))
ax, ay = r.anchor((0, 0, 0))
frames = []
for f in range(FRAMES):
    r.clear()
    ph = 2 * math.pi * f / FRAMES
    mouth, pts = build(ph)
    bpy.context.view_layer.update()
    grid = r.render(OUT, heights=True)
    hgt = r.last_heights
    mx, my = r.anchor((mouth.x, mouth.y, mouth.z / r.stretch))
    splash = [(int(round(r.anchor((x, y, 0))[0] - ax)), int(round(r.anchor((x, y, 0))[1] - ay))) for x, y in crossings(pts)]
    frames.append((grid, hgt, (mx - ax, my - ay), splash))
    print("frame", f)

r.preview(os.path.join(OUT, "bakunawa-preview.png"), [frames[0][0], frames[FRAMES // 2][0]], scale=4)

if CS:
    pal = r.colours([g for g, *_ in frames])
    lines = [
        "// Generated by tools/blender/bakunawa.py from a 3D model of Bakunawa: don't edit by hand, change the model and",
        "// re-run the script. Each frame is zlib-compressed bytes in base64: Width x Height palette indices (255 clear),",
        "// then as many heights above the water in rows. The anchor is the serpent's origin on the water; Mouth is where",
        "// the moon sits and Splash where the body breaks the surface, both from the anchor.",
        "namespace Fesh;",
        "",
        "static class BakunawaArt",
        "{",
        f"    public const int Width = {W}, Height = {H}, AnchorX = {ax}, AnchorY = {ay}, Frames = {FRAMES};",
        "    public static readonly string[] Palette = { " + ", ".join(f'"{c}"' for c in pal) + " };",
        "    public static readonly (int x, int y)[] Mouth = { " + ", ".join(f"({m[0]}, {m[1]})" for _, _, m, _ in frames) + " };",
        "    public static readonly (int x, int y)[][] Splash =",
        "    {",
    ]
    for _, _, _, sp in frames:
        lines.append("        new (int, int)[] { " + ", ".join(f"({x}, {y})" for x, y in sp) + " },")
    lines += ["    };", "    public static readonly string[] Data =", "    {"]
    for grid, hgt, _, _ in frames:
        col = bytearray(W * H)
        for j in range(H):
            for i in range(W):
                c = grid[j][i]
                col[j * W + i] = 255 if c is None else pal.index(TONES[c[0]][c[1]])
        hb = bytes(int(min(254, max(0, v))) for v in hgt.ravel())
        lines.append('        "' + base64.b64encode(zlib.compress(bytes(col) + hb, 9)).decode() + '",')
    lines += ["    };", "}"]
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", ax, ay)
