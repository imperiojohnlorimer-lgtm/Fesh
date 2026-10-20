using Raylib_cs;

namespace Fesh;

// Colour grade (1.39): the colour of the day. One pass over the live view, after the scene is drawn and before the night
// is laid on it, so lamps and glows keep their true colour. It replaces the flat blue rain tint and the flat orange sunset
// fill (they stay for Light: Off). The time of day and the weather are the same everywhere; each island adds a faint tint
// of its own, from a soft field over the world (never from where the player stands, so riding along a coast or the
// title's tour can't recolour a view). It runs on the live picture only, so the baked ground and the chart are untouched.
partial class Game
{
    internal readonly record struct GradeKey(float Min, float R, float G, float B, float Sat, float LR, float LG, float LB);

    // Multipliers on red, green and blue, a saturation and a small lift (0-255 units), through the day. Night is nearly
    // neutral: the dark (ApplyDark) already does the darkening and the blue.
    static readonly GradeKey[] GradeKeys =
    {
        new(5 * 60, 0.98f, 1.00f, 1.02f, 0.98f, 0, 0, 0),         // before dawn
        new(6 * 60, 1.04f, 0.98f, 1.00f, 0.96f, 2, 0, 1),         // dawn
        new(9 * 60, 1.02f, 1.00f, 0.98f, 1.00f, 0, 0, 0),         // morning
        new(12 * 60, 1.00f, 1.01f, 1.02f, 1.00f, 0, 0, 0),        // noon
        new(17 * 60, 1.06f, 1.01f, 0.93f, 0.98f, 1, 0, 0),        // afternoon gold
        new(19 * 60 + 30, 1.13f, 0.96f, 0.82f, 0.94f, 3, 0, 0),   // sunset
        new(21 * 60, 0.98f, 1.00f, 1.02f, 0.98f, 0, 0, 0),        // night
    };
    static readonly GradeKey AmihanKey = new(0, 1.035f, 1.00f, 0.955f, 0.97f, 0, 0, 0), HabagatKey = new(0, 0.975f, 1.03f, 1.005f, 1.05f, 0, 0, 0);
    static readonly GradeKey OvercastKey = new(0, 0.965f, 0.975f, 1.0f, 0.93f, 0, 0, 0);
    static readonly GradeKey RainKey = new(0, 0.85f, 0.89f, 0.95f, 0.88f, 0, 0, 1);
    static readonly GradeKey StormKey = new(0, 0.68f, 0.74f, 0.85f, 0.75f, 0, 1, 3);

    static GradeKey LerpKey(GradeKey a, GradeKey b, float t) => new(a.Min + (b.Min - a.Min) * t,
        a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.Sat + (b.Sat - a.Sat) * t,
        a.LR + (b.LR - a.LR) * t, a.LG + (b.LG - a.LG) * t, a.LB + (b.LB - a.LB) * t);

    // The grade for a time of day (minutes since midnight), without weather.
    internal static GradeKey GradeAt(float clock)
    {
        float c = clock < GradeKeys[0].Min ? clock + 24 * 60 : clock;
        for (int i = 0; i < GradeKeys.Length; i++)
        {
            var a = GradeKeys[i];
            var b = i + 1 < GradeKeys.Length ? GradeKeys[i + 1] : GradeKeys[0] with { Min = GradeKeys[0].Min + 24 * 60 };
            if (c >= a.Min && c < b.Min) return LerpKey(a, b, (c - a.Min) / (b.Min - a.Min));
        }
        return GradeKeys[0];
    }

    // Time, weather and the light setting together. Soft is half as strong as Full.
    internal GradeKey GradeNow()
    {
        var g = GradeAt(state.clock);
        // The season's cast first (a crisper, drier, golden amihan; a greener, wetter habagat), then the weather on top.
        var season = LerpKey(AmihanKey, HabagatKey, SeasonMix());
        g = g with { R = g.R * season.R, G = g.G * season.G, B = g.B * season.B, Sat = g.Sat * season.Sat };
        float rain = Math.Clamp(rainAmt, 0, 1), storm = Math.Clamp(stormAmt, 0, 1);
        g = LerpKey(g, OvercastKey, overShown * (1 - rain) * 0.85f);   // clouds gathering before the rain keys take over (Overcast.cs)
        g = LerpKey(g, RainKey, rain * (1 - storm));
        g = LerpKey(g, StormKey, storm);
        float k = LightMode == 1 ? 0.5f : 1;
        return LerpKey(new GradeKey(0, 1, 1, 1, 1, 0, 0, 0), g, k);
    }

    // The matrix and lift in 8.8 fixed point: out = (m * rgb + lift) >> 8, saturation first, then the multipliers.
    static int[] GradeMatrix(GradeKey g)
    {
        float[] luma = { 0.299f, 0.587f, 0.114f }, mult = { g.R, g.G, g.B }, lift = { g.LR, g.LG, g.LB };
        var m = new int[12];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++) m[i * 4 + j] = (int)MathF.Round(256 * mult[i] * ((i == j ? g.Sat : 0) + (1 - g.Sat) * luma[j]));
            m[i * 4 + 3] = (int)MathF.Round(256 * lift[i]);
        }
        return m;
    }

    static bool GradeIsIdentity(int[] m) => m[0] == 256 && m[1] == 0 && m[2] == 0 && m[3] == 0 && m[4] == 0 && m[5] == 256 && m[6] == 0 && m[7] == 0 && m[8] == 0 && m[9] == 0 && m[10] == 256 && m[11] == 0;

    static Color GradeColor(Color c, int[] m, int fr, int fg, int fb)
    {
        int r = c.R, g = c.G, b = c.B;
        int nr = (m[0] * r + m[1] * g + m[2] * b + m[3]) >> 8, ng = (m[4] * r + m[5] * g + m[6] * b + m[7]) >> 8, nb = (m[8] * r + m[9] * g + m[10] * b + m[11]) >> 8;
        nr = nr * fr >> 8; ng = ng * fg >> 8; nb = nb * fb >> 8;
        return new Color((byte)(nr < 0 ? 0 : nr > 255 ? 255 : nr), (byte)(ng < 0 ? 0 : ng > 255 ? 255 : ng), (byte)(nb < 0 ? 0 : nb > 255 ? 255 : nb), (byte)255);
    }

    /* ---------- The islands' palettes ---------- */
    // A faint multiplier per biome (Data.Biomes order): Saltmere neutral, Frostfang cooler, Sunscald warmer, Mirewood greener,
    // the atoll a touch turquoise, Amihan warm and lush, Habagat sun-bleached, Marilaya earthy.
    static readonly float[][] BiomeTints =
    {
        new[] { 1.00f, 1.00f, 1.00f }, new[] { 0.94f, 0.99f, 1.07f }, new[] { 1.07f, 1.01f, 0.92f }, new[] { 0.93f, 1.03f, 0.97f },
        new[] { 0.96f, 1.04f, 1.06f }, new[] { 1.04f, 1.03f, 0.96f }, new[] { 1.04f, 1.02f, 0.99f }, new[] { 1.04f, 1.00f, 0.95f },
    };
    const int TintBlur = 3;   // tiles each way, applied twice: a coast fades into the sea over about six tiles

    float[] tintField;        // per tile, three multipliers
    char[,] tintFor;          // the world map the field was made from

    static bool TintWater(char c) => c is '~' or 'w';

    // Built once per world: each land tile takes its island's tint, the open sea is neutral, and a box blur (twice) makes
    // every seam gradual.
    internal float[] TintField()
    {
        if (tintField != null && ReferenceEquals(tintFor, worldMap)) return tintField;
        var f = new float[COLS * ROWS * 3];
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                var t = TintWater(worldMap[y, x]) ? BiomeTints[0] : BiomeTints[Math.Min(biome[y, x], BiomeTints.Length - 1)];
                for (int c = 0; c < 3; c++) f[(y * COLS + x) * 3 + c] = t[c];
            }
        var tmp = new float[f.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < ROWS; y++)
                for (int x = 0; x < COLS; x++)
                    for (int c = 0; c < 3; c++)
                    {
                        float s = 0; int n = 0;
                        for (int d = -TintBlur; d <= TintBlur; d++) { int xx = x + d; if (xx < 0 || xx >= COLS) continue; s += f[(y * COLS + xx) * 3 + c]; n++; }
                        tmp[(y * COLS + x) * 3 + c] = s / n;
                    }
            for (int y = 0; y < ROWS; y++)
                for (int x = 0; x < COLS; x++)
                    for (int c = 0; c < 3; c++)
                    {
                        float s = 0; int n = 0;
                        for (int d = -TintBlur; d <= TintBlur; d++) { int yy = y + d; if (yy < 0 || yy >= ROWS) continue; s += tmp[(yy * COLS + x) * 3 + c]; n++; }
                        f[(y * COLS + x) * 3 + c] = s / n;
                    }
        }
        tintFor = worldMap; tintField = f;
        return f;
    }

    // The island tint at a world point, bilinear between tile centres. `strength` 0 to 1 scales how far it strays from neutral.
    internal (int r, int g, int b) TintAt(float wx, float wy, float strength)
    {
        var f = TintField();
        float fx = Math.Clamp(wx / T - 0.5f, 0, COLS - 1.001f), fy = Math.Clamp(wy / T - 0.5f, 0, ROWS - 1.001f);
        int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
        int i00 = (y0 * COLS + x0) * 3, i01 = i00 + 3, i10 = i00 + COLS * 3, i11 = i10 + 3;
        int Ch(int c)
        {
            float a = f[i00 + c], b = f[i01 + c], cc = f[i10 + c], d = f[i11 + c];
            float v = (a + (b - a) * tx) * (1 - ty) + (cc + (d - cc) * tx) * ty;
            return (int)MathF.Round(256 * (1 + (v - 1) * strength));
        }
        return (Ch(0), Ch(1), Ch(2));
    }

    /* ---------- The pass ---------- */
    const int GradeBlock = 4;
    int[] gradeMatrix;        // what this frame's pass used, so a colour planned before it (the moon's path) can be graded the same way
    int[] gradeBlocks;        // per 4 x 4 block of the view, the island tint as three 8.8 multipliers
    int gradeBlocksW;

    // Replaces DrawWeatherTint and DrawSunGlow when light is on. Live outdoor view only.
    void ApplyGrade()
    {
        gradeMatrix = null;
        if (LightMode == 0 || !liveRender || scene != "world") return;
        var m = GradeMatrix(GradeNow());
        float tintK = LightMode == 1 ? 0.5f : 1;
        int bw = (W + GradeBlock - 1) / GradeBlock, bh = (H + GradeBlock - 1) / GradeBlock;
        if (gradeBlocks == null || gradeBlocks.Length != bw * bh * 3) gradeBlocks = new int[bw * bh * 3];
        gradeBlocksW = bw;
        bool tinted = false;
        for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                var (r, g, b) = TintAt(camX + bx * GradeBlock + GradeBlock / 2f, camY + by * GradeBlock + GradeBlock / 2f, tintK);
                int i = (by * bw + bx) * 3;
                gradeBlocks[i] = r; gradeBlocks[i + 1] = g; gradeBlocks[i + 2] = b;
                if (r != 256 || g != 256 || b != 256) tinted = true;
            }
        if (GradeIsIdentity(m) && !tinted) return;
        gradeMatrix = m;
        var buf = pix.Buf;
        int m0 = m[0], m1 = m[1], m2 = m[2], m3 = m[3], m4 = m[4], m5 = m[5], m6 = m[6], m7 = m[7], m8 = m[8], m9 = m[9], m10 = m[10], m11 = m[11];
        for (int y = 0; y < H; y++)
        {
            int row = y * W, brow = (y / GradeBlock) * bw;
            for (int bx = 0; bx < bw; bx++)
            {
                int bi = (brow + bx) * 3, fr = gradeBlocks[bi], fg = gradeBlocks[bi + 1], fb = gradeBlocks[bi + 2];
                int xe = Math.Min(W, bx * GradeBlock + GradeBlock);
                for (int x = bx * GradeBlock; x < xe; x++)
                {
                    var c = buf[row + x];
                    int r = c.R, g = c.G, b = c.B;
                    int nr = ((m0 * r + m1 * g + m2 * b + m3) >> 8) * fr >> 8, ng = ((m4 * r + m5 * g + m6 * b + m7) >> 8) * fg >> 8, nb = ((m8 * r + m9 * g + m10 * b + m11) >> 8) * fb >> 8;
                    buf[row + x] = new Color((byte)(nr < 0 ? 0 : nr > 255 ? 255 : nr), (byte)(ng < 0 ? 0 : ng > 255 ? 255 : ng), (byte)(nb < 0 ? 0 : nb > 255 ? 255 : nb), (byte)255);
                }
            }
        }
        // The moon's sparkles were planned on the ungraded water: their under-colours become what the pass made of them.
        for (int i = 0; i < moonPx.Count; i++) { var mp = moonPx[i]; moonPx[i] = (mp.x, mp.y, GradedAt(mp.under, mp.x, mp.y), mp.a); }
    }

    // A colour from before the pass, as the pass made it at that pixel of the view (the moon's sparkles are planned early).
    Color GradedAt(Color c, int sx, int sy)
    {
        if (gradeMatrix == null) return c;
        int bi = ((sy / GradeBlock) * gradeBlocksW + sx / GradeBlock) * 3;
        return GradeColor(c, gradeMatrix, gradeBlocks[bi], gradeBlocks[bi + 1], gradeBlocks[bi + 2]);
    }
}
