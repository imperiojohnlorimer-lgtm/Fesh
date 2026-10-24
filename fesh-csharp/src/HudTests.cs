#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_HUD_TEST=1: the pause menu's Game tab fitting its panel in both fonts, and the Minimal HUD setting (Graphics tab)
// by real clicks: the status card and top buttons gone, the menu button and hotbar still there, First steps keeping it all.
partial class Game
{
    IEnumerable<int> HudScript()
    {
        Note("The pause menu's fit and the Minimal HUD");
        state = new State { created = true, look = new Look { name = "Huddy" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;
        foreach (bool clear in new[] { false, true })
        {
            Settings.Data.clearFont = clear;
            OpenPause(); menuTab = "game"; yield return 4;
            float panelBottom = (Gfx.LH + 625) / 2f - 28;
            Check($"every Game tab button sits inside the pause panel, {(clear ? "Clear" : "pixel")} font ({lastPauseBottom:0} of {panelBottom:0})", lastPauseBottom > 0 && lastPauseBottom <= panelBottom + 0.5f);
            pendingShot = $"hud-pause-{(clear ? "clear" : "pixel")}"; yield return 2;
            ClosePause(); yield return 3;
        }
        Settings.Data.clearFont = false;

        // Turning it on in the Graphics tab, by a real click.
        OpenPause(); menuTab = "graphics"; yield return 4;
        Check("the Graphics tab offers the HUD: Full or Minimal", Gfx.Seen.ContainsKey("Minimal") && Gfx.Seen.ContainsKey("Full"));
        pendingShot = "hud-graphics"; yield return 2;
        ClickButton("Minimal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("clicking Minimal sets it", Settings.Data.minimalHud);
        ClosePause(); yield return 4;
        Gfx.Seen.Remove("Map"); Gfx.Seen.Remove("Fesh-dex"); yield return 3;
        Check("with a minimal HUD the top buttons aren't drawn", !Gfx.Seen.ContainsKey("Map") && !Gfx.Seen.ContainsKey("Fesh-dex"));
        Check("but the hotbar is", HotbarShown);
        pendingShot = "hud-minimal"; yield return 2;
        // The menu button is still there and still opens the menu.
        Inp.ScriptMouse = new System.Numerics.Vector2(Gfx.LW - 13 - 18, 13 + 18); Inp.ScriptClickNext = true; yield return 3;
        Inp.ScriptMouse = Offscreen;
        Check("the menu button in the corner still opens the pause menu", mode == "pause");
        ClosePause(); yield return 3;
        Inp.Tap(KeyboardKey.Tab); yield return 3;
        Check("its key (Tab) still opens the map with a minimal HUD", mode == "panel" && panel == "map");
        ClosePanels(); yield return 3;
        // First steps point at the HUD's buttons: the full HUD comes back while they run.
        tourOff = false; state.tour = 0; yield return 3;
        Check("during First steps the full HUD is shown", !MinimalHud);
        state.tour = TourDone; tourOff = true; yield return 2;
        Check("and after them, minimal again", MinimalHud);
        Settings.Data.minimalHud = false; Settings.Save(); yield return 3;
        Check("Full brings everything back", Gfx.Seen.ContainsKey("Map"));
    }
}
#endif
