using Raylib_cs;

namespace Fesh;

// 1.48: people in the style of a farming-life game. The old figure (a 4x3 head on a block of a body) read like an early
// 8-bit game; now the head is big and round (10 wide, 10 tall, a third of the person or more), with eyes two pixels tall,
// rosy cheeks, hair with a lit crown and a darker edge of its own colour, and hats drawn as pieces of their own. Each
// head and hat is a hand-pixelled template of letters that is recoloured for whoever wears it; the body is drawn in flat
// cel-shaded colours. The feet, the shoulders and the hands are where they were, so every tool, rod and seat still fits.
// FESH_OLDPEOPLE=1 (or FESH_OLDART=1) draws the old figure.
static partial class LookData
{
    // Set by a caller that remembers a turn (Stroller.Turning), just before DrawFigure; used once and cleared.
    public static string TurnFrom;

    public static bool OldPeople = Environment.GetEnvironmentVariable("FESH_OLDPEOPLE") == "1"
        || Environment.GetEnvironmentVariable("FESH_OLDART") == "1";

    // How far the new head reaches above the old one, for anything drawn on a person's head (an islander's salakot, a scarf,
    // Ma'am Isay's glasses): hats and hair sit this much higher, the eyes EyeLift higher, the chin where it was.
    public static int HeadLift => OldPeople ? 0 : 4;
    public static int EyeLift => OldPeople ? 0 : 1;
    public static int HeadWiden => OldPeople ? 0 : 2;   // half the extra width of the head
    public static int HoldLift => OldPeople ? 0 : 6;   // a catch held up over the head sits this much higher

    // Row 0 of every head template is uy - 18 and row 9 the chin at uy - 9, just above the shoulders. 1.48 trimmed a row: the
    // head templates drop their row 3 and the hats their row 1 (Stamp's drop), and the rows above it move down one, so the
    // face, the brim and everything below stay where the rows say while the head is a row shorter (top of hair uy - 17).
    const int HeadTop = 18;
    const int HeadDrop = 3, HatDrop = 1;

    // Heads, 10 wide (the left column is hx - 5, so the middle falls between hx - 1 and hx, like the body's).
    // O hair's edge, H hair, h hair in shade, L hair in the light; S skin, s skin in shade, o the face's edge, B a rosy cheek,
    // E an eye. The light comes from the upper left, as everywhere in the world.
    static readonly string[] FrontShort =
    {
        "...OOOO...",
        "..OHLLHO..",
        ".OHLLHHHO.",
        ".OHLHHHhO.",
        "OHHHHHHHhO",
        "OhHShHSHhO",
        ".OSSSSSsO.",
        ".hSESSEsh.",
        ".oBESSEBo.",
        "..oSSSso..",
    };
    static readonly string[] FrontLong =
    {
        "...OOOO...",
        "..OHLLHO..",
        ".OHLLHHHO.",
        "OHHLHHHHhO",
        "OHHHHHHHhO",
        "OHhShHShhO",
        "OHSSSSSsHO",
        "OHSESSEsHO",
        "OhBESSEBhO",
        "OHoSSSsohO",
        "OH......hO",
        "Oh......hO",
        ".O......O.",
    };
    static readonly string[] BackShort =
    {
        "...OOOO...",
        "..OHLLHO..",
        ".OHLLHHHO.",
        ".OHLHHHhO.",
        "OHHHHHHHhO",
        "OHHHHHHHhO",
        ".OHHHHHhO.",
        ".OhHHHHhO.",
        "..OhhhhO..",
        "...sSSs...",
    };
    static readonly string[] BackLong =
    {
        "...OOOO...",
        "..OHLLHO..",
        ".OHLLHHHO.",
        "OHHLHHHHhO",
        "OHHHHHHHhO",
        "OHHHHHHHhO",
        "OHHHHHHHhO",
        "OHHHHHHHhO",
        "OHHHHHHHhO",
        "OHhHHHHhhO",
        ".OHHHHHhO.",
        ".OhHHHhhO.",
        "..OhhhhO..",
    };
    // Facing right (mirrored for left): the back of the head on the left, an ear, the eye and a little nose on the right.
    static readonly string[] SideShort =
    {
        "...OOOO...",
        "..OHLLHO..",
        ".OHLLHHHO.",
        "OHHLHHHHO.",
        "OHHHHHHhO.",
        "OHHHHhSSo.",
        "OHHhSSSSo.",
        "OhHsSSSES.",
        "..OsSSBEo.",
        "..osSSso..",
    };
    static readonly string[] SideLong =
    {
        "...OOOO...",
        "..OHLLHO..",
        ".OHLLHHHO.",
        "OHHLHHHHO.",
        "OHHHHHHhO.",
        "OHHHHhSSo.",
        "OHHhSSSSo.",
        "OHHsSSSES.",
        "OHhSSSBEo.",
        "OHhOsSso..",
        "OHHO......",
        "OhhO......",
        ".OO.......",
    };

    // Hats: (template, left column relative to hx, top row relative to row 0 of the head). Q the hat's edge, A the hat,
    // a in shade, Z in the light, R its band, W white (a bobble, a pattern).
    static readonly string[] StrawFront =
    {
        "....QQQQQQ....",
        "...QAZZAAaQ...",
        "...QAZAAAaQ...",
        "...QRRRRRRQ...",
        "QAZAAAAAAAAaaQ",
        ".QQaaaaaaaaQQ.",
    };
    static readonly string[] StrawSide =
    {
        "....QQQQQ.....",
        "...QAZZAaQ....",
        "...QAZAAaQ....",
        "...QRRRRRQ....",
        ".QAZAAAAAAAaaQ",
        "..QQaaaaaaaaQ.",
    };
    // A knitted beanie: a round pom-pom, ribbed knit, and a folded cuff of two ribbed rows low on the brow (1.48, second
    // pass: the first one was a bare dome and read as a helmet). Row 1 is dropped like every hat's (HatDrop).
    static readonly string[] Beanie =
    {
        "...wWWw...",
        "..........",
        "..QaWWaQ..",
        ".QAZAZAaQ.",
        "QAZAZAZAaQ",
        "QRrRrRrRrQ",
        "QRrRrRrRrQ",
    };
    static readonly string[] CapFront =
    {
        "...QQQQ...",
        "...QWWQ...",
        "..QAZZAQ..",
        ".QAZAAAaQ.",
        "QAAAAAAAaQ",
        ".QRRRRRRQ.",
    };
    static readonly string[] CapBack =
    {
        "...QQQQ...",
        "...QQQQ...",
        "..QAZZAQ..",
        ".QAZAAAaQ.",
        "QAAAAAAAaQ",
        ".QaaRRaaQ.",
    };
    static readonly string[] CapSide =
    {
        "...QQQQ.....",
        "...QQQQ.....",
        "..QAZZAQ....",
        ".QAZAAAaQ...",
        "QAAAAAAAaQ..",
        ".QaaaaaRRRRQ",
    };
    static readonly string[] Bandana =
    {
        "...QQQQ...",
        "...QQQQ...",
        "..QAWAAQ..",
        ".QAAAAWAQ.",
        "QAAWAAAAaQ",
        ".QRRRRRRQ.",
    };
    static readonly string[] BucketHat =
    {
        "...QQQQQQ...",
        "..QAZZAAaQ..",
        "..QAZAAAaQ..",
        "..QAAAAAaQ..",
        "QAZAAAAAAAaQ",
        ".QaaaaaaaaQ.",
    };
    static readonly string[] Salakot =
    {
        ".......QQ.......",
        ".....QQZZQQ.....",
        "....QAZAAAaQ....",
        "...QAZAAAAAaQ...",
        "..QAAAAAAAAAaQ..",
        ".QZAaAaAaAaAaaQ.",
        "QaaaaaaaaaaaaaaQ",
    };
    // A beard (Tomas), from row 7 of the head down onto the collar.
    // W beard, w beard in shade, m the moustache. It leaves the eyes and the cheeks clear and tapers below the chin.
    static readonly string[] BeardFront =
    {
        ".W......W.",
        ".WW....WW.",
        ".wWWmmWWw.",
        "..wWWWWw..",
        "....ww....",
    };
    static readonly string[] BeardSide =
    {
        "...WW.....",
        "..wWWW....",
        "..wWWWWWm.",
        "...wWWWw..",
        ".....ww...",
    };

    static Color C(string hex) => Pal.C(hex);

    // Stamps a template: column c lands at left + c (or mirrored about hx - 0.5 when mirror), row r at top + r.
    static void Stamp(Pix p, string[] rows, int hx, int left, int top, bool mirror, Func<char, Color?> col, int drop = -1, int lag = 0)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            if (r == drop) continue;
            string row = rows[r];
            int ry = top + r + (r < drop ? 1 : 0);
            for (int c = 0; c < row.Length; c++)
            {
                char ch = row[c];
                if (ch == '.') continue;
                if (col(ch) is not { } k) continue;
                int x = hx + left + c;
                if (mirror) x = 2 * hx - 1 - x;
                p.Rect(x, ry, 1, 1, k);
                if (lag > 0 && r == rows.Length - 1 && rows.Length > 10) p.Rect(x, ry + 1, 1, 1, k);   // long hair's ends trail a beat behind the bounce
            }
        }
    }

    static string Edge(string hex, string toward, float k) => Blend(Shade(hex, 0.55f), toward, k);

    // The new head, hair and hat. hx is the head's middle (like the body's ux), uy the upper body's baseline, hf where the
    // head looks. Long hair hangs over the shoulders, so this is drawn after the body.
    static void DrawHead(Pix p, int hx, int uy, string hf, string skin, string hair, bool longHair, int hat, string cap,
        string shirt, bool blink, int step, string beard)
    {
        bool side = hf is "left" or "right", back = hf == "up", mirror = hf == "left";
        int top = uy - HeadTop;
        string[] head = back ? (longHair ? BackLong : BackShort) : side ? (longHair ? SideLong : SideShort) : (longHair ? FrontLong : FrontShort);
        Color hO = C(Shade(hair, 0.7f)), hH = C(hair), hh = C(Shade(hair, 0.78f)), hL = C(Blend(hair, "#f0dcc0", 0.3f));
        Color sS = C(skin), ss = C(SkinShade(skin, 0.86f)), so = C(SkinShade(skin, 0.8f));   // edges are a darker shade of their own colour, never near black (the user found dark outlines ugly)
        Color blush = C(Blend(skin, "#e0806a", 0.38f)), eye = C("#2a1c18");
        // A brim shades the brow just under it.
        bool brim = hat is 1 or 3 or 5 or 6 && !back;
        int strideRow = step is 1 or 3 ? 11 : -1;   // (row 11 of the long templates, below the dropped row)   // long hair's ends swing out behind on a stride (side view)
        Stamp(p, head, hx, -5, top, mirror, ch => ch switch
        {
            'O' => hO, 'H' => hH, 'h' => hh, 'L' => hL,
            'S' => sS, 's' => ss, 'o' => so, 'B' => blush,
            'E' => blink ? sS : eye,
            _ => null,
        }, HeadDrop, longHair && step is 2 or 4 ? 1 : 0);
        if (blink && !back)
        {
            // Closed eyes: a short dark lash line on the lower row.
            Color lash = C(Blend(SkinShade(skin, 0.6f), "#2a1c18", 0.5f));
            if (side) Px(p, hx, 2, top + 8, mirror, lash);
            else { p.Rect(hx - 2, top + 8, 1, 1, lash); p.Rect(hx + 1, top + 8, 1, 1, lash); }
        }
        else if (!back)
        {
            // A catch of light in each eye's top pixel, so they read as eyes and not holes.
            Color glint = eye;
            if (side) Px(p, hx, 2, top + 7, mirror, glint);
            else { }
        }
        if (side && longHair && strideRow > 0) Px(p, hx, -6, top + strideRow, mirror, hh);
        if (brim && hat != 0) { Color shade = C(SkinShade(skin, 0.84f)); for (int c = side ? 0 : -3; c <= (side ? 2 : 2); c++) Px(p, hx, c, top + 6, side && mirror, shade); }
        if (beard != null && !back)
        {
            Color bW = C(beard), bS = C(Shade(beard, 0.78f)), bM = C(Shade(beard, 0.88f));
            Stamp(p, side ? BeardSide : BeardFront, hx, -5, top + 7, mirror, ch => ch switch { 'W' => bW, 'w' => bS, 'm' => bM, _ => null });
        }

        if (hat == 0) return;
        string hc = hat switch { 1 => "#e3b456", 2 => shirt, 3 => cap ?? shirt, 4 => cap ?? "#c0392b", 5 => cap ?? "#b8a574", 6 => cap ?? "#d7b66a", _ => shirt };
        Color qA = C(hc), qa = C(Shade(hc, 0.78f)), qZ = C(Lighten(hc, 0.22f)), qQ = C(Shade(hc, 0.66f));
        Color qR = hat switch { 1 => C("#a8402f"), 2 => C(Shade(hc, 0.9f)), 3 => C(Shade(hc, 0.66f)), 4 => C(Shade(hc, 0.7f)), _ => qa };
        Color qW = hat == 3 ? C(Lighten(hc, 0.4f)) : C("#f3eee0");
        Color qr = C(Shade(hc, 0.68f)), qw = C("#d4ccbc");
        Color? Hat(char ch) => ch switch { 'Q' => qQ, 'A' => qA, 'a' => qa, 'Z' => qZ, 'R' => qR, 'W' => qW, 'r' => qr, 'w' => qw, _ => null };
        switch (hat)
        {
            case 1: Stamp(p, side ? StrawSide : StrawFront, hx, side ? -6 : -7, top, mirror, Hat, HatDrop); break;
            case 2: Stamp(p, Beanie, hx, -5, top - 1, mirror, Hat, HatDrop); break;
            case 3: Stamp(p, side ? CapSide : back ? CapBack : CapFront, hx, -5, top, mirror, Hat, HatDrop);
                if (beard != null && hf == "down") p.Rect(hx - 1, top + 3, 2, 1, C("#e2b84a"));   // Tomas's old patrol badge
                break;
            case 4:
                Stamp(p, Bandana, hx, -5, top, mirror, Hat, HatDrop);
                // Its knotted tails hang behind.
                if (side) { Px(p, hx, -6, top + 5, mirror, qa); Px(p, hx, -6, top + 6, mirror, qA); Px(p, hx, -7, top + 7, mirror, qa); }
                else if (back) { p.Rect(hx - 1, top + 6, 2, 1, qa); p.Rect(hx - 2, top + 7, 1, 1, qA); p.Rect(hx + 1, top + 7, 1, 1, qA); }
                break;
            case 5: Stamp(p, BucketHat, hx, -6, top, mirror, Hat, HatDrop); break;
            case 6:
                Stamp(p, Salakot, hx, -8, top - 1, mirror, Hat, HatDrop);
                if (!back) { Color cord = C(Shade(hc, 0.55f)); p.Rect(hx - 4, top + 6, 1, 3, cord); if (!side) p.Rect(hx + 3, top + 6, 1, 3, cord); }
                break;
        }
    }

    // The body, hand-pixelled for every walk frame (1.48, second pass: the first new figure kept the old box of a body, which
    // looked plain beside the head, and its side view floated two loose hands). Upper templates are 8 wide (column c at
    // ux - 4 + c, mirrored for facing left) and 6 rows (row 0 at uy - 8, the shoulders; row 4 the belt at uy - 4; row 5 only a hand swung low); legs
    // are 4 rows on the ground (y - 4 to y - 1, the shoes: legs as long as the shirt, not a box on stubs), and a passing frame's lift fills the gap with the hips' row.
    // U shirt in the light, T shirt, t in shade, C collar, k neck; H/J the left (front) or far (side) sleeve and hand,
    // M/N the right (front) or near (side) ones; E belt, G buckle; P/p trousers lit and in shade; F/f shoes.
    static readonly string[] FrontStand = { ".UCkkCt.", "HUTTTTtM", "HTTTTTtM", "HttttttM", "JEEGEEEN", "........" };
    static readonly string[] FrontStep1 = { ".UCkkCt.", "HUTTTTtM", "JTTTTTtM", ".ttttttM", ".EEGEEEM", ".......N" };
    static readonly string[] FrontStep3 = { ".UCkkCt.", "HUTTTTtM", "HTTTTTtN", "Htttttt.", "HEEGEEE.", "J......." };
    static readonly string[] FrontLegs = { "..Pp.Pp.", "..Pp.Pp.", "..Pp.Pp.", "..Ff.Ff." };
    static readonly string[] FrontLegs1 = { "..Pp.Pp.", "..Pp.Pp.", "..Ff.Pp.", ".....Ff." };
    static readonly string[] FrontLegs3 = { "..Pp.Pp.", "..Pp.Pp.", "..Pp.Ff.", "..Ff...." };
    // Heels coming up before a foot lifts (front and back views have little else to show of a step).
    static readonly string[] FrontLegsLHeel = { "..Pp.Pp.", "..Pp.Pp.", "..Pp.Pp.", "...f.Ff." };
    static readonly string[] FrontLegsRHeel = { "..Pp.Pp.", "..Pp.Pp.", "..Pp.Pp.", "..Ff.F.." };
    // Front and back arms through the eight frames: which row each hand hangs to (left, right). 4 is beside the belt; a
    // hand swung forward hangs lower (5) and one swung back is foreshortened higher (2, 3).
    static readonly (int l, int r)[] FrontHands = { (4, 4), (2, 5), (4, 4), (5, 2), (4, 4), (3, 4), (4, 3), (4, 3), (3, 4) };
    static readonly string[][] FrontUppers = FrontHands.Select(h =>
    {
        var rows = new[] { ".UCkkCt.", ".UTTTTt.", ".TTTTTt.", ".tttttt.", ".EEGEEE.", "........" }.Select(s => s.ToCharArray()).ToArray();
        for (int r = 1; r <= h.l; r++) rows[r][0] = r == h.l ? 'J' : 'H';
        for (int r = 1; r <= h.r; r++) rows[r][7] = r == h.r ? 'N' : 'M';
        return rows.Select(c => new string(c)).ToArray();
    }).ToArray();
    static readonly string[] SideStand = { ".UTTTTt.", ".TTTMTt.", ".TTTMTt.", ".tttMtt.", ".EEENGE.", "........" };
    static readonly string[] SideStep1 = { ".UTTTTt.", ".TTMTTt.", ".TMTTTtH", "MttttttJ", "NEEEEGE.", "........" };
    static readonly string[] SideStep3 = { ".UTTTTt.", ".TTTTMt.", "HTTTTTM.", "JttttttM", ".EEEEGEN", "........" };
    // 1.48: an eight-frame walk. Steps 1 to 4 are the key frames (contact, passing, contact, passing); 5 to 8 are the
    // frames between them (5 after 1, 6 after 2, 7 after 3, 8 after 4), so a walk plays 1, 5, 2, 6, 3, 7, 4, 8 (Walk8).
    // Anything that only knows the four key frames still draws right.
    public static readonly int[] Walk8 = { 1, 5, 2, 6, 3, 7, 4, 8 };
    public static int WalkStep(float walkT, float framesPerSec) => Walk8[(int)(walkT * framesPerSec * 2) % 8];
    static readonly string[] SideHalfBack = { ".UTTTTt.", ".TTMTTt.", ".TTMTTt.", ".tMtttt.", ".ENEEGE.", "........" };
    static readonly string[] SideHalfFwd = { ".UTTTTt.", ".TTTMTt.", ".TTTTMt.", ".ttttMt.", ".EEEEGN.", "........" };
    static readonly string[] SideLegs5 = { "..pPP...", ".ppPP...", ".f..PP..", "....FFF." };
    static readonly string[] SideLegs6 = { "..PPp...", "..PPpp..", "..PP..f.", "..FF...." };
    static readonly string[] SideLegs7 = { "..Ppp...", ".PPpp...", ".F..pp..", "....fff." };
    static readonly string[] SideLegs8 = { "..pPP...", "..ppPP..", "..pp..F.", "..ff...." };
    static readonly string[] SideLegs = { "..pPP...", "..pPP...", "..pPP...", "..fFFF.." };
    static readonly string[] SideLegs1 = { "..pPP...", ".pp.PP..", ".pp..PP.", ".ff..FFF" };
    static readonly string[] SideLegs2 = { "..pPP...", "..pPP...", "..fPP...", "...FFF.." };
    static readonly string[] SideLegs3 = { "..PPp...", ".PP.pp..", ".PP..pp.", ".FF..fff" };
    static readonly string[] SideLegs4 = { "..Ppp...", "..Ppp...", "..Fpp...", "...fff.." };

    // Draws the templated body. Arms 0 swing with the walk; 1 (a wave) and 3 (a rod) keep the left or far arm and leave the
    // other to the caller; 2 (both up) and 4 (reaching for a tool) leave both, except a front view's free left arm.
    static void DrawBodyNew(Pix p, int x, int y, int ux, int uy, string face, int step, int arms, bool freeLeft,
        string shirt, string pants, string skin)
    {
        bool side = face is "left" or "right", mirror = face == "left", back = face == "up";
        string[] upper = side ? step switch { 1 => SideStep1, 3 => SideStep3, 5 or 8 => SideHalfBack, 6 or 7 => SideHalfFwd, _ => SideStand }
                              : FrontUppers[Math.Clamp(step, 0, 8)];
        string[] legs = side ? step switch { 1 => SideLegs1, 2 => SideLegs2, 3 => SideLegs3, 4 => SideLegs4, 5 => SideLegs5, 6 => SideLegs6, 7 => SideLegs7, 8 => SideLegs8, _ => SideLegs }
                             : step switch { 1 => FrontLegs1, 3 => FrontLegs3, 5 or 6 => FrontLegsRHeel, 7 or 8 => FrontLegsLHeel, _ => FrontLegs };
        if (arms != 0) upper = side ? SideStand : FrontStand;   // posed arms come from the caller, over a still body
        bool keepLeft = arms is 0 or 1 or 3 || arms == 4 && !side && freeLeft, keepRight = arms == 0;
        Color sT = C(shirt), sU = C(Lighten(shirt, 0.14f)), st = C(Shade(shirt, 0.84f)), sC = C(Lighten(shirt, 0.26f));
        Color neck = C(SkinShade(skin, 0.86f)), armL = C(Shade(shirt, 0.92f)), armR = C(Shade(shirt, 0.86f));
        Color hand = C(skin), handFar = C(SkinShade(skin, 0.85f));
        Color pP = C(pants), pp = C(Shade(pants, 0.8f)), belt = C(Shade(pants, 0.68f)), buckle = C("#d8b04a");
        Color shoe = C("#4a3428"), shoeD = C("#33241c");
        Color? Col(char ch) => ch switch
        {
            'U' => sU, 'T' => sT, 't' => st,
            'C' => back ? sT : sC, 'k' => back ? sT : neck,
            'H' => keepLeft ? armL : side ? sT : null, 'J' => keepLeft ? (side ? handFar : hand) : side ? sT : null,
            'M' => keepRight ? armR : side ? sT : null, 'N' => keepRight ? hand : side ? st : null,
            'E' => belt, 'G' => back ? belt : buckle,
            'P' => pP, 'p' => pp, 'F' => shoe, 'f' => shoeD,
            _ => null,
        };
        void Put(string[] t, int ox, int oy)
        {
            for (int r = 0; r < t.Length; r++)
                for (int c = 0; c < t[r].Length; c++)
                {
                    if (t[r][c] == '.' || Col(t[r][c]) is not { } k) continue;
                    int px = ox - 4 + c;
                    if (mirror) px = 2 * ox - 1 - px;
                    p.Rect(px, oy + r, 1, 1, k);
                }
        }
        // Legs first (a crouch lowers the body over them), the hips' row repeated up to the belt on a passing frame.
        Put(legs, x, y - 4);
        for (int gy = uy - 3; gy < y - 4; gy++) Put(new[] { legs[0] }, x, gy);
        Put(upper, ux, uy - 8);
    }

    // The body is drawn into a small buffer first, so its silhouette can be given an edge of its own colours (Stardew-like
    // "selective outline"): every pixel on the edge is darkened, more on the side away from the light (right and below) than
    // on the lit side. Nothing is drawn outside the body (an outside line was tried in 1.29 and read as a hard black line).
    static readonly Pix bodyBuf = new(48, 48);
    static Pix BeginBody(int x, int y)
    {
        Array.Clear(bodyBuf.Buf);
        bodyBuf.CamX = x - 24; bodyBuf.CamY = y - 40;
        return bodyBuf;
    }

    static Color Darker(Color c, float k)
    {
        // Darker and a touch bluer, like Shade.
        float t = (1 - k) * 0.3f;
        return new Color((byte)(c.R * k * (1 - t) + 26 * t), (byte)(c.G * k * (1 - t) + 42 * t), (byte)(c.B * k * (1 - t) + 80 * t), (byte)255);
    }

    static void EndBody(Pix dst)
    {
        var b = bodyBuf; int w = b.W, h = b.H;
        bool On(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && b.Buf[y * w + x].A > 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = b.Buf[y * w + x];
                if (c.A == 0) continue;
                bool l = On(x - 1, y), r = On(x + 1, y), u = On(x, y - 1), d = On(x, y + 1);
                float k = 1;
                if (!r || !d) k = l || r ? 0.84f : 0.93f;   // a one-pixel limb is only shaded a little, or it turns black
                else if (!l || !u) k = 0.96f;
                if (k < 1) c = Darker(c, k);
                dst.Rect(b.CamX + x, b.CamY + y, 1, 1, c);
            }
    }

    // Arms raised over the head (a catch held up, a stretch) or one waving: drawn after the head, reaching up past it.
    static void RaisedArms(Pix p, int ux, int uy, string face, int arms, int swing, string sleeve, string skin)
    {
        var sl = C(Shade(Natural(sleeve), 0.9f)); var sk = C(skin);
        bool side = face is "left" or "right", mirror = face == "left";
        void Arm(int x0, int y0, int x1, int y1, int wave)
        {
            int x0m = mirror ? 2 * ux - 1 - x0 : x0, x1m = mirror ? 2 * ux - 1 - x1 : x1;
            int n = Math.Max(Math.Abs(x1m - x0m), Math.Abs(y1 - y0));
            for (int i = 0; i <= n; i++)
            {
                float t = n == 0 ? 0 : (float)i / n;
                p.Rect(MathF.Round(x0m + (x1m - x0m) * t), MathF.Round(y0 + (y1 - y0) * t), 1, 1, sl);
            }
            p.Rect(x1m + (mirror ? -wave : wave), y1 - 1, 1, 1, sk);
        }
        if (arms == 2)
        {
            if (side) Arm(ux, uy - 8, ux + 3, uy - 17, 0);
            else { Arm(ux - 4, uy - 8, ux - 6, uy - 17, 0); Arm(ux + 3, uy - 8, ux + 5, uy - 17, 0); }
        }
        else if (side) Arm(ux, uy - 8, ux + 4, uy - 13, swing & 1);
        else Arm(ux + 3, uy - 8, ux + 5, uy - 13, swing & 1);
    }

    // A child's body (from the shorts up) and big round head in the new style, painted through R (Game.DrawKid's painter,
    // which draws the same child in the world and scaled up on the quiz panel). x is the child's middle column.
    static readonly string[] KidFace =
    {
        ".OOOOO.",
        "OHLLHhO",
        "OhSSShO",
        "SSESESs",
        "oBSSSBo",
        ".osSso.",
    };
    static readonly string[] KidBack =
    {
        ".OOOOO.",
        "OHLLHhO",
        "OHHHHhO",
        "OHHHHhO",
        "OhHHhhO",
        ".OsSsO.",
    };
    public static void DrawKidNew(Action<int, int, int, int, string> R, int x, int y, int k, int pose, string skin, string shirt, string hair, bool back = false)
    {
        string sShade = Shade(Natural(shirt), 0.78f), nShirt = Natural(shirt);
        // Body and arms: a little smock, the arms at its sides, up in the air, or hands on the head.
        R(x - 1, y - 5, 3, 3, nShirt); R(x + 1, y - 5, 1, 3, sShade); R(x - 1, y - 5, 1, 1, Lighten(nShirt, 0.2f));
        if (pose == 1) { R(x - 3, y - 9, 1, 4, sShade); R(x + 3, y - 9, 1, 4, sShade); R(x - 3, y - 10, 1, 1, skin); R(x + 3, y - 10, 1, 1, skin); }
        else if (pose == 2) { R(x - 3, y - 9, 1, 3, sShade); R(x + 3, y - 9, 1, 3, sShade); R(x - 2, y - 12, 1, 1, skin); R(x + 2, y - 12, 1, 1, skin); }
        else { R(x - 2, y - 5, 1, 2, sShade); R(x + 2, y - 5, 1, 2, sShade); R(x - 2, y - 3, 1, 1, skin); R(x + 2, y - 3, 1, 1, skin); }
        string hO = Shade(hair, 0.7f), hL = Blend(hair, "#f0dcc0", 0.3f), hh = Shade(hair, 0.78f);
        string ss = SkinShade(skin, 0.86f), so = SkinShade(skin, 0.8f), blush = Blend(skin, "#e0806a", 0.38f);
        string[] t = back ? KidBack : KidFace;
        for (int r = 0; r < t.Length; r++)
            for (int c = 0; c < t[r].Length; c++)
            {
                string col = t[r][c] switch
                {
                    'O' => hO, 'H' => hair, 'h' => hh, 'L' => hL, 'S' => skin, 's' => ss, 'o' => so, 'B' => blush, 'E' => "#2a1c18",
                    _ => null,
                };
                if (col != null) R(x - 3 + c, y - 11 + r, 1, 1, col);
            }
        // Two of them have pigtails.
        if (k % 2 == 1) { R(x - 4, y - 9, 1, 2, hair); R(x + 4, y - 9, 1, 2, hair); R(x - 4, y - 7, 1, 1, hh); R(x + 4, y - 7, 1, 1, hh); }
    }

    // One pixel at column c of a 10-wide template whose left edge is hx - 5... (c relative to hx), mirrored for facing left.
    static void Px(Pix p, int hx, int c, int y, bool mirror, Color k)
    {
        int x = hx + c;
        if (mirror) x = 2 * hx - 1 - x;
        p.Rect(x, y, 1, 1, k);
    }
}
