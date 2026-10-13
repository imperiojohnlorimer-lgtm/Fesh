#if DEBUG
namespace Fesh;

partial class Game
{
    static (DerbySession host, DerbySession guest, MemoryLink hl, MemoryLink gl) DerbyPair()
    {
        var (a, b) = MemoryLink.Pair();
        var host = new DerbySession("Hosta", a, true);
        var guest = new DerbySession("Guesta", b, false);
        for (int i = 0; i < 3; i++) { host.Tick(0.01f, 0); guest.Tick(0.01f, 0); }
        return (host, guest, a, b);
    }

    // Runs a handshake to the start of a match with the given seed; returns once both are in the run.
    static void DerbyStart(DerbySession host, DerbySession guest, int seed)
    {
        host.HostBegin(seed);
        for (int i = 0; i < 3; i++) { host.Tick(0.01f, 0); guest.Tick(0.01f, 0); }
        guest.GuestReady();
        for (int i = 0; i < 3; i++) { host.Tick(0.01f, 0); guest.Tick(0.01f, 0); }
    }

    // What a player who lands a fish with a given perfect-hook luck on casts 0.. scores, and the landing times.
    static (MatchScore score, List<(int cast, bool perfect, float t)> log) DerbyPlay(int seed, int landings, int perfectEvery)
    {
        var s = new MatchScore(); var log = new List<(int, bool, float)>();
        for (int i = 0; i < landings; i++)
        {
            int cast = i + (i / 3);           // some casts lose their fish
            bool perfect = perfectEvery > 0 && i % perfectEvery == 0;
            var plan = MatchRules.Plan(seed, cast);
            s.Cg += MatchRules.Centi(plan.FishId, plan.Size, perfect); s.Catches++; if (perfect) s.Perfects++; s.Casts = cast + 1;
            log.Add((cast, perfect, DerbySession.EarliestLanding(cast) + 3f));
        }
        return (s, log);
    }

    // The LAN Derby Duel's referee and its checks (1.30). FESH_DERBYNET_TEST=1 runs only this.
    IEnumerable<int> DerbyNetScript()
    {
        Note("Derby net: the handshake");
        var (host, guest, hl, gl) = DerbyPair();
        Check("hello gets the host waiting to begin, with the guest's name", host.State == "wait" && host.RivalName == "Guesta");
        DerbyStart(host, guest, 4321);
        Check("the host begins, the guest says ready, and both are running the same seed and match", host.State == "run" && guest.State == "run" && host.Seed == 4321 && guest.Seed == 4321 && host.MatchId == guest.MatchId && host.MatchId.Length == 32 && guest.RivalName == "Hosta");
        Check("each has its own record id", host.RecordId.Length == 32 && guest.RecordId.Length == 32 && host.RecordId != guest.RecordId);

        Note("Derby net: a game");
        var (hs, hlog) = DerbyPlay(4321, 7, 2);
        var (gs, glog) = DerbyPlay(4321, 5, 0);
        float clock = 0;
        foreach (var (cast, perfect, t) in hlog) { clock = Math.Max(clock, t); host.ReportLanding(cast, perfect, t); }
        foreach (var (cast, perfect, t) in glog) { clock = Math.Max(clock, t); guest.ReportLanding(cast, perfect, t); }
        for (int i = 0; i < 3; i++) { host.Tick(0.01f, clock); guest.Tick(0.01f, clock); }
        Check($"each side sees the other's running total, worked out from the seed ({guest.TheirScore.Kg} kg and {host.TheirScore.Kg} kg)", guest.TheirScore.Cg == hs.Cg && host.TheirScore.Cg == gs.Cg && guest.TheirScore.Catches == 7 && host.TheirScore.Catches == 5 && guest.TheirScore.Perfects == hs.Perfects);
        host.ReportEnd(hs); guest.ReportEnd(gs);
        for (int i = 0; i < 3; i++) { host.Tick(0.01f, 181); guest.Tick(0.01f, 181); }
        Check("both end the game with the same figures and complementary results", host.State == "over" && guest.State == "over" && host.EndWhy == "finished" && (host.Result, guest.Result) is ("win", "loss") or ("loss", "win") or ("draw", "draw"));
        Check("the heavier side wins", (hs.Cg > gs.Cg) == (host.Result == "win") || hs.Cg == gs.Cg);

        Note("Derby net: forged landings");
        MatchScore Fresh(out DerbySession h, out DerbySession g, out MemoryLink hlk, out MemoryLink glk)
        {
            (h, g, hlk, glk) = DerbyPair(); DerbyStart(h, g, 99); return default;
        }
        // The same cast landed twice.
        Fresh(out var h1, out var g1, out var hl1, out var gl1);
        h1.ReportLanding(0, false, 6); h1.ReportLanding(0, true, 7);
        g1.Tick(0.01f, 10); h1.Tick(0.01f, 10);
        Check("a cast landed twice stops the game as invalid", g1.State == "over" && g1.EndWhy == "invalid" && g1.Result == "");
        // Too early: five casts landed in the first ten seconds.
        Fresh(out var h2, out var g2, out _, out _);
        h2.ReportLanding(4, false, 9); g2.Tick(0.01f, 12);
        Check("a landing sooner than anyone could reel it in is refused", g2.State == "over" && g2.EndWhy == "invalid");
        // In the future of the receiver's own clock.
        Fresh(out var h3, out var g3, out _, out _);
        h3.ReportLanding(0, false, 120); g3.Tick(0.01f, 20);
        Check("a landing stamped well after the receiver's own clock is refused", g3.State == "over" && g3.EndWhy == "invalid");
        // After the end of the match.
        Fresh(out var h4, out var g4, out _, out _);
        h4.ReportLanding(30, false, 190); g4.Tick(0.01f, 400);
        Check("a landing after the three minutes are up is refused", g4.State == "over" && g4.EndWhy == "invalid");
        // Hostile shapes straight onto the link.
        foreach (var bad in new[] { new NetMsg { t = "dland", n = new[] { -1, 0, 9000 } }, new NetMsg { t = "dland", n = new[] { 0, 2, 9000 } }, new NetMsg { t = "dland", n = new[] { 0, 0 } }, new NetMsg { t = "dland" }, new NetMsg { t = "dland", n = new[] { 5000, 0, 9000 } } })
        {
            Fresh(out var h5, out var g5, out var hl5, out var gl5);
            bad.id = h5.MatchId;
            hl5.Send(bad);
            bool threw = false; try { g5.Tick(0.01f, 15); } catch (Exception) { threw = true; }
            Check($"a malformed landing ({bad.n?.Length.ToString() ?? "no"} numbers) is refused without a crash", !threw && (g5.State == "over" && g5.EndWhy == "invalid" || g5.State == "run" && g5.Theirs.Count == 0));
        }
        // A landing for another match is ignored.
        Fresh(out var h6, out var g6, out var hl6, out _);
        hl6.Send(new NetMsg { t = "dland", id = "other", n = new[] { 0, 0, 9000 } }); g6.Tick(0.01f, 15);
        Check("a landing for another match changes nothing", g6.State == "run" && g6.Theirs.Count == 0);
        // Totals that don't match what was seen.
        Fresh(out var h7, out var g7, out _, out _);
        var plan0 = MatchRules.Plan(99, 0);
        h7.ReportLanding(0, false, 9);
        for (int i = 0; i < 3; i++) { h7.Tick(0.01f, 12); g7.Tick(0.01f, 12); }
        h7.ReportEnd(new MatchScore { Cg = 999999, Perfects = 5, Catches = 9, Casts = 9 }); g7.ReportEnd(new MatchScore { Cg = 100, Casts = 3 });
        for (int i = 0; i < 3; i++) { h7.Tick(0.01f, 181); g7.Tick(0.01f, 181); }
        Check("a final claiming more than was landed is a game that doesn't count, with no winner", g7.State == "over" && g7.EndWhy == "invalid" && g7.Result == "" && g7.Mismatch);
        // The other side never finishes.
        Fresh(out var h8, out var g8, out _, out _);
        g8.ReportEnd(new MatchScore { Cg = 0 });
        for (int i = 0; i < 3; i++) { h8.Tick(0.01f, 181); g8.Tick(0.01f, 181); }
        Check("a side still waiting for the other's end doesn't wait forever", g8.State == "run");
        for (int i = 0; i < 200; i++) { h8.Tick(0.1f, 190); g8.Tick(0.1f, 190); }
        Check("after a few seconds it goes with what it saw", g8.State == "over" && g8.EndWhy == "finished");

        Note("Derby net: review round");
        // Fifty "simultaneous" landings can't happen: each cast takes time.
        Fresh(out var rq1, out var rq2, out var ql1, out _);
        for (int c = 0; c < 50; c++) rq1.ReportLanding(c, false, 177.5f);
        rq2.Tick(0.01f, 178);
        Check("many landings stamped at the same second are refused (casts take time)", rq2.State == "over" && rq2.EndWhy == "invalid" && rq2.Theirs.Count <= 1);
        // Landings that go back in cast order or crowd together.
        Fresh(out var rw1, out var rw2, out _, out _);
        rw1.ReportLanding(3, false, 30); rw1.ReportLanding(2, false, 40); rw2.Tick(0.01f, 41);
        Check("landings out of cast order are refused", rw2.State == "over" && rw2.EndWhy == "invalid");
        Fresh(out var re1, out var re2, out _, out _);
        re1.ReportLanding(2, false, 12); re1.ReportLanding(3, false, 13); re2.Tick(0.01f, 20);
        Check("a landing sooner than a cast could be after the last is refused", re2.State == "over" && re2.EndWhy == "invalid");
        // A slow link: the other side's clock is several seconds behind and it still gets its catches counted.
        Fresh(out var rs1, out var rs2, out _, out _);
        rs1.ReportLanding(0, false, 12); rs2.Tick(0.01f, 6);
        Check("a link several seconds slow doesn't make an honest landing look false", rs2.State == "run" && rs2.Theirs.Count == 1);
        // The last word arrives late (well past the old four-second wait) and is still checked.
        Fresh(out var rl1, out var rl2, out _, out _);
        rl1.ReportLanding(0, false, 20); for (int i = 0; i < 3; i++) { rl1.Tick(0.01f, 25); rl2.Tick(0.01f, 25); }
        rl2.ReportEnd(new MatchScore { Cg = 0, Casts = 1 });
        for (int i = 0; i < 100; i++) { rl1.Tick(0.1f, 100); rl2.Tick(0.1f, 100); }
        Check("a side waiting for the other's final keeps waiting past a few seconds", rl2.State == "run");
        rl1.ReportEnd(new MatchScore { Cg = rl2.TheirScore.Cg, Perfects = 0, Catches = 1, Casts = 1 });
        for (int i = 0; i < 3; i++) { rl1.Tick(0.01f, 181); rl2.Tick(0.01f, 181); }
        Check("and when it arrives the game is finished and checked against what was seen", rl2.State == "over" && rl2.EndWhy == "finished" && !rl2.Mismatch);
        // A game stopped for a broken rule is nobody's win.
        Fresh(out var rb1, out var rb2, out _, out _);
        rb1.ReportLanding(5, false, 3); rb2.Tick(0.01f, 10); rb1.Tick(0.01f, 10);
        Check("when one side stops a game for a broken rule the other records no win either", rb2.EndWhy == "invalid" && rb1.State == "over" && rb1.EndWhy == "invalid" && rb1.Result == "" && rb2.Result == "");
        Note("Derby net: leaving and rematches");
        Fresh(out var h9, out var g9, out _, out _);
        g9.Leave(); h9.Tick(0.01f, 30);
        Check("leaving mid-game is a forfeit: the leaver loses, the other wins", g9.State == "over" && g9.Result == "loss" && h9.State == "over" && h9.Result == "win" && h9.EndWhy == "left");
        Fresh(out var h10, out var g10, out var hl10, out var gl10);
        gl10.Quiet = DerbySession.SilentLimit + 1; g10.Tick(0.01f, 30);
        Check("a quiet line loses it for the quiet side", g10.State == "over" && g10.Result == "win" && g10.PeerGone);
        var (va, vb) = MemoryLink.Pair();
        var vh = new DerbySession("H", va, true);
        vb.Send(new NetMsg { t = "dhello", v = 77, name = "Future" }); vh.Tick(0.01f, 0);
        Check("a guest from another version is turned away", vh.State == "closed");
        // A whole game and a rematch.
        var (rh, rg, rhl, rgl) = DerbyPair(); DerbyStart(rh, rg, 31);
        var (s1, l1) = DerbyPlay(31, 4, 2); var (s2, l2) = DerbyPlay(31, 3, 0);
        foreach (var (c, p, t) in l1) rh.ReportLanding(c, p, t); foreach (var (c, p, t) in l2) rg.ReportLanding(c, p, t);
        for (int i = 0; i < 3; i++) { rh.Tick(0.01f, 100); rg.Tick(0.01f, 100); }
        rh.ReportEnd(s1); rg.ReportEnd(s2);
        for (int i = 0; i < 3; i++) { rh.Tick(0.01f, 181); rg.Tick(0.01f, 181); }
        string oldId = rh.MatchId, oldSeed = rh.Seed.ToString();
        rgl.Inject(new NetMsg { t = "dstart", id = "stray", s0 = 5, s1 = 180, v = 1, name = "Hosta" }); rg.Tick(0.01f, 181);
        Check("a start the guest never asked for doesn't restart a finished game", rg.State == "over" && rg.MatchId == oldId);
        rg.Rematch(); for (int i = 0; i < 3; i++) { rh.Tick(0.01f, 181); rg.Tick(0.01f, 181); }
        Check("a rematch waits for both", rg.WaitingForRematch && rh.State == "over");
        rh.Rematch(); for (int i = 0; i < 4; i++) { rh.Tick(0.01f, 181); rg.Tick(0.01f, 181); }
        Check("then a new game: new id, new seed, back to the handshake on both", rh.State == "wait" && rg.State == "wait" && rh.MatchId != oldId && rh.MatchId == rg.MatchId && rg.Seed == rh.Seed && rh.Theirs.Count == 0 && rg.Theirs.Count == 0);
        rg.GuestReady(); for (int i = 0; i < 4; i++) { rh.Tick(0.01f, 0); rg.Tick(0.01f, 0); }
        Check("and it runs again", rh.State == "run" && rg.State == "run");
        // A reused id on a rematch is refused.
        var (xh, xg, xhl, xgl) = DerbyPair(); DerbyStart(xh, xg, 8);
        xh.ReportEnd(default); xg.ReportEnd(default);
        for (int i = 0; i < 3; i++) { xh.Tick(0.01f, 181); xg.Tick(0.01f, 181); }
        xg.Rematch();
        xgl.Inject(new NetMsg { t = "dstart", id = xh.MatchId, s0 = 5, s1 = 180, v = 1, name = "Hosta" }); xg.Tick(0.01f, 181);
        Check("a rematch that reuses the old match id is refused", xg.State == "over" && xg.EndWhy == "invalid");
        yield return 1;
    }
}
#endif
