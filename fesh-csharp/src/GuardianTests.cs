#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// 1.19: Platejaw, the guardian of the Ancient pool. FESH_CAVEBOSS_TEST=1 runs only this (it still needs FESH_AUTOTEST and
// FESH_SAVE); the full play-through runs it after the looking-closer checks.
partial class Game
{
    IEnumerable<int> GuardianScript()
    {
        Note("Platejaw, the guardian of the Ancient pool");
        state = new State { created = true, look = new Look { name = "Cave diver" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        Give("iron_sword");
        state.food = 100; state.hp = 100;
        yield return 2;

        /* ---------- Nothing given away beforehand ---------- */
        var row = LegendRows(400).First(r => r.key == "platejaw");
        var card = ExtraInfo("platejaw");
        Check($"before it's met, its legends row and card are nameless ({card.name}: {card.about})",
            !row.got && card.name == "???" && !card.about.Contains(GuardianName) && !card.about.Contains("Dunkleosteus") && !string.Join(" ", row.note).Contains(GuardianName));
        Check("the plate armour recipe is hidden until it's beaten", !RecipeKnown(Items.Recipes.First(r => r.Out == "plate_armor")));
        OpenDexLegends(); yield return 3;
        Check($"with nothing found, the legends page (every hint on two lines) fits ({lastDexBottom:0} of {Gfx.LH})", lastDexBottom <= Gfx.LH - 8);
        ClosePanels(); yield return 2;

        /* ---------- The carved stone ---------- */
        caveFloor = AncientFloor; LoadScene("cave"); AtLadder(); yield return 2;
        Check($"the carved stone stands on open floor by the pool ({TileAt((int)(RuneStoneX / T), (int)(RuneStoneY / T))})",
            Walkable(TileAt((int)(RuneStoneX / T), (int)(RuneStoneY / T))) && Dist(RuneStoneX, RuneStoneY, AncientPoolX, AncientPoolY) > Data.SpotById["ancientpool"].R);
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        Check($"before the coelacanth, the stone can only be read (prompt: {prompt.Text})", target?.Type == "runestone" && prompt.Text.Contains("Read"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("it shows an armoured fish and how to call it", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("armour")) && dlg.Lines.Any(l => l.T.Contains("Knock")));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("reading it wakes nothing", guardian == null);
        // It stands back from the water, so the south shore in front of it still fishes.
        player.X = RuneStoneX; player.Y = 207; player.Face = "up"; yield return 3;
        Check($"the south shore in front of it still fishes (prompt: {prompt.Text})", target?.Type == "spot" && target.Id == "ancientpool");

        /* ---------- Landing the coelacanth wakes it ---------- */
        var (apx, apy) = StandNear("ancientpool");
        player.X = apx; player.Y = apy; player.Face = "up"; yield return 2;
        // A couple of the floor's monsters, well away from you.
        monsters.Add(new Monster { Kind = "shade", X = 150, Y = 120, Hp = MonsterKinds["shade"].Hp });
        monsters.Add(new Monster { Kind = "crab", X = 460, Y = 110, Hp = MonsterKinds["crab"].Hp });
        int monstersBefore = monsters.Count;
        fish = new FishCast { Spot = "ancientpool", Bx = AncientPoolX, By = AncientPoolY };
        reel = new ReelState { Roll = new Catchable { Id = "ancient_coelacanth", Name = "Ancient coelacanth", Difficulty = 4.6f, Rare = true } };
        mode = "reeling";
        LandCatch();
        Check("the coelacanth still gets its legendary card first", mode == "legend" && Has("ancient_coelacanth") == 1);
        yield return 40;
        // Closed with its real button, then E on the very next frame (Codex: the stale fishing target cast the rod, and
        // Platejaw waited until the fishing was over).
        ClickButton("Incredible!");
        for (int i = 0; i < 8 && mode == "legend"; i++) yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Inp.ScriptMouse = Offscreen;
        Check($"E right after clicking the card closed doesn't cast instead ({mode})", mode == "play" && fish == null);
        Check($"closing the card brings it up out of the pool ({guardian?.Phase}, toast: {toastMsg})",
            guardian != null && mode == "play" && toastMsg.Contains(GuardianName) && toastMsg.Contains("coelacanth"));
        Check($"the coelacanth stays yours ({Has("ancient_coelacanth")}) and the cave's monsters scatter ({monstersBefore} -> {monsters.Count})",
            Has("ancient_coelacanth") == 1 && monsters.Count == 0 && monstersBefore > 0);
        Check("its name is on the legends page now, its story isn't yet", LegendRows(400).First(r => r.key == "platejaw").got && ExtraInfo("platejaw").name.Contains(GuardianName) && !ExtraInfo("platejaw").got);
        pendingShot = "g01-platejaw-rises"; yield return 12;
        // Halfway through rising, its armoured head is up out of the water, not just a ripple (Codex: it peaked at three rows).
        guardian.Phase = "rise"; guardian.T = 0.8f;
        int headPx = HeadAbove(guardian);
        Check($"rising, its head comes right up out of the water ({headPx} pixels above the surface)", headPx > 60);
        for (int i = 0; i < 120 && guardian.Phase == "rise"; i++) yield return 1;
        Check($"it settles to prowl the pool ({guardian.Phase})", guardian.Phase == "prowl" && InPool(guardian.X, guardian.Y));
        for (int i = 0; i < 40; i++) yield return 1;
        Check($"the fight has its own tune ({Music.Current})", Music.Current == "boss");
        Check($"mid-fight the only thing to do is face it (prompt: {prompt.Text})", target == null || target.Type is "guardian" or "info");

        /* ---------- Prowling: it follows you round ---------- */
        var g = guardian;
        player.X = 420; player.Y = 190; player.Face = "left";   // the far side of the east pillar, out of reach
        Prowl(g); g.Wait = 99;
        for (int i = 0; i < 150; i++) yield return 1;
        Check($"under the surface it follows you round the pool (angle {g.Ang:0.00} vs {AngleToPlayer:0.00}, at {g.X:0},{g.Y:0})",
            MathF.Abs(WrapAngle(g.Ang - AngleToPlayer)) < 0.35f && g.X > AncientPoolX && InPool(g.X, g.Y));

        /* ---------- Its armour turns a blow in the water ---------- */
        var (ex, ey) = PoolEdge(0);
        player.X = ex + 10; player.Y = ey + 2; player.Face = "left";
        iframes = 30;
        Aim(g); yield return 15;
        Check($"at the edge, its head can be struck (prompt: {prompt.Text})", target?.Type == "guardian" && prompt.Text.Contains("armoured head"));
        float sp0 = g.Spirit;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a blow on its head clangs off ({sp0:0} -> {g.Spirit:0}, toast: {toastMsg})", g.Spirit == sp0 && toastMsg.Contains("armoured"));
        pendingShot = "g02-platejaw-aims"; yield return 2;
        for (int i = 0; i < 120 && g.Phase is "aim" or "lunge"; i++) yield return 1;

        /* ---------- The lunge: telegraphed, dodgeable, and it strands itself ---------- */
        // South of the pool, where the lane is clear of pillars all the way.
        player.X = 300; player.Y = 236; player.Face = "up";
        Prowl(g); g.Wait = 99; yield return 60;
        iframes = 0; state.hp = 100;
        Aim(g);
        bool laneShot = false;
        for (int i = 0; i < 120 && g.Phase is "aim" or "lunge"; i++)
        {
            if (!laneShot && g.Phase == "aim" && g.T > 0.6f) { laneShot = true; pendingShot = "g03-lunge-lane"; }
            yield return 1;
        }
        Check($"standing in its path, the lunge hurts ({state.hp:0} health)", MathF.Abs(state.hp - (100 - LungeHurt)) < 0.6f);
        Check($"then it lies stranded on the stone ({g.Phase}, at {g.X:0},{g.Y:0})", g.Phase == "beached" && !InPool(g.X, g.Y) && GuardCanBe(g.X, g.Y));
        var (inBox, outBox) = GuardianPixels(g.X - (g.Dir > 0 ? 28 : 9), g.Y - 13, 38, 15);
        Check($"it's drawn where it lies: {inBox} pixels change in its box, {outBox} outside", inBox > 200 && outBox < inBox / 2);
        pendingShot = "g04-beached"; yield return 2;

        // Strike it while it's stranded.
        g.Hurt = 0; g.T = 0; g.Wait = 99;
        player.X = g.X - g.Dir * 12; player.Y = g.Y + 8; player.Face = "up"; iframes = 30; swingT = 0;
        yield return 3;
        Check($"stranded, it can be struck (prompt: {prompt.Text})", target?.Type == "guardian" && prompt.Text.Contains("iron sword"));
        sp0 = g.Spirit;
        Inp.Tap(KeyboardKey.E); yield return 3;
        float blow = sp0 - g.Spirit;
        Check($"a blow on the stone wears it down ({sp0:0} -> {g.Spirit:0})", MathF.Abs(blow - (3 + 5 * 0.5f) * 2) < 0.01f);
        sp0 = g.Spirit; swingT = 0;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("it shrugs off a second blow straight after", g.Spirit == sp0);
        g.Wait = 0.1f;
        for (int i = 0; i < 90 && g.Phase != "prowl"; i++) yield return 1;
        Check($"then it slides back into the water ({g.Phase})", g.Phase == "prowl" && InPool(g.X, g.Y));

        // Step out of the line after it has locked on, and it misses.
        player.X = 300; player.Y = 236; player.Face = "up"; iframes = 0; state.hp = 100;
        Aim(g);
        for (int i = 0; i < 80 && g.T < 0.6f; i++) yield return 1;
        player.X = 326; yield return 1;
        for (int i = 0; i < 120 && g.Phase is "aim" or "lunge"; i++) yield return 1;
        Check($"stepping out of its line once it has picked it, the lunge misses ({state.hp:0} health)", state.hp == 100);
        g.Wait = 0.05f;
        for (int i = 0; i < 90 && g.Phase != "prowl"; i++) yield return 1;

        /* ---------- Behind a pillar, the lunge ends in a crash ---------- */
        Check("the east pillar is where the map says", TileAt(41, 16) == 'Q');
        player.X = 432; player.Y = 168; player.Face = "left"; iframes = 30;
        Prowl(g); g.Wait = 99; yield return 40;
        Aim(g);
        for (int i = 0; i < 120 && g.Phase is "aim" or "lunge"; i++) yield return 1;
        Check($"lunging into a pillar dazes it ({g.Phase}, {g.X:0},{g.Y:0})", g.Phase == "stunned" && g.Wait > 2);
        pendingShot = "g05-crash"; yield return 2;
        g.Hurt = 0; g.T = 0; g.Wait = 99; swingT = 0;
        player.X = g.X - g.Dir * 8; player.Y = g.Y + 8; player.Face = "up";
        yield return 3;
        sp0 = g.Spirit;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a blow while it's dazed counts for more ({sp0 - g.Spirit:0.0} vs {blow:0.0})", sp0 - g.Spirit > blow + 1);
        g.Wait = 0.05f;
        for (int i = 0; i < 90 && g.Phase != "prowl"; i++) yield return 1;

        /* ---------- Lingering at the edge earns a bite ---------- */
        (ex, ey) = PoolEdge(0);
        player.X = ex + 8; player.Y = ey + 2; player.Face = "left"; iframes = 0; state.hp = 100;
        Prowl(g); g.Wait = 99; g.Ang = 0;
        for (int i = 0; i < 120 && g.Phase is "prowl" or "snapRear"; i++) yield return 1;
        Check($"standing at the very edge, it snaps at you ({g.Phase}, {state.hp:0} health)", g.Phase == "snap" && MathF.Abs(state.hp - (100 - SnapHurt)) < 0.6f);
        for (int i = 0; i < 60 && g.Phase == "snap"; i++) yield return 1;

        /* ---------- Rocks from the roof ---------- */
        player.X = 300; player.Y = 247; player.Face = "up"; iframes = 0; state.hp = 100;
        Prowl(g); g.Wait = 99;
        Slam(g);
        Check($"slamming the pool brings rocks down, one right where you stand ({rocks.Count})", rocks.Count >= 4 && rocks.Any(r => Dist(r.X, r.Y, player.X, player.Y) < 1));
        pendingShot = "g06-rock-shadows"; yield return 30;
        for (int i = 0; i < 80 && rocks.Any(r => !r.Down); i++) yield return 1;
        Check($"standing still under one hurts ({state.hp:0} health)", state.hp < 100 && state.hp >= 100 - RockHurt - 0.6f);
        for (int i = 0; i < 120 && g.Phase == "slam"; i++) yield return 1;
        // Again, but step out of the shadows.
        Prowl(g); g.Wait = 99; iframes = 0; state.hp = 100;
        Slam(g);
        var safe = Enumerable.Range(0, 60).Select(i => (x: 300f + (i % 12 - 6) * 6, y: 247f + (i / 12 - 2) * 6))
            .Where(p => CanStand(p.x, p.y) && Dist(p.x, p.y, AncientPoolX, AncientPoolY) < GuardLeash - 10 && rocks.All(r => Dist(r.X, r.Y, p.x, p.y) > 10))
            .OrderBy(p => Dist(p.x, p.y, player.X, player.Y)).First();
        player.X = safe.x; player.Y = safe.y;
        for (int i = 0; i < 100 && rocks.Any(r => !r.Down); i++) yield return 1;
        Check($"out of the shadows, the rocks miss ({state.hp:0} health)", state.hp == 100);
        for (int i = 0; i < 120 && g.Phase == "slam"; i++) yield return 1;

        /* ---------- The wave, and the shelter of a pillar ---------- */
        (float x, float y) open = (432, 190), shelter = (432, 167);
        Check($"right behind a pillar is sheltered, beside it isn't ({Sheltered(shelter.x, shelter.y)}, {Sheltered(open.x, open.y)})",
            Sheltered(shelter.x, shelter.y) && !Sheltered(open.x, open.y) && CanStand(open.x, open.y) && CanStand(shelter.x, shelter.y));
        // Every pillar shelters someone standing right behind it.
        bool allShelter = pillarSpots.All(q =>
        {
            float dx = q.x - AncientPoolX, dy = (q.y - AncientPoolY) * WaveSquash, l = MathF.Sqrt(dx * dx + dy * dy);
            return Sheltered(q.x + dx / l * 10, q.y + dy / l / WaveSquash * 10);
        });
        Check($"each of the {pillarSpots.Count} pillars shelters the spot right behind it", pillarSpots.Count == 10 && allShelter);
        player.X = open.x; player.Y = open.y; iframes = 0; state.hp = 100;
        Prowl(g); g.Wait = 99;
        SurgeRear(g);
        bool waveShot = false;
        for (int i = 0; i < 300 && g.Phase is "surgeRear" or "surge"; i++)
        {
            if (!waveShot && g.Phase == "surge" && g.Wave > 110) { waveShot = true; pendingShot = "g07-wave"; }
            yield return 1;
        }
        Check($"out in the open, the wave knocks you about ({state.hp:0} health)", MathF.Abs(state.hp - (100 - WaveHurt)) < 0.6f);
        player.X = shelter.x; player.Y = shelter.y; iframes = 0; state.hp = 100;
        Prowl(g); g.Wait = 99;
        SurgeRear(g);
        for (int i = 0; i < 300 && g.Phase is "surgeRear" or "surge"; i++) yield return 1;
        Check($"right behind a pillar, the wave breaks before it reaches you ({state.hp:0} health)", state.hp == 100);

        /* ---------- Pausing freezes it ---------- */
        Prowl(g); g.Wait = 99; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        float frozenT = g.T;
        yield return 20;
        Check($"Esc pauses the fight, and it waits ({mode}, {frozenT:0.00} -> {g.T:0.00})", mode == "pause" && g.T == frozenT);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to the fight", mode == "play" && guardian == g);

        /* ---------- Walking off, and calling it back ---------- */
        player.X = 305; player.Y = 345; player.Face = "down"; yield return 3;
        Check($"walk off down the passage and it sinks back (toast: {toastMsg})", guardian == null && toastMsg.Contains("carved stone"));
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        Check($"now the stone calls it up (prompt: {prompt.Text})", target?.Type == "runestone" && prompt.Text.Contains("Knock") && prompt.Text.Contains(GuardianName));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"knocking on the stone brings it back (toast: {toastMsg})", guardian != null && toastMsg.StartsWith(GuardianName));

        /* ---------- A save mid-fight wakes at the mouth, and the stone still works ---------- */
        Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("loading a save made mid-fight wakes outside the caverns, with no fight", scene == "world" && guardian == null && rocks.Count == 0 && GuardianMet && !GuardianBeaten);
        caveFloor = AncientFloor; LoadScene("cave"); yield return 2;
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        Check($"and the stone still calls it up (prompt: {prompt.Text})", target?.Type == "runestone" && prompt.Text.Contains("Knock"));

        /* ---------- Knocked out ---------- */
        Inp.Tap(KeyboardKey.E); yield return 3;
        g = guardian;
        for (int i = 0; i < 120 && g.Phase == "rise"; i++) yield return 1;
        int coins = state.coins = 200;
        player.X = 300; player.Y = 247; iframes = 0; state.hp = 5;
        Slam(g);
        for (int i = 0; i < 100 && mode != "fade"; i++) yield return 1;
        Check("knocked out by a rock: it goes back under and its rocks are gone", guardian == null && rocks.Count == 0);
        for (int i = 0; i < 200 && !(mode == "play" && scene == "world"); i++) yield return 1;
        Check($"you wake outside the caverns, a bit poorer, told how to try again ({state.coins}, toast: {toastMsg})",
            scene == "world" && state.coins == coins - coins / 10 && toastMsg.Contains("carved stone") && Has("ancient_coelacanth") == 1);
        state.hp = 100;

        /* ---------- Beating it ---------- */
        caveFloor = AncientFloor; LoadScene("cave"); yield return 2;
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        g = guardian;
        for (int i = 0; i < 120 && g.Phase == "rise"; i++) yield return 1;
        player.X = 432; player.Y = 168; player.Face = "left"; iframes = 30;
        Prowl(g); g.Wait = 99; yield return 40;
        Aim(g);
        for (int i = 0; i < 120 && g.Phase is "aim" or "lunge"; i++) yield return 1;
        g.Spirit = 4; g.Hurt = 0; g.T = 0; g.Wait = 99; swingT = 0;
        player.X = g.X - g.Dir * 8; player.Y = g.Y + 8; player.Face = "up";
        int xp0 = state.xp;
        yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"the last blow beats it, and the win is kept at once ({g.Phase}, plate {Has("platejaw_plate")})",
            g.Phase == "sink" && GuardianBeaten && Has("platejaw_plate") == 1 && state.xp > xp0 && SaveFile.Read(SaveFile.Slot).Hinted("platejaw"));
        Check($"the music calms down as it sinks ({GuardianFighting})", !GuardianFighting);
        pendingShot = "g08-sinking"; yield return 2;
        for (int i = 0; i < 200 && mode != "dialogue"; i++) yield return 1;
        Check("it sinks back, and the coelacanths can rest", mode == "dialogue" && guardian == null && dlg.Lines.Any(l => l.T.Contains("coelacanth")));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"then its card ({mode})", mode == "platejaw");
        yield return 40;
        pendingShot = "g09-card"; yield return 2;
        Check($"the card fits the screen ({lastGoldBottom:0} of {Gfx.LH})", lastGoldBottom <= Gfx.LH - 4);
        ClickButton("Onward!"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the real button closes it (toast: {toastMsg})", mode == "play" && toastMsg.Contains("armour"));
        Check("its plate makes plate armour, which halves the harm", RecipeKnown(Items.Recipes.First(r => r.Out == "plate_armor")));
        Give("iron_bar", 2);
        craftTab = "Combat"; craftPage = 0; OpenCraft("workbench"); yield return 4;
        // Of the Combat tab's six, only the plate armour can be made with what's in the bag.
        bool madeIt = Gfx.Seen.ContainsKey("Make");
        ClickButton("Make"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the workbench makes plate armour from it by a real click ({Has("plate_armor")}, plate left {Has("platejaw_plate")})", madeIt && Has("plate_armor") == 1 && Has("platejaw_plate") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Give("shell_armor");
        Check($"wearing both, plate armour wins ({ArmourMul})", ArmourMul == 0.5f);
        Take("plate_armor");
        Check($"shell armour alone is a third off ({ArmourMul})", MathF.Abs(ArmourMul - 0.65f) < 0.001f);
        Take("shell_armor"); Give("plate_armor");
        state.hp = 100; iframes = 0;
        HurtPlayer(10, player.X + 5, player.Y);
        Check($"a 10-point blow takes 5 in plate armour ({state.hp:0})", MathF.Abs(state.hp - 95) < 0.01f);
        Take("plate_armor");
        state.hp = 100;

        /* ---------- Afterwards ---------- */
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        Check($"beaten, the stone can only be read (prompt: {prompt.Text})", target?.Type == "runestone" && prompt.Text.Contains("Read"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("and knocking wakes nothing now", mode == "dialogue" && guardian == null);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        legendId = "ancient_coelacanth"; catchOpenedAt = -10; mode = "legend";
        CloseLegend(); yield return 3;
        Check("closing a coelacanth card again doesn't wake it", guardian == null && toastMsg.Contains("aquarium"));

        /* ---------- Its card, from the legends page, by a real click ---------- */
        OpenDexLegends(); yield return 3;
        Check($"the legends page still fits ({lastDexBottom:0} of {Gfx.LH})", lastDexBottom <= Gfx.LH - 8);
        bool clicked = Gfx.Seen.TryGetValue("row:platejaw", out var rr);
        if (clicked) { Inp.ScriptMouse = new System.Numerics.Vector2(rr.X + rr.Width / 2, rr.Y + rr.Height / 2); Inp.ScriptClickNext = true; }
        yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"clicking its row opens its card ({dexExtra})", clicked && dexExtra == "platejaw");
        card = ExtraInfo("platejaw");
        Check("beaten, its card tells its story and the real fish behind it", card.got && card.about == Data.Platejaw.Desc && ExtraFacts["platejaw"].sci.Contains("Dunkleosteus"));
        pendingShot = "g10-dex-card"; yield return 2;
        Check($"the real fish's note fits its box ({lastExtraBoxBottom:0})", lastExtraBoxBottom <= 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        ClosePanels(); yield return 2;

        /* ---------- A save from before 1.19 that already had the coelacanth ---------- */
        state = new State { created = true, look = new Look { name = "Old hand" } };
        state.flags.metTomas = true;
        state.commons["ancient_coelacanth"] = 1;
        state.caveDeepest = AncientFloor;
        mode = "play"; StartGame(false); ClearSkies(); yield return 2;
        // Through a real save file, as a game from before 1.19 would load (Codex: a fresh State skipped that).
        Save();
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); ClearSkies(); yield return 2;
        caveFloor = AncientFloor; LoadScene("cave"); yield return 2;
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        Check($"an old save with the coelacanth can still meet it (prompt: {prompt.Text})", target?.Type == "runestone" && prompt.Text == "Knock on the carved stone");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"its first knock wakes it with the long introduction (toast: {toastMsg})", guardian != null && toastMsg.StartsWith("The knocks carry"));
        yield return 30;
        pendingShot = "g11-old-save"; yield return 2;
        LoadScene("cave"); yield return 2;
        Check("leaving the floor puts it back under", guardian == null);
    }

    // Renders the view with and without Platejaw's sprite (lit the same either way), and counts the pixels that change
    // inside a world box and outside it, so a sprite drawn in the wrong place shows up.
    (int inside, int outside) GuardianPixels(float bx, float by, int bw, int bh)
    {
        RenderWorld(time);
        var with = (Color[])pix.Buf.Clone();
        guardianUndrawn = true;
        RenderWorld(time);
        guardianUndrawn = false;
        int inside = 0, outside = 0;
        for (int i = 0; i < with.Length; i++)
        {
            var a = with[i]; var b = pix.Buf[i];
            if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 12) continue;
            float wx = i % W + camX, wy = i / W + camY;
            if (wx >= bx && wx < bx + bw && wy >= by && wy < by + bh) inside++; else outside++;
        }
        RenderWorld(time);
        return (inside, outside);
    }

    // Pixels of Platejaw's sprite drawn clear of the water's surface (above its ripples), where only its head can be.
    int HeadAbove(Guardian g)
    {
        RenderWorld(time);
        var with = (Color[])pix.Buf.Clone();
        guardianUndrawn = true;
        RenderWorld(time);
        guardianUndrawn = false;
        int n = 0;
        for (int y = (int)g.Y - 20 - camY; y < (int)g.Y - 6 - camY; y++)
            for (int x = (int)g.X - 34 - camX; x < (int)g.X + 34 - camX; x++)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) continue;
                var a = with[y * W + x]; var b = pix.Buf[y * W + x];
                if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) >= 12) n++;
            }
        RenderWorld(time);
        return n;
    }

    // The Fish log on its last page, the legends.
    void OpenDexLegends()
    {
        TogglePanel("dex");
        dexTab = "log";
        logPage = Data.Biomes.Length;
        dexFish = null; dexExtra = null;
    }

    // FESH_SPRITES: Platejaw in each pose, both ways, at 8x.
    void ExportGuardianSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#2e4a4e"));
        for (int dir = 0; dir < 2; dir++)
        {
            int d = dir == 0 ? 1 : -1, y = 20 + dir * 40;
            DrawPlatejaw(30, y, d, false, false);
            DrawPlatejaw(75, y, d, true, false);
            DrawPlatejaw(120, y, d, false, false, 10);
            DrawPlatejaw(160, y, d, false, false, 4);
            DrawPlatejaw(200, y, d, false, true);
        }
        sheet.CamX = (int)RuneStoneX - 215; sheet.CamY = (int)RuneStoneY - 90;
        DrawRuneStone(0);
        pix = keep;
        int w = 230, h = 100;
        var img = GenImageColor(w, h, Color.Black);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++) ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * W + xx]);
        ImageResizeNN(ref img, w * 6, h * 6);
        ExportImage(img, path.Replace(".png", "-platejaw.png"));
        UnloadImage(img);
    }
}
#endif
