#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // Each cell is drawn at the same real location, so water clipping and the shared rider/rod anchors run normally.
    // Rows: right, left, up, down. Boat columns: moored, idle, sailing twice, big sail, trolling, fishing, catch.
    // Mount columns: standing, galloping twice, swimming idle/moving twice, fishing, catch.
    void ExportDirectionSprites(string path)
    {
        var keep = pix;
        const int cell = 52, cols = 8, rows = 4;
        state.inv["boat"] = 1; state.tamed = true; scene = "world";
        state.look = new Look { skin = 1, hair = 0, hairColor = 1, hat = 1, shirt = 0, pants = 0 };
        foreach (bool boat in new[] { true, false })
        {
            var img = GenImageColor(cell * cols, cell * rows, Color.Black);
            int r = 0;
            foreach (string face in new[] { "right", "left", "up", "down" })
            {
                for (int c = 0; c < cols; c++)
                {
                    bool water = boat || c >= 3;
                    player.X = water ? 1385 : 160; player.Y = water ? 395 : 115;
                    pix = new Pix(cell, cell) { CamX = (int)player.X - 26, CamY = (int)player.Y - 34 };
                    for (int sy = 0; sy < cell; sy++)
                        for (int sx = 0; sx < cell; sx++)
                            pix.Buf[sy * cell + sx] = worldBase.Buf[(pix.CamY + sy) * PW + pix.CamX + sx];
                    state.aboard = boat; state.riding = !boat;
                    state.weather = "clear"; state.inv.Remove("big_sail");
                    if (boat && c == 4) state.inv["big_sail"] = 1;
                    boatFace = player.Face = face;
                    player.Moving = boat ? c is >= 2 and <= 5 : c is 1 or 2 or 4 or 5;
                    player.WalkT = c is 2 or 5 ? .15f : 0;
                    time = c is 3 or 5 ? .7f : 0;
                    mode = c == 6 ? "waiting" : "play"; sailFurl = c >= 6 ? 1 : 0;
                    heldItem = c == 7 ? "mahi_mahi" : null; heldT = c == 7 ? 2 : 0;
                    trolling = boat && c == 5;
                    float dx = face == "left" ? -1 : face == "right" ? 1 : 0;
                    float dy = face == "up" ? -1 : face == "down" ? 1 : 0;
                    trollDir = (dx, dy); lure = (player.X - dx * 22, player.Y - dy * 22);
                    fish = c == 6 ? new FishCast { Spot = "opensea", Bx = player.X + dx * 22, By = player.Y + dy * 22 } : null;
                    if (boat && c == 0) { state.aboard = false; DrawBoat(player.X, player.Y, time); }
                    else DrawPlayer();
                    if (fish != null) DrawFishing(time);
                    for (int sy = 0; sy < cell; sy++)
                        for (int sx = 0; sx < cell; sx++) ImageDrawPixel(ref img, c * cell + sx, r * cell + sy, pix.Buf[sy * cell + sx]);
                }
                r++;
            }
            ImageResizeNN(ref img, cell * cols * 3, cell * rows * 3);
            ExportImage(img, path.Replace(".png", boat ? "-boat-directions.png" : "-mount-directions.png"));
            UnloadImage(img);
        }
        pix = keep; state.aboard = state.riding = false; state.inv.Remove("big_sail");
        fish = null; heldT = 0; heldItem = null; trolling = false; mode = "title";
    }

    IEnumerable<int> DirectionScript()
    {
        Note("Boat and mount directions through real movement, fishing and pause input");
        Inp.ScriptMouse = Offscreen;
        state = new State { created = true, tamed = true, flags = new Flags { metTomas = true } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true;
        state.inv["boat"] = 1; state.inv["spinner_lure"] = 1;
        state.aboard = true;
        foreach (var (face, key) in new[] { ("up", KeyboardKey.Up), ("down", KeyboardKey.Down), ("left", KeyboardKey.Left), ("right", KeyboardKey.Right) })
        {
            player.X = state.boatX = 1385; player.Y = state.boatY = 395;
            yield return 2;
            Inp.Hold(key, true); yield return 4;
            Check($"steering {face} turns the hull with the fisher", boatFace == face && player.Face == face && player.Moving);
            pendingShot = "direction-boat-" + face; yield return 2;
            Inp.Hold(key, false); yield return 3;
            Check($"stopping preserves the boat's {face} heading", boatFace == face && !player.Moving);
            // Looking over the side while casting must not turn the hull.
            seaSpot = (player.X + 30, player.Y);
            Inp.Hold(KeyboardKey.E, true); yield return 3;
            Check($"charging a cast preserves the boat's {face} heading", mode == "charging" && boatFace == face);
            Inp.Hold(KeyboardKey.E, false); yield return 1;
            for (int i = 0; i < 60 && mode == "casting"; i++) yield return 0;
            if (fish != null) fish.Timer = 99;
            Check($"casting from the {face} boat reaches waiting", mode == "waiting" && boatFace == face && fish != null);
            pendingShot = "direction-fishing-" + face; yield return 2;
            Inp.Tap(KeyboardKey.Escape); yield return 3;
            Inp.Tap(KeyboardKey.F); yield return 3; trollT = 99;
            Inp.Hold(key, true); yield return 24;
            Check($"trolling {face} keeps the lure astern", trolling && boatFace == face &&
                (face == "up" ? lure.y > player.Y : face == "down" ? lure.y < player.Y : face == "left" ? lure.x > player.X : lure.x < player.X));
            pendingShot = "direction-troll-" + face; yield return 2;
            Inp.Hold(key, false); Inp.Tap(KeyboardKey.F); yield return 3;
        }
        TestBite("opensea", "ironbill"); Hook();
        foreach (var (angle, face) in new[] { (-MathF.PI / 2, "up"), (MathF.PI / 2, "down") })
        {
            towAng = angle; reel.Progress = .5f; yield return 2;
            Check($"Ironbill towing {face} turns the bow toward the fish", towing && boatFace == face && player.Face == face);
            pendingShot = "direction-tow-" + face; yield return 2;
        }
        fish = null; reel = null; towing = false; mode = "play"; yield return 3;
        state.aboard = false; state.riding = true;
        foreach (bool water in new[] { false, true })
            foreach (var (face, key) in new[] { ("up", KeyboardKey.Up), ("down", KeyboardKey.Down) })
            {
                player.X = state.mountX = water ? 1385 : 160;
                player.Y = state.mountY = water ? 395 : 115;
                yield return 2;
                float from = player.Y;
                Inp.Hold(key, true); yield return 6;
                Check($"Tidemane {(water ? "swims" : "gallops")} {face}", Riding && Swimming == water && player.Face == face &&
                    (face == "up" ? player.Y < from : player.Y > from));
                pendingShot = $"direction-mount-{(water ? "swim" : "land")}-{face}"; yield return 2;
                Inp.Hold(key, false); yield return 2;
                Inp.Tap(KeyboardKey.Escape); yield return 3;
                Check("riding pauses from its current heading", mode == "pause");
                ClickButton("Resume"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
                Check("Resume keeps the mount direction and releases movement", mode == "play" && Riding && player.Face == face && !player.Moving);
            }
        boatFace = "up"; sailFurl = 0; sprayT = 1;
        StartGame(false); yield return 2;
        Check("loading clears the previous session's boat animation", boatFace == "right" && sailFurl == 1 && sprayT == 0);
    }
}
#endif
