#if DEBUG
using Raylib_cs;

namespace Fesh;

// 1.19: the restless sea, story 3 on Amihan Village. FESH_SEA_TEST=1 runs only this (it still needs FESH_AUTOTEST and
// FESH_SAVE); the full play-through runs it after Platejaw's checks.
partial class Game
{
    IEnumerable<int> RestlessSeaScript()
    {
        Note("The restless sea: the tremor, the case, the drill and the evacuation");
        state = new State { created = true, look = new Look { name = "Sea watcher" } };
        state.flags.metTomas = true;
        state.hinted["amihan"] = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        state.charted?.Add("amihan:Amihan Village");
        chartEast = true;
        Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        state.day = 2;
        yield return 2;

        /* ---------- The places ---------- */
        bool Land(float x, float y) => Walkable(TileAt((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1) / T)));
        Check("the post, the sign spots, the children's spots and School Rise are all on open land, where a person can stand",
            Land(PostX, PostY) && CanStand(PostX, PostY + 8) && SignSpots.All(p => CanStand(p.x, p.y + 7)) && KidSpots.All(p => CanStand(p.x, p.y)) && CanStand(RiseX, RiseY + 5));
        // The wave's reach is authored: it never comes near School Rise, the school or Isay.
        var field = SeaField();
        int nearRise = 0;
        for (int y = (int)(RiseY - RiseR); y <= RiseY + RiseR; y++)
            for (int x = (int)(RiseX - RiseR); x <= RiseX + RiseR; x++)
            {
                short d = SeaFieldAt(x, y);
                if (Dist(x, y, RiseX, RiseY) < RiseR && d != short.MaxValue && d > -85) nearRise++;
            }
        Check($"no wave can reach School Rise ({nearRise} pixels within reach)", nearRise == 0);
        Check($"the flood can reach the beach by the landing ({SeaFieldAt(1532, 205)})", SeaFieldAt(1532, 205) is <= 0 and > -30);
        Check("the inland pond is never a source of the wave", PondTiles().All(t => SeaFieldAt(t.x * T + 5, t.y * T + 5) is short d && (d == short.MaxValue || d < 0)));

        /* ---------- Nothing before its time ---------- */
        player.X = 1580; player.Y = 205; player.Face = "down"; yield return 3;
        Check("before you've met Ma'am Isay, the ground stays still", tremor == null && !SeaStoryStarted);
        Check("and there's nothing in the journal or on the Case board yet", !Goals().Any(g => g.Id.StartsWith("sea:")) && !SeaStoryStarted);
        state.hinted["metIsay"] = true; yield return 3;
        Check($"meeting her starts the count, but not the same day (ready day {state.gifts.GetValueOrDefault("rs_ready")})", tremor == null && state.gifts.GetValueOrDefault("rs_ready") == 2);
        state.day = 3; SetNight(true); yield return 3;
        Check("not at night", tremor == null);
        SetNight(false); state.weather = "storm"; yield return 3;
        Check("not in a storm", tremor == null);
        ClearSkies();
        player.X = 1440; player.Y = 205; yield return 3;
        Check("not out at sea", tremor == null);

        /* ---------- The tremor ---------- */
        var palm = trees.Where(p => p.kind == 'h' && RegionOf(p.x * T + 5, p.y * T + 5) == "amihan:Amihan Village")
            .OrderBy(p => Dist(p.x * T + 5, p.y * T + 8, 1600, 200)).First();
        player.X = palm.x * T + 5; player.Y = palm.y * T + 18; player.Face = "up";
        if (!CanStand(player.X, player.Y)) player.Y += 4;
        yield return 3;
        Check($"standing on the island by day, the ground shakes ({tremor?.Len} s)", tremor != null && toastMsg.Contains("duck"));
        yield return 2;
        Check($"under a palm, it tells you to get into the open first (prompt: {prompt.Text})", target?.Type == "info" && prompt.Text.Contains("open"));
        player.X = 1580; player.Y = 205; yield return 3;
        Check($"in the open: duck, cover and hold (prompt: {prompt.Text})", target?.Type == "duck" && InTheOpen);
        float qx = player.X;
        Inp.Hold(KeyboardKey.E, true); Inp.Hold(KeyboardKey.Right, true);
        yield return 20;
        Check($"holding it, you crouch and stay put ({qx:0} -> {player.X:0}), and the view shakes", Ducking && player.X == qx && quake > 0);
        pendingShot = "s01-tremor-duck"; yield return 2;
        Inp.Hold(KeyboardKey.Right, false);
        for (int i = 0; i < 400 && tremor != null; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"when it stops, you're told you did it right ({toastMsg})", tremor == null && SeaStoryStarted && state.Hinted("rs:duckedTremor") && toastMsg.Contains("Well done"));
        Check("the bangus pond has drained", PondDry && PondClosed);
        Check("and the restless sea is in the journal, pointing at Ma'am Isay", Goals().FirstOrDefault(g => g.Id == "sea:case") is { HasPoint: true } g1 && g1.Text.Contains("Isay"));

        // The Case board has a third case, by a real click on its tab.
        TogglePanel("case"); yield return 3;
        bool tab = ClickButton("The restless sea"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the Case board's third tab opens by a real click ({caseTab})", tab && caseTab == "amihan" && mode == "panel");
        pendingShot = "s02-case-board"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- The clues ---------- */
        IEnumerable<int> TalkTo(string id, float ox, float oy, string face)
        {
            var s = IslanderWalk(id);
            player.X = s.X + ox; player.Y = s.Y + oy; player.Face = face;
            yield return 3;
            Inp.Tap(KeyboardKey.E); yield return 3;
        }
        IEnumerable<int> Finish() { for (int i = 0; i < 30 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 2; } }

        // The pond can't be fished while it's dry.
        player.X = 1650; player.Y = 213; player.Face = "down"; yield return 3;
        Check($"the drained pond can't be fished (prompt: {prompt.Text})", target?.Type == "info" && prompt.Text.Contains("dry"));
        player.X = CrackX; player.Y = CrackY + 4; player.Face = "right"; yield return 3;
        Check($"the crack in its bank (prompt: {prompt.Text})", target?.Type == "info" && prompt.Text.Contains("crack"));

        var isay = IslanderWalk("isay");
        foreach (var f in TalkTo("isay", -10, 9, "right")) yield return f;
        Check($"Ma'am Isay explains the earthquake (clue 1)", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("fault")) && state.Hinted("rs:carpio"));
        foreach (var f in Finish()) yield return f;
        Check("and then it's her usual lesson again, not the story", IsaySeaLabel() == null);

        // The first time, Lira asks for her supper dish (Rondalla.cs); that comes first, and the story's next.
        foreach (var f in TalkTo("lira", 0, 10, "up")) yield return f;
        Check("Lira's supper request still comes first", mode == "dialogue" && state.Hinted("liraSupperAsked"));
        foreach (var f in Finish()) yield return f;
        foreach (var f in TalkTo("lira", 0, 10, "up")) yield return f;
        Check("Lira tells her lola's Bernardo Carpio story", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("Bernardo Carpio") && l.T.Contains("Montalban")));
        foreach (var f in Finish()) yield return f;

        foreach (var f in TalkTo("niko", 0, 10, "up")) yield return f;
        Check("Niko tells his lola's berberoka story, and what really drained the pond (clue 2)",
            mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("berberoka") && l.T.Contains("stranded")) && dlg.Lines.Any(l => l.T.Contains("cracked")) && state.Hinted("rs:berberoka"));
        foreach (var f in Finish()) yield return f;
        Check("the pond's bank is a job in the journal", Goals().Any(g => g.Id == "sea:pond"));
        // A hand-in Niko asked for earlier still goes first while the bank waits for stone and wood (Codex: the repair
        // reminder used to swallow every talk, though the prompt offered the asohos).
        state.hinted["niko_request"] = true; state.hinted["nikoAsohosAsked"] = true; Give("asohos", NikoAsohos);
        foreach (var f in TalkTo("niko", 0, 10, "up")) yield return f;
        Check($"with the asohos he asked for, Niko takes them even with the pond dry ({Has("asohos")} left)", state.Hinted("niko_asohos") && Has("asohos") == 0);
        foreach (var f in Finish()) yield return f;
        Give("stone", 6); Give("wood", 2); yield return 3;
        Check($"with the stone and wood, Niko's label says so", NikoSeaLabel()?.Contains("stone") == true);
        foreach (var f in TalkTo("niko", 0, 10, "up")) yield return f;
        foreach (var f in Finish()) yield return f;
        Check($"Niko mends the bank; it refills by tomorrow ({Has("stone")} stone left)", state.Hinted("rs:pondFixed") && !PondDry && PondRefilling && Has("stone") == 0);
        player.X = 1650; player.Y = 213; player.Face = "down"; yield return 3;
        Check($"refilling, it still can't be fished today (prompt: {prompt.Text})", target?.Type == "info" && prompt.Text.Contains("tomorrow"));

        foreach (var f in TalkTo("lira", 0, 10, "up")) yield return f;
        Check("Lira sends you to the old post", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("post")) && state.Hinted("rs:markTold"));
        foreach (var f in Finish()) yield return f;
        player.X = PostX; player.Y = PostY + 8; player.Face = "up"; yield return 3;
        Check($"at the post (prompt: {prompt.Text})", target?.Type == "seapost" && prompt.Text.Contains("mark"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the notch, high up: clue 3", mode == "dialogue" && state.Hinted("rs:mark"));
        foreach (var f in Finish()) yield return f;
        pendingShot = "s03-old-post"; yield return 2;

        /* ---------- The lesson and the signs ---------- */
        Check($"Ma'am Isay has something to say ({IsaySeaLabel()})", IsaySeaLabel()?.Contains("found") == true);
        foreach (var f in TalkTo("isay", -10, 9, "right")) yield return f;
        var lesson = string.Join(" ", dlg?.Lines.Select(l => l.T) ?? Enumerable.Empty<string>());
        Check("the lesson: Shake, Drop, Roar; one sign is enough; tides take hours; Moro Gulf; leave the boat; School Rise",
            lesson.Contains("Shake, Drop, Roar") && lesson.Contains("Any one sign is enough") && lesson.Contains("six hours") && lesson.Contains("Moro Gulf")
            && lesson.Contains("leave the boat") && lesson.Contains("School Rise") && lesson.Contains("We don't know how the berberoka story began"));
        foreach (var f in Finish()) yield return f;
        Check("clue 4, and the case has all four", state.Hinted("rs:signs") && SeaClueCount == 4);
        for (int i = 0; i < SignSpots.Length; i++)
        {
            player.X = SignSpots[i].x; player.Y = SignSpots[i].y + 7; player.Face = "up"; yield return 3;
            if (target?.Type != "seasign") { Check($"sign spot {i} offers a sign (prompt: {prompt.Text})", false); continue; }
            Inp.Tap(KeyboardKey.E); yield return 3;
        }
        Check($"all three signs are up by real presses ({SignsUp})", SignsUp == 3);
        player.X = 1600; player.Y = 205; yield return 2;
        pendingShot = "s04-signs"; yield return 2;

        /* ---------- The drill ---------- */
        foreach (var f in TalkTo("isay", -10, 9, "right")) yield return f;
        foreach (var f in Finish()) yield return f;
        Check($"the drill starts ({evac?.Drill})", evac?.Drill == true && evac.People.Count == 3 && evac.People.All(m => !m.Going));
        // It's only practice: hunger goes on as usual (Codex: the drill used to freeze it).
        float food0 = state.food;
        yield return 90;
        Check($"during the drill, hunger goes on as usual ({food0:0.00} -> {state.food:0.00})", state.food < food0);
        // Walk off and it's called off; Ma'am Isay runs it again.
        player.X = RiseX - 320; player.Y = RiseY; yield return 3;
        Check($"walking far off calls the drill off ({toastMsg})", evac == null && toastMsg.Contains("called off"));
        foreach (var f in TalkTo("isay", -10, 9, "right")) yield return f;
        foreach (var f in Finish()) yield return f;
        Check("Ma'am Isay runs it again", evac?.Drill == true);
        player.X = 1590; player.Y = 205; player.Face = "down"; yield return 3;
        Check($"near the children, you call to them (prompt: {prompt.Text})", target?.Type == "shout" && prompt.Text.Contains("Mia"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"one call sends everyone in earshot up the route ({evac.Shouted})", evac.Shouted == 3 && evac.People.All(m => m.Going));
        pendingShot = "s05-drill"; yield return 2;
        player.X = RiseX; player.Y = RiseY + 6; yield return 2;
        int coins0 = state.coins;
        for (int i = 0; i < 900 && evac != null; i++) yield return 1;
        Check($"once they're all back on the bench and you're up, the drill's done ({state.coins - coins0} coins)", evac == null && state.Hinted("rs:drill") && state.coins == coins0 + 30);
        foreach (var f in Finish()) yield return f;
        yield return 3;
        Check("not the real thing the same day", evac == null);

        /* ---------- The real thing ---------- */
        state.day = 5; yield return 3;
        Check($"day {state.day} is the Bangus Festival: not today", FestivalToday && evac == null);
        state.day = 6;
        // One foot still in the shallows (in waders): it waits until you're fully ashore, so you can't be left unable to
        // step inland once wading stops for the evacuation (Codex).
        Give("waders");
        player.X = 1512; player.Y = 186; player.Face = "right"; yield return 3;
        bool straddling = Wadeable(TileAt((int)((player.X - 3) / T), (int)((player.Y - 1) / T))) && Walkable(TileUnder(player.X, player.Y));
        Check($"standing with one foot in the water ({straddling}), it doesn't start yet", straddling && evac == null);
        Take("waders");
        // Following a goal of your own: the evacuation's goal mustn't lose it, even if you follow that one (Codex).
        string mine = Goals().First(g => g.Group == "story").Id;
        state.track = mine;
        player.X = 1575; player.Y = 205; player.Face = "down"; state.hp = 100;
        float hp0 = state.hp;
        yield return 3;
        Check($"a later day on the island: a strong earthquake ({evac?.Phase})", evac is { Drill: false, Phase: "quake" } && toastMsg.Contains("strong"));
        // Under a palm, coconuts fall all round you, but nothing hurts you; it only tells you to move.
        player.X = palm.x * T + 5; player.Y = palm.y * T + 18; if (!CanStand(player.X, player.Y)) player.Y += 4;
        for (int i = 0; i < 60; i++) yield return 1;
        Check($"coconuts shake loose ({cocos.Count}), harmlessly ({state.hp:0} health)", cocos.Count > 0 && state.hp == hp0);
        pendingShot = "s06-quake"; yield return 2;
        player.X = 1580; player.Y = 205; yield return 2;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 600 && evac.Phase == "quake"; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"after the shaking: up to School Rise, now ({toastMsg})", evac.Phase == "evacuate" && evac.Ducked && toastMsg.Contains("School Rise"));
        Check("Lira and Niko head up straight away; the children wait", evac.People.Where(m => m.Kid < 0).All(m => m.Going) && evac.People.Where(m => m.Kid >= 0).All(m => !m.Going));
        for (int i = 0; i < 30; i++) yield return 1;
        Check($"the music turns urgent ({Music.Current})", Music.Current == "alarm");
        Check($"the guide points up to School Rise ({tracked?.Title})", tracked?.Id == "sea:up" && GuideShown);
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check($"no riding or boats now ({toastMsg})", toastMsg.Contains("on foot"));
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check("no building", mode == "play");
        // Esc pauses it, and everything waits.
        float t0 = evac.T;
        Inp.Tap(KeyboardKey.Escape); yield return 10;
        Check($"Esc pauses it ({mode}, {t0:0.00} -> {evac.T:0.00})", mode == "pause" && evac.T == t0);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to it", mode == "play" && evac != null);
        TogglePanel("journal"); yield return 4;
        bool followed = ClickButton("follow:sea:up"); yield return 4; Inp.ScriptMouse = Offscreen;
        ClosePanels(); yield return 2;
        Check($"the journal shows the way up during it, and it can be followed ({state.track})", followed);
        // A save made now keeps what you were following before, not the evacuation's goal (Codex).
        Save();
        Check($"a save made during it keeps your own goal ({SaveFile.Read(SaveFile.Slot).track})", SaveFile.Read(SaveFile.Slot).track == mine);
        player.X = 1588; player.Y = 205; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"you call to the children on your way ({evac.Shouted})", evac.Shouted == 3);
        // Up the route.
        player.X = RiseX; player.Y = RiseY + 6; player.Face = "down";
        for (int i = 0; i < 900 && evac.T < 21; i++) yield return 1;
        Check($"the sea goes out past the reef ({SeaLine:0} px out)", SeaLine > 60);
        // A look from the beach while it's out (before the water comes back), then straight back up.
        player.X = 1560; player.Y = 200;
        int seabed = SeaPixels();
        Check($"the dry seabed is drawn where the sea was ({seabed} pixels)", seabed > 1500);
        Check("you can't walk out onto it", !CanStand(1500, 205) && !CanStand(1495, 230));
        pendingShot = "s07-sea-out"; yield return 1;
        player.X = RiseX; player.Y = RiseY + 6; yield return 1;
        for (int i = 0; i < 900 && evac.T < 34; i++) yield return 1;
        Check($"the wave comes back over the low ground ({SeaLine:0})", SeaLine < -20 && FloodAt(1535, 205));
        Check("but you're dry up on School Rise", !evac.Caught && !FloodAt(player.X, player.Y));
        pendingShot = "s08-wave"; yield return 2;
        int coinsBefore = state.coins;
        for (int i = 0; i < 2400 && evac != null && evac.Phase == "evacuate"; i++) yield return 1;
        Check($"the 150 coins come with it, and the save has it ({state.coins - coinsBefore})", state.coins == coinsBefore + 150 && SaveFile.Read(SaveFile.Slot).Hinted("rs:done"));
        Check($"everyone's counted ({string.Join(", ", evac?.People.Where(m => !m.Safe).Select(m => m.Name) ?? new List<string>())} missing)", evac == null || evac.People.All(m => m.Safe));
        Check("when the waves have passed, the case is closed and paid at once, before the words", SeaCaseClosed && state.Hinted("rs:dry") && state.Hinted("rs:called") && state.Hinted("rs:ducked"));
        for (int i = 0; i < 200 && mode != "dialogue"; i++) yield return 1;
        Check("time passes, then the radio's all-clear from the officials", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("disaster office")) && dlg.Lines.Any(l => l.T.Contains("hour")));
        Check($"and the report says what you did right ({dlg.Lines.Last().T})", dlg.Lines.Last().T.StartsWith("You ducked") && dlg.Lines.Last().T.Contains("before the sea came back"));
        foreach (var f in Finish()) yield return f;
        Check($"then its card ({mode})", mode == "seacard");
        yield return 40;
        pendingShot = "s09-card"; yield return 2;
        Check($"the card fits the screen ({lastGoldBottom:0})", lastGoldBottom <= Gfx.LH - 4);
        ClickButton("Salamat!"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the real button closes it ({toastMsg})", mode == "play" && toastMsg.Contains("Case closed"));
        Check($"hp untouched through it all ({state.hp:0})", state.hp >= hp0 - 0.01f);
        Check($"and what you were following is still followed ({state.track})", state.track == mine);
        TogglePanel("case"); yield return 3;
        caseTab = "amihan"; yield return 3;
        pendingShot = "s10-case-closed"; yield return 2;
        Check($"the solved case fits on the board ({lastCaseBottom:0} of {Gfx.LH})", lastCaseBottom <= Gfx.LH);
        caseTab = "saltmere"; yield return 3;
        Note($"saltmere board ends at {lastCaseBottom:0}");
        state.hinted["habagat"] = true; foreach (var c in Data.MoonClues) state.hinted[c.Key] = true; caseTab = "habagat"; yield return 3;
        Check($"the vanishing moon's full board fits too ({lastCaseBottom:0})", lastCaseBottom <= Gfx.LH);
        state.hinted.Remove("habagat"); foreach (var c in Data.MoonClues) state.hinted.Remove(c.Key);
        ClosePanels(); yield return 2;

        // Niko's crab pot, on a later day.
        foreach (var f in TalkTo("niko", 0, 10, "up")) yield return f;
        foreach (var f in Finish()) yield return f;
        Check("not the same day", !state.Hinted("rs:crabpot"));
        state.day = 7; yield return 2;
        int pots = Has("crab_pot");
        foreach (var f in TalkTo("niko", 0, 10, "up")) yield return f;
        Check("the next day, Niko gives you a crab pot", mode == "dialogue" && Has("crab_pot") == pots + 1 && dlg.Lines.Any(l => l.T.Contains("crabs")));
        foreach (var f in Finish()) yield return f;
        Check("and the pond is fishable again", !PondClosed);

        /* ---------- Caught on the beach: pulled up, never hurt ---------- */
        state.hinted.Remove("rs:done"); state.hinted.Remove("rs:dry"); state.gifts["rs_drill"] = state.day - 1;
        player.X = 1535; player.Y = 207; player.Face = "left"; state.hp = 60;
        // Starving, too: nothing hurts you during it, hunger included (Codex).
        state.food = 0;
        yield return 3;
        Check("set up again on the beach", evac is { Drill: false });
        state.hp = 60;   // (the frame or two of starving before it began)
        evac.T = QuakeLen - 0.05f; evac.Quake.T = evac.Quake.Len - 0.05f;
        for (int i = 0; i < 2400 && !evac.Caught; i++) yield return 1;
        Check("staying down on the beach, the water reaches you", evac.Caught);
        for (int i = 0; i < 200 && mode != "dialogue"; i++) yield return 1;
        Check($"a neighbour pulls you up to School Rise, unhurt ({state.hp:0} health)", OnRise && state.hp >= 60 && mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("never go back down")));
        foreach (var f in Finish()) yield return f;
        // Back down to the beach before the second wave: pulled up again (Codex: only the first time used to work).
        for (int i = 0; i < 2400 && evac.T < 44; i++) yield return 1;
        player.X = 1535; player.Y = 207;
        for (int i = 0; i < 2400 && mode == "play" && evac.T < 60; i++) yield return 1;
        for (int i = 0; i < 200 && mode != "dialogue"; i++) yield return 1;
        Check($"going back down, you're pulled up a second time ({mode}, on the rise {OnRise})", mode == "dialogue" && OnRise);
        foreach (var f in Finish()) yield return f;
        Check($"still unhurt, though starving ({state.hp:0} health)", state.hp >= 60);
        // The children go up when Ma'am Isay rings the bell, even if nobody called.
        Check("nobody called this time; the bell sent the children up", evac.Bell && evac.People.Where(m => m.Kid >= 0).All(m => m.Going));
        for (int i = 0; i < 3000 && mode != "seacard"; i++) { if (mode == "dialogue") Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"the report gives the tips kindly ({SeaReport()})", SeaReport().Contains("never back down toward the sea") && SeaReport().Contains("call to anyone"));
        catchOpenedAt = -10; CloseSeaCard(); yield return 2;

        /* ---------- Dry, but late up ---------- */
        // Waiting about away from the water and coming up only after the sea came back isn't "straight up" (Codex).
        state.hinted.Remove("rs:done"); state.gifts["rs_drill"] = state.day - 1;
        player.X = 1580; player.Y = 205; yield return 3;
        evac.T = QuakeLen - 0.05f; evac.Quake.T = evac.Quake.Len - 0.05f;
        player.X = 1715; player.Y = 205;
        for (int i = 0; i < 2400 && evac.T < 42; i++) yield return 1;
        Check($"away from the water, the wave doesn't reach you ({evac.Caught})", !evac.Caught);
        player.X = RiseX; player.Y = RiseY + 6;
        for (int i = 0; i < 3000 && mode != "seacard"; i++) { if (mode == "dialogue") Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"the report doesn't say you went straight up ({SeaReport()})", !SeaReport().Contains("before the sea came back") && SeaReport().Contains("as soon as"));
        catchOpenedAt = -10; CloseSeaCard(); yield return 2;

        /* ---------- A save mid-evacuation ---------- */
        state.hinted.Remove("rs:done"); state.gifts["rs_drill"] = state.day - 1;
        player.X = 1580; player.Y = 205; yield return 3;
        Check("it can start again", evac != null);
        Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); ClearSkies(); yield return 2;
        Check($"loading a save made mid-evacuation: the case is still open, and it starts again from the shaking ({evac?.Phase} {evac?.T:0.0})",
            !SeaCaseClosed && evac is { Phase: "quake" } && evac.T < 1);

        /* ---------- Another slot ---------- */
        // Left over from a game quit mid-evacuation, the snapshot of what you followed mustn't reach another slot (Codex).
        trackBeforeSea = "sea:case";
        state = new State { created = true, look = new Look { name = "Other slot" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false);
        string other = Goals().First(g => g.Group == "story").Id;
        state.track = other; yield return 3;
        Check($"loading another game keeps that game's own goal ({state.track})", state.track == other);

        /* ---------- Old saves ---------- */
        state = new State { created = true, look = new Look { name = "Old islander" } };
        state.flags.metTomas = true;
        state.hinted["amihan"] = true; state.hinted["metIsay"] = true;
        state.day = 9;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false);
        Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); ClearSkies(); SetNight(false);
        player.X = 1580; player.Y = 205; yield return 3;
        Check($"a save from before 1.19 that has met Ma'am Isay waits a day for the tremor (ready {state.gifts.GetValueOrDefault("rs_ready")})", tremor == null && state.gifts.GetValueOrDefault("rs_ready") == 9);
        state.day = 10; yield return 3;
        Check("then it comes", tremor != null);
        tremor = null;
    }

    // FESH_SPRITES: ducking (duck, cover and hold) in each facing, with two looks, at 8x; and a route sign.
    void ExportSeaSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#e8d8a8"));
        var looks = new[] { new Look { skin = 1, hair = 0, hairColor = 1, hat = 1, shirt = 0, pants = 0 }, new Look { skin = 3, hair = 1, hairColor = 0, hat = 0, shirt = 1, pants = 2 } };
        int col = 0;
        foreach (var look in looks)
            foreach (var face in new[] { "down", "up", "left", "right" })
            {
                LookData.DrawPerson(sheet, look, 8 + col * 12, 18, face, 0);
                DrawDucked(look, 8 + col * 12, 36, face);
                col++;
            }
        DrawRouteSign(110, 36, 0);
        pix = keep;
        int w = 120, h = 42;
        var img = Raylib.GenImageColor(w, h, Color.Black);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * W + xx]);
        Raylib.ImageResizeNN(ref img, w * 8, h * 8);
        Raylib.ExportImage(img, path.Replace(".png", "-duck.png"));
        Raylib.UnloadImage(img);
    }

    // Pixels drawn differently over the west side's water and shore right now than with the sea at rest.
    int SeaPixels()
    {
        RenderWorld(time);
        var with = (Color[])pix.Buf.Clone();
        var keep = evac.T;
        evac.T = 0;
        RenderWorld(time);
        evac.T = keep;
        int n = 0;
        for (int i = 0; i < with.Length; i++)
        {
            var a = with[i]; var b = pix.Buf[i];
            if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) >= 30) n++;
        }
        RenderWorld(time);
        return n;
    }
}
#endif
