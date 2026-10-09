using Raylib_cs;

namespace Fesh;

// The Sea school (1.17): three badges, each bronze, silver and gold, for Ma'am Isay's class (fish learned), letting
// fish go (Release.cs) and the tides (Tides.cs: Lola Pacing's riddles, and reef walks counting double). Each tier pays
// out once (hinted "badge:<id>:<tier>"); all three gold is the diploma. Nothing needs them: they're there for the
// curious, in the journal's "Sea school" tab.
sealed record Badge(string Id, string Name, string Where, int[] Goals, string[] Prizes);

partial class Game
{
    static readonly Badge[] Badges =
    {
        new("fishid", "Fish ID", "Fish learned in Ma'am Isay's class (Amihan Village)", new[] { 5, 15, 30 },
            new[] { "field_guide", "coins:150", "gold_star_pin" }),
        new("letgo", "Let it go", "Undersized and closed-season fish let go, and crabs sorted back", new[] { 5, 15, 40 },
            new[] { "dehooker", "coins:150", "steward_badge" }),
        new("tides", "Tides", "Lola Pacing's tide riddles (a reef walk counts two)", new[] { 2, 6, 14 },
            new[] { "coins:60", "gleaner_basket", "tide_watch" })
    };
    const int DiplomaCoins = 300;
    static readonly string[] TierNames = { "bronze", "silver", "gold" };

    int BadgeValue(string id) => id switch { "fishid" => LearnedCount, "letgo" => state.released, _ => state.riddles + 2 * state.reefWalks };
    int BadgeTier(Badge b) => b.Goals.Count(g => BadgeValue(b.Id) >= g);

    static string PrizeText(string prize) => prize.StartsWith("coins:") ? $"{prize[6..]} coins" : Items.ById[prize].Name.ToLowerInvariant();

    string badgeNews;           // a prize won while a panel or a conversation was up, told once you're back in play
    List<string> lessonPrizes;  // prizes won during a lesson, for its results card

    // Prize news always queues: whatever message is up (a haul, a release, a panel) has its turn first.
    void BadgeNews(string msg) => ToastLater(msg);

    // A message for once the toast showing now has had its turn (and you're back in play).
    void ToastLater(string msg) => badgeNews = badgeNews == null ? msg : badgeNews + " " + msg;

    void TellBadgeNews()
    {
        if (badgeNews == null || mode != "play" || toastTimer > 0.5f) return;
        Toast(badgeNews, 6);
        badgeNews = null;
    }

    // Pays out any tier just reached. Called after anything that moves a badge on.
    void CheckBadges()
    {
        foreach (var b in Badges)
            for (int t = 0; t < 3; t++)
            {
                string key = $"badge:{b.Id}:{t}";
                if (BadgeValue(b.Id) < b.Goals[t] || state.Hinted(key)) continue;
                state.hinted[key] = true;
                string prize = b.Prizes[t];
                if (prize.StartsWith("coins:")) state.coins += int.Parse(prize[6..]);
                else Give(prize);
                Sfx.Play(t == 2 ? "rare" : "coin");
                string got = prize.StartsWith("coins:") ? PrizeText(prize) : "a " + PrizeText(prize);
                lessonPrizes?.Add($"{TierNames[t]} for {b.Name}: {got}");
                BadgeNews($"Sea school: {TierNames[t]} for {b.Name}! You get {got}. See your journal (<journal>).");
            }
        if (!state.Hinted("diploma") && Badges.All(b => BadgeTier(b) == 3))
        {
            state.hinted["diploma"] = true;
            state.coins += DiplomaCoins;
            Sfx.Play("rare");
            lessonPrizes?.Add($"the Sea school diploma: {DiplomaCoins} coins");
            BadgeNews($"Sea school diploma! Gold in every badge. Ma'am Isay's class sends {DiplomaCoins} coins and a very loud cheer.");
        }
    }

    /* ---------- The journal's Sea school tab ---------- */
    string journalSide = "basics";      // the journal's right-hand column: "basics" (Getting started) or "school"

    float DrawSeaSchool(float rx, float ry, float rw)
    {
        float y = ry;
        bool grad = state.Hinted("diploma");
        Gfx.Text(grad ? "Graduate of the Sea school" : "Three badges, for the curious", rx, y, FontKind.Ui600, 15, grad ? Pal.C("#9a6a1a") : Muted);
        y += 26;
        foreach (var b in Badges)
        {
            int tier = BadgeTier(b), val = BadgeValue(b.Id);
            Gfx.Box(rx, y, rw, 104, CardBg, Pal.C("#c9b48f"), 2, 5);
            DrawBadgeIcon(b.Id, rx + 22, y + 26, tier);
            Gfx.Text(b.Name, rx + 46, y + 8, FontKind.Ui700, 17, Pal.PaperInk);
            for (int t = 0; t < 3; t++)
            {
                var c = t < tier ? (t == 0 ? Pal.C("#b87a3a") : t == 1 ? Pal.C("#9aa8b4") : Pal.C("#e8b83a")) : Pal.C("#e2d6bc");
                Gfx.Circle(rx + rw - 54 + t * 18, y + 18, 7, c);
            }
            var where = Gfx.Wrap(b.Where, FontKind.Ui500, 13, rw - 58);
            Lines(where.Take(2).ToList(), rx + 46, y + 32, 16, FontKind.Ui500, 13, Muted);
            string next = tier < 3 ? $"{val} of {b.Goals[tier]}: next, {PrizeText(b.Prizes[tier])}" : $"{val}: all three earned";
            Gfx.Text(Gfx.Ellipsize(next, FontKind.Ui600, 14, rw - 58), rx + 46, y + 70, FontKind.Ui600, 14, tier < 3 ? Pal.PaperInk : Pal.C("#9a6a1a"));
            float bw = rw - 58, frac = tier < 3 ? Math.Clamp(val / (float)b.Goals[tier], 0, 1) : 1;
            Gfx.Rect(rx + 46, y + 90, bw, 6, Pal.C("#e9e0cc"), 3);
            Gfx.Rect(rx + 46, y + 90, bw * frac, 6, tier == 3 ? Pal.C("#e8b83a") : Pal.C("#3f8a4a"), 3);
            y += 112;
        }
        // A few numbers, and the tide table once Lola has given it to you.
        string facts = $"Back grown: {state.grownBack} · crabs sorted right: {state.sortRight}{(state.Hinted("quickSort") ? " (by eye now)" : "")}";
        Gfx.Text(Gfx.Ellipsize(facts, FontKind.Ui500, 13, rw), rx, y, FontKind.Ui500, 13, Muted);
        y += 24;
        // The tide table once Lola has given it to you, the fish album and the island guide.
        float bx = rx;
        if (TidesLearned) { if (Button("Tide table", bx, y, 104, 36, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 3, 2, 5)) { OpenTides(); return -1; } bx += 112; }
        if (Button("Fish album", bx, y, 104, 36, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 3, 2, 5)) { OpenAlbum(); return -1; }
        bx += 112;
        if (Button("Island guide", bx, y, 118, 36, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 3, 2, 5)) { OpenAtlas(HereRegion()); return -1; }
        y += 44;
        return y;
    }

    // A little badge: a fish, a fish going home, or a wave, on a disc the colour of the best tier earned.
    static void DrawBadgeIcon(string id, float x, float y, int tier)
    {
        var disc = tier == 0 ? Pal.C("#d8cbb0") : tier == 1 ? Pal.C("#b87a3a") : tier == 2 ? Pal.C("#9aa8b4") : Pal.C("#e8b83a");
        Gfx.Circle(x, y, 17, disc);
        Gfx.Circle(x, y, 13, Pal.C("#fffaf0"));
        var ink = Pal.C("#1d5a88");
        switch (id)
        {
            case "fishid":
                Gfx.Circle(x - 1, y, 6, ink); Gfx.Triangle(x + 4, y, x + 10, y - 5, x + 10, y + 5, ink); Gfx.Circle(x - 4, y - 1, 1.4f, Pal.Paper);
                break;
            case "letgo":
                Gfx.Line(x - 9, y + 6, x + 9, y + 6, 2, ink);
                Gfx.Circle(x - 1, y - 2, 4, ink); Gfx.Triangle(x + 2, y - 2, x + 7, y - 6, x + 7, y + 2, ink);
                break;
            default:
                for (int i = 0; i < 3; i++) Gfx.Circle(x - 6 + i * 6, y + 2, 4, ink);
                Gfx.Rect(x - 10, y + 2, 20, 5, ink);
                Gfx.Rect(x - 10, y - 2, 20, 4, Pal.C("#fffaf0"));
                break;
        }
    }

    /* ---------- The guide ---------- */
    // Today's lesson and Lola's riddle are daily goals (never followed by themselves); the reef walk she booked is a
    // request; meeting Ma'am Isay is part of meeting Amihan's people (Guide.cs), and learning the tides is exploring.
    void SchoolGoals(List<Goal> list)
    {
        if (state.Hinted("metIsay") && LessonToday && KnownFish().Count >= MinKnown)
        {
            var g = NewGoal("isay:lesson", "daily", "Today's lesson with Ma'am Isay", "Eight questions on the fish you've caught, at the school in Amihan Village. The first lesson each day pays.");
            list.Add(AtIslander(g, "isay"));
        }
        if (!state.Hinted("habagat")) return;
        if (state.reefLow >= 0 && !ReefThanked)
        {
            var (start, end) = LowWindow(state.reefLow);
            var g = NewGoal("pacing:reef", "request", "Reef walk with Lola Pacing", "");
            var (sx, sy) = ReefSpot();
            // A big spring low dries the reef flat; a smaller one is a walk along the beach (Lola says the same).
            bool reef = LowDepth(state.reefLow) >= -FlatLevel;
            string where = reef ? "on the flats" : "on the beach";
            if (ReefWalkOn) { g.Text = $"The tide's out and she's {where} below her sungka board with two of her apo. Go and say hello."; g.Ready = true; }
            else g.Text = $"Meet her {where} below her sungka board on Daang Pulo at the low tide, {TideClock(LowAt(state.reefLow))}: "
                + (reef ? $"the reef flat dries out around then, and you can glean from {TideClock(start)}." : $"not a big spring tide, so the reef stays under, but you can glean from {TideClock(start)}.");
            list.Add(At(g, sx, sy, "Lola Pacing"));
        }
        if (RiddleToday)
            list.Add(AtHabagat(NewGoal("pacing:riddle", "daily", "Lola Pacing's tide riddle", "Read the tide table and answer her question (<alt> by her sungka board)."), "pacing"));
        else if (state.Hinted("metPacing") && !TidesLearned)
            list.Add(AtHabagat(NewGoal("explore:tides", "explore", "Ask Lola Pacing about the tides", "She knows the flats of Daang Pulo better than anyone. Talk to her by her sungka board."), "pacing"));
    }
}
