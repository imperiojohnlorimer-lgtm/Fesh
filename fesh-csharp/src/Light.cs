using Raylib_cs;

namespace Fesh;

// Light and colour (1.39). Settings > Light and colour: Off is the look before 1.39 exactly, Soft and Full add real light
// (glows that brighten the picture), and the lamps come on at dusk. Everything works on the CPU picture (`pix`), so the
// Android app draws it the same way; none of it touches the baked ground layer or the chart's picture.
partial class Game
{
    // The autotest's scripts run with the effects off so every exact-pixel check keeps its colours; the light tests turn them on.
    internal static bool EffectsOff;

    // True only while RenderWorld is drawing the live view. The chart's texture is drawn through the same code onto a
    // world-sized picture that stays for hours, so it must never be lit or graded.
    bool liveRender;

    // 0 off, 1 soft, 2 full.
    internal int LightMode
    {
        get
        {
            if (EffectsOff) return 0;
            int m = RequestedLight;
            // Quality.cs may have stepped it down to keep a slow device smooth; the player's setting itself is untouched.
            return Quality.Rung >= 7 ? 0 : Quality.Rung >= 6 ? Math.Min(m, 1) : m;
        }
    }
    // What the player chose (automatic: Full on a PC, Soft on a phone).
    internal static int RequestedLight => Settings.Data.light >= 0 ? Settings.Data.light : Platform.Touch ? 1 : 2;

    // The darkened corners (Settings > Vignette): normal is the look before 1.39 (0.2 outdoors, 0.4 in the caves).
    float VignetteStrength
    {
        get
        {
            float f = EffectsOff ? 1 : Gfx.HighContrast ? 0 : Settings.Data.vignette switch { 0 => 0, 1 => 0.6f, _ => 1 };
            return (scene == "cave" ? 0.4f : 0.2f) * f;
        }
    }

    // How lit the lamps are, 0 to 1: the whole night, and from 18:00 an hour of dusk in which the windows, lanterns and
    // fires come on while the sky is still orange. (With light off the lamps wait for the dark, as before.)
    float LampK
    {
        get
        {
            float k = Darkness;
            if (LightMode == 0) return k;
            float c = state.clock;
            // The hour from 18:00 brings them up to full; from 21:00 the dark itself has taken over.
            if (c >= 18 * 60 && c < 21 * 60) k = Math.Max(k, Smooth((c - 18 * 60) / 60));
            return k;
        }
    }

    // A lamp's glow: with light off the old flat alpha glow (so nothing changes), otherwise light added to the picture.
    // `oldA` is the old alpha, `k` the strength of the new glow; soft is a little smaller and fainter than full.
    void LightGlow(double wx, double wy, double r, byte cr, byte cg, byte cb, float oldA, float k)
    {
        if (LightMode == 0) { pix.Glow(wx, wy, r, Pal.Rgba(cr, cg, cb, oldA)); return; }
        bool full = LightMode == 2;
        var colour = new Color(cr, cg, cb, (byte)255);
        pix.AddGlow(wx, wy, full ? r * 1.15 : r, colour, full ? k : k * 0.6f);
        // Bloom (Settings > Graphics): a wide, faint halo round the light itself. It comes from the lights, not from how
        // bright the picture is, so snow and sand never glow.
        if (BloomOn) pix.AddGlow(wx, wy, r * 2.4, colour, k * 0.22f);
    }

    internal bool BloomOn => LightMode > 0 && Settings.Data.bloom && Quality.Rung < 2;
    internal bool GrainOn => !EffectsOff && Settings.Data.grain && Quality.Rung < 1;

    // Film grain: every pixel moves one level up or down, in a pattern that changes about eight times a second.
    void ApplyGrain()
    {
        int frame = (int)(time * 8);
        var buf = pix.Buf;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ frame * 83492791);
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                int d = (int)(h % 3) - 1;
                if (d == 0) continue;
                var c = buf[y * W + x];
                buf[y * W + x] = new Color((byte)Math.Clamp(c.R + d, 0, 255), (byte)Math.Clamp(c.G + d, 0, 255), (byte)Math.Clamp(c.B + d, 0, 255), (byte)255);
            }
    }
}
