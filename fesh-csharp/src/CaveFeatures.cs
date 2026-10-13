using Raylib_cs;

namespace Fesh;

// Something to find on a cave floor besides ore and monsters (1.29). Kind: "chest" (loot, and below floor 3 sometimes an
// ambush), "crack" (a cracked wall that three pickaxe blows open onto a hidden chamber with a richer chest), "spring" (a
// glowing spring that heals you once) and "shroom" (glowing mushrooms: light and looks only).
sealed class CaveFeature
{
    public string Kind; public int X, Y; public bool Done, Ambush, Rich; public int Hits; public (int x, int y)[] Pocket;
}

partial class Game
{
    readonly List<CaveFeature> caveFeatures = new();
    readonly HashSet<(int, int)> crackTiles = new();

    // Called at the end of a floor's generation (not the Ancient Floor, which is its own place).
    void PlaceCaveFeatures(Random r, Dictionary<(int, int), int> dist, int sx, int sy)
    {
        caveFeatures.Clear(); crackTiles.Clear();
        int Walls(int x, int y) => (caveMap[y - 1, x] == 'W' ? 1 : 0) + (caveMap[y + 1, x] == 'W' ? 1 : 0) + (caveMap[y, x - 1] == 'W' ? 1 : 0) + (caveMap[y, x + 1] == 'W' ? 1 : 0);
        bool Clear(int x, int y) => caveMap[y, x] == 'F' && dist.ContainsKey((x, y)) && dist[(x, y)] > 5
            && Math.Max(Math.Abs(x - sx), Math.Abs(y - sy)) > 4 && (holeTile.x < 0 || Math.Max(Math.Abs(x - holeTile.x), Math.Abs(y - holeTile.y)) > 3)
            && !nodes.Any(n => Math.Abs(n.X - x) <= 1 && Math.Abs(n.Y - y) <= 1)
            && !caveFeatures.Any(f => Math.Abs(f.X - x) + Math.Abs(f.Y - y) < 6)
            && (poolPos is not (float ppx, float ppy) || Dist(x * T + 5, y * T + 5, ppx, ppy) > 40);
        var order = dist.Keys.Where(p => p.Item1 > 2 && p.Item2 > 2 && p.Item1 < CaveW - 3 && p.Item2 < CaveH - 3).OrderBy(_ => r.Next()).ToList();

        // Chests tucked into dead ends and alcoves: one on the way, a second further off, a third on the deepest floors.
        int chests = caveFloor >= 8 ? 3 : 2;
        for (int minWalls = 2; minWalls >= 1 && caveFeatures.Count(f => f.Kind == "chest") < chests; minWalls--)
            foreach (var (x, y) in order.OrderByDescending(p => Walls(p.Item1, p.Item2) + r.NextDouble()))
            {
                if (caveFeatures.Count(f => f.Kind == "chest") >= chests) break;
                if (Walls(x, y) < minWalls || !Clear(x, y)) continue;
                caveFeatures.Add(new CaveFeature { Kind = "chest", X = x, Y = y, Ambush = caveFloor >= 3 && r.NextDouble() < 0.4 });
            }

        // A cracked wall hiding a chamber: a chip in the rock beside a passage, with solid rock all round the room behind it.
        if (caveFloor >= 2 && r.NextDouble() < 0.8)
        {
            bool Solid(int x0, int y0, int x1, int y1)
            {
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                        if (xx < 1 || yy < 1 || xx > CaveW - 2 || yy > CaveH - 2 || caveMap[yy, xx] != 'W') return false;
                return true;
            }
            foreach (var (x, y) in order)
            {
                if (!Clear(x, y)) continue;
                var dirs = new[] { (1, 0), (-1, 0), (0, -1), (0, 1) }.OrderBy(_ => r.Next());
                bool done = false;
                foreach (var (dx, dy) in dirs)
                {
                    (int x, int y) crack = (x + dx, y + dy);
                    (int x, int y)[] pocket; bool ok;
                    if (dx != 0)
                    {
                        int px = dx > 0 ? x + 2 : x - 3;
                        ok = Solid(dx > 0 ? x + 1 : x - 4, y - 1, dx > 0 ? x + 4 : x - 1, y + 2);
                        pocket = new[] { (px + (dx > 0 ? 0 : 1), y), (px + (dx > 0 ? 1 : 0), y), (px + (dx > 0 ? 0 : 1), y + 1), (px + (dx > 0 ? 1 : 0), y + 1) };
                    }
                    else
                    {
                        int py = dy > 0 ? y + 2 : y - 3;
                        ok = Solid(x - 1, dy > 0 ? y + 1 : y - 4, x + 2, dy > 0 ? y + 4 : y - 1);
                        pocket = new[] { (x, py + (dy > 0 ? 0 : 1)), (x + 1, py + (dy > 0 ? 0 : 1)), (x, py + (dy > 0 ? 1 : 0)), (x + 1, py + (dy > 0 ? 1 : 0)) };
                    }
                    if (!ok) continue;
                    caveFeatures.Add(new CaveFeature { Kind = "crack", X = crack.x, Y = crack.y, Pocket = pocket });
                    crackTiles.Add(crack);
                    done = true; break;
                }
                if (done) break;
            }
        }

        // A glowing spring now and then, in the open.
        if (caveFloor >= 2 && r.NextDouble() < 0.5)
            foreach (var (x, y) in order)
            {
                if (!Clear(x, y) || Walls(x, y) > 0) continue;
                caveFeatures.Add(new CaveFeature { Kind = "spring", X = x, Y = y });
                break;
            }

        // Glowing mushrooms along the walls.
        int shrooms = 6 + r.Next(5);
        foreach (var (x, y) in order)
        {
            if (caveFeatures.Count(f => f.Kind == "shroom") >= shrooms) break;
            if (caveMap[y, x] != 'F' || Walls(x, y) < 1 || dist[(x, y)] < 4) continue;
            if (caveFeatures.Any(f => f.Kind == "shroom" && Math.Abs(f.X - x) + Math.Abs(f.Y - y) < 4) || nodes.Any(n => n.X == x && n.Y == y)) continue;
            caveFeatures.Add(new CaveFeature { Kind = "shroom", X = x, Y = y });
        }
    }

    /* ---------- What E does ---------- */
    Target CaveFeatureTarget(float x, float y, int fx, int fy)
    {
        foreach (var f in caveFeatures)
        {
            if (f.Kind == "crack" && !f.Done && f.X == fx && f.Y == fy)
                return new Target { Type = "cavecrack", Ref = f, Label = PickTier > 0 ? "Chip at the cracked wall" : "A cracked wall. A pickaxe would break it" };
            if (f.Kind == "chest" && !f.Done && Dist(x, y, f.X * T + 5, f.Y * T + 7) < 13)
                return new Target { Type = "cavechest", Ref = f, Label = f.Rich ? "Open the hidden chest" : "Open the chest" };
            if (f.Kind == "spring" && Dist(x, y, f.X * T + 5, f.Y * T + 6) < 15)
                return new Target { Type = f.Done ? "info" : "cavespring", Ref = f, Label = f.Done ? "The spring has gone still. It will not help you again" : "Drink from the glowing spring" };
        }
        return null;
    }

    void ChipCrack(CaveFeature f)
    {
        if (PickTier < 1) { Sfx.Play("nope"); Toast("The wall is cracked, and something glints behind it. You need a pickaxe.", 3.5f); return; }
        FaceToward(f.X * T + 5, f.Y * T + 5);
        Swing("pick", 0.25f);
        Sfx.Play("mine");
        shakeT = 0.25f;
        Burst(f.X * T + 5, f.Y * T + 5, "#8a7a68", 6);
        if (++f.Hits < 3) { Floater(f.Hits == 1 ? "Crack!" : "It's giving...", f.X * T + 5, f.Y * T - 4, "#e8d8b8"); return; }
        f.Done = true;
        crackTiles.Remove((f.X, f.Y));
        caveMap[f.Y, f.X] = 'F';
        foreach (var (px, py) in f.Pocket) caveMap[py, px] = 'F';
        // The chest sits at the far end of the chamber, and the room is drawn again with its new floor.
        var far = f.Pocket.OrderByDescending(p => Math.Abs(p.x - f.X) + Math.Abs(p.y - f.Y)).First();
        caveFeatures.Add(new CaveFeature { Kind = "chest", X = far.x, Y = far.y, Rich = true });
        RenderCaveBase();
        Sfx.Play("rare");
        Burst(f.X * T + 5, f.Y * T + 5, "#d8c8a8", 14);
        Toast("The wall crumbles. A hidden chamber, and a chest at the back!", 4);
    }

    void OpenCaveChest(CaveFeature f)
    {
        f.Done = true;
        FaceToward(f.X * T + 5, f.Y * T + 5);
        Swing("hands", 0.3f);
        Sfx.Play("rare");
        var r = new Random(rng.Next());
        int mult = f.Rich ? 2 : 1;
        int coins = (r.Next(18, 41) + caveFloor * 9) * mult;
        state.coins += coins;
        var parts = new List<string> { $"{coins} coins" };
        // Ore for the depth (the pickaxe you carry decides what you could mine, so offer a kind you can use or are about to).
        var kinds = Ores.Where(o => caveFloor >= o.MinFloor && caveFloor <= o.MaxFloor && o.Tier <= Math.Max(1, PickTier + 1) && o.Kind != "abyssite").ToList();
        if (kinds.Count > 0)
        {
            var o = kinds[r.Next(kinds.Count)];
            int n = (o.Tier >= 4 ? 1 : 2) * mult + r.Next(0, 2);
            Give(o.Item, n);
            parts.Add(Items.Amount(o.Item, n));
        }
        if (r.NextDouble() < 0.4 + (f.Rich ? 0.3 : 0))
        {
            string bait = new[] { "glow_shrimp", "slime_gel", "worm", "cut_bait" }[r.Next(4)];
            int n = r.Next(2, 5) * mult;
            Give(bait, n);
            parts.Add(Items.Amount(bait, n));
        }
        GainXp(6 * mult);
        Burst(f.X * T + 5, f.Y * T + 2, "#f3c25b", 12);
        Floater("+" + coins, f.X * T + 5, f.Y * T - 8, "#f3c25b");
        Toast("In the chest: " + string.Join(", ", parts) + ".", 4);
        // Some chests are guarded: the lid goes up and the dark moves.
        if (f.Ambush)
        {
            var spots = caveMap.Cast<char>().Select((c, i) => (c, x: i % CaveW, y: i / CaveW)).Where(t => t.c == 'F'
                && Math.Abs(t.x - f.X) + Math.Abs(t.y - f.Y) is >= 5 and <= 10 && !monsters.Any(m => Dist(m.X, m.Y, t.x * T + 5, t.y * T + 7) < 40)).OrderBy(_ => r.Next()).Take(caveFloor >= 7 ? 3 : 2).ToList();
            foreach (var s in spots)
            {
                string kind = PickMonster(r);
                monsters.Add(new Monster { Kind = kind, X = s.x * T + 5, Y = s.y * T + 7, Hp = MonsterHp(kind), Max = MonsterHp(kind), Timer = (float)r.NextDouble() });
            }
            if (spots.Count > 0) ToastLater("Something stirs in the dark: the chest was guarded!");
        }
        Save();
    }

    void DrinkSpring(CaveFeature f)
    {
        f.Done = true;
        FaceToward(f.X * T + 5, f.Y * T + 5);
        Swing("hands", 0.35f);
        Sfx.Play("splash");
        float healed = Math.Min(100 - state.hp, 60);
        state.hp = Math.Min(100, state.hp + 60);
        state.food = Math.Min(100, state.food + 15);
        Burst(f.X * T + 5, f.Y * T + 3, "#7fe8ff", 12);
        Floater(healed > 0 ? $"+{healed:0} health" : "Refreshed", f.X * T + 5, f.Y * T - 8, "#7fe8ff");
        Toast("Cold, clear water that tastes of stone. You feel better.", 3.5f);
    }

    /* ---------- Drawing ---------- */
    // The chamber's wall, with its crack, drawn into the floor's base layer.
    void PaintCrack(Pix g, int X, int Y, int x, int y)
    {
        const string c = "#a89a86", d = "#6a5c4e";
        int j = (int)(Pix.Hash(x, y, 71) * 3);
        g.Rect(X + 4 + j, Y, 1, 2, c); g.Rect(X + 3 + j, Y + 2, 1, 2, c); g.Rect(X + 5 + j, Y + 4, 1, 2, c); g.Rect(X + 4 + j, Y + 6, 1, 2, c); g.Rect(X + 5 + j, Y + 8, 1, 2, c);
        g.Rect(X + 5 + j, Y + 1, 1, 3, d); g.Rect(X + 6 + j, Y + 5, 1, 2, d);
        g.Rect(X + 2, Y + 7, 1, 1, c); g.Rect(X + 8, Y + 3, 1, 1, c);
    }

    void AddCaveFeatureObjects(List<(float y, Action draw)> list, float t)
    {
        foreach (var f in caveFeatures)
        {
            var feat = f;
            if (f.Kind == "chest") list.Add((f.Y * T + 9, () => DrawCaveChest(feat, t)));
            else if (f.Kind == "spring") list.Add((f.Y * T + 6, () => DrawSpring(feat, t)));
            else if (f.Kind == "shroom") list.Add((f.Y * T + 9, () => DrawShrooms(feat, t)));
        }
    }

    // A glint on the cracked wall every so often, so it catches the eye.
    void DrawCrackGlints(float t)
    {
        foreach (var f in caveFeatures)
            if (f.Kind == "crack" && !f.Done && (t * 0.7f + f.X * 0.37f) % 1 < 0.12f)
                pix.Rect(f.X * T + 4 + (int)(Pix.Hash(f.X, f.Y, 72) * 3), f.Y * T + 3 + (int)(t * 3) % 4, 1, 1, "#ffffff");
    }

    void DrawCaveChest(CaveFeature f, float t)
    {
        int X = f.X * T, Y = f.Y * T;
        pix.Rect(X + 1, Y + 8, 8, 2, "rgba(0,0,0,0.35)");
        string wood = f.Rich ? "#7a4a8a" : "#8a5a32", woodHi = f.Rich ? "#9a66ae" : "#a8703f", woodLo = f.Rich ? "#4e2e5a" : "#5e3b20";
        pix.Rect(X + 1, Y + 4, 8, 5, wood); pix.Rect(X + 1, Y + 4, 8, 1, woodHi); pix.Rect(X + 1, Y + 8, 8, 1, woodLo);
        pix.Rect(X + 3, Y + 4, 1, 5, "#c9b27a"); pix.Rect(X + 6, Y + 4, 1, 5, "#c9b27a");
        if (!f.Done)
        {
            pix.Rect(X + 2, Y + 2, 6, 2, wood); pix.Rect(X + 2, Y + 1, 6, 1, woodHi); pix.Rect(X + 3, Y + 2, 1, 2, "#c9b27a"); pix.Rect(X + 6, Y + 2, 1, 2, "#c9b27a");
            pix.Rect(X + 4, Y + 4, 2, 2, "#f3c25b");
            if ((t * 0.9 + f.X * 0.4) % 1 < 0.1) pix.Rect(X + 7, Y + 2, 1, 1, "#ffffff");
        }
        else
        {
            // The lid thrown back and a few coins left in the corner.
            pix.Rect(X + 2, Y, 6, 3, woodLo); pix.Rect(X + 2, Y, 6, 1, wood);
            pix.Rect(X + 2, Y + 5, 6, 2, "#2a1a10");
        }
    }

    void DrawSpring(CaveFeature f, float t)
    {
        int X = f.X * T, Y = f.Y * T;
        pix.Rect(X, Y + 7, 10, 2, "rgba(0,0,0,0.3)");
        pix.Rect(X + 1, Y + 3, 8, 5, "#5e5852"); pix.Rect(X + 2, Y + 2, 6, 6, "#766f66"); pix.Rect(X + 2, Y + 2, 6, 1, "#8f877c");
        string w = f.Done ? "#2b4a58" : "#4fc8ff", wl = f.Done ? "#3a5a68" : "#bff4ff";
        pix.Rect(X + 3, Y + 3, 4, 3, w);
        if (!f.Done)
        {
            int sh = (int)(t * 4 + f.X) % 3;
            pix.Rect(X + 3 + sh, Y + 3 + sh % 2, 2, 1, wl);
            if ((t * 1.3 + f.Y * 0.2) % 1 < 0.25) pix.Rect(X + 4, Y + 1 - (int)((t * 1.3 + f.Y * 0.2) % 1 * 8), 1, 1, "#bff4ff");
        }
    }

    // Mushrooms in the theme's colours: pink and cyan on the upper floors, violet deeper, teal on the Ancient floor.
    (string cap, string hi) ShroomColors(CaveFeature f)
    {
        int k = (f.X * 7 + f.Y * 3) % 3;
        return caveFloor >= 9 ? (k == 0 ? "#b38cff" : k == 1 ? "#8cc8ff" : "#ff8ccf", "#e6d6ff") : (k == 0 ? "#6fe8d8" : k == 1 ? "#ff9ad8" : "#a8ff9a", "#e8fff8");
    }

    void DrawShrooms(CaveFeature f, float t)
    {
        int X = f.X * T, Y = f.Y * T;
        var (cap, hi) = ShroomColors(f);
        float pulse = 0.5f + 0.5f * MathF.Sin(t * 1.6f + f.X * 0.7f);
        string under = "#2a3a38", stem = "#d8d0c0", stemLo = "#a8a090";
        pix.Rect(X + 1, Y + 8, 8, 1, "rgba(0,0,0,0.25)");
        // A big one in the middle with two small ones beside it: round caps with pale spots, short thick stems.
        pix.Rect(X + 4, Y + 5, 2, 3, stem); pix.Rect(X + 5, Y + 5, 1, 3, stemLo);
        pix.Rect(X + 3, Y + 1, 4, 1, cap); pix.Rect(X + 2, Y + 2, 6, 2, cap); pix.Rect(X + 2, Y + 4, 6, 1, under);
        pix.Rect(X + 3, Y + 2, 1, 1, hi); pix.Rect(X + 6, Y + 3, 1, 1, hi);
        if (pulse > 0.6f) pix.Rect(X + 4, Y + 1, 2, 1, hi);
        pix.Rect(X + 1, Y + 7, 1, 2, stem); pix.Rect(X, Y + 5, 3, 2, cap); pix.Rect(X, Y + 7, 3, 1, under); pix.Rect(X + 1, Y + 5, 1, 1, hi);
        pix.Rect(X + 8, Y + 7, 1, 2, stem); pix.Rect(X + 7, Y + 6, 3, 2, cap); pix.Rect(X + 7, Y + 8, 3, 1, under); pix.Rect(X + 8, Y + 6, 1, 1, hi);
    }

    void LightCaveFeatures(float t)
    {
        foreach (var f in caveFeatures)
        {
            if (f.Kind == "spring") LightHole(f.X * T + 5, f.Y * T + 5, f.Done ? 8 : 26, f.Done ? 0.2f : 0.6f);
            else if (f.Kind == "shroom") LightHole(f.X * T + 4, f.Y * T + 4, 12, 0.5f + 0.1f * MathF.Sin(t * 1.5f + f.X));
            else if (f.Kind == "chest" && !f.Done) LightHole(f.X * T + 5, f.Y * T + 5, 11, 0.4f);
            else if (f.Kind == "crack" && !f.Done) LightHole(f.X * T + 5, f.Y * T + 5, 9, 0.25f);
        }
    }

    void GlowCaveFeatures(float t)
    {
        foreach (var f in caveFeatures)
        {
            if (f.Kind == "spring" && !f.Done) pix.Glow(f.X * T + 5, f.Y * T + 4, 16, Pal.Rgba(127, 232, 255, 0.3f + 0.06f * MathF.Sin(t * 2)));
            else if (f.Kind == "shroom")
            {
                var c = Pal.C(ShroomColors(f).cap);
                pix.Glow(f.X * T + 4, f.Y * T + 4, 10, Pal.Rgba(c.R, c.G, c.B, 0.28f + 0.06f * MathF.Sin(t * 1.5f + f.X)));
            }
            else if (f.Kind == "chest" && !f.Done) pix.Glow(f.X * T + 5, f.Y * T + 4, 9, f.Rich ? Pal.Rgba(190, 120, 255, 0.28f) : Pal.Rgba(255, 215, 106, 0.22f));
        }
    }
}
