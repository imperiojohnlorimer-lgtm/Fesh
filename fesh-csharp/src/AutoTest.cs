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
        // FESH_SPRITES=file.png draws the people and boats in every pose to a zoomed sheet and quits (no saves touched).
        if (Environment.GetEnvironmentVariable("FESH_SPRITES") is { Length: > 0 } sheet) { ExportSprites(sheet); quit = true; return; }
        testDir = Environment.GetEnvironmentVariable("FESH_AUTOTEST");
        if (string.IsNullOrEmpty(testDir)) return;
        Directory.CreateDirectory(testDir);
        // The run wipes every save slot and the settings where it saves, so never let it near the real ones.
        if (Environment.GetEnvironmentVariable("FESH_SAVE") is not { Length: > 0 })
        {
            File.WriteAllText(Path.Combine(testDir, "autotest.log"), "FAIL  FESH_SAVE isn't set: the autotest would wipe your real saves, so it didn't run.");
            quit = true;
            return;
        }
        SaveFile.Slot = 1;
        for (int n = 1; n <= SaveFile.Slots; n++) SaveFile.Clear(n);
        hasSave = false;
        Settings.Reset();
        // The clock stands still unless a check moves it, so day and night only change when the script says so.
        Settings.Data.dayLength = 0;
        // Clicking another window during the run mustn't pause it: the run always has focus, as far as the game knows.
        Settings.Data.pauseUnfocused = false;
        Inp.ScriptFocused = true;
        // Tomas, Pip and the villagers stay at home unless a check wants them strolling (see Folk.cs).
        standStill = true;
        script = Environment.GetEnvironmentVariable("FESH_VIEW_TEST") == "1" ? ViewScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_ATLAS_TEST") == "1" ? AtlasScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_EDU_TEST") == "1" ? EduScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_GUIDE_TEST") == "1" ? GuideScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_GUSO_TEST") == "1" ? GusoScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_SEASON_TEST") == "1" ? SeasonScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_DIRECTION_TEST") == "1" ? DirectionScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_HABAGAT_TEST") == "1" ? HabagatScript().GetEnumerator()
            : Environment.GetEnvironmentVariable("FESH_AMIHAN_TEST") == "1" ? AmihanScript().GetEnumerator() : Script();
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

    // A sheet of every person pose (rows: facing and look; columns: standing, the four walk frames, then the rod,
    // overhead, leaning, glancing and blinking poses), drawn 5x so single pixels can be judged.
    void ExportSprites(string path)
    {
        ExportToolSprites(path);
        ExportDirectionSprites(path);
        var keep = pix;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#86b86a"));
        var looks = new[]
        {
            new Look { skin = 1, hair = 0, hairColor = 1, hat = 1, shirt = 0, pants = 0 },
            new Look { skin = 3, hair = 1, hairColor = 0, hat = 0, shirt = 1, pants = 2 }
        };
        int row = 0;
        foreach (string face in new[] { "down", "up", "right", "left" })
            foreach (var look in looks)
            {
                int y = 17 + row++ * 20, col = 0;
                void At(int step, int arms = 0, int swing = 0, int lean = 0, string head = null, bool blink = false) =>
                    LookData.DrawPerson(sheet, look, 9 + col++ * 13, y, face, step, arms: arms, swing: swing, lean: lean, head: head, blink: blink);
                for (int s = 0; s <= 4; s++) At(s);
                At(0, arms: 3); At(0, arms: 3, swing: 1); At(0, arms: 2); At(0, arms: 1);
                At(0, lean: -1); At(0, lean: 1); At(0, head: "left"); At(0, head: "right"); At(0, blink: true);
            }
        // The boat, out at sea: rows facing right, facing left, then moored and in a storm. Columns step through time.
        var boats = new Pix(W, H) { CamX = 1150, CamY = 380 };
        pix = boats;
        Array.Fill(boats.Buf, Pal.C("#1d5f8c"));
        state.inv["boat"] = 1; state.aboard = true; scene = "world";
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 8; c++)
            {
                player.X = boats.CamX + 22 + c * 37; player.Y = boats.CamY + 24 + r * 38;
                boatFace = r == 1 ? "left" : "right"; player.Face = boatFace;
                state.weather = r == 3 ? "storm" : "clear";
                bool sailing = c is >= 2 and <= 4 || r == 3, fishingNow = c >= 6;
                player.Moving = sailing; mode = fishingNow ? (c == 7 ? "reeling" : "waiting") : "play";
                sailFurl = fishingNow ? 1 : c == 5 ? .5f : 0;
                fish = fishingNow ? new FishCast { Spot = "opensea", Bx = player.X + 30 * BoatDir, By = player.Y + 4, Tx = player.X + 30 * BoatDir, Ty = player.Y + 4 } : null;
                float t = c * .37f;
                if (r == 2) { state.aboard = false; DrawBoat(player.X, player.Y, t); state.aboard = true; }
                else { time = t; DrawBoat(player.X, player.Y, t, occupied: true); if (fish != null) DrawFishing(t); }
            }
        fish = null; mode = "title"; state.aboard = false; state.weather = "clear";
        pix = keep;
        {
            int w = 300, h = 154;
            var img = GenImageColor(w, h, Color.Black);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) ImageDrawPixel(ref img, x, y, boats.Buf[y * W + x]);
            ImageResizeNN(ref img, w * 5, h * 5);
            ExportImage(img, path.Replace(".png", "-c.png"));
            UnloadImage(img);
        }
        // Every fish: the Fish log's big picture, its silhouette and its bag icon.
        {
            var ids = FishArt.Looks.Keys.ToList();
            int cols = 6, cw = 84, ch = 34, w = cols * cw, h = (ids.Count + cols - 1) / cols * ch;
            var fishes = new Pix(w, h);
            Array.Fill(fishes.Buf, Pal.C("#2a5a7a"));
            for (int i = 0; i < ids.Count; i++)
            {
                int cx = i % cols * cw + 2, cy = i / cols * ch + 3;
                FishArt.Draw(fishes, ids[i], cx, cy, FishArt.BigW, FishArt.BigH);
                FishArt.Draw(fishes, ids[i], cx + 50, cy, 12, 12);
                FishArt.Draw(fishes, ids[i], cx + 64, cy, 12, 12, silhouette: "#1d3550");
                FishArt.Draw(fishes, ids[i], cx + 50, cy + 15, 11, 7, flip: true);
            }
            var img = GenImageColor(w, h, Color.Black);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) ImageDrawPixel(ref img, x, y, fishes.Buf[y * w + x]);
            ImageResizeNN(ref img, w * 3, h * 3);
            ExportImage(img, path.Replace(".png", "-fish.png"));
            UnloadImage(img);
        }
        // Two halves (front and back, then the sides), each small enough to judge pixel by pixel.
        for (int half = 0; half < 2; half++)
        {
            int w = 184, h = 82, top = half * 80;
            var img = GenImageColor(w, h, Color.Black);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[(top + y) * W + x]);
            ImageResizeNN(ref img, w * 7, h * 7);
            ExportImage(img, path.Replace(".png", half == 0 ? "-a.png" : "-b.png"));
            UnloadImage(img);
        }
    }

    // Noon or ten at night, straight away (no dusk or dawn in between: the day doesn't change).
    void SetNight(bool night) => state.clock = night ? 22 * 60 : 12 * 60;

    // Clicks a button the way a player would: the mouse goes where it was last drawn, and clicks on the next frame.
    bool ClickButton(string label)
    {
        if (!Gfx.Seen.TryGetValue(label, out var r)) return false;
        Inp.ScriptMouse = new System.Numerics.Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
        Inp.ScriptClickNext = true;
        return true;
    }

    // Fair weather all day, so no change of weather turns up in the middle of a check.
    void ClearSkies()
    {
        state.forecast = new() { new WeatherSpell { at = 0, w = "clear" } };
        state.weather = planned = "clear";
        SnapWeather();
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

    // Every outdoor tile you could reach from a point on foot in waders (land and shallow water).
    HashSet<(int, int)> WadeReach(float px, float py)
    {
        var start = ((int)(px / T), (int)(py / T));
        var seen = new HashSet<(int, int)> { start };
        var stack = new Stack<(int, int)>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = (x + ox, y + oy);
                char t = TileAt(n.Item1, n.Item2);
                if (!(Walkable(t) || Wadeable(t)) || !seen.Add(n)) continue;
                stack.Push(n);
            }
        }
        return seen;
    }

    // Puts Tidemane on the sand north of the Starwell, winded, with you right beside it, facing it.
    void BossBeside(float spirit = -1)
    {
        boss.X = StarwellX - 30; boss.Y = StarwellY - 45; boss.Dir = 1; boss.Lift = 0; boss.Ring = -1; boss.Hurt = 0;
        if (spirit >= 0) boss.Spirit = spirit;
        Winded(boss, 5);
        player.X = boss.X + 12; player.Y = boss.Y; player.Face = "left";
        swingT = 0;
    }

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
        Note(Inp.PadIndex >= 0 ? $"a gamepad is plugged in (index {Inp.PadIndex})" : "no gamepad plugged in");
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
        SetNight(true); yield return 10;
        pendingShot = "04-night"; yield return 2;

        // Resting at the built campfire
        var fire = state.builds.First(b => b.id == "campfire");
        player.X = fire.x * T + 5; player.Y = fire.y * T + 14; yield return 3;
        Check($"campfire offers rest and cooking (prompt: {prompt.Text})", target?.Type == "rest" && target.AltType == "cook");

        // Talking to Tomas shows the speaker tag
        SetNight(false);
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
        Check("the menu opens on the Game tab", menuTab == "game");
        menuTab = "settings"; yield return 3;
        pendingShot = "10b-settings"; yield return 2;

        // Rebinding a key in the Controls tab
        menuTab = "controls"; yield return 3;
        rebind = ("bag", 0); yield return 2;
        pendingShot = "10c-controls"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Check("Esc while waiting for a key cancels it and keeps the menu open", rebind == null && mode == "pause" && Bind.Key("bag", 0) == KeyboardKey.I);
        rebind = ("bag", 0); yield return 2;
        Inp.Tap(KeyboardKey.K); yield return 2;
        Check("pressing a key binds it", rebind == null && Bind.Key("bag", 0) == KeyboardKey.K && mode == "pause");
        rebind = ("bag", 1); yield return 2;
        Inp.Tap(KeyboardKey.Enter); yield return 2;
        Check($"Enter can't be bound ({rebindNote})", rebind != null && Bind.Key("bag", 1) == KeyboardKey.Null);
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("then Esc closes the menu", mode == "play");
        Inp.Tap(KeyboardKey.I); yield return 3;
        Check("the old key no longer opens the bag", mode == "play");
        Inp.Tap(KeyboardKey.K); yield return 3;
        Check("the new one does", mode == "panel" && panel == "bag");
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Bind.Set("act", 0, KeyboardKey.K);
        Check("taking another action's key swaps the two", Bind.Key("act", 0) == KeyboardKey.K && Bind.Key("bag", 0) == KeyboardKey.E);
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 3;
        Check($"prompts name the new key ({prompt.Key}: {prompt.Text})", target?.Type == "spot" && prompt.Key == "K");
        TestBite("lagoon", "pond_perch"); yield return 2;
        Check($"so does the bite ({prompt.Key}: {prompt.Text})", prompt.Key == "K");
        fish = null; reel = null; mode = "play"; yield return 2;
        Toast("Press <act> to cast, <bag> for your bag, <move> to walk.");
        Check($"so do tips ({toastMsg})", toastMsg == "Press K to cast, E for your bag, WASD to walk.");
        Settings.Data.music = 0.35f;
        Settings.Save();
        Settings.Load();
        Check("settings and keys are saved and come back", Math.Abs(Settings.Data.music - 0.35f) < 0.001f && Bind.Key("act", 0) == KeyboardKey.K && Settings.Data.dayLength == 0);
        Settings.Reset();
        Settings.Data.dayLength = 0; Settings.Data.pauseUnfocused = false;
        Check("resetting puts every key back", Bind.Key("act", 0) == KeyboardKey.E && Bind.Key("bag", 0) == KeyboardKey.I && Settings.Data.music == 0.8f);
        Bind.Load(new() { ["bag"] = new[] { (int)KeyboardKey.W, 0 } });
        Check("a settings file that gives W to the bag doesn't also leave W walking up",
            Bind.Key("bag", 0) == KeyboardKey.W && Bind.Key("up", 0) == KeyboardKey.Null && Bind.Key("up", 1) == KeyboardKey.Up);
        Bind.Reset();
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        menuTab = "settings"; yield return 2;
        dragSlider = "music"; Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc while holding a slider lets go of it", dragSlider == null && mode == "play");
        Check($"test settings live apart from the real ones ({Path.GetFileName(SaveFile.SettingsPath)})",
            Path.GetFileName(SaveFile.SettingsPath) == Path.GetFileNameWithoutExtension(Environment.GetEnvironmentVariable("FESH_SAVE")) + "-settings.json");
        player.X = 160; player.Y = 115; yield return 3;

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
        foreach (var s in Data.Spots.Where(s => s.Scene == "world" && s.Biome is not ("atoll" or "amihan" or "habagat")))
        {
            bool ok = reach.Any(t => { float cx = t.Item1 * T + 5, cy = t.Item2 * T + 7; return Dist(cx, cy, s.X, s.Y) < s.R - 2 && CanStand(cx, cy); });
            Check($"{s.Label} ({Data.Biomes.First(b => b.Id == s.Biome).Name}) can be fished from land", ok);
        }
        state.flags.tideOut = tide; state.flags.dockFixed = dock; BuildMap();
        Note($"world {COLS}x{ROWS} tiles, {trees.Count} trees, {shore.Count} shoreline pixels");
        File.WriteAllLines(Path.Combine(testDir, "map.txt"), Enumerable.Range(0, ROWS).Select(y =>
            $"{y,2} " + new string(Enumerable.Range(0, COLS).Select(x => map[y, x]).ToArray())));

        // Catch odds: the dog at the lagoon, the night-only pike, and each spot keeping its own fish
        SetNight(false);
        int dogs = 0;
        for (int i = 0; i < 4000; i++) if (RollCatch("lagoon").Id == "dog") dogs++;
        Check($"a dog bites at the lagoon about 7% of the time ({dogs / 40.0:0.0}%)", dogs > 180 && dogs < 400);
        int pikeDay = Enumerable.Range(0, 2000).Count(_ => RollCatch("icehole").Id == "crystal_pike");
        SetNight(true);
        int pikeNight = Enumerable.Range(0, 2000).Count(_ => RollCatch("icehole").Id == "crystal_pike");
        SetNight(false);
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
        SetNight(true); yield return 10;
        pendingShot = "15-mire-night"; yield return 2;
        SetNight(false);

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
        SetNight(true); yield return 5;
        pendingShot = "24-shack-night"; yield return 2;
        SetNight(false);
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
        float healingStarted = time;
        yield return 120;
        Check($"health slowly recovers while you're fed ({state.hp:0.0})", state.hp > 50.6f && Math.Abs(state.hp - 50 - (time - healingStarted) * .5f) < .05f);
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
        SetNight(true);
        player.X = FireX; player.Y = FireY + 11; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check($"resting into morning starts day {state.day}", state.day == day0 + 1 && !Night);
        state.day += 3; BuildMap();
        Check("chopped trees grow back after a few days", worldMap[11, 12] == 't');

        // ---------- The clock ----------
        Check($"resting through the night ends at 6:30 AM ({ClockText(state.clock)})", (int)state.clock == 6 * 60 + 30);
        Check($"the clock reads like a clock ({ClockText(0)}, {ClockText(13 * 60 + 5)}, {HourText(20 * 60)})",
            ClockText(0) == "12:00 AM" && ClockText(13 * 60 + 5) == "1:05 PM" && HourText(20 * 60) == "8 PM");
        Settings.Data.clock24 = true;
        Check($"or a 24-hour one ({ClockText(13 * 60 + 5)})", ClockText(13 * 60 + 5) == "13:05" && HourText(6 * 60) == "06:00");
        Settings.Data.clock24 = false;
        int dayR = state.day;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check($"resting by day ends at 9 PM the same day ({ClockText(state.clock)}, day {state.day})", (int)state.clock == 21 * 60 && state.day == dayR && Night);
        // Two boundaries in one skip (dusk, then dawn) make exactly one new day.
        state.clock = 19 * 60;
        SkipTo(7 * 60);
        Check($"skipping from 7 PM to 7 AM is one new day ({state.day})", state.day == dayR + 1 && !Night);

        // Left alone, the clock runs while you play (12-minute days: two game minutes a second) and stops in menus.
        player.X = 160; player.Y = 115; player.Face = "down";
        ClearSkies();
        Settings.Data.dayLength = 12;
        state.clock = 19 * 60 + 58; yield return 90;
        Check($"the clock runs while you play ({ClockText(state.clock)})", state.clock > 20 * 60 && state.clock < 20 * 60 + 15 && Night);
        Check($"and nightfall is announced ({toastMsg})", toastMsg.StartsWith("Night falls"));
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        float pausedAt = state.clock; yield return 60;
        Check("it stops while the menu is open", mode == "pause" && state.clock == pausedAt);
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Dawn brings a new day without resting, and grows back what's due, one tile at a time, but not under your feet.
        var due = trees.Where(t => t.kind == 't' && t.x < 32 && t.y < 18 && Dist(t.x * T + 5, t.y * T + 5, 160, 115) > 30).Take(2).ToList();
        foreach (var (tx, ty, tk) in due) { Fell(tx, ty, tk); state.felled[$"{tx},{ty}"] = state.day - 5; }
        var (ux, uy, _) = due[1];
        player.X = ux * T + 5; player.Y = uy * T + 7;
        int dayD = state.day;
        // A fair tomorrow, so a storm warning first thing can't replace the morning's toast.
        state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        // Nothing lying on the first tree's tile or wandering over it, which would rightly hold it back a day.
        state.loose.RemoveAll(l => l.tx == due[0].x && l.ty == due[0].y);
        animals.RemoveAll(a => Dist(a.X, a.Y, due[0].x * T + 5, due[0].y * T + 5) < 50);
        state.clock = 5 * 60 + 59; yield return 60;
        Check($"dawn starts a new day by itself (day {state.day}, {toastMsg})", state.day == dayD + 1 && toastMsg.StartsWith($"Morning of day {state.day}"));
        Check("a felled tree that's due grows back at dawn", worldMap[due[0].y, due[0].x] == 't' && !state.felled.ContainsKey($"{due[0].x},{due[0].y}"));
        Check("but not the one you're standing on", worldMap[uy, ux] != 't' && state.felled.ContainsKey($"{ux},{uy}"));
        BuildMap();
        Check("and it still doesn't when the map is rebuilt, as it is on loading", worldMap[uy, ux] != 't');
        Settings.Data.dayLength = 0;
        player.X = 160; player.Y = 115;
        ClearSkies();
        state.clock = 19.5f * 60; yield return 5;
        pendingShot = "12a-dusk"; yield return 2;
        state.clock = 6 * 60; yield return 5;
        pendingShot = "12b-dawn"; yield return 2;
        SetNight(false);

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
        state.weather = "rain"; SnapWeather(); player.X = 160; player.Y = 115; yield return 30;
        pendingShot = "31-rain"; yield return 2;
        Check("rain makes fish bite faster", WeatherBite < 1);
        state.weather = "storm"; SnapWeather(); flash = 1; yield return 2;
        pendingShot = "32-storm"; yield return 2;
        var bridgeTile = bridgeSet.First(b => b.Item2 > 18 && b.Item1 == 16);
        Check("a storm closes the bridges", !CanStand(bridgeTile.Item1 * T + 5, bridgeTile.Item2 * T + 7));
        Check("but not the jetty", CanStand(SaltJettyX, SaltJettyY + 1));
        player.X = 480; player.Y = 150; yield return 3;
        Note($"in a storm away from everything, the prompt is: {prompt.Text}");
        Check("you can always wait out a storm", target?.Type == "rest");
        state.weather = "clear";

        // The forecast moves the weather along as the clock runs: rain, a warning, then a storm
        ClearSkies();
        SetNight(false);
        state.clock = 9 * 60;
        state.forecast = new() { new() { at = 0, w = "clear" }, new() { at = 200, w = "rain" }, new() { at = 260, w = "storm" }, new() { at = 400, w = "clear" } };
        Advance(30, quiet: false);
        Check($"rain comes when the forecast says ({state.weather}: {toastMsg})", state.weather == "rain" && toastMsg.StartsWith("It's starting to rain"));
        Advance(30, quiet: false);
        Check($"with half an hour's warning before a storm ({toastMsg})", state.weather == "rain" && toastMsg.Contains("A storm is coming"));
        // Rain that starts only half an hour before the storm still gets the warning, in the same message
        state.clock = 9 * 60;
        state.forecast = new() { new() { at = 0, w = "clear" }, new() { at = 200, w = "rain" }, new() { at = 230, w = "storm" }, new() { at = 400, w = "clear" } };
        state.weather = planned = "clear"; warnedStorm = -1;
        Advance(25, quiet: false);
        Check($"and when the rain only starts half an hour before ({toastMsg})", toastMsg.StartsWith("It's starting to rain") && toastMsg.Contains("A storm is coming"));
        state.clock = 10 * 60;
        state.forecast = new() { new() { at = 0, w = "clear" }, new() { at = 200, w = "rain" }, new() { at = 260, w = "storm" }, new() { at = 400, w = "clear" } };
        planned = state.weather = "rain"; warnedStorm = 2;
        // Caught halfway across a bridge by the storm: you can walk off it, but not back on.
        var span = bridgeSet.Where(b => b.Item1 == 16 && b.Item2 > 10).OrderBy(b => b.Item2).ToList();
        var mid = span[span.Count / 2];
        player.X = mid.Item1 * T + 5; player.Y = mid.Item2 * T + 7; player.Face = "down";
        Advance(25, quiet: false);
        SnapWeather();
        Check($"the storm arrives on time ({state.weather}: {toastMsg})", Stormy && toastMsg.StartsWith(Season == "habagat" ? "A bagyo is raging" : "A storm is raging"));
        float yOnBridge = player.Y;
        Inp.Hold(KeyboardKey.Down, true); yield return 40; Inp.Hold(KeyboardKey.Down, false); yield return 2;
        Check($"you can still walk off a bridge the storm caught you on (moved {player.Y - yOnBridge:0}px)", player.Y > yOnBridge + 15);
        var top = span[0];
        player.X = top.Item1 * T + 5; player.Y = top.Item2 * T - 6; yield return 2;
        Check("but you can't step onto one", !OnBridge() && CanStand(player.X, player.Y) && !CanStand(top.Item1 * T + 5, top.Item2 * T + 7));
        pendingShot = "32b-storm-forecast"; yield return 2;
        // Sheltering waits until the storm has passed, the same day.
        int dayS = state.day;
        player.X = 480; player.Y = 150; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check($"sheltering waits out the storm ({ClockText(state.clock)}, {state.weather}, day {state.day})",
            (int)state.clock == 6 * 60 + 400 && state.weather == "clear" && state.day == dayS && toastMsg.StartsWith("You wait out the storm"));
        // Each morning's forecast: well formed, storms only out of rain and only from day 3. Every other day in the
        // sample is in the habagat (days 6-10, 16-20...) and the rest in the amihan (Seasons.cs).
        int keepDayW = state.day, stormDays = 0, wetDays = 0;
        bool plansOk = true, earlyStorm = false;
        for (int i = 0; i < 2000; i++)
        {
            state.day = 10 + i % 2 * 5;
            RollWeather();
            var p = state.forecast;
            var pairs = p.Zip(p.Skip(1)).ToList();
            plansOk &= p[0].at == 0 && p.All(s => s.w is "clear" or "rain" or "storm" && s.at < 1440)
                && pairs.All(z => z.First.at < z.Second.at && z.First.w != z.Second.w && (z.Second.w != "storm" || z.First.w == "rain"));
            if (p.Any(s => s.w == "storm")) stormDays++; else if (p.Any(s => s.w == "rain")) wetDays++;
        }
        state.day = 2;
        for (int i = 0; i < 300; i++) earlyStorm |= MakeForecast(1).Any(s => s.w == "storm") || MakeForecast(2).Any(s => s.w == "storm");
        Check($"forecasts are well formed ({stormDays / 20.0:0}% storm days, {wetDays / 20.0:0}% rain without a storm, none before day 3)",
            plansOk && !earlyStorm && stormDays > 180 && stormDays < 420 && wetDays > 500);
        state.day = keepDayW;
        ClearSkies();
        SetNight(false);

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
        SetNight(false); state.weather = "clear";
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
        SetNight(true);
        while (MoonPhase != 4) state.day++;
        Check($"the moon carp bites on a full-moon night (day {state.day})", FullMoon && FishWeights("lagoon", null, 1).Any(p => p.f.Id == "moon_carp"));
        yield return 15;
        pendingShot = "41-full-moon"; yield return 2;
        state.day++;
        Check("but not on other nights", !FullMoon && !FishWeights("lagoon", null, 1).Any(p => p.f.Id == "moon_carp"));
        state.day = keepDay; SetNight(false);
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
        // Counted from what you had, since a sunken chest earlier in the run can already have given you chum.
        int chum0 = Has("chum");
        Give("chum", 2); yield return 2;
        Check($"a fishing spot offers chum (prompt: {prompt.Text})", target?.AltType == "chum");
        Inp.Tap(KeyboardKey.F); yield return 2;
        Check($"F throws chum and the fish crowd in ({chum0 + 2} -> {Has("chum")})", Chummed("coral") && Has("chum") == chum0 + 1);
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
        Inp.Hold(KeyboardKey.Up, true);
        for (int steps = 0; steps < 60 && !InWater; steps++) yield return 1;
        Inp.Hold(KeyboardKey.Up, false); yield return 2;
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
        // (They wait on the sorting tray, out of the bag, until you keep them: 1.17.)
        Check($"hauling the pot brings up crabs ({toastMsg})", Has("shore_crab") + (sorting?.Count ?? 0) > crabs0 && potB != null && !PotReady(potB));
        // They're on the sorting tray (1.17): closing it sorts them the safe way.
        if (mode == "panel" && panel == "sort") { Inp.Tap(KeyboardKey.Escape); yield return 3; }
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
        logPage = Data.Biomes.Length; yield return 10;
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
        string oldSave = Path.Combine(testDir, "old-save.json");
        File.WriteAllText(oldSave, "{\"caught\":[\"glowgill\"],\"commons\":{\"pond_perch\":3},\"night\":false,\"casts\":7,\"flags\":{\"metTomas\":true},"
            + "\"pity\":{},\"hinted\":{},\"px\":160,\"py\":115,\"mats\":{\"wood\":12,\"stone\":5},\"builds\":[{\"id\":\"campfire\",\"x\":17,\"y\":11}],\"loose\":[]}");
        var old = SaveFile.Load(oldSave);
        Check("an old save moves wood and stone into the bag and asks for a fisher",
            old != null && old.inv.GetValueOrDefault("wood") == 12 && old.inv.GetValueOrDefault("stone") == 5 && old.inv.ContainsKey("rod_old")
            && !old.created && old.food == 100 && old.builds.Count == 1 && old.scene == "world");
        Check($"a save from before the clock starts at 8 AM, keeping its weather all day ({old?.clock})",
            old != null && old.clock == 8 * 60 && old.forecast.Count == 1 && old.forecast[0].w == "clear" && old.forecast[0].at == 0);
        File.WriteAllText(oldSave, "{\"night\":true,\"day\":6,\"weather\":\"rain\",\"created\":true}");
        var oldNight = SaveFile.Load(oldSave);
        Check($"and one saved at night starts at 9 PM, still raining ({oldNight?.clock})",
            oldNight != null && oldNight.clock == 21 * 60 && oldNight.weather == "rain" && oldNight.forecast[0].w == "rain" && oldNight.day == 6);

        // ---------- Starfall Atoll, the Starwell and Tidemane ----------
        SetNight(false); state.weather = "clear"; mode = "play"; boss = null; fish = null; reel = null;
        state.tackle["bait"] = "auto"; iframes = 0; state.hp = 100; state.food = 90;
        int atollLand = 0;
        for (int yy = 0; yy < ROWS; yy++)
            for (int xx = 0; xx < COLS; xx++)
                if (biome[yy, xx] == 4 && Land.Contains(worldMap[yy, xx])) atollLand++;
        Check($"Starfall Atoll is a big island now ({atollLand} land tiles)", atollLand > 500);
        Check("waders can't wade across to the atoll", !WadeReach(160, 115).Any(t => biome[t.Item2, t.Item1] == 4 && Land.Contains(worldMap[t.Item2, t.Item1])));
        player.X = AtollJettyX + 4; player.Y = AtollJettyY + 1; player.Face = "right"; yield return 3;
        var fromJetty = Reachable();
        Check("the Starwell glade can be walked to from the jetty", fromJetty.Contains(((int)(StarwellX / T) - 4, (int)(StarwellY / T))));
        int ringPalms = trees.Count(tr => tr.kind == 'h' && GladeDist(tr.x, tr.y) >= GladeR && GladeDist(tr.x, tr.y) < PalmRingR);
        Check($"a ring of palms hides it ({ringPalms} palms) and hoofprints lead there ({hoofprints.Count} tiles)", ringPalms >= 30 && hoofprints.Count >= 12);
        Check("the Starwell starts out secret: not on the map, ??? in the Fish log", !StarwellFound && LogGroups(4).Any(g => g.head == "???"));
        yield return 10;
        pendingShot = "62-atoll-jetty"; yield return 2;
        player.X = StarwellX - 9 * T; player.Y = StarwellY + 3; player.Face = "right"; yield return 2;
        Inp.Hold(KeyboardKey.Right, true); yield return 45; Inp.Hold(KeyboardKey.Right, false); yield return 3;
        Check($"walking in through the gap in the palms finds the Starwell ({toastMsg})", StarwellFound && toastMsg.Contains("Starwell"));
        pendingShot = "63-starwell"; yield return 2;
        player.X = CarvingX; player.Y = CarvingY + 10; player.Face = "up"; yield return 3;
        Check($"there's a carving by the pool (prompt: {prompt.Text})", target?.Type == "carving");
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check("the carving gives the secret away", mode == "dialogue" && dlg.Lines.Any(l => l.T.Contains("coconut")) && dlg.Lines.Any(l => l.T.Contains("night") || l.T.Contains("moon")));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }

        // What bites at the Starwell
        var (wsx, wsy) = StandNear("starwell");
        player.X = wsx; player.Y = wsy; yield return 3;
        Check($"the Starwell can be fished (prompt: {prompt.Text})", target?.Type == "spot" && target.Id == "starwell");
        fish = new FishCast { Spot = "starwell", Bait = "coconut" };
        Check("nothing strange bites there by day", Enumerable.Range(0, 300).All(_ => !RollCatch("starwell").Boss));
        SetNight(true); fish.Bait = "worm";
        Check("nor at night on any other bait", Enumerable.Range(0, 300).All(_ => !RollCatch("starwell").Boss));
        fish.Bait = "coconut";
        Check("but at night, a coconut brings up something enormous", RollCatch("starwell").Boss);
        fish = null;
        Give("coconut", 4); state.tackle["bait"] = "coconut";
        var wellOdds = CatchOdds("starwell", 1);
        Check($"the fish finder knows ({wellOdds[0].name}, {wellOdds[0].pct:0.00})", wellOdds.Count == 1 && wellOdds[0].id == "tidemane");

        // On the line: it fights every way a fish can, switching every few seconds
        int coco = Has("coconut");
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"the cast uses a coconut ({coco} -> {Has("coconut")})", fish?.Bait == "coconut" && Has("coconut") == coco - 1);
        for (int i = 0; i < 600 && mode is "casting" or "waiting"; i++) yield return 1;
        Check("something bites", mode == "bite" && fish.Roll.Boss);
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("hooking it starts a long, hard fight", mode == "reeling" && BossOnLine && reel.Pull < 1);
        yield return 20;
        pendingShot = "64-boss-on-the-line"; yield return 2;
        string style0 = reel.Style;
        reel.Running = 0; reel.Leap = 0; reel.StyleT = 0.01f; yield return 2;
        Check($"it switches fighting style ({style0} -> {reel.Style})", reel.Style != style0);
        reel.Progress = 0.8f; yield return 50;
        Check($"the fight has its own tune ({Music.Current})", Music.Current == "boss");
        reel.Progress = 0.01f; reel.FishY = 0; reel.ZoneY = Bar - reel.ZoneH; reel.Running = 0; reel.Leap = 0; reel.LeapT = 99; reel.RunT = 99; reel.StyleT = 99;
        Inp.Hold(KeyboardKey.Space, false);
        for (int i = 0; i < 30 && mode == "reeling"; i++) yield return 1;
        Check($"losing it sends it back down for another night ({toastMsg})", mode == "play" && toastMsg.Contains("Starwell") && boss == null);

        // Landing it brings it out onto the sand
        player.X = wsx; player.Y = wsy;
        Inp.Tap(KeyboardKey.E); yield return 2;
        for (int i = 0; i < 600 && mode is "casting" or "waiting"; i++) yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 2;
        reel.Progress = 1.2f; reel.Running = 0; reel.Leap = 0; reel.StyleT = 99; yield return 2;
        Check($"landing it brings it bursting out of the pool ({boss?.Phase})", boss != null && boss.Phase == "emerge" && mode == "play" && !BossOnLine);
        iframes = 30;
        yield return 18;
        pendingShot = "65-boss-emerges"; yield return 2;
        for (int i = 0; i < 60 && boss.Phase == "emerge"; i++) yield return 1;
        Check($"it lands on the sand and starts circling ({boss.Phase})", boss.Phase == "stalk" && BossCanBe(boss.X, boss.Y));
        Check($"mid-fight the only thing to do is fight (prompt: {prompt.Text})", target == null || target.Type is "boss" or "info");

        // A charge that hits you hurts, and leaves it winded
        player.X = StarwellX; player.Y = StarwellY - 45; player.Face = "left";
        boss.X = StarwellX - 34; boss.Y = player.Y; boss.Lift = 0;
        boss.Phase = "charge"; boss.T = 0; boss.Ran = 0; boss.Hit = false; boss.AimX = 1; boss.AimY = 0; boss.Dir = 1;
        state.hp = 100; iframes = 0;
        yield return 6;
        pendingShot = "66-boss-charge"; yield return 2;
        for (int i = 0; i < 90 && boss.Phase == "charge"; i++) yield return 1;
        Check($"a charge that hits you hurts ({state.hp:0} health)", state.hp < 100 && state.hp >= 100 - ChargeHurt - 0.1f);
        Check($"after a charge it's winded ({boss.Phase})", boss.Phase == "winded");

        // Blows: big ones while it's winded, then a moment where it shrugs them off
        iframes = 30;
        BossBeside();
        yield return 2;
        Check($"you can strike it up close (prompt: {prompt.Text})", target?.Type == "boss");
        float sp0 = boss.Spirit;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"a blow while it's winded tires it out a lot ({sp0:0} -> {boss.Spirit:0})", boss.Spirit <= sp0 - 10);
        sp0 = boss.Spirit; swingT = 0;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("it shrugs off another blow straight after", boss.Spirit == sp0);
        pendingShot = "67-boss-winded"; yield return 2;

        // The stomp: the shockwave knocks you over at a distance, but not right under its hooves
        state.hp = 100; iframes = 0;
        BossBeside();
        boss.Phase = "stompRear"; boss.T = 0.59f; boss.Hit = false;
        player.X = boss.X + 40; yield return 16;
        pendingShot = "68-boss-stomp"; yield return 2;
        yield return 25;
        Check($"the stomp's shockwave hurts ({state.hp:0} health)", state.hp < 100);
        state.hp = 100; iframes = 0;
        BossBeside();
        boss.Phase = "stompRear"; boss.T = 0.59f; boss.Hit = false;
        player.X = boss.X + 5; yield return 50;
        Check($"but right under its hooves you're safe ({state.hp:0} health)", state.hp == 100);

        // Into the pool and back out, spitting water
        iframes = 30;
        BossBeside();
        StartDive(boss);
        for (int i = 0; i < 300 && boss.Phase != "stalk"; i++) yield return 1;
        Check($"it dives, then bursts out spitting water bolts ({bolts.Count})", boss.Phase == "stalk" && bolts.Count >= 3);
        pendingShot = "69-boss-bolts"; yield return 2;
        state.hp = 100; iframes = 0;
        bolts.Clear();
        bolts.Add(new Bolt { X = player.X - 20, Y = player.Y - 5, Vx = 85, Vy = 0, Life = 2 });
        yield return 20;
        Check($"a water bolt hurts ({state.hp:0} health)", state.hp < 100);

        // Knocked flat: you come round on the jetty, and it's gone back under
        state.hp = 3; iframes = 0;
        bolts.Add(new Bolt { X = player.X - 20, Y = player.Y - 5, Vx = 85, Vy = 0, Life = 2 });
        for (int i = 0; i < 200 && !(mode == "play" && boss == null && state.hp == 35); i++) yield return 1;
        Check($"fainting wakes you on the atoll's jetty ({player.X:0},{player.Y:0}, {state.hp:0} health)",
            boss == null && state.hp == 35 && Dist(player.X, player.Y, AtollJettyX + 8, AtollJettyY + 1) < 4 && !state.tamed);

        // Worn right out, it calms down and becomes yours
        state.hp = 100;
        player.X = wsx; player.Y = wsy;
        BossBreach(); iframes = 30;
        for (int i = 0; i < 60 && boss.Phase == "emerge"; i++) yield return 1;
        BossBeside(3); yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"worn out, it lies down on the sand ({boss?.Phase})", boss?.Phase == "calm" && mode == "dialogue");
        yield return 30;
        pendingShot = "70-boss-calm"; yield return 2;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check("then it's yours", mode == "tamed" && state.tamed && boss == null);
        yield return 45;
        pendingShot = "71-tamed-card"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"closing the card explains riding ({toastMsg})", mode == "play" && toastMsg.Contains("R to ride"));
        state.tackle["bait"] = "auto";
        Check("it won't bite again once it's yours", !TidemaneBites("coconut"));

        // Riding: faster on land, and it swims
        SetNight(false); iframes = 0;
        player.X = StarwellX - 30; player.Y = StarwellY - 40; player.Face = "right";
        state.mountX = player.X + 10; state.mountY = player.Y; yield return 2;
        Check($"you can climb on (prompt: {prompt.Text})", target?.Type == "ride");
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("R puts you in the saddle", Riding);
        float rx0 = player.X;
        Inp.Hold(KeyboardKey.Right, true); yield return 30; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"riding is much faster than walking ({player.X - rx0:0} px in half a second)", player.X - rx0 > 36);
        pendingShot = "72-riding"; yield return 2;
        player.X = AtollJettyX + 4; player.Y = AtollJettyY + 1; player.Face = "left"; yield return 2;
        Inp.Hold(KeyboardKey.Left, true);
        // Frame rate varies during screenshot capture. Stop in the channel, before reaching the Dunes.
        for (int steps = 0; steps < 60 && player.X > 862; steps++) yield return 1;
        Inp.Hold(KeyboardKey.Left, false); yield return 3;
        Check($"it swims out to sea ({player.X:0},{player.Y:0}, {AreaName()})", Swimming && player.X < AtollJettyX - 20 && AreaName() == "Open sea");
        yield return 10;
        pendingShot = "73-swimming"; yield return 2;
        // Swimming along (bow wave and wake), then back to this spot out at sea for the checks below.
        var (seaX, seaY) = (player.X, player.Y);
        Inp.Hold(KeyboardKey.Left, true); yield return 12;
        pendingShot = "73a-swimming-moving"; yield return 2;
        Inp.Hold(KeyboardKey.Left, false); yield return 2;
        // Half in, half out at the Starwell's edge: the water only covers the part of it that's over water.
        player.X = StarwellX - 24; player.Y = StarwellY; player.Face = "right"; yield return 10;
        Check("it swims right up to the edge of the Starwell", Swimming);
        pendingShot = "73b-swimming-at-shore"; yield return 2;
        player.X = seaX; player.Y = seaY; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check($"you can't hop off at sea ({toastMsg})", Riding && toastMsg.Contains("Nowhere dry"));
        state.weather = "storm";
        Check("caught out at sea by a storm, it still swims you home", CanStand(870, 268, false, true));
        player.X = AtollJettyX + 4;
        Check("but from the shore it won't head out into deep water in a storm", !CanStand(870, 268, false, true));
        // And it really gets there: climbing out, its back end is still over the deep for a few steps.
        player.X = 870; player.Y = 268; player.Face = "right"; yield return 2;
        Inp.Hold(KeyboardKey.Right, true); yield return 40; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"in a storm it swims you all the way up onto the jetty ({player.X:0},{player.Y:0})", Riding && player.X > AtollJettyX);
        Inp.Hold(KeyboardKey.Left, true); yield return 30; Inp.Hold(KeyboardKey.Left, false); yield return 2;
        Check($"and won't take you back out into the deep ({player.X:0},{player.Y:0})", Riding && TileUnder(player.X, player.Y) != '~');
        player.X = AtollJettyX + 4;
        state.weather = "clear";
        Check("it will in fair weather", CanStand(870, 268, false, true) && !CanStand(870, 268));
        player.X = 975; player.Y = 268; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check($"on dry land R hops you off and it waits there ({state.mountX:0},{state.mountY:0})", !Riding && Dist(state.mountX, state.mountY, 975, 268) < 1 && CanStand(player.X, player.Y));
        yield return 10;
        pendingShot = "74-tidemane-waiting"; yield return 2;
        player.X = StarwellX; player.Y = StarwellY - 45; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check("from far away, R whistles it over", Riding && Dist(state.mountX, state.mountY, player.X, player.Y) < 1);
        player.X = 160; player.Y = 74; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Check("going indoors leaves it waiting at the door", scene == "house:tomas" && !state.riding && Dist(state.mountX, state.mountY, 160, 74) < 1);
        player.X = RoomDoorWX; player.Y = RoomDoorWY - 6; player.Face = "down"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 70;
        Save();
        var rideSave = SaveFile.Read();
        Check("Tidemane, riding and the Starwell are saved", rideSave != null && rideSave.tamed && !rideSave.riding && rideSave.hinted.ContainsKey("starwell")
            && Math.Abs(rideSave.mountX - 160) < 1);

        // The map, the Fish log and the legends page
        mapTexDirty = true;
        Inp.Tap(KeyboardKey.Tab); yield return 10;
        pendingShot = "75-map-atoll"; yield return 2;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Inp.Tap(KeyboardKey.J); yield return 3;
        dexTab = "log"; logPage = 4; yield return 15;
        pendingShot = "76-fishlog-atoll"; yield return 2;
        logPage = Data.Biomes.Length; yield return 10;
        pendingShot = "77-fishlog-legends"; yield return 2;
        logPage = 0; dexTab = "creatures";
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        // Anything built on the old, smaller atoll's shore (now open sea) comes back to the bag
        state.builds.Add(new Build { id = "campfire", x = 86, y = 21 });
        int woodR = Has("wood"), stoneR = Has("stone");
        int rescued = RescueSunkBuilds();
        Check($"pieces on the old atoll's shore come back to the bag ({rescued})", rescued == 1 && Has("wood") == woodR + 3 && Has("stone") == stoneR + 2);
        Check("and nothing else is touched", RescueSunkBuilds() == 0);

        // ---------- Tomorrow's forecast, opening hours and bedtime ----------
        ClearSkies();
        state.clock = 10 * 60; state.toldDay = 0; mode = "play";
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Check($"by day Tomas is at his camp ({target?.Label})", target?.Type == "npc" && !TomasInBed);
        Inp.Tap(KeyboardKey.E); yield return 5;
        bool toldWeather = dlg != null && dlg.Lines.Any(l => l.T.StartsWith("Tomorrow? My old knee says"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check($"Tomas tells you tomorrow's weather ({DescribeDay(state.tomorrow)})", toldWeather && KnowTomorrow);
        var promised = state.tomorrow.Select(s => (s.at, s.w)).ToList();
        string said = DescribeDay(state.tomorrow);
        Save();
        LoadSlot(SaveFile.Slot); yield return 10;
        Check($"tomorrow's forecast survives saving and loading, spell by spell ({promised.Count})", state.tomorrow.Select(s => (s.at, s.w)).SequenceEqual(promised) && KnowTomorrow);
        int dayF = state.day;
        SkipTo(7 * 60);
        Check($"and it comes true ({said} -> {DescribeDay(state.forecast)})", state.day == dayF + 1 && state.forecast.Select(s => (s.at, s.w)).SequenceEqual(promised) && DescribeDay(state.forecast) == said && !KnowTomorrow);
        Check($"with the next day already rolled ({DescribeDay(state.tomorrow)})", state.tomorrow != null && !ReferenceEquals(state.tomorrow, state.forecast));
        ClearSkies();
        // Night: Pip's stall shuts, Tomas goes to bed, and you can still wake him.
        state.clock = 23 * 60; yield return 2;
        player.X = PipX; player.Y = PipY + 15; player.Face = "up"; yield return 3;
        Check($"Pip's stall is closed at night ({target?.Label})", target?.Type == "info" && !PipOpen);
        pendingShot = "80-stall-closed"; yield return 2;
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Check($"Tomas has gone to bed ({target?.Label})", target?.Type == "info" && TomasInBed);
        Check("his spot isn't solid while he's away", CanStand(TomasHomeX, TomasHomeY));
        int bedTx = (int)(TomasHomeX / T), bedTy = (int)(TomasHomeY / T);
        Check($"and nothing can be built on it ({PlaceProblem(Data.BuildById["fence"], bedTx, bedTy)})", PlaceProblem(Data.BuildById["fence"], bedTx, bedTy) == "Keep Tomas's spot clear");
        player.X = 160; player.Y = 74; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 70;
        player.X = TomasBedX; player.Y = TomasBedY + 3; player.Face = "up"; yield return 3;
        Check($"in his hut, he's asleep in bed ({target?.Label})", scene == "house:tomas" && target?.Type == "npc" && target.Label == "Wake Tomas");
        player.X = 60; player.Y = 60; yield return 2;
        pendingShot = "81-tomas-asleep"; yield return 2;
        player.X = TomasBedX; player.Y = TomasBedY + 3; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check($"waking him gets a sleepy hello ({dlg?.Full})", mode == "dialogue" && dlg.Full.StartsWith("Mm?"));
        Inp.Tap(KeyboardKey.E); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 5;
        Check("then he talks as usual", mode == "dialogue" && dlg.Lines[0].S == "Tomas" && !dlg.Full.StartsWith("Mm?"));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        player.X = RoomDoorWX; player.Y = RoomDoorWY - 6; player.Face = "down"; yield return 2;
        Inp.Tap(KeyboardKey.E); yield return 70;
        // Morning: he's back at his camp, even if you were standing on his spot (you can step off it).
        player.X = TomasHomeX; player.Y = TomasHomeY + 1;
        state.clock = 7 * 60; yield return 3;
        Check("in the morning Pip opens and Tomas is up", PipOpen && !TomasInBed && scene == "world");
        float stuckY = player.Y;
        Inp.Hold(KeyboardKey.Down, true); yield return 25; Inp.Hold(KeyboardKey.Down, false); yield return 2;
        Check($"and you can step off his spot if you were on it (moved {player.Y - stuckY:0}px)", player.Y > stuckY + 8);
        tomasX = 270; tomasY = 95; state.clock = 23 * 60;
        Check("on the dock for the ending, Tomas isn't in bed", !TomasInBed);
        tomasX = TomasHomeX; tomasY = TomasHomeY;

        // ---------- Birds and crickets ----------
        player.X = 160; player.Y = 115; ClearSkies();
        state.clock = 6.5f * 60; yield return 3;
        Check($"birds sing at dawn ({Music.NatureWanted})", Music.NatureWanted == "birds");
        state.clock = 23 * 60; yield return 3;
        Check($"crickets at night ({Music.NatureWanted})", Music.NatureWanted == "crickets");
        float calm = Music.NatureVolume;
        state.weather = "storm"; SnapWeather(); yield return 3;
        Check($"and they go quiet in a storm ({calm:0.00} -> {Music.NatureVolume:0.00})", Music.NatureVolume < 0.05f && calm > 0.3f);
        ClearSkies(); state.clock = 13 * 60;

        // ---------- Pausing in the background ----------
        Settings.Data.pauseUnfocused = true;
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 3;
        TestBite("lagoon", "pond_perch"); yield return 1;
        Inp.ScriptFocused = false; yield return 2;
        Check($"switching away mid-bite pauses ({mode}, from {pausedFrom})", mode == "pause" && pausedFrom == "bite");
        float biteLeft = fish.BiteT; yield return 30;
        Check("and the bite waits", fish != null && fish.BiteT == biteLeft);
        Inp.ScriptFocused = true;
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Check($"resuming goes back to the bite ({mode})", mode == "bite");
        Inp.Hold(KeyboardKey.E, true); yield return 1;
        bool reelsAtOnce = ReelHeld();
        Inp.Hold(KeyboardKey.E, false); yield return 1;
        bool letGo = !ReelHeld();
        Inp.Hold(KeyboardKey.E, true); yield return 1;
        Check("a button still held from clicking Resume doesn't reel until it's let go", !reelsAtOnce && letGo && ReelHeld());
        Inp.Hold(KeyboardKey.E, false);
        fish = null; reel = null; mode = "play"; yield return 2;
        Inp.Hold(KeyboardKey.E, true); yield return 3;
        bool charging = mode == "charging";
        Inp.ScriptFocused = false; yield return 2;
        Inp.ScriptFocused = true; Inp.Hold(KeyboardKey.E, false);
        Check($"a power cast being held is dropped rather than thrown ({mode}, from {pausedFrom})", charging && mode == "pause" && pausedFrom == "play" && fish == null);
        Inp.Tap(KeyboardKey.Escape); yield return 2;
        Settings.Data.pauseUnfocused = false;
        mode = "play"; fish = null;

        // ---------- Gamepad ----------
        player.X = 146; player.Y = 96; player.Face = "up"; yield return 3;
        Inp.TapPad(GamepadButton.RightFaceDown); yield return 2;
        Check($"A on a gamepad talks to Tomas ({mode})", mode == "dialogue" && Inp.UsingPad);
        while (mode == "dialogue") { Inp.TapPad(GamepadButton.RightFaceDown); yield return 3; }
        yield return 2;
        Check($"prompts name the gamepad's buttons ({prompt.Key}: {prompt.Text})", prompt.Key == "A");
        float padX = player.X;
        Inp.ScriptStick = new System.Numerics.Vector2(1, 0); yield return 20; Inp.ScriptStick = null; yield return 2;
        Check($"the left stick walks (moved {player.X - padX:0}px)", player.X > padX + 10);
        Inp.TapPad(GamepadButton.RightFaceUp); yield return 3;
        Check("Y opens the bag", mode == "panel" && panel == "bag");
        Inp.TapPad(GamepadButton.RightFaceRight); yield return 3;
        Check("B closes it", mode == "play");
        player.X = 140; player.Y = 135; yield return 2;
        Inp.TapPad(GamepadButton.LeftTrigger2); yield return 3;
        string tool0 = buildTool;
        Inp.TapPad(GamepadButton.RightTrigger1); yield return 3;
        Check($"LT builds and RB picks the next piece ({tool0} -> {buildTool})", mode == "build" && buildTool != tool0);
        Inp.TapPad(GamepadButton.RightFaceRight); yield return 3;
        Check("B leaves build mode", mode == "play");
        TestBite("lagoon", "pond_perch"); yield return 1;
        Inp.TapPad(GamepadButton.MiddleRight); yield return 3;
        Check($"Start pauses even mid-bite ({mode}, from {pausedFrom})", mode == "pause" && pausedFrom == "bite");
        Inp.TapPad(GamepadButton.MiddleRight); yield return 3;
        Check($"and Start again goes back to it ({mode})", mode == "bite");
        fish = null; mode = "play"; yield return 2;
        Inp.TapPad(GamepadButton.MiddleRight); yield return 3;
        Check("Start opens the menu", mode == "pause");
        // In menus the stick moves a pointer and A clicks: here, on Resume.
        Inp.ScriptMouse = null;
        Inp.ScriptStick = new System.Numerics.Vector2(0.9f, -0.9f); yield return 4; Inp.ScriptStick = null;
        bool pointerMoved = Inp.PadCursor != null;
        Inp.PadCursor = new System.Numerics.Vector2(160 + 960 - 28 - SmallW("Resume") / 2, (720 - 610) / 2 + 28 + 22); yield return 2;
        pendingShot = "82-pad-pointer"; yield return 2;
        Inp.TapPad(GamepadButton.RightFaceDown); yield return 3;
        Check($"the stick moves a pointer in menus, and A clicks Resume ({mode})", pointerMoved && mode == "play");
        Inp.ScriptMouse = Offscreen; Inp.PadCursor = null;
        Inp.Tap(KeyboardKey.Escape); yield return 3; Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("the keyboard takes over again as soon as it's used", !Inp.UsingPad && mode == "play");
        yield return 2;

        // ---------- Save slots ----------
        Save();
        int robinDay = state.day;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        QuitToTitle(); yield return 10;
        Check("Quit to title goes back to the title with the game saved", mode == "title" && SaveFile.Read(1)?.look.name == "Robin");
        pendingShot = "78-title-saves"; yield return 2;
        // Clicked for real, as a player would (opening the slot list mid-draw once crashed the game).
        bool found = ClickButton("Load game"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 5;
        Check($"clicking Load game opens the slot list ({titleView})", found && mode == "title" && titleView == "slots" && slotsFor == "load");
        pendingShot = "79-slots"; yield return 2;
        found = ClickButton("Back"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"and Back returns to the title ({titleView})", found && titleView == "main");
        ClickButton("Load game"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 3;
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check("Esc goes back from the slot list", titleView == "main");
        // With every slot in use, New game asks which one to give up (that path opened the list mid-draw too)
        CopySlot(1); CopySlot(1); yield return 3;
        found = ClickButton("New game"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"with every slot full, New game asks which to replace ({titleView}, {slotsFor})", found && mode == "title" && titleView == "slots" && slotsFor == "new");
        pendingShot = "79b-slots-full"; yield return 2;
        ClickButton("Back"); yield return 3;
        ClickButton("Load game"); yield return 3;
        ClickButton("Delete"); yield return 3;
        found = ClickButton("Yes, delete"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"deleting through the slot list empties that slot and redraws the list", found && !SaveFile.Exists(3) && SaveFile.Exists(2) && titleView == "slots" && slotInfo != null && slotInfo[3] == null);
        SaveFile.Clear(2); slotInfo = null;
        ClickButton("Back"); yield return 3;
        Inp.ScriptMouse = Offscreen; yield return 3;
        TitleNew(); yield return 5;
        Check($"a new game goes in the first empty slot (slot {newSlot})", mode == "create" && newSlot == 2);
        foreach (char c in "Sam") Inp.ScriptChars.Enqueue(c);
        yield return 3;
        Inp.Tap(KeyboardKey.Enter); yield return 10;
        Check($"the new fisher starts day 1 at 8 AM in slot 2 ({state.look.name}, day {state.day}, {ClockText(state.clock)})",
            SaveFile.Slot == 2 && state.look.name == "Sam" && state.day == 1 && (int)state.clock == 8 * 60 && SaveFile.Exists(2));
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
        var robin = SaveFile.Read(1);
        Check("and Robin's game in slot 1 is untouched", robin?.look.name == "Robin" && robin.day == robinDay && robin.tamed);
        QuitToTitle(); yield return 5;
        Check($"Continue now means the newest save (slot {SaveFile.Newest()})", SaveFile.Newest() == 2);
        // A save whose weather disagrees with its forecast takes the forecast's weather when it loads
        var r1 = SaveFile.Read(1);
        r1.weather = "clear"; r1.forecast = new() { new() { at = 0, w = "rain" } };
        SaveFile.Slot = 1; SaveFile.Write(r1); SaveFile.Slot = 2;
        LoadSlot(1); yield return 10;
        Check("loading slot 1 brings Robin back", SaveFile.Slot == 1 && state.look.name == "Robin" && state.day == robinDay && mode == "play" && state.tamed);
        Check($"and the weather follows the forecast on loading ({state.weather})", state.weather == "rain");
        SaveFile.Clear(2);
        Check("deleting a slot empties only that one", !SaveFile.Exists(2) && SaveFile.Exists(1));
        // Copy a slot, then give the copy its own name; Continue still means the original
        Save();
        QuitToTitle(); yield return 5;
        OpenSlots("load"); yield return 5;
        CopySlot(1); yield return 3;
        Check($"copying a slot fills the first empty one ({toastMsg})", SaveFile.Read(2)?.look.name == "Robin" && SaveFile.Read(2).day == robinDay && SaveFile.Newest() == 1);
        renameSlot = 2; renameText = "";
        foreach (char c in "Robin, backup") Inp.ScriptChars.Enqueue(c);
        yield return 3;
        Inp.Tap(KeyboardKey.Enter); yield return 5;
        var named = SaveFile.Read(2);
        Check($"renaming gives the slot its own name ({named?.slotName}), keeps the fisher's, and Continue stays put",
            renameSlot == 0 && named?.slotName == "Robin, backup" && named.look.name == "Robin" && SaveFile.Newest() == 1);
        yield return 5;
        pendingShot = "83-slots-copy"; yield return 2;
        TitleBack(); yield return 2;
        LoadSlot(1); yield return 10;
        SaveFile.Clear(2);
        // The single save.json from before slots becomes the first free slot
        string legacy = Environment.GetEnvironmentVariable("FESH_SAVE");
        File.WriteAllText(legacy, "{\"created\":true,\"day\":9,\"look\":{\"name\":\"Old\"}}");
        SaveFile.MigrateLegacy();
        Check("an old single save moves into the first free slot", !File.Exists(legacy) && SaveFile.Read(2)?.look.name == "Old" && SaveFile.Read(1)?.look.name == "Robin");
        SaveFile.Clear(2);

        // Ending screen
        foreach (int frames in AmihanScript()) yield return frames;
        foreach (int frames in DirectionScript()) yield return frames;
        foreach (int frames in SeasonScript()) yield return frames;
        foreach (int frames in GusoScript()) yield return frames;
        foreach (int frames in GuideScript()) yield return frames;
        foreach (int frames in EduScript()) yield return frames;
        foreach (int frames in AtlasScript()) yield return frames;
        foreach (int frames in ViewScript()) yield return frames;
        state.caught = Data.Creatures.Select(c => c.Id).ToList();
        endStats = $"Creatures found: 5 of 5. Common fish caught: 3. Casts: {state.casts}. Things built: {state.builds.Count}.";
        mode = "ending"; yield return 10;
        pendingShot = "11-end"; yield return 2;
    }
}
#endif
