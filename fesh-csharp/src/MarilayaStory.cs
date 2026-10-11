using Raylib_cs;

namespace Fesh;

// The heart that rose (1.23), story 5, on Marilaya. Aling Sela, a retired surveyor who fishes, tells her lola's version
// of the story people tell about Marinduque (Mutya Maria and Garduke, lost in a storm, and a heart-shaped island rising
// where they vanished; the game's retelling, after the one in Tales from the 7,000 Isles), then asks for help: she wants
// a discovery trail for the island's children, from the shore to Tindig's top, with a sign at every stop saying what the
// island itself tells us. Four stops, in any order (and you can start before you've met her): the rock face in the
// north-east hills (brush it clean: shells in the limestone, an old wave notch high and dry, the layers' order), the cave
// (follow the water through it, then run its model), the hot springs (read two gauges and a night's record, then warm
// the dryer with the spring), and the tide post off Maniwaya (read it at low tide and at high tide). Her shore model in
// the town square sorts what moves the shore and how fast. Then the chart: set up the heliotrope on Tindig's top with
// Jun, sight from the survey stone (A) and from Baltasar (B), and the two bearings fix the summit. Codex designed this
// with Claude and fact-checked it: no single site's notch is claimed for Marinduque, Malbog's heat is measured (DOST's
// dryer there), heliotropes were used in Philippine surveys (1908), and the sandbar walk is a guided extra.
partial class Game
{
    bool MrMet => state.Hinted("mr:met");
    bool MrDone => state.Hinted("mr:done");
    bool MrSorted => state.Hinted("mr:theory");
    int MrClueCount => Data.MarilayaClues.Count(c => state.Hinted(c.Key));
    // The four stops are done (the legend is the fifth clue, from meeting her).
    bool MrStopsDone => Data.MarilayaClues.Where(c => c.Key != "mr:legend").All(c => state.Hinted(c.Key));
    bool MrAllClues => MrClueCount == Data.MarilayaClues.Length;
    bool MrBearingsDone => state.Hinted("mr:ab") && state.Hinted("mr:ac") && state.Hinted("mr:bc");
    // All five stories done: the Halcyon, the moon, the restless sea, Baga, and this one.
    bool AllStoriesDone => state.flags.ended && MoonReturned && SeaCaseClosed && MgDone && MrDone;
    bool GatherDue => AllStoriesDone && !state.Hinted("mr:gathered");
    // The heliotrope flashes while the sun's out on a clear day (MarilayaUi.cs reads it for the sight).
    bool MirrorFlashing => state.Hinted("mr:mirror") && Darkness < 0.3f && state.weather == "clear";
    bool SightingLight => Darkness < 0.3f;

    sealed class MlGather { }
    MlGather mlGather;

    /* ---------- What's in front of you ---------- */
    Target MarilayaTarget()
    {
        float x = player.X, y = player.Y;
        if (Dist(x, y, NoticeX, NoticeY + 6) < 14 && !Aboard)
            return new Target { Type = "marilaya", Id = "notice", Label = "Read the notice" };
        if (!InMarilaya || Aboard) return null;
        if (mlGather == null)
            foreach (var f in MlFolk)
            {
                if (!MlFolkHere(f.id)) continue;
                var s = MlWalk(f.id);
                if (Dist(x, y, s.X, s.Y + 6) < 15)
                    return new Target { Type = "marilaya", Id = f.id, Label = f.id == "sela" && GatherDue ? "Gather the storytellers" : MlFolkLabel(f.id, f.name) };
            }
        // At the stations, by night (or cloud, with the heliotrope up), you can wait for the light (Codex).
        bool sighting = MrSorted && !MrDone, waitLight = sighting && !SunReady;
        if (Dist(x, y, DatumX, DatumY + 6) < 14)
            return new Target { Type = "marilaya", Id = "datum", Label = sighting ? "Sight from the survey stone" : "Read the survey stone",
                AltType = waitLight ? "mlsun" : null, AltLabel = waitLight ? (SightingLight ? "Wait for the sun" : "Wait for daylight") : null };
        if (Dist(x, y, BaltasarX, BaltasarY + 6) < 16)
            return new Target { Type = "marilaya", Id = "baltasar", Label = sighting ? "Sight from Baltasar's mark" : "Read the survey mark",
                AltType = waitLight ? "mlsun" : null, AltLabel = waitLight ? (SightingLight ? "Wait for the sun" : "Wait for daylight") : null };
        var (fx, fy) = MlPlace("cliff");
        if (Dist(x, y, fx, fy + 6) < 26) return new Target { Type = "marilaya", Id = "cliff", Label = state.Hinted("mr:layers") ? "Look at the rock face" : "Look closely at the rock face" };
        var (cx, cy) = MlPlace("cave");
        if (Dist(x, y, cx, cy + 5) < 13) return new Target { Type = "marilaya", Id = "cave", Label = "Go into the cave" };
        if (Dist(x, y, SpringGaugeAt.x, SpringGaugeAt.y + 6) < 12) return new Target { Type = "marilaya", Id = "gauge1", Label = "Read the hot spring's gauge" };
        if (Dist(x, y, StreamGaugeAt.x, StreamGaugeAt.y + 6) < 12) return new Target { Type = "marilaya", Id = "gauge2", Label = "Read the stream's gauge" };
        if (Dist(x, y, DryerAt.x, DryerAt.y + 6) < 18) return new Target { Type = "marilaya", Id = "dryer", Label = DryerLabel() };
        var (sx, sy) = MlPlace("springs");
        if (Dist(x, y, sx, sy + 6) < 18) return new Target { Type = "info", Label = "Hot water: stay behind the rail. Read the gauge instead." };
        var (px, py) = PostReadSpot();
        if (Dist(x, y, px, py) < 13 || Dist(x, y, tidePostX, tidePostY + 4) < 12)
        {
            // No offer to wait when the water's already where you need it.
            string wait = TideWaitFor();
            if (wait == "low" && SeaLevel <= TideLowMark || wait == "high" && SeaLevel >= TideHighMark) wait = null;
            return new Target { Type = "marilaya", Id = "tidepost", Label = "Read the tide post", AltType = wait != null ? "mlwait" : null,
                AltLabel = wait == "low" ? "Wait for low tide" : wait == "high" ? "Wait for high tide" : null };
        }
        if (Dist(x, y, CairnAt.x, CairnAt.y + 5) < 14 || Dist(x, y, MirrorAt.x, MirrorAt.y + 5) < 12)
            return new Target { Type = "marilaya", Id = "cairn", Label = MrSorted && !state.Hinted("mr:mirror") ? "Set up the mirror with Jun" : "The summit cairn" };
        if (Dist(x, y, ModelAt.x, ModelAt.y + 6) < 18) return new Target { Type = "marilaya", Id = "model", Label = "The shore model" };
        var (ax, ay) = MlPlace("arrowroot");
        if (Dist(x, y, ax, ay) < 16) return new Target { Type = "info", Label = "Arrowroot (uraro): starch from its underground stems (rhizomes) is dried into flour for cookies" };
        var (tx, ty) = MlPlace("trailhead");
        if (Dist(x, y, tx, ty) < 12) return new Target { Type = "info", Label = MrDone ? "The discovery trail: shore to summit" : "The trail up Bundok Tindig" };
        return SideTarget();
    }

    string MlFolkLabel(string id, string name) => id switch
    {
        "sela" => !MrMet ? "Talk to Aling Sela" : MrStopsDone && !MrSorted ? "Tell Aling Sela what you've found" : MrBearingsDone && !MrDone ? "Plot the chart with Aling Sela" : "Talk to Aling Sela",
        "jun" => MrSorted && !state.Hinted("mr:mirror") ? "Set up the mirror with Jun" : "Talk to Jun",
        "ambo" => AmboLabel(),
        "iyo" => "Practise the kalutang with Mang Iyo",
        "nita" => "Visit Aling Nita's butterflies",
        _ => $"Talk to {name}"
    };

    void UseMarilaya(Target t)
    {
        if (MlFolk.Any(f => f.id == t.Id)) FaceToward(MlWalk(t.Id).X, MlWalk(t.Id).Y);
        switch (t.Id)
        {
            case "notice": ReadSelaNotice(); break;
            case "sela": if (GatherDue) StartGathering(); else TalkSela(); break;
            case "jun": TalkJun(); break;
            case "ambo": TalkAmbo(); break;
            case "lorna": TalkLorna(); break;
            case "iyo": OpenKalutang(); break;
            case "nita": OpenNursery(); break;
            case "datum": FaceToward(DatumX, DatumY); UseStation("A"); break;
            case "baltasar": FaceToward(BaltasarX, BaltasarY); UseStation("B"); break;
            case "cliff": FaceToward(MlPlace("cliff").x, MlPlace("cliff").y - 8); OpenCliff(); break;
            case "cave": EnterHouse("house:mlcave", MlPlace("cave").x, MlPlace("cave").y); break;
            case "gauge1": FaceToward(SpringGaugeAt.x, SpringGaugeAt.y); ReadGauge(true); break;
            case "gauge2": FaceToward(StreamGaugeAt.x, StreamGaugeAt.y); ReadGauge(false); break;
            case "dryer": FaceToward(DryerAt.x, DryerAt.y); UseDryer(); break;
            case "tidepost": FaceToward(tidePostX, tidePostY); OpenTidePost(); break;
            case "cairn": FaceToward(CairnAt.x, CairnAt.y); UseCairn(); break;
            case "model": FaceToward(ModelAt.x, ModelAt.y); UseModel(); break;
            default: UseSide(t); break;
        }
    }

    // The key press that opened a panel mustn't also act inside it in the same frame (HandleKeys clears it each frame).
    bool mlFreshPanel;
    bool MlKeysLive => !mlFreshPanel;

    void OpenMlPanel(string which)
    {
        mlFreshPanel = true;
        Sfx.Play("ui");
        panel = which;
        mode = "panel";
        SetPrompt("");
    }

    // A clue earned: kept at once (and the trail sign goes up), with a word about how many are in.
    void NoteMlClue(string key, string toast)
    {
        if (state.Hinted(key)) return;
        state.hinted[key] = true;
        GainXp(15);
        Save();
        Sfx.Play("rare");
        ToastLater($"{toast} ({MrClueCount} of {Data.MarilayaClues.Length} clues: <case>)");
        if (MrStopsDone) ToastLater(MrMet ? "All four stops are done. Meet Aling Sela at the shore model in the town square." : "All four stops are done. Aling Sela, at the harbour, will want to hear about them.");
    }

    // A step within a stop, kept at once.
    void NoteMlStep(string key)
    {
        if (state.Hinted(key)) return;
        state.hinted[key] = true;
        GainXp(4);
        Save();
    }

    /* ---------- The notice at Amihan Village ---------- */
    void ReadSelaNotice()
    {
        bool first = !state.Hinted("mlNotice");
        state.hinted["mlNotice"] = true;
        Talk(new()
        {
            new("Notice", "WANTED: a field partner who fishes and has a boat (or something that swims). To help finish a chart and a trail for the children of Marilaya."),
            new("Notice", "Marilaya is the island shaped like a heart, far out east past Baga. The harbour is on its north-west side, under a hill with a survey stone on it. Ask for Aling Sela."),
        }, () => { if (first) { Save(); Toast("Sela's notice is in your journal (<journal>). Marilaya is far to the east: sail out past Baga.", 6); } });
    }

    /* ---------- Aling Sela ---------- */
    void TalkSela()
    {
        Say S(string t) => new("Aling Sela", t);
        if (!MrMet)
        {
            int already = Data.MarilayaClues.Count(c => c.Key != "mr:legend" && state.Hinted(c.Key));
            var lines = new List<Say>
            {
                S("Magandang araw! A visitor from across the water. I'm Sela; everyone calls me Aling Sela. I measured land for maps for forty years. Now I fish, and I wonder about things."),
                S("Do you know how our island got its heart shape? My lola told it like this. A datu named Batumbakal forbade his daughter, Mutya Maria, to marry Garduke, a poor fisherman."),
                S("So one night the two of them slipped away in Garduke's little boat. A storm caught them far out at sea, and they were never seen again. Some days later, where they'd been lost, an island had risen out of the water: shaped like a heart."),
                S("That's Lola's telling. Other people tell it other ways: the names change, and so do the endings."),
                new("", "(People tell this story about Marinduque, a real island, in many versions. Aling Sela's lola's is the game's own retelling, after versions like the one in Tales from the 7,000 Isles, by Dianne de Las Casas and Zarah C. Gagatiga.)"),
                S("Here's my plan. I want to make a discovery trail for the island's children, from the shore to the top of Bundok Tindig, with a sign at every stop saying what the island itself can tell us. Did it really rise out of the sea?"),
                S("But my field chart isn't finished, and my knees aren't what they were. Will you be my field partner? There are four stops: the rock face in the north-east hills, the cave near it, the hot springs at Tindig's foot, and the tide post off Maniwaya, the islet to the north-east."),
                S("Take my old survey compass while you help. With it, the arrow to your map pin tells you its bearing, the way a surveyor reads it. If we finish the chart, it's yours.")
            };
            if (already > 0) lines.Add(S($"Oh, and you've been looking around already: {already} of the four stops! A good surveyor starts by looking."));
            Talk(lines, () =>
            {
                state.hinted["mr:met"] = true;
                state.hinted["mr:legend"] = true;
                if (Has("survey_compass") == 0) Give("survey_compass");
                Save();
                Sfx.Play("rare");
                Toast("Story 5: the heart that rose. It's on your Case board (<case>) and in your journal (<journal>). The survey compass is in your bag.", 7);
            });
            return;
        }
        if (MrDone)
        {
            Talk(new() { S(AllStoriesDone && state.Hinted("mr:gathered") ? MlSelaLastWords
                : "The chart's inked, and the trail's signs are up from the shore to the summit. The children start walking it next week. Come and fish the harbour with me any time.") },
                () => { if (!state.Hinted("mr:cacheAsked")) OfferCacheHunt(); });
            return;
        }
        if (MrBearingsDone) { OpenPlot(); return; }
        if (MrSorted)
        {
            var next = !state.Hinted("mr:mirror") ? "First, climb the trail from the hot springs to Tindig's top, where Jun is waiting with my heliotrope, and help him set it up."
                : !(state.Hinted("mr:ab") && state.Hinted("mr:ac")) ? "Now go up to the survey stone on the hill above the harbour (A) and sight two things: the striped target on Baltasar (B), and Jun's flash on Tindig (C)."
                : "One more bearing: sail to Baltasar, the smallest of the Tres Reyes to the south-west, and sight Jun's flash from the mark there (B).";
            Talk(new()
            {
                S("The chart now. We know two places exactly: the survey stone here (A), and the mark on Baltasar (B). Sight Tindig's top (C) from both, and where the two lines cross, that's where the summit goes on the chart."),
                S(next),
                S("You need daylight for sighting, and a clear sky for the mirror's flash. If it's cloudy or dark, wait it out.")
            });
            return;
        }
        if (MrStopsDone)
        {
            Talk(new() { S("Every stop's done! Come to the shore model in the town square, down the lane south of here. Let's see what moves a shore, and how fast.") });
            return;
        }
        var left = new List<string>();
        if (!state.Hinted("mr:layers")) left.Add("the rock face in the north-east hills (brush it clean)");
        if (!state.Hinted("mr:karst")) left.Add("the cave near it (follow the water)");
        if (!state.Hinted("mr:springs")) left.Add("the hot springs at Tindig's west foot (Lorna will show you the gauges)");
        if (!state.Hinted("mr:tide")) left.Add(state.Hinted("mr:low") || state.Hinted("mr:high") ? $"the tide post again, at {(state.Hinted("mr:low") ? "high" : "low")} tide" : "the tide post off Maniwaya, at low tide and at high tide");
        Talk(new()
        {
            S($"How's the notebook? Still to do: {AndList(left)}."),
            S("A good surveyor checks things twice and writes everything down. Your map (<map>) has every stop marked once you've been near.")
        });
    }

    static string AndList(List<string> l) => l.Count == 0 ? "" : l.Count == 1 ? l[0] : string.Join(", ", l.Take(l.Count - 1)) + " and " + l[^1];

    /* ---------- The springs (Lorna, the gauges, the night's record, the dryer) ---------- */
    // The readings: the spring outlet runs near the warmest the dryer project reported at Malbog (about 54-58 C), the
    // stream about as warm as the day.
    const int SpringC = 56, StreamC = 27;

    void TalkLorna()
    {
        Say L(string t) => new("Lorna", t);
        if (state.Hinted("mr:springs"))
        {
            Talk(new()
            {
                L(LornaDryerLine),
                L("The water stays in its pipes, and the food never touches it. Heat goes from the water to the coil, and from the coil to the air.")
            });
            return;
        }
        Talk(new()
        {
            L("Hello! I look after the springs and the dryer. Don't touch the water: it comes out hot enough to scald. Stay behind the rail and read the gauges."),
            L("There's one on the spring's outlet, and one on the stream over there for comparison. Read both, then come and see my record from last night at the dryer.")
        });
    }

    void ReadGauge(bool spring)
    {
        string key = spring ? "mr:gauge1" : "mr:gauge2";
        bool first = !state.Hinted(key);
        NoteMlStep(key);
        Sfx.Play("blip");
        Talk(new() { new("You", spring ? $"The hot spring's outlet: {SpringC} degrees C. Steam, and a smell of rotten eggs: that's hydrogen sulfide gas."
            : $"The stream off Tindig: {StreamC} degrees C. About as warm as the day.") },
            () => { if (first && state.Hinted("mr:gauge1") && state.Hinted("mr:gauge2")) Toast("Both gauges read. Lorna's night record is at the dryer.", 4); });
    }

    string DryerLabel() => state.Hinted("mr:springs") ? (DryerReady ? "Take the dried fish" : DryerLoad > 0 ? "The dryer (fish drying)" : "Dry fish in the dryer") : "Look at the dryer";

    void UseDryer()
    {
        if (!state.Hinted("mr:springs"))
        {
            if (!(state.Hinted("mr:gauge1") && state.Hinted("mr:gauge2"))) { Toast("Lorna says: read the two gauges first, the spring's and the stream's.", 4); return; }
            OpenSprings();
            return;
        }
        DryerService();
    }

    /* ---------- The tide post ---------- */
    const float TideLowMark = -0.35f, TideHighMark = 0.35f;
    // Which tide you still need to read at the post (null: both read).
    string TideWaitFor() => state.Hinted("mr:tide") ? null : !state.Hinted("mr:low") ? "low" : !state.Hinted("mr:high") ? "high" : null;

    // Waiting on the beach for the tide you still need: the clock runs forward the ordinary way (Advance), to the next
    // time the water's past the mark. It says how long first, and costs food like a rest would.
    void WaitForTide()
    {
        string want = TideWaitFor();
        if (want == null) return;
        float now = TideNow, m = 0;
        bool found = false;
        for (; m <= 1500; m += 5)
            if (want == "low" ? TideAt(now + m) <= TideLowMark - 0.05f : TideAt(now + m) >= TideHighMark + 0.05f) { found = true; break; }
        if (!found) { Toast("The water isn't turning yet. Come back later.", 4); return; }
        // On to the turn itself: low (or high) water, not just past the mark (Codex).
        while (m < 1500 && (want == "low" ? TideAt(now + m + 2) < TideAt(now + m) : TideAt(now + m + 2) > TideAt(now + m))) m += 2;
        if (m <= 0) { OpenTidePost(); return; }
        int food = (int)Math.Min(10, MathF.Ceiling(m / 60));
        FadeThrough(() => { Advance(m, quiet: true); state.food = Math.Max(0, state.food - food); Save(); }, () =>
        {
            Toast($"You wait on the sand by the post ({(int)(m / 60)} h {(int)(m % 60)} min, and some of your food). It's {want} tide now ({ClockText(state.clock, 10)}).", 5);
        });
    }

    /* ---------- The summit ---------- */
    void TalkJun()
    {
        Say J(string t) => new("Jun", t);
        if (!state.Hinted("mr:mirror")) { UseCairn(); return; }
        Talk(new() { J(MirrorFlashing ? "I'm keeping the mirror pointed at the stones. Lola says you'll see the flash from a long way off." : Darkness >= 0.3f
            ? "No sun, no flash. I'll start again in the morning." : "Clouds! The mirror needs sunshine to flash. I'll keep watching for a gap.") });
    }

    void UseCairn()
    {
        if (!MrSorted || state.Hinted("mr:mirror"))
        {
            Talk(new() { new("You", MrDone ? "The summit cairn, with the trail's last sign beside it. The whole island's below: the harbour, the town, the hills, the islets." : "A cairn of stones on Tindig's top. On a clear day you can see the whole island from here.") });
            return;
        }
        Say J(string t) => new("Jun", t);
        Talk(new()
        {
            J("You made it up! Lola sent her heliotrope: a mirror on a stand. Surveyors used them to throw a flash of sunlight to a station far away, so it could sight this exact spot."),
            J("They used them here in the Philippines too, more than a hundred years ago, for sightings across whole gulfs. Help me level the stand... there."),
            J("I'll stay up here and keep it pointed at the stones while the sun's out. Now you sight me from the survey stone (A), and from Baltasar (B)!")
        }, () =>
        {
            NoteMlStep("mr:mirror");
            Sfx.Play("rare");
            Toast("The heliotrope is up. Sight it from the survey stone above the harbour (A), then from Baltasar's mark (B).", 6);
        });
    }

    /* ---------- The shore model ---------- */
    void UseModel()
    {
        if (!MrStopsDone || !MrMet)
        {
            Talk(new() { new("You", "A shore model on a table: a tray of sand and water over a rock base, with three handles. A card says: \"Aling Sela's, for the trail. Ask her before you play with it.\"") });
            return;
        }
        OpenModel();
    }

    /* ---------- The survey stations ---------- */
    void UseStation(string which)
    {
        if (!MrSorted || MrDone)
        {
            Talk(new() { new("You", which == "A"
                ? (MrDone ? "The survey stone, with the little flag beside it. The whole chart is measured from here, and from the mark on Baltasar." : "A squat concrete stone with a brass disc in its top: a survey station. Its position was measured with great care, and maps are measured from it. Nobody may move it.")
                : "A survey mark on Baltasar, with a striped target pole beside it, so it can be sighted from far away.") });
            return;
        }
        if (!SightingLight) { Toast("Too dark to sight anything. Come back by daylight, or wait here (<alt>).", 4); return; }
        if (which == "B" && !(state.Hinted("mr:ab") && state.Hinted("mr:ac"))) { Toast("Aling Sela said to start from the survey stone above the harbour (A).", 4); return; }
        if (which == "B" && state.Hinted("mr:bc")) { Toast("Bearing from Baltasar recorded. Take it to Aling Sela at the harbour.", 4); return; }
        if (which == "A" && state.Hinted("mr:ab") && state.Hinted("mr:ac")) { Toast("Both bearings from the stone are recorded. Next: Baltasar's mark (B), by boat.", 4); return; }
        OpenScope(which);
    }

    /* ---------- The end of the survey ---------- */
    // The bearing from one point to another, in degrees clockwise from north (up on the chart).
    static float BearingTo(float x0, float y0, float x1, float y1)
    {
        float b = MathF.Atan2(x1 - x0, -(y1 - y0)) * 180 / MathF.PI;
        return (b + 360) % 360;
    }
    static float AngleDiff(float a, float b) => (a - b + 540) % 360 - 180;

    // Where two lines of sight cross: from A at bearing a, and from B at bearing b (null if they're parallel).
    static (float x, float y)? Intersect(float ax, float ay, float a, float bx, float by, float b)
    {
        float ra = a * MathF.PI / 180, rb = b * MathF.PI / 180;
        float dax = MathF.Sin(ra), day = -MathF.Cos(ra), dbx = MathF.Sin(rb), dby = -MathF.Cos(rb);
        float den = dax * dby - day * dbx;
        if (MathF.Abs(den) < 1e-4f) return null;
        float t = ((bx - ax) * dby - (by - ay) * dbx) / den;
        return (ax + dax * t, ay + day * t);
    }

    // The survey's end: the win is kept before any words (commit first), then Sela, then the card.
    void FinishMarilaya()
    {
        if (MrDone) return;
        state.hinted["mr:survey"] = true;
        state.hinted["mr:done"] = true;
        state.gifts["mr_done"] = state.day;
        state.coins += 200;
        if (Has("survey_compass") == 0) Give("survey_compass");
        GainXp(60);
        mapTexDirty = true;
        Save();
        Say S(string t) => new("Aling Sela", t);
        Talk(new()
        {
            S("There: two bearings from two stations we already know, and where they cross is the summit, as near as our sights can tell. That's triangulation. It fixes where a place is on the chart, not how high it is."),
            S(SelaDatumLine),
            S("So, was Lola right? The island did rise from the sea. Not in one night: a volcano built Tindig, and old reef was lifted up into the hills, mostly slowly, now and then all at once in an earthquake."),
            S("Lola's story kept something true: the sea gave this island up. Now the children can walk the trail and read it for themselves."),
            S("The compass is yours now. And two hundred coins, for doing a surveyor's job properly.")
        }, () => { catchOpenedAt = Raylib.GetTime(); mlCardKind = "done"; mode = "marilayacard"; SetPrompt(""); });
    }

    string mlCardKind = "done";
    const string LornaDryerLine = "The dryer's running on the spring's heat. Bring me raw fish and I'll dry them for you: up to three at a time, ready in two hours, rain or shine.";
    const string SelaDatumLine = "And the stone is what it all hangs from: a datum, an agreed starting point and direction, so everyone's maps fit together. On the real Marinduque, Station Balanacan became the origin of the Luzon Datum of 1911, and its direction was fixed by sighting a mark on Baltasar, in the Tres Reyes.";
    static readonly string[] MlOtherWords = { LornaDryerLine, SelaDatumLine };

    // What's said once everything's done (checked: nothing claims more than the stories measure).
    const string MlSelaLastWords = "Every story told. Come and fish the harbour with me any time.";
    const string MlGatherToast = "Thank you for playing Fesh. All five stories are told, and the sea is still full of fish. Keep exploring!";
    const string MlGatherAbout = "Tomas's Halcyon, Bakunawa and the moon, the restless sea, Baga beneath the clouds, and the heart that rose: all five stories told.";
    // What a crab pot brings up on an island (Marilaya's are Amihan's): one answer for hauling and for the island guide.
    static CommonFish[] PotCatchFor(string biome) => Data.PotCatch.GetValueOrDefault(biome) ?? (biome == "marilaya" ? Data.PotCatch["amihan"] : null);

    // Where you wake after fainting on Marilaya (Cave.Faint): the boat only if you have one.
    string MlWakeWords() => "Aling Sela found you and brought you up to her house above the harbour."
        + (Has("boat") > 0 ? " Your boat is tied up at the jetty." : "") + " Eat and rest before going out again.";
    static IEnumerable<string> MarilayaEndingWords() => new[] { MlSelaLastWords, MlGatherToast, MlGatherAbout };

    void CloseMarilayaCard()
    {
        if (mode != "marilayacard" || Raylib.GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        if (mlCardKind == "done")
            Toast(AllStoriesDone ? "Case closed: the heart that rose. Every story is done: the storytellers want to meet you at Marilaya's harbour."
                : "Case closed: the heart that rose. 200 coins, and Aling Sela's survey compass is yours.", 7);
        else Toast(MlGatherToast, 8);
    }

    void DrawMarilayaCard()
    {
        if (mlCardKind == "done")
        {
            if (GoldCard("marilaya", "The heart that rose", "Case closed",
                "The rock face, the cave, the springs and the tide post told it: Marilaya rose from the sea, built by a volcano, its old reef lifted into the hills. Now it's on the chart, and the children have a trail to the top.",
                "Triangulation: two bearings from two known points fix a third. A datum is the agreed starting point that lets everyone's maps fit together.", "Salamat po!")) CloseMarilayaCard();
        }
        else if (GoldCard("marilaya", "Five stories, one chart", "The storytellers",
            MlGatherAbout,
            "The people and places are fictional, inspired by the Philippines and the stories people tell there. Thank you for listening to them.", "Keep exploring")) CloseMarilayaCard();
    }

    // A save made in the middle of a step's words (the window closed) has every part of a clue but not the clue: make it
    // whole on loading (StartGame). Nothing here says anything; the toasts wait for play.
    void ReconcileMarilaya()
    {
        if (!state.Hinted("mr:fossil") && MlFossilsFound >= MlFossilsNeeded) state.hinted["mr:fossil"] = true;
        CheckKarst();
        CheckPools();
        if (state.Hinted("mr:low") && state.Hinted("mr:high") && !state.Hinted("mr:tide")) NoteMlClue("mr:tide", "Clue: the sandbar that comes and goes.");
        if (!MrSorted && Enumerable.Range(0, 3).All(i => state.Hinted($"mr:obs:{i}"))) state.hinted["mr:theory"] = true;
    }

    /* ---------- The chart (Chart.cs) ---------- */
    void MarilayaChartMarks(Action<string, float, float, List<string>, int> place)
    {
        if (!state.Hinted("marilaya")) return;
        place("Marilaya harbour", MlJettyX, MlJettyY - 12, new() { "Marilaya harbour", Has("boat") > 0 ? "E sails from here; F takes the helm" : "You'll need a boat" }, 3);
        if (!Charted("marilaya")) return;
        place("Aling Sela", SelaX, SelaY, new() { "Aling Sela", MrMet ? "A retired surveyor who fishes" : "She knows a story about how the island rose" }, 3);
        place("Survey stone (A)", DatumX, DatumY, new() { "The survey stone", "A survey station: maps are measured from it" }, 2);
        place("River town", MlPlace("plaza").x, MlPlace("plaza").y, new() { "River town", "The shore model is in the square", "Mang Iyo teaches the kalutang here" }, 2);
        place("Rock face", MlPlace("cliff").x, MlPlace("cliff").y, new() { "The rock face", state.Hinted("mr:layers") ? "Done: the sea in the stone" : "Brush it clean and read its layers" }, 2);
        place("Cave", MlPlace("cave").x, MlPlace("cave").y, new() { "The cave", state.Hinted("mr:karst") ? "Done: water's patient work" : "Follow the water through it" }, 2);
        place("Hot springs", MlPlace("springs").x, MlPlace("springs").y, new() { "The hot springs", state.Hinted("mr:springs") ? "Done: heat under Tindig; the dryer works" : "Read the gauges with Lorna" }, 2);
        place("Bundok Tindig", TindigX, TindigY, new() { "Bundok Tindig", "A forested volcano", "The trail starts by the hot springs" }, 2);
        place("Butterflies", MlPlace("garden").x, MlPlace("garden").y, new() { "Aling Nita's butterfly nursery" }, 3);
        if (Charted("marilaya:north")) place("Tide post", tidePostX, tidePostY, new() { "The tide post", state.Hinted("mr:tide") ? "Done: the sandbar that comes and goes" : "Read it at low tide and at high tide", TideLine() }, 2);
        if (Charted("marilaya:tresreyes")) place("Baltasar mark (B)", BaltasarX, BaltasarY, new() { "Baltasar's survey mark", "The far station, with a striped target" }, 2);
        if (state.Hinted("mr:cacheAsked") && !state.Hinted("mr:cache")) place("Cache hunt start", DatumX, DatumY + 8, new() { "Sela's cache", $"From the stone: bearing {CacheBearing:0}, about {CacheMetres} m" }, 4);
    }

    /* ---------- The guide (Guide.cs) ---------- */
    // Finding the island (once you've a boat or Tidemane and know Amihan, or read Sela's notice), meeting Aling Sela, the
    // next stop (any order works; the guide suggests one), the shore model, the survey, and the gathering.
    void MarilayaGoals(List<Goal> list)
    {
        if (!state.Hinted("marilaya"))
        {
            if ((Has("boat") > 0 || state.tamed) && (state.Hinted("amihan") || state.Hinted("habagat")) || state.Hinted("mlNotice"))
                list.Add(At(NewGoal("explore:marilaya", "explore", "Find the heart-shaped island",
                    "An island shaped like a heart lies far out east, past Baga. Its harbour is on the north-west side, under a hill with a survey stone."),
                    MlJettyX, MlJettyY, "Marilaya harbour"));
            return;
        }
        if (GatherDue)
        {
            var g = NewGoal("mr:gather", "request", "The storytellers are waiting", "Everyone from the five stories has come to Marilaya. Talk to Aling Sela by her house above the harbour.");
            g.Ready = true;
            list.Add(At(g, SelaX, SelaY + 10, "Aling Sela"));
            return;
        }
        if (MrDone)
        {
            if (state.Hinted("mr:cacheAsked") && !state.Hinted("mr:cache"))
                list.Add(At(NewGoal("mr:cache", "request", "Sela's cache", $"From the survey stone, walk on a bearing of {CacheBearing:0} degrees for about {CacheMetres} m, and dig. Your compass shows your bearing from the stone."), DatumX, DatumY + 8, "Survey stone"));
            return;
        }
        var c = NewGoal("mr:case", "request", $"The heart that rose ({MrClueCount} of {Data.MarilayaClues.Length} clues)", "");
        if (!MrMet && MrClueCount == 0) { c.Title = "Meet Aling Sela"; c.Text = "She lives above Marilaya's harbour, and knows a story about how the island rose."; At(c, SelaX, SelaY + 10, "Aling Sela"); }
        else if (MrBearingsDone) { c.Title = "Plot the chart"; c.Text = "All three bearings are in. Plot them with Aling Sela at her house above the harbour."; c.Ready = true; At(c, SelaX, SelaY + 10, "Aling Sela"); }
        else if (MrSorted)
        {
            c.Title = "The survey";
            if (!state.Hinted("mr:mirror")) { c.Text = "Climb the trail from the hot springs to Tindig's top, and set up the heliotrope with Jun."; At(c, CairnAt.x, CairnAt.y + 8, "Tindig's top"); }
            else if (!(state.Hinted("mr:ab") && state.Hinted("mr:ac"))) { c.Text = "At the survey stone above the harbour (A), sight Baltasar's target (B) and Jun's flash on Tindig (C). By daylight; the flash needs sun."; At(c, DatumX, DatumY + 8, "Survey stone"); }
            else { c.Text = "Sail to Baltasar, the smallest of the Tres Reyes, and sight Jun's flash from the mark there (B)."; c.Region = "marilaya:tresreyes"; At(c, BaltasarX, BaltasarY + 8, "Baltasar's mark"); }
        }
        else if (MrStopsDone)
        {
            c.Title = MrMet ? "The shore model" : "Meet Aling Sela";
            c.Text = MrMet ? "Every stop's done. Meet Aling Sela at the shore model in the town square." : "You've done all four stops! Tell Aling Sela, above the harbour.";
            c.Ready = true;
            if (MrMet) At(c, ModelAt.x, ModelAt.y + 8, "Shore model"); else At(c, SelaX, SelaY + 10, "Aling Sela");
        }
        else if (!state.Hinted("mr:layers")) { c.Text = "Brush the rock face in the north-east hills clean, and read its layers."; At(c, MlPlace("cliff").x, MlPlace("cliff").y + 10, "The rock face"); }
        else if (!state.Hinted("mr:karst"))
        {
            // Inside the cave the guide goes from one marked spot to the next, then the model by the door.
            c.Text = "Follow the water through the cave in the north-east hills: look at the three marked spots, and run the model by the door.";
            if (!state.Hinted("mr:cave1")) At(c, CaveDripX, CaveDripY + 28, "The dripping stalactite", MlCaveKey);
            else if (!state.Hinted("mr:cave2")) At(c, CaveShellX, CaveShellY + 32, "The cave wall", MlCaveKey);
            else if (!state.Hinted("mr:cave3")) At(c, CaveRopeX - 10, 80, "The bats", MlCaveKey);
            else At(c, CaveModelX, CaveModelY + 10, "The cave model", MlCaveKey);
        }
        else if (!state.Hinted("mr:springs"))
        {
            c.Text = !(state.Hinted("mr:gauge1") && state.Hinted("mr:gauge2")) ? "At the hot springs at Tindig's west foot, read the spring's gauge and the stream's." : "Look at Lorna's night record at the dryer, by the springs.";
            if (!state.Hinted("mr:gauge1")) At(c, SpringGaugeAt.x, SpringGaugeAt.y + 8, "Spring gauge");
            else if (!state.Hinted("mr:gauge2")) At(c, StreamGaugeAt.x, StreamGaugeAt.y + 8, "Stream gauge");
            else At(c, DryerAt.x, DryerAt.y + 10, "The dryer");
        }
        else
        {
            string want = TideWaitFor() ?? "low";
            c.Text = $"Read the tide post off Maniwaya at low tide and at high tide. Still to see: {want} tide (or wait there with <alt>).";
            c.Region = "marilaya:north";
            var (px, py) = PostReadSpot();
            At(c, px, py, "Tide post");
        }
        list.Add(c);
    }

    /* ---------- The storytellers gather (once all five stories are done) ---------- */
    static readonly (string name, float dx, float dy, string shirt)[] GatherSpots =
    {
        ("Tomas", -30, 4, "#5d6e7e"), ("Pip", -18, 6, "#e04b3a"), ("Lola Pacing", -6, 7, "#8a5fb5"), ("Lira", 6, 7, "#e6a843"),
        ("Niko", 18, 6, "#367caa"), ("Ma'am Isay", 30, 4, "#d9a43a"), ("Manay Mila", -12, 17, "#c0392b"), ("Ben", 12, 17, "#2a8a8a")
    };

    void StartGathering()
    {
        if (!GatherDue) return;
        mlGather = new MlGather();
        Say L(string who, string t) => new(who, t);
        Talk(new()
        {
            L("Aling Sela", "They came! Every one of them, by boat and by banca, to hear the end of the stories. Look at them all on my little jetty."),
            L("Tomas", "Mara set the creatures free, and you found every one of them. The Halcyon's story is told now. Thank you."),
            L("Lola Pacing", "And the moon still rises over Parola, full and round. The islands made their noise, and Bakunawa gave it back."),
            L("Lira", "When the ground shook, Amihan Village knew what to do. Shake, drop, roar: any one of them, and you go up."),
            L("Manay Mila", "My lola's story kept Mayon's name, and Ben's readings told us when to leave. Between them, nobody had to be brave."),
            L("Ben", "Baga is still a volcano. The danger zone stays closed, and the station keeps reading. That's what keeps everyone safe."),
            L("Ma'am Isay", "Stories and science, side by side. My class wants to hear every one of them again."),
            L("Niko", "And Pip wants to know who's buying the next round of fish."),
            L("Pip", "Fresh fish, fair prices! Same as always. Come back to the stall."),
            L("Aling Sela", "Lola's story said the island rose from the sea, and it did. Every story here kept something true. Go on home with Tomas: the sea's still full of fish."),
        }, () =>
        {
            state.hinted["mr:gathered"] = true;
            Save();
            FadeThrough(() =>
            {
                mlGather = null;
                LeaveMount();
                state.aboard = false;
                if (Has("boat") > 0) { state.boatAt = "saltmere"; state.boatX = state.boatY = 0; }
                LoadScene("world");
                player.X = SaltJettyX - 4; player.Y = SaltJettyY + 1; player.Face = "left";
                lastBiome = 0;
                Save();
            }, () => { catchOpenedAt = Raylib.GetTime(); mlCardKind = "gather"; mode = "marilayacard"; SetPrompt(""); });
        });
    }

    // While they're gathered, the storytellers stand round Aling Sela in front of her house (looks only).
    void AddGathering(List<(float y, Action draw)> list)
    {
        float bx = SelaX + 34, by = SelaY + 14;
        list.Add((by - 8, () => DrawSela(pix, (int)bx, (int)by - 8, "down", 0)));
        foreach (var (name, dx, dy, shirt) in GatherSpots)
        {
            int x = (int)(bx + dx), y = (int)(by + dy);
            string face = dx < 0 ? "right" : "left";
            list.Add((y, () =>
            {
                if (name == "Tomas") LookData.DrawFigure(pix, "#d9a066", "#c9c4bc", "#5d6e7e", "#3f4a5a", false, 3, "#3f4a5a", x, y, face, 0);
                else if (name == "Manay Mila") DrawMila(pix, x, y, face, 0);
                else if (name == "Ben") DrawBen(pix, x, y, face, 0);
                else LookData.DrawFigure(pix, "#c68a5a", name == "Lola Pacing" ? "#e8e8e8" : "#2b1d14", shirt, "#3f4a5a", name is "Lira" or "Lola Pacing" or "Ma'am Isay", 0, null, x, y, face, 0);
            }));
        }
    }
}
