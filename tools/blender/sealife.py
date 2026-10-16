# The sanctuary's big animals seen from above as they swim under the surface: the butanding (whale shark), the dugong
# and the pawikan (green sea turtle). Modelled and rendered to pixel art (see pixelrender.py), FRAMES frames of a swim
# cycle each, facing east (the game mirrors them for west). The game draws them see-through, as shapes under the water.
# Run:  blender -b --python tools/blender/sealife.py -- <out_dir> [cs_path]
#
# Looking straight down, nothing here uses pixelrender's row heights (they'd divide by cos 90); models are in pixels.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
import pixelrender
from pixelrender import Renderer, args

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)
FRAMES = 6
# Seen from above, "from the upper left" on screen is from the west and the north.
pixelrender.LIGHT = np.array([-0.5, 0.45, 0.75]) / np.linalg.norm([-0.5, 0.45, 0.75])


def renderer(w, h, tones):
    r = Renderer(w, h, pitch=89.99, tones=tones, lit_at=(0.72, 0.25))
    return r


def meta_body(r, name, spine, mat, res=0.25):
    """A smooth body from balls along a spine: (x, y, z, radius, width scale, height scale) each."""
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
    # Make it a mesh, so faces can take materials of their own (the whale shark's spots).
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.object
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def flat(r, name, pts, mat, z=0.0, thick=0.5):
    """A flat fin or flipper from a polygon outline (x, y), lying at height z."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, y in pts]
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


def bend(x, head_x, length, ph, amp):
    """How far the body swings sideways at x: nothing at the head, most at the tail, a wave running back."""
    k = max(0.0, (head_x - x) / length)
    return amp * k * k * math.sin(ph - k * 4.0)


results = []

# ---- The butanding (whale shark): a broad flat head with the mouth across its front, a long body tapering to a tall
# tail, two long pectoral fins, dark blue-grey above with rows of pale spots.
tones = {"skin": ("#1d3042", "#2b4660", "#3d6182"), "spot": ("#9db4c4", "#c8dae6", "#e6f0f6"),
         "fin": ("#182a3a", "#243e56", "#345874")}
frames = []
for f in range(FRAMES):
    r = renderer(66, 40, tones)
    r.keep_blend = f == 0       # one .blend per animal (FESH_BLEND_DIR)
    M = r.ids
    ph = 2 * math.pi * f / FRAMES
    L, HX = 54.0, 26.0
    spine = []
    for i in range(30):
        k = i / 29
        x = HX - L * k
        width = (9.5 if k < 0.12 else 9.5 - 7.6 * (k - 0.12) / 0.88) if k < 0.85 else 1.8
        y = bend(x, HX, L, ph, 3.2)
        spine.append((x, y, 0, max(1.0, width), 1.0, 0.55))
    body = meta_body(r, "shark", spine, M["skin"], res=0.35)
    # Spots: faces on the back where a jittered grid lands.
    order = list(M)
    body.data.materials.append(M["spot"])
    random.seed(11)
    pts = [(HX - 6 - c * 3.6 + random.uniform(-0.8, 0.8), (row - 1.5) * 2.6 + random.uniform(-0.6, 0.6))
           for c in range(9) for row in range(4)]
    for p in body.data.polygons:
        cx, cy, cz = p.center
        if cz < 0.4 or cx > HX - 3:
            continue
        sx = cy - bend(cx, HX, L, ph, 3.2)
        if any((cx - px) ** 2 + (sx - py) ** 2 < 0.45 for px, py in pts):
            p.material_index = 1
    # Pectoral fins, the tall tail (seen from above, a blade swinging with the body), and a dorsal fin.
    py0 = bend(HX - 12, HX, L, ph, 3.2)
    for s in (-1, 1):
        flat(r, "pec", [(HX - 10, py0 + s * 6.5), (HX - 15, py0 + s * 14), (HX - 19, py0 + s * 12.5), (HX - 18, py0 + s * 6)], M["fin"])
    tx = HX - L
    ty = bend(tx, HX, L, ph, 3.2)
    ang = math.atan2(bend(tx + 3, HX, L, ph, 3.2) - ty, 3)
    tail = [(0, 0), (-6, 5), (-4.5, 0.6), (-8, -3), (-0.5, -0.8)]
    flat(r, "tail", [(tx + px * math.cos(ang) - py * math.sin(ang), ty + px * math.sin(ang) + py * math.cos(ang)) for px, py in tail], M["fin"], z=0.4)
    dx = HX - 24
    flat(r, "dorsal", [(dx + 2, bend(dx + 2, HX, L, ph, 3.2) + 0.5), (dx - 4, bend(dx - 4, HX, L, ph, 3.2) + 0.5),
                       (dx - 4, bend(dx - 4, HX, L, ph, 3.2) - 0.5), (dx + 2, bend(dx + 2, HX, L, ph, 3.2) - 0.5)], M["fin"], z=3.2, thick=1)
    r.aim((-1, 0, 0))
    frames.append(r.render(OUT))
results.append(("Butanding", frames, r.anchor((0, 0, 0)), r))

# ---- The dugong: a long rounded grey-brown body, a fluked tail like a dolphin's, small flippers and a broad muzzle.
tones = {"skin": ("#55554e", "#706f66", "#8e8d82"), "fluke": ("#4a4a44", "#62615a", "#7c7b72")}
frames = []
for f in range(FRAMES):
    r = renderer(36, 22, tones)
    r.keep_blend = f == 0       # one .blend per animal (FESH_BLEND_DIR)
    M = r.ids
    ph = 2 * math.pi * f / FRAMES
    L, HX = 26.0, 13.0
    spine = []
    for i in range(20):
        k = i / 19
        x = HX - L * k
        width = 3.2 + 2.8 * math.sin(min(1, k * 1.25) * math.pi) * (1 - 0.45 * k) if k > 0.04 else 3.2
        spine.append((x, bend(x, HX, L, ph, 1.6), 0, max(0.9, width), 1.0, 0.75))
    meta_body(r, "dugong", spine, M["skin"], res=0.3)
    tx = HX - L
    ty = bend(tx, HX, L, ph, 1.6)
    flat(r, "fluke", [(tx + 1, ty), (tx - 2, ty + 5.5), (tx - 3.5, ty + 4.5), (tx - 1.5, ty), (tx - 3.5, ty - 4.5), (tx - 2, ty - 5.5)], M["fluke"], z=0.2)
    for s in (-1, 1):
        fy = bend(HX - 6, HX, L, ph, 1.6)
        flat(r, "flip", [(HX - 5, fy + s * 3), (HX - 8, fy + s * 6.5), (HX - 9.5, fy + s * 5.5), (HX - 8, fy + s * 3)], M["fluke"])
    r.aim((-1, 0, 0))
    frames.append(r.render(OUT))
results.append(("Dugong", frames, r.anchor((0, 0, 0)), r))

# ---- The pawikan (green sea turtle): a domed shell with its scutes, the head out in front, and long front flippers
# that sweep like wings (the back ones just steer).
tones = {"shell": ("#33461f", "#4f6a33", "#7a9450"), "scute": ("#26361a", "#3a5028", "#56703a"),
         "skin": ("#4f5e34", "#6e7e44", "#94a462")}
frames = []
for f in range(FRAMES):
    r = renderer(26, 24, tones)
    r.keep_blend = f == 0       # one .blend per animal (FESH_BLEND_DIR)
    M = r.ids
    ph = 2 * math.pi * f / FRAMES
    # The shell: a squashed sphere, its scutes marked by faces in a ring pattern.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1, location=(0, 0, 0))
    sh = bpy.context.object
    sh.scale = (6.2, 5.0, 2.4)
    bpy.ops.object.transform_apply(scale=True)
    bpy.ops.object.shade_smooth()
    sh.data.materials.append(M["shell"])
    sh.data.materials.append(M["scute"])
    for p in sh.data.polygons:
        cx, cy, cz = p.center
        if cz > 0.3 and (abs(cy) < 0.45 and abs(cx) < 4.6 or abs(cx - 1.6) < 0.35 or abs(cx + 1.8) < 0.35):
            p.material_index = 1
    # The head, and the flippers.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=(7.4, 0, 0.3))
    hd = bpy.context.object
    hd.scale = (1.9, 1.4, 1.1)
    hd.data.materials.append(M["skin"])
    bpy.ops.object.shade_smooth()
    sweep = math.sin(ph)
    for s in (-1, 1):
        a0 = math.radians(25 + 40 * sweep)      # the front flipper swept forward then back
        base = (3.0, s * 4.2)
        tip = (base[0] + 7 * math.cos(a0) * -0.4 + 2, base[1] + s * 7 * math.sin(a0 + 0.6))
        flat(r, "front", [(base[0] + 1.5, base[1]), (tip[0] + 2.2, tip[1]), (tip[0] - 0.4, tip[1] + s * 0.4), (base[0] - 3.5, base[1] + s * 0.8)], M["skin"], z=0.2, thick=0.6)
        bk = math.radians(10 * math.sin(ph + 1))
        flat(r, "back", [(-4, s * 3.2), (-6.5, s * (6 + bk)), (-8.6, s * (4.6 + bk)), (-6.4, s * 2.2)], M["skin"], z=0.1, thick=0.5)
    r.aim((0, 0, 0))
    frames.append(r.render(OUT))
results.append(("Pawikan", frames, r.anchor((0, 0, 0)), r))

for name, frames, _, rr in results:
    rr.preview(os.path.join(OUT, f"{name}-preview.png"), frames, back=(40, 110, 140), scale=6)

if CS:
    lines = [
        "// Generated by tools/blender/sealife.py from 3D models of the sanctuary's animals, seen from above: don't edit by",
        "// hand, change the models and re-run the script. Each animal has Frames frames of its swim (palette letters, '.'",
        "// clear), facing east, with its anchor at the middle of its body.",
        "namespace Fesh;",
        "",
        "static class SeaLifeArt",
        f"{{",
        f"    public const int Frames = {FRAMES};",
    ]
    for name, frames, (ax, ay), rr in results:
        pal = rr.colours(frames)
        lines.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{c}"' for c in pal) + " };")
        lines.append(f"    public const int {name}AnchorX = {ax}, {name}AnchorY = {ay};")
        lines.append(f"    public static readonly string[][] {name} =")
        lines.append("    {")
        for g in frames:
            lines.append("        new[]")
            lines.append("        {")
            lines += [f'            "{row}",' for row in rr.rows(g, pal)]
            lines.append("        },")
        lines.append("    };")
    lines.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", [(n, anc) for n, _, anc, _ in results])
