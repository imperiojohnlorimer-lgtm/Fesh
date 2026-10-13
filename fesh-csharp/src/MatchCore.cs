using System.Text.Json;

namespace Fesh;

// Matches (1.30): short, fair, head-to-head runs with a clear winner. This file is the part with no screen in it: the
// seeded rules both sides share, the rival who plays them, how a winner is decided and the local leaderboard file.
// Nothing here reads the game's `rng`, the clock or the save, so the same seed gives the same fish on any machine and
// any frame rate (Match.cs runs it).

// A small generator of our own: System.Random's seeded sequence may change between .NET versions, and a score has to
// mean the same thing next year (and on a friend's PC over the LAN).
sealed class MatchRng
{
    ulong s;
    public MatchRng(int seed) { s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; }
    public ulong Next()
    {
        ulong z = s += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));
}

// One landed fish of a run: when (seconds into the match), what, its weight in hundredths of a kilo, and whether the hook
// was a perfect one. Both the player's run and a rival's are lists of these.
sealed class MatchEvent { public float t; public string fish; public int cg; public bool perfect; }

// How a side did: total weight in hundredths of a kilo (whole numbers, so no rounding argument), perfect hooks on landed
// fish, fish landed and casts made.
struct MatchScore
{
    public int Cg, Perfects, Catches, Casts;
    public string Kg => (Cg / 100.0).ToString("0.00");
}

// What the seed asks of one cast: the fish, its base size (before a perfect hook's bonus) and how long the bite takes.
readonly record struct CastPlan(string FishId, double Size, float Wait);

// A rival who fishes the same casts: how long a cast takes them, how often they land it and how often they hook perfectly.
sealed record GhostSkill(string Id, string Name, float Overhead, float Speed, float Win, float Perfect, string Blurb);

static class MatchRules
{
    // Bump this when the pool, the sizes or the scoring change: older scores keep their own number.
    public const int Version = 1;
    public const float DerbySecs = 180, CountdownSecs = 3;

    // The Derby Duel's water: everyday fish from Saltmere's lagoon, rocks, wreck and deep water, each with a fixed chance
    // so nothing in Data.cs can quietly change who wins. (Weight in the draw, not kilos.)
    public static readonly (string id, int w)[] Pool =
    {
        ("pond_perch", 5), ("rock_goby", 5), ("striped_wrasse", 4), ("barnacle_bass", 4),
        ("mud_carp", 3), ("lantern_squid", 3), ("rusty_grouper", 2), ("silver_tuna", 2)
    };

    public static readonly GhostSkill[] Rivals =
    {
        new("bess", "Old Bess", 4.5f, 1.30f, 0.62f, 0.10f, "Patient, slow to land a fish, rarely perfect."),
        new("marlo", "Marlo", 3.2f, 1.08f, 0.76f, 0.25f, "Steady hands. Beats most visitors."),
        new("ivy", "Captain Ivy", 2.4f, 0.92f, 0.90f, 0.45f, "Quick and sure. Hard to beat.")
    };
    public static GhostSkill RivalById(string id) => Rivals.FirstOrDefault(r => r.Id == id) ?? Rivals[1];

    public static int Mix(params int[] xs)
    {
        ulong h = 0x9E3779B97F4A7C15UL;
        foreach (int x in xs)
        {
            h ^= (ulong)(uint)x + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27;
        }
        return (int)(h ^ (h >> 32));
    }

    // The same for everyone on the same calendar date (the player's own date, in the ISO form).
    public static int DailySeed(DateTime d) => Mix(Version, d.Year, d.Month, d.Day, 7);
    public static string DailyLabel(DateTime d) => d.ToString("yyyy-MM-dd");

    // The nth cast of a match: its fish, its size and its wait are drawn from a stream of their own, so they don't
    // depend on what happened in the casts before (a lost fish, a late hook) or on how fast the frames come.
    public static CastPlan Plan(int seed, int cast)
    {
        var r = new MatchRng(Mix(Version, seed, cast, 1));
        double pick = r.NextDouble() * Pool.Sum(p => p.w);
        string id = Pool[^1].id;
        foreach (var (fid, w) in Pool)
        {
            if (pick < w) { id = fid; break; }
            pick -= w;
        }
        double u = r.NextDouble();
        double size = 0.55 + Math.Pow(u, 1.8) * 1.15;
        float wait = 1.6f + 2.0f * (float)r.NextDouble();
        return new CastPlan(id, size, wait);
    }

    // A fish's weight in hundredths of a kilo; a perfect hook makes it a tenth heavier, as in the adventure.
    public static int Centi(string fishId, double size, bool perfect) =>
        Math.Max(1, (int)Math.Round(Data.FishById[fishId].Kg * size * (perfect ? 1.1 : 1.0) * 100.0));

    // Who wins: the heavier catch, then more perfect hooks, and if that's equal too it's a draw. +1 first side, -1 second.
    public static (int winner, string reason) Decide(MatchScore a, MatchScore b)
    {
        if (a.Cg != b.Cg) return (a.Cg > b.Cg ? 1 : -1, "Heavier total catch");
        if (a.Perfects != b.Perfects) return (a.Perfects > b.Perfects ? 1 : -1, "Same weight: more perfect hooks");
        return (0, "Exactly level");
    }

    // A rival's run over the same casts. Their time per cast is the bite, a quick hook, the fight (longer for a harder
    // fish) and their own overhead; whether each fish is landed, and hooked perfectly, is drawn from the seed and the
    // cast, so the same rival over the same seed always fishes the same match.
    public static List<MatchEvent> GhostRun(int seed, GhostSkill sk, float duration)
    {
        var events = new List<MatchEvent>();
        float t = 0;
        int skillKey = sk.Id.Aggregate(17, (h, c) => h * 31 + c);
        for (int cast = 0; cast < 200; cast++)
        {
            var plan = Plan(seed, cast);
            var f = Data.FishById[plan.FishId];
            var r = new MatchRng(Mix(Version, seed, cast, 77, skillKey));
            float fight = (4.5f + 3.2f * f.Difficulty) * sk.Speed * (0.85f + 0.3f * (float)r.NextDouble());
            t += 1.6f + plan.Wait + 0.7f + fight + sk.Overhead;
            if (t >= duration) break;
            double winP = Math.Clamp(sk.Win - 0.12 * (f.Difficulty - 1), 0.2, 0.95);
            bool landed = r.NextDouble() < winP;
            bool perfect = r.NextDouble() < sk.Perfect;
            if (!landed) continue;
            events.Add(new MatchEvent { t = t, fish = plan.FishId, cg = Centi(plan.FishId, plan.Size, perfect), perfect = perfect });
        }
        return events;
    }

    // What a run had landed by a moment (a rival's running total on screen).
    public static MatchScore ScoreAt(List<MatchEvent> run, float t, int casts = 0)
    {
        var s = new MatchScore { Casts = casts };
        foreach (var e in run)
        {
            if (e.t > t) break;
            s.Cg += e.cg; s.Catches++; if (e.perfect) s.Perfects++;
        }
        return s;
    }
}

// ---------- The local leaderboard ----------
// One finished match, kept on this computer only. `id` is the match's own id, so a result is recorded once.
sealed class BoardEntry
{
    public string id, mode, label, rival, result, date;      // mode: derby, daily, sungka; result: win, loss, draw, forfeit
    public int seed, ruleset, score, perfects, catches, casts;
    public List<MatchEvent> log;                             // the landed fish, kept for the best run of each seed (a ghost to race)
}

sealed class BoardFile
{
    public string player = "", name = "";                    // a random id made once, and a display name from the lists below
    public int adj, noun;
    public List<BoardEntry> entries = new();
}

static class Leaderboards
{
    static readonly JsonSerializerOptions Opts = new() { IncludeFields = true, WriteIndented = true };
    public const int Keep = 120;                             // recent matches kept; the best of each kind is always kept
    static readonly string[] Adj = { "Swift", "Patient", "Salty", "Lucky", "Brave", "Calm", "Sunny", "Clever", "Steady", "Quiet", "Bold", "Merry" };
    static readonly string[] Noun = { "Bangus", "Tamban", "Tilapia", "Lapu-lapu", "Dalag", "Galunggong", "Tuna", "Wrasse", "Perch", "Grouper", "Squid", "Eel" };

    static BoardFile file;

    public static string Name(BoardFile f) => $"{Adj[((f.adj % Adj.Length) + Adj.Length) % Adj.Length]} {Noun[((f.noun % Noun.Length) + Noun.Length) % Noun.Length]}";
    public static int NameChoices => Adj.Length * Noun.Length;
    // A name from the two lists by index, so a stranger's name is never free text.
    public static string NameOf(int adj, int noun) => $"{Adj[((adj % Adj.Length) + Adj.Length) % Adj.Length]} {Noun[((noun % Noun.Length) + Noun.Length) % Noun.Length]}";
    public static string AdjectiveAt(int i) => Adj[((i % Adj.Length) + Adj.Length) % Adj.Length];
    public static string NounAt(int i) => Noun[((i % Noun.Length) + Noun.Length) % Noun.Length];
    public static int Adjectives => Adj.Length;
    public static int Nouns => Noun.Length;

    // Loaded on first use. A file that can't be read is set aside (.bad) and the board starts empty, so a damaged file
    // can never stop a match or the game.
    public static BoardFile Board()
    {
        if (file != null) return file;
        string path = SaveFile.BoardPath;
        try
        {
            if (File.Exists(path))
            {
                file = JsonSerializer.Deserialize<BoardFile>(File.ReadAllText(path), Opts);
                if (file == null) throw new JsonException("empty");
            }
        }
        catch (Exception)
        {
            try { File.Move(path, path + ".bad", true); } catch (Exception) { /* leave it */ }
            file = null;
        }
        file ??= new BoardFile();
        // A file that parses can still hold nonsense (null entries, null fish): drop what can't be used.
        file.entries = (file.entries ?? new()).Where(e => e != null && !string.IsNullOrEmpty(e.mode)).ToList();
        foreach (var e in file.entries)
        {
            e.id ??= Guid.NewGuid().ToString("N");
            e.result ??= "solo";
            e.date ??= "";
            e.log = e.log?.Where(l => l != null && !string.IsNullOrEmpty(l.fish)).ToList();
        }
        if (string.IsNullOrEmpty(file.player))
        {
            file.player = Guid.NewGuid().ToString("N");
            var r = new Random();
            file.adj = r.Next(Adj.Length); file.noun = r.Next(Noun.Length);
        }
        file.name = Name(file);
        return file;
    }

    // For tests: forget what's in memory so the next read comes from the file again.
    public static void Reset() => file = null;

    public static bool Write()
    {
        try
        {
            Directory.CreateDirectory(SaveFile.Dir);
            string path = SaveFile.BoardPath, tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Board(), Opts));
            File.Move(tmp, path, true);
            return true;
        }
        catch (Exception) { return false; }
    }

    public static void SetName(int adj, int noun)
    {
        var f = Board();
        f.adj = adj; f.noun = noun; f.name = Name(f);
        Write();
    }

    // Adds a finished match once (by its id) and keeps the file small: the newest matches, plus the best run of each
    // kind of match (with its fish log), however old.
    public static bool Add(BoardEntry e)
    {
        var f = Board();
        if (f.entries.Any(x => x.id == e.id)) return false;
        f.entries.Add(e);
        // Kept: the newest matches and the ten best of each kind. A fish log (the ghost "Your best") is kept only for the
        // best run of each of the last thirty daily challenges; every other log is dropped.
        var recent = f.entries.OrderByDescending(x => x.date).Take(Keep).ToHashSet();
        var top = f.entries.Where(x => x.result != "forfeit").GroupBy(x => x.mode)
            .SelectMany(g => Rank(g.Key, g).Take(10)).ToHashSet();
        var ghosts = f.entries.Where(x => x.mode == "daily" && x.result != "forfeit").GroupBy(x => (x.seed, x.ruleset))
            .Select(g => g.OrderByDescending(x => x.score).ThenByDescending(x => x.perfects).First())
            .OrderByDescending(x => x.date).Take(30).ToHashSet();
        foreach (var x in f.entries.Where(x => !ghosts.Contains(x))) x.log = null;
        f.entries = f.entries.Where(x => recent.Contains(x) || top.Contains(x) || ghosts.Contains(x)).OrderBy(x => x.date).ToList();
        Write();
        return true;
    }

    // Best first: the heaviest, most points or most right for most kinds; the fastest heat for a regatta (a heat nobody finished has no time).
    static IOrderedEnumerable<BoardEntry> Rank(string mode, IEnumerable<BoardEntry> q) =>
        mode == "regatta" ? q.Where(x => x.score > 0).OrderBy(x => x.score).ThenByDescending(x => x.date)
            : q.OrderByDescending(x => x.score).ThenByDescending(x => x.perfects).ThenByDescending(x => x.date);

    static IEnumerable<BoardEntry> Played(string mode) => Board().entries.Where(x => x.mode == mode && x.result != "forfeit");

    // Your best runs first: the heaviest, then the most perfect hooks, then the newest.
    public static List<BoardEntry> Best(string mode, int n) =>
        Rank(mode, Played(mode).Where(x => x.ruleset == MatchRules.Version)).Take(n).ToList();

    public static List<BoardEntry> Recent(string mode, int n) =>
        Board().entries.Where(x => x.mode == mode).OrderByDescending(x => x.date).Take(n).ToList();

    // Your best run on one seed (the daily challenge of one date), with its fish log: the ghost "Your best".
    public static BoardEntry BestOn(string mode, int seed) =>
        Played(mode).Where(x => x.seed == seed && x.ruleset == MatchRules.Version).OrderByDescending(x => x.score).ThenByDescending(x => x.perfects).FirstOrDefault();

    // Matches played, won, lost and drawn. Leaving a match is a loss, so it can't keep a record clean (it is never a best score).
    public static (int played, int won, int lost, int drawn) Record(string mode)
    {
        var l = Board().entries.Where(x => x.mode == mode).ToList();
        return (l.Count, l.Count(x => x.result == "win"), l.Count(x => x.result is "loss" or "forfeit"), l.Count(x => x.result == "draw"));
    }
}
