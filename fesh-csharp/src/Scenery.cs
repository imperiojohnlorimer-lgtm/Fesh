using Raylib_cs;

namespace Fesh;

sealed class Leaf { public float X, Y, Vx, Vy, Life, Ground, Phase; public Color Color; }

// How the outdoors looks. The baked ground: soft colour patches, ragged borders where grass meets sand, coasts rounded
// off at the tile corners, and water that darkens with depth. Then the shoreline foam, trees and bushes at their real
// size (two or three times your height), grass that sways, cloud shadows and falling leaves.
// Collisions and interactions stay on whole tiles; only the pictures change.
partial class Game
{
    const int PW = COLS * T, PH = ROWS * T;
    // Every outdoor pixel's ground once the coast is rounded off (a ground or water tile letter), filled with the base layer.
    readonly char[] shape = new char[PW * PH];
    // How deep the water is under each tile: 0 on land, 1 in the shallows and at a pond's edge, more further out.
    readonly float[,] depth = new float[ROWS, COLS];
    // Water pixels touching land, with the way out to sea, for the foam.
    readonly List<(short x, short y, sbyte nx, sbyte ny, char w)> shore = new();
    readonly List<Leaf> leaves = new();
    readonly Random fxRng = new();   // for looks only, so scenery never changes what the game's own rng gives
    float FxRand(float a, float b) => a + (float)fxRng.NextDouble() * (b - a);
    float leafT;

    static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    static float BayerAt(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

    static Color[] Tones(params string[] c) => c.Select(Pal.C).ToArray();
    // Each ground in three tones (dark, mid, light), laid down in soft patches.
    static readonly Dictionary<char, Color[]> GroundTones = new()
    {
        ['g'] = Tones("#548f40", "#5d9b45", "#67a74b"),
        ['j'] = Tones("#3e7836", "#45833d", "#4d8e43"),
        ['s'] = Tones("#e0c68c", "#e8cf96", "#efd9a6"),
        ['p'] = Tones("#c6a872", "#cdb07a", "#d5ba86"),
        ['n'] = Tones("#dde8ef", "#e8f0f4", "#f3f8fa"),
        ['e'] = Tones("#a29d91", "#b5b0a4", "#c7c2b5"),
        ['D'] = Tones("#e0ae64", "#e9b96e", "#efc37d"),
        ['i'] = Tones("#b5dded", "#bfe3f0", "#cae8f3"),
        ['k'] = Tones("#b5dded", "#bfe3f0", "#cae8f3")
    };
    // Where land meets water, the ground darkens for a pixel (wet sand, damp grass).
    static readonly Dictionary<char, Color> WetEdge = new()
    {
        ['s'] = Pal.C("#d2b47c"), ['p'] = Pal.C("#b89b63"), ['g'] = Pal.C("#477f38"), ['j'] = Pal.C("#2f612c"),
        ['n'] = Pal.C("#c9d6de"), ['e'] = Pal.C("#9a9589"), ['D'] = Pal.C("#cf9f55"), ['i'] = Pal.C("#9fcfe2")
    };
    // The sea from the foamy edge out to the deep, and the ponds from their edge to their middle.
    // Tones 0-1 are the shallows (wadeable), 2 and up the deep; ShallowRim marks the line between them.
    static readonly Color[] SeaTones = Tones("#3d97b8", "#2f7fa3", "#266893", "#225e89", "#1f5580", "#1c4d77");
    static readonly Color ShallowRim = Pal.C("#21597f");
    static readonly Dictionary<char, Color[]> PondTones = new()
    {
        ['l'] = Tones("#3fb5a5", "#2a9d8f", "#228a7e"), ['o'] = Tones("#3cc4bb", "#22a6a0", "#1b928d"), ['m'] = Tones("#5e7449", "#4f6440", "#44573a")
    };

    static bool Wet(char g) => g is '~' or 'w' or 'l' or 'o' or 'm' or 'T';
    static bool Shore(char g) => g is 's' or 'g' or 'p' or 'n' or 'e' or 'D' or 'j';
    // What the land's corners round off against: water, and Frostfang's frozen lake.
    static bool Pool(char g) => Wet(g) || g == 'i';
    static bool SeaLike(char g) => g is '~' or 'w' or 'T' or 'r' or 'I' or 'x' or 'd' or 'b';
    // Which ground spreads into which where two meet: grass creeps over sand, snow over gravel, and so on.
    static int Prio(char g) => g switch { 'j' => 7, 'g' => 6, 'n' => 5, 'D' => 4, 'e' => 3, 's' => 2, 'p' => 1, _ => 0 };

    // Each tile's ground, worked out once per bake (RenderBase, and round a tile RenderTile repaints).
    readonly char[,] groundCache = new char[ROWS, COLS];
    char GroundAt(int tx, int ty) => tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS ? '~' : groundCache[ty, tx];

    // Smooth value noise in 0..1 with cells of the given size in pixels.
    static float VNoise(int x, int y, int cell, int seed)
    {
        int cx = x / cell, cy = y / cell;
        float fx = (x % cell + 0.5f) / cell, fy = (y % cell + 0.5f) / cell;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = (float)Pix.Hash(cx, cy, seed), b = (float)Pix.Hash(cx + 1, cy, seed);
        float c = (float)Pix.Hash(cx, cy + 1, seed), d = (float)Pix.Hash(cx + 1, cy + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    /* ---------- The baked ground ---------- */
    // Which ground a world pixel shows once the coast is rounded: land tiles lose their corners to the water, and the
    // water's inside corners fill in with land. Each corner is a quarter circle about the tile's middle.
    char ShapeAt(int wx, int wy)
    {
        char s = CoastShape(wx, wy);
        if (s is 'w' or '~') return SeaShape(wx, wy, s);
        if (!Shore(s)) return s;
        char g = GroundShape(wx, wy, s);
        return g == 'p' && wx >= MlStart * T ? MlFootpath(wx, wy) : g;
    }

    // Marilaya's trail and lanes are whole tiles of path underfoot, but they're drawn as a footpath a few pixels wide
    // along their line, winding through whatever ground is either side.
    char MlFootpath(int wx, int wy)
    {
        float lx = MlLx(wx + 0.5f), ly = MlLy(wy + 0.5f);
        float d = Math.Min(PolyDist(lx, ly, MlTrail), PolyDist(lx, ly, MlLanes)) * MlS * T;
        if (d < 2.6f + (VNoise(wx, wy, 5, 369) - 0.5f) * 2f) return 'p';
        // Off the path: the ground most of the tiles round it are.
        int tx = wx / T, ty = wy / T;
        Span<char> seen = stackalloc char[8];
        Span<int> count = stackalloc int[8];
        int kinds = 0, best = -1;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                char c = GroundAt(tx + ox, ty + oy);
                if (c == 'p' || !Shore(c)) continue;
                int k = 0;
                while (k < kinds && seen[k] != c) k++;
                if (k == kinds) { if (kinds == 8) continue; seen[kinds] = c; count[kinds++] = 0; }
                count[k]++;
                if (best < 0 || count[k] > count[best]) best = k;
            }
        return best < 0 ? 'g' : seen[best];
    }

    char CoastShape(int wx, int wy)
    {
        int tx = wx / T, ty = wy / T, px = wx - tx * T, py = wy - ty * T;
        char g = GroundAt(tx, ty);
        bool land = Shore(g);
        if (!land && !Pool(g)) return g;
        int cx = px < T / 2 ? -1 : 1, cy = py < T / 2 ? -1 : 1;
        char a = GroundAt(tx + cx, ty), b = GroundAt(tx, ty + cy), c = GroundAt(tx + cx, ty + cy);
        float dx = px + 0.5f - T / 2f, dy = py + 0.5f - T / 2f;
        // Water that only touches at a tile's corner (a creek running diagonally) flows on through it in a smooth channel,
        // rather than showing as squares that meet at a point.
        if (!land && Wet(g) && SameWater(g, c) && Shore(a) && Shore(b))
        {
            // Distance from the line joining this tile's middle to the diagonal tile's middle.
            float off = MathF.Abs(dx * cy - dy * cx) / 1.4142f;
            return off < ChannelHalf || dx * dx + dy * dy <= T * T / 4f ? g : (MathF.Abs(dx) > MathF.Abs(dy) ? a : b);
        }
        if (land && Wet(a) && SameWater(a, b) && Shore(c))
        {
            // The same corner seen from one of the two land tiles beside it: the channel cuts across its corner.
            float ox = dx - cx * T / 2f, oy = dy - cy * T / 2f;   // from the shared corner
            if (MathF.Abs(ox * cy + oy * cx) / 1.4142f < ChannelHalf) return a;
        }
        // Otherwise the coast is traced through the tile middles, eased, with a pixel of wobble: a straight shore stays on
        // the tile edge, a corner comes out round and a staircase of tiles becomes a smooth slope. Anything that's neither
        // land nor water (a dock, a rock in the shallows) counts as this tile's own kind, so nothing changes beside it.
        float wob = (VNoise(wx, wy, 9, 367) - 0.5f) * 2.4f, wob2 = (VNoise(wx + 3000, wy, 9, 368) - 0.5f) * 2.4f;
        float fx = (wx + 0.5f + wob) / T - 0.5f, fy = (wy + 0.5f + wob2) / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = Ease(fx - x0), ay = Ease(fy - y0);
        float Water(char t) => Pool(t) ? 1 : Shore(t) ? 0 : land ? 0 : 1;
        char c00 = GroundAt(x0, y0), c10 = GroundAt(x0 + 1, y0), c01 = GroundAt(x0, y0 + 1), c11 = GroundAt(x0 + 1, y0 + 1);
        float w00 = (1 - ax) * (1 - ay), w10 = ax * (1 - ay), w01 = (1 - ax) * ay, w11 = ax * ay;
        float v = Water(c00) * w00 + Water(c10) * w10 + Water(c01) * w01 + Water(c11) * w11;
        if (land ? v < 0.5f : v >= 0.5f) return g;
        // This pixel goes over to the other side: it takes the ground (or water) of the nearest tile of that kind.
        char best = g; float bw = -1;
        void Try(char t, float w) { if ((land ? Pool(t) : Shore(t)) && w > bw) { bw = w; best = t; } }
        Try(c00, w00); Try(c10, w10); Try(c01, w01); Try(c11, w11);
        return best;
    }
    const float ChannelHalf = 4.2f;
    // Two waters that join up through a corner: the same pond or lagoon, or both the sea.
    static bool SameWater(char a, char b) => a == b || (SeaLike(a) && SeaLike(b) && Wet(a) && Wet(b));

    // Smooth weights for blending between tile middles (bilinear, eased so corners come out round).
    static float Ease(float t) => t * t * (3 - 2 * t);

    // Where the shallows end. The sea's tiles are only shallow or deep, so the line between them used to step along
    // the tile grid. Here it's traced through the tile middles instead, with a pixel or so of wobble: a straight run
    // stays exactly on the tile edge, a staircase becomes a slope and a corner comes out round. It stays a sharp line
    // (GroundPixel's ShallowRim), so the wading limit still reads.
    char SeaShape(int wx, int wy, char s)
    {
        float warpX = (VNoise(wx, wy + 5000, 11, 361) - 0.5f) * 3f, warpY = (VNoise(wx + 5000, wy, 11, 362) - 0.5f) * 3f;
        float fx = (wx + 0.5f + warpX) / T - 0.5f, fy = (wy + 0.5f + warpY) / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = Ease(fx - x0), ay = Ease(fy - y0);
        float Deep(int x, int y) => GroundAt(x, y) is '~' or 'd' or 'b' ? 1 : 0;
        float v = (Deep(x0, y0) * (1 - ax) + Deep(x0 + 1, y0) * ax) * (1 - ay) + (Deep(x0, y0 + 1) * (1 - ax) + Deep(x0 + 1, y0 + 1) * ax) * ay;
        // Only the sea's own two letters swap; a reef rock, a jetty or the ice keep theirs.
        int tx = wx / T, ty = wy / T;
        if (GroundAt(tx, ty) is not ('w' or '~')) return s;
        return v >= 0.5f ? '~' : 'w';
    }

    // Which ground a land pixel shows. Each tile's ground fills its middle, and between tile middles the grounds blend
    // the same eased way as the shallows, with a ragged edge from two scales of noise, so grass meets sand, a forest
    // meets a meadow and a path wanders in soft curves instead of along the tile grid. A stronger ground (Prio) wins a
    // little more than half the way.
    char GroundShape(int wx, int wy, char s)
    {
        // The sampling point wanders a few pixels (two scales), so long straight runs of tiles become gentle waves.
        float warpX = (VNoise(wx, wy + 7000, 17, 363) - 0.5f) * 8f + (VNoise(wx, wy + 7000, 6, 365) - 0.5f) * 3f;
        float warpY = (VNoise(wx + 7000, wy, 17, 364) - 0.5f) * 8f + (VNoise(wx + 7000, wy, 6, 366) - 0.5f) * 3f;
        float fx = (wx + 0.5f + warpX) / T - 0.5f, fy = (wy + 0.5f + warpY) / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = Ease(fx - x0), ay = Ease(fy - y0);
        Span<char> gs = stackalloc char[4];
        Span<float> ws = stackalloc float[4];
        gs[0] = GroundAt(x0, y0); ws[0] = (1 - ax) * (1 - ay);
        gs[1] = GroundAt(x0 + 1, y0); ws[1] = ax * (1 - ay);
        gs[2] = GroundAt(x0, y0 + 1); ws[2] = (1 - ax) * ay;
        gs[3] = GroundAt(x0 + 1, y0 + 1); ws[3] = ax * ay;
        // All one ground (or only water besides): nothing to blend.
        bool mixed = false;
        for (int i = 0; i < 4; i++) if (Shore(gs[i]) && gs[i] != s) { mixed = true; break; }
        if (!mixed) return s;
        char best = s; float bestScore = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            char g = gs[i];
            if (!Shore(g)) continue;
            float w = 0;
            for (int j = 0; j < 4; j++) if (gs[j] == g) w += ws[j];
            float n = (VNoise(wx, wy, 5, 370 + g) - 0.5f) * 0.3f + (VNoise(wx, wy, 2, 380 + g) - 0.5f) * 0.16f;
            float score = w + n + Prio(g) * 0.025f;
            if (score > bestScore) { bestScore = score; best = g; }
        }
        return best;
    }

    void ShapeTile(int x, int y)
    {
        // A tile among eight of the same can't take anything from its neighbours (every blend and wobble above reaches
        // less than a tile), so it's filled straight away; that's most of the map, and keeps the bake quick.
        char g = GroundAt(x, y);
        bool same = !(g == 'p' && x >= MlStart);
        for (int oy = -1; oy <= 1 && same; oy++)
            for (int ox = -1; ox <= 1 && same; ox++) same = GroundAt(x + ox, y + oy) == g;
        if (same)
        {
            for (int py = 0; py < T; py++) Array.Fill(shape, g, (y * T + py) * PW + x * T, T);
            return;
        }
        for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++) shape[(y * T + py) * PW + x * T + px] = ShapeAt(x * T + px, y * T + py);
    }

    char ShapePx(int wx, int wy) => wx < 0 || wy < 0 || wx >= PW || wy >= PH ? '~' : shape[wy * PW + wx];

    // Depth per tile: shallows 1, open sea 2 and more the further it is from anything else (a chamfer distance, so the
    // deep shades off in rounded bands rather than diamonds); ponds 1 at the edge, 2 inside.
    void ComputeDepth()
    {
        const float Far = 999, Diag = 1.41f;
        var dist = new float[ROWS, COLS];
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                char g = GroundAt(x, y);
                bool deep = g is '~' or 'd' or 'b';
                dist[y, x] = deep ? Far : 0;
                depth[y, x] = deep ? 0 : g switch
                {
                    'w' or 'T' or 'r' or 'I' or 'x' => 1,
                    'l' or 'o' or 'm' => Wet(GroundAt(x - 1, y)) && Wet(GroundAt(x + 1, y)) && Wet(GroundAt(x, y - 1)) && Wet(GroundAt(x, y + 1)) ? 2 : 1,
                    _ => 0
                };
            }
        float At(int x, int y) => x < 0 || y < 0 || x >= COLS || y >= ROWS ? Far : dist[y, x];
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
                dist[y, x] = MathF.Min(dist[y, x], MathF.Min(MathF.Min(At(x - 1, y) + 1, At(x, y - 1) + 1), MathF.Min(At(x - 1, y - 1) + Diag, At(x + 1, y - 1) + Diag)));
        for (int y = ROWS - 1; y >= 0; y--)
            for (int x = COLS - 1; x >= 0; x--)
                dist[y, x] = MathF.Min(dist[y, x], MathF.Min(MathF.Min(At(x + 1, y) + 1, At(x, y + 1) + 1), MathF.Min(At(x + 1, y + 1) + Diag, At(x - 1, y + 1) + Diag)));
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
                if (dist[y, x] > 0) depth[y, x] = 1 + dist[y, x];
        // The same chamfer distance the other way: how far each land tile is from any water.
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++) dist[y, x] = Shore(GroundAt(x, y)) ? Far : 0;
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
                dist[y, x] = MathF.Min(dist[y, x], MathF.Min(MathF.Min(At(x - 1, y) + 1, At(x, y - 1) + 1), MathF.Min(At(x - 1, y - 1) + Diag, At(x + 1, y - 1) + Diag)));
        for (int y = ROWS - 1; y >= 0; y--)
            for (int x = COLS - 1; x >= 0; x--)
                dist[y, x] = MathF.Min(dist[y, x], MathF.Min(MathF.Min(At(x + 1, y) + 1, At(x, y + 1) + 1), MathF.Min(At(x + 1, y + 1) + Diag, At(x - 1, y + 1) + Diag)));
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++) landDist[y, x] = dist[y, x] >= Far ? 0 : dist[y, x];
    }

    // Depth at a pixel, blended between tile centres so the water shades off smoothly.
    float DepthAt(int wx, int wy)
    {
        float fx = (wx + 0.5f) / T - 0.5f, fy = (wy + 0.5f) / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = fx - x0, ay = fy - y0;
        float D(int x, int y) => depth[Math.Clamp(y, 0, ROWS - 1), Math.Clamp(x, 0, COLS - 1)];
        float top = D(x0, y0) + (D(x0 + 1, y0) - D(x0, y0)) * ax, bottom = D(x0, y0 + 1) + (D(x0 + 1, y0 + 1) - D(x0, y0 + 1)) * ax;
        return top + (bottom - top) * ay;
    }

    // Dithers between two neighbouring tones of a ramp at a fractional level.
    static Color RampAt(Color[] ramp, float level, int wx, int wy)
    {
        level = Math.Clamp(level, 0, ramp.Length - 1);
        int i = (int)level;
        return i + 1 < ramp.Length && level - i > BayerAt(wx, wy) ? ramp[i + 1] : ramp[i];
    }

    // The colour of one ground pixel before any details go on top.
    Color GroundPixel(int wx, int wy, char g)
    {
        if (SeaLike(g))
        {
            // The wading limit stays sharp: shallows never take a deep tone or the deep a shallow one, and a darker line
            // marks exactly where the shallows end.
            float d = DepthAt(wx, wy);
            if (g is not ('~' or 'd' or 'b'))
            {
                bool rim = ShapePx(wx + 1, wy) == '~' || ShapePx(wx - 1, wy) == '~' || ShapePx(wx, wy + 1) == '~' || ShapePx(wx, wy - 1) == '~';
                return rim ? ShallowRim : RampAt(SeaTones, Math.Min(d, 1), wx, wy);
            }
            // Out past the shallows the deep water gets slow, uneven shading, like banks and holes on the sea floor.
            float far = Math.Clamp((d - 2) / 2, 0, 1);
            return RampAt(SeaTones, Math.Max(2, d + (VNoise(wx, wy, 48, 175) - 0.5f) * 1.2f * far), wx, wy);
        }
        if (PondTones.TryGetValue(g, out var pond)) return RampAt(pond, DepthAt(wx, wy), wx, wy);
        if (!GroundTones.TryGetValue(g, out var tones)) return Pal.C(TileCol.TryGetValue(g, out var tc) ? tc.Item1 : "#e8cf96");
        int tx = wx / T;
        if (Shore(g) && WetEdge.TryGetValue(g, out var wet)
            && (Pool(ShapePx(wx + 1, wy)) || Pool(ShapePx(wx - 1, wy)) || Pool(ShapePx(wx, wy + 1)) || Pool(ShapePx(wx, wy - 1)))) return wet;
        float v = VNoise(wx, wy, 26, 170) * 0.65f + VNoise(wx, wy, 8, 171) * 0.35f + (BayerAt(wx, wy) - 0.5f) * 0.16f;
        // Marilaya's hills and Tindig are shaded by the light from the north-west (Marilaya.cs), through the same three
        // tones, and a fourth, darker one in the deepest shade.
        if (tx >= MlStart)
        {
            float shade = MlShade(wx, wy);
            v += shade * 0.6f;
            var c = tones[v < 0.38f ? 0 : v > 0.62f ? 2 : 1];
            return MlRelief(wx, wy, g, c, shade);
        }
        // Everywhere else the islands rise gently from their shores into low rolling ground, lit the same way.
        float isle = IsleShade(wx, wy);
        v += isle * 0.5f;
        var col = tones[v < 0.38f ? 0 : v > 0.62f ? 2 : 1];
        return isle > 0 ? Pix.Mix(col, ReliefLight, isle * 0.4f) : Pix.Mix(col, ReliefShadow, -isle * 0.55f);
    }

    // How far each land tile is from water (tiles), for the islands' gentle rise (ComputeDepth fills it).
    readonly float[,] landDist = new float[ROWS, COLS];

    // The height of the land at a pixel: rising from the shore and levelling off inland, with broad low swells.
    float IsleHeight(float wx, float wy)
    {
        float fx = wx / T - 0.5f, fy = wy / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = fx - x0, ay = fy - y0;
        float D(int x, int y) => landDist[Math.Clamp(y, 0, ROWS - 1), Math.Clamp(x, 0, COLS - 1)];
        float d = (D(x0, y0) * (1 - ax) + D(x0 + 1, y0) * ax) * (1 - ay) + (D(x0, y0 + 1) * (1 - ax) + D(x0 + 1, y0 + 1) * ax) * ay;
        float rise = 1 - MathF.Exp(-d / 3.2f);
        return rise + (VNoise((int)wx, (int)wy, 70, 750) - 0.5f) * 0.9f * Math.Min(1, d / 5);
    }

    // Light from the north-west on that height, -0.3 to 0.3.
    float IsleShade(int wx, int wy)
    {
        const float e = 4;
        float gx = (IsleHeight(wx + e, wy) - IsleHeight(wx - e, wy)) / (2 * e), gy = (IsleHeight(wx, wy + e) - IsleHeight(wx, wy - e)) / (2 * e);
        return Math.Clamp((gx + gy) * 9f, -0.3f, 0.3f);
    }

    /* ---------- Relief ---------- */
    // Marilaya's hills in 3/4 view: slopes facing the light (north-west) warm and lighter, slopes facing away cool and
    // darker, and the height cut into a few broken steps. Where a step's edge faces you (the higher ground to the north)
    // there's a low rock face under a lit rim; where it faces away, only a soft dark line. They're only paint: you can
    // walk over them as before, and where the trail crosses one it's cut into steps.
    static readonly Color ReliefLight = Pal.C("#fff1b8"), ReliefShadow = Pal.C("#173548");
    static readonly Color[] LedgeFace = Tones("#8e8068", "#776a55", "#5f5444", "#4b4237");
    static readonly Color[] LedgeFaceRock = Tones("#a8a294", "#8f897c", "#767165", "#5d5950");
    static readonly Color[] LedgeStairs = Tones("#d8c294", "#a88d62");
    const int LedgeLevels = 5, LedgeFaceH = 3;

    static float ReliefHeight(int wx, int wy) => MlHeight(MlLx(wx + 0.5f), MlLy(wy + 0.5f)) + (VNoise(wx, wy, 23, 720) - 0.5f) * 0.06f;
    static int LedgeLevel(float h) => h < 0.16f ? 0 : 1 + (int)((h - 0.16f) * LedgeLevels / 0.84f);
    // Not every step has a face all the way round: they come and go, so it reads as ground, not a wedding cake.
    static bool LedgeOn(int wx, int wy, int level) => VNoise(wx, wy, 34, 730 + level) > 0.5f;

    Color MlRelief(int wx, int wy, char g, Color c, float shade)
    {
        c = shade > 0 ? Pix.Mix(c, ReliefLight, shade * 0.55f) : Pix.Mix(c, ReliefShadow, -shade * 0.8f);
        if (g == 's') return c;
        float h0 = ReliefHeight(wx, wy);
        // High ground a touch lighter and cooler, as if nearer the sky.
        if (h0 > 0.3f) c = Pix.Mix(c, Pal.C("#dfe8d8"), Math.Min(0.14f, (h0 - 0.3f) * 0.25f));
        if (h0 < 0.1f) return c;
        int l0 = LedgeLevel(h0);
        // A face: the ground a pixel or three to the north is a step higher.
        for (int k = 1; k <= LedgeFaceH; k++)
        {
            int lu = LedgeLevel(ReliefHeight(wx, wy - k));
            if (lu <= l0 || !LedgeOn(wx, wy - k, lu) || !Shore(ShapePx(wx, wy - k))) continue;
            if (g == 'p') return LedgeStairs[(wy - k) % 2 == 0 ? 0 : 1];
            var face = g == 'e' || ShapePx(wx, wy - k) == 'e' ? LedgeFaceRock : LedgeFace;
            var fc = face[Math.Min(face.Length - 1, k - 1 + ((Pix.Hash(wx, wy, 741) < 0.18) ? 1 : 0))];
            return shade > 0 ? Pix.Mix(fc, ReliefLight, shade * 0.25f) : Pix.Mix(fc, ReliefShadow, -shade * 0.35f);
        }
        // Shadow at the foot of a face.
        int lf = LedgeLevel(ReliefHeight(wx, wy - LedgeFaceH - 1));
        if (lf > l0 && LedgeOn(wx, wy - LedgeFaceH - 1, lf)) return Pix.Mix(c, ReliefShadow, 0.3f);
        // The lit rim along the top of a face.
        int ld = LedgeLevel(ReliefHeight(wx, wy + 1));
        if (ld < l0 && LedgeOn(wx, wy, l0)) return g == 'p' ? LedgeStairs[0] : Pix.Mix(c, ReliefLight, 0.32f);
        return c;
    }

    void PaintGround(int x, int y)
    {
        var g = worldBase;
        for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++)
            {
                int wx = x * T + px, wy = y * T + py;
                g.Buf[wy * g.W + wx] = GroundPixel(wx, wy, shape[wy * PW + wx]);
            }
    }

    // Small things baked into the ground in clusters: flowers, shells, pebbles, dune ripples, snow glints.
    // Everything stays inside the tile's middle 8x8 so it never pokes out into a rounded-off corner.
    // Details stay on their own ground: anything that landed where a neighbouring ground has blended in is painted over
    // with that ground again.
    void PaintGroundDetail(int x, int y, char t)
    {
        PaintGroundDetailRaw(x, y, t);
        char own = Ground(t, x, y);
        if (!Shore(own)) return;
        for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++)
            {
                int wx = x * T + px, wy = y * T + py;
                char s = shape[wy * PW + wx];
                if (s != own) worldBase.Buf[wy * worldBase.W + wx] = GroundPixel(wx, wy, s);
            }
    }

    void PaintGroundDetailRaw(int x, int y, char t)
    {
        var g = worldBase;
        int X = x * T, Y = y * T;
        double h = Pix.Hash(x, y, 190), h2 = Pix.Hash(x, y, 191);
        int ax = 2 + (int)(Pix.Hash(x, y, 192) * 4), ay = 2 + (int)(Pix.Hash(x, y, 193) * 4);
        var tones = GroundTones.GetValueOrDefault(Ground(t, x, y));
        if (tones != null && h2 < 0.5) g.Rect(X + 1 + (int)(h2 * 14), Y + 1 + (int)(Pix.Hash(x, y, 194) * 7), 1, 1, tones[0]);
        switch (t)
        {
            case 'g':
                if (h < 0.1)
                {
                    // A little group of wildflowers.
                    string petal = h < 0.03 ? "#f3e27a" : h < 0.055 ? "#ffffff" : h < 0.08 ? "#f2a5b5" : "#c9b8f2";
                    foreach (var (ox, oy) in new[] { (0, 0), (2, 1), (1, 3), (3, 3), (4, 0) })
                        if (Pix.Hash(x + ox, y + oy, 195) < 0.8) { g.Rect(X + ax + ox, Y + ay + oy, 1, 1, petal); g.Rect(X + ax + ox, Y + ay + oy + 1, 1, 1, "#3f7a32"); }
                }
                else if (h > 0.97) { g.Rect(X + ax, Y + ay, 2, 1, "#9aa0a5"); g.Rect(X + ax, Y + ay + 1, 2, 1, "#6e7478"); }
                break;
            case 'j':
                if (h < 0.14) { g.Rect(X + ax, Y + ay + 1, 3, 1, "#2f6a2e"); g.Rect(X + ax + 1, Y + ay, 1, 1, "#5aa047"); }
                else if (h < 0.17) { g.Rect(X + ax + 1, Y + ay, 1, 1, h < 0.155 ? "#e04b3a" : "#f2a5b5"); g.Rect(X + ax + 1, Y + ay + 1, 1, 1, "#2f6a2e"); }
                break;
            case 's':
                if (h < 0.05) { g.Rect(X + ax, Y + ay, 2, 1, "#f6d6dc"); g.Rect(X + ax, Y + ay + 1, 2, 1, "#e0a9b3"); }
                else if (h < 0.1) { g.Rect(X + ax, Y + ay, 2, 1, "#c9b38a"); g.Rect(X + ax + 2, Y + ay + 1, 1, 1, "#b39d76"); }
                else if (h > 0.9) g.Rect(X + ax, Y + ay, 1, 1, "#f8ead0");
                break;
            case 'D':
                if (h < 0.2)
                {
                    // A ripple in the dunes: a lit crest over a shadowed trough.
                    int len = 4 + (int)(h2 * 3);
                    g.Rect(X + 1, Y + ay, len, 1, "#f3d08f"); g.Rect(X + 1 + len, Y + ay - 1, 2, 1, "#f3d08f");
                    g.Rect(X + 1, Y + ay + 1, len, 1, "#d29a4f");
                }
                else if (h > 0.95) { g.Rect(X + ax, Y + ay, 2, 1, "#a8743f"); g.Rect(X + ax + 2, Y + ay + 1, 1, 1, "#c2884c"); }
                break;
            case 'n':
                if (h < 0.18) g.Rect(X + ax, Y + ay, 1, 1, "#ffffff");
                else if (h < 0.3) { g.Rect(X + 1, Y + ay, 5, 1, "#d3e0e8"); g.Rect(X + 2, Y + ay - 1, 3, 1, "#ffffff"); }
                break;
            case 'e':
                if (x >= MlStart)
                {
                    // Scree on Marilaya's high rock: loose stones, each lit on top with a shadow under it, and cracks.
                    if (h < 0.45) { g.Rect(X + ax, Y + ay, 3, 2, "#9a958a"); g.Rect(X + ax, Y + ay, 2, 1, "#d9d4c7"); g.Rect(X + ax, Y + ay + 2, 3, 1, "#7a766c"); }
                    if (h2 < 0.3) { g.Rect(X + 2 + (int)(h2 * 10), Y + 6, 1, 1, "#8a857a"); g.Rect(X + 3 + (int)(h2 * 10), Y + 7, 2, 1, "#8a857a"); }
                    break;
                }
                if (h < 0.25) { g.Rect(X + ax, Y + ay, 2, 1, "#8f8a7e"); g.Rect(X + ax + 1, Y + ay - 1, 1, 1, "#d4cfc2"); }
                break;
            case 'i':
            case 'k':
                if (h < 0.5) { g.Line(X + 2, Y + 7, X + 6, Y + 3, "#e8f6fb"); g.Rect(X + 6, Y + 3, 1, 1, "#9cc9db"); }
                break;
            case 'o': if (h < 0.4) g.Rect(X + 2, Y + 5, 3, 1, "#5fd6c9"); break;
            case 'l': if (h < 0.4) g.Rect(X + 2, Y + 5, 3, 1, "#4cc4b4"); break;
            case 'm':
                if (h < 0.3) { g.Rect(X + 2, Y + 4, 3, 2, "#3f7d3a"); g.Rect(X + 3, Y + 4, 1, 1, "#f2a5b5"); }
                else if (h < 0.6) g.Rect(X + ax, Y + ay, 1, 1, "#3d4f32");
                break;
        }
    }

    // Water pixels with land beside them, for the foam.
    void FindShore()
    {
        shore.Clear();
        for (int wy = 0; wy < PH; wy++)
            for (int wx = 0; wx < PW; wx++)
            {
                char w = shape[wy * PW + wx];
                if (!Wet(w)) continue;
                int nx = 0, ny = 0;
                if (Shore(ShapePx(wx + 1, wy))) nx--;
                if (Shore(ShapePx(wx - 1, wy))) nx++;
                if (Shore(ShapePx(wx, wy + 1))) ny--;
                if (Shore(ShapePx(wx, wy - 1))) ny++;
                bool touches = Shore(ShapePx(wx + 1, wy)) || Shore(ShapePx(wx - 1, wy)) || Shore(ShapePx(wx, wy + 1)) || Shore(ShapePx(wx, wy - 1));
                if (touches) shore.Add(((short)wx, (short)wy, (sbyte)nx, (sbyte)ny, w));
            }
    }

    /* ---------- Water life ---------- */
    // Foam in broken strokes along every shore that come and go, and a wash line a pixel or two out that breathes.
    void DrawFoam(float t)
    {
        int x0 = camX - 3, x1 = camX + W + 3, y0 = camY - 3, y1 = camY + H + 3;
        var bright = Pal.Rgba(236, 248, 252, 0.85f);
        var dim = Pal.Rgba(236, 248, 252, 0.3f);
        var wash = Pal.Rgba(236, 248, 252, 0.32f);
        var murky = Pal.Rgba(196, 214, 168, 0.45f);
        foreach (var (sx, sy, nx, ny, w) in shore)
        {
            if (sx < x0 || sx > x1 || sy < y0 || sy > y1) continue;
            bool on = (Pix.Hash(sx / 3, sy / 3, 5) + t * 0.3) % 1 < 0.55;
            if (w == 'm') { if (on) pix.Rect(sx, sy, 1, 1, murky); continue; }
            pix.Rect(sx, sy, 1, 1, on ? bright : dim);
            if (!on || ((sx + sy) & 1) != 0) continue;
            int off = 2 + (int)MathF.Round(MathF.Sin(t * 1.8f + sx * 0.11f + sy * 0.13f));
            pix.Rect(sx + nx * off, sy + ny * off, 1, 1, wash);
        }
    }

    // After dark the surf still catches the moon, so you can make out the coast beyond the firelight.
    void DrawMoonlitShore(float t, float k = 1)
    {
        var glint = Pal.Rgba(214, 228, 240, 0.3f * k);
        int x0 = camX - 1, x1 = camX + W + 1, y0 = camY - 1, y1 = camY + H + 1;
        foreach (var (sx, sy, _, _, w) in shore)
            if (sx >= x0 && sx <= x1 && sy >= y0 && sy <= y1 && w != 'm' && (Pix.Hash(sx / 3, sy / 3, 5) + t * 0.3) % 1 < 0.55 && !TideDry(sx, sy))
                pix.Rect(sx, sy, 1, 1, glint);
    }

    // Now and then a little ripple opens on open water and fades.
    void DrawRipples(float t)
    {
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                if (map[y, x] is not ('~' or 'w') || Pix.Hash(x, y, 140) > 0.05) continue;
                float cycle = 3 + (float)Pix.Hash(x, y, 141) * 3, ph = (t + (float)Pix.Hash(x, y, 142) * 9) % cycle;
                if (ph > 1.2f) continue;
                int f = (int)(ph / 0.4f), cx = x * T + 5, cy = y * T + 5;
                // Kept faint and away from fishing spots, so a spot's own rings are never mistaken for these.
                if (Data.Spots.Any(s => s.Scene == "world" && Dist(cx, cy, s.X, s.Y) < s.R + 12)) continue;
                var c = Pal.Rgba(205, 232, 245, 0.35f - f * 0.1f);
                pix.Rect(cx - 1 - f, cy, 3 + f * 2, 1, c);
                if (f > 0) { pix.Rect(cx - 2 - f, cy - 1, 1, 1, c); pix.Rect(cx + 2 + f, cy - 1, 1, 1, c); }
            }
    }

    /* ---------- Grass that moves ---------- */
    // Tufts of grass, ferns and wildflowers sway in the wind (harder in a storm) and lean away from your feet.
    void DrawTufts(float t)
    {
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        float wind = Stormy ? 3.4f : 1.5f, amp = Stormy ? 1.6f : 0.75f;
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                char g = map[y, x];
                double h = Pix.Hash(x, y, 150);
                byte bi = BiomeAt(x, y);
                bool tuft = g switch { 'g' => h < 0.38, 'j' => h < 0.45, 'D' => h < 0.06, 's' => bi is 0 or 4 or 6 && h < 0.04, _ => false };
                if (!tuft || BuildAt(x, y) != null) continue;
                int bx = x * T + 2 + (int)(Pix.Hash(x, y, 151) * 6), by = y * T + 4 + (int)(Pix.Hash(x, y, 152) * 5);
                // A gust (Ambience.cs) flattens the grass the way it's blowing, and its tips catch the light.
                float gust = Gust(bx, by);
                int s = (int)MathF.Round(MathF.Sin(t * wind + bx * 0.21f + by * 0.17f) * amp * (1 + gust) + gust * 2.2f);
                if (MathF.Abs(player.X - bx) < 6 && MathF.Abs(player.Y - by) < 4 && scene == "world") s = player.X < bx ? 2 : -2;
                var (dark, light) = g switch
                {
                    'j' => ("#2f6a2e", "#5fae4c"), 'D' => ("#b8904a", "#dcb66c"), 's' => ("#9fae72", "#c6cf98"), _ => ("#3f7a32", "#7cbf5a")
                };
                if (gust > 0.55f) light = g switch { 'j' => "#86c86a", 'D' => "#ecd08e", 's' => "#dde3b4", _ => "#a6dc80" };
                bool flower = g == 'g' && h < 0.05;
                if (flower)
                {
                    string petal = h < 0.017 ? "#f3e27a" : h < 0.034 ? "#ffffff" : "#f2a5b5";
                    pix.Rect(bx, by - 2, 1, 3, dark);
                    pix.Rect(bx + s, by - 3, 1, 1, petal);
                    pix.Rect(bx + s - 1, by - 3, 1, 1, Pal.WithAlpha(Pal.C(petal), 0.6f));
                    continue;
                }
                int tall = g == 'j' ? 4 : 3;
                pix.Rect(bx, by - 1, 3, 1, dark);
                pix.Rect(bx, by - 2, 1, 1, dark); pix.Rect(bx - 1 + (s < 0 ? s : 0), by - 3, 1, 1, light);
                pix.Rect(bx + 1, by - tall + 1, 1, tall - 1, dark); pix.Rect(bx + 1 + s, by - tall, 1, 1, light);
                pix.Rect(bx + 2, by - 2, 1, 1, dark); pix.Rect(bx + 3 + (s > 0 ? s : 0), by - 3, 1, 1, light);
            }
    }

    /* ---------- Sky ---------- */
    // Big soft cloud shadows drift slowly over land and sea on fair days, fading out as rain comes in or night falls.
    void DrawCloudShadows(float t)
    {
        float fair = (1 - rainAmt) * (1 - Darkness);
        if (fair <= 0.02f) return;
        var shade = Pal.Rgba(16, 34, 56, 0.1f * fair);
        float span = PW + 360;
        Span<(float ox, float oy, float rx, float ry)> lobes = stackalloc (float, float, float, float)[4];
        for (int i = 0; i < 8; i++)
        {
            float speed = 4 + (float)Pix.Hash(i, 1, 160) * 3;
            float cx = (float)((Pix.Hash(i, 2, 160) * span + t * speed) % span) - 180;
            float cy = (float)(Pix.Hash(i, 3, 160) * PH) + MathF.Sin(t * 0.05f + i) * 6;
            float rx = 36 + (float)Pix.Hash(i, 4, 160) * 28, ry = rx * 0.42f;
            int sx0 = Math.Max(0, (int)(cx - rx * 1.4f) - camX), sx1 = Math.Min(W, (int)(cx + rx * 1.4f) - camX);
            int sy0 = Math.Max(0, (int)(cy - ry * 1.6f) - camY), sy1 = Math.Min(H, (int)(cy + ry * 1.6f) - camY);
            if (sx0 >= sx1 || sy0 >= sy1) continue;
            lobes[0] = (0, 0, rx, ry);
            lobes[1] = (-rx * 0.62f, ry * 0.25f, rx * 0.55f, ry * 0.75f);
            lobes[2] = (rx * 0.58f, ry * 0.15f, rx * 0.6f, ry * 0.8f);
            lobes[3] = (rx * 0.1f, -ry * 0.55f, rx * 0.5f, ry * 0.65f);
            for (int py = sy0; py < sy1; py++)
                for (int px = sx0; px < sx1; px++)
                {
                    float wx = px + camX + 0.5f, wy = py + camY + 0.5f, edge = (BayerAt(px + camX, py + camY) - 0.5f) * 0.3f;
                    foreach (var l in lobes)
                    {
                        float dx = (wx - cx - l.ox) / l.rx, dy = (wy - cy - l.oy) / l.ry;
                        if (dx * dx + dy * dy + edge < 1) { pix.Buf[py * W + px] = Pix.Mix(pix.Buf[py * W + px], shade, shade.A / 255f); break; }
                    }
                }
        }
    }

    // A leaf now and then flutters down from a tree in view (a lot more of them in a storm), lands, and fades.
    void UpdateLeaves(float dt)
    {
        for (int i = leaves.Count - 1; i >= 0; i--)
        {
            var l = leaves[i];
            l.Life -= dt;
            if (l.Life <= 0) { leaves.RemoveAt(i); continue; }
            if (l.Y >= l.Ground) continue;
            l.X += (l.Vx + MathF.Sin(time * 3 + l.Phase) * 9) * dt;
            l.Y += l.Vy * dt;
        }
        if (scene != "world" || Night || leaves.Count > 40) return;
        leafT -= dt * (Stormy ? 5 : 1);
        if (leafT > 0) return;
        leafT = 0.4f + (float)fxRng.NextDouble() * 0.8f;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        (int x, int y, char kind) pick = (-1, -1, ' ');
        int seen = 0;
        foreach (var tr in trees)
            if (tr.kind is 't' or 'h' && tr.x >= vx0 && tr.x <= vx1 && tr.y >= vy0 && tr.y <= vy1 + 2 && fxRng.Next(++seen) == 0) pick = tr;
        if (pick.x < 0) return;
        float bx = pick.x * T + 5, by = pick.y * T + 9;
        string[] cols = pick.kind == 't' ? new[] { "#5aa047", "#7cc25a", "#c9b34a" } : new[] { "#4caa55", "#6fc46a" };
        leaves.Add(new Leaf
        {
            X = bx + (float)fxRng.NextDouble() * 18 - 9, Y = by - 14 - (float)fxRng.NextDouble() * 8, Ground = by + (float)fxRng.NextDouble() * 10 - 2,
            Vx = Stormy ? -30 : (float)fxRng.NextDouble() * 6 - 3, Vy = 7 + (float)fxRng.NextDouble() * 5, Life = 7, Phase = (float)fxRng.NextDouble() * 6,
            Color = Pal.C(cols[fxRng.Next(cols.Length)])
        });
    }

    void DrawLeaves()
    {
        foreach (var l in leaves)
        {
            var c = l.Life < 1.5f ? Pal.WithAlpha(l.Color, l.Life / 1.5f) : l.Color;
            bool down = l.Y >= l.Ground;
            pix.Rect(l.X, l.Y, down || (int)((time + l.Phase) * 5) % 2 == 0 ? 2 : 1, 1, c);
        }
    }

    /* ---------- Trees ---------- */
    static readonly Color Bark = Pal.C("#6b4a2b"), BarkLight = Pal.C("#8a6239"), BarkDark = Pal.C("#4e3520"), TreeShadow = Pal.Rgba(18, 46, 40, 0.24f),
        TreeShadowSoft = Pal.Rgba(18, 46, 40, 0.12f);
    static readonly Color[] OakLeaf = Tones("#24502a", "#2f6428", "#3f7d35", "#5aa047", "#7cc25a"),
        FirLeaf = Tones("#173a2c", "#234a37", "#2e5e46", "#3f7a5a"),
        PalmLeaf = Tones("#1f5a2b", "#2f7a3a", "#4caa55"), JunglePalmLeaf = Tones("#173f22", "#24602f", "#3a8a45"),
        CactusSkin = Tones("#2a5226", "#3d6e2f", "#4f8a3c", "#6aa84f", "#8fc76a"),
        BushLeaf = Tones("#24502a", "#2f6428", "#3f7d35", "#5aa047");
    static readonly Color Snow = Pal.C("#eef5f8"), SnowBright = Pal.C("#ffffff");

    // A filled round blob of pixel rows, for canopies and bushes.
    void Disc(float cx, float cy, float r, Color c)
    {
        int n = (int)MathF.Ceiling(r);
        for (int dy = -n; dy <= n; dy++)
        {
            int w = (int)MathF.Round(MathF.Sqrt(MathF.Max(0, r * r - dy * dy)) * 2);
            if (w > 0) pix.Rect(MathF.Round(cx - w / 2f), MathF.Round(cy) + dy, w, 1, c);
        }
    }

    // Trees stand on their tile (that's what you bump into and chop) and rise well above it. Each one is a little
    // bigger or smaller and mirrored at random, and lit from the upper left. If you're standing behind one, it fades.
    void DrawTree(int tx, int ty, char kind, bool live = true)
    {
        int X = tx * T, Y = ty * T;
        if (live && chopTile == (tx, ty) && shakeT > 0) X += (int)MathF.Round(MathF.Sin(time * 70) * 1.5f);
        int bx = X + 5, by = Y + 9, sw = kind is 't' or 'f' or 'h' ? Sway(tx, ty) : 0;
        float s = 0.9f + 0.2f * (float)Pix.Hash(tx, ty, 61);
        // Higher up Marilaya's hills the trees grow smaller, so the slope shows between them.
        if (tx >= MlStart) s *= 1 - 0.3f * Math.Clamp((MlHeight(MlLx(X + 5), MlLy(Y + 5)) - 0.28f) / 0.35f, 0, 1);
        int m = Pix.Hash(tx, ty, 62) < 0.5 ? 1 : -1;
        var (height, half) = kind switch { 't' => (30, 12), 'f' => (31, 9), 'h' => (30, 12), 'c' => (21, 7), _ => (0, 0) };
        bool Hides(float x, float y) => y < by - 1 && y > by - height && MathF.Abs(x - bx) < half;
        bool fade = live && height > 0 && scene == "world" && (Hides(player.X, player.Y) || !TomasInBed && Hides(tomasX, tomasY));
        switch (kind)
        {
            case 'y': DrawBush(tx, ty, bx, by); return;
            case 'R': DrawBoulder(tx, ty, X, Y); return;
        }
        pix.Rect(bx - 6, by - 1, 13, 2, TreeShadow);
        pix.Rect(bx - 4, by + 1, 9, 1, TreeShadowSoft);
        float keep = pix.Alpha;
        if (fade) pix.Alpha = 0.4f;
        try
        {
            switch (kind)
            {
                case 'f': DrawFir(tx, ty, bx, by, sw, s); break;
                case 'c': DrawCactus(tx, ty, bx, by, s, m); break;
                case 'h': DrawPalm(tx, ty, bx, by, sw, s, m); break;
                default: DrawOak(tx, ty, bx, by, sw, s, m); break;
            }
        }
        finally { pix.Alpha = keep; }
    }

    void DrawOak(int tx, int ty, int bx, int by, int sw, float s, int m)
    {
        int trunk = (int)MathF.Round(14 * s);
        pix.Rect(bx - 2, by - trunk, 4, trunk, Bark);
        pix.Rect(bx - 2, by - trunk, 1, trunk, BarkLight);
        pix.Rect(bx + 1, by - trunk, 1, trunk, BarkDark);
        pix.Rect(bx - 3, by - 2, 6, 2, Bark); pix.Rect(bx - 3, by - 2, 1, 1, BarkLight);
        pix.Rect(bx - 4, by - 1, 1, 1, BarkDark); pix.Rect(bx + 3, by - 1, 1, 1, BarkDark);
        float cy = by - 17 * s, r = 7.5f * s;
        Span<(float ox, float oy, float r)> lobes = stackalloc (float, float, float)[]
        {
            (0.5f, -1, r), (-5.5f * m, 3, r * 0.68f), (6 * m, 2.5f, r * 0.64f), (2 * m, -6, r * 0.56f)
        };
        foreach (var l in lobes) Disc(bx + l.ox + sw, cy + l.oy + 1, l.r + 1, OakLeaf[0]);
        foreach (var l in lobes) Disc(bx + l.ox + sw, cy + l.oy + 0.5f, l.r, OakLeaf[1]);
        foreach (var l in lobes) Disc(bx + l.ox + sw - 0.5f, cy + l.oy - 0.5f, l.r - 0.9f, OakLeaf[2]);
        foreach (var l in lobes) Disc(bx + l.ox + sw - 1.5f, cy + l.oy - 2, l.r * 0.52f, OakLeaf[3]);
        Disc(bx + sw - 2.5f, cy - 4.5f, r * 0.24f, OakLeaf[4]);
        // Clumps of leaves: a few darker and lighter flecks.
        for (int i = 0; i < 8; i++)
        {
            float a = (float)Pix.Hash(tx, ty, 200 + i) * MathF.Tau, d = (float)Pix.Hash(tx, ty, 210 + i) * r * 0.75f;
            pix.Rect(bx + sw + MathF.Cos(a) * d * 1.2f, cy + MathF.Sin(a) * d * 0.8f, 1, 1, i % 3 == 0 ? OakLeaf[3] : OakLeaf[1]);
        }
    }

    void DrawFir(int tx, int ty, int bx, int by, int sw, float s)
    {
        pix.Rect(bx - 1, by - 6, 3, 6, Bark);
        pix.Rect(bx - 1, by - 6, 1, 6, BarkLight);
        // Four tiers, widest at the bottom, a little uneven; the top sways the most.
        for (int k = 0; k < 4; k++)
        {
            int o = (int)MathF.Round(sw * k / 3f), yb = by - 5 - (int)(k * 6 * s);
            int hwMax = (int)MathF.Round((8.5f - k * 1.9f) * s) + (Pix.Hash(tx, ty, 220 + k) < 0.3 ? -1 : 0);
            int rows = k == 3 ? 9 : 8;
            for (int r = 0; r < rows; r++)
            {
                int hw = (int)MathF.Round(hwMax * (r + 1) / (float)rows), yy = yb - rows + 1 + r, x0 = bx + o - hw;
                int w = hw * 2 + 1;
                bool bottom = r == rows - 1;
                pix.Rect(x0, yy, w, 1, bottom ? FirLeaf[1] : FirLeaf[2]);
                if (!bottom && hw > 1) pix.Rect(bx + o + hw / 2 + 1, yy, hw - hw / 2, 1, FirLeaf[1]);
                if (!bottom) pix.Rect(x0 + 1, yy, Math.Max(1, hw / 2), 1, FirLeaf[3]);
                // Snow along the lit (left) edge and on the tips of each tier.
                if (r >= 1) pix.Rect(x0, yy, Math.Min(2, w), 1, r < 3 && k < 3 ? FirLeaf[3] : Snow);
                if (bottom) { pix.Rect(x0, yy, 2, 1, SnowBright); pix.Rect(x0 + w - 1, yy, 1, 1, Snow); pix.Rect(x0 + 2, yy + 1, w - 4, 1, FirLeaf[0]); }
            }
        }
        pix.Rect(bx + sw, by - 5 - (int)(18 * s) - 9, 1, 1, SnowBright);
    }

    void DrawCactus(int tx, int ty, int bx, int by, float s, int m)
    {
        int top = by - (int)(19 * s);
        int lx = m > 0 ? -1 : 1;   // which side the low arm is on
        int la = by - 9, ra = by - 12 - (int)(Pix.Hash(tx, ty, 230) * 3);
        Span<(int x, int y, int w, int h)> parts = stackalloc (int, int, int, int)[]
        {
            (bx - 2, top, 5, by - top),
            (lx < 0 ? bx - 5 : bx + 3, la, 3, 2), (lx < 0 ? bx - 7 : bx + 5, la - 6, 3, 8),
            (lx < 0 ? bx + 3 : bx - 5, ra, 3, 2), (lx < 0 ? bx + 5 : bx - 7, ra - 5, 3, 7)
        };
        void Capsule(int x, int y, int w, int h, Color c) { pix.Rect(x, y + 1, w, h - 1, c); pix.Rect(x + 1, y, w - 2, 1, c); }
        foreach (var p in parts) Capsule(p.x - 1, p.y - 1, p.w + 2, p.h + 1, CactusSkin[0]);
        foreach (var p in parts)
        {
            Capsule(p.x, p.y, p.w, p.h, CactusSkin[2]);
            if (p.h > 2) { pix.Rect(p.x, p.y + 1, 1, p.h - 1, CactusSkin[3]); pix.Rect(p.x + p.w - 1, p.y + 1, 1, p.h - 1, CactusSkin[1]); }
        }
        pix.Rect(bx - 1, top + 1, 1, by - top - 2, CactusSkin[4]);
        pix.Rect(bx + 1, top + 2, 1, by - top - 3, CactusSkin[1]);
        for (int i = 0; i < 5; i++) pix.Rect(bx + (i % 2 == 0 ? -3 : 3), top + 3 + i * 3, 1, 1, "#f2ecd0");
        if (Pix.Hash(tx, ty, 5) > 0.6) { pix.Rect(bx - 1, top - 1, 3, 1, "#f2a5b5"); pix.Rect(bx, top - 2, 1, 1, "#e04b3a"); }
    }

    void DrawPalm(int tx, int ty, int bx, int by, int sw, float s, int m)
    {
        byte bi = BiomeAt(tx, ty);
        var leaf = bi == 3 ? JunglePalmLeaf : PalmLeaf;
        if (bi == 3) s *= 0.9f;
        // No two palms quite alike: each leans its own amount, and its fronds sit at their own angles and lengths.
        int n = (int)(21 * s);
        float lean = m * (2.5f + 5 * (float)Pix.Hash(tx, ty, 240));
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n, x = bx + (lean + sw) * k * k;
            int w = i < n / 3 ? 3 : 2;
            pix.Rect(x - 1, by - i - 1, w, 1, i % 3 == 0 ? BarkDark : Bark);
            pix.Rect(x - 1, by - i - 1, 1, 1, i % 3 == 0 ? Bark : BarkLight);
        }
        float cx = bx + lean + sw, cy = by - n - 1;
        if (bi is 2 or 4 or 6) { Disc(cx - 1.5f, cy + 2, 1.3f, Pal.C("#5b3e24")); Disc(cx + 1.5f, cy + 2, 1.3f, Pal.C("#6b4a2b")); pix.Rect(cx + 1, cy + 1, 1, 1, BarkLight); }
        // Six fronds that droop at the tips, with leaflets underneath; the tips sway most.
        Span<float> angles = stackalloc float[] { -2.85f, -2.15f, -1.2f, -0.45f, 0.3f, 2.75f };
        for (int f = 0; f < angles.Length; f++)
        {
            float a = angles[f] + ((float)Pix.Hash(tx, ty, 241 + f) - 0.5f) * 0.45f * m;
            float len = 11.5f * s * (0.85f + 0.3f * (float)Pix.Hash(tx, ty, 248 + f));
            if (f == 2 && Pix.Hash(tx, ty, 255) < 0.35) continue;   // some have lost a frond
            float ca = MathF.Cos(a), sa = MathF.Sin(a);
            bool upper = sa < -0.5f;
            for (int j = 1; j <= len; j++)
            {
                float u = j / len, fx = cx + ca * j + sw * u, fy = cy + sa * j * 0.7f + 6 * u * u;
                pix.Rect(fx, fy, 1, 2, leaf[1]);
                if (j % 2 == 0 && u > 0.25f) pix.Rect(fx, fy + 2, 1, 1, leaf[0]);
                if (u < 0.85f && (upper || j % 2 == 1)) pix.Rect(fx, fy, 1, 1, leaf[2]);
            }
        }
        Disc(cx, cy, 1.6f, leaf[0]);
        pix.Rect(cx - 1, cy - 1, 2, 1, leaf[2]);
    }

    void DrawBush(int tx, int ty, int bx, int by)
    {
        pix.Rect(bx - 6, by - 1, 12, 2, TreeShadow);
        Span<(float ox, float oy, float r)> lobes = stackalloc (float, float, float)[] { (-3, -3, 3.2f), (3, -3, 3.2f), (0, -5, 4) };
        foreach (var l in lobes) Disc(bx + l.ox, by + l.oy + 1, l.r + 1, BushLeaf[0]);
        foreach (var l in lobes) Disc(bx + l.ox, by + l.oy + 0.5f, l.r, BushLeaf[1]);
        foreach (var l in lobes) Disc(bx + l.ox - 0.5f, by + l.oy - 0.5f, l.r - 0.9f, BushLeaf[2]);
        Disc(bx - 1.5f, by - 6.5f, 1.6f, BushLeaf[3]);
        // Red berries, or little green calamansi on Habagat's bushes.
        bool calamansi = BiomeAt(tx, ty) == 6;
        if (state.picked.GetValueOrDefault($"{tx},{ty}") != state.day)
            foreach (var (ox, oy) in new[] { (-4, -3), (1, -6), (-1, -2), (3, -3), (-2, -6), (4, -5) })
            {
                pix.Rect(bx + ox, by + oy, 1, 1, calamansi ? "#9ccc3a" : "#e04b3a");
                if (ox % 2 == 0) pix.Rect(bx + ox, by + oy - 1, 1, 1, calamansi ? "#e0f08a" : "#ff9a8a");
            }
    }

    // Boulders: snow-capped, sandstone or mossy depending on the island.
    void DrawBoulder(int tx, int ty, int X, int Y)
    {
        byte bi = BiomeAt(tx, ty);
        var (ol, dk, md, lt) = bi == 2 ? ("#6e4128", "#9a5f3a", "#b5764a", "#d69a68") : bi == 1 ? ("#3f4a52", "#5e6b74", "#7d8b95", "#a9b6be")
            : ("#35393b", "#4f5558", "#6e7478", "#8f959a");
        pix.Rect(X, Y + 8, 11, 2, TreeShadow);
        Rock(pix, X, Y, ol, dk, md, lt);
        if (bi == 1) { pix.Rect(X + 3, Y + 2, 4, 1, "#ffffff"); pix.Rect(X + 2, Y + 3, 4, 1, "#eef5f8"); pix.Rect(X + 7, Y + 3, 1, 1, "#eef5f8"); }
        if (bi == 3) { pix.Rect(X + 3, Y + 2, 3, 1, "#4caa55"); pix.Rect(X + 2, Y + 3, 2, 1, "#3f8a45"); pix.Rect(X + 7, Y + 6, 1, 1, "#3f8a45"); }
    }

    // A round-shouldered rock filling most of a tile, with a dark outline so it reads on any ground, lit from the upper left.
    static void Rock(Pix p, int X, int Y, string ol, string dk, string md, string lt)
    {
        p.Rect(X + 3, Y + 1, 4, 1, ol); p.Rect(X + 1, Y + 2, 8, 1, ol); p.Rect(X, Y + 3, 10, 4, ol); p.Rect(X + 1, Y + 7, 8, 1, ol); p.Rect(X + 2, Y + 8, 6, 1, ol);
        p.Rect(X + 3, Y + 2, 4, 1, dk); p.Rect(X + 1, Y + 3, 8, 4, dk); p.Rect(X + 2, Y + 7, 6, 1, dk);
        p.Rect(X + 3, Y + 2, 3, 1, md); p.Rect(X + 2, Y + 3, 5, 1, md); p.Rect(X + 1, Y + 4, 6, 1, md); p.Rect(X + 1, Y + 5, 4, 1, md);
        p.Rect(X + 3, Y + 3, 2, 1, lt); p.Rect(X + 4, Y + 2, 1, 1, lt);
    }
}
