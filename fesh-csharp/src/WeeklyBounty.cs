namespace Fesh;

// The weekly bounty (1.45.1): one big notice on the harbour board that lasts a week of in-game days (days 1-7, 8-14, ...),
// the same kinds as the day's board but about five times the work and a bigger purse. Codex said it needed deadline rules,
// so: it is rolled from the week's number (like the day's board, from the fisher's name), it is measured only from when it was
// rolled, what you finished in the last seconds before the new week is settled and paid first, and a week not finished is
// simply gone (nothing is taken away). A match counts for nothing, as with the day's bounties. It doesn't touch the streak.
sealed class WeekBounty { public int week; public Bounty b = new(); }

partial class Game
{
    int WeekNow => (state.day - 1) / 7;
    int DaysLeftInWeek => (WeekNow + 1) * 7 - state.day + 1;

    // Kinds that scale to a week, and by how much (day need x this): the plain catches, the sale, the big fish and the dishes.
    static readonly (string id, int mult)[] WeekKinds =
    {
        ("fish", 6), ("kinds", 3), ("big", 4), ("sell", 4), ("cook", 4), ("perfect", 4), ("monsters", 4), ("sail", 3), ("sort", 4)
    };

    static (int coins, int bait) WeekPay(Bounty b) => b.tier switch { 0 => (100, 6), 1 => (150, 6), _ => (220, 8) };
    static string WeekPayText(Bounty b) { var (c, n) = WeekPay(b); return $"{c} coins, {n} bait"; }

    // This week's bounty, rolled the first time anything asks in a new week (the last week's is settled first).
    Bounty Weekly()
    {
        if (state.week != null && state.week.week == WeekNow && state.week.b?.kind != null) return state.week.b;
        if (state.week?.b?.kind != null && state.week.week < WeekNow) { CatchUpOne(state.week.b); PayWeekly(state.week.b); }
        var rng = new MatchRng(MatchRules.Mix(WeekNow, BoardSalt, BoardVersion, 43));
        int tier = BoardTier;
        var open = WeekKinds.Where(w => BountyKinds.FirstOrDefault(k => k.Id == w.id) is BountyKind k && k.Ok(this)).ToList();
        var pick = open[(int)(rng.NextDouble() * open.Count) % open.Count];
        var kind = BountyKinds.First(k => k.Id == pick.id);
        var b = new Bounty { kind = kind.Id, need = kind.Need[tier] * pick.mult, tier = tier, start = kind.Counter?.Invoke(this) ?? 0 };
        state.week = new WeekBounty { week = WeekNow, b = b };
        return b;
    }

    void CatchUpOne(Bounty b)
    {
        if (!b.paid && BountyKinds.FirstOrDefault(k => k.Id == b.kind)?.Counter is Func<Game, double> counter)
            b.have = Math.Max(b.have, (int)Math.Floor(counter(this) - b.start + 1e-6));
    }

    string WeekText(Bounty b) => BountyText(b) + " this week";

    // Done is paid at once and saved before anything is shown, like the day's.
    void PayWeekly(Bounty b, bool later = false)
    {
        if (b.paid || b.have < b.need) return;
        b.paid = true;
        var (coins, bait) = WeekPay(b);
        state.coins += coins;
        Give("bait", bait);
        state.weeksDone++;
        if (later) boardSaveDue = true; else Save();
        Celebrate(new Celeb
        {
            Kind = "board", Icon = "star", Sting = "levelup", Tag = "Harbour board: the week's big one!",
            Title = WeekText(b), Sub = $"You get {coins} coins and {bait} bait.",
        }, () => Toast($"Harbour board, this week's bounty done: {coins} coins and {bait} bait."));
    }
}
