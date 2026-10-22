#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The events deck (1.45, FESH_EVENT_TEST=1): the same event for the same day, how often, the day-3 promise, only what's
    // charted and found, each kind by real presses and clicks (the trader's Buy, the bottle and its X, the turtle and Tomas),
    // nothing paid twice across a reload, the luck and the bite speed, the news once, and every place standing clear.
    IEnumerable<int> EventScript()
    {
        Note("The events deck");
        OldCelebrate = false; EventsOff = false; BoardOff = false;
        state = new State { created = true, look = new Look { name = "Newsreader" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        string Sig(DayEvent e) => $"{e.kind}:{e.arg}:{e.x:0}:{e.y:0}:{string.Join(",", e.offers.Select(o => o.item))}";

        /* ---------- the roll ---------- */
        Check($"the same day rolls the same event ({Sig(RollEvent(9))})", Sig(RollEvent(9)) == Sig(RollEvent(9)));
        state.eventsHad = 5;
        var rolls = Enumerable.Range(4, 80).Select(d => { state.day = d; return RollEvent(d); }).ToList();
        int some = rolls.Count(e => e.kind != "none");
        var kinds = rolls.Select(e => e.kind).Where(k => k != "none").Distinct().OrderBy(k => k).ToList();
        Check($"most days have something ({some} of 80), every kind turns up ({string.Join(", ", kinds)})", some >= 35 && some <= 60 && kinds.Count == 5);
        Check("a run only at a charted Saltmere spot, a bottle only on a charted beach, a turtle on Saltmere",
            rolls.All(e => e.kind != "run" || Charted(SpotRegion(Data.SpotById[e.arg])))
            && rolls.Where(e => e.kind is "bottle" or "turtle").All(e => Charted(RegionOf(e.x, e.y)) && TileAt((int)(e.x / T), (int)(e.y / T)) == 's')
            && rolls.Where(e => e.kind == "turtle").All(e => RegionOf(e.x, e.y) == "saltmere"));
        Check("no trader at a board you haven't found", rolls.Where(e => e.kind == "trader").All(e => BoardKnown(e.arg)));
        state.eventsHad = 0;
        var promise = Enumerable.Range(0, 30).Select(i => { var s = BoardSaltSwap(i); return s; }).ToList();
        Check($"a fisher who hasn't had one gets one on day 3 ({promise.Count(p => p)} of 30 names)", promise.All(p => p));
        state.day = 5; state.dayEvent = null; state.eventsHad = 1;

        /* ---------- every place stands clear ---------- */
        bool clear = true;
        var solids = Solids();
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (tx, ty) = TraderSpot(i) ?? (0, 0);
            if (!CanStandBare(tx, ty, solids.Where(b => b != BoardBox(HarbourBoards[i].x, HarbourBoards[i].y)).ToList()) || !CanStand(tx, ty + 8)) { clear = false; Note($"trader at {HarbourBoards[i].id}: {tx},{ty}"); }
        }
        Check("the trader has room beside every harbour board, and you have room in front of it", clear);

        /* ---------- the trader ---------- */
        DayEvent Force(string kind, string arg = null, float x = 0, float y = 0)
        {
            var e = new DayEvent { day = state.day, version = EventVersion, kind = kind, arg = arg, x = x, y = y, announced = true };
            state.dayEvent = e;
            return e;
        }
        var (trX, trY) = TraderSpot(0).Value;
        var trader = Force("trader", "saltmere", trX, trY);
        trader.offers.Add(new Offer { item = "glow_bait", n = 3, price = 30 });
        trader.offers.Add(new Offer { item = "golden_hook", n = 1, price = 150 });
        state.clock = 12 * 60; state.coins = 200;
        player.X = trX; player.Y = trY + 8; player.Face = "up"; yield return 3;
        pendingShot = "e01-trader"; yield return 4;
        Check("the trader stands by the harbour board by day", target?.Type == "event" && target.Id == "trader");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E opens the trader's goods", mode == "panel" && panel == "trader");
        pendingShot = "e02-trader-panel"; yield return 4;
        Check($"the panel fits ({lastTraderBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastTraderBottom > 0 && lastTraderBottom <= panelAt.y + panelAt.h - 12);
        int gb = Has("glow_bait");
        ClickButton("trader:buy:0"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"Buy takes the coins and gives the goods ({state.coins} coins, {Has("glow_bait") - gb} glow bait)", state.coins == 170 && Has("glow_bait") == gb + 3 && trader.offers[0].sold);
        ClickButton("trader:buy:0"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("a sold row can't be bought again", state.coins == 170);
        ClosePanels(); yield return 2;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("after a reload it's still sold, and the hook still on offer", state.dayEvent.offers[0].sold && !state.dayEvent.offers[1].sold && state.coins == 170);
        state.clock = 19 * 60; player.X = trX; player.Y = trY + 8; yield return 3;
        Check("after 18:00 the trader has gone", target?.Type != "event");
        state.clock = 12 * 60; state.weather = "storm"; yield return 3;
        Check("and in a storm the trader isn't out", target?.Type != "event");
        ClearSkies(); SnapWeather();

        /* ---------- the bottle ---------- */
        var beach = BeachTiles("saltmere");
        var (bt, bty) = beach[beach.Count / 2];
        var bottle = Force("bottle", null, bt * T + 5, bty * T + 7);
        player.X = bottle.x; player.Y = bottle.y + 2; player.Face = "up"; yield return 3;
        pendingShot = "e03-bottle"; yield return 4;
        Check("a bottle lies on the beach", target?.Type == "event" && target.Id == "bottle");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("picking it up reads the note and puts an X on the chart (saved at once)", mode == "dialogue" && state.hunt != null && SaveFile.Read(SaveFile.Slot).hunt != null && bottle.stage == 1);
        for (int i = 0; i < 12 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; }
        var marks = new List<string>(); EventChartMarks((l, x, y, g, tip, p) => marks.Add(l));
        Check("the X is on the chart and in the journal", marks.Contains("X") && Goals().Any(g => g.Id == "event:hunt" && g.HasPoint));
        Check("the X is on the same island, standing room", RegionOf(state.hunt.x, state.hunt.y) == "saltmere" && CanStand(state.hunt.x, state.hunt.y));
        // No new bottle washes up while the X is undug, on any day.
        bool noBottle = Enumerable.Range(40, 60).All(d => RollEvent(d).kind != "bottle");
        Check("no new bottle while an X waits", noBottle);
        int c0 = state.coins, ch0 = state.chests; var h = state.hunt;
        int had = Has(h.item);
        player.X = h.x; player.Y = h.y; player.Face = "down"; yield return 3;
        pendingShot = "e04-x"; yield return 4;
        Check("standing on the X, E digs", target?.Type == "event" && target.Id == "dig");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"digging finds the box ({state.coins - c0} coins, {Has(h.item) - had} {h.item})", state.hunt == null && state.coins - c0 == h.coins && Has(h.item) - had == h.n && state.chests == ch0 + 1);
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("after a reload there's nothing to dig twice", state.hunt == null && state.coins == c0 + h.coins);

        /* ---------- the turtle ---------- */
        var sand = BeachTiles("saltmere");
        var (tt, tty) = sand[sand.Count / 3];
        var turtle = Force("turtle", null, tt * T + 5, tty * T + 7);
        player.X = turtle.x; player.Y = turtle.y + 22; player.Face = "up"; yield return 3;
        pendingShot = "e05-turtle"; yield return 4;
        Check("a stranded turtle can be looked at", target?.Type == "event" && target.Id == "turtle");
        int seen0 = state.sightings.GetValueOrDefault("pawikan");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("looking counts it as a sighting and says not to move it", state.sightings.GetValueOrDefault("pawikan") == seen0 + 1 && mode == "dialogue"
            && dlg.Lines.Any(l => l.T.Contains("Don't pick it up or push it back")));
        for (int i = 0; i < 12 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; }
        Check("the journal sends you to Tomas", Goals().Any(g => g.Id == "event:turtle" && g.Title.Contains("Tomas")));
        int c1 = state.coins;
        player.X = tomasX; player.Y = tomasY + 10; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"telling Tomas: he calls the responders and pays ({state.coins - c1} coins)", turtle.stage == 2 && state.coins - c1 == 40 && mode == "dialogue"
            && dlg.Lines.Any(l => l.T.Contains("municipal environment office")));
        for (int i = 0; i < 12 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; }
        Inp.Tap(KeyboardKey.E); yield return 4;
        for (int i = 0; i < 12 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; }
        Check("telling him again pays nothing", state.coins - c1 == 40);
        player.X = turtle.x + 20; player.Y = turtle.y + 24; yield return 3;
        pendingShot = "e06-turtle-help"; yield return 4;

        /* ---------- the run and the stars ---------- */
        Force("run", "lagoon");
        Check("a fish run speeds up only its own spot", FishRunAt("lagoon") && !FishRunAt("rocks"));
        var lag = Data.SpotById["lagoon"];
        player.X = lag.X; player.Y = lag.Y + lag.R - 2; Force("run", "lagoon"); yield return 30;
        pendingShot = "e07-run"; yield return 4;
        Check($"the run shows splashes over the lagoon ({runDrawn})", runDrawn > 0);
        Force("meteor");
        SetNight(true); ClearSkies(); SnapWeather(); yield return 2;
        int luckStars = Luck("lagoon", null);
        Force("none");
        int luckPlain = Luck("lagoon", null);
        Check($"shooting stars give rare luck +1 on a clear night ({luckPlain} -> {luckStars})", luckStars == luckPlain + 1);
        Force("meteor"); EffectsOff = false; yield return 3;
        pendingShot = "e08-meteors"; yield return 6;
        EffectsOff = true;
        state.weather = "rain"; Check("not through cloud", !MeteorNow);
        ClearSkies(); SnapWeather(); SetNight(false);

        /* ---------- Codex's review ---------- */
        var seen = Force("turtle", null, turtle.x, turtle.y); seen.stage = 1;
        state.day++;
        Check("a turtle seen but not reported is still waiting after the dawn", TodayEvent().kind == "turtle" && TodayEvent().stage == 1 && TurtleToReport);
        state.forecast = new() { new WeatherSpell { at = 0, w = "clear" }, new WeatherSpell { at = 600, w = "rain" } };
        bool rainLate = ClearFromAfternoon();
        state.forecast = new() { new WeatherSpell { at = 0, w = "clear" }, new WeatherSpell { at = 300, w = "rain" } };
        bool rainOn = ClearFromAfternoon();
        state.forecast = new() { new WeatherSpell { at = 0, w = "rain" }, new WeatherSpell { at = 400, w = "clear" } };
        bool clearsUp = ClearFromAfternoon();
        Check("shooting stars only when it stays clear from 14:00 (rain at 16:00 or rain still falling at 14:00 rule them out)", !rainLate && !rainOn && clearsUp);
        ClearSkies();
        var hem = new List<Build>();
        foreach (var (dx, dy) in new[] { (-3, 0), (-2, 0), (2, 0), (3, 0), (-3, 1), (-2, 1), (-1, 1), (0, 1), (1, 1), (2, 1), (-3, -1), (2, -1) })
        {
            var b = new Build { id = "fence", x = (int)(HarbourBoards[0].x / T) + dx, y = (int)(HarbourBoards[0].y / T) + dy };
            if (Data.BuildById.ContainsKey("fence")) { hem.Add(b); state.builds.Add(b); }
        }
        Check($"a board built round gets no trader ({hem.Count} fences)", hem.Count == 0 || TraderSpot(0) == null);
        foreach (var b in hem) state.builds.Remove(b);
        state.dayEvent = null; LoadScene("house:tomas"); yield return 2;
        var inside = TodayEvent();
        Check("indoors, today's event waits to be worked out on the outdoor map", inside.kind == "none" && state.dayEvent == null);
        LoadScene("world"); player.X = 160; player.Y = 115; yield return 3;
        Check("and is rolled once you're back out", state.dayEvent?.day == state.day);

        /* ---------- news, the board, a match ---------- */
        var news = Force("run", "lagoon"); news.announced = false; toastTimer = 0; badgeNews = null;
        for (int i = 0; i < 300 && !news.announced; i++) { if (toastTimer > 0) toastTimer = 0; yield return 1; }
        yield return 3;
        Check($"the day's news is told once, in a toast ({mode}, {news.announced}, \"{toastMsg}\")", news.announced && toastMsg.StartsWith("Harbour news"));
        toastTimer = 0; yield return 3;
        Check("and not again", !toastMsg.StartsWith("Harbour news") || toastTimer <= 0);
        OpenBoard(); yield return 3; lastBoardBottom = 0; yield return 2;
        pendingShot = "e09-board-news"; yield return 4;
        Check($"the harbour board shows the news and still fits ({lastBoardBottom:0})", lastBoardBottom <= panelAt.y + panelAt.h - 12);
        ClosePanels(); yield return 2;

        EventsOff = true; BoardOff = true; OldCelebrate = true;
        yield return 2;
    }

    // Day 3 for a fisher with no events yet, under 30 different names (salts).
    bool BoardSaltSwap(int i)
    {
        var keep = state.look.name;
        state.look.name = "Fisher " + i;
        var e = RollEvent(3);
        state.look.name = keep;
        return e.kind != "none";
    }
}
#endif
