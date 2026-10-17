#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_RAY_TEST=1: the Starfall ray's boss fight (RayBoss.cs): when it bites (and again after it's been let go), the
// card, bar and music, staying over the deep water, a feeding sweep followed by the keys and one lost, a somersault let
// go and one held through, a breach bowed to (held down) and one not, events waiting for a run to end, pausing by the
// real Resume, landing it (let go: the record and card, nothing in the bag), the glide home, losing it and a faint, with
// screenshots of each.
partial class Game
{
    IEnumerable<int> RayScript()
    {
        Note("The Starfall ray: the boss fight");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(true); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hp = 100; state.inv["glow_shrimp"] = 20;
        state.hinted["visitedAtoll"] = true;

        /* ---------- When it bites ---------- */
        bool Bitable() => FishWeights("dropoff", "glow_shrimp", 2).Any(p => p.f.Id == RayFish);
        Check($"under the full moon (phase {MoonPhase}) on a glow shrimp it can bite at the drop-off", FullMoon && Bitable());
        SetNight(false);
        Check("never by day", !Bitable());
        SetNight(true);
        Check("never on another bait", FishWeights("dropoff", "worm", 2).All(p => p.f.Id != RayFish));
        state.commons[RayFish] = 1;
        Check("and after you've let it go once, it can come back", Bitable());
        state.commons.Remove(RayFish);

        /* ---------- It takes the shrimp ---------- */
        // The atoll is across the channel from Saltmere, so look for the beach near the spot directly.
        var ds = Data.SpotById["dropoff"];
        var (sx, sy) = Enumerable.Range(0, 30 * 30).Select(i => (x: ds.X - 150 + i % 30 * 10 + 5f, y: ds.Y - 20 + i / 30 * 10 + 7f))
            .Where(p => CanStand(p.x, p.y) && Dist(p.x, p.y, ds.X, ds.Y) < ds.R - 4).OrderBy(p => Dist(p.x, p.y, ds.X, ds.Y)).First();
        player.X = sx; player.Y = sy; player.Face = "up"; yield return 3;
        var spot = Data.SpotById["dropoff"];
        TestBite("dropoff", RayFish); fish.Bx = fish.Tx = spot.X; fish.By = fish.Ty = spot.Y;
        Hook(); yield return 2;
        Check("the ray takes the shrimp: the fight is on", RayOnLine && ray != null);
        Check($"it starts far out ({reel.Progress:0.00})", reel.Progress <= 0.39f);
        Check($"over deep water ({ray.CX:0},{ray.CY:0})", RayWater(ray.CX, ray.CY));
        Check("its title card shows", ray.Banner > 2.5f);
        Check($"the boss music plays ({WantedTrack()})", WantedTrack() == "boss");
        yield return 10;
        pendingShot = "ray-01-banner"; yield return 2;
        ray.NextEv = 99;
        int dry = 0;
        for (int i = 0; i < 150; i++)
        {
            if (!RayWater(ray.X, ray.Y)) dry++;
            reel.Progress = 0.35f; reel.Running = 0; reel.Tension = 0;
            yield return 1;
        }
        Check($"it glides over the deep water and never into the shallows ({dry} frames out)", dry == 0);
        pendingShot = "ray-02-glide"; yield return 2;
        SetNight(false); yield return 4;
        pendingShot = "ray-02b-day"; yield return 2;
        SetNight(true); yield return 2;
        float hp0 = state.hp;

        /* ---------- A feeding sweep ---------- */
        reel.Running = 0; ray.NextEv = 0.01f; yield return 3;
        Check($"it unrolls its fins to feed: a sweep is coming ({ray.Ev})", ray.Ev == "sweepWarn");
        for (int i = 0; i < 200 && ray.Ev == "sweepWarn"; i++) yield return 1;
        Check("then the sweep", ray.Ev == "sweep");
        float p0 = reel.Progress;
        yield return 6;
        pendingShot = "ray-03-sweep"; yield return 1;
        // Follow it: steer the marker with the real keys toward where it is.
        for (int i = 0; i < 300 && ray?.Ev == "sweep"; i++)
        {
            bool right = ray.Rod < ray.Mark - 0.04f, left = ray.Rod > ray.Mark + 0.04f;
            Inp.Hold(KeyboardKey.Right, right); Inp.Hold(KeyboardKey.Left, left);
            yield return 1;
        }
        Inp.Hold(KeyboardKey.Right, false); Inp.Hold(KeyboardKey.Left, false);
        Check($"following it with the keys pays ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.03f && ray.Ev == "");
        ray.NextEv = 99; reel.Progress = 0.4f; yield return 2;
        // One left alone, the marker kept off to the side.
        reel.Running = 0; ray.NextEv = 0.01f; p0 = 0.4f;
        for (int i = 0; i < 400 && ray.Ev != "sweep"; i++) yield return 1;
        for (int i = 0; i < 400 && ray.Ev == "sweep"; i++) { ray.Rod = ray.Mark > 0.5f ? 0 : 1; yield return 1; }
        Check($"lose it and it glides farther out ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0 && reel.Progress > p0 - 0.05f);
        ray.NextEv = 99;

        /* ---------- A turning constellation ---------- */
        reel.Progress = 0.55f; yield return 3;
        Check($"past the first mark ({ray.Phase}, {ray.Title})", ray.Phase == 2 && ray.Title == "A TURNING CONSTELLATION");
        ray.Banner = 0; reel.Running = 0; ray.NextEv = 0.01f; yield return 3;
        Check($"its first trick is the loop ({ray.Ev})", ray.Ev == "loopWarn");
        for (int i = 0; i < 200 && ray.Ev == "loopWarn"; i++) yield return 1;
        yield return 10;
        pendingShot = "ray-04-loop"; yield return 1;
        p0 = reel.Progress;
        for (int i = 0; i < 300 && ray.Ev == "loop"; i++) yield return 1;
        Check($"letting go through the somersault pays ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.02f);
        ray.NextEv = 99; reel.Progress = 0.6f; yield return 2;
        // Held through one.
        ray.Events = 0; reel.Running = 0; ray.NextEv = 0.01f; p0 = 0.6f;
        for (int i = 0; i < 300 && ray.Ev != "loop"; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, true);
        for (int i = 0; i < 300 && ray.Ev == "loop"; i++) yield return 1;
        Inp.Hold(KeyboardKey.E, false);
        Check($"hold on through it and the loop takes line ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0);
        ray.NextEv = 99;
        Check("but the line holds: it's still on", RayOnLine);

        /* ---------- Stars above the sea ---------- */
        reel.Progress = 0.8f; yield return 3;
        Check($"past the second mark ({ray.Phase})", ray.Phase == 3);
        ray.Banner = 0; reel.Running = 0; ray.NextEv = 0.01f; yield return 3;
        Check($"it's coming up: a breach ({ray.Ev})", ray.Ev == "breachWarn");
        p0 = reel.Progress;
        Inp.Hold(KeyboardKey.Down, true);
        for (int i = 0; i < 300 && ray.Ev != "breach"; i++) yield return 1;
        for (int i = 0; i < 300 && ray.Ev == "breach" && ray.EvT > BreachLen * 0.5f; i++) yield return 1;
        pendingShot = "ray-05-breach"; yield return 1;
        for (int i = 0; i < 300 && ray.Ev == "breach"; i++) yield return 1;
        Inp.Hold(KeyboardKey.Down, false);
        Check($"bowing to it (holding down) pays ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress > p0 + 0.04f);
        ray.NextEv = 99; reel.Progress = 0.82f; yield return 2;
        ray.Events = 0; reel.Running = 0; ray.NextEv = 0.01f; p0 = 0.82f;
        for (int i = 0; i < 300 && ray.Ev == ""; i++) yield return 1;
        for (int i = 0; i < 600 && ray.Ev is "breachWarn" or "breach"; i++) yield return 1;
        Check($"stand stiff through a breach and the splash pulls out line ({p0:0.00} -> {reel.Progress:0.00})", reel.Progress < p0);
        ray.NextEv = 99;

        /* ---------- Events wait for a run to end ---------- */
        reel.Running = 1.0f; ray.NextEv = 0.01f; yield return 6;
        Check($"no trick in the middle of one of its runs ({ray.Ev})", ray.Ev == "");
        reel.Running = 0; reel.Progress = 0.85f; yield return 6;
        Check($"once the run is over, it comes ({ray.Ev})", ray.Ev != "");

        /* ---------- Pausing ---------- */
        yield return 10;
        OpenPause(anyMode: true); yield return 3;
        float frozen = ray.EvT;
        yield return 20;
        Check($"paused, it waits ({mode}, {frozen:0.00} -> {ray?.EvT:0.00})", mode == "pause" && ray != null && ray.EvT == frozen && RayShown);
        ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check("the real Resume button goes back to the fight", mode == "reeling" && RayOnLine);
        Check($"and nothing in it hurt you ({hp0:0} -> {state.hp:0})", state.hp >= hp0);

        /* ---------- Landing it, and letting it go ---------- */
        for (int i = 0; i < 400 && ray.Ev != ""; i++) { reel.Progress = 0.9f; yield return 1; }
        ray.NextEv = 99;
        reel.Progress = 0.999f; reel.Running = 0;
        for (int i = 0; i < 300 && mode == "reeling"; i++) { Inp.Hold(KeyboardKey.E, true); yield return 1; }
        Inp.Hold(KeyboardKey.E, false);
        Check($"reeled in: its legend card ({mode}, {legendId})", mode == "legend" && legendId == RayFish);
        Check($"it's in your records, not your bag (caught {state.commons.GetValueOrDefault(RayFish)}, in the bag {Has(RayFish)})", state.commons.GetValueOrDefault(RayFish) == 1 && Has(RayFish) == 0 && state.big.GetValueOrDefault(RayFish) == 0);
        pendingShot = "ray-06-card"; yield return 2;
        while (mode == "legend") { Inp.Tap(KeyboardKey.E); yield return 3; }
        Check($"you free the line and it glides back over the drop-off ({toastMsg})", rayHome != null && toastMsg.Contains("free the line") && heldItem != RayFish);
        yield return 20;
        pendingShot = "ray-07-home"; yield return 2;
        for (int i = 0; i < 400 && rayHome != null; i++) yield return 1;
        Check("and it's gone into the deep", rayHome == null && ray == null);


        /* ---------- Codex's review ---------- */
        // The wings beat (the frame changes over time).
        Check($"its wings beat: the flap step changes ({RayFlap(0.1f)}, {RayFlap(0.5f)}, {RayFlap(0.9f)})", new[] { 0.1f, 0.5f, 0.9f, 1.3f }.Select(RayFlap).Distinct().Count() >= 2);
        // Holding down only through the warning, then letting go in the air, isn't bowing to it.
        TestBite("dropoff", RayFish); fish.Bx = fish.Tx = spot.X; fish.By = fish.Ty = spot.Y;
        Hook(); yield return 2;
        reel.Progress = 0.85f; ray.Phase = 3; ray.Banner = 0; ray.Events = 0; reel.Running = 0; ray.NextEv = 0.01f;
        for (int i = 0; i < 300 && ray.Ev != "breachWarn"; i++) yield return 1;
        Inp.Hold(KeyboardKey.Down, true);
        for (int i = 0; i < 300 && ray.Ev == "breachWarn"; i++) yield return 1;
        Inp.Hold(KeyboardKey.Down, false);
        p0 = reel.Progress;
        for (int i = 0; i < 300 && ray.Ev == "breach"; i++) yield return 1;
        Check($"bowing only before it leaves the water doesn't count ({p0:0.00} -> {reel?.Progress:0.00})", reel != null && reel.Progress < p0);
        // Letting one go doesn't take the premium off a big one already in the bag (from before it was let go).
        if (MireOnLine || RayOnLine) GotAway("test");
        yield return 2;
        state.inv[RayFish] = 2; state.big[RayFish] = 1; lastM = 1.0f;   // an old big one, and an ordinary one just landed
        FreeRay();
        Check($"an ordinary one let go leaves the old big one's premium ({Has(RayFish)} in the bag, {state.big.GetValueOrDefault(RayFish)} big)", Has(RayFish) == 1 && state.big.GetValueOrDefault(RayFish) == 1);
        state.inv[RayFish] = 2; state.big[RayFish] = 2; lastM = 1.4f;   // and a big one just landed
        FreeRay();
        Check($"a big one let go takes its own premium with it ({state.big.GetValueOrDefault(RayFish)} big)", state.big.GetValueOrDefault(RayFish) == 1);
        state.inv.Remove(RayFish); state.big.Remove(RayFish);
        /* ---------- Losing it, and fainting ---------- */
        TestBite("dropoff", RayFish); fish.Bx = fish.Tx = spot.X; fish.By = fish.Ty = spot.Y;
        Hook(); yield return 2;
        Check("it can be hooked again", RayOnLine);
        reel.Progress = 0.02f;
        for (int i = 0; i < 400 && RayOnLine; i++) { if (ray != null) ray.NextEv = 99; yield return 1; }
        yield return 2;
        Check($"let it go slack and it gets away, and the fight is cleared ({mode})", mode == "play" && ray == null);
        TestBite("dropoff", RayFish); fish.Bx = fish.Tx = spot.X; fish.By = fish.Ty = spot.Y;
        Hook(); yield return 2;
        Faint(); yield return 4;
        for (int i = 0; i < 300 && mode != "play"; i++) yield return 1;
        yield return 3;
        Check($"fainting mid-fight ends it cleanly ({mode})", !RayOnLine && ray == null);
    }

    // FESH_SPRITES -manta, at 4x: the Blender ray gliding at eight headings, its loop (the breach poses), and below them the
    // Fish log's hand-drawn picture, on the night sea.
    void ExportRaySprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(600, 170);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#123a5e"));
        for (int i = 0; i < 8; i++) DrawRayFrame(RayGlideFrame(-i * MathF.PI / 4, 1), 36 + i * 70, 40 + 99, -99, true, 1);
        for (int i = 0; i < 7; i++) DrawRayFrame(RayLoopFrame(45 + i * 45), 36 + i * 70, 105 + 99, -99, true, 1);
        FishArt.Draw(sheet, RayFish, 520, 120, 36, 16);
        pix = keep;
        var img = Raylib.GenImageColor(600, 170, Color.Black);
        for (int yy = 0; yy < 170; yy++)
            for (int xx = 0; xx < 600; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * 600 + xx]);
        Raylib.ImageResizeNN(ref img, 600 * 3, 170 * 3);
        Raylib.ExportImage(img, path.Replace(".png", "-manta.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
