#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    static int Bright(Color c) => c.R + c.G + c.B;
    static long PixHash(Pix p) { long s = 0; for (int i = 0; i < p.Buf.Length; i++) s = s * 31 + p.Buf[i].R + p.Buf[i].G * 3 + p.Buf[i].B * 7; return s; }

    // Light and colour (FESH_LIGHT_TEST=1): the glow maths, lamps at dusk, Off being the old look, the Graphics tab by real
    // clicks, and screenshots of dusk and night for judging by eye. The other scripts run with the effects off (EffectsOff).
    IEnumerable<int> LightScript()
    {
        Note("Light: glows, dusk lamps, settings");
        state = new State { created = true, look = new Look { name = "Lamplighter" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;

        /* ---------- AddGlow: light added to the picture ---------- */
        {
            var p = new Pix(60, 40);
            Array.Fill(p.Buf, new Color(20, 30, 60, 255));
            var orange = new Color(243, 150, 60, 255);
            p.AddGlow(30, 20, 16, orange, 0.5f);
            var centre = p.Buf[20 * 60 + 30]; var edge = p.Buf[20 * 60 + 30 + 15]; var far = p.Buf[20 * 60 + 52];
            Check($"a glow is brightest in the middle, fades to nothing at its edge and leaves the rest alone ({Bright(centre)} > {Bright(edge)})",
                Bright(centre) > Bright(edge) + 20 && far.Equals(new Color(20, 30, 60, 255)) && p.Buf[20 * 60 + 30 + 16].Equals(new Color(20, 30, 60, 255)));
            Check($"the glow's colour is warm over a dark blue picture ({centre.R},{centre.G},{centre.B})", centre.R > centre.G && centre.G > 30 && centre.R > 100);
            // Two lamps on one spot brighten but never overflow or turn white.
            var q = new Pix(60, 40); Array.Fill(q.Buf, new Color(20, 30, 60, 255));
            for (int i = 0; i < 6; i++) q.AddGlow(30, 20, 16, orange, 0.5f);
            var many = q.Buf[20 * 60 + 30];
            Check($"six lamps in one place brighten without clipping to white ({many.R},{many.G},{many.B})", Bright(many) > Bright(centre) && many.B < 255 && many.G < 255 && many.A == 255);
            var white = new Pix(20, 20); Array.Fill(white.Buf, new Color(255, 255, 255, 255));
            white.AddGlow(10, 10, 8, orange, 1f);
            Check("light over white stays white (no wrap-around)", white.Buf[10 * 20 + 10].Equals(new Color(255, 255, 255, 255)));
            var cam = new Pix(60, 40); Array.Fill(cam.Buf, new Color(20, 30, 60, 255)); cam.CamX = 100; cam.CamY = 50;
            cam.AddGlow(130, 70, 10, orange, 0.5f);
            Check("a glow follows the camera", Bright(cam.Buf[20 * 60 + 30]) > Bright(new Color(20, 30, 60, 255)) + 30);
            var half = new Pix(60, 40); Array.Fill(half.Buf, new Color(20, 30, 60, 255)); half.Alpha = 0.5f;
            half.AddGlow(30, 20, 16, orange, 0.5f);
            Check("a glow respects the buffer's alpha", Bright(half.Buf[20 * 60 + 30]) < Bright(centre) && Bright(half.Buf[20 * 60 + 30]) > Bright(new Color(20, 30, 60, 255)));
            var edgeP = new Pix(30, 30); Array.Fill(edgeP.Buf, new Color(20, 30, 60, 255));
            edgeP.AddGlow(-3, -3, 12, orange, 0.5f); edgeP.AddGlow(40, 40, 12, orange, 0.5f);
            Check("a glow half off the picture is clipped, not wrapped", Bright(edgeP.Buf[0]) > Bright(new Color(20, 30, 60, 255)) && edgeP.Buf[29 * 30 + 29].Equals(new Color(20, 30, 60, 255)));
        }

        /* ---------- Lamps come on at dusk ---------- */
        EffectsOff = true;
        state.clock = 18 * 60 + 45;
        Check($"light off: the lamps still wait for the dark ({LampK:0.00} at 6:45 PM)", LampK == 0);
        state.clock = 22 * 60;
        Check("light off: they burn at 10 PM", LampK == 1);
        EffectsOff = false;
        foreach (int mode2 in new[] { 1, 2 })
        {
            Settings.Data.light = mode2;
            state.clock = 17 * 60 + 59;
            Check($"light {mode2}: no lamps at 5:59 PM", LampK == 0);
            state.clock = 18 * 60 + 30;
            float half = LampK;
            state.clock = 18 * 60 + 45;
            float three = LampK;
            state.clock = 19 * 60;
            float full = LampK;
            Check($"light {mode2}: the lamps come on gently through dusk ({half:0.00}, {three:0.00}, {full:0.00})", half > 0 && half < three && three < full && full >= 0.99f);
            state.clock = 12 * 60;
            Check($"light {mode2}: no lamps at noon", LampK == 0);
            state.clock = 5 * 60 + 30;
            Check($"light {mode2}: still burning before dawn", LampK > 0.8f);
        }

        // The Amihan village's windows at 6:45 PM: dark with light off, lit with light on.
        Settings.Data.light = 2;
        (player.X, player.Y) = StandableNear(1625, 192); ClearSkies();
        state.clock = 18 * 60 + 45; yield return 4;
        int litDusk = litWindows.Count;
        EffectsOff = true; yield return 4;
        int litOff = litWindows.Count;
        EffectsOff = false; yield return 2;
        Check($"the village windows light at 6:45 PM with light on ({litDusk}), and not with it off ({litOff})", litDusk >= 2 && litOff == 0);
        state.clock = 18 * 60 + 20; yield return 4;
        Check("and not yet before 6:30 PM", litWindows.Count == 0);

        // Screenshots: dusk, night, off against full, at the village and at Tomas's fire.
        foreach (var (id, x, y) in new[] { ("village", 1625f, 192f), ("camp", FireX + 10, FireY + 14) })
        {
            (player.X, player.Y) = StandableNear(x, y);
            foreach (var (tag, minute) in new[] { ("1830", 18 * 60 + 30), ("1900", 19 * 60), ("1930", 19 * 60 + 30), ("2200", 22 * 60) })
                foreach (int lm in new[] { 0, 1, 2 })
                {
                    EffectsOff = lm == 0; Settings.Data.light = lm == 0 ? 2 : lm;
                    state.clock = minute; yield return 4;
                    pendingPixShot = $"light-{id}-{tag}-{(lm == 0 ? "off" : lm == 1 ? "soft" : "full")}"; yield return 2;
                }
        }
        {
            // Same moment, off and full: the fire's middle and the window pane are brighter with light on.
            (player.X, player.Y) = StandableNear(FireX + 10, FireY + 14);
            state.clock = 19 * 60 + 30;
            EffectsOff = true; yield return 4;
            RenderWorld(time);
            var fxs = (int)(FireX - camX); var fys = (int)(FireY - 3 - camY);
            // The average over a patch round the fire, so a flickering flame pixel can't decide it.
            int Patch() { long s = 0; int n = 0; for (int dy = -12; dy <= 12; dy++) for (int dx = -12; dx <= 12; dx++) { int px = fxs + dx, py = fys + dy; if (px < 0 || py < 0 || px >= W || py >= H) continue; s += Bright(pix.Buf[py * W + px]); n++; } return (int)(s / n); }
            int offB = Patch();
            EffectsOff = false; Settings.Data.light = 2; yield return 2;
            RenderWorld(time);
            int onB = Patch();
            Check($"the ground round Tomas's fire is brighter with light on than off ({onB} > {offB})", onB > offB + 8);
        }

        /* ---------- The colour grade ---------- */
        {
            EffectsOff = false; Settings.Data.light = 2;
            float worst = 0;
            for (int m = 0; m < 1440; m++)
            {
                var a = GradeAt(m); var b = GradeAt((m + 1) % 1440);
                worst = Math.Max(worst, Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.Sat - b.Sat) + Math.Abs(a.LR - b.LR) * 0.1f))));
            }
            Check($"the grade changes smoothly through the whole day, midnight included (largest step {worst:0.0000} a minute)", worst < 0.01f);
            var noon = GradeAt(12 * 60); var sunset = GradeAt(19 * 60 + 30); var dawn = GradeAt(6 * 60);
            Check("sunset is warm (more red than blue), noon is not", sunset.R - sunset.B > 0.15f && noon.R - noon.B < 0.05f && dawn.R > dawn.B);
            Check("the grade is exactly the key's own colour on a key's minute", GradeAt(12 * 60).G == 1.01f && GradeAt(5 * 60).B == 1.02f);

            state.clock = 12 * 60; ClearSkies(); state.day = 1; yield return 3;
            var clear = GradeNow();
            state.forecast = new() { new WeatherSpell { at = 0, w = "rain" } }; state.weather = planned = "rain"; SnapWeather(); yield return 3;
            var rainy = GradeNow();
            state.forecast = new() { new WeatherSpell { at = 0, w = "storm" } }; state.weather = planned = "storm"; SnapWeather(); yield return 3;
            var storm2 = GradeNow();
            ClearSkies(); yield return 3;
            Check($"rain greys and darkens the colour, a storm more ({clear.R:0.00}, {rainy.R:0.00}, {storm2.R:0.00}; saturation {clear.Sat:0.00}, {rainy.Sat:0.00}, {storm2.Sat:0.00})",
                rainy.R < clear.R && storm2.R < rainy.R && rainy.Sat < clear.Sat && storm2.Sat < rainy.Sat);
            state.clock = 19 * 60 + 30;
            var fullSet = GradeNow();
            Settings.Data.light = 1;
            var softSet = GradeNow();
            Settings.Data.light = 2;
            state.clock = 12 * 60;
            Check($"Soft is half way between no grade and Full at sunset ({softSet.R:0.000} against {fullSet.R:0.000}, blue {softSet.B:0.000} against {fullSet.B:0.000})", fullSet.R > 1.1f && MathF.Abs((softSet.R - 1) - (fullSet.R - 1) / 2) < 0.002f && MathF.Abs((softSet.B - 1) - (fullSet.B - 1) / 2) < 0.002f && MathF.Abs((softSet.Sat - 1) - (fullSet.Sat - 1) / 2) < 0.002f);

            // With light off the old flat fills run and the grade does nothing.
            (player.X, player.Y) = StandableNear(160, 115); yield return 3;
            EffectsOff = true; yield return 3; RenderWorld(time);
            Check("light off: no grade pass runs (the old rain tint and sunset fill do)", gradeMatrix == null);
            float Warmth() { double s = 0; foreach (var c in pix.Buf) s += c.R - c.B; return (float)(s / pix.Buf.Length); }
            RenderWorld(time); float warmNoon = Warmth();
            state.clock = 19 * 60 + 30; yield return 3; RenderWorld(time); float warmSunset = Warmth();
            state.clock = 12 * 60; yield return 3;
            Check($"light off: the old flat sunset fill still warms the picture ({warmSunset:0.0} against {warmNoon:0.0} at noon)", warmSunset > warmNoon + 5);
            RenderWorld(1.0f); long offA = PixHash(pix);
            EffectsOff = false; Settings.Data.light = 0; RenderWorld(1.0f); long offB = PixHash(pix);
            Check("light off by the setting draws exactly what the autotest's own off does", offA == offB);
            Settings.Data.light = 2; EffectsOff = true;
            EffectsOff = false; yield return 3; RenderWorld(time);
            Check("light on: the grade pass ran", gradeMatrix != null);

            // The island palette: neutral over the open sea, tinted over an island, gradual between.
            int seaX = 70 * T, seaY = 70 * T;
            var seaT = TintAt(seaX, seaY, 1);
            var frostT = TintAt(620, 90, 1);
            var saltT = TintAt(160, 115, 1);
            var sunT = TintAt(660, 380, 1);
            Check($"the palette is neutral over the open sea ({seaT.r}, {seaT.g}, {seaT.b}) and Saltmere's own colour is near neutral ({saltT.r}, {saltT.g}, {saltT.b})",
                Math.Abs(seaT.r - 256) <= 3 && Math.Abs(seaT.g - 256) <= 3 && Math.Abs(seaT.b - 256) <= 3 && Math.Abs(saltT.r - 256) <= 6 && Math.Abs(saltT.b - 256) <= 6);
            Check($"Frostfang leans cool ({frostT.r}, {frostT.b}) and Sunscald warm ({sunT.r}, {sunT.b})", frostT.b > frostT.r + 6 && sunT.r > sunT.b + 6);
            int worstSeam = 0;
            for (float x = 0; x < COLS * T - 5; x += 5)
            {
                var a = TintAt(x, 90, 1); var b = TintAt(x + 5, 90, 1);
                worstSeam = Math.Max(worstSeam, Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b))));
            }
            Check($"no seam in the palette along a whole row (largest step {worstSeam}/256 in 5 px)", worstSeam <= 3);
            Check("a nonsense strength can't break the palette", TintAt(0, 0, 0).r == 256 && TintAt(-500, -500, 1).r > 200 && TintAt(1e6f, 1e6f, 1).r > 200);

            // The baked ground and the chart's picture are never graded.
            long Sum(Pix p) { long s = 0; for (int i = 0; i < p.Buf.Length; i += 97) s += p.Buf[i].R * 3 + p.Buf[i].G * 5 + p.Buf[i].B * 7; return s; }
            long before = Sum(worldBase);
            for (int i = 0; i < 3; i++) { state.clock = 19 * 60 + 30; RenderWorld(time); }
            Check("rendering with the grade on leaves the baked ground exactly as it was", Sum(worldBase) == before);

            // Readability: the shallow/deep sea contrast survives the grade at every hour and in a storm.
            Color Tile(char k) { for (int y = 0; y < ROWS; y++) for (int x = 0; x < COLS; x++) if (worldMap[y, x] == k && worldMap[y, Math.Min(COLS - 1, x + 1)] == k && worldMap[Math.Max(0, y - 1), x] == k) return worldBase.Buf[(y * T + 5) * worldBase.W + x * T + 5]; return Color.Black; }
            var shallow = Tile('w'); var deep = Tile('~');
            int Luma(Color c) => (299 * c.R + 587 * c.G + 114 * c.B) / 1000;
            int rawGap = Math.Abs(Luma(shallow) - Luma(deep));
            float minKeep = 9;
            foreach (int minute in new[] { 6 * 60, 9 * 60, 12 * 60, 17 * 60, 19 * 60 + 30, 22 * 60 })
                foreach (float wx in new[] { 0f, 1f })
                {
                    var g = LerpKey(GradeAt(minute), wx == 0 ? GradeAt(minute) : StormKey, wx);
                    var mx = GradeMatrix(g);
                    var s2 = GradeColor(shallow, mx, 256, 256, 256); var d2 = GradeColor(deep, mx, 256, 256, 256);
                    minKeep = Math.Min(minKeep, Math.Abs(Luma(s2) - Luma(d2)) / (float)Math.Max(1, rawGap));
                }
            Check($"the wading line stays readable: the shallow-to-deep contrast keeps at least {minKeep:0.00} of itself (from {rawGap})", rawGap > 15 && minKeep > 0.7f);

            // Moonlight on the water survives the grade (its sparkles are planned on the ungraded water).
            (player.X, player.Y) = StandableNear(275, 117); state.day = 1; state.clock = 22 * 60; ClearSkies(); yield return 4;
            EffectsOff = true; yield return 3; RenderWorld(time); int keptOff = moonPx.Count;
            EffectsOff = false; yield return 3; RenderWorld(time); int keptOn = moonPx.Count;
            Check($"the moon's sparkles still land with the grade on ({keptOn}, against {keptOff} without it)", keptOff > 10 && keptOn >= keptOff * 0.8f);

            // The cost of the pass: it must stay cheap, even at the widest zoom.
            Settings.Data.zoom = 2; yield return 4;
            state.clock = 17 * 60; yield return 3;
            double t0 = Raylib.GetTime();
            liveRender = true;
            for (int i = 0; i < 30; i++) ApplyGrade();
            liveRender = false;
            double ms = (Raylib.GetTime() - t0) / 30 * 1000;
            Check($"the grade pass costs little at the widest zoom ({ms:0.00} ms in a Debug build, {W} x {H})", gradeMatrix != null && ms < 12);
            Settings.Data.zoom = 4; yield return 4;

            // Screenshots across the day and the weather, light off against full.
            foreach (var (id, x, y) in new[] { ("saltmere", 160f, 115f), ("luntian", 2040f, 138f), ("frostfang", 620f, 90f), ("mirewood", 200f, 380f), ("sunscald", 660f, 380f) })
            {
                (player.X, player.Y) = StandableNear(x, y); state.day = 3;
                foreach (var (tag, minute) in new[] { ("0600", 6 * 60), ("1200", 12 * 60), ("1700", 17 * 60), ("1930", 19 * 60 + 30) })
                    foreach (int lm in new[] { 0, 2 })
                    {
                        EffectsOff = lm == 0; Settings.Data.light = 2;
                        state.clock = minute; ClearSkies(); yield return 4;
                        pendingPixShot = $"grade-{id}-{tag}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                    }
            }
            (player.X, player.Y) = StandableNear(160, 115);
            foreach (var w in new[] { "rain", "storm" })
                foreach (int lm in new[] { 0, 2 })
                {
                    EffectsOff = lm == 0; Settings.Data.light = 2; state.clock = 12 * 60;
                    state.forecast = new() { new WeatherSpell { at = 0, w = w } }; state.weather = planned = w; SnapWeather(); yield return 4;
                    pendingPixShot = $"grade-saltmere-{w}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                }
            ClearSkies(); EffectsOff = false;
        }

        /* ---------- Shadows that follow the sun, and the low sun on the sea ---------- */
        {
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3;
            var (c6, a6) = SunPose(6 * 60); var (c20, a20) = SunPose(20 * 60); var (c13, a13) = SunPose(13 * 60);
            Check($"the sun rises in the east, sets in the west and stands high at midday ({c6:0.00}, {c20:0.00}, {c13:0.00}; {a13 * 180 / MathF.PI:0} degrees at 1 PM)", c6 > 0.99f && c20 < -0.99f && MathF.Abs(c13) < 0.3f && a13 > 1.0f && a6 < 0.2f);
            state.clock = 9 * 60; var morning = ShadowOffset(46);
            state.clock = 12 * 60; var midday = ShadowOffset(46);
            state.clock = 17 * 60; var evening = ShadowOffset(46);
            state.clock = 19 * 60 + 30; var late = ShadowOffset(46);
            Check($"a tree's shadow falls west in the morning ({morning.dx:0.0}), under it at noon ({midday.dx:0.0}), east in the evening ({evening.dx:0.0}) and longest at sunset ({late.dx:0.0})",
                morning.dx < -5 && MathF.Abs(midday.dx) < 3 && evening.dx > 5 && late.dx > evening.dx && late.far > 0.95f && midday.far < 0.3f && morning.dy > 0 && late.dx <= 0.65f * 46 + 0.01f);

            // Where the shadows are on the screen moves with the sun: the mean place of the mask on Mirewood's forest floor.
            (player.X, player.Y) = StandableNear(200, 380); yield return 4;
            float MeanX() { double s = 0; int n = 0; for (int i = 0; i < shadeMask.Length; i++) if (shadeMask[i] > 0.02f) { s += i % W; n++; } return n == 0 ? -1 : (float)(s / n); }
            state.clock = 9 * 60; yield return 4; float xMorning = MeanX();
            state.clock = 12 * 60; yield return 4; float xNoon = MeanX();
            state.clock = 17 * 60; yield return 4; float xEvening = MeanX();
            Check($"the shadows on the ground shift from west to east through the day ({xMorning:0.0}, {xNoon:0.0}, {xEvening:0.0} across the view)", xMorning > 0 && xMorning < xNoon - 1.5f && xNoon < xEvening - 1.5f);
            float peak = 0; foreach (var v in shadeMask) peak = Math.Max(peak, v);
            Check($"shadows never darken more than one tree's own ({peak:0.000} at most), however many overlap", peak <= 0.2f + 0.0001f);
            int Dapples() { int n = 0; foreach (var v in shadeMask) if (v is > 0.055f and < 0.075f) n++; return n; }
            state.clock = 10 * 60; yield return 4; int gaps = Dapples();
            Settings.Data.light = 1; yield return 4; int gapsSoft = Dapples();
            Settings.Data.light = 2;
            Check($"light comes through the canopy in gaps at Full ({gaps}), not at Soft ({gapsSoft})", gaps > 5 && gapsSoft == 0);

            // At night the shadows are gone and the ground is left alone.
            state.clock = 22 * 60; yield return 4;
            long h0 = PixHash(pix); DrawShadows(); long h1 = PixHash(pix);
            Check("at night the shadow pass leaves the picture alone", h0 == h1);

            // The low sun on the sea: more, and golden, glints than with light off.
            (player.X, player.Y) = StandableNear(230, 60); state.clock = 19 * 60 + 20; yield return 4;
            (int n, float blue) Glints(int lm)
            {
                EffectsOff = lm == 0; Settings.Data.light = 2;
                int n = 0; double b = 0;
                var keep = pix;
                for (int k = 0; k < 40; k++)
                {
                    pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(20, 60, 110, 255)); pix.CamX = camX; pix.CamY = camY;
                    DrawGlints(time + k * 0.37f);
                    foreach (var c in pix.Buf) if (c.R > 60) { n++; b += c.B; }
                }
                pix = keep;
                return (n, n == 0 ? 255 : (float)(b / n));
            }
            var gOff = Glints(0); var gOn = Glints(1);
            EffectsOff = false;
            Check($"at sunset the sea glitters gold, and more of it: {gOn.n} glints (blue {gOn.blue:0}) against {gOff.n} (blue {gOff.blue:0}) with light off", gOn.n > gOff.n * 1.4f && gOn.blue < gOff.blue - 20);
            state.clock = 12 * 60; yield return 3;
            var noonOff = Glints(0); var noonOn = Glints(1);
            EffectsOff = false;
            Check($"at noon the glints are the same with light on or off ({noonOn.n} and {noonOff.n})", noonOn.n == noonOff.n);

            // Screenshots: the shadows through a day, at Tomas's island and in the village.
            foreach (var (id, x, y) in new[] { ("saltmere", 160f, 115f), ("village", 1625f, 192f), ("mirewood", 200f, 380f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (var (tag, minute) in new[] { ("0700", 7 * 60), ("0900", 9 * 60), ("1200", 12 * 60), ("1500", 15 * 60), ("1730", 17 * 60 + 30), ("1930", 19 * 60 + 30) })
                    foreach (int lm in new[] { 0, 2 })
                    {
                        EffectsOff = lm == 0; Settings.Data.light = 2;
                        state.clock = minute; ClearSkies(); yield return 4;
                        pendingPixShot = $"sun-{id}-{tag}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                    }
            }
            (player.X, player.Y) = StandableNear(230, 60); state.clock = 19 * 60 + 15;
            foreach (int lm in new[] { 0, 2 })
            {
                EffectsOff = lm == 0; Settings.Data.light = 2; yield return 4;
                pendingPixShot = $"sun-sea-1915-{(lm == 0 ? "off" : "full")}"; yield return 2;
            }
            EffectsOff = false; SetNight(false);
        }

        /* ---------- Bloom, grain and the dark corners ---------- */
        {
            EffectsOff = false; Settings.Reset(); Settings.Data.dayLength = 0; Settings.Data.pauseUnfocused = false; Settings.Data.light = 2;
            ClearSkies(); state.day = 3; state.clock = 22 * 60;
            (player.X, player.Y) = StandableNear(FireX + 10, FireY + 14); yield return 4;
            double Annulus()
            {
                double s = 0; int n = 0; int fx = (int)(FireX - camX), fy = (int)(FireY - 3 - camY);
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { double d = Math.Sqrt((x - fx) * (x - fx) + (y - fy) * (y - fy)); if (d > 52 && d < 90) { s += Bright(pix.Buf[y * W + x]); n++; } }
                return s / Math.Max(1, n);
            }
            Check("bloom, grain and the full corners are the defaults: bloom and grain off, corners normal", !Settings.Data.bloom && !Settings.Data.grain && Settings.Data.vignette == 2 && !BloomOn && !GrainOn);
            RenderWorld(1.0f); double plain = Annulus();
            Settings.Data.bloom = true; RenderWorld(1.0f); double bloomed = Annulus();
            Settings.Data.light = 0; RenderWorld(1.0f); double offBloom = Annulus();
            Settings.Data.bloom = false; RenderWorld(1.0f); double offNoBloom = Annulus();
            Settings.Data.light = 2;
            Check($"bloom lays a faint halo round a fire ({bloomed:0.0} against {plain:0.0} in the ring beyond its light), and does nothing with light off ({offBloom:0.0} and {offNoBloom:0.0})", bloomed > plain + 1 && Math.Abs(offBloom - offNoBloom) < 0.01);
            Settings.Data.bloom = false;

            // Film grain: one level up or down, about two pixels in three, a new pattern a few times a second.
            state.clock = 12 * 60; yield return 4;
            time = MathF.Floor(time * 8) / 8 + 0.005f;   // the start of one grain pattern
            RenderWorld(2.0f); var clean = (Color[])pix.Buf.Clone();
            Settings.Data.grain = true; RenderWorld(2.0f); var grainy = (Color[])pix.Buf.Clone();
            int changed = 0, wild = 0;
            for (int i = 0; i < clean.Length; i++)
            {
                int dr = Math.Abs(grainy[i].R - clean[i].R), dg = Math.Abs(grainy[i].G - clean[i].G), db = Math.Abs(grainy[i].B - clean[i].B);
                if (dr + dg + db > 0) changed++;
                if (dr > 1 || dg > 1 || db > 1) wild++;
            }
            double share = changed / (double)clean.Length;
            Check($"grain moves pixels by one level at most ({wild} more), on about two in three ({share:0.00})", wild == 0 && share is > 0.55 and < 0.78);
            // The pattern itself (on a flat picture, so nothing else that moves can muddy it): the same within an eighth of a second, new after it.
            Color[] Flat() { Array.Fill(pix.Buf, new Color(100, 100, 100, 255)); ApplyGrain(); return (Color[])pix.Buf.Clone(); }
            var g1 = Flat(); time += 0.05f; var g2 = Flat(); time += 0.3f; var g3 = Flat();
            int differs = 0; for (int i = 0; i < g2.Length; i++) if (!g2[i].Equals(g3[i])) differs++;
            Check($"the grain holds still within an eighth of a second and changes after it ({g1.SequenceEqual(g2)}, {differs} pixels differ)", g1.SequenceEqual(g2) && differs > g2.Length * 0.4);
            EffectsOff = true; bool grainWhenForcedOff = GrainOn; EffectsOff = false;
            Check("with the effects forced off (the autotest's other scripts) there is no grain", !grainWhenForcedOff);
            double g0 = Raylib.GetTime();
            for (int i = 0; i < 20; i++) ApplyGrain();
            double gms = (Raylib.GetTime() - g0) / 20 * 1000;
            Check($"the grain pass is cheap ({gms:0.00} ms in a Debug build, {W} x {H})", gms < 8);
            Settings.Data.grain = false;

            // The corners: None, Light and Normal darken the edge by 0, a little and the usual amount.
            int CornerLuma() { RenderWorld(1.0f); var c = pix.Buf[2 * W + 2]; return c.R + c.G + c.B; }
            Settings.Data.vignette = 2; int vNormal = CornerLuma();
            Settings.Data.vignette = 1; int vLight = CornerLuma();
            Settings.Data.vignette = 0; int vNone = CornerLuma();
            Settings.Data.vignette = 2;
            Check($"dark corners: None is brightest, Light between, Normal darkest ({vNone}, {vLight}, {vNormal})", vNone > vLight && vLight > vNormal);
        }

        /* ---------- Settings: the Graphics tab by real clicks ---------- */
        EffectsOff = keepOff;
        Settings.Reset(); Settings.Data.dayLength = 0; Settings.Data.pauseUnfocused = false;
        Check("a fresh settings file means automatic light, normal corners", Settings.Data.light == -1 && Settings.Data.vignette == 2 && !Settings.Data.bloom && !Settings.Data.grain);
        OpenPause(); menuTab = "graphics"; yield return 4;
        EffectsOff = false;
        bool touchWas = Platform.Touch;
        Platform.Touch = false; int onPc = LightMode; Platform.Touch = true; int onPhone = LightMode; Platform.Touch = touchWas;
        Check($"automatic light is Full on a computer ({onPc}) and Soft on a phone ({onPhone})", onPc == 2 && onPhone == 1 && Settings.Data.light == -1);
        EffectsOff = keepOff; yield return 3;
        foreach (bool clearFont in new[] { false, true })
        {
            Settings.Data.clearFont = clearFont; yield return 3;
            float Row(params string[] opts) => opts.Sum(o => Gfx.Measure(o, FontKind.Ui700, 18) + 30 + 8);
            float bloomEnd = Row("Soft halo", "No halo"), grainEnd = 380 + Row("Grain", "No grain");
            Check($"the Graphics tab's Bloom and Film grain row fits ({(clearFont ? "Clear" : "Pixel")} font: bloom buttons {bloomEnd:0} of 240, the row {grainEnd:0} of 650)", bloomEnd <= 240 && grainEnd <= 650);
        }
        Settings.Data.clearFont = false; yield return 3;
        ClickButton("Full"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("clicking the option already shown (Full) stops following the platform", Settings.Data.light == 2);
        ClickButton("Soft"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Graphics: Soft by a real click is kept", Settings.Data.light == 1);
        ClickButton("Off"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Graphics: Off by a real click is kept", Settings.Data.light == 0);
        ClickButton("Soft halo"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        bool bloomClick = Settings.Data.bloom;
        ClickButton("Grain"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        bool grainClick = Settings.Data.grain;
        ClickButton("None"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        int cornersClick = Settings.Data.vignette;
        Check($"Graphics: bloom, grain and the corners by real clicks ({bloomClick}, {grainClick}, {cornersClick})", bloomClick && grainClick && cornersClick == 0);
        pendingShot = "light-settings"; yield return 2;
        ClickButton("No halo"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("No grain"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("Normal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("and back: halo off, grain off, normal corners", !Settings.Data.bloom && !Settings.Data.grain && Settings.Data.vignette == 2);
        ClickButton("Off"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Settings.Load();
        Check("the choice is saved with the settings", Settings.Data.light == 0);
        File.WriteAllText(SaveFile.SettingsPath, "{ \"light\": 17, \"vignette\": -4 }");
        Settings.Load();
        Check("nonsense values in the file are repaired", Settings.Data.light == -1 && Settings.Data.vignette == 2);
        Settings.Reset(); Settings.Data.dayLength = 0; Settings.Data.pauseUnfocused = false;
        ClosePause(); yield return 3;
        EffectsOff = keepOff;
        SetNight(false);
    }
}
#endif
