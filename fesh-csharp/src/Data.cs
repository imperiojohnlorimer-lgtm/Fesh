namespace Fesh;

sealed record Creature(string Id, string Name, string Spot, string Time, string Req, string Rarity, float Difficulty,
    string Desc, string Hint, string TimeHint = null, string LockedHint = null);

sealed record Clue(string Title, string Text, string Finding);

// Weight is the relative chance at its spot. Time ("day", "night" or "any") limits when it bites.
// Kg is a typical weight; each catch rolls its own size around it.
// Style is how it fights: "dart" (darts about), "runner" (makes runs that snap a tight line), "jumper" (leaps you time a
// press for) or "bottom" (sulks deep; pump it up with short taps). Depth ("shallow", "any", "deep") is where it feeds:
// a short cast lands in the shallows, a long one in deep water. Weather ("rain", "storm", "clear") and FullMoon limit
// when it bites. Legend fish bite only once, and only on their Bait (if they name one).
sealed record CommonFish(string Id, string Name, float Difficulty, int Weight = 4, bool Rare = false, string Time = "any",
    float Kg = 1, string Style = "dart", string Depth = "any", string Weather = null, bool FullMoon = false, bool Legend = false, string Bait = null);

// A set of four fish. While all four are on show in your aquariums, its Perk applies.
sealed record AquaSet(string Id, string Name, string[] Fish, string Perk);

// The legendary card: which portrait to draw, what it is, and how it was caught.
sealed record LegendInfo(string Art, string Desc, string Where, string Hint);

// Scene is where the spot is: "world" or "cave". Coordinates are in that scene.
sealed record Spot(string Id, string Label, string Action, float X, float Y, float R, string Biome = "saltmere", string Scene = "world");

sealed record Biome(string Id, string Name, string Climate, string Enter);

// Land animals that sometimes end up on the hook at one particular spot.
sealed record OddCatch(string Id, string Name, string Spot, float Difficulty, string Title, string Desc, string After);

// Box: solid footprint [x, y, w, h] in pixels from the tile corner. Light: [x, y, radius]. Rest: where to stand to rest.
// Indoor pieces go inside your own shack. Station is the crafting station it acts as; Door means it can be entered.
// Water pieces (crab pots) are set in shallow water instead of on land.
sealed record BuildDef(string Id, string Name, Dictionary<string, int> Cost, string Desc, int W = 1,
    int[] Box = null, int[] Light = null, int[] Rest = null, string Tip = null, bool Indoor = false, string Station = null, bool Door = false, bool Water = false)
{
    public string CostText => string.Join(", ", Cost.Select(kv => $"{kv.Value} {Items.ById[kv.Key].Name.ToLowerInvariant()}"));
}

static class Data
{
    public static readonly Creature[] Creatures =
    {
        new("glowgill", "Glowgill", "lagoon", "night", null, "Uncommon", 1.8f,
            "A lagoon fish whose gills pulse with soft blue light. It only rises after dark.",
            "Something glows in the lagoon, but only after dark.",
            TimeHint: "For a second, something glowed under the lagoon. Maybe it only comes up at night."),
        new("tidecrawler", "Tidecrawler", "rocks", "day", "glowgill", "Uncommon", 2.2f,
            "Half crab, half fish. It scuttles between tide pools and only swims when it has to.",
            "Clicking echoes from the rocky shore up north, in daylight.",
            TimeHint: "The rocks are quiet at night. Whatever clicks here must come out in daylight.",
            LockedHint: "Something clicked between the rocks and hid. Tomas said to start with the lagoon."),
        new("hollow_eel", "Hollow Eel", "wreck", "any", "tidecrawler", "Rare", 2.8f,
            "Its body is full of hollow chambers. It whistles faintly when it breathes.",
            "Lives in the old wreck off the southwest shore. The tide blocks the way."),
        new("mirror_ray", "Mirror Ray", "deep", "day", "hollow_eel", "Rare", 3.2f,
            "Its skin reflects everything around it, so it almost disappears in open water.",
            "Glides through the deep water past the old dock. The dock is broken.",
            TimeHint: "Something wide and silver flashed past, too quick to see in the dark. Better try in daylight."),
        new("abyssal", "The Abyssal", "deep", "night", "mirror_ray", "Legendary", 4f,
            "A gentle giant from the trench beneath Saltmere. Dozens of eyes, every one of them calm.",
            "Only island legends mention it. Gather every other clue first.",
            TimeHint: "Something enormous moved far below, then stayed down. Maybe it waits for night.")
    };
    public static readonly Dictionary<string, Creature> ById = Creatures.ToDictionary(c => c.Id);

    public static readonly Dictionary<string, Clue> Clues = new()
    {
        ["glowgill"] = new("Specimen tag",
            "A metal tag clipped to the Glowgill's fin: \"Specimen 03. Property of R/V Halcyon, Deepwell Marine Co.\"",
            "The creatures were cargo on a research ship called the Halcyon."),
        ["tidecrawler"] = new("Torn logbook page",
            "Wedged in the Tidecrawler's shell: \"Day 41. Deepwell wants the specimens sold to collectors. They don't belong in tanks, and I won't let it happen. M.I.\"",
            "Someone aboard, M.I., refused to let the creatures be sold."),
        ["hollow_eel"] = new("Key card",
            "Tangled in the Hollow Eel's coils: a waterlogged key card. \"Dr. Mara Ilao. Chief Biologist, R/V Halcyon.\"",
            "M.I. is Dr. Mara Ilao, the ship's chief biologist, and Tomas's daughter."),
        ["mirror_ray"] = new("Waterproof recorder",
            "Stuck to the Mirror Ray's underside. It crackles: \"If anyone finds this, I opened the hold valves myself. The Halcyon will sink, but they'll be free. Deepwell took them from the trench under Saltmere. I'm bringing them home.\"",
            "Mara sank the Halcyon on purpose to return the creatures to their trench."),
        ["abyssal"] = new("Silver locket",
            "Looped around one of the Abyssal's fins. Inside is a photo of a young woman and Tomas, standing on Saltmere's old dock.",
            "The Abyssal kept Mara's locket safe until her father could see it again.")
    };

    public static readonly string[] Theories =
    {
        "No leads yet. Strange creatures, a sunken ship and a quiet island. Go fishing.",
        "The creatures were cargo aboard a research ship, the R/V Halcyon.",
        "Someone on the Halcyon, \"M.I.\", was protecting the creatures from being sold.",
        "M.I. is Dr. Mara Ilao, the Halcyon's chief biologist and Tomas's daughter.",
        "Mara sank the Halcyon on purpose so the creatures could return to the trench beneath Saltmere.",
        "Case closed. Deepwell took the creatures from the trench, Mara set them free, and the Abyssal kept her locket."
    };

    public static readonly Biome[] Biomes =
    {
        new("saltmere", "Saltmere Island", "Temperate", "Back on Saltmere Island."),
        new("frost", "Frostfang Isle", "Snow", "Frostfang Isle. Snow crunches underfoot."),
        new("dunes", "Sunscald Dunes", "Desert", "Sunscald Dunes. The air shimmers with heat."),
        new("mire", "Mirewood", "Jungle", "Mirewood. Everything here is green and dripping."),
        new("atoll", "Starfall Atoll", "Tropical", "Starfall Atoll. Warm water, white sand, and something big out past the reef.")
    };

    // Tidemane, the hippocamp of the Starwell: fished up and fought on the atoll, then ridden as a mount.
    public const string MountName = "Tidemane";
    public static readonly LegendInfo Tidemane = new("tidemane",
        "Half horse, half fish, and all stubbornness. A mane of sea foam, coral horns, starlight speckled down its flanks, and a tail "
        + "that could flip a rowing boat. It gallops on sand and swims the open sea faster than any sail.",
        "Hooked in the Starwell at night on a coconut, then worn down on the sand until it chose you.",
        "Hoofprints on Starfall Atoll lead into the palms and stop at the water.");

    public static readonly Dictionary<string, CommonFish[]> Common = new()
    {
        ["lagoon"] = new CommonFish[]
        {
            new("pond_perch", "Pond perch", 1f, Kg: 0.4f, Depth: "shallow"),
            new("mud_carp", "Mud carp", 1.1f, Kg: 2.2f, Style: "bottom"),
            new("moon_carp", "Moon carp", 2.5f, 1, true, "night", Kg: 3.5f, Style: "jumper", FullMoon: true),
            new("old_whiskers", "Old Whiskers", 4.2f, 1, true, Kg: 24f, Style: "bottom", Depth: "deep", Weather: "rain", Legend: true, Bait: "berries")
        },
        ["rocks"] = new CommonFish[] { new("rock_goby", "Rock goby", 1f, Kg: 0.3f, Style: "bottom", Depth: "shallow"), new("striped_wrasse", "Striped wrasse", 1.2f, Kg: 0.8f) },
        ["wreck"] = new CommonFish[]
        {
            new("rusty_grouper", "Rusty grouper", 1.3f, Kg: 6f, Style: "bottom", Depth: "deep"),
            new("barnacle_bass", "Barnacle bass", 1.2f, Kg: 2.5f, Style: "bottom"),
            new("storm_eel", "Storm eel", 2.8f, 1, true, Kg: 4f, Style: "runner", Weather: "storm")
        },
        ["deep"] = new CommonFish[] { new("silver_tuna", "Silver tuna", 1.5f, Kg: 9f, Style: "runner", Depth: "deep"), new("lantern_squid", "Lantern squid", 1.4f, Kg: 2f, Depth: "deep") },
        ["icehole"] = new CommonFish[]
        {
            new("arctic_char", "Arctic char", 1.2f, Kg: 1.6f), new("frost_smelt", "Frost smelt", 1f, Kg: 0.15f, Depth: "shallow"),
            new("crystal_pike", "Crystal pike", 2.6f, 1, true, "night", Kg: 4f, Style: "runner")
        },
        ["glacier"] = new CommonFish[]
        {
            new("polar_cod", "Polar cod", 1.3f, Kg: 3f, Style: "bottom", Depth: "deep"), new("snow_crab", "Snow crab", 1.4f, Kg: 1.2f, Style: "bottom", Depth: "shallow"),
            new("aurora_trout", "Aurora trout", 2.6f, 1, true, "night", Kg: 2.5f, Style: "jumper", FullMoon: true)
        },
        ["oasis"] = new CommonFish[]
        {
            new("oasis_tilapia", "Oasis tilapia", 1.1f, Kg: 1f, Depth: "shallow"), new("desert_pupfish", "Desert pupfish", 1f, Kg: 0.05f, Depth: "shallow"),
            new("mirage_koi", "Mirage koi", 2.6f, 1, true, "day", Kg: 3f, Style: "jumper"),
            new("sunscale_lungfish", "Sunscale lungfish", 4.3f, 1, true, "day", Kg: 30f, Style: "runner", Depth: "deep", Weather: "clear", Legend: true, Bait: "cricket")
        },
        ["mirage"] = new CommonFish[]
        {
            new("sun_mackerel", "Sun mackerel", 1.4f, Kg: 1.5f, Style: "runner"), new("sand_ray", "Sand ray", 1.6f, Kg: 5f, Style: "bottom", Depth: "deep"),
            new("thunderfin", "Thunderfin", 2.9f, 1, true, Kg: 6f, Style: "runner", Weather: "storm")
        },
        ["swamp"] = new CommonFish[]
        {
            new("mudskipper", "Mudskipper", 1.1f, Kg: 0.2f, Depth: "shallow"), new("swamp_catfish", "Swamp catfish", 1.3f, Kg: 7f, Style: "bottom", Depth: "deep"),
            new("emerald_arowana", "Emerald arowana", 2.8f, 1, true, Kg: 4f, Style: "jumper"),
            new("mire_leviathan", "Mire leviathan", 4.4f, 1, true, "night", Kg: 90f, Style: "bottom", Depth: "deep", Legend: true, Bait: "worm")
        },
        ["coral"] = new CommonFish[] { new("parrotfish", "Parrotfish", 1.3f, Kg: 2f), new("jungle_piranha", "Jungle piranha", 1.6f, Kg: 0.8f, Style: "runner") },
        ["cavepool"] = new CommonFish[]
        {
            new("blind_cavefish", "Blind cavefish", 1.4f, Kg: 0.5f), new("glow_shrimp", "Glow shrimp", 1.2f, Kg: 0.05f, Depth: "shallow"),
            new("ghost_eel", "Ghost eel", 2.9f, 1, true, Kg: 2f, Style: "runner")
        },
        ["ancientpool"] = new CommonFish[]
        {
            new("abyssal_lanternfish", "Abyssal lanternfish", 2.2f, Kg: 1f, Depth: "deep"), new("pale_cave_shark", "Pale cave shark", 2.8f, Kg: 30f, Style: "runner", Depth: "deep"),
            new("ancient_coelacanth", "Ancient coelacanth", 4.6f, 1, true, Kg: 80f, Style: "bottom", Depth: "deep", Legend: true)
        },
        ["atolllagoon"] = new CommonFish[]
        {
            new("clownfish", "Clownfish", 1.1f, Kg: 0.25f, Depth: "shallow"), new("moorish_idol", "Moorish idol", 1.2f, Kg: 0.6f),
            new("pearl_angelfish", "Pearl angelfish", 2.7f, 1, true, Kg: 1.2f), new("silver_moonfish", "Silver moonfish", 2.6f, 1, true, "night", Kg: 2f, Style: "jumper", FullMoon: true)
        },
        ["dropoff"] = new CommonFish[]
        {
            new("bluefin_tuna", "Bluefin tuna", 2f, Kg: 60f, Style: "runner", Depth: "deep"), new("sailfish", "Sailfish", 2.4f, Kg: 45f, Style: "jumper"),
            new("golden_marlin", "Golden marlin", 3.4f, 1, true, "day", Kg: 120f, Style: "jumper", Depth: "deep"),
            new("giant_squid", "Giant squid", 3.3f, 1, true, "night", Kg: 150f, Style: "bottom", Depth: "deep"),
            new("starfall_ray", "Starfall ray", 4.5f, 1, true, "night", Kg: 200f, Style: "runner", Depth: "deep", FullMoon: true, Legend: true, Bait: "glow_shrimp")
        },
        ["starwell"] = new CommonFish[]
        {
            new("seafoam_goby", "Seafoam goby", 1.2f, Kg: 0.3f, Style: "bottom", Depth: "shallow"),
            new("blue_hole_grouper", "Blue hole grouper", 1.9f, Kg: 14f, Style: "bottom", Depth: "deep"),
            new("moonglass_fish", "Moonglass fish", 2.8f, 1, true, "night", Kg: 1.5f, Style: "jumper")
        }
    };

    // What turns up in a crab pot left in shallow water overnight, by island (plus seaweed, and now and then a boot or a pearl).
    public static readonly Dictionary<string, CommonFish[]> PotCatch = new()
    {
        ["saltmere"] = new CommonFish[] { new("shore_crab", "Shore crab", 1f, Kg: 0.4f) },
        ["frost"] = new CommonFish[] { new("king_crab", "King crab", 1f, Kg: 3f) },
        ["dunes"] = new CommonFish[] { new("ghost_crab", "Ghost crab", 1f, Kg: 0.3f) },
        ["mire"] = new CommonFish[] { new("crayfish", "Crayfish", 1f, Kg: 0.15f) },
        ["atoll"] = new CommonFish[] { new("spiny_lobster", "Spiny lobster", 1f, Kg: 2.5f) }
    };

    public static readonly CommonFish[] AllCommon = Common.Values.SelectMany(v => v).Concat(PotCatch.Values.SelectMany(v => v)).ToArray();
    public static readonly Dictionary<string, CommonFish> FishById = AllCommon.ToDictionary(f => f.Id);
    // The spot each rod-caught fish lives at.
    public static readonly Dictionary<string, string> SpotOfFish = Common.SelectMany(kv => kv.Value.Select(f => (f.Id, kv.Key))).ToDictionary(p => p.Id, p => p.Key);

    // Shallow reef water you can spearfish in.
    public static readonly string[] ReefSpots = { "coral", "atolllagoon" };

    public static readonly AquaSet[] AquaSets =
    {
        new("saltmere", "Saltmere shore", new[] { "pond_perch", "mud_carp", "rock_goby", "striped_wrasse" }, "Fish bite 15% faster on Saltmere"),
        new("frost", "Frozen north", new[] { "arctic_char", "frost_smelt", "polar_cod", "snow_crab" }, "Rare luck +1 on Frostfang and in the caverns"),
        new("dunes", "Desert springs", new[] { "oasis_tilapia", "desert_pupfish", "sun_mackerel", "sand_ray" }, "Pip pays 10% more for fish"),
        new("mire", "Jungle waters", new[] { "mudskipper", "swamp_catfish", "parrotfish", "jungle_piranha" }, "15% more fishing XP"),
        new("atoll", "Coral reef", new[] { "clownfish", "moorish_idol", "pearl_angelfish", "spiny_lobster" }, "Fish are 10% heavier"),
        new("cave", "Deep dark", new[] { "blind_cavefish", "glow_shrimp", "ghost_eel", "abyssal_lanternfish" }, "Reel bar +3")
    };

    public static readonly Dictionary<string, LegendInfo> Legends = new()
    {
        ["old_whiskers"] = new("whiskers",
            "The lagoon's oldest resident: a carp as long as a rowing boat, with whiskers like an old sea captain. Tomas's grandfather swore it once stole his lunch.",
            "Hooked in the lagoon in the rain, on a berry.", "The lagoon. Something huge stirs there when it rains, and it has a sweet tooth."),
        ["ancient_coelacanth"] = new("coelacanth",
            "A living fossil, older than the islands themselves. Its fins move like little legs, and its blue scales are flecked with silver like stars. Everyone said it died out millions of years ago, yet here it is, staring back at you.",
            "Hooked in the Ancient pool, floor 12 of Frostfang Caverns.", "Somewhere at the very bottom of Frostfang Caverns."),
        ["sunscale_lungfish"] = new("lungfish",
            "It breathes air, and it can sleep in the mud for years when the oasis dries up. Its golden scales are warm to the touch, like sand at noon.",
            "Hooked in the oasis on a clear day, on a cricket.", "The oasis, on a clear, bright day. It likes something that chirps."),
        ["mire_leviathan"] = new("leviathan",
            "A giant arapaima armoured in scales like old green coins. When it rises to breathe, the whole swamp goes quiet.",
            "Hooked in the mangrove swamp at night, on worms.", "The mangrove swamp after dark. It roots in the mud for worms."),
        ["starfall_ray"] = new("starray",
            "A manta whose back glitters with points of light, as if the night sky fell into the sea. It only rises when the moon is full.",
            "Hooked at the deep drop-off under a full moon, on a glow shrimp.", "The deep drop-off on a full-moon night. It follows things that glow.")
    };

    public static readonly Spot[] Spots =
    {
        new("lagoon", "The lagoon", "Fish in the lagoon", 100, 66, 38),
        new("rocks", "Rocky shore", "Fish off the rocky shore", 205, 24, 24),
        new("wreck", "Old wreck", "Fish by the old wreck", 76, 164, 26),
        new("deep", "Deep water", "Fish the deep water", 312, 95, 22),
        new("icehole", "Ice hole", "Fish through the ice", 605, 85, 22, "frost"),
        new("glacier", "Glacier shore", "Fish off the glacier shore", 795, 88, 26, "frost"),
        new("oasis", "The oasis", "Fish in the oasis", 685, 405, 40, "dunes"),
        new("mirage", "Mirage coast", "Fish off the mirage coast", 665, 505, 26, "dunes"),
        new("swamp", "Mangrove swamp", "Fish in the swamp", 145, 405, 42, "mire"),
        new("coral", "Coral shallows", "Fish the coral shallows", 195, 515, 26, "mire"),
        // Cave spots move with each randomly generated floor; their real positions come from Game.SpotPos.
        new("cavepool", "Cave pool", "Fish the cave pool", 0, 0, 36, "frost", "cave"),
        new("ancientpool", "Ancient pool", "Fish the ancient pool", 0, 0, 44, "frost", "cave"),
        new("atolllagoon", "Atoll lagoon", "Fish the atoll lagoon", 1060, 272, 32, "atoll"),
        new("dropoff", "Deep drop-off", "Fish the deep drop-off", 1135, 76, 36, "atoll"),
        // A blue hole hidden in a ring of palms on the atoll. It stays off the map and out of the Fish log until you find it.
        new("starwell", "The Starwell", "Fish the Starwell", Game.StarwellX, Game.StarwellY, 40, "atoll")
    };
    public static readonly Dictionary<string, Spot> SpotById = Spots.ToDictionary(s => s.Id);

    public const double OddChance = 0.07;
    public static readonly OddCatch[] Odd =
    {
        new("dog", "Dog", "lagoon", 1.6f, "A dog?!",
            "A very soggy dog paddled straight onto your hook. It looks absolutely thrilled about it.",
            "The dog shakes itself dry all over you, then trots off happily."),
        new("sheep", "Sheep", "glacier", 1.5f, "A sheep?!",
            "A sheep in a thick, frosty fleece. Nobody knows how it got into the sea, least of all the sheep.",
            "The sheep gives an offended baa and stomps off through the snow."),
        new("cat", "Cat", "oasis", 1.8f, "A cat?!",
            "An extremely wet and extremely offended cat. It will remember this.",
            "The cat glares at you, then stalks away with its tail held high."),
        new("pig", "Pig", "swamp", 1.5f, "A pig?!",
            "A muddy, cheerful pig. Honestly, it seems to have been enjoying the swamp.",
            "The pig snorts, rolls in the mud once more, and trundles off."),
        new("parrot", "Parrot", "atolllagoon", 1.7f, "A parrot?!",
            "A soaked and furious parrot. It has a lot to say about this, and it says all of it.",
            "The parrot shakes itself off, squawks something rude, and stalks off toward the palms.")
    };
    public static readonly Dictionary<string, OddCatch> OddById = Odd.ToDictionary(o => o.Id);

    public static readonly Dictionary<string, string> TimeLabel = new() { ["day"] = "Daytime", ["night"] = "Night", ["any"] = "Any time" };

    static Dictionary<string, int> Cost(params (string id, int n)[] c) => c.ToDictionary(p => p.id, p => p.n);

    public static readonly BuildDef[] Builds =
    {
        new("path", "Stone path", Cost(("stone", 1)), "Looks nice, walks nice."),
        new("fence", "Fence", Cost(("wood", 1)), "Keeps things in, or out.", Box: new[] { 0, 2, 10, 8 }),
        new("lantern", "Lantern", Cost(("wood", 1), ("stone", 1)), "Lights up the night.", Box: new[] { 3, 5, 4, 4 }, Light: new[] { 5, -4, 30 },
            Tip: "Your lantern will light the way after dark."),
        new("baitbox", "Bait box", Cost(("wood", 2), ("stone", 1)), "Fish bite faster nearby.", Box: new[] { 1, 3, 8, 6 },
            Tip: "Cast from near the bait box and fish will bite faster."),
        new("campfire", "Campfire", Cost(("wood", 3), ("stone", 2)), "Rest here, or cook on it.", Box: new[] { 1, 3, 9, 5 }, Light: new[] { 5, 3, 42 }, Rest: new[] { 5, 6 },
            Tip: "At your campfire, E rests and F cooks.", Station: "fire"),
        new("shack", "Shack", Cost(("wood", 8), ("stone", 4)), "A home of your own to furnish.", W: 2, Box: new[] { 1, 0, 18, 9 }, Light: new[] { 10, 2, 16 },
            Tip: "Press E at the shack door to go inside and furnish it.", Door: true),
        new("berrybush", "Berry bush", Cost(("sapling", 1)), "Pick berries from it every day.", Box: new[] { 1, 3, 8, 6 },
            Tip: "Your bush will have berries every morning. Face it and press E to pick them."),
        new("smoker", "Smoking rack", Cost(("wood", 5), ("stone", 3)), "Smoke fish to eat or sell.", Box: new[] { 1, 3, 8, 6 }, Station: "smoker",
            Tip: "Press E at the smoking rack to smoke fish."),
        new("crabpot", "Crab pot", Cost(("crab_pot", 1)), "Set it in shallow water; haul it up each morning.", Water: true,
            Tip: "Come back tomorrow, face the float and press E to haul up the pot."),

        new("workbench", "Workbench", Cost(("wood", 6), ("stone", 2)), "Craft tools and rods.", W: 2, Box: new[] { 1, 3, 18, 6 }, Indoor: true, Station: "workbench",
            Tip: "Press E at the workbench to craft."),
        new("furnace", "Furnace", Cost(("stone", 10), ("wood", 2)), "Smelt ore into bars.", Box: new[] { 1, 2, 8, 7 }, Light: new[] { 5, 5, 26 }, Indoor: true, Station: "furnace",
            Tip: "Press E at the furnace to smelt ore into bars."),
        new("stove", "Cooking stove", Cost(("stone", 6), ("wood", 2)), "Cook hearty meals.", Box: new[] { 1, 3, 8, 6 }, Light: new[] { 5, 6, 20 }, Indoor: true, Station: "stove",
            Tip: "Press E at the stove to cook."),
        new("bed", "Bed", Cost(("wood", 6), ("wool", 2)), "Sleep to pass the time.", W: 2, Box: new[] { 1, 2, 18, 7 }, Rest: new[] { 10, 13 }, Indoor: true,
            Tip: "Stand by the bed and press E to sleep."),
        new("table", "Table", Cost(("wood", 4)), "Somewhere to put your cup.", Box: new[] { 1, 4, 8, 5 }, Indoor: true),
        new("rug", "Rug", Cost(("wool", 2)), "Soft underfoot.", W: 2, Indoor: true),
        new("plant", "Potted plant", Cost(("stone", 1), ("berries", 1)), "A little green.", Box: new[] { 2, 5, 6, 4 }, Indoor: true),
        new("lamp", "Lamp", Cost(("wood", 1), ("copper_bar", 1)), "Warm light at night.", Box: new[] { 3, 6, 4, 3 }, Light: new[] { 5, -5, 44 }, Indoor: true),
        new("aquarium", "Aquarium", Cost(("wood", 4), ("stone", 6), ("copper_bar", 2)), "Show off up to four fish.", W: 2, Box: new[] { 1, 2, 18, 7 }, Indoor: true,
            Tip: "Press E at the aquarium to put fish in it.")
    };
    public static readonly Dictionary<string, BuildDef> BuildById = Builds.ToDictionary(b => b.Id);
    public static readonly string[] OutdoorTools = Builds.Where(b => !b.Indoor).Select(b => b.Id).Append("remove").ToArray();
    public static readonly string[] IndoorTools = Builds.Where(b => b.Indoor).Select(b => b.Id).Append("remove").ToArray();
}
