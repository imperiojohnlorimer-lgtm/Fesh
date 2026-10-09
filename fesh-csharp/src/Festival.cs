namespace Fesh;

// The Bangus Festival in Amihan Village (1.15), inspired by Dagupan's: on the last day of every amihan season the
// village hangs bunting across the square and lights a long street grill (Lira hands out grilled bangus instead of her
// stew), Niko judges a bangus derby (the heaviest bangus you land that day, anywhere), and Dado's regatta pays double.
partial class Game
{
    // Days 5, 15, 25...: the last day of each amihan (Seasons.cs).
    static bool FestivalDay(int day) => SeasonOf(day) == "amihan" && SeasonOf(day + 1) == "habagat";
    bool FestivalToday => FestivalDay(state.day);
    bool FestivalHere => FestivalToday && state.Hinted("amihan") && scene == "world";

    // What the dawn toast adds on festival day (once you know the village).
    string FestivalNews() => FestivalToday && state.Hinted("amihan") ? " It's the Bangus Festival in Amihan Village today!" : "";

    // AddCatch calls this: the day's heaviest bangus counts for the derby.
    void FestivalCatch(CommonFish f, float kg)
    {
        if (!FestivalToday || f.Id != "bangus") return;
        if (state.festDay != state.day) { state.festDay = state.day; state.festKg = 0; }
        state.festKg = Math.Max(state.festKg, kg);
    }

    float FestivalBest => state.festDay == state.day ? state.festKg : 0;
    bool DerbyToJudge => FestivalToday && FestivalBest > 0 && state.gifts.GetValueOrDefault("fest_derby") != state.day;

    // Niko judges: gold from 2.6 kg, silver from 2 kg, and something for anyone who tried.
    void JudgeBangus()
    {
        float kg = FestivalBest;
        var (place, coins) = kg >= 2.6f ? ("first prize", 150) : kg >= 2f ? ("second prize", 80) : ("a prize for taking part", 40);
        state.gifts["fest_derby"] = state.day;
        state.coins += coins;
        Sfx.Play(coins >= 150 ? "rare" : "coin");
        Save();
        Talk(new()
        {
            new("Niko", $"The derby's judge, that's me! Let's weigh it... your best bangus today is {Kg(kg)}."),
            new("Niko", $"That's {place}: {coins} coins! " + (coins >= 150 ? "The biggest I've seen all festival." : "Come back next festival and beat it.")),
        });
    }

    // Lira's meal for visiting fishers: stew, or on festival day a grilled bangus off the street grill.
    void LiraMeal()
    {
        state.gifts["lira_meal"] = state.day;
        Give(FestivalToday ? "grilled_fish" : "fish_stew");
        Save();
        Toast(FestivalToday ? "Lira hands you a bangus straight off the festival grill. (+1 grilled fish)" : "Lira packed you a bowl of fish stew. (+1 fish stew)");
    }

    string FestivalNikoLine() => FestivalBest > 0 ? "" : "It's the Bangus Festival! Land the heaviest bangus you can today (the village pond's full of them) and bring it to me to weigh.";

    /* ---------- Drawing ---------- */
    // Bunting across the square, and a long street grill beside the path from the landing, smoking with bangus.
    void AddFestival(List<(float y, Action draw)> list)
    {
        if (MathF.Abs(1640 - player.X) > W + 60 || MathF.Abs(200 - player.Y) > H + 40) return;
        list.Add((205, () => DrawGrill(time)));
        list.Add((600, () => DrawBunting(time)));
    }

    static readonly string[] BuntingCols = { "#e04b3a", "#f3c25b", "#2f7fa3", "#3f9a5a", "#f2efe6" };

    void DrawBunting(float t)
    {
        // Two strings sagging between the houses and a pole by the pond, little flags fluttering on each.
        foreach (var (x0, y0, x1, y1) in new[] { (1611f, 166f, 1699f, 168f), (1606f, 182f, 1702f, 186f) })
        {
            int n = (int)((x1 - x0) / 4);
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n, x = x0 + (x1 - x0) * k, y = y0 + (y1 - y0) * k + MathF.Sin(k * MathF.PI) * 9;
                pix.Rect(x, y, 1, 1, "#6b4a2b");
                if (i % 2 == 1 || i == n) continue;
                int flap = MathF.Sin(t * 5 + i) > 0.4f ? 1 : 0;
                string c = BuntingCols[(i / 2) % BuntingCols.Length];
                pix.Rect(x - 1, y + 1, 3, 1, c); pix.Rect(x, y + 2, 1 + flap, 1, c);
            }
        }
    }

    void DrawGrill(float t)
    {
        // Three half-drum grills on legs in a row, glowing coals, split bangus on the grates, and smoke drifting off.
        for (int g = 0; g < 3; g++)
        {
            int x = 1566 + g * 16, y = 205;
            pix.Rect(x, y + 4, 12, 1, "rgba(0,0,0,0.2)");
            pix.Rect(x + 1, y - 1, 1, 5, "#5b3a24"); pix.Rect(x + 10, y - 1, 1, 5, "#5b3a24");
            pix.Rect(x, y - 4, 12, 3, "#4a4a4e"); pix.Rect(x, y - 4, 12, 1, "#6e6e74");
            pix.Rect(x + 1, y - 5, 10, 1, (int)(t * 5 + g) % 2 == 0 ? "#e04b3a" : "#f3a83b");
            for (int f = 0; f < 2; f++) { pix.Rect(x + 2 + f * 5, y - 7, 4, 2, "#c98b3a"); pix.Rect(x + 2 + f * 5, y - 7, 1, 1, "#8a5420"); }
            for (int k = 0; k < 2; k++)
            {
                double ph = (t * 0.5 + k * 0.5 + g * 0.3) % 1;
                pix.Rect(x + 5 + (int)(Math.Sin(t + k + g) * 2), y - 10 - (int)(ph * 14), 2, 2, Pal.Rgba(220, 220, 214, (float)(0.45 * (1 - ph))));
            }
        }
    }
}
