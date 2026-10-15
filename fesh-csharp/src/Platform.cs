namespace Fesh;

// What the Android app tells the game about itself. On Windows nothing sets any of this, so every value stays at its default
// and the game behaves exactly as before. The Android app sets Touch before the game starts, saves its own settings when
// Changed is called, and runs the layout editor for EditLayout.
static class Platform
{
    public static bool Touch;                  // running on a phone or tablet: no keyboard rebinding, no fullscreen or background-pause settings

    // The on-screen controls (Settings, "Touch" tab).
    public static bool FixedStick;             // the stick stays where it is drawn instead of appearing under the thumb
    public static float StickScale = 1f;       // 0.6 to 1.6
    public static float ButtonScale = 1f;      // 0.6 to 1.6
    public static float Opacity = 0.8f;        // 0.25 to 1
    public static bool SwapSides;              // left-handed
    public static bool Haptics = true;         // a short buzz on a button press
    public static int Fps = 60;                // 30 or 60
    public static bool ShowDebug;              // a line of build information at the bottom of the screen

    public static Action Changed;              // a touch setting changed: the app saves it
    public static Action EditLayout;           // the app lets you drag the controls where you want them
    public static Action ResetTouch;           // back to the default controls
}
