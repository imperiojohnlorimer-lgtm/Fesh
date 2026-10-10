using Raylib_cs;

namespace Fesh;

// Beneath the clouds (Magayon.cs): the alert card, Ben's readings, the go-bag, the lahar watch, and the school as the
// evacuation centre.
partial class Game
{
    float lastMgPanelBottom;   // where the last of these panels ended (the autotest checks they fit)

    /* ---------- The alert card ---------- */
    void OpenAlertCard() { Sfx.Play("ui"); panel = "alertcard"; mode = "panel"; SetPrompt(""); state.hinted["mg:card"] = true; }

    void DrawAlertCard()
    {
        Backdrop();
        const float cw = 1040, pad = 24;
        var rows = AlertLevels.Select((l, i) => (i, name: Gfx.Wrap(l.Name, FontKind.Ui700, 20, 330), means: Gfx.Wrap(l.Means, FontKind.Ui500, 17, 560))).ToList();
        float rowsH = rows.Sum(r => Math.Max(r.name.Count * 24, r.means.Count * 22) + 18);
        var notes = Gfx.Wrap("These are PHIVOLCS's alert levels for Mayon (revised January 2018); each volcano has its own table. Baga and its station are made up for this game and borrow Mayon's. Mayon reached Level 5 in 2000 and 2001. In January 2018 it went to Level 4 while lava was already fountaining, with an 8 km danger zone.", FontKind.Note, 17, cw - pad * 2);
        float ch = pad + 44 + 30 + rowsH + 14 + notes.Count * 24 + pad;
        float x = (Gfx.LW - cw) / 2, y = Math.Max(8, (Gfx.LH - ch) / 2);
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Volcano alert levels", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        Gfx.Text($"Baga today: Level {BagaLevel}, {AlertLevels[BagaLevel].Name}", x + pad, y + pad + 42, FontKind.Ui600, 18, Pal.C("#b5523b"));
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { ClosePanels(); return; }
        float ry = y + pad + 76;
        foreach (var (i, name, means) in rows)
        {
            float h = Math.Max(name.Count * 24, means.Count * 22) + 10;
            bool now = i == BagaLevel;
            Gfx.Rect(x + pad - 6, ry - 4, cw - pad * 2 + 12, h + 4, now ? Pal.C("#fbe3b0") : i % 2 == 0 ? Pal.C("#f6edd6") : Pal.Paper, 6);
            string col = i < 2 ? "#5fb05f" : i < 4 ? "#e8a83a" : "#d8323a";
            Gfx.Circle(x + pad + 18, ry + 14, 16, Pal.C(col));
            Gfx.TextCenter(i.ToString(), x + pad + 18, ry + 2, FontKind.Ui700, 22, Color.White);
            Lines(name, x + pad + 48, ry + 2, 24, FontKind.Ui700, 20, Pal.PaperInk);
            Lines(means, x + pad + 400, ry + 3, 22, FontKind.Ui500, 17, Pal.PaperInk);
            ry += h + 8;
        }
        Lines(notes, x + pad, ry + 6, 24, FontKind.Note, 17, Muted);
        lastMgPanelBottom = y + ch;
    }

    /* ---------- Ben's readings ---------- */
    // Simplified examples. Each step has to be read right before the next (Codex: no praise for a wrong reading, and the
    // alert itself is the team's decision, not the score).
    static readonly (int row, float x)[][] QuakesToday = { new[] { (0, 0.12f), (0, 0.63f), (1, 0.28f), (1, 0.81f), (2, 0.45f), (3, 0.18f), (3, 0.72f) }, new[] { (0, 0.38f), (2, 0.66f), (3, 0.24f) } };
    static readonly int[] QuakesBefore = { 2, 7 };
    static readonly int[] GasBefore = { 300, 1100 }, GasToday = { 1100, 1800 };
    static readonly string[] ReadSteps = { "Seismograph", "Ground (GPS)", "Gas scanner", "Summit camera" };

    void OpenReadings(int round)
    {
        readings = new Readings { Round = round };
        Sfx.Play("ui");
        panel = "readings"; mode = "panel"; SetPrompt("");
    }

    void UpdateReadings(float dt)
    {
        var r = readings;
        if (r == null || r.Step != 2 || r.Swept || r.Sweep <= 0) return;
        r.Sweep = Math.Min(1, r.Sweep + dt / 2.2f);
        if (r.Sweep >= 1) r.Swept = true;
    }

    void DrawReadings()
    {
        Backdrop();
        var r = readings;
        if (r == null) { ClosePanels(); return; }
        const float cw = 1080, ch = 660, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text(r.Round == 1 ? "Today's readings" : "New readings", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        Gfx.Text("Simplified examples of what Baga's station measures.", x + pad, y + pad + 42, FontKind.Note, 17, Muted);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { readings = null; ClosePanels(); Toast("Ben: \"Come back when you're ready; the readings will wait.\""); return; }
        // The four steps along the top.
        float sx = x + pad;
        for (int i = 0; i < ReadSteps.Length; i++)
        {
            string label = $"{i + 1} {ReadSteps[i]}";
            float w = Gfx.Measure(label, FontKind.Ui700, 17) + 26;
            Gfx.Box(sx, y + pad + 70, w, 32, i == r.Step ? Pal.Lantern : i < r.Step ? Pal.C("#cfe8c0") : Pal.Sand, Pal.Ink, 2, 5);
            Gfx.Text(label, sx + 13, y + pad + 76, FontKind.Ui700, 17, Pal.Ink);
            sx += w + 8;
        }
        float ax = x + pad, ay = y + pad + 116, aw = cw - pad * 2, ah = 316;
        Gfx.Rect(ax, ay, aw, ah, Pal.C("#fbf6e8"), 6);
        float qy = ay + ah + 14;
        switch (r.Step)
        {
            case 0: DrawSeismo(r, ax, ay, aw, ah, qy); break;
            case 1: DrawGps(r, ax, ay, aw, ah, qy); break;
            case 2: DrawGasScan(r, ax, ay, aw, ah, qy); break;
            case 3: DrawSummitCam(r, ax, ay, aw, ah, qy); break;
            default:
                Gfx.TextCenter("All four read. Ben looks them over with the team in town.", ax + aw / 2, ay + ah / 2 - 14, FontKind.Ui700, 22, Pal.PaperInk);
                if (Button("Send the readings to Ben", ax + aw / 2 - 170, qy + 20, 340, 52, FontKind.Ui700, 22, Pal.Buoy, Color.White, 5))
                {
                    int round = r.Round;
                    readings = null;
                    ClosePanels();
                    FinishReadings(round);
                    return;
                }
                break;
        }
        // Ben's word on your answer, and Next once it's right.
        if (r.Note != "" && r.Step < 4)
        {
            var note = Gfx.Wrap("Ben: " + r.Note, FontKind.Ui600, 17, aw - 200);
            Gfx.Rect(ax, y + ch - pad - 74, aw - 180, 74, r.Ok ? Pal.C("#e4f3e0") : Pal.C("#f8e6dc"), 6);
            Lines(note.Take(3).ToList(), ax + 12, y + ch - pad - 68, 22, FontKind.Ui600, 17, Pal.PaperInk);
            if (r.Ok && Button("Next", x + cw - pad - 150, y + ch - pad - 62, 150, 50, FontKind.Ui700, 22, Pal.Buoy, Color.White, 5))
            {
                r.Step++; r.Ok = false; r.Note = ""; r.Picks.Clear();
                Sfx.Play("blip");
                return;
            }
        }
        lastMgPanelBottom = y + ch;
    }

    // Three answer buttons; a right one ends the step (Ben says why), a wrong one gets his explanation and a retry.
    void ReadAnswers(Readings r, float x, float y, string[] options, string right, string whyRight, string whyWrong)
    {
        float bx = x;
        foreach (var o in options)
        {
            float w = Gfx.Measure(o, FontKind.Ui700, 20) + 44;
            if (Button(o, bx, y, w, 46, FontKind.Ui700, 20, r.Ok && o == right ? Pal.C("#9fd36b") : Pal.Sand, Pal.Ink, 4, live: !r.Ok))
            {
                r.Ok = o == right;
                r.Note = r.Ok ? whyRight : whyWrong;
                Sfx.Play(r.Ok ? "right" : "wrong");
            }
            bx += w + 12;
        }
    }

    void DrawSeismo(Readings r, float ax, float ay, float aw, float ah, float qy)
    {
        var quakes = QuakesToday[r.Round - 1];
        float lx = ax + 30, lw = aw - 60;
        // The key: what a volcanic earthquake looks like on the drum, and what the surf looks like.
        Gfx.Text("Key:", ax + 20, ay + 12, FontKind.Ui700, 16, Pal.PaperInk);
        DrawTrace(ax + 70, ay + 22, 60, 0.4f, 0);
        Gfx.Text("volcanic earthquake", ax + 136, ay + 12, FontKind.Ui500, 16, Pal.PaperInk);
        DrawTrace(ax + 320, ay + 22, 60, -1, 0);
        Gfx.Text("waves on the shore (not a quake)", ax + 386, ay + 12, FontKind.Ui500, 16, Pal.PaperInk);
        Gfx.Text("Today, 24 hours, 6 hours a line", ax + aw - 260, ay + 12, FontKind.Ui600, 16, Muted);
        for (int row = 0; row < 4; row++)
        {
            float cy = ay + 80 + row * 64;
            Gfx.Line(lx, cy, lx + lw, cy, 1, Pal.C("rgba(0,0,0,0.08)"));
            DrawTraceRow(lx, cy, lw, row, quakes, r.Round);
            for (int i = 0; i < quakes.Length; i++)
                if (quakes[i].row == row && r.Marked.Contains(i)) Gfx.Circle(lx + quakes[i].x * lw, cy, 18, Pal.C("rgba(216,50,58,0.25)"));
        }
#if DEBUG
        Gfx.Seen["seismo"] = new Rectangle(lx, ay + 80 - 32, lw, 4 * 64);
#endif
        // Clicking a spike marks it; clicking the wiggles in between says so.
        if (!r.Counted && Gfx.Click(lx, ay + 48, lw, 4 * 64))
        {
            var m = Gfx.Mouse;
            int row = (int)((m.Y - (ay + 48)) / 64);
            int hit = Array.FindIndex(quakes, q => q.row == row && MathF.Abs(lx + q.x * lw - m.X) < 22);
            if (hit >= 0 && r.Marked.Add(hit)) { Sfx.Play("blip"); r.Note = ""; }
            else if (hit < 0) { r.Note = "That's just the surf: small, even wiggles all day. A volcanic earthquake is a sharp spike that dies away."; r.Ok = false; }
            if (r.Marked.Count == quakes.Length) { r.Counted = true; r.Note = ""; }
        }
        if (!r.Counted)
        {
            Gfx.Text($"Click each volcanic earthquake on today's drum. Marked: {r.Marked.Count}", ax, qy + 8, FontKind.Ui700, 20, Pal.PaperInk);
            return;
        }
        int n = quakes.Length, before = QuakesBefore[r.Round - 1];
        Gfx.Text($"Today: {n} volcanic earthquakes. Last time, over the same 24 hours: {before}. Today is...", ax, qy, FontKind.Ui700, 20, Pal.PaperInk);
        ReadAnswers(r, ax, qy + 34, new[] { "More", "Fewer", "About the same" }, r.Round == 1 ? "More" : "Fewer",
            r.Round == 1 ? $"{n}, up from {before}. Rock is cracking as something pushes underneath." : $"{n}, down from {before}. Fewer quakes, but that doesn't mean Baga is calming down: we look at every sign.",
            r.Round == 1 ? $"Count again: {n} today against {before} last time. That's more." : $"Look again: {n} today, {before} last time. That's fewer.");
    }

    // One line of the drum: the surf's small, even wiggle, and a sharp spike that dies away for each quake.
    void DrawTraceRow(float x, float cy, float w, int row, (int row, float x)[] quakes, int round)
    {
        float prevX = x, prevY = cy;
        for (int i = 1; i <= (int)w; i += 2)
        {
            float px = x + i, f = i / w;
            float v = MathF.Sin(px * 0.21f + row * 3 + round) * 2.2f + MathF.Sin(px * 0.057f) * 1.2f;
            foreach (var q in quakes)
                if (q.row == row)
                {
                    float d = (f - q.x) * w;
                    if (d >= 0 && d < 70) v += MathF.Sin(d * 1.3f) * 26 * MathF.Exp(-d / 14);
                }
            float py = cy + v;
            Gfx.Line(prevX, prevY, px, py, 1.4f, Pal.C("#1d3a5a"));
            prevX = px; prevY = py;
        }
    }

    void DrawTrace(float x, float cy, float w, float at, int seed)
    {
        float prevX = x, prevY = cy;
        for (int i = 1; i <= (int)w; i++)
        {
            float px = x + i, v = MathF.Sin(px * 0.6f + seed) * 2.2f;
            if (at >= 0) { float d = i - at * w; if (d >= 0) v += MathF.Sin(d * 1.3f) * 12 * MathF.Exp(-d / 7); }
            Gfx.Line(prevX, prevY, px, cy + v, 1.4f, Pal.C("#1d3a5a"));
            prevX = px; prevY = cy + v;
        }
    }

    void DrawGps(Readings r, float ax, float ay, float aw, float ah, float qy)
    {
        float cx = ax + 300, cy = ay + ah / 2 + 10;
        // Baga from above: the island, the cone, and three GPS marks with last week's (hollow) and today's (filled) place.
        Gfx.Circle(cx, cy, 140, Pal.C("#e8d8a8"));
        Gfx.Circle(cx, cy, 124, Pal.C("#9fc07a"));
        Gfx.Circle(cx, cy, 60, Pal.C("#8a8580"));
        Gfx.Circle(cx, cy, 14, Pal.C("#45413f"));
        float shift = r.Round == 1 ? 10 : 14;
        foreach (var a in new[] { -2.2f, -0.6f, 1.5f })
        {
            float bx = cx + MathF.Cos(a) * 92, by = cy + MathF.Sin(a) * 92, tx = bx + MathF.Cos(a) * shift, ty = by + MathF.Sin(a) * shift;
            Gfx.Circle(bx, by, 9, Pal.C("#1d3a5a")); Gfx.Circle(bx, by, 6, Pal.C("#9fc07a"));
            Gfx.Line(bx, by, tx, ty, 3, Pal.C("#d8323a"));
            Gfx.Circle(tx, ty, 7, Pal.C("#1d3a5a"));
        }
        var key = Gfx.Wrap("Hollow: where each GPS mark was last week. Filled: where it is today. The red line is how it moved. (Real moves are a few centimetres; they're drawn much bigger here.)", FontKind.Ui500, 18, aw - 520);
        Lines(key, ax + 500, ay + 40, 26, FontKind.Ui500, 18, Pal.PaperInk);
        Gfx.Text("Is Baga swelling, sinking or steady?", ax, qy, FontKind.Ui700, 20, Pal.PaperInk);
        ReadAnswers(r, ax, qy + 34, new[] { "Swelling", "Sinking", "Steady" }, "Swelling",
            r.Round == 1 ? "Every mark moved outward, away from the crater: the ground is swelling, as if something underneath is filling up." : "Still moving outward, a bit more than last time: still swelling.",
            "Look at the red lines: every mark moved outward, away from the crater. That's swelling.");
    }

    void DrawGasScan(Readings r, float ax, float ay, float aw, float ah, float qy)
    {
        int before = GasBefore[r.Round - 1], today = GasToday[r.Round - 1];
        float gx = ax + 40, gy = ay + 50, gw = 560, gh = 220;
        Gfx.Text("A scan under the plume (recorded)", gx, ay + 14, FontKind.Ui700, 18, Pal.PaperInk);
        Gfx.Line(gx, gy + gh, gx + gw, gy + gh, 2, Pal.Ink);
        Gfx.Line(gx, gy, gx, gy + gh, 2, Pal.Ink);
        Gfx.Text("along the scan", gx + gw - 120, gy + gh + 6, FontKind.Ui500, 15, Muted);
        Gfx.Text("sulfur dioxide", gx + 8, gy - 2, FontKind.Ui500, 15, Muted);
        float peak = today / 2000f;
        float Curve(float u) => peak * MathF.Exp(-MathF.Pow((u - 0.52f) / 0.16f, 2));
        float sweepTo = r.Swept ? 1 : r.Sweep;
        for (int i = 0; i < (int)gw; i += 3)
        {
            float u = i / gw, v = Curve(u) * gh;
            if (u <= sweepTo) Gfx.Rect(gx + i, gy + gh - v, 3, v, Pal.C("rgba(232,168,58,0.55)"));
            Gfx.Rect(gx + i, gy + gh - v - 2, 3, 2, Pal.C("#b5523b"));
        }
        if (r.Sweep > 0 && !r.Swept) Gfx.Line(gx + sweepTo * gw, gy, gx + sweepTo * gw, gy + gh, 2, Pal.C("#1d3a5a"));
        var why = Gfx.Wrap("This estimates how much sulfur dioxide the volcano releases, in tonnes per day. It doesn't measure how much is in the air where you are.", FontKind.Note, 17, aw - gw - 120);
        Lines(why, gx + gw + 40, ay + 40, 24, FontKind.Note, 17, Pal.PaperInk);
        if (!r.Swept)
        {
            if (r.Sweep <= 0 && Button("Sweep across the plume", ax, qy + 4, 300, 48, FontKind.Ui700, 20, Pal.Buoy, Color.White, 5)) { r.Sweep = 0.01f; Sfx.Play("blip"); }
            else if (r.Sweep > 0) Gfx.Text("Sweeping...", ax, qy + 14, FontKind.Ui700, 20, Pal.PaperInk);
            return;
        }
        // The bars: last time and today.
        float bx = gx + gw + 40, by = ay + ah - 40;
        Gfx.Rect(bx, by - before / 2000f * 140, 60, before / 2000f * 140, Pal.C("#9aa0a5"));
        Gfx.Rect(bx + 90, by - today / 2000f * 140, 60, today / 2000f * 140, Pal.C("#e8a83a"));
        Gfx.Text($"{before:N0}", bx + 4, by + 4, FontKind.Ui700, 16, Pal.PaperInk); Gfx.Text($"{today:N0}", bx + 94, by + 4, FontKind.Ui700, 16, Pal.PaperInk);
        Gfx.Text("before", bx + 6, by + 22, FontKind.Ui500, 14, Muted); Gfx.Text("today", bx + 98, by + 22, FontKind.Ui500, 14, Muted);
        Gfx.Text($"Today: about {today:N0} tonnes a day. Before: {before:N0}. Today is...", ax, qy, FontKind.Ui700, 20, Pal.PaperInk);
        ReadAnswers(r, ax, qy + 34, new[] { "More", "Less", "About the same" }, "More",
            r.Round == 1 ? $"Up from {before:N0} to {today:N0}. More sulfur dioxide often means magma is rising and letting gas out." : $"Up again, to {today:N0}. Still more gas.",
            "Compare the bars: today's is taller. That's more.");
    }

    void DrawSummitCam(Readings r, float ax, float ay, float aw, float ah, float qy)
    {
        bool night = r.Round == 2;
        float px = ax + 30, py = ay + 30, pw = 520, ph = 270;
        // The picture: the summit by day (round 1) or last night (round 2).
        Gfx.Rect(px, py, pw, ph, Pal.C(night ? "#0d1b2a" : "#9fc4dc"));
        for (int i = 0; i < 40; i++)
        {
            float f = i / 40f, half = 30 + f * 200;
            Gfx.Rect(px + pw / 2 - half, py + 90 + i * 4.5f, half * 2, 5, Pal.C(night ? "#1a1a22" : "#77716a"));
        }
        Gfx.Rect(px + pw / 2 - 28, py + 86, 56, 8, Pal.C(night ? "#2a2020" : "#45413f"));
        for (int i = 0; i < 5; i++)
        {
            float rise = (time * 18 + i * 14) % 70;
            Gfx.Rect(px + pw / 2 - 16 - rise * 0.6f, py + 70 - rise, 26 + rise / 2, 10, Pal.Rgba(220, 224, 220, (night ? 0.25f : 0.6f) * (1 - rise / 70)));
        }
        if (night)
        {
            Gfx.Circle(px + pw / 2, py + 90, 26, Pal.Rgba(255, 120, 50, 0.45f));
            Gfx.Rect(px + pw / 2 - 18, py + 86, 36, 6, Pal.C("#ffb347"));
            foreach (var (k, o) in new[] { (0.2f, -30f), (0.55f, -55f), (0.8f, 10f) })
            {
                float u = (time * 0.3f + k) % 1;
                Gfx.Rect(px + pw / 2 + o * u - 20 * u, py + 96 + u * 160, 4, 4, Pal.C("#ffd27a"));
                Gfx.Rect(px + pw / 2 + o * u - 20 * u - 2, py + 92 + u * 160, 2, 4, Pal.Rgba(255, 160, 60, 0.6f));
            }
        }
        Gfx.Text(night ? "Last night, from the crater camera" : "This morning, from the crater camera", px + 10, py + ph - 26, FontKind.Ui600, 15, Pal.C("#e8e4da"));
        Gfx.Text("What's new at the summit? Pick all you see.", ax, qy, FontKind.Ui700, 20, Pal.PaperInk);
        string[] opts = { "A glow at the crater", "Rocks rolling down", "Nothing new: just steam" };
        float bx = ax;
        foreach (var o in opts)
        {
            float w = Gfx.Measure(o, FontKind.Ui700, 18) + 40;
            bool on = r.Picks.Contains(o);
            if (Button(o, bx, qy + 34, w, 44, FontKind.Ui700, 18, on ? Pal.Lantern : Pal.Sand, Pal.Ink, 4, live: !r.Ok))
            {
                if (!r.Picks.Add(o)) r.Picks.Remove(o);
                if (o.StartsWith("Nothing") && on == false) { r.Picks.Clear(); r.Picks.Add(o); }
                else if (!o.StartsWith("Nothing")) r.Picks.Remove(opts[2]);
                Sfx.Play("blip");
            }
            bx += w + 10;
        }
        if (!r.Ok && Button("Check", bx + 10, qy + 34, 110, 44, FontKind.Ui700, 20, Pal.Buoy, Color.White, 4))
        {
            bool right = night ? r.Picks.SetEquals(new[] { opts[0], opts[1] }) : r.Picks.SetEquals(new[] { opts[2] });
            r.Ok = right;
            r.Note = right ? (night ? "A glow at the crater means hot rock right at the top. Rocks rolling down mean fresh lava is piling up and breaking off." : "Just steam, like most days. No glow, no falling rocks.")
                : night ? "Look again: there's an orange glow at the top, and glowing rocks rolling down the slope." : "Look again: only white steam.";
            Sfx.Play(right ? "right" : "wrong");
        }
    }

    // What Ben makes of the readings, with the team in town: the alert goes up, and after round 2 the order comes.
    void FinishReadings(int round)
    {
        Say B(string t) => new("Ben", t);
        if (round == 1)
        {
            state.hinted["mg:read1"] = true;
            state.gifts["mg_read1"] = state.day;
            UpdateBagaLevel();
            Save();
            Talk(new()
            {
                B("More quakes, the ground swelling and more gas: together that says magma is moving under Baga. Our team in town looked at all of it too: Level 2, Increasing Unrest."),
                B("Now's the time to pack a go-bag, while it's calm. The supply crate by my door has the things. New readings tomorrow.")
            });
            return;
        }
        state.hinted["mg:read2"] = true;
        state.hinted["mg:signs"] = true;
        state.hinted["mg:order"] = true;
        state.gifts["mg_order"] = state.day;
        mgEvacT = 0;
        UpdateBagaLevel();
        mapTexDirty = true;
        Save();
        Talk(new()
        {
            B("Only three quakes. But the glow and the rolling rocks mean fresh lava at the crater, the ground's still swelling, and the gas is up again. Signs don't all rise together: that's why we watch them all."),
            B("That's Level 3: Increased Tendency Towards Hazardous Eruption. The disaster office has ordered everyone off Baga, early, while the sea is calm. Niko's bringing his banca to the jetty."),
            B("Grab a go-bag, call Manay Mila from her garden, sign the list at the table by the jetty, then board. Nothing has to be finished before we go."),
            new("", "New clue on your Case board (Baga): The rising signs.")
        });
    }

    /* ---------- The go-bag ---------- */
    static readonly (string Name, bool Belongs, string Why)[] GoBagThings =
    {
        ("Bottled water", true, "You'll need drinking water: ash can spoil open water."),
        ("Ready-to-eat food", true, "Food that keeps, for when the shelter is busy."),
        ("Medicines and first aid", true, "Any medicines you take, and a first-aid kit."),
        ("Flashlight and batteries", true, "The power can go out."),
        ("Battery radio", true, "The radio brings the official updates."),
        ("Papers and phone numbers, in plastic", true, "Papers and numbers, sealed against the wet."),
        ("Well-fitting face masks (N95)", true, "For when you must go outside in ash."),
        ("Goggles", true, "Ash scratches your eyes."),
        ("Your best fishing rod", false, "Leave the rod: the banca has room for people and essentials."),
        ("Your aquarium", false, "The aquarium stays: far too heavy, and no help at a shelter.")
    };

    void OpenGoBag()
    {
        if (state.Hinted("mg:bag")) { Toast("Your go-bag's packed and waiting by Ben's door."); return; }
        gobag = new GoBag();
        Sfx.Play("ui");
        panel = "gobag"; mode = "panel"; SetPrompt("");
    }

    void DrawGoBag()
    {
        Backdrop();
        var g = gobag;
        if (g == null) { ClosePanels(); return; }
        const float cw = 1060, pad = 24, cardW = 190, cardH = 116, gap = 12;
        float ch = pad + 44 + 34 + 2 * (cardH + gap) + 20 + 90 + 60 + pad;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Pack a go-bag", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        Gfx.Text("Click what goes in. A real go-bag can hold more; these are the essentials here.", x + pad, y + pad + 42, FontKind.Note, 17, Muted);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { gobag = null; ClosePanels(); return; }
        float gy = y + pad + 84;
        for (int i = 0; i < GoBagThings.Length; i++)
        {
            float cx = x + pad + (i % 5) * (cardW + gap), cy = gy + (i / 5) * (cardH + gap);
            bool on = g.In.Contains(i);
            Gfx.Box(cx, cy, cardW, cardH, on ? Pal.C("#cfe8c0") : Pal.C("#fbf6e8"), on ? Pal.C("#3f7f5a") : Pal.C("#c9b48a"), on ? 3 : 2, 6);
            DrawGoBagIcon(i, cx + cardW / 2, cy + 36);
            var name = Gfx.Wrap(GoBagThings[i].Name, FontKind.Ui700, 16, cardW - 16);
            for (int l = 0; l < name.Count; l++) Gfx.TextCenter(name[l], cx + cardW / 2, cy + 66 + l * 19, FontKind.Ui700, 16, Pal.PaperInk);
#if DEBUG
            Gfx.Seen[$"gobag:{i}"] = new Rectangle(cx, cy, cardW, cardH);
#endif
            if (!g.Done && Gfx.Click(cx, cy, cardW, cardH)) { if (!g.In.Add(i)) g.In.Remove(i); Sfx.Play("blip"); g.Note = ""; }
        }
        float ny = gy + 2 * (cardH + gap) + 10;
        if (g.Note != "")
        {
            Gfx.Rect(x + pad, ny, cw - pad * 2, 80, g.Done ? Pal.C("#e4f3e0") : Pal.C("#f8e6dc"), 6);
            Lines(Gfx.Wrap(g.Note, FontKind.Ui600, 18, cw - pad * 2 - 24).Take(3).ToList(), x + pad + 12, ny + 8, 23, FontKind.Ui600, 18, Pal.PaperInk);
        }
        float by = ny + 96;
        if (!g.Done && Button("Done packing", x + cw - pad - 220, by, 220, 50, FontKind.Ui700, 22, Pal.Buoy, Color.White, 5))
        {
            int missing = Enumerable.Range(0, GoBagThings.Length).FirstOrDefault(i => GoBagThings[i].Belongs && !g.In.Contains(i), -1);
            int extra = g.In.FirstOrDefault(i => !GoBagThings[i].Belongs, -1);
            if (extra >= 0) { g.Note = GoBagThings[extra].Why; Sfx.Play("wrong"); }
            else if (missing >= 0) { g.Note = $"Still missing: {GoBagThings[missing].Name.ToLowerInvariant()}. {GoBagThings[missing].Why}"; Sfx.Play("wrong"); }
            else
            {
                g.Done = true;
                g.Note = "That's a go-bag. Keep it by the door, ready to grab. Masks often don't fit children well, so staying inside out of the ash comes first.";
                state.hinted["mg:bag"] = true;
                // Only a bag packed before the order counts as early in the report (Codex).
                if (!MgOrder) state.hinted["mg:r:bagEarly"] = true;
                Save();
                Sfx.Play("right");
            }
        }
        if (g.Done && Button("Close", x + cw - pad - 140, by, 140, 50, FontKind.Ui700, 22, Pal.Sand, Pal.Ink, 5)) { gobag = null; ClosePanels(); return; }
        lastMgPanelBottom = y + ch;
    }

    static void DrawGoBagIcon(int i, float x, float y)
    {
        switch (i)
        {
            case 0: Gfx.Rect(x - 8, y - 18, 16, 34, Pal.C("#9fd8f0"), 4); Gfx.Rect(x - 4, y - 24, 8, 6, Pal.C("#3f7fd0")); break;
            case 1: Gfx.Rect(x - 18, y - 10, 36, 22, Pal.C("#e8a83a"), 4); Gfx.Rect(x - 18, y - 10, 36, 6, Pal.C("#c0392b")); break;
            case 2: Gfx.Rect(x - 18, y - 14, 36, 28, Pal.C("#f2efe6"), 4); Gfx.Rect(x - 3, y - 10, 6, 20, Pal.C("#d8323a")); Gfx.Rect(x - 10, y - 3, 20, 6, Pal.C("#d8323a")); break;
            case 3: Gfx.Rect(x - 18, y - 6, 28, 12, Pal.C("#5e6468"), 3); Gfx.Triangle(x + 10, y - 8, x + 22, y - 14, x + 22, y + 14, Pal.C("#f3c25b")); break;
            case 4: Gfx.Rect(x - 18, y - 12, 36, 26, Pal.C("#3a3f45"), 4); Gfx.Circle(x - 6, y + 1, 7, Pal.C("#9aa0a5")); Gfx.Rect(x + 6, y - 22, 2, 12, Pal.C("#9aa0a5")); break;
            case 5: Gfx.Rect(x - 16, y - 16, 32, 30, Pal.C("#dfe8f0"), 2); Gfx.Rect(x - 12, y - 10, 24, 2, Pal.C("#8a8478")); Gfx.Rect(x - 12, y - 4, 18, 2, Pal.C("#8a8478")); Gfx.Rect(x - 12, y + 2, 20, 2, Pal.C("#8a8478")); break;
            case 6: Gfx.Rect(x - 16, y - 10, 32, 20, Pal.C("#f2efe6"), 8); Gfx.Line(x - 22, y - 4, x - 16, y - 2, 2, Pal.C("#9aa0a5")); Gfx.Line(x + 16, y - 2, x + 22, y - 4, 2, Pal.C("#9aa0a5")); break;
            case 7: Gfx.Circle(x - 9, y, 9, Pal.C("#9fd8f0")); Gfx.Circle(x + 9, y, 9, Pal.C("#9fd8f0")); Gfx.Rect(x - 20, y - 2, 40, 3, Pal.C("#3a3f45")); break;
            case 8: Gfx.Line(x - 24, y + 14, x + 24, y - 16, 3, Pal.C("#8a6440")); Gfx.Circle(x - 14, y + 8, 5, Pal.C("#5e6468")); break;
            default: Gfx.Rect(x - 22, y - 14, 44, 28, Pal.C("#9fd8f0"), 3); Gfx.Rect(x - 6, y - 4, 10, 5, Pal.C("#e8a83a")); break;
        }
    }

    /* ---------- The lahar watch ---------- */
    void OpenLaharWatch()
    {
        laharWatch = new LaharWatch { Live = state.weather != "clear" };
        Sfx.Play("ui");
        panel = "lahar"; mode = "panel"; SetPrompt("");
    }

    // Closing it doesn't stop the warning: Ben sends it (without credit to you), and the case still closes. Every way out
    // goes through ClosePanels (Codex: another panel's key, or build mode, used to leave it unfinished).
    void CloseLaharWatch() => ClosePanels();

    void LaharWatchClosed()
    {
        var w = laharWatch;
        laharWatch = null;
        if (w == null || MgDone) return;
        if (!w.Sent) Toast("Ben sends the lahar warning himself: keep out of the channel and the river mouth.", 5);
        FinishMagayon(w.Sent && !w.ByBen);
    }

    void UpdateLaharWatch(float dt)
    {
        var w = laharWatch;
        if (w == null) return;
        w.Open += dt;
        if (w.T >= 0) w.T += dt;
        // Public safety doesn't wait for anyone: after a while Ben closes the channel and sends the warning himself.
        if (!w.Sent && w.Open > 60) { w.Closed.Add("crossing"); w.Closed.Add("mouth"); w.Sent = true; w.ByBen = true; w.T = 0; w.Note = "Ben: \"I've sent the warning. Next time, you mark the channel and send it.\""; Sfx.Play("whistle"); }
    }

    void DrawLaharWatch()
    {
        Backdrop();
        var w = laharWatch;
        if (w == null) { ClosePanels(); return; }
        const float cw = 1120, ch = 640, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Lahar watch", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        Gfx.Text(w.Live ? "Live from Baga's station: it's raining on the fresh ash." : "Recorded at Baga's station during the last rain.", x + pad, y + pad + 42, FontKind.Note, 17, Muted);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { CloseLaharWatch(); return; }
        // The map of Baga's north-east, with the three places to decide about.
        float mx = x + pad, my = y + pad + 80, mw = 520, mh = 420;
        DrawBagaMap(mx, my, mw, mh, w);
        // The camera: the channel, and the lahar once the warning's out.
        float vx = x + pad + mw + 30, vy = my, vw = cw - pad * 2 - mw - 30, vh = 300;
        DrawLaharCam(vx, vy, vw, vh, w);
        float ty = vy + vh + 16;
        string step = !w.Sent ? "Mark the places to close: lahars rush down channels, and can spill past the banks where a channel meets the sea."
            : w.T < 6 ? "A lahar: mud, ash and rock, moving fast. Look: it spills past the banks at the mouth."
            : "Everyone's at the shelter: the families, Ma'am Isay's class, Manay Mila and Ben. Nobody's on Baga.";
        Lines(Gfx.Wrap(step, FontKind.Ui600, 18, vw).Take(3).ToList(), vx, ty, 23, FontKind.Ui600, 18, Pal.PaperInk);
        if (w.Note != "")
        {
            var note = Gfx.Wrap(w.Note, FontKind.Ui600, 16, vw - 20);
            Gfx.Rect(vx, ty + 76, vw, 20 + note.Count * 20, w.Note.StartsWith("Ben: \"I've") || w.Note.StartsWith("That's the way") ? Pal.C("#f8e6dc") : Pal.C("#e4f3e0"), 6);
            Lines(note, vx + 10, ty + 86, 20, FontKind.Ui600, 16, Pal.PaperInk);
        }
        float by = y + ch - pad - 52;
        if (!w.Sent && Button("Radio the warning", vx, by, 260, 52, FontKind.Ui700, 22, w.Closed.Count == 2 ? Pal.Buoy : Pal.Sand, w.Closed.Count == 2 ? Color.White : Pal.Ink, 5))
        {
            if (w.Closed.Count < 2) { w.Note = "Mark the channel crossing and the river mouth closed first."; Sfx.Play("wrong"); }
            else { w.Sent = true; w.T = 0; w.Note = "Ben (on the radio): \"Lahar warning for Baga: keep out of the channel and the river mouth.\""; Sfx.Play("whistle"); }
        }
        if (w.Sent && w.T >= 6 && Button("Close the case", vx, by, 240, 52, FontKind.Ui700, 22, Pal.Buoy, Color.White, 5)) { ClosePanels(); return; }
        lastMgPanelBottom = y + ch;
    }

    // Baga's north half from above, drawn from the real tiles: the shore, the jetty, the houses, the cone and its danger
    // zone, the channel, and the three places to decide about.
    void DrawBagaMap(float mx, float my, float mw, float mh, LaharWatch w)
    {
        const float wx0 = 2180, wy0 = 312;
        float s = Math.Min(mw / 250, mh / 205);
        (float, float) P(float wx, float wy) => (mx + (wx - wx0) * s, my + (wy - wy0) * s);
        Gfx.Rect(mx, my, mw, mh, Pal.C("#2f6f98"), 6);
        var clip = Gfx.S(mx, my, mw, mh);
        Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)MathF.Ceiling(clip.Width), (int)MathF.Ceiling(clip.Height));
        try
        {
            for (int ty = (int)(wy0 / T); ty <= (int)((wy0 + mh / s) / T); ty++)
                for (int tx = (int)(wx0 / T); tx <= (int)((wx0 + mw / s) / T); tx++)
                {
                    char c = worldMap[ty, tx];
                    string col = c == '~' ? "#2f6f98" : c == 'w' ? "#4a98c0" : c == 's' ? "#e8d8a8" : c == 'b' ? "#9a6a3a" : "#9fc07a";
                    var (px, py) = P(tx * T, ty * T);
                    Gfx.Rect(px, py, T * s + 0.6f, T * s + 0.6f, Pal.C(col));
                }
            // The permanent danger zone (dashed, roughly) and the cone.
            var (kx, ky) = P(PdzX, PdzY);
            for (int i = 0; i < 48; i += 2)
            {
                float a0 = i / 48f * MathF.Tau, a1 = (i + 1) / 48f * MathF.Tau;
                Gfx.Line(kx + MathF.Cos(a0) * PdzRX * s, ky + MathF.Sin(a0) * PdzRY * s, kx + MathF.Cos(a1) * PdzRX * s, ky + MathF.Sin(a1) * PdzRY * s, 3, Pal.C("#d8323a"));
            }
            var (cx, cy) = P(ConeX, ConeBaseY - 25);
            Gfx.Circle(cx, cy, 40 * s, Pal.C("#8a8580"));
            Gfx.Circle(cx, cy, 16 * s, Pal.C("#6d6862"));
            Gfx.Circle(cx, cy, 6 * s, Pal.C("#45413f"));
            // The channel, with mud in it once the lahar has come.
            for (int i = 1; i < LaharChannel.Length; i++)
            {
                var (ax, ay) = P(LaharChannel[i - 1].x, LaharChannel[i - 1].y);
                var (bx, by) = P(LaharChannel[i].x, LaharChannel[i].y);
                Gfx.Line(ax, ay, bx, by, 7 * s, Pal.C("#5d554d"));
                Gfx.Line(ax, ay, bx, by, 4.5f * s, w.T >= 0 ? Pal.C("#8a7a62") : Pal.C("#9a9288"));
            }
            foreach (var (hx, hy, c) in new[] { (MilaX, MilaY, "#c0392b"), (BenX, BenY, "#2a8a8a") })
            {
                var (px, py) = P(hx - 14, hy - 20);
                Gfx.Rect(px, py, 28 * s, 16 * s, Pal.C(c), 2);
            }
        }
        finally { Raylib.EndScissorMode(); }
        Gfx.Rect(mx + 8, my + 8, 200, 26, Pal.C("rgba(16,36,58,0.8)"), 4);
        Gfx.Text("Baga, the north side", mx + 16, my + 12, FontKind.Ui700, 16, Color.White);
        // The route to the jetty stays open; the channel crossing and the mouth are what to close.
        var pins = new[] { ("crossing", "Channel crossing", P(2386, 408)), ("mouth", "River mouth", P(LaharMouthX, LaharMouthY)), ("route", "Route to the jetty", P(2304, 404)) };
        foreach (var (id, label, (px, py)) in pins)
        {
            bool closed = w.Closed.Contains(id);
            Gfx.Circle(px, py, 16, Pal.Ink);
            Gfx.Circle(px, py, 13, Pal.C(closed ? "#d8323a" : id == "route" ? "#2f8a4a" : "#f3c25b"));
            if (closed) { Gfx.Line(px - 7, py - 7, px + 7, py + 7, 4, Color.White); Gfx.Line(px - 7, py + 7, px + 7, py - 7, 4, Color.White); }
            float tw = Gfx.Measure(label, FontKind.Ui700, 15) + 12, lx = Math.Clamp(px - tw / 2, mx + 4, mx + mw - tw - 4), ly = id == "mouth" ? py - 42 : py + 18;
            Gfx.Rect(lx, ly, tw, 22, Pal.C("rgba(16,36,58,0.85)"), 4);
            Gfx.Text(label, lx + 6, ly + 3, FontKind.Ui700, 15, Color.White);
#if DEBUG
            Gfx.Seen[$"lahar:{id}"] = new Rectangle(px - 16, py - 16, 32, 32);
#endif
            if (!w.Sent && Gfx.Click(px - 18, py - 18, 36, 36))
            {
                if (id == "route") { w.Note = "That's the way to the jetty, away from the channel: keep it open."; Sfx.Play("wrong"); }
                else if (w.Closed.Add(id)) { w.Note = id == "crossing" ? "Channel crossing closed." : "River mouth closed: a lahar can spread out past the banks there."; Sfx.Play("blip"); }
            }
        }
    }

    void DrawLaharCam(float vx, float vy, float vw, float vh, LaharWatch w)
    {
        Gfx.Rect(vx, vy, vw, vh, Pal.C(w.Live ? "#5a6a72" : "#6a6a62"), 6);
        // A grey slope with the channel cut down it to the beach and the sea.
        Gfx.Triangle(vx, vy + 40, vx + vw * 0.7f, vy + vh, vx, vy + vh, Pal.C("#6f6a62"));
        Gfx.Rect(vx, vy + vh - 40, vw, 40, Pal.C("#3a7fa8"));
        Gfx.Rect(vx + vw * 0.55f, vy + vh - 58, vw * 0.45f, 22, Pal.C("#d8c49a"));
        float ax = vx + 40, ay = vy + 60, bx = vx + vw * 0.66f, by = vy + vh - 50;
        Gfx.Line(ax, ay, bx, by, 18, Pal.C("#4d453d"));
        Gfx.Line(ax, ay, bx, by, 12, Pal.C("#7d746a"));
        if (w.T >= 0)
        {
            float k = Math.Min(1, w.T / 4);
            float hx = ax + (bx - ax) * k, hy = ay + (by - ay) * k;
            Gfx.Line(ax, ay, hx, hy, 14, Pal.C("#8a7a62"));
            for (int i = 0; i < 8; i++)
            {
                float u = (time * 1.4f + i * 0.13f) % 1 * k;
                Gfx.Circle(ax + (bx - ax) * u, ay + (by - ay) * u, 4, Pal.C("#5a4a3a"));
            }
            // Spilling past the banks at the mouth.
            if (w.T > 3.5f)
            {
                float s = Math.Min(1, (w.T - 3.5f) / 2);
                Gfx.Circle(bx, by, 10 + s * 46, Pal.Rgba(138, 122, 98, 0.85f));
            }
        }
        // Rain streaks while it's live.
        if (w.Live)
            for (int i = 0; i < 30; i++)
            {
                float rx = vx + (i * 37 % (int)vw), ry = vy + ((time * 300 + i * 53) % vh);
                Gfx.Line(rx, ry, rx - 3, ry + 10, 1, Pal.Rgba(220, 230, 240, 0.5f));
            }
        Gfx.Text(w.Live ? "LIVE  channel camera" : "RECORDED  channel camera", vx + 10, vy + 8, FontKind.Ui700, 15, Color.White);
    }

    /* ---------- The school as the evacuation centre ---------- */
    static readonly List<Build> SchoolRoom = new()
    {
        new() { id = "table", x = 3, y = 9 }, new() { id = "table", x = 4, y = 9 },
        new() { id = "table", x = 11, y = 2 }, new() { id = "table", x = 12, y = 2 },
        new() { id = "rug", x = 5, y = 5 }, new() { id = "rug", x = 15, y = 5 }, new() { id = "rug", x = 18, y = 7 },
        new() { id = "lamp", x = 1, y = 10 }, new() { id = "lamp", x = 22, y = 10 }
    };

    IEnumerable<Box> SchoolRoomSolids() => scene != "house:school" ? Enumerable.Empty<Box>() : new[] { new Box(WaterCrateX - 7, WaterCrateY - 6, 14, 7) };

    Target SchoolRoomTarget()
    {
        float x = player.X, y = player.Y;
        if (Dist(x, y, RoomDoorWX, RoomDoorWY) < 14) return new Target { Type = "exit", Label = AshFalling ? "Go outside (ash is falling)" : "Go outside" };
        if (AshPhase)
        {
            for (int i = 0; i < ShutterSpots.Length; i++)
                if (!ShutterClosed(i) && Dist(x, y, ShutterSpots[i].x, ShutterSpots[i].y) < 14) return new Target { Type = "shutter", Tx = i, Label = "Close the shutter" };
            if (AshFalling)
            {
                if (mgWater > 0)
                    for (int i = 0; i < FamilyMats.Length; i++)
                        if (!FamilyHasWater(i) && Dist(x, y, FamilyMats[i].x, FamilyMats[i].y + 8) < 18) return new Target { Type = "givewater", Tx = i, Label = "Give this family sealed water" };
                if (Dist(x, y, WaterCrateX, WaterCrateY + 4) < 14 && Enumerable.Range(0, 3).Any(i => !FamilyHasWater(i)) && mgWater == 0)
                    return new Target { Type = "takewater", Label = "Take sealed water for the families" };
                if (!state.Hinted("mg:desk") && !state.Hinted("mg:inHelped") && Dist(x, y, DeskX + 5, DeskY + 4) < 16) return new Target { Type = "desk", Label = "Register everyone at the desk" };
            }
        }
        if (Dist(x, y, DisplayX, DisplayY) < 16)
            return LaharWatchDue && MgRained ? new Target { Type = "display", Label = "Watch Baga's channel on the display" }
                : new Target { Type = "info", Label = LaharWatchDue ? "The display: Baga's station camera. No rain on Baga yet" : "The display: Baga's station camera" };
        if (BagaFolkAtSchool && Dist(x, y, 74, 84) < 14) return new Target { Type = "islander", Id = "mila", Label = MilaLabel() };
        if (BagaFolkAtSchool && Dist(x, y, 146, 40) < 14) return new Target { Type = "islander", Id = "ben", Label = BenLabel() };
        if (AshPhase && Dist(x, y, 92, 104) < 14) return new Target { Type = "info", Label = "Ma'am Isay: \"Everyone in, and shutters closed!\"" };
        return null;
    }

    // People and things in the shelter, drawn with the room.
    void AddSchoolRoomObjects(List<(float y, Action draw)> list)
    {
        list.Add((WaterCrateY, () =>
        {
            pix.Rect(WaterCrateX - 7, WaterCrateY - 8, 14, 8, "#9a6a3a"); pix.Rect(WaterCrateX - 7, WaterCrateY - 8, 14, 1, "#b58250");
            for (int i = 0; i < 4; i++) pix.Rect(WaterCrateX - 6 + i * 3, WaterCrateY - 11, 2, 3, "#9fd8f0");
        }));
        // The families on their mats, each with a water bottle once they've got one.
        var looks = new[] { new Look { skin = 1, shirt = 3, hair = 0, hat = 0 }, new Look { skin = 2, shirt = 5, hair = 1, hat = 0 }, new Look { skin = 3, shirt = 1, hair = 1, hat = 0 } };
        for (int i = 0; i < FamilyMats.Length && AshPhase; i++)
        {
            int k = i;
            var (fx, fy) = FamilyMats[k];
            list.Add((fy + 6, () =>
            {
                LookData.DrawPerson(pix, looks[k], (int)fx - 4, (int)fy + 6, "down", 0, bob: -3);
                DrawKid((px, py, pw, ph, c) => pix.Rect(px, py, pw, ph, c), (int)fx + 5, (int)fy + 6, k + 1, 0, time);
                if (FamilyHasWater(k)) { pix.Rect(fx + 9, fy - 1, 2, 5, "#9fd8f0"); pix.Rect(fx + 9, fy - 2, 2, 1, "#3f7fd0"); }
            }));
        }
        // Ma'am Isay's class on the middle mat, Ma'am Isay at the desk, and the folk from Baga.
        // While the ash comes and falls, the village shelters here too: Ma'am Isay and her class (not outside meanwhile).
        if (AshPhase)
        {
            list.Add((70, () => { for (int k = 0; k < 3; k++) DrawKid((px, py, pw, ph, c) => pix.Rect(px, py, pw, ph, c), 95 + k * 8, 70, k, AshFalling ? 2 : 0, time); }));
            list.Add((104, () => DrawIsayFigure(pix, 92, 104, "down", 0, 0, (time + 3) % 4.3f < 0.12f)));
        }
        if (BagaFolkAtSchool)
        {
            list.Add((84, () => DrawMila(pix, 74, 84, "down", 0)));
            list.Add((40, () => DrawBen(pix, 146, 40, "left", 0)));
        }
        // Ben's display on the back table.
        list.Add((29, () =>
        {
            pix.Rect(DisplayX - 9, 13, 18, 12, "#2a2f35"); pix.Rect(DisplayX - 8, 14, 16, 10, LaharWatchDue && MgRained ? "#5a6a72" : "#3a5a72");
            pix.Rect(DisplayX - 6, 20, 12, 2, "#7d746a");
            if ((time * 1.5f) % 1 < 0.5f && LaharWatchDue && MgRained) pix.Rect(DisplayX + 6, 15, 1, 1, "#ff4a3a");
            pix.Rect(DisplayX + 10, 18, 4, 6, "#3a3f45");
        }));
    }

    // The shutters over the school's windows, closed for the ash.
    void DrawSchoolShutters()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!(AshPhase && ShutterClosed(i)) && !(AshFalling)) continue;
            int X = i == 0 ? 3 * T : (SchoolW - 4) * T;
            pix.Rect(X, 2, 10, 12, "#7a5a3e");
            for (int j = 0; j < 4; j++) pix.Rect(X, 4 + j * 3, 10, 1, "#5b3a24");
        }
        // The blackboard, with "Evacuation centre" in chalk lines.
        pix.Rect(70, 4, 50, 12, "#6b4a2b"); pix.Rect(71, 5, 48, 10, "#2f4a3a");
        pix.Rect(74, 7, 20, 1, "#e8f0e8"); pix.Rect(74, 10, 28, 1, "#e8f0e8"); pix.Rect(98, 7, 14, 1, "#e8f0e8");
    }
}
