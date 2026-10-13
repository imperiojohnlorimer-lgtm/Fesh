#if true
using Raylib_cs;

namespace Fesh;

// Platejaw's picture (1.29, replacing the flat 14-row grid): a Dunkleosteus drawn side on, facing right, 46 x 24, by lighting a
// silhouette so it has volume (a lit back, a pale belly, rim light) and then adding what it's known for: the armour plates
// and their seams, a ring of bone round the cheek, rivet-like bosses along the head, a gold eye in a dark socket, and the
// shearing jaw (a bony blade, not teeth) that really swings open on its hinge. Everything that moves is worked out from
// the pose: the tail beats in a travelling wave (the caudal fin trailing it), the pectoral fin flaps, the dorsal fin
// sways, the gill slit opens and shuts, and the jaw opens by any amount. Frames are cached by their quantised pose.
partial class Game
{
    const int PjW = 46, PjH = 24, PjHead = 33, PjBelly = 16;
    const float PjYc = 10f;
    static readonly Dictionary<(int jaw, int phase, int amp, bool flash), Color[]> platejawCache = new();

    static float PjLerp(float x, (float x, float v)[] pts)
    {
        if (x <= pts[0].x) return pts[0].v;
        for (int i = 1; i < pts.Length; i++)
            if (x <= pts[i].x) return pts[i - 1].v + (pts[i].v - pts[i - 1].v) * (x - pts[i - 1].x) / (pts[i].x - pts[i - 1].x);
        return pts[^1].v;
    }

    static readonly (float x, float v)[] PjTop = { (14, 2.4f), (17, 3.5f), (20, 4.6f), (23, 5.7f), (26, 6.7f), (29, 7.5f), (32, 7.9f), (35, 7.5f), (38, 6.4f), (41, 4.8f), (43, 2.8f), (44.6f, 0) };
    static readonly (float x, float v)[] PjBot = { (14, 2.4f), (17, 3.4f), (20, 4.4f), (24, 5.2f), (28, 5.7f), (31, 5.6f), (34, 4.8f), (38, 3.6f), (41, 2.8f), (43, 1.9f), (44.6f, 0.5f) };

    static Color PjMul(Color c, float k) => new((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), (byte)255);
    static Color PjMix(Color a, Color b, float k) => new((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k), (byte)255);

    // jaw 0 (shut) to 4 (wide), phase 0-15 round the tail's beat, amp 0-3 how hard it beats.
    static Color[] PlatejawPixels(int jaw, int phase, int ampQ, bool flash)
    {
        var key = (jaw, phase, ampQ, flash);
        if (platejawCache.TryGetValue(key, out var cached)) return cached;
        var px = new Color[PjW * PjH];
        var inked = new bool[PjW * PjH];
        float open = jaw / 4f, ph = phase / 16f * MathF.Tau, amp = 0.4f + 0.45f * ampQ;
        void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < PjW && y < PjH) { px[y * PjW + x] = c; inked[y * PjW + x] = true; } }
        bool Has(int x, int y) => x >= 0 && y >= 0 && x < PjW && y < PjH && inked[y * PjW + x];
        double H(int x, int y, int salt) => Pix.Hash(x, y, 940 + salt);

        // Palette: a cool grey-blue armour, a pale belly, bone for the jaw, green-grey fins.
        Color rim = Pal.C("#aab4c6"), hi = Pal.C("#8791a6"), mid = Pal.C("#5f6779"), lo = Pal.C("#444b5b"), deep = Pal.C("#2f3441"),
            seam = Pal.C("#232834"), belly = Pal.C("#a9ae9c"), bellyLo = Pal.C("#7b8176"),
            boneHi = Pal.C("#f4eedb"), bone = Pal.C("#d9d0b4"), boneLo = Pal.C("#9d9378"), mouth = Pal.C("#3e1015"),
            finHi = Pal.C("#6f8884"), fin = Pal.C("#4d625f"), finLo = Pal.C("#34443f"), eyeGold = Pal.C("#ffd76a"), ink = Pal.C("#151922");

        float Off(float x) { float t = Math.Clamp((14 - x) / 14f, 0, 1); return MathF.Sin(ph - (14 - x) * 0.45f) * amp * 3f * MathF.Pow(t, 1.2f); }

        // Fins behind the body: the dorsal fin on the back, the far pectoral fin.
        {
            float sway = MathF.Sin(ph * 0.5f + 1) * 0.8f * amp * 0.6f;
            for (int x = 14; x <= 24; x++)
            {
                float top = PjYc - PjLerp(x, PjTop) + 0.5f;
                float h = Math.Clamp((x - 13) * 0.55f, 0, 3.6f) * Math.Clamp((26 - x) / 4f, 0, 1) + 0.4f;
                float lean = (x - 14) * 0.2f + sway * (h / 4f);
                for (int y = (int)(top - h); y < (int)top + 2; y++) Put(x + (int)MathF.Round(lean * 0.4f), y, (y - (top - h) < 1.2f) ? finHi : (x % 2 == 0 ? fin : finLo));
            }
            // The far pectoral fin, a darker paddle behind the head.
            float fa = 0.9f + 0.35f * MathF.Sin(ph * 1.2f + 0.8f);
            for (float a = 0; a < 8.5f; a += 0.5f)
            {
                float bx = 31 - MathF.Cos(fa) * a * 0.8f, by = PjYc + 4 + MathF.Sin(fa) * a * 0.8f;
                float th = 2.4f - a * 0.22f;
                for (float b = 0; b < th; b += 0.5f) Put((int)MathF.Round(bx + 1), (int)MathF.Round(by + b), a < 1.5f || b < 0.6f ? finLo : Pal.C("#2a3733"));
            }
        }

        // The caudal fin: a forked tail on the end of the tail's wave, long on top.
        for (int x = 0; x < 7; x++)
        {
            float c = PjYc + Off(x);
            float u = (6.5f - x) / 6.5f;           // 0 at the stalk, 1 at the tips
            float up = 0.8f + u * 6.2f, down = 0.8f + u * 4.0f;
            float notch = x < 2 ? (2 - x) * 1.5f : 0;
            for (int y = (int)MathF.Floor(c - up); y <= (int)MathF.Ceiling(c + down); y++)
            {
                float d = y + 0.5f - c;
                if (d < -up || d > down) continue;
                if (MathF.Abs(d) < notch) continue;
                Color col = d < -up + 1.2f ? finHi : d > down - 1 ? finLo : (x + y) % 3 == 0 ? finLo : fin;
                Put(x, y, flash ? Color.White : col);
            }
        }

        // The tail stalk and the body: each column is lit from the top, so the back is bright and the belly pale and shaded.
        for (int x = 6; x < PjW; x++)
        {
            float cy, top, bot;
            if (x < 14)
            {
                cy = PjYc + Off(x);
                float half = 1.3f + (x - 6) / 8f * 1.6f;
                top = cy - half; bot = cy + half;
            }
            else
            {
                cy = PjYc; top = PjYc - PjLerp(x, PjTop); bot = PjYc + PjLerp(x, PjBot);
            }
            for (int y = 0; y < PjH; y++)
            {
                float yy = y + 0.5f;
                if (yy < top || yy > bot) continue;
                float v = (yy - top) / MathF.Max(0.01f, bot - top);   // 0 on the back, 1 on the belly
                double n = H(x, y, 0) - 0.5;
                Color c;
                if (x >= 14 && x < 32 && v > 0.84f) c = PjMix(belly, bellyLo, Math.Clamp((v - 0.84f) * 5, 0, 1) * 0.6f);
                else if (v < 0.13f) c = rim;
                else if (v < 0.34f) c = hi;
                else if (v < 0.62f) c = mid;
                else if (v < 0.84f) c = lo;
                else c = deep;
                c = PjMul(c, 1 + (float)n * 0.1f);
                // The plates: the trunk shield and the head have seams, a ring of bone round the cheek, bosses along the back.
                bool onSeam = false;
                if (x >= 14)
                {
                    foreach (float sx in new[] { 17f, 21.5f, 26f })
                        if (MathF.Abs(x + 0.5f - (sx + 0.2f * (yy - PjYc))) < 0.5f && v > 0.08f && v < 0.9f) onSeam = true;
                    float cx = (x + 0.5f - 37) / 7f, cyy = (yy - (PjYc + 0.8f)) / 6.4f, rr = MathF.Sqrt(cx * cx + cyy * cyy);
                    if (x >= 29 && MathF.Abs(rr - 1) < 0.075f && v > 0.1f) onSeam = true;
                    if (x >= 29 && rr < 0.93f) c = PjMul(c, 1.07f);   // the cheek plate stands a little proud
                    // Gill slit, opening and shutting.
                    if (x >= 29 && x <= 30 && yy > PjYc + 0.5f && yy < PjYc + 5 && (x == 29 || MathF.Sin(ph) > 0.35f)) onSeam = true;
                    // Rivet-like bosses along the head's top.
                    if (yy >= top + 1.4f && yy < top + 2.4f && x is 27 or 30 or 33 or 36 or 39) c = rim;
                }
                else if ((x + 1) % 3 == 0 && v > 0.1f && v < 0.9f) c = PjMul(c, 0.82f);   // rings along the tail stalk
                if (onSeam) c = seam;
                // A pale scar, a nick, and a little algae on the back of an old fish.
                if ((x == 22 && y == (int)PjYc - 2) || (x == 23 && y == (int)PjYc - 3) || (x == 24 && y == (int)PjYc - 4) || (x == 38 && y == (int)PjYc + 1)) c = Pal.C("#b8bfcc");
                if (v < 0.3f && H(x, y, 3) > 0.94) c = Pal.C("#4f6e56");
                Put(x, y, flash ? Color.White : c);
            }
        }

        // The mouth, the lower jaw on its hinge, and the upper jaw's blade.
        {
            float hx = 31.5f, hy = PjYc + 5.2f, th0 = -0.255f, theta = th0 + open * 0.72f, L = 12.5f;
            float ux = MathF.Cos(theta), uy = MathF.Sin(theta), nx = -uy, ny = ux;
            // Inside the open mouth.
            if (open > 0.08f)
                for (int x = 33; x < 45; x++)
                    for (int y = 0; y < PjH; y++)
                    {
                        float mouthY = PjYc + PjLerp(x, PjBot) - 0.1f, jawTop = hy + (x + 0.5f - hx) * MathF.Tan(theta);
                        if (y + 0.5f > mouthY && y + 0.5f < jawTop && !Has(x, y)) Put(x, y, flash ? Color.White : mouth);
                    }
            // The jaw itself: a bone blade, white along its cutting edge with notches where the teeth would be.
            for (int y = 0; y < PjH; y++)
                for (int x = 0; x < PjW; x++)
                {
                    float dx = x + 0.5f - hx, dy = y + 0.5f - hy;
                    float a = dx * ux + dy * uy, b = dx * nx + dy * ny;
                    float thick = 3.2f - 1.5f * Math.Clamp(a / L, 0, 1);
                    if (a < -0.5f || a > L || b < -0.6f || b > thick) continue;
                    Color c = b < 0.5f ? boneHi : b < thick - 1 ? bone : boneLo;
                    if (b < 0.5f && ((int)MathF.Floor(a) % 3 == 1) && a > 3) c = Color.White;
                    Put(x, y, flash ? Color.White : c);
                }
            // The upper jaw's cutting edge and the notches hanging from it.
            for (int x = 33; x < 45; x++)
            {
                int my = (int)MathF.Floor(PjYc + PjLerp(x, PjBot) - 0.2f);
                Put(x, my, flash ? Color.White : boneHi);
                if (x is 37 or 40 or 43) Put(x, my + 1, flash ? Color.White : bone);
            }
        }

        // The pectoral fin, in front: an armoured paddle that flaps, with a few plate lines.
        {
            float fa = 0.8f + 0.4f * MathF.Sin(ph * 1.2f);
            for (float a = 0; a < 9f; a += 0.45f)
            {
                float bx = 30 - MathF.Cos(fa) * a * 0.85f, by = PjYc + 3.5f + MathF.Sin(fa) * a * 0.85f;
                float th = 3.2f - a * 0.28f;
                for (float b = 0; b < th; b += 0.5f)
                {
                    Color c = b < 0.7f ? finHi : b > th - 0.8f ? finLo : ((int)a % 3 == 1 ? finLo : fin);
                    Put((int)MathF.Round(bx), (int)MathF.Round(by + b), flash ? Color.White : c);
                }
            }
        }

        // The eye: a gold bead in a dark socket under the brow.
        {
            int ex = 36, ey = (int)PjYc - 3;
            for (int dy = -1; dy <= 2; dy++) for (int dx = -1; dx <= 2; dx++) Put(ex + dx, ey + dy, seam);
            Put(ex, ey, eyeGold); Put(ex + 1, ey, eyeGold); Put(ex, ey + 1, eyeGold); Put(ex + 1, ey + 1, Pal.C("#e0a53a"));
            Put(ex + 1, ey, Pal.C("#10243a"));
            Put(ex - 1, ey - 2, rim); Put(ex, ey - 2, rim); Put(ex + 1, ey - 2, hi);
        }

        // Dark ink round the whole shape, so it stands out of the water and the stone.
        var final = new Color[PjW * PjH];
        for (int y = 0; y < PjH; y++)
            for (int x = 0; x < PjW; x++)
            {
                int i = y * PjW + x;
                if (inked[i]) { final[i] = px[i]; continue; }
                if (Has(x - 1, y) || Has(x + 1, y) || Has(x, y - 1) || Has(x, y + 1)) final[i] = ink;
            }
        platejawCache[key] = final;
        return final;
    }
}
#endif
