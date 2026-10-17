using Raylib_cs;

namespace Fesh;

// 1.34: trees and bushes rendered from Blender models (tools/blender/trees.py, TreeArt.cs). Each sprite is decoded once
// into colours and heights; the crown sways with the wind by height (the trunk stays put), it is mirrored per tile, and
// gets a soft dark outline like the hand-drawn trees had. FESH_TREES=old (or FESH_OLDART=1) draws the procedural trees of
// 1.32; FESH_BUSHES=blender draws the Blender bushes instead of the hand-drawn ones.
partial class Game
{
    static readonly string TreeStyleEnv = Environment.GetEnvironmentVariable("FESH_TREES") ?? "";
    static readonly string BushStyleEnv = Environment.GetEnvironmentVariable("FESH_BUSHES") ?? "";
    static string treeStyle, bushStyle;   // set by the sprite sheet to draw each style in turn
    static bool ModelTrees => treeStyle != null ? treeStyle != "old" : !OldArt && TreeStyleEnv != "old";
    static string BushStyle => bushStyle ?? (OldArt ? "old" : BushStyleEnv is "blender" or "leafy" ? BushStyleEnv : "hand");
    static bool ModelBushes => BushStyle == "blender";

    sealed class TreeSprite
    {
        public int W, H, AX, AY, Top;
        public Color[] Px;        // null alpha = clear
        public byte[] Hgt;        // rows above the ground
        public sbyte[] Special;   // 0..2 for a special (berry) pixel, -1 otherwise
        public Color[] Line;      // the outline's colour per pixel (A = 0 where there is none)
        public float Size;        // how tall it stands, for picking the variant nearest the tree's own size
    }

    static TreeSprite LoadTree(string[] grid, string[] heights, string[] palette, int ax, int ay)
    {
        int h = grid.Length, w = grid[0].Length;
        var s = new TreeSprite { W = w, H = h, AX = ax, AY = ay, Px = new Color[w * h], Hgt = new byte[w * h], Special = new sbyte[w * h], Line = new Color[w * h], Top = h };
        var pal = palette.Select(Pal.C).ToArray();
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                char c = grid[j][i];
                s.Special[j * w + i] = -1;
                if (c == '.') continue;
                s.Top = Math.Min(s.Top, j);
                if (c is 'X' or 'Y' or 'Z') { s.Special[j * w + i] = (sbyte)(c - 'X'); s.Px[j * w + i] = Pal.C("#c0392b"); }
                else s.Px[j * w + i] = pal[c - 'a'];
                s.Hgt[j * w + i] = (byte)((heights[j][i] - 'a') * 2);
            }
        // The outline: every clear pixel beside the sprite takes the darkest neighbour, darker still and a little blue.
        var ink = Pal.C("#0b1d16");
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                if (s.Px[j * w + i].A != 0) continue;
                Color best = default; int lum = 999;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = i + dx, y = j + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h || s.Px[y * w + x].A == 0) continue;
                    var n = s.Px[y * w + x];
                    int l = n.R + n.G + n.B;
                    if (l < lum) { lum = l; best = n; }
                }
                // No outline under the trunk's foot (it stands on the ground).
                if (lum < 999 && j < ay - 1) { var o = Pix.Mix(best, ink, 0.6f); o.A = 215; s.Line[j * w + i] = o; }
            }
        s.Size = ay - s.Top;
        return s;
    }

    static TreeSprite[] oakSprites, palmSprites, firSprites, bushSprites;

    static void LoadTreeSprites()
    {
        if (oakSprites != null) return;
        oakSprites = new[]
        {
            LoadTree(TreeArt.Oak0, TreeArt.Oak0Height, TreeArt.Oak0Palette, TreeArt.Oak0AnchorX, TreeArt.Oak0AnchorY),
            LoadTree(TreeArt.Oak1, TreeArt.Oak1Height, TreeArt.Oak1Palette, TreeArt.Oak1AnchorX, TreeArt.Oak1AnchorY),
            LoadTree(TreeArt.Oak2, TreeArt.Oak2Height, TreeArt.Oak2Palette, TreeArt.Oak2AnchorX, TreeArt.Oak2AnchorY),
            LoadTree(TreeArt.Oak3, TreeArt.Oak3Height, TreeArt.Oak3Palette, TreeArt.Oak3AnchorX, TreeArt.Oak3AnchorY)
        };
        palmSprites = new[]
        {
            LoadTree(TreeArt.Palm0, TreeArt.Palm0Height, TreeArt.Palm0Palette, TreeArt.Palm0AnchorX, TreeArt.Palm0AnchorY),
            LoadTree(TreeArt.Palm1, TreeArt.Palm1Height, TreeArt.Palm1Palette, TreeArt.Palm1AnchorX, TreeArt.Palm1AnchorY),
            LoadTree(TreeArt.Palm2, TreeArt.Palm2Height, TreeArt.Palm2Palette, TreeArt.Palm2AnchorX, TreeArt.Palm2AnchorY),
            LoadTree(TreeArt.Palm3, TreeArt.Palm3Height, TreeArt.Palm3Palette, TreeArt.Palm3AnchorX, TreeArt.Palm3AnchorY)
        };
        firSprites = new[]
        {
            LoadTree(TreeArt.Fir0, TreeArt.Fir0Height, TreeArt.Fir0Palette, TreeArt.Fir0AnchorX, TreeArt.Fir0AnchorY),
            LoadTree(TreeArt.Fir1, TreeArt.Fir1Height, TreeArt.Fir1Palette, TreeArt.Fir1AnchorX, TreeArt.Fir1AnchorY),
            LoadTree(TreeArt.Fir2, TreeArt.Fir2Height, TreeArt.Fir2Palette, TreeArt.Fir2AnchorX, TreeArt.Fir2AnchorY),
            LoadTree(TreeArt.Fir3, TreeArt.Fir3Height, TreeArt.Fir3Palette, TreeArt.Fir3AnchorX, TreeArt.Fir3AnchorY)
        };
        bushSprites = new[]
        {
            LoadTree(TreeArt.Bush0, TreeArt.Bush0Height, TreeArt.Bush0Palette, TreeArt.Bush0AnchorX, TreeArt.Bush0AnchorY),
            LoadTree(TreeArt.Bush1, TreeArt.Bush1Height, TreeArt.Bush1Palette, TreeArt.Bush1AnchorX, TreeArt.Bush1AnchorY),
            LoadTree(TreeArt.Bush2, TreeArt.Bush2Height, TreeArt.Bush2Palette, TreeArt.Bush2AnchorX, TreeArt.Bush2AnchorY),
            LoadTree(TreeArt.Bush3, TreeArt.Bush3Height, TreeArt.Bush3Palette, TreeArt.Bush3AnchorX, TreeArt.Bush3AnchorY)
        };
        foreach (var set in new[] { oakSprites, palmSprites, firSprites }) Array.Sort(set, (a, b) => a.Size.CompareTo(b.Size));
    }

    // The variant nearest a tree's own size: 'rel' is its size against the usual (0.8 to 1.2, smaller up Marilaya's hills).
    static TreeSprite PickTree(TreeSprite[] set, float rel, int tx, int ty)
    {
        // set[0] is the small one (Pip's stall, high on Marilaya's hills), then three ordinary sizes.
        if (rel < 0.78f) return set[0];
        int i = rel < 0.92f ? 1 : rel > 1.08f ? 3 : 2;
        if (rel >= 0.92f && rel <= 1.08f && Pix.Hash(tx, ty, 63) < 0.3) i = Pix.Hash(tx, ty, 64) < 0.5 ? 1 : 3;
        return set[i];
    }

    // Draws a sprite with its foot at (x, y), mirrored if m < 0, the crown shifted by sw at the top and not at all at the
    // trunk. berry: the special pixels' three tones (null leaves them as they are). tint: mixed over every pixel.
    void BlitTree(TreeSprite s, int x, int y, int m, int sw, Color[] berry = null, Color? tint = null, float tintAmt = 0)
    {
        float hMax = Math.Max(1, s.AY - s.Top), h0 = hMax * 0.35f;
        int cx = pix.CamX, cy = pix.CamY;
        for (int j = 0; j < s.H; j++)
        {
            int py = y - s.AY + j - cy;
            if (py < -1 || py > pix.H) continue;
            for (int i = 0; i < s.W; i++)
            {
                int k = j * s.W + i;
                var c = s.Px[k];
                bool line = c.A == 0;
                if (line) { c = s.Line[k]; if (c.A == 0) continue; }
                // A crown pixel's height (an outline pixel takes the row's height above the trunk).
                float hh = line ? s.AY - j : s.Hgt[k];
                int dx = sw == 0 ? 0 : (int)MathF.Round(sw * Math.Clamp((hh - h0) / (hMax - h0), 0, 1));
                if (!line && s.Special[k] >= 0 && berry != null) c = berry[s.Special[k]];
                if (tint is { } t) c = Pix.Mix(c, t, tintAmt);
                int px = (m < 0 ? x - (i - s.AX) : x + (i - s.AX)) + dx - cx;
                pix.Fill(px, py, 1, 1, c);
            }
        }
    }

    static readonly Color JungleTint = Pal.C("#0c2a1c");
    static readonly Color[] BerryRed = { Pal.C("#7a1a16"), Pal.C("#b8302a"), Pal.C("#ff7a6a") },
        BerryCalamansi = { Pal.C("#4e7420"), Pal.C("#7fae2e"), Pal.C("#c8e46a") },
        BerryPicked = { Pal.C("#1d5a38"), Pal.C("#2b7a40"), Pal.C("#4a9a42") };

    // Returns false when the tree kind has no model (the caller draws the old one).
    bool DrawModelTree(int tx, int ty, char kind, int bx, int by, int sw, float rel, int m)
    {
        if (!ModelTrees) return false;
        LoadTreeSprites();
        switch (kind)
        {
            case 't': BlitTree(PickTree(oakSprites, rel, tx, ty), bx, by, m, sw); return true;
            case 'f': BlitTree(PickTree(firSprites, rel, tx, ty), bx, by, m, sw); return true;
            case 'h':
                bool jungle = BiomeAt(tx, ty) == 3;
                BlitTree(PickTree(palmSprites, jungle ? rel * 0.9f : rel, tx, ty), bx, by, m, sw, null, jungle ? JungleTint : null, 0.22f);
                return true;
        }
        return false;
    }

    // 1.34's hand-drawn bush: a mound of overlapping lobes treated as a little height field, every pixel lit by its own
    // slope from the upper left (four leaf tones, dithered), a leafy rim of nubs instead of a smooth curve, leaf clusters
    // that catch the light in patches, a dark hollow at the foot, and berries in small bunches on the side facing you.
    // Four shapes by tile, mirrored, swaying a little at the top. FESH_BUSHES=leafy draws 1.32's.
    static readonly Color[] ShrubLeaf = Tones("#123a1e", "#1c5426", "#2a7030", "#3f8c34", "#62a83c", "#97c84e");

    void DrawShrub(int tx, int ty, int bx, int by)
    {
        // Painted the way hand-made RPG shrubs are: every clump is a ball of little overlapping leaves. Each leaf is a small
        // disc over a darker one set down and right of it (so dark gaps show between leaves and the lower-right rim goes
        // deep), lit by where it sits on its clump's ball (upper left bright yellow-green, lower right deep green), with a
        // fleck of light on its own upper left. Leaves at the rim stick out past the ball, so the outline is bumpy, not drawn.
        float s = 1.2f + 0.2f * (float)Pix.Hash(tx, ty, 63);
        int shape = (int)(Pix.Hash(tx, ty, 64) * 4) % 4, m = Pix.Hash(tx, ty, 65) < 0.5 ? 1 : -1;
        (float ox, float oy, float r)[] lobes = shape switch
        {
            0 => new[] { (0f, -6f, 6.4f) },
            1 => new[] { (-0.4f, -8.6f, 4.6f), (-4.2f, -4.4f, 4.5f), (4f, -4.2f, 4.3f) },
            2 => new[] { (-5.4f, -4f, 4.2f), (5.4f, -4f, 4.2f), (0f, -5.4f, 5.2f) },
            _ => new[] { (1f, -9.6f, 4.2f), (3.8f, -4f, 4f), (-2.4f, -5.2f, 5f) }
        };
        int sw = Sway(tx, ty) / 2;
        pix.Rect(bx - 9 * s, by - 1, (int)(18 * s), 2, TreeShadow);
        pix.Rect(bx - 7 * s + 2, by + 1, (int)(14 * s), 1, TreeShadowSoft);
        float Sw(float y) => sw * Math.Clamp(-y / (12 * s), 0, 1);
        // Back clumps first (the higher ones), then the front ones over them.
        foreach (var (l, li) in lobes.Select((l, i) => (l, i)).OrderBy(p => p.l.oy + p.l.r))
        {
            float cx = bx + l.ox * m * s, cy = by + l.oy * s, r = l.r * s;
            var leaves = new List<(float x, float y, int t)>();
            const float step = 2.0f;
            for (float gy = -r; gy <= r; gy += step)
                for (float gx = -r; gx <= r; gx += step)
                {
                    int ix = (int)MathF.Round(gx * 10), iy = (int)MathF.Round(gy * 10);
                    float jx = gx + ((float)Pix.Hash(tx * 31 + ix, ty * 31 + iy, 330 + li) - 0.5f) * 1.6f;
                    float jy = gy + ((float)Pix.Hash(tx * 31 + ix, ty * 31 + iy, 340 + li) - 0.5f) * 1.6f;
                    float nx = jx / r, ny = jy / (r * 0.92f), dd = nx * nx + ny * ny;
                    if (dd > 1) continue;
                    float nz = MathF.Sqrt(MathF.Max(0, 1 - dd));
                    float lit = -0.55f * nx - 0.65f * ny + 0.55f * nz + ((float)Pix.Hash(tx * 31 + ix, ty * 31 + iy, 350 + li) - 0.5f) * 0.22f;
                    if (cy + jy > by - 3 * s) lit -= 0.2f;             // the foot sits in shade
                    int t = lit < 0.1f ? 1 : lit < 0.38f ? 2 : lit < 0.64f ? 3 : lit < 0.9f ? 4 : 5;
                    leaves.Add((cx + jx, cy + jy, t));
                }
            leaves.Sort((a, b) => a.y.CompareTo(b.y));
            // The shadow discs first, all of them, so each leaf's dark underside shows between the leaves in front.
            foreach (var (x, y, t) in leaves) Disc(x + 0.3f + Sw(y - by), y + 0.4f, 2.4f, ShrubLeaf[0]);   // a dark rim round the whole clump
            foreach (var (x, y, t) in leaves) Disc(x + 0.5f + Sw(y - by), y + 0.8f, 1.7f, ShrubLeaf[Math.Max(0, t - 2)]);
            foreach (var (x, y, t) in leaves)
            {
                float ox = Sw(y - by);
                Disc(x + ox, y, 1.3f, ShrubLeaf[t]);
                if (t >= 2) pix.Rect(x + ox - 1, y - 1, 1, 1, ShrubLeaf[Math.Min(5, t + 1)]);
            }
        }
        if (state.picked.GetValueOrDefault($"{tx},{ty}") == state.day) return;
        // A few berries (or Habagat's calamansi) peeping out between the leaves.
        var berry = BiomeAt(tx, ty) == 6 ? BerryCalamansi : BerryRed;
        foreach (var l in lobes)
            for (int b = 0; b < 2; b++)
            {
                float a = (float)Pix.Hash(tx, ty, 360 + b + (int)(l.ox * 3)) * MathF.PI * 0.9f + 0.1f, d = l.r * s * (0.35f + 0.45f * (float)Pix.Hash(tx, ty, 370 + b + (int)(l.oy * 3)));
                float x = bx + l.ox * m * s + MathF.Cos(a) * d, y = by + l.oy * s + MathF.Sin(a) * d * 0.9f;
                x += Sw(y - by);
                pix.Rect(x, y, 1, 1, berry[2]); pix.Rect(x + 1, y, 1, 1, berry[1]); pix.Rect(x, y + 1, 2, 1, berry[0]);
            }
    }

    bool DrawModelBush(int tx, int ty, int bx, int by)
    {
        if (BushStyle == "hand") { DrawShrub(tx, ty, bx, by); return true; }
        if (!ModelBushes) return false;
        LoadTreeSprites();
        var s = bushSprites[(int)(Pix.Hash(tx, ty, 64) * 4) % 4];
        int m = Pix.Hash(tx, ty, 65) < 0.5 ? 1 : -1;
        pix.Rect(bx - 8, by - 1, 16, 2, TreeShadow);
        bool picked = state.picked.GetValueOrDefault($"{tx},{ty}") == state.day;
        BlitTree(s, bx, by, m, Sway(tx, ty) / 2, picked ? BerryPicked : BiomeAt(tx, ty) == 6 ? BerryCalamansi : BerryRed);
        return true;
    }
}
