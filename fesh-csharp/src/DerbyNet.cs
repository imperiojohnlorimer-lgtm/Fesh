using System.Diagnostics;

namespace Fesh;

// A Derby Duel between two people (1.30): each fishes the same seeded water on their own computer and reports what they
// land; both see the other's running total and both work out the result. No screen and no sockets in here: it runs over any
// ILink (a routed link through the Tournament Dock, in the game).
//
// The challenger is the referee. It picks the seed and the length, starts the run once both are ready, and its final
// scores are the ones both sides record; the guest checks them against what it saw. Nobody is told a weight: a landing
// is reported as "cast number n, hooked perfectly or not, at this second", and the receiver works the fish and its
// weight out from the seed (MatchRules.Plan), so a made-up weight, a cast that never happened, a second landing of the
// same cast or one at an impossible time is refused. What can't be checked without replaying the fight is whether a
// reported landing really was reeled in, or really was a perfect hook: that is taken on trust, as in any game with no
// server, and the game says so (the boards are local and for fun).
sealed class DerbySession
{
    public const float Duration = MatchRules.DerbySecs, Grace = 15, SilentLimit = 6, HeartbeatSecs = 1, FutureSlack = 8;
    public const float MinSecsPerCast = 3.5f;          // the fastest a cast can come in: bite, hook and fight
    public readonly bool Host;
    public ILink Link;
    public readonly string MyName;
    public string RivalName = "";
    public string State = "joining";                    // joining (host: waiting for hello), wait (agreeing to start), run, over, closed
    public string MatchId = "";
    public int Seed, Games;
    public string EndWhy = "";                          // finished, left, invalid
    public bool PeerGone, Started, StartAsked;
    public string Notice = "";
    public int Version;
    public readonly List<MatchEvent> Theirs = new();
    public int TheirCasts, TheirFinalCg = -1, TheirFinalPerfects, TheirFinalCatches;
    public MatchScore MyFinal; public bool MyFinished;
    public bool Mismatch;
    public string RecordId = "";
    int lastCast = -1; float lastT;                     // the last landing accepted: landings come in cast order and a cast takes time
    bool myReady, theirReady, rematchMe, rematchThem, hasResult, iLeft;
    float hb, graceLeft = -1;

    public DerbySession(string myName, ILink link, bool host)
    {
        Host = host; MyName = SungkaSession.Clean(myName); Link = link;
        if (!host) Send(new NetMsg { t = "dhello", v = SungkaSession.Protocol, name = MyName });
    }

    void Send(NetMsg m) => Link?.Send(m);
    void Bump() => Version++;

    public MatchScore TheirScore
    {
        get
        {
            var s = new MatchScore { Casts = TheirCasts };
            foreach (var e in Theirs) { s.Cg += e.cg; s.Catches++; if (e.perfect) s.Perfects++; }
            return s;
        }
    }
    public string Result
    {
        get
        {
            if (State != "over" || !hasResult) return "";
            if (EndWhy == "left") return iLeft ? "loss" : "win";
            var (w, _) = MatchRules.Decide(MyFinal, TheirScore);
            return w > 0 ? "win" : w < 0 ? "loss" : "draw";
        }
    }

    /* ---------- The handshake ---------- */
    // The host's side is ready (it has fished into the water and has nothing left to wait for) and sends the match.
    public void HostBegin(int seed)
    {
        if (!Host || State != "wait" || myReady) return;
        Seed = seed;
        MatchId = Guid.NewGuid().ToString("N");
        RecordId = Guid.NewGuid().ToString("N");
        Games++;
        Send(new NetMsg { t = "dstart", id = MatchId, s0 = seed, s1 = (int)Duration, v = SungkaSession.Protocol, name = MyName });
        myReady = true;
        Bump();
    }

    // The guest's side is ready: say so.
    public void GuestReady()
    {
        if (Host || !StartAsked || myReady) return;
        myReady = true;
        Send(new NetMsg { t = "dready", id = MatchId });
    }

    public void Tick(float dt, float myClock)
    {
        if (State == "closed") return;
        if (Link != null)
        {
            for (int i = 0; i < 64; i++)
            {
                var m = Link.Receive();
                if (m == null) break;
                string was = State;
                Handle(m, myClock);
                if (State == "closed" || was != State && State == "over") break;
            }
            hb += dt;
            if (hb >= HeartbeatSecs) { hb = 0; Send(new NetMsg { t = "hb" }); }
            if (!PeerGone && (!Link.Alive || Link.SilentFor > SilentLimit)) PeerLost();
        }
        // A finished run waits a few seconds for the other side's last word, then goes with what it has.
        if (State == "run" && MyFinished && graceLeft > 0)
        {
            graceLeft -= dt;
            // Their landings all arrive before their final (one ordered link), so once it is here nothing is missing; if it
            // never comes, the game goes with what was seen.
            if (graceLeft <= 0 || TheirFinalCg >= 0) Finish("finished");
        }
    }

    void Handle(NetMsg m, float myClock)
    {
        switch (m.t)
        {
            case "dhello" when Host && State == "joining":
                if (m.v != SungkaSession.Protocol) { Send(new NetMsg { t = "bye", why = "version" }); Link.Close(); PeerGone = true; State = "closed"; Bump(); return; }
                RivalName = SungkaSession.Clean(m.name);
                State = "wait";
                Bump();
                break;
            case "dstart" when !Host && State is "joining" or "wait" or "over":
            {
                if (m.v != SungkaSession.Protocol || string.IsNullOrEmpty(m.id) || m.id.Length > 40 || m.id == MatchId || m.s1 != (int)Duration) { Invalid("a bad start"); return; }
                // After a game, a start is only taken if a rematch was asked for.
                if (State == "over" && !rematchMe) return;
                RivalName = SungkaSession.Clean(m.name);
                MatchId = m.id; Seed = m.s0; Games++;
                RecordId = Guid.NewGuid().ToString("N");
                ResetRun();
                State = "wait"; StartAsked = true;
                Bump();
                break;
            }
            case "dready" when Host && State == "wait" && m.id == MatchId:
                theirReady = true;
                if (myReady && !Started) HostRun();
                break;
            case "drun" when !Host && State == "wait" && m.id == MatchId && myReady:
                Started = true; State = "run"; Bump();
                break;
            case "dland" when State == "run":
                AcceptLanding(m, myClock);
                break;
            case "dend" when State is "run" or "over":
                if (m.n == null || m.n.Length != 4) { Invalid("a bad result"); break; }
                TheirFinalCg = m.n[0]; TheirFinalPerfects = m.n[1]; TheirFinalCatches = m.n[2]; TheirCasts = Math.Clamp(m.n[3], 0, 400);
                if (MyFinished) Finish("finished");
                break;
            case "drematch" when State == "over" && EndWhy == "finished":
                rematchThem = true;
                if (Host && rematchMe && rematchThem) HostRematch();
                break;
            case "bye":
                PeerLeft(m.why);
                break;
        }
    }

    void ResetRun()
    {
        Theirs.Clear(); lastCast = -1; lastT = 0; TheirCasts = 0; TheirFinalCg = -1; TheirFinalPerfects = 0; TheirFinalCatches = 0;
        MyFinal = default; MyFinished = false; Mismatch = false; hasResult = false; EndWhy = ""; Notice = "";
        myReady = theirReady = rematchMe = rematchThem = Started = StartAsked = false; graceLeft = -1;
    }

    void HostRun()
    {
        Started = true; State = "run";
        Send(new NetMsg { t = "drun", id = MatchId });
        Bump();
    }

    /* ---------- Landings ---------- */
    // Called when I land a fish (cast number, hooked perfectly, seconds into the run): the other side is told.
    public void ReportLanding(int cast, bool perfect, float t)
    {
        if (State != "run" || MyFinished) return;
        Send(new NetMsg { t = "dland", id = MatchId, n = new[] { cast, perfect ? 1 : 0, (int)MathF.Round(t * 1000) } });
    }

    // The earliest second a landing of the nth cast can happen: nobody reels in five fish in the first ten seconds.
    public static float EarliestLanding(int cast) => 2.5f + MinSecsPerCast * cast;

    void AcceptLanding(NetMsg m, float myClock)
    {
        if (m.id != MatchId || m.n == null || m.n.Length != 3) return;
        int cast = m.n[0]; bool perfect = m.n[1] == 1; float t = m.n[2] / 1000f;
        // Landings come in cast order, and every cast takes at least the fastest a cast can come in, so a landing has to be
        // that much later than the last one for each cast between them, and not before the first could be. It also has to
        // be within the run and no further ahead of my own clock than a slow link could explain (a few seconds).
        if (cast < 0 || cast > 400 || cast <= lastCast || m.n[1] is not (0 or 1)) { Invalid("an impossible landing"); return; }
        float earliest = lastCast < 0 ? EarliestLanding(cast) : lastT + MinSecsPerCast * (cast - lastCast);
        if (t < earliest || t > Duration + 1 || t > myClock + FutureSlack) { Invalid("an impossible landing"); return; }
        lastCast = cast; lastT = t;
        var plan = MatchRules.Plan(Seed, cast);
        Theirs.Add(new MatchEvent { t = t, fish = plan.FishId, cg = MatchRules.Centi(plan.FishId, plan.Size, perfect), perfect = perfect });
        TheirCasts = Math.Max(TheirCasts, cast + 1);
        Bump();
    }

    /* ---------- The end ---------- */
    // My run is over: tell the other side the totals, and wait a few seconds for theirs.
    public void ReportEnd(MatchScore mine)
    {
        if (State != "run" || MyFinished) return;
        MyFinal = mine; MyFinished = true;
        Send(new NetMsg { t = "dend", id = MatchId, n = new[] { mine.Cg, mine.Perfects, mine.Catches, mine.Casts } });
        graceLeft = Grace;
        if (TheirFinalCg >= 0) Finish("finished");
    }

    void Finish(string why)
    {
        if (State == "over") return;
        State = "over"; EndWhy = why; hasResult = true;
        // What they said they landed has to match what they were seen to land.
        var ts = TheirScore;
        if (TheirFinalCg >= 0 && (TheirFinalCg != ts.Cg || TheirFinalPerfects != ts.Perfects || TheirFinalCatches != ts.Catches)) { Mismatch = true; EndWhy = "invalid"; hasResult = false; Notice = "The scores didn't agree, so this game doesn't count."; }
        else if (TheirFinalCg < 0) { Notice = ""; }
        Bump();
    }

    void Invalid(string why)
    {
        Send(new NetMsg { t = "bye", why = "invalid" });
        State = "over"; EndWhy = "invalid"; hasResult = false; PeerGone = true; Notice = $"The game was stopped ({why}).";
        Link?.Close();
        Bump();
    }

    void PeerLeft(string why)
    {
        // A stop for a broken rule or another version is no one winning; leaving is.
        if (State is "run" or "wait" && why is "invalid" or "version") { State = "over"; EndWhy = "invalid"; hasResult = false; Notice = "The game was stopped."; }
        else if (State is "run" or "wait") { State = "over"; EndWhy = "left"; hasResult = true; Notice = $"{RivalName} left the game."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    void PeerLost()
    {
        if (State is "run" or "wait") { State = "over"; EndWhy = "left"; hasResult = true; Notice = $"{RivalName} stopped answering. You win."; }
        else if (State == "joining") { State = "closed"; Notice = "The other player didn't answer."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    /* ---------- After ---------- */
    public void Rematch()
    {
        if (State != "over" || PeerGone || EndWhy != "finished" || rematchMe) return;
        rematchMe = true;
        if (Host) { if (rematchThem) HostRematch(); } else Send(new NetMsg { t = "drematch", id = MatchId });
        Bump();
    }
    public bool WaitingForRematch => rematchMe && State == "over";

    void HostRematch()
    {
        // The next game: the same two, a new seed, a new id.
        ResetRun();
        State = "wait";
        Seed = MatchRules.Mix(Environment.TickCount, Games, 17);
        MatchId = Guid.NewGuid().ToString("N"); RecordId = Guid.NewGuid().ToString("N");
        Games++;
        Send(new NetMsg { t = "dstart", id = MatchId, s0 = Seed, s1 = (int)Duration, v = SungkaSession.Protocol, name = MyName });
        myReady = true;
        Bump();
    }

    // Leaving: mid-game a forfeit.
    public void Leave()
    {
        if (State == "closed") return;
        bool inGame = State is "run" or "wait";
        iLeft = true;
        Send(new NetMsg { t = "bye", why = "left" });
        Link?.Close();
        Link = null;
        if (inGame) { State = "over"; EndWhy = "left"; hasResult = true; PeerGone = true; }
        else if (State != "over") State = "closed";
        PeerGone = true;
        Bump();
    }

    public void Close()
    {
        Link?.Close(); Link = null;
        State = "closed";
    }
}
