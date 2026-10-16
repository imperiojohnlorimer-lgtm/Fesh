#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;
// FESH_SCENIC=poster: pixel-view shots of the big moments (Tidemane's fight, Platejaw, the eclipse) for promotional art.
partial class Game
{
    IEnumerable<int> PosterScript()
    {
        Note("Poster shots");
        state = new State { created = true, look = new Look { name = "Painter" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;

        // Tidemane on the sand by the Starwell, at night.
        SetNight(true);
        state.hp = 9999;
        player.X = StarwellX - 70; player.Y = StarwellY + 24; player.Face = "right"; yield return 4;
        BossBreach();
        yield return 20; pendingPixShot = "p-tide-1"; yield return 2;
        for (int i = 0; i < 90; i++) { state.hp = 9999; yield return 1; }
        pendingPixShot = "p-tide-2"; yield return 2;
        for (int i = 0; i < 120; i++) { state.hp = 9999; yield return 1; }
        pendingPixShot = "p-tide-3"; yield return 2;
        BossRetreat(null); yield return 2;

        // Platejaw rising from the Ancient pool.
        state.commons["ancient_coelacanth"] = 1; state.caveDeepest = AncientFloor;
        caveFloor = AncientFloor; LoadScene("cave"); yield return 3;
        player.X = RuneStoneX; player.Y = RuneStoneY + 9; player.Face = "up"; yield return 3;
        GuardianRise(false);
        for (int i = 0; i < 40; i++) { state.hp = 9999; yield return 1; }
        pendingPixShot = "p-plate-1"; yield return 2;
        for (int i = 0; i < 100; i++) { state.hp = 9999; yield return 1; }
        pendingPixShot = "p-plate-2"; yield return 2;
        for (int i = 0; i < 100; i++) { state.hp = 9999; yield return 1; }
        pendingPixShot = "p-plate-3"; yield return 2;
        LoadScene("world"); yield return 3;

        // Bakunawa takes the moon.
        state.parola = 3; state.hinted["parolaLit"] = true;
        foreach (var c in Data.MoonClues) state.hinted[c.Key] = true;
        Give("agong", 1);
        state.day = 9; SetNight(true); ClearSkies();
        player.X = 1190; player.Y = 625; player.Face = "up"; yield return 3;
        while (mode == "dialogue") { Inp.Tap(KeyboardKey.E); yield return 2; }
        for (int i = 0; i < 400 && eclipse?.Phase == "rise"; i++) yield return 1;
        pendingPixShot = "p-eclipse-1"; yield return 2;
        for (int i = 0; i < 60; i++) yield return 1;
        pendingPixShot = "p-eclipse-2"; yield return 2;
        Note("Poster shots done");
    }
}
#endif
