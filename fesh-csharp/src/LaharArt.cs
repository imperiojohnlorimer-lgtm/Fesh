using Raylib_cs;

namespace Fesh;

// The lahar watch (1.28), painted like the game's other scenes instead of flat shapes: the map is Baga's real ground (the baked
// tiles with the cone, the houses, the trees and the marker stones drawn on it, then the danger zone), and the channel camera is a
// little pixel scene of a trench down an ash slope to the beach, with a flow of mud that churns, carries boulders, and spills past
// the banks at the mouth, the rain coming down, and the scan lines and vignette of a station camera.
partial class Game
{
    const int LaharMapX0 = 2180, LaharMapY0 = 312, LaharMapW = 260, LaharMapH = 210;

    // Baga's north side from above, 260 x 210 world pixels shown at 2x.
    void PaintLaharMap(Pix p, LaharWatch w)
    {
        p.CamX = LaharMapX0; p.CamY = LaharMapY0;
        p.CopyFrom(worldBase, LaharMapX0, LaharMapY0, Pal.C("#2f6f98"));
        var keep = pix;
        pix = p;
        try
        {
            // The channel (dry, or with the lahar's mud in it once it has come down).
            DrawBagaGround(time, true, w.T >= 0);
            var list = new List<(float y, Action draw)>();
            foreach (var (tx, ty, kind) in trees)
                if (tx * T >= LaharMapX0 - 24 && tx * T <= LaharMapX0 + LaharMapW + 24 && ty * T >= LaharMapY0 - 36 && ty * T <= LaharMapY0 + LaharMapH + 20)
                {
                    int ax = tx, ay = ty; char k = kind;
                    list.Add((ty * T + 9, () => DrawTree(ax, ay, k, live: false)));
                }
            foreach (var (x, y) in MarkerStones()) { float mx = x, my = y; list.Add((my, () => DrawMarkerStone(mx, my))); }
            list.Add((AbacaY, DrawAbaca));
            list.Add((PiliY, DrawPili));
            list.Add((AlertBoardY, () => DrawAlertBoard(AlertBoardX, AlertBoardY)));
            list.Add((MilaY - 3, () => DrawBahay(MilaX, MilaY - 5, "#c0392b")));
            list.Add((BenY - 3, () => DrawStation(BenX, BenY - 5)));
            list.Add((ConeBaseY, DrawBagaCone));
            foreach (var o in list.OrderBy(o => o.y)) o.draw();
            // The permanent danger zone, dashed in red, two pixels thick.
            for (int i = 0; i < 140; i++)
            {
                if (i % 3 == 2) continue;
                float a = i / 140f * MathF.Tau;
                float x = PdzX + MathF.Cos(a) * PdzRX, y = PdzY + MathF.Sin(a) * PdzRY;
                p.Rect(x, y, 2, 2, Pal.C("#d8323a"));
            }
        }
        finally { pix = keep; p.CamX = p.CamY = 0; }
    }

    /* ---------- The channel camera ---------- */
    const int LcW = 130, LcH = 75;
    static readonly (float x, float y) LcA = (10, 5), LcB = (90, 55);

    // Where a point is along the channel (s, 0 at the top to 1 at the mouth) and how far across it (v, in pixels, + to the right).
    static (float s, float v, float half) LcAxis(float x, float y)
    {
        float dx = LcB.x - LcA.x, dy = LcB.y - LcA.y, len = MathF.Sqrt(dx * dx + dy * dy);
        float ux = dx / len, uy = dy / len;
        float s = ((x - LcA.x) * ux + (y - LcA.y) * uy) / len, v = -(x - LcA.x) * uy + (y - LcA.y) * ux;
        return (s, v, 4.4f + Math.Clamp(s, 0, 1) * 6.4f + MathF.Sin(s * 14) * 0.7f);
    }

    void PaintLaharCam(Pix p, LaharWatch w)
    {
        float t = time;
        float spill = w.T > 3.5f ? Math.Min(1, (w.T - 3.5f) / 2.5f) : 0;
        float flow = w.T >= 0 ? Math.Min(1, w.T / 4) : 0;
        float len = MathF.Sqrt((LcB.x - LcA.x) * (LcB.x - LcA.x) + (LcB.y - LcA.y) * (LcB.y - LcA.y));
        for (int y = 0; y < LcH; y++)
            for (int x = 0; x < LcW; x++)
            {
                // The ground: a grey ash slope, rougher and darker towards the foot, a beach below it and the sea.
                float n = VNoise(x, y, 7, 740) * 0.6f + VNoise(x, y, 3, 741) * 0.4f + (BayerAt(x, y) - 0.5f) * 0.12f;
                Color c = n < 0.34f ? Pal.C("#5a5650") : n < 0.62f ? Pal.C("#6f6a62") : Pal.C("#85807a");
                bool sea = y >= 61 + MathF.Sin(x * 0.2f + t * 0.6f) * 0.8f, beach = !sea && y >= 52 && x > 52 + (y - 52) * 0.2f;
                if (beach)
                {
                    c = n < 0.5f ? Pal.C("#d8c49a") : Pal.C("#e6d4aa");
                    if (y > 59) c = Pix.Mix(c, Pal.C("#9a8a66"), 0.4f);   // wet sand
                }
                if (sea)
                {
                    float d = (y - 61) / 14f;
                    c = Pix.Mix(Pal.C("#4a98c0"), Pal.C("#2a6a96"), Math.Clamp(d, 0, 1));
                    if (VNoise(x + (int)(t * 8), y * 2, 5, 742) > 0.7f) c = Pix.Mix(c, Color.White, 0.18f);   // a little surf
                }
                var (s, v, half) = LcAxis(x, y);
                // Ridges either side, the bank, and the trench itself.
                bool inChannel = s > -0.06f && s < 1.08f && MathF.Abs(v) < half && !sea;
                if (!sea && !beach && s > -0.06f && s < 1.08f)
                {
                    float edge = MathF.Abs(v) - half;
                    if (edge > 0 && edge < 2.2f) c = v < 0 ? Pal.C("#9a948a") : Pal.C("#4c4842");   // the lit and shaded lips
                    else if (edge >= 2.2f && edge < 6) c = Pix.Mix(c, v < 0 ? Pal.C("#8a857c") : Pal.C("#46423c"), 0.35f);
                }
                if (inChannel)
                {
                    float k = 1 - MathF.Abs(v) / half;
                    c = Pix.Mix(Pal.C("#403a34"), Pal.C("#5e574e"), 0.35f + 0.4f * VNoise(x, y, 4, 743));
                    if (Pix.Hash(x, y, 744) < 0.04) c = Pal.C("#8a847a");   // stones in the bed
                    // The flow: mud that has reached this far down, churning, brighter on its crests, with a ragged front.
                    float front = flow * 1.02f + (VNoise((int)(v * 3), 0, 5, 745) - 0.5f) * 0.04f;
                    if (s < front && w.T >= 0)
                    {
                        float u = s * len * 1.4f - t * 26, lat = v * 1.6f;
                        float wave = VNoise((int)(u * 2) + 5000, (int)(lat * 2) + 5000, 5, 746) * 0.7f + VNoise((int)(u * 4) + 5000, (int)(lat * 4) + 5000, 3, 747) * 0.3f;
                        c = wave < 0.34f ? Pal.C("#55432f") : wave < 0.58f ? Pal.C("#6c5640") : wave < 0.78f ? Pal.C("#85704f") : Pal.C("#a08a64");
                        float toFront = (front - s) * len;
                        if (toFront < 3.5f) c = Pix.Mix(c, Pal.C("#d6c8a6"), 0.55f - toFront * 0.14f);   // the churning head of the flow
                        if (k < 0.18f) c = Pix.Mix(c, Pal.C("#3a2c20"), 0.4f);   // slower and darker along the edges
                    }
                    else if (w.Live && w.T < 0 && Pix.Hash((int)(x + t * 4), y, 748) < 0.05) c = Pal.C("#7a90a0");   // a trickle before it comes
                }
                p.Buf[y * LcW + x] = c;
            }
        // Boulders and logs rolled along in the flow.
        if (flow > 0.05f)
            for (int i = 0; i < 9; i++)
            {
                float s = ((t * 0.16f + i * 0.113f) % 1.1f);
                if (s > flow * 1.0f || s < 0) continue;
                float lat = ((float)Pix.Hash(i, 2, 749) - 0.5f) * 1.2f;
                float px = LcA.x + (LcB.x - LcA.x) * s, py = LcA.y + (LcB.y - LcA.y) * s;
                float half = 3.6f + s * 5.2f, nxv = -(LcB.y - LcA.y) / len, nyv = (LcB.x - LcA.x) / len;
                px += nxv * lat * half * 0.7f; py += nyv * lat * half * 0.7f;
                float r = 1.3f + (float)Pix.Hash(i, 3, 749) * 1.6f;
                Blob(p, px + 0.6f, py + 1.2f, r + 0.6f, r * 0.6f, 0, (l, x, y) => Pal.Rgba(30, 22, 14, 0.4f));
                Blob(p, px, py, r, r * 0.9f, 0, (l, x, y) => l > 0.2f ? Pal.C("#b0aaa0") : l > -0.1f ? Pal.C("#807a72") : Pal.C("#4e4a44"));
                // The splash it throws.
                Dot(p, (int)px - 1, (int)(py - r - 1 + MathF.Sin(t * 8 + i)), Pal.Rgba(230, 220, 190, 0.8f));
            }
        // At the mouth the flow spills past the banks over the beach and out into the sea, which turns brown.
        if (spill > 0)
        {
            float r = 5 + spill * 24;
            for (int y = (int)(LcB.y - r - 4); y <= LcB.y + r + 4; y++)
                for (int x = (int)(LcB.x - r - 4); x <= LcB.x + r + 4; x++)
                {
                    if (x < 0 || y < 0 || x >= LcW || y >= LcH) continue;
                    float dx = x - LcB.x, dy = (y - LcB.y) * 1.3f, d = MathF.Sqrt(dx * dx + dy * dy) + (VNoise(x, y, 5, 750) - 0.5f) * 8;
                    if (d > r) continue;
                    float wave = VNoise(x * 2 + (int)(t * 12), y * 2, 4, 751);
                    var mud = wave < 0.4f ? Pal.C("#64503a") : wave < 0.72f ? Pal.C("#7e6848") : Pal.C("#9a8460");
                    float a = d > r - 3 ? 0.55f : 0.9f;
                    var under = p.Buf[y * LcW + x];
                    p.Buf[y * LcW + x] = Pix.Mix(under, mud, a * (y >= 61 ? 0.6f : 1f));
                }
        }
        // Rain while it's live: slanting streaks, and splashes on the mud.
        if (w.Live)
        {
            for (int i = 0; i < 70; i++)
            {
                double fx = Wrap(Pix.Hash(i, 1, 752) * (LcW + 20) - t * 18, LcW + 20) - 10, fy = Wrap(Pix.Hash(i, 2, 752) * LcH + t * (60 + 30 * Pix.Hash(i, 3, 752)), LcH);
                for (int k = 0; k < 4; k++) Dot(p, (int)fx + k / 2, (int)fy - k, Pal.Rgba(222, 234, 244, 0.5f - k * 0.08f));
            }
            for (int i = 0; i < 14; i++)
            {
                float ph = (t * 1.6f + (float)Pix.Hash(i, 4, 752) * 7) % 1;
                int sx = (int)(Pix.Hash(i, 5, 752) * LcW), sy = (int)(Pix.Hash(i, 6, 752) * LcH);
                if (ph < 0.5f) { Dot(p, sx - 1, sy, Pal.Rgba(230, 240, 250, 0.5f * (1 - ph * 2))); Dot(p, sx + 1, sy, Pal.Rgba(230, 240, 250, 0.5f * (1 - ph * 2))); }
            }
        }
        // The station camera: a little barrel darkening, scan lines, a blinking record light and corner marks.
        for (int y = 0; y < LcH; y++)
            for (int x = 0; x < LcW; x++)
            {
                float nx = (x - LcW / 2f) / (LcW / 2f), ny = (y - LcH / 2f) / (LcH / 2f);
                float vig = Math.Clamp((nx * nx * 0.5f + ny * ny * 0.7f - 0.35f) * 0.55f, 0, 0.5f);
                var c = p.Buf[y * LcW + x];
                if (y % 2 == 1) vig += 0.07f;
                p.Buf[y * LcW + x] = Pix.Mix(c, Color.Black, vig);
            }
        // An occasional glitch band, as old cameras do.
        float gl = (t * 0.37f) % 6;
        if (gl < 0.12f) { int gy = (int)(Pix.Hash((int)(t * 0.37f / 6), 1, 753) * (LcH - 4)); for (int x = 0; x < LcW; x++) p.Buf[gy * LcW + x] = Pix.Mix(p.Buf[gy * LcW + (x + 3) % LcW], Color.White, 0.12f); }
        if ((int)(t * 1.2f) % 2 == 0) Blob(p, LcW - 7, 6, 2.4f, 2.4f, 0, (l, x, y) => Pal.C("#e03a3a"));
        var mark = Pal.Rgba(255, 255, 255, 0.55f);
        foreach (var (cx, cy, sx, sy) in new[] { (3, 3, 1, 1), (LcW - 4, 3, -1, 1), (3, LcH - 4, 1, -1), (LcW - 4, LcH - 4, -1, -1) })
        { for (int k = 0; k < 5; k++) { Dot(p, cx + k * sx, cy, mark); Dot(p, cx, cy + k * sy, mark); } }
    }
}
