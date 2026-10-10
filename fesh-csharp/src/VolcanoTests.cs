#if DEBUG
using Raylib_cs;

namespace Fesh;

// 1.21: beneath the clouds, story 4 on Baga Island. FESH_VOLCANO_TEST=1 runs only this (it still needs FESH_AUTOTEST
// and FESH_SAVE); the full play-through runs it after the hotbar's checks.
partial class Game
{
    IEnumerable<int> VolcanoScript()
    {
        Note("Beneath the clouds: Baga's volcano, the go-bag, the shelter and the lahar watch");
        state = new State { created = true, look = new Look { name = "Ember watcher" } };
        state.flags.metTomas = true;
        state.hinted["amihan"] = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        chartEast = true;
        state.charted?.Add("amihan:Baga Island");
        state.charted?.Add("amihan:Amihan Village");
        Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        state.day = 2;
        yield return 2;

        IEnumerable<int> Finish() { for (int i = 0; i < 40 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 2; } }
        IEnumerable<int> Press() { Inp.Tap(KeyboardKey.E); yield return 3; }
        IEnumerable<int> Click(string label) { bool ok = ClickButton(label); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2; if (!ok) Note($"(no button called {label})"); }
        IEnumerable<int> ClickAt(float x, float y) { Inp.ScriptMouse = new System.Numerics.Vector2(x, y); Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 2; }
        IEnumerable<int> Stand(float x, float y, string face) { player.X = x; player.Y = y; player.Face = face; yield return 3; }

        /* ---------- The places ---------- */
        bool Open(float x, float y) => CanStand(x, y) && !InPdz(x, y);
        var spots = new (string, float, float)[] { ("Mila", MilaX, MilaY + 6), ("Ben", BenX, BenY + 6), ("abaca", AbacaX, AbacaY + 8), ("pili", PiliX, PiliY + 8),
            ("board", AlertBoardX, AlertBoardY + 8), ("crate", CrateX, CrateY + 8), ("table", EvacTableX, EvacTableY + 8), ("jetty end", BoardPointX, BoardPointY) };
        var shut = spots.Where(p => !Open(p.Item2, p.Item3)).Select(p => p.Item1).ToList();
        Check($"Manay Mila, Ben, the abaca frame, the pili spot, the board, the crate, the table and the jetty end can be reached, outside the danger zone ({string.Join(", ", shut)})", shut.Count == 0);
        Check("the cone and its crater are inside the permanent danger zone, and its marker stones stand on land", InPdz(ConeX, ConeBaseY - 10) && InPdz(ConeX, ConeBaseY - ConeH + 10) && MarkerStones().Count() >= 10);
        Check("the lahar channel's mouth is on the beach, and the jetty route is clear of it", Walkable(TileUnder(LaharMouthX, LaharMouthY)) && ChannelDist(BoardPointX, BoardPointY + 40) > 40 && ChannelDist(BenX, BenY) > 20);
        Check("the shelter's jars, rain barrel and door are on open ground in Amihan Village",
            JarSpots.All(j => CanStand(j.x + 6, j.y + 3)) && CanStand(BarrelX - 6, BarrelY + 4) && CanStand(SchoolDoorX, SchoolDoorY + 5) && CanStand(LandingX, LandingY));
        Check("the bagareef spot is still fishable from Baga's jetty", Data.Spots.First(s => s.Id == "bagareef") is var reef && Dist(reef.X, reef.Y, 2315, 327) < reef.R);
        foreach (var f in Stand(2300, 400, "down")) yield return f;
        pendingShot = "v01-north-shore"; yield return 2;
        foreach (var f in Stand(2290, 560, "up")) yield return f;
        pendingShot = "v02-cone-south"; yield return 2;
        foreach (var f in Stand(2232, 470, "right")) yield return f;
        pendingShot = "v02b-cone-west"; yield return 2;

        /* ---------- Before meeting anyone ---------- */
        Check("nothing in the journal and no Case board tab before you meet Manay Mila", !Goals().Any(g => g.Id.StartsWith("mg:")) && !MgStarted && BagaLevel == 0);
        Check("but Manay Mila and Ben are among the people of Amihan to meet", Goals().FirstOrDefault(g => g.Id == "meet:amihan")?.Title.Contains("of 8") == true);

        /* ---------- Manay Mila ---------- */
        var mila = IslanderWalk("mila");
        foreach (var f in Stand(mila.X, mila.Y + 10, "up")) yield return f;
        Check($"by her garden (prompt: {prompt.Text})", target?.Type == "islander" && target.Id == "mila");
        foreach (var f in Press()) yield return f;
        Check("Manay Mila introduces herself, the volcanic soil and her lola's Albay", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("soil")) && dlg.Lines.Any(l => l.T.Contains("Albay")) && MgMet);
        foreach (var f in Finish()) yield return f;
        Check("the journal sends you to the abaca frame", Goals().FirstOrDefault(g => g.Id == "mg:case") is { HasPoint: true } g0 && g0.Place == "Abaca frame");
        foreach (var f in Stand(AbacaX + 2, AbacaY + 10, "up")) yield return f;
        Check($"at the frame (prompt: {prompt.Text})", target?.Type == "abaca");
        foreach (var f in Press()) yield return f;
        foreach (var f in Stand(PiliX, PiliY + 9, "up")) yield return f;
        Check($"at the pili seedling (prompt: {prompt.Text})", target?.Type == "pili");
        foreach (var f in Press()) yield return f;
        Check("tied and planted", state.Hinted("mg:abaca") && state.Hinted("mg:pili"));
        foreach (var f in Stand(mila.X, mila.Y + 10, "up")) yield return f;
        foreach (var f in Press()) yield return f;
        Check("her lola's version of the Magayon story, told as hers, with what the names mean (clue 1)",
            mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("version")) && dlg.Lines.Any(l => l.T.Contains("Panganoron means cloud")) && dlg.Lines.Any(l => l.T.Contains("Pagtuga")) && state.Hinted("mg:story"));
        foreach (var f in Finish()) yield return f;
        TogglePanel("case"); yield return 3;
        foreach (var f in Click("Beneath the clouds")) yield return f;
        Check($"the Case board has a fourth tab, by a real click ({caseTab})", caseTab == "baga" && mode == "panel");
        Check($"its four tabs fit beside Close ({lastCaseTabRight:0} px) and the board fits ({lastCaseBottom:0} px)", lastCaseTabRight < 50 + 1180 - 13 - 24 - SmallW("Close") - 4 && lastCaseBottom <= Gfx.LH);
        pendingShot = "v03-case-board"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- Ben and the alert card ---------- */
        var ben = IslanderWalk("ben");
        foreach (var f in Stand(ben.X, ben.Y + 10, "up")) yield return f;
        foreach (var f in Press()) yield return f;
        Check("Ben: the station, PHIVOLCS for real volcanoes, Baga made up, level 0 isn't safe",
            mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("PHIVOLCS")) && dlg.Lines.Any(l => l.T.Contains("made up")) && dlg.Lines.Any(l => l.T.Contains("doesn't mean safe")));
        foreach (var f in Finish()) yield return f;
        Check("then the alert card opens: all six of Mayon's levels", mode == "panel" && panel == "alertcard" && AlertLevels.Length == 6 && AlertLevels[3].Name == "Increased Tendency Towards Hazardous Eruption");
        Check($"and it fits the screen ({lastMgPanelBottom:0})", lastMgPanelBottom <= Gfx.LH);
        pendingShot = "v04-alert-card"; yield return 2;
        foreach (var f in Click("Close")) yield return f;
        Check("Close shuts it", mode == "play");
        foreach (var f in Stand(AlertBoardX, AlertBoardY + 9, "up")) yield return f;
        Check($"the board outside reads the level (prompt: {prompt.Text})", target?.Type == "alertboard" && prompt.Text.Contains("Level 0"));

        /* ---------- Into the permanent danger zone: stopped ---------- */
        foreach (var f in Stand(PdzX - 20, 405, "down")) yield return f;
        float before = player.Y;
        Inp.Hold(KeyboardKey.Down, true); yield return 30; Inp.Hold(KeyboardKey.Down, false); yield return 2;
        Check($"walking toward the crater stops at the marker stones, with Ben's word ({before:0} -> {player.Y:0}: {toastMsg})", !InPdz(player.X, player.Y) && toastMsg.Contains("permanent danger zone"));

        /* ---------- Unrest, a later day ---------- */
        foreach (var f in Stand(2300, 404, "down")) yield return f;
        Check("not the same day", !state.Hinted("mg:unrest"));
        state.day = 3; SetNight(true); yield return 3;
        Check("not at night", !state.Hinted("mg:unrest"));
        SetNight(false); state.weather = "storm"; yield return 3;
        Check("not in a storm", !state.Hinted("mg:unrest"));
        ClearSkies();
        foreach (var f in Stand(1600, 205, "down")) yield return f;
        Check("not when you're off Baga", !state.Hinted("mg:unrest"));
        foreach (var f in Stand(2300, 404, "down")) yield return f;
        Check($"on Baga by day: a small shake, more steam, Level 1 ({toastMsg})", state.Hinted("mg:unrest") && BagaLevel == 1);

        /* ---------- Readings, round 1 ---------- */
        foreach (var f in Stand(ben.X, ben.Y + 10, "up")) yield return f;
        Check($"Ben has readings for you (prompt: {prompt.Text})", prompt.Text.Contains("readings"));
        foreach (var f in Press()) yield return f;
        foreach (var f in Finish()) yield return f;
        Check("the readings panel opens, labelled as simplified examples", mode == "panel" && panel == "readings" && readings?.Round == 1);
        yield return 2;
        var seismo = Gfx.Seen["seismo"];
        foreach (var f in ClickAt(seismo.X + 0.9f * seismo.Width, seismo.Y + 32)) yield return f;
        Check($"clicking the surf's wiggles says that's not a quake ({readings.Note})", readings.Note.Contains("surf") && readings.Marked.Count == 0);
        var quakes = QuakesToday[0];
        foreach (var q in quakes)
            foreach (var f in ClickAt(seismo.X + q.x * seismo.Width, seismo.Y + 32 + q.row * 64)) yield return f;
        Check($"every quake marked by clicking its spike ({readings.Marked.Count} of {quakes.Length})", readings.Counted);
        pendingShot = "v05-seismograph"; yield return 2;
        foreach (var f in Click("Fewer")) yield return f;
        Check($"a wrong reading gets Ben's explanation and no praise ({readings.Note})", !readings.Ok && readings.Note.Contains("Count again"));
        foreach (var f in Click("More")) yield return f;
        Check("the right one: Next", readings.Ok);
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("Steady")) yield return f;
        Check("GPS: steady is wrong", !readings.Ok && readings.Note.Contains("outward"));
        foreach (var f in Click("Swelling")) yield return f;
        pendingShot = "v06-gps"; yield return 2;
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("Sweep across the plume")) yield return f;
        for (int i = 0; i < 200 && !readings.Swept; i++) yield return 2;
        Check("the gas scan sweeps across the plume", readings.Swept);
        foreach (var f in Click("More")) yield return f;
        Check($"more sulfur dioxide, and what the number means ({readings.Note})", readings.Ok);
        pendingShot = "v07-gas"; yield return 2;
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("A glow at the crater")) yield return f;
        foreach (var f in Click("Check")) yield return f;
        Check("by day there's no glow: wrong", !readings.Ok);
        foreach (var f in Click("Nothing new: just steam")) yield return f;
        foreach (var f in Click("Check")) yield return f;
        Check("just steam", readings.Ok);
        pendingShot = "v08-camera-day"; yield return 2;
        Check($"the readings panel fits ({lastMgPanelBottom:0})", lastMgPanelBottom <= Gfx.LH);
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("Send the readings to Ben")) yield return f;
        Check("Ben and the team in town: Level 2, Increasing Unrest", mode == "dialogue" && BagaLevel == 2 && dlg.Lines.Any(l => l.T.Contains("Increasing Unrest")));
        foreach (var f in Finish()) yield return f;
        Check("the go-bag is a job in the journal", Goals().Any(g => g.Id == "mg:bag"));

        /* ---------- The go-bag ---------- */
        foreach (var f in Stand(CrateX, CrateY + 9, "up")) yield return f;
        Check($"at the crate (prompt: {prompt.Text})", target?.Type == "gobag");
        foreach (var f in Press()) yield return f;
        Check("the go-bag panel opens", mode == "panel" && panel == "gobag");
        yield return 2;
        foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })
            foreach (var f in ClickAt(Gfx.Seen[$"gobag:{i}"].X + 40, Gfx.Seen[$"gobag:{i}"].Y + 40)) yield return f;
        foreach (var f in Click("Done packing")) yield return f;
        Check($"the fishing rod stays behind ({gobag?.Note})", gobag?.Done == false && gobag.Note.Contains("rod"));
        foreach (var f in ClickAt(Gfx.Seen["gobag:8"].X + 40, Gfx.Seen["gobag:8"].Y + 40)) yield return f;
        foreach (var f in ClickAt(Gfx.Seen["gobag:4"].X + 40, Gfx.Seen["gobag:4"].Y + 40)) yield return f;
        foreach (var f in Click("Done packing")) yield return f;
        Check($"leaving the radio out: still missing ({gobag?.Note})", gobag?.Done == false && gobag.Note.Contains("radio"));
        foreach (var f in ClickAt(Gfx.Seen["gobag:4"].X + 40, Gfx.Seen["gobag:4"].Y + 40)) yield return f;
        foreach (var f in Click("Done packing")) yield return f;
        Check("all eight essentials: packed", state.Hinted("mg:bag") && gobag?.Done == true);
        Check($"the go-bag panel fits ({lastMgPanelBottom:0})", lastMgPanelBottom <= Gfx.LH);
        pendingShot = "v09-gobag"; yield return 2;
        foreach (var f in Click("Close")) yield return f;

        /* ---------- Round 2: the counterexample, and the order ---------- */
        foreach (var f in Stand(ben.X, ben.Y + 10, "up")) yield return f;
        Check("no new readings the same day", !ReadingsDue);
        state.day = 4; state.weather = "storm"; yield return 3;
        foreach (var f in Press()) yield return f;
        Check("in a storm Ben waits", mode == "dialogue" && dlg.Lines[0].T.Contains("storm") && readings == null);
        foreach (var f in Finish()) yield return f;
        ClearSkies(); yield return 2;
        foreach (var f in Press()) yield return f;
        foreach (var f in Finish()) yield return f;
        Check("round 2 opens", mode == "panel" && readings?.Round == 2);
        yield return 2;
        seismo = Gfx.Seen["seismo"];
        foreach (var q in QuakesToday[1])
            foreach (var f in ClickAt(seismo.X + q.x * seismo.Width, seismo.Y + 32 + q.row * 64)) yield return f;
        foreach (var f in Click("More")) yield return f;
        Check("fewer quakes this time: 'more' is wrong", !readings.Ok);
        foreach (var f in Click("Fewer")) yield return f;
        Check($"fewer, and Ben says that doesn't mean it's calming ({readings.Note})", readings.Ok && readings.Note.Contains("every sign"));
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("Swelling")) yield return f;
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("Sweep across the plume")) yield return f;
        for (int i = 0; i < 200 && !readings.Swept; i++) yield return 2;
        foreach (var f in Click("More")) yield return f;
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("A glow at the crater")) yield return f;
        foreach (var f in Click("Rocks rolling down")) yield return f;
        pendingShot = "v10-camera-night"; yield return 2;
        foreach (var f in Click("Check")) yield return f;
        Check("last night: the glow and the rocks", readings.Ok);
        foreach (var f in Click("Next")) yield return f;
        foreach (var f in Click("Send the readings to Ben")) yield return f;
        Check("Level 3, its full name, and the order to leave early (clue 2)",
            mode == "dialogue" && BagaLevel == 3 && MgOrder && dlg.Lines.Any(l => l.T.Contains("Increased Tendency Towards Hazardous Eruption")) && state.Hinted("mg:signs"));
        foreach (var f in Finish()) yield return f;

        /* ---------- Leaving Baga ---------- */
        float clock0 = state.clock, food0 = state.food = 50;
        yield return 30;
        Check("while you leave, the clock and hunger wait", state.clock == clock0 && state.food == food0);
        Check($"the music is the alarm ({WantedTrack()})", WantedTrack() == "alarm");
        Check($"the guide follows only leaving ({tracked?.Title}: {tracked?.Place})", tracked?.Id == "mg:leave" && tracked.Place == "Manay Mila");
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check($"no riding or boating off ({toastMsg})", toastMsg.Contains("Niko's banca"));
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check("no building", mode == "play");
        foreach (var f in Stand(2400, 480, "down")) yield return f;
        Check($"anywhere else, the prompt says what's next ({prompt.Text})", target?.Type == "info" && prompt.Text.Contains("Manay Mila"));
        foreach (var f in Stand(MilaX, MilaY + 14, "up")) yield return f;
        Check($"by her house: call to Manay Mila ({prompt.Text})", target?.Type == "callmila");
        foreach (var f in Press()) yield return f;
        for (int i = 0; i < 120 && mgMilaWalk?.Safe != true; i++) yield return 2;
        Check("she walks down to the table by the jetty", state.Hinted("mg:called") && mgMilaWalk?.Safe == true);
        foreach (var f in Stand(EvacTableX, EvacTableY + 9, "up")) yield return f;
        Check($"at the table (prompt: {prompt.Text})", target?.Type == "register");
        foreach (var f in Press()) yield return f;
        Check("you sign the list", state.Hinted("mg:registered"));
        foreach (var f in Stand(BoardPointX, BoardPointY + 2, "up")) yield return f;
        pendingShot = "v11-evacuation"; yield return 2;
        Check($"at the jetty's end: board Niko's banca ({prompt.Text})", target?.Type == "boardevac");
        Give("boat");
        foreach (var f in Press()) yield return f;
        for (int i = 0; i < 80 && mode != "dialogue"; i++) yield return 2;
        Check($"a short crossing to the village landing ({player.X:0},{player.Y:0}), your banca alongside",
            MgEvacuated && Dist(player.X, player.Y, LandingX, LandingY) < 4 && Dist(state.boatX, state.boatY, 1483, 217) < 1 && !BagaEvacuating);
        Check("Ma'am Isay: the school is the checked shelter, and what to do before the ash", dlg.Lines.Any(l => l.T.Contains("checked the school")) && dlg.Lines.Any(l => l.T.Contains("winds higher in the atmosphere")));
        foreach (var f in Finish()) yield return f;

        /* ---------- Before the ash, then the shelter ---------- */
        Check("Baga is closed now, and its reef", BagaClosed && !SpotOpen("bagareef"));
        Check($"the guide points at the first water jar ({tracked?.Place})", tracked?.Place == "Water jar");
        for (int i = 0; i < JarSpots.Length; i++)
        {
            foreach (var f in Stand(JarSpots[i].x + 6, JarSpots[i].y + 3, "left")) yield return f;
            Check($"jar {i + 1} (prompt: {prompt.Text})", target?.Type == "jar");
            foreach (var f in Press()) yield return f;
        }
        foreach (var f in Stand(BarrelX - 7, BarrelY + 4, "right")) yield return f;
        Check($"the rain barrel (prompt: {prompt.Text})", target?.Type == "barrel");
        foreach (var f in Press()) yield return f;
        Check("water covered and the downpipe off", JarCovered(0) && JarCovered(1) && BarrelOff && !AshFalling);
        pendingShot = "v12-school-front"; yield return 2;
        foreach (var f in Stand(SchoolDoorX, SchoolDoorY + 5, "up")) yield return f;
        Check($"the school door (prompt: {prompt.Text})", target?.Type == "schooldoor");
        foreach (var f in Press()) yield return f;
        for (int i = 0; i < 60 && (scene != "house:school" || mode != "play"); i++) yield return 2;
        Check("inside the school, the evacuation centre", scene == "house:school");
        for (int i = 0; i < ShutterSpots.Length; i++)
        {
            foreach (var f in Stand(ShutterSpots[i].x, ShutterSpots[i].y + 2, "up")) yield return f;
            Check($"shutter {i + 1} (prompt: {prompt.Text})", target?.Type == "shutter");
            foreach (var f in Press()) yield return f;
        }
        yield return 3;
        Check($"with everything ready, the ash comes ({toastMsg})", AshFalling && state.Hinted("mg:r:water") && state.Hinted("mg:r:shut") && !state.Hinted("mg:preHelped"));
        pendingShot = "v13-shelter"; yield return 2;
        // Out in the falling ash for a while, and Ma'am Isay brings you back in: no harm, and the report will say so.
        foreach (var f in Stand(RoomDoorWX, RoomDoorWY - 4, "down")) yield return f;
        foreach (var f in Press()) yield return f;
        for (int i = 0; i < 60 && (scene != "world" || mode != "play"); i++) yield return 2;
        Check("you can still go outside", scene == "world");
        pendingShot = "v14-ashfall"; yield return 2;
        Check($"the ash is drawn falling outside ({ashFlakes.Count} flakes, ash {AshFalling})", ashFlakes.Count > 0);
        float hp0 = state.hp;
        for (int i = 0; i < 600 && scene == "world"; i++) yield return 2;
        for (int i = 0; i < 40 && mode != "play"; i++) { if (mode == "dialogue") Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"out in the ash a while, Ma'am Isay brings you in, unhurt ({scene}, hp {state.hp})", scene == "house:school" && state.hp >= hp0 && state.Hinted("mg:r:outside"));
        foreach (var f in Stand(WaterCrateX, WaterCrateY + 10, "up")) yield return f;
        Check($"the water crate (prompt: {prompt.Text})", target?.Type == "takewater");
        foreach (var f in Press()) yield return f;
        for (int i = 0; i < FamilyMats.Length; i++)
        {
            foreach (var f in Stand(FamilyMats[i].x, FamilyMats[i].y + 10, "up")) yield return f;
            Check($"family {i + 1} (prompt: {prompt.Text})", target?.Type == "givewater");
            foreach (var f in Press()) yield return f;
        }
        foreach (var f in Stand(DeskX + 5, DeskY + 9, "up")) yield return f;
        Check($"the desk (prompt: {prompt.Text})", target?.Type == "desk");
        foreach (var f in Press()) yield return f;
        for (int i = 0; i < 80 && mode != "dialogue"; i++) yield return 2;
        Check("an hour passes; the ash lesson, with no wet-cloth myth (clue 3)", MgSheltered && state.Hinted("mg:ash") && dlg.Lines.Any(l => l.T.Contains("N95")) && dlg.Lines.Any(l => l.T.Contains("wet cloth doesn't")));
        foreach (var f in Finish()) yield return f;
        Check("the ash has passed and the game runs again", !VolcanoControlled && !AshFalling);

        /* ---------- Baga stays closed ---------- */
        LoadScene("world"); state.aboard = true; Give("boat");
        player.X = 2080; player.Y = 330; boatFace = "right"; yield return 3;
        Inp.Hold(KeyboardKey.Right, true);
        for (int i = 0; i < 200 && player.X < 2300; i++) yield return 2;
        Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"sailing toward Baga stops at its closed waters ({player.X:0}: {toastMsg})", player.X < 2230 && toastMsg.Contains("closed"));
        state.aboard = false; state.boatX = 1483; state.boatY = 217;

        /* ---------- The lahar watch ---------- */
        EnterSchoolNow(); yield return 3;
        foreach (var f in Stand(DisplayX, DisplayY + 8, "up")) yield return f;
        Check($"no rain on Baga yet: the display just says so ({prompt.Text})", target?.Type == "info" && prompt.Text.Contains("No rain"));
        state.forecast = new() { new WeatherSpell { at = 0, w = "rain" } }; state.weather = planned = "rain"; SnapWeather(); yield return 3;
        Check($"rain on Baga is recorded, and Ben calls you ({toastMsg})", state.Hinted("mg:rained") && toastMsg.Contains("Rain on Baga"));
        ClearSkies(); yield return 2;
        Check($"after the rain stops, the display still has the recording ({prompt.Text})", target?.Type == "display");
        foreach (var f in Press()) yield return f;
        Check("the lahar watch opens with recorded footage", mode == "panel" && panel == "lahar" && laharWatch?.Live == false);
        yield return 2;
        foreach (var f in Click("Radio the warning")) yield return f;
        Check("the warning needs the channel and mouth marked first", laharWatch?.Sent == false && laharWatch.Note.Contains("first"));
        var rr = Gfx.Seen["lahar:route"];
        foreach (var f in ClickAt(rr.X + 16, rr.Y + 16)) yield return f;
        Check($"the jetty route stays open ({laharWatch?.Note})", !laharWatch.Closed.Contains("route") && laharWatch.Note.Contains("keep it open"));
        foreach (var id in new[] { "crossing", "mouth" }) { var p = Gfx.Seen[$"lahar:{id}"]; foreach (var f in ClickAt(p.X + 16, p.Y + 16)) yield return f; }
        Check("the crossing and the mouth closed", laharWatch.Closed.Count == 2);
        foreach (var f in Click("Radio the warning")) yield return f;
        Check("the warning goes out", laharWatch.Sent && !laharWatch.ByBen);
        for (int i = 0; i < 200 && laharWatch.T < 6.2f; i++) yield return 2;
        pendingShot = "v15-lahar-watch"; yield return 2;
        Check($"the lahar panel fits ({lastMgPanelBottom:0})", lastMgPanelBottom <= Gfx.LH);
        int coins0 = state.coins;
        foreach (var f in Click("Close the case")) yield return f;
        Check($"the case closes, saved at once with the coins ({state.coins - coins0})", MgDone && state.coins == coins0 + 150 && SaveFile.Read(SaveFile.Slot).Hinted("mg:done") && state.Hinted("mg:channel"));
        string report = MgReport();
        Check($"the report praises what you did and tips what you didn't ({report})", report.Contains("packed a go-bag early") && report.Contains("called to Manay Mila")
            && report.Contains("radioed the lahar warning") && report.Contains("Next time, stay inside while ash falls") && report.Contains("danger zone is closed"));
        foreach (var f in Finish()) yield return f;
        Check("then the gold card", mode == "magayoncard");
        yield return 40;
        pendingShot = "v16-card"; yield return 2;
        Check($"the card fits ({lastGoldBottom:0})", lastGoldBottom <= Gfx.LH);
        foreach (var f in Click("Dios mabalos!")) yield return f;
        Check("its button closes it", mode == "play");
        Check("the case closes the Case board's last theory", Data.MagayonTheories[^1].Contains("Case closed"));

        /* ---------- Afterwards ---------- */
        Check("Baga isn't reopened by finishing the story: only the advisory does that", BagaClosed && BagaLevel == 3);
        state.day++; yield return 3;
        Check("Manay Mila has something for you", GiftDue && Goals().Any(g => g.Id == "mg:gift"));
        foreach (var f in Stand(74, 96, "up")) yield return f;
        Check($"at the shelter (prompt: {prompt.Text})", target?.Type == "islander" && target.Id == "mila");
        foreach (var f in Press()) yield return f;
        foreach (var f in Finish()) yield return f;
        Check("an abaca line", Has("abaca_line") == 1 && state.Hinted("mg:gift"));
        state.day = state.gifts["mg_order"] + 4; yield return 3;
        Check($"the advisory: Level 2, Baga and its reef open again ({BagaLevel})", BagaLevel == 2 && !BagaClosed && SpotOpen("bagareef") && !BagaFolkAtSchool);
        LoadScene("world");
        foreach (var f in Stand(2300, 404, "down")) yield return f;
        pendingShot = "v17-after"; yield return 2;
        foreach (var f in Stand(LaharMouthX - 20, LaharMouthY, "right")) yield return f;
        float mx0 = player.X;
        Inp.Hold(KeyboardKey.Right, true); yield return 30; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"the lahar channel's mouth stays closed ({mx0:0} -> {player.X:0})", !InChannelZone(player.X, player.Y));
        state.day = state.gifts["mg_order"] + 9; yield return 3;
        Check("later still, Level 1", BagaLevel == 1);
        Check("the danger zone is still closed at Level 1", Restriction(ConeX, ConeBaseY + 10) != null);

        /* ---------- Nights at Level 3 ---------- */
        state.bagaAlert = 3; state.gifts["mg_order"] = state.day; yield return 2;
        LoadScene("world"); SetNight(true);
        foreach (var f in Stand(2300, 404, "down")) yield return f;
        // From the north shore by night the lava glows.
        pendingShot = "v18-night-glow"; yield return 2;
        foreach (var f in Stand(2232, 470, "right")) yield return f;
        pendingShot = "v19-cone-night"; yield return 2;
        SetNight(false); yield return 2;
        pendingShot = "v20-cone-day3"; yield return 2;

        foreach (int f in VolcanoAssistScript()) yield return f;
    }

    // The quieter ways through: leaving without doing anything (helpers, no credit), and the warning Ben sends himself.
    IEnumerable<int> VolcanoAssistScript()
    {
        Note("Beneath the clouds: helpers, saves and old games");
        state = new State { created = true, look = new Look { name = "Slow walker" } };
        state.flags.metTomas = true;
        foreach (var k in new[] { "amihan", "mg:metMila", "mg:abaca", "mg:pili", "mg:story", "mg:metBen", "mg:unrest", "mg:read1", "mg:read2", "mg:signs", "mg:order" }) state.hinted[k] = true;
        state.day = 6; state.gifts["mg_order"] = 6; state.bagaAlert = 3;
        state.px = 2300; state.py = 404;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;
        Check("a game saved mid-evacuation carries on with it", BagaEvacuating && OnBaga());
        mgEvacT = 148; yield return 3;
        for (int i = 0; i < 120 && !MgEvacuated; i++) yield return 2;
        for (int i = 0; i < 40 && mode != "play"; i++) { if (mode == "dialogue") Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("doing nothing, Ben and Niko see you aboard, and nothing is credited to you",
            MgEvacuated && state.Hinted("mg:assisted") && state.Hinted("mg:bagGiven") && !state.Hinted("mg:bag") && !state.Hinted("mg:called") && !state.Hinted("mg:registered"));
        // Before the ash, nobody waits for you either.
        mgPrepT = 75.5f; yield return 6;
        Check("the jobs before the ash get done by helpers, without credit", AshFalling && state.Hinted("mg:preHelped") && !state.Hinted("mg:r:water"));
        // A save during the ash comes back with the ash still falling.
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; standStill = true; yield return 3;
        Check("a save during the ash comes back to the ash", AshFalling && VolcanoControlled);
        Check("story 3's tremor can't start in the middle of it", !SeaQuiet);
        EnterSchoolNow(); yield return 3;
        mgAshT = 75.5f; yield return 6;
        for (int i = 0; i < 80 && mode != "dialogue"; i++) yield return 2;
        foreach (var x in Enumerable.Range(0, 40)) { if (mode != "dialogue") break; Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("inside, helpers finish too, and the shelter's done", MgSheltered && !VolcanoControlled);
        // The lahar watch: closing it straight away still gets the warning out, from Ben, without credit.
        state.hinted["mg:rained"] = true;
        player.X = DisplayX; player.Y = DisplayY + 8; player.Face = "up";
        yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the display opens", panel == "lahar");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("closing it, Ben sends the warning himself and the case still closes, uncredited", MgDone && !state.Hinted("mg:r:channel"));
        for (int i = 0; i < 40 && mode != "play"; i++) { if (mode == "dialogue") Inp.Tap(KeyboardKey.E); else if (mode == "magayoncard") { yield return 40; Inp.Tap(KeyboardKey.E); } yield return 2; }
        Check("and the report says what to do next time", MgReport().Contains("pack a go-bag early") && MgReport().Contains("close the channel and radio the warning yourself"));

        // An old save: no alert field, standing where the danger zone is now: back to Baga's jetty.
        state = new State { created = true, look = new Look { name = "Old timer" } };
        state.flags.metTomas = true; state.hinted["amihan"] = true;
        state.px = ConeX; state.py = ConeBaseY + 12;
        mode = "play"; StartGame(false); ClearSkies(); yield return 3;
        Check($"an old save standing in what's now the danger zone wakes by Baga's jetty ({player.X:0},{player.Y:0})", !InPdz(player.X, player.Y) && OnBaga() && BagaLevel == 0);
        foreach (int f in VolcanoReviewScript()) yield return f;
        foreach (int f in TitleTourScript()) yield return f;
    }

    // The title's tour of the islands and the Case board's locked stories (1.21). FESH_VOLCANO_TEST=title runs only this.
    IEnumerable<int> TitleTourScript()
    {
        Note("The title's tour of the islands, and every story on the Case board");
        state = new State { created = true, look = new Look { name = "Newcomer" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;

        // A new game: all four stories on the board, the last three locked.
        TogglePanel("case"); yield return 3;
        Check($"a new game's Case board has all four stories, the tabs fitting beside Close ({lastCaseTabRight:0})",
            new[] { "The Halcyon", "The vanishing moon", "The restless sea", "Beneath the clouds" }.All(n => Gfx.Seen.ContainsKey(n)) && lastCaseTabRight < 50 + 1180 - 13 - 24 - SmallW("Close") - 4);
        Check("stories 2, 3 and 4 are locked", CaseStories().Count(s => !s.open) == 3 && CaseStories()[0].open);
        bool clicked = ClickButton("Beneath the clouds"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"a locked story's tab opens by a real click, to a locked note that fits ({caseTab}, {lastCaseBottom:0})", clicked && caseTab == "baga" && mode == "panel" && lastCaseBottom <= Gfx.LH);
        Check("its hint points the way without giving the story away", CaseStories()[3].hint.Contains("Manay Mila") && !CaseStories()[3].hint.Contains("volcano"));
        pendingShot = "t01-case-locked"; yield return 2;
        ClickButton("The restless sea"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"story 3 locked, with where to begin ({CaseStories()[2].hint})", caseTab == "amihan" && CaseStories()[2].hint.Contains("Ma'am Isay"));
        state.hinted["metIsay"] = true; yield return 2;
        Check($"after meeting Ma'am Isay, the hint moves on ({CaseStories()[2].hint})", CaseStories()[2].hint.Contains("later day"));
        ClickButton("The Halcyon"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("back to story 1, open", caseTab == "saltmere");
        state.hinted["mg:metMila"] = true; yield return 2;
        ClickButton("Beneath the clouds"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"once Manay Mila is met, story 4's board opens for real ({lastCaseBottom:0})", caseTab == "baga" && CaseStories()[3].open && lastCaseBottom <= Gfx.LH);
        pendingShot = "t02-case-open"; yield return 2;
        ClosePanels(); yield return 2;

        // Quitting to the title from indoors: the tour is outdoors, and loading puts you back where you were.
        var shack = new Build { id = "shack", x = 20, y = 8 };
        state.builds.Add(shack); ReindexBuilds();
        EnterHouse(ShackKey(shack), shack.x * T + 10, shack.y * T + 12);
        for (int i = 0; i < 60 && (!InHouse || mode != "play"); i++) yield return 2;
        float hx = player.X, hy = player.Y;
        QuitToTitle(); yield return 3;
        Check($"quitting to the title from a shack shows the islands outdoors ({scene})", mode == "title" && scene == "world");
        Check($"the tour starts at Saltmere ({player.X:0},{player.Y:0})", Dist(player.X, player.Y, 160, 115) < 30);
        pendingShot = "t03-title-saltmere"; yield return 2;
        for (int i = 1; i < TitleStops.Length; i++)
        {
            titleT = i * TitleStopSecs + TitleStopSecs / 2; yield return 3;
            var stop = TitleStops[i];
            Check($"the tour reaches {stop.name} ({player.X:0},{player.Y:0})", Dist(player.X, player.Y, stop.x, stop.y) < TitleDrift && TitleTour().dark == 0);
            pendingShot = $"t{i + 3:00}-title-{stop.name.Replace(' ', '-').ToLowerInvariant()}"; yield return 2;
        }
        titleT = TitleStopSecs * 3 - 0.1f; yield return 1;
        Check($"between islands it fades through dark ({TitleTour().dark:0.00})", TitleTour().dark > 0.8f);
        Check("Starfall's stop stays well clear of the Starwell's secret glade", Math.Abs(TitleStops[1].x + TitleDrift / 2 - StarwellX) > W / 2 + 70);
        LoadSlot(SaveFile.Slot);
        for (int i = 0; i < 60 && mode != "play"; i++) yield return 2;
        Check($"loading the game puts you back in the shack where you were ({scene}, {player.X:0},{player.Y:0})", scene == ShackKey(shack) && Dist(player.X, player.Y, hx, hy) < 1);
    }

    // A check for each finding of Codex's code review (2026-10-10), each failing before its fix.
    IEnumerable<int> VolcanoReviewScript()
    {
        Note("Beneath the clouds: Codex's review");
        State Fresh(params string[] flags)
        {
            var st = new State { created = true, look = new Look { name = "Reviewer" } };
            st.flags.metTomas = true; st.hinted["amihan"] = true;
            foreach (var k in flags) st.hinted[k] = true;
            return st;
        }
        string[] ordered = { "mg:metMila", "mg:abaca", "mg:pili", "mg:story", "mg:metBen", "mg:unrest", "mg:read1", "mg:read2", "mg:signs", "mg:order" };
        IEnumerable<int> Settle() { for (int i = 0; i < 80 && mode != "play"; i++) { if (mode == "dialogue") Inp.Tap(KeyboardKey.E); yield return 2; } }

        // 1. The helpers finishing inside while you're out in the ash used to "spend an hour in the shelter" outdoors.
        state = Fresh(ordered.Concat(new[] { "mg:evacuated", "mg:preHelped", "mg:ashStarted" }).ToArray());
        state.day = 6; state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.px = SchoolDoorX + 20; state.py = SchoolDoorY + 20;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true; yield return 3;
        mgAshT = 75.5f; yield return 4;
        foreach (var f in Settle()) yield return f;
        Check($"the shelter is finished inside the school, not out in the ash ({scene})", MgSheltered && scene == "house:school");

        // 2. Sheltered in the small hours: rain from earlier that morning isn't rain since the shelter.
        state = Fresh(ordered.Concat(new[] { "mg:evacuated", "mg:preHelped", "mg:ashStarted" }).ToArray());
        state.day = 6; state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.px = SchoolDoorX + 20; state.py = SchoolDoorY + 20;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true;
        state.forecast = new() { new WeatherSpell { at = 0, w = "rain" }, new WeatherSpell { at = 240, w = "clear" } };
        state.weather = planned = "clear"; SnapWeather(); state.clock = 60; yield return 3;
        EnterSchoolNow(); mgAshT = 75.5f; yield return 4;
        foreach (var f in Settle()) yield return f;
        Check($"sheltered at {ClockText(state.clock, 10)}: the morning's rain doesn't count for the lahar watch", MgSheltered && !MgRained);
        state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        Advance(600, quiet: true); yield return 2;
        Check("and the next day's dawn doesn't count it either", !MgRained);
        // Round 2: sheltered at 05:30, a shower at 05:40 during the hour's skip that crosses dawn: the watch starts after it.
        state = Fresh(ordered.Concat(new[] { "mg:evacuated", "mg:preHelped", "mg:ashStarted" }).ToArray());
        state.day = 6; state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.px = SchoolDoorX + 20; state.py = SchoolDoorY + 20;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true;
        state.forecast = new() { new WeatherSpell { at = 0, w = "clear" }, new WeatherSpell { at = 1420, w = "rain" }, new WeatherSpell { at = 1430, w = "clear" } };
        state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        state.weather = planned = "clear"; SnapWeather(); state.clock = 330; yield return 3;
        EnterSchoolNow(); mgAshT = 75.5f; yield return 4;
        foreach (var f in Settle()) yield return f;
        Check($"a shower during the hour in the shelter, across dawn, isn't the rain the watch waits for ({ClockText(state.clock, 10)})", MgSheltered && !MgRained);

        // 3. An old shack whose door now opens into the danger zone, and walking deeper from inside it.
        state = Fresh();
        var shack = new Build { id = "shack", x = 229, y = 50 };
        state.builds.Add(shack); state.px = 2238; state.py = 404;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true; yield return 3;
        EnterHouse(ShackKey(shack), shack.x * T + 10, shack.y * T + 12);
        for (int i = 0; i < 60 && (!InHouse || mode != "play"); i++) yield return 2;
        LeaveToWorld();
        for (int i = 0; i < 60 && (InHouse || mode != "play"); i++) yield return 2;
        Check($"leaving an old shack whose door is in the danger zone puts you outside it ({player.X:0},{player.Y:0})", scene == "world" && !InPdz(player.X, player.Y));
        // Bare ground inside the zone, clear of the cone itself: walking north (toward its middle) would go deeper.
        player.X = 2270; player.Y = 495; yield return 2;
        float depth0 = MathF.Pow((player.X - PdzX) / PdzRX, 2) + MathF.Pow((player.Y - PdzY) / PdzRY, 2), py0 = player.Y;
        Inp.Hold(KeyboardKey.Up, true); yield return 20; Inp.Hold(KeyboardKey.Up, false); yield return 2;
        float depth1 = MathF.Pow((player.X - PdzX) / PdzRX, 2) + MathF.Pow((player.Y - PdzY) / PdzRY, 2);
        Check($"standing inside it, you can't walk further in ({py0:0} -> {player.Y:0}, {depth0:0.00} -> {depth1:0.00})", depth1 >= depth0 - 0.001f);
        Inp.Hold(KeyboardKey.Left, true); yield return 70; Inp.Hold(KeyboardKey.Left, false); yield return 2;
        Check($"and walking away from the crater takes you out ({player.X:0})", !InPdz(player.X, player.Y));

        // 4. No wading off the village while the shelter's being readied (Claude found this one too, before the review).
        state = Fresh(ordered.Concat(new[] { "mg:evacuated" }).ToArray());
        state.day = 6; state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.inv["waders"] = 1; state.px = 1545; state.py = 216;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true; yield return 3;
        // A beach on the village island with shallow water just west of it.
        var beach = (from ty in Enumerable.Range(12, 25) from tx in Enumerable.Range(146, 20)
                     where worldMap[ty, tx] == 's' && worldMap[ty, tx - 1] == 'w' && worldMap[ty, tx - 2] == 'w' && RegionOf(tx * T + 5, ty * T + 5) == "amihan:Amihan Village"
                     select (x: tx * T + 4f, y: ty * T + 8f)).First();
        player.X = beach.x; player.Y = beach.y; yield return 3;
        float wx0 = player.X;
        Inp.Hold(KeyboardKey.Left, true); yield return 40; Inp.Hold(KeyboardKey.Left, false); yield return 2;
        Check($"with waders on, you still can't wade off the village island while the ash is coming ({wx0:0} -> {player.X:0}: {toastMsg})",
            Walkable(TileUnder(player.X, player.Y)) && toastMsg.Contains("ash is coming"));
        // Round 2: a save standing in those shallows can wade back to the beach, but not further out.
        player.X = beach.x - T - 2; player.Y = beach.y; yield return 3;
        float wd0 = WaterDepth(player.X, player.Y);
        Inp.Hold(KeyboardKey.Left, true); yield return 30; Inp.Hold(KeyboardKey.Left, false); yield return 2;
        Check($"standing in the shallows, you can't wade further out ({wd0:0.0} -> {WaterDepth(player.X, player.Y):0.0})", WaterDepth(player.X, player.Y) <= wd0 + 0.01f);
        // Heading for the nearest dry ground always gets you out.
        for (int i = 0; i < 80 && !Walkable(TileUnder(player.X, player.Y)); i++)
        {
            int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
            var land = (from oy in Enumerable.Range(-6, 13) from ox in Enumerable.Range(-6, 13) where Walkable(TileAt(tx + ox, ty + oy))
                        select (x: (tx + ox) * T + 5f, y: (ty + oy) * T + 6f)).OrderBy(p => Dist(player.X, player.Y, p.x, p.y)).First();
            var d = new System.Numerics.Vector2(land.x - player.X, land.y - player.Y);
            Inp.ScriptStick = System.Numerics.Vector2.Normalize(d); yield return 2;
        }
        Inp.ScriptStick = null; yield return 2;
        Check($"but heading for dry ground gets you back on the beach ({player.X:0},{player.Y:0})", Walkable(TileUnder(player.X, player.Y)));
        // Round 3: a save made standing in those shallows loads on dry ground (Codex: a build on the nearest sand could
        // otherwise leave no allowed step).
        player.X = beach.x - T - 2; player.Y = beach.y; Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); ClearSkies(); quietWildlife = true; standStill = true; yield return 3;
        Check($"a save standing in the shallows during the ash phase loads on dry ground ({player.X:0},{player.Y:0})", Walkable(TileUnder(player.X, player.Y)) && CanStand(player.X, player.Y));

        // 5. A two-tile piece whose second tile is in the danger zone.
        state = Fresh(); state.px = 2240; state.py = 500; state.inv["wood"] = 20; state.inv["stone"] = 20;
        mode = "play"; StartGame(false); ClearSkies(); yield return 3;
        string why = PlaceProblem(Data.BuildById["shack"], 223, 48);
        Check($"a shack can't be built half inside the danger zone ({why})", !string.IsNullOrEmpty(why));

        // 6. The report only praises what was measured: a go-bag packed after the order isn't "early", and a walk in the
        // falling ash isn't "stayed inside".
        state = Fresh(ordered);
        state.day = 6; state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.px = CrateX; state.py = CrateY + 9;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true; yield return 3;
        player.Face = "up"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 4;
        for (int i = 0; i < 8; i++) { var r = Gfx.Seen[$"gobag:{i}"]; Inp.ScriptMouse = new System.Numerics.Vector2(r.X + 40, r.Y + 40); Inp.ScriptClickNext = true; yield return 3; }
        Inp.ScriptMouse = Offscreen; yield return 2;
        ClickButton("Done packing"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("packed during the evacuation", state.Hinted("mg:bag"));
        Check($"isn't praised as packing early ({MgReport()})", !MgReport().Contains("packed a go-bag early") && MgReport().Contains("pack a go-bag early"));
        ClosePanels(); yield return 2;
        BoardEvacBoat(false); foreach (var f in Settle()) yield return f;
        mgPrepT = 75.5f; yield return 4;
        EnterSchoolNow(); yield return 3;
        LeaveToWorld(); for (int i = 0; i < 60 && (InHouse || mode != "play"); i++) yield return 2;
        yield return 200;
        EnterSchoolNow(); yield return 3;
        Check($"a walk out in the falling ash isn't 'stayed inside' ({MgReport()})", !MgReport().Contains("stayed inside") && MgReport().Contains("stay inside while ash falls"));

        // 7. Leaving the lahar watch by another panel's key still gets the warning out (Ben, uncredited).
        state = Fresh(ordered.Concat(new[] { "mg:evacuated", "mg:preHelped", "mg:ashStarted", "mg:sheltered", "mg:ash", "mg:rained" }).ToArray());
        state.day = 8; state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.scene = "house:school"; state.exitX = SchoolDoorX; state.exitY = SchoolDoorY + 3;
        state.px = DisplayX; state.py = DisplayY + 8;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true; yield return 3;
        player.X = DisplayX; player.Y = DisplayY + 8; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the display opens", panel == "lahar");
        Inp.Tap(KeyboardKey.Tab); yield return 3;
        Check($"switching to the map still gets the warning out and closes the case ({mode}, {panel})", MgDone && !state.Hinted("mg:r:channel"));
        Check("and the finale's words come up, not the map", mode == "dialogue" && panel == null);
        bool sawCard = false;
        for (int i = 0; i < 120 && mode != "play"; i++)
        {
            if (mode == "dialogue") Inp.Tap(KeyboardKey.E);
            if (mode == "magayoncard") { sawCard = true; yield return 40; Inp.Tap(KeyboardKey.E); }
            yield return 2;
        }
        Check("then its card", sawCard);
    }
}
#endif
