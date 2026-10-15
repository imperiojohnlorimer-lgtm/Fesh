using Android.Content;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Java.Lang;
using Raylib_cs;

namespace Fesh.Droid;

// The game's surface, which can also be a text editor: while a name or an address is being typed, the soft keyboard sends
// what is typed into the game's character queue (and Backspace and Enter as one-frame key presses).
public class GameView : SurfaceView
{
    public GameView(Context context) : base(context)
    {
        Focusable = true;
        FocusableInTouchMode = true;
    }

    public override bool OnCheckIsTextEditor() => true;

    public override IInputConnection OnCreateInputConnection(EditorInfo outAttrs)
    {
        outAttrs.InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions | InputTypes.TextVariationVisiblePassword;
        outAttrs.ImeOptions = ImeFlags.NoExtractUi | ImeFlags.NoFullscreen | (ImeFlags)(int)ImeAction.Done;
        return new Connection(this);
    }

    public void ShowKeyboard()
    {
        kbRequested = true;
        RequestFocus();
        ((InputMethodManager)Context.GetSystemService(Context.InputMethodService))?.ShowSoftInput(this, ShowFlags.Implicit);
    }

    public void HideKeyboard()
    {
        kbRequested = false;
        ((InputMethodManager)Context.GetSystemService(Context.InputMethodService))?.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
    }

    bool kbRequested;
    // Whether the keyboard is up: asked of the system where it can say, else our own memory of what we last asked.
    public bool KeyboardVisible()
    {
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.R && RootWindowInsets != null)
            return RootWindowInsets.IsVisible(WindowInsets.Type.Ime());
        return kbRequested;
    }
    public void ToggleKeyboard() { if (KeyboardVisible()) HideKeyboard(); else ShowKeyboard(); }

    sealed class Connection : BaseInputConnection
    {
        string composing = "";
        readonly GameView owner;
        public Connection(GameView v) : base(v, false) { owner = v; }

        // Predictive keyboards send a word as it is being composed; the game gets each change as typed characters and Backspaces.
        void Replace(string now)
        {
            int common = 0;
            while (common < composing.Length && common < now.Length && composing[common] == now[common]) common++;
            for (int i = common; i < composing.Length; i++) InputBackend.Pulse(KeyboardKey.Backspace);
            for (int i = common; i < now.Length; i++) InputBackend.TypeChar(now[i]);
            composing = now;
        }

        public override bool SetComposingText(ICharSequence text, int newCursorPosition) { Replace(text?.ToString() ?? ""); return true; }
        public override bool CommitText(ICharSequence text, int newCursorPosition) { Replace(text?.ToString() ?? ""); composing = ""; return true; }
        public override bool FinishComposingText() { composing = ""; return true; }
        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
        {
            for (int i = 0; i < Java.Lang.Math.Max(1, beforeLength); i++) InputBackend.Pulse(KeyboardKey.Backspace);
            composing = "";
            return true;
        }
        // The keyboard's check mark means "I'm done typing": put the keyboard away and leave the game's own buttons (Start,
        // Connect) to do the rest, rather than pressing Enter, which would start the game from the creator.
        public override bool PerformEditorAction(ImeAction actionCode) { owner.HideKeyboard(); return true; }

        public override bool SendKeyEvent(KeyEvent e)
        {
            if (e.Action != KeyEventActions.Down) return true;
            if (e.KeyCode == Keycode.Del) InputBackend.Pulse(KeyboardKey.Backspace);
            else if (e.KeyCode is Keycode.Enter or Keycode.NumpadEnter) owner.HideKeyboard();   // the same as the check mark
            else if (e.UnicodeChar > 0) InputBackend.TypeChar(e.UnicodeChar);
            return true;
        }
    }
}
