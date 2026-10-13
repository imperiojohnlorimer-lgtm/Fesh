#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    static (QuizSession host, QuizSession guest) QuizPair()
    {
        var (a, b) = MemoryLink.Pair();
        var host = new QuizSession("Hosta", a, true);
        var guest = new QuizSession("Guesta", b, false);
        for (int i = 0; i < 4; i++) { host.Tick(0.01f); guest.Tick(0.01f); }
        return (host, guest);
    }
    static void QuizTicks(QuizSession h, QuizSession g, float secs, float dt = 0.1f)
    {
        for (float t = 0; t < secs; t += dt) { h.Tick(dt); g.Tick(dt); }
    }

    QuizSession qVs; DockClientCore qVisitor;
    void QTick() { float d = MathF.Min(0.05f, Raylib.GetFrameTime()); qVisitor?.Tick(d); qVs?.Tick(d); }

    // The Sea School Duel (1.30). FESH_QUIZ_TEST=1 runs this alone: the rules and the session without a screen, then the
    // game against an angler and against a person on the dock.
    IEnumerable<int> QuizScript()
    {
        Note("Quiz: the questions");
        Check($"the tournament set has plenty of questions ({QuizRules.Bank.Length})", QuizRules.Bank.Length >= 40);
        Check("every question has a prompt and four different, non-empty answers, in plain ASCII", QuizRules.Bank.All(q => q.Prompt.Length > 10 && q.Options.Length == 4 && q.Options.Distinct().Count() == 4 && q.Options.All(o => o.Length > 0) && (q.Prompt + string.Concat(q.Options)).All(c => c is >= ' ' and <= '~')));
        Check("no two questions are the same", QuizRules.Bank.Select(q => q.Prompt).Distinct().Count() == QuizRules.Bank.Length);
        var p1 = QuizRules.Plan(1234); var p2 = QuizRules.Plan(1234);
        Check("a seed gives the same eight questions, in the same order, with the same answer places", p1.Count == 8 && p1.Select(x => x.q).SequenceEqual(p2.Select(x => x.q)) && p1.Zip(p2).All(z => z.First.shown.SequenceEqual(z.Second.shown)));
        Check("the eight are all different and a different seed gives a different set", p1.Select(x => x.q).Distinct().Count() == 8 && !p1.Select(x => x.q).SequenceEqual(QuizRules.Plan(1235).Select(x => x.q)));
        var places = new int[4];
        for (int sd = 0; sd < 300; sd++) for (int k = 0; k < 8; k++) places[QuizRules.RightPlace(sd, k)]++;
        Check($"the right answer lands on every place about as often ({string.Join('/', places)})", places.All(c => c is > 480 and < 720));
        Check("IsRight and RightPlace agree, and no answer is never right", Enumerable.Range(0, 8).All(k => QuizRules.IsRight(77, k, QuizRules.RightPlace(77, k)) && !QuizRules.IsRight(77, k, (QuizRules.RightPlace(77, k) + 1) % 4) && !QuizRules.IsRight(77, k, -1)));
        Check("a score keeps right and time apart", QuizRules.Right(QuizRules.Score(6, 48200)) == 6 && QuizRules.TimeMs(QuizRules.Score(6, 48200)) == 48200 && QuizRules.Score(6, 90000) > QuizRules.Score(5, 1000) && QuizRules.Score(5, 1000) > QuizRules.Score(5, 2000));
        var A = (int c, int ms, bool r) => new QuizAnswer(c, ms, r);
        Check("more right wins; then less time; else a draw", QuizRules.Decide(new[] { A(0, 9000, true), A(0, 1, false) }, new[] { A(0, 1, false), A(0, 1, false) }) > 0
            && QuizRules.Decide(new[] { A(0, 5000, true) }, new[] { A(0, 3000, true) }) < 0 && QuizRules.Decide(new[] { A(0, 3000, true) }, new[] { A(0, 3000, true) }) == 0);

        Note("Quiz: the session");
        var (h, g) = QuizPair();
        Check("a guest's hello starts the host's game: one seed and one id on both sides", h.State == "play" && g.State == "play" && h.Seed == g.Seed && h.MatchId == g.MatchId && h.RivalName == "Guesta" && g.RivalName == "Hosta" && h.RecordId != g.RecordId);
        int seed = h.Seed;
        QuizTicks(h, g, 3.5f);
        for (int k = 0; k < 8; k++)
        {
            h.Answer(k, QuizRules.RightPlace(seed, k), 2000);            // always right
            g.Answer(k, (QuizRules.RightPlace(seed, k) + 1) % 4, 1500);  // always wrong, and quicker
            QuizTicks(h, g, 2.0f);
        }
        QuizTicks(h, g, 0.5f);
        Check("both sides finish with the same eight answers each", h.State == "over" && g.State == "over" && h.Mine.Count == 8 && h.Theirs.Count == 8 && g.Theirs.Count == 8 && h.Mine.SequenceEqual(g.Theirs) && h.Theirs.SequenceEqual(g.Mine));
        Check("right beats quick: the host wins and the guest loses, on both screens", h.Result == "win" && g.Result == "loss" && h.MyRight == 8 && h.TheirRight == 0 && g.EndWhy == "finished");
        // The same count: the quicker wins.
        var (h2, g2) = QuizPair(); QuizTicks(h2, g2, 3.5f);
        for (int k = 0; k < 8; k++) { h2.Answer(k, QuizRules.RightPlace(h2.Seed, k), k < 4 ? 3000 : 3000); g2.Answer(k, QuizRules.RightPlace(h2.Seed, k), 2500); QuizTicks(h2, g2, 3f); }
        Check("equal right: the lower total time wins", h2.State == "over" && h2.Result == "loss" && g2.Result == "win");
        var (h3, g3) = QuizPair(); QuizTicks(h3, g3, 15.5f);
        for (int k = 0; k < 8; k++) { h3.Answer(k, -1, 1); g3.Answer(k, -1, 1); QuizTicks(h3, g3, 15.5f); }
        Check("two sides who answer nothing draw (an unanswered question costs the whole fifteen seconds)", h3.State == "over" && h3.Result == "draw" && h3.Mine.All(a => a.Ms == 15000 && a.Choice == -1));
        Check("an answer for the wrong question, or after the end, is not accepted", !h2.Answer(0, 0, 1000) && !g2.Answer(8, 0, 1000));

        Note("Quiz: rematch and leaving");
        h.Rematch(); QuizTicks(h, g, 0.3f);
        Check("a rematch needs both", h.WaitingForRematch && h.State == "over");
        g.Rematch(); QuizTicks(h, g, 0.3f);
        Check("then a new game begins on a new seed and id, with nothing carried over", h.State == "play" && g.State == "play" && h.Seed == g.Seed && h.Seed != seed && h.MatchId == g.MatchId && h.Mine.Count == 0 && g.Theirs.Count == 0);
        g.Leave(); QuizTicks(h, g, 0.3f);
        Check("leaving mid-game gives the other the win", h.State == "over" && h.Result == "win" && h.EndWhy == "left" && g.Result == "loss");
        var (h4, g4) = QuizPair(); QuizTicks(h4, g4, 3.5f);
        for (int k = 0; k < 8; k++) { h4.Answer(k, 0, 1000); h4.Tick(0.1f); }
        for (int i = 0; i < 400; i++) { h4.Tick(0.1f); if (i % 8 == 0) g4.Tick(0.1f); }
        Check("someone who finishes while the other has stopped answering wins after the stall time", h4.State == "over" && (h4.EndWhy is "stalled" or "left") && h4.Result == "win");

        Note("Quiz: forged answers");
        foreach (var (name, k, choice, ms, upto) in new[]
        {
            ("out of order (question 3 first)", 3, 0, 2000, 0),
            ("a place that doesn't exist", 0, 7, 2000, 0),
            ("faster than anyone can read", 0, 1, 40, 0),
            ("more time than the game has had", 0, 1, 14000, 0),
            ("a no-answer that took no time", 0, -1, 500, 0),
        })
        {
            // The guest's session is bypassed: its message is made up.
            var (a2, b2) = MemoryLink.Pair();
            var host2 = new QuizSession("H", a2, true);
            b2.Send(new NetMsg { t = "qhello", v = SungkaSession.Protocol, name = "Forger" });
            host2.Tick(0.1f);
            for (float t = 0; t < 4; t += 0.1f) host2.Tick(0.1f);
            b2.Send(new NetMsg { t = "qans", id = host2.MatchId, n = new[] { k, choice, ms } });
            host2.Tick(0.1f);
            Check($"a forged answer is refused: {name}", host2.State == "over" && host2.EndWhy == "invalid" && host2.Result == "");
        }
        {
            var (a2, b2) = MemoryLink.Pair();
            var host2 = new QuizSession("H", a2, true);
            b2.Send(new NetMsg { t = "qhello", v = SungkaSession.Protocol, name = "Forger" });
            host2.Tick(0.1f);
            for (float t = 0; t < 20; t += 0.1f) host2.Tick(0.1f);
            b2.Send(new NetMsg { t = "qans", id = host2.MatchId, n = new[] { 0, 1, 2000 } });
            b2.Send(new NetMsg { t = "qans", id = host2.MatchId, n = new[] { 0, 1, 2000 } });
            host2.Tick(0.1f);
            Check("an answer to the same question twice is refused", host2.State == "over" && host2.EndWhy == "invalid");
            var (a3, b3) = MemoryLink.Pair();
            var host3 = new QuizSession("H", a3, true);
            b3.Send(new NetMsg { t = "qhello", v = SungkaSession.Protocol + 5, name = "Old" });
            host3.Tick(0.1f);
            Check("another version of the game is turned away", host3.State == "closed");
        }

        Note("Quiz: the anglers");
        var means = new Dictionary<string, float>();
        foreach (var rv in new[] { "bess", "marlo", "ivy" })
        {
            float tot = 0, ms = 0; int games = 40;
            for (int gi = 0; gi < games; gi++)
            {
                var (a, b) = MemoryLink.Pair();
                var hs = new QuizSession("Me", a, true); var bot = new QuizBot(rv == "bess" ? "Old Bess" : rv, rv, b);
                for (int i = 0; i < 6; i++) { hs.Tick(0.01f); bot.Tick(0.01f); }
                // I answer everything at once at 3.5 s; then the bot's whole game plays out.
                for (float t = 0; t < 4; t += 0.1f) { hs.Tick(0.1f); bot.Tick(0.1f); }
                for (int k = 0; k < 8; k++) hs.Answer(k, QuizRules.RightPlace(hs.Seed, k), 600);
                for (float t = 0; t < 120 && hs.State == "play"; t += 0.1f) { hs.Tick(0.1f); bot.Tick(0.1f); }
                tot += hs.TheirRight; ms += hs.Theirs.Sum(x => x.Ms) / 1000f;
                if (hs.State != "over" || hs.Theirs.Count != 8) { Check($"a game against {rv} ends", false); break; }
            }
            means[rv] = tot / games;
            Check($"{rv} is right {tot / games:0.0} of 8 and takes {ms / games / 8:0.0} s a question", true);
        }
        Check("the harder the angler, the more they get right", means["bess"] < means["marlo"] && means["marlo"] < means["ivy"] && means["bess"] > 2.5f && means["ivy"] < 7.9f);

        yield return 1;
        // ---------------- in the game ----------------
        Note("Quiz: against an angler, in the game");
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        MatchTestGame("Scholar");
        state.coins = 80; state.day = 4;
        yield return 4;
        Save();
        var realState = state; var realRng = rng;
        var diskBytes = File.ReadAllBytes(SaveFile.FilePath);
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        player.X = 266 + 9; player.Y = 198; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("E at an angler offers the Sea School Duel", dockAnglerMenu == "bess" && Gfx.Seen.ContainsKey("Sea School Duel"));
        ClickButton("Sea School Duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "quiz" && mode == "play" && quizSession != null && quizSession.State == "play"); i++) yield return 1;
        Check("it fades to the quiz with Old Bess as the other side", InQuiz && quizBot != null && match.Spec.Mode == "quiz" && match.FromDock && quizPeerName == "Old Bess" && quizSession.State == "play");
        Check("the adventure is untouched", ReferenceEquals(match.Real, realState) && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        pendingShot = "quiz-countdown"; yield return 2;
        for (int i = 0; i < 400 && quizShownK < 0; i++) yield return 1;
        Check("after the count, the first question is up", quizShownK == 0 && quizSession.PlayClock >= QuizSession.CountSecs);
        pendingShot = "quiz-question"; yield return 2;
        Check("each answer is a real, clickable button", Enumerable.Range(0, 4).All(i => Gfx.Seen.ContainsKey($"quizq:{i}")));
        int seedA = quizSession.Seed;
        // Answer all eight right: some by key, some by clicking the button.
        for (int k = 0; k < 8; k++)
        {
            for (int i = 0; i < 200 && !(quizShownK == k && quizFeedT <= 0 && quizQT > 0.4f); i++) yield return 1;
            int rp = QuizRules.RightPlace(seedA, k);
            if (k % 2 == 0) Inp.Tap(KeyboardKey.One + rp);
            else { ClickButton($"quizq:{rp}"); yield return 2; Inp.ScriptMouse = Offscreen; }
            yield return 2;
            if (k == 0) { Check("a right answer is marked for you", quizSession.Mine.Count == 1 && quizSession.Mine[0].Right && quizPick == rp); pendingShot = "quiz-feedback"; yield return 2; }
        }
        for (int i = 0; i < 100 && quizSession.Mine.Count < 8; i++) yield return 1;
        Check("eight answers in, all right, quickly", quizSession.Mine.Count == 8 && quizSession.MyRight == 8 && quizSession.Mine.Sum(a => a.Ms) < 20000);
        pendingShot = "quiz-waiting"; yield return 2;
        // The test would otherwise wait a minute for the angler: move both clocks on.
        quizSession.PlayClock += 100; quizBot.S.PlayClock += 100;
        for (int i = 0; i < 200 && quizSession.State != "over"; i++) yield return 1;
        Check("the angler finishes and the duel is yours: eight right in a few seconds", quizSession.State == "over" && quizSession.Result == "win" && quizSession.Theirs.Count == 8);
        pendingShot = "quiz-result"; yield return 3;
        Check("it is on the board as a win against Old Bess", Leaderboards.Board().entries.Any(e => e.mode == "quiz" && e.result == "win" && e.rival == "Old Bess" && QuizRules.Right(e.score) == 8));
        Check("the card offers a rematch, the dock and leaving", Gfx.Seen.ContainsKey("Rematch") && Gfx.Seen.ContainsKey("Back to the dock") && Gfx.Seen.ContainsKey("Leave"));
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 300 && !(quizSession.State == "play" && quizSession.Mine.Count == 0 && quizSession.Seed != seedA); i++) yield return 1;
        Check("a rematch starts a new game on a new seed", quizSession.State == "play" && quizSession.Seed != seedA && quizSession.Mine.Count == 0);
        for (int i = 0; i < 400 && quizShownK != 0; i++) yield return 1;
        for (int i = 0; i < 100 && quizQT < 0.5f; i++) yield return 1;
        int rp0 = QuizRules.RightPlace(quizSession.Seed, 0);
        Inp.Tap(KeyboardKey.One + (rp0 + 1) % 4); yield return 3;
        Check("a wrong answer is marked wrong and shows the right one", quizSession.Mine.Count == 1 && !quizSession.Mine[0].Right && quizPick == (rp0 + 1) % 4 && quizFeedT > 0);
        pendingShot = "quiz-wrong"; yield return 2;
        for (int i = 0; i < 100 && !(quizShownK == 1 && quizFeedT <= 0); i++) yield return 1;
        quizQT = 14.99f; quizSession.PlayClock += 15; quizBot.S.PlayClock += 15; yield return 6;
        Check("an unanswered question times out at fifteen seconds", quizSession.Mine.Count >= 2 && quizSession.Mine[1].Choice == -1 && quizSession.Mine[1].Ms == QuizRules.AnswerMs);
        string before = $"{match?.Phase}/{quizSession?.State}/{mode}/{quizSession?.Mine.Count}";
        Inp.Tap(KeyboardKey.Escape); yield return 1;
        string after1 = $"{match?.Phase}/{match?.LeaveAsk}/{mode}";
        yield return 2;
        Check($"Esc asks whether to leave (before {before}, after one frame {after1}, now phase {match.Phase}, mode {mode}, quiz {quizSession?.State}, paused? {mode == "pause"})", match.LeaveAsk);
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("leaving goes back to the dock and counts as a forfeit", match.Phase == "dock" && quizSession == null && Leaderboards.Board().entries.Any(e => e.mode == "quiz" && e.result == "forfeit"));
        Inp.Tap(KeyboardKey.Escape); yield return 3; ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("leaving the dock puts the adventure back as it was", !MatchActive && ReferenceEquals(state, realState) && ReferenceEquals(rng, realRng) && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));

        // --- With a person ---
        Note("Quiz: against someone on the dock");
        lanDiscoveryPort = 47890; lanFirstPort = 47891; lanLoopbackOnly = true;
        MatchTestGame("Hostq");
        state.coins = 50; state.day = 3;
        yield return 4;
        Save();
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Host a dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 400 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        var vConn = new LanConnect("127.0.0.1", dockListener.Port);
        ILink vLink = null; for (int i = 0; i < 300 && vLink == null; i++) { vLink = vConn.Poll(); yield return 1; }
        var visitor = new DockClientCore(vLink, 6, 4, new Look { skin = 1, shirt = 2, hat = 1 });
        qVisitor = visitor; qVs = null; testEachFrame = QTick;
        for (int i = 0; i < 120 && !(visitor.State == "in" && dockClient.Roster.Count == 2); i++) yield return 1;
        var vp = dockClient.Others.First();
        visitor.SetPose(player.X + 12, player.Y, 3, 0);
        for (int i = 0; i < 80; i++) yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the challenge menu offers the Sea School Duel", dockChallengeTarget == vp.Id && Gfx.Seen.ContainsKey("Sea School Duel"));
        pendingShot = "quiz-challenge-menu"; yield return 2;
        ClickButton("Sea School Duel"); yield return 3; Inp.ScriptMouse = Offscreen;
        DockEvent qInv = null; for (int i = 0; i < 120 && qInv == null; i++) { qInv = visitor.Events.FirstOrDefault(x => x.Kind == "invite"); yield return 1; }
        Check("the visitor is asked to a Sea School Duel", qInv != null && qInv.Mode == 3);
        visitor.Reply(qInv.Ch, true);
        DockEvent qGo = null; for (int i = 0; i < 120 && qGo == null; i++) { qGo = visitor.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        qVs = new QuizSession("Visitor", qGo.Link, host: false);
        for (int i = 0; i < 300 && !(InQuiz && quizSession.State == "play" && qVs.State == "play"); i++) yield return 1;
        Check("you host the duel, they are the guest, one seed on both sides", InQuiz && quizSession.Host && quizBot == null && qVs.Seed == quizSession.Seed && qVs.MatchId == quizSession.MatchId && quizPeerName == vp.Name);
        int seedB = quizSession.Seed;
        // The visitor answers a little slower than you, getting two wrong.
        int vk = 0; float vNext = 0;
        for (int i = 0; i < 400 && quizShownK != 0; i++) yield return 1;
        for (int k = 0; k < 8; k++)
        {
            for (int i = 0; i < 300 && !(quizShownK == k && quizFeedT <= 0 && quizQT > 0.5f); i++)
            {
                yield return 1;
                if (vk < 8 && qVs.PlayClock >= QuizSession.CountSecs + 1.2f * (vk + 1)) { qVs.Answer(vk, vk is 2 or 5 ? (QuizRules.RightPlace(seedB, vk) + 1) % 4 : QuizRules.RightPlace(seedB, vk), 1200); vk++; }
            }
            Inp.Tap(KeyboardKey.One + QuizRules.RightPlace(seedB, k)); yield return 2;
        }
        for (int i = 0; i < 400 && !(quizSession.State == "over" && qVs.State == "over"); i++)
        {
            yield return 1;
            if (vk < 8 && qVs.PlayClock >= QuizSession.CountSecs + 1.2f * (vk + 1)) { qVs.Answer(vk, QuizRules.RightPlace(seedB, vk), 1200); vk++; }
        }
        Check($"both sides finish ({quizSession.State}/{quizSession.EndWhy}/{quizSession.Notice}, {quizSession.Mine.Count}+{quizSession.Theirs.Count})", quizSession.State == "over" && qVs.State == "over" && quizSession.Theirs.Count == 8);
        Check("the same winner on both screens: you, eight right to six", quizSession.Result == "win" && qVs.Result == "loss" && quizSession.MyRight == 8 && quizSession.TheirRight == 6 && qVs.MyRight == 6);
        Check("it is on your board once, against their name, with no prize", Leaderboards.Board().entries.Count(e => e.mode == "quiz" && e.rival == vp.Name && e.result == "win") == 1);
        pendingShot = "quiz-lan-result"; yield return 3;
        // They leave in the next game.
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen;
        qVs.Rematch();
        for (int i = 0; i < 300 && !(quizSession.State == "play" && quizSession.Seed != seedB && qVs.State == "play"); i++) yield return 1;
        Check("a rematch needs both and begins a new game", quizSession.State == "play" && quizSession.Seed == qVs.Seed && quizSession.Seed != seedB);
        qVs.Leave();
        for (int i = 0; i < 120 && quizSession.State != "over"; i++) yield return 1;
        Check("when they leave, the duel is over with the win for you", quizSession.State == "over" && quizSession.Result == "win" && quizSession.EndWhy == "left");
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("Leave goes straight to the island", !MatchActive && quizSession == null);
        testEachFrame = null; qVs = null;
        visitor.Close();
    }
}
#endif
