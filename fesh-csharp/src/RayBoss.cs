using Raylib_cs;

namespace Fesh;

// 1.36: the Starfall ray, the atoll's legend, as a boss fight: a giant manta whose back glitters like the night sky, at the
// deep drop-off under a full moon (a glow shrimp). Real mantas filter tiny drifting animals from the water with their
// cephalic fins unrolled, turn backward somersaults where the food is thick, and breach (why, nobody is sure). The fight
// is the ordinary runner reel (let go when it runs) plus one event at a time by how far you've reeled it in, each with
// its own move:
//   1 along the silver edge: a feeding sweep: it banks along the drop-off and you follow it with the rod (hold left or
//     right, or the stick) to keep your marker on it;
//   2 a turning constellation: it also loops backward: let go of the reel through the somersault, or the loop takes line;
//   3 stars above the sea: it also breaches: lower the rod (hold down) to give it slack as it flies, as anglers "bow" to
//     a leaping fish.
// Nothing hurts you, and a miss costs a little reel, never the line. Mantas are endangered: when it's in, you free the
// line and it glides back over the drop-off (the record, the card and the album are yours; nothing goes in the bag).
// Its sprite is a Blender model (tools/blender/manta.py renders MantaArt.cs) seen from above at eight headings with
// three steps of the wing beat, and a loop (nose up, belly up and round); stars on its back are coloured here, so they
// twinkle and glow after dark. FESH_OLDART=1 draws the Fish log's picture instead.
partial class Game
{
    const string RayFish = "starfall_ray";

    sealed class RayFight
    {
        public float Banner = 3.2f, T, X, Y, CX, CY, Heading, TargetX, TargetY, WanderT;
        public string Ev = "";          // "", "sweepWarn", "sweep", "loopWarn", "loop", "breachWarn", "breach"
        public float EvT, NextEv = 5f, Mark = 0.5f, Rod = 0.5f, OnT, FreeT, BowT, SweepPh;
        public int Phase = 1, Events;
        public string Title = "STARFALL RAY", Sub = "Legend of the deep drop-off";
    }
    RayFight ray;

    bool RayOnLine => mode == "reeling" && reel?.Roll?.Id == RayFish && fish != null;
    bool RayShown => (mode == "reeling" || mode == "pause" && pausedFrom == "reeling") && reel?.Roll?.Id == RayFish && fish != null && ray != null;
    static int RayPhaseOf(float p) => p < 0.5f ? 1 : p < 0.78f ? 2 : 3;
    const float RayWarnLen = 1.3f, SweepLen = 2.8f, LoopLen = 1.9f, BreachLen = 1.6f, SweepNeed = 1.2f, LoopNeed = 1.35f, BowNeed = 1.0f;

    // Deep water for it and its wingspan, out over the drop-off: never the shallows or the beach.
    bool RayWater(float x, float y) => TileUnder(x, y) == '~' && TileUnder(x - 16, y) == '~' && TileUnder(x + 16, y) == '~' && TileUnder(x, y - 10) == '~' && TileUnder(x, y + 10) == '~';

    void StartRay()
    {
        ray = new RayFight { X = fish.Bx, Y = fish.By, CX = fish.Bx, CY = fish.By };
        // A cast that came down at the edge of the shallows: its middle is the nearest deep water out from there.
        if (!RayWater(ray.CX, ray.CY))
            for (int r = 4; r < 80 && !RayWater(ray.CX, ray.CY); r += 4)
                for (int a = 0; a < 16; a++)
                {
                    float x = fish.Bx + MathF.Cos(a * MathF.Tau / 16) * r, y = fish.By + MathF.Sin(a * MathF.Tau / 16) * r;
                    if (RayWater(x, y)) { ray.CX = x; ray.CY = y; break; }
                }
        ray.X = ray.TargetX = ray.CX; ray.Y = ray.TargetY = ray.CY;
        reel.Progress = reel.Perfect ? 0.38f : 0.3f;
        reel.Pull = 0.75f;
        quake = 0.3f;
        Sfx.Play("splash");
    }

    // It glides in slow curves round where it took the bait, out over the deep water, and turns the way it's going.
    void MoveRay(float dt)
    {
        var m = ray;
        if (m.Ev == "sweep")
        {
            // Banking along the drop-off: across and back, the marker you follow.
            m.SweepPh += dt * 1.6f;
            m.Mark = 0.5f + 0.36f * MathF.Sin(m.SweepPh);
            float tx = m.CX + (m.Mark - 0.5f) * 90;
            if (RayWater(tx, m.Y)) { m.Heading = tx > m.X ? 0 : MathF.PI; m.X = tx; }
        }
        else if (m.Ev is "" or "sweepWarn" or "loopWarn" or "breachWarn")
        {
            m.WanderT -= dt;
            if (m.WanderT <= 0 || Dist(m.X, m.Y, m.TargetX, m.TargetY) < 4)
            {
                m.WanderT = Rand(2.5f, 4.5f);
                for (int i = 0; i < 12; i++)
                {
                    float a = Rand(0, MathF.Tau), d = Rand(8, 46), x = m.CX + MathF.Cos(a) * d, y = m.CY + MathF.Sin(a) * d * 0.6f;
                    if (RayWater(x, y) && Dist(x, y, player.X, player.Y) > 30) { m.TargetX = x; m.TargetY = y; break; }
                }
            }
            float dx = m.TargetX - m.X, dy = m.TargetY - m.Y, d2 = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            float speed = m.Ev == "" ? 15 : 7;
            if (d2 > 0.5f)
            {
                float want = MathF.Atan2(dy, dx), turn = MathF.IEEERemainder(want - m.Heading, MathF.Tau);
                m.Heading += Math.Clamp(turn, -dt * 2.2f, dt * 2.2f);
                float nx = m.X + MathF.Cos(m.Heading) * speed * dt, ny = m.Y + MathF.Sin(m.Heading) * speed * dt;
                if (RayWater(nx, ny)) { m.X = nx; m.Y = ny; } else m.WanderT = 0;
            }
        }
        fish.Bx = fish.Tx = m.X; fish.By = fish.Ty = m.Y;
    }

    // Called from UpdateReel: true while an event has the reel (the bar waits).
    bool UpdateRayReel(ReelState r, float dt, bool hold, bool tap)
    {
        ray ??= new RayFight { X = fish.Bx, Y = fish.By, CX = fish.Bx, CY = fish.By, TargetX = fish.Bx, TargetY = fish.By };
        var m = ray;
        m.T += dt;
        m.Banner = Math.Max(0, m.Banner - dt);
        int phase = Math.Max(m.Phase, RayPhaseOf(r.Progress));
        if (phase > m.Phase)
        {
            m.Phase = phase;
            m.Events = 0;
            m.Banner = 3.2f;
            m.Title = phase == 2 ? "A TURNING CONSTELLATION" : "STARS ABOVE THE SEA";
            m.Sub = phase == 2 ? "When it loops, let go of the reel" : "When it breaches, lower the rod: hold <down>";
            m.NextEv = MathF.Max(m.NextEv, 3.4f);
            Sfx.Play("splash");
        }
        MoveRay(dt);
        if (m.Ev == "")
        {
            // Never in the middle of one of its runs.
            if (r.Running > 0) return false;
            m.NextEv -= dt;
            if (m.NextEv > 0) return false;
            m.Events++;
            string next = m.Phase == 1 ? "sweep"
                : m.Phase == 2 ? (m.Events % 2 == 1 ? "loop" : "sweep")
                : (m.Events % 3) switch { 1 => "breach", 2 => "loop", _ => "sweep" };
            BeginRayEvent(next);
        }
        switch (m.Ev)
        {
            case "sweepWarn":
                SetPrompt("It unrolls its fins to feed... get ready to follow it with <left> and <right>", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "sweep"; m.EvT = SweepLen; m.OnT = 0; m.SweepPh = 0; m.Rod = 0.5f; m.CX = m.X; }
                return true;
            case "sweep":
            {
                // Your rod follows the keys or the stick; keep it on the ray as it banks across.
                float push = (Bind.Down("right") ? 1 : 0) - (Bind.Down("left") ? 1 : 0);
                if (MathF.Abs(Inp.Stick.X) > 0.2f) push = Inp.Stick.X;
                m.Rod = Math.Clamp(m.Rod + push * dt * 0.9f, 0, 1);
                if (MathF.Abs(m.Rod - m.Mark) < 0.12f) m.OnT += dt;
                SetPrompt("It's feeding along the edge! Follow it with <left> and <right>", null, true);
                if (m.OnT >= SweepNeed)
                {
                    r.Progress += 0.05f;
                    Floater("You followed it!", player.X, player.Y - 26, "#bfe3ff");
                    Sfx.Play("coin");
                    if (r.Progress >= 1) { LandCatch(); return true; }
                    EndRayEvent();
                }
                else if ((m.EvT -= dt) <= 0)
                {
                    MirePenalty(r, 0.03f);
                    Floater("It glides farther out.", player.X, player.Y - 26, "#ff9a8a");
                    EndRayEvent();
                }
                return true;
            }
            case "loopWarn":
                SetPrompt("Its mouth opens and its head tips up: it's going to loop. Let go of the reel!", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "loop"; m.EvT = LoopLen; m.FreeT = 0; Sfx.Play("splash"); }
                return true;
            case "loop":
                if (!hold) m.FreeT += dt;
                SetPrompt(hold ? "Let go! It's somersaulting!" : "That's it: let it loop...", null, true);
                if ((m.EvT -= dt) <= 0)
                {
                    if (m.FreeT >= LoopNeed)
                    {
                        r.Progress += 0.04f;
                        Floater("You gave it line!", player.X, player.Y - 26, "#bfe3ff");
                        Sfx.Play("coin");
                        if (r.Progress >= 1) { LandCatch(); return true; }
                    }
                    else { MirePenalty(r, 0.05f); Floater("The loop takes more line.", player.X, player.Y - 26, "#ff9a8a"); }
                    EndRayEvent();
                }
                return true;
            case "breachWarn":
                SetPrompt("It's coming up fast! Lower the rod: hold <down>", null, true);
                // Lowering the rod early is fine, but only time bowed while it's in the air counts (Codex).
                if ((m.EvT -= dt) <= 0) { m.Ev = "breach"; m.EvT = BreachLen; Sfx.Play("splash"); quake = 0.2f; }
                return true;
            case "breach":
                if (Bind.Down("down")) m.BowT += dt;
                SetPrompt("It breaches! Keep the rod down: hold <down>", null, true);
                fish.By = fish.Ty = m.Y - RayLift(1 - m.EvT / BreachLen);   // the line follows it up
                if ((m.EvT -= dt) <= 0)
                {
                    // The belly flop: a huge splash where it comes down.
                    Burst(m.X, m.Y, "#e8f4ff", 18); quake = 0.45f; Sfx.Play("splash");
                    if (m.BowT >= BowNeed)
                    {
                        r.Progress += 0.06f;
                        Floater("You bowed to it!", player.X, player.Y - 26, "#bfe3ff");
                        Sfx.Play("coin");
                        if (r.Progress >= 1) { LandCatch(); return true; }
                    }
                    else { MirePenalty(r, 0.06f); Floater("The splash pulls out line.", player.X, player.Y - 26, "#ff9a8a"); }
                    EndRayEvent();
                }
                return true;
        }
        return false;
    }

    void BeginRayEvent(string ev)
    {
        var m = ray;
        var r = reel;
        m.Ev = ev + "Warn";
        m.EvT = RayWarnLen;
        m.BowT = 0;
        r.Running = 0; r.Tension = Math.Min(r.Tension, 0.3f); r.ZoneV = 0;
        if (ev == "sweep") m.CX = m.X;
    }

    void EndRayEvent()
    {
        var m = ray;
        m.Ev = "";
        m.NextEv = m.Phase == 1 ? Rand(6.5f, 9f) : Rand(5f, 7.5f);
        m.WanderT = 0;
        if (reel != null) { reel.RunT = Rand(1.6f, 3f); reel.Tension = 0; reel.FishTimer = 0.4f; }
    }

    // Every frame: forget a finished fight (the pause keeps it).
    void UpdateRay(float dt)
    {
        if (!RayOnLine && mode != "pause") ray = null;
        UpdateRayHome(dt);
    }

    // Landed: you free the line and it glides back over the drop-off. It's in your records and on its card, never in the
    // bag (call straight after AddCatch put it there).
    void FreeRay()
    {
        if (ray != null) { rayLastX = ray.X; rayLastY = ray.Y; }
        Take(RayFish);
        // Only this one's premium, if it was big: an older big one in the bag keeps its own (Codex).
        if (lastM >= 1.25f && state.big.GetValueOrDefault(RayFish) > 0) state.big[RayFish]--;
    }

    /* ---------- The picture ---------- */
    static (Color[] col, byte[] h, byte[] star)[] rayFrames;
    static readonly Color[] RayStar = { Pal.C("#8ea6d6"), Pal.C("#d9e6ff"), Pal.C("#ffffff") };

    static (Color[] col, byte[] h, byte[] star) RayFrame(int i)
    {
        if (rayFrames == null)
        {
            var pal = MantaArt.Palette.Select(Pal.C).ToArray();
            int n = MantaArt.Width * MantaArt.Height;
            rayFrames = new (Color[], byte[], byte[])[MantaArt.Data.Length];
            for (int f = 0; f < rayFrames.Length; f++)
            {
                using var src = new MemoryStream(Convert.FromBase64String(MantaArt.Data[f]));
                using var z = new System.IO.Compression.ZLibStream(src, System.IO.Compression.CompressionMode.Decompress);
                var raw = new byte[n * 2];
                int got = 0;
                while (got < raw.Length) { int k = z.Read(raw, got, raw.Length - got); if (k <= 0) break; got += k; }
                var col = new Color[n];
                var star = new byte[n];
                for (int p = 0; p < n; p++)
                {
                    byte v = raw[p];
                    if (v == 255) continue;
                    if (v >= 250) { star[p] = (byte)(v - 249); col[p] = RayStar[v - 250]; }
                    else col[p] = pal[v];
                }
                rayFrames[f] = (col, raw[n..], star);
            }
        }
        return rayFrames[i];
    }

    // The gliding frame nearest a heading (radians, screen: 0 east, y down) and wing step; or the loop frame nearest an
    // angle (degrees nose-up about its wings).
    static int RayGlideFrame(float heading, int flap)
    {
        // Blender's headings turn counter-clockwise seen from above; the screen's y runs down.
        float deg = (-heading * 180 / MathF.PI % 360 + 360) % 360;
        int h = (int)MathF.Round(deg / 45) % 8 * 45;
        for (int i = 0; i < MantaArt.Heading.Length; i++)
            if (MantaArt.Loop[i] == 0 && MantaArt.Heading[i] == h && MantaArt.Flap[i] == flap) return i;
        return 0;
    }

    static int RayLoopFrame(float loop)
    {
        loop = (loop % 360 + 360) % 360;
        if (loop < 22.5f || loop >= 337.5f) return RayGlideFrame(MathF.PI / 2, 1);   // flat, facing you
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < MantaArt.Loop.Length; i++)
        {
            if (MantaArt.Loop[i] == 0) continue;
            float d = MathF.Abs(MantaArt.Loop[i] - loop);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // Draws frame f with its middle `sink` rows under the water at (x, y) (negative: in the air). Above the water it's
    // drawn as it is; under it, a dim blue shape (unless dropUnder), though its stars still show. starsOnly redraws just
    // the stars, bright, for after the dark.
    void DrawRayFrame(int f, float x, float y, float sink, bool dropUnder, float t, bool starsOnly = false, float k = 1)
    {
        if (OldArt)
        {
            if (starsOnly || sink > 10) return;
            FishArt.Draw(pix, RayFish, (int)x - 18, (int)(y + MathF.Min(0, sink)) - 8, 36, 16);
            return;
        }
        var (col, h, star) = RayFrame(f);
        int w = MantaArt.Width, hh = MantaArt.Height, ax = MantaArt.AnchorX, ay = MantaArt.AnchorY;
        int ox = (int)MathF.Round(x), oy = (int)MathF.Round(y + sink);
        var deep = Pal.C("#173a5c");
        for (int j = 0; j < hh; j++)
            for (int i = 0; i < w; i++)
            {
                int p = j * w + i;
                var c = col[p];
                if (c.A == 0) continue;
                int px = ox + i - ax, py = oy + j - ay;
                bool above = h[p] - MantaArt.Lift > sink;
                if (star[p] > 0)
                {
                    // Each star twinkles on its own beat.
                    float tw = 0.55f + 0.45f * MathF.Sin(t * 3.1f + (float)Pix.Hash(i, j, 77) * 20);
                    if (!above && dropUnder) continue;
                    if (!above && !InSeaPx(px, py)) continue;
                    pix.Rect(px, py, 1, 1, Pal.WithAlpha(c, (above ? 1 : 0.85f) * tw * (starsOnly ? k : 1)));
                    continue;
                }
                if (starsOnly) continue;
                if (above) { pix.Rect(px, py, 1, 1, c); continue; }
                if (dropUnder || !InSeaPx(px, py)) continue;
                var u = Pix.Mix(c, deep, 0.38f);
                u.A = 205;
                pix.Rect(px, py, 1, 1, u);
            }
    }

    bool InSeaPx(float x, float y) => Swimmable(TileUnder(x, y + 1.5f));

    int RayFlap(float t) => ((int)(t * 2.6f) % 4) switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 };   // brackets: % binds looser than switch (Codex)

    // Under the water: gliding (stars glinting through), the plankton gathering before a sweep, the loop's outline before
    // a somersault and the somersault itself, and the rising oval before a breach.
    void DrawRayUnder(float t)
    {
        if (!RayShown) return;
        var m = ray;
        if (m.Ev == "breach") return;
        if (m.Ev is "loop")
        {
            float k = 1 - m.EvT / LoopLen;
            DrawRayFrame(RayLoopFrame(k * 360), m.X, m.Y, 7, false, t);
            return;
        }
        DrawRayFrame(RayGlideFrame(m.Heading, RayFlap(t)), m.X, m.Y, RayUnderSink(m), false, t);
        if (m.Ev is "sweepWarn" or "sweep")
        {
            // Plankton drawn to the glow, a pale cloud the ray feeds through.
            for (int i = 0; i < 26; i++)
            {
                float a = (float)Pix.Hash(i, 3, 91) * MathF.Tau, r = 6 + (float)Pix.Hash(i, 4, 91) * 26;
                float px = m.X + MathF.Cos(a + t * 0.4f) * r, py = m.Y + MathF.Sin(a + t * 0.4f) * r * 0.5f;
                if (InSeaPx(px, py)) pix.Rect(px, py, 1, 1, Pal.Rgba(190, 255, 220, 0.35f + 0.3f * MathF.Sin(t * 4 + i)));
            }
        }
        if (m.Ev == "loopWarn")
        {
            // The loop it's about to turn: a dashed ring rolling forward.
            for (int i = 0; i < 24; i++)
            {
                if ((i + (int)(t * 12)) % 3 == 0) continue;
                float a = i / 24f * MathF.Tau;
                pix.Rect(m.X + MathF.Cos(a) * 16, m.Y + MathF.Sin(a) * 7, 1, 1, Pal.Rgba(200, 225, 255, 0.75f));
            }
        }
        if (m.Ev == "breachWarn")
        {
            float k = 1 - m.EvT / RayWarnLen;
            pix.Ring(m.X, m.Y, 6 + k * 16, Pal.Rgba(225, 240, 255, 0.4f + 0.5f * k));
            pix.Ring(m.X, m.Y, 3 + k * 9, Pal.Rgba(225, 240, 255, 0.3f + 0.4f * k));
        }
    }

    // Out of the water: the breach, belly first, wings spread across the moon, and the flop back down.
    void DrawRayOver(float t)
    {
        if (!RayShown) return;
        var m = ray;
        if (m.Ev != "breach") return;
        float k = 1 - m.EvT / BreachLen, lift = RayLift(k);
        float loop = k < 0.4f ? 45 + k / 0.4f * 45 : k < 0.7f ? 90 + (k - 0.4f) / 0.3f * 45 : 135 + (k - 0.7f) / 0.3f * 45;
        pix.Rect(m.X - 16, m.Y + 1, 32, 2, Pal.Rgba(8, 20, 40, 0.25f));
        if (k < 0.2f)
            for (int i = 0; i < 12; i++)
                pix.Rect(m.X + (i - 6) * 2.4f, m.Y - MathF.Abs(MathF.Sin(i * 1.7f)) * 10 * (1 - k / 0.2f), 1, 2, Pal.Rgba(230, 242, 255, 0.9f));
        // Water pouring off its wings.
        for (int i = 0; i < 8; i++)
        {
            float d = (t * 40 + i * 5) % 22;
            pix.Rect(m.X + (i - 4) * 4, m.Y - lift + d, 1, 1, Pal.Rgba(215, 235, 255, 0.7f * (1 - d / 22)));
        }
        DrawRayFrame(RayLoopFrame(loop), m.X, m.Y, -lift, true, t);
    }

    // By night the moon lights the water round it, and after the dark its stars shine.
    void LightRay()
    {
        if (!RayShown || ray is not { } m) return;
        LightHole(m.X, m.Y, 42, 0.75f);
        if (m.Ev == "breach") LightHole(m.X, m.Y - RayLift(1 - m.EvT / BreachLen), 34, 0.8f);
    }

    void GlowRay(float t, float k)
    {
        if (!RayShown || ray is not { } m || k <= 0 || OldArt) return;
        if (m.Ev == "breach")
        {
            float q = 1 - m.EvT / BreachLen, lift = RayLift(q);
            float loop = q < 0.4f ? 45 + q / 0.4f * 45 : q < 0.7f ? 90 + (q - 0.4f) / 0.3f * 45 : 135 + (q - 0.7f) / 0.3f * 45;
            DrawRayFrame(RayLoopFrame(loop), m.X, m.Y, -lift, true, t, starsOnly: true, k: k);
        }
        else if (m.Ev == "loop") DrawRayFrame(RayLoopFrame((1 - m.EvT / LoopLen) * 360), m.X, m.Y, 7, false, t, starsOnly: true, k: k);
        else DrawRayFrame(RayGlideFrame(m.Heading, RayFlap(t)), m.X, m.Y, RayUnderSink(m), false, t, starsOnly: true, k: k);
        pix.Glow(m.X, m.Y, 22, Pal.Rgba(140, 180, 255, 0.10f * k));
    }

    // The camera frames you and the ray, with room above for a breach.
    float rayCamK, rayCamX, rayCamY;
    (float x, float y) RayCamera(float fx, float fy)
    {
        rayCamK = Math.Clamp(rayCamK + (RayShown && scene == "world" ? 1 : -1) * Raylib.GetFrameTime() * 1.6f, 0, 1);
        if (rayCamK <= 0) return (fx, fy);
        if (ray != null) (rayCamX, rayCamY) = (ray.CX, ray.CY);   // easing out from where it last framed it
        // Centred just above its water, so a breach rises clear of the boss bar and you stay in view below.
        float tx = (player.X + rayCamX) / 2, ty = rayCamY - 12;
        float k = rayCamK * rayCamK * (3 - 2 * rayCamK);
        return (fx + (tx - fx) * k, fy + (ty - fy) * k);
    }

    // By the reel: what to do now. The sweep's track runs along the bottom of the screen.
    void DrawRayCue(RayFight m, float rx, Func<float, float> Y)
    {
        void Big(string s, Color c, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui700, 30), Y(at), FontKind.Ui700, 30, c);
        void Small(string s, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui600, 18), Y(at), FontKind.Ui600, 18, Pal.C("#ffffff"));
        switch (m.Ev)
        {
            case "sweepWarn": Big("Feeding...", Pal.C("#bfe3ff"), 130); Small(Bind.Fix("Follow: <left> <right>"), 168); break;
            case "loopWarn": Big("LET GO!", Pal.C("#f3c25b"), 130); Small("It's going to loop", 168); break;
            case "breachWarn":
            case "breach": Big("BOW!", Pal.C("#f3c25b"), 130); Small(Bind.Fix("Hold <down>"), 168); break;
        }
        if (m.Ev is "loop" or "breach" or "breachWarn")
        {
            // A ring filling with time let go, or time bowed.
            float need = m.Ev == "loop" ? LoopNeed : BowNeed, have = m.Ev == "loop" ? m.FreeT : m.BowT;
            float cx = rx - 40, cy = Y(232), k = Math.Clamp(have / need, 0, 1);
            Gfx.Rect(cx - 30, cy - 30, 60, 60, Pal.C("rgba(10,20,48,0.85)"), 30);
            for (int i = 0; i < 40; i++)
            {
                if (i / 40f > k) break;
                float a = -MathF.PI / 2 + i / 40f * MathF.Tau;
                Gfx.Rect(cx + MathF.Cos(a) * 22 - 3, cy + MathF.Sin(a) * 22 - 3, 6, 6, k >= 1 ? Pal.C("#7fd36b") : Pal.C("#bfe3ff"), 3);
            }
            if (m.Ev == "loop") Small("Let go", 270);
        }
        if (m.Ev == "sweep")
        {
            const float w = 460, hgt = 30;
            float x = Gfx.LW / 2 - w / 2, y = Gfx.LH - 130;
            Gfx.Rect(x - 6, y - 6, w + 12, hgt + 12, Pal.C("rgba(10,20,48,0.92)"), 6);
            Gfx.Rect(x, y, w, hgt, Pal.C("#1d4f78"), 4);
            Gfx.Rect(x + w * (m.Mark - 0.12f), y, w * 0.24f, hgt, Pal.C("#bfe3ff"), 3);
            float rxp = x + w * m.Rod;
            Gfx.Rect(rxp - 3, y - 8, 6, hgt + 16, MathF.Abs(m.Rod - m.Mark) < 0.12f ? Pal.C("#7fd36b") : White);
            Gfx.Rect(x, y + hgt + 4, w * Math.Clamp(m.OnT / SweepNeed, 0, 1), 4, Pal.C("#7fd36b"));
            string s = Bind.Fix("FOLLOW IT!  <left> <right>");
            Outlined(s, Gfx.LW / 2 - Gfx.Measure(s, FontKind.Ui700, 26) / 2, y - 40, FontKind.Ui700, 26, Pal.C("#bfe3ff"));
        }
    }

    // After its card: it glides away over the drop-off, its stars fading into the deep.
    (float x, float y, float heading, float t)? rayHome;
    float rayLastX, rayLastY;

    void LetRayGo()
    {
        float away = MathF.Atan2(rayLastY - player.Y, rayLastX - player.X);
        rayHome = (rayLastX, rayLastY, away, 0);
        Sfx.Play("release");
        Floater("Let go", player.X, player.Y - 30, "#bfe3ff");
        Toast("For a moment the stars swam beside you. You free the line, and the ray glides back over the drop-off.", 5);
    }

    void UpdateRayHome(float dt)
    {
        if (rayHome is not var (x, y, hd, t) || mode is "pause") return;
        t += dt;
        // It only glides on over deep water; against a shore it just sinks away where it is (Codex).
        float nx = x + MathF.Cos(hd) * 18 * dt, ny = y + MathF.Sin(hd) * 18 * dt;
        if (!RayWater(nx, ny)) (nx, ny) = (x, y);
        rayHome = t > 4 || scene != "world" ? null : (nx, ny, hd, t);
    }

    void DrawRayHome(float t)
    {
        if (rayHome is not var (x, y, hd, k)) return;
        DrawRayFrame(RayGlideFrame(hd, RayFlap(t)), x, y, 8 + k * 4, false, t);
    }

    // How high a breach rises at k (0 to 1): up to 36 px, but never so high it goes behind the boss bar, as the drop-off
    // lies right at the top of the map, where the view can't scroll any further up.
    float RayLift(float k) => MathF.Sin(k * MathF.PI) * Math.Clamp(ray.Y - camY - 34, 14, 36);

    // How deep its middle is under the water while it glides: rising toward the surface before a breach.
    static float RayUnderSink(RayFight m) => m.Ev == "breachWarn" ? 8 - 4 * (1 - m.EvT / RayWarnLen) : 8;
}
