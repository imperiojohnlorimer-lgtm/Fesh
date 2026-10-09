#if DEBUG
using Raylib_cs;

namespace Fesh;

// The island guide and the fish album (1.18). FESH_ATLAS_TEST=1 runs only this (it still needs FESH_AUTOTEST and
// FESH_SAVE); the full play-through runs it at the end.
partial class Game
{
    IEnumerable<int> AtlasScript()
    {
        Note("The island guide and the fish album");
        state = new State { created = true, look = new Look { name = "Atlas reader" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        animals.Clear();
        Inp.ScriptMouse = Offscreen;
        yield return 2;

        /* ---------- The data ---------- */
        Check($"every place has a page, with its water and a real-world note ({Regions.Length})",
            Regions.All(r => r.Waters.Length > 0 && r.Real.Length > 60 && r.Land.Length > 20 && r.Climate.Length > 10 && r.Life.Length > 10));
        Check("every cross-section runs left to right across the page, with its labels and lakes on it",
            Regions.Where(r => r.Diagram == "profile").All(r => r.Profile[0].X == 0 && r.Profile[^1].X == 100
                && r.Profile.Zip(r.Profile.Skip(1)).All(p => p.Second.X > p.First.X) && r.Labels.All(l => l.x is >= 0 and <= 100) && r.Lakes.All(l => l.x0 < l.x1)));
        var spotRegions = Data.Spots.Where(s => s.Scene == "world").Select(SpotRegion).Distinct().ToList();
        Check($"every island with a fishing spot has a page ({string.Join(", ", spotRegions.Where(r => !RegionById.ContainsKey(r)))})", spotRegions.All(RegionById.ContainsKey));
        var albumFish = AlbumChapters.SelectMany(c => c.Fish).ToList();
        Check($"every fish and crab is in the album exactly once ({Data.AllCommon.Length} kinds, {albumFish.Count} pages)",
            albumFish.Count == albumFish.Distinct().Count() && Data.AllCommon.All(f => albumFish.Contains(f.Id)) && albumFish.All(Data.FishById.ContainsKey));
        Check("and every one has its family, water and food, and a real-fish note",
            albumFish.All(id => AlbumData.TryGetValue(id, out var a) && a.Family.Contains('(') && a.Water.Split(',').All(w => w.Trim() is "fresh" or "brackish" or "salt") && a.Diet.Length > 4 && FishFacts.ById.ContainsKey(id)));
        Check("crabs, shrimp and lobsters are filed as crustaceans, squid and octopus as cephalopods, not as fish",
            Data.AllCommon.Where(f => FishArt.Looks[f.Id].Shape is "crab" or "lobster" or "shrimp").All(f => AlbumChapters.First(c => c.Id == "crustaceans").Fish.Contains(f.Id))
            && Data.AllCommon.Where(f => FishArt.Looks[f.Id].Shape == "squid").All(f => AlbumChapters.First(c => c.Id == "cephalopods").Fish.Contains(f.Id)));
        Check("every place with a fishing spot lists its fish", Regions.Where(r => r.Id != "sanctuary").All(r => RegionFish(r).Count > 0));

        /* ---------- Only what you've found ---------- */
        Check("at first only Saltmere is open in the guide", AtlasOpen(RegionById["saltmere"]) && !AtlasOpen(RegionById["atoll"]) && !AtlasOpen(RegionById["amihan:Luntian Karsts"]) && !AtlasOpen(RegionById["opensea"]));
        OpenAtlas("amihan:Baga Island"); yield return 3;
        Check($"asking for an island you haven't found opens one you have ({atlasRegion})", mode == "panel" && panel == "atlas" && atlasRegion == "saltmere");
        pendingShot = "360-atlas-saltmere"; yield return 2;
        Check($"Saltmere's page fits ({lastAtlasBottom:0})", lastAtlasBottom <= (Gfx.LH + 664) / 2 - 18);
        ClickButton("atlas:amihan:Baga Island"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("an uncharted island's button does nothing", atlasRegion == "saltmere");
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        /* ---------- From the map ---------- */
        Note("Opening it from the map and the Fesh-dex");
        Inp.Tap(KeyboardKey.Tab); yield return 4;
        Check("the map is open", mode == "panel" && panel == "map");
        float pin0 = state.pinX;
        ClickButton("isle:saltmere"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("clicking Saltmere's name on the map opens its page, without dropping a pin", mode == "panel" && panel == "atlas" && atlasRegion == "saltmere" && state.pinX == pin0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.Tab); yield return 4;
        ClickButton("Island guide"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("the map's Island guide button opens it on where you are", panel == "atlas" && atlasRegion == "saltmere");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.J); yield return 4;
        ClickButton("Island guide"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("and so does the Fesh-dex's", panel == "atlas");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.J); yield return 4;
        ClickButton("Fish album"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("the Fesh-dex's Fish album button opens the album", panel == "album" && albumSpread == 0);
        pendingShot = "370-album-intro"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        /* ---------- Every page, once found ---------- */
        Note("Every island's page");
        state.charted = Regions.Select(r => r.Id).Concat(new[] { "habagat:islet:0" }).ToList();
        state.caveDeepest = 12; state.inv["boat"] = 1; state.hinted["amihan"] = state.hinted["habagat"] = state.hinted["metJoy"] = true;
        foreach (var f in Data.AllCommon.Where((f, i) => i % 3 == 0)) state.commons[f.Id] = 1;
        Check("found, every place opens", Regions.All(AtlasOpen));
        OpenAtlas("saltmere"); yield return 2;
        bool fits = true;
        string worst = "";
        foreach (var r in Regions)
        {
            ClickButton("atlas:" + r.Id); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
            if (atlasRegion != r.Id) { fits = false; worst = r.Id + " didn't open"; break; }
            if (lastAtlasBottom > (Gfx.LH + 664) / 2 - 18) { fits = false; worst = $"{r.Id} ends at {lastAtlasBottom:0}"; }
            if (r.Id is "atoll" or "amihan:Luntian Karsts" or "amihan:Baga Island" or "caverns" or "opensea" or "sanctuary" or "habagat:Asinan" or "frost")
            { pendingShot = "361-atlas-" + r.Id.Replace(':', '-').Replace(' ', '-'); yield return 2; }
        }
        Check($"every page opens by its button and fits the panel ({worst})", fits);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // The first island charted after Saltmere: a word about the guide.
        state.charted = new() { "saltmere" }; state.hinted.Remove("atlasHint"); badgeNews = null;
        player.X = 530; player.Y = 125; yield return 3;
        ChartHere(); yield return 2;
        Check($"charting a new island mentions the guide ({badgeNews ?? toastMsg})", (badgeNews ?? toastMsg).Contains("island's name on the map"));
        state.charted = Regions.Select(r => r.Id).Concat(new[] { "habagat:islet:0" }).ToList();
        player.X = 160; player.Y = 115; toastTimer = 0; yield return 3;

        /* ---------- The album ---------- */
        Note("The fish album");
        OpenAlbum(); yield return 3;
        int spreads = AlbumPages().Count / 2;
        bool albumFits = true;
        for (int s = 0; s < spreads; s++)
        {
            albumSpread = s; yield return 2;
            if (lastAlbumBottom > AlbumPageH - 24) { albumFits = false; worst = $"spread {s} ends at {lastAlbumBottom:0}"; }
        }
        Check($"every page of the album fits ({spreads} spreads; {worst})", albumFits);
        // And with every fish caught, every entry filled in (Codex: a third caught didn't test the full pages).
        var savedCommons = new Dictionary<string, int>(state.commons);
        foreach (var f in Data.AllCommon) { state.commons[f.Id] = 3; state.records[f.Id] = f.Kg * 1.4f; }
        albumFits = true;
        for (int s = 0; s < spreads; s++)
        {
            albumSpread = s; yield return 2;
            if (lastAlbumBottom > AlbumPageH - 24) { albumFits = false; worst = $"spread {s} ends at {lastAlbumBottom:0}"; }
        }
        Check($"and with every fish caught ({worst})", albumFits);
        pendingShot = "376-album-full"; yield return 2;
        state.commons = savedCommons; state.records.Clear();
        albumSpread = 0; yield return 1;
        Inp.Tap(KeyboardKey.Right); yield return 3;
        Check("the right arrow turns the page", albumSpread == 1);
        pendingShot = "371-album-sharks"; yield return 2;
        Inp.Tap(KeyboardKey.Left); yield return 3;
        Check("and the left arrow turns it back", albumSpread == 0);
        ClickButton("album:crustaceans"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        var shown = AlbumPages()[albumSpread * 2].fish.Concat(AlbumPages()[albumSpread * 2 + 1].fish).ToList();
        Check($"a chapter tab opens its chapter ({string.Join(", ", shown)})", shown.Contains("shore_crab"));
        pendingShot = "372-album-crustaceans"; yield return 2;
        ClickButton("Next >"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("Next turns the page", AlbumPages()[albumSpread * 2].fish.Concat(AlbumPages()[albumSpread * 2 + 1].fish).Any(id => id is "glow_shrimp" or "spiny_lobster" or "alimasag_crab" or "lantern_squid"));
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // A full chapter pays once.
        var ceph = AlbumChapters.First(c => c.Id == "cephalopods");
        foreach (var id in ceph.Fish.Skip(1)) state.commons[id] = 1;
        state.commons.Remove(ceph.Fish[0]);
        int coins = state.coins; badgeNews = null;
        AddCatch(Data.FishById[ceph.Fish[0]]);
        Check($"filling a chapter pays five coins a page ({state.coins - coins})", state.Hinted("album:cephalopods") && state.coins - coins == 5 * ceph.Fish.Length && badgeNews?.Contains("chapter is complete") == true);
        AddCatch(Data.FishById[ceph.Fish[0]]);
        Check("but only once", state.coins - coins == 5 * ceph.Fish.Length);
        toastTimer = 0; yield return 3;
        // The fish card's Album page button goes to that fish.
        dexFish = "pugita"; panel = "dex"; mode = "panel"; yield return 3;
        ClickButton("Album page"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        var here = AlbumPages()[albumSpread * 2].fish.Concat(AlbumPages()[albumSpread * 2 + 1].fish);
        Check("a fish card's Album page button opens the album at that fish", panel == "album" && here.Contains("pugita"));
        pendingShot = "373-album-cephalopods"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // The first five fish bring a word about the album.
        state.commons.Clear(); state.hinted.Remove("albumHint"); badgeNews = null;
        foreach (var f in Data.AllCommon.Where(f => !f.Legend).Take(5)) AddCatch(f);
        Check($"five kinds caught mentions the album ({badgeNews})", badgeNews?.Contains("fish album") == true);
        badgeNews = null;
        // From the journal's Sea school tab.
        Inp.Tap(KeyboardKey.Q); yield return 3;
        ClickButton("Sea school"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"the Sea school tab still fits with its buttons ({lastJournalBottom:0})", lastJournalBottom <= (Gfx.LH + 650) / 2 - 24);
        pendingShot = "374-journal-buttons"; yield return 2;
        ClickButton("Fish album"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("and opens the album", panel == "album");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.Q); yield return 3;
        ClickButton("Island guide"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("and the island guide", panel == "atlas");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        journalSide = "basics";
        // The Fesh-dex header still fits its buttons.
        Inp.Tap(KeyboardKey.J); yield return 3;
        bool headerFits = Gfx.Seen.TryGetValue("Island guide", out var ig) && Gfx.Seen.TryGetValue("Close", out var cl) && ig.X + ig.Width < cl.X - 4;
        Check("the Fesh-dex's new buttons fit beside Close", headerFits);
        pendingShot = "375-dex-header"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // Nothing names a fish you haven't caught (Codex): the guide's and the album's own words, with nothing caught.
        // (Bangus is named by Niko before you catch one, and crayfish is the crustacean chapter's own subject.)
        var names = Data.AllCommon.Select(f => f.Name.Split(" (")[0]).Where(n => n is not ("Bangus" or "Crayfish")).Distinct().ToList();
        string allText = string.Join(" ", Regions.SelectMany(r => new[] { r.Land, r.Life, r.Climate, r.Real }.Concat(r.Waters.Select(w => w.Name))))
            + " " + string.Join(" ", AlbumChapters.Select(c => c.Blurb));
        var named = names.Where(n => System.Text.RegularExpressions.Regex.IsMatch(allText, @"\b" + System.Text.RegularExpressions.Regex.Escape(n) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
        Check($"the guide's and album's own words name no fish you might not have caught ({string.Join(", ", named)})", named.Count == 0);
        Check("the sanctuary's page waits until you've met its warden, and no page names Tidemane",
            !Regions.Any(r => (r.Land + r.Life + r.Real).Contains("Tidemane")) && !AtlasOpen(new RegionInfo("sanctuary", "", "", "", new WaterBody[0], "", "", "", new ProfilePoint[0], new (float, float, float, bool)[0], new (float, string)[0], new (float, string)[0])) == !state.Hinted("metJoy"));
        // A save from before the album with a chapter already caught pays for it on loading.
        var cephs = AlbumChapters.First(c => c.Id == "cephalopods").Fish;
        state.hinted.Remove("album:cephalopods"); foreach (var id in cephs) state.commons[id] = 1;
        int c0 = state.coins; Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; standStill = true; yield return 3;
        Check($"an older save's complete chapter pays when it loads ({state.coins - c0})", state.Hinted("album:cephalopods") && state.coins - c0 >= 5 * cephs.Length);
        // Old saves have nothing new to load.
        SaveFile.Clear(3);
        File.WriteAllText(SaveFile.SlotPath(3), "{\"created\":true,\"inv\":{\"rod_old\":1}}");
        var old = SaveFile.Read(3);
        Check("older saves still load", old != null && old.charted == null && old.commons != null);
        SaveFile.Clear(3);
        Inp.ScriptMouse = Offscreen;
    }
}
#endif
