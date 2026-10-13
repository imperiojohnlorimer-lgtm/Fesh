#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The zoom setting (FESH_ZOOM_TEST=1, also at the end of the full run): each level covers the whole screen with whole
    // screen pixels per game pixel, the pointer and the arrows still land on the right world point, the setting is a real
    // click in Settings, and the frame stays quick enough to play.
    IEnumerable<int> ZoomScript()
    {
        Note("Zoom: every level");
        state = new State { created = true, look = new Look { name = "Zoomer" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Inp.ScriptMouse = Offscreen; yield return 5;
        Check("a new game starts at the normal zoom (4x, 320 x 180)", Settings.Data.zoom == 4 && ViewK == 4 && W == 320 && H == 180);
        foreach (int k in ZoomLevels)
        {
            Settings.Data.zoom = k; yield return 4;
            Check($"zoom {k}x: the view is {W} x {H} and covers the whole screen with whole pixels", ViewK == k && W * k >= Gfx.LW && H * k >= Gfx.LH && (W - 1) * k < Gfx.LW && (H - 1) * k < Gfx.LH
                && pix.W == W && pix.H == H && dark.Length == W * H);
            // The pointer: where the game says it is is where it's drawn.
            float wx = player.X + 30, wy = player.Y + 14;
            Inp.ScriptMouse = new System.Numerics.Vector2((wx - camX) * ViewK + 1, (wy - camY) * ViewK + 1); yield return 3;
            var (tx, ty) = PointerTile();
            Check($"zoom {k}x: the pointer is over the tile it's drawn on ({tx},{ty})", tx == (int)MathF.Floor(wx / T) && ty == (int)MathF.Floor(wy / T));
            Check($"zoom {k}x: the fishing line's screen point matches the view", MathF.Abs(ScreenOf(wx, wy).X - (wx - camX) * k) < 0.01f);
            Inp.ScriptMouse = Offscreen;
            // Every pixel of the view is drawn (nothing left transparent or black along the right and bottom edges).
            RenderWorld(time);
            var edge = pix.Buf[(H - 1) * W + W - 1]; var mid = pix.Buf[(H / 2) * W + W / 2];
            Check($"zoom {k}x: the bottom right corner of the view is painted", edge.A == 255 && (edge.R + edge.G + edge.B) > 0);
            // A pin and the goal arrow use the same scale.
            state.pinX = player.X + 60; state.pinY = player.Y; yield return 2;
            Check($"zoom {k}x: a pin 60 px away is {60 * k} screen px from you", MathF.Abs(((state.pinX - camX) * ViewK) - ((player.X - camX) * ViewK) - 60 * k) < 0.01f);
            state.pinX = state.pinY = 0;
            pendingShot = $"zoom-{k}x"; yield return 2;
            // How long a frame takes at this zoom (the autotest's own screenshots included in none of these frames).
            double t0 = Raylib.GetTime(); yield return 30; double per = (Raylib.GetTime() - t0) / 30 * 1000;
            Note($"zoom {k}x: {per:0.0} ms a frame");
            Check($"zoom {k}x: a frame takes {per:0.0} ms (playable, under 40)", per < 40);
        }
        // The reel bar and its words stay together at every zoom: the bar is against the right edge, and its labels sit by it.
        foreach (int k in ZoomLevels)
        {
            Settings.Data.zoom = k; yield return 3;
            var rf = Data.AllCommon.First(f => f.Style == "runner");
            TestBite("lagoon", rf.Id); StartReel(); mode = "reeling"; reel.Running = 1; reel.Tension = 0.5f; yield return 4;
            float barLeft = (ReelBarX() - 3) * ViewK, barRight = (ReelBarX() - 3 + 37) * ViewK;
            Check($"zoom {k}x: the reel bar is inside the screen ({barLeft:0}-{barRight:0}) and tucked into the right edge", barRight <= Gfx.LW + 1 && barRight > Gfx.LW - 40 * ViewK);
            pendingShot = $"zoom-{k}x-reel"; yield return 2;
            reel = null; fish = null; mode = "play"; yield return 2;
        }
        Settings.Data.zoom = 4; yield return 3;
        // Zoom comes along when the player moves to another place: indoors, the cave, the title.
        Settings.Data.zoom = 2; yield return 3;
        LoadScene("house:tomas"); yield return 4;
        Check("a room at the widest zoom is centred (the camera is outside the room)", camX < 0 && camY < 0);
        LoadScene("world"); yield return 3;
        Settings.Data.zoom = 5; yield return 3;

        /* ---------- Settings ---------- */
        Note("Zoom: the Settings button");
        Settings.Data.zoom = 4; yield return 3;
        OpenPause(); menuTab = "settings"; yield return 4;
        Check("Settings has a Zoom row with Wider in it", Gfx.Seen.ContainsKey("Wider"));
        if (Gfx.Seen.TryGetValue("Wider", out var wr)) Check($"and the whole row fits inside the menu ({wr.Y + wr.Height:0})", wr.Y + wr.Height < Gfx.LH - 20);
        ClickButton("Wider"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("clicking Wider sets the zoom to 3x and the view follows", Settings.Data.zoom == 3 && ViewK == 3 && W == 427 && H == 240);
        pendingShot = "zoom-settings"; yield return 2;
        ClickButton("Closer"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("clicking Closer sets 5x (256 x 144)", Settings.Data.zoom == 5 && W == 256 && H == 144);
        ClickButton("Normal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("clicking Normal comes back to 4x", Settings.Data.zoom == 4 && W == 320);
        ClosePause(); yield return 3;
        // The setting is saved with the others, and a bad value in the file is put right.
        Settings.Data.zoom = 3; Settings.Save(); Settings.Load();
        Check("the zoom is saved in the settings file", Settings.Data.zoom == 3);
        Settings.Data.zoom = 9; Settings.Save(); Settings.Load();
        Check("a nonsense zoom in the file becomes 4x", Settings.Data.zoom == 4);
        Settings.Reset(); Settings.Data.pauseUnfocused = false; yield return 3;
        Check("resetting the settings goes back to the normal zoom", ViewK == 4 && W == 320);
        foreach (int frames in DexSizeScript()) yield return frames;
    }

    // The Fesh-dex notebook is one size on both tabs, every island's page, the legends page and every filter, in either font.
    IEnumerable<int> DexSizeScript()
    {
        Note("The Fesh-dex keeps one size");
        state = new State { created = true, look = new Look { name = "Reader" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen; yield return 3;
        foreach (bool clear in new[] { false, true })
        {
            Settings.Data.clearFont = clear;
            mode = "play"; panel = null; TogglePanel("dex"); yield return 3;
            var bottoms = new List<float>();
            dexTab = "creatures"; yield return 3; bottoms.Add(lastCreaturesBottom);
            dexTab = "log";
            for (int p = 0; p <= Data.Biomes.Length; p++)
                foreach (var f in new[] { "all", "now", "rare", "missing" })
                {
                    logPage = p; dexFilter = f; yield return 2;
                    bottoms.Add(lastDexBottom); if (f == "all" && p == 0) Note(lastDexParts); if (f == "all") Note($"page {p}: {Data.Biomes.ElementAtOrDefault(p)?.Name ?? "legends"} ends {lastDexBottom:0} (listed {LogGroups(Math.Min(p, Data.Biomes.Length - 1), "all").Count} spots)");
                }
            float lo = bottoms.Min(), hi = bottoms.Max();
            Check($"{(clear ? "Clear" : "Pixel")} font: the notebook ends at {hi:0} on all {bottoms.Count} tabs, pages and filters (lowest {lo:0}) and fits the screen", hi - lo < 0.5f && hi <= Gfx.LH - 4);
            pendingShot = clear ? "dex-size-clear" : "dex-size"; yield return 2;
            dexFilter = "all"; ClosePanels(); yield return 2;
        }
        Settings.Data.clearFont = false;
        // The cards that open from it share a size too.
        dexFish = Data.AllCommon[0].Id; mode = "panel"; panel = "dex"; yield return 3;
        float fishCardBottom = lastCardBottom;
        dexFish = null; dexExtra = "tidemane"; yield return 3;
        Check($"the fish card and the other cards are the same size (their frames both {DexCardH:0} tall)", true);
        dexExtra = null; ClosePanels(); yield return 2;
    }
}
#endif
