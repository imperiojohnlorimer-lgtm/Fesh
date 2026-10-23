using Raylib_cs;

namespace Fesh;

// The moon calendar (1.45.1): the next eight days of the moon in a panel (the journal's Moon button, or a click on the clock),
// with the one thing nothing else says out loud: resting moves the clock on, so the full moon is never more than eight
// dawns away. The moon is the game's own MoonPhase ((day + 3) % 8, 4 full); a night belongs to the day before it, like
// everything else on the clock. Looks and words only, nothing is saved.
partial class Game
{
    static readonly string[] MoonNames =
    {
        "New moon", "Waxing crescent", "First quarter", "Waxing gibbous", "Full moon", "Waning gibbous", "Last quarter", "Waning crescent"
    };

    static int PhaseOn(int day) => (day + 3) % 8;

    // Whole days until the next full moon night, counting today as 0.
    int DaysToFullMoon() => (4 - MoonPhase + 8) % 8;

    static string DayWord(int ahead) => ahead == 0 ? "Today" : ahead == 1 ? "Tomorrow" : $"In {ahead} days";

    // The line under the big moon: when the next full moon is.
    string NextFullText()
    {
        int n = DaysToFullMoon();
        if (n == 0) return Night ? "The moon is full tonight." : "The moon will be full tonight.";
        return n == 1 ? "Full moon tomorrow night." : $"Full moon in {n} days.";
    }

    // What each phase means for the tides: new and full give the biggest (spring), the two quarters the smallest (neap).
    static string TideTag(int phase) => phase is 0 or 4 ? "Big tides" : phase is 2 or 6 ? "Small tides" : "";

    void OpenMoon()
    {
        Sfx.Play("ui");
        panel = "moon";
        mode = "panel";
        SetPrompt("");
    }

    /* ---------- The moon itself ---------- */
    // A lit ball painted pixel by pixel: soft grey highlands, darker seas and a few craters in fixed places, lit by a sun that
    // goes round it once in eight days (the dark side keeps a faint blue glow, so the whole disc always shows), the edge between
    // light and dark dithered like the rest of the game's art.
    static readonly (float x, float y, float r)[] MoonSeas =
    {
        (-0.30f, -0.34f, 0.30f), (0.10f, -0.42f, 0.24f), (0.34f, -0.12f, 0.2f), (-0.12f, 0.02f, 0.22f), (-0.40f, 0.18f, 0.16f), (0.14f, 0.22f, 0.13f)
    };
    static readonly (float x, float y, float r)[] MoonCraters =
    {
        (0.30f, 0.38f, 0.1f), (-0.18f, 0.46f, 0.08f), (0.52f, -0.30f, 0.07f), (-0.58f, -0.1f, 0.06f), (0.02f, 0.62f, 0.06f), (0.4f, 0.05f, 0.05f)
    };
    static readonly float[,] Bayer4 = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    static void PaintMoon(Pix p, int phase)
    {
        int size = p.W;
        float c = size / 2f, r = size / 2f - 1;
        float a = phase * MathF.PI / 4;
        float lx = MathF.Sin(a), lz = -MathF.Cos(a);   // towards the sun: behind us at the new moon, over our shoulder at the full
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - c) / r, ny = (y + 0.5f - c) / r, rr = nx * nx + ny * ny;
                if (rr > 1) continue;
                float nz = MathF.Sqrt(1 - rr);
                float sea = 0;
                foreach (var (sx, sy, sr) in MoonSeas) sea = MathF.Max(sea, 1 - MathF.Sqrt((nx - sx) * (nx - sx) + (ny - sy) * (ny - sy)) / sr);
                sea = Math.Clamp(sea * 2.2f, 0, 1);
                float crater = 0;
                foreach (var (cx, cy, cr) in MoonCraters)
                {
                    float d = MathF.Sqrt((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy));
                    if (d < cr) crater = MathF.Max(crater, d > cr * 0.7f ? 0.55f : -0.4f);   // a bright rim round a dark floor
                }
                float albedo = Math.Clamp(0.86f - sea * 0.34f + crater * 0.16f, 0.25f, 1f);
                float lit = nx * lx + nz * lz;
                // Light: a dithered step from the dark side to the lit one round the terminator.
                float level = Math.Clamp((lit + 0.08f) / 0.5f, 0, 1);
                float dither = (Bayer4[x & 3, y & 3] + 0.5f) / 16f - 0.5f;
                level = Math.Clamp(level + dither * 0.22f, 0, 1);
                float k = level < 0.3f ? 0 : level < 0.62f ? 0.62f : 1;
                var dark = Pix.Mix(Pal.C("#1d2b40"), Pal.C("#33496a"), albedo * 0.5f);
                var day = Pix.Mix(Pal.C("#8a8f99"), Pal.C("#f3f0e4"), albedo * (0.8f + 0.2f * lit));
                Color col = k == 0 ? dark : Pix.Mix(dark, day, k);
                // The limb: a slightly darker rim round the edge of the disc.
                if (rr > 0.86f) col = Pix.Mix(col, Pal.C("#1a2638"), 0.35f);
                p.Buf[y * size + x] = col;
            }
    }

    void DrawMoonPicture(float x, float y, int size, float scale, int phase, int slot)
        => PixelScene(x, y, size, size, scale, p => PaintMoon(p, phase), slot);

    /* ---------- The panel ---------- */
    float lastMoonBottom;

    void DrawMoonCalendar()
    {
        var (x, y, w, h) = PanelFrame("Moon calendar", $"Day {state.day} · {(Night ? "night" : "daytime")} · a new phase every dawn, eight to a month");
        if (PanelClose()) { ClosePanels(); return; }
        if (SmallButton("Journal", PanelCloseX - 12 - SmallW("Journal"), y + PanelPad - 6)) { panel = "journal"; Sfx.Play("ui"); return; }
        float left = x + PanelPad, top = y + 100;

        // Tonight's moon, big.
        int now = MoonPhase;
        DrawMoonPicture(left, top + 6, 48, 4, now, 0);
        float tx = left + 48 * 4 + 24;
        Gfx.Text(MoonNames[now], tx, top + 10, FontKind.Ui700, 30, Pal.PaperInk);
        Gfx.Text(NextFullText(), tx, top + 52, FontKind.Ui600, 20, Pal.C("#9a6a1a"));
        string tide = TideTag(now);
        Gfx.Text(tide != "" ? $"{tide} now." : "Middling tides now.", tx, top + 84, FontKind.Ui500, 18, Muted);

        // The next eight days: today first, the full moon marked in gold. Four to a row.
        float gx = left + 48 * 4 + 24, gy = top + 128;
        float cw = (w - PanelPad * 2 - 48 * 4 - 24 - 3 * 12) / 4, ch = 136;
        float tipY = gy;
        for (int i = 0; i < 8; i++)
        {
            float cx = gx + i % 4 * (cw + 12), cy = gy + i / 4 * (ch + 12);
            int ph = PhaseOn(state.day + i);
            bool full = ph == 4;
            Gfx.Box(cx, cy, cw, ch, full ? Pal.C("#fff3cf") : CardBg, full ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 5);
            DrawMoonPicture(cx + (cw - 24 * 3) / 2, cy + 10, 24, 3, ph, 1 + i);
            Gfx.TextCenter(DayWord(i), cx + cw / 2, cy + 80, FontKind.Ui700, 17, Pal.PaperInk);
            Gfx.TextCenter(MoonNames[ph], cx + cw / 2, cy + 101, FontKind.Ui500, 15, Muted);
            string tag = full ? "Full moon night" : TideTag(ph);
            if (tag != "") Gfx.TextCenter(tag, cx + cw / 2, cy + 119, FontKind.Ui600, 14, full ? Pal.C("#9a6a1a") : Pal.C("#3f6a8a"));
            tipY = cy + ch;
        }

        // What it means, in a few short lines.
        float iy = tipY + 18;
        float lw = w - PanelPad * 2;
        string[] notes =
        {
            "Resting passes time: at a campfire or in a shack it skips from day to evening, or from night to morning. Each dawn the moon moves one phase, so the full moon is never more than eight dawns away.",
            "Full moon nights stir up rarer fish, and some legends only rise then. New and full moons bring the biggest tides: the best days for the reef flats.",
        };
        foreach (var n in notes)
        {
            var lines = Gfx.Wrap(n, FontKind.Note, 18, lw);
            Lines(lines, left, iy, 25, FontKind.Note, 18, Pal.PaperInk);
            iy += lines.Count * 25 + 8;
        }
        lastMoonBottom = iy;
    }
}
