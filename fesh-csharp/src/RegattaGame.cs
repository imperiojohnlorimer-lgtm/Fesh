using Raylib_cs;

namespace Fesh;

// The head-to-head Regatta in the game (1.30): started from the Tournament Dock by a challenge (or by talking to one of the
// three anglers, who race in a boat of their own). Best of three heats round Dado's course at Daang Pulo, both boats on
// the water at once so each racer sees the other; the referee and tracker are RegattaNet.cs, and the angler on the other
// side is a RegattaBot over an in-memory link, so racing alone is the same code as racing a person. Your boat is the plain
// banca in fair weather whatever you own (a fresh set of belongings, like every match), and the wind is the amihan's.
partial class Game
{
    RegattaSession regSession;
    RegattaBot regBot;
    string regPeerName = "", regRivalId = "";
    Look regPeerLook;
    int regCh, regPlacedHeat = -1;
    bool regRecorded;
    internal float regDt;
    float regRX, regRY, regFaceT;
    string regFace = "up";
    bool regMoving;
    string regBanner = "", regMatchId = ""; float regBannerT;

    bool InRegatta => MatchActive && match.Phase == "regatta" && regSession != null;

    // The angler boats' looks (Look indexes into the creator's lists).
    static Look AnglerLook(string id) => id switch
    {
        "bess" => new Look { name = "Old Bess", skin = 2, hair = 1, hairColor = 5, hat = 1, shirt = 4, pants = 3 },
        "marlo" => new Look { name = "Marlo", skin = 3, hair = 0, hairColor = 0, hat = 3, shirt = 1, pants = 2 },
        _ => new Look { name = "Captain Ivy", skin = 2, hair = 1, hairColor = 1, hat = 3, shirt = 0, pants = 2 }
    };
    static float AnglerSpeed(string id) => id switch { "bess" => 72, "marlo" => 82, _ => 90 };

    /* ---------- Starting ---------- */
    // Against one of the anglers on the dock.
    void StartDockRegattaAi(string rivalId)
    {
        if (!InDock) return;
        var sk = MatchRules.RivalById(rivalId);
        regRivalId = rivalId; regPeerName = sk.Name; regPeerLook = AnglerLook(rivalId);
        var (a, b) = MemoryLink.Pair();
        regSession = new RegattaSession(LanName, a, host: true);
        regCh = 0;
        int g = ++match.Gen;
        match.Phase = "ready";
        var spec = new MatchSpec { Mode = "regatta", Seed = NewMatchSeed(), Rival = rivalId, Label = $"Regatta · against {sk.Name}", Duration = 0 };
        FadeThrough(() =>
        {
            if (match == null || match.Gen != g) return;
            EnterRegattaChild(spec);
            var path = RegattaRoute(1);
            regBot = new RegattaBot(sk.Name, b, path, AnglerSpeed(rivalId), spec.Seed);
        }, () => { if (match == null || match.Gen != g) return; match.Phase = "regatta"; regPlacedHeat = -1; });
    }

    // Against someone on the dock, by their challenge.
    void StartDockRegatta(DockEvent ev)
    {
        dockPending = 0; dockInvite = null; dockChallengeTarget = -1;
        if (!InDock || ev.Link == null) { dockClient?.Done(ev.Ch); return; }
        var peer = dockClient.Roster.FirstOrDefault(p => p.Id == ev.From);
        regPeerName = peer?.Name ?? "Someone"; regPeerLook = peer != null ? RemoteLook(peer) : new Look(); regRivalId = "net";
        regSession = new RegattaSession(LanName, ev.Link, host: ev.Role == 0);
        regCh = ev.Ch; regBot = null;
        var spec = new MatchSpec { Mode = "regatta", Seed = 0, Rival = "net", Label = $"Regatta · against {regPeerName}", Duration = 0 };
        int g = ++match.Gen;
        match.Phase = "ready";
        FadeThrough(() => { if (match != null && match.Gen == g) EnterRegattaChild(spec); },
            () => { if (match == null || match.Gen != g) return; match.Phase = "regatta"; regPlacedHeat = -1; });
    }

    void EnterRegattaChild(MatchSpec spec)
    {
        match.FromDock = true;
        ResetMatchCounters(spec);
        // A boat and nothing else, in fair weather: the same for both.
        state.inv["boat"] = 1;
        state.aboard = true;
        ResetMatchPlace();
        var s = RegattaRules.Slot(regSession?.MySeat ?? 0);
        player.X = s.x; player.Y = s.y; player.Face = "up"; boatFace = "up"; sailFurl = 0;
        state.boatX = s.x; state.boatY = s.y;
        regRecorded = false; regBanner = ""; regPlacedHeat = -1;
        var o = RegattaRules.Slot(1 - (regSession?.MySeat ?? 0)); regRX = o.x; regRY = o.y; regFace = "up"; regMoving = false;
    }

    // The way a boat can sail from the start to each gate in turn and home: the shortest route through the water, on a 5 px
    // grid, joined up (the anglers sail it). From the given seat's place.
    List<(float x, float y)> RegattaRoute(int seat)
    {
        var s = RegattaRules.Slot(seat);
        var route = new List<(float x, float y)> { (s.x, s.y) };
        var from = (x: s.x, y: s.y);
        for (int g = 1; g <= RegattaRules.Gates.Length; g++)
        {
            var to = RegattaRules.Gates[g % RegattaRules.Gates.Length];
            var leg = BoatRoute(from.x, from.y, to.x, to.y);
            if (leg.Count == 0) leg.Add(to);
            route.AddRange(leg);
            from = to;
        }
        return route;
    }

    List<(float x, float y)> BoatRoute(float fx, float fy, float tx, float ty)
    {
        const int S = 5;
        var start = ((int)MathF.Round(fx / S), (int)MathF.Round(fy / S)); var goal = ((int)MathF.Round(tx / S), (int)MathF.Round(ty / S));
        var dist = new Dictionary<(int, int), float> { [start] = 0 };
        var prev = new Dictionary<(int, int), (int, int)>();
        var pq = new PriorityQueue<(int x, int y), float>();
        pq.Enqueue(start, 0);
        int minX = Math.Min(start.Item1, goal.Item1) - 40, maxX = Math.Max(start.Item1, goal.Item1) + 40, minY = Math.Min(start.Item2, goal.Item2) - 40, maxY = Math.Max(start.Item2, goal.Item2) + 40;
        while (pq.TryDequeue(out var cur, out float d))
        {
            if (cur == goal) break;
            if (d > dist[cur]) continue;
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var n = (cur.Item1 + ox, cur.Item2 + oy);
                if (n.Item1 < minX || n.Item1 > maxX || n.Item2 < minY || n.Item2 > maxY) continue;
                float nd = d + (ox != 0 && oy != 0 ? 1.414f : 1);
                if (nd >= dist.GetValueOrDefault(n, float.MaxValue) || !BoatCanStand(n.Item1 * S, n.Item2 * S)) continue;
                dist[n] = nd; prev[n] = cur; pq.Enqueue(n, nd);
            }
        }
        var path = new List<(float x, float y)>();
        if (!dist.ContainsKey(goal)) return path;
        for (var p = goal; p != start; p = prev[p]) path.Add((p.Item1 * S, p.Item2 * S));
        path.Reverse();
        path.Add((tx, ty));
        return path;
    }

    /* ---------- Every frame of the race ---------- */
    void UpdateRegattaDuel(float dt)
    {
        regDt = dt;
        var s = regSession;
        regBot?.Tick(dt);
        s.Tick(dt);
        if (regMatchId != s.MatchId) { regMatchId = s.MatchId; regPlacedHeat = -1; regRecorded = false; }
        if (regBannerT > 0) regBannerT -= dt;
        player.Moving = false;
        // The other boat glides toward where it was last seen, turning to face the way it goes.
        if (s.TheirSeen)
        {
            float ddx = s.TheirX - regRX, ddy = s.TheirY - regRY, k = MathF.Min(1, dt * 12);
            regRX += ddx * k; regRY += ddy * k;
            regMoving = MathF.Abs(ddx) + MathF.Abs(ddy) > 0.5f;
            if (regMoving) regFace = MathF.Abs(ddx) > MathF.Abs(ddy) ? (ddx > 0 ? "right" : "left") : (ddy > 0 ? "down" : "up");
        }
        else regMoving = false;
        if (s.State == "over") { FinishRegatta(); return; }
        switch (s.HeatPhase)
        {
            case "lineup":
                if (regPlacedHeat != s.Heat)
                {
                    var slot = RegattaRules.Slot(s.MySeat);
                    player.X = state.boatX = slot.x; player.Y = state.boatY = slot.y; player.Face = boatFace = "up";
                    var o = RegattaRules.Slot(1 - s.MySeat); regRX = o.x; regRY = o.y; regFace = "up"; regMoving = false;
                    regPlacedHeat = s.Heat;
                    regBanner = $"Heat {s.Heat + 1} of {RegattaRules.Heats}"; regBannerT = 3;
                    s.Ready();
                }
                break;
            case "race":
                if (s.HeatT > 0) SailRegattaBoat(dt);        // (the frame the race begins is time 0: the boat stays on the line)
                s.Position(player.X, player.Y);
                break;
            case "result":
                if (regBannerT <= 0 && s.Results.Count > 0)
                {
                    var (me, them) = s.Results[^1];
                    bool won = me >= 0 && (them < 0 || me < them);
                    regBanner = me < 0 && them < 0 ? "Neither of you finished" : won ? $"You win the heat: {me:0.0} s" + (them >= 0 ? $" to {them:0.0} s" : "") : $"{regPeerName} wins the heat: {them:0.0} s" + (me >= 0 ? $" to {me:0.0} s" : "");
                    regBannerT = RegattaRules.BreakSecs;
                    Sfx.Play(won ? "coin" : "fail");
                }
                break;
        }
    }

    // The boat's movement is the real one (speed, wind, water under the hull), without anything else of the game.
    void SailRegattaBoat(float dt)
    {
        float dx = 0, dy = 0;
        if (Bind.Down("left")) dx -= 1;
        if (Bind.Down("right")) dx += 1;
        if (Bind.Down("up")) dy -= 1;
        if (Bind.Down("down")) dy += 1;
        float push = 1;
        if (Inp.Stick != System.Numerics.Vector2.Zero)
        {
            (dx, dy) = (Inp.Stick.X, Inp.Stick.Y);
            push = Math.Clamp(0.35f + 0.65f * (Inp.Stick.Length() - 0.35f) / 0.5f, 0.35f, 1);
        }
        player.Moving = dx != 0 || dy != 0;
        if (!player.Moving) return;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        float speed = BoatSpeed * WindFactor(dx / len, dy / len) * push * dt;
        float mx = dx / len * speed, my = dy / len * speed;
        if (BoatCanStand(player.X + mx, player.Y)) player.X += mx;
        if (BoatCanStand(player.X, player.Y + my)) player.Y += my;
        player.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
        boatFace = player.Face;
        state.boatX = player.X; state.boatY = player.Y;
    }

    // The match is over (finished, or the other side left or broke the rules): the result, recorded once.
    void FinishRegatta()
    {
        if (regRecorded) return;
        regRecorded = true;
        var s = regSession;
        string r = s.Result;
        if (r != "")
        {
            bool left = s.EndWhy == "left" && r == "loss";
            Leaderboards.Add(new BoardEntry
            {
                id = s.RecordId, mode = "regatta", label = $"Regatta · against {regPeerName}", seed = 0, ruleset = MatchRules.Version, rival = regPeerName,
                result = left ? "forfeit" : r, date = DateTime.Now.ToString("o"), score = s.MyBest >= 0 ? (int)MathF.Round(s.MyBest * 1000) : 0, perfects = 0,
                catches = s.TheirWins, casts = s.MyWins
            });
        }
        Sfx.Play(r == "win" ? "rare" : r == "loss" ? "fail" : "coin");
        SetPrompt("");
    }

    void ShutRegatta()
    {
        if (regSession != null && regSession.State == "over" && !regRecorded) FinishRegatta();      // (a result reached but not yet written down)
        if (regSession != null && regSession.State == "play" && !regRecorded) { regSession.Leave(); FinishRegatta(); }
        regSession?.Leave();
        regSession = null; regBot = null;
        if (regCh != 0) dockClient?.Done(regCh);
        regCh = 0; regPlacedHeat = -1;
        state.aboard = false;
    }

    /* ---------- Drawing ---------- */
    void AddRegattaObjects(List<(float y, Action draw)> list)
    {
        if (!InRegatta || match.Phase != "regatta") return;
        list.Add((regRY + 2, () => DrawRemoteBoat(regRX, regRY, regFace, regMoving, regPeerLook ?? new Look())));
    }

    // Someone else's boat, with them in it: the game's own boat drawing, told whose it is for a moment.
    void DrawRemoteBoat(float x, float y, string face, bool moving, Look look)
    {
        var kf = boatFace; var kl = state.look; var kPf = player.Face; var km = player.Moving; var kfurl = sailFurl;
        boatFace = face; state.look = look; player.Face = face; player.Moving = moving; sailFurl = 0;
        try { DrawBoat(x, y, time, occupied: true); }
        finally { boatFace = kf; state.look = kl; player.Face = kPf; player.Moving = km; sailFurl = kfurl; }
    }

    // The next buoy to flash for you during a heat.
    int RegattaNextGate => InRegatta && regSession.HeatPhase is "count" or "race" ? (regSession.Mine.Next < RegattaRules.Gates.Length ? regSession.Mine.Next : 0) : -1;

    static string HeatTime(float s) => s < 0 ? "no finish" : $"{(int)s / 60}:{s % 60:00.0}";

    void DrawRegattaHud()
    {
        var s = regSession;
        float w = 640, h = 112, x = (Gfx.LW - w) / 2, y = 14;
        Gfx.Box(x, y, w, h, NavyStrong, Pal.Ink, 2, 8, 3);
        bool racing = s.HeatPhase == "race";
        string clock = racing ? HeatTime(s.HeatT) : s.HeatPhase == "count" ? "Ready" : s.HeatPhase == "result" ? "Heat over" : "Line up";
        Gfx.TextCenter(clock, x + w / 2, y + 8, FontKind.Ui700, 34, White);
        Gfx.TextCenter($"Regatta · heat {Math.Min(s.Heat + 1, RegattaRules.Heats)} of {RegattaRules.Heats}", x + w / 2, y + 52, FontKind.Ui600, 16, Pal.C("#bcd3e0"));
        void Side(float sx, bool right, string name, int wins, RegattaTracker tr)
        {
            string wl = $"{wins} heat{(wins == 1 ? "" : "s")}";
            Gfx.Text(name, right ? sx - Gfx.Measure(name, FontKind.Ui600, 17) : sx, y + 10, FontKind.Ui600, 17, Pal.C("#bcd3e0"));
            Gfx.Text(wl, right ? sx - Gfx.Measure(wl, FontKind.Ui700, 26) : sx, y + 30, FontKind.Ui700, 26, White);
            // Gates passed, as a row of little squares.
            for (int i = 0; i < RegattaRules.Gates.Length; i++)
            {
                bool done = tr.FinishT >= 0 || i < tr.Next - 1 + (tr.Next >= 1 ? 0 : 0) && i < tr.GateTimes.Count;
                float bx = right ? sx - (RegattaRules.Gates.Length - i) * 13 : sx + i * 13;
                Gfx.Rect(bx, y + 78, 10, 10, done ? Pal.Lantern : Pal.C("rgba(255,255,255,0.2)"), 2);
            }
        }
        Side(x + 22, false, "You", s.MyWins, s.Mine);
        Side(x + w - 22, true, regPeerName, s.TheirWins, s.Theirs);
        // Whose boat is where, in the big picture: names over the boats.
        var me = ScreenOf(player.X, player.Y - 26); var they = ScreenOf(regRX, regRY - 26);
        float tw = Gfx.Measure(regPeerName, FontKind.Ui600, 16) + 16;
        Gfx.Rect(they.X - tw / 2, they.Y - 12, tw, 24, NavyStrong, 5);
        Gfx.TextCenter(regPeerName, they.X, they.Y - 9, FontKind.Ui600, 16, Pal.Paper);
        if (s.HeatPhase == "count")
        {
            int n = (int)MathF.Ceiling(s.Count);
            Gfx.TextCenter(n.ToString(), Gfx.LW / 2 + 4, 244, FontKind.Ui700, 150, Pal.C("rgba(6,14,24,0.55)"));
            Gfx.TextCenter(n.ToString(), Gfx.LW / 2, 240, FontKind.Ui700, 150, Pal.WithAlpha(White, 0.95f));
        }
        else if (racing && s.HeatT < 1.2f)
            Gfx.TextCenter("Sail!", Gfx.LW / 2, 250, FontKind.Ui700, 120, Pal.WithAlpha(Pal.Lantern, Math.Clamp((1.2f - s.HeatT) / 0.6f, 0, 1)));
        if (regBannerT > 0 && regBanner != "")
        {
            float bw = Gfx.Measure(regBanner, FontKind.Ui700, 26) + 50;
            Gfx.Box((Gfx.LW - bw) / 2, 150, bw, 52, Pal.Paper, Pal.Ink, 3, 8, 4);
            Gfx.TextCenter(regBanner, Gfx.LW / 2, 162, FontKind.Ui700, 26, Pal.PaperInk);
        }
        if (s.State == "over") DrawRegattaResult();
    }

    void DrawRegattaResult()
    {
        var s = regSession;
        Backdrop();
        const float w = 820, h = 540;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        PanelBox(x, y, w, h);
        string r = s.Result;
        string title = r == "win" ? "You win the regatta!" : r == "loss" ? $"{regPeerName} wins" : r == "draw" ? "A draw!" : "No result";
        var tone = r == "win" ? Pal.C("#2f7d3a") : r == "loss" ? Rust : Pal.PaperInk;
        Gfx.TextCenter(title, x + w / 2, y + 22, FontKind.Ui700, 44, tone);
        string sub = r == "" ? s.Notice : s.EndWhy == "left" ? s.Notice : $"{s.MyWins} heat{(s.MyWins == 1 ? "" : "s")} to {s.TheirWins}";
        Gfx.TextCenter(sub, x + w / 2, y + 80, FontKind.Ui600, 20, Muted);
        float ty = y + 130;
        Gfx.Text("Heat", x + 40, ty, FontKind.Ui700, 18, Muted);
        Gfx.Text("You", x + 220, ty, FontKind.Ui700, 18, Muted);
        Gfx.Text(regPeerName, x + 460, ty, FontKind.Ui700, 18, Muted);
        for (int i = 0; i < s.Results.Count; i++)
        {
            var (me, them) = s.Results[i];
            float ry = ty + 34 + i * 40;
            bool won = me >= 0 && (them < 0 || me < them), lost = them >= 0 && (me < 0 || them < me);
            Gfx.Rect(x + 24, ry - 6, w - 48, 36, i % 2 == 0 ? Pal.C("rgba(16,36,58,0.06)") : Color.Blank, 5);
            Gfx.Text($"{i + 1}", x + 40, ry, FontKind.Ui700, 22, Pal.PaperInk);
            Gfx.Text(HeatTime(me), x + 220, ry, FontKind.Ui700, 22, won ? Pal.C("#2f7d3a") : Pal.PaperInk);
            Gfx.Text(HeatTime(them), x + 460, ry, FontKind.Ui700, 22, lost ? Rust : Pal.PaperInk);
        }
        if (s.MyBest >= 0) Gfx.Text($"Your fastest heat: {HeatTime(s.MyBest)}", x + 40, y + h - 150, FontKind.Ui600, 20, Pal.C("#9a6a1a"));
        float by = y + h - 84, bx = x + 30;
        bool can = !s.PeerGone && s.EndWhy == "finished" && !s.WaitingForRematch;
        string again = s.WaitingForRematch ? $"Waiting for {regPeerName}..." : "Rematch";
        if (Button(again, bx, by, BigW(again), 56, FontKind.Ui700, 24, can ? Pal.Buoy : Pal.C("#d8cbb0"), can ? White : Muted, 5, 3, 6, can)) { s.Rematch(); return; }
        bx += BigW(again) + 14;
        if (BigButton("Back to the dock", bx, by, false)) { LeaveMatch(); return; }
        if (BigButton("Leave", x + w - 30 - BigW("Leave"), by, false)) { LeaveDockFromResult(); return; }
    }

    // After a rematch starts, the match begins again from heat one.
    void RegattaKeys()
    {
        var s = regSession;
        if (s.State == "over") { if (Inp.Pressed(KeyboardKey.Enter)) s.Rematch(); else if (BackPressed()) LeaveMatch(); return; }
        if (BackPressed()) match.LeaveAsk = true;
    }
}
