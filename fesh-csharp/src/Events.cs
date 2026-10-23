using Raylib_cs;

namespace Fesh;

// The events deck (1.45): on most days something out of the ordinary happens somewhere you've been. It's rolled once a
// day from the day itself (like the harbour board) and saved with the day, so a reload, a purchase or finding a new island
// never rerolls it. Nothing is required, and nothing in a match. Five kinds so far:
//  run     a fish run: one spot bites twice as fast all day
//  trader  a travelling trader beside a harbour board, 09:00-18:00 (not in a storm), with two things Pip doesn't sell
//  bottle  a message in a bottle on a beach: picking it up reads a note that puts an X on your chart; dig there for a chest
//          (the hunt keeps across days until it's dug, and no new bottle washes up meanwhile)
//  meteor  shooting stars after dark on a clear night, and rare luck +1 while they fall
//  turtle  a pawikan stranded on a Saltmere beach: keep back, keep dogs off, and tell Tomas, who calls the trained
//          responders (the municipal environment office and the Bantay Dagat); they take it from there. Its facts were
//          checked by Codex against Philippine marine turtle response guidance: no handling, no pushing it back to sea.
sealed class DayEvent
{
    public int day, version;
    public string kind = "none", arg;   // arg: the run's spot, the trader's board
    public float x, y;                  // the bottle, the turtle or the trader
    public int stage;                   // bottle: 0 lying, 1 picked up; turtle: 0 stranded, 1 looked at, 2 reported
    public List<Offer> offers = new();
    public bool announced;
}

sealed class Offer { public string item; public int n, price; public bool sold; }

// The X from a bottle's note: kept until it's dug, across days.
sealed class Hunt { public float x, y; public string region; public int coins; public string item; public int n; }

partial class Game
{
    internal static bool EventsOff;   // the old scripts never see events (AutoTestStart); EventScript turns them on
    const int EventVersion = 1;
    static readonly string[] EventKinds = { "run", "trader", "bottle", "meteor", "turtle", "whale", "market", "kite" };

    DayEvent TodayEvent()
    {
        if (state.dayEvent != null && state.dayEvent.day == state.day) return state.dayEvent;
        // A turtle you've seen but not reported yet is still waiting for help after the dawn (Codex): it carries over.
        if (state.dayEvent is { kind: "turtle", stage: 1 } waiting)
        {
            waiting.day = state.day;
            waiting.announced = true;
            return waiting;
        }
        // A kite you are carrying home waits for you across the dawn, too.
        if (state.dayEvent is { kind: "kite", stage: 1 } carrying) { carrying.day = state.day; carrying.announced = true; return carrying; }
        // Places are worked out on the outdoor map: indoors or underground, today has nothing until you're back out.
        if (scene != "world") return new DayEvent { day = state.day, announced = true };
        state.dayEvent = RollEvent(state.day);
        if (state.dayEvent.kind != "none") state.eventsHad++;
        return state.dayEvent;
    }

    bool EventOn(string kind) => !EventsOff && !MatchActive && state?.created == true && TodayEvent().kind == kind;

    DayEvent RollEvent(int day)
    {
        var ev = new DayEvent { day = day, version = EventVersion };
        var rng = new MatchRng(MatchRules.Mix(day, BoardSalt, EventVersion, 53));
        double r = rng.NextDouble();
        // About three days in five, and one by day 3 for a fisher who hasn't had one yet.
        bool due = state.eventsHad == 0 && day >= 3;
        if (r >= 0.6 && !due) return ev;
        var kinds = EventKinds.Where(k => EventPossible(k, day)).ToList();
        if (kinds.Count == 0) return ev;
        string kind = kinds[(int)(rng.NextDouble() * kinds.Count) % kinds.Count];
        ev.kind = kind;
        switch (kind)
        {
            case "run":
            {
                var spots = RunSpots();
                ev.arg = spots[(int)(rng.NextDouble() * spots.Count) % spots.Count].Id;
                break;
            }
            case "trader":
            {
                var boards = Enumerable.Range(0, HarbourBoards.Length).Where(i => BoardKnown(HarbourBoards[i].id) && BoardStands(i) && TraderSpot(i) != null).ToList();
                int b = boards[(int)(rng.NextDouble() * boards.Count) % boards.Count];
                ev.arg = HarbourBoards[b].id;
                (ev.x, ev.y) = TraderSpot(b).Value;
                var pool = new List<Offer> { new() { item = "glow_bait", n = 3, price = 30 }, new() { item = "chum", n = 3, price = 20 } };
                if (Has("golden_hook") == 0) pool.Add(new() { item = "golden_hook", n = 1, price = 150 });
                if (Has("fly_lure") == 0) pool.Add(new() { item = "fly_lure", n = 1, price = 50 });
                if (Has("spinner_lure") == 0) pool.Add(new() { item = "spinner_lure", n = 1, price = 40 });
                while (ev.offers.Count < 2 && pool.Count > 0)
                {
                    int k = (int)(rng.NextDouble() * pool.Count) % pool.Count;
                    ev.offers.Add(pool[k]); pool.RemoveAt(k);
                }
                break;
            }
            case "bottle":
            {
                var beaches = BeachTiles(PlayerIsland());
                var (tx, ty) = beaches[(int)(rng.NextDouble() * beaches.Count) % beaches.Count];
                (ev.x, ev.y) = (tx * T + 5, ty * T + 7);
                break;
            }
            case "whale":
            {
                var beaches = BeachTiles(PlayerIsland());
                var (tx, ty) = beaches[(int)(rng.NextDouble() * beaches.Count) % beaches.Count];
                (ev.x, ev.y) = (tx * T + 5, ty * T + 7);
                break;
            }
            case "market":
                (ev.x, ev.y) = MarketSpot().Value;
                ev.offers = MarketOffers(rng);
                break;
            case "kite":
            {
                var beaches = BeachTiles(VillageRegion);
                var (tx, ty) = beaches[(int)(rng.NextDouble() * beaches.Count) % beaches.Count];
                (ev.x, ev.y) = (tx * T + 5, ty * T + 7);
                break;
            }
            case "turtle":
            {
                var beaches = BeachTiles("saltmere");
                var (tx, ty) = beaches[(int)(rng.NextDouble() * beaches.Count) % beaches.Count];
                (ev.x, ev.y) = (tx * T + 5, ty * T + 7);
                break;
            }
        }
        return ev;
    }

    bool EventPossible(string kind, int day) => kind switch
    {
        "run" => RunSpots().Count > 0,
        "trader" => Enumerable.Range(0, HarbourBoards.Length).Any(i => BoardKnown(HarbourBoards[i].id) && BoardStands(i) && TraderSpot(i) != null),
        "bottle" => state.hunt == null && BeachTiles(PlayerIsland()).Count > 0,
        // Only on a night the forecast keeps clear (the stars must show), and the forecast is only known for today.
        "meteor" => day == state.day && ClearFromAfternoon(),
        "turtle" => BeachTiles("saltmere").Count > 0,
        // Only while the listening window (06:00-10:00) has at least an hour left: not for someone who comes outdoors at 11:00.
        "whale" => day == state.day && state.clock >= 6 * 60 && state.clock < 9 * 60 && !Night && BeachTiles(PlayerIsland()).Count > 0,
        "market" => Charted(VillageRegion) && MarketSpot() != null,
        "kite" => Charted(VillageRegion) && state.Hinted("metIsay") && BeachTiles(VillageRegion).Count > 0,
        _ => false
    };

    // Spots a fish run can come to: open, known, charted, out in the world, with everyday fish (never the Starwell).
    List<Spot> RunSpots() => Data.Spots.Where(s => s.Scene == "world" && s.Id != "starwell" && SpotOpen(s.Id) && SpotKnown(s)
        && Charted(SpotRegion(s)) && Data.Common[s.Id].Any(EverydayFish)).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

    // The island you're on (or Saltmere, indoors or at sea), if it's charted.
    string PlayerIsland()
    {
        if (scene != "world") return "saltmere";
        string r = RegionOf(player.X, player.Y);
        return Charted(r) && !WaterTile(TileAt((int)(player.X / T), (int)((player.Y - 2) / T))) ? r : "saltmere";
    }

    // Sand tiles beside the sea on one island, where nothing stands: for a bottle, a turtle or an X.
    List<(int tx, int ty)> BeachTiles(string region, bool shore = true)
    {
        var list = new List<(int, int)>();
        var solids = Solids();
        for (int ty = 1; ty < ROWS - 1; ty++)
            for (int tx = 1; tx < COLS - 1; tx++)
            {
                char c = TileAt(tx, ty);
                if (c != 's' && (shore || c != 'g')) continue;
                bool sea = TileAt(tx + 1, ty) is '~' or 'w' || TileAt(tx - 1, ty) is '~' or 'w' || TileAt(tx, ty + 1) is '~' or 'w' || TileAt(tx, ty - 1) is '~' or 'w';
                if (shore != sea) continue;
                if (RegionAt(tx, ty) != region) continue;
                float cx = tx * T + 5, cy = ty * T + 7;
                if (InSanctuary(cx, cy) || Restriction(cx, cy) != null || !CanStandBare(cx, cy, solids)) continue;
                if (HarbourBoards.Any(b => Dist(b.x, b.y, cx, cy) < 30)) continue;   // not in front of a harbour board
                list.Add((tx, ty));
            }
        return list;
    }

    // CanStand without the player's own wading or riding, and with the solids fetched once.
    bool CanStandBare(float x, float y, List<Box> solids)
    {
        foreach (var (ax, ay) in new[] { (x - 4, y - 4), (x + 4, y - 4), (x - 4, y + 2), (x + 4, y + 2) })
            if (!Walkable(TileAt((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T)))) return false;
        var me = new Box(x - 6, y - 5, 12, 8);
        return !solids.Any(s => s.Overlaps(me)) && !state.builds.Any(b => BuildBox(b) is Box r && r.Overlaps(me));
    }

    // Today's forecast clear from 14:00 to the dawn: the spell already going at 14:00 and every later one (forecast times
    // are minutes after 06:00, so 14:00 is 480; Codex).
    bool ClearFromAfternoon()
    {
        var f = (state.forecast ?? new()).OrderBy(s => s.at).ToList();
        if (f.Count == 0) return true;
        var at14 = f.LastOrDefault(s => s.at <= 480) ?? f[0];
        return at14.w == "clear" && f.All(s => s.at <= 480 || s.w == "clear");
    }

    // Beside the board, on whichever side has room for the trader and its crate, with room in front to trade from (Saltmere's
    // has Pip's stall to its east); null when a board has none (built round), so no trader comes there.
    (float x, float y)? TraderSpot(int board)
    {
        var (_, _, bx, by) = HarbourBoards[board];
        var solids = Solids().Where(b => b != BoardBox(bx, by)).ToList();
        foreach (float dx in new[] { 22f, -22f, 0f })
        {
            float x = bx + dx, y = by + (dx == 0 ? 16 : 4);
            if (CanStandBare(x, y, solids) && CanStandBare(x - 8, y, solids) && CanStandBare(x, y + 8, solids)) return (x, y);
        }
        return null;
    }

    static Box TraderBox(float x, float y) => new(x - 14, y - 8, 20, 18);

    bool TraderHere => EventOn("trader") && !Night && state.clock >= 9 * 60 && state.clock < 18 * 60 && !Stormy;

    /* ---------- What it says ---------- */
    string EventNews(DayEvent ev) => ev.kind switch
    {
        "run" => $"Fish are running at {SpotName(ev.arg)}: bites come twice as fast there today.",
        "trader" => $"A travelling trader is by the harbour board on {BoardName(ev.arg)} from {HourText(9 * 60)} to {HourText(18 * 60)}.",
        "bottle" => $"Something glints on a beach of {IslandName(RegionOf(ev.x, ev.y))}: a bottle washed up.",
        "meteor" => "Clear skies tonight: watch for shooting stars after dark. Rare fish rise while they fall.",
        "turtle" => "A sea turtle is stranded on a Saltmere beach. Keep back, keep dogs away, and tell Tomas.",
        _ => MoreEventNews(ev)
    };

    string SpotName(string id) => Data.SpotById.TryGetValue(id ?? "", out var s) ? s.Label : "a fishing spot";
    string BoardName(string id) => HarbourBoards.FirstOrDefault(b => b.id == id).name ?? "Saltmere";
    string IslandName(string region) => RegionById.TryGetValue(region ?? "", out var r) ? r.Name : "Saltmere";

    /* ---------- Each frame ---------- */
    void UpdateEvents()
    {
        if (EventsOff || MatchActive || mode != "play" || state == null || !state.created || TourActive) return;
        var ev = TodayEvent();
        // Harbour news, once a day, when nothing else is being said.
        if (!ev.announced && ev.kind != "none" && toastTimer <= 0 && badgeNews == null)
        {
            ev.announced = true;
            Toast("Harbour news: " + EventNews(ev), 6);
        }
    }

    // Cast: the run's spot bites twice as fast.
    bool FishRunAt(string spot) => EventOn("run") && TodayEvent().arg == spot;
    // Luck: shooting stars over a clear night.
    bool MeteorNow => EventOn("meteor") && Night && state.weather == "clear";

    /* ---------- Targets ---------- */
    Target EventTarget()
    {
        if (EventsOff || MatchActive || state?.created != true || Aboard) return null;
        float x = player.X, y = player.Y;
        var ev = TodayEvent();
        if (ev.kind == "bottle" && ev.stage == 0 && Dist(x, y, ev.x, ev.y) < 12) return new Target { Type = "event", Id = "bottle", Label = "Pick up the bottle" };
        if (ev.kind == "turtle" && ev.stage < 2 && Dist(x, y, ev.x, ev.y + 2) < (ev.stage == 0 ? 26 : 14))
            return new Target { Type = "event", Id = "turtle", Label = ev.stage == 0 ? "Look at the stranded turtle (from a distance)" : "The turtle needs help: tell Tomas" };
        if (TraderHere && Dist(x, y, ev.x, ev.y + 8) < 13) return new Target { Type = "event", Id = "trader", Label = "Trade with the travelling trader" };
        if (state.hunt is Hunt h && Dist(x, y, h.x, h.y) < 12) return new Target { Type = "event", Id = "dig", Label = "Dig where the X is" };
        if (MoreEventTarget(ev) is Target moreTarget) return moreTarget;
        return null;
    }

    void UseEvent(Target t)
    {
        var ev = TodayEvent();
        switch (t.Id)
        {
            case "bottle": PickBottle(ev); break;
            case "turtle": LookAtTurtle(ev); break;
            case "trader": FaceToward(ev.x, ev.y); Sfx.Play("ui"); panel = "trader"; mode = "panel"; SetPrompt(""); break;
            case "dig": DigHunt(); break;
            default: UseMoreEvent(t.Id, ev); break;
        }
    }

    /* ---------- The bottle ---------- */
    void PickBottle(DayEvent ev)
    {
        FaceToward(ev.x, ev.y);
        string island = RegionOf(ev.x, ev.y);
        var spots = BeachTiles(island, shore: false).Where(p => Dist(p.tx * T + 5, p.ty * T + 7, ev.x, ev.y) > 60).ToList();
        if (spots.Count == 0) spots = BeachTiles(island, shore: false);
        if (spots.Count == 0) spots = BeachTiles(island);
        // Nowhere left at all (built over): the X goes where the bottle lay, which you're standing beside (Codex).
        if (spots.Count == 0) spots = new() { ((int)(ev.x / T), (int)(ev.y / T)) };
        var rng = new MatchRng(MatchRules.Mix(ev.day, BoardSalt, EventVersion, 59));
        var (tx, ty) = spots[(int)(rng.NextDouble() * spots.Count) % spots.Count];
        var loot = new (string id, int n)[] { ("glow_bait", 3), ("pearl", 1), ("iron_bar", 2), ("chum", 3), ("crystal", 1) };
        var (item, n) = loot[(int)(rng.NextDouble() * loot.Length) % loot.Length];
        // Picked up, read and the X kept, all in one save before anything is said.
        ev.stage = 1;
        state.hunt = new Hunt { x = tx * T + 5, y = ty * T + 7, region = island, coins = 30 + (int)(rng.NextDouble() * 41), item = item, n = n };
        mapTexDirty = true;
        Sfx.Play("pickup");
        Save();
        Talk(new()
        {
            new("", "Inside the bottle, a curl of paper in a careful hand:"),
            new("Note", $"\"Left something for whoever finds this. On {IslandName(island)}, away from the water. I marked it on a chart.\""),
            new("", "There's a little map on the back. You copy the X onto your own chart (<map>).")
        });
    }

    void DigHunt()
    {
        if (state.hunt is not Hunt h) return;
        FaceToward(h.x, h.y);
        Swing("dig", 0.5f);
        state.coins += h.coins;
        Give(h.item, h.n);
        state.chests++;
        state.hunt = null;
        mapTexDirty = true;
        Burst(h.x, h.y - 4, "#f3c25b", 12);
        Sfx.Play("rare");
        GainXp(10);
        Save();
        Toast($"You dig up a little box: {h.coins} coins and {Items.Amount(h.item, h.n)}!", 4.5f);
    }

    /* ---------- The turtle ---------- */
    void LookAtTurtle(DayEvent ev)
    {
        FaceToward(ev.x, ev.y);
        bool first = ev.stage == 0;
        if (first) { ev.stage = 1; RecordSighting("pawikan", ev.x, ev.y - 10, "A green sea turtle, stranded high on the sand."); }
        Talk(new()
        {
            new("", "It's breathing, but it can't get back down to the water by itself."),
            new("", "Don't pick it up or push it back into the sea: it may be hurt or weak, and trained people need to check it."),
            new("", "Keep back, keep dogs and people away, and get help: tell Tomas, so he can call the responders.")
        });
    }

    // Tomas, with a turtle to report: he radios the trained responders, and they take it from there.
    bool TurtleToReport => EventOn("turtle") && TodayEvent().stage < 2;

    void ReportTurtle()
    {
        var ev = TodayEvent();
        ev.stage = 2;
        state.coins += 40;
        state.hinted["turtleReported"] = true;
        Sfx.Play("coin");
        Save();
        Talk(new()
        {
            new("Tomas", "A pawikan on the beach? Good that you came straight here and didn't try to move it."),
            new("Tomas", "I'll radio the municipal environment office and the Bantay Dagat. They have people trained for this: they'll check it and see it home."),
            new("Tomas", "Here, 40 coins from the patrol box. That's what it's for.")
        });
    }

    /* ---------- The trader's panel ---------- */
    float lastTraderBottom;

    void DrawTrader()
    {
        var ev = TodayEvent();
        var market = ev.kind == "market";
        var (x, y, w, h) = PanelFrame(market ? "Market day" : "Travelling trader", market ? "Local goods until " + HourText(16 * 60) + ", from the stalls of the village." : "Here until " + HourText(18 * 60) + ". Things you won't find at Pip's.", 760, market ? 540 : 420);
        if (PanelClose()) { ClosePanels(); return; }
        if (!TradeOpen) { Gfx.Text(ev.kind == "market" ? "The market has packed up for the day." : "The trader has packed up and gone.", x + PanelPad, y + 120, FontKind.Note, 20, Muted); return; }
        float ry = y + 112;
        for (int i = 0; i < ev.offers.Count; i++)
        {
            var o = ev.offers[i];
            float top = ry + i * 104;
            Gfx.Box(x + PanelPad, top, w - PanelPad * 2, 92, CardBg, Pal.C("#c9b48f"), 2, 6);
            DrawIcon(o.item, x + PanelPad + 16, top + 22, 48);
            string name = Items.Amount(o.item, o.n);
            Gfx.Text(Gfx.Ellipsize(char.ToUpper(name[0]) + name[1..], FontKind.Ui700, 22, 360), x + PanelPad + 84, top + 16, FontKind.Ui700, 22, Pal.PaperInk);
            Gfx.Text(Gfx.Ellipsize(Items.ById[o.item].Desc, FontKind.Ui500, 15, 380), x + PanelPad + 84, top + 50, FontKind.Ui500, 15, Muted);
            float bx = x + w - PanelPad - 180;
            if (o.sold) Gfx.Text("Sold", bx + 60, top + 32, FontKind.Ui700, 22, Pal.C("#3f7d35"));
            else
            {
                string label = $"Buy: {o.price}";
                if (Button(label, bx, top + 22, 164, 48, FontKind.Ui700, 20, state.coins >= o.price ? Pal.Buoy : Pal.Sand, state.coins >= o.price ? White : Pal.Ink, 4))
                {
                    if (state.coins < o.price) { Sfx.Play("nope"); Toast("Not enough coins."); }
                    else
                    {
                        state.coins -= o.price; o.sold = true; Give(o.item, o.n);
                        Sfx.Play("coin"); Save();
                        Toast($"You buy {Items.Amount(o.item, o.n)} from the trader.");
                    }
                }
#if DEBUG
                Gfx.Seen["trader:buy:" + i] = new Rectangle(bx, top + 22, 164, 48);
#endif
            }
            lastTraderBottom = MathF.Max(lastTraderBottom, top + 92);
        }
    }

    /* ---------- Drawing ---------- */
    void AddEventObjects(List<(float y, Action draw)> list)
    {
        if (EventsOff || MatchActive || state?.created != true) return;
        var ev = TodayEvent();
        bool Near(float wx, float wy) => wx > camX - 40 && wx < camX + W + 40 && wy > camY - 30 && wy < camY + H + 40;
        if (ev.kind == "bottle" && ev.stage == 0 && Near(ev.x, ev.y)) list.Add((ev.y, () => DrawBottle(ev.x, ev.y)));
        if (ev.kind == "turtle" && ev.stage < 3 && Near(ev.x, ev.y)) list.Add((ev.y, () => DrawStrandedTurtle(ev)));
        if (TraderHere && Near(ev.x, ev.y)) list.Add((ev.y, () => DrawTraderFigure(ev.x, ev.y)));
        if (state.hunt is Hunt h && Near(h.x, h.y)) list.Add((h.y - 6, () => DrawX(h.x, h.y)));
        MoreEventObjects(ev, list, Near);
    }

    void DrawBottle(float x, float y)
    {
        float glint = Calm ? 0 : MathF.Max(0, MathF.Sin(time * 3.1f)) ;
        pix.Rect(x - 3, y, 7, 1, "rgba(0,0,0,0.2)");
        pix.Rect(x - 3, y - 2, 5, 2, "#5f9a7a"); pix.Rect(x + 2, y - 2, 2, 1, "#5f9a7a"); pix.Rect(x + 4, y - 2, 1, 1, "#8a6038");
        pix.Rect(x - 2, y - 2, 2, 1, "#e9e0c6");
        if (glint > 0.6f) pix.Rect(x, y - 3, 1, 1, "#ffffff");
    }

    void DrawX(float x, float y)
    {
        var c = Pal.C("#8a2a1c");
        for (int i = -3; i <= 3; i++) { pix.Rect(x + i, y + i * 0.6f, 1, 1, c); pix.Rect(x + i, y - i * 0.6f, 1, 1, c); }
    }

    void DrawStrandedTurtle(DayEvent ev)
    {
        var pal = SeaPalette("pawikan", SeaLifeArt.PawikanPalette, 1);
        var grid = SeaLifeArt.Pawikan[0];
        int x = (int)ev.x, y = (int)ev.y, ax = SeaLifeArt.PawikanAnchorX, ay = SeaLifeArt.PawikanAnchorY;
        // Its flipper tracks up the sand from the water's edge, so it reads as having crawled out of the sea.
        float best = 99, dx = 0, dy = 1;
        for (int k = 0; k < 8; k++)
        {
            float ux = MathF.Cos(k * MathF.PI / 4), uy = MathF.Sin(k * MathF.PI / 4);
            for (int d = 6; d < 40 && d < best; d += 2)
                if (WaterTile(TileAt((int)((x + ux * d) / T), (int)((y + uy * d) / T)))) { best = d; dx = ux; dy = uy; break; }
        }
        var track = Pal.Rgba(90, 70, 40, 0.35f);
        for (float d = 8; d < best; d += 3)
        {
            float tx = x + dx * d, ty = y + dy * d;
            pix.Rect(tx - dy * 3, ty + dx * 3, 1, 1, track); pix.Rect(tx + dy * 3, ty - dx * 3, 1, 1, track);
        }
        pix.Rect(x - 9, y + 1, 18, 2, "rgba(0,0,0,0.2)");
        for (int j = 0; j < grid.Length; j++)
            for (int i = 0; i < grid[j].Length; i++)
                if (grid[j][i] != '.') pix.Rect(x + i - ax, y + j - ay, 1, 1, pal[grid[j][i] - 'a']);
        // Reported: a responder kneels by it with a damp cloth.
        if (ev.stage == 2) LookData.DrawPerson(pix, new Look { skin = 2, shirt = 4, hat = 0, hair = 1 }, x + 14, y + 2, "left", 0);
    }

    void DrawTraderFigure(float x, float y)
    {
        pix.Rect(x - 12, y - 6, 9, 7, "#8a6038"); pix.Rect(x - 12, y - 6, 9, 1, "#b58250");   // a crate of goods
        pix.Rect(x - 10, y - 9, 3, 3, "#c0392b"); pix.Rect(x - 6, y - 8, 2, 2, "#f3c25b");
        LookData.DrawPerson(pix, new Look { skin = 1, shirt = 5, hat = 1, hair = 2 }, (int)x, (int)y, Dist(player.X, player.Y, x, y) < 40 ? "down" : "left", 0,
            arms: Dist(player.X, player.Y, x, y) < 40 ? 1 : 0, swing: (int)(time * 3) % 2);
    }

    // A fish run: little silver splashes over the spot.
    int runDrawn;   // splashes drawn last frame (the autotest)
    void DrawFishRun(float t)
    {
        if (!EventOn("run") || !Data.SpotById.TryGetValue(TodayEvent().arg, out var s)) return;
        if (s.X < camX - 30 || s.X > camX + W + 30 || s.Y < camY - 30 || s.Y > camY + H + 30) return;
        // Six fish at a time, each leaping in a little arc and leaving a ring, all over the spot.
        runDrawn = 0;
        for (int k = 0; k < 6; k++)
        {
            float ph = (t * 0.8f + k * 0.29f) % 1;
            float a = k * 2.1f + MathF.Floor(t * 0.8f + k * 0.29f) * 1.7f;
            float px = s.X + MathF.Cos(a) * s.R * 0.55f, py = s.Y + MathF.Sin(a) * s.R * 0.4f;
            if (TileAt((int)(px / T), (int)(py / T)) is not ('~' or 'w' or 'l' or 'o' or 'm')) continue;
            pix.Ring(px, py, 1.5f + ph * 6, Pal.Rgba(245, 252, 255, 0.95f * (1 - ph))); runDrawn++;
            if (ph > 0.15f) pix.Ring(px, py, 1 + (ph - 0.15f) * 4, Pal.Rgba(245, 252, 255, 0.6f * (1 - ph)));
            if (ph < 0.35f && !Calm)
            {
                float u = ph / 0.35f, fx = px - 3 + u * 6, fy = py - MathF.Sin(u * MathF.PI) * 6;
                pix.Rect(fx - 1, fy, 3, 1, Pal.C("#c9d6de")); pix.Rect(fx - 1, fy + 1, 2, 1, Pal.C("#7f93a3"));
                if (u > 0.85f) pix.Rect(px + 2, py - 1, 2, 1, Pal.Rgba(255, 255, 255, 0.8f));
            }
        }
    }

    // Shooting stars after dark, over the dark (drawn in screen space like the rain).
    void DrawMeteors(float t)
    {
        if (!MeteorNow || Calm) return;
        for (int k = 0; k < 3; k++)
        {
            float cyc = 2.6f + k * 1.3f, ph = (t + k * 0.9f) % cyc / 0.7f;
            if (ph > 1) continue;
            int seed = (int)((t + k * 0.9f) / cyc) * 7 + k;
            float sx = Hash01(seed, 1) * W, sy = Hash01(seed, 2) * H * 0.6f;
            float hx = sx + ph * 40, hy = sy + ph * 18;
            for (int i = 0; i < 12; i++)
                pix.Fill((int)(hx - i * 1.8f), (int)(hy - i * 0.8f), 1, 1, Pal.Rgba(255, 250, 230, (1 - ph) * (1 - i / 12f)));
        }
    }

    static float Hash01(int a, int b) => (uint)MatchRules.Mix(a, b, 97) % 10000 / 10000f;

    /* ---------- The chart, the journal and the board ---------- */
    void EventChartMarks(Action<string, float, float, int, List<string>, int> place)
    {
        if (EventsOff || MatchActive || state?.created != true) return;
        var ev = TodayEvent();
        if (state.hunt is Hunt h) place("X", h.x, h.y, 5, new() { "X marks the spot", "From the note in the bottle. Dig there with <act>." }, 4);
        if (ev.kind == "run" && Data.SpotById.TryGetValue(ev.arg, out var s)) place("Fish run", s.X, s.Y, 5, new() { "Fish run", "Bites twice as fast here today" }, 4);
        if (ev.kind == "trader") place("Trader", ev.x, ev.y, 1, new() { "Travelling trader", $"{HourText(9 * 60)} to {HourText(18 * 60)}" }, 4);
        if (ev.kind == "turtle" && ev.stage < 2) place("Turtle", ev.x, ev.y, 5, new() { "Stranded sea turtle", "Keep back, and tell Tomas" }, 4);
        if (ev.kind == "whale" && WhaleOpen && ev.stage == 0) place("Whale song", ev.x, ev.y, 5, new() { "Whale song", $"Listen here, {HourText(6 * 60)} to {HourText(10 * 60)}" }, 4);
        if (MarketHere) place("Market", ev.x, ev.y, 1, new() { "Market day", $"{HourText(8 * 60)} to {HourText(16 * 60)}" }, 4);
        if (ev.kind == "kite" && ev.stage == 0) place("Kite", ev.x, ev.y, 5, new() { "A lost kite", "Pick it up, then see Ma'am Isay" }, 4);
    }

    void EventGoals(List<Goal> list)
    {
        if (EventsOff || MatchActive || state?.created != true) return;
        var ev = TodayEvent();
        if (state.hunt is Hunt h)
            list.Add(At(NewGoal("event:hunt", "daily", "X marks the spot", $"Dig where the bottle's note marked, on {IslandName(h.region)} (<act>)."), h.x, h.y, "The X"));
        Goal g = ev.kind switch
        {
            "run" when Data.SpotById.TryGetValue(ev.arg, out var s) => At(NewGoal("event:run", "daily", $"Fish run at {s.Label}", "Bites come twice as fast there today."), s.X, s.Y, s.Label),
            "trader" when TraderHere && ev.offers.Any(o => !o.sold) => At(NewGoal("event:trader", "daily", "A travelling trader", $"By the harbour board on {BoardName(ev.arg)} until {HourText(18 * 60)}."), ev.x, ev.y + 8, "The trader"),
            "bottle" when ev.stage == 0 => At(NewGoal("event:bottle", "daily", "A bottle on the beach", "Something glints at the water's edge. Pick it up (<act>)."), ev.x, ev.y, "The bottle"),
            "turtle" when ev.stage == 0 => At(NewGoal("event:turtle", "daily", "A stranded sea turtle", "Look from a distance, then tell Tomas."), ev.x, ev.y, "The turtle"),
            "turtle" when ev.stage == 1 => AtTomas(NewGoal("event:turtle", "daily", "Tell Tomas about the turtle", "He'll call the trained responders. Don't move it yourself.")),
            "meteor" => NewGoal("event:meteor", "daily", "Shooting stars tonight", "After dark on a clear night, rare fish rise while they fall."),
            "whale" or "market" or "kite" => MoreEventGoal(ev),
            _ => null
        };
        if (g != null) list.Add(g);
    }

    string EventBoardLine()
    {
        if (EventsOff || MatchActive || state?.created != true) return null;
        var ev = TodayEvent();
        return ev.kind == "none" ? "No news today. Quiet seas." : EventNews(ev);
    }
}
