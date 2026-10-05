using Raylib_cs;

namespace Fesh;

static class LookData
{
    public static readonly string[] Skins = { "#f6d3b3", "#f1c27d", "#d9a066", "#a9714b", "#6b4428" };
    public static readonly string[] HairColors = { "#2b1d14", "#6b4a2b", "#c98b3a", "#e8d27a", "#b5523b", "#e8e8e8" };
    public static readonly string[] Shirts = { "#e04b3a", "#2f7fa3", "#4c9a45", "#f3c25b", "#8a5fb5", "#2b3a4a", "#e8e8e8" };
    public static readonly string[] Pants = { "#2b3a4a", "#5b3a24", "#3f5d8a", "#5e6468", "#1b1b1b" };
    public static readonly string[] Hats = { "No hat", "Straw hat", "Beanie", "Cap" };
    public static readonly string[] Hairs = { "Short", "Long" };

    public static Look Random(Random r) => new()
    {
        skin = r.Next(Skins.Length), hair = r.Next(Hairs.Length), hairColor = r.Next(HairColors.Length),
        hat = r.Next(Hats.Length), shirt = r.Next(Shirts.Length), pants = r.Next(Pants.Length)
    };

    static string Shade(string hex, float k)
    {
        var c = Pal.C(hex);
        return $"#{(int)Math.Min(255, c.R * k):x2}{(int)Math.Min(255, c.G * k):x2}{(int)Math.Min(255, c.B * k):x2}";
    }

    // Draws a person standing with their feet at (x, y). Step 0 or 1 is the walking frame.
    // Bob lifts the upper body a pixel (breathing, or the bounce of a step), blink closes the eyes, swing (-1, 0, 1)
    // swings the arms, and arms is 0 at their sides, 1 one arm waving (swing is the wave frame), 2 both raised overhead.
    public static void DrawPerson(Pix p, Look look, int x, int y, string face, int step, bool shadow = true, int bob = 0, bool blink = false, int arms = 0, int swing = 0)
    {
        string skin = Skins[look.skin % Skins.Length], hair = HairColors[look.hairColor % HairColors.Length];
        string shirt = Shirts[look.shirt % Shirts.Length], pants = Pants[look.pants % Pants.Length];
        bool longHair = look.hair == 1;
        int hat = look.hat % Hats.Length;
        int fo = face == "left" ? -1 : face == "right" ? 1 : 0;

        if (shadow) p.Rect(x - 3, y, 6, 1, "rgba(0,0,0,0.22)");
        p.Rect(x - 2, y - 3 + step, 2, 3 - step, pants);
        p.Rect(x + 1, y - 3 + (1 - step), 2, 3 - (1 - step), pants);
        y -= bob;   // everything above the legs
        if (bob > 0) { p.Rect(x - 2, y - 3 + step, 2, 1, pants); p.Rect(x + 1, y - 3 + (1 - step), 2, 1, pants); }
        if (longHair && face == "up") p.Rect(x - 3, y - 11, 6, 6, hair);
        p.Rect(x - 3, y - 8, 6, 5, shirt);
        p.Rect(x - 3, y - 5, 6, 1, Shade(shirt, 0.7f));
        Arms(p, x, y, face, arms, swing, shirt, skin);
        if (longHair && face != "up") { p.Rect(x - 3, y - 11, 1, 5, hair); p.Rect(x + 2, y - 11, 1, 5, hair); }
        p.Rect(x - 2 + fo, y - 11, 4, 3, face == "up" ? hair : skin);

        switch (hat)
        {
            case 1: // straw hat
                p.Rect(x - 3, y - 13, 6, 2, "#f3c25b");
                p.Rect(x - 4, y - 11, 8, 1, "#d9a83a");
                break;
            case 2: // beanie in the shirt colour
                p.Rect(x - 2, y - 14, 4, 1, shirt);
                p.Rect(x - 3, y - 13, 6, 2, shirt);
                p.Rect(x - 3, y - 11, 6, 1, Shade(shirt, 0.7f));
                p.Rect(x, y - 15, 1, 1, "#ffffff");
                break;
            case 3: // cap with a brim facing forward
                p.Rect(x - 3, y - 13, 6, 2, shirt);
                if (face == "right") p.Rect(x + 2, y - 11, 3, 1, Shade(shirt, 0.7f));
                else if (face == "left") p.Rect(x - 5, y - 11, 3, 1, Shade(shirt, 0.7f));
                else if (face == "down") p.Rect(x - 3, y - 11, 6, 1, Shade(shirt, 0.7f));
                break;
            default: // hair only
                p.Rect(x - 2, y - 13, 4, 1, hair);
                p.Rect(x - 3, y - 12, 6, 1, hair);
                if (face != "up") p.Rect(x - 2 + fo, y - 11, 4, 1, hair);
                p.Rect(x - 3, y - 11, 1, 1, hair); p.Rect(x + 2, y - 11, 1, 1, hair);
                break;
        }
        if (blink) return;
        if (face == "down") { p.Rect(x - 1, y - 10, 1, 1, "#1b1b1b"); p.Rect(x + 1, y - 10, 1, 1, "#1b1b1b"); }
        else if (face == "left") p.Rect(x - 2, y - 10, 1, 1, "#1b1b1b");
        else if (face == "right") p.Rect(x + 1, y - 10, 1, 1, "#1b1b1b");
    }

    // Sleeves in a slightly darker shirt colour with a hand at the end.
    static void Arms(Pix p, int x, int u, string face, int arms, int swing, string shirt, string skin)
    {
        string sleeve = Shade(shirt, 0.82f);
        if (arms == 2)
        {
            foreach (int ax in new[] { x - 4, x + 3 }) { p.Rect(ax, u - 11, 1, 4, sleeve); p.Rect(ax, u - 12, 1, 1, skin); }
            return;
        }
        if (face is "left" or "right")
        {
            int dir = face == "right" ? 1 : -1, ax = x + (dir > 0 ? 0 : -1) + swing * dir;
            p.Rect(ax, u - 8, 1, 3, sleeve);
            p.Rect(ax, u - 5, 1, 1, skin);
            return;
        }
        p.Rect(x - 4, u - 8 + Math.Max(0, swing), 1, 3, sleeve);
        p.Rect(x - 4, u - 5 + Math.Max(0, swing), 1, 1, skin);
        if (arms == 1)
        {
            p.Rect(x + 3, u - 10, 1, 3, sleeve);
            p.Rect(x + 3 + (swing & 1), u - 11, 1, 1, skin);
        }
        else
        {
            p.Rect(x + 3, u - 8 + Math.Max(0, -swing), 1, 3, sleeve);
            p.Rect(x + 3, u - 5 + Math.Max(0, -swing), 1, 1, skin);
        }
    }
}
