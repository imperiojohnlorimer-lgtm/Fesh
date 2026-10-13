namespace Fesh;

// A game of sungka between two people over a link (1.30). Pure logic: no screen, no sockets of its own, so it runs the
// same over a real connection and over the in-memory one the tests use.
//
// The host is the referee. Seat 0 is the host, seat 1 the guest. The guest asks (`move`, with the move number it expects
// next); the host checks it is that seat's turn, a house they own with shells in it, and the right move number, plays it
// and tells both sides (`commit`, with a fingerprint of the board). The host's own moves go out the same way, so both
// computers apply every move exactly once. A move that doesn't fit is turned down (`reject`). Whoever stops answering,
// leaves, or sends nonsense loses the game; there is no resuming.
//
// 20 seconds a move (the host plays the leftmost house for someone who runs out, and three in a row is a forfeit) and
// 5 minutes a game (what's left on each side then goes to its owner), so a match stays short.
sealed class SungkaSession
{
    public const int Protocol = 1;
    public const float TurnSecs = 20, MatchSecs = 300, SilentLimit = 6, HeartbeatSecs = 1;

    public readonly bool Host;
    public ILink Link;
    public readonly string MyName;
    public string RivalName = "";
    // waiting (the host, until someone says hello), joining (the guest, until the host starts), play, over, closed.
    public string State;
    public SungkaMatch Board;
    public int MySeat;
    public string MatchId = "";
    public int Games;                       // games started in this session (a rematch is another)
    public float TurnLeft, MatchLeft;
    public string EndWhy = "";              // finished, time, timeout, left, invalid
    public int Winner = -1;                 // the winning seat, or -1 for a draw (or none: see Result)
    public bool PeerGone;                   // the other side has left or been lost
    public bool Pending;                    // the guest's move is on its way to the host
    public string Notice = "";              // something worth telling the player ("Time ran out: a move was played for them")
    public int Version;                     // goes up whenever the board or the state changes, so a screen can notice
    bool myReady, theirReady, hasResult;
    readonly int[] timeouts = new int[2];      // the host's count of moves it had to play for each seat
    readonly int[] autoSeen = new int[2];      // the guest's own count of the same, from what arrives and its own clock
    int pendingPit = -1, moveSeq, pendingSeq;
    public string RecordId = "";               // this computer's own id for the game on the leaderboard (the other side can't choose it)
    float hb;
    // A move played for someone who ran out of time must arrive when the guest's own clock for that turn is nearly down too
    // (it started a little later than the host's, by however long a message takes), so a host can't make the guest's moves for
    // it, or end the game early, by claiming a timeout that never happened.
    // (The host is the referee and is trusted with the game itself; these checks only stop plain forgery. The grace is a little
    // more than the silence the link tolerates, so a slow connection never makes a real timeout look false.)
    const float ExpiryGrace = SilentLimit + 2;

    // The host: waiting for a guest.
    public SungkaSession(string myName)
    {
        Host = true; MyName = Clean(myName); State = "waiting";
    }

    // The guest: connected to a host, saying hello.
    public SungkaSession(string myName, ILink link)
    {
        Host = false; MyName = Clean(myName); Link = link; State = "joining"; MySeat = 1;
        Send(new NetMsg { t = "hello", v = Protocol, name = MyName });
    }

    // Names from the other side are cut to plain, short text before they reach a screen.
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Player";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) if (c is >= ' ' and <= '~' && sb.Length < 24) sb.Append(c);
        return sb.Length == 0 ? "Player" : sb.ToString().Trim();
    }

    /// <summary>My result once it's over: win, loss or draw; empty if there isn't one (a broken game) or it isn't over.</summary>
    public string Result => State != "over" || !hasResult ? "" : Winner < 0 ? "draw" : Winner == MySeat ? "win" : "loss";
    public int MyScore => Board?.Score(MySeat) ?? 0;
    public int RivalScore => Board?.Score(1 - MySeat) ?? 0;
    public bool MyTurn => State == "play" && Board != null && Board.Turn == MySeat && !Pending;

    void Send(NetMsg m) => Link?.Send(m);
    void Bump() => Version++;

    // The host takes a guest that has connected. It must say hello first.
    public bool AttachGuest(ILink link)
    {
        if (!Host || Link != null || State != "waiting") { link.Send(new NetMsg { t = "bye", why = "full" }); link.Close(); return false; }
        Link = link;
        helloBy = 0;
        Bump();
        return true;
    }
    float helloBy;

    public void Tick(float dt)
    {
        if (State == "closed") return;
        if (Link != null)
        {
            for (int i = 0; i < 64; i++)
            {
                var m = Link.Receive();
                if (m == null) break;
                string was = State;
                Handle(m);
                // A game that has just ended is looked at (and recorded) before anything else that arrived with it.
                if (Link == null || State == "closed" || was != State && State == "over") break;
            }
        }
        if (Link != null)
        {
            hb += dt;
            if (hb >= HeartbeatSecs) { hb = 0; Send(new NetMsg { t = "hb" }); }
            if (State == "waiting") { helloBy += dt; if (!Link.Alive || helloBy > SilentLimit) DropGuest(); }
            else if (!PeerGone && (!Link.Alive || Link.SilentFor > SilentLimit)) PeerLost();
        }
        if (State != "play") return;
        TurnLeft = Math.Max(0, TurnLeft - dt);
        MatchLeft = Math.Max(0, MatchLeft - dt);
        if (!Host) return;
        if (MatchLeft <= 0)
        {
            Board.Settle();
            Board.Over = true;
            Send(new NetMsg { t = "end", id = MatchId, why = "time", s0 = Board.Score(0), s1 = Board.Score(1) });
            Finish("time", Board.Winner);
        }
        else if (TurnLeft <= 0) TimedOut();
    }

    void DropGuest()
    {
        Send(new NetMsg { t = "bye", why = "silent" });
        Link?.Close();
        Link = null;
        RivalName = "";
        Bump();
    }

    void TimedOut()
    {
        int seat = Board.Turn;
        timeouts[seat]++;
        if (timeouts[seat] >= 3)
        {
            Send(new NetMsg { t = "end", id = MatchId, why = "timeout", seat = seat });
            Finish("timeout", 1 - seat);
            return;
        }
        int pit = Board.AutoPit();
        Notice = seat == MySeat ? "Time ran out: a move was played for you." : $"{RivalName} ran out of time: a move was played for them.";
        PlayHost(seat, pit, auto: true);
    }

    void Handle(NetMsg m)
    {
        switch (m.t)
        {
            case "hello" when Host && State == "waiting":
                if (m.v != Protocol) { Send(new NetMsg { t = "bye", why = "version" }); Link.Close(); Link = null; Notice = "Someone with a different version of Fesh tried to join."; Bump(); return; }
                RivalName = Clean(m.name);
                StartMatch();
                break;
            // A start is the first one, or a rematch the guest asked for: a stray one never resets a finished game.
            case "start" when !Host && (State == "joining" || State == "over" && myReady):
                if (m.v != 0 && m.v != Protocol) { Invalid("version"); return; }
                int firstSeat = m.first == 1 ? 1 : 0;
                // A start needs a fresh id, and a rematch the other player to begin.
                if (string.IsNullOrEmpty(m.id) || m.id.Length > 40 || m.id == MatchId || Board != null && firstSeat != 1 - Board.FirstSeat) { Invalid("a bad start"); return; }
                RivalName = Clean(m.name);
                MatchId = m.id ?? "";
                Board = new SungkaMatch(firstSeat);
                RecordId = Guid.NewGuid().ToString("N");
                MySeat = 1; State = "play"; Games++; Pending = false; pendingPit = -1; hasResult = false; Winner = -1; EndWhy = ""; Notice = "";
                TurnLeft = TurnSecs; MatchLeft = MatchSecs; myReady = theirReady = false;
                Array.Clear(autoSeen);
                Bump();
                break;
            case "move" when Host:
                // (before the game has started there is no board to look at)
                if (State != "play" || m.id != MatchId || m.rev != Board.Revision) { Send(new NetMsg { t = "reject", id = MatchId, rev = Board?.Revision ?? 0, seq = m.seq, why = "stale" }); break; }
                if (!Board.CanMove(1, m.pit)) { Send(new NetMsg { t = "reject", id = MatchId, rev = Board.Revision, seq = m.seq, why = "illegal" }); break; }
                PlayHost(1, m.pit, auto: false);
                break;
            case "commit" when !Host && State == "play":
            {
                if (m.id != MatchId || m.rev != Board.Revision + 1 || m.seat is not (0 or 1) || !Board.CanMove(m.seat, m.pit)) { Invalid("out of step"); break; }
                bool expired = TurnLeft <= ExpiryGrace, auto = expired && m.pit == Board.AutoPit();
                if (m.seat == MySeat && !(Pending && m.pit == pendingPit) && !auto) { Invalid("a move I didn't make"); break; }
                autoSeen[m.seat] = auto ? autoSeen[m.seat] + 1 : 0;
                Board.Apply(m.seat, m.pit);
                if (Board.Digest() != m.dig) { Invalid("the boards differ"); break; }
                Pending = false; pendingPit = -1; TurnLeft = TurnSecs; Notice = "";
                if (Board.Over) Finish("finished", Board.Winner); else Bump();
                break;
            }
            case "reject" when !Host:
                // Only the answer to the move still waiting (a late one for an older request changes nothing).
                if (m.seq == pendingSeq) { Pending = false; pendingPit = -1; Bump(); }
                break;
            case "end" when !Host && State == "play" && m.id == MatchId:
                if (m.why == "time")
                {
                    if (MatchLeft > ExpiryGrace) { Invalid("time hadn't run out"); break; }
                    Board.Settle(); Board.Over = true;
                    if (Board.Score(0) != m.s0 || Board.Score(1) != m.s1) { Invalid("the scores differ"); break; }
                    Finish("time", Board.Winner);
                }
                else if (m.why == "timeout" && m.seat is 0 or 1)
                {
                    // Only after the same seat has been seen running out of time twice before, and now again.
                    if (autoSeen[m.seat] < 2 || TurnLeft > ExpiryGrace || Board.Turn != m.seat) { Invalid("no timeout"); break; }
                    Finish("timeout", 1 - m.seat);
                }
                else Invalid("unknown end");
                break;
            case "ready" when Host && State == "over":
                theirReady = true;
                MaybeRematch();
                break;
            case "bye":
                PeerLeft(m.why == "version" ? "version" : "left");
                break;
        }
    }

    // The host picks who starts the first game from the match id, and swaps it each time after.
    void StartMatch()
    {
        MatchId = Guid.NewGuid().ToString("N");
        int first = Games == 0 ? (MatchId[0] % 2) : 1 - Board.FirstSeat;
        Board = new SungkaMatch(first);
        RecordId = Guid.NewGuid().ToString("N");
        MySeat = 0; State = "play"; Games++; Pending = false; hasResult = false; Winner = -1; EndWhy = ""; Notice = "";
        TurnLeft = TurnSecs; MatchLeft = MatchSecs; myReady = theirReady = false;
        Array.Clear(timeouts);
        Send(new NetMsg { t = "start", v = Protocol, id = MatchId, first = first, name = MyName });
        Bump();
    }

    void PlayHost(int seat, int pit, bool auto)
    {
        if (!Board.Apply(seat, pit)) return;
        if (!auto) timeouts[seat] = 0;
        TurnLeft = TurnSecs;
        Send(new NetMsg { t = "commit", id = MatchId, seat = seat, pit = pit, rev = Board.Revision, dig = Board.Digest() });
        if (Board.Over) Finish("finished", Board.Winner); else Bump();
    }

    void Finish(string why, int winner)
    {
        State = "over"; EndWhy = why; Winner = winner; hasResult = true; Pending = false;
        Bump();
    }

    // Something that can't be right: the game is over with no winner, and the connection is closed.
    void Invalid(string why)
    {
        Send(new NetMsg { t = "bye", why = "invalid" });
        State = "over"; EndWhy = "invalid"; hasResult = false; Winner = -1; Pending = false; PeerGone = true;
        Notice = $"The game was stopped ({why}).";
        Link?.Close();
        Bump();
    }

    void PeerLeft(string why)
    {
        if (State == "play") { Finish("left", MySeat); Notice = $"{RivalName} left the game."; }
        else if (State == "joining") { State = "closed"; Notice = why == "version" ? "That game runs a different version of Fesh." : why == "full" ? "That game already has two players." : "The host closed the game."; }
        PeerGone = true;
        Link?.Close();
        if (Host && State == "waiting") { Link = null; RivalName = ""; PeerGone = false; }
        Bump();
    }

    // The link died or went quiet.
    void PeerLost()
    {
        if (State == "play") { Finish("left", MySeat); Notice = $"{RivalName} stopped answering. You win."; }
        else if (State == "joining") { State = "closed"; Notice = "The host didn't answer."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    /* ---------- What the player does ---------- */
    // A move of mine. False if it isn't allowed right now.
    public bool Move(int pit)
    {
        if (State != "play" || Board == null || !Board.CanMove(MySeat, pit) || Pending) return false;
        if (Host) { PlayHost(0, pit, auto: false); return true; }
        pendingSeq = ++moveSeq;
        Send(new NetMsg { t = "move", id = MatchId, rev = Board.Revision, pit = pit, seq = pendingSeq });
        Pending = true; pendingPit = pit;
        Bump();
        return true;
    }

    // Ready for another game. The host starts it once both are.
    public void Rematch()
    {
        if (State != "over" || PeerGone || myReady) return;
        myReady = true;
        if (Host) MaybeRematch(); else Send(new NetMsg { t = "ready" });
        Bump();
    }
    public bool WaitingForRematch => myReady && State == "over";

    void MaybeRematch() { if (Host && myReady && theirReady && State == "over" && !PeerGone) StartMatch(); }

    // Leaving. Mid-game it's a forfeit.
    public void Leave()
    {
        if (State == "closed") return;
        if (State == "play") Finish("left", 1 - MySeat);
        Send(new NetMsg { t = "bye", why = "left" });
        Link?.Close();
        Link = null;
        if (State != "over") State = "closed";
        PeerGone = true;
        Bump();
    }

    public void Close()
    {
        Link?.Close();
        Link = null;
        State = "closed";
    }
}
