using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The tide (1.17). The sea rises and falls twice every lunar day (a low every 12 h 25 min), so each low tide comes about
// 50 minutes later than the day before. How far it goes out follows the moon: around a new or a full moon the sun and the
// moon pull in line and the tides are big (spring tides: the highest highs and the lowest lows); at a half moon they pull
// at right angles and the tides are small (neap tides). The moon is the same MoonPhase the clock and the fish use.
// Habagat's beaches show it: at low tide the water draws back off the sand (painted over the base layer, never baked),
// and at a big spring low the shallows beside the beaches dry right out ("flats"), so you can walk out and glean where it's
// usually knee-deep. Lola Pacing teaches it: the tide table (a panel), a riddle a day, and a reef walk at the lowest tide.
sealed class TideRiddle { public string Kind, Ask, Reply; public int Answer; public bool Done, Right; public float ClickM = -1; }

partial class Game
{
    const float TidePeriod = 745.2f;                 // minutes from one low tide to the next
    const float FirstLow = 30;                       // day 1's first low tide, in minutes after 06:00
    const float GleanLevel = -0.45f, FlatLevel = -0.8f;
    const float TideMetres = 0.9f;                   // a tide of 1.0 is 0.9 m from the average sea level

    // Minutes since 06:00 on day 1. A night belongs to the day before it, like everything else on the clock.
    static float TideMinute(int day, float clock) => (day - 1) * 1440f + (clock >= DawnMin ? clock - DawnMin : clock + 1440 - DawnMin);
    float TideNow => TideMinute(state.day, state.clock);
    static (int day, float clock) FromTideMinute(float m)
    {
        int day = 1 + (int)MathF.Floor(m / 1440);
        float rem = m - (day - 1) * 1440;
        return (day, (rem + DawnMin) % 1440);
    }

    // How big the tides are (0.5 at a half moon to 1 at a new or full moon), smooth through the day. The moon's phase is a
    // whole number at 18:00 on its day.
    static float TideRange(float m)
    {
        float c = MathF.Cos((m / 1440f + 3.5f) * MathF.PI / 4);
        return 0.5f + 0.5f * c * c;
    }
    // The height of the sea, -1 (a spring low) to 1, at a tide minute.
    static float TideAt(float m) => -TideRange(m) * MathF.Cos(MathF.Tau * (m - FirstLow) / TidePeriod);
    float SeaLevel => TideAt(TideNow);
    static int LowIndex(float m) => (int)MathF.Round((m - FirstLow) / TidePeriod);
    static float LowAt(int n) => FirstLow + n * TidePeriod;
    static float LowDepth(int n) => TideRange(LowAt(n));   // 1 is as low as it goes

    // Gleaning: while the sea is below GleanLevel. Each low tide is its own (the nearest low's number), with a quota from
    // how far it goes out: 4 finds at a neap tide up to 16 at a spring tide (half as many again with the basket).
    bool LowTide => SeaLevel < GleanLevel;
    string GleanTide => $"t{LowIndex(TideNow)}";
    int GleanQuota
    {
        get
        {
            int q = (int)MathF.Round(4 + 12 * (LowDepth(LowIndex(TideNow)) - 0.5f) / 0.5f);
            return Wears("gleaner_basket") ? (int)MathF.Round(q * 1.5f) : q;
        }
    }

    // When a low tide's flats are uncovered (or with flat: true, dry right out), as tide minutes.
    static (float start, float end) LowWindow(int n, bool flat = false)
    {
        float c = LowAt(n), level = flat ? FlatLevel : GleanLevel;
        if (TideAt(c) >= level) return (c, c);
        float s = c, e = c;
        while (TideAt(s - 2) < level && c - s < TidePeriod / 2) s -= 2;
        while (TideAt(e + 2) < level && e - c < TidePeriod / 2) e += 2;
        return (s, e);
    }

    static string TideSize(float depth) => depth > 0.85f ? "a big spring tide" : depth < 0.62f ? "a small neap tide" : "a middling tide";

    // "18:40", or "tomorrow 06:10", or "day 9, 06:10", from today.
    string TideClock(float m)
    {
        var (day, clock) = FromTideMinute(m);
        return day == state.day ? ClockText(clock) : day == state.day + 1 ? $"tomorrow {ClockText(clock)}" : $"day {day}, {ClockText(clock)}";
    }

    // The clock's tooltip line about the tide (on Habagat, or anywhere with the tide watch).
    string TideLine()
    {
        float now = TideNow;
        int n = LowIndex(now);
        if (LowTide) return $"Low tide until {TideClock(LowWindow(n).end)}: {TideSize(LowDepth(n))}";
        int next = LowAt(n) > now ? n : n + 1;
        return $"Next low tide {TideClock(LowAt(next))}: {TideSize(LowDepth(next))}";
    }

    /* ---------- The flats ---------- */
    // Habagat's shallow tiles right beside a beach. They dry out at a spring low, and you can walk on them.
    HashSet<(int, int)> flatTiles;
    const int FlatBoxX = 0, FlatBoxY = (HabagatTop - 4) * T, FlatBoxW = EastStart * T, FlatBoxH = ROWS * T - FlatBoxY;
    byte[] flatPx;          // per pixel in the box: how far a flat's water pixel is from the land (255: not a flat)

    HashSet<(int, int)> Flats()
    {
        if (flatTiles != null) return flatTiles;
        flatTiles = new();
        if (worldMap == null) return flatTiles;
        for (int y = HabagatTop - 4; y < ROWS - 1; y++)
            for (int x = 1; x < EastStart; x++)
            {
                if (worldMap[y, x] != 'w' || biome[y, x] != 6) continue;
                if (new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(o => Shore(GroundAt(x + o.Item1, y + o.Item2)))) flatTiles.Add((x, y));
            }
        // How far each water pixel of a flat is from the sand, so the water can draw back a pixel at a time.
        flatPx = new byte[FlatBoxW * FlatBoxH];
        Array.Fill(flatPx, (byte)255);
        var queue = new Queue<(int x, int y)>();
        for (int py = 0; py < FlatBoxH; py++)
            for (int px = 0; px < FlatBoxW; px++)
            {
                int wx = FlatBoxX + px, wy = FlatBoxY + py;
                if (!flatTiles.Contains((wx / T, wy / T))) continue;
                if (!Wet(ShapePx(wx, wy))) continue;
                bool edge = false;
                for (int oy = -1; oy <= 1 && !edge; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (Shore(ShapePx(wx + ox, wy + oy))) { edge = true; break; }
                if (edge) { flatPx[py * FlatBoxW + px] = 1; queue.Enqueue((px, py)); }
            }
        while (queue.Count > 0)
        {
            var (px, py) = queue.Dequeue();
            byte d = flatPx[py * FlatBoxW + px];
            if (d >= 30) continue;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = px + ox, ny = py + oy;
                    if (nx < 0 || ny < 0 || nx >= FlatBoxW || ny >= FlatBoxH || flatPx[ny * FlatBoxW + nx] <= d + 1) continue;
                    int wx = FlatBoxX + nx, wy = FlatBoxY + ny;
                    if (!flatTiles.Contains((wx / T, wy / T)) || !Wet(ShapePx(wx, wy))) continue;
                    flatPx[ny * FlatBoxW + nx] = (byte)(d + 1);
                    queue.Enqueue((nx, ny));
                }
        }
        return flatTiles;
    }

    bool IsFlat(int tx, int ty) => scene == "world" && Flats().Contains((tx, ty));
    bool FlatsDry => SeaLevel <= FlatLevel;
    bool OnFlat => IsFlat((int)MathF.Floor(player.X / T), (int)MathF.Floor((player.Y - 1.5f) / T));
    // You can walk a flat while it's dry. Caught out on one by the rising tide, you can still move about on the flat
    // tiles under your feet (every flat touches the beach, so you can always step back ashore), but not onto others.
    bool FlatOk(int tx, int ty) => IsFlat(tx, ty) && (FlatsDry || UnderFeet(tx, ty));
    bool UnderFeet(int tx, int ty)
    {
        foreach (var (ax, ay) in new[] { (player.X - 3, player.Y - 3), (player.X + 2.9f, player.Y - 3), (player.X - 3, player.Y), (player.X + 2.9f, player.Y) })
            if ((int)MathF.Floor(ax / T) == tx && (int)MathF.Floor(ay / T) == ty) return true;
        return false;
    }
    bool FlatOpen(int tx, int ty) => IsFlat(tx, ty) && FlatsDry;

    // How many pixels the water has drawn back off the sand: none until the tide is well down, a whole flat at FlatLevel.
    float TideDryPx => Math.Clamp((-SeaLevel - 0.25f) / (-FlatLevel - 0.25f) * 11, 0, 11);

    bool TideDry(int wx, int wy)
    {
        if (scene != "world" || flatPx == null) return false;
        int px = wx - FlatBoxX, py = wy - FlatBoxY;
        if (px < 0 || py < 0 || px >= FlatBoxW || py >= FlatBoxH) return false;
        byte d = flatPx[py * FlatBoxW + px];
        return d != 255 && d <= TideDryPx;
    }

    static readonly Color[] FlatSand = { Pal.C("#b89a68"), Pal.C("#c4a874"), Pal.C("#cfb582") };
    static readonly Color FlatPool = Pal.C("#4ea3bf"), FlatWeed = Pal.C("#5f7a3a"), FlatRock = Pal.C("#7d7466");

    // Wet sand where the water has gone out, tide pools and weed on it, and a line of foam at the new edge.
    void DrawTideFlats(float t)
    {
        if (scene != "world" || player.Y < FlatBoxY - H) return;
        Flats();
        float dry = TideDryPx;
        if (dry < 0.5f) return;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        var foam = Pal.Rgba(236, 248, 252, 0.75f);
        foreach (var (tx, ty) in flatTiles)
        {
            if (tx < vx0 || tx > vx1 || ty < vy0 || ty > vy1) continue;
            for (int py = 0; py < T; py++)
                for (int px = 0; px < T; px++)
                {
                    int wx = tx * T + px, wy = ty * T + py;
                    int bx = wx - FlatBoxX, by = wy - FlatBoxY;
                    if (bx < 0 || by < 0 || bx >= FlatBoxW || by >= FlatBoxH) continue;
                    byte d = flatPx[by * FlatBoxW + bx];
                    if (d == 255) continue;
                    if (d <= dry)
                    {
                        float v = VNoise(wx, wy, 6, 201);
                        Color c = v > 0.74f ? FlatPool : d >= dry - 1.2f ? FlatSand[0] : FlatSand[v < 0.4f ? 1 : 2];
                        if (Pix.Hash(wx, wy, 203) < 0.015) c = FlatWeed;
                        else if (Pix.Hash(wx / 2, wy / 2, 204) < 0.01) c = FlatRock;
                        else if (c.Equals(FlatPool) && (t * 0.6 + Pix.Hash(wx, wy, 205)) % 1 < 0.04) c = Pal.C("#d8f0f8");
                        pix.Rect(wx, wy, 1, 1, c);
                    }
                    else if (d <= dry + 1.5f && (Pix.Hash(wx / 2, wy / 2, 5) + t * 0.3) % 1 < 0.6) pix.Rect(wx, wy, 1, 1, foam);
                }
        }
    }

    /* ---------- The tide table ---------- */
    TideRiddle riddle;          // Lola Pacing's question, while the tide table is open for it
    float tideHoverM = -1;

    void OpenTides(TideRiddle r = null)
    {
        riddle = r;
        Sfx.Play("ui");
        panel = "tides";
        mode = "panel";
        SetPrompt("");
    }

    // The tide table's three days start at 06:00 today.
    float TideTableStart => TideMinute(state.day, DawnMin);
    float lastTidesBottom;      // where the tide table's text ended (the autotest checks it fits)

    void DrawTides()
    {
        Backdrop();
        const float cw = 1160, ch = 660, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Tide table", x + pad, y + pad, FontKind.Ui700, 34, Pal.PaperInk);
        Gfx.Text("The sea on Habagat's flats, for three days. Below the sandy line you can glean.", x + pad, y + pad + 44, FontKind.Note, 18, Muted);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { CloseTides(); return; }

        // Lola's question, and her answer once you've clicked.
        float gy0 = y + pad + 84;
        if (riddle != null)
        {
            Gfx.Box(x + pad, gy0, 760, 64, riddle.Done ? (riddle.Right ? Pal.C("#e4f3e0") : Pal.C("#f8e6dc")) : NoteBg, Pal.C("#c9a640"), 2, 5);
            Gfx.Text("Lola Pacing asks", x + pad + 12, gy0 + 6, FontKind.Ui700, 15, Pal.C("#8a5fb5"));
            var ask = Gfx.Wrap(riddle.Done ? riddle.Reply : riddle.Ask, FontKind.Ui600, 16, 736);
            if (ask.Count > 2) ask = new List<string> { ask[0], Gfx.Ellipsize(ask[1] + " " + ask[2], FontKind.Ui600, 16, 736) };
            Lines(ask, x + pad + 12, gy0 + 24, 19, FontKind.Ui600, 16, Pal.PaperInk);
            gy0 += 74;
        }

        // The graph.
        float gx = x + pad + 40, gw = 720, gh = riddle != null ? 300 : 360, gy = gy0 + 30;
        float m0 = TideTableStart, span = 3 * 1440;
        float X(float m) => gx + (m - m0) / span * gw;
        float Y(float h) => gy + gh / 2 - h * gh * 0.42f;
        Gfx.Rect(gx, gy, gw, gh, Pal.C("#dcecf2"), 4);
        // Nights shaded, and each day's moon and how big its tides are.
        for (int d = 0; d < 3; d++)
        {
            float day0 = m0 + d * 1440;
            Gfx.Rect(X(day0 + 840), gy, gw / 3 * 600 / 1440, gh, Pal.C("rgba(29,53,80,0.16)"));
            Gfx.Line(X(day0), gy, X(day0), gy + gh, 1.5f, Pal.C("rgba(16,36,58,0.35)"));
            int phase = (state.day + d + 3) % 8;
            float mx = X(day0) + 18, my = gy - 16;
            // The moon that day, in a little patch of night sky.
            Gfx.Rect(mx - 12, my - 11, 24, 22, Pal.C("#22364d"), 5);
            Gfx.Circle(mx, my, 7.5f, Pal.C("#f2ecd8"));
            if (phase != 4) Gfx.Circle(mx + (phase < 4 ? (phase + 1) * 2.6f : -(9 - phase) * 2.6f), my - 1, 7f, Pal.C("#22364d"));
            float range = TideRange(day0 + 720);
            string tag = $"Day {state.day + d}" + (range > 0.85f ? " · spring tides" : range < 0.62f ? " · neap tides" : "");
            Gfx.Text(tag, mx + 14, my - 9, FontKind.Ui700, 15, range > 0.85f ? Pal.C("#2a7d74") : range < 0.62f ? Pal.C("#8a6a2a") : Pal.PaperInk);
            for (int hr = 6; hr < 30; hr += 6)
            {
                float hx = X(day0 + (hr - 6) * 60);
                if (hr > 6) Gfx.Line(hx, gy + gh - 6, hx, gy + gh, 1, Pal.C("rgba(16,36,58,0.4)"));
                Gfx.Text($"{hr % 24:00}", hx + 2, gy + gh + 4, FontKind.Ui500, 12, Muted);
            }
        }
        // Below the glean line the flats are uncovered: shade them sandy.
        for (float m = m0; m < m0 + span; m += span / gw * 2)
        {
            float h = TideAt(m);
            if (h < GleanLevel) Gfx.Rect(X(m), Y(GleanLevel), 2, Y(h) - Y(GleanLevel), Pal.C("#e2c88e"));
        }
        Gfx.Dashed(gx, Y(GleanLevel), gw, 0, 1.5f, 6, Pal.C("#b08a4a"));
        Gfx.Dashed(gx, Y(FlatLevel), gw, 0, 1.5f, 6, Pal.C("#2a7d74"));
        // The two lines' names, inside the graph at its right-hand end.
        void Label(string t, float ly, Color c)
        {
            float w = Gfx.Measure(t, FontKind.Ui700, 12) + 10;
            Gfx.Rect(gx + gw - w - 4, ly - 17, w, 15, Pal.C("rgba(255,250,240,0.85)"), 3);
            Gfx.Text(t, gx + gw - w + 1, ly - 16, FontKind.Ui700, 12, c);
        }
        Label("below here: gleaning", Y(GleanLevel), Pal.C("#8a6a2a"));
        Label("below here: the reef flat is dry", Y(FlatLevel) + 20, Pal.C("#2a7d74"));
        Gfx.Text("high", gx - 36, Y(1) - 6, FontKind.Ui500, 12, Muted);
        Gfx.Text("low", gx - 32, Y(-1) - 6, FontKind.Ui500, 12, Muted);
        float px0 = X(m0), py0 = Y(TideAt(m0));
        for (float m = m0 + 12; m <= m0 + span; m += 12)
        {
            float px1 = X(m), py1 = Y(TideAt(m));
            Gfx.Line(px0, py0, px1, py1, 3, Pal.C("#1d5a88"));
            px0 = px1; py0 = py1;
        }
        // Now.
        float now = TideNow;
        if (now >= m0 && now <= m0 + span)
        {
            Gfx.Line(X(now), gy, X(now), gy + gh, 2, PinRed);
            Gfx.Circle(X(now), Y(TideAt(now)), 5, PinRed);
            Gfx.Text("now", X(now) + 4, gy + 4, FontKind.Ui700, 13, PinRed);
        }
        // Lola's answer, marked once you've clicked.
        if (riddle is { Done: true })
        {
            if (riddle.Kind == "neap")
            {
                float d0 = m0 + riddle.Answer * 1440;
                Gfx.Rect(X(d0), gy, gw / 3, gh, Pal.C("rgba(63,174,90,0.16)"));
            }
            else
            {
                float am = LowAt(riddle.Answer);
                Gfx.Circle(X(am), Y(TideAt(am)), 9, Pal.C("#3fae5a"));
                Gfx.Circle(X(am), Y(TideAt(am)), 5, Pal.Paper);
            }
            if (riddle.ClickM >= 0) Gfx.Circle(X(riddle.ClickM), Y(TideAt(riddle.ClickM)), 4, riddle.Right ? Pal.C("#3fae5a") : PinRed);
        }
        // Hovering reads the graph; clicking answers Lola.
        tideHoverM = -1;
        if (Gfx.Hover(gx, gy, gw, gh))
        {
            float m = m0 + (Gfx.Mouse.X - gx) / gw * span;
            tideHoverM = m;
            float h = TideAt(m);
            Gfx.Line(X(m), gy, X(m), gy + gh, 1, Pal.C("rgba(16,36,58,0.5)"));
            var (d, clock) = FromTideMinute(m);
            string tip = $"Day {d}, {ClockText(clock)} · {MathF.Abs(h * TideMetres):0.0} m {(h < 0 ? "below" : "above")} average · {(TideAt(m + 5) > h ? "rising" : "falling")}";
            float tw = Gfx.Measure(tip, FontKind.Ui600, 15) + 20;
            float tx = Math.Min(Gfx.Mouse.X + 12, gx + gw - tw);
            Gfx.Rect(tx, gy + gh - 34, tw, 26, NavyStrong, 4);
            Gfx.Text(tip, tx + 10, gy + gh - 30, FontKind.Ui600, 15, Pal.Paper);
        }
#if DEBUG
        Gfx.Seen["tidegraph"] = new Rectangle(gx, gy, gw, gh);
#endif
        if (riddle is { Done: false } && Gfx.Click(gx, gy, gw, gh)) AnswerRiddle(m0 + (Gfx.Mouse.X - gx) / gw * span);

        // The next low tides, in words.
        float ly = gy + gh + 30;
        Gfx.Text("Coming low tides", gx, ly, FontKind.Ui700, 17, Pal.PaperInk);
        ly += 24;
        int n0 = LowAt(LowIndex(now)) < now - 60 ? LowIndex(now) + 1 : LowIndex(now);
        var lows = Enumerable.Range(n0, 4).Select(n => $"{TideClock(LowAt(n))} ({TideSize(LowDepth(n)).Replace("a ", "")})").ToList();
        Gfx.Text(Gfx.Ellipsize(string.Join(" · ", lows), FontKind.Ui500, 15, gw), gx, ly, FontKind.Ui500, 15, Pal.PaperInk);
        ly += 22;

        // Why: sun, earth and moon in a line, then at right angles.
        float rx = x + pad + 820, rw = x + cw - pad - rx, ry = y + pad + 84;
        Gfx.Text("Why the tide changes", rx, ry, FontKind.Ui700, 19, Pal.PaperInk);
        ry += 30;
        DrawTideDiagram(rx, ry, rw, true);
        Gfx.Text("New or full moon: spring tides", rx, ry + 62, FontKind.Ui700, 14, Pal.C("#2a7d74"));
        ry += 84;
        DrawTideDiagram(rx, ry, rw, false);
        Gfx.Text("Half moon: neap tides", rx, ry + 62, FontKind.Ui700, 14, Pal.C("#8a6a2a"));
        ry += 88;
        string why = "The moon's pull (and the sun's) heaps the sea into two bulges, and the Earth turns through them: here, as on many coasts, two highs and two lows a day (some places get only one). "
            + "Each day the lows come about 50 minutes later, as the moon moves on along its orbit. "
            + "When the sun and moon pull in line their pulls add up, for the biggest tides. At right angles they partly cancel. "
            + "On Earth that's every two weeks or so; here the moon goes round in 8 days, so it's every 4.";
        var whyLines = Gfx.Wrap(why, FontKind.Ui500, 14, rw);
        Lines(whyLines, rx, ry, 18, FontKind.Ui500, 14, Pal.PaperInk);
        ry += whyLines.Count * 18 + 10;
        var glean = Gfx.Wrap("In the Philippines, gleaning (panginhas) is done at low tide, especially the low spring tides, often by women and children, for shells, sea urchins and seaweed.", FontKind.Note, 15, rw);
        Lines(glean, rx, ry, 19, FontKind.Note, 15, Muted);
        ry += glean.Count * 19;
        lastTidesBottom = Math.Max(ly, ry);
        if (Gfx.PressedOutside(x, y, cw, ch)) CloseTides();
    }

    // A little sky: the sun on the left, the Earth with its two bulges of sea, and the moon in line or above.
    static void DrawTideDiagram(float x, float y, float w, bool spring)
    {
        float cy = y + 26, ex = x + w * 0.55f;
        Gfx.Rect(x, y, w, 54, Pal.C("#1d3550"), 5);
        Gfx.Circle(x + 18, cy, 12, Pal.C("#f2c94a"));
        Gfx.Circle(x + 18, cy, 8, Pal.C("#ffe28a"));
        // The sea bulges toward the moon (and away from it), stretched further when the sun pulls the same way.
        float a = spring ? 22 : 18, b = spring ? 12 : 15;
        if (spring) DrawEllipse((int)Gfx.P(ex, cy).X, (int)Gfx.P(ex, cy).Y, a * Gfx.Z, b * Gfx.Z, Pal.C("#4ea3bf"));
        else DrawEllipse((int)Gfx.P(ex, cy).X, (int)Gfx.P(ex, cy).Y, b * Gfx.Z, a * Gfx.Z, Pal.C("#4ea3bf"));
        Gfx.Circle(ex, cy, 11, Pal.C("#3f8a4a"));
        Gfx.Circle(ex - 3, cy - 3, 4, Pal.C("#5fb05f"));
        if (spring) { Gfx.Circle(x + w - 14, cy, 6, Pal.C("#d8d8d0")); Gfx.Line(x + 34, cy, x + w - 24, cy, 1, Pal.C("rgba(255,255,255,0.35)")); }
        else { Gfx.Circle(ex, y + 8, 5, Pal.C("#d8d8d0")); Gfx.Line(x + 34, cy, ex - 26, cy, 1, Pal.C("rgba(255,255,255,0.35)")); }
    }

    void CloseTides()
    {
        // Closing without answering keeps her question for later in the day.
        riddle = null;
        ClosePanels();
    }

    /* ---------- Lola Pacing's riddles ---------- */
    static readonly string[] RiddleKinds = { "next", "lowest", "daylight", "neap", "lowest" };
    bool TidesLearned => state.Hinted("tides");
    bool RiddleToday => TidesLearned && state.riddleDay != state.day;

    TideRiddle MakeRiddle()
    {
        float now = TideNow, m0 = TideTableStart, end = m0 + 3 * 1440;
        string kind = RiddleKinds[state.riddlesAsked % RiddleKinds.Length];
        int first = LowAt(LowIndex(now)) > now ? LowIndex(now) : LowIndex(now) + 1;
        var lows = Enumerable.Range(first, 8).Where(n => LowAt(n) < end).ToList();
        var r = new TideRiddle { Kind = kind };
        switch (kind)
        {
            case "next":
                r.Ask = "When is the next low tide? Click the bottom of it on the graph.";
                r.Answer = first;
                break;
            case "lowest":
                r.Ask = "Which is the lowest tide still to come in these three days? Click it, and I'll meet you on the flats then.";
                r.Answer = lows.OrderByDescending(LowDepth).First();
                break;
            case "daylight":
                r.Ask = "The children glean with me, so it has to be light. When is the next low tide between 06:30 and 17:30?";
                r.Answer = lows.FirstOrDefault(n => FromTideMinute(LowAt(n)).clock is >= 6.5f * 60 and <= 17.5f * 60, first);
                break;
            default:
                r.Ask = "Which of these three days has the smallest tides? Click anywhere in that day.";
                r.Answer = Enumerable.Range(0, 3).OrderBy(d => TideRange(m0 + d * 1440 + 720)).First();
                break;
        }
        return r;
    }

    const float LowClickTolerance = 75;   // minutes either side of a low (about 12 px on the graph)

    void AnswerRiddle(float m)
    {
        var r = riddle;
        if (r == null || r.Done) return;
        r.Done = true;
        r.ClickM = m;
        state.riddleDay = state.day;
        state.riddlesAsked++;
        float m0 = TideTableStart;
        if (r.Kind == "neap")
        {
            // Two days with the same smallest tides (to the eye) both count.
            int d = (int)MathF.Floor((m - m0) / 1440);
            r.Right = d is >= 0 and < 3 && MathF.Abs(TideRange(m0 + d * 1440 + 720) - TideRange(m0 + r.Answer * 1440 + 720)) < 0.01f;
            if (r.Right) r.Answer = d;
        }
        else
        {
            int picked = LowIndex(m);
            // The click has to be at the bottom of a dip (within an hour and a quarter), not up on a high tide; two lows
            // the same depth (to the eye) both count as the lowest still to come.
            bool atLow = MathF.Abs(m - LowAt(picked)) <= LowClickTolerance;
            r.Right = atLow && (picked == r.Answer || r.Kind == "lowest" && MathF.Abs(LowDepth(picked) - LowDepth(r.Answer)) < 0.005f && LowAt(picked) > TideNow);
            if (r.Right) r.Answer = picked;
        }
        int day = r.Kind == "neap" ? state.day + r.Answer : FromTideMinute(LowAt(r.Answer)).day;
        string when = r.Kind == "neap" ? $"day {day}" : TideClock(LowAt(r.Answer));
        if (r.Right)
        {
            state.riddles++;
            state.coins += 25;
            Sfx.Play("right");
            r.Reply = r.Kind switch
            {
                "lowest" => LowDepth(r.Answer) >= -FlatLevel
                    ? $"Tama! {Cap(when)}, by the {(MoonOn(day) == 4 ? "full" : "new")} moon: a spring tide. Meet me on the flats below my board then, and we'll walk out to the reef together. (25 coins)"
                    : $"Tama! {Cap(when)}. Not a big spring tide, so the reef stays under, but it's the lowest we'll get. Meet me on the beach below my board then, and we'll glean together. (25 coins)",
                "neap" => HalfMoon(day) ? $"Tama! The moon's at half on {when}, so the sun and moon pull at right angles. Small tides: hardly worth the walk. (25 coins)"
                    : $"Tama! {Cap(when)} is the nearest these three days get to a half moon, when the sun and moon pull at right angles, so its tides are the smallest. (25 coins)",
                "daylight" => $"Tama! {Cap(when)}, in daylight. The little ones can come. (25 coins)",
                _ => $"Tama! {Cap(when)}. You read the sea like a gleaner. (25 coins)"
            };
            if (r.Kind == "lowest") { state.reefLow = r.Answer; state.hinted.Remove("reefMissed"); }
        }
        else
        {
            Sfx.Play("wrong");
            r.Reply = r.Kind switch
            {
                "neap" => $"Not quite, apo. It's {when}: {(HalfMoon(day) ? "the half moon" : "the nearest to a half moon")}, when the tides are smallest. See the green day?",
                "lowest" => $"Look again: the lowest dip is {when}, near the {(MoonOn(day) == 4 ? "full" : "new")} moon. I marked it in green. Try again tomorrow.",
                _ => $"Not quite, apo: it's {when}. I marked it in green. Each low comes about 50 minutes later than the day before."
            };
        }
        CheckBadges();
        Save();
    }

    int MoonOn(int day) => (day + 3) % 8 is 3 or 4 or 5 ? 4 : 0;
    static bool HalfMoon(int day) => (day + 3) % 8 is 2 or 6;
    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /* ---------- The reef walk ---------- */
    // The low tide you found for Lola (state.reefLow). While it's out, she and two of her apo are on the flats below her
    // board; talk to her there for her thanks, and the first find on the outer flat is an octopus.
    bool ReefWalkOn => state.reefLow >= 0 && LowTide && LowIndex(TideNow) == state.reefLow;
    bool ReefThanked => state.reefThanked == state.reefLow;
    bool ReefOctopusDue => ReefWalkOn && state.reefOctopus != state.reefLow;
    (float x, float y)? reefSpot;

    // The beach beside the flats nearest Lola's board.
    (float x, float y) ReefSpot()
    {
        if (reefSpot is { } s) return s;
        var home = HabagatFolk.First(n => n.id == "pacing");
        int hx = (int)(home.x / T), hy = (int)(home.y / T);
        var best = (x: home.x, y: home.y + 2f);
        float bestD = float.MaxValue;
        for (int y = hy - 10; y <= hy + 10; y++)
            for (int x = hx - 12; x <= hx + 12; x++)
            {
                if (x < 0 || y < 0 || x >= COLS || y >= ROWS || worldMap[y, x] != 's') continue;
                if (!new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(o => Flats().Contains((x + o.Item1, y + o.Item2)))) continue;
                float d = Dist(x * T + 5, y * T + 5, home.x, home.y);
                if (d < 24 || d >= bestD) continue;
                bestD = d; best = (x * T + 5, y * T + 8);
            }
        reefSpot = best;
        return best;
    }

    void UpdateReefWalk()
    {
        if (state.reefLow < 0) return;
        // Missed it: the tide's come and gone.
        // The tide's come back in: she goes home, and if you never came, the walk was missed.
        if (TideNow > LowWindow(state.reefLow).end + 1)
        {
            if (!ReefThanked) state.hinted["reefMissed"] = true;
            state.reefLow = -1;
            state.hinted.Remove("reefSeen");
            state.hinted.Remove("reefCalled");
            PlaceLola(false);
            return;
        }
        bool on = ReefWalkOn;
        PlaceLola(on);
        if (!on || ReefThanked) return;
        if (!state.Hinted("reefSeen") && Dist(player.X, player.Y, ReefSpot().x, ReefSpot().y) < 140)
        {
            state.hinted["reefSeen"] = true;
            Toast($"Lola Pacing and two of her apo are out {(FlatsDry ? "on the flats" : "on the beach")}! Go and say hello (<act>).", 5);
        }
        // Somewhere else when the tide goes out: a word that she's waiting.
        else if (!state.Hinted("reefCalled") && !state.Hinted("reefSeen") && mode == "play" && toastTimer <= 0)
        {
            state.hinted["reefCalled"] = true;
            Toast($"The tide's going out on Daang Pulo: Lola Pacing is waiting for you below her sungka board, until {TideClock(LowWindow(state.reefLow).end)}.", 6);
        }
    }

    // Lola stands by the flats during the walk, and back at her board otherwise.
    void PlaceLola(bool onFlats)
    {
        var s = HabagatWalk("pacing");
        var home = HabagatFolk.First(n => n.id == "pacing");
        var (tx, ty) = onFlats ? ReefSpot() : (home.x, home.y + 2);
        if (s.HomeX == tx && s.HomeY == ty) return;
        s.HomeX = s.X = tx; s.HomeY = s.Y = ty; s.ToX = s.ToY = null;
    }

    // Saying hello on the flats: her thanks, once. She and the children stay out until the tide comes back.
    void ThankReefWalk()
    {
        state.reefThanked = state.reefLow;
        state.reefWalks++;
        state.coins += 80;
        Give("cowrie", 3);
        Sfx.Play("coin");
        CheckBadges();
        Save();
    }

    // Lola's tide lesson, the second time you talk to her (the first is sungka).
    void TeachTides()
    {
        Say L(string t) => new("Lola Pacing", t);
        state.hinted["tides"] = true;
        Talk(new()
        {
            L("Have you been out on my flats yet? Everything worth finding there comes with the tide."),
            L("The sea breathes out twice a day. The moon pulls it. And every day it breathes out a little later, about fifty minutes."),
            L("When the moon is full, or dark, the sun pulls with it, and the sea goes out so far you can walk to the reef. That's when we glean. At a half moon it hardly moves."),
            L("Here: my granddaughter drew you a tide table. Read it well and I'll have a riddle for you each day.")
        }, () => { Save(); OpenTides(); });
    }

    // Out on a flat as the tide turns: once a low tide, a word to head back.
    int warnedTide = int.MinValue;
    void WarnRisingTide()
    {
        if (!OnFlat || Wading || Riding || Aboard) return;
        bool turning = FlatsDry && TideAt(TideNow + 5) > SeaLevel && SeaLevel > FlatLevel - 0.04f;
        if (FlatsDry && !turning) return;
        int n = LowIndex(TideNow);
        if (warnedTide == n) return;
        warnedTide = n;
        Toast("The tide's turning! The water's coming back over the flats: head back to the beach.", 4);
    }

    void AskRiddle()
    {
        FaceToward(HabagatWalk("pacing").X, HabagatWalk("pacing").Y);
        if (!RiddleToday) { Toast("Lola Pacing: One riddle a day, apo. Come back tomorrow."); return; }
        OpenTides(MakeRiddle());
    }
}
