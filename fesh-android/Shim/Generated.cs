// Generated from Raylib-cs 8.1.0 so persisted key codes and flag values keep the same numbers.
namespace Raylib_cs;

public enum KeyboardKey : int
{
    Null = 0,
    Back = 4,
    Menu = 5,
    VolumeUp = 24,
    VolumeDown = 25,
    Space = 32,
    Apostrophe = 39,
    Comma = 44,
    Minus = 45,
    Period = 46,
    Slash = 47,
    Zero = 48,
    One = 49,
    Two = 50,
    Three = 51,
    Four = 52,
    Five = 53,
    Six = 54,
    Seven = 55,
    Eight = 56,
    Nine = 57,
    Semicolon = 59,
    Equal = 61,
    A = 65,
    B = 66,
    C = 67,
    D = 68,
    E = 69,
    F = 70,
    G = 71,
    H = 72,
    I = 73,
    J = 74,
    K = 75,
    L = 76,
    M = 77,
    N = 78,
    O = 79,
    P = 80,
    Q = 81,
    R = 82,
    S = 83,
    T = 84,
    U = 85,
    V = 86,
    W = 87,
    X = 88,
    Y = 89,
    Z = 90,
    LeftBracket = 91,
    Backslash = 92,
    RightBracket = 93,
    Grave = 96,
    Escape = 256,
    Enter = 257,
    Tab = 258,
    Backspace = 259,
    Insert = 260,
    Delete = 261,
    Right = 262,
    Left = 263,
    Down = 264,
    Up = 265,
    PageUp = 266,
    PageDown = 267,
    Home = 268,
    End = 269,
    CapsLock = 280,
    ScrollLock = 281,
    NumLock = 282,
    PrintScreen = 283,
    Pause = 284,
    F1 = 290,
    F2 = 291,
    F3 = 292,
    F4 = 293,
    F5 = 294,
    F6 = 295,
    F7 = 296,
    F8 = 297,
    F9 = 298,
    F10 = 299,
    F11 = 300,
    F12 = 301,
    Kp0 = 320,
    Kp1 = 321,
    Kp2 = 322,
    Kp3 = 323,
    Kp4 = 324,
    Kp5 = 325,
    Kp6 = 326,
    Kp7 = 327,
    Kp8 = 328,
    Kp9 = 329,
    KpDecimal = 330,
    KpDivide = 331,
    KpMultiply = 332,
    KpSubtract = 333,
    KpAdd = 334,
    KpEnter = 335,
    KpEqual = 336,
    LeftShift = 340,
    LeftControl = 341,
    LeftAlt = 342,
    LeftSuper = 343,
    RightShift = 344,
    RightControl = 345,
    RightAlt = 346,
    RightSuper = 347,
    KeyboardMenu = 348,
}

public enum GamepadButton : int
{
    Unknown = 0,
    LeftFaceUp = 1,
    LeftFaceRight = 2,
    LeftFaceDown = 3,
    LeftFaceLeft = 4,
    RightFaceUp = 5,
    RightFaceRight = 6,
    RightFaceDown = 7,
    RightFaceLeft = 8,
    LeftTrigger1 = 9,
    LeftTrigger2 = 10,
    RightTrigger1 = 11,
    RightTrigger2 = 12,
    MiddleLeft = 13,
    Middle = 14,
    MiddleRight = 15,
    LeftThumb = 16,
    RightThumb = 17,
}

public enum GamepadAxis : int
{
    LeftX = 0,
    LeftY = 1,
    RightX = 2,
    RightY = 3,
    LeftTrigger = 4,
    RightTrigger = 5,
}

public enum MouseButton : int
{
    Left = 0,
    Right = 1,
    Middle = 2,
    Side = 3,
    Extra = 4,
    Forward = 5,
    Back = 6,
}

public enum TextureFilter : int
{
    Point = 0,
    Bilinear = 1,
    Trilinear = 2,
    Anisotropic4X = 3,
    Anisotropic8X = 4,
    Anisotropic16X = 5,
}

public enum TextureWrap : int
{
    Repeat = 0,
    Clamp = 1,
    MirrorRepeat = 2,
    MirrorClamp = 3,
}

[Flags]
public enum ConfigFlags : uint
{
    FullscreenMode = 2,
    ResizableWindow = 4,
    UndecoratedWindow = 8,
    TransparentWindow = 16,
    Msaa4xHint = 32,
    VSyncHint = 64,
    HiddenWindow = 128,
    AlwaysRunWindow = 256,
    MinimizedWindow = 512,
    MaximizedWindow = 1024,
    UnfocusedWindow = 2048,
    TopmostWindow = 4096,
    HighDpiWindow = 8192,
    MousePassthroughWindow = 16384,
    BorderlessWindowMode = 32768,
    InterlacedHint = 65536,
}

public enum TraceLogLevel : int
{
    All = 0,
    Trace = 1,
    Debug = 2,
    Info = 3,
    Warning = 4,
    Error = 5,
    Fatal = 6,
    None = 7,
}

public enum PixelFormat : int
{
    UncompressedGrayscale = 1,
    UncompressedGrayAlpha = 2,
    UncompressedR5G6B5 = 3,
    UncompressedR8G8B8 = 4,
    UncompressedR5G5B5A1 = 5,
    UncompressedR4G4B4A4 = 6,
    UncompressedR8G8B8A8 = 7,
    UncompressedR32 = 8,
    UncompressedR32G32B32 = 9,
    UncompressedR32G32B32A32 = 10,
    UncompressedR16 = 11,
    UncompressedR16G16B16 = 12,
    UncompressedR16G16B16A16 = 13,
    CompressedDxt1Rgb = 14,
    CompressedDxt1Rgba = 15,
    CompressedDxt3Rgba = 16,
    CompressedDxt5Rgba = 17,
    CompressedEtc1Rgb = 18,
    CompressedEtc2Rgb = 19,
    CompressedEtc2EacRgba = 20,
    CompressedPvrtRgb = 21,
    CompressedPvrtRgba = 22,
    CompressedAstc4X4Rgba = 23,
    CompressedAstc8X8Rgba = 24,
}

public partial struct Color
{
    public static readonly Color LightGray = new(200, 200, 200, 255);
    public static readonly Color Gray = new(130, 130, 130, 255);
    public static readonly Color DarkGray = new(80, 80, 80, 255);
    public static readonly Color Yellow = new(253, 249, 0, 255);
    public static readonly Color Gold = new(255, 203, 0, 255);
    public static readonly Color Orange = new(255, 161, 0, 255);
    public static readonly Color Pink = new(255, 109, 194, 255);
    public static readonly Color Red = new(230, 41, 55, 255);
    public static readonly Color Maroon = new(190, 33, 55, 255);
    public static readonly Color Green = new(0, 228, 48, 255);
    public static readonly Color Lime = new(0, 158, 47, 255);
    public static readonly Color DarkGreen = new(0, 117, 44, 255);
    public static readonly Color SkyBlue = new(102, 191, 255, 255);
    public static readonly Color Blue = new(0, 121, 241, 255);
    public static readonly Color DarkBlue = new(0, 82, 172, 255);
    public static readonly Color Purple = new(200, 122, 255, 255);
    public static readonly Color Violet = new(135, 60, 190, 255);
    public static readonly Color DarkPurple = new(112, 31, 126, 255);
    public static readonly Color Beige = new(211, 176, 131, 255);
    public static readonly Color Brown = new(127, 106, 79, 255);
    public static readonly Color DarkBrown = new(76, 63, 47, 255);
    public static readonly Color White = new(255, 255, 255, 255);
    public static readonly Color Black = new(0, 0, 0, 255);
    public static readonly Color Blank = new(0, 0, 0, 0);
    public static readonly Color Magenta = new(255, 0, 255, 255);
    public static readonly Color RayWhite = new(245, 245, 245, 255);
}
