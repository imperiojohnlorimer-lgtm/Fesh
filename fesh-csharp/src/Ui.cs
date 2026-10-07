using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    static readonly Color Navy = Pal.C("rgba(16,36,58,0.8)"), NavyStrong = Pal.C("rgba(16,36,58,0.88)"), White = Color.White,
        Muted = Pal.C("#6b5a45"), Rust = Pal.C("#b5523b"), CardBg = Pal.C("#fffaf0"), NoteBg = Pal.C("#fdf3c4"), PinRed = Pal.C("#c0392b");
    static readonly float[] Tilts = { -2, 1.6f, -1, 2.2f, -1.4f };
    float buildBarH;
    float? clockTip;   // where the clock chip is, while the mouse is over it (its tooltip is drawn last, over the meters)

    void DrawUi()
    {
        if (mode == "create") { DrawCreator(); return; }
        bool title = mode == "title";
        if (!title) DrawHud();
        if (!title) DrawFishingUi();
        if (!title && (BossFighting || BossOnLine)) DrawBossBar();
        if (mode == "build") DrawBuildBar();
        if (!title) DrawPrompt();
        if (!title) DrawToast();
        if (!title) DrawFishAttackWarning();
        if (mode == "dialogue" && dlg != null) DrawDialogue();
        if (title) { DrawTitle(); DrawToast(); }
        if (mode == "ending") DrawEnd();
        if (mode == "catch") DrawCatch();
        if (mode == "odd") DrawOdd();
        if (mode == "legend") DrawLegend();
        if (mode == "tamed") DrawTamed();
        if (mode == "panel" && panel == "dex") DrawDex();
        if (mode == "panel" && panel == "case") DrawCase();
        if (mode == "panel" && panel == "map") DrawMap();
        if (mode == "panel" && panel == "bag") DrawBag();
        if (mode == "panel" && panel == "craft") DrawCraft();
        if (mode == "panel" && panel == "shop") DrawShop();
        if (mode == "panel" && panel == "tank") DrawTank();
        if (mode == "panel" && panel == "lift") DrawLift();
        if (mode == "panel" && panel == "tackle") DrawTackle();
        if (mode == "pause") DrawMenu();
    }

    // The pointer a gamepad steers around menus (drawn over everything, since the real mouse pointer doesn't move).
    static void DrawPadPointer()
    {
        if (!Inp.CursorUi || Inp.PadCursor == null) return;
        var m = Gfx.Mouse;
        Vector2 P(float x, float y) => Gfx.P(m.X + x, m.Y + y);
        // Drawn both ways round, as Raylib only fills one winding.
        void Tri(Vector2 a, Vector2 b, Vector2 c, Color col) { DrawTriangle(a, b, c, col); DrawTriangle(a, c, b, col); }
        Tri(P(-3, -5), P(-3, 27), P(20, 19), Pal.Ink);
        Tri(P(0, 0), P(0, 20), P(14, 15), Color.White);
    }

    /* ---------- Building blocks ---------- */
    static Color Lighten(Color c, float k) => new((byte)(c.R + (255 - c.R) * k), (byte)(c.G + (255 - c.G) * k), (byte)(c.B + (255 - c.B) * k), c.A);

    static bool Button(string label, float x, float y, float w, float h, FontKind font, float fs, Color fill, Color text,
        float shadow, float border = 3, float radius = 6, bool live = true)
    {
        if (live && Gfx.Hover(x, y, w, h)) fill = Lighten(fill, 0.14f);
        Gfx.Box(x, y, w, h, fill, Pal.Ink, border, radius, shadow);
        Gfx.TextCenter(label, x + w / 2, y + (h - fs) / 2 - 1, font, fs, text);
#if DEBUG
        Gfx.Seen[label] = new Rectangle(x, y, w, h);   // so the autotest can click it by name
#endif
        return live && Gfx.Click(x, y, w, h);
    }

    // The big rounded buttons from the title, end and catch screens.
    static float BigW(string label) => Gfx.Measure(label, FontKind.Ui700, 24) + 58;
    static bool BigButton(string label, float x, float y, bool primary) =>
        Button(label, x, y, BigW(label), 56, FontKind.Ui700, 24, primary ? Pal.Buoy : Pal.Sand, primary ? White : Pal.Ink, 5);

    static float SmallW(string label) => Gfx.Measure(label, FontKind.Ui700, 20) + 44;
    static bool SmallButton(string label, float x, float y) =>
        Button(label, x, y, SmallW(label), 44, FontKind.Ui700, 20, Pal.Sand, Pal.Ink, 4);

    static void Backdrop()
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Pal.C("rgba(6,14,24,0.74)"));
        Gfx.Block(-5000, -5000, 10000, 10000);
    }

    static void Lines(List<string> lines, float x, float y, float lh, FontKind k, float fs, Color c)
    {
        for (int i = 0; i < lines.Count; i++) Gfx.Text(lines[i], x, y + i * lh, k, fs, c);
    }

    static void Pin(float x, float y)
    {
        Gfx.Circle(x, y + 2, 6.6f, Pal.C("rgba(0,0,0,0.4)"));
        Gfx.Circle(x, y, 6.6f, Pal.C("#7a1f16"));
        Gfx.Circle(x, y, 5.4f, PinRed);
        Gfx.Circle(x - 1.6f, y - 1.6f, 2.6f, Pal.C("#ff8b7a"));
    }

    /* ---------- HUD ---------- */
    void DrawHud()
    {
        const float m = 13, h = 36, fs = 20;
        float x = m;
        string weather = state.weather switch { "rain" => " · Rain", "storm" => " · Storm", _ => "" };
        string clock = $"Day {state.day} · {ClockText(state.clock, 10)}{weather}";
        // Sized for a wide time, so the chips beside it don't shuffle about as the minutes tick by.
        float cw = Math.Max(Gfx.Measure(clock, FontKind.Ui600, fs), Gfx.Measure($"Day {state.day} · {(Settings.Data.clock24 ? "00:00" : "00:00 PM")}{weather}", FontKind.Ui600, fs)) + 50;
        Gfx.Rect(x, m, cw, h, Navy, 5);
        float ix = x + 20, iy = m + h / 2;
        if (Night)
        {
            // The moon in its current phase: a shadow slides across it over eight days.
            Gfx.Circle(ix, iy, 7.5f, Pal.Paper);
            int ph = MoonPhase;
            if (ph != 4) Gfx.Circle(ix + (ph < 4 ? (ph + 1) * 2.6f : -(9 - ph) * 2.6f), iy - 1, 7f, Pal.C("#22364d"));
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI / 4;
                Gfx.Circle(ix + MathF.Cos(a) * 9, iy + MathF.Sin(a) * 9, 1.6f, Pal.Lantern);
            }
            Gfx.Circle(ix, iy, 5.5f, Pal.Lantern);
        }
        Gfx.Text(clock, x + 36, m + (h - fs) / 2 - 1, FontKind.Ui600, fs, Pal.Paper);
        Gfx.Block(x, m, cw, h);
        if (Gfx.Hover(x, m, cw, h)) clockTip = x;
        x += cw + 7;
        string area = AreaName();
        float aw = Gfx.Measure(area, FontKind.Ui600, fs) + 24;
        Gfx.Rect(x, m, aw, h, Navy, 5);
        Gfx.Text(area, x + 12, m + (h - fs) / 2 - 1, FontKind.Ui600, fs, Pal.Paper);
        x += aw + 7;
        string coins = state.coins.ToString();
        float coinW = Gfx.Measure(coins, FontKind.Ui600, fs) + 50;
        Gfx.Rect(x, m, coinW, h, Navy, 5);
        DrawIcon("coin", x + 8, m + 6, 24);
        Gfx.Text(coins, x + 38, m + (h - fs) / 2 - 1, FontKind.Ui600, fs, Pal.Lantern);
        // Health and food sit on a second row under the clock.
        DrawHealthMeter(m, m + h + 7, h);
        DrawFoodMeter(m + 136 + 7, m + h + 7, h);

        bool live = mode is not ("panel" or "catch" or "odd" or "pause" or "ending");
        // The menu button (the same as Esc), far right.
        float rx = Gfx.LW - m - h;
        if (Button("", rx, m, h, h, FontKind.Ui600, fs, Pal.Sand, Pal.Ink, 2, 2, 5, live)) OpenPause();
        for (int i = 0; i < 3; i++) Gfx.Rect(rx + 9, m + 10 + i * 7, h - 18, 3, Pal.Ink, 1);
        rx -= 7;
        var buttons = new (string label, Action act, bool on)[]
        {
            ("Case board", () => TogglePanel("case"), false),
            ($"Fesh-dex {state.caught.Count}/5", () => TogglePanel("dex"), false),
            ("Map", () => TogglePanel("map"), false),
            ("Tackle", () => TogglePanel("tackle"), false),
            ("Bag", () => TogglePanel("bag"), false),
            (mode == "build" ? "Done" : "Build", ToggleBuild, mode == "build")
        };
        foreach (var (label, act, on) in buttons)
        {
            float w = Gfx.Measure(label, FontKind.Ui600, fs) + 24;
            rx -= w;
            if (Button(label, rx, m, w, h, FontKind.Ui600, fs, on ? Pal.Lantern : Pal.Sand, Pal.Ink, 2, 2, 5, live)) act();
            rx -= 7;
        }
        if (clockTip is float tipX) DrawClockTip(tipX, m + h + 6);
        clockTip = null;
    }

    // Hovering over the clock: the moon, and what the weather will do next.
    void DrawClockTip(float x, float y)
    {
        int toFull = (4 - MoonPhase + 8) % 8;
        var lines = new List<string>
        {
            Night ? $"Night until {HourText(DawnMin)}" : $"Daylight until {HourText(DuskMin)}",
            FullMoon ? "The moon is full tonight" : toFull == 0 ? "Full moon tonight" : toFull == 1 ? "Full moon tomorrow night" : $"Full moon in {toFull} days",
            Forecast() ?? "No change in the weather expected today."
        };
        if (KnowTomorrow) lines.Add($"Tomorrow: {DescribeDay(state.tomorrow)}");
        float w = lines.Max(l => Gfx.Measure(l, FontKind.Ui500, 17)) + 24;
        Gfx.Rect(x, y, w, lines.Count * 23 + 14, NavyStrong, 5);
        for (int i = 0; i < lines.Count; i++) Gfx.Text(lines[i], x + 12, y + 8 + i * 23, FontKind.Ui500, 17, Pal.Paper);
    }

    // A small fish icon and a bar that goes from green to amber to red as you get hungry.
    void DrawFoodMeter(float x, float y, float h)
    {
        const float bw = 92;
        float w = 30 + bw + 14;
        Gfx.Rect(x, y, w, h, Navy, 5);
        DrawIcon("grilled_fish", x + 7, y + 6, 24);
        float f = state.food / 100f;
        var c = f > 0.5f ? Pal.C("#7fd36b") : f > 0.25f ? Pal.Lantern : Pal.Buoy;
        if (Starving && (int)(time * 3) % 2 == 0) c = Pal.C("#ff8b7a");
        Gfx.Rect(x + 34, y + 12, bw, 12, Pal.C("rgba(0,0,0,0.35)"), 3);
        if (f > 0) Gfx.Rect(x + 34, y + 12, Math.Max(6, bw * f), 12, c, 3);
        Gfx.Block(x, y, w, h);
        if (Gfx.Hover(x, y, w, h)) Gfx.Text($"Food {state.food:0}/100", x, y + h + 6, FontKind.Ui600, 16, Pal.Paper);
    }

    // A pixel heart and a red bar. It flashes when you're hurt and beats when you're nearly out.
    void DrawHealthMeter(float x, float y, float h)
    {
        const float bw = 92;
        float w = 30 + bw + 14, f = state.hp / 100f;
        Gfx.Rect(x, y, w, h, hurtFlash > 0 ? Pal.C("rgba(150,30,30,0.85)") : Navy, 5);
        string[] heart = { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." };
        float px = f < 0.25f ? 3 + 0.5f * MathF.Max(0, MathF.Sin(time * 9)) : 3;
        float hx = x + 18 - 3.5f * px, hy = y + h / 2 - 3 * px;
        for (int r = 0; r < heart.Length; r++)
            for (int c = 0; c < heart[r].Length; c++)
                if (heart[r][c] == 'X') Gfx.Rect(hx + c * px, hy + r * px, px, px, r == 1 && c == 1 ? Pal.C("#ff9a8a") : Pal.C("#e04b3a"));
        var col = f > 0.5f ? Pal.C("#e8574a") : f > 0.25f ? Pal.C("#f08a3a") : Pal.C("#ff4040");
        if (f < 0.25f && (int)(time * 3) % 2 == 0) col = Pal.C("#ffb0a0");
        Gfx.Rect(x + 34, y + 12, bw, 12, Pal.C("rgba(0,0,0,0.35)"), 3);
        if (f > 0) Gfx.Rect(x + 34, y + 12, Math.Max(6, bw * f), 12, col, 3);
        Gfx.Block(x, y, w, h);
        if (Gfx.Hover(x, y, w, h)) Gfx.Text($"Health {state.hp:0}/100", x, y + h + 6, FontKind.Ui600, 16, Pal.Paper);
    }

    static void DrawIcon(string itemId, float x, float y, float size) =>
        DrawTexturePro(ItemArt.Icon(itemId), new Rectangle(0, 0, 12, 12), Gfx.S(x, y, size, size), Vector2.Zero, 0, Color.White);

    /* ---------- Prompt and toast ---------- */
    static List<(string text, bool key)> Segments(string s)
    {
        var res = new List<(string, bool)>();
        int i = 0;
        while (i < s.Length)
        {
            int open = s.IndexOf('[', i);
            if (open < 0) { res.Add((s[i..], false)); break; }
            if (open > i) res.Add((s[i..open], false));
            int close = s.IndexOf(']', open);
            res.Add((s[(open + 1)..close], true));
            i = close + 1;
        }
        return res;
    }

    void DrawPrompt()
    {
        string full = (prompt.Key != null ? $"[{prompt.Key}] " : "") + (prompt.Text ?? "");
        if (full.Trim().Length == 0) return;
        const float fs = 22, h = 42;
        var segs = Segments(full);
        float SegW((string text, bool key) s) => s.key ? Gfx.Measure(s.text, FontKind.Ui700, fs) + 12 : Gfx.Measure(s.text, FontKind.Ui500, fs);
        float w = segs.Sum(SegW) + 36;
        float y = Gfx.LH - (mode == "build" ? buildBarH + 13 + 46 : 19) - h;
        float x = Gfx.LW / 2 - w / 2;
        Gfx.Rect(x, y, w, h, prompt.Urgent ? Pal.Buoy : NavyStrong, 5);
        x += 18;
        foreach (var s in segs)
        {
            float sw = SegW(s);
            if (s.key)
            {
                Gfx.Rect(x, y + 8, sw, h - 16, Pal.Sand, 4);
                Gfx.TextCenter(s.text, x + sw / 2, y + (h - fs) / 2 - 1, FontKind.Ui700, fs, Pal.Ink);
            }
            else Gfx.Text(s.text, x, y + (h - fs) / 2 - 1, FontKind.Ui500, fs, prompt.Urgent ? White : Pal.Paper);
            x += sw;
        }
    }

    void DrawToast()
    {
        if (toastAlpha <= 0 || toastMsg == "") return;
        const float fs = 22, lh = 27;
        float a = toastAlpha;
        var lines = Gfx.Wrap(toastMsg, FontKind.Ui600, fs, 1100);
        float w = lines.Max(l => Gfx.Measure(l, FontKind.Ui600, fs)) + 40, h = lines.Count * lh + 22;
        float x = Gfx.LW / 2 - w / 2, y = 115 - 6 * (1 - a);
        Gfx.Box(x, y, w, h, Pal.WithAlpha(Pal.Paper, a), Pal.WithAlpha(Pal.Ink, a), 2, 5);
        for (int i = 0; i < lines.Count; i++)
            Gfx.TextCenter(lines[i], Gfx.LW / 2, y + 11 + i * lh, FontKind.Ui600, fs, Pal.WithAlpha(Pal.PaperInk, a));
    }

    /* ---------- Dialogue ---------- */
    void DrawDialogue()
    {
        const float fs = 27, lh = 36, padX = 30, padTop = 26, padBot = 32, x = 38, w = 1204;
        var lines = Gfx.Wrap(dlg.Full, FontKind.Ui500, fs, w - 2 * padX);
        float h = Math.Max(173, padTop + lines.Count * lh + padBot);
        float y = Gfx.LH - 29 - h;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 6, 4);
        int left = (int)dlg.Shown;
        for (int i = 0; i < lines.Count && left > 0; i++)
        {
            string part = lines[i].Length <= left ? lines[i] : lines[i][..left];
            Gfx.Text(part, x + padX, y + padTop + i * lh, FontKind.Ui500, fs, Pal.PaperInk);
            left -= lines[i].Length + 1;
        }
        string sp = dlg.Lines[dlg.I].S;
        bool you = sp == "You";
        if (you) sp = state.look.name;
        if (sp != "")
        {
            const float sfs = 23, sh = 34;
            float sw = Gfx.Measure(sp, FontKind.Ui700, sfs) + 26;
            Gfx.Box(x + 21, y - 17, sw, sh, you ? Pal.Lantern : Pal.Buoy, Pal.Ink, 2, 4);
            Gfx.TextCenter(sp, x + 21 + sw / 2, y - 17 + (sh - sfs) / 2 - 1, FontKind.Ui700, sfs, you ? Pal.Ink : White);
        }
        if (dlg.Shown >= dlg.Full.Length)
        {
            var c = Pal.C("#7a6a55");
            float nx = x + w - 34, ny = y + h - 30;
            Gfx.Text("Next", nx - Gfx.Measure("Next", FontKind.Ui500, 19) - 4, ny, FontKind.Ui500, 19, c);
            DrawTriangle(Gfx.P(nx, ny + 7), Gfx.P(nx + 5, ny + 15), Gfx.P(nx + 10, ny + 7), c);
        }
    }

    /* ---------- Build bar ---------- */
    void DrawBuildBar()
    {
        const float pad = 5, gap = 5, bh = 56;
        float fs = 19, small = 15;
        var tools = BuildTools();
        var widths = new float[tools.Length];
        float total = 0;
        // Shrink the text if this set of pieces doesn't fit across the screen.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < tools.Length; i++)
            {
                var d = Data.BuildById.GetValueOrDefault(tools[i]);
                string name = $"{(d != null ? $"{i + 1}" : Bind.Name("remove"))} {(d != null ? d.Name : "Take down")}", cost = d != null ? d.CostText : "refunds all";
                widths[i] = Math.Max(Gfx.Measure(name, FontKind.Ui600, fs), Gfx.Measure(cost, FontKind.Ui500, small)) + 18;
            }
            total = widths.Sum() + gap * (tools.Length - 1) + pad * 2;
            if (total <= Gfx.LW - 30) break;
            fs = 16; small = 13;
        }
        buildBarH = bh + pad * 2;
        float x = Gfx.LW / 2 - total / 2, y = Gfx.LH - 13 - buildBarH;
        Gfx.Rect(x, y, total, buildBarH, NavyStrong, 6);
        Gfx.Block(x, y, total, buildBarH);
        // What you have of everything these pieces cost.
        var mats = tools.Select(t => Data.BuildById.GetValueOrDefault(t)).Where(d => d != null).SelectMany(d => d.Cost.Keys).Distinct();
        string have = "In your bag: " + string.Join("  ·  ", mats.Select(id => $"{Has(id)} {Items.ById[id].Name.ToLowerInvariant()}"));
        float hw = Gfx.Measure(have, FontKind.Ui600, 16) + 20;
        Gfx.Rect(x, y - 32, hw, 28, NavyStrong, 5);
        Gfx.Text(have, x + 10, y - 27, FontKind.Ui600, 16, Pal.Paper);
        x += pad;
        for (int i = 0; i < tools.Length; i++)
        {
            var d = Data.BuildById.GetValueOrDefault(tools[i]);
            bool on = buildTool == tools[i], remove = d == null, shortOf = d != null && !CanAfford(d);
            float a = shortOf && !on ? 0.6f : 1f, bw = widths[i];
            Color fill = on ? (remove ? Pal.Buoy : Pal.Lantern) : remove ? Pal.C("#e6d6b6") : Pal.Sand;
            if (Gfx.Hover(x, y + pad, bw, bh) && !on) fill = Lighten(fill, 0.14f);
            Gfx.Box(x, y + pad, bw, bh, Pal.WithAlpha(fill, a), Pal.WithAlpha(Pal.Ink, a), 2, 5);
            if (on && !remove) Gfx.Box(x + 2, y + pad + 2, bw - 4, bh - 4, fill, Pal.C("#fff8e3"), 2, 3);
            string num = d != null ? $"{i + 1} " : Bind.Name("remove") + " ", name = d != null ? d.Name : "Take down", cost = d != null ? d.CostText : "refunds all";
            float nw = Gfx.Measure(num + name, FontKind.Ui600, fs), tx = x + bw / 2 - nw / 2;
            Color main = remove && on ? White : Pal.Ink;
            Color sub = remove && on ? Pal.C("#ffe3dc") : shortOf ? Rust : Muted;
            Gfx.Text(num, tx, y + pad + 8, FontKind.Ui700, fs, Pal.WithAlpha(remove && on ? sub : Pal.C("#8a7a62"), a));
            Gfx.Text(name, tx + Gfx.Measure(num, FontKind.Ui700, fs), y + pad + 8, FontKind.Ui600, fs, Pal.WithAlpha(main, a));
            Gfx.TextCenter(cost, x + bw / 2, y + pad + 31, FontKind.Ui500, small, Pal.WithAlpha(sub, a));
            if (Gfx.Click(x, y + pad, bw, bh)) SelectTool(tools[i]);
            x += bw + gap;
        }
    }

    /* ---------- Ending (the title screen and menus are in UiMenu.cs) ---------- */
    void DrawEnd()
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Pal.C("rgba(8,18,32,0.88)"));
        Gfx.Block(-5000, -5000, 10000, 10000);
        var p = Gfx.Wrap("The creatures were taken from the trench beneath Saltmere and loaded onto the Halcyon. Dr. Mara Ilao sank the ship to bring them home, and the Abyssal kept her locket safe until her father could see it again.",
            FontKind.Note, 23, 720);
        var stats = Gfx.Wrap(endStats, FontKind.Ui500, 23, 720);
        float total = 75 + 22 + p.Count * 32 + 18 + stats.Count * 32 + 26 + 56;
        float y = (Gfx.LH - total) / 2, cx = Gfx.LW / 2;
        Gfx.TextCenter("Case closed", cx, y, FontKind.Ui700, 75, Pal.Lantern);
        y += 75 + 22;
        for (int i = 0; i < p.Count; i++) Gfx.TextCenter(p[i], cx, y + i * 32, FontKind.Note, 23, Pal.Paper);
        y += p.Count * 32 + 18;
        for (int i = 0; i < stats.Count; i++) Gfx.TextCenter(stats[i], cx, y + i * 32, FontKind.Ui500, 23, Pal.Sand);
        y += stats.Count * 32 + 26;
        float w = BigW("Keep fishing") + 13 + BigW("Start a new game"), x = cx - w / 2;
        if (BigButton("Keep fishing", x, y, true)) KeepFishing();
        if (BigButton("Start a new game", x + BigW("Keep fishing") + 13, y, false)) RestartFromEnding();
    }

    /* ---------- Catch card ---------- */
    void DrawCatch()
    {
        Backdrop();
        var cr = Data.ById[catchId];
        var clue = Data.Clues[catchId];
        const float cw = 500, pad = 17, iw = cw - pad * 2, artH = iw / 2;
        var desc = Gfx.Wrap(cr.Desc, FontKind.Note, 20, iw);
        var clueTitle = Gfx.Wrap($"New clue: {clue.Title}", FontKind.Ui700, 21, iw - 32);
        var clueText = Gfx.Wrap(clue.Text, FontKind.Note, 19, iw - 32);
        float clueH = 14 + clueTitle.Count * 26 + 4 + clueText.Count * 27 + 14;
        float h = pad + artH + 14 + 38 + 8 + 26 + 12 + desc.Count * 29 + 12 + clueH + 18 + 56 + pad + 8;
        float x = (Gfx.LW - cw) / 2, y = Math.Max(10, (Gfx.LH - h) / 2);
        Gfx.Box(x, y, cw, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        float cy = y + pad;
        Gfx.Box(x + pad - 2, cy - 2, iw + 4, artH + 4, Pal.Ink, Pal.Ink, 2, 4);
        Gfx.Portrait(catchId, false, x + pad, cy, iw, artH);
        cy += artH + 14;
        Gfx.Text(cr.Name, x + pad, cy, FontKind.Ui700, 37, Pal.PaperInk);
        cy += 38 + 8;
        float rw = Gfx.Measure(cr.Rarity, FontKind.Ui600, 17) + 18;
        Gfx.Rect(x + pad, cy, rw, 26, Pal.Ink, 3);
        Gfx.Text(cr.Rarity, x + pad + 9, cy + 4, FontKind.Ui600, 17, Pal.Lantern);
        cy += 26 + 12;
        Lines(desc, x + pad, cy, 29, FontKind.Note, 20, Pal.PaperInk);
        cy += desc.Count * 29 + 12;
        Gfx.Rect(x + pad, cy, iw, clueH, Pal.C("#fff8e3"), 4);
        Gfx.Dashed(x + pad, cy, iw, clueH, 2, 6, Rust);
        Lines(clueTitle, x + pad + 16, cy + 14, 26, FontKind.Ui700, 21, Rust);
        Lines(clueText, x + pad + 16, cy + 14 + clueTitle.Count * 26 + 4, 27, FontKind.Note, 19, Pal.PaperInk);
        cy += clueH + 18;
        bool canClose = GetTime() - catchOpenedAt >= 0.6;
        if (BigButton(catchId == "abyssal" ? "Continue" : "Add to Fesh-dex", x + pad, cy, true) && canClose) CloseCatch();
    }

    /* ---------- Fesh-dex ---------- */
    static readonly Dictionary<string, string> BiomeColor = new() { ["saltmere"] = "#5d9b45", ["frost"] = "#8fc3d6", ["dunes"] = "#e0a85a", ["mire"] = "#3f7d3a", ["atoll"] = "#2bb3a3", ["amihan"] = "#d59c4c" };
    int logPage;   // one page per biome, then legends, odd catches and records

    // The Fish log for an island: each fishing spot's fish, then what turns up in crab pots there.
    List<(string head, List<CommonFish> fish)> LogGroups(int page)
    {
        var b = Data.Biomes[page];
        var groups = Data.Spots.Where(s => s.Biome == b.Id).Select(s => (SpotKnown(s) ? s.Label : "???", Data.Common[s.Id].ToList())).ToList();
        groups.Add(("Crab pots", Data.PotCatch[b.Id].ToList()));
        return groups;
    }

    // When and how a fish bites. Before you've caught it, only the hints that help you find it.
    static string Conditions(CommonFish f, bool known)
    {
        var bits = new List<string>();
        if (f.Attack > 0) bits.Add("fights back!");
        if (f.Legend) bits.Add("legendary");
        else if (f.Rare) bits.Add("rare");
        if (f.Time != "any") bits.Add(f.Time);
        if (f.FullMoon) bits.Add("full moon");
        if (f.Weather != null) bits.Add(f.Weather == "clear" ? "clear days" : f.Weather);
        if (known)
        {
            if (f.Depth != "any") bits.Add(f.Depth);
            bits.Add(f.Style switch { "runner" => "runs", "jumper" => "leaps", "bottom" => "hugs the bottom", _ => "darts" });
        }
        return string.Join(", ", bits);
    }

    // Splits cards between two columns so they come out about the same height, keeping their order.
    static List<List<int>> TwoColumns(List<int> heights)
    {
        var cols = new List<List<int>> { new(), new() };
        int total = heights.Sum(), left = 0;
        for (int i = 0; i < heights.Count; i++)
        {
            if (cols[1].Count == 0 && (cols[0].Count == 0 || left + heights[i] / 2 <= total / 2)) { cols[0].Add(i); left += heights[i]; }
            else cols[1].Add(i);
        }
        return cols;
    }

    void DrawFishRow(CommonFish f, float x, float y, float w)
    {
        int n = state.commons.GetValueOrDefault(f.Id);
        bool known = n > 0;
        if (known) DrawIcon(f.Id, x, y + 1, 22);
        else Gfx.Rect(x + 2, y + 3, 18, 18, Pal.C("rgba(0,0,0,0.08)"), 4);
        var c = !known ? Pal.C("#9a8a70") : f.Legend ? Pal.C("#9a6a1a") : f.Rare ? Rust : Pal.PaperInk;
        Gfx.Text(Gfx.Ellipsize(known ? $"{f.Name} ×{n}" : "???", FontKind.Ui600, 17, 200), x + 30, y + 3, FontKind.Ui600, 17, c);
        string cond = Conditions(f, known);
        if (cond != "") Gfx.Text(Gfx.Ellipsize(cond, FontKind.Ui500, 14, w - 360), x + 240, y + 5, FontKind.Ui500, 14, Muted);
        if (state.records.TryGetValue(f.Id, out float kg))
        {
            int tr = state.trophies.GetValueOrDefault(f.Id);
            string rec = Kg(kg);
            float rx = x + w - 20 - Gfx.Measure(rec, FontKind.Ui700, 16);
            Gfx.Text(rec, rx, y + 4, FontKind.Ui700, 16, tr > 0 ? Pal.C("#9a6a1a") : Pal.C("#3f7d35"));
            if (tr > 0)
            {
                string n2 = tr.ToString();
                float sx = rx - 26 - Gfx.Measure(n2, FontKind.Ui700, 15);
                DrawIcon("star", sx, y + 3, 20);
                Gfx.Text(n2, sx + 21, y + 5, FontKind.Ui700, 15, Pal.C("#9a6a1a"));
            }
        }
    }

    void DrawDex()
    {
        Backdrop();
        const float nx = 50, nw = 1180, padT = 21, padR = 24, padB = 27, padL = 64, gap = 18;
        float innerW = nw - padL - padR;
        bool log = dexTab == "log";
        var lead = Gfx.Wrap(log ? "Every fish across the islands, with your heaviest catch of each. A gold star counts trophy-sized catches."
            : "Every strange creature you catch on Saltmere, plus what you know about the ones still out there.", FontKind.Note, 20, innerW);

        // Creatures tab layout
        float cardW = (innerW - gap * 4) / 5, artW = cardW - 20, artH = artW * 0.6f;
        var cards = Data.Creatures.Select(cr =>
        {
            bool got = state.Caught(cr.Id);
            string meta = got ? $"{Data.SpotById[cr.Spot].Label}, {Data.TimeLabel[cr.Time].ToLowerInvariant()}. {cr.Rarity}." : "Not caught yet";
            return (cr, got, meta: Gfx.Wrap(meta, FontKind.Ui500, 17, artW), text: Gfx.Wrap(got ? cr.Desc : cr.Hint, FontKind.Note, 18, artW));
        }).ToList();
        float cardH = cards.Max(c => 10 + artH + 10 + 30 + c.meta.Count * 21 + 6 + c.text.Count * 25 + 12);
        int kinds = Data.AllCommon.Count(f => state.commons.GetValueOrDefault(f.Id) > 0);
        string summary = $"Fish caught: {kinds} of {Data.AllCommon.Length} kinds. Open the Fish log for the full list.";

        // Fish log layout: a card per spot, in two columns
        const float rowH = 27, headH = 34, cardGap = 12;
        float colW = (innerW - gap) / 2;
        var groups = log && logPage < Data.Biomes.Length ? LogGroups(logPage) : new();
        var heights = groups.Select(g => (int)(headH + g.fish.Count * rowH + 8)).ToList();
        var cols = TwoColumns(heights);
        float logH = logPage < Data.Biomes.Length ? cols.Max(c => c.Sum(i => heights[i] + cardGap)) : 380;

        float contentH = log ? 50 + 34 + logH : cardH + 24 + 30 + 32;
        float h = padT + 46 + 10 + lead.Count * 28 + 16 + contentH + padB + 6;
        float ny = Math.Max(14, (Gfx.LH - h) / 2);
        Gfx.Box(nx, ny, nw, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        for (float ly = ny + 37; ly < ny + h - 4; ly += 37) Gfx.Rect(nx + 3, ly, nw - 6, 1, Pal.C("rgba(29,79,120,0.13)"));
        Gfx.Rect(nx + 45, ny + 3, 3, h - 6, Pal.C("rgba(224,75,58,0.4)"));

        float x = nx + padL, y = ny + padT;
        Gfx.Text("Fesh-dex", x, y + 2, FontKind.Ui700, 40, Pal.PaperInk);
        float tx = x + Gfx.Measure("Fesh-dex", FontKind.Ui700, 40) + 28;
        foreach (var (id, label) in new[] { ("creatures", "Creatures"), ("log", "Fish log") })
        {
            float w = SmallW(label);
            if (Button(label, tx, y, w, 44, FontKind.Ui700, 20, dexTab == id ? Pal.Lantern : Pal.Sand, Pal.Ink, 4)) dexTab = id;
            tx += w + 10;
        }
        if (SmallButton("Close", nx + nw - padR - SmallW("Close"), y)) ClosePanels();
        y += 46 + 10;
        Lines(lead, x, y, 28, FontKind.Note, 20, Pal.PaperInk);
        y += lead.Count * 28 + 16;

        if (!log)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var (cr, got, meta, text) = cards[i];
                float cx = x + i * (cardW + gap), cy = y;
                Gfx.Box(cx, cy, cardW, cardH, CardBg, Pal.PaperInk, 2, 4);
                Gfx.Portrait(cr.Id, !got, cx + 10, cy + 10, artW, artH);
                cy += 10 + artH + 10;
                Gfx.Text(got ? cr.Name : "???", cx + 10, cy, FontKind.Ui700, 24, got ? Pal.PaperInk : Pal.C("#8a7a62"));
                cy += 30;
                Lines(meta, cx + 10, cy, 21, FontKind.Ui500, 17, Muted);
                cy += meta.Count * 21 + 6;
                Lines(text, cx + 10, cy, 25, FontKind.Note, 18, Pal.PaperInk);
            }
            y += cardH + 24;
            Gfx.Text("Fishing", x, y, FontKind.Ui700, 24, Pal.PaperInk);
            Gfx.Text(summary, x, y + 32, FontKind.Note, 20, Pal.PaperInk);
            if (Gfx.PressedOutside(nx, ny, nw, h)) ClosePanels();
            return;
        }

        // Page buttons: one per island, then legends and other finds.
        float bx = x;
        for (int p = 0; p <= Data.Biomes.Length; p++)
        {
            string label = p < Data.Biomes.Length ? Data.Biomes[p].Name.Split(' ')[0] : "Legends & more";
            float w = Gfx.Measure(label, FontKind.Ui700, 18) + 30;
            var fill = logPage == p ? Pal.Lantern : p < Data.Biomes.Length ? Lighten(Pal.C(BiomeColor[Data.Biomes[p].Id]), 0.55f) : Pal.Sand;
            if (Button(label, bx, y, w, 40, FontKind.Ui700, 18, fill, Pal.Ink, 3, 2, 5)) { logPage = p; Sfx.Play("blip"); return; }
            bx += w + 8;
        }
        y += 50;
        int trophies = state.trophies.Values.Sum();
        var biggest = state.records.OrderByDescending(kv => kv.Value).FirstOrDefault();
        string stats = $"Fishing level {FishLevel}   ·   {kinds} of {Data.AllCommon.Length} kinds caught   ·   {trophies} {(trophies == 1 ? "trophy" : "trophies")}"
            + (biggest.Key != null ? $"   ·   Biggest: {Items.ById[biggest.Key].Name} ({Kg(biggest.Value)})" : "");
        Gfx.Text(stats, x, y, FontKind.Ui600, 17, Muted);
        y += 34;

        if (logPage < Data.Biomes.Length)
        {
            for (int c = 0; c < 2; c++)
            {
                float cx = x + c * (colW + gap), cy = y;
                foreach (int i in cols[c])
                {
                    var (head, fish) = groups[i];
                    Gfx.Box(cx, cy, colW, heights[i], CardBg, Pal.C("#c9b48f"), 2, 5);
                    Gfx.Rect(cx + 2, cy + 2, colW - 4, headH - 6, Lighten(Pal.C(BiomeColor[Data.Biomes[logPage].Id]), 0.45f), 3);
                    Gfx.Text(head, cx + 12, cy + 6, FontKind.Ui700, 18, Pal.PaperInk);
                    float ry = cy + headH;
                    foreach (var f in fish) { DrawFishRow(f, cx + 10, ry, colW - 10); ry += rowH; }
                    cy += heights[i] + cardGap;
                }
            }
        }
        else
        {
            // Legends, with a hint for each one still out there.
            float cx = x, cy = y;
            Gfx.Box(cx, cy, colW, 350, CardBg, Pal.C("#c9b48f"), 2, 5);
            Gfx.Text($"Legends: {Data.Legends.Keys.Count(id => state.commons.GetValueOrDefault(id) > 0)} of {Data.Legends.Count}", cx + 12, cy + 8, FontKind.Ui700, 18, Pal.PaperInk);
            cy += 38;
            foreach (var (id, info) in Data.Legends)
            {
                bool got = state.commons.GetValueOrDefault(id) > 0;
                if (got) DrawIcon(id, cx + 10, cy, 24); else Gfx.TextCenter("?", cx + 22, cy, FontKind.Ui700, 20, Pal.C("#9a8a70"));
                Gfx.Text(got ? Data.FishById[id].Name : "???", cx + 42, cy + 1, FontKind.Ui700, 17, got ? Pal.C("#9a6a1a") : Pal.C("#9a8a70"));
                string note = got && state.records.TryGetValue(id, out float kg) ? $"{Kg(kg)}. {info.Where}" : info.Hint;
                Gfx.Text(Gfx.Ellipsize(note, FontKind.Note, 15, colW - 54), cx + 42, cy + 22, FontKind.Note, 15, Muted);
                cy += 50;
            }
            // Not a fish, but the atoll's other legend.
            Gfx.Rect(cx + 10, cy - 6, colW - 20, 1, Pal.C("#e0d0b0"));
            if (state.tamed) DrawIcon("tidemane", cx + 10, cy, 24); else Gfx.TextCenter("?", cx + 22, cy, FontKind.Ui700, 20, Pal.C("#9a8a70"));
            Gfx.Text(state.tamed ? $"{Data.MountName}, your mount" : "???", cx + 42, cy + 1, FontKind.Ui700, 17, state.tamed ? Pal.C("#1d6f68") : Pal.C("#9a8a70"));
            Gfx.Text(Gfx.Ellipsize(state.tamed ? Data.Tidemane.Where : Data.Tidemane.Hint, FontKind.Note, 15, colW - 54), cx + 42, cy + 22, FontKind.Note, 15, Muted);
            cx = x + colW + gap; cy = y;
            Gfx.Box(cx, cy, colW, 196, CardBg, Pal.C("#c9b48f"), 2, 5);
            Gfx.Text("Odd catches", cx + 12, cy + 8, FontKind.Ui700, 18, Pal.PaperInk);
            cy += 38;
            foreach (var o in Data.Odd)
            {
                int n = state.odd.GetValueOrDefault(o.Id);
                Gfx.Text(n > 0 ? $"{o.Name} ×{n}" : "???", cx + 14, cy, FontKind.Ui600, 17, n > 0 ? Pal.PaperInk : Pal.C("#9a8a70"));
                Gfx.Text(n > 0 ? Data.SpotById[o.Spot].Label : "Something that isn't a fish", cx + 180, cy + 2, FontKind.Ui500, 15, Muted);
                cy += 29;
            }
            cy = y + 196 + 12;
            Gfx.Box(cx, cy, colW, 92, CardBg, Pal.C("#c9b48f"), 2, 5);
            Gfx.Text("Records", cx + 12, cy + 8, FontKind.Ui700, 18, Pal.PaperInk);
            Gfx.Text($"Sunken chests opened: {state.chests}    Perfect hooks: {state.perfects}", cx + 14, cy + 38, FontKind.Ui500, 16, Pal.PaperInk);
            Gfx.Text($"Derby wins: {state.derbyWins}    Casts: {state.casts}", cx + 14, cy + 62, FontKind.Ui500, 16, Pal.PaperInk);
        }
        if (Gfx.PressedOutside(nx, ny, nw, h)) ClosePanels();
    }

    /* ---------- Odd catch card ---------- */
    void DrawOdd()
    {
        Backdrop();
        var o = Data.OddById[oddId];
        const float cw = 500, pad = 17, iw = cw - pad * 2, artH = 210;
        var desc = Gfx.Wrap(o.Desc, FontKind.Note, 20, iw);
        int times = state.odd.GetValueOrDefault(oddId);
        var where = Gfx.Wrap($"Hooked at {Data.SpotById[oddSpot].Label}. " + (times > 1 ? $"This has happened {times} times now." : "Not a fish, but it counts in your Fish log."),
            FontKind.Ui500, 17, iw - 32);
        float boxH = 12 + where.Count * 22 + 10;
        float h = pad + artH + 14 + 38 + 8 + 26 + 12 + desc.Count * 29 + 12 + boxH + 18 + 56 + pad + 8;
        float x = (Gfx.LW - cw) / 2, y = Math.Max(10, (Gfx.LH - h) / 2);
        Gfx.Box(x, y, cw, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        float cy = y + pad, ax = x + pad;
        Gfx.Box(ax - 2, cy - 2, iw + 4, artH + 4, Pal.Ink, Pal.Ink, 2, 4);
        var sky = Gfx.S(ax, cy, iw, artH * 0.55f);
        DrawRectangleGradientV((int)sky.X, (int)sky.Y, (int)sky.Width, (int)sky.Height, Pal.C("#bfe0ea"), Pal.C("#8fc6dc"));
        var sea = Gfx.S(ax, cy + artH * 0.55f, iw, artH * 0.45f);
        DrawRectangleGradientV((int)sea.X, (int)sea.Y, (int)sea.Width, (int)Math.Ceiling(sea.Height), Pal.C("#3a8db0"), Pal.C("#1d4f78"));
        var tex = AnimalArt.Texture(oddId);
        var (sw, sh) = AnimalArt.Size(oddId);
        float k = MathF.Floor(Math.Min(iw * 0.55f / sw, artH * 0.62f / sh));
        float spx = ax + iw / 2 - sw * k / 2, spy = cy + artH * 0.6f - sh * k;
        DrawTexturePro(tex, new Rectangle(0, 0, sw, sh), Gfx.S(spx, spy, sw * k, sh * k), Vector2.Zero, 0, Color.White);
        for (int i = 0; i < 10; i++)
        {
            float dx = spx + (float)Pix.Hash(i, 1, 33) * sw * k, fall = (float)((time * 70 + i * 17) % 50);
            Gfx.Rect(dx, spy + sh * k * 0.5f + fall, 4, 7, Pal.WithAlpha(Pal.C("#7fd6f0"), 1 - fall / 50));
        }
        cy += artH + 14;
        Gfx.Text(o.Title, ax, cy, FontKind.Ui700, 37, Pal.PaperInk);
        cy += 38 + 8;
        float rw = Gfx.Measure("Odd catch", FontKind.Ui600, 17) + 18;
        Gfx.Rect(ax, cy, rw, 26, Pal.Buoy, 3);
        Gfx.Text("Odd catch", ax + 9, cy + 4, FontKind.Ui600, 17, White);
        cy += 26 + 12;
        Lines(desc, ax, cy, 29, FontKind.Note, 20, Pal.PaperInk);
        cy += desc.Count * 29 + 12;
        Gfx.Rect(ax, cy, iw, boxH, Pal.C("#e9f4f8"), 4);
        Gfx.Dashed(ax, cy, iw, boxH, 2, 6, Pal.Sea);
        Lines(where, ax + 16, cy + 12, 22, FontKind.Ui500, 17, Pal.Ink);
        cy += boxH + 18;
        if (BigButton("Let it go", ax, cy, true) && GetTime() - catchOpenedAt >= 0.6) CloseOdd();
    }

    /* ---------- Legendary catch card ---------- */
    void DrawLegend()
    {
        var info = Data.Legends[legendId];
        string weight = state.records.TryGetValue(legendId, out float kg) ? $" It weighs {Kg(kg)}." : "";
        if (GoldCard(info.Art, Data.FishById[legendId].Name, "Legendary", info.Desc,
            $"{info.Where}{weight} It's in your bag. Pip would pay a fortune, but it belongs in an aquarium.", "Incredible!")) CloseLegend();
    }

    // Winning Tidemane over at the Starwell.
    void DrawTamed()
    {
        var t = Data.Tidemane;
        if (GoldCard(t.Art, Data.MountName, "Your mount", t.Desc,
            Bind.Fix($"{t.Where} Press <ride> to ride. From anywhere outdoors, <ride> whistles it over."), "Ride on!")) CloseTamed();
    }

    // A glowing card for something extraordinary: portrait, name, a tag, a description, a boxed note and one button.
    // Returns true when the button is clicked.
    bool GoldCard(string art, string name, string tag, string about, string note, string button)
    {
        Backdrop();
        const float cw = 520, pad = 17, iw = cw - pad * 2, artH = iw / 2;
        var desc = Gfx.Wrap(about, FontKind.Note, 20, iw);
        var where = Gfx.Wrap(note, FontKind.Ui500, 17, iw - 32);
        float boxH = 12 + where.Count * 22 + 10;
        float h = pad + artH + 14 + 38 + 8 + 26 + 12 + desc.Count * 29 + 12 + boxH + 18 + 56 + pad + 8;
        float x = (Gfx.LW - cw) / 2, y = Math.Max(10, (Gfx.LH - h) / 2);
        Gfx.Rect(x - 7, y - 7, cw + 14, h + 14, Pal.WithAlpha(Pal.Lantern, 0.4f + 0.2f * MathF.Sin(time * 3)), 12);
        Gfx.Box(x, y, cw, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        float cy = y + pad;
        Gfx.Box(x + pad - 2, cy - 2, iw + 4, artH + 4, Pal.Ink, Pal.Ink, 2, 4);
        Gfx.Portrait(art, false, x + pad, cy, iw, artH);
        cy += artH + 14;
        Gfx.Text(name, x + pad, cy, FontKind.Ui700, 37, Pal.PaperInk);
        cy += 38 + 8;
        float rw = Gfx.Measure(tag, FontKind.Ui600, 17) + 18;
        Gfx.Rect(x + pad, cy, rw, 26, Pal.Ink, 3);
        Gfx.Text(tag, x + pad + 9, cy + 4, FontKind.Ui600, 17, Pal.Lantern);
        cy += 26 + 12;
        Lines(desc, x + pad, cy, 29, FontKind.Note, 20, Pal.PaperInk);
        cy += desc.Count * 29 + 12;
        Gfx.Rect(x + pad, cy, iw, boxH, Pal.C("#fff8e3"), 4);
        Gfx.Dashed(x + pad, cy, iw, boxH, 2, 6, Rust);
        Lines(where, x + pad + 16, cy + 12, 22, FontKind.Ui500, 17, Pal.Ink);
        cy += boxH + 18;
        return BigButton(button, x + pad, cy, true);
    }

    /* ---------- Tidemane's bar ---------- */
    // Over the top of the screen during the fight: on the line it's "something enormous"; on the sand, its wild spirit.
    void DrawBossBar()
    {
        const float w = 520, h = 56, y = 54;
        float x = Gfx.LW / 2 - w / 2;
        bool line = BossOnLine;
        string name = line ? "Something enormous" : Data.MountName;
        Gfx.Box(x, y, w, h, Pal.C("rgba(16,36,58,0.92)"), Pal.C("#5fd6c9"), 2, 6);
        Gfx.Text(name, x + 16, y + 7, FontKind.Ui700, 20, Pal.C("#bff4ff"));
        string sub = line ? "Reel it in!" : boss.Phase == "winded" ? "Winded! Strike now!" : "Wild spirit";
        Gfx.Text(sub, x + w - 16 - Gfx.Measure(sub, FontKind.Ui600, 16), y + 10, FontKind.Ui600, 16,
            !line && boss.Phase == "winded" ? Pal.Lantern : Pal.C("#9fc3d1"));
        float k = line ? 1 : Math.Clamp(boss.Spirit / BossSpirit, 0, 1);
        Gfx.Rect(x + 16, y + 34, w - 32, 12, Pal.C("rgba(0,0,0,0.4)"), 4);
        if (k > 0) Gfx.Rect(x + 16, y + 34, MathF.Max(8, (w - 32) * k), 12, line ? Pal.C("#2a9d8f") : Pal.C("#5fd6c9"), 4);
    }

    /* ---------- Cave lift ---------- */
    // Once you've been deep enough, the ladders at the cave mouth take you straight down to a lower floor.
    void DrawLift()
    {
        Backdrop();
        var floors = new List<(string label, string sub, int floor)> { ("Floor 1", "Copper ore and slimes", 1) };
        if (state.caveDeepest >= 5) floors.Add(("Floor 5", "Iron and copper, rock crabs", 5));
        if (state.caveDeepest >= 10) floors.Add(("Floor 10", "Gold and crystal, shades", 10));
        if (state.caveDeepest >= AncientFloor) floors.Add(("The Ancient Floor", "Abyssite and the coelacanth", AncientFloor));
        const float w = 620, pad = 24, rowH = 62, gap = 10;
        string deepest = state.caveDeepest >= AncientFloor ? "the Ancient Floor" : $"floor {state.caveDeepest}";
        float h = pad + 44 + 8 + 26 + 18 + floors.Count * (rowH + gap) - gap + pad;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Frostfang Caverns", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        if (SmallButton("Close", x + w - pad - SmallW("Close"), y + pad - 4)) { ClosePanels(); return; }
        float cy = y + pad + 44 + 8;
        Gfx.Text($"Where do you want to climb down to? Deepest so far: {deepest}.", x + pad, cy, FontKind.Ui500, 18, Muted);
        cy += 26 + 18;
        foreach (var (label, sub, floor) in floors)
        {
            float rx = x + pad, rw = w - pad * 2;
            var fill = floor == AncientFloor ? Pal.C("#bfe3dc") : Pal.Sand;
            Gfx.Box(rx, cy, rw, rowH, Gfx.Hover(rx, cy, rw, rowH) ? Lighten(fill, 0.14f) : fill, Pal.Ink, 3, 6, 4);
            Gfx.Text(label, rx + 18, cy + (rowH - 24) / 2 - 1, FontKind.Ui700, 24, Pal.Ink);
            Gfx.Text(sub, rx + rw - 18 - Gfx.Measure(sub, FontKind.Ui500, 17), cy + (rowH - 17) / 2, FontKind.Ui500, 17, Muted);
            if (Gfx.Click(rx, cy, rw, rowH)) { ClosePanels(); EnterCaveFloor(floor); return; }
            cy += rowH + gap;
        }
        if (Gfx.PressedOutside(x, y, w, h)) ClosePanels();
    }

    /* ---------- Map ---------- */
    Texture2D mapTex;

    // The map picture is the island layer plus trees, buildings and the hut, redrawn when the island changes.
    void RefreshMapTexture()
    {
        if (!mapTexDirty && mapTex.Id != 0) return;
        var img = new Pix(COLS * T, ROWS * T);
        // Always the outdoor world, even when the map is opened indoors or underground, where map and basePix are that room's.
        img.CopyFrom(worldBase, 0, 0);
        var (savedPix, savedScene, savedMap, savedBase, savedIndex) = (pix, scene, map, basePix, buildIndex);
        (pix, scene, map, basePix) = (img, "world", worldMap, worldBase);
        ReindexBuilds();
        foreach (var (tx, ty, kind) in trees) DrawTree(tx, ty, kind, live: false);
        foreach (var b in state.builds) DrawBuild(b, 0);
        DrawHut();
        DrawFire(0);
        DrawWreck();
        foreach (var n in Islanders) DrawBahay(n.x, n.y - 5, n.shirt);
        DrawKarst(2085, 107, 41); DrawKarst(1995, 141, 32); DrawBagaCone();
        (pix, scene, map, basePix, buildIndex) = (savedPix, savedScene, savedMap, savedBase, savedIndex);
        if (mapTex.Id != 0) UnloadTexture(mapTex);
        mapTex = Gfx.ToTexture(img.Buf, img.W, img.H, TextureFilter.Point);
        mapTexDirty = false;
    }

    static void Shadowed(string s, float x, float y, FontKind k, float size, Color c)
    {
        Gfx.Text(s, x + 2, y + 2, k, size, Pal.C("rgba(0,0,0,0.6)"));
        Gfx.Text(s, x, y, k, size, c);
    }

    void DrawMap()
    {
        Backdrop();
        RefreshMapTexture();
        const float pad = 18, head = 54;
        float origin = chartEast ? EastStart * T : 0, chartW = chartEast ? (COLS - EastStart) * T : EastStart * T;
        float chartH = ROWS * T;
        float scale = Math.Min(1120f / chartW, 550f / chartH);
        float mw = chartW * scale, mh = chartH * scale, bw = mw + pad * 2, bh = mh + pad * 2 + head;
        float bx = (Gfx.LW - bw) / 2, by = (Gfx.LH - bh) / 2;
        Gfx.Box(bx, by, bw, bh, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text(chartEast ? "Amihan sea chart" : "Map of the islands", bx + pad, by + pad, FontKind.Ui700, 28, Pal.PaperInk);
        string other = chartEast ? "Saltmere" : "Amihan";
        if (SmallButton(other, bx + bw - pad - SmallW("Close") - SmallW(other) - 14, by + pad - 4)) { chartEast = !chartEast; return; }
        if (SmallButton("Close", bx + bw - pad - SmallW("Close"), by + pad - 4)) { ClosePanels(); return; }
        float mx = bx + pad, my = by + pad + head;
        Gfx.Rect(mx - 2, my - 2, mw + 4, mh + 4, Pal.Ink);
        DrawTexturePro(mapTex, new Rectangle(origin, 0, chartW, chartH), Gfx.S(mx, my, mw, mh), Vector2.Zero, 0, Color.White);
        Vector2 M(float wx, float wy) => new(mx + (wx - origin) * scale, my + wy * scale);
        bool OnChart(float wx, float wy) => wx >= origin && wx < origin + chartW && wy >= 0 && wy < chartH;

        bool atollKnown = state.Hinted("visitedAtoll");
        foreach (var (name, wx, wy) in new[] { ("Saltmere Island", 255f, 182f), ("Frostfang Isle", 560f, 186f), ("Sunscald Dunes", 760f, 292f),
                     ("Mirewood", 100f, 236f), (atollKnown ? "Starfall Atoll" : "Unknown island", 1120f, 170f) })
        {
            if (!OnChart(wx, wy)) continue;
            var p = M(wx, wy);
            Shadowed(name, p.X - Gfx.Measure(name, FontKind.Ui700, 22) / 2, p.Y, FontKind.Ui700, 22, White);
        }
        foreach (var s in Data.Spots.Where(s => s.Scene == "world" && (s.Biome != "atoll" || atollKnown) && SpotKnown(s)))
        {
            if (!OnChart(s.X, s.Y)) continue;
            var p = M(s.X, s.Y);
            bool open = SpotOpen(s.Id);
            Gfx.Circle(p.X, p.Y, 6.5f, Pal.Ink);
            Gfx.Circle(p.X, p.Y, 5, open ? Pal.C("#7fd6f0") : Pal.C("#8a8f93"));
            string label = open ? s.Label : s.Label + " (closed)";
            Shadowed(label, p.X - Gfx.Measure(label, FontKind.Ui600, 14) / 2, p.Y + 8, FontKind.Ui600, 14, Pal.Paper);
        }
        var places = new List<(string, float, float)> { ("Frostfang Caverns", MouthDoorX, MouthDoorY - 26), ("Pip's stall", PipX, PipY - 6), ("Pip's jetty", SaltJettyX, SaltJettyY) };
        if (chartEast)
        {
            foreach (var isle in AmihanIslands)
            {
                var p = M(isle.cx * T, (isle.cy + isle.ry + 2) * T);
                Shadowed(isle.name, p.X - Gfx.Measure(isle.name, FontKind.Ui700, 20) / 2, p.Y, FontKind.Ui700, 20, White);
            }
            places.Add(("Village landing", 1495, 217));
            foreach (var n in Islanders) places.Add((n.name, n.x, n.y));
        }
        if (Has("boat") > 0 && !Aboard) { var bp = BoatPosition(); places.Add(("Your boat", bp.x, bp.y)); }
        if (state.tamed && !state.riding) places.Add((Data.MountName, state.mountX, state.mountY - 6));
        foreach (var (label, wx, wy) in places)
        {
            if (!OnChart(wx, wy)) continue;
            var p = M(wx, wy);
            Gfx.Circle(p.X, p.Y, 5, Pal.Ink);
            Gfx.Circle(p.X, p.Y, 3.5f, Pal.Lantern);
            Shadowed(label, p.X - Gfx.Measure(label, FontKind.Ui600, 14) / 2, p.Y + 7, FontKind.Ui600, 14, Pal.Lantern);
        }
        // Indoors you're shown at the door you came in by; underground, at the cave mouth.
        var (youX, youY, youLabel) = scene == "world" ? (player.X, player.Y - 6, "You")
            : scene == "cave" ? (MouthDoorX, MouthDoorY + 2, caveFloor == AncientFloor ? "You (Ancient Floor)" : $"You (floor {caveFloor})")
            : (state.exitX, state.exitY - 9, "You");
        if (OnChart(youX, youY))
        {
            var you = M(youX, youY);
            float pulse = 1 + 0.3f * MathF.Sin(time * 6);
            Gfx.Circle(you.X, you.Y, 10 * pulse, Pal.WithAlpha(Pal.Buoy, 0.35f));
            Gfx.Circle(you.X, you.Y, 6.5f, White);
            Gfx.Circle(you.X, you.Y, 5, Pal.Buoy);
            // Underground the label goes below the dot so it doesn't cover the caverns' own label.
            Shadowed(youLabel, you.X - Gfx.Measure(youLabel, FontKind.Ui700, 16) / 2, you.Y + (scene == "cave" ? 12 : -28), FontKind.Ui700, 16, White);
        }
        if (Gfx.PressedOutside(bx, by, bw, bh)) ClosePanels();
    }

    /* ---------- Case board ---------- */
    void DrawCase()
    {
        Backdrop();
        const float bx = 50, bw = 1180, border = 13, padT = 21, padX = 24, padB = 29, colGap = 24;
        float ix = bx + border, iw = bw - border * 2, cw = iw - padX * 2;
        const float qw = 693;
        var question = Gfx.Wrap("How did these creatures end up around Saltmere Island?", FontKind.Note, 26, qw - 48);
        float qh = 19 + question.Count * 35 + 16;
        float noteW = (cw - colGap * 4) / 5;
        var notes = Data.Creatures.Select(cr =>
        {
            bool got = state.Caught(cr.Id);
            var clue = Data.Clues[cr.Id];
            return (cr, got, title: got ? Gfx.Wrap(clue.Title, FontKind.Ui700, 21, noteW - 32) : new List<string>(),
                text: got ? Gfx.Wrap(clue.Finding, FontKind.Note, 19, noteW - 32) : new List<string>(),
                src: got ? Gfx.Wrap($"Found on the {cr.Name}", FontKind.Ui500, 16, noteW - 32) : new List<string>());
        }).ToList();
        float noteH = Math.Max(181, notes.Max(n => 29 + n.title.Count * 24 + 8 + n.text.Count * 27 + 11 + n.src.Count * 20 + 16));
        const float tw = 827;
        var theory = Gfx.Wrap(Data.Theories[state.caught.Count], FontKind.Note, 20, tw - 48);
        float th = 19 + 26 + theory.Count * 29 + 19;
        float innerH = padT + 46 + 8 + qh + 32 + noteH + 35 + th + padB;
        float h = innerH + border * 2, by = Math.Max(10, (Gfx.LH - h) / 2);

        Gfx.Rect(bx, by + 6, bw, h, Pal.Ink, 6);
        Gfx.Rect(bx, by, bw, h, Pal.C("#6b4a2b"), 6);
        Gfx.Cork(ix, by + border, iw, innerH);
        float x = ix + padX, y = by + border + padT;
        Gfx.Text("Case board", x, y + 5, FontKind.Ui700, 40, Pal.C("rgba(0,0,0,0.35)"));
        Gfx.Text("Case board", x, y + 2, FontKind.Ui700, 40, Pal.C("#fff7e6"));
        if (SmallButton("Close", ix + iw - padX - SmallW("Close"), y)) ClosePanels();
        y += 46 + 8;

        var pins = new List<Vector2>();
        var lockedPins = new List<Vector2>();
        var shadow = Pal.C("rgba(0,0,0,0.25)");
        float qx = ix + iw / 2 - qw / 2;
        var qt = new Tilt(qx + qw / 2, y + qh / 2, -1);
        qt.Rect(qx + 1, y + 3, qw, qh, shadow);
        qt.Rect(qx, y, qw, qh, White);
        for (int i = 0; i < question.Count; i++)
            qt.Text(question[i], qx + qw / 2 - Gfx.Measure(question[i], FontKind.Note, 26) / 2, y + 19 + i * 35, FontKind.Note, 26, Pal.PaperInk);
        pins.Add(qt.Point(qx + qw / 2, y + 4 + 6.5f));
        y += qh + 32;

        for (int i = 0; i < notes.Count; i++)
        {
            var (cr, got, title, text, src) = notes[i];
            float nx = x + i * (noteW + colGap);
            var nt = new Tilt(nx + noteW / 2, y + noteH / 2, Tilts[i]);
            nt.Rect(nx + 1, y + 3, noteW, noteH, shadow);
            nt.Rect(nx, y, noteW, noteH, got ? NoteBg : Pal.C("#e6d6b6"));
            if (got)
            {
                float ty = y + 29;
                for (int k = 0; k < title.Count; k++) nt.Text(title[k], nx + 16, ty + k * 24, FontKind.Ui700, 21, Pal.PaperInk);
                ty += title.Count * 24 + 8;
                for (int k = 0; k < text.Count; k++) nt.Text(text[k], nx + 16, ty + k * 27, FontKind.Note, 19, Pal.PaperInk);
                ty += text.Count * 27 + 11;
                for (int k = 0; k < src.Count; k++) nt.Text(src[k], nx + 16, ty + k * 20, FontKind.Ui500, 16, Pal.C("#7a6a55"));
                pins.Add(nt.Point(nx + noteW / 2, y + 8 + 6.5f));
            }
            else
            {
                var c = Pal.C("#9a8a70");
                nt.Text("?", nx + noteW / 2 - Gfx.Measure("?", FontKind.Ui700, 45) / 2, y + noteH / 2 - 38, FontKind.Ui700, 45, c);
                nt.Text("Missing clue", nx + noteW / 2 - Gfx.Measure("Missing clue", FontKind.Ui500, 17) / 2, y + noteH / 2 + 12, FontKind.Ui500, 17, c);
                lockedPins.Add(nt.Point(nx + noteW / 2, y + 8 + 6.5f));
            }
        }
        // Red string runs from the question through every clue found so far.
        for (int i = 1; i < pins.Count; i++)
        {
            Vector2 a = pins[i - 1], b = pins[i];
            var mid = new Vector2((a.X + b.X) / 2, Math.Max(a.Y, b.Y) + 13);
            DrawSplineSegmentBezierQuadratic(Gfx.P(a.X, a.Y), Gfx.P(mid.X, mid.Y), Gfx.P(b.X, b.Y), 3 * Gfx.Z, PinRed);
        }
        foreach (var p in pins.Concat(lockedPins)) Pin(p.X, p.Y);
        y += noteH + 35;

        float tx = ix + iw / 2 - tw / 2;
        var tt = new Tilt(tx + tw / 2, y + th / 2, 0.6f);
        tt.Rect(tx + 1, y + 3, tw, th, shadow);
        tt.Rect(tx, y, tw, th, White);
        tt.Text("Current theory", tx + 24, y + 19, FontKind.Ui700, 21, Rust);
        for (int i = 0; i < theory.Count; i++) tt.Text(theory[i], tx + 24, y + 19 + 26 + i * 29, FontKind.Note, 20, Pal.PaperInk);

        if (Gfx.PressedOutside(bx, by, bw, h)) ClosePanels();
    }
}
