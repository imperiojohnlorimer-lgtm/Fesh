namespace Fesh;

// Each morning rolls the day's weather. Rain makes fish bite faster; a storm makes them bite faster still,
// but closes the bridges (not the jetties) until the next morning.
partial class Game
{
    float flash, thunderT = 6;

    void RollWeather()
    {
        double r = rng.NextDouble();
        state.weather = state.day >= 3 && r < 0.12 ? "storm" : r < 0.42 ? "rain" : "clear";
    }

    float WeatherBite => state.weather switch { "storm" => 0.6f, "rain" => 0.75f, _ => 1f };
    bool Stormy => state.weather == "storm";
    bool BridgeClosed(int tx, int ty) => scene == "world" && Stormy && bridgeSet.Contains((tx, ty));

    string WeatherNews() => state.weather switch
    {
        "rain" => " Rain is falling. The fish are biting.",
        "storm" => " A storm is raging! The bridges are closed until tomorrow.",
        _ => ""
    };

    void UpdateWeather(float dt)
    {
        flash = Math.Max(0, flash - dt * 3);
        if (!Stormy || scene != "world" || mode is "title" or "create") return;
        thunderT -= dt;
        if (thunderT > 0) return;
        flash = 1;
        thunderT = Rand(6, 14);
        Sfx.Play("thunder");
    }

    void DrawWeatherTint()
    {
        if (state.weather == "clear") return;
        pix.Fill(0, 0, W, H, Stormy ? Pal.Rgba(30, 40, 60, 0.28f) : Pal.Rgba(50, 70, 95, 0.16f));
    }

    // Slanting rain everywhere except Frostfang, where it falls as snow instead.
    void DrawRain(float t)
    {
        if (state.weather == "clear") return;
        int n = Stormy ? 230 : 120, len = Stormy ? 5 : 3;
        float fall = Stormy ? 260 : 180, drift = Stormy ? -70 : -30;
        var drop = Pal.C("rgba(200,220,235,0.55)");
        for (int i = 0; i < n; i++)
        {
            double sx = (Pix.Hash(i, 7, 95) * (W + 40) + t * drift) % (W + 40);
            if (sx < 0) sx += W + 40;
            sx -= 20;
            double sy = (Pix.Hash(i, 8, 95) * H + t * fall * (0.8 + Pix.Hash(i, 9, 95) * 0.4)) % H;
            if (BiomeAt(((int)sx + camX) / T, ((int)sy + camY) / T) == 1) continue;
            for (int k = 0; k < len; k++) pix.Fill((int)sx - (Stormy ? k / 2 : 0), (int)sy + k, 1, 1, drop);
        }
        if (flash > 0) pix.Fill(0, 0, W, H, Pal.Rgba(235, 240, 255, flash * 0.55f));
    }
}
