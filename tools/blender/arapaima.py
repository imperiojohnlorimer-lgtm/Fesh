# The Mire leviathan, a giant arapaima, modelled and rendered to pixel art for its boss fight (MireBoss.cs; see
# pixelrender.py for the method). Run:  blender -b --python tools/blender/arapaima.py -- <out_dir> [cs_path]
#
# Seen from the side and a little above (the game's view), facing east (the game mirrors it for west): a long round
# body, a broad flat head with an upturned mouth, big scales olive at the front and edged red toward the tail (its
# Amazonian name, pirarucu, means "red fish"), and the dorsal and anal fins set far back so with the rounded tail they
# make one paddle. Frames: the body tipped nose-up to nose-down (for breaching, gulping air and leaping), each at three
# steps of the tail's beat, plus the mouth open for the gulp. Every pixel stores its height (in rows, offset by Lift) so
# the game can sink the fish to any depth and keep only what is above the water.
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from pixelrender import Renderer, args
import base64, zlib

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

TONES = {
    "olive":   ("#26402c", "#3b5e42", "#5f8a63"),
    "oliverim": ("#1a2d20", "#2a4532", "#406448"),
    "red":     ("#4f1913", "#86291e", "#b9492f"),
    "redrim":  ("#36110d", "#5c1c15", "#842d20"),
    "head":    ("#18261c", "#2a3f2f", "#466249"),
    "belly":   ("#6f7b66", "#9eaa92", "#c8d2b8"),
    "fin":     ("#5e1d14", "#8a2e1f", "#bb4d31"),      # the red margin of each fin
    "finin":   ("#1f1a14", "#2f271d", "#46392a"),      # and its dark middle
    "findk":   ("#33130e", "#4f1c14", "#73291d"),      # the long dorsal and anal fins, darker than the scales
    "pec":     ("#34503b", "#46664d", "#62856a"),
    "mouth":   ("#1c0909", "#3a1312", "#5a2220"),
    "eye":     ("#0c0c0a", "#d6b65a", "#f2e39a"),
}
W, H = 76, 76
LIFT = 40              # the model's centre sits LIFT rows up, so every stored height is positive
HX, L = 22.0, 44.0     # snout at x = HX, the tail's root L px behind it
UPS = [60, 40, 20, 0, -20, -40, -60]      # nose up (degrees) to nose down
PHASES = 3
OPEN_UPS = [40, 20, 0]


def bend(x, ph, amp=2.4):
    """How far the body swings sideways at x: nothing at the head, most at the tail, a wave running back."""
    k = max(0.0, (HX - x) / L)
    return amp * k * k * math.sin(ph - k * 3.5)


def half(k):
    """The body's half-height at k (0 snout, 1 tail root): a flat broad head, deepest a third back, a long taper."""
    if k < 0.14:
        return 2.3 + (4.3 - 2.3) * (k / 0.14) ** 0.7
    if k < 0.45:
        return 4.3 + 0.7 * math.sin((k - 0.14) / 0.31 * math.pi / 2)
    return max(1.6, 5.0 - 3.4 * ((k - 0.45) / 0.55) ** 1.1)


def meta_body(r, name, spine, mat, res=0.3):
    mb = bpy.data.metaballs.new(name)
    mb.resolution = res
    mb.render_resolution = res
    mb.threshold = 0.6
    for x, y, z, rad, sy, sz in spine:
        e = mb.elements.new(type="ELLIPSOID")
        e.co = (x, y, z)
        e.radius = rad
        e.size_x, e.size_y, e.size_z = 1.0, sy, sz
        e.stiffness = 2.0
    o = bpy.data.objects.new(name, mb)
    r.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.object
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def fin_xz(r, name, pts, y, mat, thick=0.6):
    """A flat fin standing upright along the body: an outline in (x, z), at depth y."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, z in pts]
    bm.faces.new(vs)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    r.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    s = o.modifiers.new("t", "SOLIDIFY")
    s.thickness = thick
    s.offset = 0
    return o


def fin2(r, name, pts, y, M, thick=0.6):
    """A fin with a red margin: the outline in red, and a smaller dark copy just in front of it."""
    fin_xz(r, name, pts, y, M["fin"], thick)
    cx = sum(p[0] for p in pts) / len(pts)
    cz = sum(p[1] for p in pts) / len(pts)
    inner = [(cx + (x - cx) * 0.68, cz + (z - cz) * 0.68) for x, z in pts]
    return [bpy.data.objects[name], fin_xz(r, name + "_in", inner, y - thick * 0.9, M["finin"], thick)]


def ball(r, loc, scale, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=20, ring_count=10, location=loc)
    o = bpy.context.object
    o.scale = scale
    o.rotation_euler = [math.radians(v) for v in rot]
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def build(r, up, ph, open_mouth):
    M = r.ids
    olive = r.banded("scales_f", "X", 2.7, "olive", "oliverim", 0.72)
    red = r.banded("scales_r", "X", 2.7, "red", "redrim", 0.66)
    parts = []
    spine = []
    for i in range(34):
        k = i / 33
        x = HX - L * k
        sy = 0.9 - 0.25 * k                     # rounder at the front, flatter from side to side toward the tail
        sz = 0.78 if k < 0.12 else 1.0         # the flat head
        spine.append((x, bend(x, ph), 0, half(k) * 1.55, sy, sz))   # a metaball surface sits well inside its radius
    body = meta_body(r, "body", spine, olive)
    body.data.materials.append(red)
    body.data.materials.append(M["head"])
    body.data.materials.append(M["belly"])
    for p in body.data.polygons:
        cx, cy, cz = p.center
        k = (HX - cx) / L
        hh = half(min(1, max(0, k)))
        if cz < -hh * 0.42 and k < 0.8:
            p.material_index = 3               # the pale belly
        elif k < 0.16:
            p.material_index = 2               # the bony head
        elif k > 0.5 + 0.06 * math.sin(cz * 0.9):
            p.material_index = 1               # red-edged scales toward the tail
    parts.append(body)
    # Dorsal and anal fins far back, the rounded tail: one paddle.
    def at(x):
        return half(min(1, max(0, (HX - x) / L)))
    dy = bend(-14, ph)
    parts.append(fin_xz(r, "dorsal", [(-6, at(-6) - 0.6), (-8.5, at(-8.5) + 3.4), (-13, at(-13) + 4.6), (-18, at(-18) + 4.0),
                                      (-21, at(-21) + 1.2), (-21, at(-21) - 0.8), (-6, at(-6) - 1.4)], dy, M["findk"]))
    parts.append(fin_xz(r, "anal", [(-7, -at(-7) + 0.6), (-9.5, -at(-9.5) - 3.2), (-14, -at(-14) - 4.2), (-18.5, -at(-18.5) - 3.6),
                                    (-21, -at(-21) - 1.2), (-21, -at(-21) + 0.8), (-7, -at(-7) + 1.4)], dy, M["findk"]))
    ty = bend(-24, ph)
    parts.extend(fin2(r, "tail", [(-20.5, 1.6), (-23, 4.6), (-26.5, 5.0), (-29.5, 2.4), (-30, 0), (-29.5, -2.4),
                                    (-26.5, -5.0), (-23, -4.6), (-20.5, -1.6)], ty, M))
    # Small pectoral fins low behind the head, and the pelvic fins halfway along.
    py = bend(13, ph) - 3.2
    parts.append(fin_xz(r, "pec", [(14, -1.6), (10.5, -4.4), (9, -3.8), (12.5, -1.0)], py, M["pec"], 0.5))
    parts.append(fin_xz(r, "pelvic", [(1, -4.2), (-2.5, -6.0), (-3.5, -5.2), (0, -3.8)], bend(-1, ph) - 2.6, M["pec"], 0.5))
    # The upturned mouth: the lower jaw jutting up in front; open, it drops and shows the dark inside.
    hy = bend(HX, ph)
    if open_mouth:
        parts.append(ball(r, (HX + 0.4, hy - 1.0, 0.4), (2.6, 2.6, 2.0), M["mouth"]))
        parts.append(ball(r, (HX + 1.0, hy - 0.6, -2.6), (2.8, 2.2, 1.0), M["head"], (0, 34, 0)))
    else:
        parts.append(ball(r, (HX + 0.6, hy, -0.4), (1.8, 1.7, 1.1), M["head"], (0, -18, 0)))
    for s in (-1, 1):
        parts.append(ball(r, (HX - 4.4, hy + s * 3.9, 1.0), (0.85, 0.6, 0.85), M["eye"]))
    # Tip the whole fish nose up or down about its middle, lifted so every height is positive.
    root = bpy.data.objects.new("root", None)
    r.scene.collection.objects.link(root)
    for p in parts:
        p.parent = root
    root.rotation_euler = (0, -math.radians(up), 0)
    root.location = (0, 0, r.Z(LIFT))
    bpy.context.view_layer.update()


frames, meta = [], []
specs = [(up, f, False) for up in UPS for f in range(PHASES)] + [(up, 0, True) for up in OPEN_UPS]
if os.environ.get("FESH_QUICK"):
    specs = [(0, 0, False), (0, 0, True), (40, 1, False)]   # a look at the model without rendering every frame
anchor = None
for n, (up, f, op) in enumerate(specs):
    r = Renderer(W, H, pitch=20, tones=TONES)
    r.keep_blend = n == 0
    build(r, up, 2 * math.pi * f / PHASES, op)
    r.aim((0, 0, LIFT))
    grid = r.render(OUT, heights=True)
    anchor = r.anchor((0, 0, LIFT))
    frames.append((grid, r.last_heights))
    meta.append((up, f, op))
    print("frame", n, up, f, op)

r.preview(os.path.join(OUT, "arapaima-preview.png"), [g for g, _ in frames], scale=5)

if CS:
    pal = r.colours([g for g, _ in frames])
    lines = [
        "// Generated by tools/blender/arapaima.py from a 3D model of the Mire leviathan: don't edit by hand, change the",
        "// model and re-run the script. Each frame is zlib-compressed bytes in base64: Width x Height palette indices (255",
        "// clear), then as many heights in rows (the fish's middle is at Lift). Up is how far the nose is tipped up",
        "// (degrees), Phase the step of the tail's beat, Open the mouth open to gulp air. The anchor is the fish's middle.",
        "namespace Fesh;",
        "",
        "static class ArapaimaArt",
        "{",
        f"    public const int Width = {W}, Height = {H}, AnchorX = {anchor[0]}, AnchorY = {anchor[1]}, Lift = {LIFT}, Phases = {PHASES};",
        "    public static readonly string[] Palette = { " + ", ".join(f'"{c}"' for c in pal) + " };",
        "    public static readonly int[] Up = { " + ", ".join(str(m[0]) for m in meta) + " };",
        "    public static readonly int[] Phase = { " + ", ".join(str(m[1]) for m in meta) + " };",
        "    public static readonly bool[] Open = { " + ", ".join("true" if m[2] else "false" for m in meta) + " };",
        "    public static readonly string[] Data =",
        "    {",
    ]
    for grid, hgt in frames:
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
print("DONE", anchor)
