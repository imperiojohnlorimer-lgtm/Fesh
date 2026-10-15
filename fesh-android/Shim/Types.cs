// The slice of Raylib-cs that Fesh uses, with the same names and shapes, so the game's source compiles unchanged on Android.
using System.Numerics;
using System.Runtime.InteropServices;

namespace Raylib_cs;

[StructLayout(LayoutKind.Sequential)]
public partial struct Color : IEquatable<Color>
{
    public byte R, G, B, A;
    public Color(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; }
    public Color(byte r, byte g, byte b) : this(r, g, b, (byte)255) { }
    public Color(int r, int g, int b, int a) : this((byte)r, (byte)g, (byte)b, (byte)a) { }
    public Color(int r, int g, int b) : this((byte)r, (byte)g, (byte)b, (byte)255) { }
    public Color(float r, float g, float b, float a) : this((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255)) { }
    public Color(float r, float g, float b) : this(r, g, b, 1f) { }
    public bool Equals(Color o) => R == o.R && G == o.G && B == o.B && A == o.A;
    public override bool Equals(object o) => o is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);
}

[StructLayout(LayoutKind.Sequential)]
public struct Rectangle
{
    public float X, Y, Width, Height;
    public Rectangle(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
    public Rectangle(Vector2 pos, float width, float height) : this(pos.X, pos.Y, width, height) { }
    public Rectangle(float x, float y, Vector2 size) : this(x, y, size.X, size.Y) { }
    public Rectangle(Vector2 pos, Vector2 size) : this(pos.X, pos.Y, size.X, size.Y) { }
}

// A GPU texture: Id is the GL name.
[StructLayout(LayoutKind.Sequential)]
public struct Texture2D
{
    public uint Id;
    public int Width, Height, Mipmaps;
    public PixelFormat Format;
}

// CPU pixels (always RGBA 8 bits here), in unmanaged memory like Raylib's.
[StructLayout(LayoutKind.Sequential)]
public unsafe struct Image
{
    public void* Data;
    public int Width, Height, Mipmaps;
    public PixelFormat Format;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct GlyphInfo
{
    public int Value, OffsetX, OffsetY, AdvanceX;
    public Image Image;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Font
{
    public int BaseSize, GlyphCount, GlyphPadding;
    public Texture2D Texture;
    public Rectangle* Recs;
    public GlyphInfo* Glyphs;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Wave
{
    public uint FrameCount, SampleRate, SampleSize, Channels;
    public void* Data;
    internal int Handle;
}

[StructLayout(LayoutKind.Sequential)]
public struct AudioStream
{
    public IntPtr Buffer, Processor;
    public uint SampleRate, SampleSize, Channels;
}

[StructLayout(LayoutKind.Sequential)]
public struct Sound
{
    public AudioStream Stream;
    public uint FrameCount;
    internal int Handle;
}
