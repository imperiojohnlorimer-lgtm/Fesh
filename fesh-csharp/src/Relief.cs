using Raylib_cs;

namespace Fesh;

// 1.33: terrain for every island, not only Marilaya. Each island has a profile (how high its hills rise, how broad, whether
// they break into rock steps, how much of its coast is a bank). The height is baked once per pixel (RenderBase) from the
// distance to the coast plus noise, then the ground is lit from the north-west like Marilaya's, the coast gets a bank with a
// face and a lip where it is not a beach, and grass that rises over sand gets a low step. Looks only: nothing here changes a
// tile, a collision or a coordinate. FESH_OLDGROUND=1 draws the old gentle rise for a before-and-after.
partial class Game
{
    static readonly bool OldGround = Environment.GetEnvironmentVariable("FESH_OLDGROUND") == "1";

    // Amp: how tall the hills are (0..1), Hill: the size of a swell in pixels, Ridge: how much of it is long ridges,
    // Rise: tiles from the shore to the top of the rise, Steps: rock steps in the hills (0 none), Bank: the share of the coast
    // that is a bank (0 none), Light: how strongly the slopes catch the light.
    sealed record Terrain(float Amp, int Hill, float Ridge, float Rise, int Steps, float Bank, float Light);

    // Per biome (Marilaya, 7, has its own relief in Marilaya.cs); Amihan's and Habagat's islands each have their own.
    static readonly Terrain[] Terrains =
    {
        new(0.55f, 64, 0.0f, 3.0f, 0, 0.30f, 5f),   // 0 Saltmere
        new(0.90f, 56, 0.35f, 3.0f, 4, 0.55f, 6f),  // 1 Frostfang: tall, rugged, stepped
        new(0.60f, 90, 0.60f, 3.0f, 0, 0.15f, 6f),  // 2 Sunscald: long dune ridges
        new(0.45f, 50, 0.0f, 3.0f, 0, 0.25f, 5f),   // 3 Mirewood: low mounds
        new(0.25f, 60, 0.0f, 2.5f, 0, 0.10f, 4f),   // 4 Starfall Atoll: soft dunes
        null, null, null
    };
    static readonly Terrain[] AmihanTerrains =
    {
        new(0.45f, 72, 0.1f, 3.5f, 0, 0.30f, 5f),   // Amihan Village: a rise toward School Rise
        new(0.85f, 52, 0.30f, 3.0f, 3, 0.50f, 6f),  // Luntian Karsts: hilly, stepped
        new(0.20f, 64, 0.0f, 3.0f, 0, 0.15f, 4f),   // Bakawan: low mangrove flats
        new(0.60f, 90, 0.20f, 4.0f, 0, 0.30f, 5f)   // Baga: a broad skirt round the cone
    };
    static readonly Terrain HabagatFlat = new(0.25f, 64, 0f, 3.0f, 0, 0.20f, 4f), HabagatKnoll = new(0.55f, 40, 0.1f, 2.5f, 2, 0.45f, 5f);

    float[] relief;   // the land's height at every pixel, 0..1 (0 where there is none)
    Terrain[,] terrainAt;   // each tile's terrain, null for the old look

    Terrain TerrainAt(int tx, int ty) => terrainAt == null || tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS ? null : terrainAt[ty, tx];

    Terrain TerrainOfTile(int tx, int ty)
    {
        if (OldGround || tx >= MlStart) return null;
        switch (biome[ty, tx])
        {
            case 5:
                int best = 0;
                double bd = double.MaxValue;
                for (int i = 0; i < AmihanIslands.Length; i++)
                {
                    var a = AmihanIslands[i];
                    double dx = (tx + 0.5 - a.cx) / a.rx, dy = (ty + 0.5 - a.cy) / a.ry, d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; best = i; }
                }
                return AmihanTerrains[best];
            case 6: return tx >= 104 ? HabagatKnoll : HabagatFlat;
            default: return Terrains[biome[ty, tx]];
        }
    }

    // How far a pixel is from water in tiles, blended between tile centres (landDist is one number a tile).
    float LandDistAt(int wx, int wy)
    {
        float fx = (wx + 0.5f) / T - 0.5f, fy = (wy + 0.5f) / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = fx - x0, ay = fy - y0;
        float D(int x, int y) => landDist[Math.Clamp(y, 0, ROWS - 1), Math.Clamp(x, 0, COLS - 1)];
        return (D(x0, y0) * (1 - ax) + D(x0 + 1, y0) * ax) * (1 - ay) + (D(x0, y0 + 1) * (1 - ax) + D(x0 + 1, y0 + 1) * ax) * ay;
    }

    float GenHeight(int wx, int wy, Terrain p)
    {
        float d = LandDistAt(wx, wy);
        if (d <= 0) return 0;
        float edge = 1 - MathF.Exp(-d / p.Rise);
        float n = VNoise(wx, wy, p.Hill, 760) * 0.65f + VNoise(wx, wy, Math.Max(8, p.Hill / 2), 761) * 0.35f;
        float hill = Math.Clamp((n - 0.38f) * 2.4f, 0, 1);
        float ridge = 0;
        if (p.Ridge > 0)
        {
            float r = 1 - MathF.Abs(2 * VNoise(wx, wy, p.Hill * 3 / 2, 762) - 1);
            ridge = r * r * r;
        }
        return Math.Clamp(edge * (0.1f + p.Amp * Math.Clamp(hill * (1 - p.Ridge * 0.5f) + p.Ridge * ridge, 0, 1)), 0, 1);
    }

    // Bakes the height for every tile on or beside land (RenderBase, after ComputeDepth).
    void BakeRelief()
    {
        relief ??= new float[PW * PH];
        Array.Clear(relief);
        terrainAt ??= new Terrain[ROWS, COLS];
        for (int ty = 0; ty < ROWS; ty++)
            for (int tx = 0; tx < COLS; tx++) terrainAt[ty, tx] = TerrainOfTile(tx, ty);
        for (int ty = 0; ty < ROWS; ty++)
            for (int tx = 0; tx < COLS; tx++)
            {
                var p = terrainAt[ty, tx];
                if (p == null) continue;
                bool near = false;
                for (int j = Math.Max(0, ty - 1); j <= Math.Min(ROWS - 1, ty + 1) && !near; j++)
                    for (int i = Math.Max(0, tx - 1); i <= Math.Min(COLS - 1, tx + 1); i++)
                        if (landDist[j, i] > 0) { near = true; break; }
                if (!near) continue;
                for (int py = 0; py < T; py++)
                    for (int px = 0; px < T; px++) relief[(ty * T + py) * PW + tx * T + px] = GenHeight(tx * T + px, ty * T + py, p);
            }
    }

    float Rel(int wx, int wy) => relief == null || wx < 0 || wy < 0 || wx >= PW || wy >= PH ? 0 : relief[wy * PW + wx];

    // Light from the north-west on that height, in -0.45..0.45.
    float ReliefShadeAt(int wx, int wy, Terrain p)
    {
        const int e = 3;
        float gx = Rel(wx + e, wy) - Rel(wx - e, wy), gy = Rel(wx, wy + e) - Rel(wx, wy - e);
        return Math.Clamp((gx + gy) * p.Light, -0.45f, 0.45f);
    }

    // The bank's colours by ground: dark at the waterline, lighter up the face, then a lit lip.
    static readonly Dictionary<char, Color[]> BankFace = new()
    {
        ['g'] = Tones("#3e3526", "#54472f", "#6b5a3f"), ['j'] = Tones("#33301f", "#463f2a", "#5a5036"),
        ['p'] = Tones("#4a3f2e", "#62533a", "#7b6a4a"), ['e'] = Tones("#5d5950", "#767165", "#8f897c"),
        ['D'] = Tones("#92632f", "#ad7a3c", "#c28f50"), ['n'] = Tones("#7d9db3", "#9bbbcc", "#bfd6e2")
    };

    // The 3/4 view of a coast that is a bank: looking from the south you see its face above the water. Returns the face
    // or lip colour, or null when this pixel is plain ground.
    Color? BankPixel(int wx, int wy, char g, Terrain p, Color c)
    {
        if (p.Bank <= 0) return null;
        // Sand below a step of grass sits in its shadow.
        if (g == 's')
        {
            char up = ShapePx(wx, wy - 1);
            return up is 'g' or 'j' && VNoise(wx, wy - 1, 26, 782) < 0.8f ? Pix.Mix(c, ReliefShadow, 0.3f) : null;
        }
        if (!BankFace.TryGetValue(g, out var face)) return null;
        bool bankOn = VNoise(wx, wy, 30, 780) < p.Bank, stepOn = g is 'g' or 'j' && VNoise(wx, wy, 26, 782) < 0.8f;
        if (!bankOn && !stepOn) return null;
        int dw = 0;
        bool step = false;
        for (int k = 1; k <= 6; k++)
        {
            char s = ShapePx(wx, wy + k);
            if (s == g) continue;
            if (s == 's' && stepOn && g is 'g' or 'j') { dw = k; step = true; break; }
            if (Shore(s)) { if (bankOn) continue; break; }
            if (bankOn && s is '~' or 'w') dw = k;
            break;
        }
        if (step)
            return dw == 1 ? face[0] : dw == 2 ? face[1] : dw == 3 ? face[2] : dw == 4 ? Pix.Mix(c, ReliefLight, 0.25f) : null;
        if (dw < 2) return null;
        int h = 2 + (int)(VNoise(wx, 0, 24, 781) * 2.4f);   // a face two to four pixels tall
        if (dw <= h + 1) return face[Math.Min(2, (dw - 2) * 3 / h)];
        if (dw == h + 2) return Pix.Mix(c, ReliefLight, 0.3f);
        return null;
    }

    // Land pixels of a generic island: the old gentle rise replaced by the baked height.
    Color ReliefPixel(int wx, int wy, char g, Color c, float shade, Terrain p)
    {
        c = shade > 0 ? Pix.Mix(c, ReliefLight, shade * 0.5f) : Pix.Mix(c, ReliefShadow, -shade * 0.7f);
        float h = Rel(wx, wy);
        if (g != 's' && h > 0.4f) c = Pix.Mix(c, Pal.C("#dfe8d8"), Math.Min(0.12f, (h - 0.4f) * 0.25f));
        var b = BankPixel(wx, wy, g, p, c);
        if (b != null) return b.Value;
        return p.Steps > 0 && g != 's' ? RockSteps(wx, wy, g, c, shade, p) : c;
    }

    int StepLevel(float h, int steps) => h < 0.2f ? 0 : 1 + Math.Min(steps - 1, (int)((h - 0.2f) * steps / 0.8f));

    // The hills of a rugged island break into broken rock steps like Marilaya's: a face where the ground a pixel or three
    // to the north is a step higher, a shadow at its foot, a lit rim along the top.
    Color RockSteps(int wx, int wy, char g, Color c, float shade, Terrain p)
    {
        int l0 = StepLevel(Rel(wx, wy), p.Steps);
        for (int k = 1; k <= LedgeFaceH; k++)
        {
            int lu = StepLevel(Rel(wx, wy - k), p.Steps);
            if (lu <= l0 || !LedgeOn(wx, wy - k, lu) || !Shore(ShapePx(wx, wy - k))) continue;
            var face = g is 'e' or 'n' ? LedgeFaceRock : LedgeFace;
            var fc = face[Math.Min(face.Length - 1, k - 1 + (Pix.Hash(wx, wy, 741) < 0.18 ? 1 : 0))];
            if (g == 'n') fc = Pix.Mix(fc, Pal.C("#dde8ef"), 0.45f);
            return shade > 0 ? Pix.Mix(fc, ReliefLight, shade * 0.25f) : Pix.Mix(fc, ReliefShadow, -shade * 0.35f);
        }
        int lf = StepLevel(Rel(wx, wy - LedgeFaceH - 1), p.Steps);
        if (lf > l0 && LedgeOn(wx, wy - LedgeFaceH - 1, lf)) return Pix.Mix(c, ReliefShadow, 0.3f);
        int ld = StepLevel(Rel(wx, wy + 1), p.Steps);
        if (ld < l0 && LedgeOn(wx, wy, l0)) return Pix.Mix(c, ReliefLight, 0.32f);
        return c;
    }
}
