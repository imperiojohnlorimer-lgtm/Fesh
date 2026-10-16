#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;
// FESH_SCENIC=poster2: more cinematic moments for promotional art.
partial class Game
{
    IEnumerable<int> Poster2Script()
    {
        Note("Poster shots 2");
        state = new State { created = true, look = new Look { name = "Painter" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;

        // Fireflies and glowing water at Bakawan.
        state.day = 4; SetNight(true); ClearSkies();
        player.X = FireflyTrees[1].x - 26; player.Y = FireflyTrees[1].y; player.Face = "right"; yield return 40;
        pendingPixShot = "q-fireflies-1"; yield return 2;
        for (int i = 0; i < 50; i++) yield return 1;
        pendingPixShot = "q-fireflies-2"; yield return 2;

        // Baga at Level 3, at dusk.
        state.hinted["mg:unrest"] = true; state.hinted["mg:read1"] = true; state.hinted["mg:read2"] = true;
        state.gifts["mg_order"] = state.day; UpdateBagaLevel();
        (player.X, player.Y) = StandableNear(ConeX + 20, ConeBaseY + 14); player.Face = "up";
        state.clock = 19 * 60 + 40; yield return 40;
        pendingPixShot = "q-baga-1"; yield return 2;
        SetNight(true); yield return 40;
        pendingPixShot = "q-baga-2"; yield return 2;
        state.hinted.Remove("mg:unrest"); state.hinted.Remove("mg:read1"); state.hinted.Remove("mg:read2");

        // A storm at sea in the banca.
        SetNight(false); ClearSkies();
        state.aboard = true; player.X = state.boatX = 1385; player.Y = state.boatY = 395; player.Face = "right"; yield return 5;
        state.forecast = new() { new WeatherSpell { at = 0, w = "storm" } }; state.weather = planned = "storm"; SnapWeather();
        for (int i = 0; i < 90; i++) yield return 1;
        pendingPixShot = "q-storm-1"; yield return 2;
        state.aboard = false; ClearSkies(); SnapWeather();
        (player.X, player.Y) = StandableNear(1640, 175); yield return 3;

        // The tsunami, from School Rise.
        SetNight(false); ClearSkies();
        state.gifts["rs_ready"] = 1;
        StartEmergency();
        for (int i = 0; i < 2000 && evac != null && evac.T < 9; i++) yield return 1;
        player.X = RiseX; player.Y = RiseY + 6; player.Face = "left"; yield return 2;
        for (int i = 0; i < 4000 && evac != null && evac.T < 22.5f; i++) yield return 1;
        pendingPixShot = "q-sea-out"; yield return 2;
        for (int i = 0; i < 4000 && evac != null && evac.T < 33; i++) yield return 1;
        pendingPixShot = "q-sea-in"; yield return 2;
        for (int i = 0; i < 4000 && evac != null && evac.T < 38; i++) yield return 1;
        pendingPixShot = "q-sea-flood"; yield return 2;
        Note("Poster shots 2 done");
    }
}
#endif
