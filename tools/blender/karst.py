# Luntian's limestone karst towers (like El Nido's), modelled and rendered to pixel art (see pixelrender.py).
# Run:  blender -b --python tools/blender/karst.py -- <out_dir> [cs_path]
#
# A fluted limestone tower: a rounded top under a cap of forest, steep sides that bulge a little and flare at the foot,
# vertical grooves worn by rain, ledges with shrubs on them, vines down the shaded side, and fallen blocks at its foot.
# Two towers (Luntian has a tall one and a short one), each its own shape.
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from pixelrender import Renderer, args

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

TONES = {
    "stone":  ("#8f8c7a", "#b8b49e", "#dcd8c4"),
    "groove": ("#6c6e62", "#8f8d7c", "#a9a691"),
    "ledge":  ("#7a7a6c", "#9c9986", "#c4c0aa"),
    "leaf":   ("#2a5428", "#427e38", "#68a64e"),
    "leaf2":  ("#25482a", "#386c36", "#549046"),
    "vine":   ("#2c4e2a", "#3f6e35", "#4f8a3c"),
    "block":  ("#7a786a", "#a8a592", "#d0ccb6"),
}
PITCH = 18


def tower(name, height, seed):
    random.seed(seed)
    r = Renderer(40, int(height * 1.2) + 18, pitch=PITCH, tones=TONES, lit_at=(0.6, -0.05))
    M = r.ids
    H = float(height)
    NT, NA = 70, 160
    flutes = random.randint(9, 12)
    phase = random.random() * 6.28
    ledges = [random.uniform(0.25, 0.8) for _ in range(3)]

    def radius(h, th):
        k = 1 - h / H                                    # 0 at the top, 1 at the foot
        if k < 0.16:
            base = 8.0 * math.sqrt(max(0.0, 1 - ((0.16 - k) / 0.16) ** 2))
        else:
            base = 8.0 + 2.4 * math.sin(k * math.pi * 0.9) + (max(0, k - 0.85) * 26)
        flute = 0.55 * math.cos(flutes * th + phase + 0.6 * math.sin(h * 0.3)) * min(1, k * 5)
        lumps = 0.35 * math.sin(th * 3 + h * 0.21 + seed) + 0.25 * math.sin(th * 7 - h * 0.13)
        return max(0.3, base + flute + lumps)

    bm = bmesh.new()
    rings = []
    for i in range(NT + 1):
        h = H * (1 - i / NT)
        rings.append([bm.verts.new((radius(h, th) * math.sin(th), -radius(h, th) * math.cos(th), r.Z(h)))
                      for th in (2 * math.pi * j / NA - math.pi for j in range(NA))])
    top = bm.verts.new((0, 0, r.Z(H)))
    faces = []
    for j in range(NA):
        faces.append((bm.faces.new((rings[0][(j + 1) % NA], rings[0][j], top)), "stone"))
    for i in range(NT):
        h = H * (1 - (i + 0.5) / NT)
        for j in range(NA):
            th = 2 * math.pi * (j + 0.5) / NA - math.pi
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % NA], rings[i + 1][(j + 1) % NA], rings[i + 1][j]))
            k = 1 - h / H
            groove = math.cos(flutes * th + phase + 0.6 * math.sin(h * 0.3)) < -0.55 and k > 0.15
            ledge = any(abs(k - l) < 0.025 for l in ledges)
            vine = th > 0.5 and th < 1.6 and k > 0.12 and k < 0.6 and math.sin(th * 23 + seed) > 0.5 and (h * 0.7 + th * 9) % 3 < 2.2
            faces.append((f, "vine" if vine else "ledge" if ledge else "groove" if groove else "stone"))
    me = bpy.data.meshes.new(name)
    order = list(TONES)
    for f, m in faces:
        f.material_index = order.index(m)
        f.smooth = True
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    r.scene.collection.objects.link(o)
    for k in order:
        o.data.materials.append(M[k])

    def blob(x, y, z, s, mat):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=s, location=(x, y, r.Z(z)))
        b = bpy.context.object
        b.scale = (1, 1, 0.8)
        b.data.materials.append(M[mat])
        bpy.ops.object.shade_smooth()

    # The forest cap, in clumps, spilling a little over the top.
    for _ in range(14):
        ang = random.random() * 6.28
        d = random.random() * 6.0
        blob(d * math.sin(ang), -d * math.cos(ang), H - 1.5 + random.random() * 2.5 - d * 0.25, 2.4 + random.random() * 1.8,
             "leaf" if random.random() < 0.7 else "leaf2")
    # Shrubs on the ledges, mostly on the lit and front faces.
    for l in ledges:
        h = H * (1 - l)
        for _ in range(3):
            th = random.uniform(-1.6, 0.6)
            rr = radius(h, th) + 0.6
            blob(rr * math.sin(th), -rr * math.cos(th), h + 0.6, 1.0 + random.random() * 0.7, "leaf")
    # Fallen blocks and a fern at the foot.
    for x, y, s in ((-12.5, -3, 2.2), (11.5, -2, 1.6), (-9, -7, 1.3)):
        bpy.ops.mesh.primitive_cube_add(size=s * 2, location=(x, y, r.Z(s * 0.8)), rotation=(0.2, 0.1, random.random()))
        bpy.context.object.data.materials.append(M["block"])
    blob(-8, -8, 0.8, 1.3, "leaf")

    r.aim((0, 0, H * 0.5))
    grid = r.render(OUT)
    ax, ay = r.anchor((0, 0, 0))
    r.preview(os.path.join(OUT, f"{name}-preview.png"), [grid], back=(120, 160, 100))
    return name, grid, ax, ay, r


results = [tower("Tall", 54, 3), tower("Short", 42, 8)]

if CS:
    lines = [
        "// Generated by tools/blender/karst.py from 3D models of Luntian's karst towers: don't edit by hand, change the",
        "// models and re-run the script. Each tower is a grid of palette letters ('.' clear) with its anchor at the middle",
        "// of its foot on the ground.",
        "namespace Fesh;",
        "",
        "static class KarstArt",
        "{",
    ]
    for name, grid, ax, ay, rr in results:
        pal = rr.colours([grid])
        lines.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{c}"' for c in pal) + " };")
        lines.append(f"    public const int {name}AnchorX = {ax}, {name}AnchorY = {ay};")
        lines.append(f"    public static readonly string[] {name} =")
        lines.append("    {")
        lines += [f'        "{row}",' for row in rr.rows(grid, pal)]
        lines.append("    };")
    lines.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", [(n, ax, ay) for n, _, ax, ay, _ in results])
