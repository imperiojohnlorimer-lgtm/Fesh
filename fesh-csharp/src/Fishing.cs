using Raylib_cs;

namespace Fesh;

// Rod fishing from start to finish: charging a cast, waiting (and jigging at the ice hole), the bite and the perfect
// hook, the reel fight in four styles, and landing the fish with its size, records and experience. Also bait, tackle,
// the fishing level, chum, and sunken chests.
partial class Game
{
    const float Bar = 120, JigBeat = 0.9f;
    float charge, chargeDir = 1;
    string chargeSpot;
    bool reelTap;                                       // a fresh press this frame while reeling (leaps and pumps)
    readonly Dictionary<string, float> chumUntil = new(); // spot -> time the chum wears off
    string heldItem;                                    // what you're holding up after a catch, for heldT seconds
    float heldT;

    /* ---------- Who bites ---------- */
    bool Eligible(Creature cr) =>
        !state.Caught(cr.Id)
        && (cr.Req == null || state.Caught(cr.Req))
        && (cr.Time == "any" || (cr.Time == "night") == Night);

    // The moon runs through eight phases, one per day; phase 4 is full. The first night of a new game is a full moon.
    int MoonPhase => (state.day + 3) % 8;
    bool FullMoon => Night && MoonPhase == 4;

    bool Bites(CommonFish f, string bait) =>
        (f.Time == "any" || (f.Time == "night") == Night)
        && (f.Weather == null || (f.Weather == "storm" ? Stormy : f.Weather == "rain" ? state.weather != "clear" : state.weather == "clear"))
        && (!f.FullMoon || FullMoon)
        && (f.Need == null || state.Hinted(f.Need))
        && (f.Season == null || f.Season == Season)
        && (!f.Legend || (state.commons.GetValueOrDefault(f.Id) == 0 && (f.Bait == null || f.Bait == bait)));

    static readonly HashSet<string> FreshSpots = new() { "lagoon", "oasis", "swamp", "icehole", "amihanpond", "bakawanpool" };
    static readonly HashSet<string> SeaSpots = new() { "rocks", "wreck", "deep", "glacier", "mirage", "coral", "dropoff", "starwell", "karstlagoon", "bagareef", "opensea", "amihansea",
        "asinan", "pulo", "parola", "habagatsea" };
    static readonly HashSet<string> CarpLike = new() { "mud_carp", "moon_carp", "old_whiskers", "oasis_tilapia", "parrotfish" };

    // Fish that go for a bait are three times as likely to bite on it.
    static bool BaitLikes(string bait, CommonFish f, string spot) => bait switch
    {
        "worm" => FreshSpots.Contains(spot) || f.Style == "bottom",
        "cricket" => f.Style == "jumper" || f.Depth == "shallow",
        "cut_bait" => SeaSpots.Contains(spot) && f.Style is "runner" or "bottom",
        "glow_shrimp" => f.Depth == "deep",
        "slime_gel" => spot is "cavepool" or "ancientpool",
        "berries" => CarpLike.Contains(f.Id),
        "spinner_lure" => f.Style == "runner",
        "fly_lure" => f.Style == "jumper",
        "tamban" => SeaSpots.Contains(spot) && f.Style is "runner" or "jumper",
        _ => false
    };

    string SpotBiome(string spot) => Data.SpotById[spot].Biome;

    int Luck(string spot, string bait)
    {
        int l = Rod.Luck + (bait != null ? Items.Baits[bait].Luck : 0) + (Stormy ? 1 : 0) + (Wears("lucky_charm") ? 2 : 0) + (Perk(5) ? 1 : 0) + (Gear("hook")?.Luck ?? 0);
        if (Chummed(spot)) l++;
        if (SpotBiome(spot) == "frost" && SetActive("frost")) l++;
        // The sanctuary's fish spilling over into the water just outside it, and Bakunawa's scale under the moon.
        if (fish?.Spill == true) l++;
        if (Night && Wears("moon_charm")) l++;
        if (Wears("gold_star_pin")) l++;
        return l;
    }

    // Every fish that could bite at a spot right now, with its relative chance. Depth is 0 shallow, 1 middle, 2 deep.
    // Trolled is for a lure trolled behind the boat, the only thing Troll fish take.
    List<(CommonFish f, double w)> FishWeights(string spot, string bait, int depth, bool trolled = false)
    {
        int luck = Luck(spot, bait);
        var list = new List<(CommonFish, double)>();
        foreach (var f in Data.Common[spot])
        {
            if (!Bites(f, bait) || f.Troll && !trolled) continue;
            double w = f.Weight;
            if (f.Depth == "shallow") w *= depth == 0 ? 2 : depth == 2 ? 0.5 : 1;
            else if (f.Depth == "deep") w *= depth == 2 ? 2 : depth == 0 ? 0.3 : 1;
            if (bait != null && BaitLikes(bait, f, spot)) w *= 3;
            if (f.Rare) w += luck * (f.Legend ? 0.25 : 1);
            list.Add((f, w));
        }
        return list;
    }

    double ChestChance => Perk(8) ? 0.06 : 0.03;

    Catchable RollCatch(string spot)
    {
        if (spot == "starwell" && TidemaneBites(fish?.Bait)) return new Catchable { Id = "tidemane", Name = "Something enormous", Difficulty = 4.8f, Boss = true };
        var ex = Data.Creatures.FirstOrDefault(c => c.Spot == spot && Eligible(c));
        if (ex != null)
        {
            if (ex.Id == "abyssal" || state.Pity(spot) >= 2 || rng.NextDouble() < 0.45)
                return new Catchable { Id = ex.Id, Name = ex.Name, Difficulty = ex.Difficulty, Exotic = true };
            state.pity[spot] = state.Pity(spot) + 1;
        }
        var odd = Data.Odd.FirstOrDefault(o => o.Spot == spot);
        if (odd != null && rng.NextDouble() < Data.OddChance)
            return new Catchable { Id = odd.Id, Name = odd.Name, Difficulty = odd.Difficulty, Odd = true };
        if (rng.NextDouble() < ChestChance) return new Catchable { Id = "chest", Name = "Sunken chest", Difficulty = 2, Chest = true };
        var list = FishWeights(spot, fish?.Bait, fish?.Depth ?? 1);
        double roll = rng.NextDouble() * list.Sum(p => p.w);
        var pick = list[^1].f;
        foreach (var (f, w) in list)
        {
            if (roll < w) { pick = f; break; }
            roll -= w;
        }
        return new Catchable { Id = pick.Id, Name = pick.Name, Difficulty = pick.Difficulty, Rare = pick.Rare };
    }

    // The chance of each catch at a spot right now, for the fish finder.
    List<(string id, string name, double pct)> CatchOdds(string spot, int depth)
    {
        var res = new List<(string, string, double)>();
        if (spot == "starwell" && TidemaneBites(NextBait())) return new() { ("tidemane", "Something enormous", 1) };
        double left = 1;
        var ex = Data.Creatures.FirstOrDefault(c => c.Spot == spot && Eligible(c));
        if (ex != null)
        {
            double p = ex.Id == "abyssal" || state.Pity(spot) >= 2 ? 1 : 0.45;
            res.Add(("creature", "Something strange", p));
            left -= p;
        }
        if (Data.Odd.FirstOrDefault(o => o.Spot == spot) is OddCatch odd)
        {
            res.Add((odd.Id, state.odd.GetValueOrDefault(odd.Id) > 0 ? odd.Name : "Something odd", left * Data.OddChance));
            left *= 1 - Data.OddChance;
        }
        res.Add(("chest", "Sunken chest", left * ChestChance));
        left *= 1 - ChestChance;
        var list = FishWeights(spot, NextBait(), depth);
        double sum = list.Sum(p => p.w);
        foreach (var (f, w) in list) res.Add((f.Id, state.commons.GetValueOrDefault(f.Id) > 0 ? f.Name : "???", left * w / sum));
        return res.Where(r => r.Item3 > 0.001).OrderByDescending(r => r.Item3).ToList();
    }

    /* ---------- Tackle, bait and the fishing level ---------- */
    // Level from experience: level 2 at 40 XP, 3 at 120, 4 at 240 ... 10 at 1800.
    static int XpFor(int level) => 20 * (level - 1) * level;
    int FishLevel { get { int l = 1; while (l < 10 && state.xp >= XpFor(l + 1)) l++; return l; } }
    bool Perk(int level) => FishLevel >= level;

    public static readonly string[] PerkText =
    {
        "", "", "Reel bar +3", "Bites come 10% faster", "Perfect-hook window +0.1 s", "Rare luck +1",
        "Line holds 20% more tension", "Fish are 10% heavier", "Sunken chests twice as common", "Fish slip away 15% slower", "10% chance of a double catch"
    };

    bool Wears(string id) => Has(id) > 0;

    // The tackle in a slot: the one chosen in the tackle box, or (if never chosen) the best you own. Sinkers are only ever chosen.
    string GearId(string slot)
    {
        if (state.tackle.TryGetValue(slot, out var id))
            return id != null && Items.Tackle.TryGetValue(id, out var t) && t.Slot == slot && Has(id) > 0 ? id : null;
        return slot == "sinker" ? null : Items.TackleOrder[slot].LastOrDefault(i => Has(i) > 0);
    }

    TackleDef Gear(string slot) => GearId(slot) is string id ? Items.Tackle[id] : null;
    float ReelMul => Gear("reel")?.Reel ?? 1;
    float LineMul => (Gear("line")?.Line ?? 1) * (Perk(6) ? 1.2f : 1);
    float SizeMul => (Gear("hook")?.Size ?? 1) * (Perk(7) ? 1.1f : 1) * (SetActive("atoll") ? 1.1f : 1) * (Wears("steward_badge") ? 1.1f : 1);
    float BiteWindow => 1f + (Gear("bobber")?.Window ?? 0);
    float PerfectWindow => 0.25f + (Gear("bobber")?.Window ?? 0) * 0.3f + (Perk(4) ? 0.1f : 0);

    // The bait the next cast uses: the one picked in the tackle box, or glow bait, then plain bait.
    string NextBait()
    {
        string pick = state.tackle.GetValueOrDefault("bait");
        if (pick != null && pick != "auto" && Items.Baits.ContainsKey(pick) && Has(pick) > 0) return pick;
        return Has("glow_bait") > 0 ? "glow_bait" : Has("bait") > 0 ? "bait" : null;
    }

    static string Kg(float kg) => kg >= 10 ? $"{kg:0.0} kg" : $"{kg:0.00} kg";

    void GainXp(int n)
    {
        if (SetActive("mire")) n = (int)MathF.Round(n * 1.15f);
        int before = FishLevel;
        state.xp += n;
        if (FishLevel <= before) return;
        Sfx.Play("rare");
        Floater($"Fishing level {FishLevel}!", player.X, player.Y - 26, "#f3c25b");
        Toast($"Fishing level {FishLevel}! New perk: {PerkText[FishLevel]}.", 4.5f);
    }

    /* ---------- Chum ---------- */
    bool Chummed(string spot) => chumUntil.TryGetValue(spot, out float until) && until > time;

    void ThrowChum(string spotId)
    {
        if (!Take("chum")) return;
        var (sx, sy) = SpotPos(Data.SpotById[spotId]);
        chumUntil[spotId] = time + 120;
        FaceToward(sx, sy);
        Swing("throw", 0.25f);
        Burst(sx, sy, "#8a6440", 10);
        Sfx.Play("splash");
        Toast("Fish crowd in around the chum. For the next two minutes they bite twice as fast, and rare ones a little more often.", 4);
    }

    /* ---------- Casting ---------- */
    Target SpotTarget(Spot s)
    {
        // The bangus pond after the tremor: drained through its cracked bank, then refilling (RestlessSea.cs).
        if (s.Id == "amihanpond" && PondClosed)
            return new Target { Type = "info", Label = PondDry ? "The bangus pond is nearly dry. Its bank cracked in the shaking" : "The pond is filling on the tide. Fish it again tomorrow" };
        if (s.Id == "icehole" && IceFrozen)
            return new Target { Type = "drill", Id = s.Id, Label = PickTier > 0 ? "Break through the ice with your pickaxe" : "Chip a hole through the ice" };
        var t = new Target { Type = "spot", Id = s.Id, Label = s.Id == "icehole" ? s.Action : s.Action + " (hold to cast further)" };
        if (Has("chum") > 0 && !Chummed(s.Id)) { t.AltType = "chum"; t.AltLabel = "Throw chum"; }
        if (Data.ReefSpots.Contains(s.Id) && Has("spear") > 0) { t.Alt2Type = "spear"; t.Alt2Label = "Spearfish"; }
        return t;
    }

    void FaceToward(float x, float y)
    {
        float dx = x - player.X, dy = y - player.Y;
        player.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
    }

    // The rod tip: pulled back over your shoulder while you charge a cast, bending toward the fish while you reel.
    (float X, float Y) RodTip()
    {
        int dir = player.Face == "left" ? -1 : 1;
        // Where the body is: standing, up in the saddle, or sitting at the boat's stern. From the boat the rod reaches
        // out low past the bow (or, held up or down, stays short of the mast), so it doesn't read as rigging.
        float bx = player.X, up = player.Y - 15 - (Riding ? 8 : 0), reach = 7;
        if (Riding)
        {
            var seat = RiderSeat((int)MathF.Round(player.X), (int)MathF.Round(player.Y));
            bx = seat.x; up = seat.y - 15;
        }
        if (Aboard)
        {
            bool sideways = player.Face is "left" or "right";
            var seat = BoatSeat();
            bx = seat.x; up = seat.y - (sideways ? 12 : 15); reach = sideways ? 14 : 4;
        }
        float forward = bx + dir * reach;
        if (mode == "charging") return (bx - dir * (1 + charge * 5), up - 2 - charge * 3);
        if (mode == "casting" && fish != null && fish.T < 0.25f)
        {
            float k = fish.T / 0.25f;
            return (bx - dir * 6 + (forward - bx + dir * 6) * k, up - 5 + 5 * k);
        }
        if (mode == "reeling") return (forward + dir * MathF.Sin(time * 23) * 0.6f, up + 3 + (reel?.Running > 0 ? 2 : 0));
        if (mode == "bite") return (forward, up + 1);
        return (forward, up);
    }

    // Hold E (or the mouse) to power up a cast; let go to throw. A short cast lands in the shallows, a long one in deep water.
    void StartCharge(string spotId)
    {
        chargeSpot = spotId;
        charge = 0;
        chargeDir = 1;
        var (sx, sy) = SpotPos(Data.SpotById[spotId]);
        FaceToward(sx, sy);
        mode = "charging";
        if (!state.Hinted("powerCast"))
        {
            state.hinted["powerCast"] = true;
            Toast("Hold <act> to power up your cast, and let go to throw. Short casts reach shallow fish, long casts deep ones.", 5);
        }
    }

    int CastDepth(float power) => Math.Clamp((power < 0.35f ? 0 : power < 0.75f ? 1 : 2) + (Gear("sinker")?.Depth ?? 0) + (InWater ? 1 : 0), 0, 2);
    static string DepthName(int d) => d == 0 ? "shallow water" : d == 1 ? "middle water" : "deep water";

    static bool WaterTile(char t) => t is '~' or 'w' or 'l' or 'T' or 'o' or 'm' or 'P' or 'k';
    bool IsWater(float x, float y) => WaterTile(TileAt((int)MathF.Floor(x / T), (int)MathF.Floor(y / T)));

    // Where the bobber lands: along the line from you to the spot, further the harder you cast, but always in water.
    (float x, float y) LandingPoint(Spot s, float power)
    {
        var (sx, sy) = SpotPos(s);
        if (s.Id == "icehole") return (sx, sy);
        float px = player.X, py = player.Y - 6, dx = sx - px, dy = sy - py, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        float reach = Math.Clamp(d * (0.4f + power * 0.95f), 12, d + s.R * 0.6f);
        float lx = px + dx / d * reach, ly = py + dy / d * reach;
        for (int i = 0; i < 40 && !IsWater(lx, ly); i++) { lx += (sx - lx) * 0.2f; ly += (sy - ly) * 0.2f; }
        if (!IsWater(lx, ly)) (lx, ly) = (sx, sy);
        return (lx, ly);
    }

    // Fish shadows that drift around a spot. With polarized sunglasses you can see them; a cast that lands on one
    // gets a quick bite, and the big one (when it's there) gives a heavier fish.
    (float x, float y, bool big)? ShadowPos(Spot s, int k, float t)
    {
        if (s.Id == "icehole" || s.Scene == "sea") return null;
        var (sx, sy) = SpotPos(s);
        float w = 0.22f + k * 0.07f, ph = k * 1.9f + s.R * 0.37f;
        float x = sx + MathF.Cos(t * w + ph) * s.R * 0.5f, y = sy + MathF.Sin(t * w * 1.3f + ph) * s.R * 0.28f;
        if (!IsWater(x, y)) return null;
        return (x, y, k == 0 && Pix.Hash((int)(t / 25), s.Id.Length, 61) < 0.5);
    }

    void Cast(string spotId, float power = 0.6f)
    {
        var s = Data.SpotById[spotId];
        var (spotX, spotY) = SpotPos(s);
        FaceToward(spotX, spotY);
        var (lx, ly) = LandingPoint(s, power);
        // From just outside the sanctuary, a long cast toward it falls short of the buoys; if it can't, there's no cast.
        for (int i = 0; i < 40 && scene == "world" && InSanctuary(lx, ly); i++) { lx += (player.X - lx) * 0.15f; ly += (player.Y + 3 - ly) * 0.15f; }
        if (scene == "world" && InSanctuary(lx, ly))
        {
            mode = "play";
            Sfx.Play("nope");
            Toast("No fishing in the marine sanctuary. Turn away from the buoys.");
            return;
        }
        state.casts++;
        var tip = RodTip();
        bool baitBox = scene == "world" && state.builds.Any(b => b.id == "baitbox" && Dist(player.X, player.Y, b.x * T + 5, b.y * T + 6) < 40);
        string bait = NextBait();
        if (bait != null && !Items.Baits[bait].Reusable) Take(bait);
        if (bait != null && !state.Hinted("usedBait"))
        {
            state.hinted["usedBait"] = true;
            Toast($"Used 1 {Items.ById[bait].Name.ToLowerInvariant()}. {Has(bait)} left.", 2.4f);
        }
        bool onShadow = false, big = false;
        for (int k = 0; k < 4; k++)
            if (ShadowPos(s, k, time) is (float hx, float hy, bool hb) && Dist(lx, ly, hx, hy) < 8) { onShadow = true; big |= hb; }
        if (s.Scene == "sea" && InSchool(lx, ly)) onShadow = true;
        bool spill = scene == "world" && InSpillover(lx, ly);
        float wait = (baitBox ? Rand(0.5f, 1.2f) : Rand(1.6f, 3.6f)) * Rod.Bite * WeatherBite * (bait != null ? Items.Baits[bait].Bite : 1)
            * (Perk(3) ? 0.9f : 1) * (Chummed(spotId) ? 0.5f : 1) * (onShadow ? 0.55f : 1) * (spotId == "icehole" ? 1.6f : 1)
            * (s.Biome == "saltmere" && s.Scene != "sea" && SetActive("saltmere") ? 0.85f : 1) * (spill ? 0.8f : 1);
        fish = new FishCast
        {
            Spot = spotId, Sx = tip.X, Sy = tip.Y, Tx = lx, Ty = ly, Bx = tip.X, By = tip.Y, Bait = bait, Power = power,
            Depth = CastDepth(power), BigShadow = big, Timer = wait, Spill = spill
        };
        if (spill && !state.Hinted("spillover"))
        {
            state.hinted["spillover"] = true;
            Toast("Fish spill out of the sanctuary into the water round it: bites come quicker here, and rare fish more often.", 5);
        }
        mode = "casting";
        Sfx.Play("cast");
    }

    void ReelIn(string msg)
    {
        fish = null; reel = null; pointerHold = false;
        mode = "play";
        Toast(msg);
    }

    /* ---------- The ice hole ---------- */
    bool IceFrozen => state.iceDay != state.day;
    float drill;

    void StartDrill()
    {
        drill = 0;
        FaceToward(605, 85);
        mode = "drill";
        Sfx.Play("mine");
    }

    // Each press chips at the ice; the crack closes up again if you stop. A pickaxe is much quicker.
    void DrillHit()
    {
        drill += PickTier > 0 ? 0.11f : 0.065f;
        Swing("pick", 0.2f);
        shakeT = 0.15f;
        Burst(605, 85, "#e8f6fb", 4);
        Sfx.Play("mine");
        if (drill < 1) return;
        state.iceDay = state.day;
        mode = "play";
        Burst(605, 85, "#9fd3e6", 12);
        Sfx.Play("splash");
        Toast("The ice cracks open! The hole stays clear until it freezes over tonight. At the ice hole, jig your line on the beat for a quicker bite.", 5);
        Save();
    }

    // At the ice hole, E jigs the line. On the beat (as the ring closes) the fish get curious; off the beat it scares them.
    void Jig()
    {
        float ph = fish.WaitT % JigBeat, off = MathF.Min(ph, JigBeat - ph);
        if (off < 0.15f)
        {
            fish.Timer -= 0.75f;
            fish.Jigs++;
            Floater(fish.Jigs >= 3 ? $"Jig x{fish.Jigs}!" : "Jig!", fish.Bx, fish.By - 8, "#bff4ff");
            Sfx.Play("blip");
        }
        else
        {
            fish.Timer += 0.5f;
            fish.Jigs = 0;
            Floater("Too jerky", fish.Bx, fish.By - 8, "#ff9a8a");
            Sfx.Play("nope");
        }
    }

    /* ---------- The bite and the fight ---------- */
    // Hooking right as the bobber goes under is a perfect hook: the fight starts half won, and the fish is a bit bigger.
    void Hook()
    {
        bool perfect = BiteWindow - fish.BiteT < PerfectWindow;
        StartReel(perfect);
    }

    void StartReel(bool perfect = false)
    {
        var roll = fish.Roll;
        if (roll.Chest) { StartChest(); return; }
        var f = Data.FishById.GetValueOrDefault(roll.Id);
        float zoneH = roll.Exotic ? (roll.Id == "abyssal" ? 28 : 30) : roll.Rare || roll.Boss ? 30 : roll.Odd ? 32 : 34;
        zoneH += Rod.Zone + (Perk(2) ? 3 : 0) + (SetActive("cave") ? 3 : 0) - (Starving ? 6 : 0);
        reel = new ReelState
        {
            ZoneH = zoneH, ZoneY = Bar - zoneH, FishY = 70, FishTarget = 50, FishTimer = 0.3f, Progress = perfect ? 0.5f : 0.35f,
            Diff = roll.Difficulty, Exotic = roll.Exotic, Roll = roll, Perfect = perfect, Style = f?.Style ?? "dart",
            RunT = Rand(1.6f, 2.6f), LeapT = Rand(1.6f, 2.4f), LeapLen = 1.1f - Math.Min(0.3f, roll.Difficulty * 0.07f)
        };
        if (perfect)
        {
            state.perfects++;
            Floater("Perfect hook!", player.X, player.Y - 24, "#f3c25b");
            Sfx.Play("coin");
        }
        mode = "reeling";
        if (roll.Boss)
        {
            // It fights every way there is, switching every few seconds, and it takes a long time to bring in.
            reel.Pull = 0.6f;
            reel.StyleT = 4.5f;
            Sfx.Play("neigh");
            quake = 0.4f;
            Toast("Something enormous takes the coconut and the line screams off the reel! It fights every way a fish can.", 5);
            return;
        }
        if (roll.Id == TowFish)
        {
            // Ironbill: a long fight while it tows the boat (UpdateTow).
            reel.Pull = 0.7f;
            quake = 0.3f;
            Sfx.Play("splash");
            Toast("Ironbill! The legend takes the lure and tows your boat away. Hang on!", 5);
            return;
        }
        string hint = reel.Style switch
        {
            "runner" => "This one runs! When it does, let go of the reel, or the line will snap.",
            "jumper" => "A jumper! When it leaps, press <act> as the marker crosses the gold.",
            "bottom" => "It's hugging the bottom. Tap <act> in short pumps; holding too long lets it dig in.",
            _ => null
        };
        if (hint != null && !state.Hinted("style:" + reel.Style))
        {
            state.hinted["style:" + reel.Style] = true;
            Toast(hint, 4.5f);
        }
    }

    void GotAway(string msg)
    {
        if (mode == "reeling" && fish?.Roll is { Boss: false, Exotic: false, Odd: false, Chest: false } seen) MarkSeen(seen.Id);
        if (fish?.Roll is { Exotic: true }) state.pity[fish.Spot] = 2;
        if (fish?.Roll is { Boss: true })
        {
            msg = "It tears free and sinks back into the Starwell. It'll rise again for another coconut.";
            quake = 0.3f;
        }
        fish = null; reel = null; pointerHold = false;
        mode = "play";
        Sfx.Play("fail");
        Toast(msg);
    }

    void UpdateFishing(float dt)
    {
        switch (mode)
        {
            case "charging":
                if (ReelHeld())
                {
                    charge += chargeDir * dt * 1.15f;
                    if (charge >= 1) { charge = 1; chargeDir = -1; }
                    if (charge <= 0) { charge = 0; chargeDir = 1; }
                    SetPrompt($"Let go to cast into {DepthName(CastDepth(charge))}");
                }
                else Cast(chargeSpot, charge);
                return;
            case "casting":
            {
                var f = fish;
                f.T = Math.Min(1, f.T + dt / 0.55f);
                f.Bx = f.Sx + (f.Tx - f.Sx) * f.T;
                f.By = f.Sy + (f.Ty - f.Sy) * f.T - MathF.Sin(MathF.PI * f.T) * (10 + f.Power * 14);
                SetPrompt("");
                if (f.T >= 1)
                {
                    f.Bx = f.Tx; f.By = f.Ty;
                    mode = "waiting";
                    Sfx.Play("splash");
                    Burst(f.Bx, f.By, "#cfe8ee", 6);
                }
                return;
            }
            case "waiting":
                fish.Timer -= dt;
                fish.WaitT += dt;
                SetPrompt(fish.Spot == "icehole" ? "Jig with [<act>] each time the ring closes. [Esc] reel in" : "Waiting for a bite... [<act>] reel in");
                if (fish.Timer <= 0)
                {
                    if (fish.Spot == "starwell" && !state.tamed && Night && fish.Bait != "coconut" && !state.Hinted("sniffed"))
                    {
                        state.hinted["sniffed"] = true;
                        Toast("Something huge circles under your bobber, noses at the bait, and turns away. It wants something sweeter.", 5);
                    }
                    fish.Roll = RollCatch(fish.Spot);
                    fish.BiteT = BiteWindow;
                    mode = "bite";
                    Sfx.Play("bite");
                    Burst(fish.Bx, fish.By, "#ffffff", 5);
                }
                return;
            case "bite":
                fish.BiteT -= dt;
                SetPrompt((BiteWindow - fish.BiteT < PerfectWindow ? "Bite! Hook it NOW for a perfect hook!" : "Bite! Hook it!") + FieldGuideName(), "<act>", true);
                if (fish.BiteT <= 0) GotAway("Too slow. It got away.");
                return;
            case "reeling":
                if (reel != null) UpdateReel(dt);
                return;
            case "chest": UpdateChest(dt); return;
            case "spear": UpdateSpear(dt); return;
            case "drill":
                drill = MathF.Max(0, drill - dt * 0.12f);
                SetPrompt("Mash [<act>] to break through the ice!   [Esc] stop", null, true);
                return;
        }
    }

    void UpdateReel(float dt)
    {
        var r = reel;
        bool hold = ReelHeld(), tap = reelTap;
        reelTap = false;
        if (UpdateFishAttack(r, dt, hold)) return;
        if (r.Roll.Boss)
        {
            // Tidemane switches how it fights every few seconds (never in the middle of a run or a leap).
            r.StyleT -= dt;
            if (r.StyleT <= 0 && r.Leap <= 0 && r.Running <= 0)
            {
                var styles = new[] { "dart", "runner", "jumper", "bottom" }.Where(s => s != r.Style).ToArray();
                r.Style = styles[rng.Next(styles.Length)];
                r.StyleT = Rand(4f, 6f);
                r.HoldT = 0; r.Digging = false; r.Tension = Math.Min(r.Tension, 0.3f);
                r.RunT = Rand(0.6f, 1.2f); r.LeapT = Rand(0.6f, 1.2f);
                string shout = r.Style switch { "runner" => "It's bolting!", "jumper" => "It's leaping!", "bottom" => "It's diving deep!", _ => "It's thrashing!" };
                Floater(shout, player.X, player.Y - 30, "#ffd76a");
                Sfx.Play("splash");
            }
        }
        bool leaping = r.Leap > 0;
        if (!leaping)
        {
            r.ZoneV += (hold ? -360 : 270) * dt;
            r.ZoneV = Math.Clamp(r.ZoneV, -130, 150);
            r.ZoneY += r.ZoneV * dt;
            if (r.ZoneY < 0) { r.ZoneY = 0; r.ZoneV = Math.Max(0, r.ZoneV); }
            if (r.ZoneY > Bar - r.ZoneH) { r.ZoneY = Bar - r.ZoneH; r.ZoneV = r.ZoneV > 60 ? -r.ZoneV * 0.3f : 0; }
        }

        float fs = 16 + r.Diff * 16;
        r.FishTimer -= dt;
        void Dart()
        {
            if (r.FishTimer <= 0)
            {
                r.FishTarget = Rand(4, Bar - 4);
                r.FishTimer = Rand(0.5f, 1.4f) / (0.6f + r.Diff * 0.2f);
            }
            if (r.Diff >= 3 && rng.NextDouble() < dt * 0.8) r.FishTarget = Math.Clamp(r.FishY + Rand(-40, 40), 4, Bar - 4);
        }
        string prompt = "Hold [<act>] to lift the green bar. Keep the fish inside.";
        bool urgent = false;
        float gain = 0.3f;
        switch (r.Style)
        {
            case "runner":
                // Every few seconds it makes a run for the bottom. Holding on during a run builds tension until the line snaps.
                if (r.Running > 0)
                {
                    r.Running -= dt;
                    r.FishTarget = Bar - 4;
                    fs *= 2.2f;
                    if (r.Running <= 0) r.RunT = Rand(1.6f, 3.2f);
                    prompt = "It's running! Let go so the line doesn't snap!";
                    urgent = true;
                }
                else
                {
                    Dart();
                    r.RunT -= dt;
                    if (r.RunT <= 0) { r.Running = Rand(0.8f, 1.3f) + r.Diff * 0.1f; Sfx.Play("splash"); }
                    prompt = "Hold [<act>] to lift the bar. Let go when it runs.";
                }
                r.Tension += r.Running > 0 && hold ? dt * (0.75f + r.Diff * 0.08f) / LineMul : -dt * (r.Running > 0 ? 0.25f : 0.55f);
                r.Tension = Math.Clamp(r.Tension, 0, 1);
                if (r.Tension >= 1) { GotAway("Snap! The line broke. Let go of the reel when a fish runs."); return; }
                break;
            case "jumper":
                // Now and then it leaps clear of the water. Press as the marker crosses the gold to keep it hooked.
                if (leaping)
                {
                    r.Leap -= dt;
                    r.LeapMark = 1 - r.Leap / r.LeapLen;
                    if (tap && !r.LeapDone)
                    {
                        r.LeapDone = true;
                        bool good = r.LeapMark >= 0.6f && r.LeapMark <= 0.85f;
                        r.Progress += good ? 0.14f * r.Pull : -0.08f;
                        Floater(good ? "Nice!" : "Missed!", player.X, player.Y - 24, good ? "#7fd36b" : "#ff9a8a");
                        Sfx.Play(good ? "coin" : "nope");
                    }
                    if (r.Leap <= 0)
                    {
                        if (!r.LeapDone) { r.Progress -= 0.08f; Floater("It shook its head!", player.X, player.Y - 24, "#ff9a8a"); }
                        r.LeapT = Rand(2.2f, 3.6f);
                    }
                    prompt = "It leaps! Press [<act>] as the marker hits the gold!";
                    urgent = true;
                }
                else
                {
                    Dart();
                    r.LeapT -= dt;
                    if (r.LeapT <= 0) { r.Leap = r.LeapLen; r.LeapMark = 0; r.LeapDone = false; Sfx.Play("splash"); }
                    prompt = "Hold [<act>] to lift the bar. Time a press when it leaps.";
                }
                break;
            case "bottom":
                // It sulks near the bottom. Short pumps haul it up; hold too long and it digs in.
                r.HoldT = hold ? r.HoldT + dt : 0;
                r.Digging = r.HoldT > 0.55f;
                if (r.FishTimer <= 0) { r.FishTarget = Rand(Bar * 0.5f, Bar - 4); r.FishTimer = Rand(0.8f, 1.6f); }
                if (r.Digging) r.FishTarget = Bar - 4;
                fs *= 0.7f;
                gain = 0.16f;
                if (tap && r.FishY >= r.ZoneY - 4 && r.FishY <= r.ZoneY + r.ZoneH + 4)
                {
                    r.Progress += 0.05f * ReelMul * r.Pull;
                    Sfx.Reel();
                }
                prompt = r.Digging ? "It's digging in! Let go, then tap [<act>] in short pumps" : "Tap [<act>] in short pumps to haul it up.";
                urgent = r.Digging;
                break;
            default:
                Dart();
                break;
        }
        if (!leaping) r.FishY += Math.Clamp(r.FishTarget - r.FishY, -fs * dt, fs * dt);
        r.Inside = !leaping && r.FishY >= r.ZoneY && r.FishY <= r.ZoneY + r.ZoneH;
        if (!leaping)
        {
            float loss = (0.13f + r.Diff * 0.035f) * (Perk(9) ? 0.85f : 1) + (r.Digging ? 0.1f : 0);
            r.Progress += (r.Inside ? gain * ReelMul * r.Pull : -loss) * dt;
        }
        if (hold) { r.Tick += dt; if (r.Tick > 0.08f) { r.Tick = 0; Sfx.Reel(); } }
        SetPrompt(prompt, null, urgent);
        if (r.Progress >= 1) LandCatch();
        else if (r.Progress <= 0) GotAway("The line went slack. It got away.");
    }

    /* ---------- Landing a fish ---------- */
    string legendId = "ancient_coelacanth";

    void CloseLegend()
    {
        if (mode != "legend" || Raylib.GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        // The coelacanth is landed, and the thing that hunts it rises (Guardian.cs), until you've driven it off.
        // The target is still the pool from before the card, so a press next frame would cast instead (Codex): clear it.
        if (legendId == "ancient_coelacanth" && OnAncientFloor && !GuardianBeaten) { guardianDue = true; target = null; return; }
        heldItem = legendId;
        heldT = 2f;
        Toast($"The {Data.FishById[legendId].Name} is in your bag. It would look magnificent in an aquarium.", 4);
    }

    void LandCatch()
    {
        if (reel.Roll.Boss) { BossBreach(); return; }
        var roll = reel.Roll;
        string spot = fish.Spot;
        float waterX = fish.Bx, waterY = fish.By;
        float bonus = (reel.Perfect ? 1.1f : 1) * (fish.BigShadow ? 1.15f : 1);
        bool perfect = reel.Perfect;
        fish = null; reel = null; pointerHold = false;
        if (roll.Exotic)
        {
            state.caught.Add(roll.Id);
            state.pity[spot] = 0;
            GainXp(30);
            Save();
            Sfx.Play("rare");
            ShowCatch(roll.Id);
            return;
        }
        if (roll.Odd)
        {
            state.odd[roll.Id] = state.odd.GetValueOrDefault(roll.Id) + 1;
            GainXp(8);
            Save();
            Sfx.Play("odd");
            ShowOdd(roll.Id, spot);
            return;
        }
        var f = Data.FishById[roll.Id];
        // A fish you let go here before may be back, grown (Release.cs).
        var back = f.Legend ? null : Returning(f.Id, spot);
        // Back grown: big, and a fifth again on what it weighed when you let it go.
        float kg = AddCatch(f, bonus, perfect, back != null ? MathF.Max(1.3f, back.kg / f.Kg * 1.2f) : 0);
        float landedM = lastM;
        int n = state.commons[f.Id];
        if (f.Legend)
        {
            legendId = f.Id;
            Sfx.Play("rare");
            catchOpenedAt = Raylib.GetTime();
            mode = "legend";
            SetPrompt("");
            Save();
            return;
        }
        heldItem = f.Id;
        heldT = 1.4f;
        // A first catch points you at its Fish log card, which tells you about the real fish (FishFacts).
        string firstNote = n == 1 ? " New in your Fesh-dex (<dex>): read about the real fish." : $" ({n} so far)";
        if (f.Rare)
        {
            Sfx.Play("rare");
            Toast($"Rare catch! A {f.Name.ToLowerInvariant()}, {Kg(kg)}!{firstNote}", n == 1 ? 4.5f : 3.5f);
        }
        else
        {
            Sfx.Play("catch");
            Toast($"Caught a {f.Name.ToLowerInvariant()}, {Kg(kg)}!{firstNote}", n == 1 ? 4 : 2.6f);
        }
        if (Perk(10) && !f.Rare && rng.NextDouble() < 0.1)
        {
            AddCatch(f);
            Floater("Double catch!", player.X, player.Y - 34, "#7fd36b");
        }
        mode = "play";
        bool bagHint = !f.Rare && !state.Hinted("fishBag");
        if (bagHint)
        {
            state.hinted["fishBag"] = true;
            Toast($"Caught a {f.Name.ToLowerInvariant()}, {Kg(kg)}! It's in your bag (<bag>). Cook fish at a campfire with <alt>.", 4.5f);
        }
        if (back != null)
        {
            state.grownBack++;
            Floater("The one you let go!", player.X, player.Y - 40, "#9fe0b0");
            Toast($"It's the {f.Name.ToLowerInvariant()} you let go on day {back.day}, back and grown: {Kg(kg)} (it was {Kg(back.kg)}).", 5);
            CheckBadges();
        }
        // For a few seconds you can let it go (F). (A double catch rolled its own size: this is the first one's.)
        lastM = landedM;
        NoteLanded(f, spot, kg, waterX, waterY, bagHint || back != null);
        Save();
        MaybeHint(spot);
    }

    // Puts a fish in your bag and records it: its weight, your records and trophies, experience and the derby.
    // Big fish (a quarter over the usual size) sell for more; trophies are half again over.
    // MinM is a size it's at least (a fish you let go, back and grown: Release.cs).
    float AddCatch(CommonFish f, float bonus = 1, bool perfect = false, float minM = 0)
    {
        float m = MathF.Max(minM, (0.55f + MathF.Pow((float)rng.NextDouble(), 1.8f) * 1.15f) * SizeMul * bonus);
        lastM = m;
        float kg = MathF.Max(0.01f, MathF.Round(f.Kg * m * 100) / 100);
        bool first = state.commons.GetValueOrDefault(f.Id) == 0;
        state.commons[f.Id] = state.commons.GetValueOrDefault(f.Id) + 1;
        Give(f.Id);
        if (m >= 1.25f) state.big[f.Id] = state.big.GetValueOrDefault(f.Id) + 1;
        bool trophy = m >= 1.5f;
        if (trophy) state.trophies[f.Id] = state.trophies.GetValueOrDefault(f.Id) + 1;
        bool record = kg > state.records.GetValueOrDefault(f.Id);
        if (record) state.records[f.Id] = kg;
        float fx = player.X, fy = player.Y - 26;
        Floater(Kg(kg), fx, fy, m >= 1.25f ? "#f3c25b" : "#ffffff");
        if (trophy) Floater("Trophy!", fx, fy - 7, "#f3c25b");
        else if (record && !first) Floater("New record!", fx, fy - 7, "#7fd36b");
        GainXp(4 + (int)MathF.Round(f.Difficulty * 4) + (f.Rare ? 8 : 0) + (f.Legend ? 60 : 0) + (perfect ? 3 : 0) + (trophy ? 5 : 0));
        DerbyCatch(f, kg);
        FestivalCatch(f, kg);
        if (first) { CheckPageDone(f.Id); CheckAlbum(f.Id); }
        // The fish album, mentioned once you've a few pages in it.
        if (first && !state.Hinted("albumHint") && AlbumChapters.Sum(ch => ch.Fish.Count(InAlbum)) >= 5)
        {
            state.hinted["albumHint"] = true;
            ToastLater("Your fish album has five fish taped in: their families, the water they live in and what they eat. Open it from the Fesh-dex (<dex>).");
        }
        return kg;
    }

    // How much Pip pays for one, with your cooler and the desert collection.
    int PriceOf(string id)
    {
        int p = Items.SellPrice(id);
        if (Items.ById[id].Kind != "fish") return p;
        float k = (Wears("cooler") ? 1.25f : 1) * (SetActive("dunes") ? 1.1f : 1);
        return (int)MathF.Round(p * k);
    }

    // Selling n of a fish: the big ones go first, at 60% more.
    int SaleValue(string id, int n)
    {
        int big = Math.Min(Math.Min(n, BigCount(id)), n), price = PriceOf(id);
        return big * (int)MathF.Round(price * 1.6f) + (n - big) * price;
    }

    int BigCount(string id) => Math.Min(state.big.GetValueOrDefault(id), Has(id));

    void SoldFish(string id, int n)
    {
        Learned("sell");
        int big = Math.Min(n, BigCount(id));
        if (big > 0) state.big[id] = state.big.GetValueOrDefault(id) - big;
    }

    /* ---------- Sunken chests ---------- */
    List<int> chestSeq;
    int chestAt;
    float chestT, chestShake;
    static readonly string[] ArrowNames = { "Up", "Right", "Down", "Left" };

    // A chest on the line: press the arrows in order before it slips off.
    void StartChest()
    {
        int n = 5 + Math.Min(2, FishLevel / 4);
        chestSeq = Enumerable.Range(0, n).Select(_ => rng.Next(4)).ToList();
        chestAt = 0;
        chestT = 4.5f + n * 0.35f;
        reel = null;
        mode = "chest";
        Sfx.Play("odd");
        if (!state.Hinted("chest")) { state.hinted["chest"] = true; Toast("A sunken chest! Press the direction keys (<move>) in order to haul it up.", 4); }
    }

    int? ArrowPressed()
    {
        if (Bind.Pressed("up")) return 0;
        if (Bind.Pressed("right")) return 1;
        if (Bind.Pressed("down")) return 2;
        if (Bind.Pressed("left")) return 3;
        return null;
    }

    void UpdateChest(float dt)
    {
        chestT -= dt;
        chestShake = Math.Max(0, chestShake - dt);
        SetPrompt("Haul it up: press the arrows in order!", null, true);
        if (ArrowPressed() is int d)
        {
            if (d == chestSeq[chestAt])
            {
                chestAt++;
                Sfx.Reel();
                if (chestAt >= chestSeq.Count) { OpenChest(); return; }
            }
            else
            {
                chestT -= 0.8f;
                chestShake = 0.3f;
                Sfx.Play("nope");
            }
        }
        if (chestT <= 0) GotAway("The chest slipped off the hook and sank back into the dark.");
    }

    void OpenChest()
    {
        fish = null; reel = null;
        mode = "play";
        state.chests++;
        int coins = 20 + rng.Next(61);
        state.coins += coins;
        var pool = new (string id, int n)[] { ("bait", 5), ("glow_bait", 3), ("copper_bar", 2), ("iron_bar", 2), ("gold_bar", 1), ("crystal", 1), ("seaweed", 3), ("pearl", 1), ("worm", 4), ("chum", 2) };
        var loot = new List<(string id, int n)>();
        while (loot.Count < 2)
        {
            var p = pool[rng.Next(pool.Length)];
            if (!loot.Any(l => l.id == p.id)) loot.Add(p);
        }
        if (Has("golden_hook") == 0 && rng.NextDouble() < 0.15) loot.Add(("golden_hook", 1));
        foreach (var (id, n) in loot) Give(id, n);
        heldItem = "chest";
        heldT = 1.8f;
        Burst(player.X, player.Y - 20, "#f3c25b", 14);
        Sfx.Play("rare");
        GainXp(15);
        Toast($"The chest creaks open: {coins} coins, {string.Join(" and ", loot.Select(l => Items.Amount(l.id, l.n)))}!", 4.5f);
        Save();
    }

    /* ---------- Floating text ---------- */
    sealed class FloatText { public string Text; public float X, Y, T; public Color C; }
    readonly List<FloatText> floaters = new();

    // A word that rises from a point in the world and fades: weights, "Perfect hook!", "+1 worm" and so on.
    void Floater(string text, float wx, float wy, string col = "#ffffff")
    {
        int near = floaters.Count(f => f.T < 0.5f && MathF.Abs(f.X - wx) < 20 && MathF.Abs(f.Y - wy) < 12);
        floaters.Add(new FloatText { Text = text, X = wx, Y = wy - near * 7, C = Pal.C(col) });
    }

    void UpdateFloaters(float dt)
    {
        foreach (var f in floaters) f.T += dt;
        floaters.RemoveAll(f => f.T > 1.5f);
    }
}
