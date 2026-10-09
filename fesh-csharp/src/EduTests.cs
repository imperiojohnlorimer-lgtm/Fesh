#if DEBUG
using Raylib_cs;

namespace Fesh;

// The Sea school (1.17): the tides and Lola Pacing, letting fish go and sorting crabs, and Ma'am Isay's class.
// FESH_EDU_TEST=1 runs only this (it still needs FESH_AUTOTEST and FESH_SAVE); the full play-through runs it at the end.
partial class Game
{
    // The middle of today's first low tide (or a high tide), keeping the day.
    void SetLowTide()
    {
        float m0 = TideMinute(state.day, DawnMin);
        int n = LowIndex(m0);
        if (LowAt(n) < m0) n++;
        state.clock = FromTideMinute(LowAt(n)).clock;
    }

    void SetHighTide()
    {
        float m0 = TideMinute(state.day, DawnMin), hi = LowAt(LowIndex(m0)) + TidePeriod / 2;
        while (hi < m0) hi += TidePeriod;
        state.clock = FromTideMinute(hi).clock;
    }

    void SetTideTo(float m) { var (d, c) = FromTideMinute(m); state.day = d; state.clock = c; }

    // The next low tide (from today) at least this deep, by day if it can be.
    int FindLow(float minDepth, bool daylight = true)
    {
        int n = LowIndex(TideMinute(state.day, DawnMin));
        for (int i = 0; i < 80; i++, n++)
        {
            float c = FromTideMinute(LowAt(n)).clock;
            if (LowDepth(n) >= minDepth && (!daylight || c is >= 8 * 60 and <= 17 * 60)) return n;
        }
        return n;
    }

    void FreshEduGame(string name)
    {
        state = new State { created = true, look = new Look { name = name } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        animals.Clear();
        state.hinted["amihan"] = state.hinted["habagat"] = true;
        Inp.ScriptMouse = Offscreen;
    }

    IEnumerable<int> EduScript()
    {
        Note("The Sea school: tides, letting fish go, and Ma'am Isay's class");
        FreshEduGame("Learner");
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        yield return 2;

        /* ---------- The tide ---------- */
        Note("The tide");
        int n0 = LowIndex(TideMinute(3, DawnMin));
        Check($"a low tide every 12 h 25 min ({LowAt(n0 + 1) - LowAt(n0):0.0} min)", MathF.Abs(LowAt(n0 + 1) - LowAt(n0) - 745.2f) < 0.5f);
        Check($"and each day's comes about 50 minutes later ({LowAt(n0 + 2) - LowAt(n0) - 1440:0} min)", MathF.Abs(LowAt(n0 + 2) - LowAt(n0) - 1440 - 50.4f) < 1);
        // Day d's moon is (d + 3) % 8: full on day 1, 9..., new on day 5, 13...; half moons on days 3, 7.
        float Range(int day) => TideRange(TideMinute(day, 18 * 60));
        Check($"spring tides at the full and new moon ({Range(1):0.00}, {Range(5):0.00}), neap at the half moons ({Range(3):0.00}, {Range(7):0.00})",
            Range(1) > 0.97f && Range(5) > 0.97f && Range(3) < 0.53f && Range(7) < 0.53f);
        int spring = FindLow(0.93f), neap = FindLow(0, false);
        neap = Enumerable.Range(neap, 20).OrderBy(LowDepth).First();
        SetTideTo(LowAt(spring)); int springQuota = GleanQuota; bool springDry = FlatsDry;
        SetTideTo(LowAt(neap)); int neapQuota = GleanQuota; bool neapDry = FlatsDry; bool neapLow = LowTide;
        Check($"a spring low has more to glean than a neap low ({springQuota} against {neapQuota})", springQuota >= 14 && neapQuota <= 5);
        Check("only a spring low dries the flats out, though a neap low still uncovers the beach", springDry && !neapDry && neapLow);
        SetTideTo(LowAt(spring) + TidePeriod / 2);
        Check("half a tide later it's high water", !LowTide && SeaLevel > 0.5f);
        var (ws, we) = LowWindow(spring);
        Check($"a spring low's gleaning lasts hours ({(we - ws) / 60:0.0} h; a neap's {(LowWindow(neap).end - LowWindow(neap).start) / 60:0.0} h)",
            we - ws > 3.5f * 60 && LowWindow(neap).end - LowWindow(neap).start < 2.5f * 60);
        var flats = Flats();
        Check($"Habagat has reef flats beside its beaches ({flats.Count} tiles)", flats.Count > 30 && flats.All(f => worldMap[f.Item2, f.Item1] == 'w' && biome[f.Item2, f.Item1] == 6));

        // Walking out onto the flats at a spring low, and the tide coming back.
        Note("Walking the flats");
        SetTideTo(LowAt(spring));
        (int x, int y, int fx, int fy, string face) walk = default;
        foreach (var (fx, fy) in flats.OrderBy(f => Dist(f.Item1 * T, f.Item2 * T, 200, 668)))
        {
            foreach (var (ox, oy, face) in new[] { (0, -1, "down"), (0, 1, "up"), (-1, 0, "right"), (1, 0, "left") })
                if (worldMap[fy + oy, fx + ox] == 's' && CanStand((fx + ox) * T + 5, (fy + oy) * T + 7) && !Solids().Any(r => r.Overlaps(new Box((fx + ox) * T - 5, (fy + oy) * T - 5, 20, 20))))
                { walk = (fx + ox, fy + oy, fx, fy, face); break; }
            if (walk.face != null) break;
        }
        Check($"found a beach beside a flat ({walk.x},{walk.y})", walk.face != null);
        var key = walk.face switch { "down" => KeyboardKey.Down, "up" => KeyboardKey.Up, "left" => KeyboardKey.Left, _ => KeyboardKey.Right };
        var back = walk.face switch { "down" => KeyboardKey.Up, "up" => KeyboardKey.Down, "left" => KeyboardKey.Right, _ => KeyboardKey.Left };
        player.X = walk.x * T + 5; player.Y = walk.y * T + 7; player.Face = walk.face; yield return 2;
        Inp.Hold(key, true);
        for (int i = 0; i < 60 && !OnFlat; i++) yield return 1;
        for (int i = 0; i < 6; i++) yield return 1;
        Inp.Hold(key, false); yield return 2;
        Check($"at a spring low you walk out onto the flat, on dry sand ({player.X:0},{player.Y:0})", OnFlat && !InWater);
        pendingShot = "300-tide-flats"; yield return 2;
        // The flat really is drawn dry: a pixel of it near the beach (away from you) is sand, not sea.
        var dryPx = flats.Where(f => Dist(f.Item1 * T, f.Item2 * T, player.X, player.Y) < 80).SelectMany(f => Enumerable.Range(0, T * T).Select(i => (x: f.Item1 * T + i % T, y: f.Item2 * T + i / T)))
            .Where(q => TideDry(q.x, q.y) && !(Math.Abs(q.x - player.X) < 6 && q.y > player.Y - 18 && q.y < player.Y + 3) && q.x - camX is >= 0 and < W && q.y - camY is >= 0 and < H).ToList();
        int sandy = dryPx.Count(q => { var c = pix.Buf[(q.y - camY) * W + q.x - camX]; return c.R > c.B; });
        Check($"the dried flat is drawn as sand, with a few pools ({sandy} of {dryPx.Count} px sandy)", dryPx.Count > 50 && sandy > dryPx.Count * 0.6f && sandy < dryPx.Count);
        Check("finds turn up out on a dried flat", GleanGround(walk.fx, walk.fy));
        // The tide comes back: you can still walk back to the beach, but not out again.
        SetTideTo(LowAt(spring) + 150); toastMsg = ""; yield return 3;
        Check($"the water comes back over the flat ({SeaLevel:0.00})", !FlatsDry && !GleanGround(walk.fx, walk.fy));
        Check($"and you're told to head back ({toastMsg})", toastMsg.Contains("tide's turning"));
        Inp.Hold(back, true);
        for (int i = 0; i < 60 && OnFlat; i++) yield return 1;
        Inp.Hold(back, false); yield return 2;
        Check("caught out by the tide, you can still walk back to the beach", !OnFlat && worldMap[(int)((player.Y - 1.5f) / T), (int)(player.X / T)] == 's');
        // A step further up the beach, then back toward the sea.
        Inp.Hold(back, true); for (int i = 0; i < 14; i++) yield return 1; Inp.Hold(back, false); yield return 1;
        Inp.Hold(key, true);
        for (int i = 0; i < 30; i++) yield return 1;
        Inp.Hold(key, false); yield return 2;
        Check("but you can't walk out onto it again until the next big low", !OnFlat);
        // Caught out at high water you can step ashore, but not wade along the flats to the next one.
        var pair = flats.FirstOrDefault(f => flats.Contains((f.Item1 + 1, f.Item2)) && !flats.Contains((f.Item1 - 1, f.Item2)) && Walkable(worldMap[f.Item2 - 1, f.Item1]));
        if (pair != default)
        {
            player.X = pair.Item1 * T + 4; player.Y = pair.Item2 * T + 7; yield return 2;
            Inp.Hold(KeyboardKey.Right, true);
            bool crossed = false;
            for (int i = 0; i < 40; i++) { yield return 1; crossed |= (int)(player.X / T) == pair.Item1 + 1 && (int)((player.Y - 1.5f) / T) == pair.Item2; }
            Inp.Hold(KeyboardKey.Right, false); yield return 1;
            Check("caught by the tide you can't wade along the flats to the next one", !crossed);
            Inp.Hold(KeyboardKey.Up, true);
            for (int i = 0; i < 30 && OnFlat; i++) yield return 1;
            Inp.Hold(KeyboardKey.Up, false); yield return 1;
            Check("but you can always step back ashore", !OnFlat);
        }
        else Check("found two flats side by side", false);
        // From a flooded flat you can walk ashore whichever way the beach is, and carry on up it (Codex's review: the
        // rear of your feet could still be over the water once your middle was ashore).
        foreach (var (ox, oy, k) in new[] { (0, -1, KeyboardKey.Up), (0, 1, KeyboardKey.Down), (-1, 0, KeyboardKey.Left), (1, 0, KeyboardKey.Right) })
        {
            var f0 = flats.FirstOrDefault(f => worldMap[f.Item2 + oy, f.Item1 + ox] == 's' && worldMap[f.Item2 + 2 * oy, f.Item1 + 2 * ox] is 's' or 'g' or 'j'
                && CanStand((f.Item1 + 2 * ox) * T + 5, (f.Item2 + 2 * oy) * T + 7));
            if (f0 == default) { Check($"found a flat with the beach {k}", false); continue; }
            player.X = f0.Item1 * T + 5; player.Y = f0.Item2 * T + 7; yield return 2;
            Inp.Hold(k, true);
            for (int i = 0; i < 45; i++) yield return 1;
            Inp.Hold(k, false); yield return 1;
            float moved = MathF.Abs(player.X - (f0.Item1 * T + 5)) + MathF.Abs(player.Y - (f0.Item2 * T + 7));
            Check($"flooded, you walk ashore {k} and on up the beach ({moved:0} px)", !OnFlat && moved >= 14);
        }
        pendingShot = "301-tide-in"; yield return 2;
        // A save made out on the flats loads, wherever the tide is by then.
        SetTideTo(LowAt(spring)); player.X = walk.fx * T + 5; player.Y = walk.fy * T + 7; Save();
        state = SaveFile.Read(SaveFile.Slot); state.clock = FromTideMinute(LowAt(spring) + 300).clock;
        StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); yield return 3;
        Check($"a game saved out on the flats loads there, even at high tide ({player.X:0},{player.Y:0})", OnFlat);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"the clock's tooltip tells the tide on Habagat ({TideLine()})", TideLine().Contains("low tide"));
        player.X = walk.x * T + 5; player.Y = walk.y * T + 7;

        /* ---------- Lola Pacing, the tide table and her riddles ---------- */
        Note("Lola Pacing's tides");
        var lola = HabagatFolk.First(f => f.id == "pacing");
        state.day = 2; state.clock = 10 * 60; ClearSkies(); state.hinted["metPacing"] = true;
        player.X = lola.x; player.Y = lola.y + 12; player.Face = "up"; yield return 3;
        Check($"the second time, Lola has something to tell you ({target?.Label})", target?.Label == "Talk to Lola Pacing");
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        yield return 2;
        Check("she teaches the tides and gives you the tide table", mode == "panel" && panel == "tides" && TidesLearned);
        pendingShot = "302-tide-table"; yield return 2;
        Check($"the tide table fits its panel ({lastTidesBottom:0})", lastTidesBottom <= (Gfx.LH + 660) / 2 - 24);
        ClickButton("Close"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("Close shuts it", mode == "play");
        yield return 2;
        Check($"then E plays sungka and F asks her riddle ({target?.Label} / {target?.AltLabel})", target?.Label == "Play sungka with Lola Pacing" && target?.AltType == "riddle");
        Check("the guide lists her riddle as a daily goal", goals.Any(g => g.Id == "pacing:riddle" && g.Group == "daily"));
        int coins0 = state.coins;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check($"F opens the tide table with her riddle ({riddle?.Kind})", mode == "panel" && panel == "tides" && riddle?.Kind == "next");
        pendingShot = "303-tide-riddle"; yield return 2;
        bool ClickTide(float m)
        {
            if (!Gfx.Seen.TryGetValue("tidegraph", out var r)) return false;
            float m0 = TideTableStart;
            Inp.ScriptMouse = new System.Numerics.Vector2(r.X + (m - m0) / (3 * 1440) * r.Width, r.Y + r.Height / 2);
            Inp.ScriptClickNext = true;
            return true;
        }
        var asked = riddle;
        ClickTide(LowAt(asked.Answer)); yield return 3;
        Inp.ScriptMouse = Offscreen;
        Check($"clicking the next low tide on the graph answers it ({asked.Reply})", asked.Done && asked.Right && state.riddles == 1 && state.coins == coins0 + 25);
        pendingShot = "304-tide-riddle-right"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("one riddle a day", !RiddleToday && target?.AltType == null);
        // The next day, the lowest tide: a wrong click first.
        state.day = 3; yield return 2;
        Inp.Tap(KeyboardKey.F); yield return 3;
        asked = riddle;
        float wrongM = LowAt(Enumerable.Range(asked.Answer - 3, 7).Where(k => LowAt(k) > TideTableStart && LowAt(k) < TideTableStart + 3 * 1440)
            .OrderBy(k => LowDepth(k)).First());
        ClickTide(wrongM); yield return 3; Inp.ScriptMouse = Offscreen;
        Check($"clicking a shallower low isn't the lowest ({asked.Kind}: {asked.Reply})", asked.Kind == "lowest" && asked.Done && !asked.Right && state.reefLow < 0);
        pendingShot = "305-tide-riddle-wrong"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // And right, a day later (the riddles go round, so ask for the lowest again).
        state.day = 4; state.riddlesAsked = 1; yield return 2;
        Inp.Tap(KeyboardKey.F); yield return 3;
        asked = riddle;
        ClickTide(LowAt(asked.Answer)); yield return 3; Inp.ScriptMouse = Offscreen;
        Check($"finding the lowest tide books a reef walk with her ({state.reefLow})", asked.Right && state.reefLow == asked.Answer);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("the reef walk is a request in the journal", goals.Any(g => g.Id == "pacing:reef" && g.Group == "request"));
        // The tide comes: she's out on the flats with her apo, and the first find is an octopus.
        SetTideTo(LowAt(state.reefLow)); ClearSkies(); yield return 3;
        var spot = ReefSpot();
        player.X = spot.x; player.Y = spot.y + 10; yield return 3;
        var ls = HabagatWalk("pacing");
        Check($"at the low tide she's on the flats ({ls.X:0},{ls.Y:0})", ReefWalkOn && Dist(ls.X, ls.Y, spot.x, spot.y) < 1 && Dist(ls.X, ls.Y, lola.x, lola.y) > 20);
        Check("and the guide says the walk is ready", goals.FirstOrDefault(g => g.Id == "pacing:reef")?.Ready == true);
        pendingShot = "306-reef-walk"; yield return 2;
        int octo = state.commons.GetValueOrDefault("pugita");
        player.X = spot.x; player.Y = spot.y; yield return 1;
        state.loose.Add(new Loose { kind = "glean", tx = (int)(player.X / T), ty = (int)((player.Y - 1) / T), x = player.X, y = player.Y - 1 });
        yield return 3;
        Check($"the walk's first find is an octopus ({toastMsg})", state.commons.GetValueOrDefault("pugita") == octo + 1);
        // Reloading during the walk doesn't bring the guaranteed octopus back (Codex's review).
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); quietWildlife = true; standStill = true; ClearSkies(); yield return 3;
        Check("after a reload the walk's octopus is still found", ReefWalkOn && state.reefOctopus == state.reefLow && !ReefOctopusDue);
        player.X = ls.X; player.Y = ls.Y + 12; player.Face = "up"; yield return 3;
        Check($"E walks the reef with her ({target?.Label})", target?.Label == "Walk the reef with Lola Pacing");
        coins0 = state.coins;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"she thanks you: 80 coins and cowries, and the walk counts ({state.reefWalks})", state.reefWalks == 1 && state.coins == coins0 + 80 && ReefThanked);
        yield return 3;
        // Saying hello doesn't end it (Codex's review): she stays out with the children until the tide comes back.
        Check($"she stays on the flats for the rest of the tide ({target?.Label})", ReefWalkOn && Dist(HabagatWalk("pacing").X, HabagatWalk("pacing").Y, spot.x, spot.y) < 1 && target?.Label == "Talk to Lola Pacing");
        coins0 = state.coins;
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("and she only thanks you once", state.coins == coins0 && state.reefWalks == 1);
        Check("the walk leaves the journal once she's thanked you", !goals.Any(g => g.Id == "pacing:reef"));
        SetTideTo(LowWindow(state.reefLow).end + 30); yield return 3;
        Check("then the tide comes in and she goes back to her board, nothing missed", state.reefLow < 0 && !state.Hinted("reefMissed") && Dist(HabagatWalk("pacing").X, HabagatWalk("pacing").Y, lola.x, lola.y + 2) < 1);
        // The riddles: the next low really is the next one, and a click has to be at the bottom of a dip.
        float nextLow = LowAt(LowIndex(TideNow) + 1);
        SetTideTo(nextLow - 10); state.riddlesAsked = 0;
        var near = MakeRiddle();
        Check($"ten minutes before a low, 'the next low' is that one ({near.Answer} for {LowIndex(nextLow)})", near.Kind == "next" && near.Answer == LowIndex(nextLow));
        riddle = near; AnswerRiddle(LowAt(near.Answer) + 300); riddle = null;
        Check("clicking up on the high tide five hours later isn't the low", near.Done && !near.Right);
        // Days 4 and 6 have the same smallest tides: either is right (Codex's second review).
        state.day = 4; state.clock = 10 * 60; state.riddlesAsked = 3;
        var tie = MakeRiddle(); riddle = tie;
        int other = tie.Answer == 0 ? 2 : 0;
        AnswerRiddle(TideTableStart + other * 1440 + 600); riddle = null;
        Check($"either of two equally small days is the smallest ({tie.Kind}, answered day {state.day + other})", tie.Kind == "neap" && tie.Right);
        // A lowest tide that won't dry the reef flat is a beach walk, and Lola says so.
        state.day = 14; state.clock = 18 * 60; state.riddlesAsked = 1; state.reefLow = -1;
        var low = MakeRiddle(); riddle = low;
        AnswerRiddle(LowAt(low.Answer)); riddle = null;
        Check($"a lowest tide too shallow for the reef is booked as a beach walk ({LowDepth(low.Answer):0.00}: {low.Reply})", low.Right && LowDepth(low.Answer) < -FlatLevel && low.Reply.Contains("reef stays under"));
        yield return 2;
        var walkGoal = goals.FirstOrDefault(g => g.Id == "pacing:reef");
        Check($"and the journal says the same ({walkGoal?.Text})", walkGoal != null && walkGoal.Text.Contains("on the beach") && walkGoal.Text.Contains("reef stays under"));
        state.reefLow = -1; state.riddleDay = 0;
        // Missing a booked walk.
        state.reefLow = LowIndex(TideNow) + 4; SetTideTo(LowWindow(state.reefLow).end + 30); yield return 3;
        Check("a reef walk you miss is called off", state.reefLow < 0 && state.Hinted("reefMissed"));
        state.hinted.Remove("reefMissed");

        /* ---------- Letting fish go ---------- */
        Note("Letting fish go");
        FreshEduGame("Releaser");
        var perch = Data.FishById["pond_perch"];
        var (lx, ly) = StandNear("lagoon"); player.X = lx; player.Y = ly; yield return 2;
        // A juvenile: landed, then let go with F.
        Toast("Caught a pond perch!", 3);
        AddCatch(perch); lastM = 0.5f; NoteLanded(perch, "lagoon", 0.12f, player.X, player.Y - 20, false);
        yield return 2;
        Check($"the catch's own message comes first ({toastMsg})", toastMsg.StartsWith("Caught a pond perch") && badgeNews != null);
        toastTimer = 0; yield return 2;
        Check($"after landing a little one, F lets it go ({prompt.Text})", prompt.Text.Contains("Let the little one go") && CanRelease);
        Check($"and you're told why the first time ({toastMsg})", toastMsg.Contains("hasn't had a chance to spawn"));
        int had = Has("pond_perch");
        Inp.Tap(KeyboardKey.F); yield return 6;
        Check($"F lets it go: out of the bag, and it counts ({state.released})", Has("pond_perch") == had - 1 && state.released == 1 && state.letGo.Count == 1 && releaseFx != null);
        pendingShot = "310-let-go"; yield return 2;
        Check("you can't let the same fish go twice", !CanRelease);
        // A big one let go isn't counted as a big one still in the bag.
        AddCatch(perch, minM: 1.3f); lastM = 1.3f; NoteLanded(perch, "lagoon", 1f, player.X, player.Y - 20, true);
        int big0 = state.big.GetValueOrDefault("pond_perch");
        yield return 2;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check($"letting a big one go takes it off Pip's big-fish count ({big0} to {state.big.GetValueOrDefault("pond_perch")})", state.big.GetValueOrDefault("pond_perch") == big0 - 1);
        Check("a fish that isn't small or in season only counts as let go, not toward the badge", state.released == 1 && state.releasedAll == 2);
        // Moving off ends the chance.
        AddCatch(perch); lastM = 0.5f; NoteLanded(perch, "lagoon", 0.1f, player.X, player.Y - 20, true);
        yield return 2;
        Inp.Hold(KeyboardKey.Right, true); yield return 4; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check("walking off keeps the fish", !CanRelease && landed == null);
        player.X = lx; player.Y = ly;
        // Back, grown: a fish let go here yesterday.
        state.letGo.Clear();
        int tries = 0, grown0 = state.grownBack;
        while (state.grownBack == grown0 && tries < 60)
        {
            state.letGo.Add(new LetGo { id = "pond_perch", spot = "lagoon", day = state.day - 1, kg = 0.1f });
            fish = new FishCast { Spot = "lagoon", Bx = player.X, By = player.Y - 20 };
            reel = new ReelState { Roll = new Catchable { Id = "pond_perch", Name = perch.Name } };
            LandCatch(); tries++;
            while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
            if (state.grownBack == grown0) state.letGo.Clear();
        }
        Check($"now and then the one you let go is back, grown ({tries} tries: {toastMsg})", state.grownBack == grown0 + 1 && toastMsg.Contains("back and grown") && lastM >= 1.3f);
        // A big one let go comes back bigger still (Codex's review: it could come back lighter).
        tries = 0; grown0 = state.grownBack; state.letGo.Clear();
        float bigKg = perch.Kg * 1.7f;
        while (state.grownBack == grown0 && tries < 80)
        {
            state.letGo.Add(new LetGo { id = "pond_perch", spot = "lagoon", day = state.day - 1, kg = bigKg });
            fish = new FishCast { Spot = "lagoon", Bx = player.X, By = player.Y - 20 };
            reel = new ReelState { Roll = new Catchable { Id = "pond_perch", Name = perch.Name } };
            LandCatch(); tries++;
            while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
            if (state.grownBack == grown0) state.letGo.Clear();
        }
        Check($"a big one let go comes back heavier than it went ({perch.Kg * lastM:0.00} kg against {bigKg:0.00} kg)", state.grownBack == grown0 + 1 && perch.Kg * lastM > bigKg);
        state.letGo.Clear();
        int hits = 0;
        for (int i = 0; i < 400; i++) { state.letGo.Add(new LetGo { id = "pond_perch", spot = "lagoon", day = state.day - 1 }); if (Returning("pond_perch", "lagoon") != null) hits++; state.letGo.Clear(); }
        Give("dehooker");
        int hitsD = 0;
        for (int i = 0; i < 400; i++) { state.letGo.Add(new LetGo { id = "pond_perch", spot = "lagoon", day = state.day - 1 }); if (Returning("pond_perch", "lagoon") != null) hitsD++; state.letGo.Clear(); }
        Check($"about a third come back, more with the dehooker ({hits} and {hitsD} of 400)", hits is > 100 and < 180 && hitsD is > 200 && hitsD > hits + 60);
        state.letGo.Add(new LetGo { id = "pond_perch", spot = "lagoon", day = state.day });
        Check("never the same day", Enumerable.Range(0, 50).All(_ => Returning("pond_perch", "lagoon") == null));
        state.letGo.Clear();
        landed = null; mode = "play";

        // The closed season.
        Note("The closed season");
        state.day = 2;   // the amihan
        Check($"galunggong and tamban are closed in the amihan ({Season})", Season == "amihan" && ClosedSeason("galunggong") && ClosedSeason("tamban") && !ClosedSeason("bangus"));
        Give("galunggong", 3); Give("bangus", 2); state.commons["galunggong"] = 3;
        int c1 = state.coins;
        Sell("galunggong", 1);
        Check("Pip won't buy them", Has("galunggong") == 3 && state.coins == c1);
        SellAllFish();
        Check("and selling all your fish leaves them in the bag", Has("galunggong") == 3 && Has("bangus") == 0 && state.coins > c1);
        state.hinted["metPip"] = true;
        player.X = PipX; player.Y = PipY + 14; player.Face = "up"; state.clock = 10 * 60; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        bool explained = mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("spawning season"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check("Pip explains the closed season once", explained && state.Hinted("pipClosed"));
        yield return 2;
        pendingShot = "311-closed-season"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        state.day = 7;   // the habagat
        Check($"in the habagat Pip buys them again ({Season})", Season == "habagat" && !ClosedSeason("galunggong"));
        state.day = 2;
        var gal = Data.FishById["galunggong"];
        AddCatch(gal); lastM = 1f; NoteLanded(gal, "amihansea", 0.2f, player.X, player.Y - 20, false);
        toastTimer = 0; yield return 3;
        Check($"a closed-season catch can be let go ({prompt.Text})", prompt.Text.Contains("closed season") && toastMsg.Contains("closed season"));
        int rel0 = state.released;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check("and it counts toward the badge", state.released == rel0 + 1);

        /* ---------- Sorting crabs ---------- */
        Note("Sorting a crab pot");
        // The crab's underside, read off its pixels: the apron's width in a row through it, and the eggs.
        int Count(Pix p, string hex) { var c = Pal.C(hex); return p.Buf.Count(b => b.R == c.R && b.G == c.G && b.B == c.B); }
        var male = UndersidePix("alimasag_crab", false, false, false);
        var female = UndersidePix("alimasag_crab", true, false, false);
        var berried = UndersidePix("alimasag_crab", true, true, false);
        var lateEggs = UndersidePix("alimasag_crab", true, true, true);
        string apron = Shade(FishArt.Looks["alimasag_crab"].Belly, 0.86f);
        int RowWidth(Pix p, int y) { var c = Pal.C(apron); return Enumerable.Range(0, 24).Count(x => p.Buf[y * 24 + x].R == c.R && p.Buf[y * 24 + x].G == c.G && p.Buf[y * 24 + x].B == c.B); }
        int Widest(Pix p) => Enumerable.Range(12, 6).Max(y => RowWidth(p, y));
        Check($"a male's apron is narrow and a female's broad ({Widest(male)} against {Widest(female)} px)", Widest(male) <= 3 && Widest(female) >= 8);
        Check($"a female carrying eggs shows them, orange or dark ({Count(berried, "#e8862a")}, {Count(lateEggs, "#4a3a30")})", Count(berried, "#e8862a") > 30 && Count(lateEggs, "#4a3a30") > 30 && Count(female, "#e8862a") == 0);
        // A pot in Amihan's shallows, hauled for real.
        player.X = 1595; player.Y = 200; yield return 2;
        Give("crab_pot");
        Check("found shallow water by the village", FaceWater());
        var (potX, potY, potFace) = (player.X, player.Y, player.Face);
        var (ptx, pty) = FrontTile();
        var pot = new Build { id = "crabpot", x = ptx, y = pty };
        state.builds.Add(pot); ReindexBuilds();
        state.pots[$"{ptx},{pty}"] = state.day - 1;
        yield return 3;
        int crabs0 = Has("alimasag_crab");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"hauling the pot puts the crabs on the sorting tray, out of the bag until kept ({sorting?.Count})", mode == "panel" && panel == "sort" && sorting?.Count >= 1 && Has("alimasag_crab") == crabs0);
        Check($"and off Pip's big-crab count while they're there, so quitting can't leave a phantom one (Codex) ({state.big.GetValueOrDefault("alimasag_crab")} big, {Has("alimasag_crab")} in the bag)",
            state.big.GetValueOrDefault("alimasag_crab") <= Has("alimasag_crab"));
        pendingShot = "320-sort-top"; yield return 2;
        ClickButton("Turn it over"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("Turn it over shows its underside", sorting?[sortAt].Turned == true);
        pendingShot = "321-sort-under"; yield return 2;
        var crab = sorting[sortAt];
        int bag = Has("alimasag_crab");
        ClickButton(MustGo(crab) ? "Let it go" : "Keep it"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"sorting it right counts ({crab.Note})", crab.Decided && crab.Right && state.sortStreak == 1 && Has("alimasag_crab") == bag + (MustGo(crab) ? 0 : 1));
        pendingShot = "322-sort-done"; yield return 2;
        Check($"the tray's text fits ({lastSortBottom:0})", lastSortBottom <= (Gfx.LH + 510) / 2 - 20);
        for (int guard = 0; guard < 10 && mode == "panel" && panel == "sort"; guard++)
        {
            var c = sorting[sortAt];
            if (!c.Decided) { DecideCrab(!MustGo(c)); yield return 2; }
            ClickButton(sortAt < sorting.Count - 1 ? "Next" : "Done"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        }
        Check("Done closes the tray", mode == "play");
        // A wrong keep: she goes back, and the streak starts again.
        int bag2 = Has("alimasag_crab"), relBefore = state.released;
        sorting = new() { new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 12, Female = true, Berried = true } };
        sortAt = 0; panel = "sort"; mode = "panel"; yield return 2;
        ClickButton("Keep it"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"keeping a crab carrying eggs isn't allowed ({sorting?[0].Note})", sorting[0].Decided && !sorting[0].Right && state.sortStreak == 0 && Has("alimasag_crab") == bag2 && state.released == relBefore);
        pendingShot = "323-sort-wrong"; yield return 2;
        Inp.Tap(KeyboardKey.Enter); yield return 3;
        Check("Enter goes on", mode == "play");
        // Too small, let go with the number key.
        sorting = new() { new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 9.4f, Female = false }, new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 11f, Female = true, Berried = true },
            new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 12.5f, Female = false, Big = true } };
        state.big["alimasag_crab"] = 0;
        sortAt = 0; panel = "sort"; mode = "panel"; yield return 2;
        Inp.Tap(KeyboardKey.Two); yield return 3;
        Check($"2 lets an undersized one go ({sorting?[0].Note})", sorting[0].Right && sorting[0].Note.Contains("legal minimum"));
        // Closing halfway: the berried one goes back by itself.
        int bag3 = Has("alimasag_crab"), rel3 = state.released;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"closing the tray halfway lets the one carrying eggs go (it counts) and keeps the legal one ({state.released - rel3})", mode == "play" && Has("alimasag_crab") == bag3 + 1 && state.released == rel3 + 1);
        // A big crab let go off the tray isn't left counted as a big one in the bag (Codex's review).
        Give("alimasag_crab"); state.big["alimasag_crab"] = 1; int bigBag = Has("alimasag_crab");
        var bigOnes = new List<CrabSort> { new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 16f, Female = true, Berried = true, Big = true },
            new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 17f, Female = false, Big = true } };
        foreach (var c in bigOnes) { Give(c.Id); state.big["alimasag_crab"]++; }   // as AddCatch leaves them
        StartSort(bigOnes); yield return 2;
        Check($"big crabs on the tray are off the big count ({state.big["alimasag_crab"]})", state.big["alimasag_crab"] == 1 && Has("alimasag_crab") == bigBag);
        Inp.Tap(KeyboardKey.Two); yield return 3; Inp.Tap(KeyboardKey.Enter); yield return 3;
        Inp.Tap(KeyboardKey.One); yield return 3; Inp.Tap(KeyboardKey.Enter); yield return 3;
        Check($"letting a big one go leaves it off, keeping one puts it back on ({state.big["alimasag_crab"]})", state.big["alimasag_crab"] == 2 && Has("alimasag_crab") == bigBag + 1);
        // A prize won with the tray open waits until you're back in play, so it isn't hidden behind it.
        state.released = 4; state.hinted.Remove("badge:letgo:0"); state.inv.Remove("dehooker");
        sorting = new() { new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 8.9f, Female = false } };
        sortAt = 0; panel = "sort"; mode = "panel"; yield return 2;
        Inp.Tap(KeyboardKey.Two); yield return 3;
        Check("the fifth one let go is bronze: a dehooker", Has("dehooker") == 1 && badgeNews != null);
        Inp.Tap(KeyboardKey.Enter); yield return 4;
        Check($"closing the tray says what you kept first ({toastMsg})", mode == "play" && toastMsg.StartsWith("Sorted"));
        for (int i = 0; i < 400 && badgeNews != null; i++) yield return 1;
        Check($"then the prize ({toastMsg})", badgeNews == null && toastMsg.Contains("bronze for Let it go"));
        // Eight right in a row and you sort by eye.
        state.sortStreak = QuickSortStreak - 1;
        sorting = new() { new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 13f, Female = false } };
        sortAt = 0; panel = "sort"; mode = "panel"; yield return 2;
        Inp.Tap(KeyboardKey.One); yield return 3;
        Check("eight right in a row and you sort by eye", state.Hinted("quickSort"));
        Inp.Tap(KeyboardKey.Enter); yield return 3;
        state.pots[$"{ptx},{pty}"] = state.day - 1; (player.X, player.Y, player.Face) = (potX, potY, potFace); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"then a haul sorts itself ({toastMsg})", mode == "play" && sorting == null && toastMsg.StartsWith("You haul up the pot"));
        // A prize from a pot that sorts itself waits for the haul's message (Codex's second review).
        state.released = 14; state.hinted.Remove("badge:letgo:1"); badgeNews = null;
        var small = new CrabSort { Id = "alimasag_crab", Line = 10.2f, Width = 8f, Female = false };
        Give("alimasag_crab");
        string note = StartSort(new() { small }); Toast("You haul up the pot: 1 alimasag." + note, 4.5f); yield return 2;
        Check($"the haul's message shows first, the prize waits ({toastMsg})", toastMsg.StartsWith("You haul up the pot") && badgeNews?.Contains("silver for Let it go") == true);
        toastTimer = 0; yield return 3;
        Check($"then the prize ({toastMsg})", toastMsg.Contains("silver for Let it go"));
        state.builds.Remove(pot); ReindexBuilds();

        /* ---------- Ma'am Isay's class ---------- */
        Note("Ma'am Isay's class");
        FreshEduGame("Pupil");
        var knownIds = Data.Spots.Where(s => s.Biome is "amihan" or "saltmere" && s.Scene == "world").SelectMany(s => Data.Common[s.Id])
            .Where(f => !f.Legend && FishFacts.ById.ContainsKey(f.Id)).Select(f => f.Id).Distinct().Take(12).ToList();
        // Questions only come from fish you know: three isn't enough.
        foreach (var id in knownIds.Take(3)) state.commons[id] = 1;
        var isay = IslanderWalk("isay");
        player.X = isay.X - 10; player.Y = isay.Y + 10; player.Face = "up"; state.clock = 10 * 60; yield return 3;
        Check($"Ma'am Isay is at the school ({target?.Label})", target?.Type == "islander" && target.Id == "isay" && target.Label == "Talk to Ma'am Isay");
        pendingShot = "330-school"; yield return 2;
        Check("the children sit on the bench in school hours, and it's solid", SchoolHours && !CanStand(SchoolX, SchoolY + 18));
        Inp.Tap(KeyboardKey.E); yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        yield return 2;
        Check("she asks you to help, and opens the class", mode == "panel" && panel == "quiz" && state.Hinted("metIsay") && quiz?.View == "intro");
        Check("with three fish there's no lesson yet", !Gfx.Seen.ContainsKey("quiz:start") || KnownFish().Count < MinKnown);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        foreach (var id in knownIds) { state.commons[id] = 1; state.know[id] = 2; }
        yield return 2;
        Check($"the guide has today's lesson ({target?.Label})", goals.Any(g => g.Id == "isay:lesson" && g.Group == "daily") && target?.Label == "Today's lesson with Ma'am Isay");
        Inp.Tap(KeyboardKey.E); yield return 4;
        Gfx.Seen.Remove("quiz:start"); yield return 2;
        ClickButton("quiz:start"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"Start begins eight questions ({quiz?.View}, {quiz?.Qs.Count})", quiz?.View == "question" && quiz.Qs.Count == LessonLength && quiz.Paid);
        pendingShot = "331-quiz-question"; yield return 2;
        int coinsQ = state.coins, knowSum = state.know.Values.Sum();
        bool fits = true, named = true;
        for (int i = 0; i < LessonLength; i++)
        {
            var qq = quiz.Qs[quiz.At];
            int pick = i == 2 ? (qq.Answer + 1) % qq.Options.Length : qq.Answer;
            if (i == 4) { ClickButton("quiz:ask"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
                Check($"asking the class shows their votes ({string.Join(",", quiz.Votes ?? new int[0])})", quiz.Votes?.Sum() == 100 && quiz.Votes[qq.Answer] >= 35 && quiz.AskUsed); }
            if (i == 5) { Inp.Tap((KeyboardKey)((int)KeyboardKey.One + pick)); yield return 3; }
            else { ClickButton($"quiz:{pick}"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1; }
            fits &= lastQuizBottom <= (Gfx.LH + 650) / 2 - 20;
            if (i == 2) { pendingShot = "332-quiz-wrong"; yield return 2; }
            if (i == 3) { pendingShot = "333-quiz-right"; yield return 2; }
            if (quiz.View != "answer") { named = false; break; }
            ClickButton("quiz:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        }
        Check($"answering by clicks and by number keys, with one wrong ({quiz?.Right} right, {quiz?.Hearts} hearts)", named && quiz?.View == "done" && quiz.Right == LessonLength - 1 && quiz.Hearts == 2);
        Check($"today's lesson pays from the school fund ({state.coins - coinsQ} coins)", state.coins - coinsQ == 6 * (LessonLength - 1) && state.lessonDay == state.day && state.lessons == 1);
        // (A question about a place, 1.18.1, scores but isn't about a fish.)
        int fishRight = quiz.Qs.Where((q, k) => k != 2 && q.Subject != null).Count();
        Check($"every right answer about a fish counts toward learning it, and three learns it ({quiz.Learned.Count} learned)", state.know.Values.Sum() == knowSum + fishRight && quiz.Learned.Count >= 3 && quiz.Learned.All(Learnt));
        Check("the questions and answers fit the panel", fits);
        pendingShot = "334-quiz-done"; yield return 2;
        Check($"a prize won in the lesson is on its results card ({string.Join("; ", lessonPrizes ?? new())})", lessonPrizes?.Any(p => p.Contains("Fish ID")) == true);
        Check($"five fish learned is bronze for Fish ID: Ma'am Isay's field guide ({LearnedCount})", LearnedCount >= 5 && Has("field_guide") == 1 && state.Hinted("badge:fishid:0"));
        // Practice after that: no coins.
        ClickButton("quiz:again"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("another lesson is practice", quiz?.View == "question" && !quiz.Paid);
        while (quiz?.View is "question" or "answer")
        {
            if (quiz.View == "question") AnswerQuiz(quiz.Qs[quiz.At].Answer); else NextQuestion();
            yield return 1;
        }
        Check($"a perfect practice pays nothing ({quiz?.Coins})", quiz?.View == "done" && quiz.Coins == 0 && quiz.Right == LessonLength);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("no lesson goal once today's is done", !goals.Any(g => g.Id == "isay:lesson"));
        // The field guide names a learned fish at the bite.
        var learnedFish = Data.FishById[state.know.First(kv => kv.Value >= LearnedAt && Data.SpotOfFish.ContainsKey(kv.Key)).Key];
        TestBite(Data.SpotOfFish[learnedFish.Id], learnedFish.Id); yield return 2;
        Check($"with the field guide, a learned fish is named at the bite ({prompt.Text})", prompt.Text.Contains(learnedFish.Name.ToLowerInvariant()));
        fish = null; mode = "play"; yield return 2;
        // Every kind of question is well formed, and the fact questions never name the fish.
        var known = KnownFish();
        bool ok = true, redacted = true, spots = true;
        foreach (var kind in new[] { "picture", "shadow", "where", "when", "fight", "heavier", "fact", "real", "sci", "bait", "family", "water", "diet", "notfish", "isle", "waterbody" })
            for (int i = 0; i < 30; i++)
            {
                var q = MakeQuestion(kind, known, new());
                if (q == null) continue;
                ok &= q.Options.Length >= 2 && q.Options.Distinct().Count() == q.Options.Length && q.Answer >= 0 && q.Answer < q.Options.Length && q.Teach != null;
                if (kind is "picture" or "shadow" or "fact" or "sci" or "notfish") ok &= q.Options[q.Answer] == Data.FishById[q.Subject].Name;
                if (kind == "fact")
                    redacted &= !Data.FishById[q.Subject].Name.Split(' ', '(', ')').Where(w => w.Length >= 4).Any(w => q.Quote.Contains(w, StringComparison.OrdinalIgnoreCase));
                if (kind == "where")
                    spots &= q.Options.All(o => Data.Spots.Any(s => s.Label == o && Data.Common[s.Id].Any(f => state.commons.GetValueOrDefault(f.Id) > 0)));
            }
        Check("every kind of question is well formed", ok);
        Check("a fact question never names its fish", redacted);
        Check("'where' only offers places you've fished", spots);
        // The Fish log: a learned fish has a star, and its card says so.
        dexFish = learnedFish.Id; panel = "dex"; mode = "panel"; yield return 3;
        pendingShot = "335-dex-learned"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- The Sea school ---------- */
        Note("The Sea school badges and journal");
        Inp.Tap(KeyboardKey.Q); yield return 3;
        ClickButton("Sea school"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check("the journal's Sea school tab opens", mode == "panel" && panel == "journal" && journalSide == "school");
        pendingShot = "340-sea-school"; yield return 2;
        Check($"and fits the journal ({lastJournalBottom:0})", lastJournalBottom <= (Gfx.LH + 650) / 2 - 24);
        ClickButton("Getting started"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"Getting started still fits beside it ({lastJournalBottom:0})", journalSide == "basics" && lastJournalBottom <= (Gfx.LH + 650) / 2 - 24);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        int cB = state.coins;
        state.released = 15; CheckBadges();
        Check("bronze and silver for letting go: a dehooker and 150 coins, once", Has("dehooker") == 1 && state.coins == cB + 150);
        CheckBadges();
        Check("each tier pays once", Has("dehooker") == 1 && state.coins == cB + 150);
        state.riddles = 14; state.released = 40; foreach (var id in Data.AllCommon.Take(30)) state.know[id.Id] = 3;
        cB = state.coins;
        CheckBadges();
        Check("gold everywhere: the gleaner's basket, tide watch, steward's badge, gold star pin and the diploma",
            Has("gleaner_basket") == 1 && Has("tide_watch") == 1 && Has("steward_badge") == 1 && Has("gold_star_pin") == 1 && state.Hinted("diploma") && state.coins == cB + 150 + 60 + DiplomaCoins);
        Inp.Tap(KeyboardKey.Q); yield return 3;
        ClickButton("Sea school"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        pendingShot = "341-sea-school-gold"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        SetTideTo(LowAt(FindLow(0.5f)));
        int basketQuota = GleanQuota; state.inv.Remove("gleaner_basket"); int plainQuota = GleanQuota; Give("gleaner_basket");
        Check($"the basket gleans half as much again ({basketQuota} against {plainQuota})", basketQuota == (int)MathF.Round(plainQuota * 1.5f));
        player.X = 160; player.Y = 115; yield return 2;
        Check($"the tide watch tells the tide anywhere ({TideLine()})", Wears("tide_watch") && !InHabagat);

        /* ---------- Guide and old saves ---------- */
        Note("The guide and old saves");
        state.hinted.Remove("metIsay"); state.hinted["amihan"] = true; yield return 2;
        Check($"Ma'am Isay is one of the people of Amihan to meet ({goals.FirstOrDefault(g => g.Id == "meet:amihan")?.Title})", goals.FirstOrDefault(g => g.Id == "meet:amihan")?.Title.Contains("of 6") == true);
        Check("nothing to follow into a spoiler: the guide never names the Starwell", goals.All(g => !g.Text.Contains("Starwell") && !g.Title.Contains("Starwell")));
        SaveFile.Clear(3);
        File.WriteAllText(SaveFile.SlotPath(3), "{\"created\":true,\"inv\":{\"rod_old\":1}}");
        var old = SaveFile.Read(3);
        Check("older saves load with the Sea school's defaults", old != null && old.know != null && old.letGo != null && old.reefLow == -1 && old.released == 0 && old.lessonDay == 0);
        SaveFile.Clear(3);
        Inp.ScriptMouse = Offscreen;
    }
}
#endif
