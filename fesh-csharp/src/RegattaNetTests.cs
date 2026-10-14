#if DEBUG
namespace Fesh;

partial class Game
{
    // A boat that sails a straight-line path through the gates at a steady speed.
    sealed class PolyBoat
    {
        public readonly List<(float x, float y)> Path = new();
        readonly float[] cum;
        public readonly float V;
        public PolyBoat(int seat, float v, bool skip = false)
        {
            V = v;
            var s = RegattaRules.Slot(seat);
            Path.Add((s.x, s.y));
            for (int g = 1; g < RegattaRules.Gates.Length; g++) if (!(skip && g == 6)) Path.Add(RegattaRules.Gates[g]);
            Path.Add(RegattaRules.Gates[0]);
            cum = new float[Path.Count];
            for (int i = 1; i < Path.Count; i++) cum[i] = cum[i - 1] + MathF.Sqrt(MathF.Pow(Path[i].x - Path[i - 1].x, 2) + MathF.Pow(Path[i].y - Path[i - 1].y, 2));
        }
        public float Length => cum[^1];
        public (float x, float y) At(float t)
        {
            float d = Math.Min(V * t, Length); int i = 1;
            while (i < Path.Count - 1 && cum[i] < d) i++;
            float k = Math.Clamp((d - cum[i - 1]) / MathF.Max(0.001f, cum[i] - cum[i - 1]), 0, 1);
            return (Path[i - 1].x + (Path[i].x - Path[i - 1].x) * k, Path[i - 1].y + (Path[i].y - Path[i - 1].y) * k);
        }
    }

    static (RegattaSession host, RegattaSession guest, MemoryLink hl, MemoryLink gl) RegattaPair()
    {
        var (a, b) = MemoryLink.Pair();
        var host = new RegattaSession("Hosta", a, true);
        var guest = new RegattaSession("Guesta", b, false);
        for (int i = 0; i < 3; i++) { host.Tick(0.01f); guest.Tick(0.01f); }
        return (host, guest, a, b);
    }

    // Plays heats with scripted boats until the match is over (or the time is up): each boat is on the line when asked and
    // then sails its path. `drive` lets a test change what a boat does in a heat.
    static void RegattaRace(RegattaSession h, RegattaSession g, Func<int, PolyBoat> hostBoat, Func<int, PolyBoat> guestBoat, float maxSecs = 400, float dt = 1f / 30)
    {
        for (float t = 0; t < maxSecs && h.State == "play" && g.State == "play"; t += dt)
        {
            if (h.HeatPhase == "lineup") h.Ready();
            if (g.HeatPhase == "lineup") g.Ready();
            if (h.HeatPhase == "race") { var p = hostBoat(h.Heat).At(h.HeatT); h.Position(p.x, p.y); }
            if (g.HeatPhase == "race") { var p = guestBoat(g.Heat).At(g.HeatT); g.Position(p.x, p.y); }
            h.Tick(dt); g.Tick(dt);
        }
        for (int i = 0; i < 4; i++) { h.Tick(0.01f); g.Tick(0.01f); }
    }

    // Like RegattaRace, but stops as soon as `stop` is true (so a test can step in at an exact moment).
    static void RegattaUntil(RegattaSession h, RegattaSession g, Func<int, PolyBoat> hostBoat, Func<int, PolyBoat> guestBoat, Func<bool> stop, float maxSecs = 400, float dt = 1f / 30, float wob = 0)
    {
        for (float t = 0; t < maxSecs && h.State == "play" && g.State == "play" && !stop(); t += dt)
        {
            if (h.HeatPhase == "lineup") h.Ready();
            if (g.HeatPhase == "lineup") g.Ready();
            if (h.HeatPhase == "race") { var p = hostBoat(h.Heat).At(h.HeatT); h.Position(p.x + wob * MathF.Sin(5 * h.HeatT), p.y + wob * MathF.Cos(4 * h.HeatT)); }
            if (g.HeatPhase == "race") { var p = guestBoat(g.Heat).At(g.HeatT); g.Position(p.x + wob * MathF.Sin(4 * g.HeatT + 1), p.y + wob * MathF.Cos(3 * g.HeatT + 2)); }
            h.Tick(dt); g.Tick(dt);
        }
    }

    // The head-to-head Regatta's referee and tracker (1.30). FESH_REGATTANET_TEST=1 runs only this.
    IEnumerable<int> RegattaNetScript()
    {
        Note("Regatta net: the tracker");
        var fast = new PolyBoat(0, 96);
        var tr = new RegattaTracker(); var s0 = RegattaRules.Slot(0); tr.Reset(s0.x, s0.y);
        for (float t = 0; t < 60 && tr.FinishT < 0; t += 0.125f) { var p = fast.At(t); tr.Feed(p.x, p.y, t); }
        float expect = fast.Length / 96;
        Check($"sailing through every gate in order and back to the start finishes, at the time the path takes ({tr.FinishT:0.00} s against about {expect:0.00})", tr.FinishT > 0 && MathF.Abs(tr.FinishT - expect) < 0.4f && tr.Next == RegattaRules.Gates.Length && tr.GateTimes.Count == RegattaRules.Gates.Length && !tr.Cheat);
        Check("the gate times only go up", tr.GateTimes.Zip(tr.GateTimes.Skip(1)).All(p => p.Second > p.First));
        var cutter = new PolyBoat(0, 96, skip: true);
        var tc = new RegattaTracker(); tc.Reset(s0.x, s0.y);
        for (float t = 0; t < 60; t += 0.125f) { var p = cutter.At(t); tc.Feed(p.x, p.y, t); }
        Check("a boat that misses a gate never finishes", tc.FinishT < 0 && tc.Next <= 6);
        var tj = new RegattaTracker(); tj.Reset(s0.x, s0.y);
        tj.Feed(s0.x + 10, s0.y, 0.125f); tj.Feed(RegattaRules.Gates[3].x, RegattaRules.Gates[3].y, 0.25f);
        Check("a jump across the course is caught", tj.Cheat);
        var tb = new RegattaTracker(); tb.Reset(s0.x, s0.y);
        tb.Feed(s0.x + 5, s0.y, 5); tb.Feed(s0.x + 6, s0.y, 4);
        Check("a position from earlier than the last is caught", tb.Cheat);
        var tf = new RegattaTracker(); tf.Reset(s0.x, s0.y);
        tf.Feed(float.NaN, 0, 1);
        Check("a position that isn't a number is caught", tf.Cheat);
        // 1.30.1 (Codex's review): positions stamped with one and the same moment can't add up to a course.
        var tz = new RegattaTracker(); tz.Reset(s0.x, s0.y);
        float zx = s0.x, zy = s0.y;
        for (int gi = 1; gi <= RegattaRules.Gates.Length && !tz.Cheat; gi++)
        {
            var (gx2, gy2) = RegattaRules.Gates[gi % RegattaRules.Gates.Length];
            float dist = MathF.Sqrt((gx2 - zx) * (gx2 - zx) + (gy2 - zy) * (gy2 - zy));
            int steps = (int)MathF.Ceiling(dist / 9);
            for (int st = 1; st <= steps && !tz.Cheat; st++) { float k = (float)st / steps; tz.Feed(zx + (gx2 - zx) * k, zy + (gy2 - zy) * k, 1.0f); }
            zx = gx2; zy = gy2;
        }
        Check("packets 9 px apart all stamped with the same moment can't sail the whole course in no time", tz.Cheat && tz.FinishT < 0);
        var tdist = new RegattaTracker(); tdist.Reset(s0.x, s0.y);
        for (int i = 1; i <= 40; i++) tdist.Feed(s0.x + i * 9, s0.y, 0.125f * i);          // 72 px a second, in 9 px steps: fine
        Check("a boat sailing at a steady pace is not caught by the distance budget", !tdist.Cheat);
        var tdist2 = new RegattaTracker(); tdist2.Reset(s0.x, s0.y);
        for (int i = 1; i <= 80 && !tdist2.Cheat; i++) tdist2.Feed(s0.x + i * 9, s0.y, 0.0625f * i);   // 144 px a second: more than any boat
        Check("but one that keeps a pace above any boat's is caught even in small steps", tdist2.Cheat);
        yield return 1;

        Note("Regatta net: a match");
        var (h, g, hl, gl) = RegattaPair();
        Check("hello starts the match: both in heat 1, lined up, names swapped", h.State == "play" && g.State == "play" && h.Heat == 0 && g.Heat == 0 && h.HeatPhase == "lineup" && g.HeatPhase == "lineup" && h.RivalName == "Guesta" && g.RivalName == "Hosta" && h.MatchId == g.MatchId);
        Check("each side stands in its own place at the start", RegattaRules.Slot(0) != RegattaRules.Slot(1) && h.MySeat == 0 && g.MySeat == 1);
        h.Ready();
        for (int i = 0; i < 3; i++) { h.Tick(0.01f); g.Tick(0.01f); }
        Check("the countdown waits for both boats to be on the line", h.HeatPhase == "lineup" && g.HeatPhase == "lineup");
        g.Ready();
        for (int i = 0; i < 4; i++) { h.Tick(0.01f); g.Tick(0.01f); }
        Check("then both count down", h.HeatPhase == "count" && g.HeatPhase == "count" && h.Count > 2.9f);
        for (int i = 0; i < 60; i++) { h.Tick(0.1f); g.Tick(0.1f); }
        Check("and race", h.HeatPhase == "race" && g.HeatPhase == "race");
        // Whole matches. The host is faster by 8 px/s.
        (h, g, hl, gl) = RegattaPair();
        RegattaRace(h, g, _ => new PolyBoat(0, 100), _ => new PolyBoat(1, 92));
        Check($"the faster boat wins best of three, two heats straight ({h.MyWins}-{h.TheirWins})", h.State == "over" && g.State == "over" && h.EndWhy == "finished" && h.Result == "win" && g.Result == "loss" && h.MyWins == 2 && h.TheirWins == 0 && h.Results.Count == 2);
        Check("both sides recorded the same times for both boats", h.Results.Zip(g.Results).All(p => MathF.Abs(p.First.mine - p.Second.theirs) < 0.3f && MathF.Abs(p.First.theirs - p.Second.mine) < 0.3f) && h.Results[0].mine > 0 && h.Results[0].mine < h.Results[0].theirs);
        Check("the best heat is the fastest the boat did", MathF.Abs(h.MyBest - h.Results.Min(r => r.mine)) < 0.001f);
        // 2-1 over three heats: the guest is faster in the second.
        (h, g, hl, gl) = RegattaPair();
        RegattaRace(h, g, heat => new PolyBoat(0, heat == 1 ? 80 : 100), _ => new PolyBoat(1, 92));
        Check($"a heat each and a third decides it: three heats played ({h.MyWins}-{h.TheirWins})", h.State == "over" && h.Results.Count == 3 && h.MyWins == 2 && h.TheirWins == 1 && h.Result == "win" && g.Result == "loss");
        // A boat that doesn't finish loses the heat; both not finishing is a draw heat.
        (h, g, hl, gl) = RegattaPair();
        RegattaRace(h, g, _ => new PolyBoat(0, 100), _ => new PolyBoat(1, 5), maxSecs: 600, dt: 0.1f);
        Check($"a boat that can't finish in time loses the heat ({h.MyWins}-{h.TheirWins}, {h.Results.Count} heats)", h.State == "over" && h.Result == "win" && h.Results.All(r => r.theirs < 0 && r.mine > 0));
        (h, g, hl, gl) = RegattaPair();
        RegattaRace(h, g, _ => new PolyBoat(0, 4), _ => new PolyBoat(1, 4), maxSecs: 900, dt: 0.1f);
        Check("two boats that both fail to finish draw every heat, and the match is a draw", h.State == "over" && h.Results.Count == 3 && h.MyWins == 0 && h.TheirWins == 0 && h.Result == "draw" && g.Result == "draw");
        yield return 1;

        Note("Regatta net: cheating, leaving and rematches");
        // Positions no boat could have sailed.
        (h, g, hl, gl) = RegattaPair();
        for (float t = 0; t < 30 && h.HeatPhase != "race"; t += 0.05f) { if (h.HeatPhase == "lineup") h.Ready(); if (g.HeatPhase == "lineup") g.Ready(); h.Tick(0.05f); g.Tick(0.05f); }
        for (int i = 0; i < 40; i++) { h.Tick(0.05f); g.Tick(0.05f); }
        g.Position(RegattaRules.Slot(1).x, RegattaRules.Slot(1).y);
        h.Tick(0.2f); g.Tick(0.2f); g.Position(RegattaRules.Gates[4].x, RegattaRules.Gates[4].y); h.Tick(0.2f); g.Tick(0.2f);
        Check("a boat that jumps across the course stops the race as invalid, with no winner", h.State == "over" && h.EndWhy == "invalid" && h.Result == "" && g.State == "over" && g.Result == "");
        // A finish claimed that the positions never showed counts as a DNF.
        (h, g, hl, gl) = RegattaPair();
        for (float t = 0; t < 30 && h.HeatPhase != "race"; t += 0.05f) { if (h.HeatPhase == "lineup") h.Ready(); if (g.HeatPhase == "lineup") g.Ready(); h.Tick(0.05f); g.Tick(0.05f); }
        gl.Send(new NetMsg { t = "rfin", id = h.MatchId, n = new[] { 0, 9000 } });
        for (float t = 0; t < 100; t += 0.1f) { if (h.HeatPhase == "race") { var p = new PolyBoat(0, 100).At(h.HeatT); h.Position(p.x, p.y); } h.Tick(0.1f); g.Tick(0.1f); if (h.HeatPhase == "result") break; }
        Check("a finish time with no sailing behind it is no finish: the heat goes to the boat that did sail", h.HeatPhase == "result" && h.Results.Count == 1 && h.Results[0].theirs < 0 && h.MyWins == 1);
        // A real finish is accepted, and one that disagrees with the positions is not.
        var liar = new PolyBoat(1, 90); var (h2, g2, hl2, gl2) = RegattaPair();
        for (float t = 0; t < 30 && h2.HeatPhase != "race"; t += 0.05f) { if (h2.HeatPhase == "lineup") h2.Ready(); if (g2.HeatPhase == "lineup") g2.Ready(); h2.Tick(0.05f); g2.Tick(0.05f); }
        for (float t = 0; t < 40 && g2.Sent.FinishT < 0; t += 1f / 30) { var p = liar.At(g2.HeatT); g2.Position(p.x, p.y); g2.Tick(1f / 30); h2.Tick(1f / 30); }
        Check("a real finish is accepted by the other side", h2.Theirs.FinishT > 0);
        // Rewrite what the guest would claim: 8 seconds faster than its positions showed.
        var (h4, g4, hl4, gl4) = RegattaPair();
        for (float t = 0; t < 30 && h4.HeatPhase != "race"; t += 0.05f) { if (h4.HeatPhase == "lineup") h4.Ready(); if (g4.HeatPhase == "lineup") g4.Ready(); h4.Tick(0.05f); g4.Tick(0.05f); }
        var pb = new PolyBoat(1, 90);
        for (int i = 0; i < 900 && h4.State == "play"; i++)
        {
            float t = h4.HeatT;
            var p = pb.At(t);
            // Send positions as a real boat would, but a false, faster finish at the end.
            if (t < pb.Length / 90 - 0.05f) gl4.Send(new NetMsg { t = "rpos", id = h4.MatchId, n = new[] { 0, (int)(p.x * 4), (int)(p.y * 4), (int)(t * 1000) } });
            else { var e = RegattaRules.Gates[0]; gl4.Send(new NetMsg { t = "rpos", id = h4.MatchId, n = new[] { 0, (int)(e.x * 4), (int)(e.y * 4), (int)(t * 1000) } }); gl4.Send(new NetMsg { t = "rfin", id = h4.MatchId, n = new[] { 0, (int)((t - 8) * 1000) } }); }
            h4.Tick(1f / 30);
            if (t > pb.Length / 90 + 1) break;
        }
        Check("a finish claimed several seconds faster than the positions showed stops the race as invalid", h4.State == "over" && h4.EndWhy == "invalid" && h4.Result == "");
        // A clock ahead of the receiver's own, and one running backwards.
        var (h5, g5, hl5, gl5) = RegattaPair();
        for (float t = 0; t < 30 && h5.HeatPhase != "race"; t += 0.05f) { if (h5.HeatPhase == "lineup") h5.Ready(); if (g5.HeatPhase == "lineup") g5.Ready(); h5.Tick(0.05f); g5.Tick(0.05f); }
        gl5.Send(new NetMsg { t = "rpos", id = h5.MatchId, n = new[] { 0, (int)(RegattaRules.Slot(1).x * 4), (int)(RegattaRules.Slot(1).y * 4), 60000 } }); h5.Tick(0.01f);
        Check("a position stamped a minute ahead of the receiver's clock is refused", h5.State == "over" && h5.EndWhy == "invalid");
        // Hostile shapes.
        foreach (var bad in new[] { new NetMsg { t = "rpos" }, new NetMsg { t = "rpos", n = new[] { 0, 1 } }, new NetMsg { t = "rfin", n = new[] { 9, 9, 9 } }, new NetMsg { t = "rready" }, new NetMsg { t = "rheat", n = new[] { 5 } }, new NetMsg { t = "rgo" }, new NetMsg { t = "mystery" } })
        {
            var (hx, gx, hlx, glx) = RegattaPair();
            bad.id = hx.MatchId; glx.Send(bad);
            bool threw = false; try { hx.Tick(0.01f); } catch (Exception) { threw = true; }
            Check($"a malformed '{bad.t}' changes nothing and doesn't crash", !threw && (hx.State == "play" || hx.State == "over" && hx.EndWhy == "invalid"));
        }
        // Leaving, silence, version.
        (h, g, hl, gl) = RegattaPair();
        g.Leave(); h.Tick(0.01f);
        Check("leaving mid-match is a forfeit: the leaver loses", g.Result == "loss" && h.Result == "win" && h.EndWhy == "left");
        (h, g, hl, gl) = RegattaPair();
        gl.Quiet = RegattaSession.SilentLimit + 1; g.Tick(0.01f);
        Check("a quiet line ends the race for the quiet side with no winner (1.30.1: it used to record a win)", g.State == "over" && g.Result == "" && g.EndWhy == "lost");
        h.Tick(0.01f);
        Check("and the other side, whose line has gone, also records no result rather than a second win", h.State == "over" && h.Result == "");
        var (va, vb) = MemoryLink.Pair(); var vh = new RegattaSession("H", va, true);
        vb.Send(new NetMsg { t = "rhello", v = 77, name = "Future" }); vh.Tick(0.01f);
        Check("a guest from another version is turned away", vh.State == "closed");
        // A rematch.
        (h, g, hl, gl) = RegattaPair();
        RegattaRace(h, g, _ => new PolyBoat(0, 100), _ => new PolyBoat(1, 92));
        string oldId = h.MatchId;
        g.Rematch(); for (int i = 0; i < 3; i++) { h.Tick(0.01f); g.Tick(0.01f); }
        Check("a rematch waits for both", g.WaitingForRematch && h.State == "over");
        h.Rematch(); for (int i = 0; i < 4; i++) { h.Tick(0.01f); g.Tick(0.01f); }
        Check("then a new match: new id, heat one, the score back to nothing", h.State == "play" && g.State == "play" && h.MatchId != oldId && h.MatchId == g.MatchId && h.Heat == 0 && h.MyWins == 0 && h.Results.Count == 0 && g.RecordId != h.RecordId);
        (h, g, hl, gl) = RegattaPair();
        RegattaRace(h, g, _ => new PolyBoat(0, 100), _ => new PolyBoat(1, 92)); g.Rematch();
        gl.Inject(new NetMsg { t = "rstart", id = h.MatchId, v = 1, name = "Hosta" }); g.Tick(0.01f);
        Check("a rematch that reuses the old match id is refused", g.State == "over" && g.EndWhy == "invalid");
        yield return 1;

        Note("Regatta net: Codex's review of 1.30");
        // Two honest racers must always agree who won a heat, even by a hair.
        int split = 0, bothWon = 0, neither = 0, races = 0; string badRaces = "";
        foreach (var (wob, fdt) in new[] { (0f, 1f / 30), (5f, 1f / 30), (5f, 1f / 12), (0f, 1f / 12) })
        for (int step = -25; step <= 25; step++)
        {
            float delta = step * 0.01f;                    // the guest's finish is `delta` seconds later than the host's
            var hostB = new PolyBoat(0, 100);
            var guestB = new PolyBoat(1, 1);
            var gb = new PolyBoat(1, 100 * guestB.Length / (hostB.Length + 100 * delta));
            var (hh, gg, hhl, ggl) = RegattaPair();
            RegattaUntil(hh, gg, _ => hostB, _ => gb, () => hh.HeatPhase == "result" && gg.HeatPhase == "result", maxSecs: 120, dt: fdt, wob: wob);
            races++;
            if (hh.Results.Count != 1 || gg.Results.Count != 1 || hh.Results[0].mine <= 0 || hh.Results[0].theirs <= 0 || hh.Results[0].mine != gg.Results[0].theirs || hh.Results[0].theirs != gg.Results[0].mine) { neither++; continue; }
            if (hh.MyWins != gg.TheirWins || hh.TheirWins != gg.MyWins) { split++; badRaces += $"[wob {wob} dt {fdt:0.000} d {delta:0.00}: host {hh.Results[0].mine:0.000}/{hh.Results[0].theirs:0.000} guest {gg.Results[0].mine:0.000}/{gg.Results[0].theirs:0.000}] "; }
            if (hh.MyWins == 1 && gg.MyWins == 1) bothWon++;
        }
        Check($"two honest racers always agree who won a heat, however close, wobbling or not, at any frame rate ({split} disagreements {badRaces}and {bothWon} heats both claimed in {races} races, {neither} unfinished or not mirrored)", split == 0 && bothWon == 0 && neither == 0);
        // A host that keeps adding heats after the match is decided.
        {
            var (hx, gx, hlx, glx) = RegattaPair();
            RegattaUntil(hx, gx, _ => new PolyBoat(0, 100), _ => new PolyBoat(1, 92), () => gx.Heat == 1 && gx.HeatPhase == "result");
            bool decided = gx.TheirWins == 2 && gx.State == "play";
            hlx.Send(new NetMsg { t = "rheat", id = hx.MatchId, n = new[] { 2 } }); gx.Tick(0.01f);
            Check("a host that starts a third heat after one side has won two is refused, with no result", decided && gx.State == "over" && gx.EndWhy == "invalid" && gx.Result == "");
            var (hy, gy, hly, gly) = RegattaPair();
            RegattaUntil(hy, gy, heat => new PolyBoat(0, heat == 1 ? 80 : 100), _ => new PolyBoat(1, 92), () => gy.Heat == 2 && gy.HeatPhase == "result");
            bool lastHeat = gy.Results.Count == 3 && gy.State == "play";
            hly.Send(new NetMsg { t = "rheat", id = hy.MatchId, n = new[] { 3 } }); gy.Tick(0.01f);
            Check("and a fourth heat after the last of three is refused too", lastHeat && gy.State == "over" && gy.EndWhy == "invalid" && gy.Result == "");
            var (hz, gz, hlz, glz) = RegattaPair();
            RegattaUntil(hz, gz, heat => new PolyBoat(0, heat == 1 ? 80 : 100), _ => new PolyBoat(1, 92), () => gz.Heat == 0 && gz.HeatPhase == "result");
            hlz.Send(new NetMsg { t = "rheat", id = hz.MatchId, n = new[] { 1 } }); gz.Tick(0.01f);
            Check("while the next heat of an undecided match is accepted", gz.State == "play" && gz.Heat == 1 && gz.HeatPhase == "lineup");
        }
        // Codex's second look: a guest that froze through the end of its countdown and then caught up with the host's samples.
        {
            var (hc, gc, hlc, glc) = RegattaPair();
            hc.Ready(); gc.Ready(); hc.Tick(0.01f); gc.Tick(0.01f);
            var hb0 = new PolyBoat(0, 100);
            for (float t = 0; t < 4.0f; t += 1f / 30) { hc.Tick(1f / 30); if (hc.HeatPhase == "race") { var p = hb0.At(hc.HeatT); hc.Position(p.x, p.y); } }     // (the guest is not ticked)
            bool guestStillCounting = gc.HeatPhase == "count";
            RegattaUntil(hc, gc, _ => hb0, _ => new PolyBoat(1, 90), () => hc.HeatPhase == "result" && gc.HeatPhase == "result", maxSecs: 120);
            Check($"a guest that froze during its countdown still sees the host's whole course afterwards, and both agree the heat (guest saw the host finish: {gc.Theirs.FinishT:0.00})",
                guestStillCounting && hc.Results.Count == 1 && gc.Results.Count == 1 && gc.Results[0].theirs > 0 && gc.Results[0].theirs == hc.Results[0].mine && gc.Results[0].mine == hc.Results[0].theirs);
        }
        // A slow clock (3 frames a second, so the game clock runs at three quarters of real time) is not a boat that never finished.
        {
            var (hd, gd, hld, gld) = RegattaPair();
            var slowB = new PolyBoat(1, 1); var slowBoat = new PolyBoat(1, slowB.Length / 79f);          // finishes at 79 s of its own clock
            var fastB = new PolyBoat(0, 100);
            for (float t = 0; t < 400 && hd.State == "play" && gd.State == "play" && !(hd.HeatPhase == "result" && gd.HeatPhase == "result"); t += 1f / 30)
            {
                if (hd.HeatPhase == "lineup") hd.Ready();
                if (gd.HeatPhase == "lineup") gd.Ready();
                if (hd.HeatPhase == "race") { var p = fastB.At(hd.HeatT); hd.Position(p.x, p.y); }
                if (gd.HeatPhase == "race") { var p = slowBoat.At(gd.HeatT); gd.Position(p.x, p.y); }
                hd.Tick(1f / 30); gd.Tick(1f / 30 * 0.75f);
            }
            Check($"a racer whose clock runs slow is waited for, not written off as a no-finish at 88 s ({hd.Results.Count} results, guest {(hd.Results.Count > 0 ? hd.Results[0].theirs : -9):0.0})", hd.Results.Count == 1 && gd.Results.Count == 1 && hd.Results[0].theirs > 70 && hd.Results[0].theirs == gd.Results[0].mine);
        }
        // A finish that falls between two samples, in the last moments before the heat is called, still counts.
        {
            int bad = 0, total = 0; float lastBad = 0; string lostList = "";
            var lenB = new PolyBoat(1, 1);
            for (int k = 0; k < 55; k++)
            {
                float target = 80.9f + 0.005f * k;     // (the 18 px circle is entered over a second before the centre at this pace, so these cross the line in the last fraction of a second before 80 s)
                var (he, ge, hle, gle) = RegattaPair();
                var gb2 = new PolyBoat(1, lenB.Length / target);
                RegattaUntil(he, ge, _ => new PolyBoat(0, 100), _ => gb2, () => he.HeatPhase == "result" && ge.HeatPhase == "result", maxSecs: 200, dt: 0.03f);
                total++;
                if (ge.Results.Count != 1 || ge.Results[0].mine <= 0 || he.Results.Count != 1 || he.Results[0].theirs <= 0) { bad++; lastBad = target; lostList += $"{target:0.00} "; }
            }
            Check($"a finish just before the 80 s limit is never turned into a no-finish ({bad} of {total} lost, last at {lastBad:0.00} s of boat time: {lostList})", bad == 0);
        }
        // A claim is compared to what the positions showed to the millisecond: shaving 20 ms off it does not buy the heat.
        foreach (int shave in new[] { 0, 20, 1, 99 })
        {
            var (ha, hb2) = MemoryLink.Pair();
            var hostF = new RegattaSession("H", ha, true);
            hb2.Send(new NetMsg { t = "rhello", v = RegattaRules.Protocol, name = "Forger" }); hostF.Tick(0.01f);
            hostF.Ready(); hb2.Send(new NetMsg { t = "rready", id = hostF.MatchId, n = new[] { 0 } }); hostF.Tick(0.01f);
            for (int i = 0; i < 40 && hostF.HeatPhase != "race"; i++) hostF.Tick(0.1f);
            var fb = new PolyBoat(1, 100); float tt = 0;
            while (hostF.State == "play" && tt < fb.Length / 100 + 1)
            {
                var p = fb.At(tt);
                hb2.Send(new NetMsg { t = "rpos", id = hostF.MatchId, n = new[] { 0, (int)MathF.Round(p.x * 4), (int)MathF.Round(p.y * 4), (int)MathF.Round(tt * 1000) } });
                tt += 0.125f; hostF.Tick(0.125f);
            }
            int shown = (int)MathF.Round(hostF.Theirs.FinishT * 1000);
            int claim = shave switch { 0 => shown, 20 => shown - 20, 1 => int.MinValue + shown, _ => 81000 };
            bool threw = false;
            try { hb2.Send(new NetMsg { t = "rfin", id = hostF.MatchId, n = new[] { 0, claim } }); hostF.Tick(0.01f); } catch (Exception) { threw = true; }
            if (shave == 0) Check($"a claim equal to what the positions showed is taken ({shown} ms)", shown > 0 && hostF.State == "play");
            else if (shave == 20) Check("a claim 20 ms faster than the positions showed stops the race as invalid", hostF.State == "over" && hostF.EndWhy == "invalid");
            else if (shave == 1) Check("a claim that would overflow the comparison is refused as invalid, not thrown", !threw && hostF.State == "over" && hostF.EndWhy == "invalid");
            else Check("a finish claimed after the 80 s limit is refused as invalid, whatever the positions showed", !threw && hostF.State == "over" && hostF.EndWhy == "invalid");
        }
        // A racer who crosses the line after the limit, with honest samples and a matching claim, has not won the heat.
        {
            var (ha, hb2) = MemoryLink.Pair();
            var hostL = new RegattaSession("H", ha, true);
            hb2.Send(new NetMsg { t = "rhello", v = RegattaRules.Protocol, name = "Late" }); hostL.Tick(0.01f);
            hostL.Ready(); hb2.Send(new NetMsg { t = "rready", id = hostL.MatchId, n = new[] { 0 } }); hostL.Tick(0.01f);
            for (int i = 0; i < 40 && hostL.HeatPhase != "race"; i++) hostL.Tick(0.1f);
            var lenP = new PolyBoat(1, 1); var lateBoat = new PolyBoat(1, lenP.Length / 83f);
            float tl = 0; bool threwL = false;
            try
            {
                while (hostL.State == "play" && hostL.HeatPhase == "race" && tl < 84)
                {
                    var p = lateBoat.At(tl);
                    hb2.Send(new NetMsg { t = "rpos", id = hostL.MatchId, n = new[] { 0, (int)MathF.Round(p.x * 4), (int)MathF.Round(p.y * 4), (int)MathF.Round(tl * 1000) } });
                    if (hostL.Theirs.FinishT > 0 && hostL.State == "play") hb2.Send(new NetMsg { t = "rfin", id = hostL.MatchId, n = new[] { 0, (int)MathF.Round(hostL.Theirs.FinishT * 1000) } });
                    tl += 0.125f; hostL.Tick(0.125f);
                }
            }
            catch (Exception) { threwL = true; }
            Check($"a finish after the 80 s limit does not win the heat, even with honest samples and a matching claim (host: {hostL.State}/{hostL.HeatPhase}, wins {hostL.MyWins}-{hostL.TheirWins}, their finish {hostL.Theirs.FinishT:0.0})",
                !threwL && !(hostL.TheirWins > 0));
        }
        // A host that moves on to the next heat while I am still racing the last one leaves nobody stranded.
        {
            var (hr, gr, hlr, glr) = RegattaPair();
            RegattaUntil(hr, gr, _ => new PolyBoat(0, 100), _ => new PolyBoat(1, 20), () => gr.HeatPhase == "race" && gr.HeatT > 5);
            hlr.Send(new NetMsg { t = "rheat", id = hr.MatchId, n = new[] { 1 } }); gr.Tick(0.01f);
            Check("a next heat announced while I am still racing ends the race with no result instead of being ignored", gr.State == "over" && gr.EndWhy == "invalid" && gr.Result == "");
        }
        // A bye, or silence, before the match began.
        {
            var (ja, jb) = MemoryLink.Pair(); var guestJ = new RegattaSession("G", jb, false);
            ja.Send(new NetMsg { t = "bye", why = "left" }); guestJ.Tick(0.01f);
            Check("a guest told 'bye' before the race starts is not left waiting: the match is over with no result", guestJ.State != "joining" && guestJ.Result == "" && guestJ.Notice != "");
            var (ka, kb) = MemoryLink.Pair(); var hostJ = new RegattaSession("H", ka, true);
            kb.Send(new NetMsg { t = "bye", why = "left" }); hostJ.Tick(0.01f);
            Check("and a host told 'bye' before the guest says hello is not left waiting either", hostJ.State != "joining" && hostJ.Result == "");
        }
        yield return 1;

        Note("Regatta net: the bot");
        (h, _, _, _) = RegattaPair();
        var (ba, bb) = MemoryLink.Pair();
        var hostS = new RegattaSession("Me", ba, true);
        var path = new PolyBoat(1, 1).Path;
        var bot = new RegattaBot("Marlo", bb, path, 84, 11);
        for (float t = 0; t < 400 && hostS.State != "over"; t += 1f / 30)
        {
            if (hostS.HeatPhase == "lineup") hostS.Ready();
            if (hostS.HeatPhase == "race") { var p = new PolyBoat(0, 100).At(hostS.HeatT); hostS.Position(p.x, p.y); }
            hostS.Tick(1f / 30); bot.Tick(1f / 30);
        }
        Check($"racing a bot is the same race: it sails the course in about {bot.Length / 84:0.0} s a heat and the faster boat wins ({hostS.MyWins}-{hostS.TheirWins})", hostS.State == "over" && hostS.Result == "win" && hostS.Results.All(r => r.theirs > bot.Length / 84 * 0.9f && r.theirs < bot.Length / 84 * 1.15f));
        Check("and its heats differ a little (no two the same)", hostS.Results.Count >= 2 && hostS.Results.Select(r => MathF.Round(r.theirs, 1)).Distinct().Count() == hostS.Results.Count);
        yield return 1;
    }
}
#endif
