using Raylib_cs;

namespace Fesh;

// Reduce motion and high contrast (1.42, Settings > Graphics). Reduce motion keeps what the game tells you (warnings, hits,
// timing) but stops the picture shaking, flashing, swaying and bobbing: each telegraph stays on screen as a steady shape.
partial class Game
{
    internal static bool Calm => Settings.Data.reduceMotion;

    // A drawn tick (right) or cross (wrong) about 2r wide, for the places a right or wrong answer was only a green or a red.
    static void DrawVerdict(float cx, float cy, float r, bool right, Color c, float thick = 2)
    {
        if (right)
        {
            Gfx.Line(cx - r * 0.6f, cy, cx - r * 0.15f, cy + r * 0.5f, thick, c);
            Gfx.Line(cx - r * 0.15f, cy + r * 0.5f, cx + r * 0.7f, cy - r * 0.55f, thick, c);
        }
        else
        {
            Gfx.Line(cx - r * 0.55f, cy - r * 0.55f, cx + r * 0.55f, cy + r * 0.55f, thick, c);
            Gfx.Line(cx - r * 0.55f, cy + r * 0.55f, cx + r * 0.55f, cy - r * 0.55f, thick, c);
        }
    }
}
