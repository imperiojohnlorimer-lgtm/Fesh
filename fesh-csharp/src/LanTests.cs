#if DEBUG
using System.Net.Sockets;
using Raylib_cs;

namespace Fesh;

partial class Game
{
    static bool Rm(SungkaSession s) { s.Rematch(); return true; }
    static NetMsg M(string t, Action<NetMsg> f = null) { var m = new NetMsg { t = t }; f?.Invoke(m); return m; }

    // Two sessions joined by the in-memory link, with the guest's hello already delivered.
    static (SungkaSession host, SungkaSession guest, MemoryLink hl, MemoryLink gl) LanPair(string hostName = "Host", string guestName = "Guest")
    {
        var (a, b) = MemoryLink.Pair();
        var host = new SungkaSession(hostName);
        host.AttachGuest(a);
        var guest = new SungkaSession(guestName, b);
        host.Tick(0.01f); guest.Tick(0.01f);
        return (host, guest, a, b);
    }

    // Plays random legal moves for whoever's turn it is until the game ends (or 400 moves).
    static void PlayOut(SungkaSession host, SungkaSession guest, Random r, int maxMoves = 400)
    {
        for (int i = 0; i < maxMoves && host.State == "play"; i++)
        {
            var mover = host.Board.Turn == 0 ? host : guest;
            var legal = Enumerable.Range(0, 16).Where(p => mover.Board.CanMove(mover.MySeat, p)).ToList();
            if (legal.Count == 0) break;
            mover.Move(legal[r.Next(legal.Count)]);
            for (int k = 0; k < 3; k++) { host.Tick(0.001f); guest.Tick(0.001f); }
        }
    }

    // LAN sungka (1.30): the rules, the wire, the referee and both sides' views of a game, over memory and over real
    // loopback sockets, then the screens by real clicks. FESH_LAN_TEST=1 runs only this.
    IEnumerable<int> LanScript()
    {
        Note("LAN Sungka: the rules");
        var start = SungkaRules.Start();
        Check("98 shells at the start: seven in each of fourteen houses, heads empty", start.Sum() == 98 && start[7] == 0 && start[15] == 0 && Enumerable.Range(0, 16).Where(i => i is not (7 or 15)).All(i => start[i] == 7));
        Check("a seat owns only its own houses", Enumerable.Range(0, 7).All(p => SungkaRules.Owns(0, p) && !SungkaRules.Owns(1, p)) && Enumerable.Range(8, 7).All(p => SungkaRules.Owns(1, p) && !SungkaRules.Owns(0, p)) && !SungkaRules.Owns(0, 7) && !SungkaRules.Owns(1, 15));
        var m0 = new SungkaMatch(0);
        Check("sowing seven from the first house ends in your own head and you go again", m0.Apply(0, 0) && m0.Turn == 0 && m0.Pits[7] == 1 && m0.Revision == 1 && m0.Last == 7);
        Check("a move from the other side, an empty house, a head or nothing at all is refused", !m0.Apply(1, 8) && !m0.Apply(0, 0) && !m0.Apply(0, 7) && !m0.Apply(0, 15) && !m0.Apply(0, -1) && !m0.Apply(0, 99) && !m0.Apply(2, 1) && m0.Revision == 1);
        // A capture: seat 0's last shell in an empty house of their own takes what's opposite, plus itself.
        var cap = new SungkaMatch(0); Array.Clear(cap.Pits);
        cap.Pits[0] = 1; cap.Pits[13] = 4; int before = cap.Pits.Sum();
        cap.Apply(0, 0);
        Check("the last shell in an empty house of your own captures the house across, with itself, into your head", cap.Pits[7] == 5 && cap.Pits[1] == 0 && cap.Pits[13] == 0 && cap.Pits.Sum() == before);
        // Lola's side too (seat 1 goes the same way round to its own head).
        var cap1 = new SungkaMatch(1); Array.Clear(cap1.Pits);
        cap1.Pits[8] = 1; cap1.Pits[6] = 3; cap1.Pits[12] = 2; cap1.Pits[9] = 0; cap1.Pits[5] = 3;
        cap1.Apply(1, 8);
        Check("seat 1 captures into the head at 15", cap1.Pits[15] == 4 && cap1.Pits[9] == 0 && cap1.Pits[5] == 0);
        // Fuzz: no game, however it goes, ever loses or makes a shell, and every one ends.
        var rnd = new Random(2026);
        int games = 0, longest = 0; bool conserved = true, ended = true, wins = true;
        for (int g = 0; g < 4000; g++)
        {
            var b = new SungkaMatch(g % 2); int n = 0;
            while (!b.Over && n < 1000)
            {
                int seat = b.Turn;
                var legal = Enumerable.Range(0, 16).Where(p => b.CanMove(seat, p)).ToList();
                if (legal.Count == 0) { conserved = false; break; }
                b.Apply(seat, legal[rnd.Next(legal.Count)]);
                conserved &= b.Pits.Sum() == 98 && b.Pits.All(x => x >= 0);
                n++;
            }
            ended &= b.Over;
            wins &= b.Score(0) + b.Score(1) == 98 && b.Pits.Where((_, i) => i is not (7 or 15)).All(x => x == 0);
            longest = Math.Max(longest, n); games++;
        }
        Check($"4000 random games: all 98 shells always on the board, every game ends (longest {longest} moves) and the two heads hold all 98", conserved && ended && wins);
        // The same board both sides would compute.
        var d1 = new SungkaMatch(0); var d2 = new SungkaMatch(0);
        d1.Apply(0, 2); d2.Apply(0, 2);
        Check("the board fingerprint is the same for the same moves and differs for different ones", d1.Digest() == d2.Digest() && d1.Digest() != new SungkaMatch(0).Digest());
        d2.Apply(d2.Turn, d2.Turn == 0 ? 1 : 8);
        Check("and it changes with every move", d1.Digest() != d2.Digest());

        // --- The wire ---
        Note("LAN Sungka: the wire");
        var f1 = Wire.Frame(M("move", m => { m.id = "abc"; m.pit = 3; m.rev = 9; }));
        var f2 = Wire.Frame(M("hb"));
        var buf = new byte[Wire.MaxFrame * 2 + 8]; int have = 0;
        Buffer.BlockCopy(f1, 0, buf, 0, f1.Length); have = f1.Length;
        Buffer.BlockCopy(f2, 0, buf, have, f2.Length); have += f2.Length;
        var got = new List<NetMsg>();
        Check("two frames in one read come out as two messages, in order, with their fields", Wire.Parse(buf, ref have, got) && have == 0 && got.Count == 2 && got[0].t == "move" && got[0].pit == 3 && got[0].rev == 9 && got[0].id == "abc" && got[1].t == "hb");
        have = 0; got.Clear();
        int half = f1.Length / 2;
        Buffer.BlockCopy(f1, 0, buf, 0, half); have = half;
        Check("half a frame waits for the rest", Wire.Parse(buf, ref have, got) && got.Count == 0 && have == half);
        Buffer.BlockCopy(f1, half, buf, have, f1.Length - half); have += f1.Length - half;
        Check("and completes when it arrives", Wire.Parse(buf, ref have, got) && got.Count == 1 && have == 0);
        byte[] Bad(int len, string body) { var b = new byte[4 + body.Length]; b[0] = (byte)len; b[1] = (byte)(len >> 8); b[2] = (byte)(len >> 16); b[3] = (byte)(len >> 24); System.Text.Encoding.ASCII.GetBytes(body).CopyTo(b, 4); return b; }
        bool Rejects(byte[] data) { var bb = new byte[Wire.MaxFrame * 2 + 8]; Buffer.BlockCopy(data, 0, bb, 0, data.Length); int h = data.Length; return !Wire.Parse(bb, ref h, new List<NetMsg>()); }
        Check("a frame longer than the limit, a zero or negative length, text that isn't JSON, JSON with no type and a too-long type are all refused", Rejects(Bad(Wire.MaxFrame + 1, "x")) && Rejects(Bad(0, "")) && Rejects(Bad(-5, "xx")) && Rejects(Bad(5, "hello")) && Rejects(Bad(2, "{}")) && Rejects(Bad(36, "{\"t\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")));
        Check("no message of any kind comes near the size limit", Wire.Frame(M("start", m => { m.id = new string('x', 32); m.name = new string('n', 24); })).Length < 200);
        Check("names from the other side are cut to short plain text", SungkaSession.Clean("Bob\u0007\u001b[31m") == "Bob[31m" && SungkaSession.Clean(new string('a', 100)).Length == 24 && SungkaSession.Clean("") == "Player" && SungkaSession.Clean("\u0001\u0002") == "Player" && SungkaSession.Clean(null) == "Player");
        Check("an address is read from 1.2.3.4 or 1.2.3.4:5000 and nothing else", Lan.TryParse("192.168.1.20", out var ip1, out int pt1) && ip1 == "192.168.1.20" && pt1 == LanHost.FirstPort && Lan.TryParse(" 10.0.0.2:5000 ", out var ip2, out int pt2) && pt2 == 5000
            && !Lan.TryParse("", out _, out _) && !Lan.TryParse("abc", out _, out _) && !Lan.TryParse("1.2.3", out _, out _) && !Lan.TryParse("1.2.3.4:0", out _, out _) && !Lan.TryParse("1.2.3.4:99999", out _, out _) && !Lan.TryParse("1.2.3.4:x", out _, out _) && !Lan.TryParse("999.1.1.1", out _, out _) && !Lan.TryParse("::1", out _, out _));
        yield return 1;

        // --- The referee over an in-memory link ---
        Note("LAN Sungka: a game between two sessions");
        var (host, guest, hl, gl) = LanPair("Alma", "Beng");
        Check("hello starts the game on both sides: names swapped, seats 0 and 1, one match id", host.State == "play" && guest.State == "play" && host.RivalName == "Beng" && guest.RivalName == "Alma" && host.MySeat == 0 && guest.MySeat == 1 && host.MatchId == guest.MatchId && host.MatchId.Length == 32);
        Check("both start on the same board with the same first seat", host.Board.FirstSeat == guest.Board.FirstSeat && host.Board.Digest() == guest.Board.Digest() && host.Board.Turn == host.Board.FirstSeat);
        var firstMover = host.Board.Turn == 0 ? host : guest; var other = firstMover == host ? guest : host;
        Check("the player whose turn it isn't can't move", !other.Move(other.MySeat == 0 ? 0 : 8) && !other.MyTurn && firstMover.MyTurn);
        int pit = firstMover.MySeat == 0 ? 2 : 10;
        Check("the one whose turn it is can", firstMover.Move(pit));
        for (int i = 0; i < 3; i++) { host.Tick(0.001f); guest.Tick(0.001f); }
        Check("the move reaches both boards exactly once, and they agree", host.Board.Revision == 1 && guest.Board.Revision == 1 && host.Board.Digest() == guest.Board.Digest() && host.Board.Pits.Sum() == 98);
        if (!guest.MyTurn && !host.MyTurn) { }
        // A guest's move waits for the host's word: it isn't on the guest's own board until committed.
        var (h2, g2, hl2, gl2) = LanPair();
        while (h2.Board.Turn != 1) { h2.Move(h2.Board.Turn == 0 ? new[] { 0, 1, 2, 3, 4, 5, 6 }.First(p => h2.Board.CanMove(0, p)) : 8); h2.Tick(0.001f); g2.Tick(0.001f); }
        int gRev = g2.Board.Revision;
        Check("a guest's own move waits for the host's word before it shows", g2.MyTurn && g2.Move(g2.Board.Pits[8] > 0 ? 8 : 9) && g2.Pending && g2.Board.Revision == gRev && !g2.MyTurn);
        Check("while one is on its way, a second can't be sent", !g2.Move(10) && gl2.Sent.Count(m => m.t == "move") == 1);
        h2.Tick(0.001f); g2.Tick(0.001f);
        Check("the host plays it and the commit arrives: boards agree and the wait is over", g2.Board.Revision == gRev + 1 && !g2.Pending && g2.Board.Digest() == h2.Board.Digest());
        // Things a rude or broken guest might send.
        var (h3, g3, hl3, gl3) = LanPair();
        while (h3.Board.Turn != 1) { h3.Move(new[] { 0, 1, 2, 3, 4, 5, 6 }.First(p => h3.Board.CanMove(0, p))); h3.Tick(0.001f); g3.Tick(0.001f); }
        int rev3 = h3.Board.Revision; int dig3 = h3.Board.Digest();
        void Rude(NetMsg m) { gl3.Send(m); h3.Tick(0.001f); g3.Tick(0.001f); }
        Rude(M("move", m => { m.id = h3.MatchId; m.rev = rev3 - 1; m.pit = 8; }));
        Check("a move for an old position is turned down and changes nothing", h3.Board.Revision == rev3 && h3.Board.Digest() == dig3 && hl3.Sent.Last().t == "reject" && hl3.Sent.Last().why == "stale");
        Rude(M("move", m => { m.id = h3.MatchId; m.rev = rev3; m.pit = 3; }));
        Check("a move from the host's houses is turned down", h3.Board.Digest() == dig3 && hl3.Sent.Last().why == "illegal");
        Rude(M("move", m => { m.id = h3.MatchId; m.rev = rev3; m.pit = 7; }));
        Rude(M("move", m => { m.id = h3.MatchId; m.rev = rev3; m.pit = 15; }));
        Rude(M("move", m => { m.id = h3.MatchId; m.rev = rev3; m.pit = -4; }));
        Rude(M("move", m => { m.id = h3.MatchId; m.rev = rev3; m.pit = 400; }));
        Check("heads and out-of-range houses too", h3.Board.Digest() == dig3 && h3.State == "play");
        Rude(M("move", m => { m.id = "someone else's match"; m.rev = rev3; m.pit = 8; }));
        Check("a move for another match is turned down", h3.Board.Digest() == dig3 && hl3.Sent.Last().why == "stale");
        // Out of turn: make it the host's turn, then have the guest ask.
        var (h4, g4, hl4, gl4) = LanPair();
        while (h4.Board.Turn != 0) { var gm = g4.Board.Pits.Select((c, i) => (c, i)).First(t => t.i is >= 8 and <= 14 && t.c > 0).i; g4.Move(gm); h4.Tick(0.001f); g4.Tick(0.001f); }
        int rev4 = h4.Board.Revision, dig4 = h4.Board.Digest();
        gl4.Send(M("move", m => { m.id = h4.MatchId; m.rev = rev4; m.pit = 9; })); h4.Tick(0.001f);
        Check("the guest moving on the host's turn is turned down", h4.Board.Digest() == dig4 && hl4.Sent.Last().why == "illegal");
        // What a bad host might send the guest.
        var (h5, g5, hl5, gl5) = LanPair();
        while (h5.Board.Turn != 0) { var gm = g5.Board.Pits.Select((c, i) => (c, i)).First(t => t.i is >= 8 and <= 14 && t.c > 0).i; g5.Move(gm); h5.Tick(0.001f); g5.Tick(0.001f); }
        h5.Move(h5.Board.Pits.Select((c, i) => (c, i)).First(t => t.i is >= 0 and <= 6 && t.c > 0).i); h5.Tick(0.001f); g5.Tick(0.001f);
        var lastCommit = hl5.Sent.Last(m => m.t == "commit");
        hl5.Inject(M("hb")); // nothing
        gl5.Inject(M("commit", m => { m.id = lastCommit.id; m.rev = lastCommit.rev; m.seat = lastCommit.seat; m.pit = lastCommit.pit; m.dig = lastCommit.dig; }));
        g5.Tick(0.001f);
        Check("a move committed twice (the same move number again) stops the game as invalid", g5.State == "over" && g5.EndWhy == "invalid" && g5.Result == "");
        var (h6, g6, hl6, gl6) = LanPair();
        while (h6.Board.Turn != 0) { var gp = Enumerable.Range(8, 7).First(p => g6.Board.CanMove(1, p)); g6.Move(gp); h6.Tick(0.001f); g6.Tick(0.001f); }
        gl6.Inject(M("commit", m => { m.id = h6.MatchId; m.rev = g6.Board.Revision + 1; m.seat = 0; m.pit = Enumerable.Range(0, 7).First(p => h6.Board.CanMove(0, p)); m.dig = 12345; }));
        g6.Tick(0.001f);
        Check("a commit whose board fingerprint disagrees stops the game as invalid, with no winner", g6.State == "over" && g6.EndWhy == "invalid" && g6.Result == "" && g6.Notice.Contains("differ"));
        var (h7, g7, hl7, gl7) = LanPair();
        gl7.Inject(M("commit", m => { m.id = h7.MatchId; m.rev = 1; m.seat = 0 == h7.Board.Turn ? 1 : 0; m.pit = 3; m.dig = 1; }));
        g7.Tick(0.001f);
        Check("a commit played for the seat whose turn it isn't stops the game", g7.State == "over" && g7.EndWhy == "invalid");
        // The version and a full table.
        var (a1, b1) = MemoryLink.Pair();
        var hostV = new SungkaSession("H"); hostV.AttachGuest(a1);
        b1.Send(M("hello", m => { m.v = 99; m.name = "Future"; }));
        hostV.Tick(0.01f);
        Check("a guest speaking another protocol version is sent away and the host keeps waiting", hostV.State == "waiting" && hostV.Link == null && a1.Sent.Any(m => m.t == "bye" && m.why == "version"));
        var (a2, b2) = MemoryLink.Pair();
        Check("a second guest while one is playing is told the game is full", !host.AttachGuest(a2) && a2.Sent.Any(m => m.why == "full") && host.State == "play");
        var (a3, b3) = MemoryLink.Pair();
        var hostS = new SungkaSession("H"); hostS.AttachGuest(a3); hostS.Tick(SungkaSession.SilentLimit + 0.5f);
        Check("someone who connects and never says hello is dropped after a few seconds", hostS.Link == null && hostS.State == "waiting");

        // --- Clocks ---
        Note("LAN Sungka: the clocks and leaving");
        var (ht, gt, hlt, glt) = LanPair("Alma", "Beng");
        int seatToMove = ht.Board.Turn; int rev0 = ht.Board.Revision;
        gt.Tick(SungkaSession.TurnSecs - 0.5f); ht.Tick(SungkaSession.TurnSecs + 0.1f); gt.Tick(0.001f);
        Check("a player who runs out of time has a move played for them (the leftmost house) and is told", ht.Board.Revision == rev0 + 1 && gt.Board.Revision == rev0 + 1 && ht.Board.Digest() == gt.Board.Digest() && ht.Notice.Length > 0);
        // Three timeouts for the same seat in a row is a forfeit; a real move in between resets it.
        var (hf, gf, hlf, glf) = LanPair("Alma", "Beng");
        int slow = hf.Board.Turn;
        string endWhy = ""; int guard = 0;
        // Let the same seat time out every turn: when it comes round to them again, expire again.
        while (hf.State == "play" && guard++ < 40)
        {
            if (hf.Board.Turn == slow) { gf.Tick(SungkaSession.TurnSecs - 0.5f); hf.Tick(SungkaSession.TurnSecs + 0.1f); }
            else { var mover = hf.Board.Turn == 0 ? hf : gf; var legal = Enumerable.Range(0, 16).Where(p => mover.Board.CanMove(mover.MySeat, p)).ToList(); mover.Move(legal[0]); hf.Tick(0.001f); gf.Tick(0.001f); }
            gf.Tick(0.001f);
        }
        endWhy = hf.EndWhy;
        Check("three timeouts in a row lose the game (the guest sees it too)", hf.State == "over" && endWhy == "timeout" && hf.Winner == 1 - slow && gf.State == "over" && gf.Winner == 1 - slow);
        var (hm2, gm3, hlm2, glm2) = LanPair("Alma", "Beng");
        PlayOut(hm2, gm3, new Random(9), 6);
        hm2.MatchLeft = 0.01f; hm2.TurnLeft = 99; gm3.MatchLeft = 0.01f; hm2.Tick(0.05f); gm3.Tick(0.001f);
        Check("at the whistle both boards are settled the same way and agree who won", hm2.State == "over" && hm2.EndWhy == "time" && gm3.State == "over" && gm3.EndWhy == "time" && hm2.Board.Total == 98 && hm2.Board.Digest() == gm3.Board.Digest() && hm2.Result == (gm3.Result == "win" ? "loss" : gm3.Result == "loss" ? "win" : "draw"));
        // Silence and leaving.
        var (hs, gs, hls, gls) = LanPair("Alma", "Beng");
        gls.Quiet = SungkaSession.SilentLimit + 1; gs.Tick(0.01f);
        Check("a host who goes quiet loses the game for the guest to win", gs.State == "over" && gs.EndWhy == "left" && gs.Result == "win" && gs.PeerGone);
        var (hs2, gs2, hls2, gls2) = LanPair("Alma", "Beng");
        gs2.Leave(); hs2.Tick(0.01f);
        Check("leaving mid-game is a forfeit: the one who left lost, the other won", gs2.State == "over" && gs2.Result == "loss" && hs2.State == "over" && hs2.Result == "win" && hs2.EndWhy == "left" && hs2.Notice.Contains("left"));
        var (hs3, gs3, hls3, gls3) = LanPair("Alma", "Beng");
        hls3.Close(); hs3.Tick(0.01f);
        Check("a dropped connection counts as leaving", hs3.State == "over" && hs3.Result == "win" && hs3.PeerGone);
        Check("nothing more can be played after it, and no rematch", !hs3.Move(0) && Rm(hs3) && !hs3.WaitingForRematch);

        // --- Rematch ---
        var (hr, gr, hlr, glr) = LanPair("Alma", "Beng");
        PlayOut(hr, gr, new Random(5));
        Check("a game played out ends the same on both sides, one winner or a draw, all 98 shells in the heads", hr.State == "over" && gr.State == "over" && hr.EndWhy == "finished" && hr.Board.Digest() == gr.Board.Digest() && hr.Board.Score(0) + hr.Board.Score(1) == 98 && (hr.Result, gr.Result) is ("win", "loss") or ("loss", "win") or ("draw", "draw"));
        string oldId = hr.MatchId; int oldFirst = hr.Board.FirstSeat;
        gr.Rematch(); hr.Tick(0.001f); gr.Tick(0.001f);
        Check("a rematch waits for both to be ready", gr.WaitingForRematch && hr.State == "over");
        hr.Rematch(); hr.Tick(0.001f); gr.Tick(0.001f);
        Check("then a new game starts on both, with a new id and the other player first, and seats unchanged", hr.State == "play" && gr.State == "play" && hr.MatchId != oldId && hr.MatchId == gr.MatchId && hr.Board.FirstSeat == 1 - oldFirst && gr.Board.FirstSeat == 1 - oldFirst && hr.MySeat == 0 && gr.MySeat == 1 && hr.Board.Total == 98 && hr.Board.Revision == 0 && hr.Games == 2 && gr.Games == 2);
        // Both ask at once.
        PlayOut(hr, gr, new Random(6));
        hr.Rematch(); gr.Rematch(); hr.Tick(0.001f); gr.Tick(0.001f); hr.Tick(0.001f); gr.Tick(0.001f);
        Check("two rematch requests at once make one new game, not two", hr.State == "play" && gr.State == "play" && hr.MatchId == gr.MatchId && hr.Games == 3);
        yield return 1;

        // --- Real sockets on the loopback address ---
        Note("LAN Sungka: over real sockets");
        const int udpPort = 47860, tcpPort = 47861;
        using var loopHost = new LanHost("Alma on the loopback", "sungka", udpPort, tcpPort);
        Check("a host listens on a port and can be found", loopHost.Port >= tcpPort && loopHost.Port < tcpPort + 12 && loopHost.Discoverable);
        using var browser = new LanBrowser(udpPort, loopbackOnly: true);
        for (int i = 0; i < 200 && browser.Hosts.Count == 0; i++) { browser.Poll(); loopHost.Poll(); yield return 1; }
        Check("the browser hears the host: name, address, port, game", browser.Hosts.Count == 1 && browser.Hosts[0].Name == "Alma on the loopback" && browser.Hosts[0].Ip == "127.0.0.1" && browser.Hosts[0].Port == loopHost.Port && browser.Hosts[0].Game == "sungka");
        // A host heard through several adapters is one entry, not three.
        using var mergeBrowser = new LanBrowser(47868, loopbackOnly: true);
        byte[] Reply(string id, int port) => System.Text.Encoding.UTF8.GetBytes($"{{\"fesh\":\"lan\",\"v\":{SungkaSession.Protocol},\"id\":\"{id}\",\"name\":\"Swift Tuna\",\"port\":{port},\"game\":\"sungka\",\"open\":true}}");
        mergeBrowser.Ingest(Reply("abc123", 47831), System.Net.IPAddress.Parse("127.0.0.1"), 1);
        mergeBrowser.Ingest(Reply("abc123", 47831), System.Net.IPAddress.Parse("192.168.56.1"), 1);
        mergeBrowser.Ingest(Reply("abc123", 47831), System.Net.IPAddress.Parse("10.0.4.2"), 1);
        mergeBrowser.Ingest(Reply("other", 47832), System.Net.IPAddress.Parse("10.0.4.9"), 1);
        mergeBrowser.Ingest(System.Text.Encoding.UTF8.GetBytes("not json"), System.Net.IPAddress.Parse("10.0.4.9"), 1);
        Check($"one host answering from three addresses is one entry, with a real address rather than the loopback ({string.Join(", ", mergeBrowser.Hosts.Select(h => h.Id + "@" + h.Ip))})", mergeBrowser.Hosts.Count == 2 && mergeBrowser.Hosts.First(h => h.Id == "abc123").Ip == "192.168.56.1");
        Check("this computer's own address for others is a real-looking IPv4 address", System.Net.IPAddress.TryParse(Lan.LocalAddress(), out var localAddr) && localAddr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        var hostSes = new SungkaSession("Alma");
        // Someone who is not Fesh connects and sends rubbish: dropped, the host is unharmed.
        var junk = new TcpClient(); junk.Connect("127.0.0.1", loopHost.Port);
        ILink junkLink = null; for (int i = 0; i < 100 && junkLink == null; i++) { junkLink = loopHost.Poll(); yield return 1; }
        hostSes.AttachGuest(junkLink);
        junk.GetStream().Write(new byte[] { 0xff, 0xff, 0xff, 0x7f, 1, 2, 3 }, 0, 7);
        for (int i = 0; i < 30; i++) { hostSes.Tick(1f / 60); yield return 1; }
        Check("a stranger sending rubbish is dropped and the host goes on waiting", hostSes.State == "waiting" && !junkLink.Alive);
        junk.Close();
        hostSes.Close();
        hostSes = new SungkaSession("Alma");
        var connect = new LanConnect("127.0.0.1", loopHost.Port);
        ILink hostSide = null, guestSide = null;
        for (int i = 0; i < 300 && (hostSide == null || guestSide == null); i++) { hostSide ??= loopHost.Poll(); guestSide ??= connect.Poll(); yield return 1; }
        Check("a guest connects by address", hostSide != null && guestSide != null && connect.Error == null);
        hostSes.AttachGuest(hostSide);
        var guestSes = new SungkaSession("Beng", guestSide);
        for (int i = 0; i < 120 && !(hostSes.State == "play" && guestSes.State == "play"); i++) { hostSes.Tick(1f / 60); guestSes.Tick(1f / 60); yield return 1; }
        Check("hello and start cross the real connection", hostSes.State == "play" && guestSes.State == "play" && guestSes.RivalName == "Alma" && hostSes.RivalName == "Beng" && hostSes.MatchId == guestSes.MatchId);
        var rr = new Random(77);
        for (int n = 0; n < 500 && hostSes.State == "play"; n++)
        {
            var mover = hostSes.Board.Turn == 0 ? hostSes : guestSes;
            if (mover.MyTurn)
            {
                var legal = Enumerable.Range(0, 16).Where(p => mover.Board.CanMove(mover.MySeat, p)).ToList();
                mover.Move(legal[rr.Next(legal.Count)]);
            }
            for (int k = 0; k < 4; k++) { hostSes.Tick(1f / 60); guestSes.Tick(1f / 60); yield return 1; }
        }
        for (int i = 0; i < 30; i++) { hostSes.Tick(1f / 60); guestSes.Tick(1f / 60); yield return 1; }
        Check($"a whole game over real sockets: both end, with the same board ({hostSes.MyScore} to {hostSes.RivalScore})", hostSes.State == "over" && guestSes.State == "over" && hostSes.Board.Digest() == guestSes.Board.Digest() && hostSes.MyScore + hostSes.RivalScore == 98);
        // Leaving closes the socket and the other side notices at once.
        hostSes.Rematch(); guestSes.Rematch();
        for (int i = 0; i < 60; i++) { hostSes.Tick(1f / 60); guestSes.Tick(1f / 60); yield return 1; }
        Check("a rematch crosses the connection too", hostSes.State == "play" && guestSes.State == "play" && hostSes.Games == 2);
        guestSes.Leave();
        for (int i = 0; i < 30 && hostSes.State == "play"; i++) { hostSes.Tick(1f / 60); yield return 1; }
        Check("closing the guest's side ends the host's game as a win by forfeit", hostSes.State == "over" && hostSes.Result == "win" && hostSes.PeerGone);
        hostSes.Close(); guestSes.Close();
        // Connecting to nothing gives up with a message.
        var nothing = new LanConnect("127.0.0.1", 47899);
        for (int i = 0; i < 400 && !nothing.Done; i++) { nothing.Poll(); yield return 1; }
        Check($"connecting to an address nobody answers on says so ({nothing.Error})", nothing.Done && !string.IsNullOrEmpty(nothing.Error));
        loopHost.Dispose();

        // --- Codex's review: hostile and odd messages ---
        Note("LAN Sungka: review findings");
        // A move before any hello used to dereference a board that doesn't exist yet.
        var (preA, preB) = MemoryLink.Pair();
        var preHost = new SungkaSession("H"); preHost.AttachGuest(preA);
        preB.Send(M("move", m => { m.id = "x"; m.pit = 8; m.rev = 0; }));
        bool preThrew = false; try { preHost.Tick(0.01f); } catch (Exception) { preThrew = true; }
        Check("a move sent before hello is turned down without a crash, and the host keeps waiting", !preThrew && preHost.State == "waiting");
        foreach (var jmsg in new[] { M("commit"), M("end", m => m.why = "timeout"), M("ready"), M("start"), M("reject"), M("hb"), M("nonsense") })
        {
            var (ja, jb) = MemoryLink.Pair();
            var jh = new SungkaSession("H"); jh.AttachGuest(ja);
            jb.Send(jmsg);
            bool threw = false; try { jh.Tick(0.01f); } catch (Exception) { threw = true; }
            Check($"a stray '{jmsg.t}' before hello does nothing and doesn't crash", !threw && jh.State == "waiting");
        }
        // A guest that has not been started yet gets stray game messages too.
        var (sa, sb) = MemoryLink.Pair();
        var strayGuest = new SungkaSession("G", sb);
        sa.Send(M("commit", m => { m.id = "x"; m.rev = 1; m.seat = 0; m.pit = 0; m.dig = 1; }));
        sa.Send(M("end", m => { m.id = "x"; m.why = "timeout"; m.seat = 1; }));
        bool strayThrew = false; try { strayGuest.Tick(0.01f); } catch (Exception) { strayThrew = true; }
        Check("a joining guest ignores game messages that arrive before the start", !strayThrew && strayGuest.State == "joining");
        // A hostile host can't pick the guest's moves or end the game for itself.
        var (fh, fg, fhl, fgl) = LanPair();
        while (fh.Board.Turn != 1) { fh.Move(Enumerable.Range(0, 7).First(p => fh.Board.CanMove(0, p))); fh.Tick(0.001f); fg.Tick(0.001f); }
        int forgedRev = fg.Board.Revision;
        var forged = new SungkaMatch(fh.Board.FirstSeat); foreach (var p in Enumerable.Range(0, 16)) forged.Pits[p] = fg.Board.Pits[p]; forged.Turn = fg.Board.Turn; forged.Revision = fg.Board.Revision;
        forged.Apply(1, Enumerable.Range(8, 7).First(p => forged.CanMove(1, p)));
        int forgedPit = Enumerable.Range(8, 7).First(p => fg.Board.CanMove(1, p));
        fgl.Inject(M("commit", m => { m.id = fg.MatchId; m.rev = forgedRev + 1; m.seat = 1; m.pit = forgedPit; m.dig = forged.Digest(); }));
        fg.Tick(0.001f);
        Check("a commit that plays the guest's move when the guest asked for nothing (and no time ran out) stops the game", fg.State == "over" && fg.EndWhy == "invalid" && fg.Result == "");
        var (eh, eg, ehl, egl) = LanPair();
        egl.Inject(M("end", m => { m.id = eh.MatchId; m.why = "timeout"; m.seat = 1 - eh.Board.Turn; }));
        eg.Tick(0.001f);
        Check("an immediate end for timeout, with no time having run out, is refused", eg.State == "over" && eg.EndWhy == "invalid" && eg.Result == "");
        var (th, tg, thl, tgl) = LanPair();
        tgl.Inject(M("end", m => { m.id = th.MatchId; m.why = "time"; m.s0 = 49; m.s1 = 49; }));
        tg.Tick(0.001f);
        Check("an end for time with minutes still on the clock is refused", tg.State == "over" && tg.EndWhy == "invalid");
        // Real timeouts still pass the guest's checks when its own clock has run down too.
        var (rh, rg, rhl, rgl) = LanPair();
        int pmover = rh.Board.Turn;
        rg.Tick(SungkaSession.TurnSecs - 0.5f); rh.Tick(SungkaSession.TurnSecs + 0.1f); rg.Tick(0.001f);
        Check("a move played for someone who really ran out of time is accepted by the guest", rg.State == "play" && rg.Board.Revision == 1 && rg.Board.Digest() == rh.Board.Digest());
        // A hostile host sending a start (a rematch) the guest never asked for, right after the last move.
        var (xh, xg, xhl, xgl) = LanPair();
        PlayOut(xh, xg, new Random(3));
        Check("a played-out game is over on both", xh.State == "over" && xg.State == "over");
        int gamesBefore = xg.Games;
        xgl.Inject(M("start", m => { m.v = 1; m.id = "newmatch"; m.first = 0; m.name = "Host"; }));
        xg.Tick(0.001f);
        Check("a start the guest never asked for (no rematch pressed) doesn't reset a finished game", xg.State == "over" && xg.MatchId == xh.MatchId && xg.Games == gamesBefore);
        // The finished game is not lost when a commit and a bye arrive together.
        // Backpressure: a peer flooding valid frames is cut off rather than allowed to pile up.
        using var floodHost = new LanHost("Flood", "sungka", 47862, 47863);
        var raw = new TcpClient(); raw.Connect("127.0.0.1", floodHost.Port);
        ILink floodLink = null; for (int i = 0; i < 100 && floodLink == null; i++) { floodLink = floodHost.Poll(); yield return 1; }
        var hbFrame = Wire.Frame(M("hb")); var floodBytes = new byte[hbFrame.Length * 3000];
        for (int i = 0; i < 3000; i++) Buffer.BlockCopy(hbFrame, 0, floodBytes, i * hbFrame.Length, hbFrame.Length);
        raw.GetStream().Write(floodBytes, 0, floodBytes.Length);
        for (int i = 0; i < 60 && floodLink.Alive; i++) { int n = 0; while (floodLink.Receive() != null && n++ < 64) { } yield return 1; }
        Check("a peer flooding thousands of valid frames is cut off instead of being queued without limit", !floodLink.Alive);
        raw.Close();
        yield return 1;

        // --- Round 2 ---
        Note("LAN Sungka: review round 2");
        // A late rejection for an older request can't clear the newer one that's waiting.
        var (qh, qg, qhl, qgl) = LanPair();
        while (qh.Board.Turn != 1) { qh.Move(Enumerable.Range(0, 7).First(p => qh.Board.CanMove(0, p))); qh.Tick(0.001f); qg.Tick(0.001f); }
        qg.Move(Enumerable.Range(8, 7).First(p => qg.Board.CanMove(1, p)));
        int liveSeq = qgl.Sent.Last(m => m.t == "move").seq;
        qgl.Inject(M("reject", m => { m.id = qh.MatchId; m.seq = liveSeq - 1; m.why = "stale"; }));
        qg.Tick(0.001f);
        Check("a rejection that answers an older request leaves the waiting move waiting", qg.Pending);
        qgl.Inject(M("reject", m => { m.id = qh.MatchId; m.seq = liveSeq; m.why = "stale"; }));
        qg.Tick(0.001f);
        Check("the one that answers it clears the wait", !qg.Pending);
        // A slow connection (nearly the silence limit) doesn't make a real timeout look false.
        var (lh, lg, lhl, lgl) = LanPair();
        lg.Tick(SungkaSession.TurnSecs - 6.5f); lh.Tick(SungkaSession.TurnSecs + 0.1f); lg.Tick(0.001f);
        Check("an automatic move that arrives with six seconds still on the guest's clock is accepted", lg.State == "play" && lg.Board.Revision == 1);
        // A start needs a fresh id and, for a rematch, the other player to begin.
        var (bh, bg, bhl, bgl) = LanPair();
        PlayOut(bh, bg, new Random(21));
        bg.Rematch();
        bgl.Inject(M("start", m => { m.v = 1; m.id = bh.MatchId; m.first = 1 - bh.Board.FirstSeat; m.name = "Host"; }));
        bg.Tick(0.001f);
        Check("a rematch that reuses the old match id is refused", bg.State == "over" && bg.EndWhy == "invalid");
        var (ch, cg, chl, cgl) = LanPair();
        PlayOut(ch, cg, new Random(22)); cg.Rematch();
        cgl.Inject(M("start", m => { m.v = 1; m.id = ""; m.first = 1 - ch.Board.FirstSeat; m.name = "Host"; })); cg.Tick(0.001f);
        Check("a rematch with no id is refused", cg.State == "over" && cg.EndWhy == "invalid");
        var (dh, dg, dhl, dgl) = LanPair();
        PlayOut(dh, dg, new Random(23)); dg.Rematch();
        dgl.Inject(M("start", m => { m.v = 1; m.id = "freshid"; m.first = dh.Board.FirstSeat; m.name = "Host"; })); dg.Tick(0.001f);
        Check("a rematch that doesn't hand the start to the other player is refused", dg.State == "over" && dg.EndWhy == "invalid");
        var (eh2, eg2, ehl2, egl2) = LanPair();
        PlayOut(eh2, eg2, new Random(24)); eg2.Rematch();
        egl2.Inject(M("start", m => { m.v = 1; m.id = "anotherid"; m.first = 1 - eh2.Board.FirstSeat; m.name = "Host"; })); eg2.Tick(0.001f);
        Check("a proper rematch start is accepted, and the guest's own record id is its own", eg2.State == "play" && eg2.RecordId.Length == 32 && eg2.RecordId != eg2.MatchId);
        // Sending to someone who never reads can't stall the game.
        using var sinkHost = new LanHost("Sink", "sungka", 47864, 47865);
        var sinkRaw = new TcpClient(); sinkRaw.Connect("127.0.0.1", sinkHost.Port);
        ILink sinkLink = null; for (int i = 0; i < 100 && sinkLink == null; i++) { sinkLink = sinkHost.Poll(); yield return 1; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20000 && sinkLink.Alive; i++) sinkLink.Send(M("commit", m => { m.id = new string('x', 32); m.pit = 3; }));
        sw.Stop();
        Check($"flooding a peer that never reads takes no real time ({sw.ElapsedMilliseconds} ms) and ends with the peer cut off", sw.ElapsedMilliseconds < 1500 && !sinkLink.Alive);
        sinkRaw.Close();
        yield return 1;

        // --- The screens ---
        Note("LAN Sungka: the screens, hosting");
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        MatchTestGame("Lanner");
        state.coins = 321; Give("rod_copper");
        lanDiscoveryPort = 47870; lanFirstPort = 47871; lanLoopbackOnly = true;
        yield return 4;
        Save();
        string advBefore = AdventureJson();
        // Lola's own board still plays after the refactor.
        OpenSungka(); yield return 3;
        ClickButton("sungka:0"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Lola's board still plays: the first house sows seven and ends in your head for another turn", mode == "panel" && panel == "sungka" && sungka[7] == 1 && !sungkaLolaTurn && sungka.Sum() == 98);
        ClosePanels(); yield return 2;
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "lan-matches-play"; yield return 2;
        Check("the Matches panel has the Sungka card with Host and Find buttons", panel == "matches" && Gfx.Seen.ContainsKey("Host a game") && Gfx.Seen.ContainsKey("Find a game"));
        ClickButton("Host a game"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"Host a game opens the screen and starts listening (mode {mode}, panel {panel}, view {lanView}, msg {lanMsg}, host {lanHost != null}, lan {lan?.State})", mode == "panel" && panel == "sungkalan" && lanView == "hosting" && lanHost != null && lan != null && lan.State == "waiting" && lanHost.Port is >= 47871 and < 47883);
        pendingShot = "lan-hosting"; yield return 2;
        // A guest (the test's own session) joins over a real socket.
        var gConnect = new LanConnect("127.0.0.1", lanHost.Port);
        ILink gLink = null; for (int i = 0; i < 300 && gLink == null; i++) { gLink = gConnect.Poll(); yield return 1; }
        var guestS = new SungkaSession("Test Guest", gLink);
        for (int i = 0; i < 200 && !(lanView == "play" && guestS.State == "play"); i++) { guestS.Tick(1f / 60); yield return 1; }
        Check($"a guest joins: the screen switches to the game and both know each other's names (view {lanView}, lan {lan?.State}, guest {guestS.State})", lanView == "play" && lan.State == "play" && lan.RivalName == "Test Guest" && guestS.RivalName == lan.MyName && lan.MySeat == 0 && guestS.MySeat == 1);
        Check("the game saw no change to the adventure (nothing was started or saved)", AdventureJson() == advBefore && mode == "panel");
        pendingShot = "lan-game-start"; yield return 2;
        // Play ten moves each: the guest as the test, the host through real clicks on the board.
        int hostMoves = 0, guestMoves = 0; var pr = new Random(11);
        for (int spin = 0; spin < 4000 && (hostMoves < 5 || guestMoves < 5) && lan.State == "play"; spin++)
        {
            guestS.Tick(1f / 60);
            if (guestS.MyTurn)
            {
                var legal = Enumerable.Range(8, 7).Where(p => guestS.Board.CanMove(1, p)).ToList();
                if (legal.Count > 0) { guestS.Move(legal[pr.Next(legal.Count)]); guestMoves++; }
            }
            else if (lan.MyTurn && lanView == "play")
            {
                int v = Enumerable.Range(0, 7).FirstOrDefault(i => lan.Board.Pits[i] > 0, -1);
                if (v >= 0 && ClickButton($"sungka:{v}")) { yield return 2; Inp.ScriptMouse = Offscreen; hostMoves++; }
            }
            yield return 1;
        }
        for (int i = 0; i < 30; i++) { guestS.Tick(1f / 60); yield return 1; }
        Check($"ten-odd moves by real clicks and by the guest keep both boards identical ({hostMoves} + {guestMoves} moves, move {lan.Board.Revision})", lan.Board.Revision == guestS.Board.Revision && lan.Board.Digest() == guestS.Board.Digest() && lan.Board.Pits.Sum() == 98 && hostMoves >= 5 && lan.State == "play");
        pendingShot = "lan-game-mid"; yield return 2;
        // Esc asks; the game goes on behind it.
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"Esc in a game asks whether to leave (ask {lanLeaveAsk}, panel {panel}, mode {mode}, state {lan?.State}, view {lanView})", lanLeaveAsk && panel == "sungkalan");
        pendingShot = "lan-leave"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc again keeps playing", !lanLeaveAsk && lan.State == "play");
        // The end by the clock: the host (this game) ends it and both agree.
        lan.MatchLeft = 0.01f; lan.TurnLeft = 99; guestS.MatchLeft = 0.01f;
        for (int i = 0; i < 40; i++) { guestS.Tick(1f / 60); yield return 1; }
        Check("when the clock runs out both sides end the game on the same board", lan.State == "over" && guestS.State == "over" && lan.EndWhy == "time" && guestS.EndWhy == "time" && lan.Board.Digest() == guestS.Board.Digest() && lan.Board.Total == 98);
        Check("one is the winner and the other the loser, or both drew", (lan.Result, guestS.Result) is ("win", "loss") or ("loss", "win") or ("draw", "draw"));
        Check($"the result is on the local board once, as {lan.Result}", Leaderboards.Board().entries.Count(e => e.mode == "sungka" && e.id == lan.RecordId) == 1 && Leaderboards.Board().entries.First(e => e.mode == "sungka").result == lan.Result);
        pendingShot = "lan-result"; yield return 2;
        string oldMatch = lan.MatchId;
        ClickButton("Rematch"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Rematch waits for the other side", lan.WaitingForRematch && lan.State == "over");
        guestS.Rematch();
        for (int i = 0; i < 60 && lan.State != "play"; i++) { guestS.Tick(1f / 60); yield return 1; }
        for (int i = 0; i < 10; i++) { guestS.Tick(1f / 60); yield return 1; }
        Check($"then a new game starts for both, with a new id (lan {lan.State}/{lan.MatchId == oldMatch}/{lan.Board.Revision}, guest {guestS.State}/{guestS.MatchId == lan.MatchId}, view {lanView})", lan.State == "play" && guestS.State == "play" && lan.MatchId != oldMatch && lan.MatchId == guestS.MatchId && lan.Board.Revision == 0 && lanView == "play");
        // Leaving by the real buttons is a forfeit.
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 4;
        for (int i = 0; i < 40; i++) { guestS.Tick(1f / 60); yield return 1; }
        Check("Leave closes the panel and the guest is told it left", mode == "play" && panel == null && lan == null && lanHost == null && guestS.State == "over" && guestS.Result == "win" && guestS.EndWhy == "left");
        Check("the leaver's game is on the board as a forfeit, not a best score", Leaderboards.Board().entries.Any(e => e.mode == "sungka" && e.result == "forfeit") && Leaderboards.Best("sungka", 10).All(e => e.result != "forfeit"));
        Check("none of this touched the adventure", AdventureJson() == advBefore && state.coins == 321);
        guestS.Close();

        // --- Joining ---
        Note("LAN Sungka: the screens, joining");
        using var theHost = new LanHost("Pia the host", "sungka", 47870, 47881);
        var hostSession = new SungkaSession("Pia the host");
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Find a game"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Find a game opens the list", panel == "sungkalan" && lanView == "finding" && lanBrowser != null);
        for (int i = 0; i < 300 && !(lanBrowser.Hosts.Count > 0); i++) { theHost.Poll(); yield return 1; }
        yield return 3;
        Check("a game on the network shows up by name", lanBrowser.Hosts.Count == 1 && lanBrowser.Hosts[0].Name == "Pia the host" && Gfx.Seen.ContainsKey("Join"));
        pendingShot = "lan-finding"; yield return 2;
        ClickButton("Join"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ILink jlink = null; for (int i = 0; i < 300 && jlink == null; i++) { jlink = theHost.Poll(); yield return 1; }
        hostSession.AttachGuest(jlink);
        for (int i = 0; i < 200 && !(lanView == "play" && lan != null && lan.State == "play"); i++) { hostSession.Tick(1f / 60); yield return 1; }
        Check("clicking Join connects and the game starts, with you as the guest in seat 1", lanView == "play" && lan.State == "play" && lan.MySeat == 1 && lan.RivalName == "Pia the host" && hostSession.RivalName == lan.MyName);
        // The guest's board is turned round: its own houses are along the bottom.
        for (int k = 0; k < 8 && hostSession.Board.Turn == 0; k++) { hostSession.Move(Enumerable.Range(0, 7).First(p => hostSession.Board.CanMove(0, p))); hostSession.Tick(1f / 60); }
        for (int i = 0; i < 40 && !lan.MyTurn; i++) { hostSession.Tick(1f / 60); yield return 1; }
        pendingShot = "lan-guest-view"; yield return 2;
        Check("it is the guest's turn after the host's first move (or it started)", lan.MyTurn);
        int gv = Enumerable.Range(0, 7).First(v => lan.Board.Pits[8 + v] > 0);
        int revBeforeClick = lan.Board.Revision;
        for (int attempt = 0; attempt < 4 && lan.Board.Revision <= revBeforeClick; attempt++) { ClickButton($"sungka:{gv}"); yield return 4; Inp.ScriptMouse = Offscreen; for (int k = 0; k < 8; k++) { hostSession.Tick(1f / 60); yield return 1; } }
        for (int i = 0; i < 60; i++) { hostSession.Tick(1f / 60); yield return 1; }
        Check($"a click on the guest's bottom row plays that house on both boards (rev {lan.Board.Revision}/{hostSession.Board.Revision}, myturn {lan.MyTurn}, pending {lan.Pending}, gv {gv})", lan.Board.Revision > revBeforeClick && lan.Board.Revision == hostSession.Board.Revision && lan.Board.Digest() == hostSession.Board.Digest());
        // The host disappears.
        hostSession.Leave(); theHost.Dispose();
        for (int i = 0; i < 40; i++) yield return 1;
        Check("the host leaving ends the game as a win for the guest, with the result on screen", lan.State == "over" && lan.Result == "win" && lan.EndWhy == "left");
        pendingShot = "lan-host-left"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc after a game closes the panel with no question", panel == null && mode == "play" && lan == null);
        // Typing an address.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Find a game"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        foreach (char c in "12ab.3x") Inp.ScriptChars.Enqueue(c);
        yield return 3;
        Check("only digits, dots and colons can be typed into the address", lanAddr == "12.3");
        for (int i = 0; i < 5; i++) { Inp.Tap(KeyboardKey.Backspace); yield return 2; }
        Check("Backspace takes them off again", lanAddr == "");
        foreach (char c in "999") Inp.ScriptChars.Enqueue(c);
        yield return 3; ClickButton("Connect"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a bad address says so and doesn't try", lanConnect == null && lanMsg.Contains("address") && lanView == "finding");
        lanAddr = "127.0.0.1:47899"; ClickButton("Connect"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a good address that nobody answers on shows Connecting and then gives up politely", lanConnect != null || lanMsg != "");
        for (int i = 0; i < 400 && lanConnect != null; i++) yield return 1;
        Check($"and the answer is a message, not a crash ({lanMsg})", lanConnect == null && lanMsg != "" && lanView == "finding");
        // Typing the real host's address connects.
        var host2 = new LanHost("Typed host", "sungka", 47870, 47891); var typedSes = new SungkaSession("Typed host");
        lanAddr = $"127.0.0.1:{host2.Port}"; ClickButton("Connect"); yield return 3; Inp.ScriptMouse = Offscreen;
        ILink typedLink = null; for (int i = 0; i < 300 && typedLink == null; i++) { typedLink = host2.Poll(); yield return 1; }
        typedSes.AttachGuest(typedLink);
        for (int i = 0; i < 200 && !(lanView == "play" && lan?.State == "play"); i++) { typedSes.Tick(1f / 60); yield return 1; }
        Check("typing an address and pressing Connect joins that game", lanView == "play" && lan.State == "play" && lan.RivalName == "Typed host");
        ClosePanels(); yield return 3;
        for (int i = 0; i < 30; i++) { typedSes.Tick(1f / 60); yield return 1; }
        Check("closing the panel mid-game forfeits it: the host is told", typedSes.State == "over" && typedSes.Result == "win");
        host2.Dispose(); typedSes.Close();
        Check("the panel left nothing running", lan == null && lanHost == null && lanBrowser == null && lanConnect == null && AdventureJson() == advBefore);
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        lanDiscoveryPort = LanHost.DiscoveryPort; lanFirstPort = LanHost.FirstPort; lanLoopbackOnly = false;
        yield return 2;
        yield return 2;
    }
}
#endif
