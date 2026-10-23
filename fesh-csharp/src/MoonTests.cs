#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The moon calendar (1.45.1, FESH_MOON_TEST=1): the painted moon in every phase (the same disc, the lit part growing and
    // shrinking on the right side while waxing and the left while waning), the words for the next full moon on each day of
    // the cycle, the panel by real clicks (the journal's Moon button, a click on the clock, Esc) in both fonts, its fit, and
    // the clock's tooltip pointing at it.
    IEnumerable<int> MoonScript()
    {
        Note("Moon calendar");
        /* ---------- the painted moon ---------- */
        var lit = new int[8]; var rightLit = new int[8]; var leftLit = new int[8]; var discs = new int[8];
        for (int ph = 0; ph < 8; ph++)
        {
            var p = new Pix(48, 48);
            PaintMoon(p, ph);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                {
                    var c = p.Buf[y * 48 + x];
                    if (c.A == 0) continue;
                    discs[ph]++;
                    bool bright = c.R + c.G + c.B > 3 * 110;
                    if (bright) { lit[ph]++; if (x >= 24) rightLit[ph]++; else leftLit[ph]++; }
                }
        }
        Note("lit pixels by phase: " + string.Join(" ", lit) + " of " + discs[0]);
        Check("every phase is the same round disc", discs.All(d => d == discs[0]) && discs[0] > 1500);
        Check("the new moon is nearly dark, the full moon nearly all lit", lit[0] < discs[0] * 0.08 && lit[4] > discs[0] * 0.85);
        Check("it grows towards the full moon and shrinks after it", lit[0] <= lit[1] && lit[1] < lit[2] && lit[2] < lit[3] && lit[3] < lit[4] && lit[4] > lit[5] && lit[5] > lit[6] && lit[6] > lit[7]);
        Check("the first quarter is lit on the right, the last on the left", rightLit[2] > leftLit[2] * 3 && leftLit[6] > rightLit[6] * 3);
        Check("waxing and waning mirror each other", Math.Abs(lit[1] - lit[7]) < discs[0] * 0.08 && Math.Abs(lit[3] - lit[5]) < discs[0] * 0.08);

        /* ---------- the words ---------- */
        MatchTestGame("Moon watcher");
        yield return 3;
        bool words = true;
        for (int ph = 0; ph < 8; ph++)
        {
            state.day = ((ph - 3) % 8 + 8) % 8 + 8; SetNight(false);
            int toFull = (4 - ph + 8) % 8;
            string t = NextFullText();
            words &= MoonPhase == ph && DaysToFullMoon() == toFull && (toFull == 0 ? t.Contains("tonight") : toFull == 1 ? t.Contains("tomorrow") : t.Contains($"{toFull} days"));
        }
        Check("the next full moon is worded right on every day of the cycle", words);
        state.day = 9; SetNight(true);   // day 9: phase (9+3)%8 = 4
        Check("a full moon at night says so", MoonPhase == 4 && NextFullText().Contains("full tonight"));
        Check("every phase has a name", MoonNames.Length == 8 && MoonNames[4] == "Full moon" && MoonNames[0] == "New moon");
        Check("only new and full moons give big tides, only the quarters small", new[] { 0, 4 }.All(p => TideTag(p) == "Big tides") && new[] { 2, 6 }.All(p => TideTag(p) == "Small tides") && new[] { 1, 3, 5, 7 }.All(p => TideTag(p) == ""));

        /* ---------- the panel, by clicks ---------- */
        state.day = 8; SetNight(false);   // phase 3
        FriendsOff = true; BoardOff = true;
        Settings.Data.zoom = 4; yield return 2;
        TogglePanel("journal"); yield return 3;
        Check("the journal is open", mode == "panel" && panel == "journal");
        Check("it has a Moon button", Gfx.Seen.ContainsKey("Moon"));
        ClickButton("Moon"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a click opens the moon calendar", mode == "panel" && panel == "moon");
        pendingShot = "m01-pixel"; yield return 4;
        Check($"it fits its panel in the pixel font ({lastMoonBottom:0} of {panelAt.y + panelAt.h:0})", lastMoonBottom > 0 && lastMoonBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = true; lastMoonBottom = 0; yield return 4;
        pendingShot = "m02-clear"; yield return 4;
        Check($"and in the clear font ({lastMoonBottom:0})", lastMoonBottom > 0 && lastMoonBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = false; yield return 2;
        ClickButton("Journal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("its Journal button goes back to the journal", panel == "journal");
        TogglePanel("journal"); yield return 3;
        Check("closed", mode == "play");

        // A click on the clock.
        Inp.ScriptMouse = new System.Numerics.Vector2(HudX + 120, HudY + 18); yield return 2;
        Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a click on the clock opens the moon calendar too", mode == "panel" && panel == "moon");
        Inp.Tap(KeyboardKey.Escape); yield return 4;
        Check("Esc closes it", mode == "play");
        FriendsOff = false; BoardOff = false;
    }
}
#endif
