#if DEBUG
using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The trailer's legends and bosses (TrailerScenes.cs has the walking and reeling they share).
partial class Game
{
    string botEv = ""; int botStep; float botT;

    // Plays a legend's events the way the game asks (the prompt on screen says the same), and keeps them coming.
    void BossBot()
    {
        var r = reel;
        if (r == null) return;
        string ev = r.Roll.Id switch
        {
            MireFish => mire?.Ev, RayFish => ray?.Ev, TarponFish => tarpon?.Ev, LungfishFish => lung?.Ev, WhiskersFish => whisk?.Ev, _ => ""
        } ?? "";
        if (ev != botEv) { botEv = ev; botStep = 0; botT = 0; }
        botT += FixedDt;
        foreach (var k in new[] { KeyboardKey.Up, KeyboardKey.Down, KeyboardKey.Left, KeyboardKey.Right }) Inp.Hold(k, false);
        if (r.AttackWarning > 0 || ironbill is { Walk: > 0 }) Inp.Hold(KeyboardKey.E, false);   // brace for a charge; let it tail-walk
        switch (r.Roll.Id)
        {
            case MireFish when mire != null:
                if (ev == "") mire.NextEv = MathF.Min(mire.NextEv, 2.5f);
                if (ev == "gulp" && botStep == 0 && botT > 0.25f) { Inp.Tap(KeyboardKey.E); botStep = 1; }
                if (ev == "roots" && botStep == 0 && botT > 0.3f)
                {
                    var rt = MireRoots()[mire.RootI];
                    Inp.Tap(ArrowKey(rt.bx < rt.wx ? 1 : 3)); botStep = 1;
                }
                break;
            case RayFish when ray != null:
                if (ev == "") ray.NextEv = MathF.Min(ray.NextEv, 2.5f);
                if (ev == "sweep") { Inp.Hold(KeyboardKey.Right, ray.Rod < ray.Mark - 0.04f); Inp.Hold(KeyboardKey.Left, ray.Rod > ray.Mark + 0.04f); }
                if (ev is "loopWarn" or "loop") Inp.Hold(KeyboardKey.E, false);
                if (ev is "breachWarn" or "breach") Inp.Hold(KeyboardKey.Down, true);
                break;
            case TarponFish when tarpon != null:
                if (ev == "") tarpon.NextEv = MathF.Min(tarpon.NextEv, 2.5f);
                if (ev == "breath") Inp.Hold(KeyboardKey.Up, true);
                if (ev == "pilings") { Inp.Hold(KeyboardKey.E, false); if (botStep++ % 4 == 0) Inp.Tap(KeyboardKey.E); }
                if (ev == "shake") Inp.Hold(KeyboardKey.E, true);
                break;
            case LungfishFish when lung != null:
                if (ev == "") lung.NextEv = MathF.Min(lung.NextEv, 2.5f);
                if (ev != "") Inp.Hold(KeyboardKey.E, false);
                if (ev == "breath" && botStep < 3 && LungBreathLen - lung.EvT >= LungBeats[botStep] - 0.02f) { Inp.Tap(KeyboardKey.E); botStep++; }
                if (ev == "march" && lung.Step < LungMarch.Length && (LungMarchLen - lung.EvT) - lung.LastStep > 0.3f && botT > 0.15f) { Inp.Tap(ArrowKey(LungMarch[lung.Step])); botT = 0; }
                if (ev == "cradle") { float a = LungCradleLen - lung.EvT; Inp.Hold(KeyboardKey.E, a - (int)MathF.Min(a, 2.999f) < 0.58f); }
                break;
            case WhiskersFish when whisk != null:
                if (ev == "") whisk.NextEv = MathF.Min(whisk.NextEv, 2.5f);
                if (ev != "") Inp.Hold(KeyboardKey.E, false);
                if (ev == "thief" && botStep == 0 && whisk.Delay + WhiskWindow - whisk.EvT >= whisk.Delay + 0.1f) { Inp.Tap(KeyboardKey.E); botStep = 1; }
                if (ev == "memory" && botStep < 3 && botT > 0.3f) { Inp.Tap(ArrowKey(whisk.Seq[botStep])); botStep++; botT = 0; }
                break;
        }
    }

    // Stands on the bank of a spot (after a walk up to it), casts and hooks one of the legends, then fights it.
    IEnumerable<int> LegendShot(string spot, string fishId, float secs, string title, string sub, float walk = 45, float boost = 0)
    {
        foreach (var _ in TakeStand(spot)) yield return 0;
        if (target?.Type != "spot") Check($"{spot}: no stand found", false);
        float sx = standX, sy = standY;
        (player.X, player.Y) = WalkStart(sx, sy, walk);
        foreach (var _ in Frames(6)) yield return 0;
        Roll(secs, "LEGEND", title, sub);
        foreach (var _ in WalkTo(sx, sy, 90)) yield return 0;
        FaceToSpot(spot);
        foreach (var _ in CastAndHook(16, fishId, perfect: false)) yield return 0;
        foreach (var _ in Reel(900, boost, BossBot)) yield return 0;
    }

    IEnumerable<int> ShotMire()
    {
        TrailerGame(night: true);
        state.inv["worm"] = 10; state.tackle["bait"] = "worm";
        return LegendShot("swamp", MireFish, 7f, "The Mire leviathan", "Mirewood's swamp, after dark");
    }

    IEnumerable<int> ShotRay()
    {
        TrailerGame(night: true, day: 9);
        state.inv["glow_shrimp"] = 10; state.tackle["bait"] = "glow_shrimp"; state.hinted["visitedAtoll"] = true;
        return LegendShot("dropoff", RayFish, 7f, "The Starfall ray", "The drop-off, under a full moon");
    }

    IEnumerable<int> ShotTarpon()
    {
        TrailerGame(night: true, day: 9);
        state.inv["tamban"] = 10; state.tackle["bait"] = "tamban";
        state.hinted["habagat"] = true; state.hinted["moonReturned"] = true; state.hinted["parolaLit"] = true; state.parola = 3;
        return LegendShot("parola", TarponFish, 6f, "Haring Buan-buan", "The king tarpon of Parola's pier");
    }

    IEnumerable<int> ShotLungfish()
    {
        TrailerGame(day: 5);
        state.clock = 15 * 60;
        state.inv["cricket"] = 10; state.tackle["bait"] = "cricket";
        return LegendShot("oasis", LungfishFish, 4.5f, "The Sunscale lungfish", "Sunscald's desert oasis");
    }

    IEnumerable<int> ShotWhiskers()
    {
        TrailerGame(day: 5);
        state.forecast = new() { new WeatherSpell { at = 0, w = "rain" } }; state.weather = planned = "rain"; SnapWeather();
        state.inv["berries"] = 10; state.tackle["bait"] = "berries";
        return LegendShot("lagoon", WhiskersFish, 4.5f, "Old Whiskers", "Saltmere's lagoon, in the rain");
    }

    // Ironbill: trolling a lure behind the banca, and the marlin that takes it and tows the boat.
    IEnumerable<int> ShotIronbill()
    {
        TrailerGame(day: 4);
        state.clock = 10 * 60;
        state.inv["boat"] = 1; state.inv["big_sail"] = 1; state.inv["spinner_lure"] = 1;
        state.aboard = true; player.X = state.boatX = 1385; player.Y = state.boatY = 395; player.Face = "right"; boatFace = "right";
        foreach (var _ in Frames(8)) yield return 0;
        Roll(8f, "LEGEND", "Ironbill", "A marlin that tows the boat");
        Inp.ScriptStick = new Vector2(1, 0.15f);
        foreach (var _ in Frames(30)) yield return 0;
        Inp.Tap(KeyboardKey.F);
        foreach (var _ in Frames(40)) yield return 0;
        trollT = 0.01f;
        for (int i = 0; i < 60 && mode != "bite"; i++) yield return 0;
        Inp.ScriptStick = null;
        if (fish != null) { var f = Data.FishById[TowFish]; fish.Roll = new Catchable { Id = TowFish, Name = f.Name, Difficulty = f.Difficulty, Rare = f.Rare }; }
        Inp.Tap(KeyboardKey.E);
        foreach (var _ in Frames(2)) yield return 0;
        foreach (var _ in Reel(900, 0, () =>
        {
            BossBot();
        })) yield return 0;
    }

    // Platejaw: knocking on the carved stone by the Ancient pool, and the armoured fish that answers.
    IEnumerable<int> ShotPlatejaw()
    {
        TrailerGame();
        state.commons["ancient_coelacanth"] = 1; state.caveDeepest = AncientFloor; state.inv["iron_sword"] = 1;
        state.hinted["platejawMet"] = true; state.hinted["runestone"] = true; state.hinted["platejawArmour"] = true;
        caveFloor = AncientFloor; LoadScene("cave");
        foreach (var _ in Frames(4)) yield return 0;
        float sx = RuneStoneX, sy = RuneStoneY + 10;
        (player.X, player.Y) = WalkStart(sx, sy, 50, prefY: 1);
        foreach (var _ in Frames(6)) yield return 0;
        Roll(8f, "BOSS", "Platejaw", "The armoured guardian of the Ancient pool");
        foreach (var _ in WalkTo(sx, sy, 90)) yield return 0;
        player.Face = "up";
        foreach (var _ in Frames(3)) yield return 0;
        Inp.Tap(KeyboardKey.E);
        for (int i = 0; i < 900; i++)
        {
            state.hp = Math.Max(state.hp, 60);
            if (guardian != null)
            {
                var g = guardian;
                bool open = g.Phase is "beached" or "stunned";
                DuelStep(g.X, g.Y, g.Phase is "aim" or "lunge" or "snapRear" or "snap" or "surgeRear" or "surge", open && target?.Type == "guardian", 52, open, RuneStoneX, RuneStoneY - 10, 70);
            }
            yield return 0;
        }
    }
}
#endif
