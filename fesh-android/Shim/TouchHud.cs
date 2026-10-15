// On-screen controls for a phone: a stick that holds W/A/S/D, and round buttons that hold E/F/R/V/G/Q (and X while building).
// Everything else (the HUD buttons, the hotbar, the menus, tapping the world) is the game's own UI, which already works by
// pointer, so any touch that isn't on a control becomes the mouse. The settings for all this are the Touch tab of the game's
// own Settings screen (Platform holds the values), and "Move the controls" there starts the drag-to-place editor below.
//
// The game decides nothing here: this reads `mode` (and whether a text box is open) from the Game object by reflection.
// Pointer roles are worked out on the UI thread; drawing is on the game thread. All coordinates are inside the safe area.
using System.Reflection;
using Raylib_cs;

namespace Fesh.Droid;

internal static class TouchHud
{
    public static object Game;   // the running Fesh.Game, set by the activity
    public static Action<bool> TextWanted;   // the soft keyboard should show (true) or hide (false)
    public static Action ToggleKeyboard;     // the keyboard button (UI thread)
    public static Action Haptic;             // a short buzz, on the UI thread
    static FieldInfo fMode, fRename, fDockView, fLanView, fPanel;
    static string mode = "title";
    public static string Mode => mode;
    public static volatile bool WantText;
    public static volatile bool Visible;      // the game is in a mode where the controls apply
    public static volatile bool Editing;
    public static volatile bool WheelAllowed; // a menu or panel is up: a vertical drag scrolls it
    public static int W = 1280, H = 720;
    public const string Build = "1.31.0";

    static readonly string[] Live = { "play", "build", "charging", "casting", "waiting", "bite", "reeling", "chest", "spear", "drill" };

    // ---------- layout ----------
    // Each control: where it sits by default (layout units from its side edge and from the bottom), how big it is, and
    // whether it only shows while building.
    readonly record struct Spec(string Id, string Label, KeyboardKey Key, float Ax, float Ay, float R, bool BuildOnly = false);
    static readonly Spec[] Specs =
    {
        new("stick", "", KeyboardKey.Null, 190, 165, 74),
        new("E", "E", KeyboardKey.E, 118, 130, 56),    // talk, fish, reel, use (hold to charge a cast)
        new("F", "F", KeyboardKey.F, 240, 92, 38),     // cook, throw chum
        new("R", "R", KeyboardKey.R, 218, 206, 34),    // ride, board
        new("V", "V", KeyboardKey.V, 112, 246, 34),    // use what you hold
        new("G", "G", KeyboardKey.G, 330, 150, 30),    // spearfish
        new("Q", "Q", KeyboardKey.Q, 330, 235, 28),    // journal
        new("X", "X", KeyboardKey.X, 225, 300, 30, BuildOnly: true),   // take down a piece while building
    };
    static float U => H / 720f;
    static bool OnRight(string id) => (id != "stick") != TouchSettings.SwapSides;   // buttons right, stick left (swapped: the other way)
    static bool Shown(Spec s) => !s.BuildOnly || mode == "build" || Editing;

    static (float x, float y) Where(Spec s)
    {
        var (ax, ay) = TouchSettings.Pos.TryGetValue(s.Id, out var p) ? p : (s.Ax, s.Ay);
        float u = U;
        return (OnRight(s.Id) ? W - ax * u : ax * u, H - ay * u);
    }
    static float Radius(Spec s) => s.R * U * (s.Id == "stick" ? TouchSettings.StickScale : TouchSettings.ButtonScale);
    static Spec Find(string id) => Specs.First(s => s.Id == id);

    static bool InFloatZone(float x, float y) => y > H * 0.38f && (TouchSettings.SwapSides ? x > W * 0.58f : x < W * 0.42f);
    // The keyboard button, while a name or an address is being typed.
    static (float x, float y, float r) KbButton() => (TouchSettings.SwapSides ? 44 * U : W - 44 * U, 44 * U, 26 * U);

    // ---------- pointer roles (UI thread) ----------
    enum Role { Mouse, Stick, Button, Ui, Drag }
    static readonly Dictionary<int, (Role role, string id)> roles = new();
    static float stickOx, stickOy, stickX, stickY;
    static int stickId = -1;
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> down = new();   // read by the game thread to draw pressed buttons
    static bool kW, kA, kS, kD;

    static bool Hit(float px, float py, float x, float y, float r) { float dx = px - x, dy = py - y; return dx * dx + dy * dy <= r * r; }

    public static void Down(int id, float x, float y)
    {
        if (Editing) { EditDown(id, x, y); return; }
        if (WantText)
        {
            var kb = KbButton();
            if (Hit(x, y, kb.x, kb.y, kb.r * 1.3f)) { roles[id] = (Role.Ui, "kb"); Buzz(); ToggleKeyboard?.Invoke(); return; }
        }
        if (Visible)
        {
            foreach (var s in Specs)
            {
                if (s.Id == "stick" || !Shown(s)) continue;
                var (cx, cy) = Where(s);
                if (Hit(x, y, cx, cy, Radius(s) * 1.25f)) { roles[id] = (Role.Button, s.Id); down[s.Id] = 1; InputBackend.PressTouch(s.Key); Buzz(); return; }
            }
            if (stickId < 0)
            {
                var st = Find("stick");
                var (bx, by) = Where(st);
                if (TouchSettings.FixedStick ? Hit(x, y, bx, by, Radius(st) * 1.5f) : InFloatZone(x, y))
                {
                    stickId = id;
                    (stickOx, stickOy) = TouchSettings.FixedStick ? (bx, by) : (x, y);
                    stickX = x; stickY = y;
                    roles[id] = (Role.Stick, "stick");
                    UpdateStick();
                    return;
                }
            }
        }
        if (!roles.Values.Any(r => r.role == Role.Mouse)) { roles[id] = (Role.Mouse, ""); InputBackend.TouchDown(x, y); }
    }

    public static void Move(int id, float x, float y)
    {
        if (!roles.TryGetValue(id, out var r)) return;
        switch (r.role)
        {
            case Role.Mouse: InputBackend.TouchMove(x, y); break;
            case Role.Stick: stickX = x; stickY = y; UpdateStick(); break;
            case Role.Drag: DragTo(r.id, x, y); break;
        }
    }

    public static void Up(int id, bool cancel = false)
    {
        if (!roles.Remove(id, out var r)) return;
        switch (r.role)
        {
            case Role.Mouse: if (cancel) InputBackend.TouchCancel(); else InputBackend.TouchUp(); break;
            case Role.Button: down.TryRemove(r.id, out _); InputBackend.ReleaseTouch(Find(r.id).Key); break;
            case Role.Stick: stickId = -1; ReleaseStick(); break;
            case Role.Drag: TouchSettings.Save(); break;
        }
    }

    public static void CancelAll() { foreach (var id in roles.Keys.ToList()) Up(id, cancel: true); }

    static void Buzz() { if (TouchSettings.Haptics) Haptic?.Invoke(); }

    static void UpdateStick()
    {
        var st = Find("stick");
        float r = Radius(st), dx = stickX - stickOx, dy = stickY - stickOy, len = MathF.Sqrt(dx * dx + dy * dy);
        if (!TouchSettings.FixedStick && len > r * 1.5f)   // a floating stick follows a long drag
        {
            float k = (len - r * 1.5f) / len;
            stickOx += dx * k; stickOy += dy * k;
            dx = stickX - stickOx; dy = stickY - stickOy;
        }
        float fx = dx / r, fy = dy / r;
        Set(KeyboardKey.A, ref kA, fx < -0.38f);
        Set(KeyboardKey.D, ref kD, fx > 0.38f);
        Set(KeyboardKey.W, ref kW, fy < -0.38f);
        Set(KeyboardKey.S, ref kS, fy > 0.38f);
    }
    static void Set(KeyboardKey k, ref bool state, bool on) { if (state != on) { state = on; if (on) InputBackend.PressTouch(k); else InputBackend.ReleaseTouch(k); } }
    static void ReleaseStick() { Set(KeyboardKey.A, ref kA, false); Set(KeyboardKey.D, ref kD, false); Set(KeyboardKey.W, ref kW, false); Set(KeyboardKey.S, ref kS, false); }

    // ---------- the layout editor (Settings > Touch > Move the controls) ----------
    // Asked for by the game (its Settings screen), so the finger bookkeeping is done on the UI thread, where the fingers are.
    public static void StartEditing() { global::Android.App.Application.SynchronizationContext.Post(_ => { CancelAll(); Editing = true; }, null); }

    static void EditDown(int id, float x, float y)
    {
        var done = EditDone();
        if (x >= done.X && x <= done.X + done.W && y >= done.Y && y <= done.Y + done.H) { Editing = false; TouchSettings.Save(); roles[id] = (Role.Ui, "done"); Buzz(); return; }
        var reset = EditReset();
        if (x >= reset.X && x <= reset.X + reset.W && y >= reset.Y && y <= reset.Y + reset.H) { TouchSettings.Pos.Clear(); TouchSettings.Save(); roles[id] = (Role.Ui, "reset"); Buzz(); return; }
        // The smallest control under the finger wins, so a small button on top of a big one can still be picked.
        Spec? best = null; float bestR = float.MaxValue;
        foreach (var s in Specs)
        {
            var (cx, cy) = Where(s);
            float r = Radius(s) * 1.2f;
            if (Hit(x, y, cx, cy, r) && r < bestR) { best = s; bestR = r; }
        }
        roles[id] = best is Spec b ? (Role.Drag, b.Id) : (Role.Ui, "none");
    }

    static void DragTo(string id, float x, float y)
    {
        float u = U;
        x = Math.Clamp(x, 0, W); y = Math.Clamp(y, 0, H);
        float ax = (OnRight(id) ? W - x : x) / u, ay = (H - y) / u;
        TouchSettings.Pos[id] = (ax, ay);
    }

    static (float X, float Y, float W, float H) EditDone() => (W / 2f + 8 * U, 14 * U, 130 * U, 46 * U);
    static (float X, float Y, float W, float H) EditReset() => (W / 2f - 138 * U, 14 * U, 130 * U, 46 * U);

    // ---------- the game thread: read the game's state, then draw ----------
    static T Field<T>(ref FieldInfo f, string name, T fallback)
    {
        if (Game == null) return fallback;
        f ??= Game.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return f?.GetValue(Game) is T v ? v : fallback;
    }

    static Font font;
    static bool fontTried;
    static Color A(int r, int g, int b, int a) => new(r, g, b, (int)(a * TouchSettings.Opacity));

    public static void Draw()
    {
        mode = Field(ref fMode, "mode", "title");
        bool live = Array.IndexOf(Live, mode) >= 0;
        if (live != Visible)
        {
            Visible = live;
            if (!live) global::Android.App.Application.SynchronizationContext.Post(_ => CancelAll(), null);
        }
        bool wheelNow = mode == "panel" && Field(ref fPanel, "panel", "") == "atlas" && !Editing;   // the island guide is the one screen that scrolls with the wheel
        if (WheelAllowed && !wheelNow) global::Android.App.Application.SynchronizationContext.Post(_ => InputBackend.TouchCancel(), null);   // a press still waiting must not click the next screen
        WheelAllowed = wheelNow;   // the island guide is the one screen that scrolls with the wheel
        bool text = mode == "create" || Field(ref fRename, "renameSlot", 0) > 0
            || mode == "panel" && Field(ref fPanel, "panel", "") == "matches" && Field(ref fDockView, "dockView", "") == "finding"
            || Field(ref fLanView, "lanView", "") == "finding";
        if (text != WantText) { WantText = text; TextWanted?.Invoke(text); }

        if (!fontTried)
        {
            fontTried = true;
            try
            {
                using var s = typeof(TouchHud).Assembly.GetManifestResourceStream("PixelifySans-Bold.ttf");
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                var cps = Enumerable.Range(32, 95).ToArray();
                font = FontBackend.Load(ms.ToArray(), 36, cps, cps.Length);
                Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
            }
            catch { }
        }
        if (TouchSettings.ShowDebug) Stamp();

        if (Editing) Raylib.DrawRectangle(0, 0, W, H, new Color(8, 16, 24, 150));   // hides the menu it was opened from
        if (live || Editing)
        {
            foreach (var s in Specs)
            {
                if (!Shown(s)) continue;
                var (x, y) = Where(s);
                if (s.Id == "stick") DrawStick(x, y);
                else Round(x, y, Radius(s), s.Label, down.ContainsKey(s.Id), s.Id == "E");
            }
        }
        if (Editing) DrawEditBanner();
        if (WantText && !Editing) DrawKbButton();
    }

    static void DrawStick(float bx, float by)
    {
        float r = Radius(Find("stick"));
        if (stickId >= 0)
        {
            float ox = stickOx, oy = stickOy, dx = stickX - ox, dy = stickY - oy, len = MathF.Sqrt(dx * dx + dy * dy);
            if (len > r) { dx *= r / len; dy *= r / len; }
            Raylib.DrawCircleV(new(ox, oy), r, A(20, 30, 40, 90));
            Raylib.DrawCircleLinesV(new(ox, oy), r, A(241, 230, 200, 200));
            Raylib.DrawCircleV(new(ox + dx, oy + dy), r * 0.45f, A(241, 230, 200, 200));
        }
        else
        {
            // Where it is (stays put) or where it would start (follows the thumb: only a faint hint).
            bool fix = TouchSettings.FixedStick || Editing;
            Raylib.DrawCircleV(new(bx, by), r, A(20, 30, 40, fix ? 70 : 0));
            Raylib.DrawCircleLinesV(new(bx, by), r, A(241, 230, 200, fix ? 170 : 80));
            Raylib.DrawCircleV(new(bx, by), r * 0.4f, A(241, 230, 200, fix ? 110 : 55));
            if (Editing) Label("STICK", bx, by + r + 14 * U, 15 * U, new Color(255, 255, 255, 255));
        }
    }

    static void Round(float x, float y, float r, string label, bool pressed, bool primary)
    {
        var fill = pressed ? A(255, 214, 90, 230) : primary ? A(232, 190, 70, 140) : A(241, 230, 200, 110);
        Raylib.DrawCircleV(new(x, y), r, fill);
        Raylib.DrawCircleLinesV(new(x, y), r, A(40, 30, 20, 190));
        if (Editing) Raylib.DrawCircleLinesV(new(x, y), r + 4 * U, new Color(255, 255, 255, 220));
        Label(label, x, y, r * 0.9f, A(40, 30, 20, 255));
    }

    // Centred text; `size` is the pixel height.
    static void Label(string s, float cx, float cy, float size, Color c)
    {
        if (font.Texture.Id == 0 || s.Length == 0) return;
        float wd = Raylib.MeasureTextEx(font, s, size, 0).X;
        Raylib.DrawTextEx(font, s, new(cx - wd / 2, cy - size * 0.55f), size, 0, c);
    }
    static void LabelLeft(string s, float x, float cy, float size, Color c)
    {
        if (font.Texture.Id == 0) return;
        Raylib.DrawTextEx(font, s, new(x, cy - size * 0.55f), size, 0, c);
    }

    // A keyboard icon: tap it to bring the keyboard back (or put it away) while a name or an address is being typed.
    static void DrawKbButton()
    {
        var (x, y, r) = KbButton();
        Raylib.DrawCircleV(new(x, y), r, new Color(20, 30, 40, 170));
        Raylib.DrawCircleLinesV(new(x, y), r, new Color(241, 230, 200, 220));
        var col = new Color(241, 230, 200, 235);
        float w = r * 1.2f, h = r * 0.7f;
        Raylib.DrawRectangleRounded(new Rectangle(x - w / 2, y - h / 2, w, h), 0.25f, 4, col);
        var dark = new Color(20, 30, 40, 255);
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 5; i++)
                Raylib.DrawRectangle((int)(x - w / 2 + w * 0.1f + i * w * 0.18f), (int)(y - h * 0.32f + row * h * 0.34f), (int)Math.Max(2, w * 0.1f), (int)Math.Max(2, h * 0.16f), dark);
        Raylib.DrawRectangle((int)(x - w * 0.3f), (int)(y + h * 0.2f), (int)(w * 0.6f), (int)Math.Max(2, h * 0.14f), dark);
    }

    static void DrawEditBanner()
    {
        Raylib.DrawRectangle(0, 0, W, (int)(66 * U), new Color(10, 20, 30, 170));
        var d = EditDone(); var rs = EditReset();
        Pill(rs.X, rs.Y, rs.W, rs.H, "Reset", false);
        Pill(d.X, d.Y, d.W, d.H, "Done", true);
        LabelLeft("Drag a control to move it", 18 * U, 37 * U, 22 * U, new Color(255, 255, 255, 235));
    }

    static void Pill(float x, float y, float w, float h, string label, bool on)
    {
        Raylib.DrawRectangleRounded(new Rectangle(x, y + 3, w, h), 0.5f, 8, new Color(40, 30, 20, 220));
        Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), 0.5f, 8, on ? new Color(255, 205, 80, 255) : new Color(232, 215, 170, 255));
        Label(label, x + w / 2, y + h / 2, h * 0.46f, new Color(40, 30, 20, 255));
    }

    // A small line in the corner (Settings > Touch > Debug line), so a screenshot says which build this is and what the controls see.
    static void Stamp()
    {
        if (font.Texture.Id == 0) return;
        Raylib.DrawTextEx(font, $"{Build}  mode={mode}  controls={(Visible ? "on" : "off")}  text={(WantText ? "on" : "off")}  game={(Game != null ? "ok" : "none")}  fps={Raylib.GetFrameTime() switch { > 0 and var t => (int)(1 / t), _ => 0 }}", new(8, H - 22), 16, 0, new Color(255, 255, 255, 170));
    }
}
