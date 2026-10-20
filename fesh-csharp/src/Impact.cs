using Raylib_cs;

namespace Fesh;

// What a big hit feels like (1.43): a shake, a flash over the world, and a very short pause (hit-stop). Only with light on
// (Settings > Graphics), the shake and the pause only with Screen shake on and Reduce motion off, and Reduce motion turns
// the flash into a steady thin edge. The pause stops the game's simulation and its input, not music, the pause menu, the
// focus check or any real-time guard; it ends on a real-time deadline and can't chain.
partial class Game
{
    float screenFlash;
    Color flashColour = Color.White;
    double stopUntil, stopCool;
    const float MaxHitStop = 0.07f;

    void Impact(float shake, string flash = null, float flashAlpha = 0, float stop = 0)
    {
        if (LightMode == 0 || MatchActive || mode is "title" or "create") return;
        if (shake > 0 && Settings.Data.shake && !Calm) quake = Math.Max(quake, shake);
        if (flash != null && flashAlpha > 0)
        {
            if (flashAlpha > screenFlash) flashColour = Pal.C(flash);
            screenFlash = Math.Max(screenFlash, flashAlpha);
        }
        double now = Raylib.GetTime();
        if (stop > 0 && Settings.Data.shake && !Calm && now >= stopCool)
        {
            stopUntil = now + Math.Min(stop, MaxHitStop);
            stopCool = stopUntil + 0.25;
        }
    }

    // True while a hit-stop is on: the frame skips the simulation and its input.
    bool HitStopped() => stopUntil > 0 && Raylib.GetTime() < stopUntil && mode == "play" && LightMode > 0 && !Calm;

    void ClearImpact() { screenFlash = 0; stopUntil = stopCool = 0; }

    // Over the finished world: a flash fades out, or with Reduce motion a steady edge for the same moment.
    void DrawScreenFlash()
    {
        if (screenFlash <= 0.01f || scene != "world") return;
        if (Calm)
        {
            var edge = Pal.WithAlpha(flashColour, Math.Min(0.8f, screenFlash * 2.2f));
            pix.Fill(0, 0, W, 3, edge); pix.Fill(0, H - 3, W, 3, edge); pix.Fill(0, 0, 3, H, edge); pix.Fill(W - 3, 0, 3, H, edge);
            return;
        }
        pix.Fill(0, 0, W, H, Pal.WithAlpha(flashColour, screenFlash));
        // The corners take it hardest, so a hit reads as something that struck you, not a fade.
        for (int i = 1; i <= 3; i++)
        {
            var c = Pal.WithAlpha(flashColour, screenFlash * 0.25f);
            pix.Fill(0, 0, W, i * 3, c); pix.Fill(0, H - i * 3, W, i * 3, c); pix.Fill(0, 0, i * 3, H, c); pix.Fill(W - i * 3, 0, i * 3, H, c);
        }
    }
}
