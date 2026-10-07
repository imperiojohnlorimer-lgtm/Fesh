using System.Text.Json;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Player preferences. They belong to the computer, not to a save slot, so they live in settings.json next to the saves.
sealed class SettingsData
{
    public float music = 0.8f, sound = 0.8f;   // volumes, 0..1
    public bool musicOn = true, muted;
    public bool fullscreen;
    public bool shake = true;                  // screen shake on big impacts
    public bool pauseUnfocused = true;         // pause when the game window loses focus
    public int dayLength = 24;                 // real minutes for a whole day and night; 0 stops the clock (only resting moves it)
    public bool clock24;                       // 14:30 instead of 2:30 PM
    public Dictionary<string, int[]> keys = new(); // action -> its two keys (KeyboardKey values, 0 = none); missing actions use the defaults
}

static class Settings
{
    static readonly JsonSerializerOptions Opts = new() { IncludeFields = true, WriteIndented = true };
    public static SettingsData Data = new();
    public static readonly int[] DayLengths = { 12, 24, 36, 48, 0 };

    static string FilePath => SaveFile.SettingsPath;

    public static void Load()
    {
        Data = new SettingsData();
        try
        {
            if (File.Exists(FilePath)) Data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath), Opts) ?? new();
        }
        catch (Exception) { Data = new SettingsData(); }
        Data.keys ??= new();
        Data.music = float.IsFinite(Data.music) ? Math.Clamp(Data.music, 0, 1) : 0.8f;
        Data.sound = float.IsFinite(Data.sound) ? Math.Clamp(Data.sound, 0, 1) : 0.8f;
        if (!DayLengths.Contains(Data.dayLength)) Data.dayLength = 24;
        Bind.Load(Data.keys);
        ApplyAudio();
    }

    // Back to the defaults, forgetting the file (the autotest starts this way).
    public static void Reset()
    {
        try { File.Delete(FilePath); } catch (Exception) { /* ignore */ }
        Data = new SettingsData();
        Bind.Reset();
        ApplyAudio();
    }

    public static void Save()
    {
        Data.keys = Bind.Snapshot();
        try
        {
            Directory.CreateDirectory(SaveFile.Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Opts));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception) { /* best effort, like saving */ }
    }

    public static void ApplyAudio()
    {
        Sfx.Muted = Data.muted;
        Sfx.Volume = Data.sound;
        Music.Enabled = Data.musicOn;
        Music.Volume = Data.music;
    }

    // Real seconds per in-game minute is dayLength * 60 / 1440; this is the other way round. 0 when the clock is stopped.
    public static float GameMinutesPerSecond => Data.dayLength <= 0 ? 0 : 1440f / (Data.dayLength * 60f);
}

// Rebindable controls. Each action has two keys. Esc (menu and back), Enter (confirm) and the number keys
// (picking a build piece) are fixed. Text shown to the player names keys with tokens like <act>, which Fix
// swaps for whatever the key is bound to now (SetPrompt, Toast, dialogue and item descriptions all do this).
static class Bind
{
    public sealed record Act(string Id, string Name, KeyboardKey A, KeyboardKey B = KeyboardKey.Null);

    public static readonly Act[] Acts =
    {
        new("up", "Walk up", KeyboardKey.W, KeyboardKey.Up),
        new("down", "Walk down", KeyboardKey.S, KeyboardKey.Down),
        new("left", "Walk left", KeyboardKey.A, KeyboardKey.Left),
        new("right", "Walk right", KeyboardKey.D, KeyboardKey.Right),
        new("act", "Talk, fish, reel, use", KeyboardKey.E, KeyboardKey.Space),
        new("alt", "Cook, throw chum", KeyboardKey.F),
        new("spear", "Spearfish", KeyboardKey.G),
        new("ride", "Ride your mount", KeyboardKey.R),
        new("bag", "Bag", KeyboardKey.I),
        new("tackle", "Tackle box", KeyboardKey.T),
        new("build", "Build", KeyboardKey.B),
        new("remove", "Take down (building)", KeyboardKey.X),
        new("dex", "Fesh-dex", KeyboardKey.J),
        new("case", "Case board", KeyboardKey.C),
        new("map", "Map", KeyboardKey.Tab),
        new("mute", "Sound on or off", KeyboardKey.M),
        new("fullscreen", "Fullscreen", KeyboardKey.F11)
    };
    public static readonly Dictionary<string, Act> ById = Acts.ToDictionary(a => a.Id);

    static readonly Dictionary<string, KeyboardKey[]> keys = new();

    // Saved keys first, then the defaults for any action the file doesn't mention (one added in a newer version, say),
    // minus any key a saved action already uses, so no key ever ends up doing two things.
    public static void Load(Dictionary<string, int[]> saved)
    {
        keys.Clear();
        var used = new HashSet<KeyboardKey>();
        foreach (var a in Acts)
        {
            if (!saved.TryGetValue(a.Id, out var k) || k is not { Length: 2 } || !k.All(v => v == 0 || Enum.IsDefined(typeof(KeyboardKey), v) && IsBindable((KeyboardKey)v))) continue;
            var ks = k.Select(v => (KeyboardKey)v).Select(x => x != KeyboardKey.Null && !used.Add(x) ? KeyboardKey.Null : x).ToArray();
            keys[a.Id] = ks;
        }
        foreach (var a in Acts)
            if (!keys.ContainsKey(a.Id))
                keys[a.Id] = new[] { a.A, a.B }.Select(x => x != KeyboardKey.Null && !used.Add(x) ? KeyboardKey.Null : x).ToArray();
    }

    public static Dictionary<string, int[]> Snapshot() => keys.ToDictionary(kv => kv.Key, kv => kv.Value.Select(k => (int)k).ToArray());

    public static void Reset() => Load(new());

    public static KeyboardKey Key(string id, int slot) => keys[id][slot];

    // The gamepad layout is fixed. The left stick walks too, and the D-pad and stick both work the chest arrows.
    // Start opens the menu and B backs out (Game.BackPressed); in build mode LB/RB pick the piece and X takes down.
    public static readonly Dictionary<string, GamepadButton> PadButtons = new()
    {
        ["up"] = GamepadButton.LeftFaceUp, ["down"] = GamepadButton.LeftFaceDown, ["left"] = GamepadButton.LeftFaceLeft, ["right"] = GamepadButton.LeftFaceRight,
        ["act"] = GamepadButton.RightFaceDown, ["alt"] = GamepadButton.RightFaceLeft, ["remove"] = GamepadButton.RightFaceLeft, ["bag"] = GamepadButton.RightFaceUp,
        ["tackle"] = GamepadButton.LeftTrigger1, ["map"] = GamepadButton.RightTrigger1, ["build"] = GamepadButton.LeftTrigger2,
        ["ride"] = GamepadButton.RightTrigger2, ["dex"] = GamepadButton.MiddleLeft, ["case"] = GamepadButton.LeftThumb, ["spear"] = GamepadButton.RightThumb
    };
    static int Dir(string id) => id switch { "up" => 0, "right" => 1, "down" => 2, "left" => 3, _ => -1 };

    public static bool Pressed(string id) => keys[id].Any(k => k != KeyboardKey.Null && Inp.Pressed(k))
        || PadButtons.TryGetValue(id, out var b) && Inp.PadPressed(b) || Dir(id) is int d && d >= 0 && Inp.StickPressed(d);
    public static bool Down(string id) => keys[id].Any(k => k != KeyboardKey.Null && Inp.Down(k))
        || PadButtons.TryGetValue(id, out var b) && Inp.PadDown(b) || Dir(id) switch
        {
            0 => Inp.Stick.Y < 0 && -Inp.Stick.Y >= MathF.Abs(Inp.Stick.X) * 0.4f,
            2 => Inp.Stick.Y > 0 && Inp.Stick.Y >= MathF.Abs(Inp.Stick.X) * 0.4f,
            3 => Inp.Stick.X < 0 && -Inp.Stick.X >= MathF.Abs(Inp.Stick.Y) * 0.4f,
            1 => Inp.Stick.X > 0 && Inp.Stick.X >= MathF.Abs(Inp.Stick.Y) * 0.4f,
            _ => false
        };
    // Ignores keys that type a character (letters, digits, space, punctuation), for while a text field has the keyboard.
    public static bool PressedNotTyping(string id) => keys[id].Any(k => k != KeyboardKey.Null && !Typing(k) && Inp.Pressed(k));
    static bool Typing(KeyboardKey k) => (int)k is >= 32 and <= 126 || k >= KeyboardKey.Kp0 && k <= KeyboardKey.KpEqual;

    // Esc, Enter, Backspace (clears a key while rebinding) and the number keys can't be taken.
    public static bool IsBindable(KeyboardKey k) => k is not (KeyboardKey.Null or KeyboardKey.Escape or KeyboardKey.Enter or KeyboardKey.KpEnter
        or KeyboardKey.Backspace) && !(k >= KeyboardKey.One && k <= KeyboardKey.Nine);

    // Puts a key on an action. Whatever had that key before gets this slot's old key instead, so no key does two things.
    public static void Set(string id, int slot, KeyboardKey k)
    {
        var old = keys[id][slot];
        if (k != KeyboardKey.Null)
            foreach (var ks in keys.Values)
                for (int i = 0; i < 2; i++)
                    if (ks[i] == k) ks[i] = old;
        keys[id][slot] = k;
    }

    // The key to show for an action: its first key, or its second if the first is empty.
    // With a gamepad in hand (Inp.UsingPad), its button instead.
    public static string Name(string id)
    {
        if (Inp.UsingPad && PadButtons.TryGetValue(id, out var pb)) return PadName(pb);
        var ks = keys[id];
        var k = ks[0] != KeyboardKey.Null ? ks[0] : ks[1];
        return k == KeyboardKey.Null ? "(unbound)" : KeyName(k);
    }

    // The four walking keys for a sentence: "WASD", or "IJKL" and so on, or "Up/Left/Down/Right" for longer names.
    public static string MoveKeys()
    {
        if (Inp.UsingPad) return "the left stick";
        var names = new[] { "up", "left", "down", "right" }.Select(Name).ToList();
        return names.All(n => n.Length == 1) ? string.Concat(names) : string.Join("/", names);
    }

    public static string PadName(GamepadButton b) => b switch
    {
        GamepadButton.RightFaceDown => "A", GamepadButton.RightFaceRight => "B", GamepadButton.RightFaceLeft => "X", GamepadButton.RightFaceUp => "Y",
        GamepadButton.LeftTrigger1 => "LB", GamepadButton.RightTrigger1 => "RB", GamepadButton.LeftTrigger2 => "LT", GamepadButton.RightTrigger2 => "RT",
        GamepadButton.MiddleLeft => "Back", GamepadButton.MiddleRight => "Start", GamepadButton.LeftThumb => "L3", GamepadButton.RightThumb => "R3",
        GamepadButton.LeftFaceUp => "D-pad up", GamepadButton.LeftFaceDown => "D-pad down", GamepadButton.LeftFaceLeft => "D-pad left",
        GamepadButton.LeftFaceRight => "D-pad right", _ => b.ToString()
    };

    public static string KeyName(KeyboardKey k) => k switch
    {
        KeyboardKey.Null => "",
        KeyboardKey.Space => "Space",
        KeyboardKey.Up => "Up", KeyboardKey.Down => "Down", KeyboardKey.Left => "Left", KeyboardKey.Right => "Right",
        KeyboardKey.LeftShift => "L-Shift", KeyboardKey.RightShift => "R-Shift",
        KeyboardKey.LeftControl => "L-Ctrl", KeyboardKey.RightControl => "R-Ctrl",
        KeyboardKey.LeftAlt => "L-Alt", KeyboardKey.RightAlt => "R-Alt",
        KeyboardKey.LeftSuper => "Win", KeyboardKey.RightSuper => "Win",
        KeyboardKey.Zero => "0", KeyboardKey.Apostrophe => "'", KeyboardKey.Comma => ",", KeyboardKey.Minus => "-",
        KeyboardKey.Period => ".", KeyboardKey.Slash => "/", KeyboardKey.Semicolon => ";", KeyboardKey.Equal => "=",
        KeyboardKey.LeftBracket => "[", KeyboardKey.RightBracket => "]", KeyboardKey.Backslash => "\\", KeyboardKey.Grave => "`",
        KeyboardKey.PageUp => "Page Up", KeyboardKey.PageDown => "Page Down", KeyboardKey.CapsLock => "Caps Lock",
        KeyboardKey.KeyboardMenu => "Menu", KeyboardKey.Insert => "Insert", KeyboardKey.Delete => "Delete",
        >= KeyboardKey.Kp0 and <= KeyboardKey.Kp9 => $"Num {k - KeyboardKey.Kp0}",
        _ => k.ToString()
    };

    // Swaps <act>, <bag>, <move> and so on for the keys they're bound to right now.
    public static string Fix(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            int open = s.IndexOf('<', i), close = open < 0 ? -1 : s.IndexOf('>', open);
            if (close < 0) { sb.Append(s, i, s.Length - i); break; }
            string id = s[(open + 1)..close];
            sb.Append(s, i, open - i);
            sb.Append(id == "move" ? MoveKeys() : keys.ContainsKey(id) ? Name(id) : s[open..(close + 1)]);
            i = close + 1;
        }
        return sb.ToString();
    }
}
