#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The Saltmere mystery's chapters (1.22: tools, a home and the caverns between the creatures), the Fesh-dex's
    // chapter strip, Getting started's pages, Pip's bars, and the Clear font. FESH_CHAPTER_TEST=1 runs only this.
    IEnumerable<int> ChapterScript()
    {
        Note("The Saltmere mystery's chapters, Getting started, Pip's bars and the Clear font");
        Inp.ScriptMouse = Offscreen;
        IEnumerable<int> Talk()
        {
            for (int i = 0; i < 200 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
            for (int i = 0; i < 90 && mode == "fade"; i++) yield return 2;
            yield return 2;
        }
        IEnumerable<int> AtTomasTalk()
        {
            player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
            Inp.Tap(KeyboardKey.E); yield return 4;
        }

        /* ---------- A new game: the first creature, then tools for the fallen rocks ---------- */
        state = new State { created = true, look = new Look { name = "Chapters" } };
        state.hinted["chapters"] = true;   // as StartGame(true) stamps a new game
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false);
        foreach (int f in Talk()) yield return f;
        Check("a game started now is stamped, so Pip doesn't keep bars for it", state.Hinted("chapters") && !state.Hinted("pipBars"));
        Check($"the fallen rocks are on the rocky shore, and solid ({CanStand(RockfallX, RockfallY + 2)})", !RocksCleared && !CanStand(RockfallX, RockfallY + 2));
        player.X = RockfallX - 12; player.Y = RockfallY + 3; player.Face = "right"; yield return 3;
        Check($"without a pickaxe they're only looked at ({target?.Type}: {target?.Label})", target?.Type == "info" && target.Label.Contains("pickaxe"));
        pendingShot = "chapter-01-rocks"; yield return 2;
        // The rocky shore still fishes beside them.
        player.X = 200; player.Y = 36; player.Face = "up"; yield return 3;
        Check($"the rocky shore still fishes beside the rocks ({target?.Type} {target?.Id})", target?.Type == "spot" && target.Id == "rocks");

        state.flags.metTomas = true; state.commons["pond_perch"] = 1; state.caught.Add("glowgill"); yield return 3;
        Check($"after the Glowgill, the guide sends you to Tomas with the tag ({tracked?.Id})", tracked?.Id == "story:tag" && guideWay?.Place == "Tomas");
        Check("the Tidecrawler doesn't bite while the rocks cover its pools", !Eligible(Data.ById["tidecrawler"]) && !CatchOdds("rocks", 1).Any(o => o.id == "creature"));
        foreach (int f in AtTomasTalk()) yield return f;
        bool asksTools = dlg?.Lines.Any(l => l.T.Contains("pickaxe")) == true;
        pendingShot = "chapter-02-tomas-tools"; yield return 2;
        foreach (int f in Talk()) yield return f;
        Check("Tomas asks for an axe and a pickaxe to shift the rocks", asksTools && state.Hinted("ch:toolsAsked"));
        Check($"then: make the tools ({tracked?.Id}: {tracked?.Text})", tracked?.Id == "story:tools" && tracked.Text.Contains("stone axe") && tracked.Text.Contains("stone pickaxe"));
        state.inv["wood"] = 6; state.inv["stone"] = 5; yield return 2;
        Check($"with the wood and stone, it's off to Tomas's workbench ({guideWay?.Place}, {guideWay?.How})", tracked.Ready && tracked.Scene == "house:tomas" && guideWay?.How?.Contains("Tomas's hut") == true);
        Craft(Items.Recipes.First(r => r.Out == "axe"));
        Craft(Items.Recipes.First(r => r.Out == "pickaxe"));
        yield return 3;
        Check($"with both tools: clear the fallen rocks ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "story:rocks" && guideWay?.Place == "Fallen rocks");
        player.X = RockfallX - 12; player.Y = RockfallY + 3; player.Face = "right"; yield return 3;
        Check($"facing the rocks with a pickaxe ({target?.Label})", target?.Type == "rockfall");
        int stone0 = Has("stone");
        for (int i = 0; i < 4 && mode == "play"; i++) { Inp.Tap(KeyboardKey.E); yield return 8; }
        pendingShot = "chapter-03-rocks-broken"; yield return 2;
        foreach (int f in Talk()) yield return f;
        Check($"four blows clear them, for 3 stone ({stone0} -> {Has("stone")})", RocksCleared && Has("stone") == stone0 + 3 && CanStand(RockfallX, RockfallY + 2));
        Check("now the Tidecrawler bites, by day", Eligible(Data.ById["tidecrawler"]) && tracked?.Id == "story:tidecrawler");
        foreach (int f in AtTomasTalk()) yield return f;
        bool cleared = dlg?.Lines.Any(l => l.T.Contains("cleared those rocks")) == true;
        foreach (int f in Talk()) yield return f;
        Check("Tomas knows you've cleared them", cleared);

        /* ---------- The Tidecrawler, then a home and copper for the wreck's hatch ---------- */
        state.caught.Add("tidecrawler"); state.flags.tideOut = true; BuildMap(); yield return 3;
        Check($"after the Tidecrawler: walk out to the wreck ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "story:wreck" && guideWay?.Place == "Old wreck");
        Check("the Hollow Eel doesn't bite while the hatch is shut", !Eligible(Data.ById["hollow_eel"]));
        player.X = HatchFrontX; player.Y = HatchFrontY; player.Face = "left"; yield return 3;
        Check($"at the wreck, the rusted hatch ({target?.Label})", target?.Type == "hatch" && target.Label.Contains("rusted"));
        pendingShot = "chapter-04-hatch-shut"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 4;
        bool bounces = dlg?.Lines.Any(l => l.T.Contains("bounces off")) == true;
        foreach (int f in Talk()) yield return f;
        Check("a stone pickaxe bounces off it", bounces && state.Hinted("ch:hatchSeen") && !HatchOpen);
        Check($"so: ask Tomas ({tracked?.Id})", tracked?.Id == "story:hatch:ask");
        int wood0 = Has("wood"), stoneT = Has("stone");
        foreach (int f in AtTomasTalk()) yield return f;
        bool home = dlg?.Lines.Any(l => l.T.Contains("place of your own")) == true && dlg.Lines.Any(l => l.T.Contains("copper"));
        foreach (int f in Talk()) yield return f;
        Check($"Tomas: copper opens it, so a home with a workbench and a furnace, and some wood and stone to start ({wood0} -> {Has("wood")})",
            home && state.Hinted("ch:homeAsked") && Has("wood") == wood0 + 5 && Has("stone") == stoneT + 5);
        Check($"then: build a shack ({tracked?.Id})", tracked?.Id == "story:shack" && tracked.Text.Contains("8 wood"));
        // A shack, by the build bar's rules.
        state.inv["wood"] = 30; state.inv["stone"] = 30;
        var spot = (x: 13, y: 12);
        player.X = spot.x * T + 10; player.Y = (spot.y + 2) * T + 4; player.Face = "up"; yield return 2;
        string problem = PlaceProblem(Data.BuildById["shack"], spot.x, spot.y);
        state.builds.Add(new Build { id = "shack", x = spot.x, y = spot.y }); ReindexBuilds(); yield return 3;
        Check($"with a shack: a workbench in it, pointing at its door ({problem}; {tracked?.Id}, {guideWay?.Place})",
            problem == "" && tracked?.Id == "story:workbench" && guideWay?.Place == "Your shack" && Dist(guideWay.X, guideWay.Y, spot.x * T + 10, spot.y * T + 14) < 3);
        player.X = spot.x * T + 10; player.Y = spot.y * T + 14; player.Face = "up"; yield return 3;
        Check($"the shack's door is right there ({target?.Type})", target?.Type == "door");
        Inp.Tap(KeyboardKey.E); yield return 40;
        for (int i = 0; i < 60 && (mode != "play" || !InHouse); i++) yield return 2;
        Check($"inside your shack, no arrow: anywhere will do ({scene}, {guideWay})", InOwnHouse && guideWay == null && tracked?.Id == "story:workbench");
        var room = SceneBuilds();
        room.Add(new Build { id = "workbench", x = 3, y = 2 }); ReindexBuilds(); yield return 3;
        Check($"a workbench at home: now a furnace ({tracked?.Id})", tracked?.Id == "story:furnace");
        room.Add(new Build { id = "furnace", x = 8, y = 2 }); ReindexBuilds(); yield return 3;
        Check($"a furnace too: down into Frostfang Caverns ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "story:caverns" && guideWay?.Place == "Door");
        pendingShot = "chapter-05-home"; yield return 2;
        LeaveToWorld(); yield return 40;
        for (int i = 0; i < 60 && (scene != "world" || mode != "play"); i++) yield return 2;
        Check($"outdoors it points at the cave mouth ({guideWay?.Place})", guideWay?.Place == "Frostfang Caverns");

        // Underground: the arrow finds the copper.
        caveFloor = 1; LoadScene("cave"); state.caveDeepest = Math.Max(state.caveDeepest, 1); AtLadder(); yield return 3;
        Check($"in the caverns: mine copper ({tracked?.Id}: {tracked?.Title})", tracked?.Id == "story:copper:mine" && tracked.Title.Contains("0 of 4"));
        var near = nodes.Where(n => n.Kind == "copper" && !n.Mined).OrderBy(n => Dist(player.X, player.Y, n.X * T + 5, n.Y * T + 10)).FirstOrDefault();
        Check($"the arrow points at the nearest copper rock ({guideWay?.Place})", near != null && guideWay?.Place == "Copper ore" && Dist(guideWay.X, guideWay.Y, near.X * T + 5, near.Y * T + 10) < 1);
        pendingShot = "chapter-06-copper-arrow"; yield return 2;
        for (int k = 0; k < 2; k++)
        {
            AtLadder();
            var rock = ClosestNode("copper");
            if (rock == null || !StandBeside(rock.X, rock.Y)) { Check("standing by a copper rock", false); break; }
            yield return 3;
            for (int i = 0; i < 3; i++) { Inp.Tap(KeyboardKey.E); yield return 6; }
        }
        Check($"two rocks, four copper ore ({Has("copper_ore")}): smelt it ({tracked?.Id})", Has("copper_ore") >= 4 && tracked?.Id == "story:copper:smelt" && state.Hinted("tut:ore:copper"));
        Check($"underground, the smelting points up the ladder ({guideWay?.Place})", guideWay?.Place == "Ladder");
        caveFloor = 1; LoadScene("world"); player.X = 160; player.Y = 115; yield return 3;
        Check($"outdoors, at your shack ({guideWay?.Place})", guideWay?.Place == "Your shack");
        Check("Pip doesn't sell copper bars before you've smelted any", !ShopStock().Any(s => s.id == "copper_bar") && !ShopStock().Any(s => s.id == "iron_bar"));
        var bar = Items.Recipes.First(r => r.Out == "copper_bar");
        Craft(bar); Craft(bar);
        for (int i = 0; i < 200 && !toastMsg.Contains("Pip will keep"); i++) yield return 2;
        Note($"after the wait: mode {mode}, panel {panel}, scene {scene}, toast {toastTimer:0.0} '{toastMsg}', news '{badgeNews}', at {player.X:0},{player.Y:0}");
        Check($"two copper bars ({Has("copper_bar")}): Pip stocks them now, not iron yet, and says so after the second bar's toast ({toastMsg})",
            Has("copper_bar") == 2 && ShopStock().Any(s => s.id == "copper_bar") && !ShopStock().Any(s => s.id == "iron_bar") && toastMsg.Contains("Pip will keep"));
        Check($"then: a copper pickaxe at your workbench ({tracked?.Id}, {tracked?.Scene})", tracked?.Id == "story:copperpick" && tracked.Scene == ShackKey(state.builds.First(b => b.id == "shack")));
        Craft(Items.Recipes.First(r => r.Out == "copper_pickaxe")); yield return 3;
        Check($"with a copper pickaxe: open the hatch ({tracked?.Id}, {guideWay?.Place})", tracked?.Id == "story:hatch" && guideWay?.Place == "Old wreck");
        player.X = HatchFrontX; player.Y = HatchFrontY; player.Face = "left"; yield return 3;
        Note($"at the hatch: mode {mode}, scene {scene}, tideOut {state.flags.tideOut}, hatch {HatchOpen}, at {player.X:0},{player.Y:0}");
        Check($"the hatch, with a copper pickaxe ({target?.Label})", target?.Type == "hatch" && target.Label.Contains("copper"));
        Inp.Tap(KeyboardKey.E); yield return 6;
        pendingShot = "chapter-07-hatch-open"; yield return 2;
        foreach (int f in Talk()) yield return f;
        Check("it opens, and the Hollow Eel bites at the wreck", HatchOpen && Eligible(Data.ById["hollow_eel"]) && tracked?.Id == "story:hollow_eel");
        player.X = HatchFrontX; player.Y = HatchFrontY; yield return 3;
        Check($"the open hatch is no longer a target ({target?.Type})", target?.Type != "hatch");

        /* ---------- The key card, then iron for the dock ---------- */
        state.caught.Add("hollow_eel"); yield return 3;
        Check($"after the Hollow Eel: the key card ({tracked?.Id})", tracked?.Id == "story:keycard");
        foreach (int f in AtTomasTalk()) yield return f;
        bool iron = dlg?.Lines.Any(l => l.T.Contains("two iron bars")) == true;
        foreach (int f in Talk()) yield return f;
        Check("Tomas will mend the dock, with two iron bars", iron && state.Hinted("ch:ironAsked") && !state.flags.dockFixed && !SpotOpen("deep"));
        Check($"then: mine iron ({tracked?.Id}: {tracked?.Title})", tracked?.Id == "story:iron:mine" && tracked.Title.Contains("0 of 4") && tracked.Ore == "iron");
        caveFloor = 1; LoadScene("cave"); AtLadder(); yield return 3;
        Check($"on floor 1, it says iron is further down ({guideWay?.Place}: {guideWay?.How})", guideWay?.Place == "Way down" && guideWay.How.Contains("floor 3"));
        caveFloor = 3; LoadScene("cave"); AtLadder(); yield return 3;
        Check($"on floor 3, it points at iron ({guideWay?.Place})", guideWay?.Place == "Iron ore");
        LoadScene("world"); player.X = 160; player.Y = 115;
        state.inv["iron_ore"] = 4; yield return 2;
        Check($"with the ore: smelt it at your furnace ({tracked?.Id})", tracked?.Id == "story:iron:smelt");
        var ibar = Items.Recipes.First(r => r.Out == "iron_bar");
        Craft(ibar); Craft(ibar); yield return 2;
        Check($"with the bars: bring them to Tomas ({tracked?.Id})", tracked?.Id == "story:dock" && tracked.Ready && guideWay?.Place == "Tomas" && ShopStock().Any(s => s.id == "iron_bar"));
        foreach (int f in AtTomasTalk()) yield return f;
        foreach (int f in Talk()) yield return f;
        Check($"handing them over mends the dock ({Has("iron_bar")} bars left)", state.flags.dockFixed && Has("iron_bar") == 0 && SpotOpen("deep") && tracked?.Id == "story:mirror_ray");

        /* ---------- Mara's recording ---------- */
        state.caught.Add("mirror_ray"); SetNight(true); yield return 3;
        Check($"after the Mirror Ray: play Tomas the recording ({tracked?.Id})", tracked?.Id == "story:recording" && !Eligible(Data.ById["abyssal"]));
        TalkTomasWithRequests(); yield return 2;
        foreach (int f in Talk()) yield return f;
        Check($"then the Abyssal rises at night ({tracked?.Id})", RecordingHeard && Eligible(Data.ById["abyssal"]) && tracked?.Id == "story:abyssal");
        SetNight(false);

        /* ---------- The Fesh-dex ---------- */
        var ch = Chapters();
        Check($"the Fesh-dex counts nine chapters, eight done here ({ch.Count(c => c.done)})", ch.Length == 9 && ch.Count(c => c.done) == 8);
        dexTab = "creatures"; dexFish = null; dexExtra = null;
        Inp.Tap(KeyboardKey.J); yield return 6;
        Check($"the creatures page with its chapters fits the screen ({lastCreaturesBottom:0})", mode == "panel" && panel == "dex" && lastCreaturesBottom > 400 && lastCreaturesBottom <= Gfx.LH - 4);
        pendingShot = "chapter-08-dex"; yield return 2;
        ClickButton("dex:journal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("its Open journal button opens the journal", mode == "panel" && panel == "journal");
        ClosePanels(); yield return 2;
        Settings.Data.clearFont = true; TogglePanel("dex"); yield return 4;
        Check($"and with the Clear font ({lastCreaturesBottom:0})", lastCreaturesBottom > 400 && lastCreaturesBottom <= Gfx.LH - 4);
        pendingShot = "chapter-08b-dex-clear"; yield return 2;
        ClosePanels(); Settings.Data.clearFont = false; yield return 2;
        Check($"the HUD's button just says Fesh-dex ({string.Join(", ", Gfx.Seen.Keys.Where(k => k.StartsWith("Fesh-dex")))})", Gfx.Seen.ContainsKey("Fesh-dex") && !Gfx.Seen.Keys.Any(k => k.StartsWith("Fesh-dex ")));

        /* ---------- Getting started ---------- */
        var basics = Basics();
        Check($"Getting started has three pages ({string.Join("/", Enumerable.Range(0, 3).Select(p => basics.Count(b => b.page == p)))})",
            Enumerable.Range(0, 3).All(p => basics.Count(b => b.page == p) is >= 5 and <= 9));
        float rowW = 1120 - 24 - 726 - 24 - 30;
        var tooLong = basics.Where(b => Gfx.Measure(Bind.Fix(b.hint), FontKind.Ui500, 13) > rowW || Gfx.Measure(b.title, FontKind.Ui600, 16) > rowW).Select(b => b.title).ToList();
        Check($"every row and hint fits without being cut ({string.Join(", ", tooLong)})", tooLong.Count == 0);
        Check($"the tools page is ticked off by what you've done ({string.Join(", ", basics.Where(b => b.page == 1 && !b.done).Select(b => b.title))})",
            basics.Where(b => b.page == 1 && b.title is not ("Chop a tree")).All(b => b.done));
        TogglePanel("journal"); journalSide = "basics"; yield return 3;
        int page0 = basicsPage;
        ClickButton("basics:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the > button turns the page ({page0} -> {basicsPage})", basicsPage == Math.Min(page0 + 1, 2) && lastJournalBottom <= (Gfx.LH + 650) / 2 - 20);
        pendingShot = "chapter-09-basics"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- Pip's stall ---------- */
        state.coins = 500; state.hinted.Remove("tut:smelted:copper_bar"); state.hinted.Remove("tut:smelted:iron_bar");
        player.X = PipX; player.Y = PipY + 15; player.Face = "up"; yield return 3;
        OpenShop(); shopTab = "buy"; yield return 4;
        Check($"before you've smelted any, Pip's stall has no bars, and says why ({lastShopBottom:0})", !ShopStock().Any(s => s.id is "copper_bar" or "iron_bar") && lastShopBottom <= (Gfx.LH + 680) / 2 - 20);
        pendingShot = "chapter-10-stall"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- Saves from before 1.22 ---------- */
        state = new State { created = true, flags = new Flags { metTomas = true, tideOut = true }, look = new Look { name = "Old keycard" } };
        state.caught.AddRange(new[] { "glowgill", "tidecrawler", "hollow_eel" }); state.commons["pond_perch"] = 3;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false);
        foreach (int f in Talk()) yield return f;
        Check("an older save keeps Pip's bars", state.Hinted("pipBars") && ShopStock().Any(s => s.id == "iron_bar"));
        Check("its passed chapters count as done (no rocks, the hatch open)", RocksCleared && HatchOpen && CanStand(RockfallX, RockfallY + 2));
        foreach (int f in AtTomasTalk()) yield return f;
        bool asksIron = dlg?.Lines.Any(l => l.T.Contains("two iron bars")) == true;
        foreach (int f in Talk()) yield return f;
        Check($"at the key card, Tomas asks for iron and says Pip has some ({tracked?.Text})", asksIron && !state.flags.dockFixed && tracked?.Text.Contains("Pip sells iron bars") == true);
        state = new State { created = true, flags = new Flags { metTomas = true, tideOut = true, dockFixed = true, ended = true }, look = new Look { name = "Old ending" } };
        state.caught = Data.Creatures.Select(c => c.Id).ToList();
        mode = "play"; StartGame(false); yield return 3;
        Check("a finished story has every chapter done", Chapters().All(c => c.done) && !goals.Any(g => g.Group == "story"));
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Old glow" } };
        state.caught.Add("glowgill"); state.commons["pond_perch"] = 1; state.inv["axe"] = 1; state.inv["copper_pickaxe"] = 1;
        mode = "play"; StartGame(false); yield return 3;
        Check($"an older save with tools already goes straight to the rocks ({tracked?.Id})", tracked?.Id == "story:rocks");

        /* ---------- The Clear font ---------- */
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Reader" } };
        mode = "play"; StartGame(false); ClearSkies(); yield return 3;
        OpenPause(); menuTab = "settings"; yield return 4;
        ClickButton("Clear (easier to read)"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Settings' Font: Clear by a real click", Settings.Data.clearFont && Gfx.Clear);
        pendingShot = "chapter-11-clear-settings"; yield return 2;
        foreach (var k in new[] { FontKind.Ui500, FontKind.Ui600, FontKind.Ui700, FontKind.Note })
        {
            var (scale, dy) = Gfx.ClearFit(k);
            Note($"Clear {k}: size x{scale:0.00}, moved {dy:0.000} of a size");
        }
        const string sample = "Sell all ordinary fish. Daang Pulo's islets, Frostfang Caverns and the Habagat sea.";
        float widest = 0;
        foreach (var k in new[] { FontKind.Ui500, FontKind.Ui600, FontKind.Ui700, FontKind.Note })
        {
            Settings.Data.clearFont = false; float pw = Gfx.Measure(sample, k, 18);
            Settings.Data.clearFont = true; float cw = Gfx.Measure(sample, k, 18);
            widest = Math.Max(widest, cw / pw);
        }
        float names = 0;
        foreach (var d in Items.ById.Values)
        {
            Settings.Data.clearFont = false; float pw = Gfx.Measure(d.Name, FontKind.Ui700, 19);
            Settings.Data.clearFont = true; float cw = Gfx.Measure(d.Name, FontKind.Ui700, 19);
            names = Math.Max(names, cw / pw);
        }
        Check($"Clear text is about as wide as the pixel font's, so layouts hold (a line x{widest:0.00}, item names x{names:0.00} at most)", widest <= 1.08f && names <= 1.15f);
        ClosePause(); yield return 3;
        TogglePanel("journal"); yield return 4;
        pendingShot = "chapter-12-clear-journal"; yield return 2;
        ClosePanels(); dexTab = "log"; logPage = 0; TogglePanel("dex"); yield return 4;
        pendingShot = "chapter-13-clear-dex"; yield return 2;
        ClosePanels(); state.inv["pond_perch"] = 2; state.inv["axe"] = 1; TogglePanel("bag"); yield return 4;
        pendingShot = "chapter-14-clear-bag"; yield return 2;
        ClosePanels(); yield return 2;
        pendingShot = "chapter-15-clear-hud"; yield return 2;
        OpenPause(); menuTab = "settings"; yield return 4;
        ClickButton("Pixel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("and back to Pixel", !Settings.Data.clearFont);
        Settings.Load();
        Check("the choice is saved with the settings", !Settings.Data.clearFont);
        Settings.Data.pauseUnfocused = false; Settings.Data.dayLength = 0;
        ClosePause(); yield return 3;
        foreach (int f in ChapterReviewScript()) yield return f;
    }

    // A check for each finding of Codex's review of the chapters, each failing before its fix.
    IEnumerable<int> ChapterReviewScript()
    {
        Note("The chapters: Codex's review");
        Inp.ScriptMouse = Offscreen;
        IEnumerable<int> Talk()
        {
            for (int i = 0; i < 200 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
            for (int i = 0; i < 90 && mode == "fade"; i++) yield return 2;
            yield return 2;
        }

        // A real new game is stamped by StartGame(true) itself (the first check stamped its own state).
        newSlot = 3;
        StartGame(true, new Look { name = "Brand new" });
        foreach (int f in Talk()) yield return f;
        Check("a new game made the real way is stamped, without Pip's old bars", state.Hinted("chapters") && !state.Hinted("pipBars") && !ShopStock().Any(s => s.id == "iron_bar"));
        quietWildlife = true; standStill = true; ClearSkies(); SetNight(false);

        // The guide agrees with what each gate really needs: a pickaxe for the rocks (no axe), a copper pickaxe for the
        // hatch (no home needed if you already have one).
        state.flags.metTomas = true; state.commons["pond_perch"] = 1; state.caught.Add("glowgill"); state.hinted["ch:toolsAsked"] = true;
        state.inv["pickaxe"] = 1; yield return 3;
        Check($"with a pickaxe but no axe, the guide sends you to the rocks, which is all they need ({tracked?.Id})", tracked?.Id == "story:rocks");
        state.caught.Add("tidecrawler"); state.flags.tideOut = true; state.hinted["ch:hatchSeen"] = true; state.hinted["ch:homeAsked"] = true;
        state.inv["copper_pickaxe"] = 1; yield return 3;
        Check($"with a copper pickaxe and no home, it goes straight to the hatch ({tracked?.Id})", tracked?.Id == "story:hatch");
        // ...and Tomas says the same (Codex's second review: his nudge still asked for a roof first).
        TalkTomasWithRequests(); yield return 2;
        string nudge = dlg?.Lines.FirstOrDefault()?.T ?? "";
        foreach (int f in Talk()) yield return f;
        Check($"so does Tomas ({nudge})", nudge.Contains("copper pick will shift"));
        state.inv.Remove("copper_pickaxe");

        // Bars Pip doesn't stock: with the ore already in your bag and no furnace, the way on is a furnace, not more mining.
        state.inv["iron_ore"] = 4;
        var g = NewGoal("t", "request", "", "");
        string how = Source(g, "iron_bar");
        Check($"with ore and no furnace, a request for bars asks for a furnace ({how}; ore goal {g.Ore})", g.Ore == null && how.Contains("furnace"));
        state.inv.Remove("iron_ore");

        // Copper's deepest floor with every copper rock mined: back up the ladder, not down to a floor with none.
        var copperGoal = NewGoal("t2", "story", "", "");
        copperGoal.Scene = "cave"; copperGoal.Ore = "copper"; copperGoal.HasPoint = true;
        caveFloor = Ore("copper").MaxFloor; LoadScene("cave"); AtLadder();
        foreach (var n in nodes.Where(n => n.Kind == "copper")) n.Mined = true;
        var way = OreRoute(copperGoal);
        Check($"copper all mined on its deepest floor: back up the ladder ({way?.Place}: {way?.How})", way?.Place == "Ladder");
        LoadScene("world"); player.X = 160; player.Y = 115; yield return 3;

        // Every Getting started row fits in the Clear font too.
        Settings.Data.clearFont = true;
        float rowW = 1120 - 24 - 726 - 24 - 30;
        var cut = Basics().Where(b => Gfx.Measure(Bind.Fix(b.hint), FontKind.Ui500, 13) > rowW || Gfx.Measure(b.title, FontKind.Ui600, 16) > rowW).Select(b => b.title).ToList();
        Settings.Data.clearFont = false;
        Check($"every Getting started row fits in the Clear font too ({string.Join(", ", cut)})", cut.Count == 0);

        // Saved and loaded halfway: in Tomas's hand-in dialogue nothing has changed hands; during the dock's fade it's all done.
        state = new State { created = true, flags = new Flags { metTomas = true, tideOut = true }, look = new Look { name = "Halfway" } };
        state.hinted["chapters"] = true; state.hinted["ch:ironAsked"] = true;
        state.caught.AddRange(new[] { "glowgill", "tidecrawler", "hollow_eel" }); state.inv["iron_bar"] = 2;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false);
        foreach (int f in Talk()) yield return f;
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        bool talking = mode == "dialogue";
        Save();
        state = SaveFile.Read(); mode = "play"; StartGame(false);
        foreach (int f in Talk()) yield return f;
        Check($"a save in the middle of handing over the bars keeps them, the dock still broken ({Has("iron_bar")})", talking && Has("iron_bar") == 2 && !state.flags.dockFixed);
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        for (int i = 0; i < 200 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
        bool fading = mode == "fade";
        state = SaveFile.Read(); mode = "play"; StartGame(false);
        foreach (int f in Talk()) yield return f;
        Check($"a game closed during the dock's fade wakes with it mended and the bars gone ({fading}, {Has("iron_bar")})", fading && state.flags.dockFixed && Has("iron_bar") == 0 && SpotOpen("deep"));
    }
}
#endif
