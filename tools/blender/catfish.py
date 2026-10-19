# Old Whiskers, the lagoon's legendary catfish, modelled and rendered to pixel art for its boss fight (WhiskersBoss.cs; see
# pixelrender.py for the method). Run:  blender -b --python tools/blender/catfish.py -- <out_dir> [cs_path]
#
# Based on the wels catfish (Silurus glanis): a broad flat head with a wide mouth and tiny eyes, two long barbels
# (whiskers) from the upper jaw and short ones under the chin, a smooth scaleless body, a very long anal fin and a small
# dorsal, and a rounded tail. In the game's amber and brown, marbled darker, with a pale belly and pale-gold whiskers.
# Seen from the side and a little above, facing east (the game mirrors it for west). Frames: the body tipped nose-up to
# nose-down, each at three steps of the swimming wave (the whiskers trail in it), plus the mouth open. Every pixel stores
# its height (in rows, offset by Lift) so the game can sink the fish to any depth and keep only what is above the water.
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
    "amber":   ("#6a4418", "#a8742a", "#d09a4a"),
    "mottle":  ("#3e2a12", "#5e3f1a", "#85602c"),
    "head":    ("#4e3416", "#7e5524", "#a4783a"),
    "belly":   ("#a48a58", "#d2b97c", "#ecdba4"),
    "fin":     ("#4a3014", "#7a5226", "#a07640"),
    "barbel":  ("#8a6a34", "#c9a55a", "#f0d890"),
    "mouth":   ("#2a0f0a", "#4d1a12", "#7a2c1e"),
    "eye":     ("#0c0c0a", "#2a2218", "#c9b36a"),
}
W, H = 64, 64
LIFT = 32              # the model's centre sits LIFT rows up, so every stored height is positive
HX, L = 18.0, 29.0     # snout at x = HX, the tail's root L px behind it
UPS = [60, 40, 20, 0, -20, -40, -60]      # nose up (degrees) to nose down
PHASES = 3
OPEN_UPS = [40, 20, 0]


def bend(x, ph, amp=2.0):
    """How far the body swings sideways at x: nothing at the head, most at the tail, a wave running back."""
    k = max(0.0, (HX - x) / L)
    return amp * k * k * math.sin(ph - k * 3.2)


def half(k):
    """The body's half-height at k (0 snout, 1 tail root): a broad flat head, deepest behind it, a long taper."""
    if k < 0.2:
        return 2.4 + (4.3 - 2.4) * (k / 0.2) ** 0.55
    if k < 0.45:
        return 4.3 + 0.5 * math.sin((k - 0.2) / 0.25 * math.pi / 2)
    return max(1.7, 4.8 - 3.1 * ((k - 0.45) / 0.55) ** 1.1)


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


def ribbon(r, name, pts, y, mat, width=1.3, thick=0.9):
    """A thin strip along a polyline in (x, z) at depth y (a whisker)."""
    left, right = [], []
    for i, (x, z) in enumerate(pts):
        j0, j1 = max(0, i - 1), min(len(pts) - 1, i + 1)
        dx, dz = pts[j1][0] - pts[j0][0], pts[j1][1] - pts[j0][1]
        n = math.hypot(dx, dz) or 1.0
        nx, nz = -dz / n, dx / n
        left.append((x + nx * width / 2, z + nz * width / 2))
        right.append((x - nx * width / 2, z - nz * width / 2))
    return fin_xz(r, name, left + right[::-1], y, mat, thick)


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
    for i in range(34):
        k = i / 33
        x = HX - L * k
        sy = 1.15 - 0.55 * k                  # broad from side to side at the head, flatter toward the tail
        sz = 0.62 if k < 0.2 else 0.95        # the flat head
        spine.append((x, bend(x, ph), 0, half(k) * 1.5, sy, sz))
    body = meta_body(r, "body", spine, M["amber"])
    body.data.materials.append(M["mottle"])
    body.data.materials.append(M["head"])
    body.data.materials.append(M["belly"])
    for p in body.data.polygons:
        cx, cy, cz = p.center
        k = (HX - cx) / L
        hh = half(min(1, max(0, k)))
        if cz < -hh * 0.4 and k < 0.9:
            p.material_index = 3               # the pale belly
        elif k < 0.22:
            p.material_index = 2               # the broad head
        else:
            u, v = math.floor(cx / 2.2 + 0.5 * math.sin(cz * 0.8)), math.floor((cz + 9) / 1.8)
            hsh = (math.sin(u * 12.9898 + v * 78.233) * 43758.5453) % 1.0
            if hsh < 0.34:
                p.material_index = 1           # darker marbling
    parts.append(body)

    def at(x):
        return half(min(1, max(0, (HX - x) / L)))
    # The long anal fin along the belly, the small dorsal, and the rounded tail.
    dy = bend(HX - L * 0.6, ph)
    parts.append(fin_xz(r, "anal", [(HX - L * 0.30, -at(HX - L * 0.30) + 0.5), (HX - L * 0.42, -at(HX - L * 0.42) - 2.0),
                                    (HX - L * 0.65, -at(HX - L * 0.65) - 2.4), (HX - L * 0.9, -at(HX - L * 0.9) - 1.0),
                                    (HX - L * 0.95, -at(HX - L * 0.95) + 0.6)], dy, M["fin"], 0.5))
    parts.append(fin_xz(r, "dorsal", [(HX - L * 0.26, at(HX - L * 0.26) - 0.6), (HX - L * 0.3, at(HX - L * 0.3) + 2.2),
                                      (HX - L * 0.4, at(HX - L * 0.4) + 1.8), (HX - L * 0.44, at(HX - L * 0.44) - 0.8)],
                       bend(HX - L * 0.34, ph), M["fin"], 0.5))
    ty = bend(HX - L - 3, ph)
    parts.append(fin_xz(r, "tail", [(HX - L + 1.0, 1.2), (HX - L - 1.6, 3.0), (HX - L - 3.8, 2.6), (HX - L - 4.8, 0),
                                    (HX - L - 3.8, -2.6), (HX - L - 1.6, -3.0), (HX - L + 1.0, -1.2)], ty, M["fin"], 0.6))
    # Pectoral fins, low behind the head.
    parts.append(fin_xz(r, "pec", [(HX - 6, -2.6), (HX - 10.5, -5.4), (HX - 12, -4.6), (HX - 9, -1.6)], bend(HX - 8, ph) - 3.4, M["fin"], 0.5))
    # The whiskers: two long ones from the upper lip, trailing back in the swim, and short ones under the chin.
    hy = bend(HX, ph)
    for s in (-1, 1):
        wave = ph + (0.8 if s > 0 else 0)
        pts = []
        for i in range(9):
            q = i / 8
            pts.append((HX + 2.0 + q * 6.0 - q * q * 20.0 + math.sin(wave + q * 3.0) * 1.0, -0.5 - 8.0 * q + math.sin(wave + q * 4.0) * 0.9))
        parts.append(ribbon(r, "whisker%d" % s, pts, hy + s * 6.5, M["barbel"], 1.9, 1.6))
        parts.append(ribbon(r, "chin%d" % s, [(HX - 0.5, -2.6), (HX - 3.4, -5.4), (HX - 6.0, -6.4)], hy + s * 3.6, M["barbel"], 1.5, 1.2))
    # The wide mouth: a lower jaw across the front; open, it drops and shows the dark inside.
    if open_mouth:
        parts.append(ball(r, (HX + 0.4, hy, -0.6), (2.8, 3.4, 2.0), M["mouth"]))
        parts.append(ball(r, (HX + 0.4, hy, -3.2), (3.0, 3.6, 1.0), M["head"], (0, 30, 0)))
    else:
        parts.append(ball(r, (HX + 0.4, hy, -0.9), (2.4, 3.4, 1.2), M["head"], (0, -10, 0)))
    for s in (-1, 1):
        parts.append(ball(r, (HX - 3.6, hy + s * 3.4, 1.8), (0.7, 0.6, 0.7), M["eye"]))
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

r.preview(os.path.join(OUT, "catfish-preview.png"), [g for g, _ in frames], scale=5)

if CS:
    pal = r.colours([g for g, _ in frames])
    lines = [
        "// Generated by tools/blender/catfish.py from a 3D model of Old Whiskers: don't edit by hand, change the model and",
        "// re-run the script. Each frame is zlib-compressed bytes in base64: Width x Height palette indices (255 clear), then",
        "// as many heights in rows (the fish's middle is at Lift). Up is how far the nose is tipped up (degrees), Phase the",
        "// step of the swimming wave, Open the mouth open. The anchor is the fish's middle.",
        "namespace Fesh;",
        "",
        "static class WhiskersArt",
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
