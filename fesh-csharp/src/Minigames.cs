using Raylib_cs;

namespace Fesh;

sealed class Bug { public float X, Y, Vx, Vy, Hop, Rest, Z; public bool Flip; }
sealed class SpearFish { public float X, Y, Vx, Vy, Turn, Flee; public CommonFish F; public bool Big; }

// Fishing besides the rod: spearfishing on the reefs, crab pots, digging worms and catching crickets for bait,
// Pip's derby, and the aquarium collections.
partial class Game
{
    /* ---------- Spearfishing ---------- */
    readonly List<SpearFish> spearFish = new();
    string spearSpot;
    int spears, speared;
    float spearT, aimX, aimY;
    (float x0, float y0, float x1, float y1, float t)? thrown;

    // Three spears and 25 seconds. Aim at a shadow with WASD or the mouse and press E to throw.
    void StartSpear(string spotId)
    {
        if (Has("spear") == 0) return;
        var s = Data.SpotById[spotId];
        var (sx, sy) = SpotPos(s);
        spearSpot = spotId;
        spears = 3;
        speared = 0;
        spearT = 25;
        thrown = null;
        aimX = sx; aimY = sy;
        spearFish.Clear();
        var list = FishWeights(spotId, null, 0);
        double sum = list.Sum(p => p.w);
        for (int i = 0; i < 6; i++)
        {
            double roll = rng.NextDouble() * sum;
            var pick = list[^1].f;
            foreach (var (f, w) in list) { if (roll < w) { pick = f; break; } roll -= w; }
            for (int tries = 0; tries < 30; tries++)
            {
                float x = sx + Rand(-s.R * 0.7f, s.R * 0.7f), y = sy + Rand(-s.R * 0.4f, s.R * 0.4f);
                if (!IsWater(x, y)) continue;
                spearFish.Add(new SpearFish { X = x, Y = y, F = pick, Big = pick.Rare || rng.NextDouble() < 0.2 });
                break;
            }
        }
        FaceToward(sx, sy);
        mode = "spear";
        Sfx.Play("splash");
        if (!state.Hinted("spear"))
        {
            state.hinted["spear"] = true;
            Toast("Spearfishing! Move the target onto a fish shadow with WASD or the mouse, and press E to throw. You have three spears.", 5);
        }
    }

    void AimAtPointer()
    {
        aimX = Gfx.Mouse.X / 4 + camX;
        aimY = Gfx.Mouse.Y / 4 + camY;
    }

    void ThrowSpear()
    {
        if (mode != "spear" || thrown != null || spears <= 0) return;
        spears--;
        FaceToward(aimX, aimY);
        swingT = 0.3f;
        thrown = (player.X, player.Y - 10, aimX, aimY, 0);
        Sfx.Play("cast");
    }

    void EndSpear()
    {
        if (mode != "spear") return;
        mode = "play";
        spearFish.Clear();
        thrown = null;
        Toast(speared == 0 ? "No luck with the spear this time." : $"You speared {speared} fish.", 2.6f);
        Save();
    }

    void UpdateSpear(float dt)
    {
        var s = Data.SpotById[spearSpot];
        var (sx, sy) = SpotPos(s);
        spearT -= dt;
        float ax = 0, ay = 0;
        if (Inp.Down(KeyboardKey.Left) || Inp.Down(KeyboardKey.A)) ax -= 1;
        if (Inp.Down(KeyboardKey.Right) || Inp.Down(KeyboardKey.D)) ax += 1;
        if (Inp.Down(KeyboardKey.Up) || Inp.Down(KeyboardKey.W)) ay -= 1;
        if (Inp.Down(KeyboardKey.Down) || Inp.Down(KeyboardKey.S)) ay += 1;
        aimX += ax * 70 * dt; aimY += ay * 70 * dt;
        if (Gfx.MouseMoved && !Gfx.OverUiPrev) AimAtPointer();
        aimX = Math.Clamp(aimX, sx - s.R - 10, sx + s.R + 10);
        aimY = Math.Clamp(aimY, sy - s.R * 0.7f - 6, sy + s.R * 0.7f + 6);

        // The fish wander, turn now and then, and dart away from a spear that lands near them.
        foreach (var f in spearFish)
        {
            f.Turn -= dt;
            f.Flee = Math.Max(0, f.Flee - dt);
            if (f.Turn <= 0)
            {
                float ang = Rand(0, MathF.Tau), sp = f.Big ? Rand(12, 20) : Rand(7, 13);
                if (Dist(f.X, f.Y, sx, sy) > s.R * 0.6f) ang = MathF.Atan2(sy - f.Y, sx - f.X) + Rand(-0.6f, 0.6f);
                f.Vx = MathF.Cos(ang) * sp; f.Vy = MathF.Sin(ang) * sp * 0.6f;
                f.Turn = Rand(0.6f, 1.8f);
            }
            float k = f.Flee > 0 ? 3.2f : 1;
            float nx = f.X + f.Vx * k * dt, ny = f.Y + f.Vy * k * dt;
            if (IsWater(nx, ny) && Dist(nx, ny, sx, sy) < s.R + 4) { f.X = nx; f.Y = ny; }
            else { f.Vx = -f.Vx; f.Vy = -f.Vy; f.Turn = Math.Min(f.Turn, 0.4f); }
        }

        if (thrown is var (x0, y0, x1, y1, t))
        {
            t += dt / 0.22f;
            thrown = (x0, y0, x1, y1, t);
            if (t >= 1)
            {
                thrown = null;
                var hit = spearFish.Where(f => Dist(f.X, f.Y, x1, y1) < (f.Big ? 6 : 5)).OrderBy(f => Dist(f.X, f.Y, x1, y1)).FirstOrDefault();
                Burst(x1, y1, "#cfe8ee", 8);
                if (hit != null)
                {
                    spearFish.Remove(hit);
                    speared++;
                    float kg = AddCatch(hit.F, hit.Big ? 1.15f : 1);
                    Floater($"{hit.F.Name}!", x1, y1 - 8, hit.F.Rare ? "#f3c25b" : "#ffffff");
                    Sfx.Play(hit.F.Rare ? "rare" : "catch");
                    Toast($"Speared a {hit.F.Name.ToLowerInvariant()}, {Kg(kg)}!", 2.4f);
                }
                else
                {
                    Floater("Missed!", x1, y1 - 8, "#cfe8ee");
                    Sfx.Play("splash");
                }
                foreach (var f in spearFish) if (Dist(f.X, f.Y, x1, y1) < 22) { f.Flee = 0.7f; f.Turn = 0; }
                if (spears <= 0 || spearFish.Count == 0) { EndSpear(); return; }
            }
        }
        if (spearT <= 0 && thrown == null) { EndSpear(); return; }
        SetPrompt($"Aim with WASD or the mouse.   [E] Throw ({spears} left)   [Esc] Stop");
    }

    void DrawSpearing(float t)
    {
        if (mode != "spear") return;
        foreach (var f in spearFish)
        {
            // A dark fish shape facing the way it swims; big ones are larger and glint.
            int dir = f.Vx < 0 ? -1 : 1, len = f.Big ? 7 : 5;
            var body = Pal.Rgba(10, 30, 45, f.Big ? 0.6f : 0.45f);
            int x = (int)MathF.Round(f.X), y = (int)MathF.Round(f.Y);
            pix.Rect(x - len / 2, y - 1, len, 2, body);
            pix.Rect(x - len / 2 + 1, y - 2, len - 2, 1, body);
            int tail = dir > 0 ? x - len / 2 - 1 : x + len / 2 + 1;
            int wag = (int)(t * 8 + f.X) % 2;
            pix.Rect(tail - (dir > 0 ? 1 : 0), y - 2 + wag, 2, 1, body);
            pix.Rect(tail - (dir > 0 ? 1 : 0), y + wag, 2, 1, body);
            if (f.Big && (t * 2 + f.Y) % 1 < 0.15f) pix.Rect(x + dir, y - 1, 1, 1, "#ffffff");
        }
        if (thrown is var (x0, y0, x1, y1, tt))
        {
            float k = Math.Min(1, tt), hx = x0 + (x1 - x0) * k, hy = y0 + (y1 - y0) * k - MathF.Sin(k * MathF.PI) * 6;
            float dx = x1 - x0, dy = y1 - y0, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            pix.Line(hx - dx / d * 8, hy - dy / d * 8, hx, hy, "#8a6440");
            pix.Rect(hx, hy, 1, 1, "#e8f0f4");
        }
        // The aiming ring pulses.
        float r = 3.5f + MathF.Sin(t * 8) * 0.6f;
        pix.Ring(aimX, aimY, r, Pal.C("#ffe28a"));
        pix.Rect(aimX, aimY - 2, 1, 1, "#ffe28a"); pix.Rect(aimX, aimY + 2, 1, 1, "#ffe28a");
        pix.Rect(aimX - 3, aimY, 1, 1, "#ffe28a"); pix.Rect(aimX + 3, aimY, 1, 1, "#ffe28a");
    }

    /* ---------- Crab pots ---------- */
    bool PotReady(Build b) => state.day > state.pots.GetValueOrDefault($"{b.x},{b.y}", state.day);

    void HaulPot(Build b)
    {
        string key = $"{b.x},{b.y}";
        if (!PotReady(b)) return;
        state.pots[key] = state.day;
        var biome = Data.Biomes[BiomeAt(b.x, b.y)].Id;
        var got = new List<string>();
        var crab = Data.PotCatch[biome][0];
        int crabs = 1 + (rng.NextDouble() < 0.45 ? 1 : 0);
        for (int i = 0; i < crabs; i++) AddCatch(crab);
        got.Add(Items.Amount(crab.Id, crabs));
        if (rng.NextDouble() < 0.6) { int n = 1 + rng.Next(2); Give("seaweed", n); got.Add(Items.Amount("seaweed", n)); }
        if (rng.NextDouble() < 0.08) { Give("old_boot"); got.Add("an old boot"); }
        if (rng.NextDouble() < 0.04) { Give("pearl"); got.Add("a pearl!"); Sfx.Play("rare"); }
        FaceToward(b.x * T + 5, b.y * T + 5);
        swingT = 0.3f;
        Burst(b.x * T + 5, b.y * T + 4, "#cfe8ee", 10);
        Sfx.Play("splash");
        heldItem = crab.Id;
        heldT = 1.2f;
        Toast($"You haul up the pot: {string.Join(", ", got)}. It's set again for tomorrow.", 3.5f);
        Save();
    }

    /* ---------- Worms and crickets ---------- */
    // Worm mounds are a kind of loose find (see Build.cs) that you dig up instead of walking over.
    void DigWorms(Loose l)
    {
        state.loose.Remove(l);
        int n = 2 + rng.Next(2);
        Give("worm", n);
        FaceToward(l.x, l.y);
        swingT = 0.25f;
        Burst(l.x, l.y, "#6b4a2b", 8);
        Sfx.Play("chop");
        Floater($"+{n} worms", l.x, l.y - 6, "#e8939a");
        if (!state.Hinted("worms"))
        {
            state.hinted["worms"] = true;
            Toast("Worms! Pick them as your bait in the tackle box (T). Freshwater fish love them.", 4);
        }
        else Toast($"Dug up {n} worms ({Has("worm")} now).", 1.8f);
    }

    readonly List<Bug> bugs = new();
    bool quietWildlife;   // no crickets or worm mounds turning up on their own (the autotest uses this so they stay out of its way)

    Bug CricketNear() => bugs.FirstOrDefault(b => b.Hop <= 0 && Dist(b.X, b.Y, player.X, player.Y - 2) < 12);

    void CatchCricket(Bug b)
    {
        bugs.Remove(b);
        Give("cricket");
        Sfx.Play("pickup");
        Floater("+1 cricket", b.X, b.Y - 6, "#b8d46a");
        if (!state.Hinted("crickets"))
        {
            state.hinted["crickets"] = true;
            Toast("Got a cricket! Jumpers and shallow-water fish love them. Pick your bait in the tackle box (T).", 4);
        }
    }

    // Crickets live in the grass. They sit, chirp, and hop away when you come close, but only once every second or so.
    void UpdateBugs(float dt)
    {
        if (scene != "world") { bugs.Clear(); return; }
        if (quietWildlife) return;
        bugs.RemoveAll(b => Dist(b.X, b.Y, player.X, player.Y) > 230);
        if (bugs.Count < 3 && rng.NextDouble() < dt * 0.6)
        {
            int tx = (int)(player.X / T) + rng.Next(-15, 16), ty = (int)(player.Y / T) + rng.Next(-9, 10);
            float x = tx * T + 5, y = ty * T + 6;
            if (TileAt(tx, ty) is 'g' or 'j' && Dist(x, y, player.X, player.Y) > 40 && CanStand(x, y))
                bugs.Add(new Bug { X = x, Y = y, Rest = Rand(0.5f, 2f) });
        }
        foreach (var b in bugs)
        {
            if (b.Hop > 0)
            {
                b.Hop -= dt;
                float nx = b.X + b.Vx * dt, ny = b.Y + b.Vy * dt;
                if (CanStand(nx, ny)) { b.X = nx; b.Y = ny; }
                b.Z = MathF.Sin(MathF.Max(0, b.Hop) / 0.35f * MathF.PI) * 5;
                if (b.Hop <= 0) b.Z = 0;
                continue;
            }
            b.Rest -= dt;
            bool spooked = Dist(b.X, b.Y, player.X, player.Y) < 16 && b.Rest < 0.9f;
            if (b.Rest > 0 && !spooked) continue;
            float ang = spooked ? MathF.Atan2(b.Y - player.Y, b.X - player.X) + Rand(-0.7f, 0.7f) : Rand(0, MathF.Tau);
            float sp = spooked ? 38 : 16;
            b.Vx = MathF.Cos(ang) * sp; b.Vy = MathF.Sin(ang) * sp;
            b.Flip = b.Vx < 0;
            b.Hop = 0.35f;
            b.Rest = spooked ? 1.6f : Rand(1.5f, 4f);
        }
    }

    void DrawBugs(float t)
    {
        foreach (var b in bugs)
        {
            int x = (int)MathF.Round(b.X), y = (int)MathF.Round(b.Y), z = (int)MathF.Round(b.Z), d = b.Flip ? -1 : 1;
            pix.Rect(x - 1, y + 1, 3, 1, "rgba(0,0,0,0.18)");
            pix.Rect(x - 1, y - 1 - z, 3, 1, "#5b6b2a");
            pix.Rect(x + d * 2, y - 2 - z, 1, 1, "#4a5822");
            pix.Rect(x - d, y - z, 1, 1, "#4a5822");
            if (b.Hop <= 0 && (t * 5 + b.X) % 2 < 0.2f) pix.Rect(x + d * 2, y - 3, 1, 1, "#4a5822");   // a twitching feeler
        }
    }

    /* ---------- Pip's derby ---------- */
    float derbyT, derbyBest;
    string derbyFish;
    const float DerbyTime = 180;
    bool DerbyOn => derbyT > 0;

    // Today's rival anglers and their catches. They get better each time you win.
    (string name, float kg, string fish)[] DerbyRivals()
    {
        float scale = 1 + 0.5f * Math.Min(state.derbyWins, 6);
        string[] names = { "Old Bess", "Marlo", "Captain Ivy" };
        float[] baseKg = { 0.8f, 1.6f, 3.2f };
        return names.Select((n, i) =>
        {
            float kg = MathF.Round(baseKg[i] * scale * (0.85f + 0.3f * (float)Pix.Hash(state.day, i, 71)) * 100) / 100;
            var f = Data.AllCommon.Where(c => !c.Legend && !c.Rare && c.Kg * 1.3f >= kg).OrderBy(c => c.Kg).FirstOrDefault() ?? Data.FishById["bluefin_tuna"];
            return (n, kg, f.Name);
        }).ToArray();
    }

    void StartDerby()
    {
        if (state.derbyDay == state.day || DerbyOn) return;
        state.derbyDay = state.day;
        derbyT = DerbyTime;
        derbyBest = 0;
        derbyFish = null;
        ClosePanels();
        Sfx.Play("coin");
        Toast("The derby is on! Catch the heaviest fish you can in three minutes. Any island, any spot.", 4.5f);
        Save();
    }

    void DerbyCatch(CommonFish f, float kg)
    {
        if (!DerbyOn || kg <= derbyBest) return;
        derbyBest = kg;
        derbyFish = f.Name;
        Floater("Derby best!", player.X, player.Y - 40, "#f3c25b");
    }

    void TickDerby(float dt)
    {
        if (!DerbyOn) return;
        derbyT -= dt;
        if (derbyT <= 0) EndDerby();
    }

    void EndDerby()
    {
        derbyT = 0;
        var rivals = DerbyRivals();
        int place = 4 - rivals.Count(r => derbyBest > r.kg);
        string what = derbyFish == null ? "You didn't catch anything" : $"Your {derbyFish.ToLowerInvariant()} weighed {Kg(derbyBest)}";
        string prize;
        switch (place)
        {
            case 1:
                state.coins += 120;
                string tackle = Has("golden_hook") == 0 ? "golden_hook" : Has("gold_reel") == 0 ? "gold_reel" : "glow_bait";
                Give(tackle, tackle == "glow_bait" ? 5 : 1);
                state.derbyWins++;
                prize = $"First place! Pip hands you 120 coins and {Items.Amount(tackle, tackle == "glow_bait" ? 5 : 1)}.";
                Sfx.Play("rare");
                break;
            case 2: state.coins += 50; Give("glow_bait", 3); prize = "Second place: 50 coins and 3 glow bait."; Sfx.Play("coin"); break;
            case 3: state.coins += 20; Give("bait", 5); prize = "Third place: 20 coins and 5 bait."; Sfx.Play("coin"); break;
            default: Give("bait", 1); prize = "No place this time. Pip gives you a bait for trying."; Sfx.Play("fail"); break;
        }
        Toast($"The derby is over! {what}. {prize}", 6);
        Save();
    }

    /* ---------- Aquarium collections ---------- */
    HashSet<string> Displayed() => state.tanks.Values.SelectMany(l => l).ToHashSet();
    bool SetActive(string id) => Data.AquaSets.First(a => a.Id == id).Fish.All(Displayed().Contains);

    /* ---------- Footsteps ---------- */
    // A little puff at your feet on sand, dunes and snow, and a splash when you're wading.
    void Footstep()
    {
        char g = TileAt((int)MathF.Floor(player.X / T), (int)MathF.Floor((player.Y - 1.5f) / T));
        string col = g switch { 's' or 'p' => "#d8bb7e", 'D' => "#d9a457", 'n' or 'i' => "#ffffff", 'e' => "#a19c90", _ => Wadeable(g) ? "#cfe8ee" : null };
        if (col == null || scene != "world") return;
        for (int i = 0; i < 2; i++)
            particles.Add(new Particle { X = player.X + Rand(-2, 2), Y = player.Y, Vx = Rand(-10, 10), Vy = Rand(-22, -10), Life = Rand(0.18f, 0.3f), Color = col });
    }
}
