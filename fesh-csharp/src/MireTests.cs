#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_MIRE_TEST=1: the Mire leviathan's boss fight (MireBoss.cs): when it bites (and again after the first), the pool
// and its mangroves, the title card, bar and music, staying in the pool, a breath struck and one missed (the swamp going
// quiet and coming back), a run for the roots turned the right way (after a wrong key) and one let through, the leap,
// pausing by the real Resume button, landing it, losing it and fainting, with screenshots of each.
partial class Game
{
    IEnumerable<int> MireScript()
    {
        Note("The Mire leviathan: the boss fight");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(true); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hp = 100; state.inv["worm"] = 20;

        /* ---------- When it bites ---------- */
        bool Bitable() => FishWeights("swamp", "worm", 2).Any(p => p.f.Id == MireFish);
        Check("at night on a worm it can bite in the swamp", Bitable());
        SetNight(false);
        Check("never by day", !Bitable());
        SetNight(true);
        Check("never on another bait", FishWeights("swamp", "cricket", 2).All(p => p.f.Id != MireFish));
        state.commons[MireFish] = 1;
        Check("and after you've caught it once, it comes back for a rematch", Bitable());
        state.commons.Remove(MireFish);

        /* ---------- The pool and its mangroves ---------- */
        var pool = MirePool();
        var roots = MireRoots();
        Check($"the swamp's pool is found ({pool.Count} tiles)", pool.Count >= 20);
        Check($"mangroves stand at its edges, their roots in its water ({roots.Count})", roots.Count >= 2 && roots.All(r => InMirePool(r.wx, r.wy) && !InMirePool(r.bx, r.by)));
        float cx = (float)pool.Average(p => p.Item1) * T + 5, cy = (float)pool.Average(p => p.Item2) * T + 5;
        // Stand on the bank just south of the pool's middle column, facing it.
        int col = (int)(cx / T), bottom = pool.Where(p => p.Item1 == col).Max(p => p.Item2);
        player.X = col * T + 5; player.Y = (bottom + 1) * T + 8; player.Face = "up";
        Check($"there's a bank to stand on ({player.X:0},{player.Y:0})", CanStand(player.X, player.Y));
        yield return 3;

        /* ---------- It takes the worm ---------- */
        TestBite("swamp", MireFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        Check("the leviathan takes the worm: the fight is on", MireOnLine && mire != null);
        Check($"a long fight: it starts far out ({reel.Progress:0.00})", reel.Progress <= 0.39f);
        Check("its title card shows", mire.Banner > 2.5f);
        Check($"the boss music plays ({WantedTrack()})", WantedTrack() == "boss");
        yield return 10;
        pendingShot = "mire-01-banner"; yield return 2;
        mire.NextEv = 99;

        // It stays in its pool, whatever it does.
        int outside = 0, frames = 0;
        for (int i = 0; i < 150; i++)
        {
            if (!InMirePool(mire.X, mire.Y)) outside++;
            frames++;
            reel.Progress = 0.35f;
            yield return 1;
        }
        Check($"it wanders the pool and never leaves it ({outside} of {frames} frames outside)", outside == 0);
        pendingShot = "mire-02-under"; yield return 2;
        // By day too (and a look at the fish in good light).
        SetNight(false); mire.NextEv = 0.01f;
        for (int i = 0; i < 200 && mire.Ev != "gulp"; i++) { reel.Progress = 0.4f; yield return 1; }
        yield return 14;
        pendingShot = "mire-02b-day-gulp"; yield return 2;
        for (int i = 0; i < 200 && mire.Ev != ""; i++) { reel.Progress = MathF.Max(reel.Progress, 0.3f); yield return 1; }
        mire.NextEv = 99; mire.Events = 2; SetNight(true); yield return 3;

        /* ---------- It comes up for air ---------- */
        float hp0 = state.hp;
        reel.Progress = 0.4f;
        mire.NextEv = 0.01f; yield return 3;
        Check($"bubbles first: it's rising ({mire.Ev})", mire.Ev == "rise");
        float p0 = reel.Progress;
        Inp.Hold(KeyboardKey.E, true); yield return 20; Inp.Hold(KeyboardKey.E, false);
        Check($"while it rises the reel waits: holding on costs nothing ({p0:0.00} -> {reel.Progress:0.00})", MathF.Abs(reel.Progress - p0) < 0.001f);
        for (int i = 0; i < 120 && mire.Ev == "rise"; i++) yield return 1;
        Check($"the swamp goes quiet ({MireHush:0.00})", MireHush > 0.5f);
        Check($"then it gulps at the surface ({mire.Ev})", mire.Ev == "gulp");
        yield return 12;
        pendingShot = "mire-03-gulp"; yield return 2;
        p0 = reel.Progress;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a press while it gulps hauls it in ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.07f && mire.Struck);
        for (int i = 0; i < 200 && mire.Ev != ""; i++) { reel.Progress = MathF.Max(reel.Progress, 0.3f); yield return 1; }
        mire.NextEv = 99;
        Check("then it dives again", mire.Ev == "");
        for (int i = 0; i < 200 && MireHush > 0.1f; i++) { reel.Progress = 0.4f; yield return 1; }
        Check($"and the swamp's night sounds come back ({MireHush:0.00})", MireHush <= 0.1f);

        // A breath let go by.
        reel.Progress = 0.4f; p0 = 0.4f;
        mire.NextEv = 0.01f;
        for (int i = 0; i < 300 && mire.Ev != "dive"; i++) yield return 1;
        Check($"miss the gulp and it gets its breath ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0 && reel.Progress > p0 - 0.05f);
        for (int i = 0; i < 100 && mire.Ev != ""; i++) yield return 1;
        mire.NextEv = 99;

        /* ---------- Among the roots ---------- */
        reel.Progress = 0.55f; yield return 3;
        Check($"past the first mark: among the roots ({mire.Phase}, {mire.Title})", mire.Phase == 2 && mire.Title == "AMONG THE ROOTS");
        mire.Banner = 0; mire.Events = 0; mire.NextEv = 0.01f; yield return 3;
        Check($"it bolts for the mangroves ({mire.Ev})", mire.Ev == "roots");
        var rt = MireRoots()[mire.RootI];
        int away = rt.bx < rt.wx ? 1 : 3;
        yield return 4;
        pendingShot = "mire-04-roots"; yield return 2;
        p0 = reel.Progress;
        Inp.Tap(ArrowKey(away == 1 ? 3 : 1)); yield return 3;
        Check($"the wrong way doesn't turn it ({mire.Ev}, {reel.Progress:0.00})", mire.Ev == "roots" && reel.Progress <= p0);
        p0 = reel.Progress;
        Inp.Tap(ArrowKey(away)); yield return 3;
        Check($"pulling it the other way turns it ({mire.Ev}, {p0:0.00} -> {reel.Progress:0.00})", mire.Ev == "" && reel.Progress > p0 + 0.02f);
        mire.NextEv = 99; yield return 3;

        // One let through: into the roots, and the line rubs.
        reel.Progress = 0.6f; p0 = 0.6f;
        mire.Events = 0; mire.NextEv = 0.01f;
        for (int i = 0; i < 300 && mire.Ev != "rub"; i++) yield return 1;
        Check($"too late and it's in the roots: the line rubs ({p0:0.00} -> {reel.Progress:0.00})", mire.Ev == "rub" && reel.Progress < p0 - 0.05f);
        pendingShot = "mire-05-rub"; yield return 2;
        for (int i = 0; i < 200 && mire.Ev != ""; i++) { reel.Progress = MathF.Max(reel.Progress, 0.3f); yield return 1; }
        mire.NextEv = 99;
        Check("but the line holds: it's still on", MireOnLine);

        /* ---------- The moonlit giant ---------- */
        reel.Progress = 0.8f; yield return 3;
        Check($"past the second mark: the moonlit giant ({mire.Phase})", mire.Phase == 3);
        mire.Banner = 0; mire.Events = 0; mire.NextEv = 0.01f; yield return 3;
        Check($"it leaps ({mire.Ev}, {reel.Leap:0.00})", mire.Ev == "leap" && reel.Leap > 0);
        for (int i = 0; i < 200 && reel.LeapMark < 0.42f; i++) yield return 1;
        pendingShot = "mire-06-leap"; yield return 1;
        for (int i = 0; i < 200 && reel.LeapMark < 0.68f; i++) yield return 1;
        p0 = reel.Progress;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a press as the marker crosses the gold pays ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.1f);
        for (int i = 0; i < 200 && mire.Ev != ""; i++) { reel.Progress = MathF.Max(reel.Progress, 0.3f); yield return 1; }
        mire.NextEv = 99;
        Check($"and it lands back in the pool ({mire.X:0},{mire.Y:0})", InMirePool(mire.X, mire.Y));

        /* ---------- Pausing ---------- */
        mire.Events = 2; mire.NextEv = 0.01f; reel.Progress = 0.85f; yield return 30;   // a breath: the swamp has gone quiet
        OpenPause(anyMode: true); yield return 3;
        float frozen = mire.EvT;
        yield return 20;
        Check($"paused, it waits ({mode}, {frozen:0.00} -> {mire?.EvT:0.00})", mode == "pause" && mire != null && mire.EvT == frozen);
        Check($"and it stays drawn and the swamp stays quiet behind the menu ({MireHush:0.00})", MireShown && MireHush > 0.3f && mireCamK > 0.9f);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to the fight", mode == "reeling" && MireOnLine);
        Check($"and it never hurt you ({hp0:0} -> {state.hp:0})", state.hp >= hp0);

        /* ---------- Landing it ---------- */
        for (int i = 0; i < 200 && mire.Ev != "gulp"; i++) { if (mire.Ev == "" ) mire.NextEv = 0.01f; yield return 1; }
        reel.Progress = 0.97f;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"one last haul as it gulps lands it: its legend card ({mode}, {legendId})", mode == "legend" && legendId == MireFish && Has(MireFish) == 1);
        pendingShot = "mire-07-card"; yield return 2;
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
        yield return 3;
        Check("and the fight is over: nothing left of it", mire == null && !MireOnLine && MireHush == 0);


        /* ---------- Codex's review ---------- */
        // A missed event never raises a nearly lost fish (the penalty used to floor it at 0.05).
        TestBite("swamp", MireFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        mire.NextEv = 0.01f; mire.Phase = 1; yield return 2;
        for (int i = 0; i < 300 && mire.Ev != "gulp"; i++) yield return 1;
        reel.Progress = 0.02f;
        for (int i = 0; i < 300 && mire.Ev == "gulp"; i++) yield return 1;
        Check($"ignoring a gulp at 0.02 doesn't save it ({reel?.Progress:0.00})", reel == null || reel.Progress <= 0.02f);
        if (MireOnLine) { reel.Progress = 0.01f; for (int i = 0; i < 200 && MireOnLine; i++) { if (mire != null) mire.NextEv = 99; yield return 1; } }
        yield return 3;
        // Every leap lands in the pool, from anywhere in it (narrow parts too): it never crosses onto the bank.
        TestBite("swamp", MireFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        int badLeaps = 0, leaps = 0;
        foreach (var (tx, ty) in MirePool().ToList())
        {
            if (!MireOnLine) break;
            mire.X = tx * T + 5; mire.Y = ty * T + 5; mire.Ev = "";
            BeginMireEvent("leap");
            leaps++;
            for (int s = 0; s <= 10; s++)
            {
                var (lx, _) = MireLeapPos(s / 10f);
                if (!InMirePool(lx, mire.Y)) { badLeaps++; break; }
            }
            reel.Leap = 0; EndMireEvent(); mire.NextEv = 99; reel.Progress = 0.5f;
        }
        Check($"leaps from every pool tile come down in the pool ({badLeaps} of {leaps} crossed the bank)", badLeaps == 0 && leaps > 0);
        if (MireOnLine) GotAway("test");
        yield return 3;
        // Codex's second review: a mistimed leap press costs no more than letting it go by.
        TestBite("swamp", MireFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        mire.X = cx; mire.Y = cy; BeginMireEvent("leap"); reel.Progress = 0.07f;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a press at the wrong moment of a leap at 0.07 keeps it at 0.05 or more ({reel?.Progress:0.00})", reel != null && reel.Progress >= 0.05f);
        for (int i = 0; i < 200 && mire?.Ev == "leap"; i++) { reel.Progress = MathF.Max(reel.Progress, 0.3f); yield return 1; }
        // ...and the way to pull is fixed by which bank the roots are on, even with the fish right at them.
        var westI = Enumerable.Range(0, MireRoots().Count).First(i => MireRoots()[i].bx < MireRoots()[i].wx);
        var west = MireRoots()[westI];
        mire.X = west.wx - 0.5f; mire.Y = west.wy; mire.Ev = ""; BeginMireEvent("roots"); mire.RootI = westI; reel.Progress = 0.6f;
        Inp.Tap(KeyboardKey.Right); yield return 3;
        Check($"right at the western roots, pulling right (east) still turns it ({mire?.Ev})", mire?.Ev == "");
        if (MireOnLine) GotAway("test");
        yield return 3;
        /* ---------- A rematch it wins, and a faint ---------- */
        TestBite("swamp", MireFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        Check("it can be hooked again", MireOnLine);
        mire.NextEv = 99; reel.Progress = 0.02f;
        for (int i = 0; i < 300 && MireOnLine; i++) { if (mire != null) mire.NextEv = 99; yield return 1; }
        yield return 2;
        Check($"let it run and it gets away ({mode}), and the fight is cleared", mode == "play" && mire == null);
        TestBite("swamp", MireFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        mire.NextEv = 0.01f; yield return 3;
        Faint(); yield return 4;
        Check($"the swamp sounds come straight back as you faint ({MireHush:0.00})", MireHush == 0);
        for (int i = 0; i < 300 && mode != "play"; i++) yield return 1;
        yield return 3;
        Check($"fainting mid-fight ends it cleanly ({mode})", !MireOnLine && mire == null && MireHush == 0);
    }

    // FESH_SPRITES -leviathan, at 4x: the Blender arapaima (top: swimming level, the gulp with its mouth open, a leap's
    // rise, top and dive, and facing west) beside the Fish log's hand-drawn picture (bottom, level only), on murky water.
    void ExportMireSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(420, 110);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#3b4a37"));
        var poses = new (float up, bool open, int dir)[] { (0, false, 1), (35, true, 1), (45, false, 1), (0, false, 1), (-45, false, 1), (0, false, -1) };
        for (int i = 0; i < poses.Length; i++) DrawMireFish(36 + i * 68, 38 + 99, poses[i].dir, poses[i].up, i, poses[i].open, -99, dropUnder: true);
        FishArt.Draw(sheet, MireFish, 16, 80, 40, 12);
        FishArt.Draw(sheet, MireFish, 84, 80, 40, 12, true);
        pix = keep;
        var img = Raylib.GenImageColor(420, 110, Color.Black);
        for (int yy = 0; yy < 110; yy++)
            for (int xx = 0; xx < 420; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * 420 + xx]);
        Raylib.ImageResizeNN(ref img, 420 * 4, 110 * 4);
        Raylib.ExportImage(img, path.Replace(".png", "-leviathan.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
