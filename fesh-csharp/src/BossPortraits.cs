using Raylib_cs;

namespace Fesh;

// The portraits of the fish you fight (on the Fish log's cards, the legends page and the gold card when you land one)
// show the same creature as the fight: its sprite, cropped and drawn large over the portrait's underwater scene, rather
// than the old vector drawing. Ironbill is drawn from its marlin silhouette (IronbillFight.cs), the Mire leviathan, the
// Starfall ray and Haring Buan-buan from their Blender frames, Platejaw from its lit pixels, Tidemane rearing (its mount
// sprite) and Bakunawa rising out of the sea with the moon in its jaws (the Blender serpent's neck and head over a band of
// water, since the model is only what's above the surface). FESH_OLDART=1 keeps the old drawings.
partial class Game
{
    static readonly Dictionary<string, (Color[] px, int w, int h)?> bossSprites = new();

    // The sprite for a portrait id, cropped to what's drawn (null: no sprite, the vector drawing stays).
    internal static (Color[] px, int w, int h)? BossSprite(string artId)
    {
        if (OldArt) return null;
        if (bossSprites.TryGetValue(artId, out var got)) return got;
        (Color[] px, int w, int h)? s = artId switch
        {
            "leviathan" => (MireFrame(MireFrameIndex(10, 0, false)).col, ArapaimaArt.Width, ArapaimaArt.Height),
            "starray" => (RayFrame(RayGlideFrame(-MathF.PI / 2, 1)).col, MantaArt.Width, MantaArt.Height),
            "tarpon" => (TarponFrame(TarponFrameIndex(20, 1, false, 0)).col, TarponArt.Width, TarponArt.Height),
            "lungfish" => (LungFrame(LungFrameIndex(10, 0, false)).col, LungfishArt.Width, LungfishArt.Height),
            "whiskers" => (WhiskFrame(WhiskFrameIndex(10, 0, false)).col, WhiskersArt.Width, WhiskersArt.Height),
            "marlin" => MarlinSprite(-0.32f),
            "platejaw" => (PlatejawPixels(2, 4, 1, false), PjW, PjH),
            "tidemane" => TidemaneSprite(),
            "bakunawa" => BakunawaSprite(),
            _ => StoryPortraitArt.All.TryGetValue(artId, out var story) ? PortraitPixels(story.Palette, story.Rows) : null
        };
        if (s is var (px, w, h)) s = Crop(px, w, h);
        bossSprites[artId] = s;
        return s;
    }

    // The marlin at an angle (nose up for negative a), sail raised, with its dark outline, as DrawMarlin draws it.
    static (Color[] px, int w, int h) MarlinSprite(float a)
    {
        const int R = 28, n = R * 2 + 1;
        var px = new Color[n * n];
        float ca = MathF.Cos(a), sa = MathF.Sin(a);
        int Mat(int x, int y) { float u = x * ca + y * sa, v = -(-x * sa + y * ca); return MarlinAt(u, v, 0.4f, 1); }
        for (int y = -R; y <= R; y++)
            for (int x = -R; x <= R; x++)
            {
                int m = Mat(x, y);
                if (m == 0)
                {
                    if (Mat(x + 1, y) == 0 && Mat(x - 1, y) == 0 && Mat(x, y + 1) == 0 && Mat(x, y - 1) == 0) continue;
                    m = 1;
                }
                px[(y + R) * n + x + R] = MarlinCol[m];
            }
        return (px, n, n);
    }

    // Trims the empty border off a sprite.
    static (Color[] px, int w, int h) Crop(Color[] px, int w, int h)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].A > 0) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        if (x1 < 0) return (px, w, h);
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        var o = new Color[cw * ch];
        for (int y = 0; y < ch; y++) Array.Copy(px, (y + y0) * w + x0, o, y * cw, cw);
        return (o, cw, ch);
    }

    // Tidemane rearing, as the fight draws it: the side view of its sprite with its own outline (no saddle: a wild one).
    static (Color[] px, int w, int h) TidemaneSprite()
    {
        var c = new MountCanvas();
        MountArt.BuildSide(c, new MountPose { Kind = "rear", Time = 0.4f, Seed = 1, Speed = 1 });
        const int W = MountCanvas.W, H = MountCanvas.H;
        var px = new Color[W * H];
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                char m = c.M[y * W + x];
                if (m != '.') { px[y * W + x] = MountArt.Colors[m]; continue; }
                char best = '.';
                int rank = 0;
                void Near(char q) { int r = MountArt.OutlineRank(q); if (r > rank) { rank = r; best = q; } }
                Near(c.M[y * W + x - 1]); Near(c.M[y * W + x + 1]); Near(c.M[(y - 1) * W + x]); Near(c.M[(y + 1) * W + x]);
                if (rank > 0) px[y * W + x] = MountArt.OutlineOf(best);
            }
        return (px, W, H);
    }

    // Bakunawa rising out of the sea at Parola: the neck and head (the humps of its body are left out) with the full moon
    // in its jaws, standing in a band of dark water with a line of foam, since the model is only what's above the surface.
    static (Color[] px, int w, int h) BakunawaSprite()
    {
        var (col, _) = SerpentFrame(0);
        int fw = BakunawaArt.Width, fh = BakunawaArt.Height;
        var full = (Color[])col.Clone();
        var (mx, my) = BakunawaArt.Mouth[0];
        int cx = BakunawaArt.AnchorX + mx, cy = BakunawaArt.AnchorY + my;
        for (int y = -6; y <= 6; y++)
            for (int x = -6; x <= 6; x++)
            {
                int d = x * x + y * y;
                if (d > 36 || cx + x < 0 || cy + y < 0 || cx + x >= fw || cy + y >= fh) continue;
                full[(cy + y) * fw + cx + x] = d > 30 ? Pal.C("#d9c88a") : (x + y) % 5 == 0 && d > 6 ? Pal.C("#efe2b0") : Pal.C("#fff6d0");
            }
        // Keep the neck and head: from a little left of where the neck leaves the sea.
        int x0 = BakunawaArt.AnchorX - 6;
        int top = 0, bottom = 0;
        for (int y = 0; y < fh; y++) for (int x = x0; x < fw; x++) if (full[y * fw + x].A > 0) { bottom = Math.Max(bottom, y); if (top == 0) top = y; }
        // Room either side, for the pool of water it rises from.
        const int pad = 26, below = 9;
        int w = fw - x0 + pad, h = bottom - top + 1 + below;
        var px = new Color[w * h];
        for (int y = top; y <= bottom; y++) for (int x = x0; x < fw; x++) px[(y - top) * w + x - x0 + pad] = full[y * fw + x];
        // The sea round its neck: a flat pool, foam along its top, dark toward its bottom, behind everything already drawn.
        int wl = bottom - top + 1;
        float ex = pad + 12, rx = 34, ry = below - 1;
        for (int y = wl - 3; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - ex) / rx, dy = (y - wl) / ry, d = dx * dx + dy * dy;
                if (d > 1 || px[y * w + x].A > 0) continue;
                bool rim = d > 0.72f || y == wl - 3;
                px[y * w + x] = y < wl - 1 ? Pal.C("#9fd2d6") : rim && (x + y) % 2 == 0 ? Pal.C("#e8f6f6") : Pix.Mix(Pal.C("#3a8a92"), Pal.C("#0c2b3a"), Math.Clamp((y - wl) / (float)below + d * 0.35f, 0, 1));
            }
        return (px, w, h);
    }
}
