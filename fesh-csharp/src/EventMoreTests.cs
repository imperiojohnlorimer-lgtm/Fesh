#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The whale song, market day and the lost kite (1.45.1, FESH_EVENT2_TEST=1): which days roll them and where, listening in
    // its hours (rare luck +1 until noon), the market's stalls by real E and Buy clicks and its hours, a kite picked up on
    // a beach, kept across the dawn, and given to Ma'am Isay (paid once), and what each says on the board and the chart.
    IEnumerable<int> EventMoreScript()
    {
        Note("More events");
        OldCelebrate = false; EventsOff = false; BoardOff = false; FriendsOff = true;
        state = new State { created = true, look = new Look { name = "Newsreader" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        IEnumerable<int> EndTalk() { for (int i = 0; i < 20 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; } yield return 2; }
        DayEvent Force(string kind, float x, float y, int stage = 0)
        {
            var e = new DayEvent { day = state.day, version = EventVersion, kind = kind, x = x, y = y, stage = stage, announced = true };
            state.dayEvent = e;
            return e;
        }

        /* ---------- the roll ---------- */
        state.eventsHad = 5;
        state.day = 4; state.dayEvent = null;
        state.clock = 7 * 60;
        var before = Enumerable.Range(4, 120).Select(d => { state.day = d; return RollEvent(d); }).Select(e => e.kind).Distinct().ToList();
        Check($"with only Saltmere charted: a whale can sing, but no market and no kite ({string.Join(",", before.OrderBy(k => k))})", before.Contains("whale") && !before.Contains("market") && !before.Contains("kite"));
        state.charted ??= new(); state.charted.Add(VillageRegion); state.hinted["metIsay"] = true; state.hinted["amihan"] = true; state.hinted["rs:tremor"] = true;   // no tremor on the next day
        var rolls = Enumerable.Range(4, 200).Select(d => { state.day = d; return RollEvent(d); }).ToList();
        Check($"once the village is charted and Isay met, market and kite turn up ({rolls.Count(e => e.kind == "market")}, {rolls.Count(e => e.kind == "kite")})", rolls.Any(e => e.kind == "market") && rolls.Any(e => e.kind == "kite"));
        var solids = Solids();
        Check("every market stands where its stalls and seller fit", rolls.Where(e => e.kind == "market").All(e => CanStandBare(e.x, e.y, solids) && e.offers.Count == 3 && e.offers.All(o => Items.ById.ContainsKey(o.item))));
        Check("every kite lies on a village beach, every whale off a charted beach", rolls.Where(e => e.kind == "kite").All(e => RegionOf(e.x, e.y) == VillageRegion && TileAt((int)(e.x / T), (int)(e.y / T)) == 's')
            && rolls.Where(e => e.kind == "whale").All(e => Charted(RegionOf(e.x, e.y)) && TileAt((int)(e.x / T), (int)(e.y / T)) == 's'));
        Check("the same day rolls the same kind again", RollEvent(30).kind == RollEvent(30).kind && RollEvent(30).x == RollEvent(30).x);
        state.day = 5; state.dayEvent = null;

        /* ---------- whale song ---------- */
        // The whale only rolls while its window has an hour left (Codex's review).
        state.hinted["metIsay"] = true; state.charted.Add(VillageRegion);
        state.day = 20; state.clock = 11 * 60; Check("at 11:00 no whale can roll", !EventPossible("whale", 20));
        state.clock = 7 * 60; Check("at 07:00 one can", EventPossible("whale", 20));
        state.clock = 9 * 60 + 30; Check("but not with under an hour left", !EventPossible("whale", 20));
        state.clock = 7 * 60; state.day = 5;
        var beach = BeachTiles("saltmere")[0];
        float wx = beach.tx * T + 5, wy = beach.ty * T + 7;
        var whale = Force("whale", wx, wy);
        state.clock = 7 * 60; player.X = wx; player.Y = wy; player.Face = "up"; yield return 3;
        Check($"at the marked beach in the morning you can listen ({target?.Type}:{target?.Id})", target?.Type == "event" && target.Id == "whale");
        pendingShot = "w01-whale"; yield return 4;
        int luck0 = Luck("lagoon", null);
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E listens: it talks and remembers", mode == "dialogue" && whale.stage == 1);
        foreach (var s in EndTalk()) yield return s;
        Check($"rare luck is up by one until noon ({Luck("lagoon", null) - luck0})", Luck("lagoon", null) == luck0 + 1);
        Check("and it can't be listened to twice", target?.Id != "whale");
        state.clock = 12 * 60 + 5; yield return 2;
        Check("after noon the luck is gone", Luck("lagoon", null) == luck0);
        var whale2 = Force("whale", wx, wy);
        state.clock = 11 * 60; yield return 3;
        Check("after ten o'clock the song has faded: nothing to listen to", target?.Id != "whale");
        state.clock = 5 * 60; yield return 2;
        Check("and before six it hasn't begun", !WhaleOpen);
        state.clock = 8 * 60; Force("whale", wx, wy); ClosePanels();
        Check("the board and the chart tell of it", EventNews(state.dayEvent).Contains("whale") && EventBoardLine().Contains("whale"));

        /* ---------- market day ---------- */
        var spot = MarketSpot();
        Check("the village has room for a market", spot != null);
        if (spot == null) yield break;
        var market = Force("market", spot.Value.x, spot.Value.y);
        market.offers = new() { new Offer { item = "salt", n = 3, price = 15 }, new Offer { item = "suka", n = 2, price = 10 }, new Offer { item = "calamansi", n = 4, price = 16 } };
        state.clock = 10 * 60; state.coins = 100;
        player.X = spot.Value.x; player.Y = spot.Value.y + 14; player.Face = "up"; yield return 3;
        pendingShot = "w02-market"; yield return 4;
        Check($"by day the market is a target ({target?.Type}:{target?.Id})", target?.Type == "event" && target.Id == "market");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E opens the stalls", mode == "panel" && panel == "trader");
        pendingShot = "w03-market-panel"; yield return 4;
        Check($"the panel says it is market day and fits three rows ({lastTraderBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastTraderBottom > 0 && lastTraderBottom <= panelAt.y + panelAt.h - 12);
        int salt0 = Has("salt");
        ClickButton("trader:buy:0"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"Buy pays and gives the goods ({state.coins} coins, {Has("salt") - salt0} salt)", state.coins == 85 && Has("salt") == salt0 + 3 && market.offers[0].sold);
        ClosePanels(); yield return 2;
        state.clock = 17 * 60; yield return 3;
        Check("at five o'clock the market has gone", !MarketHere && target?.Id != "market");
        state.clock = 7 * 60 + 30; yield return 2;
        Check("and it isn't open before eight", !MarketHere);
        state.clock = 10 * 60; state.weather = "storm"; planned = "storm"; yield return 2;
        Check("nor in a storm", !MarketHere);
        ClearSkies(); SnapWeather();

        /* ---------- the kite ---------- */
        var kb = BeachTiles(VillageRegion)[0];
        float kx = kb.tx * T + 5, ky = kb.ty * T + 7;
        var kite = Force("kite", kx, ky);
        state.clock = 12 * 60; player.X = kx; player.Y = ky; player.Face = "up"; yield return 3;
        Check($"the kite lies on the beach ({target?.Type}:{target?.Id})", target?.Type == "event" && target.Id == "kite");
        pendingShot = "w04-kite"; yield return 4;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E picks it up", kite.stage == 1 && KiteHeld);
        foreach (var s in EndTalk()) yield return s;
        state.day++; state.clock = 12 * 60; yield return 3;
        Check("it is still yours the next day", KiteHeld && TodayEvent().kind == "kite" && TodayEvent().stage == 1);
        var isay = IslanderWalk("isay");
        player.X = isay.X; player.Y = isay.Y + 12; player.Face = "up"; yield return 3;
        Check($"Ma'am Isay offers to take it ({target?.Label})", target?.Type == "islander" && target.Label == "Give Ma'am Isay the kite");
        int coins0 = state.coins;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (var s in EndTalk()) yield return s;
        Check($"the kite goes home for 35 coins ({state.coins - coins0})", state.coins - coins0 == 35 && !KiteHeld && state.dayEvent.stage == 2);
        player.X = isay.X; player.Y = isay.Y + 12; yield return 3;
        Check("and her usual lesson is back", target?.Label != "Give Ma'am Isay the kite");
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("a reload doesn't bring the kite back", !KiteHeld && state.dayEvent?.stage != 1);
        EventsOff = true; FriendsOff = false;
    }
}
#endif
