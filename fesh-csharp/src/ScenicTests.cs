#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// FESH_SCENIC=1 (with FESH_AUTOTEST and FESH_SAVE): visits a place on every island and saves the world view alone (no
// HUD) at 4x, by day, at night and in the rain, for judging the scenery's pixels before and after a change.
partial class Game
{
    string pendingPixShot;

    void ExportPixShot()
    {
        if (pendingPixShot == null) return;
        var img = GenImageColor(W, H, Color.Black);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++) ImageDrawPixel(ref img, x, y, pix.Buf[y * W + x]);
        ImageResizeNN(ref img, W * 4, H * 4);
        ExportImage(img, Path.Combine(testDir, pendingPixShot + ".png"));
        UnloadImage(img);
        pendingPixShot = null;
    }

    // The standable point nearest a spot, searched outwards in rings.
    (float x, float y) StandableNear(float x, float y)
    {
        for (int r = 0; r < 200; r += 3)
            for (int a = 0; a < 24; a++)
            {
                float px = x + MathF.Cos(a * MathF.Tau / 24) * r, py = y + MathF.Sin(a * MathF.Tau / 24) * r;
                if (CanStand(px, py)) return (px, py);
                if (r == 0) break;
            }
        return (x, y);
    }

    IEnumerable<(string id, float x, float y)> ScenicStops() => new (string, float, float)[]
    {
        ("saltmere", 160, 115), ("saltmere-coast", 230, 60), ("frostfang", 620, 90), ("sunscald", 660, 380), ("mirewood", 200, 380),
        ("atoll", 935, 262), ("amihan", 1625, 192), ("luntian", 2040, 138), ("baga", 2235, 470), ("bakawan", 1890, 560),
        ("asinan", 265, 643), ("daangpulo", 645, 630), ("parola", LighthouseX - 90, LighthouseY + 14),
        ("ml-town", SelaX - 20, SelaY + 24), ("ml-springs", MlPlace("springs").x + 30, MlPlace("springs").y - 40),
        ("ml-tindig", TindigX, TindigY + 30), ("ml-tindig-n", TindigX - 20, TindigY - 70), ("ml-hills", MlPlace("cliff").x - 20, MlPlace("cliff").y + 14),
        ("ml-river", MlPlace("town").x + 40, MlPlace("town").y + 60)
    };

    IEnumerable<int> ScenicScript()
    {
        Note("Scenery screenshots (world view only, 4x)");
        state = new State { created = true, look = new Look { name = "Painter" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        string only = Environment.GetEnvironmentVariable("FESH_SCENIC_ONLY");
        yield return 3;
        var sw = System.Diagnostics.Stopwatch.StartNew(); RenderBase(); Note($"RenderBase took {sw.ElapsedMilliseconds} ms");
        foreach (var (id, x, y) in ScenicStops())
        {
            if (only is { Length: > 0 } && !id.StartsWith(only)) continue;
            (player.X, player.Y) = StandableNear(x, y);
            player.Face = "down";
            yield return 4;
            pendingPixShot = "s-" + id; yield return 2;
        }
        if (only is not { Length: > 0 })
        {
            foreach (var (id, x, y) in new[] { ("saltmere", 160f, 115f), ("ml-tindig", TindigX, TindigY + 30), ("amihan", 1625f, 192f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                SetNight(true); yield return 4;
                pendingPixShot = "n-" + id; yield return 2;
                SetNight(false);
                state.forecast = new() { new WeatherSpell { at = 0, w = "rain" } }; state.weather = planned = "rain"; SnapWeather();
                yield return 4;
                pendingPixShot = "r-" + id; yield return 2;
                ClearSkies();
            }
            (player.X, player.Y) = StandableNear(160, 115);
            state.clock = 6 * 60 + 50; yield return 4;
            pendingPixShot = "m-saltmere-mist"; yield return 2;
            state.clock = 19 * 60 + 20; yield return 4;
            pendingPixShot = "g-saltmere-sunset"; yield return 2;
            SetNight(false); yield return 2;
        }
        if (only is not { Length: > 0 } || only == "ui")
        {
            // The HUD and the panels, as the player sees them.
            (player.X, player.Y) = StandableNear(TindigX, TindigY + 30);
            state.coins = 380; state.charted = new() { "0", "1", "2", "3", "4" };
            state.hinted["amihan"] = true; state.hinted["marilaya"] = true;
            yield return 6;
            pendingShot = "u-hud"; yield return 3;
            foreach (var p in new[] { "map", "bag", "dex", "tackle", "case", "journal" })
            {
                TogglePanel(p); yield return 6;
                pendingShot = "u-" + p; yield return 3;
                ClosePanels(); yield return 3;
            }
            foreach (var (p, setup) in new (string, Action)[] { ("shop", null), ("craft", () => craftStation = "workbench"), ("tides", null), ("atlas", () => OpenAtlas("saltmere")) })
            {
                setup?.Invoke();
                if (p != "atlas") { mode = "panel"; panel = p; }
                yield return 6;
                pendingShot = "u-" + p; yield return 3;
                ClosePanels(); yield return 3;
            }
            chart = 0; TogglePanel("map"); chart = 0; yield return 6;
            pendingShot = "u-map-west"; yield return 3;
            ClosePanels(); yield return 3;
        }
        // The villagers' chores, zoomed: sweeping and mending a net, facing down, left and right.
        {
            var keep = pix;
            var sheet = new Pix(100, 24);
            pix = sheet;
            Array.Fill(sheet.Buf, Pal.C("#e8cf96"));
            var look = new Look { skin = 2, shirt = 1, hat = 0, hair = 1 };
            int col = 0;
            foreach (var kind in new[] { "sweep", "net" })
                foreach (var face in new[] { "down", "left", "right" })
                {
                    int x = 10 + col++ * 16, y = 18;
                    var (h1, h2) = ChoreHands(kind, x, y, face);
                    LookData.DrawPerson(sheet, look, x, y, face, 0, arms: 4, hand: h1, hand2: h2);
                    DrawChoreProp(kind, x, y, face);
                }
            pix = keep;
            var img = GenImageColor(100, 24, Color.Black);
            for (int y = 0; y < 24; y++) for (int x = 0; x < 100; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 100 + x]);
            ImageResizeNN(ref img, 100 * 8, 24 * 8);
            ExportImage(img, Path.Combine(testDir, "chores.png"));
            UnloadImage(img);
        }
        foreach (int f in GraphicsScript()) yield return f;
    }

    // Checks on the scenery and the HUD (also at the end of the full run).
    IEnumerable<int> GraphicsScript()
    {
        Note("Graphics: the blended ground, the HUD and the chart");
        state = new State { created = true, look = new Look { name = "Painter" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RenderBase();
        long bake = sw.ElapsedMilliseconds;
        Check($"the outdoor base layer bakes in good time ({bake} ms)", bake < 3000);
        // Every tile's middle shows its own kind: the smoothing only ever rounds tiles off at their edges, so land stays
        // land, water stays water, and the shallows' line stays where wading ends.
        int bad = 0; string firstBad = null;
        for (int ty = 0; ty < ROWS; ty++)
            for (int tx = 0; tx < COLS; tx++)
            {
                char g = GroundAt(tx, ty), c = ShapePx(tx * T + 5, ty * T + 5);
                bool ok = Shore(g) ? Shore(c) : Pool(g) ? Pool(c) : c == g;
                if (g is 'w' or '~') ok = c == g;
                if (!ok) { bad++; firstBad ??= $"{tx},{ty} {g}->{c}"; }
            }
        Check($"every tile's middle keeps its own kind of ground or water ({bad} wrong{(firstBad != null ? ", first " + firstBad : "")})", bad == 0);
        // A diagonal creek joins up: Tindig's stream runs through tile corners without a break.
        var creekTiles = new HashSet<(int x, int y)>();
        for (int ty = 0; ty < ROWS; ty++) for (int tx = MlStart; tx < COLS; tx++) if (worldMap[ty, tx] == 'l' && Dist(tx * T + 5, ty * T + 5, MlWx(12.5f), MlWy(25.3f)) < 50) creekTiles.Add((tx, ty));
        int joined = 0, diag = 0;
        foreach (var (cx, cy) in creekTiles)
            foreach (var (ox, oy) in new[] { (1, 1), (1, -1) })
                if (creekTiles.Contains((cx + ox, cy + oy)) && !creekTiles.Contains((cx + ox, cy)) && !creekTiles.Contains((cx, cy + oy)))
                {
                    diag++;
                    // The shared corner and the pixels either side of it along the diagonal are all water.
                    int px = (cx + 1) * T, py = oy > 0 ? (cy + 1) * T : cy * T;
                    if (Wet(ShapePx(px, py)) && Wet(ShapePx(px - 1, py - oy)) && Wet(ShapePx(px, py + (oy > 0 ? 0 : -1)))) joined++;
                }
        Check($"diagonal steps of a creek are joined by water ({joined} of {diag})", diag > 0 && joined == diag);
        // Chopping keeps the ground it always left (what you can build or plant there doesn't change), and repainting
        // round it gives exactly what a full bake would.
        var palm = trees.FirstOrDefault(t => t.kind == 'h' && BiomeAt(t.x, t.y) == 5);
        var oak = trees.FirstOrDefault(t => t.kind == 't' && BiomeAt(t.x, t.y) == 0);
        Check($"a chopped palm on Amihan still leaves forest floor ({FelledGround('h', palm.x, palm.y)})", palm.kind == 'h' && FelledGround('h', palm.x, palm.y) == 'j');
        Check($"a chopped oak on Saltmere still leaves grass ({FelledGround('t', oak.x, oak.y)})", oak.kind == 't' && FelledGround('t', oak.x, oak.y) == 'g');
        var keepMap = (char[,])worldMap.Clone();
        int diffs = 0;
        foreach (var tr in new[] { palm, oak })
        {
            worldMap[tr.y, tr.x] = FelledGround(tr.kind, tr.x, tr.y);
            RenderTile(tr.x, tr.y);
        }
        var partial = (Color[])worldBase.Buf.Clone();
        RenderBase();
        for (int i = 0; i < partial.Length; i++) if (partial[i].R != worldBase.Buf[i].R || partial[i].G != worldBase.Buf[i].G || partial[i].B != worldBase.Buf[i].B) diffs++;
        Check($"repainting round a chopped tile matches a full bake ({diffs} pixels differ)", diffs == 0);
        Array.Copy(keepMap, worldMap, keepMap.Length);
        RenderBase();

        // Thinning Tindig's forest doesn't turn the gaps into boulders.
        int rocks = 0, forestTiles = 0;
        for (int ty = 0; ty < ROWS; ty++)
            for (int tx = MlStart; tx < COLS; tx++)
            {
                float lx = (tx - MlX + 0.5f) / MlS, ly = (ty - MlY + 0.5f) / MlS;
                if (!InTindig(lx, ly) || (worldMap[ty, tx] != 'j' && !(worldMap[ty, tx] is 't' or 'R' && MlGroundUnder(tx, ty) == 'j'))) continue;
                forestTiles++;
                if (worldMap[ty, tx] == 'R') rocks++;
            }
        Check($"boulders on Tindig's forest stay rare ({rocks} of {forestTiles} tiles)", forestTiles > 50 && rocks <= forestTiles * 0.05f);
        // What the moving touches cost a frame, in a view full of shallows and shore (Luntian's lagoon).
        (player.X, player.Y) = StandableNear(2040, 138);
        yield return 3;
        UpdateCamera(); pix.CamX = camX; pix.CamY = camY;
        var fx = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 30; i++) { DrawCaustics(i * 0.1f); DrawGlints(i * 0.1f); DrawWaves(i * 0.1f); DrawRainSplashes(i * 0.1f); }
        pix.CamX = pix.CamY = 0;
        double perFrame = fx.Elapsed.TotalMilliseconds / 30;
        Check($"the water's moving touches cost little a frame ({perFrame:0.00} ms)", perFrame < 4);

        // Walking on the beach leaves footprints that fade; fish leap out of open water now and then.
        footprints.Clear();
        // A strip of beach on Saltmere four tiles long, walked along to the east.
        var beach = Enumerable.Range(0, 32 * 18).Select(i => (x: i % 32, y: i / 32))
            .First(p => Enumerable.Range(0, 5).All(k => worldMap[p.y, p.x + k] == 's' && BuildAt(p.x + k, p.y) == null));
        (player.X, player.Y) = (beach.x * T + 5, beach.y * T + 7);
        player.Face = "right";
        Inp.Hold(KeyboardKey.Right, true);
        for (int i = 0; i < 30; i++) yield return 1;
        Inp.Hold(KeyboardKey.Right, false);
        Check($"walking on sand leaves footprints ({footprints.Count})", footprints.Count >= 2);
        // They fade on the game's clock, not on drawing, so time spent indoors counts (Codex's review).
        int before = footprints.Count;
        var shack = new Build { id = "shack", x = beach.x + 8, y = beach.y };
        EnterHouse(ShackKey(shack), player.X, player.Y);
        for (int i = 0; i < 60 && (!InHouse || mode != "play"); i++) yield return 2;
        float until = time + FootprintLife + 0.5f;
        while (time < until) yield return 5;
        LoadScene("world"); (player.X, player.Y) = (beach.x * T + 5, beach.y * T + 7);
        yield return 3;
        Check($"footprints fade while you're indoors too ({before} -> {footprints.Count})", before > 0 && footprints.Count == 0);
        leaps.Clear(); leapT = 0;
        (player.X, player.Y) = StandableNear(275, 117);
        int leapt = 0;
        for (int i = 0; i < 300 && leapt == 0; i++) { yield return 1; leapt = leaps.Count; }
        Check($"a fish leaps out of the open water in view ({leapt})", leapt > 0);

        // Lamps in the village windows in the evening, none by day or in the small hours.
        (player.X, player.Y) = StandableNear(1625, 192);
        SetNight(true); yield return 4;
        int evening = litWindows.Count;
        state.clock = 2 * 60; yield return 4;
        int smallHours = litWindows.Count;
        SetNight(false); yield return 4;
        int noon = litWindows.Count;
        Check($"village windows are lit in the evening, dark at 2 AM and by day ({evening}, {smallHours}, {noon})", evening >= 2 && smallHours == 0 && noon == 0);
        // The chart's picture keeps the windows in their day colour even when it's drawn at night (Codex's review).
        {
            var keepPix = pix;
            var chartLike = new Pix(200, 80);
            pix = chartLike;
            SetNight(true);
            int windowsBefore = litWindows.Count;
            DrawBahay(100, 60, "#d9734f");
            var win = chartLike.Buf[(60 - 12 + 2) * 200 + 100 - 13 + 2];
            pix = keepPix;
            SetNight(false);
            Check($"a house drawn onto the chart at night keeps its day windows ({win.R},{win.G},{win.B})", win.Equals(Pal.C("#485d56")) && litWindows.Count == windowsBefore);
        }
        // The moon's path only lands on bare water: anything drawn over its strokes keeps them off (Codex's review).
        state.day = 1; SetNight(true);
        (player.X, player.Y) = StandableNear(275, 117);
        yield return 3;
        UpdateCamera(); pix.CamX = camX; pix.CamY = camY;
        PlanMoonPath(time);
        int planned = moonPx.Count;
        pix.Fill(0, 0, W * 2 / 3, H, Pal.C("#ff00ff"));
        KeepMoonPath();
        bool onlyLower = moonPx.All(m => m.x >= W * 2 / 3);
        int kept = moonPx.Count;
        pix.CamX = pix.CamY = 0;
        SetNight(false); yield return 2;
        Check($"the moon's path keeps off anything drawn over the water ({planned} planned, {kept} kept, all beside the cover: {onlyLower})", planned > 10 && kept > 0 && kept < planned && onlyLower);
        // Chores: only by day, standing still, and never with you right beside them.
        bool AnyChore(float px, float py) => Enumerable.Range(0, 40).Any(i => { time += 1.3f; return ChoreNow("niko", 1650, 190, "down", false) != null; });
        LookData.AutoBlink = true;
        (player.X, player.Y) = (1650 + 60, 190);
        bool byDay = AnyChore(0, 0);
        (player.X, player.Y) = (1650 + 10, 190);
        bool beside = AnyChore(0, 0);
        SetNight(true); (player.X, player.Y) = (1650 + 60, 190);
        bool atNight = AnyChore(0, 0);
        SetNight(false);
        LookData.AutoBlink = Environment.GetEnvironmentVariable("FESH_SCENIC") == "1";
        Check($"villagers do their chores by day, not beside you or at night ({byDay}, {beside}, {atNight})", byDay && !beside && !atNight);

        // The HUD: one card that never changes size, buttons all the same width.
        (player.X, player.Y) = StandableNear(TindigX, TindigY + 30);
        yield return 4;
        var labels = new[] { "Build", "Bag", "Tackle", "Map", "Fesh-dex", "Case board" };
        var widths = labels.Select(l => Gfx.Seen.TryGetValue(l, out var r) ? r.Width : -1).ToList();
        Check($"the top buttons are all one width ({string.Join(", ", widths.Select(w => w.ToString("0")))})", widths.All(w => w > 0 && Math.Abs(w - widths[0]) < 0.5f));
        float leftButton = labels.Min(l => Gfx.Seen[l].X);
        Check($"the buttons stay clear of the status card ({leftButton:0} > {HudX + HudW:0})", leftButton > HudX + HudW + 6);
        Check($"the goal card sits under the status card at its width ({CardY:0}, {CardW:0})", CardY >= HudY + HudH && CardW == HudW);
        // The goal marker never lands on the status card, wherever the goal is on screen.
        int onCard = 0;
        for (float sx = 40; sx < Gfx.LW - 40; sx += 20)
            for (float sy = 100; sy < Gfx.LH - 50; sy += 10)
                if (MarkerAt(sx, sy) is var (mx, my, up) && mx > HudX - 14 && mx < HudX + HudW + 14 && my - (up ? 11 : 18) < HudY + HudH)
                    onCard++;
        Check($"the goal marker is never drawn over the status card ({onCard} spots)", onCard == 0);
        // A toast sits right of the cards, under the buttons.
        Toast("A test message to see where toasts go, long enough to wrap onto a second line across the screen for sure.", 3);
        yield return 20;
        pendingShot = "gfx-toast"; yield return 2;
        toastTimer = 0; yield return 15;
        // The chart: a narrow one sits left, with the key beside it, all inside the panel.
        state.hinted["amihan"] = true; state.hinted["marilaya"] = true;
        TogglePanel("map"); chart = 2; yield return 4;
        var chartR = Gfx.Seen["chart"];
        Check($"the Marilaya chart fits on the screen ({chartR.X:0},{chartR.Y:0} {chartR.Width:0}x{chartR.Height:0})", chartR.X > 0 && chartR.Y > 0 && chartR.X + chartR.Width < Gfx.LW && chartR.Y + chartR.Height < Gfx.LH);
        pendingShot = "gfx-chart"; yield return 2;
        ClosePanels(); yield return 3;
    }
}
#endif
