using Raylib_cs;

namespace Fesh;

// Marilaya (1.23), the island of story 5: a fictional island modelled on Marinduque, on a chart of its own at the far
// east of the world (every column from MlStart on), past Amihan. Its coast is the real island's outline, traced from
// OpenStreetMap (© OpenStreetMap contributors, ODbL) and simplified, drawn at MlS tiles to a unit of the trace (about
// 0.7 km a tile, so there's room to walk it); its islets sit where Marinduque's do (Polo, Maniwaya and Mompong to the
// north-east, Salomague to the east, and the Tres Reyes trio to the south-west). Its places follow the real ones too:
// a survey hill above a sheltered harbour in the north-west (Balanacan and its datum station), a river town on the west
// coast (Boac), a forested volcano at the southern point (Malindig) with sulfur springs at its foot (Malbog), limestone
// hills and a cave in the north-east (Bathala, at Ipil), a white beach in the south-east (Poctoy) and a sandbar off the
// north islets that only shows at low tide (Palad). Biome 7 owns the whole band of sea and land from MlStart east.
partial class Game
{
    /* ---------- Where it is ---------- */
    // Marilaya's band: its own chart, sea and biome from this column east to the edge of the world.
    public const int MlStart = 252;
    // Tiles to a unit of the traced outline, and the world tile of the trace's (0, 0).
    public const float MlS = 2.1f;
    public const int MlX = 263, MlY = 4;
    // A point of the trace (its units) in world pixels.
    public static float MlWx(float lx) => (MlX + lx * MlS) * T;
    public static float MlWy(float ly) => (MlY + ly * MlS) * T;
    static float MlLx(float wx) => (wx / T - MlX) / MlS;
    static float MlLy(float wy) => (wy / T - MlY) / MlS;

    // Marinduque's coast (trace units, clockwise from the west), simplified from OpenStreetMap's outline.
    public static readonly (float x, float y)[] MlCoast =
    {
        (0.8f, 10.2f), (1.6f, 12.2f), (2.0f, 14.3f), (1.5f, 18.4f), (3.3f, 19.8f), (5.0f, 23.2f), (6.8f, 24.1f), (8.3f, 24.3f), (9.8f, 25.6f),
        (11.6f, 26.1f), (12.6f, 28.0f), (13.1f, 28.2f), (12.9f, 28.5f), (14.1f, 29.4f), (15.3f, 29.6f), (15.9f, 30.1f), (17.6f, 30.1f),
        (18.8f, 28.8f), (19.7f, 25.9f), (18.9f, 24.4f), (19.2f, 23.8f), (20.1f, 23.5f), (20.5f, 23.0f), (20.8f, 21.6f), (21.9f, 21.2f),
        (22.6f, 20.1f), (23.4f, 20.0f), (23.9f, 19.3f), (24.8f, 19.0f), (25.5f, 17.1f), (26.3f, 16.6f), (27.2f, 16.5f), (26.9f, 16.0f),
        (26.2f, 15.8f), (26.7f, 15.1f), (25.0f, 14.8f), (23.4f, 12.6f), (23.2f, 11.6f), (23.4f, 11.4f), (24.6f, 12.2f), (24.8f, 11.3f),
        (24.4f, 11.1f), (25.1f, 11.0f), (25.4f, 10.4f), (25.1f, 9.4f), (23.8f, 8.1f), (22.2f, 7.2f), (21.5f, 7.3f), (21.1f, 7.7f), (21.2f, 8.1f),
        (19.5f, 8.1f), (18.9f, 7.2f), (19.4f, 7.1f), (20.0f, 7.4f), (19.4f, 5.8f), (19.2f, 5.8f), (19.2f, 5.3f), (17.3f, 5.2f), (18.3f, 4.8f),
        (18.0f, 4.0f), (16.2f, 2.4f), (15.8f, 3.0f), (16.4f, 3.5f), (15.3f, 3.5f), (15.1f, 3.8f), (15.5f, 4.3f), (15.3f, 4.5f), (14.3f, 4.4f),
        (13.9f, 4.7f), (13.0f, 4.0f), (12.9f, 2.5f), (13.2f, 1.9f), (12.8f, 2.5f), (12.8f, 4.0f), (11.8f, 3.6f), (11.2f, 3.9f), (10.8f, 3.4f),
        (10.9f, 3.1f), (10.1f, 2.8f), (9.9f, 3.5f), (9.5f, 3.7f), (9.7f, 4.1f), (9.1f, 4.3f), (7.5f, 3.6f), (7.2f, 3.2f), (7.6f, 3.1f), (7.1f, 2.6f),
        (7.0f, 1.7f), (6.8f, 2.4f), (6.3f, 2.0f), (5.7f, 2.1f), (6.1f, 1.7f), (5.5f, 0.8f), (4.2f, 1.1f), (5.0f, 2.3f), (4.6f, 3.2f), (4.3f, 3.1f),
        (4.6f, 3.7f), (4.9f, 3.7f), (4.7f, 3.3f), (5.4f, 2.9f), (5.7f, 3.1f), (5.7f, 3.3f), (5.0f, 3.5f), (5.2f, 4.2f), (4.5f, 4.7f), (3.8f, 4.6f),
        (3.8f, 4.9f), (4.2f, 5.1f), (4.0f, 5.6f), (3.6f, 5.6f), (3.9f, 5.9f), (3.5f, 5.9f), (3.4f, 6.5f), (3.0f, 6.5f), (3.3f, 7.0f), (2.8f, 7.9f)
    };

    // The islets (trace units): which group each belongs to on the chart, its middle and radii. A little bigger than the
    // real ones, so they still read as islands.
    public static readonly (string group, float cx, float cy, float rx, float ry)[] MlIslets =
    {
        ("north", 21.9f, 5.2f, 1.25f, 1.0f),    // Polo
        ("north", 25.4f, 3.4f, 1.7f, 0.95f),    // Maniwaya
        ("north", 29.2f, 5.0f, 1.15f, 1.05f),   // Mompong, where the arrowroot grows
        ("salomague", 27.6f, 12.8f, 1.05f, 1.15f),
        ("tresreyes", 4.4f, 27.2f, 1.15f, 0.85f),    // Gaspar
        ("tresreyes", 2.3f, 28.9f, 0.85f, 0.75f),    // Melchor
        ("tresreyes", 0.4f, 30.5f, 0.85f, 0.8f)      // Baltasar, the far survey station
    };

    // The chart's names for each part of Marilaya (RegionAt): the island itself and its three groups of islets.
    public static readonly Dictionary<string, string> MlRegionNames = new()
    {
        ["marilaya"] = "Marilaya", ["marilaya:north"] = "the north islets", ["marilaya:salomague"] = "Salomague", ["marilaya:tresreyes"] = "the Tres Reyes"
    };

    /* ---------- Its places (world pixels) ---------- */
    // The survey hill: the datum stone on the headland above the harbour. The far station is on Baltasar, the
    // smallest of the Tres Reyes, sighted across the whole island from the hill, as Station Balanacan sights Baltasar.
    const float DatumX = (MlX + 6.2f * MlS) * T, DatumY = (MlY + 2.0f * MlS) * T;
    const float BaltasarX = (MlX + 0.45f * MlS) * T, BaltasarY = (MlY + 30.5f * MlS) * T;
    // The harbour cove and its jetty (horizontal, tiles MlJettyX0..1 at row MlJettyRow), and where a boat ties up.
    const int MlJettyRow = 13, MlJettyX0 = 270, MlJettyX1 = 273;
    public const float MlJettyX = (MlJettyX1 + 0.5f) * 10, MlJettyY = MlJettyRow * 10 + 7;
    const float MlBoatX = (MlJettyX0 - 1.2f) * 10, MlBoatY = MlJettyRow * 10 + 5;
    // Aling Sela's house above the harbour, and where you wake if you faint anywhere on Marilaya's waters.
    const float SelaX = (MlX + 5.9f * MlS) * T, SelaY = (MlY + 4.7f * MlS) * T;
    const float MlWakeX = SelaX, MlWakeY = SelaY + 9;
    // The river (Boac's), from the hills to the west coast, in trace units.
    static readonly (float x, float y)[] MlRiver = { (10.6f, 14.4f), (9.2f, 13.6f), (7.8f, 13.3f), (6.4f, 12.9f), (4.4f, 12.3f), (2.6f, 12.0f), (1.0f, 11.8f) };
    // Fishing spots (Data.Spots reads these): the harbour cove, the river mouth, the Tres Reyes reef and the white beach.
    public const float MlHarbourSpotX = 2680, MlHarbourSpotY = 141,
        MlRiverSpotX = (MlX + 1.4f * MlS) * 10, MlRiverSpotY = (MlY + 11.9f * MlS) * 10,
        MlReefSpotX = (MlX + 5.4f * MlS) * 10, MlReefSpotY = (MlY + 26.6f * MlS) * 10,
        MlBeachSpotX = (MlX + 22.3f * MlS) * 10, MlBeachSpotY = (MlY + 21.5f * MlS) * 10;
    // The story's places (MarilayaStory.cs), in trace units, with how much open ground each keeps round it (pixels): the
    // limestone exposure and the cave in the north-east hills, the hot springs at Tindig's west foot with their gauges and
    // the dryer, the river town and its square, the butterfly nursery, the trailhead and the summit, the white beach.
    static readonly (string id, float lx, float ly, float clear)[] MlPlaceDefs =
    {
        ("cliff", 20.0f, 11.0f, 40), ("cave", 15.9f, 7.4f, 22), ("springs", 11.0f, 24.5f, 20), ("dryer", 10.2f, 23.4f, 14), ("streamgauge", 12.15f, 25.3f, 10),
        ("town", 4.0f, 11.2f, 46), ("plaza", 4.7f, 10.9f, 32), ("garden", 5.4f, 19.6f, 26), ("arrowroot", 29.2f, 4.9f, 8), ("baltasar", 0.45f, 30.5f, 8),
        ("trailhead", 12.4f, 23.5f, 14), ("summit", 15.6f, 25.4f, 20), ("beach", 21.3f, 20.9f, 16), ("barroot", 24.25f, 3.75f, 10)
    };
    static readonly Dictionary<string, (float x, float y, float clear)> MlPlaces = MlPlaceDefs.ToDictionary(p => p.id, p => (MlWx(p.lx), MlWy(p.ly), p.clear));
    static (float x, float y) MlPlace(string id) { var p = MlPlaces[id]; return (p.x, p.y); }
    // Bundok Tindig: its summit (trace units) and the cone's radii, for the ground, the forest and the hill shading.
    const float TindigLX = 15.6f, TindigLY = 25.4f, TindigRX = 4.9f, TindigRY = 4.5f;
    const float TindigX = (MlX + TindigLX * MlS) * T, TindigY = (MlY + TindigLY * MlS) * T;
    // The stream off Tindig to the sea, past the springs.
    static readonly (float x, float y)[] MlCreek = { (13.4f, 24.3f), (12.7f, 24.9f), (12.2f, 25.6f), (11.75f, 26.4f) };
    // The trail up Tindig from the springs' side: switchbacks through the forest to the rocky top.
    static readonly (float x, float y)[] MlTrail = { (12.4f, 23.5f), (13.6f, 22.7f), (14.7f, 23.3f), (13.8f, 24.0f), (14.9f, 24.5f), (14.4f, 25.1f), (15.6f, 25.4f) };

    // The name of the part of Marilaya you're on, for the location line (Game.LocationLine).
    string MarilayaPlaceName(float x, float y)
    {
        if (Dist(x, y, MlJettyX, MlJettyY) < 26) return "Marilaya harbour";
        if (Dist(x, y, DatumX, DatumY) < 30) return "Survey hill";
        if (Dist(x, y, MlPlace("town").x, MlPlace("town").y) < 70) return "River town";
        float tx = (MlLx(x) - TindigLX) / TindigRX, ty = (MlLy(y) - TindigLY) / TindigRY;
        if (tx * tx + ty * ty < 0.06f) return "Tindig's summit";
        if (tx * tx + ty * ty < 1) return "Bundok Tindig";
        string r = RegionOf(x, y);
        return r == "marilaya" ? "Marilaya" : char.ToUpper(MlRegionNames[r][0]) + MlRegionNames[r][1..];
    }

    static bool InMarilayaBox(float wx, float wy) => wx >= MlStart * T;
    // Marilaya's whole band, in tiles (the survey panel's chart is drawn from it).
    const int MlBoxX0 = MlStart, MlBoxY0 = 0, MlBoxX1 = COLS, MlBoxY1 = ROWS;
    bool InMarilaya => scene == "world" && InMarilayaBox(player.X, player.Y);
    // On Marilaya or in its shallows (within about a tile and a half of its coasts), not just in its sea.
    bool NearMarilaya(float wx, float wy)
    {
        int tx = (int)MathF.Floor(wx / T), ty = (int)MathF.Floor(wy / T);
        return InMarilayaBox(wx, wy) && tx < COLS && ty >= 0 && ty < ROWS && mlRegion[ty, tx] != 0;
    }

    // Which part of the outdoors a point is in, for discovery, fishing the open sea, where you wake, and so on: Marilaya's
    // band at the far east first, then Amihan east of EastStart, Habagat along the bottom, and the west (Saltmere and its
    // neighbours, the atoll, the open sea between).
    string ZoneAt(float wx, float wy) => InMarilayaBox(wx, wy) ? "marilaya" : wx >= EastStart * T ? "amihan" : wy >= HabagatTop * T ? "habagat" : "west";

    /* ---------- Making it ---------- */
    // Each tile's region in the band (0 none, 1 the island, 2 north islets, 3 Salomague, 4 Tres Reyes), set by GenerateMarilaya.
    readonly byte[,] mlRegion = new byte[ROWS, COLS];
    // The ground under each of its trees, bushes and boulders (Ground).
    readonly char[,] mlGround = new char[ROWS, COLS];

    static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay, t = Math.Clamp(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy), 0, 1);
        float dx = px - ax - vx * t, dy = py - ay - vy * t;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    static float PolyDist(float px, float py, (float x, float y)[] line)
    {
        float d = float.MaxValue;
        for (int i = 1; i < line.Length; i++) d = Math.Min(d, SegDist(px, py, line[i - 1].x, line[i - 1].y, line[i].x, line[i].y));
        return d;
    }

    static bool InPolygon(float px, float py, (float x, float y)[] poly)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (poly[i].y > py != poly[j].y > py && px < (poly[j].x - poly[i].x) * (py - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }

    // How far a point (trace units) is from the island's coast, in trace units (negative inside).
    static float MlCoastDist(float lx, float ly)
    {
        float d = float.MaxValue;
        for (int i = 0, j = MlCoast.Length - 1; i < MlCoast.Length; j = i++) d = Math.Min(d, SegDist(lx, ly, MlCoast[j].x, MlCoast[j].y, MlCoast[i].x, MlCoast[i].y));
        // The survey hill's headland, a little broader than the real one so the stone has a hilltop to stand on.
        float hx = (lx - 6.0f) / 1.5f, hy = (ly - 2.0f) / 1.15f;
        float head = (MathF.Sqrt(hx * hx + hy * hy) - 1) * 1.15f;
        float coast = InPolygon(lx, ly, MlCoast) ? -d : d;
        return Math.Min(coast, head);
    }

    static (float d, int i) MlIsletDist(float lx, float ly)
    {
        float best = float.MaxValue; int bi = -1;
        for (int i = 0; i < MlIslets.Length; i++)
        {
            var s = MlIslets[i];
            float qx = (lx - s.cx) / s.rx, qy = (ly - s.cy) / s.ry, d = (MathF.Sqrt(qx * qx + qy * qy) - 1) * Math.Min(s.rx, s.ry);
            if (d < best) { best = d; bi = i; }
        }
        return (best, bi);
    }

    static double MlNoise(int x, int y) => 0.16 * Math.Sin(x * 0.9 + y * 0.5) + 0.1 * Math.Cos(x * 0.43 - y * 1.1) + 0.06 * Math.Sin(x * 1.7 - y * 1.3);

    // How high the land stands (0 at the shore, 1 at Tindig's top), for the forest and the hill shading: Tindig's broad
    // cone, the uplands down the middle of the island, the limestone hills in the north-east and the survey hill.
    static float MlHeight(float lx, float ly)
    {
        float tx = (lx - TindigLX) / TindigRX, ty = (ly - TindigLY) / TindigRY, tr = MathF.Sqrt(tx * tx + ty * ty);
        float h = tr < 1 ? MathF.Pow(1 - tr, 1.35f) : 0;
        // A little crater at the very top.
        if (tr < 0.07f) h -= (0.07f - tr) * 1.2f;
        float spine = SegDist(lx, ly, 8.6f, 6.6f, 12.6f, 19.8f);
        h += 0.34f * MathF.Pow(MathF.Max(0, 1 - spine / 4.4f), 1.5f);
        float kx = (lx - 17.6f) / 4.4f, ky = (ly - 9.4f) / 2.9f, kr = MathF.Sqrt(kx * kx + ky * ky);
        h += 0.26f * MathF.Max(0, 1 - kr) * (0.75f + 0.25f * MathF.Sin(lx * 2.3f) * MathF.Cos(ly * 1.9f));
        float sx = (lx - 6.1f) / 1.3f, sy = (ly - 2.0f) / 1.0f, sr = MathF.Sqrt(sx * sx + sy * sy);
        h += 0.2f * MathF.Max(0, 1 - sr);
        return h;
    }

    // Light from the north-west: slopes facing it a tone lighter, slopes facing away a tone darker (GroundPixel).
    static float MlShade(int wx, int wy)
    {
        float lx = MlLx(wx + 0.5f), ly = MlLy(wy + 0.5f), e = 0.12f;
        float gx = (MlHeight(lx + e, ly) - MlHeight(lx - e, ly)) / (2 * e), gy = (MlHeight(lx, ly + e) - MlHeight(lx, ly - e)) / (2 * e);
        return Math.Clamp((gx + gy) * 1.3f, -0.55f, 0.55f);
    }

    // The forest on the volcano and the high middle of the island; limestone shows through in the hills of the north-east.
    static bool MlForest(float lx, float ly) => MlHeight(lx, ly) > 0.2f;
    static bool MlLimestone(float lx, float ly)
    {
        float kx = (lx - 17.6f) / 4.6f, ky = (ly - 9.0f) / 3.2f;
        return kx * kx + ky * ky < 1;
    }
    static bool InTindig(float lx, float ly)
    {
        float tx = (lx - TindigLX) / TindigRX, ty = (ly - TindigLY) / TindigRY;
        return tx * tx + ty * ty < 1;
    }

    void GenerateMarilaya()
    {
        for (int y = 0; y < ROWS; y++)
            for (int x = MlStart; x < COLS; x++)
            {
                biome[y, x] = 7;
                mlRegion[y, x] = 0;
                mlGround[y, x] = '\0';
                map[y, x] = '~';
                if (x == COLS - 1 || y == 0 || y == ROWS - 1) continue;
                float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                float coast = MlCoastDist(lx, ly);
                var (islet, ii) = MlIsletDist(lx, ly);
                bool main = coast <= islet;
                // Distance to the shore in tiles: a tile of sand, shallows a little wider, deep water beyond.
                float d = Math.Min(coast, islet) * MlS;
                char t = d < -0.95f ? (main && MlLimestone(lx, ly) && VNoise(x * T, y * T, 34, 701) + (Pix.Hash(x, y, 702) - 0.5) * 0.25 > 0.42 ? 'e' : main && MlForest(lx, ly) ? 'j' : 'g')
                    : d < 0 ? 's' : d < 1.25 + MlNoise(x, y) ? 'w' : '~';
                map[y, x] = t;
                if (d < 2.4f)
                    mlRegion[y, x] = main ? (byte)1 : MlIslets[ii].group switch { "north" => (byte)2, "salomague" => (byte)3, _ => (byte)4 };
            }
        // The sandbar off Maniwaya, out at low tide (MarilayaPlaces.cs).
        MakeSandbar();
        // Tindig's upper slopes: the forest gives way to a ring of mossy meadow, then bare rock and scree near the top.
        for (int y = 0; y < ROWS; y++)
            for (int x = MlStart; x < COLS; x++)
            {
                if (map[y, x] is not ('j' or 'g')) continue;
                float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                if (!InTindig(lx, ly)) continue;
                float h = MlHeight(lx, ly) + (VNoise(x * T, y * T, 30, 704) - 0.5f) * 0.1f;
                if (h > 0.6f) map[y, x] = 'e';
                else if (h > 0.42f) map[y, x] = 'g';
            }
        // Tindig's rocky top.
        for (int y = (int)(MlY + (TindigLY - 1) * MlS); y <= MlY + (TindigLY + 1) * MlS; y++)
            for (int x = (int)(MlX + (TindigLX - 1) * MlS); x <= MlX + (TindigLX + 1) * MlS; x++)
                if (Dist(x + 0.5f, y + 0.5f, MlX + TindigLX * MlS, MlY + TindigLY * MlS) < 2.2f) map[y, x] = 'e';
        // The harbour: a cove in the side of the headland, deep enough for a banca, with a jetty out into it.
        for (int y = MlJettyRow - 3; y <= MlJettyRow + 3; y++)
            for (int x = MlJettyX0 - 4; x <= MlJettyX1 + 2; x++)
                if (InEllipse(x, y, MlJettyX0 - 0.5, MlJettyRow + 0.2, 3.6, 2.2)) map[y, x] = 'w';
                else if (InEllipse(x, y, MlJettyX0 - 0.5, MlJettyRow + 0.2, 3.6, 2.2, 1.6) && !WaterTile(map[y, x])) map[y, x] = 's';
        for (int x = MlJettyX0; x <= MlJettyX1; x++) map[MlJettyRow, x] = 'd';
        // Boac's river, wadeable fresh water from the hills to the sea, two tiles wide near its mouth.
        for (int y = (int)(MlY + 8 * MlS); y < MlY + 17 * MlS; y++)
            for (int x = MlX; x < MlX + 12 * MlS; x++)
            {
                float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                float along = Math.Clamp((10.6f - lx) / 9.6f, 0, 1), width = 0.2f + 0.26f * along;
                if (PolyDist(lx, ly, MlRiver) < width && map[y, x] is not ('~' or 'w')) map[y, x] = 'l';
            }
        // The stream off Tindig past the springs: the ordinary water the spring gauge is compared with.
        for (int y = (int)(MlY + 23 * MlS); y < MlY + 27.5f * MlS; y++)
            for (int x = (int)(MlX + 10.5f * MlS); x < MlX + 14 * MlS; x++)
            {
                float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                if (PolyDist(lx, ly, MlCreek) < 0.22f && map[y, x] is not ('~' or 'w')) map[y, x] = 'l';
            }
        // The footbridge across it in the town.
        foreach (int y in Enumerable.Range((int)(MlY + 11.4f * MlS), 5))
            if (map[y, (int)(MlX + 4.6f * MlS)] == 'l') map[y, (int)(MlX + 4.6f * MlS)] = 'b';
        // The trail up Tindig, and the town's lanes.
        foreach (var (line, ground) in new[] { (MlTrail, 'p'), (MlLanes, 'p') })
            for (int y = 1; y < ROWS - 1; y++)
                for (int x = MlStart; x < COLS - 1; x++)
                {
                    float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                    if (map[y, x] is 'g' or 'j' or 'e' && PolyDist(lx, ly, line) < 0.28f) map[y, x] = ground;
                }
        // Clear ground round the places, the people and the fishing spots.
        foreach (var (px, py, r) in MlClearings())
            for (int y = (int)((py - r) / T) - 1; y <= (py + r) / T + 1; y++)
                for (int x = (int)((px - r) / T) - 1; x <= (px + r) / T + 1; x++)
                    if (x >= MlStart && x < COLS && y >= 0 && y < ROWS && map[y, x] == 'j' && Dist(x * T + 5, y * T + 5, px, py) < r) map[y, x] = 'g';
        // Trees: broadleaf forest on the mountain (thickest on Tindig) and the high ground, coconut palms along the coast and
        // in the lowlands, limestone boulders in the north-east, and the odd bush.
        for (int y = 1; y < ROWS - 1; y++)
            for (int x = MlStart + 1; x < COLS - 1; x++)
            {
                char t = map[y, x];
                if (t is not ('g' or 'j' or 's' or 'e') || MlKeepClear(x, y)) continue;
                float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                double roll = Pix.Hash(x, y, 703);
                bool mountain = InTindig(lx, ly);
                // Thick forest low on Tindig, thinning out higher up so the slope shows (boulders stay as rare as ever).
                double forest = mountain ? .42 - .3 * Math.Clamp((MlHeight(lx, ly) - .25f) / .2f, 0, 1) : .3;
                char kind = mountain && t == 'g' ? (roll < .1 ? 't' : roll < .13 ? 'R' : '\0') : t switch
                {
                    's' => roll < .08 ? 'h' : '\0',
                    'j' => roll < forest ? 't' : roll < forest + (mountain ? .02 : .03) ? 'R' : '\0',
                    'e' => roll < .14 ? 'R' : roll < .21 ? 't' : '\0',
                    _ => roll < .06 ? 'h' : roll < .085 ? 't' : roll < .105 ? 'y' : roll < .115 ? 'R' : '\0'
                };
                if (kind == '\0') continue;
                if (t != 's' && kind != 'h' && (!Walkable(map[y - 1, x]) || !Walkable(map[y + 1, x]) || !Walkable(map[y, x - 1]) || !Walkable(map[y, x + 1]))) continue;
                mlGround[y, x] = t;
                string key = $"{x},{y}";
                if (state.felled.TryGetValue(key, out int day))
                {
                    if (state.day - day < (kind == 'R' ? BoulderRegrowDays : TreeRegrowDays) || RegrowBlocked(x, y))
                    { stumps[(x, y)] = kind; continue; }
                    state.felled.Remove(key);
                }
                map[y, x] = kind; trees.Add((x, y, kind));
            }
    }

    // The town's lanes (trace units): along the river's north bank, up to the plaza and the road to the harbour.
    static readonly (float x, float y)[] MlLanes = { (2.4f, 11.2f), (4.0f, 11.3f), (5.4f, 11.7f), (6.4f, 12.3f), (5.4f, 11.7f), (4.8f, 9.0f), (5.4f, 6.6f), (5.9f, 5.2f) };

    // Open ground kept round Marilaya's places, people and fishing spots (pixels: x, y, radius).
    IEnumerable<(float x, float y, float r)> MlClearings()
    {
        yield return (DatumX, DatumY, 24);
        yield return (SelaX, SelaY, 34);
        yield return (SelaX + 34, SelaY + 20, 36);   // where the storytellers gather
        yield return (MlJettyX, MlJettyY, 24);
        foreach (var p in MlPlaces.Values) yield return (p.x, p.y, p.clear);
        foreach (var (hx, hy, _) in MlHouses) yield return (hx, hy, 26);
        foreach (var s in Data.Spots.Where(s => s.Biome == "marilaya" && s.Scene == "world")) yield return (s.X, s.Y, s.R + 10);
    }

    bool MlKeepClear(int x, int y)
    {
        float px = x * T + 5, py = y * T + 5;
        if (MlClearings().Any(c => Dist(px, py, c.x, c.y) < c.r + 4)) return true;
        if (Math.Abs(y - MlJettyRow) <= 2 && x >= MlJettyX0 - 1 && x <= MlJettyX1 + 3) return true;
        float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
        // A tile either side of the trail and the lanes, and of the river's banks.
        return PolyDist(lx, ly, MlTrail) < 0.75f || PolyDist(lx, ly, MlLanes) < 0.7f || PolyDist(lx, ly, MlRiver) < 0.75f || PolyDist(lx, ly, MlCreek) < 0.6f;
    }

    // The part of Marilaya a tile belongs to, for the chart (RegionAt).
    string MarilayaRegion(int tx, int ty) => mlRegion[ty, tx] switch { 2 => "marilaya:north", 3 => "marilaya:salomague", 4 => "marilaya:tresreyes", _ => "marilaya" };

    // Ground under a tree, a bush or a boulder on Marilaya.
    char MlGroundUnder(int x, int y) => mlGround[y, x] is char g and not '\0' ? g : 'g';

    /* ---------- Finding it ---------- */
    void DiscoverMarilaya()
    {
        if (!InMarilaya || state.Hinted("marilaya")) return;
        state.hinted["marilaya"] = true;
        chart = 2;
        mapTexDirty = true;
        Toast("Marilaya! A big green island shaped like a heart, with a mountain at its southern point. The harbour is on the north-west side, under a hill. Open <map> for its chart.", 7);
        Save();
    }

    // Sailing close by an islet group charts it (landing on one does too, through ChartHere).
    void ChartNearMlIslets()
    {
        if (!InMarilaya || state.charted == null) return;
        foreach (var s in MlIslets)
        {
            string region = "marilaya:" + s.group;
            if (state.charted.Contains(region) || Dist(player.X, player.Y, MlWx(s.cx), MlWy(s.cy)) > 60) continue;
            state.charted.Add(region);
            mapTexDirty = true;
            Toast($"You've charted {MlRegionNames[region]} of Marilaya.", 2.5f);
        }
    }
}
