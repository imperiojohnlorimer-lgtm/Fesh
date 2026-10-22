using Raylib_cs;

namespace Fesh;

// First steps (1.37): a short forced tutorial for a brand-new fisher, and only for them (StartGame(true) sets
// state.tour to 0; every older or loaded save has 99, "done"). Seven steps, each finished by doing it: walking, meeting
// Tomas, the first fish, the bag, selling to Pip, the map and the journal. It rides on the guide: while it runs, the
// goal card and the arrow show only the current step (even with the guide turned off), and the journal lists just that
// step. Every step is worked out from the save (or a tut: flag), so a game closed halfway picks up at the right step, and
// one already done by the time it's reached (the fish caught before Tomas, a map opened early) is ticked off at once.
// The pause menu has a Skip, so nobody is ever stuck.
partial class Game
{
    const int TourDone = 99;
    float tourWalked, tourLastX = float.NaN, tourLastY;
    static bool tourOff;   // the autotest's games skip it (the scripts that test it turn it back on)

    sealed record TourStep(string Id, string Title, string Text, Func<bool> Done, Func<Goal, Goal> Place);
    List<TourStep> tourSteps;

    bool TourActive => state.tour < TourDone && !MatchActive && mode is not ("title" or "create");

    List<TourStep> TourSteps => tourSteps ??= new()
    {
        new("walk", "Walk around", "Use <move> to walk. Try the beach and the grass.", () => tourWalked >= 60, null),
        new("tomas", "Meet Tomas", "Walk up to Tomas, by his hut, and press <act> to talk to him.", () => state.flags.metTomas, g => AtTomas(g)),
        new("fish", "Catch your first fish", "Face the lagoon's water. Hold <act> and let go to cast. When the float dips, press <act>, then hold <act> to keep the fish in the green bar.",
            () => state.commons.Values.Sum() > 0 || state.caught.Count > 0, g => AtSpot(g, "lagoon")),
        new("bag", "Open your bag", "Press <bag>. Your catch is in it. Food can be eaten from the bag, and fish sold to Pip.", () => state.Hinted("tut:bag"), null),
        new("sell", "Sell a fish to Pip", "Pip's stall is just east of Tomas's camp. Face it, press <act>, and sell your catch for coins.",
            () => state.Hinted("tut:sell") || FishCount() == 0 || !PipOpen, g => At(g, PipX, PipY + 14, "Pip's stall")),
        new("map", "Open the map", "Press <map>. Click the chart to drop a pin: the arrow will point you to it.", () => state.Hinted("tut:map"), null),
        new("journal", "Open your journal", "Press <journal>. It lists what to do next. Follow a goal and the arrow points the way.", () => state.Hinted("tut:journal"), null)
    };

    // A new fisher starts at the first step (not in the autotest's games, unless a script asks).
    void StartTour(bool fresh)
    {
        tourSteps = null; tourWalked = 0; tourLastX = float.NaN;
        if (fresh && !tourOff) state.tour = 0;
    }

    Goal TourGoal()
    {
        var st = TourSteps[Math.Clamp(state.tour, 0, TourSteps.Count - 1)];
        var g = NewGoal("tour:" + st.Id, "story", $"{state.tour + 1}/{TourSteps.Count} {st.Title}", st.Text);
        return st.Place != null ? st.Place(g) : g;
    }

    // Ticks off what's done, in order. Called every frame from UpdateGuide while the tutorial is running.
    void UpdateTour()
    {
        if (mode == "play")
        {
            if (!float.IsNaN(tourLastX))
            {
                float d = Dist(player.X, player.Y, tourLastX, tourLastY);
                if (d < 8) tourWalked += d;   // a jump (a door, a fade) isn't walking
            }
            (tourLastX, tourLastY) = (player.X, player.Y);
        }
        else tourLastX = float.NaN;
        bool moved = false;
        while (state.tour < TourSteps.Count && TourSteps[state.tour].Done())
        {
            var st = TourSteps[state.tour];
            if (st.Id == "sell" && !state.Hinted("tut:sell") && FishCount() > 0) Toast("Pip's stall is shut at this hour. Sell your fish to Pip when it opens at 7.", 5);
            goalDoneTitle = st.Title; goalDoneT = 3.5f;
            state.tour++;
            moved = true;
        }
        if (state.tour >= TourSteps.Count)
        {
            state.tour = TourDone;
            ToastLater("That's the basics. Your journal (<journal>) always shows what to do next.");
        }
        if (moved && mode == "play") { Sfx.Play("ui"); Save(); }
    }

    // The pause menu's way out, for anyone the tutorial isn't working for.
    void SkipTour()
    {
        state.tour = TourDone;
        goalDoneT = 0;
        Save();
        Toast("First steps skipped. Your journal (<journal>) has everything.", 4);
    }
}

// The First steps banner (1.37): bigger and bolder than the goal card it replaces, so it can't be mistaken for an
// optional hint, with a pip for each step.
partial class Game
{
    // Something the current step doesn't allow yet: a nudge back to it. True when blocked.
    bool TourBlocked(int fromStep, string why)
    {
        if (!TourActive || state.tour >= fromStep) return false;
        Sfx.Play("nope");
        Toast(why, 4);
        return true;
    }

    // The panels, in the order the steps open them; the rest wait until First steps are done.
    bool TourAllowsPanel(string which)
    {
        if (!TourActive) return true;
        int from = which switch { "bag" => 3, "map" => 5, "journal" => 6, _ => 99 };
        return state.tour >= from;
    }

    void DrawTourBanner()
    {
        guideCardBottom = 0;
        if (!GuideShown) return;
        const float pad = 14;
        float x = CardX, y = CardY, w = CardW;
        int n = TourSteps.Count, cur = Math.Clamp(state.tour, 0, n - 1);
        if (goalDoneT > 0 && goalDoneTitle != null)
        {
            float dh = 86;
            Gfx.Box(x, y, w, dh, Pal.C("rgba(32,90,48,0.94)"), Pal.C("#7fd36b"), 3, 6);
            Gfx.Text("Step done!", x + pad, y + pad - 2, FontKind.Ui700, 22, Pal.C("#bff2b0"));
            Gfx.Text(Gfx.Ellipsize(goalDoneTitle, FontKind.Ui600, 18, w - 2 * pad), x + pad, y + pad + 30, FontKind.Ui600, 18, Pal.Paper);
            guideCardBottom = y + dh;
            Gfx.Block(x, y, w, dh);
            return;
        }
        var g = tracked;
        if (g == null) return;
        var text = Gfx.Wrap(Bind.Fix(g.Text), FontKind.Ui500, 16, w - 2 * pad);
        var how = guideWay?.How is string hw ? Gfx.Wrap(Bind.Fix(hw), FontKind.Ui600, 15, w - 2 * pad - 16) : new List<string>();
        float h = pad + 20 + 14 + 30 + Math.Max(3, text.Count) * 21 + (how.Count > 0 ? 6 + how.Count * 19 : 0) + pad;
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 4);
        Gfx.Box(x, y, w, h, NavyStrong, Pal.WithAlpha(Pal.Lantern, 0.7f + 0.3f * pulse), 3, 6);
        Gfx.Text("FIRST STEPS", x + pad, y + pad - 3, FontKind.Ui700, 14, Pal.Lantern);
        // A pip for each step: done, this one (breathing), still to come.
        float px = x + w - pad - n * 18;
        for (int i = 0; i < n; i++)
            Gfx.Rect(px + i * 18, y + pad, 12, 12, i < cur ? Pal.C("#7fd36b") : i == cur ? Pal.WithAlpha(Pal.Lantern, 0.6f + 0.4f * pulse) : Pal.C("rgba(255,255,255,0.18)"), 3);
        float ty = y + pad + 24;
        GoalMarker(x + pad + 8, ty + 10, 0.75f);
        Gfx.Text(Gfx.Ellipsize(g.Title.Substring(g.Title.IndexOf(' ') + 1), FontKind.Ui700, 22, w - 2 * pad - 26), x + pad + 26, ty, FontKind.Ui700, 22, Pal.Paper);
        ty += 34;
        Lines(text, x + pad, ty, 21, FontKind.Ui500, 16, Pal.C("#dcecf2"));
        ty += Math.Max(3, text.Count) * 21;
        if (how.Count > 0)
        {
            ty += 6;
            Gfx.Triangle(x + pad + 1, ty + 4, x + pad + 1, ty + 14, x + pad + 9, ty + 9, Pal.C("#bff4ff"));
            Lines(how, x + pad + 16, ty, 19, FontKind.Ui600, 15, Pal.C("#bff4ff"));
        }
        guideCardBottom = y + h;
#if DEBUG
        Gfx.Seen["guide card"] = new Rectangle(x, y, w, h);
#endif
        if (state.tour == 6 && mode == "play" && Gfx.Click(x, y, w, h)) { TogglePanel("journal"); return; }
        Gfx.Block(x, y, w, h);
    }
}

// The dark highlight (1.37): while a step points at somewhere in view, the rest of the world is dimmed round it, and a
// step that wants a HUD button (the bag, the map) dims the world and rings that button.
partial class Game
{
    readonly Dictionary<string, Rectangle> tourBtn = new();
    string TourButton => TourActive ? state.tour switch { 3 => "Bag", 5 => "Map", 6 => "Journal", _ => null } : null;

    // Drawn first in DrawUi, so the HUD, the prompt and the toasts stay bright over it.
    void DrawTourSpotlight()
    {
        if (!TourActive || mode != "play" || MatchActive || !GuideShown) return;
        float lw = Gfx.LW, lh = Gfx.LH;
        if (TourButton != null) { Gfx.Rect(0, 0, lw, lh, Pal.Rgba(4, 12, 26, 0.42f)); return; }
        if (guideWay == null || tracked?.HasPoint != true) return;
        float k = ViewK, xc = (guideWay.X - camX) * k, yc = (guideWay.Y - camY) * k - 22;
        if (!(xc > 0 && xc < lw && yc > 0 && yc < lh)) return;   // out of view: the edge arrow does the pointing
        float breathe = Calm ? 0 : 5 * MathF.Sin(time * 3);
        // Three bands of shade, each outside a wider circle, so the light falls off softly.
        foreach (var (r, a) in new[] { (78f + breathe, 0.17f), (112f + breathe, 0.17f), (150f + breathe, 0.17f) })
            for (float y = 0; y < lh; y += 6)
            {
                float dy = MathF.Abs(yc - (y + 3));
                var c = Pal.Rgba(4, 12, 26, a);
                if (dy >= r) { Gfx.Rect(0, y, lw, 6, c); continue; }
                float hw = MathF.Sqrt(r * r - dy * dy);
                if (xc - hw > 0) Gfx.Rect(0, y, xc - hw, 6, c);
                if (xc + hw < lw) Gfx.Rect(xc + hw, y, lw - xc - hw, 6, c);
            }
    }

    // After the HUD: a gold ring on the button the step wants.
    void DrawTourButtonRing()
    {
        if (TourButton is not string name || mode != "play" || !tourBtn.TryGetValue(name, out var r)) return;
        float pulse = Calm ? 1 : 0.5f + 0.5f * MathF.Sin(time * 5);
        Gfx.Box(r.X - 5, r.Y - 5, r.Width + 10, r.Height + 10, Pal.Rgba(0, 0, 0, 0), Pal.WithAlpha(Pal.Lantern, 0.6f + 0.4f * pulse), 4, 8);
        float cx = r.X + r.Width / 2, bob = Calm ? 0 : 3 * MathF.Sin(time * 5);
        if (r.Y > Gfx.LH / 2)
        {
            // A button low on the screen (the bag on the hotbar): the arrow sits above it, pointing down.
            float ay = r.Y - 14 - bob;
            Gfx.Triangle(cx - 9, ay - 12, cx + 9, ay - 12, cx, ay, Pal.Ink);
            Gfx.Triangle(cx - 7, ay - 10, cx + 7, ay - 10, cx, ay - 3, Pal.Lantern);
            return;
        }
        float ty = r.Y + r.Height + 12 + bob;
        Gfx.Triangle(cx - 9, ty + 12, cx + 9, ty + 12, cx, ty, Pal.Ink);
        Gfx.Triangle(cx - 7, ty + 10, cx + 7, ty + 10, cx, ty + 3, Pal.Lantern);
    }
}
