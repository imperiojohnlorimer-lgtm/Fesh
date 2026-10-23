using Raylib_cs;

namespace Fesh;

// The harbour board's panel (Bounties.cs): today's three notices, each with a picture, one line, a bar and its pay, and
// the run of days under them. Big rows and nothing that needs hovering, so it reads the same on a phone.
partial class Game
{
    const float BoardW = 860, BoardH = 690;
    float lastBoardBottom;

    string BountyIcon(Bounty b)
    {
        string id = b.kind switch
        {
            "fish" => BestRod(),
            "kinds" or "place" or "big" => "pond_perch",
            "species" => b.arg,
            "perfect" => "bait",
            "sort" => "alimasag_crab",
            "cook" => "grilled_fish",
            "lesson" => "star",
            "sail" => "boat",
            "sell" => "coin",
            "monsters" => "slime_gel",
            _ => "star"
        };
        return id == "coin" || id != null && Items.ById.ContainsKey(id) ? id : "star";   // the coin is an icon, not an item
    }


    void DrawBoard()
    {
        var day = Board();
        var week = Weekly();
        var (x, y, w, h) = PanelFrame("Harbour board", "New notices every morning at " + HourText(DawnMin) + ". Done is paid at once.", BoardW, BoardH);
        if (PanelClose()) { ClosePanels(); return; }
        float rx = x + PanelPad, ry = y + 104, rw = w - PanelPad * 2;
        const float rh = 88, gap = 10;

        // One notice: a pinned picture, one line, a bar and its count, and what it pays (or a tick once it's paid).
        void Row(Bounty b, float top, bool weekly, string seen)
        {
            Gfx.Box(rx, top, rw, rh, b.paid ? Pal.C("#eef6e4") : weekly ? Pal.C("#fff3cf") : CardBg, b.paid ? Pal.C("#5f9a4a") : weekly ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 6);
#if DEBUG
            Gfx.Seen[seen] = new Rectangle(rx, top, rw, rh);
#endif
            Gfx.Box(rx + 14, top + 8, 72, 72, Pal.C("#f4ecd6"), Pal.C("#b9a27a"), 2, 4);
            DrawIcon(BountyIcon(b), rx + 26, top + 20, 48);
            Gfx.Circle(rx + 50, top + 10, 5, PinRed);
            float tx = rx + 104, tw = rw - 104 - 170;
            string text = weekly ? WeekText(b) : BountyText(b);
            Gfx.Text(Gfx.Ellipsize(text, FontKind.Ui700, 21, tw), tx, top + 12, FontKind.Ui700, 21, Pal.PaperInk);
            int have = Math.Min(b.have, b.need);
            float frac = b.need > 0 ? have / (float)b.need : 0, bw = tw - 80;
            Gfx.Rect(tx, top + 52, bw, 12, Pal.C("#e9e0cc"), 4);
            if (frac > 0) Gfx.Rect(tx, top + 52, MathF.Max(8, bw * frac), 12, Pal.C("#3f8a4a"), 4);
            string count = b.kind == "sail" ? $"{have / 10f:0.0}/{b.need / 10f:0.#} mi" : $"{have}/{b.need}";
            Gfx.Text(count, tx + bw + 10, top + 48, FontKind.Ui600, 18, Muted);
            float px = rx + rw - 160;
            if (b.paid)
            {
                DrawVerdict(px + 30, top + rh / 2, 18, true, Pal.C("#3f8a4a"), 4);
                Gfx.Text("Paid", px + 60, top + rh / 2 - 12, FontKind.Ui700, 22, Pal.C("#3f7d35"));
            }
            else
            {
                DrawIcon("coin", px + 4, top + 16, 24);
                Gfx.Text(Gfx.Ellipsize(weekly ? WeekPayText(b) : BountyPayText(b), FontKind.Ui600, 17, 120), px + 36, top + 18, FontKind.Ui600, 17, Pal.PaperInk);
                string sub = weekly ? $"{DaysLeftInWeek} day{(DaysLeftInWeek == 1 ? "" : "s")} left" : b.tier == 0 ? "Easy" : b.tier == 1 ? "Standard" : "Stretch";
                Gfx.Text(sub, px + 36, top + 44, FontKind.Ui500, 16, Muted);
            }
            lastBoardBottom = MathF.Max(lastBoardBottom, top + rh);
        }
        for (int i = 0; i < day.list.Count; i++) Row(day.list[i], ry + i * (rh + gap), false, "board:row:" + i);
        // The week's big one, set apart under today's three.
        float wy = ry + 3 * (rh + gap) + 6;
        Gfx.Text("This week", rx, wy - 2, FontKind.Ui700, 17, Pal.C("#9a6a1a"));
        Row(week, wy + 22, true, "board:week");
        // The run of days.
        float sy = wy + 22 + rh + 14;
        int done = day.list.Count(b => b.paid);
        bool today = state.boardLastDay == state.day;
        string streak = state.boardStreak > 0 ? $"{state.boardStreak} day{(state.boardStreak == 1 ? "" : "s")} in a row{(today ? "" : " (do one today to keep it)")}" : "Do one a day to start a run of days";
        Gfx.Text(streak, rx, sy, FontKind.Ui600, 19, Pal.PaperInk);
        string best = $"Best run: {state.boardBest} · Done so far: {state.boardDone} · Weeks done: {state.weeksDone}" + (done == day.list.Count ? " · All three done today!" : "");
        Gfx.Text(Gfx.Ellipsize(best, FontKind.Ui500, 17, rw), rx, sy + 26, FontKind.Ui500, 17, Muted);
        lastBoardBottom = MathF.Max(lastBoardBottom, sy + 26 + 22);
        // Today's harbour news (Events.cs), wrapped to two lines at most.
        if (EventBoardLine() is string news)
        {
            var lines = Gfx.Wrap("Harbour news: " + news, FontKind.Note, 18, rw);
            if (lines.Count > 2) lines = new() { lines[0], Gfx.Ellipsize(string.Join(" ", lines.Skip(1)), FontKind.Note, 18, rw) };
            Lines(lines, rx, sy + 54, 24, FontKind.Note, 18, Pal.PaperInk);
            lastBoardBottom = MathF.Max(lastBoardBottom, sy + 54 + lines.Count * 24);
        }
    }
}
