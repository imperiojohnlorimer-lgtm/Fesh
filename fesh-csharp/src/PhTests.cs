#if DEBUG
namespace Fesh;

// FESH_PH_TEST=1 (1.48): the Philippines map. A new game on it starts at Boac on Marinduque with a banca; the world is the
// country, true to scale, baked in a window round you; real places land where they should; the window moves as you go and
// a moved window is the same, pixel for pixel, as one baked from scratch there; a save and reload keep the map and your place.
// Screenshots round the country (ph-*.png).
partial class Game
{
    IEnumerable<int> PhScript()
    {
        Note("The Philippines map");
        // A new game from the title, by real clicks: New game, the Philippines in the creator's Map row, Start.
        state = new State { created = true, look = new Look { name = "Mangingisda" } };
        mode = "play"; StartGame(false); yield return 3;
        QuitToTitle(); yield return 30;
        ClickButton("New game"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3;
        if (mode != "create" && titleView == "slots")
        {
            var slotBtn = Gfx.Seen.Keys.FirstOrDefault(k => k.StartsWith("slot"));
            if (slotBtn != null) { ClickButton(slotBtn); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3; }
        }
        bool sawMap = ClickButton("Philippines"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"the fisher maker offers the map, and the Philippines can be picked ({sawMap}, {newWorld})", mode == "create" && sawMap && newWorld == "ph");
        pendingShot = "ph-00-creator"; yield return 2;
        bool keepTourOff = tourOff; tourOff = false;   // a real new game would start First steps: the Philippines map must not
        ClickButton("Start"); yield return 8; Inp.ScriptMouse = Offscreen; yield return 3;
        tourOff = keepTourOff;
        Check($"a new game on the Philippines map skips First steps, which are about the original island (tour {state.tour}, active {TourActive})", state.tour == TourDone && !TourActive);
        for (int i = 0; i < 40 && mode == "dialogue"; i++) { Inp.Tap(Raylib_cs.KeyboardKey.E); yield return 3; }
        ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        yield return 4;
        Check($"the world is the whole country ({COLS} x {ROWS} tiles), baked in a window of {bufW / T} x {bufH / T}", PhWorld && COLS == PhMapData.Cols && ROWS == PhMapData.Rows && bufW == PhWin * T);
        var (lat, lon) = PhLatLon(player.X, player.Y);
        Check($"a new game starts at Boac on Marinduque ({lat:0.00}N {lon:0.00}E, {PhPlaceName(player.X, player.Y, true)})", Math.Abs(lat - StartLat) < 0.1 && Math.Abs(lon - StartLon) < 0.1 && CanStand(player.X, player.Y));
        Check("with a banca on the water beside it", Has("boat") > 0 && BoatCanStand(state.boatX, state.boatY) && Dist(state.boatX, state.boatY, player.X, player.Y) < 120);
        // True to scale: Marinduque is about 30-40 tiles across.
        int land = 0, y0t = (int)(player.Y / T);
        for (int x = (int)(player.X / T) - 60; x < (int)(player.X / T) + 60; x++) if (!WaterTile(worldMap[y0t, x])) land++;
        Check($"Marinduque is island-sized at this scale ({land} tiles of land across its middle)", land is > 20 and < 70);
        pendingShot = "ph-01-boac"; yield return 2;
        double ft0 = Raylib_cs.Raylib.GetTime(); yield return 60; double perFrame = (Raylib_cs.Raylib.GetTime() - ft0) / 60 * 1000;
        Note($"a frame on the Philippines map takes {perFrame:0.0} ms ({trees.Count} trees in the country)");
        Check($"the country plays smoothly ({perFrame:0.0} ms a frame)", perFrame < 40);
        pendingPixShot = "ph-02-boac-world"; yield return 2;
        // Walking or sailing to the window's edge moves it; the new edge is baked over the next frames.
        int ox = bufX0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        PhMoveWindow(bufX0 / T + 24, bufY0 / T);
        Note($"moving the window 24 tiles takes {sw.ElapsedMilliseconds} ms at once, then {phQueue.Count} tile steps over the next frames");
        for (int i = 0; i < 900 && phQueue.Count > 0; i++) { PhBakeStep(350); yield return 1; }
        Check($"the window moved ({ox / T} -> {bufX0 / T}) and its new edge is baked ({phQueue.Count} steps left)", bufX0 != ox && phQueue.Count == 0);
        // The moved window matches one baked from scratch at the same place.
        var moved = (Raylib_cs.Color[])worldBase.Buf.Clone(); var movedShape = (char[])shape.Clone(); int shoreMoved = shore.Count;
        PhBakeWindow(bufX0 / T, bufY0 / T); yield return 2;
        int diff = 0, shapeDiff = 0;
        for (int i = 0; i < moved.Length; i++) { var a = moved[i]; var b = worldBase.Buf[i]; if (a.R != b.R || a.G != b.G || a.B != b.B) diff++; if (movedShape[i] != shape[i]) shapeDiff++; }
        Check($"a moved window matches one baked from scratch ({diff} pixels and {shapeDiff} shape pixels differ of {moved.Length}; shore {shoreMoved} vs {shore.Count})", diff < moved.Length / 1000 && shapeDiff == 0 && Math.Abs(shoreMoved - shore.Count) < 20);
        // Real places round the country.
        foreach (var (name, la, lo) in new[] { ("manila", 14.59, 120.98), ("cebu", 10.31, 123.89), ("davao", 7.07, 125.61), ("puerto-princesa", 9.74, 118.74), ("vigan", 17.57, 120.39), ("tacloban", 11.24, 125.0) })
        {
            PhTeleport(la, lo); yield return 4;
            var (pla, plo) = PhLatLon(player.X, player.Y);
            Check($"{name}: standing on land near {la}N {lo}E ({pla:0.00}, {plo:0.00}, {PhPlaceName(player.X, player.Y, true)})", CanStand(player.X, player.Y) && Math.Abs(pla - la) < 0.15 && Math.Abs(plo - lo) < 0.15);
            pendingPixShot = $"ph-{name}"; yield return 2;
        }
        PhTeleport(StartLat, StartLon); yield return 4;
        // Walking off past the window's edge: the window follows (PhRecenter in Update).
        int wx0 = bufX0;
        player.X = bufX0 + (PhWin - PhEdge + 4) * T; yield return 3;
        Check($"near the window's edge it moves to keep you in its middle ({wx0 / T} -> {bufX0 / T})", bufX0 != wx0);
        for (int i = 0; i < 900 && phQueue.Count > 0; i++) yield return 1;
        PhTeleport(StartLat, StartLon); yield return 4;
        // A save and a reload keep the map and your place.
        float sx = player.X, sy = player.Y;
        Save(); yield return 1;
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 4;
        Check($"a reload keeps the map and your place ({state.world}, {player.X:0},{player.Y:0})", PhWorld && Dist(player.X, player.Y, sx, sy) < 2 && CanStand(player.X, player.Y));
        pendingShot = "ph-03-reloaded"; yield return 2;
        TogglePanel("map"); yield return 3;
        pendingShot = "ph-04-chart"; yield return 2;
        ClosePanels(); yield return 2;
        state.aboard = true; player.X = state.boatX; player.Y = state.boatY; yield return 3;
        pendingShot = "ph-05-aboard"; yield return 2;
        state.aboard = false;
        foreach (var step in PhTownScript()) yield return step;
        // And the original map still works after it: a new game there is the old world.
        state = new State { created = true, look = new Look { name = "Back home" } };
        mode = "play"; StartGame(false); yield return 4;
        Check($"the original map still loads afterwards ({COLS} x {ROWS}, at {player.X:0},{player.Y:0})", !PhWorld && COLS == OldCols && ROWS == OldRows && bufX0 == 0 && CanStand(player.X, player.Y));
        pendingShot = "ph-06-old-world"; yield return 2;
    }
    // Step 2: towns, fishing spots, animals, island names and the zoomable chart.
    IEnumerable<int> PhTownScript()
    {
        Note("The Philippines map, step 2: towns, spots, animals, names and the chart");
        // What a player sees, with light and colour on (the autotest normally turns them off), through the day.
        if (Environment.GetEnvironmentVariable("FESH_PH_LIGHT") == "1")
        {
            EffectsOff = false; SnapWeather();
            foreach (int hr in new[] { 7, 10, 12, 15, 17, 19 })
            {
                state.clock = hr * 60; SnapWeather(); yield return 4;
                pendingPixShot = $"ph-light-{hr:00}"; yield return 3;
                pendingShot = $"ph-light-hud-{hr:00}"; yield return 3;
            }
            EffectsOff = true; SetNight(false); yield return 2;
        }
        // Names: the land at 5.86 N 117.75 E is Sabah, Malaysia (it used to say "Palawan"), Palawan is Palawan, and an islet
        // is named after its island.
        var (sbx, sby) = PhPlace(5.86, 117.75);
        var (pwx, pwy) = PhPlace(9.8, 118.7);
        var (mqx, mqy) = PhPlace(13.40, 121.97);
        Check($"the land in the bottom-left corner is Sabah, Malaysia, not Palawan ({PhIslandAt(sbx, sby)})", PhIslandAt(sbx, sby)?.Contains("Sabah") == true);
        Check($"Palawan and Marinduque are named by the island under you ({PhIslandAt(pwx, pwy)}, {PhIslandAt(mqx, mqy)})", PhIslandAt(pwx, pwy) == "Palawan" && PhIslandAt(mqx, mqy) == "Marinduque");
        int islets = phIsleName.Count(n => n != null && n.StartsWith("an islet"));
        Check($"every bit of land has a name ({phIsleName.Count - 1} islands, {islets} of them islets named after their neighbours)", phIsleName.Skip(1).All(n => !string.IsNullOrEmpty(n)) && islets > 10);
        // The towns.
        var towns = PhTowns();
        Check($"{towns.Count} of {PhTownDefs.Length} towns are laid out", towns.Count == PhTownDefs.Length);
        foreach (var t in towns)
        {
            bool land = PhKindAt(t.Cx, t.Cy) == 3;
            int houses = t.Things.Count(h => h.Kind is "house" or "inn");
            bool pier = t.PierLen > 0 && Enumerable.Range(0, t.PierLen).All(k => PhKindAt(t.PierX + t.PierDx * k, t.PierY + t.PierDy * k) is 0 or 1);
            Check($"{t.Def.Name} ({t.Def.Island}): a plaza on land, a church, a market, an inn and {houses} houses, a pier into the sea ({land} {t.Church != null} {t.Market != null} {t.Inn != null} {pier}, pier {t.PierLen})",
                land && (t.Church != null) == !PhVillages.Contains(t.Def.Id) && t.Market != null && t.Inn != null && houses >= (PhVillages.Contains(t.Def.Id) ? 1 : 2) && pier);
        }
        // No building stands behind another where it can't be seen: their pictures never overlap.
        int hidden = 0; string hiddenAt = "";
        foreach (var t in towns)
        {
            var boxes = t.Things.Where(h => h.Kind != "prop").Select(h =>
            {
                var (grid, _, ax, ay, _, _) = PhArt(h.Art);
                int half = Math.Max(ax, grid[0].Length - ax);
                return (x0: h.X - half, y0: h.Y - ay, x1: h.X + half, y1: h.Y - ay + grid.Length);
            }).ToList();
            for (int a = 0; a < boxes.Count; a++)
                for (int b = a + 1; b < boxes.Count; b++)
                    if (boxes[a].x1 > boxes[b].x0 && boxes[a].x0 < boxes[b].x1 && boxes[a].y1 > boxes[b].y0 && boxes[a].y0 < boxes[b].y1) { hidden++; hiddenAt = t.Def.Name; }
        }
        Check($"no building is drawn over another ({hidden} overlaps{(hidden > 0 ? ", " + hiddenAt : "")})", hidden == 0);
        Check($"Marinduque has one church (Boac's) and the church is smaller than before ({PhTownArt.Church[0].Length} px wide)",
            towns.Count(t => t.Def.Island == "Marinduque" && t.Church != null) == 1 && PhTownArt.Church[0].Length < 60);
        // Sailing into a town (the window moving bit by bit, not baked from scratch) still draws its pier: it used to be
        // invisible, so you seemed to walk on the water.
        var dum = PhTownById("dumaguete");
        var (dlat, dlon) = PhLatLon((dum.Cx - 70) * T, dum.Cy * T);
        PhTeleport(dlat, dlon, land: false); yield return 2;
        for (int k = 0; k < 6 && bufX0 / T + PhWin / 2 < dum.Cx; k++)
        {
            PhMoveWindow(bufX0 / T + 16, bufY0 / T);
            while (phQueue.Count > 0) PhBakeStep(int.MaxValue);
            yield return 1;
        }
        bool inWin = TileInBuf(dum.PierX, dum.PierY);
        var pierPx = new List<Raylib_cs.Color>();
        for (int k = 0; k < dum.PierLen; k++)
            for (int p = 2; p < 8; p++)
            {
                int wx = (dum.PierX + dum.PierDx * k) * T + p, wy = (dum.PierY + dum.PierDy * k) * T + 5;
                pierPx.Add(worldBase.Buf[BI(wx, wy)]);
            }
        PhBakeWindow(bufX0 / T, bufY0 / T);
        int differ = 0, n = 0;
        for (int k = 0; k < dum.PierLen; k++)
            for (int p = 2; p < 8; p++)
            {
                int wx = (dum.PierX + dum.PierDx * k) * T + p, wy = (dum.PierY + dum.PierDy * k) * T + 5;
                var c = worldBase.Buf[BI(wx, wy)];
                if (!c.Equals(pierPx[n++])) differ++;
            }
        Check($"a pier reached by sailing is drawn (in the window {inWin}; {differ} of {n} pier pixels differ from a fresh bake)", inWin && differ == 0);
        // The land's shape: real mountains and volcanoes at their real heights.
        PhTerrainField();
        float ElevOf(double la, double lo) { var (tx, ty) = PhTileOf(la, lo); return PhElevTile((int)tx, (int)ty); }
        Check($"Mayon, Apo, Pulag and Malindig stand at about their real heights ({ElevOf(13.257, 123.685):0}, {ElevOf(6.99, 125.27):0}, {ElevOf(16.583, 120.887):0}, {ElevOf(13.24, 122.02):0} m), Manila Bay's shore low ({ElevOf(14.59, 120.98):0} m)",
            ElevOf(13.257, 123.685) > 2000 && ElevOf(6.99, 125.27) > 2400 && ElevOf(16.583, 120.887) > 2300 && ElevOf(13.24, 122.02) > 800 && ElevOf(14.59, 120.98) < 150);
        { var (tx, ty) = PhTileOf(14.002, 120.995); var (px, py) = PhTileOf(15.143, 120.35);
          Check($"Taal Volcano is an island in its lake, and Pinatubo has its crater lake ({PhCell((int)tx, (int)ty)}, {PhCell((int)px, (int)py)})", PhCell((int)tx, (int)ty) == 1 && PhCell((int)px, (int)py) == 2); }
        int mqTrees = trees.Count(tr => PhIsleName(PhIsleAt(tr.x, tr.y)) == "Marinduque");
        Check($"Marinduque keeps its trees round its towns ({mqTrees} trees and palms on it)", mqTrees > 40);
        bool distinct = PhTowns().All(t => t.Things.Where(h => h.Kind is "house" or "inn").Select(h => h.Art).Distinct().Count() == t.Things.Count(h => h.Kind is "house" or "inn"));
        Check("no town has the same house twice", distinct);
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PhBakeWindow(bufX0 / T, bufY0 / T);
            Check($"baking a window with the mountains is still quick ({sw.ElapsedMilliseconds} ms)", sw.ElapsedMilliseconds < 4000);
        }
        // The mountains' drawings stand on their own land, and never over a town's buildings.
        {
            var mts = PhMountains();
            Note("mountains: " + string.Join(", ", mts.Select(m => m.p.Name + "=" + m.art)));
            Note("without: " + string.Join(", ", PhPeaks.Where(p => PhPeakArt.ContainsKey(p.Name) && !mts.Any(m => m.p == p)).Select(p => p.Name)));
            int over = 0; string overAt = "";
            foreach (var (p, art, mx, my) in mts)
            {
                var (grid, _, ax, ay, foot, _, _) = PhMountain(art);
                float x0 = mx - ax, y0 = my - ay, x1 = x0 + grid[0].Length, y1 = my + foot;
                foreach (var t in PhTowns())
                    foreach (var h in t.Things.Where(h => h.Kind != "prop"))
                    {
                        var (hg, _, hax, hay, _, _) = PhArt(h.Art);
                        if (x0 < h.X - hax + hg[0].Length && x1 > h.X - hax && y0 < h.Y && y1 > h.Y - hay) { over++; overAt = $"{p.Name} over {t.Def.Name}"; }
                    }
            }
            Check($"{mts.Count} mountains drawn; none stands over a town's buildings ({over}{(over > 0 ? ", " + overAt : "")})", over == 0 && mts.Count > 20);
            Check("Mayon, Apo, Kanlaon, Pinatubo, Taal, Malindig and Pulag all have theirs", new[] { "Mayon Volcano", "Mount Apo", "Kanlaon Volcano", "Mount Pinatubo", "Taal Volcano", "Mount Malindig", "Mount Pulag" }.All(n => mts.Any(m => m.p.Name == n)));
        }
        // Mountains are solid: walking straight at Mayon from the south stops at its foot (you used to walk into the slope).
        {
            var may = PhMountains().First(m => m.p.Name == "Mayon Volcano");
            var (mla, mlo) = PhLatLon(may.x, may.y + PhMountainArt.MayonFoot + 40);
            PhTeleport(mla, mlo); yield return 4;
            float startY = player.Y;
            Inp.Hold(Raylib_cs.KeyboardKey.Up, true);
            for (int i = 0; i < 240; i++) yield return 1;
            Inp.Hold(Raylib_cs.KeyboardKey.Up, false); yield return 2;
            bool inside = PhMountainBlocks(player.X, player.Y);
            Check($"walking at Mayon stops at its foot (from {startY - may.y:0} to {player.Y - may.y:0} px from its middle; foot at {PhMountainArt.MayonFoot}, never inside it)",
                !inside && player.Y > may.y && player.Y < startY - 10);
            pendingPixShot = "ph-land-mayon-foot"; yield return 3;
        }
        // Each mountain in its own colours: a cache keyed on the first and last colours gave Pinatubo another mountain's, its
        // crater lake brown.
        {
            var pina = PhMountains().First(m => m.p.Name == "Mount Pinatubo");
            var (pla2, plo2) = PhLatLon(pina.x, pina.y + PhMountainArt.PinatuboFoot + 24);
            PhTeleport(pla2, plo2); yield return 8;   // (in front of it: from behind it fades)
            var lakeCols = new[] { "#2f6f78", "#3d8a90", "#63b2b0" }.Select(Pal.C).ToArray();
            int lakePx = pix.Buf.Count(c => lakeCols.Any(l => Math.Abs(l.R - c.R) + Math.Abs(l.G - c.G) + Math.Abs(l.B - c.B) < 30));
            Check($"Pinatubo's crater lake is drawn in its own blue ({lakePx} pixels of it in view)", lakePx > 40);
        }
        // Screenshots of the landmarks, lit as you'd see them.
        {
            bool keepFx = EffectsOff; EffectsOff = false; SetNight(false); ClearSkies(); SnapWeather();
            foreach (var (name, la, lo) in new[] { ("bacolod", 10.66, 122.97), ("mayon", 13.21, 123.685), ("taal", 13.96, 120.995), ("pinatubo", 15.10, 120.35), ("apo", 6.95, 125.27), ("malindig", 13.20, 122.02), ("kanlaon", 10.37, 123.13), ("cordillera", 16.55, 120.887), ("marinduque", 13.40, 121.95) })
            {
                PhTeleport(la, lo); yield return 6;
                pendingPixShot = $"ph-land-{name}"; yield return 3;
                if (name is "mayon" or "apo" or "pinatubo" or "taal" or "cordillera") Check($"{name}: its mountain is drawn ({mountainsDrawn} in view)", mountainsDrawn > 0);
            }
            EffectsOff = keepFx;
        }
        // Walking into Boac.
        var boac = PhTownById("boac");
        var (bla, blo) = PhLatLon(boac.Cx * T + 5, boac.Cy * T + 8);
        PhTeleport(bla, blo); yield return 10;
        Check($"arriving in Boac names it and puts it on the chart ({AreaName()})", AreaName().StartsWith("Boac") && state.phVisited.Contains("boac"));
        Check($"its buildings are solid ({PhTownSolids().Count} boxes near you)", PhTownSolids().Count > 8 && !CanStand(boac.Church.X, boac.Church.Y - 4));
        Check("its plaza is paved and its pier is a deck", worldMap[boac.Cy, boac.Cx] == 'p' && worldMap[boac.PierY, boac.PierX] is 'd' or 'b');
        SetNight(false); yield return 4;
        pendingPixShot = "ph-10-boac-town"; yield return 3;
        pendingShot = "ph-11-boac-hud"; yield return 3;
        // Villagers and animals strolling about never walk into (or behind) a building's picture.
        {
            var bld = boac.Things.Where(h => h.Kind != "prop").Select(PhSolidOf).ToList();
            bool Inside(float x, float y) => bld.Any(b => x + 2 > b.x0 && x - 2 < b.x0 + b.w && y > b.y0 && y - 2 < b.y0 + b.h);
            standStill = false; quietWildlife = false; phAnimalT = 0;
            int bad = 0, samples = 0; string badWho = "";
            for (int f = 0; f < 600; f++)
            {
                foreach (var id in boac.Folk) { var s = PhFolkWalk(id); samples++; if (Inside(s.X, s.Y)) { bad++; badWho = id; } }
                foreach (var a in animals.Where(a => a.Key.StartsWith("ph:boac"))) { samples++; if (Inside(a.X, a.Y)) { bad++; badWho = a.Key; } }
                yield return 1;
            }
            standStill = true; quietWildlife = true;
            Check($"nobody walks through a building in Boac ({bad} of {samples} positions inside one{(bad > 0 ? ", " + badWho : "")})", bad == 0);
        }
        // A villager.
        string fid = boac.Folk[^1];
        yield return 2;   // (back home after the stroll above)
        var fw = PhFolkWalk(fid);
        player.X = fw.X; player.Y = fw.Y + 8; player.Face = "up"; yield return 3;
        Check($"standing by a villager offers a talk ({target?.Label})", target?.Type == "phfolk");
        Inp.Tap(Raylib_cs.KeyboardKey.E); yield return 4;
        Check($"and E talks to them ({mode})", mode == "dialogue");
        pendingShot = "ph-12-villager"; yield return 3;
        for (int i = 0; i < 10 && mode == "dialogue"; i++) { Inp.Tap(Raylib_cs.KeyboardKey.E); yield return 3; }
        // The market, by real clicks: E, then Sell all.
        var mk = boac.Market;
        (player.X, player.Y) = PhStandNear(mk.X + 10, mk.Y + 12); player.Face = "up"; yield return 3;
        Check($"the market's vendor trades ({target?.Label})", target?.Type == "phmarket");
        Give("tulingan", 3); Give("lapu_lapu", 1);
        int coins0 = state.coins;
        for (int i = 0; i < 6 && mode != "panel"; i++) { Inp.Tap(Raylib_cs.KeyboardKey.E); yield return 4; }
        Check($"E opens the town's stall ({shopName})", mode == "panel" && panel == "shop" && shopName?.Contains("Boac") == true);
        shopTab = "sell"; yield return 3;
        pendingShot = "ph-13-market"; yield return 3;
        ClickButton("Sell all ordinary fish"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"selling the fish by a real click pays for them ({coins0} -> {state.coins})", state.coins > coins0 && Has("tulingan") == 0);
        ClosePanels(); yield return 3;
        // The inn.
        (player.X, player.Y) = PhStandNear(boac.Inn.X, boac.Inn.Y + 5); player.Face = "up"; yield return 3;
        Check($"the inn offers a rest ({target?.Label})", target?.Type == "rest");
        // Fishing spots.
        var spots = PhSpots();
        int named = spots.Count(p => p.Spot.Scene == "ph");
        Check($"{named} fishing spots placed ({spots.Count(p => p.Town != null)} town piers)", named >= PhSpotDefs.Length + towns.Count - 4);
        foreach (var p in spots.Where(p => p.Spot.Scene == "ph"))
        {
            var s = p.Spot;
            bool reach = p.Hab == "deep" ? BoatCanStand(s.X, s.Y) : PhShoreNear(s.X, s.Y, s.R);
            var pool = PhPool(s.Id);
            bool own = pool.Length > 0 && pool.All(f => PhHabitats[p.Hab].Contains(f.Id));
            Check($"{s.Label} ({p.Hab}): {(p.Hab == "deep" ? "a boat can sit on it" : "fishable from the shore")}, and its own fish ({pool.Length})", reach && own);
        }
        // Fish a spot for real: Boac's pier.
        var pierSpot = spots.First(p => p.Spot.Id == "ph:pier:boac").Spot;
        var stand = PhPierStand(boac);
        player.X = stand.x; player.Y = stand.y; FaceToward(pierSpot.X, pierSpot.Y); yield return 4;
        Check($"at the end of Boac's pier, E fishes ({target?.Id}, standing {CanStand(player.X, player.Y)})", target?.Type == "spot" && target.Id == pierSpot.Id);
        var w = FishWeights(pierSpot.Id, null, 1);
        Check($"and it bites the bay's fish ({string.Join(", ", w.Select(q => q.f.Id))})", w.Count > 3 && w.All(q => PhHabitats["bay"].Contains(q.f.Id)));
        Inp.Hold(Raylib_cs.KeyboardKey.E, true); yield return 12; Inp.Hold(Raylib_cs.KeyboardKey.E, false);
        for (int i = 0; i < 400 && mode is "casting" or "waiting" or "charging"; i++) yield return 1;
        Check($"a cast off the pier gets a bite ({mode})", mode is "bite" or "reeling");
        pendingPixShot = "ph-14-pier-fishing"; yield return 2;
        fish = null; reel = null; mode = "play"; yield return 2;
        // Casting anywhere: the water decides.
        var laguna = spots.First(p => p.Spot.Id == "ph:laguna").Spot;
        Check($"a cast into Laguna de Bay gets the lake's fish ({PhWildSpot(laguna.X, laguna.Y)})", PhWildSpot(laguna.X, laguna.Y) == "ph:laguna");
        var (seaX, seaY) = PhPlace(12.0, 119.0);
        Check($"the open Sulu Sea gets the open sea's ({PhWildSpot(seaX, seaY)})", PhWildSpot(seaX, seaY) == "ph:sea" && FishWeights("ph:sea", null, 1).All(q => PhHabitats["sea"].Contains(q.f.Id)));
        // Animals: near you, none far off.
        PhTeleport(bla, blo); yield return 4;
        phAnimalT = 0; yield return 3;
        var ph = animals.Where(a => a.Key.StartsWith("ph:")).ToList();
        float far = (PhAnimalNear + 1.5f) * PhAnimalCell * T;
        Check($"animals come round you ({ph.Count}: {string.Join(", ", ph.Select(a => a.Kind).Distinct())})", ph.Count >= 4 && ph.All(a => Math.Abs(a.X - player.X) < far && Math.Abs(a.Y - player.Y) < far));
        Check("Boac's dogs are dogs, not Biscuit", ph.Where(a => a.Kind == "dog").All(a => !AnimalLabel(a).Contains("Biscuit")));
        PhTeleport(9.74, 118.74); yield return 4; phAnimalT = 0; yield return 3;
        Check($"sail away and the old ones go ({animals.Count(a => a.Key.StartsWith("ph:boac"))} of Boac's left)", !animals.Any(a => a.Key.StartsWith("ph:boac")));
        foreach (var tid in new[] { "santacruz", "dumaguete", "cebu", "davao", "legazpi", "puertoprincesa" })
        {
            var tt = PhTownById(tid);
            var (tla, tlo) = PhLatLon(tt.Cx * T + 5, tt.Cy * T + 8);
            PhTeleport(tla, tlo); yield return 6;
            pendingPixShot = $"ph-15-town-{tid}"; yield return 2;
        }
        // The chart: opens on you zoomed in, zooms by its buttons, a click drops a pin.
        PhTeleport(StartLat, StartLon); yield return 4;
        TogglePanel("map"); yield return 4;
        Check($"the chart opens on you, zoomed in ({phZoom}x)", mode == "panel" && panel == "map" && phZoom > 1 && Math.Abs(phCx - player.X / T) < 60);
        pendingShot = "ph-16-chart-near"; yield return 3;
        float z0 = phZoom;
        ClickButton("+"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"+ zooms in ({z0} -> {phZoom})", phZoom > z0);
        pendingShot = "ph-17-chart-closer"; yield return 3;
        ClickButton("Whole map"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"Whole map zooms out ({phZoom})", phZoom == 1);
        pendingShot = "ph-18-chart-whole"; yield return 3;
        ClickButton("Find me"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        state.pinX = state.pinY = 0;
        var r = Gfx.Seen["phchart"];
        Inp.ScriptMouse = new System.Numerics.Vector2(r.X + r.Width * 0.3f, r.Y + r.Height * 0.4f);
        yield return 2;
        Inp.ScriptClickNext = true; yield return 4;
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"a click on the chart drops a pin ({state.pinX:0},{state.pinY:0})", HasPin);
        // Dragging moves it, without dropping a pin.
        float cx0 = phCx, pinX0 = state.pinX;
        Inp.ScriptMouse = new System.Numerics.Vector2(r.X + r.Width * 0.6f, r.Y + r.Height * 0.5f); yield return 2;
        Inp.ScriptMouseDown = true; yield return 2;
        for (int i = 0; i < 8; i++) { Inp.ScriptMouse = new System.Numerics.Vector2(r.X + r.Width * 0.6f - i * 12, r.Y + r.Height * 0.5f); yield return 1; }
        Inp.ScriptMouseDown = false; yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"dragging moves the chart ({cx0:0} -> {phCx:0}) and leaves the pin", phCx > cx0 + 5 && state.pinX == pinX0);
        pendingShot = "ph-19-chart-pin"; yield return 3;
        ClosePanels(); state.pinX = state.pinY = 0; yield return 2;
        // A save keeps the towns you've been to.
        int visited = state.phVisited.Count;
        Save(); yield return 1;
        state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 4;
        Check($"a reload keeps the towns you've been to ({state.phVisited.Count} of {visited})", state.phVisited.Count == visited && visited >= 5);
    }

    // A point on dry land (or a pier) within reach of a shore spot.
    bool PhShoreNear(float sx, float sy, float r)
    {
        for (float d = 4; d < r; d += 3)
            for (int a = 0; a < 24; a++)
            {
                float x = sx + MathF.Cos(a * MathF.Tau / 24) * d, y = sy + MathF.Sin(a * MathF.Tau / 24) * d;
                int tx = (int)(x / T), ty = (int)(y / T);
                if (PhKindAt(tx, ty) == 3 || PhTownTile(tx, ty) is 'd' or 'b') return true;
            }
        return false;
    }

    // The last plank of a town's pier.
    (float x, float y) PhPierStand(PhTown t)
    {
        int k = t.PierLen - 1;
        return ((t.PierX + t.PierDx * k) * T + 5, (t.PierY + t.PierDy * k) * T + 7);
    }
}
#endif
