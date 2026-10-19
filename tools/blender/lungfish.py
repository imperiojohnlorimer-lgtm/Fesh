# The Sunscale lungfish, the oasis legend, modelled and rendered to pixel art for its boss fight (LungfishBoss.cs; see
# pixelrender.py for the method). Run:  blender -b --python tools/blender/lungfish.py -- <out_dir> [cs_path]
#
# A real African lungfish (Protopterus) is long and eel-like, with a blunt head, tiny eyes, four thin whip-like paired
# fins and one fringe of fin running from the back round the tail to the belly. This one is the game's gold: a sun-gold
# back mottled with orange, a pale belly and a rust-brown fringe. Seen from the side and a little above, facing east
# (the game mirrors it for west). Frames: the body tipped nose-up to nose-down (to gulp air, to leap), each at three
# steps of the body's wave, plus the mouth open for the gulp. Every pixel stores its height (in rows, offset by Lift) so
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
    "gold":    ("#a8631d", "#e0963a", "#f5c466"),
    "blotch":  ("#8a3510", "#c85a1e", "#e8843a"),
    "head":    ("#7a4a16", "#c88430", "#e8b255"),
    "belly":   ("#b79a62", "#e4cf95", "#f8ecc2"),
    "fringe":  ("#5a2e16", "#8a4c26", "#b5764a"),
    "thread":  ("#6e4a24", "#a9763a", "#d3a05a"),
    "mouth":   ("#2a0f0a", "#4d1a12", "#7a2c1e"),
    "eye":     ("#0c0c0a", "#2a2218", "#8a7a4a"),
}
W, H = 80, 80
LIFT = 42              # the model's centre sits LIFT rows up, so every stored height is positive
HX, L = 24.0, 46.0     # snout at x = HX, the tail's tip L px behind it
UPS = [60, 40, 20, 0, -20, -40, -60]      # nose up (degrees) to nose down
PHASES = 3
OPEN_UPS = [40, 20, 0]


def bend(x, ph, amp=3.0):
    """How far the body swings sideways at x: little at the head, most at the tail, a wave running back (an eel's swim)."""
    k = max(0.0, (HX - x) / L)
    return amp * (0.25 + k) * math.sin(ph - k * 5.0)


def half(k):
    """The body's half-height at k (0 snout, 1 tail tip): a blunt head, a round even body, a long taper to a point."""
    if k < 0.08:
        return 1.7 + (2.4 - 1.7) * (k / 0.08) ** 0.6
    if k < 0.6:
        return 2.4 + 0.15 * math.sin((k - 0.08) / 0.52 * math.pi)
    return max(0.6, 2.4 - 1.8 * ((k - 0.6) / 0.4) ** 1.2)


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
    parts = []
    spine = []
    for i in range(40):
        k = i / 39
        x = HX - L * k
        spine.append((x, 0.5 * bend(x, ph), 0.9 * bend(x, ph), half(k) * 1.55, 0.95, 0.95))
    body = meta_body(r, "body", spine, M["gold"])
    body.data.materials.append(M["blotch"])
    body.data.materials.append(M["head"])
    body.data.materials.append(M["belly"])
    for p in body.data.polygons:
        cx, cy, cz = p.center
        k = (HX - cx) / L
        hh = half(min(1, max(0, k)))
        if cz < -hh * 0.45:
            p.material_index = 3               # the pale belly
        elif k < 0.09:
            p.material_index = 2               # the blunt head
        else:
            u, v = math.floor(cx / 2.6 + 0.4 * math.sin(cz)), math.floor((cz + 8) / 2.0)
            hsh = (math.sin(u * 12.9898 + v * 78.233) * 43758.5453) % 1.0
            if hsh < 0.26 and cz > -hh * 0.2:
                p.material_index = 1           # an orange blotch
    parts.append(body)

    def at(x):
        return half(min(1, max(0, (HX - x) / L)))

    # One fringe of fin: the dorsal part from a third back, round the pointed tail, along the belly.
    def fringe(x0, x1, sign):
        pts = []
        n = 9
        for i in range(n + 1):
            x = x0 + (x1 - x0) * i / n
            pts.append((x, sign * (at(x) + 0.9 * math.sin(math.pi * (i / n)) * (0.5 + 0.5 * (1 - i / n)) + 0.2) + 0.9 * bend(x, ph)))
        for i in range(n, -1, -1):
            x = x0 + (x1 - x0) * i / n
            pts.append((x, sign * (at(x) - 0.4) + 0.9 * bend(x, ph)))
        return pts
    for x0, x1, sign, nm in ((HX - L * 0.30, HX - L * 0.98, 1, "dorsal"), (HX - L * 0.62, HX - L * 0.98, -1, "anal")):
        parts.append(fin_xz(r, nm, fringe(x0, x1, sign), 0.5 * (bend(x0, ph) + bend(x1, ph)) / 2, M["fringe"], 0.5))
    # Four thread-thin fins: a long pair behind the head and a pair a third of the way along, trailing back and down.
    for (x, drop, ln) in ((HX - 7, -2.2, 9.0), (HX - 18, -2.2, 8.0)):
        for s in (-1, 1):
            y = 0.5 * bend(x, ph) + s * 2.0
            zb = 0.9 * bend(x, ph)
            pts = [(x, zb + drop + 0.5), (x - ln, zb + drop - 3.0), (x - ln - 1.2, zb + drop - 3.8), (x - 1, zb + drop - 0.6)]
            parts.append(fin_xz(r, "thread%d%d" % (int(x), s), pts, y, M["thread"], 0.45))
    # Blunt snout, a tiny eye each side; open, the lower jaw drops and shows the dark inside.
    hy = 0.5 * bend(HX, ph)
    if open_mouth:
        parts.append(ball(r, (HX + 0.2, hy, 0.9 * bend(HX, ph) - 0.4), (2.2, 2.2, 1.6), M["mouth"]))
        parts.append(ball(r, (HX + 0.6, hy, 0.9 * bend(HX, ph) - 2.4), (2.4, 2.0, 0.9), M["head"], (0, 34, 0)))
    else:
        parts.append(ball(r, (HX + 0.5, hy, 0.9 * bend(HX, ph) - 0.2), (1.5, 1.7, 1.2), M["head"], (0, -12, 0)))
    for s in (-1, 1):
        parts.append(ball(r, (HX - 3.0, hy + s * 2.2, 0.9 * bend(HX - 3.0, ph) + 0.8), (0.55, 0.45, 0.55), M["eye"]))
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

r.preview(os.path.join(OUT, "lungfish-preview.png"), [g for g, _ in frames], scale=5)

if CS:
    pal = r.colours([g for g, _ in frames])
    lines = [
        "// Generated by tools/blender/lungfish.py from a 3D model of the Sunscale lungfish: don't edit by hand, change the",
        "// model and re-run the script. Each frame is zlib-compressed bytes in base64: Width x Height palette indices (255",
        "// clear), then as many heights in rows (the fish's middle is at Lift). Up is how far the nose is tipped up",
        "// (degrees), Phase the step of the body's wave, Open the mouth open to gulp air. The anchor is the fish's middle.",
        "namespace Fesh;",
        "",
        "static class LungfishArt",
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
