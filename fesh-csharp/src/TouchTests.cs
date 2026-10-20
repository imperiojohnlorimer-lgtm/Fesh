#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Phone play (FESH_TOUCH_TEST=1): the chest's arrows are tap buttons and the timer is kinder, and a tap that isn't on a
    // button no longer acts like E. Platform.Touch is set here the way the Android app sets it.
    IEnumerable<int> TouchScript()
    {
        Note("Touch: arrow pad and stray taps");
        bool touch0 = Platform.Touch, tap0 = Platform.TapWorld;
        state = new State { created = true, look = new Look { name = "Toucher" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Inp.ScriptMouse = Offscreen; yield return 5;

        // The chest: desktop numbers first.
        Platform.Touch = false;
        TestBite("lagoon", "pond_perch");
        fish.Roll = new Catchable { Id = "chest", Name = "Sunken chest", Difficulty = 2, Chest = true };
        Hook();
        float deskTotal = chestTotal;
        Check("on a PC the chest gives 4.5 s plus 0.35 s an arrow", MathF.Abs(deskTotal - (4.5f + chestSeq.Count * 0.35f)) < 0.01f);
        yield return 6;
        Check("the four arrow buttons are drawn for a mouse too", Enumerable.Range(0, 4).All(d => Gfx.Seen.ContainsKey("arrow:" + d)));
        float t0 = chestT;
        ClickButton("arrow:" + (chestSeq[0] + 1) % 4); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("on a PC a wrong arrow still costs 0.8 s", chestAt == 0 && chestT < t0 - 0.75f && chestT > t0 - 1.4f);
        mode = "play"; fish = null; yield return 3;

        // The chest on a phone: a bigger window, a lighter penalty, and every arrow by a tap.
        Platform.Touch = true;
        TestBite("lagoon", "pond_perch");
        fish.Roll = new Catchable { Id = "chest", Name = "Sunken chest", Difficulty = 2, Chest = true };
        Hook();
        Check("a chest on a phone gets 1.5 s more", chestSeq.Count >= 5 && MathF.Abs(chestTotal - (4.5f + chestSeq.Count * 0.35f + 1.5f)) < 0.01f);
        yield return 6;
        pendingShot = "touch-chest"; yield return 2;
        t0 = chestT;
        ClickButton("arrow:" + (chestSeq[0] + 1) % 4); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"a wrong tap costs 0.4 s, not 0.8 ({t0 - chestT:0.00} s)", chestAt == 0 && chestT < t0 - 0.35f && chestT > t0 - 0.75f);
        int coins0 = state.coins, chests0 = state.chests;
        foreach (int d in chestSeq.ToList())
        {
            ClickButton("arrow:" + d); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        }
        Check($"tapping the arrows in order hauls it up ({toastMsg})", mode == "play" && state.chests == chests0 + 1 && state.coins >= coins0 + 20);
        Check("a tap left over from the chest isn't kept", arrowTap == null);

        // A chest left to run out still sinks.
        yield return 10;
        TestBite("lagoon", "pond_perch");
        fish.Roll = new Catchable { Id = "chest", Name = "Sunken chest", Difficulty = 2, Chest = true };
        Hook();
        chestT = 0.05f; yield return 4;
        Check("a chest nobody hauls still slips off", mode != "chest");
        mode = "play"; fish = null; yield return 3;
        // Out of time is out of time: a last tap in the frame it runs out doesn't haul it up.
        TestBite("lagoon", "pond_perch");
        fish.Roll = new Catchable { Id = "chest", Name = "Sunken chest", Difficulty = 2, Chest = true };
        Hook();
        yield return 2;
        int chestsLate = state.chests;
        chestT = 0.0005f; chestAt = chestSeq.Count - 1; arrowTap = chestSeq[chestAt]; yield return 3;
        Check("a final arrow on the frame the time runs out is too late", mode != "chest" && state.chests == chestsLate);
        mode = "play"; fish = null; arrowTap = null; yield return 3;

        // Stray taps: stand where E would cast, then touch the empty sea of the screen.
        Platform.Touch = true; Platform.TapWorld = false;
        var (sx, sy) = StandNear("lagoon");
        player.X = sx; player.Y = sy; player.Face = "up"; mode = "play"; fish = null; yield return 4;
        Check($"E has something to do here ({prompt.Text})", target?.Type == "spot");
        var empty = new System.Numerics.Vector2(640, 120);
        Inp.ScriptMouse = empty; yield return 2;
        Inp.ScriptClickNext = true; yield return 3;
        Check("a tap on empty screen does nothing on a phone", mode == "play");
        Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the pointer taps are only ignored in the modes that act", TapsIgnored("play") && TapsIgnored("reeling") && TapsIgnored("bite")
            && TapsIgnored("boatwork") && TapsIgnored("boatanim") && !TapsIgnored("build") && !TapsIgnored("dialogue") && !TapsIgnored("spear") && !TapsIgnored("chest"));

        Platform.TapWorld = true;
        Inp.ScriptMouse = empty; yield return 2;
        Inp.ScriptClickNext = true; yield return 3;
        Check($"with 'Acts like E' on, the tap casts again ({mode})", mode != "play");
        Inp.ScriptMouse = Offscreen; mode = "play"; fish = null; yield return 4;

        Platform.TapWorld = false;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"the E button still works ({mode})", mode != "play");
        mode = "play"; fish = null; yield return 4;

        Platform.Touch = false;
        Inp.ScriptMouse = empty; yield return 2;
        Inp.ScriptClickNext = true; yield return 3;
        Check($"on a PC a click on the world acts as before ({mode})", mode != "play");
        Inp.ScriptMouse = Offscreen; mode = "play"; fish = null; yield return 4;

        // The Touch tab shows the new row.
        Platform.Touch = true;
        OpenPause(); menuTab = "controls"; yield return 4;
        Check("the Touch tab has the Tap the screen choices", Gfx.Seen.ContainsKey("Buttons only") && Gfx.Seen.ContainsKey("Acts like E"));
        pendingShot = "touch-tab"; yield return 2;
        bool changed = false; Platform.Changed = () => changed = true;
        ClickButton("Acts like E"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("a real click on 'Acts like E' sets it and tells the app to save", Platform.TapWorld && changed);
        Platform.Changed = null;
        ClosePause(); yield return 3;

        foreach (int frames in BagSlotScript()) yield return frames;

        Platform.Touch = touch0; Platform.TapWorld = tap0;
    }

    // The bag is the seventh box on the hotbar (and no longer a button in the top row).
    IEnumerable<int> BagSlotScript()
    {
        Note("Bag: the seventh hotbar box");
        state = new State { created = true, look = new Look { name = "Packer" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Inp.ScriptMouse = Offscreen; yield return 6;
        Check("the bag is a box on the hotbar", Gfx.Seen.TryGetValue("Bag", out var bagBox) && bagBox.Y > Gfx.LH - 80 && bagBox.Width == 50);
        var six = Gfx.Seen["hotbar:6"];
        Check($"it sits to the right of the sixth slot ({Gfx.Seen["Bag"].X:0} > {six.X + six.Width:0})", Gfx.Seen["Bag"].X > six.X + six.Width + 8);
        Check("and the top row keeps its other buttons", new[] { "Build", "Tackle", "Map", "Fesh-dex", "Case board" }.All(l => Gfx.Seen.ContainsKey(l) && Gfx.Seen[l].Y < 80));
        ClickButton("Bag"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"a click on it opens the bag ({mode}, {panel})", mode == "panel" && panel == "bag");
        ClosePanels(); yield return 3;
        Inp.Tap(KeyboardKey.I); yield return 3;
        Check("and its key still does", mode == "panel" && panel == "bag");
        ClosePanels(); yield return 3;
        pendingShot = "bag-slot"; yield return 2;
    }
}
#endif
