#if DEBUG
using Raylib_cs;

namespace Fesh;

// The monsoons, the bubo, and fish that keep (1.14). FESH_SEASON_TEST=1 runs only this (it still needs FESH_AUTOTEST
// and FESH_SAVE); the full play-through runs it at the end.
partial class Game
{
    IEnumerable<int> SeasonScript()
    {
        Note("Seasons, the bubo and fish that keep");
        Inp.ScriptMouse = Offscreen;
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Season tester" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        state.loose.RemoveAll(l => l.kind == "worm");
        animals.Clear();
        yield return 3;

        /* ---------- Long names ---------- */
        // Most of the islands' fish have their English name in brackets after the local one: none may run off the bag card.
        var tooLong = Items.All.Where(d => BagTitle(d.Name, BagNameW) is var (lines, size) && (lines.Count > 2 || lines.Any(l => Gfx.Measure(l, FontKind.Ui700, size) > BagNameW))).Select(d => d.Name).ToList();
        Check($"every name fits on the bag card ({tooLong.Count} don't: {string.Join(", ", tooLong.Take(4))})", tooLong.Count == 0);
        // The other places a long name could run over (Codex's list, measured with the real font).
        float W(string t, FontKind k, float size) => Gfx.Measure(t, k, size);
        var cutBait = Items.Baits.Keys.Where(b => { state.inv[b] = 99; return TackleLabel("bait", b).EndsWith("…"); }).ToList();
        foreach (var b in Items.Baits.Keys) state.inv.Remove(b);
        Check($"every bait fits its tackle box row ({TackleLabel("bait", "tamban")}{(cutBait.Count > 0 ? "; cut: " + string.Join(", ", cutBait) : "")})", cutBait.Count == 0);
        var cutDerby = Data.AllCommon.Where(f => !f.Legend && !f.Rare && W($"{f.Name}, 99.99 kg", FontKind.Ui500, 18) > DerbyFishW).Select(f => f.Name).ToList();
        Check($"every fish a derby rival could bring fits their row ({cutDerby.Count} cut: {string.Join(", ", cutDerby.Take(3))})", cutDerby.Count == 0);
        Check("rod names fit the bag header and the tackle box", Items.Rods.All(r => W($"Fishing with: {Items.ById[r].Name}", FontKind.Ui600, 18) <= 244 && W(Items.ById[r].Name, FontKind.Ui700, 21) <= 200));
        Check("everything on Pip's stall fits beside its buttons", Items.Shop.All(p => W(Items.ById[p.id].Name + "  (have 99)", FontKind.Ui700, 19) <= (p.id is "bait" or "chum" ? 220 : 330)));
        foreach (var id in new[] { "pating", "salay_salay" })
        {
            state.inv[id] = 3; bagSel = id;
            TogglePanel("bag"); yield return 4;
            pendingShot = $"139-bag-{id}"; yield return 2;
            Inp.Tap(KeyboardKey.Escape); yield return 3;
            state.inv.Remove(id);
        }

        /* ---------- The seasons ---------- */
        Check("days 1-5 are the amihan, 6-10 the habagat, then the amihan again",
            Enumerable.Range(1, 5).All(d => SeasonOf(d) == "amihan") && Enumerable.Range(6, 5).All(d => SeasonOf(d) == "habagat") && SeasonOf(11) == "amihan" && SeasonOf(0) == "amihan");
        int Storms(int day) => Enumerable.Range(0, 2000).Count(_ => MakeForecast(day).Any(s => s.w == "storm"));
        int AnyRain(int day) => Enumerable.Range(0, 2000).Count(_ => MakeForecast(day).Any(s => s.w != "clear"));
        int StormMinutes(int day) => Enumerable.Range(0, 2000).Sum(_ =>
        {
            var p = MakeForecast(day);
            return p.Select((s, i) => s.w == "storm" ? (i + 1 < p.Count ? p[i + 1].at : 1440) - s.at : 0).Sum();
        });
        int stA = Storms(13), stH = Storms(8), wetA = AnyRain(13), wetH = AnyRain(8);
        Check($"the amihan is drier ({stA / 20.0:0}% storm days, {wetA / 20.0:0}% with rain) than the habagat ({stH / 20.0:0}% storm days, {wetH / 20.0:0}% with rain)",
            stA < 200 && stH > 360 && wetH > wetA + 400);
        float stormMins = (StormMinutes(13) + StormMinutes(8)) / 4000f;
        Check($"storm time over both seasons stays about what it was ({stormMins:0} minutes a day; it was about 29)", stormMins > 22 && stormMins < 36);
        var plan = new List<WeatherSpell> { new() { at = 0, w = "clear" }, new() { at = 240, w = "rain" }, new() { at = 300, w = "storm" }, new() { at = 620, w = "rain" }, new() { at = 780, w = "clear" } };
        string longStorm = DescribeDay(plan);
        plan[3].at = 420;
        Check($"forecasts say how long a storm lasts ({longStorm}; {DescribeDay(plan)})", longStorm.StartsWith("a long storm around") && DescribeDay(plan).StartsWith("a short storm around"));

        // The day before the habagat, Tomas says so with tomorrow's weather.
        state.day = 5; state.toldDay = 0; state.clock = 10 * 60; ClearSkies();
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 5;
        bool toldSeason = dlg != null && dlg.Lines.Any(l => l.T.StartsWith("Tomorrow? My old knee says") && l.T.Contains("the habagat is coming"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check("on day 5 Tomas says the habagat is coming tomorrow", toldSeason);
        // The morning it comes.
        state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        state.clock = 5 * 60 + 59; yield return 2;
        Advance(2, quiet: false);
        Check($"the habagat comes on the morning of day 6 ({toastMsg})", state.day == 6 && Season == "habagat" && toastMsg.Contains("The habagat has come"));
        Check($"and the clock says so ({SeasonLine()})", SeasonLine() == "Habagat season (southwest wind), day 1 of 5");
        // Sleeping through the night into the amihan still tells you.
        state.day = 10; state.clock = 22 * 60; state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } }; yield return 2;
        Rest(); yield return 80;
        Check($"resting into day 11 brings the amihan back, and says so ({toastMsg})", state.day == 11 && Season == "amihan" && toastMsg.Contains("The amihan has come"));
        state.day = 1; ClearSkies(); SetNight(false); yield return 2;
        OpenPause(); yield return 4;
        pendingShot = "140-pause-season"; yield return 2;
        ClosePause(); yield return 3;

        // The wind: southwest is quick in the amihan and slow in the habagat.
        state.day = 1;
        float swA = WindFactor(-MathF.Sqrt(.5f), MathF.Sqrt(.5f)), neA = WindFactor(MathF.Sqrt(.5f), -MathF.Sqrt(.5f));
        state.day = 6;
        float swH = WindFactor(-MathF.Sqrt(.5f), MathF.Sqrt(.5f)), neH = WindFactor(MathF.Sqrt(.5f), -MathF.Sqrt(.5f));
        Check($"the amihan blows from the northeast ({swA:0.00}x sailing southwest, {neA:0.00}x northeast) and the habagat from the southwest ({swH:0.00}x, {neH:0.00}x)",
            Math.Abs(swA - 1.15f) < .01f && Math.Abs(neA - .85f) < .01f && Math.Abs(swH - .85f) < .01f && Math.Abs(neH - 1.15f) < .01f);
        state.inv["boat"] = 1;
        var speeds = new List<float>();
        foreach (int day in new[] { 1, 6 })
        {
            state.day = day; ClearSkies();
            state.aboard = true; player.X = state.boatX = 1390; player.Y = state.boatY = 395; boatFace = player.Face = "left"; yield return 3;
            float x0 = player.X, t0 = time;
            Inp.Hold(KeyboardKey.Left, true);
            for (int i = 0; i < 30; i++) yield return 0;
            Inp.Hold(KeyboardKey.Left, false); yield return 1;
            speeds.Add((x0 - player.X) / Math.Max(.001f, time - t0));
        }
        Check($"steering west, the boat is quicker in the amihan ({speeds[0]:0} px/s) than the habagat ({speeds[1]:0} px/s)", speeds[0] > speeds[1] * 1.12f && speeds[1] > 40);
        state.aboard = false;
        player.X = 160; player.Y = 115; boatFace = player.Face = "down"; yield return 3;

        // A fish for each monsoon, out on the Habagat Sea.
        state.day = 1;
        var inAmihan = Enumerable.Range(0, 3000).Select(_ => RollCatch("habagatsea").Id).ToHashSet();
        state.day = 6;
        var inHabagat = Enumerable.Range(0, 3000).Select(_ => RollCatch("habagatsea").Id).ToHashSet();
        Check("the fusilier bites only in the amihan, the yellowstripe scad only in the habagat",
            inAmihan.Contains("dalagang_bukid") && !inAmihan.Contains("salay_salay") && inHabagat.Contains("salay_salay") && !inHabagat.Contains("dalagang_bukid"));
        var fusilier = Data.FishById["dalagang_bukid"];
        state.day = 1; bool bitingA = BitingNow(fusilier);
        state.day = 6;
        Check($"the Fish log knows ({WhenText(fusilier)}; biting now in the amihan {bitingA}, in the habagat {BitingNow(fusilier)})",
            WhenText(fusilier).Contains("in the amihan season") && Conditions(fusilier, false) == "amihan" && bitingA && !BitingNow(fusilier));
        Check("both have pictures and bag entries", new[] { "dalagang_bukid", "salay_salay" }.All(id => FishArt.Looks.ContainsKey(id) && Items.ById[id].Kind == "fish"));
        // No fish changed colour: they're handed out in the same order, the new ones last.
        int ti = 0;
        bool same = true;
        void Tints(IEnumerable<CommonFish> fs) { foreach (var f in fs.Where(f => !Items.AddedLater.Contains(f.Id))) same &= Items.ById[f.Id].Tint == Items.TintAt(ti++); }
        bool Late(string b) => b == "habagat";
        Tints(Data.Spots.Where(s => s.Scene != "sea" && !Late(s.Biome)).SelectMany(s => Data.Common[s.Id]));
        Tints(Data.PotCatch.Where(p => !Late(p.Key)).SelectMany(p => p.Value));
        Tints(Data.Spots.Where(s => s.Scene == "sea" && !Late(s.Biome)).SelectMany(s => Data.Common[s.Id]));
        Tints(Data.Spots.Where(s => s.Scene != "sea" && Late(s.Biome)).SelectMany(s => Data.Common[s.Id]));
        Tints(Data.PotCatch.Where(p => Late(p.Key)).SelectMany(p => p.Value));
        int beforeNew = ti;
        Tints(Data.Spots.Where(s => s.Scene == "sea" && Late(s.Biome)).SelectMany(s => Data.Common[s.Id]));
        foreach (var f in Data.Spots.Where(s => s.Scene != "sea").SelectMany(s => Data.Common[s.Id]).Where(f => Items.AddedLater.Contains(f.Id))) same &= Items.ById[f.Id].Tint == Items.TintAt(ti++);
        Check($"no fish changed colour, and the two new ones come last ({beforeNew} before them)", same && Data.Common["habagatsea"][^2..].Select(f => f.Id).SequenceEqual(new[] { "dalagang_bukid", "salay_salay" }));
        state.reqDone = Items.RequestChain.Length; state.hinted["habagat"] = true; state.hinted["amihan"] = true; state.hinted["visitedAtoll"] = true;
        Check("Tomas never asks for a seasonal fish", Enumerable.Range(0, 600).All(_ => Data.FishById[NextRequest().item].Season == null));
        state.day = 1;

        /* ---------- The bubo ---------- */
        Note("The bubo");
        var bubo = Items.Recipes.First(r => r.Out == "bubo");
        Check("before Tala shows you, the workbench has no bubo", !RecipeKnown(bubo));
        player.X = 1895; player.Y = 610; player.Face = "up"; yield return 3;
        Check($"Tala is on Bakawan ({target?.Label})", target?.Type == "islander" && target.Id == "tala");
        Inp.Tap(KeyboardKey.E); yield return 5;
        pendingShot = "141-tala-bubo"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Tala gives you a bubo and shows you how it's woven", Has("bubo") == 1 && state.Hinted("bubo") && RecipeKnown(bubo) && Items.Category(bubo) == "Tools");
        TalkIslander("tala"); while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("only once", Has("bubo") == 1);

        foreach (var id in new[] { "lagoon", "oasis", "swamp", "amihanpond", "bakawanpool" })
            Check($"{Data.SpotById[id].Label} has water for a bubo, and small ordinary fish for it ({string.Join(", ", BuboWeights(id).Select(p => p.f.Id))})",
                BuboWaters().ContainsValue(id) && BuboWeights(id).Count > 0 && BuboWeights(id).All(p => !p.f.Rare && !p.f.Legend && p.f.Kg <= BuboMaxKg));
        bool SaltTiles(string spotId)
        {
            var s = Data.SpotById[spotId];
            var tiles = Enumerable.Range(0, COLS * ROWS).Select(i => (x: i % COLS, y: i / COLS))
                .Where(p => worldMap[p.y, p.x] is 'l' or 'm' or 'o' && Dist(p.x * T + 5, p.y * T + 5, s.X, s.Y) < s.R).ToList();
            return tiles.Count > 0 && tiles.All(p => BuboSpot(p.x, p.y) == null);
        }
        Check("the karst lagoon and the atoll lagoon are salt: no bubo there", SaltTiles("karstlagoon") && SaltTiles("atolllagoon"));

        // Not in the sea...
        player.X = 160; player.Y = 115; yield return 2;
        Check("found the sea to try", FaceTile((x, y) => TileAt(x, y) == 'w'));
        Check($"a bubo won't go in the sea ({PlaceProblem(Data.BuildById["bubo"], FrontTile().tx, FrontTile().ty)})", PlaceProblem(Data.BuildById["bubo"], FrontTile().tx, FrontTile().ty) == BuboWater);
        // ...but it goes in the lagoon, picked with the bumpers (it has no number key) and placed with the build key.
        player.X = 100; player.Y = 105; yield return 2;
        Check("found the lagoon's edge", FaceTile((x, y) => BuboSpot(x, y) == "lagoon"));
        yield return 2;
        Inp.Tap(KeyboardKey.B); yield return 3;
        for (int i = 0; i < 14 && buildTool != "bubo"; i++) { Inp.TapPad(GamepadButton.RightTrigger1); yield return 2; }
        int buboAt = Array.IndexOf(BuildTools(), "bubo");
        Check($"the bumpers reach the bubo, the eleventh piece, which shows no number key ({buildTool}, '{PieceKey(buboAt, Data.BuildById["bubo"])}')",
            buildTool == "bubo" && buboAt == 10 && PieceKey(buboAt, Data.BuildById["bubo"]) == "" && PieceKey(0, Data.BuildById[BuildTools()[0]]) == "1 ");
        pendingShot = "142-bubo-build-bar"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        var trap = state.builds.FirstOrDefault(b => b.id == "bubo");
        Check($"set the bubo in the lagoon ({ghost?.Reason})", trap != null && Has("bubo") == 0 && state.pots.ContainsKey($"{trap.x},{trap.y}") && BuboSpot(trap.x, trap.y) == "lagoon");
        if (trap != null)
        {
            yield return 2;
            Check($"it needs a night in the water ({target?.Label})", target?.Type == "info" && target.Label.StartsWith("The bubo is soaking"));
            float keepY = player.Y;
            player.Y += 16; yield return 3;
            pendingShot = "142b-bubo-in-water"; yield return 2;
            player.Y = keepY; yield return 2;
            // It and its soaking day survive saving and loading.
            var (bx, by, set) = (trap.x, trap.y, state.pots[$"{trap.x},{trap.y}"]);
            var (px, py, face) = (player.X, player.Y, player.Face);
            Save();
            LoadSlot(SaveFile.Slot); yield return 10;
            animals.Clear();
            player.X = px; player.Y = py; player.Face = face;
            trap = state.builds.FirstOrDefault(b => b.id == "bubo");
            Check("the bubo and its soaking day survive saving and loading", trap != null && trap.x == bx && trap.y == by && state.pots.GetValueOrDefault($"{bx},{by}", -1) == set);
            if (trap == null) yield break;
            yield return 2;
            // A smoking rack right behind you doesn't hide the trap you're facing.
            state.builds.Add(new Build { id = "smoker", x = (int)(player.X / T) - (player.Face == "left" ? -1 : 1), y = (int)(player.Y / T) });
            ReindexBuilds();
            state.day++; yield return 3;
            Check($"the next morning you can lift it, even beside a smoking rack ({target?.Label})", target?.Type == "pot" && target.Label == "Lift the bubo");
            int perch = Has("pond_perch"), carp = Has("mud_carp");
            Inp.Tap(KeyboardKey.E); yield return 3;
            Check($"it brings up lagoon fish ({toastMsg})", Has("pond_perch") + Has("mud_carp") > perch + carp && !PotReady(trap));
            pendingShot = "143-bubo-lifted"; yield return 2;
            var got = new Dictionary<string, int>();
            for (int i = 0; i < 300; i++)
            {
                var before = state.commons.ToDictionary(kv => kv.Key, kv => kv.Value);
                state.pots[$"{trap.x},{trap.y}"] = state.day - 1;
                LiftBubo(trap);
                foreach (var (k, v) in state.commons) if (v > before.GetValueOrDefault(k)) got[k] = got.GetValueOrDefault(k) + v - before.GetValueOrDefault(k);
            }
            int lifted = got.Values.Sum();
            Check($"only the lagoon's small ordinary fish, one or two a time ({string.Join(", ", got.Select(kv => $"{kv.Key} {kv.Value}"))})",
                got.Keys.All(k => k is "pond_perch" or "mud_carp") && lifted >= 330 && lifted <= 450);
            Gfx.Seen.Remove("dot:Bubo"); Gfx.Seen.Remove("dot:Bubo (ready)");
            state.pots[$"{trap.x},{trap.y}"] = state.day - 1;
            TogglePanel("map"); yield return 4;
            Check("the chart marks your bubo", Gfx.Seen.ContainsKey("dot:Bubo (ready)"));
            Inp.Tap(KeyboardKey.Escape); yield return 3;
            state.builds.RemoveAll(b => b.id == "smoker"); ReindexBuilds();
            state.builds.Remove(trap); PackUp(trap); ReindexBuilds();
            Check("taking it down gives the bubo back", Has("bubo") == 1 && !state.pots.ContainsKey($"{trap.x},{trap.y}"));
        }

        /* ---------- Fish that keep ---------- */
        Note("Tinapa, tuyo and paksiw");
        void NoFish() { foreach (var k in state.inv.Keys.Where(k => Items.ById[k].Kind == "fish").ToList()) state.inv.Remove(k); }
        NoFish();
        state.inv["tamban"] = 3; state.inv["salt"] = 1; state.inv.Remove("cut_bait");
        OpenCraft("smoker"); yield return 4;
        pendingShot = "144-smoker-tinapa"; yield return 2;
        ClickButton("Make"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"two tamban and a pinch of salt smoke into two tinapa ({Has("tinapa")} tinapa, {Has("tamban")} tamban, {Has("salt")} salt)", Has("tinapa") == 2 && Has("tamban") == 1 && Has("salt") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("a bangus can be smoked too, but not a lapu-lapu", Items.TinapaFish.Contains("bangus") && !Items.TinapaFish.Contains("lapu_lapu"));

        // On a drying rack, tamban and sapsap become tuyo; anything else, daing.
        NoFish();
        state.inv["tamban"] = 2; state.inv["bangus"] = 1; state.inv["salt"] = 3;
        player.X = 160; player.Y = 115; player.Face = "right"; yield return 2;
        var rack = new Build { id = "dryrack", x = (int)(player.X / T) + 1, y = (int)((player.Y - 1.5f) / T) };
        state.builds.Add(rack); ReindexBuilds();
        player.X = rack.x * T - 5; player.Y = rack.y * T + 7; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"the rack takes the three fish ({Rack(rack)?.fish.Count})", Rack(rack)?.fish.Count == 3);
        if (Rack(rack) is RackLoad load) load.dry = DryGoal;
        yield return 3;
        Check($"once they're dry, the rack offers the dried fish ({target?.Label})", target?.Label == "Take down the dried fish (3)");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"two tuyo and a daing ({toastMsg})", Has("tuyo") == 2 && Has("daing") == 1 && toastMsg.Contains("1 daing and 2 tuyo"));
        state.builds.Remove(rack); ReindexBuilds();

        // Paksiw: vinegar from Pip (the last thing on the stall, bought with a real click), then the stove.
        state.coins = 100; int suka0 = Has("suka");
        OpenShop(); yield return 3;
        ClickButton("Buy"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        pendingShot = "145-pip-buy-list"; yield return 2;
        ClickButton("Buy 1"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"Pip sells suka ({Has("suka")}, {state.coins} coins left)", Has("suka") == suka0 + 1 && state.coins == 96 && toastMsg.Contains("paksiw"));
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        NoFish(); state.inv["bangus"] = 1;
        foreach (var k in new[] { "calamansi", "coconut", "seaweed", "berries", "egg" }) state.inv.Remove(k);
        craftPage = 0; OpenCraft("stove"); yield return 4;
        ClickButton("Next"); yield return 3;
        ClickButton("Make"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"a fish simmered in suka is paksiw ({Has("paksiw")} paksiw, {Has("suka")} suka)", Has("paksiw") == 1 && Has("suka") == suka0 && Has("bangus") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("they say \"2 tinapa\", not \"2 tinapas\"", Items.Amount("tinapa", 2) == "2 tinapa" && Items.Amount("cowrie", 2) == "2 cowries (sigay)");

        /* ---------- Manang Rosa's provisions ---------- */
        Note("Manang Rosa's orders");
        state.day = 3; ClearSkies(); SetNight(false);
        state.inv.Remove("tuyo"); state.inv.Remove("daing"); state.inv.Remove("tinapa"); state.inv.Remove("paksiw");
        player.X = 225; player.Y = 658; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("you meet Manang Rosa first", state.Hinted("metRosa"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        bool asked = dlg != null && dlg.Lines.Any(l => l.T.Contains("bring me 3 dried fish") && l.T.Contains("amihan"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("in the amihan she wants dried fish", asked);
        state.inv["tuyo"] = 2; state.inv["daing"] = 2; int coins = state.coins, salt = Has("salt"); yield return 3;
        Check($"with them in your bag, E hands them over ({target?.Label})", target?.Label == "Give Manang Rosa 3 dried fish");
        Inp.Tap(KeyboardKey.E); yield return 3;
        pendingShot = "146-rosa-order"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"she takes the tuyo first, and pays coins and salt ({state.coins - coins} coins, {Has("salt") - salt} salt)",
            Has("tuyo") == 0 && Has("daing") == 1 && state.coins == coins + 85 && Has("salt") == salt + 2);
        state.inv["tuyo"] = 3; coins = state.coins; salt = Has("salt"); yield return 3;
        Check($"once a day ({target?.Label})", target?.Label == "Talk to Manang Rosa");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("even with enough in your bag: talking again takes and pays nothing", Has("tuyo") == 3 && Has("daing") == 1 && state.coins == coins && Has("salt") == salt);
        state.day = 8; state.inv["tinapa"] = 1; state.inv["paksiw"] = 1; coins = state.coins; yield return 3;
        Check($"in the habagat it's tinapa or paksiw ({target?.Label})", target?.Label == "Give Manang Rosa 2 tinapa or paksiw");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("and she pays for those", Has("tinapa") == 0 && Has("paksiw") == 0 && state.coins == coins + 60);
        state.day = 1;
    }

    // Stands on reachable land close to the player, facing a tile that matches.
    bool FaceTile(Func<int, int, bool> want)
    {
        int px = (int)(player.X / T), py = (int)(player.Y / T);
        foreach (var (x, y) in Reachable().OrderBy(t => Math.Abs(t.Item1 - px) + Math.Abs(t.Item2 - py)))
            foreach (var (ox, oy, f) in new[] { (0, -1, "up"), (0, 1, "down"), (-1, 0, "left"), (1, 0, "right") })
            {
                if (!want(x + ox, y + oy) || BuildAt(x + ox, y + oy) != null) continue;
                float sx = x * T + 5, sy = y * T + 7;
                if (!CanStand(sx, sy)) continue;
                player.X = sx; player.Y = sy; player.Face = f;
                if (FrontTile() == (x + ox, y + oy) && InReach(x + ox, y + oy, 1)) return true;
            }
        return false;
    }
}
#endif
