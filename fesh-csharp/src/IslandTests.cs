#if DEBUG
using Raylib_cs;

namespace Fesh;

// 1.34: the islands grew. FESH_ISLAND_TEST=1 checks every island's land area, that islands still only meet through
// bridges (no new wading route between two of them), that every outdoor fishing spot can still be fished from land, that
// Saltmere's story tiles are where they were, and that plants keep clear of doors, spots and people. It also writes
// islands.png (the whole world, two pixels a tile) and the land area of each island to the log.
partial class Game
{
    // Each island's land before 1.34 (tiles), so the growth can be measured.
    static readonly Dictionary<string, int> LandBefore = new()
    {
        ["saltmere"] = 205, ["frost"] = 371, ["dunes"] = 710, ["mire"] = 552, ["atoll"] = 710,
        ["amihan:Amihan Village"] = 590, ["amihan:Luntian Karsts"] = 348, ["amihan:Bakawan Island"] = 412, ["amihan:Baga Island"] = 446,
        ["habagat:Asinan"] = 253, ["habagat:Parola"] = 197
    };

    static bool LandTile(char c) => c is not ('~' or 'w' or 'l' or 'T' or 'x' or 'r' or 'o' or 'm' or 'I' or 'k' or 'd' or 'b' or 'i' or 'P');

    // Connected pieces of land (8-way), largest first, each with its region.
    List<(string region, int tiles, HashSet<(int, int)> set)> LandPieces()
    {
        var seen = new bool[ROWS, COLS];
        var pieces = new List<(string, int, HashSet<(int, int)>)>();
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                if (seen[y, x] || !LandTile(worldMap[y, x])) continue;
                var set = new HashSet<(int, int)>();
                var q = new Queue<(int, int)>();
                q.Enqueue((x, y)); seen[y, x] = true;
                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    set.Add((cx, cy));
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS || seen[ny, nx] || !LandTile(worldMap[ny, nx])) continue;
                            seen[ny, nx] = true; q.Enqueue((nx, ny));
                        }
                }
                // The region of the piece is its most common region.
                string region = set.GroupBy(t => RegionAt(t.Item1, t.Item2)).OrderByDescending(g => g.Count()).First().Key;
                pieces.Add((region, set.Count, set));
            }
        return pieces.OrderByDescending(p => p.Item2).ToList();
    }

    // Pairs of land pieces joined by wadeable water (shallows, not bridges or jetties): a way across without a bridge.
    List<string> WadeLinks(List<(string region, int tiles, HashSet<(int, int)> set)> pieces)
    {
        var owner = new int[ROWS, COLS];
        for (int i = 0; i < pieces.Count; i++) foreach (var (x, y) in pieces[i].set) owner[y, x] = i + 1;
        var links = new HashSet<string>();
        // Grow every piece through shallow water; where two growths meet, they're linked.
        var from = (int[,])owner.Clone();
        var q = new Queue<(int, int)>();
        for (int y = 0; y < ROWS; y++) for (int x = 0; x < COLS; x++) if (from[y, x] > 0) q.Enqueue((x, y));
        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS) continue;
                char c = worldMap[ny, nx];
                bool wade = c is 'w' or 'T' or 'r' or 'I' or 'x' || LandTile(c);
                if (!wade) continue;
                if (from[ny, nx] == 0) { from[ny, nx] = from[cy, cx]; q.Enqueue((nx, ny)); }
                else if (from[ny, nx] != from[cy, cx])
                {
                    var a = pieces[from[ny, nx] - 1].region; var b = pieces[from[cy, cx] - 1].region;
                    if (a != b && pieces[from[ny, nx] - 1].tiles > 6 && pieces[from[cy, cx] - 1].tiles > 6)
                        links.Add(string.CompareOrdinal(a, b) < 0 ? $"{a} ~ {b}" : $"{b} ~ {a}");
                }
            }
        }
        return links.OrderBy(s => s).ToList();
    }

    IEnumerable<int> IslandScript()
    {
        Note("Islands: sizes, wading routes, spots and plants");
        state = new State { created = true, look = new Look { name = "Surveyor" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        yield return 3;
        bool tide = state.flags.tideOut, dock = state.flags.dockFixed;
        state.flags.tideOut = state.flags.dockFixed = true; BuildMap();

        var pieces = LandPieces();
        var areas = pieces.Where(p => p.tiles > 40).GroupBy(p => p.region).ToDictionary(g => g.Key, g => g.Sum(p => p.tiles));
        Note("land by island: " + string.Join(", ", areas.OrderBy(a => a.Key).Select(a => $"{a.Key} {a.Value}")));
        foreach (var (region, before) in LandBefore)
        {
            if (before == 0) continue;
            int now = areas.GetValueOrDefault(region);
            float want = region == "saltmere" ? 1.12f : 1.15f;
            Check($"{region} has grown ({before} -> {now} tiles, {now / (float)before:0.00}x)", now >= before * want);
        }
        var links = WadeLinks(pieces);
        Note("wading links: " + (links.Count == 0 ? "none" : string.Join("; ", links)));
        var allowed = new HashSet<string>(WadeAllowed);
        var extra = links.Where(l => !allowed.Contains(l)).ToList();
        Check($"no new wading route between islands ({(extra.Count == 0 ? "none" : string.Join("; ", extra))})", extra.Count == 0);

        // Every outdoor fishing spot can be fished from land somewhere near it.
        foreach (var s in Data.Spots.Where(s => s.Scene == "world"))
        {
            bool ok = false;
            for (int ty = (int)(s.Y - s.R) / T - 1; ty <= (int)(s.Y + s.R) / T + 1 && !ok; ty++)
                for (int tx = (int)(s.X - s.R) / T - 1; tx <= (int)(s.X + s.R) / T + 1 && !ok; tx++)
                {
                    float cx = tx * T + 5, cy = ty * T + 7;
                    ok = Dist(cx, cy, s.X, s.Y) < s.R - 2 && CanStand(cx, cy);
                }
            char at = worldMap[(int)(s.Y / T), (int)(s.X / T)];
            Check($"{s.Label} can be fished from land, and lies on water ({at})", ok && LandTile(at) == false);
        }
        File.WriteAllLines(Path.Combine(testDir, "map.txt"), Enumerable.Range(0, ROWS).Select(y => $"{y,2} " + new string(Enumerable.Range(0, COLS).Select(x => worldMap[y, x]).ToArray())));
        state.flags.tideOut = tide; state.flags.dockFixed = dock; BuildMap();

        // Saltmere's story tiles are where they always were.
        Check("Saltmere's tide pool, dock and hut ground are unchanged",
            Tide.All(t => worldMap[t.y, t.x] is 'T' or 'p') && Enumerable.Range(25, 5).All(x => worldMap[9, x] is 'd' or 'x')
            && CanStand(TomasHomeX, TomasHomeY + 12) && CanStand(PipX, PipY + 14));

        // The whole world, two pixels a tile.
        var img = Raylib.GenImageColor(COLS * 2, ROWS * 2, Color.Black);
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                char c = worldMap[y, x];
                var col = c switch
                {
                    '~' => Pal.C("#1f5580"), 'w' or 'T' or 'r' or 'I' => Pal.C("#3d97b8"), 'd' or 'b' or 'x' => Pal.C("#8a6239"),
                    't' or 'h' or 'f' => Pal.C("#1d5a38"), 'c' => Pal.C("#3d6e2f"), 'y' => Pal.C("#b8302a"), 'R' => Pal.C("#6d7478"),
                    'l' or 'o' or 'm' or 'k' => Pal.C("#2a9d8f"), 'i' => Pal.C("#bfe3f0"),
                    _ => GroundTones.TryGetValue(c, out var tn) ? tn[1] : Pal.C("#e8cf96")
                };
                Raylib.ImageDrawRectangle(ref img, x * 2, y * 2, 2, 2, col);
            }
        foreach (var s in Data.Spots.Where(s => s.Scene == "world")) Raylib.ImageDrawRectangle(ref img, (int)(s.X / T) * 2, (int)(s.Y / T) * 2, 2, 2, Pal.C("#ffd23f"));
        Raylib.ImageResizeNN(ref img, COLS * 4, ROWS * 4);
        Raylib.ExportImage(img, Path.Combine(testDir, "islands.png"));
        Raylib.UnloadImage(img);
        yield return 1;
    }

    // Wading routes that were always there (filled in from the first run of this script).
    static readonly string[] WadeAllowed = { "marilaya ~ marilaya:north", "marilaya ~ marilaya:salomague" };
}
#endif
