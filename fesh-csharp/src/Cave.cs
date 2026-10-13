using Raylib_cs;

namespace Fesh;

// An ore rock on the current cave floor. Kind is one of Game.Ores.
sealed class Node { public string Kind; public int X, Y; public bool Mined; }
// Phase: "" moving about, "windup" (it has picked its line and shows it), "dash" (the lunge itself), "recover" (open to a
// blow) or "stun" (a crab that charged into a wall). Max is its hit points as spawned, for the health bar (0: the kind's own).
sealed class Monster
{
    public string Kind; public float X, Y, Vx, Vy, Kx, Ky, Hp, Timer, Hurt, Max;
    public bool Flip, Dead, Hit;
    public string Phase = ""; public float PhaseT, AimX, AimY, Touch, Stagger, Cool;
}
// How a monster attacks: it starts a windup within Range, rushes along the line it locked at the start of it, and is
// open for Recover afterwards. Touch is what merely bumping into it costs; Radius is how near its dash must pass.
sealed record AttackKind(float Range, float Windup, float DashSpeed, float DashTime, float Recover, int Touch, float Radius);
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
        // Damage is what a lunge costs (before armour); Speed is how fast it closes in (you walk at 52). Deeper floors
        // grow tougher (MonsterHp), and every kind now rushes you in a short, shown lunge (MonsterAttacks).
        ["slime"] = new("cave slime", 8, 12, 30, 1, "slime_gel", 0.7f, false, "#5fd06b"),
        ["bat"] = new("cave bat", 6, 10, 62, 2, "bat_wing", 0.5f, true, "#7a5a8e"),
        ["crab"] = new("rock crab", 18, 18, 38, 5, "crab_shell", 0.6f, false, "#8a8f93"),
        ["shade"] = new("shade", 24, 22, 54, 9, "shadow_essence", 0.55f, true, "#9b6be0")
    };
    static readonly Dictionary<string, AttackKind> MonsterAttacks = new()
    {
        ["slime"] = new(40, 0.45f, 105, 0.32f, 0.85f, 4, 9),
        ["bat"] = new(66, 0.45f, 140, 0.35f, 0.75f, 4, 9),
        ["crab"] = new(44, 0.65f, 115, 0.42f, 1.10f, 7, 11),
        ["shade"] = new(66, 0.70f, 155, 0.35f, 1.00f, 6, 9)
    };
    // A monster's hit points on a floor: tougher the deeper below the floor it first appears (up to 1.7 times).
    int MonsterHp(string kind) => (int)MathF.Ceiling(MonsterKinds[kind].Hp * Math.Min(1.7f, 1 + 0.07f * Math.Max(0, caveFloor - MonsterKinds[kind].MinFloor)));
    static float MonsterMax(Monster m) => m.Max > 0 ? m.Max : MonsterKinds[m.Kind].Hp;

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

        // Monsters, more and nastier the deeper you go (3 on the first floors, 9 on the eleventh), never right by the ladder
        // and never in a heap: each lair is a few tiles from the last.
        var kinds = ancient ? new List<string> { "shade", "shade", "shade", "crab", "crab" } : null;
        int count = ancient ? 5 : Math.Min(9, 3 + (int)MathF.Floor(0.6f * (caveFloor - 1)));
        var lairs = dist.Where(kv => kv.Value > 14 && caveMap[kv.Key.Item2, kv.Key.Item1] is 'F' or 'G').Select(kv => kv.Key).OrderBy(_ => r.Next()).ToList();
        var placed = new List<(int, int)>();
        foreach (var (mx, my) in lairs)
        {
            if (placed.Count >= count) break;
            if (!ancient && placed.Any(p => Math.Abs(p.Item1 - mx) + Math.Abs(p.Item2 - my) < 5)) continue;
            string kind = ancient ? kinds[placed.Count] : PickMonster(r);
            placed.Add((mx, my));
            monsters.Add(new Monster { Kind = kind, X = mx * T + 5, Y = my * T + 7, Hp = MonsterHp(kind), Max = MonsterHp(kind), Timer = (float)r.NextDouble() });
        }
        if (!ancient && placed.Count < Math.Min(count, 3))
            foreach (var (mx, my) in lairs.Where(l => !placed.Contains(l)).Take(3 - placed.Count))
            {
                string kind = PickMonster(r);
                monsters.Add(new Monster { Kind = kind, X = mx * T + 5, Y = my * T + 7, Hp = MonsterHp(kind), Max = MonsterHp(kind), Timer = (float)r.NextDouble() });
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
    (float x, float y) SpotPos(Spot s) => s.Scene == "sea" ? seaSpot : s.Scene != "cave" ? (s.X, s.Y) : s.Id == "ancientpool" ? (AncientPoolX, AncientPoolY) : poolPos ?? (-999, -999);

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
        Swing("pick", 0.25f);
        FaceToward(n.X * T + 5, n.Y * T + 5);
        Sfx.Play("mine");
        Burst(n.X * T + 5, n.Y * T + 3, ore.Color, 5);
        if (chopHits < 3) return;
        int count = ore.Tier >= 4 ? 1 : 2;
        Give(ore.Item, count);
        Give("stone");
        Learned("ore:" + n.Kind);
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
        // The warning before a lunge: the line it will take (fixed once the windup is half over), a shake, a red flush, and a mark.
        bool winding = m.Phase == "windup";
        if (winding)
        {
            var atk = MonsterAttacks[m.Kind];
            float k0 = m.PhaseT / atk.Windup, pulse = 0.35f + 0.35f * MathF.Abs(MathF.Sin(t * (10 + 14 * k0)));
            float reach = atk.DashSpeed * atk.DashTime, y0 = m.Y - (flies ? 6 : 3);
            for (float s = 6; s < reach + 4; s += 3)
                pix.Rect(m.X + m.AimX * s, y0 + m.AimY * s, 1, 1, Pal.Rgba(255, 106, 90, pulse * (1 - 0.4f * s / reach)));
            left += (int)MathF.Round(MathF.Sin(t * 60) * (0.4f + k0));
        }
        else if (m.Phase == "dash") pix.Rect(m.X - m.AimX * 5, m.Y - 3 - m.AimY * 5, 2, 1, Pal.Rgba(255, 255, 255, 0.35f));
        bool stunned = m.Phase == "stun";
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                char ch = rows[r][c];
                if (ch == '.') continue;
                string col = m.Hurt > 0 ? "#ffffff" : winding && (int)(t * 16) % 2 == 0 ? "#ff8a7a" : MonsterPixel(m.Kind, ch);
                pix.Rect(left + (m.Flip ? w - 1 - c : c), top + r, 1, 1, col);
            }
        if (winding) { pix.Rect(m.X - 1, top - 10, 3, 5, "#10243a"); pix.Rect(m.X, top - 9, 1, 3, "#ff6a5a"); pix.Rect(m.X, top - 5, 1, 1, "#ff6a5a"); }
        if (stunned)
            for (int i = 0; i < 3; i++)
            {
                float a = t * 5 + i * MathF.Tau / 3;
                pix.Rect(m.X + MathF.Cos(a) * 6, top - 3 + MathF.Sin(a) * 1.5f, 1, 1, "#ffd76a");
            }
        var k = MonsterKinds[m.Kind];
        float max = MonsterMax(m);
        if (m.Hp < max)
        {
            pix.Rect(m.X - 6, top - 4, 12, 2, "#10243a");
            pix.Rect(m.X - 6, top - 4, (int)MathF.Ceiling(12 * Math.Max(0, m.Hp) / max), 2, "#e04b3a");
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
        Swing(WeaponTool(), 0.3f);
        FaceToward(m.X, m.Y);
        m.Hp -= dmg;
        m.Hurt = 0.2f;
        float dx = m.X - player.X, dy = m.Y - player.Y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        m.Kx = dx / d * 130; m.Ky = dy / d * 130;
        m.Vx = m.Vy = 0; m.Timer = 0.5f;
        // A real blade interrupts a windup (once in a while: it can't be held off for ever); fists and a pickaxe only scuff it.
        if (m.Phase == "windup" && m.Stagger <= 0 && Items.Weapons.Any(w => Has(w.id) > 0))
        {
            m.Phase = "recover"; m.PhaseT = MathF.Max(0, MonsterAttacks[m.Kind].Recover - 0.45f); m.Stagger = 1.2f;
            Floater("Interrupted!", m.X, m.Y - 16, "#ffd76a");
        }
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

    string PickMonster(Random r)
    {
        (string kind, int w)[] table = caveFloor switch
        {
            <= 1 => new[] { ("slime", 1) },
            <= 4 => new[] { ("slime", 60), ("bat", 40) },
            <= 8 => new[] { ("slime", 35), ("bat", 30), ("crab", 35) },
            _ => new[] { ("slime", 20), ("bat", 25), ("crab", 30), ("shade", 25) }
        };
        int roll = r.Next(table.Sum(t => t.w));
        foreach (var (kind, w) in table) { if (roll < w) return kind; roll -= w; }
        return table[0].kind;
    }

    // Moves a monster in steps of at most 3 px so a fast lunge can't skip over a wall. Returns which axes were blocked.
    (bool x, bool y) MoveMonster(Monster m, float mx, float my, bool stopOnBlock = false)
    {
        int n = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(mx), MathF.Abs(my)) / 3f));
        float sx = mx / n, sy = my / n;
        bool bx = false, by = false;
        for (int i = 0; i < n; i++)
        {
            if (!bx) { if (MonsterCanBe(m, m.X + sx, m.Y)) m.X += sx; else bx = true; }
            // A lunge ends the moment either axis is blocked, so it never slides off the line it showed.
            if (stopOnBlock && bx) break;
            if (!by) { if (MonsterCanBe(m, m.X, m.Y + sy)) m.Y += sy; else by = true; }
            if (stopOnBlock && by) break;
            if (bx && by) break;
        }
        return (bx, by);
    }

    // Each monster wanders, then closes in when it has seen you, and within reach of you stops, shows the line it has picked
    // (its windup: you can sidestep it), rushes along it, and is open to a blow afterwards. Only two attack at once.
    void UpdateMonsters(float dt)
    {
        iframes = Math.Max(0, iframes - dt);
        hurtFlash = Math.Max(0, hurtFlash - dt);
        if (scene != "cave") return;
        foreach (var m in monsters)
        {
            if (m.Dead) continue;
            var k = MonsterKinds[m.Kind];
            var atk = MonsterAttacks[m.Kind];
            m.Hurt = Math.Max(0, m.Hurt - dt);
            m.Touch = Math.Max(0, m.Touch - dt);
            m.Stagger = Math.Max(0, m.Stagger - dt);
            m.Cool = Math.Max(0, m.Cool - dt);
            float dx = player.X - m.X, dy = player.Y - 3 - m.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            bool safe = Dist(player.X, player.Y, ropeTile.x * T + 5, ropeTile.y * T + 5) <= 24;
            bool chase = d < (k.Flies ? 140 : 115) && !safe;
            float fade = MathF.Pow(0.02f, dt);
            if (m.Phase == "")
            {
                m.Timer -= dt;
                int busy = monsters.Count(o => !o.Dead && o.Phase is "windup" or "dash");
                if (chase && d < atk.Range && m.Cool <= 0 && busy < 2 && mode != "fade")
                {
                    m.Phase = "windup"; m.PhaseT = 0; m.Hit = false;
                    m.Vx = m.Vy = 0;
                    if (d > 0.5f) { m.AimX = dx / d; m.AimY = dy / d; } else { m.AimX = m.Flip ? -1 : 1; m.AimY = 0; }
                    Sfx.Play("warn");
                }
                else if (m.Kind == "slime")
                {
                    // Slimes hop: a quick burst, then a pause.
                    if (m.Timer <= 0)
                    {
                        m.Timer = chase ? 0.75f : 1.1f;
                        float ang = chase ? MathF.Atan2(dy, dx) : Rand(0, MathF.Tau);
                        float burst = chase ? 2.3f : 1.6f;
                        m.Vx = MathF.Cos(ang) * k.Speed * burst; m.Vy = MathF.Sin(ang) * k.Speed * burst;
                    }
                    else if (m.Timer < (chase ? 0.4f : 0.6f)) m.Vx = m.Vy = 0;
                }
                else if (m.Timer <= 0)
                {
                    m.Timer = m.Kind == "bat" ? Rand(0.2f, 0.45f) : Rand(0.4f, 1.0f);
                    float ang = chase ? MathF.Atan2(dy, dx) + (m.Kind == "bat" ? Rand(-0.9f, 0.9f) : Rand(-0.3f, 0.3f)) : Rand(0, MathF.Tau);
                    float sp = chase ? k.Speed : (rng.NextDouble() < 0.4 ? 0 : k.Speed * 0.4f);
                    m.Vx = MathF.Cos(ang) * sp; m.Vy = MathF.Sin(ang) * sp;
                }
                float mx = (m.Vx + m.Kx) * dt, my = (m.Vy + m.Ky) * dt;
                m.Kx *= fade; m.Ky *= fade;
                var (bx, by) = MoveMonster(m, mx, my);
                if (bx) { m.Vx = -m.Vx; m.Kx = 0; }
                if (by) { m.Vy = -m.Vy; m.Ky = 0; }
                if (MathF.Abs(m.Vx) > 1) m.Flip = m.Vx < 0;
            }
            else
            {
                m.PhaseT += dt;
                m.Kx *= fade; m.Ky *= fade;
                if (m.Phase == "windup")
                {
                    // Crabs and shades keep their eyes on you for the first half of the warning, then the line is fixed.
                    if (m.Kind is "crab" or "shade" && m.PhaseT < atk.Windup * 0.5f && d > 1)
                    {
                        m.AimX = dx / d; m.AimY = dy / d;
                    }
                    if (MathF.Abs(m.AimX) > 0.2f) m.Flip = m.AimX < 0;
                    MoveMonster(m, m.Kx * dt, m.Ky * dt);
                    if (m.PhaseT >= atk.Windup) { m.Phase = "dash"; m.PhaseT = 0; Sfx.Play("splash"); }
                }
                else if (m.Phase == "dash")
                {
                    float step = atk.DashSpeed * dt;
                    var (bx, by) = MoveMonster(m, m.AimX * step, m.AimY * step, stopOnBlock: true);
                    if (!m.Hit && iframes <= 0 && mode != "fade" && Dist(m.X, m.Y, player.X, player.Y - 3) < atk.Radius)
                    {
                        m.Hit = true;
                        HurtPlayer(k.Damage, m);
                    }
                    if (bx || by)
                    {
                        // Straight into a wall: a crab is stunned for a good while, the others just stop.
                        Burst(m.X, m.Y - 3, "#c9c2ae", 5);
                        m.Phase = m.Kind == "crab" ? "stun" : "recover"; m.PhaseT = 0;
                        if (m.Kind == "crab") Floater("Crash!", m.X, m.Y - 14, "#ffd76a");
                    }
                    else if (m.PhaseT >= atk.DashTime) { m.Phase = "recover"; m.PhaseT = 0; }
                }
                else if (m.Phase == "stun")
                {
                    if (m.PhaseT >= 1.4f) { m.Phase = ""; m.Cool = 0.5f; m.Timer = 0; }
                }
                else if (m.Phase == "recover")
                {
                    MoveMonster(m, m.Kx * dt, m.Ky * dt);
                    if (m.PhaseT >= atk.Recover) { m.Phase = ""; m.Cool = 0.35f; m.Timer = 0; }
                }
            }
            // Just bumping into one still hurts, but a good deal less than being caught by a lunge.
            if (d < 7 && iframes <= 0 && m.Touch <= 0 && mode != "fade" && m.Phase is not ("windup" or "stun"))
            {
                m.Touch = 1f;
                HurtPlayer(atk.Touch, m);
            }
        }
        monsters.RemoveAll(m => m.Dead);
    }

    /* ---------- Health ---------- */
    void HurtPlayer(int dmg, Monster m) => HurtPlayer(dmg, m.X, m.Y);

    // A blow from something at (fromX, fromY) knocks you back a few steps of 3 pixels.
    void HurtPlayer(int dmg, float fromX, float fromY, int push = 4)
    {
        float taken = dmg * ArmourMul;
        state.hp = Math.Max(0, state.hp - taken);
        iframes = 0.75f;
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

    // Platejaw's plate armour beats the rock crabs' shells; you wear the better one.
    float ArmourMul => Has("plate_armor") > 0 ? 0.5f : Has("shell_armor") > 0 ? 0.65f : 1f;

    // Blacking out in the caverns: you wake up outside, a bit poorer. Knocked flat at the Starwell, you come round on the
    // atoll's jetty, and Tidemane has gone back under.
    void Faint()
    {
        fish = null; reel = null; pointerHold = false;
        panel = null;
        // A whistle for Tidemane still on its way is called off: you wake somewhere else (Codex).
        call = null;
        mode = "play";
        // Knocked flat by Platejaw: it goes back down, and its rocks with it. The coelacanth was landed before the fight.
        bool guarded = guardian != null;
        guardian = null; guardianDue = false; rocks.Clear();
        tremor = null; evac = null;
        // Anywhere on Marilaya or its waters: Aling Sela takes you in, and the boat is tied up at the harbour (Marilaya.cs).
        if ((InMarilaya || InMlCave) && boss == null)
        {
            LeaveMount();
            state.aboard = false;
            FadeThrough(() =>
            {
                if (scene != "world") LoadScene("world");
                player.X = MlWakeX; player.Y = MlWakeY; player.Face = "down";
                state.hp = 35;
                if (Has("boat") > 0) { state.boatAt = "marilaya"; state.boatX = state.boatY = 0; }
                Save();
            }, () => Toast(MlWakeWords(), 7));
            return;
        }
        if (InAmihan && boss == null)
        {
            LeaveMount();
            state.aboard = false;
            FadeThrough(() =>
            {
                player.X = 1595; player.Y = 197; player.Face = "down";
                state.hp = 35;
                if (Has("boat") > 0) { state.boatX = 1483; state.boatY = 217; }
                Save();
            }, () => Toast("Lira found you by the water. You wake in Amihan Village; your boat is at the western landing. Eat and rest before another fierce catch.", 7));
            return;
        }
        if (InHabagat && boss == null)
        {
            LeaveMount();
            state.aboard = false;
            race = null; eclipse = null;
            FadeThrough(() =>
            {
                player.X = AsinanWakeX; player.Y = AsinanWakeY; player.Face = "down";
                state.hp = 35;
                if (Has("boat") > 0) { state.boatAt = "asinan"; state.boatX = state.boatY = 0; }
                Save();
            }, () => Toast("Manang Rosa found you and brought you to her house on Asinan. Your boat is tied up at the Asinan landing. Eat and rest before heading back out.", 7));
            return;
        }
        if (scene == "world" && (Aboard || Swimming) && boss == null)
        {
            LeaveMount();
            state.aboard = false;
            FadeThrough(() =>
            {
                player.X = SaltJettyX - 4; player.Y = SaltJettyY + 1; player.Face = "left";
                state.hp = 35;
                if (Has("boat") > 0) { state.boatAt = "saltmere"; state.boatX = state.boatY = 0; }
                Save();
            }, () => Toast("A passing fishing boat towed you home. You wake on Pip's jetty with your boat tied up beside it. Eat and rest before heading back out.", 7));
            return;
        }
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
        }, () => Toast($"Everything went dark... You woke up outside the caverns{(lost > 0 ? $", {lost} coins lighter" : "")}. Eat and rest to recover."
            + (guarded ? $" {GuardianName} went back under; knock on the carved stone by the Ancient pool to face it again." : ""), guarded ? 7 : 5));
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
        bool ancientFloor = caveFloor == AncientFloor;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
                if (map[y, x] == 'P' && (t * 0.3 + Pix.Hash(x, y, 7)) % 1 < 0.15) pix.Rect(x * T + 3, y * T + 5, 2, 1, "#4f8fb8");
        DrawSpots(t);
        var list = new List<(float y, Action draw)>();
        DrawGuardianWater(t);
        foreach (var n in nodes) if (!n.Mined) list.Add((n.Y * T + 9, () => DrawNode(n, t)));
        if (ancientFloor) list.Add((RuneStoneY, () => DrawRuneStone(t)));
        if (guardian != null) list.Add((guardian.Y, () => DrawGuardianBody(t)));
        foreach (var m in monsters) list.Add((m.Y, () => DrawMonster(m, t)));
        for (int y = vy0; y <= vy1; y++)
            for (int x = vx0; x <= vx1; x++)
                if (map[y, x] == 'Q') { int px = x, py = y; list.Add((py * T + 9, () => DrawPillar(px, py, t))); }
        list.Add((player.Y, DrawPlayer));
        foreach (var o in list.OrderBy(o => o.y)) o.draw();
        DrawFishing(t);
        DrawParticles();
        DrawCaveSparkles(t);

        // Always dark underground: your own light, daylight down the ladder, and anything that glows.
        bool ancient = caveFloor == AncientFloor;
        Array.Fill(dark, ancient ? 0.62f : 0.72f);
        LightHole(player.X, player.Y - 6, (Wears("headlamp") ? 100 : 64) + MathF.Sin(t * 8.3f) * 1.2f + MathF.Sin(t * 3.1f) * 0.8f, 0.95f);
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
            LightHole(RuneStoneX, RuneStoneY - 4, 14, 0.4f);
            if (guardian != null) LightHole(guardian.X, guardian.Y - 6, 30, 0.6f);
        }
        ApplyDark(new Color(5, 4, 8, 255));
        DrawCaveDust(t);
        foreach (var m in monsters) if (m.Kind == "slime") pix.Glow(m.X, m.Y - 3, 9, Pal.Rgba(110, 230, 120, 0.16f));
        foreach (var n in nodes)
            if (!n.Mined && n.Kind is "crystal" or "abyssite")
                pix.Glow(n.X * T + 5, n.Y * T, 12, n.Kind == "crystal" ? Pal.Rgba(127, 232, 255, 0.25f) : Pal.Rgba(155, 107, 224, 0.28f));
        foreach (var m in monsters) if (m.Kind == "shade") pix.Glow(m.X, m.Y - 10, 10, Pal.Rgba(155, 107, 224, 0.2f));
        DrawGuardianWarnings(t);
        if (ancient)
        {
            float a = 0.25f + 0.15f * MathF.Sin(t * 0.7f);
            pix.Glow(AncientPoolX + MathF.Sin(t * 0.4f) * 20, AncientPoolY + MathF.Cos(t * 0.3f) * 8, 14, Pal.Rgba(255, 215, 106, a));
        }
        if (hurtFlash > 0) pix.Fill(0, 0, W, H, Pal.Rgba(224, 75, 58, hurtFlash * 0.6f));
    }
}
