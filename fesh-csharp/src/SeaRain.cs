using Raylib_cs;

namespace Fesh;

// Rain and storm on the sea (1.40, Light: Soft or Full; Light: Off keeps the old rings in DrawRainSplashes).
//
// Rain lands as small, flat, faint arcs: a pixel or two to a few wide, a pixel high at most, grey-blue and fading as they
// open, the second arc a beat behind. A fishing spot's ring is the opposite (white, round, bigger, and it stays), so rain is
// kept clear of every spot and the bobber, and never drawn on a pier, a dry flat or the land: every arc pixel is tested
// against the exposed water, not just its middle. In a storm the deep water gets short wind-driven strokes, light and dark,
// that drift across their cell and fade, and the old dashes and ripples step aside.
// All of it is drawn in the water layer, before the grade and the night, so nothing glows in the dark.
partial class Game
{
    readonly List<(float x, float y, float r)> rainKeepClear = new();

    bool ExposedWater(int wx, int wy) => Wet(ShapePx(wx, wy)) && !TideDry(wx, wy);

    // The keep-clear list: every open spot near the view, plus the bobber, so rain never reads as a fishing cue.
    void GatherKeepClear()
    {
        rainKeepClear.Clear();
        foreach (var s in Data.Spots)
        {
            if (s.Scene != "world") continue;
            var (sx, sy) = SpotPos(s);
            if (sx > camX - 40 && sx < camX + W + 40 && sy > camY - 40 && sy < camY + H + 40) rainKeepClear.Add((sx, sy, s.R + 10));
        }
        if (fish != null) rainKeepClear.Add((fish.Bx, fish.By, 12));
    }

    bool NearCue(float x, float y)
    {
        foreach (var (cx, cy, r) in rainKeepClear) if (Dist(x, y, cx, cy) < r) return true;
        return false;
    }

    // How hard the sea is being worked: a storm fully, rain a little.
    float ChopK => LightMode > 0 ? Math.Max(Math.Clamp(stormAmt, 0, 1), 0.3f * Math.Clamp(rainAmt, 0, 1)) : 0;

    void DrawSeaRain(float t)
    {
        if (LightMode == 0 || rainAmt <= 0.05f || scene != "world") return;
        GatherKeepClear();
        int n = Math.Min(140, (int)(60 * rainAmt + 70 * stormAmt));
        for (int i = 0; i < n; i++)
        {
            // Each drop has its own beat: it lands, opens for 0.45-0.8 s, and rests before the next.
            float life = 0.45f + 0.35f * (float)Pix.Hash(i, 3, 410), period = life * (1.5f + 1.2f * (float)Pix.Hash(i, 4, 410));
            float u = (t + (float)Pix.Hash(i, 2, 410) * 17) / period;
            int cyc = (int)u;
            float age = (u - cyc) * period / life;
            if (age >= 1) continue;
            // Fixed in the world (the drop stays where it hit), not on the screen.
            int wx = camX + (int)Wrap(Pix.Hash(i, cyc, 411) * W - camX, W), wy = camY + (int)Wrap(Pix.Hash(cyc, i, 412) * H - camY, H);
            if (BiomeAt(wx / T, wy / T) == 1 || !ExposedWater(wx, wy) || NearCue(wx, wy)) continue;
            float a = (1 - age) * 0.2f * rainAmt;
            if (a < 0.02f) continue;
            var c = Pal.Rgba(200, 222, 236, a);
            Arc(wx, wy, 1 + (int)(age * 2.99f), age >= 0.45f, c);
            if (age > 0.3f) Arc(wx, wy, Math.Max(1, (int)((age - 0.3f) * 3)), false, Pal.Rgba(200, 222, 236, a * 0.6f));
        }
    }

    // A flat arc about a point: its two ends, and below the ends once it has opened a little.
    void Arc(int cx, int cy, int rx, bool lower, Color c)
    {
        void P(int x, int y) { if (ExposedWater(x, y)) pix.Rect(x, y, 1, 1, c); }
        P(cx - rx, cy); P(cx + rx, cy);
        if (lower && rx >= 2) { P(cx - rx + 1, cy + 1); P(cx + rx - 1, cy + 1); }
    }

    // Storm chop on the deep water.
    void DrawSeaChop(float t)
    {
        float k = ChopK;
        if (k < 0.1f || scene != "world") return;
        var (wdx, wdy) = SwellDir;
        var light = Pal.Rgba(170, 205, 226, 0.26f * k);
        var dark = Pal.Rgba(8, 36, 66, 0.3f * k);
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                if (map[y, x] != '~' || Pix.Hash(x, y, 420) > 0.12 + 0.34 * k) continue;
                // One stroke a cell, alive for a few seconds, drifting downwind a few pixels as it goes.
                float cycle = 2.2f + (float)Pix.Hash(x, y, 421) * 1.6f, p = ((t + (float)Pix.Hash(x, y, 422) * 11) % cycle) / cycle;
                float fade = MathF.Sin(p * MathF.PI);
                if (fade < 0.2f) continue;
                int gx = x * T + 1 + (int)(Pix.Hash(x, y, 423) * 5) + (int)MathF.Round(wdx * p * 6), gy = y * T + 2 + (int)(Pix.Hash(x, y, 424) * 6) + (int)MathF.Round(wdy * p * 6);
                var col = Pix.Hash(x, y, 425) < 0.55 ? light : dark;
                int len = 2 + (int)(Pix.Hash(x, y, 426) * 2);
                for (int i = 0; i < len; i++)
                {
                    int px = gx + i, py = gy - (i >= 2 ? 1 : 0);
                    if (ShapePx(px, py) == '~') pix.Rect(px, py, 1, 1, Pal.Rgba(col.R, col.G, col.B, col.A / 255f * fade));
                }
            }
    }
}
