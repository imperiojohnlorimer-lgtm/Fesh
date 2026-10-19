#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_YARD_TEST=1: the boatyard (BoatYard.cs): the slipway piece (only beside the sea, taken down for a refund), the five
// stages by real keys (a stage's materials handed over when work starts, holding <act> to hammer, letting go and Esc keeping
// the work, the stage saved before its animation, the animation skipped by a fresh press), a storm stopping work, pausing,
// the launch (the boat is yours and moored at Pip's jetty), the guide's goal and the workbench recipe gone, the new hull's
// pictures, with screenshots of each stage.
partial class Game
{
    IEnumerable<int> BoatYardScript()
    {
        Note("The boatyard");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hp = 100; state.inv["wood"] = 60; state.inv["stone"] = 10; state.inv["iron_bar"] = 8; state.inv["sailcloth"] = 2;
        var def = Data.BuildById["slipway"];
        Settings.Data.zoom = 5;   // closer, so the screenshots show the boat

        /* ---------- The piece ---------- */
        Check($"the slipway is a build piece: {def.CostText}", def.W == 3 && def.Cost["wood"] == 4 && def.Cost["stone"] == 2 && Data.OutdoorTools.Contains("slipway"));
        Check("the workbench no longer makes the boat", !RecipeKnown(Items.Recipes.First(r => r.Out == "boat")));
        Check("the old recipe is still there for old tests and saves", Items.Recipes.Any(r => r.Out == "boat"));
        int sx = -1, sy = -1, ix = -1, iy = -1;
        for (int ty = 17; ty >= 2 && sx < 0; ty--)
            for (int tx = 1; tx < 40 && sx < 0; tx++)
            {
                player.X = (tx + 1.5f) * T; player.Y = ty * T + 17;
                if (PlaceProblem(def, tx, ty) == "") { sx = tx; sy = ty; }
            }
        for (int ty = 2; ty < 18 && ix < 0; ty++)
            for (int tx = 1; tx < 40 && ix < 0; tx++)
            {
                player.X = (tx + 1.5f) * T; player.Y = ty * T + 17;
                if (PlaceProblem(def, tx, ty).StartsWith("Build it beside the sea")) { ix = tx; iy = ty; }
            }
        int wx = -1, wy = -1;
        for (int ty = 17; ty >= 2 && wx < 0; ty--)
            for (int tx = 1; tx < 40 && wx < 0; tx++)
            {
                player.X = (tx + 1.5f) * T; player.Y = ty * T + 17;
                if (PlaceProblem(def, tx, ty).StartsWith("The boat needs dry beach")) { wx = tx; wy = ty; }
            }
        Check($"right at the water's edge or on a jetty it's refused: the boat needs room ({wx},{wy})", wx >= 0);
        Check($"there's a place beside the sea to put one ({sx},{sy})", sx >= 0);
        Check($"and inland it's refused with a reason ({ix},{iy})", ix >= 0);
        Check($"the boat slides toward the water: face {YardFace(sx, sy)}", YardFace(sx, sy) != 0);

        /* ---------- Placing it by the real keys ---------- */
        player.X = (sx + 1.5f) * T; player.Y = sy * T + 17; player.Face = "up"; yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        int woodBefore = Has("wood"), stoneBefore = Has("stone");
        SelectTool("slipway"); hover = (sx, sy); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        var b = state.builds.FirstOrDefault(p => p.id == "slipway");
        Check($"E in build mode puts it up ({state.builds.Count(p => p.id == "slipway")})", b != null && b.x == sx && b.y == sy);
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check($"and wood and stone were paid (wood {woodBefore} -> {Has("wood")}, stone {stoneBefore} -> {Has("stone")})", Has("wood") == woodBefore - 4 && Has("stone") == stoneBefore - 2);
        pendingShot = "yard-00-slipway"; yield return 2;
        player.X = b.x * T + 15; player.Y = b.y * T + 20; player.Face = "up"; yield return 4;
        Check($"standing by it the target is the slipway ({target?.Type}, {target?.Label})", target?.Type is "yard" or "info" && target.Label.Contains("keel"));

        /* ---------- Stage 1: not enough, then paid ---------- */
        int wood0 = Has("wood"), iron0 = Has("iron_bar");
        state.inv["wood"] = 3; yield return 3;
        Check($"with too little it only says what's needed ({target?.Type}, {target?.Label})", target?.Type == "info" && target.Label.Contains("6 wood"));
        state.inv["wood"] = wood0; yield return 4;
        Check($"with it all it offers to build ({target?.Label})", target?.Type == "yard" && target.Label.Contains("keel and ribs") && target.Label.Contains("6 wood"));
        Inp.Hold(KeyboardKey.E, true); yield return 3;
        Check($"E hands the materials over and starts work ({mode})", mode == "boatwork" && Has("wood") == wood0 - 6 && Has("iron_bar") == iron0 - 2);
        var yard = YardOf(b);
        Check("the project remembers it's paid", yard != null && yard.paid && yard.stage == 0);
        // Hammer for about a second, then let go.
        foreach (var _ in LungUntil(() => yard.work < 1.0f, 4f)) yield return 1;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        float w1 = yard.work;
        pendingShot = "yard-01-work"; yield return 1;
        yield return 15;
        Check($"letting go pauses the work: {w1:0.00} s kept ({yard.work:0.00})", w1 >= 0.95f && w1 < 2.5f && MathF.Abs(yard.work - w1) < 0.001f && mode == "boatwork");
        Save();
        var saved = SaveFile.Read(SaveFile.Slot);
        Check("and a save in the middle of it keeps the paid stage and the work", saved.yards.TryGetValue($"{b.x},{b.y}", out var sv) && sv.paid && sv.stage == 0 && sv.work >= 0.95f);
        Inp.Tap(KeyboardKey.Escape); yield return 3;
        Check($"Esc puts the hammer down, the work is kept ({mode})", mode == "play" && yard.paid && yard.work >= 0.95f);
        yield return 3;
        Check($"and the target is to keep working ({target?.Label})", target?.Type == "yard" && target.Label.Contains("Keep working"));
        int wood1 = Has("wood");
        Inp.Hold(KeyboardKey.E, true); yield return 3;
        Check($"starting again costs nothing more ({mode}, {wood1} -> {Has("wood")})", mode == "boatwork" && Has("wood") == wood1);
        foreach (var _ in LungUntil(() => mode == "boatwork", 6f)) yield return 1;
        Check($"two and a half seconds of hammering finish the stage ({mode}, stage {yard.stage})", mode == "boatanim" && yard.stage == 1 && !yard.paid && yard.work == 0);
        saved = SaveFile.Read(SaveFile.Slot);
        Check("the stage was saved before its animation played", saved.yards[$"{b.x},{b.y}"].stage == 1);
        yield return 20;
        pendingShot = "yard-02-stage1-anim"; yield return 1;
        // Holding <act> through the end doesn't skip it: a fresh press does.
        yield return 5;
        Check($"the animation plays on while <act> is still held ({mode})", mode == "boatanim");
        Inp.Hold(KeyboardKey.E, false); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a fresh press skips it ({mode})", mode == "play");
        yield return 3;
        pendingShot = "yard-03-stage1"; yield return 1;

        /* ---------- Stages 2 to 5 ---------- */
        for (int s = 1; s < 5; s++)
        {
            string name = YardStages[s].name;
            var cost = YardStages[s].cost;
            var before = cost.ToDictionary(c => c.id, c => Has(c.id));
            yield return 4;
            Check($"stage {s + 1}: the {name} is offered ({target?.Label})", target?.Type == "yard" && target.Label.Contains(name));
            Inp.Hold(KeyboardKey.E, true); yield return 3;
            Check($"stage {s + 1}: its materials go in ({string.Join(", ", cost.Select(c => $"{c.n} {c.id}"))})", mode == "boatwork" && cost.All(c => Has(c.id) == before[c.id] - c.n));
            foreach (var _ in LungUntil(() => mode == "boatwork", 6f)) yield return 1;
            Inp.Hold(KeyboardKey.E, false); yield return 2;
            Check($"stage {s + 1} done ({yard.stage})", yard.stage == s + 1 && mode == "boatanim");
            yield return 18;
            pendingShot = $"yard-0{4 + s}-stage{s + 1}"; yield return 1;
            foreach (var _ in LungUntil(() => mode == "boatanim", 4f)) yield return 1;
            Check($"the animation ends by itself ({mode})", mode == "play");
            yield return 3;
        }
        Check($"all five stages are built ({yard.stage}), and the offer is to launch ({target?.Label})", yard.stage == 5 && target?.Type == "yard" && target.Label.Contains("Launch"));
        Check("none of the materials were left over", Has("sailcloth") == 1);

        /* ---------- Storm and pause (on a second project later); launch ---------- */
        Check("before the launch you have no boat", Has("boat") == 0);
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"launching gives you the boat ({Has("boat")}, {mode})", Has("boat") == 1 && mode == "boatanim" && yardLaunching);
        Check($"moored at Pip's jetty, not aboard ({state.boatAt}, {state.aboard})", state.boatAt == "saltmere" && !state.aboard);
        saved = SaveFile.Read(SaveFile.Slot);
        Check("and the boat was saved before it slid", saved.inv.GetValueOrDefault("boat") == 1);
        yield return 25;
        pendingShot = "yard-09-launch"; yield return 1;
        foreach (var _ in LungUntil(() => mode == "boatanim", 5f)) yield return 1;
        Check($"the launch ends and you're back in play ({mode})", mode == "play");
        yield return 4;
        Check($"the slipway now says the boat is built ({target?.Type}, {target?.Label})", target?.Type == "info" && target.Label.Contains("already built"));
        Check("the guide no longer asks for a boat", !Goals().Any(g => g.Id == "explore:boat"));

        /* ---------- Storms and pausing, on a fresh project ---------- */
        state.inv.Remove("boat"); state.boatAt = "saltmere";
        state.yards.Clear();
        state.inv["wood"] = 40; state.inv["iron_bar"] = 4; state.inv["sailcloth"] = 1;
        yield return 4;
        Check($"a new project starts at stage one ({target?.Label})", target?.Type == "yard" && target.Label.Contains("keel and ribs"));
        state.weather = "storm"; state.forecast = null; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"in a storm there's no starting work ({mode})", mode == "play" && !(YardOf(b)?.paid ?? false));
        ClearSkies(); SnapWeather(); yield return 4;
        Inp.Hold(KeyboardKey.E, true); yield return 3;
        Check($"work starts once the weather clears ({mode})", mode == "boatwork");
        yield return 12;
        state.weather = "storm"; yield return 4;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"a storm coming up stops the work and keeps it ({mode}, paid {YardOf(b)?.paid}, {YardOf(b)?.work:0.00} s)", mode == "play" && YardOf(b).paid && YardOf(b).work > 0.05f);
        ClearSkies(); SnapWeather(); yield return 4;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"and after it you can go on ({mode})", mode == "boatwork");
        OpenPause(anyMode: true); yield return 3;
        float frozen = YardOf(b).work;
        yield return 20;
        Check($"paused, the work waits ({mode}, {frozen:0.00} -> {YardOf(b).work:0.00})", mode == "pause" && YardOf(b).work == frozen);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the real Resume button goes back to the work ({mode})", mode == "boatwork");
        Inp.Tap(KeyboardKey.Escape); yield return 3;

        /* ---------- Taking it down gives back what's in it ---------- */
        int woodNow = Has("wood"), stoneNow = Has("stone"), ironNow = Has("iron_bar");
        Inp.Tap(KeyboardKey.B); yield return 3;
        SelectTool("remove"); hover = (b.x, b.y); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Inp.Tap(KeyboardKey.B); yield return 3;
        Check($"taking it down: the slipway is gone ({state.builds.Count(p => p.id == "slipway")})", !state.builds.Any(p => p.id == "slipway"));
        Check($"and the paid stage came back with it (wood {woodNow} -> {Has("wood")}, iron {ironNow} -> {Has("iron_bar")}, stone {stoneNow} -> {Has("stone")})",
            Has("wood") == woodNow + 4 + 6 && Has("iron_bar") == ironNow + 2 && Has("stone") == stoneNow + 2);
        Check("no project is left behind", state.yards.Count == 0);

        /* ---------- The guide, and the bar ---------- */
        state.commons["tilapia"] = 1; state.inv.Remove("boat");
        yield return 4;
        var goal = Goals().FirstOrDefault(g => g.Id == "explore:boat");
        Check($"with no slipway the guide says to build one ({goal?.Text})", goal != null && goal.Text.Contains("slipway"));
        state.builds.Add(new Build { id = "slipway", x = sx, y = sy }); ReindexBuilds(); state.inv["wood"] = 40; state.inv["iron_bar"] = 4; state.inv["sailcloth"] = 1;
        yield return 4;
        goal = Goals().FirstOrDefault(g => g.Id == "explore:boat");
        Check($"with one, it points at the slipway and the first stage ({goal?.Text})", goal != null && goal.Text.Contains("keel and ribs") && goal.Ready);
        state.hinted["amihan"] = true;
        Inp.Tap(KeyboardKey.B); yield return 4;
        yield return 3;
        Check($"the build bar still fits the screen with every piece ({lastBuildBarW:0} of {Gfx.LW})", mode == "build" && lastBuildBarW <= Gfx.LW);
        pendingShot = "yard-10-bar"; yield return 2;
        Inp.Tap(KeyboardKey.B); yield return 3;

        /* ---------- Old saves ---------- */
        var old = System.Text.Json.JsonSerializer.Deserialize<State>("{\"created\":true,\"yards\":{\"1,1\":null,\"2,2\":{\"stage\":9,\"paid\":true,\"work\":99}}}", new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        Check("a save without yards, or with damaged ones, still loads", old != null);
        Settings.Data.zoom = 4;
    }

    // FESH_SPRITES -yard, at 3x: every construction frame on its cradle (the empty slipway, the keel and ribs, the planking,
    // the outriggers, the mast and rigging, the sail), then the finished hull in each facing.
    void ExportYardSprites(string path)
    {
        var keep = pix;
        const int W = 13 * 52, H = 120;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#c9b57e"));
        int n = 0;
        for (int s = 0; s <= 5; s++)
            foreach (var (g, ax, ay) in YardFrames(s)) BlitBoat(g, ax, ay, 26 + n++ * 52, 40);
        BlitBoat(BoatArt.RightBack, BoatArt.RightBackX, BoatArt.RightBackY, 26, 92); BlitBoat(BoatArt.RightFront, BoatArt.RightFrontX, BoatArt.RightFrontY, 26, 92);
        BlitBoat(BoatArt.UpBack, BoatArt.UpBackX, BoatArt.UpBackY, 78, 92); BlitBoat(BoatArt.UpFront, BoatArt.UpFrontX, BoatArt.UpFrontY, 78, 92);
        BlitBoat(BoatArt.DownBack, BoatArt.DownBackX, BoatArt.DownBackY, 130, 92); BlitBoat(BoatArt.DownFront, BoatArt.DownFrontX, BoatArt.DownFrontY, 130, 92);
        pix = keep;
        var img = Raylib.GenImageColor(W, H, Color.Black);
        for (int yy = 0; yy < H; yy++)
            for (int xx = 0; xx < W; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * W + xx]);
        Raylib.ImageResizeNN(ref img, W * 3, H * 3);
        Raylib.ExportImage(img, path.Replace(".png", "-yard.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
