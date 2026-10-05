namespace Fesh;

// The app icon: a Glowgill on a sea-blue tile, drawn with 4x4 supersampling.
// It has no Raylib dependency so the icon generator tool can share it.
static class IconArt
{
    public static (byte R, byte G, byte B, byte A)[] Pixels(int size)
    {
        var px = new (byte, byte, byte, byte)[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        var (cr, cg, cb, ca) = Sample((x + (sx + 0.5f) / 4) / size, (y + (sy + 0.5f) / 4) / size);
                        r += cr * ca; g += cg * ca; b += cb * ca; a += ca;
                    }
                px[y * size + x] = a <= 0 ? ((byte)0, (byte)0, (byte)0, (byte)0)
                    : ((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)Math.Round(a / 16 * 255));
            }
        return px;
    }

    static (float, float, float, float) Rgb(int hex, float a = 1) => ((hex >> 16) & 255, (hex >> 8) & 255, hex & 255, a);

    static (float, float, float, float) Sample(float u, float v)
    {
        const float rad = 0.2f;
        float qx = Math.Max(Math.Abs(u - 0.5f) - (0.5f - rad), 0), qy = Math.Max(Math.Abs(v - 0.5f) - (0.5f - rad), 0);
        if (qx * qx + qy * qy > rad * rad) return (0, 0, 0, 0);

        float ex = (u - 0.55f) / 0.27f, ey = (v - 0.52f) / 0.16f;
        bool body = ex * ex + ey * ey < 1;
        float gd = MathF.Sqrt((u - 0.6f) * (u - 0.6f) + (v - 0.52f) * (v - 0.52f));
        float eye = MathF.Sqrt((u - 0.71f) * (u - 0.71f) + (v - 0.48f) * (v - 0.48f));

        if (body)
        {
            if (eye < 0.022f) return Rgb(0x10243a);
            if (eye < 0.042f) return Rgb(0xffffff);
            for (int k = 0; k < 3; k++)
            {
                float cx = 0.56f - k * 0.05f, dx = u - cx, dy = v - 0.52f, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > 0.07f && d < 0.088f && dx > 0.55f * d) return Rgb(0x8af6ff);
            }
            return v > 0.56f ? Rgb(0x86c3d6) : Rgb(0x3f7fa8);
        }
        if (InTri(u, v, 0.1f, 0.3f, 0.1f, 0.74f, 0.32f, 0.52f)) return Rgb(0x2d5f86);
        if (InTri(u, v, 0.43f, 0.39f, 0.55f, 0.22f, 0.66f, 0.39f)) return Rgb(0x2d5f86);
        float glow = MathF.Max(0, 1 - gd / 0.32f) * 0.35f;
        float t = v;
        float br = 0x3a + (0x12 - 0x3a) * t, bg = 0x8d + (0x30 - 0x8d) * t, bb = 0xb0 + (0x4a - 0xb0) * t;
        return (br + (0x8a - br) * glow, bg + (0xf6 - bg) * glow, bb + (0xff - bb) * glow, 1);
    }

    static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
        float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
        float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
}
