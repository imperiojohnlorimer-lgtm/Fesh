namespace Fesh;

// The island clock. state.clock is minutes since midnight and runs while you play (ActiveModes only: never in menus,
// panels, dialogue, cards or fades). A whole day and night takes Settings.Data.dayLength real minutes (24 by default).
// A new day starts at 06:00, not midnight, so a night belongs to the day before it: the moon phase, the ice hole,
// crab pots and the derby stay the same all night, as they did when resting flipped day and night.
// Night is 20:00 to 06:00; the light fades over the hour either side. Resting skips ahead through the same Advance.
partial class Game
{
    const float DawnMin = 6 * 60, DuskMin = 20 * 60, DayMin = 24 * 60;
    const float NewGameClock = 8 * 60;

    bool Night => state.clock >= DuskMin || state.clock < DawnMin;
    // Minutes since this day began at 06:00 (0 to 1440), which is how the forecast is timed.
    float SinceDawn => Wrap(state.clock - DawnMin);
    static float Wrap(float m) => (m % DayMin + DayMin) % DayMin;

    // 0 in daylight, 1 at full night: easing in from 19:00 to 21:00 and out from 05:00 to 07:00.
    float Darkness
    {
        get
        {
            float c = state.clock;
            if (c >= 21 * 60 || c < 5 * 60) return 1;
            if (c >= 19 * 60) return Smooth((c - 19 * 60) / 120);
            if (c < 7 * 60) return 1 - Smooth((c - 5 * 60) / 120);
            return 0;
        }
    }
    static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    // The warm light of sunset (peaking at 19:30) and the pink of dawn (peaking at 06:00), 0 to 1.
    (float dusk, float dawn) SunGlow()
    {
        float c = state.clock;
        float Bump(float mid, float half) => Math.Max(0, 1 - Math.Abs(c - mid) / half);
        return (Smooth(Bump(19.5f * 60, 90)), Smooth(Bump(6 * 60, 75)));
    }

    // Time stands still while Bakunawa holds the moon.
    void TickClock(float dt)
    {
        float rate = Settings.GameMinutesPerSecond;
        if (rate > 0 && eclipse == null) Advance(dt * rate, quiet: false);
    }

    // Moves the clock forward, stopping at each dusk and dawn on the way so nothing is skipped (resting can cross both).
    // Quiet skips the toasts and saving; resting shows its own message once the fade lifts.
    void Advance(float mins, bool quiet)
    {
        while (mins > 0)
        {
            float toDusk = Until(DuskMin), toDawn = Until(DawnMin), hit = Math.Min(toDusk, toDawn);
            if (mins < hit) { DryRacks(state.clock, mins); state.clock = Wrap(state.clock + mins); FollowForecast(quiet); return; }
            DryRacks(state.clock, hit);
            state.clock = toDusk < toDawn ? DuskMin : DawnMin;
            mins -= hit;
            if (toDusk < toDawn) { FollowForecast(quiet); if (!quiet) Toast(FullMoon ? "Night falls. The moon is full tonight." : "Night falls.", 3); }
            else NewDay(quiet);
        }
    }

    // Minutes until the clock next reads `at` (a full day if it reads that now).
    float Until(float at) { float d = Wrap(at - state.clock); return d <= 0 ? DayMin : d; }

    void SkipTo(float at) => Advance(Wrap(at - state.clock), quiet: true);

    // 06:00. What resting into the morning used to do: a new day, new weather, trees and berries back.
    // Resting itself still costs food and heals you; a morning that just arrives doesn't.
    void NewDay(bool quiet)
    {
        // A storm in the day that's ending tears at Maya's guso lines (Seaweed.cs); the forecast is still that day's here.
        TearGuso();
        state.day++;
        RollWeather();
        Regrow();
        // A line already down the ice hole keeps it open.
        if (fish?.Spot == "icehole") state.iceDay = state.day;
        if (scene == "world") FillLoose();
        mapTexDirty = true;
        if (quiet) return;
        Save();
        string turn = SeasonTurn();
        Toast($"Morning of day {state.day}." + turn + WeatherNews() + FestivalNews() + (state.food < 25 ? " You're getting hungry." : ""), turn != "" ? 7 : 4.5f);
    }

    // Chopped trees and broken boulders whose time is up grow back, one tile at a time (rebuilding the whole map would
    // stall the game). Not under the player, the mount, a build or something lying on the ground: those wait a day.
    void Regrow()
    {
        foreach (var key in state.felled.Keys.ToList())
        {
            var parts = key.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) continue;
            if (!stumps.TryGetValue((x, y), out char kind) || state.day - state.felled[key] < (kind == 'R' ? BoulderRegrowDays : TreeRegrowDays)) continue;
            if (RegrowBlocked(x, y)) continue;
            state.felled.Remove(key);
            stumps.Remove((x, y));
            worldMap[y, x] = kind;
            trees.Add((x, y, kind));
            RenderTile(x, y);
            mapTexDirty = true;
        }
    }

    bool RegrowBlocked(int x, int y)
    {
        float x0 = x * T, y0 = y * T;
        bool Inside(float px, float py, float pad) => px + pad > x0 && px - pad < x0 + T && py + pad > y0 && py - pad < y0 + T;
        if (scene == "world" && Inside(player.X, player.Y, 6)) return true;
        if (state.tamed && !state.riding && Inside(state.mountX, state.mountY, 10)) return true;
        if (animals.Any(a => Inside(a.X, a.Y, 6))) return true;
        if (state.loose.Any(l => l.tx == x && l.ty == y)) return true;
        return state.builds.Any(b => b.y == y && x >= b.x && x < b.x + Data.BuildById[b.id].W);
    }

    // "2:40 PM", or "14:40" with the 24-hour clock. The HUD shows ten-minute steps.
    static string ClockText(float minutes, int step = 1)
    {
        int m = (int)Wrap(minutes) / step * step, h = m / 60, mm = m % 60;
        if (Settings.Data.clock24) return $"{h:00}:{mm:00}";
        return $"{(h % 12 == 0 ? 12 : h % 12)}:{mm:00} {(h < 12 ? "AM" : "PM")}";
    }

    // "2 PM" or "14:00", for forecasts.
    static string HourText(float minutes)
    {
        int h = (int)MathF.Round(Wrap(minutes) / 60) % 24;
        if (Settings.Data.clock24) return $"{h:00}:00";
        return $"{(h % 12 == 0 ? 12 : h % 12)} {(h < 12 ? "AM" : "PM")}";
    }
}
