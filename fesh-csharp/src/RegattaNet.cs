namespace Fesh;

// The head-to-head Regatta (1.30): two boats on Dado's course round the Daang Pulo islets, best of three heats, each
// racer seeing the other's boat on the water. No screen and no sockets in here: a RegattaSession runs over any ILink (a
// routed link through the Tournament Dock, or an in-memory one with a RegattaBot on the other end, so racing alone is the
// same code as racing a person).
//
// Nobody is told a time. Each side streams its boat's position eight times a second, and each side works the other's
// progress out itself (RegattaTracker: gates in order, with the time it crossed each, from the positions), so a
// finish can't be claimed that the positions don't show; positions that jump faster than a boat can sail, go backwards in time
// or run ahead of the receiver's own clock stop the race as invalid. What can't be told from here is a boat that
// really sails the course faster than the game allows by some other means; the boards are local and for fun.
static class RegattaRules
{
    public const int Protocol = 1, Heats = 3;
    public const float GateR = 18, HeatLimit = 80, CountSecs = 3, BreakSecs = 5, MaxSpeed = 135, FutureSlack = 6, PosEvery = 0.125f;
    // Gate centres, in pixels: [0] is the start and finish line. Each gate is a pair of buoys across the course.
    public static readonly (float x, float y)[] Gates =
    {
        (645, 535), (835, 565), (935, 645), (865, 735), (765, 705), (620, 698), (440, 725), (420, 590), (545, 600)
    };
    // Where the two boats wait for the start (the host on the left): just north-west and north-east of the first buoys' line.
    public static (float x, float y) Slot(int seat) => (seat == 0 ? 616 : 674, 548);
}

// What one boat has done on the course, worked out from where it was and when: how many gates, and the time it crossed the
// last. Used for your own boat and, from the positions it sends, for the other.
sealed class RegattaTracker
{
    public int Next = 1;                              // the gate to cross next (1..8); 9 means back to the start line to finish
    public float FinishT = -1;                        // seconds into the heat when the finish line was crossed
    public bool Cheat;                                // a position no boat could have sailed
    public float X, Y, T;
    public float Dist;                                // how far it has sailed in all: no boat beats MaxSpeed times the time so far
    bool has;
    public readonly List<float> GateTimes = new();

    public void Reset(float x, float y)
    {
        Next = 1; FinishT = -1; Cheat = false; X = x; Y = y; T = 0; Dist = 0; has = true; GateTimes.Clear();
    }

    public int Gates => RegattaRules.Gates.Length;

    public void Feed(float x, float y, float t)
    {
        if (!has) { X = x; Y = y; T = t; has = true; return; }
        if (FinishT >= 0 || Cheat) return;
        float dt = t - T;
        if (dt < -0.001f || !float.IsFinite(x) || !float.IsFinite(y)) { Cheat = true; return; }
        float dx = x - X, dy = y - Y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > RegattaRules.MaxSpeed * MathF.Max(dt, 0) + 10) { Cheat = true; return; }
        // And over the whole heat, so positions stamped with one moment (or a few) can't add up to a course.
        Dist += d;
        if (Dist > RegattaRules.MaxSpeed * MathF.Max(t, 0) + 10) { Cheat = true; return; }
        // Did the straight line between the last two positions pass through the gate it is heading for?
        var (gx, gy) = RegattaRules.Gates[Next < Gates ? Next : 0];
        float k = 0;
        if (d > 0.001f) k = Math.Clamp(((gx - X) * dx + (gy - Y) * dy) / (d * d), 0, 1);
        float cx = X + dx * k, cy = Y + dy * k;
        if (MathF.Sqrt((cx - gx) * (cx - gx) + (cy - gy) * (cy - gy)) < RegattaRules.GateR)
        {
            float at = T + MathF.Max(dt, 0) * k;
            GateTimes.Add(at);
            if (Next < Gates) Next++; else FinishT = at;
        }
        X = x; Y = y; T = t;
    }
}

sealed class RegattaSession
{
    public const float SilentLimit = 6, HeartbeatSecs = 1;
    public readonly bool Host;
    public ILink Link;
    public readonly string MyName;
    public string RivalName = "";
    public string State = "joining";                    // joining, play, over, closed
    public string HeatPhase = "lineup";                 // lineup, count, race, result
    public string MatchId = "", RecordId = "", EndWhy = "", Notice = "";
    public int Heat, Games, Version;
    public float Count, HeatT, ResultT;
    public int MyWins, TheirWins;
    public readonly List<(float mine, float theirs)> Results = new();    // seconds per finished heat; -1 did not finish
    public readonly RegattaTracker Mine = new(), Theirs = new(), Sent = new();
    public bool PeerGone;
    public float TheirX, TheirY; public bool TheirSeen;
    public int MySeat => Host ? 0 : 1;
    bool myReady, theirReady, finSent, hasResult, iLeft, rematchMe, rematchThem;
    float myFin = -2, theirFin = -2;                     // -2 not yet, -1 did not finish, else seconds
    float hb, sinceTheirPos;
    int theirClaimMs = NoClaim;
    const int NoClaim = int.MinValue;
    bool forcedFinal;
    readonly List<NetMsg> earlyPos = new();               // their samples that came while my countdown was still running

    public RegattaSession(string myName, ILink link, bool host)
    {
        Host = host; MyName = SungkaSession.Clean(myName); Link = link;
        if (!host) Send(new NetMsg { t = "rhello", v = RegattaRules.Protocol, name = MyName });
    }

    void Send(NetMsg m) => Link?.Send(m);
    void Bump() => Version++;

    public string Result
    {
        get
        {
            if (State != "over" || !hasResult) return "";
            if (EndWhy == "left") return iLeft ? "loss" : "win";
            if (MyWins != TheirWins) return MyWins > TheirWins ? "win" : "loss";
            float a = Results.Sum(r => r.mine < 0 ? RegattaRules.HeatLimit : r.mine), b = Results.Sum(r => r.theirs < 0 ? RegattaRules.HeatLimit : r.theirs);
            return MathF.Abs(a - b) < 0.05f ? "draw" : a < b ? "win" : "loss";
        }
    }
    public float MyBest => Results.Where(r => r.mine >= 0).Select(r => r.mine).DefaultIfEmpty(-1).Min();

    /* ---------- The match and its heats ---------- */
    void BeginMatch()
    {
        MatchId = Guid.NewGuid().ToString("N"); RecordId = Guid.NewGuid().ToString("N");
        Games++;
        ResetMatch();
        State = "play";
        Send(new NetMsg { t = "rstart", id = MatchId, v = RegattaRules.Protocol, name = MyName });
        StartHeat(0);
    }

    void ResetMatch()
    {
        Heat = 0; MyWins = TheirWins = 0; Results.Clear(); hasResult = false; EndWhy = ""; Notice = ""; rematchMe = rematchThem = false;
    }

    void StartHeat(int heat)
    {
        Heat = heat; HeatPhase = "lineup"; myReady = theirReady = false; finSent = false; myFin = theirFin = -2; theirClaimMs = NoClaim; forcedFinal = false; sinceTheirPos = 0; earlyPos.Clear();
        HeatT = 0; Count = 0; TheirSeen = false;
        var s = RegattaRules.Slot(MySeat); Mine.Reset(s.x, s.y); Sent.Reset(s.x, s.y);
        var o = RegattaRules.Slot(1 - MySeat); Theirs.Reset(o.x, o.y); TheirX = o.x; TheirY = o.y;
        if (Host && heat > 0) Send(new NetMsg { t = "rheat", id = MatchId, n = new[] { heat } });
        Bump();
    }

    // My boat is on the line: say so. The host starts the countdown once both are.
    public void Ready()
    {
        if (State != "play" || HeatPhase != "lineup" || myReady) return;
        myReady = true;
        if (!Host) Send(new NetMsg { t = "rready", id = MatchId, n = new[] { Heat } });
        MaybeGo();
    }

    void MaybeGo()
    {
        if (!Host || !myReady || !theirReady || HeatPhase != "lineup") return;
        Send(new NetMsg { t = "rgo", id = MatchId, n = new[] { Heat } });
        HeatPhase = "count"; Count = RegattaRules.CountSecs;
        Bump();
    }

    // Called every frame in a race with where my boat is: it's tracked for my own screen, and eight times a second a sample goes
    // to the other side. The finish is judged on those samples, exactly as the numbers go out (`Sent`, the same tracker the
    // other side runs on them), so both screens work out the very same finish time and so the same winner.
    public void Position(float x, float y)
    {
        if (State != "play" || HeatPhase != "race") return;
        Mine.Feed(x, y, HeatT);
        if (finSent) return;
        // A sample every PosEvery, and one more the moment my own tracker sees the line crossed, so a finish between two samples
        // is not lost to the clock running out first.
        bool final = Mine.FinishT >= 0 && !forcedFinal;
        if (HeatT - lastPosSent < RegattaRules.PosEvery && !final) return;
        if (final) forcedFinal = true;
        lastPosSent = HeatT;
        int qx = (int)MathF.Round(x * 4), qy = (int)MathF.Round(y * 4), qt = (int)MathF.Round(HeatT * 1000);
        Send(new NetMsg { t = "rpos", id = MatchId, n = new[] { Heat, qx, qy, qt } });
        Sent.Feed(qx / 4f, qy / 4f, qt / 1000f);
        if (Sent.FinishT >= 0)
        {
            finSent = true;
            int finMs = (int)MathF.Round(Sent.FinishT * 1000);
            myFin = finMs / 1000f;                       // the very number sent
            Send(new NetMsg { t = "rfin", id = MatchId, n = new[] { Heat, finMs } });
            CheckHeat();
        }
    }
    float lastPosSent = -1;

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
                if (State == "closed" || was != State && State == "over") break;
            }
            hb += dt;
            if (hb >= HeartbeatSecs) { hb = 0; Send(new NetMsg { t = "hb" }); }
            if (!PeerGone && (!Link.Alive || Link.SilentFor > SilentLimit)) PeerLost();
        }
        if (State != "play") return;
        switch (HeatPhase)
        {
            case "count":
                Count -= dt;
                if (Count <= 0) { HeatPhase = "race"; HeatT = 0; lastPosSent = -1; sinceTheirPos = 0; forcedFinal = false; Mine.Reset(RegattaRules.Slot(MySeat).x, RegattaRules.Slot(MySeat).y); Sent.Reset(RegattaRules.Slot(MySeat).x, RegattaRules.Slot(MySeat).y); Theirs.Reset(RegattaRules.Slot(1 - MySeat).x, RegattaRules.Slot(1 - MySeat).y);
                    // Their samples that arrived during my countdown (their race can begin a moment, or after a hitch a good while, before mine).
                    var early = earlyPos.ToArray(); earlyPos.Clear();
                    foreach (var em in early) { Handle(em); if (State != "play") break; }
                    Bump();
                }
                break;
            case "race":
                HeatT += dt;
                if (HeatT >= RegattaRules.HeatLimit && myFin == -2) { myFin = -1; finSent = true; Send(new NetMsg { t = "rfin", id = MatchId, n = new[] { Heat, -1 } }); CheckHeat(); }
                // The other side has had its whole time and a few seconds more and still says nothing: their own clock has reached the
                // limit (a slow machine's clock runs behind mine, so mine alone proves nothing), or they have stopped sending, or the
                // heat has run twice as long as any heat can.
                else if (theirFin == -2 && (HeatT >= RegattaRules.HeatLimit * 2 || HeatT >= RegattaRules.HeatLimit + 8 && (Theirs.T >= RegattaRules.HeatLimit || sinceTheirPos > 8)))
                { theirFin = -1; CheckHeat(); }
                sinceTheirPos += dt;
                break;
            case "result":
                ResultT -= dt;
                if (ResultT <= 0)
                {
                    if (MyWins >= 2 || TheirWins >= 2 || Heat >= RegattaRules.Heats - 1) Finish("finished");
                    else if (Host) StartHeat(Heat + 1);          // (the guest waits to be told)
                    else ResultT = 0;
                }
                break;
        }
    }

    void Handle(NetMsg m)
    {
        switch (m.t)
        {
            case "rhello" when Host && State == "joining":
                if (m.v != RegattaRules.Protocol) { Send(new NetMsg { t = "bye", why = "version" }); Link.Close(); PeerGone = true; State = "closed"; Bump(); return; }
                RivalName = SungkaSession.Clean(m.name);
                BeginMatch();
                break;
            case "rstart" when !Host && (State == "joining" || State == "over" && rematchMe):
            {
                if (m.v != RegattaRules.Protocol || string.IsNullOrEmpty(m.id) || m.id.Length > 40 || m.id == MatchId) { Invalid("a bad start"); return; }
                RivalName = SungkaSession.Clean(m.name);
                MatchId = m.id; RecordId = Guid.NewGuid().ToString("N"); Games++;
                ResetMatch();
                State = "play";
                StartHeat(0);
                break;
            }
            case "rheat" when !Host && State == "play" && m.id == MatchId && HeatPhase == "result":
                if (m.n == null || m.n.Length != 1 || m.n[0] != Heat + 1) { Invalid("a heat out of order"); return; }
                // No heat after the match is decided: two wins, or the last of the three.
                if (MyWins >= 2 || TheirWins >= 2 || Heat >= RegattaRules.Heats - 1) { Invalid("a heat after the match was decided"); return; }
                StartHeat(m.n[0]);
                break;
            // The host has moved on to the next heat while I am still in this one (it gave up waiting for me): nobody can be
            // left stranded between the two, so the race ends with no result.
            case "rheat" when !Host && State == "play" && m.id == MatchId && HeatPhase != "result":
                Invalid("the next heat started before this one was over");
                return;
            case "rready" when Host && State == "play" && m.id == MatchId && HeatPhase == "lineup":
                if (m.n == null || m.n.Length != 1 || m.n[0] != Heat) return;
                theirReady = true;
                MaybeGo();
                break;
            case "rgo" when !Host && State == "play" && m.id == MatchId && HeatPhase == "lineup" && myReady:
                if (m.n == null || m.n.Length != 1 || m.n[0] != Heat) return;
                HeatPhase = "count"; Count = RegattaRules.CountSecs;
                Bump();
                break;
            case "rpos" when State == "play" && m.id == MatchId && HeatPhase == "count":
                // (their race began before mine: keep what they sent, to be read once mine has started)
                if (m.n != null && m.n.Length == 4 && m.n[0] == Heat && earlyPos.Count < 512) earlyPos.Add(m);
                break;
            case "rpos" when State == "play" && m.id == MatchId && HeatPhase == "race":
            {
                if (m.n == null || m.n.Length != 4 || m.n[0] != Heat) return;
                float t = m.n[3] / 1000f;
                // Their clock can't run ahead of mine by more than a slow link explains, nor go backwards.
                if (t > HeatT + RegattaRules.FutureSlack || t < Theirs.T - 0.001f) { Invalid("an impossible position"); return; }
                Theirs.Feed(m.n[1] / 4f, m.n[2] / 4f, t);
                if (Theirs.Cheat) { Invalid("an impossible position"); return; }
                TheirX = Theirs.X; TheirY = Theirs.Y; TheirSeen = true;
                sinceTheirPos = 0;
                if (theirClaimMs != NoClaim) VerifyClaim();
                break;
            }
            case "rfin" when State == "play" && m.id == MatchId && HeatPhase is "race" or "result" && theirFin == -2:
            {
                if (m.n == null || m.n.Length != 2 || m.n[0] != Heat) return;
                if (m.n[1] == -1) { theirFin = -1; CheckHeat(); break; }
                // A finish is somewhere in the heat's own time: nothing negative, and nothing after the limit (a boat still out there
                // then has not finished; the honest side sends -1). Also keeps the comparison below clear of overflow.
                if (m.n[1] < 0 || m.n[1] > (int)(RegattaRules.HeatLimit * 1000)) { Invalid("a finish time outside the heat"); return; }
                theirClaimMs = m.n[1];
                VerifyClaim();
                break;
            }
            case "rrematch" when State == "over" && EndWhy == "finished":
                rematchThem = true;
                if (Host && rematchMe) BeginMatch();
                break;
            case "bye":
                PeerLeft(m.why);
                break;
        }
    }

    // Their claimed finish has to be the finish their own positions showed (within a moment). The heat is then judged on the
    // claim itself, the very millisecond count they sent and I sent them, so both screens compare the same two numbers and
    // can't each decide they were the quicker (judging each from its own samples did).
    void VerifyClaim()
    {
        if (Theirs.FinishT < 0) return;          // (the positions up to the line may still be on their way)
        if (Math.Abs(theirClaimMs - (int)MathF.Round(Theirs.FinishT * 1000)) > ClaimToleranceMs) { Invalid("a finish time the positions don't show"); return; }
        theirFin = theirClaimMs / 1000f; theirClaimMs = NoClaim;
        CheckHeat();
    }
    const int ClaimToleranceMs = 1;       // the same samples give the same time to the millisecond; one is for rounding

    // Both finishes known: the heat is decided.
    void CheckHeat()
    {
        if (HeatPhase != "race" || myFin == -2 || theirFin == -2) return;
        Results.Add((myFin, theirFin));
        bool iWin = myFin >= 0 && (theirFin < 0 || myFin < theirFin), theyWin = theirFin >= 0 && (myFin < 0 || theirFin < myFin);
        if (iWin) MyWins++; else if (theyWin) TheirWins++;
        HeatPhase = "result"; ResultT = RegattaRules.BreakSecs;
        Bump();
    }

    void Finish(string why)
    {
        if (State == "over") return;
        State = "over"; EndWhy = why; hasResult = true;
        Bump();
    }

    void Invalid(string why)
    {
        Send(new NetMsg { t = "bye", why = "invalid" });
        State = "over"; EndWhy = "invalid"; hasResult = false; PeerGone = true; Notice = $"The race was stopped ({why}).";
        Link?.Close();
        Bump();
    }

    void PeerLeft(string why)
    {
        if (State == "play" && why is "invalid" or "version") { State = "over"; EndWhy = "invalid"; hasResult = false; Notice = "The race was stopped."; }
        else if (State == "play") { State = "over"; EndWhy = "left"; hasResult = true; Notice = $"{RivalName} left the race."; }
        else if (State == "joining") { State = "over"; EndWhy = "left"; hasResult = false; Notice = "The other player left before the race began."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    // The line has gone quiet or dropped. Each side sees the other vanish, so neither can know who it was: nobody wins.
    void PeerLost()
    {
        if (State == "play") { State = "over"; EndWhy = "lost"; hasResult = false; Notice = "The connection was lost. The race has no result."; }
        else if (State == "joining") { State = "over"; EndWhy = "lost"; hasResult = false; Notice = "The other player didn't answer."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    public void Rematch()
    {
        if (State != "over" || PeerGone || EndWhy != "finished" || rematchMe) return;
        rematchMe = true;
        if (Host) { if (rematchThem) BeginMatch(); } else Send(new NetMsg { t = "rrematch", id = MatchId });
        Bump();
    }
    public bool WaitingForRematch => rematchMe && State == "over";

    public void Leave()
    {
        if (State == "closed") return;
        bool inGame = State == "play";
        iLeft = true;
        Send(new NetMsg { t = "bye", why = "left" });
        Link?.Close();
        Link = null;
        if (inGame) { State = "over"; EndWhy = "left"; hasResult = true; }
        else if (State != "over") State = "closed";
        PeerGone = true;
        Bump();
    }

    public void Close() { Link?.Close(); Link = null; State = "closed"; }
}

// The other racer when you race alone: a RegattaSession on the other end of an in-memory link that sails a path along the
// course at a steady speed, so the race against it is the same code as a race against a person.
sealed class RegattaBot
{
    public readonly RegattaSession S;
    readonly List<(float x, float y)> path;
    readonly float[] cum;
    readonly float speed;
    readonly int seed;
    float readyIn = -1, overT;
    int readyHeat = -1;
    public float X, Y;

    public RegattaBot(string name, ILink link, List<(float x, float y)> path, float speed, int seed)
    {
        S = new RegattaSession(name, link, host: false);
        this.path = path; this.speed = speed; this.seed = seed;
        cum = new float[path.Count];
        for (int i = 1; i < path.Count; i++) cum[i] = cum[i - 1] + MathF.Sqrt((path[i].x - path[i - 1].x) * (path[i].x - path[i - 1].x) + (path[i].y - path[i - 1].y) * (path[i].y - path[i - 1].y));
        X = path[0].x; Y = path[0].y;
    }

    public float Length => cum[^1];

    // A little different each heat: a steadier or a slower start, so no two heats are the same.
    float Pace(int heat) => speed * (0.95f + 0.1f * (float)new MatchRng(MatchRules.Mix(seed, heat, 5)).NextDouble());

    public void Tick(float dt)
    {
        S.Tick(dt);
        if (S.State == "over" && S.EndWhy == "finished" && !S.PeerGone) { overT += dt; if (overT > 1.2f) { readyHeat = -1; S.Rematch(); } }   // the angler is always up for another
        else overT = 0;
        if (S.State != "play") return;
        if (S.HeatPhase == "lineup" && readyHeat != S.Heat)
        {
            if (readyIn < 0) readyIn = 0.8f;
            readyIn -= dt;
            if (readyIn <= 0) { S.Ready(); readyHeat = S.Heat; readyIn = -1; X = path[0].x; Y = path[0].y; }
        }
        if (S.HeatPhase == "race")
        {
            float d = Math.Min(Pace(S.Heat) * S.HeatT, Length);
            int i = 1;
            while (i < path.Count - 1 && cum[i] < d) i++;
            float seg = cum[i] - cum[i - 1], k = seg > 0.001f ? Math.Clamp((d - cum[i - 1]) / seg, 0, 1) : 1;
            X = path[i - 1].x + (path[i].x - path[i - 1].x) * k; Y = path[i - 1].y + (path[i].y - path[i - 1].y) * k;
            S.Position(X, Y);
        }
    }
}
