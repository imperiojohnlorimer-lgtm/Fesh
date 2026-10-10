#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    IEnumerable<int> AmihanScript()
    {
        Note("Amihan: navigation, isolated islands, village, wildlife and fish attacks");
        Inp.ScriptMouse = Offscreen;
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Amihan tester" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true;
        state.loose.RemoveAll(l => l.kind == "worm");
        yield return 3;
        state.reqDone = Items.RequestChain.Length;
        var amihanFish = Data.Spots.Where(s => s.Biome == "amihan").SelectMany(s => Data.Common[s.Id]).Select(f => f.Id).ToHashSet();
        Check("Tomas won't request fish from undiscovered Amihan", Enumerable.Range(0, 300).All(_ => !amihanFish.Contains(NextRequest().item)));
        var dryReach = WadeReach(160, 115);
        var atollWade = WadeReach(AtollJettyX + 4, AtollJettyY + 1);
        Check("Amihan has no walking or wading route from Saltmere or Starfall", !dryReach.Any(p => p.Item1 >= EastStart) && !atollWade.Any(p => p.Item1 >= EastStart));
        foreach (var isle in AmihanIslands)
        {
            var dry = WadeReach(isle.cx * T, (isle.cy + 5) * T);
            Check($"{isle.name} is separated from the other islands by deep sea", !AmihanIslands.Where(i => i.name != isle.name)
                .Any(i => dry.Contains(((int)i.cx, (int)i.cy + 5))));
        }
        state.inv["boat"] = 1;
        player.X = SaltJettyX - 4; player.Y = SaltJettyY + 1;
        yield return 3;
        Check("the existing jetty offers the helm as an alternate action", target?.AltType == "launch");
        Inp.Tap(KeyboardKey.F); yield return 4;
        Check($"F launches a steerable boat ({player.X:0},{player.Y:0})", Aboard && BoatCanStand(player.X, player.Y));
        float bx = player.X;
        Inp.Hold(KeyboardKey.Right, true); yield return 14; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check("movement keys steer the boat through water", Aboard && player.X > bx + 10);
        foreach (string face in new[] { "right", "left", "up", "down" })
        {
            var key = face switch { "left" => KeyboardKey.Left, "right" => KeyboardKey.Right, "up" => KeyboardKey.Up, _ => KeyboardKey.Down };
            Inp.Hold(key, true); yield return 2;
            Inp.Hold(key, false); pendingShot = "83-banca-" + face; yield return 2;
        }
        Save(); float savedX = player.X, savedY = player.Y;
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("saving and loading afloat keeps the helm and position", Aboard && Dist(player.X, player.Y, savedX, savedY) < 2);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc pauses sailing", mode == "pause");
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button returns to sailing", mode == "play" && Aboard);
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check("building is blocked while sailing", mode == "play" && Aboard);
        state.tamed = true; Inp.Tap(KeyboardKey.R); yield return 3;
        Check("mounting cannot leave both transports active", Aboard && !Riding);
        state.tamed = false;
        player.X = state.boatX = 1385; player.Y = state.boatY = 217;
        Inp.Hold(KeyboardKey.Right, true); yield return 90; Inp.Hold(KeyboardKey.Right, false); yield return 3;
        Check($"sailing east discovers Amihan and stops at its landing ({player.X:0},{player.Y:0})", state.Hinted("amihan") && player.X > 1460 && player.X < 1490 && Aboard);
        Check("the hull cannot sail onto land or beyond the map", !BoatCanStand(1595, 207) && !BoatCanStand(COLS * T + 5, 217));
        pendingShot = "84-amihan-arrival"; yield return 2;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("reloading with the hull touching a dock keeps the boat afloat", Aboard && BoatCanStand(player.X, player.Y) && player.X > 1400);
        state.weather = "storm"; SnapWeather();
        bx = player.X;
        Inp.Hold(KeyboardKey.Left, true); yield return 12; Inp.Hold(KeyboardKey.Left, false); yield return 2;
        Check("a storm at sea still allows steering home", Aboard && player.X < bx - 5);
        Inp.Hold(KeyboardKey.Right, true); yield return 20; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 4;
        Check($"the ride key lands safely during a storm ({player.X:0},{player.Y:0})", !Aboard && CanStand(player.X, player.Y));
        // In a storm E is the shelter (a tied-up boat can't hide it), so try the ride key, which boards in fair weather.
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("the boat will not set out again during the storm", !Aboard && mode == "play");
        ClearSkies();
        yield return 3; Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E boards the moored boat again in fair weather", Aboard);
        Inp.Tap(KeyboardKey.R); yield return 3;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("a landed boat and player survive reload", !Aboard && CanStand(player.X, player.Y) && BoatPosition().x > 1400);
        pendingShot = "84-banca-moored"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the saved mooring can be boarded after reload", Aboard);
        Inp.Tap(KeyboardKey.R); yield return 3;
        state.tamed = true; state.riding = true;
        player.X = 1385; player.Y = 395;
        Inp.Hold(KeyboardKey.Right, true); yield return 30; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check("Tidemane can cross the same deep channel", Riding && player.X > 1400 && Swimming);
        yield return 2;
        Check($"swimming on Tidemane over deep water, you can fish the open sea (prompt: {prompt.Text})", target?.Type == "spot" && target.Id is "amihansea" or "opensea");
        state.riding = false; state.tamed = false;

        // Each island is reached by transport, then its spot must be fishable from that island's connected land.
        foreach (var (spot, x, y) in new[] { ("amihanpond", 1595f, 207f), ("karstlagoon", 2045f, 142f), ("bakawanpool", 1895f, 617f), ("bagareef", 2315f, 327f) })
        {
            player.X = x; player.Y = y;
            var positions = Reachable().Select(p => (x: p.Item1 * T + 5f, y: p.Item2 * T + 7f));
            var s = Data.SpotById[spot];
            Check($"{s.Label} is fishable from its island's reachable dry land", positions.Any(p => CanStand(p.x, p.y) && Dist(p.x, p.y, s.X, s.Y) < s.R - 4));
        }
        Check("the region has eighteen catchable species registered in the bag (asohos joined in 1.15)", Data.Spots.Where(s => s.Biome == "amihan").Sum(s => Data.Common[s.Id].Length) + Data.PotCatch["amihan"].Length == 18
            && Data.Common.Where(k => Data.SpotById[k.Key].Biome == "amihan").SelectMany(k => k.Value).All(f => Items.ById.ContainsKey(f.Id)));
        Check("carabao, tarsiers and hornbills spawn on safe ground", new[] { "carabao", "tarsier", "hornbill" }.All(k => animals.Any(a => a.Kind == k && CanStand(a.X, a.Y))));
        player.X = 1595; player.Y = 198; yield return 3;
        Check("Lira is reached through the normal interaction target", target?.Type == "islander" && target.Id == "lira");
        Inp.Tap(KeyboardKey.E); yield return 6;
        pendingShot = "85-amihan-village"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Lira gives a meal", Has("fish_stew") == 1);
        TalkIslander("lira"); while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Lira's meal is limited to once per dawn-day", Has("fish_stew") == 1);
        state.inv["bangus"] = 2; int coinsBefore = state.coins;
        player.X = 1715; player.Y = 200; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Niko trades two bangus for coins and bait", Has("bangus") == 0 && state.coins == coinsBefore + 90 && Has("cut_bait") == 5);
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("village request and meal progress persist", state.Hinted("niko_request") && state.gifts.GetValueOrDefault("lira_meal") == state.day);

        Inp.Tap(KeyboardKey.Tab); yield return 4;
        Check("the map opens on the new chart", mode == "panel" && panel == "map" && chartEast);
        pendingShot = "86-amihan-chart"; yield return 2;
        bool clicked = ClickButton("Saltmere"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real chart tab switches back to Saltmere", clicked && !chartEast && mode == "panel");
        ClickButton("Amihan"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the chart tab switches safely back to Amihan", chartEast);
        ClickButton("Close"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        TogglePanel("dex"); dexTab = "log"; yield return 4;
        clicked = ClickButton("Amihan"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the Fish log has a clickable Amihan page", clicked && logPage == 5 && mode == "panel");
        pendingShot = "87-amihan-fish-log"; yield return 2;
        ClosePanels();

        player.X = 2315; player.Y = 327; state.hp = 100;
        TestBite("bagareef", "amihan_barracuda"); Hook();
        reel.Progress = .8f; reel.AttackTimer = 0;
        yield return 3;
        Check("fierce fish telegraph their attack", reel?.AttackWarning > 0);
        pendingShot = "88-fish-attack"; yield return 2;
        float before = state.hp;
        Inp.Tap(KeyboardKey.Escape); yield return 3; // keyboard pause isn't available while reeling; Start is.
        OpenPause(anyMode: true); yield return 3;
        float warning = reel.AttackWarning; yield return 10;
        Check("pausing freezes an attack's warning", mode == "pause" && reel.AttackWarning == warning);
        ClosePause(); yield return 85;
        Check("releasing the reel dodges without health loss", state.hp == before);
        if (reel == null) { TestBite("bagareef", "amihan_barracuda"); Hook(); }
        reel.Progress = .8f; reel.AttackTimer = 0;
        Inp.Hold(KeyboardKey.E, true); yield return 82; Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"holding through an attack loses health ({state.hp:0}) and retains the hooked fish", state.hp < before && mode == "reeling" && reel != null);
        state.hp = 1; reel.AttackTimer = 0;
        Inp.Hold(KeyboardKey.E, true); yield return 150; Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check("a lethal fish attack recovers in Amihan Village", mode == "play" && state.hp >= 35 && Dist(player.X, player.Y, 1595, 197) < 2 && fish == null && reel == null);
        Check("rescue retrieves the boat at a reachable landing", BoatCanStand(BoatPosition().x, BoatPosition().y));
        pendingShot = "89-amihan-recovery"; yield return 2;
        // Old saves omit every new field and retain the original jetty defaults.
        SaveFile.Clear(3);
        File.WriteAllText(SaveFile.SlotPath(3), "{\"created\":true,\"boatAt\":\"atoll\",\"inv\":{\"boat\":1,\"rod_old\":1}}");
        var legacy = SaveFile.Read(3);
        Check("older saves default to a moored boat", legacy != null && !legacy.aboard && legacy.boatX == 0 && legacy.boatAt == "atoll");
        SaveFile.Clear(3);

        Note("Claude review: nearby activities, Starfall discovery, shore saves and attack penalties");
        ClearSkies(); state.aboard = false; state.riding = state.tamed = false;
        state.flags.dockFixed = true; state.boatAt = "saltmere"; state.boatX = state.boatY = 0;
        state.inv["boat"] = 1; BuildMap();
        player.X = 293; player.Y = 98; player.Face = "up"; yield return 3;
        Check("a boat at Pip's jetty does not hide the story's deep-water spot", target?.Type == "spot" && target.Id == "deep");

        player.X = 2315; player.Y = 327; player.Face = "up";
        state.boatX = 2315; state.boatY = 315; state.inv["chum"] = 1;
        yield return 3;
        Check("mooring at Baga leaves fishing and chum available", target?.Type == "spot" && target.Id == "bagareef" && target.AltType == "chum");
        Check($"the prompt says the ride key boards the boat (prompt: {prompt.Text})", prompt.Text.Contains("R] Board your boat"));
        pendingShot = "90-moored-prompt"; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("the ride key boards a nearby boat while other activities keep E", Aboard);
        state.aboard = false; player.X = 1495; player.Y = 217; state.boatX = 1483; state.boatY = 217;
        state.weather = "storm"; SnapWeather(); yield return 3;
        Check("a moored boat cannot hide the storm shelter action", target?.Type == "rest" && target.Id == "shelter");
        state.tamed = true; state.mountX = player.X + 60; state.mountY = player.Y;
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("in a storm, the ride key beside the tied-up boat still calls Tidemane", call != null && !Aboard);
        for (int i = 0; i < 300 && call != null; i++) yield return 1;
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("and once it's there, rides it rather than boarding", Riding && !Aboard);
        state.riding = state.tamed = false;

        ClearSkies(); state.hinted.Remove("visitedAtoll"); state.aboard = true;
        player.X = state.boatX = AtollJettyX - 30; player.Y = state.boatY = AtollJettyY;
        Inp.Hold(KeyboardKey.Right, true); yield return 25; Inp.Hold(KeyboardKey.Right, false); yield return 3;
        Inp.Tap(KeyboardKey.R); yield return 4;
        Check("landing manually at Starfall unlocks its chart, clues and requests", !Aboard && PlayerBiome() == 4 && state.Hinted("visitedAtoll"));

        // Find a valid shoreline position whose rounded feet would fall into the next water tile.
        (float x, float y)? shore = null;
        for (int tx = EastStart + 1; tx < COLS - 1 && shore == null; tx++)
            for (int ty = 1; ty < ROWS - 1 && shore == null; ty++)
            {
                float x = tx * T + 5, y = ty * T + 9.75f;
                if (Walkable(TileAt(tx, ty)) && CanStand(x, y) && !CanStand(x, MathF.Round(y))) shore = (x, y);
            }
        Check("the shoreline regression starts at a valid fractional position", shore != null);
        if (shore is (float sx, float sy))
        {
            state.aboard = false; player.X = sx; player.Y = sy; Save();
            state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
            Check("saving against the shore reloads on the same island and position", InAmihan && Dist(player.X, player.Y, sx, sy) < .01f && CanStand(player.X, player.Y));
        }

        player.X = 2315; player.Y = 327; state.hp = 100;
        TestBite("bagareef", "amihan_barracuda"); Hook();
        reel.Progress = .03f; reel.AttackWarning = .05f; reel.DuckTime = 0;
        // Watch every frame, so the meter is read straight after the hit and before the reel moves it again.
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 30 && state.hp >= 100; i++) yield return 0;
        float afterHit = reel?.Progress ?? 0;
        Inp.Hold(KeyboardKey.E, false);
        Check($"a fish attack cannot refill a nearly empty catch meter ({afterHit:0.000})", state.hp < 100 && afterHit <= .03f);
        if (reel != null) GotAway("It got away.");
        yield return 3;
        foreach (int frames in OpenSeaScript()) yield return frames;
    }

    IEnumerable<int> OpenSeaScript()
    {
        Note("The open sea: fishing from the boat over deep water, feeding frenzies, and being towed home");
        ClearSkies(); SetNight(false); state.riding = state.tamed = false; state.hp = 100;
        schools.Clear(); state.inv["boat"] = 1;
        // Deep water west of Amihan, between the atoll and the archipelago.
        state.aboard = true; player.X = state.boatX = 1385; player.Y = state.boatY = 395; player.Face = "right";
        yield return 3;
        Check($"at the helm over deep water, E fishes the open sea (prompt: {prompt.Text})", target?.Type == "spot" && target.Id == "opensea" && Aboard);
        Check("a long way from shore there is no landing offered", target?.RideLabel == null);
        Inp.Hold(KeyboardKey.Left, true); yield return 20; Inp.Hold(KeyboardKey.Left, false);
        Check($"the sail is up while you steer ({sailFurl:0.00})", sailFurl < 0.1f);
        yield return 2;
        Inp.Hold(KeyboardKey.E, true); yield return 30; Inp.Hold(KeyboardKey.E, false);
        for (int i = 0; i < 90 && mode is "charging" or "casting"; i++) yield return 1;
        Check($"a cast from the boat lands in the water ({mode}, {fish?.Tx:0},{fish?.Ty:0})", mode is "waiting" or "bite" && fish?.Spot == "opensea" && IsWater(fish.Tx, fish.Ty) && Aboard);
        yield return 40;
        Check($"the sail comes down while you fish ({sailFurl:0.00})", sailFurl > 0.9f);
        pendingShot = "91-boat-fishing"; yield return 2;
        TestBite("opensea", "mahi_mahi"); yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("a bite at sea starts the fight", mode == "reeling" && reel != null);
        LandCatch(); yield return 4;
        Check($"an open-sea fish lands in the bag, held up in the boat ({mode})", Has("mahi_mahi") >= 1 && heldItem == "mahi_mahi" && heldT > 0);
        pendingShot = "93-boat-catch"; yield return 2;
        while (mode is "catch" or "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check("after the catch you're still at the helm", mode == "play" && Aboard);
        var westAllowed = Data.Common["opensea"].Select(f => f.Id).Append("chest").ToHashSet();
        Check("the open sea only gives its own catches", Enumerable.Range(0, 400).All(_ => westAllowed.Contains(RollCatch("opensea").Id)));

        // A feeding frenzy close by: the cast goes to it, and it bites quicker.
        schools.Add(new School { X = player.X + 44, Y = player.Y - 4, Age = 5, Life = 60, Seed = 7 });
        yield return 3;
        Check($"a feeding frenzy in reach draws the cast (prompt: {prompt.Text})", target?.Label.Contains("feeding frenzy") == true && Dist(SpotPos(Data.SpotById["opensea"]).x, SpotPos(Data.SpotById["opensea"]).y, player.X + 44, player.Y - 4) < 1);
        pendingShot = "92-feeding-frenzy"; yield return 2;
        float slow = 0, fast = 0;
        for (int i = 0; i < 40; i++)
        {
            Cast("opensea", 0.7f); fast += fish.Timer; fish = null; mode = "play";
            schools.Clear(); seaSpot = (player.X + 30, player.Y - 6);
            Cast("opensea", 0.7f); slow += fish.Timer; fish = null; mode = "play";
            schools.Add(new School { X = player.X + 44, Y = player.Y - 4, Age = 5, Life = 60, Seed = 7 }); seaSpot = (player.X + 44, player.Y - 4);
        }
        Check($"fish bite quicker in a feeding frenzy ({fast / 40:0.00}s against {slow / 40:0.00}s)", fast < slow * 0.75f);
        schools.Clear();
        state.inv.Remove("bait"); state.inv.Remove("glow_bait");

        // Amihan's own waters have their own fish.
        player.X = state.boatX = 1440; player.Y = state.boatY = 395; yield return 3;
        Check($"over Amihan's deep water the open sea is Amihan's (target {target?.Id})", target?.Id == "amihansea");
        var eastAllowed = Data.Common["amihansea"].Select(f => f.Id).Append("chest").ToHashSet();
        Check("the Amihan Sea only gives its own catches", Enumerable.Range(0, 400).All(_ => eastAllowed.Contains(RollCatch("amihansea").Id)));

        // A swordfish's lunge can knock you out at sea: a passing boat tows you home to Pip's jetty.
        player.X = state.boatX = 1385; player.Y = state.boatY = 395; state.hp = 1; yield return 2;
        TestBite("opensea", "swordfish"); Hook();
        reel.Progress = 0.8f; reel.AttackTimer = 0;
        Inp.Hold(KeyboardKey.E, true); yield return 150; Inp.Hold(KeyboardKey.E, false);
        for (int i = 0; i < 120 && mode == "fade"; i++) yield return 1;
        yield return 2;
        Check($"fainting at sea west of Amihan wakes you on Pip's jetty ({player.X:0},{player.Y:0})", mode == "play" && !Aboard && Dist(player.X, player.Y, SaltJettyX - 4, SaltJettyY + 1) < 2 && state.hp >= 35);
        Check("and your boat is tied up there", state.boatAt == "saltmere" && state.boatX == 0 && BoatCanStand(BoatPosition().x, BoatPosition().y));
        Check("Tomas only asks for sea fish once you can get out there", HasSeaRequestsGated());
        foreach (int frames in BoatUpgradesScript()) yield return frames;
        foreach (int frames in FolkScript()) yield return frames;
        foreach (int frames in ChartDexScript()) yield return frames;
        foreach (int frames in HabagatScript()) yield return frames;
    }

    // The chart (opening where you are, charting, hover details, the pin) and the Fish log (cards, seen fish, biting
    // now, filters, a finished page's reward).
    IEnumerable<int> ChartDexScript()
    {
        Note("The chart and the Fish log");
        state.aboard = state.riding = false; ClearSkies(); SetNight(false); Inp.ScriptMouse = Offscreen;
        // Charting: only where you've been.
        state.charted = new() { "saltmere" }; mapTexDirty = true;
        player.X = 595; player.Y = 120; player.Face = "down"; yield return 3;
        Check($"setting foot on Frostfang charts it ({string.Join(", ", state.charted)})", state.charted.Contains("frost") && !Charted("dunes"));
        var img = new Pix(PW, PH);
        img.CopyFrom(worldBase, 0, 0);
        FogUncharted(img);
        var duneLand = Enumerable.Range(0, 400).Select(i => (x: 700 + i % 20 * 5, y: 330 + i / 20 * 5)).First(p => !Wet(shape[p.y * PW + p.x]));
        var fogged = img.Buf[duneLand.y * PW + duneLand.x];
        Check($"uncharted land is a rough outline on the chart ({fogged.R},{fogged.G},{fogged.B})", fogged.R > 180 && fogged.G > 160 && fogged.B > 110 && fogged.B < 180);
        var old = state.charted; state.charted = null;
        state.commons["oasis_tilapia"] = 1;
        var legacy = LegacyCharted();
        Check($"older saves are charted from what you've caught ({string.Join(", ", legacy)})", legacy.Contains("saltmere") && legacy.Contains("dunes"));
        state.commons.Remove("oasis_tilapia"); state.charted = old;
        player.X = 2040; player.Y = 112; yield return 3;
        Check("landing on one of Amihan's islands charts that island", state.charted.Contains("amihan:Luntian Karsts") && !Charted("amihan:Baga Island"));

        // The map opens where you are, with an arrow at the edge from the other chart.
        player.X = 1595; player.Y = 205; yield return 2;
        Inp.Tap(KeyboardKey.Tab); yield return 6;
        Check("in Amihan the map opens on the Amihan chart", mode == "panel" && panel == "map" && chartEast);
        pendingShot = "101-chart-amihan"; yield return 2;
        ClickButton("Saltmere"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "102-chart-offchart"; yield return 2;
        ClosePanels(); yield return 2;
        player.X = 160; player.Y = 115; yield return 2;
        Inp.Tap(KeyboardKey.Tab); yield return 6;
        Check("on Saltmere it opens on the Saltmere chart", mode == "panel" && panel == "map" && !chartEast);
        // Hovering a dot: what's there.
        if (Gfx.Seen.TryGetValue("dot:The lagoon", out var lagoon))
        {
            Inp.ScriptMouse = new System.Numerics.Vector2(lagoon.X + 4, lagoon.Y + 4); yield return 3;
            Check($"hovering a spot shows what's there ({mapHover})", mapHover == "The lagoon");
            pendingShot = "103-chart-tip"; yield return 2;
        }
        else Check("the lagoon has a dot on the chart", false);
        // A click on the chart drops a pin.
        var chartRect = Gfx.Seen["chart"];
        Inp.ScriptMouse = new System.Numerics.Vector2(chartRect.X + chartRect.Width * 0.6f, chartRect.Y + chartRect.Height * 0.7f);
        Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking the chart drops a pin ({state.pinX:0},{state.pinY:0})", HasPin && state.pinX > 700 && state.pinX < 1000 && mode == "panel");
        pendingShot = "104-chart-pin"; yield return 2;
        ClosePanels(); yield return 3;
        pendingShot = "105-pin-compass"; yield return 2;
        player.X = state.pinX; player.Y = state.pinY; yield return 3;
        Check($"reaching the pin takes it away ({toastMsg})", !HasPin && toastMsg.Contains("pin"));

        // The Fish log: a fish that gets away mid-fight is seen.
        state.commons.Remove("pond_perch"); state.seen.Clear();
        player.X = 100; player.Y = 100;
        TestBite("lagoon", "pond_perch"); Hook(); yield return 2;
        GotAway("It got away."); yield return 2;
        Check("one that gets away mid-fight is seen", Seen("pond_perch"));
        TestBite("lagoon", "mud_carp"); fish.BiteT = 0.01f; yield return 4;
        Check("one that's never hooked isn't", !Seen("mud_carp") && !state.seen.Contains("mud_carp"));
        // Biting now follows the time, weather and moon.
        var pike = Data.FishById["crystal_pike"];
        SetNight(false); bool day = BitingNow(pike); SetNight(true); bool night = BitingNow(pike); SetNight(false);
        Check("the night-only pike is biting at night and not by day", !day && night);
        // Filters.
        state.commons["arctic_char"] = 1;
        var missing = LogGroups(1, "missing").SelectMany(g => g.fish).ToList();
        var rare = LogGroups(1, "rare").SelectMany(g => g.fish).ToList();
        var now = LogGroups(1, "now").SelectMany(g => g.fish).ToList();
        Check("the filters show what isn't caught, what's rare, and what's biting now",
            missing.All(f => state.commons.GetValueOrDefault(f.Id) == 0) && !missing.Any(f => f.Id == "arctic_char")
            && rare.All(f => f.Rare || f.Legend) && rare.Count > 0 && now.All(BitingNow) && now.Count < PageFish(1).Count);
        // The card, opened by clicking a row, and Esc back to the page.
        TogglePanel("dex"); dexTab = "log"; logPage = 0; dexFilter = "all"; yield return 4;
        bool clicked = ClickButton("fish:pond_perch"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("clicking a row opens that fish's card", clicked && dexFish == "pond_perch" && mode == "panel");
        pendingShot = "106-dex-card-seen"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc goes back to the page, not out of the log", dexFish == null && mode == "panel" && panel == "dex");
        state.commons["pond_perch"] = 3; state.records["pond_perch"] = 0.52f;
        dexFish = "pond_perch"; yield return 3;
        pendingShot = "107-dex-card"; yield return 2;
        dexFish = "ironbill"; state.commons["ironbill"] = 1; yield return 3;
        pendingShot = "108-dex-card-legend"; yield return 2;
        dexFish = null; dexFilter = "now"; yield return 3;
        pendingShot = "109-dex-biting-now"; yield return 2;
        dexFilter = "all"; ClosePanels(); yield return 2;
        // Catching the last fish on a page pays out once.
        var amihanPage = Array.FindIndex(Data.Biomes, b => b.Id == "amihan");
        var pageFish = PageFish(amihanPage);
        foreach (var f in pageFish.Skip(1)) state.commons[f.Id] = 1;
        state.commons.Remove(pageFish[0].Id); state.hinted.Remove("dexdone:amihan");
        int coins = state.coins;
        AddCatch(pageFish[0]);
        Check($"catching every fish on an island's page pays {PageReward} coins ({state.coins - coins})", state.coins == coins + PageReward && state.Hinted("dexdone:amihan"));
        AddCatch(pageFish[0]);
        Check("and only once", state.coins == coins + PageReward);
        TogglePanel("dex"); dexTab = "log"; logPage = amihanPage; yield return 4;
        pendingShot = "110-dex-page-done"; yield return 2;
        ClosePanels(); yield return 2;
    }

    IEnumerable<int> BoatUpgradesScript()
    {
        Note("Trolling, Ironbill, the big sail and the echo sounder");
        ClearSkies(); SetNight(false); state.hp = 100; schools.Clear();
        state.inv["boat"] = 1; state.inv.Remove("spinner_lure"); state.inv.Remove("fly_lure"); state.inv.Remove("big_sail"); state.inv.Remove("echo_sounder");
        state.aboard = true; player.X = state.boatX = 1385; player.Y = state.boatY = 395; player.Face = "right";
        yield return 3;
        Check($"over deep water the helm offers trolling (prompt: {prompt.Text})", target?.AltType == "troll" && prompt.Text.Contains("Troll a lure"));
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check($"trolling needs a lure ({toastMsg})", !trolling && toastMsg.Contains("lure"));
        state.inv["spinner_lure"] = 1;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check($"F lets out a trolling line (prompt: {prompt.Text})", trolling && target?.Type == "trolling");
        Check($"trolling is slow ({BoatSpeed:0})", BoatSpeed < 70);
        trollT = 99;
        Inp.Hold(KeyboardKey.Left, true); yield return 40;
        Check($"the lure trails behind the stern ({lure.x:0} vs the boat at {player.X:0})", lure.x > player.X + 15);
        pendingShot = "94-trolling"; yield return 2;
        trollT = 0.01f; yield return 3;
        Inp.Hold(KeyboardKey.Left, false); yield return 1;
        var struck = fish?.Roll;
        Check($"something that chases strikes the moving lure ({struck?.Id})", mode == "bite" && struck != null && Data.FishById[struck.Id].Style is "runner" or "jumper" && !trolling);
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the strike becomes a normal fight", mode == "reeling" && reel != null);
        if (reel != null) GotAway("It got away.");
        yield return 3;
        Check("trolled catches only come from fish that chase, and never the legend outside a frenzy",
            Enumerable.Range(0, 400).Select(_ => RollTroll("opensea", false).Id).All(id => Data.FishById[id].Style is "runner" or "jumper" && id != "ironbill"));
        int legends = Enumerable.Range(0, 3000).Count(_ => RollTroll("opensea", true).Id == "ironbill");
        Check($"in a feeding frenzy by day, Ironbill can take a trolled lure ({legends} of 3000)", legends > 0);
        Check("Ironbill never takes an ordinary cast", Enumerable.Range(0, 3000).All(_ => RollCatch("opensea").Id != "ironbill"));

        // Ironbill tows the boat.
        float bx0 = player.X, by0 = player.Y;
        TestBite("opensea", "ironbill"); Hook(); yield return 2;
        Check("Ironbill's fight is a long one", mode == "reeling" && reel?.Pull < 1);
        reel.Running = 3; reel.RunT = 99; reel.Progress = 0.5f;
        yield return 45;
        Check($"Ironbill tows the boat ({Dist(player.X, player.Y, bx0, by0):0} px)", Dist(player.X, player.Y, bx0, by0) > 6 && Aboard && BoatCanStand(player.X, player.Y));
        pendingShot = "95-ironbill-tow"; yield return 2;
        if (reel != null) { LandCatch(); yield return 4; }
        Check("landing Ironbill shows its legend card", mode == "legend" && legendId == "ironbill" && Has("ironbill") == 1);
        pendingShot = "96-ironbill-card"; yield return 2;
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }

        // The big sail.
        float plain = BoatSpeed;
        state.inv["big_sail"] = 1;
        Check($"the big sail is a third faster ({plain:0} to {BoatSpeed:0})", BoatSpeed > plain * 1.25f);
        Inp.Hold(KeyboardKey.Right, true); yield return 12;
        pendingShot = "97-big-sail"; yield return 2;
        Inp.Hold(KeyboardKey.Right, false); yield return 2;

        // The echo sounder: more frenzies, further out, pointed to and charted.
        player.X = state.boatX = 1385; player.Y = state.boatY = 395; schools.Clear();
        for (int i = 0; i < 200; i++) { schoolT = 0; yield return 0; }
        int without = schools.Count;
        state.inv["echo_sounder"] = 1; schools.Clear();
        for (int i = 0; i < 200; i++) { schoolT = 0; yield return 0; }
        Check($"an echo sounder finds more feeding frenzies ({without} without, {schools.Count} with)", without <= 2 && schools.Count == 3);
        schools.Clear();
        schools.Add(new School { X = player.X + 200, Y = player.Y - 30, Age = 5, Life = 60, Seed = 3 });
        yield return 3;
        pendingShot = "98-sonar"; yield return 2;
        Inp.Tap(KeyboardKey.Tab); yield return 6;
        Check("the sea chart opens with the frenzy on it", mode == "panel" && panel == "map");
        pendingShot = "99-sonar-chart"; yield return 2;
        ClosePanels(); yield return 2;
        schools.Clear(); state.inv.Remove("big_sail"); state.inv.Remove("echo_sounder");
    }

    // Tomas, Pip and the villagers stroll about near home, and stop for you.
    IEnumerable<int> FolkScript()
    {
        Note("Tomas, Pip and the villagers stroll about");
        state.aboard = false; state.riding = false; ClearSkies(); SetNight(false);
        tomasX = TomasHomeX; tomasY = TomasHomeY;
        player.X = 160; player.Y = 130; player.Face = "down";
        standStill = false;
        float far = 0, wander = 0;
        bool clear = true;
        for (int i = 0; i < 480; i++)
        {
            yield return 0;
            far = Math.Max(far, Dist(tomasX, tomasY, TomasHomeX, TomasHomeY));
            wander = Math.Max(wander, Math.Abs(pipWalk.X - PipX));
            if (tomasWalk.Moving && !FolkCanStand(tomasX, tomasY, tomas: true)) clear = false;
        }
        Check($"Tomas potters about his camp ({far:0} px at most)", far > 3 && far < 26 && clear);
        Check($"Pip shuffles about behind the counter ({wander:0} px)", wander > 0.5f && wander <= 6.01f);
        pendingShot = "100-tomas-strolling"; yield return 2;
        player.X = tomasX; player.Y = tomasY + 12; player.Face = "up"; yield return 4;
        Check($"he stops and turns to you when you come over ({target?.Label})", !tomasWalk.Moving && target?.Type == "npc");
        state.clock = 23 * 60; yield return 2;
        Check("at bedtime he's back in his hut", TomasInBed && tomasX == TomasHomeX && tomasY == TomasHomeY);
        SetNight(false);
        var lira = IslanderWalk("lira");
        player.X = 1595; player.Y = 260; yield return 2;
        float liraFar = 0;
        for (int i = 0; i < 480; i++) { yield return 0; liraFar = Math.Max(liraFar, Dist(lira.X, lira.Y, lira.HomeX, lira.HomeY)); }
        Check($"the villagers stroll in front of their houses ({liraFar:0} px at most)", liraFar > 3 && liraFar < 16);
        player.X = lira.X; player.Y = lira.Y + 8; yield return 4;
        Check($"and stop to talk ({target?.Label})", target?.Type == "islander" && target.Id == "lira" && !lira.Moving);
        standStill = true; yield return 2;
        Check("pinned for the other checks, everyone is back home", tomasX == TomasHomeX && lira.X == lira.HomeX && pipWalk.X == PipX);
    }

    // With no boat and no Tidemane, none of Tomas's random requests come from the open sea.
    bool HasSeaRequestsGated()
    {
        int boat = state.inv.GetValueOrDefault("boat"); bool tamed = state.tamed; int done = state.reqDone;
        state.inv.Remove("boat"); state.tamed = false; state.reqDone = Items.RequestChain.Length;
        var seaFish = Data.Common["opensea"].Concat(Data.Common["amihansea"]).Select(f => f.Id).ToHashSet();
        bool ok = Enumerable.Range(0, 300).All(_ => !seaFish.Contains(NextRequest().item));
        state.inv["boat"] = boat; state.tamed = tamed; state.reqDone = done;
        return ok;
    }
}
#endif
