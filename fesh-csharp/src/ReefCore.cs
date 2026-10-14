namespace Fesh;

// The Reef Run (1.30): a spearfishing time attack played against the clock and one of the three anglers. Forty-five seconds
// over a reef; fish shadows cross the water on paths worked out from the seed and the time alone (no stepping, no random
// numbers while playing), so every side gets exactly the same fish at the same moments on any machine and any frame rate.
// A spear takes a moment to land and a moment to pull back, and it scores the fish that is under it when it lands. No screen
// in here: the plan, where a fish is, what a throw hits and what an angler would have speared.
static class ReefRules
{
    public const int Version = 1, Fish = 24;
    public const float Duration = 45, FlightSecs = 0.18f, CooldownSecs = 0.6f, FieldW = 880, FieldH = 440, CountSecs = 3;
    public static readonly float[] Radius = { 11, 14, 19 };
    public static readonly int[] Points = { 1, 2, 4 };

    // One fish's pass: when it enters, how long it takes to cross, its lane and its wobble, which side it comes from, how big.
    public sealed record Pass(int Id, float T0, float Dur, float Y, float Amp, float Freq, float Phase, bool FromLeft, int Size);

    public static List<Pass> Plan(int seed)
    {
        var rng = new MatchRng(MatchRules.Mix(seed, Version, 61));
        var list = new List<Pass>();
        float span = Duration - 7;
        for (int i = 0; i < Fish; i++)
        {
            double sz = rng.NextDouble();
            int size = sz < 0.55 ? 0 : sz < 0.85 ? 1 : 2;
            float dur = size == 0 ? 5f + (float)rng.NextDouble() * 1.4f : size == 1 ? 6f + (float)rng.NextDouble() * 1.5f : 8.5f + (float)rng.NextDouble() * 1.5f;
            float t0 = 0.5f + i * span / Fish + ((float)rng.NextDouble() - 0.5f) * 0.8f;
            list.Add(new Pass(i, Math.Max(0.3f, t0), dur, 70 + (float)rng.NextDouble() * 300, 8 + (float)rng.NextDouble() * 22,
                1.0f + (float)rng.NextDouble() * 1.4f, (float)rng.NextDouble() * 6.283f, rng.NextDouble() < 0.5, size));
        }
        return list;
    }

    // Where a fish is at a time (seconds into the run), or null when it is not in the water then.
    public static (float x, float y)? PosAt(Pass p, float t)
    {
        float u = (t - p.T0) / p.Dur;
        if (u < 0 || u > 1) return null;
        float x = p.FromLeft ? -30 + (FieldW + 60) * u : FieldW + 30 - (FieldW + 60) * u;
        float y = p.Y + p.Amp * MathF.Sin(p.Freq * (t - p.T0) + p.Phase);
        return (x, y);
    }

    // A spear thrown at (ax, ay) at time `t` lands FlightSecs later and takes the nearest fish under it that has not been
    // speared. Returns that fish's id, or -1.
    public static int Hit(List<Pass> plan, ISet<int> speared, float ax, float ay, float t)
    {
        float tl = t + FlightSecs, best = float.MaxValue; int hit = -1;
        foreach (var p in plan)
        {
            if (speared.Contains(p.Id) || PosAt(p, tl) is not var (fx, fy) || (fx, fy) == default && PosAt(p, tl) == null) continue;
            float d = MathF.Sqrt((fx - ax) * (fx - ax) + (fy - ay) * (fy - ay));
            if (d <= Radius[p.Size] + 3 && d < best) { best = d; hit = p.Id; }
        }
        return hit;
    }

    // What an angler speared: each fish with their chance, at a moment while it was in the water. Nothing counts after the
    // whistle plus a spear's flight, the latest a spear of yours can land (the angler gets no longer than you do).
    public static (float t, int id, int pts)[] Angler(int seed, string rival)
    {
        float p = rival switch { "bess" => 0.34f, "marlo" => 0.5f, _ => 0.66f };
        var plan = Plan(seed);
        var rng = new MatchRng(MatchRules.Mix(seed, Version, 62, rival == "bess" ? 0 : rival == "marlo" ? 1 : 2));
        var list = new List<(float, int, int)>();
        foreach (var f in plan)
        {
            double roll = rng.NextDouble(), when = rng.NextDouble();
            if (roll < p && f.T0 + f.Dur * (0.2f + 0.6f * (float)when) <= Duration + FlightSecs) list.Add((f.T0 + f.Dur * (0.2f + 0.6f * (float)when), f.Id, Points[f.Size]));
        }
        return list.OrderBy(x => x.Item1).ToArray();
    }

    public static int AnglerScoreAt(int seed, string rival, float t) => Angler(seed, rival).Where(x => x.t <= t).Sum(x => x.pts);

    // More points win; the same points is a draw (the anglers have no throw count to compare). Positive: the first side.
    public static int Decide(int ptsA, int ptsB) => ptsA == ptsB ? 0 : ptsA > ptsB ? 1 : -1;
}
