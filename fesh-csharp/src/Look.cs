using Raylib_cs;

namespace Fesh;

static class LookData
{
    public static readonly string[] Skins = { "#f6d3b3", "#f1c27d", "#d9a066", "#a9714b", "#6b4428" };
    public static readonly string[] HairColors = { "#2b1d14", "#6b4a2b", "#c98b3a", "#e8d27a", "#b5523b", "#e8e8e8" };
    public static readonly string[] Shirts = { "#e04b3a", "#2f7fa3", "#4c9a45", "#f3c25b", "#8a5fb5", "#2b3a4a", "#e8e8e8" };
    public static readonly string[] Pants = { "#2b3a4a", "#5b3a24", "#3f5d8a", "#5e6468", "#1b1b1b" };
    // The creator offers the first four hats; the rest are wardrobe cosmetics (Wardrobe.cs), drawn for anyone wearing them.
    public static readonly string[] Hats = Wardrobe.HatNames;
    public static readonly string[] CreatorHats = Hats.Take(4).ToArray();
    public static readonly string[] Hairs = { "Short", "Long" };

    // What a person actually wears: a wardrobe cosmetic if they have one on (and it is the right kind), else the creator's colour.
    public static string ShirtHex(Look l) => Wardrobe.Find(l.shirtId) is { Slot: "shirt" } c ? c.Value : Shirts[l.shirt % Shirts.Length];
    public static string PantsHex(Look l) => Wardrobe.Find(l.pantsId) is { Slot: "pants" } c ? c.Value : Pants[l.pants % Pants.Length];
    public static string HairHex(Look l) => Wardrobe.Find(l.hairId) is { Slot: "hair" } c ? c.Value : HairColors[l.hairColor % HairColors.Length];
    // A hat's own colour (hats 4 to 6 only; the creator's three take the shirt's or the cap's).
    public static string CapHex(Look l) => l.hat is >= 4 and < 7 ? (Wardrobe.Find(l.hatId) is { Slot: "hat" } c ? c.Value : Wardrobe.HatDefault[l.hat]) : null;
    public static Look Random(Random r) => new()
    {
        skin = r.Next(Skins.Length), hair = r.Next(Hairs.Length), hairColor = r.Next(HairColors.Length),
        hat = r.Next(CreatorHats.Length), shirt = r.Next(Shirts.Length), pants = r.Next(Pants.Length)
    };

    // A shade of a colour: darker, and pulled a little towards deep blue, the way the world's own shadows are (the oaks' shade is
    // bluer than their light), so a person's shadows sit in the same light as the trees and the sand.
    static string Shade(string hex, float k)
    {
        var c = Pal.C(hex);
        float t = Math.Clamp((1 - k) * 0.3f, 0, 0.3f);
        float r = c.R * k * (1 - t) + 26 * t, g = c.G * k * (1 - t) + 42 * t, b = c.B * k * (1 - t) + 80 * t;
        return $"#{(int)Math.Min(255, r):x2}{(int)Math.Min(255, g):x2}{(int)Math.Min(255, b):x2}";
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
        (int x, int y)? hand = null, (int x, int y)? hand2 = null, bool armsBehind = false, bool idle = true, bool seated = false) =>
        DrawFigure(p, Skins[look.skin % Skins.Length], HairHex(look), ShirtHex(look),
            PantsHex(look), look.hair == 1, look.hat % Hats.Length, CapHex(look), x, y, face, step, shadow, bob, blink, arms, swing, lean, head,
            hand, hand2, armsBehind, idle, seated);

    // Where the shoulders are for an upper body at (ux, uy): the near and far one in a side view, the right and left
    // one from the front or back. Arms hang from here, so a hand placed relative to them stays on the arm.
    // How much taller than the first drawing a person stands (the legs are longer; the head, body and arms are as they were), so
    // they stand properly beside the trees. The shoulders sit this much higher than uy - 7.
    public const int Stature = 2;
    public static (int x, int y) NearShoulder(int ux, int uy, string face) => face switch
    {
        "right" => (ux - 1, uy - 7 - Stature), "left" => (ux, uy - 7 - Stature), _ => (ux + 3, uy - 7 - Stature)
    };
    public static (int x, int y) FarShoulder(int ux, int uy, string face) => face switch
    {
        "right" => (ux + 1, uy - 7 - Stature), "left" => (ux - 2, uy - 7 - Stature), _ => (ux - 4, uy - 7 - Stature)
    };

    // The game's clock, for people blinking on their own (0 outside the game, so sprite sheets never blink).
    public static float Now;
    public static bool AutoBlink = true;

    static string Blend(string a, string b, float k)
    {
        var c = Pal.C(a); var d = Pal.C(b);
        return $"#{(int)(c.R + (d.R - c.R) * k):x2}{(int)(c.G + (d.G - c.G) * k):x2}{(int)(c.B + (d.B - c.B) * k):x2}";
    }

    // A colour lightened towards white by k (for highlights; Shade only scales, which does nothing for light colours).
    static string Lighten(string hex, float k)
    {
        var c = Pal.C(hex);
        return $"#{(int)(c.R + (255 - c.R) * k):x2}{(int)(c.G + (255 - c.G) * k):x2}{(int)(c.B + (255 - c.B) * k):x2}";
    }

    // The creator's colours are clean and bright (that is what made people look like painted plastic): worn cloth is a little
    // duller and warmer than its dye, so every cloth colour is pulled gently towards an earthy grey-brown.
    static string Natural(string hex) => Blend(hex, "#8a7a66", 0.17f);

    // Skin in shadow goes warm and a little red, not grey or blue.
    static string SkinShade(string skin, float k) => Blend(Shade(skin, k), "#9a4a34", 0.16f);

    // Fills a rectangle a pixel at a time: each pixel a touch lighter or darker by a hash (cloth, hair and skin are never one flat
    // colour), and darker across it from left to right by `grad` (the light is from the left), so there are no hard bands.
    static void Tex(Pix p, int x, int y, int w, int h, string hex, float amp = 0.06f, float grad = 0f)
    {
        var c = Pal.C(hex);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++)
            {
                float j = ((float)Pix.Hash(x + xx, y + yy, 905) - 0.5f) * 2 * amp;
                float k = 1 + j - (w > 1 ? grad * xx / (w - 1) : 0);
                p.Rect(x + xx, y + yy, 1, 1, new Color((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), (byte)255));
            }
    }

    // A person with their feet at (x, y), in any colours (Tomas isn't in the creator's palettes). Cap is the cap's colour (hat 3),
    // else the shirt's. (1.29: not a block any more: the body narrows at the waist so the arms hang clear of it, it twists a
    // little as it walks, the head sits over the foot taking the weight and long hair swings; the colours are softened and
    // textured so nothing is flat and shiny.) The anchors other code relies on are unchanged: the head rows, the hat rows,
    // the shoulders.
    public static void DrawFigure(Pix p, string skin, string hair, string shirt, string pants, bool longHair, int hat, string cap,
        int x, int y, string face, int step, bool shadow = true, int bob = 0, bool blink = false, int arms = 0, int swing = 0,
        int lean = 0, string head = null, (int x, int y)? hand = null, (int x, int y)? hand2 = null, bool armsBehind = false, bool idle = true, bool seated = false)
    {
        // Standing about, everyone blinks and now and then glances aside, each on their own beat (they already breathe
        // through the bob their callers pass; you do all this yourself: Game.DrawPlayer passes idle false).
        if (idle && step == 0 && Now > 0 && AutoBlink)
        {
            int seed = (shirt.Length * 31 + skin[1] * 7 + hair[2] * 13 + pants[3] * 3) & 255;
            float beat = 3.4f + seed / 255f * 2.2f;
            if (!blink) blink = (Now + beat * 1.7f) % beat < 0.12f;
            float look = (Now * 0.5f + seed * 0.37f) % 7.5f;
            if (head == null && arms == 0 && face == "down" && look < 0.7f) head = seed % 2 == 0 ? "left" : "right";
        }
        shirt = Natural(shirt); pants = Natural(pants); if (cap != null) cap = Natural(cap);
        string farPants = Shade(pants, 0.74f), shirtShade = Shade(shirt, 0.84f), hem = Shade(shirt, 0.92f);
        string belt = Shade(pants, 0.78f), skinShade = SkinShade(skin, 0.9f), farSkin = SkinShade(skin, 0.8f), hairLo = Shade(hair, 0.8f);
        const string shoe = "#3a2c24", farShoe = "#281e18", eye = "#2a201c";
        bool side = face is "left" or "right";
        int dir = face == "left" ? -1 : 1;
        string hf = head ?? face;
        int fo = hf == "left" ? -1 : hf == "right" ? 1 : 0;
        int up = bob + (step is 2 or 4 ? 1 : 0);
        // Walking in profile the body leans into each stride a pixel.
        int lean2 = lean;
        int lg = seated ? 0 : 1, tl = lg;   // standing: the body is a row longer and the legs a row shorter, so the legs do not dominate
        int st = seated ? 0 : Stature;   // a rider or a boat's fisher sits in the saddle with their legs down its flank: no longer legs
        int ux = side ? x + lean2 * dir : x, uy0 = y - up, uy = uy0 - st;
        // Front and back, walking: the shoulders and head swing a pixel over the foot that carries the weight (the hips don't),
        // and the hair follows a beat behind.
        int hs = !side && arms == 0 ? (step == 1 ? -1 : step == 3 ? 1 : 0) : 0;
        int hx = ux + hs, sh = -hs;
        // Side views mirror about x - 0.5, the middle of the body. S draws the legs, U the upper body (bob and lean).
        void S(int dx, int dy, int w, int h, string c) => Tex(p, dir > 0 ? x + dx : x - dx - w, y + dy, w, h, c, 0.05f);
        void U(int dx, int dy, int w, int h, string c) => Tex(p, dir > 0 ? ux + dx : ux - dx - w, uy + dy, w, h, c, 0.05f);

        if (shadow) { p.Rect(x - 3, y, 6, 1, "rgba(16,40,44,0.3)"); p.Rect(x - 2, y + 1, 4, 1, "rgba(16,40,44,0.14)"); }

        // Legs, with a darker leather shoe on each foot so the steps read.
        if (side)
        {
            // Each leg is drawn from the hip down to its foot a row at a time, so the whole leg swings (not just the shoe):
            // reaching forward at a stride, pushing off behind, and bent at the knee with the foot lifted as it passes.
            // fx is where the foot lands (forward is +), kx how far the knee pokes forward, lift raises the foot.
            void Leg(int fx, int kx, int lift, string c, string sc)
            {
                int top = -3 - up - st + lg, bot = -1 - lift, n = Math.Max(1, bot - top);
                for (int r = 0; r < n; r++)
                {
                    float t = (float)r / n;
                    float off = kx == 0 ? fx * t : t < 0.5f ? kx * (t / 0.5f) : kx + (fx - kx) * ((t - 0.5f) / 0.5f);
                    S(-1 + (int)MathF.Round(off), top + r, 2, 1, c);
                }
                if (lift > 1) S(-1 + fx, bot, 2, 1, sc); else S(-1 + fx, bot, 3, 1, sc);
            }
            switch (step)
            {
                case 1: Leg(-2, 0, 1, farPants, farShoe); Leg(2, 0, 0, pants, shoe); break;    // contact: near forward, far pushing off
                case 2: Leg(-1, 1, 2, farPants, farShoe); Leg(0, 0, 0, pants, shoe); break;    // passing: near planted, far bent through
                case 3: Leg(2, 0, 0, farPants, farShoe); Leg(-2, 0, 1, pants, shoe); break;    // contact: far forward, near pushing off
                case 4: Leg(0, 0, 0, farPants, farShoe); Leg(-1, 1, 2, pants, shoe); break;    // passing: far planted, near bent through
                default: Leg(-1, 0, 0, farPants, farShoe); Leg(0, 0, 0, pants, shoe); break;
            }
        }
        else
        {
            // Front and back: the leg that isn't taking the weight lifts its foot; the right one is a shade darker.
            int liftL = step == 3 ? 1 : 0, liftR = step == 1 ? 1 : 0;
            Tex(p, x - 2, y - 3 - up - st + lg, 2, 2 - liftL + up + st - lg, pants, 0.05f, 0.12f); Tex(p, x - 2, y - 1 - liftL, 2, 1, shoe, 0.06f);
            Tex(p, x + 1, y - 3 - up - st + lg, 2, 2 - liftR + up + st - lg, Shade(pants, 0.92f), 0.05f, 0.12f); Tex(p, x + 1, y - 1 - liftR, 2, 1, shoe, 0.06f);
        }

        // Arms swing against the legs: a stride on the near leg puts the near arm back.
        int armSwing = arms == 0 ? (step == 1 ? -1 : step == 3 ? 1 : swing) : 0;
        if (longHair && hf == "up") Tex(p, hx - 3, uy - 11, 6, 6, hair, 0.12f);
        if (side)
        {
            // Profile: the back curves in at the waist, the far arm behind the body and the near arm over it.
            if (arms == 4 && hand2 is { } h2) ArmTo(p, FarShoulder(ux, uy0 + Stature - st, face), h2, Shade(shirt, 0.66f), farSkin);
            if (arms == 3) U(1, -6 + swing, 1, 1, farSkin);
            else if (arms == 0 && armSwing < 0) U(2, -3 + tl, 1, 1, farSkin);
            else if (arms == 0 && armSwing > 0) U(-4, -3 + tl, 1, 1, farSkin);
            U(-2, -4 + tl, 4, 1, belt);
            U(-2, -8, 4, 1, shirt);
            U(-3, -7, 5, 3 + tl, shirt);
            U(-3, -7, 1, 3 + tl, shirtShade);
            U(-3, -5 + tl, 5, 1, hem);
            if (arms == 4) { if (hand is { } h) ArmTo(p, NearShoulder(ux, uy0 + Stature - st, face), h, shirtShade, skin); }
            else SideArm(U, arms, armSwing, swing, shirtShade, skin, tl);
        }
        else
        {
            if (arms == 4 && armsBehind) ReachArms();
            // A slim belt under the shirt gives the waist; the arms hang flush at the sides; the shoulders swing, the hips stay.
            int sx = hs;
            Tex(p, ux - 2, uy - 4 + tl, 4, 1, belt, 0.05f);
            Tex(p, ux - 3, uy - 5 + tl, 6, 1, hem, 0.055f, 0.12f);
            Tex(p, ux - 3, uy - 6, 6, 1 + tl, shirt, 0.055f, 0.12f);
            Tex(p, ux - 3 + sx, uy - 7, 6, 1, shirt, 0.055f, 0.14f);
            Tex(p, ux - 2 + sx, uy - 8, 4, 1, shirt, 0.055f, 0.12f);
            if (face == "down") Tex(p, ux - 1 + sx, uy - 8, 2, 1, skinShade, 0.03f);
            else Tex(p, ux - 1 + sx, uy - 8, 2, 1, shirtShade, 0.05f);
            if (arms != 4) FrontArms(p, ux + sx, uy, arms, armSwing, swing, shirtShade, skin, tl);
            else if (!armsBehind) ReachArms();
        }
        // Front and back: the right arm to the hand, the left to the other hand (or hanging).
        void ReachArms()
        {
            if (hand is { } h) ArmTo(p, NearShoulder(ux, uy0 + Stature - st, face), h, shirtShade, skin);
            if (hand2 is { } h2) ArmTo(p, FarShoulder(ux, uy0 + Stature - st, face), h2, shirtShade, skin);
            else { p.Rect(ux - 4, uy - 7, 1, 4 + tl, shirtShade); p.Rect(ux - 4, uy - 3 + tl, 1, 1, skin); }
        }
        if (longHair && hf != "up")
        {
            if (hf is "left" or "right")
            {
                int hbx = hf == "right" ? hx - 3 : hx + 1;
                Tex(p, hbx, uy - 11, 2, 4, hair, 0.12f);
                p.Rect(hf == "right" ? hx - 3 : hx + 2, uy - 7, 1, 1, hair);
                // The ends swing out behind her on a stride.
                if (step is 1 or 3) p.Rect(hf == "right" ? hx - 4 : hx + 3, uy - 7, 1, 1, hairLo);
            }
            else
            {
                Tex(p, hx - 3, uy - 11, 1, 4, hair, 0.12f); Tex(p, hx + 2, uy - 11, 1, 4, hair, 0.12f);
                p.Rect(hx - 3 + sh, uy - 7, 1, 1, hairLo); p.Rect(hx + 2 + sh, uy - 7, 1, 1, hairLo);
            }
        }
        // The head: skin that isn't one flat colour (a warm cheek, a shadowed jaw); the details that read at a glance are the hair, the eyes and the hat.
        Tex(p, hx - 2 + fo, uy - 11, 4, 2, hf == "up" ? hair : skin, hf == "up" ? 0.12f : 0.035f, hf == "up" ? 0f : 0.04f);
        Tex(p, hx - 2 + fo, uy - 9, 4, 1, hf == "up" ? hair : Blend(skin, "#d08a70", 0.12f), 0.04f, 0.05f);
        if (hf is "left" or "right") p.Rect(hf == "right" ? hx - 1 : hx, uy - 10, 1, 1, skinShade);   // a hint of ear
        // A brim or a cap's peak shades the top of the face.
        if (hat is 1 or 3 or 5 or 6 && hf != "up") Tex(p, hx - 2 + fo, uy - 10, 4, 1, SkinShade(skin, 0.93f), 0.03f);

        switch (hat)
        {
            case 1: // straw hat: a woven crown (the rows alternate), a brim
                Tex(p, hx - 3, uy - 13, 6, 1, "#e9b852", 0.07f); Tex(p, hx - 3, uy - 12, 6, 1, "#d9a944", 0.07f);
                Tex(p, hx - 4, uy - 11, 8, 1, "#c99a3c", 0.07f, 0.12f);
                break;
            case 2: // beanie in the shirt colour, with a bobble
                Tex(p, hx - 2, uy - 14, 4, 1, shirt, 0.07f);
                Tex(p, hx - 3, uy - 13, 6, 2, shirt, 0.07f, 0.1f);
                Tex(p, hx - 3, uy - 11, 6, 1, Shade(shirt, 0.72f), 0.06f);
                p.Rect(hx, uy - 15, 1, 1, "#efe9dc");
                break;
            case 4: // bandana: cloth knotted round the head, a pale pattern, its tails trailing behind
            {
                string c = cap ?? "#c0392b", d = Shade(c, 0.72f);
                Tex(p, hx - 3, uy - 13, 6, 1, c, 0.06f); Tex(p, hx - 3, uy - 12, 6, 1, c, 0.06f, 0.1f);
                Tex(p, hx - 3, uy - 11, 6, 1, d, 0.05f);   // the band across the brow
                p.Rect(hx - 1, uy - 12, 1, 1, "#f3eedd"); p.Rect(hx + 1, uy - 13, 1, 1, "#f3eedd");
                if (hf == "right") { p.Rect(hx - 4, uy - 11, 1, 2, d); p.Rect(hx - 5, uy - 10, 1, 1, c); }
                else if (hf == "left") { p.Rect(hx + 3, uy - 11, 1, 2, d); p.Rect(hx + 4, uy - 10, 1, 1, c); }
                else if (hf == "up") { p.Rect(hx - 1, uy - 11, 2, 2, d); p.Rect(hx - 2, uy - 9, 1, 1, c); p.Rect(hx + 1, uy - 9, 1, 1, c); }
                break;
            }
            case 5: // bucket hat: a soft round crown and a short brim all round
            {
                string c = cap ?? "#b8a574", d = Shade(c, 0.74f);
                Tex(p, hx - 2, uy - 14, 4, 1, c, 0.06f); Tex(p, hx - 3, uy - 13, 6, 1, c, 0.06f, 0.1f);
                Tex(p, hx - 4, uy - 12, 8, 1, d, 0.05f);
                p.Rect(hx - 4, uy - 11, 1, 1, d); p.Rect(hx + 3, uy - 11, 1, 1, d);
                break;
            }
            case 6: // salakot: a domed, wide, woven hat; its brim shades the brow and a cord runs under the chin
            {
                string c = cap ?? "#d7b66a", d = Shade(c, 0.76f), lt = Lighten(c, 0.12f);
                Tex(p, hx - 1, uy - 16, 2, 1, lt, 0.05f);
                Tex(p, hx - 2, uy - 15, 4, 1, c, 0.06f); Tex(p, hx - 3, uy - 14, 6, 1, c, 0.06f, 0.08f);
                Tex(p, hx - 5, uy - 13, 10, 1, lt, 0.07f); Tex(p, hx - 6, uy - 12, 12, 1, d, 0.06f);   // the wide, woven brim
                for (int k = -5; k <= 4; k += 2) p.Rect(hx + k, uy - 13, 1, 1, d);
                if (hf != "up") { p.Rect(hx - 3, uy - 11, 1, 3, Shade(c, 0.6f)); p.Rect(hx + 2, uy - 11, 1, 3, Shade(c, 0.6f)); }
                break;
            }
            case 3: // cap with a brim facing forward
                Tex(p, hx - 3, uy - 13, 6, 2, cap ?? shirt, 0.06f, 0.1f);
                if (hf == "right") Tex(p, hx + 2, uy - 11, 3, 1, Shade(cap ?? shirt, 0.7f), 0.05f);
                else if (hf == "left") Tex(p, hx - 5, uy - 11, 3, 1, Shade(cap ?? shirt, 0.7f), 0.05f);
                else if (hf == "down") Tex(p, hx - 3, uy - 11, 6, 1, Shade(cap ?? shirt, 0.7f), 0.05f);
                break;
            default: // hair only: strands of different tones (no clean highlight), a fringe over the brow
                Tex(p, hx - 2, uy - 13, 4, 1, hair, 0.13f);
                Tex(p, hx - 3, uy - 12, 6, 1, hair, 0.13f);
                if (hf != "up") Tex(p, hx - 2 + fo, uy - 11, 4, 1, hair, 0.13f);
                Tex(p, hx - 3, uy - 11, 1, 1, hair, 0.1f); Tex(p, hx + 2, uy - 11, 1, 1, hair, 0.1f);
                break;
        }
        if (blink) return;
        if (hf == "down") { p.Rect(hx - 1, uy - 10, 1, 1, eye); p.Rect(hx + 1, uy - 10, 1, 1, eye); }
        else if (hf == "left") p.Rect(hx - 2, uy - 10, 1, 1, eye);
        else if (hf == "right") p.Rect(hx + 1, uy - 10, 1, 1, eye);
    }

    // An arm from its shoulder to the hand: a sleeve, then the hand itself.
    static void ArmTo(Pix p, (int x, int y) s, (int x, int y) h, string sleeve, string skin)
    {
        if (s != h) p.Line(s.x, s.y, h.x, h.y, sleeve);
        p.Rect(h.x, h.y, 1, 1, skin);
    }

    // The near arm in a side view (U mirrors it for facing left): hanging, swinging forward (+1) or back (-1),
    // holding a rod out in front, or the two-arm and waving poses.
    static void SideArm(Action<int, int, int, int, string> U, int arms, int armSwing, int pump, string sleeve, string skin, int tl = 0)
    {
        if (arms == 2) { U(-1, -11, 1, 4, sleeve); U(-1, -12, 1, 1, skin); return; }
        if (arms == 3) { U(0, -7, 1, 1, sleeve); U(1, -6 + pump, 1, 1, sleeve); U(2, -6 + pump, 1, 1, skin); return; }
        if (arms == 1) { U(0, -9, 1, 2, sleeve); U(pump & 1, -10, 1, 1, skin); return; }
        if (armSwing > 0) { U(0, -7, 1, 1, sleeve); U(1, -6, 1, 3 + tl, sleeve); U(2, -3 + tl, 1, 1, skin); }
        else if (armSwing < 0) { U(-2, -7, 1, 1, sleeve); U(-3, -6, 1, 3 + tl, sleeve); U(-4, -3 + tl, 1, 1, skin); }
        else { U(-1, -7, 1, 4 + tl, sleeve); U(-1, -3 + tl, 1, 1, skin); }
    }

    // Front and back views: an arm each side. A swing lifts one hand and lowers the other.
    static void FrontArms(Pix p, int x, int u, int arms, int armSwing, int pump, string sleeve, string skin, int tl = 0)
    {
        if (arms == 2)
        {
            foreach (int ax in new[] { x - 4, x + 3 }) { p.Rect(ax, u - 11, 1, 4, sleeve); p.Rect(ax, u - 12, 1, 1, skin); }
            return;
        }
        void Arm(int ax, int drop)
        {
            p.Rect(ax, u - 7, 1, 4 + drop + tl, sleeve);
            p.Rect(ax, u - 3 + drop + tl, 1, 1, skin);
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
