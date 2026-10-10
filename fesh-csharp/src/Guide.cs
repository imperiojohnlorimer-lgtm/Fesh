using Raylib_cs;

namespace Fesh;

// The guide (1.16), for players who skim the conversations: a card on the HUD with the goal you're following, an arrow
// to where it happens, and a journal of every goal open right now with a "Getting started" checklist.
// Nothing here moves the story on. Goals are worked out from the save every frame (Goals), so whatever order you do
// things in, the card shows the next step that's still open, and you can ignore it entirely. You follow one goal at a
// time: state.track, or "" for automatic (the story, then a request that's ready to hand in, then the other requests,
// then somewhere new to explore).
sealed class Goal
{
    public string Id, Group, Title, Text, Place = "";   // Group: "story", "request", "daily" or "explore"
    public string Scene = "world";                        // where X, Y are: "world", "house:tomas", or "cave"/"sea" (Spot says which)
    public string Spot;                                   // a cave or sea fishing spot, which has no fixed place
    public string Region;                                 // the island it's on, when its spot says (null: from X, Y)
    public string Ore;                                    // a cave goal to mine this ore ("copper", "iron"): no spot (Chapters.cs)
    public float X, Y;
    public bool HasPoint, Ready;                          // Ready: everything's in hand, it only needs taking there
}

// Where the arrow points right now, and what to do first when that isn't the goal itself (go outside, sail).
sealed record Waypoint(float X, float Y, string Place, string How);

partial class Game
{
    List<Goal> goals = new();         // this frame's goals (UpdateGuide), so the card, the arrow and the journal agree
    string trackBeforeSea;            // what you were following when the evacuation began (RestlessSea.cs)
    Goal tracked;                     // the goal the card and the arrow follow
    Waypoint guideWay;
    string lastTrackedId, lastTrackedTitle;
    string goalDoneTitle;             // a followed goal that just finished: the card says so for a moment
    float goalDoneT;
    HashSet<string> requestsKnown;    // requests already announced (null until the first frame of a game)
    int journalPage;
    float guideCardBottom;            // where the HUD card ended (edge arrows keep below it)
    (string spot, float px, float py, float x, float y) seaPointCache = ("", 0, 0, 0, 0);
    const float CardX = 13, CardY = 99, CardW = 372;
    const int JournalRows = 5;

    bool GuideOn => Settings.Data.guide;
    // The card and the arrow: in ordinary play only, never over a fight, the eclipse or a race (they have their own).
    bool GuideShown => GuideOn && mode is "play" or "build" && boss == null && guardian == null && tremor == null && evac?.Phase != "quake" && eclipse == null && race == null && !raceArmed;

    /* ---------- Working out the goals ---------- */
    List<Goal> Goals()
    {
        var list = new List<Goal>();
        StoryGoal(list);
        RequestGoals(list);
        SeaGoals(list);
        MagayonGoals(list);
        SchoolGoals(list);
        ExploreGoals(list);
        return list;
    }

    static Goal NewGoal(string id, string group, string title, string text) => new() { Id = id, Group = group, Title = title, Text = text };

    static Goal At(Goal g, float x, float y, string place, string scene = "world")
    {
        g.X = x; g.Y = y; g.Place = place; g.Scene = scene; g.HasPoint = true;
        return g;
    }

    // Tomas at his camp, or asleep in his hut at night (waking him works: TalkTomasWithRequests).
    Goal AtTomas(Goal g)
    {
        if (!TomasInBed) return At(g, tomasX, tomasY, "Tomas");
        g.Text += " He's asleep in his hut at this hour: wake him by his bed.";
        return At(g, TomasBedX, TomasBedY, "Tomas", "house:tomas");
    }

    static Goal AtFire(Goal g) => At(g, FireX, FireY, "Campfire");

    Goal AtSpot(Goal g, string spotId)
    {
        var s = Data.SpotById[spotId];
        g.Spot = spotId;
        if (s.Scene is "sea" or "cave") { g.Scene = s.Scene; g.Place = s.Label; g.HasPoint = true; return g; }
        At(g, s.X, s.Y, SpotKnown(s) ? s.Label : "???");
        if (s.Biome is "amihan" or "habagat") g.Region = SpotRegion(s);
        return g;
    }

    // The Saltmere mystery, one step at a time. The titles give the hint, not the creature's name.
    void StoryGoal(List<Goal> list)
    {
        if (state.flags.ended) return;
        bool C(string id) => state.Caught(id);
        Goal G(string step, string title, string text) => NewGoal("story:" + step, "story", title, text);
        if (!state.flags.metTomas) { list.Add(AtTomas(G("tomas", "Meet Tomas", "Tomas keeps the camp on Saltmere. Walk up to him and press <act> to talk."))); return; }
        if (state.commons.Values.Sum() == 0 && state.caught.Count == 0)
        {
            list.Add(AtSpot(G("firstfish", "Catch your first fish",
                "At the lagoon's edge, hold <act> and let go to cast. When the float dips, press <act>, then hold <act> to keep the fish inside the green bar."), "lagoon"));
            return;
        }
        if (!C("glowgill"))
            list.Add(Night ? AtSpot(G("glowgill", "Find the glow in the lagoon", "Something glows in the lagoon after dark. Fish there tonight and keep casting until it bites."), "lagoon")
                : AtFire(G("glowgill", "Wait for night", "Something glows in the lagoon after dark. Rest by Tomas's campfire (<act>) to skip ahead to night, then fish the lagoon.")));
        // Between the creatures, the chapters (Chapters.cs): tools for the fallen rocks, a home and copper for the
        // wreck's hatch, iron for the dock, and Mara's recording for Tomas.
        else if (!C("tidecrawler"))
            list.Add(!RocksCleared ? RocksGoal()
                : Night ? AtFire(G("tidecrawler", "Wait for morning", "Whatever clicks on the rocky shore only comes out in daylight. Rest by the campfire (<act>) until morning."))
                : AtSpot(G("tidecrawler", "Fish off the rocky shore", "Something clicks in the tide pools on Saltmere's north shore in daylight. Cast there and keep trying."), "rocks"));
        else if (!C("hollow_eel"))
            list.Add(!HatchOpen ? HatchGoal() : AtSpot(G("hollow_eel", "Fish by the old wreck", "The hatch is open, and something slipped out of it into the water. Cast beside the old wreck off Saltmere's southwest shore."), "wreck"));
        else if (!state.flags.dockFixed)
        {
            if (DockGoal() is Goal dock) list.Add(dock);
        }
        else if (!C("mirror_ray"))
            list.Add(Night ? AtFire(G("mirror_ray", "Wait for morning", "Something wide and silver glides past the end of the old dock, but only in daylight. Rest by the campfire (<act>) until morning."))
                : AtSpot(G("mirror_ray", "Fish off the end of the dock", "Tomas mended the old dock east of camp. Walk to its end and cast into the deep water by day."), "deep"));
        else if (!RecordingHeard)
            list.Add(AtTomas(G("recording", "Play Tomas the recording", "Mara's waterproof recorder still crackles. Tomas needs to hear what she said.")));
        else if (!C("abyssal"))
            list.Add(Night ? AtSpot(G("abyssal", "The end of the dock, at night", "The deepest things only rise at night. Fish off the end of the old dock after dark."), "deep")
                : AtFire(G("abyssal", "Wait for night", "Whatever is left down there only rises at night. Rest by the campfire (<act>) until dark, then fish off the end of the dock.")));
    }

    // People waiting on you for something. Only ones they've already asked for: nothing here gives a quest away early.
    void RequestGoals(List<Goal> list)
    {
        Goal R(string id, string title, string text, string group = "request") => NewGoal(id, group, title, text);
        if (state.req is Req r && state.flags.metTomas)
        {
            var g = R("req:tomas", $"Tomas wants {Items.Amount(r.item, r.count)}", "");
            if (CanHandIn) { g.Text = $"You have them. Bring them to Tomas for {RewardText(r)}."; g.Ready = true; AtTomas(g); }
            else g.Text = $"{Source(g, r.item)} You have {Has(r.item)} of {r.count}.";
            list.Add(g);
        }
        if (state.Hinted("amihan"))
        {
            if (!state.Hinted("niko_request") && state.Hinted("nikoAsked"))
                list.Add(Hand(R("niko:bangus", "Bring Niko 2 bangus", ""), "bangus", 2, "niko", "Niko pays 90 coins and cut bait for them."));
            else if (state.Hinted("nikoAsohosAsked") && !state.Hinted("niko_asohos"))
                list.Add(Hand(R("niko:asohos", $"Bring Niko {NikoAsohos} asohos", ""), "asohos", NikoAsohos, "niko", "Niko pays 70 coins and four crickets for them."));
            if (state.Hinted("liraSupperAsked") && !state.Hinted("rondalla"))
            {
                var g = R("lira:supper", "Cook supper for Lira", "");
                if (SupperDish() is string dish) { g.Text = $"Bring Lira the {Items.ById[dish].Name.ToLowerInvariant()} for the village supper."; g.Ready = true; AtIslander(g, "lira"); }
                else g.Text = "Cook ginataang isda (a fish and a coconut) or sinigang na isda (2 fish and 2 calamansi) at a cooking stove, then bring it to Lira in Amihan Village.";
                list.Add(g);
            }
            if (state.Hinted("guso") && !state.Hinted("gusoCoop"))
            {
                var g = R("maya:coop", $"Maya's co-op: {GusoHandIn} dried guso", "");
                if (CanGiveMayaGuso) { g.Text = $"Bring Maya the {GusoHandIn} dried guso."; g.Ready = true; AtIslander(g, "maya"); }
                else
                {
                    g.Text = $"Tie cuttings on your lines in the karst lagoon (face a line, <act>), harvest on the second morning, dry the guso on a rack in the sun, then bring {GusoHandIn} to Maya. You have {Has("dried_guso")}.";
                    At(g, GusoFarmX, GusoFarmY, "Seaweed farm");
                }
                list.Add(g);
            }
            if (state.Hinted("fireflies") && !state.Hinted("talaReward"))
            {
                int seen = BakawanKinds.Keys.Count(k => state.sightings.GetValueOrDefault(k) > 0);
                var g = R("tala:list", $"Tala's list: {seen} of {BakawanKinds.Count} seen", "");
                if (TalaListDone) { g.Text = "You've seen everything on Tala's list. Go and tell her."; g.Ready = true; AtIslander(g, "tala"); }
                else
                {
                    var missing = BakawanKinds.Where(k => state.sightings.GetValueOrDefault(k.Key) == 0).Select(k => LowerName(k.Value.name)).ToList();
                    g.Text = $"Still to see on Bakawan: {string.Join(", ", missing)}. The alitaptap shine in the pagatpat trees on clear nights; the water glows when the moon is small.";
                    if (Night && state.sightings.GetValueOrDefault("alitaptap") == 0) At(g, FireflyTrees[1].x, FireflyTrees[1].y, "Alitaptap trees");
                    else AtIslander(g, "tala");
                }
                list.Add(g);
            }
            if (state.Hinted("metJoy") && !state.Hinted("sanctuaryReward"))
            {
                int seen = SeaKinds.Keys.Count(k => state.sightings.GetValueOrDefault(k) > 0);
                var g = R("joy:list", $"Bantay Joy's list: {seen} of {SeaKinds.Count} seen", "");
                if (seen == SeaKinds.Count) { g.Text = "You've watched every animal in the sanctuary. Go and tell Bantay Joy."; g.Ready = true; At(g, JoyX, JoyY, "Bantay Joy"); }
                else
                {
                    var missing = SeaKinds.Where(k => state.sightings.GetValueOrDefault(k.Key) == 0).Select(k => LowerName(k.Value.name)).ToList();
                    g.Text = $"Watch the sanctuary's animals from your boat, inside the buoys (<act>). Still to see: {string.Join(", ", missing)}.";
                    At(g, SanctCX * T, SanctCY * T, "Marine sanctuary");
                }
                list.Add(g);
            }
        }
        if (state.Hinted("habagat"))
        {
            if (state.Hinted("metCelso") && state.parola < 3)
            {
                var g = R("celso:lamp", $"Mend the Parola lighthouse ({state.parola} of 3)", "");
                var stage = ParolaStages[state.parola];
                if (ParolaReady) { g.Text = $"Bring Tatay Celso the {ParolaNeeds} to {stage.what}."; g.Ready = true; AtHabagat(g, "celso"); }
                else
                {
                    g.Text = $"Tatay Celso needs {ParolaNeeds} to {stage.what}. You have {string.Join(", ", stage.needs.Select(p => $"{Has(p.id)} {Items.ById[p.id].Name.ToLowerInvariant()}"))}.";
                    // Bars and crystal are on Pip's stall.
                    if (stage.needs.Any(p => Has(p.id) < p.n && ShopStock().Any(s => s.id == p.id))) { g.Text += " Pip sells what you're missing."; At(g, PipX, PipY + 14, "Pip's stall"); }
                }
                list.Add(g);
            }
            if (state.Hinted("metDado") && !state.Hinted("isletsCharted"))
            {
                var g = R("dado:islets", $"Chart the islets ({IsletsCharted} of {IsletCount})", "Sail close by every islet of Daang Pulo to put it on your chart, then race Dado.");
                var near = Enumerable.Range(0, IsletCount).Where(i => state.charted?.Contains($"habagat:islet:{i}") != true)
                    .Select(i => i == 0 ? (x: HabagatIslands[2].cx * T, y: HabagatIslands[2].cy * T) : (x: Islets[i - 1].cx * T, y: Islets[i - 1].cy * T))
                    .OrderBy(p => Dist(player.X, player.Y, p.x, p.y)).ToList();
                if (near.Count > 0) At(g, near[0].x, near[0].y, "Uncharted islet");
                list.Add(g);
            }
            else if (state.Hinted("isletsCharted") && !state.Hinted("bk:agong"))
                list.Add(AtHabagat(R("dado:race", "Race in Dado's regatta", "Talk to Dado, then sail through every gate and back. Beat his lolo's old record to win the agong."), "dado"));
            if (state.Hinted("parolaLit") && !MoonReturned)
            {
                if (EclipseReady)
                {
                    int toFull = (4 - MoonPhase + 8) % 8;
                    var g = R("moon", "Bring the agong to Parola", $"On a full-moon night, stand on Parola with the agong. {(FullMoon ? "Tonight is full!" : toFull == 0 ? "The full moon is tonight." : $"The next full moon is in {toFull} day{(toFull == 1 ? "" : "s")}.")}");
                    g.Ready = true;
                    list.Add(At(g, LighthouseX, LighthouseY + 10, "Parola"));
                }
                else list.Add(R("moon", $"The vanishing moon ({MoonClueCount} of 4 clues)", "Your Case board (<case>) has the story so far. The people of Habagat know the rest: talk to them, and keep your eyes open."));
            }
            // Once a day: Manang Rosa's provisions for the boats.
            if (state.Hinted("metRosa") && !RosaOrderDone)
            {
                var (items, count, what) = RosaOrder;
                var g = R("rosa:order", $"Manang Rosa's order: {count} {what}", "", "daily");
                if (RosaCanFill) { g.Text = $"You have them. Manang Rosa pays {RosaPay} coins and {RosaSalt} salt."; g.Ready = true; AtHabagat(g, "rosa"); }
                else g.Text = Season == "amihan" ? $"Dry salted fish on a rack in the sun for daing or tuyo, then bring {count} to Manang Rosa today."
                    : $"Smoke fish with salt for tinapa, or simmer a fish in suka at a stove for paksiw, then bring {count} to Manang Rosa today.";
                list.Add(g);
            }
        }
    }

    // The restless sea (RestlessSea.cs): one goal for the case, a step at a time, and the pond's bank as its own job.
    // Nothing before the ground first shakes, and nothing ever says what's coming.
    void SeaGoals(List<Goal> list)
    {
        if (!SeaStoryStarted) return;
        if (PondDry && state.Hinted("rs:berberoka"))
        {
            var p = NewGoal("sea:pond", "request", "Mend the bangus pond's bank", "");
            if (Has("stone") >= 6 && Has("wood") >= 2) { p.Text = "You have them. Niko will mend the bank with the others."; p.Ready = true; AtIslander(p, "niko"); }
            else p.Text = $"Niko needs 6 stone and 2 wood. You have {Has("stone")} stone and {Has("wood")} wood.";
            list.Add(p);
        }
        var g = NewGoal("sea:case", "request", $"The restless sea ({SeaClueCount} of 4 clues)", "");
        if (!state.Hinted("rs:carpio")) { g.Text = "The ground shook. Ask Ma'am Isay about it."; AtIslander(g, "isay"); }
        else if (!state.Hinted("rs:berberoka")) { g.Text = "The bangus pond has drained. Ask Niko about it."; AtIslander(g, "niko"); }
        else if (!state.Hinted("rs:markTold")) { g.Text = "Ask Lira about the old post at the landing."; AtIslander(g, "lira"); }
        else if (!state.Hinted("rs:mark")) { g.Text = "Look at the mark on the old post by the landing."; At(g, PostX, PostY + 8, "The old post"); }
        else if (!state.Hinted("rs:signs")) { g.Text = "Tell Ma'am Isay what you've found."; AtIslander(g, "isay"); }
        else if (SignsUp < SignSpots.Length)
        {
            g.Title = $"Put up the evacuation signs ({SignsUp} of {SignSpots.Length})";
            g.Text = "At the green marks, from the landing up to School Rise.";
            int i = Enumerable.Range(0, SignSpots.Length).First(k => !state.Hinted($"rs:sign{k}"));
            At(g, SignSpots[i].x, SignSpots[i].y + 6, "A green mark");
        }
        else if (!state.Hinted("rs:drill")) { g.Title = "Run the drill"; g.Text = "Ask Ma'am Isay to ring the bell, then call Mia, Jun and Bea up to School Rise with you."; AtIslander(g, "isay"); }
        else if (!SeaCaseClosed) { g.Title = "Know the way up"; g.Text = "If the ground ever shakes hard by the sea, go straight up to School Rise along the signs, calling to anyone you pass."; }
        else if (!state.Hinted("rs:crabpot") && state.day > state.gifts.GetValueOrDefault("rs_done")) { g.Title = "Niko has something for you"; g.Text = "Visit Niko in Amihan Village."; g.Ready = true; AtIslander(g, "niko"); }
        else return;
        list.Add(g);
    }

    // An animal's name inside a sentence: lower case, but not a place in it ("Philippine tarsier").
    static string LowerName(string n) => n.StartsWith("Philippine") ? n : char.ToLowerInvariant(n[0]) + n[1..];

    // Bring someone n of a fish: where to catch it, or the person once you have them.
    Goal Hand(Goal g, string id, int n, string who, string pay)
    {
        if (Has(id) >= n) { g.Text = $"You have them. {pay}"; g.Ready = true; return AtIslander(g, who); }
        g.Text = $"{Source(g, id)} You have {Has(id)} of {n}.";
        return g;
    }

    Goal AtIslander(Goal g, string id)
    {
        var s = IslanderWalk(id);
        return At(g, s.X, s.Y, Islanders.First(n => n.id == id).name);
    }

    Goal AtHabagat(Goal g, string id)
    {
        var s = HabagatWalk(id);
        return At(g, s.X, s.Y, HabagatFolk.First(n => n.id == id).name);
    }

    // How to come by something for a request, pointing the goal at where (a fishing spot, Pip, an animal, a stove).
    string Source(Goal g, string id)
    {
        if (Data.SpotOfFish.TryGetValue(id, out var spotId))
        {
            var s = Data.SpotById[spotId];
            var f = Data.FishById[id];
            AtSpot(g, spotId);
            if (s.Scene == "sea") return $"Fish the {s.Label} from your boat over deep water, {WhenText(f)}.";
            if (s.Scene == "cave") return $"Fish the {s.Label.ToLowerInvariant()} in Frostfang Caverns, {WhenText(f)}.";
            string island = Data.Biomes.First(b => b.Id == s.Biome).Name;
            // "on Saltmere Island", but "in the Amihan Archipelago" and "in Mirewood".
            string on = island.EndsWith("Island") || island.EndsWith("Isle") || island.EndsWith("Atoll") ? "on " : island.Contains(' ') ? "in the " : "in ";
            return $"They bite at the {(SpotKnown(s) ? s.Label.ToLowerInvariant().Replace("the ", "") : "???")} {on}{island}, {WhenText(f)}{(SpotOpen(spotId) ? "" : " (it isn't open yet)")}.";
        }
        if (Items.Animals.Values.FirstOrDefault(a => a.Gift == id) is AnimalKind animal)
        {
            var home = Items.AnimalSpawns.Where(a => a.kind == animal.Kind).OrderBy(a => Dist(player.X, player.Y, a.x, a.y)).FirstOrDefault();
            if (home.kind != null) At(g, home.x, home.y, $"A {animal.Name}");
            return $"Pat a {animal.Name} (<act>): it gives one a day.";
        }
        if (ShopStock().Any(p => p.id == id)) { At(g, PipX, PipY + 14, "Pip's stall"); return "Pip sells them."; }
        // Bars Pip doesn't stock yet: smelt them (Chapters.cs).
        if (id is "copper_bar" or "iron_bar")
        {
            string metal = id == "iron_bar" ? "iron" : "copper";
            if (Has(metal + "_ore") >= 2)
            {
                if (HomeHas("furnace")) { AtHome(g, "furnace", "Your furnace"); return $"Smelt them at your furnace: 2 {metal} ore and 1 wood a bar."; }
                if (HasHome) { AtHome(g, null, "Your shack"); return $"You have the ore. Build a furnace in your shack (10 stone, 2 wood) and smelt it: 2 {metal} ore and 1 wood a bar."; }
                return $"You have the ore. Build a shack (<build>), put a furnace in it (10 stone, 2 wood) and smelt it: 2 {metal} ore and 1 wood a bar.";
            }
            g.Scene = "cave"; g.Ore = metal; g.Place = metal == "iron" ? "Iron ore" : "Copper ore"; g.HasPoint = true;
            return $"Mine {metal} ore in Frostfang Caverns{(metal == "iron" ? " (floor 3 and below, with a copper pickaxe)" : "")}, then smelt it at a furnace: 2 ore and 1 wood a bar.";
        }
        if (id == "wood") return "Chop trees (<act>) or pick up driftwood on the beaches.";
        if (Items.Recipes.FirstOrDefault(rc => rc.Out == id) is Recipe recipe)
        {
            string station = Items.StationName.GetValueOrDefault(recipe.Station, recipe.Station).ToLowerInvariant();
            if (recipe.Station == "fire") AtFire(g);
            else if (recipe.Station is "workbench" or "stove") At(g, recipe.Station == "workbench" ? 4 * T + 10 : 15 * T + 10, 3 * T + 8, $"Tomas's {station}", "house:tomas");
            else if (recipe.Station == "furnace" && HomeHas("furnace")) AtHome(g, "furnace", "Your furnace");
            return $"Make them at a {station} from {string.Join(", ", recipe.Needs.Select(kv => $"{kv.Value} {IngredientName(kv.Key)}"))}.";
        }
        return Items.ById.TryGetValue(id, out var d) ? d.Desc : "";
    }

    // Somewhere new, once there's a way to get there; and the people of each new island, who have work and stories.
    void ExploreGoals(List<Goal> list)
    {
        Goal E(string id, string title, string text) => NewGoal(id, "explore", title, text);
        if (state.commons.Values.Sum() > 0 && Has("boat") == 0 && !state.tamed)
        {
            var g = E("explore:boat", "Build a sailboat", "");
            if (Has("sailcloth") == 0) { g.Text = "A boat takes you to the other islands: 20 wood, 4 iron bars and sailcloth, made at a workbench. Pip sells the sailcloth."; At(g, PipX, PipY + 14, "Pip's stall"); }
            else if (Has("iron_bar") < 4)
                g.Text = $"Smelt 4 iron bars at a furnace from iron ore, which you mine in Frostfang Caverns (floor 3 and below) with a copper pickaxe{(ShopSells("iron_bar") ? ". Pip sells them too" : "")}. You have {Has("iron_bar")}.";
            else if (Has("wood") < 20) g.Text = $"Gather 20 wood (you have {Has("wood")}): chop trees with <act> or pick up driftwood. Then make the boat at the workbench in Tomas's hut.";
            else { g.Text = "You have everything. Make the boat at the workbench in Tomas's hut."; g.Ready = true; At(g, 4 * T + 10, 3 * T + 8, "Workbench", "house:tomas"); }
            list.Add(g);
        }
        bool canTravel = Has("boat") > 0 || state.tamed;
        if (canTravel && !state.Hinted("visitedAtoll"))
            list.Add(At(E("explore:atoll", "Sail to Starfall Atoll", "A ring of white sand far to the east, with warm water and big fish off its reef."), AtollJettyX, AtollJettyY, "Starfall Atoll"));
        else if (canTravel && !state.Hinted("amihan"))
            list.Add(At(E("explore:amihan", "Find the Amihan Archipelago", "Palm villages and limestone lagoons, far to the east of Starfall Atoll. Moor at the village landing on the western island."), 1495, 217, "Village landing"));
        if (canTravel && state.Hinted("pipHabagat") && !state.Hinted("habagat"))
            list.Add(At(E("explore:habagat", "Find the Habagat islands", "Pip says there are salt beds and a lighthouse south of the big islands. Asinan's landing is on the biggest one."), AsinanJettyX, AsinanJettyY, "Asinan"));
        // The people of each island: the nearest one you haven't met yet.
        if (state.Hinted("amihan"))
        {
            var unmet = new List<(string name, float x, float y, string note)>();
            if (!state.Hinted("liraSupperAsked") && !state.Hinted("rondalla") && !state.gifts.ContainsKey("lira_meal")) Add("lira", "keeps a pot warm in Amihan Village");
            if (!state.Hinted("nikoAsked") && !state.Hinted("niko_request")) Add("niko", "tends the nets in Amihan Village");
            if (!state.Hinted("guso")) Add("maya", "studies the karst lagoon on Luntian");
            if (!state.Hinted("bubo")) Add("tala", "watches the wildlife on Bakawan");
            if (!state.Hinted("metIsay")) Add("isay", "teaches the children at the school in Amihan Village");
            // Baga (Magayon.cs): Manay Mila in her abaca garden, Ben at the volcano station.
            if (!MgMet) Add("mila", "grows abaca on Baga's north shore");
            if (!state.Hinted("mg:metBen")) Add("ben", "runs the volcano station on Baga");
            if (!state.Hinted("metJoy")) unmet.Add(("Bantay Joy", JoyX, JoyY, "guards the marine sanctuary"));
            Meet("amihan", "Amihan", unmet, 8);
            void Add(string id, string note) { var s = IslanderWalk(id); unmet.Add((Islanders.First(n => n.id == id).name, s.X, s.Y, note)); }
        }
        if (state.Hinted("habagat"))
        {
            var unmet = new List<(string name, float x, float y, string note)>();
            foreach (var (id, flag, note) in new[] { ("rosa", "metRosa", "keeps the salt beds on Asinan"), ("pacing", "metPacing", "plays sungka on Daang Pulo"),
                ("dado", "metDado", "races boats around Daang Pulo"), ("celso", "metCelso", "keeps the Parola lighthouse") })
                if (!state.Hinted(flag)) { var s = HabagatWalk(id); unmet.Add((HabagatFolk.First(n => n.id == id).name, s.X, s.Y, note)); }
            Meet("habagat", "Habagat", unmet, 4);
        }

        void Meet(string id, string region, List<(string name, float x, float y, string note)> unmet, int all)
        {
            if (unmet.Count == 0) return;
            var p = unmet.OrderBy(u => Dist(player.X, player.Y, u.x, u.y)).First();
            list.Add(At(E("meet:" + id, $"Meet the people of {region} ({all - unmet.Count} of {all})", $"{p.name} {p.note}. Islanders have work and stories for a visiting fisher."), p.x, p.y, p.name));
        }
    }

    // The goal to follow: the one you picked, else the story, a request that's ready, any request, then exploring.
    Goal PickTracked(List<Goal> list)
    {
        if (state.track is { Length: > 0 } id && list.FirstOrDefault(g => g.Id == id) is Goal chosen) return chosen;
        return list.FirstOrDefault(g => g.Group == "story")
            ?? list.FirstOrDefault(g => g.Group == "request" && g.Ready)
            ?? list.FirstOrDefault(g => g.Group == "request")
            ?? list.FirstOrDefault(g => g.Group == "explore")
            ?? list.FirstOrDefault(g => g.Group == "daily");
    }

    void UpdateGuide(float dt)
    {
        if (mode is "title" or "create") return;
        if (mode == "play") goalDoneT = Math.Max(0, goalDoneT - dt);
        // Heading up to School Rise (RestlessSea.cs): that's the only goal, and nothing you were following is lost.
        if (SeaEmergency)
        {
            // What you were following comes back afterwards, even if you follow this one meanwhile (Codex).
            trackBeforeSea ??= state.track ?? "";
            var up = At(NewGoal("sea:up", "story", OnRise ? "Stay on School Rise" : "Up to School Rise!",
                OnRise ? "Wait together for the all-clear." : "Follow the green signs uphill, and call to anyone you pass."), RiseX, RiseY, "School Rise");
            goals = new() { up };
            tracked = up;
            guideWay = Route(up);
            return;
        }
        // Leaving Baga and the ash (Magayon.cs) work the same way: their own goal, and yours kept for afterwards.
        if (VolcanoControlled)
        {
            trackBeforeSea ??= state.track ?? "";
            var now = MagayonNowGoal();
            goals = new() { now };
            tracked = now;
            guideWay = Route(now);
            return;
        }
        if (trackBeforeSea != null) { state.track = trackBeforeSea; trackBeforeSea = null; }
        goals = Goals();
        if (state.track is { Length: > 0 } t && !goals.Any(g => g.Id == t)) state.track = "";
        var pick = PickTracked(goals);
        // The goal you were following is gone: it's done. The card says so, then moves on to the next.
        if (lastTrackedId != null && pick?.Id != lastTrackedId && !goals.Any(g => g.Id == lastTrackedId))
        {
            goalDoneTitle = lastTrackedTitle;
            goalDoneT = 3.5f;
        }
        tracked = pick;
        lastTrackedId = pick?.Id;
        lastTrackedTitle = pick?.Title;
        guideWay = Route(tracked);
        // Someone asked you for something: say it's in the journal, once you're back in play (and not over another toast).
        if (mode != "play") return;
        var ids = goals.Where(g => g.Group == "request").Select(g => g.Id).ToHashSet();
        if (requestsKnown != null && GuideOn)
            foreach (var g in goals.Where(g => g.Group == "request" && !requestsKnown.Contains(g.Id)))
            {
                if (toastTimer > 0) return;
                if (g != tracked) Toast($"New in your journal: {g.Title}. Follow it from the journal (<journal>).", 4.5f);
                break;
            }
        requestsKnown = ids;
    }

    /* ---------- Getting there ---------- */
    // Which island a place is on, for crossing the sea. The four western islands are joined by bridges, so they're
    // one ("west"); Amihan's islands and Habagat's are each their own ("amihan:Luntian Karsts", "habagat:Parola"),
    // and Bantay Joy's platform stands in open sea.
    string GuideRegion(float x, float y)
    {
        if (x >= EastStart * T && Dist(x, y, JoyX, JoyY) < 60) return "amihan:sanctuary";
        byte b = BiomeAt((int)MathF.Floor(x / T), (int)MathF.Floor(y / T));
        return b == 5 || b == 6 ? RegionOf(x, y) : b == 4 ? "atoll" : "west";
    }

    static string Zone(string region) => region.Split(':')[0];

    static string Heading(float x0, float y0, float x1, float y1)
    {
        float a = MathF.Atan2(y1 - y0, x1 - x0) * 180 / MathF.PI;
        string[] names = { "east", "south-east", "south", "south-west", "west", "north-west", "north", "north-east" };
        return names[((int)MathF.Round(a / 45) + 8) % 8];
    }

    // Where the arrow points for a goal from where you are, and what to do first when it isn't straight there.
    Waypoint Route(Goal g)
    {
        if (g == null || !g.HasPoint) return null;
        if (InHouse)
            return g.Scene == scene ? (g.X < 0 ? null : new(g.X, g.Y, g.Place, null)) : new(RoomDoorWX, RoomDoorWY, "Door", "Go outside first.");
        if (scene == "cave")
        {
            if (g.Scene != "cave") return new(ropeTile.x * T + 5, ropeTile.y * T + 8, "Ladder", "Climb the ladder back to the surface first.");
            if (g.Ore != null) return OreRoute(g);
            var s = Data.SpotById[g.Spot];
            if (SpotHere(s)) { var (sx, sy) = SpotPos(s); return new(sx, sy, s.Label, null); }
            if (s.Id != "ancientpool" && caveFloor == AncientFloor)
                return new(ropeTile.x * T + 5, ropeTile.y * T + 8, "Ladder", "The cave pool is on the floors above: climb back up and come down again.");
            return holeTile.x >= 0 ? new(holeTile.x * T + 5, holeTile.y * T + 5, "Way down", $"Keep climbing down to find the {s.Label.ToLowerInvariant()}.") : null;
        }
        if (scene != "world") return null;
        float gx = g.X, gy = g.Y;
        string place = g.Place, how = null;
        if (g.Scene == "house:tomas") { gx = 160; gy = 72; how = "It's inside Tomas's hut."; }
        else if (g.Scene == "house:school") { gx = SchoolDoorX; gy = SchoolDoorY + 4; how = "It's inside the school."; }
        else if (HouseDoor(g.Scene) is (float hx, float hy)) { gx = hx; gy = hy + 2; place = "Your shack"; how = "It's inside your shack: <act> at the door."; }
        else if (g.Scene == "cave") { gx = MouthDoorX; gy = MouthDoorY; place = "Frostfang Caverns"; how = "It's down in Frostfang Caverns."; }
        else if (g.Scene == "sea")
        {
            if (OverDeepSea && SeaSpotHere == g.Spot && !InSanctuary(player.X, player.Y)) return null;   // you're on it: cast
            (gx, gy) = SeaPoint(g.Spot);
            how = Aboard || Riding ? "Fish over the deep water there (<act>)." : null;
        }
        string from = GuideRegion(player.X, player.Y), to = gx == g.X && gy == g.Y && g.Region != null ? g.Region : GuideRegion(gx, gy);
        if (Aboard)
        {
            // Pip's jetty is inside the bridges, so home from far away is by the atoll's jetty.
            if (to == "west" && from != "west")
                return new(AtollJettyX, AtollJettyY, "Atoll jetty", "Land at the atoll's jetty (<ride>), then sail home from it (<act>).");
            bool land = g.Scene != "sea" && !WaterTile(TileAt((int)MathF.Floor(gx / T), (int)MathF.Floor(gy / T)));
            return new(gx, gy, place, how ?? (land ? "Land beside it with <ride>." : null));
        }
        if (from == to || Riding) return new(gx, gy, place, how);
        return Crossing(from, to, gx, gy, place) ?? new(gx, gy, place, how);
    }

    // On foot, with the goal across the sea: to the jetty you'd sail from, or your boat.
    Waypoint Crossing(string from, string to, float gx, float gy, string place)
    {
        string storm = Stormy ? " Wait for the storm to pass first." : "";
        if (state.tamed) return new(gx, gy, place, $"Whistle for {Data.MountName} (<ride>) and swim across.{storm}");
        if (Has("boat") == 0) return new(gx, gy, place, "You'll need a boat to get there.");
        string name = to.StartsWith("amihan:") && to != "amihan:sanctuary" ? to[7..] : to.StartsWith("habagat:") ? (to.StartsWith("habagat:islet") ? "Daang Pulo" : to[8..])
            : to switch { "atoll" => "Starfall Atoll", "amihan:sanctuary" => "the sanctuary", _ => "Saltmere" };
        // Among Amihan's islands or Habagat's, it's your boat if it's moored out here (else Asinan's landing, on Asinan).
        if (Zone(from) is "amihan" or "habagat")
        {
            var (bx, by) = BoatPosition();
            if (Zone(GuideRegion(bx, by)) == Zone(from)) return new(bx, by, "Your boat", $"Board your boat (<ride>) and sail {Heading(bx, by, gx, gy)} to {name}.{storm}");
            if (from != "habagat:Asinan") return null;
        }
        switch (Zone(from))
        {
            case "west":
                return new(SaltJettyX, SaltJettyY, "Pip's jetty", (Zone(to) == "habagat" && VoyageChoice ? "At Pip's jetty, sail to Asinan (<act>)."
                    : to == "atoll" ? "At Pip's jetty, sail to Starfall Atoll (<act>)." : "At Pip's jetty, sail to Starfall Atoll (<act>), then take the helm there (<alt>).") + storm);
            case "atoll":
                return new(AtollJettyX, AtollJettyY, "Atoll jetty", (to == "west" ? "At the atoll's jetty, sail back to Saltmere (<act>)."
                    : Zone(to) == "habagat" && VoyageChoice ? "At the atoll's jetty, sail to Asinan (<act>)."
                    : $"At the atoll's jetty, take the helm (<alt>) and steer {Heading(AtollJettyX, AtollJettyY, gx, gy)} to {name}.") + storm);
            case "habagat":
                return new(AsinanJettyX, AsinanJettyY, "Asinan landing", (to is "west" or "atoll" ? $"At Asinan's landing, sail to {name} (<act>)."
                    : $"At Asinan's landing, take the helm (<alt>) and steer {Heading(AsinanJettyX, AsinanJettyY, gx, gy)} to {name}.") + storm);
            default:
                return null;
        }
    }

    // Deep water of one sea, the nearest to you (worked out again only when you've moved a way).
    (float x, float y) SeaPoint(string spot)
    {
        var c = seaPointCache;
        if (c.spot == spot && Dist(c.px, c.py, player.X, player.Y) < 40) return (c.x, c.y);
        bool Fits(int tx, int ty) => worldMap[ty, tx] == '~' && (spot == "amihansea" ? tx >= EastStart && !InSanctuary(tx * T + 5, ty * T + 5)
            : tx < EastStart && (spot == "habagatsea") == (ty >= HabagatTop));
        int px = Math.Clamp((int)(player.X / T), 1, COLS - 2), py = Math.Clamp((int)(player.Y / T), 1, ROWS - 2);
        (float x, float y) best = (0, 0);
        bool Try(int tx, int ty)
        {
            if (tx < 1 || ty < 1 || tx >= COLS - 1 || ty >= ROWS - 1 || !Fits(tx, ty)) return false;
            best = (tx * T + 5, ty * T + 5);
            return true;
        }
        // Ring by ring outwards, so the first one found is (about) the nearest.
        for (int r = 0; r < COLS; r++)
        {
            bool found = false;
            for (int d = -r; d <= r && !found; d++) found = Try(px + d, py - r) || Try(px + d, py + r) || Try(px - r, py + d) || Try(px + r, py + d);
            if (found) break;
        }
        seaPointCache = (spot, player.X, player.Y, best.x, best.y);
        return best;
    }

    // Where the followed goal is on the chart: outdoors, at the door of the hut or the caverns it's in.
    (float x, float y)? GoalOnChart()
    {
        var g = tracked;
        if (g == null || !g.HasPoint) return null;
        return g.Scene switch
        {
            "house:tomas" => (160, 72),
            "house:school" => (SchoolDoorX, SchoolDoorY),
            "cave" => (MouthDoorX, MouthDoorY),
            _ when HouseDoor(g.Scene) is (float hx, float hy) => (hx, hy),
            "sea" => scene == "world" ? SeaPoint(g.Spot) : null,
            _ => (g.X, g.Y)
        };
    }

    /* ---------- The HUD card ---------- */
    void DrawGuideCard()
    {
        guideCardBottom = 0;
        if (!GuideShown) return;
        const float pad = 12;
        float x = CardX, y = CardY, w = CardW;
        if (goalDoneT > 0 && goalDoneTitle != null)
        {
            // Just done: a moment of green before the next goal.
            var done = Gfx.Wrap(goalDoneTitle, FontKind.Ui600, 16, w - 2 * pad);
            float dh = pad + 20 + done.Count * 20 + pad - 4;
            Gfx.Box(x, y, w, dh, Pal.C("rgba(32,90,48,0.92)"), Pal.C("#7fd36b"), 2, 5);
            Gfx.Text("Goal done!", x + pad, y + pad - 2, FontKind.Ui700, 17, Pal.C("#bff2b0"));
            Lines(done, x + pad, y + pad + 20, 20, FontKind.Ui600, 16, Pal.Paper);
            guideCardBottom = y + dh;
#if DEBUG
            Gfx.Seen["guide card"] = new Rectangle(x, y, w, dh);
#endif
            if (mode == "play" && Gfx.Click(x, y, w, dh)) TogglePanel("journal");
            Gfx.Block(x, y, w, dh);
            return;
        }
        var g = tracked;
        if (g == null) return;
        var text = Gfx.Wrap(Bind.Fix(g.Text), FontKind.Ui500, 15, w - 2 * pad);
        if (text.Count > 3) text = new List<string> { text[0], text[1], Gfx.Ellipsize(text[2] + " " + text[3], FontKind.Ui500, 15, w - 2 * pad) };
        var how = guideWay?.How is string hw ? Gfx.Wrap(Bind.Fix(hw), FontKind.Ui600, 15, w - 2 * pad - 16) : new List<string>();
        string dist = guideWay != null ? $"{(int)MathF.Round(Dist(player.X, player.Y, guideWay.X, guideWay.Y) * 0.15f)} m" : "";
        float distW = dist == "" ? 0 : Gfx.Measure(dist, FontKind.Ui600, 15) + 10;
        float h = pad + 22 + text.Count * 19 + (how.Count > 0 ? 6 + how.Count * 19 : 0) + pad - 2;
        bool hover = Gfx.Hover(x, y, w, h) && mode == "play";
        Gfx.Box(x, y, w, h, NavyStrong, hover ? Pal.Lantern : Pal.C("rgba(243,194,91,0.55)"), 2, 5);
        // A little gold marker, like the one over the goal.
        GoalMarker(x + pad + 7, y + pad + 8, 0.7f);
        string title = Gfx.Ellipsize(g.Title, FontKind.Ui700, 17, w - 2 * pad - 22 - distW);
        Gfx.Text(title, x + pad + 22, y + pad - 2, FontKind.Ui700, 17, Pal.Lantern);
        if (dist != "") Gfx.Text(dist, x + w - pad - distW + 10, y + pad, FontKind.Ui600, 15, Pal.C("#9fc3d1"));
        float ty = y + pad + 22;
        Lines(text, x + pad, ty, 19, FontKind.Ui500, 15, Pal.Paper);
        ty += text.Count * 19;
        if (how.Count > 0)
        {
            ty += 6;
            Gfx.Triangle(x + pad + 1, ty + 4, x + pad + 1, ty + 14, x + pad + 9, ty + 9, Pal.C("#bff4ff"));
            Lines(how, x + pad + 16, ty, 19, FontKind.Ui600, 15, Pal.C("#bff4ff"));
        }
        guideCardBottom = y + h;
#if DEBUG
        Gfx.Seen["guide card"] = new Rectangle(x, y, w, h);
#endif
        if (mode == "play" && Gfx.Click(x, y, w, h)) { TogglePanel("journal"); return; }
        Gfx.Block(x, y, w, h);
        if (hover) Gfx.Text(Bind.Fix("Click for your journal (<journal>)"), x + 2, y + h + 4, FontKind.Ui600, 14, Pal.Paper);
    }

    // A gold double chevron pointing down: the goal's mark, over the place in the world and on the card.
    static void GoalMarker(float x, float y, float k, bool up = false)
    {
        // Pointing up (from below the place), the whole thing is mirrored top to bottom.
        float d = up ? -1 : 1;
        for (int i = 1; i >= 0; i--)
        {
            float cy = y - i * 9 * k * d;
            var gold = i == 1 ? Pal.WithAlpha(Pal.Lantern, 0.75f) : Pal.Lantern;
            Gfx.Triangle(x - 11 * k, cy - 6 * k * d, x + 11 * k, cy - 6 * k * d, x, cy + 8 * k * d, Pal.Ink);
            Gfx.Triangle(x - 8 * k, cy - 4.5f * k * d, x + 8 * k, cy - 4.5f * k * d, x, cy + 5 * k * d, gold);
        }
    }

    /* ---------- The arrow ---------- */
    // Over the place when it's in view; otherwise at the edge of the view pointing there, with the distance.
    void DrawGuideArrow()
    {
        if (!GuideShown || guideWay == null) return;
        const float k = 4;   // the world view is drawn 4x
        float sx = (guideWay.X - camX) * k, sy = (guideWay.Y - camY) * k;
        float cx = (player.X - camX) * k, cy = (player.Y - 6 - camY) * k;
        if (MarkerAt(sx, sy) is { } at)
        {
            GoalMarker(at.x, at.y + MathF.Sin(time * 4) * 3, 1, at.up);
            return;
        }
        float dx = sx - cx, dy = sy - cy, len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        float ux = dx / len, uy = dy / len;
        var (ax, ay) = EdgePoint(cx, cy, ux, uy);
        float px = -uy, py = ux;
        Gfx.Circle(ax, ay, 21, Pal.WithAlpha(Pal.Ink, 0.8f));
        Gfx.Circle(ax, ay, 19, Pal.WithAlpha(Pal.Lantern, 0.25f));
        Gfx.Triangle(ax + ux * 16, ay + uy * 16, ax - ux * 6 + px * 10, ay - uy * 6 + py * 10, ax - ux * 6 - px * 10, ay - uy * 6 - py * 10, Pal.Lantern);
        string label = $"{guideWay.Place} {(int)MathF.Round(Dist(player.X, player.Y, guideWay.X, guideWay.Y) * 0.15f)} m";
        float lw = Gfx.Measure(label, FontKind.Ui700, 15);
        float lx = Math.Clamp(ax - lw / 2, 8, Gfx.LW - lw - 8), ly = ay + (uy > 0.5f ? -48 : 25);
        Gfx.Rect(lx - 6, ly - 2, lw + 12, 22, Pal.WithAlpha(Pal.Ink, 0.8f), 4);
        Gfx.Text(label, lx, ly, FontKind.Ui700, 15, Pal.Lantern);
    }

    // Where the marker goes for a point in view (screen coordinates): high enough to clear a person's hat, as the
    // point is at their feet. Null when the point is off the view.
    (float x, float y, bool up)? MarkerAt(float sx, float sy)
    {
        if (!(sx > 30 && sx < Gfx.LW - 30 && sy > 90 && sy < Gfx.LH - 40)) return null;
        // Both chevrons and the bob: pointing down it reaches 18 px above its y and 11 below; pointing up, the reverse.
        bool OnCard(float y, bool up) => guideCardBottom > 0 && sx > CardX - 14 && sx < CardX + CardW + 14
            && y + (up ? 18 : 11) > CardY - 4 && y - (up ? 11 : 18) < guideCardBottom + 4;
        if (!OnCard(sy - 76, false)) return (sx, sy - 76, false);
        if (!OnCard(sy + 26, true)) return (sx, sy + 26, true);
        return (sx, guideCardBottom + 16, true);
    }

    // Where the line from the middle of the view leaves it, inset from the edges, and kept below the HUD card.
    (float x, float y) EdgePoint(float cx, float cy, float ux, float uy)
    {
        float top = 100, t = float.MaxValue;
        if (ux > 0) t = MathF.Min(t, (Gfx.LW - 46 - cx) / ux); else if (ux < 0) t = MathF.Min(t, (46 - cx) / ux);
        // Above the hotbar along the bottom (1.20), with room for the label under a sideways arrow.
        float bottom = MathF.Min(Gfx.LH - 80, HotbarShown && lastHotbarTop > 0 ? lastHotbarTop - 56 : Gfx.LH);
        if (uy > 0) t = MathF.Min(t, (bottom - cy) / uy); else if (uy < 0) t = MathF.Min(t, (top - cy) / uy);
        float ax = cx + ux * t, ay = cy + uy * t;
        if (guideCardBottom > 0 && ax < CardX + CardW + 24 && ay < guideCardBottom + 26) ay = guideCardBottom + 26;
        return (ax, ay);
    }

    /* ---------- Getting started ---------- */
    // The basics, ticked off as you do them, on three pages: the first days, tools and a home, and the caverns (the
    // last two follow the story's chapters, Chapters.cs). Old saves from well into the story count the first page as
    // done; the others are worked out from what you have, so a save that already has a shack or a copper pickaxe ticks
    // those off.
    static readonly string[] BasicsPages = { "First days", "Tools and a home", "Into the caverns" };
    (string title, string hint, bool done, int page)[] Basics()
    {
        bool old = state.Hinted("tut:legacy");
        bool H(string k) => old || state.Hinted("tut:" + k);
        bool L(string k) => state.Hinted("tut:" + k);
        int tier = PickTier;
        return new[]
        {
            ("Talk to Tomas", "Walk up to him at his camp and press <act>.", state.flags.metTomas, 0),
            ("Catch a fish", "Face the water, hold <act> to power up a cast, then let go.", state.commons.Values.Sum() > 0 || state.caught.Count > 0, 0),
            ("Sell fish to Pip", "Pip's stall is just east of Tomas's camp.", H("sell"), 0),
            ("Buy bait from Pip", "Bait makes fish bite sooner. One goes on each cast.", H("buy"), 0),
            ("Eat something", "Open your bag (<bag>) and eat when the food bar runs low.", H("eat"), 0),
            ("Cook a fish", "Face a campfire and press <alt> to cook.", H("cook"), 0),
            ("Rest until night", "Press <act> by a campfire or a bed to skip ahead.", H("rest"), 0),
            ("Look at your Fesh-dex", "Press <dex> for the creatures and every fish you've found.", H("dex"), 0),
            ("Open the map", "Press <map>. Click the map to drop a pin.", H("map"), 0),

            ("Pick up driftwood and stones", "Walk over them on the beaches to pick them up.", L("gather") || Has("axe") > 0 || tier > 0, 1),
            ("Make a stone axe", "At a workbench (Tomas's hut has one): 3 wood, 2 stone.", Has("axe") > 0, 1),
            ("Make a stone pickaxe", "At a workbench: 3 wood and 3 stone.", tier > 0, 1),
            ("Chop a tree", "Face a tree with your axe and press <act>.", L("chop"), 1),
            ("Break a boulder", "Grey boulders give stone. Use your pickaxe.", L("boulder"), 1),
            ("Build a shack", "Press <build> outdoors: 8 wood and 4 stone.", HasHome, 1),
            ("Put a workbench in it", "Inside your shack, press <build>: 6 wood, 2 stone.", HomeHas("workbench"), 1),
            ("Build a furnace", "Inside your shack: 10 stone and 2 wood.", HomeHas("furnace"), 1),

            ("Climb down into the caverns", "Frostfang Caverns: across the bridge east of Saltmere.", state.caveDeepest > 0, 2),
            ("Mine copper ore", "The orange rock in the cave walls. Any pickaxe.", L("ore:copper") || tier >= 2, 2),
            ("Smelt a copper bar", "At your furnace: 2 copper ore and 1 wood.", L("smelted:copper_bar") || tier >= 2, 2),
            ("Make a copper pickaxe", "At a workbench: 2 copper bars and 2 wood.", tier >= 2, 2),
            ("Mine iron ore", "Floor 3 and deeper, with a copper pickaxe.", L("ore:iron") || tier >= 3, 2),
            ("Smelt an iron bar", "At your furnace: 2 iron ore and 1 wood.", L("smelted:iron_bar") || tier >= 3, 2)
        };
    }
    int basicsPage = -1;   // the Getting started page showing (-1: the first with something left to do)

    // Loading a save made before the guide, well into the story: the basics count as done (it's played them).
    // Every other game, new or old, does them for real.
    void NoteGuideVersion(bool fresh)
    {
        if (state.Hinted("guide")) return;
        if (!fresh && (state.caught.Count >= 2 || state.flags.ended)) state.hinted["tut:legacy"] = true;
        state.hinted["guide"] = true;
    }

    void Learned(string what)
    {
        if (state.Hinted("tut:" + what)) return;
        state.hinted["tut:" + what] = true;
    }

    /* ---------- The journal ---------- */
    void DrawJournal()
    {
        Backdrop();
        const float cw = 1120, ch = 650, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Journal", x + pad, y + pad, FontKind.Ui700, 36, Pal.PaperInk);
        Gfx.Text("Where to go next. Follow a goal and the arrow points the way.", x + pad, y + pad + 46, FontKind.Note, 18, Muted);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { ClosePanels(); return; }
        string toggle = GuideOn ? "Hide the arrow" : "Show the arrow";
        if (SmallButton(toggle, x + cw - pad - SmallW("Close") - 12 - SmallW(toggle), y + pad - 2))
        {
            Settings.Data.guide = !Settings.Data.guide;
            Settings.Save();
            Sfx.Play("ui");
        }

        // Goals, a page at a time.
        float lx = x + pad, ly = y + pad + 84, lw = 700;
        int pages = Math.Max(1, (goals.Count + JournalRows - 1) / JournalRows);
        journalPage = Math.Clamp(journalPage, 0, pages - 1);
        bool auto = state.track is not { Length: > 0 };
        Gfx.Text("Goals", lx, ly, FontKind.Ui700, 22, Pal.PaperInk);
        float hx = lx + Gfx.Measure("Goals", FontKind.Ui700, 22) + 18;
        if (Button("Automatic", hx, ly - 4, 130, 32, FontKind.Ui700, 16, auto ? Pal.Lantern : Pal.Sand, Pal.Ink, 2, 2, 5))
        {
            state.track = "";
            Sfx.Play("ui");
        }
        Gfx.Text(auto ? "Following the story, then requests" : "Following the goal you picked", hx + 142, ly + 3, FontKind.Ui500, 15, Muted);
        if (pages > 1)
        {
            Gfx.Text($"{journalPage + 1} / {pages}", lx + lw - 150, ly + 2, FontKind.Ui600, 16, Muted);
            if (Button("<", lx + lw - 92, ly - 4, 42, 32, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 2, 2, 5, journalPage > 0)) journalPage--;
            if (Button(">", lx + lw - 44, ly - 4, 42, 32, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 2, 2, 5, journalPage < pages - 1)) journalPage++;
        }
        ly += 40;
        if (goals.Count == 0)
            Lines(Gfx.Wrap("Nothing waiting on you right now. Fish where you like, and talk to people: they'll have something for you.", FontKind.Note, 19, lw), lx, ly + 10, 26, FontKind.Note, 19, Muted);
        const float rowH = 82;
        foreach (var (g, i) in goals.Skip(journalPage * JournalRows).Take(JournalRows).Select((g, i) => (g, i)))
        {
            float ry = ly + i * (rowH + 6);
            bool on = tracked?.Id == g.Id;
            Gfx.Box(lx, ry, lw, rowH, on ? Pal.C("#fff3cf") : CardBg, on ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 5);
            var (chip, col) = g.Group switch
            {
                "story" => ("Story", Pal.C("#2a7d74")), "request" => ("Request", Pal.C("#b5523b")), "daily" => ("Today", Pal.C("#8a6a2a")), _ => ("Explore", Pal.C("#3f6a8a"))
            };
            float chipW = Gfx.Measure(chip, FontKind.Ui700, 13) + 14;
            Gfx.Rect(lx + 12, ry + 10, chipW, 20, col, 4);
            Gfx.Text(chip, lx + 19, ry + 12, FontKind.Ui700, 13, Pal.Paper);
            float bw = 112, textW = lw - 24 - bw - 16;
            Gfx.Text(Gfx.Ellipsize(g.Title, FontKind.Ui700, 18, textW - chipW - 10), lx + 22 + chipW, ry + 9, FontKind.Ui700, 18, Pal.PaperInk);
            var body = Gfx.Wrap(Bind.Fix(g.Text), FontKind.Ui500, 15, textW);
            if (body.Count > 2) body = new List<string> { body[0], Gfx.Ellipsize(body[1] + " " + body[2], FontKind.Ui500, 15, textW) };
            Lines(body, lx + 12, ry + 36, 19, FontKind.Ui500, 15, Pal.PaperInk);
            if (g.Ready) { Gfx.Circle(lx + lw - bw - 26, ry + 20, 6, Pal.C("#3fae5a")); }
            float bx = lx + lw - bw - 12, by = ry + (rowH - 36) / 2;
            string label = on ? "Following" : "Follow";
#if DEBUG
            Gfx.Seen["follow:" + g.Id] = new Rectangle(bx, by, bw, 36);
#endif
            if (Button(label, bx, by, bw, 36, FontKind.Ui700, 17, on ? Pal.Lantern : Pal.Sand, Pal.Ink, 3, 2, 5))
            {
                state.track = on && !auto ? "" : g.Id;
                if (!GuideOn) { Settings.Data.guide = true; Settings.Save(); }
                Sfx.Play("ui");
                Save();
            }
        }

        // Getting started, or the Sea school (SeaSchool.cs).
        float rx = lx + lw + 26, rw = x + cw - pad - rx, ry0 = y + pad + 84;
        var basics = Basics();
        bool school = journalSide == "school";
        float tw = (rw - 8) / 2;
        if (Button("Getting started", rx, ry0 - 6, tw, 34, FontKind.Ui700, 16, !school ? Pal.Lantern : Pal.Sand, Pal.Ink, 2, 2, 5)) { journalSide = "basics"; Sfx.Play("ui"); }
        if (Button("Sea school", rx + tw + 8, ry0 - 6, tw, 34, FontKind.Ui700, 16, school ? Pal.Lantern : Pal.Sand, Pal.Ink, 2, 2, 5)) { journalSide = "school"; Sfx.Play("ui"); }
        if (school)
        {
            float end = DrawSeaSchool(rx, ry0 + 38, rw);
            if (end < 0) return;
            lastJournalBottom = Math.Max(ly + Math.Min(JournalRows, goals.Count) * (rowH + 6), end);
            if (Gfx.PressedOutside(x, y, cw, ch)) ClosePanels();
            return;
        }
        // A page at a time, with its name and arrows.
        if (basicsPage < 0) basicsPage = Enumerable.Range(0, BasicsPages.Length).FirstOrDefault(p => basics.Any(b => b.page == p && !b.done));
        basicsPage = Math.Clamp(basicsPage, 0, BasicsPages.Length - 1);
        var shown = basics.Where(b => b.page == basicsPage).ToList();
        string count = $"{BasicsPages[basicsPage]}: {shown.Count(b => b.done)} of {shown.Count} done";
        Gfx.Text(Gfx.Ellipsize(count, FontKind.Ui600, 15, rw - 96), rx, ry0 + 40, FontKind.Ui600, 15, Muted);
        // Drawn arrows, not "<" and ">" buttons: those labels already name the goals' page buttons (Gfx.Seen).
        if (PageArrow("basics:prev", false, rx + rw - 88, ry0 + 34, basicsPage > 0)) { basicsPage--; Sfx.Play("ui"); }
        if (PageArrow("basics:next", true, rx + rw - 42, ry0 + 34, basicsPage < BasicsPages.Length - 1)) { basicsPage++; Sfx.Play("ui"); }
        float by0 = ry0 + 72;
        foreach (var (title, hint, done, _) in shown)
        {
            Gfx.Circle(rx + 10, by0 + 11, 10, done ? Pal.C("#3f8a4a") : Pal.C("#c9b48f"));
            if (done)
            {
                Gfx.Line(rx + 5, by0 + 11, rx + 9, by0 + 15, 2.4f, Pal.Paper);
                Gfx.Line(rx + 9, by0 + 15, rx + 16, by0 + 6, 2.4f, Pal.Paper);
            }
            else Gfx.Circle(rx + 10, by0 + 11, 7.5f, CardBg);
            Gfx.Text(Gfx.Ellipsize(title, FontKind.Ui600, 16, rw - 30), rx + 28, by0 + 2, FontKind.Ui600, 16, done ? Muted : Pal.PaperInk);
            Gfx.Text(Gfx.Ellipsize(Bind.Fix(hint), FontKind.Ui500, 13, rw - 30), rx + 28, by0 + 22, FontKind.Ui500, 13, Muted);
            by0 += 40;
        }
        lastJournalBottom = Math.Max(ly + Math.Min(JournalRows, goals.Count) * (rowH + 6), by0);
        if (Gfx.PressedOutside(x, y, cw, ch)) ClosePanels();
    }
    float lastJournalBottom;   // where the journal's lists ended (the autotest checks they fit the panel)

    // A small button with a drawn arrow on it, named in Gfx.Seen by its key.
    static bool PageArrow(string key, bool right, float x, float y, bool live)
    {
        const float w = 42, h = 30;
        var fill = live && Gfx.Hover(x, y, w, h) ? Lighten(Pal.Sand, 0.14f) : Pal.Sand;
        Gfx.Box(x, y, w, h, fill, Pal.Ink, 2, 5, 2);
        float cx = x + w / 2, cy = y + h / 2, d = right ? 1 : -1;
        var ink = live ? Pal.Ink : Pal.WithAlpha(Pal.Ink, 0.3f);
        Gfx.Triangle(cx + 6 * d, cy, cx - 5 * d, cy - 7, cx - 5 * d, cy + 7, ink);
#if DEBUG
        Gfx.Seen[key] = new Rectangle(x, y, w, h);
#endif
        return live && Gfx.Click(x, y, w, h);
    }
}
