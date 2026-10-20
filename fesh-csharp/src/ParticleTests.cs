#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Soft particles (FESH_PARTICLE_TEST=1, also in the full run): with light on every speck fades and dust grows, with light
    // off they are the old opaque pixels; looks-only effects never touch the game's rng; the cap holds; rain, snow and ash
    // keep belonging to the world.
    IEnumerable<int> ParticleScript()
    {
        Note("Particles: soft specks, caps, rng");
        state = new State { created = true, look = new Look { name = "Dusty" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;
        var bg = new Color(40, 90, 60, 255);
        void Clear() { Array.Fill(pix.Buf, bg); pix.CamX = pix.CamY = 0; camX = camY = 0; }
        int Lit() => pix.Buf.Count(c => !c.Equals(bg));
        int At(int x, int y) { var c = pix.Buf[y * W + x]; return c.R + c.G + c.B; }

        /* ---------- light off: the old opaque pixel ---------- */
        EffectsOff = true;
        particles.Clear(); Clear();
        particles.Add(new Particle { X = 50, Y = 40, Life = 0.4f, Color = "#ffffff" });
        DrawParticles();
        Check("with light off a speck is one opaque white pixel", At(50, 40) == 765 && Lit() == 1);
        particles[0].Life = 0.02f; Clear(); DrawParticles();
        Check("and stays opaque to the very end (the old look)", At(50, 40) == 765);

        /* ---------- light on: fade and grow ---------- */
        EffectsOff = false; Settings.Data.light = 2;
        particles.Clear();
        var sp = new Particle { X = 50, Y = 40, Life = 0.5f, Color = "#ffffff", Vx = 0, Vy = 0, Gy = 0 };
        particles.Add(sp);
        var seen = new List<int>();
        foreach (float life in new[] { 0.5f, 0.3f, 0.2f, 0.1f, 0.03f })
        {
            sp.Life = life; Clear(); DrawParticles(); seen.Add(At(50, 40));
        }
        Check($"with light on a speck fades out over the end of its life ({string.Join(", ", seen)})", seen[0] == 765 && seen.Zip(seen.Skip(1), (a, b) => b <= a).All(x => x) && seen[^1] < seen[0] / 2 + 100 && seen[^1] < 400);
        var puff = new Particle { X = 80, Y = 60, Life = 0.6f, Color = "#d8bb7e", Soft = true, Size0 = 1.5f, Size1 = 9, Alpha0 = 0.8f, Gy = 0 };
        particles.Clear(); particles.Add(puff);
        puff.Life = 0.59f; Clear(); DrawParticles(); int young = Lit();
        puff.Life = 0.25f; Clear(); DrawParticles(); int old = Lit();
        Check($"dust grows as it ages ({young} pixels, then {old})", old > young + 6);
        puff.Life = 0.01f; Clear(); DrawParticles();
        Check("and has all but vanished at the end", Lit() <= 2 || pix.Buf.Max(c => Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G) + Math.Abs(c.B - bg.B)) < 12);
        puff.Life = 0.3f; Clear(); DrawParticles();
        var rim = new List<float>();
        foreach (var c in pix.Buf) if (!c.Equals(bg)) rim.Add(Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G));
        Check("a puff has a soft edge (several different strengths, not one flat blob)", rim.Distinct().Count() >= 3);

        /* ---------- the cap ---------- */
        particles.Clear();
        for (int i = 0; i < 1200; i++) particles.Add(new Particle { X = i % 300, Y = i % 100, Life = 5, Color = "#ffffff" });
        UpdateParticles(0.001f);
        Check($"there are never more than {MaxParticles} specks ({particles.Count})", particles.Count <= MaxParticles);
        particles.Clear();

        /* ---------- looks only: the game's own rng is left alone ---------- */
        var keepRng = rng;
        rng = new Random(4242);
        for (int i = 0; i < 300; i++) Puff(100 + i % 20, 60, "#d8bb7e", FxRand(-8, 8), FxRand(-9, -3), FxRand(0.4f, 0.7f), 1.5f, 4f, 0.5f);
        int after = rng.Next();
        Check("300 puffs don't draw a single number from the game's rng", after == new Random(4242).Next());
        particles.Clear();
        // A footstep on sand with light on: the same promise.
        (int sx, int sy) = (-1, -1);
        for (int ty = 6; ty < 70 && sx < 0; ty++) for (int tx = 4; tx < 200 && sx < 0; tx++) if (TileAt(tx, ty) == 's' && CanStand(tx * T + 5, ty * T + 5)) (sx, sy) = (tx, ty);
        if (sx >= 0)
        {
            player.X = sx * T + 5; player.Y = sy * T + 5; yield return 3;
            rng = new Random(77);
            for (int i = 0; i < 20; i++) Footstep();
            int footAfter = rng.Next();
            Check($"footsteps on sand puff softly without using the game's rng ({particles.Count(p => p.Soft)} puffs)", footAfter == new Random(77).Next() && particles.Any(p => p.Soft));
            EffectsOff = true; particles.Clear();
            for (int i = 0; i < 4; i++) Footstep();
            Check("and with light off they are the old hard specks", particles.Count > 0 && particles.All(p => !p.Soft));
            EffectsOff = false;
        }
        rng = keepRng; particles.Clear();

        /* ---------- rain, snow and ash ---------- */
        (player.X, player.Y) = StandableNear(160f, 115f); yield return 4;
        Settings.Data.light = 2; EffectsOff = false; rainAmt = 1; stormAmt = 0; yield return 2;
        Clear(); DrawRain(time);
        var shades = pix.Buf.Where(c => !c.Equals(bg)).Select(c => c.R).Distinct().Count();
        Check($"with light on a raindrop is a streak that fades toward its tail ({shades} different shades)", shades >= 3);
        EffectsOff = true; Clear(); DrawRain(time);
        var shadesOff = pix.Buf.Where(c => !c.Equals(bg)).Select(c => c.R).Distinct().Count();
        Check($"with light off every drop is one flat colour as before ({shadesOff})", shadesOff == 1);
        EffectsOff = false; rainAmt = 0;

        (player.X, player.Y) = StandableNear(620f, 90f); yield return 4;
        Settings.Data.light = 2; EffectsOff = false; rainAmt = 0.3f;
        camX = Math.Max(0, (int)player.X - W / 2); camY = Math.Max(0, (int)player.Y - H / 2);
        Clear(); camX = Math.Max(0, (int)player.X - W / 2); camY = Math.Max(0, (int)player.Y - H / 2);
        DrawSnow(time);
        int levels = pix.Buf.Where(c => !c.Equals(bg)).Select(c => c.R).Distinct().Count();
        Check($"snow comes in three depths with light on ({levels} brightness levels)", levels >= 3);
        rainAmt = 0;

        (player.X, player.Y) = StandableNear(160f, 115f); yield return 4;
        Clear(); camX = 100; camY = 40; DrawAshSoft(); var ash1 = pix.Buf.Select((c, i) => (c, i)).Where(t => !t.c.Equals(bg)).Select(t => t.i).ToHashSet();
        Clear(); camX = 106; camY = 40; DrawAshSoft(); var ash2 = pix.Buf.Select((c, i) => (c, i)).Where(t => !t.c.Equals(bg)).Select(t => t.i).ToHashSet();
        int moved = ash1.Count(i => ash2.Contains(i - 6) || (i % W) < 6);
        Check($"ash belongs to the world: a 6 px shift of the view slides it ({moved} of {ash1.Count})", ash1.Count > 80 && moved >= ash1.Count * 0.7);
        camX = camY = 0;

        // The cost of 400 soft specks at once.
        particles.Clear();
        for (int i = 0; i < 400; i++) particles.Add(new Particle { X = 10 + i % 300, Y = 10 + i % 150, Life = 0.5f, Color = "#d8bb7e", Soft = true, Size0 = 2, Size1 = 5, Alpha0 = 0.5f, Gy = 0 });
        double t0 = Raylib.GetTime();
        for (int i = 0; i < 20; i++) { foreach (var p in particles) p.Life = 0.5f; DrawParticles(); }
        double ms = (Raylib.GetTime() - t0) / 20 * 1000;
        Note($"400 soft specks cost {ms:0.00} ms a frame in a Debug build");
        Check($"400 soft specks stay cheap ({ms:0.00} ms Debug)", ms < 4);
        particles.Clear();

        Settings.Data.light = -1; EffectsOff = keepOff; rainAmt = 0;
        pendingShot = "particles-done"; yield return 2;
    }
}
#endif
