// Touch becomes Raylib's mouse (one finger = the left button at that spot), the on-screen controls press virtual keys, a
// hardware keyboard and a gamepad feed the same key and button tables, and a vertical drag over the island guide's list
// becomes the mouse wheel.
using System.Numerics;
using Raylib_cs;

namespace Fesh.Droid;

internal static class InputBackend
{
    // ---------- written by the UI thread ----------
    static volatile float touchX = -10000, touchY = -10000;
    static volatile bool touchDown;
    static int latch;   // a press that began and ended between two frames still counts (consumed atomically in Poll)
    static readonly object keyGate = new();
    static readonly Queue<int> keyQueue = new(), charQueue = new();

    // A press where a vertical drag should scroll (the island guide's list) waits to see which it is: a tap presses where
    // it landed when the finger lifts, a sideways drag presses at once (a slider), an up-or-down drag scrolls.
    static bool pending, scrolling;
    static float downX, downY, lastY, wheelAccum;
    static float Step => 60 * TouchHud.H / 720f;   // a mouse-wheel notch is this many pixels of drag

    public static void TouchDown(float x, float y)
    {
        touchX = x; touchY = y; downX = x; downY = y; lastY = y; scrolling = false;
        if (TouchHud.WheelAllowed) { pending = true; return; }
        pending = false; touchDown = true; Interlocked.Exchange(ref latch, 1);
    }

    public static void TouchMove(float x, float y)
    {
        touchX = x; touchY = y;
        float thr = 12 * TouchHud.H / 720f;
        if (pending)
        {
            if (MathF.Abs(x - downX) > thr && MathF.Abs(x - downX) >= MathF.Abs(y - downY)) { pending = false; touchDown = true; Interlocked.Exchange(ref latch, 1); }
            else if (MathF.Abs(y - downY) > thr) { pending = false; scrolling = true; lastY = y; }
        }
        else if (scrolling)
        {
            lock (keyGate) wheelAccum += (y - lastY) / Step;
            lastY = y;
        }
    }

    public static void TouchUp()
    {
        if (pending) { pending = false; touchX = downX; touchY = downY; Interlocked.Exchange(ref latch, 1); touchDown = false; return; }   // a tap
        scrolling = false;
        touchDown = false;
    }

    // The system took the touch away (a call, a gesture, the app leaving): nothing was clicked.
    public static void TouchCancel() { pending = false; scrolling = false; touchDown = false; Interlocked.Exchange(ref latch, 0); }

    // The on-screen controls hold virtual keys. Two fingers can hold the same one, so each press is counted.
    static readonly int[] touchCount = new int[512];
    static readonly bool[] hwKeys = new bool[512];
    public static void PressTouch(KeyboardKey k) { int i = (int)k; if (i > 0 && i < touchCount.Length) lock (keyGate) touchCount[i]++; }
    public static void ReleaseTouch(KeyboardKey k) { int i = (int)k; if (i > 0 && i < touchCount.Length) lock (keyGate) touchCount[i] = Math.Max(0, touchCount[i] - 1); }
    // A key on a hardware keyboard (or the Back button as Esc).
    public static void SetKey(KeyboardKey k, bool down) { int i = (int)k; if (i > 0 && i < hwKeys.Length) lock (keyGate) hwKeys[i] = down; }
    public static void ReleaseAllKeys()
    {
        lock (keyGate)
        {
            Array.Clear(hwKeys);
            Array.Clear(padHeld);
        }
        padLX = padLY = padRX = 0;
    }

    // Typing from the soft keyboard: characters and Backspaces in the order they came, one per frame, with a frame between
    // two Backspaces so each one is a fresh press.
    static readonly Queue<int> textActions = new();     // >= 0 a character, -1 a Backspace
    static readonly Queue<KeyboardKey> pulses = new();  // other keys (Enter) that are down for one frame
    public static void TypeChar(int cp) { lock (keyGate) textActions.Enqueue(cp); }
    public static void Pulse(KeyboardKey k) { lock (keyGate) { if (k == KeyboardKey.Backspace) textActions.Enqueue(-1); else pulses.Enqueue(k); } }

    // ---------- the gamepad (Android's first one) ----------
    static volatile bool padConnected;
    static readonly bool[] padHeld = new bool[32];
    static volatile float padLX, padLY, padRX;
    public static void SetPadConnected(bool c) { padConnected = c; if (!c) { lock (keyGate) Array.Clear(padHeld); padLX = padLY = padRX = 0; } }
    public static void SetPadButton(GamepadButton b, bool down) { int i = (int)b; if (i >= 0 && i < padHeld.Length) { lock (keyGate) padHeld[i] = down; padConnected = true; } }
    public static void SetPadAxes(float lx, float ly, float rx) { padLX = lx; padLY = ly; padRX = rx; padConnected = true; }

    // ---------- read by the game thread, once a frame (Raylib's PollInputEvents) ----------
    static bool curDown, prevDown, backspaceHeld;
    static Vector2 mouse = new(-10000, -10000), prevMouse = new(-10000, -10000);
    static readonly bool[] keyNow = new bool[512], keyPrev = new bool[512];
    static readonly bool[] padNow = new bool[32], padPrev = new bool[32];
    static float wheel;

    public static void Poll()
    {
        prevDown = curDown;
        bool latched = Interlocked.Exchange(ref latch, 0) == 1;
        curDown = touchDown || latched;   // a tap shorter than a frame still counts as a press
        prevMouse = mouse;
        mouse = new Vector2(touchX, touchY);
        lock (keyGate)
        {
            Array.Copy(keyNow, keyPrev, keyNow.Length);
            for (int i = 0; i < keyNow.Length; i++) keyNow[i] = touchCount[i] > 0 || hwKeys[i];
            if (backspaceHeld) backspaceHeld = false;   // the gap between two Backspaces
            else if (textActions.Count > 0)
            {
                int a = textActions.Dequeue();
                if (a >= 0) charQueue.Enqueue(a);
                else { keyNow[(int)KeyboardKey.Backspace] = true; backspaceHeld = true; }
            }
            if (pulses.Count > 0) { int p = (int)pulses.Dequeue(); if (p > 0 && p < keyNow.Length) keyNow[p] = true; }
            for (int i = 0; i < keyNow.Length; i++) if (keyNow[i] && !keyPrev[i]) keyQueue.Enqueue(i);
            Array.Copy(padNow, padPrev, padNow.Length);
            Array.Copy(padHeld, padNow, padNow.Length);
            wheel = wheelAccum; wheelAccum = 0;
        }
    }

    public static bool MouseDown => curDown;
    public static bool MousePressed => curDown && !prevDown;
    public static Vector2 Mouse => mouse;
    public static Vector2 MouseDelta => prevMouse.X < -9000 || mouse.X < -9000 ? Vector2.Zero : mouse - prevMouse;
    public static float Wheel => wheel;

    public static bool KeyDown(KeyboardKey k) { int i = (int)k; return i >= 0 && i < keyNow.Length && keyNow[i]; }
    public static bool KeyPressed(KeyboardKey k) { int i = (int)k; return i >= 0 && i < keyNow.Length && keyNow[i] && !keyPrev[i]; }
    public static int NextKey() { lock (keyGate) return keyQueue.Count > 0 ? keyQueue.Dequeue() : 0; }
    public static int NextChar() { lock (keyGate) return charQueue.Count > 0 ? charQueue.Dequeue() : 0; }

    public static bool PadAvailable => padConnected;
    public static bool PadDown(GamepadButton b) { int i = (int)b; return i >= 0 && i < padNow.Length && padNow[i]; }
    public static bool PadPressed(GamepadButton b) { int i = (int)b; return i >= 0 && i < padNow.Length && padNow[i] && !padPrev[i]; }
    public static float PadAxis(GamepadAxis a) => a switch { GamepadAxis.LeftX => padLX, GamepadAxis.LeftY => padLY, GamepadAxis.RightX => padRX, _ => 0 };
}
