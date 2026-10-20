using Raylib_cs;

namespace Fesh;

// Ground detail for Light: Soft and Full (1.41): small clusters of flowers, clover, pebbles, shell fragments, tide wrack,
// ferns, moss, leaf litter, mushrooms, dry scrub and ice chips, painted into the rich layer (worldRich) after the sea. The
// ordinary baked ground, the chart and Light: Off never see any of it.
//
// A cluster is only ever painted onto a pixel that is plain ground: the baked pixel must equal what the ground alone would
// give there (so nothing already drawn over it, no hoofprint, stump, path or bridge shadow is touched), its neighbours must
// be about the same colour (so bank faces, steps, ledges and rims are left alone), its shape letter must be the tile's own
// ground (rounded coasts and blended borders are left alone) and it must lie in the tile's middle 8x8. Tiles of other letters
// (trees, boulders, the footpath `p`) get nothing. Everything is decided from `Pix.Hash` of the tile and from the map within
// two tiles, so RenderTile's rings repaint it exactly as a whole bake does (felling an oak changes its neighbours' litter).
//
// The silhouettes are chosen not to look like the loose things you can pick up (driftwood is a brown 6x2 bar, a stone a grey
// 4x2 block, a worm mound a lump): clusters are 1-2 px marks, flat litter and irregular fans, never a lone object.
partial class Game
{
    internal bool groundDecorOff;   // the tests build the rich layer without land decor as their baseline

    static readonly Color[] MeadowPetals = Tones("#f3e27a", "#ffffff", "#f2a5b5", "#c9b8f2", "#f2955f", "#9fd0f2");
    static readonly Color[] LitterTones = Tones("#b0763a", "#8f5a2c", "#c9a23a", "#9a6a34");
    static readonly Color[] WrackTones = Tones("#4d5b3a", "#6b6a3d", "#3d4a35");

    internal int TreeNeighbours(int x, int y, char[] kinds)
    {
        int n = 0;
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int tx = x + dx, ty = y + dy;
                if ((dx != 0 || dy != 0) && tx >= 0 && ty >= 0 && tx < COLS && ty < ROWS && Array.IndexOf(kinds, worldMap[ty, tx]) >= 0) n++;
            }
        return n;
    }

    static readonly char[] LeafTrees = { 't', 'f', 'h' };

    // Water within three pixels (or two on the diagonal): where the wash leaves weed on the sand.
    internal bool NearWater(int wx, int wy) => Wet(ShapePx(wx + 3, wy)) || Wet(ShapePx(wx - 3, wy)) || Wet(ShapePx(wx, wy + 3)) || Wet(ShapePx(wx, wy - 3)) || Wet(ShapePx(wx + 2, wy + 2)) || Wet(ShapePx(wx - 2, wy - 2));

    // Paints one pixel of a cluster if it is plain, calm ground of tile letter t inside the middle 8x8 (px, py are 0-9 in the tile).
    bool Deco(int x, int y, char t, int px, int py, Color c)
    {
        if (px < 1 || px > 8 || py < 1 || py > 8) return false;
        int wx = x * T + px, wy = y * T + py, i = wy * worldBase.W + wx;
        if (shape[wy * PW + wx] != t) return false;
        var b = worldBase.Buf[i];
        if (!worldRich.Buf[i].Equals(b) || !b.Equals(GroundPixel(wx, wy, t))) return false;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var n = worldBase.Buf[(wy + dy) * worldBase.W + wx + dx];
            if (Math.Abs(n.R - b.R) + Math.Abs(n.G - b.G) + Math.Abs(n.B - b.B) > 80) return false;
        }
        worldRich.Buf[i] = c;
        return true;
    }

    void RichGroundDetail(int x, int y)
    {
        if (groundDecorOff || worldRich == null) return;
        char t = worldMap[y, x];
        if (t is not ('g' or 'j' or 's' or 'D' or 'n') || groundCache[y, x] != t) return;
        if (stumps.ContainsKey((x, y)) || hoofprints.Contains((x, y))) return;
        // Ground you have walked bare comes first; a worn patch has no flowers on it.
        int wear = t is 'g' or 'j' ? WearStage(x, y) : 0;
        if (wear > 0) { PaintWear(x, y, t, wear); if (wear >= 2) return; }
        double r = Pix.Hash(x, y, 1000), r2 = Pix.Hash(x, y, 1001);
        int cx = 2 + (int)(Pix.Hash(x, y, 1002) * 5), cy = 2 + (int)(Pix.Hash(x, y, 1003) * 5);
        int trees = TreeNeighbours(x, y, LeafTrees);
        int X = x * T, Y = y * T;
        bool P(int ox, int oy, Color c) => Deco(x, y, t, cx + ox, cy + oy, c);
        switch (t)
        {
            case 'g':
                if (trees >= 2 && r2 < 0.07)
                {
                    // A toadstool: a red cap over a pale stalk.
                    P(0, 0, Pal.C("#c8473d")); P(1, 0, Pal.C("#c8473d")); P(0, 1, Pal.C("#efe6d2"));
                }
                else if (trees >= 1 && r < 0.14 + 0.07 * Math.Min(trees, 4))
                {
                    // Leaf litter under the trees: a loose scatter of orange, brown and yellow specks.
                    for (int k = 0; k < 4; k++)
                        P((int)(Pix.Hash(x, y, 1010 + k) * 5) - 2, (int)(Pix.Hash(x, y, 1020 + k) * 5) - 2, LitterTones[(int)(Pix.Hash(x, y, 1030 + k) * LitterTones.Length) % LitterTones.Length]);
                }
                else if (VNoise(X + 5, Y + 5, 70, 1040) > 0.55f && r < 0.3)
                {
                    // A drift of meadow flowers: three or four of one colour, a leaf under one.
                    var petal = MeadowPetals[(int)(Pix.Hash(x / 2, y / 2, 1041) * MeadowPetals.Length) % MeadowPetals.Length];
                    P(0, 0, petal); P(2, 1, petal); P(-1, 2, petal);
                    if (r2 < 0.5) P(1, -1, petal);
                    P(0, 1, Pal.C("#3f7a32"));
                }
                else if (r < 0.22) { P(0, 0, Pal.C("#46823a")); P(1, 1, Pal.C("#46823a")); P(-1, 1, Pal.C("#46823a")); }   // a clump of clover
                else if (r2 > 0.96) { P(0, 0, Pal.C("#a9aeb2")); P(1, 1, Pal.C("#7b8084")); }                               // a pebble sunk in the turf
                break;
            case 'j':
                if (r < 0.12)
                {
                    // A fern: a fan of fronds.
                    var dark = Pal.C("#2a5f2a"); var lit = Pal.C("#5aa047");
                    P(0, 0, lit); P(-1, 1, dark); P(1, 1, dark); P(-2, 2, dark); P(2, 2, dark); P(0, 1, dark);
                }
                else if (r < 0.22) { P(0, 0, Pal.C("#4f9a55")); P(1, 0, Pal.C("#4f9a55")); P(0, 1, Pal.C("#3f8548")); P(-1, 0, Pal.C("#3f8548")); }   // moss
                else if (trees >= 1 && r2 < 0.2)
                    for (int k = 0; k < 3; k++) P((int)(Pix.Hash(x, y, 1050 + k) * 5) - 2, (int)(Pix.Hash(x, y, 1060 + k) * 5) - 2, LitterTones[(int)(Pix.Hash(x, y, 1070 + k) * 2)]);
                break;
            case 's':
                // Tide wrack: strands of dark weed on the sand within three pixels of the water, thickest where the wash reaches.
                for (int py = 1; py <= 8; py++)
                    for (int px = 1; px <= 8; px++)
                    {
                        int wx = X + px, wy = Y + py;
                        if (!NearWater(wx, wy)) continue;
                        double wh = Pix.Hash(wx, wy, 1080);
                        if (wh < 0.13)
                        {
                            var col = WrackTones[(int)(Pix.Hash(wx / 3, wy / 2, 1081) * WrackTones.Length) % WrackTones.Length];
                            Deco(x, y, t, px, py, col);
                            if (Pix.Hash(wx, wy, 1082) < 0.5 && NearWater(wx + 1, wy)) Deco(x, y, t, px + 1, py, col);
                        }
                    }
                if (r < 0.08) { P(0, 0, Pal.C("#f6efe0")); P(1, 0, Pal.C("#d8c8a8")); }              // a broken shell
                else if (r2 < 0.07) { P(0, 0, Pal.C("#e9dcc0")); P(1, 1, Pal.C("#cdbb98")); P(-1, 1, Pal.C("#f1e6cd")); }   // a few shell fragments
                break;
            case 'D':
                if (r < 0.1) { P(0, 0, Pal.C("#a89a58")); P(-1, 1, Pal.C("#7a7440")); P(1, 1, Pal.C("#7a7440")); P(0, 1, Pal.C("#a89a58")); }   // dry scrub
                else if (r2 < 0.08) { P(0, 0, Pal.C("#f3d08f")); P(1, 0, Pal.C("#f3d08f")); P(2, 0, Pal.C("#f3d08f")); }                       // a windblown streak
                break;
            case 'n':
                if (r < 0.14) { P(0, 0, Pal.C("#c5dcec")); P(1, 0, Pal.C("#e8f4fb")); }                      // chips of ice
                else if (r2 < 0.06) { P(0, 0, Pal.C("#6b7a63")); P(1, 1, Pal.C("#55634f")); }                // a tuft pushing through the snow
                break;
        }
    }
}
