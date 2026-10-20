#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The frame profiler and the automatic quality step (FESH_PROF_TEST=1): the controller on frame times fed to it by hand
    // (no waiting), what each step switches off, that the player's own settings are never touched, the overlay, and the cost
    // of each pass written into the log for judging by eye (a Debug build is slower than the real thing).
    IEnumerable<int> ProfScript()
    {
        Note("Prof: controller, rungs, overlay");
        state = new State { created = true, look = new Look { name = "Timer" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff, keepEnabled = Quality.Enabled;
        var said = new List<string>();
        Quality.Say = said.Add;

        /* ---------- the controller, on made-up frames ---------- */
        bool All(int r) => true;
        // Runs `secs` of frames at `work` ms each, `fps` a second, in the mode given; returns how many times the step changed.
        int Run(float secs, float work, int fps, string m = "play", Func<int, bool> matters = null)
        {
            int changes = 0, n = (int)(secs * fps);
            for (int i = 0; i < n; i++) if (Quality.Feed(work, 1f / fps, fps, m, matters ?? All)) changes++;
            return changes;
        }
        Quality.Reset();
        Run(3, 4, 60);   // settle: the first frames are held
        Check("a game running well changes nothing", Quality.Rung == 0 && said.Count == 0);
        Run(6, 30, 60);
        Check($"frames far over budget step one effect down ({Quality.Rung}: {(Quality.Rung > 0 ? Quality.Effect[Quality.Rung] : "")})", Quality.Rung >= 1 && said.Count >= 1 && said[0].Contains(Quality.Effect[1]));
        Quality.Reset(); said.Clear();
        Run(2, 4, 60);
        int c = Run(1.4f, 30, 60);
        Check("under two seconds over budget is not enough", c == 0 && Quality.Rung == 0);
        Run(2, 30, 60);
        Check("two seconds over is", Quality.Rung == 1);
        Run(1.5f, 4, 60);   // the hold after a step: nothing is judged straight away
        c = Run(1.0f, 30, 60);
        Check("a step is followed by a pause before the next", c == 0);

        // A stall, a mode change and a change of cap say nothing about the steady cost.
        Quality.Reset(); said.Clear();
        Run(2, 4, 60);
        for (int i = 0; i < 10; i++) { Quality.Feed(400, 0.6f, 60, "play", All); Run(0.5f, 4, 60); }
        Check("a few long stalls (a bake, a resume) never step it", Quality.Rung == 0);
        for (int i = 0; i < 12; i++) { Run(0.6f, 30, 60, i % 2 == 0 ? "panel" : "play"); }
        Check("busy frames that keep switching mode are held, not counted", Quality.Rung == 0);
        Quality.Reset();
        Run(3, 4, 60);
        Run(8, 20, 30);   // 20 ms at 30 fps is within 23 ms
        Check("the 30 fps cap has the budget of a 30 fps frame", Quality.Rung == 0 && Quality.Budget(30) > 20 && Quality.Budget(60) < 12);
        Run(8, 20, 60);
        Check("the same work at 60 fps is over budget", Quality.Rung >= 1);

        // A game too slow to reach 4 frames a second still gets simpler (Codex: every frame was thrown out as a stall).
        Quality.Reset(); said.Clear();
        Run(3, 4, 60);
        for (int i = 0; i < 5; i++) Quality.Feed(300, 0.3f, 60, "play", All);
        Check("five slow frames are still just a stall", Quality.Rung == 0);
        for (int i = 0; i < 12; i++) Quality.Feed(300, 0.3f, 60, "play", All);
        Check($"a run of them is a game too slow to play: it steps down ({Quality.Rung})", Quality.Rung >= 1);
        Quality.Reset(); said.Clear();
        Run(3, 4, 60);
        for (int i = 0; i < 20; i++) { Quality.Feed(300, 0.3f, 60, "play", All); Run(0.3f, 4, 60); }
        Check("stalls between good frames never count as a slow game", Quality.Rung == 0);

        // Coming back: only with plenty of room, and not straight away.
        Quality.Reset(); said.Clear();
        Run(3, 4, 60); Run(3.2f, 30, 60);
        int down = Quality.Rung;
        Run(20, 3, 60);
        Check($"it doesn't come back after 20 quiet seconds ({Quality.Rung})", Quality.Rung == down);
        Run(14, 3, 60);
        Check($"it comes back after a long quiet spell ({down} -> {Quality.Rung})", Quality.Rung == down - 1 && said.Any(s => s.Contains("back on")));
        // ...and if that wasn't sustainable, the wait next time is longer.
        Run(2, 3, 60); Run(3.2f, 30, 60);
        Check("it goes down again when the effect was too dear", Quality.Rung == down);
        Run(40, 3, 60);
        Check("then a short quiet spell is no longer enough", Quality.Rung == down);
        Run(30, 3, 60);
        Check("a longer one is", Quality.Rung == down - 1);

        // Only effects that would change something for this player are counted.
        Quality.Reset(); said.Clear();
        Run(3, 4, 60);
        Run(6, 30, 60, "play", r => r == 5);
        Check($"it skips what's already off ({Quality.Rung}: {said.FirstOrDefault()})", Quality.Rung == 5 && said.Count == 1 && said[0].Contains("water sparkle"));
        Quality.Reset(); said.Clear();
        Run(3, 4, 60);
        Run(60, 30, 60, "play", r => r == 2);
        Check("with nothing left to switch off it stops asking", Quality.Rung == 2 && said.Count == 1);
        Quality.Reset(); said.Clear();
        Run(3, 4, 60);
        Run(200, 30, 60);
        Check($"it never goes past the last step ({Quality.Rung})", Quality.Rung == Quality.Rungs);
        Quality.Reset(); said.Clear();

        /* ---------- what each step does to the picture's switches ---------- */
        EffectsOff = false;
        var d = Settings.Data;
        d.light = 2; d.bloom = true; d.grain = true; d.vignette = 2; d.autoQuality = true;
        ClearSkies(); SetNight(false); yield return 3;
        Quality.Rung = 0;
        Check("step 0: Full light, bloom and grain all on", LightMode == 2 && BloomOn && GrainOn);
        Quality.Rung = 1; Check("step 1 takes the grain", !GrainOn && BloomOn && LightMode == 2);
        Quality.Rung = 2; Check("step 2 takes the bloom", !BloomOn && LightMode == 2);
        Quality.Rung = 3; Check("step 3 takes the reflections", ReflectK == 0 && LightMode == 2);
        Quality.Rung = 5; Check("step 5 takes the water sparkle", LightMode == 2);
        Quality.Rung = 6; Check("step 6 drops Full to Soft", LightMode == 1);
        Quality.Rung = 7; Check("step 7 turns light and colour off", LightMode == 0);
        Check("the player's own settings are untouched", d.light == 2 && d.bloom && d.grain && d.vignette == 2 && d.autoQuality);
        d.light = 1; Quality.Rung = 6; Check("a Soft player is not dropped by step 6", LightMode == 1);
        d.light = 2; Quality.Rung = 0;
        Check("steps that change nothing for this player are skipped", !RungMatters(1) == !d.grain && RungMatters(3) && RungMatters(6)
            && ((d.light = 1) == 1 && !RungMatters(3) && !RungMatters(4) && !RungMatters(6) && RungMatters(7)));
        d.light = 0; Check("with light off the light steps don't matter", !RungMatters(7) && !RungMatters(3));
        d.light = 2; d.bloom = d.grain = false;

        // The caustics really are left out from step 5.
        yield return 2;
        (player.X, player.Y) = StandableNear(2040f, 138f); state.day = 3; state.clock = 12 * 60; yield return 4;
        Settings.Data.zoom = 4; yield return 3;
        Quality.Rung = 0; RenderWorld(time); long withCaustics = PixHash(pix);
        Quality.Rung = 5; RenderWorld(time); long without = PixHash(pix);
        Check("the water sparkle is what step 5 removes", withCaustics != without);
        Quality.Rung = 0;

        // The caustics' table of sines draws the same threads as the real sines (to the pixel, bar a rare borderline one).
        float worstSin = 0;
        for (int i = 0; i < 20000; i++) { float ang = (i - 10000) * 0.0137f; worstSin = MathF.Max(worstSin, MathF.Abs(CausticSin(ang) - MathF.Sin(ang))); }
        Check($"the sine table is within 0.004 of the real sine ({worstSin:0.00000})", worstSin < 0.004f);
        int agree = 0, total = 0;
        foreach (float tq in new[] { 0f, 2.5f, 7.1666f })
            for (int wx = -150; wx < 450; wx++)
                for (int wy = -80; wy < 200; wy++)
                {
                    float exact = MathF.Sin(wx * 0.31f + tq * 1.1f + MathF.Sin(wy * 0.13f + tq * 0.5f) * 1.8f) + MathF.Sin(wy * 0.37f - tq * 0.9f + MathF.Sin(wx * 0.11f - tq * 0.4f) * 1.8f);
                    float fast = CausticSin(wx * 0.31f + tq * 1.1f + CausticSin(wy * 0.13f + tq * 0.5f) * 1.8f) + CausticSin(wy * 0.37f - tq * 0.9f + CausticSin(wx * 0.11f - tq * 0.4f) * 1.8f);
                    total++; if ((MathF.Abs(exact) <= 0.13f) == (MathF.Abs(fast) <= 0.13f)) agree++;
                }
        Check($"the threads fall on the same pixels as with real sines ({agree} of {total})", agree >= total * 0.995);

        /* ---------- the game loop's part: the automatic setting and its reset ---------- */
        Quality.Enabled = true;
        Quality.Rung = 3;
        d.autoQuality = false; UpdateQuality();
        Check("turning Automatic off puts everything back", Quality.Rung == 0);
        d.autoQuality = true;
        Quality.Rung = 3; EffectsOff = true; UpdateQuality();
        Check("a test run with the effects off is never stepped", Quality.Rung == 0);
        EffectsOff = false; Quality.Rung = 0;
        Check("it is on by default and an old settings file keeps it on", new SettingsData().autoQuality);

        // The Graphics tab shows it, and a real click on Off sets it.
        OpenPause(); menuTab = "graphics"; yield return 4;
        Check("the Graphics tab has Keep it smooth", Gfx.Seen.ContainsKey("Automatic") && Gfx.Seen.ContainsKey("Keep all"));
        pendingShot = "prof-graphics-tab"; yield return 2;
        ClickButton("Automatic"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("Automatic stays on when clicked", d.autoQuality);
        Quality.Enabled = keepEnabled;
        ClosePause(); yield return 3;

        /* ---------- the numbers: where a frame's time goes ---------- */
        d.light = 2; d.bloom = d.grain = false; EffectsOff = false;
        foreach (int zoom in new[] { 4, 2 })
        {
            Settings.Data.zoom = zoom; yield return 4;
            foreach (var (name, x, y, minute) in new[] { ("Luntian lagoon at noon", 2040f, 138f, 12 * 60), ("Saltmere at 5 PM", 160f, 115f, 17 * 60), ("Saltmere at night", 160f, 115f, 22 * 60) })
            {
                (player.X, player.Y) = StandableNear(x, y); state.day = 3; state.clock = minute; ClearSkies(); yield return 4;
                Prof.Detail = true;
                for (int i = 0; i < 60; i++) { Prof.FrameStart(); RenderWorld(time); Prof.FrameEnd(1f / 60); }
                Prof.Detail = false;
                string line = string.Join(", ", Enumerable.Range(0, Pass.Count).Where(i => Prof.Ms[i] >= 0.05f && i is not (Pass.Update or Pass.Upload or Pass.Ui)).Select(i => $"{Pass.Names[i].Trim()} {Prof.Ms[i]:0.00}"));
                Note($"{name}, {W}x{H}: world {Prof.WorkAvg:0.0} ms ({line})");
            }
        }
        Settings.Data.zoom = 4; yield return 4;

        /* ---------- the overlay ---------- */
        (player.X, player.Y) = StandableNear(2040f, 138f); state.clock = 12 * 60; yield return 4;
        Prof.Overlay = true; yield return 12;
        Check("with the overlay up every drawn pass has a time", Prof.Detail && Prof.Ms[Pass.Water] > 0.001f && Prof.Ms[Pass.Objects] > 0.001f && Prof.Ms[Pass.Ui] > 0.001f && Prof.WorkAvg > 0.1f);
        pendingShot = "prof-overlay"; yield return 3;
        Prof.Overlay = false; yield return 3;
        Check("with it off nothing is timed per pass", !Prof.Detail);

        Quality.Reset(); Quality.Say = null; Quality.Enabled = keepEnabled;
        EffectsOff = keepOff;
    }
}
#endif
