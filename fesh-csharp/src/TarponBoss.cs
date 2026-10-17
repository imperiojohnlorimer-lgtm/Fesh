using Raylib_cs;

namespace Fesh;

// 1.36: Haring Buan-buan, the king of the tarpon, as a boss fight under the Parola lighthouse (full moon, night, a tamban,
// once Bakunawa has given the moon back). Indo-Pacific tarpon really do rise to gulp air at the surface, and tarpon are
// famous for leaping and shaking their heads; the lit lighthouse beam sweeps the water, and as it passes his scales
// flash. The fight is the ordinary jumper reel (time a press when he leaps) plus one event at a time by how far you've
// reeled him in, each with its own move:
//   1 a breath of moonlight: he rolls at the surface to breathe: hold <up> to lift the line clear of him;
//   2 between the pilings: he turns for the pier: mash <act> to crank him clear before the line wraps a piling;
//   3 the king shakes his crown: a head-shake at the surface: hold <act> steadily through it.
// Nothing hurts you; a miss costs a little reel, never the line. When he's in you ease the hook free beside the pier
// and he swims out of the light (the record, card and album are yours, nothing goes in the bag). His sprite is a Blender
// model (tools/blender/tarpon.py renders TarponArt.cs): nose up to down, the tail's beat, the mouth open, rolled on his
// side; some scales are coloured here so they flash in the beam. FESH_OLDART=1 draws the Fish log's picture instead.
partial class Game
{
    const string TarponFish = "haring_buan";

    sealed class TarponFight
    {
        public float Banner = 3.2f, T, X, Y, CX, CY, Vx = 1, TargetX, TargetY, WanderT;
        public string Ev = "";          // "", "breathWarn", "breath", "pilingsWarn", "pilings", "shakeWarn", "shake"
        public float EvT, NextEv = 5f, Lift, Steady, PileX, PileY;
        public int Phase = 1, Events, Cranks;
        public bool Broke;
        public string Title = "HARING BUAN-BUAN", Sub = "King beneath the lighthouse";
    }
    TarponFight tarpon;

    bool TarponOnLine => mode == "reeling" && reel?.Roll?.Id == TarponFish && fish != null;
    bool TarponShown => (mode == "reeling" || mode == "pause" && pausedFrom == "reeling") && reel?.Roll?.Id == TarponFish && fish != null && tarpon != null;
    static int TarponPhaseOf(float p) => p < 0.5f ? 1 : p < 0.78f ? 2 : 3;
    const float TarponWarnLen = 1.4f, BreathLen = 2.0f, PilingsLen = 2.2f, ShakeLen = 2.0f, LiftNeed = 1.0f, CranksNeed = 7, SteadyNeed = 1.5f;

    // Water for him, nose to tail: never the pier's planks or the beach.
    bool TarponWater(float x, float y)
    {
        // Every 3 px along his body (a plank is 10 px, so none fits between) and above and below his middle (Codex).
        for (float dx = -21; dx <= 21; dx += 3) if (!Swimmable(TileUnder(x + dx, y))) return false;
        return Swimmable(TileUnder(x, y - 6)) && Swimmable(TileUnder(x, y + 6)) && Swimmable(TileUnder(x + 21, y));
    }

    void StartTarpon()
    {
        tarpon = new TarponFight { X = fish.Bx, Y = fish.By, CX = fish.Bx, CY = fish.By };
        if (!TarponWater(tarpon.CX, tarpon.CY))
            for (int r = 4; r < 160 && !TarponWater(tarpon.CX, tarpon.CY); r += 4)
                for (int a = 0; a < 32; a++)
                {
                    float x = fish.Bx + MathF.Cos(a * MathF.Tau / 32) * r, y = fish.By + MathF.Sin(a * MathF.Tau / 32) * r;
                    if (TarponWater(x, y)) { tarpon.CX = x; tarpon.CY = y; break; }
                }
        tarpon.X = tarpon.TargetX = tarpon.CX; tarpon.Y = tarpon.TargetY = tarpon.CY;
        reel.Progress = reel.Perfect ? 0.38f : 0.3f;
        reel.Pull = 0.75f;
        quake = 0.3f;
        Sfx.Play("splash");
    }

    // The nearest piling: the pier's planks ('b') closest to him, and the water just off them where he's heading.
    (float x, float y) NearestPiling(float fx, float fy)
    {
        (float x, float y) best = (fx, fy);
        float bd = float.MaxValue;
        for (int ty = (int)(fy / T) - 6; ty <= (int)(fy / T) + 6; ty++)
            for (int tx = (int)(fx / T) - 8; tx <= (int)(fx / T) + 8; tx++)
            {
                if (TileAt(tx, ty) != 'b') continue;
                // The water beside this plank, on his side of it.
                float px = tx * T + (fx < tx * T + 5 ? -4 : 14), py = ty * T + 5, d = Dist(px, py, fx, fy);
                if (d < bd && Swimmable(TileUnder(px, py))) { bd = d; best = (px, py); }
            }
        return best;
    }

    void MoveTarpon(float dt)
    {
        var m = tarpon;
        float speed = 0;
        if (m.Ev == "pilings") { m.TargetX = m.PileX; m.TargetY = m.PileY; speed = 26; }
        else if (m.Ev is "" or "pilingsWarn")
        {
            speed = m.Ev == "" ? 14 : 8;
            m.WanderT -= dt;
            if (!TarponWater(m.X, m.Y)) (m.TargetX, m.TargetY) = NearestTarponWater(m.X, m.Y);   // stranded by the pier: to the nearest open water
            else if (m.WanderT <= 0 || Dist(m.X, m.Y, m.TargetX, m.TargetY) < 3)
            {
                m.WanderT = Rand(2.5f, 4.5f);
                for (int i = 0; i < 12; i++)
                {
                    float a = Rand(0, MathF.Tau), d = Rand(8, 44), x = m.CX + MathF.Cos(a) * d, y = m.CY + MathF.Sin(a) * d * 0.6f;
                    if (TarponWater(x, y) && Dist(x, y, player.X, player.Y) > 24) { m.TargetX = x; m.TargetY = y; break; }
                }
            }
        }
        float dx = m.TargetX - m.X, dy = m.TargetY - m.Y, d2 = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        if (speed > 0 && d2 > 0.5f)
        {
            float step = MathF.Min(d2, speed * dt), nx = m.X + dx / d2 * step, ny = m.Y + dy / d2 * step;
            // Into the pilings he may brush the pier; otherwise only where he fits.
            // Out of the pilings (or anywhere he doesn't fit) he may swim on through any water, back to where he does.
            bool loose = m.Ev == "pilings" || !TarponWater(m.X, m.Y);
            if (loose ? Swimmable(TileUnder(nx, ny)) : TarponWater(nx, ny)) { m.X = nx; m.Y = ny; } else m.WanderT = 0;
            if (MathF.Abs(dx) > 0.5f) m.Vx = MathF.Sign(dx);
        }
        fish.Bx = fish.Tx = m.X; fish.By = fish.Ty = m.Y;
        if (reel?.Leap > 0)
        {
            // Through a leap the line follows him up into the air.
            float k = Math.Clamp(1 - reel.Leap / reel.LeapLen, 0, 1);
            fish.Bx = fish.Tx = m.X + (k - 0.5f) * 30 * (m.Vx < 0 ? -1 : 1);
            fish.By = fish.Ty = m.Y - MathF.Sin(k * MathF.PI) * 26;
        }
    }

    // Called from UpdateReel: true while an event has the reel (the bar, and the leaps, wait).
    bool UpdateTarponReel(ReelState r, float dt, bool hold, bool tap)
    {
        tarpon ??= new TarponFight { X = fish.Bx, Y = fish.By, CX = fish.Bx, CY = fish.By, TargetX = fish.Bx, TargetY = fish.By };
        var m = tarpon;
        m.T += dt;
        m.Banner = Math.Max(0, m.Banner - dt);
        int phase = Math.Max(m.Phase, TarponPhaseOf(r.Progress));
        if (phase > m.Phase)
        {
            m.Phase = phase;
            m.Events = 0;
            m.Banner = 3.2f;
            m.Title = phase == 2 ? "BETWEEN THE PILINGS" : "THE KING SHAKES HIS CROWN";
            m.Sub = phase == 2 ? "When he turns for the pier, mash <act> to crank him clear" : "When he shakes, hold <act> steady";
            m.NextEv = MathF.Max(m.NextEv, 3.4f);
            Sfx.Play("splash");
        }
        MoveTarpon(dt);
        if (m.Ev == "")
        {
            // Never in the middle of one of his leaps (Codex).
            if (r.Leap > 0) return false;
            m.NextEv -= dt;
            if (m.NextEv > 0) return false;
            m.Events++;
            string next = m.Phase == 1 ? "breath"
                : m.Phase == 2 ? (m.Events % 2 == 1 ? "pilings" : "breath")
                : (m.Events % 3) switch { 1 => "shake", 2 => "pilings", _ => "breath" };
            if (next == "pilings" && !PilingNear(m)) next = "breath";
            m.Ev = next + "Warn"; m.EvT = TarponWarnLen; m.Lift = 0; m.Cranks = 0; m.Steady = 0; m.Broke = false;
            r.HoldT = 0; r.ZoneV = 0;
            if (next == "pilings") (m.PileX, m.PileY) = NearestPiling(m.X, m.Y);
            Sfx.Play("splash");
        }
        switch (m.Ev)
        {
            case "breathWarn":
                SetPrompt("His nose is rising... he's coming up to breathe. Get ready to hold <up>", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "breath"; m.EvT = BreathLen; Sfx.Play("gulp"); }
                return true;
            case "breath":
                if (Bind.Down("up")) m.Lift += dt;
                SetPrompt("He's rolling at the surface! Hold <up> to lift the line clear", null, true);
                if ((m.EvT -= dt) <= 0)
                {
                    if (m.Lift >= LiftNeed) { r.Progress += 0.05f; Floater("Line clear!", player.X, player.Y - 26, "#dfe9ff"); Sfx.Play("coin"); if (r.Progress >= 1) { LandCatch(); return true; } }
                    else { MirePenalty(r, 0.04f); Floater("He rolled on the line.", player.X, player.Y - 26, "#ff9a8a"); }
                    EndTarponEvent();
                }
                return true;
            case "pilingsWarn":
                SetPrompt("He's turning for the pier! Get ready to mash <act>", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "pilings"; m.EvT = PilingsLen; }
                return true;
            case "pilings":
                if (tap) { m.Cranks++; Sfx.Reel(); }
                SetPrompt($"Crank him clear of the pilings! Mash <act> ({Math.Min(m.Cranks, (int)CranksNeed)}/{CranksNeed})", null, true);
                if (m.Cranks >= CranksNeed)
                {
                    r.Progress += 0.05f;
                    Floater("Cranked clear!", player.X, player.Y - 26, "#dfe9ff");
                    Sfx.Play("coin");
                    m.TargetX = m.X - (m.PileX - m.X) * 2; m.TargetY = m.Y;
                    if (r.Progress >= 1) { LandCatch(); return true; }
                    EndTarponEvent();
                }
                else if ((m.EvT -= dt) <= 0)
                {
                    MirePenalty(r, 0.05f);
                    Floater("The line wraps a piling!", player.X, player.Y - 26, "#ff9a8a");
                    Sfx.Play("splash");
                    EndTarponEvent();
                }
                return true;
            case "shakeWarn":
                SetPrompt("He rolls his shoulders... he's going to shake. Get ready to hold <act>", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "shake"; m.EvT = ShakeLen; Sfx.Play("splash"); quake = 0.2f; }
                return true;
            case "shake":
                // Steady means without letting go: a let-go starts it again (Codex).
                if (hold) m.Steady += dt; else { if (m.Steady > 0) m.Broke = true; m.Steady = 0; }
                SetPrompt(hold ? "Steady... keep holding <act>!" : "He's shaking his head! Hold <act> steady!", null, true);
                if ((m.EvT -= dt) <= 0)
                {
                    if (m.Steady >= SteadyNeed) { r.Progress += 0.06f; Floater("You rode out the shake!", player.X, player.Y - 26, "#dfe9ff"); Sfx.Play("coin"); if (r.Progress >= 1) { LandCatch(); return true; } }
                    else { MirePenalty(r, 0.05f); Floater("He shook out some line.", player.X, player.Y - 26, "#ff9a8a"); }
                    EndTarponEvent();
                }
                return true;
        }
        return false;
    }

    bool PilingNear(TarponFight m) { var p = NearestPiling(m.X, m.Y); return p.x != m.X || p.y != m.Y; }

    void EndTarponEvent()
    {
        var m = tarpon;
        m.Ev = "";
        m.NextEv = m.Phase == 1 ? Rand(6.5f, 9f) : Rand(5f, 7.5f);
        m.WanderT = 0;
        if (reel != null) { reel.LeapT = Rand(2f, 3.2f); reel.FishTimer = 0.4f; }
    }

    void UpdateTarpon(float dt)
    {
        if (!TarponOnLine && mode != "pause") tarpon = null;
        UpdateTarponHome(dt);
    }

    /* ---------- Letting him go ---------- */
    (float x, float y, int dir, float t)? tarponHome;
    float tarponLastX, tarponLastY;

    void LetTarponGo()
    {
        int dir = tarponLastX >= player.X ? 1 : -1;
        tarponHome = (tarponLastX, tarponLastY, dir, 0);
        Sfx.Play("release");
        Floater("Let go", player.X, player.Y - 30, "#dfe9ff");
        Toast("You ease the hook free beside the pier. The king rolls for a breath, then swims beyond the light.", 5);
    }

    void UpdateTarponHome(float dt)
    {
        if (tarponHome is not var (x, y, d, t) || mode is "pause") return;
        t += dt;
        float nx = x + d * 22 * dt;
        if (!Swimmable(TileUnder(nx, y))) nx = x;
        tarponHome = t > 3.5f || scene != "world" ? null : (nx, y, d, t);
    }

    // Landed: the record and card are yours, the king isn't (straight after AddCatch, which set lastM for this one).
    void FreeTarpon()
    {
        if (tarpon != null) { tarponLastX = tarpon.X; tarponLastY = tarpon.Y; }
        Take(TarponFish);
        if (lastM >= 1.25f && state.big.GetValueOrDefault(TarponFish) > 0) state.big[TarponFish]--;
    }

    /* ---------- The picture ---------- */
    static (Color[] col, byte[] h, byte[] glint)[] tarponFrames;
    static readonly Color[] TarponGlint = { Pal.C("#c7d6e6"), Pal.C("#f2f7ff"), Pal.C("#ffffff") };

    static (Color[] col, byte[] h, byte[] glint) TarponFrame(int i)
    {
        if (tarponFrames == null)
        {
            var pal = TarponArt.Palette.Select(Pal.C).ToArray();
            int n = TarponArt.Width * TarponArt.Height;
            tarponFrames = new (Color[], byte[], byte[])[TarponArt.Data.Length];
            for (int f = 0; f < tarponFrames.Length; f++)
            {
                using var src = new MemoryStream(Convert.FromBase64String(TarponArt.Data[f]));
                using var z = new System.IO.Compression.ZLibStream(src, System.IO.Compression.CompressionMode.Decompress);
                var raw = new byte[n * 2];
                int got = 0;
                while (got < raw.Length) { int k = z.Read(raw, got, raw.Length - got); if (k <= 0) break; got += k; }
                var col = new Color[n];
                var gl = new byte[n];
                for (int p = 0; p < n; p++)
                {
                    byte v = raw[p];
                    if (v == 255) continue;
                    if (v >= 250) { gl[p] = (byte)(v - 249); col[p] = TarponGlint[v - 250]; }
                    else col[p] = pal[v];
                }
                tarponFrames[f] = (col, raw[n..], gl);
            }
        }
        return tarponFrames[i];
    }

    static int TarponFrameIndex(float up, int phase, bool open, int roll)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < TarponArt.Up.Length; i++)
        {
            if (TarponArt.Open[i] != open || (TarponArt.Roll[i] != 0) != (roll != 0)) continue;
            float d = MathF.Abs(TarponArt.Up[i] - up) + MathF.Abs(TarponArt.Roll[i] - roll) * 0.5f + (TarponArt.Phase[i] == phase % TarponArt.Phases ? 0 : 0.5f);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // How bright the lighthouse beam is on a point right now (0 to 1): the same sweep LightParola draws.
    float BeamOn(float x, float y)
    {
        if (state.parola < 3 || !Night) return 0;
        float lx = LighthouseX, ly = LighthouseY - LampRise, a = time * 0.7f;
        float ex = (x - lx) / 22, ey = (y - ly) / 13, r = MathF.Sqrt(ex * ex + ey * ey);
        if (r < 1 || r > 9.5f) return 0;
        float off = MathF.Abs(MathF.IEEERemainder(MathF.Atan2(ey, ex) - a, MathF.Tau));
        return Math.Clamp(1 - off / 0.22f, 0, 1);
    }

    // Draws him with his middle `sink` rows under the water at (x, y) (negative: in the air): what's above the water as
    // it is, the rest a dim shape (unless dropUnder). Scales flash as the beam crosses him (and glint now and then in the
    // moonlight); glintOnly redraws just those, bright, after the dark.
    void DrawTarponFish(float x, float y, int dir, float up, int phase, bool open, int roll, float sink, bool dropUnder, float t, bool glintOnly = false, float k = 1)
    {
        if (OldArt)
        {
            if (glintOnly || sink > 8) return;
            FishArt.Draw(pix, TarponFish, (int)x - 18, (int)(y + MathF.Min(0, sink)) - 7, 36, 14, dir < 0);
            return;
        }
        var (col, h, gl) = TarponFrame(TarponFrameIndex(up, phase, open, roll));
        int w = TarponArt.Width, hh = TarponArt.Height, ax = TarponArt.AnchorX, ay = TarponArt.AnchorY;
        int ox = (int)MathF.Round(x), oy = (int)MathF.Round(y + sink);
        float beam = BeamOn(x, y);
        var deep = Pal.C("#173a5c");
        for (int j = 0; j < hh; j++)
            for (int i = 0; i < w; i++)
            {
                int p = j * w + i;
                var c = col[p];
                if (c.A == 0) continue;
                int px = ox + (dir < 0 ? ax - i : i - ax), py = oy + j - ay;
                bool above = h[p] - TarponArt.Lift > sink;
                if (gl[p] > 0)
                {
                    // In the beam every glint scale blazes; otherwise one now and then catches the moon.
                    float moon = MathF.Max(0, MathF.Sin(t * 2.3f + (float)Pix.Hash(i, j, 41) * 30) - 0.7f) / 0.3f;
                    float lit = MathF.Max(beam, moon * 0.7f);
                    if (!above && (dropUnder || !InSeaPx(px, py))) continue;
                    if (glintOnly) { if (lit > 0.05f) pix.Rect(px, py, 1, 1, Pal.WithAlpha(c, lit * k)); continue; }
                    c = Pix.Mix(Pal.C("#b8c6d2"), c, lit);
                }
                else if (glintOnly) continue;
                if (above) { pix.Rect(px, py, 1, 1, c); continue; }
                if (dropUnder || !InSeaPx(px, py)) continue;
                var u = Pix.Mix(c, deep, 0.45f);
                u.A = 190;
                pix.Rect(px, py, 1, 1, u);
            }
    }

    int TarponTail(float t, float speed = 6) => (int)(t * speed) % 3;

    // Under the water: his shape gliding by the pier, the ripple as his nose rises, the turn for the pilings.
    void DrawTarponUnder(float t)
    {
        if (!TarponShown || reel == null) return;
        var m = tarpon;
        int dir = m.Vx < 0 ? -1 : 1;
        if (reel.Leap > 0 || m.Ev is "breath" or "shake") return;   // above the water: DrawTarponOver
        var (sink, tail) = TarponUnderPose(m, t);
        DrawTarponFish(m.X, m.Y, dir, 0, tail, false, 0, sink, false, t);
        if (m.Ev == "breathWarn")
        {
            float k = 1 - m.EvT / TarponWarnLen;
            pix.Ring(m.X + dir * 10, m.Y, 3 + k * 8, Pal.Rgba(225, 238, 250, 0.35f + 0.5f * k));
        }
        if (m.Ev == "shakeWarn")
        {
            // Two small splashes off his shoulders before he shakes.
            for (int i = 0; i < 2; i++)
            {
                float ph = (t * 3 + i * 0.5f) % 1;
                pix.Ring(m.X + (i == 0 ? -6 : 6), m.Y, 1 + ph * 5, Pal.Rgba(230, 240, 250, 0.7f * (1 - ph)));
            }
        }
        if (m.Ev is "pilingsWarn" or "pilings")
        {
            // The piling he's heading for flashes, and a V of water behind him.
            bool on = (int)(t * 8) % 2 == 0;
            pix.Rect(m.PileX - 1, m.PileY - 6, 3, 10, on ? Pal.Rgba(255, 170, 120, 0.85f) : Pal.Rgba(140, 90, 60, 0.6f));
            for (int k = 2; k < 14; k++)
            {
                float al = 0.6f * (1 - k / 14f), ox = m.X - dir * k * 1.4f;
                pix.Rect(ox, m.Y - k * 0.4f, 1, 1, Pal.Rgba(225, 238, 250, al));
                pix.Rect(ox, m.Y + k * 0.4f, 1, 1, Pal.Rgba(225, 238, 250, al));
            }
        }
    }

    // Above the water: his leaps (the jumper's, arcing across), the roll for air, and the head-shake.
    void DrawTarponOver(float t)
    {
        if (!TarponShown || reel == null) return;
        var m = tarpon;
        int dir = m.Vx < 0 ? -1 : 1;
        if (reel.Leap > 0)
        {
            float k = Math.Clamp(1 - reel.Leap / reel.LeapLen, 0, 1);
            float jx = m.X + (k - 0.5f) * 30 * dir, jy = m.Y - MathF.Sin(k * MathF.PI) * 26;
            float up = 55 * (1 - 2 * k);
            // A shake at the top of the leap: rolled one way then the other.
            int roll = k > 0.35f && k < 0.65f ? ((int)(t * 10) % 2 == 0 ? 40 : -40) : 0;
            pix.Rect(jx - 10, m.Y + 1, 20, 2, Pal.Rgba(8, 20, 40, 0.25f));
            if (k < 0.2f || k > 0.8f)
            {
                float ex = m.X + (k < 0.5f ? -15 : 15) * dir, q = k < 0.5f ? k / 0.2f : (k - 0.8f) / 0.2f;
                pix.Ring(ex, m.Y + 1, 2 + q * 8, Pal.Rgba(230, 240, 250, 0.85f * (1 - q * 0.6f)));
            }
            DrawTarponFish(jx, m.Y, dir, roll != 0 ? 40 : up, TarponTail(t, 12), false, roll, jy - m.Y, true, t);
        }
        else if (m.Ev == "breath")
        {
            float k = 1 - m.EvT / BreathLen;
            // Up, mouth open, then rolling over and down.
            bool rolling = k > 0.55f;
            float sink = rolling ? 2 + (k - 0.55f) * 14 : 8 - 7 * MathF.Min(1, k * 3);
            DrawTarponFish(m.X, m.Y, dir, rolling ? 0 : 25, 0, !rolling, rolling ? 50 : 0, sink, false, t);
            pix.Ring(m.X + dir * 6, m.Y + 1, 4 + k * 8, Pal.Rgba(230, 240, 250, 0.7f * (1 - k)));
            if (k < 0.5f) for (int i = 0; i < 5; i++) { float ph = (t * 3 + i * 0.2f) % 1; pix.Rect(m.X + dir * (8 + i), m.Y - 4 - ph * 4, 1, 1, Pal.Rgba(230, 240, 250, 0.8f * (1 - ph))); }
        }
        else if (m.Ev == "shake")
        {
            // Head and shoulders out, shaking side to side, white water flying.
            int roll = (int)(t * 12) % 2 == 0 ? 40 : -40;
            DrawTarponFish(m.X + MathF.Sin(t * 30) * 1.2f, m.Y, dir, 40, TarponTail(t, 14), false, roll, 3, false, t);
            for (int i = 0; i < 10; i++)
            {
                float ph = (t * 4 + i * 0.1f) % 1;
                pix.Rect(m.X + (i - 5) * 2.2f, m.Y - 2 - MathF.Abs(MathF.Sin(i * 1.9f)) * 7 * (1 - ph), 1, 2, Pal.Rgba(230, 240, 250, 0.9f * (1 - ph)));
            }
        }
    }

    void DrawTarponHome(float t)
    {
        if (tarponHome is not var (x, y, d, k)) return;
        DrawTarponFish(x, y, d, k < 0.6f ? 15 : 0, TarponTail(t), false, k < 0.6f ? 50 : 0, k < 0.6f ? 5 : 9 + k * 2, false, t);
    }

    void LightTarpon()
    {
        if (!TarponShown || tarpon is not { } m) return;
        LightHole(m.X, m.Y - 2, 30, 0.5f);
        if (reel?.Leap > 0) LightHole(m.X, m.Y - 22, 30, 0.6f);
    }

    void GlowTarpon(float t, float k)
    {
        if (!TarponShown || tarpon is not { } m || k <= 0 || OldArt || reel == null) return;
        int dir = m.Vx < 0 ? -1 : 1;
        if (reel.Leap > 0 || m.Ev is "breath" or "shake") return;   // in the air or at the surface: lit by LightTarpon already
        var (sink, tail) = TarponUnderPose(m, t);   // the same pose as the body, or the glints sit off it (Codex)
        DrawTarponFish(m.X, m.Y, dir, 0, tail, false, 0, sink, false, t, glintOnly: true, k: k);
    }

    float tarponCamK, tarponCamX, tarponCamY;
    (float x, float y) TarponCamera(float fx, float fy)
    {
        tarponCamK = Math.Clamp(tarponCamK + (TarponShown && scene == "world" ? 1 : -1) * Raylib.GetFrameTime() * 1.6f, 0, 1);
        if (tarponCamK <= 0) return (fx, fy);
        // Easing back out after he's landed or gone, from where it last framed him (Codex: it used to snap).
        if (tarpon != null) (tarponCamX, tarponCamY) = (tarpon.CX, tarpon.CY);
        float tx = (player.X + tarponCamX) / 2, ty = (player.Y - 8 + tarponCamY - 30) / 2;
        float k = tarponCamK * tarponCamK * (3 - 2 * tarponCamK);
        return (fx + (tx - fx) * k, fy + (ty - fy) * k);
    }

    // By the reel: what to do now, with a ring for the held moves and a count for the cranks.
    void DrawTarponCue(TarponFight m, float rx, Func<float, float> Y)
    {
        void Big(string s, Color c, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui700, 30), Y(at), FontKind.Ui700, 30, c);
        void Small(string s, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui600, 18), Y(at), FontKind.Ui600, 18, Pal.C("#ffffff"));
        bool blink = (int)(time * 8) % 2 == 0;
        float need = 0, have = 0;
        switch (m.Ev)
        {
            case "breathWarn": Big("Rising...", Pal.C("#dfe9ff"), 130); Small(Bind.Fix("Get ready: <up>"), 168); break;
            case "breath": Big("LIFT!", Pal.C("#f3c25b"), 130); Small(Bind.Fix("Hold <up>"), 168); need = LiftNeed; have = m.Lift; break;
            case "pilingsWarn": Big("PILINGS!", Pal.WithAlpha(Pal.C("#ff9a5a"), blink ? 1 : 0.7f), 130); Small(Bind.Fix("Get ready: mash <act>"), 168); break;
            case "pilings": Big("CRANK!", Pal.WithAlpha(Pal.C("#f3c25b"), blink ? 1 : 0.7f), 130); Small(Bind.Fix($"Mash <act>  {Math.Min(m.Cranks, (int)CranksNeed)}/{CranksNeed}"), 168); need = CranksNeed; have = m.Cranks; break;
            case "shakeWarn": Big("Shoulders...", Pal.C("#dfe9ff"), 130); Small(Bind.Fix("Get ready: hold <act>"), 168); break;
            case "shake": Big("STEADY!", Pal.C("#f3c25b"), 130); Small(Bind.Fix("Hold <act>"), 168); need = SteadyNeed; have = m.Steady; break;
        }
        if (need > 0)
        {
            float cx = rx - 40, cy = Y(232), k = Math.Clamp(have / need, 0, 1);
            Gfx.Rect(cx - 30, cy - 30, 60, 60, Pal.C("rgba(10,20,48,0.85)"), 30);
            for (int i = 0; i < 40; i++)
            {
                if (i / 40f > k) break;
                float a = -MathF.PI / 2 + i / 40f * MathF.Tau;
                Gfx.Rect(cx + MathF.Cos(a) * 22 - 3, cy + MathF.Sin(a) * 22 - 3, 6, 6, k >= 1 ? Pal.C("#7fd36b") : Pal.C("#dfe9ff"), 3);
            }
        }
    }

    // How deep he glides, and the step of his tail, under the water: shared by the body and its glints.
    (float sink, int tail) TarponUnderPose(TarponFight m, float t) =>
        (m.Ev == "breathWarn" ? 9 - 4 * (1 - m.EvT / TarponWarnLen) : m.Ev == "shakeWarn" ? 6 : 9, TarponTail(t, m.Ev == "pilings" ? 12 : 6));

    // The nearest place he fits, out from (x, y) (or his middle, if there's none close).
    (float x, float y) NearestTarponWater(float x, float y)
    {
        for (int r = 3; r < 90; r += 3)
            for (int a = 0; a < 24; a++)
            {
                float px = x + MathF.Cos(a * MathF.Tau / 24) * r, py = y + MathF.Sin(a * MathF.Tau / 24) * r;
                if (TarponWater(px, py)) return (px, py);
            }
        return (tarpon.CX, tarpon.CY);
    }
}
