#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The title's opening and the ending card (1.44, FESH_INTRO_TEST=1): the opening's stages by screenshot, no buttons
    // until they arrive, a key skipping it without also pressing the title's own shortcut, Reduce motion's short fade, and
    // the ending card fitting the screen (both fonts), saving itself as an image and Keep exploring by a real click.
    IEnumerable<int> IntroScript()
    {
        Note("The title's opening and the ending card");
        Settings.Data.pauseUnfocused = false; Settings.Data.reduceMotion = false; Inp.ScriptMouse = Offscreen;
        mode = "title"; titleView = "main"; slotInfo = null;
        yield return 3;
        IEnumerable<int> Until(float t) { for (int i = 0; i < 900 && introT < t && !introDone; i++) yield return 1; }

        /* ---------- the opening ---------- */
        introDone = false; introT = 0; Gfx.Seen.Remove("New game"); Gfx.Seen.Remove("Continue");
        foreach (var s in Until(0.8f)) yield return s;
        pendingShot = "i01-eyes"; yield return 2;
        Check("under the sea at first, with no buttons yet", !Gfx.Seen.ContainsKey("New game") && !Gfx.Seen.ContainsKey("Continue") && !introDone);
        Check($"on the seabed to begin with ({IntroCamY(introT):0})", IntroCamY(introT) >= DeepH - 181);
        foreach (var s in Until(2.2f)) yield return s;
        pendingShot = "i02-reef"; yield return 2;
        foreach (var s in Until(3.0f)) yield return s;
        pendingShot = "i03-rising"; yield return 2;
        foreach (var s in Until(3.95f)) yield return s;
        pendingShot = "i04-bite"; yield return 2;
        Check($"at the surface by the bite ({IntroCamY(introT):0})", IntroCamY(introT) < 1);
        foreach (var s in Until(4.35f)) yield return s;
        pendingShot = "i05-jerk"; yield return 2;
        foreach (var s in Until(4.9f)) yield return s;
        pendingShot = "i06-surfacing"; yield return 2;
        foreach (var s in Until(5.6f)) yield return s;
        pendingShot = "i07-letters"; yield return 2;
        foreach (var s in Until(6.9f)) yield return s;
        pendingShot = "i08-tagline"; yield return 2;
        Check("still no buttons while the line about the game arrives", !Gfx.Seen.ContainsKey("New game") && !Gfx.Seen.ContainsKey("Continue"));
        foreach (var s in Until(99)) yield return s;
        yield return 30;
        pendingShot = "i09-done"; yield return 2;
        Check($"then it's over ({introT:0.0} s) and the buttons are there", introDone && (Gfx.Seen.ContainsKey("New game") || Gfx.Seen.ContainsKey("Continue")) && introT < IntroLen + 0.5f);

        // A key skips it, and isn't also the title's own shortcut (Enter would start a game).
        introDone = false; introT = 0; yield return 20;
        Inp.Tap(KeyboardKey.Enter); yield return 4;
        Check($"a key skips the opening and does nothing else ({mode}, {titleView})", introDone && mode == "title" && titleView == "main");
        // A click too.
        introDone = false; introT = 0; yield return 20;
        Inp.ScriptMouse = new System.Numerics.Vector2(640, 400); Inp.ScriptClickNext = true; yield return 4; Inp.ScriptMouse = Offscreen;
        Check("so does a click", introDone && mode == "title");
        // A click right where a button will appear skips, and doesn't also press it (Codex: the buttons are drawn the
        // same frame, while the click is still down).
        yield return 10;
        var btn = Gfx.Seen.TryGetValue("New game", out var nb) ? nb : Gfx.Seen["Continue"];
        introDone = false; introT = 0; yield return 20;
        Inp.ScriptMouse = new System.Numerics.Vector2(btn.X + btn.Width / 2, btn.Y + btn.Height / 2); Inp.ScriptClickNext = true; yield return 6; Inp.ScriptMouse = Offscreen;
        Check($"a click where a button will be skips without pressing it ({mode}, {titleView})", introDone && mode == "title" && titleView == "main");

        // A key with a job of its own (F3, the profiler) only skips (Codex: it also toggled).
        bool prof0 = Prof.Overlay;
        introDone = false; introT = 0; yield return 20;
        Inp.Tap(KeyboardKey.F3); yield return 4;
        Check("F3 during the opening skips it and leaves the profiler alone", introDone && Prof.Overlay == prof0);
        // Reduce motion: a short fade.
        Settings.Data.reduceMotion = true;
        introDone = false; introT = 0;
        for (int i = 0; i < 300 && !introDone; i++) yield return 1;
        Check($"with Reduce motion it's a short fade ({introT:0.00} s)", introDone && introT <= IntroCalmLen + 0.2f);
        Settings.Data.reduceMotion = false;

        /* ---------- story 1's catch cards (reworked in 1.44) fit, in both fonts ---------- */
        state = new State { created = true, look = new Look { name = "Messenger" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; yield return 3;
        bool cardsFit = true;
        foreach (bool clear in new[] { false, true })
        {
            Settings.Data.clearFont = clear;
            foreach (var cr in Data.Creatures)
            {
                catchId = cr.Id; catchOpenedAt = Raylib.GetTime(); mode = "catch"; yield return 3;
                if (lastCatchBottom > Gfx.LH - 4) { cardsFit = false; Note($"{cr.Name} ({(clear ? "Clear" : "pixel")}): {lastCatchBottom:0}"); }
                if (!clear && cr.Id is "glowgill" or "mirror_ray" or "abyssal") { pendingShot = "i12-catch-" + cr.Id; yield return 2; }
            }
        }
        Settings.Data.clearFont = false; mode = "play"; yield return 2;
        Check("every creature's catch card fits the screen, in both fonts", cardsFit);
        Check("the creatures have their Filipino names", Data.ById["glowgill"].Name == "Hasang-ilaw" && Data.ById["abyssal"].Name == "Bantay ng Lalim");

        /* ---------- the ending card ---------- */
        state = new State { created = true, look = new Look { name = "Closer" }, casts = 312, day = 19 };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); quietWildlife = true; standStill = true; yield return 3;
        state.commons["pond_perch"] = 40; state.caught.AddRange(new[] { "glowgill", "tidecrawler", "hollow_eel", "mirror_ray", "abyssal" });
        mode = "ending"; endOpenedAt = -1; yield return 40;
        pendingShot = "i10-ending"; yield return 2;
        Check($"the ending card fits the screen ({lastEndBottom:0})", lastEndBottom <= Gfx.LH - 4);
        Settings.Data.clearFont = true; yield return 4;
        Check($"and in the Clear font ({lastEndBottom:0})", lastEndBottom <= Gfx.LH - 4);
        pendingShot = "i11-ending-clear"; yield return 2;
        Settings.Data.clearFont = false; yield return 2;
        int before = Directory.Exists(PhotoDir) ? Directory.GetFiles(PhotoDir, "fesh-case-closed-*.png").Length : 0;
        ClickButton("Save as image"); yield return 6; Inp.ScriptMouse = Offscreen;
        Check("Save as image saves the card", Directory.GetFiles(PhotoDir, "fesh-case-closed-*.png").Length == before + 1 && !shotHadBar);
        yield return 3;
        ClickButton("Keep exploring"); yield return 4; Inp.ScriptMouse = Offscreen;
        Check($"Keep exploring goes back out to sea ({mode})", mode == "play");
    }
}
#endif
