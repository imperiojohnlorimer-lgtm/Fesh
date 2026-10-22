using Raylib_cs;

namespace Fesh;

// The harbour board (1.45): three short bounties a day, never required, rolled from the day itself so they are the same
// whenever you look, and paid the moment they're done. Most are measured as how far a counter the save already keeps
// (perfect hooks, crabs sorted, dishes cooked, miles sailed...) has moved since the board was rolled; the catches go
// through one hook in AddCatch. A match plays on a State of its own, so nothing in one counts. Finishing any bounty
// credits the day for the streak; a day with none breaks it (nothing is taken away).
sealed class Bounty
{
    public string kind, arg;          // what it asks; arg is a fish id or a region for "species" and "place"
    public int need, have, tier;      // how many, how many so far, and 0 easy, 1 standard, 2 stretch (what it pays)
    public double start;              // the counter it measures when it was rolled
    public bool paid;
    public List<string> ids = new();  // the different fish so far, for "kinds"
}

sealed class BoardDay { public int day; public List<Bounty> list = new(); }

partial class Game
{
    // The old scripts never see the board's goal, payments or banners (AutoTestStart); BoardScript turns it on.
    internal static bool BoardOff;
    const int BoardVersion = 1;

    // The boards: on Saltmere in the meadow between Tomas's camp and Pip's stall, at Amihan Village's landing, Asinan's landing and Marilaya's
    // harbour, each placed (BoardSpotOk) well clear of everything people walk up to.
    // Each needs its island found before it shows on the chart; they all show the one board.
    static readonly (string id, string name, float x, float y)[] HarbourBoards =
    {
        ("saltmere", "Saltmere", 176, 120),
        ("amihan", "Amihan Village", 1569, 246),
        ("asinan", "Asinan", 250, 598),
        ("marilaya", "Marilaya", 2800, 160),
    };

    bool BoardKnown(string id) => id switch
    {
        "amihan" => state.Hinted("amihan"),
        "asinan" => state.Hinted("habagat"),
        "marilaya" => state.Hinted("marilaya"),
        _ => true
    };

    static Box BoardBox(float x, float y) => new(x - 9, y - 2, 18, 3);

    // Everything people walk up to on the islands: a board must keep well clear of all of them (the user found the first
    // Saltmere board standing in front of Tomas's campfire, and the Amihan one in front of Lira's door).
    IEnumerable<(float x, float y)> InteractionPoints()
    {
        yield return (FireX, FireY); yield return (PipX, PipY + 14); yield return (TomasHomeX, TomasHomeY); yield return (160, 72);
        yield return (SaltJettyX, SaltJettyY); yield return (AtollJettyX, AtollJettyY); yield return (AsinanJettyX, AsinanJettyY); yield return (MlJettyX, MlJettyY);
        yield return (1495, 217); yield return (NoticeX, NoticeY + 6); yield return (MouthDoorX, MouthDoorY); yield return (CarvingX, CarvingY);
        yield return (PostX, PostY); yield return (AlertBoardX, AlertBoardY); yield return (CrateX, CrateY); yield return (EvacTableX, EvacTableY); yield return (RockfallX, RockfallY);
        foreach (var p in SignSpots) yield return p;
        foreach (var n in Islanders) { yield return (n.x, n.y); yield return (n.x, n.y + 10); }
        foreach (var n in HabagatFolk) { yield return (n.x, n.y); yield return (n.x, n.y + 10); }
        foreach (var f in MlFolk) yield return (MlWx(f.lx), MlWy(f.ly));
        foreach (var p in MlPlaces.Values) yield return (p.x, p.y);
        foreach (var s in Data.Spots) if (s.Scene == "world") yield return (s.X, s.Y);
    }

    // A board fits here: its posts on open ground with nothing solid by them, room to stand in front, its drawing clear of
    // buildings, and every place people go at least 34 px off.
    bool BoardSpotOk(float x, float y, List<Box> solids)
    {
        for (int k = 0; k < 3; k++) if (!Walkable(TileAt((int)MathF.Floor((x - 9 + k * 8.5f) / T), (int)MathF.Floor((y - 1) / T)))) return false;
        var look = new Box(x - 12, y - 30, 24, 33);
        if (solids.Any(s => s.Overlaps(look))) return false;
        if (!CanStand(x, y + 8) || !CanStand(x, y + 14)) return false;
        if (trees.Any(t => Dist(t.x * T + 5, t.y * T + 5, x, y) < 16)) return false;
        // Not at the head of a bridge, a jetty or the old dock either, where it would stand in the way.
        int tx0 = (int)MathF.Floor(x / T), ty0 = (int)MathF.Floor(y / T);
        for (int dy = -3; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (TileAt(tx0 + dx, ty0 + dy) is 'd' or 'b' or 'x') return false;
        return InteractionPoints().All(p => Dist(p.x, p.y, x, y) >= 34 && Dist(p.x, p.y, x, y + 8) >= 34);
    }
    // A board you've built over in an older save isn't there (it never sits on something of yours).
    bool BoardStands(int i) => !state.builds.Any(b => BuildBox(b) is Box r && r.Overlaps(BoardBox(HarbourBoards[i].x, HarbourBoards[i].y)));
    IEnumerable<Box> BoardSolids()
    {
        for (int i = 0; i < HarbourBoards.Length; i++) if (BoardStands(i)) yield return BoardBox(HarbourBoards[i].x, HarbourBoards[i].y);
    }

    Target BoardTarget()
    {
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (_, _, bx, by) = HarbourBoards[i];
            if (BoardStands(i) && Dist(player.X, player.Y, bx, by + 8) < 10)
                return new Target { Type = "board", Label = BoardOff ? "Read the harbour board" : $"Read the harbour board ({BoardDoneCount()} of 3 done)" };
        }
        return null;
    }

    /* ---------- The day's bounties ---------- */
    // Today's board, rolled the first time anything asks on a new day (so a save loaded at any hour has one).
    BoardDay Board()
    {
        if (state.board != null && state.board.day == state.day && state.board.list.Count > 0) return state.board;
        // Yesterday's board is settled first: a counter that moved since the last once-a-second look still pays (and
        // credits its own day), even if the dawn came in between (Codex).
        if (state.board != null && state.board.day < state.day) { CatchUpCounters(state.board); PayBounties(state.board); }
        // A day passed with nothing done breaks the streak; the board you missed is just gone.
        if (state.boardLastDay < state.day - 1) state.boardStreak = 0;
        state.board = RollBoard(state.day);
        return state.board;
    }

    int BoardDoneCount() => Board().list.Count(b => b.paid);

    // Progress tier from how many kinds of fish you know: what a bounty asks for grows with you.
    int BoardTier => SpeciesCaught < 10 ? 0 : SpeciesCaught < 40 ? 1 : 2;

    sealed record BountyKind(string Id, int[] Need, int Tier, Func<Game, bool> Ok, Func<Game, double> Counter = null);

    static readonly BountyKind[] BountyKinds =
    {
        new("fish", new[] { 2, 4, 6 }, 0, g => true),
        // These four any fisher can do from the first morning (Pip buys any fish), so a board always has three.
        new("kinds", new[] { 2, 3, 4 }, 1, g => true),
        new("species", new[] { 1, 2, 3 }, 1, g => g.BoardFishChoices().Count > 0),
        new("place", new[] { 2, 3, 4 }, 1, g => g.BoardPlaceChoices().Count > 0),
        new("big", new[] { 1, 1, 2 }, 2, g => true),
        new("perfect", new[] { 1, 2, 3 }, 1, g => g.state.perfects > 0, g => g.state.perfects),
        new("sort", new[] { 3, 5, 8 }, 1, g => g.state.sortRight > 0, g => g.state.sortRight),
        new("cook", new[] { 1, 2, 3 }, 1, g => g.Tally("cooked") > 0, g => g.Tally("cooked")),
        new("lesson", new[] { 1, 1, 1 }, 1, g => g.state.lessons > 0, g => g.state.lessons),
        new("sail", new[] { 5, 8, 10 }, 2, g => g.Has("boat") > 0, g => g.state.sailed / MilePx * 10),   // tenths of a nautical mile
        new("sell", new[] { 3, 6, 10 }, 0, g => true, g => g.Tally("sold")),
        new("monsters", new[] { 2, 3, 5 }, 2, g => g.state.caveDeepest > 0 && g.Tally("monsters") > 0, g => g.Tally("monsters")),
    };

    // Fish a bounty may ask for by name: ones you've caught, that bite somewhere you can fish today without a story, a
    // moon, a storm or a special lure, at a spot that's open and known (never the Starwell), and that you may keep.
    List<CommonFish> BoardFishChoices()
    {
        var list = new List<CommonFish>();
        foreach (var s in Data.Spots)
        {
            if (!SpotOpen(s.Id) || !SpotKnown(s) || s.Id == "starwell" || s.Scene == "cave") continue;
            if (s.Scene == "sea" && Has("boat") == 0 && !state.tamed) continue;
            if (s.Scene == "world" && !Charted(SpotRegion(s))) continue;
            foreach (var f in Data.Common[s.Id])
                if (state.commons.GetValueOrDefault(f.Id) > 0 && EverydayFish(f) && !list.Contains(f))
                    list.Add(f);
        }
        return list.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
    }

    // A fish that bites today with nothing special: no story, moon, weather, bait or lure, in season and yours to keep.
    bool EverydayFish(CommonFish f) => !f.Legend && !f.Rare && !f.Troll && !f.FullMoon && f.Weather == null && f.Bait == null
        && f.Need == null && (f.Season == null || f.Season == Season) && !ClosedSeason(f.Id);

    // Places a bounty may name: charted islands with an open spot of everyday fish on them.
    List<string> BoardPlaceChoices() => Data.Spots
        .Where(s => s.Scene == "world" && s.Id != "starwell" && SpotOpen(s.Id) && SpotKnown(s) && Charted(SpotRegion(s))
            && RegionById.ContainsKey(SpotRegion(s)) && Data.Common[s.Id].Any(EverydayFish))
        .Select(SpotRegion).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();

    BoardDay RollBoard(int day)
    {
        var rng = new MatchRng(MatchRules.Mix(day, BoardSalt, BoardVersion, 41));
        int tier = BoardTier;
        var open = BountyKinds.Where(k => k.Ok(this)).ToList();
        var picked = new List<BountyKind>();
        // One plain catch first, then two others, never the same kind twice.
        picked.Add(open.First(k => k.Id == "fish"));
        if (rng.NextDouble() < 0.4 && open.FirstOrDefault(k => k.Id == "kinds") is BountyKind kinds) picked[0] = kinds;
        while (picked.Count < 3)
        {
            var left = open.Where(k => !picked.Contains(k) && !(k.Id is "fish" or "kinds" && picked.Any(p => p.Id is "fish" or "kinds"))).ToList();
            if (left.Count == 0) left = open.Where(k => !picked.Contains(k)).ToList();
            if (left.Count == 0) break;
            picked.Add(left[(int)(rng.NextDouble() * left.Count) % left.Count]);
        }
        var board = new BoardDay { day = day };
        foreach (var k in picked)
        {
            var b = new Bounty { kind = k.Id, need = k.Need[tier], tier = k.Tier, start = k.Counter?.Invoke(this) ?? 0 };
            if (k.Id == "species") { var c = BoardFishChoices(); b.arg = c[(int)(rng.NextDouble() * c.Count) % c.Count].Id; }
            if (k.Id == "place") { var c = BoardPlaceChoices(); b.arg = c[(int)(rng.NextDouble() * c.Count) % c.Count]; }
            board.list.Add(b);
        }
        return board;
    }

    // Each save's boards differ from another's on the same day: a number made from the fisher's name.
    int BoardSalt
    {
        get
        {
            int h = 17;
            foreach (char c in state.look?.name ?? "") h = unchecked(h * 31 + c);
            return h;
        }
    }

    /* ---------- What they say and pay ---------- */
    string BountyText(Bounty b)
    {
        int n = b.need;
        string S(string one, string many) => n == 1 ? one : many;
        return b.kind switch
        {
            "fish" => $"Catch {n} {S("fish", "fish")}",
            "kinds" => $"Catch {n} different kinds of fish",
            "species" => n == 1 ? $"Catch a {FishShort(b.arg)}" : $"Catch {n} {FishShort(b.arg)}",
            "place" => $"Catch {n} fish around {(RegionById.TryGetValue(b.arg ?? "", out var r) ? r.Name : "the islands")}",
            "big" => n == 1 ? "Land a big fish" : $"Land {n} big fish",
            "perfect" => $"Hook {n} {S("fish", "fish")} perfectly",
            "sort" => $"Sort {n} crabs right",
            "cook" => $"Cook {n} {S("dish", "dishes")}",
            "lesson" => "Finish one of Ma'am Isay's lessons",
            "sail" => $"Steer your boat {n / 10f:0.#} nautical mile{(n == 10 ? "" : "s")}",
            "sell" => $"Sell Pip {n} fish",
            "monsters" => $"Beat {n} cave monsters",
            _ => b.kind
        };
    }

    static string FishShort(string id) => Data.FishById.TryGetValue(id ?? "", out var f) ? f.Name.Split(" (")[0] : "fish";

    static (int coins, int bait) BountyPay(Bounty b) => b.tier switch { 0 => (10, 0), 1 => (20, 2), _ => (35, 3) };
    static string BountyPayText(Bounty b) { var (c, n) = BountyPay(b); return n > 0 ? $"{c} coins, {n} bait" : $"{c} coins"; }

    /* ---------- Progress ---------- */
    float boardCheckT;
    bool boardSaveDue;

    // Once a second: the counted bounties catch up, and anything done is paid.
    void UpdateBoard(float dt)
    {
        if (BoardOff || MatchActive || mode is "title" or "create" || state == null || !state.created) return;
        // A catch that finished a bounty saves here, after whatever landed it has finished (a pot hauled, a crab tray opened).
        if (boardSaveDue) { boardSaveDue = false; Save(); }
        boardCheckT -= dt;
        if (boardCheckT > 0) return;
        boardCheckT = 1;
        var day = Board();
        CatchUpCounters(day);
        PayBounties(day);
    }

    void CatchUpCounters(BoardDay day)
    {
        foreach (var b in day.list)
            if (!b.paid && BountyKinds.FirstOrDefault(k => k.Id == b.kind)?.Counter is Func<Game, double> counter)
                b.have = Math.Max(b.have, (int)Math.Floor(counter(this) - b.start + 1e-6));
    }

    // AddCatch: one fish (each of a double catch counts). Where you stand is where it was caught: the rod, a pot, a
    // spear or a bubo, it's all from beside you.
    void BoardCatch(CommonFish f, float m)
    {
        if (BoardOff || MatchActive || f.Legend || state == null || !state.created) return;
        var day = Board();
        string here = scene == "world" ? RegionOf(player.X, player.Y) : null;
        foreach (var b in day.list)
        {
            if (b.paid) continue;
            switch (b.kind)
            {
                case "fish": b.have++; break;
                case "kinds": if (!b.ids.Contains(f.Id)) { b.ids.Add(f.Id); b.have = b.ids.Count; } break;
                case "species": if (f.Id == b.arg) b.have++; break;
                case "place": if (here == b.arg) b.have++; break;
                case "big": if (m >= 1.25f) b.have++; break;
            }
        }
        PayBounties(day, later: true);
    }

    // Done is paid at once and saved before anything is shown, so a quit can't lose it and a reload can't pay it twice.
    void PayBounties(BoardDay day, bool later = false)
    {
        var done = day.list.Where(b => !b.paid && b.have >= b.need).ToList();
        if (done.Count == 0) return;
        int coins = 0, bait = 0;
        foreach (var b in done)
        {
            b.paid = true;
            var (c, n) = BountyPay(b);
            coins += c; bait += n;
            state.boardDone++;
        }
        state.coins += coins;
        if (bait > 0) Give("bait", bait);
        bool newDay = state.boardLastDay != day.day;
        if (newDay)
        {
            state.boardStreak = state.boardLastDay == day.day - 1 ? state.boardStreak + 1 : 1;
            state.boardLastDay = day.day;
            state.boardBest = Math.Max(state.boardBest, state.boardStreak);
        }
        if (later) boardSaveDue = true; else Save();
        bool all = day.list.All(b => b.paid);
        string got = $"{coins} coins" + (bait > 0 ? $" and {bait} bait" : "");
        Celebrate(new Celeb
        {
            Kind = "board", Icon = "star", Sting = all ? "badge" : "page",
            Tag = all ? "Harbour board: all three done!" : "Harbour board",
            Title = done.Count == 1 ? BountyText(done[0]) : $"{done.Count} bounties done",
            Sub = $"You get {got}." + (newDay && state.boardStreak > 1 ? $" {state.boardStreak} days in a row!" : ""),
        }, () => Toast($"Harbour board: {(done.Count == 1 ? BountyText(done[0]) : $"{done.Count} bounties")} done. {got}."));
    }

    /* ---------- The board in the world ---------- */
    void AddBoardObjects(List<(float y, Action draw)> list)
    {
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (_, _, bx, by) = HarbourBoards[i];
            if (!BoardStands(i) || bx < camX - 40 || bx > camX + W + 40 || by < camY - 20 || by > camY + H + 50) continue;
            list.Add((by, () => DrawHarbourBoard((int)bx, (int)by)));
        }
    }

    void BoardShadows()
    {
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (_, _, bx, by) = HarbourBoards[i];
            if (BoardStands(i) && bx > camX - 60 && bx < camX + W + 60 && by > camY - 40 && by < camY + H + 60) BuildingShadow((int)bx - 9, (int)by + 1, 18, 16);
        }
    }

    void DrawHarbourBoard(int x, int y)
    {
        if (OldArt)
        {
            // The hand-drawn fallback: two posts, a board and three notices.
            pix.Rect(x - 8, y - 18, 2, 18, "#4e3420"); pix.Rect(x + 6, y - 18, 2, 18, "#4e3420");
            pix.Rect(x - 9, y - 17, 18, 10, "#8a6038");
            pix.Rect(x - 7, y - 15, 4, 5, "#e9e0c6"); pix.Rect(x - 2, y - 14, 4, 5, "#d8d0b0"); pix.Rect(x + 3, y - 12, 3, 4, "#e9e0c6");
            pix.Rect(x - 10, y - 20, 20, 2, "#7e4030");
        }
        else BlitArt(LandmarkArt.HarbourBoard, LandmarkArt.HarbourBoardPalette, LandmarkArt.HarbourBoardAnchorX, LandmarkArt.HarbourBoardAnchorY, x, y);
        // Something on it you haven't read since it went up: a gold pin that bobs a little.
        if (!BoardOff && state.boardSeen != state.day)
        {
            float bob = Calm ? 0 : MathF.Round(MathF.Sin(time * 3) * 0.6f);
            pix.Rect(x - 1, y - 26 + bob, 3, 3, "#f3c25b");
            pix.Rect(x, y - 25 + bob, 1, 1, "#fff2b0");
        }
    }

    void OpenBoard()
    {
        Board();
        state.boardSeen = state.day;
        Sfx.Play("ui");
        panel = "board";
        mode = "panel";
        SetPrompt("");
    }

    /* ---------- The journal ---------- */
    // A goal for today's board: where it is until you've read it today, then just how far along it is.
    void BoardGoals(List<Goal> list)
    {
        if (BoardOff || MatchActive || state == null || !state.created) return;
        var day = Board();
        int done = day.list.Count(b => b.paid);
        if (done == day.list.Count) return;
        var left = day.list.Where(b => !b.paid).Select(BountyText).ToList();
        var g = NewGoal("board:today", "daily", $"Harbour board: {done} of {day.list.Count} done", string.Join(". ", left) + ".");
        if (state.boardSeen != state.day && NearestBoard() is var (bx, by, name)) At(g, bx, by + 8, $"The harbour board ({name})");
        list.Add(g);
    }

    (float x, float y, string name)? NearestBoard()
    {
        (float, float, string)? best = null;
        float bd = float.MaxValue;
        float px = scene == "world" ? player.X : state.exitX, py = scene == "world" ? player.Y : state.exitY;
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (id, name, x, y) = HarbourBoards[i];
            if (!BoardKnown(id) || !BoardStands(i)) continue;
            float d = Dist(px, py, x, y);
            if (d < bd) { bd = d; best = (x, y, name); }
        }
        return best;
    }

    void BoardChartMarks(Action<string, float, float, int, List<string>, int> place)
    {
        if (BoardOff) return;
        var day = Board();
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (id, name, x, y) = HarbourBoards[i];
            if (BoardKnown(id) && BoardStands(i)) place("Harbour board", x, y, 5, new() { $"Harbour board ({name})", $"{day.list.Count(b => b.paid)} of {day.list.Count} bounties done today" }, 3);
        }
    }
}
