using System.Globalization;
using Raylib_cs;

namespace Fesh;

static class Pal
{
    static readonly Dictionary<string, Color> cache = new();

    // Parses "#rrggbb" or "rgba(r,g,b,a)".
    public static Color C(string s)
    {
        if (cache.TryGetValue(s, out var c)) return c;
        if (s[0] == '#')
        {
            c = new Color(Convert.ToInt32(s.Substring(1, 2), 16), Convert.ToInt32(s.Substring(3, 2), 16), Convert.ToInt32(s.Substring(5, 2), 16), 255);
        }
        else
        {
            var parts = s[(s.IndexOf('(') + 1)..s.IndexOf(')')].Split(',');
            float a = parts.Length > 3 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : 1f;
            c = new Color(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), (int)MathF.Round(a * 255));
        }
        cache[s] = c;
        return c;
    }

    public static Color Rgba(int r, int g, int b, float a) => new(r, g, b, (int)MathF.Round(Math.Clamp(a, 0, 1) * 255));
    public static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, (byte)MathF.Round(Math.Clamp(a, 0, 1) * c.A));

    public static readonly Color Ink = C("#10243a"), Sea = C("#1d4f78"), Sand = C("#e8cf96"), Buoy = C("#e04b3a"),
        Lantern = C("#f3c25b"), Paper = C("#f1e6c8"), PaperInk = C("#2b2420"), Page = C("#0d1b2a");
}

// A pixel buffer where every drawing call is an alpha-blended rectangle, like the canvas fillRect it replaces.
// Rect, Line, Ring and Glow take world coordinates and subtract the camera; Fill takes buffer coordinates.
sealed class Pix
{
    public readonly int W, H;
    public readonly Color[] Buf;
    public int CamX, CamY;
    public float Alpha = 1f;

    public Pix(int w, int h) { W = w; H = h; Buf = new Color[w * h]; }

    // Copies the W x H window of another buffer that starts at (sx, sy), filling anything outside it with bg.
    public void CopyFrom(Pix src, int sx, int sy, Color bg = default)
    {
        bg.A = 255;
        for (int y = 0; y < H; y++)
        {
            int ry = sy + y;
            if (ry < 0 || ry >= src.H) { Array.Fill(Buf, bg, y * W, W); continue; }
            int x0 = Math.Max(0, -sx), x1 = Math.Min(W, src.W - sx);
            if (x0 > 0) Array.Fill(Buf, bg, y * W, Math.Min(W, x0));
            if (x1 > x0) Array.Copy(src.Buf, ry * src.W + sx + x0, Buf, y * W + x0, x1 - x0);
            if (x1 < W) Array.Fill(Buf, bg, y * W + Math.Max(0, x1), W - Math.Max(0, x1));
        }
    }

    static int Round(double v) => (int)Math.Floor(v + 0.5);

    public void Rect(double x, double y, int w, int h, Color c) => Fill(Round(x) - CamX, Round(y) - CamY, w, h, c);
    public void Rect(double x, double y, int w, int h, string c) => Fill(Round(x) - CamX, Round(y) - CamY, w, h, Pal.C(c));

    public void Fill(int x0, int y0, int w, int h, Color c)
    {
        int x1 = Math.Min(W, x0 + w), y1 = Math.Min(H, y0 + h);
        if (x0 < 0) x0 = 0;
        if (y0 < 0) y0 = 0;
        float a = c.A / 255f * Alpha;
        if (a <= 0.001f || x0 >= x1 || y0 >= y1) return;
        if (a >= 0.999f)
        {
            var solid = new Color(c.R, c.G, c.B, (byte)255);
            for (int y = y0; y < y1; y++) Array.Fill(Buf, solid, y * W + x0, x1 - x0);
            return;
        }
        for (int y = y0; y < y1; y++)
            for (int i = y * W + x0, end = y * W + x1; i < end; i++) Buf[i] = Mix(Buf[i], c, a);
    }

    public static Color Mix(Color dst, Color src, float a) => new(
        (byte)(src.R * a + dst.R * (1 - a) + 0.5f),
        (byte)(src.G * a + dst.G * (1 - a) + 0.5f),
        (byte)(src.B * a + dst.B * (1 - a) + 0.5f),
        (byte)255);

    public void Line(double x0d, double y0d, double x1d, double y1d, string col)
    {
        var c = Pal.C(col);
        int x0 = Round(x0d), y0 = Round(y0d), x1 = Round(x1d), y1 = Round(y1d);
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        for (int i = 0; i < 400; i++)
        {
            Fill(x0 - CamX, y0 - CamY, 1, 1, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // A flat dotted ellipse, used for ripples.
    public void Ring(double cx, double cy, double r, Color c)
    {
        int steps = Math.Max(8, Round(r * 5));
        for (int i = 0; i < steps; i++)
        {
            double a = (double)i / steps * Math.PI * 2;
            Fill(Round(cx + Math.Cos(a) * r * 1.6) - CamX, Round(cy + Math.Sin(a) * r * 0.7) - CamY, 1, 1, c);
        }
    }

    // A radial gradient from color c (with its alpha) at the center to transparent at radius r.
    public void Glow(double wx, double wy, double r, Color c)
    {
        double cx = wx - CamX, cy = wy - CamY;
        int x0 = Math.Max(0, (int)Math.Floor(cx - r)), x1 = Math.Min(W, (int)Math.Ceiling(cx + r));
        int y0 = Math.Max(0, (int)Math.Floor(cy - r)), y1 = Math.Min(H, (int)Math.Ceiling(cy + r));
        float a0 = c.A / 255f;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                if (d >= r) continue;
                Buf[y * W + x] = Mix(Buf[y * W + x], c, (float)(a0 * (1 - d / r)));
            }
    }

    public static double Hash(int x, int y, int k)
    {
        unchecked
        {
            int h = (x * 374761393) ^ (y * 668265263) ^ ((k + 1) * 1442695041);
            h = (h ^ (int)((uint)h >> 13)) * 1274126177;
            h ^= (int)((uint)h >> 16);
            return (uint)h / 4294967296.0;
        }
    }
}
