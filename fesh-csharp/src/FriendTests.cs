#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Villager friendship (1.45, FESH_FRIEND_TEST=1): the data (names that really speak, gifts that exist), talking by a real
    // E press (once a day), a gift by a real F press (taken from the bag, once a day, never what a request is waiting for,
    // a big fish's premium with it), the third and fifth hearts (paid once, also across a reload), asleep villagers, the
    // panel by a real click, the Ledger, and nothing at all in the old scripts.
    IEnumerable<int> FriendScript()
    {
        Note("Villager friendship");
        OldCelebrate = false; FriendsOff = false;
        state = new State { created = true, look = new Look { name = "Neighbour" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;

        /* ---------- the data ---------- */
        var people = new HashSet<string>(Islanders.Select(n => n.id).Concat(HabagatFolk.Select(n => n.id)).Concat(MlFolk.Select(n => n.id)).Concat(new[] { "tomas", "pip", "joy" }));
        Check($"{Friends.Length} friends, each a real villager", Friends.All(f => people.Contains(f.Id)) && Friends.Select(f => f.Id).Distinct().Count() == Friends.Length);
        Check("everything they love and give is a real item, and no legend or protected animal is a gift",
            Friends.All(f => f.Loves.All(Items.ById.ContainsKey) && Items.ById.ContainsKey(f.Present) && f.Loves.All(l => !Data.FishById.TryGetValue(l, out var fish) || !fish.Legend)));
        var villagerNames = Islanders.Select(n => n.name).Concat(HabagatFolk.Select(n => n.name)).Concat(MlFolk.Select(n => n.name)).Concat(new[] { "Tomas", "Pip", "Bantay Joy" }).ToHashSet();
        Check("their names are the villagers' own names (the ones they speak under)", Friends.All(f => villagerNames.Contains(f.Name)));
        var badWords = new[] { "daughter", "Mara", "Starwell", "Tidemane", "guardian", "Platejaw", "Bakunawa", "Ironbill", "legend" };
        Check("every friend has five heart lines: ASCII, short, and nothing about the stories", Friends.All(f => HeartLines.TryGetValue(f.Id, out var hl) && hl.Length == 5
            && hl.All(s => s.Length is > 20 and <= 140 && s.All(ch => ch is >= ' ' and <= '~') && !badWords.Any(b => s.Contains(b, StringComparison.OrdinalIgnoreCase)))));
        Check("and no two lines are the same", HeartLines.Values.SelectMany(v => v).Distinct().Count() == Friends.Length * 5);
        // Codex's review: Rosa takes so many of any of her kinds, so only the total is kept back.
        int dayKeep = state.day; state.day = 2; state.hinted["metRosa"] = true;
        var (kinds, need, _) = RosaOrder;
        Give(kinds[0], need); Give("daing", 1);
        bool spare = kinds.Contains("daing") ? GiftFor("lorna") == "daing" : true;
        Take("daing");
        Give("daing", 1); Take(kinds[0]);
        bool keep = !kinds.Contains("daing") || GiftFor("lorna") != "daing";
        Check($"Rosa's order ({need} of {string.Join("/", kinds)}): one to spare can be given, none to spare can't", spare && keep);
        foreach (var k in kinds) state.inv.Remove(k);
        state.inv.Remove("daing"); state.hinted.Remove("metRosa"); state.day = dayKeep;

        /* ---------- talking ---------- */
        IEnumerable<int> Stand(float x, float y) { player.X = x; player.Y = y; player.Face = "up"; yield return 3; }
        IEnumerable<int> EndTalk() { for (int i = 0; i < 20 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; } yield return 2; }
        var lira = Islanders.First(n => n.id == "lira");
        state.hinted["amihan"] = true;
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Check($"Lira in front of you ({target?.Type}:{target?.Id})", target?.Type == "islander" && target.Id == "lira");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("talking to her is a point", FriendData("lira")?.points == 1 && FriendData("lira").talkDay == state.day);
        foreach (var s in EndTalk()) yield return s;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (var s in EndTalk()) yield return s;
        Check("talking again the same day isn't", FriendData("lira").points == 1);
        state.day++;
        Talk(new() { new("Lira", "Look at the sea!") }); yield return 2;
        foreach (var s in EndTalk()) yield return s;
        Check("a scene she speaks in, that you didn't start, isn't a talk", FriendData("lira").points == 1);
        state.day++; SetNight(false);
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (var s in EndTalk()) yield return s;
        Check("the next day it is", FriendData("lira").points == 2);

        /* ---------- gifts ---------- */
        Give("bangus", 1); state.big["bangus"] = 1;
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Check($"with a bangus, F gives it ({target?.AltLabel})", target?.AltType == "gift" && target.AltLabel.Contains("Lira"));
        Inp.Tap(KeyboardKey.F); yield return 4;
        Check("the gift is taken, with its premium, for three points", Has("bangus") == 0 && state.big.GetValueOrDefault("bangus") == 0 && FriendData("lira").points == 5 && mode == "dialogue");
        Check("her thanks isn't a second talk", FriendData("lira").talkDay == state.day && FriendData("lira").points == 5);
        foreach (var s in EndTalk()) yield return s;
        Give("bangus", 1);
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Check("one gift a day", target?.AltType != "gift");
        state.day++; SetNight(false);
        state.hinted["nikoAsked"] = true; Give("bangus", 1);   // 2 now: both are Niko's
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Check("never what a request is waiting for (Niko's two bangus)", Has("bangus") == 2 && target?.AltType != "gift");
        Give("bangus", 1);
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Check("a third bangus can be spared", target?.AltType == "gift");
        state.hinted.Remove("nikoAsked");

        /* ---------- hearts ---------- */
        celebQueue.Clear(); banner = null;
        var f = FriendData("lira");
        f.points = 23; int c0 = state.coins, gb = Has("glow_bait"); var earned0 = Achievements.Where(a => AchEarned(state, a)).Select(a => a.Id).ToHashSet();
        Inp.Tap(KeyboardKey.E); yield return 4;   // today's talk: 24
        Check("the heart brings that friend's own line into the conversation", dlg != null && dlg.Lines.Any(l => l.T == HeartLine("lira", 3)) && HeartLine("lira", 3) != null);
        foreach (var s in EndTalk()) yield return s;
        Check($"three hearts: her present ({Has("glow_bait") - gb} glow bait) and a banner", Hearts("lira") == 3 && f.got3 && Has("glow_bait") - gb == 3 && celebQueue.Concat(banner == null ? Array.Empty<Celeb>() : new[] { banner }).Any(c => c.Kind == "friend"));
        state.day++; SetNight(false);
        f.points = 52;
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Inp.Tap(KeyboardKey.F); yield return 4;   // a bangus: 55
        foreach (var s in EndTalk()) yield return s;
        Inp.Tap(KeyboardKey.E); yield return 4;   // and talk: 56
        foreach (var s in EndTalk()) yield return s;
        int achCoins = Achievements.Where(a => AchEarned(state, a) && !earned0.Contains(a.Id)).Sum(a => a.Coins);
        Check($"five hearts: {BestFriendCoins} coins once ({state.coins - c0 - achCoins}, besides {achCoins} from the Ledger)", Hearts("lira") == 5 && f.got5 && state.coins - c0 - achCoins == BestFriendCoins);
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        int c1 = state.coins, gb1 = Has("glow_bait");
        state.day++; foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Inp.Tap(KeyboardKey.E); yield return 4; foreach (var s in EndTalk()) yield return s;
        Check("after a reload nothing pays twice (and points stop at five hearts)", state.coins == c1 && Has("glow_bait") == gb1 && FriendData("lira").points == HeartAt[^1]);
        CheckLedger(); celebQueue.Clear(); banner = null;
        Check("the Ledger counts a friend and a best friend", AchEarned(state, Achievements.First(a => a.Id == "friend3")) && AchEarned(state, Achievements.First(a => a.Id == "friend5")));

        /* ---------- asleep ---------- */
        state.day++; state.clock = 23 * 60 + 30; Give("bangus", 1);
        int p0 = FriendData("lira").points;
        foreach (var s in Stand(lira.x, lira.y + 12)) yield return s;
        Inp.Tap(KeyboardKey.E); yield return 4; foreach (var s in EndTalk()) yield return s;
        Check($"asleep: no talk and no gift ({target?.Type})", target?.AltType != "gift" && FriendData("lira").talkDay != state.day);
        SetNight(false);

        /* ---------- Tomas and Pip ---------- */
        foreach (var s in Stand(tomasX, tomasY + 10)) yield return s;
        Inp.Tap(KeyboardKey.E); yield return 4; foreach (var s in EndTalk()) yield return s;
        Check("Tomas counts as a talk", FriendData("tomas")?.talkDay == state.day);
        state.clock = 12 * 60;
        foreach (var s in Stand(PipX, PipY + 15)) yield return s;
        Inp.Tap(KeyboardKey.E); yield return 4; foreach (var s in EndTalk()) yield return s;
        ClosePanels(); yield return 2;
        Check("so does a chat over Pip's counter", FriendData("pip")?.talkDay == state.day);

        /* ---------- the panel ---------- */
        mode = "play"; yield return 2;
        Inp.Tap(KeyboardKey.Q); yield return 4;
        ClickButton("Friends"); yield return 4; Inp.ScriptMouse = Offscreen; lastFriendsBottom = 0; yield return 3;
        pendingShot = "f01-friends"; yield return 4;
        Check($"the journal's Friends button opens the panel, which fits ({lastFriendsBottom:0})", mode == "panel" && panel == "friends" && lastFriendsBottom > 0 && lastFriendsBottom <= panelAt.y + panelAt.h - 12);
        foreach (var fr in Friends) state.friends.TryAdd(fr.Id, new Friendship { points = 5 });
        lastFriendsBottom = 0; yield return 3;
        bool fit = lastFriendsBottom <= panelAt.y + panelAt.h - 12;
        ClickButton("More"); yield return 3; Inp.ScriptMouse = Offscreen; lastFriendsBottom = 0; yield return 3;
        pendingShot = "f02-friends-p2"; yield return 4;
        Check($"with everyone met, it pages and still fits (page {friendsPage + 1})", fit && friendsPage == 1 && lastFriendsBottom <= panelAt.y + panelAt.h - 12);
        ClosePanels(); yield return 2;

        /* ---------- off ---------- */
        FriendsOff = true;
        int p1 = FriendData("lira").points; state.day++;
        NoteTalk("lira");
        Check("in the old scripts nothing counts", FriendData("lira").points == p1);

        OldCelebrate = true;
        yield return 2;
    }
}
#endif
