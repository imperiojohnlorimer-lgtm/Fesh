using Raylib_cs;

namespace Fesh;

// An ore rock on the current cave floor. Kind is one of Game.Ores.
sealed class Node { public string Kind; public int X, Y; public bool Mined; }
sealed class Monster { public string Kind; public float X, Y, Vx, Vy, Kx, Ky, Hp, Timer, Hurt; public bool Flip, Dead; }
sealed record MonsterKind(string Name, int Hp, int Damage, float Speed, int MinFloor, string Drop, float DropChance, bool Flies, string Color);
sealed record OreKind(string Kind, string Item, int Tier, string Color, int MinFloor, int MaxFloor);

// Frostfang Caverns: floors 1 to 11 are freshly generated every time you climb down to them, and floor 12 is the
// Ancient Floor, a flooded ruin that always looks the same and is home to the Ancient coelacanth.
partial class Game
{
    const int CaveW = 60, CaveH = 40, AncientFloor = 12;
    int caveFloor = 1;
    char[,] caveMap;
    Pix caveBase;
    readonly List<Node> nodes = new();
    readonly List<Monster> monsters = new();
    (int x, int y) ropeTile, holeTile = (-1, -1);
    (float x, float y)? poolPos;
    const float AncientPoolX = 305, AncientPoolY = 165;
    float iframes, hurtFlash, lastHitT = -10;

    static readonly Dictionary<string, MonsterKind> MonsterKinds = new()
    {
        ["slime"] = new("cave slime", 5, 8, 26, 1, "slime_gel", 0.7f, false, "#5fd06b"),
        ["bat"] = new("cave bat", 3, 6, 42, 2, "bat_wing", 0.5f, true, "#7a5a8e"),
        ["crab"] = new("rock crab", 12, 12, 18, 5, "crab_shell", 0.6f, false, "#8a8f93"),
        ["shade"] = new("shade", 16, 16, 34, 9, "shadow_essence", 0.55f, true, "#9b6be0")
    };

    // Each ore turns up on a range of floors and needs a pickaxe of at least its tier.
    static readonly OreKind[] Ores =
    {
        new("copper", "copper_ore", 1, "#d9823f", 1, 6),
        new("iron", "iron_ore", 2, "#e8e2d6", 3, 10),
        new("gold", "gold_ore", 3, "#f3c25b", 6, 12),
        new("crystal", "crystal", 4, "#9fe8ff", 9, 12),
        new("abyssite", "abyssite", 5, "#9b6be0", 12, 12)
    };
    static OreKind Ore(string kind) => Ores.First(o => o.Kind == kind);

    bool OnAncientFloor => scene == "cave" && caveFloor == AncientFloor;

    /* ---------- Getting in and out ---------- */
    void EnterCaveFloor(int floor)
    {
        Sfx.Play("door");
        FadeThrough(() =>
        {
            caveFloor = floor;
            state.caveDeepest = Math.Max(state.caveDeepest, floor);
            LoadScene("cave");
            player.X = ropeTile.x * T + 5;
            player.Y = (ropeTile.y + 1) * T + 8;
            player.Face = "up";
            Save();
        }, () =>
        {
            if (floor == AncientFloor)
                Toast("The Ancient Floor. Old stone, glowing runes, and something huge moving in the black water.", 5);
            else
                Toast($"Floor {floor}." + (floor == 1 && !state.Hinted("caveFloors")
                    ? " Find the hole to go deeper; the ladder takes you back up. Each floor is different every time." : "")
                    + (floor is 5 or 10 ? " The lift at the entrance can bring you straight here from now on." : ""), 4.5f);
            state.hinted["caveFloors"] = true;
        });
    }

    void EnterCaveFromMouth()
    {
        if (state.caveDeepest >= 5)
        {
            Sfx.Play("ui");
            panel = "lift";
            mode = "panel";
            SetPrompt("");
        }
        else EnterCaveFloor(1);
    }

    void LeaveCave() => GoTo("world", MouthDoorX, MouthDoorY + 2, "down");

    /* ---------- Floor generation ---------- */
    (string floor, string wall, string face, string speck, string water) CaveTheme() =>
        caveFloor == AncientFloor ? ("#2e4a4e", "#142226", "#234044", "#3c6266", "#12404a")
        : caveFloor >= 9 ? ("#463c52", "#1e1a28", "#342c44", "#574b66", "#1c2a48")
        : caveFloor >= 5 ? ("#4a5058", "#20242a", "#3a4250", "#5c636c", "#173650")
        : ("#544840", "#2a2320", "#4a3d35", "#6a5d52", "#16364f");

    void BuildCave()
    {
        bool ancient = caveFloor == AncientFloor;
        var r = ancient ? new Random(777) : new Random(rng.Next());
        caveMap = new char[CaveH, CaveW];
        for (int y = 0; y < CaveH; y++)
            for (int x = 0; x < CaveW; x++) caveMap[y, x] = 'W';
        void Carve(int x, int y, int size = 2)
        {
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    caveMap[Math.Clamp(y + dy, 1, CaveH - 2), Math.Clamp(x + dx, 1, CaveW - 2)] = 'F';
        }
        int sx, sy;
        nodes.Clear();
        monsters.Clear();
        poolPos = null;
        holeTile = (-1, -1);

        if (ancient)
        {
            // A great cavern around a lake, ringed with old pillars, with a passage down from the ladder.
            sx = CaveW / 2; sy = CaveH - 3;
            for (int y = 1; y < CaveH - 1; y++)
                for (int x = 1; x < CaveW - 1; x++)
                    if (InBlob(x, y, 30.5, 17.5, 23, 13, 13)) caveMap[y, x] = 'F';
            for (int y = 27; y < CaveH - 1; y++) Carve(sx - 1, y, 3);
            for (int y = 1; y < CaveH - 1; y++)
                for (int x = 1; x < CaveW - 1; x++)
                    if (InBlob(x, y, AncientPoolX / T, AncientPoolY / T, 6, 3.5, 17)) caveMap[y, x] = 'P';
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI / 5;
                int px = (int)Math.Round(AncientPoolX / T - 0.5 + Math.Cos(a) * 11), py = (int)Math.Round(AncientPoolY / T - 0.5 + Math.Sin(a) * 7);
                if (caveMap[py, px] == 'F') caveMap[py, px] = 'Q';
            }
            for (int y = 1; y < CaveH - 1; y++)
                for (int x = 1; x < CaveW - 1; x++)
                    if (caveMap[y, x] == 'F' && Pix.Hash(x, y, 31) < 0.045) caveMap[y, x] = 'G';
            poolPos = (AncientPoolX, AncientPoolY);
        }
        else
        {
            // Random walks out from the ladder, with the odd chamber, then smoothed.
            sx = r.Next(10, CaveW - 10); sy = CaveH - 4;
            var carved = new List<(int x, int y)> { (sx, sy) };
            for (int walk = 0; walk < 12; walk++)
            {
                var (x, y) = walk == 0 ? (sx, sy) : carved[r.Next(carved.Count)];
                int dir = r.Next(4);
                for (int s = 0; s < 360; s++)
                {
                    Carve(x, y);
                    carved.Add((x, y));
                    if (r.NextDouble() < 0.015) for (int cy = -2; cy <= 2; cy++) Carve(x - 2, y + cy, 5);
                    if (r.NextDouble() < 0.35) dir = r.NextDouble() < 0.3 ? 0 : r.Next(4);
                    x += dir == 2 ? -1 : dir == 3 ? 1 : 0;
                    y += dir == 0 ? -1 : dir == 1 ? 1 : 0;
                    x = Math.Clamp(x, 1, CaveW - 3); y = Math.Clamp(y, 1, CaveH - 3);
                }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                var copy = (char[,])caveMap.Clone();
                for (int y = 1; y < CaveH - 1; y++)
                    for (int x = 1; x < CaveW - 1; x++)
                    {
                        if (copy[y, x] != 'W') continue;
                        int open = 0;
                        for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) if (copy[y + oy, x + ox] == 'F') open++;
                        if (open >= 6) caveMap[y, x] = 'F';
                    }
            }
        }

        // Distances from the ladder decide where the hole, ore and monsters go.
        ropeTile = (sx, sy);
        var dist = CaveDistances(sx, sy);
        if (!ancient)
        {
            bool Open4(int x, int y) => caveMap[y - 1, x] == 'F' && caveMap[y + 1, x] == 'F' && caveMap[y, x - 1] == 'F' && caveMap[y, x + 1] == 'F';
            var far = dist.Where(kv => kv.Key.Item2 > 1 && kv.Key.Item2 < CaveH - 2 && kv.Key.Item1 > 1 && kv.Key.Item1 < CaveW - 2 && Open4(kv.Key.Item1, kv.Key.Item2))
                .OrderByDescending(kv => kv.Value).Take(20).ToList();
            var hole = far[r.Next(far.Count)].Key;
            holeTile = hole;
            caveMap[hole.Item2, hole.Item1] = 'H';

            // Sometimes an underground pool, as long as it doesn't cut the way to the hole.
            if (r.NextDouble() < 0.6)
            {
                var spots = dist.Where(kv => kv.Value >= 8 && kv.Key.Item1 > 4 && kv.Key.Item1 < CaveW - 5 && kv.Key.Item2 > 3 && kv.Key.Item2 < CaveH - 4
                    && Math.Abs(kv.Key.Item1 - hole.Item1) + Math.Abs(kv.Key.Item2 - hole.Item2) > 8
                    && Enumerable.Range(-3, 7).All(ox => Enumerable.Range(-2, 5).All(oy => caveMap[kv.Key.Item2 + oy, kv.Key.Item1 + ox] == 'F')))
                    .Select(kv => kv.Key).ToList();
                if (spots.Count > 0)
                {
                    var (px, py) = spots[r.Next(spots.Count)];
                    var undo = new List<(int, int)>();
                    for (int y = py - 3; y <= py + 3; y++)
                        for (int x = px - 4; x <= px + 4; x++)
                            if (caveMap[y, x] == 'F' && InBlob(x, y, px + 0.5, py + 0.5, 3.2, 2, r.Next(100))) { caveMap[y, x] = 'P'; undo.Add((x, y)); }
                    if (CaveDistances(sx, sy).ContainsKey(NextToHole())) poolPos = (px * T + 5, py * T + 5);
                    else foreach (var (x, y) in undo) caveMap[y, x] = 'F';
                }
            }
            dist = CaveDistances(sx, sy);
        }

        // Ore in the alcoves, weighted towards the commoner kinds for this depth.
        var oreKinds = Ores.Where(o => caveFloor >= o.MinFloor && caveFloor <= o.MaxFloor).ToList();
        var alcoves = dist.Keys.Where(p =>
        {
            var (x, y) = p;
            if (caveMap[y, x] is not ('F' or 'G')) return false;
            int walls = (caveMap[y - 1, x] == 'W' ? 1 : 0) + (caveMap[y + 1, x] == 'W' ? 1 : 0) + (caveMap[y, x - 1] == 'W' ? 1 : 0) + (caveMap[y, x + 1] == 'W' ? 1 : 0);
            bool clear = Math.Max(Math.Abs(x - sx), Math.Abs(y - sy)) > 3 && (holeTile.x < 0 || Math.Max(Math.Abs(x - holeTile.x), Math.Abs(y - holeTile.y)) > 2)
                && (poolPos is not (float ppx, float ppy) || Dist(x * T + 5, y * T + 5, ppx, ppy) > 46);
            return walls >= 2 && dist[p] > 5 && clear;
        }).OrderBy(_ => r.Next()).ToList();
        int want = ancient ? 10 : 16 + caveFloor;
        foreach (var (x, y) in alcoves)
        {
            if (nodes.Count >= want) break;
            if (nodes.Any(n => Math.Abs(n.X - x) <= 1 && Math.Abs(n.Y - y) <= 1)) continue;
            string kind;
            if (ancient) kind = nodes.Count < 6 ? "abyssite" : "crystal";
            else
            {
                int total = oreKinds.Select((o, i) => oreKinds.Count - i + 1).Sum(), roll = r.Next(total);
                kind = oreKinds[^1].Kind;
                for (int i = 0; i < oreKinds.Count; i++)
                {
                    int wgt = oreKinds.Count - i + 1;
                    if (roll < wgt) { kind = oreKinds[i].Kind; break; }
                    roll -= wgt;
                }
            }
            nodes.Add(new Node { Kind = kind, X = x, Y = y });
        }

        // Monsters, more and nastier the deeper you go, never right by the ladder.
        var kinds = ancient ? new List<string> { "shade", "shade", "shade", "crab", "crab" }
            : MonsterKinds.Where(kv => caveFloor >= kv.Value.MinFloor).Select(kv => kv.Key).ToList();
        int count = ancient ? 5 : 3 + caveFloor / 2;
        var lairs = dist.Where(kv => kv.Value > 14 && caveMap[kv.Key.Item2, kv.Key.Item1] is 'F' or 'G').Select(kv => kv.Key).OrderBy(_ => r.Next()).ToList();
        for (int i = 0; i < count && i < lairs.Count; i++)
        {
            string kind = ancient ? kinds[i] : kinds[r.Next(kinds.Count)];
            var (mx, my) = lairs[i];
            monsters.Add(new Monster { Kind = kind, X = mx * T + 5, Y = my * T + 7, Hp = MonsterKinds[kind].Hp, Timer = (float)r.NextDouble() });
        }

        caveMap[sy, sx] = 'U';
        map = caveMap;
        RenderCaveBase();
    }

    // A walkable tile beside the hole, used to check a pool hasn't blocked the way down.
    (int, int) NextToHole()
    {
        foreach (var (ox, oy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            if (caveMap[holeTile.y + oy, holeTile.x + ox] == 'F') return (holeTile.x + ox, holeTile.y + oy);
        return holeTile;
    }

    Dictionary<(int, int), int> CaveDistances(int sx, int sy)
    {
        var dist = new Dictionary<(int, int), int> { [(sx, sy)] = 0 };
        var queue = new Queue<(int, int)>();
        queue.Enqueue((sx, sy));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= CaveW || ny >= CaveH || caveMap[ny, nx] is not ('F' or 'G' or 'U') || dist.ContainsKey((nx, ny))) continue;
                dist[(nx, ny)] = dist[(x, y)] + 1;
                queue.Enqueue((nx, ny));
            }
        }
        return dist;
    }

    void RenderCaveBase()
    {
        var th = CaveTheme();
        caveBase = new Pix(CaveW * T, CaveH * T);
        basePix = caveBase;
        var g = caveBase;
        for (int y = 0; y < CaveH; y++)
            for (int x = 0; x < CaveW; x++)
            {
                int X = x * T, Y = y * T;
                char t = caveMap[y, x];
                double h1 = Pix.Hash(x, y, 21 + caveFloor);
                if (t == 'W')
                {
                    g.Rect(X, Y, T, T, th.wall);
                    g.Rect(X + (int)(h1 * 7), Y + (int)(Pix.Hash(x, y, 22) * 7), 3, 2, th.face);
                    if (y + 1 < CaveH && caveMap[y + 1, x] != 'W') { g.Rect(X, Y + 6, T, 4, th.face); g.Rect(X, Y + 6, T, 1, th.speck); }
                    continue;
                }
                if (t == 'P')
                {
                    g.Rect(X, Y, T, T, th.water);
                    if (h1 > 0.6) g.Rect(X + 2, Y + 4, 3, 1, "#2a6a8a");
                    continue;
                }
                g.Rect(X, Y, T, T, th.floor);
                g.Rect(X + (int)(h1 * 8), Y + (int)(Pix.Hash(x, y, 23) * 8), 2, 1, th.speck);
                if (h1 > 0.8) g.Rect(X + 3, Y + 6, 1, 1, th.speck);
                if (t == 'G')
                {
                    // Old runes cut into the floor, in three shapes.
                    const string rune = "#5fe8d8";
                    double v = Pix.Hash(x, y, 32);
                    if (v < 0.34)
                    {
                        g.Rect(X + 2, Y + 2, 6, 1, rune); g.Rect(X + 2, Y + 2, 1, 5, rune); g.Rect(X + 5, Y + 4, 3, 1, rune);
                        g.Rect(X + 7, Y + 4, 1, 4, rune); g.Rect(X + 4, Y + 7, 4, 1, rune);
                    }
                    else if (v < 0.67)
                    {
                        g.Rect(X + 4, Y + 1, 2, 1, rune); g.Rect(X + 2, Y + 2, 1, 2, rune); g.Rect(X + 7, Y + 2, 1, 2, rune);
                        g.Rect(X + 4, Y + 4, 2, 2, rune); g.Rect(X + 2, Y + 6, 1, 2, rune); g.Rect(X + 7, Y + 6, 1, 2, rune); g.Rect(X + 4, Y + 8, 2, 1, rune);
                    }
                    else
                    {
                        g.Rect(X + 5, Y + 1, 1, 8, rune); g.Rect(X + 2, Y + 3, 7, 1, rune); g.Rect(X + 3, Y + 6, 5, 1, rune);
                        g.Rect(X + 2, Y + 3, 1, 2, rune); g.Rect(X + 8, Y + 3, 1, 2, rune);
                    }
                }
                if (t == 'H')
                {
                    g.Rect(X + 1, Y + 2, 8, 7, "#050404"); g.Rect(X + 2, Y + 1, 6, 9, "#050404");
                    g.Rect(X + 3, Y + 2, 1, 6, "#6b4a2b"); g.Rect(X + 6, Y + 2, 1, 6, "#6b4a2b");
                    for (int i = 3; i < 8; i += 2) g.Rect(X + 3, Y + i, 4, 1, "#8a6440");
                }
                if (t == 'U')
                {
                    // The ladder back up, reaching into the rock above.
                    g.Rect(X + 2, Y - 10, 1, 20, "#8a6440"); g.Rect(X + 7, Y - 10, 1, 20, "#8a6440");
                    for (int i = -9; i < 10; i += 3) g.Rect(X + 2, Y + i, 6, 1, "#b08458");
                }
            }
    }

    /* ---------- Fishing spots that move with the floor ---------- */
    (float x, float y) SpotPos(Spot s) => s.Scene != "cave" ? (s.X, s.Y) : s.Id == "ancientpool" ? (AncientPoolX, AncientPoolY) : poolPos ?? (-999, -999);

    bool SpotHere(Spot s) => s.Scene == scene && SpotOpen(s.Id)
        && (s.Scene != "cave" || (s.Id == "ancientpool" ? caveFloor == AncientFloor : caveFloor != AncientFloor && poolPos != null));

    /* ---------- Ore ---------- */
    bool NodeMined(Node n) => n.Mined;
    Node NodeAt(int tx, int ty) => scene == "cave" ? nodes.FirstOrDefault(n => n.X == tx && n.Y == ty && !n.Mined) : null;

    static string PickName(int tier) => Items.ById[Items.Pickaxes[Math.Clamp(tier, 1, 5) - 1]].Name.ToLowerInvariant();

    void MineNode(Node n)
    {
        var ore = Ore(n.Kind);
        if (PickTier < ore.Tier)
        {
            Sfx.Play("nope");
            Toast(PickTier == 0 ? "You need a pickaxe to mine ore. Make one at a workbench."
                : $"Too hard for your pickaxe. {Items.ById[ore.Item].Name} needs a {PickName(ore.Tier)} or better.", 3.5f);
            return;
        }
        if (chopTile != (n.X, n.Y)) { chopTile = (n.X, n.Y); chopHits = 0; }
        chopHits++;
        shakeT = 0.25f;
        swingT = 0.25f;
        FaceToward(n.X * T + 5, n.Y * T + 5);
        Sfx.Play("mine");
        Burst(n.X * T + 5, n.Y * T + 3, ore.Color, 5);
        if (chopHits < 3) return;
        int count = ore.Tier >= 4 ? 1 : 2;
        Give(ore.Item, count);
        Give("stone");
        n.Mined = true;
        chopTile = (-1, -1);
        Sfx.Play("pickup");
        Toast($"Mined {Items.Amount(ore.Item, count)} and a stone.");
        Save();
    }

    void DrawNode(Node n, float t)
    {
        int X = n.X * T, Y = n.Y * T;
        if (chopTile == (n.X, n.Y) && shakeT > 0) X += (int)MathF.Round(MathF.Sin(time * 70) * 1.5f);
        var ore = Ore(n.Kind);
        pix.Rect(X + 1, Y + 8, 8, 2, "rgba(0,0,0,0.3)");
        pix.Rect(X + 1, Y + 2, 8, 7, "#5e5550");
        pix.Rect(X + 2, Y + 1, 6, 7, "#766b64");
        pix.Rect(X + 3, Y + 1, 3, 2, "#8f847c");
        foreach (var (ox, oy) in new[] { (3, 4), (6, 3), (5, 6), (2, 6), (7, 6) }) pix.Rect(X + ox, Y + oy, 1, 1, ore.Color);
        if (n.Kind is "crystal" or "abyssite")
        {
            string a = n.Kind == "crystal" ? "#9fe8ff" : "#9b6be0", b = n.Kind == "crystal" ? "#bff4ff" : "#c9a6ff", c = n.Kind == "crystal" ? "#5fc8e8" : "#4f3a7a";
            pix.Rect(X + 4, Y - 3, 2, 6, a); pix.Rect(X + 6, Y - 1, 2, 4, b); pix.Rect(X + 2, Y, 2, 3, c);
            if ((t * 0.8 + n.X * 0.3) % 1 < 0.1) pix.Rect(X + 5, Y - 2, 1, 1, "#ffffff");
        }
        if (n.Kind == "gold" && (t * 0.6 + n.Y * 0.3) % 1 < 0.08) pix.Rect(X + 6, Y + 2, 1, 1, "#ffffff");
    }

    void DrawPillar(int tx, int ty, float t)
    {
        int X = tx * T, Y = ty * T;
        pix.Rect(X, Y + 8, 10, 2, "rgba(0,0,0,0.3)");
        pix.Rect(X + 2, Y - 14, 6, 23, "#4f6a6c");
        pix.Rect(X + 2, Y - 14, 2, 23, "#6a8a8c");
        pix.Rect(X + 1, Y - 16, 8, 3, "#5f7c7e"); pix.Rect(X + 1, Y + 6, 8, 3, "#5f7c7e");
        float glow = 0.5f + 0.5f * MathF.Sin(t * 1.5f + tx);
        pix.Rect(X + 4, Y - 8, 2, 1, Pal.Rgba(95, 232, 216, glow)); pix.Rect(X + 4, Y - 4, 2, 1, Pal.Rgba(95, 232, 216, glow));
    }

    /* ---------- Monsters ---------- */
    static readonly Dictionary<string, string[][]> MonsterFrames = new()
    {
        ["slime"] = new[]
        {
            new[] { "...gggg...", "..gGGGGg..", ".gGwGGwGg.", ".gGkGGkGg.", "gGGGGGGGGg", "gGGGGGGGGg", ".gggggggg." },
            new[] { "..........", "...gggg...", "..gGGGGg..", ".gGwGGwGg.", "ggGkGGkGgg", "gGGGGGGGGg", "gggggggggg" }
        },
        ["bat"] = new[]
        {
            new[] { "P.........P", "PP..ppp..PP", ".PPpPPPpPP.", "..PPrPrPP..", "...PPPPP...", "....P.P...." },
            new[] { "....ppp....", "...pPPPp...", ".PPPrPrPPP.", "PP.PPPPP.PP", "P...P.P...P", "..........." }
        },
        ["crab"] = new[]
        {
            new[] { "..RRRRRRRR..", ".RrrRRRRrrR.", "RRRRRRRRRRRR", "oRRwRRRRwRRo", "oo.RRRRRR.oo", "...o.o.o.o.." },
            new[] { "..RRRRRRRR..", ".RrrRRRRrrR.", "RRRRRRRRRRRR", "oRRwRRRRwRRo", ".oo.RRRR.oo.", "..o.o..o.o.." }
        },
        ["shade"] = new[]
        {
            new[] { "..dddd..", ".dDDDDd.", "dDcDDcDd", "dDDDDDDd", "dDDDDDDd", ".dDDDDd.", ".dDdDDd.", "..d.d.d." },
            new[] { "..dddd..", ".dDDDDd.", "dDcDDcDd", "dDDDDDDd", "dDDDDDDd", ".dDDDDd.", "dDd.dDd.", ".d...d.." }
        }
    };

    static readonly Dictionary<char, string> MonsterColors = new()
    {
        ['g'] = "#3f9a4a", ['G'] = "#5fd06b", ['w'] = "#ffffff", ['k'] = "#10243a",
        ['P'] = "#5b3f6e", ['p'] = "#7a5a8e", ['r'] = "#e04b3a",
        ['R'] = "#7d8288", ['o'] = "#d9823f",
        ['d'] = "#2a2238", ['D'] = "#4a3a6a", ['c'] = "#c9a6ff"
    };

    string MonsterPixel(string kind, char ch) => kind == "crab" && ch == 'r' ? "#9aa0a5" : kind == "crab" && ch == 'w' ? "#1b1b1b" : MonsterColors[ch];

    void DrawMonster(Monster m, float t)
    {
        var frames = MonsterFrames[m.Kind];
        var rows = frames[(int)(t * (m.Kind == "bat" ? 10 : 4) + m.X) % frames.Length];
        int w = rows[0].Length, h = rows.Length;
        bool flies = MonsterKinds[m.Kind].Flies;
        int left = (int)MathF.Round(m.X) - w / 2, top = (int)MathF.Round(m.Y) - h + 1 - (flies ? 6 + (int)MathF.Round(MathF.Sin(t * 5 + m.X) * 1.5f) : 0);
        pix.Rect(m.X - w / 2 + 1, m.Y + 1, w - 2, 1, "rgba(0,0,0,0.3)");
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                char ch = rows[r][c];
                if (ch == '.') continue;
                pix.Rect(left + (m.Flip ? w - 1 - c : c), top + r, 1, 1, m.Hurt > 0 ? "#ffffff" : MonsterPixel(m.Kind, ch));
            }
        var k = MonsterKinds[m.Kind];
        if (m.Hp < k.Hp)
        {
            pix.Rect(m.X - 6, top - 4, 12, 2, "#10243a");
            pix.Rect(m.X - 6, top - 4, (int)MathF.Ceiling(12 * Math.Max(0, m.Hp) / k.Hp), 2, "#e04b3a");
        }
    }

    Monster MonsterInFront()
    {
        if (scene != "cave") return null;
        float fx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0, fy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
        return monsters.Where(m =>
        {
            float dx = m.X - player.X, dy = m.Y - player.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            return !m.Dead && d < 20 && (d < 10 || (dx * fx + dy * fy) / d > 0.2f);
        }).OrderBy(m => Dist(m.X, m.Y, player.X, player.Y)).FirstOrDefault();
    }

    (string name, int damage) Weapon()
    {
        foreach (var (id, dmg) in Items.Weapons) if (Has(id) > 0) return (Items.ById[id].Name.ToLowerInvariant(), dmg);
        return PickTier > 0 ? ("pickaxe", 2) : ("fists", 1);
    }

    void Attack(Monster m)
    {
        if (swingT > 0.12f) return;
        var (_, dmg) = Weapon();
        swingT = 0.3f;
        FaceToward(m.X, m.Y);
        m.Hp -= dmg;
        m.Hurt = 0.2f;
        float dx = m.X - player.X, dy = m.Y - player.Y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        m.Kx = dx / d * 130; m.Ky = dy / d * 130;
        m.Vx = m.Vy = 0; m.Timer = 0.5f;
        Sfx.Play("hit");
        Burst(m.X, m.Y - 4, "#ffffff", 4);
        if (m.Hp > 0) return;
        m.Dead = true;
        var k = MonsterKinds[m.Kind];
        Burst(m.X, m.Y - 3, k.Color, 14);
        string got = "";
        if (rng.NextDouble() < k.DropChance) { Give(k.Drop); got = $" It dropped {Items.Amount(k.Drop, 1)}."; }
        Toast($"You beat the {k.Name}.{got}", 2.4f);
        Save();
    }

    bool MonsterCanBe(Monster m, float x, float y)
    {
        bool flies = MonsterKinds[m.Kind].Flies;
        foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
        {
            char t = TileAt((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T));
            if (!(Walkable(t) || (flies && t is 'P' or 'H'))) return false;
        }
        // The ladder is safe ground: monsters won't follow you onto it.
        return Dist(x, y, ropeTile.x * T + 5, ropeTile.y * T + 5) > 24;
    }

    void UpdateMonsters(float dt)
    {
        iframes = Math.Max(0, iframes - dt);
        hurtFlash = Math.Max(0, hurtFlash - dt);
        if (scene != "cave") return;
        foreach (var m in monsters)
        {
            if (m.Dead) continue;
            var k = MonsterKinds[m.Kind];
            m.Hurt = Math.Max(0, m.Hurt - dt);
            float dx = player.X - m.X, dy = player.Y - 3 - m.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            bool chase = d < (k.Flies ? 95 : 75) && Dist(player.X, player.Y, ropeTile.x * T + 5, ropeTile.y * T + 5) > 24;
            m.Timer -= dt;
            if (m.Kind == "slime")
            {
                // Slimes hop: a quick burst, then a pause.
                if (m.Timer <= 0)
                {
                    m.Timer = 1.0f;
                    float ang = chase ? MathF.Atan2(dy, dx) : Rand(0, MathF.Tau);
                    m.Vx = MathF.Cos(ang) * k.Speed * 2.2f; m.Vy = MathF.Sin(ang) * k.Speed * 2.2f;
                }
                else if (m.Timer < 0.6f) m.Vx = m.Vy = 0;
            }
            else if (m.Timer <= 0)
            {
                m.Timer = m.Kind == "bat" ? Rand(0.2f, 0.5f) : Rand(0.6f, 1.4f);
                float ang = chase ? MathF.Atan2(dy, dx) + (m.Kind == "bat" ? Rand(-0.9f, 0.9f) : Rand(-0.3f, 0.3f)) : Rand(0, MathF.Tau);
                float sp = chase ? k.Speed : (rng.NextDouble() < 0.4 ? 0 : k.Speed * 0.4f);
                m.Vx = MathF.Cos(ang) * sp; m.Vy = MathF.Sin(ang) * sp;
            }
            float mx = (m.Vx + m.Kx) * dt, my = (m.Vy + m.Ky) * dt;
            float fade = MathF.Pow(0.02f, dt);
            m.Kx *= fade; m.Ky *= fade;
            if (MonsterCanBe(m, m.X + mx, m.Y)) m.X += mx; else { m.Vx = -m.Vx; m.Kx = 0; }
            if (MonsterCanBe(m, m.X, m.Y + my)) m.Y += my; else { m.Vy = -m.Vy; m.Ky = 0; }
            if (MathF.Abs(m.Vx) > 1) m.Flip = m.Vx < 0;
            if (d < 8 && iframes <= 0 && m.Hurt <= 0 && mode != "fade") HurtPlayer(k.Damage, m);
        }
        monsters.RemoveAll(m => m.Dead);
    }

    /* ---------- Health ---------- */
    void HurtPlayer(int dmg, Monster m) => HurtPlayer(dmg, m.X, m.Y);

    // A blow from something at (fromX, fromY) knocks you back a few steps of 3 pixels.
    void HurtPlayer(int dmg, float fromX, float fromY, int push = 4)
    {
        float taken = dmg * (Has("shell_armor") > 0 ? 0.65f : 1f);
        state.hp = Math.Max(0, state.hp - taken);
        iframes = 0.9f;
        hurtFlash = 0.35f;
        lastHitT = time;
        Sfx.Play("hurt");
        float dx = player.X - fromX, dy = player.Y - fromY, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        for (int i = 0; i < push; i++)
        {
            float nx = player.X + dx / d * 3, ny = player.Y + dy / d * 3;
            if (CanStand(nx, ny, Wading)) { player.X = nx; player.Y = ny; }
        }
        if (FishingModes.Contains(mode))
        {
            fish = null; reel = null; pointerHold = false;
            mode = "play";
            Toast("A monster knocked your line loose!");
        }
        if (state.hp <= 0) Faint();
    }

    // Blacking out in the caverns: you wake up outside, a bit poorer. Knocked flat at the Starwell, you come round on the
    // atoll's jetty, and Tidemane has gone back under.
    void Faint()
    {
        fish = null; reel = null; pointerHold = false;
        panel = null;
        mode = "play";
        if (boss != null)
        {
            boss = null;
            bolts.Clear();
            FadeThrough(() =>
            {
                player.X = AtollJettyX + 8; player.Y = AtollJettyY + 1; player.Face = "right";
                state.hp = 35;
                Save();
            }, () => Toast("Everything went dark... You came round on the atoll's jetty. Whatever lives in the Starwell has gone back under. Eat and rest, then try again another night.", 6));
            return;
        }
        int lost = state.coins / 10;
        FadeThrough(() =>
        {
            LoadScene("world");
            player.X = MouthDoorX; player.Y = MouthDoorY + 2; player.Face = "down";
            state.hp = 35;
            state.coins -= lost;
            Save();
        }, () => Toast($"Everything went dark... You woke up outside the caverns{(lost > 0 ? $", {lost} coins lighter" : "")}. Eat and rest to recover.", 5));
    }

    // Health comes back slowly while you're fed and out of danger; starving wears it down (but never below 10).
    void TickHealth(float dt)
    {
        if (state.food > 30 && time - lastHitT > 4 && state.hp < 100) state.hp = Math.Min(100, state.hp + dt * 0.5f);
        if (Starving && state.hp > 10) state.hp = Math.Max(10, state.hp - dt / 8f);
    }

    /* ---------- Drawing a floor ---------- */
    void RenderCave(float t)
    {
        var th = CaveTheme();
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
                if (map[y, x] == 'P' && (t * 0.3 + Pix.Hash(x, y, 7)) % 1 < 0.15) pix.Rect(x * T + 3, y * T + 5, 2, 1, "#4f8fb8");
        DrawSpots(t);
        var list = new List<(float y, Action draw)>();
        foreach (var n in nodes) if (!n.Mined) list.Add((n.Y * T + 9, () => DrawNode(n, t)));
        foreach (var m in monsters) list.Add((m.Y, () => DrawMonster(m, t)));
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
                if (map[y, x] == 'Q') { int px = x, py = y; list.Add((py * T + 9, () => DrawPillar(px, py, t))); }
        list.Add((player.Y, DrawPlayer));
        foreach (var o in list.OrderBy(o => o.y)) o.draw();
        DrawFishing(t);
        DrawParticles();

        // Always dark underground: your own light, daylight down the ladder, and anything that glows.
        bool ancient = caveFloor == AncientFloor;
        Array.Fill(dark, ancient ? 0.62f : 0.72f);
        LightHole(player.X, player.Y - 6, Wears("headlamp") ? 100 : 64, 0.95f);
        LightHole(ropeTile.x * T + 5, ropeTile.y * T, 46, 0.9f);
        if (fish != null) LightHole(fish.Bx, fish.By, 14, 0.5f);
        foreach (var n in nodes)
            if (!n.Mined && n.Kind is "crystal" or "abyssite") LightHole(n.X * T + 5, n.Y * T, 18, 0.7f);
        if (ancient)
        {
            for (int y = vy0; y <= vy1; y++)
                for (int x = vx0; x <= vx1; x++)
                    if (map[y, x] == 'G') LightHole(x * T + 5, y * T + 5, 12, 0.55f);
            LightHole(AncientPoolX, AncientPoolY, 40, 0.35f + 0.1f * MathF.Sin(t));
        }
        ApplyDark(new Color(5, 4, 8, 255));
        foreach (var n in nodes)
            if (!n.Mined && n.Kind is "crystal" or "abyssite")
                pix.Glow(n.X * T + 5, n.Y * T, 12, n.Kind == "crystal" ? Pal.Rgba(127, 232, 255, 0.25f) : Pal.Rgba(155, 107, 224, 0.28f));
        foreach (var m in monsters) if (m.Kind == "shade") pix.Glow(m.X, m.Y - 10, 10, Pal.Rgba(155, 107, 224, 0.2f));
        if (ancient)
        {
            float a = 0.25f + 0.15f * MathF.Sin(t * 0.7f);
            pix.Glow(AncientPoolX + MathF.Sin(t * 0.4f) * 20, AncientPoolY + MathF.Cos(t * 0.3f) * 8, 14, Pal.Rgba(255, 215, 106, a));
        }
        if (hurtFlash > 0) pix.Fill(0, 0, W, H, Pal.Rgba(224, 75, 58, hurtFlash * 0.6f));
    }
}
