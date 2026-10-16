# The small buildings, modelled and rendered to pixel art (see pixelrender.py): a villager's bahay, your shack, the
# bahay kubo you can build, and Tomas's hut. One file, BuildingArt.cs, holds all four.
# Run:  blender -b --python tools/blender/buildings.py -- <out_dir> [cs_path]
#
# Each is laid out on its old hand drawing (heights in rows above the ground, x in pixels from the middle of the front
# wall), so the windows the game paints itself (lit at night), the doors and the collision boxes still line up. Roofs
# have their ridge running back from the viewer, so the west slope is lit and the east slope in shade, with a small hip
# at the front, as the old drawings showed them. Materials marked "special" are coloured by the game.
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from pixelrender import Renderer, args, SPECIAL_KEYS

a = args()
OUT = a[0] if a else "."
CS = a[1] if len(a) > 1 else None
os.makedirs(OUT, exist_ok=True)

# A lower view than the lighthouse: at 30 degrees a deep roof showed as a tall block with straight sides.
PITCH = 16
# Drawn bigger than the old houses (which looked small beside the trees): the model is laid out on the old drawings and
# shown ZOOM times the size, so the game scales its window and chimney positions by the same Scale.
ZOOM = 1.3

WOOD = {
    "post":   ("#4a3220", "#65452e", "#7d5a3c"),
    "floor":  ("#4a3220", "#5a3e28", "#6e4c32"),
    "door":   ("#2c261f", "#3c342a", "#4d4437"),
    "step":   ("#a4804e", "#c7a06a", "#dcbb88"),
}
THATCH = {
    "thatch": ("#a68449", "#d0b170", "#e8cf8e"),
    "band":   ("#6e5230", "#93723f", "#ab8a52"),
    "ridge":  ("#5a4426", "#80643e", "#9a7a4a"),
}


def box(r, x0, x1, y0, y1, z0, z1, mat):
    """A box from x0..x1 across, y0..y1 back from the front wall, rows z0..z1 above the ground."""
    return r.box((x0 + x1) / 2, (y0 + y1) / 2, z0, x1 - x0, y1 - y0, z1 - z0, mat)


def roof(r, xh, y0, y1, eave, rise, mat, hip=3.0, thick=1.0, plain=None):
    """A roof with its ridge running across, so its front slope faces you with the thatch (or tiles) in horizontal rows,
    and hipped ends that catch the light on the west and fall into shade on the east. `hip` is how far in from each end
    the ridge stops; the hips use `plain` (no rows), so they read as the roof's lit and shaded sides."""
    bm = bmesh.new()
    Z = r.Z
    ym = (y0 + y1) / 2
    e0, e1, e2, e3 = [bm.verts.new((x, y, Z(eave))) for x, y in ((-xh, y0), (xh, y0), (xh, y1), (-xh, y1))]
    ra = bm.verts.new((-xh + hip, ym, Z(eave + rise)))
    rb = bm.verts.new((xh - hip, ym, Z(eave + rise)))
    faces = [(bm.faces.new((e0, e1, rb, ra)), 0),      # front slope
             (bm.faces.new((e2, e3, ra, rb)), 0),      # back slope
             (bm.faces.new((e3, e0, ra)), 1),          # west hip
             (bm.faces.new((e1, e2, rb)), 1),          # east hip
             (bm.faces.new((e3, e2, e1, e0)), 1)]      # underside
    for f, k in faces:
        f.material_index = k if plain else 0
    me = bpy.data.meshes.new("roof")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("roof", me)
    r.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    if plain:
        o.data.materials.append(plain)
    s = o.modifiers.new("thick", "SOLIDIFY")
    s.thickness = thick
    return o


def finish(r, name, size_note):
    grid = r.render(OUT)
    ax, ay = r.anchor((0, 0, 0))
    return name, grid, ax, ay, r, size_note


results = []

# ---- A villager's bahay (Archipelago.cs DrawBahay; the anchor is the middle of the front wall on the ground, which
# is the old drawing's y + 5). Walls from row 5 to 24, 32 wide; door in the middle; windows at x -13..-7 and 7..12,
# rows 12..17; eaves 42 wide at row 24. The band along the foot of the wall is the resident's colour.
r = Renderer(int(50 * ZOOM), int(58 * ZOOM), pitch=PITCH, zoom=ZOOM, tones={**WOOD, **THATCH,
                                     "wall": ("#94693e", "#b98b52", "#cfa466"), "slat": ("#bf9758", "#dfbc78", "#ecd096")},
             special="trim", lit_at=(0.66, 0.0))
M = r.ids
D = 18.0
for x in (-14, 12):
    for y in (0.5, D - 1.5):
        box(r, x, x + 2, y, y + 1.5, 0, 5.5, M["post"])
box(r, -16.5, 16.5, -0.5, D + 0.5, 4.5, 5.5, M["floor"])
box(r, -16, 16, 0, D, 5.5, 24, r.banded("weave", "X", 4.0, "wall", "slat", 0.72))
box(r, -16.3, 16.3, -0.3, D + 0.3, 5.5, 7.5, M["trim"])
box(r, -3, 4, -0.4, 0.6, 5.5, 16, M["door"])
for k in range(3):
    box(r, -4, 5, -4 + k * 1.3, -2.6 + k * 1.3, k * 1.6, k * 1.6 + 1.6, M["step"])
roof(r, 23, -5, D + 5, 24, 12, r.banded("thatch", "Z", r.Z(3.0), "thatch", "band", 0.7), hip=14, thick=1.4, plain=M["thatch"])
r.rod((-9.5, D / 2, 24 + 12 + 0.4), (9.5, D / 2, 24 + 12 + 0.4), 0.9, M["ridge"])
r.aim((0, D / 2, 21))
results.append(finish(r, "Bahay", "bahay"))

# ---- Your shack (World.cs DrawShack at the tile's X, Y; the anchor is X + 10, Y + 9). Plank walls 18 wide, rows 0..12,
# the door at x -2..2 to row 6, windows at x -7..-5 and 4..6, rows 6..8, and a blue tin roof from row 12 up to row 20.
r = Renderer(int(28 * ZOOM), int(30 * ZOOM), pitch=PITCH, zoom=ZOOM, tones={**WOOD,
                                     "plank": ("#8a5f36", "#a8794a", "#c0915c"), "seam": ("#6e4a2a", "#8a5f36", "#9a6c40"),
                                     "tin": ("#33607d", "#4a7d9c", "#6fa4c4"), "rib": ("#284d66", "#3b6b88", "#5a8fb0"),
                                     "lamp": ("#c99a3a", "#f3c25b", "#ffe08a")}, lit_at=(0.66, 0.0))
M = r.ids
D = 10.0
box(r, -9, 9, 0, D, 0, 12, r.banded("planks", "Z", r.Z(3.0), "plank", "seam", 0.6))
box(r, -2, 2, -0.4, 0.6, 0, 6, M["door"])
box(r, 1.0, 2.0, -0.6, 0.4, 3, 4, M["lamp"])          # the brass knob
roof(r, 12, -2, D + 2, 12, 7, r.banded("tin", "X", 2.0, "tin", "rib", 0.6), hip=10, thick=0.8, plain=M["tin"])
r.aim((0, D / 2, 10))
results.append(finish(r, "Shack", "shack"))

# ---- A bahay kubo (World.cs DrawKubo; anchor X + 10, Y + 10). Four stilts to row 7, the floor rows 6..8, woven walls
# 16 wide rows 8..16 with the door x -2..2 rows 8..14 and windows x -7..-5 and 4..6 rows 11..14, steps, and a steep
# nipa roof from row 17 to 26 with eaves 22 wide.
r = Renderer(int(28 * ZOOM), int(34 * ZOOM), pitch=PITCH, zoom=ZOOM, tones={**WOOD, **THATCH,
                                     "wall": ("#b89a62", "#d9bf86", "#ead4a2"), "slat": ("#a88c56", "#c4a668", "#d6ba80"),
                                     "deck": ("#8a6a3c", "#a8844e", "#c09a62")}, lit_at=(0.66, 0.0))
M = r.ids
D = 12.0
for x in (-8, -3, 2, 7):
    for y in (0.5, D - 1.5):
        box(r, x, x + 1.2, y, y + 1.2, 0, 7, M["post"])
box(r, -9, 9, -0.6, D + 0.6, 6, 8, M["deck"])
box(r, -8, 8, 0, D, 8, 16.5, r.banded("weave", "X", 2.0, "wall", "slat", 0.5))
box(r, -2, 2, -0.4, 0.6, 8, 14, M["door"])
for k in range(3):
    box(r, -2, 2, -3.6 + k * 1.2, -2.4 + k * 1.2, k * 2.2, k * 2.2 + 1.0, M["step"])
roof(r, 12.5, -3, D + 3, 16.5, 9, r.banded("thatch", "Z", r.Z(3.0), "thatch", "band", 0.68), hip=9.5, thick=1.0, plain=M["thatch"])
r.aim((0, D / 2, 13))
results.append(finish(r, "Kubo", "kubo"))

# ---- Tomas's hut (World.cs DrawHut, fixed at x 151..169; anchor 160, 69). Plank walls 18 wide rows 0..12, the door at
# x -2..2 to row 7, the window at x 4..7 rows 6..9, a red tiled roof up to row 21, and a stone chimney east of the
# ridge (the game draws the smoke).
r = Renderer(int(28 * ZOOM), int(30 * ZOOM), pitch=PITCH, zoom=ZOOM, tones={**WOOD,
                                     "plank": ("#7e5530", "#9a6a3a", "#b07c48"), "seam": ("#5e3e22", "#7a5230", "#8a6038"),
                                     "tile": ("#7f3424", "#a8472f", "#d06a4c"), "tiledk": ("#6a2c1e", "#94442f", "#b35a40"),
                                     "stone": ("#5d6268", "#7d8288", "#9aa0a5")}, lit_at=(0.66, 0.0))
M = r.ids
D = 10.0
box(r, -9, 9, 0, D, 0, 12, r.banded("planks", "Z", r.Z(3.0), "plank", "seam", 0.6))
box(r, -2, 2, -0.4, 0.6, 0, 7, M["door"])
roof(r, 12.5, -2, D + 2, 11, 9, r.banded("tiles", "Z", r.Z(2.0), "tile", "tiledk", 0.5), hip=10.5, thick=0.9, plain=M["tile"])
box(r, 4.5, 7.5, 3.0, 6.0, 14, 23, M["stone"])
r.aim((0, D / 2, 10))
results.append(finish(r, "Hut", "hut"))

# ---- Ma'am Isay's school (School.cs DrawSchool; anchor the middle of the front wall on the ground, the old y + 5).
# Bamboo walls 46 wide from row 5 to 24 on stilts, the gold band along the foot, the blackboard (x -15..3, rows 10..20),
# the door (x 7..14, rows 5..17), a window (x 16..21, rows 13..18) and a thatched roof with eaves 54 wide at row 25.
# The game draws the chalk fish, the flag and the window glass.
r = Renderer(int(64 * ZOOM), int(52 * ZOOM), pitch=PITCH, zoom=ZOOM, tones={**WOOD, **THATCH,
             "wall": ("#a88048", "#c39a5c", "#d8b274"), "slat": ("#c8a666", "#e2c486", "#efd6a2"),
             "gold": ("#a87a22", "#d9a43a", "#efc566"), "board": ("#22382c", "#2f4a3a", "#3e5c4a"),
             "frame": ("#4e3420", "#6b4a2b", "#86603a")}, lit_at=(0.66, 0.0))
M = r.ids
D = 20.0
for x in (-21, 19):
    for y in (0.5, D - 1.5):
        box(r, x, x + 2, y, y + 1.5, 0, 5.5, M["post"])
box(r, -23.5, 23.5, -0.5, D + 0.5, 4.5, 5.5, M["floor"])
box(r, -23, 23, 0, D, 5.5, 24, r.banded("weave", "X", 3.0, "wall", "slat", 0.62))
box(r, -23.3, 23.3, -0.3, D + 0.3, 5.5, 7.5, M["gold"])
box(r, -15, 3, -0.5, 0.5, 10, 20, M["frame"])
box(r, -14, 2, -0.8, 0.2, 11, 19, M["board"])
box(r, 7, 14, -0.4, 0.6, 5.5, 17, M["door"])
for k in range(3):
    box(r, 6, 15, -4 + k * 1.3, -2.6 + k * 1.3, k * 1.6, k * 1.6 + 1.6, M["step"])
roof(r, 27, -5, D + 5, 25, 12, r.banded("thatch", "Z", r.Z(3.0), "thatch", "band", 0.7), hip=14, thick=1.4, plain=M["thatch"])
r.rod((-13, D / 2, 25 + 12 + 0.4), (13, D / 2, 25 + 12 + 0.4), 0.9, M["ridge"])
r.aim((0, D / 2, 21))
results.append(finish(r, "School", "school"))

# ---- Ben's volcano station (Magayon.cs DrawStation; anchor the old y + 5). Rendered walls 30 wide on tall stilts, rows
# 9..24, a teal band, a blue-grey door (x -4..3, rows 9..19), a low tin roof with eaves 34 wide at row 25 and a solar panel
# on it, the radio mast at x -12 up to row 47 and a dish. The game draws the window glass and the mast's blinking light.
r = Renderer(int(44 * ZOOM), int(54 * ZOOM), pitch=PITCH, zoom=ZOOM, tones={**WOOD,
             "white": ("#b8b4a8", "#d8d4c8", "#efece4"), "teal": ("#1e6a6a", "#2a8a8a", "#44a8a6"),
             "sdoor": ("#2c3846", "#3c4a5a", "#4e5e70"), "tin": ("#7a8085", "#9aa0a5", "#c4cacd"),
             "rib": ("#6a7075", "#868c91", "#a8aeb2"), "panel": ("#22385a", "#2f4f7a", "#5f8fc0"),
             "mast": ("#9a9a92", "#c8c8c0", "#e8e8e0")}, lit_at=(0.66, 0.0))
M = r.ids
D = 14.0
for x in (-13, 11):
    for y in (0.5, D - 1.5):
        box(r, x, x + 2, y, y + 1.5, 0, 9.5, M["post"])
box(r, -15.5, 15.5, -0.5, D + 0.5, 8.5, 9.5, M["floor"])
box(r, -15, 15, 0, D, 9.5, 24, M["white"])
box(r, -15.3, 15.3, -0.3, D + 0.3, 9.5, 11.5, M["teal"])
box(r, -4, 3, -0.4, 0.6, 9.5, 19.5, M["sdoor"])
for k in range(4):
    box(r, -4, 3, -5 + k * 1.3, -3.6 + k * 1.3, k * 2.3, k * 2.3 + 1.4, M["step"])
roof(r, 17, -2, D + 2, 24, 6, r.banded("tin", "X", 2.0, "tin", "rib", 0.6), hip=5, thick=0.8, plain=M["tin"])
bpy.ops.mesh.primitive_cube_add(size=1, location=(7, 1.5, r.Z(26.2)), rotation=(math.radians(28), 0, 0))
pnl = bpy.context.object
pnl.scale = (10, 5, 0.6)
pnl.data.materials.append(M["panel"])
r.rod((-12, D / 2, 24), (-12, D / 2, 47), 0.5, M["mast"])
for k in range(4):
    r.rod((-13.5, D / 2, 26 + k * 5), (-10.5, D / 2, 26 + k * 5), 0.3, M["mast"])
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=2.4, location=(-6, D / 2 - 1, r.Z(33)))
dish = bpy.context.object
dish.scale = (1, 0.45, 1)
dish.rotation_euler = (0, 0, math.radians(-30))
dish.data.materials.append(M["white"])
bpy.ops.object.shade_smooth()
r.aim((0, D / 2, 26))
results.append(finish(r, "Station", "station"))

# Previews: each with its own palette (and the bahay's band in red).
for name, grid, ax, ay, rr, note in results:
    rr.preview(os.path.join(OUT, f"{note}-preview.png"), [grid], special_colours=("#8a2a20", "#c0392b", "#e05a48"), back=(120, 160, 100))

if CS:
    lines = [
        "// Generated by tools/blender/buildings.py from 3D models: don't edit by hand, change the models and re-run the",
        "// script. Each building is a grid of palette letters ('.' clear) with its anchor at the middle of the front wall on",
        "// the ground; " + "/".join(SPECIAL_KEYS) + " in the bahay are the band along the foot of its wall, in the resident's colour",
        "// (shade, base, light). The game paints the windows itself.",
        "namespace Fesh;",
        "",
        "static class BuildingArt",
        "{",
        f"    public const float Scale = {ZOOM}f;",
    ]
    for name, grid, ax, ay, rr, note in results:
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
print("DONE", [(n, ax, ay) for n, _, ax, ay, _, _ in results])
