using Raylib_cs;

namespace Fesh;

// Aling Nita's lime butterflies, painted a pixel at a time: the leaves, the four stages, and a butterfly coming out of its
// pupa slowly enough to watch (the shell darkening as the wings show through, splitting, the body hauling out, the wings
// pumped up from crumpled and wet to full size, drying, and opening).
partial class Game
{
    /* ---------- The stem, the pupa and the butterfly ---------- */
    static void PaintStem(Pix p, float topX, float botX, int w0, int w1)
    {
        for (int y = 0; y < p.H; y++)
        {
            float k = y / (float)p.H, cx = topX + (botX - topX) * k + MathF.Sin(y * 0.07f) * 1.6f;
            float w = w0 + (w1 - w0) * k;
            for (int x = (int)MathF.Floor(cx - w / 2); x <= (int)MathF.Ceiling(cx + w / 2); x++)
            {
                float u = (x - cx) / (w / 2);
                var c = u < -0.4f ? Pal.C("#9a7448") : u < 0.35f ? Pal.C("#74502e") : Pal.C("#4c341f");
                if (Pix.Hash(x, y / 2, 710) < 0.08) c = Pix.Mix(c, Pal.C("#2a1c10"), 0.5f);
                if (u < -0.8f || u > 0.9f) c = Pix.Mix(c, Pal.C("#2a1c10"), 0.35f);
                Dot(p, x, y, c);
            }
        }
    }

    static float StemX(Pix p, float topX, float botX, int y) => topX + (botX - topX) * (y / (float)p.H) + MathF.Sin(y * 0.07f) * 1.6f;

    // The pupa clinging to the stem by a silk belt, upright: green-brown and mottled, the wings of the butterfly showing through as
    // it ripens (ripe 0 to 1), then pale and split when the butterfly has left or is leaving (spent 0 to 1).
    static void PaintPupa(Pix p, float cx, float top, float len, float wid, float ripe, float spent, float split, float time, float stemX)
    {
        float k0 = 0.38f;
        for (int y = 0; y <= len; y++)
        {
            float k = y / len;
            float half = k < k0 ? wid / 2 * MathF.Sqrt(MathF.Max(0, 1 - MathF.Pow((k0 - k) / k0, 2.2f))) : wid / 2 * (1 - MathF.Pow((k - k0) / (1 - k0), 1.5f)) + 0.6f;
            if (half < 0.6f) half = 0.6f;
            int py = (int)MathF.Round(top + y);
            // It leans away from the stem a little towards the foot, and bows.
            float bow = MathF.Sin(k * MathF.PI) * 1.2f - k * 0.8f;
            for (int x = (int)MathF.Round(cx + bow - half); x <= (int)MathF.Round(cx + bow + half); x++)
            {
                float nx = (x - cx - bow) / half;
                float L = -nx * 0.55f + (0.5f - k) * 0.15f + ((float)Pix.Hash(x, py, 711) - 0.5f) * 0.18f + (BayerAt(x, py) - 0.5f) * 0.14f;
                var c = L > 0.35f ? Pal.C("#a2b560") : L > -0.05f ? Pal.C("#85994a") : Pal.C("#5f7436");
                // Mottling (brown and paler patches), a few pale specks.
                float mott = VNoise(x * 3, py * 3, 7, 712);
                if (mott > 0.64f) c = Pix.Mix(c, Pal.C("#7a6a3c"), 0.5f);
                else if (mott < 0.26f) c = Pix.Mix(c, Pal.C("#c4cf86"), 0.35f);
                // The folded wings beneath: the dark and yellow of the butterfly, more and more plainly.
                if (nx > -0.15f && k > 0.12f && k < 0.78f)
                {
                    float band = MathF.Sin((py - top) * 0.55f + nx * 3);
                    var wing = band > 0.55f ? Pal.C("#d8d273") : Pal.C("#2c2a22");
                    c = Pix.Mix(c, wing, ripe * 0.78f);
                }
                // Abdomen rings on the lower part.
                if (k > 0.62f && (y % 3 == 0)) c = Pix.Mix(c, Pal.C("#3b4a22"), 0.4f);
                // Spent: the shell goes pale and glassy.
                if (spent > 0) c = Pix.Mix(c, Pal.C(L > 0.2f ? "#e6e2c8" : "#bdb996"), spent * 0.88f);
                bool edge = x == (int)MathF.Round(cx + bow - half) || x == (int)MathF.Round(cx + bow + half) || y == 0 || y == (int)len;
                if (edge) c = Pix.Mix(c, Pal.C(spent > 0.5f ? "#7f7a5a" : "#33431f"), 0.8f);
                Dot(p, x, py, c);
            }
        }
        // The split down the front from the head end, and the little flaps either side of it opening.
        if (split > 0)
        {
            int n = (int)(len * 0.42f * split);
            for (int y = 1; y <= n; y++)
            {
                float open = Math.Min(1.6f, y * 0.2f) * split;
                int py = (int)MathF.Round(top + y);
                Dot(p, (int)MathF.Round(cx - open * 0.5f), py, Pal.C("#2a2a1e")); Dot(p, (int)MathF.Round(cx + open * 0.5f + 0.5f), py, Pal.C("#2a2a1e"));
                if (open > 0.8f) Dot(p, (int)MathF.Round(cx), py, Pal.C("#1d1a14"));
            }
        }
        // The silk belt round the middle, to the stem, and the pad at the foot.
        float by = top + len * 0.5f;
        Line(p, stemX + 1, by - 2, cx + wid / 2 + 1, by + 1, Pal.C("#ece6cf"));
        Line(p, stemX + 1, by - 1, cx + wid / 2 + 1, by + 2, Pal.C("#c9c3a8"));
        Line(p, cx - wid / 2, by + 1, cx + wid / 2, by + 1, Pal.C("#ece6cf"));
        Blob(p, stemX + 2, top + len + 0.5f, 2.2f, 1.6f, 0, (l, x, y) => Pal.C("#e4dec6"));
    }

    // One wing set, as a polygon from the shoulder at (ox, oy): size S, open 0 (hanging down) to 1 (spread out), `dir` -1 or +1.
    static void PaintWing(Pix p, float ox, float oy, float S, float open, float crumple, float wet, int dir, bool far, int seed)
    {
        if (S < 2) return;
        float rot = (1 - open) * MathF.PI / 2 * 0.98f + 0.05f;
        float cr = MathF.Cos(rot), sr = MathF.Sin(rot);
        (float x, float y)[] Crumple((float x, float y)[] pts) => pts.Select((q, i) =>
            (q.x + crumple * S * 0.13f * MathF.Sin(i * 2.3f + seed), q.y + crumple * S * 0.13f * MathF.Cos(i * 1.7f + seed * 2))).ToArray();
        (float x, float y)[] fore = { (0, 0), (0.42f, -0.22f), (0.92f, -0.32f), (1.0f, -0.12f), (0.92f, 0.22f), (0.58f, 0.38f), (0.16f, 0.3f) };
        (float x, float y)[] hind = { (0.05f, 0.2f), (0.5f, 0.3f), (0.82f, 0.5f), (0.8f, 0.78f), (0.62f, 1.0f), (0.5f, 0.76f), (0.3f, 0.56f), (0.08f, 0.4f) };
        foreach (var (poly, isHind) in new[] { (hind, true), (fore, false) })
        {
            var pts = Crumple(poly.Select(q => (q.x * S, q.y * S)).ToArray());
            // Into place: rotate (hanging down at open 0), mirror for the far side.
            var world = pts.Select(q => (ox + dir * (q.x * cr - q.y * sr), oy + (q.x * sr + q.y * cr))).ToArray();
            FillPoly(p, world, (x, y) =>
            {
                float rx = (x - ox) * dir, ry = y - oy;
                float lx = rx * cr + ry * sr, ly = -rx * sr + ry * cr;
                float u = lx / S, v = ly / S;
                // The lime butterfly: black wings, a pale yellow-green band, spots round the edge, a blue and an orange eye on the hind wing.
                Color c = Pal.C("#1e1c18");
                if (!isHind)
                {
                    float diag = u * 1.0f - v * 0.6f;
                    if (diag > 0.46f && diag < 0.66f && v > -0.28f) c = Pal.C("#d9d57a");
                    if (u > 0.74f && MathF.Sin(v * 22) > 0.45f && v > -0.28f) c = Pal.C("#d9d57a");
                    if (u > 0.2f && u < 0.4f && MathF.Abs(v - 0.1f) < 0.06f) c = Pal.C("#c8c46a");
                }
                else
                {
                    if (Dist2(u, v, 0.58f, 0.58f) < 0.011f) c = Pal.C("#4e82d8");
                    else if (Dist2(u, v, 0.42f, 0.8f) < 0.01f) c = Pal.C("#e0803a");
                    if (u > 0.62f && MathF.Sin((u + v) * 18) > 0.5f) c = Pal.C("#d9d57a");
                    if (u > 0.2f && u < 0.45f && v < 0.5f && v > 0.26f && (int)(lx) % 2 == 0) c = Pal.C("#d9d57a");
                }
                // Veins, faint.
                if ((int)(MathF.Abs(MathF.Atan2(v, u) * 12) ) % 3 == 0 && u > 0.12f) c = Pix.Mix(c, Color.Black, 0.18f);
                // Wet: pale and glossy, drying to its colour; the far wing a little darker.
                if (wet > 0) c = Pix.Mix(c, Pal.C(((x + y) % 5 == 0) ? "#aebdc8" : "#6e8394"), wet * 0.62f);
                if (far) c = Pix.Mix(c, Pal.C("#0c0b09"), 0.3f);
                if (u < 0.12f) c = Pix.Mix(c, Pal.C("#3b3024"), 0.7f);   // the fur at the shoulder
                return c;
            });
        }
    }

    static float Dist2(float a, float b, float c, float d) => (a - c) * (a - c) + (b - d) * (b - d);

    // The butterfly: head up, thorax at (ax, ay), the abdomen hanging below, six legs gripping the stem, antennae twitching,
    // the wings at size S (open 0 hanging down, 1 spread).
    static void PaintButterfly(Pix p, float ax, float ay, float S, float open, float crumple, float wet, float abd, float time, float stemX)
    {
        // The far wing, behind the body (mirrored), then the body, then the near wing.
        PaintWing(p, ax + 0.5f, ay + 1, S, open, crumple, wet, -1, true, 3);
        // The abdomen: tapering, banded cream and brown, swaying a hair.
        float sway = MathF.Sin(time * 1.7f) * 0.5f;
        for (int y = 0; y < abd; y++)
        {
            float k = y / MathF.Max(1, abd), half = 1.6f + 0.9f * MathF.Sin(MathF.PI * MathF.Min(1, k * 1.3f)) * (abd > 12 ? 1.3f : 1);
            if (k > 0.8f) half *= (1 - k) * 4;
            for (int x = (int)MathF.Round(ax - half + sway * k); x <= (int)MathF.Round(ax + half + sway * k); x++)
            {
                bool band = y % 3 == 0;
                var c = (x < ax - half * 0.3f) ? (band ? Pal.C("#e8e0b0") : Pal.C("#8a7658")) : band ? Pal.C("#c4bb8a") : Pal.C("#4a3c2c");
                Dot(p, x, (int)(ay + 4 + y), c);
            }
        }
        // The thorax and head.
        Blob(p, ax, ay, 3.2f, 4.4f, 0, (l, x, y) => l > 0.1f ? Pal.C("#6e5a3a") : Pal.C("#3a2d20"));
        Blob(p, ax, ay - 5.4f, 2.1f, 2.1f, 0, (l, x, y) => l > 0.0f ? Pal.C("#4a3a2a") : Pal.C("#241b13"));
        Dot(p, (int)ax - 1, (int)ay - 6, Pal.C("#a8c8a0")); Dot(p, (int)ax + 1, (int)ay - 6, Pal.C("#a8c8a0"));
        // Antennae with a club at the end, moving independently.
        for (int s = -1; s <= 1; s += 2)
        {
            float tx = ax + s * (4 + 1.2f * MathF.Sin(time * 2.3f + s)), ty = ay - 12 + MathF.Sin(time * 1.9f + s * 2) * 0.9f;
            Line(p, ax + s * 0.8f, ay - 7, tx, ty, Pal.C("#2a2018"));
            Dot(p, (int)tx, (int)ty, Pal.C("#3a2d20")); Dot(p, (int)tx + s, (int)ty - 1, Pal.C("#3a2d20"));
        }
        // Legs: three pairs gripping the stem.
        for (int i = 0; i < 3; i++)
            foreach (int s in new[] { -1, 1 })
                Line(p, ax + s * 1.5f, ay - 1 + i * 2, stemX + s * 2.5f, ay + 1 + i * 3, Pal.C("#2a2018"));
        PaintWing(p, ax - 0.5f, ay + 1, S, open, crumple, wet, 1, false, 7);
    }

    // The whole emergence, e from 0 to 1.
    void PaintEmergence(Pix p, float e)
    {
        LeafyBackdrop(p, time);
        const float topX = 50, botX = 54;
        PaintStem(p, topX, botX, 4, 6);
        // Calamansi leaves on short stalks, swaying a little in the air.
        float sway = MathF.Sin(time * 0.9f) * 1.2f;
        LeafShape(p, 22 + sway, 24, 26, 11, -0.5f + sway * 0.03f, "#3f7a37", "#7fb35a", "#2a5a2a");
        Line(p, 36 + sway * 0.5f, 18, StemX(p, topX, botX, 16), 22, Pal.C("#5a7a32"));
        LeafShape(p, 92 - sway, 52, 28, 12, 0.35f - sway * 0.03f, "#3f7a37", "#7fb35a", "#2a5a2a");
        Line(p, 80, 54, StemX(p, topX, botX, 56) + 3, 60, Pal.C("#5a7a32"));
        LeafShape(p, 20 + sway, 70, 26, 11, 0.5f, "#3a7233", "#76ab55", "#285227");
        float stemAtPupa = StemX(p, topX, botX, 56);
        float ripe = Smooth(0.04f, 0.4f, e), spent = Smooth(0.42f, 0.58f, e), split = Smooth(0.44f, 0.54f, e);
        // Before it splits it trembles, faintly.
        float tremble = e > 0.3f && e < 0.5f ? MathF.Sin(time * 40) * 0.35f * Smooth(0.3f, 0.45f, e) : 0;
        PaintPupa(p, stemAtPupa + 6 + tremble, 42, 31, 11, ripe, spent, split, time, stemAtPupa);
        if (e >= 0.5f)
        {
            float out1 = Smooth(0.50f, 0.68f, e), grow = Smooth(0.64f, 0.93f, e), dry = Smooth(0.78f, 0.99f, e);
            // Out of the top of the shell and up the stem: the thorax first, then the swollen abdomen following.
            float ax = StemX(p, topX, botX, 36) + 4 + (1 - out1) * 2, ay = 46 - out1 * 12;
            float S = 5 + grow * 33, abd = 17 - Smooth(0.64f, 0.93f, e) * 7;
            float open = Smooth(0.94f, 1.0f, e) * 0.8f + (e > 0.99f ? (0.1f * MathF.Sin(time * 1.4f)) : 0);
            PaintButterfly(p, ax, ay, S, open, 1 - grow * 0.95f, 1 - dry, abd, time, StemX(p, topX, botX, (int)ay));
        }
        // A bead of moisture falling from the shell as it opens.
        if (e > 0.52f && e < 0.7f)
        {
            float d = (e - 0.52f) / 0.18f;
            Dot(p, (int)(stemAtPupa + 4), (int)(60 + d * 14), Pal.Rgba(220, 240, 250, 0.8f * (1 - d)));
        }
    }

    /* ---------- The four stages ---------- */
    void PaintStage(Pix p, int stage)
    {
        LeafyBackdrop(p, time * 0.5f, "#2c5030", "#86b25e");
        float sw = MathF.Sin(time * 0.8f);
        switch (stage)
        {
            case 0:
            {
                // A young citrus leaf with eggs on it, a breeze lifting it a little.
                LeafShape(p, 30, 22 + sw * 0.6f, 42, 22, -0.25f, "#5f9a42", "#9fcf6a", "#3b6e2f");
                foreach (var (ex, ey) in new[] { (26, 20), (34, 17), (31, 25) })
                {
                    float yy = ey + sw * 0.6f;
                    Blob(p, ex + 1, yy + 1.5f, 2.4f, 1.1f, 0, (l, x, y) => Pal.Rgba(20, 50, 20, 0.35f));
                    Blob(p, ex, yy, 2.2f, 2.2f, 0, (l, x, y) => l > 0.25f ? Pal.C("#fff6c8") : l > -0.05f ? Pal.C("#e8d890") : Pal.C("#b8a860"));
                    Dot(p, ex - 1, (int)yy - 1, Pal.C("#ffffff"));
                }
                break;
            }
            case 1:
            {
                // A leaf with a bitten edge, a small dark young caterpillar and a bigger green one inching along.
                LeafShape(p, 28, 24, 46, 24, 0.15f, "#4f8a3a", "#8fc462", "#2f5e2a");
                for (int i = 0; i < 3; i++) Blob(p, 46 + i * 2.5f, 18 + i * 3, 2.4f, 2.4f, 0, (l, x, y) => Pal.C("#86b25e"));   // nibbled out of the rim
                // The young one: black with a white saddle, like a bird dropping.
                for (int s = 0; s < 5; s++)
                    Blob(p, 14 + s * 2.4f, 26 + MathF.Sin(time * 2 + s * 0.8f) * 0.6f, 1.7f, 1.6f, 0, (l, x, y) => s is 2 or 3 ? Pal.C("#ece8da") : l > 0.1f ? Pal.C("#4a3a30") : Pal.C("#1e1812"));
                // The grown one: a fat green body of rings, an eyespot, a pale band, hunching along in a wave.
                for (int s = 0; s < 9; s++)
                {
                    float wave = MathF.Sin(time * 1.6f - s * 0.7f), bx = 22 + s * 3.2f + wave * 0.5f, by = 14 + s * 0.5f - MathF.Max(0, wave) * 1.4f;
                    Blob(p, bx, by, 2.6f, 2.5f, 0, (l, x, y) => l > 0.25f ? Pal.C("#9fd070") : l > -0.1f ? Pal.C("#7bb24f") : Pal.C("#4f8035"));
                    if (s == 4) Blob(p, bx, by - 0.3f, 1.1f, 1.1f, 0, (l, x, y) => Pal.C("#2a2a20"));
                    if (s is 2 or 3) Dot(p, (int)bx, (int)by + 1, Pal.C("#f0f0c0"));
                }
                Blob(p, 53, 17, 2.4f, 2.4f, 0, (l, x, y) => l > 0 ? Pal.C("#6a9a46") : Pal.C("#3e6a2c"));
                Dot(p, 54, 16, Color.Black);
                break;
            }
            case 2:
            {
                PaintStem(p, 32, 34, 3, 4);
                PaintPupa(p, 37, 7, 26, 10, 0.35f, 0, 0, time, StemX(p, 32, 34, 20));
                LeafShape(p, 12, 14, 22, 9, -0.6f, "#4f8a3a", "#8fc462", "#2f5e2a");
                break;
            }
            default:
            {
                PaintStem(p, 24, 26, 3, 4);
                float open = 0.45f + 0.4f * MathF.Sin(time * 0.9f) * MathF.Sin(time * 0.9f);
                PaintButterfly(p, 30, 14, 15, open, 0, 0, 8, time, StemX(p, 24, 26, 14));
                LeafShape(p, 48, 34, 20, 9, 0.5f, "#4f8a3a", "#8fc462", "#2f5e2a");
                break;
            }
        }
    }

    // The four leaves the caterpillars are offered.
    void PaintFeedLeaf(Pix p, int leaf)
    {
        LeafyBackdrop(p, time * 0.4f, "#2e5232", "#78a456");
        float sw = MathF.Sin(time * 0.7f + leaf) * 0.04f;
        switch (leaf)
        {
            case 0: LeafShape(p, 30, 19, 38, 20, -0.2f + sw, "#3f7a37", "#8bc060", "#27552a"); LeafShape(p, 14, 29, 14, 7, -0.9f, "#5a9a40", "#9bd070", "#2f6a2c"); break;   // calamansi: glossy ovate, a small wing at the stalk
            case 1: LeafShape(p, 30, 19, 40, 17, -0.15f + sw, "#5a9a3a", "#a4d46e", "#34702c", serrated: true); break;   // lemon: paler, toothed
            case 2: LeafShape(p, 30, 19, 50, 11, -0.1f + sw, "#2f6a2e", "#6aa550", "#1d4a22"); break;   // mango: long and narrow, darker
            default:
                for (int g = 0; g < 9; g++)
                {
                    float bx = 8 + g * 5.5f, bend = MathF.Sin(time * 1.1f + g * 0.9f) * 1.8f;
                    for (int y = 0; y < 26; y++)
                    {
                        float k = y / 26f, x = bx + bend * k * k + k * (g % 3 - 1) * 4;
                        Dot(p, (int)x, 35 - y, k < 0.3f ? Pal.C("#5a8a3a") : Pal.C("#86b84a"));
                        if (y < 22 && k > 0.2f) Dot(p, (int)x + 1, 35 - y, Pal.C("#4a7a30"));
                    }
                }
                break;
        }
    }
}
