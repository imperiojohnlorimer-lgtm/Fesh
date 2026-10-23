#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The Tournament Dock (1.30): visiting alone, walking about it, the anglers, the notice board, quick phrases, a Derby
    // Duel from the dock and back, leaving, and that none of it touches the adventure. FESH_DOCK_TEST=1 runs only this.
    IEnumerable<int> DockScript()
    {
        Note("Dock: visiting alone");
        bool siteOpen = true;
        MatchTestGame("Docker0"); yield return 3;
        for (int ty = DockSiteY0; ty <= DockSiteY1; ty++) for (int tx = DockSiteX0; tx <= DockSiteX1; tx++) if (map[ty, tx] is not ('~' or 'w')) siteOpen = false;
        Check("the dock's own place in the sea south of Saltmere is open water to begin with, and the walk area sits inside the islet", siteOpen);
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        MatchTestGame("Docker");
        state.coins = 200; state.day = 5; Give("rod_copper"); state.xp = 500;
        yield return 4;
        Save();
        var realState = state; var realRng = rng;
        var diskBytes = File.ReadAllBytes(SaveFile.FilePath);
        float realX = player.X, realY = player.Y;
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the Matches panel has a Tournament Dock tab with a Visit button", matchesTab == "dock" && Gfx.Seen.ContainsKey("Visit alone"));
        pendingShot = "dock-tab"; yield return 2;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("Visit alone fades in on the dock", MatchActive && match.Phase == "dock" && match.Dock && !match.FromDock && mode == "play");
        string advAtStart = AdventureJson_Of(realState);
        diskBytes = File.ReadAllBytes(SaveFile.FilePath);
        Check("the dock stands on an islet of its own: land where you walk, open sea beyond it", map[(int)(DockSpawnY / T), (int)(DockSpawnX / T)] is 'g' or 's' && map[(int)(DockY1 / T) + 5, (int)(DockSpawnX / T)] is not ('g' or 's'));
        Check("it runs on a tournament state (the Old rod only), with the adventure kept aside", !ReferenceEquals(state, realState) && state.inv.Count == 1 && ReferenceEquals(match.Real, realState) && ReferenceEquals(match.RealRng, realRng));
        Check("you stand at the dock's spawn, facing the sea", MathF.Abs(player.X - DockSpawnX) < 0.01f && MathF.Abs(player.Y - DockSpawnY) < 0.01f);
        pendingShot = "dock-start"; yield return 2;
        // Walking about it, and not beyond it (each from a clear spot, since the anglers and the board are solid).
        foreach (var (key, name, sx, sy) in new[] { (KeyboardKey.A, "left", 263f, 191f), (KeyboardKey.W, "up", 285f, 196f), (KeyboardKey.D, "right", 323f, 187.5f), (KeyboardKey.S, "down", 285f, 199f) })
        {
            player.X = sx; player.Y = sy; yield return 2;
            Inp.Hold(key, true); yield return 300; Inp.Hold(key, false);
            bool atEdge = name switch { "left" => player.X <= DockX0 + 0.01f, "up" => player.Y <= DockY0 + 0.01f, "right" => player.X >= DockX1 - 0.01f, _ => player.Y >= DockY1 - 0.01f };
            Check($"walking {name} reaches the dock's edge and stops there ({player.X:0.0}, {player.Y:0.0})", atEdge && player.X >= DockX0 - 0.01f && player.X <= DockX1 + 0.01f && player.Y >= DockY0 - 0.01f && player.Y <= DockY1 + 0.01f);
        }
        player.X = 275; player.Y = 198; Inp.Hold(KeyboardKey.A, true); yield return 120; Inp.Hold(KeyboardKey.A, false);
        Check($"the anglers are solid: walking into Old Bess stops short of her ({player.X:0.0})", player.X > 266 + 5.9f);
        Check("the clock stands still at noon and nothing was saved", state.clock == 720 && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        // The anglers.
        player.X = 266 + 9; player.Y = 198; yield return 3;
        Check("next to Old Bess, E offers a challenge to her", target?.Type == "dockrival" && target.Id == "bess" && target.Label.Contains("Old Bess"));
        pendingShot = "dock-bess"; yield return 2;
        player.X = 326; player.Y = 199; yield return 3;
        Check("next to the notice board, E offers to read it", target?.Type == "dockboard");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("E opens the board", match.BoardOpen);
        pendingShot = "dock-board"; yield return 2;
        // The showroom (1.46): the board's third page, with your own card and arrows for your favourite fish.
        realState.records[ShowcaseFish[0]] = 1.2f; realState.records[ShowcaseFish[3]] = 1.5f; realState.favFish = null;
        ClickButton("More boards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the dock's second board page has a Showroom button beside First boards", boardPage == 1 && Gfx.Seen.ContainsKey("Showroom") && Gfx.Seen.ContainsKey("First boards"));
        ClickButton("Showroom"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the showroom shows you, alone, and nobody else", boardPage == 2 && Gfx.Seen.ContainsKey("show:card:0") && !Gfx.Seen.ContainsKey("show:card:1") && ShowEntries().Count == 1 && ShowEntries()[0].Mine);
        pendingShot = "dock-showroom"; yield return 3;
        Check($"its card fits the board ({lastShowroomBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastShowroomBottom > 0 && lastShowroomBottom <= panelAt.y + panelAt.h - 12);
        ClickButton("show:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"the arrow picks a favourite from the fish you hold a record for, in the real save ({realState.favFish})", realState.favFish == ShowcaseFish[0] && state.favFish == null);
        ClickButton("show:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("and the next one", realState.favFish == ShowcaseFish[3]);
        ClickButton("Back to boards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("First boards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Back to boards and First boards return to the first page", boardPage == 0);
        realState.records.Remove(ShowcaseFish[0]); realState.records.Remove(ShowcaseFish[3]); realState.favFish = null;
        Inp.Tap(KeyboardKey.W); yield return 3;
        Check("keys do nothing behind the board", match.BoardOpen && MathF.Abs(player.X - 326) < 0.5f);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc closes the board and nothing more", !match.BoardOpen && !match.LeaveAsk && InDock);
        // Quick phrases.
        Inp.Tap(KeyboardKey.One); yield return 3;
        Check("key 1 says the first phrase", match.Emote == Emotes[0] && time < match.EmoteT);
        pendingShot = "dock-emote"; yield return 2;
        ClickButton("emote:5"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("clicking a phrase says it too", match.Emote == Emotes[5]);
        Check("there is no free text: the phrases are a fixed list of eight", Emotes.Length == 8 && Emotes.All(e => e.Length < 16));

        // A Derby Duel from the dock.
        Note("Dock: a Derby Duel and back");
        int gen0 = match.Gen;
        player.X = 266 + 9; player.Y = 198; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("E at an angler opens a choice of games", dockAnglerMenu == "bess");
        ClickButton("Derby Duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "countdown" && mode == "play"); i++) yield return 1;
        Check("talking to Old Bess starts a Derby Duel against her, counting down", match.Phase == "countdown" && match.Spec.Mode == "derby" && match.Spec.Rival == "bess" && match.FromDock && match.RivalName == "Old Bess" && match.Gen == gen0 + 1);
        Check("at the lagoon, on fresh belongings", MathF.Abs(player.X - MatchX) < 0.01f && state.inv.Count == 1 && state.coins == 0);
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        match.Me = new MatchScore { Cg = 5000, Perfects = 2, Catches = 3 };
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("the match ends on its result card, a win with Pip's prize", mode == "matchresult" && match.Result.Result == "win" && match.Result.Prize.Contains("30 coins"));
        Check("the card offers the dock, not the island", Gfx.Seen.ContainsKey("Back to the dock") && Gfx.Seen.ContainsKey("Rematch"));
        pendingShot = "dock-result"; yield return 2;
        ClickButton("Back to the dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("Back to the dock returns to the dock: spawn point, fresh belongings, no match", match.Phase == "dock" && !match.FromDock && match.RivalRun == null && MathF.Abs(player.X - DockSpawnX) < 0.01f && state.inv.Count == 1 && match.Spec.Mode == "dock");
        Check("the adventure is still kept aside, with nothing paid yet", ReferenceEquals(match.Real, realState) && realState.coins == 200 && match.Won.Contains("bess"));
        Check("Pip's prize waits until you leave the dock, and the save is untouched", File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        // A rematch and a leaver: Leave from the countdown or a run goes back to the dock, with a forfeit.
        player.X = 296 + 9; player.Y = 193; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        ClickButton("Derby Duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "countdown" && mode == "play"); i++) yield return 1;
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        string leaverId = match.Spec.Id;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("leaving a duel that began on the dock goes back to the dock, and counts as a forfeit", match.Phase == "dock" && Leaderboards.Board().entries.Any(e => e.id == leaverId && e.result == "forfeit"));
        // Esc on the dock asks about leaving it.
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc on the dock asks whether to leave it", match.LeaveAsk);
        pendingShot = "dock-leave"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc again stays", !match.LeaveAsk && InDock);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("and the sea is open water again where the islet was", map[19, 28] is '~' or 'w');
        Check("leaving the dock puts the island back, once", !MatchActive && ReferenceEquals(state, realState) && ReferenceEquals(rng, realRng) && mode == "play");
        Check("you are where you were", MathF.Abs(player.X - realX) < 0.01f && MathF.Abs(player.Y - realY) < 0.01f);
        Check("Pip paid Old Bess's prize into the adventure on the way out", state.coins == 230 && state.gifts.GetValueOrDefault("duel_bess") == 5);
        Check("and nothing else about the adventure changed", AdventureJson_Of(state) == advAtStart && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));

        // Leaving from a result card, and closing the window on the dock.
        Note("Dock: leaving from a result, and the window closing");
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        player.X = 301; player.Y = 205; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        ClickButton("Derby Duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "countdown" && mode == "play"); i++) yield return 1;
        for (int i = 0; i < 400 && match.Phase != "run"; i++) yield return 1;
        match.Me = new MatchScore { Cg = 99999 }; match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("a win over Captain Ivy from the dock", mode == "matchresult" && match.Result.Result == "win");
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) yield return 1;
        Check($"Leave on the card goes straight to the island and pays Ivy's prize (active {MatchActive}, coins {state.coins}, bait {Has("glow_bait")}, mode {mode})", !MatchActive && state.coins == 330 && Has("glow_bait") == 3);
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        AbandonMatch(); Save();
        Check("closing the window on the dock puts the adventure back and it is what gets saved", !MatchActive && ReferenceEquals(state, realState) && SaveFile.Read(SaveFile.Slot)?.coins == 330);
        // A stale fade can't act on a later state.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        StartDockDuel("marlo");
        int staleGen = match.Gen;
        match.Gen++;      // something else happened first (as a faster click would do)
        for (int i = 0; i < 60; i++) yield return 1;
        Check("a fade started for an older state does nothing when it finishes", match.Phase == "ready" && !match.FromDock);
        AbandonMatch();

        // --- With other people ---
        Note("Dock: hosting a dock for others");
        lanDiscoveryPort = 47870; lanFirstPort = 47871; lanLoopbackOnly = true;
        MatchTestGame("Hostess");
        state.coins = 50; state.day = 3;
        yield return 4;
        Save();
        string hostAdv = AdventureJson();
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the dock tab offers visiting alone, hosting and finding", Gfx.Seen.ContainsKey("Visit alone") && Gfx.Seen.ContainsKey("Host a dock") && Gfx.Seen.ContainsKey("Find a dock"));
        pendingShot = "dock-tab-net"; yield return 2;
        ClickButton("Host a dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 400 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("Host a dock opens a port and walks you onto the dock", MatchActive && match.Phase == "dock" && dockHost != null && dockListener != null && dockClient != null && dockClient.State == "in" && dockHost.Count == 1);
        // A visitor (the test's own client) joins by real socket.
        var vConn = new LanConnect("127.0.0.1", dockListener.Port);
        ILink vLink = null; for (int i = 0; i < 300 && vLink == null; i++) { vLink = vConn.Poll(); yield return 1; }
        var visitor = new DockClientCore(vLink, 7, 3, new Look { skin = 1, shirt = 4, hat = 3 });
        for (int i = 0; i < 120 && !(visitor.State == "in" && dockClient.Roster.Count == 2); i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("a visitor joins over the network and both see two players", visitor.State == "in" && dockClient.Roster.Count == 2 && visitor.Roster.Count == 2 && dockHost.Count == 2);
        var vp = dockClient.Others.First();
        Check("the visitor is called by a name from the lists", vp.Name == Leaderboards.NameOf(7, 3));
        // Put them next to you and look at the screen.
        visitor.SetPose(player.X + 12, player.Y, 3, 0);
        for (int i = 0; i < 80; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("their walk reaches your screen", MathF.Abs(vp.TX - (player.X + 12)) < 3);
        int PlayersPx()
        {
            RenderWorld(time);
            var with = (Color[])pix.Buf.Clone();
            var keepClient = dockClient; var others = dockClient.Roster.ToList();
            dockClient.Roster.RemoveAll(p => p.Id != dockClient.MyId);
            RenderWorld(time);
            foreach (var o in others) if (o.Id != dockClient.MyId) dockClient.Roster.Add(o);
            int diff = 0; for (int i = 0; i < with.Length; i++) if (with[i].R != pix.Buf[i].R || with[i].G != pix.Buf[i].G || with[i].B != pix.Buf[i].B) diff++;
            return diff;
        }
        Check($"the visitor is drawn on the dock as a person ({PlayersPx()} pixels)", PlayersPx() > 60);
        pendingShot = "dock-visitor"; yield return 2;
        Check("next to them, E offers a challenge", target?.Type == "dockplayer" && target.Label.Contains(vp.Name));
        // Phrases both ways.
        visitor.Emote(2);
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("their phrase is over their head on your screen", vp.Emote == DockRules.Emotes[2] && dockClient.Now() < vp.EmoteUntil);
        pendingShot = "dock-visitor-emote"; yield return 2;
        Inp.Tap(KeyboardKey.Four); yield return 3;
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("and yours reaches them", visitor.Roster.First(p => p.Id == dockClient.MyId).Emote == DockRules.Emotes[3]);

        // I challenge them: sungka, I am the referee.
        Note("Dock: a challenge from you");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("E opens the challenge menu for them", dockChallengeTarget == vp.Id);
        pendingShot = "dock-challenge-menu"; yield return 2;
        Inp.Tap(KeyboardKey.W); yield return 2;
        Check("keys do nothing behind it", dockChallengeTarget == vp.Id);
        ClickButton("Sungka"); yield return 3; Inp.ScriptMouse = Offscreen;
        DockEvent vInv = null; for (int i = 0; i < 120 && vInv == null; i++) { visitor.Tick(1f / 60); vInv = visitor.Events.FirstOrDefault(x => x.Kind == "invite"); yield return 1; }
        Check("the challenge reaches them: who and which game", vInv != null && vInv.From == dockClient.MyId && vInv.Mode == 0);
        Check("and you are waiting for the answer", dockPending != 0);
        visitor.Reply(vInv.Ch, false);
        for (int i = 0; i < 60; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("they say no: you are told and free again", dockPending == 0 && !DockBusy && InDock);
        visitor.Events.Clear();
        for (int i = 0; i < 200; i++) { visitor.Tick(1f / 60); yield return 1; }      // the invite bucket refills
        Inp.Tap(KeyboardKey.E); yield return 3;
        ClickButton("Sungka"); yield return 3; Inp.ScriptMouse = Offscreen;
        vInv = null; for (int i = 0; i < 120 && vInv == null; i++) { visitor.Tick(1f / 60); vInv = visitor.Events.LastOrDefault(x => x.Kind == "invite" && x.Ch != 0 && x != vInv); yield return 1; }
        visitor.Events.Clear();
        visitor.Reply(vInv.Ch, true);
        DockEvent vGo = null; for (int i = 0; i < 120 && vGo == null; i++) { visitor.Tick(1f / 60); vGo = visitor.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        for (int i = 0; i < 30 && !(match.Phase == "sungka"); i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("they accept: a sungka game starts through the dock, you as the referee (seat 0)", vGo != null && vGo.Role == 1 && match.Phase == "sungka" && lan != null && lan.MySeat == 0);
        var vSes = new SungkaSession("Visitor", vGo.Link);
        for (int i = 0; i < 90 && !(lan.State == "play" && vSes.State == "play"); i++) { visitor.Tick(1f / 60); vSes.Tick(1f / 60); yield return 1; }
        Check($"the game is on: names, one match (lan {lan.State}/{lan.RivalName}/{lan.MyName}, v {vSes.State}/{vSes.RivalName})", lan.State == "play" && vSes.State == "play" && lan.RivalName == "Visitor" && vSes.RivalName == lan.MyName && lan.MatchId == vSes.MatchId);
        Check("the roster shows you both in a match", dockHost.Players.All(p => p.State == 1));
        pendingShot = "dock-sungka"; yield return 2;
        int myMoves = 0, theirMoves = 0; var pr2 = new Random(8);
        for (int spin = 0; spin < 3000 && (myMoves < 4 || theirMoves < 4) && lan.State == "play"; spin++)
        {
            visitor.Tick(1f / 60); vSes.Tick(1f / 60);
            if (vSes.MyTurn) { var lg = Enumerable.Range(8, 7).Where(p => vSes.Board.CanMove(1, p)).ToList(); if (lg.Count > 0) { vSes.Move(lg[pr2.Next(lg.Count)]); theirMoves++; } }
            else if (lan.MyTurn)
            {
                int v = Enumerable.Range(0, 7).FirstOrDefault(i => lan.Board.Pits[i] > 0, -1);
                if (v >= 0 && ClickButton($"sungka:{v}")) { yield return 2; Inp.ScriptMouse = Offscreen; myMoves++; }
            }
            yield return 1;
        }
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); vSes.Tick(1f / 60); yield return 1; }
        Check($"moves by real clicks and by the visitor keep both boards the same ({myMoves} + {theirMoves})", lan.Board.Revision == vSes.Board.Revision && lan.Board.Digest() == vSes.Board.Digest() && lan.Board.Pits.Sum() == 98 && myMoves >= 4);
        lan.MatchLeft = 0.01f; lan.TurnLeft = 99; vSes.MatchLeft = 0.01f;
        for (int i = 0; i < 40; i++) { visitor.Tick(1f / 60); vSes.Tick(1f / 60); yield return 1; }
        Check("the clock ends it for both and the result is on your board", lan.State == "over" && vSes.State == "over" && Leaderboards.Board().entries.Any(e => e.mode == "sungka" && e.id == lan.RecordId));
        pendingShot = "dock-sungka-over"; yield return 2;
        ClickButton("Close"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); vSes.Tick(1f / 60); yield return 1; }
        Check("Close brings the dock back (you on it, free to move) and the visitor is told the match is over", match.Phase == "dock" && lan == null && !vSes.Link.Alive && dockHost.Players.All(p => p.State == 0));

        // They challenge me.
        Note("Dock: a challenge to you");
        visitor.Events.Clear(); vSes.Close();
        for (int i = 0; i < 200; i++) { visitor.Tick(1f / 60); yield return 1; }
        visitor.Invite(dockClient.MyId, 0);
        for (int i = 0; i < 60 && dockInvite == null; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("their challenge shows on your screen with Accept and No thanks", dockInvite != null && Gfx.Seen.ContainsKey("Accept"));
        pendingShot = "dock-invite"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("keys and E do nothing behind it", dockInvite != null && dockChallengeTarget < 0);
        ClickButton("Accept"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        DockEvent vGo2 = null; for (int i = 0; i < 120 && vGo2 == null; i++) { visitor.Tick(1f / 60); vGo2 = visitor.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        var vSes2 = new SungkaSession("Visitor"); vSes2.AttachGuest(vGo2.Link);
        for (int i = 0; i < 120 && !(match.Phase == "sungka" && lan != null && lan.State == "play"); i++) { visitor.Tick(1f / 60); vSes2.Tick(1f / 60); yield return 1; }
        Check("accepting starts a game with you as the guest (seat 1) and them as referee", match.Phase == "sungka" && lan.MySeat == 1 && vSes2.MySeat == 0 && lan.State == "play");
        // Leaving mid-game.
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc asks whether to leave the game", lanLeaveAsk);
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 40; i++) { visitor.Tick(1f / 60); vSes2.Tick(1f / 60); yield return 1; }
        Check("leaving forfeits: you're back on the dock and they win", match.Phase == "dock" && vSes2.State == "over" && vSes2.Result == "win" && Leaderboards.Board().entries.Any(e => e.mode == "sungka" && e.result == "forfeit"));
        vSes2.Close();
        // An angler duel while others are about: they see you as away.
        player.X = 266 + 9; player.Y = 198; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3; ClickButton("Derby Duel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "countdown" && mode == "play"); i++) { visitor.Tick(1f / 60); yield return 1; }
        for (int i = 0; i < 90; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("while you fish a duel against an angler the visitor sees you as in a match", visitor.Roster.First(p => p.Id == dockClient.MyId).State == 1);
        Inp.Tap(KeyboardKey.Escape); yield return 2; ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) { visitor.Tick(1f / 60); yield return 1; }
        for (int i = 0; i < 60; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("and back on the dock again afterwards", visitor.Roster.First(p => p.Id == dockClient.MyId).State == 0);
        // A Derby Duel against the visitor.
        Note("Dock: a Derby Duel against another person");
        player.X = DockSpawnX; player.Y = DockSpawnY; visitor.SetPose(DockSpawnX + 12, DockSpawnY, 3, 0);
        visitor.Events.Clear();
        for (int i = 0; i < 240; i++) { visitor.Tick(1f / 60); yield return 1; }
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("the challenge menu now offers Sungka and the Derby Duel", dockChallengeTarget >= 0 && Gfx.Seen.ContainsKey("Derby Duel") && Gfx.Seen.ContainsKey("Sungka"));
        pendingShot = "dock-challenge-menu2"; yield return 2;
        ClickButton("Derby Duel"); yield return 3; Inp.ScriptMouse = Offscreen;
        DockEvent dInv = null; for (int i = 0; i < 120 && dInv == null; i++) { visitor.Tick(1f / 60); dInv = visitor.Events.FirstOrDefault(x => x.Kind == "invite"); yield return 1; }
        Check("the visitor is asked to a Derby Duel", dInv != null && dInv.Mode == 1);
        visitor.Reply(dInv.Ch, true);
        DockEvent dGo = null; for (int i = 0; i < 120 && dGo == null; i++) { visitor.Tick(1f / 60); dGo = visitor.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        var vDerby = new DerbySession("Visitor", dGo.Link, host: false);
        float vClock = 0;
        for (int i = 0; i < 300 && !(match.Phase == "netwait" && mode == "play"); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("accepting fades into the lagoon to wait for each other, you as the referee", match.Phase == "netwait" && netDerby != null && netDerby.Host && match.FromDock && match.RivalName == vp.Name);
        pendingShot = "dock-netwait"; yield return 2;
        for (int i = 0; i < 120 && !(vDerby.StartAsked); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("the match reaches the visitor: seed and match id", vDerby.StartAsked && vDerby.Seed == netDerby.Seed && vDerby.MatchId == netDerby.MatchId);
        vDerby.GuestReady();
        for (int i = 0; i < 120 && !(match.Phase == "countdown" && vDerby.State == "run"); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("both counting down on the same seed", match.Phase == "countdown" && vDerby.State == "run" && match.Spec.Seed == vDerby.Seed && match.Spec.Seed != 0);
        for (int i = 0; i < 300 && match.Phase != "run"; i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("then the three minutes", match.Phase == "run");
        // The visitor lands a fish; it's on your scoreboard and in your world.
        int dSeed = match.Spec.Seed;
        match.T = 10; vClock = 12;
        var vplan = MatchRules.Plan(dSeed, 0);
        vDerby.ReportLanding(0, true, 11);
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        int vCg = MatchRules.Centi(vplan.FishId, vplan.Size, true);
        Check($"their landing is on your scoreboard, worked out from the seed ({vCg / 100.0:0.00} kg)", netDerby.TheirScore.Cg == vCg && match.RivalRun.Count == 1 && match.RivalRun[0].fish == vplan.FishId);
        Check("and their catch is announced over them", floaters.Any(f => f.Text.StartsWith("+") && MathF.Abs(f.X - RivalX) < 3));
        pendingShot = "dock-net-run"; yield return 2;
        // You land one.
        var myPlan = MatchRules.Plan(dSeed, 1);
        fish = new FishCast { Spot = "lagoon", MatchN = 1, Roll = new Catchable { Id = myPlan.FishId, Name = "x" } };
        reel = new ReelState { Roll = fish.Roll, Progress = 1, Perfect = false }; mode = "reeling";
        match.T = 20; vClock = 21; MatchLand();
        Check("your landing is scored and reported", match.Me.Cg == MatchRules.Centi(myPlan.FishId, myPlan.Size, false));
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("and the visitor sees it", vDerby.TheirScore.Cg == match.Me.Cg && vDerby.TheirScore.Catches == 1);
        // The end.
        vClock = 181;
        match.T = match.Spec.Duration - 0.05f; yield return 8;
        Check("the whistle waits for their last word", match.Phase == "waitend");
        vDerby.ReportEnd(new MatchScore { Cg = vCg, Perfects = 1, Catches = 1, Casts = 1 });
        for (int i = 0; i < 90 && mode != "matchresult"; i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("then the result card", mode == "matchresult" && match.Result != null && match.Result.Result is "win" or "loss" or "draw");
        Check("the same winner on both screens", (match.Result.Result, vDerby.Result) is ("win", "loss") or ("loss", "win") or ("draw", "draw"));
        Check("it is on your board once, against their name, and Pip pays nothing for a person", Leaderboards.Board().entries.Count(e => e.mode == "derby" && e.rival == vp.Name) == 1 && match.Result.Prize == "");
        pendingShot = "dock-net-result"; yield return 2;
        // A rematch needs both.
        Check("the card offers a rematch", Gfx.Seen.ContainsKey("Rematch"));
        string firstMatch = netDerby.MatchId;
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("pressing Rematch waits for them", netDerby.WaitingForRematch);
        vDerby.Rematch();
        for (int i = 0; i < 120 && !(match.Phase == "netwait" && mode == "play"); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        for (int i = 0; i < 90 && !(vDerby.State == "wait" && vDerby.MatchId != firstMatch); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("then a new game begins on a new seed", match.Phase == "netwait" && vDerby.State == "wait" && vDerby.Seed == netDerby.Seed && netDerby.Seed != dSeed);
        vDerby.GuestReady();
        vClock = 0;
        for (int i = 0; i < 120 && !(match.Phase == "countdown"); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        for (int i = 0; i < 300 && match.Phase != "run"; i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("running again", match.Phase == "run" && vDerby.State == "run");
        // Leaving mid-game.
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && !(match.Phase == "dock" && mode == "play"); i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); vDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("leaving a game against a person is a forfeit: you are back on the dock and they win", match.Phase == "dock" && vDerby.State == "over" && vDerby.Result == "win" && Leaderboards.Board().entries.Any(e => e.mode == "derby" && e.rival == vp.Name && e.result == "forfeit"));
        Check("the dock is free again for new challenges", dockHost.Pending == 0 && dockHost.Players.All(p => p.State == 0));
        vDerby.Close();

        // A forged result is refused.
        Note("Dock: a forged Derby result");
        for (int i = 0; i < 200; i++) { visitor.Tick(1f / 60); yield return 1; }
        visitor.Events.Clear();
        visitor.Invite(dockClient.MyId, 1);
        for (int i = 0; i < 60 && dockInvite == null; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("the visitor asks you to a Derby Duel", dockInvite != null && dockInvite.Value.mode == 1);
        ClickButton("Accept"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        DockEvent fGo = null; for (int i = 0; i < 120 && fGo == null; i++) { visitor.Tick(1f / 60); fGo = visitor.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        var fDerby = new DerbySession("Forger", fGo.Link, host: true);
        vClock = 0;
        for (int i = 0; i < 300 && !(match.Phase == "netwait" && mode == "play"); i++) { visitor.Tick(1f / 60); fDerby.Tick(1f / 60, vClock); yield return 1; }
        for (int i = 0; i < 60 && fDerby.State != "wait"; i++) { visitor.Tick(1f / 60); fDerby.Tick(1f / 60, vClock); yield return 1; }
        fDerby.HostBegin(MatchRules.Mix(5, 5, 5));
        for (int i = 0; i < 300 && match.Phase != "run"; i++) { visitor.Tick(1f / 60); fDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("as the guest you take their seed and start", match.Phase == "run" && !netDerby.Host && match.Spec.Seed == fDerby.Seed);
        vClock = 100; match.T = 100;
        // They claim a landing at a time that cannot be real.
        fDerby.ReportLanding(9, true, 20);
        for (int i = 0; i < 40; i++) { visitor.Tick(1f / 60); fDerby.Tick(1f / 60, vClock); yield return 1; }
        Check("an impossible landing ends the game with no result", mode == "matchresult" && match.Result.Result == "void" && Leaderboards.Board().entries.All(e => e.rival != "Forger" || e.result == "forfeit"));
        pendingShot = "dock-net-void"; yield return 2;
        ClickButton("Back to the dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("and you are back on the dock", match.Phase == "dock");
        fDerby.Close();
        // Leaving the dock closes it for everyone.
        Inp.Tap(KeyboardKey.Escape); yield return 3; ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) { visitor.Tick(1f / 60); yield return 1; }
        for (int i = 0; i < 30; i++) { visitor.Tick(1f / 60); yield return 1; }
        Check("the host leaving the dock closes it: the visitor is told, and the host's port and clients are gone", !MatchActive && dockHost == null && dockListener == null && dockClient == null && visitor.State == "closed" && visitor.Why.Contains("closed"));
        Check("the host's adventure is as it was", AdventureJson() == hostAdv);

        // --- Joining someone else's dock ---
        Note("Dock: joining a dock");
        using var theDock = new LanHost("Rival dock", "dock", 47870, 47881);
        var theHost = new DockHostCore();
        var (thA, thB) = MemoryLink.Pair(); theHost.AddLink(thA);
        var hostPlayer2 = new DockClientCore(thB, 9, 9, new Look { shirt = 5 });
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Find a dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Find a dock opens the list", dockView == "finding" && dockBrowser != null);
        for (int i = 0; i < 300 && dockBrowser.Hosts.Count(h => h.Game == "dock") == 0; i++) { theDock.Poll(); hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); yield return 1; }
        yield return 3;
        Check("a dock on the network shows up by name", dockBrowser.Hosts.Any(h => h.Name == "Rival dock" && h.Game == "dock") && Gfx.Seen.ContainsKey("Join"));
        pendingShot = "dock-finding"; yield return 2;
        ClickButton("Join"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ILink jl = null; for (int i = 0; i < 300 && jl == null; i++) { jl = theDock.Poll(); yield return 1; }
        theHost.AddLink(jl);
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); yield return 1; }
        Check("Join connects and walks you onto their dock", MatchActive && match.Phase == "dock" && dockClient != null && dockClient.State == "in" && dockHost == null && dockClient.Roster.Count == 2);
        Check("you see their host on it", dockClient.Others.Count() == 1 && dockClient.Others.First().Name == Leaderboards.NameOf(9, 9));
        pendingShot = "dock-joined"; yield return 2;
        // They challenge you; you decline.
        hostPlayer2.Invite(dockClient.MyId, 0);
        for (int i = 0; i < 90 && dockInvite == null; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); yield return 1; }
        Check("their challenge reaches you", dockInvite != null);
        ClickButton("No thanks"); yield return 3; Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 40; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); yield return 1; }
        Check("declining tells them", hostPlayer2.Events.Any(x => x.Kind == "cancel" && x.Why == "declined") && dockInvite == null);
        // Their dock closes while a Derby Duel is on: nobody forfeited, so there is no result either way.
        int entriesBefore = Leaderboards.Board().entries.Count;
        hostPlayer2.Events.Clear();
        for (int i = 0; i < 240; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); yield return 1; }
        hostPlayer2.Invite(dockClient.MyId, 1);
        for (int i = 0; i < 90 && dockInvite == null; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); yield return 1; }
        ClickButton("Accept"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        DockEvent jGo = null; for (int i = 0; i < 120 && jGo == null; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); jGo = hostPlayer2.Events.FirstOrDefault(x => x.Kind == "go"); yield return 1; }
        var jd = new DerbySession("Hosty", jGo.Link, host: true);
        for (int i = 0; i < 300 && !(match.Phase == "netwait" && mode == "play"); i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); jd.Tick(1f / 60, 0); yield return 1; }
        for (int i = 0; i < 90 && jd.State != "wait"; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); jd.Tick(1f / 60, 0); yield return 1; }
        jd.HostBegin(MatchRules.Mix(3, 3, 3));
        for (int i = 0; i < 400 && match.Phase != "run"; i++) { hostPlayer2.Tick(1f / 60); theHost.Tick(1f / 60); jd.Tick(1f / 60, 0); yield return 1; }
        Check("a Derby Duel against someone on their dock is under way", match.Phase == "run" && netDerby != null && !netDerby.Host);
        theHost.Close();
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("their dock closing mid-game ends it with no result for either of you, and you are back on the island", !MatchActive && mode == "play" && Leaderboards.Board().entries.Count == entriesBefore && netDerby == null && dockClient == null);
        Check("and your adventure is as it was", AdventureJson() == hostAdv);
        jd.Close();
        theHost = new DockHostCore();      // (the first one is closed: this stands in for it so the checks below still read)
        // The host goes away.
        theHost.Close();
        for (int i = 0; i < 90 && (MatchActive || mode != "play"); i++) yield return 1;
        for (int i = 0; i < 200 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("when their host closes the dock you are back on the island with a message", !MatchActive && mode == "play" && dockClient == null && ReferenceEquals(state, state) && AdventureJson() == hostAdv);
        theDock.Dispose();
        lanDiscoveryPort = LanHost.DiscoveryPort; lanFirstPort = LanHost.FirstPort; lanLoopbackOnly = false;
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        yield return 3;
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        mode = "play"; yield return 3;
    }
}
#endif
