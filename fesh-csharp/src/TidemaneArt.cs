using Raylib_cs;

namespace Fesh;

// What Tidemane is doing, for the sprite. Kind is "stand", "trot", "gallop", "rear", "lie", "leap" or "swim"; Frame
// steps through a gait (gallop 0-7, trot 0-5, swim 0-5). Time drives the idle motions (breath, tail, mane, blinks) and
// Seed keeps two of them out of step.
sealed class MountPose
{
    public string Kind = "stand";
    public int Frame;
    public float Time, Seed;
    public bool Saddle;     // tamed: a woven saddle cloth
    public bool Flash;      // just hurt: drawn all white
    public float Leap;      // "leap": 0 to 1 through the arc (rising, then falling)
    public float Toss;      // 0 to 1 through a toss of the head
    public float Speed = 1; // how hard it's going, for the mane and the tail
    public bool Ridden;     // seen from the front, it carries its head low so you can see its rider over it
}

// Tidemane's sprite (1.20). Each frame it's built in a small canvas of materials, one letter a pixel: a hand-pixelled
// head, the body from spans, and parts placed from the pose for everything that moves (forelegs, the scaled tail and its
// fan, the foam mane, the fins). Then the body is lit from above, every empty pixel touching it gets a coloured outline
// (sea green by the body, slate by the foam, deep red by the coral; never black: a black outline on the player was tried
// and dropped), and Game.BlitMount draws it, mirrored for facing left. The side view faces right with its feet at 0,0.
sealed class MountCanvas
{
    public const int W = 80, H = 60, OX = 40, OY = 46;
    public readonly char[] M = new char[W * H];
    public readonly char[] Tmp = new char[W * H];
    public void Clear() => Array.Fill(M, '.');
    public static bool In(int x, int y) => x + OX >= 0 && x + OX < W && y + OY >= 0 && y + OY < H;
    public char Get(int x, int y) => In(x, y) ? M[(y + OY) * W + x + OX] : '.';
    public void Set(int x, int y, char c) { if (In(x, y)) M[(y + OY) * W + x + OX] = c; }
    // Behind whatever is already there.
    public void Under(int x, int y, char c) { if (Get(x, y) == '.') Set(x, y, c); }

    public void Stamp(string[] g, int x0, int y0, bool under = false)
    {
        for (int r = 0; r < g.Length; r++)
            for (int i = 0; i < g[r].Length; i++)
                if (g[r][i] != '.') { if (under) Under(x0 + i, y0 + r, g[r][i]); else Set(x0 + i, y0 + r, g[r][i]); }
    }

    public void Disc(float cx, float cy, float r, char c, bool under = false)
    {
        for (int y = (int)MathF.Floor(cy - r - 1); y <= (int)MathF.Ceiling(cy + r + 1); y++)
            for (int x = (int)MathF.Floor(cx - r - 1); x <= (int)MathF.Ceiling(cx + r + 1); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) { if (under) Under(x, y, c); else Set(x, y, c); }
            }
    }

    // A line w pixels thick (a w x w square stepped along it).
    public void Thick(float x0, float y0, float x1, float y1, int w, char c, bool under = false)
    {
        float len = MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0));
        int n = Math.Max(1, (int)MathF.Ceiling(len * 2));
        for (int i = 0; i <= n; i++)
        {
            float k = (float)i / n, x = x0 + (x1 - x0) * k, y = y0 + (y1 - y0) * k;
            int px = (int)MathF.Floor(x - (w - 1) / 2f), py = (int)MathF.Floor(y - (w - 1) / 2f);
            for (int a = 0; a < w; a++)
                for (int b = 0; b < w; b++)
                    if (under) Under(px + a, py + b, c); else Set(px + a, py + b, c);
        }
    }

    public void Tri(float ax, float ay, float bx, float by, float cx, float cy, char c, bool under = false)
    {
        int x0 = (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))), x1 = (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx)));
        int y0 = (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))), y1 = (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy)));
        float Edge(float px, float py, float qx, float qy, float rx, float ry) => (qx - px) * (ry - py) - (qy - py) * (rx - px);
        float area = Edge(ax, ay, bx, by, cx, cy);
        if (MathF.Abs(area) < 0.01f) return;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float w0 = Edge(bx, by, cx, cy, px, py) / area, w1 = Edge(cx, cy, ax, ay, px, py) / area, w2 = Edge(ax, ay, bx, by, px, py) / area;
                if (w0 >= -0.02f && w1 >= -0.02f && w2 >= -0.02f) { if (under) Under(x, y, c); else Set(x, y, c); }
            }
    }

    // Turns everything drawn so far by a radians about (px, py) (negative tips the front up), nearest pixel.
    public void Rotate(float a, float px, float py)
    {
        if (MathF.Abs(a) < 0.001f) return;
        Array.Copy(M, Tmp, M.Length);
        float cs = MathF.Cos(a), sn = MathF.Sin(a);
        for (int y = -OY; y < H - OY; y++)
            for (int x = -OX; x < W - OX; x++)
            {
                float dx = x + 0.5f - px, dy = y + 0.5f - py;
                int sx = (int)MathF.Floor(px + dx * cs + dy * sn), sy = (int)MathF.Floor(py - dx * sn + dy * cs);
                M[(y + OY) * W + x + OX] = In(sx, sy) ? Tmp[(sy + OY) * W + sx + OX] : '.';
            }
    }

    public void Shift(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        Array.Copy(M, Tmp, M.Length);
        Array.Fill(M, '.');
        for (int y = -OY; y < H - OY; y++)
            for (int x = -OX; x < W - OX; x++)
            {
                char ch = Tmp[(y + OY) * W + x + OX];
                if (ch != '.') Set(x + dx, y + dy, ch);
            }
    }
}

static class MountArt
{
    // Materials. B body (lit to h on top and b underneath by Light), d the far side, L belly, l its shade, S the scaled
    // tail, s a scale's edge, F fin, f a fin's ray, g a fin's pale tip, M foam, m foam in shadow, C coral, c coral in
    // shadow, E the gold eye, K nostril and lips, O hoof, * a starlight speckle, w one twinkling, N R Y the saddle cloth.
    public static readonly Dictionary<char, Color> Colors = new()
    {
        ['h'] = Pal.C("#6fd6c0"), ['B'] = Pal.C("#35a897"), ['b'] = Pal.C("#25837a"), ['d'] = Pal.C("#1c6462"),
        ['L'] = Pal.C("#c4f0de"), ['l'] = Pal.C("#8fd2c0"),
        ['S'] = Pal.C("#2f9a8c"), ['s'] = Pal.C("#21766f"), ['t'] = Pal.C("#5cc6b3"),
        ['F'] = Pal.C("#79e6d6"), ['f'] = Pal.C("#41b3a6"), ['g'] = Pal.C("#c9fbf2"),
        ['M'] = Pal.C("#f6fdff"), ['m'] = Pal.C("#bfe2ef"),
        ['C'] = Pal.C("#ff8f7c"), ['c'] = Pal.C("#d4605a"),
        ['E'] = Pal.C("#ffd76a"), ['K'] = Pal.C("#123f45"), ['O'] = Pal.C("#1d4c55"),
        ['*'] = Pal.C("#fff2c4"), ['w'] = Pal.C("#ffffff"),
        ['N'] = Pal.C("#2c3f78"), ['R'] = Pal.C("#d2463c"), ['Y'] = Pal.C("#f3c25b"),
    };

    // The outline takes its colour from what it's around (the darkest neighbour wins). The coral horns have none:
    // outlined, a 1 px antler turned into a red lump.
    static readonly Color OutBody = Pal.C("#0f3b40"), OutFin = Pal.C("#17595a"), OutFoam = Pal.C("#5e8fa8"), OutCloth = Pal.C("#2a1f3a");
    public static int OutlineRank(char m) => m switch { '.' or 'C' or 'c' => 0, 'M' or 'm' => 1, 'F' or 'f' or 'g' => 2, 'N' or 'R' or 'Y' => 3, _ => 4 };
    public static Color OutlineOf(char m) => m switch
    {
        'F' or 'f' or 'g' => OutFin,
        'M' or 'm' => OutFoam,
        'N' or 'R' or 'Y' => OutCloth,
        _ => OutBody
    };

    static bool Bodyish(char m) => m is 'B' or 'h' or 'b' or 'd' or 'L' or 'l' or 'S' or 's' or 't' or 'E' or 'K' or '*' or 'w' or 'O';

    /* ---------- The side view, facing right ---------- */

    // Head, neck and barrel, hand-pixelled; the top-left corner is at (GridX, GridY). The near ear and the far one ('d')
    // behind it, the forehead sloping down to the muzzle (nostril and lips 'K'), the round cheek with the throat tucked in
    // under it, the arched neck, and a short deep barrel with a pale belly. The haunch runs on into the tail (Tail).
    static readonly string[] Grid =
    {
        "....................B.......",   // -29
        "..................d.B.......",   // -28
        ".................BdBB.......",
        "................BBBBBB......",
        "................BBBBBBB.....",
        "...............BBBBEBBBB....",   // -24
        "...............BBBBBBBBBB...",
        "..............BBBBBBBBBBBB..",
        "..............BBBBBBBBBBBBB.",
        ".............BBBBBBBBBBBBBK.",   // -20
        ".............BBBBBB.BBBBBK..",
        "............BBBBBB....BBB...",
        "............BBBBBBB.........",
        "...........BBBBBBBBB........",   // -16
        "....BBBBBBBBBBBBBBBBB.......",
        "..BBBBBBBBBBBBBBBBBBB.......",
        ".BBBBBBBBBBBBBBBBBBBB.......",
        ".BBBBBBBBBBBBBBBBBBBB.......",   // -12
        ".BBBBBBBBBBBBBBBBBBB........",
        "..BBBBBBBBBBBBBBBBBB........",
        "...LLLLLLLLLLLLLLLL.........",
        "......llllllllll............",   // -8
    };
    const int GridX = -10, GridY = -29, NeckTop = -17;
    const int EyeX = 9, EyeY = -24;

    // Coral horns: a stem rising behind the ears with a branch sweeping back.
    static readonly string[] Horns =
    {
        "C..C..",
        ".C.C.c",
        "..CC.c",
        "...C.c",
        "...c..",
    };
    const int HornX = 4, HornY = -34;

    // Speckles on the haunch and the shoulder, fixed to the body (they don't shimmer as it moves).
    static readonly (int x, int y)[] Speckles = { (-7, -12), (-6, -10), (6, -13), (8, -11), (4, -10) };

    // Forelegs through each gait: the forearm's angle from straight down (forward is +) and how far the cannon folds
    // back from it, for the near and the far leg. The gallop is a bound (it has no hind legs): reach, land, sweep back,
    // tuck; the far leg leads by a frame. Body is how the barrel rides (up is -) and Pitch tips the chest (up is -).
    static readonly float[] GallopNearA = { 0.75f, 0.45f, 0.05f, -0.4f, -0.7f, -0.35f, 0.3f, 0.75f };
    static readonly float[] GallopNearB = { 0.1f, 0f, 0f, 0f, 0.2f, 1.5f, 1.9f, 1.1f };
    static readonly float[] GallopFarA = { 0.85f, 0.8f, 0.5f, 0.1f, -0.35f, -0.65f, -0.3f, 0.35f };
    static readonly float[] GallopFarB = { 0.9f, 0.1f, 0f, 0f, 0f, 0.3f, 1.6f, 1.9f };
    static readonly int[] GallopBody = { -1, 0, 0, 1, 1, 0, -1, -2 };
    static readonly float[] GallopPitch = { -0.1f, -0.04f, 0.02f, 0.07f, 0.05f, 0f, -0.06f, -0.1f };
    // The tail pumps against the bound: down and coiled as the chest lifts, kicking up as the forelegs sweep back.
    static readonly float[] GallopTail = { 1.5f, 1f, 0f, -1.5f, -3f, -2.5f, -1f, 1f };

    static readonly float[] TrotNearA = { 0.35f, 0.1f, -0.2f, -0.3f, -0.05f, 0.25f };
    static readonly float[] TrotNearB = { 0f, 0f, 0f, 0.3f, 1.3f, 0.9f };
    static readonly int[] TrotBody = { 0, 0, -1, 0, 0, -1 };

    // The saddle, relative to the feet: where the rider's feet go (their hips sit on its back).
    public static (int x, int y) Seat(MountPose p)
    {
        var (dx, dy, pitch) = Ride(p);
        // The saddle rides up and down with the lean (its back is a little behind the pivot).
        int lean = (int)MathF.Round(MathF.Sin(pitch) * (p.Kind == "rear" ? 7 : -2));
        return (-2 + dx, -11 + dy + lean);
    }

    // How the body sits for a pose: a shift (x, y) and a pitch.
    static (int dx, int dy, float pitch) Ride(MountPose p)
    {
        int f = p.Frame;
        return p.Kind switch
        {
            "gallop" => (0, GallopBody[f % 8], GallopPitch[f % 8]),
            "trot" => (0, TrotBody[f % 6], 0),
            "rear" => (0, -1 - (f % 2), -0.5f),
            "lie" => (0, 5, 0.04f),
            "swim" => (0, 3, -0.06f),
            "leap" => (0, 0, p.Leap < 0.5f ? -0.2f : 0.16f),
            _ => (0, 0, 0)
        };
    }

    public static void BuildSide(MountCanvas c, MountPose p)
    {
        c.Clear();
        var (bx, by, pitch) = Ride(p);
        int f = p.Frame;
        bool lie = p.Kind == "lie", rear = p.Kind == "rear";
        float t = p.Time + p.Seed * 7;

        // The forelegs, the far one first (darker, behind the body).
        float nA, nB, fA, fB;
        switch (p.Kind)
        {
            case "gallop": nA = GallopNearA[f % 8]; nB = GallopNearB[f % 8]; fA = GallopFarA[f % 8]; fB = GallopFarB[f % 8]; break;
            case "trot": nA = TrotNearA[f % 6]; nB = TrotNearB[f % 6]; fA = TrotNearA[(f + 3) % 6]; fB = TrotNearB[(f + 3) % 6]; break;
            // Rearing, it paws the air, one leg and then the other.
            case "rear": nA = f % 2 == 0 ? 1.3f : 0.8f; nB = f % 2 == 0 ? 2.2f : 1.5f; fA = f % 2 == 0 ? 0.7f : 1.3f; fB = f % 2 == 0 ? 1.4f : 2.2f; break;
            case "leap": nA = 1.2f; nB = 2.4f; fA = 1.0f; fB = 2.2f; break;
            // Swimming, they paddle under the surface.
            case "swim": nA = Paddle(f); nB = 0.9f; fA = Paddle(f + 3); fB = 0.9f; break;
            default: nA = 0.04f; nB = 0; fA = -0.1f; fB = 0; break;
        }
        if (lie) { c.Thick(4, -3, 9, -2, 2, 'd'); c.Thick(9, -2, 6, -1, 2, 'd'); }
        else Leg(c, 5, -9, fA, fB, 'd', false);

        // Head, neck and barrel. Tossing its head lifts everything above the neck's base; galloping, it dips with the stride.
        int toss = p.Toss <= 0 ? 0 : -(int)MathF.Round(MathF.Sin(MathF.Min(1, p.Toss) * MathF.PI) * 2);
        int headDy = toss + (lie ? 2 : 0) + (p.Kind == "gallop" && f % 8 is 3 or 4 ? 1 : 0) + (rear && f % 2 == 1 ? -1 : 0);
        for (int r = 0; r < Grid.Length; r++)
            for (int i = 0; i < Grid[r].Length; i++)
            {
                char ch = Grid[r][i];
                if (ch == '.') continue;
                int x = GridX + i, y = GridY + r;
                if (y <= NeckTop) c.Set(x, y + headDy, ch);
                else c.Set(x, y, ch);
            }
        // Fill the gap a raised head leaves.
        for (int y = NeckTop + headDy + 1; y <= NeckTop; y++)
            for (int x = 2; x <= 9; x++) c.Set(x, y, 'B');
        if ((t * 0.27f) % 1 < 0.035f) c.Set(EyeX, EyeY + headDy, 'b');   // a blink
        else c.Set(EyeX + 1, EyeY + headDy, 'K');                         // the pupil, in front of the gold
        c.Stamp(Horns, HornX, HornY + headDy, under: true);

        if (p.Saddle)
        {
            // A woven saddle cloth: indigo with a red band and gold edging, hanging down the flank.
            for (int x = -5; x <= 0; x++) { c.Set(x, -15, 'Y'); c.Set(x, -14, 'N'); c.Set(x, -13, 'R'); c.Set(x, -12, 'N'); }
            c.Set(-5, -11, 'Y'); c.Set(-2, -11, 'Y'); c.Set(0, -11, 'Y');
        }
        int tw = (int)(t * 1.6f) % 7;
        for (int i = 0; i < Speckles.Length; i++)
            if (c.Get(Speckles[i].x, Speckles[i].y) is 'B' or 'L') c.Set(Speckles[i].x, Speckles[i].y, i == tw ? 'w' : '*');

        // The near foreleg, and the fin that flares back from its elbow.
        if (lie) { c.Thick(6, -3, 11, -2, 2, 'B'); c.Thick(11, -2, 8, -1, 2, 'b'); c.Set(12, -2, 'O'); }
        else Leg(c, 7, -9, nA, nB, 'B', true);
        float flut = MathF.Sin(t * (p.Kind is "gallop" or "swim" ? 9 : 2.5f)) * (p.Kind == "gallop" ? 1.2f : 0.6f);
        c.Tri(5, -10, 1, -7 + flut * 0.4f, 3, -5 + flut, 'F');
        c.Thick(4.5f, -9.5f, 2, -6f + flut * 0.6f, 1, 'f');

        // Foam lies along the crest and streams back from it, longer and flatter at a gallop.
        Mane(c, p, t, headDy);

        // Lean it: chest up rearing and leaping, a little rock through the bound.
        if (rear) c.Rotate(pitch, -8, -9);
        else c.Rotate(pitch, 0, -9);
        c.Shift(bx, by);

        // The tail, behind it all: down from the haunch to the sand, then up into the fan.
        Tail(c, p, t, bx, by, pitch);
        Light(c);
    }

    static float Paddle(int f) => (f % 6) switch { 0 => 0.9f, 1 => 0.4f, 2 => -0.1f, 3 => -0.3f, 4 => 0.1f, _ => 0.6f };

    // A foreleg from the shoulder: forearm a radians from straight down, the cannon folded b further back.
    static void Leg(MountCanvas c, float sx, float sy, float a, float b, char mat, bool near)
    {
        float kx = sx + MathF.Sin(a) * 4.5f, ky = sy + MathF.Cos(a) * 4.5f;
        float a2 = a - b, hx = kx + MathF.Sin(a2) * 4f, hy = ky + MathF.Cos(a2) * 4f;
        // A muscled forearm (wider at the top), a slim cannon.
        c.Thick(sx, sy, kx, ky, 2, mat, under: !near);
        c.Thick(sx + 0.8f, sy, sx + 0.8f + MathF.Sin(a) * 2f, sy + MathF.Cos(a) * 2f, 2, mat, under: !near);
        c.Thick(kx, ky, hx, hy, near ? 2 : 1, mat, under: !near);
        // The hoof, and a little fan of fin at the fetlock instead of feathering.
        int ox = (int)MathF.Floor(hx - 0.5f), oy = (int)MathF.Floor(hy);
        if (near) { c.Set(ox, oy, 'O'); c.Set(ox + 1, oy, 'O'); }
        else { c.Under(ox, oy, 'O'); c.Under(ox + 1, oy, 'O'); }
        float bx = -MathF.Cos(a2), byy = MathF.Sin(a2);   // back along the leg's side
        int fx = (int)MathF.Round(hx + bx * 1.6f - 0.5f), fy = (int)MathF.Round(hy - 1.2f + byy);
        if (near) { c.Set(fx, fy, 'F'); c.Set(fx - 1, fy + 1, 'g'); }
        else c.Under(fx, fy, 'f');
    }

    static void Mane(MountCanvas c, MountPose p, float t, int headDy)
    {
        bool fast = p.Kind is "gallop" or "leap" || p.Kind == "swim" && p.Speed > 0.5f;
        float flow = p.Kind == "gallop" ? 1.5f : fast ? 1f : 0.3f;
        int i = 0;
        for (int y = -27 + headDy; y <= -16; y++, i++)
        {
            int x0 = int.MaxValue;
            for (int x = -2; x <= 10; x++) if (c.Get(x, y) is 'B') { x0 = x; break; }
            if (x0 == int.MaxValue) continue;
            // The foam lies on the crest...
            c.Set(x0, y, 'M');
            if (i % 3 != 2) c.Set(x0 + 1, y, i < 3 ? 'M' : 'm');
            // ...and every other lock streams off it.
            if (i % 2 == 1) continue;
            float wave = MathF.Sin(t * (fast ? 11 : 3) - i * 0.9f);
            int len = 1 + (i % 4 == 0 ? 1 : 0) + (int)MathF.Round(flow + wave * 0.6f);
            float droop = fast ? 0.2f : 0.6f;
            for (int k = 1; k <= len; k++)
            {
                int x = x0 - k, yy = y + (int)MathF.Round(k * droop + (k == len ? wave * 0.5f : 0));
                c.Set(x, yy, k == len ? 'm' : 'M');
            }
        }
        // A forelock tumbling over the forehead.
        c.Set(10, -26 + headDy, 'M'); c.Set(11, -25 + headDy, 'm');
    }

    static void Tail(MountCanvas c, MountPose p, float t, int bx, int by, float pitch)
    {
        int f = p.Frame;
        float sway = MathF.Sin(t * 2.6f) * 0.8f;
        // Control points of the curve, from the haunch back: P0 root, P1, P2 (the low bend that rests on the sand), P3 the fan's base.
        (float x, float y) P0 = (-8, -12), P1, P2, P3;
        float lift = 0;
        switch (p.Kind)
        {
            case "gallop": lift = GallopTail[f % 8]; P1 = (-15, -9); P2 = (-15, -1 - lift); P3 = (-21, -5 - lift * 1.6f); break;
            case "trot": lift = f % 3 == 1 ? -1 : 0; P1 = (-15, -10); P2 = (-15, -1 + lift); P3 = (-21, -4 + lift); break;
            case "rear": P0 = (-9, -9); P1 = (-12, -1); P2 = (-17, 1); P3 = (-22, -3 + sway); break;
            case "lie": P0 = (-9, -6); P1 = (-14, -3); P2 = (-15, 1); P3 = (-21, -1 + sway * 0.5f); break;
            case "leap": P1 = (-15, -11); P2 = (-20, -11 + (p.Leap - 0.5f) * 6); P3 = (-25, -12 + (p.Leap - 0.5f) * 10); break;
            case "swim":
            {
                // Along the surface, rolling in a wave, the fan flicking up out of the water.
                float w = MathF.Sin(f / 6f * MathF.Tau) * 2;
                P0 = (-9, -11); P1 = (-14, -7 + w * 0.5f); P2 = (-19, -6 - w); P3 = (-24, -8 + w * 0.5f); break;
            }
            default: P1 = (-15, -10); P2 = (-15, -1); P3 = (-21, -4 + sway); break;
        }
        // The root follows the body's lean and shift.
        (float x, float y) Move((float x, float y) q, float k)
        {
            float cs = MathF.Cos(pitch * k), sn = MathF.Sin(pitch * k);
            float px = p.Kind == "rear" ? -9 : 0, py = -9;
            float dx = q.x - px, dy = q.y - py;
            return (px + dx * cs - dy * sn + bx, py + dx * sn + dy * cs + by);
        }
        P0 = Move(P0, 1); P1 = Move(P1, 0.6f);
        P2 = (P2.x + bx, P2.y + (p.Kind is "swim" or "lie" ? by : by * 0.5f));
        P3 = (P3.x + bx, P3.y + (p.Kind is "swim" or "lie" ? by : by * 0.5f));

        (float x, float y) At(float k)
        {
            float u = 1 - k;
            return (u * u * u * P0.x + 3 * u * u * k * P1.x + 3 * u * k * k * P2.x + k * k * k * P3.x,
                    u * u * u * P0.y + 3 * u * u * k * P1.y + 3 * u * k * k * P2.y + k * k * k * P3.y);
        }
        for (int i = 0; i <= 60; i++)
        {
            float k = i / 60f;
            var (x, y) = At(k);
            c.Disc(x, y, 3.1f - 2.2f * k, 'S', under: true);
        }
        // Scales: little arcs in rows along the tail.
        for (int y = -30; y <= 4; y++)
            for (int x = -30; x <= -6; x++)
                if (c.Get(x, y) == 'S' && ((x + 40 + (y + 40) / 2 % 2 * 2) % 4 == 0) && (y + 40) % 2 == 0) c.Set(x, y, 's');

        // The fan: two lobes spread from the end of the tail, with rays and pale tips.
        var e = At(1);
        var q = At(0.92f);
        float ang = MathF.Atan2(e.y - q.y, e.x - q.x), spread = 0.95f + MathF.Sin(t * 3.1f) * 0.08f;
        float len = p.Kind == "gallop" ? 7.5f : 7f;
        (float x, float y) Tip(float da, float l) => (e.x + MathF.Cos(ang + da) * l, e.y + MathF.Sin(ang + da) * l);
        var up = Tip(-spread, len); var dn = Tip(spread, len - 0.5f); var notch = Tip(0, 2.6f);
        c.Tri(e.x, e.y, up.x, up.y, notch.x, notch.y, 'F');
        c.Tri(e.x, e.y, dn.x, dn.y, notch.x, notch.y, 'F');
        c.Thick(e.x, e.y, up.x, up.y, 1, 'f'); c.Thick(e.x, e.y, dn.x, dn.y, 1, 'f');
        var mid1 = Tip(-spread * 0.5f, len * 0.8f); var mid2 = Tip(spread * 0.5f, len * 0.75f);
        c.Thick(e.x, e.y, mid1.x, mid1.y, 1, 'f'); c.Thick(e.x, e.y, mid2.x, mid2.y, 1, 'f');
        c.Set((int)MathF.Floor(up.x), (int)MathF.Floor(up.y), 'g'); c.Set((int)MathF.Floor(dn.x), (int)MathF.Floor(dn.y), 'g');
        // A little fin along the top of the tail, near the fan.
        var r1 = At(0.62f); var r2 = At(0.78f);
        c.Under((int)MathF.Floor(r1.x), (int)MathF.Floor(r1.y) - 2, 'F'); c.Under((int)MathF.Floor(r2.x), (int)MathF.Floor(r2.y) - 2, 'F');
    }

    // Light from above: body pixels with nothing above them catch it, ones with nothing below fall into shade.
    static void Light(MountCanvas c)
    {
        Array.Copy(c.M, c.Tmp, c.M.Length);
        char At(int x, int y) => MountCanvas.In(x, y) ? c.Tmp[(y + MountCanvas.OY) * MountCanvas.W + x + MountCanvas.OX] : '.';
        for (int y = -MountCanvas.OY + 1; y < MountCanvas.H - MountCanvas.OY - 1; y++)
            for (int x = -MountCanvas.OX + 1; x < MountCanvas.W - MountCanvas.OX - 1; x++)
            {
                char m = At(x, y);
                if (m == 'B')
                {
                    if (!Bodyish(At(x, y - 1))) c.Set(x, y, 'h');
                    else if (!Bodyish(At(x, y + 1)) && At(x, y + 1) != 'F') c.Set(x, y, 'b');
                }
                else if (m == 'S')
                {
                    if (!Bodyish(At(x, y - 1))) c.Set(x, y, 't');
                    else if (!Bodyish(At(x, y + 1))) c.Set(x, y, 's');
                }
            }
    }

    /* ---------- Front and back ---------- */

    // Coming toward you: the long face narrowing to a pale muzzle, gold eyes at the sides of the brow, the ears; its
    // top-left corner is at (-4, -28). The rider sits behind it.
    static readonly string[] FaceFront =
    {
        "B.....B",   // -29
        "BB...BB",
        ".BBBBB.",
        "EBBBBBE",
        ".BBBBB.",   // -25
        ".BBBBB.",
        "..BBB..",
        "..BBB..",
        "..BBB..",   // -21
        ".LLLLL.",
        ".LKLKL.",
        "..lll..",
    };

    // Ridden, its head is held lower and toward you, so it's foreshortened.
    static readonly string[] FaceShort =
    {
        "B.....B",
        "BB...BB",
        ".BBBBB.",
        "EBBBBBE",
        ".BBBBB.",
        "..BBB..",
        ".lllll.",
        ".lblbl.",
        "..lll..",
    };

    // Its chest from the front (top-left at (-6, -16)), and its croup from behind (top-left at (-6, -15)).
    static readonly string[] ChestFront =
    {
        "....BBBBB....",   // -16
        "..BBBBBBBBB..",
        ".BBBBBBBBBBB.",
        ".BBBBBBBBBBB.",
        "BBBBBBBBBBBBB",   // -12
        "BBBBBBBBBBBBB",
        ".BBBBBBBBBBB.",
        ".BBBLLLLLBBB.",
        "..BBLLLLLBB..",   // -8
        "....lllll....",
    };
    static readonly string[] CroupBack =
    {
        "...BBBBBBB...",   // -15
        "..BBBBBBBBB..",
        ".BBBBBBBBBBB.",
        ".BBBBBBBBBBB.",
        ".BBBBBBBBBBB.",   // -11
        ".BBBBBBBBBBB.",
        "..BBBBBBBBB..",
        "...BBBBBBB...",   // -8
    };

    // Going away: the back of the head with its ears (top-left at (-3, -27)).
    static readonly string[] HeadBack =
    {
        "B.....B",
        "BB...BB",
        ".BBBBB.",
        ".BBBBB.",
        "..BBB..",
    };

    // Two coral horns, one over each ear, branching outward.
    static void HornsEnd(MountCanvas c, int y)
    {
        foreach (int s in new[] { -1, 1 })
        {
            c.Under(2 * s, y - 1, 'c'); c.Under(2 * s, y - 2, 'C'); c.Under(3 * s, y - 3, 'C'); c.Under(4 * s, y - 4, 'C');
            c.Under(2 * s, y - 4, 'C');
        }
    }

    // A foreleg seen end on: lift raises the hoof as it reaches.
    static void LegEnd(MountCanvas c, int lx, int lift, char mat, int fin)
    {
        c.Thick(lx + 0.5f, -8, lx + 0.5f, -2 - lift, 2, mat, under: true);
        c.Under(lx, -1 - lift, 'O'); c.Under(lx + 1, -1 - lift, 'O');
        if (fin != 0) c.Under(fin < 0 ? lx - 1 : lx + 2, -2 - lift, 'F');
    }

    // "Front": the near layer (the head) is drawn after the rider. "Back": the tail comes toward you and is the near layer.
    public static void BuildEnd(MountCanvas c, MountPose p, bool away, bool near)
    {
        c.Clear();
        int f = p.Frame;
        float t = p.Time + p.Seed * 7;
        bool swim = p.Kind == "swim", moving = p.Kind is "gallop" or "trot";
        int bob = p.Kind == "gallop" ? GallopBody[f % 8] : p.Kind == "trot" ? TrotBody[f % 6] : 0;
        int sink = swim ? 3 : 0;
        int sway = (int)MathF.Round(MathF.Sin(t * (moving ? 9 : 3)) * (moving ? 1.5f : 0.8f));
        int Lift(int side) => p.Kind == "gallop" ? Math.Max(0, (int)MathF.Round(MathF.Sin((f + (side > 0 ? 2 : 0)) / 8f * MathF.Tau) * 2.5f))
            : p.Kind == "trot" ? ((f / 3 + (side > 0 ? 1 : 0)) % 2) * 2 : 0;
        // Ridden, it carries its head lower (the rider's face shows over it from the front, and from behind its ears and
        // horns peek past the rider's shoulders), and seen from the front the head comes toward you, so it's shorter.
        int headDy = (p.Kind == "gallop" && f % 8 is 3 or 4 ? 1 : 0) + (p.Ridden ? (away ? 7 : 10) : 0);
        bool low = p.Ridden && !away;
        if (!away && !near)
        {
            // Behind it: the fan of its tail, raised over its back.
            int lift = p.Kind == "gallop" ? (int)MathF.Round(GallopTail[f % 8]) : swim ? (int)MathF.Round(MathF.Sin(f / 6f * MathF.Tau) * 1.5f) : 0;
            int fy = -17 + lift, fx = sway;
            if (!low)
            {
                c.Tri(fx, fy, fx - 5, fy - 5, fx - 1, fy - 3, 'F'); c.Tri(fx, fy, fx + 5, fy - 5, fx + 1, fy - 3, 'F');
                c.Thick(fx, fy, fx - 5, fy - 5, 1, 'f'); c.Thick(fx, fy, fx + 5, fy - 5, 1, 'f');
                c.Set(fx - 5, fy - 5, 'g'); c.Set(fx + 5, fy - 5, 'g');
            }
            if (!swim) { LegEnd(c, -4, Lift(-1), 'b', -1); LegEnd(c, 2, Lift(1), 'B', 1); }
            c.Stamp(ChestFront, -6, -16);
            // The mane falls to either side of its neck, below the jaw.
            for (int y = -24; y <= -16 && !low; y++)
            {
                float w = MathF.Sin(t * (moving ? 10 : 3) + y * 0.8f);
                c.Set(-3 - (y % 3 == 0 && w > 0.3f ? 1 : 0), y + headDy, 'M');
                c.Set(3 + (y % 3 == 1 && w < -0.3f ? 1 : 0), y + headDy, 'm');
            }
            if (p.Saddle) { c.Set(-6, -12, 'Y'); c.Set(6, -12, 'Y'); c.Set(-6, -13, 'N'); c.Set(6, -13, 'N'); }
            // Its horns, behind the rider's head.
            if (low) HornsEnd(c, -28 + headDy);
            foreach (var (sx, sy) in new[] { (-4, -12), (4, -11), (-2, -9) }) c.Set(sx, sy, ((int)(t * 1.6f) + sx) % 5 == 0 ? 'w' : '*');
        }
        else if (!away)
        {
            if (low) c.Stamp(FaceShort, -3, -29 + headDy);
            else { c.Stamp(FaceFront, -3, -29 + headDy); HornsEnd(c, -28 + headDy); }
            // The forelock down the brow, and a blink now and then.
            c.Set(0, -27 + headDy, 'M'); c.Set(0, -26 + headDy, 'M'); c.Set(-1, -25 + headDy, 'm');
            if ((t * 0.27f) % 1 < 0.035f) { c.Set(-3, -26 + headDy, 'b'); c.Set(3, -26 + headDy, 'b'); }
        }
        else if (!near)
        {
            // From behind: the back of the head and the neck with its mane, the croup, hooves just showing.
            if (!swim) { LegEnd(c, -4, Lift(-1) / 2, 'd', 0); LegEnd(c, 2, Lift(1) / 2, 'd', 0); }
            for (int y = -23; y <= -14; y++) for (int x = y > -17 ? -3 : -2; x <= (y > -17 ? 3 : 2); x++) c.Set(x, y + (y < -18 ? headDy : 0), 'B');
            c.Stamp(HeadBack, -3, -27 + headDy);
            HornsEnd(c, -26 + headDy);
            for (int y = -25; y <= -14; y++)
            {
                int wob = ((y + (int)(t * (moving ? 10 : 3))) % 4 == 0) ? (sway > 0 ? 1 : -1) : 0;
                c.Set(wob, y + (y < -18 ? headDy : 0), y % 3 == 0 ? 'm' : 'M');
                if (y > -23 && y % 2 == 0) c.Set(wob + (y % 4 == 0 ? 1 : -1), y + (y < -18 ? headDy : 0), 'M');
            }
            c.Stamp(CroupBack, -6, -15);
            foreach (var (sx, sy) in new[] { (-4, -11), (4, -12), (2, -9) }) c.Set(sx, sy, ((int)(t * 1.6f) + sx) % 5 == 0 ? 'w' : '*');
            if (p.Saddle) for (int x = -3; x <= 3; x++) { c.Set(x, -15, 'Y'); c.Set(x, -14, 'N'); c.Set(x, -13, 'R'); }
        }
        else
        {
            // The tail lies along the ground toward you, swinging, and spreads into the fan.
            int lift = p.Kind == "gallop" ? -(int)MathF.Round(GallopTail[f % 8]) : swim ? (int)MathF.Round(MathF.Sin(f / 6f * MathF.Tau) * 1.5f) : 0;
            float mx = sway * 0.6f;
            for (int i = 0; i <= 24; i++)
            {
                float k = i / 24f, u = 1 - k;
                float x = u * u * 0 + 2 * u * k * mx + k * k * sway, y = u * u * -9 + 2 * u * k * -3 + k * k * (2 - lift);
                c.Disc(x + 0.5f, y + 0.5f, 3f - 1.8f * k, 'S');
            }
            int fy = 2 - lift;
            c.Tri(sway, fy, sway - 6, fy + 3, sway - 1, fy + 4, 'F'); c.Tri(sway, fy, sway + 6, fy + 3, sway + 1, fy + 4, 'F');
            c.Thick(sway, fy, sway - 6, fy + 3, 1, 'f'); c.Thick(sway, fy, sway + 6, fy + 3, 1, 'f');
            c.Thick(sway, fy, sway - 3, fy + 4, 1, 'f'); c.Thick(sway, fy, sway + 3, fy + 4, 1, 'f');
            c.Set(sway - 6, fy + 3, 'g'); c.Set(sway + 6, fy + 3, 'g');
            for (int y = -8; y <= fy; y++)
                for (int x = -4; x <= 4; x++)
                    if (c.Get(x, y) == 'S' && (x + y + 40) % 3 == 0 && y % 2 == 0) c.Set(x, y, 's');
        }
        c.Shift(0, bob + sink);
        Light(c);
    }

    public static (int x, int y) SeatEnd(MountPose p, bool away)
    {
        int f = p.Frame;
        int bob = p.Kind == "gallop" ? GallopBody[f % 8] : p.Kind == "trot" ? TrotBody[f % 6] : 0;
        int sink = p.Kind == "swim" ? 3 : 0;
        return away ? (0, -8 + bob + sink) : (0, -11 + bob + sink);
    }
}

partial class Game
{
    readonly MountCanvas mountCanvas = new();
    // Its speckles and eye, in world pixels, from the last time it was drawn: they glow after dark (DrawNight).
    readonly List<(int x, int y, bool eye, Color col)> mountGlints = new();

    // Draws a built canvas with its feet at (x, y), mirrored for dir -1, with the coloured outline. With a waterline
    // wl, pixels from that row down that land on water are left to the sea, with just a hint of the body showing through.
    // With clip (world pixels, inclusive) it redraws only that box: put back over a rider's legs.
    void BlitMount(MountCanvas c, int x, int y, int dir, bool flash = false, int wl = int.MaxValue, (int x0, int y0, int x1, int y1)? clip = null)
    {
        const int W = MountCanvas.W, H = MountCanvas.H, OX = MountCanvas.OX, OY = MountCanvas.OY;
        var white = Pal.C("#ffffff");
        var flashOut = Pal.C("#b9e8ee");
        for (int cy = 1; cy < H - 1; cy++)
            for (int cx = 1; cx < W - 1; cx++)
            {
                char m = c.M[cy * W + cx];
                int wx = dir > 0 ? x + cx - OX : x - (cx - OX), wy = y + cy - OY;
                if (clip is { } k && (wx < k.x0 || wx > k.x1 || wy < k.y0 || wy > k.y1)) continue;
                bool under = wy >= wl && Wet(ShapePx(wx, wy));
                if (m != '.')
                {
                    var col = flash ? white : MountArt.Colors[m];
                    if (under) pix.Rect(wx, wy, 1, 1, Pal.WithAlpha(col, m is 'M' or 'm' or 'F' or 'g' ? 0.22f : 0.16f));
                    else
                    {
                        pix.Rect(wx, wy, 1, 1, col);
                        if (m is '*' or 'w' or 'E') mountGlints.Add((wx, wy, m == 'E', col));
                    }
                    continue;
                }
                if (under) continue;
                // An outline where it meets the empty canvas, coloured by the darkest neighbour.
                char best = '.';
                int rank = 0;
                void Near(char q) { int r = MountArt.OutlineRank(q); if (r > rank) { rank = r; best = q; } }
                Near(c.M[cy * W + cx - 1]); Near(c.M[cy * W + cx + 1]); Near(c.M[(cy - 1) * W + cx]); Near(c.M[(cy + 1) * W + cx]);
                if (rank > 0) pix.Rect(wx, wy, 1, 1, flash ? flashOut : MountArt.OutlineOf(best));
            }
    }
}
