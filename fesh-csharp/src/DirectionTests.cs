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

    // Tools in hand (Tools.cs): rows facing right, left, up and down; columns the axe's wind-up, strike, impact, hold
    // and lowering, the pickaxe up and down, the sword's three frames, a punch, hauling a trap twice, the rake, then the
    // rod waiting, loading a cast, mid-throw and reeling. Written to "-tools" next to the FESH_SPRITES sheet.
    void ExportToolSprites(string path)
    {
        var keep = pix;
        const int cell = 30;
        var cols = new (string tool, float p, string rod)[]
        {
            ("axe", 0, null), ("axe", .22f, null), ("axe", .32f, null), ("axe", .45f, null), ("axe", .75f, null),
            ("pick", 0, null), ("pick", .32f, null), ("sword", .05f, null), ("sword", .2f, null), ("sword", .35f, null),
            ("fist", .45f, null), ("haul", .2f, null), ("haul", .55f, null), ("rake", .4f, null),
            (null, 0, "waiting"), (null, 0, "charging"), (null, 0, "casting"), (null, 0, "reeling")
        };
        scene = "world"; state.aboard = state.riding = false; heldT = 0; iframes = 0;
        state.look = new Look { skin = 1, hair = 0, hairColor = 1, hat = 1, shirt = 0, pants = 0 };
        state.inv["iron_pickaxe"] = 1; state.inv["iron_sword"] = 1; state.inv["rod_copper"] = 1;
        var img = GenImageColor(cell * cols.Length, cell * 4, Color.Black);
        int r = 0;
        foreach (string face in new[] { "right", "left", "up", "down" })
        {
            for (int c = 0; c < cols.Length; c++)
            {
                player.X = 160; player.Y = 115; player.Face = face; player.Moving = false; time = 0.3f;
                pix = new Pix(cell, cell) { CamX = (int)player.X - 15, CamY = (int)player.Y - 23 };
                for (int sy = 0; sy < cell; sy++)
                    for (int sx = 0; sx < cell; sx++)
                        pix.Buf[sy * cell + sx] = worldBase.Buf[(pix.CamY + sy) * PW + pix.CamX + sx];
                var (tool, p, rodMode) = cols[c];
                float dx = face == "left" ? -1 : face == "right" ? 1 : 0, dy = face == "up" ? -1 : face == "down" ? 1 : 0;
                fish = null; reel = null; swingT = 0;
                if (rodMode != null)
                {
                    mode = rodMode; charge = 0.8f;
                    if (rodMode != "charging") fish = new FishCast { Spot = "lagoon", T = rodMode == "casting" ? 0.12f : 1, Bx = player.X + dx * 25, By = player.Y + dy * 25, Tx = player.X + dx * 25, Ty = player.Y + dy * 25 };
                    if (rodMode == "reeling") { reel = new ReelState(); pointerHold = true; }
                }
                else { mode = "play"; Swing(tool, 0.3f); swingT = 0.3f * (1 - p); }
                DrawPlayer();
                pointerHold = false;
                for (int sy = 0; sy < cell; sy++)
                    for (int sx = 0; sx < cell; sx++) ImageDrawPixel(ref img, c * cell + sx, r * cell + sy, pix.Buf[sy * cell + sx]);
            }
            r++;
        }
        // Three sheets, 8x, so single pixels can be judged: swings, more swings, the rod.
        foreach (var (c0, c1, tag) in new[] { (0, 7, "a"), (7, 14, "b"), (14, cols.Length, "c") })
        {
            var part = ImageFromImage(img, new Rectangle(c0 * cell, 0, (c1 - c0) * cell, cell * 4));
            ImageResizeNN(ref part, (c1 - c0) * cell * 8, cell * 4 * 8);
            ExportImage(part, path.Replace(".png", $"-tools-{tag}.png"));
            UnloadImage(part);
        }
        UnloadImage(img);
        pix = keep; fish = null; reel = null; swingT = 0; mode = "title";
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
        foreach (int frames in ToolScript()) yield return frames;
    }

    // Tools in hand (Tools.cs): the arms hold what's swung, the blow lands in front, each job has its tool.
    IEnumerable<int> ToolScript()
    {
        Note("Tools in hand");
        state.riding = state.aboard = false; mode = "play"; fish = null; reel = null;
        state.inv["axe"] = 1; state.inv["iron_pickaxe"] = 1;
        var faces = new[] { "right", "left", "up", "down" };
        var tools = new[] { "axe", "pick", "sword", "fist", "mallet", "rake", "haul", "hands", "dig", "throw" };
        // Every frame of every swing, every facing: the hands stay within an arm's length of the shoulders.
        var stretched = new List<string>();
        foreach (var face in faces)
            foreach (var tool in tools)
                for (float p = 0; p <= 1.001f; p += 0.05f)
                {
                    Swing(tool, 0.3f); swingT = 0.3f * (1 - p);
                    var hp = HandsPose(face, 160, 108, false, 0);
                    if (hp == null) continue;
                    var ns = LookData.NearShoulder(160, 108, face);
                    float d1 = Dist(ns.x, ns.y, hp.Hand.x, hp.Hand.y);
                    float d2 = hp.Hand2 is { } h2 ? Dist(LookData.FarShoulder(160, 108, face).x, LookData.FarShoulder(160, 108, face).y, h2.x, h2.y) : 0;
                    if (d1 > 6.5f || d2 > 7.5f) stretched.Add($"{tool}/{face}/{p:0.00}: {d1:0.0},{d2:0.0}");
                }
        Check($"through every swing the hands stay at arm's length ({stretched.Count} frames don't: {string.Join("; ", stretched.Take(3))})", stretched.Count == 0);
        // The blow lands on the tile in front: one tile ahead from the side, below you from the front.
        (float x, float y) Head(HandPose hp)
        {
            float n = MathF.Sqrt(hp.Dx * hp.Dx + hp.Dy * hp.Dy), len = hp.Length * MathF.Min(1, n);
            return (hp.Hand.x + hp.Dx / n * len, hp.Hand.y + hp.Dy / n * len);
        }
        var landed = new List<string>();
        foreach (var tool in new[] { "axe", "pick" })
            foreach (var face in faces.Where(f => f != "up"))
            {
                Swing(tool, 0.25f); swingT = 0.25f * (1 - 0.35f);
                var head = Head(HandsPose(face, 160, 108, false, 0));
                float ahead = face == "right" ? head.x - 160 : face == "left" ? 160 - head.x : head.y - 108;
                bool ok = face == "down" ? ahead >= 0 && ahead <= 12 : ahead >= 5 && ahead <= 15 && head.y >= 108 - 10;
                if (!ok) landed.Add($"{tool}/{face}: {head.x:0},{head.y:0}");
            }
        Check($"at impact the axe and pickaxe land on the tile in front ({string.Join("; ", landed)})", landed.Count == 0);
        // Seen from behind, a tool raised overhead shows above you; the blow itself is hidden in front of you.
        Swing("axe", 0.25f); swingT = 0.25f;
        var up0 = HandsPose("up", 160, 108, false, 0);
        swingT = 0.25f * 0.65f;
        var up1 = HandsPose("up", 160, 108, false, 0);
        Check($"from behind: raised over your head, then out of sight ahead ({Head(up0).y:0}, behind: {up1.ToolBehind})", Head(up0).y < 108 - 10 && !up0.ToolBehind && up1.ToolBehind);

        // Each job, its tool: chopping an oak, raking salt, a fight with and without a sword.
        var oak = trees.Where(t => t.kind == 't' && t.x < 32 && t.y < 18).Select(t => (x: t.x, y: t.y))
            .FirstOrDefault(t => CanStand(t.x * T - 5, t.y * T + 7) && !state.felled.ContainsKey($"{t.x},{t.y}"));
        player.X = oak.x * T - 5; player.Y = oak.y * T + 7; player.Face = "right"; animals.Clear(); yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check($"chopping a tree swings the axe ({target?.Label}: {swingTool})", swingTool == "axe" && swingT > 0);
        pendingShot = "tools-chop-right"; yield return 2;
        yield return 20;
        player.X = oak.x * T + 5; player.Y = oak.y * T - 3; player.Face = "down"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        pendingShot = "tools-chop-down"; yield return 2;
        yield return 20;
        state.inv.Remove("iron_sword"); state.inv.Remove("iron_pickaxe"); state.inv.Remove("pickaxe");
        var slime = new Monster { Kind = "slime", X = player.X + 8, Y = player.Y, Hp = 99 };
        swingT = 0; Attack(slime);
        Check($"bare-handed, a fight is with your fists ({swingTool})", swingTool == "fist");
        state.inv["iron_pickaxe"] = 1; swingT = 0; Attack(slime);
        Check($"with only a pickaxe, it's the pickaxe ({swingTool})", swingTool == "pick");
        state.inv["iron_sword"] = 1; swingT = 0; Attack(slime);
        var sword = HandsPose("right", 160, 108, false, 0);
        Check($"with a sword, the sword, in its own metal ({swingTool}, {sword?.Tint})", swingTool == "sword" && sword?.Tool == "sword" && sword.Tint == Items.ById["iron_sword"].Tint);
        state.saltDay = 0; ClearSkies(); RakeSalt();
        Check($"raking salt swings a rake ({swingTool})", swingTool == "rake");
        swingT = 0; yield return 2;

        // The rod: held in the hand at the near end, the other hand on the reel, which turns while you reel in.
        player.X = 100; player.Y = 98; player.Face = "up"; yield return 3;
        TestBite("lagoon", "pond_perch"); mode = "waiting"; yield return 2;
        var wait = HandsPose("up", (int)player.X, (int)player.Y, true, 0);
        var tip = RodTip();
        Check($"waiting, the rod runs from your hand to its tip ({wait?.Hand}, {Dist(wait.Hand.x, wait.Hand.y, tip.X, tip.Y):0.0} = {wait.Length:0.0})",
            wait?.Tool == "rod" && MathF.Abs(Dist(wait.Hand.x, wait.Hand.y, tip.X, tip.Y) - wait.Length) < 0.5f);
        mode = "reeling"; reel = new ReelState(); pointerHold = true;
        time = 0; var c0 = HandsPose("right", 160, 108, true, 0).Hand2;
        time = 0.1f; var c1 = HandsPose("right", 160, 108, true, 0).Hand2;
        Check($"reeling, the other hand turns the reel ({c0} -> {c1})", c0 != c1);
        pointerHold = false; mode = "charging"; charge = 0.9f; fish = null;
        var cocked = HandsPose("right", 160, 108, true, 0);
        Check($"loading a cast, the rod hand goes back over your shoulder ({cocked.Hand})", cocked.Hand.x < 160 && cocked.Hand.y < 108 - 8);
        player.Face = "right"; yield return 2;
        pendingShot = "tools-rod-charging"; yield return 2;
        mode = "play"; reel = null; fish = null; yield return 2;

        // Drawn for real: the hand that grips a tool shows on top of it (the tool mustn't paint over the grip).
        var keepPix = pix;
        var skin = Pal.C(LookData.Skins[state.look.skin % LookData.Skins.Length]);
        var hidden = new List<string>();
        foreach (var face in new[] { "right", "left", "down" })
            foreach (var (tool, p, rodMode) in new (string, float, string)[] { ("axe", .32f, null), ("pick", 0, null), ("sword", .2f, null), (null, 0, "waiting") })
            {
                player.X = 160; player.Y = 115; player.Face = face; player.Moving = false; idleT = 0;
                pix = new Pix(40, 40) { CamX = 140, CamY = 92 };
                fish = null; swingT = 0; mode = "play";
                if (rodMode != null) { mode = rodMode; fish = new FishCast { Spot = "lagoon", T = 1, Bx = 185, By = 115, Tx = 185, Ty = 115 }; }
                else { Swing(tool, 0.3f); swingT = 0.3f * (1 - p); }
                DrawPlayer();
                var hp = HandsPose(face, 160, 115, rodMode != null, 0);
                var c = pix.Buf[(hp.Hand.y - pix.CamY) * 40 + hp.Hand.x - pix.CamX];
                if (c.R != skin.R || c.G != skin.G || c.B != skin.B) hidden.Add($"{tool ?? "rod"}/{face}");
            }
        pix = keepPix; fish = null; swingT = 0; mode = "play";
        Check($"the gripping hand shows over the tool ({hidden.Count} hidden: {string.Join(", ", hidden)})", hidden.Count == 0);
    }
}
#endif
