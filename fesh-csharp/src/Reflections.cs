using Raylib_cs;

namespace Fesh;

// Reflections in calm water (1.40, Light: Full only). The things that stand at the water's edge (trees, houses, the lighthouse,
// boats) are drawn once more, off screen, by the very same drawing code that draws them (swapping `pix` the way the chart
// does), and what that picture covers is mirrored onto the water below the object's foot: a few rows, darker and bluer,
// fading with depth, shimmering a little with the swell. A reflection is laid down in the water layer, before shadows, fishing
// spots and the objects themselves, so nothing is ever painted over; every pixel is tested against the exposed water, so
// piers, dry flats and land are left alone. It fades away as rain comes on and is gone in a storm, the tsunami and the eclipse.
partial class Game
{
    const int ReflRows = 18, ReflReach = 48;
    static readonly Color ReflSentinel = new(1, 2, 3, 255);
    static readonly Color ReflWater = new(28, 72, 112, 255);
    Pix reflPix;
    internal bool reflectOff;   // the tests turn reflections off to compare a finished picture with and without them
    bool reflecting;   // true while a source is drawn for its reflection: boats leave out their wake, rings and shadow

    // How much of a reflection shows, 0 to 1.
    internal float ReflectK
    {
        get
        {
            if (LightMode < 2 || Quality.Rung >= 3 || scene != "world" || Stormy || SeaEmergency || eclipse != null) return 0;
            return Math.Clamp(1 - rainAmt / 0.6f, 0, 1) * (1 - Math.Clamp(stormAmt, 0, 1));
        }
    }

    // A pixel the water is showing: not land, not a pier or deck, not a dry flat or sandbar.
    bool ReflWaterAt(int wx, int wy) => ExposedWater(wx, wy);

    void DrawReflections(float t)
    {
        float k = ReflectK;
        if (k <= 0.02f || !liveRender || reflectOff) return;
        int x0 = camX - 70, x1 = camX + W + 70, y0 = camY - ReflRows - 30, y1 = camY + H;
        foreach (var (tx, ty, kind) in trees)
        {
            if (kind is not ('t' or 'h' or 'f')) continue;
            float fx = tx * T + 5, fy = ty * T + 9;
            if (fx < x0 || fx > x1 || fy < y0 || fy > y1) continue;
            int a = tx, b = ty; char c = kind;
            Reflect(() => DrawTree(a, b, c, false), fx, fy, k, t);
        }
        foreach (var n in Islanders)
        {
            if (n.id is "isay" or "ben" || n.y < y0 || n.y > y1 || n.x < x0 || n.x > x1) continue;
            var who = n;
            Reflect(() => DrawBahay(who.x, who.y - 5, who.shirt), n.x, n.y, k, t);
        }
        if (player.Y > (HabagatTop - 25) * T)
            foreach (var n in HabagatFolk)
            {
                if (!n.house || n.y < y0 || n.y > y1 || n.x < x0 || n.x > x1) continue;
                var who = n;
                Reflect(() => DrawBahay(who.x, who.y - 5, who.shirt), n.x, n.y, k, t);
            }
        if (LighthouseX > x0 && LighthouseX < x1 && LighthouseY > y0 && LighthouseY < y1) Reflect(DrawLighthouse, LighthouseX, LighthouseY, k, t);
        // You, on a pier, on the shore or wading (not riding or aboard, where the water already shows you). Wading, the surface
        // is at your knees: that is the line you are mirrored about, so it is your upper body that shows below you.
        if (!Aboard && !Riding && mode != "title" && !(mode == "create" && creatorFor == "new"))
            Reflect(DrawPlayer, player.X, InWater ? player.Y - 3 : player.Y, k, t);
        if (Aboard) Reflect(() => DrawBoat(player.X, player.Y, time, true), player.X, player.Y + 4, k, t);
        else if (Has("boat") > 0)
        {
            var (bx, by) = BoatPosition();
            if (bx > x0 && bx < x1 && by > y0 && by < y1) Reflect(() => DrawBoat(bx, by, t), bx, by + 4, k, t);
        }
    }

    // Draws one source off screen and mirrors it onto the water below its foot (fx, fy in world pixels).
    void Reflect(Action draw, float fx, float fy, float k, float t)
    {
        int ix = (int)MathF.Round(fx), iy = (int)MathF.Round(fy);
        // Only things with open water just below them are worth drawing a second time.
        bool wet = false;
        for (int dx = -ReflReach / 2; dx <= ReflReach / 2 && !wet; dx += 8)
            for (int dy = 1; dy <= 8 && !wet; dy++) wet = ReflWaterAt(ix + dx, iy + dy);
        if (!wet) return;

        if (reflPix == null || reflPix.W != W || reflPix.H != H) reflPix = new Pix(W, H);
        var keep = pix; bool live = liveRender;
        // The scratch picture looks at the source alone: its foot always lands on the same row, whatever the main view, so a
        // source at the very top of the screen still has all of its rows above the foot.
        reflPix.CamX = ix - W / 2; reflPix.CamY = iy - ReflRows - 6;
        // Clear just the window that will be read.
        int sx0 = W / 2 - ReflReach, sx1 = W / 2 + ReflReach, sy0 = 0, sy1 = ReflRows + 8;
        if (sx0 < 0 || sx1 >= W || sy1 >= H) return;
        for (int y = sy0; y <= sy1; y++) Array.Fill(reflPix.Buf, ReflSentinel, y * W + sx0, sx1 - sx0 + 1);
        pix = reflPix; liveRender = false; reflecting = true;
        try { draw(); }
        finally { pix = keep; liveRender = live; reflecting = false; }

        for (int x = ix - ReflReach; x <= ix + ReflReach; x++)
        {
            int sx = x - reflPix.CamX;
            if (sx < 0 || sx >= W) continue;
            // The waterline of this column: the first open water below the foot (a beach may lie between).
            int yw = -1;
            for (int y = iy + 1; y <= iy + 8; y++) if (ReflWaterAt(x, y) && ReflWaterAt(x, y + 1)) { yw = y; break; }
            if (yw < 0) continue;
            for (int dy = 0; dy < ReflRows; dy++)
            {
                int ys = iy - dy - reflPix.CamY;
                if (ys < 0 || ys >= H) break;
                var src = reflPix.Buf[ys * W + sx];
                if (src.Equals(ReflSentinel)) continue;
                int yd = 2 * yw - iy + dy;
                // A little shimmer: a row now and then is left out, and the rest slides a pixel with the swell.
                if ((yd + (int)(t * 2)) % 5 == 0) continue;
                float sw = SeaSwell(x, yd, t * 1.3f);
                int xd = x + (sw > 0.45f ? 1 : sw < -0.45f ? -1 : 0);
                if (!ReflWaterAt(xd, yd)) continue;
                int px = xd - camX, py = yd - camY;
                if (px < 0 || px >= W || py < 0 || py >= H) continue;
                float a = 0.55f * k * MathF.Pow(1 - dy / (float)ReflRows, 1.2f);
                var tint = Pix.Mix(src, ReflWater, 0.25f);
                keep.Buf[py * W + px] = Pix.Mix(keep.Buf[py * W + px], tint, a);
            }
        }
    }
}
