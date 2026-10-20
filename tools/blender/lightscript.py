# Look-dev for the light and colour pass (1.39): a little island scene (hut, oak, palm, a boulder, sand and sea) lit by a
# sun that follows the game's stylised path, hour by hour, as a contact sheet. It is a reference for choosing the colour
# grade keyframes (Grade.cs) and the shadow directions and lengths (the sun projection), not something the game draws.
# The sun's azimuth and altitude are the game's: theta = pi * (hour - 6) / 14, altitude = 65 deg * sin(theta), rising in
# the east and setting in the west, a touch north (so shadows fall west in the morning, east in the evening, a little south).
# Run:  blender -b --python tools/blender/lightscript.py -- <scratch folder> [out.png]
import bpy, sys, os, math
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
scratch = args[0]
out = args[1] if len(args) > 1 else os.path.join(scratch, "lightscript.png")
os.makedirs(scratch, exist_ok=True)

# hour, sun colour, sky colour, sky strength
HOURS = [
    (6.0, (1.0, 0.72, 0.62), (0.55, 0.50, 0.62), 0.55),
    (9.0, (1.0, 0.93, 0.82), (0.55, 0.68, 0.85), 0.8),
    (12.0, (1.0, 0.99, 0.96), (0.50, 0.70, 0.95), 0.9),
    (15.0, (1.0, 0.95, 0.84), (0.52, 0.68, 0.92), 0.85),
    (17.0, (1.0, 0.82, 0.58), (0.62, 0.62, 0.82), 0.7),
    (19.5, (1.0, 0.50, 0.24), (0.70, 0.42, 0.45), 0.45),
]
W, H = 480, 270


def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    return m


def add(obj, m):
    obj.data.materials.append(m)
    return obj


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    grass, sand, sea = mat("grass", (0.16, 0.34, 0.10)), mat("sand", (0.62, 0.52, 0.30)), mat("sea", (0.06, 0.24, 0.38), 0.25)
    wood, roof, trunk, leaf, rock = mat("wood", (0.40, 0.25, 0.12)), mat("roof", (0.62, 0.20, 0.12)), mat("trunk", (0.25, 0.15, 0.08)), mat("leaf", (0.10, 0.30, 0.08)), mat("rock", (0.40, 0.40, 0.42))
    # sea and island
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.5)); add(bpy.context.object, sea)
    bpy.ops.mesh.primitive_cylinder_add(radius=60, depth=1, location=(0, 0, -0.5), vertices=48); add(bpy.context.object, sand)
    bpy.ops.mesh.primitive_cylinder_add(radius=46, depth=1.2, location=(0, 0, -0.45), vertices=48); add(bpy.context.object, grass)
    # a hut (walls and a pitched roof)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(-18, 6, 7)); h = bpy.context.object; h.scale = (20, 16, 14); add(h, wood)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=17, depth=10, location=(-18, 6, 19), rotation=(0, 0, math.radians(45))); r = bpy.context.object; r.scale = (1.0, 0.85, 1); add(r, roof)
    # an oak: trunk and a crown of three spheres
    bpy.ops.mesh.primitive_cylinder_add(radius=2.2, depth=22, location=(20, -6, 11)); add(bpy.context.object, trunk)
    for dx, dy, dz, rad in ((20, -6, 30, 11), (14, -4, 25, 8), (26, -8, 26, 8.5)):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=rad, location=(dx, dy, dz), segments=24, ring_count=16); add(bpy.context.object, leaf)
    # a palm
    bpy.ops.mesh.primitive_cylinder_add(radius=1.4, depth=26, location=(2, 22, 13), rotation=(math.radians(6), 0, 0)); add(bpy.context.object, trunk)
    for a in range(6):
        ang = a * math.tau / 6
        bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=2, depth=15, location=(2 + math.cos(ang) * 6, 23 + math.sin(ang) * 6, 25), rotation=(math.radians(100), 0, ang - math.pi / 2)); add(bpy.context.object, leaf)
    # a boulder
    bpy.ops.mesh.primitive_uv_sphere_add(radius=6, location=(-4, -14, 2)); b = bpy.context.object; b.scale = (1.3, 1, 0.7); add(b, rock)

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 120
    cam.rotation_euler = (math.radians(35), 0, 0)   # looking down at about 55 degrees, like the game's 3/4 view
    cam.location = Vector((0, -86, 123))
    sc.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.angle = math.radians(2.5)
    world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
    return sc, sun, world


def aim(sun, hour):
    theta = math.pi * (hour - 6) / 14
    alt = math.radians(max(8.0, 65 * math.sin(theta)))
    # the direction towards the sun: east (+x) in the morning, west (-x) in the evening, a little north (+y)
    d = Vector((math.cos(theta) * math.cos(alt), 0.3 * math.cos(alt), math.sin(alt))).normalized()
    sun.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    return alt


sc, sun, world = build()
tiles = []
for hour, scol, skycol, sky in HOURS:
    alt = aim(sun, hour)
    sun.data.color = scol
    sun.data.energy = 3.0 + 3.5 * math.sin(alt)
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*skycol, 1); bg.inputs[1].default_value = sky
    path = os.path.join(scratch, f"hour-{hour:04.1f}.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    px = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4)[::-1, :, :3]
    px = np.clip(px, 0, 1) * 255
    tiles.append(px)
    print("HOUR", hour, "alt", round(math.degrees(alt), 1))
rows = [np.concatenate(tiles[i:i + 3], axis=1) for i in range(0, len(tiles), 3)]
sheet = np.concatenate(rows, axis=0)
img = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=True)
img.colorspace_settings.name = "Non-Color"
data = np.concatenate([sheet / 255, np.ones(sheet.shape[:2] + (1,))], axis=2)[::-1].astype(np.float32)
img.pixels.foreach_set(data.ravel())
img.filepath_raw = out
img.file_format = "PNG"
img.save()
print("SHEET", out)
