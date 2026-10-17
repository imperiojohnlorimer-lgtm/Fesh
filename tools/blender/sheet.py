# A zoomed contact sheet of a generated frame-art file (like ArapaimaArt.cs), to look at the frames before using them.
# Run:  blender -b --python tools/blender/sheet.py -- <Art.cs> <out.png> [scale] [per_row]
import sys, re, base64, zlib
import bpy
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]
scale = int(a[2]) if len(a) > 2 else 6
per = int(a[3]) if len(a) > 3 else 6
text = open(src, encoding="utf8").read()
W = int(re.search(r"Width = (\d+)", text).group(1))
H = int(re.search(r"Height = (\d+)", text).group(1))
pal = re.findall(r'"(#[0-9a-fA-F]{6})"', re.search(r"Palette = \{([^}]*)\}", text).group(1))
pal = [np.array([int(c[i:i + 2], 16) for i in (1, 3, 5)], dtype=np.float32) for c in pal]
data = re.findall(r'^\s+"([A-Za-z0-9+/=]{40,})",', text, re.M)
tiles = []
for d in data:
    raw = zlib.decompress(base64.b64decode(d))
    img = np.zeros((H, W, 3), dtype=np.float32) + np.array([40, 70, 60])
    for j in range(H):
        for i in range(W):
            v = raw[j * W + i]
            if v != 255:
                img[j, i] = pal[v]
    img[:, -1] = 255
    img[-1, :] = 255
    tiles.append(np.repeat(np.repeat(img, scale, 0), scale, 1))
rows = []
for k in range(0, len(tiles), per):
    row = tiles[k:k + per]
    while len(row) < per:
        row.append(np.zeros_like(tiles[0]) + 255)
    rows.append(np.concatenate(row, axis=1))
sheet = np.concatenate(rows, axis=0)
im = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=True)
im.colorspace_settings.name = "Non-Color"
px = np.concatenate([sheet / 255, np.ones(sheet.shape[:2] + (1,))], axis=2)[::-1].astype(np.float32)
im.pixels.foreach_set(px.ravel())
im.filepath_raw = out
im.file_format = "PNG"
im.save()
print("SHEET", len(tiles))
