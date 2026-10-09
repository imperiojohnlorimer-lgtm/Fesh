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

    // Draws a person with their feet at (x, y).
    // Step is 0 standing, or 1-4 through a walk: on 1 and 3 one foot is down and the other leg reaches (the arms swing
    // against the legs), and on 2 and 4 the legs pass under the body, which rides a pixel higher.
    // Bob lifts the upper body (breathing), lean tips it a pixel forward (+) or back (-) in a side view, blink closes the
    // eyes, and head turns only the head (a glance about while standing). Arms: 0 at the sides, 1 one arm waving (swing is
    // the wave frame), 2 both raised overhead, 3 holding a rod out in front (swing pumps it a pixel), 4 reaching for
    // whatever's in hand: the near (or right) arm goes from its shoulder to hand, the other to hand2 (world pixels; see
    // Game.HandsPose). ArmsBehind tucks them behind the body, for a back view with the hands out in front.
    public static void DrawPerson(Pix p, Look look, int x, int y, string face, int step, bool shadow = true, int bob = 0,
        bool blink = false, int arms = 0, int swing = 0, int lean = 0, string head = null,
        (int x, int y)? hand = null, (int x, int y)? hand2 = null, bool armsBehind = false) =>
        DrawFigure(p, Skins[look.skin % Skins.Length], HairColors[look.hairColor % HairColors.Length], Shirts[look.shirt % Shirts.Length],
            Pants[look.pants % Pants.Length], look.hair == 1, look.hat % Hats.Length, null, x, y, face, step, shadow, bob, blink, arms, swing, lean, head,
            hand, hand2, armsBehind);

    // Where the shoulders are for an upper body at (ux, uy): the near and far one in a side view, the right and left
    // one from the front or back. Arms hang from here, so a hand placed relative to them stays on the arm.
    public static (int x, int y) NearShoulder(int ux, int uy, string face) => face switch
    {
        "right" => (ux - 1, uy - 7), "left" => (ux, uy - 7), _ => (ux + 3, uy - 7)
    };
    public static (int x, int y) FarShoulder(int ux, int uy, string face) => face switch
    {
        "right" => (ux + 1, uy - 7), "left" => (ux - 2, uy - 7), _ => (ux - 4, uy - 7)
    };

    // The same, in any colours (Tomas isn't in the creator's palettes). Cap is the cap's colour (hat 3), else the shirt's.
    public static void DrawFigure(Pix p, string skin, string hair, string shirt, string pants, bool longHair, int hat, string cap,
        int x, int y, string face, int step, bool shadow = true, int bob = 0, bool blink = false, int arms = 0, int swing = 0,
        int lean = 0, string head = null, (int x, int y)? hand = null, (int x, int y)? hand2 = null, bool armsBehind = false)
    {
        string farPants = Shade(pants, 0.72f), shirtShade = Shade(shirt, 0.82f), hem = Shade(shirt, 0.9f);
        const string shoe = "#2e2420", farShoe = "#1f1815";
        bool side = face is "left" or "right";
        int dir = face == "left" ? -1 : 1;
        string hf = head ?? face;
        int fo = hf == "left" ? -1 : hf == "right" ? 1 : 0;
        int up = bob + (step is 2 or 4 ? 1 : 0);
        int ux = side ? x + lean * dir : x, uy = y - up;
        // Side views mirror about x - 0.5, the middle of the body. S draws the legs, U the upper body (bob and lean).
        void S(int dx, int dy, int w, int h, string c) => p.Rect(dir > 0 ? x + dx : x - dx - w, y + dy, w, h, c);
        void U(int dx, int dy, int w, int h, string c) => p.Rect(dir > 0 ? ux + dx : ux - dx - w, uy + dy, w, h, c);

        if (shadow) { p.Rect(x - 3, y, 6, 1, "rgba(16,40,44,0.3)"); p.Rect(x - 2, y + 1, 4, 1, "rgba(16,40,44,0.14)"); }

        // Legs, with a darker shoe on each foot so the steps read.
        if (side)
        {
            switch (step)
            {
                case 1: // near leg reaching forward, far leg pushing off behind
                    S(-2, -3, 2, 1, farPants); S(-3, -2, 2, 1, farPants); S(-4, -1, 2, 1, farShoe);
                    S(-1, -3, 2, 1, pants); S(0, -2, 2, 1, pants); S(1, -1, 2, 1, shoe);
                    break;
                case 3:
                    S(-1, -3, 2, 1, farPants); S(0, -2, 2, 1, farPants); S(1, -1, 2, 1, farShoe);
                    S(-2, -3, 2, 1, pants); S(-3, -2, 2, 1, pants); S(-4, -1, 2, 1, shoe);
                    break;
                case 2: // near leg planted, far foot lifting behind it
                    S(-2, -3, 2, 1, farPants); S(-3, -2, 2, 1, farShoe);
                    S(-1, -3, 2, 2, pants); S(-1, -1, 3, 1, shoe);
                    break;
                case 4:
                    S(-1, -3, 2, 2, farPants); S(-1, -1, 3, 1, farShoe);
                    S(-2, -3, 2, 1, pants); S(-3, -2, 2, 1, shoe);
                    break;
                default:
                    S(-2, -3, 1, 2, farPants); S(-2, -1, 1, 1, farShoe);
                    S(-1, -3, 2, 2, pants); S(-1, -1, 3, 1, shoe);
                    break;
            }
            if (up > 0) S(-2, -3 - up, 3, up, pants);
        }
        else
        {
            // Front and back: the leg that isn't taking the weight lifts its foot.
            int liftL = step == 3 ? 1 : 0, liftR = step == 1 ? 1 : 0;
            p.Rect(x - 2, y - 3 - up, 2, 2 - liftL + up, pants); p.Rect(x - 2, y - 1 - liftL, 2, 1, shoe);
            p.Rect(x + 1, y - 3 - up, 2, 2 - liftR + up, pants); p.Rect(x + 1, y - 1 - liftR, 2, 1, shoe);
        }

        // Arms swing against the legs: a stride on the near leg puts the near arm back.
        int armSwing = arms == 0 ? (step == 1 ? -1 : step == 3 ? 1 : swing) : 0;
        if (longHair && hf == "up") p.Rect(ux - 3, uy - 11, 6, 6, hair);
        if (side)
        {
            // Profile: a narrower body with a shaded back, the far arm behind it and the near arm over it.
            string farSkin = Shade(skin, 0.8f);
            if (arms == 4 && hand2 is { } h2) ArmTo(p, FarShoulder(ux, uy, face), h2, Shade(shirt, 0.66f), farSkin);
            if (arms == 3) U(1, -6 + swing, 1, 1, farSkin);
            else if (arms == 0 && armSwing < 0) U(2, -5, 1, 1, farSkin);
            else if (arms == 0 && armSwing > 0) U(-4, -5, 1, 1, farSkin);
            U(-3, -4, 5, 1, pants);
            U(-2, -8, 4, 1, shirt);
            U(-3, -7, 5, 3, shirt);
            U(-3, -7, 1, 3, shirtShade);
            U(-3, -5, 5, 1, hem);
            if (arms == 4) { if (hand is { } h) ArmTo(p, NearShoulder(ux, uy, face), h, shirtShade, skin); }
            else SideArm(U, arms, armSwing, swing, shirtShade, skin);
        }
        else
        {
            if (arms == 4 && armsBehind) ReachArms();
            U(-3, -4, 6, 1, pants);
            U(-2, -8, 4, 1, shirt);
            U(-3, -7, 6, 3, shirt);
            U(2, -7, 1, 3, shirtShade);
            U(-3, -5, 6, 1, hem);
            if (face == "down") U(-1, -8, 2, 1, skin);   // the neckline
            if (arms != 4) FrontArms(p, ux, uy, arms, armSwing, swing, shirtShade, skin);
            else if (!armsBehind) ReachArms();
        }
        // Front and back: the right arm to the hand, the left to the other hand (or hanging).
        void ReachArms()
        {
            if (hand is { } h) ArmTo(p, NearShoulder(ux, uy, face), h, shirtShade, skin);
            if (hand2 is { } h2) ArmTo(p, FarShoulder(ux, uy, face), h2, shirtShade, skin);
            else { p.Rect(ux - 4, uy - 7, 1, 2, shirtShade); p.Rect(ux - 4, uy - 5, 1, 1, skin); }
        }
        if (longHair && hf != "up")
        {
            if (hf is "left" or "right") { p.Rect(hf == "right" ? ux - 3 : ux + 1, uy - 11, 2, 4, hair); p.Rect(hf == "right" ? ux - 3 : ux + 2, uy - 7, 1, 1, hair); }
            else { p.Rect(ux - 3, uy - 11, 1, 5, hair); p.Rect(ux + 2, uy - 11, 1, 5, hair); }
        }
        p.Rect(ux - 2 + fo, uy - 11, 4, 3, hf == "up" ? hair : skin);

        switch (hat)
        {
            case 1: // straw hat
                p.Rect(ux - 3, uy - 13, 6, 2, "#f3c25b");
                p.Rect(ux - 4, uy - 11, 8, 1, "#d9a83a");
                break;
            case 2: // beanie in the shirt colour
                p.Rect(ux - 2, uy - 14, 4, 1, shirt);
                p.Rect(ux - 3, uy - 13, 6, 2, shirt);
                p.Rect(ux - 3, uy - 11, 6, 1, Shade(shirt, 0.7f));
                p.Rect(ux, uy - 15, 1, 1, "#ffffff");
                break;
            case 3: // cap with a brim facing forward
                p.Rect(ux - 3, uy - 13, 6, 2, cap ?? shirt);
                if (hf == "right") p.Rect(ux + 2, uy - 11, 3, 1, Shade(cap ?? shirt, 0.7f));
                else if (hf == "left") p.Rect(ux - 5, uy - 11, 3, 1, Shade(cap ?? shirt, 0.7f));
                else if (hf == "down") p.Rect(ux - 3, uy - 11, 6, 1, Shade(cap ?? shirt, 0.7f));
                break;
            default: // hair only
                p.Rect(ux - 2, uy - 13, 4, 1, hair);
                p.Rect(ux - 3, uy - 12, 6, 1, hair);
                if (hf != "up") p.Rect(ux - 2 + fo, uy - 11, 4, 1, hair);
                p.Rect(ux - 3, uy - 11, 1, 1, hair); p.Rect(ux + 2, uy - 11, 1, 1, hair);
                break;
        }
        if (blink) return;
        if (hf == "down") { p.Rect(ux - 1, uy - 10, 1, 1, "#1b1b1b"); p.Rect(ux + 1, uy - 10, 1, 1, "#1b1b1b"); }
        else if (hf == "left") p.Rect(ux - 2, uy - 10, 1, 1, "#1b1b1b");
        else if (hf == "right") p.Rect(ux + 1, uy - 10, 1, 1, "#1b1b1b");
    }

    // An arm from its shoulder to the hand: a sleeve, then the hand itself.
    static void ArmTo(Pix p, (int x, int y) s, (int x, int y) h, string sleeve, string skin)
    {
        if (s != h) p.Line(s.x, s.y, h.x, h.y, sleeve);
        p.Rect(h.x, h.y, 1, 1, skin);
    }

    // The near arm in a side view (U mirrors it for facing left): hanging, swinging forward (+1) or back (-1),
    // holding a rod out in front, or the two-arm and waving poses.
    static void SideArm(Action<int, int, int, int, string> U, int arms, int armSwing, int pump, string sleeve, string skin)
    {
        if (arms == 2) { U(-1, -11, 1, 4, sleeve); U(-1, -12, 1, 1, skin); return; }
        if (arms == 3) { U(0, -7, 1, 1, sleeve); U(1, -6 + pump, 1, 1, sleeve); U(2, -6 + pump, 1, 1, skin); return; }
        if (arms == 1) { U(0, -9, 1, 2, sleeve); U(pump & 1, -10, 1, 1, skin); return; }
        if (armSwing > 0) { U(0, -7, 1, 1, sleeve); U(1, -6, 1, 1, sleeve); U(2, -5, 1, 1, skin); }
        else if (armSwing < 0) { U(-2, -7, 1, 1, sleeve); U(-3, -6, 1, 1, sleeve); U(-4, -5, 1, 1, skin); }
        else { U(-1, -7, 1, 2, sleeve); U(-1, -5, 1, 1, skin); }
    }

    // Front and back views: an arm each side. A swing lifts one hand and lowers the other.
    static void FrontArms(Pix p, int x, int u, int arms, int armSwing, int pump, string sleeve, string skin)
    {
        if (arms == 2)
        {
            foreach (int ax in new[] { x - 4, x + 3 }) { p.Rect(ax, u - 11, 1, 4, sleeve); p.Rect(ax, u - 12, 1, 1, skin); }
            return;
        }
        void Arm(int ax, int drop)
        {
            p.Rect(ax, u - 7, 1, 2 + drop, sleeve);
            p.Rect(ax, u - 5 + drop, 1, 1, skin);
        }
        if (arms == 3)
        {
            // The rod is held out to the right, both hands on it.
            Arm(x - 4, 0);
            p.Rect(x + 3, u - 7, 1, 1, sleeve);
            p.Rect(x + 2, u - 6 + pump, 1, 1, skin);
            return;
        }
        if (arms == 1)
        {
            Arm(x - 4, 0);
            p.Rect(x + 3, u - 10, 1, 3, sleeve);
            p.Rect(x + 3 + (pump & 1), u - 11, 1, 1, skin);
            return;
        }
        Arm(x - 4, armSwing > 0 ? 1 : armSwing < 0 ? -1 : 0);
        Arm(x + 3, armSwing < 0 ? 1 : armSwing > 0 ? -1 : 0);
    }
}
