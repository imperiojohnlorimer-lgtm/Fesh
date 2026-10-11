#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Marilaya (1.23): the island modelled on Marinduque, on its own chart at the far east, and story 5, the heart that
    // rose, played by real key presses and clicks. FESH_MARILAYA_TEST=1 runs only this (it's also at the end of the full
    // run, after ChapterScript); FESH_MARILAYA_TEST=review runs only MarilayaReviewScript.
    IEnumerable<int> MarilayaScript()
    {
        Note("Marilaya, the island modelled on Marinduque, and story 5 (the heart that rose)");
        mlNoteCutMost = 0;
        Inp.ScriptMouse = Offscreen;
        IEnumerable<int> Talk()
        {
            for (int i = 0; i < 300 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
            for (int i = 0; i < 120 && mode == "fade"; i++) yield return 2;
            yield return 2;
        }
        IEnumerable<int> Click(string key, int wait = 3)
        {
            bool found = ClickButton(key);
            if (!found) Note($"(no button {key} on screen)");
            yield return wait;
            Inp.ScriptMouse = Offscreen;
            yield return 1;
        }
        IEnumerable<int> At(float x, float y, string face = "up")
        {
            player.X = x; player.Y = y; player.Face = face; player.Moving = false;
            yield return 3;
        }
        // Somewhere you can stand within r of a point (the nearest), for walking up to things.
        (float x, float y) StandNear(float x, float y, float r = 12)
        {
            (float x, float y) best = (x, y); float bd = float.MaxValue;
            for (float dy = -r; dy <= r; dy += 1)
                for (float dx = -r; dx <= r; dx += 1)
                {
                    float d = dx * dx + dy * dy;
                    if (d < bd && d <= r * r && CanStand(x + dx, y + dy)) { bd = d; best = (x + dx, y + dy); }
                }
            return best;
        }
        IEnumerable<int> WalkUp(float x, float y, float r = 12)
        {
            var (sx, sy) = StandNear(x, y, r);
            foreach (int f in At(sx, sy)) yield return f;
        }
        IEnumerable<int> E(int wait = 4) { Inp.Tap(KeyboardKey.E); yield return wait; }
        IEnumerable<int> Fades() { for (int i = 0; i < 120 && mode == "fade"; i++) yield return 2; yield return 3; }

        state = new State { created = true, look = new Look { name = "Marilaya" } };
        state.hinted["chapters"] = true;
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false);
        foreach (int f in Talk()) yield return f;

        /* ---------- The island, on its own chart in the east ---------- */
        File.WriteAllLines(Path.Combine(testDir, "marilaya-map.txt"), Enumerable.Range(0, ROWS).Select(y =>
            $"{y,2} " + new string(Enumerable.Range(MlStart - 2, COLS - MlStart + 2).Select(x => worldMap[y, x] switch { '~' => ' ', var c => c }).ToArray())));
        Check($"the world reaches further east ({COLS} columns), and everything from column {MlStart} on is Marilaya's",
            COLS > MlStart + 70 && Enumerable.Range(0, ROWS).All(y => Enumerable.Range(0, COLS).All(x => (biome[y, x] == 7) == (x >= MlStart))));
        var land = new List<(int x, int y)>();
        for (int y = 0; y < ROWS; y++)
            for (int x = MlStart; x < COLS; x++)
                if (mlRegion[y, x] == 1 && !WaterTile(worldMap[y, x])) land.Add((x, y));
        int w = land.Max(p => p.x) - land.Min(p => p.x) + 1, h = land.Max(p => p.y) - land.Min(p => p.y) + 1;
        Check($"the island is big now, with Marinduque's proportions ({w} x {h} tiles, {land.Count} of land)", w is >= 45 and <= 62 && h is >= 55 and <= 66 && w < h && land.Count > 1800);
        Check("the southern point (Tindig) is narrower than the middle", land.Count(p => p.y == land.Max(q => q.y) - 3) < land.Count(p => p.y == land.Min(q => q.y) + h / 2) / 2);
        Check("the harbour's jetty runs out into the cove, and a boat ties up there", Enumerable.Range(MlJettyX0, MlJettyX1 - MlJettyX0 + 1).All(x => worldMap[MlJettyRow, x] == 'd') && BoatCanStand(MlBoatX, MlBoatY));
        Check("zones: Marilaya's band, Amihan's still Amihan (and its chart)", ZoneAt(SelaX, SelaY) == "marilaya" && ZoneAt(MlStart * T - 5, 300) == "amihan" && ZoneAt(1600, 200) == "amihan"
            && SeaSpotAt(MlBoatX, MlBoatY) == "marilayasea" && SeaSpotAt(2200, 300) == "amihansea" && SeaSpotAt(600, 680) == "habagatsea" && SeaSpotAt(1000, 500) == "opensea");
        Check("charts: west, Amihan, Marilaya", ChartFor(500) == 0 && ChartFor(1600) == 1 && ChartFor(SelaX) == 2 && ChartSpan(1) == (EastStart, MlStart) && ChartSpan(2) == (MlStart, COLS));
        Check("regions: the island, the north islets, Salomague and the Tres Reyes",
            RegionOf(SelaX, SelaY) == "marilaya" && RegionOf(MlWx(25.4f), MlWy(3.4f)) == "marilaya:north" && RegionOf(MlWx(27.6f), MlWy(12.8f)) == "marilaya:salomague"
            && RegionOf(BaltasarX, BaltasarY) == "marilaya:tresreyes");
        var wade = WadeReach(MlJettyX + 4, MlJettyY);
        Check("nobody wades in from Amihan", !wade.Any(t => BiomeAt(t.Item1, t.Item2) is not 7));
        player.X = MlJettyX + 4; player.Y = MlJettyY;
        var reach = Reachable();
        foreach (var id in new[] { "cliff", "cave", "springs", "dryer", "streamgauge", "town", "plaza", "garden", "trailhead", "summit", "beach" })
        {
            var (px, py) = MlPlace(id);
            Check($"the {id} is on land you can walk to from the harbour", reach.Any(t => Dist(t.Item1 * T + 5, t.Item2 * T + 5, px, py) < 26));
        }
        Check("the trail climbs from the springs to Tindig's top", reach.Any(t => Dist(t.Item1 * T + 5, t.Item2 * T + 5, CairnAt.x, CairnAt.y + 8) < 14));
        foreach (var s in Data.Spots.Where(s => s.Biome == "marilaya" && s.Scene == "world"))
        {
            bool ok = false;
            for (int ty = (int)((s.Y - s.R) / T); ty <= (s.Y + s.R) / T && !ok; ty++)
                for (int tx = (int)((s.X - s.R) / T); tx <= (s.X + s.R) / T && !ok; tx++)
                {
                    float cx = tx * T + 5, cy = ty * T + 7;
                    ok = Dist(cx, cy, s.X, s.Y) < s.R - 2 && CanStand(cx, cy) && WaterTile(TileAt((int)(s.X / T), (int)(s.Y / T)));
                }
            Check($"{s.Label} is water you can fish from land", ok);
        }
        Check("every new fish has a picture, a real-fish note and an album entry",
            Data.Spots.Where(s => s.Biome == "marilaya").SelectMany(s => Data.Common[s.Id]).All(f => FishArt.Looks.ContainsKey(f.Id) && FishFacts.ById.ContainsKey(f.Id) && AlbumData.ContainsKey(f.Id)
                && AlbumChapters.Count(c => c.Fish.Contains(f.Id)) == 1));
        foreach (var s in Data.Spots.Where(s => s.Biome == "marilaya"))
        {
            var allowed = Data.Common[s.Id].Select(f => f.Id).Append("chest").ToHashSet();
            SetNight(false); var day = Enumerable.Range(0, 300).Select(_ => RollCatch(s.Id).Id).ToList();
            SetNight(true); var night = Enumerable.Range(0, 300).Select(_ => RollCatch(s.Id).Id).ToList();
            SetNight(false);
            Check($"{s.Label} gives only its own fish", day.Concat(night).All(allowed.Contains));
        }
        Check($"the sandbar's there off Maniwaya ({sandbar.Count} tiles), with the tide post by the beach", sandbar.Count >= 10 && tidePostX > MlStart * T && CanStand(PostReadSpot().x, PostReadSpot().y));
        foreach (var (name, x, y) in new[] { ("harbour", MlJettyX, MlJettyY), ("town", MlPlace("plaza").x, MlPlace("plaza").y), ("springs", MlPlace("springs").x, MlPlace("springs").y + 10),
            ("summit", TindigX, TindigY + 16), ("hills", MlPlace("cliff").x - 20, MlPlace("cliff").y + 14), ("tresreyes", BaltasarX, BaltasarY + 10) })
        {
            player.X = x; player.Y = y; player.Face = "down"; yield return 4;
            pendingShot = "marilaya-place-" + name; yield return 3;
        }

        /* ---------- Hearing of it, finding it, getting there ---------- */
        Note($"tide post {tidePostX:0},{tidePostY:0}, read from {PostReadSpot().x:0},{PostReadSpot().y:0}; harbour spot {MlHarbourSpotX},{MlHarbourSpotY}");
        state.hinted.Remove("marilaya"); state.charted = new() { "saltmere" }; chart = 0;
        state.inv["boat"] = 1;
        state.hinted["amihan"] = true; state.hinted["visitedAtoll"] = true;
        foreach (int f in At(160, 115)) yield return f;
        Check($"once you've a boat and know Amihan, the guide suggests the heart-shaped island ({string.Join(", ", goals.Select(g => g.Id))})", goals.Any(g => g.Id == "explore:marilaya"));
        foreach (int f in WalkUp(NoticeX, NoticeY + 8)) yield return f;
        Check($"a notice at Amihan Village's landing ({target?.Label})", target?.Id == "notice");
        foreach (int f in E()) yield return f;
        bool noticeWords = dlg?.Lines.Any(l => l.T.Contains("Aling Sela") && l.T.Contains("past Baga")) == true;
        foreach (int f in Talk()) yield return f;
        Check("it says who to ask for, and that Marilaya is far out east past Baga", noticeWords && state.Hinted("mlNotice") && !state.Hinted("marilaya"));
        // Out into its waters at the helm.
        foreach (int f in At(MlStart * T + 30, 300)) yield return f;
        state.aboard = true; state.boatX = player.X; state.boatY = player.Y; yield return 5;
        Check($"sailing into its waters finds Marilaya, and the map opens on its chart ({chart})", state.Hinted("marilaya") && toastMsg.Contains("Marilaya") && chart == 2);
        Check($"aboard, its waters are named ({AreaName()})", AreaName().StartsWith("Marilaya waters"));
        state.aboard = false;
        // The Set sail panel, by real clicks, from Pip's jetty to Marilaya's harbour.
        foreach (int f in At(SaltJettyX - 4, SaltJettyY + 1, "right")) yield return f;
        foreach (int f in E()) yield return f;
        Check($"Pip's jetty asks where to sail ({panel})", mode == "panel" && panel == "voyage");
        foreach (int f in Click("voyage:marilaya")) yield return f;
        foreach (int f in Fades()) yield return f;
        Check($"sailed to Marilaya's harbour ({player.X:0},{player.Y:0}, boat at {state.boatAt})", Dist(player.X, player.Y, MlJettyX + 4, MlJettyY) < 3 && state.boatAt == "marilaya" && InMarilaya);
        Check($"the music is the kalutang's ({WantedTrack()})", WantedTrack() == "marilaya");
        Inp.Tap(KeyboardKey.F); yield return 4;
        Check($"F at the harbour takes the helm ({state.boatX:0},{state.boatY:0})", Aboard && BoatCanStand(player.X, player.Y));
        Inp.Tap(KeyboardKey.R); yield return 4;
        Check("and R lands again", !Aboard && CanStand(player.X, player.Y));
        state.charted ??= new();
        foreach (int f in At(SelaX, SelaY + 16)) yield return f;
        Check("standing on it charts the island", state.charted.Contains("marilaya"));
        // The map's third chart, and its buttons by real clicks.
        TogglePanel("map"); yield return 6;
        Check($"the map opens on Marilaya's own chart with its places ({chart})", chart == 2 && Gfx.Seen.ContainsKey("dot:Harbour cove") && Gfx.Seen.ContainsKey("dot:Rock face"));
        pendingShot = "marilaya-01-chart"; yield return 3;
        foreach (int f in Click("Amihan")) yield return f;
        Check($"the Amihan chart is still Amihan's ({chart})", chart == 1 && mode == "panel");
        foreach (int f in Click("Marilaya")) yield return f;
        Check($"and back ({chart})", chart == 2);
        ClosePanels(); yield return 2;

        /* ---------- Before Aling Sela: brushing the rock face ---------- */
        var (cliffX, cliffY) = MlPlace("cliff");
        foreach (int f in WalkUp(cliffX, cliffY + 12)) yield return f;
        Check($"the rock face ({target?.Label})", target?.Id == "cliff");
        foreach (int f in E()) yield return f;
        Check($"you can start before meeting Aling Sela: the panel opens ({panel})", mode == "panel" && panel == "mlrock" && !MrMet);
        foreach (int f in Click("mlrock:sand")) yield return f;
        Check($"a loose shell on the sand is a beach observation, not the rock's ({mlRock?.Note})", mlRock?.Note.Contains("useful beach observation") == true && MlFossilsFound == 0);
        // Brushing: pad A held down, sweeping the pointer over the face.
        var face = Gfx.Seen["mlrock:face"];
        IEnumerable<int> Sweep(float x0, float y0, float x1, float y1)
        {
            Inp.HoldPad(GamepadButton.RightFaceDown, true);
            for (float y = y0; y <= y1; y += 26)
                for (float x = x0; x <= x1; x += 18) { Inp.ScriptMouse = new(x, y); yield return 1; }
            Inp.HoldPad(GamepadButton.RightFaceDown, false);
            yield return 2;
        }
        var f0 = Gfx.Seen["mlrock:fossil0"];
        foreach (int f in Sweep(f0.X - 10, f0.Y - 10, f0.X + f0.Width + 10, f0.Y + f0.Height + 10)) yield return f;
        Check($"brushing round a shell sketches it, saved at once ({mlRock?.Note})", state.Hinted("mr:fos:0") && SaveFile.Read()?.hinted.GetValueOrDefault("mr:fos:0") == true && mlRock?.Step == 0);
        pendingShot = "marilaya-02-brushing"; yield return 2;
        foreach (int f in Sweep(face.X + 10, face.Y + 6, face.X + face.Width - 10, face.Y + face.Height - 6)) yield return f;
        Check($"brushing the whole face finds the fossils and the old notch ({MlFossilsFound} fossils, notch {state.Hinted("mr:notch")})", MlFossilsFound >= 3 && state.Hinted("mr:notch") && state.Hinted("mr:fossil") && mlRock?.Step == 1);
        foreach (int f in Click("mlbed:A")) yield return f;
        Check($"the top layer first is explained ({mlRock?.Note})", mlRock?.Order.Count == 0 && mlRock.Note.Contains("last"));
        foreach (int f in Click("mlbed:C")) yield return f;
        foreach (int f in Click("mlbed:A")) yield return f;
        Check($"soil before limestone is explained too ({mlRock?.Note})", mlRock?.Order.Count == 1 && mlRock.Note.Contains("resting on top"));
        foreach (int f in Click("mlbed:B")) yield return f;
        foreach (int f in Click("mlbed:A")) yield return f;
        Check("oldest to youngest: C, B, A, and the clue kept at once", mlRock?.Step == 2 && state.Hinted("mr:layers") && SaveFile.Read()?.hinted.GetValueOrDefault("mr:layers") == true);
        Check($"the panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        pendingShot = "marilaya-03-rock"; yield return 2;
        foreach (int f in Click("Done")) yield return f;
        Check("its trail sign goes up, and the Case board's fifth story opens on its own", TrailSigns().First().done && CaseStories().Any(s => s.id == "marilaya" && s.open));

        /* ---------- Aling Sela ---------- */
        var sw = SelaWalk();
        foreach (int f in At(sw.X, sw.Y + 8)) yield return f;
        Check($"Aling Sela is at her house ({target?.Label})", target?.Id == "sela" && target.Label == "Talk to Aling Sela");
        foreach (int f in E()) yield return f;
        bool legend = dlg?.Lines.Any(l => l.T.Contains("Batumbakal")) == true && dlg.Lines.Any(l => l.T.Contains("Other people tell it other ways"))
            && dlg.Lines.Any(l => l.T.Contains("discovery trail")) && dlg.Lines.Any(l => l.T.Contains("already"));
        pendingShot = "marilaya-04-sela"; yield return 2;
        foreach (int f in Talk()) yield return f;
        Check("she tells her lola's telling, asks for a field partner for the children's trail, and notices what you've done", legend);
        Check("story 5 begins, and she lends you the survey compass", MrMet && state.Hinted("mr:legend") && Has("survey_compass") > 0 && Wears("survey_compass"));
        Check($"the guide follows the next stop ({string.Join(" | ", goals.Where(g => g.Id == "mr:case").Select(g => g.Text))})", goals.Any(g => g.Id == "mr:case" && g.Text.Contains("cave")));
        TogglePanel("case"); yield return 4;
        foreach (int f in Click("The heart that rose")) yield return f;
        Check($"the Case board's fifth tab ({caseTab}), its tabs fitting ({lastCaseTabRight:0})", caseTab == "marilaya" && lastCaseTabRight < Gfx.LW - 120);
        ClosePanels(); yield return 2;

        /* ---------- The cave ---------- */
        var (caveX, caveY) = MlPlace("cave");
        foreach (int f in WalkUp(caveX, caveY + 9, 8)) yield return f;
        Check($"the cave mouth ({target?.Label})", target?.Id == "cave");
        foreach (int f in E()) yield return f;
        foreach (int f in Fades()) yield return f;
        Check($"inside the cave ({scene})", scene == MlCaveKey && !InOwnHouse && SceneBuilds().Count == 0);
        pendingShot = "marilaya-05-cave"; yield return 2;
        var caveGoal = goals.FirstOrDefault(g => g.Id == "mr:case");
        Check($"inside, the guide points at the first spot, not the door ({Route(caveGoal)?.Place})", caveGoal?.Scene == MlCaveKey && Route(caveGoal)?.Place == "The dripping stalactite");
        TogglePanel("map"); yield return 4;
        Check($"the map opens on Marilaya's chart from inside the cave ({chart})", chart == 2);
        ClosePanels(); yield return 2;
        foreach (int f in At(CaveModelX, CaveModelY + 10)) yield return f;
        foreach (int f in E()) yield return f;
        Check($"the cave model opens ({panel})", mode == "panel" && panel == "mlcavemodel");
        Inp.Hold(KeyboardKey.E, true); yield return 120; Inp.Hold(KeyboardKey.E, false); yield return 2;
        float half = mlCaveModel?.T ?? 0;
        Check($"holding the button runs time forward, and letting go stops it ({half:0.00})", half > 0.1f && half < 1);
        yield return 30;
        Check("it stays where you stopped", MathF.Abs((mlCaveModel?.T ?? 0) - half) < 0.001f);
        pendingShot = "marilaya-06-cave-model"; yield return 2;
        Inp.Hold(KeyboardKey.E, true); yield return 500; Inp.Hold(KeyboardKey.E, false); yield return 3;
        Check($"run to the end, the model's story is kept, but the clue waits for the three spots ({mlCaveModel?.Note})", state.Hinted("mr:cavemodel") && !state.Hinted("mr:karst"));
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        foreach (var (id, x, y) in new[] { ("cavedrip", CaveDripX, CaveDripY + 28), ("caveshells", CaveShellX, CaveShellY + 32), ("cavebats", CaveRopeX - 10, 80f) })
        {
            foreach (int f in At(x, y)) yield return f;
            Check($"the {id[4..]} ({target?.Label})", target?.Id == id);
            foreach (int f in E()) yield return f;
            foreach (int f in Talk()) yield return f;
        }
        Check("all three noted, and the clue: water's patient work", state.Hinted("mr:cave1") && state.Hinted("mr:cave2") && state.Hinted("mr:cave3") && state.Hinted("mr:karst"));
        foreach (int f in At(CaveColumnX, CaveColumnY + 12)) yield return f;
        Check($"the column is for looking at ({target?.Label})", target?.Id == "cavecolumn");
        foreach (int f in E()) yield return f;
        bool dontTouch = dlg?.Lines.Any(l => l.T.Contains("don't touch")) == true;
        foreach (int f in Talk()) yield return f;
        Check("touching it is gently refused", dontTouch);
        foreach (int f in At(CaveRopeX + 8, 90)) yield return f;
        Check("past the rope is closed to visitors (the bats)", !CanStand(CaveRopeX + 2, 90) && CanStand(CaveRopeX - 6, 90));
        foreach (int f in At(RoomDoorWX, RoomDoorWY - 6, "down")) yield return f;
        Inp.Hold(KeyboardKey.Down, true); yield return 20; Inp.Hold(KeyboardKey.Down, false);
        foreach (int f in Fades()) yield return f;
        Check($"out of the door, back at the cave mouth ({scene}, {player.X:0},{player.Y:0})", scene == "world" && Dist(player.X, player.Y, caveX, caveY + 3) < 6 && CanStand(player.X, player.Y));

        /* ---------- The hot springs ---------- */
        foreach (int f in WalkUp(DryerAt.x, DryerAt.y + 8)) yield return f;
        Check($"the dryer ({target?.Label})", target?.Id == "dryer");
        foreach (int f in E()) yield return f;
        Check($"the gauges come first ({toastMsg})", mode == "play" && toastMsg.Contains("gauges"));
        foreach (int f in WalkUp(SpringGaugeAt.x, SpringGaugeAt.y + 9, 6)) yield return f;
        Check($"the spring's gauge ({target?.Label})", target?.Id == "gauge1");
        foreach (int f in E()) yield return f;
        bool hot = dlg?.Lines.Any(l => l.T.Contains($"{SpringC} degrees") && l.T.Contains("hydrogen sulfide")) == true;
        foreach (int f in Talk()) yield return f;
        foreach (int f in WalkUp(StreamGaugeAt.x, StreamGaugeAt.y + 8, 8)) yield return f;
        Check($"the stream's gauge ({target?.Label})", target?.Id == "gauge2");
        foreach (int f in E()) yield return f;
        foreach (int f in Talk()) yield return f;
        Check("both read (the spring hot, smelling of hydrogen sulfide)", hot && state.Hinted("mr:gauge1") && state.Hinted("mr:gauge2"));
        pendingShot = "marilaya-07-springs"; yield return 2;
        foreach (int f in WalkUp(DryerAt.x, DryerAt.y + 8)) yield return f;
        foreach (int f in E()) yield return f;
        Check($"Lorna's night record ({panel})", mode == "panel" && panel == "mlsprings" && mlSprings?.Step == 0);
        foreach (int f in Click("mlsprings:line1")) yield return f;
        Check($"the stream's line cooled with the air ({mlSprings?.Note})", mlSprings?.LineOk == false && mlSprings.Note.Contains("cooled"));
        foreach (int f in Click("mlsprings:line0")) yield return f;
        Check($"the spring's stayed hot all night: heat from below ({mlSprings?.Note})", mlSprings?.LineOk == true && mlSprings.Note.Contains("from below") && state.Hinted("mr:log"));
        pendingShot = "marilaya-08-night-record"; yield return 2;
        foreach (int f in Click("Next: the dryer")) yield return f;
        foreach (int f in Click("mlsprings:valve1")) yield return f;
        Check($"the stream's pipe leaves the trays cool ({mlSprings?.Note})", !state.Hinted("mr:springs") && mlSprings?.Note.Contains("Try the other pipe") == true);
        foreach (int f in Click("mlsprings:valve0")) yield return f;
        yield return 60;
        Check("the spring's pipe warms the dryer, and the clue: heat under Tindig", state.Hinted("mr:springs") && mlSprings?.Note.Contains("never touches the food") == true);
        pendingShot = "marilaya-09-dryer"; yield return 2;
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        foreach (int f in Click("Done")) yield return f;
        // The dryer works for you now: raw fish in, daing out two hours later, whatever the weather.
        state.inv["galunggong"] = 2;
        foreach (int f in E()) yield return f;
        Check($"two fish on the dryer's trays ({DryerLoad})", DryerLoad == 2 && Has("galunggong") == 0);
        Advance(125, quiet: true); yield return 2;
        int daing0 = Has("daing");
        foreach (int f in E()) yield return f;
        Check("two hours later, daing", Has("daing") == daing0 + 2 && DryerLoad == 0);

        /* ---------- The tide post (and Mang Ambo's sandbar) ---------- */
        SetHighTide(); yield return 2;
        var (prx, pry) = PostReadSpot();
        foreach (int f in At(prx, pry)) yield return f;
        Check($"the tide post, read from Maniwaya's sand ({target?.Label} / {target?.AltLabel})", target?.Id == "tidepost" && target.AltType == "mlwait" && target.AltLabel == "Wait for low tide");
        foreach (int f in E()) yield return f;
        Check($"its panel ({panel})", mode == "panel" && panel == "mlpost");
        var water = Gfx.Seen["mlpost:water"]; var post = Gfx.Seen["mlpost:post"];
        Inp.ScriptMouse = new(post.X + post.Width / 2, post.Y + post.Height - 10); Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"clicking under the water isn't the water line ({mlPost?.Note})", mlPost?.Read == false && mlPost.Note.Contains("under the water"));
        Inp.ScriptMouse = new(water.X + water.Width / 2, water.Y + water.Height / 2); Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"high tide read at the water line, and kept at once ({mlPost?.Note})", state.Hinted("mr:high") && !state.Hinted("mr:low") && state.gifts.ContainsKey("mr_highMark"));
        pendingShot = "marilaya-10-post-high"; yield return 2;
        foreach (int f in Click("Done")) yield return f;
        Inp.Tap(KeyboardKey.F); yield return 4;
        foreach (int f in Fades()) yield return f;
        Check($"waiting by the post brings the low tide, and says how long it took ({SeaLevel:0.00}, {toastMsg})", SeaLevel <= TideLowMark && toastMsg.Contains("low tide") && toastMsg.Contains(" h "));
        foreach (int f in E()) yield return f;
        water = Gfx.Seen["mlpost:water"];
        var bar = Gfx.Seen["mlpost:bar"];
        Inp.ScriptMouse = new(bar.X + bar.Width / 2, bar.Y + bar.Height / 2); Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"the sand's edge is no good as a mark ({mlPost?.Note})", mlPost?.Read == false && mlPost.Note.Contains("post stays put"));
        Inp.ScriptMouse = new(water.X + water.Width / 2, water.Y + water.Height / 2); Inp.ScriptClickNext = true; yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"low tide too, and the clue: the sandbar that comes and goes ({mlPost?.Note})", state.Hinted("mr:low") && state.Hinted("mr:tide") && mlPost.Note.Contains("the water moved, the post didn't"));
        pendingShot = "marilaya-11-post-both"; yield return 2;
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        foreach (int f in Click("Done")) yield return f;
        Check("every stop done: the guide sends you to the shore model", MrStopsDone && goals.Any(g => g.Id == "mr:case" && g.Title == "The shore model"));
        // Mang Ambo's walk out on the bar, while it's out (the low saddle near the beach dries last).
        Note($"the bar joins the beach below sea level {barCutLevel:0.00}");
        SetLowTide(); yield return 2;
        var amb = MlWalk("ambo");
        foreach (int f in At(amb.X, amb.Y + 8)) yield return f;
        Check($"Mang Ambo offers tide-pool guidance at low tide ({target?.Label})", target?.Id == "ambo" && target.Label == "Ask Mang Ambo about the tide pools");
        foreach (int f in E()) yield return f;
        bool margin = dlg?.Lines.Any(l => l.T.Contains("Be back on the beach by")) == true;
        foreach (int f in Talk()) yield return f;
        Check("he gives a time to be back by, with a margin", margin && state.gifts.ContainsKey("mr_walkBy"));
        for (int i = 0; i < MlPools.Length; i++)
        {
            var (px, py) = MlPoolAt(i);
            foreach (int f in At(px, py + 7)) yield return f;
            Check($"the {MlPools[i].name} in its tide pool, on the dry bar ({target?.Label})", target?.Id == $"pool{i}");
            foreach (int f in E()) yield return f;
            foreach (int f in Talk()) yield return f;
        }
        Check("all three seen and left be: Mang Ambo's coins", state.Hinted("mr:pools"));
        pendingShot = "marilaya-12-sandbar"; yield return 2;
        // Caught out on the bar as the tide comes back: someone comes for you, unhurt.
        var (endX, endY) = MlPoolAt(2);
        foreach (int f in At(endX, endY + 7)) yield return f;
        float hp0 = state.hp;
        SetHighTide(); yield return 6;
        int btx = (int)MathF.Floor(player.X / T), bty = (int)MathF.Floor((player.Y - 1.5f) / T);
        Note($"on the bar? tile {btx},{bty} bar {OnSandbarTile(btx, bty)} reaches {BarReachesShore(btx, bty)} sea {SeaLevel:0.00} mode {mode} cut {barCutOffT:0.00}");
        Check($"cut off by the tide ({toastMsg})", toastMsg.Contains("someone's coming"));
        for (int i = 0; i < 200 && mode is "play" or "fade"; i++) { yield return 2; if (mode == "dialogue") break; }
        bool lesson = dlg?.Lines.Any(l => l.T.Contains("low part of a bar first")) == true;
        foreach (int f in Talk()) yield return f;
        Check($"a banca brings you back to the beach, unhurt, with the lesson ({player.X:0},{player.Y:0})", lesson && state.hp == hp0 && !OnSandbarTile((int)(player.X / T), (int)((player.Y - 1.5f) / T)) && CanStand(player.X, player.Y));

        /* ---------- The shore model ---------- */
        foreach (int f in WalkUp(ModelAt.x, ModelAt.y + 10)) yield return f;
        Check($"the shore model in the town square ({target?.Label})", target?.Id == "model");
        foreach (int f in E()) yield return f;
        Check($"its panel ({panel})", mode == "panel" && panel == "mlmodel");
        foreach (int f in Click("Quake")) yield return f;
        Check($"a quake lifts the land in a moment ({mlModel?.Note})", mlModel?.Playing == "quake" && mlModel.Note.Contains("Bohol"));
        foreach (int f in Click("mlobs:0")) yield return f;
        foreach (int f in Click("mlmech:1")) yield return f;
        Check($"the post's readings don't go with the waves: it replays them ({mlModel?.Note})", mlModel?.Placed[0] == -1 && mlModel.Playing == "waves" && mlModel.Note.Contains("Watch"));
        for (int i = 0; i < 3; i++)
        {
            foreach (int f in Click($"mlobs:{i}")) yield return f;
            foreach (int f in Click($"mlmech:{MlObservations[i].mech}")) yield return f;
        }
        Check("all three placed, and kept at once", MrSorted && SaveFile.Read()?.hinted.GetValueOrDefault("mr:theory") == true);
        pendingShot = "marilaya-13-model"; yield return 2;
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        foreach (int f in Click("Show Aling Sela")) yield return f;
        bool notOneNight = dlg?.Lines.Any(l => l.T.Contains("Just not in one night")) == true;
        foreach (int f in Talk()) yield return f;
        Check("the island did rise from the sea, just not in one night", notOneNight);
        Check($"next, the summit ({string.Join(" | ", goals.Where(g => g.Id == "mr:case").Select(g => g.Text))})", goals.Any(g => g.Id == "mr:case" && g.Text.Contains("heliotrope")));

        /* ---------- The survey ---------- */
        foreach (int f in WalkUp(CairnAt.x, CairnAt.y + 9)) yield return f;
        Check($"Jun at the top with the heliotrope ({target?.Label})", target?.Label == "Set up the mirror with Jun");
        foreach (int f in E()) yield return f;
        foreach (int f in Talk()) yield return f;
        Check("the heliotrope's up, flashing in the sun", state.Hinted("mr:mirror") && MirrorFlashing);
        pendingShot = "marilaya-14-summit"; yield return 2;
        IEnumerable<int> Aim(float bearing)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                float d = AngleDiff(bearing, mlScope.Bearing);
                int fives = (int)(MathF.Abs(d) / 5);
                for (int i = 0; i < fives; i++) foreach (int f in Click(d > 0 ? "+5" : "-5", 2)) yield return f;
                d = AngleDiff(bearing, mlScope.Bearing);
                int ones = (int)MathF.Abs(d);
                for (int i = 0; i < ones; i++) foreach (int f in Click(d > 0 ? "+1" : "-1", 2)) yield return f;
                d = AngleDiff(bearing, mlScope.Bearing);
                int tenths = (int)MathF.Round(MathF.Abs(d) * 10);
                for (int i = 0; i < Math.Min(tenths, 12); i++) foreach (int f in Click(d > 0 ? "+0.1" : "-0.1", 2)) yield return f;
            }
        }
        foreach (int f in WalkUp(DatumX, DatumY + 9)) yield return f;
        Check($"the survey stone ({target?.Label})", target?.Label == "Sight from the survey stone");
        foreach (int f in E()) yield return f;
        Check($"the sight opens, wanting Baltasar's target first ({panel}, {mlScope?.Want}), and the E that opened it records nothing", mode == "panel" && panel == "mlscope" && mlScope?.Want == "B" && !state.Hinted("mr:ab") && mlScope.Note == "");
        // Pointing at Melchor instead.
        var melchor = MlIslets[5];
        foreach (int f in Aim(BearingTo(DatumX, DatumY, MlWx(melchor.cx), MlWy(melchor.cy)))) yield return f;
        foreach (int f in Click("Record bearing")) yield return f;
        Check($"Melchor isn't the station, and it says where to look ({mlScope?.Note})", !state.Hinted("mr:ab") && mlScope?.Note.Contains("Melchor") == true && mlScope.Note.Contains("smallest"));
        pendingShot = "marilaya-15-sight-wrong"; yield return 2;
        foreach (int f in Aim(TrueBearing("A", "B"))) yield return f;
        foreach (int f in Click("Record bearing")) yield return f;
        Check($"Baltasar's target recorded ({state.gifts.GetValueOrDefault("mr_ab") / 10f:0.0} vs {TrueBearing("A", "B"):0.0})", state.Hinted("mr:ab") && mlScope?.Want == "C");
        Check($"the stone's bearing to Baltasar is close to the real Balanacan to Baltasar azimuth ({TrueBearing("A", "B"):0}, real about 189)", MathF.Abs(TrueBearing("A", "B") - 189.2f) < 8);
        // Clouds: no flash to sight; wait for the sun.
        state.weather = "rain"; yield return 2;
        foreach (int f in Aim(TrueBearing("A", "C"))) yield return f;
        foreach (int f in Click("Record bearing")) yield return f;
        Check($"no flash under cloud ({mlScope?.Note})", !state.Hinted("mr:ac") && mlScope?.Note.Contains("needs the sun") == true);
        foreach (int f in Click("Wait for the sun")) yield return f;
        foreach (int f in Fades()) yield return f;
        Check($"waiting brings the sun back ({state.weather}, {toastMsg})", MirrorFlashing && mode == "play");
        ClearSkies(); SetNight(false); yield return 2;
        foreach (int f in E()) yield return f;
        Check($"back at the stone, it wants Tindig now ({mlScope?.Want})", mlScope?.Want == "C");
        foreach (int f in Aim(TrueBearing("A", "C"))) yield return f;
        pendingShot = "marilaya-16-sight-flash"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"E records too: Tindig's top from the stone ({mlScope?.Note})", state.Hinted("mr:ac"));
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        ClosePanels(); yield return 2;
        Check($"next, Baltasar ({string.Join(" | ", goals.Where(g => g.Id == "mr:case").Select(g => g.Text))})", goals.Any(g => g.Id == "mr:case" && g.Text.Contains("Baltasar")));
        foreach (int f in WalkUp(BaltasarX, BaltasarY + 9, 14)) yield return f;
        Check($"Baltasar's mark ({target?.Label})", target?.Id == "baltasar");
        foreach (int f in E()) yield return f;
        foreach (int f in Aim(TrueBearing("B", "C"))) yield return f;
        foreach (int f in Click("Record bearing")) yield return f;
        Check("and from Baltasar", state.Hinted("mr:bc") && MrBearingsDone);
        ClosePanels(); yield return 2;
        var cross = Intersect(DatumX, DatumY, state.gifts["mr_ac"] / 10f, BaltasarX, BaltasarY, state.gifts["mr_bc"] / 10f);
        Check($"your two lines cross at the summit ({cross?.x:0},{cross?.y:0} vs {TindigX:0},{TindigY:0})", cross is var (cx2, cy2) && Dist(cx2, cy2, TindigX, TindigY) < 40);
        foreach (int f in At(sw.X, sw.Y + 8)) yield return f;
        Check($"plotting with Aling Sela ({target?.Label})", target?.Label == "Plot the chart with Aling Sela");
        foreach (int f in E()) yield return f;
        Check($"the plot ({panel})", mode == "panel" && panel == "mlplot");
        foreach (int f in Click("Draw the line from A")) yield return f;
        yield return 40;
        foreach (int f in Click("Draw the line from B")) yield return f;
        yield return 70;
        pendingShot = "marilaya-17-plot"; yield return 2;
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        int coins0 = state.coins;
        foreach (int f in Click("Ink the chart")) yield return f;
        Check("the win is kept before Aling Sela says a word", MrDone && state.coins == coins0 + 200 && SaveFile.Read()?.hinted.GetValueOrDefault("mr:done") == true && mode == "dialogue");
        bool datumWords = dlg?.Lines.Any(l => l.T.Contains("datum")) == true && dlg.Lines.Any(l => l.T.Contains("not how high"));
        foreach (int f in Talk()) yield return f;
        Check("she explains the datum, and that triangulation fixes where, not how high", datumWords);
        Check($"the card ({mode})", mode == "marilayacard");
        pendingShot = "marilaya-18-card"; yield return 2;
        Check($"it fits ({lastGoldBottom:0})", lastGoldBottom <= Gfx.LH);
        yield return 40;
        foreach (int f in Click("Salamat po!")) yield return f;
        Check("its button closes it, and every trail sign is up", mode == "play" && TrailSigns().All(t => t.done));
        TogglePanel("case"); yield return 3; caseTab = "marilaya"; yield return 3;
        Check($"the case is closed on the board, and fits ({lastCaseBottom:0})", lastCaseBottom <= Gfx.LH);
        pendingShot = "marilaya-19-case-closed"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- After: the cache hunt ---------- */
        foreach (int f in At(sw.X, sw.Y + 8)) yield return f;
        foreach (int f in E()) yield return f;
        foreach (int f in Talk()) yield return f;
        Check($"Aling Sela sets a cache hunt by bearing ({CacheBearing:0} degrees, {CacheMetres} m)", state.Hinted("mr:cacheAsked") && goals.Any(g => g.Id == "mr:cache"));
        foreach (int f in At(DatumX + 20, DatumY + 30)) yield return f;
        pendingShot = "marilaya-20-cache-compass"; yield return 2;
        foreach (int f in WalkUp(CacheX, CacheY, 8)) yield return f;
        Check($"where bearing and distance match, you can dig ({target?.Label})", target?.Id == "cachedig");
        foreach (int f in E()) yield return f;
        foreach (int f in Talk()) yield return f;
        Check("the cache", state.Hinted("mr:cache"));

        /* ---------- Kalutang with Mang Iyo ---------- */
        var iyo = MlWalk("iyo");
        foreach (int f in At(iyo.X, iyo.Y + 8)) yield return f;
        Check($"Mang Iyo in the town square ({target?.Label})", target?.Id == "iyo");
        foreach (int f in E()) yield return f;
        foreach (int f in Talk()) yield return f;
        Check($"kalutang practice ({panel})", mode == "panel" && panel == "mlkalutang");
        bool missed = false;
        for (int round = 0; round < KalPhrases.Length && mlKalutang != null; round++)
        {
            for (int i = 0; i < 400 && mlKalutang.Listening; i++) yield return 1;
            var phrase = KalPhrases[mlKalutang.Round];
            if (!missed)
            {
                missed = true;
                Inp.Tap(phrase[0] ? KeyboardKey.F : KeyboardKey.E); yield return 4;
                Check($"a wrong bar: he plays it again, slower ({mlKalutang.Note})", mlKalutang.Listening && mlKalutang.Slow && mlKalutang.Round == 0);
                for (int i = 0; i < 500 && mlKalutang.Listening; i++) yield return 1;
            }
            foreach (bool hiNote in phrase) { Inp.Tap(hiNote ? KeyboardKey.E : KeyboardKey.F); yield return 6; }
        }
        yield return 4;
        Check($"every phrase played back ({mlKalutang?.Round})", state.Hinted("mr:kalutang") && mlKalutang?.Round == KalPhrases.Length);
        pendingShot = "marilaya-21-kalutang"; yield return 2;
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        foreach (int f in Click("Done")) yield return f;
        state.clock = 16 * 60; yield return 3;
        foreach (int f in At(MlPlace("plaza").x, MlPlace("plaza").y + 30)) yield return f;
        pendingShot = "marilaya-22-town-practice"; yield return 2;

        /* ---------- Aling Nita's butterflies ---------- */
        var nita = MlWalk("nita");
        foreach (int f in At(nita.X, nita.Y + 8)) yield return f;
        Check($"Aling Nita ({target?.Label})", target?.Id == "nita");
        foreach (int f in E()) yield return f;
        foreach (int f in Talk()) yield return f;
        foreach (int f in Click("leaf:2")) yield return f;
        Check($"mango leaves won't do ({mlNursery?.Note})", mlNursery?.Note.Contains("won't touch") == true);
        foreach (int f in Click("leaf:0")) yield return f;
        foreach (int f in Click("Next")) yield return f;
        for (int i = 0; i < 4; i++) foreach (int f in Click($"stage:{i}")) yield return f;
        Check($"every stage looked at ({mlNursery?.Note})", mlNursery?.Seen.Count == 4);
        pendingShot = "marilaya-23-butterflies"; yield return 2;
        foreach (int f in Click("Next")) yield return f;
        yield return 300;
        Check("a butterfly comes out, and Aling Nita thanks you", state.Hinted("mr:nursery"));
        Check($"its panel fits ({lastMlPanelBottom:0})", lastMlPanelBottom <= Gfx.LH);
        foreach (int f in Click("Done")) yield return f;

        /* ---------- The storytellers gather ---------- */
        Check("no gathering until every story is done", !GatherDue && !goals.Any(g => g.Id == "mr:gather"));
        state.flags.ended = true; state.hinted["moonReturned"] = true; state.hinted["rs:done"] = true; state.hinted["mg:done"] = true;
        yield return 3;
        Check($"with all five done, the storytellers wait at Marilaya ({string.Join(", ", goals.Select(g => g.Id))})", GatherDue && goals.Any(g => g.Id == "mr:gather"));
        foreach (int f in At(sw.X, sw.Y + 8)) yield return f;
        Check($"Aling Sela gathers them ({target?.Label})", target?.Label == "Gather the storytellers");
        foreach (int f in E(6)) yield return f;
        pendingShot = "marilaya-24-gathering"; yield return 2;
        bool everyone = new[] { "Tomas", "Lola Pacing", "Lira", "Manay Mila", "Ben", "Ma'am Isay", "Niko", "Pip" }.All(n => dlg?.Lines.Any(l => l.S == n) == true);
        foreach (int f in Talk()) yield return f;
        foreach (int f in Fades()) yield return f;
        Check("every storyteller has a closing word", everyone);
        Check($"then home with Tomas ({player.X:0},{player.Y:0}, {state.boatAt}), and the last card", state.Hinted("mr:gathered") && Dist(player.X, player.Y, SaltJettyX - 4, SaltJettyY + 1) < 3 && mode == "marilayacard" && mlCardKind == "gather");
        yield return 40;
        foreach (int f in Click("Keep exploring")) yield return f;
        Check("free play goes on", mode == "play" && !GatherDue);

        /* ---------- Fainting, its pages ---------- */
        foreach (int f in WalkUp(caveX, caveY + 9, 8)) yield return f;
        foreach (int f in E()) yield return f;
        foreach (int f in Fades()) yield return f;
        state.hp = 1; Faint(); yield return 3;
        foreach (int f in Fades()) yield return f;
        Check($"fainting in the cave wakes you outdoors at Aling Sela's ({scene}, {player.X:0},{player.Y:0})", scene == "world" && Dist(player.X, player.Y, MlWakeX, MlWakeY) < 3);
        foreach (int f in At(MlJettyX + 4, MlJettyY)) yield return f;
        state.hp = 1; Faint(); yield return 3;
        foreach (int f in Fades()) yield return f;
        Check($"fainting on Marilaya wakes you at Aling Sela's ({player.X:0},{player.Y:0}), the boat at the harbour", Dist(player.X, player.Y, MlWakeX, MlWakeY) < 3 && state.boatAt == "marilaya" && CanStand(player.X, player.Y));
        OpenAtlas("marilaya"); yield return 4;
        Check($"the island guide's page fits ({lastAtlasBottom:0}), its list too ({lastAtlasListBottom:0})", atlasRegion == "marilaya" && lastAtlasBottom <= (Gfx.LH + 664) / 2 - 18 && lastAtlasListBottom <= Gfx.LH - 20);
        pendingShot = "marilaya-25-atlas"; yield return 2;
        OpenAtlas("marilayasea"); yield return 4;
        Check($"and its sea's ({lastAtlasBottom:0})", atlasRegion == "marilayasea" && lastAtlasBottom <= (Gfx.LH + 664) / 2 - 18);
        ClosePanels(); yield return 2;
        TogglePanel("dex"); yield return 3;
        dexTab = "log";
        logPage = Array.FindIndex(Data.Biomes, b => b.Id == "marilaya"); yield return 4;
        Check($"the Fish log has a Marilaya page ({logPage}) that fits ({lastDexBottom:0})", logPage == 7 && lastDexBottom <= Gfx.LH);
        ClosePanels(); yield return 2;
        Check("the title tour stops at Marilaya", TitleStops.Any(s => s.name == "Marilaya"));
        Check($"no panel note was cut short (at most {mlNoteCutMost} lines over)", mlNoteCutMost == 0);
        Note("Marilaya done");
        foreach (int f in MarilayaReviewScript()) yield return f;
    }

    // A check for each finding of Codex's review of 1.23's rework (FESH_MARILAYA_TEST=review runs only these).
    IEnumerable<int> MarilayaReviewScript()
    {
        Note("Codex's review of the Marilaya rework");
        Inp.ScriptMouse = Offscreen;
        IEnumerable<int> Talk()
        {
            for (int i = 0; i < 300 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
            for (int i = 0; i < 120 && mode == "fade"; i++) yield return 2;
            yield return 2;
        }
        IEnumerable<int> Click(string key, int wait = 3)
        {
            bool found = ClickButton(key);
            if (!found) Note($"(no button {key} on screen)");
            yield return wait;
            Inp.ScriptMouse = Offscreen;
            yield return 1;
        }
        IEnumerable<int> At(float x, float y, string face = "up")
        {
            player.X = x; player.Y = y; player.Face = face; player.Moving = false;
            yield return 3;
        }
        IEnumerable<int> Fades() { for (int i = 0; i < 120 && mode == "fade"; i++) yield return 2; yield return 3; }
        IEnumerable<int> Fresh(params string[] flags)
        {
            state = new State { created = true, look = new Look { name = "Review" } };
            state.hinted["chapters"] = true; state.flags.metTomas = true; state.inv["boat"] = 1;
            state.hinted["amihan"] = true; state.hinted["marilaya"] = true; state.hinted["mr:met"] = true; state.hinted["mr:legend"] = true;
            foreach (var k in flags) state.hinted[k] = true;
            mode = "play"; StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); SetNight(false);
            foreach (int f in Talk()) yield return f;
            player.X = MlJettyX + 4; player.Y = MlJettyY; yield return 3;
        }
        // Quitting mid-dialogue: the window-close save, then loading it again.
        IEnumerable<int> QuitAndLoad()
        {
            Save();
            var loaded = SaveFile.Read();
            state = loaded; mode = "play"; StartGame(false); quietWildlife = true; standStill = true;
            foreach (int f in Talk()) yield return f;
        }

        /* MUST: the cave's clue was given in the last spot's dialogue callback, so quitting there lost it for good. */
        foreach (int f in Fresh("mr:cave1", "mr:cave2", "mr:cavemodel")) yield return f;
        var (cvx, cvy) = MlPlace("cave");
        foreach (int f in At(cvx, cvy + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (int f in Fades()) yield return f;
        foreach (int f in At(CaveRopeX - 10, 80)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"the bats' dialogue is open ({mode})", mode == "dialogue");
        foreach (int f in QuitAndLoad()) yield return f;
        Check("quitting during the last cave spot's words still gives the cave's clue", state.Hinted("mr:karst"));

        /* MUST: the tide pools' reward the same way. */
        foreach (int f in Fresh("mr:pool:star", "mr:pool:cucumber")) yield return f;
        SetLowTide(); yield return 2;
        var (p2x, p2y) = MlPoolAt(2);
        foreach (int f in At(p2x, p2y + 7)) yield return f;
        int coins0 = state.coins;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"the hermit crab's words are open ({mode})", mode == "dialogue");
        foreach (int f in QuitAndLoad()) yield return f;
        Check($"quitting during the last tide pool's words still pays Mang Ambo's coins ({state.coins - coins0})", state.Hinted("mr:pools") && state.coins >= coins0 + 40);

        /* MUST: sighting Tindig before the heliotrope's up offered thirty hours of waiting that could never work. */
        foreach (int f in Fresh("mr:layers", "mr:karst", "mr:springs", "mr:tide", "mr:theory", "mr:ab")) yield return f;
        state.gifts["mr_ab"] = (int)(TrueBearing("A", "B") * 10);
        foreach (int f in At(DatumX, DatumY + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"the sight wants Tindig ({mlScope?.Want})", mode == "panel" && mlScope?.Want == "C");
        Gfx.Seen.Remove("Wait for the sun");
        foreach (int f in Click("Record bearing")) yield return f;
        yield return 3;
        Check($"before the heliotrope's up it sends you to Jun, and offers no waiting ({mlScope?.Note})", mlScope?.Note.Contains("Jun") == true && !Gfx.Seen.ContainsKey("Wait for the sun"));
        ClosePanels(); yield return 2;
        // At night the station offers to wait for daylight.
        state.hinted["mr:mirror"] = true;
        SetNight(true); yield return 3;
        foreach (int f in At(DatumX, DatumY + 9)) yield return f;
        Check($"at night the stone offers to wait for daylight ({target?.AltLabel})", target?.Id == "datum" && target.AltType == "mlsun");
        SetNight(false); yield return 2;

        /* SHOULD: Mang Ambo's call was used up while you were ashore, and keyed to the day. */
        foreach (int f in Fresh()) yield return f;
        SetLowTide(); yield return 2;
        state.gifts["mr_walkDay"] = state.day; state.gifts["mr_walkBy"] = AbsNow - 5;
        foreach (int f in At(MlWalk("ambo").X, MlWalk("ambo").Y + 10)) yield return f;
        yield return 4;
        var (p0x, p0y) = MlPoolAt(0);
        foreach (int f in At(p0x, p0y + 7)) yield return f;
        yield return 3;
        Check($"walking out after the time to be back, Mang Ambo still calls you ({toastMsg})", toastMsg.Contains("Back to the beach"));
        // His storm remark, which the shore model's card quotes, is something he actually says.
        foreach (int f in At(MlWalk("ambo").X, MlWalk("ambo").Y + 8)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        bool storm = dlg?.Lines.Any(l => l.S == "Mang Ambo" && l.T.Contains("storm")) == true;
        foreach (int f in Talk()) yield return f;
        Check("Mang Ambo says the storm thing the card quotes", storm);
        // The time to be back by shows while you're out on the bar.
        SetLowTide(); yield return 2;
        foreach (int f in At(MlWalk("ambo").X, MlWalk("ambo").Y + 8)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (int f in Talk()) yield return f;
        foreach (int f in At(p0x, p0y + 7)) yield return f;
        Check($"out on the bar, the location line says when to be back ({AreaName()})", AreaName().Contains("back by"));

        /* SHOULD: the model's buttons didn't place a held card, and placed cards were forgotten on closing. */
        foreach (int f in Fresh("mr:layers", "mr:karst", "mr:springs", "mr:tide")) yield return f;
        foreach (int f in At(ModelAt.x, ModelAt.y + 10)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (int f in Click("mlobs:0")) yield return f;
        foreach (int f in Click("Tide")) yield return f;
        Check($"with a card picked up, the handle's own button places it ({mlModel?.Placed[0]})", mlModel?.Placed[0] == 0);
        ClosePanels(); yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("closing and opening it again keeps the card where you put it", mlModel?.Placed[0] == 0);
        ClosePanels(); yield return 2;

        /* SHOULD: on Lorna's graph the last line drawn won a click meant for a nearer one. */
        foreach (int f in Fresh("mr:gauge1", "mr:gauge2")) yield return f;
        foreach (int f in At(DryerAt.x, DryerAt.y + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (int f in Click("mlsprings:line1")) yield return f;
        Check($"a click on the stream's line answers for the stream ({mlSprings?.Note})", mlSprings?.Note.StartsWith("The stream") == true);
        ClosePanels(); yield return 2;

        /* SHOULD: 59 minutes left read "1 h 59 min"; and "three at a time" is "up to three". */
        state.hinted["mr:springs"] = true;
        state.inv["galunggong"] = 1;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Advance(61, quiet: true); yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"59 minutes to go says so ({toastMsg})", toastMsg.Contains("0 h 59 min") || toastMsg.Contains(" 59 min") && !toastMsg.Contains("1 h"));
        Check("Lorna says up to three at a time", MarilayaSpokenLines().Any(l => l.Contains("up to three")));

        /* SHOULD: a panel's subtitle showed <act> instead of the key. */
        foreach (int f in Fresh("mr:cave1")) yield return f;
        foreach (int f in At(cvx, cvy + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        foreach (int f in Fades()) yield return f;
        foreach (int f in At(CaveModelX, CaveModelY + 10)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"the cave model's subtitle names the key ({lastMlPanelSub})", !lastMlPanelSub.Contains("<act>"));
        ClosePanels(); yield return 2;

        /* SHOULD: on a gamepad, steering the pointer turned the sight too. */
        foreach (int f in Fresh("mr:layers", "mr:karst", "mr:springs", "mr:tide", "mr:theory")) yield return f;
        foreach (int f in At(DatumX, DatumY + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        float b0 = mlScope?.Bearing ?? -1;
        Inp.ScriptStick = new(-1, 0); yield return 20; Inp.ScriptStick = null; yield return 2;
        Check($"moving the pad's pointer doesn't turn the sight ({b0:0.0} -> {mlScope?.Bearing:0.0})", mlScope != null && MathF.Abs(mlScope.Bearing - b0) < 0.01f);
        Inp.ScriptRightX = -1; yield return 20; Inp.ScriptRightX = null; yield return 2;
        Check($"the pad's right stick turns it ({b0:0.0} -> {mlScope?.Bearing:0.0})", mlScope != null && mlScope.Bearing != b0);
        ClosePanels(); yield return 2;

        /* Round 2: an outing long over mustn't call you back on a later low tide. */
        foreach (int f in Fresh()) yield return f;
        SetLowTide(); yield return 2;
        state.gifts["mr_walkDay"] = state.day; state.gifts["mr_walkBy"] = AbsNow - 400;
        toastMsg = "";
        var (q0x, q0y) = MlPoolAt(0);
        foreach (int f in At(q0x, q0y + 7)) yield return f;
        yield return 4;
        Check($"an old outing's time doesn't call you back on a later low tide ({toastMsg})", !toastMsg.Contains("Back to the beach"));

        /* Round 2: on a pad, A over a sight button turned it and recorded too; and A on "Wait for the sun" recorded instead. */
        foreach (int f in Fresh("mr:layers", "mr:karst", "mr:springs", "mr:tide", "mr:theory")) yield return f;
        foreach (int f in At(DatumX, DatumY + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        float s0 = mlScope?.Bearing ?? 0;
        var plus = Gfx.Seen["+1"];
        Inp.ScriptMouse = new(plus.X + plus.Width / 2, plus.Y + plus.Height / 2); yield return 2;
        Inp.TapPad(GamepadButton.RightFaceDown); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"pad A on +1 turns the sight a degree and records nothing ({s0:0.0} -> {mlScope?.Bearing:0.0}, {mlScope?.Note})", mlScope != null && MathF.Abs(AngleDiff(mlScope.Bearing, s0) - 1) < 0.01f && mlScope.Note == "" && !state.Hinted("mr:ab"));
        ClickButton("+1"); yield return 1;
        Check($"a sight button draws its new bearing in the same frame ({lastMlScopeBearing:0.0} vs {mlScope?.Bearing:0.0})", mlScope != null && MathF.Abs(AngleDiff(lastMlScopeBearing, mlScope.Bearing)) < 0.01f);
        Inp.ScriptMouse = Offscreen; yield return 2;
        ClosePanels(); yield return 2;

        // Pad A must click the waiting button without recording a bearing or reading a discarded panel.
        foreach (int f in Fresh("mr:layers", "mr:karst", "mr:springs", "mr:tide", "mr:theory", "mr:ab", "mr:mirror")) yield return f;
        state.weather = "rain";
        foreach (int f in At(DatumX, DatumY + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        var sun = Gfx.Seen["Wait for the sun"];
        Inp.ScriptMouse = new(sun.X + sun.Width / 2, sun.Y + sun.Height / 2); yield return 2;
        Inp.TapPad(GamepadButton.RightFaceDown); yield return 4;
        Check($"pad A on Wait for the sun starts waiting without recording ({mode})", mode == "fade" && !state.Hinted("mr:ac"));
        Inp.ScriptMouse = Offscreen;
        foreach (int f in Fades()) yield return f;
        Check("waiting through the pad button returns to play in sunshine", mode == "play" && MirrorFlashing);

        // Loading fish into the dryer removes their sale premiums as well as the fish, including in a window-close save.
        foreach (int f in Fresh("mr:springs")) yield return f;
        state.inv["galunggong"] = 3; state.big["galunggong"] = 3;
        foreach (int f in At(DryerAt.x, DryerAt.y + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        var dryingSave = SaveFile.Read();
        Check("drying big fish removes their premiums from the bag and the saved game", DryerLoad == 3 && Has("galunggong") == 0 && state.big.GetValueOrDefault("galunggong") == 0 && dryingSave?.big.GetValueOrDefault("galunggong") == 0);
        Give("galunggong");
        Check("a later ordinary fish cannot inherit a dried fish's premium", BigCount("galunggong") == 0);

        foreach (int f in Fresh("mr:springs")) yield return f;
        state.inv["galunggong"] = 5; state.big["galunggong"] = 2;
        foreach (int f in At(DryerAt.x, DryerAt.y + 9)) yield return f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("the dryer uses ordinary fish before big fish of the same kind", DryerLoad == 3 && Has("galunggong") == 2 && BigCount("galunggong") == 2);
        Advance(125, quiet: true); yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("drying the remaining big fish clears their premiums too", DryerLoad == 2 && Has("galunggong") == 0 && state.big.GetValueOrDefault("galunggong") == 0);

        /* Round 2: no "wait for low tide" when the water's already low enough to read. */
        foreach (int f in Fresh()) yield return f;
        SetLowTide(); yield return 2;
        var (lrx, lry) = PostReadSpot();
        foreach (int f in At(lrx, lry)) yield return f;
        Check($"at low water the post just reads, without offering to wait ({target?.AltLabel})", target?.Id == "tidepost" && target.AltType == null);

        /* SHOULD: waiting for low tide stopped as soon as it passed the mark, not at low water. */
        foreach (int f in Fresh()) yield return f;
        SetHighTide(); yield return 2;
        var (prx, pry) = PostReadSpot();
        foreach (int f in At(prx, pry)) yield return f;
        Inp.Tap(KeyboardKey.F); yield return 4;
        foreach (int f in Fades()) yield return f;
        Check($"waiting for low tide waits for the lowest water ({SeaLevel:0.00} vs {TideAt(TideNow + 10):0.00} and {TideAt(TideNow - 10):0.00})", SeaLevel <= TideAt(TideNow + 10) + 0.002f && SeaLevel <= TideAt(TideNow - 10) + 0.002f);

        /* SHOULD: the words go no further than the evidence, and match the real animals. */
        var words = MarilayaSpokenLines().ToList();
        Check("coral is reef-building coral in warm, shallow, sunlit water", words.Any(l => l.Contains("Reef-building corals")));
        Check("the urchin is its test, its hard skeleton", words.Any(l => l.Contains("test")));
        Check("the hermit crab is watched and left, not handled", !words.Any(l => l.Contains("put it back")));
        Check("the bats are insect-eaters, resting", words.Any(l => l.Contains("insect")) && !words.Any(l => l.Contains("fast asleep")));
        Check("the notch says the sea once stood there; the uplift is from dated evidence (Sela)", words.Any(l => l.Contains("date")));
        Check("the butterfly study is named as one study, and timing varies", words.Any(l => l.Contains("Iran")) && words.Any(l => l.Contains("temperature and food")));
        Check("Baltasar is spelt one way", !words.Any(l => l.Contains("Baltazar")));
        Note("Codex review checks done");
    }

    // Everything said or shown about Marilaya that the review checked, in one list.
    IEnumerable<string> MarilayaSpokenLines()
    {
        foreach (var f in MlFossilNotes.Values) yield return f;
        yield return MlNotchNote;
        foreach (var p in MlPools) yield return p.words;
        foreach (var s in NurseryStages) yield return s.words;
        foreach (var s in CaveStages) yield return s.text;
        foreach (var w in MlCaveWords.Values) yield return w;
        foreach (var w in MlOtherWords) yield return w;
    }
}
#endif
