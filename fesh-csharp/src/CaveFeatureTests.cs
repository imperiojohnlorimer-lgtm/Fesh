#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // What there is to find in the caverns besides ore and monsters (FESH_CAVE_TEST=1): chests, cracked walls, springs, mushrooms.
    IEnumerable<int> CaveFeatureScript()
    {
        Note("Cave features");
        state = new State { created = true, look = new Look { name = "Delver" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); quietWildlife = true; standStill = true;
        Give("iron_pickaxe"); Give("iron_sword");
        Inp.ScriptMouse = Offscreen;
        int chestsSeen = 0, cracksSeen = 0, springsSeen = 0, shroomsSeen = 0;
        for (int round = 0; round < 8; round++)
        {
            caveFloor = 3 + round % 4; LoadScene("cave"); yield return 2;
            chestsSeen += caveFeatures.Count(f => f.Kind == "chest"); cracksSeen += caveFeatures.Count(f => f.Kind == "crack");
            springsSeen += caveFeatures.Count(f => f.Kind == "spring"); shroomsSeen += caveFeatures.Count(f => f.Kind == "shroom");
            // Everything stands somewhere you can reach.
            var dist = CaveDistances(ropeTile.x, ropeTile.y);
            foreach (var f in caveFeatures.Where(f => f.Kind is "chest" or "spring" or "shroom"))
                if (!dist.ContainsKey((f.X, f.Y))) { Check($"floor {caveFloor}: a {f.Kind} at {f.X},{f.Y} can be reached", false); }
            foreach (var f in caveFeatures.Where(f => f.Kind == "crack"))
                Check($"floor {caveFloor}: a crack's chamber is solid rock until it's opened and hidden from the passages", f.Pocket.All(p => caveMap[p.y, p.x] == 'W') && caveMap[f.Y, f.X] == 'W' && dist.ContainsKey((f.X - 1, f.Y)) | dist.ContainsKey((f.X + 1, f.Y)) | dist.ContainsKey((f.X, f.Y - 1)) | dist.ContainsKey((f.X, f.Y + 1))
                    && f.Pocket.All(p => !dist.ContainsKey((p.x, p.y))));
        }
        Check($"over eight floors there were chests ({chestsSeen}), cracked walls ({cracksSeen}), springs ({springsSeen}) and mushrooms ({shroomsSeen})", chestsSeen >= 13 && cracksSeen >= 3 && springsSeen >= 1 && shroomsSeen >= 40);
        Note($"chests {chestsSeen}, cracks {cracksSeen}, springs {springsSeen}, mushrooms {shroomsSeen}");

        // A chest: walk up, E, loot.
        caveFloor = 5; LoadScene("cave"); yield return 2;
        var chest = caveFeatures.First(f => f.Kind == "chest");
        chest.Ambush = false;
        player.X = chest.X * T + 5; player.Y = chest.Y * T + 14; player.Face = "up"; state.coins = 0; yield return 3;
        pendingShot = "cave-chest"; yield return 2;
        Check($"beside a chest, E offers to open it ({target?.Type})", target?.Type == "cavechest");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"the chest gives coins ({state.coins}) and a toast ({toastMsg})", chest.Done && state.coins > 40 && toastMsg.Contains("In the chest"));
        yield return 2;
        Check("an opened chest can't be opened twice", target?.Type != "cavechest");
        // A guarded chest lets something loose.
        var guarded = caveFeatures.First(f => f.Kind == "chest" && !f.Done);
        guarded.Ambush = true; monsters.Clear(); int before = monsters.Count;
        player.X = guarded.X * T + 5; player.Y = guarded.Y * T + 14; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a guarded chest brings monsters ({monsters.Count - before})", monsters.Count > before);

        // A cracked wall: three blows.
        for (int tries = 0; tries < 12 && !caveFeatures.Any(f => f.Kind == "crack"); tries++) { caveFloor = 4; LoadScene("cave"); yield return 2; }
        monsters.Clear();
        var crack = caveFeatures.FirstOrDefault(f => f.Kind == "crack");
        Check("a floor with a cracked wall turned up", crack != null);
        if (crack != null)
        {
            int cx = crack.X, cy = crack.Y;
            var (sx, sy) = dist1(crack);
            player.X = sx * T + 5; player.Y = sy * T + 7; player.Face = crack.X > sx ? "right" : crack.X < sx ? "left" : crack.Y > sy ? "down" : "up"; yield return 3;
            pendingShot = "cave-crack"; yield return 2;
            Check($"facing the crack, E offers to chip it ({target?.Type})", target?.Type == "cavecrack");
            for (int i = 0; i < 3; i++) { Inp.Tap(KeyboardKey.E); yield return 12; swingT = 0; }
            Check("three blows break the wall and open the chamber", crack.Done && caveMap[cy, cx] == 'F' && crack.Pocket.All(p => caveMap[p.y, p.x] == 'F'));
            Check("with a richer chest at the back", caveFeatures.Any(f => f.Kind == "chest" && f.Rich && !f.Done));
            Check("and it's reachable from the passage", CaveDistances(ropeTile.x, ropeTile.y).ContainsKey((crack.Pocket[0].x, crack.Pocket[0].y)));
            pendingShot = "cave-chamber"; yield return 2;
        }
        // A spring heals once.
        for (int tries = 0; tries < 16 && !caveFeatures.Any(f => f.Kind == "spring"); tries++) { caveFloor = 6; LoadScene("cave"); yield return 2; }
        monsters.Clear();
        var spring = caveFeatures.FirstOrDefault(f => f.Kind == "spring");
        Check("a floor with a spring turned up", spring != null);
        if (spring != null)
        {
            player.X = spring.X * T + 5; player.Y = spring.Y * T + 12; player.Face = "up"; state.hp = 30; yield return 3;
            pendingShot = "cave-spring"; yield return 2;
            Inp.Tap(KeyboardKey.E); yield return 3;
            Check($"drinking from the spring heals you ({state.hp:0})", state.hp >= 89);
            state.hp = 30; yield return 3; Inp.Tap(KeyboardKey.E); yield return 3;
            Check("and it won't heal you again", state.hp < 40);
        }
        // Without a pickaxe the crack only teases.
        Take("iron_pickaxe");
        Check("no pickaxe, no chipping", PickTier == 0);
    }

    (int x, int y) dist1(CaveFeature crack)
    {
        foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            if (caveMap[crack.Y + oy, crack.X + ox] == 'F') return (crack.X + ox, crack.Y + oy);
        return (crack.X, crack.Y);
    }
}
#endif
