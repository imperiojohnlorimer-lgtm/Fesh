#if DEBUG
using Raylib_cs;

namespace Fesh;

// Maya's guso farm and Bakawan's night lights (1.15). FESH_GUSO_TEST=1 runs only this (it still needs FESH_AUTOTEST
// and FESH_SAVE); the full play-through runs it at the end.
partial class Game
{
    IEnumerable<int> GusoScript()
    {
        Note("Maya's guso farm");
        Inp.ScriptMouse = Offscreen;
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Guso tester" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        state.loose.RemoveAll(l => l.kind == "worm");
        state.hinted["amihan"] = true; state.day = 2;
        foreach (var r in new[] { "amihan:Luntian Karsts", "amihan:Bakawan Island", "amihan:Amihan Village" }) if (!state.charted.Contains(r)) state.charted.Add(r);
        player.X = 2045; player.Y = 145; player.Face = "down";
        yield return 3;
        animals.Clear();

        /* ---------- The lines ---------- */
        bool OverWater(int i) => Enumerable.Range((int)GusoLines[i].y0, (int)(GusoLines[i].y1 - GusoLines[i].y0) + 1).All(y => ShapePx((int)GusoLines[i].x, y) == 'l');
        Check("all four guso lines stand in the lagoon's water", Enumerable.Range(0, GusoLines.Length).All(OverWater));
        var reach = Reachable();
        // Stands on reachable sand beside line i, facing it.
        bool AtLine(int i)
        {
            var (x, y0, y1) = GusoLines[i];
            string face = i < 2 ? "right" : "left";
            var spots = new List<(float x, float y)>();
            for (float py = y0 - 2; py <= y1 + 4; py++)
                for (float px = x - 16; px <= x + 16; px++) spots.Add((px, py));
            foreach (var (px, py) in spots.OrderBy(p => Math.Abs(p.y - (y0 + y1) / 2) + Math.Abs(Math.Abs(p.x - x) - 11)))
            {
                if (!CanStand(px, py) || !reach.Contains(((int)MathF.Floor(px / T), (int)MathF.Floor((py - 1.5f) / T)))) continue;
                player.X = px; player.Y = py; player.Face = face;
                if (GusoLineInFront() == i) return true;
            }
            return false;
        }
        Check("every line can be faced from sand you can walk to", Enumerable.Range(0, GusoLines.Length).All(AtLine));
        AtLine(0); yield return 2;
        Check($"before Maya's told you, a line is just hers ({target?.Label})", target?.Type == "info" && target.Label.Contains("Ask Maya"));
        // The lagoon still fishes from its north shore, and from the sand right beside the lines when you face away.
        player.X = 2050; player.Y = 196; player.Face = "up"; yield return 2;
        Check($"the lagoon still fishes from its south shore ({target?.Label})", target?.Type == "spot" && target.Id == "karstlagoon");
        AtLine(0); player.Face = "up"; yield return 2;
        Check($"and from beside a line, facing away from it ({target?.Label})", target?.Type == "spot" && target.Id == "karstlagoon");
        pendingShot = "150-guso-lines"; yield return 2;

        /* ---------- Maya ---------- */
        player.X = 2045; player.Y = 142; player.Face = "up"; yield return 3;
        Check($"Maya is by the lagoon ({target?.Label})", target?.Type == "islander" && target.Id == "maya");
        Inp.Tap(KeyboardKey.E); yield return 4;
        bool told = dlg != null && dlg.Lines.Any(l => l.T.Contains("guso farm"));
        pendingShot = "151-maya-guso"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"Maya tells you about her guso farm and gives you two cuttings ({Has("guso")})", told && state.Hinted("guso") && Has("guso") == 2);
        AtLine(2); yield return 2;
        Check($"the east lines wait for the co-op ({target?.Label})", target?.Type == "info" && target.Label.Contains("east lines"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("so nothing goes on them", !state.guso.ContainsKey("2") && Has("guso") == 2);

        // Tie a cutting on each west line.
        for (int i = 0; i < 2; i++)
        {
            AtLine(i); yield return 2;
            Check($"west line {i}: E ties on a cutting ({target?.Label})", target?.Type == "guso" && target.Label == "Tie a guso cutting on the line");
            Inp.Tap(KeyboardKey.E); yield return 3;
        }
        Check($"both west lines are planted today ({Has("guso")} guso left)", Has("guso") == 0 && state.guso.TryGetValue("0", out var g0) && g0.day == state.day && state.guso.ContainsKey("1"));
        AtLine(0); yield return 2;
        Check($"it needs two mornings ({target?.Label})", target?.Type == "info" && target.Label.Contains("in 2 days"));
        // The lines survive saving and loading.
        Save();
        LoadSlot(SaveFile.Slot); yield return 10;
        animals.Clear();
        Check("the lines survive saving and loading", state.guso.Count == 2 && state.guso["0"].day == state.day);
        state.day++;
        AtLine(0); yield return 2;
        Check($"the next day it's nearly there ({target?.Label})", target?.Type == "info" && target.Label.Contains("tomorrow morning"));
        state.day++;
        AtLine(0); yield return 2;
        Check($"on the second morning it's ready ({target?.Label})", target?.Type == "guso" && target.Label == "Harvest the guso");
        pendingShot = "152-guso-ready"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a harvest gives four bunches and ties one back on ({Has("guso")} guso; {toastMsg})",
            Has("guso") == 3 && state.guso["0"].day == state.day && !state.guso["0"].torn && state.Hinted("gusoHarvest"));
        AtLine(0); yield return 2;
        Check($"so the line is growing again ({target?.Label})", target?.Type == "info" && target.Label.Contains("growing"));

        // A storm while it grows tears half of it away, checked as the day turns over (even across a rest)...
        state.guso["1"] = new GusoLine { day = state.day, at = 100 };
        state.forecast = new() { new() { at = 0, w = "clear" }, new() { at = 300, w = "rain" }, new() { at = 360, w = "storm" }, new() { at = 500, w = "clear" } };
        state.clock = 21 * 60; state.weather = planned = "clear";
        state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        Rest(); yield return 80;
        Check($"resting through the night after a storm marks the line torn (day {state.day})", state.guso["1"].torn);
        state.day++; SetNight(false); ClearSkies();
        AtLine(1); yield return 2;
        int before = Has("guso");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a torn line gives only two ({Has("guso") - before} kept; {toastMsg})", Has("guso") == before + 1 && toastMsg.Contains("storm") && !state.guso["1"].torn);
        // ...but a storm that was over before the cutting went on doesn't.
        state.guso["1"] = new GusoLine { day = state.day, at = 700 };
        state.forecast = new() { new() { at = 0, w = "clear" }, new() { at = 300, w = "storm" }, new() { at = 500, w = "clear" } };
        TearGuso();
        Check("a storm earlier that day, before the cutting went on, doesn't tear it", !state.guso["1"].torn);
        ClearSkies();

        /* ---------- Drying and eating ---------- */
        Note("Drying guso, and what it's for");
        void NoFish() { foreach (var k in state.inv.Keys.Where(k => Items.ById[k].Kind == "fish").ToList()) state.inv.Remove(k); }
        NoFish(); state.inv.Remove("salt");
        state.inv["guso"] = 7;
        player.X = 160; player.Y = 115; player.Face = "right"; yield return 2;
        var rack = new Build { id = "dryrack", x = (int)(player.X / T) + 1, y = (int)((player.Y - 1.5f) / T) };
        state.builds.Add(rack); ReindexBuilds();
        player.X = rack.x * T - 5; player.Y = rack.y * T + 7; yield return 3;
        Check($"with guso and no salted fish, E spreads guso ({target?.Label})", target?.Type == "rackguso" && target.Label == "Lay out guso to dry");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a rack takes six bunches of guso, no salt ({Rack(rack)?.fish.Count}, {Has("guso")} left)", Rack(rack)?.fish.Count == 6 && Has("guso") == 1 && RackIsGuso(Rack(rack)));
        yield return 2;
        Check($"and dries them in the sun ({target?.Label}, {target?.AltLabel})", target?.Label == "6 guso drying: 6h 00m of sunshine to go" && target.AltLabel == "Take the guso back");
        Rack(rack).dry = DryGoal / 2;
        pendingShot = "153-guso-rack"; yield return 2;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check($"F takes it back ({Has("guso")})", Has("guso") == 7 && Rack(rack) == null);
        // With fish and salt too, E salts the fish as before, and F spreads the guso.
        state.inv["bangus"] = 2; state.inv["salt"] = 2; yield return 3;
        Check($"with fish and salt as well, E is the fish and F the guso ({target?.Label} / {target?.AltLabel})",
            target?.Type == "rack" && target.Label == "Salt fish and lay them out to dry" && target.AltType == "rackguso");
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check("F spreads the guso, and leaves the fish and salt alone", RackIsGuso(Rack(rack)) && Has("bangus") == 2 && Has("salt") == 2);
        Rack(rack).dry = DryGoal; yield return 3;
        Check($"once it's dry ({target?.Label})", target?.Label == "Take down the dried guso (6)");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"it comes off as dried guso ({Has("dried_guso")}; {toastMsg})", Has("dried_guso") == 6 && Rack(rack) == null);
        // Taking a rack down gives guso back, and says so.
        state.racks[RackKey(rack)] = new RackLoad { fish = new() { "guso", "guso" } };
        state.builds.Remove(rack); string packed = PackUp(rack); ReindexBuilds();
        Check($"taking a rack down returns the guso on it ({packed.Trim()})", Has("guso") == 3 && packed.Contains("2 guso"));

        // Dried guso rolls sushi (bought with a real Make click); fresh guso and suka make ensaladang guso.
        NoFish(); state.inv["bangus"] = 1;
        foreach (var k in new[] { "seaweed", "berries", "calamansi", "coconut", "egg", "suka", "salt" }) state.inv.Remove(k);
        state.inv["dried_guso"] = 1; state.inv.Remove("guso");
        craftPage = 0; OpenCraft("stove"); yield return 4;
        pendingShot = "154-stove-sushi"; yield return 2;
        ClickButton("Make"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"dried guso rolls sushi ({Has("sushi_roll")} roll, {Has("dried_guso")} dried guso left)", Has("sushi_roll") == 1 && Has("dried_guso") == 0 && Has("bangus") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        state.inv["seaweed"] = 1; state.inv["dried_guso"] = 2;
        state.inv["bangus"] = 3; state.inv["berries"] = 1;
        Craft(Items.Recipes.First(r => r.Out == "maki_platter"));
        Check($"the pot seaweed goes first ({Has("seaweed")} seaweed, {Has("dried_guso")} dried guso)", Has("maki_platter") == 1 && Has("seaweed") == 0 && Has("dried_guso") == 1);
        state.inv["guso"] = 2; state.inv["suka"] = 1; state.inv.Remove("dried_guso"); NoFish();
        craftPage = 0; OpenCraft("stove"); yield return 4;
        ClickButton("Next"); yield return 3;
        ClickButton("Make"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"two guso and a splash of suka make ensaladang guso ({Has("ensaladang_guso")})", Has("ensaladang_guso") == 1 && Has("guso") == 0 && Has("suka") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Pip pays more for dried guso than for fresh", Items.SellPrice("dried_guso") > Items.SellPrice("guso") * 3 && Items.SellPrice("guso") > 0);
        Check("they say \"4 guso\" and \"3 dried guso\"", Items.Amount("dried_guso", 3) == "3 dried guso" && !Items.Amount("guso", 4).Contains("gusos"));

        /* ---------- The co-op ---------- */
        state.inv["dried_guso"] = 5; int coins = state.coins;
        player.X = 2045; player.Y = 142; player.Face = "up"; yield return 3;
        Check($"with four dried guso, E gives them to Maya ({target?.Label})", target?.Label == "Give Maya 4 dried guso");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"the co-op's first sale pays {GusoCoopPay} coins and opens the east lines", state.coins == coins + GusoCoopPay && Has("dried_guso") == 1 && state.Hinted("gusoCoop") && GusoLineOpen(2) && GusoLineOpen(3));
        // Out of cuttings with empty lines, she tops you up.
        state.inv.Remove("guso");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"out of cuttings, Maya gives one for each empty line ({Has("guso")})", Has("guso") == 2);
        AtLine(3); yield return 2;
        Check($"the east lines take cuttings now ({target?.Label})", target?.Type == "guso");
        Inp.Tap(KeyboardKey.E); yield return 3;
        state.guso["0"].day = state.day - GusoDays;
        Gfx.Seen.Remove("dot:Seaweed farm"); Gfx.Seen.Remove("dot:Seaweed farm (ready)");
        TogglePanel("map"); yield return 4;
        Check($"the Amihan chart marks the farm, and that a line is ready ({chartEast})", chartEast && Gfx.Seen.ContainsKey("dot:Seaweed farm (ready)"));
        pendingShot = "155-chart-farm"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Older saves load without a farm.
        SaveFile.Clear(3);
        File.WriteAllText(SaveFile.SlotPath(3), "{\"created\":true,\"inv\":{\"rod_old\":1},\"guso\":{\"7\":{\"day\":1},\"0\":null}}");
        var old = SaveFile.Read(3);
        Check("older saves load with an empty farm (and junk lines are dropped)", old != null && old.guso != null && old.guso.Count == 0);
        SaveFile.Clear(3);

        foreach (int f in NightLightsScript()) yield return f;
    }

    IEnumerable<int> NightLightsScript()
    {
        Note("Bakawan's night lights");
        state.day = 5;            // a new moon: (5 + 3) % 8 == 0
        ClearSkies(); SetNight(false); state.aboard = false; state.riding = false;
        state.sightings.Clear(); state.gifts.Remove("alitaptap"); state.gifts.Remove("plankton");
        Check($"day 5 is a dark night, day 1 a full moon ({MoonPhase})", MoonPhase == 0);

        /* ---------- The firefly trees ---------- */
        Check("the pagatpat stand on Bakawan's west shore, with water just west of them",
            FireflyTrees.All(p => BiomeAt((int)(p.x / T), (int)(p.y / T)) == 5 && Swimmable(TileUnder(p.x - 12, p.y))));
        player.X = 1895; player.Y = 612; yield return 2;
        var reach = Reachable();
        bool NearTree(int i, float reachPx)
        {
            var (x, y) = FireflyTrees[i];
            foreach (var (tx, ty) in reach.OrderBy(t => Dist(t.Item1 * T + 5, t.Item2 * T + 7, x, y)))
            {
                float px = tx * T + 5, py = ty * T + 7;
                if (!CanStand(px, py) || Dist(px, py, x, y) >= reachPx) continue;
                player.X = px; player.Y = py; FaceToward(x, y);
                return true;
            }
            return false;
        }
        Check("each tree can be reached on foot", Enumerable.Range(0, FireflyTrees.Length).All(i => NearTree(i, 15)));
        NearTree(1, 15); yield return 2;
        Check($"by day they're just trees ({target?.Label})", target?.Type == "info" && target.Label.StartsWith("Pagatpat trees"));
        pendingShot = "156-pagatpat-day"; yield return 2;
        SetNight(true); state.weather = planned = "rain"; SnapWeather(); yield return 2;
        Check($"no fireflies in the rain ({target?.Label})", target?.Type == "info" && target.Label.Contains("rain"));
        ClearSkies(); yield return 2;
        Check($"on a clear night they're out ({target?.Label})", target?.Type == "fireflies" && target.Label == "Watch the alitaptap");
        yield return 10;
        // Catch the moment they flash, for the screenshot.
        for (int i = 0; i < 60 && FireflyFlash(time) < 0.7f; i++) yield return 1;
        pendingShot = "157-alitaptap"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"watching them goes on Tala's list ({toastMsg})", state.sightings.GetValueOrDefault("alitaptap") == 1 && toastMsg.Contains("Tala's list"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("once a night", state.sightings.GetValueOrDefault("alitaptap") == 1);

        // From the boat, just offshore.
        state.inv["boat"] = 1;
        var boatAt = Enumerable.Range(0, 60).SelectMany(dy => Enumerable.Range(0, 40).Select(dx => (x: 1700f + dx, y: 540f + dy)))
            .Where(p => BoatCanStand(p.x, p.y) && FireflyTrees.Any(f => Dist(f.x, f.y, p.x, p.y) < 30)).OrderBy(p => Dist(p.x, p.y, FireflyTrees[1].x, FireflyTrees[1].y)).FirstOrDefault();
        Check($"there's water for a boat just off the trees ({boatAt})", boatAt.x > 0);
        state.aboard = true; player.X = state.boatX = boatAt.x; player.Y = state.boatY = boatAt.y; boatFace = player.Face = "right"; yield return 3;
        Check($"from the boat you can watch them too ({target?.Label}, {target?.RideLabel})", target?.Type == "fireflies");

        /* ---------- The glowing water ---------- */
        // Sail through Bakawan's water on a dark night: the wake glows, and Tala's list counts it.
        state.aboard = true; player.X = state.boatX = 1690; player.Y = state.boatY = 610; boatFace = player.Face = "up"; glowTrail.Clear(); yield return 3;
        Check($"the boat's in Bakawan's glowing water ({InGlowWater(player.X, player.Y)}, {GlowNight})", InGlowWater(player.X, player.Y) && GlowNight && BoatCanStand(player.X, player.Y));
        Inp.Hold(KeyboardKey.Up, true);
        for (int i = 0; i < 40; i++) yield return 0;
        pendingShot = "158-glowing-wake"; yield return 0;
        for (int i = 0; i < 10; i++) yield return 0;
        Inp.Hold(KeyboardKey.Up, false); yield return 2;
        Check($"the wake glows ({glowTrail.Count} sparks) and the glowing water goes on Tala's list ({toastMsg})", glowTrail.Count > 30 && state.sightings.GetValueOrDefault("plankton") == 1);
        Check("the sparks stay in the water", glowTrail.All(g => InGlowWater(g.X, g.Y)));
        // Not by the full moon...
        state.day = 1; state.gifts.Remove("plankton"); state.sightings.Remove("plankton"); glowTrail.Clear();
        player.X = state.boatX = 1690; player.Y = state.boatY = 610; boatFace = player.Face = "up"; yield return 2;
        Inp.Hold(KeyboardKey.Up, true);
        for (int i = 0; i < 30; i++) yield return 0;
        Inp.Hold(KeyboardKey.Up, false); yield return 2;
        Check($"under a full moon it doesn't show ({glowTrail.Count})", glowTrail.Count == 0 && state.sightings.GetValueOrDefault("plankton") == 0);
        // ...nor far from Bakawan.
        state.day = 5;
        player.X = state.boatX = 1450; player.Y = state.boatY = 400; boatFace = player.Face = "left"; yield return 2;
        float farX = player.X;
        Inp.Hold(KeyboardKey.Left, true);
        for (int i = 0; i < 20; i++) yield return 0;
        Inp.Hold(KeyboardKey.Left, false); yield return 2;
        Check($"and only around Bakawan ({glowTrail.Count}, sailed {farX - player.X:0} px)", glowTrail.Count == 0 && state.sightings.GetValueOrDefault("plankton") == 0 && farX - player.X > 10);
        state.aboard = false;

        /* ---------- Tala's list ---------- */
        SetNight(false); ClearSkies();
        SpawnAnimals(); yield return 2;
        foreach (var kind in new[] { "tarsier", "hornbill" })
        {
            var a = animals.First(an => an.Kind == kind);
            player.X = 1872; player.Y = 614; player.Face = "right";
            a.Pause = 99; a.Vx = a.Vy = 0; a.X = player.X + 8; a.Y = player.Y; yield return 2;
            Check($"facing the {kind}, E observes it ({target?.Label})", target?.Type == "animal" && target.Label.StartsWith("Observe"));
            Inp.Tap(KeyboardKey.E); yield return 3;
            Check($"observing the {kind} counts for Tala ({toastMsg})", state.sightings.GetValueOrDefault(kind) == 1);
            animals.Remove(a);
        }
        animals.Clear();
        state.sightings["plankton"] = 1;
        Check("with all four seen, Tala's list is done", TalaListDone);
        state.hinted["bubo"] = true; state.hinted.Remove("fireflies"); state.inv.Remove("glow_bobber");
        int coinsBefore = state.coins;
        player.X = 1895; player.Y = 610; player.Face = "up"; yield return 3;
        Check($"E tells Tala ({target?.Label})", target?.Label == "Tell Tala what you've seen");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"Tala pays 100 coins and gives a glow bobber ({state.coins - coinsBefore})", state.coins == coinsBefore + 100 && Has("glow_bobber") == 1 && state.Hinted("talaReward"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        bool hint = dlg != null && dlg.Lines.Any(l => l.T.Contains("pagatpat"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("then she tells you where the alitaptap gather", hint && state.Hinted("fireflies"));
        coinsBefore = state.coins;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("only once", state.coins == coinsBefore);

        /* ---------- The Fish log and the chart ---------- */
        Gfx.Seen.Remove("dot:Alitaptap trees");
        TogglePanel("map"); yield return 4;
        Check("the Amihan chart marks the alitaptap trees", Gfx.Seen.ContainsKey("dot:Alitaptap trees"));
        pendingShot = "159-chart-alitaptap"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        foreach (var o in Data.Odd) state.odd[o.Id] = 12;
        foreach (var k in SeaKinds.Keys.Concat(BakawanKinds.Keys)) state.sightings[k] = 12;
        TogglePanel("dex"); dexTab = "log"; logPage = Data.Biomes.Length; yield return 4;
        Check($"the Legends & more page fits on the screen ({lastDexBottom:0} of {Gfx.LH})", lastDexBottom <= Gfx.LH - 8);
        float halfW = (1180 - 64 - 24 - 18) / 2f / 2;
        var tooWide = SeaKinds.Select(k => k.Value.name).Concat(BakawanKinds.Select(k => k.Value.name)).Where(n => Gfx.Measure($"{n} ×12", FontKind.Ui600, 16) > halfW - 26).ToList();
        Check($"every sighting's name fits its half of the card ({string.Join(", ", tooWide)})", tooWide.Count == 0);
        pendingShot = "160-fishlog-sightings"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        state.odd.Clear();
        foreach (int f in VillageScript()) yield return f;
    }

    // Asohos and Niko's second job, Dado's painted sail and the sanctuary's giant clam (1.15).
    IEnumerable<int> VillageScript()
    {
        Note("Asohos, the painted sail and the taklobo");
        state.day = 2;   // not a festival day (Festival.cs)
        SetNight(false); ClearSkies(); state.aboard = false; animals.Clear();
        var asohos = Data.FishById["asohos"];
        var rolls = Enumerable.Range(0, 2000).Select(_ => RollCatch("karstlagoon").Id).ToList();
        Check($"asohos bite in the karst lagoon, and often ({rolls.Count(r => r == "asohos")} of 2000)", rolls.Count(r => r == "asohos") > 400 && !asohos.Rare);
        Check("it has a picture and a bag entry", FishArt.Looks.ContainsKey("asohos") && Items.ById["asohos"].Kind == "fish");
        state.hinted["niko_request"] = true; state.hinted.Remove("niko_asohos"); state.inv.Remove("asohos"); state.inv.Remove("cricket");
        player.X = 1715; player.Y = 200; player.Face = "up"; yield return 3;
        Check($"Niko is by his house ({target?.Label})", target?.Type == "islander" && target.Id == "niko");
        Inp.Tap(KeyboardKey.E); yield return 3;
        bool asks = dlg != null && dlg.Lines.Any(l => l.T.Contains("asohos"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("after the bangus, Niko asks for asohos", asks);
        state.inv["asohos"] = 4; int coins = state.coins; yield return 3;
        Check($"with three, E hands them over ({target?.Label})", target?.Label == $"Give Niko {NikoAsohos} asohos");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"he pays 70 coins and four crickets ({state.coins - coins})", state.coins == coins + 70 && Has("asohos") == 1 && Has("cricket") == 4 && state.Hinted("niko_asohos"));
        coins = state.coins; state.inv["asohos"] = 5;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("only once", state.coins == coins && Has("asohos") == 5);

        // Gold in the regatta: Dado's family sail, alongside the agong if this is the first record.
        state.inv.Remove("painted_sail"); state.inv.Remove("agong"); state.hinted.Remove("bk:agong");
        race = new Race { T = GoldTime - 2 };
        FinishRace(); yield return 3;
        bool both = dlg != null && dlg.Lines.Any(l => l.T.Contains("agong")) && dlg.Lines.Any(l => l.T.Contains("paraw sail"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("a first gold wins the agong and Dado's painted sail together", both && Has("painted_sail") == 1 && Has("agong") == 1 && Wears("painted_sail"));
        race = new Race { T = GoldTime - 1 };
        FinishRace(); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("only one sail", Has("painted_sail") == 1);
        state.inv["boat"] = 1; state.aboard = true; player.X = state.boatX = 1690; player.Y = state.boatY = 610; boatFace = player.Face = "up"; yield return 2;
        Inp.Hold(KeyboardKey.Right, true);
        for (int i = 0; i < 40; i++) yield return 0;
        pendingShot = "161-painted-sail"; yield return 0;
        for (int i = 0; i < 4; i++) yield return 0;
        Inp.Hold(KeyboardKey.Right, false); yield return 2;
        state.aboard = false;

        // The giant clam sits in the sanctuary's seagrass, day and night; Joy's list has four now.
        Check("the taklobo sits in the sanctuary's seagrass", InSanctuary(TakloboX, TakloboY) && TileUnder(TakloboX, TakloboY + 1.5f) == 'w');
        seaLife.Clear(); player.X = 2300; player.Y = 700; state.aboard = true; state.boatX = player.X; state.boatY = player.Y; yield return 2;
        SetNight(true);
        Check("it's there by night too, and never moves", VisibleSeaLife().Any(c => c.Kind == "taklobo" && c.X == TakloboX && c.Y == TakloboY));
        SetNight(false); state.aboard = false; seaLife.Clear();
        Check("Joy's list has five, Tala's four", SeaKinds.Count == 5 && BakawanKinds.Count == 4);

        // The bahay kubo: a shack on stilts, in the build bar once you've been to Amihan.
        state.hinted.Remove("amihan");
        Check("before Amihan there's no bahay kubo to build", !BuildTools().Contains("kubo"));
        state.hinted["amihan"] = true;
        Check($"after, it's the twelfth outdoor piece ({Array.IndexOf(BuildTools(), "kubo")})", Array.IndexOf(BuildTools(), "kubo") == 11 && Array.IndexOf(BuildTools(), "bubo") == 10);
        player.X = 160; player.Y = 115; player.Face = "down"; state.inv["wood"] = 20; state.inv["stone"] = 4; yield return 2;
        Inp.Tap(KeyboardKey.B); yield return 3;
        for (int i = 0; i < 14 && buildTool != "kubo"; i++) { Inp.TapPad(GamepadButton.RightTrigger1); yield return 2; }
        pendingShot = "162-build-bar-kubo"; yield return 2;
        Check($"the build bar still fits across the screen ({lastBuildBarW:0} of {Gfx.LW - 30})", buildTool == "kubo" && lastBuildBarW <= Gfx.LW - 30);
        Inp.Tap(KeyboardKey.B); yield return 3;
        var kubo = new Build { id = "kubo", x = 20, y = 13 };
        state.builds.Add(kubo); ReindexBuilds();
        player.X = kubo.x * T + 10; player.Y = kubo.y * T + 13; player.Face = "up"; yield return 3;
        Check($"its steps are a door ({target?.Label})", target?.Type == "door" && target.Id == ShackKey(kubo));
        pendingShot = "163-kubo"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 40;
        Check($"inside, it's a room to furnish ({scene})", scene == ShackKey(kubo) && InOwnHouse);
        pendingShot = "164-kubo-inside"; yield return 2;
        LeaveToWorld(); yield return 5;
        for (int i = 0; i < 120 && (mode != "play" || scene != "world"); i++) yield return 2;
        state.rooms[ShackKey(kubo)] = new() { new Build { id = "table", x = 3, y = 3 } };
        state.builds.Remove(kubo); int wood0 = Has("wood"); PackUp(kubo); ReindexBuilds();
        Check("taking it down refunds it, and what's inside", Has("wood") == wood0 + 10 + 4 && !state.rooms.ContainsKey(ShackKey(kubo)));

        // Lira's supper, then the rondalla on evenings in the village square.
        state.hinted.Remove("rondalla"); state.hinted.Remove("liraSupperAsked"); state.inv.Remove("ginataan"); state.inv.Remove("sinigang");
        player.X = 1595; player.Y = 198; player.Face = "up"; state.clock = 19 * 60; ClearSkies(); yield return 3;
        Check($"before the supper, no band in the evening ({WantedTrack()})", !RondallaOut && WantedTrack() != "rondalla");
        string liraLabel = target?.Label + " / " + mode;
        Inp.Tap(KeyboardKey.E); yield return 3;
        bool asked = dlg != null && dlg.Lines.Any(l => l.T.Contains("supper"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"Lira asks for a pot for the village supper ({liraLabel})", asked && state.Hinted("liraSupperAsked"));
        state.inv["ginataan"] = 1; coins = state.coins; yield return 3;
        Check($"with ginataan in your bag, E gives it to her ({target?.Label})", target?.Label == "Give Lira the ginataang isda");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("she pays 60 coins, and the rondalla comes out", state.coins == coins + 60 && Has("ginataan") == 0 && state.Hinted("rondalla"));
        player.X = RondallaX; player.Y = RondallaY + 20; player.Face = "up"; yield return 3;
        Check($"in the evening the village plays strings ({WantedTrack()})", RondallaOut && WantedTrack() == "rondalla" && Music.Names.Contains("rondalla"));
        pendingShot = "165-rondalla"; yield return 2;
        state.clock = 12 * 60; yield return 2;
        Check($"by day it's the usual tune ({WantedTrack()})", !RondallaOut && WantedTrack() != "rondalla");
        state.clock = 19 * 60; state.weather = planned = "storm"; yield return 2;
        Check("not in a storm", !RondallaOut);
        ClearSkies(); player.X = 2045; player.Y = 142; yield return 2;
        Check($"and not out of earshot ({WantedTrack()})", RondallaOut && !RondallaPlaying);
        SetNight(false);

        // The Bangus Festival: the last day of each amihan.
        Check("the festival is the last day of each amihan (days 5, 15, 25)", FestivalDay(5) && FestivalDay(15) && FestivalDay(25) && !FestivalDay(4) && !FestivalDay(6) && !FestivalDay(10));
        state.day = 5; ClearSkies();
        Check($"the morning says so ({FestivalNews().Trim()})", FestivalNews().Contains("Bangus Festival"));
        state.gifts.Remove("lira_meal"); int grilled = Has("grilled_fish");
        player.X = 1595; player.Y = 198; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"Lira's meal is a bangus off the festival grill ({toastMsg})", Has("grilled_fish") == grilled + 1);
        player.X = 1640; player.Y = 212; player.Face = "up"; yield return 3;
        pendingShot = "166-bangus-festival"; yield return 2;
        // The derby: the day's heaviest bangus, weighed by Niko.
        state.festDay = 0; state.festKg = 0; state.gifts.Remove("fest_derby"); state.gifts.Remove("fest_announce");
        player.X = 1715; player.Y = 200; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        bool announced = dlg != null && dlg.Lines.Any(l => l.T.Contains("Bangus Festival"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Niko announces the bangus derby", announced);
        for (int i = 0; i < 5; i++) AddCatch(Data.FishById["bangus"]);
        float best = FestivalBest; coins = state.coins; yield return 3;
        Check($"the day's heaviest bangus counts ({Kg(best)}), and E shows it to Niko ({target?.Label})", best > 0 && target?.Label == "Show Niko your best bangus");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        int won = state.coins - coins;
        Check($"he pays by its weight ({Kg(best)}: {won} coins)", won == (best >= 2.6f ? 150 : best >= 2f ? 80 : 40));
        coins = state.coins; yield return 3;
        Check($"once a festival ({target?.Label})", target?.Label == "Talk to Niko");
        state.day = 4; state.festDay = 0; state.festKg = 0;
        AddCatch(Data.FishById["bangus"]);
        Check("on other days a bangus is just a bangus", FestivalBest == 0);
        // Dado pays double on festival day.
        state.day = 5; state.regattaDay = 0; coins = state.coins;
        race = new Race { T = BronzeTime - 1 };
        FinishRace(); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"the regatta pays double on festival day ({state.coins - coins})", state.coins == coins + 60);
        state.day = 2;
    }
}
#endif
