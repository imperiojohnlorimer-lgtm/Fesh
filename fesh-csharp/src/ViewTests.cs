#if DEBUG
using Raylib_cs;

namespace Fesh;

// 1.18.1: looking closer. Every photo in the fish album, every fish in the island guide and every row of the Fish log's
// last page opens a card with a big picture; the island guide's drawing opens big; nothing in the album, the guide or
// the cards is cut short; and Ma'am Isay's lessons teach from the album and the guide. FESH_VIEW_TEST=1 runs only this
// (it still needs FESH_AUTOTEST and FESH_SAVE); the full play-through runs it after the island guide's checks.
partial class Game
{
    IEnumerable<int> ViewScript()
    {
        Note("Looking closer: cards from the album, the guide and the legends page");
        state = new State { created = true, look = new Look { name = "Close looker" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        animals.Clear();
        Inp.ScriptMouse = Offscreen;
        yield return 2;

        // Every odd catch has a picture for its catch card (and now its Fish log card): the parrot had none, so landing
        // one at the atoll lagoon crashed the game in DrawOdd. Without one the cards below would crash too, so stop.
        var noArt = Data.Odd.Where(o => { try { AnimalArt.Texture(o.Id); return false; } catch (KeyNotFoundException) { return true; } }).Select(o => o.Id).ToList();
        Check($"every odd catch has a picture for its card ({string.Join(", ", noArt)} missing)", noArt.Count == 0);
        if (noArt.Count > 0) yield break;

        /* ---------- The album ---------- */
        // Everything caught, with records: the fullest entries there can be.
        foreach (var f in Data.AllCommon) { state.commons[f.Id] = 12; state.records[f.Id] = f.Kg * 3.1f; }
        state.hinted["metIsay"] = true;
        foreach (var f in Data.AllCommon.Where((f, i) => i % 2 == 0)) state.know[f.Id] = LearnedAt;
        OpenAlbum(); yield return 3;
        lastAlbumEntryH = 0; albumTooTall = null;
        bool tabsOk = true, pagesFit = true;
        string worst = "";
        int spreads = AlbumPages().Count / 2;
        for (int s = 0; s < spreads; s++)
        {
            albumSpread = s; yield return 2;
            tabsOk &= !lastAlbumTabCut && lastAlbumTabsBottom <= AlbumPageH;
            if (lastAlbumBottom > AlbumPageH - 24) { pagesFit = false; worst = $"spread {s} ends at {lastAlbumBottom:0}"; }
        }
        Check($"no album entry is cut short: names, scientific names, families and food wrap, and every entry fits its slot (tallest {lastAlbumEntryH:0} of {AlbumEntryH}{(albumTooTall != null ? ", " + albumTooTall + " doesn't" : "")})",
            albumTooTall == null && lastAlbumEntryH <= AlbumEntryH);
        Check($"and every page still fits above its page number ({worst})", pagesFit);
        Check("every chapter tab's title fits in three lines, and the tabs fit beside the pages", tabsOk);
        // The page turning sits on the cover under the pages, not on them.
        Check("the page-turning buttons sit below the pages", Gfx.Seen.TryGetValue("Next >", out var nextR) && nextR.Y >= (Gfx.LH - AlbumH) / 2 + 46 + AlbumPageH);
        OpenAlbum("ironbill"); yield return 3;
        pendingShot = "380-album-billfish"; yield return 2;
        // Click a photo: its card opens over the album; Esc goes back to the same spread.
        int spread0 = albumSpread;
        Gfx.Seen.Remove("Album page");
        ClickButton("albumfish:ironbill"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking an album photo opens that fish's card, in the album ({dexFish}, {panel})", dexFish == "ironbill" && panel == "album" && mode == "panel");
        Check("the card from the album has no Album page button (Back goes there)", !Gfx.Seen.ContainsKey("Album page"));
        pendingShot = "381-album-card"; yield return 2;
        Inp.Tap(KeyboardKey.Right); yield return 3;
        Check("the arrow keys don't turn the album's pages behind the card", albumSpread == spread0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc closes the card back to the same album spread", dexFish == null && panel == "album" && albumSpread == spread0);
        ClickButton("albumfish:ironbill"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        ClickButton("Back"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("and so does its Back button", dexFish == null && panel == "album");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc on the album itself closes it", mode == "play" && panel == null);

        /* ---------- Every fish's card fits ---------- */
        // The card is wider and taller now, with the album's family, water and food on it: every one must still fit.
        var tooTall = new List<string>();
        float tallest = 0;
        panel = "dex"; mode = "panel";
        foreach (var f in Data.AllCommon)
        {
            dexFish = f.Id; yield return 1;
            tallest = Math.Max(tallest, lastCardBottom);
            if (lastCardBottom > DexCardH - DexCardPad) tooTall.Add(f.Id);
        }
        Check($"every fish's card fits, with its family, water and food ({tooTall.Count} don't: {string.Join(", ", tooTall.Take(4))}; tallest {tallest:0} of {DexCardH - DexCardPad})", tooTall.Count == 0);
        dexFish = "lapu_lapu"; yield return 2;
        pendingShot = "382-fish-card-album-info"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- The island guide ---------- */
        Note("The island guide: fish cards and the drawing made big");
        state.charted = Regions.Select(r => r.Id).Concat(new[] { "habagat:islet:0" }).ToList();
        state.caveDeepest = 12; state.inv["boat"] = 1; state.hinted["amihan"] = state.hinted["habagat"] = state.hinted["metJoy"] = true;
        OpenAtlas("amihan:Bakawan Island"); yield return 3;
        var bakFish = RegionFish(RegionById["amihan:Bakawan Island"]);
        string tileId = bakFish[0].Id;
        Inp.ScriptMouse = new System.Numerics.Vector2(Gfx.Seen["atlasfish:" + tileId].X + 10, Gfx.Seen["atlasfish:" + tileId].Y + 10); yield return 3;
        pendingShot = "383-atlas-fish-hover"; yield return 2;
        ClickButton("atlasfish:" + tileId); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking a fish under \"Fish here\" opens its card in the guide ({dexFish})", dexFish == tileId && panel == "atlas");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc goes back to the same island's page", dexFish == null && panel == "atlas" && atlasRegion == "amihan:Bakawan Island");
        ClickButton("atlas:diagram"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("clicking the cross-section opens it big", atlasZoom && panel == "atlas");
        pendingShot = "384-atlas-zoom-bakawan"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc closes the big drawing back to the page", !atlasZoom && panel == "atlas");
        // Every page's big drawing fits, with its key and its real-world note.
        bool zoomFits = true;
        foreach (var r in Regions)
        {
            atlasRegion = r.Id; atlasZoom = true; yield return 2;
            if (lastAtlasZoomBottom > 664 - 18) { zoomFits = false; worst = $"{r.Id} ends at {lastAtlasZoomBottom:0}"; }
            if (r.Id is "caverns" or "opensea" or "habagat:Parola" or "saltmere")
            { pendingShot = "385-atlas-zoom-" + r.Id.Replace(':', '-').Replace(' ', '-'); yield return 2; }
        }
        Check($"every place's big drawing fits its page, with the colour key and the real-world note ({worst})", zoomFits);
        atlasZoom = false;
        // And every page itself still fits, with the bigger fish tiles.
        bool pageFits = true;
        foreach (var r in Regions)
        {
            atlasRegion = r.Id; yield return 2;
            if (lastAtlasBottom > (Gfx.LH + 664) / 2 - 18) { pageFits = false; worst = $"{r.Id} ends at {lastAtlasBottom:0}"; }
        }
        Check($"every island guide page still fits with the bigger fish tiles ({worst})", pageFits);
        atlasRegion = "amihan:Amihan Village"; yield return 2;
        pendingShot = "386-atlas-village"; yield return 2;
        // A pool that's brackish is drawn in the brackish colour (Codex: every pool used to be fresh).
        Check("a brackish pool is drawn brackish, a fresh one fresh",
            LakeColor(RegionById["amihan:Bakawan Island"], false).G != LakeColor(RegionById["saltmere"], false).G && LakeColor(RegionById["mire"], false).Equals(LakeColor(RegionById["saltmere"], false)));
        ClosePanels(); yield return 2;

        /* ---------- The legends page ---------- */
        Note("The Fish log's last page: every row opens a card");
        // Nothing found yet first: the cards say only what the page already says (no Starwell, no Tidemane).
        state.commons.Clear(); state.records.Clear(); state.odd.Clear(); state.sightings.Clear(); state.tamed = false; state.hinted.Remove("moonReturned");
        TogglePanel("dex"); dexTab = "log"; logPage = Data.Biomes.Length; yield return 3;
        const float noteW = (1180 - 64 - 24 - 18) / 2 - 52;
        Check($"with nothing found, every legend's hint shows in full (two lines at most) and the page fits ({lastDexBottom:0})",
            LegendRows(noteW).All(r => r.note.Count <= 2) && lastDexBottom <= Gfx.LH - 8);
        pendingShot = "387-legends-page-empty"; yield return 2;
        ClickButton("row:tidemane"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        var (gotT, nameT, _, _, aboutT, factsT) = ExtraInfo("tidemane");
        string allT = nameT + aboutT + string.Join(" ", factsT.Select(f => f.v));
        Check($"an unfound Tidemane's card opens and gives nothing away ({nameT})", dexExtra == "tidemane" && !gotT && nameT == "???"
            && !allT.Contains(Data.MountName) && !allT.Contains("Starwell") && !allT.Contains("coconut"));
        pendingShot = "387-legend-unknown"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc closes it back to the page", dexExtra == null && panel == "dex" && mode == "panel");
        ClickButton("row:old_whiskers"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("an uncaught legend's row opens its fish card, nameless", dexFish == "old_whiskers" && state.commons.GetValueOrDefault("old_whiskers") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // Then everything found: each kind of row opens its own card, and each fits.
        foreach (var id in Data.Legends.Keys) { state.commons[id] = 1; state.records[id] = Data.FishById[id].Kg; }
        state.tamed = true; state.hinted["moonReturned"] = true;
        foreach (var o in Data.Odd) state.odd[o.Id] = 2;
        foreach (var k in SeaKinds.Keys.Concat(BakawanKinds.Keys)) state.sightings[k] = 3;
        yield return 2;
        var rows = Data.Legends.Keys.Concat(new[] { "tidemane", "bakunawa" }).Concat(Data.Odd.Select(o => "odd:" + o.Id))
            .Concat(SeaKinds.Keys.Concat(BakawanKinds.Keys).Select(k => "sight:" + k)).ToList();
        bool opened = true, fit = true, real = true;
        string bad = "";
        foreach (var key in rows)
        {
            dexFish = null; dexExtra = null; yield return 2;
            ClickButton("row:" + key); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
            bool isFish = Data.FishById.ContainsKey(key);
            if (isFish ? dexFish != key : dexExtra != key) { opened = false; bad = key; continue; }
            if (lastCardBottom > (isFish ? DexCardH : 520) - DexCardPad || !isFish && lastExtraBoxBottom > 0) { fit = false; bad = $"{key} ({lastCardBottom:0}, {lastExtraBoxBottom:0})"; }
            if (!isFish) real &= ExtraFacts.ContainsKey(key[(key.IndexOf(':') + 1)..]);
            if (key is "tidemane" or "bakunawa" or "odd:goat" or "sight:butanding" or "sight:walowalo" or "sight:taklobo" or "sight:alitaptap" or "sight:pawikan" or "sight:dugong" or "sight:plankton" or "sight:tarsier" or "haring_buan")
            { pendingShot = "388-card-" + key.Replace(':', '-'); yield return 2; }
        }
        Check($"every row of the last page opens its card: legends, Tidemane, Bakunawa, odd catches and sightings ({rows.Count}; {bad})", opened);
        Check($"and every card fits, with its real animal or story ({bad})", fit && real);
        dexFish = null; dexExtra = null; yield return 2;
        Check($"with everything found, every note shows in full and the page fits ({lastDexBottom:0})",
            LegendRows(noteW).All(r => r.note.Count <= 2) && lastDexBottom <= Gfx.LH - 8);
        pendingShot = "389-legends-page"; yield return 2;
        // Codex: this used to check after both were already cleared. Now a card is open when the Fish log's key closes it.
        ClickButton("row:sight:dugong"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        bool wasOpen = dexExtra == "sight:dugong";
        Inp.Tap(KeyboardKey.J); yield return 3;
        Check("closing the Fish log with a card open clears the card", wasOpen && mode == "play" && dexFish == null && dexExtra == null);

        /* ---------- Ma'am Isay's lessons teach more ---------- */
        Note("Ma'am Isay's lessons: families, water, food, fish or not, and the islands");
        foreach (var f in Data.AllCommon) state.commons[f.Id] = 12;
        var known = KnownFish();
        bool wellFormed = true, familyClean = true, waterUnique = true, labelsFit = true, lessonsFit = true, placesOnly = true;
        var kinds = new[] { "picture", "shadow", "where", "when", "fight", "heavier", "fact", "real", "sci", "bait", "family", "water", "diet", "notfish", "isle", "waterbody" };
        var made = new Dictionary<string, int>();
        const float quizW = 1120, quizPad = 26;
        float lessonW = quizW - 2 * quizPad - QuizLessonX - 24, answerW = (quizW - 2 * quizPad - 400 - 30 - 16) / 2 - 50;
        foreach (var kind in kinds)
            for (int i = 0; i < 40; i++)
            {
                var q = MakeQuestion(kind, known, new());
                if (q == null) continue;
                Enrich(q);
                made[kind] = made.GetValueOrDefault(kind) + 1;
                wellFormed &= q.Options.Length >= 2 && q.Options.Distinct().Count() == q.Options.Length && q.Answer >= 0 && q.Answer < q.Options.Length && q.Teach != null
                    && (q.Subject != null) != (q.Region != null);
                if (kind == "family") familyClean &= q.Options.Where((o, k) => k != q.Answer).All(o => !SameFamily(Data.AllCommon.First(f => AlbumData.TryGetValue(f.Id, out var a) && FamilyShort(a.Family) == o).Id, q.Subject));
                if (kind == "water") waterUnique &= q.Options.Count(o => o == WaterSetWord(AlbumData[q.Subject].Water)) == 1;
                if (kind is "isle" or "waterbody") placesOnly &= AtlasOpen(RegionById[q.Region]);
                // Every answer fits its button in two lines, and the why always fits the lesson box (with a two-line prompt).
                if (q.Pics == null) labelsFit &= q.Options.All(o => Gfx.Wrap(o, FontKind.Ui700, 19, q.Options.Length == 4 ? answerW : quizW - 2 * quizPad - 400 - 30 - 50).Count <= 2);
                var (head, teach, _, _, fs) = QuizLessonLayout(q, false, lessonW, 210);
                lessonsFit &= 16 + head.Count * 21 + 2 + teach.Count * (fs + 4) <= 210;
                if (q.Quote != null) lessonsFit &= Gfx.Wrap("“" + q.Quote + "”", FontKind.Note, 18, 400 - 30).Count <= 9;
            }
        Check($"every kind of question can be asked with everything found ({string.Join(", ", kinds.Where(k => !made.ContainsKey(k)))} never came up)", kinds.All(made.ContainsKey));
        Check("and every question is well formed, about a fish or a place", wellFormed);
        Check("a family question never offers a family the fish could also belong to", familyClean);
        Check("a water question has exactly one right set of waters", waterUnique);
        Check("questions about places only use charted ones", placesOnly);
        Check("every answer fits its button, and every quote its box", labelsFit);
        Check("the why behind every answer always fits the lesson box", lessonsFit);
        // Feeding roles: a few clear cases, and the muddled ones left out.
        Check($"food roles: a tuna eats animals, a parrotfish algae, a tamban plankton, a mirage koi both ({FeedRole("yellowfin_tuna")}, {FeedRole("parrotfish")}, {FeedRole("tamban")}, {FeedRole("mirage_koi")})",
            FeedRole("yellowfin_tuna") == "carnivore" && FeedRole("parrotfish") == "herbivore" && FeedRole("tamban") == "plankton" && FeedRole("mirage_koi") == "omnivore"
            && FeedRole("swamp_catfish") == null && FeedRole("galunggong") == null);
        Check("'when' has one right answer for a fish that bites day and night", MakeQuestion("when", known.Where(f => f.Time == "any" && Data.SpotOfFish.ContainsKey(f.Id)).Take(1).ToList(), new())?.Options.Count(o => o.Contains("Only")) == 2);
        Check("'sci' never asks about a group name like an infraorder", Enumerable.Range(0, 60).Select(_ => MakeQuestion("sci", known, new())).All(q => q == null || !q.Big.Contains("order") && !q.Big.Contains("spp")));
        // Only Saltmere charted: no question names another island.
        var charted = state.charted;
        var flags = new[] { "amihan", "habagat", "metJoy" }.ToDictionary(k => k, k => state.Hinted(k));
        state.charted = new() { "saltmere" };
        foreach (var k in flags.Keys) state.hinted.Remove(k);
        Check("with only Saltmere charted, no question is about another island", Enumerable.Range(0, 40).Select(_ => MakeQuestion("isle", known, new())).All(q => q == null)
            && Enumerable.Range(0, 40).Select(_ => MakeQuestion("waterbody", known, new())).All(q => q == null || q.Region == "saltmere"));
        state.charted = charted;
        foreach (var (k, v) in flags) state.hinted[k] = v;
        // A question about a place counts for the score but not toward learning a fish, and the class can still vote.
        OpenQuiz(); yield return 2;
        StartLesson();
        var placeQ = MakeQuestion("isle", known, new()); Enrich(placeQ);
        quiz.Qs[0] = placeQ; quiz.At = 0; quiz.View = "question"; yield return 2;
        pendingShot = "390-quiz-place"; yield return 2;
        var knowBefore = new Dictionary<string, int>(state.know);
        AskClass();
        Check("asking the class works on a question about a place", quiz.Votes?.Sum() == 100);
        ClickButton($"quiz:{placeQ.Answer}"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("answering it right scores, and leaves the fish you know alone", quiz.Right == 1 && quiz.Score > 0 && state.know.Count == knowBefore.Count && state.know.All(kv => knowBefore[kv.Key] == kv.Value));
        Check($"its lesson fits its box ({lastQuizLessonEnd:0})", lastQuizLessonEnd <= 0 && lastQuizBottom <= (Gfx.LH + 650) / 2 - 20);
        pendingShot = "391-quiz-place-answer"; yield return 2;
        // A fish question's lesson, answered wrong, with its further fact and album line.
        var fishQ = MakeQuestion("family", known, new()); Enrich(fishQ);
        quiz.Qs[1] = fishQ;
        ClickButton("quiz:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("Next moves on from the lesson", quiz.At == 1 && quiz.View == "question");
        Inp.Tap((KeyboardKey)((int)KeyboardKey.One + (fishQ.Answer + 1) % fishQ.Options.Length)); yield return 3;
        Check($"a wrong answer's lesson shows the answer, the why, the album line and a further fact, and fits ({lastQuizLessonEnd:0})",
            quiz.View == "answer" && fishQ.Info != null && lastQuizLessonEnd <= 0);
        pendingShot = "392-quiz-family-wrong"; yield return 2;
        // A long prompt moves the picture down rather than running into it.
        var longQ = MakeQuestion("real", known.OrderByDescending(f => f.Name.Length).Take(1).ToList(), new());
        Enrich(longQ);
        longQ.Prompt += " Think back to what its card in the Fish log says about the real animal behind it.";
        quiz.Qs[2] = longQ; Inp.Tap(KeyboardKey.Enter); yield return 3;
        float qTop = (Gfx.LH - 650) / 2 + 26 + 70;
        bool twoLines = Gfx.Wrap(longQ.Prompt, FontKind.Ui700, 24, 1120 - 52).Count == 2;
        Check($"a two-line prompt moves the picture down instead of running into it ({lastQuizBottom:0})",
            twoLines && Gfx.Seen.TryGetValue("quiz:0", out var opt0) && opt0.Y >= qTop + 60 && lastQuizBottom <= (Gfx.LH + 650) / 2 - 20);
        pendingShot = "393-quiz-long-prompt"; yield return 2;
        AnswerQuiz(longQ.Answer); yield return 3;
        Check($"and its lesson still fits ({lastQuizLessonEnd:0})", lastQuizLessonEnd <= 0 && lastQuizBottom <= (Gfx.LH + 650) / 2 - 20);
        ClosePanels(); yield return 2;
        foreach (int f in ViewReviewScript()) yield return f;
        Inp.ScriptMouse = Offscreen;
    }

    // Codex's review of 1.18.1: each of these failed before its fix.
    IEnumerable<int> ViewReviewScript()
    {
        Note("Codex's review of 1.18.1");
        // Bakunawa's card gave its name away before the eclipse (the page's row said ???).
        state.hinted.Remove("moonReturned");
        var (gotB, nameB, _, _, aboutB, factsB) = ExtraInfo("bakunawa");
        Check($"an unseen Bakunawa's card doesn't name it ({nameB})", !gotB && !(nameB + aboutB + string.Join(" ", factsB.Select(f => f.v))).Contains("Bakunawa"));
        state.hinted["moonReturned"] = true;
        // Four caught fish of one family: a fact question must not fill its answers with fish you haven't caught.
        var tunas = new[] { "yellowfin_tuna", "bluefin_tuna", "silver_tuna", "tulingan" }.Select(id => Data.FishById[id]).ToList();
        bool namesCaught = true;
        for (int i = 0; i < 40; i++)
        {
            var q = MakeQuestion("fact", tunas, new());
            if (q != null) namesCaught &= q.Options.All(o => tunas.Any(t => t.Name == o));
        }
        Check("a fact question never offers the name of a fish you haven't caught", namesCaught);
        // Feeding: a plankton feeder like a lanternfish eats tiny animals, so "other animals" can't be a wrong answer for it;
        // and the answers a teacher would dispute (a Moorish idol is an omnivore; crabs scavenge plants too) aren't asked.
        var known = KnownFish();
        bool exclusive = true;
        foreach (var f in known.Where(f => FeedRole(f.Id) != null))
        {
            var q = MakeQuestion("diet", known, known.Where(o => o.Id != f.Id).Select(o => o.Id).ToHashSet());
            if (q == null || q.Subject != f.Id) { exclusive = false; continue; }
            string role = FeedRole(f.Id);
            // Every wrong option must be plainly false for this animal.
            exclusive &= !q.Options.Where((o, k) => k != q.Answer).Any(o => o.StartsWith("Other animals") && role is "plankton" or "omnivore"
                || o.StartsWith("Plankton") && role is "carnivore" or "herbivore" && !o.StartsWith("Only")
                || o.StartsWith("Both") && role == "plankton");
        }
        Check("a food question's wrong answers are all plainly wrong for that animal", exclusive);
        Check("no food question about a Moorish idol, a sunfish, a moonfish or a crab", FeedRole("moorish_idol") == null && FeedRole("ocean_sunfish") == null
            && FeedRole("silver_moonfish") == null && AlbumChapters.First(c => c.Id == "crustaceans").Fish.All(id => FeedRole(id) == null));
        // Water: the question asks for the whole list, since "fresh and brackish" is half true of a fish that also lives in the sea.
        var wq = MakeQuestion("water", known, known.Where(o => o.Id != "banak").Select(o => o.Id).ToHashSet());
        Check($"a water question asks for every water it lives in ({wq?.Prompt})", wq?.Prompt.Contains("all") == true);
        // Squid have fins: the lesson mustn't say they don't.
        var squids = known.Where(f => f.Id is "pusit" or "giant_squid" or "lantern_squid" or "pugita").ToList();
        bool finsOk = true;
        for (int i = 0; i < 40; i++)
        {
            var q = MakeQuestion("notfish", known.Where(f => !AlbumChapters.First(c => c.Id == "crustaceans").Fish.Contains(f.Id)).ToList(), new());
            if (q != null) finsOk &= !q.Teach.Contains("instead of fins");
        }
        Check("the squid's lesson doesn't say it has no fins", finsOk && squids.Count > 0);
        // A fish question's lesson really shows the album line and a further fact (not just has them).
        var fq = MakeQuestion("family", known, known.Where(o => o.Id != "lapu_lapu").Select(o => o.Id).ToHashSet());
        Enrich(fq);
        var (_, _, info, more, _) = QuizLessonLayout(fq, false, 1120 - 52 - QuizLessonX - 24, 240);
        Check($"a family lesson shows its album line and a further fact ({info.Count} and {more.Count} lines)", fq.Subject == "lapu_lapu" && info.Count > 0 && more.Count > 0);
        // Every subject of every kind, not a sample: the why always fits the lesson box.
        bool allFit = true;
        string tooLong = "";
        foreach (var kind in new[] { "family", "water", "diet", "fact", "real", "sci", "where", "when", "fight", "bait", "picture" })
            foreach (var f in known)
            {
                var q = MakeQuestion(kind, known, known.Where(o => o.Id != f.Id).Select(o => o.Id).ToHashSet());
                if (q == null || q.Subject != f.Id) continue;
                Enrich(q);
                var (head, teach, _, _, fs) = QuizLessonLayout(q, false, 1120 - 52 - QuizLessonX - 24, 210);
                if (16 + head.Count * 21 + 2 + teach.Count * (fs + 4) > 210) { allFit = false; tooLong = $"{kind}:{f.Id}"; }
            }
        Check($"for every fish, every kind of lesson fits ({tooLong})", allFit);
        // The fish card's title and its row of tags fit across the card, for every fish.
        bool across = true;
        panel = "dex"; mode = "panel";
        foreach (var f in Data.AllCommon)
        {
            dexFish = f.Id; yield return 1;
            if (!lastCardAcross) { across = false; tooLong = f.Id; }
        }
        Check($"every fish card's name and tags fit across it ({(across ? "" : tooLong)})", across);
        ClosePanels(); yield return 2;
    }
}
#endif
