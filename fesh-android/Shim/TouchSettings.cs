// The player's choices for the on-screen controls, kept in a small text file in the app's own folder.
using System.Globalization;

namespace Fesh.Droid;

internal static class TouchSettings
{
    // The values live in the game (Platform), so its Settings screen can show and change them.
    public static bool FixedStick { get => Platform.FixedStick; set => Platform.FixedStick = value; }
    public static float StickScale { get => Platform.StickScale; set => Platform.StickScale = value; }
    public static float ButtonScale { get => Platform.ButtonScale; set => Platform.ButtonScale = value; }
    public static float Opacity { get => Platform.Opacity; set => Platform.Opacity = value; }
    public static bool SwapSides { get => Platform.SwapSides; set => Platform.SwapSides = value; }
    public static bool Haptics { get => Platform.Haptics; set => Platform.Haptics = value; }
    public static int Fps { get => Platform.Fps; set => Platform.Fps = value; }
    public static bool ShowDebug { get => Platform.ShowDebug; set => Platform.ShowDebug = value; }
    // Where the player has dragged a control to: distance in layout units from its side edge and from the bottom.
    public static readonly Dictionary<string, (float ax, float ay)> Pos = new();

    static string path;

    public static void Load(string dir)
    {
        path = Path.Combine(dir, "touch-controls.txt");
        try
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line[..eq].Trim(), v = line[(eq + 1)..].Trim();
                float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
                switch (k)
                {
                    case "fixedStick": FixedStick = v == "1"; break;
                    case "stickScale": StickScale = Math.Clamp(F(v), 0.6f, 1.6f); break;
                    case "buttonScale": ButtonScale = Math.Clamp(F(v), 0.6f, 1.6f); break;
                    case "opacity": Opacity = Math.Clamp(F(v), 0.25f, 1f); break;
                    case "swapSides": SwapSides = v == "1"; break;
                    case "haptics": Haptics = v == "1"; break;
                    case "fps": Fps = v == "30" ? 30 : 60; break;
                    case "debug": ShowDebug = v == "1"; break;
                    default:
                        if (k.StartsWith("pos.")) { var p = v.Split(','); Pos[k[4..]] = (F(p[0]), F(p[1])); }
                        break;
                }
            }
        }
        catch { }   // a damaged file just means the defaults
    }

    public static void Save()
    {
        if (path == null) return;
        try
        {
            var inv = CultureInfo.InvariantCulture;
            var lines = new List<string>
            {
                "fixedStick=" + (FixedStick ? 1 : 0), "stickScale=" + StickScale.ToString(inv), "buttonScale=" + ButtonScale.ToString(inv),
                "opacity=" + Opacity.ToString(inv), "swapSides=" + (SwapSides ? 1 : 0), "haptics=" + (Haptics ? 1 : 0), "fps=" + Fps, "debug=" + (ShowDebug ? 1 : 0),
            };
            foreach (var (k, (ax, ay)) in Pos) lines.Add($"pos.{k}={ax.ToString(inv)},{ay.ToString(inv)}");
            File.WriteAllLines(path, lines);
        }
        catch { }
    }

    public static void Reset()
    {
        FixedStick = false; StickScale = 1; ButtonScale = 1; Opacity = 0.8f; SwapSides = false; Haptics = true; Fps = 60; ShowDebug = false;
        Pos.Clear();
        Save();
    }
}
