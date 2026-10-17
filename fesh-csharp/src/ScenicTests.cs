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
        ("ml-river", MlPlace("town").x + 40, MlPlace("town").y + 60),
        // 1.31: the Blender landmarks that no other stop shows.
        ("ml-cave", MlPlace("cave").x, MlPlace("cave").y + 24), ("sanctuary", 2202, 704), ("starwell", StarwellX, StarwellY + 40)
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
        // The gulls and a butterfly, zoomed: the wing through a beat, a glide, and both ways round.
        {
            var keep = pix;
            var sheet = new Pix(120, 40);
            pix = sheet;
            Array.Fill(sheet.Buf, Pal.C("#6ab0cc"));
            for (int i = 0; i < 7; i++) DrawGull(10 + i * 14, 12, 1, 1, i * 0.0357f);
            for (int i = 0; i < 4; i++) DrawGull(10 + i * 14, 30, i < 2 ? 1 : -1, i % 2 == 0 ? 0 : 1, 0.05f + i * 0.04f);
            pix = keep;
            var img = GenImageColor(120, 40, Color.Black);
            for (int y = 0; y < 40; y++) for (int x = 0; x < 120; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 120 + x]);
            ImageResizeNN(ref img, 120 * 8, 40 * 8);
            ExportImage(img, Path.Combine(testDir, "sky.png"));
            UnloadImage(img);
        }
        // Every animal beside a person, awake and (the lower row) asleep, to judge their sizes.
        {
            var keep = pix;
            var sheet = new Pix(280, 70);
            pix = sheet;
            Array.Fill(sheet.Buf, Pal.C("#5d9b45"));
            LookData.DrawPerson(sheet, new Look { skin = 2, shirt = 1, hat = 0, hair = 1 }, 12, 30, "down", 0);
            int ax = 40;
            foreach (var id in new[] { "carabao", "pig", "sheep", "goat", "dog", "cat", "chicken", "hornbill", "parrot", "tarsier" })
            {
                var (aw, _) = AnimalArt.Size(id);
                AnimalArt.Draw(sheet, id, ax + aw / 2, 30, false, 0);
                AnimalArt.Draw(sheet, id, ax + aw / 2, 62, false, 0, 3);
                ax += aw + 5;
            }
            pix = keep;
            var img = GenImageColor(280, 70, Color.Black);
            for (int y = 0; y < 70; y++) for (int x = 0; x < 280; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 280 + x]);
            ImageResizeNN(ref img, 280 * 4, 70 * 4);
            ExportImage(img, Path.Combine(testDir, "animals.png"));
            UnloadImage(img);
        }
        // Each animal's walk, its four frames left to right (1.31), one animal a row.
        {
            var keep = pix;
            var sheet = new Pix(140, 170);
            pix = sheet;
            Array.Fill(sheet.Buf, Pal.C("#5d9b45"));
            int ry = 18;
            foreach (var id in new[] { "carabao", "pig", "sheep", "goat", "dog", "cat", "chicken", "hornbill", "parrot", "tarsier" })
            {
                var (aw, ah) = AnimalArt.Size(id);
                for (int f = 0; f <= 4; f++) AnimalArt.Draw(sheet, id, 4 + aw / 2 + f * (aw + 3), ry, false, f);
                ry += ah + 3;
            }
            pix = keep;
            var img = GenImageColor(140, 170, Color.Black);
            for (int y = 0; y < 170; y++) for (int x = 0; x < 140; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 140 + x]);
            ImageResizeNN(ref img, 140 * 5, 170 * 5);
            ExportImage(img, Path.Combine(testDir, "animal-walk.png"));
            UnloadImage(img);
        }
        // The nursery's pictures: the emergence through its stages, then the four stages and the four leaves.
        {
            var sheetA = new Pix(120 * 5, 88 * 2);
            float[] es = { 0.1f, 0.3f, 0.42f, 0.5f, 0.56f, 0.62f, 0.7f, 0.8f, 0.92f, 1f };
            for (int i = 0; i < es.Length; i++)
            {
                var one = new Pix(120, 88);
                PaintEmergence(one, es[i]);
                for (int y = 0; y < 88; y++) for (int x = 0; x < 120; x++) sheetA.Buf[(y + 88 * (i / 5)) * 600 + x + 120 * (i % 5)] = one.Buf[y * 120 + x];
            }
            var imgA = GenImageColor(600, 176, Color.Black);
            for (int y = 0; y < 176; y++) for (int x = 0; x < 600; x++) ImageDrawPixel(ref imgA, x, y, sheetA.Buf[y * 600 + x]);
            ImageResizeNN(ref imgA, 600 * 2, 176 * 2);
            ExportImage(imgA, Path.Combine(testDir, "nursery-emerge.png"));
            UnloadImage(imgA);
            var sheetB = new Pix(59 * 4, 36 + 28);
            for (int i = 0; i < 4; i++)
            {
                var a = new Pix(59, 36); PaintStage(a, i);
                for (int y = 0; y < 36; y++) for (int x = 0; x < 59; x++) sheetB.Buf[y * 236 + x + 59 * i] = a.Buf[y * 59 + x];
                var b = new Pix(59, 28); PaintFeedLeaf(b, i);
                for (int y = 0; y < 28; y++) for (int x = 0; x < 59; x++) sheetB.Buf[(y + 36) * 236 + x + 59 * i] = b.Buf[y * 59 + x];
            }
            var imgB = GenImageColor(236, 64, Color.Black);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 236; x++) ImageDrawPixel(ref imgB, x, y, sheetB.Buf[y * 236 + x]);
            ImageResizeNN(ref imgB, 236 * 4, 64 * 4);
            ExportImage(imgB, Path.Combine(testDir, "nursery-stages.png"));
            UnloadImage(imgB);
        }
        // The cave model at its stages.
        {
            float[] ts = { 0.05f, 0.25f, 0.45f, 0.6f, 0.75f, 0.85f, 0.95f, 1f };
            var sheetC = new Pix(CmW * 4, CmH * 2);
            for (int i = 0; i < ts.Length; i++)
            {
                var one = new Pix(CmW, CmH);
                PaintCaveModel(one, ts[i]);
                for (int y = 0; y < CmH; y++) for (int x = 0; x < CmW; x++) sheetC.Buf[(y + CmH * (i / 4)) * CmW * 4 + x + CmW * (i % 4)] = one.Buf[y * CmW + x];
            }
            var imgC = GenImageColor(CmW * 4, CmH * 2, Color.Black);
            for (int y = 0; y < CmH * 2; y++) for (int x = 0; x < CmW * 4; x++) ImageDrawPixel(ref imgC, x, y, sheetC.Buf[y * CmW * 4 + x]);
            ImageResizeNN(ref imgC, CmW * 4 * 2, CmH * 2 * 2);
            ExportImage(imgC, Path.Combine(testDir, "cave-model.png"));
            UnloadImage(imgC);
        }
        // Baga's plume at each level (and in the night): puffs boiling up, bending west.
        {
            var keepTime = time; var keepKeep = pix;
            var sheetP = new Pix(160 * 4, 100);
            for (int lv = 0; lv < 4; lv++)
            {
                var one = new Pix(160, 100);
                one.CamX = (int)ConeX - 80; one.CamY = (int)(ConeBaseY - ConeH) - 80;
                Array.Fill(one.Buf, Pal.C("#6fa0c0"));
                pix = one; time = 31.4f + lv;
                DrawPlume(ConeBaseY - ConeH, lv, lv == 3);
                pix = keepKeep;
                for (int y = 0; y < 100; y++) for (int x = 0; x < 160; x++) sheetP.Buf[y * 640 + x + 160 * lv] = one.Buf[y * 160 + x];
            }
            time = keepTime;
            var imgP = GenImageColor(640, 100, Color.Black);
            for (int y = 0; y < 100; y++) for (int x = 0; x < 640; x++) ImageDrawPixel(ref imgP, x, y, sheetP.Buf[y * 640 + x]);
            ImageResizeNN(ref imgP, 640 * 2, 200);
            ExportImage(imgP, Path.Combine(testDir, "plume.png"));
            UnloadImage(imgP);
        }
        // The lahar watch: the camera before, as it comes, and at the mouth (live, in rain), and the map before and after.
        {
            var sheetL = new Pix(LcW * 4, LcH + LaharMapH * 0);
            float[] ts = { -1f, 1.5f, 3.2f, 5.5f };
            for (int i = 0; i < 4; i++)
            {
                var one = new Pix(LcW, LcH);
                PaintLaharCam(one, new LaharWatch { Live = true, T = ts[i] });
                for (int y = 0; y < LcH; y++) for (int x = 0; x < LcW; x++) sheetL.Buf[y * LcW * 4 + x + LcW * i] = one.Buf[y * LcW + x];
            }
            var imgL = GenImageColor(LcW * 4, LcH, Color.Black);
            for (int y = 0; y < LcH; y++) for (int x = 0; x < LcW * 4; x++) ImageDrawPixel(ref imgL, x, y, sheetL.Buf[y * LcW * 4 + x]);
            ImageResizeNN(ref imgL, LcW * 4 * 2, LcH * 2);
            ExportImage(imgL, Path.Combine(testDir, "lahar-cam.png"));
            UnloadImage(imgL);
            var map = new Pix(LaharMapW * 2, LaharMapH);
            for (int i = 0; i < 2; i++)
            {
                var one = new Pix(LaharMapW, LaharMapH);
                PaintLaharMap(one, new LaharWatch { T = i == 0 ? -1 : 5 });
                for (int y = 0; y < LaharMapH; y++) for (int x = 0; x < LaharMapW; x++) map.Buf[y * LaharMapW * 2 + x + LaharMapW * i] = one.Buf[y * LaharMapW + x];
            }
            var imgM = GenImageColor(LaharMapW * 2, LaharMapH, Color.Black);
            for (int y = 0; y < LaharMapH; y++) for (int x = 0; x < LaharMapW * 2; x++) ImageDrawPixel(ref imgM, x, y, map.Buf[y * LaharMapW * 2 + x]);
            ImageResizeNN(ref imgM, LaharMapW * 2 * 2, LaharMapH * 2);
            ExportImage(imgM, Path.Combine(testDir, "lahar-map.png"));
            UnloadImage(imgM);
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
        var firstDiffs = new List<string>();
        for (int i = 0; i < partial.Length; i++) if (partial[i].R != worldBase.Buf[i].R || partial[i].G != worldBase.Buf[i].G || partial[i].B != worldBase.Buf[i].B) { diffs++; if (firstDiffs.Count < 6) firstDiffs.Add($"{i % PW},{i / PW}:{worldMap[i / PW / T, i % PW / T]}"); }
        Check($"repainting round a chopped tile matches a full bake ({diffs} pixels differ: {string.Join(" ", firstDiffs)})", diffs == 0);
        Array.Copy(keepMap, worldMap, keepMap.Length);
        RenderBase();

        // 1.33: every island has terrain of its own, and the coast has banks and steps (Relief.cs).
        {
            var names = new (string name, int x0, int x1, int y0, int y1, float minMax)[]
            {
                ("Saltmere", 0, 32, 0, 18, 0.2f), ("Frostfang", 45, 80, 0, 20, 0.5f), ("Sunscald", 46, 86, 26, 50, 0.3f), ("Mirewood", 3, 37, 27, 49, 0.2f),
                ("Amihan Village", 151, 181, 9, 35, 0.2f), ("Luntian", 193, 217, 6, 26, 0.5f)
            };
            foreach (var (name, x0, x1, y0, y1, minMax) in names)
            {
                float lo = 1, hi = 0;
                for (int ty = y0; ty < y1; ty++)
                    for (int tx = x0; tx < x1; tx++)
                    {
                        if (landDist[ty, tx] < 3) continue;
                        for (int py = 0; py < T; py += 3)
                            for (int px = 0; px < T; px += 3) { float h = Rel(tx * T + px, ty * T + py); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h); }
                    }
                Check($"{name} has hills of its own ({lo:0.00} to {hi:0.00})", hi >= minMax && hi - lo > minMax * 0.5f);
            }
            int marilayaTerrain = 0, terrainTiles = 0;
            for (int ty = 0; ty < ROWS; ty++)
                for (int tx = 0; tx < COLS; tx++)
                {
                    if (TerrainAt(tx, ty) == null) continue;
                    terrainTiles++;
                    if (tx >= MlStart) marilayaTerrain++;
                }
            Check($"Marilaya keeps its own relief ({marilayaTerrain} of {terrainTiles} tiles with the new terrain)", terrainTiles > 1000 && marilayaTerrain == 0);
            // Banks and steps only ever stand on land with water (or sand, for a step) to the south; beaches have no bank.
            int faces = 0, loose = 0, sandFaces = 0; var looseAt = new List<string>();
            var faceColours = new HashSet<Color>(BankFace.Values.SelectMany(a => a));
            for (int wy = 0; wy < PH; wy += 1)
                for (int wx = 0; wx < PW; wx += 1)
                {
                    char s = ShapePx(wx, wy);
                    if (!Shore(s) || !BankFace.ContainsKey(s) || TerrainAt(wx / T, wy / T) is not { } tr) continue;
                    var c = worldBase.Buf[wy * PW + wx];
                    if (!faceColours.Contains(c) || BankPixel(wx, wy, s, tr, c) is not { } bc || bc.R != c.R || bc.G != c.G || bc.B != c.B) continue;   // a face pixel, not a pebble that happens to match
                    // Only the face colours that were not mixed with anything else can be told for certain.
                    bool south = false;
                    for (int k = 1; k <= 6 && !south; k++) { char u = ShapePx(wx, wy + k); south = u is '~' or 'w' or 's'; }
                    faces++;
                    if (!south) { loose++; if (looseAt.Count < 4) looseAt.Add($"{wx},{wy} {s}"); }
                }
            for (int wy = 0; wy < PH; wy += 2)
                for (int wx = 0; wx < PW; wx += 2)
                    if (ShapePx(wx, wy) == 's' && faceColours.Contains(worldBase.Buf[wy * PW + wx])) sandFaces++;
            Check($"banks and steps stand on land with water or sand to the south ({faces} face pixels, {loose} loose {string.Join(" ", looseAt)})", faces > 1000 && loose == 0);
            Check($"a beach has no bank face ({sandFaces} sand pixels in a face colour)", sandFaces == 0);
        }

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
