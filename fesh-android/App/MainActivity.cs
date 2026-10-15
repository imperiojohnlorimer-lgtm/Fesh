using Android.App;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using Android.Util;
using Android.Views;
using Raylib_cs;

namespace Fesh.Droid;

// One full-screen SurfaceView. The game runs on its own thread (it owns the GL context), exactly as its Run() loop does
// on the desktop; this class only passes the surface, touches, keys, gamepad and focus across.
[Activity(Label = "Fesh", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTask,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.Keyboard | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode | ConfigChanges.Density)]
public class MainActivity : Activity, ISurfaceHolderCallback
{
    GameView view;
    Thread game;
    WifiManager.MulticastLock multicast;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => RunOnUiThread(() => ShowError(e.ExceptionObject.ToString()));
        // Tell the game it is on a phone (its Settings then shows the Touch tab), and let its Settings drive our controls.
        Platform.Touch = true;
        TouchSettings.Load(FilesDir.AbsolutePath);
        Platform.Changed = TouchSettings.Save;
        Platform.ResetTouch = TouchSettings.Reset;
        Platform.EditLayout = TouchHud.StartEditing;
        // Draw under a notch or camera cut-out on the short edges too; the game keeps to the safe area (UpdateInsets).
        if (global::Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            var attrs = Window.Attributes;
            attrs.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            Window.Attributes = attrs;
        }
        view = new GameView(this);
        view.Holder.AddCallback(this);
        view.Touch += OnTouch;
        view.HapticFeedbackEnabled = true;
        TouchHud.Haptic = () => view.PerformHapticFeedback(FeedbackConstants.VirtualKey);
        TouchHud.TextWanted = on => RunOnUiThread(() => { if (on) view.ShowKeyboard(); else view.HideKeyboard(); });
        TouchHud.ToggleKeyboard = () => view.ToggleKeyboard();
        SetContentView(view);
        Window.AddFlags(WindowManagerFlags.KeepScreenOn);
        HideSystemBars();
    }

    void HideSystemBars()
    {
#pragma warning disable CA1422
        Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(SystemUiFlags.ImmersiveSticky | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen
            | SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen);
#pragma warning restore CA1422
    }

    // The part of the screen not covered by a notch or cut-out: the game and its controls are drawn inside it.
    void UpdateInsets()
    {
        int l = 0, t = 0, r = 0, b = 0;
        if (global::Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.P && view?.RootWindowInsets?.DisplayCutout is { } cut)
        { l = cut.SafeInsetLeft; t = cut.SafeInsetTop; r = cut.SafeInsetRight; b = cut.SafeInsetBottom; }
        Gl.SetInsets(l, t, r, b);
        TouchHud.W = Math.Max(1, (view?.Width ?? 1280) - l - r);
        TouchHud.H = Math.Max(1, (view?.Height ?? 720) - t - b);
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        Gl.SetFocused(hasFocus);
        if (hasFocus) { HideSystemBars(); UpdateInsets(); }
    }

    protected override void OnPause()
    {
        base.OnPause();
        AppHooks.RequestSave();   // Android may close the app without warning once it's in the background
        AudioBackend.Pause();
        TouchHud.CancelAll();
        InputBackend.ReleaseAllKeys();   // keys let go while we were away would stay held (Esc, a stick)
        InputBackend.SetPadConnected(false);
        try { if (multicast?.IsHeld == true) multicast.Release(); } catch { }
    }

    protected override void OnStop() { base.OnStop(); AppHooks.RequestSave(); }

    // Finding LAN games (the host's broadcast) needs the Wi-Fi radio to let broadcast packets through.
    protected override void OnResume()
    {
        base.OnResume();
        AudioBackend.Resume();
        RefreshPads();
        try
        {
            var wifi = (WifiManager)GetSystemService(WifiService);
            multicast ??= wifi?.CreateMulticastLock("fesh-lan");
            if (multicast != null) { multicast.SetReferenceCounted(false); multicast.Acquire(); }
        }
        catch { }
    }

    // A controller that is still plugged in (it only says so when it moves).
    static void RefreshPads()
    {
        try
        {
            foreach (int id in InputDevice.GetDeviceIds())
            {
                var d = InputDevice.GetDevice(id);
                if (d != null && !d.IsVirtual && (d.Sources & InputSourceType.Gamepad) == InputSourceType.Gamepad) { InputBackend.SetPadConnected(true); return; }
            }
        }
        catch { }
    }

    // Leaving for good: let the game finish (it saves) and end the whole process, so the next launch starts clean instead of
    // finding the quit flag and the old graphics context still set.
    protected override void OnDestroy()
    {
        Gl.Quit = true;
        if (IsFinishing)
        {
            try { game?.Join(3000); } catch { }
            base.OnDestroy();
            Process.KillProcess(Process.MyPid());
            return;
        }
        base.OnDestroy();
    }

    // Every finger is tracked on its own: one can hold the stick while another presses a button or taps the screen.
    void OnTouch(object sender, View.TouchEventArgs e)
    {
        var m = e.Event;
        UpdateInsets();
        float ox = Gl.InsetLeft, oy = Gl.InsetTop;
        int idx = m.ActionIndex;
        switch (m.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                TouchHud.Down(m.GetPointerId(idx), m.GetX(idx) - ox, m.GetY(idx) - oy);
                break;
            case MotionEventActions.Move:
                for (int i = 0; i < m.PointerCount; i++) TouchHud.Move(m.GetPointerId(i), m.GetX(i) - ox, m.GetY(i) - oy);
                break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                TouchHud.Up(m.GetPointerId(idx));
                break;
            case MotionEventActions.Cancel:
                TouchHud.CancelAll();
                break;
        }
        e.Handled = true;
    }

    // ---------- a hardware keyboard and a gamepad ----------
    static GamepadButton? PadButton(Keycode k) => k switch
    {
        Keycode.ButtonA => GamepadButton.RightFaceDown,
        Keycode.ButtonB => GamepadButton.RightFaceRight,
        Keycode.ButtonX => GamepadButton.RightFaceLeft,
        Keycode.ButtonY => GamepadButton.RightFaceUp,
        Keycode.ButtonL1 => GamepadButton.LeftTrigger1,
        Keycode.ButtonR1 => GamepadButton.RightTrigger1,
        Keycode.ButtonL2 => GamepadButton.LeftTrigger2,
        Keycode.ButtonR2 => GamepadButton.RightTrigger2,
        Keycode.ButtonSelect => GamepadButton.MiddleLeft,
        Keycode.ButtonStart => GamepadButton.MiddleRight,
        Keycode.ButtonThumbl => GamepadButton.LeftThumb,
        Keycode.ButtonThumbr => GamepadButton.RightThumb,
        Keycode.DpadUp => GamepadButton.LeftFaceUp,
        Keycode.DpadRight => GamepadButton.LeftFaceRight,
        Keycode.DpadDown => GamepadButton.LeftFaceDown,
        Keycode.DpadLeft => GamepadButton.LeftFaceLeft,
        _ => null,
    };

    static KeyboardKey KeyFor(Keycode k)
    {
        if (k >= Keycode.A && k <= Keycode.Z) return (KeyboardKey)(65 + (k - Keycode.A));
        if (k >= Keycode.Num0 && k <= Keycode.Num9) return (KeyboardKey)(48 + (k - Keycode.Num0));
        return k switch
        {
            Keycode.Space => KeyboardKey.Space, Keycode.Enter => KeyboardKey.Enter, Keycode.NumpadEnter => KeyboardKey.KpEnter,
            Keycode.Tab => KeyboardKey.Tab, Keycode.Del => KeyboardKey.Backspace, Keycode.ForwardDel => KeyboardKey.Delete,
            Keycode.Escape => KeyboardKey.Escape, Keycode.Back => KeyboardKey.Escape,
            Keycode.DpadUp => KeyboardKey.Up, Keycode.DpadDown => KeyboardKey.Down, Keycode.DpadLeft => KeyboardKey.Left, Keycode.DpadRight => KeyboardKey.Right,
            Keycode.PageUp => KeyboardKey.PageUp, Keycode.PageDown => KeyboardKey.PageDown,
            Keycode.Comma => KeyboardKey.Comma, Keycode.Period => KeyboardKey.Period, Keycode.Minus => KeyboardKey.Minus, Keycode.Equals => KeyboardKey.Equal,
            Keycode.Slash => KeyboardKey.Slash, Keycode.Semicolon => KeyboardKey.Semicolon, Keycode.Apostrophe => KeyboardKey.Apostrophe,
            Keycode.LeftBracket => KeyboardKey.LeftBracket, Keycode.RightBracket => KeyboardKey.RightBracket, Keycode.Backslash => KeyboardKey.Backslash,
            Keycode.Grave => KeyboardKey.Grave,
            Keycode.ShiftLeft => KeyboardKey.LeftShift, Keycode.CtrlLeft => KeyboardKey.LeftControl,
            _ => KeyboardKey.Null,
        };
    }

    static bool FromPad(KeyEvent e) => (e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad || (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick
        || e.KeyCode is >= Keycode.ButtonA and <= Keycode.ButtonMode;

    public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
    {
        if (keyCode == Keycode.Back && !FromPad(e)) { InputBackend.SetKey(KeyboardKey.Escape, true); return true; }
        if (FromPad(e) && PadButton(keyCode) is GamepadButton pb) { InputBackend.SetPadButton(pb, true); return true; }
        var key = KeyFor(keyCode);
        if (key != KeyboardKey.Null)
        {
            InputBackend.SetKey(key, true);
            if (TouchHud.WantText && e.UnicodeChar >= 32 && e.UnicodeChar < 127 && e.RepeatCount == 0) InputBackend.TypeChar(e.UnicodeChar);   // typing a name on a hardware keyboard
            return true;
        }
        return base.OnKeyDown(keyCode, e);
    }

    public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
    {
        if (keyCode == Keycode.Back && !FromPad(e)) { InputBackend.SetKey(KeyboardKey.Escape, false); return true; }
        if (FromPad(e) && PadButton(keyCode) is GamepadButton pb) { InputBackend.SetPadButton(pb, false); return true; }
        var key = KeyFor(keyCode);
        if (key != KeyboardKey.Null) { InputBackend.SetKey(key, false); return true; }
        return base.OnKeyUp(keyCode, e);
    }

    // Gamepad sticks, triggers and the D-pad on controllers that report it as an axis.
    public override bool OnGenericMotionEvent(MotionEvent e)
    {
        if ((e.Source & InputSourceType.Joystick) == InputSourceType.Joystick && e.Action == MotionEventActions.Move)
        {
            float lx = e.GetAxisValue(Axis.X), ly = e.GetAxisValue(Axis.Y);
            float rx = e.GetAxisValue(Axis.Z); if (Math.Abs(rx) < 0.01f) rx = e.GetAxisValue(Axis.Rx);
            InputBackend.SetPadAxes(lx, ly, rx);
            float hx = e.GetAxisValue(Axis.HatX), hy = e.GetAxisValue(Axis.HatY);
            InputBackend.SetPadButton(GamepadButton.LeftFaceLeft, hx < -0.5f);
            InputBackend.SetPadButton(GamepadButton.LeftFaceRight, hx > 0.5f);
            InputBackend.SetPadButton(GamepadButton.LeftFaceUp, hy < -0.5f);
            InputBackend.SetPadButton(GamepadButton.LeftFaceDown, hy > 0.5f);
            float lt = Math.Max(e.GetAxisValue(Axis.Ltrigger), e.GetAxisValue(Axis.Brake)), rt = Math.Max(e.GetAxisValue(Axis.Rtrigger), e.GetAxisValue(Axis.Gas));
            InputBackend.SetPadButton(GamepadButton.LeftTrigger2, lt > 0.5f);
            InputBackend.SetPadButton(GamepadButton.RightTrigger2, rt > 0.5f);
            return true;
        }
        return base.OnGenericMotionEvent(e);
    }

    public void SurfaceCreated(ISurfaceHolder holder) { }
    public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int width, int height)
    {
        UpdateInsets();
        Gl.SurfaceChanged(holder, width, height);
        if (game != null) return;
        game = new Thread(() =>
        {
            try { var g = new Game(); TouchHud.Game = g; g.Run(); RunOnUiThread(Finish); }
            catch (Exception ex) { Log.Error("Fesh", ex.ToString()); RunOnUiThread(() => ShowError(ex.ToString())); }
        }, 16 * 1024 * 1024) { Name = "fesh-game" };
        game.Start();
    }
    public void SurfaceDestroyed(ISurfaceHolder holder) => Gl.SurfaceLost();

    // A crash on the game thread would otherwise just close the app. Until it has been tested on many phones, show what went
    // wrong on the screen (and keep a copy in the app folder), so a bug report can be a screenshot.
    void ShowError(string text)
    {
        try { System.IO.File.WriteAllText(System.IO.Path.Combine(GetExternalFilesDir(null)?.AbsolutePath ?? FilesDir.AbsolutePath, "fesh-crash.txt"), text); } catch { }
        var tv = new global::Android.Widget.TextView(this) { Text = "Fesh stopped:\n\n" + text, TextSize = 11 };
        tv.SetTextColor(global::Android.Graphics.Color.White);
        tv.SetPadding(24, 24, 24, 24);
        var sv = new global::Android.Widget.ScrollView(this);
        sv.SetBackgroundColor(global::Android.Graphics.Color.Black);
        sv.AddView(tv);
        SetContentView(sv);
    }
}
