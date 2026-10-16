# Shared by the model scripts in this folder: renders a Blender model straight to Fesh-style pixel art.
#
# Models are built in game pixels (1 Blender unit = 1 pixel across; heights are given in screen rows and stretched by
# 1 / cos(pitch) so they come out that many rows tall). Blender doesn't light anything: flat renders at SS x the game's
# pixels give each subpixel's material and surface normal, and every SS x SS block becomes one game pixel, the commonest
# material in it lit by the block's mean normal in three tones (shade, base, light) from the upper left, the way the
# game's own art is shaded. A material can be "special": its pixels are written as a tone index for the game to colour
# (the lighthouse's glass changes with the lamp).
import bpy, sys, os, math
import numpy as np
from mathutils import Vector

LIGHT = np.array([-0.62, -0.35, 0.70])      # from the upper left: west, a little towards the viewer, high
LIGHT = LIGHT / np.linalg.norm(LIGHT)
KEYS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVW"
SPECIAL_KEYS = "XYZ"                         # a special material's shade, base and light


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


class Renderer:
    def __init__(self, w, h, pitch, ss=8, tones=None, special=None, lit_at=(0.72, 0.08), zoom=1.0):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.scene = bpy.context.scene
        self.w, self.h, self.pitch, self.ss = w, h, pitch, ss
        self.zoom = zoom          # game pixels per model unit: draws the same model bigger without changing it
        self.tones = dict(tones)
        self.special = special
        self.mats = list(self.tones) + ([special] if special else [])
        self.lit_at = lit_at
        self.stretch = 1 / math.cos(math.radians(pitch))
        self.ids = {k: self._emission(f"id_{k}", (self.id_of(k), 0, 0)) for k in self.mats}
        self.normal_mat = self._normal_material()
        self._setup()

    # ---- materials
    def id_of(self, k):
        return (self.mats.index(k) + 1) / 64.0

    def _emission(self, name, rgb):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        n = m.node_tree.nodes
        n.remove(n["Principled BSDF"])
        em = n.new("ShaderNodeEmission")
        em.inputs[0].default_value = (*rgb, 1)
        m.node_tree.links.new(em.outputs[0], n["Material Output"].inputs[0])
        return m

    def banded(self, name, axis, period, a, b, split):
        """A material that is `a` then `b` in bands along an object axis: b where frac(coord / period) >= split.
        `period` and the coordinate are in model units (heights already stretched)."""
        m = bpy.data.materials.new(name)
        self.band_of = getattr(self, "band_of", {})
        self.band_of[name] = a
        m.use_nodes = True
        n = m.node_tree.nodes
        l = m.node_tree.links
        n.remove(n["Principled BSDF"])
        tc = n.new("ShaderNodeTexCoord")
        sep = n.new("ShaderNodeSeparateXYZ")
        l.new(tc.outputs["Object"], sep.inputs[0])
        div = n.new("ShaderNodeMath")
        div.operation = "DIVIDE"
        div.inputs[1].default_value = period
        l.new(sep.outputs[axis], div.inputs[0])
        fr = n.new("ShaderNodeMath")
        fr.operation = "FRACT"
        l.new(div.outputs[0], fr.inputs[0])
        ge = n.new("ShaderNodeMath")
        ge.operation = "GREATER_THAN"
        ge.inputs[1].default_value = split
        l.new(fr.outputs[0], ge.inputs[0])
        ma = n.new("ShaderNodeMath")
        ma.operation = "MULTIPLY_ADD"
        ma.inputs[1].default_value = self.id_of(b) - self.id_of(a)
        ma.inputs[2].default_value = self.id_of(a)
        l.new(ge.outputs[0], ma.inputs[0])
        comb = n.new("ShaderNodeCombineXYZ")
        l.new(ma.outputs[0], comb.inputs[0])
        em = n.new("ShaderNodeEmission")
        l.new(comb.outputs[0], em.inputs[0])
        l.new(em.outputs[0], n["Material Output"].inputs[0])
        return m

    def _normal_material(self):
        m = bpy.data.materials.new("normal")
        m.use_nodes = True
        n = m.node_tree.nodes
        l = m.node_tree.links
        n.remove(n["Principled BSDF"])
        g = n.new("ShaderNodeNewGeometry")
        vm = n.new("ShaderNodeVectorMath")
        vm.operation = "MULTIPLY_ADD"
        vm.inputs[1].default_value = (0.5, 0.5, 0.5)
        vm.inputs[2].default_value = (0.5, 0.5, 0.5)
        l.new(g.outputs["Normal"], vm.inputs[0])
        em = n.new("ShaderNodeEmission")
        l.new(vm.outputs[0], em.inputs[0])
        l.new(em.outputs[0], n["Material Output"].inputs[0])
        return m

    # ---- geometry helpers (x across, y away from the viewer, z up; z arguments are screen rows)
    def Z(self, rows):
        return rows * self.stretch

    def _put(self, o, mat):
        o.data.materials.clear()
        o.data.materials.append(mat)
        return o

    def cylinder(self, x, y, z0, z1, r0, r1=None, mat=None, verts=32, smooth=True):
        """A cylinder (or frustum, r0 at the bottom, r1 at the top) standing from row z0 to row z1."""
        r1 = r0 if r1 is None else r1
        h = self.Z(z1 - z0)
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r0, radius2=r1, depth=h, location=(x, y, self.Z(z0) + h / 2))
        o = bpy.context.object
        if smooth:
            bpy.ops.object.shade_smooth()
        return self._put(o, mat)

    def box(self, x, y, z0, sx, sy, rows, mat):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, self.Z(z0) + self.Z(rows) / 2))
        o = bpy.context.object
        o.scale = (sx, sy, self.Z(rows))
        # Bake the size into the mesh, so a banded material's object coordinates are in pixels, not -0.5..0.5.
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        return self._put(o, mat)

    def rod(self, a, b, r, mat, verts=8):
        a = Vector((a[0], a[1], self.Z(a[2])))
        b = Vector((b[0], b[1], self.Z(b[2])))
        d = b - a
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=(a + b) / 2)
        o = bpy.context.object
        o.rotation_mode = "QUATERNION"
        o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
        bpy.ops.object.shade_smooth()
        return self._put(o, mat)

    # ---- rendering
    def _setup(self):
        s = self.scene
        s.render.engine = "CYCLES"
        s.cycles.device = "CPU"
        s.cycles.samples = 1
        s.cycles.pixel_filter_type = "BOX"
        s.cycles.filter_width = 0.01
        s.cycles.use_denoising = False
        s.render.film_transparent = True
        s.render.resolution_x = self.w * self.ss
        s.render.resolution_y = self.h * self.ss
        s.render.image_settings.file_format = "PNG"
        s.render.image_settings.color_mode = "RGBA"
        s.render.image_settings.color_depth = "16"
        s.view_settings.view_transform = "Raw"
        w = bpy.data.worlds.new("w")
        s.world = w
        w.use_nodes = True
        w.node_tree.nodes["Background"].inputs[1].default_value = 0
        cd = bpy.data.cameras.new("cam")
        cd.type = "ORTHO"
        cd.ortho_scale = max(self.w, self.h) / self.zoom
        cam = bpy.data.objects.new("cam", cd)
        s.collection.objects.link(cam)
        s.camera = cam
        self.cam = cam

    def aim(self, target):
        """Points the camera at a model point (z in rows) so it lands in the middle of the sprite."""
        p = math.radians(self.pitch)
        t = Vector((target[0], target[1], self.Z(target[2])))
        self.cam.location = t + Vector((0, -math.cos(p), math.sin(p))) * 200
        self.cam.rotation_euler = (math.radians(90 - self.pitch), 0, 0)
        bpy.context.view_layer.update()

    def anchor(self, point=(0, 0, 0)):
        from bpy_extras.object_utils import world_to_camera_view
        v = world_to_camera_view(self.scene, self.cam, Vector((point[0], point[1], self.Z(point[2]))))
        return int(round(v.x * self.w)), int(round((1 - v.y) * self.h))

    def _render(self, path):
        self.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path, check_existing=False)
        img.colorspace_settings.name = "Non-Color"
        a = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)[::-1]
        bpy.data.images.remove(img)
        os.remove(path)
        return a

    def _blocks(self, a):
        ss = self.ss
        h, w = a.shape[0] // ss, a.shape[1] // ss
        return a[:h * ss, :w * ss].reshape(h, ss, w, ss, -1).transpose(0, 2, 1, 3, 4).reshape(h, w, ss * ss, -1)

    def water(self):
        """A plane at the water line that hides everything under it (left transparent for the game's own sea)."""
        bpy.ops.mesh.primitive_plane_add(size=2000, location=(0, 0, 0.0))
        o = bpy.context.object
        o.name = "water"
        m = bpy.data.materials.new("holdout")
        m.use_nodes = True
        n = m.node_tree.nodes
        n.remove(n["Principled BSDF"])
        h = n.new("ShaderNodeHoldout")
        m.node_tree.links.new(h.outputs[0], n["Material Output"].inputs[0])
        o.data.materials.append(m)
        self.water_plane = o

    def clear(self):
        """Removes every object but the camera and the water, for the next frame of an animation."""
        keep = {self.cam, getattr(self, "water_plane", None)}
        for o in list(self.scene.objects):
            if o not in keep:
                bpy.data.objects.remove(o, do_unlink=True)

    def _height_material(self):
        """Writes each surface point's height above the water, in screen rows / 256, as colour."""
        m = bpy.data.materials.new("height")
        m.use_nodes = True
        n = m.node_tree.nodes
        l = m.node_tree.links
        n.remove(n["Principled BSDF"])
        g = n.new("ShaderNodeNewGeometry")
        sep = n.new("ShaderNodeSeparateXYZ")
        l.new(g.outputs["Position"], sep.inputs[0])
        div = n.new("ShaderNodeMath")
        div.operation = "DIVIDE"
        div.inputs[1].default_value = self.stretch * 256
        l.new(sep.outputs["Z"], div.inputs[0])
        comb = n.new("ShaderNodeCombineXYZ")
        l.new(div.outputs[0], comb.inputs[0])
        em = n.new("ShaderNodeEmission")
        l.new(comb.outputs[0], em.inputs[0])
        l.new(em.outputs[0], n["Material Output"].inputs[0])
        return m

    def _pass(self, tmp, name, mat):
        objs = [o for o in self.scene.objects if o.type in ("MESH", "CURVE") and o.data.materials and o != getattr(self, "water_plane", None)]
        saved = {o: list(o.data.materials) for o in objs}
        for o in objs:
            self._put(o, mat)
        a = self._render(os.path.join(tmp, name))
        for o, mats in saved.items():
            o.data.materials.clear()
            for m in mats:
                o.data.materials.append(m)
        return a

    keep_blend = True     # a script rendering many frames turns this off after the first

    def save_blend(self):
        """With FESH_BLEND_DIR set, saves the model as a .blend you can open in Blender: every material is swapped (in the
        saved copy only) for a plain one in its base colour, with a sun from the upper left, so it reads in the viewport."""
        folder = os.environ.get("FESH_BLEND_DIR")
        if not folder or not self.keep_blend:
            return
        os.makedirs(folder, exist_ok=True)
        script = os.path.splitext(os.path.basename(sys.argv[sys.argv.index("--python") + 1]))[0] if "--python" in sys.argv else "model"
        global _saved
        _saved = globals().get("_saved", 0) + 1
        path = os.path.join(folder, f"{script}-{_saved:02d}.blend")
        objs = [o for o in self.scene.objects if o.type in ("MESH", "CURVE", "META") and o.data.materials]
        saved = {o: list(o.data.materials) for o in objs}
        nice = {}
        def look(m):
            if m is None:
                return None
            if m.name not in nice:
                key = next((k for k in self.mats if m.name in (f"id_{k}", k)), None) or getattr(self, "band_of", {}).get(m.name)
                hexes = self.tones.get(key) if key else None
                if hexes is None:
                    # A banded material: show the first of its two tones; the water's holdout stays clear.
                    key = next((k for k in self.tones if k in m.name), None)
                    hexes = self.tones.get(key, ("#808080", "#a0a0a0", "#c0c0c0"))
                c = hexrgb(hexes[min(1, len(hexes) - 1)]) / 255
                lin = tuple(float(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92) for v in c)
                nm = bpy.data.materials.new(m.name + "_look")
                nm.use_nodes = True
                nm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*lin, 1)
                nm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
                nm.diffuse_color = (*lin, 1)
                nice[m.name] = nm
            return nice[m.name]
        for o in objs:
            if o.name == "water":
                continue
            mats = list(o.data.materials)
            o.data.materials.clear()
            for m in mats:
                o.data.materials.append(look(m))
        ld = bpy.data.lights.new("sun", "SUN")
        ld.energy = 3
        sun = bpy.data.objects.new("sun", ld)
        sun.rotation_euler = (math.radians(40), 0, math.radians(-135))
        self.scene.collection.objects.link(sun)
        wp = getattr(self, "water_plane", None)
        if wp:
            wp.hide_viewport = True
        bg = self.scene.world.node_tree.nodes["Background"].inputs[1]
        bg.default_value = 1.0       # a lit sky for viewing (the renders here use none)
        bpy.ops.wm.save_as_mainfile(filepath=path, copy=True)
        bg.default_value = 0.0
        if wp:
            wp.hide_viewport = False
        bpy.data.objects.remove(sun, do_unlink=True)
        for o, mats in saved.items():
            o.data.materials.clear()
            for m in mats:
                o.data.materials.append(m)
        self.keep_blend = False      # one file a model, not one a frame
        print("saved", path)

    def render(self, tmp, heights=False):
        """Renders everything in the scene; returns a grid of (material, tone) or None per game pixel, and with
        heights=True also a grid of each pixel's height above the water in rows."""
        self.save_blend()
        ids = self._render(os.path.join(tmp, "_id.png"))
        nrm = self._pass(tmp, "_n.png", self.normal_mat)
        hgt = self._pass(tmp, "_h.png", self._height_material()) if heights else None
        self.last_heights = None
        if heights:
            bh = self._blocks(hgt)
            cov = bh[..., 3] > 0.5
            vals = np.where(cov, bh[..., 0] * 256, 0).sum(axis=2) / np.maximum(cov.sum(axis=2), 1)
            self.last_heights = np.clip(np.rint(vals), 0, 255).astype(int)
        bi, bn = self._blocks(ids), self._blocks(nrm)
        h, w = bi.shape[:2]
        lit, mid = self.lit_at
        grid = [[None] * w for _ in range(h)]
        for j in range(h):
            for i in range(w):
                cov = bi[j, i, :, 3]
                if cov.mean() < 0.5:
                    continue
                k = np.rint(bi[j, i, :, 0] * 64 - 1).astype(int)
                k = k[(cov > 0.5) & (k >= 0) & (k < len(self.mats))]
                if len(k) == 0:
                    continue
                mat = self.mats[np.bincount(k).argmax()]
                n = bn[j, i, cov > 0.5, :3].mean(axis=0) * 2 - 1
                n = n / max(np.linalg.norm(n), 1e-6)
                d = float(n @ LIGHT)
                n_t = len(self.tones.get(mat, (0, 0, 0)))
                if n_t > 3:
                    # More tones (shade to highlight): steps spread between the shade line and full light.
                    steps = [mid + (lit + 0.15 - mid) * k / (n_t - 2) for k in range(n_t - 1)]
                    grid[j][i] = (mat, sum(d > s for s in steps))
                else:
                    grid[j][i] = (mat, 2 if d > lit else 1 if d > mid else 0)
        return grid

    # ---- output
    def colours(self, grids):
        return sorted({self.tones[m][t] for g in grids for row in g for c in row if c for m, t in [c] if m != self.special})

    def rows(self, grid, palette):
        out = []
        for row in grid:
            s = ""
            for c in row:
                if c is None:
                    s += "."
                elif c[0] == self.special:
                    s += SPECIAL_KEYS[c[1]]
                else:
                    s += KEYS[palette.index(self.tones[c[0]][c[1]])]
            out.append(s)
        return out

    def preview(self, path, grids, special_colours=None, scale=8, back=(31, 74, 112)):
        palette = self.colours(grids)
        tiles = []
        for g in grids:
            img = np.zeros((len(g), len(g[0]), 3), dtype=np.float32) + np.array(back)
            for j, row in enumerate(g):
                for i, c in enumerate(row):
                    if c is None:
                        continue
                    hx = special_colours[c[1]] if c[0] == self.special else self.tones[c[0]][c[1]]
                    img[j, i] = hexrgb(hx)
            tiles.append(np.repeat(np.repeat(img, scale, 0), scale, 1))
        hmax = max(t.shape[0] for t in tiles)
        tiles = [np.pad(t, ((0, hmax - t.shape[0]), (0, 8), (0, 0)), constant_values=255) for t in tiles]
        row = np.concatenate(tiles, axis=1)
        im = bpy.data.images.new("prev", row.shape[1], row.shape[0], alpha=True)
        im.colorspace_settings.name = "Non-Color"
        px = np.concatenate([row / 255, np.ones(row.shape[:2] + (1,))], axis=2)[::-1].astype(np.float32)
        im.pixels.foreach_set(px.ravel())
        im.filepath_raw = path
        im.file_format = "PNG"
        im.save()


def args():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
