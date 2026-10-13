#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The Reef Run (1.30). FESH_REEF_TEST=1 runs only this: the plan and the throws without a screen, then the run against
    // an angler in the game, thrown with real clicks and keys.
    IEnumerable<int> ReefScript()
    {
        Note("Reef Run: the plan");
        var plan = ReefRules.Plan(4321);
        Check("a seed gives the same twenty-four fish every time", plan.Count == ReefRules.Fish && plan.SequenceEqual(ReefRules.Plan(4321)) && !plan.SequenceEqual(ReefRules.Plan(4322)));
        Check("every pass is inside the forty-five seconds, with a sensible lane, speed and size", plan.All(p => p.T0 >= 0.3f && p.T0 + p.Dur <= ReefRules.Duration + 0.5f && p.Y >= 70 && p.Y <= 370 && p.Dur >= 5 && p.Dur <= 10.5f && p.Size is >= 0 and <= 2));
        float vis = 0; int maxVis = 0;
        for (float t = 0; t < ReefRules.Duration; t += 0.25f) { int n = plan.Count(p => ReefRules.PosAt(p, t) != null); vis += n; maxVis = Math.Max(maxVis, n); }
        vis /= ReefRules.Duration / 0.25f;
        Check($"a few fish are in view at any time, never a crowd ({vis:0.0} on average, {maxVis} at most)", vis > 2 && vis < 6 && maxVis <= 8);
        Check("a fish is in the water only between its entry and its exit, and moves across the whole reef", plan.All(p => ReefRules.PosAt(p, p.T0 - 0.1f) == null && ReefRules.PosAt(p, p.T0 + p.Dur + 0.1f) == null && ReefRules.PosAt(p, p.T0 + 0.01f) is { } a && ReefRules.PosAt(p, p.T0 + p.Dur - 0.01f) is { } b && MathF.Abs(a.x - b.x) > 800));
        Check("all three sizes turn up over many seeds, small most often", Enumerable.Range(0, 30).SelectMany(s => ReefRules.Plan(s)).GroupBy(p => p.Size).OrderBy(g => g.Key).Select(g => g.Count()).Let(c => c.Count() == 3 && c.ElementAt(0) > c.ElementAt(1) && c.ElementAt(1) > c.ElementAt(2)));
        var fish0 = plan[5]; float tt = fish0.T0 + fish0.Dur / 2;
        var at = ReefRules.PosAt(fish0, tt + ReefRules.FlightSecs).Value;
        var none = new HashSet<int>();
        Check("a spear thrown where the fish will be when it lands hits it", ReefRules.Hit(plan, none, at.x, at.y, tt) == fish0.Id);
        var now = ReefRules.PosAt(fish0, tt).Value;
        Check("a spear thrown where the fish is now misses it, because the fish swims on", MathF.Abs(now.x - at.x) > ReefRules.Radius[fish0.Size] + 3 ? ReefRules.Hit(plan, none, now.x, now.y, tt) != fish0.Id : true);
        Check("a spear thrown in empty water hits nothing", ReefRules.Hit(plan, none, 5, 5, tt) == -1);
        Check("a fish that was speared cannot be speared again", ReefRules.Hit(plan, new HashSet<int> { fish0.Id }, at.x, at.y, tt) != fish0.Id);
        Check("a bigger fish is worth more", ReefRules.Points[0] < ReefRules.Points[1] && ReefRules.Points[1] < ReefRules.Points[2]);
        var scores = new Dictionary<string, float>();
        foreach (var rv in new[] { "bess", "marlo", "ivy" }) scores[rv] = (float)Enumerable.Range(0, 60).Average(s => ReefRules.AnglerScoreAt(s, rv, 1000));
        Check($"the anglers get better, bess to ivy ({scores["bess"]:0.0}, {scores["marlo"]:0.0}, {scores["ivy"]:0.0} points)", scores["bess"] < scores["marlo"] && scores["marlo"] < scores["ivy"]);
        var ang = ReefRules.Angler(77, "marlo");
        Check("an angler's score only goes up as the run goes on, and every fish they take was in the water", ang.Zip(ang.Skip(1)).All(z => z.First.t <= z.Second.t) && ang.All(a => ReefRules.PosAt(plan.Count == 0 ? null : ReefRules.Plan(77)[a.id], a.t) != null)
            && ReefRules.AnglerScoreAt(77, "marlo", 10) <= ReefRules.AnglerScoreAt(77, "marlo", 30) && ReefRules.AnglerScoreAt(77, "marlo", 1000) == ang.Sum(a => a.pts));
        Check("more points wins, then fewer throws, else a draw", ReefRules.Decide(10, 30, 9, 5) > 0 && ReefRules.Decide(9, 5, 10, 30) < 0 && ReefRules.Decide(9, 5, 9, 8) > 0 && ReefRules.Decide(9, 8, 9, 8) == 0);

        yield return 1;
        Note("Reef Run: against an angler, in the game");
        File.Delete(SaveFile.BoardPath); Leaderboards.Reset();
        MatchTestGame("Spearer");
        state.coins = 80; state.day = 4;
        yield return 4;
        Save();
        var realState = state; var realRng = rng;
        var diskBytes = File.ReadAllBytes(SaveFile.FilePath);
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Tournament Dock"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Visit alone"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(MatchActive && match.Phase == "dock" && mode == "play"); i++) yield return 1;
        player.X = 296 + 9; player.Y = 193; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("E at an angler offers the Reef Run", dockAnglerMenu == "marlo" && Gfx.Seen.ContainsKey("Reef Run (spearfishing)"));
        ClickButton("Reef Run (spearfishing)"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "reef" && mode == "play" && reef != null); i++) yield return 1;
        Check("it fades to the reef with Marlo as the other side, counting down", InReef && reef.Phase == "count" && reef.RivalName == "Marlo" && match.FromDock && match.Spec.Mode == "spear");
        Check("the adventure is untouched", ReferenceEquals(match.Real, realState) && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        pendingShot = "reef-count"; yield return 2;
        for (int i = 0; i < 300 && reef.Phase != "run"; i++) yield return 1;
        Check("then forty-five seconds", reef.Phase == "run");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("a throw by key goes where the aim is, and counts", reef.Throws == 1 && reef.Flight != null);
        for (int i = 0; i < 12; i++) yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("a second throw inside the cooldown is refused", reef.Throws == 1 || reef.Cool <= 0);
        var seed = reef.Seed; var rplan = reef.Plan;
        // Throw at big fish with the real mouse, aiming where each will be when the spear lands.
        int hits = 0, tries = 0; bool shot = false;
        for (int i = 0; i < 2400 && reef.Phase == "run" && tries < 9; i++)
        {
            yield return 1;
            if (reef.Cool > 0 || reef.Flight != null || reef.T < 1) continue;
            float lat = reef.T + 0.1f + ReefRules.FlightSecs;      // the click lands a frame or two from now
            var cand = rplan.Where(p => !reef.Speared.Contains(p.Id) && ReefRules.PosAt(p, lat) is { } q && q.x > 60 && q.x < 820).OrderByDescending(p => p.Size).FirstOrDefault();
            if (cand == null) continue;
            var pos = ReefRules.PosAt(cand, lat).Value;
            Inp.ScriptMouse = new System.Numerics.Vector2(ReefX + pos.x, ReefY + pos.y);
            Inp.ScriptClickNext = true;
            int before = reef.Points, thrBefore = reef.Throws;
            yield return 2;
            tries++;
            if (!shot && reef.Throws > thrBefore) { shot = true; pendingShot = "reef-throw"; yield return 2; }
            for (int j = 0; j < 40 && reef.Flight != null; j++) yield return 1;
            if (reef.Points > before) hits++;
        }
        Inp.ScriptMouse = Offscreen;
        Check($"clicking where a fish will be speared some ({hits} of {tries} throws, {reef.Points} points, {reef.Speared.Count} fish)", hits >= 1 && reef.Points == reef.Speared.Sum(id => ReefRules.Points[rplan[id].Size]));
        Check("every throw is counted once", reef.Throws >= tries && reef.Misses + reef.Speared.Count == reef.Throws - (reef.Flight != null ? 1 : 0));
        pendingShot = "reef-run"; yield return 2;
        reef.T = ReefRules.Duration - 0.05f;
        for (int i = 0; i < 60 && reef.Phase != "done"; i++) yield return 1;
        Check("at the whistle the run is over and the result is worked out", reef.Phase == "done" && reef.Result is "win" or "loss" or "draw" && reef.Result == (ReefRules.Decide(reef.Points, 0, reef.AnglerPts, 0) > 0 ? "win" : ReefRules.Decide(reef.Points, 0, reef.AnglerPts, 0) < 0 ? "loss" : "draw"));
        pendingShot = "reef-result"; yield return 3;
        Check("it is on the board once, with its points, against Marlo", Leaderboards.Board().entries.Count(e => e.mode == "spear" && e.rival == "Marlo" && e.score == reef.Points) == 1);
        Check("the card offers another run, the dock and leaving", Gfx.Seen.ContainsKey("Again") && Gfx.Seen.ContainsKey("Back to the dock") && Gfx.Seen.ContainsKey("Leave"));
        ClickButton("Again"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Again starts a fresh run on a new seed", reef.Phase == "count" && reef.Seed != seed && reef.Points == 0 && reef.Speared.Count == 0);
        for (int i = 0; i < 300 && reef.Phase != "run"; i++) yield return 1;
        yield return 10;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc asks whether to leave", match.LeaveAsk);
        ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && !(match.Phase == "dock" && mode == "play"); i++) yield return 1;
        Check("leaving mid-run goes back to the dock and counts as a forfeit", match.Phase == "dock" && reef == null && Leaderboards.Board().entries.Any(e => e.mode == "spear" && e.result == "forfeit"));
        Inp.Tap(KeyboardKey.Escape); yield return 3; ClickButton("Leave"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        for (int i = 0; i < 300 && (MatchActive || mode != "play"); i++) yield return 1;
        Check("leaving the dock puts the adventure back as it was", !MatchActive && ReferenceEquals(state, realState) && ReferenceEquals(rng, realRng) && File.ReadAllBytes(SaveFile.FilePath).SequenceEqual(diskBytes));
        // The boards.
        ClickButton("Matches"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Leaderboards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the leaderboards have a page for the new modes", Gfx.Seen.ContainsKey("More boards"));
        ClickButton("More boards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("which shows and then goes back", boardPage == 1 && Gfx.Seen.ContainsKey("First boards"));
        pendingShot = "boards-page2"; yield return 2;
        ClickButton("First boards"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("back to the first page", boardPage == 0);
        ClosePanels();
    }
}

static class EnumerableLet
{
    public static T2 Let<T, T2>(this IEnumerable<T> e, Func<IEnumerable<T>, T2> f) => f(e);
}
#endif
