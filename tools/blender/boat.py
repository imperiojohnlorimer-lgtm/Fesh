# The banca (the player's sailboat), modelled and rendered to pixel art (see pixelrender.py for the method), for BoatArt.cs.
# Run:  blender -b --python tools/blender/boat.py -- <out_dir> [cs_path]
#
# A Philippine banca / paraw: a slender hull with a raised, carved prow and stern, plank lines, a painted teal trim, two
# bamboo floats on lashed outrigger booms, thwarts, a mast step. The rig (mast, sail, pennant), the fisher, the wake and the
# bob stay in code (Boating.cs); this is the hull and the floats, in two layers per facing so the fisher sits between them:
# BACK is everything behind the fisher (the far half, the inside, the floor, the booms), FRONT is the near wall, the rim
# and the near float, drawn after. The model is cut at the middle plane (side views: the near and far halves; end-on views:
# the near and far ends). Facings: right (the game mirrors it for left), up (heading away, seen from astern), down (bow-on).
# The same model also gives the boatyard's construction stages (BoatArt.Stage*, on a cradle on the sand, no water): the
# keel and ribs, the planking, the outriggers, the mast and rigging, the sail, each with a few frames to play.
#
# Coordinates: x along the hull (+x the bow), y across (the camera looks along +y, so -y is the near side), z in screen
# rows. The waterline is z = 0 (the water plane hides everything below it in the in-water views).
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector
from pixelrender import Renderer, args

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

PITCH = 56
TONES = {
    "hullA":  ("#7c4f30", "#bd8750", "#d8a76d"),
    "hullB":  ("#6c4429", "#a87244", "#c79660"),
    "trim":   ("#2f6c69", "#4e9691", "#7cc4b8"),
    "rim":    ("#a87a4a", "#dbb27a", "#f3d9a2"),
    "inner":  ("#3f2a1c", "#68452f", "#8a603f"),
    "floorA": ("#684429", "#936440", "#b58558"),
    "floorB": ("#5a3a23", "#815736", "#a47649"),
    "thwart": ("#a8794a", "#d6a369", "#efc88c"),
    "bamA":   ("#8a7440", "#c4ad6a", "#e6d392"),
    "bamB":   ("#6f5b2e", "#9f8950", "#c4ad6a"),
    "boom":   ("#8c6a3a", "#c09858", "#e0c180"),
    "rope":   ("#4a3524", "#6e5034", "#8e6e48"),
    "carve":  ("#c49a58", "#ecc986", "#fbe3ae"),
    "keel":   ("#5a3a20", "#7e5430", "#a07442"),
    "rib":    ("#8a5e34", "#b98450", "#dba974"),
    "cloth":  ("#cfc6a4", "#f4edce", "#ffffff"),
    "ground": ("#8a7448", "#b09a62", "#cdb87c"),
}
LEN = 13.0          # half the hull's length
BEAM = 4.5          # half the beam amidships
FLOAT_Y = 10.5       # how far out the floats lie
RING = [0.0, 0.45, 0.8, 0.97, 1.0]   # the outer profile's rows, as a share of the half beam (and below)


def width(x):
    u = abs(x) / LEN
    return BEAM * max(0.0, 1 - u ** 2.2) ** 0.8 + 0.28


def sheer(x):
    u = abs(x) / LEN
    s = 3.1 + 3.0 * u ** 3 + (0.7 * u ** 3 if x > 0 else 0)
    if u > 0.8:
        s += 2.4 * ((u - 0.8) / 0.2) ** 2          # the prow and the stern post curl up
    return s


def outer_ring(x):
    w, s = width(x), sheer(x)
    return [(0.0, -2.3), (0.45 * w, -1.7), (0.8 * w, -0.4), (0.97 * w, 0.5 * s), (w, s)]


def inner_ring(x):
    w, s = width(x), sheer(x)
    wi = max(0.05, w - 0.9)
    u = abs(x) / LEN
    floor = 0.5 + max(0.0, (u - 0.72) / 0.28) ** 1.2 * (s - 1.6)       # decked over toward the ends
    return [(0.0, floor), (0.8 * wi, floor + 0.2), (wi, s - 0.25)]


class Builder:
    def __init__(self, r, rot=0.0, near=None, layer="full", zoff=0.0):
        self.r, self.rot, self.layer, self.zoff = r, rot, layer, zoff
        self.near = near          # how to tell the near part: ("y", -1) for side views, ("x", +1/-1) for end-on views
        self.M = r.ids
        self.cache = {}
        self.parts = []
        self.root = bpy.data.objects.new("root", None)
        r.scene.collection.objects.link(self.root)
        self.root.rotation_euler = (0, 0, rot)
        self.planks = r.banded("planks", "Z", r.Z(1.25), "hullA", "hullB", 0.5)
        self.floorb = r.banded("floorb", "Y", 1.9, "floorA", "floorB", 0.5)
        self.bamboo = r.banded("bamboo", "X", 6.5, "bamA", "bamB", 0.84)

    # Is something at model (x, y) in the front layer (the near side)?
    def front(self, x, y):
        if self.near is None:
            return False
        axis, sign = self.near
        return (y if axis == "y" else x) * sign > 0

    def add(self, obj, front):
        obj.parent = self.root
        if self.layer == "full" or (self.layer == "front") == bool(front):
            self.parts.append(obj)
        else:
            bpy.data.objects.remove(obj, do_unlink=True)
        return obj

    def vert(self, bm, p):
        k = tuple(round(c, 4) for c in p)
        if k not in self.cache:
            self.cache[k] = bm.verts.new((p[0], p[1], self.r.Z(p[2] + self.zoff)))
        return self.cache[k]

    def mesh(self, name, faces, mats, front):
        bm = bmesh.new()
        self.cache = {}
        for pts, mi in faces:
            vs = [self.vert(bm, p) for p in pts]
            if len(set(vs)) < 3:
                continue
            try:
                f = bm.faces.new(vs)
            except ValueError:
                continue
            f.material_index = mi
            f.smooth = True
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        self.r.scene.collection.objects.link(o)
        for m in mats:
            o.data.materials.append(m)
        return self.add(o, front)

    def box(self, x0, x1, y0, y1, z0, z1, mat, front):
        o = self.r.box((x0 + x1) / 2, (y0 + y1) / 2, z0 + self.zoff, x1 - x0, y1 - y0, z1 - z0, mat)
        return self.add(o, front)

    def rod(self, p, q, rad, mat, front, verts=8):
        o = self.r.rod((p[0], p[1], p[2] + self.zoff), (q[0], q[1], q[2] + self.zoff), rad, mat, verts)
        return self.add(o, front)

    def ball(self, p, scale, mat, front):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=14, ring_count=8, location=(p[0], p[1], self.r.Z(p[2] + self.zoff)))
        o = bpy.context.object
        o.scale = scale
        o.data.materials.clear()
        o.data.materials.append(mat)
        bpy.ops.object.shade_smooth()
        return self.add(o, front)

    # ---- the hull: outer planking (zmax limits how high the planks reach, for the stages), the rim, the inside
    def hull(self, zmax=99.0, inside=True, xs=None):
        r, M = self.r, self.M
        N = 28
        xs = xs or [-LEN + 2 * LEN * i / N for i in range(N + 1)]
        for side in (-1, 1):
            outer, rim, inner = [], [], []
            for i in range(len(xs) - 1):
                x0, x1 = xs[i], xs[i + 1]
                o0, o1, n0, n1 = outer_ring(x0), outer_ring(x1), inner_ring(x0), inner_ring(x1)
                fr = self.front((x0 + x1) / 2, side * 1.0)
                for j in range(len(o0) - 1):
                    if o0[j + 1][1] > zmax and o1[j + 1][1] > zmax:
                        continue
                    top = j == len(o0) - 2
                    q = [(x0, side * o0[j][0], o0[j][1]), (x0, side * o0[j + 1][0], o0[j + 1][1]),
                         (x1, side * o1[j + 1][0], o1[j + 1][1]), (x1, side * o1[j][0], o1[j][1])]
                    outer.append((q, 1 if top and zmax > 90 else 0))
                if zmax > 90:
                    rim.append(([(x0, side * o0[-1][0], o0[-1][1]), (x0, side * n0[-1][0], n0[-1][1]),
                                 (x1, side * n1[-1][0], n1[-1][1]), (x1, side * o1[-1][0], o1[-1][1])], 0))
                    for j in range(len(n0) - 1):
                        inner.append(([(x0, side * n0[j][0], n0[j][1]), (x0, side * n0[j + 1][0], n0[j + 1][1]),
                                       (x1, side * n1[j + 1][0], n1[j + 1][1]), (x1, side * n1[j][0], n1[j][1])], 0))
            # Faces on the near side (or near end) go to the front layer; the others are split by station.
            self._split(outer, f"outer{side}", [self.planks, M["trim"]], side)
            if zmax > 90:
                self._split(rim, f"rim{side}", [M["rim"]], side)
                if inside:
                    self._split(inner, f"inner{side}", [self.floorb], side, inner=True)

    def _split(self, faces, name, mats, side, inner=False):
        front, back = [], []
        for pts, mi in faces:
            cx = sum(p[0] for p in pts) / 4
            cy = side * 1.0
            (front if (not inner and self.front(cx, cy)) else back).append((pts, mi))
        if front:
            self.mesh(name + "f", front, mats, True)
        if back:
            self.mesh(name + "b", back, mats, False)

    # ---- the fittings of the finished boat
    def fittings(self, mast_step=True):
        M = self.M
        # Two thwarts across the hull, a mast step, the carved prow and stern posts.
        for x, w in ((-4.0, 1.4), (4.6, 1.4)):
            wi = width(x) - 0.9
            self.box(x - w / 2, x + w / 2, -wi, wi, 1.2, 2.0, M["thwart"], False)
        if mast_step:
            self.box(1.2, 3.2, -0.9, 0.9, 0.5, 2.0, M["thwart"], False)
        self.rod((LEN - 0.4, 0, sheer(LEN - 0.4) - 0.5), (LEN + 1.2, 0, sheer(LEN) + 1.8), 0.55, M["carve"], self.front(LEN, 0))
        self.ball((LEN + 1.3, 0, sheer(LEN) + 1.9), (0.6, 0.5, 0.6), M["carve"], self.front(LEN, 0))
        self.rod((-LEN + 0.4, 0, sheer(-LEN + 0.4) - 0.5), (-LEN - 1.0, 0, sheer(-LEN) + 1.5), 0.5, M["carve"], self.front(-LEN, 0))

    # ---- the outriggers: two booms a side lashed to a bamboo float each, and the floats
    def outriggers(self, booms=True, floats=True):
        z0 = 0.0
        M = self.M
        for side in (-1, 1):
            if booms:
                for xb in (-6.0, 5.5):
                    w = width(xb)
                    # Inside the hull (back layer) and out to the float (the near side's goes to the front layer).
                    self.rod((xb, side * (w - 0.6), 2.6 + z0), (xb, side * FLOAT_Y, 1.1 + z0), 0.6, M["boom"], self.front(xb, side))
                    self.rod((xb, -(w - 0.6), 2.6 + z0), (xb, side * (w - 0.6), 2.6 + z0), 0.6, M["boom"], False) if side == 1 else None
                    self.box(xb - 0.9, xb + 0.9, side * FLOAT_Y - 0.9, side * FLOAT_Y + 0.9, 0.5 + z0, 2.4 + z0, M["rope"], self.front(xb, side))
            if floats:
                for xa, xb_ in ((-LEN + 0.5, 0.0), (0.0, LEN - 0.5)):
                    self.rod((xa, side * FLOAT_Y, 0.6 + z0), (xb_, side * FLOAT_Y, 0.6 + z0), 1.15, self.bamboo, self.front((xa + xb_) / 2, side))
                self.ball((LEN - 0.4, side * FLOAT_Y, 0.6 + z0), (1.6, 1.15, 1.15), M["bamA"], self.front(LEN, side))
                self.ball((-LEN + 0.4, side * FLOAT_Y, 0.6 + z0), (1.6, 1.15, 1.15), M["bamA"], self.front(-LEN, side))


def new_renderer(w, h, zoom=1.0):
    return Renderer(w, h, pitch=PITCH, tones=TONES, lit_at=(0.66, 0.02), zoom=zoom)


def finish_layer(r, b, name, grids, anchors):
    r.aim((0, 0, 2))
    grid = r.render(OUT)
    grids.append((name, grid, r.anchor((0, 0, 0))))


def render_boat(heading, layer):
    r = new_renderer(46, 44)
    r.water()
    rot = {"right": 0.0, "up": math.pi / 2, "down": -math.pi / 2}[heading]
    near = ("y", -1) if heading == "right" else ("x", -1 if heading == "up" else 1)
    b = Builder(r, rot, near, layer)
    b.hull()
    b.fittings()
    b.outriggers()
    r.aim((0, 0, 2))
    grid = r.render(OUT)
    return r, grid, r.anchor((0, 0, 0))


# ---- the construction stages: the boat on a cradle on the sand (no water)
def render_stage(stage, k):
    r = new_renderer(50, 46)
    z0 = 2.6                                   # the keel rides on the cradle, which rides on the ground
    ground = Builder(r, 0.0, None, "full")
    M = r.ids
    # The cradle: three cross-timbers and two rails along the ground.
    for x in (-8.0, 0.0, 8.0):
        ground.box(x - 1.0, x + 1.0, -5.5, 5.5, 0, 1.6, M["keel"], False)
    for y in (-3.6, 3.6):
        ground.box(-15, 15, y - 0.6, y + 0.6, 1.6, 2.4, M["rope"], False)
    b = Builder(r, 0.0, None, "full", zoff=z0)
    if stage >= 1:
        # The keel along the bottom, the stem and stern posts, and the ribs (frames): k = 0 keel only, 1 half, 2 all.
        keel = [(x, 0, outer_ring(x)[0][1]) for x in [-LEN + 2 * LEN * i / 20 for i in range(21)]]
        for p, q in zip(keel, keel[1:]):
            b.rod(p, q, 0.65, M["keel"], False)
        b.rod((LEN, 0, outer_ring(LEN)[0][1]), (LEN + 0.5, 0, sheer(LEN) + 0.6), 0.6, M["keel"], False)
        b.rod((-LEN, 0, outer_ring(-LEN)[0][1]), (-LEN - 0.3, 0, sheer(-LEN) + 0.4), 0.6, M["keel"], False)
        stations = [-10.0, -7.0, -4.0, -1.0, 2.0, 5.0, 8.0, 10.5]
        show = {0: 0, 1: 4, 2: 8}[min(k, 2)] if stage == 1 else (8 if stage == 2 and k < 2 else 0)
        for x in stations[:show]:
            for side in (-1, 1):
                pts = [(x, side * y, z) for y, z in outer_ring(x)]
                for p, q in zip(pts, pts[1:]):
                    b.rod(p, q, 0.6, M["rib"], False)
        if show >= 4:
            # Stringers along the ribs: the fore-and-aft battens that make the frame read as a hull.
            xs_ = stations[:show]
            for side in (-1, 1):
                for ri in (2, 4):
                    ps = [(x, side * outer_ring(x)[ri][0], outer_ring(x)[ri][1]) for x in xs_]
                    for p, q in zip(ps, ps[1:]):
                        b.rod(p, q, 0.45, M["boom"], False)
    if stage >= 2:
        frac = {0: 0.34, 1: 0.67, 2: 1.0}[min(k, 2)] if stage == 2 else 1.0
        b.hull(zmax=(-2.3 + 8.0 * frac) if frac < 1 else 99.0, inside=frac >= 1)
        if frac >= 1:
            b.fittings()
    if stage >= 3:
        b.outriggers(booms=True, floats=(stage > 3 or k >= 1))
    if stage >= 4:
        top = 18.0
        b.rod((2.2, 0, 1.0), (2.2, 0, top), 0.55, M["boom"], False)
        if stage > 4 or k >= 1:
            b.rod((2.2, 0, top), (LEN + 0.5, 0, sheer(LEN) + 0.5), 0.2, M["rope"], False, verts=4)
            b.rod((2.2, 0, top), (-LEN, 0, sheer(-LEN) + 0.5), 0.2, M["rope"], False, verts=4)
            b.rod((2.2, 0, 3.5), (11.5, 0, 4.6), 0.45, M["boom"], False)
    if stage >= 5:
        frac = {0: 0.55, 1: 1.0}[min(k, 1)] if stage == 5 else 1.0
        bm_ = bmesh.new()
        pts = [(2.7, 0.2, 3.9 + z0), (2.7, 0.2, 3.9 + z0 + (17.0 - 3.9) * frac), (2.7 + 8.6 * frac, 0.2, 4.6 + z0)]
        vs = [bm_.verts.new((x, y, r.Z(z))) for x, y, z in pts]
        bm_.faces.new(vs)
        me = bpy.data.meshes.new("sail")
        bm_.to_mesh(me)
        bm_.free()
        so = bpy.data.objects.new("sail", me)
        r.scene.collection.objects.link(so)
        so.data.materials.append(M["cloth"])
        so.modifiers.new("t", "SOLIDIFY").thickness = 0.5
    r.aim((0, 0, 7))
    grid = r.render(OUT)
    return r, grid, r.anchor((0, 0, 0))


# ---- run
sets = []
if os.environ.get("FESH_QUICK"):
    jobs = [(h, l) for h in ("right", "up", "down") for l in ("back", "front")]
else:
    jobs = [(h, l) for h in ("right", "up", "down") for l in ("back", "front")]
results = []
rr = None
for heading, layer in jobs:
    rr, grid, anchor = render_boat(heading, layer)
    results.append((f"{heading}_{layer}", grid, anchor, rr))
    print("boat", heading, layer, anchor)

stage_jobs = [(0, 0), (1, 0), (1, 1), (1, 2), (2, 0), (2, 1), (2, 2), (3, 0), (3, 1), (4, 0), (4, 1), (5, 0), (5, 1)]
if os.environ.get("FESH_QUICK"):
    stage_jobs = [(1, 2), (2, 1), (5, 1)]
for stage, k in stage_jobs:
    rr, grid, anchor = render_stage(stage, k)
    results.append((f"stage{stage}_{k}", grid, anchor, rr))
    print("stage", stage, k, anchor)

comp = []
for h in ("right", "up", "down"):
    bk = next((g for n, g, _, _ in results if n == f"{h}_back"), None)
    fr = next((g for n, g, _, _ in results if n == f"{h}_front"), None)
    if bk and fr:
        comp.append([[fr[j][i] or bk[j][i] for i in range(len(bk[0]))] for j in range(len(bk))])
rr.preview(os.path.join(OUT, "boat-preview.png"), comp + [g for _, g, _, _ in results], scale=8)

if CS:
    allgrids = [g for _, g, _, _ in results]
    pal = rr.colours(allgrids)
    lines = [
        "// Generated by tools/blender/boat.py from a 3D model of the banca: don't edit by hand, change the model and re-run the",
        "// script. Hull + floats only (the rig, the fisher and the wake are drawn in code): Right/Up/Down each in a Back layer",
        "// (behind the fisher) and a Front layer (the near wall, rim and float, after). Stage<n>_<k> are the boatyard's",
        "// construction frames on their cradle. Each grid is rows of palette letters ('.' clear); the anchor is the boat's",
        "// middle at the waterline (or on the ground for the stages).",
        "namespace Fesh;",
        "",
        "static class BoatArt",
        "{",
        "    public static readonly string[] Palette = { " + ", ".join(f'"{c}"' for c in pal) + " };",
    ]
    for name, grid, anchor, _ in results:
        cname = "".join(p.capitalize() for p in name.split("_"))
        rows = rr.rows(grid, pal)
        lines.append(f"    public const int {cname}X = {anchor[0]}, {cname}Y = {anchor[1]};")
        lines.append(f"    public static readonly string[] {cname} =")
        lines.append("    {")
        for row in rows:
            lines.append(f'        "{row}",')
        lines.append("    };")
    lines.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
print("DONE")
