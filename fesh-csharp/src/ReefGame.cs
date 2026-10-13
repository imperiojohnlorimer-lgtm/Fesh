using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The Reef Run in the game (1.30): started from the Tournament Dock by talking to one of the anglers. A spearfishing time
// attack for forty-five seconds over a reef; the fish and where they are are ReefCore.cs's plan, the angler's score runs
// alongside yours. One screen, drawn over the darkened water; nothing here touches the adventure.
sealed class ReefRun
{
    public int Seed; public string Rival = "", RivalName = "";
    public List<ReefRules.Pass> Plan = new();
    public readonly HashSet<int> Speared = new();
    public string Phase = "count";                       // count, run, done
    public float Count = ReefRules.CountSecs, T, Cool;
    public int Points, Throws, Misses;
    public float AimX = 440, AimY = 200;
    public (float t, float x, float y)? Flight;          // a spear in the air: when it was thrown and where it will land
    public readonly List<(string text, float x, float y, float t, bool good)> Pops = new();
    public bool Recorded; public string Result = ""; public int AnglerPts;
}

partial class Game
{
    ReefRun reef;
    const float ReefX = 200, ReefY = 150;

    bool InReef => MatchActive && match.Phase == "reef" && reef != null;

    /* ---------- Starting ---------- */
    void StartDockReef(string rivalId)
    {
        if (!InDock) return;
        var sk = MatchRules.RivalById(rivalId);
        var spec = new MatchSpec { Mode = "spear", Seed = NewMatchSeed(), Rival = rivalId, Label = $"Reef Run · against {sk.Name}", Duration = 0 };
        int g = ++match.Gen;
        match.Phase = "ready";
        FadeThrough(() =>
        {
            if (match == null || match.Gen != g) return;
            match.FromDock = true;
            ResetMatchCounters(spec);
            ResetMatchPlace();
            NewReef(spec.Seed, rivalId, sk.Name);
        }, () => { if (match == null || match.Gen != g) return; match.Phase = "reef"; });
    }

    void NewReef(int seed, string rivalId, string name)
    {
        reef = new ReefRun { Seed = seed, Rival = rivalId, RivalName = name, Plan = ReefRules.Plan(seed) };
        reef.AnglerPts = ReefRules.AnglerScoreAt(seed, rivalId, 1000);
    }

    void ReefAgain() { if (reef != null) { NewReef(NewMatchSeed(), reef.Rival, reef.RivalName); Sfx.Play("blip"); } }

    /* ---------- Every frame ---------- */
    void UpdateReefRun(float dt)
    {
        var r = reef;
        player.Moving = false;
        for (int i = r.Pops.Count - 1; i >= 0; i--) if (time - r.Pops[i].t > 1.2f) r.Pops.RemoveAt(i);
        if (r.Phase == "done") return;
        if (r.Phase == "count")
        {
            r.Count -= dt;
            if (r.Count <= 0) { r.Phase = "run"; r.T = 0; Sfx.Play("coin"); }
            return;
        }
        r.T += dt;
        r.Cool = Math.Max(0, r.Cool - dt);
        // Keys move the aim; the mouse sets it where it points.
        float ax = 0, ay = 0;
        if (Bind.Down("left")) ax -= 1;
        if (Bind.Down("right")) ax += 1;
        if (Bind.Down("up")) ay -= 1;
        if (Bind.Down("down")) ay += 1;
        if (!match.LeaveAsk) { r.AimX += ax * 260 * dt; r.AimY += ay * 260 * dt; }
        if (Gfx.MouseMoved && !match.LeaveAsk) { r.AimX = Gfx.Mouse.X - ReefX; r.AimY = Gfx.Mouse.Y - ReefY; }
        r.AimX = Math.Clamp(r.AimX, 0, ReefRules.FieldW); r.AimY = Math.Clamp(r.AimY, 0, ReefRules.FieldH - 40);
        // A spear lands FlightSecs after it left, and scores what is under it then.
        if (r.Flight is var (ft, fx, fy) && r.T >= ft + ReefRules.FlightSecs)
        {
            r.Flight = null;
            int id = ReefRules.Hit(r.Plan, r.Speared, fx, fy, ft);
            if (id >= 0)
            {
                r.Speared.Add(id);
                int pts = ReefRules.Points[r.Plan[id].Size];
                r.Points += pts;
                r.Pops.Add(($"+{pts}", fx, fy - 12, time, true));
                Sfx.Play(pts >= 4 ? "rare" : "catch");
            }
            else { r.Misses++; r.Pops.Add(("Miss", fx, fy - 12, time, false)); Sfx.Play("splash"); }
        }
        if (r.T >= ReefRules.Duration && r.Flight == null) EndReef();
    }

    void ThrowReefSpear()
    {
        var r = reef;
        if (r == null || r.Phase != "run" || r.Cool > 0 || r.Flight != null || match.LeaveAsk || r.T >= ReefRules.Duration) return;
        r.Flight = (r.T, r.AimX, r.AimY);
        r.Throws++;
        r.Cool = ReefRules.CooldownSecs;
        Sfx.Play("cast");
    }

    void EndReef()
    {
        var r = reef;
        if (r.Phase == "done") return;
        r.Phase = "done";
        int d = ReefRules.Decide(r.Points, 0, r.AnglerPts, 0);
        r.Result = d > 0 ? "win" : d < 0 ? "loss" : "draw";
        RecordReef(r.Result);
        Sfx.Play(r.Result == "win" ? "rare" : r.Result == "loss" ? "fail" : "coin");
    }

    void RecordReef(string result)
    {
        var r = reef;
        if (r == null || r.Recorded) return;
        r.Recorded = true;
        Leaderboards.Add(new BoardEntry
        {
            id = Guid.NewGuid().ToString("N"), mode = "spear", label = $"Reef Run · against {r.RivalName}", seed = r.Seed, ruleset = MatchRules.Version, rival = r.RivalName,
            result = result, date = DateTime.Now.ToString("o"), score = r.Points, perfects = 0, casts = r.Throws, catches = r.AnglerPts
        });
    }

    // Leaving in the middle is a loss (and never a best score); a finished run is already on the board.
    void ShutReef()
    {
        if (reef != null && reef.Phase != "done" && !reef.Recorded && reef.Phase == "run") RecordReef("forfeit");
        reef = null;
    }

    void ReefKeys()
    {
        var r = reef;
        if (r.Phase == "done") { if (Inp.Pressed(KeyboardKey.Enter)) ReefAgain(); else if (BackPressed()) LeaveMatch(); return; }
        if (BackPressed()) { match.LeaveAsk = true; return; }
        if (Bind.Pressed("act") || Inp.Pressed(KeyboardKey.Enter) || Inp.Pressed(KeyboardKey.Space)) ThrowReefSpear();
    }

    /* ---------- Drawing ---------- */
    void DrawReefHud()
    {
        var r = reef;
        Backdrop();
        const float w = 960, h = 640;
        float x = (Gfx.LW - w) / 2, y = 30;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Reef Run", x + 30, y + 14, FontKind.Ui700, 28, Pal.PaperInk);
        Gfx.Text("Fish: small 1 point, medium 2, big 4", x + 30, y + 52, FontKind.Note, 16, Muted);
        float left = Math.Max(0, ReefRules.Duration - r.T);
        string clock = r.Phase == "count" ? $"{ReefRules.Duration:0}" : $"{left:0.0}";
        Gfx.TextCenter(clock, x + w / 2, y + 8, FontKind.Ui700, 44, left < 8 && r.Phase == "run" && (int)(time * 3) % 2 == 0 ? Rust : Pal.PaperInk);
        int rival = r.Phase == "done" ? r.AnglerPts : ReefRules.AnglerScoreAt(r.Seed, r.Rival, r.T);
        Gfx.Text($"You  {r.Points}", x + w - 340, y + 14, FontKind.Ui700, 26, Pal.C("#2f7d3a"));
        string rs = $"{Gfx.Ellipsize(r.RivalName, FontKind.Ui700, 20, 150)}  {rival}";
        Gfx.Text(rs, x + w - 30 - Gfx.Measure(rs, FontKind.Ui700, 26), y + 14, FontKind.Ui700, 26, Rust);

        // The water.
        float fx = ReefX, fy = ReefY, fw = ReefRules.FieldW, fh = ReefRules.FieldH;
        Gfx.Rect(fx - 4, fy - 4, fw + 8, fh + 8, Pal.Ink, 6);
        var clip = Gfx.S(fx, fy, fw, fh);
        Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)MathF.Ceiling(clip.Width), (int)MathF.Ceiling(clip.Height));
        try
        {
            for (int i = 0; i < 11; i++) Gfx.Rect(fx, fy + i * fh / 11, fw, fh / 11 + 1, Raylib.ColorLerp(Pal.C("#5fb7cf"), Pal.C("#1d5c86"), i / 10f));
            // The sand and a few corals along the bottom (fixed, so they are the same every time).
            Gfx.Rect(fx, fy + fh - 40, fw, 40, Pal.C("#d9c48a"));
            for (int i = 0; i < 9; i++)
            {
                float cx = fx + 40 + i * 100 + (i * 37 % 23), ch = 14 + i * 7 % 18;
                Gfx.Rect(cx, fy + fh - 40 - ch, 12, ch, i % 2 == 0 ? Pal.C("#d96a7a") : Pal.C("#c98ad9"), 5);
                Gfx.Rect(cx + 14, fy + fh - 40 - ch * 0.6f, 9, ch * 0.6f, i % 2 == 0 ? Pal.C("#e58a6a") : Pal.C("#a96ac9"), 4);
            }
            // Light moving on the water.
            for (int i = 0; i < 14; i++) Gfx.Rect(fx + (i * 71 + time * 14) % (fw + 40) - 20, fy + 20 + i * 27 % (fh - 100), 46, 3, Pal.Rgba(255, 255, 255, 0.08f), 1);
            // The fish: dark shadows, a bigger one is longer and glints.
            foreach (var p in r.Plan)
            {
                if (r.Speared.Contains(p.Id) || ReefRules.PosAt(p, r.Phase == "run" ? r.T : r.Phase == "done" ? 999 : -1) is not var (px, py) || (px, py) == default && ReefRules.PosAt(p, r.T) == null) continue;
                DrawReefFish(fx + px, fy + py, p, ReefRules.Radius[p.Size]);
            }
            // The spear in the air and the aim.
            if (r.Phase == "run")
            {
                if (r.Flight is var (ft, lx, ly))
                {
                    float k = Math.Clamp((r.T - ft) / ReefRules.FlightSecs, 0, 1);
                    float sx0 = fx + fw / 2, sy0 = fy + fh + 10, tx = fx + lx, ty = fy + ly;
                    float hx = sx0 + (tx - sx0) * k, hy = sy0 + (ty - sy0) * k;
                    Gfx.Line(hx - (tx - sx0) * 0.12f, hy - (ty - sy0) * 0.12f, hx, hy, 3, Pal.C("#8a6440"));
                    Gfx.Circle(hx, hy, 3, Pal.C("#e8f0f4"));
                }
                float pulse = 14 + MathF.Sin(time * 8) * 1.5f, ax = fx + r.AimX, ay = fy + r.AimY;
                var ring = r.Cool > 0 ? Pal.C("#c9d3d8") : Pal.C("#ffe28a");
                Gfx.Rect(ax - pulse, ay - 1, pulse * 2, 2, Pal.Rgba(ring.R, ring.G, ring.B, 0.5f));
                Gfx.Rect(ax - 1, ay - pulse, 2, pulse * 2, Pal.Rgba(ring.R, ring.G, ring.B, 0.5f));
                Gfx.Circle(ax, ay, 3, ring);
                if (r.Cool > 0) Gfx.Rect(ax - 18, ay + pulse + 4, 36 * (1 - r.Cool / ReefRules.CooldownSecs), 3, ring);
            }
            foreach (var pop in r.Pops)
            {
                float age = time - pop.t;
                Gfx.TextCenter(pop.text, fx + pop.x, fy + pop.y - age * 22, FontKind.Ui700, 24, pop.good ? Pal.C("#ffe28a") : Pal.C("#e8f0f4"));
            }
        }
        finally { Raylib.EndScissorMode(); }

        if (r.Phase == "count")
        {
            int n = (int)MathF.Ceiling(r.Count);
            Gfx.TextCenter(n.ToString(), Gfx.LW / 2 + 4, fy + 120, FontKind.Ui700, 150, Pal.C("rgba(6,14,24,0.55)"));
            Gfx.TextCenter(n.ToString(), Gfx.LW / 2, fy + 116, FontKind.Ui700, 150, White);
        }
        Gfx.Text(r.Phase == "done" ? "" : "Aim with the mouse or the move keys. Click or press <act> to throw.".Replace("<act>", Bind.Name("act")), x + 30, fy + fh + 16, FontKind.Note, 18, Muted);
        if (r.Phase == "run") Gfx.Text($"Throws {r.Throws}", x + w - 30 - Gfx.Measure($"Throws {r.Throws}", FontKind.Note, 18), fy + fh + 16, FontKind.Note, 18, Muted);
        // A click anywhere on the water throws where it points.
        if (r.Phase == "run" && !match.LeaveAsk && Gfx.Click(fx, fy, fw, fh)) { r.AimX = Gfx.Mouse.X - ReefX; r.AimY = Gfx.Mouse.Y - ReefY; ThrowReefSpear(); }
        if (r.Phase == "done") DrawReefResult();
    }

    void DrawReefFish(float cx, float cy, ReefRules.Pass p, float radius)
    {
        int dir = p.FromLeft ? 1 : -1;
        float rx = radius * 1.3f, ry = radius * 0.55f;
        var body = Pal.Rgba(8, 28, 44, p.Size == 2 ? 0.7f : 0.55f);
        var c = Gfx.P(cx, cy);
        Raylib.DrawEllipse((int)c.X, (int)c.Y, rx * Gfx.Z, ry * Gfx.Z, body);
        float wag = MathF.Sin(time * 9 + p.Id) * ry * 0.5f;
        float tx = cx - dir * rx * 0.9f;
        Gfx.Triangle(tx, cy, tx - dir * rx * 0.7f, cy - ry - wag, tx - dir * rx * 0.7f, cy + ry - wag, body);
        if (p.Size == 2 && (time * 2 + p.Id) % 1 < 0.2f) Gfx.Circle(cx + dir * rx * 0.5f, cy - ry * 0.3f, 2, White);
    }

    void DrawReefResult()
    {
        var r = reef;
        const float w = 620, h = 360;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        string title = r.Result == "win" ? "You win the Reef Run!" : r.Result == "loss" ? $"{r.RivalName} wins" : "A draw!";
        Gfx.TextCenter(title, x + w / 2, y + 22, FontKind.Ui700, 40, r.Result == "win" ? Pal.C("#2f7d3a") : r.Result == "loss" ? Rust : Pal.PaperInk);
        Gfx.TextCenter($"{r.Points} points to {r.AnglerPts}", x + w / 2, y + 84, FontKind.Ui700, 26, Pal.PaperInk);
        int hits = r.Speared.Count;
        Gfx.TextCenter($"{hits} fish speared with {r.Throws} throws ({(r.Throws > 0 ? hits * 100 / r.Throws : 0)}% on target)", x + w / 2, y + 128, FontKind.Ui600, 20, Muted);
        var best = Leaderboards.Best("spear", 1).FirstOrDefault();
        if (best != null) Gfx.TextCenter($"Your best Reef Run: {best.score} points", x + w / 2, y + 164, FontKind.Ui600, 18, Pal.C("#9a6a1a"));
        float by = y + h - 84, bx = x + 30;
        if (BigButton("Again", bx, by, true)) { ReefAgain(); return; }
        bx += BigW("Again") + 14;
        if (BigButton("Back to the dock", bx, by, false)) { LeaveMatch(); return; }
        if (BigButton("Leave", x + w - 30 - BigW("Leave"), by, false)) { LeaveDockFromResult(); return; }
    }
}
