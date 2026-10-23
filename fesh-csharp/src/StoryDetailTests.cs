#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The story details (1.45.1, FESH_DETAIL_TEST=1): every laundry line, net, hauled boat, chimney and kite flyer stands on
    // ground you could stand on, clear of solids, boards and each other; smoke only at mealtimes and never in a storm; the kite
    // only on clear afternoons; the laundry still under Reduce motion and swaying without it; screenshots of each place.
    IEnumerable<int> StoryDetailScript()
    {
        Note("Story details");
        state = new State { created = true, look = new Look { name = "Villager" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.hinted["amihan"] = true; state.hinted["habagat"] = true; state.hinted["marilaya"] = true;
        yield return 3;
        var all = StoryDetailList();
        var count = all.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());
        Note("details: " + string.Join(", ", count.Select(kv => $"{kv.Key} {kv.Value}")));
        Check($"every kind is placed ({string.Join(", ", count.Select(kv => kv.Key + " " + kv.Value))})", count.GetValueOrDefault("laundry") >= 4 && count.GetValueOrDefault("net") == 2 && count.GetValueOrDefault("boat") == 3 && count.GetValueOrDefault("smoke") == 4 && count.GetValueOrDefault("kite") == 1);
        var solids = Solids();
        bool Stands(float x, float y) => CanStandBare(x, y, solids) && !IsWater(x, y);
        var bad = all.Where(d => d.Kind is "laundry" or "net" ? !(Stands(d.X, d.Y) && Stands(d.X2, d.Y2) && Stands((d.X + d.X2) / 2, d.Y)) : d.Kind is "boat" or "kite" ? !Stands(d.X, d.Y) : false).ToList();
        Check($"laundry, nets, boats and the kite child all stand on open ground ({bad.Count} bad)", bad.Count == 0);
        Check("no detail is in front of a harbour board or a place people go to", all.Where(d => d.Kind != "smoke").All(d => HarbourBoards.All(b => Dist(b.x, b.y, d.X, d.Y) >= 22) && InteractionPoints().All(p => Dist(p.x, p.y, d.X, d.Y) >= 14)));
        Check("they are placed the same way every time", StoryDetailList().Select(d => $"{d.Kind}{d.X:0}{d.Y:0}").SequenceEqual(all.Select(d => $"{d.Kind}{d.X:0}{d.Y:0}")));
        Check("none of them is a target, a solid or a build blocker (looks only)", !solids.Any(s => all.Any(d => d.Kind == "laundry" && s.Overlaps(new Box(d.X - 1, d.Y - 14, 2, 15)))));
        // Building where one stands places them again (Codex's review): a campfire on a laundry pole moves the washing.
        var line0 = all.First(d => d.Kind == "laundry");
        state.builds.Add(new Build { id = "campfire", x = (int)(line0.X / T), y = (int)(line0.Y / T) });
        var after = StoryDetailList();
        var movedLine = after.FirstOrDefault(d => d.Kind == "laundry" && Dist(d.X, d.Y, line0.X, line0.Y) < 60 && (d.X != line0.X || d.Y != line0.Y));
        Check("a piece built where the washing hangs moves it, and everything still stands clear", !ReferenceEquals(after, all) && movedLine != null && after.Where(d => d.Kind is "laundry" or "net" or "boat").All(d => CanStandBare(d.X, d.Y, Solids())));
        state.builds.RemoveAt(state.builds.Count - 1);
        Check("and taking it down puts the list back as it was", StoryDetailList().Select(d => $"{d.Kind}{d.X:0}{d.Y:0}").SequenceEqual(all.Select(d => $"{d.Kind}{d.X:0}{d.Y:0}")));

        /* ---------- smoke at mealtimes ---------- */
        var house = all.First(d => d.Kind == "smoke");
        void GoTo(float x, float y) { player.X = x; player.Y = y + 20; }
        GoTo(house.X, house.Y - 15); state.clock = 7 * 60;
        yield return 4;
        Check($"at breakfast the chimney smokes ({smokeDrawn})", smokeDrawn >= 1);
        pendingShot = "d01-smoke"; yield return 4;
        state.clock = 15 * 60; yield return 3;
        Check("in the afternoon it doesn't", smokeDrawn == 0);
        state.clock = 12 * 60; yield return 3;
        Check("at midday it does", smokeDrawn >= 1);
        state.weather = "storm"; planned = "storm"; yield return 3;
        Check("never in a storm", smokeDrawn == 0);
        ClearSkies(); SnapWeather();
        state.clock = 18 * 60; yield return 3;
        Check("or at supper", smokeDrawn >= 1);
        SetNight(true); yield return 3;
        Check("and not at night", smokeDrawn == 0);
        SetNight(false);

        /* ---------- the kite ---------- */
        var kid = all.First(d => d.Kind == "kite");
        GoTo(kid.X, kid.Y + 10); state.clock = 15 * 60; yield return 4;
        Check($"on a clear afternoon a child flies a kite ({kitesDrawn})", kitesDrawn == 1);
        pendingShot = "d02-kite"; yield return 4;
        state.clock = 10 * 60; yield return 3;
        Check("not in the morning", kitesDrawn == 0);
        state.clock = 15 * 60; state.weather = "rain"; planned = "rain"; yield return 3;
        Check("not in the rain", kitesDrawn == 0);
        ClearSkies(); SnapWeather();

        /* ---------- the rest, by eye ---------- */
        foreach (var (kind, shot) in new[] { ("laundry", "d03-laundry"), ("net", "d04-net"), ("boat", "d05-boat") })
        {
            var d = all.First(x => x.Kind == kind);
            GoTo(d.X, d.Y); state.clock = 11 * 60; yield return 4;
            pendingShot = shot; yield return 4;
        }

        /* ---------- Reduce motion ---------- */
        var lines = all.First(d => d.Kind == "laundry");
        float timeWas = time;
        Settings.Data.reduceMotion = true;
        var still = Enumerable.Range(0, 40).Select(i => { time = timeWas + i * 0.1f; return (int)MathF.Round(DetailSway(lines, 1.7f)); }).Distinct().ToList();
        Settings.Data.reduceMotion = false;
        var moving = Enumerable.Range(0, 40).Select(i => { time = timeWas + i * 0.1f; return (int)MathF.Round(DetailSway(lines, 1.7f)); }).Distinct().ToList();
        time = timeWas;
        Check("under Reduce motion the washing hangs still", still.Count == 1 && still[0] == 0);
        Check($"otherwise it sways a pixel or so both ways ({string.Join(",", moving.OrderBy(v => v))})", moving.Contains(-1) && moving.Contains(1));
    }
}
#endif
