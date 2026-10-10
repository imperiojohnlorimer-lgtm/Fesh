#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // Tidemane's look, its gaits, the whistle and the ride (1.20). FESH_MOUNT_TEST=1 runs only this.
    IEnumerable<int> MountScript()
    {
        Note("Tidemane: the new sprite, riding and the whistle");
        state = new State { created = true, tamed = true, look = new Look { name = "Rider" } };
        state.flags.metTomas = true; state.hinted["starwell"] = true; state.hinted["visitedAtoll"] = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 2;

        /* ---------- The sprite ---------- */
        // Every pose has its head (eye, ears, horns), its foam, its hooves and its fan, and nothing runs off the canvas.
        var poses = new List<MountPose>();
        foreach (var k in new[] { "stand", "rear", "lie", "leap", "swim" }) poses.Add(new MountPose { Kind = k, Saddle = true, Time = 1 });
        for (int f = 0; f < 8; f++) poses.Add(new MountPose { Kind = "gallop", Frame = f, Saddle = true, Time = 1 });
        for (int f = 0; f < 6; f++) poses.Add(new MountPose { Kind = "trot", Frame = f, Time = 1 });
        poses.Add(new MountPose { Kind = "leap", Leap = 0.9f, Time = 1 }); poses.Add(new MountPose { Kind = "stand", Toss = 0.5f, Time = 1 });
        var missing = new List<string>();
        foreach (var p in poses)
        {
            MountArt.BuildSide(mountCanvas, p);
            string has = new string(mountCanvas.M);
            foreach (char m in new[] { 'E', 'C', 'F', 'M', 'S' })
                if (!has.Contains(m)) missing.Add($"{p.Kind}{p.Frame}:{m}");
            if (p.Kind is not ("swim" or "lie") && !has.Contains('O')) missing.Add($"{p.Kind}{p.Frame}:hoof");
            for (int i = 0; i < MountCanvas.W; i++)
                if (mountCanvas.M[i] != '.' || mountCanvas.M[(MountCanvas.H - 1) * MountCanvas.W + i] != '.') { missing.Add($"{p.Kind}{p.Frame}:edge"); break; }
            for (int j = 0; j < MountCanvas.H; j++)
                if (mountCanvas.M[j * MountCanvas.W] != '.' || mountCanvas.M[j * MountCanvas.W + MountCanvas.W - 1] != '.') { missing.Add($"{p.Kind}{p.Frame}:edge"); break; }
        }
        Check($"every pose has its eye, horns, fins, foam, tail and hooves, all on the canvas ({string.Join(", ", missing.Take(5))})", missing.Count == 0);
        // The saddle cloth only once it's yours.
        MountArt.BuildSide(mountCanvas, new MountPose());
        bool bare = !new string(mountCanvas.M).Contains('N');
        MountArt.BuildSide(mountCanvas, new MountPose { Saddle = true });
        Check("the woven saddle cloth is only on your own mount", bare && new string(mountCanvas.M).Contains('N'));
        // Coming toward you it has two eyes; going away, none.
        MountArt.BuildEnd(mountCanvas, new MountPose { Time = 1 }, false, true);
        int eyes = new string(mountCanvas.M).Count(ch => ch == 'E');
        MountArt.BuildEnd(mountCanvas, new MountPose(), true, false);
        int backEyes = new string(mountCanvas.M).Count(ch => ch == 'E');
        MountArt.BuildEnd(mountCanvas, new MountPose(), true, true);
        backEyes += new string(mountCanvas.M).Count(ch => ch == 'E');
        Check($"from the front it looks at you with both eyes, from behind there are none ({eyes}, {backEyes})", eyes == 2 && backEyes == 0);
        // Ridden, its head is low enough from the front that the rider's face shows over it.
        MountArt.BuildEnd(mountCanvas, new MountPose { Ridden = true }, false, true);
        int faceTop = Enumerable.Range(0, MountCanvas.H).First(r => Enumerable.Range(0, MountCanvas.W).Any(i => mountCanvas.M[r * MountCanvas.W + i] != '.')) - MountCanvas.OY;
        var seatF = MountArt.SeatEnd(new MountPose { Ridden = true }, false);
        Check($"ridden toward you, its head sits below the rider's face (head from {faceTop}, rider's chin at {seatF.y - 9})", faceTop > seatF.y - 9);

        // Drawn for real: facing left is the mirror image of facing right, and the outline is coloured, never black.
        var keep = pix;
        var a = new Pix(80, 60) { CamX = 120, CamY = 70 };
        var b = new Pix(80, 60) { CamX = 120, CamY = 70 };
        var poseA = new MountPose { Kind = "stand", Saddle = true };
        pix = a; MountArt.BuildSide(mountCanvas, poseA); BlitMount(mountCanvas, 160, 115, 1);
        pix = b; BlitMount(mountCanvas, 160, 115, -1);
        int mismatched = 0, black = 0, drawn = 0;
        for (int yy = 0; yy < 60; yy++)
            for (int xx = 0; xx < 80; xx++)
            {
                var ca = a.Buf[yy * 80 + xx];
                if (ca.A == 0) continue;
                drawn++;
                if (ca.R < 12 && ca.G < 12 && ca.B < 12) black++;
                int mx = 2 * (160 - a.CamX) - xx;   // mirrored about the feet's column
                if (mx < 0 || mx >= 80) continue;
                var cb = b.Buf[yy * 80 + mx];
                if (ca.R != cb.R || ca.G != cb.G || ca.B != cb.B) mismatched++;
            }
        Check($"facing left is the mirror image of facing right ({mismatched} of {drawn} pixels differ), with no black outline ({black})", drawn > 200 && mismatched == 0 && black == 0);
        // Swimming, what's under the surface is left to the sea: no solid body colour below the waterline on water.
        scene = "world";
        var sea = new Pix(80, 60) { CamX = 1385 - 40, CamY = 398 - 45 };
        for (int sy = 0; sy < 60; sy++) for (int sx = 0; sx < 80; sx++) sea.Buf[sy * 80 + sx] = worldBase.Buf[(sea.CamY + sy) * PW + sea.CamX + sx];
        pix = sea;
        MountArt.BuildSide(mountCanvas, new MountPose { Kind = "swim", Saddle = true });
        BlitMount(mountCanvas, 1385, 398, 1, false, 398 - 6);
        var bodyCols = new[] { "#35a897", "#6fd6c0", "#25837a", "#2f9a8c" }.Select(Pal.C).ToList();
        int solidUnder = 0;
        for (int sy = 398 - 6 - sea.CamY; sy < 60; sy++)
            for (int sx = 0; sx < 80; sx++)
                if (Wet(ShapePx(sea.CamX + sx, sea.CamY + sy)) && bodyCols.Any(c => c.R == sea.Buf[sy * 80 + sx].R && c.G == sea.Buf[sy * 80 + sx].G && c.B == sea.Buf[sy * 80 + sx].B)) solidUnder++;
        Check($"swimming, its body under the surface is left to the sea ({solidUnder} solid pixels under it)", solidUnder == 0);
        // Hurt, it flashes white all over.
        var fl = new Pix(80, 60) { CamX = 120, CamY = 70 };
        pix = fl; MountArt.BuildSide(mountCanvas, new MountPose { Flash = true }); BlitMount(mountCanvas, 160, 115, 1, true);
        Check("a blow makes it flash white", fl.Buf.Count(c => c.A > 0 && c.R == 255 && c.G == 255 && c.B == 255) > 150);
        pix = keep;

        /* ---------- Climbing on and riding ---------- */
        float sx0 = StarwellX - 30, sy0 = StarwellY - 40;
        player.X = sx0; player.Y = sy0; player.Face = "right";
        state.riding = false; state.mountX = sx0 + 12; state.mountY = sy0; yield return 2;
        Check($"beside it, you can climb on (prompt: {prompt.Text})", target?.Type == "ride");
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check($"<ride> puts you in the saddle where it stood, swinging up ({player.X:0}, hop {hopT:0.00})", Riding && Dist(player.X, player.Y, sx0 + 12, sy0) < 1 && hopT > 0 && !hopDown);
        pendingShot = "mount-01-climbing-on"; yield return 2;
        yield return 20;
        // It picks up speed rather than leaping to a gallop, but still covers ground fast.
        float x0 = player.X;
        Inp.Hold(KeyboardKey.Right, true); yield return 4;
        float early = player.X - x0;
        yield return 26;
        float half = player.X - x0;
        Inp.Hold(KeyboardKey.Right, false);
        Check($"it picks up speed: {early:0.0} px in the first four frames, {half:0} in half a second", early < 4 * 92 / 60f && half > 36);
        Check($"at full tilt it gallops, and you lean into it ({RidePose().Kind})", RidePose().Kind == "gallop");
        // Let go and it pulls up within a few pixels.
        float stopFrom = player.X;
        for (int i = 0; i < 30 && rideVel.x > 0; i++) yield return 1;
        yield return 2;
        Check($"let go and it pulls up short ({player.X - stopFrom:0.0} px)", rideVel.x == 0 && player.X - stopFrom < 8 && player.X - stopFrom > 0);
        Check($"stopped, it stands ({RidePose().Kind})", RidePose().Kind == "stand");
        // The saddle rides up and down through the bound, and the rod and hands share it.
        var seatYs = new HashSet<int>();
        mountSpeed = 92;
        for (int f = 0; f < 8; f++) { mountPhase = f / 8f; seatYs.Add(RiderSeat(0, 0).y); }
        mountSpeed = 0; mountPhase = 0;
        Check($"you rise and fall with its stride ({string.Join(",", seatYs)})", seatYs.Count >= 2);

        // Facing only swaps view when one direction clearly wins (no flicker on a diagonal).
        player.X = sx0; player.Y = sy0; player.Face = "right"; yield return 2;
        Inp.ScriptStick = new System.Numerics.Vector2(0.7f, 0.72f); yield return 6;
        bool kept = player.Face == "right";
        Inp.ScriptStick = new System.Numerics.Vector2(0.3f, 0.95f); yield return 4;
        Check($"a near-diagonal keeps it side on, a clear turn swings it round ({player.Face})", kept && player.Face == "down");
        Inp.ScriptStick = null; yield return 10;
        // A gentle push trots.
        player.X = sx0; player.Y = sy0; player.Face = "right"; yield return 2;
        Inp.ScriptStick = new System.Numerics.Vector2(0.42f, 0); yield return 25;
        Check($"a gentle push of the stick trots ({RidePose().Kind}, {mountSpeed:0} px/s)", RidePose().Kind == "trot");
        Inp.ScriptStick = null; yield return 10;

        // Pushing against a palm it doesn't gallop on the spot, and leaves no prints.
        (int px, int py)? palm = null;
        for (int ty = 22; ty <= 38 && palm == null; ty++)
            for (int tx = 112; tx <= 128 && palm == null; tx++)
                if (TileAt(tx, ty) == 'h' && Walkable(TileAt(tx + 1, ty)) && Walkable(TileAt(tx + 2, ty)) && Walkable(TileAt(tx + 3, ty))) palm = (tx, ty);
        if (palm is { } pm)
        {
            player.X = (pm.px + 2) * T + 5; player.Y = pm.py * T + 7; player.Face = "left"; yield return 2;
            Inp.Hold(KeyboardKey.Left, true); yield return 40;
            int prints = trackPrints.Count;
            yield return 20;
            Check($"pushing against a palm it stands its ground rather than galloping in place ({RidePose().Kind}, {mountSpeed:0.0} px/s)", RidePose().Kind == "stand" && trackPrints.Count == prints);
            Inp.Hold(KeyboardKey.Left, false); yield return 2;
        }
        else Note("no palm with open sand beside it found for the wall check");

        // Galloping on sand leaves prints and kicks up sand.
        trackPrints.Clear(); particles.Clear();
        player.X = sx0 - 10; player.Y = sy0; player.Face = "right"; yield return 2;
        Inp.Hold(KeyboardKey.Right, true); yield return 30; Inp.Hold(KeyboardKey.Right, false);
        Check($"galloping over sand leaves hoofprints and kicks up sand ({trackPrints.Count} prints, {particles.Count} grains)", trackPrints.Count >= 2 && particles.Count > 0);
        pendingShot = "mount-02-galloping-on-sand"; yield return 2;
        yield return 15;

        // Over the shoreline at speed, it bounds into the water with a splash, then out again.
        (float x, float y)? edge = null;
        for (int ty = 18; ty <= 28 && edge == null; ty++)
            for (int tx = 104; tx <= 116 && edge == null; tx++)
                if (TileAt(tx, ty) == 'l' && TileAt(tx + 1, ty) == 's' && TileAt(tx + 2, ty) == 's' && TileAt(tx + 3, ty) == 's' && TileAt(tx + 4, ty) == 's' && TileAt(tx, ty - 1) is 'l' && TileAt(tx, ty + 1) is 'l')
                    edge = ((tx + 4) * T + 6, ty * T + 7);
        if (edge is { } ed)
        {
            player.X = ed.x; player.Y = ed.y; player.Face = "left"; wasSwimming = false; yield return 2;
            bool hopped = false, leapt = false;
            Inp.Hold(KeyboardKey.Left, true);
            for (int i = 0; i < 60 && !Swimming; i++) yield return 1;
            for (int i = 0; i < 6; i++) { hopped |= shoreHopT > 0; leapt |= RidePose().Kind == "leap"; if (i == 2) pendingShot = "mount-03-bounding-in"; yield return 1; }
            Inp.Hold(KeyboardKey.Left, false);
            Check($"at speed it bounds over the shoreline into the water ({(hopped ? "bound" : "no bound")}, {(leapt ? "leaping" : "not leaping")})", Swimming && hopped && leapt);
            yield return 25;
            Check($"in the water it swims ({RidePose().Kind})", RidePose().Kind == "swim");
            pendingShot = "mount-04-swimming"; yield return 2;
            hopped = false;
            Inp.Hold(KeyboardKey.Right, true);
            for (int i = 0; i < 60 && Swimming; i++) yield return 1;
            for (int i = 0; i < 4; i++) { hopped |= shoreHopT > 0; yield return 1; }
            Inp.Hold(KeyboardKey.Right, false);
            Check($"and bounds back out onto the sand ({(hopped ? "bound" : "walked out")})", !Swimming && Riding && hopped);
            yield return 20;
        }
        else Note("no lagoon edge found for the shoreline check");

        // Opening the bag (or starting to fish) from the saddle stops it dead: it doesn't set off again with the speed it had.
        player.X = sx0; player.Y = sy0; player.Face = "right"; yield return 2;
        Inp.Hold(KeyboardKey.Right, true); yield return 15;
        TogglePanel("bag"); Inp.Hold(KeyboardKey.Right, false); yield return 2;
        Check($"stopping to open the bag stops it ({rideVel.x:0}, {mountSpeed:0})", mode == "panel" && rideVel.x == 0 && mountSpeed == 0);
        ClosePanels(); yield return 2;
        float still = player.X; yield return 6;
        Check("and it stays put afterwards", player.X == still);

        // Hopping off: down beside it, in a little hop.
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check($"<ride> on dry land hops you down beside it ({hopT:0.00})", !Riding && hopDown && hopT > 0 && Dist(state.mountX, state.mountY, player.X, player.Y) > 4);
        pendingShot = "mount-05-hopping-off"; yield return 2;
        yield return 20;
        // Climbing on, moving finishes the swing up at once.
        Inp.Tap(KeyboardKey.R); yield return 1;
        Inp.Hold(KeyboardKey.Right, true); yield return 3; Inp.Hold(KeyboardKey.Right, false);
        Check("moving straight away finishes climbing on", Riding && hopT == 0);
        Inp.Tap(KeyboardKey.R); yield return 25;

        /* ---------- The whistle ---------- */
        // Waiting on the same island, it runs to you from where it is.
        player.X = sx0 - 60; player.Y = sy0 + 70; player.Face = "down";
        state.mountX = sx0 + 60; state.mountY = sy0; yield return 2;
        float startD = Dist(player.X, player.Y, state.mountX, state.mountY);
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check($"from far off, <ride> whistles instead of teleporting ({call?.Arrival}, {startD:0} px away)", call != null && !Riding && call.Arrival == "run" && whistleT > 0);
        pendingShot = "mount-06-whistle"; yield return 2;
        float oldX = state.mountX, oldY = state.mountY;
        for (int i = 0; i < 40 && call?.Phase != "run"; i++) yield return 1;
        float d0 = Dist(call.X, call.Y, player.X, player.Y);
        yield return 12;
        float d1 = call != null ? Dist(call.X, call.Y, player.X, player.Y) : 0;
        Check($"it comes running ({d0:0} -> {d1:0} px)", call != null && d1 < d0 && state.mountX == oldX);
        // Only the one on its way is drawn: where it was waiting is empty sand now.
        for (int i = 0; i < 60 && call != null && Dist(call.X, call.Y, oldX, oldY) < 40; i++) yield return 1;
        var teal = new[] { "#35a897", "#6fd6c0", "#25837a", "#0f3b40" }.Select(Pal.C).ToList();
        int ghost = 0, seen = 0;
        for (int yy = (int)oldY - 16; yy <= (int)oldY - 6; yy++)
            for (int xx = (int)oldX - 8; xx <= (int)oldX + 8; xx++)
            {
                int vx = xx - camX, vy = yy - camY;
                if (vx < 0 || vy < 0 || vx >= W || vy >= H) continue;
                seen++;
                var c = pix.Buf[vy * W + vx];
                if (teal.Any(t => t.R == c.R && t.G == c.G && t.B == c.B)) ghost++;
            }
        Check($"only the one on its way is drawn, not a second one where it waited ({ghost} of {seen} pixels there are its colours)", seen > 50 && ghost == 0);
        pendingShot = "mount-07-coming"; yield return 2;
        // Saved on its way, it's where it was (the run isn't saved).
        Save();
        var mid = SaveFile.Read(SaveFile.Slot);
        Check($"a save on its way keeps it where it was ({mid.mountX:0},{mid.mountY:0})", Math.Abs(mid.mountX - oldX) < 1 && Math.Abs(mid.mountY - oldY) < 1);
        for (int i = 0; i < 300 && call != null; i++) yield return 1;
        Check($"it arrives beside you and waits ({Dist(state.mountX, state.mountY, player.X, player.Y):0} px, riding: {Riding})", call == null && !Riding && Dist(state.mountX, state.mountY, player.X, player.Y) < 21 && CallCanBe(state.mountX, state.mountY));
        pendingShot = "mount-08-arrived"; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("then <ride> climbs on", Riding);
        Inp.Tap(KeyboardKey.R); yield return 25;

        // You keep walking while it comes: it follows you to where you've got to.
        state.mountX = sx0 + 60; state.mountY = sy0; player.X = sx0 - 50; player.Y = sy0 + 70; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Inp.Hold(KeyboardKey.Left, true); yield return 30; Inp.Hold(KeyboardKey.Left, false);
        for (int i = 0; i < 300 && call != null; i++) yield return 1;
        Check($"walking away while it comes, it still ends up beside you ({Dist(state.mountX, state.mountY, player.X, player.Y):0} px)", call == null && Dist(state.mountX, state.mountY, player.X, player.Y) < 21);

        // <ride> again while it's still coming: once it's close, you climb on there.
        state.mountX = sx0 + 60; state.mountY = sy0; player.X = sx0 - 50; player.Y = sy0 + 70; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check($"too far off yet, <ride> again just says it's coming ({toastMsg})", call != null && !Riding && toastMsg.Contains("on its way"));
        for (int i = 0; i < 300 && call != null && !(call.Phase is "run" or "arrive" && Dist(call.X, call.Y, player.X, player.Y) < 24); i++) yield return 1;
        Inp.Tap(KeyboardKey.R); yield return 3;
        Check("once it's close, <ride> climbs on wherever it has got to", Riding && call == null);
        Inp.Tap(KeyboardKey.R); yield return 25;

        // Too slow (or held up): it steps out of a swirl of sea foam beside you.
        state.mountX = sx0 + 60; state.mountY = sy0; player.X = sx0 - 50; player.Y = sy0 + 70; yield return 2;
        Inp.Tap(KeyboardKey.R);
        for (int i = 0; i < 60 && call?.Phase != "run"; i++) yield return 1;
        call.Total = CallLimit + 0.1f; yield return 3;
        Check($"if it takes too long, sea foam gathers beside you ({call?.Phase})", call?.Phase == "foam");
        pendingShot = "mount-09-foam"; yield return 2;
        for (int i = 0; i < 120 && call != null; i++) yield return 1;
        Check($"and it steps out of it ({Dist(state.mountX, state.mountY, player.X, player.Y):0} px)", call == null && Dist(state.mountX, state.mountY, player.X, player.Y) < 21);

        // Left on another island: it rises out of water you can see.
        state.mountX = 160; state.mountY = 115;
        player.X = 1060; player.Y = 336; player.Face = "left"; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        Check($"left far away, it comes up out of water in view ({call?.Arrival}, from {call?.X:0},{call?.Y:0})",
            call?.Arrival == "breach" && Swimmable(TileUnder(call.X, call.Y)) && MathF.Abs(call.X - player.X) < 160 && MathF.Abs(call.Y - player.Y) < 90);
        for (int i = 0; i < 60 && call?.Phase != "breach"; i++) yield return 1;
        yield return 10;
        pendingShot = "mount-10-breaching"; yield return 2;
        for (int i = 0; i < 360 && call != null; i++) yield return 1;
        Check($"and swims and runs to you ({Dist(state.mountX, state.mountY, player.X, player.Y):0} px)", call == null && Dist(state.mountX, state.mountY, player.X, player.Y) < 21);

        // In a storm it won't come through the deep sea.
        state.weather = "storm"; SnapWeather();
        state.mountX = 160; state.mountY = 115;
        player.X = AtollJettyX + 8; player.Y = AtollJettyY; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        bool deep = call != null && (call.Path.Any(w => TileUnder(w.x, w.y) == '~') || TileUnder(call.X, call.Y) == '~');
        Check($"in a storm its way never crosses the deep sea ({call?.Arrival})", call != null && !deep);
        for (int i = 0; i < 360 && call != null; i++) yield return 1;
        Check("and it still gets to you", call == null && Dist(state.mountX, state.mountY, player.X, player.Y) < 21);
        ClearSkies(); SnapWeather();

        // Going indoors calls it off: it stays where it was.
        state.mountX = sx0 + 60; state.mountY = sy0; player.X = sx0 - 50; player.Y = sy0 + 70; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 20;
        LoadScene("house:tomas"); yield return 2;
        Check($"going indoors calls it off, and it's left where it was ({state.mountX:0},{state.mountY:0})", call == null && state.mountX == sx0 + 60 && state.mountY == sy0);
        LoadScene("world"); player.X = sx0 - 50; player.Y = sy0 + 70; yield return 2;
        // A new game (or loading another) never inherits one.
        Inp.Tap(KeyboardKey.R); yield return 2;
        StartGame(false); yield return 2;
        Check("loading a game drops a whistle still on its way", call == null && whistleT == 0);

        /* ---------- Codex's review ---------- */
        quietWildlife = true; state.riding = false;
        // Every stretch of its way is clear for its whole footprint (it used to cut a diagonal past a palm).
        state.mountX = sx0 + 60; state.mountY = sy0; player.X = sx0 - 50; player.Y = sy0 + 70; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        var blk = CallBlocked(); var sol = Solids().ToList();
        bool clear = call != null;
        for (int i = 0; call != null && i < call.Path.Count; i++)
        {
            var (ax, ay) = i == 0 ? (call.X, call.Y) : call.Path[i - 1];
            clear &= CallClear(ax, ay, call.Path[i].x, call.Path[i].y, blk, sol);
        }
        Check($"every stretch of its way is clear for its whole footprint ({call?.Path.Count} stretches)", clear);
        // Keep walking while it rears on arrival: it comes on after you.
        for (int i = 0; i < 300 && call != null && call.Phase != "arrive"; i++) yield return 1;
        if (call?.Phase == "arrive")
        {
            Inp.Hold(KeyboardKey.Left, true); yield return 25; Inp.Hold(KeyboardKey.Left, false);
            for (int i = 0; i < 300 && call != null; i++) yield return 1;
            Check($"walk on while it rears and it follows you ({Dist(state.mountX, state.mountY, player.X, player.Y):0} px)", call == null && Dist(state.mountX, state.mountY, player.X, player.Y) < 21);
        }
        else Check("it reached the rear on arrival", false);
        // A storm coming on while it's on its way: it takes another way, off the deep sea.
        ClearSkies(); SnapWeather();
        state.mountX = 160; state.mountY = 115; player.X = AtollJettyX + 8; player.Y = AtollJettyY; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        for (int i = 0; i < 60 && call?.Phase is "whistle"; i++) yield return 1;
        state.weather = "storm"; SnapWeather();
        // (A leap already under way finishes first; once it's running it takes the storm into account.)
        for (int i = 0; i < 60 && call?.Phase is "breach"; i++) yield return 1;
        yield return 3;
        bool deepNow = call != null && call.Path.Any(w => TileUnder(w.x, w.y) == '~');
        Check($"a storm coming on while it's on its way sends it another way, off the deep sea ({call?.Phase}, storm {call?.Storm})", call == null || call.Storm && !deepNow);
        ClearSkies(); SnapWeather();
        // Fainting calls it off.
        player.X = sx0 - 50; player.Y = sy0 + 70; state.mountX = sx0 + 60; state.mountY = sy0; call = null; yield return 2;
        Inp.Tap(KeyboardKey.R); yield return 2;
        bool hadCall = call != null;
        Faint(); yield return 2;
        Check("fainting calls a whistle off", hadCall && call == null);
        fade = 0; mode = "play"; state.hp = 100; yield return 60;
        // Opening a panel in the middle of a bound over the shoreline ends the bound (it used to hang in the air).
        state.riding = true; state.mountX = player.X = sx0; state.mountY = player.Y = sy0; yield return 2;
        shoreHopT = 0.2f;
        TogglePanel("bag"); yield return 2;
        Check($"opening a panel mid-bound ends it ({shoreHopT:0.00})", shoreHopT == 0 && RideLift() == 0);
        ClosePanels(); yield return 2;
        // A speckle covered by something drawn later (a palm, a hut) doesn't glow through it.
        mountGlints.Clear();
        mountGlints.Add((camX + 50, camY + 50, false, Pal.C("#fff2c4")));
        pix.CamX = camX; pix.CamY = camY;
        pix.Rect(camX + 50, camY + 50, 1, 1, "#3b2a1d");
        CullMountGlints();
        pix.CamX = pix.CamY = 0;
        Check("a speckle that something covers doesn't glow through it", mountGlints.Count == 0);
        state.riding = false;

        // After dark its speckles and eyes glow.
        SetNight(true); quietWildlife = true;
        state.riding = true; state.mountX = player.X = sx0; state.mountY = player.Y = sy0; player.Face = "right"; yield return 3;
        Check($"after dark its speckles glow ({mountGlints.Count} glints)", mountGlints.Count >= 3);
        // And they really are lit on the screen: bright, where the night darkens everything round them.
        var lit = mountGlints.Select(g => pix.Buf[(g.y - camY) * W + (g.x - camX)]).ToList();
        var dim = pix.Buf[(mountGlints[0].y - camY + 12) * W + (mountGlints[0].x - camX + 14)];
        Check($"lit up on the screen, brighter than the dark round them", lit.Count > 0 && lit.All(c => c.R + c.G + c.B > dim.R + dim.G + dim.B + 150));
        pendingShot = "mount-11-night"; yield return 2;
        SetNight(false);
        state.riding = false;
        yield return 2;
    }

    // FESH_SPRITES: Tidemane at 8x. Rows: the side view facing right (stand, a blink, a toss, the gallop's eight frames),
    // the trot, rearing, lying, leaping and swimming, then the same facing left, then the front and back views.
    void ExportMountSprites(string path)
    {
        var keep = pix;
        const int cw = 60, ch = 54, cols = 12;
        var cells = new List<(MountPose p, int dir, string view)>();
        void Add(MountPose p, int dir = 1, string view = "side") => cells.Add((p, dir, view));
        for (int d = 1; d >= -1; d -= 2)
        {
            Add(new MountPose { Kind = "stand", Time = 0.3f, Saddle = true }, d);
            Add(new MountPose { Kind = "stand", Time = 3.75f, Saddle = true }, d);
            Add(new MountPose { Kind = "stand", Time = 1f, Toss = 0.5f, Saddle = true }, d);
            for (int f = 0; f < 8; f++) Add(new MountPose { Kind = "gallop", Frame = f, Time = f * 0.07f, Saddle = true }, d);
            Add(new MountPose { Kind = "stand", Time = 0.3f }, d);
            for (int f = 0; f < 6; f++) Add(new MountPose { Kind = "trot", Frame = f, Time = f * 0.1f, Saddle = true }, d);
            Add(new MountPose { Kind = "rear", Frame = 0 }, d);
            Add(new MountPose { Kind = "rear", Frame = 1 }, d);
            Add(new MountPose { Kind = "lie" }, d);
            Add(new MountPose { Kind = "leap", Leap = 0.25f }, d);
            Add(new MountPose { Kind = "leap", Leap = 0.75f }, d);
            Add(new MountPose { Kind = "stand", Flash = true }, d);
            for (int f = 0; f < 6; f++) Add(new MountPose { Kind = "swim", Frame = f, Time = f * 0.1f, Saddle = true }, d);
            for (int i = 0; i < 6; i++) Add(new MountPose { Kind = "stand", Time = 99 });
        }
        foreach (bool away in new[] { false, true })
        {
            Add(new MountPose { Kind = "stand", Saddle = true }, 1, away ? "back" : "front");
            for (int f = 0; f < 8; f++) Add(new MountPose { Kind = "gallop", Frame = f, Time = f * 0.07f, Saddle = true }, 1, away ? "back" : "front");
            Add(new MountPose { Kind = "swim", Frame = 0, Saddle = true }, 1, away ? "back" : "front");
            Add(new MountPose { Kind = "swim", Frame = 3, Saddle = true }, 1, away ? "back" : "front");
            Add(new MountPose { Kind = "stand", Time = 99 });
        }
        int rows = (cells.Count + cols - 1) / cols;
        var img = GenImageColor(cw * cols, ch * rows, Color.Black);
        scene = "world";
        for (int i = 0; i < cells.Count; i++)
        {
            var (p, dir, view) = cells[i];
            if (p.Time == 99) continue;
            bool water = p.Kind == "swim";
            float wx = water ? 1385 : 160, wy = water ? 398 : 118;
            pix = new Pix(cw, ch) { CamX = (int)wx - cw / 2, CamY = (int)wy - ch + 12 };
            for (int sy = 0; sy < ch; sy++)
                for (int sx = 0; sx < cw; sx++)
                    pix.Buf[sy * cw + sx] = worldBase.Buf[(pix.CamY + sy) * PW + pix.CamX + sx];
            int wl = water ? (int)wy - 5 : int.MaxValue;
            if (view == "side") { MountArt.BuildSide(mountCanvas, p); BlitMount(mountCanvas, (int)wx, (int)wy, dir, p.Flash, wl); }
            else
            {
                bool away = view == "back";
                MountArt.BuildEnd(mountCanvas, p, away, false); BlitMount(mountCanvas, (int)wx, (int)wy, 1, false, wl);
                MountArt.BuildEnd(mountCanvas, p, away, true); BlitMount(mountCanvas, (int)wx, (int)wy, 1, false, wl);
            }
            for (int sy = 0; sy < ch; sy++)
                for (int sx = 0; sx < cw; sx++) ImageDrawPixel(ref img, (i % cols) * cw + sx, (i / cols) * ch + sy, pix.Buf[sy * cw + sx]);
        }
        ImageResizeNN(ref img, cw * cols * 4, ch * rows * 4);
        ExportImage(img, path.Replace(".png", "-tidemane.png"));
        UnloadImage(img);
        // The gold card's portrait, and its silhouette (the legends page before it's found), at 2x.
        {
            const int pw = 760, ph = 440;
            var card = GenImageColor(pw * 2, ph, Color.Black);
            for (int s = 0; s < 2; s++)
            {
                var g = new VCanvas(pw, ph) { ShadowScale = 2 };
                g.Scale(2);
                CreatureArt.Scene(g, pw / 2f, ph / 2f, "tidemane", s == 1);
                var px = g.ToColors();
                for (int yy = 0; yy < ph; yy++)
                    for (int xx = 0; xx < pw; xx++) ImageDrawPixel(ref card, s * pw + xx, yy, px[yy * pw + xx]);
            }
            ExportImage(card, path.Replace(".png", "-tidemane-card.png"));
            UnloadImage(card);
        }
        pix = keep;
        mountGlints.Clear();
    }
}
#endif
