using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The Fish log's extras: a card for each fish (click its row), fish that are biting right now, fish you've seen get
// away, each island's progress and the reward for catching all of it, and the filters.
partial class Game
{
    string dexFish;               // the fish whose card is open in the Fish log, or null
    string dexFilter = "all";     // "all", "missing", "rare" or "now"
    float lastDexBottom;          // where the Fish log panel last ended (the autotest checks it fits on the screen)
    const int PageReward = 200;   // coins for catching every fish on an island's page

    // Whether its time, weather, moon and season are right just now (bait and legends aside). Crab-pot catches don't bite.
    bool BitingNow(CommonFish f) =>
        Data.SpotOfFish.ContainsKey(f.Id)
        && (f.Time == "any" || (f.Time == "night") == Night)
        && (f.Weather == null || (f.Weather == "storm" ? Stormy : f.Weather == "rain" ? state.weather != "clear" : state.weather == "clear"))
        && (!f.FullMoon || FullMoon)
        && (f.Need == null || state.Hinted(f.Need))
        && (f.Season == null || f.Season == Season)
        // A legend only bites once (unless it has a boss fight: Refightable), and only on its own bait (1.29): without it the green
        // dot said "biting" for a fish that never would.
        && (!f.Legend || (state.commons.GetValueOrDefault(f.Id) == 0 || Refightable(f)) && (f.Bait == null || Has(f.Bait) > 0));

    bool Seen(string id) => state.seen.Contains(id) && state.commons.GetValueOrDefault(id) == 0;

    // One that got away in the middle of a fight: you saw it, so the Fish log shows its silhouette and name.
    void MarkSeen(string id)
    {
        if (!Data.FishById.ContainsKey(id) || state.commons.GetValueOrDefault(id) > 0 || state.seen.Contains(id)) return;
        state.seen.Add(id);
    }

    // The Fish log page (a biome) a fish is on: its spot's island, or the island whose crab pots catch it.
    int PageOf(string id)
    {
        string biome = Data.SpotOfFish.TryGetValue(id, out var spot) ? Data.SpotById[spot].Biome
            : Data.PotCatch.First(kv => kv.Value.Any(f => f.Id == id)).Key;
        return Array.FindIndex(Data.Biomes, b => b.Id == biome);
    }

    List<CommonFish> PageFish(int page) => LogGroups(page, "all").SelectMany(g => g.fish).ToList();

    // After a first catch: a page with every fish caught pays out once.
    void CheckPageDone(string id)
    {
        int page = PageOf(id);
        var b = Data.Biomes[page];
        if (state.Hinted("dexdone:" + b.Id) || PageFish(page).Any(f => state.commons.GetValueOrDefault(f.Id) == 0)) return;
        state.hinted["dexdone:" + b.Id] = true;
        state.coins += PageReward;
        Sfx.Play("rare");
        Toast($"Fish log: every fish of {b.Name} caught! Pip sends {PageReward} coins for the collection.", 5);
    }

    bool PassesFilter(CommonFish f, string filter) => filter switch
    {
        "missing" => state.commons.GetValueOrDefault(f.Id) == 0,
        "rare" => f.Rare || f.Legend,
        "now" => BitingNow(f),
        _ => true
    };

    // The baits a fish goes for (BaitLikes), by name.
    List<string> LikedBaits(CommonFish f)
    {
        if (!Data.SpotOfFish.TryGetValue(f.Id, out var spot)) return new();
        // A fish that takes only one thing says just that (it used to list other baits after "only that").
        if (f.Troll) return new() { "a trolled lure (only that)" };
        if (f.Legend && f.Bait != null) return new() { Items.ById[f.Bait].Name.ToLowerInvariant() + " (only that)" };
        return Items.Baits.Keys.Where(b => BaitLikes(b, f, spot)).Select(b => Items.ById[b].Name.ToLowerInvariant()).Distinct().ToList();
    }

    // A fish's own card, in place of the Fish log page, the album or the island guide (Back or Esc returns to it).
    const float DexCardW = 1000, DexCardH = 600, DexCardPad = 28, DexPicW = 336, DexPicH = 196;
    float lastCardBottom;       // where the card's right-hand column ended (the autotest checks it fits)
    bool lastCardAcross;        // the card's name and its row of tags both fit across it (checked)

    void DrawFishCard()
    {
        var f = Data.FishById[dexFish];
        int n = state.commons.GetValueOrDefault(f.Id);
        bool known = n > 0, seen = Seen(f.Id);
        Data.Legends.TryGetValue(f.Id, out var legend);
        FishArt.Looks.TryGetValue(f.Id, out var look);
        AlbumData.TryGetValue(f.Id, out var album);
        const float w = DexCardW, h = DexCardH, pad = DexCardPad, picW = DexPicW, picH = DexPicH;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        PanelBox(x, y, w, h);
        if (SmallButton("Back", x + w - pad - SmallW("Back"), y + pad - 6)) { dexFish = null; return; }
        // Its page in the fish album, once you've caught one (not from the album itself: Back goes there).
        if (panel != "album" && n > 0 && SmallButton("Album page", x + w - pad - SmallW("Back") - 12 - SmallW("Album page"), y + pad - 6)) { OpenAlbum(dexFish); dexFish = null; return; }

        // The picture: its own (a silhouette if you've only seen it), or the legend's portrait.
        float px = x + pad, py = y + pad + 50;
        Gfx.Rect(px - 3, py - 3, picW + 6, picH + 6, Pal.Ink, 4);
        if (legend != null && (known || seen)) Gfx.Portrait(legend.Art, !known, px, py, picW, picH);
        else
        {
            Gfx.Rect(px, py, picW, picH, Pal.C("#2a5a7a"), 3);
            if (known || seen)
            {
                var tex = FishArt.Picture(f.Id, !known);
                DrawTexturePro(tex, new Rectangle(0, 0, FishArt.BigW, FishArt.BigH), Gfx.S(px + 12, py + 22, FishArt.BigW * 6.5f, FishArt.BigH * 5.5f), System.Numerics.Vector2.Zero, 0, Color.White);
            }
            else Gfx.TextCenter("?", px + picW / 2, py + picH / 2 - 30, FontKind.Ui700, 60, Pal.C("#9fc3d1"));
        }
        // Under the picture: the real animal (FishFacts), once you've caught one or seen one get away.
        DrawRealFish(f.Id, known || seen, px - 3, py + picH + 14, picW + 6, y + h - pad - (py + picH + 14));

        string name = known || seen ? f.Name : "???";
        // A long name gets a smaller size rather than losing its end.
        float nameRoom = w - 2 * pad - SmallW("Back") - SmallW("Album page") - 40, nameSize = Gfx.Measure(name, FontKind.Ui700, 34) <= nameRoom ? 34 : 27;
        Gfx.Text(name, x + pad, y + pad - 2 + (34 - nameSize) / 2, FontKind.Ui700, nameSize, f.Legend && known ? Pal.C("#9a6a1a") : Pal.PaperInk);
        var tags = new List<string>();
        if (f.Legend) tags.Add("Legendary"); else if (f.Rare) tags.Add("Rare");
        if (Refightable(f)) tags.Add("Boss fight");
        if (LetGoLegend(f.Id) && known) tags.Add("Let go");
        if (f.Attack > 0) tags.Add("Fights back");
        if (f.Troll) tags.Add("Trolling only");
        if (!known && seen) tags.Add("Seen, not caught");
        float tx = x + pad + picW + 26, ty = py, tw = w - pad * 2 - picW - 26;
        float chipX = tx;
        foreach (var t in tags)
        {
            float cw = Gfx.Measure(t, FontKind.Ui700, 15) + 16;
            Gfx.Rect(chipX, ty, cw, 24, t == "Legendary" ? Pal.C("#10243a") : t is "Fights back" or "Boss fight" ? Pal.C("#b5423a") : t == "Let go" ? Pal.C("#3f8a5a") : Pal.C("#3f6a8a"), 4);
            Gfx.Text(t, chipX + 8, ty + 3, FontKind.Ui700, 15, t == "Legendary" ? Pal.Lantern : Pal.Paper);
            chipX += cw + 8;
        }
        // The water it lives in (the album's tags), once you've caught one.
        if (known && album != null)
            foreach (var kind in album.Water.Split(',').Select(k => k.Trim()))
            {
                string word = WaterWord(kind);
                float cw = Gfx.Measure(word, FontKind.Ui700, 15) + 16;
                Gfx.Rect(chipX, ty, cw, 24, WaterColor(kind), 4);
                Gfx.Text(word, chipX + 8, ty + 3, FontKind.Ui700, 15, Pal.Paper);
                chipX += cw + 8;
            }
        lastCardAcross = Gfx.Measure(name, FontKind.Ui700, nameSize) <= nameRoom && chipX - 8 <= tx + tw;
        if (chipX > tx) ty += 34;
        string about = known ? legend?.Desc ?? look?.About ?? "" : seen ? "You saw one get away in the middle of a fight. Catch one to learn more."
            : legend != null ? "Not caught yet. " + legend.Hint : "Not caught yet.";
        var aboutLines = Gfx.Wrap(about, FontKind.Note, 19, tw);
        Lines(aboutLines, tx, ty, 26, FontKind.Note, 19, Pal.PaperInk);
        ty += aboutLines.Count * 26 + 12;

        // Where, when and how.
        string where = Data.SpotOfFish.TryGetValue(f.Id, out var spotId)
            ? $"{(SpotKnown(Data.SpotById[spotId]) ? Data.SpotById[spotId].Label : "???")}, {Data.Biomes[PageOf(f.Id)].Name}"
            : $"Crab pots on {Data.Biomes[PageOf(f.Id)].Name}";
        var facts = new List<(string k, string v)> { ("Where", where), ("When", WhenText(f)) };
        // A legend's own story of how you landed it (the legends page keeps only its weight and place).
        if (known && legend != null) facts.Add(("How", legend.Where));
        if (known)
        {
            // Its family and food, from the album.
            if (album != null)
            {
                facts.Add(("Family", album.Family));
                facts.Add(("Eats", album.Diet));
            }
            facts.Add(("Fights", BossFightText(f.Id) ?? f.Style switch { "runner" => "makes runs: let go while it runs", "jumper" => "leaps: press as the marker crosses the gold", "bottom" => "hugs the bottom: pump it up with short taps", _ => "darts about" }
                + (f.Depth != "any" ? $"; feeds in {f.Depth} water" : "")));
            var baits = LikedBaits(f);
            facts.Add(("Likes", baits.Count > 0 ? string.Join(", ", baits) : "nothing in particular"));
            string rec = state.records.TryGetValue(f.Id, out float kg) ? Kg(kg) : "-";
            int tr = state.trophies.GetValueOrDefault(f.Id);
            facts.Add(("Caught", $"{n} · heaviest {rec} · trophy from about {Kg(f.Kg * 1.5f)}{(tr > 0 ? $" ({tr} so far)" : "")}"));
            facts.Add(LetGoLegend(f.Id) ? ("Kept?", "No: you let it go once it's in. The record is yours.")
                : ("Pip pays", ClosedFish.Contains(f.Id) ? $"{PriceOf(f.Id)} coins, but not in the amihan: Pip's closed season while they spawn" : $"{PriceOf(f.Id)} coins"));
        }
        // Ma'am Isay's class (School.cs).
        if ((known || seen) && state.Hinted("metIsay") && !f.Legend)
        {
            int k = state.know.GetValueOrDefault(f.Id);
            facts.Add(("Class", Learnt(f.Id) ? "Learned: you got three questions about it right" : $"{k} of {LearnedAt} right answers to learn it"));
        }
        foreach (var (k, v) in facts)
        {
            Gfx.Text(k, tx, ty, FontKind.Ui700, 16, Muted);
            var vl = Gfx.Wrap(v, FontKind.Ui500, 16, tw - 90);
            Lines(vl, tx + 90, ty, 21, FontKind.Ui500, 16, Pal.PaperInk);
            ty += Math.Max(1, vl.Count) * 21 + 7;
        }
        bool now = BitingNow(f);
        if (Data.SpotOfFish.ContainsKey(f.Id))
        {
            Gfx.Circle(tx + 8, ty + 11, 6, now ? Pal.C("#3fae5a") : Pal.C("#a89a80"));
            Gfx.Text(now ? "Biting now" : "Not biting right now", tx + 22, ty + 2, FontKind.Ui700, 17, now ? Pal.C("#2f7d45") : Muted);
            ty += 24;
        }
        lastCardBottom = ty - y;
        if (Gfx.PressedOutside(x, y, w, h)) dexFish = null;
    }

    // The real fish behind a game fish: a scientific name and a few true things about it, or what an invented one is
    // based on. FactLayout is shared with the autotest, which checks every one fits the box.
    const float FactFs = 15, FactLh = 20, FactPad = 12;
    (List<string> sci, List<string> body) FactLayout(FishFacts.Fact fact, float w) =>
        (Gfx.Wrap((fact.Real ? "" : "Based on: ") + fact.Sci, FontKind.Note, 15, w - 2 * FactPad), Gfx.Wrap(fact.Text, FontKind.Ui500, FactFs, w - 2 * FactPad));
    static float FactHeight(List<string> sci, List<string> body) => FactPad + 20 + sci.Count * 19 + 6 + body.Count * FactLh + FactPad - 4;

    void DrawRealFish(string id, bool open, float x, float y, float w, float h)
    {
        Gfx.Box(x, y, w, h, Pal.C("#eef6f2"), Pal.C("#8fb8a8"), 2, 5);
        if (!FishFacts.ById.TryGetValue(id, out var fact)) return;
        if (!open)
        {
            Lines(Gfx.Wrap("Catch one to read about the real fish.", FontKind.Note, 16, w - 2 * FactPad), x + FactPad, y + FactPad, 22, FontKind.Note, 16, Muted);
            return;
        }
        var (sci, body) = FactLayout(fact, w);
        float ty = y + FactPad - 2;
        Gfx.Text(fact.Real ? "The real fish" : "Made up for Fesh", x + FactPad, ty, FontKind.Ui700, 16, fact.Real ? Pal.C("#2a7d74") : Pal.C("#8a6a2a"));
        ty += 20;
        Lines(sci, x + FactPad, ty, 19, FontKind.Note, 15, Muted);
        ty += sci.Count * 19 + 6;
        Lines(body, x + FactPad, ty, FactLh, FontKind.Ui500, FactFs, Pal.PaperInk);
    }

    string WhenText(CommonFish f)
    {
        var bits = new List<string> { f.Time == "any" ? "day or night" : f.Time == "night" ? "at night" : "by day" };
        if (f.Weather != null) bits.Add(f.Weather == "clear" ? "on clear days" : f.Weather == "rain" ? "in the rain" : "in storms");
        if (f.FullMoon) bits.Add("under a full moon");
        if (f.Season != null) bits.Add($"in the {f.Season} season");
        if (f.Need == "parolaLit") bits.Add("once the Parola lamp is lit");
        if (f.Need == "moonReturned") bits.Add("once the moon is safe");
        if (f.Legend) bits.Add(Refightable(f) ? "and again after you've fought it" : "only once");
        return string.Join(", ", bits);
    }

    // How a boss fish fights, for its card (the ordinary line for its style would undersell it). Null for the rest.
    string BossFightText(string id) => id switch
    {
        TowFish => Bind.Fix("a boss fight: it tows your boat and breaches (press on the gold), charges once it's furious (let go of <act> to brace) and tail-walks at the end (ease off the reel)"),
        MireFish => Bind.Fix("a boss fight: pump it up; press <act> as it gulps air, pull it away from the mangrove roots with <left>/<right>, and press on the gold when it leaps"),
        WhiskersFish => Bind.Fix("a boss fight: a bottom-hugger; wait for the bobber to go under and press <act> once, repeat the three arrows his whiskers flick, and keep every control still while he listens in the rain"),
        LungfishFish => Bind.Fix("a boss fight: a runner; ease off when it runs, tap <act> with its three ripples, alternate <left> and <right> as it walks the bottom, and pump <act> (pull, rest) at its mud hollow"),
        RayFish => Bind.Fix("a boss fight: let go when it runs; follow its feeding sweep with <left>/<right>, let go through a somersault, and hold <down> to bow to a breach"),
        TarponFish => Bind.Fix("a boss fight: press on the gold when he leaps; hold <up> as he rolls to breathe, mash <act> at the pilings and hold <act> through a head-shake"),
        _ => null
    };
}
