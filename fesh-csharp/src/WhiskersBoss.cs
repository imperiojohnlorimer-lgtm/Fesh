using Raylib_cs;

namespace Fesh;

// 1.38: Old Whiskers, the Saltmere lagoon's legend, as a boss fight: a catfish as long as a rowing boat (the game's is
// based on the wels catfish, Silurus glanis, whose barbels taste and feel for food in muddy water) that only comes up
// in the rain, for a berry, and once stole Tomas's grandfather's lunch. The gentle, tricksy first boss of the island:
// the ordinary bottom reel plus one event at a time, unlocked by how far you've reeled it in, each a trick of its own:
//   1 the lunch thief: it mouths the berry and the bobber twitches; the bobber goes UNDER after a random wait and you
//     press <act> once inside a short window (press early and it pinches the berry);
//   2 the captain's whiskers: its whiskers flick three directions, one at a time; once they fade, repeat them with the
//     arrow keys, in order (a memory game);
//   3 quiet in the rain: it listens through the water; hold every control still until the rain has covered the line.
// Nothing hurts you, and a miss costs a little reel, never the line. The lagoon is a tiny pool, so the fish (whiskers and
// all) never leaves it, and the rain rings it. Its sprite is a Blender model (tools/blender/catfish.py renders
// WhiskersArt.cs). FESH_OLDART=1 draws the Fish log's hand-drawn picture instead.
partial class Game
{
    const string WhiskersFish = "old_whiskers";

    sealed class WhiskFight
    {
        public float Banner = 3.2f, T, X, Y, Vx = 1, TargetX, TargetY, WanderT;
        public string Ev = "";          // "", "thiefWarn", "thief", "memoryWarn", "memory", "quietWarn", "quiet"
        public float EvT, NextEv = 5f, Delay = 1.2f, QuietT, WaitT;
        public int Phase = 1, Events, Entered;
        public bool Fresh, Armed;
        public int[] Seq = { 3, 0, 1 };
        public string Title = "OLD WHISKERS", Sub = "Wait for the bobber";
    }
    WhiskFight whisk;

    bool WhiskersOnLine => mode == "reeling" && reel?.Roll?.Id == WhiskersFish && fish != null;
    bool WhiskersShown => (mode == "reeling" || mode == "pause" && pausedFrom == "reeling") && reel?.Roll?.Id == WhiskersFish && fish != null && whisk != null;
    static int WhiskPhaseOf(float p) => p < 0.5f ? 1 : p < 0.78f ? 2 : 3;
    const float WhiskThiefWarn = 1.2f, WhiskWindow = 0.5f, WhiskMemoryWarn = 1.4f, WhiskMemoryLen = 3.0f, WhiskQuietWarn = 1.4f, WhiskQuietNeed = 2.0f, WhiskArmMax = 1.2f;
    static readonly float[] WhiskFlicks = { 0.20f, 0.65f, 1.10f };   // when each whisker flick starts in the preview; each lasts 0.30 s
    const float WhiskFlickLen = 0.30f;

    /* ---------- The lagoon ---------- */
    HashSet<(int, int)> whiskPool;
    List<(float x, float y)> whiskSpots;
    float whiskHalf = 13;   // half the fish's length for the places it fits (smaller only if the lagoon is too small for it)

    // The lagoon spot's own water: its connected lagoon tiles.
    HashSet<(int, int)> WhiskPool()
    {
        if (whiskPool != null) return whiskPool;
        var s = Data.SpotById["lagoon"];
        var seen = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        int sx = (int)(s.X / T), sy = (int)(s.Y / T);
        for (int r = 0; r < 8 && q.Count == 0; r++)
            for (int dy = -r; dy <= r && q.Count == 0; dy++)
                for (int dx = -r; dx <= r && q.Count == 0; dx++)
                {
                    int x = sx + dx, y = sy + dy;
                    if (x >= 0 && y >= 0 && x < COLS && y < ROWS && worldMap[y, x] == 'l') { q.Enqueue((x, y)); seen.Add((x, y)); }
                }
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS || worldMap[ny, nx] != 'l' || !seen.Add((nx, ny))) continue;
                q.Enqueue((nx, ny));
            }
        }
        return whiskPool = seen;
    }

    bool InWhiskPool(float x, float y) => WhiskPool().Contains(((int)MathF.Floor(x / T), (int)MathF.Floor(y / T)));

    // The whole fish, whiskers (they hang down at the head) included, must be over the lagoon's water.
    bool WhiskFits(float x, float y, float half)
    {
        for (float dx = -half; dx <= half + 0.01f; dx += 4)
            if (!InWhiskPool(x + dx, y)) return false;
        return InWhiskPool(x, y - 3.5f) && InWhiskPool(x, y + 4) && InWhiskPool(x + half, y + 6) && InWhiskPool(x + half, y - 3) && InWhiskPool(x - half, y + 3);
    }
    bool WhiskFits(float x, float y) => WhiskFits(x, y, whiskHalf);

    List<(float x, float y)> WhiskSpots()
    {
        if (whiskSpots != null) return whiskSpots;
        whiskSpots = new();
        var pool = WhiskPool();
        if (pool.Count == 0) return whiskSpots;
        int x0 = pool.Min(p => p.Item1) * T, x1 = (pool.Max(p => p.Item1) + 1) * T, y0 = pool.Min(p => p.Item2) * T, y1 = (pool.Max(p => p.Item2) + 1) * T;
        foreach (float half in new[] { 13f, 9f, 5f })
        {
            whiskHalf = half;
            for (float y = y0; y <= y1; y += 3)
                for (float x = x0; x <= x1; x += 3)
                    if (WhiskFits(x, y)) whiskSpots.Add((x, y));
            if (whiskSpots.Count > 0) break;
        }
        return whiskSpots;
    }

    // The nearest place it fits, out from (x, y); null if it fits nowhere (it then stays where it is).
    (float x, float y)? NearestWhiskSpot(float x, float y)
    {
        var spots = WhiskSpots();
        if (spots.Count == 0) return null;
        return spots.OrderBy(p => Dist(p.x, p.y, x, y)).First();
    }

    /* ---------- The fight ---------- */
    void StartWhiskers()
    {
        var spot = NearestWhiskSpot(fish.Bx, fish.By) ?? (fish.Bx, fish.By);
        whisk = new WhiskFight { X = spot.Item1, Y = spot.Item2, TargetX = spot.Item1, TargetY = spot.Item2 };
        fish.Bx = fish.Tx = whisk.X; fish.By = fish.Ty = whisk.Y;
        reel.Progress = reel.Perfect ? 0.38f : 0.3f;
        reel.Pull = 0.75f;
        quake = 0.3f;
        Sfx.Play("splash");
    }

    void MoveWhisk(float dt)
    {
        var m = whisk;
        float speed = m.Ev == "" ? 6 : m.Ev is "thiefWarn" or "memoryWarn" or "quietWarn" ? 2.5f : 0;
        if (speed > 0)
        {
            m.WanderT -= dt;
            if (m.WanderT <= 0 || Dist(m.X, m.Y, m.TargetX, m.TargetY) < 2)
            {
                m.WanderT = Rand(2.5f, 4.5f);
                var spots = WhiskSpots();
                if (spots.Count > 0) { var s = spots[rng.Next(spots.Count)]; m.TargetX = s.x; m.TargetY = s.y; }
            }
            float dx = m.TargetX - m.X, dy = m.TargetY - m.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            if (d > 0.5f)
            {
                float step = MathF.Min(d, speed * dt), nx = m.X + dx / d * step, ny = m.Y + dy / d * step;
                if (WhiskFits(nx, ny)) { m.X = nx; m.Y = ny; }
                else m.WanderT = 0;
                if (MathF.Abs(dx) > 1f) m.Vx = MathF.Sign(dx);
            }
        }
        fish.Bx = fish.Tx = m.X; fish.By = fish.Ty = m.Y;
    }

    // Which trick comes next: phase 1 only steals; phase 2 remembers and steals in turn; phase 3 keeps quiet, remembers and
    // steals in turn. `events` counts this one; a new phase's own trick always comes first.
    static string WhiskPick(int phase, int events, bool fresh) =>
        fresh ? (phase == 2 ? "memory" : phase == 3 ? "quiet" : "thief")
        : phase == 1 ? "thief"
        : phase == 2 ? (events % 2 == 1 ? "memory" : "thief")
        : (events % 3) switch { 1 => "quiet", 2 => "memory", _ => "thief" };

    internal string whiskLast = "";   // how the last trick ended ("ok" or "miss") and what it did to the reel: for the tests
    internal float whiskDelta;

    bool UpdateWhiskersReel(ReelState r, float dt, bool hold, bool tap)
    {
        whisk ??= new WhiskFight { X = fish.Bx, Y = fish.By, TargetX = fish.Bx, TargetY = fish.By };
        var m = whisk;
        m.T += dt;
        m.Banner = Math.Max(0, m.Banner - dt);
        int phase = Math.Max(m.Phase, WhiskPhaseOf(r.Progress));
        if (phase > m.Phase)
        {
            m.Phase = phase;
            m.Events = 0;
            m.Fresh = true;
            m.Banner = 3.2f;
            m.Title = phase == 2 ? "THE CAPTAIN'S WHISKERS" : "QUIET IN THE RAIN";
            m.Sub = phase == 2 ? "Remember, then repeat" : "Hands off";
            m.NextEv = MathF.Max(m.NextEv, 3.4f);   // nothing new under the title card
            Sfx.Play("splash");
        }
        MoveWhisk(dt);
        if (m.Ev == "")
        {
            m.NextEv -= dt;
            if (m.NextEv > 0 || m.Banner > 0) return false;
            m.Events++;
            BeginWhiskEvent(WhiskPick(m.Phase, m.Events, m.Fresh));
            m.Fresh = false;
        }
        switch (m.Ev)
        {
            case "thiefWarn":
                SetPrompt("He's mouthing the berry. Wait for the bobber to go UNDER, then press [<act>] once.", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "thief"; m.EvT = m.Delay + WhiskWindow; }
                return true;   // a press now is only a warning press: it doesn't count
            case "thief":
            {
                m.EvT -= dt;
                float a = m.Delay + WhiskWindow - m.EvT;
                SetPrompt(a < m.Delay ? "Wait for it..." : "NOW! Press [<act>]!", null, true);
                if (m.EvT < 0) return WhiskResolve(r, false, 0, 0.04f, "", "He pinched the berry!");   // a long frame carried it past the window: a press now is too late
                if (tap)
                {
                    if (a < m.Delay) return WhiskResolve(r, false, 0, 0.04f, "", "Too soon! He pinched the berry!");
                    return WhiskResolve(r, true, 0.06f, 0, "Lunch stays yours!", "");
                }
                if (m.EvT <= 0) return WhiskResolve(r, false, 0, 0.04f, "", "He pinched the berry!");
                return true;
            }
            case "memoryWarn":
                SetPrompt("Watch his whiskers... then repeat them with the arrow keys.", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "memory"; m.EvT = WhiskMemoryLen; m.Entered = 0; }
                return true;
            case "memory":
            {
                m.EvT -= dt;
                if (m.EvT < 0) return WhiskResolve(r, false, 0, 0.05f, "", "The old captain fooled you.");   // past the 3 s: too late
                SetPrompt("Repeat the whiskers: " + (m.Entered + 1) + " of 3", null, true);
                int n = 0, dir = -1;
                for (int i = 0; i < 4; i++)
                {
                    string key = i switch { 0 => "up", 1 => "right", 2 => "down", _ => "left" };
                    if (Bind.Pressed(key)) { n++; dir = i; }
                }
                if (n > 1) return WhiskResolve(r, false, 0, 0.05f, "", "Two at once! The old captain fooled you.");
                if (n == 1)
                {
                    if (dir != m.Seq[m.Entered]) return WhiskResolve(r, false, 0, 0.05f, "", "The old captain fooled you.");
                    m.Entered++;
                    Sfx.Play("coin");
                    if (m.Entered >= 3) return WhiskResolve(r, true, 0.08f, 0, "You know his tricks!", "");
                }
                if (m.EvT <= 0) return WhiskResolve(r, false, 0, 0.05f, "", "The old captain fooled you.");
                return true;
            }
            case "quietWarn":
                SetPrompt("Hands off. Let the rain cover the line: let go of everything.", null, true);
                if ((m.EvT -= dt) <= 0) { m.Ev = "quiet"; m.Armed = false; m.QuietT = 0; m.WaitT = 0; }
                return true;
            case "quiet":
            {
                // Every control still: the reel, the arrows, the stick. It only starts counting once you've let go of
                // whatever you were holding (up to a moment of grace), and any touch after that gives you away.
                bool rawHeld = Bind.Down("act") || Bind.Down("use") || Inp.Down(KeyboardKey.Enter) || pointerHold;   // the control itself, even while Resume's click is still being waited out
                bool neutral = !hold && !rawHeld && !tap && !Bind.Down("up") && !Bind.Down("down") && !Bind.Down("left") && !Bind.Down("right")
                    && !Bind.Pressed("up") && !Bind.Pressed("down") && !Bind.Pressed("left") && !Bind.Pressed("right") && Inp.Stick.Length() < 0.25f;
                SetPrompt(m.Armed ? "Quiet... don't touch anything." : "Let go of everything!", null, true);
                if (!m.Armed)
                {
                    if (neutral) m.Armed = true;
                    else if ((m.WaitT += dt) > WhiskArmMax) return WhiskResolve(r, false, 0, 0.06f, "", "He felt you tug.");
                    return true;
                }
                if (!neutral) return WhiskResolve(r, false, 0, 0.06f, "", "He felt you tug.");
                if ((m.QuietT += dt) >= WhiskQuietNeed) return WhiskResolve(r, true, 0.10f, 0, "He drifted closer.", "");
                return true;
            }
        }
        return false;
    }

    // A trick is over: a win pays reel, a miss costs some (never below 0.05, never raising a fish that was lower).
    bool WhiskResolve(ReelState r, bool ok, float gain, float loss, string good, string bad)
    {
        float before = r.Progress;
        if (ok)
        {
            r.Progress += gain;
            Floater(good, player.X, player.Y - 26, "#7fd36b");
            Sfx.Play("coin");
            if (r.Progress >= 1) { whiskDelta = r.Progress - before; whiskLast = "ok"; LandCatch(); return true; }
        }
        else
        {
            MirePenalty(r, loss);
            Floater(bad, player.X, player.Y - 26, "#ff9a8a");
            Sfx.Play("nope");
        }
        whiskDelta = r.Progress - before; whiskLast = ok ? "ok" : "miss";
        Burst(whisk.X, whisk.Y, "#d9c89a", ok ? 6 : 4);
        EndWhiskEvent();
        return true;
    }

    void BeginWhiskEvent(string ev)
    {
        var m = whisk;
        var r = reel;
        m.Ev = ev + "Warn";
        m.EvT = ev switch { "thief" => WhiskThiefWarn, "memory" => WhiskMemoryWarn, _ => WhiskQuietWarn };
        if (ev == "thief") m.Delay = Rand(0.7f, 1.7f);
        if (ev == "memory") m.Seq = WhiskSequence();
        m.Entered = 0; m.Armed = false; m.QuietT = 0; m.WaitT = 0;
        r.Running = 0; r.Tension = Math.Min(r.Tension, 0.3f); r.HoldT = 0; r.Digging = false; r.ZoneV = 0;
        Sfx.Play("splash");
    }

    // Three directions (0 up, 1 right, 2 down, 3 left): no two alike in a row, and both an up-down and a left-right one.
    int[] WhiskSequence()
    {
        for (int tries = 0; tries < 100; tries++)
        {
            var s = new[] { rng.Next(4), rng.Next(4), rng.Next(4) };
            if (s[0] == s[1] || s[1] == s[2]) continue;
            if (s.All(d => d % 2 == 0) || s.All(d => d % 2 == 1)) continue;
            return s;
        }
        return new[] { 3, 0, 1 };
    }

    void EndWhiskEvent()
    {
        var m = whisk;
        m.Ev = "";
        m.NextEv = m.Phase == 1 ? Rand(6.5f, 9f) : Rand(5f, 7f);
        m.WanderT = 0;
        if (reel != null) { reel.RunT = Rand(1.6f, 3f); reel.Tension = 0; reel.HoldT = 0; reel.Digging = false; reel.FishTimer = 0.4f; }
    }

    // Every frame (pause or not): forget the fight once the fish is landed or lost.
    void UpdateWhiskers(float dt)
    {
        if (!WhiskersOnLine && mode != "pause") whisk = null;
    }

    /* ---------- The picture ---------- */
    static (Color[] col, byte[] h)[] whiskFrames;

    static (Color[] col, byte[] h) WhiskFrame(int i)
    {
        if (whiskFrames == null)
        {
            var pal = WhiskersArt.Palette.Select(Pal.C).ToArray();
            int n = WhiskersArt.Width * WhiskersArt.Height;
            whiskFrames = new (Color[], byte[])[WhiskersArt.Data.Length];
            for (int f = 0; f < whiskFrames.Length; f++)
            {
                using var src = new MemoryStream(Convert.FromBase64String(WhiskersArt.Data[f]));
                using var z = new System.IO.Compression.ZLibStream(src, System.IO.Compression.CompressionMode.Decompress);
                var raw = new byte[n * 2];
                int got = 0;
                while (got < raw.Length) { int k = z.Read(raw, got, raw.Length - got); if (k <= 0) break; got += k; }
                var col = new Color[n];
                for (int p = 0; p < n; p++) col[p] = raw[p] == 255 ? default : pal[raw[p]];
                whiskFrames[f] = (col, raw[n..]);
            }
        }
        return whiskFrames[i];
    }

    static int WhiskFrameIndex(float up, int phase, bool open)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < WhiskersArt.Up.Length; i++)
        {
            if (WhiskersArt.Open[i] != open) continue;
            float d = MathF.Abs(WhiskersArt.Up[i] - up) + (open ? 0 : (WhiskersArt.Phase[i] == ((phase % WhiskersArt.Phases) + WhiskersArt.Phases) % WhiskersArt.Phases ? 0 : 0.5f));
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // Draws the catfish with its middle at (x, y) on the water, facing dir, its middle `sink` rows under the surface:
    // pixels above the water are drawn as they are, the rest (unless dropUnder) as a dim shape under the lagoon's water.
    internal void DrawWhiskFish(float x, float y, int dir, float up, int phase, bool open, float sink, bool dropUnder = false)
    {
        if (OldArt)
        {
            if (sink > 8) return;
            y += MathF.Min(0, sink);
            FishArt.Draw(pix, WhiskersFish, (int)x - 18, (int)y - 6, 36, 12, dir < 0);
            return;
        }
        var (col, h) = WhiskFrame(WhiskFrameIndex(up, phase, open));
        int w = WhiskersArt.Width, hh = WhiskersArt.Height, ax = WhiskersArt.AnchorX, ay = WhiskersArt.AnchorY;
        int ox = (int)MathF.Round(x), oy = (int)MathF.Round(y + sink);
        var water = Pal.C("#24807a");
        for (int j = 0; j < hh; j++)
            for (int i = 0; i < w; i++)
            {
                int p = j * w + i;
                var c = col[p];
                if (c.A == 0) continue;
                bool above = h[p] - WhiskersArt.Lift > sink;
                int px = ox + (dir < 0 ? ax - i : i - ax), py = oy + j - ay;
                if (above) { pix.Rect(px, py, 1, 1, c); continue; }
                if (dropUnder) continue;
                if (!InWhiskPool(px, py + 1.5f)) continue;
                var u = Pix.Mix(c, water, 0.38f);
                u.A = 215;
                pix.Rect(px, py, 1, 1, u);
            }
    }

    // A little arrow badge (5x5) in pixels: 0 up, 1 right, 2 down, 3 left.
    void PixArrow(float cx, float cy, int dir, Color c)
    {
        foreach (var (u, v) in new[] { (0, -2), (-1, -1), (0, -1), (1, -1), (-2, 0), (0, 0), (2, 0), (0, 1), (0, 2) })
        {
            var (px, py) = dir switch { 0 => (u, v), 1 => (-v, u), 2 => (-u, -v), _ => (v, -u) };
            pix.Rect(cx + px, cy + py, 1, 1, c);
        }
    }

    // Which whisker flick (0 to 2) is showing now in the preview, or -1.
    int WhiskFlickNow(WhiskFight m)
    {
        if (m.Ev != "memoryWarn") return -1;
        float w = WhiskMemoryWarn - m.EvT;
        for (int i = 0; i < 3; i++) if (w >= WhiskFlicks[i] && w < WhiskFlicks[i] + WhiskFlickLen) return i;
        return -1;
    }

    // Where the bobber floats: just in front of its mouth. It twitches while he mouths the berry and goes under at the
    // moment to press (true) for the window.
    (float x, float y, bool under) WhiskBobber(WhiskFight m)
    {
        int dir = m.Vx < 0 ? -1 : 1;
        float bx = m.X + dir * 19, by = m.Y - 1;
        bool under = false;
        if (m.Ev == "thief")
        {
            float a = m.Delay + WhiskWindow - m.EvT;
            under = a >= m.Delay;
            if (!under && (int)(a * 9) % 5 == 0) by += 1;   // a decoy nudge
        }
        return (bx, by, under);
    }

    // Under the surface: the fish in the lagoon's water, rain rings across the pool, silt, and the bobber.
    void DrawWhiskUnder(float t)
    {
        if (!WhiskersShown) return;
        var m = whisk;
        int dir = m.Vx < 0 ? -1 : 1;
        // The rain: rings spreading across the whole lagoon, heavier than the weather alone.
        var pool = WhiskPool();
        if (pool.Count > 0)
        {
            int x0 = pool.Min(p => p.Item1) * T, x1 = (pool.Max(p => p.Item1) + 1) * T, y0 = pool.Min(p => p.Item2) * T, y1 = (pool.Max(p => p.Item2) + 1) * T;
            for (int i = 0; i < 16; i++)
            {
                float cycle = t * 1.3f + i * 0.37f, age = cycle % 1;
                int seed = (int)cycle * 31 + i;
                float rx = x0 + (float)Pix.Hash(seed, i, 401) * (x1 - x0), ry = y0 + (float)Pix.Hash(seed, i, 403) * (y1 - y0);
                if (!InWhiskPool(rx, ry)) continue;
                pix.Ring(rx, ry, 1 + age * 5, Pal.Rgba(230, 246, 244, 0.55f * (1 - age)));
            }
        }
        // Soft silt behind it while it moves.
        if (m.Ev is "" or "thiefWarn" or "memoryWarn")
            for (int i = 0; i < 3; i++)
            {
                float age = (t * 0.5f + i / 3f) % 1, px = m.X - dir * (12 + age * 10) + MathF.Sin(i * 2.3f + t) * 2, py = m.Y + MathF.Cos(i * 1.7f) * 2;
                if (InWhiskPool(px, py)) pix.Rect(px, py, 2, 1, Pal.Rgba(190, 170, 120, 0.25f * (1 - age)));
            }
        float sink = m.Ev switch { "quietWarn" or "quiet" => 8, "thief" => 5, _ => 6 };
        bool open = m.Ev == "thief" && WhiskBobber(m).under;
        DrawWhiskFish(m.X, m.Y, dir, 0, (int)(t * (m.Ev is "quiet" ? 1 : 3)), open, sink);
    }

    // Over everything (the oak beside the lagoon can hide the fish itself): the bobber, the whisker badges and the quiet ring.
    void DrawWhiskOver(float t)
    {
        if (!WhiskersShown) return;
        var m = whisk;
        int dir = m.Vx < 0 ? -1 : 1;
        if (m.Ev is "thiefWarn" or "thief")
        {
            var (bx, by, under) = WhiskBobber(m);
            if (!under) { pix.Rect(bx - 1, by - 1, 3, 2, "#e04b3a"); pix.Rect(bx - 1, by - 2, 3, 1, "#ffffff"); }
            else pix.Ring(bx, by + 1, 2 + (t * 6) % 4, Pal.Rgba(255, 255, 255, 0.8f));   // under: only the rings are left
        }
        int fl = WhiskFlickNow(m);
        if (fl >= 0)
        {
            // The whisker flick: an arrow badge above its head, brightest at the start.
            float k = (WhiskMemoryWarn - m.EvT - WhiskFlicks[fl]) / WhiskFlickLen;
            PixArrow(m.X + dir * 12, m.Y - 12, m.Seq[fl], Pal.WithAlpha(Pal.C("#ffe08a"), 1 - 0.5f * k));
            pix.Ring(m.X + dir * 12, m.Y - 12, 5, Pal.Rgba(255, 224, 138, 0.5f * (1 - k)));
        }
        if (m.Ev == "quiet" && m.Armed)
            pix.Ring(m.X + dir * 4, m.Y + 2, 4 + 14 * (1 - m.QuietT / WhiskQuietNeed), Pal.Rgba(226, 244, 240, 0.7f));   // the quiet closing round it
    }

    /* ---------- The cue by the reel ---------- */
    void DrawWhiskersCue(WhiskFight m, float rx, Func<float, float> Y)
    {
        void Big(string s, Color c, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui700, 30), Y(at), FontKind.Ui700, 30, c);
        void Small(string s, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui600, 18), Y(at), FontKind.Ui600, 18, Pal.C("#ffffff"));
        bool blink = (int)(time * 8) % 2 == 0;
        float x0 = rx - 340, cy = Y(238);
        var gold = Pal.C("#f3c25b"); var green = Pal.C("#7fd36b"); var panel = Pal.C("rgba(26,30,22,0.88)");
        switch (m.Ev)
        {
            case "thiefWarn": Big("He's nibbling...", Pal.C("#e8f4ee"), 130); Small(Bind.Fix("Don't press yet: wait for the bobber to go under"), 168); break;
            case "thief":
            {
                bool now = WhiskBobber(m).under;
                Big(now ? "NOW!" : "WAIT...", now ? Pal.WithAlpha(Pal.C("#ff6a5a"), blink ? 1 : 0.75f) : Pal.C("#e8f4ee"), 130);
                Small(Bind.Fix(now ? "Press <act> once!" : "Let the bobber go under"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                float bx = x0 + 165, by = cy + (now ? 16 : MathF.Sin(time * 9) * 3);
                Gfx.Rect(x0 + 20, cy + 14, 290, 3, Pal.C("#5fb7b0"), 1);   // the water line
                if (now) LungRing(bx, cy + 14, 14 + (time * 30) % 10, Pal.C("#ffffff"));
                else { Gfx.Circle(bx, by, 12, Pal.C("#e04b3a")); Gfx.Rect(bx - 12, by - 12, 24, 10, Pal.C("#ffffff"), 5); }
                break;
            }
            case "memoryWarn":
            {
                Big("Remember...", Pal.C("#e8f4ee"), 130); Small(Bind.Fix("Watch his whiskers"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                int fl = WhiskFlickNow(m);
                for (int i = 0; i < 3; i++) Gfx.Circle(x0 + 55 + i * 110, cy, 6, i == fl ? gold : Pal.C("#6a6a52"));
                if (fl >= 0) DrawArrow(x0 + 165, cy, m.Seq[fl], gold);
                break;
            }
            case "memory":
            {
                Big("REPEAT!", Pal.WithAlpha(gold, blink ? 1 : 0.75f), 130); Small(Bind.Fix($"Press the arrows in order  {m.Entered}/3"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                for (int i = 0; i < 3; i++)
                {
                    float bx = x0 + 55 + i * 110;
                    Gfx.Rect(bx - 24, cy - 24, 48, 48, Pal.C(i < m.Entered ? "rgba(40,90,40,0.9)" : "rgba(60,52,32,0.85)"), 6);
                    if (i < m.Entered) DrawArrow(bx, cy, m.Seq[i], green);
                    else Gfx.Text("?", bx - Gfx.Measure("?", FontKind.Ui700, 26) / 2, cy - 14, FontKind.Ui700, 26, Pal.C("#8a7a5a"));
                }
                break;
            }
            case "quietWarn": Big("Hands off...", Pal.C("#e8f4ee"), 130); Small(Bind.Fix("Let go of everything"), 168); break;
            case "quiet":
            {
                Big(m.Armed ? "QUIET" : "LET GO", m.Armed ? green : Pal.WithAlpha(gold, blink ? 1 : 0.75f), 130);
                Small(Bind.Fix(m.Armed ? "Don't touch anything" : "Let go of every key"), 168);
                Gfx.Rect(x0, cy - 36, 330, 72, panel, 8);
                float k = m.Armed ? 1 - m.QuietT / WhiskQuietNeed : 1;
                LungRing(x0 + 165, cy, 8 + 24 * k, m.Armed ? green : gold);
                break;
            }
        }
    }

    /* ---------- The camera ---------- */
    float whiskCamK;
    (float x, float y) WhiskCamera(float fx, float fy)
    {
        bool on = WhiskersShown && scene == "world";
        whiskCamK = Math.Clamp(whiskCamK + (on ? 1 : -1) * Raylib.GetFrameTime() * 1.4f, 0, 1);
        var pool = WhiskPool();
        if (whiskCamK <= 0 || pool.Count == 0) return (fx, fy);
        float cx = (float)pool.Average(p => p.Item1) * T + 5, cy = (float)pool.Average(p => p.Item2) * T + 5;
        float tx = (player.X + cx) / 2, ty = (player.Y - 8 + cy - 6) / 2;
        float k = whiskCamK * whiskCamK * (3 - 2 * whiskCamK);
        return (fx + (tx - fx) * k, fy + (ty - fy) * k);
    }
}
