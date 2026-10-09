using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Ma'am Isay's class (1.17): a little school on the north side of Amihan Village. She teaches the village children about
// the fish their families bring home, and asks you to help with the fish you've caught. A lesson is eight questions on
// fish you've caught or seen (their shapes and shadows, where and when they bite, how they fight, which grows bigger,
// what's real about them), with three hearts, a streak, a quick-answer bonus and one "ask the class". Every answer
// teaches the right one. Three right answers about a fish and you've learned it: a star in the Fish log, and the Sea
// school's Fish ID badge (with Release.cs's "Let it go" and Tides.cs's tides) pays out as you go.
sealed class QuizQ
{
    public string Kind, Prompt, Subject, Teach, Quote, Big;     // Subject: the fish it's about; Big: a scientific name to show
    public bool Sil;                                            // the picture is the fish's shadow
    public string[] Options, Pics;                              // the answers (Pics: a fish picture per answer, or null)
    public int Answer;
}

sealed class Quiz
{
    public List<QuizQ> Qs = new();
    public int At, Right, Hearts = 3, Streak, BestStreak, Score, Coins, Picked = -1;
    public float T, Bounce;
    public bool Paid, AskUsed;
    public int[] Votes;
    public string View = "intro";
    public List<string> Learned = new();
}

partial class Game
{
    const float SchoolX = 1655, SchoolY = 135;
    const int LessonLength = 8, LearnedAt = 3, MinKnown = 4;
    Quiz quiz;

    bool SchoolHours => state.clock is >= 7 * 60 and < 16 * 60 && !Stormy;
    bool LessonToday => state.lessonDay != state.day;
    bool Learnt(string id) => state.know.GetValueOrDefault(id) >= LearnedAt;
    int LearnedCount => state.know.Count(kv => kv.Value >= LearnedAt);

    // Every fish you could be asked about: ones you've caught (not legends).
    List<CommonFish> KnownFish() => Data.AllCommon.Where(f => !f.Legend && FishArt.Looks.ContainsKey(f.Id) && state.commons.GetValueOrDefault(f.Id) > 0).ToList();

    /* ---------- Talking to Ma'am Isay ---------- */
    void TalkIsay()
    {
        Say I(string t) => new("Ma'am Isay", t);
        var s = IslanderWalk("isay");
        FaceToward(s.X, s.Y);
        if (!state.Hinted("metIsay"))
        {
            state.hinted["metIsay"] = true;
            Talk(new()
            {
                I("Magandang araw! I'm Ma'am Isay. I teach the village children, when I can keep them out of the water."),
                I("They know the fish their families bring home. But you've been all over these islands! Half the fish in your bag, they've never seen."),
                I("Help me teach them? I'll ask about the fish you've caught: shapes and shadows, where they bite, what's real about them. Three right answers about a fish and you've really learned it."),
                I("The first lesson each day earns you a little from the school fund. Practise as much as you like after that.")
            }, OpenQuiz);
            return;
        }
        OpenQuiz();
    }

    void OpenQuiz()
    {
        quiz = new Quiz { Paid = LessonToday };
        lessonPrizes = new();
        Sfx.Play("ui");
        panel = "quiz";
        mode = "panel";
        SetPrompt("");
    }

    /* ---------- The questions ---------- */
    QuizQ MakeQuestion(string kind, List<CommonFish> known, HashSet<string> recent)
    {
        // Fish you know least come up most, and ones you got wrong last time most of all.
        var pool = known.Where(f => !recent.Contains(f.Id)).ToList();
        if (pool.Count == 0) pool = known;
        double Weight(CommonFish f) => 1.0 + Math.Max(0, LearnedAt - state.know.GetValueOrDefault(f.Id)) + (state.missed.Contains(f.Id) ? 4 : 0);
        double roll = rng.NextDouble() * pool.Sum(Weight);
        var subject = pool[^1];
        foreach (var f in pool) { roll -= Weight(f); if (roll < 0) { subject = f; break; } }
        string name = subject.Name;
        string lower = name.ToLowerInvariant();
        FishFacts.ById.TryGetValue(subject.Id, out var fact);
        bool pot = !Data.SpotOfFish.ContainsKey(subject.Id);

        QuizQ Names(string prompt, string teach)
        {
            var others = known.Where(f => f.Id != subject.Id).OrderBy(_ => rng.Next()).Select(f => f.Name).Distinct().Take(3).ToList();
            if (others.Count < 3)
                others.AddRange(Data.AllCommon.Where(f => !f.Legend && f.Id != subject.Id && !others.Contains(f.Name) && SpotKnownFor(f.Id))
                    .OrderBy(_ => rng.Next()).Select(f => f.Name).Distinct().Take(3 - others.Count));
            return Shuffle(new QuizQ { Prompt = prompt, Subject = subject.Id, Teach = teach }, name, others);
        }

        switch (kind)
        {
            case "picture":
                return Names("What fish is this?", FishArt.Looks[subject.Id].About).With(q => q.Kind = kind);
            case "shadow":
                return Names("Whose shadow is this?", FishArt.Looks[subject.Id].About).With(q => { q.Kind = kind; q.Sil = true; });
            case "where":
            {
                if (pot) return null;
                var spot = Data.SpotById[Data.SpotOfFish[subject.Id]];
                // Only places you've fished: no names from islands you haven't found.
                var labels = Data.Spots.Where(s => s.Id != spot.Id && SpotKnown(s) && s.Label != spot.Label && Data.Common[s.Id].Any(f => state.commons.GetValueOrDefault(f.Id) > 0))
                    .Select(s => s.Label).Distinct().OrderBy(_ => rng.Next()).Take(3).ToList();
                if (labels.Count < 3) return null;
                return Shuffle(new QuizQ { Kind = kind, Prompt = $"Where would you catch a {lower}?", Subject = subject.Id,
                    Teach = $"The {lower} lives at {spot.Label}, on {Data.Biomes[PageOf(subject.Id)].Name}." }, spot.Label, labels);
            }
            case "when":
            {
                if (pot) return null;
                string right = subject.Time switch { "day" => "By day", "night" => "At night", _ => "Day or night" };
                var q = new QuizQ { Kind = kind, Prompt = $"When does the {lower} bite?", Subject = subject.Id, Teach = $"The {lower} bites {WhenText(subject)}.",
                    Options = new[] { "By day", "At night", "Day or night" } };
                q.Answer = Array.IndexOf(q.Options, right);
                return q;
            }
            case "fight":
            {
                if (pot) return null;
                var styles = new[] { ("dart", "It darts about"), ("runner", "It makes long runs"), ("jumper", "It leaps out of the water"), ("bottom", "It hugs the bottom") };
                var q = new QuizQ { Kind = kind, Prompt = $"How does a {lower} fight on the line?", Subject = subject.Id, Options = styles.Select(t => t.Item2).ToArray() };
                q.Answer = Array.FindIndex(styles, t => t.Item1 == subject.Style);
                q.Teach = subject.Style switch
                {
                    "runner" => $"The {lower} makes runs: let the line go while it runs, or it snaps.",
                    "jumper" => $"The {lower} leaps: press as it crosses the gold.",
                    "bottom" => $"The {lower} sulks on the bottom: pump it up with short taps.",
                    _ => $"The {lower} darts about: keep it in the green."
                };
                return q;
            }
            case "heavier":
            {
                var other = known.Where(f => f.Id != subject.Id && (f.Kg >= subject.Kg * 2 || subject.Kg >= f.Kg * 2)).OrderBy(_ => rng.Next()).FirstOrDefault();
                if (other == null) return null;
                var (big, small) = subject.Kg > other.Kg ? (subject, other) : (other, subject);
                bool first = rng.NextDouble() < 0.5;
                return new QuizQ
                {
                    Kind = kind, Prompt = "Which one grows heavier?", Subject = big.Id,
                    Options = first ? new[] { big.Name, small.Name } : new[] { small.Name, big.Name },
                    Pics = first ? new[] { big.Id, small.Id } : new[] { small.Id, big.Id }, Answer = first ? 0 : 1,
                    Teach = $"A grown {big.Name.ToLowerInvariant()} is usually about {Kg(big.Kg)}; a {small.Name.ToLowerInvariant()}, about {Kg(small.Kg)}."
                };
            }
            case "fact":
            {
                if (fact == null) return null;
                string quote = Redact(FirstSentences(fact.Text, 170), subject);
                if (quote == null) return null;
                return Names("Who am I?", $"That's the {lower}: {(fact.Real ? fact.Sci : "based on the " + fact.Sci)}.").With(q => { q.Kind = kind; q.Quote = quote; });
            }
            case "real":
            {
                if (fact == null) return null;
                return new QuizQ
                {
                    Kind = kind, Prompt = $"Is the {lower} a real animal, or made up for Fesh?", Subject = subject.Id,
                    Options = new[] { "A real animal", "Made up for Fesh" }, Answer = fact.Real ? 0 : 1,
                    Teach = fact.Real ? $"Real! Scientists call it {fact.Sci}." : $"Made up, but it's based on a real one: the {fact.Sci}."
                };
            }
            case "sci":
            {
                if (fact is not { Real: true } || fact.Sci.Contains("spp") || fact.Sci.StartsWith("Family") || fact.Sci.Contains(',')) return null;
                return Names("Which fish has this scientific name?", $"{fact.Sci} is the {lower}. Scientific names are agreed worldwide, so scientists everywhere know which fish you mean.")
                    .With(q => { q.Kind = kind; q.Big = fact.Sci; });
            }
            case "bait":
            {
                if (pot) return null;
                var liked = Items.Baits.Keys.Where(b => BaitLikes(b, subject, Data.SpotOfFish[subject.Id])).ToList();
                var not = Items.Baits.Keys.Where(b => !liked.Contains(b)).Select(b => Items.ById[b].Name).Distinct().OrderBy(_ => rng.Next()).Take(3).ToList();
                if (liked.Count == 0 || not.Count < 3) return null;
                string pick = Items.ById[liked[rng.Next(liked.Count)]].Name;
                return Shuffle(new QuizQ { Kind = kind, Prompt = $"Which bait does a {lower} go for?", Subject = subject.Id,
                    Teach = $"The {lower} goes for {string.Join(", ", LikedBaits(subject))}." }, pick, not);
            }
        }
        return null;
    }

    bool SpotKnownFor(string id) => !Data.SpotOfFish.TryGetValue(id, out var s) || SpotKnown(Data.SpotById[s]);

    QuizQ Shuffle(QuizQ q, string right, List<string> wrong)
    {
        var all = wrong.Take(3).Append(right).OrderBy(_ => rng.Next()).ToList();
        q.Options = all.ToArray();
        q.Answer = all.IndexOf(right);
        return q;
    }

    static string FirstSentences(string text, int max)
    {
        var parts = text.Split(". ");
        string s = parts[0];
        for (int i = 1; i < parts.Length && s.Length + parts[i].Length < max; i++) s += ". " + parts[i];
        return s.EndsWith('.') ? s : s + ".";
    }

    // The fish's own name blanked out of a fact, word by word (and nothing else that names it).
    static string Redact(string text, CommonFish f)
    {
        var words = f.Name.Replace("(", " ").Replace(")", " ").Replace("-", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4).Select(w => w.ToLowerInvariant()).ToList();
        var outWords = text.Split(' ').Select(w =>
        {
            string bare = new string(w.Where(char.IsLetter).ToArray()).ToLowerInvariant();
            bool hit = bare.Length >= 4 && words.Any(n => bare.Contains(n.TrimEnd('s')) || n.StartsWith(bare.TrimEnd('s')));
            return hit ? "____" + new string(w.Reverse().TakeWhile(c => !char.IsLetter(c)).Reverse().ToArray()) : w;
        });
        return string.Join(' ', outWords);
    }

    static readonly string[][] LessonPlan =
    {
        new[] { "picture", "shadow" }, new[] { "shadow", "picture" },
        new[] { "where", "when", "fight", "heavier", "bait" }, new[] { "where", "when", "fight", "heavier", "bait" },
        new[] { "heavier", "where", "fight", "bait", "when" }, new[] { "real", "where", "when", "fight" },
        new[] { "fact", "sci", "real" }, new[] { "fact", "sci", "real" }
    };

    void StartLesson()
    {
        var known = KnownFish();
        var q = quiz;
        q.Qs.Clear();
        var recent = new HashSet<string>();
        foreach (var slot in LessonPlan)
        {
            QuizQ made = null;
            foreach (var kind in slot.OrderBy(_ => rng.Next()))
                if ((made = MakeQuestion(kind, known, recent)) != null) break;
            made ??= MakeQuestion("picture", known, recent);
            q.Qs.Add(made);
            recent.Add(made.Subject);
            if (recent.Count > Math.Max(1, known.Count - 3)) recent.Clear();
        }
        q.At = 0; q.View = "question"; q.T = 0; q.Picked = -1; q.Votes = null;
        Sfx.Play("ui");
    }

    void AnswerQuiz(int i)
    {
        var q = quiz;
        if (q?.View != "question") return;
        var qq = q.Qs[q.At];
        if (i < 0 || i >= qq.Options.Length) return;
        q.Picked = i;
        q.View = "answer";
        q.Bounce = 1;
        if (i == qq.Answer)
        {
            q.Right++; q.Streak++; q.BestStreak = Math.Max(q.BestStreak, q.Streak);
            q.Score += 100 + (q.T < 6 ? 50 : 0) + q.Streak * 10;
            int before = state.know.GetValueOrDefault(qq.Subject);
            state.know[qq.Subject] = Math.Min(9, before + 1);
            if (before + 1 == LearnedAt) q.Learned.Add(qq.Subject);
            state.missed.Remove(qq.Subject);
            Sfx.Play("right");
        }
        else
        {
            q.Hearts--; q.Streak = 0; Sfx.Play("wrong");
            // It comes up again soon, in this lesson or the next.
            if (!state.missed.Contains(qq.Subject)) state.missed.Add(qq.Subject);
            if (state.missed.Count > 12) state.missed.RemoveAt(0);
        }
        CheckBadges();
    }

    void NextQuestion()
    {
        var q = quiz;
        if (q?.View != "answer") return;
        if (q.At + 1 >= q.Qs.Count || q.Hearts <= 0) { EndLesson(); return; }
        q.At++; q.View = "question"; q.T = 0; q.Picked = -1; q.Votes = null;
        Sfx.Play("blip");
    }

    void EndLesson()
    {
        var q = quiz;
        q.View = "done";
        if (q.Paid && state.lessonDay != state.day)
        {
            q.Coins = 6 * q.Right + (q.Right == q.Qs.Count ? 30 : 0);
            state.coins += q.Coins;
            state.lessonDay = state.day;
        }
        state.lessons++;
        state.bestLesson = Math.Max(state.bestLesson, q.Score);
        Sfx.Play(q.Right >= 7 ? "rare" : "craft");
        CheckBadges();
        Save();
    }

    // "Ask the class": once a lesson the children put their hands up. They know the islands' own fish best.
    void AskClass()
    {
        var q = quiz;
        if (q?.View != "question" || q.AskUsed) return;
        q.AskUsed = true;
        var qq = q.Qs[q.At];
        int n = qq.Options.Length;
        bool local = Data.Biomes[PageOf(qq.Subject)].Id is "amihan" or "habagat";
        var votes = new int[n];
        int right = local ? 55 + rng.Next(26) : 35 + rng.Next(26);
        votes[qq.Answer] = right;
        int left = 100 - right;
        for (int k = 0, others = n - 1; k < n; k++)
        {
            if (k == qq.Answer) continue;
            int v = --others == 0 ? left : rng.Next(left + 1);
            votes[k] = v; left -= v;
        }
        q.Votes = votes;
        Sfx.Play("pickup");
    }

    void UpdateQuiz(float dt)
    {
        var q = quiz;
        if (q == null) { ClosePanels(); return; }
        q.Bounce = Math.Max(0, q.Bounce - dt * 2);
        if (q.View == "question")
        {
            q.T += dt;
            for (int i = 0; i < q.Qs[q.At].Options.Length; i++)
                if (Inp.Pressed((KeyboardKey)((int)KeyboardKey.One + i))) { AnswerQuiz(i); return; }
        }
        else if (q.View == "answer" && (Inp.Pressed(KeyboardKey.Enter) || Bind.Pressed("act"))) NextQuestion();
    }

    // At a bite, the field guide names a fish you've learned (Fishing.cs).
    string FieldGuideName()
    {
        if (!Wears("field_guide") || fish?.Roll is not { } r || r.Exotic || r.Odd || r.Chest || r.Boss) return "";
        if (!Data.FishById.TryGetValue(r.Id, out var f) || f.Legend || !Learnt(f.Id)) return "";
        return $" It's a {f.Name.ToLowerInvariant()}!";
    }

    /* ---------- The quiz panel ---------- */
    float lastQuizBottom;       // where the panel's text ended (the autotest checks it fits)
    static readonly string[] Cheers = { "Tama!", "Galing!", "Ang galing!" }, Groans = { "Ay!", "Sayang!", "Ay, naku!" };

    void DrawQuiz()
    {
        var q = quiz;
        if (q == null) return;
        Backdrop();
        const float w = 1120, h = 650, pad = 26;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Ma'am Isay's class", x + pad, y + pad - 4, FontKind.Ui700, 32, Pal.PaperInk);
        if (SmallButton("Close", x + w - pad - SmallW("Close"), y + pad - 4)) { ClosePanels(); return; }
        float classY = y + h - 168;
        if (q.View == "intro") DrawQuizIntro(x, y, w, pad);
        else if (q.View == "done") { DrawQuizDone(x, y, w, pad); if (quiz != q) return; }
        else if (!DrawQuizQuestion(x, y, w, pad)) return;
        DrawClass(x + pad, classY, q);
        if (Gfx.PressedOutside(x, y, w, h)) ClosePanels();
    }

    void DrawQuizIntro(float x, float y, float w, float pad)
    {
        var q = quiz;
        int known = KnownFish().Count;
        float ty = y + pad + 52;
        string lead = q.Paid ? "Today's lesson: eight questions about fish you've caught. Three hearts. Every answer teaches the right one."
            : "You've had today's lesson. Practise as much as you like: it still counts toward the fish you've learned, but the school fund pays once a day.";
        var l = Gfx.Wrap(lead, FontKind.Note, 20, w - 2 * pad);
        Lines(l, x + pad, ty, 27, FontKind.Note, 20, Muted);
        ty += l.Count * 27 + 16;
        var again = state.missed.Where(Data.FishById.ContainsKey).Select(id => Data.FishById[id].Name).Take(4).ToList();
        var stats = new[]
        {
            ("Fish you know", $"{known} kinds caught"),
            ("Learned", $"{LearnedCount} (three right answers about a fish; a star in your Fish log)"),
            ("Best score", state.bestLesson > 0 ? $"{state.bestLesson}" : "no lessons yet"),
            ("How it scores", "100 a right answer, 50 more for a quick one, and a bonus for a streak"),
            ("Again today", again.Count > 0 ? $"the ones you missed: {string.Join(", ", again)}" : "nothing missed last time")
        };
        foreach (var (k, v) in stats)
        {
            Gfx.Text(k, x + pad, ty, FontKind.Ui700, 18, Muted);
            Gfx.Text(Gfx.Ellipsize(v, FontKind.Ui500, 18, w - 2 * pad - 180), x + pad + 180, ty, FontKind.Ui500, 18, Pal.PaperInk);
            ty += 30;
        }
        ty += 12;
        if (known < MinKnown)
        {
            Gfx.Text($"Catch at least {MinKnown} kinds of fish first (you have {known}), then come and teach us.", x + pad, ty, FontKind.Ui700, 20, Rust);
            lastQuizBottom = ty + 30;
            return;
        }
        string label = q.Paid ? "Start today's lesson" : "Practise";
#if DEBUG
        Gfx.Seen["quiz:start"] = new Rectangle(x + pad, ty, BigW(label), 56);
#endif
        if (BigButton(label, x + pad, ty, true)) StartLesson();
        lastQuizBottom = ty + 56;
    }

    // Draws a question and its answer. False if a button changed the screen.
    bool DrawQuizQuestion(float x, float y, float w, float pad)
    {
        var q = quiz;
        var qq = q.Qs[q.At];
        bool answered = q.View == "answer";
        // The header: hearts, which question, the streak and the score.
        float hx = x + w - pad - SmallW("Close") - 20;
        string count = $"Question {q.At + 1} of {q.Qs.Count}";
        hx -= Gfx.Measure(count, FontKind.Ui600, 17);
        Gfx.Text(count, hx, y + pad + 8, FontKind.Ui600, 17, Muted);
        for (int i = 0; i < 3; i++) DrawHeart(hx - 30 - i * 26, y + pad + 17, i < q.Hearts);
        string score = $"Score {q.Score}" + (q.Streak >= 2 ? $" · streak {q.Streak}" : "");
        Gfx.Text(score, x + pad, y + pad + 40, FontKind.Ui600, 17, q.Streak >= 2 ? Pal.C("#9a6a1a") : Muted);
        var prompt = Gfx.Wrap(qq.Prompt, FontKind.Ui700, 24, w - 2 * pad);
        Lines(prompt, x + pad, y + pad + 70, 30, FontKind.Ui700, 24, Pal.PaperInk);

        // What the question shows: the fish, its shadow, a quote, or a scientific name.
        float px = x + pad, py = y + pad + 110, pw = 400, ph = 232;
        bool pics = qq.Pics != null;
        if (!pics)
        {
            Gfx.Rect(px - 3, py - 3, pw + 6, ph + 6, Pal.Ink, 4);
            if (qq.Quote != null)
            {
                Gfx.Rect(px, py, pw, ph, NoteBg, 3);
                var lines = Gfx.Wrap("“" + qq.Quote + "”", FontKind.Note, 18, pw - 30);
                if (lines.Count > 9) lines = lines.Take(9).ToList();
                Lines(lines, px + 15, py + 14, 23, FontKind.Note, 18, Pal.PaperInk);
            }
            else if (qq.Big != null)
            {
                Gfx.Rect(px, py, pw, ph, NoteBg, 3);
                var lines = Gfx.Wrap(qq.Big, FontKind.Note, 34, pw - 30);
                Lines(lines, px + 15, py + ph / 2 - lines.Count * 22, 44, FontKind.Note, 34, Pal.PaperInk);
            }
            else
            {
                Gfx.Rect(px, py, pw, ph, Pal.C("#2a5a7a"), 3);
                // The shadow turns into the fish once you've answered.
                var tex = FishArt.Picture(qq.Subject, qq.Sil && !answered);
                DrawTexturePro(tex, new Rectangle(0, 0, FishArt.BigW, FishArt.BigH), Gfx.S(px + 20, py + 24, FishArt.BigW * 7.5f, FishArt.BigH * 6.6f), System.Numerics.Vector2.Zero, 0, Color.White);
            }
        }

        // The answers.
        int n = qq.Options.Length;
        float ax = pics ? x + pad : px + pw + 30, aw = pics ? w - 2 * pad : x + w - pad - ax, ay = py;
        float bw = pics ? (aw - 20) / 2 : n == 4 ? (aw - 16) / 2 : aw, bh = pics ? ph : n == 4 ? (ph - 16) / 2 : (ph - 12 * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            float bx = pics ? ax + i * (bw + 20) : n == 4 ? ax + i % 2 * (bw + 16) : ax;
            float by = pics ? ay : n == 4 ? ay + i / 2 * (bh + 16) : ay + i * (bh + 12);
            var fill = !answered ? Pal.Sand : i == qq.Answer ? Pal.C("#9fd0a0") : i == q.Picked ? Pal.C("#e8a090") : Pal.C("#e9e0cc");
            if (!answered && Gfx.Hover(bx, by, bw, bh)) fill = Lighten(fill, 0.14f);
            Gfx.Box(bx, by, bw, bh, fill, Pal.Ink, 3, 6, 2);
            if (pics)
            {
                var tex = FishArt.Picture(qq.Pics[i], false);
                DrawTexturePro(tex, new Rectangle(0, 0, FishArt.BigW, FishArt.BigH), Gfx.S(bx + bw / 2 - FishArt.BigW * 3, by + 14, FishArt.BigW * 6, FishArt.BigH * 6), System.Numerics.Vector2.Zero, 0, Color.White);
                Gfx.TextCenter(Gfx.Ellipsize(qq.Options[i], FontKind.Ui700, 20, bw - 20), bx + bw / 2, by + bh - 36, FontKind.Ui700, 20, Pal.Ink);
            }
            else
            {
                var l = Gfx.Wrap(qq.Options[i], FontKind.Ui700, 19, bw - 50);
                if (l.Count > 2) l = new List<string> { l[0], Gfx.Ellipsize(l[1] + " " + l[2], FontKind.Ui700, 19, bw - 50) };
                Gfx.Text($"{i + 1}", bx + 12, by + 10, FontKind.Ui700, 15, Muted);
                Lines(l, bx + 34, by + bh / 2 - l.Count * 12, 24, FontKind.Ui700, 19, Pal.Ink);
            }
            if (q.Votes != null)
            {
                Gfx.Rect(bx + 6, by + bh - 10, (bw - 12) * q.Votes[i] / 100f, 5, Pal.C("#3f6a8a"), 2);
                Gfx.Text($"{q.Votes[i]}%", bx + bw - 48, by + 6, FontKind.Ui700, 14, Pal.C("#3f6a8a"));
            }
#if DEBUG
            Gfx.Seen[$"quiz:{i}"] = new Rectangle(bx, by, bw, bh);
#endif
            if (!answered && Gfx.Click(bx, by, bw, bh)) { AnswerQuiz(i); return false; }
        }

        // Under the answers: the quick bonus and "ask the class", or what the right answer teaches.
        float fx = x + pad + 330, fy = py + ph + 22, fw = x + w - pad - fx;
        if (!answered)
        {
            float left = Math.Clamp(1 - q.T / 6, 0, 1);
            Gfx.Text(left > 0 ? "Quick answer bonus" : "Take your time", fx, fy + 4, FontKind.Ui600, 15, Muted);
            Gfx.Rect(fx + 160, fy + 8, 200, 10, Pal.C("#e9e0cc"), 4);
            if (left > 0) Gfx.Rect(fx + 160, fy + 8, 200 * left, 10, Pal.Lantern, 4);
            string ask = q.AskUsed ? "Asked the class" : "Ask the class";
#if DEBUG
            Gfx.Seen["quiz:ask"] = new Rectangle(fx + fw - 180, fy - 4, 180, 40);
#endif
            if (Button(ask, fx + fw - 180, fy - 4, 180, 40, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5, !q.AskUsed)) AskClass();
            lastQuizBottom = fy + 36;
        }
        else
        {
            bool right = q.Picked == qq.Answer;
            var lines = Gfx.Wrap(qq.Teach, FontKind.Ui500, 16, fw - 150);
            if (lines.Count > 4) lines = lines.Take(4).ToList();
            float bh2 = Math.Max(76, lines.Count * 20 + 38);
            Gfx.Box(fx, fy - 6, fw, bh2, right ? Pal.C("#e4f3e0") : Pal.C("#f8e6dc"), right ? Pal.C("#3f8a4a") : Rust, 2, 5);
            Gfx.Text(right ? "Tama! (Right!)" : $"The answer: {Gfx.Ellipsize(qq.Options[qq.Answer], FontKind.Ui700, 16, fw - 300)}", fx + 12, fy, FontKind.Ui700, 16, right ? Pal.C("#2f6a3a") : Rust);
            Lines(lines, fx + 12, fy + 22, 20, FontKind.Ui500, 16, Pal.PaperInk);
            string next = q.At + 1 >= q.Qs.Count || q.Hearts <= 0 ? "Finish" : "Next";
#if DEBUG
            Gfx.Seen["quiz:next"] = new Rectangle(fx + fw - 128, fy + bh2 / 2 - 26, 116, 44);
#endif
            if (Button(next, fx + fw - 128, fy + bh2 / 2 - 26, 116, 44, FontKind.Ui700, 19, Pal.Buoy, White, 4, 2, 6)) { NextQuestion(); return false; }
            lastQuizBottom = fy - 6 + bh2;
        }
        return true;
    }

    void DrawQuizDone(float x, float y, float w, float pad)
    {
        var q = quiz;
        float ty = y + pad + 56;
        // A big stamp, like the ones on a good report card.
        string stamp = q.Right >= 7 ? "Magaling!" : q.Right >= 5 ? "Mahusay!" : "Subukan ulit";
        string gloss = q.Right >= 7 ? "(Excellent!)" : q.Right >= 5 ? "(Well done!)" : "(Try again!)";
        var ink = q.Right >= 7 ? Pal.C("#b5423a") : q.Right >= 5 ? Pal.C("#3f6a8a") : Pal.C("#8a6a2a");
        float sx = x + w - pad - 300, sy = ty + 6;
        Gfx.Circle(sx + 130, sy + 64, 80, ink);
        Gfx.Circle(sx + 130, sy + 64, 74, Pal.Paper);
        Gfx.Circle(sx + 130, sy + 64, 68, ink);
        Gfx.Circle(sx + 130, sy + 64, 64, Pal.Paper);
        Gfx.TextCenter(stamp, sx + 130, sy + 40, FontKind.Ui700, stamp.Length > 9 ? 22 : 28, ink);
        Gfx.TextCenter(gloss, sx + 130, sy + 76, FontKind.Ui600, 15, ink);
        var rows = new List<string>
        {
            $"{q.Right} of {q.Qs.Count} right" + (q.Hearts <= 0 && q.At + 1 < q.Qs.Count ? " (out of hearts)" : ""),
            $"Best streak {q.BestStreak} · score {q.Score}" + (q.Score >= state.bestLesson && q.Score > 0 ? " · a new best!" : $" (best {state.bestLesson})"),
            q.Coins > 0 ? $"From the school fund: {q.Coins} coins" : q.Paid ? "No coins this time, but you've had your lesson" : "Practice: no coins, but it all counts toward learning",
            $"Fish learned: {LearnedCount}"
        };
        foreach (var r in rows) { Gfx.Text(r, x + pad, ty, FontKind.Ui600, 20, Pal.PaperInk); ty += 32; }
        if (q.Learned.Count > 0)
        {
            ty += 6;
            Gfx.Text("Learned today:", x + pad, ty, FontKind.Ui700, 18, Pal.C("#2a7d74"));
            // Two rows of them, clear of the stamp.
            float lx0 = x + pad + 150, lx = lx0, right = x + w - pad - 320;
            int row = 0;
            foreach (var id in q.Learned)
            {
                string nm = Gfx.Ellipsize(Data.FishById[id].Name, FontKind.Ui500, 15, 150);
                float iw = 32 + Gfx.Measure(nm, FontKind.Ui500, 15) + 18;
                if (lx + iw > right && lx > lx0) { if (++row == 2) break; lx = lx0; ty += 32; }
                DrawIcon(id, lx, ty - 4, 28);
                Gfx.Text(nm, lx + 32, ty + 2, FontKind.Ui500, 15, Pal.PaperInk);
                lx += iw;
            }
            ty += 34;
        }
        ty += 10;
        if (lessonPrizes is { Count: > 0 })
        {
            var won = Gfx.Wrap("Sea school: " + string.Join("; ", lessonPrizes) + ".", FontKind.Ui700, 17, w - 2 * pad - 330);
            Lines(won, x + pad, ty, 22, FontKind.Ui700, 17, Pal.C("#9a6a1a"));
            ty += won.Count * 22 + 10;
        }
#if DEBUG
        Gfx.Seen["quiz:again"] = new Rectangle(x + pad, ty, BigW("Another lesson"), 56);
#endif
        if (BigButton("Another lesson", x + pad, ty, true)) { quiz = new Quiz(); lessonPrizes = new(); StartLesson(); return; }
        if (BigButton("Done", x + pad + BigW("Another lesson") + 16, ty, false)) { ClosePanels(); return; }
        lastQuizBottom = ty + 56;
    }

    static void DrawHeart(float x, float y, bool full)
    {
        var c = full ? Pal.C("#d8433a") : Pal.C("#d8cbb0");
        Gfx.Circle(x - 5, y - 3, 6, c);
        Gfx.Circle(x + 5, y - 3, 6, c);
        Gfx.Triangle(x - 10.5f, y - 1, x + 10.5f, y - 1, x, y + 10, c);
    }

    // The class along the bottom: four children on a bench and Ma'am Isay, cheering or groaning at your answer.
    void DrawClass(float x, float y, Quiz q)
    {
        const int S = 7;
        bool answered = q.View == "answer", right = answered && q.Picked == q.Qs[q.At].Answer, done = q.View == "done";
        int pose = answered ? (right ? 1 : 2) : done ? (q.Right >= 5 ? 1 : 0) : 0;
        Gfx.Rect(x, y + 130, 300, 8, Pal.C("#8a5f36"), 2);
        Gfx.Rect(x + 10, y + 138, 6, 20, Pal.C("#6b4a2b")); Gfx.Rect(x + 284, y + 138, 6, 20, Pal.C("#6b4a2b"));
        for (int k = 0; k < 4; k++)
        {
            float kx = x + 34 + k * 72, ky = y + 128 - (pose == 1 ? MathF.Abs(MathF.Sin((time * 9 + k) )) * 10 * Math.Max(q.Bounce, done ? 0.4f : 0) : 0);
            void R(int px, int py, int pw, int ph, string c) => Gfx.Rect(kx + px * S, ky + py * S, pw * S, ph * S, Pal.C(c));
            DrawKid(R, 0, 0, k, pose, time);
        }
        // Ma'am Isay, standing by the board.
        DrawIsayPortrait(x + 318, y + 22);
        if (answered || done)
        {
            string say = answered ? (right ? Cheers[q.At % Cheers.Length] : Groans[q.At % Groans.Length]) : q.Right >= 5 ? "Galing!" : "Ay!";
            float bw = Gfx.Measure(say, FontKind.Ui700, 18) + 20;
            Gfx.Box(x + 40, y, bw, 32, CardBg, Pal.Ink, 2, 8);
            Gfx.Triangle(x + 60, y + 30, x + 76, y + 30, x + 62, y + 44, Pal.Ink);
            Gfx.Text(say, x + 50, y + 5, FontKind.Ui700, 18, right || done && q.Right >= 5 ? Pal.C("#2f6a3a") : Rust);
        }
    }

    // A child, about nine pixels tall, sitting: 0 still, 1 arms up, 2 hands on head. Painted by R in pixel units, so the
    // same child sits on the school's bench outdoors and on the quiz panel.
    static readonly string[] KidShirts = { "#e04b3a", "#3f7fd0", "#f2c94a", "#5fb05f" }, KidSkins = { "#a9714b", "#c68a5a", "#8a5a3a", "#b87a50" };
    static void DrawKid(Action<int, int, int, int, string> R, int x, int y, int k, int pose, float t, bool stand = false)
    {
        string skin = KidSkins[k % 4], shirt = KidShirts[k % 4], hair = "#2b1d14", shorts = k % 2 == 0 ? "#2f4a6a" : "#7a3f5a";
        bool kick = pose == 0 && (t * 1.3f + k * 0.7f) % 2 < 0.3f;
        if (stand)
        {
            // Standing (on the flats with Lola): the whole child sits two pixels higher, on straight legs.
            y -= 2;
            R(x - 1, y - 2, 3, 1, shorts);
            R(x - 1, y - 1, 1, 2, skin); R(x + 1, y - 1, 1, 2, skin);
            R(x - 1, y + 1, 1, 1, "#3a2a20"); R(x + 1, y + 1, 1, 1, "#3a2a20");
        }
        else
        {
            // Legs over the bench edge, feet swinging.
            R(x - 1, y - 2, 3, 1, shorts);
            R(x - 1, y - 1, 1, kick ? 1 : 2, skin); R(x + 1, y - 1, 1, 2, skin);
            R(x - 1 + (kick ? -1 : 0), y + (kick ? 0 : 1), 1, 1, "#3a2a20"); R(x + 1, y + 1, 1, 1, "#3a2a20");
        }
        // Body and arms.
        R(x - 1, y - 5, 3, 3, shirt);
        if (pose == 1) { R(x - 2, y - 8, 1, 3, skin); R(x + 2, y - 8, 1, 3, skin); }
        else if (pose == 2) { R(x - 2, y - 7, 1, 2, skin); R(x + 2, y - 7, 1, 2, skin); }
        else { R(x - 2, y - 5, 1, 2, skin); R(x + 2, y - 5, 1, 2, skin); }
        // Head and hair; two of them have pigtails.
        R(x - 1, y - 8, 3, 3, skin);
        R(x - 1, y - 9, 3, 1, hair); R(x - 1, y - 8, 1, 1, hair);
        if (k % 2 == 1) { R(x - 2, y - 8, 1, 2, hair); R(x + 2, y - 8, 1, 2, hair); }
        R(x - 1, y - 7, 1, 1, "#10243a"); R(x + 1, y - 7, 1, 1, "#10243a");
    }

    Texture2D isayTex;
    void DrawIsayPortrait(float x, float y)
    {
        if (isayTex.Id == 0)
        {
            var p = new Pix(16, 18);
            DrawIsayFigure(p, 8, 16, "down", 0, 0, false);
            isayTex = Gfx.ToTexture(p.Buf, 16, 18, TextureFilter.Point);
        }
        DrawTexturePro(isayTex, new Rectangle(0, 0, 16, 18), Gfx.S(x, y, 16 * 6, 18 * 6), System.Numerics.Vector2.Zero, 0, Color.White);
    }

    static void DrawIsayFigure(Pix p, int x, int y, string face, int step, int bob, bool blink)
    {
        LookData.DrawFigure(p, "#b07a52", "#2b1d14", "#f2ecf8", "#3f4a7a", true, 0, "#e04b3a", x, y, face, step, bob: bob, blink: blink);
        // Her glasses and a pencil behind her ear.
        if (face is "down") { p.Rect(x - 2, y - 10 - bob, 2, 1, "#10243a"); p.Rect(x + 1, y - 10 - bob, 2, 1, "#10243a"); }
        p.Rect(face == "left" ? x + 2 : x - 3, y - 12 - bob, 1, 2, "#f2c94a");
    }

    /* ---------- The school, outdoors ---------- */
    // A bamboo schoolhouse with a blackboard, a flag, and the children on a bench out front in school hours.
    void DrawSchool(float x, float y)
    {
        pix.Rect(x - 24, y + 3, 48, 2, "rgba(0,0,0,0.2)");
        pix.Rect(x - 21, y - 5, 2, 10, "#65452e"); pix.Rect(x + 19, y - 5, 2, 10, "#65452e");
        pix.Rect(x - 23, y - 19, 46, 19, "#c39a5c");
        for (int i = -21; i < 23; i += 3) pix.Rect(x + i, y - 18, 1, 16, "#e2c486");
        // The blackboard, with a chalk fish on it.
        pix.Rect(x - 15, y - 15, 18, 10, "#6b4a2b");
        pix.Rect(x - 14, y - 14, 16, 8, "#2f4a3a");
        pix.Rect(x - 11, y - 11, 6, 2, "#e8f0e8"); pix.Rect(x - 12, y - 12, 1, 1, "#e8f0e8"); pix.Rect(x - 12, y - 9, 1, 1, "#e8f0e8");
        pix.Rect(x - 6, y - 12, 1, 1, "#e8f0e8"); pix.Rect(x - 4, y - 11, 4, 1, "#e8f0e8");
        // The door, a window and the trim.
        pix.Rect(x + 7, y - 12, 7, 12, "#3c342a");
        pix.Rect(x + 16, y - 13, 5, 5, "#485d56");
        pix.Rect(x - 23, y - 2, 46, 2, "#d9a43a");
        for (int i = 0; i < 13; i++) pix.Rect(x - 13 - i, y - 32 + i, 26 + i * 2, 1, i % 3 == 0 ? "#967342" : "#ceb36b");
        pix.Rect(x - 27, y - 20, 54, 2, "#80643e");
        pix.Rect(x + 6, y + 1, 9, 2, "#c7a06a");
        // A flagpole by the door.
        pix.Rect(x + 30, y - 30, 1, 34, "#c8c8c0");
        pix.Rect(x + 31, y - 30, 7, 2, "#2f4fa8"); pix.Rect(x + 31, y - 28, 7, 2, "#d8323a");
        pix.Rect(x + 31, y - 30, 2, 4, "#f2f2f2"); pix.Rect(x + 31, y - 29, 1, 1, "#f2c94a");
    }

    void DrawSchoolBench(float t)
    {
        float bx = SchoolX - 18, by = SchoolY + 16;
        pix.Rect(bx, by + 1, 36, 2, "#8a5f36"); pix.Rect(bx + 1, by + 3, 1, 2, "#6b4a2b"); pix.Rect(bx + 34, by + 3, 1, 2, "#6b4a2b");
        if (!SchoolHours) return;
        for (int k = 0; k < 4; k++)
        {
            int kx = (int)bx + 5 + k * 9, ky = (int)by + 1;
            DrawKid((px, py, pw, ph, c) => pix.Rect(px, py, pw, ph, c), kx, ky, k, 0, t);
        }
    }

    Box SchoolBenchBox => new(SchoolX - 18, SchoolY + 15, 36, 5);
}

static class QuizExt
{
    public static QuizQ With(this QuizQ q, Action<QuizQ> set) { if (q != null) set(q); return q; }
}
