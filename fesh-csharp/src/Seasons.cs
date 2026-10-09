namespace Fesh;

// The two monsoons the islands are named after. The amihan is the cool northeast wind (dry, on these islands); the
// habagat is the wet southwest monsoon that brings the storms. They take turns every SeasonDays days, the game's own
// short calendar (the first days of a new game are amihan), worked out from the day, so nothing is saved. A season
// changes the weather the forecast rolls (Weather.cs), which way the wind helps a boat, which seasonal fish bite
// (CommonFish.Season), and what Manang Rosa orders (Habagat.cs).
partial class Game
{
    const int SeasonDays = 5;

    static string SeasonOf(int day) => (Math.Max(day, 1) - 1) / SeasonDays % 2 == 0 ? "amihan" : "habagat";
    string Season => SeasonOf(state.day);
    int SeasonDay => (Math.Max(state.day, 1) - 1) % SeasonDays + 1;
    static string SeasonName(string season) => season == "amihan" ? "Amihan" : "Habagat";

    // "Amihan season (northeast wind), day 2 of 5", for the clock and the menu.
    string SeasonLine() => $"{SeasonName(Season)} season ({(Season == "amihan" ? "northeast" : "southwest")} wind), day {SeasonDay} of {SeasonDays}";

    // The morning a season begins, a line for the morning toast (empty on any other morning).
    string SeasonTurn()
    {
        if (state.day <= 1 || SeasonOf(state.day) == SeasonOf(state.day - 1)) return "";
        return Season == "amihan"
            ? " The amihan has come: the cool northeast wind. Drier days on these islands, and the wind is behind you sailing southwest."
            : " The habagat has come: the southwest monsoon. Wetter days and more storms on these islands, and the wind is behind you sailing northeast.";
    }

    // Added to what Tomas or Pip say about tomorrow, when tomorrow starts the other season.
    string SeasonTomorrow() => SeasonOf(state.day + 1) == Season ? ""
        : SeasonOf(state.day + 1) == "habagat" ? " And the wind turns tomorrow: the habagat is coming." : " And the wind turns tomorrow: the amihan is coming.";

    // The wind blows towards the southwest in the amihan and the northeast in the habagat (screen y points south). Sailing
    // with it is up to 15% quicker, against it up to 15% slower. (ux, uy) is the direction you're steering, length 1.
    float WindFactor(float ux, float uy)
    {
        float s = Season == "amihan" ? 1 : -1;
        return 1 + 0.15f * (-ux + uy) * s / MathF.Sqrt(2);
    }

    // "short " for a storm of up to three hours, "long " for one over four and a half, otherwise nothing, for DescribeDay.
    static string StormLength(List<WeatherSpell> plan, int at)
    {
        int i = plan.FindIndex(s => s.at == at && s.w == "storm");
        if (i < 0) return "";
        float len = (i + 1 < plan.Count ? plan[i + 1].at : DayMin) - plan[i].at;
        return len <= 180 ? "short " : len > 270 ? "long " : "";
    }
}
