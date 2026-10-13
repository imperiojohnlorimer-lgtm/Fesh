using Raylib_cs;

namespace Fesh;

// 1.29: the restless sea and Baga's volcano give you something to do while the clock runs, not only a key to hold or a
// timer to wait out. While the ground shakes, three steps (into the open, drop and cover, hold on) tick off as you do
// them and a red ring warns when you're beside a palm or a house; up on School Rise you check each person in for the head
// count, and a child tempted down to look at the stranded fish has to be called back; on Baga a countdown shows when
// Niko's banca leaves and when the ash arrives, with the haze coming in over the sea. None of it is required: whoever you
// didn't get to is counted or called back for you, without credit (the report only praises what you measured).
partial class Game
{
    // The child who wants to see the fish: Jun, from the second bench seat.
    const int DriftKid = 1;
    const float DriftAt = 26, DriftLeash = 14;

    Quake CurrentQuake => tremor ?? (evac?.Phase == "quake" ? evac.Quake : null);

    /* ---------- On School Rise ---------- */
    void UpdateRiseDuty(Evac e, float dt)
    {
        foreach (var m in e.People)
        {
            m.Moving = false;
            if (m.Kid != DriftKid || !m.Safe) continue;
            var (bx, by) = BenchSeat(m.Kid);
            // Only while the sea is out (and early enough that there is time to bring him back before the all-clear).
            if (m.DriftState == 0 && !m.Drifted && e.T >= DriftAt && e.T < 34)
            {
                // The sea has gone right out: fish flapping on the sand, and a boy who wants a closer look.
                m.Drifted = true; m.DriftState = 1; m.DriftT = 0; m.CheckedIn = false;   // away from the group, so not counted until he is back
                m.X = bx; m.Y = by;
                m.DriftTo = (SignSpots[2].x + 10, SignSpots[2].y + 2);
                Floater("The fish! Look!", m.X, m.Y - 16, "#ffd76a");
                Toast($"{m.Name} is wandering down toward the sea to look at the stranded fish! Go and call him back.", 5);
                Sfx.Play("blip");
            }
            if (m.DriftState == 1)
            {
                m.DriftT += dt;
                if (WalkToward(m, m.DriftTo, 15, dt)) m.Face = "left";
                if (m.DriftT >= DriftLeash)
                {
                    // Lira gets there first, if you haven't (no credit).
                    m.DriftState = 2;
                    var lira = e.People.FirstOrDefault(p => p.Id == "lira");
                    Floater("Jun! Come back!", lira?.X ?? m.X, (lira?.Y ?? m.Y) - 18, "#ffd76a");
                    Toast("Lira calls Jun back. The sea going out is a warning, not something to go and look at.", 5);
                }
            }
            else if (m.DriftState == 2 && WalkToward(m, (bx, by), 26, dt)) { m.DriftState = 0; m.X = bx; m.Y = by; m.Face = "down"; }
        }
    }

    // One step toward a point; true once there.
    static bool WalkToward(Mover m, (float x, float y) to, float speed, float dt)
    {
        float dx = to.x - m.X, dy = to.y - m.Y, d = MathF.Sqrt(dx * dx + dy * dy), step = speed * dt;
        if (d <= step) { m.X = to.x; m.Y = to.y; return true; }
        m.X += dx / d * step; m.Y += dy / d * step;
        m.WalkT += dt; m.Moving = true;
        m.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
        return false;
    }

    // What E does up on the rise: call the wandering child back, or check someone in.
    Target RiseDutyTarget()
    {
        var e = evac;
        if (e == null || e.Drill) return null;
        var drifter = e.People.FirstOrDefault(m => m.DriftState == 1 && Dist(m.X, m.Y, player.X, player.Y) < ShoutR);
        if (drifter != null) return new Target { Type = "recall", Ref = drifter, Label = $"Call {drifter.Name} back: \"Stay up here! The sea comes back!\"" };
        var wait = e.People.Where(m => m.Safe && !m.CheckedIn && m.DriftState == 0 && Dist(m.X, m.Y, player.X, player.Y) < 22).OrderBy(m => Dist(m.X, m.Y, player.X, player.Y)).FirstOrDefault();
        if (wait != null) return new Target { Type = "checkin", Ref = wait, Label = $"Check in {wait.Name}" };
        return null;
    }

    void RecallKid(Mover m)
    {
        var e = evac;
        if (e == null || m.DriftState != 1) return;
        m.DriftState = 2;
        e.Kept = true;
        Sfx.Play("whistle");
        Floater("Stay up here!", player.X, player.Y - 26, "#ffd76a");
        Floater("Okay!", m.X, m.Y - 16, "#7fd36b");
        Toast("Ma'am Isay: \"Good call. When the sea runs out like that, it's coming back. We stay up here until the officials say it's safe.\"", 6);
    }

    void CheckIn(Mover m)
    {
        var e = evac;
        if (e == null || m.CheckedIn) return;
        FaceToward(m.X, m.Y);
        Swing("hands", 0.25f);
        m.CheckedIn = true;
        Sfx.Play("ui");
        Floater("Here!", m.X, m.Y - 16, "#7fd36b");
        if (e.People.All(p => p.CheckedIn)) Toast("Head count done: Lira, Niko, Mia, Jun, Bea... and you. Everyone's here. Isay writes it on her list.", 5);
    }

    /* ---------- The cards ---------- */
    // Where a card sits in the middle of the screen's bottom, above the prompt and the hotbar.
    const float DutyY = Gfx.LH - 232;

    void DrawStoryDuty()
    {
        if (mode != "play") return;
        DrawShakeSteps();
        DrawRiseRoster();
        DrawCountdown();
    }

    // Drop, cover and hold on: the three steps and how long is left.
    void DrawShakeSteps()
    {
        var q = CurrentQuake;
        if (q == null) return;
        bool open = InTheOpen, ducked = Ducking && open;
        const float w = 780, h = 84;
        float x = (Gfx.LW - w) / 2, y = DutyY;
        Gfx.Rect(x, y, w, h, Navy, 8);
        string[] names = { "Into the open", Bind.Fix("Drop and cover: hold <act>"), "Hold on" };
        for (int i = 0; i < 3; i++)
        {
            bool done = i == 0 ? open : i == 1 ? ducked : q.Ducked;
            float cx = x + 20 + i * 250;
            Gfx.Circle(cx + 12, y + 24, 13, done ? Pal.C("#3fae5a") : Pal.C("rgba(255,255,255,0.18)"));
            Gfx.TextCenter(done ? "ok" : (i + 1).ToString(), cx + 12, y + 14, FontKind.Ui700, 16, Color.White);
            Gfx.Text(Gfx.Ellipsize(names[i], FontKind.Ui700, 17, 205), cx + 32, y + 15, FontKind.Ui700, 17, done ? Pal.C("#b6f0c0") : Color.White);
        }
        float frac = Math.Clamp(q.T / MathF.Max(0.1f, q.Len), 0, 1);
        Gfx.Rect(x + 20, y + 52, w - 40, 12, Pal.C("rgba(255,255,255,0.16)"), 5);
        Gfx.Rect(x + 20, y + 52, (w - 40) * frac, 12, ducked ? Pal.C("#3fae5a") : Pal.C("#e8a83a"), 5);
        string hint = !open ? "Away from palms and houses, where nothing can fall on you." : !ducked ? "Hold the key down: head covered, low, and stay put." : "Keep holding until the shaking stops.";
        Gfx.Text(hint, x + 20, y + h + 6, FontKind.Ui600, 16, Color.White);
    }

    // The head count on School Rise.
    void DrawRiseRoster()
    {
        var e = evac;
        if (e == null || e.Drill || e.Phase != "evacuate") return;
        const float w = 236;
        float x = Gfx.LW - 13 - w, y = 142, rowH = 26, h = 40 + e.People.Count * rowH + 8;
        Gfx.Rect(x, y, w, h, Navy, 8);
        int n = e.People.Count(p => p.CheckedIn);
        Gfx.Text($"Head count {n} of {e.People.Count}", x + 14, y + 10, FontKind.Ui700, 18, Color.White);
        float ry = y + 40;
        foreach (var m in e.People)
        {
            Color c = m.DriftState != 0 ? Pal.C("#e0584a") : m.CheckedIn ? Pal.C("#3fae5a") : m.Safe ? Pal.C("#e8c050") : Pal.C("rgba(255,255,255,0.22)");
            Gfx.Circle(x + 24, ry + 11, 8, c);
            string st = m.DriftState == 1 ? "at the shore!" : m.DriftState == 2 ? "coming back" : m.CheckedIn ? "here" : m.Safe ? "up, check in" : m.Going ? "on the way" : "not called yet";
            Gfx.Text(m.Name, x + 42, ry + 3, FontKind.Ui600, 17, Color.White);
            Gfx.Text(st, x + w - 14 - Gfx.Measure(st, FontKind.Ui500, 15), ry + 5, FontKind.Ui500, 15, m.DriftState == 1 ? Pal.C("#ff9a8a") : Pal.C("#c8d4dc"));
            ry += rowH;
        }
    }

    // Beneath the clouds: how long before Niko's banca leaves Baga, and before the ash gets to the village.
    (string label, float left, float total)? Countdown()
    {
        if (BagaEvacuating && OnBaga()) return ("Niko's banca leaves Baga within", 150 - mgEvacT, 150);
        if (AshPhase && !state.Hinted("mg:ashStarted")) return ("Ash is due within", 75 - mgPrepT, 75);
        return null;
    }

    void DrawCountdown()
    {
        if (Countdown() is not var (label, left, total)) return;
        left = MathF.Max(0, left);
        const float w = 440, h = 62;
        float x = (Gfx.LW - w) / 2, y = DutyY + 22;
        Gfx.Rect(x, y, w, h, Navy, 8);
        int s = (int)MathF.Ceiling(left);
        string clock = $"{s / 60}:{s % 60:00}";
        Gfx.Text(label, x + 16, y + 9, FontKind.Ui600, 18, Color.White);
        Gfx.Text(clock, x + w - 16 - Gfx.Measure(clock, FontKind.Ui700, 26), y + 5, FontKind.Ui700, 26, left < 20 ? Pal.C("#ff9a8a") : Pal.C("#ffd76a"));
        Gfx.Rect(x + 16, y + 40, w - 32, 10, Pal.C("rgba(255,255,255,0.16)"), 4);
        Gfx.Rect(x + 16, y + 40, (w - 32) * Math.Clamp(left / total, 0, 1), 10, left < 20 ? Pal.C("#e0584a") : Pal.C("#e8a83a"), 4);
    }

    /* ---------- In the world ---------- */
    // A red ring round you while the ground shakes and there's a palm or a house beside you.
    void DrawShakeWarn()
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 10);
        pix.Ring(player.X, player.Y - 3, 10 + pulse * 2, Pal.Rgba(232, 70, 58, 0.55f + 0.4f * pulse));
        pix.Ring(player.X, player.Y - 3, 12 + pulse * 2, Pal.Rgba(232, 70, 58, 0.25f * pulse));
    }

    // The haze coming in from the sea before the ash: thickest on the side the wind brings it from, over the whole prep time.
    void DrawAshHaze()
    {
        if (!AshPhase || state.Hinted("mg:ashStarted") || scene != "world") return;
        float k = Math.Clamp(mgPrepT / 75f, 0, 1);
        int reach = (int)(W * (0.25f + 0.55f * k));
        for (int x = 0; x < reach; x += 4)
        {
            float a = 0.34f * k * MathF.Pow(1 - x / (float)reach, 1.5f);
            if (a > 0.01f) pix.Fill(x, 0, 4, H, Pal.Rgba(122, 116, 106, a));
        }
    }
}
