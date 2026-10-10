using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Biomes: Saltmere, Frostfang, Sunscald, Mirewood, Starfall, Amihan. Matches Data.Biomes.
    // map is the current scene's tiles; it points at worldMap whenever the player is outdoors.
    char[,] map = new char[ROWS, COLS];
    readonly byte[,] biome = new byte[ROWS, COLS];
    // Trees, cacti, palms, boulders ('R') and berry bushes ('y'), all solid tiles drawn as sprites.
    readonly List<(int x, int y, char kind)> trees = new();
    readonly Dictionary<(int, int), char> stumps = new();
    readonly float[] dark = new float[W * H];
    bool mapTexDirty = true;

    static readonly (int x, int y)[] SaltTrees = { (21, 6), (23, 8), (12, 11), (19, 12), (23, 11), (8, 9), (13, 3) };
    static readonly (int x, int y)[] SaltBushes = { (9, 10), (12, 4), (22, 10) };
    // The mouth of Frostfang Caverns covers these four tiles; you enter standing just below it.
    const int MouthX = 56, MouthY = 2;
    const float MouthDoorX = 570, MouthDoorY = 46;
    const int TreeRegrowDays = 3, BoulderRegrowDays = 2;
    static readonly (int x, int y)[] Tide = { (8, 13), (8, 14), (7, 14), (6, 14) };
    // The other islands: biome, center and radii in tiles. Saltmere keeps its original shape in the top-left corner.
    // Starfall Atoll (biome 4) has no bridge; you get there by boat (or on Tidemane). A channel of deep water keeps
    // it out of reach of waders from the Dunes.
    static readonly (byte biome, float cx, float cy, float rx, float ry)[] Isles = { (1, 62, 9, 17, 8), (2, 66, 38, 20, 12), (3, 20, 38, 17, 11), (4, 113, 26, 17, 15) };
    // Bridges are drawn along these lines, but only over open sea, so they always join shore to shore.
    // The lines run well into both islands so a wobbly coastline can't leave a gap.
    static readonly (int x0, int y0, int x1, int y1)[] Bridges = { (20, 5, 56, 5), (16, 10, 16, 34), (66, 12, 66, 32), (28, 38, 52, 38) };
    // Boat jetties: Pip's jetty on Saltmere's east beach, and the atoll's landing on its west shore. Storms don't close these.
    static readonly (int x0, int y0, int x1, int y1)[] Jetties = { (25, 11, 27, 11), (89, 26, 96, 26) };
    const float SaltJettyX = 275, SaltJettyY = 117, AtollJettyX = 895, AtollJettyY = 267;
    // The atoll's lagoon (in tiles), and the Starwell glade (in pixels): a blue hole inside a ring of palms with one gap,
    // facing west. Hoofprints lead to it from the jetty. The glade is raised ground, so the ring never opens onto the sea.
    const float AtollLagoonX = 106, AtollLagoonY = 24;
    public const float StarwellX = 1200, StarwellY = 300;
    const float WellDeep = 1.9f, WellRim = 2.8f, GladeR = 7f, PalmRingR = 8.2f, GladeGap = 0.24f;
    const float CarvingX = 1162, CarvingY = 282;
    static readonly (float x, float y)[] HoofTrail = { (97.5f, 26.5f), (99, 28.5f), (101, 30.5f), (105, 31.5f), (109, 31), (112.5f, 30.3f), (116.5f, 30.2f) };
    readonly HashSet<(int, int)> hoofprints = new();
    const float PipX = 210, PipY = 101;
    readonly HashSet<(int, int)> bridgeSet = new();
    // Per biome, in Data.Biomes order (Habagat, the last, places its own trees in GenerateHabagat).
    static readonly char[] InnerGround = { 'g', 'n', 'D', 'j', 's', 'j', 'g' }, ShoreGround = { 's', 'e', 's', 's', 's', 's', 's' }, TreeKind = { 't', 'f', 'c', 'h', 'h', 'h', 'h' };
    static readonly float[] TreeDensity = { 0, 0.12f, 0.05f, 0.24f, 0.16f, 0.12f, 0.1f }, BoulderDensity = { 0, 0.025f, 0.03f, 0.015f, 0, 0.015f, 0.02f };
    static readonly HashSet<char> Land = new() { 's', 'g', 'p', 't', 'n', 'e', 'i', 'D', 'j', 'f', 'c', 'h', 'R' };
    static readonly HashSet<char> Water = new() { '~', 'w', 'l', 'T', 'x', 'r', 'o', 'm', 'I', 'k' };

    /* ---------- Map ---------- */
    static double Noise(int x, int y) => 0.07 * Math.Sin(x * 1.3 + y * 0.7) + 0.05 * Math.Cos(x * 0.6 - y * 1.7);

    static double SaltD(int x, int y)
    {
        double dx = (x + 0.5 - 15.5) / 10.6, dy = (y + 0.5 - 8.6) / 6.1;
        return dx * dx + dy * dy + Noise(x, y);
    }

    // Larger, slower waves than Saltmere's noise, so the new coastlines get bays and headlands.
    static double IsleNoise(int x, int y, int s) =>
        0.14 * Math.Sin(x * 0.55 + y * 0.35 + s) + 0.1 * Math.Cos(x * 0.3 - y * 0.8 + s * 2) + 0.06 * Math.Sin(x * 1.7 + y * 1.3 + s * 3);

    static double IsleD(int x, int y, (byte biome, float cx, float cy, float rx, float ry) i)
    {
        double dx = (x + 0.5 - i.cx) / i.rx, dy = (y + 0.5 - i.cy) / i.ry;
        return dx * dx + dy * dy + IsleNoise(x, y, i.biome * 5);
    }

    static bool InEllipse(int x, int y, double cx, double cy, double rx, double ry, double k = 1)
    {
        double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
        return dx * dx + dy * dy < k;
    }

    // How far a tile's centre is from the middle of the Starwell, in tiles.
    static double GladeDist(int x, int y)
    {
        double dx = x + 0.5 - StarwellX / T, dy = y + 0.5 - StarwellY / T;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // An ellipse with a ragged edge, for ponds and lakes.
    static bool InBlob(int x, int y, double cx, double cy, double rx, double ry, int seed, double k = 1)
    {
        double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
        return dx * dx + dy * dy + 0.18 * Math.Sin(x * 1.1 + y * 0.7 + seed) + 0.12 * Math.Cos(x * 0.5 - y * 1.3 + seed) < k;
    }

    // Regenerates the outdoor world. It is deterministic, so only changes (tide, dock, chopped trees) come from the save.
    void BuildMap()
    {
        var keepMap = map;
        var keepBase = basePix;
        map = worldMap;
        basePix = worldBase;
        GenerateWorld();
        if (scene != "world") { map = keepMap; basePix = keepBase; }
    }

    void GenerateWorld()
    {
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                char t;
                byte bi = 0;
                // Amihan to the east, and the Habagat band along the bottom, are carved later (GenerateArchipelago,
                // GenerateHabagat); the open sea there belongs to them.
                if (x >= EastStart || y >= HabagatTop)
                {
                    map[y, x] = '~'; biome[y, x] = x >= EastStart ? (byte)5 : (byte)6;
                    continue;
                }
                if (x < 32 && y < 18)
                {
                    // Saltmere, exactly as it was when it was the whole world.
                    double d = SaltD(x, y);
                    t = d < 0.6 ? 'g' : d < 1.0 ? 's' : d < 1.55 ? 'w' : '~';
                    if (InEllipse(x, y, 10, 6.6, 2.7, 1.9)) t = 'l';
                    double ix = (x + 0.5 - 4.2) / 3.0, iy = (y + 0.5 - 15.4) / 1.7, id = ix * ix + iy * iy;
                    if (id < 1) t = 's';
                    else if (id < 1.7 && t == '~') t = 'w';
                }
                else
                {
                    double best = SaltD(x, y);
                    foreach (var isle in Isles)
                    {
                        double d = IsleD(x, y, isle);
                        if (d < best) { best = d; bi = isle.biome; }
                    }
                    t = bi == 0 ? (best < 0.6 ? 'g' : best < 1.0 ? 's' : best < 1.55 ? 'w' : '~')
                        : best < 0.8 ? InnerGround[bi] : best < 1.0 ? ShoreGround[bi] : best < 1.5 ? 'w' : '~';
                }
                map[y, x] = t;
                biome[y, x] = bi;
            }

        // Saltmere
        foreach (var (x, y) in new[] { (19, 2), (21, 2), (23, 3), (18, 3), (24, 4), (22, 2) })
            if (map[y, x] != 'g' && map[y, x] != 's') map[y, x] = 'r';
        foreach (var (x, y) in Tide) map[y, x] = state.flags.tideOut ? 'p' : 'T';
        for (int x = 25; x <= 29; x++) map[9, x] = (!state.flags.dockFixed && x >= 26 && x <= 27) ? 'x' : 'd';

        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                char t = map[y, x];
                switch (biome[y, x])
                {
                    case 1: // Frostfang: a frozen lake, and ice floes offshore
                        if (InBlob(x, y, 59.5, 8.5, 5.5, 2.8, 3) && Land.Contains(t)) map[y, x] = 'i';
                        else if (t == 'w' && Pix.Hash(x, y, 61) < 0.1) map[y, x] = 'I';
                        break;
                    case 2: // Sunscald: an oasis ringed with grass
                        if (InBlob(x, y, 68.5, 40.5, 4, 2.4, 5)) map[y, x] = 'o';
                        else if (InBlob(x, y, 68.5, 40.5, 4, 2.4, 5, 2.6) && (t == 'D' || t == 's')) map[y, x] = 'g';
                        break;
                    case 3: // Mirewood: swamp pools
                        if (InBlob(x, y, 14.5, 40.5, 5, 3, 7) || InBlob(x, y, 26.5, 34.5, 2.5, 1.6, 9)) map[y, x] = 'm';
                        break;
                    case 4: // Starfall Atoll: a ring of sand around a lagoon, and the Starwell in its palm glade
                    {
                        double gd = GladeDist(x, y);
                        if (gd < WellDeep) map[y, x] = '~';
                        else if (gd < WellRim) map[y, x] = 'w';
                        else if (gd < PalmRingR + 0.5) map[y, x] = 's';
                        else if (InBlob(x, y, AtollLagoonX, AtollLagoonY, 6, 5, 11) && Land.Contains(t)) map[y, x] = 'l';
                        break;
                    }
                }
            }
        map[8, 60] = 'k';

        var bridgeTiles = new List<(int x, int y)>();
        bridgeSet.Clear();
        foreach (var (line, isBridge) in Bridges.Select(b => (b, true)).Concat(Jetties.Select(j => (j, false))))
        {
            var (x0, y0, x1, y1) = line;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (map[y, x] is not ('~' or 'w' or 'I')) continue;
                    map[y, x] = y0 == y1 ? 'd' : 'b';
                    bridgeTiles.Add((x, y));
                    if (isBridge) bridgeSet.Add((x, y));
                }
        }

        // Cave mouth on Frostfang's north shore, with clear snow in front of it.
        for (int y = MouthY - 1; y <= MouthY + 2; y++)
            for (int x = MouthX - 1; x <= MouthX + 2; x++) map[y, x] = 'n';
        for (int y = MouthY; y <= MouthY + 1; y++)
            for (int x = MouthX; x <= MouthX + 1; x++) map[y, x] = 'C';

        trees.Clear();
        stumps.Clear();
        void Place(int x, int y, char kind)
        {
            // Chopped trees and broken boulders stay gone for a few days, leaving a stump behind.
            string key = $"{x},{y}";
            if (state.felled.TryGetValue(key, out int day))
            {
                if (state.day - day < (kind == 'R' ? BoulderRegrowDays : TreeRegrowDays) || RegrowBlocked(x, y)) { stumps[(x, y)] = kind; return; }
                state.felled.Remove(key);
            }
            map[y, x] = kind;
            trees.Add((x, y, kind));
        }
        foreach (var (x, y) in SaltTrees) Place(x, y, 't');
        foreach (var (x, y) in SaltBushes) Place(x, y, 'y');

        // The hoofprint trail, sampled every half tile along its waypoints.
        hoofprints.Clear();
        for (int i = 0; i + 1 < HoofTrail.Length; i++)
        {
            var (ax, ay) = HoofTrail[i]; var (bx, by) = HoofTrail[i + 1];
            int steps = (int)MathF.Ceiling(MathF.Max(MathF.Abs(bx - ax), MathF.Abs(by - ay)) * 2);
            for (int k = 0; k <= steps; k++)
            {
                int hx = (int)MathF.Floor(ax + (bx - ax) * k / steps), hy = (int)MathF.Floor(ay + (by - ay) * k / steps);
                if (map[hy, hx] == 's') hoofprints.Add((hx, hy));
            }
        }
        // The palm ring around the Starwell, open on the west where the hoofprints come in.
        for (int y = 1; y < ROWS - 1; y++)
            for (int x = 1; x < COLS - 1; x++)
            {
                double gd = GladeDist(x, y);
                if (gd < GladeR || gd >= PalmRingR || map[y, x] != 's') continue;
                double ang = Math.Atan2(y + 0.5 - StarwellY / T, x + 0.5 - StarwellX / T);
                if (Math.PI - Math.Abs(ang) < GladeGap) continue;
                Place(x, y, 'h');
            }
        for (int y = 1; y < ROWS - 1; y++)
            for (int x = 1; x < COLS - 1; x++)
            {
                byte bi = biome[y, x];
                if (bi == 0) continue;
                if (bi == 4 && (GladeDist(x, y) < PalmRingR + 1.5 || hoofprints.Contains((x, y)))) continue;
                bool palm = bi == 2 && map[y, x] == 'g' && Pix.Hash(x, y, 78) < 0.15;
                double roll = Pix.Hash(x, y, 77);
                bool boulder = false, bush = false;
                if (!palm)
                {
                    char inner = InnerGround[bi];
                    if (map[y, x] != inner) continue;
                    boulder = roll >= TreeDensity[bi] && roll < TreeDensity[bi] + BoulderDensity[bi];
                    bush = bi != 2 && bi != 4 && !boulder && roll >= TreeDensity[bi] && Pix.Hash(x, y, 79) < 0.035;
                    if (roll >= TreeDensity[bi] && !boulder && !bush) continue;
                    if (map[y - 1, x] != inner || map[y + 1, x] != inner || map[y, x - 1] != inner || map[y, x + 1] != inner) continue;
                }
                if (bridgeTiles.Any(b => Math.Abs(b.x - x) <= 2 && Math.Abs(b.y - y) <= 2)) continue;
                if (Data.Spots.Any(s => s.Scene == "world" && Dist(x * T + 5, y * T + 5, s.X, s.Y) < s.R + (bi == 4 ? -6 : 10))) continue;
                if (Math.Abs(x - MouthX) <= 3 && Math.Abs(y - MouthY) <= 4) continue;
                Place(x, y, palm ? 'h' : boulder ? 'R' : bush ? 'y' : TreeKind[bi]);
            }

        GenerateArchipelago();
        GenerateHabagat();
        RenderBase();
        mapTexDirty = true;
    }

    int SCols => map.GetLength(1);
    int SRows => map.GetLength(0);
    char TileAt(int tx, int ty) => tx < 0 || ty < 0 || tx >= SCols || ty >= SRows ? (scene == "world" ? '~' : '#') : map[ty, tx];
    byte BiomeAt(int tx, int ty) => tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS ? (byte)0 : biome[ty, tx];
    byte PlayerBiome() => BiomeAt((int)MathF.Floor(player.X / T), (int)MathF.Floor((player.Y - 1.5f) / T));

    /* ---------- Static tile layer ---------- */
    static readonly Dictionary<char, (string, string)> TileCol = new()
    {
        ['~'] = ("#1d4f78", "#245a86"), ['w'] = ("#2f7fa3", "#3a8db0"), ['T'] = ("#2f7fa3", "#3a8db0"), ['x'] = ("#2f7fa3", "#3a8db0"),
        ['r'] = ("#2f7fa3", "#3a8db0"), ['l'] = ("#2a9d8f", "#33ab9c"), ['s'] = ("#e8cf96", "#d8bb7e"), ['g'] = ("#5d9b45", "#4c873a"),
        ['p'] = ("#cdb07a", "#bb9c66"), ['d'] = ("#2f7fa3", "#3a8db0"), ['b'] = ("#2f7fa3", "#3a8db0"), ['I'] = ("#2f7fa3", "#3a8db0"),
        ['n'] = ("#e8f0f4", "#d3e0e8"), ['e'] = ("#b7b2a6", "#a19c90"), ['i'] = ("#bfe3f0", "#a8d4e6"), ['k'] = ("#bfe3f0", "#a8d4e6"),
        ['D'] = ("#e9b96e", "#d9a457"), ['o'] = ("#22a6a0", "#2fb8b0"), ['j'] = ("#3f7d3a", "#356b31"), ['m'] = ("#4f6440", "#5a7048")
    };

    // The ground drawn under a tree, bush or the cave mouth.
    char Ground(char t, int x, int y) => t switch
    {
        't' => 'g', 'f' => 'n', 'c' => 'D', 'C' => 'n', 'h' => biome[y, x] switch { 2 => 'g', 4 => 's', 6 => HabagatPalmGround(x, y), _ => 'j' }, 'R' or 'y' => InnerGround[biome[y, x]], _ => t
    };

    // Habagat's palms grow on the beaches and inland too: they stand on whichever their neighbours mostly are.
    char HabagatPalmGround(int x, int y)
    {
        int grass = 0, sand = 0;
        foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int nx = x + ox, ny = y + oy;
            if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS) continue;
            char c = worldMap[ny, nx];
            if (c is 'g' or 'y' or 'R') grass++;
            else if (c is 's') sand++;
        }
        return grass > sand ? 'g' : 's';
    }

    // The whole outdoor base layer: depth, the rounded coast, every tile's ground and details, then where the foam goes.
    void RenderBase()
    {
        ComputeDepth();
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++) ShapeTile(x, y);
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++) PaintTile(x, y);
        FindShore();
    }

    // Redraws one world tile into the base layer (also used after chopping a tree).
    void RenderTile(int x, int y)
    {
        ShapeTile(x, y);
        PaintTile(x, y);
    }

    void PaintTile(int x, int y)
    {
        var g = worldBase;
        char t = worldMap[y, x];
        int X = x * T, Y = y * T;
        PaintGround(x, y);
        PaintGroundDetail(x, y, t);
        if (hoofprints.Contains((x, y)))
        {
            // Two hoofprints per tile, left and right of the trail, a little off the grid.
            int o = (int)(Pix.Hash(x, y, 34) * 3);
            foreach (var (hx, hy) in new[] { (1 + o, 1), (5, 5 + o / 2) })
            {
                g.Rect(X + hx, Y + hy, 3, 1, "#c4a56a"); g.Rect(X + hx, Y + hy + 1, 1, 2, "#c4a56a"); g.Rect(X + hx + 2, Y + hy + 1, 1, 2, "#c4a56a");
                g.Rect(X + hx + 1, Y + hy + 1, 1, 1, "#b8975c");
            }
        }
        if (t == 'k')
        {
            g.Rect(X + 2, Y + 3, 6, 4, "#1d4f78"); g.Rect(X + 3, Y + 2, 4, 6, "#1d4f78");
            g.Rect(X + 3, Y + 3, 2, 1, "#3a6f99");
            g.Rect(X + 2, Y + 2, 1, 1, "#ffffff"); g.Rect(X + 7, Y + 7, 1, 1, "#ffffff");
        }
        if (t == 'r')
        {
            // A rock standing in the shallows, wet and dark at the waterline, with a ring of foam.
            Rock(g, X, Y, "#33383b", "#5e6468", "#8a8f93", "#b4b9bc");
            g.Rect(X + 1, Y + 6, 8, 1, "#4a5155");
            g.Rect(X + 1, Y + 8, 1, 1, "#cfe8ee"); g.Rect(X + 8, Y + 8, 1, 1, "#cfe8ee"); g.Rect(X + 2, Y + 9, 6, 1, "#cfe8ee");
            g.Rect(X, Y + 7, 1, 1, "#e8f6fb"); g.Rect(X + 9, Y + 7, 1, 1, "#e8f6fb");
        }
        if (t == 'I')
        {
            // A chunk of floating ice, blue where it meets the water.
            Rock(g, X, Y, "#7fb3cc", "#cfe6f0", "#eef6fa", "#ffffff");
            g.Rect(X + 1, Y + 6, 8, 1, "#a8d4e6");
            g.Rect(X + 2, Y + 9, 6, 1, "#cfe8ee");
        }
        if (t == 'T')
        {
            g.Rect(X + 2, Y + 3, 2, 2, "#6e767b"); g.Rect(X + 6, Y + 6, 2, 1, "#6e767b");
            g.Rect(X + 1, Y + 7, 3, 1, "#7fc0d6");
        }
        if (t == 'p') { g.Rect(X + 2, Y + 3, 2, 1, "#a88c58"); g.Rect(X + 6, Y + 7, 1, 1, "#a88c58"); }
        if (t == 'd')
        {
            g.Rect(X, Y + 1, T, 8, "#9a6a3a");
            for (int i = 0; i < T; i += 3) g.Rect(X + i, Y + 1, 1, 8, "#7a5230");
            g.Rect(X, Y + 1, T, 1, "#b58250");
            g.Rect(X, Y + 9, T, 1, "#1f5a7a");
            g.Rect(X + 1, Y + 9, 1, 1, "#5b3a24"); g.Rect(X + 8, Y + 9, 1, 1, "#5b3a24");
        }
        else if (t == 'b')
        {
            g.Rect(X + 1, Y, 8, T, "#9a6a3a");
            for (int i = 0; i < T; i += 3) g.Rect(X + 1, Y + i, 8, 1, "#7a5230");
            g.Rect(X + 1, Y, 1, T, "#b58250");
            g.Rect(X + 9, Y, 1, T, "#1f5a7a");
            g.Rect(X, Y + 4, 1, 2, "#5b3a24");
        }
        else if (t == 'x')
        {
            g.Rect(X + 1, Y + 2, 3, 6, "#7a5230");
            g.Rect(X + 6, Y + 4, 3, 2, "#7a5230");
            g.Rect(X + 1, Y + 9, 1, 1, "#5b3a24"); g.Rect(X + 8, Y + 9, 1, 1, "#5b3a24");
        }
        if (stumps.TryGetValue((x, y), out char was))
        {
            if (was == 'R') { g.Rect(X + 2, Y + 6, 2, 2, "#8a8f93"); g.Rect(X + 6, Y + 5, 2, 1, "#9aa0a5"); g.Rect(X + 5, Y + 7, 1, 1, "#7d8288"); }
            else if (was == 'c') { g.Rect(X + 4, Y + 6, 3, 3, "#4f8a3c"); g.Rect(X + 4, Y + 6, 3, 1, "#8fbf6a"); }
            else
            {
                g.Rect(X + 3, Y + 5, 4, 4, "#6b4a2b"); g.Rect(X + 3, Y + 5, 4, 1, "#c9a06a");
                g.Rect(X + 4, Y + 5, 2, 1, "#a87d52"); g.Rect(X + 2, Y + 8, 6, 1, "rgba(0,0,0,0.2)");
            }
        }
    }

    /* ---------- World drawing ---------- */
    (int x0, int y0, int x1, int y1) VisibleTiles() =>
        (Math.Max(0, camX / T - 1), Math.Max(0, camY / T - 1), Math.Min(SCols - 1, (camX + W) / T + 1), Math.Min(SRows - 1, (camY + H) / T + 1));

    void DrawWater(float t)
    {
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                char tile = map[y, x];
                string c = tile switch { '~' => "#3a6f99", 'l' => "#5cc7b8", 'o' => "#62dccf", 'm' => "#6b7f4a", 'w' or 'T' => "#6ab0cc", _ => null };
                if (c == null) continue;
                double k = Pix.Hash(x, y, 7);
                int gx = x * T + (int)(k * 7), gy = y * T + (int)(Pix.Hash(x, y, 9) * 9);
                if ((t * 0.35 + k) % 1 < 0.2 && Wet(ShapePx(gx, gy)) && Wet(ShapePx(gx + 1, gy))) pix.Rect(gx, gy, 2, 1, c);
            }
        DrawRipples(t);
        DrawFoam(t);
        DrawTideFlats(t);
    }

    void DrawSpots(float t)
    {
        for (int i = 0; i < Data.Spots.Length; i++)
        {
            var s = Data.Spots[i];
            if (!SpotHere(s)) continue;
            var (sx, sy) = SpotPos(s);
            bool frozen = s.Id == "icehole" && IceFrozen;
            if (Wears("sunglasses") && mode != "spear")
                for (int k = 0; k < 4; k++)
                    if (ShadowPos(s, k, t) is (float hx, float hy, bool big)) DrawShadowFish(hx, hy, big, t, k);
            if (Chummed(s.Id)) DrawChum(sx, sy, s.R, t);
            if (!frozen && s.Id != "icehole") DrawJump(s, i, sx, sy, t);
            if (fish != null && fish.Spot == s.Id && !fish.Wild && mode != "casting") continue;
            if (frozen) continue;
            for (int k = 0; k < 2; k++)
            {
                double ph = (t * 0.6 + k * 0.5 + i * 0.13) % 1;
                pix.Ring(sx, sy, 1.5 + ph * 4, Pal.Rgba(235, 248, 252, (float)(0.75 * (1 - ph))));
            }
            if (Data.Creatures.Any(c => c.Spot == s.Id && Eligible(c)))
            {
                for (int b = 0; b < 3; b++)
                {
                    double ph = (t * 0.8 + b * 0.33) % 1;
                    pix.Rect(s.X - 5 + b * 5 + Math.Sin(t * 3 + b), s.Y + 4 - ph * 9, 1, 1, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
                }
            }
        }
    }

    // A fish shadow drifting under the surface (seen through polarized sunglasses).
    void DrawShadowFish(float x, float y, bool big, float t, int k)
    {
        var c = Pal.Rgba(8, 28, 44, big ? 0.55f : 0.38f);
        int len = big ? 7 : 5, X = (int)MathF.Round(x), Y = (int)MathF.Round(y), dir = MathF.Sin(t * (0.22f + k * 0.07f) + k) > 0 ? -1 : 1;
        pix.Rect(X - len / 2, Y, len, 2, c);
        pix.Rect(X - len / 2 + 1, Y - 1, len - 2, 1, c);
        pix.Rect(X + (dir > 0 ? -len / 2 - 2 : len / 2 + 1), Y - 1 + (int)(t * 6 + k) % 2, 1, 3, c);
    }

    // Fish swirling around a cloud of chum, and bubbles rising.
    void DrawChum(float sx, float sy, float r, float t)
    {
        var murk = Pal.Rgba(120, 80, 50, 0.25f);
        pix.Rect(sx - 6, sy - 2, 12, 4, murk);
        for (int k = 0; k < 5; k++)
        {
            float a = t * (1.6f + k * 0.2f) + k * 1.3f, rr = 4 + k * 1.6f;
            float fx = sx + MathF.Cos(a) * rr * 1.5f, fy = sy + MathF.Sin(a) * rr * 0.6f;
            pix.Rect(fx, fy, 2, 1, Pal.Rgba(10, 30, 45, 0.55f));
            pix.Rect(fx + (MathF.Sin(a) > 0 ? 2 : -1), fy - 1, 1, 1, Pal.Rgba(220, 240, 248, 0.7f));
        }
        for (int b = 0; b < 3; b++)
        {
            double ph = (t * 0.9 + b * 0.33) % 1;
            pix.Rect(sx - 4 + b * 4, sy + 2 - ph * 7, 1, 1, Pal.Rgba(255, 255, 255, (float)(0.8 * (1 - ph))));
        }
    }

    // Every few seconds a fish leaps clear of the water somewhere in a spot and splashes back.
    void DrawJump(Spot s, int i, float sx, float sy, float t)
    {
        float cycle = 4.5f + i % 3, tt = (t + i * 1.7f) % cycle;
        if (tt > 0.8f) return;
        int n = (int)((t + i * 1.7f) / cycle);
        double ang = Pix.Hash(i, n, 55) * Math.PI * 2, rr = Pix.Hash(i, n, 56) * s.R * 0.55;
        float jx = sx + (float)(Math.Cos(ang) * rr), jy = sy + (float)(Math.Sin(ang) * rr * 0.5);
        if (!IsWater(jx - 5, jy) || !IsWater(jx + 5, jy)) return;
        float ph = tt / 0.8f, dir = Pix.Hash(i, n, 57) < 0.5 ? -1 : 1;
        if (ph < 0.65f)
        {
            float k = ph / 0.65f, fx = jx + dir * (k - 0.5f) * 10, fy = jy - MathF.Sin(k * MathF.PI) * 8;
            pix.Rect(fx - 1, fy, 3, 1, "#dfe9ee"); pix.Rect(fx - 1 - dir, fy - (k < 0.5f ? 1 : 0), 1, 1, "#9fc3d1");
            if (k < 0.2f) pix.Ring(jx - dir * 5, jy, 1 + k * 8, Pal.Rgba(235, 248, 252, 0.8f - k * 3));
        }
        else
        {
            float k = (ph - 0.65f) / 0.35f;
            pix.Ring(jx + dir * 5, jy, 1 + k * 4, Pal.Rgba(235, 248, 252, 0.85f * (1 - k)));
            pix.Rect(jx + dir * 5 - 1, jy - 2 - k * 2, 1, 1, Pal.Rgba(255, 255, 255, 1 - k));
            pix.Rect(jx + dir * 5 + 1, jy - 1 - k * 3, 1, 1, Pal.Rgba(255, 255, 255, 1 - k));
        }
    }

    // The ice hole freezes over again by each morning. While you're drilling, cracks spread across the ice.
    void DrawIceCap()
    {
        if (scene != "world" || !IceFrozen) return;
        int X = 600, Y = 80;
        pix.Rect(X + 1, Y + 1, 8, 8, "#cfe6f0"); pix.Rect(X + 2, Y + 2, 6, 6, "#e6f3f8");
        pix.Rect(X + 2, Y + 2, 3, 1, "#ffffff"); pix.Rect(X + 6, Y + 6, 1, 1, "#ffffff");
        if (mode != "drill") return;
        int cracks = (int)(drill * 6);
        var lines = new (int, int, int, int)[] { (5, 5, 2, 2), (5, 5, 8, 3), (5, 5, 3, 8), (5, 5, 8, 7), (5, 5, 1, 5), (5, 5, 6, 1) };
        for (int i = 0; i < cracks && i < lines.Length; i++)
        {
            var (x0, y0, x1, y1) = lines[i];
            pix.Line(X + x0, Y + y0, X + x1, Y + y1, "#7fa9bf");
        }
        if (drill > 0.7f) pix.Rect(X + 4, Y + 4, 2, 2, "#3a6f99");
    }

    // Trees sway a pixel in the breeze, and a lot more in a storm.
    int Sway(int tx, int ty)
    {
        float wind = Stormy ? 3.2f : 1.1f, amp = Stormy ? 1.4f : 0.62f;
        return (int)MathF.Round(MathF.Sin(time * wind + tx * 0.7f + ty * 1.3f) * amp);
    }

    void DrawHut()
    {
        pix.Rect(150, 68, 21, 3, "rgba(0,0,0,0.2)");
        pix.Rect(151, 57, 18, 12, "#9a6a3a");
        foreach (int y in new[] { 60, 63, 66 }) pix.Rect(151, y, 18, 1, "#7a5230");
        pix.Rect(158, 62, 4, 7, "#3b2a1d");
        pix.Rect(164, 60, 3, 3, Night ? "#f3c25b" : "#bfe0ea");
        for (int i = 0; i < 10; i++)
        {
            int half = 2 + (int)(i * 1.25);
            pix.Rect(160 - half, 48 + i, half * 2, 1, i % 2 == 1 ? "#963f2d" : "#b5523b");
        }
        // A stone chimney with smoke curling away on the wind.
        pix.Rect(164, 47, 3, 6, "#7d8288"); pix.Rect(164, 47, 3, 1, "#9aa0a5");
        for (int k = 0; k < 4; k++)
        {
            float ph = (time * 0.35f + k * 0.25f) % 1;
            float sx = 165 + ph * 6 + MathF.Sin(ph * 6 + k) * 1.5f, sy = 45 - ph * 14;
            int r = ph < 0.3f ? 1 : 2;
            pix.Rect(sx - r / 2, sy, r + 1, r, Pal.Rgba(225, 228, 232, 0.55f * (1 - ph)));
        }
    }

    void DrawFire(float t, float fx = FireX, float fy = FireY)
    {
        pix.Rect(fx - 4, fy + 1, 9, 2, "rgba(0,0,0,0.2)");
        pix.Rect(fx - 4, fy, 2, 2, "#7d8288"); pix.Rect(fx + 3, fy, 2, 2, "#7d8288");
        pix.Rect(fx - 2, fy + 1, 5, 1, "#6e737a");
        pix.Rect(fx - 3, fy - 1, 7, 1, "#6b4a2b");
        int f = (int)Math.Floor(t * 8 + fx) % 3;
        pix.Rect(fx - 2, fy - 3, 5, 2, "#e04b3a");
        pix.Rect(fx - 1, fy - 5 - (f == 1 ? 1 : 0), 3, 3, "#f3a83b");
        pix.Rect(fx + (f == 2 ? 1 : 0), fy - 6 - f % 2, 1, 2, "#ffe28a");
        // Sparks drifting up.
        for (int k = 0; k < 3; k++)
        {
            float ph = (t * 0.9f + k * 0.37f + fx * 0.01f) % 1;
            pix.Rect(fx + MathF.Sin(ph * 9 + k * 2) * 2, fy - 7 - ph * 10, 1, 1, Pal.Rgba(255, 210, 110, 1 - ph));
        }
    }

    // Tomas breathes, blinks, and turns his eyes toward you when you're close.
    // Tomas in his navy cap and white beard: pottering round his camp (Folk.cs), and turning his head to you when
    // you're close by.
    void DrawTomas()
    {
        int x = (int)MathF.Round(tomasX), y = (int)MathF.Round(tomasY);
        var s = tomasWalk;
        bool walking = s.Moving && TomasAtCamp;
        int step = walking ? s.Step : 0, bob = walking ? 0 : (time + 0.7f) % 2.4f > 1.5f ? 1 : 0;
        string face = TomasAtCamp ? s.Face : "down";
        string head = !walking && face == "down" && Dist(player.X, player.Y, tomasX, tomasY) < 50
            ? (player.X < tomasX - 12 ? "left" : player.X > tomasX + 12 ? "right" : null) : null;
        LookData.DrawFigure(pix, "#e0b07d", "#e8e8e8", "#2f5d8a", "#2b3a4a", false, 3, "#1d2f45", x, y, face, step,
            bob: bob, blink: (time + 1.3f) % 3.9f < 0.13f, head: head);
        string hf = head ?? face;
        if (hf != "up")
        {
            int up = bob + (step is 2 or 4 ? 1 : 0), fo = hf == "left" ? -1 : hf == "right" ? 1 : 0;
            pix.Rect(x - 2 + fo, y - up - 9, 4, 2, "#e8e8e8");
        }
    }

    void DrawPlayer()
    {
        if (Aboard) { DrawHelmsman(); return; }
        int x = (int)Math.Floor(player.X + 0.5), y = (int)Math.Floor(player.Y + 0.5);
        bool moving = player.Moving && mode is "play" or "build";
        int walk = moving ? 1 + (int)(player.WalkT * 9) % 4 : 0;   // your own four-frame walk
        // Stepping down off Tidemane: from the saddle to the ground in a little hop (Tidemane.cs).
        if (!Riding && hopDown && hopT > 0)
        {
            float k = 1 - hopT / HopDownTime;
            x = (int)MathF.Round(hopFromX + (x - hopFromX) * k);
            y = (int)MathF.Round(hopFromY + (y - hopFromY) * k - MathF.Sin(k * MathF.PI) * 3);
            walk = 0;
        }
        string f = player.Face;
        if (iframes > 0 && (int)(time * 16) % 2 == 0) return; // blink while recovering from a hit
        // Ducked down with your hands over your head while the ground shakes (RestlessSea.cs).
        if (Ducking && !Riding) { DrawDucked(state.look, x, y, f); return; }
        bool holding = heldT > 0 && heldItem != null;
        // Breathing when still, and the odd blink. The walk adds its own bounce.
        int bob = moving || mode == "reeling" ? 0 : (time % 2.2f > 1.4f ? 1 : 0);
        bool blink = time % 3.7f < 0.12f;
        bool rod = fish != null || mode == "charging";
        var (arms, pump, lean, glance) = PlayerPose(rod, holding);
        // Whistling for Tidemane, a hand goes up.
        if (whistleT > 0.2f && !Riding && arms == 0) { arms = 1; pump = (int)(time * 8) % 2; }
        int side = f == "left" ? -1 : 1;
        // Where the upper body is (in the saddle, everything you hold or swing is up where you sit), and what's in
        // your hands there: a tool mid-swing, the rod or the spear (Tools.cs). The arms reach for it.
        var (bx, by) = Riding ? RiderSeat(x, y) : (x, y);
        // In the saddle you lean into a gallop (not while you're holding or swinging anything).
        if (Riding) lean = !rod && !holding && swingT <= 0 && RidePose().Kind == "gallop" ? 1 : 0;
        int rise = Riding ? 0 : bob + (walk is 2 or 4 ? 1 : 0);
        var hands = holding ? null : HandsPose(f, bx + (f is "left" or "right" ? lean * side : 0), by - rise, rod, pump);
        if (hands != null) arms = 4;
        if (hands?.ToolBehind == true) DrawToolPose(hands);
        if (Riding)
        {
            DrawRider(x, y, arms, pump, hands, lean);
            (x, y) = (bx, by);
        }
        else LookData.DrawPerson(pix, state.look, x, y, f, walk, bob: bob, blink: blink, arms: arms, swing: pump, lean: lean, head: glance,
            hand: hands?.Hand, hand2: hands?.Hand2, armsBehind: hands?.ArmsBehind == true);
        if (InWater && !Riding)
        {
            // Wading: the water comes up past your knees, with a ripple around you.
            string water = TileAt((int)MathF.Floor(player.X / T), (int)MathF.Floor((player.Y - 1.5f) / T)) switch { 'l' => "#2a9d8f", 'o' => "#22a6a0", 'm' => "#4f6440", _ => "#2f7fa3" };
            pix.Rect(x - 4, y - 3, 9, 4, water);
            pix.Rect(x - 4, y - 3, 9, 1, Pal.Rgba(230, 246, 250, 0.6f));
            pix.Ring(x + 0.5, y - 2, 3 + (time * 2 % 1) * 2, Pal.Rgba(230, 246, 250, 0.5f * (1 - time * 2 % 1)));
        }
        if (hands != null && !hands.ToolBehind)
        {
            DrawToolPose(hands);
            // The hands close round the handle over the tool: the near one from the side (the far one really is behind
            // the shaft), both from the front; from behind they're out of sight in front of you.
            if (!hands.ArmsBehind)
            {
                string skin = LookData.Skins[state.look.skin % LookData.Skins.Length];
                pix.Rect(hands.Hand.x, hands.Hand.y, 1, 1, skin);
                if (f == "down" && hands.Hand2 is { } h2) pix.Rect(h2.x, h2.y, 1, 1, skin);
            }
        }
        if (holding) DrawHeld(x, y - 17);
    }

    // How you hold yourself: the rod out in front while fishing (pumping it as you reel, leaning back as you pull and
    // forward when a runner takes line, back to load a cast and forward to throw it), a catch held overhead, or, after
    // standing about for a few seconds, a glance around and now and then a stretch.
    (int arms, int pump, int lean, string head) PlayerPose(bool rod, bool holding)
    {
        if (holding) return (2, 0, 0, null);
        if (rod)
        {
            int lean = mode switch
            {
                "charging" => charge > 0.4f ? -1 : 0,
                "casting" => fish != null && fish.T < 0.35f ? 1 : 0,
                "reeling" => reel?.Running > 0 ? 1 : ReelHeld() ? -1 : 0,
                _ => 0
            };
            return (3, mode == "reeling" && ReelHeld() ? (int)(time * 10) % 2 : 0, lean, null);
        }
        if (mode != "play" || player.Moving || idleT < 3) return (0, 0, 0, null);
        if (idleT > 10 && (idleT - 10) % 15 < 0.9f) return (2, 0, 0, null);
        float ph = (idleT - 3) % 6;
        string glance = ph < 1 ? (player.Face == "down" ? "left" : player.Face == "up" ? "right" : "down")
            : ph is >= 3 and < 4 ? (player.Face is "down" ? "right" : player.Face == "up" ? "left" : "down") : null;
        return (0, 0, 0, glance);
    }

    // What you hold over your head after a catch: the fish (wriggling), or a chest spilling light.
    void DrawHeld(int x, int y)
    {
        float k = 1 - heldT / 1.8f;
        y -= k < 0.15f ? (int)((0.15f - k) * 20) : 0;
        if (heldItem == "chest")
        {
            pix.Rect(x - 4, y - 1, 9, 5, "#8a5f36"); pix.Rect(x - 4, y + 1, 9, 1, "#f3c25b"); pix.Rect(x, y, 1, 3, "#f3c25b");
            pix.Rect(x - 4, y - 4, 9, 2, "#6b4a2b"); pix.Rect(x - 3, y - 2, 7, 1, "#ffe28a");
            pix.Glow(x + 0.5, y - 2, 9, Pal.Rgba(255, 226, 138, 0.35f));
        }
        else if (FishArt.Looks.ContainsKey(heldItem))
        {
            // Its own picture, wriggling (big ones are held up a bit bigger).
            bool flip = (int)(time * 7) % 2 == 0;
            int big = state.big.GetValueOrDefault(heldItem) > 0 && Data.FishById.TryGetValue(heldItem, out var bf) && bf.Kg > 5 ? 1 : 0;
            FishArt.Draw(pix, heldItem, x - 6 - big, y - 2 - big, 13 + big * 2, 8 + big * 2, flip);
        }
        else
        {
            string tint = Items.ById.TryGetValue(heldItem, out var d) ? d.Tint ?? "#cfe8ee" : "#cfe8ee";
            bool flip = (int)(time * 7) % 2 == 0;
            int big = state.big.GetValueOrDefault(heldItem) > 0 && Data.FishById.TryGetValue(heldItem, out var cf) && cf.Kg > 5 ? 1 : 0;
            int fx = x - 4 - big, w = 7 + big * 2;
            pix.Rect(fx + 2, y, w - 3, 3 + big, tint);
            pix.Rect(fx + 3, y - 1, w - 5, 1, tint); pix.Rect(fx + 3, y + 3 + big, w - 5, 1, tint);
            int tx = flip ? fx : fx + w - 1;
            pix.Rect(tx, y - 1 + (flip ? 0 : 1), 1, 2, tint); pix.Rect(tx, y + 2 + big - (flip ? 0 : 1), 1, 2, tint);
            pix.Rect(flip ? fx + w - 2 : fx + 2, y + 1, 1, 1, "#10243a");
        }
        for (int s = 0; s < 3; s++)
            if ((time * 4 + s * 0.33f) % 1 < 0.5f)
                pix.Rect(x - 7 + s * 7, y - 4 + (s % 2) * 6, 1, 1, "#ffffff");
    }

    void DrawWreck()
    {
        pix.Rect(12, 155, 30, 3, "rgba(0,0,0,0.18)");
        for (int i = 0; i < 9; i++) pix.Rect(14 + i, 150 - i / 3, 26 - i * 2, 1, i % 2 == 1 ? "#4a2f1d" : "#5b3a24");
        pix.Rect(14, 150, 26, 6, "#5b3a24");
        pix.Rect(14, 152, 26, 1, "#7a5230");
        pix.Rect(18, 151, 3, 3, "#1b120c"); pix.Rect(30, 152, 4, 2, "#1b120c");
        pix.Line(26, 147, 33, 132, "#4a2f1d");
        pix.Line(27, 147, 34, 132, "#3b2516");
        pix.Rect(30, 136, 6, 1, "#4a2f1d");
        pix.Rect(32, 137, 4, 5, "#d9d2bf");
        pix.Rect(14, 155, 26, 1, "#cfe8ee");
    }

    /* ---------- Things you build ---------- */
    void DrawPath(int X, int Y)
    {
        pix.Rect(X + 1, Y + 1, 4, 3, "#9d968c"); pix.Rect(X + 6, Y + 1, 3, 4, "#aaa398");
        pix.Rect(X + 1, Y + 5, 3, 4, "#aaa398"); pix.Rect(X + 5, Y + 6, 4, 3, "#9d968c");
        pix.Rect(X + 1, Y + 1, 2, 1, "#c8c2b8"); pix.Rect(X + 6, Y + 1, 2, 1, "#c8c2b8");
        pix.Rect(X + 1, Y + 5, 2, 1, "#c8c2b8"); pix.Rect(X + 5, Y + 6, 2, 1, "#c8c2b8");
    }

    bool IsFence(int tx, int ty) => BuildAt(tx, ty)?.id == "fence";

    void DrawFence(int X, int Y, int tx, int ty)
    {
        bool l = IsFence(tx - 1, ty), r = IsFence(tx + 1, ty), u = IsFence(tx, ty - 1), d = IsFence(tx, ty + 1);
        void Rail(int x, int w) { pix.Rect(x, Y + 3, w, 1, "#a87444"); pix.Rect(x, Y + 6, w, 1, "#a87444"); }
        pix.Rect(X + 3, Y + 9, 4, 1, "rgba(0,0,0,0.18)");
        if (l) Rail(X, 4);
        if (r) Rail(X + 6, 4);
        if (!l && !r && !u && !d) Rail(X + 1, 8);
        if (d) pix.Rect(X + 4, Y + 9, 2, 3, "#9a6a3a");
        pix.Rect(X + 4, Y + 1, 2, 8, "#7a5230");
        pix.Rect(X + 4, Y + 1, 2, 1, "#c08a55");
    }

    void DrawLantern(int X, int Y)
    {
        pix.Rect(X + 3, Y + 8, 5, 1, "rgba(0,0,0,0.2)");
        pix.Rect(X + 3, Y + 7, 4, 2, "#3b2a1d");
        pix.Rect(X + 4, Y - 1, 2, 8, "#5b3a24");
        pix.Rect(X + 3, Y - 6, 4, 5, "#1d2f45");
        pix.Rect(X + 4, Y - 5, 2, 3, Night ? "#ffe28a" : "#9fc3d1");
        pix.Rect(X + 2, Y - 7, 6, 1, "#1d2f45");
    }

    void DrawBaitBox(int X, int Y, float t)
    {
        int w = (int)(t * 3) % 2;
        pix.Rect(X + 3 + w, Y + 1, 1, 2, "#e8939a"); pix.Rect(X + 6 - w, Y + 1, 1, 2, "#e8939a");
        pix.Rect(X + 1, Y + 8, 9, 1, "rgba(0,0,0,0.2)");
        pix.Rect(X + 1, Y + 3, 8, 6, "#9a6a3a");
        pix.Rect(X + 1, Y + 3, 8, 1, "#b58250");
        pix.Rect(X + 2, Y + 4, 6, 1, "#4a2f1d");
        pix.Rect(X + 1, Y + 6, 8, 1, "#7a5230");
        pix.Rect(X + 3, Y + 7, 3, 1, "#cfe8ee"); pix.Rect(X + 2, Y + 7, 1, 1, "#9fc3d1");
    }

    void DrawShack(int X, int Y)
    {
        string win = Night ? "#f3c25b" : "#bfe0ea";
        pix.Rect(X, Y + 9, 20, 1, "rgba(0,0,0,0.2)");
        pix.Rect(X + 1, Y - 3, 18, 12, "#a8794a");
        foreach (int o in new[] { 0, 3, 6 }) pix.Rect(X + 1, Y + o, 18, 1, "#8a5f36");
        pix.Rect(X + 8, Y + 3, 4, 6, "#3b2a1d");
        pix.Rect(X + 11, Y + 6, 1, 1, "#f3c25b");
        pix.Rect(X + 3, Y + 1, 3, 3, win); pix.Rect(X + 14, Y + 1, 3, 3, win);
        for (int i = 0; i < 8; i++)
        {
            int half = 2 + (int)(i * 1.3);
            pix.Rect(X + 10 - half, Y - 11 + i, half * 2, 1, i % 2 == 1 ? "#3d6d8c" : "#4c84a8");
        }
    }

    // A bahay kubo: woven bamboo walls raised on four stilts, a steep nipa roof, and bamboo steps up to the door.
    void DrawKubo(int X, int Y)
    {
        string win = Night ? "#f3c25b" : "#3a3024";
        pix.Rect(X, Y + 9, 20, 1, "rgba(0,0,0,0.2)");
        foreach (int sx in new[] { 2, 7, 12, 17 }) { pix.Rect(X + sx, Y + 3, 1, 7, "#6b4a2b"); pix.Rect(X + sx, Y + 3, 1, 1, "#8a6440"); }
        pix.Rect(X + 1, Y + 2, 18, 2, "#a8844e");
        pix.Rect(X + 2, Y - 6, 16, 8, "#d9bf86");
        for (int i = 0; i < 16; i += 2) pix.Rect(X + 2 + i, Y - 6 + (i / 2 % 2), 1, 7, "#c4a668");
        pix.Rect(X + 8, Y - 4, 4, 6, "#4a3a2a");
        pix.Rect(X + 3, Y - 4, 3, 3, win); pix.Rect(X + 14, Y - 4, 3, 3, win);
        for (int s = 0; s < 3; s++) pix.Rect(X + 8, Y + 4 + s * 2, 4, 1, "#c9a06a");
        for (int i = 0; i < 9; i++)
        {
            int half = 3 + i * 9 / 8;
            pix.Rect(X + 10 - half, Y - 15 + i, half * 2, 1, i % 3 == 0 ? "#9a7440" : "#c8a25e");
        }
        pix.Rect(X - 1, Y - 7, 22, 1, "#80643e");
    }

    void DrawBuild(Build b, float t)
    {
        int X = b.x * T, Y = b.y * T;
        switch (b.id)
        {
            case "smoker":
                // A wooden frame with fish hanging over a smouldering fire, smoke drifting up.
                pix.Rect(X + 1, Y + 9, 9, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 1, Y - 6, 1, 15, "#6b4a2b"); pix.Rect(X + 8, Y - 6, 1, 15, "#6b4a2b"); pix.Rect(X, Y - 7, 10, 2, "#8a6440");
                for (int i = 0; i < 3; i++) { pix.Rect(X + 2 + i * 2, Y - 5, 1, 1, "#5b3a24"); pix.Rect(X + 2 + i * 2, Y - 4, 2, 4, i == 1 ? "#c98b3a" : "#b5764a"); }
                pix.Rect(X + 2, Y + 6, 6, 2, "#7d8288"); pix.Rect(X + 3, Y + 5, 4, 1, (int)(t * 4) % 2 == 0 ? "#e04b3a" : "#f3a83b");
                for (int k = 0; k < 3; k++)
                {
                    float ph = (t * 0.5f + k * 0.33f) % 1;
                    pix.Rect(X + 4 + MathF.Sin(ph * 7 + k) * 2, Y + 3 - ph * 18, 2, 2, Pal.Rgba(220, 222, 226, 0.45f * (1 - ph)));
                }
                break;
            case "crabpot":
            {
                // A red-and-white float bobbing over the pot, with a ripple. Bubbles once it's ready to haul.
                int bob = (int)MathF.Round(MathF.Sin(t * 2.2f + b.x) * 0.8f);
                pix.Ring(X + 5, Y + 6, 3.2 + MathF.Sin(t * 2.2f + b.x) * 0.5, Pal.Rgba(230, 246, 250, 0.45f));
                pix.Rect(X + 4, Y + 2 + bob, 3, 2, "#e04b3a"); pix.Rect(X + 4, Y + 4 + bob, 3, 2, "#f2efe6"); pix.Rect(X + 5, Y + 1 + bob, 1, 1, "#3b3b3b");
                if (scene == "world" && PotReady(b))
                    for (int k = 0; k < 2; k++)
                    {
                        double ph = (t * 0.8 + k * 0.5 + b.y * 0.1) % 1;
                        pix.Rect(X + 2 + k * 5, Y + 7 - ph * 6, 1, 1, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
                    }
                break;
            }
            case "bubo":
            {
                // The woven cone lies just under the surface, its mouth facing the bank; a bamboo stake marks it.
                pix.Ring(X + 6, Y + 6, 3 + MathF.Sin(t * 2 + b.x) * 0.4, Pal.Rgba(230, 246, 250, 0.35f));
                pix.Rect(X + 1, Y + 5, 3, 4, Pal.Rgba(170, 125, 70, 0.55f)); pix.Rect(X + 4, Y + 6, 4, 2, Pal.Rgba(150, 110, 60, 0.5f));
                pix.Rect(X + 1, Y + 5, 1, 4, Pal.Rgba(80, 55, 30, 0.6f));
                for (int i = 0; i < 3; i++) pix.Rect(X + 3 + i * 2, Y + 5 + (i > 0 ? 1 : 0), 1, i > 0 ? 2 : 4, Pal.Rgba(90, 62, 34, 0.5f));
                pix.Rect(X + 7, Y - 2, 1, 8, "#c9a06a"); pix.Rect(X + 7, Y - 2, 1, 1, "#e8d0a0"); pix.Rect(X + 7, Y + 1, 1, 1, "#a8844e");
                pix.Rect(X + 6, Y + 3, 3, 1, "#8a6440");
                if (scene == "world" && PotReady(b))
                    for (int k = 0; k < 2; k++)
                    {
                        double ph = (t * 0.8 + k * 0.5 + b.y * 0.1) % 1;
                        pix.Rect(X + 3 + k * 3, Y + 6 - ph * 6, 1, 1, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
                    }
                break;
            }
            case "dryrack":
            {
                // A low bamboo frame with a net across it; salted fish laid on it go from silver to sun-dried gold.
                pix.Rect(X + 1, Y + 9, 9, 1, "rgba(0,0,0,0.22)");
                pix.Rect(X + 1, Y + 3, 1, 6, "#8a6440"); pix.Rect(X + 8, Y + 3, 1, 6, "#8a6440");
                pix.Rect(X, Y + 3, 10, 2, "#c9a06a"); pix.Rect(X, Y + 3, 10, 1, "#e0c088");
                for (int i = 1; i < 10; i += 2) pix.Rect(X + i, Y + 4, 1, 1, "#a8844e");
                pix.Rect(X + 2, Y + 7, 6, 1, "#a8844e");
                var load = scene == "world" ? Rack(b) : null;
                if (RackIsGuso(load))
                {
                    // Guso spread along the rack in tangles, bleaching from green-brown to pale straw as it dries.
                    float k = Math.Min(1, load.dry / DryGoal);
                    var c = Pal.Rgba((byte)(95 + (217 - 95) * k), (byte)(110 + (196 - 110) * k), (byte)(46 + (138 - 46) * k), 1);
                    var tip = Pal.Rgba((byte)(200 + (242 - 200) * k), (byte)(192 + (234 - 192) * k), (byte)(112 + (208 - 112) * k), 1);
                    for (int i = 0; i < load.fish.Count; i++)
                    {
                        int gx = X + 1 + i * 3 / 2, up = i % 2;
                        pix.Rect(gx, Y + 1 + up, 1, 4 - up, c);
                        pix.Rect(gx, Y + 1 + up, 1, 1, tip);
                    }
                }
                else if (load != null)
                {
                    float k = Math.Min(1, load.dry / DryGoal);
                    var c = Pal.Rgba((byte)(200 - (200 - 201) * k), (byte)(212 - (212 - 154) * k), (byte)(220 - (220 - 82) * k), 1);
                    for (int i = 0; i < load.fish.Count; i++)
                    {
                        pix.Rect(X + 1 + i * 3, Y + 1, 2, 3, c);
                        pix.Rect(X + 1 + i * 3, Y + 4, 2, 1, Pal.Rgba(c.R, c.G, c.B, 0.6f));
                    }
                }
                break;
            }
            case "path": DrawPath(X, Y); break;
            case "fence": DrawFence(X, Y, b.x, b.y); break;
            case "lantern": DrawLantern(X, Y); break;
            case "baitbox": DrawBaitBox(X, Y, t); break;
            case "campfire": DrawFire(t, X + 5, Y + 6); break;
            case "shack": DrawShack(X, Y); break;
            case "kubo": DrawKubo(X, Y); break;
            case "workbench":
                pix.Rect(X + 1, Y + 9, 18, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 2, Y + 4, 2, 5, "#6b4a2b"); pix.Rect(X + 16, Y + 4, 2, 5, "#6b4a2b");
                pix.Rect(X + 1, Y + 1, 18, 3, "#b58250"); pix.Rect(X + 1, Y + 1, 18, 1, "#d39a62");
                pix.Rect(X + 1, Y + 4, 18, 2, "#8a5f36");
                pix.Rect(X + 4, Y - 1, 5, 2, "#9aa0a5"); pix.Rect(X + 5, Y - 2, 1, 1, "#6b4a2b");
                pix.Rect(X + 12, Y, 4, 1, "#c0c5c9"); pix.Rect(X + 14, Y - 2, 1, 2, "#8a6440");
                break;
            case "furnace":
                pix.Rect(X + 1, Y + 9, 8, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 1, Y - 5, 8, 14, "#6e737a");
                for (int r = 0; r < 4; r++) pix.Rect(X + 1, Y - 3 + r * 3, 8, 1, "#5e6468");
                pix.Rect(X + 3, Y - 9, 4, 4, "#5e6468");
                pix.Rect(X + 3, Y + 3, 4, 5, "#1b120c");
                pix.Rect(X + 3, Y + 5 + (int)(t * 6) % 2, 4, 2, "#f3a83b"); pix.Rect(X + 4, Y + 6, 2, 1, "#ffe28a");
                break;
            case "stove":
                pix.Rect(X + 1, Y + 9, 8, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 1, Y, 8, 9, "#3b3b3b"); pix.Rect(X + 1, Y, 8, 1, "#5e6468");
                pix.Rect(X + 2, Y - 3, 5, 3, "#8a8f93"); pix.Rect(X + 2, Y - 3, 5, 1, "#b4b9bc");
                pix.Rect(X + 3, Y + 4, 4, 3, "#1b1b1b"); pix.Rect(X + 3, Y + 5, 4, 1, "#e04b3a");
                if ((t * 1.5) % 1 < 0.6) pix.Rect(X + 4, Y - 5 - (int)(t * 4) % 2, 1, 2, "#dfe9ee");
                break;
            case "bed":
                pix.Rect(X + 1, Y + 9, 18, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 1, Y + 2, 18, 7, "#7a5230");
                pix.Rect(X + 2, Y + 2, 16, 5, "#e8e8e8");
                pix.Rect(X + 7, Y + 2, 11, 5, scene == "house:tomas" ? "#2f5d8a" : "#b5523b");
                pix.Rect(X + 7, Y + 4, 11, 1, scene == "house:tomas" ? "#3f7da8" : "#d9734f");
                pix.Rect(X + 2, Y + 3, 4, 3, "#ffffff");
                pix.Rect(X + 1, Y + 1, 2, 8, "#5b3a24");
                break;
            case "table":
                pix.Rect(X + 1, Y + 9, 8, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 2, Y + 5, 1, 4, "#6b4a2b"); pix.Rect(X + 7, Y + 5, 1, 4, "#6b4a2b");
                pix.Rect(X + 1, Y + 3, 8, 2, "#b58250"); pix.Rect(X + 1, Y + 3, 8, 1, "#d39a62");
                pix.Rect(X + 4, Y + 1, 2, 2, "#e8e8e8"); pix.Rect(X + 6, Y + 1, 1, 1, "#e8e8e8");
                break;
            case "rug":
                pix.Rect(X + 1, Y + 2, 18, 6, "#b5523b");
                pix.Rect(X + 2, Y + 3, 16, 4, "#e8cf96");
                pix.Rect(X + 3, Y + 4, 14, 2, "#2f7fa3");
                break;
            case "plant":
                pix.Rect(X + 2, Y + 9, 6, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 3, Y + 5, 4, 4, "#b5523b"); pix.Rect(X + 3, Y + 5, 4, 1, "#d9734f");
                pix.Rect(X + 2, Y + 1, 2, 4, "#4c9a45"); pix.Rect(X + 4, Y - 1, 2, 6, "#5aa047"); pix.Rect(X + 6, Y + 1, 2, 4, "#4c9a45");
                break;
            case "berrybush":
                pix.Rect(X + 1, Y + 8, 8, 2, "rgba(0,0,0,0.18)");
                pix.Rect(X + 1, Y + 3, 8, 6, "#3f7d35"); pix.Rect(X, Y + 5, 10, 3, "#3f7d35"); pix.Rect(X + 2, Y + 2, 6, 1, "#3f7d35");
                pix.Rect(X + 2, Y + 3, 3, 2, "#5aa047"); pix.Rect(X + 6, Y + 5, 2, 1, "#5aa047");
                pix.Rect(X + 2, Y + 8, 6, 2, "#8a6440");
                if (state.picked.GetValueOrDefault($"b:{b.x},{b.y}") != state.day)
                    foreach (var (bx, by) in new[] { (2, 5), (6, 3), (4, 7), (7, 6), (1, 7), (5, 4) }) pix.Rect(X + bx, Y + by, 1, 1, "#e04b3a");
                break;
            case "aquarium":
                pix.Rect(X + 1, Y + 9, 18, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 1, Y + 6, 18, 3, "#6b4a2b"); pix.Rect(X + 2, Y + 8, 2, 2, "#5b3a24"); pix.Rect(X + 16, Y + 8, 2, 2, "#5b3a24");
                pix.Rect(X + 1, Y - 6, 18, 12, "#9fd3e6");
                pix.Rect(X + 2, Y - 5, 16, 10, "#3a8db0");
                pix.Rect(X + 2, Y + 3, 16, 2, "#e8cf96");
                pix.Rect(X + 4, Y - 1, 1, 4, "#4c9a45"); pix.Rect(X + 14, Y - 3, 1, 6, "#4c9a45"); pix.Rect(X + 15, Y, 1, 3, "#5aa047");
                if (scene.StartsWith("house:"))
                {
                    var fishIn = state.tanks.GetValueOrDefault($"{scene}|{b.x},{b.y}");
                    if (fishIn != null)
                        for (int i = 0; i < fishIn.Count; i++)
                        {
                            float sw = MathF.Sin(t * (0.7f + i * 0.23f) + i * 1.7f);
                            int fx = X + 9 + (int)MathF.Round(sw * 5), fy = Y - 4 + i * 2 + (int)MathF.Round(MathF.Sin(t * 1.3f + i) * 0.6f);
                            string c = Items.ById[fishIn[i]].Tint ?? "#cfe8ee";
                            bool left = MathF.Cos(t * (0.7f + i * 0.23f) + i * 1.7f) < 0;
                            pix.Rect(fx - 1, fy, 3, 2, c); pix.Rect(left ? fx + 2 : fx - 2, fy, 1, 2, c);
                            pix.Rect(left ? fx - 1 : fx + 1, fy, 1, 1, "#10243a");
                        }
                }
                pix.Rect(X + 3, Y - 5, 2, 1, "#dff4ff"); pix.Rect(X + 1, Y - 7, 18, 1, "#6b4a2b");
                if ((t * 0.7) % 1 < 0.5) pix.Rect(X + 12, Y - 2 - (int)(t * 6) % 5, 1, 1, "#dff4ff");
                break;
            case "lamp":
                pix.Rect(X + 3, Y + 9, 4, 1, "rgba(0,0,0,0.25)");
                pix.Rect(X + 3, Y + 7, 4, 2, "#3b2a1d");
                pix.Rect(X + 4, Y - 4, 2, 11, "#5b3a24");
                pix.Rect(X + 2, Y - 8, 6, 4, Night ? "#ffe28a" : "#f3c25b");
                pix.Rect(X + 2, Y - 8, 6, 1, "#d9a83a");
                break;
        }
    }

    void DrawLoose(float t)
    {
        foreach (var l in state.loose)
        {
            float x = l.x, y = l.y;
            if (l.kind == "worm")
            {
                // A little mound of earth with a worm poking out and wriggling.
                pix.Rect(x - 3, y + 1, 7, 2, "#6b4a2b"); pix.Rect(x - 2, y, 5, 1, "#8a6440"); pix.Rect(x - 1, y - 1, 3, 1, "#8a6440");
                int w = (int)(t * 3 + l.tx) % 3;
                if (w != 2) { pix.Rect(x + (w == 0 ? 0 : 1), y - 2, 1, 1, "#e8939a"); pix.Rect(x, y - 3 + w, 1, 1, "#d9788e"); }
                continue;
            }
            if (l.kind == "glean")
            {
                // A little wet patch with a shell, a dark urchin and a sprig of sea grapes in it, glinting.
                pix.Rect(x - 3, y + 1, 7, 2, "rgba(40,90,110,0.25)");
                pix.Rect(x - 2, y, 2, 1, "#e8d2a8"); pix.Rect(x - 2, y - 1, 2, 1, "#f6e6c4");
                pix.Rect(x + 1, y, 2, 2, "#3a2a4a"); pix.Rect(x + 3, y - 1, 1, 1, "#4a3a5e");
                pix.Rect(x, y - 2, 1, 1, "#5fb04f");
            }
            else if (l.kind == "wood")
            {
                pix.Rect(x - 3, y + 2, 7, 1, "rgba(0,0,0,0.15)");
                pix.Rect(x - 3, y, 6, 2, "#8a6440"); pix.Rect(x - 3, y, 6, 1, "#b08458"); pix.Rect(x + 3, y, 1, 2, "#6b4a2b");
            }
            else
            {
                pix.Rect(x - 2, y + 2, 5, 1, "rgba(0,0,0,0.15)");
                pix.Rect(x - 2, y, 4, 2, "#8a8f93"); pix.Rect(x - 1, y - 1, 2, 1, "#b4b9bc");
            }
            if ((t * 0.5 + Pix.Hash(l.tx, l.ty, 3)) % 1 < 0.08) pix.Rect(x + 1, y - 2, 1, 1, "#ffffff");
        }
    }

    void Outline(int X, int Y, int w, int h, Color c)
    {
        pix.Rect(X, Y, w, 1, c); pix.Rect(X, Y + h - 1, w, 1, c);
        pix.Rect(X, Y, 1, h, c); pix.Rect(X + w - 1, Y, 1, h, c);
    }

    void DrawGhost(float t)
    {
        if (mode != "build" || ghost == null) return;
        var g = ghost;
        if (buildTool == "remove")
        {
            if (g.Target == null) { Outline(g.Tx * T, g.Ty * T, T, T, Pal.C("rgba(255,255,255,0.4)")); return; }
            int X = g.Target.x * T, Y = g.Target.y * T, w = Data.BuildById[g.Target.id].W * T;
            string c = g.Reason != "" ? "rgba(255,255,255,0.5)" : "#e04b3a";
            Outline(X, Y, w, T, Pal.C(c));
            if (g.Reason == "") { pix.Line(X + 2, Y + 2, X + w - 3, Y + T - 3, c); pix.Line(X + w - 3, Y + 2, X + 2, Y + T - 3, c); }
            return;
        }
        var d = Data.BuildById[buildTool];
        pix.Alpha = 0.55f + 0.15f * MathF.Sin(t * 6);
        DrawBuild(new Build { id = d.Id, x = g.Tx, y = g.Ty }, t);
        pix.Alpha = 1;
        Outline(g.Tx * T, g.Ty * T, d.W * T, T, Pal.C(g.Reason != "" ? "#e04b3a" : "#7fd36b"));
    }

    void DrawAnimal(Animal a)
    {
        bool moving = a.Vx != 0 || a.Vy != 0;
        // Standing still, they get on with things: chickens peck, sheep and pigs graze, the dog wags, the cat flicks its tail.
        int pose = 0;
        float ph = (time * 0.7f + a.HomeX * 0.13f) % 3f;
        if (a.Hearts > 0 && a.Kind is "dog" or "cat") pose = (int)(time * 12) % 2 == 0 ? 2 : 0;
        else if (!moving)
            pose = a.Kind switch
            {
                "chicken" => (time * 1.7f + a.HomeX) % 2.2f < 0.35f ? 1 : 0,
                "sheep" or "pig" => ph < 1.4f ? 1 : 0,
                "dog" => (int)(time * 6) % 2 == 0 ? 2 : 0,
                "cat" => ph < 0.35f ? 2 : 0,
                _ => 0
            };
        AnimalArt.Draw(pix, a.Kind, a.X, a.Y, a.Flip, moving ? (int)(time * 8) % 2 : 0, pose);
        if (a.Hearts > 0)
        {
            var (_, h) = AnimalArt.Size(a.Kind);
            float hy = a.Y - h - 5 - (1.5f - a.Hearts) * 6;
            pix.Rect(a.X - 2, hy, 2, 1, "#e04b3a"); pix.Rect(a.X + 1, hy, 2, 1, "#e04b3a");
            pix.Rect(a.X - 2, hy + 1, 5, 1, "#e04b3a"); pix.Rect(a.X - 1, hy + 2, 3, 1, "#e04b3a"); pix.Rect(a.X, hy + 3, 1, 1, "#e04b3a");
        }
    }

    // Pip's market stall: a counter under a striped awning, with fish and bait on display.
    void DrawStall(float t)
    {
        int X = (int)PipX - 11, Y = (int)PipY - 15;
        pix.Rect(X + 1, Y + 24, 22, 2, "rgba(0,0,0,0.22)");
        pix.Rect(X + 1, Y + 2, 2, 23, "#6b4a2b"); pix.Rect(X + 19, Y + 2, 2, 23, "#6b4a2b");
        for (int i = 0; i < 6; i++) pix.Rect(X + i * 4, Y, 4, 5, i % 2 == 0 ? "#e04b3a" : "#f2efe6");
        for (int i = 0; i < 6; i++) pix.Rect(X + i * 4 + 1, Y + 5, 2, 1, i % 2 == 0 ? "#b5523b" : "#d6d1c4");
        pix.Rect(X + 1, Y + 16, 20, 8, "#9a6a3a"); pix.Rect(X + 1, Y + 16, 20, 2, "#b58250");
        pix.Rect(X + 3, Y + 14, 5, 2, "#9fc3d1"); pix.Rect(X + 9, Y + 14, 4, 2, "#e8b04a");
        pix.Rect(X + 15, Y + 13, 4, 3, "#9aa0a5"); pix.Rect(X + 16, Y + 12, 1, 1, "#e8939a");
        pix.Rect(X + 2, Y + 19, 18, 1, "#7a5230");
        pix.Rect(X + 6, Y + 20, 10, 3, "#f3c25b"); pix.Rect(X + 7, Y + 21, 8, 1, "#9a6a3a");
        // After hours a canvas sheet covers the goods.
        if (!PipOpen) { pix.Rect(X + 2, Y + 11, 18, 6, "#b9a27a"); pix.Rect(X + 2, Y + 11, 18, 1, "#d2bf98"); pix.Rect(X + 9, Y + 12, 1, 5, "#a08a62"); }
    }

    // A snowy rock arch over a dark opening, on Frostfang's north shore.
    void DrawCaveMouth()
    {
        int X = MouthX * T - 3, Y = MouthY * T - 14;
        pix.Rect(X, Y + 33, 26, 2, "rgba(0,0,0,0.25)");
        pix.Rect(X, Y + 6, 26, 28, "#5e6b74");
        pix.Rect(X + 2, Y + 3, 22, 4, "#6f7d86");
        pix.Rect(X + 2, Y + 8, 8, 6, "#8f9ea8"); pix.Rect(X + 15, Y + 10, 9, 7, "#8f9ea8");
        pix.Rect(X + 1, Y + 1, 24, 3, "#e8f0f4"); pix.Rect(X + 4, Y, 14, 2, "#ffffff");
        pix.Rect(X + 6, Y + 18, 14, 16, "#0b0908");
        pix.Rect(X + 8, Y + 16, 10, 2, "#0b0908");
        foreach (int ix in new[] { 9, 12, 16 }) pix.Rect(X + ix, Y + 18, 1, 3, "#dff4ff");
        pix.Rect(X + 6, Y + 33, 14, 1, "#3b302a");
    }

    void DrawObjects(float t)
    {
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        bool Visible(float wx, float wy) => wx >= vx0 * T - 20 && wx <= (vx1 + 2) * T + 20 && wy >= vy0 * T - 20 && wy <= (vy1 + 2) * T + 20;
        var list = new List<(float y, Action draw)>();
        foreach (var (tx, ty, kind) in trees)
            if (Visible(tx * T, ty * T)) list.Add((ty * T + 9, () => DrawTree(tx, ty, kind)));
        foreach (var b in state.builds)
            if (b.id != "path" && Visible(b.x * T, b.y * T)) list.Add((b.y * T + 9, () => DrawBuild(b, t)));
        foreach (var c in critters) list.Add((c.Y, () => AnimalArt.Draw(pix, c.Id, c.X, c.Y, c.Vx < 0, (int)(c.T * 10) % 2)));
        foreach (var a in animals)
            if (Visible(a.X, a.Y)) list.Add((a.Y, () => DrawAnimal(a)));
        if (Visible(MouthX * T, MouthY * T)) list.Add(((MouthY + 2) * T, DrawCaveMouth));
        if (Visible(PipX, PipY)) { if (PipOpen) list.Add((PipY, DrawPip)); list.Add((PipY + 9, () => DrawStall(t))); }
        AddArchipelagoObjects(list);
        AddRestlessSeaObjects(list);
        if (player.Y > (HabagatTop - 25) * T) AddHabagatObjects(list);
        if (Has("boat") > 0 && !Aboard)
        {
            var (bx, by) = BoatPosition();
            if (Visible(bx, by)) list.Add((by - 6, () => DrawBoat(bx, by, t)));
        }
        if (Visible(160, 60)) { list.Add((69, DrawHut)); list.Add((FireY + 2, () => DrawFire(t))); }
        if (Visible(CarvingX, CarvingY)) list.Add((CarvingY, DrawCarving));
        if (state.tamed && !state.riding && (call != null || Visible(state.mountX, state.mountY))) list.Add((call is { Phase: not "whistle" } ? call.Y : state.mountY, DrawMountIdle));
        if (boss != null) list.Add((boss.Y, () => DrawBoss(t)));
        if (!TomasInBed) list.Add((tomasY, DrawTomas));
        list.Add((player.Y, DrawPlayer));
        if (Visible(26, 150)) list.Add((157, DrawWreck));
        foreach (var o in list.OrderBy(o => o.y)) o.draw();
    }

    void DrawFishing(float t)
    {
        var f = fish;
        if (f == null) return;
        var tip = RodTip();
        float bx = f.Bx, by = f.By;
        if (mode == "waiting") by += MathF.Round(MathF.Sin(t * 4) * 0.6f);
        bool taut = mode == "reeling";
        int n = Math.Max(6, (int)MathF.Round(Dist(tip.X, tip.Y, bx, by)));
        float sag = taut ? 0 : mode == "casting" ? 1 : 4;
        // A tight line shivers while you fight the fish.
        float buzz = taut ? MathF.Sin(t * 60) * (reel?.Running > 0 ? 1.2f : 0.5f) : 0;
        var line = Pal.C("rgba(240,240,240,0.75)");
        for (int i = 0; i <= n; i++)
        {
            float k = (float)i / n;
            pix.Rect(tip.X + (bx - tip.X) * k, tip.Y + (by - tip.Y) * k + MathF.Sin(MathF.PI * k) * (sag + buzz), 1, 1, line);
        }
        if (mode == "bite" || mode == "reeling" || mode == "chest")
        {
            double ph = (t * 3) % 1;
            pix.Ring(bx, by + 1, 1 + ph * 3, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
            if (mode == "bite")
            {
                // The bobber plunges under; right at the start (the perfect-hook moment) it goes all the way down.
                bool deepDip = BiteWindow - f.BiteT < PerfectWindow;
                pix.Rect(bx - 1, by + (deepDip ? 1 : 0), 3, 1, "#e04b3a");
                pix.Ring(bx, by + 1, 2 + ph * 2, Pal.Rgba(255, 255, 255, 0.6f));
            }
            else
            {
                pix.Rect(bx - 2 + MathF.Round(MathF.Sin(t * 20)), by - 1, 1, 1, "#ffffff");
                pix.Rect(bx + 2 + MathF.Round(MathF.Cos(t * 17)), by - 2, 1, 1, "#ffffff");
                if (reel?.Leap > 0)
                {
                    // The fish leaps clear of the water at the end of the line.
                    float k = 1 - reel.Leap / reel.LeapLen, jx = bx + (k - 0.5f) * 8, jy = by - MathF.Sin(k * MathF.PI) * 10;
                    pix.Rect(jx - 2, jy, 5, 2, "#dfe9ee"); pix.Rect(jx - 3, jy - 1, 1, 1, "#9fc3d1"); pix.Rect(jx - 3, jy + 2, 1, 1, "#9fc3d1");
                    pix.Rect(jx + 1, jy, 1, 1, "#10243a");
                }
            }
        }
        else
        {
            pix.Rect(bx - 1, by - 2, 3, 2, "#e04b3a");
            pix.Rect(bx - 1, by, 3, 1, "#ffffff");
            if (GearId("bobber") == "glow_bobber") pix.Glow(bx, by - 1, 5, Pal.Rgba(127, 232, 107, 0.35f));
        }
        if (mode == "waiting")
        {
            if (f.Spot == "icehole")
            {
                // The jig ring closes in on the bobber; jig as it meets it.
                float ph = f.WaitT % JigBeat / JigBeat;
                pix.Ring(bx, by + 1, 1.5 + (1 - ph) * 7, Pal.Rgba(191, 244, 255, 0.4f + 0.5f * ph));
                if (ph < 0.12f) pix.Ring(bx, by + 1, 2.2, Pal.C("#ffffff"));
            }
            else pix.Ring(bx, by + 1, 2.2, Pal.C("rgba(255,255,255,0.35)"));
        }
    }

    void DrawBang()
    {
        int x = (int)MathF.Round(player.X), y = (int)MathF.Round(player.Y) - 26;
        bool perfect = fish != null && BiteWindow - fish.BiteT < PerfectWindow;
        int jump = perfect ? (int)(time * 20) % 2 : 0;
        y -= jump;
        if (perfect)
        {
            // Two gold marks: hook it now for a perfect hook.
            pix.Rect(x - 5, y - 1, 11, 10, "#10243a");
            pix.Rect(x - 4, y, 9, 8, "#f3c25b");
            pix.Rect(x - 2, y + 1, 1, 4, "#10243a"); pix.Rect(x - 2, y + 6, 1, 1, "#10243a");
            pix.Rect(x + 2, y + 1, 1, 4, "#10243a"); pix.Rect(x + 2, y + 6, 1, 1, "#10243a");
            return;
        }
        pix.Rect(x - 3, y - 1, 7, 10, "#10243a");
        pix.Rect(x - 2, y, 5, 8, "#ffffff");
        pix.Rect(x, y + 1, 1, 4, "#e04b3a");
        pix.Rect(x, y + 6, 1, 1, "#e04b3a");
    }

    void DrawTinyFish(int x, int y, string c)
    {
        pix.Rect(x + 2, y + 1, 5, 3, c);
        pix.Rect(x + 3, y, 3, 1, c); pix.Rect(x + 3, y + 4, 3, 1, c);
        pix.Rect(x, y, 1, 1, c); pix.Rect(x, y + 4, 1, 1, c); pix.Rect(x + 1, y + 1, 1, 3, c);
        pix.Rect(x + 5, y + 1, 1, 1, "#10243a");
    }

    // Drawn in screen space (the camera is zeroed by the caller). The green zone, the fish, the catch meter,
    // and for runners a tension gauge that turns red as the line nears snapping.
    void DrawReel()
    {
        if (reel == null) return;
        bool tension = reel.Style == "runner";
        int x0 = tension ? 280 : 284, w = tension ? 37 : 33;
        const int y0 = 26, bh = 120;
        pix.Rect(x0 - 3, y0 - 5, w, bh + 10, "#10243a");
        pix.Rect(x0 - 2, y0 - 4, w - 2, bh + 8, "#6b4a2b");
        pix.Rect(x0 - 2, y0 - 4, w - 2, 1, "#9a6a3a");
        pix.Rect(x0 + 2, y0, 12, bh, "#12304a");
        // Bubbles rising through the water column.
        for (int b = 0; b < 4; b++)
        {
            double bp = (time * 0.5 + b * 0.27) % 1;
            pix.Rect(x0 + 4 + b * 2.5, y0 + bh - bp * bh, 1, 1, Pal.Rgba(170, 210, 230, (float)(0.5 * (1 - bp))));
        }
        int zy = y0 + (int)MathF.Round(reel.ZoneY), zh = (int)reel.ZoneH;
        pix.Alpha = reel.Leap > 0 ? 0.45f : 0.9f;
        pix.Rect(x0 + 2, zy, 12, zh, reel.Inside ? "#7fd36b" : reel.Digging ? "#c98b3a" : "#4f9a45");
        pix.Alpha = 1;
        pix.Rect(x0 + 2, zy, 12, 1, "#d6f5c6"); pix.Rect(x0 + 2, zy + zh - 1, 12, 1, "#d6f5c6");
        string fishCol = reel.Roll.Exotic ? "#f3c25b" : reel.Roll.Odd ? "#c08a55" : reel.Roll.Rare ? "#b8f0ff" : "#cfe8ee";
        int wiggle = (int)MathF.Round(MathF.Sin(time * 18) * (reel.Running > 0 ? 1.5f : 0.6f));
        if (reel.Leap <= 0) DrawTinyFish(x0 + 4 + wiggle, y0 + (int)MathF.Round(reel.FishY) - 2, fishCol);
        else DrawTinyFish(x0 + 4, y0 - 12 - (int)(MathF.Sin(reel.LeapMark * MathF.PI) * 6), fishCol);
        pix.Rect(x0 + 18, y0, 6, bh, "#12304a");
        int ph = (int)MathF.Round(bh * Math.Clamp(reel.Progress, 0, 1));
        pix.Rect(x0 + 18, y0 + bh - ph, 6, ph, reel.Progress > 0.66f ? "#7fd36b" : reel.Progress > 0.33f ? "#f3c25b" : "#e04b3a");
        if (ph > 2) pix.Rect(x0 + 18, y0 + bh - ph + (int)(time * 40 % Math.Max(1, ph - 1)), 6, 1, Pal.Rgba(255, 255, 255, 0.35f));
        if (tension)
        {
            pix.Rect(x0 + 27, y0, 4, bh, "#12304a");
            int th = (int)MathF.Round(bh * reel.Tension);
            string tc = reel.Tension > 0.7f ? ((int)(time * 10) % 2 == 0 ? "#ff4040" : "#ffb0a0") : reel.Tension > 0.4f ? "#f08a3a" : "#e8c27a";
            pix.Rect(x0 + 27, y0 + bh - th, 4, th, tc);
        }
    }

    /* ---------- Weather and night ---------- */
    // Snow falls wherever the ground (or sea) under a flake belongs to Frostfang.
    void DrawSnow(float t)
    {
        var flake = Pal.C("rgba(255,255,255,0.85)");
        int flakes = (int)(110 + 60 * rainAmt + 90 * stormAmt);
        for (int i = 0; i < flakes; i++)
        {
            double sx = (Pix.Hash(i, 0, 91) * W + t * 6 * (0.5 + Pix.Hash(i, 1, 91)) + Math.Sin(t * 1.3 + i) * 4) % W;
            double sy = (Pix.Hash(i, 2, 91) * H + t * (16 + Pix.Hash(i, 3, 91) * 14)) % H;
            int wx = (int)sx + camX, wy = (int)sy + camY;
            if (BiomeAt(wx / T, wy / T) != 1) continue;
            pix.Fill((int)sx, (int)sy, i % 5 == 0 ? 2 : 1, i % 5 == 0 ? 2 : 1, flake);
        }
    }

    void LightHole(float wx, float wy, float r, float a)
    {
        float x = wx - camX, y = wy - camY;
        int x0 = Math.Max(0, (int)MathF.Floor(x - r)), x1 = Math.Min(W, (int)MathF.Ceiling(x + r));
        int y0 = Math.Max(0, (int)MathF.Floor(y - r)), y1 = Math.Min(H, (int)MathF.Ceiling(y + r));
        for (int py = y0; py < y1; py++)
            for (int px = x0; px < x1; px++)
            {
                float d = Dist(px + 0.5f, py + 0.5f, x, y);
                if (d < r) dark[py * W + px] *= 1 - a * (1 - d / r);
            }
    }

    // k is how dark it is (Darkness): the lights, glows and fireflies fade in with it at dusk and out at dawn.
    void DrawNight(float t)
    {
        float k = Darkness;
        // With the moon swallowed, the night is darker still.
        Array.Fill(dark, (eclipse is { Phase: "rise" or "bang" } ? 0.74f : 0.63f) * k);
        LightHole(player.X, player.Y - 6, Wears("headlamp") ? 58 : 32, 0.85f);
        LightHole(FireX, FireY - 3, 44 + MathF.Sin(t * 9) * 2, 1);
        LightHole(165, 61, 12, 0.7f);
        if (fish != null) LightHole(fish.Bx, fish.By, 12, 0.5f);
        foreach (var b in state.builds)
        {
            var d = Data.BuildById[b.id];
            if (d.Light == null) continue;
            float r = d.Light[2] + (b.id == "campfire" ? MathF.Sin(t * 9 + b.x) * 2 : 0);
            LightHole(b.x * T + d.Light[0], b.y * T + d.Light[1], r, b.id is "shack" or "kubo" ? 0.75f : 1);
        }
        // The Starwell glows after dark, enough to light a fight around it (and to catch your eye through the palms).
        LightHole(StarwellX, StarwellY, 62 + MathF.Sin(t * 1.3f) * 4, 0.8f);
        if (boss != null) LightHole(boss.X, boss.Y - 8, 34, 0.7f);
        LightParola(t);
        LightEclipse();
        LightFireflies(t);
        ApplyDark(new Color(8, 16, 40, 255));
        // With the moon in Bakunawa's jaws, the surf doesn't catch any moonlight.
        if (eclipse == null || eclipse.Phase == "spit") DrawMoonlitShore(t, k);
        // Bakawan's glowing water and its alitaptap (Bakawan.cs).
        DrawGlowingSurf(t, k);
        DrawGlowTrail(t, k);
        DrawFireflies(t, k);
        GlowMount(k);
        GlowParola(t, k);
        GlowEclipse(k);
        pix.Glow(StarwellX, StarwellY, 30, Pal.Rgba(120, 220, 255, (0.22f + 0.06f * MathF.Sin(t * 1.3f)) * k));

        var warm = Pal.Rgba(243, 150, 60, 0.18f * k);
        pix.Glow(FireX, FireY - 3, 40, warm);
        foreach (var b in state.builds)
        {
            if (b.id != "campfire" && b.id != "lantern") continue;
            var l = Data.BuildById[b.id].Light;
            pix.Glow(b.x * T + l[0], b.y * T + l[1], l[2] - 4, warm);
        }
        if (Eligible(Data.ById["glowgill"]))
        {
            float gx = 100 + MathF.Sin(t * 0.7f) * 13, gy = 66 + MathF.Cos(t * 0.9f) * 5;
            pix.Glow(gx, gy, 9, Pal.Rgba(127, 243, 255, 0.55f * k));
            pix.Rect(gx, gy, 2, 1, "#bdfaff");
        }
        if (Eligible(Data.ById["abyssal"]) && state.flags.dockFixed)
        {
            float a = (0.4f + 0.4f * MathF.Sin(t * 1.4f)) * k;
            foreach (var (x, y) in new[] { (300, 112), (306, 115), (311, 110), (316, 116), (304, 120) })
                pix.Rect(x, y, 1, 1, Pal.Rgba(255, 215, 106, a));
        }
        // Fireflies drift over Mirewood after dark, and a few over Saltmere's grass, each with a soft glow.
        for (int i = 0; i < 40; i++)
        {
            double fx = Pix.Hash(i, 5, 93) * W + Math.Sin(t * 0.6 + i * 1.7) * 14;
            double fy = Pix.Hash(i, 6, 93) * H + Math.Cos(t * 0.5 + i) * 10;
            int ftx = ((int)fx + camX) / T, fty = ((int)fy + camY) / T;
            byte fb = BiomeAt(ftx, fty);
            if (fb != 3 && !(fb == 0 && i % 3 == 0 && TileAt(ftx, fty) is 'g' or 't')) continue;
            float a = (float)(0.5 + 0.5 * Math.Sin(t * 3 + i * 2.1)) * k;
            pix.Glow(fx + camX + 0.5, fy + camY + 0.5, 4, Pal.Rgba(200, 255, 120, 0.25f * a));
            pix.Fill((int)fx, (int)fy, 1, 1, Pal.Rgba(230, 255, 140, a));
        }
    }

    void ApplyDark(Color c)
    {
        for (int i = 0; i < dark.Length; i++) if (dark[i] > 0.002f) pix.Buf[i] = Pix.Mix(pix.Buf[i], c, dark[i]);
    }

    // Follows the player, and centres scenes smaller than the view (house interiors).
    void UpdateCamera()
    {
        int sw = SCols * T, sh = SRows * T;
        // Under Bakunawa the view frames you and the serpent out in the channel together.
        float fx = player.X, fy = player.Y - 8;
        if (eclipse != null && scene == "world") { fx = (player.X + SerpentX) / 2; fy = (player.Y - 8 + SerpentY - 12) / 2; }
        camX = sw <= W ? -(W - sw) / 2 : Math.Clamp((int)MathF.Round(fx) - W / 2, 0, sw - W);
        camY = sh <= H ? -(H - sh) / 2 : Math.Clamp((int)MathF.Round(fy) - H / 2, 0, sh - H);
        // Big impacts (Tidemane landing, charging into a palm, stomping) shake the view, unless that is turned off in Settings.
        if (quake > 0 && Settings.Data.shake && mode is not ("pause" or "panel"))
        {
            float k = Math.Min(1, quake * 4);
            camX += (int)MathF.Round(MathF.Sin(time * 71) * 2 * k);
            camY += (int)MathF.Round(MathF.Cos(time * 53) * 2 * k);
        }
    }

    void RenderWorld(float t)
    {
        UpdateCamera();
        pix.CamX = camX; pix.CamY = camY;
        pix.CopyFrom(basePix, camX, camY, scene == "world" ? Pal.Sea : scene == "cave" ? Pal.C("#0b0908") : Pal.C("#140f0c"));
        if (scene == "world")
        {
            DrawWater(t);
            DrawTufts(t);
            foreach (var b in state.builds) if (b.id == "path") DrawBuild(b, t);
            DrawRestlessSea(t);
            DrawRiseGround();
            DrawSaltBeds();
            DrawGusoFarm(t);
            DrawLoose(t);
            DrawMountGround();
            DrawBugs(t);
            DrawSpots(t);
            DrawSchools(t);
            DrawSeaLife(t);
            DrawBuoys(t);
            DrawRaceBuoys(t);
            DrawSonar(t);
            DrawStarwell(t);
            DrawIceCap();
            DrawSpearing(t);
            DrawEclipse(t);
            DrawObjects(t);
            CullMountGlints();
            DrawWhistle();
            DrawBossEffects(t);
            DrawFishing(t);
            DrawRelease(t);
            DrawEclipseRing();
            DrawParticles();
            DrawLeaves();
            DrawCloudShadows(t);
            DrawSkyLife(t);
            DrawSnow(t);
            DrawWeatherTint();
            DrawSunGlow();
            if (Darkness > 0) DrawNight(t);
            DrawEclipseSky();
            DrawRain(t);
            DrawGhost(t);
        }
        else if (scene == "cave") RenderCave(t);
        else RenderRoom(t);
        if (mode == "bite") DrawBang();
        pix.CamX = pix.CamY = 0;
        if (mode == "reeling") DrawReel();
        if (fade > 0) pix.Fill(0, 0, W, H, Pal.Rgba(4, 9, 16, fade));
    }
}
