using Raylib_cs;

namespace Fesh;

// Small moving touches that make the outdoors feel alive, all looks only (fxRng and hashes, never the game's rng):
// gusts of wind that run across the grass and the trees, waves washing up the beaches, light playing in the shallows,
// glints on the open sea, morning mist, rain splashing, and long beams of light when the sun is low.
partial class Game
{
    /* ---------- Wind ---------- */
    // How hard a gust is blowing at a point just now (0 to 1): broad bands that roll across the islands from the
    // west, stronger and closer together in a storm.
    float Gust(float wx, float wy)
    {
        float speed = Stormy ? 1.6f : 0.75f;
        float band = MathF.Sin(wx * 0.011f + wy * 0.004f - time * speed);
        float g = MathF.Max(0, band);
        g *= g; g *= g;
        float patch = 0.45f + 0.55f * VNoise((int)wx + (int)(time * 18), (int)wy, 70, 390);
        return Math.Clamp(g * patch * (Stormy ? 1.3f : 1), 0, 1);
    }

    /* ---------- Water ---------- */
    // Waves: a faint crest rolls in toward every sea shore and breaks into the foam, and the sand it reached stays dark
    // and wet for a moment as it runs back.
    void DrawWaves(float t)
    {
        int x0 = camX - 10, x1 = camX + W + 10, y0 = camY - 10, y1 = camY + H + 10;
        float night = 1 - 0.6f * Darkness;
        bool rich = LightMode > 0;
        float storm = rich ? Math.Clamp(stormAmt, 0, 1) : 0;
        foreach (int si in ShoreInView())
        {
            var (sx, sy, nx, ny, w) = shore[si];
            if (sx < x0 || sx > x1 || sy < y0 || sy > y1 || w is not ('w' or '~' or 'T') || (nx == 0 && ny == 0)) continue;
            if (TideDry(sx, sy)) continue;
            float p = (t * 0.2f + VNoise(sx, sy, 46, 391) * 1.7f) % 1f;
            // The crest, coming in from about 8 px out.
            if (((sx * 3 + sy) & 1) == 0)
            {
                float off = (1 - p) * 8 + 1;
                int cx = sx + (int)MathF.Round(nx * off), cy = sy + (int)MathF.Round(ny * off);
                if (Wet(ShapePx(cx, cy)))
                {
                    float a = MathF.Sin(p * MathF.PI) * 0.32f * night;
                    // The crests ride the swell, and build with the weather.
                    if (rich) a *= Math.Clamp(1 + 0.35f * SeaSwell(sx, sy, t) + 0.5f * storm, 0, 1.8f);
                    if (a > 0.03f) pix.Rect(cx, cy, 1, 1, Pal.Rgba(236, 248, 252, a));
                }
            }
            // Wet sand behind the wash, drying as the water runs back.
            if (p < 0.35f)
            {
                float a = (0.35f - p) / 0.35f * 0.16f;
                for (int k = 1; k <= 2; k++)
                {
                    int lx = sx - nx * k, ly = sy - ny * k;
                    if (ShapePx(lx, ly) == 's') pix.Rect(lx, ly, 1, 1, Pal.Rgba(92, 66, 32, a / k));
                }
            }
        }
    }

    // Light dancing on the bottom of the shallows and the clear lagoons: soft bright threads that slowly shift,
    // stepped a few times a second so it stays pixel-art. Only by day.
    void DrawCaustics(float t)
    {
        float day = 1 - Darkness, clear = 1 - 0.7f * rainAmt;
        if (day < 0.1f) return;
        float tq = MathF.Floor(t * 6) / 6;
        float a = 0.15f * day * clear;
        var sea = Pal.Rgba(196, 238, 246, a);
        var pond = Pal.Rgba(206, 250, 238, a);
        for (int py = 0; py < H; py++)
        {
            int wy = py + camY;
            if (wy < 0 || wy >= PH) continue;
            for (int px = 0; px < W; px++)
            {
                int wx = px + camX;
                if (wx < 0 || wx >= PW) continue;
                char s = shape[wy * PW + wx];
                if (s is not ('w' or 'l' or 'o')) continue;
                // Where two slow wavy fields cancel out, a thin bright thread: a net of light like the real thing.
                float v = MathF.Sin(wx * 0.31f + tq * 1.1f + MathF.Sin(wy * 0.13f + tq * 0.5f) * 1.8f)
                        + MathF.Sin(wy * 0.37f - tq * 0.9f + MathF.Sin(wx * 0.11f - tq * 0.4f) * 1.8f);
                if (MathF.Abs(v) > 0.13f || (s == 'w' && TideDry(wx, wy))) continue;
                var c = s == 'w' ? sea : pond;
                pix.Buf[py * W + px] = Pix.Mix(pix.Buf[py * W + px], c, c.A / 255f);
            }
        }
    }

    // The sun glinting on the open sea: tiny stars that flash and go.
    void DrawGlints(float t)
    {
        float day = 1 - Darkness, clear = 1 - rainAmt;
        if (day * clear < 0.2f) return;
        // With the sun low (Light: Full) the sea glitters gold, thicker over the swell and in the shallows too, as the
        // moon's glitter does at night.
        var (dusk, dawn) = SunGlow();
        float low = LightMode == 2 ? MathF.Max(dusk, dawn) : 0;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                bool shallow = low > 0 && map[y, x] == 'w';
                if (map[y, x] != '~' && !shallow) continue;
                float cycle = 2.6f + (float)Pix.Hash(x, y, 393) * 2.4f;
                int n = (int)((t + (float)Pix.Hash(x, y, 394) * 9) / cycle);
                double chance = 0.07;
                if (low > 0) chance += 0.2 * low * (0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(x * 0.09f - y * 0.05f - t * 0.25f)));
                if (Pix.Hash(x, y, 395 + n % 50) > chance) continue;
                float ph = (t + (float)Pix.Hash(x, y, 394) * 9) % cycle / 0.7f;
                if (ph > 1) continue;
                int gx = x * T + 2 + (int)(Pix.Hash(x, n, 396) * 6), gy = y * T + 2 + (int)(Pix.Hash(n, y, 397) * 6);
                if (!Wet(ShapePx(gx, gy)) || shallow && TideDry(gx, gy)) continue;
                float a = MathF.Sin(ph * MathF.PI) * 0.85f * day * clear;
                var c = low > 0 ? Pal.Rgba((int)(255), (int)(252 - 46 * low), (int)(228 - 108 * low), a) : Pal.Rgba(255, 252, 228, a);
                pix.Rect(gx, gy, 1, 1, c);
                if (ph > 0.3f && ph < 0.7f)
                {
                    var d = Pal.Rgba(255, 252, 228, a * 0.45f);
                    pix.Rect(gx - 1, gy, 1, 1, d); pix.Rect(gx + 1, gy, 1, 1, d); pix.Rect(gx, gy - 1, 1, 1, d); pix.Rect(gx, gy + 1, 1, 1, d);
                }
            }
    }

    /* ---------- Sky ---------- */
    // Mist lies on the water and the low ground in the early morning and burns off by half past eight.
    void DrawMist(float t)
    {
        float c = state.clock;
        float m = c < 300 || c > 540 ? 0 : c < 390 ? (c - 300) / 90f : c < 450 ? 1 : (540 - c) / 90f;
        m *= 1 - 0.5f * rainAmt;
        if (m <= 0.02f || Stormy) return;
        var fog = Pal.Rgba(232, 238, 244, 0.16f * m);
        float span = PW + 300;
        for (int i = 0; i < 10; i++)
        {
            float speed = 2 + (float)Pix.Hash(i, 1, 398) * 2;
            float cx = (float)((Pix.Hash(i, 2, 398) * span + t * speed) % span) - 150;
            float cy = (float)(Pix.Hash(i, 3, 398) * PH);
            float rx = 50 + (float)Pix.Hash(i, 4, 398) * 40, ry = rx * 0.32f;
            int sx0 = Math.Max(0, (int)(cx - rx) - camX), sx1 = Math.Min(W, (int)(cx + rx) - camX);
            int sy0 = Math.Max(0, (int)(cy - ry) - camY), sy1 = Math.Min(H, (int)(cy + ry) - camY);
            for (int py = sy0; py < sy1; py++)
                for (int px = sx0; px < sx1; px++)
                {
                    float dx = (px + camX - cx) / rx, dy = (py + camY - cy) / ry;
                    float d = dx * dx + dy * dy + (BayerAt(px + camX, py + camY) - 0.5f) * 0.35f;
                    if (d >= 1) continue;
                    float k = d < 0.45f ? 1 : 0.55f;
                    pix.Buf[py * W + px] = Pix.Mix(pix.Buf[py * W + px], fog, fog.A / 255f * k);
                }
        }
    }

    // When the sun is low, long soft beams slant down across the view.
    void DrawSunBeams(float t)
    {
        var (dusk, dawn) = SunGlow();
        float k = MathF.Max(dusk, dawn) * (1 - rainAmt);
        if (k <= 0.02f || scene != "world") return;
        var beam = dusk > dawn ? Pal.Rgba(255, 196, 120, 0.06f * k) : Pal.Rgba(255, 214, 190, 0.05f * k);
        // The beams are lines in the world (x = intercept + 0.55 y), a couple to every 260 px, so they stay put as you walk.
        const int cell = 260;
        int u0 = (int)MathF.Floor((camX - 0.55f * (camY + H) - 60) / cell), u1 = (int)MathF.Floor((camX + W - 0.55f * camY) / cell);
        for (int c = u0; c <= u1; c++)
            for (int b = 0; b < 2; b++)
            {
                if (Pix.Hash(c, b, 397) > 0.75) continue;
                float u = (c + (float)Pix.Hash(c, b, 399) * 0.9f) * cell + MathF.Sin(t * 0.07f + c + b) * 16;
                int bw = 10 + (int)(Pix.Hash(c, b, 398) * 22);
                for (int py = 0; py < H; py++)
                    pix.Fill((int)(u + 0.55f * (py + camY)) - camX, py, bw, 1, beam);
            }
    }

    /* ---------- Night ---------- */
    // Lit windows: houses keep a lamp burning in the evening and before dawn. Drawn in their day colour, they're noted
    // here and lit again over the dark (GlowWindows), with a little light spilling out (LightWindows).
    readonly List<(int x, int y, int w, int h)> litWindows = new();
    // Lamps burn from 18:30 until the village goes to bed at 22:30, and again from the first stirrings at 05:00 (FolkAsleep).
    bool LampsLit => state.clock >= 18 * 60 + 30 && state.clock < 22 * 60 + 30 || state.clock >= 5 * 60 && state.clock < 6 * 60 + 30;

    void Window(int x, int y, int w, int h, string day)
    {
        // Only in the live view: the chart's texture is drawn through the same code onto a world-sized picture, kept for
        // hours, so it always shows the windows by day.
        bool lit = LampK > 0 && LampsLit && liveRender && pix.W == W;
        // Coming on at dusk, a window warms from its day colour to the lamp's over the hour (LampK).
        if (!lit) pix.Rect(x, y, w, h, day);
        else
        {
            float on = LampK;
            pix.Rect(x, y, w, h, on >= 1 || LightMode == 0 ? Pal.C("#f3c25b") : Pix.Mix(Pal.C(day), Pal.C("#f3c25b"), on));
            litWindows.Add((x, y, w, h));
        }
    }

    void LightWindows()
    {
        foreach (var (x, y, w, h) in litWindows) LightHole(x + w / 2f, y + h + 4, 9, 0.45f);
    }

    void GlowWindows(float t, float k)
    {
        foreach (var (x, y, w, h) in litWindows)
        {
            float flick = 0.9f + 0.1f * MathF.Sin(t * 7 + x * 0.7f);
            pix.Rect(x, y, w, h, Pal.Rgba(255, 214, 120, k * flick));
            pix.Rect(x, y, w, 1, Pal.Rgba(255, 238, 180, k * 0.8f));
            LightGlow(x + w / 2f, y + h / 2f, w + 4, 255, 190, 90, 0.22f * k, 0.55f * k * flick);
        }
    }

    // The moon's path on the sea: a column of broken light that shivers, brighter the fuller the moon (not in rain, nor
    // while Bakunawa holds it). The strokes are planned on the bare water (PlanMoonPath, with each pixel's colour), kept
    // only where nothing has been drawn over them since (KeepMoonPath, before the dark), and lit after it (DrawMoonPath),
    // so the light never lands on a boat, a swimmer or a dry flat.
    readonly List<(int x, int y, Color under, float a)> moonPx = new();
    void PlanMoonPath(float t)
    {
        moonPx.Clear();
        if (Darkness <= 0 || rainAmt > 0.5f || eclipse != null || scene != "world") return;
        float full = 1 - MathF.Abs(MoonPhase - 4) / 4f;
        if (full < 0.15f) return;
        // Moonlight glitters on the whole sea, not on a track that follows you: each tile's own sparkles come and go on
        // their own beat, and a swell of light rolls slowly across the water so some stretches shimmer more than others.
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                if (map[y, x] is not ('~' or 'w')) continue;
                float swell = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(x * 0.09f - y * 0.05f - t * 0.25f));
                for (int k = 0; k < 2; k++)
                {
                    float cycle = 1.6f + (float)Pix.Hash(x, y, 472 + k) * 1.8f, off = (float)Pix.Hash(x, y, 474 + k) * 9;
                    int n = (int)((t + off) / cycle);
                    if (Pix.Hash(x, y, 476 + k + n % 40 * 2) > 0.34 * swell * (0.4 + full)) continue;
                    float ph = (t + off) % cycle / 0.9f;
                    if (ph > 1) continue;
                    int gx = x * T + 1 + (int)(Pix.Hash(x, n + k, 478) * 8), gy = y * T + 1 + (int)(Pix.Hash(n, y + k, 479) * 8);
                    float al = MathF.Sin(ph * MathF.PI);
                    for (int i = 0; i < (al > 0.6f ? 2 : 1); i++)
                    {
                        int px = gx + i - camX, py = gy - camY;
                        if (px < 0 || px >= W || py < 0 || py >= H || !Wet(ShapePx(gx + i, gy)) || TideDry(gx + i, gy)) continue;
                        moonPx.Add((px, py, pix.Buf[py * W + px], al));
                    }
                }
            }
    }

    void KeepMoonPath() => moonPx.RemoveAll(m => { var c = pix.Buf[m.y * W + m.x]; return c.R != m.under.R || c.G != m.under.G || c.B != m.under.B; });

    void DrawMoonPath(float k)
    {
        if (moonPx.Count == 0) return;
        float full = 1 - MathF.Abs(MoonPhase - 4) / 4f;
        float strength = 0.6f * k * (0.35f + 0.65f * full) * (1 - rainAmt);
        foreach (var (x, y, _, a) in moonPx) pix.Fill(x, y, 1, 1, Pal.Rgba(232, 240, 255, strength * a));
    }

    // Every building's shadow, drawn on the ground before anything stands on it (so it never falls across someone
    // drawn earlier): the villagers' houses, Tomas's hut, and your shacks and kubo.
    void DrawBuildingShadows()
    {
        bool Near(float x, float y) => x > camX - 60 && x < camX + W + 60 && y > camY - 40 && y < camY + H + 60;
        void Bahay(float x, float y) { if (Near(x, y)) BuildingShadow((int)(x - 17 * HouseS), (int)y + 3, (int)(34 * HouseS), (int)(18 * HouseS)); }
        foreach (var n in Islanders) if (n.id is not ("isay" or "ben")) Bahay(n.x, n.y - 5);
        foreach (var n in HabagatFolk) if (n.house) Bahay(n.x, n.y - 5);
        if (InMarilayaBox(player.X, player.Y))
        {
            Bahay(SelaX, SelaY - 5);
            foreach (var (hx, hy, _) in MlHouses) Bahay(hx, hy - 5);
        }
        if (Near(160, 68)) BuildingShadow((int)(160 - 10 * HouseS), 68, (int)(21 * HouseS), (int)(14 * HouseS));
        foreach (var b in state.builds)
            if (b.id is "shack" or "kubo" && Near(b.x * T, b.y * T)) BuildingShadow((int)(b.x * T + 10 - 10 * HouseS), b.y * T + 9, (int)(20 * HouseS), (int)((b.id == "kubo" ? 16 : 14) * HouseS));
    }

    /* ---------- Buildings ---------- */
    // A building's shadow on the ground to the east (the light is from the west and a little north): a soft band that
    // leans away from its base. x, y is the left end of the base, w its width, h roughly how tall it stands.
    void BuildingShadow(int x, int y, int w, int h)
    {
        if (shadeActive) { ShadeBuilding(x, y, w, h); return; }   // light on: into the sun's shadow mask (Shadows.cs)
        var c = Pal.Rgba(18, 40, 36, 0.16f * (1 - Darkness * 0.7f));
        int top = y - h / 2 + 3;
        // Along the east wall, widening toward the ground, and a strip along the foot of the building.
        for (int yy = top; yy <= y + 2; yy++)
        {
            int len = 3 + (yy - top) * 4 / Math.Max(1, y + 2 - top);
            pix.Rect(x + w, yy, len, 1, c);
        }
        pix.Rect(x + 3, y + 1, w - 3, 2, c);
    }

    // A thatched roof: rows widening from the ridge, lit on the west slope and in shade on the east, the thatch in
    // bands with a few stray straws at the eaves.
    void ThatchRoof(int cx, int top, int rows, int start, int grow)
    {
        for (int i = 0; i < rows; i++)
        {
            int half = start + i * grow;
            bool band = i % 3 == 0;
            pix.Rect(cx - half, top + i, half, 1, band ? "#a07c46" : "#d8bd78");
            pix.Rect(cx, top + i, half, 1, band ? "#7d5f33" : "#b4955a");
            if (i == rows - 1)
                for (int k = -half; k < half; k += 3) if (Pix.Hash(cx + k, top, 480) < 0.5) pix.Rect(cx + k, top + i + 1, 1, 1, "#c8a862");
        }
        pix.Rect(cx - start, top, start * 2, 1, "#e8cf8e");
    }

    /* ---------- Chores ---------- */
    // Standing about by day, some villagers get on with a job for a while: sweeping the yard, or mending a net. Looks
    // only; they stop when you come up to them, and never in the rain or at night.
    static string ChoreKind(string id) => id switch
    {
        "lira" or "rosa" or "lorna" or "nita" => "sweep",
        "niko" or "dado" or "maya" or "ambo" => "net",
        _ => null
    };

    string ChoreNow(string id, int x, int y, string face, bool moving)
    {
        string kind = ChoreKind(id);
        if (kind == null || moving || face == "up" || Night || rainAmt > 0.3f || !AutoBlinkOn) return null;
        if (state.clock < 7 * 60 || state.clock > 18 * 60 || Dist(player.X, player.Y, x, y) < 36) return null;
        float seed = id.Length * 1.7f + id[0] * 0.13f;
        return (time * 0.07f + seed) % 1f < 0.55f ? kind : null;
    }
    static bool AutoBlinkOn => LookData.AutoBlink;

    // Where the hands go for a chore (near hand, far hand), for DrawFigure's arms mode 4.
    ((int x, int y) hand, (int x, int y) hand2) ChoreHands(string kind, int x, int y, string face)
    {
        int dir = face == "left" ? -1 : 1;
        if (kind == "sweep")
        {
            // Both hands on the shaft: the top one near its end, the other two fifths of the way down.
            var (hx, hy, fx, fy) = BroomShaft(x, y, face);
            return ((hx, hy + 1), ((int)MathF.Round(hx + (fx - hx) * 0.4f), (int)MathF.Round(hy + (fy - hy) * 0.4f)));
        }
        int knot = (int)(time * 4) % 2;
        return face == "down" ? ((x + 5, y - 6 - knot), (x + 2, y - 5)) : ((x + 4 * dir, y - 6 - knot), (x + 2 * dir, y - 5));
    }

    // The broom's shaft, from its top (held) to where the bristles start, swinging side to side.
    (int hx, int hy, int fx, int fy) BroomShaft(int x, int y, string face)
    {
        int dir = face == "left" ? -1 : 1, sw = (int)MathF.Round(MathF.Sin(time * 5) * 1.5f);
        return face == "down" ? (x + 3 + sw, y - 7, x + 5 + sw * 2, y - 2) : (x + 2 * dir + sw * dir, y - 7, x + 6 * dir + sw * 2 * dir, y - 2);
    }

    void DrawChoreProp(string kind, int x, int y, string face)
    {
        int dir = face == "left" ? -1 : 1;
        if (kind == "sweep")
        {
            // A walis tingting: a bundle of thin midribs on a short cord, swept side to side, kicking up a little dust.
            int sw = (int)MathF.Round(MathF.Sin(time * 5) * 1.5f);
            var (hx, hy, fx, fy) = BroomShaft(x, y, face);
            fy += 2;
            pix.Line(hx, hy, fx, fy - 2, "#8a6a3a");
            pix.Rect(fx - 2, fy - 2, 5, 1, "#c9a86a"); pix.Rect(fx - 2, fy - 1, 5, 1, "#b08f52");
            pix.Rect(fx - 3, fy, 1, 1, "#b08f52"); pix.Rect(fx + 3, fy, 1, 1, "#b08f52");
            if ((int)(time * 5) % 4 == 0) pix.Rect(fx + (sw > 0 ? 3 : -4), fy - 1, 1, 1, Pal.Rgba(220, 200, 160, 0.7f));
            return;
        }
        // A fishing net held up between the hands, its mesh hanging, a float or two at the edge.
        var (h1, h2) = ChoreHands(kind, x, y, face);
        // Held up between the two hands (and a little past the near one), hanging below them.
        int d = face == "left" ? -1 : 1, top = Math.Min(h1.y, h2.y) + 1;
        int x0 = Math.Min(h1.x, h2.x), x1 = Math.Max(h1.x, h2.x);
        if (d > 0) x1 = Math.Max(x1, x0 + 4); else x0 = Math.Min(x0, x1 - 4);
        // Strands hanging from the hands to the knees, knotted across in two rows.
        int bottom = top + 5;
        var cord = Pal.C("#d8cfa6");
        for (int xx = x0; xx <= x1; xx += 2) pix.Rect(xx, top, 1, bottom - top + 1, cord);
        for (int xx = x0 + 1; xx < x1; xx += 2) { pix.Rect(xx, top + 2, 1, 1, cord); pix.Rect(xx, top + 4, 1, 1, cord); }
        pix.Rect(x0, bottom + 1, 1, 1, "#e0603a"); pix.Rect(x1, bottom + 1, 1, 1, "#e0603a");
    }

    /* ---------- Footprints ---------- */
    // Each print with the moment it was made (game time), so they fade on time even while you're indoors.
    readonly List<(float x, float y, float made, bool snow, int side)> footprints = new();
    const float FootprintLife = 5;
    int footSide;
    void AddFootprint(bool snow)
    {
        footSide = -footSide == 0 ? 1 : -footSide;
        int ox = player.Face is "up" or "down" ? footSide : 0, oy = player.Face is "left" or "right" ? (footSide > 0 ? 0 : 1) : 0;
        footprints.Add((MathF.Round(player.X) + ox, MathF.Round(player.Y) + oy, time, snow, footSide));
        if (footprints.Count > 60) footprints.RemoveAt(0);
    }

    void DrawFootprints()
    {
        for (int i = footprints.Count - 1; i >= 0; i--)
        {
            var f = footprints[i];
            float life = FootprintLife - (time - f.made);
            if (life <= 0) { footprints.RemoveAt(i); continue; }
            float a = Math.Min(1, life / 2f);
            var c = f.snow ? Pal.Rgba(150, 175, 200, 0.45f * a) : Pal.Rgba(120, 90, 50, 0.28f * a);
            pix.Rect(f.x, f.y, 1, 1, c);
            pix.Rect(f.x, f.y - 1, 1, 1, Pal.WithAlpha(c, c.A / 255f * 0.6f));
        }
    }

    /* ---------- Fish jumping ---------- */
    // Now and then by day a small fish leaps out of the water in view and falls back with a splash.
    readonly List<(float x, float y, float start, int dir)> leaps = new();
    float leapT = 3;
    void DrawLeaps(float t)
    {
        float dt = Raylib.GetFrameTime();
        if (scene == "world" && !Night && !Stormy)
        {
            leapT -= dt;
            if (leapT <= 0)
            {
                leapT = 2.5f + (float)fxRng.NextDouble() * 5;
                for (int tries = 0; tries < 12; tries++)
                {
                    int wx = camX + fxRng.Next(W), wy = camY + 8 + fxRng.Next(H - 8);
                    // Out in open water, clear of the shore and of dry flats, both where it leaps and where it lands.
                    int dir = fxRng.Next(2) * 2 - 1;
                    bool open = true;
                    for (int k = -6; k <= 15 && open; k += 3)
                    {
                        int ax = wx + k * dir;
                        open = Wet(ShapePx(ax, wy)) && ShapePx(ax, wy) != 'm' && !TideDry(ax, wy) && Wet(ShapePx(ax, wy - 4)) && Wet(ShapePx(ax, wy + 4));
                    }
                    if (!open || Data.Spots.Any(s => s.Scene == "world" && Dist(wx, wy, s.X, s.Y) < s.R + 6)) continue;
                    leaps.Add((wx, wy, time, dir));
                    break;
                }
            }
        }
        for (int i = leaps.Count - 1; i >= 0; i--)
        {
            var (lx0, ly0, start, ldir) = leaps[i];
            var l = (x: lx0, y: ly0, t: time - start, dir: ldir);
            if (l.t > 1.4f) { leaps.RemoveAt(i); continue; }
            const float air = 0.6f;
            var ring = Pal.Rgba(225, 242, 250, 0.6f);
            if (l.t < 0.4f) pix.Ring(l.x, l.y, 1 + l.t * 6, Pal.WithAlpha(ring, 0.6f * (1 - l.t / 0.4f)));
            if (l.t < air)
            {
                float k = l.t / air, fx = l.x + l.dir * k * 9, fy = l.y - MathF.Sin(k * MathF.PI) * 7;
                var body = Pal.C("#c9d6dc");
                pix.Rect(fx - 1, fy, 3, 1, body); pix.Rect(fx + (k < 0.5f ? -l.dir : l.dir) * 2, fy - (k < 0.5f ? 1 : -1), 1, 1, Pal.C("#8fa4ae"));
                pix.Rect(fx + l.dir, fy, 1, 1, Pal.C("#eef6f8"));
            }
            else
            {
                float k = (l.t - air) / (1.4f - air);
                pix.Ring(l.x + l.dir * 9, l.y, 1 + k * 5, Pal.WithAlpha(ring, 0.6f * (1 - k)));
                if (k < 0.3f) { pix.Rect(l.x + l.dir * 9, l.y - 2, 1, 1, ring); pix.Rect(l.x + l.dir * 9 - 2, l.y - 1, 1, 1, ring); pix.Rect(l.x + l.dir * 9 + 2, l.y - 1, 1, 1, ring); }
            }
        }
    }

    /* ---------- Rain ---------- */
    // Rain hitting the ground: little rings on water and splashes on land, here and there across the view.
    void DrawRainSplashes(float t)
    {
        if (rainAmt <= 0.05f || scene != "world") return;
        int n = (int)(40 * rainAmt + 50 * stormAmt);
        for (int i = 0; i < n; i++)
        {
            float rate = 2.2f + (float)Pix.Hash(i, 1, 400);
            float u = t * rate + (float)Pix.Hash(i, 2, 400) * 10;
            int cyc = (int)u;
            float age = u - cyc;
            // Fixed in the world (a splash stays where the rain hit), not on the screen.
            int wx = camX + (int)Wrap(Pix.Hash(i, cyc, 401) * W - camX, W), wy = camY + (int)Wrap(Pix.Hash(cyc, i, 402) * H - camY, H);
            if (BiomeAt(wx / T, wy / T) == 1) continue;
            char s = ShapePx(wx, wy);
            float a = (1 - age) * 0.5f * rainAmt;
            if (Wet(s)) { if (LightMode == 0) pix.Ring(wx + 0.5, wy + 0.5, 0.6 + age * 2.4, Pal.Rgba(220, 238, 248, a)); }   // with light on the sea's rain is DrawSeaRain's
            else if (age < 0.4f)
            {
                var c = Pal.Rgba(220, 234, 244, a);
                int h = age < 0.2f ? 1 : 2;
                pix.Rect(wx - h, wy - h, 1, 1, c); pix.Rect(wx + h, wy - h, 1, 1, c);
                if (age < 0.2f) pix.Rect(wx, wy, 1, 1, c);
            }
        }
    }
}
