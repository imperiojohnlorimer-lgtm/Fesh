using Raylib_cs;

namespace Fesh;

// Bridges and jetties (1.27). Every deck tile used to be the same eight strokes, so a long bridge read as one picture pasted
// over and over, with a blue edge that matched nothing. Now the planks belong to the run, not the tile: each is its own board
// (a tone, a seam, sometimes a short one with the dark underside showing, a knot, a worn patch), the rails are lighter on the
// far side, posts and piles stand every third tile with foam at the waterline, the bridge throws a soft shadow on the water to
// the south-east (the light is from the north-west), and where it meets land the boards run a little way onto the sand.
partial class Game
{
    static readonly string[] PlankTones = { "#9a6a3a", "#8e6234", "#a77544", "#84582f", "#9d7040" };

    // When RenderTile repaints a ring of tiles, a deck beside it may only add its shadow and landing to the pixels that were cleared (or
    // they would be darkened again each time); the first full pass has no clip.
    (int x0, int y0, int x1, int y1)? bridgeClip;

    bool IsDeck(int x, int y) => x >= 0 && y >= 0 && x < COLS && y < ROWS && worldMap[y, x] is 'd' or 'b' or 'x';

    void PaintBridge(int tx, int ty)
    {
        char t = worldMap[ty, tx];
        if (t is not ('d' or 'b')) return;
        var g = worldBase;
        bool horiz = t == 'd';
        int X = tx * T, Y = ty * T;
        // u runs along the bridge, v across it: v 1 is the near rail, 8 the far one, 2 to 7 the planks.
        void P(int u, int v, Color c)
        {
            int wx = horiz ? X + u : X + v, wy = horiz ? Y + v : Y + u;
            if (wx < 0 || wy < 0 || wx >= PW || wy >= PH) return;
            if (bridgeClip is var (cx0, cy0, cx1, cy1) && (wx < cx0 || wy < cy0 || wx >= cx1 || wy >= cy1)) return;
            g.Buf[wy * PW + wx] = c.A == 255 ? c : Pix.Mix(g.Buf[wy * PW + wx], c, c.A / 255f);
        }
        int crossIdx = horiz ? ty : tx;
        for (int u = 0; u < T; u++)
        {
            int w = horiz ? X + u : Y + u;
            int plank = Math.DivRem(w, 3, out int inPlank);
            double h = Pix.Hash(plank, crossIdx, 600);
            var tone = Pal.C(PlankTones[(int)(h * PlankTones.Length) % PlankTones.Length]);
            bool shortTop = Pix.Hash(plank, crossIdx, 601) < 0.08, shortBottom = Pix.Hash(plank, crossIdx, 602) < 0.08;
            for (int v = 1; v <= 8; v++)
            {
                Color c;
                if (v == 1) c = Pix.Mix(Pal.C("#b9895a"), Pal.C("#8a6239"), (float)Pix.Hash(w, crossIdx, 603) * 0.4f);   // the rail on the sunlit side
                else if (v == 8) c = Pix.Mix(Pal.C("#6b4424"), Pal.C("#4e3520"), (float)Pix.Hash(w, crossIdx, 604) * 0.5f);   // and in the shade
                else if ((v == 2 && shortTop) || (v == 7 && shortBottom)) c = Pal.C("#2f2418");   // a short board: the dark under the deck shows
                else
                {
                    c = inPlank == 0 ? Pix.Mix(tone, Pal.C("#3a2616"), 0.28f) : tone;   // a seam between boards
                    // Lit along its upper edge, a worn pale patch here and there, a knot now and then.
                    if (v == 2 && inPlank != 0) c = Pix.Mix(c, Color.White, 0.12f);
                    double hh = Pix.Hash(w, v * 7 + crossIdx, 605);
                    if (hh < 0.05) c = Pix.Mix(c, Pal.C("#d9b98a"), 0.35f);
                    else if (hh > 0.975) c = Pix.Mix(c, Pal.C("#2f2418"), 0.6f);
                }
                P(u, v, c);
            }
        }
        // Posts and piles every third tile: a cap above the rails and a pile down into the water with foam at its foot.
        if ((horiz ? tx : ty) % 3 == 1)
            foreach (int v in new[] { 0, 9 })
            {
                P(4, v, Pal.C("#5b3d24")); P(5, v, Pal.C("#46301c"));
                if (v == 9)
                {
                    P(4, 10, Pal.C("#3a2616")); P(5, 10, Pal.C("#2c1d10"));
                    P(3, 11, Color.White); P(6, 11, Pal.WithAlpha(Color.White, 0.7f));
                    P(3, 10, Pal.WithAlpha(Color.White, 0.5f)); P(6, 10, Pal.WithAlpha(Color.White, 0.5f));
                }
            }
        // The shadow on the water to the south-east, softer the further from the deck, only on water.
        for (int u = 0; u < T + 2; u++)
            for (int v = 9; v <= 11; v++)
            {
                int su = u + (v - 8);   // slanting away
                int wx = horiz ? X + su : X + v, wy = horiz ? Y + v : Y + su;
                char under = ShapePx(wx, wy);
                bool onLand = Shore(under);
                if (!(Wet(under) || under == '~' || onLand)) continue;
                float a = (v == 9 ? 0.30f : v == 10 ? 0.18f : 0.08f) * (onLand ? 0.7f : 1f);
                if (v == 11 && BayerAt(wx, wy) > 0.5f) continue;
                P2(wx, wy, Pal.Rgba(6, 26, 40, a));
            }
        // Where the deck meets land, the boards run a couple of pixels onto it, with a scatter of sand and dirt on the ends.
        bool landBefore = horiz ? !IsDeck(tx - 1, ty) && Shore(GroundAt(tx - 1, ty)) : !IsDeck(tx, ty - 1) && Shore(GroundAt(tx, ty - 1));
        bool landAfter = horiz ? !IsDeck(tx + 1, ty) && Shore(GroundAt(tx + 1, ty)) : !IsDeck(tx, ty + 1) && Shore(GroundAt(tx, ty + 1));
        void Landing(int u0, int dir)
        {
            for (int k = 0; k < 3; k++)
                for (int v = 2; v <= 7; v++)
                {
                    int wx = horiz ? X + (dir < 0 ? -1 - k : T + k) : X + v, wy = horiz ? Y + v : Y + (dir < 0 ? -1 - k : T + k);
                    if (Pix.Hash(wx, wy, 607) < 0.35 + k * 0.2) continue;   // the ends are ragged
                    P2(wx, wy, Pix.Mix(Pal.C(PlankTones[(wx + wy) % PlankTones.Length]), Pal.C("#d8bb7e"), 0.2f + k * 0.2f));
                }
        }
        if (landBefore) Landing(0, -1);
        if (landAfter) Landing(T, 1);
        void P2(int wx, int wy, Color c)
        {
            if (wx < 0 || wy < 0 || wx >= PW || wy >= PH) return;
            if (bridgeClip is var (cx0, cy0, cx1, cy1) && (wx < cx0 || wy < cy0 || wx >= cx1 || wy >= cy1)) return;
            g.Buf[wy * PW + wx] = c.A == 255 ? c : Pix.Mix(g.Buf[wy * PW + wx], c, c.A / 255f);
        }
    }
}

partial class Game
{
    static bool LandLetter(char c) => c is 's' or 'g' or 'j' or 'p' or 'n' or 'e' or 'D' or 't' or 'h' or 'f' or 'R' or 'y' or 'c' or 'C';

    // A deck tile standing on land: more of its neighbours (not counting the deck itself) are land than water, so the ground under
    // it is that land. (A deck over the sea has water either side, and the tile where it meets the beach counts as land.)
    bool LandDeck(int x, int y)
    {
        int land = 0, water = 0;
        // Only the two sides count (across the run): what lies along it is more deck, or where it ends.
        var sides = worldMap[y, x] == 'd' ? new[] { (0, 1), (0, -1) } : new[] { (1, 0), (-1, 0) };
        foreach (var (ox, oy) in sides)
        {
            int nx = x + ox, ny = y + oy;
            if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS) continue;
            char c = worldMap[ny, nx];
            if (c is 'd' or 'b' or 'x') continue;
            if (LandLetter(c)) land++; else if (c is '~' or 'w' or 'l' or 'o' or 'T' or 'I' or 'r') water++;
        }
        return land > 0 && land >= water;
    }
}
