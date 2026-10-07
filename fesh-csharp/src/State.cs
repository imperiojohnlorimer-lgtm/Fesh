using System.Text.Json;

namespace Fesh;

sealed class Flags { public bool metTomas, tideOut, dockFixed, ended; }
#pragma warning disable CS0649 // only ever filled in by loading an old save
sealed class Mats { public int wood, stone; }
#pragma warning restore CS0649
sealed class Build { public string id; public int x, y; }
sealed class Loose { public string kind; public int tx, ty; public float x, y; }
sealed class Req { public string item, bonus; public int count, coins, bonusCount; }
// A stretch of weather in the day's forecast, starting `at` minutes after 06:00 and lasting until the next one.
sealed class WeatherSpell { public int at; public string w; }

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
#pragma warning disable CS0649 // only ever filled in by loading an old save
    public bool night;                               // only read when loading a save from before the clock (then clock is -1)
#pragma warning restore CS0649
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
    public string weather = "clear";                 // "clear", "rain" or "storm" right now; it follows the forecast as the clock runs
    public List<WeatherSpell> forecast;              // today's weather from 06:00, rolled each morning (null in older saves)
    public List<WeatherSpell> tomorrow;              // tomorrow's forecast, rolled a day early so Tomas and Pip can tell you (null in older saves)
    public int toldDay;                              // the day Tomas or Pip last told you tomorrow's weather
    public float clock = -1;                         // minutes since midnight; a day starts at 06:00. -1 in saves from before the clock
    public double playSecs;                          // time played, for the save slot list
    public string savedAt;                           // when this save was last written (local time, round-trip format)
    public string slotName = "";                     // a name for the save slot, given on the slot screen ("" shows the fisher's name)
    public Dictionary<string, List<string>> tanks = new(); // aquarium ("house:x,y|tx,ty") -> fish inside
    public string boatAt = "saltmere";               // which jetty the boat is tied up at
    public bool aboard;                              // steering the boat outdoors
    public float boatX, boatY;                        // 0,0: use the old boatAt jetty (older saves)
    public float hp = 100;
    public int caveDeepest;                          // deepest cave floor reached (unlocks lift stops at 5, 10 and the Ancient Floor)

    // Fishing
    public Dictionary<string, string> tackle = new(); // tackle box slot -> item id ("none" = empty, "auto" for bait); a missing slot uses your best
    public Dictionary<string, float> records = new(); // fish -> heaviest catch in kg
    public Dictionary<string, int> trophies = new();  // fish -> trophy-sized catches
    public Dictionary<string, int> big = new();       // fish -> how many of the ones in your bag are big (Pip pays more)
    public int xp;                                   // fishing experience; the level comes from this
    public int iceDay;                               // the day the ice hole was last drilled open (it freezes over again by the next morning)
    public Dictionary<string, int> pots = new();      // crab pot "x,y" -> the day it was set or last hauled
    public int derbyDay, derbyWins;                  // the last day you entered Pip's derby, and how many you've won
    public int chests, perfects;                     // sunken chests opened, perfect hooks

    // Tidemane, the mount from the Starwell
    public bool tamed;                               // won the fight at the Starwell
    public bool riding;                              // in the saddle right now
    public float mountX, mountY;                     // where Tidemane is waiting outdoors when you're not riding

    // New games and saves from before the clock: day or night becomes a time, and the weather lasts the rest of the day.
    public void FixClock()
    {
        if (!float.IsFinite(clock) || clock < 0 || clock >= 1440) clock = night ? 21 * 60 : 8 * 60;
        if (weather is not ("clear" or "rain" or "storm")) weather = "clear";
        forecast = forecast?.Where(f => f?.w is "clear" or "rain" or "storm" && f.at >= 0 && f.at < 1440).OrderBy(f => f.at).ToList();
        if (forecast is not { Count: > 0 }) forecast = new() { new WeatherSpell { at = 0, w = weather } };
        forecast[0].at = 0;
        tomorrow = tomorrow?.Where(f => f?.w is "clear" or "rain" or "storm" && f.at >= 0 && f.at < 1440).OrderBy(f => f.at).ToList();
        if (tomorrow is not { Count: > 0 } || tomorrow[0].at != 0) tomorrow = null;
    }

    public bool Caught(string id) => caught.Contains(id);
    public int Pity(string spot) => pity.GetValueOrDefault(spot);
    public bool Hinted(string key) => hinted.GetValueOrDefault(key);
}

// Three save slots: save1.json to save3.json in %AppData%\Fesh, next to settings.json. FESH_SAVE (a file path) moves
// all of it, which keeps test runs away from real saves: FESH_SAVE=C:\t\x.json gives C:\t\x1.json to x3.json and
// C:\t\x-settings.json. The single save from before slots (save.json, or the FESH_SAVE file itself) becomes the first
// free slot on startup.
static class SaveFile
{
    static readonly JsonSerializerOptions Opts = new() { IncludeFields = true };
    public const int Slots = 3;
    public static int Slot = 1;   // the slot the game in progress saves to

    static string Override => Environment.GetEnvironmentVariable("FESH_SAVE") is { Length: > 0 } p ? p : null;
    public static string Dir => Override is string p ? Path.GetDirectoryName(Path.GetFullPath(p))
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fesh");
    static string Stem => Override is string p ? Path.GetFileNameWithoutExtension(p) : "save";
    public static string SlotPath(int n) => Path.Combine(Dir, $"{Stem}{n}.json");
    static string LegacyPath => Override ?? Path.Combine(Dir, "save.json");
    // Under FESH_SAVE the settings get their own name too, so a test run can never touch the real settings.json.
    public static string SettingsPath => Path.Combine(Dir, Override != null ? $"{Stem}-settings.json" : "settings.json");
    public static string FilePath => SlotPath(Slot);

    public static void MigrateLegacy()
    {
        try
        {
            if (!File.Exists(LegacyPath)) return;
            for (int n = 1; n <= Slots; n++)
                if (!File.Exists(SlotPath(n))) { File.Move(LegacyPath, SlotPath(n)); return; }
        }
        catch (Exception) { /* leave it where it is */ }
    }

    public static bool Exists(int n) => File.Exists(SlotPath(n));
    public static bool Any() => Enumerable.Range(1, Slots).Any(Exists);
    public static int FirstFree() => Enumerable.Range(1, Slots).FirstOrDefault(n => !Exists(n));

    // The slot written to most recently, for Continue (0 if there are no saves).
    public static int Newest() => Enumerable.Range(1, Slots).Where(Exists).OrderByDescending(n => File.GetLastWriteTimeUtc(SlotPath(n))).FirstOrDefault();

    // Saving is best effort (it happens every few seconds); the menu's Save button reports a failure.
    public static bool Write(State s)
    {
        try
        {
            s.savedAt = DateTime.Now.ToString("o");
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Opts));
            File.Move(tmp, FilePath, true);
            return true;
        }
        catch (Exception) { return false; }
    }

    // Writes a slot again (a new name) without it looking newer, so Continue still picks the same slot. The date goes
    // on the temporary file first, so the slot is either untouched or fully done.
    public static bool Rewrite(int n, State s)
    {
        string path = SlotPath(n), tmp = path + ".tmp";
        try
        {
            var when = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Opts));
            File.SetLastWriteTimeUtc(tmp, when);
            File.Move(tmp, path, true);
            return true;
        }
        catch (Exception) { try { File.Delete(tmp); } catch (Exception) { } return false; }
    }

    // Into an empty slot, dated a shade older than the original so Continue keeps picking the game you were playing.
    public static bool Copy(int from, int to)
    {
        string tmp = SlotPath(to) + ".tmp";
        try
        {
            File.Copy(SlotPath(from), tmp, true);
            File.SetLastWriteTimeUtc(tmp, File.GetLastWriteTimeUtc(SlotPath(from)).AddSeconds(-2));
            File.Move(tmp, SlotPath(to), false);
            return true;
        }
        catch (Exception) { try { File.Delete(tmp); } catch (Exception) { } return false; }
    }

    public static State Read() => Load(FilePath);
    public static State Read(int n) => Load(SlotPath(n));

    public static State Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var s = JsonSerializer.Deserialize<State>(File.ReadAllText(path), Opts);
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
            s.slotName ??= "";
            s.FixClock();
            return s;
        }
        catch (Exception) { return null; }
    }

    public static void Clear(int n)
    {
        try { File.Delete(SlotPath(n)); } catch (Exception) { /* ignore */ }
    }
}
