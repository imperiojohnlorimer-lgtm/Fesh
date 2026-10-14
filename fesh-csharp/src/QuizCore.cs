namespace Fesh;

// The Sea School Duel (1.30): eight questions from a fixed tournament set, the same eight in the same order for both
// sides; the most right wins, the faster total time breaks a tie. No screen and no game state in here: the rules, the
// bots that play alone and the two-player session over any ILink (a routed link through the Tournament Dock).
//
// The questions are a set of their own (not what you have caught or learned), so nothing in a save changes who wins, and
// a seed fixes which eight and the order of each one's four answers. The receiver of an answer works out whether it was
// right from the seed, so a made-up "right" is impossible; what can't be checked is how long someone really took to read
// (a time is held to a floor and to the real clock, nothing more), and the game says so: the boards are local and for fun.
static class QuizRules
{
    public const int Count = 8, Version = 1;
    public const int AnswerMs = 15000, MinMs = 350;

    public sealed record Q(string Prompt, string[] Options);   // Options[0] is the right one, as authored

    static Q M(string prompt, string right, string a, string b, string c) => new(prompt, new[] { right, a, b, c });

    public static readonly Q[] Bank =
    {
        M("Which monsoon brings heavy rain to the western Philippines from about June to September?", "Habagat, the southwest monsoon", "Amihan, the northeast monsoon", "The doldrums", "The jet stream"),
        M("Which is the cooler, drier northeast monsoon?", "Amihan", "Habagat", "Bagyo", "Tag-ulan"),
        M("Most coasts with two tides a day have a high tide about every...", "12 hours", "6 hours", "24 hours", "3 days"),
        M("Spring tides, the biggest tide ranges, come near...", "the full moon and the new moon", "the half moons", "the longest day of the year", "the strongest wind"),
        M("Neap tides, the smallest tide ranges, come near...", "the half moons", "the full moon and the new moon", "the shortest day of the year", "the first rain"),
        M("Each day a tide comes about how much later than the day before?", "About 50 minutes", "About 5 minutes", "About 5 hours", "At the very same time"),
        M("After a strong earthquake near the coast, where should you go?", "To high ground, away from the shore", "Down to the beach to watch the sea", "Out to sea in a boat", "Under the nearest palm tree"),
        M("The sea suddenly draws far back from the shore. What might be coming?", "A tsunami, so go uphill at once", "A low tide, nothing to worry about", "A school of fish", "A rainy day"),
        M("A tsunami is...", "A series of waves, and the first is not always the biggest", "One single wave", "A wave made only by wind", "A very high tide"),
        M("When can you leave high ground after a tsunami warning?", "When officials say it is safe", "As soon as the first wave passes", "After ten minutes", "When the sea looks calm"),
        M("PHIVOLCS gives Mayon Volcano an alert level from 0 up to...", "5", "3", "7", "10"),
        M("Which agency issues volcano alert levels in the Philippines?", "PHIVOLCS", "PAGASA", "BFAR", "DepEd"),
        M("Which agency forecasts weather and tropical cyclones in the Philippines?", "PAGASA", "PHIVOLCS", "BFAR", "DepEd"),
        M("Which agency looks after the country's fisheries?", "BFAR", "PAGASA", "PHIVOLCS", "DOH"),
        M("During heavy ashfall, the safest place is...", "Inside, with doors and windows shut", "Outside, to watch it", "On a hilltop", "In a river"),
        M("A lahar is...", "A flow of volcanic ash, rock and water, like wet concrete", "A kind of lava fountain", "A cloud of steam", "A deep-sea current"),
        M("A crab carrying eggs (berried) should be...", "Let go back into the sea", "Kept, because she is big", "Sold at once", "Moved to a tank"),
        M("The Philippine minimum size for keeping a blue swimming crab (alimasag) is a shell about...", "10.2 cm wide", "2 cm wide", "5 cm wide", "20 cm wide"),
        M("Bangus is the Filipino name for...", "Milkfish", "Mackerel", "Tuna", "Catfish"),
        M("Galunggong is a...", "Round scad", "Grouper", "Eel", "Ray"),
        M("Tilapia live mostly in...", "Fresh or brackish water", "The deep ocean", "Coral crevices only", "Cold mountain springs only"),
        M("Which of these is not a fish?", "Crab", "Milkfish", "Tuna", "Grouper"),
        M("Sharks and rays have a skeleton made of...", "Cartilage", "Hollow bone", "Shell", "Coral"),
        M("In the scientific name Chanos chanos, the first word is the...", "Genus", "Family", "Order", "Phylum"),
        M("Mangroves help a coast by...", "Holding the shore and sheltering young fish", "Making the water salty", "Stopping all tides", "Warming the sea"),
        M("A coral reef is built by...", "Tiny animals called coral polyps", "Seaweed", "Fish bones", "Volcanoes"),
        M("Carrageenan, used in food, comes from...", "Red seaweed", "Squid ink", "Fish scales", "Sea sand"),
        M("The Philippines lies on the...", "Pacific Ring of Fire", "Atlantic Ridge", "Arctic Circle", "Great Rift Valley"),
        M("Sea salt can be made by...", "Letting the sun dry seawater in shallow beds", "Melting mountain ice", "Mining it from clouds", "Boiling river water"),
        M("Daing, dried fish, keeps well because...", "Salt and drying take water away, so it spoils slowly", "The sun freezes it", "Sunlight makes it fresh again", "The wind makes it sweet"),
        M("Fish take oxygen from the water with their...", "Gills", "Fins", "Scales", "Whiskers"),
        M("A fish's lateral line senses...", "Movement and pressure in the water", "Colour", "Sound above the water", "The temperature of the air"),
        M("You can estimate a fish's age from rings on its...", "Scales or ear stones", "Eyes", "Fin colours", "Tail length"),
        M("In Visayan stories, who swallows the moon?", "Bakunawa, a great sea serpent", "A giant crab", "A flying fish", "The wind spirit"),
        M("About how long does the moon take to go through all its phases?", "About a month", "About a day", "About a week", "About a year"),
        M("Sea turtles (pawikan) in the Philippines are...", "Protected by law: look, don't catch", "Free to catch", "Fish that lay eggs in rivers", "Only found in zoos"),
        M("A bantay dagat is...", "A community watch that guards the sea", "A kind of boat", "A fishing net", "A sea storm"),
        M("A closed fishing season helps because...", "Fish can breed and stocks recover", "Boats need repairs", "Fish prefer winter", "Nets get tangled"),
        M("Which zone of the ocean has no sunlight at all?", "The midnight zone", "The sunlit zone", "The twilight zone", "The surface"),
        M("Brackish water is...", "A mix of fresh and salt water", "Very cold water", "Only rain", "Water with no fish"),
        M("Which fish has a long bill?", "Marlin", "Sardine", "Tilapia", "Goby"),
        M("Fireflies flash mostly to...", "Find mates", "Light the way home", "Scare fish", "Keep warm"),
        M("Most of the water on Earth is...", "Salt water in the oceans", "Fresh water in rivers", "Ice", "In clouds"),
        M("Indoors in an earthquake, you should...", "Drop, cover and hold on", "Run to the top floor", "Hide under a window", "Stand by the stairs"),
        M("A herbivore eats...", "Plants and seaweed", "Other animals", "Only insects", "Rocks"),
    };

    // The eight questions of a seed: indices into Bank, and for each the order its four answers are shown in (shown[i] is
    // the authored index of the answer at place i; authored 0 is the right one).
    public static List<(int q, int[] shown)> Plan(int seed)
    {
        var rng = new MatchRng(MatchRules.Mix(seed, Version, 31));
        var idx = Enumerable.Range(0, Bank.Length).ToList();
        for (int i = idx.Count - 1; i > 0; i--) { int j = (int)(rng.NextDouble() * (i + 1)); (idx[i], idx[j]) = (idx[j], idx[i]); }
        var plan = new List<(int, int[])>();
        for (int k = 0; k < Count; k++)
        {
            var o = new MatchRng(MatchRules.Mix(seed, k, 32));
            var sh = new[] { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = (int)(o.NextDouble() * (i + 1)); (sh[i], sh[j]) = (sh[j], sh[i]); }
            plan.Add((idx[k], sh));
        }
        return plan;
    }

    // Is the answer at place `choice` of the shown order the right one? (-1 is no answer.)
    public static bool IsRight(int seed, int k, int choice)
    {
        if (k < 0 || k >= Count || choice is < 0 or > 3) return false;
        return Plan(seed)[k].shown[choice] == 0;
    }

    public static int RightPlace(int seed, int k) => Array.IndexOf(Plan(seed)[k].shown, 0);

    // A score for the board: most right first, then the lower total time (999999 ms is the slowest that counts).
    public static int Score(int right, int totalMs) => right * 1_000_000 + 999_999 - Math.Clamp(totalMs, 0, 999_999);
    public static int Right(int score) => score / 1_000_000;
    public static int TimeMs(int score) => 999_999 - score % 1_000_000;

    // Who won: more right, then less time (an unanswered question costs the whole fifteen seconds). Positive: the first.
    public static int Decide(IList<QuizAnswer> a, IList<QuizAnswer> b)
    {
        int ra = a.Count(x => x.Right), rb = b.Count(x => x.Right);
        if (ra != rb) return ra > rb ? 1 : -1;
        int ta = a.Sum(x => x.Ms), tb = b.Sum(x => x.Ms);
        return ta == tb ? 0 : ta < tb ? 1 : -1;
    }

    // How the three anglers do: how often they are right and how long they take.
    public static (float right, float fast, float slow) Skill(string rival) => rival switch
    {
        "bess" => (0.55f, 7f, 12f),
        "marlo" => (0.70f, 5f, 9f),
        _ => (0.85f, 3.5f, 7f)
    };
}

// One answer: the place chosen (-1 for none), the milliseconds it took, and whether it was right.
readonly record struct QuizAnswer(int Choice, int Ms, bool Right);

// A Sea School Duel between two sides over any ILink. The challenger is the referee: it picks the seed and starts the game.
// Both sides see the same eight questions and answer at their own pace; each tells the other every answer as it is given
// (the place chosen and the time), and each works out right and wrong itself, so both reach the same result with no last
// word needed. The other side's progress is shown as a count only until the game is over (a right or wrong mark would tell
// the slower side something).
sealed class QuizSession
{
    public const float SilentLimit = 6, HeartbeatSecs = 1, StallSecs = 30, ClockSlack = 2, CountSecs = 3;
    public readonly bool Host;
    public ILink Link;
    public readonly string MyName;
    public string RivalName = "";
    public string State = "joining";                    // joining, play, over, closed
    public string MatchId = "", RecordId = "", EndWhy = "", Notice = "";
    public int Seed, Games, Version;
    public readonly List<QuizAnswer> Mine = new(), Theirs = new();
    public bool PeerGone;
    public float PlayClock;                             // seconds since this game began, on my own clock
    bool rematchMe, rematchThem, hasResult, iLeft;
    float hb, sinceTheirs;
    int theirSum;
    readonly Func<long> wall;                           // milliseconds on a clock that nothing slows (a test passes its own)
    long startTick;
    // How long this game has run: my game clock, or the wall clock if that is longer (a frozen window, a dragged title bar and
    // a clamped frame all slow the game clock but not the other player's answering).
    float Elapsed => MathF.Max(PlayClock, (wall() - startTick) / 1000f);

    public QuizSession(string myName, ILink link, bool host, Func<long> wall = null)
    {
        this.wall = wall ?? (() => Environment.TickCount64); startTick = this.wall();
        Host = host; MyName = SungkaSession.Clean(myName); Link = link;
        if (!host) Send(new NetMsg { t = "qhello", v = SungkaSession.Protocol, name = MyName });
    }

    void Send(NetMsg m) => Link?.Send(m);
    void Bump() => Version++;

    public bool MineDone => Mine.Count >= QuizRules.Count;
    public bool TheirsDone => Theirs.Count >= QuizRules.Count;
    public string Result
    {
        get
        {
            if (State != "over" || !hasResult) return "";
            if (EndWhy == "left") return iLeft ? "loss" : "win";
            if (EndWhy == "stalled") return "win";
            int d = QuizRules.Decide(Mine, Theirs);
            return d > 0 ? "win" : d < 0 ? "loss" : "draw";
        }
    }
    public int MyRight => Mine.Count(a => a.Right);
    public int TheirRight => Theirs.Count(a => a.Right);

    /* ---------- Playing ---------- */
    // I answered question k (the place chosen, or -1 for no answer) in this many milliseconds.
    public bool Answer(int k, int choice, int ms)
    {
        if (State != "play" || k != Mine.Count || k >= QuizRules.Count) return false;
        ms = Math.Clamp(ms, 0, QuizRules.AnswerMs);
        if (choice is < -1 or > 3) return false;
        if (choice == -1) ms = QuizRules.AnswerMs;
        Mine.Add(new QuizAnswer(choice, ms, QuizRules.IsRight(Seed, k, choice)));
        Send(new NetMsg { t = "qans", id = MatchId, n = new[] { k, choice, ms } });
        CheckDone();
        Bump();
        return true;
    }

    void CheckDone()
    {
        if (State == "play" && MineDone && TheirsDone) { State = "over"; EndWhy = "finished"; hasResult = true; Bump(); }
    }

    public void Tick(float dt)
    {
        if (State == "closed") return;
        if (Link != null)
        {
            for (int i = 0; i < 64; i++)
            {
                var m = Link.Receive();
                if (m == null) break;
                string was = State;
                Handle(m);
                if (State == "closed" || was != State && State == "over") break;
            }
            hb += dt;
            if (hb >= HeartbeatSecs) { hb = 0; Send(new NetMsg { t = "hb" }); }
            if (!PeerGone && (!Link.Alive || Link.SilentFor > SilentLimit)) PeerLost();
        }
        if (State == "play")
        {
            PlayClock += dt;
            // I have finished and they have gone quiet for longer than a question takes: they have stopped playing.
            if (MineDone && !TheirsDone) { sinceTheirs += dt; if (sinceTheirs > StallSecs) { State = "over"; EndWhy = "stalled"; hasResult = true; Notice = $"{RivalName} stopped answering. You win."; Bump(); } }
            else sinceTheirs = 0;
        }
    }

    void Handle(NetMsg m)
    {
        switch (m.t)
        {
            case "qhello" when Host && State == "joining":
                if (m.v != SungkaSession.Protocol) { Send(new NetMsg { t = "bye", why = "version" }); Link.Close(); PeerGone = true; State = "closed"; Bump(); return; }
                RivalName = SungkaSession.Clean(m.name);
                BeginMatch();
                break;
            case "qstart" when !Host && (State == "joining" || State == "over" && rematchMe):
            {
                if (m.v != SungkaSession.Protocol || string.IsNullOrEmpty(m.id) || m.id.Length > 40 || m.id == MatchId) { Invalid("a bad start"); return; }
                RivalName = SungkaSession.Clean(m.name);
                MatchId = m.id; Seed = m.s0; RecordId = Guid.NewGuid().ToString("N"); Games++;
                ResetGame();
                State = "play";
                Bump();
                break;
            }
            case "qans" when State == "play" && m.id == MatchId:
            {
                if (m.n == null || m.n.Length != 3) return;
                int k = m.n[0], choice = m.n[1], ms = m.n[2];
                // In order, one each; a place among the four (or none); a time between the fastest a person can read and
                // answer and the whole fifteen seconds, and not more time in all than their own clock could have held.
                if (k != Theirs.Count || k >= QuizRules.Count || choice is < -1 or > 3 || ms < (choice == -1 ? QuizRules.AnswerMs : QuizRules.MinMs) || ms > QuizRules.AnswerMs) { Invalid("an impossible answer"); return; }
                // Nothing can be read in the countdown, so the time claimed so far can only be what my own clock has run since it ended, plus
                // a little for the link and for a frame or two that ran long on either side.
                if (theirSum + ms > (MathF.Max(0, Elapsed - CountSecs) + ClockSlack) * 1000) { Invalid("an impossible time"); return; }
                theirSum += ms; sinceTheirs = 0;
                Theirs.Add(new QuizAnswer(choice, ms, QuizRules.IsRight(Seed, k, choice)));
                CheckDone();
                Bump();
                break;
            }
            case "qrematch" when State == "over" && EndWhy == "finished":
                rematchThem = true;
                if (Host && rematchMe) BeginMatch();
                break;
            case "bye":
                PeerLeft(m.why);
                break;
        }
    }

    void ResetGame()
    {
        Mine.Clear(); Theirs.Clear(); theirSum = 0; PlayClock = 0; sinceTheirs = 0; startTick = wall();
        rematchMe = rematchThem = hasResult = false; EndWhy = ""; Notice = "";
    }

    // The host starts a game (the first, or a rematch): a new seed, a new id.
    void BeginMatch()
    {
        ResetGame();
        Seed = MatchRules.Mix(Environment.TickCount, Games, 53);
        MatchId = Guid.NewGuid().ToString("N"); RecordId = Guid.NewGuid().ToString("N");
        Games++;
        State = "play";
        Send(new NetMsg { t = "qstart", id = MatchId, s0 = Seed, v = SungkaSession.Protocol, name = MyName });
        Bump();
    }

    void Invalid(string why)
    {
        Send(new NetMsg { t = "bye", why = "invalid" });
        State = "over"; EndWhy = "invalid"; hasResult = false; PeerGone = true; Notice = $"The game was stopped ({why}).";
        Link?.Close();
        Bump();
    }

    void PeerLeft(string why)
    {
        if (State == "play" && why is "invalid" or "version") { State = "over"; EndWhy = "invalid"; hasResult = false; Notice = "The game was stopped."; }
        else if (State == "play") { State = "over"; EndWhy = "left"; hasResult = true; Notice = $"{RivalName} left the game."; }
        else if (State == "joining") { State = "over"; EndWhy = "left"; hasResult = false; Notice = "The other player left before the game began."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    // The line has gone quiet or dropped. Each side sees the other vanish, so neither can know who it was: nobody wins.
    void PeerLost()
    {
        if (State == "play") { State = "over"; EndWhy = "lost"; hasResult = false; Notice = "The connection was lost. The game has no result."; }
        else if (State == "joining") { State = "over"; EndWhy = "lost"; hasResult = false; Notice = "The other player didn't answer."; }
        PeerGone = true;
        Link?.Close();
        Bump();
    }

    public void Rematch()
    {
        if (State != "over" || PeerGone || EndWhy != "finished" || rematchMe) return;
        rematchMe = true;
        if (Host) { if (rematchThem) BeginMatch(); } else Send(new NetMsg { t = "qrematch", id = MatchId });
        Bump();
    }
    public bool WaitingForRematch => rematchMe && State == "over";

    public void Leave()
    {
        if (State == "closed") return;
        bool inGame = State == "play";
        iLeft = true;
        Send(new NetMsg { t = "bye", why = "left" });
        Link?.Close();
        Link = null;
        if (inGame) { State = "over"; EndWhy = "left"; hasResult = true; }
        else if (State != "over") State = "closed";
        PeerGone = true;
        Bump();
    }

    public void Close() { Link?.Close(); Link = null; State = "closed"; }
}

// The other side when you play alone: a QuizSession on the other end of an in-memory link that answers the eight questions
// the way one of the anglers would (how often right, how long they take).
sealed class QuizBot
{
    public readonly QuizSession S;
    readonly string rival;
    string idSeen = "";
    readonly List<(int choice, int ms)> plan = new();

    public QuizBot(string name, string rival, ILink link)
    {
        S = new QuizSession(name, link, host: false);
        this.rival = rival;
    }

    void Plan()
    {
        plan.Clear();
        var (right, fast, slow) = QuizRules.Skill(rival);
        int ri = Array.FindIndex(MatchRules.Rivals, r => r.Id == rival);
        var rng = new MatchRng(MatchRules.Mix(S.Seed, 41, ri));
        for (int k = 0; k < QuizRules.Count; k++)
        {
            bool ok = rng.NextDouble() < right;
            int place = QuizRules.RightPlace(S.Seed, k);
            int choice = ok ? place : (place + 1 + (int)(rng.NextDouble() * 3)) % 4;
            int ms = (int)((fast + (slow - fast) * rng.NextDouble()) * 1000);
            plan.Add((choice, ms));
        }
    }

    float overT;

    public void Tick(float dt)
    {
        S.Tick(dt);
        if (S.State == "over" && S.EndWhy == "finished" && !S.PeerGone) { overT += dt; if (overT > 1.2f) S.Rematch(); }   // the angler is always up for another
        else overT = 0;
        if (S.State != "play") return;
        if (idSeen != S.MatchId) { idSeen = S.MatchId; Plan(); }
        // It answers question k once its clock has run past the countdown and everything it has "read" so far.
        int k = S.Mine.Count;
        if (k >= QuizRules.Count) return;
        float due = QuizSession.CountSecs + plan.Take(k + 1).Sum(p => p.ms) / 1000f;
        if (S.PlayClock >= due) S.Answer(k, plan[k].choice, plan[k].ms);
    }
}
