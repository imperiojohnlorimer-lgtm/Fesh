#if DEBUG
using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The Habagat islands (1.13): the world and getting there, Asinan's salt and drying racks, gleaning, sungka, the islets
// and the regatta, the Parola lighthouse, the sanctuary, and the eclipse. FESH_HABAGAT_TEST=1 runs only this (it still
// needs FESH_AUTOTEST and FESH_SAVE); the full play-through runs it at the end.
partial class Game
{
    IEnumerable<int> HabagatScript()
    {
        Note("Habagat: the islands along the bottom of the map");
        Inp.ScriptMouse = Offscreen;
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Habagat tester" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        state.loose.RemoveAll(l => l.kind == "worm");
        animals.Clear();   // goats wandering in front of a check would take its target
        yield return 3;
        File.WriteAllLines(Path.Combine(testDir, "habagat-map.txt"), Enumerable.Range(HabagatTop - 6, ROWS - HabagatTop + 6).Select(y =>
            $"{y,2} " + new string(Enumerable.Range(0, COLS).Select(x => worldMap[y, x]).ToArray())));

        /* ---------- The world, and getting there ---------- */
        Check("nobody can walk or wade to Habagat from Saltmere, Mirewood, Sunscald or Starfall",
            !WadeReach(160, 115).Any(p => BiomeAt(p.Item1, p.Item2) == 6) && !WadeReach(AtollJettyX + 4, AtollJettyY + 1).Any(p => BiomeAt(p.Item1, p.Item2) == 6));
        Check("the coast spots of Mirewood and Sunscald are still fishable from land",
            new[] { "coral", "mirage" }.All(id => { player.X = 160; player.Y = 115; var s = Data.SpotById[id]; return Reachable().Any(t => CanStand(t.Item1 * T + 5f, t.Item2 * T + 7f) && Dist(t.Item1 * T + 5f, t.Item2 * T + 7f, s.X, s.Y) < s.R - 2); }));
        foreach (var (spot, x, y) in new[] { ("asinan", 200f, 668f), ("pulo", 640f, 640f), ("parola", 1190f, 625f) })
        {
            player.X = x; player.Y = y;
            var s = Data.SpotById[spot];
            Check($"{s.Label} is fishable from its island's reachable land", Reachable().Any(t => CanStand(t.Item1 * T + 5f, t.Item2 * T + 7f) && Dist(t.Item1 * T + 5f, t.Item2 * T + 7f, s.X, s.Y) < s.R - 4));
        }
        foreach (var s in Data.Spots.Where(s => s.Biome == "habagat"))
        {
            var allowed = Data.Common[s.Id].Select(f => f.Id).Concat(Data.Odd.Where(o => o.Spot == s.Id).Select(o => o.Id)).Append("chest").ToHashSet();
            Check($"{s.Label} only gives its own catches", Enumerable.Range(0, 400).All(_ => allowed.Contains(RollCatch(s.Id).Id)));
        }
        // No older fish changed colour: they're still handed out in the same order, with Habagat's after them all.
        int ti = 0;
        bool same = true;
        void Tints(IEnumerable<CommonFish> fs) { foreach (var f in fs) same &= Items.ById[f.Id].Tint == Items.TintAt(ti++); }
        Tints(Data.Spots.Where(s => s.Scene != "sea" && s.Biome != "habagat").SelectMany(s => Data.Common[s.Id]));
        Tints(Data.PotCatch.Where(p => p.Key != "habagat").SelectMany(p => p.Value));
        Tints(Data.Spots.Where(s => s.Scene == "sea" && s.Biome != "habagat").SelectMany(s => Data.Common[s.Id]));
        Check("no older fish changed colour", same);
        var habagatFish = Data.Spots.Where(s => s.Biome == "habagat").SelectMany(s => Data.Common[s.Id]).Concat(Data.PotCatch["habagat"]).ToList();
        Check($"all {habagatFish.Count} Habagat catches are in the bag's item list with pictures", habagatFish.All(f => Items.ById.ContainsKey(f.Id) && FishArt.Looks.ContainsKey(f.Id)));
        state.reqDone = Items.RequestChain.Length;
        var ids = habagatFish.Select(f => f.Id).ToHashSet();
        Check("Tomas won't ask for Habagat fish before you've found it", Enumerable.Range(0, 300).All(_ => !ids.Contains(NextRequest().item)));

        state.inv["boat"] = 1;
        player.X = SaltJettyX; player.Y = SaltJettyY; player.Face = "right"; yield return 3;
        Check($"before you've found Habagat, Pip's jetty still sails straight to the atoll ({target?.Label})", target?.Type == "sail" && target.Label == "Sail to Starfall Atoll");
        // Steering south from the atoll's side of the sea.
        state.aboard = true; player.X = state.boatX = 1000; player.Y = state.boatY = 480; player.Face = "down"; yield return 2;
        Inp.Hold(KeyboardKey.Down, true);
        for (int i = 0; i < 200 && player.Y < 600; i++) yield return 0;
        Inp.Hold(KeyboardKey.Down, false); yield return 3;
        Check($"sailing south finds the Habagat islands ({player.X:0},{player.Y:0})", state.Hinted("habagat") && Aboard);
        Check($"over its deep water you fish the Habagat Sea (target {target?.Id})", target?.Type == "spot" && target.Id == "habagatsea");
        var seaAllowed = Data.Common["habagatsea"].Select(f => f.Id).Append("chest").ToHashSet();
        Check("the Habagat Sea only gives its own catches", Enumerable.Range(0, 400).All(_ => seaAllowed.Contains(RollCatch("habagatsea").Id)));
        Check("Tomas can ask for Habagat fish once you've been", Enumerable.Range(0, 600).Any(_ => ids.Contains(NextRequest().item)));
        Check("but never for the squid before the lighthouse is lit", Enumerable.Range(0, 600).All(_ => NextRequest().item != "pusit"));
        pendingShot = "120-habagat-sea"; yield return 2;

        // The Sail route, through the real panel.
        state.aboard = false; state.boatAt = "saltmere"; state.boatX = state.boatY = 0;
        player.X = SaltJettyX; player.Y = SaltJettyY; player.Face = "right"; yield return 3;
        Check($"once found, Pip's jetty asks where to sail ({target?.Label})", target?.Type == "sail" && target.Label == "Sail somewhere");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E opens the Set sail panel", mode == "panel" && panel == "voyage");
        pendingShot = "121-set-sail"; yield return 2;
        bool clicked = ClickButton("voyage:asinan"); yield return 3; Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 120 && mode == "fade"; i++) yield return 1;
        yield return 3;
        Check($"clicking Asinan sails there and moors the boat at its landing ({player.X:0},{player.Y:0})",
            clicked && mode == "play" && Dist(player.X, player.Y, AsinanJettyX, AsinanJettyY) < 10 && state.boatAt == "asinan" && BoatCanStand(BoatPosition().x, BoatPosition().y) && CanStand(player.X, player.Y));
        Check("the Asinan landing offers the helm too", target?.Type == "sail" && target.AltType == "launch");
        Inp.Tap(KeyboardKey.F); yield return 4;
        Check("F takes the helm at the Asinan landing", Aboard && BoatCanStand(player.X, player.Y));
        float sx0 = player.X;
        Inp.Hold(KeyboardKey.Right, true); for (int i = 0; i < 90 && player.X < sx0 + 60; i++) yield return 0; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"and you can sail out along the channel from there ({player.X:0},{player.Y:0})", Aboard && player.X > sx0 + 50 && TileUnder(player.X, player.Y) == '~');
        // Coming alongside the Parola pier and the sanctuary's platform, R lands; beside the boat, R boards again.
        foreach (var (name, bx, by) in new[] { ("the Parola pier", 1117f, 548f), ("the sanctuary's watch platform", 2215f, 707f) })
        {
            state.aboard = true; player.X = state.boatX = bx; player.Y = state.boatY = by; yield return 3;
            Inp.Tap(KeyboardKey.R); yield return 4;
            Check($"R lands on {name} ({player.X:0},{player.Y:0})", !Aboard && CanStand(player.X, player.Y) && TileUnder(player.X, player.Y) is 'b' or 'd');
            Inp.Tap(KeyboardKey.R); yield return 4;
            Check($"and boards again from {name}", Aboard);
            Inp.Tap(KeyboardKey.R); yield return 3;
        }
        state.aboard = false;

        /* ---------- Asinan: salt, drying racks, calamansi ---------- */
        Note("Asinan: the salt beds and drying fish");
        ClearSkies(); SetNight(false);
        player.X = 225; player.Y = 658; player.Face = "up"; yield return 3;
        Check($"Manang Rosa is there to talk to ({target?.Label})", target?.Type == "habagatfolk" && target.Id == "rosa");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        player.X = 155; player.Y = 661; player.Face = "up"; yield return 3;
        Check($"by the salt beds you can rake them ({target?.Label})", target?.Type == "saltbed");
        pendingShot = "122-salt-beds"; yield return 2;
        int salt = Has("salt");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"raking on a dry day gives salt, and the first time a strange scale ({Has("salt") - salt})", Has("salt") == salt + 3 && state.Hinted("bk:scale"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("only once a day", Has("salt") == salt + 3);
        state.day++; state.forecast = new() { new WeatherSpell { at = 0, w = "rain" } }; state.weather = planned = "rain"; SnapWeather();
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a day that rained from the morning gives no salt ({toastMsg})", Has("salt") == salt + 3 && toastMsg.Contains("rain"));
        state.forecast = new() { new WeatherSpell { at = 0, w = "rain" }, new WeatherSpell { at = 300, w = "clear" } }; state.weather = planned = "clear"; state.clock = 13 * 60;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("nor does one that rained this morning and cleared up", Has("salt") == salt + 3);
        state.day++; ClearSkies();
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the next dry day it does", Has("salt") == salt + 6);

        // A drying rack, built with the tenth piece in the build bar (the 0 key).
        state.inv["wood"] = 10; state.inv["stone"] = 10;
        player.X = 255; player.Y = 677; player.Face = "right"; yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        Inp.Tap(KeyboardKey.Zero); yield return 3;
        Check($"0 picks the tenth piece, the drying rack ({buildTool})", mode == "build" && buildTool == "dryrack");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        var rack = state.builds.FirstOrDefault(b => b.id == "dryrack");
        Check("the rack is built", rack != null && mode == "play");
        if (rack != null)
        {
            state.inv["danggit"] = 3; state.inv["salt"] = 5;
            player.X = rack.x * T - 5; player.Y = rack.y * T + 7; player.Face = "right"; yield return 3;
            Check($"facing it you can lay fish out ({target?.Label})", target?.Type == "rack");
            Inp.Tap(KeyboardKey.E); yield return 3;
            var load = Rack(rack);
            Check("three fish are salted and laid out", load?.fish.Count == 3 && Has("danggit") == 0 && Has("salt") == 2);
            pendingShot = "123-drying-rack"; yield return 2;
            state.clock = 12 * 60;
            state.forecast = new() { new WeatherSpell { at = 0, w = "rain" } }; state.weather = planned = "rain";
            Advance(120, quiet: true);
            Check($"rain stops them drying ({load?.dry:0} minutes)", load != null && load.dry == 0);
            ClearSkies(); state.clock = 12 * 60;
            Advance(240, quiet: true);
            Check($"clear daylight dries them ({load?.dry:0} of {DryGoal})", load != null && Math.Abs(load.dry - 240) < 0.5f);
            Advance(240, quiet: true);
            Check($"but only until six in the evening ({load?.dry:0})", load != null && Math.Abs(load.dry - 360) < 0.5f);
            yield return 3;
            Check($"once dry, E takes the daing down ({target?.Label})", target?.Type == "rack" && RackDone(load));
            Inp.Tap(KeyboardKey.E); yield return 3;
            Check("three daing", Has("daing") == 3 && Rack(rack) == null);
            // A rest from 17:00 to 21:00 dries them for one hour only; taking the rack down gives the fish back.
            state.inv["sapsap"] = 2; state.clock = 17 * 60; yield return 2;
            Inp.Tap(KeyboardKey.E); yield return 3;
            SkipTo(21 * 60);
            Check($"resting into the evening dries them only until six ({Rack(rack)?.dry:0})", Rack(rack) is RackLoad r2 && Math.Abs(r2.dry - 60) < 0.5f);
            state.builds.Remove(rack); PackUp(rack); ReindexBuilds();
            Check("taking the rack down gives the fish back", Has("sapsap") == 2 && Rack(rack) == null);
            SetNight(false);
        }
        // Calamansi from Habagat's bushes, and the new dishes.
        var bush = Enumerable.Range(0, COLS * ROWS).Select(i => (x: i % COLS, y: i / COLS)).FirstOrDefault(p => p.y >= HabagatTop && p.x < 40 && worldMap[p.y, p.x] == 'y');
        if (bush.y > 0 && StandBeside(bush.x, bush.y))
        {
            yield return 3;
            Check($"Habagat's bushes are calamansi ({target?.Label})", target?.Label == "Pick calamansi");
            Inp.Tap(KeyboardKey.E); yield return 3;
            Check("and give calamansi", Has("calamansi") >= 2);
        }
        else Check("there's a calamansi bush on Asinan", false);
        state.inv["calamansi"] = 5; state.inv["coconut"] = 1; state.inv["salt"] = 3; state.inv["bisugo"] = 5;
        foreach (var dish in new[] { "kinilaw", "sinigang", "ginataan" })
        {
            Craft(Items.Recipes.First(r => r.Out == dish));
            Check($"{Items.ById[dish].Name} can be cooked at a stove", Has(dish) == 1 && Items.StationMakes("stove", Items.Recipes.First(r => r.Out == dish)));
        }
        // Habagat's crab pots bring up all three of its catches.
        var water = Enumerable.Range(0, COLS * ROWS).Select(i => (x: i % COLS, y: i / COLS)).First(p => p.y >= HabagatTop && p.x < 40 && worldMap[p.y, p.x] == 'w');
        var pot = new Build { id = "crabpot", x = water.x, y = water.y };
        state.builds.Add(pot); ReindexBuilds();
        foreach (var f in Data.PotCatch["habagat"]) state.commons.Remove(f.Id);
        for (int i = 0; i < 60; i++) { state.pots[$"{pot.x},{pot.y}"] = state.day - 1; HaulPot(pot); }
        Check("Habagat's pots bring up mud crabs, spanner crabs and tiger prawns", Data.PotCatch["habagat"].All(f => state.commons.GetValueOrDefault(f.Id) > 0));
        state.builds.Remove(pot); ReindexBuilds();
        int goats = Enumerable.Range(0, 4000).Count(_ => RollCatch("asinan").Id == "goat");
        Check($"now and then a goat ends up on the hook at Asinan ({goats} of 4000)", goats > 150 && goats < 450);

        /* ---------- Gleaning ---------- */
        Note("Gleaning at low tide");
        state.loose.RemoveAll(l => l.kind == "glean");
        player.X = 200; player.Y = 668; state.clock = 12 * 60; yield return 2;
        FillLoose();
        Check("nothing to glean at high tide", LooseCount("glean") == 0);
        state.clock = 6 * 60 + 30; yield return 2;
        FillLoose();
        Check($"at low tide the flats have finds ({LooseCount("glean")})", LooseCount("glean") > 0 && state.loose.Where(l => l.kind == "glean").All(l => GleanGround(l.tx, l.ty)));
        var find = state.loose.FirstOrDefault(l => l.kind == "glean");
        int shells = Has("cowrie") + Has("sea_urchin") + Has("sea_grapes");
        if (find != null) { player.X = find.x; player.Y = find.y + 2; for (int i = 0; i < 20 && state.loose.Contains(find); i++) yield return 1; }
        Check("walking over one picks it up", Has("cowrie") + Has("sea_urchin") + Has("sea_grapes") == shells + 1);
        pendingShot = "124-gleaning"; yield return 2;
        state.gleanTide = GleanTide; state.gleaned = GleanQuota - 1;
        state.loose.RemoveAll(l => l.kind == "glean");
        state.loose.Add(new Loose { kind = "glean", tx = (int)(player.X / T), ty = (int)(player.Y / T), x = player.X, y = player.Y - 1 });
        yield return 3;
        FillLoose();
        Check("each low tide has only so much to glean", state.gleaned == GleanQuota && LooseCount("glean") == 0);
        state.gleaned = 0;
        FillLoose();
        state.clock = 12 * 60; looseTimer = 99; yield return 3;
        Check("the tide coming in takes what's left", LooseCount("glean") == 0);

        /* ---------- Sungka ---------- */
        Note("Sungka with Lola Pacing");
        int[] Board() { var b = new int[16]; for (int i = 0; i < 16; i++) b[i] = i is 7 or 15 ? 0 : 7; return b; }
        var bd = Board();
        bool again = SungkaSow(bd, 0, false, out int last);
        Check("seven shells from the first house end in your head: go again", again && last == 7 && bd[7] == 1 && bd[0] == 0 && bd[6] == 8);
        bd = new int[16]; bd[2] = 1; bd[11] = 5;
        SungkaSow(bd, 2, false, out _);
        Check("ending in an empty house of yours takes Lola's house opposite", bd[7] == 6 && bd[11] == 0 && bd[3] == 0);
        bd = new int[16]; bd[0] = 2; bd[2] = 3;
        again = SungkaSow(bd, 0, false, out last);
        Check("ending where there are shells already picks them up and keeps sowing", !again && last == 6 && bd[2] == 0 && bd[3] == 1 && bd[6] == 1);
        bd = new int[16]; bd[6] = 10;
        SungkaSow(bd, 6, false, out last);
        Check("sowing skips Lola's head", bd[15] == 0 && last == 1);
        player.X = HabagatFolk.First(n => n.id == "pacing").x; player.Y = HabagatFolk.First(n => n.id == "pacing").y + 12; player.Face = "up"; yield return 3;
        Check($"Lola Pacing offers a game ({target?.Label})", target?.Type == "habagatfolk" && target.Id == "pacing");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("the board opens", mode == "panel" && panel == "sungka");
        clicked = ClickButton("sungka:0"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"clicking your first house sows it into your head, and it's your go again ({sungka?[7]})", clicked && sungka[7] == 1 && !sungkaLolaTurn);
        pendingShot = "125-sungka"; yield return 2;
        for (int i = 0; i < 900 && !sungkaOver; i++)
        {
            sungkaWait = 0;   // no need to wait for Lola to think in a test
            if (!sungkaLolaTurn)
            {
                int pick = Enumerable.Range(0, 7).Where(p => sungka[p] > 0).OrderByDescending(p => (p + sungka[p]) % 15 == 7 ? 1 : 0).FirstOrDefault(-1);
                if (pick >= 0) { ClickButton($"sungka:{pick}"); yield return 3; Inp.ScriptMouse = Offscreen; }
            }
            yield return 1;
        }
        Check($"a whole game plays out, every shell ending in a head ({sungka[7]} to {sungka[15]})", sungkaOver && sungka[7] + sungka[15] == 98);
        pendingShot = "126-sungka-over"; yield return 2;
        ClickButton("Close"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("after the first game Lola tells the story of the seven moons", state.Hinted("bk:tale") && mode == "play");

        /* ---------- The islets and the regatta ---------- */
        Note("Daang Pulo: charting the islets, and Dado's regatta");
        state.charted.RemoveAll(r => r.StartsWith("habagat:islet:")); state.hinted.Remove("isletsCharted");
        player.X = 655; player.Y = 627; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Dado won't race until every islet is charted", !raceArmed);
        state.aboard = true;
        foreach (var isl in Islets) { player.X = state.boatX = isl.cx * T + 30; player.Y = state.boatY = isl.cy * T; yield return 2; }
        player.X = state.boatX = 645; player.Y = state.boatY = 545; yield return 2;
        Check($"sailing close by every islet charts them all ({IsletsCharted} of {IsletCount})", IsletsCharted == IsletCount && state.Hinted("isletsCharted"));
        state.aboard = false; player.X = 655; player.Y = 627; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("then Dado sets up a race", raceArmed);
        state.aboard = true; player.X = state.boatX = 590; player.Y = state.boatY = 522; player.Face = "right"; yield return 3;
        pendingShot = "127-regatta-start"; yield return 2;
        foreach (int f in SailTo(RaceGates[0].x, RaceGates[0].y)) yield return f;
        Check("crossing the start buoys starts the clock", race != null && race.Next == 1);
        for (int g = 1; g <= RaceGates.Length && race != null; g++)
        {
            var (gx, gy) = RaceGates[g % RaceGates.Length];
            foreach (int f in SailTo(gx, gy)) yield return f;
            if (g == 4) pendingShot = "128-regatta";
        }
        Check($"steering round the course through every gate finishes the race ({RaceTime(state.regattaBest)})", race == null && state.regattaBest > 0 && state.regattaBest < RaceLimit);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        if (state.regattaBest <= SilverTime) Check("beating Dado's lolo's record wins the old agong", Has("agong") == 1 && state.Hinted("bk:agong"));
        else { Check($"the autopilot beat the record ({state.regattaBest:0.0} s)", false); Give("agong"); state.hinted["bk:agong"] = true; }
        // Landing mid-race calls it off.
        raceArmed = true; player.X = state.boatX = 590; player.Y = state.boatY = 522; yield return 2;
        foreach (int f in SailTo(RaceGates[0].x, RaceGates[0].y)) yield return f;
        player.X = state.boatX = 628; player.Y = state.boatY = 572; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check($"landing in the middle of a race calls it off ({toastMsg})", race == null && !Aboard);

        /* ---------- Parola ---------- */
        Note("Parola: the lighthouse and its squid");
        state.aboard = false; SetNight(true);
        Check("before the lamp is lit, no squid or tarpon come to the pier", Enumerable.Range(0, 400).All(_ => RollCatch("parola").Id is not ("pusit" or "buan_buan")));
        state.day = 10;   // (day + 3) % 8 != 4: not a full moon tonight
        player.X = 1190; player.Y = 625; yield return 3;
        Check("on an ordinary night at Parola nothing stirs", eclipse == null);
        SetNight(false);
        var celso = HabagatFolk.First(n => n.id == "celso");
        player.X = celso.x; player.Y = celso.y + 12; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        state.inv["wood"] = 10; state.inv["stone"] = 4; state.inv["iron_bar"] = 2; state.inv["copper_bar"] = 2; state.inv["crystal"] = 2;
        for (int stage = 1; stage <= 3; stage++)
        {
            yield return 2;
            Check($"Tatay Celso takes what stage {stage} needs ({target?.Label})", target?.Label.StartsWith("Give Tatay Celso") == true);
            Inp.Tap(KeyboardKey.E); yield return 3;
            while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
            Check($"stage {stage} of the lighthouse is mended", state.parola == stage);
        }
        Check("the lamp is lit and he gives you the keeper's log", state.Hinted("parolaLit") && state.Hinted("bk:log"));
        SetNight(true);
        Check("now squid and tarpon come to the pier at night", Enumerable.Range(0, 400).Any(_ => RollCatch("parola").Id == "pusit"));
        player.X = 1150; player.Y = 610; yield return 10;
        pendingShot = "129-parola-lit"; yield return 2;

        /* ---------- The eclipse ---------- */
        Note("The vanishing moon: Bakunawa");
        Check("every clue found", MoonClueCount == Data.MoonClues.Length && EclipseReady);
        state.day = 9;   // a full moon
        player.X = 1190; player.Y = 625; player.Face = "up"; yield return 3;
        Check("on a full-moon night at Parola, Bakunawa rises", eclipse != null && mode == "dialogue");
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        for (int i = 0; i < 400 && eclipse?.Phase == "rise"; i++) yield return 1;
        Check("it rises, then the noise begins", eclipse?.Phase == "bang");
        pendingShot = "130-bakunawa"; yield return 2;
        Settings.Data.dayLength = 24;
        float clock = state.clock, px0 = player.X;
        Inp.Hold(KeyboardKey.Left, true); yield return 30; Inp.Hold(KeyboardKey.Left, false);
        Check("while it holds the moon the clock stops and you stay put", state.clock == clock && player.X == px0);
        Settings.Data.dayLength = 0;
        OpenPause(); float t0 = eclipse.T; yield return 20;
        Check("pausing pauses it", mode == "pause" && eclipse.T == t0);
        ClosePause(); yield return 2;
        // Off the beat, the noise falls apart.
        for (int i = 0; i < 120 && MathF.Abs(eclipse.Beat - BeatLen / 2) > 0.05f; i++) yield return 0;
        int misses = eclipse.Misses;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("a strike off the beat misses", eclipse.Misses == misses + 1);
        // Saving and loading in the middle: it isn't saved, and starts again that night.
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; yield return 3;
        Check("reloading mid-eclipse brings it back that same night", eclipse != null && mode == "dialogue");
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        for (int i = 0; i < 400 && eclipse?.Phase == "rise"; i++) yield return 1;
        bool shot = false;
        for (int i = 0; i < 3000 && eclipse?.Phase == "bang"; i++)
        {
            // Strike as the ring meets the gong.
            if (!eclipse.Judged && (eclipse.Beat < 0.06f || eclipse.Beat > BeatLen - 0.06f)) { Inp.Tap(KeyboardKey.E); yield return 2; }
            else yield return 0;
            if (!shot && eclipse?.Hits >= 8 && eclipse.Beat > 0.05f && eclipse.Beat < 0.15f) { shot = true; pendingShot = "130b-bakunawa-noise"; }
        }
        Check($"striking on the beat makes enough noise ({eclipse?.Hits} hits)", eclipse?.Phase is "spit" or "done" || mode == "moon");
        for (int i = 0; i < 400 && mode != "moon"; i++) yield return 1;
        Check("Bakunawa gives the moon back", mode == "moon");
        pendingShot = "131-moon-card"; yield return 2;
        yield return 40;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("the case is closed: the moon is safe and you have the Moonscale charm", MoonReturned && Has("moon_charm") == 1 && eclipse == null && mode == "play");
        var king = Data.FishById["haring_buan"];
        Check("under a full moon the king of the tarpon will now take a live tamban", Bites(king, "tamban") && !Bites(king, "bait"));
        yield return 3;
        Check("and no eclipse comes again", eclipse == null);

        /* ---------- The sanctuary ---------- */
        Note("The marine sanctuary");
        SetNight(false); ClearSkies();
        seaLife.Clear(); state.aboard = true; player.X = state.boatX = 2160; player.Y = state.boatY = 690; player.Face = "right"; yield return 2;
        seaLife.Clear();
        seaLife.AddRange(new[]
        {
            new SeaCreature { Kind = "pawikan", X = 2060, Y = 690, ToX = 2060, ToY = 690, Wait = 999 },
            new SeaCreature { Kind = "dugong", X = 2100, Y = 705, ToX = 2100, ToY = 705, Wait = 999 },
            new SeaCreature { Kind = "butanding", X = 2300, Y = 680, ToX = 2300, ToY = 680, Wait = 999 }
        });
        yield return 2;
        Check($"inside the buoys there's no fishing or trolling ({target?.Label})", target?.Type == "info" && target.AltType == null);
        var grass = Enumerable.Range(0, COLS * ROWS).Select(i => (x: i % COLS, y: i / COLS)).First(p => p.x >= 198 && p.y >= 63 && worldMap[p.y, p.x] == 'w' && InSanctuary(p.x * T + 5, p.y * T + 5));
        state.aboard = false; player.X = grass.x * T + 5; player.Y = grass.y * T - 8;
        Check($"no crab pots inside it ({PlaceProblem(Data.BuildById["crabpot"], grass.x, grass.y)})", PlaceProblem(Data.BuildById["crabpot"], grass.x, grass.y).Contains("sanctuary"));
        state.aboard = true;
        foreach (var c in seaLife.ToList())
        {
            player.X = state.boatX = c.X - 20; player.Y = state.boatY = c.Y; player.Face = "right"; yield return 2;
            if (c.Kind == "butanding") pendingShot = "132-butanding";
            if (c.Kind == "pawikan") pendingShot = "133-pawikan";
            yield return 2;
            Check($"alongside, the boat offers to watch the {c.Kind} ({target?.Label})", target?.Type == "watch" && target.Ref == c);
            Inp.Tap(KeyboardKey.E); yield return 3;
            Check($"watching the {c.Kind} records a sighting", state.sightings.GetValueOrDefault(c.Kind) == 1);
        }
        SetNight(true);
        Check("the whale shark only comes up by day", !VisibleSeaLife().Any(c => c.Kind == "butanding") && VisibleSeaLife().Any(c => c.Kind == "pawikan"));
        SetNight(false);
        state.aboard = true; player.X = state.boatX = 2215; player.Y = state.boatY = 707; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 3;
        player.X = JoyX - 6; player.Y = JoyY + 8; player.Face = "up"; yield return 3;
        int coins = state.coins;
        Check($"Bantay Joy is on her platform ({target?.Label})", target?.Type == "joy");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"having seen all three, she pays 150 coins ({state.coins - coins})", state.coins == coins + 150 && state.Hinted("sanctuaryReward"));
        Check("standing on the platform charts no island", !state.charted.Any(r => r.StartsWith("amihan:")));
        // Just outside the buoys, fish spill over: quicker bites, and better luck.
        state.aboard = true; player.X = state.boatX = 1960; player.Y = state.boatY = 690; player.Face = "left"; schools.Clear(); yield return 3;
        state.inv.Remove("bait"); state.inv.Remove("glow_bait"); state.inv.Remove("tamban");
        float near = 0, far = 0;
        for (int i = 0; i < 120; i++)
        {
            seaSpot = (player.X - 30, player.Y - 6); Cast("amihansea", 0.7f); near += fish.Timer; bool spill = fish.Spill; fish = null; mode = "play";
            if (!spill) { Check("a cast just outside the buoys is in the spillover", false); break; }
            seaSpot = (1500, 400); Cast("amihansea", 0.7f); far += fish.Timer; fish = null; mode = "play";
        }
        Check($"fish bite quicker just outside the sanctuary ({near / 120:0.00}s against {far / 120:0.00}s)", near < far * 0.92f);

        /* ---------- Saves, rescue and fainting ---------- */
        Note("Saves, rescues and fainting in Habagat");
        SaveFile.Clear(3);
        File.WriteAllText(SaveFile.SlotPath(3), "{\"created\":true,\"inv\":{\"rod_old\":1}}");
        var old = SaveFile.Read(3);
        Check("older saves load with the new fields' defaults", old != null && old.racks != null && old.sightings != null && old.parola == 0 && old.gleanTide == "" && old.saltDay == 0);
        SaveFile.Clear(3);
        state.aboard = false; state.boatAt = "saltmere"; state.boatX = 225; state.boatY = 650;   // what used to be open sea is Asinan now
        player.X = 160; player.Y = 115; Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; yield return 3;
        Check($"a boat left where Asinan now stands is found at Pip's jetty ({toastMsg})", state.boatX == 0 && state.boatAt == "saltmere" && toastMsg.Contains("New islands"));
        player.X = 85; player.Y = 668; player.Face = "up"; state.hp = 1; state.inv["boat"] = 1;
        TestBite("asinan", "pagi"); Hook();
        reel.Progress = 0.8f; reel.AttackTimer = 0;
        Inp.Hold(KeyboardKey.E, true); yield return 150; Inp.Hold(KeyboardKey.E, false);
        for (int i = 0; i < 120 && mode == "fade"; i++) yield return 1;
        yield return 2;
        Check($"fainting in Habagat wakes you at Manang Rosa's ({player.X:0},{player.Y:0})", mode == "play" && Dist(player.X, player.Y, AsinanWakeX, AsinanWakeY) < 2 && CanStand(player.X, player.Y) && state.hp >= 35);
        Check("with your boat at the Asinan landing", state.boatAt == "asinan" && state.boatX == 0 && BoatCanStand(BoatPosition().x, BoatPosition().y));
        player.X = 1190; player.Y = 625; Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; yield return 3;
        Check("saving on Parola reloads there", Dist(player.X, player.Y, 1190, 625) < 0.1f && InHabagat);

        /* ---------- The Fish log, the Case board and the chart ---------- */
        Note("The Fish log, the Case board and the chart");
        TogglePanel("dex"); dexTab = "log"; yield return 4;
        clicked = ClickButton("Habagat"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the Fish log has a Habagat page", clicked && logPage == Array.FindIndex(Data.Biomes, b => b.Id == "habagat"));
        pendingShot = "134-fishlog-habagat"; yield return 2;
        ClickButton("Legends & more"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        pendingShot = "135-fishlog-legends"; yield return 2;
        ClosePanels(); yield return 2;
        TogglePanel("case"); yield return 3;
        clicked = ClickButton("The vanishing moon"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the Case board has a second case", clicked && caseTab == "habagat" && mode == "panel");
        pendingShot = "136-case-moon"; yield return 2;
        ClosePanels(); yield return 2;
        player.X = 200; player.Y = 668; yield return 2;
        Inp.Tap(KeyboardKey.Tab); yield return 6;
        Check("on Habagat the map opens on the west chart", mode == "panel" && panel == "map" && !chartEast);
        pendingShot = "137-chart-habagat"; yield return 2;
        ClickButton("Amihan"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "138-chart-sanctuary"; yield return 2;
        ClosePanels(); yield return 2;
        foreach (int f in HabagatReviewScript()) yield return f;
    }

    // Regressions from Codex's review of 1.13.
    IEnumerable<int> HabagatReviewScript()
    {
        Note("Codex review: gleaning at the quota, the Parola pier's region, drying with the aquarium set, racing, Tidemane and the buoys");
        ClearSkies(); state.aboard = state.riding = false;
        // The tenth find, with another still on the flats and driftwood about: no crash, and the rest washes away.
        player.X = 200; player.Y = 668; state.clock = 6 * 60 + 30;
        state.gleanTide = GleanTide; state.gleaned = GleanQuota - 1;
        state.loose.RemoveAll(l => l.kind == "glean");
        state.loose.Add(new Loose { kind = "wood", tx = 18, ty = 66, x = 185, y = 664 });
        state.loose.Add(new Loose { kind = "glean", tx = 30, ty = 67, x = 305, y = 675 });
        state.loose.Add(new Loose { kind = "glean", tx = 20, ty = 66, x = player.X, y = player.Y - 1 });
        yield return 3;
        Check("picking up the tide's last find with another still out doesn't crash, and clears the flats", state.gleaned == GleanQuota && LooseCount("glean") == 0);
        // At high tide a find can't be picked up, even if it's still there for a moment.
        state.gleaned = 0; state.clock = 12 * 60;
        int finds = Has("cowrie") + Has("sea_urchin") + Has("sea_grapes");
        state.loose.Add(new Loose { kind = "glean", tx = 20, ty = 66, x = player.X, y = player.Y - 1 });
        yield return 3;
        Check("at high tide nothing more is gleaned", Has("cowrie") + Has("sea_urchin") + Has("sea_grapes") == finds && LooseCount("glean") == 0);
        // The Parola pier reaches past HabagatTop: it and its spot are still Parola's.
        Check($"the Parola spot belongs to Parola on the chart ({SpotRegion(Data.SpotById["parola"])})", SpotRegion(Data.SpotById["parola"]) == "habagat:Parola");
        state.hinted.Remove("visitedAtoll"); state.charted.Remove("atoll"); lastBiome = 6;
        player.X = 1135; player.Y = 536; yield return 3;
        Check("standing at the end of the Parola pier doesn't count as a visit to the atoll", !state.Hinted("visitedAtoll") && !state.charted.Contains("atoll") && PlayerBiome() == 6);
        // Finishing the aquarium set halfway through speeds the rest up; it doesn't finish the fish there and then.
        var rack = new Build { id = "dryrack", x = 27, y = 67 };
        state.builds.Add(rack); ReindexBuilds();
        state.racks[RackKey(rack)] = new RackLoad { fish = new() { "danggit", "sapsap" }, dry = 180 };
        var tank = Tank("house:test|1,1");
        tank.AddRange(new[] { "danggit", "sapsap", "labahita", "tamban" });
        Check("finishing the aquarium set doesn't make half-dried fish ready", SetActive("habagat") && !RackDone(Rack(rack)));
        state.clock = 12 * 60; Advance(90, quiet: true);
        Check($"but they dry twice as fast while it's on show ({Rack(rack).dry:0})", RackDone(Rack(rack)));
        state.tanks.Remove("house:test|1,1"); state.builds.Remove(rack); state.racks.Remove(RackKey(rack)); ReindexBuilds();
        // No stopping to fish in the middle of a race.
        raceArmed = true; state.aboard = true; player.X = state.boatX = RaceGates[0].x; player.Y = state.boatY = RaceGates[0].y; yield return 3;
        Check($"racing, the helm doesn't offer fishing ({target?.Label})", race != null && target?.Type == "info" && target.Label.Contains("Racing"));
        race = null; raceArmed = false;
        // Tidemane left waiting where the salt beds now stand comes out of them.
        state.aboard = false; state.tamed = true; state.riding = false; state.mountX = 155; state.mountY = 636;
        player.X = 200; player.Y = 668; Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; yield return 3;
        Check($"Tidemane isn't left inside the salt beds ({state.mountX:0},{state.mountY:0})", !HabagatSolids().Any(b => b.Overlaps(new Box(state.mountX - 6, state.mountY - 4, 12, 4))));
        state.tamed = false;
        // A long cast from just outside the buoys, facing in, lands outside them.
        state.aboard = true; player.X = state.boatX = 1960; player.Y = state.boatY = 690; player.Face = "right"; schools.Clear(); yield return 3;
        if (target?.Type == "spot")
        {
            Cast(target.Id, 1f);
            Check($"a long cast toward the sanctuary lands outside the buoys ({fish?.Tx:0},{fish?.Ty:0})", fish != null && !InSanctuary(fish.Tx, fish.Ty));
            fish = null; mode = "play";
        }
        else Check($"just outside the buoys you can fish ({target?.Label})", false);
        // Right on the line, feet outside but the rod over the buoys, it's no fishing either.
        player.X = state.boatX = 2220; player.Y = state.boatY = 742; player.Face = "up"; yield return 3;
        Check($"with the rod over the buoy line the helm offers no fishing ({target?.Label})", target?.Type != "spot" && !InSanctuary(player.X, player.Y));
        seaSpot = (2220, 700); Cast("amihansea", 1f);
        Check($"and a cast aimed inside from there never lands inside ({fish?.Tx:0},{fish?.Ty:0})", fish == null || !InSanctuary(fish.Tx, fish.Ty));
        fish = null; mode = "play";
        // A trolling line out when the race starts is wound in.
        state.inv["spinner_lure"] = 1; schools.Clear();
        player.X = state.boatX = 600; player.Y = state.boatY = 522; player.Face = "right"; yield return 3;
        ToggleTroll(); raceArmed = true; yield return 2;
        player.X = state.boatX = RaceGates[0].x; player.Y = state.boatY = RaceGates[0].y; yield return 4;
        Check("a trolling line is wound in when the race starts", race != null && !trolling);
        race = null; raceArmed = false; state.aboard = false;
        // Two finds underfoot at once with one left in the tide's quota: only one counts.
        player.X = 200; player.Y = 668; state.clock = 6 * 60 + 30;
        state.gleanTide = GleanTide; state.gleaned = GleanQuota - 1;
        state.loose.RemoveAll(l => l.kind == "glean");
        int before = Has("cowrie") + Has("sea_urchin") + Has("sea_grapes");
        state.loose.Add(new Loose { kind = "glean", tx = 20, ty = 66, x = player.X, y = player.Y - 1 });
        state.loose.Add(new Loose { kind = "glean", tx = 20, ty = 66, x = player.X + 1, y = player.Y - 2 });
        yield return 3;
        Check($"two finds at once can't go over the tide's quota ({state.gleaned})", state.gleaned == GleanQuota && Has("cowrie") + Has("sea_urchin") + Has("sea_grapes") == before + 1);
        // Tidemane left in the salt beds, with you standing just west of them: it comes out somewhere clear.
        state.tamed = true; state.riding = false; state.mountX = 155; state.mountY = 636;
        player.X = 113; player.Y = 636; Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; yield return 3;
        Check($"Tidemane comes out clear of the salt beds even with you beside them ({state.mountX:0},{state.mountY:0})", !HabagatSolids().Any(b => b.Overlaps(new Box(state.mountX - 6, state.mountY - 4, 12, 4))));
        state.tamed = false;
        state.aboard = false;
    }

    // Steers the boat (with the scripted stick, the way a player would) along the shortest way through the water.
    IEnumerable<int> SailTo(float tx, float ty, int maxFrames = 1500)
    {
        var path = BoatPath(player.X, player.Y, tx, ty);
        int i = 0;
        // (It stops if something takes over, like a dialogue at the finish line.)
        for (int f = 0; f < maxFrames && Dist(player.X, player.Y, tx, ty) > 4 && mode == "play"; f++)
        {
            while (i < path.Count - 1 && Dist(player.X, player.Y, path[i].x, path[i].y) < 9) i++;
            var (wx, wy) = path.Count > 0 ? path[i] : (tx, ty);
            float dx = wx - player.X, dy = wy - player.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            Inp.ScriptStick = new Vector2(dx / d, dy / d);
            yield return 0;
        }
        Inp.ScriptStick = null;
    }

    // Shortest water route for the hull on a 5 px grid (eight directions), as a list of points.
    List<(float x, float y)> BoatPath(float fx, float fy, float tx, float ty)
    {
        const int S = 5;
        var start = ((int)MathF.Round(fx / S), (int)MathF.Round(fy / S)); var goal = ((int)MathF.Round(tx / S), (int)MathF.Round(ty / S));
        var dist = new Dictionary<(int, int), float> { [start] = 0 };
        var prev = new Dictionary<(int, int), (int, int)>();
        var pq = new PriorityQueue<(int x, int y), float>();
        pq.Enqueue(start, 0);
        int minX = Math.Min(start.Item1, goal.Item1) - 60, maxX = Math.Max(start.Item1, goal.Item1) + 60, minY = Math.Min(start.Item2, goal.Item2) - 60, maxY = Math.Max(start.Item2, goal.Item2) + 60;
        while (pq.TryDequeue(out var cur, out float d))
        {
            if (cur == goal) break;
            if (d > dist[cur]) continue;
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var n = (cur.x + ox, cur.y + oy);
                if (n.Item1 < minX || n.Item1 > maxX || n.Item2 < minY || n.Item2 > maxY) continue;
                float nd = d + (ox != 0 && oy != 0 ? 1.414f : 1);
                if (nd >= dist.GetValueOrDefault(n, float.MaxValue) || !BoatCanStand(n.Item1 * S, n.Item2 * S)) continue;
                dist[n] = nd; prev[n] = cur; pq.Enqueue(n, nd);
            }
        }
        var path = new List<(float x, float y)>();
        if (!dist.ContainsKey(goal)) return path;
        for (var p = goal; p != start; p = prev[p]) path.Add((p.Item1 * S, p.Item2 * S));
        path.Reverse();
        return path;
    }
}
#endif
