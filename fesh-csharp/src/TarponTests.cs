#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_TARPON_TEST=1: Haring Buan-buan's boss fight (TarponBoss.cs): when he bites (and again after he's been let go), the
// card, bar and music, staying in the water by the pier, a breath with the line lifted clear (hold up) and one not, the
// pilings cranked clear (mash) and one let wrap, a head-shake held steady and one let go of, events waiting for a leap
// to end, his scales flashing in the beam, pausing by the real Resume, landing him (let go: the record and card, nothing
// in the bag), the swim away, losing him and a faint, with screenshots of each.
partial class Game
{
    IEnumerable<int> TarponScript()
    {
        Note("Haring Buan-buan: the boss fight");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(true); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hp = 100; state.inv["tamban"] = 20;
        state.hinted["habagat"] = true; state.hinted["moonReturned"] = true; state.hinted["parolaLit"] = true; state.parola = 3;

        /* ---------- When he bites ---------- */
        bool Bitable() => FishWeights("parola", "tamban", 2).Any(p => p.f.Id == TarponFish);
        Check($"under the returned full moon (phase {MoonPhase}) on a tamban he can bite off the pier", FullMoon && Bitable());
        SetNight(false);
        Check("never by day", !Bitable());
        SetNight(true);
        Check("never on another bait", FishWeights("parola", "worm", 2).All(p => p.f.Id != TarponFish));
        state.commons[TarponFish] = 1;
        Check("and after he's been let go once, he can come back", Bitable());
        state.commons.Remove(TarponFish);

        /* ---------- He takes the tamban ---------- */
        var ps = Data.SpotById["parola"];
        var (sx, sy) = Enumerable.Range(0, 30 * 30).Select(i => (x: ps.X - 150 + i % 30 * 10 + 5f, y: ps.Y - 150 + i / 30 * 10 + 7f))
            .Where(p => CanStand(p.x, p.y) && Dist(p.x, p.y, ps.X, ps.Y) < ps.R - 4).OrderBy(p => Dist(p.x, p.y, ps.X, ps.Y)).First();
        player.X = sx; player.Y = sy; player.Face = "left"; yield return 3;
        TestBite("parola", TarponFish); fish.Bx = fish.Tx = ps.X; fish.By = fish.Ty = ps.Y;
        Hook(); yield return 2;
        Check("the king takes the tamban: the fight is on", TarponOnLine && tarpon != null);
        Check($"he starts far out ({reel.Progress:0.00})", reel.Progress <= 0.39f);
        Check($"in open water by the pier, room for his whole body ({tarpon.CX:0},{tarpon.CY:0})", TarponWater(tarpon.CX, tarpon.CY));
        Check("his title card shows", tarpon.Banner > 2.5f);
        Check($"the boss music plays ({WantedTrack()})", WantedTrack() == "boss");
        yield return 10;
        pendingShot = "tarpon-01-banner"; yield return 2;
        tarpon.NextEv = 99;
        int dry = 0;
        for (int i = 0; i < 150; i++)
        {
            if (!TarponWater(tarpon.X, tarpon.Y)) dry++;
            reel.Progress = 0.35f; reel.Leap = 0; reel.LeapT = 99;
            yield return 1;
        }
        Check($"he swims by the pier and never onto it ({dry} frames out)", dry == 0);
        // The beam: somewhere on its sweep it crosses him.
        float best = 0;
        for (int i = 0; i < 100; i++) { time += 0.1f; best = MathF.Max(best, BeamOn(tarpon.X, tarpon.Y)); }
        Check($"the lighthouse beam sweeps across him ({best:0.00})", best > 0.5f);
        pendingShot = "tarpon-02-swim"; yield return 2;
        float hp0 = state.hp;

        /* ---------- A breath of moonlight ---------- */
        reel.Leap = 0; tarpon.NextEv = 0.01f; yield return 3;
        Check($"his nose rises: a breath is coming ({tarpon.Ev})", tarpon.Ev == "breathWarn");
        for (int i = 0; i < 200 && tarpon.Ev == "breathWarn"; i++) yield return 1;
        float p0 = reel.Progress;
        Inp.Hold(KeyboardKey.Up, true);
        for (int i = 0; i < 200 && tarpon.Ev == "breath" && tarpon.EvT > BreathLen * 0.6f; i++) yield return 1;
        pendingShot = "tarpon-03-breath"; yield return 1;
        for (int i = 0; i < 200 && tarpon.Ev == "breath"; i++) yield return 1;
        Inp.Hold(KeyboardKey.Up, false);
        Check($"lifting the line clear (holding up) pays ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.03f);
        tarpon.NextEv = 99; reel.Progress = 0.4f; yield return 2;
        reel.Leap = 0; reel.LeapT = 99; tarpon.NextEv = 0.01f; p0 = 0.4f;
        for (int i = 0; i < 300 && tarpon.Ev == ""; i++) yield return 1;
        for (int i = 0; i < 400 && tarpon.Ev != ""; i++) yield return 1;
        Check($"leave the line on him and he rolls on it ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0);
        tarpon.NextEv = 99;

        /* ---------- Between the pilings ---------- */
        reel.Progress = 0.55f; reel.LeapT = 99; yield return 3;
        Check($"past the first mark ({tarpon.Phase}, {tarpon.Title})", tarpon.Phase == 2 && tarpon.Title == "BETWEEN THE PILINGS");
        tarpon.Banner = 0; reel.Leap = 0; tarpon.NextEv = 0.01f; yield return 3;
        Check($"he turns for the pier ({tarpon.Ev}, piling at {tarpon.PileX:0},{tarpon.PileY:0})", tarpon.Ev == "pilingsWarn" && Enumerable.Range(-12, 25).Any(dx => TileUnder(tarpon.PileX + dx, tarpon.PileY) == 'b'));
        for (int i = 0; i < 200 && tarpon.Ev == "pilingsWarn"; i++) yield return 1;
        pendingShot = "tarpon-04-pilings"; yield return 1;
        p0 = reel.Progress;
        for (int i = 0; i < 40 && tarpon.Ev == "pilings"; i++) { Inp.Tap(KeyboardKey.E); yield return 2; }
        Check($"mashing the reel cranks him clear ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.03f && tarpon.Ev == "");
        tarpon.NextEv = 99; reel.Progress = 0.6f; yield return 2;
        tarpon.Events = 0; reel.Leap = 0; reel.LeapT = 99; tarpon.NextEv = 0.01f; p0 = 0.6f;
        for (int i = 0; i < 300 && tarpon.Ev == ""; i++) yield return 1;
        for (int i = 0; i < 400 && tarpon.Ev != ""; i++) yield return 1;
        Check($"leave him and the line wraps a piling ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0);
        tarpon.NextEv = 99;
        Check("but the line holds: he's still on", TarponOnLine);

        /* ---------- The king shakes his crown ---------- */
        reel.Progress = 0.8f; reel.LeapT = 99; yield return 3;
        Check($"past the second mark ({tarpon.Phase})", tarpon.Phase == 3);
        tarpon.Banner = 0; reel.Leap = 0; tarpon.NextEv = 0.01f; yield return 3;
        Check($"he rolls his shoulders: a shake is coming ({tarpon.Ev})", tarpon.Ev == "shakeWarn");
        for (int i = 0; i < 200 && tarpon.Ev == "shakeWarn"; i++) yield return 1;
        p0 = reel.Progress;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 200 && tarpon.Ev == "shake" && tarpon.EvT > ShakeLen * 0.5f; i++) yield return 1;
        pendingShot = "tarpon-05-shake"; yield return 1;
        for (int i = 0; i < 200 && tarpon.Ev == "shake"; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        Check($"holding steady through the shake pays ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.04f);
        tarpon.NextEv = 99; reel.Progress = 0.82f; yield return 2;
        tarpon.Events = 0; reel.Leap = 0; reel.LeapT = 99; tarpon.NextEv = 0.01f; p0 = 0.82f;
        for (int i = 0; i < 300 && tarpon.Ev == ""; i++) yield return 1;
        for (int i = 0; i < 400 && tarpon.Ev != ""; i++) yield return 1;
        Check($"let go of him in the shake and he shakes out line ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0);
        tarpon.NextEv = 99;

        /* ---------- His leaps, and events waiting for them ---------- */
        reel.Progress = 0.85f; reel.Leap = reel.LeapLen; reel.LeapMark = 0; reel.LeapDone = false; tarpon.NextEv = 0.01f; yield return 3;
        Check($"no event in the middle of a leap ({tarpon.Ev})", tarpon.Ev == "");
        for (int i = 0; i < 200 && reel.LeapMark < 0.45f; i++) yield return 1;
        pendingShot = "tarpon-06-leap"; yield return 1;
        for (int i = 0; i < 300 && reel.Leap > 0; i++) yield return 1;
        reel.LeapT = 99; reel.Progress = 0.85f;
        for (int i = 0; i < 300 && tarpon.Ev == ""; i++) { reel.LeapT = 99; yield return 1; }
        Check($"once he's down, it comes ({tarpon.Ev})", tarpon.Ev != "");

        /* ---------- Pausing ---------- */
        yield return 6;
        OpenPause(anyMode: true); yield return 3;
        float frozen = tarpon.EvT;
        yield return 20;
        Check($"paused, he waits ({mode}, {frozen:0.00} -> {tarpon?.EvT:0.00})", mode == "pause" && tarpon != null && tarpon.EvT == frozen && TarponShown);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to the fight", mode == "reeling" && TarponOnLine);
        Check($"and nothing in it hurt you ({hp0:0} -> {state.hp:0})", state.hp >= hp0);

        /* ---------- Landing him, and letting him go ---------- */
        // One last breath, the line lifted clear, brings him in.
        for (int i = 0; i < 400 && tarpon.Ev != ""; i++) { reel.Progress = 0.9f; yield return 1; }
        reel.Progress = 0.97f; reel.Leap = 0; reel.LeapT = 99; tarpon.Events = 2; tarpon.NextEv = 0.01f;
        Inp.Hold(KeyboardKey.Up, true);
        for (int i = 0; i < 600 && mode == "reeling"; i++) { reel.LeapT = 99; if (reel.Progress < 0.9f) reel.Progress = 0.97f; yield return 1; }
        Inp.Hold(KeyboardKey.Up, false);
        Check($"reeled in: his legend card ({mode}, {legendId})", mode == "legend" && legendId == TarponFish);
        Check($"he's in your records, not your bag (caught {state.commons.GetValueOrDefault(TarponFish)}, in the bag {Has(TarponFish)})", state.commons.GetValueOrDefault(TarponFish) == 1 && Has(TarponFish) == 0 && state.big.GetValueOrDefault(TarponFish) == 0);
        pendingShot = "tarpon-07-card"; yield return 2;
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check($"you ease the hook free and he swims beyond the light ({toastMsg})", tarponHome != null && toastMsg.Contains("ease the hook free") && heldItem != TarponFish);
        yield return 16;
        pendingShot = "tarpon-08-home"; yield return 2;
        for (int i = 0; i < 400 && tarponHome != null; i++) yield return 1;
        Check("and he's gone", tarponHome == null && tarpon == null);
        // An older big one in the bag keeps its premium when an ordinary one is let go.
        state.inv[TarponFish] = 2; state.big[TarponFish] = 1; lastM = 1.0f;
        FreeTarpon();
        Check($"an ordinary one let go leaves an older big one's premium ({Has(TarponFish)}, {state.big.GetValueOrDefault(TarponFish)} big)", Has(TarponFish) == 1 && state.big.GetValueOrDefault(TarponFish) == 1);
        state.inv.Remove(TarponFish); state.big.Remove(TarponFish);


        /* ---------- Codex's review ---------- */
        TestBite("parola", TarponFish); fish.Bx = fish.Tx = ps.X; fish.By = fish.Ty = ps.Y;
        Hook(); yield return 2;
        // A shake must be held steady: holding, letting go, then holding again isn't.
        reel.Progress = 0.85f; tarpon.Phase = 3; tarpon.Banner = 0; tarpon.Events = 0; reel.Leap = 0; reel.LeapT = 99; tarpon.NextEv = 0.01f;
        for (int i = 0; i < 300 && tarpon.Ev != "shake"; i++) yield return 1;
        p0 = reel.Progress;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 300 && tarpon.Ev == "shake" && tarpon.EvT > ShakeLen - 0.85f; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        for (int i = 0; i < 300 && tarpon.Ev == "shake" && tarpon.EvT > ShakeLen - 1.15f; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 300 && tarpon.Ev == "shake"; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        Check($"holding, letting go and holding again through a shake doesn't count ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0);
        tarpon.NextEv = 99;
        // Every place the fish may be is water all along his body: no plank between the samples.
        int crossing = 0;
        for (float y = ps.Y - 40; y <= ps.Y + 40; y += 2)
            for (float x = ps.X - 60; x <= ps.X + 60; x += 2)
                if (TarponWater(x, y))
                    for (float dx = -21; dx <= 21; dx += 1) if (!Swimmable(TileUnder(x + dx, y))) { crossing++; break; }
        Check($"wherever he's allowed, his whole body is over water ({crossing} places with a plank under him)", crossing == 0);
        // Left right against the pier after the pilings, he swims back out.
        var pile = NearestPiling(ps.X, ps.Y);
        tarpon.X = pile.x; tarpon.Y = pile.y; tarpon.Ev = ""; tarpon.NextEv = 99; tarpon.WanderT = 0;
        for (int i = 0; i < 400 && !TarponWater(tarpon.X, tarpon.Y); i++) { reel.Progress = 0.5f; reel.Leap = 0; reel.LeapT = 99; yield return 1; }
        Check($"left against the pier, he swims back out to open water ({tarpon.X:0},{tarpon.Y:0})", TarponWater(tarpon.X, tarpon.Y));
        // The piling he heads for is right beside the pier's planks.
        bool beside = Enumerable.Range(-12, 25).Any(dx => TileUnder(pile.x + dx, pile.y) == 'b');
        Check($"the piling he turns for is right by the pier ({pile.x:0},{pile.y:0})", beside && Swimmable(TileUnder(pile.x, pile.y)));
        if (TarponOnLine) GotAway("test");
        yield return 3;
        /* ---------- Losing him, and fainting ---------- */
        TestBite("parola", TarponFish); fish.Bx = fish.Tx = ps.X; fish.By = fish.Ty = ps.Y;
        Hook(); yield return 2;
        Check("he can be hooked again", TarponOnLine);
        reel.Progress = 0.02f;
        for (int i = 0; i < 400 && TarponOnLine; i++) { if (tarpon != null) tarpon.NextEv = 99; reel.LeapT = 99; yield return 1; }
        yield return 2;
        Check($"let the line go slack and he gets away, and the fight is cleared ({mode})", mode == "play" && tarpon == null);
        TestBite("parola", TarponFish); fish.Bx = fish.Tx = ps.X; fish.By = fish.Ty = ps.Y;
        Hook(); yield return 2;
        Faint(); yield return 4;
        for (int i = 0; i < 300 && mode != "play"; i++) yield return 1;
        yield return 3;
        Check($"fainting mid-fight ends it cleanly ({mode})", !TarponOnLine && tarpon == null);
    }

    // FESH_SPRITES -tarpon, at 4x: the Blender king (level, nose up and down, the mouth open, rolled for a shake, facing
    // west), and the Fish log's hand-drawn picture below, on the night sea.
    void ExportTarponSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(470, 110);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#123a5e"));
        var poses = new (float up, bool open, int roll, int dir)[] { (0, false, 0, 1), (40, false, 0, 1), (-40, false, 0, 1), (25, true, 0, 1), (40, false, 40, 1), (0, false, 50, 1), (0, false, 0, -1) };
        for (int i = 0; i < poses.Length; i++) DrawTarponFish(34 + i * 66, 38 + 99, poses[i].dir, poses[i].up, i, poses[i].open, poses[i].roll, -99, true, 1);
        FishArt.Draw(sheet, TarponFish, 16, 82, 36, 14);
        pix = keep;
        var img = Raylib.GenImageColor(470, 110, Color.Black);
        for (int yy = 0; yy < 110; yy++)
            for (int xx = 0; xx < 470; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * 470 + xx]);
        Raylib.ImageResizeNN(ref img, 470 * 3, 110 * 3);
        Raylib.ExportImage(img, path.Replace(".png", "-tarpon.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
