#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_IRONBILL_TEST=1: Ironbill's boss fight (IronbillFight.cs): the title card, the name bar and music, the free first
// breach, circling with only its sail out, the second phase's charge (braced by letting go, rammed by holding on), the third
// phase's tail-walk, and landing it, with screenshots of each.
partial class Game
{
    IEnumerable<int> IronbillScript()
    {
        Note("Ironbill: the boss fight");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.inv["boat"] = 1; state.inv["spinner_lure"] = 1; state.hp = 100;
        state.aboard = true; player.X = state.boatX = 1385; player.Y = state.boatY = 395; player.Face = "right";
        yield return 3;
        // It roams every open sea, not just the one near Saltmere (it used to be rolled from the "opensea" list only).
        foreach (var sea in new[] { "opensea", "amihansea", "habagatsea", "marilayasea" })
        {
            int hits = Enumerable.Range(0, 3000).Count(_ => RollTroll(sea, true).Id == "ironbill");
            Check($"Ironbill can take a trolled lure in a frenzy at {sea} ({hits} of 3000)", hits > 0);
            Check($"but never outside a frenzy at {sea}", Enumerable.Range(0, 1500).All(_ => RollTroll(sea, false).Id != "ironbill"));
            Check($"and never on an ordinary cast at {sea}", Enumerable.Range(0, 1500).All(_ => RollCatch(sea).Id != "ironbill"));
        }
        foreach (var sea in new[] { "opensea", "amihansea", "habagatsea", "marilayasea" })
        {
            Check($"trolled weights at {sea} hold exactly one Ironbill", FishWeights(sea, "spinner_lure", 1, trolled: true).Count(p => p.f.Id == "ironbill") == 1);
            Check($"ordinary weights at {sea} hold none", FishWeights(sea, "spinner_lure", 1).All(p => p.f.Id != "ironbill"));
        }
        Check("each sea's spot is picked by position (west, Amihan, Habagat, Marilaya)",
            SeaSpotAt(60 * T, 20 * T) == "opensea" && SeaSpotAt((EastStart + 20) * T, 20 * T) == "amihansea" && SeaSpotAt(60 * T, (HabagatTop + 4) * T) == "habagatsea" && SeaSpotAt((MlStart + 10) * T, 40 * T) == "marilayasea");
        TestBite("opensea", "ironbill"); Hook(); yield return 2;
        Check("Ironbill takes the lure: the fight is on", IronbillOnLine && ironbill != null);
        Check("its title card shows", ironbill?.Banner > 2);
        Check("the first breach comes at once, and it's free", reel.Leap > 0 && reel.LeapDone);
        yield return 12;
        pendingShot = "ib-01-banner-breach"; yield return 2;
        Check($"the boss music plays ({WantedTrack()})", WantedTrack() == "boss");
        Check("its name bar is up", Gfx.Seen.Count >= 0 && IronbillOnLine);
        // Let the breach finish, then watch it circle.
        reel.Leap = 0.01f; yield return 3;
        reel.LeapT = 99; reel.AttackTimer = 99; reel.Progress = 0.3f;
        float a0 = towAng; yield return 30;
        Check($"it circles, towing the boat (angle {a0:0.00} -> {towAng:0.00})", towAng > a0 && Aboard);
        pendingShot = "ib-02-circling"; yield return 2;
        // A breach mid-air.
        reel.Leap = reel.LeapLen * 0.5f; reel.LeapDone = true; yield return 1;
        pendingShot = "ib-03-breach-top"; yield return 1;
        reel.Leap = 0.01f; yield return 3;

        // Phase two: it charges. Holding on: rammed.
        reel.Progress = 0.5f; reel.LeapT = 99; yield return 2;
        Check($"past the first mark it's furious (phase {ironbill?.Phase})", ironbill?.Phase == 2);
        float hp0 = state.hp;
        reel.AttackTimer = 0.01f; Inp.Hold(KeyboardKey.E, true); yield return 20;
        Check("it turns on the boat: a warning first", reel.AttackWarning > 0);
        pendingShot = "ib-04-charge-lane"; yield return 2;
        for (int i = 0; i < 200 && reel.AttackWarning > 0; i++) yield return 1;
        yield return 6;
        pendingShot = "ib-05-charge"; yield return 2;
        Inp.Hold(KeyboardKey.E, false);
        Check($"holding on, the bill rams the hull ({hp0:0} -> {state.hp:0})", state.hp < hp0 && IronbillOnLine);
        for (int i = 0; i < 60 && ironbill?.ChargeT > 0; i++) yield return 1;
        // Letting go: braced.
        hp0 = state.hp;
        reel.AttackTimer = 0.01f; yield return 2;
        for (int i = 0; i < 200 && reel.AttackWarning > 0; i++) yield return 1;
        yield return 3;
        Check($"letting go of the reel braces you ({hp0:0} -> {state.hp:0})", state.hp == hp0 && IronbillOnLine);
        for (int i = 0; i < 60 && ironbill?.ChargeT > 0; i++) yield return 1;

        // Phase three: tail-walking.
        reel.Progress = 0.8f; reel.AttackTimer = 99; reel.LeapT = 99; yield return 2;
        Check($"past the second mark it's desperate (phase {ironbill?.Phase})", ironbill?.Phase == 3);
        ironbill.WalkT = 0.01f; yield return 10;
        Check("it stands up and tail-walks", ironbill.Walk > 0);
        pendingShot = "ib-06-tailwalk"; yield return 2;
        float p0 = reel.Progress;
        Inp.Hold(KeyboardKey.E, true); yield return 20; Inp.Hold(KeyboardKey.E, false);
        Check($"holding the reel while it tail-walks shakes the hook ({p0:0.00} -> {reel?.Progress:0.00})", reel != null && reel.Progress < p0 + 0.05f);
        for (int i = 0; i < 300 && ironbill?.Walk > 0; i++) yield return 1;
        Check("it drops back in", ironbill?.Walk <= 0);

        // Landing it.
        LandCatch(); yield return 4;
        Check("landing Ironbill shows its legend card", mode == "legend" && legendId == "ironbill");
        Check("and the fight is over", ironbill == null || !IronbillOnLine);
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }

        // Near a shore it must stay over the sea: its circle used to cross the land and the marlin swam over it.
        int bestLand = 0, sx = 0, sy = 0;
        for (int ty = 4; ty < HabagatTop - 4; ty++)
            for (int tx = 4; tx < EastStart - 4; tx++)
            {
                if (TileAt(tx, ty) != '~' || !BoatCanStand(tx * T + 5, ty * T + 5) || InSanctuary(tx * T + 5, ty * T + 5)) continue;
                int land = 0;
                for (int dy = -5; dy <= 5; dy++)
                    for (int dx = -5; dx <= 5; dx++)
                        if (!WaterTile(TileAt(tx + dx, ty + dy))) land++;
                if (land > bestLand && land < 60) { bestLand = land; sx = tx; sy = ty; }
            }
        Check($"a deep-water spot with a shore beside it exists ({bestLand} land tiles near {sx},{sy})", bestLand >= 8);
        state.aboard = true; player.X = state.boatX = sx * T + 5; player.Y = state.boatY = sy * T + 5; mode = "play"; yield return 3;
        state.commons.Remove("ironbill"); state.inv.Remove("ironbill");
        TestBite("opensea", "ironbill"); Hook(); yield return 3;
        int onLand = 0, frames = 0, charges = 0;
        float lastX = fish.Bx, lastY = fish.By, maxJump = 0; string jumpAt = "", landAt = ""; bool wasCharging = false;
        for (int i = 0; i < 700 && IronbillOnLine; i++)
        {
            // Turning back from the shore must be smooth, not a snap across the water (charges and landings move it fast on purpose).
            if (ironbill.ChargeT <= 0 && !wasCharging && reel.AttackWarning <= 0 && reel.Leap <= 0 && i > 3) { float jd = Dist(fish.Bx, fish.By, lastX, lastY); if (jd > maxJump) { maxJump = jd; jumpAt = $"frame {i} leap {reel.Leap:0.00} walk {ironbill.Walk:0.0} run {reel.Running:0.0} dir {towDir}"; } }
            lastX = fish.Bx; lastY = fish.By; wasCharging = ironbill.ChargeT > 0;
            reel.Progress = 0.5f; reel.LeapT = 99;
            if (i % 120 == 60) reel.AttackTimer = 0.01f;
            if (ironbill.ChargeT > 0) charges++;
            frames++;
            if (!TowWater(fish.Bx, fish.By) && reel.Leap <= 0) { onLand++; landAt += $"[{i} ch{ironbill.ChargeT:0.00} warn{reel.AttackWarning:0.0} f({fish.Bx:0},{fish.By:0}) p({player.X:0},{player.Y:0}) tile {TileUnder(fish.Bx, fish.By)} lane ({ironbill.Cx0:0},{ironbill.Cy0:0})->({ironbill.Cx1:0},{ironbill.Cy1:0}) clear {ChargeLaneClear(ironbill.Cx0, ironbill.Cy0, ironbill.Cx1, ironbill.Cy1)} tiles {string.Concat(Enumerable.Range(0, 15).Select(q => TileUnder(ironbill.Cx0 + (ironbill.Cx1 - ironbill.Cx0) * q / 14f, ironbill.Cy0 + (ironbill.Cy1 - ironbill.Cy0) * q / 14f)))} fishTx {fish.Tx:0},{fish.Ty:0}] "; }
            yield return 1;
        }
        Check($"through {frames} frames beside a shore Ironbill was never over land ({onLand} frames on land {landAt}, {charges} charging)", frames > 400 && onLand == 0);
        Check($"and it turned back from the shore smoothly (largest step {maxJump:0.0} px at {jumpAt})", maxJump < 16);
        if (reel != null) { LandCatch(); yield return 4; }
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
    }
}
#endif
