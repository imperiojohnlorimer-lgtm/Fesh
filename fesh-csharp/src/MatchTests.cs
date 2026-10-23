#if DEBUG
using System.Text.Json;
using Raylib_cs;

namespace Fesh;

partial class Game
{
    static readonly JsonSerializerOptions JsonAll = new() { IncludeFields = true };

    // Everything that makes up the adventure, except the two fields that are meant to move (time played, save stamp).
    string AdventureJson()
    {
        var copy = JsonSerializer.Deserialize<State>(JsonSerializer.Serialize(state, JsonAll), JsonAll);
        copy.playSecs = 0; copy.savedAt = null; copy.food = 0;   // hunger is meant to move on the island
        copy.coins = 0; copy.dailyDays = new(); copy.inv.Remove("glow_bait"); foreach (var k in copy.gifts.Keys.Where(k => k.StartsWith("duel_") || k.StartsWith("daily_")).ToList()) copy.gifts.Remove(k);   // and Pip pays for a Duel won
        return JsonSerializer.Serialize(copy, JsonAll);
    }

    void MatchTestGame(string name, int x = 160, int y = 115)
    {
        state = new State { created = true, look = new Look { name = name } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Inp.ScriptMouse = Offscreen;
        player.X = x; player.Y = y;
    }

    // Matches (1.30): the rules, the leaderboard file, the sandbox that keeps an adventure safe, and a whole duel and
    // daily challenge played by real clicks and keys. FESH_MATCH_TEST=1 runs only this; it is also in the full run.
    IEnumerable<int> MatchScript()
    {
        Note("Matches: the rules");
        // --- The seeded rules, with no game running ---
        var plan0 = MatchRules.Plan(12345, 0);
        Check("a cast's plan repeats exactly for the same seed and cast", MatchRules.Plan(12345, 0) == plan0 && MatchRules.Plan(12345, 7) == MatchRules.Plan(12345, 7));
        Check("another seed or another cast is a different plan somewhere", Enumerable.Range(0, 30).Any(i => MatchRules.Plan(12345, i) != MatchRules.Plan(54321, i)) && Enumerable.Range(1, 30).Any(i => MatchRules.Plan(12345, i) != plan0));
        Note($"golden: Plan(12345,0) = {plan0.FishId} size {plan0.Size:0.0000} wait {plan0.Wait:0.0000}; Plan(7,3) = {MatchRules.Plan(7, 3)}; Mix(1,2,3) = {MatchRules.Mix(1, 2, 3)}; daily 2026-10-13 = {MatchRules.DailySeed(new DateTime(2026, 10, 13))}");
        var counts = new Dictionary<string, int>();
        double minSize = 9, maxSize = 0, minWait = 9, maxWait = 0;
        for (int i = 0; i < 20000; i++)
        {
            var p = MatchRules.Plan(99, i);
            counts[p.FishId] = counts.GetValueOrDefault(p.FishId) + 1;
            minSize = Math.Min(minSize, p.Size); maxSize = Math.Max(maxSize, p.Size); minWait = Math.Min(minWait, p.Wait); maxWait = Math.Max(maxWait, p.Wait);
        }
        int totalW = MatchRules.Pool.Sum(p => p.w);
        bool shares = MatchRules.Pool.All(p => Math.Abs(counts.GetValueOrDefault(p.id) / 20000.0 - p.w / (double)totalW) < 0.015);
        Check("20000 casts give each fish about its share of the pool", shares && counts.Count == MatchRules.Pool.Length);
        Check($"sizes stay between 0.55 and 1.70 ({minSize:0.00}-{maxSize:0.00}) and waits between 1.6 and 3.6 s ({minWait:0.00}-{maxWait:0.00})", minSize >= 0.55 && maxSize <= 1.701 && minWait >= 1.6 && maxWait <= 3.6);
        Check("every pool fish is an everyday, non-attacking fish (nothing rare, legendary or fighting back)",
            MatchRules.Pool.All(p => Data.FishById[p.id] is { Rare: false, Legend: false, Attack: 0, Troll: false }));
        Check("a perfect hook makes the same fish a tenth heavier", MatchRules.Centi("mud_carp", 1.0, true) == (int)Math.Round(Data.FishById["mud_carp"].Kg * 110.0) && MatchRules.Centi("mud_carp", 1.0, false) == (int)Math.Round(Data.FishById["mud_carp"].Kg * 100.0));
        Check("the daily seed is the same all day and different the next", MatchRules.DailySeed(new DateTime(2026, 10, 13, 0, 1, 0)) == MatchRules.DailySeed(new DateTime(2026, 10, 13, 23, 59, 0)) && MatchRules.DailySeed(new DateTime(2026, 10, 13)) != MatchRules.DailySeed(new DateTime(2026, 10, 14)));
        Check("its label is the ISO date", MatchRules.DailyLabel(new DateTime(2026, 1, 5)) == "2026-01-05");

        // --- Deciding a winner ---
        MatchScore S(int cg, int perfects) => new() { Cg = cg, Perfects = perfects };
        Check("the heavier total wins, both ways round", MatchRules.Decide(S(500, 0), S(400, 9)).winner == 1 && MatchRules.Decide(S(400, 9), S(500, 0)).winner == -1);
        Check("equal weight goes to more perfect hooks", MatchRules.Decide(S(500, 3), S(500, 2)).winner == 1 && MatchRules.Decide(S(500, 2), S(500, 3)).winner == -1);
        Check("equal in both is a draw, and says so", MatchRules.Decide(S(500, 2), S(500, 2)) is (0, "Exactly level"));

        // --- The rivals ---
        var ghostA = MatchRules.GhostRun(777, MatchRules.RivalById("marlo"), 180);
        var ghostB = MatchRules.GhostRun(777, MatchRules.RivalById("marlo"), 180);
        Check("a rival fishes the same match every time", ghostA.Count == ghostB.Count && ghostA.Zip(ghostB).All(p => p.First.t == p.Second.t && p.First.cg == p.Second.cg && p.First.fish == p.Second.fish && p.First.perfect == p.Second.perfect));
        Check("their catches are in time order and inside the three minutes", ghostA.Count > 0 && ghostA.Zip(ghostA.Skip(1)).All(p => p.First.t < p.Second.t) && ghostA.All(e => e.t > 0 && e.t < 180));
        double Avg(string id) => Enumerable.Range(0, 300).Average(s => MatchRules.GhostRun(s * 7 + 1, MatchRules.RivalById(id), 180).Sum(e => e.cg)) / 100.0;
        double bess = Avg("bess"), marlo = Avg("marlo"), ivy = Avg("ivy");
        Note($"rivals' average kg over 300 seeds: Old Bess {bess:0.0}, Marlo {marlo:0.0}, Captain Ivy {ivy:0.0}");
        Check("Old Bess is the weakest and Captain Ivy the strongest", bess < marlo && marlo < ivy && bess > 4 && ivy < 60);
        // Their fish are the casts' own fish, and a landed one weighs what the same cast would weigh for you.
        bool sameCasts = true;
        foreach (var e in ghostA)
        {
            // Some cast's plan gave this fish, at this size, with or without a perfect hook.
            bool ok = Enumerable.Range(0, 60).Any(n => { var p = MatchRules.Plan(777, n); return p.FishId == e.fish && MatchRules.Centi(p.FishId, p.Size, e.perfect) == e.cg; });
            sameCasts &= ok;
        }
        Check("every fish a rival lands is one of the seed's own casts, at that cast's size", sameCasts);
        var sc = MatchRules.ScoreAt(ghostA, 90);
        Check("a rival's running total counts only what they've landed by then", sc.Cg == ghostA.Where(e => e.t <= 90).Sum(e => e.cg) && MatchRules.ScoreAt(ghostA, 0).Cg == 0 && MatchRules.ScoreAt(ghostA, 180).Cg == ghostA.Sum(e => e.cg));

        // --- The leaderboard file ---
        Note("Matches: the leaderboard file");
        string boardPath = SaveFile.BoardPath;
        Check($"the board sits beside the settings, under the test prefix ({Path.GetFileName(boardPath)})", Path.GetFileName(boardPath).EndsWith("-leaderboard.json") && Path.GetDirectoryName(boardPath) == Path.GetDirectoryName(SaveFile.SettingsPath));
        if (File.Exists(boardPath)) File.Delete(boardPath);
        Leaderboards.Reset();
        var b0 = Leaderboards.Board();
        string id0 = b0.player;
        Check("a player id and a name from the lists are made once (no free text)", id0.Length == 32 && b0.name.Split(' ').Length >= 2 && Leaderboards.NameChoices >= 100);
        BoardEntry E(string id, string mode, int seed, int score, int perfects, string result = "win", string date = null, bool log = true) => new()
        {
            id = id, mode = mode, seed = seed, ruleset = MatchRules.Version, score = score, perfects = perfects, catches = 3, casts = 5, result = result, rival = "Marlo",
            date = date ?? DateTime.Now.ToString("o"), label = "t", log = log ? new() { new MatchEvent { t = 5, fish = "pond_perch", cg = score, perfect = false } } : null
        };
        Check("a result is added once and a repeat of the same match id is ignored", Leaderboards.Add(E("a", "derby", 1, 500, 1)) && !Leaderboards.Add(E("a", "derby", 1, 999, 9)) && Leaderboards.Board().entries.Count == 1);
        Leaderboards.Add(E("b", "derby", 2, 800, 0)); Leaderboards.Add(E("c", "derby", 3, 800, 2)); Leaderboards.Add(E("d", "derby", 4, 300, 0, "loss"));
        Leaderboards.Add(E("e", "derby", 5, 9999, 0, "forfeit"));
        var best = Leaderboards.Best("derby", 10);
        Check("best runs run heaviest first, ties to more perfect hooks, and leavers are left out", best.Select(x => x.id).SequenceEqual(new[] { "c", "b", "a", "d" }));
        Check("the record counts wins, losses and draws, with a forfeit as a loss", Leaderboards.Record("derby") == (5, 3, 2, 0));
        Leaderboards.Add(E("f", "daily", 20261013, 700, 1, "solo")); Leaderboards.Add(E("g", "daily", 20261013, 900, 0, "solo")); Leaderboards.Add(E("h", "daily", 20261014, 100, 0, "solo"));
        Check("your best run on one seed is found, with its fish log", Leaderboards.BestOn("daily", 20261013) is { id: "g", log: not null } && Leaderboards.BestOn("daily", 5) == null);
        Check("the other mode's board is separate", Leaderboards.Best("daily", 10).Count == 3 && Leaderboards.Best("derby", 10).All(x => x.mode == "derby"));
        Check("no half-written temporary file is left behind", !File.Exists(boardPath + ".tmp") && File.Exists(boardPath));
        Leaderboards.Reset();
        Check("it all survives a reload from the file", Leaderboards.Board().entries.Count == 8 && Leaderboards.Board().player == id0 && Leaderboards.BestOn("daily", 20261013)?.id == "g");
        // Old runs lose their fish logs unless they're a best; a long history is trimmed to the newest.
        for (int i = 0; i < Leaderboards.Keep + 40; i++) Leaderboards.Add(E($"x{i}", "derby", 1000 + i, 10 + i, 0, "loss", new DateTime(2026, 1, 1).AddMinutes(i).ToString("o")));
        var after = Leaderboards.Board().entries;
        Check($"the file is trimmed ({after.Count} entries) but the ten best of every kind are kept", after.Count <= Leaderboards.Keep + 20 && after.Any(x => x.id == "c") && after.Any(x => x.id == "g"));
        Check("a best run keeps its fish log; an old, beaten one doesn't", after.First(x => x.id == "g").log != null && after.First(x => x.id == "f").log == null);
        // A damaged file is set aside and the board starts again; the game carries on.
        File.WriteAllText(boardPath, "{ this is not json");
        Leaderboards.Reset();
        var broken = Leaderboards.Board();
        Check("a damaged leaderboard file is set aside (.bad) and the board starts empty, with a new id", broken.entries.Count == 0 && File.Exists(boardPath + ".bad") && broken.player.Length == 32);
        File.Delete(boardPath + ".bad");
        Leaderboards.SetName(3, 5);
        Check("the name can be changed from the lists only, and is saved", Leaderboards.Board().name == Leaderboards.Name(Leaderboards.Board()) && File.ReadAllText(boardPath).Contains(Leaderboards.Board().name));
        File.Delete(boardPath); Leaderboards.Reset();
        yield return 1;

        // --- A match in the game: the adventure stays safe ---
        Note("Matches: a Derby Duel by real clicks");
        MatchTestGame("Matcher");
        state.coins = 777; Give("rod_copper"); Give("bait", 5); state.xp = 900; state.day = 4;
        yield return 4;
        Save();
        string realJson = AdventureJson();
        var realState = state; var realRng = rng;
        var diskBytes = File.ReadAllBytes(SaveFile.FilePath);
        var diskTime = File.GetLastWriteTimeUtc(SaveFile.FilePath);
        float realX = player.X, realY = player.Y;
        Check("a match can start from the middle of Saltmere", MatchBlockReason() == null);

        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc opens the menu", mode == "pause");
        pendingShot = "match-esc-menu"; yield return 2;
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the Esc menu's Matches button opens the Matches panel", mode == "panel" && panel == "matches");
        ClickButton("Close"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("and Close puts you back", mode == "play" && panel == null);
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the Matches button in the HUD opens the panel", mode == "panel" && panel == "matches");
        pendingShot = "match-panel-play"; yield return 2;
        ClickButton("rival:ivy"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("clicking a rival picks them", matchRival == "ivy");
        ClickButton("Leaderboards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the Leaderboards tab opens, empty to begin with", matchesTab == "board");
        pendingShot = "match-panel-board"; yield return 2;
        ClickButton("Play"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("and Play goes back", matchesTab == "play");
        ClickButton("Start the duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Start the duel begins a fade", mode == "fade" || MatchActive);
        for (int i = 0; i < 200 && !(MatchActive && mode == "play" && match.Phase == "countdown"); i++) yield return 1;
        Check($"the match is on, counting down (match {(MatchActive ? match.Phase : "none")}, mode {mode}, block {MatchBlockReason(true)})", MatchActive && match.Phase == "countdown" && mode == "play");
        Check("it is a Derby Duel against Captain Ivy with the three minutes", match.Spec.Mode == "derby" && match.Spec.Rival == "ivy" && match.Spec.Duration == 180 && match.RivalName == "Captain Ivy" && match.RivalRun != null);
        Check("the game now runs on a tournament state: the Old rod and nothing else", !ReferenceEquals(state, realState) && state.inv.Count == 1 && Has("rod_old") == 1 && state.coins == 0 && FishLevel == 1);
        Check("noon, fair weather, and you at the lagoon", state.clock == 720 && state.weather == "clear" && MathF.Abs(player.X - MatchX) < 0.01 && MathF.Abs(player.Y - MatchY) < 0.01 && scene == "world");
        realJson = AdventureJson_Of(realState); diskBytes = File.ReadAllBytes(SaveFile.FilePath); diskTime = File.GetLastWriteTimeUtc(SaveFile.FilePath);   // the adventure is frozen from here
        Check("the adventure's state, random numbers and place are kept aside", ReferenceEquals(match.Real, realState) && ReferenceEquals(match.RealRng, realRng) && realState.coins == 777 && realState.inv.GetValueOrDefault("rod_copper") == 1);
        Check("the target is the lagoon, once the countdown ends", target == null);
        // Keys that would do other things do nothing.
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("pressing E during the countdown casts nothing", mode == "play" && fish == null);
        Inp.Tap(KeyboardKey.B); yield return 3;
        Inp.Tap(KeyboardKey.M); yield return 3;
        Inp.Tap(KeyboardKey.Q); yield return 3;
        Check("the bag, map and journal keys do nothing in a match", mode == "play" && panel == null);
        Save();
        Check("Save writes nothing while the match is on", File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes) && File.GetLastWriteTimeUtc(SaveFile.FilePath) == diskTime);
        pendingShot = "match-countdown"; yield return 2;
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        Check("the countdown ends and fishing starts at zero", match.Phase == "run" && match.T < 1);
        Inp.Hold(KeyboardKey.W, true); yield return 20; Inp.Hold(KeyboardKey.W, false);
        Check("walking does nothing: you stay where you stand", MathF.Abs(player.X - MatchX) < 0.01 && MathF.Abs(player.Y - MatchY) < 0.01);
        yield return 3;
        Check("the lagoon is now the thing to act on", target?.Type == "spot" && target.Id == "lagoon" && target.AltType == null);
        Check("the clock stood still at noon", state.clock == 720);

        // Cast 0: its bite time and its fish are the seed's.
        int seed = match.Spec.Seed;
        var p0 = MatchRules.Plan(seed, 0);
        Inp.Tap(KeyboardKey.E); yield return 3;
        for (int i = 0; i < 100 && mode is "charging" or "play"; i++) yield return 1;
        Check($"E casts, and it is cast number 0 (mode {mode})", fish?.MatchN == 0 && mode is "casting" or "waiting");
        Check($"the wait is the seed's {p0.Wait:0.00} s (the Old rod, fair weather)", MathF.Abs(fish.Timer + fish.WaitT - p0.Wait) < 0.2f);
        for (int i = 0; i < 900 && mode != "bite"; i++) yield return 1;
        Check($"it bites, and the fish is the seed's {p0.FishId}", mode == "bite" && fish.Roll.Id == p0.FishId);
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("hooking it in time starts the fight", mode == "reeling" && reel != null);
        bool perfect0 = reel.Perfect;
        // The fight is the same at any frame rate: one second of it, in different slices, ends in the same place.
        var snap = reel.Roll;
        var states = new List<(float y, float prog, float age)>();
        foreach (int perSecond in new[] { 30, 60, 144, 20 })
        {
            fish.MatchN = 0; MatchSeedFight();
            reel = new ReelState { ZoneH = 1, ZoneY = Bar - 1, FishY = 70, FishTarget = 50, FishTimer = 0.3f, Progress = 0.6f, Diff = 1f, Roll = snap, Style = "dart", RunT = 2f, LeapT = 2f, LeapLen = 1f };
            mode = "reeling"; match.ReelAcc = 0;
            // Two seconds of play in frames of this size (and a hair over, so no rounding drops a step).
            for (int i = 0; i < 2 * perSecond; i++) StepMatchReel(1f / perSecond);
            StepMatchReel(0.002f);
            if (reel != null) states.Add((reel.FishY, reel.Progress, reel.Age));
            reel = null;
        }
        Check($"two seconds of fight end in the same place at 20, 30, 60 and 144 frames a second ({string.Join(" / ", states.Select(s => $"{s.y:0.00}@{s.age:0.000}"))})", states.Count == 4 && states.All(s => MathF.Abs(s.y - states[0].y) < 0.01f && MathF.Abs(s.prog - states[0].prog) < 0.01f && MathF.Abs(s.age - states[0].age) < 0.001f));
        // Back to the real fight on the line, then land it.
        fish.Roll = snap; fish.MatchN = 0;
        reel = new ReelState { ZoneH = 34, ZoneY = Bar - 34, FishY = 70, FishTarget = 50, FishTimer = 0.3f, Progress = 1.5f, Diff = snap.Difficulty, Roll = snap, Perfect = perfect0, Style = "dart", LeapLen = 1 };
        mode = "reeling";
        yield return 3;
        int want = MatchRules.Centi(p0.FishId, p0.Size, perfect0);
        Check($"landing it scores the seed's weight ({want / 100.0:0.00} kg){(perfect0 ? ", perfect" : "")}", match.Me.Cg == want && match.Me.Catches == 1 && match.Me.Perfects == (perfect0 ? 1 : 0) && match.Log.Count == 1 && match.Log[0].fish == p0.FishId);
        Check("nothing from the adventure was touched by the catch: no bag, XP or records", state.inv.Count == 1 && state.xp == 0 && state.commons.Count == 0 && state.records.Count == 0);
        Check("the fish is held up and the fight is over", mode == "play" && fish == null && reel == null && heldItem == p0.FishId);
        pendingShot = "match-running"; yield return 2;
        // A fish lost counts as lost and scores nothing.
        int lostBefore = match.Lost, cgBefore = match.Me.Cg;
        TestBite("lagoon", MatchRules.Plan(seed, 1).FishId); fish.MatchN = 1; match.Casts = 2;
        fish.BiteT = 0.01f; yield return 5;
        Check("a bite let go too late is a lost fish and scores nothing", match.Lost == lostBefore + 1 && match.Me.Cg == cgBefore && fish == null);

        // Esc: asks, and the match goes on while it's asked.
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        float t1 = match.T;
        Check("Esc asks whether to leave", match.LeaveAsk && mode == "play");
        pendingShot = "match-leave"; yield return 2;
        yield return 20;
        Check("the clock keeps running behind the question", match.T > t1 + 0.1f);
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("nothing can be cast with the question up", fish == null);
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Check("Esc again keeps playing", !match.LeaveAsk);
        // Losing the window's focus doesn't pause it.
        Settings.Data.pauseUnfocused = true; Inp.ScriptFocused = false; float t2 = match.T; yield return 20;
        Check("losing focus does not pause a match", mode == "play" && match.T > t2 + 0.1f);
        Inp.ScriptFocused = true; Settings.Data.pauseUnfocused = false;
        Check("Settings' day length was never touched", Settings.Data.dayLength == 0);

        // The whistle: a fish still on the line when time runs out doesn't count.
        TestBite("lagoon", MatchRules.Plan(seed, 2).FishId); fish.MatchN = 2; match.Casts = 3;
        reel = new ReelState { ZoneH = 34, ZoneY = Bar - 34, FishY = 70, Progress = 0.6f, Roll = fish.Roll, Perfect = true, LeapLen = 1 };
        mode = "reeling";
        // A rival who is clearly ahead.
        match.RivalRun = new() { new MatchEvent { t = 50, fish = "silver_tuna", cg = 90000, perfect = true } };
        match.T = match.Spec.Duration - 0.05f;
        yield return 8;
        Check("at the whistle the result card shows", mode == "matchresult" && match.Phase == "done" && match.Result != null);
        Check("the fish still on the line was dropped, uncounted", fish == null && reel == null && match.Me.Cg == want);
        Check("the heavier rival wins and the reason says why", match.Result.Result == "loss" && match.Result.Reason == "Heavier total catch" && match.Result.Title == "Captain Ivy wins");
        pendingShot = "match-result"; yield return 2;
        fish = new FishCast { Spot = "lagoon", MatchN = 3, Roll = new Catchable { Id = "mud_carp", Name = "Mud carp" } }; reel = new ReelState { Roll = fish.Roll, Progress = 1, Perfect = true };
        MatchLand();   // a landing after the whistle changes nothing
        Check("a catch landed after the whistle is not counted", match.Me.Cg == want);
        Check("the result was written to the leaderboard exactly once", Leaderboards.Board().entries.Count(x => x.id == match.Spec.Id) == 1 && Leaderboards.Record("derby") == (1, 0, 1, 0));
        Check("a duel keeps no fish log (only the daily challenge needs one, for its ghost)", Leaderboards.Board().entries.First(x => x.id == match.Spec.Id).log == null);
        Save();
        Check("Save still writes nothing on the result card", File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        // Rematch by the real button.
        int oldSeed = match.Spec.Seed; string oldId = match.Spec.Id;
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Rematch starts a new countdown on a new seed and a new id, with the same rival", mode == "play" && match.Phase == "countdown" && match.Spec.Seed != oldSeed && match.Spec.Id != oldId && match.Spec.Rival == "ivy");
        Check("and clears the score", match.Me.Cg == 0 && match.Log.Count == 0 && match.Casts == 0 && fish == null);
        // Win, draw and the perfect-hook tie-break through the real end.
        foreach (var (mine, myP, theirs, theirP, expect) in new[] { (900, 0, 500, 3, "win"), (500, 2, 500, 2, "draw"), (500, 3, 500, 1, "win"), (500, 1, 500, 3, "loss") })
        {
            for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
            match.Me = new MatchScore { Cg = mine, Perfects = myP };
            match.RivalRun = new() { new MatchEvent { t = 10, fish = "pond_perch", cg = theirs, perfect = theirP > 0 } };
            // The rival's perfect count comes from the events: give them theirP events summing to theirs.
            match.RivalRun = Enumerable.Range(0, Math.Max(1, theirP)).Select(k => new MatchEvent { t = 10 + k, fish = "pond_perch", cg = theirs / Math.Max(1, theirP) + (k == 0 ? theirs % Math.Max(1, theirP) : 0), perfect = k < theirP }).ToList();
            match.T = match.Spec.Duration - 0.05f; yield return 8;
            Check($"{mine}cg/{myP} perfect against {theirs}cg/{theirP} is a {expect}", mode == "matchresult" && match.Result.Result == expect);
            if (expect == "draw") Check("a draw says it is exactly level", match.Result.Reason == "Exactly level" && match.Result.Title == "A draw!");
            ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        }
        // Leave by the real button: everything is put back.
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("leaving puts you back on the island", !MatchActive && mode == "play");
        Check("your state is the very same object, unchanged", ReferenceEquals(state, realState) && AdventureJson() == realJson && state.coins >= 777 && Has("rod_copper") == 1);
        Check("the adventure's random numbers are back", ReferenceEquals(rng, realRng));
        Check("and you stand where you were, facing as before", MathF.Abs(player.X - realX) < 0.01 && MathF.Abs(player.Y - realY) < 0.01);
        Check("the save on disk was not touched by any of it", File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        Check("leaving a run counted as a forfeit, and a forfeit isn't in the best runs", Leaderboards.Board().entries.Any(x => x.result == "forfeit") && Leaderboards.Best("derby", 20).All(x => x.result != "forfeit"));
        Save();
        Check("saving works again", !File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes) || File.GetLastWriteTimeUtc(SaveFile.FilePath) >= diskTime);

        // --- Where a match can't start ---
        Note("Matches: where it can't start");
        Check("it can start again", MatchBlockReason() == null);
        derbyT = 60; Check("not during Pip's derby", MatchBlockReason() != null); derbyT = 0;
        LoadScene("house:tomas"); Check("not indoors", MatchBlockReason() != null); LoadScene("world"); player.X = realX; player.Y = realY;
        Give("boat"); state.aboard = true; Check("not at the helm of the boat", MatchBlockReason() != null); state.aboard = false; state.inv.Remove("boat");
        state.tamed = true; state.riding = true; Check("not on Tidemane", MatchBlockReason() != null); state.riding = false; state.tamed = false;
        mode = "bite"; Check("not while fishing", MatchBlockReason() != null); mode = "play";
        Check("and it is allowed again after all that", MatchBlockReason() == null);

        // --- The daily challenge ---
        Note("Matches: the Daily Challenge");
        int dailySeed = MatchRules.DailySeed(DateTime.Now);
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Play today's run"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && mode == "play"); i++) yield return 1;
        Check("the daily challenge uses today's seed and has no rival at first", MatchActive && match.Spec.Mode == "daily" && match.Spec.Seed == dailySeed && match.RivalRun == null);
        Check("its label names the date", match.Spec.Label.EndsWith(MatchRules.DailyLabel(DateTime.Now)));
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        match.Me = new MatchScore { Cg = 1234, Perfects = 2, Catches = 2 };
        match.Log.Add(new MatchEvent { t = 30, fish = "mud_carp", cg = 1234, perfect = true });
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("with no rival the card says time, and the run is your best of the day", mode == "matchresult" && match.Result.Result == "solo" && match.Result.NewBest && match.Result.Rank == 1);
        pendingShot = "match-daily-result"; yield return 2;
        ClickButton("Try again"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Try again keeps the day's seed (the same fish again)", match.Spec.Seed == dailySeed && match.Phase == "countdown");
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        match.Me = new MatchScore { Cg = 800, Perfects = 0, Catches = 1 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("a worse second run isn't marked a new best", mode == "matchresult" && !match.Result.NewBest);
        Check("both runs are on the board and the better one leads it", Leaderboards.Best("daily", 5).Count == 2 && Leaderboards.Best("daily", 5)[0].score == 1234);
        ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("back on the island after a finished match, unchanged", !MatchActive && ReferenceEquals(state, realState) && AdventureJson() == realJson);
        // Racing your best run.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the panel offers to race your best run once there is one", Gfx.Seen.ContainsKey("Race your best run"));
        ClickButton("Race your best run"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the toggle is on", matchRaceBest);
        pendingShot = "match-panel-daily"; yield return 2;
        ClickButton("Play it again"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && mode == "play"); i++) yield return 1;
        Check("the rival is your best run, its fish and times replayed", MatchActive && match.RivalName == "Your best" && match.RivalRun.Count == 1 && match.RivalRun[0].cg == 1234);
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        match.Me = new MatchScore { Cg = 2000, Perfects = 1, Catches = 2 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("beating your own best run wins", match.Result.Result == "win" && match.Result.NewBest);
        // Closing the window mid-match saves the adventure, not the tournament.
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("on again", MatchActive && match.Phase == "countdown");
        RestoreFromMatch(); Save();
        Check("closing the window mid-match puts the adventure back first, and that is what gets saved", !MatchActive && ReferenceEquals(state, realState) && AdventureJson() == realJson && SaveFile.Read(SaveFile.Slot)?.coins >= 777);
        // The Clear font: every screen of it still fits (looked at in the screenshots).
        Settings.Data.clearFont = true; yield return 3;
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "match-panel-play-clear"; yield return 2;
        ClickButton("Leaderboards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "match-panel-board-clear"; yield return 2;
        ClickButton("Play"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Start the duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "countdown" && mode == "play"); i++) yield return 1;
        pendingShot = "match-countdown-clear"; yield return 2;
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        match.Me = new MatchScore { Cg = 1234, Perfects = 2, Catches = 3, Casts = 5 };
        match.Log.Add(new MatchEvent { t = 31, fish = "silver_tuna", cg = 900, perfect = true });
        match.Log.Add(new MatchEvent { t = 70, fish = "pond_perch", cg = 334, perfect = false });
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        pendingShot = "match-result-clear"; yield return 2;
        ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Settings.Data.clearFont = false; yield return 2;
        Check("the Clear font round trip leaves you on the island", !MatchActive && mode == "play");

        // --- Codex's review ---
        Note("Matches: review findings");
        // A damaged leaderboard that still parses must not crash anything.
        File.WriteAllText(SaveFile.BoardPath, "{\"player\":\"abc\",\"entries\":[null,{\"id\":null,\"mode\":null,\"log\":[null]},{\"id\":\"ok\",\"mode\":\"daily\",\"result\":\"solo\",\"score\":5,\"seed\":9,\"ruleset\":1,\"log\":[null,{\"t\":1,\"fish\":\"pond_perch\",\"cg\":5}]}]}");
        Leaderboards.Reset();
        bool boardThrew = false;
        try
        {
            var bd = Leaderboards.Board();
            Leaderboards.Best("daily", 5); Leaderboards.Recent("daily", 5); Leaderboards.Record("derby"); Leaderboards.BestOn("daily", 9);
            Leaderboards.Add(E("fresh", "derby", 1, 100, 0));
            var ghostLog = Leaderboards.BestOn("daily", 9)?.log;
            if (ghostLog != null) MatchRules.ScoreAt(ghostLog, 100);
            boardThrew = bd.entries.Any(x => x == null || x.mode == null || x.log != null && x.log.Any(l => l == null));
        }
        catch (Exception) { boardThrew = true; }
        Check("a valid but damaged leaderboard (null entries, null fish logs) loads cleanly and never crashes", !boardThrew);
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        // Leaving counts as a loss in your record, but never in the best scores.
        Leaderboards.Add(E("w1", "derby", 1, 500, 0, "win")); Leaderboards.Add(E("f1", "derby", 2, 900, 0, "forfeit")); Leaderboards.Add(E("l1", "derby", 3, 100, 0, "loss"));
        Check("a forfeit is a loss in the record (so leaving every losing game can't keep it undefeated)", Leaderboards.Record("derby") == (3, 1, 2, 0));
        Check("but a forfeit is never a best score", Leaderboards.Best("derby", 5).All(x => x.result != "forfeit"));
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();

        // Chum thrown in the adventure doesn't speed up a match.
        MatchTestGame("Reviewer");
        chumUntil["lagoon"] = time + 500;
        yield return 3;
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 4242, Rival = "bess", Label = "review" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        for (int i = 0; i < 100 && mode is "charging" or "play"; i++) yield return 1;
        var rp0 = MatchRules.Plan(4242, 0);
        Check($"adventure chum doesn't halve a match's wait ({fish?.Timer + fish?.WaitT:0.00} s against the seed's {rp0.Wait:0.00})", fish != null && MathF.Abs(fish.Timer + fish.WaitT - rp0.Wait) < 0.2f);
        // A long hitch moves the match clock and the fishing by the same small step.
        fish = null; reel = null; mode = "play";
        var rf = Data.FishById["mud_carp"];
        fish = new FishCast { Spot = "lagoon", MatchN = 1, Roll = new Catchable { Id = rf.Id, Name = rf.Name, Difficulty = rf.Difficulty } };
        reel = new ReelState { ZoneH = 1, ZoneY = Bar - 1, FishY = 70, FishTarget = 50, FishTimer = 0.3f, Progress = 0.6f, Diff = 1, Roll = fish.Roll, Style = "dart", LeapLen = 1 };
        mode = "reeling"; match.ReelAcc = 0;
        float t0m = match.T, age0 = reel.Age;
        Update(0.5f);
        Check($"a half-second hitch advances the match clock by at most a quarter second ({match.T - t0m:0.000}) and the fight by the same ({reel.Age - age0:0.000})", MathF.Abs(match.T - t0m - 0.25f) < 0.011f && MathF.Abs(reel.Age - age0 - 0.25f) < 0.02f);
        // The fight has a random stream of its own: ambient things consuming `rng` between frames change nothing.
        var fightFish = new List<float>();
        foreach (bool noisy in new[] { false, true, false })
        {
            fish.MatchN = 1; fish.Roll = new Catchable { Id = rf.Id, Name = rf.Name, Difficulty = rf.Difficulty };
            MatchSeedFight();
            reel = new ReelState { ZoneH = 1, ZoneY = Bar - 1, FishY = 70, FishTarget = 50, FishTimer = 0.3f, Progress = 0.6f, Diff = 1f, Roll = fish.Roll, Style = "dart", RunT = 2, LeapT = 2, LeapLen = 1f };
            mode = "reeling"; match.ReelAcc = 0;
            for (int i = 0; i < 90; i++) { if (noisy) for (int k = 0; k < i % 5 + 1; k++) rng.Next(); StepMatchReel(1f / 60); }
            if (reel != null) fightFish.Add(reel.FishY);
        }
        Check($"random draws by the rest of the world between frames don't change the fight ({string.Join(" / ", fightFish.Select(v => v.ToString("0.00")))})", fightFish.Count == 3 && MathF.Abs(fightFish[0] - fightFish[1]) < 0.001f && MathF.Abs(fightFish[0] - fightFish[2]) < 0.001f);
        fish = null; reel = null; mode = "play";
        // Leaving a run a hair before the whistle can't turn into a finished match as well.
        match.T = match.Spec.Duration - 0.5f;
        string raceId = match.Spec.Id;
        bool sawResult = false;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 120 && (MatchActive || mode != "play"); i++) { if (mode == "matchresult") sawResult = true; yield return 1; }
        Check("(while leaving) it never becomes a result card", !sawResult);
        var raced = Leaderboards.Board().entries.Where(x => x.id == raceId).ToList();
        Check("a match left just before the whistle is one forfeit and nothing else", !MatchActive && mode == "play" && raced.Count == 1 && raced[0].result == "forfeit");
        // Closing the window mid-match is a forfeit too.
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 777, Rival = "marlo", Label = "review" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        string closeId = match.Spec.Id;
        AbandonMatch();
        Check("closing the window mid-match records a forfeit and puts the adventure back", !MatchActive && Leaderboards.Board().entries.Any(x => x.id == closeId && x.result == "forfeit"));
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        mode = "play"; fish = null; reel = null; chumUntil.Clear();
        yield return 2;

        // --- Pip's prize for the Duel ---
        Note("Matches: Pip pays for the Duel");
        MatchTestGame("Prizer");
        state.day = 7; state.coins = 100; state.hinted["metPip"] = true; state.hinted["pipDuel"] = true;
        yield return 3;
        OpenShop(); yield return 3;
        ClickButton("Derby"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Pip's Derby tab points at the Duel with a button", Gfx.Seen.ContainsKey("Open Matches"));
        pendingShot = "match-pip-derby"; yield return 2;
        ClickButton("Open Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Open Matches goes to the Matches panel", mode == "panel" && panel == "matches");
        ClosePanels(); yield return 2;
        foreach (var (rival, coins, glow) in new[] { ("bess", 30, 0), ("ivy", 100, 3) })
        {
            int coinsBefore = state.coins, glowBefore = Has("glow_bait");
            BeginMatch(new MatchSpec { Mode = "derby", Seed = 31 + coins, Rival = rival, Label = "prize" });
            for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
            match.Me = new MatchScore { Cg = 99999, Perfects = 1, Catches = 1 };
            match.T = match.Spec.Duration - 0.05f; yield return 8;
            Check($"beating {rival} shows Pip's prize on the card", mode == "matchresult" && match.Result.Result == "win" && match.Result.Prize.Contains($"{coins} coins"));
            Check("and nothing is paid while the match is still on", match.Real.coins == coinsBefore);
            if (rival == "bess") pendingShot = "match-result-prize";
            yield return 2;
            ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
            for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
            Check($"back on the island the prize is paid into the adventure: {coins} coins{(glow > 0 ? " and glow bait" : "")}", state.coins == coinsBefore + coins && Has("glow_bait") == glowBefore + glow && state.gifts["duel_" + rival] == 7);
        }
        // Once a day for each rival.
        int coinsNow = state.coins;
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 77, Rival = "bess", Label = "prize" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        match.Me = new MatchScore { Cg = 99999, Perfects = 1, Catches = 1 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("the same rival again the same day shows no prize, with the reason", match.Result.Result == "win" && match.Result.Prize == "" && match.Result.PrizeNote.Contains("already"));
        ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("and pays nothing", state.coins == coinsNow);
        // A loss, a draw, the daily challenge and leaving never pay; the next day pays again.
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 78, Rival = "marlo", Label = "prize" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        match.Me = new MatchScore { Cg = 0 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("a loss has no prize", match.Result.Result == "loss" && match.Result.Prize == "");
        ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("and pays nothing", state.coins == coinsNow);
        state.day = 8;
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 79, Rival = "bess", Label = "prize" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        match.Me = new MatchScore { Cg = 99999 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("the next day pays again", state.coins == coinsNow + 30);
        BeginMatch(new MatchSpec { Mode = "daily", Seed = 80, Rival = null, Label = "prize" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        match.Me = new MatchScore { Cg = 99999 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("the daily challenge pays nothing", state.coins == coinsNow + 30);
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        yield return 2;

        // --- The rival on the bank ---
        Note("Matches: the rival is on the bank");
        MatchTestGame("Watcher");
        yield return 3;
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 4242, Rival = "ivy", Label = "rival" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        var rev = match.RivalRun;
        Check("the rival has landings to show", rev != null && rev.Count >= 3);
        int RivalPixels()
        {
            RenderWorld(time);
            var with = (Color[])pix.Buf.Clone();
            var keep = match.RivalRun; match.RivalRun = null;
            RenderWorld(time);
            match.RivalRun = keep;
            int diff = 0;
            for (int i = 0; i < with.Length; i++) if (with[i].R != pix.Buf[i].R || with[i].G != pix.Buf[i].G || with[i].B != pix.Buf[i].B) diff++;
            return diff;
        }
        match.T = rev[0].t - 3; match.RivalEvt = 0; yield return 3;
        int reelPx = RivalPixels();
        Check($"the rival is drawn on the bank while they fight a fish ({reelPx} pixels)", reelPx > 80);
        pendingShot = "match-rival-reeling"; yield return 2;
        match.T = rev[0].t + 0.2f; match.RivalEvt = 0; yield return 3;
        Check("when their fish comes in its weight rises over them", floaters.Any(f => f.Text.StartsWith("+") && f.Text.EndsWith(" kg") && MathF.Abs(f.X - RivalX) < 3));
        Check("and they hold it up (drawn)", RivalPixels() > 80);
        pendingShot = "match-rival-holding"; yield return 2;
        match.T = rev[0].t + 3; yield return 3;
        Check("then they fish on, bobber out (drawn)", RivalPixels() > 60);
        AbandonMatch();
        BeginMatch(new MatchSpec { Mode = "derby", Seed = 4242, Rival = null, Label = "solo rival" });
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
        Check("a match with no rival (the daily challenge) has nobody on the bank", match.RivalRun == null && RivalPixels() == 0);
        AbandonMatch();
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        mode = "play"; yield return 2;
        state.regattaBest = 83.4f; state.sortBest = 12; state.platejawBest = 141;
        // Your name is picked from the lists with arrows.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        var nb0 = Leaderboards.Board(); int adj0 = nb0.adj, noun0 = nb0.noun;
        ClickButton("name:adj:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the adjective arrow picks the next word", Leaderboards.Board().adj == adj0 + 1 && Leaderboards.Board().noun == noun0 && Leaderboards.Board().name == Leaderboards.NameOf(adj0 + 1, noun0));
        ClickButton("name:noun:prev"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the noun arrow picks the previous word", Leaderboards.Board().noun == noun0 - 1 && Leaderboards.Board().name == Leaderboards.NameOf(adj0 + 1, noun0 - 1));
        pendingShot = "match-name-picker"; yield return 2;
        string pickedName = Leaderboards.Board().name;
        Leaderboards.Reset();
        Check("and it is saved", Leaderboards.Board().name == pickedName);
        ClickButton("Random"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Random still picks another by chance", Leaderboards.Board().name.Split(' ').Length >= 2);
        ClosePanels(); yield return 2;
        // A populated board, for the eye.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Leaderboards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "match-panel-board-full"; yield return 2;
        ClosePanels(); yield return 2;
        mode = "play"; fish = null; reel = null;
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        yield return 3;
    }

    static string AdventureJson_Of(State s)
    {
        var copy = JsonSerializer.Deserialize<State>(JsonSerializer.Serialize(s, JsonAll), JsonAll);
        copy.playSecs = 0; copy.savedAt = null; copy.food = 0;   // hunger is meant to move on the island
        copy.coins = 0; copy.dailyDays = new(); copy.inv.Remove("glow_bait"); foreach (var k in copy.gifts.Keys.Where(k => k.StartsWith("duel_") || k.StartsWith("daily_")).ToList()) copy.gifts.Remove(k);   // and Pip pays for a Duel won
        return JsonSerializer.Serialize(copy, JsonAll);
    }
}
#endif
