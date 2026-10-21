#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Photo mode and saving a card (1.44, FESH_PHOTO_TEST=1): in from the pause menu by a real click, the HUD gone and the
    // clock stopped, looking about by a held key and by dragging, zooming by the buttons without touching the setting, the
    // frames and caption, a picture by E written as a PNG without the buttons in it, Esc out (not into the pause menu),
    // and a Fesh-dex card saved as an image of just the card.
    IEnumerable<int> PhotoScript()
    {
        Note("Photo mode");
        state = new State { created = true, look = new Look { name = "Snapper" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        if (Directory.Exists(PhotoDir)) Directory.Delete(PhotoDir, true);

        /* ---------- in from the pause menu ---------- */
        toastTimer = 0;
        Inp.Tap(KeyboardKey.Escape); yield return 4;
        Check("Esc opens the pause menu", mode == "pause");
        ClickButton("Photo mode"); yield return 4; Inp.ScriptMouse = Offscreen;
        Check("its Photo mode button opens photo mode", mode == "photo");
        Gfx.Seen.Remove("Map"); Gfx.Seen.Remove("Fesh-dex");
        float clock0 = state.clock; yield return 20;
        Check("the HUD is hidden (its buttons aren't drawn)", !Gfx.Seen.ContainsKey("Map") && !Gfx.Seen.ContainsKey("Fesh-dex"));
        Check($"the clock stops ({clock0:0.0} -> {state.clock:0.0})", state.clock == clock0);

        /* ---------- looking about ---------- */
        int cam0 = camX;
        Inp.Hold(KeyboardKey.Right, true); yield return 30; Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"a held move key looks about ({photoPanX:0} px, view {cam0} -> {camX}) without moving you", photoPanX > 20 && camX != cam0 && mode == "photo");
        float panBefore = photoPanX;
        Inp.ScriptMouse = new System.Numerics.Vector2(700, 400); yield return 2;
        Inp.ScriptMouseDown = true; yield return 2;
        Inp.ScriptMouse = new System.Numerics.Vector2(600, 400); yield return 3;
        Inp.ScriptMouseDown = false; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"dragging the view to the left looks right ({panBefore:0} -> {photoPanX:0})", photoPanX > panBefore + 10);
        photoPanX = 9999; yield return 2; UpdatePhoto(0);
        Check("it doesn't go further than a little way", photoPanX <= PhotoPanMax);

        /* ---------- zoom, frames, caption ---------- */
        int zoom0 = Settings.Data.zoom;
        ClickButton("photo:+"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"+ zooms in ({ViewK}) without changing the setting ({Settings.Data.zoom})", ViewK == 5 && Settings.Data.zoom == zoom0);
        ClickButton("photo:-"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        ClickButton("photo:-"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"- zooms out ({ViewK})", ViewK == 3);
        int f0 = photoFrame;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check("F changes the frame", photoFrame == (f0 + 1) % PhotoFrames.Length);
        bool cap0 = photoCaption;
        ClickButton("photo:caption"); yield return 4; Inp.ScriptMouse = Offscreen;
        Check("the caption button turns the caption off and on", photoCaption != cap0);
        photoCaption = true;
        for (int fr = 1; fr < PhotoFrames.Length; fr++) { photoFrame = fr; yield return 3; pendingShot = $"p0{fr}-{PhotoFrames[fr].ToLowerInvariant()}"; yield return 3; }

        /* ---------- taking it ---------- */
        photoFrame = 1; toastTimer = 0; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 6;
        var files = Directory.Exists(PhotoDir) ? Directory.GetFiles(PhotoDir, "fesh-photo-*.png") : Array.Empty<string>();
        Check($"E takes a picture: a PNG in the photos folder ({files.Length}, {lastShotSize.w}x{lastShotSize.h})", files.Length == 1 && new FileInfo(files[0]).Length > 1000 && lastShotSize.w > 100);
        Check("the buttons aren't in it", !shotHadBar);
        Check($"and the game says where it went ({toastMsg})", toastMsg.StartsWith("Saved to"));
        // A second straight after: the first one's flash isn't in it (Codex).
        Check("its flash is showing", photoFlash > 0);
        Inp.Tap(KeyboardKey.E); yield return 6;
        Check($"a second picture taken during the flash leaves the flash out ({Directory.GetFiles(PhotoDir, "fesh-photo-*.png").Length} pictures)",
            Directory.GetFiles(PhotoDir, "fesh-photo-*.png").Length == 2 && !shotHadFlash);
        Inp.Tap(KeyboardKey.Escape); yield return 4;
        Check($"Esc leaves photo mode, not into the pause menu ({mode}), and the view's zoom is the setting's again", mode == "play" && ViewK == Settings.Data.zoom);

        /* ---------- a card as an image ---------- */
        state.commons["pond_perch"] = 1;
        TogglePanel("dex"); dexFish = "pond_perch"; yield return 4;
        ClickButton("Save as image"); yield return 5; Inp.ScriptMouse = Offscreen; yield return 2;
        var cards = Directory.GetFiles(PhotoDir, "fesh-card-pond_perch-*.png");
        float k = Gfx.Z;
        Check($"\"Save as image\" saves just the card ({cards.Length}, {lastShotSize.w}x{lastShotSize.h} at x{k:0.00})",
            cards.Length == 1 && MathF.Abs(lastShotSize.w - (DexCardW + 8) * k) < 4 && MathF.Abs(lastShotSize.h - (DexCardH + 8) * k) < 4);
        // Closed before its frame comes: nothing is saved (Codex: it captured whatever was there instead).
        ClickButton("Save as image"); yield return 1; Inp.ScriptMouse = Offscreen;
        dexFish = null; yield return 5;
        Check("a card closed before its picture is taken saves nothing", Directory.GetFiles(PhotoDir, "fesh-card-*.png").Length == 1 && shotReq == null);
        ClosePanels(); yield return 2;
    }
}
#endif
