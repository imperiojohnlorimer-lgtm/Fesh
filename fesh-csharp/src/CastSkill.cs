namespace Fesh;

// The cast skill shot (1.45.1). Now and then, while you stand facing a fishing spot, a patch of bubbles stirs the water
// somewhere along the line your cast would fly. Hold to power up (the meter shows a gold band for the power that lands in
// it, and a ring shows where the bobber would fall) and let go on the band: the fish are curious, so that cast bites
// quicker and rare fish a little more often. Never needed, never punishing: a cast that misses is an ordinary cast.
// Looks and the bonus only; the game's rng is not touched (the bubbles use skillRng).
sealed class HotSpot { public string Spot; public float X, Y, Born, Life; }

partial class Game
{
    internal static bool CastSkillOff;   // the old scripts never see bubbles (AutoTestStart); CastSkillScript turns them on
    // The bubbles' reach, how long they last, the wait before the next ones, the bite-time factor of a cast that lands in
    // them (luck +1 comes with it), and how much of the meter the gold band may be (never most of it, never a sliver).
    internal const float HotRadius = 6.5f, HotLife = 14, HotCooldownGone = 12, HotCooldownUsed = 25, HotBonus = 0.8f, HotMaxBand = 0.5f, HotMinBand = 0.06f;

    HotSpot hot;
    float hotNext, hotArmAt = -1, hotLastT;
    readonly Random skillRng = new();

    bool HotOn => !CastSkillOff && Settings.Data.castSkill && !MatchActive && state?.created == true && scene == "world" && !Aboard && !TourActive;

    static bool HotEligible(Spot s) => s.Scene == "world" && s.Id is not ("icehole" or "starwell");

    // Where a cast of this power really falls: the landing point, pulled back out of the sanctuary if it was inside (ok is
    // false when it can't be). Cast, the preview, the gold band and the bubbles all use this one answer.
    (float x, float y, bool ok) FinalLanding(Spot s, float power)
    {
        var (lx, ly) = LandingPoint(s, power);
        for (int i = 0; i < 40 && scene == "world" && InSanctuary(lx, ly); i++) { lx += (player.X - lx) * 0.15f; ly += (player.Y + 3 - ly) * 0.15f; }
        return (lx, ly, !(scene == "world" && InSanctuary(lx, ly)));
    }

    // The powers (0 to 1) whose landing is within HotRadius of (hx, hy), found by sweeping the real landing; null if none.
    (float lo, float hi)? PowerRangeFor(Spot s, float hx, float hy)
    {
        float lo = -1, hi = -1;
        for (int i = 0; i <= 50; i++)
        {
            float p = i / 50f;
            var (lx, ly, ok) = FinalLanding(s, p);
            if (!ok || Dist(lx, ly, hx, hy) > HotRadius) continue;
            if (lo < 0) lo = p;
            hi = p;
        }
        return lo < 0 ? null : (lo, hi);
    }

    // The spot you're about to fish: the one in front of you, or the one you are powering a cast at.
    string HotSpotInReach() => mode switch
    {
        "play" => target?.Type == "spot" ? target.Id : null,
        "charging" => chargeSpot,
        _ => null
    };

    void UpdateCastSkill()
    {
        // A target you are aiming at doesn't fade away while you power up the cast.
        float step = MathF.Max(0, time - hotLastT);
        hotLastT = time;
        if (hot != null && mode == "charging") hot.Born += step;
        if (hot != null && (time - hot.Born >= hot.Life || !HotOn)) { hot = null; hotNext = time + HotCooldownGone; }
        if (!HotOn) { hotArmAt = -1; return; }
        if (hot != null) return;
        string id = HotSpotInReach();
        if (id == null || !Data.SpotById.TryGetValue(id, out var s) || !HotEligible(s) || !SpotOpen(id) || !SpotKnown(s) || s.Id == "amihanpond" && PondClosed)
        {
            hotArmAt = -1;
            return;
        }
        if (hotArmAt < 0) hotArmAt = MathF.Max(time + 3 + (float)skillRng.NextDouble() * 5, hotNext);
        if (time < hotArmAt) return;
        hotArmAt = -1;
        PlaceHot(s, 0.3f + (float)skillRng.NextDouble() * 0.55f, ((float)skillRng.NextDouble() * 2 - 1) * 3);
    }

    // Bubbles at the point a cast of this power would land (from where you stand now), nudged sideways by `lateral` px.
    // False when that isn't open water you could fish, or when the bubbles would be no test: out of reach of every
    // power, or so near that almost any cast lands in them (the gold band must be a part of the meter, not all of it).
    internal bool PlaceHot(Spot s, float power, float lateral)
    {
        var (lx, ly, ok) = FinalLanding(s, power);
        var (sx, sy) = SpotPos(s);
        float dx = sx - player.X, dy = sy - (player.Y - 6), d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        lx += -dy / d * lateral; ly += dx / d * lateral;
        if (!ok || !IsWater(lx, ly) || InSanctuary(lx, ly) || Dist(lx, ly, player.X, player.Y) < 14) return false;
        if (PowerRangeFor(s, lx, ly) is not (float lo, float hi) || hi - lo > HotMaxBand || hi - lo < HotMinBand) return false;
        hot = new HotSpot { Spot = s.Id, X = lx, Y = ly, Born = time, Life = HotLife };
        if (!state.Hinted("hotSpot") && toastTimer <= 0)
        {
            state.hinted["hotSpot"] = true;
            Toast("Bubbles on the water: the fish are stirring. Hold <act>, let go with the meter on its gold mark, and your bobber lands in them for a quicker bite and better odds at a rare one.", 6);
        }
        return true;
    }

    // Does a cast at this spot that lands here fall in the bubbles?
    bool HotHit(string spotId, float lx, float ly) => HotOn && hot != null && hot.Spot == spotId && Dist(lx, ly, hot.X, hot.Y) <= HotRadius;

    void HotUsed()
    {
        hot = null;
        hotNext = time + HotCooldownUsed;
        hotArmAt = -1;
    }

    // The powers (0 to 1) whose landing, from where you stand, is in the bubbles; null if none is or they aren't ours.
    (float lo, float hi)? HotPowerRange()
    {
        if (!HotOn || hot == null || mode != "charging" || chargeSpot == null || hot.Spot != chargeSpot) return null;
        return PowerRangeFor(Data.SpotById[chargeSpot], hot.X, hot.Y);
    }

    /* ---------- Drawing ---------- */
    void DrawHotSpot(float t)
    {
        if (!HotOn || hot == null || !SpotHere(Data.SpotById[hot.Spot])) return;
        float age = time - hot.Born, a = Math.Clamp(age / 0.8f, 0, 1) * Math.Clamp((hot.Life - age) / 2f, 0, 1);
        int x = (int)MathF.Round(hot.X), y = (int)MathF.Round(hot.Y);
        // Stirred, darker water, and a ring that breathes (still under Reduce motion), so it reads by shape as well as colour.
        pix.Rect(x - 5, y - 2, 10, 4, Pal.Rgba(10, 40, 70, 0.38f * a));
        pix.Rect(x - 3, y - 3, 6, 6, Pal.Rgba(10, 40, 70, 0.22f * a));
        float breathe = Calm ? 0.5f : (MathF.Sin(t * 3) + 1) / 2;
        pix.Ring(x, y, HotRadius + breathe * 1.5f, Pal.Rgba(240, 250, 255, 0.9f * a));
        for (int b = 0; b < 5; b++)
        {
            float ph = Calm ? b * 0.2f : (t * 0.7f + b * 0.2f) % 1;
            float bx = x - 5 + b * 2.4f + (Calm ? 0 : MathF.Sin(t * 3 + b) * 0.8f), by = y + 3 - ph * 9;
            pix.Rect(MathF.Round(bx), MathF.Round(by), b % 2 == 0 ? 2 : 1, b % 2 == 0 ? 2 : 1, Pal.Rgba(255, 255, 255, (1f - 0.5f * ph) * a));
        }
        // Where the bobber would fall, while you power up the cast.
        if (mode == "charging" && chargeSpot == hot.Spot)
        {
            var (lx, ly, _) = FinalLanding(Data.SpotById[chargeSpot], charge);
            int px = (int)MathF.Round(lx), py = (int)MathF.Round(ly);
            bool inside = Dist(lx, ly, hot.X, hot.Y) <= HotRadius;
            var c = inside ? Pal.C("#ffd76a") : Pal.Rgba(255, 255, 255, 0.85f);
            pix.Ring(px, py, 3, c);
            pix.Rect(px, py, 1, 1, c);
        }
    }

    // The gold band on the power meter (FishingUi.cs): where on the bar, as 0 to 1, a release lands in the bubbles.
    void DrawHotBand(float x, float y, float w, float h)
    {
        if (HotPowerRange() is not (float lo, float hi)) return;
        var gold = Pal.C("#ffd76a");
        float bx = x + w * lo, bw = MathF.Max(6, w * (hi - lo));
        Gfx.Rect(bx, y - 3, bw, h + 6, Pal.Rgba(255, 215, 106, 0.38f));
        Gfx.Rect(bx, y - 3, bw, 2, gold);
        Gfx.Rect(bx, y + h + 1, bw, 2, gold);
        Gfx.Rect(bx, y - 3, 2, h + 6, gold);
        Gfx.Rect(bx + bw - 2, y - 3, 2, h + 6, gold);
    }
}
