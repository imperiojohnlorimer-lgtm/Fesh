# The Starfall ray, a manta whose back glitters like the night sky, modelled and rendered to pixel art for its boss fight
# (RayBoss.cs; see pixelrender.py for the method). Run:  blender -b --python tools/blender/manta.py -- <out_dir> [cs_path]
#
# A giant manta seen from above and a little in front (the game's view): broad swept-back wings that beat, the two
# cephalic fins curled forward at the head, pale shoulder patches, a long thin tail, a white belly. The "stars" on its back
# are a special material (written as tones the game colours itself, so they can twinkle and glow after dark). Frames:
# gliding flat at eight headings with three steps of the wing beat, then a loop facing east (nose up to upside down and
# round), for its somersaults and breaches. Every pixel stores its height (rows, offset by Lift) so the game can sink it.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from pixelrender import Renderer, args, SPECIAL_KEYS
import base64, zlib

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

TONES = {
    "back":   ("#0c1630", "#16264c", "#26407a"),
    "edge":   ("#08101f", "#101c38", "#1c2f5a"),      # the dark rim of the wings
    "patch":  ("#5b6f8e", "#8ea2bf", "#c3d2e6"),      # the pale shoulder patches
    "belly":  ("#9aa8b8", "#d6dee8", "#f4f7fb"),
    "spot":   ("#1d2a3c", "#2f3f55", "#46586f"),      # the belly's own spots, a pattern every manta has
    "cephal": ("#0e1a36", "#1b2f5c", "#2f4c88"),
    "mouth":  ("#05080f", "#0c1120", "#18213a"),
    "tail":   ("#0a1226", "#142240", "#22355f"),
    "eye":    ("#05070c", "#0b0f18", "#9fb2d0"),
}
SPECIAL = "star"
W, H = 84, 84
LIFT = 40
SPAN = 23.0          # half the wingspan
HEADINGS = [0, 45, 90, 135, 180, 225, 270, 315]   # degrees, 0 east, counter-clockwise seen from above
FLAPS = 3
LOOPS = [45, 90, 135, 180, 225, 270, 315]


def outline_half_width(x):
    """How far the wing reaches to each side at x (head +12, tail root -11): a straight leading edge sweeping back from
    the head to a pointed tip a little behind the middle, then a concave trailing edge back to the tail."""
    tip_x = -2.0
    if x > 12 or x < -11:
        return 0
    if x >= tip_x:
        return 6.5 + (12 - x) / (12 - tip_x) * (SPAN - 6.5)
    return 3 + (SPAN - 3) * ((x + 11) / (tip_x + 11)) ** 2.2


def wing_z(x, y, flap):
    """The wing's height: thick over the body, thin at the tips, the tips raised or lowered by the beat."""
    k = abs(y) / SPAN
    return flap * 7.5 * k ** 1.8 - 0.6 * k


def meta(r, name, elems, mat, res=0.35):
    mb = bpy.data.metaballs.new(name)
    mb.resolution = res
    mb.render_resolution = res
    mb.threshold = 0.6
    for (x, y, z, rad, sx, sy, sz) in elems:
        e = mb.elements.new(type="ELLIPSOID")
        e.co = (x, y, z)
        e.radius = rad
        e.size_x, e.size_y, e.size_z = sx, sy, sz
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


def ball(r, loc, scale, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=16, ring_count=8, location=loc)
    o = bpy.context.object
    o.scale = scale
    o.rotation_euler = [math.radians(v) for v in rot]
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


random.seed(7)
STARS = [(random.uniform(-9, 9), random.uniform(-1, 1) * SPAN * 0.85) for _ in range(70)]
BELLY_SPOTS = [(random.uniform(-6, 4), random.uniform(-6, 6)) for _ in range(9)]


def build(r, heading, flap, loop):
    M = r.ids
    parts = []
    # The body and wings: flattened metaballs on a grid inside the outline, a thicker ridge down the middle.
    elems = []
    x = 12.0
    while x >= -11:
        hw = outline_half_width(x)
        y = -hw
        while y <= hw:
            body = max(0.0, 1 - abs(y) / 7.5)
            z = wing_z(x, y, flap)
            elems.append((x, y, z, 2.6, 1.0, 1.0, 0.34 + 0.55 * body))
            y += 2.2
        x -= 2.2
    body = meta(r, "body", elems, M["back"])
    body.data.materials.append(M["belly"])
    body.data.materials.append(M["patch"])
    body.data.materials.append(r.ids[SPECIAL])
    body.data.materials.append(M["edge"])
    body.data.materials.append(M["spot"])
    for p in body.data.polygons:
        cx, cy, cz = p.center
        nz = p.normal.z
        top = cz >= wing_z(cx, cy, flap) - 0.05 and nz > 0
        if not top:
            p.material_index = 1
            if any((cx - sx) ** 2 + (cy - sy) ** 2 < 0.9 for sx, sy in BELLY_SPOTS):
                p.material_index = 5
            continue
        hw = outline_half_width(cx)
        if hw > 0 and abs(cy) > hw - 1.6:
            p.material_index = 4
        elif 2 < cx < 7 and 4 < abs(cy) < 8.5 and abs(cy) > 3.5 + (7 - cx) * 0.5:
            p.material_index = 2                   # the white shoulder patches, a chevron behind the head
        elif any((cx - sx) ** 2 + (cy - sy) ** 2 < 0.55 for sx, sy in STARS):
            p.material_index = 3                   # a star
    parts.append(body)
    # The cephalic fins: two curled lobes either side of the mouth, reaching forward.
    for s in (-1, 1):
        parts.append(ball(r, (14.0, s * 6.2, 0.6), (3.6, 1.2, 1.4), M["cephal"], (0, -10, s * 14)))
    parts.append(ball(r, (13.4, 0, 0.0), (1.4, 4.6, 0.9), M["mouth"]))
    for s in (-1, 1):
        parts.append(ball(r, (11.6, s * 7.4, 1.1), (0.7, 0.6, 0.6), M["eye"]))
    # The thin tail, trailing behind, waving a little with the beat.
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.9, radius2=0.2, depth=17, location=(-18.0, 0, 0.3 + flap * 0.6))
    t = bpy.context.object
    t.rotation_euler = (0, math.radians(90), 0)
    t.data.materials.append(M["tail"])
    parts.append(t)
    root = bpy.data.objects.new("root", None)
    r.scene.collection.objects.link(root)
    for p in parts:
        p.parent = root
    # Loop: nose up (about the wing axis) round to upside down; then the heading, seen from above.
    root.rotation_mode = "XYZ"   # the loop about its wings first, then the heading
    root.rotation_euler = (0, -math.radians(loop), math.radians(heading))
    root.location = (0, 0, r.Z(LIFT))
    bpy.context.view_layer.update()


# The loops face south, toward you, so a breach shows its white belly with the wings spread across the view.
specs = [(h, f, 0) for h in HEADINGS for f in range(FLAPS)] + [(270, 1, lp) for lp in LOOPS]
if os.environ.get("FESH_QUICK"):
    specs = [(0, 0, 0), (0, 2, 0), (90, 1, 0), (270, 1, 60) if False else (270, 1, 90), (270, 1, 180)]
frames, meta_rows = [], []
anchor = None
for n, (h, f, lp) in enumerate(specs):
    r = Renderer(W, H, pitch=55, tones=TONES, special=SPECIAL)
    r.keep_blend = n == 0
    flap = [-0.55, 0.25, 0.85][f]
    build(r, h, flap, lp)
    r.aim((0, 0, LIFT))
    grid = r.render(OUT, heights=True)
    anchor = r.anchor((0, 0, LIFT))
    frames.append((grid, r.last_heights))
    meta_rows.append((h, f, lp))
    print("frame", n, h, f, lp)

r.preview(os.path.join(OUT, "manta-preview.png"), [g for g, _ in frames], special_colours=["#7f8fb0", "#cfd8ee", "#ffffff"], scale=5)

if CS:
    pal = r.colours([g for g, _ in frames])
    lines = [
        "// Generated by tools/blender/manta.py from a 3D model of the Starfall ray: don't edit by hand, change the model and",
        "// re-run the script. Each frame is zlib-compressed bytes in base64: Width x Height palette indices (255 clear, 250-252",
        "// a star on its back in three tones, for the game to colour), then as many heights in rows (its middle at Lift).",
        "// Heading is where it faces (degrees, 0 east, counter-clockwise from above), Flap the step of the wing beat (0 down,",
        "// 2 up), Loop how far it has turned nose-up about its wings (0 flat, 90 nose up, 180 upside down).",
        "namespace Fesh;",
        "",
        "static class MantaArt",
        "{",
        f"    public const int Width = {W}, Height = {H}, AnchorX = {anchor[0]}, AnchorY = {anchor[1]}, Lift = {LIFT}, Flaps = {FLAPS};",
        "    public static readonly string[] Palette = { " + ", ".join(f'"{c}"' for c in pal) + " };",
        "    public static readonly int[] Heading = { " + ", ".join(str(m[0]) for m in meta_rows) + " };",
        "    public static readonly int[] Flap = { " + ", ".join(str(m[1]) for m in meta_rows) + " };",
        "    public static readonly int[] Loop = { " + ", ".join(str(m[2]) for m in meta_rows) + " };",
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
