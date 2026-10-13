using Raylib_cs;

namespace Fesh;

// Small things that make the islands feel alive: gulls overhead, butterflies over the grass, Pip waving you over,
// and Pip and Tomas keeping their own hours.
partial class Game
{
    // Pip keeps the stall open from 7 AM to 9 PM. Tomas goes to bed in his hut at 10 PM and is up again at 6 AM;
    // you can still go in and wake him (nothing in the story ever has to wait for morning). He isn't in bed while
    // he's away from his camp (the ending, down on the dock); pottering round it (Folk.cs) still counts as at camp.
    bool PipOpen => state.clock >= 7 * 60 && state.clock < 21 * 60;
    bool TomasInBed => (state.clock >= 22 * 60 || state.clock < DawnMin) && TomasAtCamp;
    // His pillow, in his hut (the bed is the first piece of TomasRoom).
    const float TomasBedX = 20, TomasBedY = 33;

    // Asleep under his blue blanket, with a few Zs drifting up.
    void DrawTomasAsleep()
    {
        int X = 10, Y = 20;
        pix.Rect(X + 3, Y + 2, 3, 2, "#1d2f45");
        pix.Rect(X + 3, Y + 4, 3, 2, "#e0b07d");
        pix.Rect(X + 3, Y + 5, 3, 1, "#e8e8e8");
        pix.Rect(X + 4, Y + 4, 1, 1, "#7a5a40");
        pix.Rect(X + 7, Y + 1, 9, 2, "#2f5d8a"); pix.Rect(X + 8, Y + 1, 7, 1, "#3f7da8");
        for (int i = 0; i < 3; i++)
        {
            float ph = (time * 0.5f + i / 3f) % 1;
            int zx = X + 6 + (int)(ph * 6) + i, zy = Y - (int)(ph * 12);
            var c = Pal.Rgba(232, 240, 255, 1 - ph);
            pix.Rect(zx, zy, 3, 1, c); pix.Rect(zx + 1, zy + 1, 1, 1, c); pix.Rect(zx, zy + 2, 3, 1, c);
        }
    }

    // Pip shuffles about behind the stall, bobs and blinks, and turns to wave when you come near.
    void DrawPip()
    {
        bool near = Dist(player.X, player.Y, PipX, PipY + 12) < 42;
        var s = pipWalk;
        int x = (int)MathF.Round(s.X == 0 ? PipX : s.X);
        LookData.DrawPerson(pix, PipLook, x, (int)PipY, near ? "down" : s.Face, s.Step,
            bob: !s.Moving && (time + 0.4f) % 2.6f > 1.6f ? 1 : 0, blink: (time + 2.1f) % 4.3f < 0.13f, arms: near ? 1 : 0, swing: near ? (int)(time * 4) % 2 : 0);
    }

}
