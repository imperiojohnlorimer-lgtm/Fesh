using Raylib_cs;

namespace Fesh;

// Beneath the clouds (1.21), story 4, on Baga Island. Manay Mila grows abaca on Baga's north shore, and her lola in Albay
// told her a version of the Magayon story: how Mayon rose from the lovers' grave, and why clouds wrap its summit. Ben
// runs Baga's (made-up) volcano station, which borrows the alert levels PHIVOLCS uses for Mayon. Baga wakes slowly: you
// help read the signs (quakes, the ground swelling, sulfur dioxide, the summit camera), pack a go-bag while it's calm,
// leave early when the disaster office orders everyone off, get the school ready as the shelter before the ash comes,
// and when rain falls on the fresh ash, help close the channel and radio the lahar warning. Nobody fights anything and
// nothing hurts you: you're stopped before you step anywhere closed, and helpers finish anything you skip (without
// credit). Codex designed it alongside Claude and fact-checked it (2026-10-10): the alert names are Mayon's, the
// readings must be read right before anyone praises them, reopening comes from an advisory and not from finishing the
// story, and the lahar can be watched live or as recorded footage, so the finale never waits on catching the rain.
sealed class Readings
{
    public int Round, Step;            // Step 0 seismograph, 1 ground (GPS), 2 gas, 3 summit camera, 4 all done
    public readonly HashSet<int> Marked = new();
    public bool Counted, Swept, Ok;    // the quakes all marked; the gas swept; the step answered right
    public float Sweep;                // 0..1, the gas cursor
    public string Note = "";           // what Ben says about your answer
    public readonly HashSet<string> Picks = new();
}

sealed class GoBag { public readonly HashSet<int> In = new(); public string Note = ""; public bool Done; }

sealed class LaharWatch
{
    public bool Live;                  // raining now, or recorded footage of rain since the shelter
    public readonly HashSet<string> Closed = new();
    public bool Sent, ByBen, Counted;
    public float T = -1;               // seconds the camera has shown the flow (-1: not yet)
    public float Open;                 // seconds the panel has been open (Ben sends the warning himself after a while)
    public string Note = "";
}

partial class Game
{
    /* ---------- Where things are ---------- */
    // Baga Island, world pixels. The cone stands south of the north shore's houses; the fictional permanent danger zone is
    // an ellipse round it, marked with stones.
    const float ConeX = 2330, ConeBaseY = 500, ConeH = 70, ConeHalf = 64;
    const float PdzX = 2330, PdzY = 478, PdzRX = 90, PdzRY = 58;
    const float MilaX = 2270, MilaY = 392, BenX = 2350, BenY = 398;
    const float AbacaX = 2226, AbacaY = 400, PiliX = 2252, PiliY = 416;
    const float AlertBoardX = 2332, AlertBoardY = 406, CrateX = 2374, CrateY = 404, EvacTableX = 2296, EvacTableY = 412;
    const float EvacBoatX = 2330, EvacBoatY = 327, BoardPointX = 2315, BoardPointY = 338;
    const float LaharMouthX = 2398, LaharMouthY = 384;
    // The channel down the cone's north-east flank to the beach, from behind the cone to its mouth.
    static readonly (float x, float y)[] LaharChannel = { (2344, 440), (2362, 427), (2378, 411), (2390, 397), (2398, 384) };
    // Amihan Village: the school is the shelter the disaster office checked for this.
    const float SchoolDoorX = 1665.5f, SchoolDoorY = 132;
    static readonly (float x, float y)[] JarSpots = { (1615, 139), (1627, 139) };
    const float BarrelX = 1690, BarrelY = 134;
    const float LandingX = 1540, LandingY = 216;
    // Inside the school (room pixels): the shutters, the water crate, the families' mats and the desk.
    const int SchoolW = 24, SchoolH = 13;
    static readonly (float x, float y)[] ShutterSpots = { (35, 28), (205, 28) };
    static readonly (float x, float y)[] FamilyMats = { (60, 58), (160, 58), (190, 80) };
    const float WaterCrateX = 196, WaterCrateY = 104, DeskX = 40, DeskY = 104, DisplayX = 120, DisplayY = 30;

    // Mayon's alert levels (PHIVOLCS, revised 14 January 2018): the names exactly, the meanings in plain words.
    public static readonly (string Name, string Means)[] AlertLevels =
    {
        ("No Alert", "Quiet: background activity, no eruption expected. Sudden steam blasts can still happen, so the permanent danger zone stays closed."),
        ("Abnormal", "Slight unrest. No eruption expected soon. Stay out of the permanent danger zone."),
        ("Increasing Unrest", "Probably magma moving underground. An eruption could follow. Danger zones may be widened."),
        ("Increased Tendency Towards Hazardous Eruption", "Magma near or at the crater. Worsening signs can lead to a hazardous eruption within weeks."),
        ("Hazardous Eruption Imminent", "Intense unrest. A hazardous eruption is possible within days. Wider danger zones."),
        ("Hazardous Eruption", "A hazardous eruption is happening: pyroclastic flows, tall ash columns and ash over a wide area.")
    };

    /* ---------- State ---------- */
    float mgEvacT, mgPrepT, mgAshT, mgOutsideT, mgTurnedT, mgUsonT = 8, mgUsonAt = -1;
    int mgWater;                       // sealed water you're carrying to the families
    Mover mgMilaWalk;                  // Manay Mila on her way to the table by the jetty
    Readings readings;
    GoBag gobag;
    LaharWatch laharWatch;
    readonly List<(float x, float y, float vx, float life)> ashFlakes = new();

    bool MgMet => state.Hinted("mg:metMila");
    bool MgStory => state.Hinted("mg:story");
    bool MgOrder => state.Hinted("mg:order");
    bool MgEvacuated => state.Hinted("mg:evacuated");
    bool MgSheltered => state.Hinted("mg:sheltered");
    bool MgDone => state.Hinted("mg:done");
    int BagaLevel => state.bagaAlert;
    int MgClueCount => Data.MagayonClues.Count(c => state.Hinted(c.Key));
    bool MgStarted => MgMet;
    // Controlled stretches: leaving Baga (on Baga), and getting the shelter ready until the ash has passed.
    bool BagaEvacuating => MgOrder && !MgEvacuated;
    bool AshPhase => MgEvacuated && !MgSheltered;
    bool VolcanoControlled => BagaEvacuating || AshPhase;
    bool AshFalling => AshPhase && state.Hinted("mg:ashStarted");
    // The advisory (not finishing the story) reopens the north shore after Baga quiets down; the zone and channel stay shut.
    int ReopenDay => state.gifts.GetValueOrDefault("mg_order") + 4;
    bool BagaClosed => MgEvacuated && state.day < ReopenDay;
    // Mila and Ben stay at the shelter until the shore's open again and the lahar watch is done.
    bool BagaFolkAtSchool => MgEvacuated && !(MgDone && state.day >= ReopenDay);
    bool PreAshDone => state.Hinted("mg:preHelped") || state.Hinted("mg:jar0") && state.Hinted("mg:jar1") && state.Hinted("mg:barrel") && state.Hinted("mg:shut0") && state.Hinted("mg:shut1");
    bool InsideDone => state.Hinted("mg:inHelped") || state.Hinted("mg:water0") && state.Hinted("mg:water1") && state.Hinted("mg:water2") && state.Hinted("mg:desk");
    bool MgRained => state.Hinted("mg:rained") || MgSheltered && RainSinceShelter();
    bool LaharWatchDue => MgSheltered && !MgDone;
    bool GiftDue => MgDone && !state.Hinted("mg:gift") && state.day > state.gifts.GetValueOrDefault("mg_done");
    bool InSchoolRoom => scene == "house:school";

    bool OnBagaIsland => scene == "world" && !Aboard && !Riding && RegionOf(player.X, player.Y) == "amihan:Baga Island" && Walkable(TileUnder(player.X, player.Y));
    // Quiet enough for Baga's next step: play, by day, no storm, nothing else going on.
    bool BagaQuiet => mode == "play" && boss == null && guardian == null && eclipse == null && race == null && !raceArmed && tremor == null && evac == null
        && !Stormy && state.clock is >= 7 * 60 and < 17 * 60 && fish == null && landed == null;

    /* ---------- Every frame (ActiveModes) ---------- */
    void UpdateMagayon(float dt)
    {
        mgTurnedT = Math.Max(0, mgTurnedT - dt);
        UpdateBagaLevel();
        if (mode == "play") CheckBagaUnrest();
        // Rain on Baga after the shelter: it's recorded, so the lahar watch never needs you there at that moment.
        if (MgSheltered && !state.Hinted("mg:rained") && state.weather != "clear") { state.hinted["mg:rained"] = true; RadioRain(); }
        else if (MgSheltered && !MgDone && state.weather != "clear" && state.gifts.GetValueOrDefault("mg_rainCall", -1) != state.day) RadioRain();
        if (mgMilaWalk != null) StepMover(mgMilaWalk, dt);
        UpdateUson(dt);
        if (mode != "play") return;
        if (BagaEvacuating && OnBaga())
        {
            mgEvacT += dt;
            // Nobody has to finish anything before leaving: after a while, Ben and Niko see everyone aboard.
            if (mgEvacT > 150) { Toast("Ben: \"Time to go. I've got your things; come on.\"", 4); BoardEvacBoat(assisted: true); }
        }
        if (AshPhase) UpdateAshPhase(dt);
    }

    bool OnBaga() => scene == "world" && RegionOf(player.X, player.Y) == "amihan:Baga Island";

    // The alert board follows Baga's story, then the advisory lowers it as Baga quiets (calendar days, not the story).
    void UpdateBagaLevel()
    {
        int want = !state.Hinted("mg:unrest") ? 0 : !state.Hinted("mg:read1") ? 1 : !state.Hinted("mg:read2") ? 2
            : state.day >= state.gifts.GetValueOrDefault("mg_order") + 9 ? 1 : state.day >= ReopenDay ? 2 : 3;
        if (want == state.bagaAlert) return;
        bool lowered = want < state.bagaAlert && MgOrder;
        state.bagaAlert = want;
        mapTexDirty = true;
        if (lowered && scene != "cave")
            ToastLater(want == 2 ? "Ben (on the radio): \"The team says Baga has quieted: Level 2. The advisory reopens the north shore and the reef. The danger zone and the channel stay closed.\""
                : "Ben (on the radio): \"Baga's down to Level 1. Still nobody past the marker stones.\"");
    }

    void CheckBagaUnrest()
    {
        if (!MgStory || !state.Hinted("mg:metBen")) return;
        if (!state.gifts.ContainsKey("mg_ready")) state.gifts["mg_ready"] = state.day;
        if (state.Hinted("mg:unrest") || state.day <= state.gifts["mg_ready"] || !BagaQuiet || !OnBagaIsland) return;
        state.hinted["mg:unrest"] = true;
        state.gifts["mg_unrest"] = state.day;
        UpdateBagaLevel();
        quake = Math.Max(quake, 0.12f);
        Sfx.Play("thunder");
        Save();
        Toast("A small shake underfoot, and more steam from the crater. Ben waves from his station: Baga's at Level 1.", 6);
    }

    void RadioRain()
    {
        state.gifts["mg_rainCall"] = state.day;
        if (scene == "cave" || !(InAmihan || InSchoolRoom)) return;
        Toast(InSchoolRoom ? "Ben: \"Rain on Baga! Come to the display: we need to warn about lahars.\""
            : "Ben (on the radio): \"Rain on Baga! Come to the school's display: we need to warn about lahars.\"", 6);
    }

    // Minutes since day 0's dawn: one clock for "since the shelter" that doesn't trip over the small hours (a night belongs
    // to the day before it, so 02:00 is late in a day, not early; Codex: comparing clock minutes counted that morning's rain).
    int AbsNow => state.day * (int)DayMin + (int)SinceDawn;
    int ShelterAt => state.gifts.GetValueOrDefault("mg_shelterAt");

    // Rain since the shelter, today (earlier days are kept by NewDay in mg:rained).
    bool RainSinceShelter()
    {
        if (state.weather != "clear") return true;
        int day0 = state.day * (int)DayMin;
        return state.forecast?.Any(s => s.w != "clear" && s.at <= SinceDawn && day0 + Math.Min(NextSpellAt(s), SinceDawn) > ShelterAt) == true;
    }

    float NextSpellAt(WeatherSpell s)
    {
        int i = state.forecast.IndexOf(s);
        return i + 1 < state.forecast.Count ? state.forecast[i + 1].at : DayMin;
    }

    // Called by NewDay before the day changes: rain later in the ending day still counts for the lahar watch.
    void NoteBagaRain()
    {
        if (!MgSheltered || state.Hinted("mg:rained") || state.forecast == null) return;
        int day0 = state.day * (int)DayMin;
        if (state.forecast.Any(s => s.w != "clear" && day0 + NextSpellAt(s) > ShelterAt)) state.hinted["mg:rained"] = true;
    }

    /* ---------- Where you can't go ---------- */
    bool InPdz(float x, float y) { float dx = (x - PdzX) / PdzRX, dy = (y - PdzY) / PdzRY; return dx * dx + dy * dy < 1; }

    float ChannelDist(float x, float y)
    {
        float best = float.MaxValue;
        for (int i = 1; i < LaharChannel.Length; i++)
        {
            var (ax, ay) = LaharChannel[i - 1]; var (bx, by) = LaharChannel[i];
            float vx = bx - ax, vy = by - ay, t = Math.Clamp(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy), 0, 1);
            best = Math.Min(best, Dist(x, y, ax + vx * t, ay + vy * t));
        }
        return best;
    }

    // The channel and the fan where it meets the sea, closed from the order on (lahars can come long after an eruption).
    bool InChannelZone(float x, float y) => MgOrder && !InPdz(x, y) && (ChannelDist(x, y) < 8 || Dist(x, y, LaharMouthX, LaharMouthY) < 15);

    bool[,] bagaClosedTiles;
    // Baga's land, and the water within six tiles of it (not the sanctuary): closed while the evacuation order stands.
    bool BagaClosedTile(int tx, int ty)
    {
        if (bagaClosedTiles == null)
        {
            var land = new List<(int x, int y)>();
            for (int y = 29; y <= 62; y++)
                for (int x = 215; x <= 246; x++)
                    if (!(worldMap[y, x] is '~' or 'w')) land.Add((x, y));
            bagaClosedTiles = new bool[ROWS, COLS];
            for (int y = 20; y < Math.Min(ROWS, 70); y++)
                for (int x = 205; x < Math.Min(COLS, 256); x++)
                {
                    if (InSanctuary(x * T + 5, y * T + 5)) continue;
                    bagaClosedTiles[y, x] = land.Any(l => (l.x - x) * (l.x - x) + (l.y - y) * (l.y - y) <= 36);
                }
        }
        return tx >= 0 && ty >= 0 && tx < COLS && ty < ROWS && bagaClosedTiles[ty, tx];
    }

    // Why you can't step to (x, y) right now, or null. Only for you (on foot, riding or at the helm), outdoors.
    string Restriction(float x, float y)
    {
        if (scene != "world") return null;
        int tx = (int)MathF.Floor(x / T), ty = (int)MathF.Floor((y - 1.5f) / T);
        // Leaving Baga goes by Niko's banca, and nobody wades off the village island while the ash comes (both anywhere).
        if (BagaEvacuating && !Walkable(TileAt(tx, ty))) return "Niko's banca is waiting at the jetty.";
        if (AshPhase && !Walkable(TileAt(tx, ty))) return "Stay with the shelter: the ash is coming.";
        if (x < 1900) return null;
        if (InPdz(x, y)) return PdzWords;
        if (InChannelZone(x, y)) return "That's the lahar channel and its mouth. They stay closed: lahars can come long after an eruption.";
        if (BagaClosed && BagaClosedTile(tx, ty)) return "Baga is closed: the evacuation order still stands. Ben will say on the radio when it reopens.";
        return null;
    }

    const string PdzWords = "The marker stones ring the permanent danger zone round the crater. Nobody goes past them, at any alert level.";

    // How deep into a closed place a point is (bigger is deeper), for walking out of one you're already in.
    float ClosedDepth(string why, float x, float y)
    {
        if (why == PdzWords) { float dx = (x - PdzX) / PdzRX, dy = (y - PdzY) / PdzRY; return 1 - dx * dx - dy * dy; }
        if (why.StartsWith("That's the lahar")) return 8 - Math.Min(ChannelDist(x, y), Dist(x, y, LaharMouthX, LaharMouthY) - 7);
        if (why.StartsWith("Baga is closed")) return -Dist(x, y, 2310, 460);
        // In the water while leaving or sheltering (an old save): further from dry land is deeper (Codex).
        return WaterDepth(x, y);
    }

    float WaterDepth(float x, float y)
    {
        int tx = (int)MathF.Floor(x / T), ty = (int)MathF.Floor((y - 1.5f) / T);
        float best = 99;
        for (int oy = -6; oy <= 6; oy++)
            for (int ox = -6; ox <= 6; ox++)
                if (Walkable(TileAt(tx + ox, ty + oy))) best = Math.Min(best, Dist(x, y, (tx + ox) * T + 5, (ty + oy) * T + 6));
        return best;
    }

    // A step into somewhere closed is refused (with a word why). Already inside one (an old save, a restriction that began
    // around you), you can walk out of it, but not further in (Codex: a blanket exemption let you go anywhere from inside).
    bool StepAllowed(float x, float y)
    {
        if (Restriction(x, y) is not string why) return true;
        if (Restriction(player.X, player.Y) is string here && here == why && ClosedDepth(why, x, y) <= ClosedDepth(why, player.X, player.Y) + 0.0001f) return true;
        if (mgTurnedT <= 0) { Toast(why, 4); Sfx.Play("nope"); mgTurnedT = 3; }
        if (why == PdzWords) state.hinted["mg:r:turned"] = true;
        return false;
    }

    /* ---------- Targets ---------- */
    // Outdoors on Baga and at the school (before the islanders' usual lines, after the story-3 events).
    Target MagayonTarget()
    {
        if (scene != "world" || Aboard) return null;
        float x = player.X, y = player.Y;
        if (VolcanoControlled) return MagayonEventTarget();
        // The school is the shelter while the folk from Baga are there.
        if (BagaFolkAtSchool && Dist(x, y, SchoolDoorX, SchoolDoorY + 4) < 9)
            return new Target { Type = "schooldoor", Label = "Go into the school (the evacuation centre)" };
        if (!OnBaga()) return null;
        if (Dist(x, y, AlertBoardX, AlertBoardY + 4) < 10) return new Target { Type = "alertboard", Label = $"Read the alert board (Level {BagaLevel})" };
        if (MgMet && !state.Hinted("mg:abaca") && Dist(x, y, AbacaX, AbacaY + 2) < 13) return new Target { Type = "abaca", Label = "Tie the abaca fibre on the drying frame" };
        if (MgMet && !state.Hinted("mg:pili") && Dist(x, y, PiliX, PiliY + 2) < 11) return new Target { Type = "pili", Label = "Plant the pili seedling" };
        if (state.Hinted("mg:read1") && Dist(x, y, CrateX, CrateY + 3) < 11)
            return new Target { Type = "gobag", Label = state.Hinted("mg:bag") ? "Your go-bag is packed" : "Pack a go-bag at the supply crate" };
        if (MgOrder && InChannelZone(x, y + 6)) return new Target { Type = "info", Label = "The lahar channel: closed" };
        return null;
    }

    // During the evacuation and the ash, only what to do next.
    Target MagayonEventTarget()
    {
        float x = player.X, y = player.Y;
        if (BagaEvacuating)
        {
            if (!OnBaga()) return new Target { Type = "info", Label = "To the jetty on Baga" };
            if (!state.Hinted("mg:called") && mgMilaWalk == null && Dist(x, y, MilaX, MilaY + 4) < 22) return new Target { Type = "callmila", Label = "Call to Manay Mila: \"The order's out! To the jetty!\"" };
            if (!state.Hinted("mg:bag") && !state.Hinted("mg:bagGiven") && Dist(x, y, CrateX, CrateY + 3) < 11) return new Target { Type = "gobag", Label = "Pack a go-bag at the supply crate" };
            if (!state.Hinted("mg:registered") && Dist(x, y, EvacTableX, EvacTableY + 4) < 12) return new Target { Type = "register", Label = "Sign the evacuation list" };
            if (Dist(x, y, BoardPointX, BoardPointY) < 14) return new Target { Type = "boardevac", Label = "Board Niko's banca to the village" };
            if (Dist(x, y, BenX, BenY + 4) < 15) return new Target { Type = "ben", Label = "Ask Ben what to do" };
            return new Target { Type = "info", Label = EvacNext() };
        }
        // The ash: the jobs outside first, then inside.
        if (AshFalling) return Dist(x, y, SchoolDoorX, SchoolDoorY + 4) < 9 ? new Target { Type = "schooldoor", Label = "Go into the school, out of the ash" }
            : new Target { Type = "info", Label = "Ash is falling: get inside the school" };
        for (int i = 0; i < JarSpots.Length; i++)
            if (!JarCovered(i) && Dist(x, y, JarSpots[i].x, JarSpots[i].y + 3) < 9) return new Target { Type = "jar", Tx = i, Label = "Cover the water jar" };
        if (!BarrelOff && Dist(x, y, BarrelX, BarrelY + 3) < 10) return new Target { Type = "barrel", Label = "Take the roof's downpipe off the rain barrel" };
        if (Dist(x, y, SchoolDoorX, SchoolDoorY + 4) < 9) return new Target { Type = "schooldoor", Label = "Go into the school (the shelter)" };
        return new Target { Type = "info", Label = PreAshNext() };
    }

    string EvacNext() =>
        !state.Hinted("mg:bag") && !state.Hinted("mg:bagGiven") ? "Grab a go-bag at Ben's supply crate, call Manay Mila, then to the jetty"
        : !state.Hinted("mg:called") && mgMilaWalk == null ? "Call to Manay Mila in her garden, then to the jetty"
        : !state.Hinted("mg:registered") ? "Sign the list at the table by the jetty, then board"
        : "Board Niko's banca at the end of the jetty";

    string PreAshNext() =>
        !JarCovered(0) || !JarCovered(1) ? "Cover the water jars by the school before the ash comes"
        : !BarrelOff ? "Take the downpipe off the school's rain barrel"
        : "Close the school's shutters: go inside";

    bool JarCovered(int i) => state.Hinted($"mg:jar{i}") || state.Hinted("mg:preHelped") || MgSheltered;
    bool BarrelOff => state.Hinted("mg:barrel") || state.Hinted("mg:preHelped") || MgSheltered;
    bool ShutterClosed(int i) => state.Hinted($"mg:shut{i}") || state.Hinted("mg:preHelped") || AshFalling;
    bool FamilyHasWater(int i) => state.Hinted($"mg:water{i}") || state.Hinted("mg:inHelped") || MgSheltered;

    /* ---------- The people ---------- */
    string MilaLabel() =>
        GiftDue ? "Manay Mila has something for you"
        : !MgMet ? "Talk to the woman in the abaca garden"
        : MgMet && !MgStory && state.Hinted("mg:abaca") && state.Hinted("mg:pili") ? "Tell Manay Mila it's done"
        : "Talk to Manay Mila";

    string BenLabel() =>
        !state.Hinted("mg:metBen") ? "Talk to the man at the station"
        : ReadingsDue ? "Help Ben with today's readings"
        : "Talk to Ben";

    bool ReadingsDue => state.Hinted("mg:unrest") && (!state.Hinted("mg:read1")
        || !state.Hinted("mg:read2") && state.day > state.gifts.GetValueOrDefault("mg_read1"));

    void TalkMila()
    {
        Say M(string t) => new("Manay Mila", t);
        FaceToward(IslanderWalk("mila").X, IslanderWalk("mila").Y);
        if (GiftDue)
        {
            state.hinted["mg:gift"] = true;
            Give("abaca_line");
            Save();
            Talk(new()
            {
                M("For you: a fishing line I twisted from my own abaca. Abaca fibre has made ropes and fishing lines for a long time; it's strong, and it grows well in Bicol."),
                M("Dios mabalos, for helping us leave early and keep everyone safe. That's thank you, where my lola came from."),
                new("", "You got an Abaca line (tackle). Fit it in the tackle box.")
            });
            return;
        }
        if (!MgMet)
        {
            state.hinted["mg:metMila"] = true;
            Save();
            Talk(new()
            {
                M("Hello! I'm Mila; everyone calls me Manay Mila. Manay means older sister, where my lola came from in Bicol."),
                M("I grow abaca, and pili trees. The soil here is rich: old volcanic rock and ash break down into good soil, though fresh ash can hurt crops."),
                M("In Albay, where my lola lived, people farm right up the slopes of Mayon. Help me? Tie that bundle of abaca fibre on the drying frame, and plant this pili seedling by the path.")
            });
            return;
        }
        if (!MgStory)
        {
            if (!(state.Hinted("mg:abaca") && state.Hinted("mg:pili")))
            {
                Talk(new() { M(!state.Hinted("mg:abaca") && !state.Hinted("mg:pili") ? "The abaca's on the frame west of my house, and the pili seedling goes by the path."
                    : !state.Hinted("mg:abaca") ? "Just the abaca left: tie it on the frame west of my house." : "Just the pili seedling left, by the path.") });
                return;
            }
            state.hinted["mg:story"] = true;
            Save();
            Talk(new()
            {
                M("Salamat! Now sit a moment. My lola in Albay told me a version of the Magayon story. This is how she told it."),
                M("Magayon, the daughter of Makusog, a chief of Rawis, was the most beautiful girl anyone had seen. Daragang Magayon: the beautiful maiden."),
                M("One day she slipped into the river, and Panganoron, a young man from another land, jumped in and saved her. They fell in love."),
                M("But Pagtuga, a proud chief who wanted to marry her, took her father prisoner to force a wedding. There was fighting, and in it, Panganoron and Magayon both died."),
                M("Their people buried them together, and the mound grew and grew into Mayon, the most perfect cone in the world. When clouds wrap its summit, my lola said, that's the two of them together."),
                M("Panganoron means cloud. And Pagtuga? It means eruption. Nobody knows exactly how old the story is; one telling was printed by Merito Espinas in 1968."),
                M("Ben at the station will tell you what really goes on inside a volcano. He's the one who watches Baga."),
                new("", "New clue on your Case board (Baga): Magayon's story.")
            });
            return;
        }
        if (MgDone)
        {
            Talk(new() { M(state.day >= ReopenDay ? "Back home, and the abaca came through. Fresh ash is hard on crops, but the soil will be rich again in time."
                : "We'll go home when the advisory says so. Until then, this shelter's home.") });
            return;
        }
        if (BagaFolkAtSchool)
        {
            Talk(new() { M("Thank you for calling me at the jetty. My lola always said: when the mountain is restless, leave early, and come back when they say.") });
            return;
        }
        Talk(new() { M(BagaLevel >= 2 ? "Ben says Baga's restless. My go-bag's by the door: water, food, medicines, a flashlight, a radio and our papers." : "The abaca's drying nicely. Look how the clouds sit on Baga today.") });
    }

    void TalkBen()
    {
        Say B(string t) => new("Ben", t);
        var s = IslanderWalk("ben");
        FaceToward(s.X, s.Y);
        if (!state.Hinted("mg:metBen"))
        {
            state.hinted["mg:metBen"] = true;
            Save();
            Talk(new()
            {
                B("Hi! I'm Ben. I run Baga's volcano station: seismometers that feel the ground, GPS that measures how it moves, a scanner for the gas, and a camera on the crater."),
                B("In the real Philippines, PHIVOLCS watches volcanoes like Mayon and Taal and sets their alert levels. Baga and this station are made up, but we use the same levels as Mayon."),
                B("Baga's at Level 0, No Alert. That doesn't mean safe: nobody goes past the marker stones round the crater, at any level. That's the permanent danger zone."),
                B("Here's the whole scale. It's on the board outside too.")
            }, () => OpenAlertCard());
            return;
        }
        if (ReadingsDue)
        {
            if (Stormy) { Talk(new() { B("Not in this storm. We'll read them when it passes.") }); return; }
            int round = state.Hinted("mg:read1") ? 2 : 1;
            // Whatever round 2 says, any order to leave should come by daylight, with the sea calm.
            if (round == 2 && state.clock is < 7 * 60 or >= 17 * 60) { Talk(new() { B("It's late. We'll do the readings in the morning, in daylight.") }); return; }
            Talk(new()
            {
                B(round == 1 ? "Baga's restless: small quakes since this morning, and more steam. Help me with today's readings? These are simplified examples of what the station measures."
                    : "New readings. Let's see what Baga's doing today."),
            }, () => OpenReadings(round));
            return;
        }
        if (BagaEvacuating) { Talk(new() { B(EvacNext() + ". Nothing has to be finished before we leave: if it's time, we go.") }); return; }
        if (state.Hinted("mg:read1") && !state.Hinted("mg:read2"))
        {
            Talk(new() { B(state.Hinted("mg:bag") ? "Your go-bag's packed. Good: when an order comes, there's no time to think about it. New readings tomorrow."
                : "Pack a go-bag now, while it's calm: the supply crate by my door has the things. New readings tomorrow.") });
            return;
        }
        if (LaharWatchDue && InSchoolRoom)
        {
            Talk(new() { B(MgRained ? "There's been rain on Baga. Let's look at the channel on the display." : "We wait for rain. Rain on fresh ash can turn it into a lahar, a fast flow of mud and rock. When it rains, come to the display.") });
            return;
        }
        if (MgDone)
        {
            Talk(new() { B(BagaLevel >= 2 ? $"Baga's at Level {BagaLevel}. The advisory says when places reopen; finishing a job doesn't make the volcano quiet." : "Level 1 now. The marker stones stay where they are.") });
            return;
        }
        Talk(new() { B(BagaLevel == 0 ? "All quiet: a little steam, the odd tiny quake. Every volcano has its own normal, and that's Baga's." : $"Baga's at Level {BagaLevel}. The board outside has the whole scale.") });
    }

    /* ---------- Hands-on bits ---------- */
    void TieAbaca()
    {
        FaceToward(AbacaX, AbacaY);
        Swing("hands", 0.35f);
        Sfx.Play("build");
        state.hinted["mg:abaca"] = true;
        Save();
        Floater("Tied!", AbacaX, AbacaY - 18, "#e8d8a8");
        Toast(state.Hinted("mg:pili") ? "The abaca's drying. Tell Manay Mila." : "The abaca fibre hangs to dry in the sun: it'll be twisted into rope. Now the pili seedling.", 4);
    }

    void PlantPili()
    {
        FaceToward(PiliX, PiliY);
        Swing("dig", 0.4f);
        Sfx.Play("build");
        state.hinted["mg:pili"] = true;
        Save();
        Floater("Planted!", PiliX, PiliY - 14, "#9fd36b");
        Toast(state.Hinted("mg:abaca") ? "A pili tree for Manay Mila's garden. Tell her it's done." : "A pili tree, Bicol's nut tree, takes years to fruit. Now the abaca.", 4);
    }

    void CallMila()
    {
        state.hinted["mg:called"] = true;
        var s = IslanderWalk("mila");
        mgMilaWalk = new Mover { Id = "mila", Name = "Manay Mila", X = s.X, Y = s.Y, Speed = 38, Going = true, Path = new[] { (MilaX + 14, MilaY + 18f), (EvacTableX - 8, EvacTableY + 5) } };
        Sfx.Play("whistle");
        Floater("The order's out! To the jetty!", player.X, player.Y - 26, "#ffd76a");
        Floater("Coming!", s.X, s.Y - 18, "#7fd36b");
        Save();
    }

    void SignEvacList()
    {
        state.hinted["mg:registered"] = true;
        Swing("hands", 0.3f);
        Sfx.Play("ui");
        Save();
        Toast(state.Hinted("mg:called") ? "You sign the list: you, Manay Mila and Ben. The disaster office will know everyone from Baga is accounted for."
            : "You sign the list: you and Ben. Manay Mila still needs calling.", 5);
    }

    // Everyone aboard Niko's banca, then a short crossing to the village landing. Whatever you didn't do, Ben did.
    void BoardEvacBoat(bool assisted)
    {
        if (!BagaEvacuating) return;
        if (!state.Hinted("mg:bag")) state.hinted["mg:bagGiven"] = true;
        if (!state.Hinted("mg:called")) state.hinted["mg:calledHelp"] = true;
        if (!state.Hinted("mg:registered")) state.hinted["mg:registeredHelp"] = true;
        if (assisted) state.hinted["mg:assisted"] = true;
        Sfx.Play("splash");
        FadeThrough(() =>
        {
            state.hinted["mg:evacuated"] = true;
            state.gifts["mg_evac"] = state.day;
            mgMilaWalk = null;
            state.riding = false;
            state.aboard = false;
            // Your banca comes along behind, and Tidemane swims over to wait by the landing.
            if (Has("boat") > 0) { state.boatX = 1483; state.boatY = 217; state.boatAt = "saltmere"; }
            if (state.tamed) { state.mountX = LandingX + 12; state.mountY = LandingY + 6; }
            player.X = LandingX; player.Y = LandingY; player.Face = "right";
            mgPrepT = 0;
            Save();
        }, () => Talk(new()
        {
            new("Niko", "Everyone out, mind the step. Welcome to the village."),
            new("Ma'am Isay", "The disaster office has checked the school: it's the shelter for this, outside every danger zone. Ben says the winds higher in the atmosphere are blowing this way, and Baga may send up ash."),
            new("Ma'am Isay", "Before it comes: cover the two water jars, take the roof's downpipe off the rain barrel, and close the school's shutters. Then everyone inside.")
        }));
    }

    void CoverJar(int i)
    {
        FaceToward(JarSpots[i].x, JarSpots[i].y);
        Swing("hands", 0.3f);
        Sfx.Play("build");
        state.hinted[$"mg:jar{i}"] = true;
        Save();
        Floater("Covered", JarSpots[i].x, JarSpots[i].y - 12, "#d8e8f0");
    }

    void UnhookBarrel()
    {
        FaceToward(BarrelX, BarrelY);
        Swing("hands", 0.35f);
        Sfx.Play("build");
        state.hinted["mg:barrel"] = true;
        Save();
        Toast("Unhooked: ash on the roof won't wash into the drinking water now.", 4);
    }

    void UpdateAshPhase(float dt)
    {
        bool outside = scene == "world";
        if (!state.Hinted("mg:ashStarted"))
        {
            mgPrepT += dt;
            if (PreAshDone || mgPrepT > 75)
            {
                // Helpers finish the jobs you didn't get to (without credit).
                state.hinted["mg:r:water"] = state.Hinted("mg:jar0") && state.Hinted("mg:jar1") && state.Hinted("mg:barrel");
                state.hinted["mg:r:shut"] = state.Hinted("mg:shut0") && state.Hinted("mg:shut1");
                if (!PreAshDone) state.hinted["mg:preHelped"] = true;
                state.hinted["mg:ashStarted"] = true;
                mgAshT = 0; mgOutsideT = 0;
                Save();
                Toast("Ben (on the radio): \"Baga just sent up a puff of ash. The winds higher up are carrying it here. Everyone inside!\"" +
                    (state.Hinted("mg:preHelped") ? " Lira and Niko finish the jobs outside." : ""), 6);
                Sfx.Play("thunder");
            }
            return;
        }
        mgAshT += dt;
        if (outside)
        {
            mgOutsideT += dt;
            // Any real time out in the falling ash counts for the report, not just being brought in (Codex).
            if (mgOutsideT > 2) state.hinted["mg:r:outside"] = true;
            // A little while out in it, and Ma'am Isay brings you in: no harm done, and the report says why.
            if (mgOutsideT > 12)
            {
                state.hinted["mg:r:outside"] = true;
                mgOutsideT = 0;
                FadeThrough(() => { EnterSchoolNow(); }, () => Talk(new() { new("Ma'am Isay", "In you come! While ash is falling, the safest place is inside, with the doors and windows shut.") }));
                return;
            }
        }
        if (mgAshT > 75 && !InsideDone) { state.hinted["mg:inHelped"] = true; Toast("Ma'am Isay and Ben finish handing out the water and the list.", 4); }
        if (InsideDone) ShelterDone();
    }

    void EnterSchoolNow()
    {
        state.exitX = SchoolDoorX; state.exitY = SchoolDoorY + 3;
        LoadScene("house:school");
        player.X = 125; player.Y = (SchoolH - 2) * T + 8; player.Face = "up";
        Save();
    }

    void CloseShutter(int i)
    {
        Swing("hands", 0.3f);
        Sfx.Play("door");
        state.hinted[$"mg:shut{i}"] = true;
        Save();
    }

    void TakeWater()
    {
        mgWater = 3 - Enumerable.Range(0, 3).Count(FamilyHasWater);
        Swing("hands", 0.25f);
        Sfx.Play("ui");
        Toast($"You pick up {mgWater} bottles of sealed water for the families.", 3);
    }

    void GiveWater(int i)
    {
        mgWater--;
        state.hinted[$"mg:water{i}"] = true;
        Swing("hands", 0.25f);
        Sfx.Play("coin");
        Floater("Salamat!", FamilyMats[i].x, FamilyMats[i].y - 16, "#7fd36b");
        Save();
    }

    void RegisterShelter()
    {
        state.hinted["mg:desk"] = true;
        Swing("hands", 0.3f);
        Sfx.Play("ui");
        Save();
        Toast("You write everyone down at the desk: three families, Ma'am Isay's class, Manay Mila and Ben.", 4);
    }

    void ShelterDone()
    {
        if (MgSheltered) return;
        state.hinted["mg:sheltered"] = true;
        state.hinted["mg:ash"] = true;
        state.gifts["mg_shelterAt"] = AbsNow;
        ashFlakes.Clear();
        Save();
        FadeThrough(() =>
        {
            // The hour passes in the shelter: anyone the helpers finished up for while they were outside comes in (Codex).
            if (scene == "world") EnterSchoolNow();
            Advance(60, quiet: true);
            // The watch for rain starts after this hour, even if the skip crossed dawn and NewDay looked (Codex).
            state.gifts["mg_shelterAt"] = AbsNow;
            state.hinted.Remove("mg:rained");
            Save();
        }, () => Talk(new()
        {
            new("", "An hour passes in the shelter. The ash stops falling, and the radio says the air outside is clearing."),
            new("Ben", "That's the ash passing. Keep the water covered, and don't wash ash into the drains: it clogs them. Wet ash is heavy, so clearing roofs is a job for trained adults."),
            new("Ma'am Isay", "If you ever must go out in ash, wear a well-fitting mask, like an N95, and goggles. Masks often don't fit children well, so staying inside comes first. A wet cloth doesn't filter ash any better."),
            new("Ben", "Next we watch for rain. Rain on fresh ash can turn it into a lahar. When it rains, come to the display here."),
            new("", "New clue on your Case board (Baga): Shelter from the ash.")
        }));
    }

    /* ---------- The finale ---------- */
    // The warning's out and everyone's accounted for: the win is kept at once, then the words and the card.
    void FinishMagayon(bool credited)
    {
        if (MgDone) return;
        state.hinted["mg:lahar"] = true;
        state.hinted["mg:channel"] = true;
        state.hinted["mg:done"] = true;
        state.hinted["mg:r:channel"] = credited;
        state.gifts["mg_done"] = state.day;
        state.coins += 150;
        GainXp(60);
        Save();
        Talk(new()
        {
            new("Ben", "Warning's out, the channel and the mouth are closed, and everyone from Baga is here. That's the plan working."),
            new("Manay Mila", "My lola's story kept Mayon's name. Ben's readings told us when to go. Between them, nobody had to be brave today."),
            new("Ma'am Isay", "Remember: a quieter volcano doesn't mean it's over. Lahars can come with any heavy rain, long after an eruption."),
            new("", MgReport())
        }, ShowMagayonCard);
    }

    string MgReport()
    {
        var good = new List<string>();
        var tips = new List<string>();
        void Item(bool did, string yes, string no) => (did ? good : tips).Add(did ? yes : no);
        Item(state.Hinted("mg:r:bagEarly"), "packed a go-bag early", "pack a go-bag early, while it's calm");
        Item(state.Hinted("mg:called"), "called to Manay Mila", "call to your neighbours as you leave");
        Item(state.Hinted("mg:registered"), "signed the list", "sign the evacuation list");
        Item(state.Hinted("mg:r:water"), "protected the water", "cover the water before the ash");
        Item(!state.Hinted("mg:r:outside"), "stayed inside in the ash", "stay inside while ash falls");
        Item(state.Hinted("mg:r:channel"), "radioed the lahar warning", "close the channel and radio the warning yourself");
        string And(List<string> l) => l.Count == 1 ? l[0] : string.Join(", ", l.Take(l.Count - 1)) + " and " + l[^1];
        string s = good.Count > 0 ? "You " + And(good) + "." : "";
        if (tips.Count > 0) s += (s == "" ? "" : " ") + "Next time, " + string.Join("; ", tips) + ".";
        if (state.Hinted("mg:r:turned")) s += " Remember: the danger zone is closed at every level.";
        return s;
    }

    void ShowMagayonCard()
    {
        catchOpenedAt = Raylib.GetTime();
        mode = "magayoncard";
        SetPrompt("");
    }

    void CloseMagayonCard()
    {
        if (mode != "magayoncard" || Raylib.GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        Toast("Case closed (Baga): beneath the clouds. 150 coins from the village. Manay Mila has something for you tomorrow.", 6);
    }

    void DrawMagayonCard()
    {
        if (GoldCard("magayon", "Beneath the clouds", "Case closed",
            "Baga woke slowly, and its station read every sign. Everyone left early, sheltered from the ash, and warned of the lahar in the rain.",
            MgReport(), "Dios mabalos!")) CloseMagayonCard();
    }

    /* ---------- Every frame, looks: Mila walking, pyroclastic flows ---------- */
    void StepMover(Mover m, float dt)
    {
        if (m.Leg >= m.Path.Length) { m.Safe = true; m.Face = "down"; return; }
        var (tx, ty) = m.Path[m.Leg];
        float dx = tx - m.X, dy = ty - m.Y, d = MathF.Sqrt(dx * dx + dy * dy), step = m.Speed * dt;
        if (d <= step) { m.X = tx; m.Y = ty; m.Leg++; return; }
        m.X += dx / d * step; m.Y += dy / d * step;
        m.WalkT += dt;
        m.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
    }

    // At Level 3, now and then a small pyroclastic flow (locally called uson) slides down the south-west gully: looks only.
    void UpdateUson(float dt)
    {
        if (BagaLevel < 3) { mgUsonAt = -1; return; }
        if (mgUsonAt >= 0) { mgUsonAt += dt; if (mgUsonAt > 5) mgUsonAt = -1; return; }
        mgUsonT -= dt;
        if (mgUsonT <= 0) { mgUsonT = FxRand(18, 34); mgUsonAt = 0; }
    }

    /* ---------- Drawing: Baga ---------- */
    float ConeHalfAt(float r) => 5 + (ConeHalf - 5) * MathF.Pow(Math.Clamp(r / ConeH, 0, 1), 1.7f);

    // A Mayon-like cone: concave slopes, steeper near the top, bare grey rock above and green below, gullies running down,
    // and what Baga is doing right now (steam, a glow, lava, a flow, ash).
    void DrawBagaCone()
    {
        const float top = ConeBaseY - ConeH;
        int level = BagaLevel;
        float[] gullies = { -0.62f, -0.27f, 0.12f, 0.46f, 0.78f };
        // A soft shadow where the cone meets the ground.
        pix.Rect(ConeX - ConeHalf - 2, ConeBaseY - 1, (int)(ConeHalf * 2 + 5), 2, "rgba(0,0,0,0.22)");
        for (int r = 0; r < ConeH; r++)
        {
            float f = r / ConeH, half = ConeHalfAt(r);
            int y = (int)(top + r), x0 = (int)MathF.Round(ConeX - half), x1 = (int)MathF.Round(ConeX + half);
            for (int x = x0; x <= x1; x++)
            {
                float u = (x - ConeX) / Math.Max(1, half);   // -1 (west, lit) to 1 (east, shade)
                bool shade = u > 0.16f + 0.05f * MathF.Sin(r * 0.7f), deep = u > 0.62f;
                double h = Pix.Hash(x, y, 431);
                // Bare rock and old ash high up, faint bands following the slope; scrub, then green, lower down,
                // creeping higher in places.
                float green = 0.6f + 0.1f * MathF.Sin(x * 0.29f) + (float)(h * 0.06);
                string c;
                if (f < 0.07f) c = shade ? "#3a3836" : "#5a5550";
                else if (f < green - 0.08f) c = deep ? "#4f4a45" : shade ? "#615b55" : (r % 5 == 0 ? "#958e85" : h < 0.1 ? "#7c766e" : "#88827a");
                else if (f < green) c = deep ? "#4a5240" : shade ? "#5e6650" : h < 0.5 ? "#7e8566" : "#88827a";
                else c = deep ? "#365c34" : shade ? "#416c3a" : h < 0.12 ? "#71a35b" : "#5c8d49";
                pix.Rect(x, y, 1, 1, c);
            }
            // The edges, so the cone stands out from the ground behind it.
            pix.Rect(x0, y, 1, 1, f < 0.6f ? "#4f4a45" : "#3f6438");
            pix.Rect(x1, y, 1, 1, f < 0.6f ? "#2e2c2a" : "#2f4e2c");
            // The gullies: darker lines down from the summit, wavering a little.
            if (f > 0.1f)
                foreach (var g in gullies)
                {
                    int gx = (int)MathF.Round(ConeX + half * g + MathF.Sin(r * 0.33f + g * 9) * 0.8f);
                    pix.Rect(gx, y, 1, 1, f < 0.6f ? "#4a4642" : "#335533");
                    if (g < 0) pix.Rect(gx + 1, y, 1, 1, f < 0.6f ? "#a29a90" : "#6a9a55");
                }
        }
        // The crater's notch at the top.
        pix.Rect(ConeX - 5, top, 11, 2, "#2e2c2e");
        pix.Rect(ConeX - 3, top - 1, 7, 1, "#45413f");
        // Lava down the south-west gully and rocks rolling down it (Level 3 and up).
        if (level >= 3)
        {
            for (int r = 2; r < ConeH * 0.48f; r++)
            {
                float half = ConeHalfAt(r);
                int gx = (int)MathF.Round(ConeX + half * -0.27f + MathF.Sin(r * 0.33f - 2.43f) * 0.8f);
                bool hot = (r * 7 + (int)(time * 6)) % 11 < 4, night = Darkness > 0.3f;
                int wide = r > ConeH * 0.25f ? 2 : 1;
                pix.Rect(gx, top + r, wide, 1, night ? (hot ? "#ffb347" : "#e0582a") : (hot ? "#c8502a" : "#3a2a26"));
                if (!night && r % 3 == 0) pix.Rect(gx - 1, top + r, 1, 1, "#2a2220");
            }
            for (int i = 0; i < 3; i++)
            {
                float k = (time * 0.35f + i * 0.33f) % 1, r = 4 + k * ConeH * 0.55f, half = ConeHalfAt(r);
                pix.Rect(ConeX + half * -0.27f + 2 + i, top + r, 2, 1, Darkness > 0.3f ? "#ffd27a" : "#8a8580");
            }
            DrawUson(top);
        }
        // The plume: wisps of steam, or grey ash when Baga's just puffed; it leans west with the winds higher up.
        bool ash = AshFalling || level >= 3 && (int)(time / 9) % 3 == 0;
        int wisps = level == 0 ? 3 : level == 1 ? 4 : 6;
        for (int i = 0; i < wisps; i++)
        {
            float rise = (time * (level >= 2 ? 5 : 3) + i * 9) % 36;
            float a = (level == 0 ? 0.3f : 0.45f) * (1 - rise / 36);
            var col = ash ? Pal.Rgba(120, 112, 104, a + 0.1f) : Pal.Rgba(214, 220, 214, a);
            pix.Rect(ConeX - 4 - rise * 0.7f, top - 4 - rise, 8 + (int)(rise / 3), 3, col);
        }
    }

    void DrawUson(float top)
    {
        if (mgUsonAt < 0) return;
        float k = mgUsonAt / 5;
        for (int i = 0; i < 7; i++)
        {
            float r = Math.Min(ConeH - 2, (k * 1.3f - i * 0.05f) * ConeH);
            if (r < 2) continue;
            float half = ConeHalfAt(r), x = ConeX + half * -0.62f + MathF.Sin(i * 2.1f) * 2, rad = 3 + r * 0.08f;
            var c = Pal.Rgba(150, 140, 130, 0.75f * (1 - k * 0.6f));
            pix.Rect(x - rad, top + r - rad, (int)(rad * 2), (int)(rad * 1.6f), c);
            pix.Rect(x - rad * 0.6f, top + r - rad * 1.4f, (int)(rad * 1.2f), (int)rad, Pal.Rgba(185, 176, 166, 0.6f * (1 - k * 0.6f)));
        }
    }

    // At ground level: the lahar channel (dry, flowing in the rain, or with fresh mud in it) and the zone's marker stones.
    void DrawBagaGround(float t)
    {
        if (Math.Abs(ConeX - player.X) > W + 80 || Math.Abs(PdzY - player.Y) > H + 120) return;
        bool flowing = MgOrder && state.weather != "clear" && rainAmt > 0.3f;
        bool mud = MgOrder && (state.Hinted("mg:rained") || flowing);
        for (int i = 1; i < LaharChannel.Length; i++)
        {
            var (ax, ay) = LaharChannel[i - 1]; var (bx, by) = LaharChannel[i];
            int n = (int)Dist(ax, ay, bx, by);
            for (int s = 0; s <= n; s++)
            {
                float x = ax + (bx - ax) * s / n, y = ay + (by - ay) * s / n;
                for (int o = -3; o <= 3; o++)
                {
                    int px = (int)MathF.Round(x + o), py = (int)MathF.Round(y);
                    string c = Math.Abs(o) == 3 ? "#5d554d" : mud ? (flowing && ((int)(py * 0.7f - t * 14) % 5 == 0) ? "#6a5a46" : "#8a7a62") : (Pix.Hash(px, py, 441) < 0.2 ? "#9a9288" : "#7d746a");
                    pix.Rect(px, py, 1, 1, c);
                }
            }
        }
        // The mouth: a fan where the channel spreads out onto the beach; mud past its banks once a lahar has come down.
        if (mud)
            for (int y = -10; y <= 8; y++)
                for (int x = -14; x <= 14; x++)
                    if (x * x / 196f + y * y / 81f < 1 && Pix.Hash(x + 3000, y, 442) < 0.85)
                        pix.Rect(LaharMouthX + x, LaharMouthY + y, 1, 1, Pix.Hash(x, y, 443) < 0.15 ? "#6a5a46" : "#8a7a62");
    }

    // The marker stones round the permanent danger zone, standing where there's ground.
    IEnumerable<(float x, float y)> MarkerStones()
    {
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * MathF.Tau, x = PdzX + MathF.Cos(a) * (PdzRX + 2), y = PdzY + MathF.Sin(a) * (PdzRY + 2);
            if (Walkable(TileAt((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1) / T)))) yield return (x, y);
        }
    }

    void AddMagayonObjects(List<(float y, Action draw)> list)
    {
        if (Math.Abs(2310 - player.X) > W + 120 || Math.Abs(440 - player.Y) > H + 140)
        {
            AddSchoolFront(list);
            return;
        }
        foreach (var (x, y) in MarkerStones())
        {
            float mx = x, my = y;
            list.Add((my, () => DrawMarkerStone(mx, my)));
        }
        list.Add((AbacaY, DrawAbaca));
        list.Add((PiliY, DrawPili));
        list.Add((AlertBoardY, () => DrawAlertBoard(AlertBoardX, AlertBoardY)));
        if (state.Hinted("mg:read1")) list.Add((CrateY, () => DrawSupplyCrate(CrateX, CrateY)));
        if (BagaEvacuating)
        {
            list.Add((EvacTableY, () => DrawEvacTable(EvacTableX, EvacTableY)));
            list.Add((EvacBoatY, () => DrawEvacBanca(EvacBoatX, EvacBoatY)));
        }
        if (mgMilaWalk != null)
        {
            var m = mgMilaWalk;
            list.Add((m.Y, () => DrawMila(pix, (int)MathF.Round(m.X), (int)MathF.Round(m.Y), m.Face, m.Safe ? 0 : 1 + (int)(m.WalkT * 8) % 4)));
        }
    }

    void DrawMarkerStone(float x, float y)
    {
        pix.Rect(x - 1, y, 4, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 1, y - 5, 3, 5, "#e8e4da");
        pix.Rect(x - 1, y - 3, 3, 1, "#d8323a");
        pix.Rect(x + 1, y - 5, 1, 5, "#bdb8ac");
    }

    void DrawAbaca()
    {
        float x = AbacaX, y = AbacaY;
        // Three abaca plants (a banana's cousin): tall stems and broad leaves.
        foreach (var (ox, h) in new[] { (-9, 20), (0, 24), (8, 18) })
        {
            float px = x + ox;
            pix.Rect(px, y - h, 2, h, "#6f8f3a");
            pix.Rect(px - 6, y - h - 2, 7, 3, "#5f9a48"); pix.Rect(px + 2, y - h + 1, 7, 3, "#4f8a3e");
            pix.Rect(px - 4, y - h + 7, 5, 2, "#6aa552"); pix.Rect(px + 2, y - h + 9, 5, 2, "#5f9a48");
        }
        // The drying frame, with the fibre hanging once it's tied.
        pix.Rect(x + 13, y - 14, 1, 14, "#6b4a2b"); pix.Rect(x + 25, y - 14, 1, 14, "#6b4a2b"); pix.Rect(x + 13, y - 14, 13, 1, "#7a5a3e");
        if (state.Hinted("mg:abaca") || !MgMet)
            for (int i = 0; i < 5; i++) pix.Rect(x + 15 + i * 2, y - 13, 1, 9 + (i % 2), "#e8d8a8");
        else if ((time * 1.5f) % 1 < 0.5f) pix.Rect(x + 18, y - 4, 3, 2, "#e8d8a8");
    }

    void DrawPili()
    {
        float x = PiliX, y = PiliY;
        pix.Rect(x - 2, y, 5, 1, "rgba(0,0,0,0.2)");
        if (state.Hinted("mg:pili"))
        {
            pix.Rect(x, y - 7, 1, 7, "#6b4a2b");
            pix.Rect(x - 3, y - 9, 3, 2, "#4f8a3e"); pix.Rect(x + 1, y - 10, 3, 2, "#5f9a48"); pix.Rect(x - 1, y - 12, 3, 2, "#6aa552");
        }
        else if (MgMet)
        {
            // A seedling in a pot waiting by the path, and the hole dug for it.
            pix.Rect(x - 2, y - 1, 5, 2, "#5a4632");
            pix.Rect(x + 4, y - 4, 4, 4, "#b5683a"); pix.Rect(x + 5, y - 7, 1, 3, "#4f8a3e"); pix.Rect(x + 4, y - 8, 3, 1, "#5f9a48");
        }
    }

    // Ben's alert board: the number big, a colour strip, and "Level" in little bars.
    void DrawAlertBoard(float bx, float by)
    {
        int x = (int)bx, y = (int)by;
        pix.Rect(x - 7, y, 15, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 6, y - 11, 1, 11, "#6b4a2b"); pix.Rect(x + 6, y - 11, 1, 11, "#6b4a2b");
        pix.Rect(x - 8, y - 20, 17, 11, "#f2efe6");
        pix.Rect(x - 8, y - 20, 17, 1, "#c9c4b6");
        for (int i = 0; i < 6; i++) pix.Rect(x - 7 + i * 2, y - 11, 2, 1, i <= BagaLevel ? (i < 2 ? "#5fb05f" : i < 4 ? "#e8a83a" : "#d8323a") : "#c9c4b6");
        pix.Rect(x - 6, y - 17, 5, 1, "#8a8478"); pix.Rect(x - 6, y - 15, 4, 1, "#8a8478");
        DrawDigit(x + 1, y - 18, BagaLevel, BagaLevel >= 3 ? "#d8323a" : "#10243a");
    }

    // A 3x5 pixel digit.
    void DrawDigit(int x, int y, int d, string c)
    {
        string[] glyphs = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111" };
        string g = glyphs[Math.Clamp(d, 0, 5)];
        for (int i = 0; i < 15; i++) if (g[i] == '1') pix.Rect(x + i % 3, y + i / 3, 1, 1, c);
    }

    void DrawSupplyCrate(float x, float y)
    {
        pix.Rect(x - 6, y, 13, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 6, y - 8, 13, 8, "#9a6a3a"); pix.Rect(x - 6, y - 8, 13, 1, "#b58250"); pix.Rect(x - 6, y - 4, 13, 1, "#7a5230");
        pix.Rect(x - 1, y - 7, 3, 1, "#e04b3a"); pix.Rect(x, y - 8, 1, 3, "#e04b3a");
        if (!state.Hinted("mg:bag")) { pix.Rect(x + 3, y - 11, 4, 3, "#3f7f5a"); pix.Rect(x + 4, y - 12, 2, 1, "#2f5f44"); }
    }

    void DrawEvacTable(float x, float y)
    {
        pix.Rect(x - 7, y, 15, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 7, y - 6, 15, 2, "#8a5f36"); pix.Rect(x - 6, y - 4, 1, 4, "#6b4a2b"); pix.Rect(x + 6, y - 4, 1, 4, "#6b4a2b");
        pix.Rect(x - 3, y - 8, 5, 3, "#f2efe6"); pix.Rect(x - 2, y - 7, 3, 1, "#8a8478");
        // A little pennant on a stick so it's easy to find.
        pix.Rect(x + 5, y - 15, 1, 9, "#c8c8c0"); pix.Rect(x + 6, y - 15, 4, 3, "#2f8a4a");
    }

    void DrawEvacBanca(float x, float y)
    {
        float bob = MathF.Sin(time * 2) * 0.6f;
        y += bob;
        pix.Rect(x - 12, y + 3, 25, 1, "rgba(0,0,0,0.2)");
        pix.Rect(x - 10, y, 21, 3, "#9a6a3a"); pix.Rect(x - 12, y + 1, 2, 2, "#9a6a3a"); pix.Rect(x + 11, y + 1, 2, 2, "#9a6a3a");
        pix.Rect(x - 10, y, 21, 1, "#c58b4a");
        pix.Rect(x - 13, y + 5, 27, 1, "#d8c49a"); pix.Rect(x - 13, y - 3, 27, 1, "#d8c49a");
        pix.Rect(x - 6, y - 3, 1, 8, "#7a5a3e"); pix.Rect(x + 6, y - 3, 1, 8, "#7a5a3e");
        var look = new Look { skin = 2, shirt = 2, hat = 0, hair = 1 };
        LookData.DrawPerson(pix, look, (int)x + 4, (int)(y + 1), "left", 0, shadow: false);
        pix.Rect(x - 1, y - 13, 11, 2, "#ddbc78"); pix.Rect(x + 1, y - 15, 7, 2, "#f0d596");
    }

    // Manay Mila: a striped scarf over her hair and an abaca-coloured blouse.
    static void DrawMila(Pix p, int x, int y, string face, int step, int bob = 0)
    {
        LookData.DrawFigure(p, "#a9714b", "#3a2a20", "#d9a86a", "#5a4a7a", true, 0, null, x, y, face, step, bob: bob);
        p.Rect(x - 3, y - 13 - bob, 7, 2, "#c0392b"); p.Rect(x - 3, y - 12 - bob, 7, 1, "#f2c94a");
    }

    // Ben: a teal field vest and a cap.
    static void DrawBen(Pix p, int x, int y, string face, int step, int bob = 0)
    {
        LookData.DrawFigure(p, "#c68a5a", "#2b1d14", "#2a8a8a", "#3f4a5a", false, 3, "#e8a83a", x, y, face, step, bob: bob);
        p.Rect(x - 2, y - 7 - bob, 1, 3, "#f2c94a");
    }

    // Ben's station: a raised hut with a tin roof, a solar panel, a radio mast and a little dish.
    void DrawStation(float sx, float sy)
    {
        int x = (int)sx, y = (int)sy;
        pix.Rect(x - 16, y + 3, 32, 2, "rgba(0,0,0,0.2)");
        pix.Rect(x - 13, y - 5, 2, 9, "#65452e"); pix.Rect(x + 11, y - 5, 2, 9, "#65452e");
        pix.Rect(x - 15, y - 19, 30, 15, "#d8d4c8");
        pix.Rect(x - 15, y - 19, 30, 1, "#efece4");
        pix.Rect(x - 4, y - 14, 7, 10, "#3c4a5a");
        pix.Rect(x - 12, y - 15, 6, 5, "#485d56"); pix.Rect(x + 6, y - 15, 6, 5, "#485d56");
        pix.Rect(x - 15, y - 6, 30, 2, "#2a8a8a");
        // The tin roof and the solar panel on it.
        for (int i = 0; i < 6; i++) pix.Rect(x - 17 + i, y - 25 + i, 34 - i * 2, 1, i % 2 == 0 ? "#9aa0a5" : "#b9c0c4");
        pix.Rect(x - 17, y - 20, 34, 1, "#7a8085");
        pix.Rect(x + 2, y - 26, 10, 4, "#2f4f7a"); pix.Rect(x + 3, y - 25, 8, 1, "#5f8fc0");
        // The radio mast with a blinking light, and the dish.
        pix.Rect(x - 12, y - 42, 1, 22, "#c8c8c0");
        for (int i = 0; i < 4; i++) pix.Rect(x - 13, y - 40 + i * 5, 3, 1, "#a8a8a0");
        if ((time * 1.2f) % 1 < 0.5f) pix.Rect(x - 12, y - 43, 1, 1, "#ff4a3a");
        pix.Rect(x - 8, y - 30, 4, 3, "#e8e4da"); pix.Rect(x - 7, y - 31, 2, 1, "#e8e4da"); pix.Rect(x - 6, y - 27, 1, 2, "#9aa0a5");
    }

    /* ---------- Drawing: the school as the shelter ---------- */
    void AddSchoolFront(List<(float y, Action draw)> list)
    {
        if (!(AshPhase || BagaFolkAtSchool) || Math.Abs(SchoolX - player.X) > W || Math.Abs(SchoolY - player.Y) > H) return;
        for (int i = 0; i < JarSpots.Length; i++)
        {
            int k = i;
            list.Add((JarSpots[k].y, () => DrawJar(JarSpots[k].x, JarSpots[k].y, JarCovered(k))));
        }
        list.Add((BarrelY, () => DrawBarrel(BarrelX, BarrelY, BarrelOff)));
    }

    // A clay water jar (tapayan): round-bellied, narrow-necked; a woven lid once it's covered.
    void DrawJar(float x, float y, bool covered)
    {
        pix.Rect(x - 3, y, 7, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 2, y - 1, 5, 1, "#8a4a2a");
        pix.Rect(x - 3, y - 6, 7, 5, "#a8603a");
        pix.Rect(x - 2, y - 8, 5, 2, "#a8603a");
        pix.Rect(x - 1, y - 10, 3, 2, "#8a4a2a");
        pix.Rect(x - 2, y - 7, 1, 4, "#c88a5a"); pix.Rect(x + 2, y - 6, 1, 4, "#8a4a2a");
        if (covered) { pix.Rect(x - 3, y - 12, 7, 2, "#c9a86a"); pix.Rect(x - 2, y - 12, 5, 1, "#e0c890"); }
        else pix.Rect(x - 1, y - 10, 3, 1, "#3f7f96");
    }

    void DrawBarrel(float x, float y, bool off)
    {
        pix.Rect(x - 4, y, 9, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 4, y - 8, 9, 8, "#4f6f8a"); pix.Rect(x - 4, y - 6, 9, 1, "#3a5670"); pix.Rect(x - 4, y - 2, 9, 1, "#3a5670");
        // The downpipe from the school's roof: into the barrel, or swung aside.
        if (off) { pix.Rect(x - 3, y - 26, 2, 14, "#9aa0a5"); pix.Rect(x - 8, y - 12, 6, 2, "#9aa0a5"); }
        else pix.Rect(x - 1, y - 26, 2, 19, "#9aa0a5");
    }

    // Falling ash over Amihan Village, drawn over everything outdoors: grey flakes drifting down, and a grey haze.
    void DrawAshfall(float dt)
    {
        if (!AshFalling || scene != "world") { if (ashFlakes.Count > 0) ashFlakes.Clear(); return; }
        pix.Fill(0, 0, W, H, Pal.Rgba(122, 116, 106, 0.4f));
        while (ashFlakes.Count < 160) ashFlakes.Add((FxRand(0, W), FxRand(0, H), FxRand(-8, 3), FxRand(0, 1)));
        for (int i = 0; i < ashFlakes.Count; i++)
        {
            var (x, y, vx, life) = ashFlakes[i];
            x += vx * dt + MathF.Sin(time * 2 + i) * 4 * dt; y += (10 + life * 10) * dt;
            if (y > H) { y = FxRand(-10, 0); x = FxRand(0, W); }
            if (x < 0) x += W; else if (x > W) x -= W;
            ashFlakes[i] = (x, y, vx, life);
            int sz = life > 0.7f ? 2 : 1;
            pix.Fill((int)x, (int)y, sz, sz, Pal.Rgba(96, 92, 86, 0.85f));
        }
    }

    /* ---------- Night lights ---------- */
    void LightBaga(float t)
    {
        if (BagaLevel < 3 || Math.Abs(ConeX - player.X) > W + 60 || Math.Abs(ConeBaseY - player.Y) > H + 80) return;
        LightHole(ConeX, ConeBaseY - ConeH, 22 + MathF.Sin(t * 2) * 2, 0.8f);
        LightHole(ConeX - 10, ConeBaseY - ConeH * 0.7f, 16, 0.6f);
    }

    void GlowBaga(float t, float k)
    {
        if (BagaLevel < 3 || Math.Abs(ConeX - player.X) > W + 60 || Math.Abs(ConeBaseY - player.Y) > H + 80) return;
        pix.Glow(ConeX, ConeBaseY - ConeH, 18, Pal.Rgba(255, 120, 50, (0.35f + 0.08f * MathF.Sin(t * 2)) * k));
        // The lava down the gully, bright against the dark.
        for (int r = 2; r < ConeH * 0.48f; r++)
        {
            float half = ConeHalfAt(r);
            int gx = (int)MathF.Round(ConeX + half * -0.27f + MathF.Sin(r * 0.33f - 2.43f) * 0.8f);
            bool hot = (r * 7 + (int)(t * 6)) % 11 < 4;
            pix.Rect(gx, ConeBaseY - ConeH + r, r > ConeH * 0.25f ? 2 : 1, 1, Pal.Rgba(255, hot ? 190 : 110, hot ? 90 : 40, k));
        }
    }

    /* ---------- The guide ---------- */
    // One goal for the case, a step at a time; the go-bag as its own job once Ben has asked. Nothing before Manay Mila.
    void MagayonGoals(List<Goal> list)
    {
        if (!MgStarted) return;
        if (GiftDue)
        {
            var gift = NewGoal("mg:gift", "request", "Manay Mila has something for you", BagaFolkAtSchool ? "She's at the school in Amihan Village." : "Visit her garden on Baga.");
            gift.Ready = true;
            list.Add(AtMila(gift));
        }
        if (MgDone) return;
        if (state.Hinted("mg:read1") && !state.Hinted("mg:bag") && !MgOrder)
        {
            var bag = NewGoal("mg:bag", "request", "Pack a go-bag", "Ben asked: pack one now, while it's calm, at the supply crate by his station.");
            bag.Ready = true;
            list.Add(At(bag, CrateX, CrateY + 6, "Supply crate"));
        }
        var g = NewGoal("mg:case", "request", $"Beneath the clouds ({MgClueCount} of 4 clues)", "");
        if (!MgStory)
        {
            bool abaca = state.Hinted("mg:abaca"), pili = state.Hinted("mg:pili");
            if (!abaca) { g.Text = "Help Manay Mila: tie the abaca fibre on her drying frame."; At(g, AbacaX, AbacaY + 4, "Abaca frame"); }
            else if (!pili) { g.Text = "Help Manay Mila: plant the pili seedling by the path."; At(g, PiliX, PiliY + 4, "Pili seedling"); }
            else { g.Text = "Tell Manay Mila it's done."; g.Ready = true; AtMila(g); }
        }
        else if (!state.Hinted("mg:metBen")) { g.Text = "Visit Ben's volcano station on Baga's north shore."; AtIslander(g, "ben"); }
        else if (!state.Hinted("mg:unrest")) g.Text = "Baga is quiet for now. Come back to Baga on another day, by daylight.";
        else if (ReadingsDue) { g.Text = "Baga is restless. Help Ben with the readings at his station."; AtIslander(g, "ben"); }
        else if (!state.Hinted("mg:read2")) g.Text = "New readings at Ben's station tomorrow.";
        else if (MgSheltered)
        {
            g.Title = "Watch for rain on Baga";
            g.Text = MgRained ? "It has rained on Baga. Help Ben at the display in the school." : "When it rains, help Ben at the display in the school in Amihan Village. Lahars come with rain on fresh ash.";
            g.Ready = MgRained;
            At(g, DisplayX, DisplayY + 10, "The display", "house:school");
        }
        else return;
        list.Add(g);
    }

    Goal AtMila(Goal g) => BagaFolkAtSchool ? At(g, 74, 88, "Manay Mila", "house:school") : AtIslander(g, "mila");

    // During the evacuation and the ash, the guide follows only this (UpdateGuide).
    Goal MagayonNowGoal()
    {
        if (BagaEvacuating)
        {
            var g = NewGoal("mg:leave", "story", "Leave Baga", EvacNext() + ".");
            if (!state.Hinted("mg:bag") && !state.Hinted("mg:bagGiven")) return At(g, CrateX, CrateY + 6, "Supply crate");
            if (!state.Hinted("mg:called") && mgMilaWalk == null) return At(g, MilaX, MilaY + 4, "Manay Mila");
            if (!state.Hinted("mg:registered")) return At(g, EvacTableX, EvacTableY + 6, "Evacuation list");
            return At(g, BoardPointX, BoardPointY, "Niko's banca");
        }
        var a = NewGoal("mg:ash", "story", AshFalling ? "Shelter from the ash" : "Get the shelter ready", "");
        if (!AshFalling)
        {
            a.Text = PreAshNext() + ".";
            for (int i = 0; i < JarSpots.Length; i++) if (!JarCovered(i)) return At(a, JarSpots[i].x, JarSpots[i].y + 4, "Water jar");
            if (!BarrelOff) return At(a, BarrelX, BarrelY + 4, "Rain barrel");
            for (int i = 0; i < ShutterSpots.Length; i++) if (!ShutterClosed(i)) return At(a, ShutterSpots[i].x, ShutterSpots[i].y, "Shutter", "house:school");
            return At(a, SchoolDoorX, SchoolDoorY + 4, "The school");
        }
        if (!InSchoolRoom) { a.Text = "Ash is falling: get inside the school."; return At(a, SchoolDoorX, SchoolDoorY + 4, "The school"); }
        if (Enumerable.Range(0, 3).Any(i => !FamilyHasWater(i)))
        {
            a.Text = "Hand out sealed water to the three families.";
            if (mgWater == 0) return At(a, WaterCrateX, WaterCrateY + 6, "Water crate", "house:school");
            int f = Enumerable.Range(0, 3).First(i => !FamilyHasWater(i));
            return At(a, FamilyMats[f].x, FamilyMats[f].y + 10, "A family", "house:school");
        }
        a.Text = "Register everyone at the desk.";
        return At(a, DeskX + 5, DeskY + 6, "The desk", "house:school");
    }

    /* ---------- Loading and starting ---------- */
    // A game saved somewhere that's closed now: on Baga while the order stands goes to the shelter's door, and inside the
    // permanent danger zone (an old save) goes to Baga's jetty.
    void PlaceAfterLoad()
    {
        if (scene != "world") return;
        if (InPdz(player.X, player.Y)) { player.X = 2315; player.Y = 404; state.riding = false; }
        if (BagaClosed && !Aboard && BagaClosedTile((int)MathF.Floor(player.X / T), (int)MathF.Floor((player.Y - 1.5f) / T)))
        {
            player.X = SchoolDoorX; player.Y = SchoolDoorY + 6; state.riding = false;
        }
        // In the water while leaving Baga or sheltering (wading when it was saved): onto the nearest dry ground, so no
        // build or rock on the shore can leave you without a step out (Codex).
        if (VolcanoControlled && !Aboard && !Walkable(TileUnder(player.X, player.Y)))
        {
            int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
            var dry = (from r in Enumerable.Range(1, 8) from oy in Enumerable.Range(-r, 2 * r + 1) from ox in Enumerable.Range(-r, 2 * r + 1)
                       where Math.Max(Math.Abs(ox), Math.Abs(oy)) == r && Walkable(TileAt(tx + ox, ty + oy)) && CanStand((tx + ox) * T + 5, (ty + oy) * T + 8)
                       select ((tx + ox) * T + 5f, (ty + oy) * T + 8f)).FirstOrDefault();
            if (dry != default) (player.X, player.Y) = dry;
            state.riding = false;
        }
    }

    void ResetMagayon()
    {
        mgEvacT = mgPrepT = mgAshT = mgOutsideT = mgTurnedT = 0; mgUsonT = 8; mgUsonAt = -1;
        mgWater = 0; mgMilaWalk = null; readings = null; gobag = null; laharWatch = null; ashFlakes.Clear();
    }
}
