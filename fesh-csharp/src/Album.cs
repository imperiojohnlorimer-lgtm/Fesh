using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The fish album (1.18), like the ones made for school: a scrapbook with a page for every fish you've caught, taped in
// with its names (local, English and scientific), its family, the water it lives in and what it eats. The chapters
// follow how scientists group the animals, so the album teaches classification as it fills: sharks and rays have
// skeletons of cartilage, carp and catfish share a hearing bone chain, and crabs and squid aren't fish at all.
// Empty frames wait for the ones you haven't caught (with no name, so nothing is given away). A full chapter pays a
// few coins. Open it from the Fesh-dex, the journal's Sea school tab, or the island guide.
sealed record AlbumChapter(string Id, string Title, string Blurb, string[] Fish);
sealed record AlbumInfo(string Family, string Water, string Diet);   // Water: "fresh", "brackish", "salt", or several with commas

partial class Game
{
    public static readonly AlbumChapter[] AlbumChapters =
    {
        new("sharks", "Sharks and rays",
            "Their skeletons are made of cartilage, the bendy stuff in your nose and ears, not bone. They breathe through five to seven gill slits, and their skin is covered in tiny tooth-like scales.",
            new[] { "pating", "thunderfin", "pale_cave_shark", "sand_ray", "pagi", "starfall_ray" }),
        new("ancient", "Ancient lines",
            "Groups that split off early in the history of fish. Lobe-finned fish are closer kin to the first four-legged land animals than to a tuna. Bonytongues have teeth on their tongue. Some others start life as see-through, leaf-shaped larvae.",
            new[] { "ancient_coelacanth", "sunscale_lungfish", "emerald_arowana", "mire_leviathan", "buan_buan", "haring_buan", "ghost_eel" }),
        new("carps", "Carp, catfish and their kin",
            "Mostly fresh-water fish. In carp, catfish and many of their relatives, a chain of tiny bones links the swim bladder to the ear, giving them sharp hearing. A few pond and sea fish are distant cousins in the same big group.",
            new[] { "bangus", "mud_carp", "moon_carp", "mirage_koi", "old_whiskers", "swamp_catfish", "hito", "moonglass_fish", "jungle_piranha", "blind_cavefish", "storm_eel" }),
        new("cold", "Cold and deep waters",
            "Fish of icy lakes, polar seas and the deep. Cold water holds more oxygen than warm. Some polar fish make an antifreeze in their blood, and many deep-sea fish swim up toward the surface at night to feed and back down by day.",
            new[] { "arctic_char", "aurora_trout", "crystal_pike", "frost_smelt", "polar_cod", "abyssal_lanternfish" }),
        new("tunas", "Tunas, mackerels and billfish",
            "Built for speed: torpedo-shaped bodies, crescent tails, and fins that fold away into grooves. Tunas can keep their muscles warmer than the sea around them. Some slash through schools of fish with their long bills.",
            new[] { "yellowfin_tuna", "bluefin_tuna", "silver_tuna", "tulingan", "tanigue", "alumahan", "sun_mackerel", "malasugi", "sailfish", "golden_marlin", "ironbill", "swordfish" }),
        new("jacks", "Jacks and open-water fish",
            "Silver hunters and drifters of open water. Many swim in schools, and most are countershaded: dark on the back and pale on the belly, so they're hard to see from above or below.",
            new[] { "talakitok", "galunggong", "matang_baka", "salay_salay", "talang_talang", "mahi_mahi", "amihan_barracuda", "flying_fish", "ocean_sunfish", "silver_moonfish" }),
        new("groupers", "Groupers, snappers and breams",
            "Big-mouthed fish of reefs and rocky bottoms. Many suck their prey in with a sudden gulp, and many groupers start life as females and become males as they grow.",
            new[] { "lapu_lapu", "rusty_grouper", "blue_hole_grouper", "barnacle_bass", "maya_maya", "dalagang_bukid", "bisugo", "pond_perch" }),
        new("reef", "Reef fish",
            "The colourful crowd of the coral reef. Grazing fish keep algae from smothering the coral, and some crunch up dead coral and turn it into sand.",
            new[] { "striped_wrasse", "parrotfish", "clownfish", "moorish_idol", "pearl_angelfish", "labahita", "danggit" }),
        new("shore", "Shore, pond and mangrove fish",
            "Fish of beaches, ponds, mangroves and river mouths. Many can move between fresh, brackish and salt water, and some can even breathe air.",
            new[] { "banak", "kitang", "asohos", "sapsap", "tamban", "dalag", "oasis_tilapia", "desert_pupfish", "rock_goby", "seafoam_goby", "amihan_mudskipper", "mudskipper" }),
        new("crustaceans", "Crustaceans: not fish at all",
            "Crabs, lobsters, crayfish and shrimp have no backbone. They wear a hard outer skeleton, which they shed to grow, and have jointed legs: ten of them, counting the claws.",
            new[] { "shore_crab", "snow_crab", "king_crab", "ghost_crab", "crayfish", "spiny_lobster", "alimasag_crab", "alimango_crab", "curacha_crab", "sugpo_shrimp", "glow_shrimp" }),
        new("cephalopods", "Squid and octopus: not fish either",
            "Cephalopods are mollusks, like snails and clams, but fast and clever. Squid and octopuses have three hearts and blue blood, and many squirt ink and change colour in an instant.",
            new[] { "lantern_squid", "giant_squid", "pugita", "pusit" })
    };

    // Family, water and food, for the real animal (or, for one made up for the game, the real one it's based on).
    public static readonly Dictionary<string, AlbumInfo> AlbumData = new()
    {
        ["pating"] = new("Requiem sharks (Carcharhinidae)", "salt,brackish", "Hunts schooling fish"),
        ["thunderfin"] = new("Requiem sharks (Carcharhinidae)", "salt,brackish", "Hunts schooling fish"),
        ["pale_cave_shark"] = new("Nurse sharks (Ginglymostomatidae)", "salt", "Sucks crabs, octopus and fish off the bottom"),
        ["sand_ray"] = new("Stingrays (Dasyatidae)", "salt,brackish", "Digs up clams, crabs and worms"),
        ["pagi"] = new("Stingrays (Dasyatidae)", "salt", "Snails, worms, shrimp and crabs in the sand"),
        ["starfall_ray"] = new("Manta rays (Mobulidae)", "salt", "Filters plankton from the water"),
        ["ancient_coelacanth"] = new("Coelacanths (Latimeriidae)", "salt", "Hunts fish and squid at night"),
        ["sunscale_lungfish"] = new("African lungfishes (Protopteridae)", "fresh", "Snails, small fish, frogs and plants"),
        ["emerald_arowana"] = new("Bonytongues (Osteoglossidae)", "fresh", "Insects, small fish and frogs, even off branches"),
        ["mire_leviathan"] = new("Arapaimas (Arapaimidae)", "fresh", "Hunts fish, gulping air at the surface"),
        ["buan_buan"] = new("Tarpons (Megalopidae)", "fresh,brackish,salt", "Small fish and shrimp"),
        ["haring_buan"] = new("Tarpons (Megalopidae)", "fresh,brackish,salt", "Small fish and crabs"),
        ["ghost_eel"] = new("Freshwater eels (Anguillidae)", "fresh,brackish,salt", "Fish, crayfish and worms"),
        ["bangus"] = new("Milkfish (Chanidae)", "fresh,brackish,salt", "Grazes algae and tiny drifting life"),
        ["mud_carp"] = new("Carps (Cyprinidae)", "fresh", "Algae, plant bits and rotting matter on the bottom"),
        ["moon_carp"] = new("Carps (Cyprinidae)", "fresh,brackish", "Roots in the mud for worms, insects and seeds"),
        ["mirage_koi"] = new("Carps (Cyprinidae)", "fresh", "Plants, insects and worms"),
        ["old_whiskers"] = new("Sheatfishes (Siluridae)", "fresh,brackish", "Fish, frogs and even birds"),
        ["swamp_catfish"] = new("Catfishes (order Siluriformes)", "fresh", "Almost anything on the bottom"),
        ["hito"] = new("Air-breathing catfishes (Clariidae)", "fresh,brackish", "Insects, worms, small fish and scraps"),
        ["moonglass_fish"] = new("Sheatfishes (Siluridae)", "fresh", "Tiny insects and plankton"),
        ["jungle_piranha"] = new("Piranhas (Serrasalmidae)", "fresh", "Fish, insects, shrimp, seeds and carrion"),
        ["blind_cavefish"] = new("American tetras (Acestrorhamphidae, once Characidae)", "fresh", "Whatever drifts into the cave"),
        ["storm_eel"] = new("Electric eels (Electrophoridae or Gymnotidae)", "fresh", "Fish, stunned with electricity"),
        ["arctic_char"] = new("Salmon and trout (Salmonidae)", "fresh,brackish,salt", "Insects, snails and small fish"),
        ["aurora_trout"] = new("Salmon and trout (Salmonidae)", "fresh", "Insects and small fish"),
        ["crystal_pike"] = new("Pikes (Esocidae)", "fresh,brackish", "Ambushes fish and frogs"),
        ["frost_smelt"] = new("Smelts (Osmeridae)", "fresh,brackish,salt", "Plankton and small fish"),
        ["polar_cod"] = new("Cods (Gadidae)", "salt,brackish", "Tiny shrimp-like plankton under the sea ice"),
        ["abyssal_lanternfish"] = new("Lanternfishes (Myctophidae)", "salt", "Plankton; many rise near the surface at night"),
        ["yellowfin_tuna"] = new("Tunas and mackerels (Scombridae)", "salt", "Fish, squid and shrimp"),
        ["bluefin_tuna"] = new("Tunas and mackerels (Scombridae)", "salt", "Fish and squid"),
        ["silver_tuna"] = new("Tunas and mackerels (Scombridae)", "salt", "Fish and squid"),
        ["tulingan"] = new("Tunas and mackerels (Scombridae)", "salt", "Small fish, shrimp and squid"),
        ["tanigue"] = new("Tunas and mackerels (Scombridae)", "salt", "Sardines, anchovies and squid"),
        ["alumahan"] = new("Tunas and mackerels (Scombridae)", "salt", "Filters plankton"),
        ["sun_mackerel"] = new("Tunas and mackerels (Scombridae)", "salt,brackish", "Plankton and small fish"),
        ["malasugi"] = new("Marlins and sailfish (Istiophoridae)", "salt", "Tunas, mackerels and squid"),
        ["sailfish"] = new("Marlins and sailfish (Istiophoridae)", "salt", "Sardines and other schooling fish"),
        ["golden_marlin"] = new("Marlins and sailfish (Istiophoridae)", "salt", "Fish and squid"),
        ["ironbill"] = new("Marlins and sailfish (Istiophoridae)", "salt", "Tunas, mackerels and squid"),
        ["swordfish"] = new("Swordfish (Xiphiidae)", "salt", "Fish and squid: deep by day, nearer the surface at night"),
        ["talakitok"] = new("Jacks (Carangidae)", "salt,brackish", "Hunts fish and crabs"),
        ["galunggong"] = new("Jacks (Carangidae)", "salt", "Small shrimp-like plankton and small fish"),
        ["matang_baka"] = new("Jacks (Carangidae)", "salt", "Shrimp, plankton and small fish, at night"),
        ["salay_salay"] = new("Jacks (Carangidae)", "salt,brackish", "Plankton and small shrimp"),
        ["talang_talang"] = new("Jacks (Carangidae)", "salt,brackish", "Hunts small fish"),
        ["mahi_mahi"] = new("Dolphinfishes (Coryphaenidae)", "salt", "Flying fish, squid and small fish"),
        ["amihan_barracuda"] = new("Barracudas (Sphyraenidae)", "salt,brackish", "Hunts fish"),
        ["flying_fish"] = new("Flying fishes (Exocoetidae)", "salt", "Plankton"),
        ["ocean_sunfish"] = new("Molas (Molidae)", "salt", "Jellyfish, and fish and crustaceans too"),
        ["silver_moonfish"] = new("Moonfish (Menidae)", "salt,brackish", "Small animals on and near the seabed"),
        ["lapu_lapu"] = new("Groupers (Epinephelidae, once part of Serranidae)", "salt", "Ambushes fish and crabs"),
        ["rusty_grouper"] = new("Groupers (Epinephelidae, once part of Serranidae)", "salt,brackish", "Crabs, lobsters and fish"),
        ["blue_hole_grouper"] = new("Groupers (Epinephelidae, once part of Serranidae)", "salt,brackish", "Fish, lobsters, even small sharks"),
        ["barnacle_bass"] = new("Temperate basses (Moronidae)", "fresh,brackish,salt", "Small fish, crabs and shrimp"),
        ["maya_maya"] = new("Snappers (Lutjanidae)", "salt,brackish", "Fish, shrimp and crabs"),
        ["dalagang_bukid"] = new("Snappers and fusiliers (Lutjanidae)", "salt", "Plankton"),
        ["bisugo"] = new("Threadfin breams (Nemipteridae)", "salt", "Shrimp, worms and small fish on the mud"),
        ["pond_perch"] = new("Perches (Percidae)", "fresh,brackish", "Insects when small, then fish"),
        ["striped_wrasse"] = new("Wrasses (Labridae)", "salt", "Picks parasites off other fish"),
        ["parrotfish"] = new("Parrotfishes (Scaridae, or part of the wrasses, Labridae)", "salt", "Scrapes algae off coral and rock"),
        ["clownfish"] = new("Damselfishes (Pomacentridae)", "salt", "Plankton and algae"),
        ["moorish_idol"] = new("Moorish idol (Zanclidae)", "salt", "Sponges and small animals"),
        ["pearl_angelfish"] = new("Marine angelfishes (Pomacanthidae)", "salt", "Sponges, algae and small animals"),
        ["labahita"] = new("Surgeonfishes (Acanthuridae)", "salt", "Mostly algae"),
        ["danggit"] = new("Rabbitfishes (Siganidae)", "salt,brackish", "Grazes algae and seagrass"),
        ["banak"] = new("Mullets (Mugilidae)", "fresh,brackish,salt", "Sifts algae and bits from the mud"),
        ["kitang"] = new("Scats (Scatophagidae)", "brackish,salt,fresh", "Algae, worms and scraps"),
        ["asohos"] = new("Sillagos (Sillaginidae)", "salt,brackish", "Worms and small shrimp in the sand"),
        ["sapsap"] = new("Ponyfishes (Leiognathidae)", "salt,brackish", "Plankton, worms and tiny shrimp"),
        ["tamban"] = new("Sardines (Dorosomatidae, long counted with Clupeidae)", "salt", "Plankton"),
        ["dalag"] = new("Snakeheads (Channidae)", "fresh,brackish", "Fish, frogs and insects"),
        ["oasis_tilapia"] = new("Cichlids (Cichlidae)", "fresh,brackish", "Algae, plants and bits on the bottom"),
        ["desert_pupfish"] = new("Pupfishes (Cyprinodontidae)", "fresh,brackish", "Algae, insects and tiny animals"),
        ["rock_goby"] = new("Gobies (Gobiidae)", "salt,brackish", "Small shrimp and worms"),
        ["seafoam_goby"] = new("Gobies (Gobiidae)", "salt", "Small shrimp and worms"),
        ["amihan_mudskipper"] = new("Mudskippers (Oxudercidae, often put with the gobies)", "brackish,salt", "Insects, worms and tiny crabs on the mud"),
        ["mudskipper"] = new("Mudskippers (Oxudercidae, often put with the gobies)", "brackish,salt", "Insects, worms and tiny crabs on the mud"),
        ["shore_crab"] = new("Shore crabs (Carcinidae)", "salt,brackish", "Mussels, worms and smaller crabs"),
        ["snow_crab"] = new("Snow crabs (Oregoniidae)", "salt", "Worms, clams and brittle stars"),
        ["king_crab"] = new("King crabs (Lithodidae)", "salt", "Worms, clams and sea urchins"),
        ["ghost_crab"] = new("Ghost crabs (Ocypodidae)", "salt", "Beach scraps, insects and turtle hatchlings"),
        ["crayfish"] = new("Crayfish (infraorder Astacidea)", "fresh", "Plants, snails and dead animals"),
        ["spiny_lobster"] = new("Spiny lobsters (Palinuridae)", "salt", "Snails, mussels, sea urchins and scraps"),
        ["alimasag_crab"] = new("Swimming crabs (Portunidae)", "salt,brackish", "Clams, small fish and scraps"),
        ["alimango_crab"] = new("Swimming crabs (Portunidae)", "brackish,salt", "Clams, snails, small crabs and scraps"),
        ["curacha_crab"] = new("Frog crabs (Raninidae)", "salt", "Small animals, ambushed from the sand"),
        ["sugpo_shrimp"] = new("Penaeid prawns (Penaeidae)", "salt,brackish", "Small animals, algae and bits on the bottom"),
        ["glow_shrimp"] = new("Deep-sea shrimps (Acanthephyridae)", "salt", "Small animals and bits drifting down"),
        ["lantern_squid"] = new("Lantern squids (Lycoteuthidae)", "salt", "Small fish and shrimp"),
        ["giant_squid"] = new("Giant squids (Architeuthidae)", "salt", "Fish and other squid"),
        ["pugita"] = new("Octopuses (Octopodidae)", "salt", "Crabs, clams and shrimp"),
        ["pusit"] = new("Inshore squids (Loliginidae)", "salt", "Small fish and shrimp")
    };

    /* ---------- Pages ---------- */
    // Page 0 is how to read the album; each chapter then starts on a new page with its heading and two fish, and goes
    // on four to a page. Two pages show at a time, like an open book.
    List<(int chapter, string[] fish)> albumPages;
    List<(int chapter, string[] fish)> AlbumPages()
    {
        if (albumPages != null) return albumPages;
        albumPages = new() { (-1, new string[0]) };
        for (int c = 0; c < AlbumChapters.Length; c++)
        {
            var f = AlbumChapters[c].Fish;
            albumPages.Add((c, f.Take(2).ToArray()));
            for (int i = 2; i < f.Length; i += 4) albumPages.Add((c, f.Skip(i).Take(4).ToArray()));
        }
        if (albumPages.Count % 2 == 1) albumPages.Add((-2, new string[0]));
        return albumPages;
    }

    int albumSpread;
    float lastAlbumBottom;      // the lowest text drawn on a page (the autotest checks it fits)

    bool InAlbum(string id) => state.commons.GetValueOrDefault(id) > 0;

    void OpenAlbum(string fish = null)
    {
        if (fish != null)
        {
            int p = AlbumPages().FindIndex(pg => pg.fish.Contains(fish));
            if (p >= 0) albumSpread = p / 2;
        }
        Learned("album");
        Sfx.Play("ui");
        panel = "album";
        mode = "panel";
        SetPrompt("");
    }

    // A chapter complete pays five coins a fish, once.
    void CheckAlbum(string id)
    {
        int c = Array.FindIndex(AlbumChapters, ch => ch.Fish.Contains(id));
        if (c < 0) return;
        var ch = AlbumChapters[c];
        if (state.Hinted("album:" + ch.Id) || !ch.Fish.All(InAlbum)) return;
        state.hinted["album:" + ch.Id] = true;
        int pay = 5 * ch.Fish.Length;
        state.coins += pay;
        ToastLater($"Fish album: the \"{ch.Title}\" chapter is complete! {pay} coins for the collection.");
    }

    // A save from before the album may have whole chapters caught already: they pay once, on loading.
    void ReconcileAlbum()
    {
        var done = AlbumChapters.Where(ch => !state.Hinted("album:" + ch.Id) && ch.Fish.All(InAlbum)).ToList();
        if (done.Count == 0) return;
        int pay = 0;
        foreach (var ch in done) { state.hinted["album:" + ch.Id] = true; pay += 5 * ch.Fish.Length; }
        state.coins += pay;
        ToastLater($"Your new fish album already has {done.Count} complete chapter{(done.Count == 1 ? "" : "s")}! {pay} coins for the collection.");
    }

    void UpdateAlbum()
    {
        if (dexFish != null) return;     // a fish's card is open over the album
        int spreads = AlbumPages().Count / 2;
        if (Bind.Pressed("left")) { albumSpread = Math.Max(0, albumSpread - 1); Sfx.Play("blip"); }
        else if (Bind.Pressed("right")) { albumSpread = Math.Min(spreads - 1, albumSpread + 1); Sfx.Play("blip"); }
    }

    static readonly Color AlbumCover = Pal.C("#6b4a2b"), AlbumPage = Pal.C("#f4ead2"), AlbumTape = Pal.C("rgba(242,226,170,0.85)");

    void DrawAlbum()
    {
        Backdrop();
        // A fish's card, opened by clicking its photo: Back (or Esc) returns to the album.
        if (dexFish != null) { DrawFishCard(); return; }
        var pages = AlbumPages();
        int spreads = pages.Count / 2;
        albumSpread = Math.Clamp(albumSpread, 0, spreads - 1);
        const float w = 1220, h = AlbumH;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        // The cover, then two pages and the spine; the page turning sits on the cover below them.
        Gfx.Rect(x, y, w, h, AlbumCover, 10);
        float pw = (w - 190) / 2, ph = AlbumPageH, px0 = x + 18, py = y + 46;
        Gfx.Text("Fish album", x + 22, y + 10, FontKind.Ui700, 26, Pal.C("#f2e2b0"));
        int caught = AlbumChapters.Sum(ch => ch.Fish.Count(InAlbum)), total = AlbumChapters.Sum(ch => ch.Fish.Length);
        string filled = $"{caught} of {total} pages filled  ·  click a photo to look closer";
        Gfx.Text(filled, x + 22 + Gfx.Measure("Fish album", FontKind.Ui700, 26) + 18, y + 18, FontKind.Ui600, 16, Pal.C("#e2c88e"));
        if (SmallButton("Close", x + w - 18 - SmallW("Close"), y + 4)) { ClosePanels(); return; }
        for (int side = 0; side < 2; side++)
        {
            float px = px0 + side * (pw + 6);
            Gfx.Rect(px, py, pw, ph, AlbumPage, 4);
            Gfx.Rect(side == 0 ? px + pw - 10 : px, py, 10, ph, Pal.C("rgba(120,90,50,0.12)"));
            var (chapter, fish) = pages[albumSpread * 2 + side];
            float bottom = DrawAlbumPage(chapter, fish, px + 16, py + 14, pw - 32, ph - 28, albumSpread * 2 + side);
            lastAlbumBottom = side == 0 ? bottom - py : Math.Max(lastAlbumBottom, bottom - py);
            Gfx.TextCenter($"{albumSpread * 2 + side + 1}", px + pw / 2, py + ph - 22, FontKind.Note, 14, Muted);
            if (dexFish != null) return;    // a photo was clicked: its card opens next frame
        }
        // Page turning, on the cover under the pages.
        float by = py + ph + 8;
        if (Button("< Previous", px0, by, 120, 28, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 2, 2, 4, albumSpread > 0)) { albumSpread--; Sfx.Play("blip"); }
        if (Button("Next >", px0 + 2 * pw + 6 - 120, by, 120, 28, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 2, 2, 4, albumSpread < spreads - 1)) { albumSpread++; Sfx.Play("blip"); }
        Gfx.TextCenter(Bind.Fix("Turn the pages with <left> and <right>"), px0 + pw + 3, by + 5, FontKind.Ui600, 15, Pal.C("#f2e2b0"));

        // Chapter tabs down the right edge, starred when the chapter's full. Long titles take three short lines.
        float tx = px0 + 2 * pw + 18, ty = py, tw = x + w - 12 - tx;
        lastAlbumTabCut = false;
        for (int c = -1; c < AlbumChapters.Length; c++)
        {
            string label = c < 0 ? "How to read it" : AlbumChapters[c].Title.Split(':')[0];
            int first = pages.FindIndex(pg => pg.chapter == c);
            bool on = pages[albumSpread * 2].chapter == c || pages[albumSpread * 2 + 1].chapter == c;
            var fill = on ? Pal.Lantern : Pal.Sand;
#if DEBUG
            Gfx.Seen[$"album:{(c < 0 ? "intro" : AlbumChapters[c].Id)}"] = new Rectangle(tx, ty, tw, AlbumTabH);
#endif
            if (Button("", tx, ty, tw, AlbumTabH, FontKind.Ui600, 13, fill, Pal.Ink, 2, 2, 4)) { albumSpread = first / 2; Sfx.Play("blip"); return; }
            var lines = Gfx.Wrap(label, FontKind.Ui700, 12, tw - (c >= 0 ? 40 : 12));
            lastAlbumTabCut |= lines.Count > 3;
            Lines(lines, tx + 7, ty + AlbumTabH / 2 - lines.Count * 7 + 1, 14, FontKind.Ui700, 12, Pal.Ink);
            if (c >= 0)
            {
                var ch = AlbumChapters[c];
                if (state.Hinted("album:" + ch.Id)) DrawIcon("star", tx + tw - 24, ty + AlbumTabH / 2 - 10, 20);
                else { string n = $"{ch.Fish.Count(InAlbum)}/{ch.Fish.Length}"; Gfx.Text(n, tx + tw - 8 - Gfx.Measure(n, FontKind.Ui600, 12), ty + AlbumTabH / 2 - 7, FontKind.Ui600, 12, Muted); }
            }
            ty += AlbumTabH + 4;
        }
        lastAlbumTabsBottom = ty - 4 - py;
        if (Gfx.PressedOutside(x, y, w, h)) ClosePanels();
    }

    const float AlbumH = 700, AlbumTabH = 44;
    bool lastAlbumTabCut;           // a chapter tab's title needed more than three lines (the autotest checks none do)
    float lastAlbumTabsBottom;      // where the tabs ended, below the top of the pages

    // One page: the intro, a chapter's opening (heading, blurb and two fish), or four fish. Returns where it ended.
    float DrawAlbumPage(int chapter, string[] fish, float x, float y, float w, float h, int page)
    {
        if (chapter == -2)
        {
            // The last page: how far the album has come.
            int got = AlbumChapters.Sum(ch => ch.Fish.Count(InAlbum)), all = AlbumChapters.Sum(ch => ch.Fish.Length);
            int full = AlbumChapters.Count(ch => ch.Fish.All(InAlbum));
            Gfx.Text("The last page", x, y, FontKind.Ui700, 22, Pal.PaperInk);
            var l = Gfx.Wrap($"{got} of the {all} animals in the islands' waters are in this album, and {full} of its {AlbumChapters.Length} chapters are complete. Scientists describe new kinds of fish every year, so a real fish album is never quite finished.", FontKind.Note, 16, w);
            Lines(l, x, y + 34, 21, FontKind.Note, 16, Pal.PaperInk);
            return y + 34 + l.Count * 21;
        }
        if (chapter == -1)
        {
            Gfx.Text("How to read this album", x, y, FontKind.Ui700, 22, Pal.PaperInk);
            y += 34;
            var text = new[]
            {
                ("Every species has a scientific name", "Every kind of animal scientists have described has a two-word name, genus then species: people are Homo sapiens. Scientists everywhere use the same name, whatever the animal is called in their own language."),
                ("Families", "Close relatives are grouped into a family; an animal family's name ends in -idae. Cats, from house cats to tigers, are Felidae; dogs and wolves are Canidae. Families are grouped into bigger groups: orders and classes. Scientists still argue over where a few families belong, so some pages give two names."),
                ("What makes a fish a fish?", "A backbone, gills for breathing in water, and fins. So the crabs, shrimp, squid and octopus at the back of this album aren't fish at all, whatever the market calls them."),
                ("Fresh, brackish and salt", "Each page says what water the animal lives in. Brackish water, in mangroves and river mouths, is a mix of fresh and salt."),
                ("Made up for Fesh", "A few fish here are made up. Their pages name the real animal they're based on.")
            };
            foreach (var (head, body) in text)
            {
                Gfx.Text(head, x, y, FontKind.Ui700, 16, Pal.C("#8a5a2a"));
                var l = Gfx.Wrap(body, FontKind.Note, 15, w);
                Lines(l, x, y + 21, 19, FontKind.Note, 15, Pal.PaperInk);
                y += 21 + l.Count * 19 + 12;
            }
            return y;
        }
        var ch = AlbumChapters[chapter];
        bool opening = fish.Length > 0 && ch.Fish[0] == fish[0];
        float slotY = y;
        if (opening)
        {
            Gfx.Text(ch.Title, x, y, FontKind.Ui700, 22, Pal.PaperInk);
            var l = Gfx.Wrap(ch.Blurb, FontKind.Note, 15, w);
            Lines(l, x, y + 32, 19, FontKind.Note, 15, Pal.PaperInk);
            slotY = y + 32 + l.Count * 19 + 14;
        }
        // Two fish across, two rows down (one row under a chapter's heading).
        float cw = (w - 16) / 2, chh = AlbumEntryH, end = slotY;
        for (int i = 0; i < fish.Length; i++)
        {
            float cx = x + i % 2 * (cw + 16), cy = slotY + i / 2 * (chh + 10);
            end = Math.Max(end, DrawAlbumEntry(fish[i], cx, cy, cw, chh, page * 10 + i));
        }
        return end;
    }

    // The pages sit between the cover's header and the page-turning strip at the bottom.
    const float AlbumEntryH = 274, AlbumPageH = AlbumH - 46 - 48;

    float DrawAlbumEntry(string id, float x, float y, float w, float h, int seed)
    {
        var f = Data.FishById[id];
        bool got = InAlbum(id);
        // The photo: a white border, the fish on a blue ground, and two bits of tape.
        float tilt = (seed * 37 % 7 - 3) * 0.6f;
        float fx = x + 6, fy = y + 4 + tilt, fw = w - 12, fh = 88;
#if DEBUG
        Gfx.Seen["albumfish:" + id] = new Rectangle(x, y, w, h);
#endif
        // The whole entry opens the fish's card (as a row does in the Fish log), with its big picture and the real fish.
        if (Gfx.Hover(x, y, w, h)) Gfx.Rect(x - 4, y - 2, w + 8, h + 4, Pal.C("rgba(138,90,42,0.10)"), 6);
        if (Gfx.Click(x, y, w, h)) { dexFish = id; Sfx.Play("blip"); }
        if (got)
        {
            Gfx.Rect(fx, fy, fw, fh, Pal.C("#fffdf6"), 2);
            Gfx.Rect(fx + 5, fy + 5, fw - 10, fh - 10, Pal.C("#2a5a7a"), 2);
            var tex = FishArt.Picture(id, false);
            float sc = Math.Min((fw - 20) / FishArt.BigW, (fh - 16) / FishArt.BigH);
            DrawTexturePro(tex, new Rectangle(0, 0, FishArt.BigW, FishArt.BigH), Gfx.S(fx + fw / 2 - FishArt.BigW * sc / 2, fy + fh / 2 - FishArt.BigH * sc / 2, FishArt.BigW * sc, FishArt.BigH * sc), System.Numerics.Vector2.Zero, 0, Color.White);
            Gfx.Rect(fx + 10, fy - 5, 34, 12, AlbumTape, 2);
            Gfx.Rect(fx + fw - 44, fy - 5, 34, 12, AlbumTape, 2);
        }
        else
        {
            Gfx.Dashed(fx, fy, fw, fh, 2, 6, Pal.C("#c9b48f"));
            Gfx.TextCenter("?", fx + fw / 2, fy + fh / 2 - 22, FontKind.Ui700, 40, Pal.C("#c9b48f"));
        }
        float ty = fy + fh + 8;
        if (!got)
        {
            Gfx.Text("Not caught yet", x + 6, ty, FontKind.Note, 16, Muted);
            var near = Gfx.Wrap(AlbumData.TryGetValue(id, out var hint) ? $"Lives in {WaterList(hint.Water)} water." : "", FontKind.Note, 14, w - 12);
            Lines(near, x + 6, ty + 22, 18, FontKind.Note, 14, Muted);
            return ty + 22 + near.Count * 18;
        }
        // Names: the fish's own (local, with the English in brackets), then the scientific one. Nothing is cut short:
        // long ones take a second line (the autotest checks every entry still fits its slot).
        var nameLines = Gfx.Wrap(f.Name, FontKind.Ui700, 16, w - 12);
        Lines(nameLines, x + 6, ty, 19, FontKind.Ui700, 16, Pal.PaperInk);
        ty += nameLines.Count * 19 + 1;
        if (FishFacts.ById.TryGetValue(id, out var fact))
        {
            var sci = Gfx.Wrap(fact.Real ? fact.Sci : "Based on: " + fact.Sci, FontKind.Note, 14, w - 12);
            Lines(sci, x + 6, ty, 17, FontKind.Note, 14, Pal.C("#5a4a3a"));
            ty += sci.Count * 17 + 2;
        }
        if (AlbumData.TryGetValue(id, out var info))
        {
            // The family, the water as coloured tags, and what it eats.
            var fam = Gfx.Wrap((info.Family.Contains("order ") ? "Group: " : "Family: ") + info.Family, FontKind.Ui500, 13, w - 12);
            Lines(fam, x + 6, ty, 16, FontKind.Ui500, 13, Pal.PaperInk);
            ty += fam.Count * 16 + 3;
            float wx = x + 6;
            foreach (var kind in info.Water.Split(','))
            {
                string word = WaterWord(kind.Trim());
                float cw = Gfx.Measure(word, FontKind.Ui700, 11) + 10;
                Gfx.Rect(wx, ty, cw, 16, WaterColor(kind.Trim()), 3);
                Gfx.Text(word, wx + 5, ty + 2, FontKind.Ui700, 11, Pal.Paper);
                wx += cw + 4;
            }
            ty += 20;
            var eats = Gfx.Wrap("Eats: " + info.Diet, FontKind.Ui500, 13, w - 12);
            Lines(eats, x + 6, ty, 16, FontKind.Ui500, 13, Pal.PaperInk);
            ty += eats.Count * 16 + 2;
        }
        string rec = state.records.TryGetValue(id, out float kg) ? $"Caught {state.commons[id]} · biggest {Kg(kg)}" : $"Caught {state.commons[id]}";
        var recLines = Gfx.Wrap(rec, FontKind.Ui600, 13, w - 12);
        Lines(recLines, x + 6, ty + 2, 16, FontKind.Ui600, 13, Pal.C("#2a7d74"));
        ty += 2 + recLines.Count * 16;
        lastAlbumEntryH = Math.Max(lastAlbumEntryH, ty - y);
        if (ty - y > h) albumTooTall = id;
        return ty;
    }

    float lastAlbumEntryH;          // the tallest entry drawn (the autotest checks every one fits its slot)
    string albumTooTall;            // the last fish whose entry ran past its slot, or null

    static string WaterList(string water)
    {
        var parts = water.Split(',').Select(p => p.Trim()).ToList();
        return parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " or " + parts[^1];
    }
}
