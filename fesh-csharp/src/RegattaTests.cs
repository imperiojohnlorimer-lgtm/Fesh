#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // A point a distance along a path, and the way the path is heading there.
    static (float x, float y, string face) RouteAt(List<(float x, float y)> path, float d)
    {
        float run = 0;
        for (int i = 1; i < path.Count; i++)
        {
            float seg = MathF.Sqrt(MathF.Pow(path[i].x - path[i - 1].x, 2) + MathF.Pow(path[i].y - path[i - 1].y, 2));
            if (run + seg >= d || i == path.Count - 1)
            {
                float k = seg > 0.001f ? Math.Clamp((d - run) / seg, 0, 1) : 1;
                float dx = path[i].x - path[i - 1].x, dy = path[i].y - path[i - 1].y;
                string face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
                return (path[i - 1].x + dx * k, path[i - 1].y + dy * k, face);
            }
            run += seg;
        }
        return (path[^1].x, path[^1].y, "up");
    }

    // How many pixels of the view change when the other boat is taken out of the picture.
    int RemoteBoatPixels()
    {
        RenderWorld(time);
        var with = (Color[])pix.Buf.Clone();
        float kx = regRX, ky = regRY;
        regRX = -5000; regRY = -5000;
        RenderWorld(time);
        regRX = kx; regRY = ky;
        int diff = 0;
        for (int i = 0; i < with.Length; i++) if (with[i].R != pix.Buf[i].R || with[i].G != pix.Buf[i].G || with[i].B != pix.Buf[i].B) diff++;
        return diff;
    }

    // The head-to-head Regatta in the game (1.30): FESH_REGATTA_TEST=1 runs only this. The racer on the other side is an
    // angler (a bot over an in-memory link) or the test's own session over a real dock connection.
    IEnumerable<int> RegattaScript()
    {
        bool lanOnly = Environment.GetEnvironmentVariable("FESH_REGATTA_TEST") == "lan";
        var route = new List<(float x, float y)>();
        if (lanOnly) goto lan;
        Note("Regatta: against an angler");
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        MatchTestGame("Skipper");
        state.coins = 120; state.day = 4; Give("rod_copper");
        yield return 4;
        Save();
        var realState = state; var realRng = rng;
        var diskBytes = File.ReadAllBytes(SaveFile.FilePath);
        string advAtStart = AdventureJson_Of(realState);
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        player.X = 266 + 9; player.Y = 198; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("E at an angler opens a choice of games that includes the Regatta", dockAnglerMenu == "bess" && Gfx.Seen.ContainsKey("Regatta") && Gfx.Seen.ContainsKey("Derby Duel"));
        pendingShot = "regatta-menu"; yield return 2;
        Check("both start places are open water for a hull", Enumerable.Range(0, 2).All(sx => BoatCanStand(RegattaRules.Slot(sx).x, RegattaRules.Slot(sx).y)));
        ClickButton("Regatta"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "regatta" && mode == "play"); i++) yield return 1;
        Check("Regatta fades in on the water at Daang Pulo", InRegatta && regSession != null && regBot != null && match.FromDock && match.Spec.Mode == "regatta" && match.Spec.Rival == "bess");
        Check("you are in a boat at the left start place, the angler's boat at the right", state.aboard && Has("boat") > 0 && MathF.Abs(player.X - RegattaRules.Slot(0).x) < 0.5f && MathF.Abs(player.Y - RegattaRules.Slot(0).y) < 0.5f && MathF.Abs(regRX - RegattaRules.Slot(1).x) < 0.5f);
        Check("the adventure is kept aside and untouched", ReferenceEquals(match.Real, realState) && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        route = RegattaRoute(0);
        float routeLen = 0; for (int i = 1; i < route.Count; i++) routeLen += MathF.Sqrt(MathF.Pow(route[i].x - route[i - 1].x, 2) + MathF.Pow(route[i].y - route[i - 1].y, 2));
        Check($"a route through the water exists to every gate and home ({routeLen:0} px, {route.Count} points) and every point can be sailed", routeLen > 1200 && routeLen < 2600 && route.All(p => BoatCanStand(p.x, p.y)));
        int px = RemoteBoatPixels();
        Check($"the other boat is drawn, with its rider ({px} pixels)", px > 80);
        pendingShot = "regatta-start"; yield return 2;
        for (int i = 0; i < 400 && regSession.HeatPhase != "count"; i++) yield return 1;
        Check("both boats ready, the count begins", regSession.HeatPhase == "count");
        float sx0 = player.X;
        Inp.Hold(KeyboardKey.D, true); yield return 20; Inp.Hold(KeyboardKey.D, false);
        Check("you cannot sail before the start", MathF.Abs(player.X - sx0) < 0.01f);
        for (int i = 0; i < 400 && regSession.HeatPhase != "race"; i++) yield return 1;
        Check("then the heat starts", regSession.HeatPhase == "race");
        // A real key moves the boat during the race.
        sx0 = player.X;
        Inp.Hold(KeyboardKey.A, true); yield return 20; Inp.Hold(KeyboardKey.A, false);
        Check($"steering by key moves your boat in the heat ({sx0 - player.X:0.0} px)", player.X < sx0 - 3);
        // Sail the rest by following the route, faster than the angler.
        Check("the angler's boat is under way too, and the camera follows yours", regSession.TheirSeen && MathF.Abs(camX + W / 2f - player.X) < W);
        bool sawBoth = false; int closePx = 0;
        float speed = 104;
        float offX = player.X - RouteAt(route, speed * regSession.HeatT).x, offY = player.Y - RouteAt(route, speed * regSession.HeatT).y, t0 = regSession.HeatT;
        for (int i = 0; i < 2400 && regSession.State == "play" && regSession.HeatPhase == "race"; i++)
        {
            var pt = RouteAt(route, speed * regSession.HeatT);
            float fade = Math.Max(0, 1 - (regSession.HeatT - t0) / 3f);
            pt.x += offX * fade; pt.y += offY * fade;
            player.X = pt.x; player.Y = pt.y; player.Face = boatFace = pt.face; state.boatX = pt.x; state.boatY = pt.y; player.Moving = true;
            if (!sawBoth && regSession.HeatT > 5 && MathF.Abs(regRX - player.X) < 110 && MathF.Abs(regRY - player.Y) < 70)
            {
                sawBoth = true; closePx = RemoteBoatPixels();
                pendingShot = "regatta-race"; yield return 2;
            }
            yield return 1;
        }
        Check($"for a while both boats were in view at once ({closePx} pixels of the other boat)", sawBoth && closePx > 60);
        for (int i = 0; i < 60 && regSession.HeatPhase != "result"; i++) yield return 1;
        Check($"the first heat is yours ({regSession.Results.Count} done, {regSession.MyWins}-{regSession.TheirWins}, {string.Join(' ', regSession.Results.Select(r => r.mine.ToString("0.0") + '/' + r.theirs.ToString("0.0")))}, mine reached gate {regSession.Mine.Next}, cheat {regSession.Mine.Cheat})", regSession.Results.Count == 1 && regSession.MyWins == 1 && regSession.Results[0].mine > 0 && regSession.Results[0].mine < RegattaRules.HeatLimit);
        for (int i = 0; i < 20 && regBannerT <= 0; i++) yield return 1;
        Check("a banner says who won the heat", regBanner.StartsWith("You win the heat"));
        pendingShot = "regatta-heat"; yield return 2;
        // The second heat: back on the line.
        for (int i = 0; i < 700 && !(regSession.Heat == 1 && regSession.HeatPhase == "race"); i++) yield return 1;
        Check("boats go back to the line for heat two and race again", regSession.Heat == 1 && regSession.HeatPhase == "race");
        for (int i = 0; i < 2400 && regSession.State == "play" && regSession.HeatPhase == "race"; i++)
        {
            var pt = RouteAt(route, speed * regSession.HeatT);
            player.X = pt.x; player.Y = pt.y; player.Face = boatFace = pt.face; state.boatX = pt.x; state.boatY = pt.y; player.Moving = true;
            yield return 1;
        }
        for (int i = 0; i < 400 && regSession.State != "over"; i++) yield return 1;
        Check($"two heats to none ends it: a win ({regSession.Result}, {regSession.MyWins}-{regSession.TheirWins})", regSession.State == "over" && regSession.Result == "win" && mode == "play");
        Check("it is on the board as a regatta win against the angler", Leaderboards.Board().entries.Any(e => e.mode == "regatta" && e.result == "win" && e.rival == "Old Bess" && e.score > 0));
        pendingShot = "regatta-result"; yield return 3;
        Check("the result card offers a rematch, the dock and leaving", Gfx.Seen.ContainsKey("Rematch") && Gfx.Seen.ContainsKey("Back to the dock") && Gfx.Seen.ContainsKey("Leave"));
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 600 && !(regSession.State == "play" && regSession.Heat == 0 && regSession.Results.Count == 0); i++) yield return 1;
        Check($"a rematch against an angler starts a fresh best of three ({regSession.State}/{regSession.HeatPhase}/{regSession.EndWhy}, heat {regSession.Heat}, {regSession.Results.Count} results, {regSession.Notice})", regSession.State == "play" && regSession.Results.Count == 0 && regSession.MyWins == 0);
        for (int i = 0; i < 300 && regSession.HeatPhase != "count"; i++) yield return 1;
        // Leaving mid-race forfeits.
        for (int i = 0; i < 400 && regSession.HeatPhase != "race"; i++) yield return 1;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"Esc during the race asks whether to leave ({regSession?.State}/{regSession?.HeatPhase}, {regSession?.Notice})", match.LeaveAsk);
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("leaving goes back to the dock, out of the boat, and counts as a forfeit", match.Phase == "dock" && !state.aboard && regSession == null && Leaderboards.Board().entries.Any(e => e.mode == "regatta" && e.result == "forfeit"));
        Inp.Tap(KeyboardKey.Escape); yield return 3; ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("leaving the dock puts the adventure back as it was", !MatchActive && ReferenceEquals(state, realState) && ReferenceEquals(rng, realRng) && !state.aboard && AdventureJson_Of(state) == advAtStart);

        // --- With a person ---
        if (Environment.GetEnvironmentVariable("FESH_REGATTA_TEST") == "alone") yield break;
        lan:
        Note("Regatta: against someone on the dock");
        route = RegattaRoute(0);
        lanDiscoveryPort = 47880; lanFirstPort = 47881; lanLoopbackOnly = true;
        MatchTestGame("Hosta");
        state.coins = 50; state.day = 3;
        yield return 4;
        Save();
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Host a dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 400 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        var vConn = new LanConnect("127.0.0.1", dockListener.Port);
        ILink vLink = null; for (int i = 0; i < 300 && vLink == null; i++) { vLink = vConn.Poll(); yield return 1; }
        var visitor = new DockClientCore(vLink, 5, 2, new Look { skin = 1, shirt = 4, hat = 3 });
        vVisitor = visitor; vVs = null; testEachFrame = VTick;
        for (int i = 0; i < 120 && !(visitor.State == "in" && dockClient.Roster.Count == 2); i++) { visitor.Tick(1f / 60); yield return 1; }
        var vp = dockClient.Others.First();
        visitor.SetPose(player.X + 12, player.Y, 3, 0);
        for (int i = 0; i < 80; i++) { visitor.Tick(1f / 60); yield return 1; }
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the challenge menu offers Sungka, the Derby Duel and the Regatta", dockChallengeTarget == vp.Id && Gfx.Seen.ContainsKey("Sungka") && Gfx.Seen.ContainsKey("Derby Duel") && Gfx.Seen.ContainsKey("Regatta"));
        pendingShot = "regatta-challenge-menu"; yield return 2;
        ClickButton("Regatta"); yield return 3; Inp.ScriptMouse = Offscreen;
        DockEvent rInv = null; for (int i = 0; i < 120 && rInv == null; i++) { visitor.Tick(1f / 60); rInv = visitor.Events.FirstOrDefault(x => x.Kind == "invite"); yield return 1; }
        Check("the visitor is asked to a Regatta", rInv != null && rInv.Mode == 2);
        visitor.Reply(rInv.Ch, true);
        DockEvent rGo = null; for (int i = 0; i < 120 && rGo == null; i++) { visitor.Tick(1f / 60); rGo = visitor.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        var vS = new RegattaSession("Visitor", rGo.Link, host: false);
        vVs = vS;
        for (int i = 0; i < 300 && !(match.Phase == "regatta" && mode == "play" && regSession != null && regSession.State == "play" && vS.State == "play"); i++) { yield return 1; }
        Check("accepting fades onto the water: you host (the left place), they are the guest", InRegatta && regSession.Host && regSession.MySeat == 0 && vS.MySeat == 1 && regBot == null && regRivalId == "net" && regPeerName == vp.Name);
        Check("the roster shows you both in a match", dockHost.Players.All(p => p.State == 1));
        var vPath = RegattaRoute(1);
        Check("their route from their place is sailable", vPath.All(p => BoatCanStand(p.x, p.y)));
        void VisitorDrives(float v)
        {
            if (vS.HeatPhase == "lineup" && vS.Heat != vReadyHeat) { vS.Ready(); vReadyHeat = vS.Heat; }
            if (vS.HeatPhase == "race") { var pt = RouteAt(vPath, v * vS.HeatT); vS.Position(pt.x, pt.y); vX = pt.x; vY = pt.y; }
        }
        vReadyHeat = -1;
        for (int i = 0; i < 700 && !(regSession.HeatPhase == "race" && vS.HeatPhase == "race"); i++) { VisitorDrives(96); yield return 1; }
        Check($"both boats ready and the heat starts for both (host {regSession.State}/{regSession.HeatPhase}, guest {vS.State}/{vS.HeatPhase}, match {match.Phase})", regSession.HeatPhase == "race" && vS.HeatPhase == "race");
        float lOffX = player.X - RouteAt(route, 90 * regSession.HeatT).x, lOffY = player.Y - RouteAt(route, 90 * regSession.HeatT).y, lT0 = regSession.HeatT;
        bool seenAt = false; int visPx = 0, theirPx = 0; float bestGap = 9999;
        for (int i = 0; i < 2400 && regSession.State == "play" && regSession.HeatPhase == "race"; i++)
        {
            var pt = RouteAt(route, 90 * regSession.HeatT);
            float lf = Math.Max(0, 1 - (regSession.HeatT - lT0) / 3f); pt.x += lOffX * lf; pt.y += lOffY * lf;
            player.X = pt.x; player.Y = pt.y; player.Face = boatFace = pt.face; state.boatX = pt.x; state.boatY = pt.y; player.Moving = true;
            VisitorDrives(96);
            if (!seenAt && regSession.HeatT > 6 && regSession.TheirSeen)
            {
                bestGap = MathF.Sqrt(MathF.Pow(regRX - vX, 2) + MathF.Pow(regRY - vY, 2));
                if (bestGap < 20 && MathF.Abs(regRX - player.X) < 120 && MathF.Abs(regRY - player.Y) < 70) { seenAt = true; visPx = RemoteBoatPixels(); pendingShot = "regatta-lan-race"; yield return 2; }
            }
            yield return 1;
        }
        Check($"you saw their boat where it really was, and drew it ({visPx} pixels; host {regSession.State}/{regSession.HeatPhase}/{regSession.EndWhy}/{regSession.Notice} heat {regSession.Heat} seen {regSession.TheirSeen} at {regRX:0},{regRY:0} vs {vX:0},{vY:0}; guest {vS.State}/{vS.HeatPhase}/{vS.EndWhy}/{vS.Notice}; theirs cheat {regSession.Theirs.Cheat}; hostT {regSession.HeatT:0.00} guestT {vS.HeatT:0.00} guestSawHost t={vS.Theirs.T:0.00} at {vS.Theirs.X:0},{vS.Theirs.Y:0} cheat {vS.Theirs.Cheat}; me {player.X:0},{player.Y:0})", seenAt && visPx > 60);
        float mineSeenByThem = MathF.Sqrt(MathF.Pow(vS.TheirX - player.X, 2) + MathF.Pow(vS.TheirY - player.Y, 2));
        Check($"and they saw yours ({mineSeenByThem:0} px off)", vS.TheirSeen && mineSeenByThem < 60);
        for (int i = 0; i < 90 && regSession.HeatPhase != "result"; i++) { VisitorDrives(96); yield return 1; }
        Check($"the faster boat takes the heat on both screens ({regSession.MyWins}-{regSession.TheirWins}, they {vS.MyWins}-{vS.TheirWins})", regSession.TheirWins == 1 && vS.MyWins == 1 && regSession.Results.Count == 1 && vS.Results.Count == 1);
        pendingShot = "regatta-lan-heat"; yield return 2;
        for (int i = 0; i < 700 && !(regSession.Heat == 1 && regSession.HeatPhase == "race" && vS.HeatPhase == "race"); i++) { VisitorDrives(96); yield return 1; }
        // The second heat: they are faster again.
        lOffX = player.X - RouteAt(route, 90 * regSession.HeatT).x; lOffY = player.Y - RouteAt(route, 90 * regSession.HeatT).y; lT0 = regSession.HeatT;
        for (int i = 0; i < 2400 && regSession.State == "play" && regSession.HeatPhase == "race"; i++)
        {
            var pt = RouteAt(route, 90 * regSession.HeatT);
            float lf = Math.Max(0, 1 - (regSession.HeatT - lT0) / 3f); pt.x += lOffX * lf; pt.y += lOffY * lf;
            player.X = pt.x; player.Y = pt.y; player.Face = boatFace = pt.face; state.boatX = pt.x; state.boatY = pt.y; player.Moving = true;
            if (vS.HeatPhase == "race") { var cp = RouteAt(vPath, 100 * vS.HeatT); vS.Position(cp.x, cp.y); vX = cp.x; vY = cp.y; }
            yield return 1;
        }
        for (int i = 0; i < 90 && regSession.HeatPhase != "result" && regSession.State == "play"; i++) { yield return 1; }
        Check($"a second heat is decided ({regSession.MyWins}-{regSession.TheirWins})", regSession.Results.Count >= 2 || regSession.State == "over");
        // They leave mid-match.
        for (int i = 0; i < 700 && regSession.State == "play" && regSession.HeatPhase != "race"; i++) { VisitorDrives(96); yield return 1; }
        if (regSession.State == "play")
        {
            vS.Leave();
            for (int i = 0; i < 60; i++) { visitor.Tick(1f / 60); yield return 1; }
        }
        Check("when they leave, the match is over with a result for you", regSession.State == "over" && regSession.Result != "");
        pendingShot = "regatta-lan-left"; yield return 3;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("Leave goes straight to the island and the boat is put away", !MatchActive && !state.aboard && regSession == null);
        testEachFrame = null;
        visitor.Close();
    }

    // The test visitor runs on the same clock as the game does, so the two sides agree about time.
    void VTick() { float d = MathF.Min(0.05f, Raylib.GetFrameTime()); vVisitor?.Tick(d); vVs?.Tick(d); }
    DockClientCore vVisitor; RegattaSession vVs;
    int vReadyHeat = -1;
    float vX, vY;
}
#endif
