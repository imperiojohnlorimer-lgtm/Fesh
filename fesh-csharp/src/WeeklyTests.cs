#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The weekly bounty (1.45.1, FESH_WEEKLY_TEST=1): rolled from the week's number, scaled up from the day's, measured from
    // when it was rolled, paid once and saved, settled before the new week replaces it, kept across a reload, on the panel in
    // both fonts, and never counted in a match or in the old scripts.
    IEnumerable<int> WeeklyScript()
    {
        Note("Weekly bounty");
        OldCelebrate = false; BoardOff = false; EventsOff = true;
        state = new State { created = true, look = new Look { name = "Weekly" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        var anyFish = Data.AllCommon.First(f => !f.Legend && f.Id == "pond_perch");

        /* ---------- the roll ---------- */
        state.day = 1; var w1 = Weekly();
        var seenKinds = new HashSet<string>();
        for (int d = 1; d <= 7; d++) { state.day = d; Check($"day {d}: the same bounty all week", ReferenceEquals(Weekly(), w1) && state.week.week == 0); }
        Check($"it is one of the week's kinds, several times the day's work ({w1.kind} x{w1.need})", WeekKinds.Any(k => k.id == w1.kind) && w1.need >= 3);
        Check("never a lesson, a named fish or a place", WeekKinds.All(k => k.id is not ("lesson" or "species" or "place")));
        for (int wk = 0; wk < 60; wk++) { state.day = wk * 7 + 1; state.week = null; seenKinds.Add(Weekly().kind); }
        Check($"over sixty weeks it's varied ({string.Join(",", seenKinds.OrderBy(k => k))})", seenKinds.Count >= 3);
        state.day = 1; state.week = null;
        state.day = 8; var w2 = Weekly();
        Check("day 8 is a new week and a new notice", state.week.week == 1 && !ReferenceEquals(w1, w2));
        state.day = 8; int left8 = DaysLeftInWeek; state.day = 14; int left14 = DaysLeftInWeek;
        Check($"the days left count today ({left8} on day 8, {left14} on day 14)", left8 == 7 && left14 == 1);

        /* ---------- progress and pay ---------- */
        state.day = 3; state.week = null; state.coins = 0;
        var wb = Weekly();
        wb.kind = "fish"; wb.need = 3; wb.have = 0; wb.paid = false; wb.tier = 0;
        BoardCatch(anyFish, 1); BoardCatch(anyFish, 1);
        Check($"two of three catches ({wb.have}/{wb.need}) and nothing paid yet", wb.have == 2 && !wb.paid);
        int c0 = state.coins, bait0 = Has("bait");
        BoardCatch(anyFish, 1);
        var (pay, baitPay) = WeekPay(wb);
        Check($"the third pays at once ({state.coins - c0} coins, {Has("bait") - bait0} bait)", wb.paid && state.coins - c0 >= pay && Has("bait") - bait0 >= baitPay && state.weeksDone == 1);
        c0 = state.coins;
        BoardCatch(anyFish, 1); BoardCatch(anyFish, 1);
        Check("and never twice", state.coins - c0 < pay && state.weeksDone == 1);
        yield return 3;
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("a reload keeps it paid, once", state.week != null && state.week.b.paid && state.weeksDone == 1 && Weekly().paid);

        /* ---------- settled before the next week ---------- */
        state.day = 10; state.week = null; state.coins = 0;
        var sell = Weekly();
        sell.kind = "sell"; sell.need = 4; sell.have = 0; sell.paid = false; sell.start = Tally("sold"); sell.tier = 1;
        for (int i = 0; i < 4; i++) state.tally["sold"] = state.tally.GetValueOrDefault("sold") + 1;
        state.day = 15;   // the week turns before the once-a-second look
        var next = Weekly();
        Check($"a counter finished just before the new week is paid first ({state.coins} coins)", sell.paid && state.coins >= WeekPay(sell).coins && state.week.week == 2 && !ReferenceEquals(next, sell));
        // A week not finished is just gone.
        state.day = 22; state.coins = 0; var gone = Weekly(); gone.kind = "fish"; gone.need = 50; gone.have = 3;
        state.day = 29; Weekly();
        Check("an unfinished week is dropped, with nothing taken away", state.coins == 0 && state.week.week == 4);

        /* ---------- the panel ---------- */
        state.day = 30; state.week = null; Weekly();
        OpenBoard(); lastBoardBottom = 0; yield return 3;
        Check("the panel has the week's row", Gfx.Seen.ContainsKey("board:week") && Gfx.Seen.ContainsKey("board:row:2"));
        pendingShot = "wk1-board"; yield return 4;
        Check($"it fits in the pixel font ({lastBoardBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastBoardBottom > 0 && lastBoardBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = true; lastBoardBottom = 0; yield return 4;
        pendingShot = "wk2-board-clear"; yield return 4;
        Check($"and in the clear font ({lastBoardBottom:0})", lastBoardBottom > 0 && lastBoardBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = false; ClosePanels(); yield return 2;

        /* ---------- not in the old scripts or a match ---------- */
        state.day = 36; state.week = null; Weekly(); state.week.b.kind = "fish"; state.week.b.need = 99; state.week.b.have = 0;
        BoardOff = true; BoardCatch(anyFish, 1);
        Check("with the board off nothing counts", state.week.b.have == 0);
        BoardOff = false;
        OldCelebrate = true; BoardOff = true;
    }
}
#endif
