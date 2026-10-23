using Raylib_cs;

namespace Fesh;

// What a match is, and its rules (MatchCore.cs): the mode, the seed both sides fish, how long, and who the rival is.
sealed class MatchSpec
{
    public string Id = Guid.NewGuid().ToString("N");   // recorded once on the leaderboard
    public string Mode = "derby";                       // "derby" (against an AI rival) or "daily" (the day's seeded run)
    public int Seed;
    public float Duration = MatchRules.DerbySecs;
    public string Rival;                                // a MatchRules.Rivals id, "best" (your best run of this seed) or null
    public string Label = "";
    public string Day;                                  // the daily challenge's date (MatchRules.DailyLabel): only a real one counts for the streak and its prizes
}

sealed class MatchOutcome
{
    public string Result = "solo";                      // win, loss, draw, solo or forfeit
    public string Title = "", Reason = "", RivalName;
    public MatchScore Me, Rival;
    public bool NewBest;
    public int Rank;                                    // among your best runs of this mode
    public string DailyLine = "";                       // the daily challenge: your streak and what it pays (1.45.1)
    public string Prize = "", PrizeNote = "";           // Pip's prize for beating a rival, and why there isn't one
}

// A match in progress. While it lasts the game's `state` is a fresh tournament one (the old Old rod, nothing else, noon,
// fair weather) and the real one waits in `Real`, untouched; Save does nothing. Everything the match changes lives here.
sealed class MatchRun
{
    public MatchSpec Spec;
    public string Phase = "ready";                      // ready (fading in), countdown, run, done
    public float Count = MatchRules.CountdownSecs, T;    // the countdown left; seconds of fishing so far
    public MatchScore Me;
    public readonly List<MatchEvent> Log = new();
    public int Casts, Lost;
    public int RivalEvt;                                // how many of the rival's landings have been announced
    public bool Dock, FromDock, BoardOpen;               // a visit to the Tournament Dock (Dock.cs); the match now on started from it; its notice board is open
    public int Gen;                                     // bumped by every change of what the sandbox is doing, so a late fade callback can tell it is stale
    public string Emote = ""; public float EmoteT;       // your last quick phrase, while it is shown
    public List<MatchEvent> RivalRun;
    public string RivalName;
    public bool LeaveAsk;
    public float ReelAcc;
    public MatchOutcome Result;
    // Where everything was before, to put back exactly.
    public State Real;
    public string RealScene, RealFace;
    public float RealX, RealY;
    public Random RealRng;
    public double RealPlay;
    public readonly HashSet<string> Won = new();         // rivals beaten this time that Pip pays for when you are back
    public string DailyToast;                           // what the daily challenge paid into the real save, told when you are back (DailyStreak.cs)
}

partial class Game
{
    MatchRun match;
    bool MatchActive => match != null;
    const float MatchStep = 1f / 60;                    // the reel fight runs at a fixed step in a match, so every frame rate fights alike
    const float MatchMaxDt = 0.25f;                    // one frame never moves a match on by more than this, whatever the hitch
    // Where a Derby Duel is fished: Saltmere's lagoon, from the same spot.
    const float MatchX = 100, MatchY = 98;
    const string MatchSpot = "lagoon";

    /* ---------- Starting and leaving ---------- */
    // Why a match can't start right now, or null. The same test runs again when the fade is done.
    string MatchBlockReason(bool fading = false)
    {
        if (MatchActive) return "A match is already on.";
        if (!fading && mode != "play" && mode != "panel" && mode != "pause") return "Finish what you're doing first.";
        if (scene != "world") return "Go outside to start a match.";
        if (Aboard || Riding || trolling || towing || race != null || raceArmed) return "Step off your boat or mount first.";
        if (boss != null || guardian != null || eclipse != null || tremor != null || SeaEmergency || VolcanoControlled || DerbyOn || call != null)
            return "Not now: something is going on.";
        if (fish != null || reel != null || dlg != null) return "Finish fishing first.";
        return null;
    }

    // A fresh set of belongings for the tournament: the Old rod and nothing else, no skills, midday, fair weather.
    State MatchState(Look look)
    {
        var s = new State { created = true, look = look, day = 1, clock = 12 * 60, weather = "clear", scene = "world", px = MatchX, py = MatchY };
        s.inv = new() { ["rod_old"] = 1 };
        s.forecast = new() { new WeatherSpell { at = 0, w = "clear" } };
        s.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        s.charted = new();
        s.flags.metTomas = true;
        s.hinted["powerCast"] = true;                   // no first-time hint over the match
        s.hinted["usedBait"] = true;
        return s;
    }

    void BeginMatch(MatchSpec spec)
    {
        if (MatchBlockReason() is string why) { Sfx.Play("nope"); Toast(why); return; }
        if (mode != "play") { if (mode == "panel") ClosePanels(); else { ClosePause(); } }
        if (mode != "play") return;
        FadeThrough(() => EnterMatch(spec), () =>
        {
            if (match == null) return;
            if (spec.Mode == "dock") { match.Phase = "dock"; Sfx.Play("coin"); return; }
            match.Phase = "countdown";
            match.Count = MatchRules.CountdownSecs;
            Sfx.Play("blip");
        });
    }

    void EnterMatch(MatchSpec spec)
    {
        if (MatchBlockReason(fading: true) is string)
        {
            // Something started while the screen was going dark: stay as we were.
            return;
        }
        var m = new MatchRun
        {
            Spec = spec, Real = state, RealScene = scene, RealX = player.X, RealY = player.Y, RealFace = player.Face, RealRng = rng,
            RealPlay = state.playSecs, RivalName = null
        };
        (m.RivalRun, m.RivalName) = MatchRival(spec);
        match = m;
        state = MatchState(m.Real.look);
        EnsureHotbar();
        rng = new Random(MatchRules.Mix(MatchRules.Version, spec.Seed, 9));
        ResetMatchPlace();
        if (spec.Mode == "dock") { m.Dock = true; stampDock = true; BuildMap(); PlaceAtDock(); }
    }

    // The player at the lagoon with nothing in the air and nothing left over from before.
    void ResetMatchPlace()
    {
        fish = null; reel = null; pointerHold = false; chargeSpot = null; charge = 0; landed = null; releaseFx = null;
        heldItem = null; heldT = 0; toastTimer = 0; target = null;
        floaters.Clear();
        player.X = MatchX; player.Y = MatchY; player.Face = "up"; player.Moving = false;
        state.weather = "clear"; planned = "clear"; SnapWeather();
        SetPrompt("");
    }

    // Who the rival is and what they land: an AI angler, or your best run on this seed.
    (List<MatchEvent> run, string name) MatchRival(MatchSpec spec)
    {
        if (spec.Rival == "best")
        {
            var best = Leaderboards.BestOn(spec.Mode, spec.Seed);
            return best?.log == null ? (null, null) : (best.log, "Your best");
        }
        if (spec.Rival == "net") return (netDerby?.Theirs ?? new List<MatchEvent>(), netPeerName);
        if (spec.Rival == null) return (null, null);
        var sk = MatchRules.RivalById(spec.Rival);
        return (MatchRules.GhostRun(spec.Seed, sk, spec.Duration), sk.Name);
    }

    // Puts everything back (state, place, the adventure's random numbers) once, with the screen black.
    void RestoreFromMatch()
    {
        ShutReef();
        ShutQuiz();
        ShutRegatta();
        ShutNetDerby();
        ShutDockNet();
        var m = match;
        if (m == null) return;
        match = null;
        double fished = state.playSecs - 0;
        state = m.Real;
        state.playSecs = m.RealPlay + Math.Min(fished, 3600);
        if (stampDock) { stampDock = false; BuildMap(); }
        PayDuelPrizes(m);
        PayDailyPrizes(m);
        rng = m.RealRng;
        fish = null; reel = null; pointerHold = false; chargeSpot = null; charge = 0; heldItem = null; heldT = 0; landed = null;
        floaters.Clear();
        player.X = m.RealX; player.Y = m.RealY; player.Face = m.RealFace; player.Moving = false;
        state.weather = planned = Planned(SinceDawn);
        SnapWeather();
        mapTexDirty = true;
        SetPrompt("");
    }

    // Pip pays once a day for beating each rival in the Duel (only the Duel: the daily challenge has no prize). The prize goes
    // into the adventure when the match is left, never during it.
    static (int coins, string item, int n) DuelPrize(string rival) => rival switch
    {
        "bess" => (30, null, 0), "marlo" => (60, null, 0), "ivy" => (100, "glow_bait", 3), _ => (0, null, 0)
    };

    static string DuelPrizeText(string rival)
    {
        var p = DuelPrize(rival);
        return p.item == null ? $"{p.coins} coins" : $"{p.coins} coins and {Items.Amount(p.item, p.n)}";
    }

    void PayDuelPrizes(MatchRun m)
    {
        if (m.Won.Count == 0) return;
        var lines = new List<string>();
        foreach (var r in m.Won)
        {
            if (state.gifts.GetValueOrDefault("duel_" + r) == state.day) continue;
            var p = DuelPrize(r);
            state.coins += p.coins;
            if (p.item != null) Give(p.item, p.n);
            state.gifts["duel_" + r] = state.day;
            lines.Add(DuelPrizeText(r));
        }
        if (lines.Count == 0) return;
        Sfx.Play("coin");
        Toast("Pip pays you for the Duel: " + string.Join(" and ", lines) + ".", 5);
    }

    // The window is closing: the match counts as left, and the adventure goes back before the final Save.
    void AbandonMatch()
    {
        if (!MatchActive) return;
        if (match.Phase == "run") RecordMatch(Forfeit());
        match.Phase = "leaving";
        RestoreFromMatch();
    }

    void LeaveMatch(Action after = null, bool record = true)
    {
        if (!MatchActive) { after?.Invoke(); return; }
        if (record && match.Phase == "run") RecordMatch(Forfeit());
        ShutNetDerby();
        ShutRegatta();
        ShutQuiz();
        ShutReef();
        // From here on it is over: the clock must not end it a second time while the screen fades.
        match.Phase = "leaving";
        // A match that began on the dock goes back to the dock; leaving the dock itself goes back to the island.
        if (match.FromDock) { ReturnToDock(after); return; }
        FadeThrough(RestoreFromMatch, () => { after?.Invoke(); });
    }

    // A rematch: a new seed for a duel, the same day's seed for the daily challenge.
    void RestartMatch()
    {
        var old = match.Spec;
        var spec = new MatchSpec { Mode = old.Mode, Seed = old.Mode == "daily" ? old.Seed : MatchRules.Mix(Environment.TickCount, (int)(DateTime.Now.Ticks & 0x7fffffff)), Duration = old.Duration, Rival = old.Rival, Label = old.Label, Day = old.Day };
        match.Spec = spec;
        match.RivalEvt = 0;
        match.Me = default; match.Log.Clear(); match.Casts = 0; match.Lost = 0; match.T = 0; match.ReelAcc = 0; match.LeaveAsk = false; match.Result = null;
        (match.RivalRun, match.RivalName) = MatchRival(spec);
        rng = new Random(MatchRules.Mix(MatchRules.Version, spec.Seed, 9));
        ResetMatchPlace();
        mode = "play";
        match.Phase = "countdown";
        match.Count = MatchRules.CountdownSecs;
        Sfx.Play("blip");
    }

    /* ---------- The clock of a match ---------- */
    // Runs every frame (the match never pauses, whatever is open on top): the countdown, then three minutes of fishing.
    void UpdateMatch(float dt)
    {
        var m = match;
        if (m.Phase == "countdown")
        {
            int before = (int)MathF.Ceiling(m.Count);
            m.Count -= dt;
            if (m.Count <= 0) { m.Phase = "run"; m.T = 0; Sfx.Play("coin"); }
            else if ((int)MathF.Ceiling(m.Count) != before) Sfx.Play("blip");
            return;
        }
        if (m.Phase != "run") return;
        m.T += dt;
        UpdateRivalEvents();
        if (m.T >= m.Spec.Duration) EndMatch();
    }

    // The final whistle: a fish still on the line doesn't count, and the result is worked out and recorded once.
    void EndMatch()
    {
        var m = match;
        if (netDerby != null) { BeginNetFinish(); return; }
        m.T = m.Spec.Duration;
        m.Phase = "done";
        fish = null; reel = null; pointerHold = false; chargeSpot = null; charge = 0; heldItem = null;
        m.LeaveAsk = false;
        m.Me.Casts = m.Casts;
        var o = new MatchOutcome { Me = m.Me, RivalName = m.RivalName };
        if (m.RivalRun != null)
        {
            o.Rival = MatchRules.ScoreAt(m.RivalRun, m.Spec.Duration);
            var (w, reason) = MatchRules.Decide(m.Me, o.Rival);
            o.Result = w > 0 ? "win" : w < 0 ? "loss" : "draw";
            o.Reason = reason;
            o.Title = w > 0 ? "You win!" : w < 0 ? $"{m.RivalName} wins" : "A draw!";
            if (w > 0 && m.Spec.Mode == "derby" && DuelPrize(m.Spec.Rival).coins > 0)
            {
                if (m.Real.gifts.GetValueOrDefault("duel_" + m.Spec.Rival) == m.Real.day && !m.Won.Contains(m.Spec.Rival)) o.PrizeNote = "Pip has already paid for beating " + m.RivalName + " today.";
                else if (m.Won.Add(m.Spec.Rival)) o.Prize = DuelPrizeText(m.Spec.Rival);
                else o.PrizeNote = "Pip pays once a day for each rival.";
            }
        }
        else
        {
            o.Result = "solo";
            o.Title = "Time!";
        }
        m.Result = o;
        RecordMatch(o);
        PlanDailyPrize(m, o);
        Sfx.Play(o.Result == "win" ? "rare" : o.Result == "loss" ? "fail" : "coin");
        mode = "matchresult";
        SetPrompt("");
    }

    MatchOutcome Forfeit()
    {
        var m = match;
        m.Me.Casts = m.Casts;
        return new MatchOutcome { Result = "forfeit", Title = "You left the match", Me = m.Me, RivalName = m.RivalName, Rival = m.RivalRun == null ? default : MatchRules.ScoreAt(m.RivalRun, m.T) };
    }

    // One line on the leaderboard per finished match (a leaver's is marked forfeit; the daily challenge ignores leavers).
    void RecordMatch(MatchOutcome o)
    {
        var m = match;
        if (m == null || m.Spec == null) return;
        if (o.Result == "void") return;
        if (o.Result == "forfeit" && m.Spec.Mode == "daily") return;
        if (o.Result != "forfeit")
        {
            var prev = Leaderboards.BestOn(m.Spec.Mode, m.Spec.Seed);
            o.NewBest = m.Spec.Mode == "daily" && o.Me.Cg > 0 && (prev == null || o.Me.Cg > prev.score || o.Me.Cg == prev.score && o.Me.Perfects > prev.perfects);
        }
        Leaderboards.Add(new BoardEntry
        {
            id = m.Spec.Id, mode = m.Spec.Mode, label = m.Spec.Label, seed = m.Spec.Seed, ruleset = MatchRules.Version,
            rival = m.RivalName, result = o.Result, date = DateTime.Now.ToString("o"),
            score = o.Me.Cg, perfects = o.Me.Perfects, catches = o.Me.Catches, casts = o.Me.Casts, log = o.Result == "forfeit" ? null : m.Log.ToList()
        });
        if (o.Result != "forfeit") o.Rank = Leaderboards.Best(m.Spec.Mode, 1000).FindIndex(x => x.id == m.Spec.Id) + 1;
    }

    /* ---------- Fishing in a match ---------- */
    // Called from Cast (the nth cast reads its bite time and its fish from the seed), RollCatch, StartReel, LandCatch
    // and GotAway in Fishing.cs.
    float MatchWait(int n) => MatchRules.Plan(match.Spec.Seed, n).Wait;

    Catchable MatchRoll()
    {
        var f = Data.FishById[MatchRules.Plan(match.Spec.Seed, fish.MatchN).FishId];
        return new Catchable { Id = f.Id, Name = f.Name, Difficulty = f.Difficulty, Rare = f.Rare };
    }

    // The fight for the nth cast has a generator of its own (what the rest of the world draws from `rng` between frames can't
    // change it), and the reel runs at a fixed step.
    Random fightRng;
    void MatchSeedFight() => fightRng = new Random(MatchRules.Mix(MatchRules.Version, match.Spec.Seed, fish.MatchN, 4));

    void UnderFightRng(Action a)
    {
        var ambient = rng;
        rng = fightRng ?? rng;
        try { a(); }
        finally { fightRng = rng; rng = ambient; }
    }

    void StepMatchReel(float dt)
    {
        var m = match;
        m.ReelAcc += dt;
        for (int i = 0; i < 16 && m.ReelAcc >= MatchStep && reel != null && mode == "reeling"; i++)
        {
            m.ReelAcc -= MatchStep;
            UnderFightRng(() => UpdateReel(MatchStep));
        }
        if (m.ReelAcc > MatchStep) m.ReelAcc = 0;
        if (reel == null) m.ReelAcc = 0;
    }

    void MatchLand()
    {
        var m = match;
        if (reel == null || fish == null) return;
        var roll = reel.Roll;
        bool perfect = reel.Perfect;
        int n = fish.MatchN;
        fish = null; reel = null; pointerHold = false;
        if (mode == "reeling") mode = "play";
        if (m.Phase != "run" || m.T >= m.Spec.Duration) return;   // nothing counts after the whistle
        var plan = MatchRules.Plan(m.Spec.Seed, n);
        int cg = MatchRules.Centi(roll.Id, plan.Size, perfect);
        m.Me.Cg += cg; m.Me.Catches++;
        if (perfect) m.Me.Perfects++;
        m.Log.Add(new MatchEvent { t = m.T, fish = roll.Id, cg = cg, perfect = perfect });
        netDerby?.ReportLanding(n, perfect, m.T);
        heldItem = roll.Id; heldT = 1.4f;
        Floater($"+{cg / 100.0:0.00} kg", player.X, player.Y - 26, perfect ? "#f3c25b" : "#ffffff");
        Sfx.Play("catch");
    }

    /* ---------- Input and the frame ---------- */
    // The only keys a match listens to: fishing, and Esc to ask to leave. (A held key still reels: ReelHeld.)
    void MatchKeys()
    {
        var m = match;
        if (mode == "fade") return;
        if (m.LeaveAsk) { if (BackPressed()) m.LeaveAsk = false; return; }
        if (m.BoardOpen) { if (BackPressed() || Bind.Pressed("act")) m.BoardOpen = false; return; }
        if (m.Phase == "sungka") { if (BackPressed()) SungkaLanBack(); return; }
        if (m.Phase == "regatta") { RegattaKeys(); return; }
        if (m.Phase == "quiz") { QuizKeys(); return; }
        if (m.Phase == "reef") { ReefKeys(); return; }
        if (m.Phase == "dock") { DockKeys(); return; }
        if (Bind.Pressed("act") || Inp.Pressed(KeyboardKey.Enter))
        {
            if (mode == "reeling") reelTap = true;
            OnAction();
        }
        else if (BackPressed())
        {
            if (mode is "waiting" or "charging") ReelIn("You reeled in.");
            else if (m.Phase is "countdown" or "run" or "dock" or "netwait") m.LeaveAsk = true;
        }
    }

    void MatchResultKeys()
    {
        if (Inp.Pressed(KeyboardKey.Enter)) { if (netDerby != null) NetRematch(); else RestartMatch(); return; }
        if (BackPressed()) LeaveMatch();
    }

    // The play branch of Update, for a match: nobody walks, the lagoon is the only thing to act on.
    void UpdateMatchPlay(float dt)
    {
        if (match.FromDock) dockClient?.SetPose(DockSpawnX, DockSpawnY, 0, 0, away: true);
        if (match.Phase == "sungka") { player.Moving = false; target = null; return; }
        if (match.Phase == "regatta") { target = null; UpdateRegattaDuel(dt); return; }
        if (match.Phase == "quiz") { target = null; UpdateQuizDuel(dt); return; }
        if (match.Phase == "reef") { target = null; UpdateReefRun(dt); return; }
        if (match.Phase == "dock") { UpdateDock(dt); return; }
        player.Moving = false;
        target = match.Phase == "run" && !match.LeaveAsk ? SpotTarget(Data.SpotById[MatchSpot]) : null;
        if (target != null) { target.AltType = target.AltLabel = target.Alt2Type = target.Alt2Label = null; }
        SetPrompt(target?.Label ?? "", target != null ? "<act>" : null);
    }

    /* ---------- Drawing ---------- */
    static string MatchClock(float secs)
    {
        int s = Math.Max(0, (int)MathF.Ceiling(secs));
        return $"{s / 60}:{s % 60:00}";
    }

    // The scoreboard across the top, the countdown, and the question of leaving.
    void DrawMatchHud()
    {
        var m = match;
        if (m.Phase == "regatta") { DrawRegattaHud(); return; }
        if (m.Phase == "quiz") { DrawQuizHud(); return; }
        if (m.Phase == "reef") { DrawReefHud(); return; }
        if (m.Phase == "sungka") return;
        if (m.Phase == "dock") { DrawDockHud(); return; }
        bool rival = m.RivalRun != null;
        float w = rival ? 600 : 420, h = 104, x = (Gfx.LW - w) / 2, y = 14;
        Gfx.Box(x, y, w, h, NavyStrong, Pal.Ink, 2, 8, 3);
        float left = Math.Max(0, m.Spec.Duration - m.T);
        bool lastSecs = m.Phase == "run" && left <= 10;
        string clock = m.Phase == "run" || m.Phase == "done" ? MatchClock(left) : MatchClock(m.Spec.Duration);
        Gfx.TextCenter(clock, x + w / 2, y + 10, FontKind.Ui700, 44, lastSecs && (int)(time * 3) % 2 == 0 ? Pal.C("#ff9a8a") : White);
        Gfx.TextCenter(m.Spec.Label, x + w / 2, y + 64, FontKind.Ui600, 16, Pal.C("#bcd3e0"));
        if (m.Phase is "run" or "done")
        {
            float bw = w - 40;
            Gfx.Rect(x + 20, y + h - 14, bw, 5, Pal.C("rgba(255,255,255,0.18)"), 2);
            Gfx.Rect(x + 20, y + h - 14, bw * Math.Clamp(m.T / m.Spec.Duration, 0, 1), 5, lastSecs ? Pal.C("#ff9a8a") : Pal.Lantern, 2);
        }
        void Side(float sx, float align, string name, MatchScore s)
        {
            Gfx.Text(name, align < 0 ? sx : sx - Gfx.Measure(name, FontKind.Ui600, 17), y + 10, FontKind.Ui600, 17, Pal.C("#bcd3e0"));
            string kg = $"{s.Kg} kg";
            Gfx.Text(kg, align < 0 ? sx : sx - Gfx.Measure(kg, FontKind.Ui700, 28), y + 30, FontKind.Ui700, 28, White);
            string perf = s.Perfects == 1 ? "1 perfect hook" : $"{s.Perfects} perfect hooks";
            Gfx.Text(perf, align < 0 ? sx : sx - Gfx.Measure(perf, FontKind.Ui500, 15), y + 66, FontKind.Ui500, 15, Pal.C("#e9d79a"));
        }
        Side(x + 22, -1, "You", m.Me);
        if (rival) Side(x + w - 22, 1, m.RivalName, MatchRules.ScoreAt(m.RivalRun, m.Phase == "run" ? m.T : m.Phase == "done" ? m.Spec.Duration : 0));

        if (m.Phase is "netwait" or "waitend")
        {
            string wt = m.Phase == "netwait" ? $"Waiting for {m.RivalName}..." : $"Waiting for {m.RivalName}'s last catch...";
            Gfx.TextCenter(wt, Gfx.LW / 2 + 2, 262, FontKind.Ui700, 40, Pal.C("rgba(6,14,24,0.6)"));
            Gfx.TextCenter(wt, Gfx.LW / 2, 258, FontKind.Ui700, 40, Pal.WithAlpha(White, 0.95f));
        }
        if (m.Phase == "countdown")
        {
            int n = (int)MathF.Ceiling(m.Count);
            Gfx.TextCenter(n.ToString(), Gfx.LW / 2 + 4, 244, FontKind.Ui700, 150, Pal.C("rgba(6,14,24,0.55)"));
            Gfx.TextCenter(n.ToString(), Gfx.LW / 2, 240, FontKind.Ui700, 150, Pal.WithAlpha(White, 0.95f));
            Gfx.TextCenter("Get ready: cast into the lagoon", Gfx.LW / 2 + 2, 412, FontKind.Ui700, 28, Pal.C("rgba(6,14,24,0.6)"));
            Gfx.TextCenter("Get ready: cast into the lagoon", Gfx.LW / 2, 410, FontKind.Ui700, 28, Pal.WithAlpha(White, 0.92f));
        }
        else if (m.Phase == "run" && m.T < 1.2f)
            Gfx.TextCenter("Fish!", Gfx.LW / 2, 250, FontKind.Ui700, 120, Pal.WithAlpha(Pal.Lantern, Math.Clamp((1.2f - m.T) / 0.6f, 0, 1)));

    }

    void DrawMatchLeave()
    {
        Backdrop();
        const float w = 560, h = 250;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        PanelBox(x, y, w, h);
        Gfx.TextCenter(match.Phase == "dock" ? "Leave the dock?" : "Leave the match?", x + w / 2, y + 26, FontKind.Ui700, 34, Pal.PaperInk);
        var lines = Gfx.Wrap(match.Phase == "dock" ? "You go back to the island, as you were." : match.RivalRun != null ? "The match goes on while you decide. If you leave, it counts as a loss." : "The match goes on while you decide. Leaving ends the run.", FontKind.Note, 20, w - 60);
        Lines(lines, x + 30, y + 80, 26, FontKind.Note, 20, Pal.PaperInk);
        float bx = x + 30, by = y + h - 82;
        if (BigButton("Keep playing", bx, by, true)) { match.LeaveAsk = false; return; }
        if (BigButton("Leave", x + w - 30 - BigW("Leave"), by, false))
        {
            match.LeaveAsk = false;
            LeaveMatch(() => Toast(match == null || !match.Dock ? "You left the match." : "Back on the island."));
        }
    }

    void DrawMatchResult()
    {
        var m = match;
        var o = m?.Result;
        if (o == null) return;
        Backdrop();
        const float w = 820, h = 560;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        PanelBox(x, y, w, h);
        var tone = o.Result == "win" ? Pal.C("#2f7d3a") : o.Result == "loss" ? Rust : Pal.PaperInk;
        Gfx.TextCenter(o.Title, x + w / 2, y + 22, FontKind.Ui700, 46, tone);
        string sub = o.Result == "solo" ? m.Spec.Label : o.Reason;
        Gfx.TextCenter(sub, x + w / 2, y + 80, FontKind.Ui600, 20, Muted);
        if (o.Prize != "") Gfx.TextCenter($"Pip's prize when you are back on the island: {o.Prize}", x + w / 2, y + 108, FontKind.Ui700, 19, Pal.C("#9a6a1a"));
        else if (o.PrizeNote != "") Gfx.TextCenter(o.PrizeNote, x + w / 2, y + 108, FontKind.Ui600, 17, Muted);
        if (o.NewBest) Gfx.TextCenter(o.Rank == 1 ? "A new best on your board!" : $"On your board: number {o.Rank}", x + w / 2, o.DailyLine != "" ? y + 102 : y + 108, FontKind.Ui700, o.DailyLine != "" ? 18 : 20, Pal.C("#9a6a1a"));
        if (o.DailyLine != "") Gfx.TextCenter(Gfx.Ellipsize(o.DailyLine, FontKind.Ui600, 16, w - 40), x + w / 2, y + 126, FontKind.Ui600, 16, Pal.C("#9a6a1a"));

        // Two score cards.
        float cw = m.RivalRun != null ? 360 : 460, cy = y + 146, ch = 110, gap = 20;
        bool two = m.RivalRun != null;
        float cx0 = two ? x + (w - cw * 2 - gap) / 2 : x + (w - cw) / 2;
        void Card(float cx, string name, MatchScore s, bool win)
        {
            Gfx.Box(cx, cy, cw, ch, win ? Pal.C("#fff4cf") : CardBg, Pal.Ink, 2, 6, 3);
            Gfx.Text(name, cx + 18, cy + 12, FontKind.Ui700, 20, Muted);
            Gfx.Text($"{s.Kg} kg", cx + 18, cy + 38, FontKind.Ui700, 38, Pal.PaperInk);
            Gfx.Text($"{s.Catches} fish · {s.Perfects} perfect" + (s.Casts > 0 ? $" · {s.Casts} casts" : ""), cx + 18, cy + 84, FontKind.Ui500, 16, Muted);
        }
        Card(cx0, "You", o.Me, o.Result == "win");
        if (two) Card(cx0 + cw + gap, o.RivalName, o.Rival, o.Result == "loss");

        // What you landed, in order.
        float ly = cy + ch + 22;
        Gfx.Text("Your catches", x + 30, ly, FontKind.Ui700, 18, Muted);
        ly += 28;
        if (m.Log.Count == 0) Gfx.Text("Nothing this time. The fish are still out there.", x + 30, ly, FontKind.Note, 18, Pal.PaperInk);
        int col = 0;
        foreach (var e in m.Log.Take(12))
        {
            float ex = x + 30 + col % 2 * (w / 2 - 20), ey = ly + col / 2 * 25;
            string name = Gfx.Ellipsize(Data.FishById[e.fish].Name, FontKind.Ui500, 17, w / 2 - 150);
            Gfx.Text($"{(int)e.t / 60}:{(int)e.t % 60:00}", ex, ey, FontKind.Ui500, 17, Muted);
            Gfx.Text(name, ex + 50, ey, FontKind.Ui500, 17, Pal.PaperInk);
            string kg = $"{e.cg / 100.0:0.00} kg" + (e.perfect ? " perfect" : "");
            Gfx.Text(kg, ex + w / 2 - 20 - 30 - Gfx.Measure(kg, FontKind.Ui600, 17), ey, FontKind.Ui600, 17, e.perfect ? Pal.C("#9a6a1a") : Pal.PaperInk);
            col++;
        }

        float by = y + h - 84;
        string again = m.Spec.Mode == "daily" ? "Try again" : "Rematch";
        float bx = x + 30;
        if (netDerby != null)
        {
            bool can = !netDerby.PeerGone && netDerby.EndWhy == "finished" && !netDerby.WaitingForRematch;
            string nl = netDerby.WaitingForRematch ? $"Waiting for {m.RivalName}..." : "Rematch";
            if (Button(nl, bx, by, BigW(nl), 56, FontKind.Ui700, 24, can ? Pal.Buoy : Pal.C("#d8cbb0"), can ? White : Muted, 5, 3, 6, can)) { NetRematch(); return; }
            bx += BigW(nl) + 14;
        }
        else
        {
            if (BigButton(again, bx, by, true)) { RestartMatch(); return; }
            bx += BigW(again) + 14;
        }
        if (m.FromDock)
        {
            if (BigButton("Back to the dock", bx, by, false)) { LeaveMatch(); return; }
            if (BigButton("Leave", x + w - 30 - BigW("Leave"), by, false)) { LeaveDockFromResult(); return; }
            return;
        }
        if (BigButton("Leaderboards", bx, by, false)) { LeaveMatch(() => OpenMatches("board")); return; }
        if (BigButton("Back to the island", x + w - 30 - BigW("Back to the island"), by, false)) { LeaveMatch(); return; }
    }
}
