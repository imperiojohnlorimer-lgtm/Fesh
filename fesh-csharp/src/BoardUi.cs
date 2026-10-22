using Raylib_cs;

namespace Fesh;

// The harbour board's panel (Bounties.cs): today's three notices, each with a picture, one line, a bar and its pay, and
// the run of days under them. Big rows and nothing that needs hovering, so it reads the same on a phone.
partial class Game
{
    const float BoardW = 860, BoardH = 620;
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
        var (x, y, w, h) = PanelFrame("Harbour board", "New notices every morning at " + HourText(DawnMin) + ". Done is paid at once.", BoardW, BoardH);
        if (PanelClose()) { ClosePanels(); return; }
        float rx = x + PanelPad, ry = y + 112, rw = w - PanelPad * 2;
        const float rh = 104, gap = 12;
        for (int i = 0; i < day.list.Count; i++)
        {
            var b = day.list[i];
            float top = ry + i * (rh + gap);
            Gfx.Box(rx, top, rw, rh, b.paid ? Pal.C("#eef6e4") : CardBg, b.paid ? Pal.C("#5f9a4a") : Pal.C("#c9b48f"), 2, 6);
#if DEBUG
            Gfx.Seen["board:row:" + i] = new Rectangle(rx, top, rw, rh);
#endif
            // A pinned notice with its picture.
            Gfx.Box(rx + 16, top + 16, 72, 72, Pal.C("#f4ecd6"), Pal.C("#b9a27a"), 2, 4);
            DrawIcon(BountyIcon(b), rx + 28, top + 28, 48);
            Gfx.Circle(rx + 52, top + 18, 5, PinRed);
            float tx = rx + 108, tw = rw - 108 - 170;
            Gfx.Text(Gfx.Ellipsize(BountyText(b), FontKind.Ui700, 22, tw), tx, top + 18, FontKind.Ui700, 22, Pal.PaperInk);
            // The bar and the count.
            int have = Math.Min(b.have, b.need);
            float frac = b.need > 0 ? have / (float)b.need : 0, bw = tw - 70;
            Gfx.Rect(tx, top + 62, bw, 12, Pal.C("#e9e0cc"), 4);
            if (frac > 0) Gfx.Rect(tx, top + 62, MathF.Max(8, bw * frac), 12, Pal.C("#3f8a4a"), 4);
            string count = b.kind == "sail" ? $"{have / 10f:0.0}/{b.need / 10f:0.#} mi" : $"{have}/{b.need}";
            Gfx.Text(count, tx + bw + 10, top + 58, FontKind.Ui600, 18, Muted);
            // What it pays, or a tick once it's paid.
            float px = rx + rw - 160;
            if (b.paid)
            {
                DrawVerdict(px + 30, top + rh / 2, 18, true, Pal.C("#3f8a4a"), 4);
                Gfx.Text("Paid", px + 60, top + rh / 2 - 12, FontKind.Ui700, 22, Pal.C("#3f7d35"));
            }
            else
            {
                DrawIcon("coin", px + 4, top + 26, 24);
                Gfx.Text(Gfx.Ellipsize(BountyPayText(b), FontKind.Ui600, 17, 120), px + 36, top + 28, FontKind.Ui600, 17, Pal.PaperInk);
                Gfx.Text(b.tier == 0 ? "Easy" : b.tier == 1 ? "Standard" : "Stretch", px + 36, top + 54, FontKind.Ui500, 16, Muted);
            }
            lastBoardBottom = MathF.Max(lastBoardBottom, top + rh);
        }
        // The run of days.
        float sy = ry + 3 * (rh + gap) + 2;
        int done = day.list.Count(b => b.paid);
        bool today = state.boardLastDay == state.day;
        string streak = state.boardStreak > 0 ? $"{state.boardStreak} day{(state.boardStreak == 1 ? "" : "s")} in a row{(today ? "" : " (do one today to keep it)")}" : "Do one a day to start a run of days";
        Gfx.Text(streak, rx, sy, FontKind.Ui600, 19, Pal.PaperInk);
        string best = $"Best run: {state.boardBest} · Done so far: {state.boardDone}" + (done == day.list.Count ? " · All three done today!" : "");
        Gfx.Text(Gfx.Ellipsize(best, FontKind.Ui500, 17, rw), rx, sy + 28, FontKind.Ui500, 17, Muted);
        lastBoardBottom = MathF.Max(lastBoardBottom, sy + 28 + 22);
        // Today's harbour news (Events.cs), wrapped to two lines at most.
        if (EventBoardLine() is string news)
        {
            var lines = Gfx.Wrap("Harbour news: " + news, FontKind.Note, 18, rw);
            if (lines.Count > 2) lines = new() { lines[0], Gfx.Ellipsize(string.Join(" ", lines.Skip(1)), FontKind.Note, 18, rw) };
            Lines(lines, rx, sy + 60, 24, FontKind.Note, 18, Pal.PaperInk);
            lastBoardBottom = MathF.Max(lastBoardBottom, sy + 60 + lines.Count * 24);
        }
    }
}
