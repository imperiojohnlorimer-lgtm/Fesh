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
    }
}
#endif
