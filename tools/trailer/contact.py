# Contact sheet of trailer frames: blender -b --python contact.py -- out.png cols step frame1.png frame2.png ...
# Each frame is shrunk by 'step' (2 = half size) and laid out in rows of 'cols', numbered by its order.
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
out, cols, step, files = a[0], int(a[1]), int(a[2]), a[3:]
tiles = []
for f in files:
    im = bpy.data.images.load(f)
    w, h = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::step, ::step]
    tiles.append(px)
    bpy.data.images.remove(im)
th, tw = tiles[0].shape[:2]
rows = (len(tiles) + cols - 1) // cols
sheet = np.zeros((rows * (th + 4), cols * (tw + 4), 4), dtype=np.float32); sheet[..., 3] = 1
for i, t in enumerate(tiles):
    r, c = divmod(i, cols)
    y0 = (rows - 1 - r) * (th + 4)   # Blender's pixels start at the bottom
    sheet[y0:y0 + th, c * (tw + 4):c * (tw + 4) + tw] = t
H, W = sheet.shape[:2]
img = bpy.data.images.new("sheet", W, H, alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = out; img.file_format = 'PNG'; img.save()
