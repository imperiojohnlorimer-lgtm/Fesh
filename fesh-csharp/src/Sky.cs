using Raylib_cs;

namespace Fesh;

// Things that fly over the islands. They all live in the world (cells and tiles hashed from their coordinates, so nothing is
// stored and nothing is tied to the screen): you walk past a gull that keeps circling where it was, and a butterfly stays
// by its patch of flowers. Looks only, so it uses hashes and never the game's rng.
partial class Game
{
    const int GullCell = 280;

    // Where gull `i` of a cell is just now, which way it's heading and how it's flying. Each one circles a lazy loop that
    // wanders a little, flapping for a while and then gliding.
    (float x, float y, float dx, float dy, float wing, float alt) GullAt(int cx, int cy, int i, float t)
    {
        double h0 = Pix.Hash(cx, cy, 800 + i), h1 = Pix.Hash(cx, cy, 810 + i), h2 = Pix.Hash(cx, cy, 820 + i), h3 = Pix.Hash(cx, cy, 830 + i);
        float ox = (cx + 0.15f + 0.7f * (float)h0) * GullCell, oy = (cy + 0.15f + 0.7f * (float)h1) * GullCell;
        float rx = 50 + 60 * (float)h2, ry = 28 + 34 * (float)h3;
        float w = (0.16f + 0.1f * (float)Pix.Hash(cx, cy, 840 + i)) * (h0 < 0.5 ? 1 : -1);
        float a = t * w + (float)h1 * 6.28f + i * 0.7f;
        // The loop itself drifts, so a gull is never going round the same oval twice.
        float px = ox + MathF.Cos(a) * rx + MathF.Sin(t * 0.05f + (float)h3 * 9) * 24;
        float py = oy + MathF.Sin(a) * ry + MathF.Cos(t * 0.04f + (float)h2 * 9) * 14;
        float dx = -MathF.Sin(a) * rx * w, dy = MathF.Cos(a) * ry * w;
        // Six seconds flapping out of every fifteen or so, otherwise a long glide (wing 0 gliding .. 1 beating).
        float cycle = (t * 0.07f + (float)h0 * 5) % 1;
        float wing = cycle < 0.4f ? 1 : cycle < 0.46f ? 1 - (cycle - 0.4f) / 0.06f : 0;
        float alt = 13 + 5 * MathF.Sin(t * 0.3f + (float)h2 * 6);
        return (px, py, dx, dy, wing, alt);
    }

    // Everything in the sky that you can see: gulls by day, butterflies over the grass.
    void DrawSkyLife(float t)
    {
        if (scene != "world") return;
        DrawDuskBats(t);
        if (Night) return;
        DrawButterflies(t);
        if (Stormy) return;
        DrawGulls(t);
    }

    // At dusk, flying foxes and bats flap out over the islands from their roosts in loose lines, a few cells' worth of
    // them, each flock crossing the view west to east and fading out at the ends of its run.
    void DrawDuskBats(float t)
    {
        float c = state.clock;
        if (c < 17 * 60 + 40 || c > 21 * 60 + 30 || Stormy) return;
        float fadeT = c < 18 * 60 + 15 ? (c - (17 * 60 + 40)) / 35f : c > 21 * 60 ? (21 * 60 + 30 - c) / 30f : 1;
        const int cell = 360;
        int c0x = (int)MathF.Floor((camX - 60f) / cell) - 1, c1x = (int)MathF.Floor((camX + W + 60f) / cell) + 1;
        int c0y = (int)MathF.Floor((camY - 40f) / cell), c1y = (int)MathF.Floor((camY + H + 40f) / cell);
        for (int cy = c0y; cy <= c1y; cy++)
            for (int cx = c0x; cx <= c1x; cx++)
            {
                double h = Pix.Hash(cx, cy, 900);
                if (h > 0.55) continue;
                float run = cell * 2.2f, along = (t * 24 + (float)Pix.Hash(cx, cy, 901) * run) % run;
                float edge = Math.Clamp(MathF.Min(along, run - along) / 60f, 0, 1) * Math.Clamp(fadeT, 0, 1);
                if (edge <= 0.02f) continue;
                float bx0 = cx * cell - cell * 0.6f + along, by0 = (cy + 0.2f + 0.6f * (float)Pix.Hash(cx, cy, 902)) * cell + MathF.Sin(along * 0.025f + (float)h * 8) * 12;
                int n = 3 + (int)(Pix.Hash(cx, cy, 903) * 3);
                for (int i = 0; i < n; i++)
                {
                    float x = bx0 - i * 9 - (i % 2) * 4 + MathF.Sin(t * 1.7f + i * 2) * 2, y = by0 + (i % 3 - 1) * 7 + MathF.Cos(t * 1.3f + i) * 2;
                    if (x < camX - 6 || x > camX + W + 6 || y < camY - 6 || y > camY + H + 6) continue;
                    bool up = (int)((t + i * 0.13f) * 7) % 2 == 0;
                    var col = Pal.Rgba(36, 28, 52, 0.85f * edge);
                    int px = (int)MathF.Round(x), py = (int)MathF.Round(y);
                    pix.Rect(px, py, 1, 1, col);
                    pix.Rect(px - 1, py + (up ? -1 : 0), 1, 1, col); pix.Rect(px + 1, py + (up ? -1 : 0), 1, 1, col);
                    pix.Rect(px - 2, py + (up ? -2 : 1), 1, 1, col); pix.Rect(px + 2, py + (up ? -2 : 1), 1, 1, col);
                }
            }
    }

    void DrawGulls(float t)
    {
        int c0x = (int)MathF.Floor((camX - 130f) / GullCell), c1x = (int)MathF.Floor((camX + W + 130f) / GullCell);
        int c0y = (int)MathF.Floor((camY - 90f) / GullCell), c1y = (int)MathF.Floor((camY + H + 90f) / GullCell);
        var shade = Pal.Rgba(0, 0, 0, 0.11f * (1 - rainAmt));
        for (int cy = c0y; cy <= c1y; cy++)
            for (int cx = c0x; cx <= c1x; cx++)
            {
                if (Pix.Hash(cx, cy, 799) > 0.62) continue;
                int n = Pix.Hash(cx, cy, 798) < 0.4 ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    var (x, y, dx, dy, wing, alt) = GullAt(cx, cy, i, t);
                    if (x < camX - 12 || x > camX + W + 12 || y + 4 < camY - 12 || y - alt - 8 > camY + H + 12) continue;
                    // Not out over the caverns' mouth or the very edge of the map.
                    if (x < 8 || y < 8 || x > PW - 8 || y > PH - 8) continue;
                    pix.Rect(x - 2, y, 5, 1, shade);
                    DrawGull(x, y - alt, dx >= 0 ? 1 : -1, wing, t + i * 1.3f);
                }
            }
    }

    // A gull seen from above and a little to the side, facing dir: a white body with a yellow bill, its wings spread out
    // to either side of the line of flight. Beating (wing 1) they swing between wide and half folded about five times a
    // second; gliding (wing 0) they're held out wide with a slight bow.
    void DrawGull(float fx, float fy, int dir, float wing, float t, float alpha = 1f)
    {
        int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy);
        Color A(string hex) => alpha >= 1 ? Pal.C(hex) : Pal.WithAlpha(Pal.C(hex), alpha);
        var white = A("#f6f3ea"); var shadeW = A("#cfd3d6"); var tip = A("#59626e"); var bill = A("#ecb645"); var far = A("#b9c0c7");
        float beat = MathF.Sin(t * 28f);
        float span = wing > 0.01f ? 4.2f + 1.9f * beat * wing + 0.8f * (1 - wing) : 5.6f + 0.4f * MathF.Sin(t * 0.9f);
        int len = Math.Max(2, (int)MathF.Round(span));
        void P(int ox, int oy, Color c) => pix.Rect(x + dir * ox, y + oy, 1, 1, c);
        // The wing nearer the camera hangs below the body, the far one rises above it; each leans back and its tip is dark.
        for (int j = 1; j <= len; j++)
        {
            int back = (int)MathF.Round(j * (0.35f + 0.25f * (1 - wing)));
            var farC = j > len - 2 ? tip : far;
            var nearC = j > len - 2 ? tip : j % 2 == 0 ? white : shadeW;
            P(1 - back, -j, farC);
            if (j <= len - 2) P(-back, -j, farC);
            P(1 - back, 1 + j, nearC);
            if (j <= len - 2) P(-back, 1 + j, white);
        }
        // The body: back, belly, tail and head with its bill.
        for (int i = -1; i <= 2; i++) P(i, 0, white);
        for (int i = -1; i <= 1; i++) P(i, 1, shadeW);
        P(-2, 0, shadeW); P(-3, 0, shadeW);
        P(3, 0, white); P(4, 0, bill);
        P(3, -1, white);
    }

    /* ---------- Butterflies ---------- */
    // A few flutter over each patch of grass in the daytime, keeping near their own flowers and veering off when you
    // walk up to them.
    void DrawButterflies(float t)
    {
        if (rainAmt > 0.3f) return;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        var shade = Pal.Rgba(0, 0, 0, 0.12f);
        string[] wings = { "#f3c25b", "#ffffff", "#e8939a", "#8fc2f0", "#ee8a3a", "#c9a6ff" };
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                if (Pix.Hash(x, y, 850) > 0.016) continue;
                char tile = map[y, x];
                if (tile is not ('g' or 'j')) continue;
                if (BiomeAt(x, y) is not (0 or 3 or 5 or 7) || BuildAt(x, y) != null) continue;
                double h = Pix.Hash(x, y, 851);
                float a = t * (0.55f + 0.25f * (float)h) + (float)h * 40;
                float bx = x * T + 5 + MathF.Sin(a * 1.3f) * 9 + MathF.Sin(a * 0.37f + (float)h * 5) * 8;
                float by = y * T + 4 + MathF.Cos(a) * 5 + MathF.Sin(a * 0.53f) * 5;
                // Out of your way: pushed gently aside as you come near.
                float dx = bx - player.X, dy = by - 4 - player.Y, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 28 && d > 0.1f) { float k = (28 - d) / 28; bx += dx / d * k * 22; by += dy / d * k * 14; }
                float lift = 7 + MathF.Sin(a * 2.1f) * 2.5f;
                bool open = (int)((t + (float)h * 3) * 9) % 2 == 0;
                var wing = Pal.C(wings[(int)(h * 997) % wings.Length]);
                int px = (int)MathF.Round(bx), py = (int)MathF.Round(by - lift);
                pix.Rect(px - 1, (int)MathF.Round(by) + 1, 3, 1, shade);
                pix.Rect(px, py, 1, 1, Pal.C("#3b2a1d"));
                if (open)
                {
                    pix.Rect(px - 1, py - 1, 1, 2, wing); pix.Rect(px + 1, py - 1, 1, 2, wing);
                    pix.Rect(px - 2, py - 1, 1, 1, Pal.WithAlpha(wing, 0.6f)); pix.Rect(px + 2, py - 1, 1, 1, Pal.WithAlpha(wing, 0.6f));
                }
                else { pix.Rect(px - 1, py, 1, 1, wing); pix.Rect(px + 1, py, 1, 1, wing); }
            }
    }

    /* ---------- Night lights ---------- */
    // Fireflies over Mirewood's jungle and a few over Saltmere's grass after dark, each drifting round its own spot and
    // blinking, with a soft glow. They stay in the world as you walk.
    void DrawNightFireflies(float t, float k)
    {
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                double h = Pix.Hash(x, y, 860);
                byte bi = BiomeAt(x, y);
                bool ok = bi == 3 ? h < 0.035 : bi == 0 && h < 0.012 && map[y, x] is 'g' or 't';
                if (!ok) continue;
                double h2 = Pix.Hash(x, y, 861);
                float a = t * (0.5f + 0.3f * (float)h2) + (float)h2 * 30;
                float fx = x * T + 5 + MathF.Sin(a * 1.1f) * 12 + MathF.Sin(a * 0.31f) * 6, fy = y * T + 3 + MathF.Cos(a * 0.9f) * 8;
                float blink = (float)(0.5 + 0.5 * Math.Sin(t * 3 + h2 * 20)) * k;
                pix.Glow(fx + 0.5, fy + 0.5, 4, Pal.Rgba(200, 255, 120, 0.25f * blink));
                pix.Rect(fx, fy, 1, 1, Pal.Rgba(230, 255, 140, blink));
            }
    }
}

// Underground life (looks only): glints on the ore, drips falling into the pools, and dust drifting in the lamplight.
partial class Game
{
    // Sparkles on ore that catch your light now and then, and drops that fall from the roof into every pool and ring out.
    void DrawCaveSparkles(float t)
    {
        foreach (var n in nodes)
        {
            if (n.Mined) continue;
            float cycle = 4.2f + (float)Pix.Hash(n.X, n.Y, 880) * 3, ph = (t + (float)Pix.Hash(n.X, n.Y, 881) * 9) % cycle;
            if (ph > 0.6f) continue;
            float k = MathF.Sin(ph / 0.6f * MathF.PI);
            int gx = n.X * T + 3 + (int)(Pix.Hash(n.X, n.Y, 882) * 4), gy = n.Y * T + 1 + (int)(Pix.Hash(n.X, n.Y, 883) * 4);
            var c = Pal.Rgba(255, 252, 232, k);
            pix.Rect(gx, gy, 1, 1, c);
            if (k > 0.5f) { var d = Pal.WithAlpha(c, 0.5f); pix.Rect(gx - 1, gy, 1, 1, d); pix.Rect(gx + 1, gy, 1, 1, d); pix.Rect(gx, gy - 1, 1, 1, d); pix.Rect(gx, gy + 1, 1, 1, d); }
        }
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                if (map[y, x] != 'P' || Pix.Hash(x, y, 884) > 0.3) continue;
                float cycle = 3.4f + (float)Pix.Hash(x, y, 885) * 3, ph = (t + (float)Pix.Hash(x, y, 886) * 9) % cycle;
                float px = x * T + 2 + (float)Pix.Hash(x, y, 887) * 6, py = y * T + 3 + (float)Pix.Hash(x, y, 888) * 4;
                if (ph < 0.28f)
                    pix.Rect(px, py - 18 + ph / 0.28f * 18, 1, 2, Pal.Rgba(200, 235, 250, 0.9f));
                else if (ph < 0.9f)
                {
                    float k = (ph - 0.28f) / 0.62f, r = 1 + k * 4;
                    var c = Pal.Rgba(190, 232, 248, 0.6f * (1 - k));
                    pix.Rect(px - r, py, (int)MathF.Round(r * 2) + 1, 1, c);
                    if (k < 0.5f) pix.Rect(px, py - 1, 1, 1, c);
                }
            }
    }

    // Specks of dust turning slowly in the air round you, brightest where your lamp reaches. They live in the world, so
    // you walk through them.
    void DrawCaveDust(float t)
    {
        int cell = 46;
        int c0x = (int)MathF.Floor((camX - 12f) / cell), c1x = (int)MathF.Floor((camX + W + 12f) / cell);
        int c0y = (int)MathF.Floor((camY - 12f) / cell), c1y = (int)MathF.Floor((camY + H + 12f) / cell);
        for (int cy = c0y; cy <= c1y; cy++)
            for (int cx = c0x; cx <= c1x; cx++)
                for (int i = 0; i < 2; i++)
                {
                    double h = Pix.Hash(cx, cy, 890 + i), h2 = Pix.Hash(cx, cy, 892 + i);
                    float a = t * (0.2f + 0.2f * (float)h2) + (float)h * 20;
                    float x = (cx + (float)h) * cell + MathF.Sin(a) * 7, y = (cy + (float)h2) * cell + MathF.Cos(a * 0.8f) * 5 + MathF.Sin(a * 0.3f) * 6;
                    float near = Dist(x, y, player.X, player.Y - 6);
                    float lit = Math.Clamp(1 - near / (Wears("headlamp") ? 110 : 70), 0, 1);
                    if (lit <= 0.05f) continue;
                    float tw = 0.5f + 0.5f * MathF.Sin(t * 1.7f + (float)h * 30);
                    pix.Rect(x, y, 1, 1, Pal.Rgba(230, 226, 214, 0.55f * lit * tw));
                }
    }
}

// Depth: trees throw a shadow over the ground to the east, and the edges of the view fall away a little in the dark.
partial class Game
{
    // Drawn on the ground before anything stands on it, so a shadow never lies over someone drawn earlier. It leans away
    // from the sun (low in the west): short at noon, long at dawn and dusk, gone at night and softer in the rain.
    void DrawTreeShadows()
    {
        float strength = (1 - Darkness) * (1 - 0.6f * rainAmt);
        if (strength <= 0.05f || scene != "world") return;
        var (dusk, dawn) = SunGlow();
        float lean = 1 + 1.6f * MathF.Max(dusk, dawn);
        var c = Pal.Rgba(10, 34, 36, 0.15f * strength);
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        foreach (var tr in trees)
        {
            if (tr.x < vx0 - 1 || tr.x > vx1 + 1 || tr.y < vy0 || tr.y > vy1 + 3 || tr.kind is not ('t' or 'h' or 'f')) continue;
            float s = 0.9f + 0.2f * (float)Pix.Hash(tr.x, tr.y, 61);
            if (tr.x >= MlStart) s *= 1 - 0.3f * Math.Clamp((MlHeight(MlLx(tr.x * T + 5), MlLy(tr.y * T + 5)) - 0.28f) / 0.35f, 0, 1);
            float rx = (tr.kind == 't' ? 11 : tr.kind == 'f' ? 8 : 9) * s * TreeScale(tr.kind), ry = (tr.kind == 'h' ? 3.6f : 4.8f) * s * TreeScale(tr.kind);
            float cx = tr.x * T + 5 + (tr.kind == 'h' ? 5 : 6) * lean * TreeScale(tr.kind), cy = tr.y * T + 9 + 1.5f * lean * TreeScale(tr.kind);
            for (int dy = (int)-ry; dy <= (int)ry; dy++)
            {
                float half = rx * MathF.Sqrt(MathF.Max(0, 1 - dy * dy / (ry * ry)));
                // A stepped edge: every other row stops a pixel short, so it dithers into the ground rather than being a hard oval.
                int w = (int)MathF.Round(half * 2) - (dy % 2 == 0 ? 0 : 1);
                if (w > 0) pix.Rect(cx - w / 2f, cy + dy, w, 1, c);
            }
        }
    }

    // The corners and edges of the view fall away into the dark, so the eye stays in the middle.
    static float[] vignette;
    void ApplyVignette(float strength)
    {
        if (vignette == null || vignette.Length != W * H)
        {
            vignette = new float[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float nx = (x - W / 2f) / (W / 2f), ny = (y - H / 2f) / (H / 2f);
                    float r = MathF.Sqrt(nx * nx * 0.8f + ny * ny * 1.1f);
                    float k = Math.Clamp((r - 0.62f) / 0.62f, 0, 1);
                    vignette[y * W + x] = k * k * (3 - 2 * k);
                }
        }
        var edge = new Color(6, 12, 26, 255);
        for (int i = 0; i < vignette.Length; i++)
            if (vignette[i] > 0.01f) pix.Buf[i] = Pix.Mix(pix.Buf[i], edge, vignette[i] * strength);
    }
}

partial class Game
{
    // How much taller than the first drawings the trees stand (their collision tile is unchanged).
    internal static float TreeScale(char kind) => kind switch { 't' => 1.5f, 'h' => 1.45f, 'f' => 1.4f, 'c' => 1.5f, _ => 1f };
}

partial class Game
{
    // v modulo m, always in 0..m (the % operator keeps the sign).
    static double Wrap(double v, double m) { v %= m; return v < 0 ? v + m : v; }
}

// Baga's plume (1.27): puffs that boil up out of the crater, slow as they cool, widen and thin as they mix with the air,
// and are carried west by the winds higher up, the way a real plume bends. Each puff is worked out from the time alone (a hash
// per puff), so nothing is stored. Drawn as soft dithered blobs, lit from the upper left, and underlit red by the lava at night.
partial class Game
{
    void DrawPlume(float top, int level, bool ash)
    {
        float interval = level == 0 ? 1.1f : level == 1 ? 0.75f : 0.45f, life = level == 0 ? 9 : 12;
        float power = level == 0 ? 0.8f : level == 1 ? 1f : 1.45f;
        int latest = (int)MathF.Floor(time / interval), count = (int)(life / interval) + 1;
        float night = Darkness;
        // The occasional burst at Level 2 and up: a fast column that punches well above the steady plume, then the usual drift.
        float burstClock = time % 17f;
        for (int n = latest - count; n <= latest; n++)
        {
            double h1 = Pix.Hash(n, 1, 520), h2 = Pix.Hash(n, 2, 520), h3 = Pix.Hash(n, 3, 520);
            float age = time - n * interval;
            if (age < 0 || age > life) continue;
            DrawPuff(n, h1, h2, h3, age, life, power, top, ash, night, false);
        }
        if (level >= 2 && burstClock < 5.5f)
            for (int j = 0; j < 9; j++)
            {
                float age = burstClock - j * 0.16f;
                if (age < 0) continue;
                DrawPuff(900 + j + (int)(time / 17f) * 13, Pix.Hash(j, 5, 521), Pix.Hash(j, 6, 521), Pix.Hash(j, 7, 521), age, 6f, power * 1.8f, top, true, night, true);
            }
    }

    void DrawPuff(int n, double h1, double h2, double h3, float age, float life, float power, float top, bool ash, float night, bool burst)
    {
        float k = age / life;
        // Up fast at first, then slower as it cools; sideways more and more with height (the wind aloft blows west).
        float rise = (burst ? 44 : 38) * power * (1 - MathF.Exp(-age * (burst ? 0.9f : 0.3f)));
        float drift = -(rise * 0.25f + age * age * 0.05f * (0.6f + (float)h1)) - 0.8f * age;
        float turb = MathF.Sin(age * 1.6f + (float)h1 * 6.3f) * (1 + 3 * k) + MathF.Sin(age * 0.8f + (float)h2 * 6.3f) * 1.5f * k;
        float x = ConeX + ((float)h2 - 0.5f) * 5 + drift + turb, y = top - 3 - rise;
        float r = (3.4f + k * (burst ? 17 : 14) * (0.8f + 0.5f * (float)h3)) * (0.7f + 0.3f * power);
        float a = Math.Min(1, age / (life * 0.1f)) * MathF.Pow(1 - k, 0.8f) * (burst ? 0.98f : 0.9f);
        if (a < 0.03f) return;
        bool burstOrLava = night > 0.3f;
        int rr = (int)MathF.Ceiling(r);
        for (int dy = -rr; dy <= rr; dy++)
            for (int dx = -rr; dx <= rr; dx++)
            {
                // A slightly lumpy blob, not a circle: the radius swells and shrinks round the edge.
                float ang = MathF.Atan2(dy, dx), lump = 1 + 0.18f * MathF.Sin(ang * 3 + (float)h1 * 9) + 0.1f * MathF.Sin(ang * 5 + (float)h3 * 9);
                float d = MathF.Sqrt(dx * dx + dy * dy) / (r * lump);
                if (d >= 1) continue;
                float cover = a * (1 - d * d);
                int px = (int)MathF.Round(x) + dx, py = (int)MathF.Round(y) + dy;
                // The body of a puff is solid and soft; only its thinning edge is dithered away.
                if (cover < 0.4f && cover <= BayerAt(px, py) * 0.8f) continue;
                // Lit from the upper left, shaded under and to the right, thinner and paler at the edge; red from below at night.
                float lit = (-dx * 0.55f - dy * 0.8f) / MathF.Max(1, r);
                Color baseC = ash ? Pal.Rgba(104, 98, 96, 255) : Pal.Rgba(228, 234, 230, 255);
                Color c = lit > 0.25f ? Pix.Mix(baseC, Color.White, ash ? 0.25f : 0.5f) : lit < -0.3f ? Pix.Mix(baseC, Pal.Rgba(34, 36, 44, 255), 0.35f) : baseC;
                if (night > 0.3f && dy > 0 && burstOrLava) c = Pix.Mix(c, Pal.Rgba(230, 100, 50, 255), 0.35f * night * (1 - k) * Math.Clamp(dy / MathF.Max(1, r), 0, 1));
                pix.Rect(px, py, 1, 1, Pal.WithAlpha(c, Math.Min(0.88f, 0.35f + cover)));
            }
    }
}
