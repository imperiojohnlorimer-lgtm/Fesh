using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

enum FontKind { Ui500, Ui600, Ui700, Note }

// Keyboard and gamepad input, with an optional scripted layer used by the automated test run.
static class Inp
{
    static readonly HashSet<KeyboardKey> held = new(), tapped = new();
    static readonly List<KeyboardKey> framePressed = new();
    public static Vector2? ScriptMouse;
    public static bool ScriptClick;
    public static bool ScriptClickNext { get; set; }   // the autotest clicks: it lands next frame, when Gfx works out the mouse
    public static bool? ScriptFocused { get; set; }    // the autotest decides whether the window has focus
    public static bool Focused => ScriptFocused ?? IsWindowFocused();
    public static readonly Queue<int> ScriptChars = new();

    // Characters typed this frame, for the name field.
    public static List<int> Typed()
    {
        var list = new List<int>();
        for (int c = GetCharPressed(); c != 0; c = GetCharPressed()) list.Add(c);
        while (ScriptChars.Count > 0) list.Add(ScriptChars.Dequeue());
        return list;
    }

    public static bool Down(KeyboardKey k) => IsKeyDown(k) || held.Contains(k);
    public static bool Pressed(KeyboardKey k) => IsKeyPressed(k) || tapped.Contains(k);
    // Any key pressed this frame, for rebinding a control (null if none).
    public static KeyboardKey? FirstPressed()
    {
        foreach (var k in tapped) return k;
        return framePressed.Count > 0 ? framePressed[0] : null;
    }
    public static void Tap(KeyboardKey k) { tapped.Add(k); UsingPad = false; }
    public static void Hold(KeyboardKey k, bool on)
    {
        if (!on) { held.Remove(k); return; }
        if (held.Add(k)) tapped.Add(k);
        UsingPad = false;
    }

    /* ---------- Gamepad (the first one plugged in) ---------- */
    // In menus (CursorUi) the left stick or D-pad steers a pointer and A clicks, so every mouse-driven screen works.
    static readonly HashSet<GamepadButton> padTapped = new(), padQueued = new(), padHeld = new();
    public static Vector2? ScriptStick { get; set; }
    // The right stick, flicked left (-1) or right (+1) this frame: steps through the hotbar (Hotbar.cs). It has to come
    // back to the middle before it flicks again.
    public static float? ScriptRightX { get; set; }
    public static int RightFlick { get; private set; }
    static bool rightOut;
    public static bool UsingPad;          // the last input came from the gamepad: prompts name its buttons
    public static Vector2? PadCursor;     // the pointer the pad steers in menus (null: the mouse has it)
    public static bool CursorUi, PadClick, PadHold;
    static Vector2 stick, lastStick;
    const float Dead = 0.35f;

    // The first gamepad plugged in (Raylib numbers them 0 to 3), or -1.
    public static int PadIndex { get; private set; } = -1;
    static bool Pad => PadIndex >= 0;
    public static bool PadPressed(GamepadButton b) => padTapped.Contains(b) || Pad && IsGamepadButtonPressed(PadIndex, b);
    public static bool PadDown(GamepadButton b) => padHeld.Contains(b) || Pad && IsGamepadButtonDown(PadIndex, b);
    public static Vector2 Stick => stick;
    // The stick pushed into a direction this frame (0 up, 1 right, 2 down, 3 left), for one-step moves like the chest arrows.
    public static bool StickPressed(int dir) => StickDir(stick) == dir && StickDir(lastStick) != dir;
    static int StickDir(Vector2 s) => s.Length() < 0.6f ? -1 : MathF.Abs(s.X) > MathF.Abs(s.Y) ? (s.X > 0 ? 1 : 3) : (s.Y > 0 ? 2 : 0);
    // Scripted presses count from the next frame, so they're there when the pointer and clicks are worked out.
    public static void TapPad(GamepadButton b) { padQueued.Add(b); UsingPad = true; }
    public static void HoldPad(GamepadButton b, bool on)
    {
        if (!on) { padHeld.Remove(b); return; }
        if (padHeld.Add(b)) padQueued.Add(b);
        UsingPad = true;
    }

    // Once a frame, before anything reads input. cursorUi: a menu, panel or the title is up.
    public static void BeginFrame(float dt, bool cursorUi)
    {
        ScriptClick = ScriptClickNext;
        ScriptClickNext = false;
        framePressed.Clear();
        for (int k = GetKeyPressed(); k != 0; k = GetKeyPressed()) framePressed.Add((KeyboardKey)k);
        if (PadIndex < 0 || !IsGamepadAvailable(PadIndex)) PadIndex = Enumerable.Range(0, 4).FirstOrDefault(i => IsGamepadAvailable(i), -1);
        padTapped.UnionWith(padQueued);
        padQueued.Clear();
        lastStick = stick;
        stick = ScriptStick ?? (Pad ? new Vector2(GetGamepadAxisMovement(PadIndex, GamepadAxis.LeftX), GetGamepadAxisMovement(PadIndex, GamepadAxis.LeftY)) : Vector2.Zero);
        float rx = ScriptRightX ?? (Pad ? GetGamepadAxisMovement(PadIndex, GamepadAxis.RightX) : 0);
        RightFlick = 0;
        if (MathF.Abs(rx) < 0.3f) rightOut = false;
        else if (MathF.Abs(rx) > 0.65f && !rightOut) { rightOut = true; RightFlick = rx > 0 ? 1 : -1; UsingPad = true; }
        if (stick.Length() < Dead) stick = Vector2.Zero;
        bool padUsed = stick != Vector2.Zero || padTapped.Count > 0;
        if (Pad) for (var b = GamepadButton.LeftFaceUp; b <= GamepadButton.RightThumb; b++) padUsed |= IsGamepadButtonPressed(PadIndex, b);
        if (padUsed) UsingPad = true;
        if (framePressed.Count > 0 || IsMouseButtonPressed(MouseButton.Left) || GetMouseDelta() != Vector2.Zero) UsingPad = false;
        // Moving or clicking the real mouse takes the pointer back from the pad.
        if (GetMouseDelta() != Vector2.Zero || IsMouseButtonPressed(MouseButton.Left)) PadCursor = null;
        CursorUi = cursorUi;
        if (cursorUi)
        {
            var move = stick;
            if (PadDown(GamepadButton.LeftFaceUp)) move.Y -= 1;
            if (PadDown(GamepadButton.LeftFaceDown)) move.Y += 1;
            if (PadDown(GamepadButton.LeftFaceLeft)) move.X -= 1;
            if (PadDown(GamepadButton.LeftFaceRight)) move.X += 1;
            if (move != Vector2.Zero)
            {
                var p = (PadCursor ?? Gfx.Mouse) + move * 820 * dt;
                PadCursor = new Vector2(Math.Clamp(p.X, 0, Gfx.LW - 1), Math.Clamp(p.Y, 0, Gfx.LH - 1));
            }
        }
        PadClick = cursorUi && PadPressed(GamepadButton.RightFaceDown);
        PadHold = cursorUi && PadDown(GamepadButton.RightFaceDown);
        if (PadClick) PadCursor ??= Gfx.Mouse;
    }

    public static void EndFrame() { tapped.Clear(); padTapped.Clear(); ScriptClick = false; }
}

// UI drawing in a fixed 1280x720 layout that is scaled and letterboxed into the window.
// Text is rasterized at the real on-screen size so it stays sharp at any window size.
static class Gfx
{
    public const float LW = 1280, LH = 720;
    public static float Z = 1, OX, OY;
    public static Vector2 Mouse;
    public static bool MouseMoved, Pressed, Down, OverUiPrev;
    public static readonly Dictionary<string, Rectangle> Seen = new();   // debug builds: where each button was last drawn, by label
    static bool clickTaken;
    static List<Rectangle> now = new(), prev = new();
    static readonly Dictionary<(bool, FontKind, int), Font> fonts = new();
    static readonly Dictionary<FontKind, byte[]> fontData = new(), clearData = new();
    // The Clear font (Settings, "Font"): Atkinson Hyperlegible, made by the Braille Institute for low-vision readers,
    // in place of the pixel and typewriter faces. Every layout was measured with the pixel font, so each kind is drawn at
    // the size that makes it as wide as the pixel one (Fit), and moved so its capitals sit at the same height.
    public static bool Clear => Settings.Data.clearFont;
    static readonly Dictionary<FontKind, (float scale, float dy)> clearFit = new();
    static readonly Dictionary<string, Texture2D> art = new();
    static int[] codepoints;
    static Texture2D cork;

    public static void Init()
    {
        var asm = typeof(Gfx).Assembly;
        byte[] Res(string name)
        {
            using var s = asm.GetManifestResourceStream(name);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
        fontData[FontKind.Ui500] = Res("PixelifySans-Medium.ttf");
        fontData[FontKind.Ui600] = Res("PixelifySans-SemiBold.ttf");
        fontData[FontKind.Ui700] = Res("PixelifySans-Bold.ttf");
        fontData[FontKind.Note] = Res("SpecialElite-Regular.ttf");
        byte[] regular = Res("AtkinsonHyperlegible-Regular.ttf"), bold = Res("AtkinsonHyperlegible-Bold.ttf");
        clearData[FontKind.Ui500] = regular;
        clearData[FontKind.Ui600] = bold;
        clearData[FontKind.Ui700] = bold;
        clearData[FontKind.Note] = regular;
        var cps = Enumerable.Range(32, 95).ToList();
        cps.AddRange(new[] { 0xB7, 0xD7, 0xE9, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026 });
        codepoints = cps.ToArray();
        cork = MakeCork();
    }

    public static void Shutdown()
    {
        foreach (var f in fonts.Values) UnloadFont(f);
        foreach (var t in art.Values) UnloadTexture(t);
        UnloadTexture(cork);
    }

    public static void BeginFrame()
    {
        float w = GetScreenWidth(), h = GetScreenHeight();
        Z = MathF.Min(w / LW, h / LH);
        OX = MathF.Floor((w - LW * Z) / 2);
        OY = MathF.Floor((h - LH * Z) / 2);
        var m = GetMousePosition();
        var logical = Inp.ScriptMouse ?? (Inp.CursorUi && Inp.PadCursor is Vector2 pad ? pad : new Vector2((m.X - OX) / Z, (m.Y - OY) / Z));
        MouseMoved = logical != Mouse;
        Mouse = logical;
        Pressed = IsMouseButtonPressed(MouseButton.Left) || Inp.ScriptClick || Inp.PadClick;
        Down = IsMouseButtonDown(MouseButton.Left) || Inp.PadHold;
        (prev, now) = (now, prev);
        now.Clear();
        OverUiPrev = prev.Any(r => Mouse.X >= r.X && Mouse.X < r.X + r.Width && Mouse.Y >= r.Y && Mouse.Y < r.Y + r.Height);
        clickTaken = false;
    }

    // ---------- Fonts and text ----------
    public static Font GetFont(FontKind k, float size)
    {
        float scale = Clear ? ClearFit(k).scale : 1;
        return LoadFont(Clear, k, Math.Max(6, (int)MathF.Round(size * Z * scale)));
    }

    static Font LoadFont(bool clear, FontKind k, int px)
    {
        if (fonts.TryGetValue((clear, k, px), out var f)) return f;
        f = LoadFontFromMemory(".ttf", clear ? clearData[k] : fontData[k], px, codepoints, codepoints.Length);
        SetTextureFilter(f.Texture, TextureFilter.Bilinear);
        // Pixelify Sans rasterizes its space far too narrow at many sizes, so words run together. Give it a real width.
        if (!clear)
            unsafe
            {
                for (int i = 0; i < f.GlyphCount; i++)
                    if (f.Glyphs[i].Value == ' ' && f.Glyphs[i].AdvanceX < px * 0.25f) f.Glyphs[i].AdvanceX = (int)MathF.Round(px * 0.28f);
            }
        fonts[(clear, k, px)] = f;
        return f;
    }

    // How much bigger (or smaller) the Clear font is drawn so a line of it is as wide as the pixel font's, and how far
    // down (in sizes) it moves so the middle of its capitals lines up with the pixel font's: buttons stay centred.
    public static (float scale, float dy) ClearFit(FontKind k)
    {
        if (clearFit.TryGetValue(k, out var fit)) return fit;
        const int refPx = 48;
        const string sample = "The quick brown fox jumps over the lazy dog. Sold 3 fish for 120 coins!";
        Font p = LoadFont(false, k, refPx), c = LoadFont(true, k, refPx);
        float scale = Math.Clamp(MeasureTextEx(p, sample, refPx, 0).X / MeasureTextEx(c, sample, refPx, 0).X, 0.8f, 1.3f);
        static unsafe float CapMid(Font f)
        {
            int i = GetGlyphIndex(f, 'H');
            return (f.Glyphs[i].OffsetY + f.Recs[i].Height / 2f) / refPx;
        }
        return clearFit[k] = (scale, CapMid(p) - scale * CapMid(c));
    }

    // How far text of this kind and size is moved down from where it was asked for (only the Clear font is).
    public static float TextDy(FontKind k, float size) => Clear ? ClearFit(k).dy * size : 0;

    public static void Text(string s, float x, float y, FontKind k, float size, Color c)
    {
        var f = GetFont(k, size);
        DrawTextEx(f, s, new Vector2(MathF.Round(OX + x * Z), MathF.Round(OY + (y + TextDy(k, size)) * Z)), f.BaseSize, 0, c);
    }

    public static void TextCenter(string s, float cx, float y, FontKind k, float size, Color c) =>
        Text(s, cx - Measure(s, k, size) / 2, y, k, size, c);

    public static float Measure(string s, FontKind k, float size)
    {
        var f = GetFont(k, size);
        return MeasureTextEx(f, s, f.BaseSize, 0).X / Z;
    }

    public static List<string> Wrap(string s, FontKind k, float size, float maxW)
    {
        var lines = new List<string>();
        foreach (var para in s.Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' '))
            {
                var test = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(test, k, size) > maxW) { lines.Add(line); line = word; }
                else line = test;
            }
            lines.Add(line);
        }
        return lines;
    }

    public static string Ellipsize(string s, FontKind k, float size, float maxW)
    {
        if (Measure(s, k, size) <= maxW) return s;
        while (s.Length > 1 && Measure(s + "…", k, size) > maxW) s = s[..^1];
        return s.TrimEnd() + "…";
    }

    // ---------- Shapes ----------
    public static Rectangle S(float x, float y, float w, float h) => new(OX + x * Z, OY + y * Z, w * Z, h * Z);
    public static Vector2 P(float x, float y) => new(OX + x * Z, OY + y * Z);

    public static void Rect(float x, float y, float w, float h, Color c, float r = 0)
    {
        if (w <= 0 || h <= 0 || c.A == 0) return;
        var rec = S(x, y, w, h);
        if (r <= 0) DrawRectangleRec(rec, c);
        else DrawRectangleRounded(rec, Math.Min(1f, 2 * r / Math.Min(w, h)), 8, c);
    }

    // A filled box with a solid border and an optional drop shadow straight down, like the web version's buttons and cards.
    public static void Box(float x, float y, float w, float h, Color fill, Color border, float bw, float r, float shadow = 0)
    {
        if (shadow > 0) Rect(x, y + shadow, w, h, border, r);
        Rect(x, y, w, h, border, r);
        Rect(x + bw, y + bw, w - 2 * bw, h - 2 * bw, fill, Math.Max(0, r - bw));
    }

    public static void Dashed(float x, float y, float w, float h, float t, float dash, Color c)
    {
        for (float i = 0; i < w; i += dash * 2) { Rect(x + i, y, Math.Min(dash, w - i), t, c); Rect(x + i, y + h - t, Math.Min(dash, w - i), t, c); }
        for (float i = 0; i < h; i += dash * 2) { Rect(x, y + i, t, Math.Min(dash, h - i), c); Rect(x + w - t, y + i, t, Math.Min(dash, h - i), c); }
    }

    public static void Circle(float x, float y, float r, Color c) => DrawCircleV(P(x, y), r * Z, c);
    public static void Line(float x0, float y0, float x1, float y1, float thick, Color c) => DrawLineEx(P(x0, y0), P(x1, y1), thick * Z, c);
    // Either winding: raylib only fills counter-clockwise triangles, so both are drawn.
    public static void Triangle(float ax, float ay, float bx, float by, float cx, float cy, Color c)
    {
        DrawTriangle(P(ax, ay), P(bx, by), P(cx, cy), c);
        DrawTriangle(P(ax, ay), P(cx, cy), P(bx, by), c);
    }

    public static void Cork(float x, float y, float w, float h) =>
        DrawTexturePro(cork, new Rectangle(0, 0, w, h), S(x, y, w, h), Vector2.Zero, 0, Color.White);

    // ---------- Clicks ----------
    public static void Block(float x, float y, float w, float h) => now.Add(new Rectangle(x, y, w, h));
    public static bool Hover(float x, float y, float w, float h) => Mouse.X >= x && Mouse.X < x + w && Mouse.Y >= y && Mouse.Y < y + h;
    public static bool Click(float x, float y, float w, float h)
    {
        Block(x, y, w, h);
        if (!Pressed || clickTaken || !Hover(x, y, w, h)) return false;
        clickTaken = true;
        return true;
    }
    public static bool PressedOutside(float x, float y, float w, float h) => Pressed && !clickTaken && !Hover(x, y, w, h);

    // ---------- Textures ----------
    public static Texture2D ToTexture(Color[] px, int w, int h, TextureFilter filter = TextureFilter.Bilinear)
    {
        var img = GenImageColor(w, h, Color.Blank);
        var tex = LoadTextureFromImage(img);
        UnloadImage(img);
        UpdateTexture(tex, px);
        SetTextureFilter(tex, filter);
        return tex;
    }

    public static void Portrait(string id, bool sil, float x, float y, float w, float h)
    {
        int pw = Math.Max(1, (int)MathF.Round(w * Z)), ph = Math.Max(1, (int)MathF.Round(h * Z));
        string key = $"{id}:{sil}:{pw}x{ph}";
        if (!art.TryGetValue(key, out var tex))
        {
            float k = pw / w;
            var g = new VCanvas(pw, ph) { ShadowScale = k };
            g.Scale(k);
            CreatureArt.Scene(g, w, h, id, sil);
            tex = ToTexture(g.ToColors(), pw, ph);
            art[key] = tex;
        }
        DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), S(x, y, w, h), Vector2.Zero, 0, Color.White);
        if (sil) TextCenter("?", x + w / 2, y + h / 2 - h * 0.15f, FontKind.Ui700, h * 0.3f, Pal.C("rgba(241,230,200,0.55)"));
    }

    static Texture2D MakeCork()
    {
        const int n = 77;
        var px = new Color[n * n];
        var baseC = Pal.C("#b98a55");
        Array.Fill(px, baseC);
        void Dot(int cx, int cy, Color c, float a)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    float k = dx == 0 && dy == 0 ? a : (dx == 0 || dy == 0 ? a * 0.45f : a * 0.15f);
                    int i = ((cy + dy + n) % n) * n + (cx + dx + n) % n;
                    px[i] = Pix.Mix(px[i], c, k);
                }
        }
        for (int y = 0; y < n; y += 7) for (int x = 0; x < n; x += 7) Dot(x, y, Color.Black, 0.09f);
        for (int y = 4; y < n; y += 11) for (int x = 3; x < n; x += 11) Dot(x, y, Color.White, 0.08f);
        var tex = ToTexture(px, n, n);
        SetTextureWrap(tex, TextureWrap.Repeat);
        return tex;
    }
}

// Draws rectangles and text rotated by a few degrees around a center point, for the pinned case board notes.
readonly struct Tilt
{
    readonly float cx, cy, deg;
    public Tilt(float cx, float cy, float deg) { this.cx = cx; this.cy = cy; this.deg = deg; }

    public void Rect(float x, float y, float w, float h, Color c) =>
        DrawRectanglePro(new Rectangle(Gfx.OX + cx * Gfx.Z, Gfx.OY + cy * Gfx.Z, w * Gfx.Z, h * Gfx.Z),
            new Vector2((cx - x) * Gfx.Z, (cy - y) * Gfx.Z), deg, c);

    public void Text(string s, float x, float y, FontKind k, float size, Color c)
    {
        var f = Gfx.GetFont(k, size);
        DrawTextPro(f, s, Gfx.P(cx, cy), new Vector2((cx - x) * Gfx.Z, (cy - y - Gfx.TextDy(k, size)) * Gfx.Z), deg, f.BaseSize, 0, c);
    }

    public Vector2 Point(float x, float y)
    {
        float a = deg * MathF.PI / 180, dx = x - cx, dy = y - cy;
        return new Vector2(cx + dx * MathF.Cos(a) - dy * MathF.Sin(a), cy + dx * MathF.Sin(a) + dy * MathF.Cos(a));
    }
}
