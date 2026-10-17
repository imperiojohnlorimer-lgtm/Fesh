// The Raylib functions Fesh calls (91 of Raylib's 631), implemented on the Android backend. Same names and shapes as
// Raylib-cs, so `using static Raylib_cs.Raylib;` in the game's source resolves here.
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Fesh.Droid;

namespace Raylib_cs;

public static unsafe class Raylib
{
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static double lastFrame, frameTime = 1 / 60.0, frameStart;
    static int targetFps;

    // ---------- Window and loop ----------
    public static void SetConfigFlags(ConfigFlags flags) { }
    public static void SetTraceLogLevel(TraceLogLevel level) { }
    public static void InitWindow(int width, int height, string title) { Gl.Init(); frameStart = lastFrame = clock.Elapsed.TotalSeconds; }
    public static void CloseWindow() { }
    public static bool WindowShouldClose() => Gl.Quit;
    public static void SetExitKey(KeyboardKey key) { }
    public static void SetTargetFPS(int fps) { targetFps = fps; }
    public static void SetWindowIcon(Image image) { }
    public static void SetWindowMinSize(int width, int height) { }
    public static void SetWindowPosition(int x, int y) { }
    public static void SetWindowSize(int width, int height) { }
    public static void ToggleFullscreen() { }
    public static void ToggleBorderlessWindowed() { }
    public static bool IsWindowFocused() => Gl.Focused;
    public static bool IsWindowState(ConfigFlags flag) => flag is ConfigFlags.BorderlessWindowMode or ConfigFlags.FullscreenMode;
    public static int GetScreenWidth() => Gl.Width;
    public static int GetScreenHeight() => Gl.Height;
    public static int GetCurrentMonitor() => 0;
    public static int GetMonitorWidth(int monitor) => Gl.Width;
    public static int GetMonitorHeight(int monitor) => Gl.Height;
    public static Vector2 GetMonitorPosition(int monitor) => Vector2.Zero;
    public static double GetTime() => clock.Elapsed.TotalSeconds;
    public static float GetFrameTime() => (float)frameTime;
    public static string GetFileName(string path) => Path.GetFileName(path);

    public static void BeginDrawing() { Gl.BeginFrame(); }
    public static void EndDrawing()
    {
        TouchHud.Draw();
        Gl.EndFrame();
        AppHooks.EndOfFrame();
        InputBackend.Poll();
        double now = clock.Elapsed.TotalSeconds;
        int cap = TouchSettings.Fps < 60 && (targetFps == 0 || TouchSettings.Fps < targetFps) ? TouchSettings.Fps : targetFps;
        if (cap > 0)
        {
            double want = 1.0 / cap - (now - frameStart);
            if (want > 0.0015) { Thread.Sleep((int)(want * 1000) - 1); now = clock.Elapsed.TotalSeconds; }
        }
        frameTime = now - lastFrame;
        lastFrame = frameStart = now;
    }
    public static void ClearBackground(Color color) => Gl.Clear(color);
    public static void BeginScissorMode(int x, int y, int width, int height) => Gl.Scissor(true, x, y, width, height);
    public static void EndScissorMode() => Gl.Scissor(false, 0, 0, 0, 0);

    // ---------- Input ----------
    public static bool IsKeyDown(KeyboardKey key) => InputBackend.KeyDown(key);
    public static bool IsKeyPressed(KeyboardKey key) => InputBackend.KeyPressed(key);
    public static bool IsKeyPressedRepeat(KeyboardKey key) => false;
    public static int GetKeyPressed() => InputBackend.NextKey();
    public static int GetCharPressed() => InputBackend.NextChar();
    public static bool IsMouseButtonDown(MouseButton button) => button == MouseButton.Left && InputBackend.MouseDown;
    public static bool IsMouseButtonPressed(MouseButton button) => button == MouseButton.Left && InputBackend.MousePressed;
    public static Vector2 GetMousePosition() => InputBackend.Mouse;
    public static Vector2 GetMouseDelta() => InputBackend.MouseDelta;
    public static float GetMouseWheelMove() => InputBackend.Wheel;
    public static bool IsGamepadAvailable(int gamepad) => gamepad == 0 && InputBackend.PadAvailable;
    static readonly sbyte* noName = (sbyte*)NativeMemory.AllocZeroed(1);
    public static sbyte* GetGamepadName(int gamepad) => noName;
    public static bool IsGamepadButtonDown(int gamepad, GamepadButton button) => gamepad == 0 && InputBackend.PadDown(button);
    public static bool IsGamepadButtonPressed(int gamepad, GamepadButton button) => gamepad == 0 && InputBackend.PadPressed(button);
    public static float GetGamepadAxisMovement(int gamepad, GamepadAxis axis) => gamepad == 0 ? InputBackend.PadAxis(axis) : 0;

    // ---------- Colours and collisions ----------
    public static Color ColorLerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        byte L(byte x, byte y) => (byte)(x + (y - x) * t);
        return new Color(L(a.R, b.R), L(a.G, b.G), L(a.B, b.B), L(a.A, b.A));
    }
    public static bool CheckCollisionRecs(Rectangle a, Rectangle b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    // ---------- Shapes ----------
    public static void DrawRectangle(int x, int y, int w, int h, Color c) => DrawRectangleRec(new Rectangle(x, y, w, h), c);
    public static void DrawRectangleRec(Rectangle r, Color c) =>
        Gl.Quad(new(r.X, r.Y), new(r.X + r.Width, r.Y), new(r.X + r.Width, r.Y + r.Height), new(r.X, r.Y + r.Height), c);
    public static void DrawRectangleGradientV(int x, int y, int w, int h, Color top, Color bottom) => Gl.QuadGrad(x, y, w, h, top, bottom);

    public static void DrawRectanglePro(Rectangle r, Vector2 origin, float rotation, Color c)
    {
        float rad = rotation * MathF.PI / 180f, cs = MathF.Cos(rad), sn = MathF.Sin(rad);
        Vector2 P(float x, float y) { x -= origin.X; y -= origin.Y; return new(r.X + x * cs - y * sn, r.Y + x * sn + y * cs); }
        Gl.Quad(P(0, 0), P(r.Width, 0), P(r.Width, r.Height), P(0, r.Height), c);
    }

    public static void DrawRectangleRounded(Rectangle r, float roundness, int segments, Color c)
    {
        float radius = (r.Width > r.Height ? r.Height * roundness : r.Width * roundness) / 2;
        if (radius <= 0 || roundness <= 0) { DrawRectangleRec(r, c); return; }
        segments = Math.Max(segments, 4);
        var ring = new List<Vector2>();
        void Arc(float cx, float cy, float a0)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = (a0 + 90f * i / segments) * MathF.PI / 180f;
                ring.Add(new(cx + radius * MathF.Cos(a), cy + radius * MathF.Sin(a)));
            }
        }
        Arc(r.X + radius, r.Y + radius, 180);
        Arc(r.X + r.Width - radius, r.Y + radius, 270);
        Arc(r.X + r.Width - radius, r.Y + r.Height - radius, 0);
        Arc(r.X + radius, r.Y + r.Height - radius, 90);
        Gl.Fan(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), ring, c);
    }

    static List<Vector2> Ellipse(Vector2 centre, float rx, float ry, int segs)
    {
        var ring = new List<Vector2>(segs);
        for (int i = 0; i < segs; i++)
        {
            float a = 2 * MathF.PI * i / segs;
            ring.Add(new(centre.X + rx * MathF.Cos(a), centre.Y + ry * MathF.Sin(a)));
        }
        return ring;
    }
    static int CircleSegments(float radius) =>
        Math.Clamp((int)MathF.Ceiling(2 * MathF.PI / MathF.Acos(2 * MathF.Pow(1 - 0.5f / Math.Max(radius, 1f), 2) - 1)), 12, 96);

    public static void DrawCircleV(Vector2 centre, float radius, Color c) => Gl.Fan(centre, Ellipse(centre, radius, radius, CircleSegments(radius)), c);
    public static void DrawEllipse(int cx, int cy, float rh, float rv, Color c)
    {
        var centre = new Vector2(cx, cy);
        Gl.Fan(centre, Ellipse(centre, rh, rv, Math.Clamp(CircleSegments(Math.Max(rh, rv)), 24, 96)), c);
    }
    public static void DrawCircleLinesV(Vector2 centre, float radius, Color c)
    {
        var ring = Ellipse(centre, radius, radius, CircleSegments(radius));
        for (int i = 0; i < ring.Count; i++) DrawLineEx(ring[i], ring[(i + 1) % ring.Count], 1f, c);
    }

    public static void DrawLineEx(Vector2 a, Vector2 b, float thick, Color c)
    {
        var d = b - a;
        float len = d.Length();
        if (len <= 0) return;
        var n = new Vector2(-d.Y, d.X) / len * (thick / 2);
        Gl.Quad(a - n, b - n, b + n, a + n, c);
    }

    // Raylib fills only counter-clockwise triangles (the game draws both windings to be sure), so a clockwise one is skipped:
    // drawing both here would put a translucent triangle on twice.
    public static void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color c)
    {
        float cross = (v2.X - v1.X) * (v3.Y - v1.Y) - (v2.Y - v1.Y) * (v3.X - v1.X);
        if (cross < 0) Gl.Tri(v1, v2, v3, c);
    }

    public static void DrawSplineSegmentBezierQuadratic(Vector2 p1, Vector2 c2, Vector2 p3, float thick, Color c)
    {
        const int divs = 24;
        Vector2 prev = p1;
        for (int i = 1; i <= divs; i++)
        {
            float t = i / (float)divs, u = 1 - t;
            var p = u * u * p1 + 2 * u * t * c2 + t * t * p3;
            DrawLineEx(prev, p, thick, c);
            prev = p;
        }
    }

    // ---------- Textures ----------
    public static Texture2D LoadTextureFromImage(Image image) => Gl.CreateTexture(image.Width, image.Height, (IntPtr)image.Data);
    public static void UnloadTexture(Texture2D texture) => Gl.DeleteTexture(texture);
    public static void UpdateTexture<T>(Texture2D texture, T[] pixels) where T : unmanaged
    {
        fixed (T* p = pixels) Gl.UpdateTexture(texture, (IntPtr)p);
    }
    public static void UpdateTexture<T>(Texture2D texture, ReadOnlySpan<T> pixels) where T : unmanaged
    {
        fixed (T* p = pixels) Gl.UpdateTexture(texture, (IntPtr)p);
    }
    public static void SetTextureFilter(Texture2D texture, TextureFilter filter) => Gl.SetFilter(texture, filter);
    public static void SetTextureWrap(Texture2D texture, TextureWrap wrap) => Gl.SetWrap(texture, wrap == TextureWrap.Repeat);
    public static void GenTextureMipmaps(ref Texture2D texture) => Gl.Mipmaps(ref texture);

    public static void DrawTexturePro(Texture2D tex, Rectangle src, Rectangle dst, Vector2 origin, float rotation, Color tint)
    {
        if (tex.Id == 0) return;
        float u0 = src.X / tex.Width, v0 = src.Y / tex.Height, u1 = (src.X + src.Width) / tex.Width, v1 = (src.Y + src.Height) / tex.Height;
        float rad = rotation * MathF.PI / 180f, cs = MathF.Cos(rad), sn = MathF.Sin(rad);
        Vector2 P(float x, float y)
        {
            if (rotation == 0) return new(dst.X - origin.X + x, dst.Y - origin.Y + y);
            x -= origin.X; y -= origin.Y;
            return new(dst.X + x * cs - y * sn, dst.Y + x * sn + y * cs);
        }
        Gl.TexQuad(tex, P(0, 0), P(dst.Width, 0), P(dst.Width, dst.Height), P(0, dst.Height), u0, v0, u1, v1, tint);
    }

    // ---------- Images (RGBA, in unmanaged memory) ----------
    public static Image GenImageColor(int width, int height, Color color)
    {
        var data = (Color*)NativeMemory.Alloc((nuint)(width * height * 4));
        for (int i = 0; i < width * height; i++) data[i] = color;
        return new Image { Data = data, Width = width, Height = height, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
    }
    public static void ImageDrawPixel(ref Image dst, int x, int y, Color color)
    {
        if (dst.Data == null || x < 0 || y < 0 || x >= dst.Width || y >= dst.Height) return;
        ((Color*)dst.Data)[y * dst.Width + x] = color;
    }
    public static void UnloadImage(Image image) { if (image.Data != null) NativeMemory.Free(image.Data); }

    // ---------- Text ----------
    public static Font LoadFontFromMemory(string fileType, byte[] fileData, int fontSize, int[] codepoints, int codepointCount) =>
        FontBackend.Load(fileData, fontSize, codepoints, codepointCount);
    public static void UnloadFont(Font font) => FontBackend.Unload(font);
    public static int GetGlyphIndex(Font font, int character) => FontBackend.GlyphIndex(font, character);
    public static Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing) =>
        new(FontBackend.Measure(font, text, fontSize, spacing), fontSize + text.Count(c => c == (char)10) * (fontSize + 2));
    public static void DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint) =>
        FontBackend.Draw(font, text, position, Vector2.Zero, 0, fontSize, spacing, tint);
    public static void DrawTextPro(Font font, string text, Vector2 position, Vector2 origin, float rotation, float fontSize, float spacing, Color tint) =>
        FontBackend.Draw(font, text, position, origin, rotation, fontSize, spacing, tint);

    // ---------- Audio ----------
    public static void InitAudioDevice() => AudioBackend.Init();
    public static void CloseAudioDevice() => AudioBackend.Close();
    public static bool IsAudioDeviceReady() => AudioBackend.Ready;
    public static void SetMasterVolume(float volume) { }   // only the desktop test runs use it
    public static Wave LoadWaveFromMemory(string fileType, byte[] fileData) => AudioBackend.LoadWav(fileData);
    public static void UnloadWave(Wave wave) => AudioBackend.UnloadWave(wave);
    public static Sound LoadSoundFromWave(Wave wave) => AudioBackend.LoadSound(wave);
    public static void UnloadSound(Sound sound) => AudioBackend.Unload(sound);
    public static void PlaySound(Sound sound) => AudioBackend.Play(sound);
    public static void StopSound(Sound sound) => AudioBackend.Stop(sound);
    public static bool IsSoundPlaying(Sound sound) => AudioBackend.IsPlaying(sound);
    public static void SetSoundVolume(Sound sound, float volume) => AudioBackend.SetVolume(sound, volume);
}
