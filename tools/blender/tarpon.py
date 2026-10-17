# Haring Buan-buan, the king of the tarpon at Parola, modelled and rendered to pixel art for its boss fight (TarponBoss.cs;
# see pixelrender.py for the method). Run:  blender -b --python tools/blender/tarpon.py -- <out_dir> [cs_path]
#
# Seen from the side and a little above (the game's view), facing east (the game mirrors it for west): a deep silver body
# in huge scales, a blue-green back, an upturned mouth with a jutting lower jaw, a big eye, one dorsal fin whose last ray
# trails back as a long filament, and a deeply forked tail. Some scales are a special material (written as tones for the
# game to colour), so they flash in the lighthouse beam and the moonlight. Frames: nose up 60 to down 60, three steps of
# the tail each, the mouth open (rolling to gulp air), and rolled on its side (the head-shake in a leap). Every pixel
# stores its height (rows, offset by Lift) so the game can sink it.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from pixelrender import Renderer, args
import base64, zlib

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

TONES = {
    "silver":  ("#7d8c99", "#b8c6d2", "#e8eff5"),
    "rim":     ("#5b6a78", "#8494a3", "#b3c2cf"),      # the edge of each big scale
    "back":    ("#1f3b4a", "#2f5a6c", "#4b8296"),
    "belly":   ("#a9b6c0", "#dfe7ee", "#fbfdff"),
    "fin":     ("#4a5a66", "#6f818f", "#9db0bd"),
    "tail":    ("#3d4c58", "#5c6d7b", "#8798a6"),
    "jaw":     ("#6b7884", "#97a5b1", "#c7d3dd"),
    "mouth":   ("#14181c", "#262c33", "#3a424b"),
    "eye":     ("#0a0c0e", "#121619", "#e9eef2"),
    "iris":    ("#6b5a2a", "#a88a3a", "#d8b75a"),
}
SPECIAL = "glint"
W, H = 72, 72
LIFT = 40
HX, L = 21.0, 38.0
UPS = [60, 40, 20, 0, -20, -40, -60]
PHASES = 3
OPEN_UPS = [30, 15]
ROLLS = [(40, 40), (40, -40), (0, 50)]     # (nose up, roll): shaking its head in a leap, and rolled at the surface


def bend(x, ph, amp=2.2):
    k = max(0.0, (HX - x) / L)
    return amp * k * k * math.sin(ph - k * 3.5)


def half(k):
    """The body's half-height at k (0 snout, 1 tail root): a sharp head rising steeply to a deep body, then a taper."""
    if k < 0.18:
        return 1.8 + (6.4 - 1.8) * (k / 0.18) ** 0.8
    if k < 0.5:
        return 6.4 + 0.5 * math.sin((k - 0.18) / 0.32 * math.pi / 2)
    return max(1.5, 6.9 - 5.4 * ((k - 0.5) / 0.5) ** 1.15)


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


def ball(r, loc, scale, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=20, ring_count=10, location=loc)
    o = bpy.context.object
    o.scale = scale
    o.rotation_euler = [math.radians(v) for v in rot]
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def build(r, up, ph, open_mouth, roll=0):
    M = r.ids
    scales = r.banded("scales", "X", 3.4, "silver", "rim", 0.78)     # the big scales, a row every 3.4 px
    parts = []
    spine = []
    for i in range(34):
        k = i / 33
        x = HX - L * k
        spine.append((x, bend(x, ph), 0, half(k) * 1.55, 0.55, 1.0))   # deep and narrow: a tarpon's flat flanks
    body = meta_body(r, "body", spine, scales)
    body.data.materials.append(M["back"])
    body.data.materials.append(M["belly"])
    body.data.materials.append(r.ids[SPECIAL])
    random.seed(5)
    glints = [(random.uniform(-14, 14), random.uniform(-4, 4)) for _ in range(16)]
    for p in body.data.polygons:
        cx, cy, cz = p.center
        k = (HX - cx) / L
        hh = half(min(1, max(0, k)))
        if cz > hh * 0.55:
            p.material_index = 1               # the blue-green back
        elif cz < -hh * 0.45:
            p.material_index = 2               # the white belly
        elif cy < 0 and any((cx - gx) ** 2 + (cz - gz) ** 2 < 1.1 for gx, gz in glints):
            p.material_index = 3               # a scale catching the light
    parts.append(body)

    def at(x):
        return half(min(1, max(0, (HX - x) / L)))
    # The dorsal fin, its last ray trailing back as a long thread.
    dy = bend(-2, ph)
    parts.append(fin_xz(r, "dorsal", [(1, at(1) - 0.8), (0, at(0) + 5.6), (-2, at(-2) + 5.4), (-5, at(-5) + 2.6), (-14, at(-14) + 3.6),
                                      (-14, at(-14) + 2.9), (-5, at(-5) + 1.2), (-4, at(-4) - 0.8)], dy, M["fin"], 0.6))
    # The anal fin, the pelvic and pectoral fins.
    parts.append(fin_xz(r, "anal", [(-6, -at(-6) + 0.8), (-7, -at(-7) - 3.6), (-11, -at(-11) - 2.6), (-12, -at(-12) + 0.6)], bend(-9, ph), M["fin"], 0.5))
    parts.append(fin_xz(r, "pelvic", [(1, -at(1) + 0.6), (-1, -at(-1) - 2.8), (-3, -at(-3) - 2.2), (-2, -at(-2) + 0.6)], bend(-1, ph) - 1.4, M["fin"], 0.4))
    parts.append(fin_xz(r, "pec", [(13, -2.2), (8.5, -5.2), (7.5, -4.4), (11.5, -1.6)], bend(11, ph) - 2.4, M["fin"], 0.4))
    # The deeply forked tail.
    ty = bend(-21, ph)
    parts.append(fin_xz(r, "tail", [(-16.5, 1.4), (-20, 5.6), (-26, 9.5), (-24, 4.4), (-21.5, 0), (-24, -4.4), (-26, -9.5),
                                    (-20, -5.6), (-16.5, -1.4)], ty, M["tail"], 0.6))
    # The upturned mouth: the big lower jaw jutting up past the snout; open, it drops (a gulp of air at the surface).
    hy = bend(HX, ph)
    if open_mouth:
        parts.append(ball(r, (HX - 0.6, hy, 0.4), (2.6, 1.3, 2.0), M["mouth"]))
        parts.append(ball(r, (HX + 0.6, hy, -2.2), (3.4, 1.4, 1.0), M["jaw"], (0, 30, 0)))
    else:
        # The gape runs up and back from the jutting lower jaw, a dark line across the head.
        parts.append(ball(r, (HX + 1.4, hy, -0.6), (3.4, 1.6, 1.2), M["jaw"], (0, -34, 0)))
        parts.append(ball(r, (HX - 1.6, hy, 0.9), (3.0, 1.9, 0.35), M["mouth"], (0, -30, 0)))
    for s in (-1, 1):
        parts.append(ball(r, (HX - 4.6, hy + s * 3.0, 1.4), (1.6, 0.7, 1.6), M["iris"]))
        parts.append(ball(r, (HX - 4.4, hy + s * 3.45, 1.4), (0.85, 0.5, 0.85), M["eye"]))
    root = bpy.data.objects.new("root", None)
    r.scene.collection.objects.link(root)
    for p in parts:
        p.parent = root
    # Rolled about its own length first, then tipped nose up.
    root.rotation_mode = "XYZ"
    root.rotation_euler = (math.radians(roll), -math.radians(up), 0)
    root.location = (0, 0, r.Z(LIFT))
    bpy.context.view_layer.update()


specs = [(up, f, False, 0) for up in UPS for f in range(PHASES)] + [(up, 0, True, 0) for up in OPEN_UPS] + [(up, 1, False, rl) for up, rl in ROLLS]
if os.environ.get("FESH_QUICK"):
    specs = [(0, 0, False, 0), (40, 1, False, 0), (15, 0, True, 0), (40, 1, False, 40), (0, 1, False, 50)]
frames, meta = [], []
anchor = None
for n, (up, f, op, rl) in enumerate(specs):
    r = Renderer(W, H, pitch=20, tones=TONES, special=SPECIAL)
    r.keep_blend = n == 0
    build(r, up, 2 * math.pi * f / PHASES, op, rl)
    r.aim((0, 0, LIFT))
    grid = r.render(OUT, heights=True)
    anchor = r.anchor((0, 0, LIFT))
    frames.append((grid, r.last_heights))
    meta.append((up, f, op, rl))
    print("frame", n, up, f, op, rl)

r.preview(os.path.join(OUT, "tarpon-preview.png"), [g for g, _ in frames], special_colours=["#c7d6e6", "#f2f7ff", "#ffffff"], scale=5)

if CS:
    pal = r.colours([g for g, _ in frames])
    lines = [
        "// Generated by tools/blender/tarpon.py from a 3D model of Haring Buan-buan: don't edit by hand, change the model and",
        "// re-run the script. Each frame is zlib-compressed bytes in base64: Width x Height palette indices (255 clear, 250-252",
        "// a scale catching the light, for the game to colour), then as many heights in rows (its middle at Lift). Up is how",
        "// far the nose is tipped up (degrees), Phase the step of the tail, Open the mouth open, Roll how far it's rolled on",
        "// its side. The anchor is its middle.",
        "namespace Fesh;",
        "",
        "static class TarponArt",
        "{",
        f"    public const int Width = {W}, Height = {H}, AnchorX = {anchor[0]}, AnchorY = {anchor[1]}, Lift = {LIFT}, Phases = {PHASES};",
        "    public static readonly string[] Palette = { " + ", ".join(f'"{c}"' for c in pal) + " };",
        "    public static readonly int[] Up = { " + ", ".join(str(m[0]) for m in meta) + " };",
        "    public static readonly int[] Phase = { " + ", ".join(str(m[1]) for m in meta) + " };",
        "    public static readonly bool[] Open = { " + ", ".join("true" if m[2] else "false" for m in meta) + " };",
        "    public static readonly int[] Roll = { " + ", ".join(str(m[3]) for m in meta) + " };",
        "    public static readonly string[] Data =",
        "    {",
    ]
    for grid, hgt in frames:
        col = bytearray(W * H)
        for j in range(H):
            for i in range(W):
                c = grid[j][i]
                col[j * W + i] = 255 if c is None else (250 + c[1] if c[0] == SPECIAL else pal.index(TONES[c[0]][c[1]]))
        hb = bytes(int(min(254, max(0, v))) for v in hgt.ravel())
        lines.append('        "' + base64.b64encode(zlib.compress(bytes(col) + hb, 9)).decode() + '",')
    lines += ["    };", "}"]
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", anchor)
