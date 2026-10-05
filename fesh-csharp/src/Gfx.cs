using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

enum FontKind { Ui500, Ui600, Ui700, Note }

// Keyboard input, with an optional scripted layer used by the automated test run.
static class Inp
{
    static readonly HashSet<KeyboardKey> held = new(), tapped = new();
    public static Vector2? ScriptMouse;
    public static bool ScriptClick;
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
    public static void Tap(KeyboardKey k) => tapped.Add(k);
    public static void Hold(KeyboardKey k, bool on)
    {
        if (!on) { held.Remove(k); return; }
        if (held.Add(k)) tapped.Add(k);
    }
    public static void EndFrame() { tapped.Clear(); ScriptClick = false; }
}

// UI drawing in a fixed 1280x720 layout that is scaled and letterboxed into the window.
// Text is rasterized at the real on-screen size so it stays sharp at any window size.
static class Gfx
{
    public const float LW = 1280, LH = 720;
    public static float Z = 1, OX, OY;
    public static Vector2 Mouse;
    public static bool MouseMoved, Pressed, Down, OverUiPrev;
    static bool clickTaken;
    static List<Rectangle> now = new(), prev = new();
    static readonly Dictionary<(FontKind, int), Font> fonts = new();
    static readonly Dictionary<FontKind, byte[]> fontData = new();
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
        var logical = Inp.ScriptMouse ?? new Vector2((m.X - OX) / Z, (m.Y - OY) / Z);
        MouseMoved = logical != Mouse;
        Mouse = logical;
        Pressed = IsMouseButtonPressed(MouseButton.Left) || Inp.ScriptClick;
        Down = IsMouseButtonDown(MouseButton.Left);
        (prev, now) = (now, prev);
        now.Clear();
        OverUiPrev = prev.Any(r => Mouse.X >= r.X && Mouse.X < r.X + r.Width && Mouse.Y >= r.Y && Mouse.Y < r.Y + r.Height);
        clickTaken = false;
    }

    // ---------- Fonts and text ----------
    public static Font GetFont(FontKind k, float size)
    {
        int px = Math.Max(6, (int)MathF.Round(size * Z));
        if (fonts.TryGetValue((k, px), out var f)) return f;
        f = LoadFontFromMemory(".ttf", fontData[k], px, codepoints, codepoints.Length);
        SetTextureFilter(f.Texture, TextureFilter.Bilinear);
        // Pixelify Sans rasterizes its space far too narrow at many sizes, so words run together. Give it a real width.
        unsafe
        {
            for (int i = 0; i < f.GlyphCount; i++)
                if (f.Glyphs[i].Value == ' ' && f.Glyphs[i].AdvanceX < px * 0.25f) f.Glyphs[i].AdvanceX = (int)MathF.Round(px * 0.28f);
        }
        fonts[(k, px)] = f;
        return f;
    }

    public static void Text(string s, float x, float y, FontKind k, float size, Color c)
    {
        var f = GetFont(k, size);
        DrawTextEx(f, s, new Vector2(MathF.Round(OX + x * Z), MathF.Round(OY + y * Z)), f.BaseSize, 0, c);
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
        DrawTextPro(f, s, Gfx.P(cx, cy), new Vector2((cx - x) * Gfx.Z, (cy - y) * Gfx.Z), deg, f.BaseSize, 0, c);
    }

    public Vector2 Point(float x, float y)
    {
        float a = deg * MathF.PI / 180, dx = x - cx, dy = y - cy;
        return new Vector2(cx + dx * MathF.Cos(a) - dy * MathF.Sin(a), cy + dx * MathF.Sin(a) + dy * MathF.Cos(a));
    }
}
