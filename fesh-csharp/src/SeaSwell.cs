using Raylib_cs;

namespace Fesh;

// One swell for the sea (1.40, Light: Soft or Full) and an index of the coast.
//
// The swell is a slow wave travelling downwind (80 px between crests, one every 5 seconds), so the foam, the wash and the
// boats all rise and fall together instead of each keeping its own sine. It only ever moves things that were already
// moving: never the coast, the wading line, a fishing spot, the bobber or where anyone really stands.
//
// The coast list (`shore`) is every water pixel beside land, in row order. Foam, wash and the moonlit surf all walked the
// whole of it every frame; `ShoreInView` gives just the part near the view, in the same order, so what they draw is
// exactly what it was (and the Off look with it).
partial class Game
{
    /* ---------- The swell ---------- */
    // The amihan blows from the north-east (so the swell runs south-west), the habagat the other way.
    (float x, float y) SwellDir => Season == "amihan" ? (-0.7071f, 0.7071f) : (0.7071f, -0.7071f);

    // -1 to 1: a crest where it is 1. Sampled once per boat, shore segment or reflection row, never per pixel.
    internal float SeaSwell(float wx, float wy, float t)
    {
        var (dx, dy) = SwellDir;
        return MathF.Sin(MathF.Tau * ((wx * dx + wy * dy) / 80f - t / 5f));
    }

    // How far a hull rides it: calm about a pixel, a storm about two (the old bob's own sizes), the swell carrying most of it.
    internal int BoatBobAt(float t, float wx, float wy, bool underWay)
    {
        if (Calm) return 0;
        if (LightMode == 0) return BoatBob(t);
        float amp = (underWay ? 1.1f : 0.8f) + 0.8f * Math.Clamp(stormAmt, 0, 1);
        float wave = SeaSwell(wx, wy, t) * 0.75f + MathF.Sin(t * (underWay ? 3.2f : 2)) * 0.25f;
        return (int)MathF.Round(wave * amp);
    }

    /* ---------- The coast index ---------- */
    const int ShoreCell = 32, ShoreMargin = 12;
    List<int>[] shoreCells;
    int shoreCellsW, shoreCellsH;
    int[] shoreView = Array.Empty<int>();
    (int x, int y, int w, int h, int n) shoreViewKey = (int.MinValue, 0, 0, 0, -1);

    // Called when the coast is found again (FindShore).
    void IndexShore()
    {
        shoreCellsW = PW / ShoreCell + 1; shoreCellsH = PH / ShoreCell + 1;
        shoreCells = new List<int>[shoreCellsW * shoreCellsH];
        for (int i = 0; i < shore.Count; i++)
        {
            int c = (shore[i].y / ShoreCell) * shoreCellsW + shore[i].x / ShoreCell;
            (shoreCells[c] ??= new List<int>()).Add(i);
        }
        shoreViewKey = (int.MinValue, 0, 0, 0, -1);
    }

    // Indices into `shore` for everything within a margin of the view, ascending (the order of the full list).
    internal int[] ShoreInView()
    {
        var key = (camX, camY, W, H, shore.Count);
        if (key == shoreViewKey) return shoreView;
        shoreViewKey = key;
        if (shoreCells == null) IndexShore();
        int cx0 = Math.Max(0, (camX - ShoreMargin) / ShoreCell - (camX - ShoreMargin < 0 ? 1 : 0)), cx1 = Math.Min(shoreCellsW - 1, (camX + W + ShoreMargin) / ShoreCell);
        int cy0 = Math.Max(0, (camY - ShoreMargin) / ShoreCell - (camY - ShoreMargin < 0 ? 1 : 0)), cy1 = Math.Min(shoreCellsH - 1, (camY + H + ShoreMargin) / ShoreCell);
        var found = new List<int>();
        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
                if (shoreCells[cy * shoreCellsW + cx] is { } list) found.AddRange(list);
        found.Sort();
        return shoreView = found.ToArray();
    }
}
