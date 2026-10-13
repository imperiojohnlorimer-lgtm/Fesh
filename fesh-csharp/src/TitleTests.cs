#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Just the title screen (FESH_TITLE_TEST=1): the words as they were, the buttons by real clicks, the sea and the leaping fish.
    IEnumerable<int> TitleShotScript()
    {
        Note("The title screen");
        state = new State { created = true, look = new Look { name = "Newcomer" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;
        QuitToTitle(); yield return 30;
        Check("the title shows its buttons", Gfx.Seen.ContainsKey("Continue") && Gfx.Seen.ContainsKey("New game") && Gfx.Seen.ContainsKey("Load game"));
        Check("with the same line about the game", TitleTagline.StartsWith("A fishing mystery from Saltmere to Starfall"));
        // A fish is in the air a second and a half into every nine-second round.
        time = 9 * 40 + 1.5f; yield return 2;
        pendingShot = "title-a-fish"; yield return 2;
        time = 9 * 41 + 0.2f; yield return 2;
        pendingShot = "title-b-spray"; yield return 2;
        time = 9 * 42 + 6f; yield return 2;
        pendingShot = "title-c-calm"; yield return 2;
        var a = new Pix(WavesW, WavesH); var b = new Pix(WavesW, WavesH);
        PaintTitleWaves(a, 1.4f); PaintTitleWaves(b, 5.5f);
        Check("the sea moves, and the fish only leaps now and then", a.Buf.Count(c => c.R > 200 && c.G > 100 && c.B < 80) > 5 && b.Buf.Count(c => c.R > 200 && c.G > 100 && c.B < 80) == 0);
        bool clicked = ClickButton("New game"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"New game works by a real click ({mode}, {titleView})", clicked && (mode == "create" || titleView == "slots"));
    }
}
#endif
