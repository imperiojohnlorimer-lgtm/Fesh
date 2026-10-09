namespace Fesh;

// Each morning rolls the day's forecast (state.forecast): stretches of clear weather, rain and storms through the day,
// which state.weather follows as the clock runs. Rain makes fish bite faster; a storm makes them bite faster still,
// but closes the bridges (not the jetties) until it passes. Rain and storms fade in and out on screen (rainAmt, stormAmt).
partial class Game
{
    float flash, thunderT = 6;
    float rainAmt, stormAmt;          // how hard it's raining on screen, easing towards the weather
    string planned;                   // the forecast's weather the last time we looked, so a change is noticed once
    int warnedStorm = -1;             // the forecast entry we've already warned about

    // A day's forecast. Clear days (most) may get a passing shower. Rain days rain for a good part of the day. From
    // day 3, a storm day builds from rain into a storm for a few hours, then eases off. The season (Seasons.cs) sets
    // the odds: the amihan is mostly dry, the habagat wet, with more storms and longer ones.
    List<WeatherSpell> MakeForecast(int day)
    {
        var plan = new List<WeatherSpell>();
        void Add(int at, string w) { if (at < DayMin && (plan.Count == 0 || plan[^1].w != w)) plan.Add(new WeatherSpell { at = at, w = w }); }
        int Pick(int lo, int hi, int step = 15) => lo + rng.Next((hi - lo) / step + 1) * step;
        bool wet = SeasonOf(day) == "habagat";
        double r = rng.NextDouble(), storm = wet ? 0.24 : 0.05, rain = storm + (wet ? 0.5 : 0.2), shower = wet ? 0.4 : 0.25;
        if (day >= 3 && r < storm)
        {
            int start = rng.NextDouble() < 0.25 ? 0 : Pick(60, 540), lead = Pick(30, 90), len = wet ? Pick(120, 330) : Pick(120, 240);
            Add(0, start == 0 ? "rain" : "clear");
            Add(start, "rain");
            Add(start + lead, "storm");
            Add(start + lead + len, "rain");
            Add(start + lead + len + Pick(60, 180), "clear");
        }
        else if (r < rain)
        {
            int start = rng.NextDouble() < 0.5 ? 0 : Pick(60, 420), len = Pick(240, 600);
            Add(0, start == 0 ? "rain" : "clear");
            Add(start, "rain");
            Add(start + len, "clear");
        }
        else
        {
            Add(0, "clear");
            if (rng.NextDouble() < shower) { int s = Pick(120, 720); Add(s, "rain"); Add(s + Pick(60, 180), "clear"); }
        }
        return plan;
    }

    // Each morning: yesterday's "tomorrow" becomes today (so what Tomas or Pip told you comes true), and the next day
    // is rolled now, so they can tell you about it.
    void RollWeather()
    {
        state.forecast = state.tomorrow ?? MakeForecast(state.day);
        state.tomorrow = MakeForecast(state.day + 1);
        state.weather = planned = Planned(SinceDawn);
        warnedStorm = -1;
    }

    // Tomorrow, in a few words: "fair all day", "rain from about 10 AM until about 4 PM", "a long storm around 2 PM".
    // Only the plan decides it (not today's season), so what Tomas says still reads the same once it comes true.
    string DescribeDay(List<WeatherSpell> plan)
    {
        string At(int m) => HourText(DawnMin + m);
        var storm = plan.FirstOrDefault(s => s.w == "storm");
        if (storm != null) return $"a {StormLength(plan, storm.at)}storm " + (storm.at == 0 ? "from first light" : $"around {At(storm.at)}");
        int i = plan.FindIndex(s => s.w == "rain");
        if (i < 0) return "fair all day";
        int start = plan[i].at, end = i + 1 < plan.Count ? plan[i + 1].at : (int)DayMin;
        if (end - start <= 180) return $"a shower around {At(start)}";
        string from = start == 0 ? "first light" : $"about {At(start)}";
        return end >= DayMin ? $"rain from {from} on" : $"rain from {from} until about {At(end)}";
    }

    // Once Tomas or Pip has told you today, the menu and the clock remember tomorrow's weather.
    bool KnowTomorrow => state.toldDay == state.day;
    void TellTomorrow() => state.toldDay = state.day;

    int SpellAt(float sinceDawn)
    {
        int i = 0;
        while (i + 1 < state.forecast.Count && state.forecast[i + 1].at <= sinceDawn) i++;
        return i;
    }

    string Planned(float sinceDawn) => state.forecast[SpellAt(sinceDawn)].w;

    // Called as the clock moves. Only a change in the forecast changes the weather, so anything that sets
    // state.weather directly (the autotest does) holds until the next change.
    void FollowForecast(bool quiet)
    {
        float now = SinceDawn;
        int i = SpellAt(now);
        // Underground you don't hear the weather change.
        quiet |= scene == "cave";
        string news = null;
        string w = state.forecast[i].w;
        if (w != planned)
        {
            planned = w;
            if (w != state.weather)
            {
                string was = state.weather;
                state.weather = w;
                news = (was, w) switch
                {
                    (_, "storm") => $"{(Season == "habagat" ? "A bagyo" : "A storm")} is raging! The bridges are closed until it passes.",
                    ("storm", "rain") => "The storm is easing off. The bridges are open again.",
                    ("storm", _) => "The storm has passed. The bridges are open again.",
                    (_, "rain") => "It's starting to rain. The fish are biting.",
                    _ => "The rain has stopped."
                };
            }
        }
        // Half an hour's warning before a storm (added to the rain's news if the rain only just started).
        if (i + 1 < state.forecast.Count && state.forecast[i + 1].w == "storm" && warnedStorm != i + 1
            && state.forecast[i + 1].at - now <= 30 && !Stormy && !quiet)
        {
            warnedStorm = i + 1;
            news = (news == null ? "" : news + " ") + "Dark clouds are piling up. A storm is coming!";
        }
        if (news != null && !quiet) Toast(news, 3.5f);
    }

    // When the weather in the forecast next changes, and to what (null if it stays like this until morning).
    (float at, string w)? NextChange()
    {
        int i = SpellAt(SinceDawn);
        return i + 1 < state.forecast.Count ? (state.forecast[i + 1].at, state.forecast[i + 1].w) : null;
    }

    // Shelter waits until the storm is over (or until morning if it's still going then).
    float StormEnds()
    {
        float now = SinceDawn;
        var next = state.forecast.FirstOrDefault(s => s.at > now && s.w != "storm");
        return next != null ? Wrap(DawnMin + next.at) : DawnMin;
    }

    float WeatherBite => state.weather switch { "storm" => 0.6f, "rain" => 0.75f, _ => 1f };
    bool Stormy => state.weather == "storm";

    // A storm closes the bridges, but if one caught you halfway across you can still walk off it.
    bool BridgeClosed(int tx, int ty) => scene == "world" && Stormy && bridgeSet.Contains((tx, ty)) && !OnBridge();
    bool OnBridge()
    {
        float x = player.X, y = player.Y;
        foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
            if (bridgeSet.Contains(((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T)))) return true;
        return false;
    }

    // The weather right now, then what the forecast says comes next.
    string WeatherNews() => (state.weather switch
    {
        "rain" => " Rain is falling. The fish are biting.",
        "storm" => " A storm is raging! The bridges are closed until it passes.",
        _ => ""
    }) + (Forecast() is string f ? " " + f : "");

    string Forecast()
    {
        if (NextChange() is not var (at, w)) return null;
        string when = HourText(DawnMin + at);
        return (state.weather, w) switch
        {
            (_, "storm") => $"A storm is on the way, around {when}.",
            ("storm", "rain") => $"The storm should ease off around {when}.",
            ("storm", _) or ("rain", _) => $"It should clear up around {when}.",
            _ => $"Rain is likely around {when}."
        };
    }

    void UpdateWeather(float dt)
    {
        float wantRain = state.weather == "clear" ? 0 : 1, wantStorm = Stormy ? 1 : 0;
        rainAmt += Math.Clamp(wantRain - rainAmt, -dt * 0.4f, dt * 0.4f);
        stormAmt += Math.Clamp(wantStorm - stormAmt, -dt * 0.4f, dt * 0.4f);
        flash = Math.Max(0, flash - dt * 3);
        if (!Stormy || scene != "world" || mode is "title" or "create") return;
        thunderT -= dt;
        if (thunderT > 0) return;
        flash = 1;
        thunderT = Rand(6, 14);
        Sfx.Play("thunder");
    }

    // Straight to the weather's full strength, after a load or a rest (no fade-in to watch).
    void SnapWeather()
    {
        rainAmt = state.weather == "clear" ? 0 : 1;
        stormAmt = Stormy ? 1 : 0;
    }

    void DrawWeatherTint()
    {
        if (rainAmt <= 0) return;
        float k = stormAmt;
        pix.Fill(0, 0, W, H, Pal.Rgba((byte)(50 - 20 * k), (byte)(70 - 30 * k), (byte)(95 - 35 * k), rainAmt * (0.16f + 0.12f * k)));
    }

    // Slanting rain everywhere except Frostfang, where it falls as snow instead.
    void DrawRain(float t)
    {
        if (rainAmt > 0)
        {
            int n = (int)(120 * rainAmt + 110 * stormAmt);
            bool heavy = stormAmt > 0.5f;
            int len = heavy ? 5 : 3;
            float fall = 180 + 80 * stormAmt, drift = -30 - 40 * stormAmt;
            var drop = Pal.C("rgba(200,220,235,0.55)");
            for (int i = 0; i < n; i++)
            {
                double sx = (Pix.Hash(i, 7, 95) * (W + 40) + t * drift) % (W + 40);
                if (sx < 0) sx += W + 40;
                sx -= 20;
                double sy = (Pix.Hash(i, 8, 95) * H + t * fall * (0.8 + Pix.Hash(i, 9, 95) * 0.4)) % H;
                if (BiomeAt(((int)sx + camX) / T, ((int)sy + camY) / T) == 1) continue;
                for (int k = 0; k < len; k++) pix.Fill((int)sx - (heavy ? k / 2 : 0), (int)sy + k, 1, 1, drop);
            }
        }
        if (flash > 0) pix.Fill(0, 0, W, H, Pal.Rgba(235, 240, 255, flash * 0.55f));
    }

    // Sunset reddens everything for a while before dark, and dawn comes up pink.
    void DrawSunGlow()
    {
        var (dusk, dawn) = SunGlow();
        float clear = 1 - 0.6f * rainAmt;
        if (dusk > 0) pix.Fill(0, 0, W, H, Pal.Rgba(255, 128, 60, 0.21f * dusk * clear));
        if (dawn > 0) pix.Fill(0, 0, W, H, Pal.Rgba(255, 168, 150, 0.13f * dawn * clear));
    }
}
