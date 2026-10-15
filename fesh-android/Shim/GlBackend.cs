// The Android drawing backend: an EGL surface on a SurfaceView and a small OpenGL ES 2 batch renderer that stands in for
// Raylib's immediate-mode drawing. It all runs on the game's own thread, which owns the GL context.
using System.Numerics;
using System.Runtime.InteropServices;
using Android.Opengl;
using Android.Runtime;
using Android.Views;
using Raylib_cs;

namespace Fesh.Droid;

internal static unsafe class Gl
{
    // ---------- Window and surface (set by the activity on the UI thread, read by the game thread) ----------
    static readonly object gate = new();
    static ISurfaceHolder holder;
    static int surfW, surfH;
    static bool surfaceDirty;       // the activity gave us a new (or no) surface since the last frame
    static bool focused = true;
    public static volatile bool Quit;

    public static void SurfaceChanged(ISurfaceHolder h, int w, int hgt)
    {
        lock (gate) { holder = h; surfW = w; surfH = hgt; surfaceDirty = true; Monitor.PulseAll(gate); }
    }
    public static void SurfaceLost()
    {
        lock (gate) { holder = null; surfaceDirty = true; Monitor.PulseAll(gate); }
    }
    public static void SetFocused(bool f) { focused = f; }
    public static bool Focused => focused;
    // The game and the controls live inside the safe area (the screen minus any notch or camera cut-out).
    static volatile int safeL, safeT, safeR, safeB;
    public static void SetInsets(int l, int t, int r, int b) { safeL = l; safeT = t; safeR = r; safeB = b; }
    public static int InsetLeft => safeL;
    public static int InsetTop => safeT;
    public static int Width => Math.Max(1, w - safeL - safeR);
    public static int Height => Math.Max(1, h - safeT - safeB);

    // ---------- EGL ----------
    static EGLDisplay display;
    static EGLContext context;
    static EGLSurface eglSurface;
    static EGLConfig config;
    static bool glReady;
    static int w = 1, h = 1;

    // Blocks until the activity has a surface, then makes the GL context current on this thread.
    public static void Init()
    {
        display = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
        var ver = new int[2];
        EGL14.EglInitialize(display, ver, 0, ver, 1);
        var attribs = new[]
        {
            EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit, EGL14.EglSurfaceType, EGL14.EglWindowBit,
            EGL14.EglRedSize, 8, EGL14.EglGreenSize, 8, EGL14.EglBlueSize, 8, EGL14.EglAlphaSize, 0,
            EGL14.EglDepthSize, 0, EGL14.EglNone,
        };
        var configs = new EGLConfig[1];
        var num = new int[1];
        EGL14.EglChooseConfig(display, attribs, 0, configs, 0, 1, num, 0);
        config = configs[0];
        context = EGL14.EglCreateContext(display, config, EGL14.EglNoContext, new[] { EGL14.EglContextClientVersion, 2, EGL14.EglNone }, 0);
        AttachSurface(wait: true);
    }

    // Makes the surface the activity has now current (waiting for one if it has none), and rebuilds nothing else:
    // the context, textures and buffers outlive the surface, so backgrounding the app loses nothing.
    static void AttachSurface(bool wait)
    {
        ISurfaceHolder hold;
        lock (gate)
        {
            while (holder == null && !Quit && wait) { Monitor.Exit(gate); try { AppHooks.EndOfFrame(); } finally { Monitor.Enter(gate); } if (holder != null || Quit) break; Monitor.Wait(gate, 250); }   // a save asked for as the app left runs here, while we wait
            hold = holder;
            w = Math.Max(1, surfW); h = Math.Max(1, surfH);
            surfaceDirty = false;
        }
        if (eglSurface != null)
        {
            EGL14.EglMakeCurrent(display, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
            EGL14.EglDestroySurface(display, eglSurface);
            eglSurface = null;
        }
        if (hold == null) return;
        eglSurface = EGL14.EglCreateWindowSurface(display, config, hold.Surface, new[] { EGL14.EglNone }, 0);
        EGL14.EglMakeCurrent(display, eglSurface, eglSurface, context);
        EGL14.EglSwapInterval(display, 1);
        if (!glReady) { BuildGl(); glReady = true; }
    }

    // ---------- Shader and batch ----------
    static int program, vbo, locScreen, locTex, whiteTex;
    const int Stride = 8;            // x, y, u, v, r, g, b, a
    static float[] verts = new float[Stride * 6 * 2048];
    static int count;                // floats in verts
    static int boundTex = -1;
    static Java.Nio.ByteBuffer vbuf;
    static IntPtr vbufPtr;
    static bool scissorOn;
    static int scX, scY, scW, scH;

    static void BuildGl()
    {
        const string vs = "attribute vec2 aPos; attribute vec2 aUv; attribute vec4 aCol; uniform vec2 uScreen; varying vec2 vUv; varying vec4 vCol;" +
            "void main(){ vec2 p = aPos / uScreen * 2.0 - 1.0; gl_Position = vec4(p.x, -p.y, 0.0, 1.0); vUv = aUv; vCol = aCol; }";
        const string fs = "precision mediump float; uniform sampler2D uTex; varying vec2 vUv; varying vec4 vCol;" +
            "void main(){ gl_FragColor = texture2D(uTex, vUv) * vCol; }";
        int Shader(int type, string src)
        {
            int s = GLES20.GlCreateShader(type);
            GLES20.GlShaderSource(s, src);
            GLES20.GlCompileShader(s);
            var ok = new int[1];
            GLES20.GlGetShaderiv(s, GLES20.GlCompileStatus, ok, 0);
            if (ok[0] == 0) throw new Exception("shader: " + GLES20.GlGetShaderInfoLog(s));
            return s;
        }
        program = GLES20.GlCreateProgram();
        GLES20.GlAttachShader(program, Shader(GLES20.GlVertexShader, vs));
        GLES20.GlAttachShader(program, Shader(GLES20.GlFragmentShader, fs));
        GLES20.GlBindAttribLocation(program, 0, "aPos");
        GLES20.GlBindAttribLocation(program, 1, "aUv");
        GLES20.GlBindAttribLocation(program, 2, "aCol");
        GLES20.GlLinkProgram(program);
        GLES20.GlUseProgram(program);
        locScreen = GLES20.GlGetUniformLocation(program, "uScreen");
        locTex = GLES20.GlGetUniformLocation(program, "uTex");
        GLES20.GlUniform1i(locTex, 0);
        var b = new int[1];
        GLES20.GlGenBuffers(1, b, 0);
        vbo = b[0];
        GLES20.GlDisable(GLES20.GlDepthTest);
        GLES20.GlEnable(GLES20.GlBlend);
        GLES20.GlBlendFunc(GLES20.GlSrcAlpha, GLES20.GlOneMinusSrcAlpha);
        GLES20.GlPixelStorei(GLES20.GlUnpackAlignment, 1);
        var white = Color.White;
        whiteTex = (int)CreateTexture(1, 1, (IntPtr)(&white)).Id;
        GLES20.GlActiveTexture(GLES20.GlTexture0);
    }

    // ---------- Frames ----------
    public static void BeginFrame()
    {
        bool dirty; lock (gate) dirty = surfaceDirty;
        if (dirty || eglSurface == null) AttachSurface(wait: true);
        GLES20.GlViewport(safeL, safeB, Width, Height);
        GLES20.GlUseProgram(program);
        GLES20.GlUniform2f(locScreen, Width, Height);
        boundTex = -1;
        count = 0;
        scissorOn = false;
        GLES20.GlDisable(GLES20.GlScissorTest);
    }

    public static void EndFrame()
    {
        Flush();
        if (eglSurface != null && !EGL14.EglSwapBuffers(display, eglSurface))
        {
            // The surface went away under us (the app was backgrounded): wait for a new one next frame.
            lock (gate) surfaceDirty = true;
        }
    }

    public static void Clear(Color c)
    {
        Flush();
        GLES20.GlClearColor(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        GLES20.GlClear(GLES20.GlColorBufferBit);
    }

    public static void Scissor(bool on, int x, int y, int sw, int sh)
    {
        Flush();
        scissorOn = on;
        if (on) { scX = x; scY = y; scW = sw; scH = sh; GLES20.GlEnable(GLES20.GlScissorTest); GLES20.GlScissor(safeL + x, safeB + Height - (y + sh), Math.Max(0, sw), Math.Max(0, sh)); }
        else GLES20.GlDisable(GLES20.GlScissorTest);
    }

    static void Flush()
    {
        if (count == 0) return;
        int bytes = count * 4;
        if (vbuf == null || vbuf.Capacity() < bytes)
        {
            vbuf = Java.Nio.ByteBuffer.AllocateDirect(Math.Max(bytes, verts.Length * 4)).Order(Java.Nio.ByteOrder.NativeOrder());
            vbufPtr = JNIEnv.GetDirectBufferAddress(vbuf.Handle);
        }
        Marshal.Copy(verts, 0, vbufPtr, count);
        vbuf.Position(0);
        GLES20.GlBindBuffer(GLES20.GlArrayBuffer, vbo);
        GLES20.GlBufferData(GLES20.GlArrayBuffer, bytes, vbuf, GLES20.GlStreamDraw);
        GLES20.GlEnableVertexAttribArray(0);
        GLES20.GlEnableVertexAttribArray(1);
        GLES20.GlEnableVertexAttribArray(2);
        GLES20.GlVertexAttribPointer(0, 2, GLES20.GlFloat, false, Stride * 4, 0);
        GLES20.GlVertexAttribPointer(1, 2, GLES20.GlFloat, false, Stride * 4, 8);
        GLES20.GlVertexAttribPointer(2, 4, GLES20.GlFloat, false, Stride * 4, 16);
        GLES20.GlDrawArrays(GLES20.GlTriangles, 0, count / Stride);
        count = 0;
    }

    static void Use(int tex)
    {
        if (tex == boundTex) return;
        Flush();
        GLES20.GlBindTexture(GLES20.GlTexture2d, tex);
        boundTex = tex;
    }

    static void Vert(float x, float y, float u, float v, Color c)
    {
        if (count + Stride > verts.Length) Array.Resize(ref verts, verts.Length * 2);   // never flush mid-triangle
        int i = count;
        verts[i] = x; verts[i + 1] = y; verts[i + 2] = u; verts[i + 3] = v;
        verts[i + 4] = c.R / 255f; verts[i + 5] = c.G / 255f; verts[i + 6] = c.B / 255f; verts[i + 7] = c.A / 255f;
        count += Stride;
    }

    // ---------- Primitives (everything solid samples the 1x1 white texture) ----------
    public static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        Use(whiteTex);
        Vert(a.X, a.Y, 0.5f, 0.5f, col); Vert(b.X, b.Y, 0.5f, 0.5f, col); Vert(c.X, c.Y, 0.5f, 0.5f, col);
    }

    public static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col) { Tri(a, b, c, col); Tri(a, c, d, col); }

    public static void QuadGrad(float x, float y, float rw, float rh, Color top, Color bottom)
    {
        Use(whiteTex);
        Vert(x, y, .5f, .5f, top); Vert(x + rw, y, .5f, .5f, top); Vert(x + rw, y + rh, .5f, .5f, bottom);
        Vert(x, y, .5f, .5f, top); Vert(x + rw, y + rh, .5f, .5f, bottom); Vert(x, y + rh, .5f, .5f, bottom);
    }

    // A filled convex outline, fanned from its first point (the centre for circles and rounded boxes).
    public static void Fan(Vector2 centre, List<Vector2> ring, Color col)
    {
        for (int i = 0; i < ring.Count; i++) Tri(centre, ring[i], ring[(i + 1) % ring.Count], col);
    }

    public static void TexQuad(Texture2D tex, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u0, float v0, float u1, float v1, Color tint)
    {
        Use((int)tex.Id);
        Vert(p0.X, p0.Y, u0, v0, tint); Vert(p1.X, p1.Y, u1, v0, tint); Vert(p2.X, p2.Y, u1, v1, tint);
        Vert(p0.X, p0.Y, u0, v0, tint); Vert(p2.X, p2.Y, u1, v1, tint); Vert(p3.X, p3.Y, u0, v1, tint);
    }

    // ---------- Textures ----------
    static Java.Nio.ByteBuffer scratch;
    static IntPtr scratchPtr;
    static readonly Dictionary<uint, bool> mipped = new();

    public static Texture2D CreateTexture(int tw, int th, IntPtr rgba)
    {
        Flush();   // vertices already queued belong to the texture bound now, not the one made here
        var id = new int[1];
        GLES20.GlGenTextures(1, id, 0);
        GLES20.GlBindTexture(GLES20.GlTexture2d, id[0]);
        boundTex = -1;
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlNearest);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlNearest);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
        Upload(tw, th, rgba, sub: false);
        return new Texture2D { Id = (uint)id[0], Width = tw, Height = th, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
    }

    // The texture to upload into must be bound. Pixels come from managed memory, so copy them into a direct buffer.
    static void Upload(int tw, int th, IntPtr rgba, bool sub)
    {
        int bytes = tw * th * 4;
        if (scratch == null || scratch.Capacity() < bytes)
        {
            scratch = Java.Nio.ByteBuffer.AllocateDirect(bytes).Order(Java.Nio.ByteOrder.NativeOrder());
            scratchPtr = JNIEnv.GetDirectBufferAddress(scratch.Handle);
        }
        Buffer.MemoryCopy((void*)rgba, (void*)scratchPtr, bytes, bytes);
        scratch.Position(0);
        if (sub) GLES20.GlTexSubImage2D(GLES20.GlTexture2d, 0, 0, 0, tw, th, GLES20.GlRgba, GLES20.GlUnsignedByte, scratch);
        else GLES20.GlTexImage2D(GLES20.GlTexture2d, 0, GLES20.GlRgba, tw, th, 0, GLES20.GlRgba, GLES20.GlUnsignedByte, scratch);
    }

    public static void UpdateTexture(Texture2D tex, IntPtr rgba)
    {
        Flush();
        GLES20.GlBindTexture(GLES20.GlTexture2d, (int)tex.Id);
        boundTex = -1;
        Upload(tex.Width, tex.Height, rgba, sub: true);
    }

    public static void DeleteTexture(Texture2D tex)
    {
        if (tex.Id == 0) return;
        Flush();
        GLES20.GlDeleteTextures(1, new[] { (int)tex.Id }, 0);
        mipped.Remove(tex.Id);
        boundTex = -1;
    }

    public static void SetFilter(Texture2D tex, TextureFilter f)
    {
        Flush();
        GLES20.GlBindTexture(GLES20.GlTexture2d, (int)tex.Id);
        boundTex = -1;
        int mag = f == TextureFilter.Point ? GLES20.GlNearest : GLES20.GlLinear;
        int min = f == TextureFilter.Point ? GLES20.GlNearest
            : f == TextureFilter.Trilinear && mipped.ContainsKey(tex.Id) ? GLES20.GlLinearMipmapLinear : GLES20.GlLinear;
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, mag);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, min);
    }

    public static void SetWrap(Texture2D tex, bool repeat)
    {
        Flush();
        GLES20.GlBindTexture(GLES20.GlTexture2d, (int)tex.Id);
        boundTex = -1;
        int m = repeat ? GLES20.GlRepeat : GLES20.GlClampToEdge;
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapS, m);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapT, m);
    }

    public static void Mipmaps(ref Texture2D tex)
    {
        Flush();
        GLES20.GlBindTexture(GLES20.GlTexture2d, (int)tex.Id);
        boundTex = -1;
        GLES20.GlGenerateMipmap(GLES20.GlTexture2d);
        mipped[tex.Id] = true;
        tex.Mipmaps = 1 + (int)MathF.Floor(MathF.Log2(Math.Max(tex.Width, tex.Height)));
    }
}
