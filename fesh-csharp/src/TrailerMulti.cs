#if DEBUG
using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The trailer's multiplayer: a Tournament Dock hosted over the LAN with two friends walking about on it (each a real
// client over a loopback socket), then a Derby Duel, a regatta, sungka with a friend, and the Sea School Duel.
partial class Game
{
    sealed class Visitor { public DockClientCore C; public SungkaSession Ses; public float X, Y, Tx, Ty, Wait; public int Face = 2, Step; public float WalkT; public bool Stay; }
    readonly List<Visitor> visitors = new();
    readonly Random visRng = new(11);

    // Each frame: friends stroll to a spot on the dock, stand a while (now and then saying something), and go on.
    void TickVisitors()
    {
        foreach (var v in visitors)
        {
            v.C.Tick(FixedDt);
            v.Ses?.Tick(FixedDt);
            if (v.C.State != "in") continue;
            float dx = v.Tx - v.X, dy = v.Ty - v.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            if (v.Wait > 0) { v.Wait -= FixedDt; v.Step = 0; }
            else if (d < 1 && v.Stay) { v.Step = 0; v.Face = 3; }
            else if (d < 1)
            {
                v.Wait = 0.8f + (float)visRng.NextDouble() * 2.2f;
                // Somewhere not right beside an angler or you, so E there still means the angler.
                for (int k = 0; k < 30; k++)
                {
                    v.Tx = DockX0 + 6 + (float)visRng.NextDouble() * (DockX1 - DockX0 - 12);
                    v.Ty = DockY0 + 4 + (float)visRng.NextDouble() * (DockY1 - DockY0 - 8);
                    if (DockAnglers.All(a => Dist(a.x, a.y, v.Tx, v.Ty) > 24) && Dist(player.X, player.Y, v.Tx, v.Ty) > 24) break;
                }
                if (visRng.Next(3) == 0) v.C.Emote(visRng.Next(DockRules.Emotes.Length));
            }
            else
            {
                float sp = 44 * FixedDt;
                v.X += dx / d * MathF.Min(sp, d); v.Y += dy / d * MathF.Min(sp, d);
                v.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? 1 : 3) : (dy > 0 ? 2 : 0);
                v.WalkT += FixedDt * 9;
                v.Step = 1 + (int)v.WalkT % 4;
            }
            v.C.SetPose(v.X, v.Y, v.Face, v.Step);
        }
    }

    // The dock: the Matches panel's Host a dock, two friends join, and you walk over to the anglers.
    IEnumerable<int> ShotDock()
    {
        TrailerGame(day: 4);
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        player.X = 160; player.Y = 115;
        foreach (var _ in Frames(4)) yield return 0;
        OpenMatches("dock");
        foreach (var _ in Frames(4)) yield return 0;
        Roll(30f);   // (the next Roll, once the friends are on, sets the real length)
        foreach (var _ in Frames(40)) yield return 0;
        ClickButton("Host a dock"); foreach (var _ in Frames(3)) yield return 0; Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 400 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 0;
        // Two friends join over the network.
        var looks = new[] { new Look { skin = 3, hair = 1, hairColor = 2, shirt = 4, pants = 0, hat = 2 }, new Look { skin = 0, hair = 0, hairColor = 4, shirt = 2, pants = 3, hat = 3 } };
        for (int k = 0; k < 2; k++)
        {
            var conn = new LanConnect("127.0.0.1", dockListener.Port);
            ILink link = null;
            for (int i = 0; i < 300 && link == null; i++) { link = conn.Poll(); yield return 0; }
            var v = new Visitor { C = new DockClientCore(link, 3 + k * 5, 2 + k * 7, looks[k]), X = DockSpawnX + 20 + k * 14, Y = DockSpawnY - 6 + k * 8 };
            v.Tx = v.X; v.Ty = v.Y; v.Wait = 0.5f + k;
            visitors.Add(v);
        }
        testEachFrame += TickVisitors;
        for (int i = 0; i < 120 && visitors.Any(v => v.C.State != "in"); i++) yield return 0;
        foreach (var _ in Frames(30)) yield return 0;
        Roll(5f, "MULTIPLAYER", "The Tournament Dock", "Friends on your LAN meet here");
        foreach (var _ in Frames(30)) yield return 0;
        foreach (var _ in WalkTo(305, 200, 200, ok: DockOk)) yield return 0;
        Inp.Tap(KeyboardKey.Two);
        ParkVisitors(276, 199, true);
        foreach (var _ in WalkTo(266 + 10, 199, 200, ok: DockOk)) yield return 0;
        player.Face = "left";
        foreach (var _ in Frames(200)) yield return 0;
    }

    // A Derby Duel against Old Bess, fishing the same seeded casts.
    IEnumerable<int> ShotDuel()
    {
        if (!InDock) yield break;
        player.Face = "left";
        Roll(10f, "MULTIPLAYER", "Derby Duel", "Three minutes, the same fish for both. Heaviest bag wins.");
        Note($"duel: at {player.X:0},{player.Y:0} mode {mode} busy {DockBusy} menu {dockAnglerMenu} ch {dockChallengeTarget} inv {dockInvite != null} pend {dockPending}");
        foreach (var _ in AskAngler(266 + 10, 199, "Derby Duel")) yield return 0;
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 0;
        for (int c = 0; c < 4 && match.Phase == "run"; c++)
        {
            FaceToSpot("lagoon");
            foreach (var _ in CastAndHook(18, perfect: c % 2 == 0)) yield return 0;
            foreach (var _ in Reel(400)) yield return 0;
            foreach (var _ in Frames(20)) yield return 0;
        }
    }

    // Out of whatever match is on and back onto the dock itself (a duel or a race begun there goes back there).
    IEnumerable<int> BackToDock()
    {
        ReleaseKeys();
        if (!MatchActive) yield break;
        if (lan != null) { CloseSungkaView(); foreach (var _ in Frames(10)) yield return 0; }
        if (!(match.Phase == "dock" && !match.FromDock)) LeaveMatch();
        for (int i = 0; i < 600 && !(MatchActive && match.Phase == "dock" && !match.FromDock && mode == "play" && fade <= 0); i++) yield return 0;
        foreach (var _ in Frames(10)) yield return 0;
        Note($"back on the dock: {match?.Phase} {mode} fromDock {match?.FromDock}");
    }
    // Ground you can walk on the dock, with a little room round the anglers and the board.
    static bool DockOk(float x, float y) => x >= DockX0 && x <= DockX1 && y >= DockY0 && y <= DockY1 && !DockBlocked(x, y) && !DockBlocked(x + 3, y) && !DockBlocked(x - 3, y) && !DockBlocked(x, y + 3) && !DockBlocked(x, y - 3);

    // Friends wander off to the far side of the dock while you go and talk to an angler (and are free again after).
    void ParkVisitors(float ax, float ay, bool park)
    {
        var spots = new[] { (258f, 208f), (288f, 209f), (322f, 210f), (302f, 199f), (278f, 190f), (316f, 190f) }.OrderByDescending(s => Dist(s.Item1, s.Item2, ax, ay)).ToList();
        for (int i = 0; i < visitors.Count; i++)
        {
            var v = visitors[i];
            v.Stay = park; if (!park) continue;
            v.Tx = spots[i].Item1; v.Ty = spots[i].Item2; v.Wait = 0;
        }
    }

    // Asks an angler on the dock for a game, the way you would: E beside them, then the game's button.
    IEnumerable<int> AskAngler(float x, float y, string game)
    {
        ParkVisitors(x, y, true);
        foreach (var _ in WalkTo(x, y, 300, ok: DockOk)) yield return 0;
        player.X = x; player.Y = y;
        for (int tries = 0; tries < 6 && dockAnglerMenu == null; tries++) { foreach (var _ in Frames(4)) yield return 0; if (target?.Type != "dockrival") { Note($"  ask {game}: target {target?.Type} {target?.Label}"); continue; } Inp.Tap(KeyboardKey.E); foreach (var _ in Frames(4)) yield return 0; }
        foreach (var _ in Frames(16)) yield return 0;   // a moment to read the menu
        for (int tries = 0; tries < 4 && dockAnglerMenu != null; tries++) { Note($"  click {game}: seen {Gfx.Seen.GetValueOrDefault(game)}"); ClickButton(game); foreach (var _ in Frames(4)) yield return 0; Inp.ScriptMouse = Offscreen; }
        Note($"asked for {game}: menu {dockAnglerMenu}, phase {match?.Phase}, seen {Gfx.Seen.ContainsKey(game)}, target {target?.Label}");
        ParkVisitors(x, y, false);
    }

    // Back on the dock, a regatta against Marlo round Dado's course at Daang Pulo.
    IEnumerable<int> ShotRegatta()
    {
        if (!MatchActive) yield break;
        foreach (var _ in BackToDock()) yield return 0;
        player.Face = "left";
        foreach (var _ in AskAngler(296 + 9, 193, "Regatta")) yield return 0;
        for (int i = 0; i < 400 && !(match.Phase == "regatta" && mode == "play"); i++) yield return 0;
        if (regSession == null) { Check($"regatta: no race started ({match?.Phase}, {mode}, menu {dockAnglerMenu}, spec {match?.Spec?.Mode}, toast {MutedToast})", false); yield break; }
        var route = RegattaRoute(0);
        for (int i = 0; i < 600 && regSession.HeatPhase != "count"; i++) yield return 0;
        Roll(7f, "MULTIPLAYER", "Head-to-head regatta", "Best of three heats, boat against boat");
        for (int i = 0; i < 600 && regSession.HeatPhase != "race"; i++) yield return 0;
        int at = 0;
        for (int i = 0; i < 2000 && regSession is { HeatPhase: "race" }; i++)
        {
            while (at < route.Count - 1 && Dist(player.X, player.Y, route[at].x, route[at].y) < 10) at++;
            float dx = route[at].x - player.X, dy = route[at].y - player.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            Inp.ScriptStick = new Vector2(dx / d, dy / d);
            yield return 0;
        }
        Inp.ScriptStick = null;
    }

    // Sungka with one of the friends: you challenge them on the dock, they accept, and you take turns.
    IEnumerable<int> ShotSungka()
    {
        if (!MatchActive) yield break;
        foreach (var _ in BackToDock()) yield return 0;
        var v = visitors.FirstOrDefault();
        if (v == null) yield break;
        var vp = dockClient.Others.FirstOrDefault();
        player.X = 284; player.Y = 210; player.Face = "right";
        v.Tx = player.X + 9; v.Ty = player.Y; v.Wait = 0; v.Stay = true;
        foreach (var _ in Frames(90)) yield return 0;
        Roll(7f, "MULTIPLAYER", "Sungka over LAN", "The islands' shell game, against a friend");
        Note($"sungka: phase {match.Phase}, target {target?.Type} {target?.Label}, visitor {v.C.State} at {v.X:0},{v.Y:0}, me {player.X:0},{player.Y:0}, others {string.Join(" ", dockClient.Others.Select(o => $"{o.Name}@{o.X:0},{o.Y:0}s{o.State}"))}");
        Inp.Tap(KeyboardKey.E);
        foreach (var _ in Frames(4)) yield return 0;
        Note($"sungka: menu {dockChallengeTarget}, seen {Gfx.Seen.ContainsKey("Sungka")}");
        ClickButton("Sungka"); foreach (var _ in Frames(3)) yield return 0; Inp.ScriptMouse = Offscreen;
        DockEvent inv = null; for (int i = 0; i < 200 && inv == null; i++) { inv = v.C.Events.LastOrDefault(x => x.Kind == "invite"); yield return 0; }
        if (inv == null) yield break;
        v.C.Events.Clear(); v.C.Reply(inv.Ch, true);
        DockEvent go = null; for (int i = 0; i < 200 && go == null; i++) { go = v.C.Events.FirstOrDefault(x => x.Kind == "go"); yield return 0; }
        if (go == null) yield break;
        v.Ses = new SungkaSession(vp?.Name ?? "Friend", go.Link);
        for (int i = 0; i < 200 && !(lan?.State == "play" && v.Ses.State == "play"); i++) yield return 0;
        var pr = new Random(3);
        float think = 0.8f;
        for (int i = 0; i < 1200 && lan?.State == "play"; i++)
        {
            think -= FixedDt;
            if (think <= 0)
            {
                if (v.Ses.MyTurn) { var lg = Enumerable.Range(8, 7).Where(p => v.Ses.Board.CanMove(1, p)).ToList(); if (lg.Count > 0) v.Ses.Move(lg[pr.Next(lg.Count)]); think = 1.2f; }
                else if (lan.MyTurn)
                {
                    var lg = Enumerable.Range(0, 7).Where(p => lan.Board.Pits[p] > 0).ToList();
                    if (lg.Count > 0 && ClickButton($"sungka:{lg[pr.Next(lg.Count)]}")) { foreach (var _ in Frames(3)) yield return 0; Inp.ScriptMouse = Offscreen; }
                    think = 1.2f;
                }
            }
            yield return 0;
        }
    }

    // The Sea School Duel against Captain Ivy: eight questions, both sides answering the same ones.
    IEnumerable<int> ShotQuiz()
    {
        if (!MatchActive) yield break;
        foreach (var _ in BackToDock()) yield return 0;

        foreach (var _ in Frames(3)) yield return 0;
        Roll(9f, "MULTIPLAYER", "Sea School Duel", "Eight questions about the sea. Most right wins.");
        foreach (var _ in AskAngler(310 - 9, 205, "Sea School Duel")) yield return 0;
        for (int i = 0; i < 400 && !(match.Phase == "quiz" && mode == "play" && quizSession is { State: "play" }); i++) yield return 0;
        int seed = quizSession.Seed;
        for (int k = 0; k < 8; k++)
        {
            for (int i = 0; i < 400 && !(quizShownK == k && quizFeedT <= 0 && quizQT > 2.2f); i++) yield return 0;
            int rp = QuizRules.RightPlace(seed, k);
            if (k == 2) rp = (rp + 1) % 4;   // nobody gets them all
            ClickButton($"quizq:{rp}"); foreach (var _ in Frames(3)) yield return 0; Inp.ScriptMouse = Offscreen;
        }
    }

    // The end: back on the title, the islands drifting by behind the name.
    IEnumerable<int> ShotEnd()
    {
        CloseTrailerDock();
        for (int i = 0; i < 200 && MatchActive; i++) yield return 0;
        if (mode != "title") QuitToTitle();
        titleView = "main"; introDone = true;
        foreach (var _ in Frames(60)) yield return 0;
        Roll(6f);
        foreach (var _ in Frames(240)) yield return 0;
    }

    // Leaving the dock for the end card: the island back as it was, and the friends gone.
    void CloseTrailerDock()
    {
        testEachFrame -= TickVisitors;
        foreach (var v in visitors) { v.Ses?.Close(); }
        visitors.Clear();
        if (MatchActive) { ShutDockNet(); AbandonMatch(); }
    }
}
#endif
