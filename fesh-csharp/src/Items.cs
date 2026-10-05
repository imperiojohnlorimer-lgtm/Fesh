using Raylib_cs;

namespace Fesh;

// Kind is "tool", "rod", "material", "food" or "fish". Food is how much of the food meter it fills (out of 100).
sealed record ItemDef(string Id, string Name, string Kind, string Desc, int Food = 0, string Icon = null, string Tint = null);

// Station is "workbench", "furnace", "stove" or "fire" (campfires; the stove can cook these too).
// The ingredient "fish" means any raw fish.
sealed record Recipe(string Out, int Count, string Station, Dictionary<string, int> Needs);

sealed record RodStats(int Zone, float Bite, int Luck, string Summary);

// Tackle you equip in the tackle box (T). Slot is "reel", "line", "hook", "bobber" or "sinker".
// Reel speeds up the catch meter, Line multiplies the tension a line can take, Size makes fish heavier,
// Window adds seconds to a bite, Depth lets a short cast reach deeper water, Luck adds rare luck.
sealed record TackleDef(string Slot, float Reel = 1, float Line = 1, float Size = 1, float Window = 0, int Depth = 0, int Luck = 0, string Summary = "");

// Bait multiplies the wait for a bite by Bite. Reusable bait (lures) is never used up.
// Which fish like a bait (three times as likely to bite) is decided in Game.BaitLikes.
sealed record BaitDef(float Bite, int Luck, bool Reusable, string Summary);

// A job from Tomas: bring Count of Item for Coins (and sometimes a bonus item).
sealed record RequestDef(string Item, int Count, int Coins, string Bonus = null, int BonusCount = 0);

// Animals that live on the islands. Gift is what they hand over once a day when petted (null for none).
sealed record AnimalKind(string Kind, string Name, float Speed, string Pet, string Gift, string GiftText);

static class Items
{
    static readonly string[] FishTints = { "#cfe8ee", "#9fc3d1", "#e8b04a", "#c98b3a", "#7fd6a0", "#b5a3d9", "#e88aa0", "#8fb0d9", "#d9c27a", "#a6d6c9" };

    public static readonly List<ItemDef> All = new()
    {
        new("axe", "Stone axe", "tool", "Chops trees, cacti and palms. Face one and press E.", Icon: "axe"),
        new("pickaxe", "Stone pickaxe", "tool", "Breaks boulders and mines copper ore.", Icon: "pickaxe", Tint: "#8a8f93"),
        new("copper_pickaxe", "Copper pickaxe", "tool", "Strong enough for iron ore.", Icon: "pickaxe", Tint: "#d9823f"),
        new("iron_pickaxe", "Iron pickaxe", "tool", "Strong enough for gold ore.", Icon: "pickaxe", Tint: "#c9d4dc"),
        new("gold_pickaxe", "Gold pickaxe", "tool", "Strong enough for crystal.", Icon: "pickaxe", Tint: "#f3c25b"),
        new("crystal_pickaxe", "Crystal pickaxe", "tool", "The only pick that can cut abyssite, deep in the Ancient Floor.", Icon: "pickaxe", Tint: "#7fe8ff"),
        new("copper_sword", "Copper sword", "weapon", "3 damage. Better than swinging a pickaxe at things.", Icon: "sword", Tint: "#d9823f"),
        new("iron_sword", "Iron sword", "weapon", "5 damage. A proper blade.", Icon: "sword", Tint: "#c9d4dc"),
        new("gold_sword", "Gold sword", "weapon", "7 damage. Heavy and shiny.", Icon: "sword", Tint: "#f3c25b"),
        new("crystal_blade", "Crystal blade", "weapon", "10 damage. Hums faintly in the dark.", Icon: "sword", Tint: "#7fe8ff"),
        new("shell_armor", "Shell armour", "armor", "Rock crab shells strapped together. Monsters hurt you a third less.", Icon: "armor"),
        new("rod_old", "Old rod", "rod", "The rod you arrived with. It does the job.", Icon: "rod", Tint: "#8a6440"),
        new("rod_copper", "Copper rod", "rod", "A wider green bar, quicker bites, a little more luck.", Icon: "rod", Tint: "#d9823f"),
        new("rod_iron", "Iron rod", "rod", "Wider still, faster bites, better luck with rare fish.", Icon: "rod", Tint: "#b9c4cc"),
        new("rod_crystal", "Crystal rod", "rod", "Glowing and light. Rare fish can't resist it.", Icon: "rod", Tint: "#7fe8ff"),
        new("rod_ancient", "Ancient rod", "rod", "Made with abyssite. The rod for a fish older than the islands.", Icon: "rod", Tint: "#9b6be0"),
        new("boat", "Sailboat", "tool", "Sail from Pip's jetty on Saltmere to places the bridges don't reach.", Icon: "boat"),
        new("spear", "Fishing spear", "tool", "At reef shallows (coral shallows, the atoll lagoon) press G to spearfish.", Icon: "spear"),
        new("copper_reel", "Copper reel", "tackle", "The catch meter fills 15% faster.", Icon: "reel", Tint: "#d9823f"),
        new("iron_reel", "Iron reel", "tackle", "The catch meter fills 30% faster.", Icon: "reel", Tint: "#c9d4dc"),
        new("gold_reel", "Gold reel", "tackle", "The catch meter fills 45% faster.", Icon: "reel", Tint: "#f3c25b"),
        new("silk_line", "Silk line", "tackle", "Takes 40% more tension before a running fish snaps it.", Icon: "line", Tint: "#f2efe6"),
        new("crystal_line", "Crystal line", "tackle", "Takes 90% more tension before it snaps.", Icon: "line", Tint: "#9fe8ff"),
        new("barbed_hook", "Barbed hook", "tackle", "Fish come out 10% heavier.", Icon: "hook", Tint: "#d9823f"),
        new("biggame_hook", "Big-game hook", "tackle", "Fish come out 25% heavier.", Icon: "hook", Tint: "#c9d4dc"),
        new("golden_hook", "Golden hook", "tackle", "From a sunken chest. Fish come out 20% heavier, and rare luck +1.", Icon: "hook", Tint: "#f3c25b"),
        new("cork_bobber", "Cork bobber", "tackle", "Bites last 0.3 s longer, and the perfect-hook moment is longer too.", Icon: "bobber", Tint: "#e04b3a"),
        new("glow_bobber", "Glow bobber", "tackle", "Bites last 0.5 s longer. Easy to see in the dark.", Icon: "bobber", Tint: "#7fe86b"),
        new("stone_sinker", "Stone sinker", "tackle", "Your cast sinks one depth deeper.", Icon: "sinker", Tint: "#9aa0a5"),
        new("iron_sinker", "Iron sinker", "tackle", "Your cast sinks two depths deeper: even a short cast reaches deep fish.", Icon: "sinker", Tint: "#5e6468"),
        new("spinner_lure", "Spinner lure", "tackle", "A bait that's never used up. Fish that run chase it.", Icon: "lure", Tint: "#d9823f"),
        new("fly_lure", "Fly lure", "tackle", "A bait that's never used up. Jumpers snap at it.", Icon: "fly"),
        new("sunglasses", "Polarized sunglasses", "accessory", "Cut the glare: you can see fish shadows in the water. Cast onto one for a quick bite.", Icon: "sunglasses"),
        new("fish_finder", "Fish finder", "accessory", "Shows what's biting at a spot right now, and the odds.", Icon: "finder"),
        new("waders", "Waders", "accessory", "Walk out into shallow water and fish from there. Wading also lets your cast reach deeper.", Icon: "waders"),
        new("lucky_charm", "Abyssite charm", "accessory", "Rare luck +2 when fishing.", Icon: "charm"),
        new("headlamp", "Headlamp", "accessory", "A much bigger circle of light at night and in the caves.", Icon: "headlamp"),
        new("cooler", "Cooler", "accessory", "Keeps your catch fresh: Pip pays 25% more for fish.", Icon: "cooler"),
        new("bait", "Bait", "gear", "One is used each time you cast. Fish bite much faster, and rare ones a little more often.", Icon: "bait"),
        new("glow_bait", "Glow bait", "gear", "Bait soaked in slime gel. Used before plain bait: even faster bites and much better rare luck.", Icon: "glowbait"),
        new("worm", "Worms", "gear", "Dug from little mounds of earth. Freshwater fish and bottom feeders love them. Pick them in the tackle box (T).", Icon: "worm"),
        new("cricket", "Cricket", "gear", "Caught in the grass. Jumpers and shallow-water fish rise to them. Pick them in the tackle box (T).", Icon: "cricket"),
        new("cut_bait", "Cut bait", "gear", "Chunks of cheap fish. Sea fish that run or hug the bottom follow the scent.", Icon: "cutbait"),
        new("chum", "Chum", "gear", "At a fishing spot, press F to throw it. Fish crowd in and bite twice as fast for two minutes.", Icon: "chum"),
        new("crab_pot", "Crab pot", "gear", "Set it in shallow water with B. Haul it up the next morning.", Icon: "pot"),
        new("sapling", "Berry sapling", "gear", "Plant it outdoors with B. It grows berries every day.", Icon: "sapling"),
        new("sailcloth", "Sailcloth", "material", "Strong canvas from Pip. A boat needs one.", Icon: "sailcloth"),
        new("wood", "Wood", "material", "From driftwood and trees.", Icon: "wood"),
        new("stone", "Stone", "material", "From beaches and boulders.", Icon: "stone"),
        new("copper_ore", "Copper ore", "material", "Smelt two into a copper bar at a furnace.", Icon: "ore", Tint: "#d9823f"),
        new("iron_ore", "Iron ore", "material", "Smelt two into an iron bar at a furnace.", Icon: "ore", Tint: "#e8e2d6"),
        new("gold_ore", "Gold ore", "material", "Smelt two into a gold bar at a furnace. Needs an iron pickaxe.", Icon: "ore", Tint: "#f3c25b"),
        new("crystal", "Crystal", "material", "Cold, glowing shards from deep in the caverns. Needs a gold pickaxe.", Icon: "crystal"),
        new("abyssite", "Abyssite", "material", "Ancient dark ore from the Ancient Floor. Needs a crystal pickaxe.", Icon: "abyssite"),
        new("copper_bar", "Copper bar", "material", "For copper tools, rods and lamps.", Icon: "bar", Tint: "#d9823f"),
        new("iron_bar", "Iron bar", "material", "For iron tools, rods and the boat.", Icon: "bar", Tint: "#b9c4cc"),
        new("gold_bar", "Gold bar", "material", "For gold tools and the best gear.", Icon: "bar", Tint: "#f3c25b"),
        new("slime_gel", "Slime gel", "material", "Glowing goo from a cave slime. Fish love it.", Icon: "gel"),
        new("bat_wing", "Bat wing", "material", "Leathery. Pip will take it.", Icon: "wing"),
        new("crab_shell", "Crab shell", "material", "Hard as rock. Good for armour.", Icon: "shell"),
        new("shadow_essence", "Shadow essence", "material", "What's left of a shade. It's cold to hold.", Icon: "essence"),
        new("wool", "Wool", "material", "A gift from a friendly sheep. Beds and rugs need it.", Icon: "wool"),
        new("seaweed", "Seaweed", "material", "Comes up in crab pots. Sushi needs it.", Icon: "seaweed"),
        new("pearl", "Pearl", "material", "A lucky find in a crab pot or a sunken chest. Pip pays well for one.", Icon: "pearl"),
        new("old_boot", "Old boot", "material", "Somebody lost this a long time ago. Pip will take it, just about.", Icon: "boot"),
        new("grilled_fish", "Grilled fish", "food", "Smoky and filling.", 25, "grilled"),
        new("smoked_fish", "Smoked fish", "food", "Slow-smoked on the rack. Keeps forever and sells well.", 30, "smoked"),
        new("fish_jerky", "Fish jerky", "food", "Chewy smoked strips of cut bait.", 15, "jerky"),
        new("sushi_roll", "Sushi roll", "food", "Fish rolled in seaweed. Fresh and filling.", 35, "sushi"),
        new("maki_platter", "Maki platter", "food", "A whole platter of rolls. Fills you right up.", 80, "maki"),
        new("fish_stew", "Fish stew", "food", "Two fish and some berries. Very filling.", 55, "stew"),
        new("fried_egg", "Fried egg", "food", "Sunny side up.", 18, "fried"),
        new("berries", "Berries", "food", "Sweet and a little sour. Grow on bushes.", 6, "berries"),
        new("egg", "Egg", "food", "Better cooked.", 5, "egg"),
        new("truffle", "Truffle", "food", "A pig found this. It smells wonderful.", 12, "truffle"),
        new("coconut", "Coconut", "food", "From a palm tree.", 10, "coconut"),
        new("cactus_fruit", "Cactus fruit", "food", "Juicy, once you get past the spines.", 8, "cactusfruit")
    };

    static Items()
    {
        int i = 0;
        foreach (var s in Data.Spots)
            foreach (var f in Data.Common[s.Id])
            {
                string where = f.Legend ? $"A legend of {s.Label.ToLowerInvariant()}." : $"Caught at {s.Label}.";
                All.Add(new ItemDef(f.Id, f.Name, "fish", $"{where} Eat it raw in a pinch, or cook it.", 6, FishIcon(f.Id), FishTints[i++ % FishTints.Length]));
            }
        foreach (var (biome, list) in Data.PotCatch)
            foreach (var f in list)
                All.Add(new ItemDef(f.Id, f.Name, "fish", $"Found in a crab pot on {Data.Biomes.First(b => b.Id == biome).Name}.", 6, FishIcon(f.Id), FishTints[i++ % FishTints.Length]));
        ById = All.ToDictionary(d => d.Id);
    }

    // Crabs, shrimp, squid, rays and eels get their own little icons; everything else is a fish.
    static string FishIcon(string id) =>
        id.Contains("crab") ? "crab" : id.Contains("lobster") || id.Contains("crayfish") ? "lobster" : id.Contains("shrimp") ? "shrimp"
        : id.Contains("squid") ? "squid" : id.Contains("ray") ? "ray" : id.Contains("eel") ? "eel" : "fish";

    public static readonly Dictionary<string, ItemDef> ById;
    public static readonly string[] KindOrder = { "tool", "weapon", "armor", "rod", "tackle", "accessory", "gear", "material", "food", "fish" };

    public static readonly Dictionary<string, TackleDef> Tackle = new()
    {
        ["copper_reel"] = new("reel", Reel: 1.15f), ["iron_reel"] = new("reel", Reel: 1.3f), ["gold_reel"] = new("reel", Reel: 1.45f),
        ["silk_line"] = new("line", Line: 1.4f), ["crystal_line"] = new("line", Line: 1.9f),
        ["barbed_hook"] = new("hook", Size: 1.1f), ["biggame_hook"] = new("hook", Size: 1.25f), ["golden_hook"] = new("hook", Size: 1.2f, Luck: 1),
        ["cork_bobber"] = new("bobber", Window: 0.3f), ["glow_bobber"] = new("bobber", Window: 0.5f),
        ["stone_sinker"] = new("sinker", Depth: 1), ["iron_sinker"] = new("sinker", Depth: 2)
    };
    // Weakest first. When a slot has never been set in the tackle box, the best you own is used (sinkers excepted).
    public static readonly string[] TackleSlots = { "reel", "line", "hook", "bobber", "sinker" };
    public static readonly Dictionary<string, string[]> TackleOrder = new()
    {
        ["reel"] = new[] { "copper_reel", "iron_reel", "gold_reel" }, ["line"] = new[] { "silk_line", "crystal_line" },
        ["hook"] = new[] { "barbed_hook", "biggame_hook", "golden_hook" }, ["bobber"] = new[] { "cork_bobber", "glow_bobber" },
        ["sinker"] = new[] { "stone_sinker", "iron_sinker" }
    };

    public static readonly Dictionary<string, BaitDef> Baits = new()
    {
        ["glow_bait"] = new(0.4f, 3, false, "Much faster bites, much better rare luck."),
        ["bait"] = new(0.6f, 1, false, "Faster bites, a little more rare luck."),
        ["worm"] = new(0.6f, 1, false, "Freshwater fish and bottom feeders love worms."),
        ["cricket"] = new(0.6f, 1, false, "Jumpers and shallow-water fish rise to a cricket."),
        ["cut_bait"] = new(0.6f, 1, false, "Sea fish that run or hug the bottom follow the scent."),
        ["glow_shrimp"] = new(0.5f, 2, false, "Deep-water fish can't resist a glowing shrimp."),
        ["slime_gel"] = new(0.5f, 2, false, "Cave fish go wild for it."),
        ["berries"] = new(0.7f, 0, false, "Carp and other plant-eaters like a berry."),
        ["spinner_lure"] = new(0.85f, 0, true, "Never used up. Fish that run chase it."),
        ["fly_lure"] = new(0.85f, 0, true, "Never used up. Jumpers snap at it.")
    };

    // What Pip pays for one. Tools, rods, bait, saplings and sailcloth can't be sold.
    static readonly Dictionary<string, int> MaterialPrice = new()
    {
        ["wood"] = 1, ["stone"] = 1, ["copper_ore"] = 4, ["iron_ore"] = 7, ["crystal"] = 20, ["copper_bar"] = 12, ["iron_bar"] = 22,
        ["gold_ore"] = 12, ["gold_bar"] = 35, ["abyssite"] = 60, ["slime_gel"] = 4, ["bat_wing"] = 6, ["crab_shell"] = 10, ["shadow_essence"] = 25,
        ["wool"] = 6, ["egg"] = 3, ["berries"] = 1, ["truffle"] = 15, ["coconut"] = 4, ["cactus_fruit"] = 3,
        ["grilled_fish"] = 10, ["fried_egg"] = 6, ["fish_stew"] = 25, ["smoked_fish"] = 20, ["fish_jerky"] = 12, ["sushi_roll"] = 30, ["maki_platter"] = 70,
        ["seaweed"] = 2, ["pearl"] = 80, ["old_boot"] = 1
    };

    // Pip's base price for one. Game.PriceOf adds bonuses (cooler, collections) and Game.SaleValue big fish.
    public static int SellPrice(string id)
    {
        if (MaterialPrice.TryGetValue(id, out int p)) return p;
        if (!Data.FishById.TryGetValue(id, out var f)) return 0;
        if (f.Legend) return 400;
        return f.Rare ? 40 + (int)MathF.Round(f.Difficulty * 10) : Math.Max(3, (int)MathF.Round(f.Difficulty * 4));
    }

    // Pip's stock and prices.
    public static readonly (string id, int price)[] Shop =
    {
        ("bait", 5), ("chum", 8), ("cork_bobber", 20), ("crab_pot", 40), ("spinner_lure", 45), ("sapling", 15),
        ("sailcloth", 120), ("copper_bar", 30), ("iron_bar", 55), ("crystal", 90)
    };

    // Tomas's first twelve requests, in order. After that he asks for random fish.
    public static readonly RequestDef[] RequestChain =
    {
        new("pond_perch", 3, 20), new("wood", 6, 15, "bait", 3), new("rock_goby", 2, 25), new("egg", 2, 20),
        new("arctic_char", 2, 40), new("wool", 3, 45), new("oasis_tilapia", 2, 45), new("copper_bar", 2, 60),
        new("mudskipper", 3, 50), new("grilled_fish", 2, 35, "bait", 5), new("crystal", 1, 80), new("mirage_koi", 1, 120)
    };

    static readonly HashSet<string> MassNouns = new()
    {
        "wood", "stone", "wool", "berries", "crystal", "copper_ore", "iron_ore", "gold_ore", "abyssite", "bait", "glow_bait", "sailcloth", "slime_gel", "shadow_essence"
    };

    // "3 pond perch", "2 copper bars", "6 wood".
    public static string Amount(string id, int n)
    {
        var d = ById[id];
        string name = d.Name.ToLowerInvariant();
        if (n == 1) return $"1 {name}";
        bool plural = d.Kind != "fish" && !MassNouns.Contains(id) && !name.EndsWith("s");
        return $"{n} {name}{(plural ? "s" : "")}";
    }

    static Dictionary<string, int> N(params (string id, int n)[] c) => c.ToDictionary(p => p.id, p => p.n);

    public static readonly Recipe[] Recipes =
    {
        new("axe", 1, "workbench", N(("wood", 3), ("stone", 2))),
        new("pickaxe", 1, "workbench", N(("wood", 3), ("stone", 3))),
        new("copper_pickaxe", 1, "workbench", N(("wood", 2), ("copper_bar", 2))),
        new("iron_pickaxe", 1, "workbench", N(("wood", 2), ("iron_bar", 3))),
        new("gold_pickaxe", 1, "workbench", N(("wood", 2), ("gold_bar", 3))),
        new("crystal_pickaxe", 1, "workbench", N(("gold_bar", 2), ("crystal", 3), ("shadow_essence", 1))),
        new("boat", 1, "workbench", N(("wood", 20), ("iron_bar", 4), ("sailcloth", 1))),
        new("rod_copper", 1, "workbench", N(("wood", 2), ("copper_bar", 3))),
        new("rod_iron", 1, "workbench", N(("rod_copper", 1), ("iron_bar", 3))),
        new("rod_crystal", 1, "workbench", N(("rod_iron", 1), ("iron_bar", 1), ("crystal", 3))),
        new("rod_ancient", 1, "workbench", N(("rod_crystal", 1), ("gold_bar", 2), ("abyssite", 3))),
        new("copper_sword", 1, "workbench", N(("wood", 1), ("copper_bar", 2))),
        new("iron_sword", 1, "workbench", N(("wood", 1), ("iron_bar", 3))),
        new("gold_sword", 1, "workbench", N(("wood", 1), ("gold_bar", 3))),
        new("crystal_blade", 1, "workbench", N(("gold_bar", 2), ("crystal", 3), ("shadow_essence", 2))),
        new("shell_armor", 1, "workbench", N(("crab_shell", 4), ("iron_bar", 2))),
        new("spear", 1, "workbench", N(("wood", 2), ("copper_bar", 1))),
        new("crab_pot", 1, "workbench", N(("wood", 4), ("wool", 1))),
        new("copper_reel", 1, "workbench", N(("wood", 1), ("copper_bar", 2))),
        new("iron_reel", 1, "workbench", N(("copper_reel", 1), ("iron_bar", 2))),
        new("gold_reel", 1, "workbench", N(("iron_reel", 1), ("gold_bar", 2))),
        new("silk_line", 1, "workbench", N(("wool", 3))),
        new("crystal_line", 1, "workbench", N(("silk_line", 1), ("crystal", 1), ("slime_gel", 1))),
        new("barbed_hook", 1, "workbench", N(("copper_bar", 1))),
        new("biggame_hook", 1, "workbench", N(("barbed_hook", 1), ("iron_bar", 2))),
        new("cork_bobber", 1, "workbench", N(("wood", 2))),
        new("glow_bobber", 1, "workbench", N(("cork_bobber", 1), ("slime_gel", 2))),
        new("stone_sinker", 1, "workbench", N(("stone", 3))),
        new("iron_sinker", 1, "workbench", N(("stone_sinker", 1), ("iron_bar", 1))),
        new("spinner_lure", 1, "workbench", N(("copper_bar", 1))),
        new("fly_lure", 1, "workbench", N(("wool", 1), ("bat_wing", 1))),
        new("sunglasses", 1, "workbench", N(("copper_bar", 1), ("crystal", 1))),
        new("fish_finder", 1, "workbench", N(("copper_bar", 2), ("iron_bar", 1), ("crystal", 1))),
        new("waders", 1, "workbench", N(("slime_gel", 3), ("wool", 2))),
        new("lucky_charm", 1, "workbench", N(("abyssite", 2), ("gold_bar", 1))),
        new("headlamp", 1, "workbench", N(("copper_bar", 2), ("crystal", 1))),
        new("cooler", 1, "workbench", N(("wood", 6), ("iron_bar", 1), ("wool", 1))),
        new("glow_bait", 3, "workbench", N(("slime_gel", 1), ("bait", 2))),
        new("cut_bait", 4, "workbench", N(("fish", 1))),
        new("chum", 2, "workbench", N(("fish", 1), ("berries", 1))),
        new("copper_bar", 1, "furnace", N(("copper_ore", 2), ("wood", 1))),
        new("iron_bar", 1, "furnace", N(("iron_ore", 2), ("wood", 1))),
        new("gold_bar", 1, "furnace", N(("gold_ore", 2), ("wood", 1))),
        new("smoked_fish", 1, "smoker", N(("fish", 1))),
        new("fish_jerky", 2, "smoker", N(("cut_bait", 3))),
        new("grilled_fish", 1, "fire", N(("fish", 1))),
        new("fried_egg", 1, "fire", N(("egg", 1))),
        new("fish_stew", 1, "stove", N(("fish", 2), ("berries", 1))),
        new("sushi_roll", 1, "stove", N(("fish", 1), ("seaweed", 1))),
        new("maki_platter", 1, "stove", N(("fish", 3), ("seaweed", 2), ("berries", 1)))
    };

    public static readonly Dictionary<string, string> StationName = new()
    {
        ["workbench"] = "Workbench", ["furnace"] = "Furnace", ["stove"] = "Cooking stove", ["fire"] = "Campfire", ["smoker"] = "Smoking rack"
    };

    public static bool StationMakes(string station, Recipe r) => r.Station == station || (station == "stove" && r.Station == "fire");

    public static readonly string[] Rods = { "rod_old", "rod_copper", "rod_iron", "rod_crystal", "rod_ancient" };
    public static readonly Dictionary<string, RodStats> Rod = new()
    {
        ["rod_old"] = new(0, 1f, 0, "Standard green bar and bite speed."),
        ["rod_copper"] = new(5, 0.85f, 1, "Green bar +5, bites 15% faster, rare luck +1."),
        ["rod_iron"] = new(9, 0.7f, 2, "Green bar +9, bites 30% faster, rare luck +2."),
        ["rod_crystal"] = new(13, 0.55f, 3, "Green bar +13, bites 45% faster, rare luck +3."),
        ["rod_ancient"] = new(17, 0.45f, 4, "Green bar +17, bites 55% faster, rare luck +4.")
    };

    // Pickaxes from weakest to strongest; an ore needs a pickaxe at least as strong as its tier.
    public static readonly string[] Pickaxes = { "pickaxe", "copper_pickaxe", "iron_pickaxe", "gold_pickaxe", "crystal_pickaxe" };
    public static readonly (string id, int damage)[] Weapons = { ("crystal_blade", 10), ("gold_sword", 7), ("iron_sword", 5), ("copper_sword", 3) };

    // Workbench tabs, by what a recipe makes.
    public static string Category(Recipe r) => r.Out == "crab_pot" ? "Tools" : ById[r.Out].Kind switch
    {
        "rod" => "Rods", "tackle" => "Tackle", "accessory" => "Gear", "weapon" or "armor" => "Combat", "gear" => "Bait", _ => "Tools"
    };

    public static readonly Dictionary<string, AnimalKind> Animals = new()
    {
        ["dog"] = new("dog", "Biscuit", 26, "Biscuit wags his whole body.", "wood", "Biscuit drops a stick at your feet. (+1 wood)"),
        ["chicken"] = new("chicken", "chicken", 18, "The chicken clucks and lets you pat it.", "egg", "The chicken has laid an egg for you. (+1 egg)"),
        ["sheep"] = new("sheep", "sheep", 12, "The sheep leans into your hand. Its fleece is wonderfully warm.", "wool", "You gently gather some loose wool. (+1 wool)"),
        ["cat"] = new("cat", "cat", 20, "The cat allows exactly one pat.", null, null),
        ["pig"] = new("pig", "pig", 12, "The pig snorts happily.", "truffle", "The pig snuffles in the dirt and digs up a truffle. (+1 truffle)")
    };

    // Where animals live, in world pixels. They wander around this point.
    public static readonly (string kind, float x, float y)[] AnimalSpawns =
    {
        ("dog", 180, 118), ("chicken", 205, 128), ("chicken", 215, 135), ("chicken", 225, 122),
        ("sheep", 530, 125), ("sheep", 700, 60), ("sheep", 720, 130), ("sheep", 600, 140),
        ("cat", 640, 362), ("cat", 735, 440),
        ("pig", 95, 330), ("pig", 230, 380), ("pig", 255, 440)
    };
}

// 12x12 pixel icons for the bag and crafting lists, drawn with rectangles and cached as textures.
static class ItemArt
{
    const int S = 12;
    static readonly Dictionary<string, Texture2D> cache = new();

    public static Texture2D Icon(string itemId)
    {
        if (cache.TryGetValue(itemId, out var tex)) return tex;
        var p = new Pix(S, S);
        if (Items.ById.TryGetValue(itemId, out var d)) Draw(p, d.Icon ?? itemId, d.Tint);
        else Draw(p, itemId, null);
        tex = Gfx.ToTexture(p.Buf, S, S, TextureFilter.Point);
        cache[itemId] = tex;
        return tex;
    }

    public static void Shutdown()
    {
        foreach (var t in cache.Values) Raylib.UnloadTexture(t);
        cache.Clear();
    }

    static void Draw(Pix p, string icon, string tint)
    {
        switch (icon)
        {
            case "wood":
                p.Rect(1, 4, 10, 5, "#8a6440"); p.Rect(1, 4, 10, 1, "#b08458"); p.Rect(1, 8, 10, 1, "#6b4a2b");
                p.Rect(9, 4, 2, 5, "#d9b07a"); p.Rect(10, 6, 1, 1, "#8a6440");
                break;
            case "stone":
                p.Rect(2, 5, 8, 5, "#6e737a"); p.Rect(3, 4, 6, 5, "#9aa0a5"); p.Rect(4, 4, 3, 2, "#c0c5c9");
                break;
            case "ore":
                p.Rect(2, 5, 8, 5, "#5e6468"); p.Rect(3, 4, 6, 5, "#7d8288"); p.Rect(4, 4, 2, 1, "#9aa0a5");
                p.Rect(4, 6, 2, 1, tint); p.Rect(7, 5, 1, 2, tint); p.Rect(5, 8, 2, 1, tint); p.Rect(8, 8, 1, 1, tint);
                break;
            case "crystal":
                p.Rect(5, 1, 2, 10, "#9fe8ff"); p.Rect(3, 4, 2, 6, "#5fc8e8"); p.Rect(7, 3, 2, 7, "#bff4ff");
                p.Rect(5, 2, 1, 4, "#ffffff"); p.Rect(2, 10, 8, 1, "#3a8db0");
                break;
            case "bar":
                p.Rect(2, 6, 8, 4, Darker(tint)); p.Rect(3, 5, 6, 1, tint); p.Rect(3, 6, 6, 2, tint); p.Rect(4, 6, 3, 1, "#ffffff");
                break;
            case "wool":
                p.Rect(2, 5, 8, 4, "#f2efe6"); p.Rect(3, 4, 3, 2, "#f2efe6"); p.Rect(6, 3, 3, 3, "#f2efe6");
                p.Rect(2, 8, 8, 1, "#d6d1c4"); p.Rect(7, 4, 1, 1, "#ffffff");
                break;
            case "egg":
                p.Rect(4, 3, 4, 7, "#f4ead2"); p.Rect(3, 5, 6, 4, "#f4ead2"); p.Rect(4, 9, 4, 1, "#d9cba8"); p.Rect(5, 4, 1, 2, "#ffffff");
                break;
            case "berries":
                p.Rect(2, 6, 3, 3, "#c0392b"); p.Rect(6, 7, 3, 3, "#c0392b"); p.Rect(4, 3, 3, 3, "#e04b3a");
                p.Rect(5, 1, 3, 2, "#4c9a45"); p.Rect(3, 6, 1, 1, "#ff8b7a"); p.Rect(5, 3, 1, 1, "#ff8b7a");
                break;
            case "truffle":
                p.Rect(3, 4, 6, 6, "#4a2f1d"); p.Rect(2, 6, 8, 3, "#4a2f1d"); p.Rect(4, 5, 1, 1, "#6b4a2b"); p.Rect(7, 7, 1, 1, "#6b4a2b"); p.Rect(5, 8, 1, 1, "#6b4a2b");
                break;
            case "coconut":
                p.Rect(3, 3, 6, 7, "#6b4a2b"); p.Rect(2, 5, 8, 3, "#6b4a2b"); p.Rect(4, 4, 2, 1, "#8a6440"); p.Rect(5, 6, 1, 1, "#2b1d14"); p.Rect(7, 6, 1, 1, "#2b1d14");
                break;
            case "cactusfruit":
                p.Rect(3, 4, 6, 6, "#e8608a"); p.Rect(4, 3, 4, 8, "#e8608a"); p.Rect(4, 2, 4, 1, "#4f8a3c"); p.Rect(5, 5, 1, 1, "#ffd0dc"); p.Rect(7, 8, 1, 1, "#ffd0dc");
                break;
            case "grilled":
                FishShape(p, "#c98b3a"); p.Rect(4, 4, 1, 4, "#6b4a2b"); p.Rect(6, 4, 1, 4, "#6b4a2b");
                break;
            case "fried":
                p.Rect(2, 4, 8, 5, "#ffffff"); p.Rect(3, 3, 5, 7, "#ffffff"); p.Rect(4, 5, 3, 3, "#f3c25b"); p.Rect(4, 5, 1, 1, "#ffe28a");
                break;
            case "stew":
                p.Rect(1, 6, 10, 4, "#8a5f36"); p.Rect(2, 10, 8, 1, "#6b4a2b"); p.Rect(2, 5, 8, 2, "#e08a4a"); p.Rect(4, 5, 2, 1, "#f2c48a");
                p.Rect(4, 1, 1, 3, "#dfe9ee"); p.Rect(7, 2, 1, 2, "#dfe9ee");
                break;
            case "fish":
                FishShape(p, tint);
                break;
            case "axe":
                p.Line(3, 10, 8, 2, "#8a6440"); p.Line(4, 10, 9, 2, "#6b4a2b");
                p.Rect(7, 1, 4, 4, "#9aa0a5"); p.Rect(10, 2, 1, 3, "#c0c5c9"); p.Rect(7, 1, 1, 1, "#6e737a");
                break;
            case "pickaxe":
                p.Line(3, 10, 8, 3, "#8a6440"); p.Line(4, 10, 9, 3, "#6b4a2b");
                p.Rect(4, 2, 7, 2, tint); p.Rect(3, 3, 2, 2, tint); p.Rect(10, 3, 1, 2, tint); p.Rect(5, 2, 4, 1, "#ffffff");
                break;
            case "rod":
                p.Line(1, 11, 10, 1, tint); p.Line(2, 11, 11, 1, Darker(tint));
                p.Rect(2, 8, 3, 3, "#3b2a1d"); p.Rect(3, 9, 1, 1, "#9aa0a5");
                p.Line(11, 1, 11, 7, "#dfe9ee"); p.Rect(10, 7, 2, 2, "#e04b3a");
                break;
            case "bait":
                p.Rect(3, 5, 6, 6, "#9aa0a5"); p.Rect(3, 5, 6, 1, "#c0c5c9"); p.Rect(3, 7, 6, 2, "#e04b3a");
                p.Rect(4, 2, 1, 4, "#e8939a"); p.Rect(6, 1, 1, 5, "#e8939a"); p.Rect(7, 2, 1, 1, "#e8939a"); p.Rect(5, 2, 1, 1, "#e8939a");
                break;
            case "sapling":
                p.Rect(3, 8, 6, 3, "#b5523b"); p.Rect(3, 8, 6, 1, "#d9734f");
                p.Rect(5, 4, 2, 4, "#4c9a45"); p.Rect(2, 3, 3, 2, "#5aa047"); p.Rect(7, 2, 3, 2, "#5aa047"); p.Rect(8, 5, 1, 1, "#e04b3a");
                break;
            case "sailcloth":
                p.Rect(2, 4, 8, 6, "#f2efe6"); p.Rect(2, 4, 8, 1, "#ffffff"); p.Rect(2, 7, 8, 1, "#d6d1c4"); p.Rect(5, 3, 2, 8, "#c9a06a");
                break;
            case "boat":
                p.Rect(1, 8, 10, 2, "#8a5f36"); p.Rect(2, 10, 8, 1, "#6b4a2b"); p.Rect(6, 1, 1, 7, "#5b3a24");
                for (int i = 0; i < 6; i++) p.Rect(6 - i, 1 + i, i, 1, "#f2efe6");
                p.Rect(7, 3, 2, 4, "#e04b3a");
                break;
            case "sword":
                p.Line(3, 9, 10, 2, tint); p.Line(4, 9, 10, 3, "#ffffff");
                p.Rect(2, 8, 4, 1, "#8a6440"); p.Rect(3, 7, 1, 3, "#8a6440"); p.Line(1, 11, 3, 9, "#5b3a24");
                break;
            case "armor":
                p.Rect(2, 2, 8, 9, "#7d8288"); p.Rect(3, 3, 6, 7, "#9aa0a5"); p.Rect(5, 2, 2, 9, "#6e737a");
                p.Rect(1, 2, 2, 3, "#d9823f"); p.Rect(9, 2, 2, 3, "#d9823f"); p.Rect(4, 4, 1, 1, "#c0c5c9");
                break;
            case "glowbait":
                p.Rect(3, 5, 6, 6, "#5fa9c9"); p.Rect(3, 5, 6, 1, "#9fd3e6"); p.Rect(3, 7, 6, 2, "#7fe86b");
                p.Rect(4, 2, 1, 4, "#b8ff9a"); p.Rect(6, 1, 1, 5, "#b8ff9a"); p.Rect(7, 2, 1, 1, "#b8ff9a");
                break;
            case "abyssite":
                p.Rect(3, 4, 6, 6, "#3b2a5e"); p.Rect(4, 2, 4, 9, "#4f3a7a"); p.Rect(5, 3, 2, 3, "#9b6be0"); p.Rect(7, 6, 1, 2, "#c9a6ff");
                p.Rect(2, 10, 8, 1, "#24183a");
                break;
            case "gel":
                p.Rect(2, 6, 8, 4, "#5fd06b"); p.Rect(3, 4, 6, 2, "#5fd06b"); p.Rect(4, 5, 2, 1, "#c8ffcf"); p.Rect(2, 9, 8, 1, "#3f9a4a");
                break;
            case "wing":
                p.Rect(1, 4, 4, 3, "#5b3f6e"); p.Rect(5, 5, 2, 2, "#3b2a4a"); p.Rect(7, 4, 4, 3, "#5b3f6e");
                p.Rect(1, 7, 1, 2, "#5b3f6e"); p.Rect(10, 7, 1, 2, "#5b3f6e"); p.Rect(3, 7, 1, 1, "#5b3f6e"); p.Rect(8, 7, 1, 1, "#5b3f6e");
                break;
            case "shell":
                p.Rect(2, 4, 8, 6, "#8a8f93"); p.Rect(3, 3, 6, 1, "#9aa0a5"); p.Rect(3, 5, 6, 1, "#b4b9bc");
                p.Rect(4, 7, 1, 2, "#6e737a"); p.Rect(7, 7, 1, 2, "#6e737a"); p.Rect(1, 6, 1, 3, "#d9823f"); p.Rect(10, 6, 1, 3, "#d9823f");
                break;
            case "essence":
                p.Rect(4, 2, 4, 8, "#2a2238"); p.Rect(3, 4, 6, 4, "#2a2238"); p.Rect(5, 4, 2, 3, "#9b6be0"); p.Rect(5, 5, 1, 1, "#e0c9ff");
                p.Rect(4, 10, 1, 1, "#2a2238"); p.Rect(7, 10, 1, 1, "#2a2238");
                break;
            case "coin":
                p.Rect(3, 2, 6, 8, "#f3c25b"); p.Rect(2, 3, 8, 6, "#f3c25b"); p.Rect(4, 3, 4, 6, "#d9a83a"); p.Rect(5, 4, 2, 4, "#f3c25b");
                p.Rect(3, 3, 1, 2, "#ffe28a");
                break;
            case "spear":
                p.Line(1, 11, 9, 3, "#8a6440"); p.Line(2, 11, 10, 3, "#6b4a2b");
                p.Rect(9, 1, 2, 3, "#c0c5c9"); p.Rect(10, 1, 1, 1, "#ffffff"); p.Rect(8, 3, 1, 1, "#9aa0a5"); p.Rect(10, 4, 1, 1, "#9aa0a5");
                break;
            case "reel":
                p.Rect(3, 3, 6, 6, "#3b3b3b"); p.Rect(4, 2, 4, 8, "#3b3b3b"); p.Rect(4, 4, 4, 4, tint); p.Rect(5, 5, 2, 2, "#ffffff");
                p.Rect(9, 5, 2, 1, "#6b4a2b"); p.Rect(10, 4, 1, 3, "#8a6440"); p.Rect(1, 10, 10, 1, "#9aa0a5");
                break;
            case "line":
                p.Rect(3, 3, 6, 6, "#6b4a2b"); p.Rect(4, 2, 4, 8, "#6b4a2b");
                for (int i = 0; i < 3; i++) p.Rect(3, 4 + i * 2, 6, 1, tint);
                p.Rect(5, 5, 2, 2, "#3b2a1d"); p.Line(9, 4, 11, 1, tint);
                break;
            case "hook":
                p.Rect(6, 1, 1, 7, tint); p.Rect(3, 8, 1, 2, tint); p.Rect(4, 10, 2, 1, tint); p.Rect(6, 8, 1, 2, tint);
                p.Rect(3, 7, 1, 1, tint); p.Rect(5, 1, 3, 1, "#3b3b3b"); p.Rect(7, 2, 1, 1, "#ffffff");
                break;
            case "bobber":
                p.Rect(4, 2, 4, 4, tint); p.Rect(4, 6, 4, 4, "#ffffff"); p.Rect(3, 4, 6, 4, tint); p.Rect(3, 6, 6, 2, "#ffffff");
                p.Rect(5, 0, 2, 2, "#3b3b3b"); p.Rect(5, 10, 2, 2, "#3b3b3b"); p.Rect(4, 3, 1, 1, "#ffffff");
                break;
            case "sinker":
                p.Rect(4, 4, 4, 6, tint); p.Rect(3, 6, 6, 3, tint); p.Rect(5, 2, 2, 2, "#3b3b3b"); p.Rect(4, 5, 1, 2, "#ffffff");
                p.Rect(5, 0, 1, 2, "#dfe9ee");
                break;
            case "lure":
                p.Rect(2, 4, 6, 4, tint); p.Rect(3, 3, 4, 6, tint); p.Rect(8, 5, 2, 2, "#c0c5c9"); p.Rect(10, 4, 1, 4, "#c0c5c9");
                p.Rect(3, 5, 2, 1, "#ffffff"); p.Rect(5, 9, 1, 2, "#9aa0a5"); p.Rect(4, 10, 1, 1, "#9aa0a5");
                break;
            case "fly":
                p.Rect(4, 5, 4, 2, "#5b3a24"); p.Rect(2, 2, 3, 3, "#f2efe6"); p.Rect(7, 2, 3, 3, "#f2efe6"); p.Rect(3, 3, 1, 1, "#d6d1c4");
                p.Rect(8, 7, 1, 3, "#9aa0a5"); p.Rect(7, 9, 1, 1, "#9aa0a5"); p.Rect(2, 7, 2, 1, "#e04b3a");
                break;
            case "sunglasses":
                p.Rect(1, 4, 4, 3, "#1b1b1b"); p.Rect(7, 4, 4, 3, "#1b1b1b"); p.Rect(5, 4, 2, 1, "#1b1b1b");
                p.Rect(2, 5, 1, 1, "#5fa9c9"); p.Rect(8, 5, 1, 1, "#5fa9c9"); p.Rect(0, 4, 1, 1, "#1b1b1b"); p.Rect(11, 4, 1, 1, "#1b1b1b");
                break;
            case "finder":
                p.Rect(2, 1, 8, 10, "#3b3b3b"); p.Rect(3, 2, 6, 6, "#12304a"); p.Rect(4, 5, 2, 1, "#7fd36b"); p.Rect(6, 3, 2, 1, "#f3c25b");
                p.Rect(3, 9, 2, 1, "#e04b3a"); p.Rect(7, 9, 2, 1, "#9aa0a5");
                break;
            case "waders":
                p.Rect(2, 1, 8, 4, "#4c6b45"); p.Rect(2, 5, 3, 5, "#4c6b45"); p.Rect(7, 5, 3, 5, "#4c6b45");
                p.Rect(1, 9, 4, 2, "#2b3a2a"); p.Rect(7, 9, 4, 2, "#2b3a2a"); p.Rect(3, 0, 1, 2, "#8a6440"); p.Rect(8, 0, 1, 2, "#8a6440");
                break;
            case "charm":
                p.Line(3, 1, 6, 4, "#c9a06a"); p.Line(9, 1, 6, 4, "#c9a06a");
                p.Rect(4, 5, 5, 5, "#4f3a7a"); p.Rect(5, 4, 3, 7, "#4f3a7a"); p.Rect(5, 6, 2, 2, "#c9a6ff"); p.Rect(6, 6, 1, 1, "#ffffff");
                break;
            case "headlamp":
                p.Rect(1, 5, 10, 2, "#3b3b3b"); p.Rect(4, 3, 4, 5, "#6e737a"); p.Rect(5, 4, 2, 3, "#ffe28a"); p.Rect(6, 4, 1, 1, "#ffffff");
                p.Rect(8, 2, 1, 1, "#ffe28a"); p.Rect(9, 1, 1, 1, "#ffe28a");
                break;
            case "cooler":
                p.Rect(1, 4, 10, 7, "#2f7fa3"); p.Rect(1, 3, 10, 2, "#f2efe6"); p.Rect(4, 2, 4, 1, "#9aa0a5"); p.Rect(2, 6, 8, 1, "#3a8db0");
                p.Rect(3, 8, 2, 1, "#ffffff");
                break;
            case "worm":
                p.Rect(2, 8, 8, 3, "#6b4a2b"); p.Rect(3, 7, 6, 1, "#8a6440");
                p.Rect(3, 5, 2, 1, "#e8939a"); p.Rect(5, 4, 2, 1, "#e8939a"); p.Rect(7, 5, 1, 2, "#e8939a"); p.Rect(4, 6, 1, 1, "#e8939a"); p.Rect(6, 3, 1, 1, "#d9788e");
                break;
            case "cricket":
                p.Rect(3, 5, 6, 3, "#5b6b2a"); p.Rect(8, 4, 2, 2, "#4a5822"); p.Rect(9, 4, 1, 1, "#1b1b1b");
                p.Line(4, 8, 2, 10, "#4a5822"); p.Line(6, 8, 6, 10, "#4a5822"); p.Line(3, 5, 1, 3, "#4a5822"); p.Rect(4, 6, 3, 1, "#7a8a3a");
                p.Line(9, 4, 11, 1, "#4a5822");
                break;
            case "cutbait":
                p.Rect(2, 6, 3, 3, "#e8939a"); p.Rect(6, 5, 3, 3, "#d9788e"); p.Rect(4, 8, 3, 3, "#e8939a");
                p.Rect(2, 6, 3, 1, "#cfe8ee"); p.Rect(6, 5, 3, 1, "#cfe8ee"); p.Rect(4, 8, 3, 1, "#cfe8ee");
                break;
            case "chum":
                p.Rect(2, 4, 8, 7, "#8a6440"); p.Rect(2, 4, 8, 1, "#b08458"); p.Rect(3, 2, 6, 2, "#c0392b");
                p.Rect(4, 2, 1, 1, "#e8939a"); p.Rect(7, 3, 1, 1, "#e8939a"); p.Rect(4, 6, 4, 2, "#6b4a2b");
                break;
            case "pot":
                p.Rect(2, 4, 8, 7, "#8a6440"); for (int i = 3; i < 10; i += 2) p.Rect(i, 4, 1, 7, "#5b3a24");
                p.Rect(2, 7, 8, 1, "#5b3a24"); p.Rect(5, 1, 2, 3, "#e04b3a"); p.Rect(5, 1, 2, 1, "#ffffff");
                break;
            case "seaweed":
                p.Line(3, 11, 3, 2, "#2f7a3a"); p.Line(6, 11, 7, 1, "#3c9147"); p.Line(9, 11, 8, 4, "#2f7a3a");
                p.Rect(2, 4, 1, 2, "#4caa55"); p.Rect(7, 3, 1, 2, "#4caa55"); p.Rect(9, 6, 1, 2, "#4caa55");
                break;
            case "pearl":
                p.Rect(1, 7, 10, 3, "#b4a3c9"); p.Rect(2, 6, 8, 1, "#d6cbe6"); p.Rect(4, 3, 4, 4, "#f4f0fa"); p.Rect(3, 4, 6, 2, "#f4f0fa");
                p.Rect(4, 3, 1, 1, "#ffffff");
                break;
            case "boot":
                p.Rect(3, 1, 4, 7, "#5b3a24"); p.Rect(3, 7, 8, 3, "#5b3a24"); p.Rect(2, 10, 9, 1, "#3b2516"); p.Rect(4, 2, 1, 4, "#7a5230");
                p.Rect(8, 6, 2, 1, "#6e9a5a");
                break;
            case "smoked":
                FishShape(p, "#9a5f2a"); p.Rect(3, 5, 6, 1, "#6b3a1a"); p.Rect(5, 0, 1, 2, "#c8c2b8"); p.Rect(7, 1, 1, 2, "#c8c2b8");
                break;
            case "jerky":
                p.Line(2, 9, 8, 2, "#8a4a2a"); p.Line(3, 9, 9, 2, "#6b3a1a"); p.Line(5, 10, 10, 4, "#9a5a32"); p.Line(6, 10, 11, 4, "#6b3a1a");
                break;
            case "sushi":
                p.Rect(2, 4, 8, 6, "#1f3a2a"); p.Rect(3, 5, 6, 4, "#f4f0e6"); p.Rect(5, 6, 2, 2, "#e88a6a"); p.Rect(3, 5, 6, 1, "#ffffff");
                break;
            case "maki":
                p.Rect(1, 8, 10, 2, "#c98b3a"); p.Rect(1, 10, 10, 1, "#9a6a2a");
                foreach (int mx in new[] { 1, 4, 7 }) { p.Rect(mx, 4, 3, 4, "#1f3a2a"); p.Rect(mx + 1, 5, 1, 2, "#e88a6a"); }
                break;
            case "crab":
                p.Rect(3, 5, 6, 4, tint); p.Rect(4, 4, 4, 1, tint); p.Rect(1, 3, 2, 2, tint); p.Rect(9, 3, 2, 2, tint);
                p.Rect(2, 5, 1, 1, tint); p.Rect(9, 5, 1, 1, tint); p.Rect(2, 9, 1, 2, tint); p.Rect(9, 9, 1, 2, tint); p.Rect(4, 9, 1, 1, tint); p.Rect(7, 9, 1, 1, tint);
                p.Rect(4, 5, 1, 1, "#10243a"); p.Rect(7, 5, 1, 1, "#10243a");
                break;
            case "lobster":
                p.Rect(3, 4, 3, 6, tint); p.Rect(6, 5, 4, 3, tint); p.Rect(10, 5, 1, 3, tint); p.Rect(1, 2, 2, 3, tint); p.Rect(1, 9, 2, 2, tint);
                p.Rect(2, 5, 1, 1, tint); p.Rect(2, 8, 1, 1, tint); p.Rect(4, 5, 1, 1, "#10243a"); p.Line(5, 4, 8, 1, tint);
                break;
            case "shrimp":
                p.Rect(3, 4, 5, 3, tint); p.Rect(7, 5, 2, 3, tint); p.Rect(8, 8, 2, 2, tint); p.Rect(2, 5, 1, 1, "#10243a");
                p.Line(3, 4, 1, 1, tint); p.Rect(4, 7, 1, 2, tint); p.Rect(6, 7, 1, 2, tint);
                break;
            case "squid":
                p.Rect(4, 1, 4, 6, tint); p.Rect(5, 0, 2, 1, tint); p.Rect(3, 3, 1, 3, tint); p.Rect(8, 3, 1, 3, tint);
                for (int i = 0; i < 4; i++) p.Rect(4 + i, 7 + i % 2, 1, 4 - i % 2, tint);
                p.Rect(5, 5, 1, 1, "#10243a"); p.Rect(6, 5, 1, 1, "#10243a");
                break;
            case "ray":
                p.Rect(2, 5, 8, 2, tint); p.Rect(3, 4, 6, 4, tint); p.Rect(4, 3, 4, 6, tint); p.Line(6, 9, 6, 11, tint);
                p.Rect(5, 4, 1, 1, "#10243a"); p.Rect(6, 4, 1, 1, "#10243a"); p.Rect(4, 6, 4, 1, "#ffffff");
                break;
            case "star":
                p.Rect(5, 1, 2, 3, "#f3c25b"); p.Rect(1, 4, 10, 2, "#f3c25b"); p.Rect(3, 6, 6, 2, "#f3c25b");
                p.Rect(2, 8, 3, 2, "#f3c25b"); p.Rect(7, 8, 3, 2, "#f3c25b"); p.Rect(2, 10, 2, 1, "#f3c25b"); p.Rect(8, 10, 2, 1, "#f3c25b");
                p.Rect(5, 2, 1, 2, "#ffe28a");
                break;
            case "eel":
                p.Rect(1, 6, 3, 2, tint); p.Rect(3, 5, 3, 2, tint); p.Rect(5, 6, 3, 2, tint); p.Rect(7, 5, 3, 2, tint); p.Rect(9, 4, 2, 2, tint);
                p.Rect(10, 4, 1, 1, "#10243a");
                break;
        }
    }

    static void FishShape(Pix p, string c)
    {
        p.Rect(3, 4, 6, 4, c); p.Rect(4, 3, 4, 1, c); p.Rect(4, 8, 4, 1, c);
        p.Rect(1, 3, 2, 2, c); p.Rect(1, 7, 2, 2, c); p.Rect(2, 5, 1, 2, c);
        p.Rect(4, 6, 4, 1, "#ffffff"); p.Rect(7, 5, 1, 1, "#10243a");
    }

    static string Darker(string hex)
    {
        var c = Pal.C(hex);
        return $"#{(int)(c.R * 0.7):x2}{(int)(c.G * 0.7):x2}{(int)(c.B * 0.7):x2}";
    }
}
