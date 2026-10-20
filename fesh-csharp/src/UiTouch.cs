using Raylib_cs;

namespace Fesh;

// The Settings "Touch" tab, shown instead of the keyboard Controls tab when the game runs on a phone (Platform.Touch).
// The values live in Platform; the Android app reads them, and saves them when Platform.Changed is called.
partial class Game
{
    void DrawTouchTab(float x, float y, float w)
    {
        const float rowH = 44, labelW = 250;
        float cx = x + labelW;
        bool changed = false;
        void Label(string s) => Gfx.Text(s, x, y + 9, FontKind.Ui600, 21, Pal.PaperInk);
        int Pick(string label, string[] options, int sel)
        {
            Label(label);
            int c = Choices(options, sel, cx, y);
            y += rowH;
            if (c >= 0 && c != sel) { changed = true; Sfx.Play("ui"); return c; }
            return sel;
        }
        // A slider between min and max, in steps of 0.05, with its value after it.
        float Range(string label, string id, float v, float min, float max, bool percent)
        {
            Label(label);
            float t = Slider(id, Math.Clamp((v - min) / (max - min), 0, 1), cx, y + 2, 360);
            float nv = MathF.Round((min + t * (max - min)) * 20) / 20;
            Gfx.Text(percent ? $"{nv * 100:0}%" : $"{nv:0.00}x", cx + 380, y + 9, FontKind.Ui600, 20, Muted);
            y += rowH;
            if (MathF.Abs(nv - v) > 0.001f) changed = true;
            return nv;
        }

        Platform.FixedStick = Pick("Stick", new[] { "Follows your thumb", "Stays put" }, Platform.FixedStick ? 1 : 0) == 1;
        Platform.StickScale = Range("Stick size", "touchstick", Platform.StickScale, 0.6f, 1.6f, false);
        Platform.ButtonScale = Range("Button size", "touchbuttons", Platform.ButtonScale, 0.6f, 1.6f, false);
        Platform.Opacity = Range("Opacity", "touchopacity", Platform.Opacity, 0.25f, 1f, true);
        Platform.SwapSides = Pick("Hand", new[] { "Right-handed", "Left-handed" }, Platform.SwapSides ? 1 : 0) == 1;
        Platform.TapWorld = Pick("Tap the screen", new[] { "Buttons only", "Acts like E" }, Platform.TapWorld ? 1 : 0) == 1;
        Platform.Haptics = Pick("Vibration", new[] { "On", "Off" }, Platform.Haptics ? 0 : 1) == 0;
        Platform.Fps = Pick("Frame rate", new[] { "30", "60" }, Platform.Fps == 30 ? 0 : 1) == 0 ? 30 : 60;
        Platform.ShowDebug = Pick("Debug line", new[] { "Off", "On" }, Platform.ShowDebug ? 1 : 0) == 1;
        if (changed) Platform.Changed?.Invoke();

        y += 4;
        float bw = SmallW("Move the controls");
        if (SmallButton("Move the controls", x, y)) { Sfx.Play("ui"); Platform.EditLayout?.Invoke(); }
        if (SmallButton("Reset controls", x + bw + 12, y)) { Sfx.Play("ui"); Platform.ResetTouch?.Invoke(); }
        y += 56;
        Lines(Gfx.Wrap("Move the controls lets you drag each button and the stick wherever suits your hands. Tap Done when you're happy with them.",
            FontKind.Note, 17, w), x, y, 23, FontKind.Note, 17, Muted);
    }
}
