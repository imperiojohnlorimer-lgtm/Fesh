using System.Text.Json;

namespace Fesh;

sealed class Flags { public bool metTomas, tideOut, dockFixed, ended; }
#pragma warning disable CS0649 // only ever filled in by loading an old save
sealed class Mats { public int wood, stone; }
#pragma warning restore CS0649
sealed class Build { public string id; public int x, y; }
sealed class Loose { public string kind; public int tx, ty; public float x, y; }
sealed class Req { public string item, bonus; public int count, coins, bonusCount; }

// The player's appearance. Each number is an index into the option lists in Look.cs.
sealed class Look
{
    public string name = "Fisher";
    public int skin = 1, hair, hairColor = 1, hat = 1, shirt, pants;
}

// Everything that is saved between sessions. Field names match the web version's save format.
sealed class State
{
    public List<string> caught = new();
    public Dictionary<string, int> commons = new();
    public Dictionary<string, int> odd = new();
    public bool night;
    public int casts;
    public Flags flags = new();
    public Dictionary<string, int> pity = new();
    public Dictionary<string, bool> hinted = new();
    public float px = 160, py = 115;
    public Mats mats = new(); // only read when loading saves from before the bag existed
    public List<Build> builds = new();
    public List<Loose> loose = new();

    public Look look = new();
    public bool created;
    public Dictionary<string, int> inv = new() { ["rod_old"] = 1 };
    public float food = 100;
    public int day = 1;
    public Dictionary<string, int> felled = new();  // "x,y" of a chopped tree or broken boulder -> day it happened
    public Dictionary<string, int> mined = new();   // unused since cave floors became random; kept so older saves load
    public Dictionary<string, int> picked = new();  // berry bush "x,y" -> day picked
    public Dictionary<string, int> gifts = new();   // animal -> day it last gave a gift
    public Dictionary<string, List<Build>> rooms = new(); // "house:x,y" -> furniture inside that shack
    public string scene = "world";
    public float exitX = 160, exitY = 115;           // where to come back out into the world
    public int coins;
    public Req req;                                  // Tomas's current request, or null
    public int reqDone;
    public string weather = "clear";                 // "clear", "rain" or "storm", rolled each morning
    public Dictionary<string, List<string>> tanks = new(); // aquarium ("house:x,y|tx,ty") -> fish inside
    public string boatAt = "saltmere";               // which jetty the boat is tied up at
    public float hp = 100;
    public int caveDeepest;                          // deepest cave floor reached (unlocks lift stops at 5, 10 and the Ancient Floor)

    // Fishing
    public Dictionary<string, string> tackle = new(); // tackle box slot -> item id ("none" = empty, "auto" for bait); a missing slot uses your best
    public Dictionary<string, float> records = new(); // fish -> heaviest catch in kg
    public Dictionary<string, int> trophies = new();  // fish -> trophy-sized catches
    public Dictionary<string, int> big = new();       // fish -> how many of the ones in your bag are big (Pip pays more)
    public int xp;                                   // fishing experience; the level comes from this
    public int iceDay;                               // the day the ice hole was last drilled open (it freezes over each night)
    public Dictionary<string, int> pots = new();      // crab pot "x,y" -> the day it was set or last hauled
    public int derbyDay, derbyWins;                  // the last day you entered Pip's derby, and how many you've won
    public int chests, perfects;                     // sunken chests opened, perfect hooks

    public bool Caught(string id) => caught.Contains(id);
    public int Pity(string spot) => pity.GetValueOrDefault(spot);
    public bool Hinted(string key) => hinted.GetValueOrDefault(key);
}

static class SaveFile
{
    static readonly JsonSerializerOptions Opts = new() { IncludeFields = true };

    // FESH_SAVE overrides the location, which keeps test runs away from a real save.
    public static string FilePath => Environment.GetEnvironmentVariable("FESH_SAVE") is { Length: > 0 } p ? p
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fesh", "save.json");

    public static void Write(State s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Opts));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception) { /* saving is best effort */ }
    }

    public static State Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var s = JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath), Opts);
            if (s == null) return null;
            s.caught ??= new(); s.commons ??= new(); s.odd ??= new(); s.flags ??= new(); s.pity ??= new(); s.hinted ??= new();
            s.mats ??= new(); s.loose ??= new(); s.look ??= new(); s.inv ??= new(); s.felled ??= new(); s.mined ??= new();
            s.picked ??= new(); s.gifts ??= new(); s.rooms ??= new(); s.scene ??= "world";
            s.tanks ??= new(); s.weather ??= "clear"; s.boatAt ??= "saltmere";
            s.tackle ??= new(); s.records ??= new(); s.trophies ??= new(); s.big ??= new(); s.pots ??= new();
            if (s.req != null && (s.req.item == null || !Items.ById.ContainsKey(s.req.item))) s.req = null;
            s.builds = (s.builds ?? new()).Where(b => b?.id != null && Data.BuildById.ContainsKey(b.id)).ToList();
            foreach (var room in s.rooms.Values) room.RemoveAll(b => b?.id == null || !Data.BuildById.ContainsKey(b.id));
            s.inv = s.inv.Where(kv => Items.ById.ContainsKey(kv.Key) && kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            // Saves from before the bag kept wood and stone separately.
            if (s.mats.wood > 0) s.inv["wood"] = s.inv.GetValueOrDefault("wood") + s.mats.wood;
            if (s.mats.stone > 0) s.inv["stone"] = s.inv.GetValueOrDefault("stone") + s.mats.stone;
            s.mats = new();
            if (!Items.Rods.Any(r => s.inv.ContainsKey(r))) s.inv["rod_old"] = 1;
            s.food = Math.Clamp(s.food, 0, 100);
            s.hp = s.hp <= 0 ? 100 : Math.Clamp(s.hp, 1, 100);
            return s;
        }
        catch (Exception) { return null; }
    }

    public static void Clear()
    {
        try { File.Delete(FilePath); } catch (Exception) { /* ignore */ }
    }
}
