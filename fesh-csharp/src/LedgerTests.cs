#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The Angler's Ledger (1.44, FESH_LEDGER_TEST=1): the achievements' data, a new game, an old save's one-card catch-up
    // (paid once, also across a reload), a catch earning one, several at once sharing a banner, the counters (and a jump
    // aboard not counting as sailing), secrets kept off the page, titles (picked by a real click, shown on the slot card,
    // a bogus one ignored), and the panel by real clicks: every tab fitting.
    IEnumerable<int> LedgerScript()
    {
        Note("The Angler's Ledger");
        OldCelebrate = false;
        state = new State { created = true, look = new Look { name = "Ledgerkeeper" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        IEnumerable<int> UntilCard() { for (int i = 0; i < 600 && mode != "celebrate"; i++) { if (badgeNews == null && toastTimer > 0) toastTimer = 0; yield return 1; } }

        /* ---------- the data ---------- */
        Check($"{Achievements.Length} achievements with different ids, each in a group and with a goal",
            Achievements.Length >= 55 && Achievements.Select(a => a.Id).Distinct().Count() == Achievements.Length
            && Achievements.All(a => LedgerGroups.Contains(a.Group) && a.Goal > 0 && a.Name.Length > 0 && a.Desc.Length > 0));
        bool runs = true;
        foreach (var a in Achievements) { try { a.Value(this); } catch { runs = false; Note("throws: " + a.Id); } }
        Check("every achievement can be measured on a new save", runs);
        Check("a secret is anything about a legend or a boss (no Starwell or Tidemane before they're found)",
            Achievements.Where(a => a.Id is "tidemane" or "platejaw" or "ironbill" or "mire" or "ray" or "tarpon" or "lungfish" or "whiskers" or "story2").All(a => a.Secret));

        /* ---------- a new game ---------- */
        Check("a new game is stamped and has earned nothing", state.Hinted("ledger") && Achievements.All(a => !AchEarned(state, a)) && celebQueue.All(c => c.Kind != "ledger"));

        /* ---------- with the ladder off (the old scripts) nothing happens ---------- */
        OldCelebrate = true; state.commons["pond_perch"] = 1; int c0 = state.coins;
        CheckLedger();
        Check("in the old scripts the Ledger pays nothing", state.coins == c0 && !AchEarned(state, Achievements[0]));
        OldCelebrate = false;

        /* ---------- a catch earns one; several at once share a banner ---------- */
        celebQueue.Clear(); banner = null;
        ledgerCheckT = 0; yield return 3;
        var firstAch = Achievements.First(a => a.Id == "first");
        Check($"a first fish earns \"First bite\" within a second ({state.coins - c0} coins)", AchEarned(state, firstAch) && state.coins - c0 == firstAch.Coins
            && celebQueue.Concat(banner == null ? Array.Empty<Celeb>() : new[] { banner }).Any(c => c.Kind == "ach" && c.Title == "First bite"));
        celebQueue.Clear(); banner = null;
        foreach (var f in Data.AllCommon.Where(f => !f.Legend).Take(10)) state.commons[f.Id] = 1;
        CheckLedger();
        var multi = celebQueue.LastOrDefault(c => c.Kind == "ach");
        Check($"several reached at once are one banner ({multi?.Tag}: {multi?.Title})", celebQueue.Count(c => c.Kind == "ach") == 1 && multi.Tag.EndsWith(" achievements") && multi.Sub.Contains("Curious angler"));
        int paid = state.coins; CheckLedger();
        Check("nothing pays twice", state.coins == paid);
        celebQueue.Clear(); banner = null;

        /* ---------- an old save: one card, once ---------- */
        var old = new State { created = true, look = new Look { name = "Old salt" }, coins = 5, day = 30, xp = 900, perfects = 12, chests = 4 };
        old.flags.metTomas = true;
        foreach (var f in Data.AllCommon.Where(f => !f.Legend).Take(45)) old.commons[f.Id] = 3;
        old.trophies["pond_perch"] = 2;
        state = old; StartGame(false); yield return 3;
        int had = Achievements.Count(a => AchEarned(state, a)), expect = Achievements.Where(a => AchEarned(state, a)).Sum(a => a.Coins);
        // (Loading it also pays the fish album's finished chapters, so the coins are at least the Ledger's.)
        Check($"an old save gets what it already did at once ({had} achievements, {expect} coins of {state.coins - 5})", had >= 8 && state.coins - 5 >= expect && expect > 0);
        Check("with one card, not a banner each", celebQueue.Count(c => c.Kind == "ledger") == 1 && celebQueue.All(c => c.Kind != "ach"));
        foreach (var s in UntilCard()) yield return s;
        pendingShot = "l01-catchup"; yield return 8;
        Check($"the card opens once you're free ({celebCard?.Title}, fits: {lastCelebBottom:0})", mode == "celebrate" && celebCard?.Kind == "ledger" && lastCelebBottom <= Gfx.LH - 8);
        Check("its count leaves the secrets out", celebCard.Stats[0].EndsWith($"of {LedgerGroups.Sum(g => Shown(g).Count())} achievements"));
        yield return 40; Inp.Tap(KeyboardKey.E); yield return 3;
        Check("\"Let's see\" opens the Ledger", mode == "panel" && panel == "ledger");
        ClosePanels(); yield return 2;
        int after = state.coins;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("reloading it pays nothing more and shows no card again", state.coins == after && celebQueue.All(c => c.Kind != "ledger"));

        /* ---------- counters ---------- */
        int sold0 = Tally("sold"); Give("pond_perch", 3);
        Sell("pond_perch", 2);
        Check($"selling counts ({Tally("sold") - sold0} fish, {Tally("earned")} coins from fish)", Tally("sold") - sold0 == 2 && Tally("earned") > 0);
        Give("grilled_fish"); state.food = 40; int eaten0 = Tally("eaten");
        Eat("grilled_fish");
        Check("eating counts", Tally("eaten") == eaten0 + 1);
        double sailed0 = state.sailed;
        Give("boat"); state.aboard = true; player.X += 300;
        yield return 3;
        Check("a jump while aboard isn't sailing", state.sailed == sailed0);
        state.aboard = false;

        /* ---------- secrets ---------- */
        Check("Tidemane's achievement isn't on the page (or in the count) before it's tamed",
            !Shown("Legends & stories").Any(a => a.Id == "tidemane") && Shown("Legends & stories").Count() < Achievements.Count(a => a.Group == "Legends & stories"));
        state.tamed = true; CheckLedger();
        Check("once it's yours, it is", Shown("Legends & stories").Any(a => a.Id == "tidemane") && EarnedTitles.Contains("Rider of the tide"));
        celebQueue.Clear(); banner = null;

        /* ---------- the panel by real clicks ---------- */
        mode = "play"; toastTimer = 0; yield return 2;
        Inp.Tap(KeyboardKey.Q); yield return 4;
        Check("Q opens the journal", mode == "panel" && panel == "journal");
        ClickButton("Angler's Ledger"); yield return 4;
        Inp.ScriptMouse = Offscreen;
        Check("its Angler's Ledger button opens the Ledger", panel == "ledger");
        bool allFit = true;
        foreach (var g in LedgerGroups.Append("Numbers"))
        {
            ClickButton("ledger:" + g); yield return 3; Inp.ScriptMouse = Offscreen; lastLedgerBottom = 0; yield return 2;
            bool fit = ledgerTab == g && lastLedgerBottom > 0 && lastLedgerBottom <= panelAt.y + panelAt.h - 12;
            if (!fit) { allFit = false; Note($"{g}: tab {ledgerTab}, bottom {lastLedgerBottom:0} of {panelAt.y + panelAt.h:0}"); }
            if (g is "Catching" or "Legends & stories" or "Numbers") { pendingShot = "l02-" + g.Replace(" ", "").Replace("&", ""); yield return 4; }
        }
        Check("every tab opens by its button and fits the panel", allFit);
        string before = state.title;
        ClickButton("ledger:title-next"); yield return 4; Inp.ScriptMouse = Offscreen;
        Check($"the title picker puts on an earned title ({state.title})", state.title != before && EarnedTitles.Contains(state.title) && WornTitle == state.title);
        ClosePanels(); yield return 2;
        state.title = "Emperor of fish";
        Check("a title that wasn't earned is never worn", WornTitle == null && SlotTitleNow() == state.look.name);

        OldCelebrate = true;
        yield return 2;
    }
}
#endif
