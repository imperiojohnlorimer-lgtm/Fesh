# Two more tree silhouettes for 1.41, rendered the same way as trees.py: an old twin-trunk oak and a palm that leans hard over
# the beach. They are looks-only variants of the existing oak and palm (the game picks the size band first, then one of these
# by tile hash and island, Light: Soft or Full only), so each has three sizes matching the oak's and palm's ordinary bands.
# The existing sprites in TreeArt.cs are not regenerated: this writes its own file, TreeArt2.cs.
# Run:  blender -b --python tools/blender/trees2.py -- <out_dir> [cs_path]
import os, sys, math, random
here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, "trees.py"), encoding="utf8").read()
exec(compile(src.split("for i, (seed, size) in enumerate(((3, 1.0)")[0], os.path.join(here, "trees.py"), "exec"), globals())


def oak_twin(name, seed, size):
    """An old oak whose trunk has split low into two, each carrying its own crown, one a little higher than the other."""
    random.seed(seed)
    k = size
    W, H = int(54 * k), int(62 * k)
    r = Renderer(W, H, pitch=PITCH, tones={"bark": BARK, "leaf": LEAF, "leaf2": LEAF2}, lit_at=(0.66, 0.0))
    M = r.ids
    trunk = 11 * k
    bases = ((-4.2 * k, 0.0, 1.0), (4.6 * k, 0.8 * k, 0.86))
    for bx, by, h in bases:
        r.cylinder(bx, by, 0, trunk * h + 4, 2.3 * k, 1.5 * k, M["bark"], verts=12)
    for i in range(6):
        ang = i / 6 * 2 * math.pi + random.random()
        r.rod((0, 0, 1.5), (7.5 * k * math.cos(ang), 3.0 * k * math.sin(ang), 0), 0.9 * k, M["bark"], verts=6)
    for bx, by, h in bases:
        for i in range(2):
            ang = (i - 0.5) * 1.0 + random.uniform(-0.2, 0.2)
            tip = (bx + math.sin(ang) * 7 * k, by + random.uniform(-1, 1) * k, trunk * h + 8 * k)
            r.rod((bx, by, trunk * h - 2), tip, 0.9 * k, M["bark"], verts=6)
    clumps = []
    for ci, (bx, by, h) in enumerate(bases):
        cz = trunk * h + 13 * k
        clumps.append((bx, by, cz, 8.6 * (1.0 if ci == 0 else 0.92)))
        for i in range(11):
            ang = random.uniform(0, 2 * math.pi)
            up = random.uniform(-0.45, 0.9)
            d = random.uniform(5.5, 9.0) * math.sqrt(1 - up * up * 0.6)
            clumps.append((bx + math.cos(ang) * d * k, by + math.sin(ang) * d * 0.75 * k, cz + up * 8 * k, random.uniform(4.4, 6.4)))
    for i, (x, y, z, rad) in enumerate(clumps):
        dark = (i % 12 != 0) and (z < trunk * 0.9 + 12 * k or y > 3 or i % 5 == 4)
        blob(r, x, y, z, rad * k, M["leaf2" if dark else "leaf"], sz=0.8, rough=1.6 * k, seed=seed * 10 + i % 4, sub=4, scale=0.7)
    finish(name, r, (0, 0, (trunk + 13 * k) * 0.62))


def palm_lean(name, seed, size):
    """A coconut palm bowed hard over the water: a long curve of trunk, the crown well off to one side."""
    random.seed(seed)
    k = size
    W, H = int(92 * k), int(56 * k)
    r = Renderer(W, H, pitch=PITCH, tones={"bark": PALMBARK, "frond": PALM, "frond2": PALM2, "nut": ("#3a2614", "#5b3e24", "#7a5634", "#94704a")}, lit_at=(0.82, 0.3))
    LEAFLETS = r.banded("leaflets", "X", 2.0, "frond", "frond2", 0.55)
    M = r.ids
    n = 30 * k
    lean = random.choice((-1, 1)) * random.uniform(11, 14) * k
    pts = []
    for i in range(13):
        t = i / 12
        pts.append((lean * t ** 1.7, 0, n * t))
    for i, (p0, p1) in enumerate(zip(pts, pts[1:])):
        rad = (1.8 - 0.5 * i / 12) * k
        r.rod(p0, p1, rad, M["bark"], verts=10)
    top = pts[-1]
    for i in range(4):
        ang = i / 4 * 2 * math.pi + 0.4
        blob(r, top[0] + 1.6 * k * math.cos(ang), 1.0 * math.sin(ang), top[2] - 2.0, 1.4 * k, M["nut"], sub=2)
    nf = 9
    for f in range(nf):
        ang = f / nf * 2 * math.pi + random.uniform(-0.2, 0.2)
        length = random.uniform(20, 25) * k
        dx, dy = math.cos(ang), math.sin(ang) * 0.85
        segs = 9
        prev = (top[0], top[1], top[2] + 1)
        for s in range(1, segs + 1):
            t = s / segs
            x = top[0] + dx * length * t
            y = top[1] + dy * length * t
            z = top[2] + 1 + 7 * k * t - 16 * k * t * t
            w = (2.4 * math.sin(math.pi * min(1.0, t * 1.1)) + 0.9) * k
            mx, my, mz = (prev[0] + x) / 2, (prev[1] + y) / 2, (prev[2] + z) / 2
            seg_len = math.dist((prev[0], prev[1], r.Z(prev[2])), (x, y, r.Z(z))) + 0.8
            pitch_a = -math.atan2(r.Z(z) - r.Z(prev[2]), math.hypot(x - prev[0], y - prev[1]))
            yaw = math.atan2(y - prev[1], x - prev[0])
            for side in (-1, 1):
                bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
                o = bpy.context.object
                o.scale = (seg_len, w, 0.3)
                bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
                for v in o.data.vertices:
                    v.co.y += side * w / 2
                    v.co.z -= abs(v.co.y) * 0.55
                o.location = (mx, my, r.Z(mz))
                o.rotation_euler = (0, pitch_a, yaw)
                o.data.materials.append(LEAFLETS)
            prev = (x, y, z)
    finish(name, r, (top[0] * 0.7, 0, top[2] * 0.6))


# three sizes each, matching the ordinary bands (sizes of the oak and palm sets without the small one)
for i, (seed, size) in enumerate(((31, 0.9), (35, 1.0), (39, 1.12))):
    oak_twin(f"OakTwin{i}", seed, size)
for i, (seed, size) in enumerate(((32, 0.92), (36, 1.0), (40, 1.1))):
    palm_lean(f"PalmLean{i}", seed, size)

if CS:
    lines = [
        "// Generated by tools/blender/trees2.py from 3D models: don't edit by hand, change the models and re-run the script.",
        "// Looks-only variants of the oak and the palm (see trees2.py). Same layout as TreeArt.cs.",
        "namespace Fesh;",
        "",
        "static class TreeArt2",
        "{",
    ]
    for name, grid, hgt, ax, ay, rr in results:
        pal = rr.colours([grid])
        lines.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{c}"' for c in pal) + " };")
        lines.append(f"    public const int {name}AnchorX = {ax}, {name}AnchorY = {ay};")
        lines.append(f"    public static readonly string[] {name} =")
        lines.append("    {")
        lines += [f'        "{row}",' for row in rr.rows(grid, pal)]
        lines.append("    };")
        lines.append(f"    public static readonly string[] {name}Height =")
        lines.append("    {")
        for j, row in enumerate(grid):
            s = "".join("." if c is None else chr(ord("a") + min(25, int(hgt[j][i]) // 2)) for i, c in enumerate(row))
            lines.append(f'        "{s}",')
        lines.append("    };")
    lines.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE", [(n, ax, ay) for n, _, _, ax, ay, _ in results])
