using Raylib_cs;

namespace Fesh;

// The Derby Duel against another person (1.30): started from the Tournament Dock by a challenge. Both fish the same
// seeded water on their own computers (the sandbox, Match.cs) and the other side's landings arrive through a DerbySession
// (DerbyNet.cs) over the dock's routed link, so their running total is on your scoreboard and they stand on the bank beside
// you, fighting nothing you can see but holding up each catch. The challenger's seed and start are the ones used; the result
// is worked out by both from what was landed and goes on both local boards.
partial class Game
{
    DerbySession netDerby;
    string netPeerName = "";
    Look netRivalLook;
    int dockDerbyCh;
    double netWaitSince;
    string netGameId = "";                              // the match id the game screen is set up for

    void StartDockDerby(DockEvent ev)
    {
        dockPending = 0; dockInvite = null; dockChallengeTarget = -1;
        if (!InDock || ev.Link == null) { dockClient?.Done(ev.Ch); return; }
        var peer = dockClient.Roster.FirstOrDefault(p => p.Id == ev.From);
        netPeerName = peer?.Name ?? "Someone";
        netRivalLook = peer != null ? RemoteLook(peer) : new Look();
        netDerby = new DerbySession(LanName, ev.Link, host: ev.Role == 0);
        netGameId = "";
        dockDerbyCh = ev.Ch;
        var spec = new MatchSpec { Mode = "derby", Seed = 0, Rival = "net", Label = $"Derby Duel · against {netPeerName}" };
        int g = ++match.Gen;
        match.Phase = "ready";
        FadeThrough(() => { if (match != null && match.Gen == g) EnterDockChild(spec); },
            () => { if (match == null || match.Gen != g) return; match.Phase = "netwait"; netWaitSince = time; SetPrompt(""); });
    }

    // Every frame while a match against a person is on.
    void UpdateNetDerby(float dt)
    {
        var s = netDerby;
        if (s == null || match == null) return;
        s.Tick(dt, match.T);
        switch (match.Phase)
        {
            case "netwait":
                if (s.Host && s.State == "wait") s.HostBegin(NewMatchSeed());
                if (!s.Host && s.StartAsked && !s.Started) { match.Spec.Seed = s.Seed; s.GuestReady(); }
                if (s.State == "run")
                {
                    match.Spec.Seed = s.Seed;
                    netGameId = s.MatchId;
                    rng = new Random(MatchRules.Mix(MatchRules.Version, s.Seed, 9));
                    match.Phase = "countdown"; match.Count = MatchRules.CountdownSecs; Sfx.Play("blip");
                }
                else if (s.State is "over" or "closed") AbortNetDerby(s.Notice != "" ? s.Notice : "The match didn't start.");
                else if (time - netWaitSince > 20) AbortNetDerby("The other player didn't get ready in time.");
                break;
            case "countdown":
            case "run":
                // The other side left or broke the rules while we fish.
                if (s.State == "over") FinishNetMatch();
                break;
            case "waitend":
                if (s.State == "over" || time - netWaitSince > 9) FinishNetMatch();
                break;
            case "done":
                // A rematch both asked for has begun: back to the handshake on the same water.
                // (the other side may already have answered, so the session can be waiting or already running)
                if (s.MatchId != netGameId && s.State is "wait" or "run" && !s.PeerGone) StartNextNetGame();
                break;
        }
    }

    void AbortNetDerby(string why)
    {
        Toast(why);
        match.FromDock = true;
        LeaveMatch();
    }

    // Our three minutes are up: tell them our totals and wait a moment for theirs.
    void BeginNetFinish()
    {
        var m = match;
        m.T = m.Spec.Duration;
        m.Phase = "waitend";
        fish = null; reel = null; pointerHold = false; chargeSpot = null; charge = 0; heldItem = null;
        m.LeaveAsk = false;
        m.Me.Casts = m.Casts;
        netWaitSince = time;
        netDerby.ReportEnd(m.Me);
        SetPrompt("");
    }

    void FinishNetMatch()
    {
        var m = match; var s = netDerby;
        m.T = m.Spec.Duration;
        m.Phase = "done";
        fish = null; reel = null; pointerHold = false; chargeSpot = null; charge = 0; heldItem = null;
        m.LeaveAsk = false;
        m.Me.Casts = m.Casts;
        var o = new MatchOutcome { Me = s.MyFinished ? s.MyFinal : m.Me, RivalName = m.RivalName, Rival = s.TheirScore };
        string r = s.Result;
        if (r == "")
        {
            o.Result = "void"; o.Title = "No result"; o.Reason = s.Notice != "" ? s.Notice : "The game was stopped.";
        }
        else if (s.EndWhy == "left")
        {
            o.Result = r; o.Title = r == "win" ? $"{m.RivalName} left: you win!" : "You left the match"; o.Reason = s.Notice;
        }
        else
        {
            o.Result = r;
            o.Reason = MatchRules.Decide(o.Me, o.Rival).reason;
            o.Title = r == "win" ? "You win!" : r == "loss" ? $"{m.RivalName} wins" : "A draw!";
        }
        m.Spec.Id = s.RecordId.Length > 0 ? s.RecordId : m.Spec.Id;
        m.Result = o;
        RecordMatch(o);
        Sfx.Play(o.Result == "win" ? "rare" : o.Result == "loss" ? "fail" : "coin");
        mode = "matchresult";
        SetPrompt("");
    }

    // Rematch: only when the other side has asked too.
    void NetRematch()
    {
        netDerby?.Rematch();
    }

    void StartNextNetGame()
    {
        var s = netDerby;
        var spec = new MatchSpec { Mode = "derby", Seed = s.Seed, Rival = "net", Label = $"Derby Duel · against {netPeerName}" };
        ResetMatchCounters(spec);
        ResetMatchPlace();
        mode = "play";
        match.Phase = "netwait"; netWaitSince = time;
    }

    // Leaving a match against a person (or the match being over): the other side is told, the dock is told.
    void ShutNetDerby()
    {
        netDerby?.Leave();
        netDerby = null;
        if (dockDerbyCh != 0) dockClient?.Done(dockDerbyCh);
        dockDerbyCh = 0;
    }
}
