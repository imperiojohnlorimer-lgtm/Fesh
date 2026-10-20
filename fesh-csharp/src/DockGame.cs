using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The Tournament Dock with other people (1.30): hosting or joining a dock over the LAN, seeing the others walk about,
// quick phrases between you, challenges, and a sungka game played through the dock. The networking is DockNet.cs; this
// is the screens and the game's side of it. Offline, none of it exists and the dock is just you and the three anglers.
partial class Game
{
    DockHostCore dockHost;
    DockClientCore dockClient;
    LanHost dockListener;
    LanBrowser dockBrowser;
    LanConnect dockConnect;
    string dockView = "menu";                          // the Matches panel's dock tab: menu or finding
    string dockMsg = "", dockAddr = "";
    string dockHostAddr = "";                          // this computer's address and port, shown on the dock for the guests to type
    bool dockEntering;                                 // joined a dock, waiting for its welcome before walking in
    (int ch, int from, int mode, double until)? dockInvite;   // a challenge waiting for your answer
    int dockChallengeTarget = -1;                      // the player whose challenge menu is open
    int dockPending;                                   // a challenge you sent that hasn't been answered (-1 until the dock confirms it)
    double dockPendingUntil;
    int dockSungkaCh;

    bool DockNet => dockClient != null && dockClient.State == "in";
    string dockAnglerMenu;                             // the angler whose games are on offer
    bool DockBusy => dockInvite != null || dockPending != 0 || dockChallengeTarget >= 0 || dockAnglerMenu != null;

    (int adj, int noun) MyNameIdx() { var b = Leaderboards.Board(); return (b.adj, b.noun); }

    /* ---------- Hosting and joining (from the Matches panel) ---------- */
    void StartDockHost()
    {
        if (MatchBlockReason() is string why) { dockMsg = why; return; }
        ShutDockNet();
        try { dockListener = new LanHost(LanName, "dock", lanDiscoveryPort, lanFirstPort); }
        catch (Exception) { dockListener = null; dockMsg = "Couldn't open a port to host on. Another program may be using it."; return; }
        dockHostAddr = $"{Lan.LocalAddress()}:{dockListener.Port}";
        dockHost = new DockHostCore();
        var (hl, cl) = MemoryLink.Pair();
        dockHost.AddLink(hl);
        var (adj, noun) = MyNameIdx();
        dockClient = new DockClientCore(cl, adj, noun, state.look);
        dockEntering = true;
        dockMsg = "";
    }

    void StartDockFind()
    {
        ShutDockNet();
        dockBrowser = new LanBrowser(lanDiscoveryPort, lanLoopbackOnly);
        dockView = "finding";
        dockMsg = dockBrowser.Working ? "" : "Couldn't look for docks here. Type an address instead.";
    }

    void JoinDock(string ip, int port)
    {
        dockMsg = "";
        dockConnect?.Cancel();
        dockConnect = new LanConnect(ip, port);
    }

    // The Matches panel is closed (or left for another tab): a search for docks stops. (A dock you are in is left alone.)
    void StopDockFind()
    {
        if (dockClient != null || dockHost != null) return;
        dockBrowser?.Dispose(); dockBrowser = null;
        dockConnect?.Cancel(); dockConnect = null;
        dockView = "menu"; dockMsg = "";
    }

    void ShutDockNet()
    {
        dockClient?.Close("You left the dock.");
        dockHost?.Close();
        dockListener?.Dispose();
        dockBrowser?.Dispose();
        dockConnect?.Cancel();
        dockClient = null; dockHost = null; dockListener = null; dockBrowser = null; dockConnect = null;
        dockInvite = null; dockChallengeTarget = -1; dockPending = 0; dockEntering = false; dockSungkaCh = 0; dockAnglerMenu = null;
    }

    /* ---------- Every frame ---------- */
    void UpdateDockNet(float dt)
    {
        if (dockClient == null && dockBrowser == null && dockConnect == null && dockHost == null) return;
        dockBrowser?.Poll();
        if (dockView == "finding" && dockConnect == null && mode == "panel" && panel == "matches")
        {
            foreach (int c in Inp.Typed())
                if ((c is >= '0' and <= '9' || c is '.' or ':') && dockAddr.Length < 21) dockAddr += (char)c;
            if ((Inp.Pressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace)) && dockAddr.Length > 0) dockAddr = dockAddr[..^1];
            if (Inp.Pressed(KeyboardKey.Enter) && dockAddr.Length > 0 && Lan.TryParse(dockAddr, out var tip, out int tport)) JoinDock(tip, tport);
        }
        if (dockHost != null && dockListener != null)
        {
            var link = dockListener.Poll();
            if (link != null) dockHost.AddLink(link);
            dockListener.Open = dockHost.Count < DockRules.MaxPlayers;
        }
        dockHost?.Tick(dt);
        if (dockConnect != null)
        {
            var link = dockConnect.Poll();
            if (link != null)
            {
                dockConnect = null; dockBrowser?.Dispose(); dockBrowser = null;
                var (adj, noun) = MyNameIdx();
                dockClient = new DockClientCore(link, adj, noun, state.look);
                dockEntering = true;
            }
            else if (dockConnect.Done) { dockMsg = dockConnect.Error ?? "Couldn't connect."; dockConnect = null; }
        }
        dockClient?.Tick(dt);
        if (dockClient != null && dockEntering && dockClient.State == "in")
        {
            dockEntering = false;
            if (MatchBlockReason() is string why) { dockMsg = why; ShutDockNet(); return; }
            if (mode == "panel") ClosePanels();
            BeginDock();
        }
        // The dock closing comes first: whatever was running over it ends with no result (nobody forfeited), before the
        // matches see their links die and read it as the other player leaving.
        if (dockClient != null && dockClient.State == "closed")
        {
            string why = dockClient.Why;
            bool inside = MatchActive && match.Dock;
            if (match != null && match.Phase == "sungka" && lan != null) lanRecorded = lan.RecordId;
            if (netDerby != null) { netDerby.Close(); netDerby = null; dockDerbyCh = 0; }
            ShutDockNet();
            dockMsg = why;
            if (inside) { match.FromDock = false; LeaveMatch(() => Toast(why), record: false); }
            else if (why != "") Toast(why);
            return;
        }
        ProcessDockEvents();
        UpdateNetDerby(dt);
        if (dockPending != 0 && time > dockPendingUntil) dockPending = 0;
        if (dockInvite is { } iv && dockClient != null && dockClient.Now() > iv.until) { dockClient.Reply(iv.ch, false); dockInvite = null; }
        // A sungka game over: back to the dock.
        if (MatchActive && match.Phase == "sungka" && lan == null) ReturnFromDockSungka();
    }

    void ProcessDockEvents()
    {
        if (dockClient == null) return;
        while (dockClient.Events.Count > 0)
        {
            var ev = dockClient.Events.Dequeue();
            switch (ev.Kind)
            {
                case "invite":
                {
                    bool free = InDock && !DockBusy && !match.LeaveAsk && !match.BoardOpen;
                    if (!free) { dockClient.Reply(ev.Ch, false); break; }
                    dockInvite = (ev.Ch, ev.From, ev.Mode, dockClient.Now() + 14);
                    Sfx.Play("coin");
                    break;
                }
                case "invsent": dockPending = ev.Ch; dockPendingUntil = time + DockRules.InviteSecs + 2; Toast($"Challenge sent to {DockName(ev.From)}."); break;
                case "cancel":
                    if (dockInvite is { } iv && iv.ch == ev.Ch) dockInvite = null;
                    if (ev.Ch == dockPending || ev.Ch == 0 || dockPending == -1)
                    {
                        dockPending = 0;
                        Toast(ev.Why switch { "declined" => "They said no.", "expired" => "No answer.", "busy" => "They're busy right now.", "gone" => "They aren't there.", "left" => "They left.", _ => "The challenge ended." });
                    }
                    break;
                case "go": if (ev.Mode == 0) StartDockSungka(ev); else if (ev.Mode == 1) StartDockDerby(ev); else if (ev.Mode == 2) StartDockRegatta(ev); else StartDockQuiz(ev); break;
                case "emote": Sfx.Play("blip"); break;
            }
        }
    }

    string DockName(int id) => dockClient?.Roster.FirstOrDefault(p => p.Id == id)?.Name ?? "someone";

    /* ---------- Challenges ---------- */
    void ChallengePlayer(int id, int modeIdx)
    {
        dockChallengeTarget = -1;
        if (dockClient == null || DockBusy) return;
        dockPending = -1; dockPendingUntil = time + 6;
        dockClient.Invite(id, modeIdx);
    }

    void AnswerInvite(bool accept)
    {
        if (dockInvite is not { } iv) return;
        dockInvite = null;
        dockClient?.Reply(iv.ch, accept);
    }

    // A challenge was accepted: the sungka game runs over the dock, in the dock's own screen.
    void StartDockSungka(DockEvent ev)
    {
        dockPending = 0; dockInvite = null; dockChallengeTarget = -1;
        if (!InDock || ev.Mode != 0 || ev.Link == null) { dockClient?.Done(ev.Ch); return; }
        ShutSungkaLan();
        lan = ev.Role == 0 ? new SungkaSession(LanName) : new SungkaSession(LanName, ev.Link);
        if (ev.Role == 0) lan.AttachGuest(ev.Link);
        lanView = "play"; lanRecorded = ""; lanMsg = ""; lanLeaveAsk = false; lanSeenVersion = lan.Version;
        dockSungkaCh = ev.Ch;
        match.Gen++;
        match.Phase = "sungka";
        SetPrompt("");
    }

    // The game screen is closed (or the game ended and was left): the dock again, and the other player is told it's over.
    void ReturnFromDockSungka()
    {
        if (dockSungkaCh != 0) dockClient?.Done(dockSungkaCh);
        dockSungkaCh = 0;
        if (MatchActive && match.Phase == "sungka") match.Phase = "dock";
    }

    /* ---------- Drawing ---------- */
    void AddDockPlayers(List<(float y, Action draw)> list)
    {
        if (dockClient == null) return;
        foreach (var p in dockClient.Others)
        {
            if (p.State == 1) continue;
            var pl = p;
            list.Add((pl.Y, () =>
            {
                int bob = time % 2.4f > 1.5f ? 1 : 0;
                LookData.DrawPerson(pix, pl.ToLook(), (int)MathF.Round(pl.X), (int)MathF.Round(pl.Y), pl.FaceName, pl.Step, bob: pl.Step == 0 ? bob : 0, idle: false);
            }));
        }
    }

    void DrawDockPlayerTags()
    {
        if (dockClient == null) return;
        double now = dockClient.Now();
        foreach (var p in dockClient.Others)
        {
            if (p.State == 1) continue;
            var sp = ScreenOf(p.X, p.Y - 30);
            string name = p.Name;
            float w = Gfx.Measure(name, FontKind.Ui600, 16) + 16;
            Gfx.Rect(sp.X - w / 2, sp.Y - 12, w, 24, NavyStrong, 5);
            Gfx.TextCenter(name, sp.X, sp.Y - 9, FontKind.Ui600, 16, Pal.Paper);
        }
        // What they said goes over every name.
        foreach (var p in dockClient.Others)
        {
            if (p.State == 1 || now >= p.EmoteUntil || p.Emote == "") continue;
            var sp = ScreenOf(p.X, p.Y - 30);
            float ew = Gfx.Measure(p.Emote, FontKind.Ui600, 18) + 24, a = Math.Clamp((float)(p.EmoteUntil - now) / 0.4f, 0, 1);
            Gfx.Box(sp.X - ew / 2, sp.Y - 56, ew, 34, Pal.WithAlpha(Pal.Paper, a), Pal.WithAlpha(Pal.Ink, a), 2, 7);
            Gfx.TextCenter(p.Emote, sp.X, sp.Y - 51, FontKind.Ui600, 18, Pal.WithAlpha(Pal.PaperInk, a));
        }
    }

    // Who is on the dock, under the title.
    void DrawDockRoster(float x, float y, float w)
    {
        if (dockClient == null) return;
        var all = dockClient.Roster;
        Gfx.Text($"On the dock ({all.Count})", x, y, FontKind.Ui700, 17, Pal.Lantern);
        int row = 0;
        foreach (var p in all.Take(8))
        {
            string line = p.Id == dockClient.MyId ? $"{p.Name} (you)" : p.State == 1 ? $"{p.Name} (in a match)" : p.Name;
            Gfx.Text(Gfx.Ellipsize(line, FontKind.Ui500, 15, w), x, y + 24 + row * 20, FontKind.Ui500, 15, p.State == 1 ? Pal.C("#9fb3c2") : Pal.Paper);
            row++;
        }
    }

    void DrawDockOverlays()
    {
        if (!InDock) return;
        if (dockInvite is { } iv)
        {
            const float w = 560, h = 230;
            float x = (Gfx.LW - w) / 2, y = 150;
            PanelBox(x, y, w, h);
            Gfx.TextCenter($"{DockName(iv.from)} challenges you!", x + w / 2, y + 22, FontKind.Ui700, 30, Pal.PaperInk);
            Gfx.TextCenter($"A game of {DockRules.Modes[iv.mode]}.", x + w / 2, y + 72, FontKind.Ui600, 22, Muted);
            int left = Math.Max(0, (int)Math.Ceiling(iv.until - (dockClient?.Now() ?? 0)));
            Gfx.TextCenter($"{left} s to answer", x + w / 2, y + 104, FontKind.Ui500, 17, Muted);
            float by = y + h - 82;
            if (BigButton("Accept", x + 40, by, true)) { AnswerInvite(true); return; }
            if (BigButton("No thanks", x + w - 40 - BigW("No thanks"), by, false)) { AnswerInvite(false); return; }
        }
        else if (dockChallengeTarget >= 0)
        {
            var p = dockClient?.Roster.FirstOrDefault(r => r.Id == dockChallengeTarget);
            if (p == null) { dockChallengeTarget = -1; return; }
            DrawModeMenu($"Challenge {p.Name}", "Pick a game:", new (string, Action)[]
            {
                ("Sungka", () => ChallengePlayer(p.Id, 0)),
                ("Derby Duel", () => ChallengePlayer(p.Id, 1)),
                ("Regatta", () => ChallengePlayer(p.Id, 2)),
                ("Sea School Duel", () => ChallengePlayer(p.Id, 3)),
            }, () => dockChallengeTarget = -1);
        }
        else if (dockAnglerMenu != null)
        {
            var an = DockAnglers.First(a => a.id == dockAnglerMenu);
            string id = an.id;
            DrawModeMenu($"Challenge {an.name}", "Pick a game:", new (string, Action)[]
            {
                ("Derby Duel", () => { dockAnglerMenu = null; StartDockDuel(id); }),
                ("Regatta", () => { dockAnglerMenu = null; StartDockRegattaAi(id); }),
                ("Sea School Duel", () => { dockAnglerMenu = null; StartDockQuizAi(id); }),
                ("Reef Run (spearfishing)", () => { dockAnglerMenu = null; StartDockReef(id); }),
            }, () => dockAnglerMenu = null);
        }
    }

    // A box of games to pick from, one big button each, and a way out.
    void DrawModeMenu(string title, string sub, (string label, Action act)[] items, Action cancel)
    {
        const float w = 560;
        float h = 140 + items.Length * 70 + 78, x = (Gfx.LW - w) / 2, y = Math.Max(30, (Gfx.LH - h) / 2 - 60);
        PanelBox(x, y, w, h);
        Gfx.TextCenter(title, x + w / 2, y + 22, FontKind.Ui700, 30, Pal.PaperInk);
        Gfx.TextCenter(sub, x + w / 2, y + 74, FontKind.Ui600, 20, Muted);
        float by = y + 116;
        foreach (var (label, act) in items)
        {
            if (BigButton(label, x + (w - BigW(label)) / 2, by, true)) { act(); return; }
            by += 70;
        }
        if (BigButton("Not now", x + (w - BigW("Not now")) / 2, by + 8, false)) { cancel(); return; }
    }
}
