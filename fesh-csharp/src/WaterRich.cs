using Raylib_cs;

namespace Fesh;

// A richer baked sea (1.40, Light: Soft or Full). The ordinary baked layer (worldBase) stays exactly as it was for Light: Off, the
// chart and every system that reads it. With light on, the outdoor view is copied from worldRich instead: the same picture except
// for the open water pixels, which are painted again from an expanded colour ramp (four shallow tones, seven deep ones, one
// Bayer decision between neighbours) with a quiet seabed on the shallows: pale sand ripples, weed, and muted coral round the
// reef islands. Only pixels that the ordinary bake painted as plain sea are touched, so piers, decks, the wading line's rim,
// dry flats, ponds and anything drawn over the sea (the Starwell, hoofprints) can never be overpainted. Everything is a function
// of the pixel's world position and the tile depth, so RenderTile (after a tree is chopped, say) repaints it exactly as a bake does.
partial class Game
{
    Pix worldRich;

    // The wading limit survives: every shallow tone is lighter than every deep one (checked), and the rim is left alone.
    static readonly Color[] ShallowRich = Tones("#3d97b8", "#388fb1", "#3487aa", "#2f7fa3");
    static readonly Color[] DeepRich = Tones("#266893", "#24638e", "#225e89", "#205a85", "#1f5580", "#1e5179", "#1c4d77");
    static readonly Color WeedTint = Pal.C("#1f6f63"), SandTint = Pal.C("#9ad7d3");
    static readonly Color[] CoralTints = Tones("#c8707f", "#d3a24d", "#6fae78");

    // The layer the live outdoor view starts from: the rich one with light on (built the first time it is wanted).
    internal Pix LiveBase()
    {
        if (scene != "world" || LightMode == 0) return basePix;
        if (worldRich == null) BakeRich();
        return worldRich;
    }

    void BakeRich()
    {
        worldRich = new Pix(worldBase.W, worldBase.H);
        Array.Copy(worldBase.Buf, worldRich.Buf, worldBase.Buf.Length);
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++) RichTile(x, y);
    }

    // One tile of the rich layer: the ordinary bake's pixel, or for plain open sea its richer colour.
    void RichTile(int x, int y)
    {
        if (worldRich == null) return;
        for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++)
            {
                int wx = x * T + px, wy = y * T + py, i = wy * worldBase.W + wx;
                var baked = worldBase.Buf[i];
                char s = shape[wy * PW + wx];
                if (s is 'w' or '~' && baked.Equals(GroundPixel(wx, wy, s))) worldRich.Buf[i] = RichSeaPixel(wx, wy, s, baked);
                else worldRich.Buf[i] = baked;
            }
        RichGroundDetail(x, y);   // the land's clusters, over the plain ground only (GroundRich.cs)
    }

    internal Color RichSeaPixel(int wx, int wy, char s, Color baked)
    {
        if (baked.Equals(ShallowRim)) return baked;   // the line where the shallows end, exactly as before
        float d = DepthAt(wx, wy);
        if (s == 'w') return SeabedPixel(RampAt(ShallowRich, Math.Min(d, 1) * 3, wx, wy), wx, wy);
        // Deep water: the same slow banks and holes as before, spread over seven tones, with a faint surface texture.
        float far = Math.Clamp((d - 2) / 2, 0, 1);
        float level = Math.Max(2, d + (VNoise(wx, wy, 48, 175) - 0.5f) * 1.2f * far);
        var c = RampAt(DeepRich, Math.Clamp(level - 2, 0, 3) * 2, wx, wy);
        int n = (int)MathF.Round((VNoise(wx, wy, 7, 196) - 0.5f) * 4);
        return n == 0 ? c : Shift(c, n);
    }

    static Color Shift(Color c, int n) => new((byte)Math.Clamp(c.R + n, 0, 255), (byte)Math.Clamp(c.G + n, 0, 255), (byte)Math.Clamp(c.B + n, 0, 255), (byte)255);

    // What lies on the bottom of the shallows: static patches several pixels wide, world-anchored. They keep three pixels
    // clear of the deep so the wading line's edge stays clean, and weed is never darker than the shallows' own range.
    Color SeabedPixel(Color c, int wx, int wy)
    {
        if (ShapePx(wx + 3, wy) == '~' || ShapePx(wx - 3, wy) == '~' || ShapePx(wx, wy + 3) == '~' || ShapePx(wx, wy - 3) == '~'
            || ShapePx(wx + 2, wy + 2) == '~' || ShapePx(wx - 2, wy + 2) == '~' || ShapePx(wx + 2, wy - 2) == '~' || ShapePx(wx - 2, wy - 2) == '~') return c;
        // Two scales of noise and a little dither, so the patches have ragged, organic edges rather than the grid's.
        float bayer = (BayerAt(wx, wy) - 0.5f) * 0.12f;
        float sand = VNoise(wx, wy, 18, 197) * 0.65f + VNoise(wx, wy, 7, 202) * 0.35f + bayer;
        if (sand > 0.56f)                                                                                          // pale sand with ripples
            return Pix.Mix(c, SandTint, (wx * 2 + wy) % 6 < 2 ? 0.42f : 0.26f);
        if (VNoise(wx, wy, 12, 198) * 0.7f + VNoise(wx, wy, 5, 199) * 0.3f + bayer > 0.62f) return Pix.Mix(c, WeedTint, 0.5f);   // weed
        int tx = Math.Clamp(wx / T, 0, COLS - 1), ty = Math.Clamp(wy / T, 0, ROWS - 1);
        if (biome[ty, tx] is 4 or 5 or 6 && VNoise(wx, wy, 13, 200) * 0.7f + VNoise(wx, wy, 5, 203) * 0.3f + bayer > 0.66f)                                       // coral round the reef islands
            return Pix.Mix(c, CoralTints[(int)(Pix.Hash(wx / 4, wy / 4, 201) * CoralTints.Length) % CoralTints.Length], 0.42f);
        return c;
    }
}
