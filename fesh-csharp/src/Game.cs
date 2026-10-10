using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

sealed class Catchable { public string Id, Name; public float Difficulty; public bool Exotic, Odd, Rare, Chest, Boss; }
sealed class Critter { public string Id; public float X, Y, Vx, Vy, T; }
// One cast. Depth is 0 shallow, 1 middle, 2 deep. WaitT counts up while waiting (the ice hole's jig rhythm uses it).
sealed class FishCast
{
    public string Spot, Bait; public float T, Sx, Sy, Tx, Ty, Bx, By, Timer, BiteT, WaitT, Power; public int Depth, Jigs; public bool BigShadow, Spill;
    // A cast you aimed yourself (Hotbar.cs): Spot is the named spot whose water it fell in, and only its everyday fish bite
    // (Wild); Dry, it fell on land; Empty, in water nothing lives in.
    public bool Wild, Dry, Empty;
    public bool Baited => Bait != null; public Catchable Roll;
}
// The reel fight. Style is the fish's fighting style (see CommonFish). Runners build Tension while you hold during a run;
// jumpers Leap (LeapMark sweeps 0..1 and you press in the gold); bottom fish dig in if you hold too long (HoldT).
// Tidemane (Roll.Boss) changes style every StyleT seconds, and Pull scales how fast the catch meter fills.
sealed class ReelState
{
    public float ZoneH, ZoneY, ZoneV, FishY, FishTarget, FishTimer, Progress, Diff, Tick;
    public bool Exotic, Inside, Perfect, LeapDone, Digging; public Catchable Roll;
    public string Style = "dart"; public float Tension, RunT, Running, LeapT, Leap, LeapLen = 1, LeapMark, HoldT, StyleT, Pull = 1;
    public float AttackTimer = 2.5f, AttackWarning, DuckTime;
}
sealed record Say(string S, string T);
sealed class DialogueState { public List<Say> Lines; public int I; public float Shown; public string Full = ""; public Action OnDone; }
// Something the player can act on with E, and optionally second actions with F (cook, throw chum) and G (spearfish).
// RideLabel is what the ride key does here (landing the boat while E fishes, for one).
sealed class Target { public string Type, Id, Label, AltType, AltLabel, Alt2Type, Alt2Label, RideLabel; public int Tx, Ty; public object Ref; }
sealed class Ghost { public int Tx, Ty; public Build Target; public string Reason = ""; }
sealed class Actor { public float X, Y, WalkT; public string Face = "down"; public bool Moving; }

partial class Game
{
    // W x H is the view; the world is COLS x ROWS tiles of T pixels.
    const int W = 320, H = 180, T = 10, COLS = 252, ROWS = 76;
    const float FireX = 185, FireY = 86, TomasHomeX = 146, TomasHomeY = 82, Reach = 30;
    const int MaxBuilds = 150;
    static readonly HashSet<char> Buildable = new() { 's', 'g', 'n', 'e', 'D', 'j' };
    static readonly HashSet<char> WoodGround = new() { 's', 'e' };
    static readonly Dictionary<string, int> LooseMax = new() { ["wood"] = 5, ["stone"] = 4, ["worm"] = 3, ["glean"] = 5 };
    static readonly string[] FishingModes = { "charging", "casting", "waiting", "bite", "reeling", "chest", "spear", "drill" };
    // Modes where the world keeps going: food, health, monsters and the derby clock all tick.
    static readonly string[] ActiveModes = { "play", "build", "charging", "casting", "waiting", "bite", "reeling", "chest", "spear", "drill" };

    State state = new();
    readonly Random rng = new();
    float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float Dist(float ax, float ay, float bx, float by) => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    readonly Actor player = new() { X = 160, Y = 115 };
    float tomasX = TomasHomeX, tomasY = TomasHomeY;

    string mode = "title";
    float time;
    float idleT;   // seconds standing about in play (for glances and stretches)
    Target target;
    float fade, fadeDir;
    Action fadeCb, fadeAfter;
    FishCast fish;
    ReelState reel;
    DialogueState dlg;
    double catchOpenedAt;
    string catchId, panel;
    bool pointerHold, hasSave, quit;
    string pausedFrom = "play";   // the mode the menu goes back to
    bool padWas;                  // a gamepad was plugged in last frame
    bool waitRelease;             // after Resume, ignore the act button until it has been let go
    float saveTimer, looseTimer;
    string toastMsg = "";
    float toastTimer, toastAlpha;
    (string Key, string Text, bool Urgent) prompt;
    string buildTool = "fence";
    (int tx, int ty)? hover;
    Ghost ghost;
    Dictionary<(int, int), Build> buildIndex = new();
    Texture2D worldTex;
    Pix pix = new(W, H);
    Pix basePix = new(COLS * T, ROWS * T);
    int camX, camY;
    readonly List<Critter> critters = new();
    string oddId, oddSpot, dexTab = "creatures";
    byte lastBiome;

    public void Run()
    {
        Settings.Load();
        // Not in a test run: one started without FESH_SAVE would move the real save before it refuses to go on.
        if (Environment.GetEnvironmentVariable("FESH_AUTOTEST") is not { Length: > 0 }) SaveFile.MigrateLegacy();
        SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        SetTraceLogLevel(TraceLogLevel.Error);
        InitWindow(1280, 720, "Fesh");
        FitWindowToMonitor();
        SetWindowMinSize(640, 360);
        if (Settings.Data.fullscreen) ToggleBorderlessWindowed();
        SetExitKey(KeyboardKey.Null);
        SetTargetFPS(60);
        var icon = IconImage(64);
        SetWindowIcon(icon);
        UnloadImage(icon);
        Sfx.Init();
        Music.Init();
        Gfx.Init();
        worldTex = Gfx.ToTexture(new Color[W * H], W, H, TextureFilter.Point);
        state.FixClock();   // the title screen shows the island at 8 AM
        BuildMap();
        hasSave = SaveFile.Any();
        AutoTestStart();

        while (!quit && !WindowShouldClose())
        {
            float dt = Math.Min(0.05f, GetFrameTime());
            // Menus, panels and the title take a gamepad pointer (see Inp).
            Inp.BeginFrame(dt, mode is "title" or "pause" or "panel" or "create" or "ending");
            Gfx.BeginFrame();
            AutoTestTick();
            CheckAutoPause();
            HandleKeys();
            HandlePointer();
            Update(dt);
            PickMusic(dt);
            RenderWorld(time);
            UpdateTexture(worldTex, pix.Buf);
            BeginDrawing();
            ClearBackground(Pal.Page);
            DrawTexturePro(worldTex, new Rectangle(0, 0, W, H), Gfx.S(0, 0, Gfx.LW, Gfx.LH), Vector2.Zero, 0, Color.White);
            DrawUi();
            DrawPadPointer();
            AutoTestAfterDraw();
            EndDrawing();
            Inp.EndFrame();
        }
        if (mode != "title") Save();
        UnloadTexture(worldTex);
        if (mapTex.Id != 0) UnloadTexture(mapTex);
        if (previewTex.Id != 0) UnloadTexture(previewTex);
        foreach (var t in slotTex) if (t.Id != 0) UnloadTexture(t);
        AnimalArt.Shutdown();
        ItemArt.Shutdown();
        FishArt.Shutdown();
        Gfx.Shutdown();
        Music.Shutdown();
        Sfx.Shutdown();
        CloseWindow();
    }

    // Each island has its own tune; the cave and houses have theirs. Rain adds its own patter outdoors.
    string WantedTrack() => mode is "title" or "create" ? "saltmere" : BossFighting || BossOnLine || GuardianFighting ? "boss" : SeaEmergency && evac.Phase != "quake" || BagaEvacuating ? "alarm" : eclipse != null ? "eclipse" : scene == "cave" ? "cave" : InHouse ? "home"
        : RondallaPlaying ? "rondalla" : Data.Biomes[lastBiome < Data.Biomes.Length ? lastBiome : PlayerBiome()].Id;

    void PickMusic(float dt)
    {
        // lastBiome only changes on land, so crossing a bridge or standing on a jetty keeps the island's tune.
        string track = WantedTrack();
        Music.Want(track, Night && mode is not ("title" or "create") ? 0.3f : 0.42f);
        // The rain loop swells and fades with the rain on screen.
        bool wet = scene == "world" && rainAmt > 0.02f && mode is not ("title" or "create");
        Music.WantAmbience(wet ? "rain" : null, (0.32f + 0.23f * stormAmt) * rainAmt);
        PickNature();
        // Paused because you switched away: quiet until you come back.
        Music.Hushed = mode == "pause" && !Inp.Focused && Settings.Data.pauseUnfocused;
        Music.Update(dt);
    }

    // The time of day outdoors: a dawn chorus that thins to the odd bird by day, then crickets once it gets dark.
    // Rain hushes them, and Frostfang has no crickets and fewer birds.
    void PickNature()
    {
        string name = null;
        float v = 0;
        if (scene == "world" && mode is not ("title" or "create"))
        {
            float c = state.clock, dry = (1 - 0.75f * rainAmt) * (1 - stormAmt);
            float birds = c >= 5 * 60 && c < 8 * 60 ? Math.Min(1, (c - 5 * 60) / 60) : c >= 8 * 60 && c < 10 * 60 ? 1 - 0.7f * (c - 8 * 60) / 120 : c >= 10 * 60 && c < 19 * 60 ? 0.3f : 0;
            float crickets = Darkness;
            if (lastBiome == 1) { crickets = 0; birds *= 0.4f; }
            if (birds >= crickets && birds > 0.02f) { name = "birds"; v = 0.5f * birds * dry; }
            else if (crickets > 0.02f) { name = "crickets"; v = 0.42f * crickets * dry; }
        }
        Music.WantNature(name, v);
    }

    static Image IconImage(int size)
    {
        var img = GenImageColor(size, size, Color.Blank);
        var px = IconArt.Pixels(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var (r, g, b, a) = px[y * size + x];
                ImageDrawPixel(ref img, x, y, new Color(r, g, b, a));
            }
        return img;
    }

    static void FitWindowToMonitor()
    {
        int mon = GetCurrentMonitor(), mw = GetMonitorWidth(mon), mh = GetMonitorHeight(mon);
        if (mw <= 0 || mh <= 0 || (mw >= 1360 && mh >= 840)) return;
        float k = Math.Min((mw - 80) / 1280f, (mh - 140) / 720f);
        int w = (int)(1280 * k), h = (int)(720 * k);
        var pos = GetMonitorPosition(mon);
        SetWindowSize(w, h);
        SetWindowPosition((int)pos.X + (mw - w) / 2, (int)pos.Y + (mh - h) / 2);
    }

    partial void AutoTestStart();
    partial void AutoTestTick();
    partial void AutoTestAfterDraw();

    /* ---------- Save ---------- */
    // Writes the game in progress to its slot. False if there's no game to save or the file couldn't be written.
    bool Save()
    {
        if (mode == "title" || mode == "create" && creatorFor == "new") return false;
        // Exactly where you are: rounding could put a hull's edge over a dock, or the feet of someone pushed against
        // the water onto the water tile, and then StartGame wouldn't accept the spot on loading.
        state.px = player.X;
        state.py = player.Y;
        if (Aboard) { state.boatX = player.X; state.boatY = player.Y; }
        state.scene = scene;
        // During the evacuation the guide follows its own goal; the save keeps the one you chose (RestlessSea.cs, Codex).
        string track = state.track;
        if (trackBeforeSea != null) state.track = trackBeforeSea;
        bool ok = SaveFile.Write(state);
        state.track = track;
        return ok;
    }

    /* ---------- Input ---------- */
    // Actions go through Bind (rebindable in the menu); Esc, Enter and the number keys are fixed.
    void HandleKeys()
    {
        if (rebind != null) { CaptureRebind(); return; }
        // While you type your fisher's name, a letter bound to fullscreen types the letter instead.
        if (mode == "create" || mode == "title" && renameSlot > 0 ? Bind.PressedNotTyping("fullscreen") : Bind.Pressed("fullscreen")) ToggleFullscreen();
        if (mode == "create") { CreatorKeys(); return; }
        if (mode == "title") { TitleKeys(); return; }
        if (mode == "ending") { if (Inp.Pressed(KeyboardKey.Enter)) KeepFishing(); return; }
        if (mode == "pause") { if (BackPressed()) ClosePause(); return; }
        if (Inp.PadPressed(GamepadButton.MiddleRight) && ActiveModes.Contains(mode)) { OpenPause(anyMode: true); return; }
        // On a gamepad, the bumpers pick the piece to build (they open the tackle box and map otherwise).
        if (mode == "build" && (Inp.PadPressed(GamepadButton.LeftTrigger1) || Inp.PadPressed(GamepadButton.RightTrigger1)))
        {
            var tools = BuildTools();
            int at = Array.IndexOf(tools, buildTool), step = Inp.PadPressed(GamepadButton.RightTrigger1) ? 1 : tools.Length - 1;
            SelectTool(tools[(Math.Max(at, 0) + step) % tools.Length]);
            return;
        }
        if (Bind.Pressed("act") || Inp.Pressed(KeyboardKey.Enter))
        {
            if (mode == "reeling") reelTap = true;
            OnAction();
        }
        else if (HotbarKeys()) { }
        else if (mode == "build" && Bind.Pressed("remove")) SelectTool("remove");
        else if (mode == "build" && DigitPressed() is int d && d < BuildTools().Length) SelectTool(BuildTools()[d]);
        else if (Bind.Pressed("alt")) OnAlt();
        else if (Bind.Pressed("spear")) OnAlt2();
        else if (Bind.Pressed("tackle")) TogglePanel("tackle");
        else if (Bind.Pressed("build")) ToggleBuild();
        else if (Bind.Pressed("bag")) TogglePanel("bag");
        else if (Bind.Pressed("dex")) TogglePanel("dex");
        else if (Bind.Pressed("case")) TogglePanel("case");
        else if (Bind.Pressed("map")) TogglePanel("map");
        else if (Bind.Pressed("journal")) TogglePanel("journal");
        else if (Bind.Pressed("mute")) ToggleSound();
        else if (Bind.Pressed("ride")) ToggleRide();
        else if (BackPressed())
        {
            // A card open over the Fish log, the album or the island guide closes back to its page first.
            if (mode == "panel" && panel is ("dex" or "album" or "atlas") && (dexFish != null || dexExtra != null)) { dexFish = null; dexExtra = null; }
            else if (mode == "panel" && panel == "atlas" && atlasZoom) atlasZoom = false;
            else if (mode == "panel" && panel == "sungka") CloseSungka();
            else if (mode == "panel" && panel == "tides") CloseTides();
            else if (mode == "panel" && panel == "lahar") CloseLaharWatch();
            else if (mode == "panel" && panel is "readings" or "gobag") { readings = null; gobag = null; ClosePanels(); }
            else if (mode == "panel") ClosePanels();
            else if (mode == "catch") CloseCatch();
            else if (mode == "odd") CloseOdd();
            else if (mode == "legend") CloseLegend();
            else if (mode == "tamed") CloseTamed();
            else if (mode == "moon") CloseMoon();
            else if (mode == "platejaw") CloseGuardianCard();
            else if (mode == "seacard") CloseSeaCard();
            else if (mode == "magayoncard") CloseMagayonCard();
            else if (mode == "build") ExitBuild();
            else if (mode is "waiting" or "charging") ReelIn("You reeled in.");
            else if (mode == "spear") EndSpear();
            else if (mode == "drill") { mode = "play"; Toast("You stop chipping at the ice."); }
            else if (mode == "play") OpenPause();
        }
    }

    // Esc, or Start on a gamepad, or B anywhere but plain play (where Start is the way into the menu).
    bool BackPressed() => Inp.Pressed(KeyboardKey.Escape) || Inp.PadPressed(GamepadButton.MiddleRight) || mode != "play" && Inp.PadPressed(GamepadButton.RightFaceRight);

    // 1-9 pick the first nine pieces in the build bar, and 0 the tenth.
    static int? DigitPressed()
    {
        for (int i = 0; i < 9; i++) if (Inp.Pressed((KeyboardKey)((int)KeyboardKey.One + i))) return i;
        if (Inp.Pressed(KeyboardKey.Zero)) return 9;
        return null;
    }

    bool ReelHeld()
    {
        bool held = Bind.Down("act") || Bind.Down("use") || Inp.Down(KeyboardKey.Enter) || pointerHold;
        if (waitRelease && !held) waitRelease = false;
        return held && !waitRelease;
    }

    (int tx, int ty) PointerTile() => ((int)MathF.Floor((Gfx.Mouse.X / 4 + camX) / T), (int)MathF.Floor((Gfx.Mouse.Y / 4 + camY) / T));

    // Clicks on the world itself (not on a button or panel).
    void HandlePointer()
    {
        if (!Gfx.Down) pointerHold = false;
        if (mode == "build" && Gfx.MouseMoved) hover = Gfx.OverUiPrev ? null : PointerTile();
        if (!Gfx.Pressed || Gfx.OverUiPrev) return;
        switch (mode)
        {
            case "build": hover = PointerTile(); BuildAction(); return;
            case "reeling": pointerHold = true; reelTap = true; return;
            case "spear": AimAtPointer(); ThrowSpear(); return;
            case "waiting": case "bite": case "dialogue": case "drill":
                OnAction();
                if (mode == "reeling") pointerHold = true;
                return;
            case "play":
                // Holding a tool, a click uses it (on what's in front, if that's what it's for); otherwise it acts as E.
                if (HeldItem != null && (target == null || target.Type is "spot" or "tree" or "node" or "drill" or "monster" or "boss" or "guardian"))
                {
                    UseHeld();
                    if (mode == "charging") pointerHold = true;
                    return;
                }
                if (target == null) return;
                OnAction();
                if (mode == "charging") pointerHold = true;
                return;
        }
    }

    /* ---------- UI state helpers ---------- */
    // Key tokens like <act> in toasts, prompts and dialogue become whatever that action is bound to (Bind.Fix).
    void Toast(string msg, float secs = 2.6f) { toastMsg = Bind.Fix(msg); toastTimer = secs; }
    // Text in [brackets] is drawn as a key badge.
    void SetPrompt(string text, string key = null, bool urgent = false) => prompt = (Bind.Fix(key), Bind.Fix(text), urgent);

    string AreaName()
    {
        if (scene == "house:tomas") return "Tomas's hut";
        if (scene == "house:school") return "The school (evacuation centre)";
        if (InHouse) return "Your shack";
        if (scene == "cave") return caveFloor == AncientFloor ? "The Ancient Floor" : $"Caverns, floor {caveFloor}";
        float x = player.X, y = player.Y;
        if (Dist(x, y, PipX, PipY + 12) < 22) return "Pip's stall";
        if (Dist(x, y, SaltJettyX - 10, SaltJettyY) < 22) return "Pip's jetty";
        if (Dist(x, y, AtollJettyX + 20, AtollJettyY) < 34) return "Atoll jetty";
        char tile = TileAt((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1.5f) / T));
        if (race != null) return $"Regatta · {RaceTime(race.T)}";
        if (InAmihan && InSanctuary(x, y)) return Aboard ? "Marine sanctuary · at the helm" : "Marine sanctuary";
        if (Aboard) return InAmihan ? "Amihan sea · at the helm" : InHabagat ? "Habagat sea · at the helm" : "Open sea · at the helm";
        if (InAmihan && !WaterTile(tile)) return AmihanIslands.OrderBy(i => Dist(x, y, i.cx * T, i.cy * T)).First().name;
        if (InHabagat && !WaterTile(tile))
        {
            if (new Box(SaltBedX - 20, SaltBedY - 20, SaltBedW + 40, SaltBedH + 40).Overlaps(new Box(x - 3, y - 3, 6, 3))) return "Asinan salt beds";
            if (Dist(x, y, LighthouseX, LighthouseY) < 50) return "Parola lighthouse";
            string r = HabagatRegion((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1.5f) / T));
            return r.StartsWith("habagat:islet:") ? "Daang Pulo" : r[8..];
        }
        if (Riding && tile == '~' && Dist(x, y, StarwellX, StarwellY) > 40) return "Open sea";
        var b = Data.Biomes[PlayerBiome()];
        if (b.Id == "saltmere")
        {
            if (Dist(x, y, 100, 66) < 46) return "The lagoon";
            if (x > 248 && y > 82 && y < 108) return "The old dock";
            if (x < 92 && y > 128 && y < 180) return "The old wreck";
            if (y < 46 && x > 165 && x < 320) return "Rocky shore";
            if (Dist(x, y, 166, 78) < 34) return "Tomas's camp";
        }
        if (tile is 'd' or 'b') return "Old bridge";
        var near = Data.Spots.Where(s => s.Scene == "world" && s.Biome == b.Id && b.Id != "saltmere" && Dist(x, y, s.X, s.Y) < s.R + 14)
            .OrderBy(s => Dist(x, y, s.X, s.Y)).FirstOrDefault();
        return near?.Label ?? b.Name;
    }

    bool SpotOpen(string id) => id switch
    {
        "wreck" => state.flags.tideOut,
        "deep" => state.flags.dockFixed,
        "bagareef" => !BagaClosed,
        _ => true
    };

    /* ---------- Dialogue ---------- */
    void Talk(List<Say> lines, Action onDone = null)
    {
        dlg = new DialogueState { Lines = lines, OnDone = onDone };
        mode = "dialogue";
        SetPrompt("");
        ShowLine();
    }

    void ShowLine()
    {
        dlg.Full = Bind.Fix(dlg.Lines[dlg.I].T);
        dlg.Shown = 0;
    }

    void AdvanceDialogue()
    {
        if (dlg == null) return;
        if (dlg.Shown < dlg.Full.Length) { dlg.Shown = dlg.Full.Length; return; }
        dlg.I++;
        if (dlg.I >= dlg.Lines.Count)
        {
            var cb = dlg.OnDone;
            dlg = null;
            mode = "play";
            cb?.Invoke();
        }
        else ShowLine();
    }

    void FadeThrough(Action cb, Action after)
    {
        mode = "fade"; fadeDir = 1; fadeCb = cb; fadeAfter = after;
        SetPrompt("");
    }

    /* ---------- Tomas ---------- */
    void TalkTomas()
    {
        bool Has(string id) => state.Caught(id);
        Say Tm(string t) => new("Tomas", t);
        if (state.flags.ended)
        {
            Talk(new() { Tm("The water feels calmer now. Fish as long as you like, friend.") });
        }
        // The first conversation is always the introduction, even if you've already caught something strange
        // (before 1.16 an early Glowgill skipped it, and Tomas never asked for anything).
        else if (!state.flags.metTomas) MeetTomas();
        else if (Has("mirror_ray"))
        {
            Talk(new()
            {
                Tm("Play that recording again..."),
                Tm("She sank the ship herself. To send them home."),
                Tm("That's my Mara. Stubborn as the tide."),
                Tm("There's an old island saying: the deepest things only rise at night. Try the end of the dock after dark.")
            });
        }
        else if (Has("hollow_eel"))
        {
            if (!state.flags.dockFixed)
            {
                Talk(new()
                {
                    Tm("Let me see that card."),
                    Tm("Dr. Mara Ilao..."),
                    Tm("Mara is my daughter. She signed on with the Halcyon three years ago. She never came home."),
                    Tm("If she left something out there, I want to know. I'll patch up the old dock so you can reach the deep water.")
                }, () => FadeThrough(() => { state.flags.dockFixed = true; BuildMap(); Save(); }, () => Toast("Tomas repaired the old dock, east of camp.", 3.5f)));
            }
            else Talk(new() { Tm("The dock should hold now. Something wide and silver glides past the end of it, mostly in daylight.") });
        }
        else if (Has("tidecrawler"))
        {
            Talk(new()
            {
                Tm("M.I.? Those initials..."),
                Tm("Never mind me. The tide's out, so you can walk to the old wreck off the southwest shore."),
                Tm("Whatever lives in that hull, go gently with it.")
            });
        }
        else if (Has("glowgill"))
        {
            Talk(new()
            {
                Tm("A tag? \"Property of R/V Halcyon\"..."),
                Tm("So that ship was carrying more than research gear."),
                Tm("Something's been clicking around the rocky shore up north. Only in daylight. Might be worth a cast.")
            });
        }
        else Talk(new() { Tm("The lagoon glows after dark. Rest by the fire if you want to wait for night.") });
    }

    void MeetTomas()
    {
        Say Tm(string t) => new("Tomas", t);
        state.flags.metTomas = true;
        Save();
        Talk(new()
        {
            Tm($"Ahoy there. You must be {state.look.name}, the new fisher. I'm Tomas, I keep the camp on Saltmere."),
            Tm("Fair warning: the fish here have been strange since the storm three years back. The night the Halcyon went down."),
            Tm(state.Caught("glowgill") ? "And you've already hooked one of the strange ones, I hear. Come and tell me what you find." : "Folks say something glows in the lagoon after dark. Rest by my campfire if you want to wait for night."),
            Tm("And my hut's open, if you need the workbench or the stove. Keep yourself fed out there."),
            Tm("Pip has a stall just east of here, too. Pip pays good coin for fish.")
        }, () => Toast("Tip: <bag> opens your bag, <dex> your Fesh-dex and <case> the case board. <journal> opens your journal.", 5));
    }

    // Tomas's requests ride along with whatever he has to say about the mystery.
    void TalkTomasWithRequests()
    {
        if (!state.flags.metTomas || (state.Caught("hollow_eel") && !state.flags.dockFixed)) { TalkTomas(); return; }
        if (CanHandIn) { HandIn(); return; }
        TalkTomas();
        if (dlg != null && RequestLine() is Say line) dlg.Lines.Add(line);
        // And, once a day, what tomorrow's weather will do.
        if (dlg != null && !KnowTomorrow)
        {
            dlg.Lines.Add(new Say("Tomas", $"Tomorrow? My old knee says {DescribeDay(state.tomorrow)}.{SeasonTomorrow()}"));
            TellTomorrow();
        }
    }

    /* ---------- Odd catches ---------- */
    void ShowOdd(string id, string spot)
    {
        oddId = id;
        oddSpot = spot;
        catchOpenedAt = GetTime();
        mode = "odd";
        SetPrompt("");
    }

    // The animal scrambles ashore and runs off, away from the water it came out of.
    void CloseOdd()
    {
        if (mode != "odd" || GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        var s = Data.SpotById[oddSpot];
        float dx = player.X - s.X, dy = player.Y - s.Y, len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        dx /= len; dy /= len;
        float sx = player.X + dx * 8, sy = player.Y + dy * 8;
        if (!CanStand(sx, sy)) { sx = player.X; sy = player.Y; }
        critters.Add(new Critter { Id = oddId, X = sx, Y = sy, Vx = dx * 34, Vy = dy * 34 });
        Toast(Data.OddById[oddId].After, 3.5f);
    }

    void UpdateCritters(float dt)
    {
        for (int i = critters.Count - 1; i >= 0; i--)
        {
            var c = critters[i];
            c.T += dt;
            if (c.T > 5) { critters.RemoveAt(i); continue; }
            float nx = c.X + c.Vx * dt, ny = c.Y + c.Vy * dt;
            if (CanStand(nx, ny)) { c.X = nx; c.Y = ny; }
            else if (CanStand(c.X - c.Vy * dt, c.Y + c.Vx * dt)) (c.Vx, c.Vy) = (-c.Vy, c.Vx);
            else (c.Vx, c.Vy) = (c.Vy, -c.Vx);
        }
    }

    void MaybeHint(string spot)
    {
        var cr = Data.Creatures.FirstOrDefault(c => c.Spot == spot && !state.Caught(c.Id));
        if (cr == null || Eligible(cr)) return;
        bool locked = cr.Req != null && !state.Caught(cr.Req);
        string msg = locked ? cr.LockedHint : cr.TimeHint;
        if (msg == null) return;
        string key = cr.Id + (locked ? ":lock" : ":time");
        if (state.Hinted(key)) return;
        state.hinted[key] = true;
        Save();
        Talk(new() { new Say("You", msg) });
    }

    /* ---------- Catch card ---------- */
    void ShowCatch(string id)
    {
        catchId = id;
        catchOpenedAt = GetTime();
        mode = "catch";
        SetPrompt("");
    }

    void CloseCatch()
    {
        if (mode != "catch" || GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        AfterCatch(catchId);
    }

    void AfterCatch(string id)
    {
        Say Y(string t) => new("You", t);
        switch (id)
        {
            case "glowgill":
                Talk(new() { Y("A tag on its fin: \"Property of R/V Halcyon.\" Tomas might know about that ship.") });
                break;
            case "tidecrawler":
                Talk(new() { Y("A logbook page signed \"M.I.\" Someone on that ship was protecting these creatures."), new Say("", "The water around the island starts to drain away. The tide is going out.") },
                    () => FadeThrough(() => { state.flags.tideOut = true; BuildMap(); Save(); },
                        () => Talk(new() { Y("The tide pulled back. There's a path to the old wreck now, off the southwest shore.") })));
                break;
            case "hollow_eel":
                Talk(new() { Y("A key card: \"Dr. Mara Ilao, Chief Biologist.\" I should show this to Tomas.") });
                break;
            case "mirror_ray":
                Talk(new() { Y("She opened the valves herself... she sank the Halcyon on purpose. Tomas needs to hear this.") });
                break;
            case "abyssal":
                Ending();
                break;
        }
    }

    /* ---------- Ending ---------- */
    string endStats = "";

    void Ending()
    {
        tomasX = 270; tomasY = 95;
        Talk(new()
        {
            new("", "The Abyssal rests at the surface, watching you with dozens of soft, glowing eyes."),
            new("", "Boards creak behind you. Tomas has followed the light down the dock."),
            new("Tomas", "That locket... I gave it to Mara the day she left."),
            new("Tomas", "She always said the sea keeps what it loves. Looks like it kept her promise too."),
            new("Tomas", "Let it go home, friend."),
            new("", "You release the Abyssal. It sinks toward the trench, and one by one, small lights rise from the deep to follow it.")
        }, () =>
        {
            state.flags.ended = true;
            Save();
            int commons = state.commons.Values.Sum();
            endStats = $"Creatures found: 5 of 5. Common fish caught: {commons}. Casts: {state.casts}. Things built: {state.builds.Count}.";
            mode = "ending";
            SetPrompt("");
        });
    }

    void KeepFishing()
    {
        tomasX = TomasHomeX; tomasY = TomasHomeY;
        mode = "play";
        Toast("The case is closed, but the sea isn't. Your journal (<journal>) has what's next.", 5);
    }

    /* ---------- Panels and menus ---------- */
    void TogglePanel(string which)
    {
        if (mode == "panel") { ClosePanels(); return; }
        if (mode == "build") ExitBuild();
        if (mode != "play")
        {
            if (FishingModes.Contains(mode)) Toast("Finish fishing first.");
            return;
        }
        Sfx.Play("ui");
        panel = which;
        mode = "panel";
        if (which == "map") ChooseChart();
        if (which is "dex" or "map") Learned(which);
        SetPrompt("");
    }

    void ClosePanels()
    {
        // A crab still on the sorting tray goes the safe way (Release.cs); a lesson left halfway is just left.
        if (panel == "sort") FinishSort();
        if (panel == "quiz") { quiz = null; lessonPrizes = null; }
        // Baga's panels (Magayon.cs): the lahar watch always ends with the warning out, by you or by Ben.
        bool lahar = panel == "lahar";
        if (panel is "readings" or "gobag") { readings = null; gobag = null; }
        riddle = null;
        panel = null;
        dexFish = null;
        dexExtra = null;
        atlasZoom = false;
        if (mode == "panel") mode = "play";
        if (lahar) LaharWatchClosed();
    }

    string craftStation;

    void OpenCraft(string station)
    {
        craftStation = station;
        Sfx.Play("ui");
        panel = "craft";
        mode = "panel";
        SetPrompt("");
    }

    void ToggleSound()
    {
        Sfx.Muted = Settings.Data.muted = !Sfx.Muted;
        Settings.Save();
    }

    void ToggleFullscreen()
    {
        ToggleBorderlessWindowed();
        Settings.Data.fullscreen = IsWindowState(ConfigFlags.BorderlessWindowMode);
        Settings.Save();
    }

    // Esc pauses from play; Start on a gamepad pauses from any mode where time runs. Losing the window's focus
    // (Settings: on by default) pauses from those too, even mid-cast or mid-fight, and Resume goes straight back.
    void OpenPause(bool auto = false, bool anyMode = false)
    {
        if (mode != "play" && !((auto || anyMode) && ActiveModes.Contains(mode))) return;
        if (!auto) Sfx.Play("ui");
        // A power cast being held is dropped: the key or button let go while you were away would throw it on resuming.
        if (mode == "charging") mode = "play";
        pausedFrom = mode;
        mode = "pause";
        menuTab = "game";
        if (pausedFrom == "play") SetPrompt("");
    }

    // Before any input is handled, so nothing pressed while the window is in the background acts first.
    void CheckAutoPause()
    {
        if (Settings.Data.pauseUnfocused && !Inp.Focused && ActiveModes.Contains(mode)) OpenPause(auto: true);
        // Unplugging the gamepad you were playing with pauses too.
        bool padNow = Inp.PadIndex >= 0;
        if (padWas && !padNow && Inp.UsingPad && ActiveModes.Contains(mode)) OpenPause(auto: true);
        padWas = padNow;
    }

    void ClosePause()
    {
        // Still in the background (a gamepad's Start can reach it there): stay paused.
        if (Settings.Data.pauseUnfocused && !Inp.Focused) return;
        rebind = null;
        EndSliderDrag();
        if (mode != "pause") return;
        mode = pausedFrom;
        // Back to a fight or a cast: the button that clicked Resume has to be let go before it reels.
        waitRelease = pausedFrom != "play";
    }

    /* ---------- Interactions ---------- */
    Target FindTarget()
    {
        float x = player.X, y = player.Y;
        string restLabel = Night ? "Rest until morning" : "Rest until night";
        string sleepLabel = Night ? "Sleep until morning" : "Sleep until night";
        var (fx, fy) = FrontTile();

        if (InHouse)
        {
            // The school as the evacuation centre (Magayon.cs).
            if (scene == "house:school") return SchoolRoomTarget();
            if (Dist(x, y, RoomDoorWX, RoomDoorWY) < 14) return new Target { Type = "exit", Label = "Go outside" };
            if (scene == "house:tomas" && Dist(x, y, PhotoX, PhotoY) < 12) return new Target { Type = "photo", Label = "Look at the photo" };
            // Tomas's bed is taken at night. You can wake him, though.
            if (scene == "house:tomas" && TomasInBed && Dist(x, y, TomasBedX, TomasBedY) < 14) return new Target { Type = "npc", Label = "Wake Tomas" };
            foreach (var b in SceneBuilds())
            {
                var d = Data.BuildById[b.id];
                float cx = b.x * T + d.W * 5f, cy = b.y * T + 9;
                if (d.Rest != null && Dist(x, y, b.x * T + d.Rest[0], b.y * T + d.Rest[1]) < 12) return new Target { Type = "rest", Label = sleepLabel };
                if (d.Station != null && Dist(x, y, cx, cy) < 8 + d.W * 5)
                    return new Target { Type = "craft", Id = d.Station, Label = $"Use the {d.Name.ToLowerInvariant()}" };
                if (b.id == "aquarium" && Dist(x, y, cx, cy) < 18) return new Target { Type = "tank", Ref = b, Label = "Look after the aquarium" };
            }
            return null;
        }
        if (scene == "cave")
        {
            // While Platejaw is up there's nothing to do but face it (Guardian.cs).
            if (guardian != null) return GuardianTarget();
            if (MonsterInFront() is Monster m)
                return new Target { Type = "monster", Ref = m, Label = $"Attack the {MonsterKinds[m.Kind].Name} ({Weapon().name})" };
            if (Dist(x, y, ropeTile.x * T + 5, ropeTile.y * T + 8) < 14) return new Target { Type = "exit", Label = "Climb the ladder back to the surface" };
            if (holeTile.x >= 0 && Dist(x, y, holeTile.x * T + 5, holeTile.y * T + 5) < 16)
                return new Target { Type = "descend", Label = caveFloor + 1 == AncientFloor ? "Climb down. Something ancient waits below" : $"Climb down to floor {caveFloor + 1}" };
            if (NodeAt(fx, fy) is Node n)
                return new Target { Type = "node", Ref = n, Label = $"Mine the {Items.ById[Ore(n.Kind).Item].Name.ToLowerInvariant()}" };
            if (OnAncientFloor && Dist(x, y, RuneStoneX, RuneStoneY + 4) < 11) return RuneStoneTarget();
            foreach (var s in Data.Spots)
            {
                if (!SpotHere(s)) continue;
                var (spx, spy) = SpotPos(s);
                if (Dist(x, y, spx, spy) < s.R) return SpotTarget(s);
            }
            return null;
        }

        // In the middle of the fight at the Starwell there's nothing to do but fight; under Bakunawa, only beat the agong.
        if (boss != null) return BossTarget();
        if (eclipse != null) return EclipseTarget();
        // The shaking and the evacuation (RestlessSea.cs): only what to do about them. In the drill, calling to the
        // children comes first, and everything else still works.
        if (tremor != null || SeaEmergency) return SeaEventTarget();
        if (evac?.Drill == true && SeaEventTarget() is { Type: "shout" } drillCall) return drillCall;
        if (RestlessSeaTarget() is Target seaTarget) return seaTarget;
        // Baga and the shelter (Magayon.cs): during the evacuation and the ash, only what to do next.
        if (MagayonTarget() is Target volcanoTarget) return volcanoTarget;
        if (Aboard) return HelmTarget();
        if (ArchipelagoTarget() is Target islandTarget) return islandTarget;
        if (HabagatTarget() is Target habagatTarget) return habagatTarget;
        if (SanctuaryTarget() is Target sanctuaryTarget) return sanctuaryTarget;
        if (Dist(x, y, tomasX, tomasY) < 16)
            return TomasInBed ? new Target { Type = "info", Label = "Tomas has gone to bed in his hut" }
                : new Target { Type = "npc", Label = CanHandIn ? $"Give Tomas the {Items.Amount(state.req.item, state.req.count)}" : "Talk to Tomas" };
        if (Dist(x, y, PipX, PipY + 14) < 12)
            return PipOpen ? new Target { Type = "pip", Label = "Trade with Pip" } : new Target { Type = "info", Label = $"Pip's stall is closed for the night. Pip opens at {HourText(7 * 60)}" };
        if (Dist(x, y, SaltJettyX, SaltJettyY) < 10)
            return new Target { Type = "sail", Id = "atoll", Label = Has("boat") == 0 ? "Pip's jetty (you need a boat to sail)" : VoyageChoice ? "Sail somewhere" : "Sail to Starfall Atoll", AltType = "launch", AltLabel = "Take the helm / explore east" };
        if (Dist(x, y, AtollJettyX, AtollJettyY) < 10)
            return new Target { Type = "sail", Id = "saltmere", Label = VoyageChoice ? "Sail somewhere" : "Sail back to Saltmere", AltType = "launch", AltLabel = "Take the helm / explore" };
        if (Dist(x, y, AsinanJettyX, AsinanJettyY) < 10)
            return new Target { Type = "sail", Id = "saltmere", Label = Has("boat") > 0 ? "Sail somewhere" : "Asinan landing (you need a boat to sail)", AltType = "launch", AltLabel = "Take the helm" };
        if (Dist(x, y, 160, 72) < 10) return new Target { Type = "door", Id = "house:tomas", Label = "Go inside Tomas's hut" };
        if (Dist(x, y, MouthDoorX, MouthDoorY) < 14) return new Target { Type = "cave", Label = "Enter Frostfang Caverns" };
        // Facing your own crab pot or bubo beats a campfire, a smoker or a rack close by, and a chicken wandering past.
        if (BuildAt(fx, fy) is Build pot && Data.BuildById[pot.id].Water)
        {
            string trap = pot.id == "bubo" ? "bubo" : "crab pot";
            return PotReady(pot) ? new Target { Type = "pot", Ref = pot, Label = pot.id == "bubo" ? "Lift the bubo" : "Haul up the crab pot" }
                : new Target { Type = "info", Label = $"The {trap} is soaking. {(pot.id == "bubo" ? "Lift" : "Haul")} it up tomorrow morning" };
        }
        if (Dist(x, y, FireX, FireY) < 16) return new Target { Type = "rest", Label = restLabel, AltType = "cook", AltLabel = "Cook" };
        foreach (var b in state.builds)
        {
            var d = Data.BuildById[b.id];
            if (d.Door && Dist(x, y, b.x * T + 10, b.y * T + 12) < 10)
                return new Target { Type = "door", Id = ShackKey(b), Tx = b.x, Ty = b.y, Label = b.id == "kubo" ? "Go inside your bahay kubo" : "Go inside your shack" };
            if (d.Rest != null && Dist(x, y, b.x * T + d.Rest[0], b.y * T + d.Rest[1]) < 14)
                return new Target { Type = "rest", Label = restLabel, AltType = d.Station == "fire" ? "cook" : null, AltLabel = d.Station == "fire" ? "Cook" : null };
            if (d.Station == "smoker" && Dist(x, y, b.x * T + 5, b.y * T + 10) < 13)
                return new Target { Type = "craft", Id = "smoker", Label = "Use the smoking rack" };
            if (b.id == "dryrack" && Dist(x, y, b.x * T + 5, b.y * T + 10) < 13) return RackTarget(b);
        }
        // Bakawan's firefly trees: after dark, watch the alitaptap (Bakawan.cs).
        if (FireflyTarget() is Target fireflies) return fireflies;
        if (MountNear()) return new Target { Type = "ride", Label = $"Ride {Data.MountName}" };
        if (Dist(x, y, CarvingX, CarvingY + 4) < 13) return new Target { Type = "carving", Label = "Read the carving" };
        if (NearestAnimal() is Animal a) return new Target { Type = "animal", Ref = a, Label = AnimalLabel(a) };
        if (CricketNear() is Bug bug) return new Target { Type = "cricket", Ref = bug, Label = "Catch the cricket" };
        if (state.loose.FirstOrDefault(l => l.kind == "worm" && Dist(l.x, l.y, x, y - 2) < 10) is Loose mound)
            return new Target { Type = "dig", Ref = mound, Label = "Dig for worms" };
        char k = TileAt(fx, fy);
        if (k == 'y') return new Target { Type = "bush", Tx = fx, Ty = fy, Label = BiomeAt(fx, fy) == 6 ? "Pick calamansi" : "Pick berries" };
        if (BuildAt(fx, fy) is Build planted && planted.id == "berrybush") return new Target { Type = "planter", Ref = planted, Label = "Pick berries" };
        if (k is 't' or 'f' or 'h' or 'c' or 'R')
            return new Target { Type = "tree", Tx = fx, Ty = fy, Label = k == 'R' ? "Break the boulder" : $"Chop the {TreeName(k)}" };
        if (BridgeClosed(fx, fy)) return new Target { Type = "info", Label = "The bridge is closed until the storm passes" };
        foreach (var s in Data.Spots)
            if (s.Scene == "world" && SpotOpen(s.Id) && Dist(x, y, s.X, s.Y) < s.R) return SpotTarget(s);
        // A moored boat comes last: it mustn't hide anything else, and <ride> boards it from beside them anyway.
        // In a storm it stays tied up, which leaves the shelter.
        if (!Stormy && BoatInReach()) return new Target { Type = "boat", Label = "Board your boat" };
        // Swimming on Tidemane over deep water, you can fish the open sea from the saddle (not in a storm: shelter).
        if (!Stormy && SeaTarget() is Target sea) return sea;
        // A storm can strand you on another island, so you can always shelter and wait it out.
        if (Stormy) return new Target { Type = "rest", Id = "shelter", Label = "Shelter until the storm passes" };
        return null;
    }

    // Resting skips ahead: through the evening to 21:00, or through the night to 06:30 (a new day: trees, berries, animal
    // gifts and the weather come back, see NewDay). Sheltering skips to the end of the storm. Either way it costs a
    // meal's worth of food and heals you.
    void Rest(bool shelter = false)
    {
        Sfx.Play("ui");
        Learned("rest");
        int day0 = state.day;
        float to = shelter && Stormy ? StormEnds() : Night ? DawnMin + 30 : 21 * 60;
        FadeThrough(() =>
        {
            SkipTo(to);
            state.food = Math.Max(0, state.food - 10);
            state.hp = Math.Min(100, state.hp + 50);
            SnapWeather();
            Save();
        }, () =>
        {
            string msg = state.day != day0 ? $"Morning of day {state.day}." + SeasonTurn() + WeatherNews() + FestivalNews()
                : shelter ? $"You wait out the storm. It's {ClockText(state.clock, 10)}." + WeatherNews()
                : FullMoon ? "Night falls. The moon is full tonight." : "Night falls.";
            Toast(msg + (state.food < 25 ? " You wake up hungry." : ""), state.day != day0 && SeasonTurn() != "" ? 7 : 4);
        });
    }

    void OnAlt()
    {
        if (mode != "play") return;
        if (target?.AltType == "release") ReleaseCatch();
        else if (target?.AltType == "riddle") AskRiddle();
        else if (target?.AltType == "cook") OpenCraft("fire");
        else if (target?.AltType == "chum") ThrowChum(target.Id);
        else if (target?.AltType == "launch") LaunchBoat();
        else if (target?.AltType == "troll") ToggleTroll();
        else if (target?.AltType == "unrack") UnloadRack((Build)target.Ref);
        else if (target?.AltType == "rackguso") LayGuso((Build)target.Ref);
    }

    void OnAlt2()
    {
        if (mode == "play" && target?.Alt2Type == "spear") StartSpear(target.Id);
    }

    void OnAction()
    {
        switch (mode)
        {
            case "dialogue": Sfx.Play("blip"); AdvanceDialogue(); return;
            case "catch": CloseCatch(); return;
            case "odd": CloseOdd(); return;
            case "legend": CloseLegend(); return;
            case "tamed": CloseTamed(); return;
            case "moon": CloseMoon(); return;
            case "platejaw": CloseGuardianCard(); return;
            case "seacard": CloseSeaCard(); return;
            case "magayoncard": CloseMagayonCard(); return;
            case "build": BuildAction(); return;
            case "play":
                // Nothing in front of you: use what you're holding (Hotbar.cs).
                if (target == null) { UseHeld(); return; }
                switch (target.Type)
                {
                    case "npc":
                        if (TomasInBed) { FaceToward(TomasBedX, TomasBedY - 8); Talk(new() { new("Tomas", "Mm? Oh, it's you. Couldn't sleep either?") }, TalkTomasWithRequests); }
                        else { FaceToward(tomasX, tomasY); TalkTomasWithRequests(); }
                        break;
                    case "pip": FaceToward(PipX, PipY); TalkPip(); break;
                    case "sail": if (Has("boat") > 0 && (VoyageChoice || Dist(player.X, player.Y, AsinanJettyX, AsinanJettyY) < 12)) OpenVoyage(); else Sail(target.Id); break;
                    case "boat": BoardBoat(); break;
                    case "land": LandBoat(); break;
                    case "trolling": WindIn("You wind the trolling line in."); break;
                    case "islander": TalkIslander(target.Id); break;
                    case "habagatfolk": TalkHabagat(target.Id); break;
                    case "saltbed": RakeSalt(); break;
                    case "rack": UseRack((Build)target.Ref); break;
                    case "rackguso": LayGuso((Build)target.Ref); break;
                    case "guso": UseGusoLine(int.Parse(target.Id)); break;
                    case "fireflies": WatchFireflies(int.Parse(target.Id)); break;
                    case "watch": Watch((SeaCreature)target.Ref); break;
                    case "joy": TalkJoy(); break;
                    case "agong": BeatAgong(); break;
                    case "tank": OpenTank((Build)target.Ref); break;
                    case "planter": PickPlanter((Build)target.Ref); break;
                    case "info": Sfx.Play("nope"); break;
                    case "rest": Rest(target.Id == "shelter"); break;
                    case "spot": if (target.Id == "icehole") Cast(target.Id); else StartCharge(target.Id); break;
                    case "drill": StartDrill(); break;
                    case "dig": DigWorms((Loose)target.Ref); break;
                    case "cricket": CatchCricket((Bug)target.Ref); break;
                    case "pot": HaulPot((Build)target.Ref); break;
                    case "door": EnterHouse(target.Id, target.Id == "house:tomas" ? 160 : target.Tx * T + 10, target.Id == "house:tomas" ? 72 : target.Ty * T + 12); break;
                    case "exit": if (scene == "cave") LeaveCave(); else LeaveToWorld(); break;
                    case "cave": EnterCaveFromMouth(); break;
                    case "descend": EnterCaveFloor(caveFloor + 1); break;
                    case "monster": Attack((Monster)target.Ref); break;
                    case "boss": StrikeBoss(); break;
                    case "guardian": StrikeGuardian(); break;
                    case "runestone": UseRuneStone(); break;
                    case "shout": Shout(); break;
                    case "seapost": LookAtPost(); break;
                    case "seasign": PutUpSign(target.Tx); break;
                    case "duck": break;
                    // Beneath the clouds (Magayon.cs).
                    case "schooldoor": EnterHouse("house:school", SchoolDoorX, SchoolDoorY); break;
                    case "alertboard": FaceToward(AlertBoardX, AlertBoardY); OpenAlertCard(); break;
                    case "abaca": TieAbaca(); break;
                    case "pili": PlantPili(); break;
                    case "gobag": OpenGoBag(); break;
                    case "callmila": CallMila(); break;
                    case "register": SignEvacList(); break;
                    case "boardevac": BoardEvacBoat(assisted: false); break;
                    case "ben": TalkBen(); break;
                    case "jar": CoverJar(target.Tx); break;
                    case "barrel": UnhookBarrel(); break;
                    case "shutter": CloseShutter(target.Tx); break;
                    case "takewater": TakeWater(); break;
                    case "givewater": GiveWater(target.Tx); break;
                    case "desk": RegisterShelter(); break;
                    case "display": OpenLaharWatch(); break;
                    case "ride": Mount(); break;
                    case "carving": ReadCarving(); break;
                    case "craft": OpenCraft(target.Id); break;
                    case "animal": Pet((Animal)target.Ref); break;
                    case "tree": HitTree(target.Tx, target.Ty); break;
                    case "bush": PickBush(target.Tx, target.Ty); break;
                    case "node": MineNode((Node)target.Ref); break;
                    case "photo":
                        Talk(new() { new Say("You", state.Caught("hollow_eel")
                            ? "Tomas and Mara on the old dock. She has his stubborn chin."
                            : "A faded photo of Tomas with a young woman, standing on the old dock. They're both laughing.") });
                        break;
                }
                return;
            case "waiting":
                if (fish.Spot == "icehole") Jig();
                else ReelIn("You reeled in. Nothing bit.");
                return;
            case "bite": Hook(); return;
            case "spear": ThrowSpear(); return;
            case "drill": DrillHit(); return;
        }
    }

    /* ---------- Update loop ---------- */
    void Update(float dt)
    {
        time += dt;
        if (toastTimer > 0) toastTimer -= dt;
        toastAlpha = Math.Clamp(toastAlpha + (toastTimer > 0 ? dt : -dt) / 0.25f, 0, 1);
        UpdateFloaters(dt);
        UpdateTitleTour(dt);
        UpdateGuide(dt);
        TellBadgeNews();
        HoldMount();
        if (mode is not ("pause" or "panel" or "title" or "create"))
        {
            UpdateCritters(dt);
            UpdateAnimals(dt);
            UpdateBugs(dt);
            UpdateParticles(dt);
            UpdateBoat(dt);
            UpdateGlow(dt);
            UpdateMount(dt);
            hotbarTipT = MathF.Max(0, hotbarTipT - dt);
            UpdateSchools(dt);
            UpdateStrollers(dt);
            UpdateSeaLife(dt);
            UpdateLeaves(dt);
            UpdateWeather(dt);
            heldT = Math.Max(0, heldT - dt);
            UpdateRelease(dt);
        }
        if (mode == "panel" && panel == "sungka") UpdateSungka(dt);
        if (mode == "panel" && panel == "quiz") UpdateQuiz(dt);
        if (mode == "panel" && panel == "sort") UpdateSort();
        if (mode == "panel" && panel == "album") UpdateAlbum();
        if (mode == "panel" && panel == "readings") UpdateReadings(dt);
        if (mode == "panel" && panel == "lahar") UpdateLaharWatch(dt);
        if (mode is not ("title" or "create" or "pause")) state.playSecs += dt;
        if (mode != "pause") idleT = mode == "play" && !player.Moving ? idleT + dt : 0;
        if (ActiveModes.Contains(mode))
        {
            TickClock(dt);
            // Only the shaking and the real evacuation hold these (Codex: the drill used to freeze them too, and hunger
            // still hurt you during the evacuation).
            // Leaving Baga and getting the shelter ready hold them too (Magayon.cs).
            bool held = tremor != null || SeaEmergency || VolcanoControlled;
            if (!held) TickFood(dt);
            if (!held) TickHealth(dt);
            UpdateMonsters(dt);
            UpdateBoss(dt);
            UpdateGuardian(dt);
            UpdateRestlessSea(dt);
            UpdateMagayon(dt);
            UpdateEclipse(dt);
            if (!held) TickDerby(dt);
        }
        else { iframes = Math.Max(0, iframes - dt); hurtFlash = Math.Max(0, hurtFlash - dt); }

        if (mode == "fade")
        {
            fade += fadeDir * dt * 2.2f;
            if (fadeDir > 0 && fade >= 1)
            {
                fade = 1; fadeDir = -1;
                var cb = fadeCb; fadeCb = null; cb?.Invoke();
            }
            else if (fadeDir < 0 && fade <= 0)
            {
                fade = 0; fadeDir = 0; mode = "play";
                var cb = fadeAfter; fadeAfter = null; cb?.Invoke();
            }
            return;
        }

        if (mode == "dialogue" && dlg != null)
        {
            if (dlg.Shown < dlg.Full.Length)
            {
                int before = (int)dlg.Shown;
                dlg.Shown = Math.Min(dlg.Full.Length, dlg.Shown + dt * 48);
                int now = (int)dlg.Shown;
                if (now != before && now % 3 == 0) Sfx.Play("blip");
            }
            return;
        }

        if (mode == "play" || mode == "build")
        {
            float dx = 0, dy = 0;
            if (Bind.Down("left")) dx -= 1;
            if (Bind.Down("right")) dx += 1;
            if (Bind.Down("up")) dy -= 1;
            if (Bind.Down("down")) dy += 1;
            // A gamepad stick steers in any direction, and a gentle push walks slowly (from about a third of full speed).
            float push = 1;
            if (Inp.Stick != System.Numerics.Vector2.Zero)
            {
                (dx, dy) = (Inp.Stick.X, Inp.Stick.Y);
                push = Math.Clamp(0.35f + 0.65f * (Inp.Stick.Length() - 0.35f) / 0.5f, 0.35f, 1);
            }
            // Under Bakunawa you stand your ground with the agong.
            if (eclipse != null || Ducking) dx = dy = 0;
            player.Moving = dx != 0 || dy != 0;
            // On Tidemane you gallop on land and swim through any water, picking up speed and pulling up (Tidemane.cs).
            if (Riding) RideMove(dx, dy, push, dt);
            else if (player.Moving)
            {
                // On foot, waders slow you in the shallows.
                bool wet = InWater;
                float speed = Aboard ? BoatSpeed : wet ? 52 * 0.7f : 52;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                // Under sail the monsoon helps or hinders (Seasons.cs); a trolling boat just putters along.
                if (Aboard && !trolling) speed *= WindFactor(dx / len, dy / len);
                // Hard to walk while the ground shakes.
                float sp = speed * push * dt * (Starving ? 0.6f : 1f) * (Shaking ? 0.45f : 1f);
                float mx = dx / len * sp, my = dy / len * sp;
                // Somewhere closed (Baga's danger zone, the lahar channel, Baga while it's evacuated) stops you first (Magayon.cs).
                if ((Aboard ? BoatCanStand(player.X + mx, player.Y) : CanStand(player.X + mx, player.Y, Wading, flats: true)) && StepAllowed(player.X + mx, player.Y)) player.X += mx;
                if ((Aboard ? BoatCanStand(player.X, player.Y + my) : CanStand(player.X, player.Y + my, Wading, flats: true)) && StepAllowed(player.X, player.Y + my)) player.Y += my;
                player.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
                if (Aboard) boatFace = player.Face;
                int frameBefore = (int)(player.WalkT * 9);
                player.WalkT += dt * push * (wet ? 0.75f : 1);
                int frame = (int)(player.WalkT * 9);
                if (!Aboard && frame != frameBefore && frame % 2 == 0) Footstep();
                heldT = 0;
            }
            if (scene == "world")
            {
                TickLoose(dt);
                AnnounceBiome();
                CheckStarwell();
                if (Riding) { state.mountX = player.X; state.mountY = player.Y; }
                if (Aboard) { state.boatX = player.X; state.boatY = player.Y; }
                DiscoverArchipelago();
                DiscoverHabagat();
                ChartNearIslets();
                UpdateReefWalk();
                WarnRisingTide();
                if (mode == "play") { UpdateTroll(dt); UpdateRegatta(dt); CheckEclipse(); }
                CheckPin();
            }
            // Walking down onto a doorway steps back outside.
            if (InHouse && dy > 0 && TileAt((int)(player.X / T), (int)((player.Y - 1.5f) / T)) == 'Y') { LeaveToWorld(); return; }
            if (mode == "build")
            {
                if (player.Moving) hover = null;
                UpdateGhost();
                var d = Data.BuildById.GetValueOrDefault(buildTool);
                if (ghost.Reason != "") SetPrompt($"{(d != null ? d.Name : "Take down")}: {ghost.Reason}");
                else if (d != null) SetPrompt($"Place {d.Name.ToLowerInvariant()} · {d.Desc}", "<act>");
                else SetPrompt($"Take down the {Data.BuildById[ghost.Target.id].Name.ToLowerInvariant()}", "<act>");
            }
            else
            {
                target = FindTarget();
                // Just after landing a fish, F lets it go (Release.cs), over whatever F would do here.
                if (CanRelease)
                {
                    target ??= new Target { Type = "none", Label = "" };
                    target.AltType = "release";
                    target.AltLabel = ReleaseLabel;
                }
                string label = target?.Label ?? "";
                if (target?.AltLabel != null) label += $"   [<alt>] {target.AltLabel}";
                if (target?.Alt2Label != null) label += $"   [<spear>] {target.Alt2Label}";
                if (target?.RideLabel != null) label += $"   [<ride>] {target.RideLabel}";
                if (target?.Type != "boat" && !Stormy && BoatInReach()) label += "   [<ride>] Board your boat";
                if (target?.Type == "none") SetPrompt($"{target.AltLabel}", "<alt>");
                // Nothing in front of you: what <act> does with what you're holding (Hotbar.cs).
                else if (target == null && HeldUseHint() is string use) SetPrompt(use + label, "<act>");
                else SetPrompt(label, target != null ? "<act>" : null);
            }
            saveTimer += dt;
            if (saveTimer > 5) { saveTimer = 0; Save(); }
            return;
        }

        UpdateTow(dt);
        UpdateFishing(dt);
    }

    // A short banner when you step off a bridge onto a different island.
    void AnnounceBiome()
    {
        char tile = TileAt((int)MathF.Floor(player.X / T), (int)MathF.Floor((player.Y - 1.5f) / T));
        if (WaterTile(tile)) return;
        ChartHere();
        byte b = PlayerBiome();
        // Setting foot on the atoll counts as a visit however you came (Sail, your own boat or Tidemane): it puts the
        // atoll on the map and opens Pip's hoofprints story and Tomas's atoll requests.
        if (b == 4 && !state.Hinted("visitedAtoll")) state.hinted["visitedAtoll"] = true;
        if (tile is 'd' or 'b' || b == lastBiome) return;
        lastBiome = b;
        var biomeDef = Data.Biomes[b];
        Toast($"{biomeDef.Enter} ({biomeDef.Climate})", 3);
    }

    /* ---------- Start ---------- */
    void StartGame(bool fresh, Look look = null)
    {
        if (fresh)
        {
            // Only now, with the new fisher made, does the slot they picked lose whatever was in it.
            SaveFile.Slot = newSlot;
            SaveFile.Clear(newSlot);
            state = new State();
            if (look != null) { state.look = look; state.created = true; }
        }
        state.FixClock();
        chartEast = false;
        if (state.aboard && (Has("boat") == 0 || state.scene != "world")) state.aboard = false;
        if (state.aboard) state.riding = false;
        tomasX = TomasHomeX; tomasY = TomasHomeY;
        // Nothing carries over from a game played earlier in this session (quitting to the title and loading another slot).
        fish = null; reel = null; panel = null; boss = null; dlg = null; eclipse = null; race = null; raceArmed = false; sungka = null;
        guardian = null; guardianDue = false; rocks.Clear();
        tremor = null; evac = null; cocos.Clear(); trackBeforeSea = null;
        ResetMagayon();
        bolts.Clear(); chumUntil.Clear(); floaters.Clear(); leaves.Clear(); bugs.Clear(); animals.Clear(); schools.Clear(); seaLife.Clear(); glowTrail.Clear();
        habagatWalk.Clear();
        // The Sea school's moment-to-moment bits (1.17).
        landed = null; releaseFx = null; sorting = null; quiz = null; riddle = null; warnedTide = int.MinValue;
        dexFish = null; dexExtra = null; atlasZoom = false;
        badgeNews = null; lessonPrizes = null;
        albumPages = null;
        state.know ??= new(); state.letGo ??= new(); state.missed ??= new();
        goals.Clear(); tracked = null; guideWay = null; lastTrackedId = lastTrackedTitle = goalDoneTitle = null; goalDoneT = 0;
        requestsKnown = null; journalPage = 0; seaPointCache = ("", 0, 0, 0, 0);
        state.track ??= "";
        NoteGuideVersion(fresh);
        // A story finished without ever meeting Tomas (the early-Glowgill bug): he's met, so he gives requests.
        if (state.flags.ended) state.flags.metTomas = true;
        // The tide goes out after the Tidecrawler's catch card, in a fade: a game closed before then would never reach the wreck.
        if (state.Caught("tidecrawler")) state.flags.tideOut = true;
        trolling = towing = false;
        boatFace = "right"; sailFurl = 1; sprayT = 0; mountDir = 1;
        EnsureHotbar();
        // Tidemane (Tidemane.cs): no whistle still on its way from another game, no momentum, no hop half done.
        call = null; whistleT = 0; hopT = 0; rideVel = (0, 0); mountSpeed = 0; mountGaitFrame = -1; shoreHopT = 0; trackPrints.Clear();
        derbyT = 0; heldT = 0; iframes = 0; hurtFlash = 0; quake = 0; pointerHold = false; caveFloor = 1;
        mapTexDirty = true;
        titleView = "main";
        // The weather is whatever the forecast says for now. Older saves get tomorrow's forecast rolled here.
        state.weather = planned = Planned(SinceDawn); warnedStorm = -1;
        state.tomorrow ??= MakeForecast(state.day + 1);
        SnapWeather();
        LoadScene("world");
        // Where you'll be outdoors, so a tree that's due doesn't grow back on top of you (see RegrowBlocked).
        (player.X, player.Y) = state.scene == "world" ? (state.px, state.py) : state.scene == "cave" ? (MouthDoorX, MouthDoorY + 2) : (state.exitX, state.exitY);
        BuildMap();
        state.charted ??= LegacyCharted();
        mapTexDirty = true;
        SpawnAnimals();
        int sunk = RescueSunkBuilds();
        player.X = state.px; player.Y = state.py; player.Face = Aboard ? boatFace : "down";
        // Cave floors aren't saved, so a game saved underground carries on at the cave mouth.
        if (state.scene == "cave") { player.X = MouthDoorX; player.Y = MouthDoorY + 2; }
        else if (state.scene != "world" && SceneExists(state.scene)) LoadScene(state.scene);
        // A room that isn't there any more (the school once it's no longer the shelter): outside its door.
        else if (state.scene != "world") { player.X = state.exitX; player.Y = state.exitY; }
        // Somewhere closed now (Baga while it's evacuated, or inside its danger zone in an old save) (Magayon.cs).
        PlaceAfterLoad();
        if (!(Aboard ? BoatCanStand(player.X, player.Y) : CanStand(player.X, player.Y, Wading, Riding, !Riding)))
        {
            // Somewhere that isn't there any more (the atoll used to be smaller): back to the boat, or to Saltmere.
            LoadScene("world");
            state.riding = false;
            state.aboard = false;
            bool atoll = state.boatAt == "atoll" && Has("boat") > 0;
            player.X = atoll ? AtollJettyX + 4 : 160; player.Y = atoll ? AtollJettyY + 1 : 115;
        }
        int rescued = RescueStranded();
        ReconcileAlbum();
        if (state.tamed && !state.riding && state.mountX == 0 && state.mountY == 0) { state.mountX = player.X + 12; state.mountY = player.Y; }
        ReindexBuilds();
        if (scene == "world") FillLoose();
        critters.Clear();
        lastBiome = scene == "world" ? PlayerBiome() : (byte)0;
        chartEast = player.X >= EastStart * T;
        mode = "play";
        if (fresh || !state.flags.metTomas)
        {
            Talk(new()
            {
                new("", "You arrive on Saltmere Island with a rod, a bucket and a lot of questions."),
                new("", "Tomas waves from his hut. Walk over with <move> and press <act> to talk.")
            });
        }
        else if (sunk > 0)
            Toast($"Starfall Atoll has grown since you were last there, and the sea took the old shoreline. The {sunk} thing{(sunk == 1 ? "" : "s")} you built there {(sunk == 1 ? "is" : "are")} back in your bag.", 7);
        else if (rescued > 0)
            Toast($"New islands have risen in the south since you were last out there. Your boat was found and tied up at {(state.boatAt == "atoll" ? "the atoll's jetty" : "Pip's jetty")}.", 7);
        else Toast($"Welcome back to {(InAmihan ? "Amihan" : InHabagat ? "Habagat" : "Saltmere")}. Day {state.day}, {ClockText(state.clock, 10)}.");
        hasSave = true;
        Save();
    }

    // Continue picks up the slot saved to most recently.
    void TitleContinue()
    {
        int n = SaveFile.Newest();
        if (n > 0) LoadSlot(n);
    }

    void LoadSlot(int n)
    {
        renameSlot = 0;
        var s = SaveFile.Read(n);
        if (s == null) { Sfx.Play("nope"); Toast("That save can't be read."); return; }
        Sfx.Play("ui");
        SaveFile.Slot = n;
        state = s;
        // Saves from before character creation get to make their fisher first.
        if (!state.created) OpenCreator("continue");
        else StartGame(false);
    }

    // A new game goes in the first empty slot. With all three in use, you pick one to give up.
    void TitleNew()
    {
        Sfx.Play("ui");
        int free = SaveFile.FirstFree();
        if (free == 0) { OpenSlots("new"); return; }
        NewGameIn(free);
    }

    void NewGameIn(int slot)
    {
        renameSlot = 0;
        newSlot = slot;
        OpenCreator("new");
    }

    void TitlePrimary()
    {
        if (hasSave) TitleContinue();
        else TitleNew();
    }

    // Back to the title screen from the menu, to load another slot or start a new game.
    void QuitToTitle()
    {
        Save();
        rebind = null;
        EndSliderDrag();
        mode = "title";
        titleView = "main";
        // The title tours the islands outdoors (UiMenu.cs), from the start; the saved game keeps where you really were.
        if (scene != "world") LoadScene("world");
        titleT = 0;
        slotInfo = null;
        toastTimer = 0;
        SetPrompt("");
    }
}
