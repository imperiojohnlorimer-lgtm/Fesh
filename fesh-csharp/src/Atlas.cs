using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The island guide (1.18): a page for every island and sea, for players who want to know what kind of place they're
// fishing in. What the land is like (its topography), the bodies of water and whether they're fresh, brackish or salt,
// the climate, what lives there, a cross-section drawn from a few heights, and a box on the real places and science
// behind it. Islands appear once they're charted (nothing about one you haven't found). Open it from the map (the
// Island guide button, or click an island's name) or from a Fish log page.
sealed record WaterBody(string Name, string Kind);   // Kind: "fresh", "brackish", "salt"
sealed record ProfilePoint(float X, float H, char S); // X 0..100 across, H metres (negative is under the sea), S the ground from here on
sealed record RegionInfo(string Id, string Name, string Kind, string Land, WaterBody[] Waters, string Climate, string Life, string Real,
    ProfilePoint[] Profile, (float x0, float x1, float level, bool ice)[] Lakes, (float x, string text)[] Labels, (float x, string mark)[] Marks,
    string Diagram = "profile", string Biome = null);

partial class Game
{
    static ProfilePoint P(float x, float h, char s) => new(x, h, s);
    static WaterBody Wb(string name, string kind) => new(name, kind);

    // Ground letters for the cross-sections: s sand, g grass, f woods, p palms, n snow with firs, i ice, r rock, k limestone,
    // m mangrove mud, l volcanic rock, c coral, b salt beds, q seagrass, j jungle, d dunes, u sea floor.
    public static readonly RegionInfo[] Regions =
    {
        new("saltmere", "Saltmere Island", "Temperate island",
            "Low and green: grassy slopes and oak woods rise a few metres above the sea, with berry bushes, sandy beaches, and a rocky shore along the north. The old wreck lies in the shallows to the southwest.",
            new[] { Wb("The lagoon: a freshwater pond in the middle of the island", "fresh"), Wb("Tide pools on the rocky north shore", "salt"), Wb("The shallows round the wreck", "salt"), Wb("Deep water off the end of the old dock", "salt") },
            "Temperate: mild, with sunny days, rain and the odd storm coming in off the sea.",
            "Oaks and wildflowers, berry bushes, gulls over the beach, chickens, and Biscuit the dog.",
            "Temperate islands, like many off the coasts of northern Europe, have mild summers and cool, wet winters. An island pond can stay fresh when rain keeps it topped up and the sea stays out; many coastal lagoons are brackish or salty instead.",
            new[] { P(0, -25, 'u'), P(12, -6, 'r'), P(18, 0, 'r'), P(22, 5, 'g'), P(28, 9, 'f'), P(40, 6, 'g'), P(47, 1.5f, 'g'), P(50, -2, 'u'), P(57, -3, 'u'), P(64, -1, 'g'), P(67, 2, 'g'), P(76, 4, 'f'), P(84, 1, 's'), P(88, 0, 's'), P(92, -3, 'u'), P(100, -20, 'u') },
            new[] { (47f, 66f, 1.5f, false) }, new[] { (18f, "Rocky shore"), (31f, "Oak woods"), (57f, "Freshwater lagoon"), (86f, "Beach"), (97f, "Deep water") }, new (float, string)[0]),
        new("frost", "Frostfang Isle", "Snowy island with a glacier",
            "Hills of snow and fir forest over hard rock. On the east side a glacier comes right down to the sea. The mouth of Frostfang Caverns opens in the north.",
            new[] { Wb("A lake in the middle, frozen over: you drill a hole to fish", "fresh"), Wb("Icy sea at the foot of the glacier", "salt") },
            "Polar: snow on the ground all year, and long, cold nights.",
            "Firs, which keep their needles through the winter, and sheep with thick wool.",
            "Glaciers are rivers of ice made from snow packed down over many years. Where one reaches the sea, chunks break off as icebergs. Ice floats, so many lakes keep liquid water under their lid of ice, where fish can live through the winter as long as there's enough oxygen.",
            new[] { P(0, -30, 'u'), P(8, -12, 'u'), P(12, 18, 'i'), P(22, 28, 'n'), P(34, 22, 'n'), P(44, 8, 'n'), P(47, 4, 'n'), P(50, -4, 'u'), P(58, -6, 'u'), P(64, 2, 'n'), P(66, 5, 'n'), P(74, 16, 'n'), P(84, 8, 'r'), P(91, 0, 'r'), P(95, -8, 'u'), P(100, -30, 'u') },
            new[] { (47f, 66f, 5f, true) }, new[] { (12f, "Glacier"), (25f, "Fir forest"), (57f, "Frozen lake"), (82f, "Rocky shore") }, new (float, string)[0]),
        new("dunes", "Sunscald Dunes", "Desert island",
            "Rolling sand dunes and flat, rocky ground with cacti. There's almost no soil, and almost no shade.",
            new[] { Wb("The oasis: a pool fed by water from under the ground", "fresh"), Wb("The mirage coast's warm shallows", "salt") },
            "Hot desert: hardly any rain, and scorching days.",
            "Cacti, which store water in their thick stems, and cats dozing in what shade there is.",
            "Deserts get very little rain, often less than about 250 mm a year. An oasis forms where water stored underground reaches the surface. Some desert fish live in springs that can be hotter than a hot bath and saltier than the sea. A mirage is light bent by the hot air over the sand, so the sky seems to shimmer on the ground like water.",
            new[] { P(0, -18, 'u'), P(10, -3, 'u'), P(16, 0, 's'), P(22, 6, 'd'), P(28, 12, 'd'), P(34, 5, 'd'), P(42, 8, 'd'), P(47, 3, 's'), P(51, -1, 'u'), P(57, -2, 'u'), P(61, 3, 's'), P(68, 13, 'd'), P(76, 7, 'r'), P(86, 4, 'r'), P(93, 0, 's'), P(100, -15, 'u') },
            new[] { (47f, 61f, 3f, false) }, new[] { (10f, "Mirage coast"), (28f, "Dunes"), (54f, "Oasis"), (80f, "Rocky flats") }, new (float, string)[0]),
        new("mire", "Mirewood", "Rainforest and swamp",
            "Low, flat and thick with trees, vines and ferns. On the coast the forest turns into a muddy swamp; out to sea are coral shallows.",
            new[] { Wb("The swamp: still, murky water full of fallen leaves", "fresh"), Wb("Coral shallows off the coast", "salt") },
            "Tropical rainforest: hot, steamy and wet all year.",
            "Tall trees and vines, fish that hop about on the mud, and pigs rooting about.",
            "Rainforests get a great deal of rain, though some have a drier season. Still swamp water holds little oxygen, so many swamp fish (catfish, lungfish, arowanas) can gulp air at the surface, and mudskippers breathe through their skin out on the mud.",
            new[] { P(0, -12, 'c'), P(9, -3, 'c'), P(14, 0, 'm'), P(18, 1, 'm'), P(22, -1, 'u'), P(30, -1, 'u'), P(34, 1.5f, 'j'), P(48, 8, 'j'), P(64, 12, 'j'), P(80, 6, 'j'), P(90, 1, 's'), P(95, -4, 'u'), P(100, -14, 'u') },
            new[] { (18f, 34f, 1.5f, false) }, new[] { (5f, "Coral shallows"), (26f, "Swamp"), (56f, "Rainforest"), (92f, "Beach") }, new (float, string)[0]),
        new("atoll", "Starfall Atoll", "Coral atoll",
            "A ring of low islands made of coral sand, barely a few metres above the sea, with coconut palms. Outside the ring, the reef drops away into deep ocean.",
            new[] { Wb("The lagoon inside the ring: warm, clear and shallow", "salt"), Wb("The drop-off outside the reef, falling into deep water", "salt") },
            "Tropical: warm all year, with steady winds off the ocean.",
            "Coconut palms, a parrot or two, and bright little reef fish in the lagoon.",
            "An atoll starts as a coral reef round a volcanic island. Over a very long time the volcano sinks and wears away while the coral keeps growing up toward the light, until only a ring of reef and sand is left round a lagoon. Charles Darwin worked this out in the 1830s; drilling in the Marshall Islands in the 1950s found the old volcanic rock deep under the coral.",
            new[] { P(0, -60, 'u'), P(12, -48, 'u'), P(17, -4, 'c'), P(20, 0, 'c'), P(23, 3, 'p'), P(29, 2, 'p'), P(33, -2, 'u'), P(45, -7, 'u'), P(60, -6, 'u'), P(70, -2, 'u'), P(73, 2, 'p'), P(79, 3, 'p'), P(82, 0, 'c'), P(85, -5, 'c'), P(90, -50, 'u'), P(100, -60, 'u') },
            new (float, float, float, bool)[0], new[] { (8f, "Drop-off"), (20f, "Reef"), (26f, "Sand islet"), (52f, "Lagoon"), (76f, "Sand islet") }, new (float, string)[0], Biome: "atoll"),
        new("caverns", "Frostfang Caverns", "Caves",
            "Twelve floors of caves under Frostfang Isle, linked by holes and ladders, with ore in the walls and crystal on the deepest floors.",
            new[] { Wb("Cave pools: still, cold and pitch dark", "fresh"), Wb("The Ancient Pool on the lowest floor", "fresh") },
            "No weather at all: cold, damp and dark, the same all year.",
            "Pale, eyeless things, and monsters that don't like visitors.",
            "Most caves form when rainwater, made slightly acidic by carbon dioxide from the air and soil, slowly dissolves limestone over thousands of years. With no light at all, cave animals often lose their eyes and their colour over many generations.",
            new ProfilePoint[0], new (float, float, float, bool)[0], new (float, string)[0], new (float, string)[0], Diagram: "cave", Biome: "frost"),
        new("opensea", "The open sea", "Open ocean",
            "No land at all: deep water between the islands, sailed by boat.",
            new[] { Wb("Deep ocean water, far from shore", "salt"), Wb("Feeding frenzies, where hunters drive small fish to the surface", "salt") },
            "Wind and waves with nothing to stop them; storms hit hardest out here.",
            "Big, fast hunting fish, and sea birds diving after the small fish the hunters chase up.",
            "Only the top 200 metres or so of the ocean (the sunlit zone) gets enough sunlight for plankton to grow, though faint light reaches about 1,000 m. Almost everything out here depends on that plankton. Birds diving on one spot often mean tunas or other hunters have trapped a school of small fish against the surface.",
            new ProfilePoint[0], new (float, float, float, bool)[0], new (float, string)[0], new (float, string)[0], Diagram: "ocean", Biome: "saltmere"),
        new("amihan:Amihan Village", "Amihan Village", "Low island with a fishing village",
            "Flat, sandy ground with coconut palms and bamboo, and a village of houses on stilts round a square. The landing is on the west side; School Rise, the one bit of high ground, is the village's evacuation area.",
            new[] { Wb("The village pond, where bangus are raised", "brackish"), Wb("The Amihan Sea all round", "salt") },
            "Tropical, with two monsoons: the cooler, drier amihan from the northeast, and the rainy habagat from the southwest.",
            "Coconut palms and bamboo, carabao, and the villagers: Lira, Niko and Ma'am Isay's class.",
            "In the Philippines bangus (milkfish) have been raised in brackish fishponds for centuries. Farmers grow lab-lab, a mat of algae, on the pond floor for the fish to graze. The amihan blows roughly from November to March and brings rain to eastern coasts; the habagat, roughly June to October, brings heavy rain especially to the west. Low coasts plan for tsunamis: PHIVOLCS's warning signs are Shake, Drop, Roar, and any one means go to high ground.",
            new[] { P(0, -20, 'u'), P(12, -3, 'u'), P(16, 0, 's'), P(20, 2, 'p'), P(28, 3, 'g'), P(40, 3, 'g'), P(44, 1.5f, 'g'), P(47, -1.5f, 'u'), P(54, -1.5f, 'u'), P(57, 2, 'g'), P(63, 4, 'g'), P(68, 11, 'g'), P(75, 12, 'g'), P(80, 5, 'p'), P(84, 1, 's'), P(88, 0, 's'), P(92, -4, 'u'), P(100, -20, 'u') },
            new[] { (44f, 57f, 1.5f, false) }, new[] { (14f, "Landing"), (32f, "Village"), (50f, "Bangus pond"), (72f, "School Rise"), (88f, "Beach") }, new[] { (8f, "pier"), (30f, "hut"), (36f, "hut"), (72f, "hut") }, Biome: "amihan"),
        new("amihan:Luntian Karsts", "Luntian Karsts", "Limestone karst island",
            "Steep grey towers of limestone, green on top, standing round a hidden lagoon. Maya's seaweed lines run across the lagoon's ends.",
            new[] { Wb("The karst lagoon, open to the sea and clear", "salt") },
            "Tropical, with the two monsoons.",
            "Rufous hornbills in the trees on the cliffs, and Maya's guso (seaweed) growing on lines in the lagoon.",
            "Karst forms when slightly acidic rainwater dissolves limestone over hundreds of thousands of years, leaving towers, sinkholes, caves and hidden lagoons. El Nido and Coron in Palawan are famous karst landscapes. Seaweed like guso is farmed on lines in shallow, clear water, then dried and sold for carrageenan, which thickens foods like ice cream.",
            new[] { P(0, -20, 'u'), P(8, -2, 'u'), P(12, 0, 's'), P(13, 2, 'k'), P(15, 36, 'k'), P(19, 44, 'k'), P(22, 34, 'k'), P(24, 2, 'k'), P(26, 0, 's'), P(28, -3, 'u'), P(40, -5, 'u'), P(55, -3, 'u'), P(60, 0, 's'), P(62, 2, 'k'), P(64, 28, 'k'), P(68, 33, 'k'), P(72, 26, 'k'), P(74, 3, 'j'), P(84, 4, 'j'), P(92, 0, 's'), P(100, -20, 'u') },
            new (float, float, float, bool)[0], new[] { (18f, "Karst tower"), (42f, "Karst lagoon"), (68f, "Karst tower"), (86f, "Forest") }, new[] { (33f, "post"), (37f, "post"), (49f, "post"), (53f, "post") }, Biome: "amihan"),
        new("amihan:Bakawan Island", "Bakawan Island", "Mangrove island",
            "Low and muddy, held together by mangrove roots, with a pool in the middle of the forest and firefly trees along the west shore.",
            new[] { Wb("The mangrove pool, where rain water meets the tide", "brackish"), Wb("Shallow mangrove shores, glowing with plankton on dark nights", "salt") },
            "Tropical, with the two monsoons.",
            "Mangroves, fireflies (alitaptap) in the pagatpat trees, tarsiers, and fish that hop about on the mud.",
            "Mangroves are trees that grow in salty tidal water. Their stilt roots and breathing roots hold the mud together, shield the coast from storm waves, and shelter young fish. Bakawan is the Filipino name for Rhizophora mangroves. The glowing water is plankton that flashes when the water moves.",
            new[] { P(0, -14, 'u'), P(10, -3, 'u'), P(16, 0, 'm'), P(22, 1, 'm'), P(30, 1, 'm'), P(34, -1, 'u'), P(46, -1, 'u'), P(49, 1, 'm'), P(55, 3, 'j'), P(66, 5, 'j'), P(74, 2, 'm'), P(82, 0, 'm'), P(88, -3, 'u'), P(100, -14, 'u') },
            new[] { (31f, 49f, 1f, false) }, new[] { (14f, "Mangrove roots"), (40f, "Mangrove pool"), (62f, "Forest"), (80f, "Firefly trees") }, new (float, string)[0], Biome: "amihan"),
        new("amihan:Baga Island", "Baga Island", "Volcanic island",
            "A cone of dark volcanic rock with a smoking crater, black rocky shores, and a jetty out to the reef.",
            new[] { Wb("The reef off the volcanic shore", "salt") },
            "Tropical, with the two monsoons, and warm ground near the crater.",
            "Hardy shrubs on the slopes, and reef fish round the rocks.",
            "Volcanic islands are built up by eruptions. Their rock breaks down into rich soil, and corals settle on the hard rock round the shore. The Philippines lies on the Pacific Ring of Fire and has more than twenty active volcanoes, such as Mayon and Taal. Baga means ember.",
            new[] { P(0, -30, 'u'), P(7, -6, 'c'), P(12, -1, 'c'), P(14, 0, 'l'), P(22, 12, 'l'), P(32, 40, 'l'), P(41, 78, 'l'), P(45, 92, 'l'), P(48, 84, 'l'), P(51, 92, 'l'), P(56, 78, 'l'), P(66, 40, 'l'), P(78, 12, 'l'), P(87, 2, 'l'), P(91, -2, 'u'), P(100, -30, 'u') },
            new (float, float, float, bool)[0], new[] { (6f, "Reef"), (30f, "Volcano"), (48f, "Crater"), (88f, "Black rock shore") }, new[] { (48f, "smoke"), (9f, "pier") }, Biome: "amihan"),
        new("sanctuary", "The marine sanctuary", "Marine protected area",
            "Open water and seagrass far from shore, marked by a ring of yellow buoys, with a sea warden's watch platform in the middle.",
            new[] { Wb("Seagrass meadows: food for dugong and turtles", "salt"), Wb("Open water inside the buoys: no fishing, no traps", "salt") },
            "Tropical, with the two monsoons.",
            "Sea turtles (pawikan), a dugong, a giant clam (taklobo), a banded sea krait and, by day, a whale shark.",
            "In a no-take marine sanctuary like this one, fishing and collecting are banned, so fish can grow bigger and more plentiful, and some move out into the water round it, which can help the fishers outside. Apo Island's sanctuary, guarded by local sea wardens (bantay dagat) since 1982, is a famous Philippine example. Sea turtles, dugongs and whale sharks are protected by law.",
            new[] { P(0, -24, 'u'), P(14, -7, 'q'), P(34, -5, 'q'), P(46, -9, 'u'), P(56, -4, 'q'), P(70, -6, 'q'), P(84, -12, 'u'), P(100, -26, 'u') },
            new (float, float, float, bool)[0], new[] { (8f, "Buoy"), (24f, "Seagrass"), (50f, "Watch platform"), (63f, "Seagrass") }, new[] { (8f, "buoy"), (92f, "buoy"), (50f, "platform") }, Biome: "amihan"),
        new("amihansea", "The Amihan Sea", "Open ocean",
            "Deep water round the Amihan islands, sailed by banca.",
            new[] { Wb("Deep, warm ocean between the islands", "salt") },
            "Tropical, with the two monsoons: the wind pushes your sail one way in the amihan and the other in the habagat.",
            "Big hunting fish, and the schools of small fish they chase.",
            "The Philippines has more than 7,600 islands, with warm, deep seas between them. It lies in the Coral Triangle, the part of the ocean with the most kinds of coral and reef fish in the world.",
            new ProfilePoint[0], new (float, float, float, bool)[0], new (float, string)[0], new (float, string)[0], Diagram: "ocean", Biome: "amihan"),
        new("habagat:Asinan", "Asinan", "Low island with salt beds",
            "Flat and sandy, with grassy rises, calamansi bushes, Manang Rosa's salt beds and drying racks. Reef flats run along the beaches.",
            new[] { Wb("The salt beds: sea water left to dry in the sun", "salt"), Wb("Reef flats, uncovered at low tide", "salt") },
            "Tropical; the salt is made in dry weather, as rain spoils a day's work.",
            "Calamansi bushes, goats, and gleaners on the flats at low tide.",
            "Salt is made by letting sea water evaporate in shallow beds in the sun: the water goes, the salt stays. Pangasinan, whose name means 'place of salt', is famous for it. Gleaning the reef flats at low tide (panginhas) is a way of life on many Philippine coasts.",
            new[] { P(0, -15, 'u'), P(7, -1.5f, 'u'), P(11, 0, 's'), P(15, 1, 's'), P(20, 1, 'b'), P(32, 1, 'b'), P(36, 2, 'g'), P(50, 4, 'g'), P(62, 3, 'g'), P(72, 2, 'g'), P(80, 1, 's'), P(86, 0, 's'), P(90, -1, 'u'), P(94, -4, 'u'), P(100, -15, 'u') },
            new (float, float, float, bool)[0], new[] { (8f, "Reef flat"), (26f, "Salt beds"), (56f, "Grass and calamansi"), (84f, "Beach") }, new[] { (22f, "salt"), (27f, "salt") }, Biome: "habagat"),
        new("habagat:islet:0", "Daang Pulo", "Limestone islets",
            "A small home islet and a scatter of limestone islets, each undercut by the waves into a mushroom shape and capped with scrub.",
            new[] { Wb("Reef and sandy channels between the islets", "salt"), Wb("Flats round the home islet at low tide", "salt") },
            "Tropical; the islets break the waves, so the channels are often calm.",
            "Reef fish in the channels, Dado and his paraw, and Lola Pacing at her sungka board.",
            "Waves, rocks rolling in the surf and burrowing animals wear a notch round a limestone islet at the waterline, so it grows into a mushroom shape. Pangasinan's Hundred Islands, about 124 of them depending on the tide, are islets like these. Daang Pulo means a hundred islands.",
            new[] { P(0, -15, 'u'), P(5, -4, 'u'), P(7, 5, 'k'), P(11, 6, 'k'), P(13, -4, 'u'), P(24, -6, 'u'), P(30, -2, 'u'), P(33, 0, 's'), P(36, 2, 'g'), P(46, 3, 'g'), P(52, 1, 's'), P(55, -2, 'u'), P(64, -6, 'u'), P(70, -3, 'u'), P(72, 7, 'k'), P(77, 7, 'k'), P(79, -4, 'u'), P(88, -7, 'u'), P(91, 6, 'k'), P(94, 6, 'k'), P(96, -5, 'u'), P(100, -14, 'u') },
            new (float, float, float, bool)[0], new[] { (9f, "Limestone islet"), (24f, "Channel"), (42f, "Home islet"), (75f, "Islet"), (92f, "Islet") }, new[] { (40f, "hut") }, Biome: "habagat"),
        new("habagat:Parola", "Parola", "Rocky headland with a lighthouse",
            "A rocky island rising to a grassy top, with the old lighthouse at the high point and a pier running out into deep water.",
            new[] { Wb("Deep water off the end of the pier", "salt") },
            "Tropical, and windy: the habagat blows straight in.",
            "Gulls on the rocks, and at night whatever a light out on the water draws in.",
            "A lighthouse shows ships where the coast is at night; its colour and its pattern of light help sailors tell which one they're looking at. Many Philippine lighthouses date from the late 1800s, like Cape Bojeador in Ilocos Norte. Fishers use lamps to draw squid up at night, just as the lighthouse does.",
            new[] { P(0, -26, 'u'), P(8, -12, 'u'), P(12, -2, 'r'), P(16, 8, 'r'), P(24, 14, 'r'), P(34, 16, 'g'), P(46, 15, 'g'), P(54, 10, 'r'), P(62, 4, 'r'), P(68, 1, 'r'), P(72, -8, 'u'), P(84, -16, 'u'), P(100, -30, 'u') },
            new (float, float, float, bool)[0], new[] { (14f, "Rocky shore"), (40f, "Lighthouse"), (72f, "Pier"), (90f, "Deep water") }, new[] { (40f, "lighthouse"), (70f, "pier") }, Biome: "habagat"),
        new("habagatsea", "The Habagat Sea", "Open ocean",
            "Deep water south of the big islands, round the Habagat islets.",
            new[] { Wb("Deep ocean, warm and open to the southwest wind", "salt") },
            "Tropical; the habagat brings rain and rough seas.",
            "Schools of small fish that come and go with the seasons, and the hunters that follow them.",
            "The habagat, the southwest monsoon, brings heavy rain to the western Philippines from about June to October. Some fish follow the seasons too: here, some schools come only in the amihan and others only in the habagat.",
            new ProfilePoint[0], new (float, float, float, bool)[0], new (float, string)[0], new (float, string)[0], Diagram: "ocean", Biome: "habagat")
    };

    public static readonly Dictionary<string, RegionInfo> RegionById = Regions.ToDictionary(r => r.Id);

    // Whether a page is open yet: an island once it's charted, a sea once you can sail it.
    bool AtlasOpen(RegionInfo r) => r.Id switch
    {
        "caverns" => Charted("frost") && state.caveDeepest > 0,
        "opensea" => Has("boat") > 0 || state.tamed,
        "amihansea" => state.Hinted("amihan"),
        "sanctuary" => state.Hinted("metJoy"),
        "habagatsea" => state.Hinted("habagat"),
        _ => Charted(r.Id)
    };

    // The fish found in a place: its fishing spots (the Starwell only once you've found it), then its crab pots.
    List<CommonFish> RegionFish(RegionInfo r)
    {
        var spots = r.Id switch
        {
            "caverns" => Data.Spots.Where(s => s.Scene == "cave"),
            "opensea" or "amihansea" or "habagatsea" => Data.Spots.Where(s => s.Id == r.Id),
            "sanctuary" => Enumerable.Empty<Spot>(),
            _ => Data.Spots.Where(s => s.Scene == "world" && SpotKnown(s) && SpotRegion(s) == r.Id)
        };
        var list = spots.SelectMany(s => Data.Common[s.Id]).ToList();
        // An island's crab pots too (not the sanctuary's: no traps there).
        if (r.Diagram == "profile" && r.Id != "sanctuary" && Data.PotCatch.TryGetValue(r.Biome ?? r.Id, out var pots)) list.AddRange(pots);
        return list.Distinct().ToList();
    }

    // Where you are, as an island guide page: underground, at sea, or the island under (or nearest) you.
    string HereRegion()
    {
        if (scene == "cave") return "caverns";
        if (scene != "world") return RegionById.TryGetValue(RegionOf(state.exitX, state.exitY), out var inside) ? inside.Id : "saltmere";
        if (InAmihan && InSanctuary(player.X, player.Y)) return "sanctuary";
        int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
        if (TileAt(tx, ty) == '~' || Aboard && !OnFlat) return InAmihan ? "amihansea" : InHabagat ? "habagatsea" : "opensea";
        string r = RegionAt(tx, ty);
        return RegionById.ContainsKey(r) ? r : r.StartsWith("habagat:islet") ? "habagat:islet:0" : "saltmere";
    }

    // The guide page for a Fish log page (a biome).
    static string RegionForBiome(string biome) => biome switch
    {
        "amihan" => "amihan:Amihan Village", "habagat" => "habagat:Asinan", _ => biome
    };

    string islandHover;         // the island name the mouse is over on the map, for the hint line

    /* ---------- The guide's panel ---------- */
    string atlasRegion = "saltmere";
    float lastAtlasBottom;      // where the page's text ended (the autotest checks it fits)
    bool atlasZoom;             // the page's drawing is open big (click it; Back or Esc closes it)

    void OpenAtlas(string id = null)
    {
        if (id != null && id.StartsWith("habagat:islet")) id = "habagat:islet:0";
        if (id != null && RegionById.ContainsKey(id)) atlasRegion = id;
        if (!AtlasOpen(RegionById[atlasRegion])) atlasRegion = Regions.FirstOrDefault(AtlasOpen)?.Id ?? "saltmere";
        atlasZoom = false;
        dexFish = null;
        Learned("atlas");
        Sfx.Play("ui");
        panel = "atlas";
        mode = "panel";
        SetPrompt("");
    }

    static Color WaterColor(string kind) => kind switch { "fresh" => Pal.C("#3fb5a5"), "brackish" => Pal.C("#7a9a5a"), _ => Pal.C("#2f7fa3") };
    static string WaterWord(string kind) => kind switch { "fresh" => "Fresh", "brackish" => "Brackish", _ => "Salt" };

    void DrawAtlas()
    {
        Backdrop();
        // A fish's card (click one under "Fish here") or the drawing made big, in place of the page.
        if (dexFish != null) { DrawFishCard(); return; }
        if (atlasZoom) { DrawAtlasZoom(); return; }
        const float w = 1200, h = 664, pad = 22;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Island guide", x + pad, y + pad - 4, FontKind.Ui700, 30, Pal.PaperInk);
        if (SmallButton("Close", x + w - pad - SmallW("Close"), y + pad - 6)) { ClosePanels(); return; }
        if (SmallButton("Fish album", x + w - pad - SmallW("Close") - 12 - SmallW("Fish album"), y + pad - 6)) { OpenAlbum(); return; }

        // The list of places down the left: the ones you've found by name, the rest as uncharted.
        float lx = x + pad, ly = y + pad + 44, lw = 220;
        string group = "";
        foreach (var r in Regions)
        {
            string g = r.Id is "saltmere" or "frost" or "dunes" or "mire" or "atoll" or "caverns" or "opensea" ? "The west" : r.Id.StartsWith("amihan") || r.Id == "sanctuary" ? "Amihan" : "Habagat";
            if (g != group) { group = g; Gfx.Text(g, lx, ly + 2, FontKind.Ui700, 14, Muted); ly += 20; }
            bool open = AtlasOpen(r), on = r.Id == atlasRegion;
            string label = open ? r.Name : "Uncharted";
#if DEBUG
            Gfx.Seen["atlas:" + r.Id] = new Rectangle(lx, ly, lw, 26);
#endif
            if (Button(label, lx, ly, lw, 26, FontKind.Ui600, 15, on ? Pal.Lantern : open ? Pal.Sand : Pal.C("#eadfc0"), open ? Pal.Ink : Muted, 2, 2, 4, open && !on))
            { atlasRegion = r.Id; atlasZoom = false; Sfx.Play("blip"); return; }
            ly += 29;
        }

        // The page.
        var reg = RegionById[atlasRegion];
        float px = lx + lw + 22, pw = x + w - pad - px, py = y + pad + 44;
        Gfx.Text(reg.Name, px, py, FontKind.Ui700, 26, Pal.PaperInk);
        float kw = Gfx.Measure(reg.Kind, FontKind.Ui700, 14) + 18;
        Gfx.Rect(px + Gfx.Measure(reg.Name, FontKind.Ui700, 26) + 14, py + 6, kw, 22, Pal.C("#3f6a8a"), 4);
        Gfx.Text(reg.Kind, px + Gfx.Measure(reg.Name, FontKind.Ui700, 26) + 23, py + 9, FontKind.Ui700, 14, Pal.Paper);
        py += 38;

        // The cross-section, with the water bodies listed beside it. Click it to see it big.
        float dw = 500, dh = 196;
        DrawRegionDiagram(reg, px, py, dw, dh);
#if DEBUG
        Gfx.Seen["atlas:diagram"] = new Rectangle(px, py, dw, dh);
#endif
        if (Gfx.Hover(px, py, dw, dh))
        {
            const string big = "Click to see it bigger";
            float bw = Gfx.Measure(big, FontKind.Ui700, 13) + 14;
            Gfx.Rect(px + dw - bw - 6, py + dh - 26, bw, 20, Pal.Rgba(16, 36, 58, 0.8f), 4);
            Gfx.Text(big, px + dw - bw + 1, py + dh - 23, FontKind.Ui700, 13, Pal.Paper);
        }
        if (Gfx.Click(px, py, dw, dh)) { atlasZoom = true; Sfx.Play("ui"); return; }
        Gfx.Text(DiagramCaption(reg) + "  ·  click it to see it bigger", px, py + dh + 4, FontKind.Ui500, 12, Muted);
        float wx = px + dw + 20, ww = x + w - pad - wx, wy = py;
        Gfx.Text("Water", wx, wy, FontKind.Ui700, 17, Pal.PaperInk);
        wy += 24;
        foreach (var b in reg.Waters)
        {
            string word = WaterWord(b.Kind);
            float cw = Gfx.Measure(word, FontKind.Ui700, 12) + 14;
            Gfx.Rect(wx, wy + 1, cw, 18, WaterColor(b.Kind), 4);
            Gfx.Text(word, wx + 7, wy + 3, FontKind.Ui700, 12, Pal.Paper);
            var bl = Gfx.Wrap(b.Name, FontKind.Ui500, 14, ww - cw - 8);
            Lines(bl, wx + cw + 8, wy + 1, 18, FontKind.Ui500, 14, Pal.PaperInk);
            wy += Math.Max(24, bl.Count * 18 + 6);
        }
        var key = Gfx.Wrap("Fresh water has almost no salt; the sea is salt; brackish water, where rivers or rain meet the tide, is in between.", FontKind.Note, 13, ww);
        Lines(key, wx, wy + 4, 17, FontKind.Note, 13, Muted);
        py += dh + 26;

        // The land, the climate and the life, two columns; then the real world.
        float colW = (pw - 20) / 2;
        float Section(string head, string text, float sx, float sy, float sw)
        {
            Gfx.Text(head, sx, sy, FontKind.Ui700, 16, Pal.PaperInk);
            var l = Gfx.Wrap(text, FontKind.Ui500, 14, sw);
            Lines(l, sx, sy + 21, 18, FontKind.Ui500, 14, Pal.PaperInk);
            return sy + 21 + l.Count * 18 + 8;
        }
        float a = Section("The land", reg.Land, px, py, colW);
        a = Section("Climate", reg.Climate, px, a, colW);
        // The right column starts under the water list if that runs long (the left one is clear of it).
        float b2 = Section("Life", reg.Life, px + colW + 20, Math.Max(py, wy + 4 + key.Count * 17 + 10), colW);
        // The fish here: caught ones in colour, the rest as question marks. Each opens its card, as in the Fish log.
        var fish = RegionFish(reg);
        string fishTip = null;
        if (fish.Count > 0)
        {
            int caught = fish.Count(f => state.commons.GetValueOrDefault(f.Id) > 0);
            Gfx.Text($"Fish here: {caught} of {fish.Count} caught", px + colW + 20, b2, FontKind.Ui700, 16, Pal.PaperInk);
            const string tipText = "click one for its card";
            Gfx.Text(tipText, px + pw - Gfx.Measure(tipText, FontKind.Ui500, 13), b2 + 3, FontKind.Ui500, 13, Muted);
            const float tile = 38;
            float fx = px + colW + 20, fy = b2 + 24;
            foreach (var f in fish)
            {
                if (fx + tile > px + pw) { fx = px + colW + 20; fy += tile + 4; }
                bool got = state.commons.GetValueOrDefault(f.Id) > 0, hover = Gfx.Hover(fx, fy, tile, tile);
                Gfx.Rect(fx, fy, tile, tile, got ? (hover ? Pal.C("#c4e2ee") : Pal.C("#dcecf2")) : hover ? Pal.C("#b9c9d0") : Pal.C("#c9d6dc"), 4);
                if (got) DrawIcon(f.Id, fx + 3, fy + 3, tile - 6);
                else Gfx.TextCenter("?", fx + tile / 2, fy + 8, FontKind.Ui700, 20, Pal.C("#8a9aa4"));
                if (hover) fishTip = got ? f.Name : Seen(f.Id) ? f.Name + " (seen)" : "Not caught yet";
#if DEBUG
                Gfx.Seen["atlasfish:" + f.Id] = new Rectangle(fx, fy, tile, tile);
#endif
                if (Gfx.Click(fx, fy, tile, tile)) { dexFish = f.Id; Sfx.Play("blip"); return; }
                fx += tile + 4;
            }
            b2 = fy + tile + 8;
        }
        // In the real world, under both columns.
        float ry = Math.Max(a, b2) + 2;
        var real = Gfx.Wrap(reg.Real, FontKind.Ui500, 14, pw - 24);
        float rh = 30 + real.Count * 18 + 8;
        Gfx.Box(px, ry, pw, rh, Pal.C("#eef6f2"), Pal.C("#8fb8a8"), 2, 5);
        Gfx.Text("In the real world", px + 12, ry + 7, FontKind.Ui700, 15, Pal.C("#2a7d74"));
        Lines(real, px + 12, ry + 28, 18, FontKind.Ui500, 14, Pal.PaperInk);
        lastAtlasBottom = Math.Max(Math.Max(ry + rh, ly), wy + key.Count * 17 + 4);
        // The pointed fish's name, beside the mouse, over everything.
        if (fishTip != null)
        {
            var m = Gfx.Mouse;
            float tw = Gfx.Measure(fishTip, FontKind.Ui600, 15) + 20, tx = Math.Clamp(m.X + 14, 4, Gfx.LW - tw - 4), ty = m.Y + 22 > Gfx.LH - 34 ? m.Y - 34 : m.Y + 22;
            Gfx.Rect(tx, ty, tw, 28, NavyStrong, 4);
            Gfx.Text(fishTip, tx + 10, ty + 5, FontKind.Ui600, 15, Pal.Paper);
        }
        if (Gfx.PressedOutside(x, y, w, h)) ClosePanels();
    }

    static string DiagramCaption(RegionInfo r) => r.Diagram == "cave" ? "Cut-away, not to scale" : r.Diagram == "ocean" ? "The ocean's layers, not to scale"
        : "Cross-section, not to scale: heights stretched to show the shape";

    // What each colour of ground in a cross-section is, for the key under the big drawing.
    static readonly (char s, string name)[] GroundNames =
    {
        ('s', "Sand"), ('d', "Dunes"), ('g', "Grass"), ('f', "Woods"), ('j', "Rainforest"), ('p', "Palms"), ('n', "Snow and firs"), ('i', "Glacier ice"),
        ('r', "Rock"), ('k', "Limestone"), ('m', "Mangrove mud"), ('l', "Volcanic rock"), ('c', "Coral"), ('b', "Salt beds"), ('q', "Seagrass"), ('u', "Sea floor")
    };

    // The page's drawing made big, with a key to its colours and the real-world note under it.
    void DrawAtlasZoom()
    {
        var reg = RegionById[atlasRegion];
        const float w = 1200, h = 664, pad = 22;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text(reg.Name, x + pad, y + pad - 4, FontKind.Ui700, 30, Pal.PaperInk);
        float kx = x + pad + Gfx.Measure(reg.Name, FontKind.Ui700, 30) + 14, kw = Gfx.Measure(reg.Kind, FontKind.Ui700, 14) + 18;
        Gfx.Rect(kx, y + pad + 2, kw, 22, Pal.C("#3f6a8a"), 4);
        Gfx.Text(reg.Kind, kx + 9, y + pad + 5, FontKind.Ui700, 14, Pal.Paper);
        if (SmallButton("Back", x + w - pad - SmallW("Back"), y + pad - 6)) { atlasZoom = false; return; }
        float dx = x + pad, dy = y + pad + 50, dw = w - 2 * pad, dh = 430;
        DrawRegionDiagram(reg, dx, dy, dw, dh, 1.6f);
        float ty = dy + dh + 10;
        Gfx.Text(DiagramCaption(reg), dx, ty, FontKind.Ui500, 15, Muted);
        ty += 26;
        // The key: only the grounds this drawing uses, and the lakes' water.
        if (reg.Diagram == "profile")
        {
            var used = reg.Profile.Select(p => p.H < 0 && p.S is not ('c' or 'q' or 'k' or 'r' or 'l') ? 'u' : p.S).Distinct().ToHashSet();
            float cx = dx;
            void Key(Color c, string name)
            {
                float nw = Gfx.Measure(name, FontKind.Ui600, 15);
                if (cx + 26 + nw > dx + dw) { cx = dx; ty += 26; }
                Gfx.Rect(cx, ty + 2, 18, 18, c, 3);
                Gfx.Text(name, cx + 24, ty + 2, FontKind.Ui600, 15, Pal.PaperInk);
                cx += 24 + nw + 22;
            }
            foreach (var (s, name) in GroundNames) if (used.Contains(s)) Key(GroundTop(s), name);
            if (reg.Lakes.Any(l => l.ice)) Key(LakeColor(reg, true), "Frozen lake");
            if (reg.Lakes.Any(l => !l.ice)) Key(LakeColor(reg, false), reg.Waters.Any(b => b.Kind == "brackish") && !reg.Waters.Any(b => b.Kind == "fresh") ? "Brackish pool" : "Fresh water");
            Key(Pal.C("#2f7fa3"), "Sea");
            ty += 30;
        }
        var real = Gfx.Wrap(reg.Real, FontKind.Ui500, 15, dw);
        Lines(real, dx, ty, 20, FontKind.Ui500, 15, Pal.PaperInk);
        lastAtlasZoomBottom = ty + real.Count * 20 - y;
        if (Gfx.PressedOutside(x, y, w, h)) atlasZoom = false;
    }

    float lastAtlasZoomBottom;  // where the big drawing's page ended (checked against the panel)

    /* ---------- The cross-sections ---------- */
    static Color GroundTop(char s) => Pal.C(s switch
    {
        's' or 'd' => "#e6cf96", 'g' or 'f' => "#5d9b45", 'j' or 'p' => "#3f7d3a", 'n' => "#f2f6f8", 'i' => "#cfe8f0", 'r' => "#8a8f93",
        'k' => "#c8c7ae", 'm' => "#5a4a32", 'l' => "#3f3a3a", 'c' => "#e8a090", 'b' => "#f4f2ec", 'q' => "#4f8a4a", _ => "#c9b07a"
    });
    static Color GroundBody(char s) => Pal.C(s switch
    {
        'r' or 'n' => "#8a8f93", 'i' => "#bfe0ec", 'k' => "#b8b7a0", 'l' => "#4a4242", 'm' => "#6b5a3e", 'c' => "#d8a08a", 's' or 'd' or 'b' or 'u' or 'q' or 'p' => "#d8bf88", _ => "#9a7a52"
    });

    // z scales the labels, trees and buildings, for the big version of the drawing (1 on the page).
    void DrawRegionDiagram(RegionInfo r, float x, float y, float w, float h, float z = 1)
    {
        Gfx.Rect(x - 2, y - 2, w + 4, h + 4, Pal.Ink, 4);
        // Kept inside its frame: a lighthouse on a high headland used to poke out of the top (Codex).
        var clip = Gfx.S(x, y, w, h);
        BeginScissorMode((int)clip.X, (int)clip.Y, (int)MathF.Ceiling(clip.Width), (int)MathF.Ceiling(clip.Height));
        try
        {
            if (r.Diagram == "ocean") DrawOceanDiagram(x, y, w, h, z);
            else if (r.Diagram == "cave") DrawCaveDiagram(x, y, w, h, z);
            else DrawProfileDiagram(r, x, y, w, h, z);
        }
        finally { EndScissorMode(); }
    }

    // The colour a lake or pond is drawn: brackish where the place's still water is brackish (Codex: they were all fresh).
    static Color LakeColor(RegionInfo r, bool ice) => ice ? Pal.C("#7fb8cc")
        : r.Waters.Any(b => b.Kind == "brackish") && !r.Waters.Any(b => b.Kind == "fresh") ? Pal.C("#6f9a78") : Pal.C("#3fb5a5");

    void DrawProfileDiagram(RegionInfo r, float x, float y, float w, float h, float z)
    {
        var pts = r.Profile;
        // Heights and depths on a square-root scale, so a 3 m sand islet and a 60 m drop-off both show on one drawing.
        float maxH = Math.Max(4, pts.Max(p => p.H)), maxD = Math.Max(4, -pts.Min(p => p.H));
        // Room at the top for the two rows of labels and whatever stands on the high ground (a lighthouse, trees), so
        // a label never sits on top of what it names.
        float above = r.Marks.Any(m => m.mark == "lighthouse") ? 40 : pts.Any(p => p.S is 'f' or 'j' or 'p' or 'n' or 'm' or 'd') ? 24 : 6;
        float top = (40 + above) * z;
        float k = (h - top - 6 * z) / (MathF.Sqrt(maxH) + MathF.Sqrt(maxD)), sea = y + top + MathF.Sqrt(maxH) * k;
        float Yh(float e) => sea - MathF.Sign(e) * MathF.Sqrt(MathF.Abs(e)) * k;
        // Sky, then the sea's surface.
        Gfx.Rect(x, y, w, sea - y, Pal.C("#cfe6f2"));
        Gfx.Rect(x, y, w, (sea - y) * 0.45f, Pal.C("#b9dcef"));
        Gfx.Rect(x, sea, w, y + h - sea, Pal.C("#2f7fa3"));
        // The ground, a column at a time.
        (float e, char s) At(float u)
        {
            for (int i = 0; i < pts.Length - 1; i++)
                if (u <= pts[i + 1].X)
                {
                    float k = (u - pts[i].X) / Math.Max(0.001f, pts[i + 1].X - pts[i].X);
                    return (pts[i].H + (pts[i + 1].H - pts[i].H) * k, pts[i].S);
                }
            return (pts[^1].H, pts[^1].S);
        }
        const float step = 2;
        for (float cx = 0; cx < w; cx += step)
        {
            float u = cx / w * 100;
            var (e, s) = At(u);
            float gy = Yh(e);
            // The sea gets darker with depth.
            if (e < 0)
            {
                float d = Math.Clamp(-e / maxD, 0, 1);
                Gfx.Rect(x + cx, sea, step, gy - sea, Pal.Rgba((int)(70 - 40 * d), (int)(150 - 70 * d), (int)(190 - 60 * d), 1));
            }
            // A lake or pond above the sea, fresh, brackish or frozen.
            foreach (var (x0, x1, level, ice) in r.Lakes)
                if (u >= x0 && u <= x1 && e < level)
                {
                    float ly = Yh(level);
                    Gfx.Rect(x + cx, ly, step, gy - ly, LakeColor(r, ice));
                    if (ice) Gfx.Rect(x + cx, ly, step, 3 * z, Pal.C("#f2fbff"));
                }
            char body = e < 0 && s is not ('c' or 'q' or 'k' or 'r' or 'l') ? 'u' : s;
            Gfx.Rect(x + cx, gy, step, y + h - gy, GroundBody(body));
            // Layers in the rock and soil, and the bedrock deep down.
            for (float sy = y + h - 10 * z; sy > gy + 8 * z; sy -= 14 * z) Gfx.Rect(x + cx, sy, step, 2, Pal.Rgba(60, 40, 20, 0.12f));
            Gfx.Rect(x + cx, gy, step, 4 * z, GroundTop(body));
            if (e < 0 && s == 'q' && (int)(cx / step) % 3 == 0) Gfx.Rect(x + cx, gy - 6 * z, 1.5f * z, 6 * z, Pal.C("#5fa84a"));
            if (e < 0 && s == 'c' && (int)(cx / step) % (int)(4 * z) == 0) Gfx.Circle(x + cx, gy - 2 * z, 3 * z, Pal.C("#f08a7a"));
        }
        // Trees, a few to each stretch of woods.
        for (float cx = 6 * z; cx < w - 6 * z; cx += 15 * z)
        {
            float u = cx / w * 100;
            var (e, s) = At(u);
            if (e < 0.5f) continue;
            float gy = Yh(e), tx = x + cx;
            switch (s)
            {
                case 'f': Gfx.Rect(tx - z, gy - 9 * z, 2 * z, 9 * z, Pal.C("#6b4a2b")); Gfx.Circle(tx, gy - 12 * z, 6 * z, Pal.C("#3f7d3a")); break;
                case 'j': Gfx.Rect(tx - z, gy - 14 * z, 2 * z, 14 * z, Pal.C("#5a4028")); Gfx.Circle(tx, gy - 16 * z, 7 * z, Pal.C("#2f6a2c")); Gfx.Circle(tx + 5 * z, gy - 13 * z, 5 * z, Pal.C("#3f7d3a")); break;
                case 'p':
                    Gfx.Line(tx, gy, tx + 3 * z, gy - 16 * z, 2 * z, Pal.C("#8a6440"));
                    Gfx.Triangle(tx + 3 * z, gy - 17 * z, tx - 6 * z, gy - 13 * z, tx + 2 * z, gy - 15 * z, Pal.C("#4f9a45")); Gfx.Triangle(tx + 3 * z, gy - 17 * z, tx + 12 * z, gy - 13 * z, tx + 4 * z, gy - 15 * z, Pal.C("#4f9a45"));
                    break;
                case 'n': Gfx.Triangle(tx, gy - 16 * z, tx - 6 * z, gy, tx + 6 * z, gy, Pal.C("#2f5a3a")); Gfx.Triangle(tx, gy - 16 * z, tx - 3 * z, gy - 9 * z, tx + 3 * z, gy - 9 * z, Pal.C("#f2f6f8")); break;
                case 'm': Gfx.Circle(tx, gy - 10 * z, 6 * z, Pal.C("#4f7a3a")); for (int i = -3; i <= 3; i += 3) Gfx.Line(tx, gy - 6 * z, tx + i * z, gy + z, z, Pal.C("#6b5a3a")); break;
                case 'd': if ((int)(cx / z) % 45 < 15) { Gfx.Rect(tx - z, gy - 9 * z, 3 * z, 9 * z, Pal.C("#5f9a4a")); Gfx.Rect(tx - 4 * z, gy - 6 * z, 2 * z, 4 * z, Pal.C("#5f9a4a")); } break;
            }
        }
        // Things people have built.
        foreach (var (mx, mark) in r.Marks)
        {
            float tx = x + mx / 100 * w;
            var (e, _) = At(mx);
            float gy = Yh(Math.Max(0, e));
            switch (mark)
            {
                case "hut": Gfx.Rect(tx - 6 * z, gy - 9 * z, 12 * z, 7 * z, Pal.C("#b98b52")); Gfx.Triangle(tx - 9 * z, gy - 8 * z, tx + 9 * z, gy - 8 * z, tx, gy - 16 * z, Pal.C("#ceb36b")); Gfx.Rect(tx - 5 * z, gy - 2 * z, z, 2 * z, Pal.C("#65452e")); Gfx.Rect(tx + 4 * z, gy - 2 * z, z, 2 * z, Pal.C("#65452e")); break;
                case "post": Gfx.Rect(tx, sea - 7 * z, 2 * z, 12 * z, Pal.C("#8a6440")); Gfx.Line(tx, sea - 2 * z, tx + 18 * z, sea - 2 * z, z, Pal.C("#dfe9ee")); Gfx.Circle(tx + 9 * z, sea + z, 2 * z, Pal.C("#7a9a3a")); break;
                case "buoy": Gfx.Circle(tx, sea - 2 * z, 4 * z, Pal.C("#f2c94a")); Gfx.Rect(tx - z, sea - 10 * z, 2 * z, 7 * z, Pal.C("#d8a83a")); break;
                case "platform": Gfx.Rect(tx - 10 * z, sea - 10 * z, 20 * z, 3 * z, Pal.C("#8a6440")); Gfx.Rect(tx - 9 * z, sea - 7 * z, 2 * z, 12 * z, Pal.C("#6b4a2b")); Gfx.Rect(tx + 7 * z, sea - 7 * z, 2 * z, 12 * z, Pal.C("#6b4a2b")); break;
                case "pier": Gfx.Rect(tx - 14 * z, sea - 6 * z, 28 * z, 3 * z, Pal.C("#8a6440")); Gfx.Rect(tx - 10 * z, sea - 3 * z, 2 * z, 8 * z, Pal.C("#6b4a2b")); Gfx.Rect(tx + 8 * z, sea - 3 * z, 2 * z, 8 * z, Pal.C("#6b4a2b")); break;
                case "lighthouse": Gfx.Rect(tx - 4 * z, gy - 30 * z, 8 * z, 30 * z, Pal.C("#f2ecd8")); Gfx.Rect(tx - 4 * z, gy - 22 * z, 8 * z, 4 * z, Pal.C("#c0392b")); Gfx.Rect(tx - 5 * z, gy - 34 * z, 10 * z, 4 * z, Pal.C("#f2c94a")); Gfx.Triangle(tx - 5 * z, gy - 34 * z, tx + 5 * z, gy - 34 * z, tx, gy - 39 * z, Pal.C("#3f4a5a")); break;
                case "salt": Gfx.Rect(tx - 9 * z, gy - 2 * z, 18 * z, 3 * z, Pal.C("#f8f6f0")); Gfx.Rect(tx - 9 * z, gy - 3 * z, z, 4 * z, Pal.C("#8a6440")); Gfx.Rect(tx + 8 * z, gy - 3 * z, z, 4 * z, Pal.C("#8a6440")); break;
                case "smoke": for (int i = 0; i < 4; i++) Gfx.Circle(tx + i * 3 * z, gy - (8 + i * 7) * z, (4 + i) * z, Pal.Rgba(180, 180, 180, 0.6f - i * 0.12f)); break;
            }
        }
        // The labels, alternating height so neighbours don't collide, each with a line down to its place.
        int n = 0;
        float fs = 12 * z;
        foreach (var (lx, text) in r.Labels)
        {
            float tx = x + lx / 100 * w;
            var (e, _) = At(lx);
            float gy = e < 0 ? Yh(e) : Yh(e) - 2, ty = y + 6 * z + (n++ % 2) * 18 * z;
            float tw = Gfx.Measure(text, FontKind.Ui700, fs) + 8 * z;
            float bx = Math.Clamp(tx - tw / 2, x + 2, x + w - tw - 2);
            Gfx.Line(tx, ty + 14 * z, tx, gy, z, Pal.Rgba(16, 36, 58, 0.45f));
            Gfx.Rect(bx, ty, tw, 15 * z, Pal.Rgba(255, 250, 240, 0.9f), 3);
            Gfx.Text(text, bx + 4 * z, ty + z, FontKind.Ui700, fs, Pal.PaperInk);
        }
    }

    // The open ocean: the sunlit zone where plankton grows, the twilight below, the midnight dark, and who lives where.
    void DrawOceanDiagram(float x, float y, float w, float h, float z = 1)
    {
        var bands = new (float frac, string color, string name, string depth)[]
        {
            (0.28f, "#3f9ac0", "Sunlit zone", "0 to 200 m: light, plankton, tunas and flying fish"),
            (0.30f, "#245a82", "Twilight zone", "200 to 1,000 m: faint light; many lanternfish rise from here at night"),
            (0.42f, "#10243a", "Midnight zone and deeper", "below 1,000 m: no sunlight at all")
        };
        float sky = 14 * z;
        Gfx.Rect(x, y, w, sky, Pal.C("#cfe6f2"));
        float by = y + sky;
        // The words keep to the left; the animals that live in each zone are drawn on the right.
        float textW = w - 140 * z;
        foreach (var (frac, color, name, depth) in bands)
        {
            float bh = (h - sky) * frac;
            Gfx.Rect(x, by, w, bh, Pal.C(color));
            Gfx.Text(name, x + 10 * z, by + 6 * z, FontKind.Ui700, 14 * z, Pal.Paper);
            var dl = Gfx.Wrap(depth, FontKind.Ui500, 12 * z, textW);
            Lines(dl, x + 10 * z, by + 24 * z, 15 * z, FontKind.Ui500, 12 * z, Pal.Rgba(240, 248, 252, 0.85f));
            by += bh;
        }
        // A frenzy at the surface: birds above, small fish, and a hunter.
        float fx = x + w - 120 * z;
        for (int i = 0; i < 9; i++) Gfx.Circle(fx + (i * 13) % 60 * z, y + (24 + (i * 7) % 18) * z, 2.2f * z, Pal.C("#e8f0f4"));
        Gfx.Triangle(fx + 70 * z, y + 30 * z, fx + 92 * z, y + 25 * z, fx + 92 * z, y + 35 * z, Pal.C("#1d3550"));
        Gfx.Circle(fx + 66 * z, y + 30 * z, 6 * z, Pal.C("#1d3550"));
        for (int i = 0; i < 3; i++) { float bx = fx + (10 + i * 20) * z; Gfx.Line(bx - 5 * z, y + 6 * z, bx, y + 9 * z, 1.5f * z, Pal.Ink); Gfx.Line(bx, y + 9 * z, bx + 5 * z, y + 6 * z, 1.5f * z, Pal.Ink); }
        Gfx.Text("feeding frenzy", fx - 4 * z, y + 44 * z, FontKind.Ui700, 11 * z, Pal.Paper);
        // Lanternfish dots, low in the twilight, and a giant squid in the dark.
        // Lanternfish in the middle of the twilight band, their label under them, still inside it (Codex: it spilled into the dark).
        float twTop = y + sky + (h - sky) * 0.28f, twH = (h - sky) * 0.30f, twy = twTop + twH * 0.35f;
        for (int i = 0; i < 7; i++) Gfx.Circle(x + w - (90 - i * 11) * z, twy + (i % 2) * 5 * z, 1.8f * z, Pal.C("#9fe8ff"));
        Gfx.Text("lanternfish", x + w - 96 * z, MathF.Min(twy + 9 * z, twTop + twH - 14 * z), FontKind.Ui700, 11 * z, Pal.C("#bff4ff"));
        float sqx = x + w - 150 * z, sqy = y + h - 34 * z;
        Gfx.Circle(sqx, sqy, 6 * z, Pal.C("#8a4a5a")); Gfx.Rect(sqx - 14 * z, sqy - 3 * z, 12 * z, 6 * z, Pal.C("#8a4a5a"));
        for (int i = 0; i < 4; i++) Gfx.Line(sqx + 4 * z, sqy + (-3 + i * 2) * z, sqx + 22 * z, sqy + (-6 + i * 4) * z, 1.5f * z, Pal.C("#8a4a5a"));
        Gfx.Text("giant squid", sqx - 10 * z, sqy + 9 * z, FontKind.Ui700, 11 * z, Pal.C("#d8a0b0"));
    }

    // Under Frostfang: the snowy surface, then floors of cave stepping down, joined by ladders, with pools.
    void DrawCaveDiagram(float x, float y, float w, float h, float z = 1)
    {
        Gfx.Rect(x, y, w, h, Pal.C("#3a3430"));
        Gfx.Rect(x, y, w, 22 * z, Pal.C("#cfe6f2"));
        Gfx.Rect(x, y + 22 * z, w, 6 * z, Pal.C("#f2f6f8"));
        Gfx.Triangle(x + 60 * z, y + 22 * z, x + 50 * z, y + 8 * z, x + 70 * z, y + 8 * z, Pal.C("#2f5a3a"));
        var floors = new (float fx, float fy, string label, bool pool)[] { (0.08f, 0.24f, "Floor 1", true), (0.28f, 0.42f, "Floor 5", false), (0.48f, 0.6f, "Floor 10", true), (0.68f, 0.78f, "Ancient Floor", true) };
        float px = x + w * 0.12f, py = y + 28 * z;
        foreach (var (fx, fy, label, pool) in floors)
        {
            float cx = x + w * fx, cy = y + h * fy, cw = w * 0.26f, ch = 26 * z;
            Gfx.Line(px, py, cx + 14 * z, cy + 4 * z, 2 * z, Pal.C("#c9a46a"));
            Gfx.Rect(cx, cy, cw, ch, Pal.C("#151210"), 8 * z);
            if (pool) Gfx.Rect(cx + cw * 0.45f, cy + ch - 8 * z, cw * 0.4f, 7 * z, Pal.C("#2a6a8a"), 3 * z);
            Gfx.Circle(cx + 10 * z, cy + 6 * z, 2 * z, Pal.C("#7fe0ff"));
            // The label sits inside the floor's own room, clear of the room above it (Codex: they overlapped).
            Gfx.Text(label, cx + 18 * z, cy + 4 * z, FontKind.Ui700, 12 * z, Pal.C("#f2e6c8"));
            px = cx + cw * 0.8f; py = cy + ch;
        }
    }
}
