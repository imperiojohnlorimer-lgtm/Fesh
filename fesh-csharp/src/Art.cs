using System.Numerics;
using Raylib_cs;

namespace Fesh;

sealed class Paint
{
    public float R, G, B, A;
    public Vector2 From, To;
    public (float T, float R, float G, float B, float A)[] Stops;

    public static Paint Of(string s)
    {
        var c = Pal.C(s);
        return new Paint { R = c.R / 255f, G = c.G / 255f, B = c.B / 255f, A = c.A / 255f };
    }

    public static Paint Linear(float x0, float y0, float x1, float y1, params (float t, string col)[] stops) => new()
    {
        A = 1, From = new(x0, y0), To = new(x1, y1),
        Stops = stops.Select(s => { var c = Pal.C(s.col); return (s.t, c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f); }).ToArray()
    };

    public (float r, float g, float b, float a) At(float t)
    {
        t = Math.Clamp(t, 0, 1);
        for (int i = 1; i < Stops.Length; i++)
        {
            if (t > Stops[i].T) continue;
            var a = Stops[i - 1]; var b = Stops[i];
            float k = b.T > a.T ? (t - a.T) / (b.T - a.T) : 0;
            return (a.R + (b.R - a.R) * k, a.G + (b.G - a.G) * k, a.B + (b.B - a.B) * k, a.A + (b.A - a.A) * k);
        }
        var l = Stops[^1];
        return (l.R, l.G, l.B, l.A);
    }
}

// A small antialiased 2D rasterizer with the parts of the canvas API the creature art needs:
// paths, fills, round-capped strokes, linear gradients, clipping and glow shadows.
sealed class VCanvas
{
    public readonly int W, H;
    readonly float[] cr, cg, cb, ca;
    float A = 1, E, F;
    float[] clip;
    public Paint FillStyle = Paint.Of("#000000"), StrokeStyle = Paint.Of("#000000"), ShadowColor;
    public float LineWidth = 1, ShadowBlur;
    public float ShadowScale = 1; // device pixels per logical pixel, so glows keep their size at any window scale
    readonly Stack<(float, float, float, float[], Paint, Paint, float, Paint, float)> stack = new();
    readonly List<List<Vector2>> path = new();
    List<Vector2> cur;

    public VCanvas(int w, int h)
    {
        W = w; H = h;
        cr = new float[w * h]; cg = new float[w * h]; cb = new float[w * h]; ca = new float[w * h];
    }

    public void Save() => stack.Push((A, E, F, clip, FillStyle, StrokeStyle, LineWidth, ShadowColor, ShadowBlur));
    public void Restore() => (A, E, F, clip, FillStyle, StrokeStyle, LineWidth, ShadowColor, ShadowBlur) = stack.Pop();
    public void Translate(float x, float y) { E += A * x; F += A * y; }
    public void Scale(float s) => A *= s;
    Vector2 Tr(float x, float y) => new(A * x + E, A * y + F);

    public void BeginPath() { path.Clear(); cur = null; }
    public void MoveTo(float x, float y) { cur = new List<Vector2> { Tr(x, y) }; path.Add(cur); }
    public void LineTo(float x, float y) { if (cur == null) MoveTo(x, y); else cur.Add(Tr(x, y)); }
    public void ClosePath() { if (cur != null && cur.Count > 1) cur.Add(cur[0]); cur = null; }

    public void QuadTo(float cx, float cy, float x, float y)
    {
        if (cur == null) MoveTo(cx, cy);
        Vector2 p0 = cur[^1], c = Tr(cx, cy), p1 = Tr(x, y);
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f, u = 1 - t;
            cur.Add(u * u * p0 + 2 * u * t * c + t * t * p1);
        }
    }

    public void Arc(float x, float y, float r, float a0, float a1)
    {
        int n = Math.Max(8, (int)(MathF.Abs(a1 - a0) * 12));
        for (int i = 0; i <= n; i++)
        {
            float a = a0 + (a1 - a0) * i / n;
            if (i == 0 && cur == null) MoveTo(x + MathF.Cos(a) * r, y + MathF.Sin(a) * r);
            else LineTo(x + MathF.Cos(a) * r, y + MathF.Sin(a) * r);
        }
    }

    public void Ellipse(float x, float y, float rx, float ry, float rot = 0)
    {
        float cs = MathF.Cos(rot), sn = MathF.Sin(rot);
        for (int i = 0; i < 48; i++)
        {
            float t = i / 48f * MathF.PI * 2, ex = rx * MathF.Cos(t), ey = ry * MathF.Sin(t);
            if (i == 0) MoveTo(x + ex * cs - ey * sn, y + ex * sn + ey * cs);
            else LineTo(x + ex * cs - ey * sn, y + ex * sn + ey * cs);
        }
        ClosePath();
    }

    public void RectPath(float x, float y, float w, float h)
    {
        MoveTo(x, y); LineTo(x + w, y); LineTo(x + w, y + h); LineTo(x, y + h); ClosePath();
    }

    public void Fill() => PaintPolys(path, FillStyle);
    public void Stroke() => PaintPolys(StrokePolys(path, LineWidth * A), StrokeStyle);

    public void Clip()
    {
        var (cov, x0, y0, w, h) = Coverage(path, 0);
        var mask = new float[W * H];
        if (cov != null)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) mask[(y0 + y) * W + x0 + x] = cov[y * w + x];
        if (clip != null) for (int i = 0; i < mask.Length; i++) mask[i] *= clip[i];
        clip = mask;
    }

    void PaintPolys(List<List<Vector2>> polys, Paint p)
    {
        bool shadow = ShadowBlur > 0 && ShadowColor != null && ShadowColor.A > 0;
        int sigma = shadow ? (int)MathF.Ceiling(ShadowBlur * ShadowScale / 2) : 0;
        var (cov, x0, y0, w, h) = Coverage(polys, shadow ? sigma * 3 + 2 : 1);
        if (cov == null) return;
        if (shadow)
        {
            var sh = (float[])cov.Clone();
            for (int k = 0; k < 3; k++) BoxBlur(sh, w, h, sigma);
            ApplyClip(sh, x0, y0, w, h);
            var sc = new Paint { R = ShadowColor.R, G = ShadowColor.G, B = ShadowColor.B, A = ShadowColor.A * (p.Stops == null ? p.A : 1) };
            Composite(sh, x0, y0, w, h, sc);
        }
        ApplyClip(cov, x0, y0, w, h);
        Composite(cov, x0, y0, w, h, p);
    }

    void ApplyClip(float[] cov, int x0, int y0, int w, int h)
    {
        if (clip == null) return;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) cov[y * w + x] *= clip[(y0 + y) * W + x0 + x];
    }

    void Composite(float[] cov, int x0, int y0, int w, int h, Paint p)
    {
        Vector2 g0 = default, g1 = default; float glen2 = 1;
        if (p.Stops != null)
        {
            g0 = Tr(p.From.X, p.From.Y); g1 = Tr(p.To.X, p.To.Y);
            glen2 = Math.Max(1e-6f, (g1 - g0).LengthSquared());
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float c = cov[y * w + x];
                if (c <= 0.0005f) continue;
                float r = p.R, g = p.G, b = p.B, a = p.A;
                if (p.Stops != null)
                {
                    var d = new Vector2(x0 + x + 0.5f, y0 + y + 0.5f) - g0;
                    (r, g, b, a) = p.At(Vector2.Dot(d, g1 - g0) / glen2);
                }
                float sa = Math.Min(1, a * c);
                int i = (y0 + y) * W + x0 + x;
                cr[i] = r * sa + cr[i] * (1 - sa);
                cg[i] = g * sa + cg[i] * (1 - sa);
                cb[i] = b * sa + cb[i] * (1 - sa);
                ca[i] = sa + ca[i] * (1 - sa);
            }
    }

    // Coverage of the polygons (nonzero winding) inside their bounding box, 4 sub-scanlines per pixel
    // with exact horizontal coverage.
    (float[] cov, int x0, int y0, int w, int h) Coverage(List<List<Vector2>> polys, int margin)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        var edges = new List<(float xa, float ya, float xb, float yb, int dir)>();
        foreach (var poly in polys)
        {
            int n = poly.Count;
            if (n < 2) continue;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                minX = Math.Min(minX, a.X); maxX = Math.Max(maxX, a.X); minY = Math.Min(minY, a.Y); maxY = Math.Max(maxY, a.Y);
                if (a.Y == b.Y) continue;
                edges.Add(a.Y < b.Y ? (a.X, a.Y, b.X, b.Y, 1) : (b.X, b.Y, a.X, a.Y, -1));
            }
        }
        if (edges.Count == 0) return (null, 0, 0, 0, 0);
        int x0 = Math.Max(0, (int)MathF.Floor(minX) - margin), y0 = Math.Max(0, (int)MathF.Floor(minY) - margin);
        int x1 = Math.Min(W, (int)MathF.Ceiling(maxX) + margin), y1 = Math.Min(H, (int)MathF.Ceiling(maxY) + margin);
        if (x1 <= x0 || y1 <= y0) return (null, 0, 0, 0, 0);
        int w = x1 - x0, h = y1 - y0;
        var cov = new float[w * h];
        var rowEdges = new List<(float xa, float ya, float xb, float yb, int dir)>();
        var hits = new List<(float x, int dir)>();
        for (int y = y0; y < y1; y++)
        {
            rowEdges.Clear();
            foreach (var e in edges) if (e.ya < y + 1 && e.yb > y) rowEdges.Add(e);
            if (rowEdges.Count == 0) continue;
            int row = (y - y0) * w;
            for (int s = 0; s < 4; s++)
            {
                float sy = y + (s + 0.5f) / 4f;
                hits.Clear();
                foreach (var e in rowEdges)
                    if (sy >= e.ya && sy < e.yb) hits.Add((e.xa + (sy - e.ya) * (e.xb - e.xa) / (e.yb - e.ya), e.dir));
                if (hits.Count < 2) continue;
                hits.Sort((p, q) => p.x.CompareTo(q.x));
                int wind = 0; float start = 0;
                foreach (var (hx, dir) in hits)
                {
                    int before = wind;
                    wind += dir;
                    if (before == 0 && wind != 0) start = hx;
                    else if (before != 0 && wind == 0) AddSpan(cov, row, w, start - x0, hx - x0);
                }
            }
        }
        for (int i = 0; i < cov.Length; i++) if (cov[i] > 1) cov[i] = 1;
        return (cov, x0, y0, w, h);
    }

    static void AddSpan(float[] cov, int row, int w, float xs, float xe)
    {
        xs = Math.Clamp(xs, 0, w); xe = Math.Clamp(xe, 0, w);
        if (xe <= xs) return;
        int i0 = (int)xs, i1 = (int)xe;
        if (i0 == i1) { cov[row + i0] += (xe - xs) * 0.25f; return; }
        cov[row + i0] += (i0 + 1 - xs) * 0.25f;
        for (int i = i0 + 1; i < i1; i++) cov[row + i] += 0.25f;
        if (i1 < w) cov[row + i1] += (xe - i1) * 0.25f;
    }

    static List<List<Vector2>> StrokePolys(List<List<Vector2>> paths, float width)
    {
        float hw = width / 2;
        var res = new List<List<Vector2>>();
        foreach (var pts in paths)
        {
            foreach (var p in pts) res.Add(Circle(p, hw));
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var d = pts[i + 1] - pts[i];
                float len = d.Length();
                if (len < 1e-4f) continue;
                var n = new Vector2(-d.Y, d.X) / len * hw;
                res.Add(Oriented(new List<Vector2> { pts[i] + n, pts[i + 1] + n, pts[i + 1] - n, pts[i] - n }));
            }
        }
        return res;
    }

    static List<Vector2> Circle(Vector2 c, float r)
    {
        int n = Math.Clamp((int)(r * 4), 8, 32);
        var pts = new List<Vector2>(n);
        for (int i = 0; i < n; i++) pts.Add(c + new Vector2(MathF.Cos(i * MathF.Tau / n), MathF.Sin(i * MathF.Tau / n)) * r);
        return Oriented(pts);
    }

    // Every stroke piece gets the same winding so nonzero filling unions them.
    static List<Vector2> Oriented(List<Vector2> pts)
    {
        float area = 0;
        for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; area += a.X * b.Y - b.X * a.Y; }
        if (area < 0) pts.Reverse();
        return pts;
    }

    static void BoxBlur(float[] a, int w, int h, int r)
    {
        if (r <= 0) return;
        var tmp = new float[Math.Max(w, h)];
        float norm = 1f / (2 * r + 1);
        for (int y = 0; y < h; y++)
        {
            float sum = 0;
            for (int x = -r; x <= r; x++) sum += a[y * w + Math.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                tmp[x] = sum * norm;
                sum += a[y * w + Math.Min(w - 1, x + r + 1)] - a[y * w + Math.Max(0, x - r)];
            }
            Array.Copy(tmp, 0, a, y * w, w);
        }
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int y = -r; y <= r; y++) sum += a[Math.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                tmp[y] = sum * norm;
                sum += a[Math.Min(h - 1, y + r + 1) * w + x] - a[Math.Max(0, y - r) * w + x];
            }
            for (int y = 0; y < h; y++) a[y * w + x] = tmp[y];
        }
    }

    public Color[] ToColors()
    {
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++)
        {
            float a = ca[i];
            if (a <= 0) continue;
            px[i] = new Color((byte)Math.Clamp(cr[i] / a * 255 + 0.5f, 0, 255), (byte)Math.Clamp(cg[i] / a * 255 + 0.5f, 0, 255),
                (byte)Math.Clamp(cb[i] / a * 255 + 0.5f, 0, 255), (byte)Math.Clamp(a * 255 + 0.5f, 0, 255));
        }
        return px;
    }
}

// The smooth creature portraits used on the catch card and in the Fesh-dex.
static class CreatureArt
{
    static readonly Paint Sil = Paint.Of("rgba(8,18,32,0.9)");

    static void Circ(VCanvas g, float x, float y, float r) { g.BeginPath(); g.Ellipse(x, y, r, r); g.Fill(); }
    static void Ell(VCanvas g, float x, float y, float rx, float ry, float rot = 0) { g.BeginPath(); g.Ellipse(x, y, rx, ry, rot); g.Fill(); }
    static void Poly(VCanvas g, params float[] xy)
    {
        g.BeginPath();
        g.MoveTo(xy[0], xy[1]);
        for (int i = 2; i < xy.Length; i += 2) g.LineTo(xy[i], xy[i + 1]);
        g.ClosePath();
        g.Fill();
    }
    static void Seg(VCanvas g, float x0, float y0, float x1, float y1) { g.BeginPath(); g.MoveTo(x0, y0); g.LineTo(x1, y1); g.Stroke(); }

    static void Eye(VCanvas g, float x, float y, float r, string col = "#fff")
    {
        g.FillStyle = Paint.Of(col == "#fff" ? "#ffffff" : col); Circ(g, x, y, r);
        g.FillStyle = Paint.Of("#10243a"); Circ(g, x + r * 0.2f, y, r * 0.55f);
        g.FillStyle = Paint.Of("#ffffff"); Circ(g, x + r * 0.35f, y - r * 0.3f, r * 0.2f);
    }

    static int JsRound(float v) => (int)MathF.Floor(v + 0.5f);

    static void Creature(VCanvas g, string id, float cx, float cy, float s, bool sil)
    {
        Paint C(string col) => sil ? Sil : Paint.Of(col);
        g.Save();
        g.Translate(cx, cy);
        g.Scale(s);
        if (id == "glowgill")
        {
            g.FillStyle = C("#2d5f86");
            Poly(g, -24, 0, -42, -14, -36, 0, -42, 14);
            Poly(g, -12, -11, 0, -24, 12, -12);
            Poly(g, -2, 8, -12, 19, 6, 11);
            g.FillStyle = C("#3f7fa8"); Ell(g, 0, 0, 28, 14);
            if (!sil)
            {
                g.FillStyle = Paint.Of("#86c3d6"); Ell(g, 3, 5, 20, 6);
                g.FillStyle = Paint.Of("rgba(16,36,58,0.25)");
                for (int i = 0; i < 5; i++) Circ(g, -18 + i * 6, -4 + (i % 2) * 3, 1.6f);
                g.Save();
                g.StrokeStyle = Paint.Of("#8af6ff"); g.LineWidth = 2.2f; g.ShadowColor = Paint.Of("#5ff0ff"); g.ShadowBlur = 10 * s / 3.4f;
                for (int i = 0; i < 3; i++) { g.BeginPath(); g.Arc(6 - i * 5, 0, 9, -1.0f, 1.0f); g.Stroke(); }
                g.FillStyle = Paint.Of("#bdfaff");
                foreach (var (x, y) in new[] { (-10f, -7f), (-16f, 3f), (-4f, -9f), (-22f, -2f) }) Circ(g, x, y, 1.4f);
                g.Restore();
                Eye(g, 19, -3, 3.4f);
                g.StrokeStyle = Paint.Of("#1d3a52"); g.LineWidth = 1.2f;
                Seg(g, 27.5f, 3, 22, 4);
            }
        }
        else if (id == "tidecrawler")
        {
            g.StrokeStyle = C("#a8461f"); g.LineWidth = 2.6f;
            for (int i = 0; i < 3; i++)
            {
                float lx = -8 + i * 8;
                g.BeginPath(); g.MoveTo(lx, 6); g.LineTo(lx - 5, 16); g.LineTo(lx - 8, 21); g.Stroke();
            }
            g.FillStyle = C("#c4572a"); Poly(g, -18, 0, -38, -12, -33, 0, -38, 12);
            Seg(g, 15, -5, 27, -14);
            Seg(g, 15, 5, 29, 10);
            g.FillStyle = C("#e07a3a"); Ell(g, 32, -16, 7, 5, -0.4f); Ell(g, 34, 11, 6, 4.5f, 0.3f);
            if (!sil)
            {
                g.FillStyle = Paint.Of("#7a2f12");
                Poly(g, 38, -19, 33, -16, 39, -15);
                Poly(g, 40, 9, 35, 11, 40, 13);
            }
            g.StrokeStyle = C("#a8461f"); g.LineWidth = 1.8f;
            Seg(g, 11, -9, 13, -20);
            Seg(g, 17, -8, 21, -18);
            g.FillStyle = C("#d9692b"); Ell(g, 0, 0, 22, 13);
            if (!sil)
            {
                g.Save();
                g.BeginPath(); g.Ellipse(0, 0, 22, 13); g.Clip();
                g.StrokeStyle = Paint.Of("#a8461f"); g.LineWidth = 1.6f;
                for (int i = 0; i < 4; i++) { float x = -12 + i * 8; g.BeginPath(); g.MoveTo(x, -14); g.QuadTo(x + 4, 0, x, 14); g.Stroke(); }
                g.Restore();
                g.FillStyle = Paint.Of("#f19a5c"); Ell(g, -4, -6, 10, 3);
                g.FillStyle = Paint.Of("#1b1b1b"); Circ(g, 13, -21, 2.4f); Circ(g, 21.5f, -19, 2.4f);
                g.FillStyle = Paint.Of("#ffffff"); Circ(g, 13.6f, -21.8f, 0.8f); Circ(g, 22.1f, -19.8f, 0.8f);
            }
            else
            {
                g.FillStyle = Sil; Circ(g, 13, -21, 2.4f); Circ(g, 21.5f, -19, 2.4f);
            }
        }
        else if (id == "hollow_eel")
        {
            var pts = new Vector2[41];
            for (int i = 0; i <= 40; i++) { float t = i / 40f; pts[i] = new(-42 + t * 80, MathF.Sin(t * MathF.PI * 2.2f) * 9); }
            void Path(float oy) { g.BeginPath(); g.MoveTo(pts[0].X, pts[0].Y + oy); for (int i = 1; i < pts.Length; i++) g.LineTo(pts[i].X, pts[i].Y + oy); }
            g.StrokeStyle = C("#4f4373"); g.LineWidth = 4;
            g.BeginPath();
            for (int i = 4; i < 36; i++) { if (i == 4) g.MoveTo(pts[i].X, pts[i].Y - 7); else g.LineTo(pts[i].X, pts[i].Y - 7); }
            g.Stroke();
            g.StrokeStyle = C("#6b5b95"); g.LineWidth = 11; Path(0); g.Stroke();
            var (hx, hy) = (pts[40].X, pts[40].Y);
            g.FillStyle = C("#6b5b95"); Ell(g, hx + 3, hy, 8.5f, 6.5f);
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("#8b7bb8"); g.LineWidth = 2.6f; Path(-2.5f); g.Stroke();
                foreach (var t in new[] { 0.16f, 0.34f, 0.52f, 0.7f, 0.86f })
                {
                    var p = pts[JsRound(t * 40)];
                    g.FillStyle = Paint.Of("#1b1630"); Ell(g, p.X, p.Y + 0.5f, 2.4f, 3);
                    g.StrokeStyle = Paint.Of("#a99bd1"); g.LineWidth = 0.9f;
                    g.BeginPath(); g.Ellipse(p.X, p.Y + 0.5f, 2.4f, 3); g.Stroke();
                }
                g.Save(); g.ShadowColor = Paint.Of("#d9ccff"); g.ShadowBlur = 6 * s / 3.4f;
                Eye(g, hx + 5, hy - 2, 2.4f, "#efe8ff");
                g.Restore();
                g.StrokeStyle = Paint.Of("#2c2445"); g.LineWidth = 1.1f;
                Seg(g, hx + 11, hy + 2, hx + 5, hy + 3);
            }
        }
        else if (id == "mirror_ray")
        {
            g.Translate(0, -7);
            g.StrokeStyle = C("#8d9bb5"); g.LineWidth = 2;
            g.BeginPath(); g.MoveTo(0, 10); g.QuadTo(5, 24, -2, 34); g.Stroke();
            g.FillStyle = sil ? Sil : Paint.Linear(-34, -18, 34, 16, (0, "#f4f7fb"), (0.35f, "#9fb0c8"), (0.55f, "#e6dcf7"), (0.75f, "#a9d6e0"), (1, "#7f8fae"));
            g.BeginPath();
            g.MoveTo(0, -20); g.QuadTo(18, -15, 37, -2); g.QuadTo(16, 6, 0, 14);
            g.QuadTo(-16, 6, -37, -2); g.QuadTo(-18, -15, 0, -20); g.Fill();
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("rgba(255,255,255,0.85)"); g.LineWidth = 1.4f;
                Seg(g, -22, -6, -8, -12);
                Seg(g, 8, -3, 20, -8);
                g.FillStyle = Paint.Of("rgba(42,157,143,0.35)"); Ell(g, 10, 3, 8, 3, -0.3f);
                g.FillStyle = Paint.Of("#2b3550"); Circ(g, -5, -12, 1.6f); Circ(g, 5, -12, 1.6f);
                g.FillStyle = Paint.Of("rgba(255,255,255,0.6)"); Ell(g, 0, -15, 6, 1.5f);
            }
        }
        else if (id == "abyssal")
        {
            g.StrokeStyle = C("#18233a"); g.LineWidth = 3.5f;
            for (int i = 0; i < 6; i++)
            {
                float x = -25 + i * 10;
                g.BeginPath(); g.MoveTo(x, 12); g.QuadTo(x + (i % 2 == 1 ? 7 : -7), 24, x + (i % 2 == 1 ? -2 : 2), 32); g.Stroke();
            }
            g.FillStyle = C("#2a3a5c");
            for (int i = 0; i < 5; i++) Poly(g, -21 + i * 10, -16, -16 + i * 10, -27, -11 + i * 10, -16);
            g.FillStyle = C("#1f2a44"); Ell(g, 0, 0, 38, 20);
            if (!sil)
            {
                g.FillStyle = Paint.Of("rgba(120,150,210,0.14)"); Ell(g, -6, -7, 26, 7);
                var eyes = new (float x, float y, float r)[] { (-22, -4, 3), (-12, -9, 3.6f), (0, -6, 4.6f), (12, -9, 3.6f), (22, -4, 3), (-16, 5, 2.4f), (-5, 7, 2.8f), (6, 7, 2.8f), (17, 5, 2.4f), (29, 2, 2), (-29, 2, 2) };
                foreach (var (x, y, r) in eyes)
                {
                    g.Save(); g.ShadowColor = Paint.Of("#ffd76a"); g.ShadowBlur = 8 * s / 3.4f; g.FillStyle = Paint.Of("#ffd76a"); Circ(g, x, y, r); g.Restore();
                    g.FillStyle = Paint.Of("#3a2a00"); Ell(g, x, y, r * 0.25f, r * 0.7f);
                }
                g.StrokeStyle = Paint.Of("#d9dde3"); g.LineWidth = 0.8f;
                g.BeginPath(); g.MoveTo(25, -13); g.QuadTo(29, -10, 32, -12); g.Stroke();
                g.FillStyle = Paint.Of("#e3e7ec"); Circ(g, 33, -12, 2.6f);
                g.FillStyle = Paint.Of("#aab2bd"); Circ(g, 33, -12, 1.2f);
            }
        }
        else if (id == "coelacanth")
        {
            // Lobed fins on little stalks and a three-part tail, like the real living fossil.
            g.FillStyle = C("#22406b");
            Poly(g, -26, -4, -45, -16, -38, 0);
            Poly(g, -26, 4, -45, 16, -38, 0);
            Ell(g, -45, 0, 6, 3);
            Poly(g, 0, -11, 7, -25, 14, -11);
            Ell(g, -13, -14, 7, 3, -0.5f);
            Ell(g, -13, 14, 7, 3, 0.5f);
            Ell(g, -3, 14, 6, 3, 0.4f);
            g.FillStyle = sil ? Sil : Paint.Linear(0, -13, 0, 13, (0, "#4572ad"), (0.6f, "#2f5384"), (1, "#1f3a63"));
            Ell(g, 2, 0, 31, 13);
            g.FillStyle = C("#2a4a78"); Ell(g, 13, 10, 9, 3.5f, 0.7f);
            if (!sil)
            {
                g.FillStyle = Paint.Of("rgba(235,242,255,0.75)");
                foreach (var (x, y, r) in new[] { (-16f, -4f, 2.2f), (-8, 3, 1.8f), (-2, -6, 2.4f), (6, 2, 1.6f), (12, -6, 1.8f), (-20, 5, 1.5f), (0, 8, 1.4f), (-11, -9, 1.3f) })
                    Ell(g, x, y, r * 1.3f, r);
                g.StrokeStyle = Paint.Of("rgba(10,24,44,0.7)"); g.LineWidth = 1.2f;
                g.BeginPath(); g.MoveTo(18, -9); g.QuadTo(14, 0, 18, 9); g.Stroke();
                Seg(g, 33, 3, 26, 4);
                g.Save(); g.ShadowColor = Paint.Of("#9fe8ff"); g.ShadowBlur = 8 * s / 3.4f; g.FillStyle = Paint.Of("#d8f6ff"); Circ(g, 24, -4, 3.2f); g.Restore();
                g.FillStyle = Paint.Of("#0b1a2c"); Circ(g, 24.6f, -4, 1.7f);
                g.FillStyle = Paint.Of("#ffffff"); Circ(g, 25.4f, -5, 0.6f);
            }
        }
        else if (id == "whiskers")
        {
            // Old Whiskers: an enormous golden carp with big scales and long barbels.
            g.FillStyle = C("#8a5f2a");
            Poly(g, -26, 0, -44, -15, -40, 0, -44, 15);
            Poly(g, -6, -14, 6, -24, 14, -13);
            Ell(g, 4, 14, 6, 3, 0.4f);
            g.FillStyle = sil ? Sil : Paint.Linear(0, -16, 0, 16, (0, "#e0a84a"), (0.55f, "#c98b3a"), (1, "#8a5f2a"));
            Ell(g, 0, 0, 30, 15);
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("rgba(110,70,20,0.55)"); g.LineWidth = 1;
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 5; c++)
                    {
                        float x = -18 + c * 8 + (r % 2) * 4, y = -8 + r * 7;
                        g.BeginPath(); g.MoveTo(x - 3, y - 3); g.QuadTo(x + 2, y, x - 3, y + 3); g.Stroke();
                    }
                g.StrokeStyle = Paint.Of("#6b4a2b"); g.LineWidth = 1.4f;
                g.BeginPath(); g.MoveTo(27, 4); g.QuadTo(36, 10, 40, 22); g.Stroke();
                g.BeginPath(); g.MoveTo(26, 6); g.QuadTo(31, 16, 30, 26); g.Stroke();
                g.FillStyle = Paint.Of("#c26a4a"); Ell(g, 29, 3, 3, 2);
                Eye(g, 21, -5, 3.2f, "#fff3d0");
            }
        }
        else if (id == "lungfish")
        {
            // The Sunscale lungfish: long and eel-like, gold with sun-orange blotches and thread-thin fins.
            g.StrokeStyle = C("#c98b3a"); g.LineWidth = 1.6f;
            g.BeginPath(); g.MoveTo(-6, 8); g.QuadTo(-10, 20, -16, 24); g.Stroke();
            g.BeginPath(); g.MoveTo(8, 8); g.QuadTo(6, 20, 2, 25); g.Stroke();
            g.FillStyle = sil ? Sil : Paint.Linear(-40, 0, 40, 0, (0, "#b5764a"), (0.5f, "#f3c25b"), (1, "#e8a040"));
            g.BeginPath(); g.MoveTo(-46, 0); g.QuadTo(-20, -12, 10, -10); g.QuadTo(34, -9, 40, 0); g.QuadTo(34, 9, 10, 9); g.QuadTo(-20, 11, -46, 0); g.Fill();
            if (!sil)
            {
                g.FillStyle = Paint.Of("rgba(200,90,30,0.55)");
                foreach (var (x, y, r) in new[] { (-30f, -2f, 3f), (-18, 3, 3.5f), (-6, -4, 4), (8, 2, 3.5f), (20, -3, 3) }) Ell(g, x, y, r * 1.3f, r);
                g.FillStyle = Paint.Of("rgba(255,240,180,0.45)"); Ell(g, 0, -6, 28, 2.5f);
                Eye(g, 31, -3, 2.4f, "#fff3d0");
                g.StrokeStyle = Paint.Of("#6b3a1a"); g.LineWidth = 1; Seg(g, 39, 1, 33, 2);
            }
        }
        else if (id == "leviathan")
        {
            // The Mire leviathan: a giant arapaima, armoured green scales fading to red at the tail.
            g.FillStyle = C("#7a2a1e");
            Poly(g, -30, 0, -46, -11, -44, 0, -46, 11);
            Poly(g, -20, -10, -8, -16, -2, -10);
            Poly(g, -20, 10, -8, 16, -2, 10);
            g.FillStyle = sil ? Sil : Paint.Linear(-40, 0, 40, 0, (0, "#a3352a"), (0.35f, "#3f6a3a"), (1, "#2e4f30"));
            g.BeginPath(); g.MoveTo(-32, 0); g.QuadTo(-10, -14, 20, -10); g.QuadTo(40, -6, 42, 2); g.QuadTo(36, 10, 18, 10); g.QuadTo(-10, 13, -32, 0); g.Fill();
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("rgba(20,40,20,0.5)"); g.LineWidth = 1;
                for (int c = 0; c < 7; c++)
                    for (int r = 0; r < 2; r++)
                    {
                        float x = -22 + c * 7 + r * 3, y = -5 + r * 7;
                        g.BeginPath(); g.MoveTo(x - 3, y - 3); g.QuadTo(x + 1, y, x - 3, y + 3); g.Stroke();
                    }
                g.FillStyle = Paint.Of("rgba(200,230,150,0.3)"); Ell(g, 10, -7, 22, 2);
                Eye(g, 31, -2, 2.6f, "#e8e0a0");
                g.StrokeStyle = Paint.Of("#1b2a1b"); g.LineWidth = 1.2f; Seg(g, 42, 3, 34, 4);
            }
        }
        else if (id == "starray")
        {
            // The Starfall ray: a huge manta, deep blue, its back glittering like the night sky.
            g.StrokeStyle = C("#1d2f55"); g.LineWidth = 2;
            g.BeginPath(); g.MoveTo(0, 12); g.QuadTo(6, 24, -4, 34); g.Stroke();
            g.FillStyle = sil ? Sil : Paint.Linear(0, -20, 0, 14, (0, "#2a4a8a"), (1, "#101c3a"));
            g.BeginPath();
            g.MoveTo(0, -18); g.QuadTo(20, -14, 44, -2); g.QuadTo(20, 6, 0, 12);
            g.QuadTo(-20, 6, -44, -2); g.QuadTo(-20, -14, 0, -18); g.Fill();
            g.FillStyle = C("#1d2f55"); Ell(g, -5, -17, 3, 5, -0.4f); Ell(g, 5, -17, 3, 5, 0.4f);
            if (!sil)
            {
                foreach (var (x, y, r) in new[] { (-28f, -3f, 1.2f), (-18, -8, 1f), (-10, -1, 1.4f), (-3, -10, 0.9f), (4, -4, 1.3f), (12, -9, 1f), (19, -2, 1.2f), (28, -5, 1f), (-22, 2, 0.8f), (8, 4, 0.9f), (34, -1, 0.8f) })
                {
                    g.Save(); g.ShadowColor = Paint.Of("#ffe8a0"); g.ShadowBlur = 6 * s / 3.4f; g.FillStyle = Paint.Of("#fff6d0"); Circ(g, x, y, r); g.Restore();
                }
                g.FillStyle = Paint.Of("#e8f0ff"); Circ(g, -6, -12, 1.4f); Circ(g, 6, -12, 1.4f);
            }
        }
        else if (id == "marlin")
        {
            // Ironbill: a black marlin with a raised sail of a dorsal fin, a long notched bill and a crescent tail.
            g.FillStyle = C("#18263f");
            Poly(g, -30, 0, -48, -19, -41, 0, -48, 19);
            g.FillStyle = C("#22365a");
            g.BeginPath(); g.MoveTo(-20, -7); g.QuadTo(-12, -28, 8, -25); g.QuadTo(14, -15, 12, -8); g.ClosePath(); g.Fill();
            Poly(g, 6, 5, -2, 19, 13, 7);
            g.FillStyle = sil ? Sil : Paint.Linear(0, -11, 0, 10, (0, "#16233c"), (0.45f, "#34507c"), (0.7f, "#a9bccf"), (1, "#e8eef4"));
            g.BeginPath(); g.MoveTo(-33, 0); g.QuadTo(-12, -12, 14, -10); g.QuadTo(30, -8, 35, -2); g.QuadTo(30, 7, 14, 8); g.QuadTo(-12, 10, -33, 0); g.Fill();
            g.FillStyle = C("#253552");
            Poly(g, 33, -4, 54, -2.5f, 33, 1);
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("rgba(140,200,255,0.35)"); g.LineWidth = 1.3f;
                for (int i = 0; i < 6; i++) Seg(g, -21 + i * 7, -8, -23 + i * 7, 5);
                g.FillStyle = Paint.Of("rgba(255,255,255,0.18)"); Ell(g, 2, -6, 20, 2);
                g.StrokeStyle = Paint.Of("rgba(235,235,235,0.7)"); g.LineWidth = 0.9f;
                Seg(g, 40, -3.4f, 41, -1.4f); Seg(g, 45, -3, 46, -1.6f);
                Eye(g, 28, -3, 2.4f, "#e8f0ff");
            }
        }
        else if (id == "tarpon")
        {
            // Haring Buan-buan: a huge silver tarpon, its scales outlined like coins, a long trailing dorsal ray and an
            // upturned jaw, with a pale glow of moonlight about it.
            g.FillStyle = C("#8a98a2");
            Poly(g, -30, 0, -46, -16, -40, 0, -46, 16);
            g.BeginPath(); g.MoveTo(-2, -12); g.QuadTo(4, -24, 10, -26); g.QuadTo(-6, -22, -10, -12); g.ClosePath(); g.Fill();
            Poly(g, -6, 10, -14, 18, 4, 11);
            g.FillStyle = sil ? Sil : Paint.Linear(0, -14, 0, 13, (0, "#7f8f9c"), (0.4f, "#c8d4dc"), (0.75f, "#eef3f6"), (1, "#ffffff"));
            g.BeginPath(); g.MoveTo(-32, 0); g.QuadTo(-14, -15, 12, -13); g.QuadTo(30, -10, 36, -3); g.QuadTo(30, 9, 12, 11); g.QuadTo(-14, 13, -32, 0); g.Fill();
            g.FillStyle = C("#6f7f8a");
            Poly(g, 30, -2, 40, -9, 37, 2);
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("rgba(110,130,150,0.55)"); g.LineWidth = 0.8f;
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 8; c++) { g.BeginPath(); g.Arc(-20 + c * 6 + r * 3, -6 + r * 6, 3, -1.6f, 1.6f); g.Stroke(); }
                g.Save(); g.ShadowColor = Paint.Of("#fff6d0"); g.ShadowBlur = 8 * s / 3.4f;
                g.FillStyle = Paint.Of("rgba(255,246,208,0.25)"); Ell(g, 0, -2, 30, 9);
                g.Restore();
                Eye(g, 27, -4, 3.2f);
            }
        }
        else if (id == "bakunawa")
        {
            // Bakunawa: a sea serpent with a dragon's head, its body looping in and out of the waves, fins down its back,
            // and the full moon held in its open jaws.
            g.StrokeStyle = C("#1f4a52"); g.LineWidth = 9;
            g.BeginPath(); g.MoveTo(-50, 14); g.QuadTo(-40, -8, -28, 8); g.QuadTo(-18, 24, -8, 8); g.QuadTo(0, -6, 8, 2); g.Stroke();
            g.StrokeStyle = C("#2f6a6a"); g.LineWidth = 4;
            g.BeginPath(); g.MoveTo(-50, 12); g.QuadTo(-40, -10, -28, 6); g.QuadTo(-18, 22, -8, 6); g.Stroke();
            g.FillStyle = C("#3fa0a0");
            foreach (var (fx, fy) in new[] { (-42f, -4f), (-34f, -2f), (-14f, 16f), (-4f, 2f) }) Poly(g, fx - 3, fy, fx, fy - 7, fx + 3, fy);
            // Neck and head, rearing up on the right.
            g.FillStyle = sil ? Sil : Paint.Linear(0, -30, 0, 10, (0, "#2f6a6a"), (1, "#1a3a40"));
            g.BeginPath(); g.MoveTo(4, 6); g.QuadTo(14, -10, 18, -22); g.LineTo(28, -20); g.QuadTo(22, -6, 14, 8); g.ClosePath(); g.Fill();
            Ell(g, 28, -24, 12, 7, -0.2f);
            g.FillStyle = C("#1a3a40");
            Poly(g, 30, -18, 46, -14, 32, -12);
            g.FillStyle = C("#3fa0a0");
            Poly(g, 18, -30, 14, -40, 22, -31); Poly(g, 24, -31, 24, -42, 29, -31);
            if (!sil)
            {
                // The moon in its jaws.
                g.Save(); g.ShadowColor = Paint.Of("#fff6d0"); g.ShadowBlur = 12 * s / 3.4f;
                g.FillStyle = Paint.Of("#fff6d0"); Circ(g, 40, -16, 6.5f);
                g.Restore();
                g.FillStyle = Paint.Of("rgba(220,210,170,0.6)"); Circ(g, 38, -18, 1.6f); Circ(g, 42, -14, 1.2f);
                g.FillStyle = Paint.Of("#2f6a6a"); Poly(g, 30, -22, 46, -22, 34, -19);
                g.FillStyle = Paint.Of("#ffd76a"); Circ(g, 26, -27, 1.8f);
                g.FillStyle = Paint.Of("#10243a"); Circ(g, 26.4f, -27, 0.8f);
                g.StrokeStyle = Paint.Of("rgba(235,248,252,0.7)"); g.LineWidth = 1.2f;
                foreach (var wx in new[] { -50f, -28f, -8f })
                {
                    g.BeginPath(); g.MoveTo(wx - 8, 18); g.QuadTo(wx, 14, wx + 8, 18); g.Stroke();
                }
            }
        }
        else if (id == "tidemane")
        {
            // Tidemane: a horse's head, neck and forelegs with fin-edged hooves, coral horns, a mane of sea foam,
            // and a long fish tail curling up into a fan.
            g.Translate(0, 4);
            g.StrokeStyle = C("#1d6f68"); g.LineWidth = 12;
            g.BeginPath(); g.MoveTo(-8, 2); g.QuadTo(-22, 16, -34, 4); g.Stroke();
            g.LineWidth = 7;
            g.BeginPath(); g.MoveTo(-31, 7); g.QuadTo(-38, 2, -40, -6); g.Stroke();
            g.FillStyle = C("#5fd6c9");
            Poly(g, -38, -6, -48, -22, -45, -11, -54, -11, -46, -5, -51, 5, -38, 0);
            if (!sil)
            {
                g.StrokeStyle = Paint.Of("rgba(255,255,255,0.45)"); g.LineWidth = 0.8f;
                Seg(g, -40, -4, -47, -18); Seg(g, -40, -4, -51, -10); Seg(g, -40, -3, -48, 3);
            }
            // Forelegs, one reaching forward, each ending in a little fan of fin instead of a hoof.
            g.StrokeStyle = C("#1d6f68"); g.LineWidth = 4.5f;
            g.BeginPath(); g.MoveTo(7, 5); g.QuadTo(10, 14, 6, 21); g.Stroke();
            g.StrokeStyle = C("#2a9d8f");
            g.BeginPath(); g.MoveTo(14, 4); g.QuadTo(20, 12, 21, 20); g.Stroke();
            g.FillStyle = C("#5fd6c9");
            Poly(g, 3, 21, 10, 21, 11, 25, 2, 25);
            Poly(g, 18, 20, 25, 19, 27, 23, 18, 24);
            // Body, belly and dorsal fin.
            g.FillStyle = C("#3fb5a8"); Poly(g, -11, -8, -4, -19, 3, -9);
            g.FillStyle = sil ? Sil : Paint.Linear(0, -11, 0, 11, (0, "#3cb9a9"), (0.6f, "#2a9d8f"), (1, "#1d6f68"));
            Ell(g, 0, 0, 17, 11);
            // Neck and head, angled down toward the muzzle.
            g.BeginPath(); g.MoveTo(5, -6); g.QuadTo(9, -20, 16, -27); g.LineTo(26, -24); g.QuadTo(20, -12, 16, 3); g.ClosePath(); g.Fill();
            Ell(g, 25, -25, 10, 5.5f, 0.45f);
            Ell(g, 31, -20, 5.5f, 4.2f, 0.45f);
            g.StrokeStyle = C("#ff8a7a"); g.LineWidth = 2.2f;
            Seg(g, 19, -29, 17, -34); Seg(g, 17.8f, -31.6f, 14.5f, -33);
            Seg(g, 23, -30, 24, -35); Seg(g, 23.6f, -32.5f, 26.5f, -33.8f);
            if (!sil)
            {
                g.FillStyle = Paint.Of("#8fd3c4"); Ell(g, 2, 6, 13, 4);
                g.FillStyle = Paint.Of("rgba(255,255,255,0.18)"); Ell(g, -2, -6, 11, 3);
                // Starlight speckles down its flanks.
                foreach (var (x, y, r) in new[] { (-9f, -3f, 1.1f), (-4, -6, 0.9f), (1, -2, 1.2f), (6, -5, 0.8f), (-6, 2, 0.8f), (10, -1, 0.9f) })
                {
                    g.Save(); g.ShadowColor = Paint.Of("#ffe8a0"); g.ShadowBlur = 5 * s / 3.4f; g.FillStyle = Paint.Of("#fff6d0"); Circ(g, x, y, r); g.Restore();
                }
                // The mane: a sheet of sea foam streaming back down the neck, scalloped at the edge, with spray coming off it.
                g.Save(); g.ShadowColor = Paint.Of("#bff4ff"); g.ShadowBlur = 6 * s / 3.4f;
                g.FillStyle = Paint.Linear(16, -30, 0, -6, (0, "#ffffff"), (1, "#cfe8ee"));
                g.BeginPath();
                g.MoveTo(17, -31); g.QuadTo(9, -31, 8, -26); g.QuadTo(3, -25, 4, -20); g.QuadTo(-1, -19, 1, -14);
                g.QuadTo(-3, -12, 0, -8); g.QuadTo(-1, -4, 4, -4); g.QuadTo(8, -12, 10, -20); g.QuadTo(13, -26, 17, -31);
                g.Fill();
                g.Restore();
                g.StrokeStyle = Paint.Of("rgba(140,200,220,0.7)"); g.LineWidth = 0.8f;
                g.BeginPath(); g.MoveTo(13, -28); g.QuadTo(7, -22, 5, -14); g.QuadTo(4, -9, 3, -6); g.Stroke();
                g.FillStyle = Paint.Of("#e8f8ff");
                foreach (var (x, y, r) in new[] { (2f, -27f, 1.3f), (-1.5f, -21, 1.1f), (-3, -15, 0.9f), (-3.5f, -9, 0.7f), (5, -31, 0.9f) }) Circ(g, x, y, r);
                g.Save(); g.ShadowColor = Paint.Of("#ffd76a"); g.ShadowBlur = 7 * s / 3.4f; g.FillStyle = Paint.Of("#ffd76a"); Circ(g, 23, -27, 2.2f); g.Restore();
                g.FillStyle = Paint.Of("#3a2a00"); Ell(g, 23.3f, -27, 0.7f, 1.5f);
                g.FillStyle = Paint.Of("#10243a"); Circ(g, 34.6f, -18.5f, 0.9f);
                g.StrokeStyle = Paint.Of("#16514b"); g.LineWidth = 1;
                Seg(g, 29, -16.2f, 33.5f, -15.6f);
            }
        }
        g.Restore();
    }

    // Draws the underwater portrait at logical size w x h onto a canvas that is already scaled to device pixels.
    public static void Scene(VCanvas g, float w, float h, string id, bool sil)
    {
        bool deep = id is "abyssal" or "coelacanth" or "starray" or "leviathan" or "tidemane" or "marlin" or "tarpon" or "bakunawa";
        g.FillStyle = Paint.Linear(0, 0, 0, h, (0, deep ? "#1d3a5a" : "#3a8db0"), (1, deep ? "#050d18" : "#12304a"));
        g.BeginPath(); g.RectPath(0, 0, w, h); g.Fill();
        g.FillStyle = Paint.Of("rgba(255,255,255,0.06)");
        for (int i = 0; i < 3; i++)
        {
            float x = w * (0.15f + i * 0.3f);
            Poly(g, x, 0, x + w * 0.08f, 0, x + w * 0.18f, h, x + w * 0.02f, h);
        }
        if (!deep)
        {
            g.FillStyle = Paint.Of("#b8995e");
            g.BeginPath(); g.MoveTo(0, h); g.QuadTo(w * 0.5f, h - h * 0.16f, w, h - h * 0.07f); g.LineTo(w, h); g.ClosePath(); g.Fill();
            g.StrokeStyle = Paint.Of("#2f6b4a"); g.LineWidth = Math.Max(2, w / 90);
            foreach (var (fx, fh) in new[] { (0.1f, 0.32f), (0.86f, 0.26f), (0.92f, 0.2f) })
            {
                g.BeginPath(); g.MoveTo(w * fx, h); g.QuadTo(w * fx + 8, h - h * fh * 0.5f, w * fx - 3, h - h * fh); g.Stroke();
            }
        }
        g.FillStyle = Paint.Of("rgba(220,240,255,0.35)");
        foreach (var (fx, fy, r) in new[] { (0.2f, 0.3f, 2f), (0.24f, 0.18f, 1.4f), (0.78f, 0.4f, 1.8f), (0.74f, 0.25f, 1.2f) })
            Circ(g, w * fx, h * fy, r * w / 180);
        float s = Math.Min(w / 100, h / 66);
        Creature(g, id, w / 2, h / 2 + (deep ? -2 : 0), s, sil);
    }
}
