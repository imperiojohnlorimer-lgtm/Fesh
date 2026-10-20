using Raylib_cs;

namespace Fesh;

// How a catch looks when it lands (1.43). Everything here is for light on (Settings > Graphics); with light off a catch is
// the old quiet landing. One snapshot is taken at each landing and the splash, the held-up fish and the card all read it.
sealed class CatchSnap { public string Id; public int Tier; public float M = 1, X, Y; public bool Perfect; }
sealed class SplashRing { public float X, Y, Age, Life, R, Strength; public bool Gold; }

partial class Game
{
    CatchSnap catchSnap;
    readonly List<SplashRing> splashRings = new();
    float heldPeak;   // the longest the current held-up catch is meant to stay up, so its rise-in plays over the right time

    // 0 a common fish, 1 a big one (or a perfect hook), 2 a rare one, 3 exotic, odd or legendary.
    static int CatchTierOf(bool rare, bool special, float m, bool perfect) => special ? 3 : rare ? 2 : m >= 1.25f || perfect ? 1 : 0;

    // Remembers one landing, and splashes where the line came out of the water.
    void NoteCatch(string id, int tier, float m, float x, float y, bool perfect)
    {
        catchSnap = new CatchSnap { Id = id, Tier = tier, M = m, X = x, Y = y, Perfect = perfect };
        CatchSplash(x, y, tier, m);
    }

    // Two rings spreading out on the water and a fan of spray. A bigger fish splashes bigger, a rare one adds a gold ring.
    void CatchSplash(float x, float y, int tier, float m)
    {
        if (LightMode == 0 || scene == "title") return;
        float size = 1 + 0.25f * tier + Math.Clamp((m - 1) * 0.6f, 0, 0.4f);
        splashRings.Add(new SplashRing { X = x, Y = y, Life = 0.7f, R = 9 * size, Strength = 0.8f });
        splashRings.Add(new SplashRing { X = x, Y = y, Life = 0.95f, R = 14 * size, Strength = 0.5f, Age = -0.12f });
        if (tier >= 2) splashRings.Add(new SplashRing { X = x, Y = y, Life = 1.1f, R = 20 * size, Strength = 0.8f, Gold = true, Age = -0.05f });
        int drops = 7 + 3 * tier + (m > 1.25f ? 4 : 0);
        for (int i = 0; i < drops; i++)
        {
            float a = FxRand(-2.5f, -0.64f);   // a fan upward
            float sp = FxRand(22, 46) * (0.8f + 0.2f * size);
            particles.Add(new Particle
            {
                X = x + FxRand(-2, 2), Y = y, Vx = MathF.Cos(a) * sp, Vy = MathF.Sin(a) * sp, Life = FxRand(0.4f, 0.75f),
                Color = tier >= 2 && i % 3 == 0 ? "#ffe28a" : i % 2 == 0 ? "#eef9fb" : "#bfe3ec", Soft = true, Size0 = FxRand(1.5f, 2.2f), Size1 = 1, Alpha0 = 0.95f, Gy = 150
            });
        }
    }

    void UpdateCatchFx(float dt)
    {
        for (int i = splashRings.Count - 1; i >= 0; i--)
        {
            var r = splashRings[i];
            r.Age += dt;
            if (r.Age >= r.Life) splashRings.RemoveAt(i);
        }
        if (heldT > heldPeak) heldPeak = heldT;
        if (heldT <= 0) { heldPeak = 0; if (heldItem == null) catchSnap = null; }
    }

    // Over the water, with the other effects there. Rings are drawn flat (the sea is seen from above and a bit to the side).
    void DrawSplashRings()
    {
        if (LightMode == 0) { splashRings.Clear(); return; }   // light switched off while rings were out
        foreach (var r in splashRings)
        {
            if (r.Age < 0) continue;
            float k = r.Age / r.Life, rad = 2 + r.R * (1 - (1 - k) * (1 - k));
            float a = r.Strength * (1 - k);
            pix.Ring(r.X, r.Y, rad, r.Gold ? Pal.Rgba(255, 226, 138, a) : Pal.Rgba(238, 249, 251, a));
            if (k < 0.5f) pix.Ring(r.X, r.Y, rad * 0.62f, Pal.Rgba(238, 249, 251, a * 0.6f));
        }
    }

    // The held-up fish's glow and sparkles: gold for a rare one, brighter for a legend, a faint white for a big one.
    // They are steady with Reduce motion (no pulse, no twinkle).
    void DrawHeldGlow(float x, float y)
    {
        if (LightMode == 0 || catchSnap is not { } cs || cs.Id != heldItem || cs.Tier < 1) return;
        float pulse = Calm ? 1 : 1 + 0.18f * MathF.Sin(time * 4);
        byte r = 255, g = cs.Tier >= 2 ? (byte)214 : (byte)240, b = cs.Tier >= 2 ? (byte)120 : (byte)255;
        LightGlow(x, y + 1, 8 + cs.Tier * 3, r, g, b, 0.18f + 0.08f * cs.Tier, (0.18f + 0.1f * cs.Tier) * pulse);
        if (cs.Tier < 2) return;
        int stars = 3 + cs.Tier;
        for (int s = 0; s < stars; s++)
        {
            float ang = (Calm ? 0 : time * 0.9f) + s * MathF.Tau / stars, rad = 9 + 2 * (s % 2);
            if (!Calm && (time * 2.4f + s * 0.37f) % 1 > 0.7f) continue;   // twinkles in and out
            float sx = x + MathF.Cos(ang) * rad, sy = y + MathF.Sin(ang) * rad * 0.6f;
            pix.Rect(sx, sy, 1, 1, "#ffffff");
            pix.Rect(sx - 1, sy, 1, 1, "rgba(255,226,138,0.7)"); pix.Rect(sx + 1, sy, 1, 1, "rgba(255,226,138,0.7)");
            pix.Rect(sx, sy - 1, 1, 1, "rgba(255,226,138,0.7)"); pix.Rect(sx, sy + 1, 1, 1, "rgba(255,226,138,0.7)");
        }
    }

    // A soft gold halo round a card's picture: a few rings fading outward (looks only, so a few fills).
    void CardGlow(float x, float y, float w, float h, float strength = 1)
    {
        if (LightMode == 0) return;
        float pulse = Calm ? 0.12f : 0.12f + 0.05f * MathF.Sin(time * 3);
        for (int g = 1; g <= 4; g++)
            Gfx.Rect(x - g * 5, y - g * 5, w + g * 10, h + g * 10, Pal.WithAlpha(Pal.Lantern, (pulse * strength) / g), 6 + g * 3);
    }
}
