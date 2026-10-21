using Raylib_cs;

namespace Fesh;

// The sea the title's opening rises through (Opening.cs): 320 pixels across and DeepH down, seen 180 at a time from
// `cam` (the depth of the view's top edge). Everything is worked out from the time and a hash, so nothing is stored.
partial class Game
{
    void PaintUnderwater(Pix p, float t, float cam)
    {
        const int w = 320, h = 180;
        int c0 = (int)MathF.Round(cam);
        // Draws at a point of the sea (y is depth), if it's in view.
        void At(float x, float wy, Color c) => Dot(p, (int)MathF.Round(x), (int)MathF.Round(wy) - c0, c);
        void Blend(float x, float wy, Color c, float k)
        {
            int ix = (int)MathF.Round(x), iy = (int)MathF.Round(wy) - c0;
            if (ix < 0 || iy < 0 || ix >= w || iy >= h) return;
            p.Buf[iy * w + ix] = Pix.Mix(p.Buf[iy * w + ix], c, k);
        }

        // The water: bright under the surface, blue in the middle, nearly black on the bottom; shafts of light near the top.
        var top = Pal.C("#2f8bb5"); var mid = Pal.C("#175a85"); var deep = Pal.C("#06182a");
        for (int y = 0; y < h; y++)
        {
            float wy = y + cam, k = Math.Clamp(wy / DeepH, 0, 1);
            var row = k < 0.4f ? Pix.Mix(top, mid, k / 0.4f) : Pix.Mix(mid, deep, (k - 0.4f) / 0.6f);
            // The light doesn't get down past about 300 px: below that a row is just its colour (Codex: the shafts were
            // worked out for every pixel of the seabed, where none can show).
            float reach = Math.Clamp(1 - wy / 300f, 0, 1);
            for (int x = 0; x < w; x++)
            {
                var c = row;
                if (wy < 6) { float s = MathF.Sin(x * 0.18f + t * 3) + MathF.Sin(x * 0.07f - t * 1.7f); if (s > 0.9f - wy * 0.25f) c = Pix.Mix(c, Pal.C("#bfe8f4"), 0.6f); }
                if (reach > 0 && BayerAt(x, y) < reach * 0.55f)
                {
                    float shaft = MathF.Sin((x + wy * 0.45f) * 0.045f + t * 0.35f) + 0.5f * MathF.Sin((x + wy * 0.45f) * 0.11f - t * 0.6f);
                    if (shaft > 1.05f) c = Pix.Mix(c, Pal.C("#7cc7df"), 0.35f);
                }
                p.Buf[y * w + x] = c;
            }
        }

        /* ---------- the seabed: sand, the trench on the left, the old wreck ---------- */
        for (int x = 0; x < w; x++)
        {
            // The floor rises gently to the right; on the left it drops away into a trench.
            float floor = DeepH - 22 + MathF.Sin(x * 0.05f) * 3 - (x > 110 ? (x - 110) * 0.04f : 0);
            if (x < 110) floor += (110 - x) * 0.6f;
            for (float wy = floor; wy < DeepH + 60; wy++)
            {
                float d = wy - floor;
                var sand = d < 1 ? Pal.C("#5d5a4a") : BayerAt(x, (int)wy) < 0.2f ? Pal.C("#3a3a34") : Pal.C("#2a2c2a");
                At(x, wy, Pix.Mix(sand, deep, x < 110 ? 0.55f : 0.25f));
            }
        }
        // The wreck, on its side in the sand, the deep water over it.
        // (Twice its size on the beach: down here it's the whole ship, not a hulk half buried in the sand.)
        {
            var g = LandmarkArt.Wreck; var pal = WreckDeepPalette;
            int ax = LandmarkArt.WreckAnchorX, ay = LandmarkArt.WreckAnchorY;
            for (int j = 0; j < g.Length; j++)
                for (int i = 0; i < g[j].Length; i++)
                {
                    char ch = g[j][i];
                    if (ch == '.' || ch - 'a' < 0 || ch - 'a' >= pal.Length) continue;
                    var c = Pal.C(pal[ch - 'a']);
                    float x = 214 + (i - ax) * 2, wy = DeepH - 22 + (j - ay) * 2;
                    At(x, wy, c); At(x + 1, wy, c); At(x, wy + 1, c); At(x + 1, wy + 1, c);
                }
        }
        // Something in the trench: two eyes opening, looking about, blinking, and closing again.
        if (t > EyesAt && t < EyesEnd)
        {
            // The dark of a great head round them, only just there.
            for (int dy = -16; dy <= 14; dy++) for (int dx = -40; dx <= 40; dx++)
                {
                    float r2 = dx * dx / 1600f + dy * dy / 256f;
                    if (r2 < 1) Blend(53 + dx, DeepH - 12 + dy, Pal.C("#020a12"), 0.45f * (1 - r2));
                }
            float k = (t - EyesAt) / (EyesEnd - EyesAt);
            float open = Smooth(0, 0.15f, k) * (1 - Smooth(0.82f, 1, k)) * (MathF.Abs(k - 0.5f) < 0.04f ? 0 : 1);   // a blink halfway
            float look = MathF.Sin(k * 6) * 1.5f;
            foreach (float ex in new[] { 44f, 62f })
            {
                float ey = DeepH - 14;
                for (int dy = -4; dy <= 4; dy++) for (int dx = -7; dx <= 7; dx++)
                    {
                        float r2 = dx * dx / 49f + dy * dy / 16f;
                        if (r2 < 1) Blend(ex + dx, ey + dy, Pal.C("#3fd8c8"), 0.18f * open * (1 - r2));   // a faint glow round each
                    }
                int lid = (int)MathF.Round(2.5f * open);
                for (int dx = -3; dx <= 3; dx++)
                    for (int dy = -lid; dy <= lid; dy++)
                        if (dx * dx / 9f + dy * dy / 6.25f <= 1) At(ex + dx, ey + dy, Pal.C("#9ff6ea"));
                if (lid > 0) { At(ex + look, ey, Pal.C("#06182a")); At(ex + look, ey - 1, Pal.C("#06182a")); }
            }
        }
        // Kelp on the bottom, swaying.
        for (int i = 0; i < 7; i++)
        {
            float bx = 128 + i * 27 + (float)Pix.Hash(i, 7, 3) * 12;
            int len = 26 + (int)(Pix.Hash(i, 2, 9) * 34);
            float foot = DeepH - 24 - (bx - 110) * 0.04f;
            for (int j = 0; j < len; j++)
            {
                float sway = MathF.Sin(t * 1.4f + i + j * 0.12f) * j * 0.12f;
                At(bx + sway, foot - j, j % 5 == 0 ? Pal.C("#3f7d3a") : Pal.C("#2c5e30"));
            }
        }

        /* ---------- the reef, part way up on the right ---------- */
        float shelf = 395;
        for (int x = 190; x < w; x++)
        {
            float edge = shelf + MathF.Sin(x * 0.2f) * 2 + (x < 210 ? (210 - x) * 1.4f : 0);
            // Rock: a lit top with a sandy lip, a rough face, a shaded underside that thins out to the right.
            float thick = 24 - (x - 190) * 0.05f + MathF.Sin(x * 0.31f) * 3;
            for (float wy = edge; wy < edge + thick; wy++)
            {
                float d = wy - edge;
                var c = d < 1 ? Pal.C("#8a8466") : d < 3 ? Pal.C("#5e6a5a") : d > thick - 4 ? Pal.C("#1f2a30") : BayerAt(x, (int)wy) < 0.3f ? Pal.C("#46524c") : Pal.C("#36433e");
                if (d >= 3 && Pix.Hash(x, (int)wy, 31) < 0.05f) c = Pal.C("#58665a");   // a few lighter stones
                At(x, wy, c);
            }
        }
        // Coral heads: round brain corals, branching staghorn, a fan.
        foreach (var (cx, rad, col, kind) in new[] { (220f, 7f, "#d9707a", 0), (243f, 9f, "#e8a04a", 1), (268f, 6f, "#9a6ad0", 0), (290f, 8f, "#e86a8a", 2), (308f, 6f, "#f0c050", 1) })
        {
            var cc = Pal.C(col);
            float cy = shelf + MathF.Sin(cx * 0.2f) * 2;
            if (kind == 0)
                for (int dy = -(int)rad; dy <= 0; dy++) for (int dx = -(int)rad; dx <= rad; dx++)
                    { if (dx * dx + dy * dy * 1.6f < rad * rad) At(cx + dx, cy + dy, (dx + dy) % 3 == 0 ? Pix.Mix(cc, Color.White, 0.25f) : cc); }
            else if (kind == 1)
                for (int b = -2; b <= 2; b++) for (int j = 0; j < rad + 3; j++) At(cx + b * 3 + MathF.Sin(j * 0.6f + b) * 0.8f, cy - j, j > rad ? Pix.Mix(cc, Color.White, 0.4f) : cc);
            else
                for (int j = 0; j < rad * 2; j++) for (int dx = -j / 2; dx <= j / 2; dx++) if ((dx + j) % 2 == 0) At(cx + dx, cy - rad * 2 + j + MathF.Sin(t * 1.5f) * (rad * 2 - j) * 0.06f, cc);
        }
        // Little orange-and-white fish nosing about the coral.
        for (int i = 0; i < 4; i++)
        {
            float fx = 230 + i * 18 + MathF.Sin(t * 1.3f + i * 2) * 6, fy = shelf - 12 - i % 2 * 5 + MathF.Sin(t * 2 + i) * 1.5f;
            bool right = MathF.Cos(t * 1.3f + i * 2) > 0;
            for (int dx = 0; dx < 4; dx++) At(fx + (right ? dx : -dx), fy, dx == 1 ? Color.White : Pal.C("#ff8a2a"));
            At(fx + (right ? -1 : 1), fy, Pal.C("#ff8a2a"));
        }

        /* ---------- what swims in the middle water ---------- */
        // A sea turtle gliding across, flippers sweeping.
        {
            float tx = -30 + (t - 0.6f) * 62, ty = 318 + MathF.Sin(t * 0.8f) * 4;
            float flap = MathF.Sin(t * 3.2f);
            for (int dy = -5; dy <= 5; dy++) for (int dx = -8; dx <= 8; dx++)
                {
                    float r2 = dx * dx / 64f + dy * dy / 25f;
                    if (r2 > 1) continue;
                    var shell = (Math.Abs(dx) + Math.Abs(dy)) % 4 == 0 ? Pal.C("#2f4a2a") : r2 < 0.45f ? Pal.C("#6a7a3a") : Pal.C("#4f6030");
                    At(tx + dx, ty + dy, shell);
                }
            for (int j = 0; j < 4; j++) { At(tx + 9 + j, ty - 1, Pal.C("#8a9a6a")); At(tx + 9 + j, ty, Pal.C("#8a9a6a")); }
            At(tx + 12, ty - 1, Pal.C("#1a1a1a"));
            foreach (float s in new[] { -1f, 1f })
                for (int j = 0; j < 7; j++) At(tx + 4 - j * 0.5f, ty + s * (5 + j * (0.6f + 0.4f * flap * s)), Pal.C("#7a8a5a"));
            for (int j = 0; j < 3; j++) { At(tx - 7 - j, ty - 3 - j * 0.8f, Pal.C("#7a8a5a")); At(tx - 7 - j, ty + 3 + j * 0.8f, Pal.C("#7a8a5a")); }
        }
        // Jellyfish, pulsing and drifting slowly up.
        foreach (var (jx, jy, jc) in new[] { (60f, 262f, "#e8a0c8"), (270f, 236f, "#b0a0f0"), (150f, 176f, "#f0b8d0") })
        {
            float pulse = 0.8f + 0.2f * MathF.Sin(t * 3 + jx);
            float x0 = jx + MathF.Sin(t * 0.5f + jy) * 4, y0 = jy - t * 3;
            var col = Pal.C(jc);
            for (int dy = -6; dy <= 0; dy++) for (int dx = -7; dx <= 7; dx++)
                    if (dx * dx / (49f * pulse * pulse) + dy * dy / 36f <= 1) Blend(x0 + dx, y0 + dy, col, dy > -2 ? 0.45f : 0.65f);
            for (int tnt = -2; tnt <= 2; tnt++)
                for (int j = 1; j < 12; j++) Blend(x0 + tnt * 2.5f * pulse + MathF.Sin(t * 2 + j * 0.5f + tnt) * 1.2f, y0 + j, col, 0.45f * (1 - j / 12f));
        }
        // A school of little fish crossing.
        float schoolX = -40 + (t * 70) % 420;
        for (int i = 0; i < 13; i++)
        {
            float fx = schoolX - (i % 4) * 9 - (float)Pix.Hash(i, 3, 5) * 6, fy = 214 + (i / 4) * 7 + MathF.Sin(t * 3 + i) * 1.5f;
            At(fx, fy, Pal.C("#cfe9f0")); At(fx + 1, fy, Pal.C("#cfe9f0")); At(fx + 2, fy, Pal.C("#9fd0e0"));
            At(fx - 1, fy - 1, Pal.C("#6fb0c8")); At(fx - 1, fy + 1, Pal.C("#6fb0c8"));
        }
        // A little glowing fish darting past, lighting the water round it.
        {
            float gx = 340 - ((t - 1.8f) * 150) % 420, gy = 150 + MathF.Sin(t * 5) * 6;
            for (int dy = -9; dy <= 9; dy++) for (int dx = -9; dx <= 9; dx++)
                {
                    float r = MathF.Sqrt(dx * dx + dy * dy) / 9f;
                    if (r < 1) Blend(gx + dx, gy + dy, Pal.C("#9ff6ea"), 0.25f * (1 - r));
                }
            // Its body (swimming left), a forked tail behind it, and a dark eye.
            for (int dx = -4; dx <= 4; dx++)
            {
                int half = Math.Abs(dx) == 4 ? 0 : Math.Abs(dx) == 3 ? 1 : 2;
                for (int dy = -half; dy <= half; dy++) At(gx + dx, gy + dy, dy < 0 ? Pal.C("#d8fff8") : Pal.C("#9ff6ea"));
            }
            for (int j = 1; j <= 3; j++) { At(gx + 4 + j, gy - j, Pal.C("#9ff6ea")); At(gx + 4 + j, gy + j, Pal.C("#9ff6ea")); }
            At(gx - 2, gy - 1, Pal.C("#0c3a40"));
        }

        /* ---------- near the surface: a banca, its line, a bite ---------- */
        {
            // The hull from below, dark against the light, with its two floats.
            for (int x = 140; x < 210; x++)
            {
                float depth = 9 - MathF.Pow((x - 175) / 35f, 2) * 6;
                for (int wy = 0; wy < depth; wy++) At(x, wy, Pal.C("#10283a"));
            }
            for (int x = 118; x < 232; x++) { if (x < 136 || x > 214) { At(x, 3, Pal.C("#163246")); At(x, 4, Pal.C("#163246")); } }
            // The line down to the hook, and the bait on it.
            float hook = 122;
            float jerk = Math.Clamp((t - JerkAt) / 0.35f, 0, 1);
            hook -= jerk * jerk * 140;
            float tug = t > BiteAt && t < JerkAt ? MathF.Sin(t * 40) * 1.5f : 0;
            for (float wy = 8; wy < hook; wy++) At(196 + tug * (wy / hook), wy, Pal.C("#d8e8ee"));
            At(196 + tug, hook, Pal.C("#c0c8cc")); At(195 + tug, hook + 1, Pal.C("#c0c8cc")); At(196 + tug, hook + 2, Pal.C("#c0c8cc"));
            // A fish coming for the bait, then hooked and pulled up with it.
            if (t < BiteAt)
            {
                float k = Math.Clamp((t - (BiteAt - 1.6f)) / 1.6f, 0, 1);
                float fx = 120 + k * 72, fy = hook + 4 - k * 3;
                if (k > 0) DrawIntroFish(At, fx, fy, true);
                At(197, hook + 1, Pal.C("#c07a4a"));   // the bait
            }
            else DrawIntroFish(At, 194 + tug, hook + 4, true);
        }

        /* ---------- bubbles rising through all of it ---------- */
        for (int i = 0; i < 70; i++)
        {
            float speed = 18 + (float)Pix.Hash(i, 1, 1) * 22;
            float span = DeepH + 30;
            float wy = DeepH + 20 - (t * speed + (float)Pix.Hash(i, 4, 2) * span) % span;
            float bx = (float)Pix.Hash(i, 9, 4) * w + MathF.Sin(t * 2 + i) * 2;
            var bc = Pal.C("#d8f2fa");
            if (i % 3 != 0) At(bx, wy, bc);
            else { At(bx, wy - 1, bc); At(bx - 1, wy, bc); At(bx + 1, wy, bc); At(bx, wy + 1, Pal.C("#9fd0e0")); }
        }
    }

    // A small fish facing right (or left), drawn with the opening's own dot function.
    static void DrawIntroFish(Action<float, float, Color> at, float x, float y, bool right)
    {
        float s = right ? 1 : -1;
        for (int dx = -4; dx <= 4; dx++)
        {
            int half = dx is -4 or 4 ? 0 : dx is -3 or 3 ? 1 : 2;
            for (int dy = -half; dy <= half; dy++) at(x + dx * s, y + dy, dy > 0 ? Pal.C("#e8f0e0") : Pal.C("#8ab0a0"));
        }
        at(x - 5 * s, y - 2, Pal.C("#6a9080")); at(x - 5 * s, y + 2, Pal.C("#6a9080")); at(x - 6 * s, y - 3, Pal.C("#6a9080")); at(x - 6 * s, y + 3, Pal.C("#6a9080"));
        at(x + 3 * s, y - 1, Pal.C("#1a1a1a"));
    }
}
