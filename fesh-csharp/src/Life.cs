using Raylib_cs;

namespace Fesh;

// Small things that make the islands feel alive: gulls overhead, butterflies over the grass, and Pip waving you over.
partial class Game
{
    // Pip bobs and blinks behind the stall, and waves when you come near.
    void DrawPip()
    {
        bool near = Dist(player.X, player.Y, PipX, PipY + 12) < 42;
        LookData.DrawPerson(pix, PipLook, (int)PipX, (int)PipY, "down", 0,
            bob: (time + 0.4f) % 2.6f > 1.6f ? 1 : 0, blink: (time + 2.1f) % 4.3f < 0.13f, arms: near ? 1 : 0, swing: (int)(time * 4) % 2);
    }

    // Gulls drift across the sky by day (their shadows sweep over the ground), and butterflies flutter over
    // Saltmere's and Mirewood's grass. Drawn in screen space, like the snow and rain.
    void DrawSkyLife(float t)
    {
        if (state.night) return;
        for (int i = 0; i < 12; i++)
        {
            double bx = Pix.Hash(i, 5, 87) * W + Math.Sin(t * 0.7 + i * 1.9) * 16 + Math.Sin(t * 2.3 + i) * 3;
            double by = Pix.Hash(i, 6, 87) * H + Math.Cos(t * 0.6 + i * 1.3) * 10;
            int sx = (int)bx, sy = (int)by;
            if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
            byte biome = BiomeAt((sx + camX) / T, (sy + camY) / T);
            char tile = TileAt((sx + camX) / T, (sy + camY) / T);
            if (biome is not (0 or 3) || tile is not ('g' or 'j')) continue;
            var wing = Pal.C(i % 3 == 0 ? "#f3c25b" : i % 3 == 1 ? "#ffffff" : "#e8939a");
            bool open = (int)(t * 9 + i) % 2 == 0;
            pix.Fill(sx, sy, 1, 1, Pal.C("#3b2a1d"));
            if (open) { pix.Fill(sx - 1, sy - 1, 1, 1, wing); pix.Fill(sx + 1, sy - 1, 1, 1, wing); }
            else { pix.Fill(sx - 1, sy, 1, 1, wing); pix.Fill(sx + 1, sy, 1, 1, wing); }
        }
        if (Stormy) return;
        var gull = Pal.C("#f2efe6");
        var shade = Pal.Rgba(0, 0, 0, 0.12f);
        for (int i = 0; i < 3; i++)
        {
            float speed = 14 + (float)Pix.Hash(i, 1, 81) * 10, span = W + 80;
            int sx = (int)((Pix.Hash(i, 2, 81) * span + t * speed) % span) - 40;
            int sy = (int)(Pix.Hash(i, 3, 81) * H * 0.6 + 8 + Math.Sin(t * 0.8 + i) * 5);
            bool up = (int)(t * 5 + i * 1.7f) % 2 == 0;
            pix.Fill(sx + 2, sy + 26, 5, 1, shade);
            pix.Fill(sx, sy, 2, 1, gull);
            pix.Fill(sx - 2, sy + (up ? -1 : 1), 2, 1, gull);
            pix.Fill(sx + 2, sy + (up ? -1 : 1), 2, 1, gull);
            pix.Fill(sx - 3, sy + (up ? -2 : 1), 1, 1, gull);
            pix.Fill(sx + 4, sy + (up ? -2 : 1), 1, 1, gull);
        }
    }
}
