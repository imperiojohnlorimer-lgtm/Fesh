#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    static double Lum(Color c)
    {
        double F(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * F(c.R) + 0.7152 * F(c.G) + 0.0722 * F(c.B);
    }
    static double Contrast(Color a, Color b) { double x = Lum(a), y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

    // Colour read back from the finished frame at a logical (1280 x 720) point.
    static Color ScreenAt(Image im, float lx, float ly) => Raylib.GetImageColor(im, (int)(Gfx.OX + lx * Gfx.Z), (int)(Gfx.OY + ly * Gfx.Z));

    // Accessibility (FESH_ACCESS_TEST=1): Reduce motion stops the shaking, flashing, swaying and bobbing but nothing else,
    // High contrast makes the cards solid and the text readable, and the marks that used to be only a colour are shapes.
    IEnumerable<int> AccessScript()
    {
        Note("Access: reduce motion, high contrast, shapes");
        state = new State { created = true, look = new Look { name = "Reader" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;
        var d = Settings.Data;

        /* ---------- the settings ---------- */
        Check("both are off in a new settings file", !new SettingsData().reduceMotion && !new SettingsData().highContrast);
        Settings.Reset(); Settings.Data.dayLength = 0; Settings.Data.pauseUnfocused = false; d = Settings.Data;
        File.WriteAllText(SaveFile.SettingsPath, "{ \"light\": 1, \"zoom\": 4 }");
        Settings.Load(); d = Settings.Data;
        Check("an old settings file (without them) loads with both off", !d.reduceMotion && !d.highContrast && d.light == 1);
        EffectsOff = keepOff;
        OpenPause(); menuTab = "graphics"; yield return 4;
        foreach (bool clearFont in new[] { false, true })
        {
            d.clearFont = clearFont; yield return 3;
            float Row(params string[] opts) => opts.Sum(o => Gfx.Measure(o, FontKind.Ui700, 18) + 30 + 8);
            float motionEnd = 250 + Row("Normal motion", "Reduced"), contrastEnd = 250 + Row("Normal colours", "High contrast"), smoothEnd = 250 + Row("Automatic", "Keep all");
            Check($"the three new rows fit the Graphics tab ({(clearFont ? "Clear" : "Pixel")} font: {smoothEnd:0}, {motionEnd:0}, {contrastEnd:0} of 700)", Math.Max(smoothEnd, Math.Max(motionEnd, contrastEnd)) < 700);
            if (clearFont) { pendingShot = "access-graphics-clear"; yield return 2; }
        }
        d.clearFont = false; yield return 3;
        Check("the Graphics tab has the new choices", new[] { "Normal motion", "Reduced", "Normal colours", "High contrast" }.All(Gfx.Seen.ContainsKey));
        pendingShot = "access-graphics"; yield return 2;
        ClickButton("Reduced"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Reduced motion by a real click", d.reduceMotion && Calm);
        ClickButton("High contrast"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("High contrast by a real click", d.highContrast && Gfx.HighContrast);
        Settings.Load(); d = Settings.Data;
        Check("both are saved with the settings", d.reduceMotion && d.highContrast);
        ClickButton("Normal motion"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Normal colours"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("and back to normal", !d.reduceMotion && !d.highContrast);
        ClosePause(); yield return 3;

        /* ---------- reduce motion ---------- */
        d.reduceMotion = false;
        float[] gusts(bool calm) { d.reduceMotion = calm; return Enumerable.Range(0, 30).Select(i => { time = i * 0.71f; return Gust(300, 200); }).ToArray(); }
        var gNormal = gusts(false); var gCalm = gusts(true);
        Check($"gusts roll through normally ({gNormal.Max() - gNormal.Min():0.00}) and are steady with Reduce motion ({gCalm.Max() - gCalm.Min():0.00})", gNormal.Max() - gNormal.Min() > 0.05f && gCalm.Max() - gCalm.Min() < 0.001f);
        int[] sways(bool calm) { d.reduceMotion = calm; return Enumerable.Range(0, 60).Select(i => { time = i * 0.43f; return Sway(i % 7, i % 5); }).ToArray(); }
        Check("trees sway normally and stand still with Reduce motion", sways(false).Any(v => v != 0) && sways(true).All(v => v == 0));
        int[] bobs(bool calm) { d.reduceMotion = calm; return Enumerable.Range(0, 40).Select(i => BoatBob(i * 0.31f)).ToArray(); }
        Check("the boat bobs normally and rides level with Reduce motion", bobs(false).Any(v => v != 0) && bobs(true).All(v => v == 0)
            && Enumerable.Range(0, 40).All(i => BoatBobAt(i * 0.31f, 100, 100, true) == 0));
        d.reduceMotion = false;

        // The view shakes on a big impact, unless Reduce motion (or the older Screen shake setting) says not to.
        (player.X, player.Y) = StandableNear(160f, 115f); yield return 4;
        int Spread(bool calm)
        {
            d.reduceMotion = calm; d.shake = true;
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < 25; i++) { quake = 1; time = 1f + i * 0.037f; UpdateCamera(); seen.Add((camX, camY)); }
            quake = 0;
            return seen.Count;
        }
        int shaky = Spread(false), still = Spread(true);
        Check($"an impact shakes the view ({shaky} different frames) but not with Reduce motion ({still})", shaky > 3 && still == 1);
        d.reduceMotion = false; UpdateCamera();

        // The thunder still sounds but the sky doesn't flash.
        state.weather = "storm"; scene = "world"; mode = "play";
        d.reduceMotion = false; flash = 0; thunderT = 0; UpdateWeather(0.016f);
        float flashNormal = flash;
        d.reduceMotion = true; flash = 0; thunderT = 0; UpdateWeather(0.016f);
        float flashCalm = flash;
        Check($"lightning flashes the sky ({flashNormal:0.0}) unless Reduce motion ({flashCalm:0.0})", flashNormal > 0.9f && flashCalm == 0);
        d.reduceMotion = false; ClearSkies(); SnapWeather(); flash = 0; yield return 3;

        // Nothing leaps out of the sea.
        state.clock = 12 * 60; yield return 3;
        d.reduceMotion = true; leapT = 5; DrawLeaps(time);
        Check("no fish leap with Reduce motion", leapT == 5);
        d.reduceMotion = false; leapT = 5; DrawLeaps(time);
        Check("and they do without it", leapT < 5);


        /* ---------- high contrast ---------- */
        d.highContrast = false;
        var navyN = Navy; var mutedN = Muted;
        d.highContrast = true;
        var navyHC = Navy; var mutedHC = Muted;
        Check($"the dark cards are solid with High contrast ({navyN.A} -> {navyHC.A})", navyN.A < 255 && navyHC.A == 255 && NavyStrong.A == 255);
        double cardText = Contrast(Pal.Paper, navyHC), mutedOld = Contrast(mutedN, Pal.Paper), mutedNew = Contrast(mutedHC, Pal.Paper);
        Check($"text on the dark cards is at least 7:1 ({cardText:0.0}) and the small brown text on paper goes from {mutedOld:0.0}:1 to {mutedNew:0.0}:1", cardText >= 7 && mutedNew >= 7 && mutedNew > mutedOld + 2);
        Check("the small blue-grey text on the cards turns white", CardText("#cfe3ea").Equals(Pal.Paper));
        EffectsOff = false;
        d.vignette = 2;
        Check($"the dark corners go ({VignetteStrength:0.00} against 0.20)", VignetteStrength == 0);
        d.highContrast = false;
        Check("and are back without it", MathF.Abs(VignetteStrength - 0.2f) < 0.001f);
        EffectsOff = keepOff;
        float Night(bool hc)
        {
            d.highContrast = hc;
            Array.Fill(dark, 0.6f); Array.Fill(pix.Buf, new Color(120, 120, 120, 255));
            ApplyDark(new Color(10, 20, 50, 255));
            return (float)pix.Buf.Take(2000).Average(c => c.R + c.G + c.B);
        }
        float nightN = Night(false), nightHC = Night(true);
        Check($"the night is lighter with High contrast ({nightN:0} -> {nightHC:0})", nightHC > nightN + 15);
        d.highContrast = false;

        // On the screen: the status card is solid, and the readings stay on the meters.
        (player.X, player.Y) = StandableNear(160f, 115f); state.clock = 12 * 60; yield return 4;
        Color probe = default; bool probed = false;
        d.highContrast = false; screenProbe = im => { probe = ScreenAt(im, 350, 50); probed = true; }; yield return 3;
        var cardNormal = probe;
        d.highContrast = true; probed = false; screenProbe = im => { probe = ScreenAt(im, 350, 50); probed = true; }; yield return 3;
        var cardSolid = probe;
        Check($"the status card on the screen is solid with High contrast ({cardSolid.R},{cardSolid.G},{cardSolid.B})", probed && Math.Abs(cardSolid.R - 9) <= 3 && Math.Abs(cardSolid.G - 21) <= 3 && Math.Abs(cardSolid.B - 38) <= 3);
        pendingShot = "access-hc-hud"; yield return 2;
        d.highContrast = false; yield return 2;
        pendingShot = "access-normal-hud"; yield return 2;
        d.highContrast = false;

        /* ---------- shapes, not only colours ---------- */
        // Every mark drawn in one flat colour: the shapes must still tell them apart.
        bool[,] Mask(Image im, float cx, float cy)
        {
            var m = new bool[28, 28];
            for (int j = 0; j < 28; j++)
                for (int i = 0; i < 28; i++)
                {
                    var c = ScreenAt(im, cx - 14 + i, cy - 14 + j);
                    m[i, j] = c.R + c.G + c.B > 600;   // the white fill and the white ticks: what is left once the ink is ignored
                }
            return m;
        }
        int Diff(bool[,] a, bool[,] b) { int n = 0; for (int j = 0; j < 28; j++) for (int i = 0; i < 28; i++) if (a[i, j] != b[i, j]) n++; return n; }
        var masks = new List<bool[,]>();
        var verdicts = new List<bool[,]>();
        screenDraw = () =>
        {
            Gfx.Rect(60, 300, 700, 160, Pal.C("#404040"));
            for (int k = 0; k < 4; k++) MapShape(100 + k * 60, 340, k, Color.White, 6);
            DrawVerdict(100, 420, 8, true, Color.White, 2.5f); DrawVerdict(160, 420, 8, false, Color.White, 2.5f);
        };
        screenProbe = im =>
        {
            for (int k = 0; k < 4; k++) masks.Add(Mask(im, 100 + k * 60, 340));
            verdicts.Add(Mask(im, 100, 420)); verdicts.Add(Mask(im, 160, 420));
        };
        yield return 3;
        Check("the four chart marks (dot, square, diamond, crossed dot) are drawn", masks.Count == 4 && masks.All(m => m.Cast<bool>().Count(b => b) > 20));
        bool distinct = masks.Count == 4;
        var diffs = new List<string>();
        for (int a = 0; distinct && a < 4; a++) for (int b = a + 1; b < 4; b++) { int df = Diff(masks[a], masks[b]); diffs.Add($"{a}-{b}:{df}"); if (df < 12) distinct = false; }
        Check($"they differ from each other by shape alone ({string.Join(" ", diffs)} pixels)", distinct);
        Check("a tick and a cross differ by shape alone", verdicts.Count == 2 && Diff(verdicts[0], verdicts[1]) > 20 && verdicts[0].Cast<bool>().Count(b => b) > 12);

        // The build outline: blocked is dashed with a cross, fine is solid.
        mode = "build"; buildTool = "path"; yield return 3;
        (player.X, player.Y) = StandableNear(160f, 115f); yield return 3;
        int px0 = (int)player.X - 5, py0 = (int)player.Y - 25;
        int Lit(string reason)
        {
            ghost = new Ghost { Tx = (int)(player.X / T) + 2, Ty = (int)(player.Y / T) - 2, Reason = reason };
            RenderWorld(time);
            int gx = ghost.Tx * T - camX, gy = ghost.Ty * T - camY, n = 0;
            for (int i = 0; i < T; i++) for (int j = 0; j < T; j++)
                {
                    int xx = gx + i, yy = gy + j;
                    if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                    var c = pix.Buf[yy * W + xx];
                    if (c.R > 190 && c.G < 120 && c.B < 100) n++;   // the red of the outline and the cross
                }
            return n;
        }
        int blockedPx = Lit("Not here"), okPx = Lit("");
        Check($"a blocked place is drawn with a red dashed outline and a cross ({blockedPx} red pixels) and a fine one with none ({okPx})", blockedPx > 14 && okPx < 4);
        mode = "play"; ghost = null; buildTool = null;

        d.reduceMotion = false; d.highContrast = false;
        Settings.Reset(); Settings.Data.dayLength = 0; Settings.Data.pauseUnfocused = false;
        EffectsOff = keepOff;
    }
}
#endif
