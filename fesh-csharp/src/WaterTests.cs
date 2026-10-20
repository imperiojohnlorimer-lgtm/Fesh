#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    static int WLuma(Color c) => (299 * c.R + 587 * c.G + 114 * c.B) / 1000;

    // The water pass (FESH_WATER_TEST=1, also at the end of the full run after the light script): the richer baked sea, then
    // the swell, rain and reflections as they are added. Light is on for these checks and the other scripts' off state is
    // restored at the end.
    IEnumerable<int> WaterScript()
    {
        Note("Water: the richer baked sea");
        state = new State { created = true, look = new Look { name = "Wader" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;
        EffectsOff = false; Settings.Data.light = 2;

        /* ---------- Phase A: the baked layer ---------- */
        {
            long Sum(Pix p) { long s = 0; for (int i = 0; i < p.Buf.Length; i += 61) s = s * 31 + p.Buf[i].R + p.Buf[i].G * 3 + p.Buf[i].B * 7; return s; }
            long baseBefore = Sum(worldBase);
            double t0 = Raylib.GetTime();
            groundDecorOff = true;   // the sea checks judge the water-only layer (the land decor has its own tests, GroundTests.cs)
            worldRich = null; var rich = LiveBase();
            double bakeMs = (Raylib.GetTime() - t0) * 1000;
            Note($"the rich sea bakes in {bakeMs:0} ms");
            Check($"the richer sea bakes in good time ({bakeMs:0} ms in a Debug build)", bakeMs < 4000);
            Check("the ordinary baked layer is untouched by it", Sum(worldBase) == baseBefore);
            EffectsOff = true;
            Check("with light off the view still starts from the ordinary baked layer", ReferenceEquals(LiveBase(), basePix));
            EffectsOff = false;

            int water = 0, changed = 0, elsewhereDiffers = 0, rimDiffers = 0;
            int shallowMin = 255, deepMax = 0;
            for (int wy = 0; wy < PH; wy++)
                for (int wx = 0; wx < PW; wx++)
                {
                    int i = wy * PW + wx; char s = shape[i]; var b = worldBase.Buf[i]; var r = rich.Buf[i];
                    bool plain = s is 'w' or '~' && b.Equals(GroundPixel(wx, wy, s)) && !b.Equals(ShallowRim);
                    if (!plain)
                    {
                        if (!b.Equals(r)) { elsewhereDiffers++; if (b.Equals(ShallowRim)) rimDiffers++; }
                        continue;
                    }
                    water++;
                    if (!b.Equals(r)) changed++;
                    int l = WLuma(r);
                    if (s == 'w') shallowMin = Math.Min(shallowMin, l); else deepMax = Math.Max(deepMax, l);
                }
            Check($"only plain open-sea pixels are repainted: none of the other {PW * PH - water} pixels, rim included ({elsewhereDiffers} differ, {rimDiffers} rim)", elsewhereDiffers == 0 && rimDiffers == 0);
            Check($"the richer sea really is richer: {changed} of {water} sea pixels take a new colour", water > 100000 && changed > water / 5);
            Check($"every shallow pixel stays lighter than every deep one (the lightest deep {deepMax}, the darkest shallow {shallowMin})", shallowMin > deepMax);

            // Distinct shallow and deep tones: the gradient has more steps than before.
            var shallowSet = new HashSet<int>(); var deepSet = new HashSet<int>();
            for (int wy = 0; wy < PH; wy += 3)
                for (int wx = 0; wx < PW; wx += 3)
                {
                    int i = wy * PW + wx; char s = shape[i]; var r = rich.Buf[i];
                    if (s == 'w') shallowSet.Add(r.R << 16 | r.G << 8 | r.B); else if (s == '~') deepSet.Add(r.R << 16 | r.G << 8 | r.B);
                }
            Check($"the sea has many more tones than before ({shallowSet.Count} shallow, {deepSet.Count} deep colours)", shallowSet.Count >= 10 && deepSet.Count >= 12);

            // The wading line: at every place where shallow meets deep, in all four directions, the shallow side is still
            // about as much lighter than the deep side as it was.
            int pairs = 0, weak = 0; double gapBase = 0, gapRich = 0;
            foreach (var (ddx, ddy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                for (int wy = 6; wy < PH - 6; wy += 2)
                    for (int wx = 6; wx < PW - 6; wx += 2)
                    {
                        int i = wy * PW + wx, j = (wy + ddy * 3) * PW + wx + ddx * 3, m1 = (wy + ddy) * PW + wx + ddx;
                        if (shape[i] != 'w' || shape[j] != '~' || shape[m1] != 'w') continue;
                        int gb = WLuma(worldBase.Buf[i]) - WLuma(worldBase.Buf[j]), gr = WLuma(rich.Buf[i]) - WLuma(rich.Buf[j]);
                        gapBase += gb; gapRich += gr; pairs++;
                        if (gr < gb - 6) weak++;
                    }
            Check($"the wading line stays readable everywhere: {gapRich / Math.Max(1, pairs):0.0} against {gapBase / Math.Max(1, pairs):0.0} levels across it on average, {weak} of {pairs} places weaker by more than 6", pairs > 200 && gapRich >= gapBase * 0.9 && weak <= pairs / 30);
            groundDecorOff = false; worldRich = null; LiveBase();   // from here the layer is the whole thing, land decor included

            // Change a tile on the coast (a beach tile becomes shallow water), repaint it as the game does, and the rich layer
            // must really change and equal a whole new bake; putting the tile back must restore it exactly.
            (int x, int y)? beach = null;
            for (int y = 3; y < ROWS - 3 && beach == null; y += 3)
                for (int x = 3; x < COLS - 3 && beach == null; x += 3)
                    if (worldMap[y, x] == 's' && GroundAt(x, y) == 's' && GroundAt(x + 1, y) == 'w' && GroundAt(x - 1, y) == 's' && GroundAt(x, y + 1) == 's') beach = (x, y);
            Check($"a beach tile with shallow water beside it was found ({beach})", beach != null);
            var (bx, by) = beach ?? (3, 3);
            var before = (Color[])worldRich.Buf.Clone(); var baseWas = (Color[])worldBase.Buf.Clone();
            char was = worldMap[by, bx];
            worldMap[by, bx] = 'w'; RenderTile(bx, by);
            int moved = 0; for (int i = 0; i < before.Length; i++) if (!before[i].Equals(worldRich.Buf[i])) moved++;
            var incremental = (Color[])worldRich.Buf.Clone();
            BakeRich();
            int diff = 0; for (int i = 0; i < incremental.Length; i++) if (!incremental[i].Equals(worldRich.Buf[i])) diff++;
            Check($"after the tile became water the rich layer changed ({moved} pixels) and equals a whole new bake of it ({diff} differ)", moved > 20 && diff == 0);
            worldMap[by, bx] = was; RenderTile(bx, by);
            int restored = 0; for (int i = 0; i < before.Length; i++) if (!before[i].Equals(worldRich.Buf[i]) || !baseWas[i].Equals(worldBase.Buf[i])) restored++;
            Check($"putting the tile back restores both layers exactly ({restored} pixels differ)", restored == 0);

            // Screenshots: the coast, a reef and a lighthouse, with light off and on.
            foreach (var (id, x, y) in new[] { ("coast", 230f, 60f), ("atoll", 935f, 262f), ("amihan", 1625f, 192f), ("asinan", 265f, 643f), ("parola", LighthouseX - 90, LighthouseY + 14), ("luntian", 2040f, 138f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (int lm in new[] { 0, 2 })
                {
                    EffectsOff = lm == 0; Settings.Data.light = 2; state.clock = 12 * 60; ClearSkies(); yield return 4;
                    pendingPixShot = $"water-{id}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                }
            }
            EffectsOff = false;
        }

        /* ---------- Phase B: the swell, the coast index, foam and boats ---------- */
        {
            Note("Water: the swell and the coast index");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3; state.clock = 12 * 60;
            long ShapeSum() { long s = 0; for (int i = 0; i < shape.Length; i += 53) s = s * 31 + shape[i]; return s; }
            long shapeBefore = ShapeSum(), baseBefore2 = PixHash(worldBase);

            // The coast index returns everything near the view, in the full list's order.
            bool allIn = true, ordered = true; int checkedViews = 0, listed = 0;
            foreach (var (x, y) in new[] { (160f, 115f), (935f, 262f), (2040f, 138f), (265f, 643f), (LighthouseX, LighthouseY + 14), (30f, 30f), (3300f, 700f) })
            {
                (player.X, player.Y) = StandableNear(x, y); yield return 4;
                var view = ShoreInView(); listed += view.Length; checkedViews++;
                for (int k = 1; k < view.Length; k++) if (view[k] <= view[k - 1]) ordered = false;
                var set = new HashSet<int>(view);
                foreach (int margin in new[] { 1, 3, 10 })
                    for (int i = 0; i < shore.Count; i++)
                        if (shore[i].x >= camX - margin && shore[i].x <= camX + W + margin && shore[i].y >= camY - margin && shore[i].y <= camY + H + margin && !set.Contains(i)) allIn = false;
            }
            Check($"the coast index lists every shore pixel near the view, in order ({checkedViews} views, {listed} pixels)", allIn && ordered && listed > 500);
            // The foam it draws with light off is exactly what the full scan drew.
            (player.X, player.Y) = StandableNear(230, 60); yield return 4;
            Color[] FoamPicture(bool full)
            {
                var keep = pix; pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(30, 90, 140, 255)); pix.CamX = camX; pix.CamY = camY;
                float t = 3.3f;
                if (!full) DrawFoam(t);
                else
                {
                    // The old pass, over the whole list.
                    int x0 = camX - 3, x1 = camX + W + 3, y0 = camY - 3, y1 = camY + H + 3;
                    var bright = Pal.Rgba(236, 248, 252, 0.85f); var dim = Pal.Rgba(236, 248, 252, 0.3f); var wash = Pal.Rgba(236, 248, 252, 0.32f); var murky = Pal.Rgba(196, 214, 168, 0.45f);
                    foreach (var (sx, sy, nx, ny, w) in shore)
                    {
                        if (sx < x0 || sx > x1 || sy < y0 || sy > y1) continue;
                        bool on = (Pix.Hash(sx / 3, sy / 3, 5) + t * 0.3) % 1 < 0.55;
                        if (w == 'm') { if (on) pix.Rect(sx, sy, 1, 1, murky); continue; }
                        pix.Rect(sx, sy, 1, 1, on ? bright : dim);
                        if (!on || ((sx + sy) & 1) != 0) continue;
                        int off = 2 + (int)MathF.Round(MathF.Sin(t * 1.8f + sx * 0.11f + sy * 0.13f));
                        pix.Rect(sx + nx * off, sy + ny * off, 1, 1, wash);
                    }
                }
                var result = pix.Buf; pix = keep; return result;
            }
            EffectsOff = true;
            var viaIndex = FoamPicture(false); var viaList = FoamPicture(true);
            EffectsOff = false;
            Check("with light off the foam is exactly what the full scan of the coast drew", viaIndex.SequenceEqual(viaList) && viaIndex.Any(c => c.R > 100));

            // The swell: a wave 80 px long and 5 s between crests, running downwind, never beyond +-1.
            float worst = 0, drift = 0, space = 0;
            var (wdx, wdy) = SwellDir;
            for (int i = 0; i < 200; i++)
            {
                float x = i * 13.7f, y = i * 7.3f, t = i * 0.37f;
                float s0 = SeaSwell(x, y, t);
                worst = Math.Max(worst, Math.Abs(s0));
                drift = Math.Max(drift, Math.Abs(SeaSwell(x, y, t + 5) - s0));
                space = Math.Max(space, Math.Abs(SeaSwell(x + wdx * 80, y + wdy * 80, t) - s0));
            }
            Check($"the swell stays within +-1, repeats every 5 seconds and every 80 px along the wind ({worst:0.000}, {drift:0.0000}, {space:0.0000})", worst <= 1.0001f && drift < 0.001f && space < 0.001f);
            state.day = 1; var amihan = SwellDir; state.day = 6; var habagat = SwellDir; state.day = 3;
            Check($"the swell turns with the monsoon ({amihan.x:0.0},{amihan.y:0.0} then {habagat.x:0.0},{habagat.y:0.0})", amihan.x < 0 && amihan.y > 0 && habagat.x > 0 && habagat.y < 0);

            // The boat's bob: the old one with light off; a pixel or so calm, about two in a storm, following the swell by place.
            EffectsOff = true;
            bool same = true; for (int i = 0; i < 50; i++) if (BoatBobAt(i * 0.3f, 500, 300, false) != BoatBob(i * 0.3f)) same = false;
            EffectsOff = false;
            Check("with light off the boat bobs exactly as before", same);
            int calmMax = 0, stormMax = 0, apart = 0;
            for (int i = 0; i < 400; i++)
            {
                float t = i * 0.13f;
                calmMax = Math.Max(calmMax, Math.Abs(BoatBobAt(t, 500, 300, false)));
                if (BoatBobAt(t, 500, 300, false) != BoatBobAt(t, 540, 300, false)) apart++;
            }
            state.forecast = new() { new WeatherSpell { at = 0, w = "storm" } }; state.weather = planned = "storm"; SnapWeather(); yield return 3;
            for (int i = 0; i < 400; i++) stormMax = Math.Max(stormMax, Math.Abs(BoatBobAt(i * 0.13f, 500, 300, true)));
            Check($"the bob is about a pixel in calm ({calmMax}) and about two in a storm ({stormMax}), and differs between places ({apart} of 400 times)", calmMax == 1 && stormMax == 2 && apart > 40);

            // Foam builds with the weather and moves with the swell.
            (player.X, player.Y) = StandableNear(230, 60); yield return 4;
            int FoamPixels(float t)
            {
                var keep = pix; pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(30, 90, 140, 255)); pix.CamX = camX; pix.CamY = camY;
                DrawFoam(t); int n = 0; foreach (var c in pix.Buf) if (c.R > 150) n++;
                pix = keep; return n;
            }
            int stormy = FoamPixels(3.3f);
            ClearSkies(); yield return 3;
            int calm = FoamPixels(3.3f);
            Check($"foam breaks over more of the shore in a storm ({stormy} against {calm} bright pixels)", stormy > calm * 1.1f);
            // Only the wash follows the swell; the strokes themselves don't, so turning the wind round (the other monsoon) at the
            // same moment moves the wash and nothing else.
            state.day = 1; var a1 = FoamPictureAt(3.5f); state.day = 6; var a2 = FoamPictureAt(3.5f); state.day = 3;
            int moved = 0; for (int i = 0; i < a1.Length; i++) if (!a1[i].Equals(a2[i])) moved++;
            var b1 = FoamPictureAt(1.0f); var b2 = FoamPictureAt(3.5f);
            int animated = 0; for (int i = 0; i < b1.Length; i++) if (!b1[i].Equals(b2[i])) animated++;
            Check($"the wash follows the swell: the other monsoon's wind moves it ({moved} pixels) while the foam keeps animating ({animated})", moved > 20 && animated > 100);
            Check("none of it touches the baked coast, the wading line or the ground layer", ShapeSum() == shapeBefore && PixHash(worldBase) == baseBefore2);

            // The cost of the three coast passes.
            Settings.Data.zoom = 2; yield return 4; (player.X, player.Y) = StandableNear(230, 60); yield return 4;
            var keepPix = pix; pix = new Pix(W, H); pix.CamX = camX; pix.CamY = camY;
            double c0 = Raylib.GetTime();
            for (int i = 0; i < 30; i++) { DrawWaves(i * 0.1f); DrawFoam(i * 0.1f); DrawMoonlitShore(i * 0.1f); }
            double passMs = (Raylib.GetTime() - c0) / 30 * 1000;
            pix = keepPix;
            Check($"the coast passes cost little at the widest zoom ({passMs:0.00} ms for foam, wash and surf in a Debug build)", passMs < 6);
            Settings.Data.zoom = 4; yield return 4;
            (player.X, player.Y) = StandableNear(230, 60); state.clock = 12 * 60; yield return 3;
            foreach (var w in new[] { "clear", "storm" })
            {
                if (w == "storm") { state.forecast = new() { new WeatherSpell { at = 0, w = "storm" } }; state.weather = planned = "storm"; SnapWeather(); } else ClearSkies();
                yield return 4; pendingPixShot = $"water-coast-{w}"; yield return 2;
            }
            ClearSkies();
        }

        /* ---------- Phase C: rain and storm on the sea ---------- */
        {
            Note("Water: rain and chop");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3; state.clock = 12 * 60; yield return 3;
            // A window of the world that is all deep sea, to judge the rain and the chop by themselves.
            int seaX = -1, seaY = -1;
            for (int cy = 20; cy < PH - H - 20 && seaX < 0; cy += 40)
                for (int cx = 20; cx < PW - W - 20 && seaX < 0; cx += 40)
                {
                    bool allDeep = true;
                    for (int y = cy; y < cy + H && allDeep; y += 6) for (int x = cx; x < cx + W; x += 6) if (ShapePx(x, y) != '~') { allDeep = false; break; }
                    if (allDeep) { seaX = cx; seaY = cy; }
                }
            Check($"a view of nothing but open sea was found ({seaX}, {seaY})", seaX >= 0);
            var bg = new Color(30, 90, 140, 255);
            // What a pass paints on a flat picture at a view: its pixels with their changes.
            List<(int x, int y, int delta)> Paint(int vx, int vy, Action draw)
            {
                var keep = pix; var (kx, ky) = (camX, camY);
                pix = new Pix(W, H); Array.Fill(pix.Buf, bg); pix.CamX = camX = vx; pix.CamY = camY = vy;
                draw();
                var found = new List<(int, int, int)>();
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { var c = pix.Buf[y * W + x]; int d = Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G) + Math.Abs(c.B - bg.B); if (d > 0) found.Add((x + vx, y + vy, d)); }
                pix = keep; camX = kx; camY = ky;
                return found;
            }
            void Weather(string w, float amount = 1) { state.forecast = new() { new WeatherSpell { at = 0, w = w } }; state.weather = planned = w; SnapWeather(); rainAmt = w == "clear" ? 0 : amount; stormAmt = w == "storm" ? amount : 0; }

            Weather("rain", 0.12f);   // a few drops at a time, so two arcs rarely touch and each group is one drop
            var arcs = new List<(int x, int y, int delta)>(); int widest = 0, tallest = 0, biggest = 0;
            for (int i = 0; i < 60; i++)
            {
                var one = Paint(seaX, seaY, () => DrawSeaRain(1.0f + i * 0.21f));
                arcs.AddRange(one);
                // Group the pixels of this frame by touching neighbours and measure the groups.
                var left = new HashSet<(int, int)>(one.Select(p => (p.x, p.y)));
                while (left.Count > 0)
                {
                    var start = left.First(); left.Remove(start);
                    var stack = new Stack<(int, int)>(); stack.Push(start); int x0 = start.Item1, x1 = x0, y0 = start.Item2, y1 = y0;
                    while (stack.Count > 0)
                    {
                        var (px, py) = stack.Pop();
                        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (left.Remove((px + dx, py + dy))) { stack.Push((px + dx, py + dy)); x0 = Math.Min(x0, px + dx); x1 = Math.Max(x1, px + dx); y0 = Math.Min(y0, py + dy); y1 = Math.Max(y1, py + dy); }
                    }
                    widest = Math.Max(widest, x1 - x0 + 1); tallest = Math.Max(tallest, y1 - y0 + 1);
                }
            }
            Weather("rain");
            var heavyArcs = new List<(int x, int y, int delta)>();
            for (int i = 0; i < 30; i++) heavyArcs.AddRange(Paint(seaX, seaY, () => DrawSeaRain(1.0f + i * 0.21f)));
            biggest = heavyArcs.Count == 0 ? 0 : heavyArcs.Max(p => p.delta);
            Check($"rain on the sea is small flat arcs: no group wider than 7 px ({widest}) or taller than 3 ({tallest}), faint ({biggest} summed levels at most, where a spot's ring is about 380)", arcs.Count > 50 && widest <= 7 && tallest <= 3 && biggest <= 130);

            // Never on a fishing spot, never off the water.
            var coastSpots = Data.Spots.Where(s => s.Scene == "world" && ExposedWater((int)s.X, (int)s.Y)).Take(4).ToList();
            int nearSpot = 0, offWater = 0, drawn = 0;
            foreach (var sp in coastSpots)
                for (int i = 0; i < 40; i++)
                    foreach (var p in Paint((int)sp.X - W / 2, (int)sp.Y - H / 2, () => DrawSeaRain(2.0f + i * 0.17f)))
                    {
                        drawn++;
                        if (Dist(p.x, p.y, sp.X, sp.Y) < sp.R) nearSpot++;
                        if (!ExposedWater(p.x, p.y)) offWater++;
                    }
            Check($"rain keeps clear of fishing spots ({nearSpot} pixels inside a spot's ring, of {drawn} near {coastSpots.Count} spots) and never leaves the water ({offWater} off it)", coastSpots.Count >= 3 && drawn > 100 && nearSpot == 0 && offWater == 0);
            (player.X, player.Y) = StandableNear(230, 60); yield return 4;
            int offPier = 0, nearCoast = 0;
            for (int i = 0; i < 50; i++) foreach (var p in Paint(camX, camY, () => DrawSeaRain(3.0f + i * 0.19f))) { nearCoast++; if (!ExposedWater(p.x, p.y)) offPier++; }
            Check($"at the coast, with piers and the shore in view, every arc pixel is on open water ({nearCoast} pixels, {offPier} not)", nearCoast > 100 && offPier == 0);

            // More rain, more arcs.
            int Count(float amount) { Weather("rain", amount); int n = 0; for (int i = 0; i < 30; i++) n += Paint(seaX, seaY, () => DrawSeaRain(1.0f + i * 0.21f)).Count; return n; }
            int light = Count(0.3f), heavy = Count(1f);
            Check($"heavier rain makes more arcs ({heavy} against {light})", heavy > light * 1.5f && light > 0);

            // The old rings: with light off the sea still gets them; with light on DrawRainSplashes leaves the sea to the arcs.
            Weather("rain");
            EffectsOff = true; int oldRings = Paint(seaX, seaY, () => DrawRainSplashes(1.7f)).Count; EffectsOff = false;
            int newRings = Paint(seaX, seaY, () => DrawRainSplashes(1.7f)).Count;
            Check($"the old rain rings are still drawn with light off ({oldRings}) and left to the arcs with it on ({newRings})", oldRings > 20 && newRings == 0);

            // Chop: a storm only, deep water only, sparse, and the ripples step aside.
            int Chop(float amount, string w) { Weather(w, amount); int n = 0; for (int i = 0; i < 10; i++) n += Paint(seaX, seaY, () => DrawSeaChop(1.0f + i * 0.7f)).Count; return n; }
            int calmChop = Chop(0, "clear"), stormChop = Chop(1, "storm"), rainChop = Chop(1, "rain");
            var chopPixels = new List<(int x, int y, int delta)>();
            Weather("storm"); for (int i = 0; i < 10; i++) chopPixels.AddRange(Paint(seaX, seaY, () => DrawSeaChop(1.0f + i * 0.7f)));
            int offDeep = chopPixels.Count(p => ShapePx(p.x, p.y) != '~');
            double share = stormChop / (10.0 * W * H);
            Check($"storm chop: none in calm ({calmChop}), some in rain ({rainChop}), most in a storm ({stormChop}), on deep water only ({offDeep} elsewhere), sparse ({share * 100:0.0}% of the sea)", calmChop == 0 && rainChop > 0 && stormChop > rainChop * 2 && offDeep == 0 && share < 0.08);
            int ripplesStorm = Paint(seaX, seaY, () => DrawRipples(1.0f)).Count;
            Weather("clear"); int ripplesCalm = 0; for (int i = 0; i < 20; i++) ripplesCalm += Paint(seaX, seaY, () => DrawRipples(1.0f + i * 0.5f)).Count;
            Check($"the calm sea's ripples step aside for a storm ({ripplesCalm} in calm, {ripplesStorm} in a storm)", ripplesCalm > 0 && ripplesStorm == 0);

            // The cost.
            Weather("storm"); Settings.Data.zoom = 2; yield return 4;
            var keepP = pix; pix = new Pix(W, H); pix.CamX = camX = seaX; pix.CamY = camY = seaY;
            double r0 = Raylib.GetTime();
            for (int i = 0; i < 30; i++) { DrawSeaRain(i * 0.1f); DrawSeaChop(i * 0.1f); }
            double rainMs = (Raylib.GetTime() - r0) / 30 * 1000;
            pix = keepP;
            Check($"rain and chop cost little at the widest zoom ({rainMs:0.00} ms in a Debug build)", rainMs < 4);
            Settings.Data.zoom = 4; yield return 4;

            // Screenshots.
            foreach (var (id, x, y) in new[] { ("coast", 230f, 60f), ("atoll", 935f, 262f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (var w in new[] { "rain", "storm" })
                    foreach (int lm in new[] { 0, 2 })
                    {
                        EffectsOff = lm == 0; Settings.Data.light = 2; Weather(w); yield return 4;
                        pendingPixShot = $"water-{id}-{w}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                    }
            }
            Weather("clear"); ClearSkies(); EffectsOff = false;
        }

        /* ---------- Phase D: reflections ---------- */
        {
            Note("Water: reflections");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3; state.clock = 12 * 60; yield return 3;
            state.inv["boat"] = 1; state.boatAt = "saltmere";
            long hashBase = PixHash(worldBase);
            void Weather(string w, float amount = 1) { state.forecast = new() { new WeatherSpell { at = 0, w = w } }; state.weather = planned = w; SnapWeather(); rainAmt = w == "clear" ? 0 : amount; stormAmt = w == "storm" ? amount : 0; }
            // What DrawReflections paints on a flat blue picture at the current view.
            List<(int x, int y)> ReflPaint()
            {
                var keep = pix; var found = new List<(int, int)>();
                pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(30, 90, 140, 255)); pix.CamX = camX; pix.CamY = camY;
                bool lv = liveRender; liveRender = true; DrawReflections(time); liveRender = lv;
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (pix.Buf[y * W + x].R != 30 || pix.Buf[y * W + x].G != 90) found.Add((x + camX, y + camY));
                pix = keep; return found;
            }

            // The policy: Full only, calm only, fading as rain comes on.
            (player.X, player.Y) = StandableNear(300, 54); yield return 4;
            float calmK = ReflectK;
            Settings.Data.light = 1; float softK = ReflectK; Settings.Data.light = 2;
            Weather("rain", 0.3f); float lightRainK = ReflectK;
            Weather("rain", 0.6f); float rainK = ReflectK;
            Weather("storm"); float stormK = ReflectK;
            Weather("clear"); yield return 3;
            Check($"reflections are for Full light in calm water only, fading as rain comes ({calmK:0.00} calm, {lightRainK:0.00} light rain, {rainK:0.00} rain, {stormK:0.00} storm, {softK:0.00} on Soft)", calmK == 1 && lightRainK > 0.2f && lightRainK < 0.8f && rainK == 0 && stormK == 0 && softK == 0);

            // Under the player on the dock, and under the moored boat.
            (player.X, player.Y) = StandableNear(300, 54); yield return 4;
            var below = ReflPaint().Where(p => Math.Abs(p.x - player.X) < 12 && p.y > player.Y + 2 && p.y < player.Y + 40).ToList();
            Check($"you are reflected in the water below the dock ({below.Count} pixels)", below.Count > 12);
            // Even with the source at the very top of the screen, its reflection below it is complete.
            int keepCamY = camY; camY = (int)player.Y - 2;
            var atTop = ReflPaint().Where(p => Math.Abs(p.x - player.X) < 12 && p.y > player.Y + 2).ToList();
            camY = keepCamY;
            Check($"a source at the very top of the view keeps its whole reflection ({atTop.Count} pixels against {below.Count})", atTop.Count >= below.Count * 0.8f);
            var (bx, by) = BoatPosition();
            (player.X, player.Y) = StandableNear(bx - 30, by - 20); yield return 4;
            var boatRefl = ReflPaint().Where(p => Math.Abs(p.x - bx) < 26 && p.y > by + 4 && p.y < by + 32).ToList();
            state.inv["boat"] = 0; var noBoat = ReflPaint().Where(p => Math.Abs(p.x - bx) < 26 && p.y > by + 4 && p.y < by + 32).ToList(); state.inv["boat"] = 1;
            Check($"the moored boat has a reflection ({boatRefl.Count} pixels, against {noBoat.Count} with no boat)", boatRefl.Count > noBoat.Count + 20);
            // Wading in the shallows: your upper body is mirrored about the surface at your knees.
            state.inv["waders"] = 1;
            (int x, int y)? shallow = null;
            for (int ty = 1; ty < 40 && shallow == null; ty++)
                for (int tx = 1; tx < 40 && shallow == null; tx++)
                    if (worldMap[ty, tx] == 'w' && worldMap[ty, tx - 1] == 'w' && worldMap[ty, tx + 1] == 'w' && worldMap[ty + 1, tx] == 'w' && worldMap[ty - 1, tx] == 'w' && CanStand(tx * T + 5, ty * T + 5, true)) shallow = (tx * T + 5, ty * T + 5);
            var (shx, shy) = shallow ?? (0, 0);
            (player.X, player.Y) = (shx, shy); yield return 4;
            var wadeRefl = ReflPaint().Where(p => Math.Abs(p.x - player.X) < 12 && p.y > player.Y - 2 && p.y < player.Y + 30).ToList();
            Check($"wading in the shallows you are reflected too ({wadeRefl.Count} pixels; wading: {InWater})", shallow != null && InWater && wadeRefl.Count > 8);
            pendingPixShot = "refl-wading"; yield return 2;
            state.inv["waders"] = 0;
            (player.X, player.Y) = StandableNear(bx - 30, by - 20); yield return 4;
            int offWater = 0; foreach (var p in ReflPaint()) if (!ExposedWater(p.x, p.y)) offWater++;
            Check($"no reflection pixel is ever on land, a pier, a dry flat ({offWater} are)", offWater == 0);
            liveRender = false; var keepP2 = pix; pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(30, 90, 140, 255)); DrawReflections(time); int outside = pix.Buf.Count(c => c.R != 30); pix = keepP2;
            Check($"outside the live view (the chart) nothing is reflected ({outside} pixels)", outside == 0);
            Check("the baked ground layer is untouched by it", PixHash(worldBase) == hashBase);

            // In the finished picture a reflection only ever differs from none on exposed water, and the boat itself is the same.
            RenderWorld(5.0f); var withRefl = (Color[])pix.Buf.Clone();
            reflectOff = true; RenderWorld(5.0f); var without = (Color[])pix.Buf.Clone(); reflectOff = false;
            int differs = 0, onLand = 0, hullDiffers = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    if (withRefl[y * W + x].Equals(without[y * W + x])) continue;
                    differs++;
                    if (!ExposedWater(x + camX, y + camY)) onLand++;
                    if (Math.Abs(x + camX - bx) < 12 && y + camY > by - 18 && y + camY < by + 3) hullDiffers++;
                }
            Check($"in the finished picture reflections change {differs} pixels, all on open water ({onLand} elsewhere), never the boat itself ({hullDiffers})", differs > 30 && onLand == 0 && hullDiffers == 0);

            // Cost with several sources in view.
            Settings.Data.zoom = 2; yield return 4; (player.X, player.Y) = StandableNear(300, 54); yield return 4;
            var keepP3 = pix; pix = new Pix(W, H); pix.CamX = camX; pix.CamY = camY; liveRender = true;
            double q0 = Raylib.GetTime();
            for (int i = 0; i < 20; i++) DrawReflections(time);
            double reflMs = (Raylib.GetTime() - q0) / 20 * 1000;
            liveRender = false; pix = keepP3;
            Check($"reflections cost little at the widest zoom ({reflMs:0.00} ms with a boat, a player and trees in view, in a Debug build)", reflMs < 8);
            Settings.Data.zoom = 4; yield return 4;

            // Screenshots.
            foreach (var (id, x, y) in new[] { ("dock", 300f, 54f), ("jetty", 195f, 100f), ("village", 1625f, 192f), ("parola", LighthouseX - 60, LighthouseY + 30) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (var (tag, minute) in new[] { ("noon", 12 * 60), ("dusk", 19 * 60 + 10) })
                    foreach (int lm in new[] { 0, 2 })
                    {
                        EffectsOff = lm == 0; Settings.Data.light = 2; state.clock = minute; ClearSkies(); yield return 4;
                        pendingPixShot = $"refl-{id}-{tag}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                    }
            }
            EffectsOff = false; state.inv["boat"] = 0;
        }

        EffectsOff = keepOff;
        SetNight(false);
    }

    // The foam alone at a given time, on a flat blue picture.
    Color[] FoamPictureAt(float t)
    {
        var keep = pix; pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(30, 90, 140, 255)); pix.CamX = camX; pix.CamY = camY;
        DrawFoam(t); var result = pix.Buf; pix = keep; return result;
    }
}
#endif
