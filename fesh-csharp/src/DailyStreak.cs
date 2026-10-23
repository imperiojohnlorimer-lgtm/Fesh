namespace Fesh;

// The Daily Challenge streak and its small prizes (1.45.1). A real daily challenge (MatchSpec.Day set) finished with a catch
// counts that date in the fisher's own save (`dailyDays`, so each slot has its own streak and nothing is shared between
// slots). Each day you play pays a few coins and 3, 7, 14 and 30 days in a row pay more. The day, the claim and the coins are
// written into the real adventure together the moment the run ends (never into the match's sandbox, and so never lost if the
// window is closed on the result card); the toast waits until you are back. Claims only move forward: `gifts["daily_play"]` is
// the last date paid and `gifts["daily_ms_<n>"]` the first date of the run whose milestone was paid, so putting the clock back
// pays nothing twice and a streak that breaks and starts again pays again.
partial class Game
{
    static string DaysWord(int n) => n == 1 ? "1 day" : $"{n} days";

    static string DailyPrizeText(int coins, string item, int n) => item == null ? $"{coins} coins" : coins > 0 ? $"{coins} coins and {Items.Amount(item, n)}" : Items.Amount(item, n);

    // Worked out when the run ends: it adds the day to the streak and pays what is due.
    void PlanDailyPrize(MatchRun m, MatchOutcome o)
    {
        if (m.Spec.Mode != "daily" || m.Spec.Day == null || o.Result is "forfeit" or "void" || o.Me.Cg <= 0) return;
        var real = m.Real;
        if (!real.dailyDays.Contains(m.Spec.Day)) real.dailyDays = real.dailyDays.Append(m.Spec.Day).Distinct().OrderBy(d => d, StringComparer.Ordinal).TakeLast(400).ToList();
        var day = DateTime.TryParse(m.Spec.Day, out var d) ? d : DateTime.Now;
        var (streak, _, start) = MatchRules.DailyStreak(real.dailyDays, day);
        string run = $"Streak: {DaysWord(streak)}" + (MatchRules.NextMilestone(streak) is int next and > 0 ? $" (a bigger prize at {next})" : "");
        int ymd = MatchRules.DayNumber(day), startYmd = MatchRules.DayNumber(start);
        int coins = 0, n = 0; string item = null;
        if (real.gifts.GetValueOrDefault("daily_play") < ymd) { coins += MatchRules.DailyPlayCoins; real.gifts["daily_play"] = ymd; }
        var mile = MatchRules.DailyMilestone(streak);
        if (mile.coins > 0 && real.gifts.GetValueOrDefault("daily_ms_" + streak) < startYmd)
        {
            coins += mile.coins; item = mile.item; n = mile.n;
            real.gifts["daily_ms_" + streak] = startYmd;
        }
        if (coins == 0 && item == null) { o.DailyLine = run + " · today's prize is already paid"; return; }
        real.coins += coins;
        if (item != null) real.inv[item] = real.inv.GetValueOrDefault(item) + n;
        m.DailyToast = DailyPrizeText(coins, item, n);
        o.DailyLine = $"{run} · {m.DailyToast}, added to your adventure";
    }

    // Back on the island: say what was paid (it is already in the bag).
    void PayDailyPrizes(MatchRun m)
    {
        if (m.DailyToast == null) return;
        Sfx.Play("coin");
        Toast("Daily Challenge: " + m.DailyToast + ".", 5);
    }
}
