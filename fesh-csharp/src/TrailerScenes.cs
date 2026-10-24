#if DEBUG
using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The trailer's shots (TrailerShots.cs records them). Each one is played the way a player would: walking up with the
// stick, casting by holding the act key, hooking and reeling, dodging and striking. Only the waiting is cut short.
partial class Game
{
    IEnumerable<(string, Func<IEnumerable<int>>)> TrailerShots()
    {
        yield return ("title", ShotTitle);
        yield return ("fishing", ShotFishing);
        yield return ("explore", ShotExplore);
        yield return ("bosscard", () => ShotCard("BOSS FIGHTS", "Legends that fight back"));
        yield return ("tidemane", ShotTidemane);
        yield return ("tidemane2", ShotTidemaneSand);
        yield return ("platejaw", ShotPlatejaw);
        yield return ("ironbill", ShotIronbill);
        yield return ("mire", ShotMire);
        yield return ("ray", ShotRay);
        yield return ("tarpon", ShotTarpon);
        yield return ("lungfish", ShotLungfish);
        yield return ("whiskers", ShotWhiskers);
        yield return ("storycard", () => ShotCard("THE STORIES", "Five mysteries, told the way the islands tell them"));
        yield return ("glowgill", ShotGlowgill);
        yield return ("sea", ShotRestlessSea);
        yield return ("sea2", ShotRestlessSeaWave);
        yield return ("baga", ShotBaga);
        yield return ("baga2", ShotBagaAsh);
        yield return ("marilaya", ShotMarilaya);
        yield return ("bakunawa", ShotBakunawa);
        yield return ("bakunawa2", ShotBakunawaEnd);
        yield return ("multicard", () => ShotCard("PLAY TOGETHER", "LAN matches, local leaderboards"));
        yield return ("dock", ShotDock);
        yield return ("duel", ShotDuel);
        yield return ("regatta", ShotRegatta);
        yield return ("sungka", ShotSungka);
        yield return ("quiz", ShotQuiz);
        yield return ("end", ShotEnd);
    }

    IEnumerable<int> ShotCard(string big, string small)
    {
        cardBig = big; cardSmall = small;
        Roll(2.0f);
        foreach (var _ in Frames(80)) yield return 0;
    }

    /* ---------- Moving about like a player ---------- */
    // The way on foot (or riding, or swimming) from where you are to a point, on a 4 px grid, eight ways.
    List<(float x, float y)> FootPath(float tx, float ty, Func<float, float, bool> ok, int reach = 90)
    {
        const int S = 4;
        var start = ((int)MathF.Round(player.X / S), (int)MathF.Round(player.Y / S));
        var goal = ((int)MathF.Round(tx / S), (int)MathF.Round(ty / S));
        var dist = new Dictionary<(int, int), float> { [start] = 0 };
        var prev = new Dictionary<(int, int), (int, int)>();
        var pq = new PriorityQueue<(int x, int y), float>();
        pq.Enqueue(start, 0);
        int minX = Math.Min(start.Item1, goal.Item1) - reach, maxX = Math.Max(start.Item1, goal.Item1) + reach;
        int minY = Math.Min(start.Item2, goal.Item2) - reach, maxY = Math.Max(start.Item2, goal.Item2) + reach;
        (int, int) best = start; float bestD = float.MaxValue;
        while (pq.TryDequeue(out var cur, out float d))
        {
            float h = MathF.Abs(cur.x - goal.Item1) + MathF.Abs(cur.y - goal.Item2);
            if (h < bestD) { bestD = h; best = cur; }
            if (cur == goal) break;
            if (d > dist[cur]) continue;
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var n = (cur.x + ox, cur.y + oy);
                if (n.Item1 < minX || n.Item1 > maxX || n.Item2 < minY || n.Item2 > maxY) continue;
                float nd = d + (ox != 0 && oy != 0 ? 1.414f : 1);
                if (nd >= dist.GetValueOrDefault(n, float.MaxValue) || !ok(n.Item1 * S, n.Item2 * S)) continue;
                dist[n] = nd; prev[n] = cur; pq.Enqueue(n, nd);
            }
        }
        var path = new List<(float x, float y)>();
        for (var p = best; p != start; p = prev[p]) path.Add((p.Item1 * S, p.Item2 * S));
        path.Reverse();
        if (best == goal) path.Add((tx, ty));
        return path;
    }

    // Walks there with the stick, the way a player would (also riding or swimming, whatever moving means right now).
    IEnumerable<int> WalkTo(float tx, float ty, int maxFrames = 600, float push = 1, Func<float, float, bool> ok = null)
    {
        ok ??= (x, y) => CanStand(x, y);
        var path = FootPath(tx, ty, ok);
        int i = 0;
        for (int f = 0; f < maxFrames && Dist(player.X, player.Y, tx, ty) > 2.5f && mode == "play"; f++)
        {
            while (i < path.Count - 1 && Dist(player.X, player.Y, path[i].x, path[i].y) < 5) i++;
            var (wx, wy) = path.Count > 0 ? path[i] : (tx, ty);
            float dx = wx - player.X, dy = wy - player.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            Inp.ScriptStick = new Vector2(dx / d * push, dy / d * push);
            yield return 0;
        }
        Inp.ScriptStick = null;
        if (Dist(player.X, player.Y, tx, ty) < 3 && ok(tx, ty)) { player.X = tx; player.Y = ty; }
    }

    // A place on foot about d px of walking from a point, to start a walk from.
    (float x, float y) WalkStart(float tx, float ty, float d, Func<float, float, bool> ok = null, float prefX = 0, float prefY = 0)
    {
        ok ??= (x, y) => CanStand(x, y);
        const int S = 4;
        var start = ((int)MathF.Round(tx / S), (int)MathF.Round(ty / S));
        var dist = new Dictionary<(int, int), float> { [start] = 0 };
        var q = new Queue<(int x, int y)>(); q.Enqueue(start);
        (float x, float y) best = (tx, ty); float bestScore = float.MaxValue;
        while (q.Count > 0)
        {
            var c = q.Dequeue(); float cd = dist[c];
            float score = MathF.Abs(cd * S - d) - (prefX * (c.x * S - tx) + prefY * (c.y * S - ty)) * 0.5f;
            if (score < bestScore) { bestScore = score; best = (c.x * S, c.y * S); }
            if (cd * S > d + 20) continue;
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = (c.x + ox, c.y + oy);
                if (dist.ContainsKey(n) || !ok(n.Item1 * S, n.Item2 * S)) continue;
                dist[n] = cd + 1; q.Enqueue(n);
            }
        }
        return best;
    }

    // The standable point nearest a spot, within its reach (no need to get there from Saltmere on foot).
    (float x, float y) StandBy(string spotId, float keep = 6)
    {
        var s = Data.SpotById[spotId];
        var (sx, sy) = SpotPos(s);
        return Enumerable.Range(0, 61 * 61).Select(i => (x: sx - 120 + i % 61 * 4f, y: sy - 120 + i / 61 * 4f))
            .Where(p => CanStand(p.x, p.y) && Dist(p.x, p.y, sx, sy) < s.R - keep).OrderBy(p => Dist(p.x, p.y, sx, sy)).First();
    }

    // Finds where to stand so that the act key really fishes this spot (not a carving, a person or a pot beside it),
    // trying the nearest places first. Leaves the answer in standX/standY.
    float standX, standY;
    IEnumerable<int> TakeStand(string spotId, float keep = 6, float avoidX = -999, float avoidY = -999)
    {
        var s = Data.SpotById[spotId];
        var (sx, sy) = SpotPos(s);
        var cands = Enumerable.Range(0, 61 * 61).Select(i => (x: sx - 120 + i % 61 * 4f, y: sy - 120 + i / 61 * 4f))
            .Where(p => CanStand(p.x, p.y) && Dist(p.x, p.y, sx, sy) < s.R - keep && Dist(p.x, p.y, avoidX, avoidY) > 32).OrderBy(p => Dist(p.x, p.y, sx, sy)).ToList();
        (standX, standY) = cands[0];
        foreach (var (x, y) in cands)
        {
            player.X = x; player.Y = y; FaceToSpot(spotId);
            yield return 0; yield return 0;
            if (target?.Type == "spot" && target.Id == spotId) { (standX, standY) = (x, y); yield break; }
        }
    }

    void FaceToSpot(string spotId)
    {
        var (sx, sy) = SpotPos(Data.SpotById[spotId]);
        float dx = sx - player.X, dy = sy - player.Y;
        player.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
    }

    // Holds the act key to load a cast, lets go, waits (shortened) for the bite and hooks it. The roll can be swapped
    // for a particular fish (the bosses that need their night, moon or bait are set up already, but this is quicker).
    IEnumerable<int> CastAndHook(int charge = 20, string force = null, bool perfect = true, Catchable roll = null)
    {
        Inp.Hold(KeyboardKey.E, true);
        foreach (var _ in Frames(charge)) yield return 0;
        Inp.Hold(KeyboardKey.E, false);
        for (int i = 0; i < 400 && mode != "bite"; i++)
        {
            if (fish != null && mode == "waiting" && fish.Timer > 2.0f) fish.Timer = 2.0f;
            yield return 0;
        }
        if (force != null && fish != null)
        {
            var f = Data.FishById[force];
            fish.Roll = new Catchable { Id = force, Name = f.Name, Difficulty = f.Difficulty, Rare = f.Rare };
        }
        if (roll != null && fish != null) fish.Roll = roll;
        foreach (var _ in Frames(perfect ? 1 : 9)) yield return 0;   // a perfect hook: as it goes under
        Inp.Tap(KeyboardKey.E);
        foreach (var _ in Frames(2)) yield return 0;
    }

    // Reels the way a good player does: holds to lift the green bar under the fish and lets it fall when it's above, lets
    // go when a runner runs, pumps a bottom-hugger in short taps, and presses as a leaping fish's marker crosses the gold.
    // 'boost' speeds the fight up a little, but only while the fish is inside the bar, so the bar and progress agree.
    IEnumerable<int> Reel(int frames, float boost = 0, Action each = null)
    {
        float held = 0, rest = 0;
        Note($"reel start: {reel?.Roll?.Id} progress {reel?.Progress ?? -1:0.00} style {reel?.Style}");
        int inside = 0, total = 0;
        for (int i = 0; i < frames && mode == "reeling" && reel != null; i++)
        {
            var r = reel;
            bool hold;
            if (r.Leap > 0)
            {
                hold = false;
                if (!r.LeapDone && r.LeapMark >= 0.66f && r.LeapMark <= 0.8f) Inp.Tap(KeyboardKey.E);
            }
            else if (r.Style == "runner" && r.Running > 0) hold = false;
            else
            {
                float c = r.ZoneY + r.ZoneH / 2 + r.ZoneV * 0.22f;
                hold = c > r.FishY + 2;
                if (r.Style == "bottom")
                {
                    // Never long enough to dig it in; a pump whenever it's inside.
                    if (rest > 0) { rest -= FixedDt; hold = false; }
                    else if (hold && held > 0.4f) { rest = 0.15f; hold = false; }
                    if (r.Inside && i % 16 == 0) Inp.Tap(KeyboardKey.E);
                }
            }
            held = hold ? held + FixedDt : 0;
            Inp.Hold(KeyboardKey.E, hold);
            total++; if (r.Inside) inside++;
            if (r.Inside && boost > 0) r.Progress = Math.Min(0.995f, r.Progress + boost * FixedDt);
            each?.Invoke();
            yield return 0;
        }
        Inp.Hold(KeyboardKey.E, false);
        Note($"reel: fish inside the bar {inside} of {total} frames, ended in {mode}, progress {reel?.Progress ?? -1:0.00}, caught {state.commons.Sum(k => k.Value)}");
    }

    /* ---------- The shots ---------- */
    // The title's own opening: from the seabed up to the letters.
    IEnumerable<int> ShotTitle()
    {
        if (mode != "title") { QuitToTitle(); yield return 2; }
        titleView = "main"; introDone = false; introT = 1.0f;
        yield return 0;
        Roll(6.4f);
        foreach (var _ in Frames(240)) yield return 0;
    }

    // A morning at Saltmere: out of the hut, along to the lagoon, a cast and a catch.
    IEnumerable<int> ShotFishing()
    {
        TrailerGame(day: 3);

        state.clock = 7 * 60 + 20;
        foreach (var _ in TakeStand("lagoon")) yield return 0;
        if (target?.Type != "spot") Check("fishing: no stand found", false);
        float sx = standX, sy = standY;
        (player.X, player.Y) = WalkStart(sx, sy, 70, prefX: 1);
        foreach (var _ in Frames(6)) yield return 0;
        Roll(9f, null, "Pulo ng Asin", "A fishing mystery on a Philippine-inspired island");
        foreach (var _ in WalkTo(sx, sy, 200)) yield return 0;
        FaceToSpot("lagoon");
        foreach (var _ in CastAndHook(22, "pond_perch", false)) yield return 0;
        foreach (var _ in Reel(420)) yield return 0;
        foreach (var _ in Frames(30)) yield return 0;
        if (mode == "catch") Inp.Tap(KeyboardKey.E);
        foreach (var _ in Frames(60)) yield return 0;

    }

    // Riding Tidemane along the atoll, then the banca under its big sail.
    IEnumerable<int> ShotExplore()
    {
        TrailerGame(day: 4);
        state.clock = 17 * 60 + 40;
        state.tamed = true; state.hinted["visitedAtoll"] = true; state.hinted["starwell"] = true; state.hinted["carving"] = true;
        // A long beach on the atoll's west side, galloping south.
        var (ax, ay) = StandableNear(StarwellX - 150, StarwellY - 40);
        player.X = ax; player.Y = ay; state.mountX = ax; state.mountY = ay; state.riding = true;
        foreach (var _ in Frames(10)) yield return 0;
        Roll(4f, null, "Ride, sail and explore", "Nine islands and the open sea between them");
        Inp.ScriptStick = new Vector2(0.15f, 1);
        foreach (var _ in Frames(40)) yield return 0;
        Inp.ScriptStick = new Vector2(1, 0.2f);
        foreach (var _ in Frames(60)) yield return 0;
        Inp.ScriptStick = new Vector2(0.4f, -1);
        foreach (var _ in Frames(60)) yield return 0;
        Inp.ScriptStick = null;
    }

    // Tidemane: a coconut cast into the Starwell at night, and what comes out of it.
    IEnumerable<int> ShotTidemane()
    {
        TrailerGame(night: true);
        state.hinted["visitedAtoll"] = true; state.hinted["starwell"] = true; state.hinted["carving"] = true;
        state.inv["iron_sword"] = 1; state.inv["coconut"] = 5; state.tackle["bait"] = "coconut";
        foreach (var _ in TakeStand("starwell", avoidX: CarvingX, avoidY: CarvingY)) yield return 0;
        if (target?.Type != "spot") Check("tidemane: no stand found", false);
        float sx = standX, sy = standY;
        (player.X, player.Y) = WalkStart(sx, sy, 50, prefX: -1);
        foreach (var _ in Frames(6)) yield return 0;
        Roll(6f, "BOSS", "Tidemane", "The hippocamp of the Starwell");
        foreach (var _ in WalkTo(sx, sy, 120)) yield return 0;
        FaceToSpot("starwell");
        foreach (var _ in CastAndHook(18)) yield return 0;
        foreach (var _ in Reel(900)) yield return 0;
    }

    // ...and, a cut later, out of the water and on the sand: dodging its charges and striking while it's winded.
    IEnumerable<int> ShotTidemaneSand()
    {
        foreach (var _ in Frames(1)) yield return 0;
        ReleaseKeys();
        fish = null; reel = null; mode = "play";
        (player.X, player.Y) = StandableNear(StarwellX - 40, StarwellY + 30); player.Face = "right";
        foreach (var _ in Frames(3)) yield return 0;
        Roll(8f);
        BossBreach();
        for (int i = 0; i < 900 && boss != null; i++)
        {
            state.hp = Math.Max(state.hp, 60);
            DuelStep(boss.X, boss.Y, boss.Phase is "rear" or "charge" or "stompRear" or "stomp", target?.Type == "boss", 44, boss.Phase == "winded", StarwellX, StarwellY, 62);
            yield return 0;
        }
    }

    // Fighting on foot the way a person does: circling at a distance and watching, running across its line when it
    // comes, rushing in while it's open, and steering round palms and pillars rather than pushing into them.
    Vector2 duelStick, duelLast; int duelOrbit = 1, duelStill; float duelSwap = 3, duelPause;
    readonly Random duelRng = new(5);

    void DuelStep(float ex, float ey, bool danger, bool canStrike, float keep = 40, bool open = false, float cx = 0, float cy = 0, float cr = 0)
    {
        float dx = ex - player.X, dy = ey - player.Y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        Vector2 to = new(dx / d, dy / d), tangent = new(-to.Y * duelOrbit, to.X * duelOrbit);
        if (canStrike)
        {
            duelStick *= 0.5f;
            Inp.ScriptStick = duelStick.Length() > 0.15f ? duelStick : null;
            player.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
            if (duelRng.Next(9) == 0) Inp.Tap(KeyboardKey.E);
            return;
        }
        Vector2 want;
        if (danger) want = tangent + -to * 0.45f;
        else if (open) want = to;
        else
        {
            duelSwap -= FixedDt;
            if (duelSwap <= 0) { duelSwap = 2.5f + (float)duelRng.NextDouble() * 3; if (duelRng.Next(3) == 0) duelOrbit = -duelOrbit; else duelPause = 0.35f + (float)duelRng.NextDouble() * 0.4f; }
            if (duelPause > 0) { duelPause -= FixedDt; want = Vector2.Zero; }
            else want = tangent * 0.75f + to * Math.Clamp((d - keep) / 18f, -0.8f, 0.8f);
        }
        // Keep to the open ground of the arena (the sand inside the palms, the cave floor round the pool).
        if (cr > 0)
        {
            float ox = cx - player.X, oy = cy - player.Y, od = MathF.Sqrt(ox * ox + oy * oy);
            if (od > cr * 0.7f) want += new Vector2(ox / od, oy / od) * Math.Clamp((od - cr * 0.7f) / (cr * 0.3f), 0, 1.5f);
        }
                // Round whatever's in the way: the first direction near the one wanted with ground a few steps ahead.
        if (want.Length() > 0.05f)
        {
            float a0 = MathF.Atan2(want.Y, want.X), len = MathF.Min(1, want.Length());
            bool found = false;
            foreach (float off in new[] { 0f, 0.6f, -0.6f, 1.2f, -1.2f, 1.9f, -1.9f })
            {
                float a = a0 + off * duelOrbit;
                Vector2 dir = new(MathF.Cos(a), MathF.Sin(a));
                if (CanStand(player.X + dir.X * 6, player.Y + dir.Y * 6) && CanStand(player.X + dir.X * 12, player.Y + dir.Y * 12)) { want = dir * len; found = true; break; }
            }
            if (!found) { duelOrbit = -duelOrbit; want = -want; }
        }
        duelStick = Vector2.Lerp(duelStick, want, 0.22f);
        Inp.ScriptStick = duelStick.Length() > 0.08f ? duelStick : null;
        // Not getting anywhere: go round the other way.
        // (Over a third of a second: sliding along a trunk moves you a little, but not far.)
        if (duelStick.Length() > 0.4f) { if (++duelStill >= 10) { if (Vector2.Distance(duelLast, new Vector2(player.X, player.Y)) < 5) duelOrbit = -duelOrbit; duelStill = 0; duelLast = new Vector2(player.X, player.Y); } }
        else { duelStill = 0; duelLast = new Vector2(player.X, player.Y); }
    }
}
#endif
