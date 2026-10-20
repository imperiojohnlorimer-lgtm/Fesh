using Raylib_cs;

namespace Fesh;

// Shadows that follow the sun (1.39, Light: Soft or Full). The sun rises in the east at 06:00 and sets in the west at
// 20:00, so shadows fall west in the morning, almost straight down at noon and east in the evening, long and faint near the
// horizon. Every tree and building writes into one view-sized mask (the strongest wins, so overlaps never double their
// darkness), the edge steps down in two levels with a dither (pixel art, not a blur), and one pass darkens the ground.
// With light off the old east-falling shadows (Sky.cs, Ambience.cs) are drawn instead.
partial class Game
{
    // Where the sun is along its day: cos(theta) (+1 east at 06:00, -1 west at 20:00) and its height above the horizon.
    internal static (float cosTheta, float altitude) SunPose(float clock)
    {
        float hour = Math.Clamp(clock / 60f, 6f, 20f);
        float theta = MathF.PI * (hour - 6) / 14;
        float alt = MathF.Max(8f * MathF.PI / 180, 65f * MathF.PI / 180 * MathF.Sin(theta));
        return (MathF.Cos(theta), alt);
    }

    // The shadow's far end for an object `height` px tall, relative to its foot (a stylised, capped projection:
    // L = 0.22 h / tan(altitude), at most 0.65 h), and how long it is as a share of that cap (0 short, 1 longest).
    internal (float dx, float dy, float far) ShadowOffset(float height)
    {
        var (c, alt) = SunPose(state.clock);
        float max = 0.65f * height, len = Math.Clamp(0.22f * height / MathF.Tan(alt), 2, max);
        return (-len * c, 0.3f * len, len / max);
    }

    float[] shadeMask;
    bool shadeActive;

    void ShadeAt(int wx, int wy, float level)
    {
        int sx = wx - camX, sy = wy - camY;
        if (sx < 0 || sy < 0 || sx >= W || sy >= H) return;
        int i = sy * W + sx;
        if (level > shadeMask[i]) shadeMask[i] = level;
    }

    // A soft oval: full strength in the middle, then a half-strength ring, then a dithered fringe.
    void ShadeEllipse(float cx, float cy, float rx, float ry, float alpha, bool dapple, float drift)
    {
        int x0 = (int)MathF.Floor(cx - rx), x1 = (int)MathF.Ceiling(cx + rx), y0 = (int)MathF.Floor(cy - ry), y1 = (int)MathF.Ceiling(cy + ry);
        for (int wy = Math.Max(y0, camY); wy <= Math.Min(y1, camY + H - 1); wy++)
            for (int wx = Math.Max(x0, camX); wx <= Math.Min(x1, camX + W - 1); wx++)
            {
                float dx = (wx + 0.5f - cx) / rx, dy = (wy + 0.5f - cy) / ry, d = dx * dx + dy * dy;
                if (d >= 1) continue;
                float lv = d < 0.45f ? 1 : d < 0.75f ? 0.6f : BayerAt(wx, wy) < (1 - d) / 0.25f ? 0.6f : 0;
                if (lv <= 0) continue;
                // Light coming through the leaves: a few small gaps that drift slowly.
                if (dapple && lv > 0.9f && Pix.Hash((wx + (int)drift) >> 1, wy >> 1, 523) < 0.13) lv = 0.35f;
                ShadeAt(wx, wy, lv * alpha);
            }
    }

    void ShadeRect(float x, float y, float w, float h, float alpha)
    {
        for (int wy = (int)MathF.Floor(y); wy < (int)MathF.Ceiling(y + h); wy++)
            for (int wx = (int)MathF.Floor(x); wx < (int)MathF.Ceiling(x + w); wx++) ShadeAt(wx, wy, alpha);
    }

    // A building's shadow: its footprint swept along the sun's shadow vector, plus the strip at its foot.
    void ShadeBuilding(int x, int y, int w, int h)
    {
        var (dx, dy, _) = ShadowOffset(h * 1.3f);
        const float alpha = 0.2f;
        int steps = Math.Max(2, (int)(MathF.Abs(dx) + MathF.Abs(dy)));
        for (int s = 0; s <= steps; s++)
        {
            float k = (float)s / steps;
            ShadeRect(x + dx * k, y - 1 + dy * k, w, 3, alpha);
        }
        ShadeRect(x + dx + MathF.Sign(dx), y - 1 + dy, w, 3, alpha * 0.6f);   // a lighter fringe at the far end
        ShadeRect(x + 3, y + 1, w - 3, 2, alpha);
    }

    void DrawShadows()
    {
        float fade = (1 - Darkness) * (1 - 0.6f * rainAmt);
        if (fade <= 0.04f || scene != "world") return;
        if (shadeMask == null || shadeMask.Length != W * H) shadeMask = new float[W * H];
        else Array.Clear(shadeMask);
        shadeActive = true;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        bool dapples = LightMode == 2;
        foreach (var tr in trees)
        {
            if (tr.kind is not ('t' or 'h' or 'f')) continue;
            float scale = TreeScale(tr.kind), height = 25.5f * scale;
            var (ox, oy, far) = ShadowOffset(height);
            int reach = (int)(MathF.Abs(ox) / T) + 3;
            if (tr.x < vx0 - reach - 2 || tr.x > vx1 + reach + 2 || tr.y < vy0 - 3 || tr.y > vy1 + reach + 3) continue;
            float s = 0.9f + 0.2f * (float)Pix.Hash(tr.x, tr.y, 61);
            if (tr.x >= MlStart) s *= 1 - 0.3f * Math.Clamp((MlHeight(MlLx(tr.x * T + 5), MlLy(tr.y * T + 5)) - 0.28f) / 0.35f, 0, 1);
            float rx0 = (tr.kind == 't' ? 11 : tr.kind == 'f' ? 8 : 9) * s * scale, ry0 = (tr.kind == 'h' ? 3.6f : 4.8f) * s * scale;
            // Short shadows sit under the crown; long ones stretch out and thin.
            float rx = rx0 * (0.85f + 0.25f * far), ry = ry0 * (0.95f + 0.1f * far);
            float cx = tr.x * T + 5 + ox * 0.75f, cy = tr.y * T + 9 + 1.5f * scale + oy * 0.75f;
            ShadeEllipse(cx, cy, rx, ry, 0.2f * (1 - 0.35f * far), dapples, time * 1.5f + tr.x * 3);
        }
        DrawBuildingShadows();      // BuildingShadow writes into the mask while shadeActive
        shadeActive = false;
        var shade = new Color(10, 34, 36, 255);
        for (int i = 0; i < shadeMask.Length; i++)
        {
            float a = shadeMask[i] * fade;
            if (a > 0.004f) pix.Buf[i] = Pix.Mix(pix.Buf[i], shade, a);
        }
    }
}
