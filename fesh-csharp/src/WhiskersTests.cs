#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_WHISKERS_TEST=1: Old Whiskers' boss fight (WhiskersBoss.cs): when it bites (rain, a berry, and again after the first),
// the lagoon's pool and the places the whole fish fits, the title card, bar and music, staying in the pool, each of the three
// tricks with real keys (the bobber: a press in the warning, too soon, in the window, too late; the whiskers: the preview,
// the wrong arrow, two at once, a held key, the right three; the quiet: holding on through the start, a touch, never letting
// go, and keeping still), the rotation, the 0.05 floor, pausing by the real Resume, landing it (kept in the bag), losing it
// and fainting, with screenshots.
partial class Game
{
    void WhiskCalm(float p)
    {
        if (reel == null) return;
        reel.RunT = 99;
        if (p >= 0) reel.Progress = p;
        else if (p > -2) reel.Progress = Math.Clamp(reel.Progress, 0.3f, whisk?.Phase switch { 1 => 0.45f, 2 => 0.7f, _ => 0.99f });
    }

    IEnumerable<int> WhiskersScript()
    {
        Note("Old Whiskers: the boss fight");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hp = 100; state.inv["berries"] = 20;

        /* ---------- When it bites ---------- */
        bool Bitable() => FishWeights("lagoon", "berries", 1).Any(p => p.f.Id == WhiskersFish);
        state.weather = "rain";
        Check("in the rain, on a berry it can bite in the lagoon", Bitable());
        Check("never on another bait", FishWeights("lagoon", "worm", 1).All(p => p.f.Id != WhiskersFish));
        state.weather = "clear";
        Check("never when it's dry", !Bitable());
        state.weather = "rain";
        state.commons[WhiskersFish] = 1;
        Check("and after you've caught it once, it comes back for a rematch", Bitable());
        state.commons.Remove(WhiskersFish);

        /* ---------- The lagoon ---------- */
        var pool = WhiskPool();
        var spots = WhiskSpots();
        Check($"the lagoon's pool is found ({pool.Count} tiles)", pool.Count >= 6);
        Check($"it has places the whole fish fits ({spots.Count}, half-length {whiskHalf})", spots.Count >= 4 && whiskHalf >= 9 && spots.All(s => WhiskFits(s.x, s.y)));
        float cx = (float)pool.Average(p => p.Item1) * T + 5, cy = (float)pool.Average(p => p.Item2) * T + 5;
        int col = (int)(cx / T), bottom = pool.Where(p => p.Item1 == col).Max(p => p.Item2);
        player.X = col * T + 5; player.Y = (bottom + 1) * T + 8; player.Face = "up";
        Check($"there's a bank to stand on ({player.X:0},{player.Y:0})", CanStand(player.X, player.Y));
        SnapWeather();
        yield return 3;

        /* ---------- It takes the berry ---------- */
        TestBite("lagoon", WhiskersFish); fish.Bx = fish.Tx = cx + 30; fish.By = fish.Ty = cy;   // a cast that came down near the edge
        Hook(); yield return 2;
        Check("Old Whiskers takes the berry: the fight is on", WhiskersOnLine && whisk != null);
        Check($"a long fight: it starts far out ({reel.Progress:0.00})", reel.Progress <= 0.39f);
        Check($"it was put where the whole fish fits ({whisk.X:0},{whisk.Y:0})", WhiskFits(whisk.X, whisk.Y));
        Check("its title card shows", whisk.Banner > 2.5f && whisk.Title == "OLD WHISKERS");
        Check($"the boss music plays ({WantedTrack()})", WantedTrack() == "boss");
        float hp0 = state.hp;
        yield return 12;
        pendingShot = "whisk-01-banner"; yield return 2;
        whisk.NextEv = 99;

        int outside = 0, frames = 0;
        foreach (var _ in LungUntil(() => true, 4f, () => { if (!WhiskFits(whisk.X, whisk.Y)) outside++; frames++; WhiskCalm(0.35f); })) yield return 1;
        Check($"it wanders the lagoon and the whole fish never leaves it ({outside} of {frames} frames outside)", outside == 0);
        pendingShot = "whisk-02-swim"; yield return 2;
        Check($"the camera frames the lagoon ({whiskCamK:0.00})", whiskCamK > 0.9f);

        /* ---------- Phase 1: the lunch thief ---------- */
        float p0;
        Check("only the thief in the first phase", whisk.Phase == 1);
        whiskLast = "";
        reel.Progress = 0.4f; whisk.Banner = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.4f))) yield return 1;
        Check($"he starts nibbling ({whisk.Ev})", whisk.Ev == "thiefWarn");
        whisk.Delay = 1.5f;
        // A press during the warning is only a warning.
        Inp.Hold(KeyboardKey.E, true); yield return 4; Inp.Hold(KeyboardKey.E, false);
        Check($"a press in the warning counts for nothing ({whisk.Ev}, {whiskLast})", whisk.Ev == "thiefWarn" && whiskLast == "");
        pendingShot = "whisk-03-nibble"; yield return 2;
        foreach (var _ in LungUntil(() => whisk.Ev != "thief", 8f, () => WhiskCalm(0.4f))) yield return 1;
        pendingShot = "whisk-04-wait"; yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a press before the bobber goes under is too soon ({whiskLast}, {whiskDelta:0.00}, {whisk.Ev})", whiskLast == "miss" && whiskDelta < -0.039f && whiskDelta >= -0.041f && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(0.4f))) yield return 1;

        // In the window.
        whiskLast = ""; reel.Progress = 0.4f; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.4f))) yield return 1;
        whisk.Delay = 0.7f;
        foreach (var _ in LungUntil(() => whisk.Ev != "thief" || (whisk.Delay + WhiskWindow - whisk.EvT) < whisk.Delay + 0.08f, 8f, () => WhiskCalm(0.4f))) yield return 1;
        pendingShot = "whisk-05-now"; yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        Check($"a press once it's under pays ({whiskLast}, {whiskDelta:0.00})", whiskLast == "ok" && whiskDelta > 0.059f && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;

        // Too late.
        whiskLast = ""; reel.Progress = 0.4f; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.4f))) yield return 1;
        whisk.Delay = 0.7f;
        foreach (var _ in LungUntil(() => whisk.Ev != "", 8f, () => { if (whisk.Ev == "thiefWarn") WhiskCalm(0.4f); })) yield return 1;
        Check($"letting the window go by costs a little ({whiskLast}, {whiskDelta:0.00})", whiskLast == "miss" && whiskDelta < -0.039f && whiskDelta >= -0.041f);
        Check($"and it never hurt you ({hp0:0} -> {state.hp:0})", state.hp >= hp0);
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;
        whisk.NextEv = 99;

        /* ---------- Phase 2: the whiskers ---------- */
        reel.Progress = 0.55f; reel.RunT = 99; yield return 3;
        Check($"past the first mark: the captain's whiskers ({whisk.Phase}, {whisk.Title})", whisk.Phase == 2 && whisk.Title == "THE CAPTAIN'S WHISKERS");
        whiskLast = "";
        whisk.Banner = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.55f))) yield return 1;
        Check($"the new trick comes first: the whiskers ({whisk.Ev})", whisk.Ev == "memoryWarn");
        whisk.Seq = new[] { 3, 0, 1 };
        var seen = new HashSet<int>();
        bool shot = false;
        foreach (var _ in LungUntil(() => whisk.Ev == "memoryWarn", 8f, () =>
        {
            WhiskCalm(0.55f);
            int fl = WhiskFlickNow(whisk);
            if (fl >= 0) seen.Add(fl);
            if (fl == 1 && !shot) { shot = true; pendingShot = "whisk-06-flick"; }
        })) yield return 1;
        Check($"the preview flicks all three, one at a time ({string.Join(",", seen.OrderBy(v => v))})", seen.SetEquals(new[] { 0, 1, 2 }));
        Check($"then it's your turn ({whisk.Ev})", whisk.Ev == "memory");
        pendingShot = "whisk-07-repeat"; yield return 2;
        // The wrong arrow first.
        Inp.Tap(KeyboardKey.Right); yield return 3;
        Check($"the wrong arrow fails ({whiskLast}, {whiskDelta:0.00}, {whisk.Ev})", whiskLast == "miss" && whiskDelta < -0.049f && whiskDelta >= -0.051f && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;

        // Two at once.
        whiskLast = ""; reel.Progress = 0.55f; whisk.Events = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.55f))) yield return 1;
        whisk.Seq = new[] { 3, 0, 1 };
        foreach (var _ in LungUntil(() => whisk.Ev != "memory", 8f, () => WhiskCalm(0.55f))) yield return 1;
        Inp.Hold(KeyboardKey.Right, true); Inp.Hold(KeyboardKey.Down, true); yield return 3; Inp.Hold(KeyboardKey.Right, false); Inp.Hold(KeyboardKey.Down, false); yield return 2;
        Check($"two arrows at once fail ({whiskLast}, {whiskDelta:0.00})", whiskLast == "miss" && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;

        // A held key is one press.
        whiskLast = ""; reel.Progress = 0.55f; whisk.Events = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.55f))) yield return 1;
        whisk.Seq = new[] { 3, 0, 1 };
        foreach (var _ in LungUntil(() => whisk.Ev != "memory", 8f, () => WhiskCalm(0.55f))) yield return 1;
        Inp.Hold(KeyboardKey.Left, true);
        foreach (var _ in LungUntil(() => whisk.Ev == "memory", 6f)) yield return 1;
        Inp.Hold(KeyboardKey.Left, false);
        Check($"holding left is one arrow, not three: the time runs out ({whiskLast}, {whiskDelta:0.00})", whiskLast == "miss" && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;

        // The right three.
        whiskLast = ""; reel.Progress = 0.55f; whisk.Events = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.55f))) yield return 1;
        whisk.Seq = new[] { 3, 0, 1 };
        foreach (var _ in LungUntil(() => whisk.Ev != "memory", 8f, () => WhiskCalm(0.55f))) yield return 1;
        foreach (int d in whisk.Seq.ToArray()) { Inp.Tap(ArrowKey(d)); yield return 3; }
        yield return 3;
        Check($"left, up, right in order pays ({whiskLast}, {whiskDelta:0.00})", whiskLast == "ok" && whiskDelta > 0.079f && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;
        whisk.NextEv = 99;

        // Sequences are always three different neighbours, both axes.
        bool seqOk = true;
        for (int i = 0; i < 300; i++)
        {
            var s = WhiskSequence();
            if (s[0] == s[1] || s[1] == s[2] || s.All(d => d % 2 == 0) || s.All(d => d % 2 == 1)) seqOk = false;
        }
        Check("every sequence has no repeat in a row and uses both axes", seqOk);

        /* ---------- Phase 3: quiet in the rain ---------- */
        reel.Progress = 0.8f; reel.RunT = 99; yield return 3;
        Check($"past the second mark: quiet in the rain ({whisk.Phase}, {whisk.Title})", whisk.Phase == 3 && whisk.Title == "QUIET IN THE RAIN");
        whiskLast = "";
        whisk.Banner = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.8f))) yield return 1;
        Check($"the new trick comes first: keeping quiet ({whisk.Ev})", whisk.Ev == "quietWarn");
        // Holding on through the warning and into the start: it waits for you to let go, then counts.
        Inp.Hold(KeyboardKey.E, true);
        foreach (var _ in LungUntil(() => whisk.Ev != "quiet", 8f, () => WhiskCalm(0.8f))) yield return 1;
        Check($"it waits for you to let go ({whisk.Ev}, armed {whisk.Armed})", whisk.Ev == "quiet" && !whisk.Armed);
        pendingShot = "whisk-08-quiet-wait"; yield return 1;
        foreach (var _ in LungUntil(() => true, 0.3f)) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        foreach (var _ in LungUntil(() => !whisk.Armed, 3f)) yield return 1;
        Check($"once you've let go it's counting ({whisk.Armed})", whisk.Armed);
        pendingShot = "whisk-09-quiet"; yield return 1;
        foreach (var _ in LungUntil(() => whisk.Ev == "quiet", 6f)) yield return 1;
        Check($"two quiet seconds pay, even after holding on at the start ({whiskLast}, {whiskDelta:0.00})", whiskLast == "ok" && whiskDelta > 0.099f && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;

        // A touch once it's counting.
        whiskLast = ""; reel.Progress = 0.8f; whisk.Events = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.8f))) yield return 1;
        foreach (var _ in LungUntil(() => !whisk.Armed, 8f, () => WhiskCalm(0.8f))) yield return 1;
        foreach (var _ in LungUntil(() => true, 0.5f)) yield return 1;
        Inp.Tap(KeyboardKey.Left); yield return 3;
        Check($"a touch gives you away ({whiskLast}, {whiskDelta:0.00})", whiskLast == "miss" && whiskDelta < -0.059f && whisk.Ev == "");
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;

        // Never letting go.
        whiskLast = ""; reel.Progress = 0.8f; whisk.Events = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.8f))) yield return 1;
        Inp.Hold(KeyboardKey.E, true);
        foreach (var _ in LungUntil(() => whisk.Ev != "", 8f)) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        Check($"never letting go gives you away too ({whiskLast}, {whiskDelta:0.00})", whiskLast == "miss" && whiskDelta < -0.059f);
        foreach (var _ in LungUntil(() => true, 0.8f, () => WhiskCalm(-1))) yield return 1;
        whisk.NextEv = 99;

        // The order the tricks come in.
        string Seq(int phase, int n) => string.Join(",", Enumerable.Range(1, n).Select(e => WhiskPick(phase, e, e == 1)));
        Check($"phase 1 only steals ({Seq(1, 3)})", Seq(1, 3) == "thief,thief,thief");
        Check($"phase 2 remembers first, then steals and remembers in turn ({Seq(2, 4)})", Seq(2, 4) == "memory,thief,memory,thief");
        Check($"phase 3 keeps quiet first, then remembers, steals, keeps quiet ({Seq(3, 4)})", Seq(3, 4) == "quiet,memory,thief,quiet");

        // The floor: a missed trick never raises a nearly lost fish.
        reel.Progress = 0.02f; whisk.Events = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => { WhiskCalm(-2); reel.Progress = 0.02f; })) yield return 1;
        Inp.Hold(KeyboardKey.E, true);
        foreach (var _ in LungUntil(() => whisk.Ev != "", 8f, () => { if (whisk.Ev.EndsWith("Warn")) reel.Progress = 0.02f; })) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        Check($"a missed trick at 0.02 doesn't save it ({reel?.Progress:0.00})", reel == null || reel.Progress <= 0.0201f);
        if (!WhiskersOnLine) { TestBite("lagoon", WhiskersFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy; Hook(); yield return 2; }
        whisk.Banner = 0; whisk.NextEv = 99; reel.Progress = 0.5f;

        /* ---------- Pausing ---------- */
        whisk.Phase = 3; whisk.Fresh = true; whisk.NextEv = 0.01f; reel.Progress = 0.85f;
        foreach (var _ in LungUntil(() => whisk.Ev != "quiet", 8f, () => WhiskCalm(0.85f))) yield return 1;
        OpenPause(anyMode: true); yield return 3;
        float frozen = whisk.EvT, quietT = whisk.QuietT;
        yield return 20;
        Check($"paused, he waits ({mode}, {frozen:0.00} -> {whisk?.EvT:0.00}, {quietT:0.00} -> {whisk?.QuietT:0.00})", mode == "pause" && whisk != null && whisk.EvT == frozen && whisk.QuietT == quietT);
        Check($"and he stays drawn and framed behind the menu ({whiskCamK:0.00})", WhiskersShown && whiskCamK > 0.9f);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to the fight", mode == "reeling" && WhiskersOnLine);
        foreach (var _ in LungUntil(() => whisk.Ev != "", 8f, () => WhiskCalm(-1))) yield return 1;
        whisk.NextEv = 99;

        /* ---------- Codex's review ---------- */
        // A long frame that carries the thief past its window: a press in that frame is too late.
        whiskLast = ""; whisk.Ev = "thief"; whisk.Delay = 1f; whisk.EvT = 1.5f; reel.Progress = 0.5f;
        UpdateWhiskersReel(reel, 1.6f, false, true);
        Check($"a press in a frame that ran past the window doesn't win ({whiskLast}, {whiskDelta:0.00})", whiskLast == "miss");
        // A control still held (say, the click that resumed the game) stops the quiet from counting, even if the reel ignores it.
        whisk.NextEv = 99; whisk.Ev = "quiet"; whisk.Armed = false; whisk.WaitT = 0; whisk.QuietT = 0;
        Inp.Hold(KeyboardKey.E, true); yield return 3;
        UpdateWhiskersReel(reel, 0.05f, false, false);
        Check($"a held reel control keeps it from arming ({whisk.Armed})", !whisk.Armed);
        Inp.Hold(KeyboardKey.E, false); yield return 3;
        whisk.Ev = ""; whisk.NextEv = 99;

        /* ---------- Landing it ---------- */
        whiskLast = "";
        reel.Progress = 0.97f; whisk.Phase = 3; whisk.Fresh = false; whisk.Events = 2; whisk.Banner = 0; whisk.NextEv = 0.01f;
        foreach (var _ in LungUntil(() => whisk.Ev == "", 8f, () => WhiskCalm(0.97f))) yield return 1;
        Check($"the thief comes ({whisk.Ev})", whisk.Ev == "thiefWarn");
        whisk.Delay = 0.7f;
        foreach (var _ in LungUntil(() => whisk.Ev != "thief" || (whisk.Delay + WhiskWindow - whisk.EvT) < whisk.Delay + 0.08f, 8f, () => WhiskCalm(0.97f))) yield return 1;
        Inp.Tap(KeyboardKey.E); yield return 3;
        foreach (var _ in LungUntil(() => mode == "reeling", 3f)) yield return 1;
        yield return 3;
        Check($"the strike lands him: his legend card ({mode}, {legendId})", mode == "legend" && legendId == WhiskersFish);
        Check("he is yours to keep: in the bag", Has(WhiskersFish) == 1);
        pendingShot = "whisk-10-card"; yield return 2;
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
        yield return 3;
        Check("and the fight is over: nothing left of it", whisk == null && !WhiskersOnLine);
        Check("the first catch can be fought again", state.commons.GetValueOrDefault(WhiskersFish) >= 1 && Bitable());

        /* ---------- A rematch he wins, and a faint ---------- */
        state.inv["berries"] = 5;
        TestBite("lagoon", WhiskersFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        Check("he can be hooked again", WhiskersOnLine);
        whisk.NextEv = 99; reel.Progress = 0.02f;
        foreach (var _ in LungUntil(() => WhiskersOnLine, 10f, () => { if (whisk != null) whisk.NextEv = 99; })) yield return 1;
        yield return 2;
        Check($"let him run and he gets away ({mode}), and the fight is cleared", mode == "play" && whisk == null);
        TestBite("lagoon", WhiskersFish); fish.Bx = fish.Tx = cx; fish.By = fish.Ty = cy;
        Hook(); yield return 2;
        whisk.Banner = 0; whisk.NextEv = 0.01f; yield return 3;
        Faint(); yield return 4;
        for (int i = 0; i < 300 && mode != "play"; i++) yield return 1;
        yield return 3;
        Check($"fainting mid-fight ends it cleanly ({mode})", !WhiskersOnLine && whisk == null);
    }

    // FESH_SPRITES -whiskers, at 4x: the Blender catfish (swimming through its wave, the mouth open, a rise and dive, facing
    // west) beside the Fish log's hand-drawn picture, on lagoon water.
    void ExportWhiskersSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(420, 110);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#2a9d8f"));
        var poses = new (float up, bool open, int dir)[] { (0, false, 1), (0, false, 1), (0, true, 1), (30, false, 1), (-40, false, 1), (0, false, -1) };
        for (int i = 0; i < poses.Length; i++) DrawWhiskFish(36 + i * 68, 38 + 99, poses[i].dir, poses[i].up, i, poses[i].open, -99, dropUnder: true);
        FishArt.Draw(sheet, WhiskersFish, 16, 80, 36, 12);
        FishArt.Draw(sheet, WhiskersFish, 84, 80, 36, 12, true);
        pix = keep;
        var img = Raylib.GenImageColor(420, 110, Color.Black);
        for (int yy = 0; yy < 110; yy++)
            for (int xx = 0; xx < 420; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * 420 + xx]);
        Raylib.ImageResizeNN(ref img, 420 * 4, 110 * 4);
        Raylib.ExportImage(img, path.Replace(".png", "-whiskers.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
