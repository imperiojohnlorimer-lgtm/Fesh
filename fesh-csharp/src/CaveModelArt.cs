using Raylib_cs;

namespace Fesh;

// The cave model's cross-section (1.27), painted a pixel at a time from t (0 to 1, a few thousand years a second) and the
// clock. Rain falls and soaks through the soil, cracks creep down through the limestone along a wandering line and widen as
// water works at them, a hollow dissolves out of the rock with a ragged edge (not a rounded rectangle), and in the air of the
// cave each stalactite hangs a drop that swells, falls, splashes, and builds the stalagmite below until the two meet.
partial class Game
{
    const int CmW = 140, CmH = 100, CmGrass = 17, CmSoil = 21, CmRock = 34, CmCx = 70, CmCy = 64;

    // The wandering line of crack k at depth y (x from the surface to y 74).
    static float CmCrackX(int k, int y)
    {
        float x = 30 + k * 38 + (float)Pix.Hash(k, 1, 721) * 12;
        for (int yy = CmGrass; yy < y; yy++) x += ((float)Pix.Hash(k, yy, 722) - 0.5f) * 1.9f + MathF.Sin(yy * 0.13f + k) * 0.25f;
        return x;
    }

    void PaintCaveModel(Pix p, float t)
    {
        float c = Smooth(0.40f, 0.72f, t);
        float rx = 6 + 56 * c, ry = 4 + 25 * c;
        float floorY = CmCy + ry * 0.62f;
        bool InCavity(int x, int y)
        {
            if (c <= 0.001f) return false;
            float dx = (x - CmCx) / rx, dy = (y - CmCy) / ry;
            return dx * dx + dy * dy + (VNoise(x, y, 6, 720) - 0.5f) * 0.36f + (VNoise(x, y, 3, 723) - 0.5f) * 0.12f < 1 && y < floorY;
        }
        float widen = Smooth(0.12f, 0.55f, t);
        // The ground: sky, grass, soil with roots and pebbles, and limestone in strata.
        for (int y = 0; y < CmH; y++)
            for (int x = 0; x < CmW; x++)
            {
                Color col;
                if (y < CmGrass - 1)
                {
                    float g = y / (float)CmGrass;
                    var top = Pix.Mix(Pal.C("#8cc4e0"), Pal.C("#5f7f94"), 1 - Smooth(0.15f, 0.5f, t) < 0.01f ? 0 : 0.5f * (1 - Smooth(0.2f, 0.5f, t)));
                    col = Pix.Mix(top, Pal.C("#d6ecf4"), g * 0.9f);
                    // Clouds drifting, darker while it rains.
                    float cl = VNoise(x + (int)(time * 2), y * 2, 18, 724) * 0.7f + VNoise(x + (int)(time * 3), y, 7, 725) * 0.3f;
                    if (cl > 0.62f - (BayerAt(x, y) - 0.5f) * 0.1f) col = Pix.Mix(col, t < 0.5f ? Pal.C("#aeb9c4") : Pal.C("#ffffff"), 0.6f);
                }
                else if (y < CmSoil)
                {
                    col = y == CmGrass - 1 ? Pal.C("#7cc25a") : Pix.Mix(Pal.C("#4a8a3a"), Pal.C("#5d9b45"), (float)Pix.Hash(x, y, 726));
                    if (y == CmGrass && Pix.Hash(x, 0, 727) < 0.45) col = Pal.C("#3f7a32");
                }
                else if (y < CmRock)
                {
                    float v = (float)Pix.Hash(x, y, 728);
                    col = v < 0.12f ? Pal.C("#3a2a1c") : v > 0.9f ? Pal.C("#a08868") : Pix.Mix(Pal.C("#5a3f28"), Pal.C("#6b4a2b"), (BayerAt(x, y) + (y - CmSoil) / 13f) * 0.5f);
                }
                else
                {
                    // Limestone: pale beds with darker seams, a little warmer deeper down.
                    int bed = (int)((y + VNoise(x, y, 40, 729) * 5) / 9);
                    float bedTone = (float)Pix.Hash(bed, 0, 730);
                    col = Pix.Mix(Pal.C("#e4ddc8"), Pal.C("#cdc5ac"), bedTone * 0.7f + (BayerAt(x, y) - 0.5f) * 0.18f);
                    if ((y + (int)(VNoise(x, 0, 40, 729) * 5)) % 9 == 0) col = Pix.Mix(col, Pal.C("#a89f84"), 0.55f);
                    if (Pix.Hash(x, y, 731) < 0.03) col = Pix.Mix(col, Pal.C("#8a8068"), 0.5f);
                }
                p.Buf[y * CmW + x] = col;
            }
        // A tree at the left with its roots going down into the soil.
        PaintCaveTree(p, 16);
        // Shells in the limestone (it was a sea floor once): little pale curls.
        for (int i = 0; i < 9; i++)
        {
            int sx = 8 + (int)(Pix.Hash(i, 1, 732) * 124), sy = 44 + (int)(Pix.Hash(i, 2, 732) * 48);
            if (InCavity(sx, sy)) continue;
            Dot(p, sx, sy, Pal.C("#f6f2e2")); Dot(p, sx + 1, sy, Pal.C("#f6f2e2")); Dot(p, sx, sy + 1, Pal.C("#b8ae94")); Dot(p, sx + 1, sy - 1, Pal.C("#d8d0b8"));
        }
        // Rain, while it's raining: slanting drops, splashing on the grass and darkening the soil under them.
        float rain = 1 - Smooth(0.38f, 0.5f, t);
        if (rain > 0.02f)
            for (int i = 0; i < 46; i++)
            {
                float fall = 34, sx = (float)Wrap(Pix.Hash(i, 1, 733) * CmW - time * 6, CmW), sy = (float)Wrap(Pix.Hash(i, 2, 733) * CmGrass + time * fall * (0.8 + Pix.Hash(i, 3, 733) * 0.5), CmGrass - 1);
                if (Pix.Hash(i, 4, 733) < rain) { Dot(p, (int)sx, (int)sy, Pal.C("#3a7ab8")); Dot(p, (int)sx - 1, (int)sy - 1, Pal.Rgba(58, 122, 184, 0.6f)); }
            }
        // Cracks: thin at first, widening where the water works, with the water itself running down them in beads.
        for (int k = 0; k < 3; k++)
        {
            for (int y = CmRock - 12; y <= 76; y++)
            {
                float x = CmCrackX(k, y);
                float depth = Smooth(CmRock - 12, 76, y);
                // The crack creeps down with time, then widens.
                if (y > CmRock - 12 + (76 - (CmRock - 12)) * Smooth(0.0f, 0.34f, t)) break;
                int w = 1 + (int)(widen * (1.2f + 2.4f * (1 - depth * 0.5f)));
                for (int dx = 0; dx < w; dx++)
                {
                    int px = (int)MathF.Round(x) + dx;
                    if (InCavity(px, y)) continue;
                    Dot(p, px, y, w > 1 && dx > 0 && dx < w - 1 ? (t < 0.6f ? Pal.C("#5a93b0") : Pal.C("#2a2622")) : Pal.C("#4a4234"));
                }
            }
            // Beads of water sliding down.
            if (t < 0.62f && t > 0.04f)
                for (int j = 0; j < 3; j++)
                {
                    float ph = (time * 0.35f + j * 0.33f + k * 0.17f) % 1;
                    int y = (int)(CmRock - 12 + ph * (76 - (CmRock - 12)) * Smooth(0.0f, 0.34f, t));
                    int x = (int)MathF.Round(CmCrackX(k, y));
                    if (!InCavity(x, y)) { Dot(p, x, y, Pal.C("#9fd8f0")); Dot(p, x, y - 1, Pal.Rgba(159, 216, 240, 0.6f)); }
                }
        }
        // The hollow: dark, damp, with a pale dissolved rim.
        if (c > 0.001f)
            for (int y = (int)(CmCy - ry - 3); y <= CmCy + ry + 3; y++)
                for (int x = (int)(CmCx - rx - 3); x <= CmCx + rx + 3; x++)
                {
                    if (x < 0 || y < 0 || x >= CmW || y >= CmH) continue;
                    if (InCavity(x, y))
                    {
                        float k = (y - (CmCy - ry)) / (floorY - (CmCy - ry));
                        var dark = Pix.Mix(Pal.C("#2b241c"), Pal.C("#54483a"), Math.Clamp(k, 0, 1) * 0.85f + (VNoise(x, y, 5, 737) - 0.5f) * 0.3f + (BayerAt(x, y) - 0.5f) * 0.18f);
                        p.Buf[y * CmW + x] = dark;
                    }
                    else if (InCavity(x - 1, y) || InCavity(x + 1, y) || InCavity(x, y - 1) || InCavity(x, y + 1) || InCavity(x + 1, y + 1) || InCavity(x - 1, y - 1))
                        p.Buf[y * CmW + x] = Pix.Mix(p.Buf[y * CmW + x], Pal.C("#9a9078"), 0.55f);
                }
        // The floor of the cave and a pool on it once it's open, with the drips' rings.
        if (c > 0.5f)
        {
            int fy = (int)floorY;
            for (int x = (int)(CmCx - rx * 0.8f); x <= CmCx + rx * 0.8f; x++)
                if (InCavity(x, fy - 1)) { Dot(p, x, fy, Pal.C("#6b6454")); Dot(p, x, fy + 1, Pal.C("#4f4a3c")); }
            int pw = (int)(rx * 0.55f);
            float poolA = Smooth(0.55f, 0.75f, t);
            for (int x = CmCx - pw; x <= CmCx + pw; x++)
                for (int y = fy - 2; y <= fy; y++)
                    if (InCavity(x, y - 1) && Math.Abs(x - CmCx) * 1.0f / pw + (fy - y) * 0.2f < 1 && poolA > 0)
                        Dot(p, x, y, Pix.Mix(Pal.C("#173344"), Pal.C(((x + y + (int)(time * 3)) % 7 == 0) ? "#5fa3c0" : "#2d6a86"), poolA));
        }
        // Stalactites down from the roof and stalagmites up from the floor, growing, with the drop each one hangs and drips.
        float grow = Smooth(0.68f, 0.96f, t);
        if (grow > 0.01f && c > 0.7f)
        {
            int[] xs = { -42, -29, -16, -3, 10, 24, 38 };
            for (int i = 0; i < xs.Length; i++)
            {
                int sx = CmCx + (int)(xs[i] * (0.6f + 0.4f * c)) + (int)((Pix.Hash(i, 1, 734) - 0.5) * 4);
                // Where the roof is over this x.
                float roofY = CmCy - ry * MathF.Sqrt(MathF.Max(0.01f, 1 - MathF.Pow((sx - CmCx) / rx, 2))) + 2;
                int floorAt = (int)floorY;
                if (roofY >= floorAt - 6 || MathF.Abs(sx - CmCx) > rx * 0.85f) continue;
                float len = grow * (5 + (float)Pix.Hash(i, 2, 734) * 14), maxLen = floorAt - roofY - 4;
                bool meet = i == 3 && t > 0.975f;   // the middle pair join into a column
                len = meet ? floorAt - roofY - 1 : Math.Min(len, maxLen * 0.7f);   // the joined column runs right down to the floor
                int baseW = 5 - (i % 2);
                for (int y = 0; y < len; y++)
                {
                    float k = y / MathF.Max(1, len);
                    int wd = Math.Max(1, (int)MathF.Round(baseW * (1 - k * (meet ? 0.2f : 0.9f))));
                    for (int dx = -wd / 2; dx <= wd / 2; dx++)
                    {
                        float u = wd <= 1 ? 0 : dx / (wd / 2f);
                        var tone = u < -0.3f ? Pal.C("#f1ead2") : u < 0.4f ? Pal.C("#d8d0b4") : Pal.C("#a89f84");
                        if (Pix.Hash(sx + dx, y, 735) < 0.1) tone = Pix.Mix(tone, Pal.C("#8a8068"), 0.5f);
                        Dot(p, sx + dx, (int)roofY + y, tone);
                    }
                }
                // The drop on its tip: swelling for a while, then falling faster and faster, a splash, a ring.
                float ph = (time * 0.42f + i * 0.37f) % 1, tipY = roofY + len;
                if (c > 0.85f && !meet)
                {
                    if (ph < 0.62f)
                    {
                        float sz = ph / 0.62f;
                        Dot(p, sx, (int)tipY + 1, Pal.C("#bfe6f4"));
                        if (sz > 0.5f) { Dot(p, sx, (int)tipY + 2, Pal.C("#9fd8f0")); Dot(p, sx - 1, (int)tipY + 2, Pal.Rgba(191, 230, 244, 0.5f)); }
                    }
                    else
                    {
                        float k2 = (ph - 0.62f) / 0.38f, kf = Math.Min(1, k2 / 0.85f), fallTo = floorAt - 2, y = tipY + 2 + (fallTo - tipY - 2) * kf * kf;
                        if (k2 < 0.85f) { Dot(p, sx, (int)y, Pal.C("#bfe6f4")); Dot(p, sx, (int)y - 1, Pal.Rgba(191, 230, 244, 0.45f)); }
                        else
                        {
                            float r = (k2 - 0.85f) / 0.15f * 4;
                            for (int a = 0; a < 8; a++) Dot(p, sx + (int)MathF.Round(MathF.Cos(a * 0.785f) * r), floorAt - 1 + (int)MathF.Round(MathF.Sin(a * 0.785f) * r * 0.35f), Pal.Rgba(191, 230, 244, 0.7f * (1 - r / 4)));
                        }
                    }
                }
                // The stalagmite beneath, shorter and blunter, rising as the drops bring their calcite.
                float h = meet ? 0 : grow * (3 + (float)Pix.Hash(i, 3, 734) * 7);
                for (int y = 0; y < h; y++)
                {
                    float k = y / MathF.Max(1, h);
                    int wd = Math.Max(1, (int)MathF.Round(5 * (1 - k * 0.8f)));
                    for (int dx = -wd / 2; dx <= wd / 2; dx++)
                        Dot(p, sx + dx, floorAt - 1 - y, dx < 0 ? Pal.C("#e8e0c6") : dx > 0 ? Pal.C("#a89f84") : Pal.C("#d2caae"));
                }
            }
        }
    }

    static void PaintCaveTree(Pix p, int tx)
    {
        // Trunk and a lump of crown above the grass, roots below.
        for (int y = CmGrass - 15; y < CmGrass; y++) for (int x = tx - 1; x <= tx + 1; x++) Dot(p, x, y, x == tx - 1 ? Pal.C("#8a6239") : x == tx ? Pal.C("#6b4a2b") : Pal.C("#4e3520"));
        Blob(p, tx, CmGrass - 17, 9, 6, 0, (l, x, y) => l > 0.25f ? Pal.C("#5aa047") : l > -0.1f ? Pal.C("#3f7d35") : Pal.C("#27542a"));
        for (int r = -1; r <= 1; r += 2)
        {
            float x = tx;
            for (int y = CmGrass; y < CmSoil + 8; y++)
            {
                x += r * (0.22f + (float)Pix.Hash(r + 3, y, 736) * 0.35f);
                Dot(p, (int)MathF.Round(x), y, y < CmSoil ? Pal.C("#8a6239") : Pal.C("#9a7a52"));
            }
        }
        Line(p, tx, CmGrass, tx, CmSoil + 10, Pal.C("#9a7a52"));
    }
}
