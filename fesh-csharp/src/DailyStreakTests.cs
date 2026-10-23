#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The Daily Challenge streak and its prizes (1.45.1, FESH_STREAK_TEST=1): the day counting with no game running, the file,
    // and real daily runs: a run with a catch counts, nothing is paid while the match is on, the prize arrives on leaving,
    // a second run the same day pays nothing more, 3 days in a row pay a bonus, a leaver and an empty net don't count.
    IEnumerable<int> DailyStreakScript()
    {
        Note("Daily Challenge streak");
        var d0 = new DateTime(2026, 10, 13);
        string L(int back) => MatchRules.DailyLabel(d0.AddDays(-back));
        /* ---------- the days, with no game running ---------- */
        var s3 = MatchRules.DailyStreak(new[] { L(2), L(1), L(0) }, d0);
        Check($"three days in a row ending today ({s3.Streak}, start {s3.Start:MM-dd})", s3.Streak == 3 && s3.Today && s3.Start == d0.AddDays(-2));
        var alive = MatchRules.DailyStreak(new[] { L(3), L(2), L(1) }, d0);
        Check("not played yet today: yesterday keeps it alive", alive.Streak == 3 && !alive.Today);
        Check("a missed day ends it", MatchRules.DailyStreak(new[] { L(4), L(3), L(2) }, d0).Streak == 0);
        var gap = MatchRules.DailyStreak(new[] { L(5), L(3), L(1), L(0) }, d0);
        Check($"a gap starts a new run ({gap.Streak})", gap.Streak == 2 && gap.Start == d0.AddDays(-1));
        Check("no days, no streak", MatchRules.DailyStreak(Array.Empty<string>(), d0).Streak == 0);
        Check("month ends count as consecutive", MatchRules.DailyStreak(new[] { "2026-09-30", "2026-10-01" }, new DateTime(2026, 10, 1)).Streak == 2);
        Check("prizes at 3, 7, 14 and 30 only", new[] { 1, 2, 3, 4, 7, 8, 14, 30, 31 }.Select(n => MatchRules.DailyMilestone(n).coins > 0).SequenceEqual(new[] { false, false, true, false, true, false, true, true, false }));
        Check("the next prize is named", MatchRules.NextMilestone(0) == 3 && MatchRules.NextMilestone(3) == 7 && MatchRules.NextMilestone(10) == 14 && MatchRules.NextMilestone(29) == 30 && MatchRules.NextMilestone(30) == 0);
        Check("every milestone is coins, perhaps with a real item", new[] { 3, 7, 14, 30 }.All(n => { var p = MatchRules.DailyMilestone(n); return p.coins > 0 && (p.item == null || Items.ById.ContainsKey(p.item)); }));

        /* ---------- the save file ---------- */
        var raw = new State { created = true, look = new Look { name = "Old" } };
        raw.dailyDays = new List<string> { "2026-10-12", null, "x", "2026-10-12", "2026-10-11" };
        string tmpPath = Path.Combine(Path.GetTempPath(), "fesh-streak-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(tmpPath, System.Text.Json.JsonSerializer.Serialize(raw, JsonAll));
        var back = SaveFile.Load(tmpPath);
        Check("a damaged list of days is cleaned on load", back.dailyDays.SequenceEqual(new[] { "2026-10-11", "2026-10-12" }));
        File.WriteAllText(tmpPath, "{\"created\":true,\"look\":{\"name\":\"Older\"}}");
        Check("an older save has no days and needs none", SaveFile.Load(tmpPath).dailyDays.Count == 0);
        File.Delete(tmpPath);

        /* ---------- real runs ---------- */
        OldCelebrate = true;
        MatchTestGame("Streaker");
        yield return 3;
        DateTime now = DateTime.Now;
        int ymd = MatchRules.DayNumber(now);
        state.coins = 0;
        IEnumerable<int> PlayDaily(int cg, bool leaveEarly = false)
        {
            BeginMatch(DailySpec());
            for (int i = 0; i < 300 && !(MatchActive && match.Phase == "run"); i++) yield return 1;
            match.Me = new MatchScore { Cg = cg, Perfects = 0, Catches = cg > 0 ? 1 : 0 };
            if (cg > 0) match.Log.Add(new MatchEvent { t = 30, fish = "mud_carp", cg = cg });
            if (leaveEarly) yield break;
            match.T = match.Spec.Duration - 0.05f; yield return 8;
        }
        IEnumerable<int> Back()
        {
            ClickButton("Back to the island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
            for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        }
        // An empty net doesn't count.
        foreach (var s in PlayDaily(0)) yield return s;
        Check("a run with no catch isn't a streak day", MatchRules.DailyStreak(match.Real.dailyDays, now).Streak == 0 && match.Result.DailyLine == "");
        foreach (var s in Back()) yield return s;
        Check("and pays nothing", state.coins == 0 && !state.gifts.ContainsKey("daily_play"));

        // A leaver doesn't count either.
        foreach (var s in PlayDaily(500, leaveEarly: true)) yield return s;
        LeaveMatch(); for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("leaving the run doesn't count", MatchRules.DailyStreak(state.dailyDays, now).Streak == 0 && state.coins == 0);

        // A first real run.
        foreach (var s in PlayDaily(1234)) yield return s;
        Check($"a finished run starts the streak ({match.Result.DailyLine})", MatchRules.DailyStreak(match.Real.dailyDays, now).Streak == 1 && match.Result.DailyLine.Contains("Streak: 1 day") && match.Result.DailyLine.Contains($"{MatchRules.DailyPlayCoins} coins"));
        Check("the prize goes into the real save at once, never the sandbox", match.Real.coins == MatchRules.DailyPlayCoins && state.coins == 0 && match.Real.dailyDays.Contains(match.Spec.Day));
        pendingShot = "s01-result"; yield return 3;
        foreach (var s in Back()) yield return s;
        Check($"back on the island the day's prize is paid ({state.coins})", state.coins == MatchRules.DailyPlayCoins && state.gifts["daily_play"] == ymd);

        // The same day again.
        foreach (var s in PlayDaily(900)) yield return s;
        Check($"a second run the same day says the prize is paid ({match.Result.DailyLine})", match.Result.DailyLine.Contains("already paid"));
        foreach (var s in Back()) yield return s;
        Check("and pays nothing more", state.coins == MatchRules.DailyPlayCoins);

        // Three days in a row.
        state.dailyDays.Add(MatchRules.DailyLabel(now.AddDays(-1))); state.dailyDays.Add(MatchRules.DailyLabel(now.AddDays(-2)));
        state.gifts["daily_play"] = ymd - 1; state.coins = 0;
        foreach (var s in PlayDaily(700)) yield return s;
        var mile = MatchRules.DailyMilestone(3);
        Check($"three days in a row adds the bonus ({match.Result.DailyLine})", match.Result.DailyLine.Contains("Streak: 3 days") && match.Result.DailyLine.Contains($"{MatchRules.DailyPlayCoins + mile.coins} coins"));
        foreach (var s in Back()) yield return s;
        Check($"and it is paid ({state.coins})", state.coins == MatchRules.DailyPlayCoins + mile.coins && state.gifts["daily_ms_3"] == MatchRules.DayNumber(now.AddDays(-2)));
        // The same run's milestone is never paid twice.
        state.gifts["daily_play"] = ymd - 1;
        foreach (var s in PlayDaily(600)) yield return s;
        foreach (var s in Back()) yield return s;
        Check("it isn't paid again for the same run", state.coins == MatchRules.DailyPlayCoins * 2 + mile.coins);

        // The clock put back: a later date was already paid, so an earlier one pays nothing.
        int coinsBeforeRollback = state.coins;
        state.gifts["daily_play"] = ymd + 5;
        foreach (var s in PlayDaily(500)) yield return s;
        foreach (var s in Back()) yield return s;
        Check("a date earlier than one already paid pays nothing", state.coins == coinsBeforeRollback);

        // The daily card shows the streak.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "s02-card"; yield return 4;
        ClosePanels(); yield return 2;
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
    }
}
#endif
