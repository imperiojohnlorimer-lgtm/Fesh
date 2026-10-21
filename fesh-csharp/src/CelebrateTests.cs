#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // The celebration ladder (1.44, FESH_CELEBRATE_TEST=1): which moments get a banner or a card, one banner per catch
    // with the right priority, the day's biggest fish, a double catch, cards that wait until nothing else is going on
    // (a fish just landed, a fight, the coelacanth's guardian rising), closing a card by key, click and Esc without the
    // key leaking, levels merging, and the old toasts when the ladder is off.
    IEnumerable<int> CelebrateScript()
    {
        Note("Celebrations: banners and cards");
        state = new State { created = true, look = new Look { name = "Cheerer" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        // The Angler's Ledger (Ledger.cs) would add its own banners to the queue these checks read: it has all it needs.
        foreach (var a in Achievements) state.hinted["ach:" + a.Id] = true;
        yield return 3;
        var perch = Data.FishById["pond_perch"];
        var (lx, ly) = StandNear("lagoon");
        player.X = lx; player.Y = ly; player.Face = "up";
        void Land(string id)
        {
            var f = Data.FishById[id];
            fish = new FishCast { Spot = "lagoon", Bx = player.X, By = player.Y - 20 };
            reel = new ReelState { Roll = new Catchable { Id = id, Name = f.Name } };
            mode = "reeling";
            LandCatch();
        }
        IEnumerable<int> Settle() { heldT = 0; landed = null; toastTimer = 0; for (int i = 0; i < 4; i++) yield return 1; }
        void Quiet() { celebQueue.Clear(); banner = null; heldT = 0; landed = null; toastTimer = 0; badgeNews = null; }
        // A card waits for any toast still to come (hints queued by a first catch), so these give it time.
        IEnumerable<int> UntilCard() { for (int i = 0; i < 600 && mode != "celebrate"; i++) { if (badgeNews == null && toastTimer > 0) toastTimer = 0; yield return 1; } }

        /* ---------- with the ladder off (test runs), the old toasts ---------- */
        OldCelebrate = true;
        Land("pond_perch"); yield return 2;
        Check($"with the ladder off a first catch is the old toast ({toastMsg})", toastMsg.StartsWith("Caught a pond perch") && celebQueue.Count == 0 && banner == null);
        state.xp = XpFor(2) - 1; GainXp(5);
        Check($"and a level is the old toast, no card ({toastMsg})", toastMsg.Contains("Fishing level 2") && celebQueue.Count == 0 && mode != "celebrate");
        foreach (var s in Settle()) yield return s;
        OldCelebrate = false;

        /* ---------- a first catch: a banner, not the toast ---------- */
        state.commons.Remove("mudskipper"); toastMsg = ""; toastTimer = 0;
        string firstId = Data.Common["lagoon"].Select(f => f.Id).FirstOrDefault(id => state.commons.GetValueOrDefault(id) == 0 && !Data.FishById[id].Legend) ?? "pond_perch";
        Land(firstId); yield return 2;
        Check($"a new species gets a banner ({banner?.Tag}: {banner?.Title}) and no plain toast ({toastMsg})",
            banner?.Kind == "fish" && banner.Tag.StartsWith("New species") && banner.Fish == firstId && !toastMsg.StartsWith("Caught a"));
        Check("its banner says which fish it is in your Fesh-dex and points at the card", banner.Sub.Contains("fish no.") && banner.Note.Contains("Fesh-dex"));
        yield return 30; pendingShot = "c01-new-species"; yield return 4;
        Check($"the banner sits under the top buttons, inside the screen ({lastBannerBottom:0})", lastBannerBottom > 120 && lastBannerBottom < Gfx.LH / 2);
        float t0 = bannerT;
        mode = "panel"; panel = "bag"; yield return 10;
        Check("a banner waits behind a panel (it doesn't count down)", MathF.Abs(bannerT - t0) < 0.001f);
        ClosePanels(); mode = "play";
        for (int i = 0; i < 400 && banner != null; i++) yield return 1;
        Check("then it goes by itself", banner == null);
        foreach (var s in Settle()) yield return s;

        /* ---------- an ordinary catch is a toast ---------- */
        Quiet(); state.cotdDay = state.day; state.cotdId = "pond_perch"; state.cotdM = 9; cotdCool = 0;
        state.records["pond_perch"] = 999;
        Land("pond_perch"); yield return 2;
        Check($"an ordinary catch is still the toast ({toastMsg})", toastMsg.StartsWith("Caught a pond perch") && banner == null && celebQueue.Count == 0);
        foreach (var s in Settle()) yield return s;

        /* ---------- which news wins ---------- */
        // A first trophy, a record, the day's biggest: one banner, the best thing first.
        Quiet(); state.trophies.Remove("pond_perch"); state.records["pond_perch"] = 0.01f; state.cotdDay = 0;
        catchNews = null; AddCatch(perch, 1, false, 1.6f);
        Check("a trophy size is noted as the first trophy, and as a record", catchNews.FirstTrophy && catchNews.Record);
        CelebrateCatch(perch, catchNews, 5); catchNews = null;
        Check($"a first trophy beats a record ({celebQueue.LastOrDefault()?.Tag}: {celebQueue.LastOrDefault()?.Sub})",
            celebQueue.Count == 1 && celebQueue[0].Tag == "Trophy!" && celebQueue[0].Sub.Contains("new record"));
        Quiet();
        catchNews = null; AddCatch(perch, 1, false, 1.62f); bool again = CelebrateCatch(perch, catchNews, 6); catchNews = null;
        Check("a second trophy of the same fish is no banner by itself (unless it's a big record)", !again || celebQueue.All(c => c.Tag != "Trophy!"));
        Quiet();
        // A record only counts for a banner at a tenth over the old one.
        state.records["pond_perch"] = perch.Kg * 1.25f; state.cotdDay = state.day; state.cotdM = 9;
        bool small = CelebrateCatch(perch, new CatchNews { Record = true, RecordWas = 1.0f, Kg = 1.05f, M = 1.1f }, 7);
        Check("a record by a hair (5% over) is a floater, not a banner", !small && celebQueue.Count == 0);
        state.records["pond_perch"] = perch.Kg * 0.9f; state.trophies["pond_perch"] = 3;
        catchNews = null; AddCatch(perch, 1, false, 1.3f); bool rec = CelebrateCatch(perch, catchNews, 8); catchNews = null;
        Check($"a record by a tenth or more is a banner ({celebQueue.LastOrDefault()?.Sub})", rec && celebQueue.Last().Tag == "New record!" && celebQueue.Last().Sub.Contains("up from"));
        Quiet();

        /* ---------- the day's biggest ---------- */
        state.cotdDay = 0; state.records["pond_perch"] = 999; cotdCool = 0;
        catchNews = null; AddCatch(perch, 1, false, 0.9f); bool firstOfDay = CelebrateCatch(perch, catchNews, 9); catchNews = null;
        Check("the day's first fish sets the day's biggest quietly", !firstOfDay && state.cotdDay == state.day && state.cotdId == "pond_perch");
        state.cotdM = 0.9f;   // (its size is random: pin it, so the next one is bigger)
        catchNews = null; AddCatch(perch, 1, false, 1.3f); bool beat = CelebrateCatch(perch, catchNews, 10); catchNews = null;
        Check($"a big one that beats it is a banner ({celebQueue.LastOrDefault()?.Tag})", beat && celebQueue.Last().Tag.StartsWith("Biggest of the day"));
        catchNews = null; AddCatch(perch, 1, false, 1.4f); bool soon = CelebrateCatch(perch, catchNews, 11); catchNews = null;
        Check($"another straight after is not (a couple of minutes between), but it is still the day's biggest ({state.cotdM:0.00})", !soon && state.cotdM >= 1.4f);
        Check("a waiting banner for the same fish is replaced, not stacked", celebQueue.Count(c => c.Fish == "pond_perch") <= 1);
        state.day++;
        catchNews = null; AddCatch(perch, 1, false, 0.7f);
        Check("a new day starts a new biggest (its first fish, quietly)", state.cotdDay == state.day && !catchNews.Cotd);
        catchNews = null;
        Quiet();

        /* ---------- a double catch: both fish count ---------- */
        state.trophies.Remove("pond_perch");
        catchNews = null; AddCatch(perch, 1, false, 0.8f); AddCatch(perch, 1, false, 1.7f);
        Check($"a double catch's second fish adds to the news ({catchNews.M:0.00}, first trophy {catchNews.FirstTrophy})", catchNews.FirstTrophy && catchNews.M >= 1.7f);
        catchNews = null; Quiet();

        /* ---------- a level: a card, once you're free ---------- */
        state.xp = XpFor(3) - 1; mode = "reeling"; GainXp(5);
        Check("a level gained mid-fight waits", mode == "reeling" && celebQueue.Any(c => c.Kind == "level"));
        mode = "play"; heldT = 1.2f; yield return 5;
        Check("and waits while the fish is held up", mode == "play");
        heldT = 0; toastMsg = "A toast to read first"; toastTimer = 1; yield return 5;
        Check("and while a toast is still up", mode == "play");
        toastTimer = 0; foreach (var s in UntilCard()) yield return s;
        Check($"then the card opens ({celebCard?.Title})", mode == "celebrate" && celebCard?.Title == "Fishing level 3" && celebCard.Body.Count == 1);
        pendingShot = "c02-level"; yield return 10;
        Check($"the level card fits the screen ({lastCelebBottom:0})", lastCelebBottom <= Gfx.LH - 8);
        catchOpenedAt = GetTime(); Inp.Tap(KeyboardKey.E); yield return 2;
        Check("it can't be closed in its first moment", mode == "celebrate");
        yield return 40;
        // E closes it, and the same E doesn't cast at the lagoon in front of you.
        player.X = lx; player.Y = ly; player.Face = "up";
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"E closes it without casting ({mode}, fish {fish != null})", mode == "play" && fish == null && celebCard == null);

        // Two levels before the card shows are one card.
        state.xp = XpFor(4) - 1; mode = "reeling"; GainXp(2);
        state.xp = XpFor(5) - 1; GainXp(2);
        Check($"two levels gained before the card shows are one card ({celebQueue.Count(c => c.Kind == "level")})", celebQueue.Count(c => c.Kind == "level") == 1
            && celebQueue.First(c => c.Kind == "level").Body.Count == 2 && celebQueue.First(c => c.Kind == "level").Title == "Fishing level 5");
        mode = "play"; foreach (var s in UntilCard()) yield return s;
        yield return 40;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"Esc closes a card and doesn't open the pause menu ({mode})", mode == "play");
        // Its real button.
        state.xp = XpFor(6) - 1; GainXp(2);
        foreach (var s in UntilCard()) yield return s;
        yield return 40;
        ClickButton("Back to fishing");
        for (int i = 0; i < 6 && mode == "celebrate"; i++) yield return 1;
        Inp.ScriptMouse = Offscreen;
        Check("its button closes it", mode == "play");

        /* ---------- a whole Fish log page: a card ---------- */
        int page = PageOf("pond_perch");
        var pf = PageFish(page);
        state.hinted.Remove("dexdone:" + Data.Biomes[page].Id);
        foreach (var f in pf) state.commons[f.Id] = 1;
        state.commons[pf[^1].Id] = 0;
        int coins = state.coins; Quiet();
        AddCatch(pf[^1]); catchNews = null;
        Check($"the last fish of a page pays ({state.coins - coins}) and queues its card", state.coins - coins >= PageReward && celebQueue.Any(c => c.Kind == "page"));
        heldT = 0; landed = null; toastTimer = 0; mode = "play";
        foreach (var s in UntilCard()) yield return s;
        pendingShot = "c03-page"; yield return 10;
        Check($"the page card opens and fits ({celebCard?.Title}, {lastCelebBottom:0})", mode == "celebrate" && celebCard.Kind == "page" && lastCelebBottom <= Gfx.LH - 8);
        yield return 30; Inp.Tap(KeyboardKey.E); yield return 3;

        /* ---------- a Sea school badge: a banner ---------- */
        Quiet(); state.released = 5; state.hinted.Remove("badge:letgo:0");
        CheckBadges();
        Check($"a badge tier is a banner ({celebQueue.LastOrDefault()?.Tag})", celebQueue.Any(c => c.Kind == "badge" && c.Tag.Contains("bronze")) && badgeNews == null);
        yield return 30;
        pendingShot = "c04-badge"; yield return 4;
        Quiet();

        /* ---------- the coelacanth, its guardian, and a level gained landing it ---------- */
        caveFloor = AncientFloor; LoadScene("cave"); AtLadder(); yield return 2;
        state.hinted.Remove("platejaw"); state.commons.Remove("ancient_coelacanth"); state.inv.Remove("ancient_coelacanth");
        var (apx, apy) = StandNear("ancientpool");
        player.X = apx; player.Y = apy; player.Face = "up"; yield return 2;
        state.xp = XpFor(7) - 1;
        fish = new FishCast { Spot = "ancientpool", Bx = AncientPoolX, By = AncientPoolY };
        reel = new ReelState { Roll = new Catchable { Id = "ancient_coelacanth", Name = "Ancient coelacanth", Difficulty = 4.6f, Rare = true } };
        mode = "reeling"; LandCatch();
        Check("landing the coelacanth gains a level and still shows its legendary card first", mode == "legend" && celebQueue.Any(c => c.Kind == "level"));
        yield return 40;
        ClickButton("Incredible!");
        for (int i = 0; i < 8 && mode == "legend"; i++) yield return 1;
        Inp.ScriptMouse = Offscreen;
        for (int i = 0; i < 20; i++) yield return 1;
        Check($"its guardian rises and the level card waits through the fight ({mode}, guardian {guardian?.Phase})", guardian != null && mode == "play");
        guardian = null; rocks.Clear(); toastTimer = 0;
        // The coelacanth is held up for a moment first (heldT), then the card.
        badgeNews = null; foreach (var s in UntilCard()) yield return s;
        Check($"once the fight is over and the fish is put away, the card opens ({mode})", mode == "celebrate" && celebCard?.Kind == "level");
        yield return 40; Inp.Tap(KeyboardKey.E); yield return 3;
        fish = null; reel = null; mode = "play";
        LoadScene("world"); player.X = lx; player.Y = ly; yield return 3;

        /* ---------- nothing carries over to another game ---------- */
        Celebrate(new Celeb { Kind = "album", Tag = "x", Title = "x" }, null);
        state.xp = 0; StartGame(false); yield return 2;
        Check("loading a game clears waiting celebrations", celebQueue.Count == 0 && banner == null && celebCard == null);

        /* ---------- an old save ---------- */
        var old = System.Text.Json.JsonSerializer.Deserialize<State>("{\"created\":true,\"day\":4}", new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        Check("a save from before 1.44 has no day's biggest yet", old.cotdDay == 0 && old.cotdId == null);

        OldCelebrate = true;
        yield return 2;
    }
}
#endif
