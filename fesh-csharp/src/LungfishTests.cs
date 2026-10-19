#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_LUNGFISH_TEST=1: the Sunscale lungfish's boss fight (LungfishBoss.cs): when it bites (by day, clear, a cricket, and
// again after the first), the oasis's pool and the places the fish can be, the title card, bar and music, staying in the
// pool, no event during a run, each of the three events with real keys (the ripples: a warning press, an extra press, a
// missed beat and three on the beat; the march: the wrong way, both at once, holding, and six in step; the pumps: held
// all through, never held, and three slow pumps), the 0.05 floor, pausing by the real Resume, the cracked mud, landing it
// (kept in the bag), losing it and fainting, with screenshots.
partial class Game
{
    // Keeps the fish from running (an ordinary run would hold an event back) and, with p >= 0, the reel at p; -1 only keeps it from falling too low.
    void LungCalm(float p)
    {
        if (reel == null) return;
        reel.RunT = 99;
        if (p >= 0) reel.Progress = p; else if (p > -2) reel.Progress = Math.Clamp(reel.Progress, 0.3f, lung?.Phase switch { 1 => 0.45f, 2 => 0.7f, _ => 0.99f });
    }

    // Waits (a frame at a time) while a condition holds, for at most `secs` of game time, so a fast or slow frame rate runs the same script.
    IEnumerable<int> LungUntil(Func<bool> waiting, float secs, Action each = null)
    {
        float t0 = time;
        while (time - t0 < secs && waiting()) { each?.Invoke(); yield return 1; }
    }

    IEnumerable<int> LungfishScript()
    {
        Note("The Sunscale lungfish: the boss fight");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hp = 100; state.inv["cricket"] = 20;

        /* ---------- When it bites ---------- */
        bool Bitable() => FishWeights("oasis", "cricket", 2).Any(p => p.f.Id == LungfishFish);
        Check("by day, clear, on a cricket it can bite in the oasis", Bitable());
        SetNight(true);
        Check("never at night", !Bitable());
        SetNight(false);
        Check("never on another bait", FishWeights("oasis", "worm", 2).All(p => p.f.Id != LungfishFish));
        state.weather = "rain"; state.forecast = null;
        Check("never in the rain", !Bitable());
        ClearSkies();
        state.commons[LungfishFish] = 1;
        Check("and after you've caught it once, it comes back for a rematch", Bitable());
        state.commons.Remove(LungfishFish);

        /* ---------- The pool ---------- */
        var pool = LungPool();
        var spots = LungSpots();
        Check($"the oasis's pool is found ({pool.Count} tiles)", pool.Count >= 12);
        Check($"it has places the whole fish fits ({spots.Count})", spots.Count >= 8 && spots.All(s => LungFits(s.x, s.y)));
        Check("and the bank is not one of them", !LungFits(pool.Min(p => p.Item1) * T - 8, pool.First().Item2 * T + 5));
        float cx = (float)pool.Average(p => p.Item1) * T + 5, cy = (float)pool.Average(p => p.Item2) * T + 5;
        int col = (int)(cx / T), bottom = pool.Where(p => p.Item1 == col).Max(p => p.Item2);
        player.X = col * T + 5; player.Y = (bottom + 1) * T + 8; player.Face = "up";
        Check($"there's a bank to stand on ({player.X:0},{player.Y:0})", CanStand(player.X, player.Y));
        yield return 3;

        /* ---------- It takes the cricket ---------- */
        TestBite("oasis", LungfishFish); fish.Bx = fish.Tx = cx + 38; fish.By = fish.Ty = cy;   // a cast that came down at the pool's edge
        Hook(); yield return 2;
        Check("the lungfish takes the cricket: the fight is on", LungfishOnLine && lung != null);
        Check($"a long fight: it starts far out ({reel.Progress:0.00})", reel.Progress <= 0.39f);
        Check($"it was put where the whole fish fits ({lung.X:0},{lung.Y:0})", LungFits(lung.X, lung.Y));
        Check("its title card shows", lung.Banner > 2.5f && lung.Title == "SUNSCALE LUNGFISH");
        Check($"the boss music plays ({WantedTrack()})", WantedTrack() == "boss");
        float hp0 = state.hp;
        yield return 12;
        pendingShot = "lung-01-banner"; yield return 2;
        lung.NextEv = 99;

        // It stays in the pool, whatever it does.
        int outside = 0, frames = 0;
        foreach (var _ in LungUntil(() => true, 4f, () => { if (!LungFits(lung.X, lung.Y)) outside++; frames++; LungCalm(0.35f); })) yield return 1;
        Check($"it wanders the pool and the whole fish never leaves it ({outside} of {frames} frames outside)", outside == 0);
        pendingShot = "lung-02-swim"; yield return 2;
        Check($"the camera frames the pool ({lungCamK:0.00})", lungCamK > 0.9f);
        Check($"and the cracked mud creeps out round it ({lungDry:0.00}, {LungMud().Count} pixels)", lungDry > 0.4f && LungMud().Count > 400);

        // Never in the middle of one of its runs.
        reel.Progress = 0.4f; reel.Running = 1.2f; lung.Banner = 0; lung.NextEv = 0.01f; yield return 4;
        Check($"no event while it runs ({lung.Ev})", lung.Ev == "");
        reel.Running = 0; yield return 3;
        Check($"as soon as it settles it comes up for air ({lung.Ev})", lung.Ev == "breathWarn");

        /* ---------- Phase 1: three ripples ---------- */
        Check("only breathing in the first phase", lung.Phase == 1);
        float p0 = reel.Progress;
        // A press during the warning is only a warning.
        lungLast = "";
        Inp.Hold(KeyboardKey.E, true); yield return 4; Inp.Hold(KeyboardKey.E, false);
        Check($"a press in the warning isn't a ripple ({lung.Ev}, {lung.Beats})", lung.Ev == "breathWarn" && lung.Beats == 0);
        pendingShot = "lung-03-warn"; yield return 2;
        foreach (var _ in LungUntil(() => lung.Ev != "breath", 8f)) yield return 1;
        Check($"then the ripples begin ({lung.Ev})", lung.Ev == "breath");
        // A press at once, off the beat, ends it.
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"an early press is off the beat: it costs a little ({lungDelta:0.00}), the event is over ({lung.Ev})", lung.Ev == "" && lungLast == "miss" && lungDelta < -0.039f && lungDelta >= -0.041f);
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Three on the beat.
        lungLast = "";
        var tapAt = new List<string>();
        reel.Progress = 0.4f; p0 = 0.4f; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.4f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "breath", 8f, () => { LungCalm(0.4f); })) yield return 1;
        pendingShot = "lung-04-ripples"; yield return 1;
        for (int b = 0; b < 3; b++)
        {
            foreach (var _ in LungUntil(() => LungBreathLen - lung.EvT < LungBeats[b] - 0.02f && lung.Ev == "breath", 8f)) yield return 1;
            if (lung.Ev != "breath") break;
            tapAt.Add((LungBreathLen - lung.EvT).ToString("0.00")); Inp.Tap(KeyboardKey.E); yield return 1;
        }
        foreach (var _ in LungUntil(() => lung.Ev == "breath", 8f)) yield return 1;
        Check($"three taps on the beat pay ({lungLast}, {lungDelta:0.00}, beats {lung.Beats}, taps at {string.Join(",", tapAt)}, phase {lung.Phase})", lungLast == "ok" && lungDelta > 0.059f && lung.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Never tapped: a missed ripple costs a little.
        lungLast = "";
        reel.Progress = 0.4f; p0 = 0.4f; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.4f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "", 8f, () => { if (lung.Ev == "breathWarn") reel.Progress = 0.4f; })) yield return 1;
        Check($"letting the ripples go by costs a little ({lungLast}, {lungDelta:0.00})", lungLast == "miss" && lungDelta < -0.039f && lungDelta >= -0.041f);
        Check($"and it never hurt you ({hp0:0} -> {state.hp:0})", state.hp >= hp0);

        // An extra fourth press fails.
        lungLast = "";
        reel.Progress = 0.4f; p0 = 0.4f; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.4f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "breath", 8f)) yield return 1;
        for (int b = 0; b < 3; b++)
        {
            foreach (var _ in LungUntil(() => LungBreathLen - lung.EvT < LungBeats[b] - 0.02f && lung.Ev == "breath", 8f)) yield return 1;
            Inp.Tap(KeyboardKey.E); yield return 1;
        }
        Inp.Tap(KeyboardKey.E); yield return 2;   // a fourth, still inside the last beat's window
        foreach (var _ in LungUntil(() => lung.Ev != "", 8f)) yield return 1;
        Check($"a fourth press fails it ({lungLast}, {lungDelta:0.00})", lungLast == "miss");
        lung.NextEv = 99;

        /* ---------- Phase 2: the march ---------- */
        reel.Progress = 0.55f; reel.RunT = 99; yield return 3;
        Check($"past the first mark: footsteps under water ({lung.Phase}, {lung.Title})", lung.Phase == 2 && lung.Title == "FOOTSTEPS UNDER WATER");
        lung.Banner = 0; lung.NextEv = 0.01f; reel.RunT = 99;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => { LungCalm(0.55f); })) yield return 1;
        Check($"the new event comes first: it walks the bottom ({lung.Ev})", lung.Ev == "marchWarn");
        foreach (var _ in LungUntil(() => lung.Ev != "march", 8f, () => { LungCalm(0.55f); })) yield return 1;
        pendingShot = "lung-05-march"; yield return 1;
        lungLast = "";
        p0 = reel.Progress;
        Inp.Tap(KeyboardKey.Right); yield return 3;
        Check($"the wrong way first costs a little ({lungLast}, {lungDelta:0.00}, {lung.Ev})", lung.Ev == "" && lungLast == "miss" && lungDelta < -0.059f);
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Both at once.
        reel.Progress = 0.55f; p0 = 0.55f; lung.Events = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.55f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "march", 8f, () => { LungCalm(0.55f); })) yield return 1;
        lungLast = "";
        Inp.Hold(KeyboardKey.Left, true); Inp.Hold(KeyboardKey.Right, true); yield return 3; Inp.Hold(KeyboardKey.Left, false); Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"both directions at once fails ({lungLast}, {lungDelta:0.00})", lung.Ev == "" && lungLast == "miss");
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Holding one doesn't repeat.
        reel.Progress = 0.55f; p0 = 0.55f; lung.Events = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.55f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "march", 8f, () => { LungCalm(0.55f); })) yield return 1;
        lungLast = "";
        Inp.Hold(KeyboardKey.Left, true);
        foreach (var _ in LungUntil(() => lung.Ev == "march", 8f)) yield return 1;
        Inp.Hold(KeyboardKey.Left, false);
        Check($"holding left is one step, not six: it slips away ({lungLast}, {lungDelta:0.00})", lungLast == "miss" && lung.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Six in step.
        reel.Progress = 0.55f; p0 = 0.55f; lung.Events = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.55f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "march", 8f, () => { LungCalm(0.55f); })) yield return 1;
        float fx0 = lung.X;
        lungLast = "";
        for (int s = 0; s < 6; s++)
        {
            // In game time: at least a tenth of a second after the last step, whatever the frame rate.
            foreach (var _ in LungUntil(() => lung.Ev == "march" && (LungMarchLen - lung.EvT) - lung.LastStep < 0.1f, 8f)) yield return 1;
            if (lung.Ev != "march") break;
            Inp.Tap(ArrowKey(LungMarch[s])); yield return 1;
        }
        yield return 3;
        Check($"six steps, left, right, left, right, left, right, pay ({lungLast}, {lungDelta:0.00})", lungLast == "ok" && lungDelta > 0.079f && lung.Ev == "");
        Check($"and every step left a mark on the bottom ({lung.Prints.Count}), the fish moved along it ({fx0:0} -> {lung.X:0}) and still fits", lung.Prints.Count >= 5 && lung.X != fx0 && LungFits(lung.X, lung.Y));
        lung.NextEv = 99;

        /* ---------- Phase 3: the mud hollow ---------- */
        reel.Progress = 0.8f; reel.RunT = 99; yield return 3;
        Check($"past the second mark: the patient survivor ({lung.Phase}, {lung.Title})", lung.Phase == 3 && lung.Title == "THE PATIENT SURVIVOR");
        lung.Banner = 0; lung.NextEv = 0.01f; reel.RunT = 99;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => { LungCalm(0.8f); })) yield return 1;
        Check($"the new event comes first: the mud hollow ({lung.Ev})", lung.Ev == "cradleWarn");
        foreach (var _ in LungUntil(() => lung.Ev != "cradle", 8f, () => { LungCalm(0.8f); })) yield return 1;
        pendingShot = "lung-06-cradle"; yield return 1;

        // Held all the way: it never rests.
        lungLast = "";
        Inp.Hold(KeyboardKey.E, true);
        foreach (var _ in LungUntil(() => lung.Ev == "cradle", 8f)) yield return 1;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"holding on through the rests fails ({lungLast}, {lungDelta:0.00})", lungLast == "miss" && lungDelta < -0.059f && lung.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Never held: it never pulls.
        lungLast = "";
        reel.Progress = 0.8f; p0 = 0.8f; lung.Events = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.8f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "", 8f, () => { if (lung.Ev == "cradleWarn") reel.Progress = 0.8f; })) yield return 1;
        Check($"never pulling fails ({lungLast}, {lungDelta:0.00})", lungLast == "miss" && lungDelta < -0.059f);
        foreach (var _ in LungUntil(() => true, 0.8f, () => LungCalm(-1))) yield return 1;

        // Three slow pumps.
        lungLast = "";
        reel.Progress = 0.8f; p0 = 0.8f; lung.Events = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.8f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "cradle", 8f, () => { LungCalm(0.8f); })) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev == "cradle", 6f, () =>
        {
            float a = LungCradleLen - lung.EvT;
            Inp.Hold(KeyboardKey.E, a - (int)MathF.Min(a, 2.999f) < 0.58f);
        })) yield return 1;
        Inp.Hold(KeyboardKey.E, false); yield return 2;
        Check($"pull, rest, pull, rest, pull, rest pays ({lungLast}, {lungDelta:0.00}, pulls {string.Join("/", lung.Pull.Select(v => v.ToString("0.00")))}, rests {string.Join("/", lung.Rest.Select(v => v.ToString("0.00")))})", lungLast == "ok" && lungDelta > 0.099f);
        lung.NextEv = 99;

        // The floor: a missed event never raises a nearly lost fish.
        reel.Progress = 0.02f; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => { LungCalm(-2); reel.Progress = 0.02f; })) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "", 8f, () => { if (lung.Ev.EndsWith("Warn")) reel.Progress = 0.02f; })) yield return 1;
        Check($"a missed event at 0.02 doesn't save it ({reel?.Progress:0.00})", reel == null || reel.Progress <= 0.0201f);
        if (!LungfishOnLine) { TestBite("oasis", LungfishFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy; Hook(); yield return 2; }
        lung.Banner = 0; lung.NextEv = 99; reel.Progress = 0.5f;

        /* ---------- Pausing ---------- */
        lung.Phase = 3; lung.Fresh = true; lung.NextEv = 0.01f; reel.Progress = 0.85f; yield return 3;
        foreach (var _ in LungUntil(() => lung.Ev != "cradle", 8f, () => { LungCalm(0.85f); })) yield return 1;
        OpenPause(anyMode: true); yield return 3;
        float frozen = lung.EvT;
        yield return 20;
        Check($"paused, it waits ({mode}, {frozen:0.00} -> {lung?.EvT:0.00})", mode == "pause" && lung != null && lung.EvT == frozen);
        Check($"and it stays drawn and framed behind the menu ({lungCamK:0.00})", LungfishShown && lungCamK > 0.9f);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to the fight", mode == "reeling" && LungfishOnLine);
        foreach (var _ in LungUntil(() => lung.Ev != "", 8f, () => { LungCalm(-1); })) yield return 1;
        lung.NextEv = 99;

        /* ---------- Codex's review ---------- */
        string Seq(int phase, int n) => string.Join(",", Enumerable.Range(1, n).Select(e => LungPick(phase, e, e == 1)));
        Check($"phase 2 walks first, then breathes and walks in turn ({Seq(2, 4)})", Seq(2, 4) == "march,breath,march,breath");
        Check($"phase 3 pumps first, then walks, breathes, pumps ({Seq(3, 4)})", Seq(3, 4) == "cradle,march,breath,cradle");
        Check($"phase 1 only breathes ({Seq(1, 3)})", Seq(1, 3) == "breath,breath,breath");
        // A frame that straddles the end of a pull is split between pulling and resting, not given to one side.
        lung.Ev = "cradle"; lung.EvT = LungCradleLen; Array.Clear(lung.Pull); Array.Clear(lung.Rest); Array.Clear(lung.CycleOk);
        for (int k = 0; k < 3; k++) UpdateLungfishReel(reel, 0.22f, true, false);
        Check($"held for 0.66 s in 0.22 s frames: 0.60 pulling and 0.06 resting ({lung.Pull[0]:0.00}, {lung.Rest[0]:0.00})", MathF.Abs(lung.Pull[0] - 0.6f) < 0.001f && MathF.Abs(lung.Rest[0] - 0.06f) < 0.001f);
        for (int k = 0; k < 2; k++) UpdateLungfishReel(reel, 0.22f, false, false);
        Check("and a good first cycle shows as done before the event ends", lung.Ev == "cradle" && lung.CycleOk[0]);
        EndLungEvent(); lung.NextEv = 99;

        /* ---------- Landing it ---------- */
        reel.Progress = 0.97f; lung.Phase = 3; lung.Fresh = false; lung.Events = 2; lung.Banner = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(0.97f))) yield return 1;
        foreach (var _ in LungUntil(() => lung.Ev != "breath", 8f, () => { LungCalm(0.97f); })) yield return 1;
        for (int b = 0; b < 3; b++)
        {
            foreach (var _ in LungUntil(() => LungBreathLen - lung.EvT < LungBeats[b] - 0.02f && lung.Ev == "breath", 8f)) yield return 1;
            Inp.Tap(KeyboardKey.E); yield return 1;
        }
        foreach (var _ in LungUntil(() => mode == "reeling", 8f)) yield return 1;
        yield return 3;
        Check($"the third ripple lands it: its legend card ({mode}, {legendId})", mode == "legend" && legendId == LungfishFish);
        Check("it is yours to keep: in the bag, not let go", Has(LungfishFish) == 1);
        pendingShot = "lung-07-card"; yield return 2;
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
        yield return 3;
        Check("and the fight is over: nothing left of it", lung == null && !LungfishOnLine);
        foreach (var _ in LungUntil(() => lungDry > 0.02f, 8f)) yield return 1;
        Check($"and the mud dries up no more ({lungDry:0.00})", lungDry <= 0.02f);
        Check("the first catch can be fought again", state.commons.GetValueOrDefault(LungfishFish) >= 1 && Bitable());

        /* ---------- A rematch it wins, and a faint ---------- */
        state.inv["cricket"] = 5;
        TestBite("oasis", LungfishFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        Check("it can be hooked again", LungfishOnLine);
        lung.NextEv = 99; reel.Progress = 0.02f;
        foreach (var _ in LungUntil(() => LungfishOnLine, 8f, () => { if (lung != null) lung.NextEv = 99; })) yield return 1;
        yield return 2;
        Check($"let it run and it gets away ({mode}), and the fight is cleared", mode == "play" && lung == null);
        TestBite("oasis", LungfishFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        lung.Banner = 0; lung.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => lung.Ev == "", 8f, () => LungCalm(-1))) yield return 1;
        Faint(); yield return 4;
        foreach (var _ in LungUntil(() => mode != "play", 8f)) yield return 1;
        yield return 3;
        Check($"fainting mid-fight ends it cleanly ({mode})", !LungfishOnLine && lung == null);
    }

    // FESH_SPRITES -lungfish, at 4x: the Blender lungfish (swimming through its wave, the breath with its mouth open, a
    // rise and dive, and facing west) beside the Fish log's hand-drawn picture, on clear oasis water.
    void ExportLungfishSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(420, 110);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#2a9d9a"));
        var poses = new (float up, bool open, int dir)[] { (0, false, 1), (0, false, 1), (0, false, 1), (32, true, 1), (-40, false, 1), (0, false, -1) };
        for (int i = 0; i < poses.Length; i++) DrawLungFish(36 + i * 68, 38 + 99, poses[i].dir, poses[i].up, i, poses[i].open, -99, dropUnder: true);
        FishArt.Draw(sheet, LungfishFish, 16, 80, 40, 10);
        FishArt.Draw(sheet, LungfishFish, 84, 80, 40, 10, true);
        pix = keep;
        var img = Raylib.GenImageColor(420, 110, Color.Black);
        for (int yy = 0; yy < 110; yy++)
            for (int xx = 0; xx < 420; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * 420 + xx]);
        Raylib.ImageResizeNN(ref img, 420 * 4, 110 * 4);
        Raylib.ExportImage(img, path.Replace(".png", "-lungfish.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
