using Raylib_cs;

namespace Fesh;

// 1.38: the Sunscale lungfish, the oasis's legend, as a boss fight: a gold lungfish as long as a rowing boat in the
// Sunscald oasis (a clear day, a cricket). Real lungfish (the game's is based on the West African Protopterus annectens)
// have gills and lungs and must come up to breathe air, "walk" along the bottom on their pelvic fins, and when their
// pool dries they burrow into the mud and sleep inside a dried cocoon for months. The fight is the ordinary runner reel
// (let go when it runs) plus one event at a time, each a little rhythm of its own, unlocked by how far you've reeled it in:
//   1 the oasis remembers: it comes up to breathe in three ripples: tap <act> with each beat;
//   2 footsteps under water: it walks the bottom: alternate <left> and <right> in step with its fins;
//   3 the patient survivor: it settles into an old mud hollow: three slow pumps, hold <act> to pull and let go to rest.
// Nothing hurts you, and a miss costs a little reel, never the line. The oasis is a small pool, so the fish never leaves
// it, and while the fight goes on a cracked mud apron creeps out round the water (looks only: the water never shrinks).
// Its sprite is a Blender model (tools/blender/lungfish.py renders LungfishArt.cs). FESH_OLDART=1 draws the Fish log's
// hand-drawn picture instead.
partial class Game
{
    const string LungfishFish = "sunscale_lungfish";

    sealed class LungFight
    {
        public float Banner = 3.2f, T, X, Y, Vx = 1, TargetX, TargetY, WanderT;
        public string Ev = "";          // "", "breathWarn", "breath", "marchWarn", "march", "cradleWarn", "cradle"
        public float EvT, NextEv = 5f, LastStep = -9;
        public int Phase = 1, Events, Step, Beats;
        public bool Fresh;
        public bool[] BeatHit = new bool[3], CycleOk = new bool[3];
        public float[] Pull = new float[3], Rest = new float[3];
        public List<(float x, float y, float t0)> Prints = new();
        public string Title = "SUNSCALE LUNGFISH", Sub = "The oasis remembers";
    }
    LungFight lung;

    bool LungfishOnLine => mode == "reeling" && reel?.Roll?.Id == LungfishFish && fish != null;
    // On the line, or paused in the middle of the fight: it stays drawn and framed behind the pause menu.
    bool LungfishShown => (mode == "reeling" || mode == "pause" && pausedFrom == "reeling") && reel?.Roll?.Id == LungfishFish && fish != null && lung != null;
    static int LungPhaseOf(float p) => p < 0.5f ? 1 : p < 0.78f ? 2 : 3;
    const float LungBreathWarn = 1.2f, LungBreathLen = 2.4f, LungMarchWarn = 1.3f, LungMarchLen = 3.0f, LungCradleWarn = 1.4f, LungCradleLen = 3.0f;
    static readonly float[] LungBeats = { 0.6f, 1.2f, 1.8f };
    const float LungBeatSlack = 0.18f, LungPullNeed = 0.45f, LungRestMax = 0.10f;
    static readonly int[] LungMarch = { 3, 1, 3, 1, 3, 1 };   // left, right, left, right, left, right (0 up, 1 right, 2 down, 3 left)

    /* ---------- The pool ---------- */
    HashSet<(int, int)> lungPool;
    List<(float x, float y)> lungSpots;

    // The oasis spot's own water: its connected oasis tiles.
    HashSet<(int, int)> LungPool()
    {
        if (lungPool != null) return lungPool;
        var s = Data.SpotById["oasis"];
        var seen = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        int sx = (int)(s.X / T), sy = (int)(s.Y / T);
        for (int r = 0; r < 8 && q.Count == 0; r++)
            for (int dy = -r; dy <= r && q.Count == 0; dy++)
                for (int dx = -r; dx <= r && q.Count == 0; dx++)
                {
                    int x = sx + dx, y = sy + dy;
                    if (x >= 0 && y >= 0 && x < COLS && y < ROWS && worldMap[y, x] == 'o') { q.Enqueue((x, y)); seen.Add((x, y)); }
                }
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS || worldMap[ny, nx] != 'o' || !seen.Add((nx, ny))) continue;
                q.Enqueue((nx, ny));
            }
        }
        return lungPool = seen;
    }

    bool InLungPool(float x, float y) => LungPool().Contains(((int)MathF.Floor(x / T), (int)MathF.Floor(y / T)));

    // It's about 28 px long and 9 tall: the whole body (every few pixels along it, and a little above and below) must be
    // over the pool's water, so it never overlaps the bank.
    bool LungFits(float x, float y)
    {
        for (int dx = -12; dx <= 12; dx += 4)
            if (!InLungPool(x + dx, y)) return false;
        return InLungPool(x, y - 3.5f) && InLungPool(x, y + 3.5f) && InLungPool(x - 8, y + 3.5f) && InLungPool(x + 8, y - 3.5f);
    }

    // Every spot it can swim to (a grid of 5 px over the pool), found once.
    List<(float x, float y)> LungSpots()
    {
        if (lungSpots != null) return lungSpots;
        lungSpots = new();
        var pool = LungPool();
        if (pool.Count == 0) return lungSpots;
        int x0 = pool.Min(p => p.Item1) * T, x1 = (pool.Max(p => p.Item1) + 1) * T, y0 = pool.Min(p => p.Item2) * T, y1 = (pool.Max(p => p.Item2) + 1) * T;
        for (float y = y0; y <= y1; y += 5)
            for (float x = x0; x <= x1; x += 5)
                if (LungFits(x, y)) lungSpots.Add((x, y));
        return lungSpots;
    }

    // The nearest place it fits, out from (x, y); the pool's middle if nothing fits at all (a tiny pool).
    (float x, float y) NearestLungSpot(float x, float y)
    {
        var spots = LungSpots();
        if (spots.Count > 0) { var b = spots.OrderBy(p => Dist(p.x, p.y, x, y)).First(); return b; }
        var pool = LungPool();
        if (pool.Count == 0) return (x, y);
        return ((float)pool.Average(p => p.Item1) * T + 5, (float)pool.Average(p => p.Item2) * T + 5);
    }

    /* ---------- The fight ---------- */
    void StartLungfish()
    {
        var (sx, sy) = NearestLungSpot(fish.Bx, fish.By);
        lung = new LungFight { X = sx, Y = sy, TargetX = sx, TargetY = sy };
        fish.Bx = fish.Tx = sx; fish.By = fish.Ty = sy;
        reel.Progress = reel.Perfect ? 0.38f : 0.3f;
        reel.Pull = 0.75f;
        quake = 0.3f;
        Sfx.Play("splash");
    }

    // It drifts slowly about the pool (faster when it runs), only ever to places it fits.
    void MoveLung(float dt)
    {
        var m = lung;
        float speed = m.Ev switch { "" => reel.Running > 0 ? 18 : 7, "breathWarn" or "marchWarn" or "cradleWarn" => 3, _ => 0 };
        if (speed > 0)
        {
            m.WanderT -= dt;
            if (m.WanderT <= 0 || Dist(m.X, m.Y, m.TargetX, m.TargetY) < 2)
            {
                m.WanderT = Rand(2.5f, 4.5f);
                var spots = LungSpots();
                if (spots.Count > 0) { var s = spots[rng.Next(spots.Count)]; m.TargetX = s.x; m.TargetY = s.y; }
            }
            float dx = m.TargetX - m.X, dy = m.TargetY - m.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            if (d > 0.5f)
            {
                float step = MathF.Min(d, speed * dt), nx = m.X + dx / d * step, ny = m.Y + dy / d * step;
                if (LungFits(nx, ny)) { m.X = nx; m.Y = ny; }
                else m.WanderT = 0;
                if (MathF.Abs(dx) > 1f) m.Vx = MathF.Sign(dx);
            }
        }
        fish.Bx = fish.Tx = m.X; fish.By = fish.Ty = m.Y;
    }

    // Called from UpdateReel before the ordinary reel: true when an event has the reel (the bar waits meanwhile).
    bool UpdateLungfishReel(ReelState r, float dt, bool hold, bool tap)
    {
        lung ??= new LungFight { X = fish.Bx, Y = fish.By, TargetX = fish.Bx, TargetY = fish.By };
        var m = lung;
        m.T += dt;
        m.Banner = Math.Max(0, m.Banner - dt);
        int phase = Math.Max(m.Phase, LungPhaseOf(r.Progress));
        if (phase > m.Phase)
        {
            m.Phase = phase;
            m.Events = 0;
            m.Fresh = true;
            m.Banner = 3.2f;
            m.Title = phase == 2 ? "FOOTSTEPS UNDER WATER" : "THE PATIENT SURVIVOR";
            m.Sub = phase == 2 ? "Alternate <left> and <right>" : "Three slow pumps: pull, rest";
            m.NextEv = MathF.Max(m.NextEv, 3.4f);   // nothing new under the title card
            Sfx.Play("splash");
        }
        MoveLung(dt);
        if (m.Ev == "")
        {
            // Never in the middle of one of its runs, nor under the title card.
            if (r.Running > 0) return false;
            m.NextEv -= dt;
            if (m.NextEv > 0 || m.Banner > 0) return false;
            m.Events++;
            // Phase 1 only breathes; phase 2 walks and breathes; phase 3 pumps, walks and breathes in turn (the newest first).
            BeginLungEvent(LungPick(m.Phase, m.Events, m.Fresh));
            m.Fresh = false;
        }
        switch (m.Ev)
        {
            case "breathWarn":
                SetPrompt("It's coming up for air. Three ripples: tap [<act>] with each beat!", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "breath"; m.EvT = LungBreathLen; }
                return true;   // a press now is only a warning press: it doesn't count
            case "breath":
            {
                m.EvT -= dt;
                float a = LungBreathLen - m.EvT;
                SetPrompt("Tap [<act>] with each ripple!", null, true);
                if (tap)
                {
                    int hit = -1;
                    for (int i = 0; i < 3; i++) if (!m.BeatHit[i] && MathF.Abs(a - LungBeats[i]) <= LungBeatSlack) { hit = i; break; }
                    if (hit < 0) return LungResolve(r, false, 0, 0.04f, "", "Off the beat!");   // an early, late or extra press
                    m.BeatHit[hit] = true; m.Beats++;
                    Floater(hit == 2 ? "Three ripples!" : "Ripple!", player.X, player.Y - 26, "#f3c25b");
                    Sfx.Play("coin");
                }
                if (a > LungBeats[2] + LungBeatSlack)
                    return m.Beats >= 3 ? LungResolve(r, true, 0.06f, 0, "It rose to your rhythm!", "") : LungResolve(r, false, 0, 0.04f, "", "You missed a ripple.");
                return true;
            }
            case "marchWarn":
                SetPrompt("It's walking the bottom on its fins. Walk the rod: [<left>], [<right>], again!", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "march"; m.EvT = LungMarchLen; m.Step = 0; m.LastStep = -9; }
                return true;
            case "march":
            {
                m.EvT -= dt;
                float a = LungMarchLen - m.EvT;
                SetPrompt("Alternate [<left>] and [<right>]!", null, true);
                if (m.EvT <= 0) return LungResolve(r, false, 0, 0.06f, "", "It's out of step: it slips away.");   // a stroke after the deadline is too late
                bool l = Bind.Pressed("left"), rt = Bind.Pressed("right");
                if (l && rt) return LungResolve(r, false, 0, 0.06f, "", "Both at once!");
                if (l || rt)
                {
                    int want = LungMarch[m.Step], got = l ? 3 : 1;
                    if (got != want) return LungResolve(r, false, 0, 0.06f, "", "Out of step!");
                    else
                    {
                        m.Step++; m.LastStep = a;
                        m.Prints.Add((m.X - m.Vx * (m.Step % 2 == 0 ? 6 : 12), m.Y + (m.Step % 2 == 0 ? 3 : -3), time));
                        if (m.Prints.Count > 12) m.Prints.RemoveAt(0);
                        // Each step carries it a little along the bottom, if the water allows.
                        if (LungFits(m.X + m.Vx * 3, m.Y)) m.X += m.Vx * 3; else m.Vx = -m.Vx;
                        Sfx.Play(m.Step % 2 == 0 ? "hoof" : "hoof2");
                        if (m.Step >= LungMarch.Length) return LungResolve(r, true, 0.08f, 0, "You walked it in!", "");
                    }
                }
                return true;
            }
            case "cradleWarn":
                SetPrompt("It's curling into an old mud hollow. Three slow pumps: hold [<act>] to pull, let go to rest.", null, true);
                if ((m.EvT -= dt) <= 0)
                {
                    m.Ev = "cradle"; m.EvT = LungCradleLen;
                    Array.Clear(m.Pull); Array.Clear(m.Rest); Array.Clear(m.CycleOk);
                }
                return true;
            case "cradle":
            {
                // The time held is split exactly across the pull and rest parts (and the cycles) this frame covered.
                float a0 = LungCradleLen - m.EvT;
                m.EvT -= dt;
                float a = MathF.Min(LungCradleLen, LungCradleLen - m.EvT);
                int cyc = Math.Clamp((int)a, 0, 2);
                bool pulling = a - cyc < 0.6f;
                if (hold)
                    for (float s = a0; s < a - 1e-5f;)
                    {
                        int c = Math.Min(2, (int)s);
                        bool pull = s - c < 0.6f - 1e-5f;
                        float e = MathF.Min(a, pull ? c + 0.6f : c + 1f);
                        (pull ? m.Pull : m.Rest)[c] += e - s;
                        s = e;
                    }
                for (int i = 0; i < 3 && i < (int)a; i++) m.CycleOk[i] = m.Pull[i] >= LungPullNeed && m.Rest[i] <= LungRestMax;   // the cycles already over, for the cue
                SetPrompt(pulling ? "PULL: hold [<act>]!" : "REST: let go!", null, true);
                if (m.EvT <= 0)
                {
                    int good = 0;
                    for (int i = 0; i < 3; i++) { m.CycleOk[i] = m.Pull[i] >= LungPullNeed && m.Rest[i] <= LungRestMax; if (m.CycleOk[i]) good++; }
                    return good == 3 ? LungResolve(r, true, 0.10f, 0, "You eased it out of the mud!", "") : LungResolve(r, false, 0, 0.07f, "", "It slipped back into the mud.");
                }
                return true;
            }
        }
        return false;
    }

    // Which event comes next: phase 1 only breathes; phase 2 walks and breathes in turn; phase 3 pumps, walks and breathes in turn.
    // `events` counts this one (the first of a new phase is 1, and a new phase's own event always comes first).
    static string LungPick(int phase, int events, bool fresh) =>
        fresh ? (phase == 2 ? "march" : phase == 3 ? "cradle" : "breath")
        : phase == 1 ? "breath"
        : phase == 2 ? (events % 2 == 1 ? "march" : "breath")
        : (events % 3) switch { 1 => "cradle", 2 => "march", _ => "breath" };

    // An event is over: a win pays reel, a miss costs some (never below 0.05, never raising a fish that was lower).
    internal string lungLast = "";   // how the last event ended ("ok" or "miss"), and what it did to the reel: for the tests
    internal float lungDelta;

    bool LungResolve(ReelState r, bool ok, float gain, float loss, string good, string bad)
    {
        float before = r.Progress;
        if (ok)
        {
            r.Progress += gain;
            Floater(good, player.X, player.Y - 26, "#7fd36b");
            Sfx.Play("coin");
            if (r.Progress >= 1) { lungDelta = r.Progress - before; lungLast = "ok"; LandCatch(); return true; }
        }
        else
        {
            MirePenalty(r, loss);
            Floater(bad, player.X, player.Y - 26, "#ff9a8a");
            Sfx.Play("nope");
        }
        lungDelta = r.Progress - before; lungLast = ok ? "ok" : "miss";
        Burst(lung.X, lung.Y, "#e8d9a8", ok ? 6 : 4);
        EndLungEvent();
        return true;
    }

    void BeginLungEvent(string ev)
    {
        var m = lung;
        var r = reel;
        m.Ev = ev + "Warn";
        m.EvT = ev switch { "breath" => LungBreathWarn, "march" => LungMarchWarn, _ => LungCradleWarn };
        m.Beats = 0; m.Step = 0; m.LastStep = -9;
        Array.Clear(m.BeatHit);
        r.Running = 0; r.Tension = Math.Min(r.Tension, 0.3f); r.HoldT = 0; r.Digging = false; r.ZoneV = 0;
        Sfx.Play("splash");
    }

    void EndLungEvent()
    {
        var m = lung;
        m.Ev = "";
        m.NextEv = m.Phase == 1 ? Rand(6.5f, 9f) : Rand(5f, 7f);
        m.WanderT = 0;
        if (reel != null) { reel.RunT = Rand(1.6f, 3f); reel.Tension = 0; reel.HoldT = 0; reel.Digging = false; reel.FishTimer = 0.4f; }
    }

    // Every frame (pause or not): forget the fight once the fish is landed or lost.
    void UpdateLungfish(float dt)
    {
        if (!LungfishOnLine && mode != "pause") lung = null;
    }

    /* ---------- The picture ---------- */
    static (Color[] col, byte[] h)[] lungFrames;

    static (Color[] col, byte[] h) LungFrame(int i)
    {
        if (lungFrames == null)
        {
            var pal = LungfishArt.Palette.Select(Pal.C).ToArray();
            int n = LungfishArt.Width * LungfishArt.Height;
            lungFrames = new (Color[], byte[])[LungfishArt.Data.Length];
            for (int f = 0; f < lungFrames.Length; f++)
            {
                using var src = new MemoryStream(Convert.FromBase64String(LungfishArt.Data[f]));
                using var z = new System.IO.Compression.ZLibStream(src, System.IO.Compression.CompressionMode.Decompress);
                var raw = new byte[n * 2];
                int got = 0;
                while (got < raw.Length) { int k = z.Read(raw, got, raw.Length - got); if (k <= 0) break; got += k; }
                var col = new Color[n];
                for (int p = 0; p < n; p++) col[p] = raw[p] == 255 ? default : pal[raw[p]];
                lungFrames[f] = (col, raw[n..]);
            }
        }
        return lungFrames[i];
    }

    // The frame nearest a nose angle (degrees up), step of the body's wave and mouth.
    static int LungFrameIndex(float up, int phase, bool open)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < LungfishArt.Up.Length; i++)
        {
            if (LungfishArt.Open[i] != open) continue;
            float d = MathF.Abs(LungfishArt.Up[i] - up) + (open ? 0 : (LungfishArt.Phase[i] == ((phase % LungfishArt.Phases) + LungfishArt.Phases) % LungfishArt.Phases ? 0 : 0.5f));
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // Draws the lungfish with its middle at (x, y) on the water, facing dir, its middle `sink` rows under the surface:
    // pixels above the water are drawn as they are, the rest (unless dropUnder) as a dim shape under the clear water.
    internal void DrawLungFish(float x, float y, int dir, float up, int phase, bool open, float sink, bool dropUnder = false)
    {
        if (OldArt)
        {
            if (sink > 8) return;
            y += MathF.Min(0, sink);
            FishArt.Draw(pix, LungfishFish, (int)x - 20, (int)y - 5, 40, 10, dir < 0);
            return;
        }
        var (col, h) = LungFrame(LungFrameIndex(up, phase, open));
        int w = LungfishArt.Width, hh = LungfishArt.Height, ax = LungfishArt.AnchorX, ay = LungfishArt.AnchorY;
        int ox = (int)MathF.Round(x), oy = (int)MathF.Round(y + sink);
        var water = Pal.C("#1d7f86");
        for (int j = 0; j < hh; j++)
            for (int i = 0; i < w; i++)
            {
                int p = j * w + i;
                var c = col[p];
                if (c.A == 0) continue;
                bool above = h[p] - LungfishArt.Lift > sink;
                int px = ox + (dir < 0 ? ax - i : i - ax), py = oy + j - ay;
                if (above) { pix.Rect(px, py, 1, 1, c); continue; }
                if (dropUnder) continue;
                if (!InLungPool(px, py + 1.5f)) continue;
                var u = Pix.Mix(c, water, 0.38f);
                u.A = 215;
                pix.Rect(px, py, 1, 1, u);
            }
    }

    // How far its head is out of the water for the breath's ripples (0 to 1): pops at each beat, and twice in the warning
    // to show the tempo.
    float LungPop(LungFight m)
    {
        float Bump(float d) => d < 0.14f ? 1 - d / 0.14f : 0;
        if (m.Ev == "breathWarn")
        {
            float w = LungBreathWarn - m.EvT;
            return MathF.Max(Bump(MathF.Abs(w - 0.3f)), Bump(MathF.Abs(w - 0.9f)));
        }
        if (m.Ev == "breath")
        {
            float a = LungBreathLen - m.EvT;
            return LungBeats.Max(b => Bump(MathF.Abs(a - b)));
        }
        return 0;
    }

    // Under the surface: the shape gliding in the clear water, the silt it stirs up, its footprints in the sand and the
    // old mud hollow it curls in. Drawn with the water's things, before the bank.
    void DrawLungUnder(float t)
    {
        if (!LungfishShown) return;
        var m = lung;
        int dir = m.Vx < 0 ? -1 : 1;
        // Its footprints along the bottom: pale dents that fade.
        foreach (var (px, py, t0) in m.Prints)
        {
            float age = time - t0;
            if (age < 0 || age > 2.2f || !InLungPool(px, py)) continue;
            pix.Rect(px, py, 2, 1, Pal.Rgba(228, 214, 168, 0.55f * (1 - age / 2.2f)));
            pix.Ring(px + 1, py, 1 + age * 2.5f, Pal.Rgba(228, 240, 232, 0.4f * (1 - age / 2.2f)));
        }
        if (m.Ev is "cradleWarn" or "cradle")
        {
            // The hollow: a dark oval of mud on the bottom under it, loosening flakes on each pull.
            float k = m.Ev == "cradle" ? 1 : 1 - m.EvT / LungCradleWarn;
            for (int yy = -5; yy <= 5; yy++)
                for (int xx = -16; xx <= 16; xx++)
                {
                    float d = xx * xx / 256f + yy * yy / 25f;
                    if (d > 1 || !InLungPool(m.X + xx, m.Y + yy + 1)) continue;
                    pix.Rect(m.X + xx, m.Y + yy + 2, 1, 1, Pal.Rgba(74, 52, 30, (d > 0.7f ? 0.35f : 0.55f) * k));
                }
            if (m.Ev == "cradle")
            {
                float a = LungCradleLen - m.EvT;
                int cyc = Math.Clamp((int)a, 0, 2);
                if (a - cyc < 0.6f)
                    for (int i = 0; i < 7; i++)
                    {
                        float f = (a * 1.8f + i * 0.21f) % 1;
                        pix.Rect(m.X - 12 + i * 4 + MathF.Sin(i * 2.3f) * 2, m.Y + 1 - f * 8, 1, 1, Pal.Rgba(122, 92, 56, 0.8f * (1 - f)));
                    }
            }
        }
        // A little silt behind it while it swims.
        if (m.Ev is "" or "breathWarn" or "marchWarn")
            for (int i = 0; i < 4; i++)
            {
                float age = (t * 0.5f + i / 4f) % 1, px = m.X - dir * (12 + age * 14) + MathF.Sin(i * 2.3f + t) * 2, py = m.Y + MathF.Cos(i * 1.7f) * 2.5f;
                if (InLungPool(px, py)) pix.Rect(px, py, 2, 1, Pal.Rgba(214, 196, 142, 0.22f * (1 - age)));
            }
        float pop = LungPop(m);
        if (pop > 0.02f) return;   // its head is out: DrawLungOver draws it
        float sink = m.Ev switch { "breathWarn" => 7 - 3 * (1 - m.EvT / LungBreathWarn), "breath" => 4, "cradleWarn" or "cradle" => 8, _ => 6 };
        int ph = (int)(t * (m.Ev == "march" ? 5 : 3));
        DrawLungFish(m.X, m.Y, dir, 0, ph, false, sink);
        if (m.Ev == "breath")
        {
            // A ripple ring from where it breaks the surface, at each beat, growing and fading.
            float a = LungBreathLen - m.EvT;
            foreach (float b in LungBeats)
            {
                float age = a - b;
                if (age > 0 && age < 0.55f) pix.Ring(m.X + dir * 12, m.Y + 1, 2 + age * 22, Pal.Rgba(226, 244, 240, 0.8f * (1 - age / 0.55f)));
            }
        }
    }

    // Above the water: the head coming out to breathe, with the water running off it.
    void DrawLungOver(float t)
    {
        if (!LungfishShown) return;
        var m = lung;
        float pop = LungPop(m);
        if (pop <= 0.02f) return;
        int dir = m.Vx < 0 ? -1 : 1;
        DrawLungFish(m.X, m.Y, dir, 32, 0, pop > 0.3f, 6 - 10 * pop);
        pix.Ring(m.X + dir * 12, m.Y + 1, 3 + (1 - pop) * 8, Pal.Rgba(226, 244, 240, 0.7f * pop));
        for (int i = 0; i < 5; i++)
        {
            float a = (t * 2.5f + i * 0.2f) % 1;
            pix.Rect(m.X + dir * (9 + i * 1.5f), m.Y - 9 * pop + a * 9, 1, 1, Pal.Rgba(220, 238, 232, 0.8f * (1 - a)));
        }
    }

    /* ---------- The cue by the reel ---------- */
    static void LungRing(float cx, float cy, float r, Color c)
    {
        int n = Math.Max(18, (int)(r * 1.4f));
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * MathF.Tau;
            Gfx.Circle(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, 1.7f, c);
        }
    }

    // Next to the reel while an event has it: what to do now, big, and the pattern to follow.
    void DrawLungfishCue(LungFight m, float rx, Func<float, float> Y)
    {
        void Big(string s, Color c, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui700, 30), Y(at), FontKind.Ui700, 30, c);
        void Small(string s, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui600, 18), Y(at), FontKind.Ui600, 18, Pal.C("#ffffff"));
        bool blink = Calm || (int)(time * 8) % 2 == 0;
        float x0 = rx - 340, cy = Y(238);
        var gold = Pal.C("#f3c25b"); var green = Pal.C("#7fd36b"); var red = Pal.C("#ff9a8a"); var panel = Pal.C("rgba(34,22,8,0.86)");
        switch (m.Ev)
        {
            case "breathWarn": Big("Surfacing...", Pal.C("#e8f4ee"), 130); Small(Bind.Fix("Watch the ripples, then tap <act>"), 168); break;
            case "breath":
            {
                Big("TAP!", Pal.WithAlpha(gold, blink ? 1 : 0.7f), 130); Small(Bind.Fix($"<act> on each ripple  {m.Beats}/3"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                float a = LungBreathLen - m.EvT;
                for (int i = 0; i < 3; i++)
                {
                    float bx = x0 + 55 + i * 110, until = LungBeats[i] - a;
                    LungRing(bx, cy, 20, m.BeatHit[i] ? green : until < -LungBeatSlack ? red : Pal.C("#d8c48a"));
                    if (m.BeatHit[i]) Gfx.Circle(bx, cy, 12, green);
                    else if (until > -LungBeatSlack) LungRing(bx, cy, 20 + 28 * Math.Clamp(until / 0.6f, 0, 1), gold);
                }
                break;
            }
            case "marchWarn": Big("Footsteps...", Pal.C("#e8f4ee"), 130); Small(Bind.Fix("Get ready: <left> <right> <left> <right>"), 168); break;
            case "march":
            {
                Big("WALK!", Pal.WithAlpha(gold, blink ? 1 : 0.7f), 130); Small(Bind.Fix($"Alternate <left> and <right>  {m.Step}/{LungMarch.Length}"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                for (int i = 0; i < LungMarch.Length; i++)
                {
                    bool cur = i == m.Step, done = i < m.Step;
                    float bx = x0 + 28 + i * 54, s = cur ? 1f : 0.7f;
                    Gfx.Rect(bx - 20 * s, cy - 20 * s, 40 * s, 40 * s, Pal.C(done ? "rgba(40,90,40,0.9)" : cur ? "rgba(90,66,20,0.95)" : "rgba(60,48,30,0.8)"), 6);
                    DrawArrow(bx, cy, LungMarch[i], done ? green : cur ? gold : Pal.C("#8a7a5a"));
                }
                break;
            }
            case "cradleWarn": Big("The hollow...", Pal.C("#e8f4ee"), 130); Small(Bind.Fix("Get ready: pull <act>, rest, pull, rest"), 168); break;
            case "cradle":
            {
                float a = LungCradleLen - m.EvT;
                int cyc = Math.Clamp((int)a, 0, 2);
                bool pulling = a - cyc < 0.6f;
                Big(pulling ? "PULL!" : "REST", pulling ? Pal.WithAlpha(gold, blink ? 1 : 0.7f) : Pal.C("#e8f4ee"), 130); Small(Bind.Fix(pulling ? "Hold <act>" : "Let go of <act>"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                for (int i = 0; i < 3; i++)
                {
                    float sx = x0 + 8 + i * 106;
                    bool over = a >= i + 1, ok = m.CycleOk[i];
                    Color pc = over ? (ok ? green : red) : gold;
                    Gfx.Rect(sx, cy - 14, 62, 28, Pal.WithAlpha(pc, over ? 0.9f : 0.75f), 5);
                    Gfx.Rect(sx + 62, cy - 14, 38, 28, Pal.C(over ? (ok ? "rgba(40,90,40,0.9)" : "rgba(110,40,40,0.9)") : "rgba(70,56,36,0.9)"), 5);
                    Gfx.Text("PULL", sx + 31 - Gfx.Measure("PULL", FontKind.Ui700, 14) / 2, cy - 11, FontKind.Ui700, 14, Pal.C("#2a1a08"));
                    Gfx.Text("REST", sx + 81 - Gfx.Measure("REST", FontKind.Ui700, 12) / 2, cy - 9, FontKind.Ui700, 12, Pal.C("#e8d9a8"));
                }
                int cc = (int)MathF.Min(a, 2.999f);
                float cx = x0 + 8 + cc * 106 + (a - cc) * 100;
                Gfx.Rect(cx - 2, cy - 22, 4, 44, Pal.C("#ffffff"));
                break;
            }
        }
    }

    /* ---------- The oasis, drying ---------- */
    // A cracked mud apron round the water, drawn on the ground as the fight goes on (looks only: the pool never shrinks).
    List<(int x, int y, byte k)> lungMud;

    List<(int x, int y, byte k)> LungMud()
    {
        if (lungMud != null) return lungMud;
        lungMud = new();
        var pool = LungPool();
        if (pool.Count == 0) return lungMud;
        int x0 = pool.Min(p => p.Item1) * T - 16, x1 = (pool.Max(p => p.Item1) + 1) * T + 16, y0 = pool.Min(p => p.Item2) * T - 16, y1 = (pool.Max(p => p.Item2) + 1) * T + 16;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                if (x < 0 || y < 0 || x >= COLS * T || y >= ROWS * T) continue;
                if (Water.Contains(worldMap[y / T, x / T])) continue;
                // Distance to the nearest water pixel of the pool, by tile rectangles.
                float d = 99;
                for (int tx = x / T - 2; tx <= x / T + 2; tx++)
                    for (int ty = y / T - 2; ty <= y / T + 2; ty++)
                    {
                        if (!pool.Contains((tx, ty))) continue;
                        float dx = MathF.Max(MathF.Max(tx * T - x, x - (tx * T + T - 1)), 0), dy = MathF.Max(MathF.Max(ty * T - y, y - (ty * T + T - 1)), 0);
                        d = MathF.Min(d, MathF.Sqrt(dx * dx + dy * dy));
                    }
                if (d > 14) continue;
                // A ragged edge: the nearer the water, the likelier it's mud.
                if (Pix.Hash(x, y, 311) > 0.97 - d / 16.0) continue;
                int cx = x / 5, cy = y / 5, lx = x % 5, ly = y % 5;
                bool crack = Pix.Hash(cx, cy, 313) < 0.62 && (Pix.Hash(cx, cy, 317) < 0.5 ? lx == ly : lx + ly == 4);
                lungMud.Add((x, y, (byte)(crack ? 1 : 0)));
            }
        return lungMud;
    }

    float lungDry;   // 0 to 1: how far the mud apron has crept out
    void DrawOasisMud()
    {
        lungDry = Math.Clamp(lungDry + (LungfishShown ? 1 : -1) * Raylib.GetFrameTime() * 0.35f, 0, 1);
        if (lungDry <= 0 || scene != "world") return;
        var spot = Data.SpotById["oasis"];
        if (MathF.Abs(spot.X - player.X) > W + 60 || MathF.Abs(spot.Y - player.Y) > H + 60) return;
        float a = lungDry;
        foreach (var (x, y, k) in LungMud())
        {
            if (x < camX - 1 || x > camX + W || y < camY - 1 || y > camY + H) continue;
            pix.Rect(x, y, 1, 1, k == 1 ? Pal.Rgba(92, 64, 36, 0.8f * a) : Pal.Rgba(190, 152, 104, 0.5f * a));
        }
    }

    /* ---------- The camera ---------- */
    // How far the camera has eased over to frame the pool (0 to 1), and where it frames.
    float lungCamK;
    (float x, float y) LungCamera(float fx, float fy)
    {
        bool on = LungfishShown && scene == "world";
        lungCamK = Math.Clamp(lungCamK + (on ? 1 : -1) * Raylib.GetFrameTime() * 1.4f, 0, 1);
        var pool = LungPool();
        if (lungCamK <= 0 || pool.Count == 0) return (fx, fy);
        float cx = (float)pool.Average(p => p.Item1) * T + 5, cy = (float)pool.Average(p => p.Item2) * T + 5;
        float tx = (player.X + cx) / 2, ty = (player.Y - 8 + cy - 10) / 2;
        float k = lungCamK * lungCamK * (3 - 2 * lungCamK);
        return (fx + (tx - fx) * k, fy + (ty - fy) * k);
    }
}
