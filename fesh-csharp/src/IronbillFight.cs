using Raylib_cs;

namespace Fesh;

// 1.35: Ironbill as a boss fight. The marlin is drawn from a lit side-on silhouette that can turn to any angle (its sail
// raised or folded, its tail beating), so it circles under the surface with only its sail cutting the water, breaches in a
// long arc nose-up then nose-down, skims across at your boat when it charges, and stands on its tail to tail-walk across
// the sea. The fight has three phases by how far you've reeled it in (a name bar shows its stamina):
//   1 circling: it tows the boat and breaches now and then (time a press at the top, as with any jumper);
//   2 charging: it also turns on the boat: a red lane shows where it will come, and letting go of the reel in time braces
//     you (the fish-attack dodge of FishAttacks.cs) or the bill rams the hull;
//   3 tail-walking: it stands up and skitters across the surface; holding the reel then shakes the hook loose a little.
// A title banner opens the fight. Everything here is looks and timing on top of the ordinary jumper reel and UpdateTow.
partial class Game
{
    sealed class IronbillFight
    {
        public float Banner = 3.2f, Walk, WalkT = 6, ChargeT, PrevWarn, PrevLeap, T, LandFx;
        public string Title = "IRONBILL", Sub = "Legend of the open sea";
        public float Cx0, Cy0, Cx1, Cy1;
        public bool ChargeHit;
        public int Phase = 1;
    }
    IronbillFight ironbill;

    bool IronbillOnLine => mode == "reeling" && reel?.Roll?.Id == TowFish && fish != null;
    int IronbillPhaseOf(float progress) => progress < 0.45f ? 1 : progress < 0.75f ? 2 : 3;
    const float ChargeLen = 0.5f;
    int IronbillChargeDamage => ironbill != null && ironbill.Phase >= 2 ? 16 : 0;

    // Called when Ironbill takes the lure (StartReelCore): the banner, a first breach that's free, and its first charge
    // some way off.
    void StartIronbill()
    {
        ironbill = new IronbillFight();
        // A long fight: it starts further out than an ordinary fish (a perfect hook still helps).
        reel.Progress = reel.Perfect ? 0.38f : 0.3f;
        reel.Leap = reel.LeapLen; reel.LeapMark = 0; reel.LeapDone = true;
        reel.AttackTimer = 9;
        quake = 0.5f;
        Sfx.Play("splash");
    }

    void UpdateIronbill(float dt)
    {
        if (!IronbillOnLine) { if (mode != "pause") ironbill = null; return; }
        ironbill ??= new IronbillFight();
        var b = ironbill;
        var r = reel;
        b.T += dt;
        b.Banner = Math.Max(0, b.Banner - dt);
        b.LandFx = Math.Max(0, b.LandFx - dt);
        int phase = IronbillPhaseOf(r.Progress);
        if (phase > b.Phase)
        {
            b.Phase = phase;
            quake = 0.4f;
            Sfx.Play("splash");
            b.Banner = 3.2f; b.Title = phase == 2 ? "FURIOUS" : "DESPERATE";
            if (phase == 2) b.Sub = "It will charge: when the red lane flashes, let go of the reel";
            else b.Sub = "It tail-walks across the sea: ease off the reel while it does";
        }
        // A breach that just ended: the splash as it comes down.
        if (b.PrevLeap > 0 && r.Leap <= 0) { quake = Math.Max(quake, 0.3f); b.LandFx = 0.6f; Burst(fish.Bx, fish.By, "#eef9fb", 12); Sfx.Play("splash"); }
        b.PrevLeap = r.Leap;
        // A charge that just came in: it skims from where it was to past the boat.
        if (b.PrevWarn > 0 && r.AttackWarning <= 0)
        {
            b.ChargeT = ChargeLen; b.ChargeHit = r.DuckTime < 0.2f;
            b.Cx0 = fish.Bx; b.Cy0 = fish.By;
            b.Cx1 = player.X + (player.X - fish.Bx) * 0.9f; b.Cy1 = player.Y - 2 + (player.Y - fish.By) * 0.9f;
            Sfx.Play("splash");
        }
        b.PrevWarn = r.AttackWarning;
        if (b.ChargeT > 0)
        {
            b.ChargeT -= dt;
            if (b.ChargeHit && b.ChargeT < ChargeLen * 0.5f && b.ChargeT + dt >= ChargeLen * 0.5f)
            {
                quake = 0.6f; Burst(player.X, player.Y, "#8a6239", 10); Burst(player.X, player.Y, "#eef9fb", 10);
                Floater("Rammed!", player.X, player.Y - 30, "#ff9a8a");
            }
            if (b.ChargeT <= 0)
            {
                // It comes round again from the far side, and the line follows it there.
                towAng = MathF.Atan2((b.Cy1 - player.Y + 4) / 30, (b.Cx1 - player.X) / 46);
                if (!b.ChargeHit) Floater("It rams past the hull!", player.X, player.Y - 30, "#7fd36b");
            }
        }
        // Tail-walking (phase 3): every so often, never while it leaps or charges.
        if (b.Walk > 0)
        {
            b.Walk -= dt;
            if (ReelHeld()) { r.Progress -= dt * 0.06f; r.Tension = 0; }
            SetPrompt("It's tail-walking! Ease off the reel until it drops back in!", null, true);
            if (b.Walk <= 0) { b.WalkT = Rand(6, 9); b.LandFx = 0.6f; Burst(fish.Bx, fish.By, "#eef9fb", 10); quake = 0.25f; }
        }
        else if (b.Phase >= 3 && r.Leap <= 0 && r.AttackWarning <= 0 && b.ChargeT <= 0)
        {
            b.WalkT -= dt;
            if (b.WalkT <= 0) { b.Walk = 2.4f; Sfx.Play("splash"); Floater("Tail-walk!", player.X, player.Y - 30, "#ffd76a"); }
        }
        // While it charges, the line's end rides with it.
        if (b.ChargeT > 0)
        {
            float k = 1 - b.ChargeT / ChargeLen;
            fish.Bx = fish.Tx = b.Cx0 + (b.Cx1 - b.Cx0) * k;
            fish.By = fish.Ty = b.Cy0 + (b.Cy1 - b.Cy0) * k;
        }
    }

    // Where Ironbill is heading on screen as it circles (UpdateTow's ellipse, anticlockwise in towAng).
    float IronbillHeading()
    {
        float hx = -MathF.Sin(towAng) * 46, hy = MathF.Cos(towAng) * 30;
        return MathF.Atan2(hy, hx);
    }

    /* ---------- The picture ---------- */
    // Materials of the side-on marlin. Counter-shaded: a deep cobalt back, electric-blue bars down a silver flank, a white
    // belly, a tall cobalt sail with dark spots and a bright leading edge, a silver bill, a sickle tail.
    static readonly Color[] MarlinCol =
    {
        default,
        Pal.C("#0b1530"),   // 1 outline
        Pal.C("#16285a"),   // 2 back
        Pal.C("#2a4f9c"),   // 3 upper flank
        Pal.C("#86a9cf"),   // 4 flank
        Pal.C("#e6f0f6"),   // 5 belly
        Pal.C("#9fe0ff"),   // 6 stripe
        Pal.C("#2443a0"),   // 7 sail
        Pal.C("#142a66"),   // 8 sail spot
        Pal.C("#5d8ce0"),   // 9 sail's leading edge
        Pal.C("#d7dde4"),   // 10 bill
        Pal.C("#8b97a5"),   // 11 bill's underside
        Pal.C("#0a0d14"),   // 12 eye
        Pal.C("#f3f6f8"),   // 13 eye's ring
        Pal.C("#1a2c62"),   // 14 tail and fins
        Pal.C("#c6dbee")    // 15 a glint along the back
    };

    // The material at a point of the marlin: u along the body (tail -23 to bill tip 26), v up from its middle line.
    static int MarlinAt(float u, float v, float tail, float sail)
    {
        // The tail flexes as it beats; the body barely.
        float bend = tail * Math.Clamp((-u - 4) / 18, 0, 1) * 1.6f;
        v -= bend;
        float h = 0;
        if (u >= -14 && u <= 12) { float t = (u + 1) / 13.5f; h = 6.3f * MathF.Sqrt(MathF.Max(0, 1 - t * t)); }
        else if (u > -17.5f && u < -14) h = 1.1f;
        // The sickle tail: two long lobes sweeping back, their size changing as it beats side to side.
        if (u <= -15 && u >= -24)
        {
            float d = -15 - u, lobe = 0.75f + 0.25f * MathF.Abs(MathF.Cos(tail * 1.3f));
            float cy = d * 1.15f * lobe, th = 1.7f * (1 - d / 9) + 0.55f;
            if (MathF.Abs(v - cy) < th || MathF.Abs(v + cy) < th) return 14;
        }
        // The bill: long, from the upper jaw.
        if (u > 11 && u <= 26)
        {
            float th = 0.95f * (1 - (u - 11) / 15) + 0.35f;
            if (MathF.Abs(v - 1.0f) < th) return v < 1.0f - th * 0.2f ? 11 : 10;
        }
        // The sail: tallest just behind the head, a steep leading edge, sloping down toward the tail.
        if (u > -6 && u < 10.5f && sail > 0)
        {
            float top = u > 8 ? 9 * (10.5f - u) / 2.5f : 2.5f + 6.5f * (u + 6) / 14;
            top *= sail;
            float bse = h * 0.8f;
            if (v > bse && v < bse + top)
            {
                if (u > 7.6f || v > bse + top - 0.9f) return 9;
                int cx = (int)MathF.Floor((u + 20) / 2.6f), cy = (int)MathF.Floor(v / 2.4f);
                return (cx * 7 + cy * 3) % 5 == 0 ? 8 : 7;
            }
        }
        // A small second dorsal and the anal fin near the tail.
        if (u > -14 && u < -11 && v > h && v < h + 1.6f) return 14;
        if (u > -11 && u < -6 && v < -h && v > -h - 2.2f * (u + 11) / 5) return 14;
        if (MathF.Abs(v) <= h)
        {
            // The long pectoral fin, swept back along the flank.
            float px0 = 6, py0 = -1.6f, px1 = -3, py1 = -6.4f, sx = px1 - px0, sy = py1 - py0;
            float k = Math.Clamp(((u - px0) * sx + (v - py0) * sy) / (sx * sx + sy * sy), 0, 1);
            float ex = u - (px0 + sx * k), ey = v - (py0 + sy * k);
            if (ex * ex + ey * ey < 0.7f) return 14;
            // The eye.
            float dx = u - 9, dy = v - 1.7f;
            if (dx * dx + dy * dy < 0.6f) return 12;
            if (dx * dx + dy * dy < 1.5f) return 13;
            if (MathF.Abs(u - 6.5f) < 0.45f && MathF.Abs(v) < h * 0.75f) return 2;   // the gill cover
            float f = v / MathF.Max(0.5f, h);
            if (f > 0.78f) return u > -8 && u < 6 && f > 0.86f ? 15 : 2;
            if (f > 0.38f) return 3;
            if (f < -0.3f) return 5;
            // Bars down the flank, the marlin's lit-up stripes.
            float bar = (u + 30) / 4.2f;
            if (u < 5 && u > -12 && bar - MathF.Floor(bar) < 0.24f) return 6;
            return 4;
        }
        if (u > -17.5f && u < -14 && MathF.Abs(v) <= h) return 2;
        return 0;
    }

    // Draws the marlin centred on (cx, cy), its nose pointing at angle a (0 right, positive down the screen), s times its
    // size; tail is the tail's beat, sail how far the sail is raised (0 folded, 1 up). Under the surface (sub > 0) the body
    // is tinted toward the sea and see-through, and only the sail stands out; finOnly draws just the sail.
    void DrawMarlin(float cx, float cy, float a, float s, float tail, float sail, float sub = 0, bool finOnly = false)
    {
        float ca = MathF.Cos(a), sa = MathF.Sin(a);
        // The back stays up whichever way it faces.
        float flip = ca < 0 ? -1 : 1;
        int R = (int)MathF.Ceiling(27 * s);
        var sea = Pal.C("#1d4d74");
        int Mat(float x, float y)
        {
            float u = (x * ca + y * sa) / s, v = -(-x * sa + y * ca) * flip / s;
            return MarlinAt(u, v, tail, sail);
        }
        for (int y = -R; y <= R; y++)
            for (int x = -R; x <= R; x++)
            {
                int m = Mat(x, y);
                bool sailPx = m is 7 or 8 or 9;
                if (finOnly && !sailPx) continue;
                if (m == 0)
                {
                    if (finOnly || sub > 0.5f) continue;
                    if (Mat(x + 1, y) == 0 && Mat(x - 1, y) == 0 && Mat(x, y + 1) == 0 && Mat(x, y - 1) == 0) continue;
                    m = 1;
                }
                var c = MarlinCol[m];
                if (sub > 0 && !sailPx) { c = Pix.Mix(c, sea, 0.55f * sub); c.A = (byte)(255 * (1 - 0.55f * sub)); }
                pix.Rect(cx + x, cy + y, 1, 1, c);
            }
    }

    // Under the surface while it circles: its body a dark shape in the water, the sail cutting the surface with a V of
    // white water behind it and spray off its leading edge. Drawn with the sea's things, before the boat.
    void DrawIronbillUnder(float t)
    {
        if (!IronbillOnLine || OldArt) return;
        var b = ironbill;
        if (b == null || reel.Leap > 0 || b.Walk > 0 || b.ChargeT > 0) return;
        bool warn = reel.AttackWarning > 0;
        float a = warn ? MathF.Atan2(player.Y - fish.By, player.X - fish.Bx) : IronbillHeading();
        float x = fish.Bx, y = fish.By + 2, tail = MathF.Sin(t * (warn ? 16 : 9));
        // The wake: two lines of white water opening out behind the sail.
        float hx = MathF.Cos(a), hy = MathF.Sin(a);
        for (int k = 2; k < 20; k++)
        {
            float ox = x - hx * k * 1.6f, oy = y - hy * k * 1.1f, spread = k * 0.5f, al = 0.75f * (1 - k / 20f);
            pix.Rect(ox - hy * spread, oy + hx * spread * 0.7f, 1, 1, Pal.Rgba(238, 249, 251, al));
            pix.Rect(ox + hy * spread, oy - hx * spread * 0.7f, 1, 1, Pal.Rgba(238, 249, 251, al));
        }
        DrawMarlin(x, y, a, 0.85f, tail, 0, sub: 1);
        DrawMarlin(x, y, a, 0.85f, tail, warn ? 1.1f : 0.85f, finOnly: true);
        // Spray off the front of the sail.
        for (int i = 0; i < 4; i++)
        {
            float ph = (t * 3 + i * 0.25f) % 1;
            pix.Rect(x + hx * 9 + (i - 1.5f) * 1.5f, y - 2 - ph * 4, 1, 1, Pal.Rgba(238, 249, 251, 0.8f * (1 - ph)));
        }
        if (warn)
        {
            // The charge lane: a flashing red dashed line from the fish to the boat, and a "!" over the sail.
            bool on = (int)(t * 10) % 2 == 0;
            float dx = player.X - x, dy = player.Y - y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            for (float k = 6; k < d + 10; k += 4)
                pix.Rect(x + dx / d * k, y + dy / d * k, 2, 1, Pal.Rgba(255, 90, 70, on ? 0.9f : 0.45f));
            pix.Rect(x - 1, y - 18, 3, 6, "#10243a"); pix.Rect(x, y - 17, 1, 4, "#ffd76a"); pix.Rect(x, y - 12, 1, 1, "#ffd76a");
        }
        if (b.LandFx > 0)
        {
            float k = 1 - b.LandFx / 0.6f;
            pix.Ring(x, y + 1, 3 + k * 14, Pal.Rgba(238, 249, 251, 0.8f * (1 - k)));
        }
    }

    // Above the water: the breach, the charge across to the boat, and the tail-walk. Drawn over everything else.
    void DrawIronbillOver(float t)
    {
        var b = ironbill;
        if (b == null || !IronbillOnLine || OldArt) return;
        float dirX = MathF.Cos(IronbillHeading()) >= 0 ? 1 : -1;
        float x = fish.Bx, y = fish.By;
        if (reel.Leap > 0)
        {
            float k = 1 - reel.Leap / reel.LeapLen;
            float jx = x + (k - 0.5f) * 56 * dirX, jy = y - MathF.Sin(k * MathF.PI) * 34;
            float pitch = 1.05f * (1 - 2 * k);
            float a = dirX > 0 ? -pitch : MathF.PI + pitch;
            // Its shadow on the water, and the burst where it left and where it comes down.
            pix.Rect(jx - 10, y + 1, 20, 2, Pal.Rgba(10, 30, 50, 0.25f));
            if (k < 0.2f || k > 0.8f)
            {
                float ex = x + (k < 0.5f ? -28 : 28) * dirX, ph = k < 0.5f ? k / 0.2f : (k - 0.8f) / 0.2f;
                pix.Ring(ex, y + 1, 2 + ph * 9, Pal.Rgba(238, 249, 251, 0.85f * (1 - ph * 0.6f)));
                for (int i = 0; i < 9; i++)
                {
                    float sx = ex + (i - 4) * 2.2f, sy = y - MathF.Abs(MathF.Sin(i * 1.7f)) * 8 * (1 - ph) - ph * 3;
                    pix.Rect(sx, sy, 1, 2, Pal.Rgba(238, 249, 251, 0.9f));
                }
            }
            // Water streaming off it as it flies.
            for (int i = 0; i < 6; i++)
            {
                float d = (t * 40 + i * 6) % 24;
                pix.Rect(jx - MathF.Cos(a) * (8 + d), jy - MathF.Sin(a) * (8 + d) + d * 0.4f, 1, 1, Pal.Rgba(220, 240, 250, 0.7f * (1 - d / 24)));
            }
            DrawMarlin(jx, jy, a, 1, MathF.Sin(t * 14), 1);
        }
        else if (b.ChargeT > 0)
        {
            float k = 1 - b.ChargeT / ChargeLen;
            float a = MathF.Atan2(b.Cy1 - b.Cy0, b.Cx1 - b.Cx0);
            float hop = MathF.Sin(k * MathF.PI) * 8;
            // A sheet of spray along its path.
            for (int i = 0; i < 12; i++)
            {
                float q = k - i * 0.04f;
                if (q < 0) break;
                float sx = b.Cx0 + (b.Cx1 - b.Cx0) * q, sy = b.Cy0 + (b.Cy1 - b.Cy0) * q;
                pix.Rect(sx + (i % 3 - 1) * 2, sy - (i % 2) * 2, 2, 1, Pal.Rgba(238, 249, 251, 0.85f * (1 - i / 12f)));
            }
            DrawMarlin(fish.Bx, fish.By - hop, a, 1, MathF.Sin(t * 22), 1);
        }
        else if (b.Walk > 0)
        {
            // Up on its tail, nose to the sky, shaking its head, the tail churning the water white.
            float shake = MathF.Sin(t * 26) * 0.16f, bob = MathF.Abs(MathF.Sin(t * 13)) * 2;
            float a = dirX > 0 ? -1.32f + shake : MathF.PI + 1.32f - shake;
            for (int i = 0; i < 10; i++)
            {
                float ph = (t * 4 + i * 0.1f) % 1;
                pix.Rect(x - dirX * (i * 2.2f) + MathF.Sin(i * 2.1f) * 2, y + 1 - ph * 5, 2, 1, Pal.Rgba(238, 249, 251, 0.9f * (1 - ph)));
            }
            pix.Ring(x, y + 1, 4 + (t * 6 % 1) * 6, Pal.Rgba(238, 249, 251, 0.6f));
            DrawMarlin(x, y - 18 - bob, a, 1, MathF.Sin(t * 30), 0.9f);
        }
    }
}
