namespace Fesh;

// The wardrobe (1.46): cosmetics for your fisher, your banca and your rod. Nothing here changes how anything plays. A
// cosmetic has a permanent id, a slot and a value (a colour; for a hat, a style and a colour), and one way to get it:
//   shop       Pip's Dress stall, bought once with coins (state.cosmetics keeps what you bought)
//   ach:<id>   earning an Angler's Ledger achievement
//   friend:<id>:<hearts>   reaching that many hearts with a villager
//   mastery:<n>            reaching fishing mastery n (Mastery.cs)
// Earned ones are worked out from the save every time, so an older save has what it already deserves and nothing is paid
// twice. What you wear is stored by id (Look.shirtId and so on, State.hullId, State.rodId); an id you don't own, or one
// this version doesn't know, is simply not worn (ownership ids are kept, so a newer save keeps its things).
sealed record Cosmetic(string Id, string Slot, string Name, string Value, int Price, string Unlock, int Style = 0)
{
    public bool InShop => Unlock == "shop";
}

static class Wardrobe
{
    public static readonly string[] Slots = { "shirt", "pants", "hair", "hat", "hull", "rod" };
    public static string SlotName(string slot) => slot switch { "shirt" => "Shirt", "pants" => "Trousers", "hair" => "Hair colour", "hat" => "Hat", "hull" => "Boat trim", "rod" => "Rod", _ => slot };

    // Hat styles 4, 5 and 6 (Look.cs draws them); their default colours when none is chosen (a visitor on the dock).
    public static readonly string[] HatNames = { "No hat", "Straw hat", "Beanie", "Cap", "Bandana", "Bucket hat", "Salakot" };
    public static readonly string[] HatDefault = { null, null, null, null, "#c0392b", "#b8a574", "#d7b66a" };

    public static readonly Cosmetic[] Catalog =
    {
        // Dyes and cloth at Pip's, in three prices.
        new("shirt_teal", "shirt", "Teal shirt", "#1f8a8a", 150, "shop"),
        new("shirt_coral", "shirt", "Coral shirt", "#e8735a", 150, "shop"),
        new("shirt_mustard", "shirt", "Mustard shirt", "#c9962a", 150, "shop"),
        new("shirt_indigo", "shirt", "Indigo shirt", "#3a3f8f", 200, "shop"),
        new("pants_khaki", "pants", "Khaki trousers", "#a08a5a", 150, "shop"),
        new("pants_moss", "pants", "Moss trousers", "#4a6a3a", 150, "shop"),
        new("pants_maroon", "pants", "Maroon trousers", "#6a2a3a", 200, "shop"),
        new("hair_copper", "hair", "Copper hair", "#b4552d", 200, "shop"),
        new("hair_sun", "hair", "Sun-bleached hair", "#f0c040", 200, "shop"),
        new("hair_ocean", "hair", "Ocean-blue hair", "#2a7fa8", 300, "shop"),
        new("hat_bandana_red", "hat", "Red bandana", "#c0392b", 200, "shop", 4),
        new("hat_bucket_khaki", "hat", "Khaki bucket hat", "#b8a574", 300, "shop", 5),
        new("hat_salakot", "hat", "Salakot", "#d7b66a", 450, "shop", 6),
        new("hull_red", "hull", "Red boat trim", "#c0392b", 200, "shop"),
        new("hull_green", "hull", "Green boat trim", "#3f8a4a", 200, "shop"),
        new("hull_violet", "hull", "Violet boat trim", "#7a4fa8", 250, "shop"),
        new("rod_red", "rod", "Red rod", "#c0392b", 120, "shop"),
        new("rod_bamboo", "rod", "Bamboo rod", "#c8b060", 120, "shop"),
        new("rod_black", "rod", "Black rod", "#2b2b30", 150, "shop"),

        // Earned: the Ledger, friends and mastery.
        new("shirt_ivory", "shirt", "Ivory shirt", "#efe6d0", 0, "ach:fish100"),
        new("shirt_sunset", "shirt", "Sunset shirt", "#f08a3c", 0, "ach:amihan"),
        new("shirt_sea", "shirt", "Sea-blue shirt", "#2a6f97", 0, "mastery:2"),
        new("shirt_gold", "shirt", "Gold shirt", "#e0b030", 0, "ach:master1"),
        new("pants_denim", "pants", "Denim trousers", "#3a5f9a", 0, "ach:cave5"),
        new("hair_rose", "hair", "Rose hair", "#c9647a", 0, "friend:lira:5"),
        new("hair_silver", "hair", "Silver hair", "#bfc7cc", 0, "mastery:5"),
        new("hat_bandana_blue", "hat", "Blue bandana", "#2f6fa3", 0, "friend:niko:3", 4),
        new("hat_bucket_navy", "hat", "Navy bucket hat", "#2b3a5a", 0, "mastery:8", 5),
        new("hull_gold", "hull", "Gold boat trim", "#d9a43a", 0, "ach:regatta"),
        new("hull_white", "hull", "White boat trim", "#eeeeee", 0, "mastery:10"),
        new("rod_teal", "rod", "Teal rod", "#2a9a9a", 0, "mastery:5"),
        new("rod_gold", "rod", "Gold rod", "#e0b030", 0, "ach:gold1"),
    };

    public static readonly Dictionary<string, Cosmetic> ById = Catalog.ToDictionary(c => c.Id);

    // The colour of a cosmetic (null when the id is missing or unknown).
    public static string ValueOf(string id) => id != null && ById.TryGetValue(id, out var c) ? c.Value : null;
    public static Cosmetic Find(string id) => id != null && ById.TryGetValue(id, out var c) ? c : null;
    public static IEnumerable<Cosmetic> InSlot(string slot) => Catalog.Where(c => c.Slot == slot);

    // The one that mastery n (or an achievement, or a friend) unlocks, for the words on the Ledger.
    public static Cosmetic UnlockedBy(string key) => Catalog.FirstOrDefault(c => c.Unlock == key);
}

partial class Game
{
    static Cosmetic CosmeticUnlockedBy(string key) => Wardrobe.UnlockedBy(key);

    // Owned: bought, or earned by what the save already shows.
    bool Owns(Cosmetic c)
    {
        if (c == null) return false;
        if (state.cosmetics?.Contains(c.Id) == true) return true;
        return !c.InShop && UnlockMet(c.Unlock);
    }
    bool Owns(string id) => Wardrobe.Find(id) is Cosmetic c && Owns(c);

    bool UnlockMet(string key)
    {
        var p = key.Split(':');
        switch (p[0])
        {
            case "ach": return state.Hinted("ach:" + p[1]);
            case "mastery": return int.TryParse(p[1], out int n) && MasteryLevel >= n;
            case "friend": return int.TryParse(p[2], out int h) && Hearts(p[1]) >= h;
            default: return false;
        }
    }

    string UnlockText(Cosmetic c)
    {
        if (c.InShop) return $"Pip's Dress stall, {c.Price} coins";
        var p = c.Unlock.Split(':');
        return p[0] switch
        {
            "ach" => Achievements.FirstOrDefault(a => a.Id == p[1]) is Ach a ? $"Earn \"{a.Name}\" in the Angler's Ledger" : "Earn it in the Angler's Ledger",
            "mastery" => $"Reach fishing mastery {p[1]}",
            "friend" => FriendById.TryGetValue(p[1], out var fr) ? $"Reach {p[2]} hearts with {fr.Name}" : "Make a friend",
            _ => ""
        };
    }

    // Buying: once, atomically (the coins and the ownership change together, then it's saved).
    bool BuyCosmetic(Cosmetic c)
    {
        if (c == null || !c.InShop || Owns(c)) return false;
        if (state.coins < c.Price) { Sfx.Play("nope"); Toast("Not enough coins."); return false; }
        state.coins -= c.Price;
        state.cosmetics ??= new();
        state.cosmetics.Add(c.Id);
        Sfx.Play("coin");
        Save();
        Toast($"You buy the {c.Name.ToLowerInvariant()}. It's in your wardrobe.");
        return true;
    }

    // Anything worn that isn't yours (a save edited by hand, an id from a newer version) comes off; the rest stays.
    void FixWardrobe()
    {
        if (state?.look == null) return;
        var l = state.look;
        string Keep(string id, string slot) => Wardrobe.Find(id) is Cosmetic c && c.Slot == slot && Owns(c) ? id : null;
        l.shirtId = Keep(l.shirtId, "shirt"); l.pantsId = Keep(l.pantsId, "pants"); l.hairId = Keep(l.hairId, "hair"); l.hatId = Keep(l.hatId, "hat");
        // A hat style the creator doesn't offer needs the hat that gives it.
        if (l.hat >= 4 && l.hatId == null) l.hat = 0;
        if (l.hat > 6) l.hat = 0;
        state.hullId = Keep(state.hullId, "hull");
        state.rodId = Keep(state.rodId, "rod");
    }

    // What the boat's trim and your rod are painted, if you've dressed them.
    string HullHex => Wardrobe.ValueOf(state?.hullId);
    string RodHex => Wardrobe.ValueOf(state?.rodId);
}
