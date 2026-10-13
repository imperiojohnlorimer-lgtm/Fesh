using Raylib_cs;

namespace Fesh;

// The Sea School Duel in the game (1.30): started from the Tournament Dock by a challenge or by talking to one of the three
// anglers (who answer the way their skill says, over an in-memory link, so playing alone is the same code as playing a
// person). Eight questions, the same for both; the screen is one card over the darkened water. QuizCore.cs is the rules and
// the session; nothing here touches the adventure (a match runs in its own state).
partial class Game
{
    QuizSession quizSession;
    QuizBot quizBot;
    string quizPeerName = "", quizMatchId = "";
    int quizCh;
    bool quizRecorded;
    float quizQT, quizFeedT;                 // seconds on this question; seconds of feedback still to show
    int quizShownK = -1, quizPick = -2;

    bool InQuiz => MatchActive && match.Phase == "quiz" && quizSession != null;

    /* ---------- Starting ---------- */
    void StartDockQuizAi(string rivalId)
    {
        if (!InDock) return;
        var sk = MatchRules.RivalById(rivalId);
        quizPeerName = sk.Name;
        var (a, b) = MemoryLink.Pair();
        quizSession = new QuizSession(LanName, a, host: true);
        quizCh = 0;
        var spec = new MatchSpec { Mode = "quiz", Seed = 0, Rival = rivalId, Label = $"Sea School Duel · against {sk.Name}", Duration = 0 };
        int g = ++match.Gen;
        match.Phase = "ready";
        FadeThrough(() =>
        {
            if (match == null || match.Gen != g) return;
            EnterQuizChild(spec);
            quizBot = new QuizBot(sk.Name, rivalId, b);
        }, () => { if (match == null || match.Gen != g) return; match.Phase = "quiz"; });
    }

    void StartDockQuiz(DockEvent ev)
    {
        dockPending = 0; dockInvite = null; dockChallengeTarget = -1;
        if (!InDock || ev.Link == null) { dockClient?.Done(ev.Ch); return; }
        var peer = dockClient.Roster.FirstOrDefault(p => p.Id == ev.From);
        quizPeerName = peer?.Name ?? "Someone";
        quizSession = new QuizSession(LanName, ev.Link, host: ev.Role == 0);
        quizCh = ev.Ch; quizBot = null;
        var spec = new MatchSpec { Mode = "quiz", Seed = 0, Rival = "net", Label = $"Sea School Duel · against {quizPeerName}", Duration = 0 };
        int g = ++match.Gen;
        match.Phase = "ready";
        FadeThrough(() => { if (match != null && match.Gen == g) EnterQuizChild(spec); },
            () => { if (match == null || match.Gen != g) return; match.Phase = "quiz"; });
    }

    void EnterQuizChild(MatchSpec spec)
    {
        match.FromDock = true;
        ResetMatchCounters(spec);
        ResetMatchPlace();
        quizRecorded = false; quizMatchId = ""; quizShownK = -1; quizPick = -2; quizFeedT = 0; quizQT = 0;
    }

    /* ---------- Every frame ---------- */
    void UpdateQuizDuel(float dt)
    {
        var s = quizSession;
        quizBot?.Tick(dt);
        s.Tick(dt);
        player.Moving = false;
        if (quizMatchId != s.MatchId) { quizMatchId = s.MatchId; quizRecorded = false; quizShownK = -1; quizPick = -2; quizFeedT = 0; quizQT = 0; }
        if (s.State == "over") { FinishQuiz(); return; }
        if (s.State != "play") return;
        if (s.PlayClock < QuizSession.CountSecs) return;
        if (quizFeedT > 0) { quizFeedT -= dt; return; }
        int k = s.Mine.Count;
        if (k >= QuizRules.Count) return;
        if (quizShownK != k) { quizShownK = k; quizQT = 0; quizPick = -2; }
        quizQT += dt;
        if (quizQT * 1000 >= QuizRules.AnswerMs) PickAnswer(-1);
    }

    // The place chosen on the current question (-1: out of time).
    void PickAnswer(int choice)
    {
        var s = quizSession;
        if (s == null || s.State != "play" || quizFeedT > 0 || s.PlayClock < QuizSession.CountSecs) return;
        int k = s.Mine.Count;
        if (k >= QuizRules.Count || quizShownK != k) return;
        if (choice >= 0 && quizQT < QuizRules.MinMs / 1000f) return;            // a key still down from before is not an answer
        if (!s.Answer(k, choice, (int)(quizQT * 1000))) return;
        quizPick = choice;
        quizFeedT = 0.9f;
        Sfx.Play(s.Mine[^1].Right ? "right" : "wrong");
    }

    void FinishQuiz()
    {
        if (quizRecorded) return;
        quizRecorded = true;
        var s = quizSession;
        string r = s.Result;
        if (r != "")
        {
            bool left = s.EndWhy == "left" && r == "loss";
            int right = s.MyRight, ms = s.Mine.Sum(a => a.Ms) + (QuizRules.Count - s.Mine.Count) * QuizRules.AnswerMs;
            Leaderboards.Add(new BoardEntry
            {
                id = s.RecordId, mode = "quiz", label = $"Sea School Duel · against {quizPeerName}", seed = s.Seed, ruleset = MatchRules.Version, rival = quizPeerName,
                result = left ? "forfeit" : r, date = DateTime.Now.ToString("o"), score = QuizRules.Score(right, ms), perfects = 0, catches = s.TheirRight, casts = right
            });
        }
        Sfx.Play(r == "win" ? "rare" : r == "loss" ? "fail" : "coin");
        SetPrompt("");
    }

    void ShutQuiz()
    {
        if (quizSession != null && quizSession.State == "play" && !quizRecorded) { quizSession.Leave(); FinishQuiz(); }
        quizSession?.Leave();
        quizSession = null; quizBot = null;
        if (quizCh != 0) dockClient?.Done(quizCh);
        quizCh = 0;
    }

    void QuizKeys()
    {
        var s = quizSession;
        if (s.State == "over") { if (Inp.Pressed(KeyboardKey.Enter)) s.Rematch(); else if (BackPressed()) LeaveMatch(); return; }
        if (BackPressed()) { match.LeaveAsk = true; return; }
        if (DigitPressed() is int d && d < 4) PickAnswer(d);
    }

    /* ---------- Drawing ---------- */
    void DrawQuizHud()
    {
        var s = quizSession;
        if (s.State == "over") { DrawQuizResult(); return; }
        Backdrop();
        const float w = 1000, h = 620;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2 - 10;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Sea School Duel", x + 30, y + 18, FontKind.Ui700, 26, Pal.PaperInk);
        // Progress: yours (right or wrong, for you only) and theirs (answered, nothing more).
        void Dots(float dx, bool right, string name, IList<QuizAnswer> list, bool mine)
        {
            string label = mine ? "You" : name;
            float lw = Gfx.Measure(label, FontKind.Ui600, 16);
            float total = QuizRules.Count * 20;
            float sx = right ? dx - total : dx;
            Gfx.Text(Gfx.Ellipsize(label, FontKind.Ui600, 16, 200), right ? dx - Math.Min(lw, 200) : dx, y + 18, FontKind.Ui600, 16, Muted);
            for (int i = 0; i < QuizRules.Count; i++)
            {
                Color c = i >= list.Count ? Pal.C("rgba(16,36,58,0.18)") : !mine ? Pal.Lantern : list[i].Right ? Pal.C("#2f7d3a") : Rust;
                Gfx.Rect(sx + i * 20, y + 44, 16, 16, c, 3);
            }
        }
        Dots(x + 330, false, "You", s.Mine, true);
        Dots(x + w - 30, true, quizPeerName, s.Theirs, false);
        if (s.PlayClock < QuizSession.CountSecs)
        {
            int n = (int)MathF.Ceiling(QuizSession.CountSecs - s.PlayClock);
            Gfx.TextCenter("Get ready", x + w / 2, y + 190, FontKind.Ui700, 40, Pal.PaperInk);
            Gfx.TextCenter(n.ToString(), x + w / 2, y + 250, FontKind.Ui700, 150, Pal.PaperInk);
            return;
        }
        int k = quizFeedT > 0 ? s.Mine.Count - 1 : s.Mine.Count;
        if (k >= QuizRules.Count)
        {
            Gfx.TextCenter("All eight answered.", x + w / 2, y + 220, FontKind.Ui700, 36, Pal.PaperInk);
            Gfx.TextCenter($"Waiting for {quizPeerName}: {s.Theirs.Count} of {QuizRules.Count} answered.", x + w / 2, y + 280, FontKind.Ui600, 22, Muted);
            return;
        }
        var plan = QuizRules.Plan(s.Seed)[k];
        var q = QuizRules.Bank[plan.q];
        Gfx.Text($"Question {k + 1} of {QuizRules.Count}", x + 30, y + 74, FontKind.Ui600, 18, Muted);
        // The clock for this question.
        float frac = quizFeedT > 0 ? 0 : Math.Clamp(1 - quizQT * 1000 / QuizRules.AnswerMs, 0, 1);
        Gfx.Rect(x + w - 30 - 260, y + 80, 260, 12, Pal.C("rgba(16,36,58,0.15)"), 6);
        if (frac > 0) Gfx.Rect(x + w - 30 - 260, y + 80, 260 * frac, 12, frac < 0.25f ? Rust : Pal.Buoy, 6);
        var lines = Gfx.Wrap(q.Prompt, FontKind.Ui700, 28, w - 60);
        Lines(lines, x + 30, y + 110, 36, FontKind.Ui700, 28, Pal.PaperInk);
        float by = y + 110 + lines.Count * 36 + 22;
        int right = QuizRules.RightPlace(s.Seed, k);
        bool feedback = quizFeedT > 0;
        for (int i = 0; i < 4; i++)
        {
            string text = $"{i + 1}   " + q.Options[plan.shown[i]];
            Color bg = Pal.Sand, fg = Pal.Ink;
            if (feedback) { if (i == right) { bg = Pal.C("#2f7d3a"); fg = White; } else if (i == quizPick) { bg = Rust; fg = White; } }
            float bh = 62;
            string shown = Gfx.Ellipsize(text, FontKind.Ui600, 22, w - 110);
#if DEBUG
            Gfx.Seen[$"quizq:{i}"] = new Rectangle(x + 30, by, w - 60, bh);
#endif
            if (Button(shown, x + 30, by, w - 60, bh, FontKind.Ui600, 22, bg, fg, 5, 3, 6, !feedback)) { PickAnswer(i); return; }
            by += bh + 10;
        }
    }

    void DrawQuizResult()
    {
        var s = quizSession;
        Backdrop();
        const float w = 1000, h = 640;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        string r = s.Result;
        string title = r == "win" ? "You win the duel!" : r == "loss" ? $"{quizPeerName} wins" : r == "draw" ? "A draw!" : "No result";
        var tone = r == "win" ? Pal.C("#2f7d3a") : r == "loss" ? Rust : Pal.PaperInk;
        Gfx.TextCenter(title, x + w / 2, y + 18, FontKind.Ui700, 42, tone);
        int myMs = s.Mine.Sum(a => a.Ms), thMs = s.Theirs.Sum(a => a.Ms);
        string sub = r == "" || s.EndWhy is "left" or "stalled" ? s.Notice : $"{s.MyRight} right in {myMs / 1000f:0.0} s  ·  {quizPeerName}: {s.TheirRight} right in {thMs / 1000f:0.0} s";
        Gfx.TextCenter(Gfx.Ellipsize(sub, FontKind.Ui600, 20, w - 60), x + w / 2, y + 74, FontKind.Ui600, 20, Muted);
        float ty = y + 112;
        Gfx.Text("Question and the right answer", x + 30, ty, FontKind.Ui700, 16, Muted);
        Gfx.Text("You", x + w - 330, ty, FontKind.Ui700, 16, Muted);
        Gfx.Text(Gfx.Ellipsize(quizPeerName, FontKind.Ui700, 16, 150), x + w - 170, ty, FontKind.Ui700, 16, Muted);
        var plan = QuizRules.Plan(s.Seed);
        for (int k = 0; k < QuizRules.Count; k++)
        {
            float ry = ty + 28 + k * 46;
            if (k % 2 == 0) Gfx.Rect(x + 16, ry - 4, w - 32, 44, Pal.C("rgba(16,36,58,0.06)"), 4);
            var q = QuizRules.Bank[plan[k].q];
            Gfx.Text(Gfx.Ellipsize($"{k + 1}. {q.Prompt}", FontKind.Ui600, 14, w - 400), x + 30, ry - 1, FontKind.Ui600, 14, Pal.PaperInk);
            Gfx.Text(Gfx.Ellipsize(q.Options[0], FontKind.Ui500, 14, w - 400), x + 50, ry + 17, FontKind.Ui500, 14, Pal.C("#2f7d3a"));
            void Cell(float cx, IList<QuizAnswer> l)
            {
                if (k >= l.Count) { Gfx.Text("-", cx, ry + 8, FontKind.Ui600, 16, Muted); return; }
                var a = l[k];
                string t = a.Choice < 0 ? "No answer" : $"{(a.Right ? "Right" : "Wrong")}  {a.Ms / 1000f:0.0} s";
                Gfx.Text(t, cx, ry + 8, FontKind.Ui700, 16, a.Right ? Pal.C("#2f7d3a") : Rust);
            }
            Cell(x + w - 330, s.Mine); Cell(x + w - 170, s.Theirs);
        }
        float by = y + h - 78, bx = x + 30;
        bool can = !s.PeerGone && s.EndWhy == "finished" && !s.WaitingForRematch;
        string again = s.WaitingForRematch ? $"Waiting for {quizPeerName}..." : "Rematch";
        if (Button(again, bx, by, BigW(again), 56, FontKind.Ui700, 24, can ? Pal.Buoy : Pal.C("#d8cbb0"), can ? White : Muted, 5, 3, 6, can)) { s.Rematch(); return; }
        bx += BigW(again) + 14;
        if (BigButton("Back to the dock", bx, by, false)) { LeaveMatch(); return; }
        if (BigButton("Leave", x + w - 30 - BigW("Leave"), by, false)) { LeaveDockFromResult(); return; }
    }
}
