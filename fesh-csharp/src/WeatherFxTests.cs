#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Clouds before rain and wet ground after it (FESH_WEATHER_TEST=1, also in the full run): the overcast builds over the
    // hour before a planned rain and fades after it, the latest rain is remembered through rests and the 06:00 rollover, wet
    // ground darkens sand more than grass and dries, nothing touches water, snow or the baked layers, and light off is the
    // old weather.
    IEnumerable<int> WeatherFxScript()
    {
        Note("Weather: overcast, wet ground, shafts");
        state = new State { created = true, look = new Look { name = "Rainmaker" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;
        var d = Settings.Data;
        d.light = 2; EffectsOff = false;
        state.day = 4;

        /* ---------- overcast: the plan decides ---------- */
        // A rain from 600 minutes after 06:00 (4 PM) until 780, clear after.
        void Plan() { state.forecast = new() { new WeatherSpell { at = 0, w = "clear" }, new WeatherSpell { at = 600, w = "rain" }, new WeatherSpell { at = 780, w = "clear" } }; state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } }; state.weather = planned = "clear"; state.lastRain = -1000000f; }
        Plan();
        float At(int sinceDawn) { state.clock = (DawnMin + sinceDawn) % DayMin; return OvercastTarget(); }
        float far = At(300), hourBefore = At(540), mid = At(570), minuteBefore = At(598);
        Check($"nothing builds more than an hour before the rain ({far:0.00}), the clouds start gathering at the hour ({hourBefore:0.00}) and thicken ({mid:0.00}, {minuteBefore:0.00})",
            far == 0 && hourBefore < 0.05f && mid > 0.3f && minuteBefore > mid && minuteBefore < 0.85f);
        state.clock = (DawnMin + 650) % DayMin; state.weather = "rain";
        Check("while it rains it is fully overcast", OvercastTarget() == 1);
        state.weather = "clear"; state.lastRain = TideMinute(state.day, (DawnMin + 780) % DayMin);
        float after0 = At(780), after20 = At(800), after60 = At(840);
        Check($"it thins over the 45 minutes after ({after0:0.00}, {after20:0.00}, {after60:0.00})", after0 > 0.8f && after20 < after0 && after20 > 0.3f && after60 == 0);
        // Tomorrow's first spell is wet: the hour before 06:00 builds too.
        Plan(); state.tomorrow = new() { new WeatherSpell { at = 0, w = "rain" } };
        float preDawn = At(1440 - 20);
        Check($"the hour before a rainy morning builds clouds across the 06:00 rollover ({preDawn:0.00})", preDawn > 0.4f);
        Plan();

        /* ---------- the latest rain is remembered through rests ---------- */
        state.day = 4; state.clock = DawnMin + 90; state.lastRain = -1000000f; SnapWeather();
        float startAbs = TideMinute(state.day, state.clock);
        // A rain from 120 to 240 minutes after dawn; rest from 90 to 330 minutes over all of it.
        state.forecast = new() { new WeatherSpell { at = 0, w = "clear" }, new WeatherSpell { at = 120, w = "rain" }, new WeatherSpell { at = 240, w = "clear" } };
        Advance(240, quiet: true);
        float expected = startAbs + (240 - 90);
        Check($"a rest across a whole rain spell still records when it ended ({state.lastRain:0} against {expected:0})", MathF.Abs(state.lastRain - expected) < 1);
        // Resting through a clear stretch changes nothing.
        float before = state.lastRain; state.forecast = new() { new WeatherSpell { at = 0, w = "clear" } }; state.weather = planned = "clear";
        Advance(120, quiet: true);
        Check("a clear stretch leaves it alone", state.lastRain == before);
        // A night's rain that runs to the 06:00 rollover.
        state.day = 6; state.clock = 21 * 60; state.lastRain = -1000000f;
        state.forecast = new() { new WeatherSpell { at = 0, w = "clear" }, new WeatherSpell { at = 1200, w = "rain" } }; state.tomorrow = new() { new WeatherSpell { at = 0, w = "clear" } };
        state.weather = planned = "rain";
        float nightAbs = TideMinute(6, 21 * 60);
        Advance(9 * 60, quiet: true);
        Check($"rain that lasts to dawn counts up to dawn ({state.lastRain:0}, dawn is {TideMinute(7, DawnMin):0}), and it is day {state.day}", state.day == 7 && MathF.Abs(state.lastRain - TideMinute(7, DawnMin)) < 2);
        // Short steps while it rains.
        state.weather = "rain"; state.lastRain = -1000000f; float t1 = TideNow; Advance(0.5f, quiet: true);
        Check("short steps in the rain keep it current", MathF.Abs(state.lastRain - (t1 + 0.5f)) < 0.01f);
        state.weather = "clear";

        /* ---------- wetness dries over about three hours ---------- */
        state.lastRain = TideNow; float w0 = WetTarget();
        state.lastRain = TideNow - 100; float w100 = WetTarget();
        state.lastRain = TideNow - 250; float w250 = WetTarget();
        state.lastRain = -1000000f; float wNever = WetTarget();
        Check($"the ground is soaked when rain has just stopped ({w0:0.00}), half dry after 100 minutes ({w100:0.00}), dry after 200 ({w250:0.00}), and dry in a fresh game ({wNever:0.00})",
            w0 > 0.99f && MathF.Abs(w100 - 0.5f) < 0.05f && w250 == 0 && wNever == 0);
        Check("an old save without it starts dry", new State().lastRain < -100000f);

        /* ---------- wet ground on screen ---------- */
        (player.X, player.Y) = StandableNear(160f, 115f); state.clock = 12 * 60; ClearSkies(); yield return 4;
        long Hash(Pix p) => PixHash(p);
        Color[] Frame(float wet, bool lightOn = true, int rung = 0)
        {
            wetShown = wet; Quality.Rung = rung; EffectsOff = !lightOn;
            RenderWorld(time);
            var copy = (Color[])pix.Buf.Clone();
            Quality.Rung = 0; EffectsOff = false;
            return copy;
        }
        var dry = Frame(0); var soaked = Frame(1);
        int SumAt(Color[] f, Func<char, bool> pick)
        {
            long s = 0; int n = 0;
            for (int py = 0; py < H; py++) for (int px = 0; px < W; px++)
                {
                    int wx = px + camX, wy = py + camY; if (wx < 0 || wy < 0 || wx >= PW || wy >= PH) continue;
                    if (!pick(shape[wy * PW + wx])) continue;
                    var c = f[py * W + px]; s += c.R + c.G + c.B; n++;
                }
            return n == 0 ? 0 : (int)(s / n);
        }
        int sandDry = SumAt(dry, c => c == 's'), sandWet = SumAt(soaked, c => c == 's'), grassDry = SumAt(dry, c => c == 'g'), grassWet = SumAt(soaked, c => c == 'g');
        Note($"sand {sandDry} -> {sandWet}, grass {grassDry} -> {grassWet}");
        Check($"rain darkens sand ({sandDry} -> {sandWet})", sandDry > 0 && sandWet < sandDry - 25);
        Check($"and grass less ({grassDry} -> {grassWet})", grassDry > 0 && (grassDry - grassWet) < (sandDry - sandWet) && grassWet <= grassDry);
        int sea0 = SumAt(dry, c => c is 'w' or '~'), sea1 = SumAt(soaked, c => c is 'w' or '~');
        Check($"the sea is untouched ({sea0} -> {sea1})", sea0 > 0 && Math.Abs(sea0 - sea1) < 4);
        var half = Frame(0.5f);
        int sandHalf = SumAt(half, c => c == 's');
        Check($"half wet is in between ({sandHalf})", sandHalf < sandDry && sandHalf > sandWet);
        var off = Frame(1, lightOn: false); var offDry = Frame(0, lightOn: false);
        Check("with light off the ground never changes", off.SequenceEqual(offDry));
        var capped = Frame(1, rung: 5);
        Check("the quality ladder's step 5 drops the pass", capped.SequenceEqual(Frame(0, rung: 5)));
        long baseBefore = Hash(worldBase);
        Frame(1);
        Check("the baked layer and the chart's picture are untouched", Hash(worldBase) == baseBefore);

        // Frostfang: it snows, the ground doesn't go wet.
        (player.X, player.Y) = StandableNear(620f, 90f); yield return 4;
        var snowDry = Frame(0); var snowWet = Frame(1);
        Check("on Frostfang the ground doesn't go wet", snowDry.SequenceEqual(snowWet));
        (player.X, player.Y) = StandableNear(160f, 115f); yield return 4;

        // Puddles stand on sand and paths only while it's soaked.
        int Pale(Color[] f) { int n = 0; foreach (var c in f) if (c.B > c.R + 40 && c.B > 190 && c.G > 170) n++; return n; }
        Check($"puddles show when it's soaked and are gone when it's half dry ({Pale(soaked)} against {Pale(half)})", Pale(soaked) >= Pale(half));
        wetShown = 0;

        /* ---------- clouds, grade and shafts ---------- */
        rainAmt = 0; overShown = 0; Clear2();
        int fairShade = CloudShade(); overShown = 1; int overShade = CloudShade();
        Check($"clouds are darker and more under an overcast sky ({fairShade} -> {overShade} shaded pixels)", overShade > fairShade * 1.3f + 20);
        rainAmt = 1; int rainShade = CloudShade();
        Check($"and they stay while it rains instead of fading out ({rainShade})", rainShade > fairShade);
        overShown = 0; rainAmt = 0;
        var clear = GradeNow(); overShown = 1; var grey = GradeNow(); overShown = 0;
        Check($"an overcast sky is a little cooler and greyer ({clear.R:0.00}/{clear.Sat:0.00} -> {grey.R:0.00}/{grey.Sat:0.00})", grey.R < clear.R && grey.Sat < clear.Sat);
        state.weather = "clear"; state.clock = 12 * 60;
        state.lastRain = TideNow - 60; Clear2(); DrawSunBeams(time); int shaftPx = LitNow();
        state.lastRain = -1000000f; Clear2(); DrawSunBeams(time); int noShaft = LitNow();
        Check($"after rain, shafts of sun stand through the clearing sky at midday ({shaftPx} pixels, none otherwise: {noShaft})", shaftPx > 200 && noShaft == 0);
        EffectsOff = true; state.lastRain = TideNow - 60; Clear2(); DrawSunBeams(time);
        Check("not with light off", LitNow() == 0);
        EffectsOff = false; state.lastRain = -1000000f;

        // The eased amounts move gradually and snap on a load.
        state.weather = "rain"; wetShown = 0; overShown = 0;
        UpdateOvercast(1f);
        Check($"the ground wets over a few seconds, not at once ({wetShown:0.00})", wetShown > 0.05f && wetShown < 0.2f);
        SnapWeather();
        Check("SnapWeather puts both straight there", wetShown == 1 && overShown == 1);
        state.weather = "clear"; SnapWeather();

        d.light = -1; EffectsOff = keepOff; wetShown = overShown = 0;
        pendingShot = "weather-done"; yield return 2;

        void Clear2() { Array.Fill(pix.Buf, new Color(60, 120, 80, 255)); pix.CamX = pix.CamY = 0; }
        int LitNow() => pix.Buf.Count(c => !c.Equals(new Color(60, 120, 80, 255)));
        int CloudShade() { int n = 0; for (int cx = 0; cx < PW - W; cx += 280) { Clear2(); camX = cx; camY = 60; liveRender = true; DrawCloudShadows(time); liveRender = false; n += LitNow(); } camX = camY = 0; return n; }
    }
}
#endif
