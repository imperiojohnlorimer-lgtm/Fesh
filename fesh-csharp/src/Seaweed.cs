using Raylib_cs;

namespace Fesh;

// Maya's guso farm in Luntian's karst lagoon (1.15). Guso is a seaweed farmed all over the Philippines: cuttings are tied
// to lines strung between stakes in calm, shallow salt water, grow into fat bunches, and are dried in the sun and sold
// for carrageenan. Maya is starting a farm for the village, a living that takes no fish out of the lagoon. Her two west
// lines are yours from the start, and the east two once the co-op has sold its first dried guso. A cutting is ready on
// the second morning; you keep the harvest but one, which goes straight back on the line, so a line never runs empty.
// A storm while it grows tears half of it away.
partial class Game
{
    const int GusoDays = 2;                 // ready on the second morning after the cutting goes on
    const int GusoYield = 4, GusoTornYield = 2;
    const int GusoHandIn = 4;               // dried guso for the co-op's first sale
    const int GusoCoopPay = 80;
    // The lines run north to south, hugging the lagoon's west and east ends, so its north and south shores stay free for
    // fishing. Each is (x, top, bottom) in world pixels: two west (0, 1), then two east (2, 3).
    static readonly (float x, float y0, float y1)[] GusoLines = { (2026, 158, 168), (2026, 172, 182), (2074, 158, 168), (2074, 172, 182) };
    const float GusoFarmX = 2050, GusoFarmY = 170;
    static readonly Color GusoLeafDark = Pal.Rgba(79, 92, 34, 0.9f), GusoLeaf = Pal.C("#7f8c36"), GusoLeafTip = Pal.C("#d4c46c");

    bool GusoLineOpen(int i) => state.Hinted("guso") && (i < 2 || state.Hinted("gusoCoop"));
    GusoLine GusoAt(int i) => state.guso.GetValueOrDefault(i.ToString());
    bool GusoReady(GusoLine l) => l != null && state.day >= l.day + GusoDays;
    bool CanGiveMayaGuso => state.Hinted("guso") && !state.Hinted("gusoCoop") && Has("dried_guso") >= GusoHandIn;

    // The line in front of you: the point just ahead of your feet is close to its rope (from the sand beside it, or wading).
    int GusoLineInFront()
    {
        if (scene != "world" || Aboard || Riding) return -1;
        float fx = player.X + (player.Face == "left" ? -7 : player.Face == "right" ? 7 : 0);
        float fy = player.Y - 2 + (player.Face == "up" ? -7 : player.Face == "down" ? 7 : 0);
        for (int i = 0; i < GusoLines.Length; i++)
        {
            var (x, y0, y1) = GusoLines[i];
            if (Dist(fx, fy, x, Math.Clamp(fy, y0, y1)) < 6) return i;
        }
        return -1;
    }

    Target GusoTarget()
    {
        int i = GusoLineInFront();
        if (i < 0) return null;
        var l = GusoAt(i);
        Target Info(string label) => new() { Type = "info", Label = label };
        if (!state.Hinted("guso")) return Info("Maya's seaweed line. Ask Maya about it");
        if (!GusoLineOpen(i)) return Info("Maya opens the east lines once the co-op sells its first dried guso");
        if (l == null) return Has("guso") > 0 ? new Target { Type = "guso", Id = i.ToString(), Label = "Tie a guso cutting on the line" }
            : Info("An empty guso line. Maya has cuttings");
        if (GusoReady(l)) return new Target { Type = "guso", Id = i.ToString(), Label = "Harvest the guso" };
        int left = l.day + GusoDays - state.day;
        return Info($"The guso is growing. It's ready {(left <= 1 ? "tomorrow morning" : $"in {left} days")}");
    }

    void UseGusoLine(int i)
    {
        var (x, y0, y1) = GusoLines[i];
        string key = i.ToString();
        var l = GusoAt(i);
        if (l == null)
        {
            if (!GusoLineOpen(i) || !Take("guso")) return;
            state.guso[key] = new GusoLine { day = state.day, at = SinceDawn };
            Swing("hands", 0.25f);
            Sfx.Play("splash");
            Toast($"You tie a guso cutting on the line. It'll be ready on the morning of day {state.day + GusoDays}. Storms tear at it.", 4);
            mapTexDirty = true;
            Save();
            return;
        }
        if (!GusoReady(l)) return;
        bool torn = GusoTorn(l);
        int n = torn ? GusoTornYield : GusoYield;
        // One bunch goes straight back on the line as the next cutting.
        Give("guso", n - 1);
        state.guso[key] = new GusoLine { day = state.day, at = SinceDawn };
        state.hinted["gusoHarvest"] = true;
        heldItem = "guso"; heldT = 1.2f;
        Swing("haul", 0.3f);
        Burst(x, (y0 + y1) / 2, "#7d8a34", 8);
        Sfx.Play("pickup");
        Toast(torn ? $"A storm tore at this line: only {n} bunches left. You keep {n - 1} guso and tie one back on."
            : $"You cut {n} fat bunches of guso, keep {n - 1} and tie one back on to grow again. Dry them on a rack, or eat them fresh.", 4.5f);
        mapTexDirty = true;
        Save();
    }

    // A storm since the cutting went on tears half of it away. Each dawn marks the lines for the day that's ending
    // (TearGuso), and a harvest also checks today's weather so far.
    bool GusoTorn(GusoLine l) => l.torn || Stormy || StormBetween(state.forecast, l.day == state.day ? l.at : 0, SinceDawn);

    static bool StormBetween(List<WeatherSpell> plan, float from, float to)
    {
        if (plan == null) return false;
        for (int i = 0; i < plan.Count; i++)
        {
            if (plan[i].w != "storm") continue;
            float s0 = plan[i].at, s1 = i + 1 < plan.Count ? plan[i + 1].at : DayMin;
            if (s0 < to && s1 > from) return true;
        }
        return false;
    }

    // NewDay calls this before the day ticks over, while state.forecast is still the day that's ending.
    void TearGuso()
    {
        foreach (var l in state.guso.Values)
            if (!l.torn && StormBetween(state.forecast, l.day == state.day ? l.at : 0, DayMin)) l.torn = true;
    }

    /* ---------- Maya ---------- */
    void TalkMaya()
    {
        Say M(string t) => new("Maya", t);
        if (!state.Hinted("guso"))
        {
            state.hinted["guso"] = true;
            Give("guso", 2);
            mapTexDirty = true;
            Save();
            Talk(new()
            {
                M("These sheltered waters hold lapu-lapu and maya-maya. But I'm starting something that doesn't take a single fish out of the lagoon: a guso farm."),
                M("Guso is a seaweed. You tie a cutting to a line strung between two stakes, and the warm, calm water does the rest. By the second morning it's a fat bunch."),
                M("The two lines at the west end of the lagoon are yours to tend. Here are two cuttings: face a line from the sand and press <act> to tie one on."),
                M("When you harvest, one bunch goes back on the line to grow again. Dry the rest on a drying rack in the sun. Guso needs no salt, just a dry day."),
                M($"Storms tear it off the lines, so keep an eye on the forecast. Bring me {GusoHandIn} dried guso for the village co-op's first sale, and I'll open the east lines for you as well."),
                new("", "Maya gave you 2 guso cuttings.")
            });
            return;
        }
        if (CanGiveMayaGuso)
        {
            Take("dried_guso", GusoHandIn);
            state.coins += GusoCoopPay;
            state.hinted["gusoCoop"] = true;
            Sfx.Play("coin");
            mapTexDirty = true;
            Save();
            Talk(new()
            {
                M("Dried guso, pale and stiff! That's the co-op's first sale. The buyers make carrageenan from it, the stuff that sets jelly and thickens ice cream."),
                M($"Here's your share: {GusoCoopPay} coins. And the east lines are yours now too. Pip buys dried guso as well, and it rolls sushi like any seaweed."),
                new("", $"The east guso lines are open. (+{GusoCoopPay} coins)")
            });
            return;
        }
        var lines = new List<Say>();
        // Never stuck without a cutting: she tops you up for every empty line you could plant.
        int empty = Enumerable.Range(0, GusoLines.Length).Count(i => GusoLineOpen(i) && GusoAt(i) == null);
        if (empty > 0 && Has("guso") == 0)
        {
            Give("guso", empty);
            Save();
            lines.Add(M($"Out of cuttings? Here, {(empty == 1 ? "one more" : $"{empty} more")}. Tie them on before they dry out."));
        }
        int ready = Enumerable.Range(0, GusoLines.Length).Count(i => GusoReady(GusoAt(i)));
        if (ready > 0) lines.Add(M(ready == 1 ? "One of your lines is ready to harvest. Look how fat it's grown!" : $"{ready} of your lines are ready to harvest."));
        lines.Add(M(!state.Hinted("gusoCoop")
            ? state.Hinted("gusoHarvest") ? $"The co-op needs {GusoHandIn} dried guso for its first sale. Spread your harvest on a drying rack on a sunny day, then bring it here."
                : "Tie the cuttings on the west lines. They're ready on the second morning. Storms tear them, so watch the forecast."
            : "The farm's doing well. Guso grows best in clear, calm water, and the habagat's storms are hard on it."));
        lines.Add(M("Out by Baga, talakitok and tanigue put up a fierce fight. Keep food in your bag, and watch for their warning before an attack."));
        Talk(lines);
    }

    /* ---------- Drawing ---------- */
    // Stakes poking out of the lagoon, the rope just under the surface, and the guso clumps along it: little tufts when
    // they've just gone on, fat green-gold bunches when they're ready. A storm-torn line has gaps.
    void DrawGusoFarm(float t, bool live = true)
    {
        if (live && (MathF.Abs(player.X - GusoFarmX) > W || MathF.Abs(player.Y - GusoFarmY) > H)) return;
        for (int i = 0; i < GusoLines.Length; i++)
        {
            var (x, y0, y1) = GusoLines[i];
            int X = (int)x, Y0 = (int)y0, Y1 = (int)y1;
            pix.Rect(X, Y0, 1, Y1 - Y0, Pal.Rgba(232, 226, 196, 0.55f));
            foreach (int sy in new[] { Y0, Y1 })
            {
                pix.Rect(X, sy - 3, 1, 4, "#6b4a2b"); pix.Rect(X, sy - 3, 1, 1, "#b08458");
                pix.Rect(X - 1, sy + 1, 3, 1, Pal.Rgba(235, 248, 252, 0.35f));
            }
            var l = GusoAt(i);
            if (l == null) continue;
            int age = Math.Clamp(state.day - l.day, 0, GusoDays);
            bool torn = l.torn || live && GusoReady(l) && GusoTorn(l);
            // Bushy, knobbly clumps either side of the rope, overlapping into one ragged strip as they grow: olive
            // under the water, with pale golden tips breaking the surface.
            float r = age == 0 ? 0.9f : age == 1 ? 1.7f : 2.6f;
            for (int k = 0; k < 3; k++)
            {
                if (torn && age >= GusoDays && k == 1) continue;
                int cx = X + (age == 0 ? 0 : k % 2 == 0 ? -1 : 1), cy = Y0 + 2 + k * 3;
                int sway = live && age > 0 ? (int)MathF.Round(MathF.Sin(t * 1.3f + k + i * 2) * 0.55f) : 0;
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        float d = MathF.Sqrt(dx * dx + dy * dy * 1.4f);
                        if (d > r + 0.3f) continue;
                        double h = Pix.Hash(cx + dx + i * 7, cy + dy, 77);
                        if (d > r - 0.8f && h < 0.4) continue;
                        Color c = h > 0.82 && age > 0 ? GusoLeafTip : d < r - 1.1f ? GusoLeaf : GusoLeafDark;
                        pix.Rect(cx + dx + (dy < 0 ? sway : 0), cy + dy, 1, 1, c);
                    }
            }
            // Ready lines glint now and then, like a hauled-up pot's bubbles.
            if (live && GusoReady(l))
            {
                double ph = (t * 0.7 + i * 0.3) % 1;
                pix.Rect(X + 2, Y1 - 2 - (int)(ph * 6), 1, 1, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
            }
        }
    }

    // The chart's tooltip for the farm.
    List<string> GusoTip()
    {
        var tip = new List<string> { "Maya's guso farm" };
        if (!state.Hinted("guso")) { tip.Add("Ask Maya about her seaweed lines"); return tip; }
        var open = Enumerable.Range(0, GusoLines.Length).Where(GusoLineOpen).ToList();
        int ready = open.Count(i => GusoReady(GusoAt(i))), empty = open.Count(i => GusoAt(i) == null), growing = open.Count - ready - empty;
        var parts = new List<string>();
        if (ready > 0) parts.Add($"{ready} ready");
        if (growing > 0) parts.Add($"{growing} growing");
        if (empty > 0) parts.Add($"{empty} empty");
        tip.Add($"Your lines: {string.Join(", ", parts)}");
        if (!state.Hinted("gusoCoop")) tip.Add($"East lines open after the co-op's first sale ({GusoHandIn} dried guso)");
        return tip;
    }
}
