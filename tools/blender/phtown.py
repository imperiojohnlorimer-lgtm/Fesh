# The Philippines map's town buildings and props (1.48), rendered to pixel art from the Feshtry2 prototype's glTF models
# (C:\Users\growk\Desktop\Feshtry2\assets\models): houses, a church, a market shed, and the small things of a fishing town.
# Run:  blender -b --python tools/blender/phtown.py -- <out_dir> [cs_path] [models_dir]
#
# Unlike the other scripts here, which model in Blender and colour by material, these models arrive textured: an unlit
# pass gives each subpixel its texture colour, a normal pass lights it, and each SS x SS block becomes one game pixel
# (the commonest colour among its subpixels, lit in three tones from the upper left like the rest of the game's art).
# Each sprite's colours are then reduced to a small palette. The models are in metres; PPM game pixels to a metre.
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from mathutils import Matrix, Vector
from pixelrender import Renderer, args, LIGHT

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
MODELS = a[2] if len(a) > 2 else r"C:\Users\growk\Desktop\Feshtry2\assets\models"
os.makedirs(OUT, exist_ok=True)

PITCH = 16
PPM = 9.0                  # game pixels a metre: a 1.9 m fisher comes out about 17 px, like the game's people
SS = 8
KEYS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
MAXCOL = 44

# (name in the game, model file, turn about the vertical in degrees, extra size)
SPRITES = [
    ("Church", "church", 0, 0.82),
    ("Market", "market_shed", 0, 1.1),
    ("House0", "bahay_0", 0, 1.0),
    ("House1", "bahay_1", 0, 1.0),
    ("House2", "bahay_2", 0, 1.0),
    ("House3", "bahay_3", 0, 1.0),
    ("House4", "bahay_4", 0, 1.0),
    ("House5", "bahay_5", 0, 1.0),
    ("House6", "bahay_6", 0, 1.0),
    ("House7", "bahay_7", 0, 1.0),
    ("Court", "court", 0, 1.0),
    ("Flagpole", "flagpole", 0, 1.0),
    ("Pump", "water_pump", 0, 1.0),
    ("FishTable", "fish_table", 0, 1.0),
    ("TuyoRack", "tuyo_rack", 0, 1.0),
    ("NetRack", "net_rack", 0, 1.0),
    ("Laundry0", "laundry_0", 0, 1.0),
    ("Laundry1", "laundry_1", 0, 1.0),
    ("Garden0", "garden_0", 0, 1.0),
    ("Garden1", "garden_1", 0, 1.0),
    ("SmallBoat", "small_boat", 0, 1.0),
    ("Banana0", "banana_0", 0, 1.0),
    ("Banana1", "banana_1", 0, 1.0),
    ("Dryer", "fish_dryer", 0, 1.0),
    ("Goods", "yard_goods", 0, 1.0),
    ("Bench", "bench", 0, 1.0),
]
only = os.environ.get("FESH_ONLY")
if only:
    SPRITES = [s for s in SPRITES if s[0] in only.split(",")]


def srgb(c):
    c = max(0.0, min(1.0, c))
    return 12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def import_model(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def place(objs, turn, size, stretch):
    """Scales the model to game pixels (heights stretched to come out in rows), turns it, and sets it on the ground with
    the middle of its front edge at the origin. Returns its width and depth on the ground, in game pixels."""
    roots = [o for o in objs if o.parent is None]
    m = Matrix.Diagonal((PPM * size, PPM * size, PPM * size * stretch, 1)) @ Matrix.Rotation(math.radians(turn), 4, "Z")
    for o in roots:
        o.matrix_world = m @ o.matrix_world
    bpy.context.view_layer.update()
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    for o in objs:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector((min(lo[i], w[i]) for i in range(3)))
            hi = Vector((max(hi[i], w[i]) for i in range(3)))
    shift = Matrix.Translation((-(lo.x + hi.x) / 2, -lo.y, 0))
    for o in roots:
        o.matrix_world = shift @ o.matrix_world
    bpy.context.view_layer.update()
    return hi.x - lo.x, hi.y - lo.y, max(hi.z, 1.0)


# The Feshtry2 textures are made for a lit 3D scene: dark and greyish on their own. Each kind of surface is pulled to the
# colour the game's own buildings use (BuildingArt: golden thatch, warm bamboo, honey wood), keeping the texture's detail
# (its light and dark) by scaling it so its average lands on that colour. Anything not listed is only brightened a little.
TARGETS = [
    ("thatch", "#a9824c"), ("wall_bamboo", "#c09f72"), ("wall_sawali", "#c9b08a"), ("bamboo", "#c2a87c"),
    ("planks", "#94704f"), ("iron_roof", "#96928a"), ("wall_church", "#e6dfd0"), ("capiz", "#e2dac4"),
    ("concrete", "#c8c2b6"), ("rockb77959", "#9e624a"), ("rock", "#b2a994"), ("path", "#c2aa82"),
]


def hexf(h):
    return np.array([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)])


def mean_colour(img):
    a = np.array(img.pixels[:], dtype=np.float32).reshape(-1, img.channels)
    if img.channels >= 4:
        a = a[a[:, 3] > 0.5]
    return a[:, :3].mean(0) if len(a) else np.array([0.5, 0.5, 0.5])


def gain_for(name, mean):
    for key, hx in TARGETS:
        if key in name:
            # Lighten to the target's brightness, but move only part of the way to its hue, so the material keeps its own
            # natural colour (all the way looked painted and unreal).
            t = hexf(hx)
            m = np.maximum(mean, 0.03)
            lg = t.mean() / m.mean()
            hue = (t / t.mean()) / (m / m.mean())
            return np.clip(lg * hue ** 0.85, 0.6, 3.0)
    return np.full(3, 1.12)


def unlit(mat):
    """The material's colour with no light: its base colour texture, or its base colour, as sRGB numbers."""
    m = bpy.data.materials.new("unlit_" + mat.name)
    m.use_nodes = True
    nt = m.node_tree
    n = nt.nodes
    n.remove(n["Principled BSDF"])
    em = n.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], n["Material Output"].inputs[0])
    col = (0.7, 0.7, 0.7)
    tex = None
    if mat.use_nodes:
        bsdf = next((x for x in mat.node_tree.nodes if x.type == "BSDF_PRINCIPLED"), None)
        if bsdf:
            inp = bsdf.inputs["Base Color"]
            if inp.is_linked:
                src = inp.links[0].from_node
                # (through a mix with vertex colour or a factor: find the image behind it)
                stack = [src]
                while stack and tex is None:
                    s = stack.pop()
                    if s.type == "TEX_IMAGE" and s.image:
                        tex = s.image
                    else:
                        stack += [l.from_node for i in s.inputs for l in i.links]
            v = inp.default_value
            col = (srgb(v[0]), srgb(v[1]), srgb(v[2]))
    if tex is not None:
        tex.colorspace_settings.name = "Non-Color"
        g = gain_for(mat.name, mean_colour(tex))
        ti = n.new("ShaderNodeTexImage")
        ti.image = tex
        uv = n.new("ShaderNodeUVMap")
        nt.links.new(uv.outputs[0], ti.inputs[0])
        mul = n.new("ShaderNodeVectorMath")
        mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = tuple(float(v) for v in g)
        nt.links.new(ti.outputs[0], mul.inputs[0])
        nt.links.new(mul.outputs[0], em.inputs[0])
    else:
        # Plain colours: brighter and a touch richer (a dark trim stays dark).
        c = np.array(col)
        lum = c.mean()
        c = 0.08 + 0.92 * np.clip(lum + (c - lum) * 1.05, 0, 1) ** 0.9   # (a near-black trim lifted a little)
        em.inputs[0].default_value = (*c, 1)
    return m


def swap_all(objs, make):
    saved = {}
    for o in objs:
        if o.type != "MESH":
            continue
        saved[o] = list(o.data.materials)
        for i, mt in enumerate(saved[o]):
            o.data.materials[i] = make(mt)
        if not saved[o]:
            o.data.materials.append(make(None))
    return saved


def restore(saved):
    # By slot, never clear() and append: clearing resets every face to the first slot.
    for o, mats in saved.items():
        for i, mt in enumerate(mats):
            o.data.materials[i] = mt


def kmeans(cols, k, iters=14):
    cols = np.asarray(cols, dtype=np.float32)
    uniq = np.unique(cols, axis=0)
    if len(uniq) <= k:
        return uniq
    rng = np.random.default_rng(7)
    # Start from the commonest colours spread out (farthest-point), so small bright details keep a colour of their own.
    centres = [uniq[rng.integers(len(uniq))]]
    for _ in range(k - 1):
        d = np.min([((uniq - c) ** 2).sum(1) for c in centres], axis=0)
        centres.append(uniq[d.argmax()])
    centres = np.array(centres)
    for _ in range(iters):
        lab = ((cols[:, None, :] - centres[None]) ** 2).sum(2).argmin(1)
        for j in range(k):
            sel = cols[lab == j]
            if len(sel):
                centres[j] = sel.mean(0)
    return centres


def render_sprite(name, model, turn, size):
    path = os.path.join(MODELS, model + ".gltf")
    stretch = 1 / math.cos(math.radians(PITCH))
    # Measure first (the renderer's size depends on the model), then render.
    probe = Renderer(4, 4, pitch=PITCH, ss=SS, tones={"x": ("#000000", "#000000", "#000000")})
    w0, d0, h0 = place(import_model(path), turn, size, stretch)
    p = math.radians(PITCH)
    W = int(math.ceil(w0)) + 6
    H = int(math.ceil(h0 * math.cos(p) + d0 * math.sin(p))) + 6
    r = Renderer(W, H, pitch=PITCH, ss=SS, tones={"x": ("#000000", "#000000", "#000000")})
    objs = import_model(path)
    w, d, h = place(objs, turn, size, stretch)
    r.water()   # the ground: anything of the model below it (posts, a slab) is left out
    r.cam.data.ortho_scale = max(W, H)
    r.aim((0, d / 2, h / stretch / 2))
    tmp = OUT
    # Unlit colour.
    cache = {}
    def mk(mt):
        key = mt.name if mt else "_none"
        if key not in cache:
            cache[key] = unlit(mt) if mt else r._emission("none", (0.6, 0.6, 0.6))
        return cache[key]
    saved = swap_all(objs, mk)
    alb = r._render(os.path.join(tmp, "_alb.png"))
    restore(saved)
    # Which material each subpixel is (index / 255 in red), so each surface can be drawn in clean tones of one colour.
    ids = {}
    def mkid(mt):
        key = mt.name if mt else "_none"
        if key not in ids:
            ids[key] = r._emission("id_" + key, ((len(ids) + 1) / 255.0, 0, 0))
        return ids[key]
    saved = swap_all(objs, mkid)
    idp = r._render(os.path.join(tmp, "_id.png"))
    restore(saved)
    saved = swap_all(objs, lambda mt: r.normal_mat)
    nrm = r._render(os.path.join(tmp, "_nrm.png"))
    restore(saved)
    ba, bn, bi = r._blocks(alb), r._blocks(nrm), r._blocks(idp)
    gh, gw = ba.shape[:2]
    # Each material's own colour: the median of everything it shows in the sprite. Drawing each surface in steps of that one
    # colour (lit by its shape, with only the texture's broad light and dark) reads as clean pixel art; sampling the texture
    # pixel by pixel gave a noisy, speckled look.
    allid = np.rint(idp[..., 0] * 255).astype(int)
    allcov = idp[..., 3] > 0.5
    flat = {}
    for k in np.unique(allid[allcov]):
        sel = (allid == k) & allcov & (alb[..., 3] > 0.5)
        if sel.any():
            flat[int(k)] = np.median(alb[sel][:, :3], axis=0)
    px = {}
    for j in range(gh):
        for i in range(gw):
            cov = ba[j, i, :, 3] > 0.5
            if cov.mean() < 0.5:
                continue
            k = np.rint(bi[j, i, cov, 0] * 255).astype(int)
            mat = int(np.bincount(k).argmax())
            base = flat.get(mat)
            here = ba[j, i, cov, :3][k == mat]
            if base is None or len(here) == 0:
                continue
            # The texture's broad light and dark, in three steps.
            rel = here.mean() / max(base.mean(), 0.02)
            col = base * (1.07 if rel > 1.08 else 0.93 if rel < 0.92 else 1.0)
            n = bn[j, i, cov, :3].mean(0) * 2 - 1
            n = n / max(np.linalg.norm(n), 1e-6)
            dd = float(n @ LIGHT)
            if dd > 0.72:
                col = col * 1.1 + 0.03
            elif dd < -0.15:
                col = col * np.array([0.70, 0.72, 0.82])
            elif dd < 0.12:
                col = col * np.array([0.84, 0.86, 0.92])
            px[(i, j)] = (np.clip(col, 0, 1), mat)
    # Each surface's own pattern, drawn in pixels as the game's own buildings are (a flat slab of one colour read as a lid):
    # corrugated iron in vertical ribs, thatch in layered rows that sag a little, a light ridge along the top of a roof and
    # a dark line under its eave, planks and bamboo in vertical lines. Something small and dark lying on a tin roof (the
    # models weigh their roofs down with an old tyre, which read as a black blob) is painted over with the roof.
    name_of = {i + 1: k for i, k in enumerate(ids)}
    def kind(mat):
        nm = name_of.get(mat, "")
        return next((k for k in ("iron_roof", "thatch", "planks", "bamboo", "sawali") if k in nm), None)
    for (i, j), (col, mat) in list(px.items()):
        if kind(mat) is None and col.mean() < 0.4:
            ring = [px.get((i + dx, j + dy)) for dx in range(-3, 4) for dy in range(-3, 4) if (dx, dy) != (0, 0)]
            roofs = [p for p in ring if p and kind(p[1]) == "iron_roof"]
            if len(roofs) >= 14:
                px[(i, j)] = (np.median([p[0] for p in roofs], axis=0), roofs[0][1])
    def top_of(i, j, mat):
        k = 0
        while (i, j - k - 1) in px and px[(i, j - k - 1)][1] == mat:
            k += 1
        return k
    for (i, j), (col, mat) in list(px.items()):
        kd = kind(mat)
        if kd is None:
            continue
        below = px.get((i, j + 1))
        above = px.get((i, j - 1))
        f = 1.0
        if kd == "iron_roof":
            f = 1.12 if i % 3 == 0 else 0.9 if i % 3 == 2 else 1.0
        elif kd == "thatch":
            depth = top_of(i, j, mat)
            sag = 1 if (i * 7 + depth) % 11 == 0 else 0
            f = 0.86 if (depth + sag) % 3 == 2 else 1.04 if (depth + sag) % 3 == 0 else 1.0
        elif kd in ("planks", "bamboo", "sawali"):
            f = 0.88 if i % (4 if kd == "planks" else 2) == 0 else 1.0
        if kd in ("iron_roof", "thatch"):
            if above is None or above[1] != mat:
                f = max(f, 1.16)          # the ridge, catching the light
            elif below is None or below[1] != mat:
                f = min(f, 0.78)          # the eave's edge, in shadow
        px[(i, j)] = (np.clip(col * f, 0, 1), mat)
    # An inner edge a shade darker where the shape meets the ground or the sky, and where one surface meets another below or
    # to the right of it (a roof's eave over its wall): it keeps the building apart from the ground under it (thatch beside
    # sand read as one), without the hard outside line the game's people and trees don't have.
    out = {}
    for (i, j), (col, mat) in px.items():
        edge = any((i + dx, j + dy) not in px for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        seam = not edge and any(px[(i + dx, j + dy)][1] != mat and px[(i + dx, j + dy)][0].mean() < col.mean() for dx, dy in ((0, 1),))
        out[(i, j)] = col * (0.66 if edge else 0.85 if seam else 1.0)
    px = out
    anchor = r.anchor((0, 0, 0))
    return name, gw, gh, px, anchor, (w, d)


results = []
for spec in SPRITES:
    try:
        results.append(render_sprite(*spec))
        print("rendered", spec[0], results[-1][1], results[-1][2], results[-1][4])
    except Exception as e:
        print("FAILED", spec, e)

# Palettes, and a preview sheet of every sprite on grass.
out = []
for name, gw, gh, px, (ax, ay), (fw, fd) in results:
    cols = list(px.values())
    pal = kmeans(cols, MAXCOL) if cols else np.zeros((1, 3))
    pal = np.clip(pal, 0, 1)
    rows = []
    for j in range(gh):
        s = ""
        for i in range(gw):
            if (i, j) not in px:
                s += "."
            else:
                s += KEYS[int(((pal - px[(i, j)]) ** 2).sum(1).argmin())]
        rows.append(s)
    # Trim empty rows and columns (keeping the anchor in the same place).
    top = next((j for j, s in enumerate(rows) if s.strip(".")), 0)
    bot = max((j for j, s in enumerate(rows) if s.strip(".")), default=gh - 1)
    left = min((len(s) - len(s.lstrip(".")) for s in rows if s.strip(".")), default=0)
    right = max((len(s.rstrip(".")) for s in rows if s.strip(".")), default=gw)
    rows = [s[left:right] for s in rows[top:bot + 1]]
    hexes = ["#%02x%02x%02x" % tuple(int(round(v * 255)) for v in c) for c in pal]
    out.append((name, hexes, rows, ax - left, ay - top, int(round(fw)), int(round(fd))))

sheet_w = sum(len(o[2][0]) + 6 for o in out) if out else 10
sheet_h = max(len(o[2]) for o in out) + 6 if out else 10
img = np.zeros((sheet_h, sheet_w, 3), dtype=np.float32) + np.array([0.42, 0.58, 0.32])
x0 = 3
for name, hexes, rows, ax, ay, fw, fd in out:
    pal = [np.array([int(h[k:k + 2], 16) / 255 for k in (1, 3, 5)]) for h in hexes]
    for j, s in enumerate(rows):
        for i, ch in enumerate(s):
            if ch != ".":
                img[3 + j, x0 + i] = pal[KEYS.index(ch)]
    if 0 <= 3 + ay < sheet_h and 0 <= x0 + ax < sheet_w: img[3 + ay, x0 + ax] = (1, 0, 0)
    x0 += len(rows[0]) + 6
big = np.repeat(np.repeat(img, 4, 0), 4, 1)
im = bpy.data.images.new("sheet", big.shape[1], big.shape[0], alpha=True)
im.colorspace_settings.name = "Non-Color"
im.pixels.foreach_set(np.concatenate([big, np.ones(big.shape[:2] + (1,))], 2)[::-1].astype(np.float32).ravel())
im.filepath_raw = os.path.join(OUT, "phtown-sheet.png")
im.file_format = "PNG"
im.save()

if CS:
    L = [
        "// Generated by tools/blender/phtown.py from the Feshtry2 prototype's glTF models: don't edit by hand, re-run the script.",
        "// Each sprite is a grid of palette keys (KEYS; '.' clear) with its anchor at the middle of its front edge on the ground,",
        "// and W x D, its footprint on the ground in game pixels (for the solid box).",
        "namespace Fesh;",
        "",
        "static class PhTownArt",
        "{",
        f'    public const string Keys = "{KEYS}";',
    ]
    for name, hexes, rows, ax, ay, fw, fd in out:
        L.append(f"    public static readonly string[] {name}Palette = {{ " + ", ".join(f'"{h}"' for h in hexes) + " };")
        L.append(f"    public const int {name}AnchorX = {ax}, {name}AnchorY = {ay}, {name}W = {fw}, {name}D = {fd};")
        L.append(f"    public static readonly string[] {name} =")
        L.append("    {")
        L += [f'        "{s}",' for s in rows]
        L.append("    };")
    L.append("}")
    with open(CS, "w", encoding="utf8", newline="\n") as fo:
        fo.write("\n".join(L) + "\n")
print("DONE", [(o[0], len(o[2][0]), len(o[2]), o[3], o[4]) for o in out])
