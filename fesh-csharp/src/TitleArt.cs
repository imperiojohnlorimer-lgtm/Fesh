using Raylib_cs;

namespace Fesh;

// The title's sea (1.28): under the big "Fesh" there's a band of rolling waves, and every nine seconds a fish leaps out of them,
// arcs over the letters and dives back in on the far side, throwing up spray at both ends. Painted like the rest of the game's
// pixel art (PixelScene), over the world's tour; the title's words stay as they were.
partial class Game
{
    public const int WavesW = 100, WavesH = 30, WavesSurf = 24;

    void PaintTitleWaves(Pix p, float t)
    {
        for (int y = WavesSurf - 2; y < WavesH; y++)
            for (int x = 3; x < WavesW - 3; x++)
            {
                float d = (y - WavesSurf) / 6f;
                float wave = MathF.Sin(x * 0.3f + t * 2.1f + y * 0.7f) * 0.5f + MathF.Sin(x * 0.15f - t * 1.3f) * 0.5f;
                int surf = WavesSurf + (int)MathF.Round(wave * 1.2f);
                if (y < surf) continue;
                var c = y == surf ? Pal.C("#e8f6fb") : y == surf + 1 ? Pal.C("#8fd0e6") : Pix.Mix(Pal.C("#3a9bc0"), Pal.C("#1d5f8e"), Math.Clamp(d, 0, 1));
                if (y > surf + 1 && BayerAt(x, y) < 0.12f * (1 - d)) c = Pal.C("#cdeaf4");
                // The two ends dissolve into the picture behind.
                float edge = Math.Min(x - 3, WavesW - 4 - x) / 12f;
                if (edge < 1 && BayerAt(x, y) > edge) continue;
                p.Buf[y * WavesW + x] = c;
            }
        // The fish: out of the waves at the left, over the letters, back in at the right.
        float ph = (t % 9f) / 2.8f;
        if (ph < 1)
        {
            float fx = 6 + ph * 88, fy = WavesSurf - 1 - 21 * 4 * ph * (1 - ph);
            float vx = 88, vy = -21 * 4 * (1 - 2 * ph);
            float rot = MathF.Atan2(vy, vx), cs = MathF.Cos(rot), sn = MathF.Sin(rot);
            FillPoly(p, new[] { (fx - cs * 3.2f, fy - sn * 3.2f), (fx - cs * 6.4f - sn * 2.2f, fy - sn * 6.4f + cs * 2.2f), (fx - cs * 5.4f, fy - sn * 5.4f), (fx - cs * 6.4f + sn * 2.2f, fy - sn * 6.4f - cs * 2.2f) }, (x, y) => Pal.C("#e87a3a"));
            Blob(p, fx, fy, 4.2f, 2.0f, rot, (l, x, y) => l > 0.25f ? Pal.C("#8fe0e8") : l > -0.15f ? Pal.C("#3fb0c0") : Pal.C("#23788f"));
            Blob(p, fx - sn * 0.8f, fy + cs * 0.8f, 3.0f, 0.8f, rot, (l, x, y) => Pal.C("#e8f4f0"));
            Dot(p, (int)MathF.Round(fx + cs * 2.4f), (int)MathF.Round(fy + sn * 2.4f - 0.4f), Color.Black);
            Dot(p, (int)MathF.Round(fx + cs * 2.4f - 0.4f), (int)MathF.Round(fy + sn * 2.4f - 0.9f), Color.White);
        }
        // Spray where it breaks the surface and where it goes back in: drops thrown up and falling, and a ring on the water.
        foreach (var (sx, start) in new[] { (6f, 0f), (94f, 2.8f) })
        {
            float age = (t % 9f) - start;
            if (age < 0 || age > 1.0f) continue;
            for (int i = 0; i < 9; i++)
            {
                float px = sx + (i - 4) * 2.5f * age, py = WavesSurf + (-22 - (i % 3) * 5) * age + 40 * age * age;
                if (py > WavesSurf + 1) continue;
                Dot(p, (int)MathF.Round(px), (int)MathF.Round(py), Pal.Rgba(232, 246, 251, 1 - age));
            }
            float r = 1 + age * 7;
            for (int a = 0; a < 16; a++) Dot(p, (int)MathF.Round(sx + MathF.Cos(a * 0.39f) * r), (int)MathF.Round(WavesSurf + 1 + MathF.Sin(a * 0.39f) * r * 0.3f), Pal.Rgba(232, 246, 251, 0.8f * (1 - age)));
        }
    }
}
