using Raylib_cs;

namespace Fesh;

// 1.36: the Mire leviathan, Mirewood's legend, as a boss fight. A giant arapaima in the mangrove swamp pool (night, a
// worm). Arapaima really do have to come up for air (they breathe it, and drown if they can't), their scales are armour,
// and they leap; mangrove roots ring the pool. The fight is the ordinary bottom-hugger reel (short pumps) plus events,
// one at a time, by how far you've reeled it in (a name bar shows its stamina):
//   1 beneath the mud: every so often it rises to gulp air: bubbles first and the swamp goes quiet, then its head breaks
//     the surface with its mouth open; press <act> then to haul it in a good way (miss and it gets its breath);
//   2 among the roots: it also bolts for the mangrove roots at the edge of the pool: an arrow shows which way to pull it
//     away (a move key or the stick); too late and the line rubs on the roots (it costs reel, never the line);
//   3 the moonlit giant: it leaps clear of the pool too: press as the marker crosses the gold, like any jumper.
// It never hurts you. The sprite is a Blender model (tools/blender/arapaima.py renders ArapaimaArt.cs): the body tipped
// nose-up to nose-down, three steps of the tail, and the mouth open; every pixel knows its height, so it can be sunk to
// any depth and only what is above the water drawn (the rest a shape under the murky water). FESH_OLDART=1 draws the
// fish log's hand-drawn picture instead.
partial class Game
{
    const string MireFish = "mire_leviathan";

    sealed class MireFight
    {
        public float Banner = 3.2f, T, X, Y, Vx = 1, TargetX, TargetY, WanderT;
        public string Ev = "";          // "", "rise" (bubbles, coming up), "gulp" (head out), "dive", "roots" (bolting for them), "rub" (in them), "leap"
        public float EvT, NextEv = 5.5f, Hush, LeapX0, LeapDir = 1, LeapDist = 30;
        public int Phase = 1, Events, RootI;
        public bool Struck, Turned, Told;
        public string Title = "MIRE LEVIATHAN", Sub = "Legend of Mirewood";
    }
    MireFight mire;

    bool MireOnLine => mode == "reeling" && reel?.Roll?.Id == MireFish && fish != null;
    // On the line, or paused in the middle of the fight: it stays drawn, framed and quiet behind the pause menu.
    bool MireShown => (mode == "reeling" || mode == "pause" && pausedFrom == "reeling") && reel?.Roll?.Id == MireFish && fish != null && mire != null;
    static int MirePhaseOf(float p) => p < 0.5f ? 1 : p < 0.78f ? 2 : 3;
    const float RiseLen = 1.4f, GulpLen = 1.15f, DiveLen = 0.5f, RootsLen = 1.7f, RubLen = 1.1f;

    /* ---------- The pool and its roots ---------- */
    HashSet<(int, int)> mirePool;
    List<(float bx, float by, float wx, float wy)> mireRoots;

    // The swamp spot's own water: its connected pool tiles (the spot's radius isn't a safe swimming boundary).
    HashSet<(int, int)> MirePool()
    {
        if (mirePool != null) return mirePool;
        var s = Data.SpotById["swamp"];
        var seen = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        var start = ((int)(s.X / T), (int)(s.Y / T));
        // The spot is just off the water: start from the nearest pool tile.
        for (int r = 0; r < 8 && q.Count == 0; r++)
            for (int dy = -r; dy <= r && q.Count == 0; dy++)
                for (int dx = -r; dx <= r && q.Count == 0; dx++)
                    if (worldMap[start.Item2 + dy, start.Item1 + dx] == 'm') { q.Enqueue((start.Item1 + dx, start.Item2 + dy)); seen.Add(q.Peek()); }
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS || worldMap[ny, nx] != 'm' || !seen.Add((nx, ny))) continue;
                q.Enqueue((nx, ny));
            }
        }
        return mirePool = seen;
    }

    bool InMirePool(float x, float y) => MirePool().Contains(((int)MathF.Floor(x / T), (int)MathF.Floor(y / T)));

    // Where the mangroves stand: the pool's west and east ends, in its upper and lower halves. Each clump's trunk is on
    // the bank (bx, by) and its roots reach into the water to (wx, wy), where the fish bolts.
    List<(float bx, float by, float wx, float wy)> MireRoots()
    {
        if (mireRoots != null) return mireRoots;
        var pool = MirePool();
        mireRoots = new();
        if (pool.Count == 0) return mireRoots;
        int y0 = pool.Min(p => p.Item2), y1 = pool.Max(p => p.Item2);
        foreach (int row in new[] { y0 + (y1 - y0) / 4, y0 + (y1 - y0) * 3 / 4 })
        {
            var tiles = pool.Where(p => p.Item2 == row).ToList();
            if (tiles.Count == 0) continue;
            int xl = tiles.Min(p => p.Item1), xr = tiles.Max(p => p.Item1);
            mireRoots.Add((xl * T - 3, row * T + 6, xl * T + 6, row * T + 5));
            mireRoots.Add(((xr + 1) * T + 3, row * T + 6, (xr + 1) * T - 6, row * T + 5));
        }
        return mireRoots;
    }

    // A mangrove at the pool's edge: prop roots arching from a short trunk on the bank down into the water, and a small
    // leafy crown. Looks only (walk through it). While the leviathan bolts for it, its roots flash.
    void DrawMangrove(float bx, float by, float wx, float wy, bool warn, float t)
    {
        float side = MathF.Sign(wx - bx);
        var root = warn && (int)(t * 8) % 2 == 0 ? Pal.C("#ffb07a") : Pal.C("#6a5236");
        var rootDk = warn ? Pal.C("#c4603a") : Pal.C("#46371f");
        // From high on the trunk out and down to the water, an arch each: the shaded undersides first, then the roots.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < 6; i++)
            {
                float sx = bx + side * (i % 2), sy = by - 7 - i * 1.6f;
                float ex = bx + side * (3 + i * 2.2f), ey = wy + 2 - (i % 3) * 3;
                int steps = (int)(Dist(sx, sy, ex, ey) * 1.6f) + 6;
                for (int k = 0; k <= steps; k++)
                {
                    float q = k / (float)steps, x = sx + (ex - sx) * q, y = sy + (ey - sy) * q - MathF.Sin(q * MathF.PI) * 5;
                    if (pass == 0) pix.Rect(x, y + 1, 1, 1, Pal.Rgba(30, 22, 12, 0.55f));
                    else pix.Rect(x, y, 1, 1, q > 0.78f ? rootDk : root);
                }
                if (pass == 1) pix.Rect(ex - 1, ey + 1, 3, 1, Pal.Rgba(220, 230, 210, 0.25f));   // a ripple where it enters the water
            }
        pix.Rect(bx - 1, by - 18, 3, 12, "#5a4630");
        pix.Rect(bx - 1, by - 18, 1, 12, "#7c6546");
        // The crown: overlapping clumps of dark glossy leaves.
        foreach (var (ox, oy, r) in new[] { (-5f, -22f, 5f), (4f, -23f, 5.5f), (0f, -27f, 5f), (side * 7, -19f, 4f) })
        {
            for (int y = (int)-r; y <= r; y++)
                for (int x = (int)-r; x <= r; x++)
                {
                    float d = (x * x + y * y) / (r * r);
                    if (d > 1) continue;
                    var c = d > 0.72f ? Pal.C("#1f3a22") : y < -r * 0.3f && x < 0 ? Pal.C("#5d8f45") : Pal.C("#3a6630");
                    pix.Rect(bx + ox + x, by + oy + y, 1, 1, c);
                }
        }
    }

    void AddMireObjects(List<(float y, Action draw)> list)
    {
        if (Math.Abs(Data.SpotById["swamp"].X - player.X) > W + 60 || Math.Abs(Data.SpotById["swamp"].Y - player.Y) > H + 60) return;
        var roots = MireRoots();
        for (int i = 0; i < roots.Count; i++)
        {
            var (bx, by, wx, wy) = roots[i];
            bool warn = MireShown && mire is { Ev: "roots" or "rub" } m && m.RootI == i;
            list.Add((by, () => DrawMangrove(bx, by, wx, wy, warn, time)));
        }
    }

    /* ---------- The fight ---------- */
    void StartMire()
    {
        mire = new MireFight { X = fish.Bx, Y = fish.By };
        if (!InMirePool(mire.X, mire.Y) && MirePool().Count > 0)
        {
            // A cast that came down at the very edge: put the fish on the nearest pool tile.
            var (tx, ty) = MirePool().OrderBy(p => Dist(p.Item1 * T + 5, p.Item2 * T + 5, mire.X, mire.Y)).First();
            mire.X = tx * T + 5; mire.Y = ty * T + 5;
        }
        mire.TargetX = mire.X; mire.TargetY = mire.Y;
        reel.Progress = reel.Perfect ? 0.38f : 0.3f;
        reel.Pull = 0.75f;
        quake = 0.35f;
        Sfx.Play("splash");
    }

    // Steering: the fish wanders the pool slowly, bolts for the roots, and is pulled back toward the middle.
    void MoveMire(float dt)
    {
        var m = mire;
        float speed = m.Ev == "roots" ? 34 : m.Ev is "rub" or "gulp" or "rise" or "dive" or "leap" ? 0 : 11;
        if (m.Ev == "roots")
        {
            var rt = MireRoots()[m.RootI];
            m.TargetX = rt.wx; m.TargetY = rt.wy;
        }
        else if (m.Ev == "")
        {
            m.WanderT -= dt;
            if (m.WanderT <= 0 || Dist(m.X, m.Y, m.TargetX, m.TargetY) < 3)
            {
                m.WanderT = Rand(2.5f, 4.5f);
                var pool = MirePool().ToList();
                if (pool.Count > 0)
                {
                    // Somewhere in the pool, nearer the middle (and the bank you're on) than the edge.
                    var (tx, ty) = pool[rng.Next(pool.Count)];
                    float cx = (float)pool.Average(p => p.Item1) * T + 5, cy = (float)pool.Average(p => p.Item2) * T + 5;
                    m.TargetX = (tx * T + 5 + cx) / 2; m.TargetY = (ty * T + 5 + cy) / 2;
                }
            }
        }
        float dx = m.TargetX - m.X, dy = m.TargetY - m.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        if (speed > 0 && d > 0.5f)
        {
            float step = MathF.Min(d, speed * dt), nx = m.X + dx / d * step, ny = m.Y + dy / d * step;
            if (InMirePool(nx, ny)) { m.X = nx; m.Y = ny; }
            else m.WanderT = 0;
            if (MathF.Abs(dx) > 0.5f) m.Vx = MathF.Sign(dx);
        }
        fish.Bx = fish.Tx = m.X; fish.By = fish.Ty = m.Y;
    }

    // Called from UpdateReel before the ordinary reel: true when an event has the reel (the bar waits meanwhile).
    bool UpdateMireReel(ReelState r, float dt, bool hold, bool tap)
    {
        mire ??= new MireFight { X = fish.Bx, Y = fish.By, TargetX = fish.Bx, TargetY = fish.By };
        var m = mire;
        m.T += dt;
        m.Banner = Math.Max(0, m.Banner - dt);
        int phase = Math.Max(m.Phase, MirePhaseOf(r.Progress));
        if (phase > m.Phase)
        {
            m.Phase = phase;
            m.Banner = 3.2f;
            m.Title = phase == 2 ? "AMONG THE ROOTS" : "THE MOONLIT GIANT";
            m.Sub = phase == 2 ? "It will bolt for the mangroves: pull it the other way" : "It leaps: press as the marker crosses the gold";
            m.NextEv = MathF.Max(m.NextEv, 3.4f);   // nothing new under the title card
            Sfx.Play("splash");
            quake = 0.3f;
        }
        bool quiet = m.Ev is "rise" or "gulp";
        m.Hush = Math.Clamp(m.Hush + (quiet ? dt * 1.5f : -dt * 0.6f), 0, 1);
        MoveMire(dt);
        if (m.Ev == "")
        {
            m.NextEv -= dt;
            if (m.NextEv > 0) return false;
            m.Events++;
            // Phase 1 only breathes; phase 2 bolts for the roots twice for every breath; phase 3 also leaps.
            string next = m.Phase == 1 ? "rise"
                : m.Phase == 2 ? (m.Events % 3 == 0 ? "rise" : "roots")
                : (m.Events % 3) switch { 0 => "rise", 1 => "leap", _ => "roots" };
            if (next == "roots" && MireRoots().Count == 0) next = "rise";
            BeginMireEvent(next);
        }
        switch (m.Ev)
        {
            case "rise":
                SetPrompt("Bubbles! It's coming up for air. Get ready to press [<act>]", null, true);
                if (!m.Told) { m.Told = true; Floater("The whole swamp goes quiet...", player.X, player.Y - 30, "#cfe3c0"); }
                if ((m.EvT -= dt) <= 0) { m.Ev = "gulp"; m.EvT = GulpLen; m.Struck = false; Sfx.Play("gulp"); Burst(m.X + m.Vx * 8, m.Y, "#cfe3c0", 8); }
                return true;
            case "gulp":
                SetPrompt("It's gulping air! Press [<act>] now!", "<act>", true);
                if (tap && !m.Struck)
                {
                    m.Struck = true;
                    r.Progress += 0.09f;
                    Floater("Caught it breathing!", player.X, player.Y - 26, "#7fd36b");
                    Sfx.Play("coin");
                    if (r.Progress >= 1) { LandCatch(); return true; }
                }
                if ((m.EvT -= dt) <= 0)
                {
                    if (!m.Struck) { MirePenalty(r, 0.03f); Floater("It got its breath.", player.X, player.Y - 26, "#ff9a8a"); }
                    m.Ev = "dive"; m.EvT = DiveLen;
                    Burst(m.X, m.Y, "#cfe3c0", 6);
                }
                return true;
            case "dive":
                if ((m.EvT -= dt) <= 0) EndMireEvent();
                return true;
            case "roots":
            {
                var rt = MireRoots()[m.RootI];
                int away = rt.bx < rt.wx ? 1 : 3;   // roots on the west bank: pull east (right), and the other way round (by the bank, so it can't flip as it arrives: Codex)
                SetPrompt(away == 1 ? "It's bolting for the roots! Pull it away: [<right>]" : "It's bolting for the roots! Pull it away: [<left>]", null, true);
                if (ArrowPressed() is int d && d is 1 or 3)
                {
                    if (d == away)
                    {
                        r.Progress += 0.04f;
                        Floater("You turned it!", player.X, player.Y - 26, "#7fd36b");
                        Sfx.Play("coin");
                        m.Vx = d == 1 ? 1 : -1;
                        m.TargetX = m.X + m.Vx * 26; m.TargetY = m.Y;
                        if (r.Progress >= 1) { LandCatch(); return true; }
                        EndMireEvent();
                        return true;
                    }
                    Floater("The other way!", player.X, player.Y - 26, "#ff9a8a");
                    Sfx.Play("nope");
                }
                if ((m.EvT -= dt) <= 0 || Dist(m.X, m.Y, rt.wx, rt.wy) < 3)
                {
                    m.Ev = "rub"; m.EvT = RubLen;
                    MirePenalty(r, 0.07f);
                    Floater("Into the roots! The line rubs.", player.X, player.Y - 26, "#ff9a8a");
                    Sfx.Play("splash");
                    quake = 0.25f;
                }
                return true;
            }
            case "rub":
                SetPrompt("It's thrashing in the roots...", null, true);
                if ((m.EvT -= dt) <= 0) { EndMireEvent(); m.TargetX = (m.X + mire.LeapX0) / 2; }
                return true;
            case "leap":
                // The jumper's timing bar: the marker runs across, press as it crosses the gold.
                r.Leap -= dt;
                r.LeapMark = 1 - r.Leap / r.LeapLen;
                (fish.Bx, fish.By) = MireLeapPos(Math.Clamp(r.LeapMark, 0, 1));
                if (tap && !r.LeapDone)
                {
                    r.LeapDone = true;
                    bool good = r.LeapMark >= 0.6f && r.LeapMark <= 0.85f;
                    if (good) r.Progress += 0.12f; else MirePenalty(r, 0.06f);
                    Floater(good ? "Nice!" : "Missed!", player.X, player.Y - 24, good ? "#7fd36b" : "#ff9a8a");
                    Sfx.Play(good ? "coin" : "nope");
                    if (r.Progress >= 1) { LandCatch(); return true; }
                }
                SetPrompt("It leaps! Press [<act>] as the marker hits the gold!", null, true);
                if (r.Leap <= 0)
                {
                    if (!r.LeapDone) { MirePenalty(r, 0.06f); Floater("It shook its head!", player.X, player.Y - 24, "#ff9a8a"); }
                    r.Leap = 0;
                    m.X = m.LeapX0 + m.LeapDir * m.LeapDist;

                    m.TargetX = m.X;
                    Burst(m.X, m.Y, "#cfe3c0", 12); Sfx.Play("splash"); quake = 0.3f;
                    EndMireEvent();
                }
                return true;
        }
        return false;
    }

    void BeginMireEvent(string ev)
    {
        var m = mire;
        var r = reel;
        m.Ev = ev;
        r.HoldT = 0; r.Digging = false; r.ZoneV = 0;
        switch (ev)
        {
            case "rise": m.EvT = RiseLen; m.Told = m.Told && m.Events > 1; break;
            case "roots":
            {
                // The nearest clump it isn't already right beside.
                var roots = MireRoots();
                m.RootI = Enumerable.Range(0, roots.Count).OrderBy(i => Dist(roots[i].wx, roots[i].wy, m.X, m.Y) < 8 ? 999 : Dist(roots[i].wx, roots[i].wy, m.X, m.Y)).First();
                m.EvT = RootsLen;
                m.LeapX0 = m.X;
                Sfx.Play("splash");
                break;
            }
            case "leap":
            {
                // It leaps across the pool as far as it can and still come down in it (straight up where it's narrow):
                // every point under the arc is water, so it never crosses the bank (Codex).
                m.LeapX0 = m.X;
                bool Clear(int dir, float dist)
                {
                    for (int s = 0; s <= 10; s++) if (!InMirePool(m.X + dir * dist * s / 10f, m.Y)) return false;
                    return true;
                }
                int first = m.Vx < 0 ? -1 : 1;
                (m.LeapDir, m.LeapDist) = (first, 0);
                foreach (float dist in new[] { 30f, 22f, 14f, 8f })
                {
                    if (Clear(first, dist)) { (m.LeapDir, m.LeapDist) = (first, dist); break; }
                    if (Clear(-first, dist)) { (m.LeapDir, m.LeapDist) = (-first, dist); break; }
                }
                m.Vx = m.LeapDir;
                r.Leap = r.LeapLen = 1.25f; r.LeapMark = 0; r.LeapDone = false;
                Sfx.Play("splash");
                break;
            }
        }
    }

    // A missed breath, a rub in the roots or a missed leap: it costs reel down to 0.05, but never adds any to a fish that
    // was already nearly gone (Codex: flooring at 0.05 raised one at 0.02).
    static void MirePenalty(ReelState r, float x) => r.Progress = MathF.Min(r.Progress, MathF.Max(0.05f, r.Progress - x));

    void EndMireEvent()
    {
        var m = mire;
        m.Ev = "";
        m.NextEv = m.Phase == 1 ? Rand(7.5f, 10f) : Rand(5f, 7.5f);
        m.WanderT = 0;
        if (reel != null) { reel.HoldT = 0; reel.Digging = false; reel.FishTimer = 0.4f; }
    }

    // Every frame (pause or not): forget the fight once the fish is landed or lost.
    void UpdateMire(float dt)
    {
        if (!MireOnLine) { if (mode != "pause") mire = null; return; }
    }

    float MireHush => MireShown ? mire.Hush : 0;

    /* ---------- The picture ---------- */
    static (Color[] col, byte[] h)[] mireFrames;
    static Color[] mireTint;

    static (Color[] col, byte[] h) MireFrame(int i)
    {
        if (mireFrames == null)
        {
            var pal = ArapaimaArt.Palette.Select(Pal.C).ToArray();
            var murk = Pal.C("#2c3a2a");
            mireTint = pal.Select(c => Pix.Mix(c, murk, 0.62f)).ToArray();
            int n = ArapaimaArt.Width * ArapaimaArt.Height;
            mireFrames = new (Color[], byte[])[ArapaimaArt.Data.Length];
            for (int f = 0; f < mireFrames.Length; f++)
            {
                using var src = new MemoryStream(Convert.FromBase64String(ArapaimaArt.Data[f]));
                using var z = new System.IO.Compression.ZLibStream(src, System.IO.Compression.CompressionMode.Decompress);
                var raw = new byte[n * 2];
                int got = 0;
                while (got < raw.Length) { int k = z.Read(raw, got, raw.Length - got); if (k <= 0) break; got += k; }
                var col = new Color[n];
                for (int p = 0; p < n; p++) col[p] = raw[p] == 255 ? default : pal[raw[p]];
                mireFrames[f] = (col, raw[n..]);
            }
        }
        return mireFrames[i];
    }

    // The frame nearest a nose angle (degrees up), tail step and mouth.
    static int MireFrameIndex(float up, int phase, bool open)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < ArapaimaArt.Up.Length; i++)
        {
            if (ArapaimaArt.Open[i] != open) continue;
            float d = MathF.Abs(ArapaimaArt.Up[i] - up) + (open ? 0 : (ArapaimaArt.Phase[i] == phase % ArapaimaArt.Phases ? 0 : 0.5f));
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // Draws the arapaima with its middle at (x, y) on the water, facing dir (1 east, -1 west), its middle sunk `sink` rows
    // under the surface: pixels above the water are drawn as they are, the rest (unless dropUnder) as a dim shape under
    // the murky water.
    void DrawMireFish(float x, float y, int dir, float up, int phase, bool open, float sink, bool dropUnder = false)
    {
        if (OldArt)
        {
            // The fish log's hand-drawn picture, level, for the old look.
            if (sink > 8) return;
            y += MathF.Min(0, sink);   // in the air through a leap
            FishArt.Draw(pix, MireFish, (int)x - 20, (int)y - 6, 40, 12, dir < 0);
            return;
        }
        var (col, h) = MireFrame(MireFrameIndex(up, phase, open));
        int w = ArapaimaArt.Width, hh = ArapaimaArt.Height, ax = ArapaimaArt.AnchorX, ay = ArapaimaArt.AnchorY;
        int ox = (int)MathF.Round(x), oy = (int)MathF.Round(y + sink);
        var pal = ArapaimaArt.Palette;
        for (int j = 0; j < hh; j++)
            for (int i = 0; i < w; i++)
            {
                int p = j * w + i;
                var c = col[p];
                if (c.A == 0) continue;
                bool above = h[p] - ArapaimaArt.Lift > sink;
                int px = ox + (dir < 0 ? ax - i : i - ax), py = oy + j - ay;
                if (above) { pix.Rect(px, py, 1, 1, c); continue; }
                if (dropUnder) continue;
                // Under the water: only over water, dim and see-through.
                if (!InMirePool(px, py + 1.5f)) continue;
                var u = Pix.Mix(c, Pal.C("#2c3a2a"), 0.62f);
                u.A = 150;
                pix.Rect(px, py, 1, 1, u);
            }
    }

    // Under the surface: the shape gliding in the murky water with a cloud of stirred-up mud behind it, the bubbles
    // before it rises, and the V of its back as it bolts for the roots. Drawn with the water's things, before the bank.
    void DrawMireUnder(float t)
    {
        if (!MireShown) return;
        var m = mire;
        if (m == null) return;
        int dir = m.Vx < 0 ? -1 : 1, ph = (int)(t * (m.Ev is "roots" or "rub" ? 9 : 4));
        // The mud it stirs up: soft brown puffs drifting behind.
        for (int i = 0; i < 7; i++)
        {
            float age = (t * 0.6f + i / 7f) % 1, px = m.X - dir * (6 + age * 20) + MathF.Sin(i * 2.3f + t) * 3, py = m.Y + MathF.Cos(i * 1.7f) * 3;
            if (!InMirePool(px, py)) continue;
            float r = 2 + age * 4;
            for (int yy = (int)-r; yy <= r; yy++)
                for (int xx = (int)-r; xx <= r; xx++)
                    if ((xx * xx + yy * yy * 2) <= r * r && (xx + yy + i) % 2 == 0 && InMirePool(px + xx, py + yy))
                        pix.Rect(px + xx, py + yy, 1, 1, Pal.Rgba(92, 72, 44, 0.32f * (1 - age)));
        }
        if (m.Ev is "gulp" or "leap") return;   // above the water: DrawMireOver
        float sink = m.Ev switch { "rise" => 12 - 6 * (1 - m.EvT / RiseLen), "dive" => 6 + 8 * (1 - m.EvT / DiveLen), "roots" or "rub" => 3, _ => 9 };
        DrawMireFish(m.X, m.Y, dir, 0, ph, false, sink);
        if (m.Ev == "rise")
        {
            // Bubbles boiling up where it's coming, more and more.
            float k = 1 - m.EvT / RiseLen;
            for (int i = 0; i < 4 + (int)(k * 8); i++)
            {
                float a = (t * 1.7f + i * 0.31f) % 1;
                float bx = m.X + dir * 8 + MathF.Sin(i * 3.1f) * (3 + k * 4), by = m.Y - a * 4;
                pix.Rect(bx, by, 1, 1, Pal.Rgba(220, 235, 210, 0.85f * (1 - a)));
            }
            pix.Ring(m.X + dir * 8, m.Y + 1, 2 + k * 6, Pal.Rgba(200, 220, 190, 0.5f * k));
        }
        if (m.Ev is "roots" or "rub")
        {
            // Its back breaks the water: a V of ripples behind, and thrashing white water in the roots.
            for (int k = 2; k < 16; k++)
            {
                float al = 0.7f * (1 - k / 16f), ox = m.X - dir * k * 1.4f;
                pix.Rect(ox, m.Y - k * 0.45f, 1, 1, Pal.Rgba(225, 235, 215, al));
                pix.Rect(ox, m.Y + k * 0.45f, 1, 1, Pal.Rgba(225, 235, 215, al));
            }
            if (m.Ev == "rub")
                for (int i = 0; i < 8; i++)
                {
                    float a = (t * 3 + i * 0.13f) % 1;
                    pix.Rect(m.X + (i - 4) * 2.5f, m.Y - 1 - MathF.Abs(MathF.Sin(i * 1.9f)) * 6 * (1 - a), 1, 2, Pal.Rgba(230, 240, 225, 0.9f * (1 - a)));
                }
        }
    }

    // Above the water: the gulp (its head up through the surface, mouth wide, the water pouring off) and the leap.
    void DrawMireOver(float t)
    {
        if (!MireShown) return;
        var m = mire;
        if (m == null) return;
        int dir = m.Vx < 0 ? -1 : 1;
        if (m.Ev == "gulp")
        {
            float k = 1 - m.EvT / GulpLen, lift = MathF.Sin(MathF.Min(1, k * 3.5f) * MathF.PI * 0.5f) * (k > 0.8f ? 1 - (k - 0.8f) * 5 : 1);
            DrawMireFish(m.X, m.Y, dir, 35, 0, k < 0.8f, 9 - 11 * lift, dropUnder: false);
            // The ring of disturbed water round its head, and water streaming off.
            pix.Ring(m.X + dir * 6, m.Y + 1, 5 + k * 6, Pal.Rgba(225, 235, 215, 0.7f * (1 - k)));
            for (int i = 0; i < 6; i++)
            {
                float a = (t * 2.5f + i * 0.17f) % 1;
                pix.Rect(m.X + dir * (4 + i * 1.5f), m.Y - 10 * lift + a * 10, 1, 1, Pal.Rgba(210, 228, 210, 0.8f * (1 - a)));
            }
        }
        else if (m.Ev == "leap" && reel != null)
        {
            float k = Math.Clamp(1 - reel.Leap / reel.LeapLen, 0, 1);
            var (jx, jy) = MireLeapPos(k);
            float up = 50 * (1 - 2 * k);
            // Its shadow on the water, and the burst where it left and where it's coming down.
            pix.Rect(jx - 12, m.Y + 1, 24, 2, Pal.Rgba(10, 16, 10, 0.25f));

            if (k < 0.2f || k > 0.8f)
            {
                float ex = k < 0.5f ? m.LeapX0 : m.LeapX0 + m.LeapDir * m.LeapDist, q = k < 0.5f ? k / 0.2f : (k - 0.8f) / 0.2f;
                pix.Ring(ex, m.Y + 1, 2 + q * 9, Pal.Rgba(225, 235, 215, 0.85f * (1 - q * 0.6f)));
                for (int i = 0; i < 9; i++)
                    pix.Rect(ex + (i - 4) * 2.2f, m.Y - MathF.Abs(MathF.Sin(i * 1.7f)) * 8 * (1 - q) - q * 3, 1, 2, Pal.Rgba(225, 235, 215, 0.9f));
            }
            // Its middle is (m.Y - jy) above the water at m.Y: whatever is still under it near the ends isn't drawn.
            DrawMireFish(jx, m.Y, (int)m.LeapDir, up, (int)(t * 10), false, jy - m.Y, dropUnder: true);
        }
    }

    // Next to the reel while an event has it: what to do now, big.
    void DrawMireCue(MireFight m, float rx, Func<float, float> Y)
    {
        void Big(string s, Color c, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui700, 30), Y(at), FontKind.Ui700, 30, c);
        void Small(string s, float at) => Outlined(s, rx - Gfx.Measure(s, FontKind.Ui600, 18), Y(at), FontKind.Ui600, 18, Pal.C("#ffffff"));
        bool blink = (int)(time * 8) % 2 == 0;
        switch (m.Ev)
        {
            case "rise": Big("Bubbles...", Pal.C("#cfe3c0"), 130); Small("Get ready!", 168); break;
            case "gulp": Big(m.Struck ? "Got it!" : "PRESS!", m.Struck ? Pal.C("#7fd36b") : Pal.WithAlpha(Pal.C("#f3c25b"), blink ? 1 : 0.7f), 130); Small(Bind.Fix("<act> while it gulps"), 168); break;
            case "roots":
            {
                var rt = MireRoots()[m.RootI];
                int away = rt.bx < rt.wx ? 1 : 3;
                Big("ROOTS!", Pal.WithAlpha(Pal.C("#ff9a5a"), blink ? 1 : 0.7f), 124);
                // A big arrow the way to pull, with its key.
                float cx = rx - 46, cy = Y(196);
                Gfx.Rect(cx - 34, cy - 34, 68, 68, Pal.C("rgba(16,26,16,0.85)"), 8);
                DrawArrow(cx, cy, away, Pal.C("#f3c25b"));
                Small(Bind.Fix(away == 1 ? "Pull: <right>" : "Pull: <left>"), 238);
                break;
            }
            case "rub": Big("Tangled!", Pal.C("#ff9a8a"), 130); break;
        }
    }

    // Where it is through a leap (k 0 to 1): an arc across the pool.
    (float x, float y) MireLeapPos(float k) => (mire.LeapX0 + mire.LeapDir * mire.LeapDist * k, mire.Y - MathF.Sin(k * MathF.PI) * 30);

    // By night the moon lights the pool round it, more as it comes up, so a fight in the dark swamp can be seen.
    void LightMire()
    {
        if (!MireShown || mire is not { } m) return;
        LightHole(m.X, m.Y - 2, 30, 0.5f);
        if (m.Ev is "gulp" or "rise") LightHole(m.X + m.Vx * 8, m.Y - 6, 26, 0.75f);
        if (m.Ev == "leap" && reel != null) { var (jx, jy) = MireLeapPos(Math.Clamp(1 - reel.Leap / reel.LeapLen, 0, 1)); LightHole(jx, jy, 32, 0.8f); }
    }

    // After the dark: a pale sheen of moonlight on its wet back when it's out of the water.
    void GlowMire(float k)
    {
        if (!MireShown || mire is not { } m || k <= 0) return;
        if (m.Ev == "gulp") pix.Glow(m.X + m.Vx * 8, m.Y - 6, 14, Pal.Rgba(210, 235, 200, 0.16f * k));
        if (m.Ev == "leap" && reel != null) { var (jx, jy) = MireLeapPos(Math.Clamp(1 - reel.Leap / reel.LeapLen, 0, 1)); pix.Glow(jx, jy, 18, Pal.Rgba(210, 235, 200, 0.18f * k)); }
    }

    // How far the camera has eased over to frame the pool (0 to 1), and where it frames.
    float mireCamK;
    (float x, float y) MireCamera(float fx, float fy)
    {
        bool on = MireShown && scene == "world";
        mireCamK = Math.Clamp(mireCamK + (on ? 1 : -1) * Raylib.GetFrameTime() * 1.6f, 0, 1);
        if (mireCamK <= 0 || MirePool().Count == 0) return (fx, fy);
        float cx = (float)MirePool().Average(p => p.Item1) * T + 5, cy = (float)MirePool().Average(p => p.Item2) * T + 5;
        float tx = (player.X + cx) / 2, ty = (player.Y - 8 + cy - 34) / 2;
        float k = mireCamK * mireCamK * (3 - 2 * mireCamK);
        return (fx + (tx - fx) * k, fy + (ty - fy) * k);
    }
}
