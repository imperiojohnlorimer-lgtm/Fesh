using Raylib_cs;

namespace Fesh;

// The Angler's Ledger (1.44): achievements in six groups, a page of numbers about your fishing life, and titles to wear.
// Almost everything is read from what the save already keeps; state.tally and state.sailed count the few things it
// didn't (from 1.44 on, so an older save starts those at nought). An achievement is earned once (hinted "ach:<id>"),
// pays its coins at once and shows a banner (Celebrate.cs). A secret one (a legend, a boss, Tidemane) shows "???" until
// it's yours, the way the Fish log hides them.
sealed record Ach(string Id, string Group, string Name, string Desc, Func<Game, int> Value, int Goal, int Coins = 0, string Title = null, bool Secret = false);

partial class Game
{
    static readonly string[] LedgerGroups = { "Catching", "Collecting", "Exploring", "Sea school", "Legends & stories", "Home & boat", "Mastery" };
    const double MilePx = 926;   // a nautical mile, at a tile of ten pixels being about twenty metres

    int FishCaught => state.commons.Where(kv => Data.FishById.ContainsKey(kv.Key)).Sum(kv => kv.Value);
    int SpeciesCaught => state.commons.Count(kv => kv.Value > 0 && Data.FishById.ContainsKey(kv.Key));
    int RareKinds => state.commons.Count(kv => kv.Value > 0 && Data.FishById.TryGetValue(kv.Key, out var f) && f.Rare && !f.Legend);
    int LegendsCaught => Data.AllCommon.Count(f => f.Legend && state.commons.GetValueOrDefault(f.Id) > 0);
    int Tally(string k) => state.tally.GetValueOrDefault(k);
    int Flag(string k) => state.Hinted(k) ? 1 : 0;
    int Got(string id) => state.commons.GetValueOrDefault(id) > 0 ? 1 : 0;
    int StoriesDone => (state.flags.ended ? 1 : 0) + Flag("moonReturned") + Flag("rs:done") + Flag("mg:done") + Flag("mr:done");
    int MostFurnished => state.rooms.Values.Select(r => r?.Count ?? 0).DefaultIfEmpty(0).Max();

    // Made on first use: the goals read other statics of this class (the album's chapters, the sanctuary's animals), whose
    // initialisers sit in other files and may not have run yet when this one would.
    static Ach[] achievements;
    static Ach[] Achievements => achievements ??= MakeAchievements();

    static Ach[] MakeAchievements() => BaseAchievements().Concat(ExtraAchievements()).ToArray();
    static Ach[] BaseAchievements() => new Ach[]
    {
        // Catching
        new("first", "Catching", "First bite", "Catch a fish.", g => g.FishCaught, 1, 10),
        new("fish25", "Catching", "Getting the hang of it", "Catch 25 fish.", g => g.FishCaught, 25, 30),
        new("fish100", "Catching", "Angler", "Catch 100 fish.", g => g.FishCaught, 100, 80, "Angler"),
        new("fish500", "Catching", "Master angler", "Catch 500 fish.", g => g.FishCaught, 500, 250, "Master angler"),
        new("perfect10", "Catching", "Sharp eye", "Hook 10 fish perfectly.", g => g.state.perfects, 10, 40),
        new("perfect50", "Catching", "Quick hands", "Hook 50 fish perfectly.", g => g.state.perfects, 50, 120, "Quick hook"),
        new("trophy", "Catching", "A trophy", "Land a trophy fish (half again the usual size).", g => g.state.trophies.Values.Sum(), 1, 30),
        new("trophy25", "Catching", "Trophy hunter", "Land 25 trophy fish.", g => g.state.trophies.Values.Sum(), 25, 150, "Trophy hunter"),
        new("rare", "Catching", "Something special", "Catch a rare fish.", g => g.RareKinds, 1, 40),
        new("rare10", "Catching", "Rare finds", "Catch 10 kinds of rare fish.", g => g.RareKinds, 10, 120),
        new("chests", "Catching", "Sunken treasure", "Haul up 3 sunken chests.", g => g.state.chests, 3, 50),
        new("casts", "Catching", "Five hundred casts", "Cast your line 500 times.", g => g.state.casts, 500, 80),

        // Collecting
        new("kinds5", "Collecting", "Curious angler", "Catch 5 different fish.", g => g.SpeciesCaught, 5, 20, "Curious angler"),
        new("kinds10", "Collecting", "Ten kinds", "Catch 10 different fish.", g => g.SpeciesCaught, 10, 30),
        new("kinds40", "Collecting", "Forty kinds", "Catch 40 different fish.", g => g.SpeciesCaught, 40, 100),
        new("kinds80", "Collecting", "Eighty kinds", "Catch 80 different fish.", g => g.SpeciesCaught, 80, 250, "Naturalist"),
        new("page", "Collecting", "A full page", "Fill a whole page of the Fish log.", g => Data.Biomes.Count(b => g.state.Hinted("dexdone:" + b.Id)), 1, 50),
        new("pages4", "Collecting", "Half the log", "Fill four pages of the Fish log.", g => Data.Biomes.Count(b => g.state.Hinted("dexdone:" + b.Id)), 4, 150),
        new("pages", "Collecting", "The whole Fesh-dex", "Fill every page of the Fish log.", g => Data.Biomes.Count(b => g.state.Hinted("dexdone:" + b.Id)), Data.Biomes.Length, 400, "Keeper of the log"),
        new("chapter", "Collecting", "Album chapter", "Complete a chapter of the fish album.", g => AlbumChapters.Count(c => g.state.Hinted("album:" + c.Id)), 1, 30),
        new("album", "Collecting", "The whole album", "Complete every chapter of the fish album.", g => AlbumChapters.Count(c => g.state.Hinted("album:" + c.Id)), AlbumChapters.Length, 200),
        new("aquarium", "Collecting", "On display", "Complete a collection in an aquarium.", g => Data.AquaSets.Count(a => g.SetActive(a.Id)), 1, 40),
        new("odd", "Collecting", "Odd ones out", "Hook 3 different odd catches.", g => g.state.odd.Count(kv => kv.Value > 0), 3, 50),

        // Exploring
        new("chart5", "Exploring", "Island wanderer", "Chart 5 places.", g => g.state.charted?.Count ?? 0, 5, 30, "Island wanderer"),
        new("chart15", "Exploring", "Cartographer", "Chart 15 places.", g => g.state.charted?.Count ?? 0, 15, 120, "Cartographer"),
        new("atoll", "Exploring", "Over the horizon", "Reach Starfall Atoll.", g => g.Flag("visitedAtoll"), 1, 40),
        new("amihan", "Exploring", "The Amihan islands", "Sail to the Amihan Archipelago.", g => g.Flag("amihan"), 1, 60),
        new("habagat", "Exploring", "South with the habagat", "Find the Habagat islands.", g => g.Flag("habagat"), 1, 60),
        new("marilaya", "Exploring", "The heart-shaped island", "Find Marilaya, far to the east.", g => g.Flag("marilaya"), 1, 80),
        new("cave5", "Exploring", "Into the dark", "Reach floor 5 of Frostfang Caverns.", g => g.state.caveDeepest, 5, 40),
        new("cave10", "Exploring", "Deep down", "Reach floor 10 of Frostfang Caverns.", g => g.state.caveDeepest, 10, 100),
        new("ancient", "Exploring", "The Ancient Floor", "Reach the bottom of Frostfang Caverns.", g => g.state.caveDeepest, AncientFloor, 150, "Deep diver"),
        new("sailed", "Exploring", "Sea legs", "Steer your boat 5 nautical miles.", g => (int)(g.state.sailed / MilePx), 5, 80, "Navigator"),
        new("watcher", "Exploring", "Sea watcher", "Watch every animal of the marine sanctuary.", g => SeaKinds.Keys.Count(k => g.state.sightings.GetValueOrDefault(k) > 0), SeaKinds.Count, 60),

        // Sea school
        new("lesson", "Sea school", "First lesson", "Finish one of Ma'am Isay's lessons.", g => g.state.lessons, 1, 20),
        new("lessons10", "Sea school", "Top of the class", "Finish 10 lessons.", g => g.state.lessons, 10, 80),
        new("learned", "Sea school", "Sea scholar", "Learn 5 fish in class.", g => g.LearnedCount, 5, 40, "Sea scholar"),
        new("letgo", "Sea school", "Gentle angler", "Let 5 small or closed-season fish go.", g => g.state.released, 5, 40, "Gentle angler"),
        new("sorted", "Sea school", "Crab sorter", "Sort 25 crabs right.", g => g.state.sortRight, 25, 50),
        new("riddles", "Sea school", "Tide reader", "Answer 6 of Lola Pacing's tide riddles.", g => g.state.riddles, 6, 50),
        new("reef", "Sea school", "Reef walk", "Walk the reef flats with Lola Pacing.", g => g.state.reefWalks, 1, 40),
        new("diploma", "Sea school", "Graduate", "Earn the Sea school diploma.", g => g.Flag("diploma"), 1, 0, "Graduate"),

        // Legends & stories
        new("legend", "Legends & stories", "Legendary", "Land a legendary fish.", g => g.LegendsCaught, 1, 100),
        new("legends", "Legends & stories", "Every legend", "Land every legendary fish.", g => g.LegendsCaught, Data.AllCommon.Count(f => f.Legend), 500, "Legend"),
        new("ironbill", "Legends & stories", "Towed out to sea", "Land Ironbill.", g => g.Got(TowFish), 1, 100, null, true),
        new("mire", "Legends & stories", "Out of the swamp", "Land the Mire leviathan.", g => g.Got(MireFish), 1, 100, null, true),
        new("ray", "Legends & stories", "Under the stars", "Land the Starfall ray.", g => g.Got(RayFish), 1, 100, null, true),
        new("tarpon", "Legends & stories", "King of the tarpon", "Land Haring Buan-buan.", g => g.Got(TarponFish), 1, 100, null, true),
        new("lungfish", "Legends & stories", "Breath of the oasis", "Land the Sunscale lungfish.", g => g.Got(LungfishFish), 1, 100, null, true),
        new("whiskers", "Legends & stories", "Old Whiskers", "Land Old Whiskers.", g => g.Got(WhiskersFish), 1, 100, null, true),
        new("platejaw", "Legends & stories", "Armour cracked", "Drive off Platejaw.", g => g.Flag("platejaw"), 1, 150, null, true),
        new("tidemane", "Legends & stories", "Rider of the tide", "Win Tidemane over.", g => g.state.tamed ? 1 : 0, 1, 0, "Rider of the tide", true),
        new("story1", "Legends & stories", "The Saltmere mystery", "Solve the Saltmere mystery.", g => g.state.flags.ended ? 1 : 0, 1, 150),
        new("story2", "Legends & stories", "The moon is safe", "Bring the moon back over Parola.", g => g.Flag("moonReturned"), 1, 150, null, true),
        new("story3", "Legends & stories", "The restless sea", "See Amihan Village safely uphill.", g => g.Flag("rs:done"), 1, 150),
        new("story4", "Legends & stories", "Beneath the clouds", "See Baga's story through.", g => g.Flag("mg:done"), 1, 150),
        new("story5", "Legends & stories", "The heart that rose", "Finish Aling Sela's discovery trail.", g => g.Flag("mr:done"), 1, 150),
        new("stories", "Legends & stories", "Every story told", "Finish all five stories.", g => g.StoriesDone, 5, 300, "Storyteller"),

        // Home & boat
        new("shack", "Home & boat", "A roof of your own", "Build a shack or a bahay kubo.", g => g.HasHome ? 1 : 0, 1, 40),
        new("workshop", "Home & boat", "Workshop", "Put a workbench and a furnace in your home.", g => g.HomeHas("workbench") && g.HomeHas("furnace") ? 1 : 0, 1, 60),
        new("furnish", "Home & boat", "Home sweet home", "Put 6 things in one home.", g => g.MostFurnished, 6, 60),
        new("boat", "Home & boat", "Shipwright", "Build a boat.", g => g.Has("boat") > 0 ? 1 : 0, 1, 100, "Shipwright"),
        new("bigsail", "Home & boat", "Full sail", "Rig a big sail.", g => g.Has("big_sail") > 0 ? 1 : 0, 1, 60),
        new("sounder", "Home & boat", "Ping", "Fit an echo sounder.", g => g.Has("echo_sounder") > 0 ? 1 : 0, 1, 60),
        new("regatta", "Home & boat", "Round the islets", "Finish Dado's regatta.", g => g.state.regattaBest > 0 ? 1 : 0, 1, 60),
        new("painted", "Home & boat", "Regatta gold", "Win gold in the regatta.", g => g.Has("painted_sail") > 0 ? 1 : 0, 1, 100, "Regatta champion"),
        new("derby", "Home & boat", "Derby winner", "Win Pip's derby.", g => g.state.derbyWins, 1, 40),
        new("sungka", "Home & boat", "Sungka", "Beat Lola Pacing at sungka.", g => g.state.sungkaWins, 1, 40),
        new("cooked", "Home & boat", "Island cook", "Cook 10 dishes.", g => g.Tally("cooked"), 10, 40, "Island cook"),
        new("monsters", "Home & boat", "Cave clearer", "Beat 15 cave monsters.", g => g.Tally("monsters"), 15, 60),
        // The harbour board (1.45, Bounties.cs).
        new("board", "Home & boat", "Notice taker", "Finish a bounty from the harbour board.", g => g.state.boardDone, 1, 10),
        new("board25", "Home & boat", "Board regular", "Finish 25 harbour board bounties.", g => g.state.boardDone, 25, 80, "Board regular"),
        new("streak3", "Home & boat", "Three days running", "Finish a bounty three days in a row.", g => g.state.boardBest, 3, 30),
        new("streak7", "Home & boat", "A week of notices", "Finish a bounty seven days in a row.", g => g.state.boardBest, 7, 80, "Harbour regular"),
        new("streak14", "Home & boat", "Pillar of the harbour", "Finish a bounty fourteen days in a row.", g => g.state.boardBest, 14, 150, "Pillar of the harbour"),
        // Friendship (1.45, Friends.cs).
        new("friend3", "Home & boat", "A friend", "Reach three hearts with someone.", g => g.FriendsAt(3), 1, 30),
        new("friend5", "Home & boat", "Best friends", "Reach five hearts with someone.", g => g.FriendsAt(5), 1, 80, "Good neighbour"),
        new("friends10", "Home & boat", "Everybody's friend", "Reach three hearts with ten people.", g => g.FriendsAt(3), 10, 200, "Islander"),
        // A pet (1.45, Pet.cs).
        new("pet", "Home & boat", "A friend for life", "Take a dog or a cat home.", g => g.state.pet != null ? 1 : 0, 1, 40),
    };

    static bool AchEarned(State s, Ach a) => s.Hinted("ach:" + a.Id);
    int AchValue(Ach a) { try { return Math.Max(0, a.Value(this)); } catch { return 0; } }

    float ledgerCheckT;
    // Titles you can wear: the ones your achievements gave you.
    IEnumerable<string> EarnedTitles => Achievements.Where(a => a.Title != null && AchEarned(state, a)).Select(a => a.Title);

    // Counts something for the Ledger (never in a match: that's a sandbox).
    void Tick(string k, int n = 1) { if (!MatchActive) state.tally[k] = Tally(k) + n; }

    // The title you wear, if it's still one you've earned (the picker only offers those; a damaged save can't wear another).
    string WornTitle => state.title is { Length: > 0 } t && EarnedTitles.Contains(t) ? t : null;

    // What a page of the Ledger shows: a secret stays off it entirely until it's yours (its name, its count, its title).
    IEnumerable<Ach> Shown(string group) => Achievements.Where(a => a.Group == group && (!a.Secret || AchEarned(state, a)));

    // Once a second (and after a catch): pays anything newly reached. A save from before the Ledger gets everything it
    // had already done in one go, with one card, not a banner each.
    void UpdateLedger(float dt)
    {
        if (MatchActive || OldCelebrate || mode is "title" or "create" || state == null || !state.created) return;
        ledgerCheckT -= dt;
        if (ledgerCheckT > 0) return;
        ledgerCheckT = 1;
        CheckLedger();
    }

    // Marks and pays everything reached, all of it before anything is shown or saved. Several at once share one banner.
    void CheckLedger()
    {
        if (MatchActive || OldCelebrate || state == null || !state.created) return;
        bool first = !state.Hinted("ledger");
        state.hinted["ledger"] = true;
        var news = new List<Ach>();
        int coins = 0;
        foreach (var a in Achievements)
        {
            if (AchEarned(state, a) || AchValue(a) < a.Goal) continue;
            state.hinted["ach:" + a.Id] = true;
            state.coins += a.Coins;
            coins += a.Coins;
            news.Add(a);
        }
        int n = news.Count;
        if (!first && n > 0)
        {
            var titles = news.Where(a => a.Title != null).Select(a => $"\"{a.Title}\"").ToList();
            string got = (coins > 0 ? $"{coins} coins" : "") + (titles.Count > 0 ? (coins > 0 ? " and " : "") + (titles.Count == 1 ? "the title " : "the titles ") + string.Join(", ", titles) : "");
            Celebrate(new Celeb
            {
                Kind = "ach", Icon = "star", Sting = "badge", Tag = n == 1 ? "Achievement" : $"{n} achievements",
                Title = n == 1 ? news[0].Name : string.Join(", ", news.Select(a => a.Name)),
                Sub = (n == 1 ? news[0].Desc + " " : "") + (got != "" ? $"You get {got}." : ""), Note = "See your Angler's Ledger in the journal (<journal>)."
            }, null);
        }
        if (first && n > 0)
        {
            var c = new Celeb { Kind = "ledger", Card = true, Icon = "star", Sting = "page", Tag = "New: the Angler's Ledger", Title = "Your Angler's Ledger", Button = "Let's see" };
            c.Body.Add($"{n} achievements already earned by your fishing so far: {coins} coins for them.");
            c.Body.Add("Open it from the journal (<journal>).");
            c.Stats.Add($"{n} of {LedgerGroups.Sum(g => Shown(g).Count())} achievements");
            Celebrate(c, null);
        }
    }

    /* ---------- The panel ---------- */
    string ledgerTab = "Catching";
    float lastLedgerBottom;

    void OpenLedger()
    {
        CheckLedger();
        Sfx.Play("ui");
        panel = "ledger";
        mode = "panel";
        SetPrompt("");
    }

    void DrawLedger()
    {
        int earned = Achievements.Count(a => AchEarned(state, a)), known = LedgerGroups.Sum(g => Shown(g).Count());
        var (x, y, w, h) = PanelFrame("Angler's Ledger", $"{earned} of {known} achievements · {SlotTitleNow()}");
        if (PanelClose()) { ClosePanels(); return; }
        if (SmallButton("Journal", PanelCloseX - 12 - SmallW("Journal"), y + PanelPad - 6)) { panel = "journal"; Sfx.Play("ui"); return; }
        // Tabs down the left: the six groups, then the numbers.
        float tx = x + PanelPad, ty = y + 104, tw = 230;
        foreach (var g in LedgerGroups.Append("Numbers"))
        {
            var list = Shown(g).ToList();
            string label = g == "Numbers" ? g : $"{g}  {list.Count(a => AchEarned(state, a))}/{list.Count}";
            bool on = ledgerTab == g;
            if (Button(label, tx, ty, tw, 44, FontKind.Ui700, 17, on ? Pal.Lantern : Pal.Sand, Pal.Ink, 2, 2, 5)) { ledgerTab = g; Sfx.Play("ui"); }
            Gfx.Seen["ledger:" + g] = new Rectangle(tx, ty, tw, 44);
            ty += 52;
        }
        // The title you wear.
        ty += 10;
        Gfx.Text("Your title", tx, ty, FontKind.Ui700, 17, Pal.PaperInk);
        ty += 26;
        var titles = EarnedTitles.ToList();
        if (titles.Count == 0) Lines(Gfx.Wrap("Some achievements give you a title to wear.", FontKind.Note, 17, tw), tx, ty, 22, FontKind.Note, 17, Muted);
        else
        {
            var opts = new List<string> { "" };
            opts.AddRange(titles);
            int at = Math.Max(0, opts.IndexOf(state.title ?? ""));
            if (Button("<", tx, ty, 34, 34, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 2, 2, 5)) { state.title = opts[(at + opts.Count - 1) % opts.Count]; Sfx.Play("ui"); Save(); }
            if (Button(">", tx + tw - 34, ty, 34, 34, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 2, 2, 5)) { state.title = opts[(at + 1) % opts.Count]; Sfx.Play("ui"); Save(); }
            Gfx.Seen["ledger:title-next"] = new Rectangle(tx + tw - 34, ty, 34, 34);
            string t = state.title is { Length: > 0 } ? state.title : "(none)";
            Gfx.TextCenter(Gfx.Ellipsize(t, FontKind.Ui600, 16, tw - 80), tx + tw / 2, ty + 8, FontKind.Ui600, 16, Pal.PaperInk);
        }

        float lx = x + PanelPad + tw + 24, lw = x + w - PanelPad - lx, ly = y + 104;
        if (ledgerTab == "Numbers") { DrawLedgerNumbers(lx, ly, lw); return; }
        var rows = Shown(ledgerTab).ToList();
        const float gap = 10;
        int cols = rows.Count > 12 ? 3 : 2, perCol = Math.Max(1, (rows.Count + cols - 1) / cols);
        float cw = (lw - gap) / cols, rh = MathF.Min(84, (y + h - PanelPad - ly - gap * (perCol - 1)) / perCol);
        for (int i = 0; i < rows.Count; i++)
        {
            var a = rows[i];
            float rx = lx + (i / perCol) * (cw + gap), ry = ly + (i % perCol) * (rh + gap);
            DrawAchRow(a, rx, ry, cw, rh);
            lastLedgerBottom = MathF.Max(lastLedgerBottom, ry + rh);
        }
    }

    string SlotTitleNow() => WornTitle is string t ? $"{state.look.name}, {t}" : state.look.name;

    void DrawAchRow(Ach a, float x, float y, float w, float h)
    {
        bool done = AchEarned(state, a), hidden = false;   // a secret isn't listed at all until it's earned (Shown)
        int v = AchValue(a);
        Gfx.Box(x, y, w, h, done ? Pal.C("#fff3cf") : CardBg, done ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 5);
        // A medal: gold when earned, a grey ring before.
        float mx = x + 30, my = y + h / 2;
        Gfx.Circle(mx, my, 19, done ? Pal.C("#9a6a1a") : Pal.C("#cfc3a8"));
        Gfx.Circle(mx, my, 15, done ? Pal.C("#e8b83a") : Pal.C("#ece4d2"));
        if (done) DrawIcon("star", mx - 11, my - 11, 22);
        else Gfx.TextCenter(hidden ? "?" : "", mx, my - 10, FontKind.Ui700, 18, Muted);
        float tx = x + 60, tw = w - 72;
        Gfx.Text(Gfx.Ellipsize(hidden ? "???" : a.Name, FontKind.Ui700, 17, tw), tx, y + 8, FontKind.Ui700, 17, Pal.PaperInk);
        string desc = hidden ? "A secret. Keep fishing and exploring." : a.Desc;
        Gfx.Text(Gfx.Ellipsize(desc, FontKind.Ui500, 14, tw), tx, y + 30, FontKind.Ui500, 14, Muted);
        string reward = (a.Coins > 0 ? $"{a.Coins} coins" : "") + (a.Title != null && !hidden ? (a.Coins > 0 ? " · " : "") + $"title \"{a.Title}\"" : "");
        float by = y + h - 18;
        if (done) Gfx.Text(Gfx.Ellipsize("Earned" + (reward != "" ? ": " + reward : ""), FontKind.Ui600, 13, tw), tx, by - 4, FontKind.Ui600, 13, Pal.C("#9a6a1a"));
        else if (!hidden)
        {
            // The bar with its count, and what it pays above it on the right.
            string count = $"{Math.Min(v, a.Goal)}/{a.Goal}";
            float cw = Gfx.Measure(count, FontKind.Ui600, 12) + 8, bw = tw - cw, frac = Math.Clamp(v / (float)a.Goal, 0, 1);
            Gfx.Rect(tx, by, bw, 6, Pal.C("#e9e0cc"), 3);
            Gfx.Rect(tx, by, bw * frac, 6, Pal.C("#3f8a4a"), 3);
            Gfx.Text(count, tx + bw + 6, by - 5, FontKind.Ui600, 12, Muted);
            if (reward != "") Gfx.Text(Gfx.Ellipsize(reward, FontKind.Ui600, 13, tw), tx, by - 20, FontKind.Ui600, 13, Pal.C("#8a6a2a"));
        }
    }

    // The numbers of a fishing life.
    List<(string label, string value)> LedgerNumbers()
    {
        var heaviest = state.records.Where(kv => Data.FishById.ContainsKey(kv.Key)).OrderByDescending(kv => kv.Value).FirstOrDefault();
        string Heaviest() => heaviest.Key == null ? "none yet" : $"{Data.FishById[heaviest.Key].Name}, {Kg(heaviest.Value)}";
        return new()
        {
            ("Fish caught", FishCaught.ToString()),
            ("Kinds of fish", $"{SpeciesCaught} of {Data.AllCommon.Count()}"),
            ("Heaviest fish", Heaviest()),
            ("Trophy fish", state.trophies.Values.Sum().ToString()),
            ("Perfect hooks", state.perfects.ToString()),
            ("Casts", state.casts.ToString()),
            ("Legends landed", LegendsCaught.ToString()),
            ("Sunken chests", state.chests.ToString()),
            ("Fish let go", state.releasedAll.ToString()),
            ("Fishing level", FishLevel >= 10 ? $"10 · mastery {MasteryLevel} ({state.xp} XP)" : $"{FishLevel} ({state.xp} XP)"),
            ("Gold and master trophies", $"{state.golds} gold, {state.masters} master"),
            ("Days on the islands", state.day.ToString()),
            ("Time played", PlayTime(state.playSecs)),
            ("Places charted", (state.charted?.Count ?? 0).ToString()),
            ("Nautical miles sailed", $"{state.sailed / MilePx:0.0}"),
            ("Deepest cave floor", state.caveDeepest.ToString()),
            ("Cave monsters beaten", Tally("monsters").ToString()),
            ("Fish sold to Pip", Tally("sold").ToString()),
            ("Coins from fish", Tally("earned").ToString()),
            ("Dishes cooked", Tally("cooked").ToString()),
            ("Things made", Tally("crafted").ToString()),
            ("Trees and rocks cleared", Tally("felled").ToString()),
            ("Ore mined", Tally("mined").ToString()),
            ("Derby wins", state.derbyWins.ToString()),
            ("Best regatta", state.regattaBest > 0 ? RaceTime(state.regattaBest) : "not yet"),
            ("Bounties done", state.boardDone.ToString()),
            ("Best run of bounty days", state.boardBest.ToString()),
        };
    }

    void DrawLedgerNumbers(float x, float y, float w)
    {
        var nums = LedgerNumbers();
        int perCol = (nums.Count + 1) / 2;
        float cw = (w - 20) / 2;
        for (int i = 0; i < nums.Count; i++)
        {
            float rx = x + (i / perCol) * (cw + 20), ry = y + (i % perCol) * 40;
            Gfx.Rect(rx, ry, cw, 34, i % 2 == 0 ? CardBg : Pal.C("#f3ead6"), 4);
            Gfx.Text(nums[i].label, rx + 12, ry + 8, FontKind.Ui500, 16, Pal.PaperInk);
            string val = Gfx.Ellipsize(nums[i].value, FontKind.Ui700, 16, cw - 24 - Gfx.Measure(nums[i].label, FontKind.Ui500, 16) - 12);
            Gfx.Text(val, rx + cw - 12 - Gfx.Measure(val, FontKind.Ui700, 16), ry + 8, FontKind.Ui700, 16, Pal.PaperInk);
            lastLedgerBottom = MathF.Max(lastLedgerBottom, ry + 34);
        }
    }
}
