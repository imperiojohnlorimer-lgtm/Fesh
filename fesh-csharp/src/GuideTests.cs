#if DEBUG
using Raylib_cs;

namespace Fesh;

// The guide, the aquarium's layout and the real-fish cards (1.16). FESH_GUIDE_TEST=1 runs only this (it still needs
// FESH_AUTOTEST and FESH_SAVE); the full play-through runs it at the end.
partial class Game
{
    IEnumerable<int> GuideScript()
    {
        Note("The guide, the aquarium and the real fish");
        Inp.ScriptMouse = Offscreen;
        Settings.Data.guide = true;
        state = new State { created = true, look = new Look { name = "Guide tester" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        animals.Clear();
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        yield return 3;

        /* ---------- The story, step by step ---------- */
        Check($"a new game follows the first step: meet Tomas ({tracked?.Id}, {guideWay?.Place})",
            tracked?.Id == "story:tomas" && guideWay != null && Dist(guideWay.X, guideWay.Y, tomasX, tomasY) < 1);
        Check($"the card is on the HUD, under the meters ({guideCardBottom})", GuideShown && guideCardBottom > CardY + 40 && Gfx.Seen.ContainsKey("guide card"));
        player.X = 260; player.Y = 120; yield return 3;
        pendingShot = "170-guide-card"; yield return 2;
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        pendingShot = "171-guide-marker"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the card hides while you talk", !GuideShown && mode == "dialogue");
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        yield return 2;
        Check($"after meeting Tomas: catch a first fish in the lagoon ({tracked?.Id})", tracked?.Id == "story:firstfish" && tracked.Spot == "lagoon");
        Check($"and the card says the last one's done ({goalDoneTitle})", goalDoneT > 0 && goalDoneTitle == "Meet Tomas");
        pendingShot = "172-guide-done"; yield return 2;
        AddCatch(Data.FishById["pond_perch"]); mode = "play"; yield return 2;
        Check($"by day the glow in the lagoon means waiting for night at the campfire ({tracked?.Title}, {guideWay?.Place})",
            tracked?.Id == "story:glowgill" && guideWay?.Place == "Campfire");
        goalDoneT = 0;
        SetNight(true); yield return 2;
        Check($"at night the same step points at the lagoon ({guideWay?.Place})", tracked?.Id == "story:glowgill" && guideWay?.Place == "The lagoon" && goalDoneT == 0);
        state.caught.Add("glowgill"); yield return 2;
        Check($"after the Glowgill, at night: rest until morning ({tracked?.Title})", tracked?.Id == "story:tidecrawler" && guideWay?.Place == "Campfire");
        SetNight(false); yield return 2;
        Check($"by day: the rocky shore ({guideWay?.Place})", guideWay?.Place == "Rocky shore");
        state.caught.Add("tidecrawler"); state.flags.tideOut = true; yield return 2;
        Check($"then the old wreck ({guideWay?.Place})", tracked?.Id == "story:hollow_eel" && guideWay?.Place == "Old wreck");
        state.caught.Add("hollow_eel"); yield return 2;
        Check($"then show Tomas the key card ({tracked?.Title})", tracked?.Id == "story:keycard" && guideWay?.Place == "Tomas");
        state.flags.dockFixed = true; BuildMap(); yield return 2;
        Check($"then the deep water off the dock by day ({guideWay?.Place})", tracked?.Id == "story:mirror_ray" && guideWay?.Place == "Deep water");
        state.caught.Add("mirror_ray"); SetNight(true); yield return 2;
        Check($"and the end of the dock at night ({tracked?.Title})", tracked?.Id == "story:abyssal" && guideWay?.Place == "Deep water");
        SetNight(false);

        // Two old ways the story could get stuck.
        state = new State { created = true, flags = new Flags(), caught = new() { "glowgill" }, look = new Look { name = "Early bird" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        TalkTomasWithRequests(); yield return 2;
        bool intro = dlg?.Lines.Any(l => l.T.StartsWith("Ahoy there")) == true;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("catching the Glowgill before meeting Tomas still gets his introduction (it used to skip it for good)", intro && state.flags.metTomas);
        state.caught.Add("tidecrawler"); state.flags.tideOut = false; Save();
        state = SaveFile.Read(); mode = "play"; StartGame(false);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("a save closed before the tide went out has it out on loading", state.flags.tideOut && SpotOpen("wreck"));

        /* ---------- Through doors, up ladders and across the sea ---------- */
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Guide tester" } };
        state.commons["pond_perch"] = 1; state.caught.Add("glowgill");
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); yield return 3;
        GoTo("house:tomas", 0, 0, "up"); yield return 40;
        for (int i = 0; i < 60 && (scene != "house:tomas" || mode != "play"); i++) yield return 2;
        Check($"indoors, the arrow points at the door first ({guideWay?.Place}: {guideWay?.How})", scene == "house:tomas" && guideWay?.Place == "Door" && guideWay.How.Contains("outside"));
        pendingShot = "173-guide-indoors"; yield return 2;
        state.clock = 23 * 60; yield return 2;
        state.caught.Remove("glowgill"); state.caught.AddRange(new[] { "glowgill", "tidecrawler", "hollow_eel" }); state.flags.tideOut = true; yield return 2;
        Check($"with Tomas asleep, the key card goal points at his bed from inside his hut ({guideWay?.Place})",
            tracked?.Id == "story:keycard" && Dist(guideWay.X, guideWay.Y, TomasBedX, TomasBedY) < 1 && tracked.Text.Contains("asleep"));
        LeaveToWorld(); yield return 40;
        for (int i = 0; i < 60 && (scene != "world" || mode != "play"); i++) yield return 2;
        Check($"outdoors it points at his hut's door ({guideWay?.How})", scene == "world" && Dist(guideWay.X, guideWay.Y, 160, 72) < 1 && guideWay.How.Contains("Tomas's hut"));
        state.clock = 12 * 60;
        caveFloor = 2; LoadScene("cave"); yield return 3;
        Check($"underground, it points up the ladder ({guideWay?.Place})", guideWay?.Place == "Ladder" && Dist(guideWay.X, guideWay.Y, ropeTile.x * T + 5, ropeTile.y * T + 8) < 1);
        LoadScene("world"); player.X = 160; player.Y = 115; yield return 3;

        // Off the view: an arrow at the edge, kept clear of the card.
        var (ex, ey) = EdgePoint(Gfx.LW / 2, Gfx.LH / 2, -0.8f, -0.6f);
        Check($"an arrow at the top-left edge sits below the card ({ex:0},{ey:0} vs {guideCardBottom:0})", ey >= guideCardBottom + 20 && ex < CardX + CardW + 24);
        state.caught.Clear(); state.caught.Add("glowgill"); player.X = 100; player.Y = 520; yield return 3;
        pendingShot = "174-guide-edge-arrow"; yield return 2;
        player.X = 160; player.Y = 115; yield return 2;

        /* ---------- Requests ---------- */
        // While you follow the story, a new request is announced (it isn't on the card).
        requestsKnown = new HashSet<string>(); toastTimer = 0;
        state.req = new Req { item = "pond_perch", count = 3, coins = 20 }; yield return 3;
        Check($"a new request is announced, pointing at the journal ({toastMsg})", toastMsg.StartsWith("New in your journal: Tomas wants 3 pond perch") && tracked?.Group == "story");
        state.flags.ended = true; state.caught = Data.Creatures.Select(c => c.Id).ToList(); yield return 2;
        Check($"with the story done it's followed by itself, at the fish's spot ({tracked?.Id}: {tracked?.Text})",
            tracked?.Id == "req:tomas" && tracked.Spot == "lagoon" && tracked.Text.Contains("lagoon") && tracked.Text.Contains("You have 0 of 3"));
        state.inv["pond_perch"] = 3; yield return 2;
        Check($"with the fish in your bag, it's ready and points at Tomas ({guideWay?.Place})", tracked.Ready && guideWay?.Place == "Tomas");
        state.req = new Req { item = "egg", count = 2, coins = 20 }; yield return 2;
        Check($"an egg points at a chicken ({tracked?.Text}, {guideWay?.Place})", tracked.Text.Contains("chicken") && guideWay?.Place == "A chicken");
        state.req = new Req { item = "copper_bar", count = 2, coins = 60 }; yield return 2;
        Check($"a copper bar points at Pip ({guideWay?.Place})", guideWay?.Place == "Pip's stall");
        state.req = new Req { item = "grilled_fish", count = 2, coins = 35 }; yield return 2;
        Check($"grilled fish points at a campfire ({tracked?.Text})", guideWay?.Place == "Campfire" && tracked.Text.Contains("campfire"));
        state.req = null; state.inv.Remove("pond_perch"); yield return 2;

        // Exploring: the boat, then the atoll.
        Check($"no boat yet: build one, starting at Pip's stall ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "explore:boat" && guideWay?.Place == "Pip's stall");
        state.inv["sailcloth"] = 1; state.inv["iron_bar"] = 4; state.inv["wood"] = 20; yield return 2;
        Check($"with everything for it: the workbench in Tomas's hut ({guideWay?.How})", tracked.Ready && tracked.Scene == "house:tomas" && guideWay.How.Contains("Tomas's hut"));
        state.inv["boat"] = 1; yield return 2;
        Check($"with a boat: sail to the atoll from Pip's jetty ({guideWay?.Place}: {guideWay?.How})",
            tracked?.Id == "explore:atoll" && guideWay?.Place == "Pip's jetty" && guideWay.How.Contains("sail to Starfall Atoll"));
        state.hinted["visitedAtoll"] = true; player.X = AtollJettyX + 4; player.Y = AtollJettyY + 1; yield return 3;
        Check($"from the atoll, Amihan: take the helm at its jetty and steer east ({guideWay?.How})",
            tracked?.Id == "explore:amihan" && guideWay?.Place == "Atoll jetty" && guideWay.How.Contains("steer east"));
        bool spoiler = goals.Any(g => (g.Title + g.Text).Contains("Starwell") || (g.Title + g.Text).Contains(Data.MountName) || (g.Title + g.Text).Contains("hoof"));
        Check("nothing gives the atoll's secret away", !spoiler && !state.Hinted("starwell"));
        state.hinted["amihan"] = true; state.hinted["nikoAsked"] = true;
        player.X = 1595; player.Y = 200; yield return 3;
        var niko = goals.FirstOrDefault(g => g.Id == "niko:bangus");
        Check($"Niko's bangus are in the journal, at the village pond ({niko?.Text})", niko != null && niko.Spot == "amihanpond");
        Check($"and the people of Amihan you haven't met ({goals.FirstOrDefault(g => g.Id == "meet:amihan")?.Title})", goals.Any(g => g.Id == "meet:amihan"));
        // Aboard far from home: the way back is by the atoll's jetty.
        state.track = "explore:boat"; yield return 2;
        Check("a goal that's gone falls back to following by itself", state.track == "");
        state.req = new Req { item = "rock_goby", count = 2, coins = 25 }; state.track = "req:tomas";
        state.boatX = 1480; state.boatY = 240; state.aboard = true; player.X = 1480; player.Y = 240; yield return 3;
        Check($"aboard in Amihan, home is by the atoll's jetty ({guideWay?.Place}: {guideWay?.How})", guideWay?.Place == "Atoll jetty" && guideWay.How.Contains("sail home"));
        state.aboard = false; player.X = 1495; player.Y = 222; yield return 2;
        Check($"on foot in Amihan, it's back to your boat first ({guideWay?.Place})", guideWay?.Place == "Your boat");
        player.X = 160; player.Y = 115; state.boatX = state.boatY = 0; state.track = ""; yield return 3;

        // Protected moments: no card or arrow over a race, the eclipse or the Starwell fight.
        race = new Race();
        Check("the guide stays out of the way of a race", !GuideShown);
        race = null; goalDoneT = 0; yield return 2;

        /* ---------- The journal ---------- */
        Check("the card is where you click for the journal", ClickButton("guide card"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking the card opens the journal ({mode}/{panel})", mode == "panel" && panel == "journal");
        pendingShot = "175-journal"; yield return 2;
        Check($"its lists fit the panel ({lastJournalBottom:0})", lastJournalBottom <= (Gfx.LH + 650) / 2 - 20);
        string pick = goals.FirstOrDefault(g => g.Id != tracked?.Id)?.Id;
        Check($"each goal has a Follow button ({pick})", pick != null && ClickButton("follow:" + pick));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking Follow follows it ({state.track} / {tracked?.Id})", state.track == pick && tracked?.Id == pick);
        Check("and Automatic goes back to the story and requests", ClickButton("Automatic"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"({state.track})", state.track == "");
        Check("the journal's button hides the arrow", ClickButton("Hide the arrow"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check("and turns the setting off", !Settings.Data.guide);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"with the guide off there's no card ({guideCardBottom})", mode == "play" && guideCardBottom == 0 && !GuideShown);
        Inp.Tap(KeyboardKey.Q); yield return 3;
        Check("Q still opens the journal", mode == "panel" && panel == "journal");
        Check("and Show the arrow turns it back on", ClickButton("Show the arrow"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Inp.Tap(KeyboardKey.Q); yield return 3;
        Check("Q closes it again", mode == "play" && Settings.Data.guide);
        Inp.Tap(KeyboardKey.Escape); yield return 4;
        Check("a gamepad gets there from the pause menu's Journal button", mode == "pause" && ClickButton("Journal"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"({mode}/{panel})", mode == "panel" && panel == "journal");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        menuTab = "settings"; yield return 3;
        pendingShot = "176-settings-guide"; yield return 2;
        menuTab = "game"; Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Getting started.
        state.caught.Clear(); state.flags.ended = false;
        int before = Basics().Count(b => b.done);
        TogglePanel("map"); yield return 2; ClosePanels();
        state.inv["berries"] = 2; state.food = 50; Eat("berries");
        Check($"the checklist ticks off what you've done ({before} -> {Basics().Count(b => b.done)})", Basics().Count(b => b.done) == before + 2 && state.Hinted("tut:map") && state.Hinted("tut:eat"));

        /* ---------- The aquarium ---------- */
        var many = Data.AllCommon.Where(f => !f.Legend).Take(25).Select(f => f.Id).ToList();
        foreach (var id in many) state.inv[id] = 2;
        var tankBuild = new Build { id = "aquarium", x = 20, y = 12 };
        OpenTank(tankBuild); yield return 3;
        float panelBottom = (Gfx.LH - 690) / 2 + 690 - 24;
        Check($"25 kinds of fish: two pages, and everything inside the panel ({lastTankBottom:0} <= {panelBottom:0})", lastTankBottom <= panelBottom);
        pendingShot = "177-aquarium"; yield return 2;
        Check("the next page is a click away", ClickButton(">"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"(page {tankPage + 1})", tankPage == 1);
        foreach (var id in many.Take(4)) TankPut(id);
        tankPage = 0; yield return 2;
        // Point at a fish while the tank is full: its name shows on the line under the cards, clear of them.
        float fx = (Gfx.LW - 1000) / 2 + 24 + 400 + 36, fy = (Gfx.LH - 690) / 2 + 24 + 84 + 34 + 2 * 80 + 36;
        Inp.ScriptMouse = new System.Numerics.Vector2(fx, fy); yield return 3;
        pendingShot = "178-aquarium-full"; yield return 2;
        Inp.ScriptMouse = Offscreen;
        ClosePanels(); state.tanks.Clear(); yield return 2;

        /* ---------- The real fish ---------- */
        var noFact = Data.AllCommon.Where(f => !FishFacts.ById.ContainsKey(f.Id)).Select(f => f.Id).ToList();
        Check($"every fish has a real-world note ({noFact.Count} missing: {string.Join(", ", noFact.Take(4))})", noFact.Count == 0);
        const float boxW = DexPicW + 6, boxH = DexCardH - DexCardPad - (DexCardPad + 50 + DexPicH + 14);
        var tooLong = FishFacts.ById.Where(kv => FactLayout(kv.Value, boxW) is var (sci, body) && FactHeight(sci, body) > boxH).Select(kv => kv.Key).ToList();
        Check($"every note fits its box on the card ({tooLong.Count} don't: {string.Join(", ", tooLong.Take(4))})", tooLong.Count == 0);
        bool ascii = FishFacts.ById.Values.All(f => (f.Sci + f.Text).All(c => c < 128 || c == 'é'));
        Check("they only use letters the fonts have", ascii);
        foreach (var id in new[] { "bangus", "galunggong", "storm_eel" })
        {
            state.commons[id] = 1;
            TogglePanel("dex"); dexFish = id; yield return 3;
            pendingShot = $"179-fish-card-{id}"; yield return 2;
            ClosePanels(); yield return 2;
        }
        state.hinted["fishBag"] = true; state.commons.Remove("tamban");
        fish = new FishCast { Spot = "parola", Bx = player.X, By = player.Y - 20 };
        reel = new ReelState { Roll = new Catchable { Id = "tamban", Name = "Tamban (sardine)", Difficulty = 1 } };
        mode = "reeling";
        LandCatch();
        Check($"a first catch points at its card in the Fesh-dex ({toastMsg})", toastMsg.Contains("New in your Fesh-dex") && state.commons["tamban"] == 1);
        reel = null; fish = null; mode = "play"; yield return 2;
        foreach (int frames in GuideReviewScript()) yield return frames;
    }

    // Codex's review of 1.16: each of these failed before its fix.
    IEnumerable<int> GuideReviewScript()
    {
        Note("The guide: Codex's review");
        Inp.ScriptMouse = Offscreen;
        // A save that finished the story without ever meeting Tomas (the old early-Glowgill bug).
        state = new State { created = true, flags = new Flags { ended = true, dockFixed = true, tideOut = true }, look = new Look { name = "Never met" } };
        state.caught = Data.Creatures.Select(c => c.Id).ToList();
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        TalkTomasWithRequests(); yield return 2;
        bool asks = dlg?.Lines.Any(l => l.T.Contains("could you bring me")) == true;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"a finished story that never met Tomas still gets his requests ({state.req?.item})", state.flags.metTomas && asks && state.req != null);
        Check($"and its old checklist counts as done ({Basics().Count(b => b.done)} of {Basics().Length})", Basics().All(b => b.done));

        // A new game that rushes the story still has to do the basics.
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Quick" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false);
        state.caught.AddRange(new[] { "glowgill", "tidecrawler" }); yield return 2;
        Check($"a new game well into the story hasn't sold anything just by catching ({Basics().First(b => b.title.StartsWith("Sell")).done})", !Basics().First(b => b.title.StartsWith("Sell")).done);

        // Lots of goals: the journal pages them and every row stays inside the panel.
        state.flags.ended = true; state.caught = Data.Creatures.Select(c => c.Id).ToList();
        state.req = new Req { item = "pond_perch", count = 3, coins = 20 };
        foreach (var k in new[] { "amihan", "nikoAsked", "liraSupperAsked", "guso", "fireflies", "metJoy", "habagat", "metCelso", "metDado", "metRosa" }) state.hinted[k] = true;
        TogglePanel("journal"); yield return 3;
        float journalEnd = (Gfx.LH - 650) / 2 + 650;
        Check($"a full page of goals ({goals.Count}) stays inside the journal ({lastJournalBottom:0} <= {journalEnd - 8:0})", goals.Count > JournalRows && lastJournalBottom <= journalEnd - 8);
        pendingShot = "180-journal-full"; yield return 2;
        Check("and the next page is a click away", ClickButton(">"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"(page {journalPage + 1})", journalPage == 1);
        ClosePanels(); yield return 2;

        // Amihan's islands aren't joined: from the village, Maya's lagoon is by boat.
        state.inv["boat"] = 1; state.track = "maya:coop";
        state.boatX = 1480; state.boatY = 220; player.X = 1595; player.Y = 200; yield return 3;
        Check($"on foot in the village, Maya's lagoon on Luntian is by boat ({guideWay?.Place}: {guideWay?.How})", guideWay?.Place == "Your boat");
        // Habagat's too: on Parola with the boat moored there, it's your boat, not Asinan's landing.
        state.inv["daing"] = 3; state.track = "rosa:order"; state.boatX = 1150; state.boatY = 560; player.X = 1238; player.Y = 655; yield return 3;
        Check($"on Parola, Manang Rosa on Asinan is by your boat ({guideWay?.Place})", tracked?.Id == "rosa:order" && guideWay?.Place == "Your boat");
        state.track = ""; state.boatX = state.boatY = 0; player.X = 160; player.Y = 115; yield return 2;

        // With nothing else open, Manang Rosa's order is still followed.
        state.req = null; state.hinted.Clear();
        foreach (var k in new[] { "habagat", "metRosa", "visitedAtoll", "amihan", "metJoy", "sanctuaryReward", "guso", "gusoCoop", "bubo", "liraSupperAsked", "rondalla",
            "niko_request", "niko_asohos", "nikoAsohosAsked", "metPacing", "metDado", "metCelso", "isletsCharted", "bk:agong", "talaReward", "fireflies",
            "tides", "metIsay" }) state.hinted[k] = true;
        state.parola = 3; state.hinted["parolaLit"] = true; state.hinted["moonReturned"] = true; yield return 2;
        Check($"when it's the only goal, Automatic follows Rosa's order ({string.Join(", ", goals.Select(g => g.Id))} -> {tracked?.Id})", tracked?.Id == "rosa:order");

        // Niko asks for asohos the next time you talk, not at the moment he takes the bangus.
        state.hinted.Remove("niko_asohos"); state.hinted.Remove("nikoAsohosAsked"); yield return 2;
        Check("Niko's asohos aren't in the journal until he's asked for them", !goals.Any(g => g.Id == "niko:asohos"));
        player.X = 1715; player.Y = 200; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"then they are ({target?.Label})", goals.Any(g => g.Id == "niko:asohos"));
        player.X = 160; player.Y = 115; yield return 2;

        // Underground, a pool on an upper floor is back up the ladder.
        state.req = new Req { item = "blind_cavefish", count = 1, coins = 30 }; state.track = "req:tomas";
        caveFloor = AncientFloor; LoadScene("cave"); yield return 3;
        Check($"on the Ancient Floor, the cave pool is back up the ladder ({guideWay?.Place})", guideWay?.Place == "Ladder");
        LoadScene("world"); player.X = 160; player.Y = 115; yield return 2;

        // Inside the sanctuary nothing may be fished, so the arrow still points out of it (from deep water there, if any).
        state.req = new Req { item = "galunggong", count = 2, coins = 30 };
        var deepIn = Enumerable.Range(0, COLS * ROWS).Select(i => (x: i % COLS * T + 5f, y: i / COLS * T + 5f))
            .FirstOrDefault(p => worldMap[(int)p.y / T, (int)p.x / T] == '~' && InSanctuary(p.x, p.y) && BoatCanStand(p.x, p.y));
        if (deepIn.x == 0) deepIn = (SanctCX * T, SanctCY * T);
        state.aboard = true; state.boatX = player.X = deepIn.x; state.boatY = player.Y = deepIn.y; yield return 3;
        Note($"   (in the sanctuary over {(OverDeepSea ? "deep water" : "shallows")}, the {SeaSpotHere})");
        Check($"over the sanctuary, the Amihan sea goal still points somewhere you may fish ({guideWay?.Place})", guideWay != null && !InSanctuary(guideWay.X, guideWay.Y));
        state.aboard = false; state.boatX = state.boatY = 0; player.X = 160; player.Y = 115; state.req = null; state.track = ""; yield return 2;

        // A marker in view never sits on the goal card.
        // Its whole extent, both chevrons and the bob, for points all down the card's column.
        var clash = Enumerable.Range(0, 60).Select(i => 100f + i * 6).Where(sy => MarkerAt(200, sy) is { } mk
            && (mk.up ? (top: mk.y - 11, bottom: mk.y + 18) : (top: mk.y - 18, bottom: mk.y + 11)) is var e && e.bottom > CardY && e.top < guideCardBottom).ToList();
        Check($"a goal near the card never has its marker on it ({clash.Count} points do, card to {guideCardBottom:0})", guideCardBottom > CardY && clash.Count == 0);

        // The aquarium by real clicks: put a fish in, take it out.
        state.inv.Clear(); state.inv["pond_perch"] = 1; state.inv["mud_carp"] = 2;
        OpenTank(new Build { id = "aquarium", x = 20, y = 12 }); yield return 3;
        Check("a fish in your bag is a click away", ClickButton("tankfish:pond_perch"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking it puts it in the tank ({string.Join(",", Tank(tankKey))})", Tank(tankKey).Contains("pond_perch") && Has("pond_perch") == 0);
        Check("and Take out", ClickButton("Take out"));
        yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"takes it back out ({Has("pond_perch")})", Has("pond_perch") == 1 && Tank(tankKey).Count == 0);
        ClosePanels(); state.tanks.Clear(); yield return 2;

        // The journal on a gamepad: Start, then the pointer and A on Journal.
        Inp.TapPad(GamepadButton.MiddleRight); yield return 3;
        bool paused = mode == "pause";
        Inp.ScriptMouse = null;
        if (Gfx.Seen.TryGetValue("Journal", out var jb)) Inp.PadCursor = new System.Numerics.Vector2(jb.X + jb.Width / 2, jb.Y + jb.Height / 2);
        yield return 2;
        Inp.TapPad(GamepadButton.RightFaceDown); yield return 3;
        Check($"Start, then A on Journal, opens the journal ({mode}/{panel})", paused && mode == "panel" && panel == "journal");
        Inp.TapPad(GamepadButton.RightFaceRight); yield return 3;
        Check("and B closes it", mode == "play");
        Inp.ScriptMouse = Offscreen; Inp.PadCursor = null;
        Inp.Tap(KeyboardKey.Escape); yield return 3; Inp.Tap(KeyboardKey.Escape); yield return 3;

        // The facts Codex corrected.
        Check("clownfish aren't said to be born male", !FishFacts.ById["clownfish"].Text.Contains("born male"));
        Check("lungfish aren't said to be kin of the first land animals", !FishFacts.ById["sunscale_lungfish"].Text.Contains("first land animals"));
        Check("Old Whiskers' note doesn't use a fish that loses its barbels", !FishFacts.ById["old_whiskers"].Sci.Contains("Pangasianodon"));
        mode = "play";
    }
}
#endif
