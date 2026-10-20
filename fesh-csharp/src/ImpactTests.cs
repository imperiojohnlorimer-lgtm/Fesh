#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Catch and boss feedback (FESH_IMPACT_TEST=1, also in the full run): the tiers, the landing splash, the held-up fish's
    // rise-in and rare glow, the shake, flash and hit-stop (and that Reduce motion, Screen shake and light off each turn
    // the right part off), and Tidemane's charge lane.
    IEnumerable<int> ImpactScript()
    {
        Note("Impact: catches and hits");
        state = new State { created = true, look = new Look { name = "Striker" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;
        var d = Settings.Data;
        var bg = new Color(40, 90, 60, 255);
        void Clear() { Array.Fill(pix.Buf, bg); pix.CamX = pix.CamY = 0; camX = camY = 0; }
        int Lit() => pix.Buf.Count(c => !c.Equals(bg));
        int Sum(int x0, int y0, int w, int h) { int s = 0; for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) { var c = pix.Buf[y * W + x]; s += c.R + c.G + c.B; } return s; }
        int TopRow() { for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (!pix.Buf[y * W + x].Equals(bg)) return y; return -1; }

        /* ---------- tiers ---------- */
        Check("a common fish is tier 0, a big one or a perfect hook 1, a rare 2, a legend or odd catch 3",
            CatchTierOf(false, false, 1, false) == 0 && CatchTierOf(false, false, 1.3f, false) == 1 && CatchTierOf(false, false, 1, true) == 1
            && CatchTierOf(true, false, 1, false) == 2 && CatchTierOf(true, true, 1, false) == 3 && CatchTierOf(false, true, 1, false) == 3);

        /* ---------- the landing splash ---------- */
        EffectsOff = true; splashRings.Clear(); particles.Clear();
        CatchSplash(100, 60, 2, 1.3f);
        Check("with light off a catch lands quietly, as before", splashRings.Count == 0 && particles.Count == 0);
        EffectsOff = false; d.light = 2;
        CatchSplash(100, 60, 0, 1);
        int commonRings = splashRings.Count, commonDrops = particles.Count;
        splashRings.Clear(); particles.Clear();
        CatchSplash(100, 60, 2, 1.4f);
        Check($"with light on a fish splashes (rings {commonRings}, drops {commonDrops}) and a big rare one splashes more ({splashRings.Count}, {particles.Count})",
            commonRings >= 2 && commonDrops >= 7 && splashRings.Count > commonRings && particles.Count > commonDrops && splashRings.Any(r => r.Gold));
        Clear(); foreach (var r in splashRings) r.Age = r.Life * 0.4f;
        DrawSplashRings();
        Check($"the rings are drawn on the water ({Lit()} pixels)", Lit() > 20);
        UpdateCatchFx(5);
        Check("and they are all gone a few seconds later", splashRings.Count == 0);
        particles.Clear();
        NoteCatch("pond_perch", 2, 1.1f, 100, 60, false);
        Check("a landing is remembered once for the splash, the held fish and the card", catchSnap is { Id: "pond_perch", Tier: 2 });

        /* ---------- the held-up fish ---------- */
        heldItem = "pond_perch"; heldPeak = 1.4f; catchSnap = null;
        int[] tops = new int[3];
        int i = 0;
        foreach (float left in new[] { 1.4f, 1.0f, 0.5f })
        {
            heldT = left; Clear(); DrawHeld(160, 90); tops[i++] = TopRow();
        }
        Check($"a held fish comes up from below and settles over the real hold time (top row {tops[0]}, {tops[1]}, {tops[2]})", tops[0] > tops[2] + 3);
        catchSnap = new CatchSnap { Id = "pond_perch", Tier = 2, M = 1 };
        heldT = 0.9f; time = 3.1f; Clear(); DrawHeld(160, 90); int rareA = Sum(145, 70, 30, 40);
        catchSnap = null; Clear(); DrawHeld(160, 90); int plain = Sum(145, 70, 30, 40);
        Check($"a rare catch glows round the fish ({rareA} against {plain})", rareA > plain + 150);
        catchSnap = new CatchSnap { Id = "pond_perch", Tier = 2, M = 1 };
        long Region(float t) { time = t; Clear(); DrawHeld(160, 90); long s = 0; for (int y = 60; y < 120; y++) for (int x = 135; x < 185; x++) { var c = pix.Buf[y * W + x]; s = s * 31 + c.R + c.G * 3 + c.B * 7; } return s; }
        d.reduceMotion = false; bool moves = Region(3.1f) != Region(3.4f);
        d.reduceMotion = true; bool still = Region(3.1f) == Region(3.4f) && Region(3.1f) == Region(5.9f);
        d.reduceMotion = false;
        Check($"the glow pulses and the stars twinkle ({moves}), and with Reduce motion they hold still ({still})", moves && still);
        EffectsOff = true; heldT = 0.9f; catchSnap = new CatchSnap { Id = "pond_perch", Tier = 2, M = 1 };
        Clear(); DrawHeld(160, 90); int off = Sum(145, 70, 30, 40);
        EffectsOff = false;
        Check("with light off there is no glow (the old held fish)", off <= plain + 20);
        heldItem = null; heldT = 0; catchSnap = null;

        /* ---------- shake, flash and hit-stop ---------- */
        (player.X, player.Y) = StandableNear(160f, 115f); mode = "play"; yield return 4;
        ClearImpact(); quake = 0;
        EffectsOff = true; Impact(0.3f, "#e04b3a", 0.3f, 0.06f);
        Check("with light off an impact does nothing (the old look)", quake == 0 && screenFlash == 0 && !HitStopped());
        EffectsOff = false; d.light = 2; d.shake = true; d.reduceMotion = false; ClearImpact(); quake = 0;
        Impact(0.3f, "#e04b3a", 0.3f, 0.06f);
        Check($"with light on it shakes ({quake:0.00}), flashes ({screenFlash:0.00}) and pauses a moment", quake >= 0.3f && screenFlash >= 0.3f && HitStopped());
        float t0 = time; yield return 2;
        Check("the game holds still during the pause (the clock of the world doesn't move)", MathF.Abs(time - t0) < 0.0001f);
        double until = stopUntil; int guard = 0;
        while (HitStopped() && guard++ < 60) yield return 1;
        Check($"and it ends on its own within {MaxHitStop:0.00} s ({Raylib.GetTime() - (until - MaxHitStop):0.00} s)", !HitStopped() && Raylib.GetTime() - (until - MaxHitStop) < 0.5);
        yield return 3;
        Check("the world runs again after it", time > t0);
        double firstUntil = stopUntil;
        Impact(0.3f, null, 0, 0.06f);
        Check("a second hit straight away doesn't pause again (no chaining)", stopUntil == firstUntil);
        ClearImpact(); quake = 0; d.shake = false;
        Impact(0.3f, "#ffffff", 0.2f, 0.06f);
        Check("with Screen shake off there is no shake and no pause, only the flash", quake == 0 && !HitStopped() && screenFlash > 0.15f);
        d.shake = true; d.reduceMotion = true; ClearImpact(); quake = 0;
        Impact(0.3f, "#ffffff", 0.3f, 0.06f);
        Check("with Reduce motion there is no shake and no pause either", quake == 0 && !HitStopped() && screenFlash > 0.2f);
        d.reduceMotion = false; ClearImpact();
        Impact(0.2f, null, 0, 0.06f); OpenPause();
        Check("opening the pause menu ends a hit-stop at once", !HitStopped());
        ClosePause(); yield return 3; ClearImpact();
        // Esc pressed while the game holds still still pauses (Codex: the stop used to swallow it).
        Impact(0.2f, null, 0, 0.06f); stopUntil = Raylib.GetTime() + 0.6;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"Esc during a hit-stop still pauses ({mode})", mode == "pause");
        ClosePause(); yield return 3; ClearImpact();
        // Rings left from light on do not draw once light is off.
        splashRings.Add(new SplashRing { X = 100, Y = 60, Life = 1, Age = 0.3f, R = 12, Strength = 0.8f });
        EffectsOff = true; Clear(); DrawSplashRings();
        Check("splash rings vanish when light is switched off", Lit() == 0 && splashRings.Count == 0);
        EffectsOff = false;

        // The flash is drawn over the world: the corners first. With Reduce motion it is a steady edge, not a fill.
        scene = "world";
        d.reduceMotion = false; screenFlash = 0; Clear(); DrawScreenFlash(); int none = Lit();
        screenFlash = 0.5f; flashColour = Pal.C("#e04b3a"); Clear(); DrawScreenFlash();
        bool corner = !pix.Buf[0].Equals(bg), centre = !pix.Buf[(H / 2) * W + W / 2].Equals(bg);
        Check($"a flash tints the whole world ({Lit()} of {W * H} pixels)", none == 0 && corner && centre);
        d.reduceMotion = true; Clear(); DrawScreenFlash();
        bool edge = !pix.Buf[0].Equals(bg), centreCalm = !pix.Buf[(H / 2) * W + W / 2].Equals(bg);
        Check($"with Reduce motion it is a thin steady edge ({Lit()} pixels), the middle stays clear", edge && !centreCalm && Lit() < W * H / 6);
        d.reduceMotion = false; screenFlash = 0;

        // The real damage path.
        ClearImpact(); quake = 0; state.hp = 100; iframes = 0;
        EffectsOff = false; HurtPlayer(14, player.X + 10, player.Y);
        Check($"a hit on you flashes the world and shakes it ({screenFlash:0.00}, {quake:0.00})", screenFlash > 0.2f && quake > 0.1f && HitStopped());
        ClearImpact(); quake = 0; state.hp = 100; iframes = 0; EffectsOff = true; HurtPlayer(14, player.X + 10, player.Y);
        Check("with light off a hit is as it was before", screenFlash == 0 && quake == 0 && !HitStopped());
        EffectsOff = false; ClearImpact(); state.hp = 100; iframes = 0; yield return 3;

        /* ---------- Tidemane's charge lane ---------- */
        boss = new Boss { Phase = "rear", X = 120, Y = 90, AimX = 1, AimY = 0, T = 0.6f, Dir = 1 };
        scene = "world";
        Clear(); camX = camY = 0; DrawBossLane(time); int lane = Lit();
        int Band(int x0, int x1) { int n = 0; for (int y = 86; y < 95; y++) for (int x = x0; x < x1; x++) if (!pix.Buf[y * W + x].Equals(bg)) n++; return n; }
        Check($"the charge lane is drawn along the line it picked, for the length of the run ({lane} pixels)", lane > 40 && Band(125, 185) > 8 && Band(190, 240) > 4 && Band(255, 310) == 0 && Band(0, 110) == 0);
        boss.Phase = "stalk"; Clear(); DrawBossLane(time);
        Check("and only while it rears", Lit() == 0);
        boss.Phase = "rear"; EffectsOff = true; Clear(); DrawBossLane(time);
        Check("not with light off", Lit() == 0);
        EffectsOff = false; boss = null;
        pendingShot = "impact-done"; yield return 2;
        d.light = -1; EffectsOff = keepOff; ClearImpact(); quake = 0;
    }
}
#endif
