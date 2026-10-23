using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Sungka against another person on the same network (1.30). The rules and the referee are in SungkaCore.cs and
// SungkaSession.cs, the connection in Lan.cs; this is the screen. It plays in a panel like Lola's board, so the
// adventure waits behind it, and it never touches the save: a result goes on the local leaderboard (Match.cs).
partial class Game
{
    SungkaSession lan;
    LanHost lanHost;
    LanBrowser lanBrowser;
    LanConnect lanConnect;
    string lanView = "menu";                   // menu, hosting, finding, play
    string lanAddr = "", lanMsg = "", lanRecorded = "", lanJoining = "";
    bool lanLeaveAsk;
    int lanSeenVersion;
    // Where this game looks for others and where it listens (tests use ports of their own).
    int lanDiscoveryPort = LanHost.DiscoveryPort, lanFirstPort = LanHost.FirstPort;
    bool lanLoopbackOnly;

    string LanName => Leaderboards.Board().name;

    void OpenSungkaLan(string view = "menu")
    {
        if (mode == "panel") ClosePanels();
        if (mode != "play") return;
        Sfx.Play("ui");
        panel = "sungkalan";
        mode = "panel";
        ResetSungkaLan();
        SetPrompt("");
        if (view == "host") StartHosting();
        else if (view == "find") StartFinding();
    }

    void ResetSungkaLan()
    {
        ShutSungkaLan();
        lanView = "menu"; lanMsg = ""; lanLeaveAsk = false; lanRecorded = ""; lanJoining = "";
    }

    // The game screen is done with: the panel closes, or (in a game played through the Tournament Dock) the dock comes back.
    void CloseSungkaView()
    {
        if (MatchActive && match.Phase == "sungka") { ShutSungkaLan(); ReturnFromDockSungka(); return; }
        ClosePanels();
    }

    // Everything let go, whichever way the panel was left (ClosePanels calls this). Leaving a game in progress forfeits it.
    void ShutSungkaLan()
    {
        if (lan != null)
        {
            if (lan.State == "play") lan.Leave();
            RecordLan();
            lan.Close();
            lan = null;
        }
        lanHost?.Dispose(); lanHost = null;
        lanBrowser?.Dispose(); lanBrowser = null;
        lanConnect?.Cancel(); lanConnect = null;
        lanLeaveAsk = false;
    }

    void StartHosting()
    {
        ShutSungkaLan();
        try
        {
            lanHost = new LanHost(LanName, "sungka", lanDiscoveryPort, lanFirstPort);
            lan = new SungkaSession(LanName);
            lanView = "hosting"; lanMsg = "";
        }
        catch (Exception)
        {
            lanHost = null; lan = null;
            lanView = "menu"; lanMsg = "Couldn't open a port to host on. Another program may be using it.";
        }
        lanSeenVersion = lan?.Version ?? 0;
    }

    void StartFinding()
    {
        ShutSungkaLan();
        lanBrowser = new LanBrowser(lanDiscoveryPort, lanLoopbackOnly);
        lanView = "finding"; lanMsg = lanBrowser.Working ? "" : "Couldn't look for games here. Type an address instead.";
    }

    void JoinLan(string ip, int port)
    {
        lanMsg = "";
        lanConnect?.Cancel();
        lanConnect = new LanConnect(ip, port);
        lanJoining = $"{ip}:{port}";
    }

    // Back (Esc): one step at a time, and a game in progress asks first.
    void SungkaLanBack()
    {
        if (lanLeaveAsk) { lanLeaveAsk = false; return; }
        if (lanView == "play" && lan != null && lan.State == "play") { lanLeaveAsk = true; return; }
        if (lanView is "hosting" or "finding") { ResetSungkaLan(); return; }
        CloseSungkaView();
    }

    /* ---------- Every frame ---------- */
    void UpdateSungkaLan(float dt)
    {
        lanBrowser?.Poll();
        if (lanHost != null && lan != null) lanHost.Open = lan.State == "waiting" && lan.Link == null;
        if (lanHost != null && lan != null)
        {
            var link = lanHost.Poll();
            if (link != null) lan.AttachGuest(link);
        }
        if (lanConnect != null)
        {
            var link = lanConnect.Poll();
            if (link != null)
            {
                lanBrowser?.Dispose(); lanBrowser = null;
                lan = new SungkaSession(LanName, link);
                lanSeenVersion = lan.Version;
                lanView = "play"; lanConnect = null; lanMsg = "";
            }
            else if (lanConnect.Done) { lanMsg = lanConnect.Error ?? "Couldn't connect."; lanConnect = null; }
        }
        if (lanView == "finding" && lanConnect == null)
        {
            // Typing an address: digits, dots and a colon only.
            foreach (int c in Inp.Typed())
                if ((c is >= '0' and <= '9' || c is '.' or ':') && lanAddr.Length < 21) lanAddr += (char)c;
            if ((Inp.Pressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace)) && lanAddr.Length > 0) lanAddr = lanAddr[..^1];
            if (Inp.Pressed(KeyboardKey.Enter) && lanAddr.Length > 0) ConnectToTyped();
        }
        if (lan == null) return;
        lan.Tick(dt);
        if (lan.Version != lanSeenVersion)
        {
            lanSeenVersion = lan.Version;
            if (lan.State == "play" && lanView != "play") { lanView = "play"; lanMsg = ""; lanLeaveAsk = false; Sfx.Play("coin"); }
            else if (lan.State == "play") Sfx.Play("blip");
            else if (lan.State == "over") { Sfx.Play(lan.Result == "win" ? "rare" : lan.Result == "loss" ? "fail" : "coin"); lanLeaveAsk = false; }
        }
        if (lan.State == "over") RecordLan();
        if (lan.State == "closed" && lanView == "play" && lan.Notice != "") { lanMsg = lan.Notice; ResetSungkaLan(); }
        else if (lan.State == "closed" && lanView == "play") ResetSungkaLan();
        // A guest who fell through before the game began.
        if (lanView == "hosting" && lan != null && lan.Notice != "") lanMsg = lan.Notice;
    }

    void ConnectToTyped()
    {
        if (!Lan.TryParse(lanAddr, out var ip, out int port)) { lanMsg = "That doesn't look like an address. Try something like 192.168.1.20"; Sfx.Play("nope"); return; }
        JoinLan(ip, port);
    }

    // Each finished game goes on the local board once, under its match id. Leaving one you were playing is a forfeit.
    void RecordLan()
    {
        if (lan == null || lan.State != "over" || lan.RecordId == "" || lan.RecordId == lanRecorded || lan.Board == null) return;
        string result = lan.Result;
        if (result == "") { lanRecorded = lan.RecordId; return; }   // a game stopped as invalid has no result
        if (lan.EndWhy == "left" && result == "loss") result = "forfeit";
        lanRecorded = lan.RecordId;
        Leaderboards.Add(new BoardEntry
        {
            id = lan.RecordId, mode = "sungka", label = $"Sungka · against {lan.RivalName}", seed = 0, ruleset = MatchRules.Version, rival = lan.RivalName,
            result = result, date = DateTime.Now.ToString("o"), score = lan.MyScore, perfects = 0, catches = lan.RivalScore, casts = lan.Board.Revision
        });
    }

    /* ---------- Drawing ---------- */
    static string LanClock(float secs) { int s = Math.Max(0, (int)MathF.Ceiling(secs)); return $"{s / 60}:{s % 60:00}"; }

    string LanResultText()
    {
        var s = lan;
        string score = $"{s.MyScore} to {s.RivalScore}";
        string head = s.Result switch { "win" => "You win!", "loss" => $"{s.RivalName} wins.", "draw" => "A draw!", _ => "The game was stopped." };
        string why = s.EndWhy switch
        {
            "left" => s.Result == "win" ? $"{s.RivalName} left the game." : "You left the game.",
            "timeout" => s.Result == "win" ? $"{s.RivalName} missed three moves in a row." : "Three moves in a row ran out of time.",
            "time" => "Time's up: what's left on each side counts.",
            "invalid" => s.Notice,
            _ => ""
        };
        return s.Result == "" ? $"{head} {why}" : $"{head} {score}. {why}".Trim();
    }

    void DrawSungkaLan()
    {
        Backdrop();
        const float w = 1040, h = 540, pad = 26;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        PanelBox(x, y, w, h);
        Gfx.Text("Sungka over the network", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        bool playing = lanView == "play" && lan != null;
        string closeLabel = playing && lan.State == "play" ? "Leave" : "Close";
        if (SmallButton(closeLabel, x + w - pad - SmallW(closeLabel), y + pad - 4)) { SungkaLanBack(); return; }

        switch (lanView)
        {
            case "menu": DrawLanMenu(x, y, w, h, pad); break;
            case "hosting": DrawLanHosting(x, y, w, h, pad); break;
            case "finding": DrawLanFinding(x, y, w, h, pad); break;
            default: if (playing) DrawLanGame(x, y, w, h, pad); break;
        }
        if (lanLeaveAsk) DrawLanLeave();
    }

    void DrawLanMenu(float x, float y, float w, float h, float pad)
    {
        var lines = Gfx.Wrap("Play sungka against someone on the same Wi-Fi or network. One of you hosts, the other joins. A game is at most five minutes and twenty seconds a move; if you leave, the other player wins. Nothing here changes your adventure.", FontKind.Note, 20, w - pad * 2);
        Lines(lines, x + pad, y + 90, 28, FontKind.Note, 20, Pal.PaperInk);
        float by = y + 90 + lines.Count * 28 + 40;
        if (BigButton("Host a game", x + pad, by, true)) { StartHosting(); return; }
        if (BigButton("Find a game", x + pad + BigW("Host a game") + 18, by, false)) { StartFinding(); return; }
        if (lanMsg != "") Gfx.Text(Gfx.Ellipsize(lanMsg, FontKind.Ui600, 18, w - pad * 2), x + pad, by + 80, FontKind.Ui600, 18, Rust);
        var tip = Gfx.Wrap(LanNetworkTip, FontKind.Note, 18, w - pad * 2);
        Lines(tip, x + pad, y + h - pad - tip.Count * 25, 25, FontKind.Note, 18, Muted);
    }

    void DrawLanHosting(float x, float y, float w, float h, float pad)
    {
        Gfx.Text("Waiting for someone to join...", x + pad, y + 100, FontKind.Ui700, 26, Pal.PaperInk);
        Gfx.Text($"You'll play as {LanName}.", x + pad, y + 146, FontKind.Ui600, 20, Muted);
        string addr = $"{Lan.LocalAddress()}:{lanHost?.Port}";
        var lines = Gfx.Wrap(lanHost != null && lanHost.Discoverable ? "They'll see this game when they choose Find a game. If they can't, tell them this address:" : "Tell them this address:", FontKind.Note, 20, w - pad * 2);
        Lines(lines, x + pad, y + 200, 28, FontKind.Note, 20, Pal.PaperInk);
        Gfx.Text(addr, x + pad, y + 200 + lines.Count * 28 + 8, FontKind.Ui700, 34, Pal.PaperInk);
        if (lanMsg != "") Gfx.Text(Gfx.Ellipsize(lanMsg, FontKind.Ui600, 18, w - pad * 2), x + pad, y + 330, FontKind.Ui600, 18, Rust);
        if (BigButton("Cancel", x + pad, y + h - pad - 56, false)) { ResetSungkaLan(); return; }
    }

    void DrawLanFinding(float x, float y, float w, float h, float pad)
    {
        Gfx.Text("Games on your network", x + pad, y + 92, FontKind.Ui700, 24, Pal.PaperInk);
        float ry = y + 134;
        var hosts = lanBrowser?.Hosts.Where(f => f.Game == "sungka").ToList() ?? new();
        if (hosts.Count == 0) Gfx.Text(lanConnect != null ? "" : "Looking... (the other player chooses Host a game)", x + pad, ry + 6, FontKind.Note, 20, Muted);
        for (int i = 0; i < Math.Min(3, hosts.Count); i++)
        {
            var f = hosts[i];
            float rh = 56;
            Gfx.Box(x + pad, ry, w - pad * 2, rh, CardBg, Pal.C("rgba(16,36,58,0.35)"), 2, 5);
            Gfx.Text(Gfx.Ellipsize(f.Name, FontKind.Ui700, 20, 420), x + pad + 16, ry + 8, FontKind.Ui700, 20, Pal.PaperInk);
            Gfx.Text($"{f.Ip}:{f.Port}", x + pad + 16, ry + 33, FontKind.Ui500, 15, Muted);
            string label = "Join";
            if (Button(label, x + w - pad - 16 - SmallW(label), ry + 6, SmallW(label), 44, FontKind.Ui700, 20, f.Open ? Pal.Buoy : Pal.C("#d8cbb0"), f.Open ? White : Muted, 4, 3, 6, f.Open && lanConnect == null))
            { JoinLan(f.Ip, f.Port); return; }
            ry += rh + 8;
        }
        float ay = y + 134 + 3 * 64 + 12;
        Gfx.Text("Or type their address (they can read it on their screen):", x + pad, ay, FontKind.Ui600, 18, Muted);
        float fx = x + pad, fy = ay + 32, fw = 340, fh = 48;
        Gfx.Box(fx, fy, fw, fh, CardBg, Pal.Ink, 2, 5);
        string shown = lanAddr + ((int)(time * 2) % 2 == 0 && lanConnect == null ? "|" : "");
        Gfx.Text(shown, fx + 14, fy + 9, FontKind.Ui700, 26, Pal.PaperInk);
#if DEBUG
        Gfx.Seen["lanaddr"] = new Rectangle(fx, fy, fw, fh);
#endif
        if (Button("Connect", fx + fw + 14, fy + 2, SmallW("Connect"), 44, FontKind.Ui700, 20, Pal.Buoy, White, 4, 3, 6, lanAddr.Length > 0 && lanConnect == null)) { ConnectToTyped(); return; }
        string status = lanConnect != null ? $"Connecting to {lanJoining}..." : lanMsg;
        if (status != "") Gfx.Text(Gfx.Ellipsize(status, FontKind.Ui600, 18, w - pad * 2), x + pad, fy + 62, FontKind.Ui600, 18, lanConnect != null ? Muted : Rust);
        if (BigButton("Back", x + w - pad - BigW("Back"), y + h - pad - 56, false)) { ResetSungkaLan(); return; }
    }

    void DrawLanGame(float x, float y, float w, float h, float pad)
    {
        var s = lan;
        if (s.Board == null)
        {
            Gfx.Text("Joining the game...", x + pad, y + 100, FontKind.Ui700, 26, Pal.PaperInk);
            Gfx.Text("Waiting for the host to start.", x + pad, y + 146, FontKind.Ui600, 20, Muted);
            return;
        }
        // The board from my side: my houses along the bottom, whoever I am.
        int shift = s.MySeat == 1 ? 8 : 0;
        var view = new int[16];
        for (int v = 0; v < 16; v++) view[v] = s.Board.Pits[(v + shift) % 16];
        int lastView = s.Board.Last < 0 ? -1 : (s.Board.Last - shift + 16) % 16;
        string note;
        if (s.State == "over") note = LanResultText();
        else if (s.Pending) note = "Sending your move...";
        else if (s.MyTurn) note = "Your turn: pick one of your houses along the bottom.";
        else note = $"{s.RivalName} is thinking...";
        if (s.State == "play" && s.Notice != "") note = s.Notice;
        Gfx.Text(Gfx.Ellipsize(note, FontKind.Ui600, 19, w - pad * 2), x + pad, y + pad + 46, FontKind.Ui600, 19, s.State == "over" ? Pal.C("#9a6a1a") : Muted);

        int picked = DrawSungkaBoard(x, y + 10, w, view, lastView, v => s.MyTurn && !lanLeaveAsk && view[v] > 0, s.RivalName, $"{LanName} (you)");
        if (picked >= 0 && s.Move((picked + shift) % 16)) Sfx.Play("blip");

        // The clocks.
        float cy = y + h - pad - 56;
        if (s.State == "play")
        {
            string turn = s.MyTurn ? $"Your move: {LanClock(s.TurnLeft)}" : $"{s.RivalName}'s move: {LanClock(s.TurnLeft)}";
            Gfx.Text(turn, x + pad, cy + 4, FontKind.Ui700, 22, s.TurnLeft < 6 && s.MyTurn ? Rust : Pal.PaperInk);
            Gfx.Text($"Game time left {LanClock(s.MatchLeft)}", x + pad, cy + 32, FontKind.Ui500, 17, Muted);
        }
        else if (s.State == "over")
        {
            float bx = x + pad;
            if (!s.PeerGone)
            {
                string label = s.WaitingForRematch ? $"Waiting for {s.RivalName}..." : "Rematch";
                if (Button(label, bx, cy, BigW(label), 56, FontKind.Ui700, 24, s.WaitingForRematch ? Pal.C("#d8cbb0") : Pal.Buoy, s.WaitingForRematch ? Muted : White, 5, 3, 6, !s.WaitingForRematch)) { s.Rematch(); return; }
                bx += BigW(label) + 14;
            }
            if (BigButton("Leaderboards", bx, cy, false)) { if (MatchActive) { CloseSungkaView(); match.BoardOpen = true; } else { ClosePanels(); OpenMatches("board"); } return; }
        }
    }

    void DrawLanLeave()
    {
        const float w = 520, h = 230;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Rect(0, 0, Gfx.LW, Gfx.LH, Pal.C("rgba(6,14,24,0.45)"));
        Gfx.Block(-5000, -5000, 10000, 10000);
        PanelBox(x, y, w, h);
        Gfx.TextCenter("Leave the game?", x + w / 2, y + 24, FontKind.Ui700, 32, Pal.PaperInk);
        var lines = Gfx.Wrap("If you leave, your opponent wins.", FontKind.Note, 20, w - 60);
        Lines(lines, x + 30, y + 78, 26, FontKind.Note, 20, Pal.PaperInk);
        float by = y + h - 82;
        if (BigButton("Keep playing", x + 30, by, true)) { lanLeaveAsk = false; return; }
        if (BigButton("Leave", x + w - 30 - BigW("Leave"), by, false)) { lanLeaveAsk = false; CloseSungkaView(); return; }
    }
}

partial class Game
{
    // The note on every LAN card: a PC may ask about the firewall once; a phone needs to be on the same Wi-Fi.
    static string LanNetworkTip => Platform.Touch
        ? "Everyone needs to be on the same Wi-Fi (or one phone's hotspot). If Find shows nobody, type the host's address."
        : "The first time, Windows may ask whether Fesh can use the network: choose Private networks.";
}
