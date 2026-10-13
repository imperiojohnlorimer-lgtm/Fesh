using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Small hand-painted pixel scenes for panels (1.27). The nursery and the cave model used to be flat vector shapes moving in
// straight lines, "a moving slide show". These are painted in a little Pix, a pixel at a time, from the time and a hash (no
// state), and shown scaled up with point filtering, so they have the same grain as the rest of the game: shaded forms, dithered
// backgrounds, wings and shells with real shapes, and motion that eases instead of ticking.
partial class Game
{
    // One texture per picture (not per size): the draw is queued, so a shared texture re-uploaded for the next card would show it on all.
    readonly Dictionary<(int, int, int), (Pix pix, Texture2D tex)> sceneBuf = new();

    // Paints a w x h scene with `paint` (which draws into the Pix it's given) and shows it at (x, y) scaled by `scale`.
    void PixelScene(float x, float y, int w, int h, float scale, Action<Pix> paint, int slot = 0)
    {
        if (!sceneBuf.TryGetValue((w, h, slot), out var b) || b.tex.Id == 0)
        {
            var p = new Pix(w, h);
            b = (p, Gfx.ToTexture(p.Buf, w, h, TextureFilter.Point));
            sceneBuf[(w, h, slot)] = b;
        }
        Array.Fill(b.pix.Buf, default);
        b.pix.CamX = b.pix.CamY = 0;
        paint(b.pix);
        UpdateTexture(b.tex, b.pix.Buf);
        DrawTexturePro(b.tex, new Rectangle(0, 0, w, h), Gfx.S(x, y, w * scale, h * scale), Vector2.Zero, 0, Color.White);
    }

    void ClearScenes()
    {
        foreach (var (_, tex) in sceneBuf.Values) if (tex.Id != 0) UnloadTexture(tex);
        sceneBuf.Clear();
    }

    /* ---------- Drawing helpers ---------- */
    static float Smooth(float a, float b, float v) { float k = Math.Clamp((v - a) / (b - a), 0, 1); return k * k * (3 - 2 * k); }

    // Fills a polygon (even-odd, sampled at pixel centres), colouring each pixel with `shade` (x, y in the pix).
    static void FillPoly(Pix p, (float x, float y)[] pts, Func<int, int, Color> shade)
    {
        float minY = pts.Min(q => q.y), maxY = pts.Max(q => q.y);
        var xs = new List<float>();
        for (int y = Math.Max(0, (int)MathF.Floor(minY)); y <= Math.Min(p.H - 1, (int)MathF.Ceiling(maxY)); y++)
        {
            float cy = y + 0.5f;
            xs.Clear();
            for (int i = 0; i < pts.Length; i++)
            {
                var (ax, ay) = pts[i]; var (bx, by) = pts[(i + 1) % pts.Length];
                if ((ay <= cy && by > cy) || (by <= cy && ay > cy)) xs.Add(ax + (cy - ay) / (by - ay) * (bx - ax));
            }
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
                for (int x = Math.Max(0, (int)MathF.Round(xs[k])); x < Math.Min(p.W, (int)MathF.Round(xs[k + 1])); x++)
                {
                    var c = shade(x, y);
                    if (c.A > 0) p.Buf[y * p.W + x] = c.A == 255 ? c : Pix.Mix(p.Buf[y * p.W + x], c, c.A / 255f);
                }
        }
    }

    // A soft filled ellipse (axis-aligned, or tilted by `rot` radians), shaded from the upper left by `shade(lit)`, lit 1 to -1.
    static void Blob(Pix p, float cx, float cy, float rx, float ry, float rot, Func<float, int, int, Color> shade)
    {
        float cr = MathF.Cos(rot), sr = MathF.Sin(rot);
        int r = (int)MathF.Ceiling(MathF.Max(rx, ry)) + 1;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float u = (dx * cr + dy * sr) / rx, v = (-dx * sr + dy * cr) / ry;
                float d = u * u + v * v;
                if (d > 1) continue;
                int px = (int)MathF.Round(cx) + dx, py = (int)MathF.Round(cy) + dy;
                if (px < 0 || py < 0 || px >= p.W || py >= p.H) continue;
                float lit = -dx * 0.5f / MathF.Max(1, r) - dy * 0.7f / MathF.Max(1, r) + (1 - d) * 0.3f;
                var c = shade(lit, px, py);
                if (c.A > 0) p.Buf[py * p.W + px] = c.A == 255 ? c : Pix.Mix(p.Buf[py * p.W + px], c, c.A / 255f);
            }
    }

    static void Dot(Pix p, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= p.W || y >= p.H) return;
        p.Buf[y * p.W + x] = c.A == 255 ? c : Pix.Mix(p.Buf[y * p.W + x], c, c.A / 255f);
    }

    static void Line(Pix p, float x0, float y0, float x1, float y1, Color c, int thick = 1)
    {
        int n = (int)MathF.Ceiling(MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0))) + 1;
        for (int i = 0; i <= n; i++)
        {
            float k = n == 0 ? 0 : i / (float)n;
            int x = (int)MathF.Round(x0 + (x1 - x0) * k), y = (int)MathF.Round(y0 + (y1 - y0) * k);
            for (int t = 0; t < thick; t++) Dot(p, x + t, y, c);
        }
    }

    // A blurred, bright and dark patchwork of leaves behind a scene, dithered between greens, with a few round highlights.
    static void LeafyBackdrop(Pix p, float time, string top = "#27472b", string bottom = "#7aa656")
    {
        var a = Pal.C(top); var b = Pal.C(bottom);
        for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
            {
                float v = y / (float)p.H + (VNoise(x + 40, y + 70, 14, 700) - 0.5f) * 0.5f + (VNoise(x, y, 5, 701) - 0.5f) * 0.12f + (BayerAt(x, y) - 0.5f) * 0.12f;
                p.Buf[y * p.W + x] = Pix.Mix(a, b, Math.Clamp(v, 0, 1));
            }
        for (int i = 0; i < 7; i++)
        {
            float bx = (float)Pix.Hash(i, 1, 702) * p.W, by = (float)Pix.Hash(i, 2, 702) * p.H * 0.8f, r = 4 + (float)Pix.Hash(i, 3, 702) * 7;
            Blob(p, bx, by, r, r * 0.85f, 0, (l, x, y) => BayerAt(x, y) < 0.28f ? Pal.Rgba(255, 250, 200, 0.2f) : default);
        }
    }

    // A citrus-type leaf: pointed oval with a midrib and veins, glossy on its lit side, drawn tilted by `rot`.
    static void LeafShape(Pix p, float cx, float cy, float len, float wid, float rot, string baseHex, string lightHex, string darkHex, bool serrated = false)
    {
        var bc = Pal.C(baseHex); var lc = Pal.C(lightHex); var dc = Pal.C(darkHex);
        float cr = MathF.Cos(rot), sr = MathF.Sin(rot);
        for (float s = -len / 2; s <= len / 2; s += 0.5f)
        {
            float k = (s + len / 2) / len;
            float half = wid / 2 * MathF.Sin(MathF.PI * MathF.Pow(k, 0.8f)) * (serrated ? 1 + 0.08f * MathF.Sin(s * 3) : 1);
            for (float t = -half; t <= half; t += 0.5f)
            {
                int x = (int)MathF.Round(cx + s * cr - t * sr), y = (int)MathF.Round(cy + s * sr + t * cr);
                float edge = half < 0.5f ? 1 : MathF.Abs(t) / half;
                var c = t < -half * 0.2f ? (edge > 0.7f ? lc : Pix.Mix(bc, lc, 0.45f)) : edge > 0.78f ? dc : bc;
                if (MathF.Abs(t) < 0.5f) c = Pix.Mix(c, Pal.C("#d6e8a0"), 0.55f);   // the midrib
                else if (MathF.Abs(MathF.Abs(t) - half * 0.55f * MathF.Sin(MathF.PI * k)) < 0.4f && (int)(s * 0.4f + 100) % 2 == 0) c = Pix.Mix(c, dc, 0.35f);
                Dot(p, x, y, c);
            }
        }
    }
}
