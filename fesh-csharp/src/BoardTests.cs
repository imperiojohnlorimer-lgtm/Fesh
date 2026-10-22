#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The harbour board and the Hunger setting (1.45, FESH_BOARD_TEST=1): the same board for the same day, nothing it
    // asks for out of reach or a spoiler, quantities growing with you, a board for a save loaded late and a new one at
    // 06:00 (not midnight), catches and counters counting, paying once (also across a reload), the streak, the Ledger,
    // every board standing clear with its own target, the panel and the journal by real presses and clicks, the guide's
    // goal, and hunger switched off by a real Settings click: no drain, no penalties, no meter, a free rest, food healing.
    IEnumerable<int> BoardScript()
    {
        Note("The harbour board");
        OldCelebrate = false; BoardOff = false;
        state = new State { created = true, look = new Look { name = "Noticeboard" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        celebQueue.Clear(); banner = null;

        string Sig(BoardDay d) => string.Join(" | ", d.list.Select(b => $"{b.kind}:{b.arg}:{b.need}"));

        /* ---------- the roll ---------- */
        Check($"the same day rolls the same board ({Sig(RollBoard(5))})", Sig(RollBoard(5)) == Sig(RollBoard(5)));
        var sigs = Enumerable.Range(1, 20).Select(d => Sig(RollBoard(d))).ToList();
        Check($"days differ ({sigs.Distinct().Count()} different boards in 20 days)", sigs.Distinct().Count() >= 8);
        Check("a new fisher's board has three bounties, a catch first, no kind twice",
            Enumerable.Range(1, 30).All(d => RollBoard(d) is var b && b.list.Count == 3 && b.list[0].kind is "fish" or "kinds" && b.list.Select(x => x.kind).Distinct().Count() == 3));

        // A fisher who has done everything: what can it ask for?
        var rich = new State { created = true, look = new Look { name = "Old hand" } };
        rich.flags.metTomas = true; rich.flags.tideOut = true; rich.flags.dockFixed = true; rich.flags.ended = true;
        foreach (var f in Data.AllCommon) rich.commons[f.Id] = 2;
        rich.perfects = 5; rich.sortRight = 5; rich.lessons = 2; rich.caveDeepest = 4; rich.tally["cooked"] = 3; rich.tally["monsters"] = 3; rich.tally["sold"] = 9;
        rich.inv["boat"] = 1;
        foreach (var k in new[] { "amihan", "habagat", "marilaya", "visitedAtoll" }) rich.hinted[k] = true;
        var keep = state; state = rich; state.charted = null; StartGame(false); yield return 2;
        state.charted = LegacyCharted();
        var seenKinds = new HashSet<string>();
        bool fair = true;
        for (int d = 1; d <= 120; d++)
        {
            state.day = d;
            foreach (var b in RollBoard(d).list)
            {
                seenKinds.Add(b.kind);
                if (b.kind == "species" && (!Data.FishById.TryGetValue(b.arg, out var f) || f.Legend || f.Rare || f.Troll || f.Need != null || f.FullMoon || f.Weather != null || f.Bait != null
                    || (f.Season != null && f.Season != Season) || ClosedSeason(f.Id) || !Data.Spots.Any(s => s.Id != "starwell" && Data.Common[s.Id].Contains(f))))
                { fair = false; Note($"day {d}: asks for {b.arg}"); }
                if (b.kind == "place" && (!Charted(b.arg) || !RegionById.ContainsKey(b.arg))) { fair = false; Note($"day {d}: a place {b.arg}"); }
                if (BountyText(b).Contains("Starwell") || BountyText(b).Contains("Tidemane")) { fair = false; Note($"day {d}: {BountyText(b)}"); }
            }
        }
        Check($"in 120 days nothing out of reach or secret is asked ({string.Join(", ", seenKinds.OrderBy(k => k))})", fair && seenKinds.Count >= 10);
        Check("the old hand is asked for more", RollBoard(3).list.First(b => b.kind is "fish" or "kinds").need >= 4);
        state = keep; StartGame(false); yield return 2;
        Check("a new fisher only for a little", RollBoard(3).list[0].need == 2 && RollBoard(3).list.All(b => b.kind is "fish" or "kinds" or "species" or "place" or "big" or "sell" || b.start >= 0));
        // Only what a new fisher can do: no boat, no cave, no class.
        Check("nothing it can't do yet (no sailing, cave or class)", Enumerable.Range(1, 40).SelectMany(d => RollBoard(d).list).All(b => b.kind is not ("sail" or "monsters" or "lesson" or "sort" or "cook" or "perfect")));

        /* ---------- days ---------- */
        state.board = null; state.day = 7;
        Check("a save loaded late in its day still has today's board", Board().day == 7);
        state.clock = 5 * 60 + 59;
        var before = Sig(Board());
        state.clock = 0; Check("midnight doesn't change it (a night belongs to the day before)", Board().day == 7 && Sig(Board()) == before);
        state.clock = 5 * 60 + 58; Advance(4, quiet: true); yield return 2;
        Check($"06:00 brings a new board (day {state.day})", state.day == 8 && Board().day == 8);
        SetNight(false); ClearSkies();

        /* ---------- catches ---------- */
        var a = Data.Common["lagoon"].First(f => !f.Legend && !f.Rare); var b2 = Data.Common["lagoon"].First(f => !f.Legend && !f.Rare && f.Id != a.Id);
        state.board = new BoardDay { day = state.day, list = new()
        {
            new Bounty { kind = "fish", need = 2, tier = 0 },
            new Bounty { kind = "kinds", need = 2, tier = 1 },
            new Bounty { kind = "species", arg = a.Id, need = 3, tier = 1 },
        } };
        int coins0 = state.coins, bait0 = Has("bait");
        AddCatch(a);
        Check("one fish moves the catch bounties", state.board.list[0].have == 1 && state.board.list[1].have == 1 && state.board.list[2].have == 1 && state.coins == coins0);
        AddCatch(a);
        Check("the same fish twice is still one kind", state.board.list[1].have == 1 && state.board.list[2].have == 2);
        Check($"two fish pay the first ({state.coins - coins0} coins) and queue its banner", state.board.list[0].paid && state.coins - coins0 == 10
            && celebQueue.Any(c => c.Kind == "board"));
        AddCatch(b2);
        Check($"a second kind pays the second (coins +{state.coins - coins0}, bait +{Has("bait") - bait0})", state.board.list[1].paid && state.coins - coins0 == 30 && Has("bait") - bait0 == 2);
        Check($"the first done today starts a run of days ({state.boardStreak})", state.boardStreak == 1 && state.boardLastDay == state.day && state.boardDone == 2);
        CheckLedger(); celebQueue.Clear();   // the Ledger's own pay for a first fish isn't the board's
        int paidCoins = state.coins;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("after a reload the board is kept, half done, and nothing pays twice",
            state.board?.day == state.day && state.board.list[0].paid && !state.board.list[2].paid && state.board.list[2].have == 2 && state.coins == paidCoins);
        AddCatch(a);
        Check("and it carries on from there", state.board.list[2].paid);
        Check("a legend never counts", Data.AllCommon.First(f => f.Legend) is var leg && !BoardCountsLegend(leg));
        int beforeOff = state.boardDone; BoardOff = true; state.board.list[2].paid = false; state.board.list[2].have = 0;
        AddCatch(a); Check("with the board off (the old scripts) nothing counts", state.board.list[2].have == 0 && state.boardDone == beforeOff);
        BoardOff = false;

        /* ---------- counters ---------- */
        state.board = new BoardDay { day = state.day, list = new()
        {
            new Bounty { kind = "perfect", need = 2, tier = 1, start = state.perfects },
            new Bounty { kind = "sell", need = 3, tier = 0, start = Tally("sold") },
            new Bounty { kind = "sail", need = 5, tier = 2, start = state.sailed / MilePx * 10 },
        } };
        state.perfects += 1; boardCheckT = 0; yield return 3;
        Check("a perfect hook counts", state.board.list[0].have == 1 && !state.board.list[0].paid);
        state.perfects += 1; Give(a.Id, 3); Sell(a.Id, 3); boardCheckT = 0; yield return 3;
        Check("the second, and selling Pip three fish, pay both", state.board.list[0].paid && state.board.list[1].paid);
        state.sailed += MilePx * 0.3; boardCheckT = 0; yield return 3;
        Check($"sailing counts in tenths of a mile ({state.board.list[2].have})", state.board.list[2].have == 3 && !state.board.list[2].paid);

        /* ---------- the streak ---------- */
        state.boardStreak = 0; state.boardBest = 0; state.boardLastDay = 0;
        void DoOneOn(int day)
        {
            state.day = day; state.board = null;
            var bd = Board();
            bd.list[0] = new Bounty { kind = "fish", need = 1, tier = 0 };
            AddCatch(a);
        }
        DoOneOn(20); DoOneOn(21); DoOneOn(22);
        Check($"three days running is a run of three ({state.boardStreak})", state.boardStreak == 3 && state.boardBest == 3);
        state.day = 24; state.board = null; Board();
        Check("a day missed breaks it when the next board goes up", state.boardStreak == 0 && state.boardBest == 3);
        DoOneOn(24);
        Check("and it starts again", state.boardStreak == 1 && state.boardBest == 3);
        // Codex's review: a counter bounty finished in the last second before dawn still pays, for its own day.
        state.day = 30; state.board = null; Board();
        state.board.list[1] = new Bounty { kind = "sell", need = 1, tier = 0, start = Tally("sold") };
        state.boardLastDay = 29; state.boardStreak = 2;
        int c30 = state.coins; boardCheckT = 99; Tick("sold");
        state.clock = 5 * 60 + 59; Advance(2, quiet: true); Board();
        Check($"a counter bounty done just before dawn still pays and credits its day ({state.coins - c30} coins, last day {state.boardLastDay}, run {state.boardStreak})",
            state.day == 31 && state.coins - c30 == 10 && state.boardLastDay == 30 && state.boardStreak == 3);
        // Days slept through break the run, through the real clock.
        Advance(1440 * 2, quiet: true); Board();
        Check($"sleeping through two whole days breaks the run (day {state.day})", state.day == 33 && state.boardStreak == 0);
        SetNight(false);
        // A catch that pays saves a moment later, once whatever landed it (a pot, the crab tray) is done.
        state.board.list[0] = new Bounty { kind = "fish", need = 1, tier = 0 };
        AddCatch(a);
        bool due = boardSaveDue; yield return 3;
        Check("a catch that pays a bounty saves on the next frame, not in the middle of landing it", due && !boardSaveDue);
        CheckLedger();
        Check("the Ledger counts bounties and runs", AchEarned(state, Achievements.First(x => x.Id == "board")) && AchEarned(state, Achievements.First(x => x.Id == "streak3")));
        celebQueue.Clear(); banner = null;

        /* ---------- the boards in the world ---------- */
        LoadScene("world");
        bool clear = true;
        for (int i = 0; i < HarbourBoards.Length; i++)
        {
            var (id, name, bx, by) = HarbourBoards[i];
            var box = BoardBox(bx, by);
            var others = Solids().Where(s => s != box).ToList();
            bool ground = Enumerable.Range(0, 3).All(k => Walkable(TileAt((int)MathF.Floor((box.X + k * 8.5f) / T), (int)MathF.Floor((box.Y + 1) / T))));
            bool free = !others.Any(s => s.Overlaps(new Box(box.X - 1, box.Y - 1, box.W + 2, box.H + 2)));
            bool stand = CanStand(bx, by + 8);
            player.X = bx; player.Y = by + 8; player.Face = "up"; state.riding = false; state.aboard = false;
            yield return 3;
            bool mine = target?.Type == "board";
            if (!(ground && free && stand && mine)) { clear = false; Note($"{id}: ground {ground}, clear {free}, stand {stand}, target {target?.Type}:{target?.Label}"); }
            if (i == 0 || i == 1) { pendingShot = "b0" + (i + 1) + "-" + id; yield return 4; }
        }
        Check("every board stands on open ground, clear of everything, with its own target in front", clear);
        // The user's report: a board in front of Tomas's campfire (and Lira's door). Every board keeps well clear of
        // everything people walk up to, and its drawing clear of buildings.
        var allSolids = Solids();
        var far = HarbourBoards.Select((b, i) => (b, ok: BoardSpotOk(b.x, b.y, allSolids.Where(s => s != BoardBox(b.x, b.y)).ToList()))).ToList();
        foreach (var (b, ok) in far.Where(f => !f.ok))
        {
            // Where it could go instead (for whoever moves it): the nearest spot that passes.
            var sol = allSolids.Where(s => s != BoardBox(b.x, b.y)).ToList();
            var (cxp, cyp) = b.id switch { "saltmere" => (236f, 80f), "amihan" => (1575f, 230f), _ => (b.x, b.y) };
            (float x, float y)? best = null; float bd = 1e9f;
            for (float dy = -90; dy <= 90; dy += 2)
                for (float dx = -90; dx <= 90; dx += 2)
                    if (dx * dx + dy * dy < bd && BoardSpotOk(cxp + dx, cyp + dy, sol)) { bd = dx * dx + dy * dy; best = (cxp + dx, cyp + dy); }
            Note($"{b.id} at {b.x},{b.y} is too close to something; nearest clear spot {best}");
        }
        Check("every board keeps clear of campfires, doors, people, landings and fishing spots", far.All(f => f.ok));
        player.X = PipX; player.Y = PipY + 15; player.Face = "up"; yield return 3;
        Check("Pip's stall still trades beside it", target?.Type == "pip");
        // Codex's review: a board an old save built over is gone, from the guide and the chart too.
        state.hinted["amihan"] = true;
        var over = new Build { id = "shack", x = (int)(HarbourBoards[0].x / T) - 1, y = (int)(HarbourBoards[0].y / T) - 1 };
        state.builds.Add(over);
        bool gone = !BoardStands(0) && NearestBoard() is var nb && nb != null && nb.Value.name != "Saltmere";
        var marks = new List<string>(); BoardChartMarks((l, bx, by, g, tip, p) => marks.Add(tip[0]));
        Check($"a board built over in an old save isn't pointed at or charted ({string.Join(", ", marks)})", gone && !marks.Contains("Harbour board (Saltmere)") && marks.Contains("Harbour board (Amihan Village)"));
        state.builds.Remove(over);

        /* ---------- the panel ---------- */
        state.board = null; state.boardSeen = 0; Board();
        Goals();
        var g0 = Goals().FirstOrDefault(g => g.Id == "board:today");
        Check("the journal has today's board, pointing at the nearest one until it's read", g0 != null && g0.Group == "daily" && g0.HasPoint);
        player.X = HarbourBoards[0].x; player.Y = HarbourBoards[0].y + 8; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("E at the board opens it", mode == "panel" && panel == "board");
        lastBoardBottom = 0; yield return 2;
        pendingShot = "b03-panel"; yield return 4;
        Check($"the panel fits ({lastBoardBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastBoardBottom > 0 && lastBoardBottom <= panelAt.y + panelAt.h - 12);
        var g1 = Goals().FirstOrDefault(g => g.Id == "board:today");
        Check("once read, the goal stops pointing", g1 != null && !g1.HasPoint);
        Settings.Data.clearFont = true; lastBoardBottom = 0; yield return 3;
        Check($"and fits in the Clear font ({lastBoardBottom:0})", lastBoardBottom > 0 && lastBoardBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = false;
        ClickButton("Close"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("Close closes it", mode == "play");
        Inp.Tap(KeyboardKey.Q); yield return 4;
        ClickButton("Harbour board"); yield return 4; Inp.ScriptMouse = Offscreen;
        Check("the journal's Harbour board button opens it from anywhere", mode == "panel" && panel == "board");
        ClosePanels(); yield return 2;

        /* ---------- the banner ---------- */
        celebQueue.Clear(); banner = null; toastTimer = 0;
        state.board.list[0] = new Bounty { kind = "fish", need = 1, tier = 0 };
        AddCatch(a);
        for (int i = 0; i < 120 && banner?.Kind != "board"; i++) { if (toastTimer > 0 && badgeNews == null) toastTimer = 0; yield return 1; }
        pendingShot = "b04-banner"; yield return 4;
        Check($"a bounty done shows its banner ({banner?.Title})", banner?.Kind == "board");
        banner = null; celebQueue.Clear();

        /* ---------- hunger ---------- */
        Note("Hunger setting");
        OpenPause(); menuTab = "settings"; yield return 4;
        pendingShot = "b05-settings"; yield return 4;
        ClickButton("Off"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the Settings tab's Hunger Off button switches it off (and saves it)", !Settings.Data.hunger && File.ReadAllText(SaveFile.SettingsPath).Contains("\"hunger\": false"));
        ClosePause(); yield return 3;
        state.food = 50; float t0 = time;
        while (time - t0 < 2) yield return 1;
        Check($"no food goes in play ({state.food:0.0})", state.food == 50);
        state.food = 0;
        Check("an empty meter isn't starving: no slow walk, no smaller reel zone", !Starving);
        state.hp = 40; float h0 = state.hp; lastHitT = -100;
        t0 = time; while (time - t0 < 1.5f) yield return 1;
        Check($"health still comes back with nothing eaten ({h0:0} -> {state.hp:0.0})", state.hp > h0);
        pendingShot = "b06-hud-nohunger"; yield return 4;
        state.hp = 40; Give("grilled_fish"); Eat("grilled_fish");
        Check($"eating still heals ({state.hp:0}) and moves no meter ({state.food:0})", state.hp > 40 && state.food == 0);
        state.food = 30; Rest(); for (int i = 0; i < 200 && fade != null; i++) yield return 1; yield return 3;
        Check($"a rest costs no food ({state.food:0})", state.food == 30);
        OpenPause(); menuTab = "settings"; yield return 4;
        ClickButton("On"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2; ClosePause(); yield return 3;
        Check("switched back on it carries on from what you had", Settings.Data.hunger && state.food > 29.9f);
        t0 = time; while (time - t0 < 2) yield return 1;
        Check($"and the meter drops again ({state.food:0.0})", state.food < 30);
        // An old settings file has no hunger in it: hunger stays on.
        File.WriteAllText(SaveFile.SettingsPath, "{ \"music\": 0.5, \"guide\": true }");
        Settings.Load();
        Check("an older settings file keeps hunger on", Settings.Data.hunger);
        Settings.Data.pauseUnfocused = false; Settings.Data.dayLength = 0;

        /* ---------- the user's two layout bugs (1.45) ---------- */
        Note("Matches name row and Case board tabs");
        Settings.Data.zoom = 4;
        foreach (bool clearFont in new[] { false, true })
        {
            Settings.Data.clearFont = clearFont;
            ClosePanels(); mode = "play"; yield return 2;
            TogglePanel("matches"); matchesTab = "play"; yield return 4;
            pendingShot = "b07-matches" + (clearFont ? "-clear" : ""); yield return 4;
            Check($"Matches: the name picker stays clear of the tabs ({(clearFont ? "Clear" : "Pixel")}: tabs end {lastMatchTabsRight:0}, name starts {lastNameLeft:0})",
                lastMatchTabsRight > 0 && lastNameLeft >= lastMatchTabsRight + 8);
            ClosePanels(); yield return 2;
            // Every story open, so all five tabs carry their names.
            foreach (var k in new[] { "habagat", "rs:tremor", "metMila", "mg:met", "marilaya", "mr:met" }) state.hinted[k] = true;
            TogglePanel("case"); yield return 4;
            pendingShot = "b08-case" + (clearFont ? "-clear" : ""); yield return 4;
            float closeX = CaseBx + CaseBorder + (CaseBw - CaseBorder * 2) - CasePadX - SmallW("Close");
            Check($"Case board: five tabs end before Close with readable names ({(clearFont ? "Clear" : "Pixel")}: right {lastCaseTabRight:0} of {closeX:0}, {lastCaseTabFs} px)",
                lastCaseTabRight > 0 && lastCaseTabRight <= closeX - 8 && lastCaseTabFs >= 12);
            ClosePanels(); yield return 2;
        }
        Settings.Data.clearFont = false;

        BoardOff = true; OldCelebrate = true;
        yield return 2;
    }

    bool BoardCountsLegend(CommonFish leg)
    {
        var b = new Bounty { kind = "fish", need = 99 };
        var keep = state.board;
        state.board = new BoardDay { day = state.day, list = new() { b } };
        BoardCatch(leg, 1);
        state.board = keep;
        return b.have > 0;
    }
}
#endif
