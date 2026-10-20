using Raylib_cs;

namespace Fesh;

// The sky before and after rain, and the ground it leaves wet (1.43). Everything here is for light on (Settings > Graphics);
// with light off the weather is as it was. Two eased amounts drive it:
//   overShown: how overcast it looks, 0 to 1. It builds over the hour before a planned rain or storm, holds while it rains
//     and fades over the 45 minutes after, so clouds gather instead of the weather switching at an exact minute.
//   wetShown: how wet the ground is, 0 to 1: soaked while it rains, then drying over about three game hours.
// Both are worked out from the forecast and `state.lastRain` (the latest moment it rained, saved as an absolute minute), so
// they survive resting and time jumps; neither is stored, and SnapWeather puts them straight to where they should be.
partial class Game
{
    float overShown, wetShown;
    const float DryMinutes = 200, ClearMinutes = 45, LeadMinutes = 60;

    // Minutes until the next rain or storm starts: 0 if it is raining now, a huge number if none is planned.
    float RainLead()
    {
        if (state.weather != "clear") return 0;
        var fc = state.forecast;
        if (fc == null || fc.Count == 0) return float.MaxValue;
        float now = SinceDawn;
        for (int j = SpellAt(now) + 1; j < fc.Count; j++) if (fc[j].w != "clear") return fc[j].at - now;
        // The plan ends at 06:00; tomorrow may start wet.
        if (state.tomorrow is { Count: > 0 } tm && tm[0].at == 0 && tm[0].w != "clear") return DayMin - now;
        return float.MaxValue;
    }

    float MinutesSinceRain => TideNow - state.lastRain;

    float OvercastTarget()
    {
        float lead = RainLead();
        if (lead <= 0) return 1;
        float build = lead < LeadMinutes ? 0.8f * (1 - lead / LeadMinutes) : 0;
        float age = MinutesSinceRain;
        float clearing = age >= 0 && age < ClearMinutes ? 0.9f * (1 - age / ClearMinutes) : 0;
        return Math.Max(build, clearing);
    }

    float WetTarget()
    {
        if (state.weather != "clear") return 1;
        float age = MinutesSinceRain;
        return age < 0 || age >= DryMinutes ? 0 : 1 - age / DryMinutes;
    }

    // Every frame in play: the two amounts ease towards where the sky and ground should be.
    void UpdateOvercast(float dt)
    {
        float step = dt * 0.08f;
        overShown += Math.Clamp(OvercastTarget() - overShown, -step, step);
        wetShown += Math.Clamp(WetTarget() - wetShown, -step, step);
    }

    void SnapOvercast() { overShown = OvercastTarget(); wetShown = WetTarget(); }

    // Clock.Advance calls this for each stretch of time (never across a dawn or a dusk): remembers the latest moment of rain
    // in it. A short step looks at the weather now; a long one (resting) reads the forecast's spells. Absolute minutes, so a
    // night that rained is still wet the next morning.
    void NoteRain(float from, float len)
    {
        if (len <= 0 || state.forecast == null) return;
        float absStart = TideMinute(state.day, from);
        if (len <= 5)
        {
            if (state.weather != "clear") state.lastRain = Math.Max(state.lastRain, absStart + len);
            return;
        }
        float s0 = Wrap(from - DawnMin), s1 = s0 + len, lastEnd = -1;
        for (int i = 0; i < state.forecast.Count; i++)
        {
            if (state.forecast[i].w == "clear") continue;
            float f0 = state.forecast[i].at, f1 = i + 1 < state.forecast.Count ? state.forecast[i + 1].at : DayMin;
            float o0 = Math.Max(s0, f0), o1 = Math.Min(s1, f1);
            if (o1 > o0) lastEnd = o1;
        }
        if (lastEnd >= 0) state.lastRain = Math.Max(state.lastRain, absStart + (lastEnd - s0));
    }

    /* ---------- Wet ground ---------- */
    // How much each ground darkens when it is soaked (sand most, grass least).
    static float WetK(char g) => g switch { 's' => 0.34f, 'p' => 0.30f, 'D' => 0.22f, 'e' => 0.16f, 'g' or 'j' => 0.10f, _ => 0 };

    static uint WetHash(int a, int b, int s)
    {
        uint h = (uint)(a * 73856093 ^ b * 19349663 ^ s * 83492791);
        h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
        return h;
    }

    // A pass over the ground in view, right after the base picture is copied and before anything stands on it: wet ground is
    // darker and cooler, in blotches that shrink as it dries, with a few pale puddles on sand and paths while it is soaked.
    // The baked layers are never touched (the chart and the Light: Off look are unchanged).
    void DrawWetGround()
    {
        if (!liveRender || scene != "world" || LightMode == 0 || Quality.Rung >= 5) return;
        float w = wetShown;
        if (w <= 0.02f) return;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        var buf = pix.Buf;
        for (int ty = vy0; ty <= vy1; ty++)
            for (int tx = vx0; tx <= vx1; tx++)
            {
                if (BiomeAt(tx, ty) == 1) continue;   // Frostfang gets snow, not wet ground
                bool puddleTile = w > 0.55f && WetHash(tx, ty, 877) % 100 < 9;
                for (int py = 0; py < T; py++)
                {
                    int wy = ty * T + py, sy = wy - camY;
                    if (sy < 0 || sy >= H || wy >= PH) continue;
                    for (int px = 0; px < T; px++)
                    {
                        int wx = tx * T + px, sx = wx - camX;
                        if (sx < 0 || sx >= W || wx >= PW) continue;
                        char g = shape[wy * PW + wx];
                        float k = WetK(g);
                        if (k <= 0) continue;
                        // Wet in blotches of about three pixels: as it dries the blotches shrink and go.
                        float blot = (WetHash(wx / 3, wy / 3, 871) & 0xFFFF) / 65535f;
                        if (blot > w * 1.15f) continue;
                        float s = k * w * (0.75f + 0.25f * ((WetHash(wx, wy, 873) & 255) / 255f)) * 0.8f;
                        var c = buf[sy * W + sx];
                        int r = (int)(c.R * (1 - s * 0.9f)), gg = (int)(c.G * (1 - s * 0.75f)), b = (int)(c.B * (1 - s * 0.45f));
                        // A puddle: a small oval in the middle of the tile, pale where it catches the sky.
                        if (puddleTile && g is 's' or 'p')
                        {
                            float ex = (px - 4.5f) / 3.4f, ey = (py - 4.5f) / 1.9f, d = ex * ex + ey * ey;
                            if (d < 1)
                            {
                                float pa = 0.5f * w * (1 - d * 0.4f);
                                r = (int)(r + (150 - r) * pa); gg = (int)(gg + (188 - gg) * pa); b = (int)(b + (212 - b) * pa);
                                if (py == 3 && d < 0.6f) { r = Math.Min(255, r + 28); gg = Math.Min(255, gg + 28); b = Math.Min(255, b + 24); }
                            }
                        }
                        buf[sy * W + sx] = new Color((byte)r, (byte)gg, (byte)b, (byte)255);
                    }
                }
            }
    }

    // After rain: shafts of sun through the thinning cloud (a stronger sun beam than the dawn and dusk ones), for the first
    // couple of hours of a clear day that is still wet. 0 to 1.
    float ClearingShafts()
    {
        if (LightMode == 0 || state.weather != "clear" || Darkness > 0.3f) return 0;
        float age = MinutesSinceRain;
        if (age < 3 || age > 150) return 0;
        return Smooth(Math.Min(age - 3, 150 - age) / 30f) * (1 - Darkness);
    }
}
