namespace Fesh;

// Asinan's salt beds and drying fish. The beds give salt once a day, as long as it hasn't rained since morning, so a dry
// forecast from Tomas or Pip is worth knowing. Salted fish laid out on a drying rack turn into daing (or, for the
// littlest fish, tuyo) after six hours of clear daylight; rain (or night) stops them drying until the sun comes back.
partial class Game
{
    const float DryNeeded = 360;          // minutes of clear daylight
    const int RackSlots = 3;
    const int GusoSlots = 6;              // seaweed is light: a rack takes six bunches of guso (Seaweed.cs)

    bool RakedToday => state.saltDay == state.day;
    // Rain since this morning (06:00), or now, melts the salt back into brine.
    bool RainedToday => state.weather != "clear" || state.forecast.Any(s => s.at <= SinceDawn && s.w != "clear");

    bool SaltBedsInReach() => scene == "world" && !Aboard
        && new Box(SaltBedX - 9, SaltBedY - 9, SaltBedW + 18, SaltBedH + 18).Overlaps(new Box(player.X - 3, player.Y - 3, 6, 3));

    void RakeSalt()
    {
        FaceToward(SaltBedX + SaltBedW / 2, SaltBedY + SaltBedH / 2);
        if (RakedToday) { Sfx.Play("nope"); Toast("You've raked the beds today. The sun needs another day to bring the salt up."); return; }
        if (RainedToday)
        {
            Sfx.Play("nope");
            Toast("The rain has melted the salt back into brine. Rake on a day that stays dry from the morning.", 4);
            return;
        }
        state.saltDay = state.day;
        int n = 3;
        Give("salt", n);
        Swing("rake", 0.3f);
        Burst(SaltBedX + SaltBedW / 2, SaltBedY + SaltBedH / 2, "#f8fafb", 10);
        Sfx.Play("pickup");
        if (!state.Hinted("bk:scale"))
        {
            state.hinted["bk:scale"] = true;
            Sfx.Play("odd");
            Talk(new()
            {
                new("You", $"Raked up {n} salt. And something else: the rake has caught on a round, flat scale as wide as your hand."),
                new("You", "It shines like the moon on the water. That came off something much bigger than any fish here."),
                new("Manang Rosa", "Another one? They turn up in the beds after a full moon. My mother called them Bakunawa's scales. Ask Lola Pacing on Daang Pulo about it."),
                new("", "New clue on your Case board (Habagat): A scale like a mirror.")
            });
            Save();
            return;
        }
        Toast($"You rake the beds and gather {n} salt ({Has("salt")} now). Salt your fish before you dry them.", 3);
        Save();
    }

    /* ---------- Drying racks ---------- */
    static string RackKey(Build b) => $"{b.x},{b.y}";
    RackLoad Rack(Build b) => state.racks.GetValueOrDefault(RackKey(b));
    float DryGoal => DryNeeded;
    // The "Salt and islets" aquarium set: fish dry twice as fast (while it's on show, for the hours that pass then).
    float DryRate => SetActive("habagat") ? 2 : 1;
    bool RackDone(RackLoad r) => r != null && r.fish.Count > 0 && r.dry >= DryGoal;
    // A rack holds salted fish or guso, never both.
    static bool RackIsGuso(RackLoad r) => r != null && r.fish.Count > 0 && r.fish[0] == "guso";
    static string RackWhat(RackLoad r) => RackIsGuso(r) ? "guso" : "fish";

    Target RackTarget(Build b)
    {
        var r = Rack(b);
        if (r == null || r.fish.Count == 0)
        {
            // <act> salts and lays out fish as before; with guso in your bag, <alt> spreads that instead (or <act>, if you
            // have no fish and salt to lay out).
            bool fish = Has("salt") > 0 && FishCount() > 0, guso = Has("guso") > 0;
            if (fish) return new Target { Type = "rack", Ref = b, Label = "Salt fish and lay them out to dry", AltType = guso ? "rackguso" : null, AltLabel = guso ? "Lay out guso to dry" : null };
            if (guso) return new Target { Type = "rackguso", Ref = b, Label = "Lay out guso to dry" };
            return new Target { Type = "rack", Ref = b, Label = "Drying rack (needs raw fish and salt, or guso)" };
        }
        if (RackDone(r)) return new Target { Type = "rack", Ref = b, Label = $"Take down the dried {RackWhat(r)} ({r.fish.Count})" };
        float left = (DryGoal - r.dry) / DryRate;
        return new Target
        {
            Type = "info", Ref = b, Label = $"{r.fish.Count} {RackWhat(r)} drying: {(int)left / 60}h {(int)left % 60:00}m of sunshine to go",
            AltType = "unrack", AltLabel = $"Take the {RackWhat(r)} back"
        };
    }

    // Guso needs no salt: up to six bunches, spread out in the sun.
    void LayGuso(Build b)
    {
        var r = Rack(b);
        if (r != null && r.fish.Count > 0) return;
        FaceToward(b.x * T + 5, b.y * T + 5);
        int n = Math.Min(GusoSlots, Has("guso"));
        if (n == 0) { Sfx.Play("nope"); Toast("You need fresh guso to dry. It grows on Maya's lines in the Luntian lagoon.", 3.5f); return; }
        Take("guso", n);
        state.racks[RackKey(b)] = new RackLoad { fish = Enumerable.Repeat("guso", n).ToList() };
        Swing("hands", 0.25f);
        Sfx.Play("build");
        Toast($"You spread {n} guso on the rack. It needs {(int)(DryGoal / DryRate) / 60} hours of clear daylight; rain stops it drying.", 4.5f);
        Save();
    }

    void UseRack(Build b)
    {
        string key = RackKey(b);
        var r = Rack(b);
        FaceToward(b.x * T + 5, b.y * T + 5);
        if (RackDone(r) && RackIsGuso(r))
        {
            state.racks.Remove(key);
            Give("dried_guso", r.fish.Count);
            heldItem = "dried_guso"; heldT = 1.2f;
            Sfx.Play("pickup");
            Toast($"The guso is dry, pale and stiff: {r.fish.Count} dried guso. Pip buys it, and it rolls sushi like any seaweed.", 3.5f);
            Save();
            return;
        }
        if (RackDone(r))
        {
            state.racks.Remove(key);
            // Split fish dry into daing; the littlest are dried whole, as tuyo.
            int tuyo = r.fish.Count(Items.TuyoFish.Contains), daing = r.fish.Count - tuyo;
            if (daing > 0) Give("daing", daing);
            if (tuyo > 0) Give("tuyo", tuyo);
            heldItem = daing > 0 ? "daing" : "tuyo"; heldT = 1.2f;
            Sfx.Play("pickup");
            string got = string.Join(" and ", new[] { daing > 0 ? Items.Amount("daing", daing) : null, tuyo > 0 ? Items.Amount("tuyo", tuyo) : null }.Where(t => t != null));
            Toast($"The fish are dry and golden: {got}. It keeps for a long voyage.", 3.5f);
            Save();
            return;
        }
        if (r != null && r.fish.Count > 0) return;
        int slots = Math.Min(RackSlots, Math.Min(Has("salt"), FishCount()));
        if (slots == 0)
        {
            Sfx.Play("nope");
            Toast(Has("salt") == 0 ? "You need salt to cure the fish first. Rake it from the salt beds on Asinan." : "You need raw fish to dry.", 3.5f);
            return;
        }
        var load = new RackLoad();
        for (int i = 0; i < slots; i++)
        {
            // The most plentiful ordinary fish go first, the same as cooking.
            var pick = state.inv.Where(kv => Items.ById[kv.Key].Kind == "fish" && !Data.FishById[kv.Key].Legend)
                .OrderBy(kv => Data.FishById[kv.Key].Rare ? 1 : 0).ThenByDescending(kv => kv.Value).First().Key;
            Take(pick); Take("salt");
            load.fish.Add(pick);
        }
        state.racks[key] = load;
        Swing("hands", 0.25f);
        Sfx.Play("build");
        Toast($"You salt {slots} fish and lay them out. They need {(int)(DryGoal / DryRate) / 60} hours of clear daylight; rain stops them drying.", 4.5f);
        Save();
    }

    void UnloadRack(Build b)
    {
        var r = Rack(b);
        if (r == null || r.fish.Count == 0) return;
        foreach (var f in r.fish) Give(f);
        state.racks.Remove(RackKey(b));
        Sfx.Play("pickup");
        Toast(RackIsGuso(r) ? $"You take {r.fish.Count} guso back off the rack." : $"You take {r.fish.Count} fish back off the rack. The salt is spent.", 2.6f);
        Save();
    }

    // Clock.Advance calls this for each stretch of time that passes (never across a dawn or a dusk): the racks dry for
    // the part of it that's daylight (06:00 to 18:00) with clear skies. A short step looks at the weather now; a long one
    // (resting) reads the day's forecast.
    void DryRacks(float from, float len)
    {
        if (state.racks.Count == 0 || len <= 0) return;
        float start = from, end = from + len;
        if (start < DawnMin || start >= DuskMin) return;   // a night stretch, which ends at dawn
        float a = start, b = Math.Min(end, 18 * 60);
        if (b <= a) return;
        float clear;
        if (len <= 5) clear = state.weather == "clear" ? b - a : 0;
        else
        {
            clear = 0;
            float s0 = a - DawnMin, s1 = b - DawnMin;
            for (int i = 0; i < state.forecast.Count; i++)
            {
                if (state.forecast[i].w != "clear") continue;
                float f0 = state.forecast[i].at, f1 = i + 1 < state.forecast.Count ? state.forecast[i + 1].at : DayMin;
                clear += Math.Max(0, Math.Min(s1, f1) - Math.Max(s0, f0));
            }
        }
        if (clear <= 0) return;
        foreach (var r in state.racks.Values)
            if (r.fish.Count > 0) r.dry += clear * DryRate;
    }
}
