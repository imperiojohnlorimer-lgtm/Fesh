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
            player.Face = face; pendingShot = "83-banca-" + face; yield return 2;
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
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"E lands safely during a storm ({player.X:0},{player.Y:0})", !Aboard && CanStand(player.X, player.Y));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the boat will not set out again during the storm", !Aboard);
        ClearSkies();
        yield return 3; Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E boards the moored boat again in fair weather", Aboard);
        Inp.Tap(KeyboardKey.E); yield return 3;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("a landed boat and player survive reload", !Aboard && CanStand(player.X, player.Y) && BoatPosition().x > 1400);
        pendingShot = "84-banca-moored"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the saved mooring can be boarded after reload", Aboard);
        Inp.Tap(KeyboardKey.E); yield return 3;
        state.tamed = true; state.riding = true;
        player.X = 1385; player.Y = 395;
        Inp.Hold(KeyboardKey.Right, true); yield return 30; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check("Tidemane can cross the same deep channel", Riding && player.X > 1400 && Swimming);
        state.riding = false; state.tamed = false;

        // Each island is reached by transport, then its spot must be fishable from that island's connected land.
        foreach (var (spot, x, y) in new[] { ("amihanpond", 1595f, 207f), ("karstlagoon", 2045f, 142f), ("bakawanpool", 1895f, 617f), ("bagareef", 2315f, 327f) })
        {
            player.X = x; player.Y = y;
            var positions = Reachable().Select(p => (x: p.Item1 * T + 5f, y: p.Item2 * T + 7f));
            var s = Data.SpotById[spot];
            Check($"{s.Label} is fishable from its island's reachable dry land", positions.Any(p => CanStand(p.x, p.y) && Dist(p.x, p.y, s.X, s.Y) < s.R - 4));
        }
        Check("the region has thirteen catchable species registered in the bag", Data.Spots.Where(s => s.Biome == "amihan").Sum(s => Data.Common[s.Id].Length) + Data.PotCatch["amihan"].Length == 13
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
    }
}
#endif
