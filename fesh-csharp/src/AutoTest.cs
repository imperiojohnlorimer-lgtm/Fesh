#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Debug builds only. Set FESH_AUTOTEST to a folder and the game plays a scripted session,
// saves screenshots and a log of checks there, then quits. Set FESH_SAVE too so a real save is untouched.
partial class Game
{
    string testDir;
    IEnumerator<int> script;
    int waitFrames;
    string pendingShot;
    readonly List<string> testLog = new();

    partial void AutoTestStart()
    {
        testDir = Environment.GetEnvironmentVariable("FESH_AUTOTEST");
        if (string.IsNullOrEmpty(testDir)) return;
        Directory.CreateDirectory(testDir);
        SaveFile.Clear();
        hasSave = false;
        script = Script();
    }

    partial void AutoTestTick()
    {
        if (script == null) return;
        if (waitFrames > 0) { waitFrames--; return; }
        bool more;
        try { more = script.MoveNext(); }
        catch (Exception e) { Check($"script crashed: {e.GetType().Name}: {e.Message} at {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}", false); more = false; }
        if (more) waitFrames = script.Current;
        else
        {
            File.WriteAllLines(Path.Combine(testDir, "autotest.log"), testLog);
            script = null;
            quit = true;
        }
    }

    partial void AutoTestAfterDraw()
    {
        if (pendingShot == null) return;
        Rlgl.DrawRenderBatchActive();
        var img = LoadImageFromScreen();
        ExportImage(img, Path.Combine(testDir, pendingShot + ".png"));
        UnloadImage(img);
        pendingShot = null;
    }

    void Check(string what, bool ok) => testLog.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
    void Note(string what) => testLog.Add($"      {what}");

    static readonly System.Numerics.Vector2 Offscreen = new(-50, -50);

    // The reachable spot to stand on that is closest to a fishing spot.
    (float x, float y) StandNear(string spotId)
    {
        var s = Data.SpotById[spotId];
        var (sx, sy) = SpotPos(s);
        return Reachable().Select(t => (x: t.Item1 * T + 5f, y: t.Item2 * T + 7f))
            .Where(p => CanStand(p.x, p.y) && Dist(p.x, p.y, sx, sy) < s.R - 4)
            .OrderBy(p => Dist(p.x, p.y, sx, sy)).First();
    }

    // Stands on reachable land close by, facing a tile of shallow water.
    bool FaceWater()
    {
        int px = (int)(player.X / T), py = (int)(player.Y / T);
        foreach (var (x, y) in Reachable().OrderBy(t => Math.Abs(t.Item1 - px) + Math.Abs(t.Item2 - py)))
            foreach (var (ox, oy, f) in new[] { (0, -1, "up"), (0, 1, "down"), (-1, 0, "left"), (1, 0, "right") })
            {
                if (!Wadeable(TileAt(x + ox, y + oy)) || BuildAt(x + ox, y + oy) != null) continue;
                float sx = x * T + 5, sy = y * T + 7;
                if (!CanStand(sx, sy)) continue;
                player.X = sx; player.Y = sy; player.Face = f;
                if (FrontTile() == (x + ox, y + oy)) return true;
            }
        return false;
    }

    // A fish on the line, just biting.
    void TestBite(string spot, string id)
    {
        var f = Data.FishById[id];
        fish = new FishCast { Spot = spot, Bx = player.X, By = player.Y - 20, Tx = player.X, Ty = player.Y - 20, Roll = new Catchable { Id = id, Name = f.Name, Difficulty = f.Difficulty, Rare = f.Rare } };
        fish.BiteT = BiteWindow;
        mode = "bite";
    }

    static KeyboardKey ArrowKey(int d) => d switch { 0 => KeyboardKey.Up, 1 => KeyboardKey.Right, 2 => KeyboardKey.Down, _ => KeyboardKey.Left };

    /* ---------- Cave helpers ---------- */
    // Back at the foot of the ladder with no monsters about, and a few seconds of safety.
    void AtLadder()
    {
        monsters.Clear();
        iframes = 30;
        player.X = ropeTile.x * T + 5; player.Y = (ropeTile.y + 1) * T + 8; player.Face = "up";
    }

    Node ClosestNode(string kind)
    {
        var dist = CaveDistances(ropeTile.x, ropeTile.y);
        int Steps(Node n) => new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }.Select(o => dist.GetValueOrDefault((n.X + o.Item1, n.Y + o.Item2), 9999)).Min();
        return nodes.Where(n => n.Kind == kind && !n.Mined).OrderBy(Steps).FirstOrDefault();
    }

    // Stands on reachable ground right beside a tile, facing it.
    bool StandBeside(int tx, int ty)
    {
        var reach = Reachable();
        foreach (var (ox, oy, f) in new[] { (0, 1, "up"), (-1, 0, "right"), (1, 0, "left"), (0, -1, "down") })
        {
            float sx = (tx + ox) * T + 5, sy = (ty + oy) * T + 7;
            if (!reach.Contains((tx + ox, ty + oy)) || !CanStand(sx, sy)) continue;
            player.X = sx; player.Y = sy; player.Face = f;
            return true;
        }
        return false;
    }

    string CaveLayout() => new(Enumerable.Range(0, CaveW * CaveH).Select(i => caveMap[i / CaveW, i % CaveW]).ToArray());

    // Everything a floor needs to be playable, checked from the foot of the ladder.
    List<string> FloorProblems()
    {
        var bad = new List<string>();
        var dist = CaveDistances(ropeTile.x, ropeTile.y);
        var four = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
        bool Beside(int x, int y) => four.Any(o => dist.ContainsKey((x + o.Item1, y + o.Item2)));
        bool ancient = caveFloor == AncientFloor;
        if (!ancient && (holeTile.x < 0 || !Beside(holeTile.x, holeTile.y))) bad.Add("hole can't be reached");
        if (nodes.Any(n => !Beside(n.X, n.Y))) bad.Add("ore can't be reached");
        if (nodes.Any(n => caveFloor < Ore(n.Kind).MinFloor || caveFloor > Ore(n.Kind).MaxFloor)) bad.Add("ore at the wrong depth");
        if (nodes.Count < 10) bad.Add($"only {nodes.Count} ore");
        if (monsters.Count < 3) bad.Add($"only {monsters.Count} monsters");
        if (!ancient && monsters.Any(m => caveFloor < MonsterKinds[m.Kind].MinFloor)) bad.Add("monster too shallow");
        if (dist.Count < 350) bad.Add($"only {dist.Count} open tiles");
        if (!CanStand(ropeTile.x * T + 5, (ropeTile.y + 1) * T + 8)) bad.Add("can't stand at the ladder");
        foreach (var s in Data.Spots.Where(SpotHere))
        {
            var (sx, sy) = SpotPos(s);
            if (!dist.Keys.Any(k => CanStand(k.Item1 * T + 5, k.Item2 * T + 7) && Dist(k.Item1 * T + 5, k.Item2 * T + 7, sx, sy) < s.R - 4)) bad.Add($"{s.Id} can't be fished");
        }
        return bad;
    }

    IEnumerator<int> Script()
    {
        Inp.ScriptMouse = Offscreen; // keeps the real mouse from steering the build ghost
        foreach (var k in new[] { FontKind.Ui500, FontKind.Ui600, FontKind.Ui700, FontKind.Note })
        {
            var narrow = new List<string>();
            for (int s = 10; s <= 40; s++)
            {
                float sp = Gfx.Measure("a b", k, s / Gfx.Z) - Gfx.Measure("ab", k, s / Gfx.Z);
                if (sp * Gfx.Z < s * 0.15f) narrow.Add($"{s}px");
            }
            Check($"{k} spaces are visible at 10-40px{(narrow.Count > 0 ? " (too narrow at " + string.Join(" ", narrow) + ")" : "")}", narrow.Count == 0);
        }
        yield return 20;
        pendingShot = "01-title"; yield return 2;

        // Character creation
        Inp.Tap(KeyboardKey.Enter); yield return 5;
        Check("New game opens character creation", mode == "create");
        foreach (char c in "Robin") Inp.ScriptChars.Enqueue(c);
        yield return 3;
        editLook.hat = 2; editLook.shirt = 1; editLook.hairColor = 4; editLook.hair = 1;
        yield return 20;
        pendingShot = "02-creator"; yield return 2;
        Inp.Tap(KeyboardKey.Enter); yield return 70;
        Check($"the new fisher starts with the intro (name '{state.look.name}')", mode == "dialogue" && state.look.name == "Robin" && state.created);
        pendingShot = "02-intro"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check("dialogue ends in play mode", mode == "play");
        Check("you start with the old rod", BestRod() == "rod_old");
        // Crickets and worm mounds would wander into the checks below; they get their own test later.
        quietWildlife = true;
        state.loose.RemoveAll(l => l.kind == "worm");

        // Picking up a stone right under the player
        state.loose.Add(new Loose { kind = "stone", tx = 16, ty = 11, x = 161, y = 113 });
        yield return 5;
        Check("walking over a stone puts it in the bag", Has("stone") == 1);

        // Build mode with materials
        state.inv["wood"] = 30; state.inv["stone"] = 30;
        Inp.Tap(KeyboardKey.B); yield return 5;
        Check("B enters build mode", mode == "build");
        Inp.Tap(KeyboardKey.Two); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("fence placed below the player", state.builds.Any(b => b.id == "fence" && b.x == 16 && b.y == 12));
        player.Face = "left"; Inp.Tap(KeyboardKey.Three); yield return 3; Inp.Tap(KeyboardKey.E); yield return 3;
        player.Face = "right"; Inp.Tap(KeyboardKey.Five); yield return 3; Inp.Tap(KeyboardKey.E); yield return 3;
        Note($"builds: {string.Join(", ", state.builds.Select(b => $"{b.id}@{b.x},{b.y}"))}  bag {Has("wood")}w {Has("stone")}s");
        Check("lantern and campfire placed", state.builds.Any(b => b.id == "lantern") && state.builds.Any(b => b.id == "campfire"));

        // Shack in open ground, then mouse hover ghost
        player.X = 140; player.Y = 135; player.Face = "up";
        Inp.Tap(KeyboardKey.Six); yield return 3; Inp.Tap(KeyboardKey.E); yield return 3;
        Check("shack placed", state.builds.Any(b => b.id == "shack"));
        Inp.Tap(KeyboardKey.Two); yield return 2;
        Inp.ScriptMouse = new System.Numerics.Vector2((118 - camX) * 4, (125 - camY) * 4); yield return 10;
        pendingShot = "03-build"; yield return 2;
        Inp.ScriptMouse = Offscreen; yield return 2;

        // Take down the shack and get a refund
        int woodBefore = Has("wood");
        hover = null; player.Face = "up";
        Inp.Tap(KeyboardKey.X); yield return 3; Inp.Tap(KeyboardKey.E); yield return 3;
        Check("take down refunds the shack", !state.builds.Any(b => b.id == "shack") && Has("wood") == woodBefore + 8);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc leaves build mode", mode == "play");

        // Fence collision: walk down into the fence below the start spot
        player.X = 165; player.Y = 115;
        Inp.Hold(KeyboardKey.Down, true); yield return 50; Inp.Hold(KeyboardKey.Down, false); yield return 2;
        Check($"fence blocks walking (stopped at y={player.Y:0.0})", player.Y <= 122.1f);

        // Night lighting
        state.night = true; yield return 10;
        pendingShot = "04-night"; yield return 2;

        // Resting at the built campfire
        var fire = state.builds.First(b => b.id == "campfire");
        player.X = fire.x * T + 5; player.Y = fire.y * T + 14; yield return 3;
        Check($"campfire offers rest and cooking (prompt: {prompt.Text})", target?.Type == "rest" && target.AltType == "cook");

        // Talking to Tomas shows the speaker tag
        state.night = false;
        player.X = 146; player.Y = 96; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 120;
        Check("talking to Tomas opens dialogue", mode == "dialogue");
        pendingShot = "05-tomas"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        yield return 10;

        // Fishing at the lagoon with a bait box next to the player
        player.X = 100; player.Y = 98;
        state.builds.Add(new Build { id = "baitbox", x = 10, y = 10 }); ReindexBuilds();
        yield return 3;
        Check("lagoon is a fishing target", target?.Type == "spot");
        double t0 = GetTime();
        Inp.Tap(KeyboardKey.E); yield return 1;
        while (mode is "casting" or "waiting") yield return 1;
        Check($"bait box bite came fast ({GetTime() - t0:0.00}s)", mode == "bite" && GetTime() - t0 < 2.0);
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("hooking starts the reel minigame", mode == "reeling");
        Inp.Hold(KeyboardKey.Space, true); yield return 25;
        pendingShot = "06-reeling"; yield return 2;
        Inp.Hold(KeyboardKey.Space, false);
        while (mode == "reeling") { Inp.Hold(KeyboardKey.Space, reel != null && reel.FishY < reel.ZoneY + reel.ZoneH / 2); yield return 1; }
        Inp.Hold(KeyboardKey.Space, false);
        Note($"reel result: mode={mode}, toast='{toastMsg}'");
        yield return 5;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        if (mode == "catch") { yield return 40; Inp.Tap(KeyboardKey.E); yield return 3; while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; } }

        // Catch card for the Glowgill
        if (!state.Caught("glowgill")) state.caught.Add("glowgill");
        ShowCatch("glowgill"); yield return 30;
        pendingShot = "07-catch"; yield return 2;
        yield return 20;
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check("closing the catch card starts the clue dialogue", mode == "dialogue");
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }

        // Fesh-dex and case board
        state.caught.Add("tidecrawler");
        Inp.Tap(KeyboardKey.J); yield return 30;
        Check("J opens the Fesh-dex", mode == "panel" && panel == "dex");
        pendingShot = "08-dex"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.C); yield return 10;
        Check("C opens the case board", mode == "panel" && panel == "case");
        pendingShot = "09-case"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Pause menu
        Inp.Tap(KeyboardKey.Escape); yield return 5;
        Check("Esc in play opens the pause menu", mode == "pause");
        pendingShot = "10-pause"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Saving and loading
        Save();
        var loaded = SaveFile.Read();
        Check("save round-trips builds, bag and look", loaded != null && loaded.builds.Count == state.builds.Count
            && loaded.inv.GetValueOrDefault("wood") == Has("wood") && loaded.look.name == "Robin" && loaded.look.hat == 2);

        // The big map: every island and fishing spot can be reached on foot from the start
        bool tide = state.flags.tideOut, dock = state.flags.dockFixed;
        state.flags.tideOut = state.flags.dockFixed = true; BuildMap();
        player.X = 160; player.Y = 115;
        var reach = Reachable();
        for (byte b = 0; b < 4; b++)
        {
            byte bb = b;
            Check($"{Data.Biomes[b].Name} can be walked to", reach.Any(t => biome[t.Item2, t.Item1] == bb && Buildable.Contains(map[t.Item2, t.Item1])));
        }
        Check("Starfall Atoll can't be walked to (boat only)", !reach.Any(t => biome[t.Item2, t.Item1] == 4));
        foreach (var s in Data.Spots.Where(s => s.Scene == "world" && s.Biome != "atoll"))
        {
            bool ok = reach.Any(t => { float cx = t.Item1 * T + 5, cy = t.Item2 * T + 7; return Dist(cx, cy, s.X, s.Y) < s.R - 2 && CanStand(cx, cy); });
            Check($"{s.Label} ({Data.Biomes.First(b => b.Id == s.Biome).Name}) can be fished from land", ok);
        }
        state.flags.tideOut = tide; state.flags.dockFixed = dock; BuildMap();
        Note($"world {COLS}x{ROWS} tiles, {trees.Count} trees, {waterEdges.Count} shoreline edges");
        File.WriteAllLines(Path.Combine(testDir, "map.txt"), Enumerable.Range(0, ROWS).Select(y =>
            $"{y,2} " + new string(Enumerable.Range(0, COLS).Select(x => map[y, x]).ToArray())));

        // Catch odds: the dog at the lagoon, the night-only pike, and each spot keeping its own fish
        state.night = false;
        int dogs = 0;
        for (int i = 0; i < 4000; i++) if (RollCatch("lagoon").Id == "dog") dogs++;
        Check($"a dog bites at the lagoon about 7% of the time ({dogs / 40.0:0.0}%)", dogs > 180 && dogs < 400);
        int pikeDay = Enumerable.Range(0, 2000).Count(_ => RollCatch("icehole").Id == "crystal_pike");
        state.night = true;
        int pikeNight = Enumerable.Range(0, 2000).Count(_ => RollCatch("icehole").Id == "crystal_pike");
        state.night = false;
        Check($"crystal pike only bites at night (day {pikeDay}, night {pikeNight} of 2000)", pikeDay == 0 && pikeNight > 60);
        foreach (var s in Data.Spots.Where(s => s.Biome != "saltmere"))
        {
            var allowed = Data.Common[s.Id].Select(f => f.Id).Concat(Data.Odd.Where(o => o.Spot == s.Id).Select(o => o.Id)).Append("chest").ToHashSet();
            Check($"{s.Label} only gives its own catches", Enumerable.Range(0, 400).All(_ => allowed.Contains(RollCatch(s.Id).Id)));
        }

        // Visit each new island
        foreach (var (shot, spotId, name) in new[] { ("12-frost", "icehole", "Frostfang"), ("13-dunes", "oasis", "Sunscald"), ("14-mire", "swamp", "Mirewood") })
        {
            var (x, y) = StandNear(spotId);
            player.X = x; player.Y = y; player.Face = "down";
            yield return 4;
            Check($"arriving on {name} announces it (toast: {toastMsg})", toastMsg.Contains(name));
            Check($"{name} fishing spot is right there (prompt: {prompt.Text})", target?.Type is "spot" or "drill" && CanStand(x, y));
            yield return 30;
            pendingShot = shot; yield return 2;
        }
        state.night = true; yield return 10;
        pendingShot = "15-mire-night"; yield return 2;
        state.night = false;

        // Hooking a dog at the lagoon
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 3;
        int dogs0 = state.odd.GetValueOrDefault("dog");
        fish = new FishCast { Spot = "lagoon", Bx = 100, By = 66 };
        reel = new ReelState { Roll = new Catchable { Id = "dog", Name = "Dog", Difficulty = 1.6f, Odd = true } };
        mode = "reeling";
        LandCatch();
        Check("landing a dog shows the odd catch card", mode == "odd" && state.odd.GetValueOrDefault("dog") == dogs0 + 1);
        yield return 45;
        pendingShot = "16-dog"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("letting the dog go sends it trotting off", mode == "play" && critters.Count == 1);
        yield return 35;
        pendingShot = "17-dog-runs"; yield return 2;

        // A rare fish in the desert
        fish = new FishCast { Spot = "oasis", Bx = 685, By = 405 };
        reel = new ReelState { Roll = new Catchable { Id = "mirage_koi", Name = "Mirage koi", Difficulty = 2.6f, Rare = true } };
        mode = "reeling";
        LandCatch();
        Check($"rare catches get their own toast ({toastMsg})", toastMsg.StartsWith("Rare catch!") && state.commons.GetValueOrDefault("mirage_koi") == 1);
        yield return 5;

        // Fish log and map
        foreach (var id in new[] { "pond_perch", "arctic_char", "polar_cod", "oasis_tilapia", "sun_mackerel", "mudskipper", "parrotfish" })
            state.commons[id] = state.commons.GetValueOrDefault(id) + 2;
        Inp.Tap(KeyboardKey.J); yield return 3;
        dexTab = "log"; yield return 20;
        pendingShot = "18-fishlog"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        dexTab = "creatures";
        Inp.Tap(KeyboardKey.Tab); yield return 10;
        Check("Tab opens the map", mode == "panel" && panel == "map");
        pendingShot = "19-map"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // ---------- Survival, crafting, houses and the cave ----------
        Check($"the food meter drops while you play ({state.food:0.0})", state.food < 100);

        // Into Tomas's hut, and make an axe at his workbench
        state.inv["wood"] = 40; state.inv["stone"] = 40;
        player.X = 160; player.Y = 74; player.Face = "up"; yield return 3;
        Check($"Tomas's hut door can be used (prompt: {prompt.Text})", target?.Type == "door");
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check("going inside Tomas's hut", scene == "house:tomas" && mode == "play");
        yield return 20;
        pendingShot = "20-hut"; yield return 2;
        player.X = 50; player.Y = 37; player.Face = "up"; yield return 3;
        Check($"Tomas's workbench can be used (prompt: {prompt.Text})", target?.Type == "craft" && target.Id == "workbench");
        Inp.Tap(KeyboardKey.E); yield return 10;
        Check("the workbench opens crafting", mode == "panel" && panel == "craft" && craftStation == "workbench");
        pendingShot = "21-craft"; yield return 2;
        Craft(Items.Recipes.First(r => r.Out == "axe"));
        Craft(Items.Recipes.First(r => r.Out == "pickaxe"));
        Check("crafted an axe and a pickaxe", Has("axe") == 1 && Has("pickaxe") == 1);
        craftTab = "Combat"; yield return 5;
        pendingShot = "21b-craft-combat"; yield return 2;
        craftTab = "Tools";
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        // The map from indoors shows the islands, not the room (it used to copy the room's tiles)
        mapTexDirty = true;
        Inp.Tap(KeyboardKey.Tab); yield return 10;
        Check("the map opens indoors and leaves the room as it was", mode == "panel" && panel == "map" && scene == "house:tomas" && map != worldMap);
        pendingShot = "21c-map-indoors"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check("can't furnish Tomas's place", mode == "play");
        player.X = RoomDoorWX; player.Y = RoomDoorWY - 6; player.Face = "down"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check($"back outside at the hut door ({player.X:0},{player.Y:0})", scene == "world" && Dist(player.X, player.Y, 160, 75) < 4);

        // Chop an oak, pick berries, break a boulder
        int wood0 = Has("wood");
        player.X = 135; player.Y = 117; player.Face = "left"; yield return 3;
        Check($"facing an oak offers chopping (prompt: {prompt.Text})", target?.Type == "tree");
        for (int i = 0; i < 3; i++) { Inp.Tap(KeyboardKey.E); yield return 6; }
        Check("three chops fell the oak for 3 wood", Has("wood") == wood0 + 3 && state.felled.ContainsKey("12,11") && worldMap[11, 12] == 'g');
        player.X = 95; player.Y = 117; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"picking a berry bush (berries {Has("berries")})", Has("berries") >= 2);
        var boulder = trees.First(t => t.kind == 'R');
        foreach (var (ox, oy, f) in new[] { (0, 1, "up"), (-1, 0, "right"), (1, 0, "left"), (0, -1, "down") })
        {
            float sx = (boulder.x + ox) * T + 5, sy = (boulder.y + oy) * T + 7;
            if (!CanStand(sx, sy)) continue;
            player.X = sx; player.Y = sy; player.Face = f;
            break;
        }
        yield return 3;
        int stone0 = Has("stone");
        for (int i = 0; i < 4; i++) { Inp.Tap(KeyboardKey.E); yield return 6; }
        Check($"a pickaxe breaks a boulder for stone ({Has("stone") - stone0})", Has("stone") >= stone0 + 3 && !trees.Contains(boulder));

        // Pet a sheep twice: wool once a day
        var sheep = animals.First(a => a.Kind == "sheep");
        sheep.Pause = 30;
        player.X = sheep.X + 9; player.Y = sheep.Y; player.Face = "left";
        if (!CanStand(player.X, player.Y)) { player.X = sheep.X - 9; player.Face = "right"; }
        yield return 3;
        Check($"a sheep can be petted (prompt: {prompt.Text})", target?.Type == "animal");
        Inp.Tap(KeyboardKey.E); yield return 10;
        pendingShot = "22-sheep"; yield return 2;
        int wool = Has("wool");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"the sheep gives wool once a day (wool {wool} then {Has("wool")})", wool == 1 && Has("wool") == 1);
        Check($"animals wander ({animals.Count(a => Dist(a.X, a.Y, a.HomeX, a.HomeY) > 1)} of {animals.Count} have moved)", animals.Count(a => Dist(a.X, a.Y, a.HomeX, a.HomeY) > 1) >= 3);

        // Build a shack, go in and furnish it
        state.inv["wool"] = 4; state.inv["copper_ore"] = 4;
        state.builds.Add(new Build { id = "shack", x = 14, y = 12 }); ReindexBuilds();
        player.X = 150; player.Y = 132; player.Face = "up"; yield return 3;
        Check($"the shack door can be used (prompt: {prompt.Text})", target?.Type == "door");
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check("inside your own shack", scene == "house:14,12");
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check("build mode offers furniture inside", mode == "build" && BuildTools().Contains("furnace"));
        foreach (var (key, x, y) in new[] { (KeyboardKey.One, 45f, 50f), (KeyboardKey.Two, 105f, 50f), (KeyboardKey.Three, 25f, 50f), (KeyboardKey.Four, 125f, 62f), (KeyboardKey.Six, 45f, 72f) })
        {
            player.X = x; player.Y = y; player.Face = "up"; yield return 2;
            Inp.Tap(key); yield return 2; Inp.Tap(KeyboardKey.E); yield return 3;
        }
        var room = state.rooms.GetValueOrDefault("house:14,12") ?? new();
        Note($"furniture: {string.Join(", ", room.Select(b => $"{b.id}@{b.x},{b.y}"))}");
        Check("placed a workbench, furnace, stove, bed and rug", new[] { "workbench", "furnace", "stove", "bed", "rug" }.All(id => room.Any(b => b.id == id)));
        Inp.Tap(KeyboardKey.B); yield return 3;
        player.X = 85; player.Y = 75; player.Face = "up"; yield return 10;
        pendingShot = "23-shack"; yield return 2;
        var furnace = room.First(b => b.id == "furnace");
        player.X = furnace.x * T + 5; player.Y = furnace.y * T + 16; player.Face = "up"; yield return 3;
        Check($"the furnace can be used (prompt: {prompt.Text})", target?.Type == "craft" && target.Id == "furnace");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Craft(Items.Recipes.First(r => r.Out == "copper_bar"));
        Craft(Items.Recipes.First(r => r.Out == "copper_bar"));
        Check("smelted two copper bars", Has("copper_bar") == 2);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        state.inv["copper_bar"] = 3;
        Craft(Items.Recipes.First(r => r.Out == "rod_copper"));
        Check("crafted a copper rod and it's used for fishing", BestRod() == "rod_copper" && Rod.Zone == 5);
        state.night = true; yield return 5;
        pendingShot = "24-shack-night"; yield return 2;
        state.night = false;
        player.X = RoomDoorWX; player.Y = RoomDoorWY - 6; player.Face = "down"; yield return 2;
        Inp.Hold(KeyboardKey.Down, true); yield return 8; Inp.Hold(KeyboardKey.Down, false); yield return 70;
        Check("walking down through the door goes back outside", scene == "world");

        // Taking the shack down packs up the furniture too
        int bedWool = Has("wool");
        state.builds.RemoveAll(b => b.id == "shack");
        state.builds.Add(new Build { id = "shack", x = 14, y = 12 }); ReindexBuilds();
        player.X = 140; player.Y = 135; player.Face = "up";
        Inp.Tap(KeyboardKey.B); yield return 3; Inp.Tap(KeyboardKey.X); yield return 2; Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"taking down the shack refunds its furniture (wool {bedWool} -> {Has("wool")})", !state.rooms.ContainsKey("house:14,12") && Has("wool") == bedWool + 4);
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // ---------- Frostfang Caverns: random floors, ore tiers, monsters, health and the Ancient Floor ----------
        player.X = MouthDoorX; player.Y = MouthDoorY + 1; player.Face = "up"; yield return 3;
        Check($"the cave mouth can be entered (prompt: {prompt.Text})", target?.Type == "cave");
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check($"inside floor 1 of Frostfang Caverns ({map.GetLength(1)}x{map.GetLength(0)} tiles)", scene == "cave" && caveFloor == 1 && map.GetLength(1) == 60 && map.GetLength(0) == 40);
        Check($"you arrive at the foot of the ladder (prompt: {prompt.Text})", target?.Type == "exit");
        yield return 10;
        pendingShot = "25-cave"; yield return 2;

        // Every floor is freshly generated, and every one can be crossed
        var layouts = new HashSet<string>();
        var problems = new List<string>();
        var stats = new List<string>();
        for (int f = 1; f < AncientFloor; f++)
            for (int k = 0; k < 3; k++)
            {
                caveFloor = f; LoadScene("cave");
                layouts.Add(CaveLayout());
                problems.AddRange(FloorProblems().Select(p => $"floor {f}: {p}"));
                if (k == 0) stats.Add($"F{f}: {CaveDistances(ropeTile.x, ropeTile.y).Count} open, {string.Join("/", nodes.GroupBy(n => n.Kind).Select(g => $"{g.Count()} {g.Key}"))}, "
                    + $"{string.Join("/", monsters.GroupBy(m => m.Kind).Select(g => $"{g.Count()} {g.Key}"))}{(poolPos != null ? ", pool" : "")}");
            }
        foreach (var s in stats) Note(s);
        Check($"33 random floors all look different ({layouts.Count} layouts)", layouts.Count == 33);
        Check($"every random floor can be crossed: hole, ore and pool all reachable, ore and monsters right for the depth{(problems.Count > 0 ? " (" + string.Join("; ", problems.Take(8)) + ")" : "")}", problems.Count == 0);
        caveFloor = AncientFloor; LoadScene("cave");
        string ancient1 = CaveLayout(), ancientProblems = string.Join(", ", FloorProblems());
        LoadScene("cave");
        Check($"the Ancient Floor is always the same, with no way further down ({ancientProblems})", ancient1 == CaveLayout() && holeTile.x < 0 && ancientProblems == "");
        Check($"the Ancient Floor has abyssite, crystal, shades and crabs", nodes.Count(n => n.Kind == "abyssite") == 6 && nodes.Any(n => n.Kind == "crystal")
            && monsters.Count(m => m.Kind == "shade") == 3 && monsters.Count(m => m.Kind == "crab") == 2);

        // Floor 1: copper comes out with the stone pickaxe
        caveFloor = 1; LoadScene("cave"); AtLadder();
        var copper = ClosestNode("copper");
        Check("standing beside a copper rock", copper != null && StandBeside(copper.X, copper.Y));
        yield return 3;
        Check($"copper can be mined (prompt: {prompt.Text})", target?.Type == "node");
        int ore0 = Has("copper_ore");
        for (int i = 0; i < 3; i++) { Inp.Tap(KeyboardKey.E); yield return 6; }
        Check($"a stone pickaxe mines copper ({ore0} -> {Has("copper_ore")})", Has("copper_ore") == ore0 + 2 && copper.Mined);

        // Floor 4: iron is too hard for a stone pickaxe, fine for a copper one
        caveFloor = 4; LoadScene("cave"); AtLadder();
        var iron = ClosestNode("iron");
        Check("standing beside an iron rock", iron != null && StandBeside(iron.X, iron.Y));
        yield return 3;
        int iron0 = Has("iron_ore");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a stone pickaxe can't mine iron (toast: {toastMsg})", toastMsg.StartsWith("Too hard") && toastMsg.Contains("copper pickaxe") && Has("iron_ore") == iron0 && !iron.Mined);
        Give("copper_pickaxe");
        for (int i = 0; i < 3; i++) { Inp.Tap(KeyboardKey.E); yield return 6; }
        Check($"a copper pickaxe mines iron ({iron0} -> {Has("iron_ore")})", Has("iron_ore") == iron0 + 2 && iron.Mined);

        // The hole leads down a floor
        AtLadder();
        Check("standing beside the hole", StandBeside(holeTile.x, holeTile.y));
        yield return 3;
        Check($"the hole offers the way down (prompt: {prompt.Text})", target?.Type == "descend" && prompt.Text.Contains("floor 5"));
        Inp.Tap(KeyboardKey.E); yield return 75;
        Check($"climbed down to floor 5 (deepest {state.caveDeepest}, toast: {toastMsg})", caveFloor == 5 && state.caveDeepest == 5 && toastMsg.StartsWith("Floor 5"));

        // A pool on some floors
        for (int i = 0; i < 30 && poolPos == null; i++) LoadScene("cave");
        AtLadder();
        var (cpx, cpy) = StandNear("cavepool");
        player.X = cpx; player.Y = cpy; player.Face = "up"; yield return 3;
        Check($"a floor's pool can be fished (prompt: {prompt.Text})", target?.Type == "spot" && target.Id == "cavepool");

        // Fighting: a slime in front of you can be hit until it's beaten
        AtLadder();
        var open = CaveDistances(ropeTile.x, ropeTile.y).Keys
            .Where(p => p.Item1 > 1 && p.Item1 < CaveW - 3 && p.Item2 > 1 && p.Item2 < CaveH - 2 && Math.Abs(p.Item1 - ropeTile.x) + Math.Abs(p.Item2 - ropeTile.y) > 8
                && Enumerable.Range(-1, 4).All(ox => Enumerable.Range(-1, 3).All(oy => map[p.Item2 + oy, p.Item1 + ox] == 'F' && NodeAt(p.Item1 + ox, p.Item2 + oy) == null)))
            .OrderBy(p => Math.Abs(p.Item1 - ropeTile.x) + Math.Abs(p.Item2 - ropeTile.y)).First();
        player.X = open.Item1 * T + 5; player.Y = open.Item2 * T + 7; player.Face = "right";
        var slime = new Monster { Kind = "slime", X = player.X + 9, Y = player.Y, Hp = MonsterKinds["slime"].Hp, Timer = 2 };
        monsters.Add(slime);
        yield return 2;
        Check($"a monster in front can be attacked (prompt: {prompt.Text})", target?.Type == "monster");
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"hitting it with a pickaxe does 2 damage ({slime.Hp}/5 left)", slime.Hp == 3);
        for (int i = 0; i < 900 && !slime.Dead; i++)
        {
            FaceToward(slime.X, slime.Y);
            if (target?.Type == "monster" && swingT <= 0.1f) Inp.Tap(KeyboardKey.E);
            yield return 1;
        }
        Check("the slime is beaten and disappears", slime.Dead && !monsters.Contains(slime));
        Give("copper_sword");
        Check($"a sword is used as your weapon ({Weapon().name}, {Weapon().damage} damage)", Weapon() == ("copper sword", 3));
        int gel0 = Has("slime_gel");
        for (int i = 0; i < 40; i++) { swingT = 0; Attack(new Monster { Kind = "slime", X = player.X + 8, Y = player.Y, Hp = 1 }); }
        Check($"slimes drop slime gel about 70% of the time ({Has("slime_gel") - gel0} of 40)", Has("slime_gel") - gel0 is >= 18 and <= 38);

        // Getting hurt: contact damage, a flash, a moment of safety, and armour
        state.hp = 100; iframes = 0;
        var crab = new Monster { Kind = "crab", X = player.X + 2, Y = player.Y - 3, Hp = MonsterKinds["crab"].Hp, Timer = 2 };
        monsters.Add(crab);
        monsters.Add(new Monster { Kind = "bat", X = player.X - 30, Y = player.Y - 6, Hp = 3, Timer = 1 });
        monsters.Add(new Monster { Kind = "shade", X = player.X + 34, Y = player.Y + 8, Hp = 16, Timer = 1 });
        yield return 3;
        Check($"a monster touching you hurts ({state.hp:0} health)", state.hp == 88 && iframes > 0);
        pendingShot = "26-cave-fight"; yield return 2;
        yield return 10;
        Check($"you can't be hit again straight away ({state.hp:0} health)", state.hp == 88);
        Give("shell_armor");
        state.hp = 100; HurtPlayer(12, crab);
        Check($"shell armour softens blows ({100 - state.hp:0.0} damage instead of 12)", state.hp > 91.5f && state.hp < 92.5f);
        monsters.Clear(); iframes = 0;

        // Health comes back while fed, and food heals
        state.hp = 50; state.food = 80; lastHitT = time - 10;
        yield return 120;
        Check($"health slowly recovers while you're fed ({state.hp:0.0})", state.hp > 50.6f && state.hp < 52);
        state.hp = 50; state.food = 50; Give("grilled_fish");
        Eat("grilled_fish");
        Check($"eating heals too ({state.hp:0.0} health, toast: {toastMsg})", state.hp >= 62 && state.hp <= 63 && toastMsg.Contains("health"));
        state.food = 90;

        // Down to the Ancient Floor
        caveFloor = AncientFloor - 1; LoadScene("cave"); AtLadder();
        Check("standing beside the last hole", StandBeside(holeTile.x, holeTile.y));
        yield return 3;
        Check($"the last hole hints at what's below (prompt: {prompt.Text})", target?.Type == "descend" && prompt.Text.Contains("ancient"));
        Inp.Tap(KeyboardKey.E); yield return 75;
        Check($"reached the Ancient Floor ({AreaName()}, toast: {toastMsg})", OnAncientFloor && state.caveDeepest == AncientFloor && toastMsg.StartsWith("The Ancient Floor"));
        iframes = 30;
        var (apx, apy) = StandNear("ancientpool");
        player.X = apx; player.Y = apy; player.Face = "up"; yield return 3;
        Check($"the ancient pool can be fished (prompt: {prompt.Text})", target?.Type == "spot" && target.Id == "ancientpool");
        yield return 20;
        pendingShot = "27-ancient-floor"; yield return 2;
        monsters.Clear();

        // Abyssite needs the crystal pickaxe
        AtLadder();
        var aby = ClosestNode("abyssite");
        Check("standing beside abyssite", aby != null && StandBeside(aby.X, aby.Y));
        yield return 3;
        Give("gold_pickaxe");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a gold pickaxe can't mine abyssite (toast: {toastMsg})", toastMsg.Contains("crystal pickaxe") && !aby.Mined);
        Give("crystal_pickaxe");
        for (int i = 0; i < 3; i++) { Inp.Tap(KeyboardKey.E); yield return 6; }
        Check($"a crystal pickaxe mines abyssite ({Has("abyssite")})", Has("abyssite") == 1 && aby.Mined);

        // The Ancient coelacanth gets its own card
        fish = new FishCast { Spot = "ancientpool", Bx = AncientPoolX, By = AncientPoolY };
        reel = new ReelState { Roll = new Catchable { Id = "ancient_coelacanth", Name = "Ancient coelacanth", Difficulty = 4.6f, Rare = true } };
        mode = "reeling";
        LandCatch();
        Check("landing the coelacanth shows the legendary card", mode == "legend" && Has("ancient_coelacanth") == 1);
        yield return 45;
        pendingShot = "28-legend"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"closing the card goes back to play (toast: {toastMsg})", mode == "play" && toastMsg.Contains("aquarium"));

        // The map works underground (it used to copy the cave's tiles)
        mapTexDirty = true;
        Inp.Tap(KeyboardKey.Tab); yield return 10;
        Check("the map opens underground and leaves the cave as it was", mode == "panel" && panel == "map" && scene == "cave" && map == caveMap && basePix == caveBase);
        pendingShot = "29-map-from-cave"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Running out of health: wake up outside, a little poorer
        state.coins = 100; state.hp = 5; iframes = 0;
        monsters.Add(new Monster { Kind = "shade", X = player.X, Y = player.Y - 3, Hp = 16, Timer = 2 });
        yield return 90;
        Check($"fainting wakes you at the cave mouth ({state.hp:0} health, {state.coins} coins, toast: {toastMsg})",
            scene == "world" && Dist(player.X, player.Y, MouthDoorX, MouthDoorY + 2) < 4 && state.hp == 35 && state.coins == 90 && toastMsg.Contains("dark"));
        Check("monsters stay in the cave", monsters.Count == 0);

        // The lift at the entrance, now that you've been deep
        player.X = MouthDoorX; player.Y = MouthDoorY + 1; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check("the cave mouth offers the lift once you've been deep", mode == "panel" && panel == "lift");
        yield return 10;
        pendingShot = "30-lift"; yield return 2;
        ClosePanels(); EnterCaveFloor(10); yield return 75;
        Check($"the lift goes straight to floor 10 ({AreaName()})", scene == "cave" && caveFloor == 10);
        monsters.Clear(); AtLadder(); yield return 3;
        Check($"the ladder leads back up (prompt: {prompt.Text})", target?.Type == "exit");
        Inp.Tap(KeyboardKey.E); yield return 75;
        Check("climbing out of the cave", scene == "world" && Dist(player.X, player.Y, MouthDoorX, MouthDoorY + 2) < 4);
        iframes = 0;   // the cave checks gave the player a long spell of safety; end it so screenshots show them
        pendingShot = "31-cave-mouth"; yield return 2;

        // Cook at Tomas's fire, then eat from the bag
        state.inv["pond_perch"] = 2;
        player.X = FireX; player.Y = FireY + 11; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.F); yield return 5;
        Check("F at a campfire opens cooking", mode == "panel" && panel == "craft" && craftStation == "fire");
        Craft(Items.Recipes.First(r => r.Out == "grilled_fish"));
        Check("grilled a fish", Has("grilled_fish") == 1 && Has("pond_perch") == 1);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        state.food = 30;
        Inp.Tap(KeyboardKey.I); yield return 3;
        bagSel = "grilled_fish"; yield return 10;
        pendingShot = "28-bag"; yield return 2;
        Eat("grilled_fish");
        Check($"eating grilled fish fills 25 food ({state.food:0})", state.food >= 54.9f && state.food <= 55.1f && Has("grilled_fish") == 0);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        state.food = 0; yield return 2;
        Check("starving makes the reel bar smaller", Starving);
        state.food = 80;

        // A new day regrows the oak and resets the sheep's gift
        int day0 = state.day;
        state.night = true;
        player.X = FireX; player.Y = FireY + 11; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check($"resting into morning starts day {state.day}", state.day == day0 + 1 && !state.night);
        state.day += 3; BuildMap();
        Check("chopped trees grow back after a few days", worldMap[11, 12] == 't');

        // Change your look from the pause menu
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        OpenCreator("edit"); yield return 3;
        editLook.hat = 3; yield return 3;
        Inp.Tap(KeyboardKey.Enter); yield return 3;
        Check("changing your look later keeps the name", mode == "play" && state.look.hat == 3 && state.look.name == "Robin");

        // ---------- Pip, requests, bait, weather, planters, aquarium, boat, music ----------
        // Pip's stall: first meeting, then selling and buying
        state.inv["pond_perch"] = 5; state.inv["arctic_char"] = 2; state.inv["mirage_koi"] = 1;
        int coins0 = state.coins;
        player.X = PipX; player.Y = PipY + 15; player.Face = "up"; yield return 3;
        Check($"Pip can be traded with (prompt: {prompt.Text})", target?.Type == "pip");
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check("meeting Pip starts a chat", mode == "dialogue");
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check("then Pip's shop opens", mode == "panel" && panel == "shop");
        yield return 10;
        pendingShot = "29-shop-sell"; yield return 2;
        int expected = state.inv.Where(kv => Items.ById[kv.Key].Kind == "fish" && !Data.FishById[kv.Key].Rare).Sum(kv => SaleValue(kv.Key, kv.Value));
        SellAllFish();
        Check($"selling ordinary fish keeps the rare koi ({coins0} -> {state.coins} coins, expected +{expected})",
            state.coins == coins0 + expected && Has("mirage_koi") == 1 && Has("pond_perch") == 0);
        int coinsBeforeKoi = state.coins, koiValue = SaleValue("mirage_koi", 1);
        Sell("mirage_koi", 1);
        Check($"a rare fish sells for much more (+{state.coins - coinsBeforeKoi} coins)", state.coins == coinsBeforeKoi + koiValue && koiValue >= 66);
        state.coins += 400;
        shopTab = "buy"; yield return 10;
        pendingShot = "30-shop-buy"; yield return 2;
        Buy("bait", 5, 10); Buy("sailcloth", 120, 1); Buy("sapling", 15, 1);
        Check("bought bait, sailcloth and a sapling", Has("bait") == 10 && Has("sailcloth") == 1 && Has("sapling") == 1);
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Tomas's requests
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        state.req = null; state.reqDone = 0;
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check($"Tomas asks for something ({state.req?.item} x{state.req?.count})", mode == "dialogue" && state.req?.item == "pond_perch" && state.req.count == 3);
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        state.inv["pond_perch"] = 3;
        int coins1 = state.coins;
        yield return 3;
        Check($"Tomas notices you have it (prompt: {prompt.Text})", prompt.Text.StartsWith("Give Tomas"));
        Inp.Tap(KeyboardKey.E); yield return 5;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check($"handing it in pays out ({coins1} -> {state.coins})", state.coins == coins1 + 20 && state.reqDone == 1 && state.req == null && Has("pond_perch") == 0);

        // Bait gets used when casting, glow bait first
        state.inv["glow_bait"] = 1;
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"glow bait is used before plain bait ({Has("glow_bait")} glow, {Has("bait")} plain left)", fish?.Bait == "glow_bait" && Has("glow_bait") == 0 && Has("bait") == 10);
        fish = null; reel = null; mode = "play"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"then plain bait, one per cast ({Has("bait")} left)", fish?.Bait == "bait" && Has("bait") == 9);
        fish = null; reel = null; mode = "play"; yield return 3;

        // Weather
        state.weather = "rain"; player.X = 160; player.Y = 115; yield return 30;
        pendingShot = "31-rain"; yield return 2;
        Check("rain makes fish bite faster", WeatherBite < 1);
        state.weather = "storm"; flash = 1; yield return 2;
        pendingShot = "32-storm"; yield return 2;
        var bridgeTile = bridgeSet.First(b => b.Item2 > 18 && b.Item1 == 16);
        Check("a storm closes the bridges", !CanStand(bridgeTile.Item1 * T + 5, bridgeTile.Item2 * T + 7));
        Check("but not the jetty", CanStand(SaltJettyX, SaltJettyY + 1));
        player.X = 480; player.Y = 150; yield return 3;
        Note($"in a storm away from everything, the prompt is: {prompt.Text}");
        Check("you can always wait out a storm", target?.Type == "rest");
        state.weather = "clear";

        // Plant a berry bush from a sapling
        player.X = 135; player.Y = 147; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 2; Inp.Tap(KeyboardKey.Seven); yield return 2; Inp.Tap(KeyboardKey.E); yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        var planted = state.builds.FirstOrDefault(b => b.id == "berrybush");
        Check($"planted a berry bush ({planted?.x},{planted?.y})", planted != null && Has("sapling") == 0);
        int berries0 = Has("berries");
        yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"picking your own bush ({berries0} -> {Has("berries")})", Has("berries") >= berries0 + 2);

        // An aquarium in a new shack
        state.inv["copper_bar"] = 4; state.inv["wood"] = 40; state.inv["stone"] = 40; state.inv["clownfish"] = 1; state.inv["golden_marlin"] = 1;
        state.builds.Add(new Build { id = "shack", x = 14, y = 12 }); ReindexBuilds();
        player.X = 150; player.Y = 132; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Inp.Tap(KeyboardKey.B); yield return 2;
        player.X = 85; player.Y = 60; player.Face = "up"; yield return 2;
        Inp.Tap(KeyboardKey.Nine); yield return 2; Inp.Tap(KeyboardKey.E); yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        var tankB = SceneBuilds().FirstOrDefault(b => b.id == "aquarium");
        Check($"placed an aquarium ({tankB?.x},{tankB?.y})", tankB != null);
        yield return 3;
        Check($"the aquarium can be used (prompt: {prompt.Text})", target?.Type == "tank");
        Inp.Tap(KeyboardKey.E); yield return 3;
        TankPut("clownfish"); TankPut("golden_marlin");
        Check("two fish went into the aquarium", Tank(tankKey).Count == 2 && Has("clownfish") == 0);
        yield return 10;
        pendingShot = "33-aquarium-panel"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        player.X = 85; player.Y = 80; player.Face = "up"; yield return 20;
        pendingShot = "34-aquarium"; yield return 2;
        player.X = RoomDoorWX; player.Y = RoomDoorWY - 6; player.Face = "down"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 70;

        // Build a boat and sail to the atoll
        state.inv["iron_bar"] = 4;
        Craft(Items.Recipes.First(r => r.Out == "boat"));
        Check("made a sailboat", Has("boat") == 1 && Has("sailcloth") == 0);
        player.X = SaltJettyX; player.Y = SaltJettyY + 1; player.Face = "right"; yield return 3;
        Check($"Pip's jetty offers to sail (prompt: {prompt.Text})", target?.Type == "sail" && target.Id == "atoll");
        Inp.Tap(KeyboardKey.E); yield return 75;
        Check($"sailed to the atoll ({player.X:0},{player.Y:0})", Dist(player.X, player.Y, AtollJettyX + 4, AtollJettyY + 1) < 3 && state.boatAt == "atoll");
        yield return 5;
        Check($"arriving announces Starfall Atoll (toast: {toastMsg})", toastMsg.Contains("Starfall"));
        var atollReach = Reachable();
        foreach (var s in Data.Spots.Where(s => s.Biome == "atoll"))
            Check($"{s.Label} can be fished from the atoll",
                atollReach.Any(t => { float cx = t.Item1 * T + 5, cy = t.Item2 * T + 7; return Dist(cx, cy, s.X, s.Y) < s.R - 2 && CanStand(cx, cy); }));
        int musicWait = 0;
        while (Music.ReadyCount < Music.Names.Count() + 1 && musicWait++ < 600) yield return 1;
        yield return 90;
        Check($"music is ready and the atoll has its own tune (playing: {Music.Current}, {Music.ReadyCount} loops built)", Music.Current == "atoll");
        var (ax, ay) = StandNear("dropoff");
        player.X = ax; player.Y = ay; player.Face = "up"; yield return 3;
        Check($"the deep drop-off can be fished (prompt: {prompt.Text})", target?.Type == "spot" && target.Id == "dropoff");
        yield return 10;
        pendingShot = "35-atoll"; yield return 2;
        File.WriteAllLines(Path.Combine(testDir, "map.txt"), Enumerable.Range(0, ROWS).Select(yy =>
            $"{yy,2} " + new string(Enumerable.Range(0, COLS).Select(xx => worldMap[yy, xx]).ToArray())));
        player.X = AtollJettyX; player.Y = AtollJettyY + 1; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 75;
        Check("sailed back to Saltmere", Dist(player.X, player.Y, SaltJettyX - 4, SaltJettyY + 1) < 3 && state.boatAt == "saltmere");
        player.X = 300; player.Y = 450;
        LoadScene("cave"); yield return 100;
        Check($"the cave has its own music (playing: {Music.Current})", Music.Current == "cave");
        LoadScene("world"); player.X = 160; player.Y = 115; yield return 3;

        // ---------- Fishing: power casts, depth, bait, fights, sizes, and everything around the rod ----------
        state.night = false; state.weather = "clear";
        state.inv.Remove("bait"); state.inv.Remove("glow_bait");
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 3;
        Check($"the lagoon offers a power cast (prompt: {prompt.Text})", target?.Type == "spot" && prompt.Text.Contains("hold"));
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"a quick tap casts short, into the shallows (power {fish?.Power:0.00}, depth {fish?.Depth})", fish != null && fish.Power < 0.05f && fish.Depth == 0 && IsWater(fish.Tx, fish.Ty));
        fish = null; mode = "play"; yield return 3;
        Inp.Hold(KeyboardKey.E, true); Inp.Tap(KeyboardKey.E); yield return 2;
        Check("holding E charges the cast", mode == "charging");
        yield return 32;
        pendingShot = "40-power-cast"; yield return 2;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"letting go casts further (power {fish?.Power:0.00}, depth {fish?.Depth})", fish != null && fish.Power > 0.4f && fish.Depth >= 1 && fish.Depth == CastDepth(fish.Power));
        fish = null; mode = "play"; yield return 2;

        Func<string, string, string, int, double> Wt = (spot, id, bait, depth) => FishWeights(spot, bait, depth).FirstOrDefault(p => p.f.Id == id).w;
        Check("shallow fish prefer short casts, deep fish long ones", Wt("lagoon", "pond_perch", null, 0) > Wt("lagoon", "pond_perch", null, 2) && Wt("deep", "silver_tuna", null, 2) > Wt("deep", "silver_tuna", null, 0));
        Give("stone_sinker"); state.tackle["sinker"] = "stone_sinker";
        Check($"a sinker takes a short cast one depth deeper (depth {CastDepth(0)})", CastDepth(0) == 1);
        state.tackle["sinker"] = "none";
        Check("mud carp go for worms three times as much", Math.Abs(Wt("lagoon", "mud_carp", "worm", 1) - 3 * Wt("lagoon", "mud_carp", null, 1)) < 0.01);
        state.weather = "rain";
        Check("Old Whiskers bites in the rain, but only on a berry", FishWeights("lagoon", "berries", 1).Any(p => p.f.Id == "old_whiskers") && !FishWeights("lagoon", "worm", 1).Any(p => p.f.Id == "old_whiskers"));
        state.weather = "clear";
        Check("and never when it's dry", !FishWeights("lagoon", "berries", 1).Any(p => p.f.Id == "old_whiskers"));
        int keepDay = state.day;
        state.night = true;
        while (MoonPhase != 4) state.day++;
        Check($"the moon carp bites on a full-moon night (day {state.day})", FullMoon && FishWeights("lagoon", null, 1).Any(p => p.f.Id == "moon_carp"));
        yield return 15;
        pendingShot = "41-full-moon"; yield return 2;
        state.day++;
        Check("but not on other nights", !FullMoon && !FishWeights("lagoon", null, 1).Any(p => p.f.Id == "moon_carp"));
        state.day = keepDay; state.night = false;
        state.weather = "storm";
        Check("the storm eel only bites in a storm", FishWeights("wreck", null, 1).Any(p => p.f.Id == "storm_eel"));
        state.weather = "clear";
        Check("and not in fair weather", !FishWeights("wreck", null, 1).Any(p => p.f.Id == "storm_eel"));

        // The perfect hook
        TestBite("lagoon", "pond_perch"); fish.BiteT = BiteWindow - 0.05f;
        Hook();
        Check($"hooking the moment it bites is a perfect hook (progress {reel?.Progress:0.00})", mode == "reeling" && reel.Perfect && reel.Progress == 0.5f);
        fish = null; reel = null; mode = "play";
        TestBite("lagoon", "pond_perch"); fish.BiteT = 0.3f;
        Hook();
        Check("hooking late is an ordinary hook", mode == "reeling" && !reel.Perfect && reel.Progress == 0.35f);
        fish = null; reel = null; mode = "play"; yield return 2;

        // Runners snap a line held tight during a run, and calm down when you let go
        TestBite("deep", "silver_tuna"); fish.BiteT = 0.3f; Hook();
        Check("a silver tuna fights as a runner", reel?.Style == "runner");
        reel.Progress = 0.6f; reel.Running = 3; reel.RunT = 99;
        Inp.Hold(KeyboardKey.Space, true);
        for (int i = 0; i < 30; i++) yield return 1;
        pendingShot = "42-runner"; yield return 2;
        for (int i = 0; i < 240 && mode == "reeling"; i++) yield return 1;
        Inp.Hold(KeyboardKey.Space, false);
        Check($"holding on while it runs snaps the line (toast: {toastMsg})", mode == "play" && toastMsg.StartsWith("Snap"));
        TestBite("deep", "silver_tuna"); fish.BiteT = 0.3f; Hook();
        reel.Running = 1.5f; reel.RunT = 99; reel.Tension = 0.6f; reel.Progress = 0.6f;
        yield return 30;
        Check($"letting go while it runs eases the tension ({reel?.Tension:0.00})", mode == "reeling" && reel.Tension < 0.6f);
        fish = null; reel = null; mode = "play"; yield return 2;

        // Jumpers: a press in the gold keeps them hooked, a mistimed one loses ground
        TestBite("dropoff", "sailfish"); fish.BiteT = 0.3f; Hook();
        Check("a sailfish fights as a jumper", reel?.Style == "jumper");
        reel.Progress = 0.5f; reel.LeapT = 0.01f;
        yield return 2;
        Check("it leaps", reel.Leap > 0);
        while (mode == "reeling" && reel.LeapMark < 0.68f) yield return 1;
        pendingShot = "43-leap"; yield return 1;
        float before = reel.Progress;
        Inp.Tap(KeyboardKey.Space); yield return 1;
        Check($"a press in the gold keeps it hooked (+{reel.Progress - before:0.00})", reel.Progress > before + 0.1f);
        while (mode == "reeling" && reel.Leap > 0) yield return 1;
        reel.LeapT = 0.01f; yield return 2;
        before = reel.Progress;
        Inp.Tap(KeyboardKey.Space); yield return 1;
        Check($"a mistimed press loses ground ({reel.Progress - before:0.00})", reel.Progress < before - 0.05f);
        fish = null; reel = null; mode = "play"; yield return 2;

        // Bottom-huggers: short pumps haul them up, holding too long lets them dig in
        TestBite("lagoon", "mud_carp"); fish.BiteT = 0.3f; Hook();
        Check("a mud carp hugs the bottom", reel?.Style == "bottom");
        reel.Progress = 0.5f; reel.FishY = reel.FishTarget = reel.ZoneY + reel.ZoneH / 2; reel.FishTimer = 99;
        before = reel.Progress;
        Inp.Tap(KeyboardKey.Space); yield return 1;
        Check($"a short pump hauls it up (+{reel.Progress - before:0.00})", reel.Progress > before + 0.04f);
        Inp.Hold(KeyboardKey.Space, true); yield return 45;
        Check("holding too long lets it dig in", reel?.Digging == true);
        Inp.Hold(KeyboardKey.Space, false);
        fish = null; reel = null; mode = "play"; yield return 2;

        // A whole fight, then the catch is weighed, recorded and held up
        int perch0 = Has("pond_perch");
        TestBite("lagoon", "pond_perch"); fish.BiteT = 0.5f; Hook();
        reel.Progress = 0.8f;
        for (int i = 0; i < 1800 && mode == "reeling"; i++) { Inp.Hold(KeyboardKey.Space, reel != null && reel.FishY < reel.ZoneY + reel.ZoneH / 2); yield return 1; }
        Inp.Hold(KeyboardKey.Space, false);
        Check($"a landed fish is weighed and recorded ({toastMsg})", Has("pond_perch") == perch0 + 1 && state.records.ContainsKey("pond_perch") && toastMsg.Contains("kg"));
        yield return 8;
        pendingShot = "44-holding-catch"; yield return 2;
        var tuna = Data.FishById["silver_tuna"];
        int big0 = state.big.GetValueOrDefault("silver_tuna"), tr0 = state.trophies.GetValueOrDefault("silver_tuna");
        float kgBig = AddCatch(tuna, 3f);
        Check($"a huge catch is a trophy and counts as big ({Kg(kgBig)})", state.trophies.GetValueOrDefault("silver_tuna") == tr0 + 1 && state.big.GetValueOrDefault("silver_tuna") == big0 + 1 && state.records["silver_tuna"] == kgBig);
        float kgSmall = AddCatch(tuna, 0.3f);
        Check($"a small one doesn't beat the record ({Kg(kgSmall)})", state.records["silver_tuna"] == kgBig && kgSmall < kgBig);
        Check($"big fish sell for more ({SaleValue("silver_tuna", 1)} coins vs {PriceOf("silver_tuna")})", SaleValue("silver_tuna", 1) > PriceOf("silver_tuna"));
        state.xp = XpFor(5) - 1;
        GainXp(5);
        Check($"experience raises your fishing level ({FishLevel}, toast: {toastMsg})", FishLevel == 5 && toastMsg.Contains("level 5") && Perk(5));

        // A sunken chest: press the arrows in order
        TestBite("lagoon", "pond_perch");
        fish.Roll = new Catchable { Id = "chest", Name = "Sunken chest", Difficulty = 2, Chest = true };
        Hook();
        Check("a chest on the line starts the arrow game", mode == "chest" && chestSeq.Count >= 5);
        yield return 10;
        pendingShot = "45-chest"; yield return 2;
        int coinsC = state.coins, chests0 = state.chests;
        float chestT0 = chestT;
        Inp.Tap(ArrowKey((chestSeq[0] + 1) % 4)); yield return 1;
        Check("a wrong arrow costs time", chestT < chestT0 - 0.7f && chestAt == 0);
        foreach (int d in chestSeq.ToList()) { Inp.Tap(ArrowKey(d)); yield return 2; }
        Check($"the right arrows haul it up ({toastMsg})", mode == "play" && state.chests == chests0 + 1 && state.coins >= coinsC + 20);
        yield return 10;
        pendingShot = "46-chest-open"; yield return 2;

        // The ice hole: chip it open, then jig on the beat
        var (ix, iy) = StandNear("icehole");
        player.X = ix; player.Y = iy; player.Face = "up"; yield return 3;
        Check($"the ice hole has frozen over (prompt: {prompt.Text})", target?.Type == "drill");
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("E starts chipping at the ice", mode == "drill");
        for (int i = 0; i < 5; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
        pendingShot = "47-drilling"; yield return 2;
        for (int i = 0; i < 40 && mode == "drill"; i++) { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check($"mashing E breaks through the ice ({toastMsg})", mode == "play" && !IceFrozen);
        yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("at the ice hole you drop the line straight in", fish != null && fish.Spot == "icehole" && mode is "casting" or "waiting");
        while (mode == "casting") yield return 1;
        fish.Timer = 30; fish.WaitT = JigBeat * 4 - 0.03f;
        Inp.Tap(KeyboardKey.E); yield return 1;
        Check($"jigging on the beat brings a bite closer ({fish?.Timer:0.00}s, {fish?.Jigs} jig)", fish != null && fish.Timer < 29.4f && fish.Jigs == 1);
        float tj = fish.Timer;
        fish.WaitT = JigBeat * 4 + 0.45f;
        Inp.Tap(KeyboardKey.E); yield return 1;
        Check("an off-beat jig scares the fish a little", fish != null && fish.Timer > tj && fish.Jigs == 0);
        yield return 8;
        pendingShot = "48-ice-jig"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Check("Esc reels in", mode == "play" && fish == null);

        // Spearfishing on the reef
        Give("spear");
        var (rx, ry) = StandNear("coral");
        player.X = rx; player.Y = ry; player.Face = "down"; yield return 3;
        Check($"the coral shallows offer spearfishing (prompt: {prompt.Text})", target?.Alt2Type == "spear");
        Inp.Tap(KeyboardKey.G); yield return 2;
        Check($"G starts spearfishing ({spearFish.Count} fish about)", mode == "spear" && spearFish.Count > 0);
        foreach (var sf in spearFish) { sf.Vx = sf.Vy = 0; sf.Turn = 99; }
        var prey = spearFish[0];
        aimX = prey.X; aimY = prey.Y; yield return 10;
        pendingShot = "49-spearfishing"; yield return 2;
        int speared0 = speared, preyHave = Has(prey.F.Id);
        Inp.Tap(KeyboardKey.E); yield return 20;
        Check($"a spear on a shadow catches it ({prey.F.Name}, toast: {toastMsg})", speared == speared0 + 1 && Has(prey.F.Id) == preyHave + 1);
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Check("Esc ends spearfishing", mode == "play");

        // Chum, polarized sunglasses and the fish finder
        Give("chum", 2); yield return 2;
        Check($"a fishing spot offers chum (prompt: {prompt.Text})", target?.AltType == "chum");
        Inp.Tap(KeyboardKey.F); yield return 2;
        Check("F throws chum and the fish crowd in", Chummed("coral") && Has("chum") == 1);
        Give("sunglasses"); Give("fish_finder");
        yield return 20;
        pendingShot = "50-chum-shadows-finder"; yield return 2;
        var odds = CatchOdds("coral", 1);
        Check($"the fish finder's odds add up ({odds.Sum(o => o.pct):0.000})", Math.Abs(odds.Sum(o => o.pct) - 1) < 0.01);
        Check("there are fish shadows to see at a spot", Enumerable.Range(0, 4).Any(k => ShadowPos(Data.SpotById["coral"], k, time) != null));

        // Waders
        Give("waders");
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 2;
        Check("waders let you walk into shallow water", CanStand(100, 75, true) && !CanStand(100, 75));
        Inp.Hold(KeyboardKey.Up, true); yield return 40; Inp.Hold(KeyboardKey.Up, false); yield return 2;
        Check($"wading out into the lagoon reaches deeper water ({player.X:0},{player.Y:0})", InWater && CastDepth(0) == 1);
        pendingShot = "51-wading"; yield return 2;
        player.X = 160; player.Y = 115; yield return 2;

        // A crab pot: set it in the shallows, haul it up the next morning
        Give("crab_pot");
        Check("found shallow water to set a pot in", FaceWater());
        yield return 2;
        Inp.Tap(KeyboardKey.B); yield return 2;
        SelectTool("crabpot"); yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        var potB = state.builds.FirstOrDefault(b => b.id == "crabpot");
        Check($"set a crab pot in the water ({ghost?.Reason})", potB != null && state.pots.ContainsKey($"{potB.x},{potB.y}"));
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check($"the pot needs a night to fill (prompt: {prompt.Text})", target?.Type == "info");
        state.day++; yield return 2;
        Check($"the next morning it can be hauled up (prompt: {prompt.Text})", target?.Type == "pot");
        int crabs0 = Has("shore_crab");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"hauling the pot brings up crabs ({toastMsg})", Has("shore_crab") > crabs0 && potB != null && !PotReady(potB));
        yield return 10;
        pendingShot = "52-crab-pot"; yield return 2;
        state.day--;

        // Worms and crickets for bait
        quietWildlife = false;
        player.X = 125; player.Y = 125; player.Face = "down"; yield return 2;
        state.loose.Add(new Loose { kind = "worm", tx = 12, ty = 12, x = player.X + 3, y = player.Y - 1 });
        yield return 2;
        Check($"a worm mound can be dug (prompt: {prompt.Text})", target?.Type == "dig");
        int worms0 = Has("worm");
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"digging turns up worms (+{Has("worm") - worms0})", Has("worm") >= worms0 + 2);
        bugs.Clear();
        bugs.Add(new Bug { X = player.X + 7, Y = player.Y, Rest = 5 });
        yield return 2;
        Check($"a cricket can be caught (prompt: {prompt.Text})", target?.Type == "cricket");
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("caught a cricket", Has("cricket") == 1);
        bugs.Add(new Bug { X = player.X + 14, Y = player.Y, Rest = 0.2f });
        state.loose.Add(new Loose { kind = "worm", tx = 13, ty = 13, x = player.X - 12, y = player.Y + 6 });
        yield return 20;
        pendingShot = "53-worms-crickets"; yield return 2;
        quietWildlife = true; bugs.Clear(); state.loose.RemoveAll(l => l.kind == "worm");

        // The tackle box
        Inp.Tap(KeyboardKey.T); yield return 5;
        Check("T opens the tackle box", mode == "panel" && panel == "tackle");
        CycleTackle("bait", 1);
        string chosen = TackleChoice("bait");
        Check($"picking a bait in the tackle box ({chosen}) makes the next cast use it", chosen != "auto" && NextBait() == chosen);
        Give("copper_reel"); Give("silk_line");
        Check($"your best tackle is used until you choose ({GearId("reel")}, {GearId("line")})", GearId("reel") == "copper_reel" && ReelMul > 1 && LineMul > 1);
        yield return 10;
        pendingShot = "54-tackle-box"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 2;

        // Smoking and sushi
        Give("seaweed", 2); state.inv["pond_perch"] = 3; state.inv["wood"] = 30; state.inv["stone"] = 30;
        state.builds.Add(new Build { id = "smoker", x = 19, y = 11 }); ReindexBuilds();
        player.X = 195; player.Y = 129; player.Face = "up"; yield return 3;
        Check($"the smoking rack can be used (prompt: {prompt.Text})", target?.Type == "craft" && target.Id == "smoker");
        Inp.Tap(KeyboardKey.E); yield return 5;
        Craft(Items.Recipes.First(r => r.Out == "smoked_fish"));
        Check("smoked a fish", Has("smoked_fish") == 1);
        yield return 5;
        pendingShot = "55-smoker"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Craft(Items.Recipes.First(r => r.Out == "sushi_roll"));
        Check("rolled some sushi", Has("sushi_roll") == 1);

        // Pip's derby
        player.X = PipX; player.Y = PipY + 15; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 5;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        shopTab = "derby"; yield return 10;
        pendingShot = "56-derby-tab"; yield return 2;
        int coinsD = state.coins, wins0 = state.derbyWins;
        StartDerby(); yield return 3;
        Check("the derby starts and the shop closes", DerbyOn && mode == "play" && state.derbyDay == state.day);
        AddCatch(Data.FishById["bluefin_tuna"], 1.2f);
        yield return 5;
        pendingShot = "57-derby"; yield return 2;
        derbyT = 0.02f; yield return 3;
        Check($"beating every rival wins the derby ({toastMsg})", !DerbyOn && state.derbyWins == wins0 + 1 && state.coins >= coinsD + 120);
        shopTab = "sell";

        // Aquarium collections
        state.tanks["test|0,0"] = new List<string> { "pond_perch", "mud_carp", "rock_goby", "striped_wrasse" };
        Check("a full set on show turns on its collection bonus", SetActive("saltmere"));
        state.tanks.Remove("test|0,0");
        Check("and it turns off when the set is broken up", !SetActive("saltmere"));

        // Another legend
        state.weather = "rain";
        fish = new FishCast { Spot = "lagoon", Bx = 100, By = 66 };
        reel = new ReelState { Roll = new Catchable { Id = "old_whiskers", Name = "Old Whiskers", Difficulty = 4.2f, Rare = true } };
        mode = "reeling";
        LandCatch();
        Check("Old Whiskers gets a legendary card", mode == "legend" && legendId == "old_whiskers");
        yield return 45;
        pendingShot = "58-legend-whiskers"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check("and only bites once", mode == "play" && !FishWeights("lagoon", "berries", 1).Any(p => p.f.Id == "old_whiskers"));
        state.weather = "clear";

        // The Fish log and the tackle recipes
        Inp.Tap(KeyboardKey.J); yield return 3;
        dexTab = "log"; logPage = 1; yield return 20;
        pendingShot = "59-fishlog-frost"; yield return 2;
        logPage = 5; yield return 10;
        pendingShot = "60-fishlog-legends"; yield return 2;
        logPage = 0; dexTab = "creatures";
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        OpenCraft("workbench"); craftTab = "Tackle"; craftPage = 0; yield return 10;
        Check("the tackle recipes run to more than one page", Items.Recipes.Count(r => r.Station == "workbench" && Items.Category(r) == "Tackle") > CraftPerPage);
        pendingShot = "61-craft-tackle"; yield return 2;
        ClosePanels(); craftTab = "Tools";
        Save();
        var reloaded = SaveFile.Read();
        Check("records, experience, tackle and pots are saved", reloaded != null && reloaded.records.Count == state.records.Count && reloaded.xp == state.xp
            && reloaded.tackle.GetValueOrDefault("bait") == state.tackle.GetValueOrDefault("bait") && reloaded.pots.Count == state.pots.Count);

        // A save from before the bag and character creation still loads
        string realSave = Environment.GetEnvironmentVariable("FESH_SAVE"), oldSave = Path.Combine(testDir, "old-save.json");
        File.WriteAllText(oldSave, "{\"caught\":[\"glowgill\"],\"commons\":{\"pond_perch\":3},\"night\":false,\"casts\":7,\"flags\":{\"metTomas\":true},"
            + "\"pity\":{},\"hinted\":{},\"px\":160,\"py\":115,\"mats\":{\"wood\":12,\"stone\":5},\"builds\":[{\"id\":\"campfire\",\"x\":17,\"y\":11}],\"loose\":[]}");
        Environment.SetEnvironmentVariable("FESH_SAVE", oldSave);
        var old = SaveFile.Read();
        Environment.SetEnvironmentVariable("FESH_SAVE", realSave);
        Check("an old save moves wood and stone into the bag and asks for a fisher",
            old != null && old.inv.GetValueOrDefault("wood") == 12 && old.inv.GetValueOrDefault("stone") == 5 && old.inv.ContainsKey("rod_old")
            && !old.created && old.food == 100 && old.builds.Count == 1 && old.scene == "world");

        // Ending screen
        state.caught = Data.Creatures.Select(c => c.Id).ToList();
        endStats = $"Creatures found: 5 of 5. Common fish caught: 3. Casts: {state.casts}. Things built: {state.builds.Count}.";
        mode = "ending"; yield return 10;
        pendingShot = "11-end"; yield return 2;
    }
}
#endif
