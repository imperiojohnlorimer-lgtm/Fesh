#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The cast skill shot (1.45.1, FESH_CAST_TEST=1): bubbles that wait for a fisher facing a spot (and never for the old
    // scripts, a match, the ice hole, the setting off), the real landing the preview, the gold band and the cast all share,
    // a real hold and release on the band (bite sooner, luck +1, used once, a cooldown) and one off it (an ordinary cast,
    // the bubbles stay), bubbles that wait while you power up, and the band never being most of the meter.
    IEnumerable<int> CastSkillScript()
    {
        Note("Cast skill shot");
        OldCelebrate = false; CastSkillOff = false;
        state = new State { created = true, look = new Look { name = "Skill person" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Settings.Data.castSkill = true; Inp.ScriptMouse = Offscreen;
        yield return 3;
        var lag = Data.SpotById["lagoon"];

        // A shore point south of the lagoon about `dist` px from its middle, facing it.
        void Stand(float dist)
        {
            player.X = lag.X; player.Y = lag.Y + dist; player.Face = "up";
        }
        IEnumerable<int> Face() { Stand(lag.R - 3); yield return 3; }
        void Fresh() { hot = null; hotNext = 0; hotArmAt = -1; }

        /* ---------- who gets bubbles ---------- */
        foreach (var s in Face()) yield return s;
        Check($"at the lagoon E is a fishing spot (type {target?.Type}, mode {mode}, at {player.X:0},{player.Y:0}, water {IsWater(player.X, player.Y)})", target?.Type == "spot" && target.Id == "lagoon");
        if (target?.Type != "spot") yield break;   // nothing below means anything without a spot to fish
        CastSkillOff = true; Fresh();
        for (int i = 0; i < 700 && hot == null; i++) yield return 1;
        Check("the old scripts never see bubbles", hot == null);
        CastSkillOff = false; Settings.Data.castSkill = false; hotArmAt = -1;
        for (int i = 0; i < 700 && hot == null; i++) yield return 1;
        Check("with the setting off there are none", hot == null);
        Settings.Data.castSkill = true; hotArmAt = -1; hotNext = 0;
        int frames = 0;
        for (; frames < 1500 && hot == null; frames++) yield return 1;
        Check($"standing at the lagoon, bubbles stir by themselves ({frames} frames)", hot != null && hot.Spot == "lagoon");
        Check("and the one-time hint was told", state.Hinted("hotSpot"));
        if (hot != null)
        {
            Check($"they are on water ({hot.X:0},{hot.Y:0})", IsWater(hot.X, hot.Y) && !InSanctuary(hot.X, hot.Y));
            var rng = HotPowerRange();
            var (lo, hi) = PowerRangeFor(lag, hot.X, hot.Y) ?? (-1, -1);
            Check($"a gold band exists and is part of the meter ({lo:0.00}-{hi:0.00})", lo >= 0 && hi - lo <= HotMaxBand && hi - lo >= HotMinBand);
            Check("no band while not powering up", rng == null);
        }

        // Where the cast falls is one answer for the preview, the band and the cast.
        bool same = true;
        for (int i = 0; i <= 20; i++)
        {
            float p = i / 20f;
            var (fx, fy, ok) = FinalLanding(lag, p);
            Cast("lagoon", p);
            same &= ok && Dist(fx, fy, fish.Tx, fish.Ty) < 0.01f;
            ReelIn("x"); Fresh(); hot = null;
        }
        Check("the preview's landing is the cast's landing at every power", same);
        toastTimer = 0;

        /* ---------- a bubbles patch we place ourselves ---------- */
        Fresh(); Stand(lag.R - 3); yield return 2;
        bool placed = false; float usedPower = 0;
        foreach (float p in new[] { 0.5f, 0.6f, 0.4f, 0.7f, 0.55f, 0.65f })
            if (PlaceHot(lag, p, 0)) { placed = true; usedPower = p; break; }
        Check($"bubbles can be placed ({usedPower})", placed);
        if (!placed) yield break;
        var (blo, bhi) = PowerRangeFor(lag, hot.X, hot.Y).Value;
        pendingShot = "c01-bubbles"; yield return 4;

        // The band at the edge of the radius: a landing 6.4 px away counts, 6.6 px does not.
        var (cx, cy, _) = FinalLanding(lag, usedPower);
        Check("a landing right on the bubbles counts", HotHit("lagoon", hot.X, hot.Y));
        Check("6.4 px off still counts", HotHit("lagoon", hot.X + 6.4f, hot.Y));
        Check("6.6 px off does not", !HotHit("lagoon", hot.X + 6.6f, hot.Y));
        Check("another spot's cast never counts", !HotHit("rocks", hot.X, hot.Y));

        /* ---------- hold and release on the band ---------- */
        float bornWas = hot.Born;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 20 && mode != "charging"; i++) yield return 1;
        Check($"holding E powers up ({mode})", mode == "charging");
        float t0 = time; int guard = 0;
        // Wait for the bar to be well inside the band, and look at the meter.
        while (!(charge > blo + (bhi - blo) * 0.3f && charge < bhi - (bhi - blo) * 0.3f) && guard++ < 400) yield return 1;
        Check($"the band shows on the meter ({HotPowerRange()?.lo:0.00}-{HotPowerRange()?.hi:0.00})", HotPowerRange() is not null);
        Check("and the prompt says to let go", prompt.Text != null && prompt.Text.Contains("Now!"));
        pendingShot = "c02-charging"; yield return 4;
        float chargeAt = charge;
        Inp.Hold(KeyboardKey.E, false);
        yield return 3;
        Check($"letting go on the band casts into the bubbles (charge {chargeAt:0.00})", fish != null && fish.Hot);
        Check("the bubbles are used up", hot == null && hotNext > time + 20);
        int luckOn = Luck("lagoon", null); fish.Hot = false; int luckOff = Luck("lagoon", null); fish.Hot = true;
        Check($"+1 luck with it ({luckOff} -> {luckOn})", luckOn == luckOff + 1);
        for (int i = 0; i < 80 && mode == "casting"; i++) yield return 1;
        Check("the bobber lands and a floater says so", mode == "waiting" && floaters.Any(f => f.Text.Contains("bubbles")));
        pendingShot = "c03-landed"; yield return 4;
        ReelIn("x");

        /* ---------- a miss is an ordinary cast ---------- */
        Fresh(); toastTimer = 0; Stand(lag.R - 3); yield return 2;
        placed = false;
        foreach (float p in new[] { 0.5f, 0.6f, 0.4f, 0.7f, 0.55f, 0.65f }) if (PlaceHot(lag, p, 0)) { placed = true; break; }
        Check("bubbles again", placed);
        (blo, bhi) = PowerRangeFor(lag, hot.X, hot.Y).Value;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 20 && mode != "charging"; i++) yield return 1;
        guard = 0;
        while (!(charge < blo - 0.1f || charge > bhi + 0.1f) && guard++ < 400) yield return 1;
        Check("the prompt asks for the gold mark", prompt.Text != null && prompt.Text.Contains("gold mark"));
        Inp.Hold(KeyboardKey.E, false);
        yield return 3;
        Check($"letting go off the band is an ordinary cast", fish != null && !fish.Hot);
        Check("and the bubbles stay for another try", hot != null);
        ReelIn("x");

        /* ---------- waiting while you power up ---------- */
        hot.Born = time - (HotLife - 1.5f);
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 20 && mode != "charging"; i++) yield return 1;
        t0 = time; while (time - t0 < 3f) yield return 1;
        Check("they don't run out while you aim", hot != null && mode == "charging");
        Inp.Hold(KeyboardKey.E, false); yield return 3;
        ReelIn("x");
        Fresh(); Stand(lag.R - 3); yield return 2;
        foreach (float p in new[] { 0.5f, 0.6f, 0.4f, 0.7f }) if (PlaceHot(lag, p, 0)) break;
        hot.Born = time - (HotLife - 0.3f);
        t0 = time; while (time - t0 < 1.5f) yield return 1;
        Check("but they fade when you don't", hot == null);

        /* ---------- where they never come ---------- */
        Fresh(); hotArmAt = -1;
        var ice = Data.SpotById["icehole"]; var well = Data.SpotById["starwell"];
        Check("never at the ice hole or the Starwell", !HotEligible(ice) && !HotEligible(well));
        Check("or in the sea or a cave", !HotEligible(Data.SpotById["opensea"]) && !HotEligible(Data.SpotById["cavepool"]));
        Fresh(); Stand(lag.R - 3); yield return 2;
        // Standing right at the water's edge the whole meter would land in them, so there are none.
        Stand(lag.R * 0.35f); yield return 2;
        bool tooNear = !PlaceHot(lag, 0.6f, 0);
        Check($"a spot at your feet gets no bubbles (band would be most of the meter): {(tooNear ? "none" : "placed")}", tooNear || PowerRangeFor(lag, hot.X, hot.Y) is { } r && r.hi - r.lo <= HotMaxBand);

        /* ---------- looks ---------- */
        Fresh(); Stand(lag.R - 3); yield return 2;
        foreach (float p in new[] { 0.5f, 0.6f, 0.4f, 0.7f }) if (PlaceHot(lag, p, 0)) break;
        Settings.Data.reduceMotion = true; yield return 3;
        pendingShot = "c04-calm"; yield return 4;
        Settings.Data.reduceMotion = false;
        Fresh();
        Check("saved settings keep the choice", Settings.Data.castSkill);
        /* ---------- another game never inherits the bubbles (Codex's review: StartGame) ---------- */
        Fresh(); Stand(lag.R - 3); yield return 2;
        foreach (float p in new[] { 0.5f, 0.6f, 0.4f, 0.7f }) if (PlaceHot(lag, p, 0)) break;
        bool had = hot != null; hotNext = time + 99;
        StartGame(false); yield return 2;
        Check("loading a game clears the bubbles and their cooldown", had && hot == null && hotNext == 0);

        /* ---------- holding to skip ahead in a conversation ---------- */
        Note("Hold to skip ahead");
        Fresh(); mode = "play"; yield return 2;
        bool done = false;
        Talk(new List<Say> { new("Tomas", "First line of three, quite short."), new("Tomas", "Second line of three, as short."), new("Tomas", "Third and last line of three.") }, () => done = true);
        Inp.Hold(KeyboardKey.E, true);   // the press that began it is still down
        float tt = time; while (time - tt < 1.6f) yield return 1;
        Check($"a key still down from before the conversation doesn't skip (line {dlg?.I})", dlg != null && dlg.I == 0);
        Inp.Hold(KeyboardKey.E, false); yield return 3;
        Inp.Hold(KeyboardKey.E, true);
        tt = time; while (time - tt < 4f && dlg != null && dlg.I < 2) yield return 1;
        Check($"holding inside it turns the pages (line {dlg?.I})", dlg != null && dlg.I == 2);
        tt = time; while (time - tt < 1.5f) yield return 1;
        Check("and stops on the last line, still open", dlg != null && dlg.I == 2 && !done && mode == "dialogue");
        Inp.Hold(KeyboardKey.E, false); yield return 3;
        for (int i = 0; i < 6 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; }
        Check("a press then closes it", done && mode == "play");
        CastSkillOff = true;
    }
}
#endif
