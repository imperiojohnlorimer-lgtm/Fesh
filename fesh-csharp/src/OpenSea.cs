namespace Fesh;

// Fishing the open sea: from the boat (or from Tidemane's back) anywhere over deep water. The sea spots ("opensea" west
// of Amihan, "amihansea" in it, "habagatsea" along the bottom) have no fixed place; SpotPos gives where you're casting. Out there, feeding frenzies
// come and go: bait fish balled up under circling birds. Cast into one for a much quicker bite.
sealed class School { public float X, Y, Age, Life; public int Seed; }

partial class Game
{
    (float x, float y) seaSpot;            // where an open-sea cast is aimed (SpotPos for the sea spots)
    readonly List<School> schools = new();
    float schoolT = 4;

    // Afloat over deep water: in the boat, or swimming on Tidemane.
    bool OverDeepSea => scene == "world" && (Aboard || Swimming) && TileUnder(player.X, player.Y) == '~';

    School SchoolInReach() => schools.Where(sc => sc.Age > 1.5f && sc.Age < sc.Life - 1 && Dist(sc.X, sc.Y, player.X, player.Y) < 70)
        .OrderBy(sc => Dist(sc.X, sc.Y, player.X, player.Y)).FirstOrDefault();

    // Where an open-sea cast goes: toward a feeding frenzy in reach, or straight ahead (or to whichever side is water).
    (float x, float y) SeaCastPoint()
    {
        if (SchoolInReach() is School sc) return (sc.X, sc.Y);
        var order = player.Face switch
        {
            "left" => new[] { (-1, 0), (0, 1), (0, -1), (1, 0) },
            "up" => new[] { (0, -1), (1, 0), (-1, 0), (0, 1) },
            "down" => new[] { (0, 1), (1, 0), (-1, 0), (0, -1) },
            _ => new[] { (1, 0), (0, 1), (0, -1), (-1, 0) }
        };
        foreach (var (dx, dy) in order)
        {
            float x = player.X + dx * 34, y = player.Y - 6 + dy * 26;
            if (IsWater(x, y) && IsWater((x + player.X) / 2, (y + player.Y - 6) / 2)) return (x, y);
        }
        return (player.X, player.Y);
    }

    // Which sea you're on: Amihan's to the east, Habagat's along the bottom of the map, the open sea between.
    string SeaSpotHere => InAmihan ? "amihansea" : InHabagat ? "habagatsea" : "opensea";

    // The open-sea target: whichever sea you're in, aimed at the fish. Nothing is fished inside the sanctuary.
    Target SeaTarget()
    {
        if (!OverDeepSea) return null;
        if (InSanctuary(player.X, player.Y) || InSanctuary(player.X, player.Y - 6)) return new Target { Type = "info", Label = "No fishing in the marine sanctuary (the buoys mark it)" };
        var s = Data.SpotById[SeaSpotHere];
        seaSpot = SeaCastPoint();
        // Right by the buoys, facing in, the cast would land inside.
        if (InSanctuary(seaSpot.x, seaSpot.y)) return new Target { Type = "info", Label = "No fishing in the marine sanctuary (turn away from the buoys)" };
        string what = SchoolInReach() != null ? "Cast into the feeding frenzy" : s.Action;
        return new Target { Type = "spot", Id = s.Id, Label = what + " (hold to cast further)" };
    }

    // At the helm: a fishing spot in reach (named, or the open sea under you), else landing or steering.
    // R lands from beside any shore (see ToggleRide), so fishing keeps E even there.
    Target HelmTarget()
    {
        bool canLand = LandingSpot() != null;
        // With a trolling line out, E (or F) winds it in; R still lands.
        if (trolling) return new Target { Type = "trolling", Label = "Wind in the trolling line", AltType = "troll", RideLabel = canLand ? "Land on shore" : null };
        // Racing: no stopping to fish (it would stop the clock with you).
        if (race != null) return new Target { Type = "info", Label = "Racing! Steer for the flashing gate", RideLabel = canLand ? "Land (ends the race)" : null };
        // A sea turtle, a dugong or the whale shark alongside comes first: you watch them from the boat.
        if (SeaLifeInReach() is SeaCreature sc) return new Target { Type = "watch", Ref = sc, Label = SeaKinds[sc.Kind].label, RideLabel = canLand ? "Land on shore" : null };
        // So are Bakawan's alitaptap, from just offshore after dark.
        if (FirefliesOut && FireflyTreeNear(32) is int ft and >= 0)
            return new Target { Type = "fireflies", Id = ft.ToString(), Label = "Watch the alitaptap", RideLabel = canLand ? "Land on shore" : null };
        Target fishing = null;
        foreach (var s in Data.Spots)
            if (s.Scene == "world" && SpotOpen(s.Id) && SpotKnown(s) && s.Id != "icehole" && Dist(player.X, player.Y, s.X, s.Y) < s.R)
            {
                fishing = SpotTarget(s);
                fishing.Alt2Type = fishing.Alt2Label = null;   // no spearfishing from a boat
                break;
            }
        if (fishing == null && SeaTarget() is Target sea)
        {
            fishing = sea;
            if (sea.Type == "spot") { fishing.AltType = "troll"; fishing.AltLabel = "Troll a lure"; }
        }
        if (fishing != null)
        {
            if (canLand) fishing.RideLabel = "Land on shore";
            return fishing;
        }
        return new Target { Type = "land", Label = canLand ? "Land on shore" : "Steer with <move> · fish over deep water · <map> sea chart" };
    }

    bool InSchool(float x, float y) => schools.Any(sc => sc.Age < sc.Life && Dist(sc.X, sc.Y, x, y) < 14);

    void UpdateSchools(float dt)
    {
        for (int i = schools.Count - 1; i >= 0; i--)
        {
            var sc = schools[i];
            sc.Age += dt;
            if (sc.Age > sc.Life || scene != "world" || Dist(sc.X, sc.Y, player.X, player.Y) > 260) schools.RemoveAt(i);
        }
        // An echo sounder finds more of them, further out.
        bool sounder = Wears("echo_sounder");
        if (!OverDeepSea || schools.Count >= (sounder ? 3 : 2)) return;
        schoolT -= dt;
        if (schoolT > 0) return;
        schoolT = sounder ? Rand(4, 9) : Rand(6, 13);
        for (int tries = 0; tries < 16; tries++)
        {
            float a = Rand(0, MathF.Tau), d = Rand(40, sounder ? 190 : 110);
            float x = player.X + MathF.Cos(a) * d, y = player.Y + MathF.Sin(a) * d * 0.7f;
            if (TileUnder(x - 8, y) != '~' || TileUnder(x + 8, y) != '~' || TileUnder(x, y - 5) != '~' || TileUnder(x, y + 5) != '~' || InSanctuary(x, y)) continue;
            if (schools.Any(o => Dist(o.X, o.Y, x, y) < 40)) continue;
            schools.Add(new School { X = x, Y = y, Life = Rand(30, 50), Seed = rng.Next(1000) });
            break;
        }
    }

    // Dark, ruffled water where the bait fish ball up, broken by splashes, with birds circling over it by day
    // (one now and then folding its wings to dive).
    void DrawSchools(float t)
    {
        foreach (var sc in schools)
        {
            float a = Math.Min(1, Math.Min(sc.Age / 2, (sc.Life - sc.Age) / 3));
            if (a <= 0) continue;
            for (int k = 0; k < 9; k++)
            {
                float ang = k * 0.7f + sc.Seed, r = 2 + k % 3 * 2.5f;
                float x = sc.X + MathF.Cos(ang + t * 0.8f) * r, y = sc.Y + MathF.Sin(ang + t * 1.1f) * r * 0.5f;
                if (Swimmable(TileUnder(x, y + 1.5f))) pix.Rect(x, y, 2, 1, Pal.Rgba(6, 28, 46, 0.32f * a));
            }
            for (int k = 0; k < 5; k++)
            {
                float cycle = t * 1.4f + k * 0.37f + sc.Seed * 0.013f, ph = cycle % 1;
                int n = (int)cycle;
                float x = sc.X + (float)(Pix.Hash(k, n, sc.Seed) - 0.5) * 16, y = sc.Y + (float)(Pix.Hash(k + 7, n, sc.Seed) - 0.5) * 7;
                if (ph < 0.45f)
                {
                    var c = Pal.Rgba(240, 250, 252, (0.95f - ph * 1.6f) * a);
                    pix.Rect(x, y - ph * 7, 1, 1, c);
                    pix.Rect(x - 1, y, 3, 1, Pal.Rgba(220, 242, 248, (0.7f - ph) * a));
                }
            }
            if (Night) continue;
            for (int b = 0; b < 3; b++)
            {
                float ang = t * (0.8f + b * 0.21f) + b * 2.1f + sc.Seed;
                float dive = (t * 0.25f + b * 0.33f + sc.Seed * 0.007f) % 1;
                float bx = sc.X + MathF.Cos(ang) * (7 + b * 3), by = sc.Y - 14 - b * 3 + MathF.Sin(ang) * 3;
                var gull = Pal.Rgba(242, 239, 230, a);
                if (dive < 0.12f)
                {
                    // Wings folded, dropping on the fish.
                    float k = dive / 0.12f;
                    by += k * (sc.Y - by);
                    pix.Rect(bx, by - 1, 1, 2, gull);
                    if (k > 0.85f) pix.Rect(bx - 1, sc.Y, 3, 1, Pal.Rgba(240, 250, 252, a));
                    continue;
                }
                bool up = (int)(t * 5 + b * 1.7f) % 2 == 0;
                pix.Rect(bx + 2, sc.Y + 2, 4, 1, Pal.Rgba(0, 0, 0, 0.1f * a));
                pix.Rect(bx, by, 2, 1, gull);
                pix.Rect(bx - 2, by + (up ? -1 : 1), 2, 1, gull);
                pix.Rect(bx + 2, by + (up ? -1 : 1), 2, 1, gull);
            }
        }
    }

    /* ---------- Trolling ---------- */
    // A lure trailing behind the boat while you sail slowly. Fish that chase (runners and jumpers) strike it, much
    // sooner when it runs through a feeding frenzy, and only there does Ironbill rise to it. Not saved: a reload winds
    // the line in.
    bool trolling, towing;
    string trollLure;
    float trollT, trollShallowT, towAng;
    (float x, float y) lure, trollDir = (1, 0);
    const string TowFish = "ironbill";

    // The lure: the one picked in the tackle box if it's a lure, else whichever you own.
    string TrollLureOwned()
    {
        string pick = NextBait();
        return pick is "spinner_lure" or "fly_lure" ? pick : Has("spinner_lure") > 0 ? "spinner_lure" : Has("fly_lure") > 0 ? "fly_lure" : null;
    }

    void ToggleTroll()
    {
        if (trolling) { WindIn("You wind the trolling line in."); return; }
        if (race != null) { Sfx.Play("nope"); Toast("Not in the middle of a race!"); return; }
        string l = TrollLureOwned();
        if (l == null)
        {
            Sfx.Play("nope");
            Toast("Trolling needs a lure that's never used up: make a spinner lure at a workbench (one copper bar).", 4);
            return;
        }
        trolling = true; trollLure = l; trollShallowT = 0;
        trollT = Rand(5f, 11f) * Rod.Bite * (Perk(3) ? 0.9f : 1);
        lure = (player.X - trollDir.x * 32, player.Y + 3 - trollDir.y * 24);
        Sfx.Play("cast");
        if (!state.Hinted("troll"))
        {
            state.hinted["troll"] = true;
            Toast("Line out! Keep sailing and something that chases will strike the lure. Steer it through a feeding frenzy for a quicker strike. <alt> winds it in.", 6);
        }
    }

    void WindIn(string msg)
    {
        trolling = false;
        if (msg != null) Toast(msg);
    }

    // In play, at the helm: the lure follows the stern, and a strike comes while you keep moving.
    void UpdateTroll(float dt)
    {
        if (!trolling) return;
        if (!Aboard) { trolling = false; return; }
        if (race != null) { WindIn("You wind the trolling line in for the race."); return; }
        if (player.Moving)
            trollDir = (player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0, player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0);
        // It trails well behind and swings wide round the turns.
        float tx = player.X - trollDir.x * 32, ty = player.Y + 3 - trollDir.y * 24, k = Math.Min(1, dt * 1.8f);
        lure = (lure.x + (tx - lure.x) * k, lure.y + (ty - lure.y) * k);
        trollShallowT = TileUnder(player.X, player.Y) == '~' ? 0 : trollShallowT + dt;
        if (trollShallowT > 1.2f) { WindIn("Too shallow to troll here: you wind the line in before it snags."); return; }
        if (InSanctuary(lure.x, lure.y) || InSanctuary(player.X, player.Y)) { WindIn("You wind the line in: no trolling inside the sanctuary's buoys."); return; }
        if (!player.Moving) return;
        bool frenzy = InSchool(lure.x, lure.y);
        trollT -= dt * (frenzy ? 6 : 1);
        if (trollT <= 0) TrollStrike(frenzy);
    }

    void TrollStrike(bool frenzy)
    {
        string spot = SeaSpotHere;
        trolling = false;
        player.Moving = false;
        FaceToward(lure.x, lure.y);
        var tip = RodTip();
        fish = new FishCast { Spot = spot, Sx = tip.X, Sy = tip.Y, Tx = lure.x, Ty = lure.y, Bx = lure.x, By = lure.y, Bait = trollLure, Power = 1, Depth = 1, T = 1 };
        fish.Roll = RollTroll(spot, frenzy);
        fish.BiteT = BiteWindow;
        towAng = MathF.Atan2((lure.y - player.Y + 4) / 30, (lure.x - player.X) / 46);
        mode = "bite";
        Sfx.Play("bite");
        Burst(lure.x, lure.y, "#ffffff", 8);
        Floater("Strike!", player.X, player.Y - 26, "#ffd76a");
    }

    // Only fish that chase take a trolled lure, and the legend only in a feeding frenzy.
    Catchable RollTroll(string spot, bool frenzy)
    {
        var list = FishWeights(spot, trollLure, 1, trolled: true).Where(p => p.f.Style is "runner" or "jumper" && (!p.f.Legend || frenzy)).ToList();
        if (list.Count == 0) return RollCatch(spot);
        double roll = rng.NextDouble() * list.Sum(p => p.w);
        var pick = list[^1].f;
        foreach (var (f, w) in list)
        {
            if (roll < w) { pick = f; break; }
            roll -= w;
        }
        return new Catchable { Id = pick.Id, Name = pick.Name, Difficulty = pick.Difficulty, Rare = pick.Rare };
    }

    // Ironbill tows the boat: it circles out ahead and hauls the boat after it (hard while it runs or leaps), so fish,
    // line and boat all travel. Against a shore it swings away.
    void UpdateTow(float dt)
    {
        towing = Aboard && mode == "reeling" && reel?.Roll.Id == TowFish && fish != null;
        if (!towing) return;
        bool run = reel.Running > 0 || reel.Leap > 0;
        towAng += dt * (run ? 0.75f : 0.3f);
        float fx = player.X + MathF.Cos(towAng) * 46, fy = player.Y - 4 + MathF.Sin(towAng) * 30;
        fish.Bx = fish.Tx = fx; fish.By = fish.Ty = fy;
        float dx = fx - player.X, dy = fy - player.Y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        float sp = (run ? 40 : 14) * dt, mx = dx / d * sp, my = dy / d * sp;
        bool moved = false;
        if (BoatCanStand(player.X + mx, player.Y)) { player.X += mx; moved = true; }
        if (BoatCanStand(player.X, player.Y + my)) { player.Y += my; moved = true; }
        if (!moved) towAng += dt * 2;
        state.boatX = player.X; state.boatY = player.Y;
        FaceToward(fx, fy);
        boatFace = player.Face;
    }

    // The trolling rod in its holder at the stern, the line streaming back, and the lure skipping in the wake.
    void DrawTroll(int hx, int hy, int rx, int ry, float t)
    {
        pix.Line(hx, hy, rx, ry, "#6b4a2b");
        int n = Math.Max(6, (int)Dist(rx, ry, lure.x, lure.y));
        var line = Pal.Rgba(240, 240, 240, 0.7f);
        for (int i = 0; i <= n; i++)
        {
            float k = (float)i / n;
            pix.Rect(rx + (lure.x - rx) * k, ry + (lure.y - ry) * k + MathF.Sin(MathF.PI * k) * 3, 1, 1, line);
        }
        bool skip = (int)(t * 8) % 2 == 0;
        pix.Rect(lure.x - 1, lure.y - (skip ? 1 : 0), 2, 1, "#d9823f");
        pix.Rect(lure.x, lure.y - (skip ? 1 : 0), 1, 1, "#f2f4f5");
        if (player.Moving) pix.Rect(lure.x - 3 * trollDir.x, lure.y + 1, 2, 1, Pal.Rgba(240, 250, 252, 0.7f));
    }

    /* ---------- The echo sounder ---------- */
    // With one aboard, a ping spreads round the boat now and then, and any feeding frenzy out of view gets a little
    // arrow at the edge of the screen pointing to it (the sea chart marks them too).
    void DrawSonar(float t)
    {
        if (!Aboard || !Wears("echo_sounder")) return;
        float ph = t % 2.6f / 2.6f;
        pix.Ring(player.X, player.Y + 2, 6 + ph * 40, Pal.Rgba(127, 232, 192, 0.35f * (1 - ph)));
        foreach (var sc in schools)
        {
            float sx = sc.X - camX, sy = sc.Y - camY;
            if (sx > 4 && sx < W - 4 && sy > 4 && sy < H - 4) continue;
            float dx = sc.X - player.X, dy = sc.Y - player.Y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            int ax = (int)Math.Clamp(sx, 6, W - 7), ay = (int)Math.Clamp(sy, 6, H - 7);
            var c = Pal.Rgba(127, 232, 192, 0.55f + 0.4f * MathF.Sin(t * 6));
            pix.Fill(ax - 1, ay - 1, 3, 3, c);
            pix.Fill(ax + (int)MathF.Round(dx / d * 3) - 0, ay + (int)MathF.Round(dy / d * 3), 1, 1, c);
            pix.Fill(ax + (int)MathF.Round(dx / d * 4), ay + (int)MathF.Round(dy / d * 4), 1, 1, c);
        }
    }
}
