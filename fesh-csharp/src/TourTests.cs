#if DEBUG
using System.Text.Json;
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // First steps (Tutorial.cs), the shack-aware journal and the calm music (1.37). FESH_TOUR_TEST=1 runs only this.
    IEnumerable<int> TourScript()
    {
        Note("First steps, the journal after building a shack, and the calm tracks");
        Inp.ScriptMouse = Offscreen;
        IEnumerable<int> Talk()
        {
            for (int i = 0; i < 200 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
            for (int i = 0; i < 90 && mode == "fade"; i++) yield return 2;
            yield return 2;
        }

        /* ---------- A new game from the title screen, by real clicks ---------- */
        tourOff = false;
        QuitToTitle(); yield return 30;
        bool clickedNew = ClickButton("New game"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3;
        if (mode != "create" && titleView == "slots")
        {
            // Every slot is in use: pick one to replace.
            var slotBtn = Gfx.Seen.Keys.FirstOrDefault(k => k.StartsWith("slot"));
            if (slotBtn != null) { ClickButton(slotBtn); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3; }
        }
        Check($"New game opens the fisher maker ({mode}, {titleView}, {clickedNew})", mode == "create");
        bool started = ClickButton("Start"); yield return 6; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"Start begins the game with First steps on ({mode}, tour {state.tour})", started && mode is "play" or "dialogue" && state.tour == 0);
        foreach (int f in Talk()) yield return f;
        yield return 3;
        Check($"and the card shows it ({tracked?.Id}, shown {GuideShown})", GuideShown && tracked?.Id == "tour:walk");
        pendingShot = "tour-0-new-game"; yield return 2;
        /* ---------- A brand-new fisher ---------- */
        tourOff = false;
        Settings.Data.guide = false;   // First steps ignores this: it's the tutorial
        newSlot = 3;
        StartGame(true, new Look { name = "Newcomer" });
        quietWildlife = true; standStill = true; ClearSkies(); SetNight(false);
        yield return 3;
        Check($"a new fisher starts on the first step ({state.tour})", state.tour == 0);
        Check("and is told where their first steps are", dlg?.Lines.Any(l => l.T.Contains("First steps")) == true);
        foreach (int f in Talk()) yield return f;
        yield return 3;
        Check($"the card shows the step even with the guide off ({tracked?.Id}, {goals.Count} goal)", GuideShown && tracked?.Id == "tour:walk" && goals.Count == 1);
        Check("its title fits the card", Gfx.Measure(tracked.Title, FontKind.Ui700, 17) < CardW - 24 - 22 - 70);
        pendingShot = "tour-1-walk"; yield return 2;
        EnterBuild(); yield return 2;
        Check("building waits until the first steps are done", mode == "play" && toastMsg.Contains("first steps"));
        EnterCaveFromMouth(); yield return 2;
        Check("so does the cave", mode == "play" && scene == "world" && toastMsg.Contains("first steps"));
        var bridgeTile = bridgeSet.OrderBy(b => b.Item1).First();
        Check($"the bridges off Saltmere are roped off while First steps run ({bridgeTile})", BridgeClosed(bridgeTile.Item1, bridgeTile.Item2) && !CanStand(bridgeTile.Item1 * T + 5, bridgeTile.Item2 * T + 5));

        // Walking, by a real held key.
        for (int leg = 0; leg < 8 && state.tour == 0; leg++)
        {
            var key = leg % 2 == 0 ? KeyboardKey.Left : KeyboardKey.Right;
            Inp.Hold(key, true);
            for (int i = 0; i < 45 && state.tour == 0; i++) yield return 1;
            Inp.Hold(key, false); yield return 2;
        }
        Check($"walking a few steps ticks the first one ({state.tour}, {tourWalked:0} px)", state.tour == 1 && goalDoneT > 0);
        Check($"then Tomas, with an arrow ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "tour:tomas" && guideWay?.Place == "Tomas");
        pendingShot = "tour-2-tomas"; yield return 2;
        // Forced: what the step hasn't reached stays shut.
        StartCharge("lagoon"); yield return 2;
        Check("fishing waits until you've met Tomas", mode == "play" && toastMsg.Contains("Meet Tomas"));
        TalkPip(); yield return 2;
        Check("so does Pip's stall", mode == "play" && toastMsg.Contains("Pip's stall opens"));
        Inp.Tap(KeyboardKey.Tab); yield return 3;
        Check("and the map", mode == "play" && toastMsg.Contains("first steps"));
        goalDoneT = 0; yield return 3; pendingShot = "tour-1b-banner"; yield return 2;

        // Meeting him.
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (int f in Talk()) yield return f;
        yield return 3;
        Check($"meeting Tomas moves it on to the first fish ({tracked?.Id}, {guideWay?.Place})", state.flags.metTomas && tracked?.Id == "tour:fish" && guideWay?.Place == Data.SpotById["lagoon"].Label);
        Check("the fish step fits the card (three lines)", Gfx.Wrap(Bind.Fix(tracked.Text), FontKind.Ui500, 15, CardW - 24).Count <= 3);

        // The fish. (Casting and reeling are tested elsewhere; this is the step noticing the catch.)
        state.commons["pond_perch"] = 1; state.inv["pond_perch"] = 1; yield return 3;
        Check($"a first catch moves on to the bag ({tracked?.Id})", tracked?.Id == "tour:bag");
        yield return 3; pendingShot = "tour-3-bag-ring"; yield return 2;
        Inp.Tap(KeyboardKey.I); yield return 4;
        Check("the bag opens by its key", mode == "panel" && panel == "bag");
        ClosePanels(); yield return 4;
        Check($"and ticks that step ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "tour:sell" && guideWay?.Place == "Pip's stall");
        Sell("pond_perch", 1); yield return 4;
        Check($"selling the fish ticks the next ({tracked?.Id})", tracked?.Id == "tour:map");
        Inp.Tap(KeyboardKey.Tab); yield return 4;
        Check("the map opens by its key", mode == "panel" && panel == "map");
        ClosePanels(); yield return 4;
        Check($"then the journal ({tracked?.Id})", tracked?.Id == "tour:journal");
        Inp.Tap(KeyboardKey.Q); yield return 4;
        Check("the journal opens by its key, and that finishes First steps", mode == "panel" && panel == "journal" && state.tour == TourDone);
        ClosePanels(); yield return 4;
        Check($"done: back to the ordinary goals ({state.tour}, {tracked?.Id})", state.tour == TourDone && tracked != null && !tracked.Id.StartsWith("tour:"));
        Check("with a word about the journal", badgeNews?.Contains("basics") == true || toastMsg.Contains("basics"));
        EnterBuild(); yield return 2;
        Check("and building is open", mode == "build"); ExitBuild(); yield return 2;
        Check("and the bridges are open", !BridgeClosed(bridgeTile.Item1, bridgeTile.Item2));
        Check("the guide setting is respected again", !GuideShown);

        /* ---------- Saving, skipping, an early start, Pip shut, old saves ---------- */
        Settings.Data.guide = true;
        state.tour = 3; state.hinted.Remove("tut:bag"); Save(); yield return 2;
        var back = SaveFile.Read();
        Check($"a game closed on step 3 comes back on step 3 ({back?.tour})", back?.tour == 3);
        state = back; StartGame(false); yield return 3;
        Check($"and the card shows that step ({tracked?.Id})", state.tour == 3 && tracked?.Id == "tour:bag");
        Check("a save without the field has finished First steps", JsonSerializer.Deserialize<State>("{}", new JsonSerializerOptions { IncludeFields = true }).tour == TourDone);
        Check("a loaded game that was done stays done", new State().tour == TourDone);

        // The steps already done by the time they come up are ticked off together.
        state.tour = 0; tourWalked = 100; state.flags.metTomas = true; state.commons["pond_perch"] = 1; state.inv["pond_perch"] = 1; state.hinted["tut:bag"] = true;
        foreach (var k in new[] { "tut:sell", "tut:map", "tut:journal" }) state.hinted.Remove(k);
        yield return 3;
        Check($"steps done early are skipped ({state.tour}, {tracked?.Id})", tracked?.Id == "tour:sell");

        // Pip shut at 22:00: nothing to wait for.
        state.hinted.Remove("tut:sell"); state.inv["pond_perch"] = 1; state.clock = 22 * 60; state.tour = 4; yield return 3;
        Check($"Pip shut: that step is let go with a word ({tracked?.Id})", tracked?.Id == "tour:map" && toastMsg.Contains("shut"));
        state.clock = 12 * 60;

        // The way out in the pause menu, by a real click.
        state.tour = 2; state.hinted.Remove("tut:map"); yield return 3;
        OpenPause(); menuTab = "game"; yield return 4;
        pendingShot = "tour-3-pause"; yield return 2;
        bool skipped = ClickButton("Skip First steps"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"Skip First steps works by a real click ({state.tour})", skipped && state.tour == TourDone);
        ClosePause(); yield return 3;

        /* ---------- The journal after a shack ---------- */
        state = new State { created = true, look = new Look { name = "Shack" } };
        state.flags.metTomas = true; state.commons["pond_perch"] = 1; state.caught.Add("glowgill"); state.hinted["ch:toolsAsked"] = true; state.hinted["chapters"] = true;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false); yield return 3;
        foreach (int f in Talk()) yield return f;
        state.inv["wood"] = 8; state.inv["stone"] = 6; yield return 3;
        Check($"before a shack, the tools are made at Tomas's workbench ({tracked?.Id}, {tracked?.Scene})", tracked?.Id == "story:tools" && tracked.Scene == "house:tomas" && tracked.Text.Contains("Tomas's hut"));
        var shack = new Build { id = "shack", x = 13, y = 12 };
        state.builds.Add(shack);
        state.rooms[ShackKey(shack)] = new List<Build> { new() { id = "workbench", x = 3, y = 2 } };
        yield return 3;
        Check($"with your own workbench, the tools are made there ({tracked?.Id}, {tracked?.Scene}, {tracked?.Place})",
            tracked?.Id == "story:tools" && tracked.Scene == ShackKey(shack) && tracked.Place == "Your workbench" && !tracked.Text.Contains("Tomas"));
        state.inv["wood"] = 0; state.inv["stone"] = 0; state.inv["axe"] = 1; state.caught.Add("tidecrawler"); state.hinted["ch:homeAsked"] = true; state.caveDeepest = 1;
        state.hinted["ch:rocks"] = true; state.inv["copper_bar"] = 2; state.inv["wood"] = 2;
        state.rooms[ShackKey(shack)].Add(new Build { id = "furnace", x = 8, y = 2 }); yield return 3;
        Check($"the copper pickaxe is made at your workbench too ({tracked?.Id}, {tracked?.Scene})", tracked?.Id == "story:copperpick" && tracked.Scene == ShackKey(shack) && tracked.Text.Contains("your workbench"));
        state.rooms.Remove(ShackKey(shack));
        state.rooms[ShackKey(shack)] = new();
        state.hinted.Remove("ch:homeAsked"); yield return 3;
        Check($"a shack built before Tomas is asked doesn't send you to ask him ({tracked?.Id})", tracked?.Id == "story:workbench");

        /* ---------- Sand the tide has left, before the flat is wholly dry ---------- */
        state = new State { created = true, look = new Look { name = "Flats" } };
        state.flags.metTomas = true; state.inv["waders"] = 1;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false); yield return 3;
        SetLowTide();
        float lowM = TideMinute(state.day, state.clock);
        for (float off = 0; off < 300 && (FlatsDry || TideDryPx < 4); off += 5) SetTideTo(lowM - off);
        Check($"the tide is part way out ({SeaLevel:0.00}, {TideDryPx:0.0} px dry)", !FlatsDry && TideDryPx >= 4);
        (int x, int y)? sand = null;
        foreach (var (tx, ty) in Flats())
        {
            for (int dy = 0; dy < T && sand == null; dy++) for (int dx = 0; dx < T && sand == null; dx++)
                if (TideDry(tx * T + dx, ty * T + dy)) sand = (tx * T + dx, ty * T + dy);
            if (sand != null) break;
        }
        Check("some of a flat shows as sand", sand != null);
        if (sand is { } p)
        {
            player.X = p.x; player.Y = p.y + 1.5f; yield return 3;
            Check("standing there you aren't wading in water", !InWater);
            state.riding = true;
            Check("and Tidemane doesn't swim in it", !Swimming);
            state.riding = false;
        }
        // Marilaya's sandbar, where the sand is drawn but its tile is still water to the game.
        SetLowTide();
        (int x, int y)? bar = null;
        foreach (var ((tx, ty), _) in sandbar)
        {
            for (int dy = 0; dy < T && bar == null; dy++) for (int dx = 0; dx < T && bar == null; dx++)
                if (SandbarSandAt(tx * T + dx, ty * T + dy) && !SandbarDry(tx, ty)) bar = (tx * T + dx, ty * T + dy);
            if (bar != null) break;
        }
        if (bar == null) foreach (var ((tx, ty), _) in sandbar) { bar = (tx * T + 5, ty * T + 5); break; }
        if (bar is { } q && SandbarSandAt(q.x, q.y))
        {
            player.X = q.x; player.Y = q.y + 1.5f; yield return 3;
            Check($"on the sandbar's sand you aren't wading ({TileAt(q.x / T, q.y / T)})", !InWater);
            state.riding = true;
            Check("and Tidemane doesn't swim there", !Swimming);
            state.riding = false;
        }
        else Check("the sandbar shows sand at low tide", false);

        // Wading in shallow water beside a beach: a soft bowl of water over the knees, not a box (a look at it).
        state = new State { created = true, look = new Look { name = "Wader" } };
        state.flags.metTomas = true; state.inv["waders"] = 1;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false); SetHighTide(); yield return 3;
        (int x, int y)? shore = null;
        for (int ty = 9; ty < 18 && shore == null; ty++) for (int tx = 2; tx < 30 && shore == null; tx++)
            if (TileAt(tx, ty) == 'w' && TileAt(tx - 1, ty) == 'w' && TileAt(tx - 2, ty) == 's' && CanStand(tx * T + 5, ty * T + 5, true)) shore = (tx * T + 5, ty * T + 5);
        Check("there's shallow water by a Saltmere beach", shore != null);
        if (shore is { } sh)
        {
            player.X = sh.x; player.Y = sh.y; player.Face = "left"; yield return 4;
            Check("wading in it", InWater);
            Settings.Data.zoom = 5; yield return 60; pendingShot = "wading"; yield return 3; Settings.Data.zoom = 4; yield return 3;
        }

        // A crab, squid, ray or lobster held up after a catch (13x8) is a shape, not a few stripes.
        var thin = new List<string>();
        foreach (var kv in FishArt.Looks.Where(k => k.Value.Shape is "crab" or "lobster" or "shrimp" or "squid" or "ray"))
        {
            var sheet = new Pix(13, 8);
            FishArt.Draw(sheet, kv.Key, 0, 0, 13, 8);
            int px = sheet.Buf.Count(c => c.A > 0);
            int cols = Enumerable.Range(0, 13).Count(x => Enumerable.Range(0, 8).Any(y => sheet.Buf[y * 13 + x].A > 0));
            if (px < 14 || cols < 4) thin.Add($"{kv.Key} ({px} px, {cols} columns)");
        }
        Check($"every crab, squid and ray held up is a shape ({string.Join(", ", thin)})", thin.Count == 0);

        /* ---------- The calm tracks ---------- */
        int wait = 0;
        while (Music.ReadyCount < Music.Names.Count() + 1 && wait++ < 900) yield return 1;
        Check($"every loop is built ({Music.ReadyCount} of {Music.Names.Count() + 3})", Music.ReadyCount >= Music.Names.Count());
        int rate = Music.SampleRate;
        foreach (var name in Music.Names)
        {
            var pcm = Music.Loop(name);
            if (pcm == null) { Check($"{name} is built", false); continue; }
            int peak = pcm.Max(s => Math.Abs((int)s));
            double sec = pcm.Length / (double)rate;
            if (Music.IsCalm(name))
            {
                int tail = pcm.Length - rate * 5;
                bool quietTail = pcm.Skip(tail).All(s => s == 0);
                double rms = Math.Sqrt(pcm.Take(rate * 30).Sum(s => (double)s * s) / (rate * 30));
                Check($"{name}: a long tune with a rest at the end ({sec:0} s, loudest {peak}, quiet tail {quietTail}, level {rms:0})", sec > 70 && quietTail && peak < 24000 && rms > 100);
            }
            else Check($"{name}: the fight tunes still loop short and continuous ({sec:0} s)", sec < 40 && pcm.Skip(pcm.Length - rate).Any(s => s != 0));
        }
        var sa = Music.Loop("saltmere"); var sb = Music.Rebuild("saltmere");
        Check("a tune is the same every time it's composed", sa != null && sa.AsSpan().SequenceEqual(sb));
    }
}
#endif
