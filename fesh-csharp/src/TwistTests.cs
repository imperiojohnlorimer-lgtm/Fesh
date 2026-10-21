#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Fight twists (1.44, FESH_TWIST_TEST=1): who gets which (by family; none for legends, easy fish or with them off),
    // and real fights for every twist: the warning comes first, the fish never moves faster than the zone can follow,
    // holding it inside earns "Held it!", leaving it doesn't, runs and leaps wait, the roll puts the zone back as it was,
    // teeth double the loss.
    IEnumerable<int> TwistScript()
    {
        Note("Fight twists");
        TwistsOff = false;
        state = new State { created = true, look = new Look { name = "Twister" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        foreach (var t in Twists.Keys) state.hinted["twist:" + t] = true;   // no first-time toasts in the way
        var (lx, ly) = StandNear("lagoon");
        player.X = lx; player.Y = ly; player.Face = "up";
        yield return 3;

        /* ---------- who gets which ---------- */
        var all = Data.AllCommon.Where(f => !f.Legend).ToList();
        var with = all.Where(f => TwistOf(f) != null).ToList();
        Note($"{with.Count} of {all.Count} fish have a twist: " + string.Join(", ", Twists.Keys.Select(k => $"{k} {with.Count(f => TwistOf(f) == k)}")));
        Check($"every twist belongs to some fish, and plenty of fish have one ({with.Count})", Twists.Keys.All(k => with.Any(f => TwistOf(f) == k)) && with.Count >= 25);
        Check("legends and easy everyday fish have none", Data.AllCommon.Where(f => f.Legend || f.Difficulty < 1).All(f => TwistOf(f) == null));
        Check("a bottom-hugger only dives, rolls or bites", with.Where(f => f.Style == "bottom").All(f => TwistOf(f) is "dive" or "roll" or "teeth"));
        Check("a grouper dives, a jack bursts, a barracuda bites, a stingray glides",
            TwistOf(Data.FishById["lapu_lapu"]) is "dive" or null && TwistOf(Data.FishById["talakitok"]) is "burst" or null
            && Data.AllCommon.Where(f => AlbumData.TryGetValue(f.Id, out var i) && i.Family.Contains("Sphyraenidae") && f.Difficulty >= 1).All(f => TwistOf(f) == "teeth"));

        // A real fight with a fish of each twist, the progress held halfway so it neither lands nor gets away.
        bool bot = true; float maxSpeed = 0, prevY = 0, prevT = 0;
        testEachFrame = () =>
        {
            if (reel == null) { Inp.Hold(KeyboardKey.E, false); return; }
            reel.Progress = 0.5f; reel.AttackTimer = 99;
            if (reel.TwistOn > 0 && time > prevT) maxSpeed = MathF.Max(maxSpeed, MathF.Abs(reel.FishY - prevY) / (time - prevT));
            prevY = reel.FishY; prevT = time;
            Inp.Hold(KeyboardKey.E, bot && reel.ZoneY + reel.ZoneH / 2 + reel.ZoneV * 0.18f > reel.FishY);   // a little lookahead for the zone's momentum
        };
        void Fight(CommonFish f)
        {
            fish = new FishCast { Spot = "lagoon", Bx = player.X, By = player.Y - 20, Roll = new Catchable { Id = f.Id, Name = f.Name, Difficulty = f.Difficulty, Rare = f.Rare } };
            StartReel(false);
        }
        IEnumerable<int> Finish() { fish = null; reel = null; mode = "play"; Inp.Hold(KeyboardKey.E, false); yield return 3; }

        TwistsOff = true;
        Fight(with.First());
        Check("with twists off (the old scripts) a fight has none", reel.Twist == null);
        foreach (var s in Finish()) yield return s;
        TwistsOff = false;

        bool warnFirst = true, fair = true, heldAll = true;
        // Each twist twice: the hardest fish with it (for its speed) and the easiest (for holding it).
        foreach (var (id, hardest) in Twists.Keys.SelectMany(k => new[] { (k, true), (k, false) }))
        {
            var pool = with.Where(x => TwistOf(x) == id);
            var f = hardest ? pool.OrderByDescending(x => x.Difficulty).First() : pool.OrderBy(x => x.Difficulty).First();
            Fight(f);
            reel.TwistT = 0.05f; reel.RunT = reel.LeapT = 99; maxSpeed = 0;
            for (int i = 0; i < 40 && reel.TwistWarnT <= 0; i++) yield return 1;
            float warnStart = time;
            for (int i = 0; i < 120 && reel.TwistOn <= 0; i++) yield return 1;
            float warned = time - warnStart;
            if (warned < TwistWarn - 0.1f) { warnFirst = false; Note($"{id}: warned only {warned:0.00} s"); }
            if (id == "roll" && hardest) Check($"a roll shrinks the zone ({reel.ZoneH:0} of {reel.TwistZone:0})", MathF.Abs(reel.ZoneH - reel.TwistZone * 0.7f) < 0.5f);
            if (id == "teeth" && hardest) { yield return 2; Check("teeth double what an escape costs", reel.LossMul == 2); }
            if (id == "dive" && hardest) pendingShot = "t01-dive";
            int held0 = reel.TwistsHeld;
            for (int i = 0; i < 240 && reel.TwistOn > 0; i++) yield return 1;
            yield return 2;
            if (maxSpeed > TwistMaxSpeed + 25) { fair = false; Note($"{id} ({f.Name}, difficulty {f.Difficulty}): {maxSpeed:0} px/s"); }
            if (!hardest && reel.TwistsHeld <= held0) { heldAll = false; Note($"{id} ({f.Name}, difficulty {f.Difficulty}): not held with the bar kept on it ({reel.TwistIn:0.00} of {reel.TwistAll:0.00} s, zone {reel.ZoneH:0})"); }
            if (id == "roll" && hardest) Check($"and puts it back as it was ({reel.ZoneH:0})", MathF.Abs(reel.ZoneH - reel.TwistZone) < 0.01f && reel.ZoneY >= 0 && reel.ZoneY + reel.ZoneH <= Bar);
            if (id == "teeth" && hardest) Check("and then they don't", reel.LossMul == 1);
            foreach (var s in Finish()) yield return s;
        }
        Check("every twist is warned for its full time before it starts", warnFirst);
        Check($"no twist moves the fish faster than the zone can follow", fair);
        Check("keeping the bar on the fish through each twist earns \"Held it!\"", heldAll);

        // Not following a burst: no reward.
        bot = false;
        var burster = with.First(x => TwistOf(x) == "burst");
        Fight(burster); reel.TwistT = 0.05f; reel.RunT = reel.LeapT = 99;
        for (int i = 0; i < 120 && reel.TwistOn <= 0; i++) yield return 1;
        bool burstStarted = reel.TwistOn > 0;
        reel.ZoneY = Bar - reel.ZoneH; reel.ZoneV = 0;   // the zone on the bottom, and nobody holding
        for (int i = 0; i < 240 && reel.TwistOn > 0; i++) yield return 1;
        Check($"letting a burst go by earns nothing (it ran: {burstStarted}, inside {reel.TwistIn:0.00} of {reel.TwistAll:0.00} s)", burstStarted && reel.TwistOn <= 0 && reel.TwistsHeld == 0);
        foreach (var s in Finish()) yield return s;
        bot = true;

        // A runner doesn't run in the middle of a twist.
        var runner = with.FirstOrDefault(x => x.Style == "runner");
        if (runner != null)
        {
            Fight(runner); reel.TwistT = 0.05f; reel.RunT = 99;
            for (int i = 0; i < 60 && reel.TwistOn <= 0; i++) yield return 1;
            reel.RunT = 0; yield return 3;
            Check($"a runner ({runner.Name}) waits to run until the twist is over", reel.TwistOn <= 0 || reel.Running <= 0);
            foreach (var s in Finish()) yield return s;
        }

        // A jumper's leap and a twist due in the same frame: only one of them happens (Codex: the twist started mid-leap).
        var jumper = with.FirstOrDefault(x => x.Style == "jumper");
        if (jumper != null)
        {
            bool both = false;
            for (int k = 0; k < 6 && !both; k++)
            {
                Fight(jumper); reel.RunT = 99; reel.TwistT = 0.0001f; reel.LeapT = 0.0001f;
                for (int i = 0; i < 90; i++) { if (reel.Leap > 0 && (reel.TwistWarnT > 0 || reel.TwistOn > 0)) both = true; yield return 1; }
                foreach (var s in Finish()) yield return s;
            }
            Check($"a leap and a twist due together never overlap ({jumper.Name})", !both);
        }

        // The warning's badge, by pixels in the screenshot (t02).
        var any = with.First(x => TwistOf(x) == "burst");
        Fight(any); reel.TwistT = 0.05f; reel.RunT = reel.LeapT = 99;
        for (int i = 0; i < 40 && reel.TwistWarnT <= 0; i++) yield return 1;
        pendingShot = "t02-warning"; yield return 3;
        foreach (var s in Finish()) yield return s;

        testEachFrame = null;
        TwistsOff = true;
        yield return 2;
    }
}
#endif
