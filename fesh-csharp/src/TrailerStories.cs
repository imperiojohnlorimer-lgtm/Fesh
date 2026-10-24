#if DEBUG
using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The trailer's story scenes: the first creature of the Saltmere mystery, the restless sea, Baga's volcano, Marilaya's
// sandbar and Bakunawa's eclipse.
partial class Game
{
    // Story 1: a night cast in the lagoon, and the first messenger from the Lalim.
    IEnumerable<int> ShotGlowgill()
    {
        TrailerGame(night: true, day: 2);
        state.flags.metTomas = true;
        foreach (var _ in TakeStand("lagoon")) yield return 0;
        float sx = standX, sy = standY;
        (player.X, player.Y) = WalkStart(sx, sy, 50, prefX: 1);
        foreach (var _ in Frames(6)) yield return 0;
        Roll(7f, "STORY 1", "The guardian of the deep", "Five creatures rise from the Lalim. Why now?");
        foreach (var _ in WalkTo(sx, sy, 120)) yield return 0;
        FaceToSpot("lagoon");
        var gl = Data.Creatures.First(c => c.Id == "glowgill");
        foreach (var _ in CastAndHook(20, perfect: false, roll: new Catchable { Id = gl.Id, Name = gl.Name, Difficulty = 1.8f, Exotic = true })) yield return 0;
        foreach (var _ in Reel(500)) yield return 0;
        foreach (var _ in Frames(200)) yield return 0;
    }

    // Story 3: the ground shakes in Amihan Village, then everyone up to School Rise before the sea comes back.
    IEnumerable<int> ShotRestlessSea()
    {
        TrailerGame(day: 6);
        state.clock = 10 * 60;
        foreach (var k in new[] { "rs:tremor", "rs:carpio", "rs:berberoka", "rs:mark", "rs:signs", "rs:sign0", "rs:sign1", "rs:sign2", "rs:drill", "amihan" }) state.hinted[k] = true;
        state.gifts["rs_ready"] = 1; state.gifts["rs_drill"] = 2;
        (player.X, player.Y) = StandableNear(1560, 200);
        foreach (var _ in Frames(4)) yield return 0;
        // Somewhere out in the open, away from the palms and houses.
        for (int i = 0; i < 40 && !InTheOpen; i++) { (player.X, player.Y) = StandableNear(1540 + i * 3, 196 + i % 5 * 4); yield return 0; }
        foreach (var _ in Frames(3)) yield return 0;
        Roll(9f, "STORY 3", "The restless sea", "Drop, cover, hold on. Then get everyone uphill.");
        foreach (var _ in Frames(20)) yield return 0;
        StartEmergency();
        foreach (var _ in Frames(12)) yield return 0;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 400 && evac is { Phase: "quake" }; i++) yield return 0;
        Inp.Hold(KeyboardKey.E, false);
        foreach (var _ in Frames(10)) yield return 0;
        foreach (var _ in WalkTo(RiseX, RiseY + 4, 700, ok: (x, y) => CanStand(x, y) && !InSea(x, y))) yield return 0;
        player.Face = "left";
        foreach (var _ in Frames(200)) yield return 0;
    }

    bool InSea(float x, float y) => SeaEmergency && FloodAt(x, y);

    // ...and from the rise, the sea drawn back past the reef and coming in again.
    IEnumerable<int> ShotRestlessSeaWave()
    {
        if (evac == null) yield break;
        player.X = RiseX - 10; player.Y = RiseY + 6; player.Face = "left";
        for (int i = 0; i < 4000 && evac != null && evac.T < 27; i++) yield return 0;
        Roll(5f);
        for (int i = 0; i < 400; i++) { if (i % 60 == 20) player.Face = i % 120 == 20 ? "left" : "down"; yield return 0; }
    }

    // Story 4: Baga at Level 3, the order to leave, and the ash coming down on the village.
    // Story 4: Baga at Level 3 and the order to leave. The bag is packed already; call Manay Mila, sign the evacuation
    // list, and board Niko's banca, as the prompt asks.
    IEnumerable<int> ShotBaga()
    {
        TrailerGame(day: 6);
        state.clock = 9 * 60;
        foreach (var k in new[] { "mg:metMila", "mg:abaca", "mg:pili", "mg:story", "mg:metBen", "mg:unrest", "mg:read1", "mg:read2", "mg:signs", "mg:order", "mg:bag", "amihan" }) state.hinted[k] = true;
        state.gifts["mg_order"] = 6; state.bagaAlert = 3;
        StartGame(false); ClearSkies(); quietWildlife = true;
        (player.X, player.Y) = StandableNear(BenX + 10, BenY + 22);
        foreach (var _ in Frames(6)) yield return 0;
        Roll(10f, "STORY 4", "Beneath the clouds", "Read the warnings. Leave early.");
        IEnumerable<int> Do(float x, float y, string type)
        {
            foreach (var _ in WalkTo(x, y, 240)) yield return 0;
            for (int i = 0; i < 12 && target?.Type != type; i++) { player.Face = i % 4 == 0 ? "up" : player.Face; yield return 0; }
            Inp.Tap(KeyboardKey.E);
            foreach (var _ in Frames(8)) yield return 0;
            for (int i = 0; i < 300 && mode == "dialogue"; i++) yield return 0;
        }
        var (mx, my) = StandableNear(MilaX + 6, MilaY + 14);
        foreach (var _ in Do(mx, my, "callmila")) yield return 0;
        var (rx, ry) = StandableNear(EvacTableX, EvacTableY + 12);
        foreach (var _ in Do(rx, ry, "register")) yield return 0;
        var (jx, jy) = StandableNear(BoardPointX, BoardPointY + 6);
        foreach (var _ in Do(jx, jy, "boardevac")) yield return 0;
        foreach (var _ in Frames(300)) yield return 0;
    }

    IEnumerable<int> ShotBagaAsh()
    {
        TrailerGame(day: 6);
        foreach (var k in new[] { "mg:metMila", "mg:abaca", "mg:pili", "mg:story", "mg:metBen", "mg:unrest", "mg:read1", "mg:read2", "mg:signs", "mg:order", "mg:evacuated", "mg:preHelped", "mg:ashStarted", "amihan" }) state.hinted[k] = true;
        state.gifts["mg_order"] = 6; state.bagaAlert = 3; state.clock = 10 * 60;
        state.px = SchoolDoorX + 50; state.py = SchoolDoorY + 40;
        StartGame(false); ClearSkies(); quietWildlife = true;
        mgAshT = 20;
        player.X = SchoolDoorX + 50; player.Y = SchoolDoorY + 40;
        foreach (var _ in Frames(8)) yield return 0;
        Roll(4f);
        foreach (var _ in WalkTo(SchoolDoorX, SchoolDoorY + 6, 200)) yield return 0;
        player.Face = "up";
        foreach (var _ in Frames(8)) yield return 0;
        if (target?.Type == "schooldoor") Inp.Tap(KeyboardKey.E);
        foreach (var _ in Frames(200)) yield return 0;
    }

    // Story 5: Marilaya, a walk out along the sandbar at low tide.
    IEnumerable<int> ShotMarilaya()
    {
        TrailerGame(day: 8);
        state.hinted["marilaya"] = true; state.hinted["amihan"] = true;
        // A day whose low tide comes in daylight.
        for (int d = 8; d < 40; d++) { state.day = d; SetLowTide(); if (state.clock is > 9 * 60 and < 16 * 60) break; }
        ClearSkies();
        var amb = MlWalk("ambo");
        var (px, py) = MlPoolAt(0);
        (player.X, player.Y) = StandableNear(amb.X + 14, amb.Y + 10);
        foreach (var _ in Frames(8)) yield return 0;
        Roll(6f, "STORY 5", "Marilaya", "An island far to the east, and the heart that rose");
        foreach (var _ in WalkTo(px, py + 7, 400, 1, (x, y) => CanStand(x, y, flats: true))) yield return 0;
        player.Face = "up";
        for (int i = 0; i < 10 && target?.Id != "pool0"; i++) yield return 0;
        keepTalk = true; Inp.Tap(KeyboardKey.E);
        foreach (var _ in Frames(70)) yield return 0;
        keepTalk = false;
        foreach (var _ in Frames(200)) yield return 0;
    }

    // The vanishing moon: Bakunawa rises off Parola, and the islanders beat gongs and pots to make it spit the moon out.
    IEnumerable<int> ShotBakunawa()
    {
        TrailerGame(night: true, day: 9);
        state.parola = 3; state.hinted["parolaLit"] = true; state.hinted["habagat"] = true;
        foreach (var c in Data.MoonClues) state.hinted[c.Key] = true;
        state.inv["agong"] = 1;
        player.X = 1190; player.Y = 640; player.Face = "up";
        foreach (var _ in Frames(4)) yield return 0;
        player.Y = 625;
        for (int i = 0; i < 200 && eclipse == null; i++) yield return 0;
        for (int i = 0; i < 400 && mode == "dialogue"; i++) yield return 0;
        Roll(10f, "STORY", "The vanishing moon", "Bakunawa swallows the moon. Make it spit it out.");
        for (int i = 0; i < 1200 && eclipse != null; i++)
        {
            state.hp = Math.Max(state.hp, 60);
            if (eclipse.Phase == "bang" && eclipse.Writhe <= 0 && eclipse.Roar <= 0 && !eclipse.Judged && (eclipse.Beat < 0.05f || eclipse.Beat > BeatLen - 0.05f)) Inp.Tap(KeyboardKey.E);
            // Out from under the water it spits.
            if (eclipse.Spouts.Any(s => Dist(s.x, s.y, player.X, player.Y) < 16)) Inp.ScriptStick = new Vector2(player.X < 1190 ? 1 : -1, 0);
            else Inp.ScriptStick = null;
            yield return 0;
        }
    }

    // ...and the end of it: the moon coming back.
    IEnumerable<int> ShotBakunawaEnd()
    {
        if (eclipse == null) yield break;
        Inp.ScriptStick = null;
        eclipse.Verse = 3; eclipse.Noise = NoiseNeeded - 2; eclipse.Roar = 0; eclipse.Writhe = 0;
        foreach (var _ in Frames(2)) yield return 0;
        Roll(5f);
        for (int i = 0; i < 600; i++)
        {
            if (eclipse is { Phase: "bang" } e && e.Writhe <= 0 && e.Roar <= 0 && !e.Judged && (e.Beat < 0.05f || e.Beat > BeatLen - 0.05f)) Inp.Tap(KeyboardKey.E);
            yield return 0;
        }
    }
}
#endif
