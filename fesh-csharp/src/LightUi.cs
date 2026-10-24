using Raylib_cs;

namespace Fesh;

// The Graphics tab of the menu (1.39): light and colour, and the optional finishing touches.
partial class Game
{
    void DrawGraphicsTab(float x, float y, float w)
    {
        const float rowH = 48, labelW = 250;
        var d = Settings.Data;
        float cx = x + labelW;
        bool changed = false;
        int Pick(string label, string[] options, int sel)
        {
            Gfx.Text(label, x, y + 9, FontKind.Ui600, 21, Pal.PaperInk);
            int c = Choices(options, sel, cx, y);
            y += rowH;
            if (c >= 0 && c != sel) { changed = true; Sfx.Play("ui"); return c; }
            return sel;
        }

        // Automatic (the saved -1) shows as what it means here: Full on a computer, Soft on a phone. Picking one keeps it.
        int light = d.light >= 0 ? d.light : Platform.Touch ? 1 : 2;
        // Clicking the option already shown still counts: it stops following the platform.
        Gfx.Text("Light and colour", x, y + 9, FontKind.Ui600, 21, Pal.PaperInk);
        int picked = Choices(new[] { "Off", "Soft", "Full" }, light, cx, y);
        y += rowH;
        if (picked >= 0 && (picked != light || d.light < 0)) { d.light = picked; changed = true; Sfx.Play("ui"); }
        d.vignette = Pick("Dark corners", new[] { "None", "Light", "Normal" }, d.vignette);
        // Two finishing touches, both off until you want them.
        Gfx.Text("Bloom", x, y + 9, FontKind.Ui600, 21, Pal.PaperInk);
        int bl = Choices(new[] { "Soft halo", "No halo" }, d.bloom ? 0 : 1, cx, y);
        if (bl >= 0 && (bl == 0) != d.bloom) { d.bloom = bl == 0; changed = true; Sfx.Play("ui"); }
        Gfx.Text("Film grain", cx + 250, y + 9, FontKind.Ui600, 21, Pal.PaperInk);
        int gr = Choices(new[] { "Grain", "No grain" }, d.grain ? 0 : 1, cx + 250 + 130, y);
        if (gr >= 0 && (gr == 0) != d.grain) { d.grain = gr == 0; changed = true; Sfx.Play("ui"); }
        y += rowH;
        // Switches the dearest effects off for a while if the game can't keep up, and back on when there's room (Quality.cs).
        d.autoQuality = Pick("Keep it smooth", new[] { "Automatic", "Keep all" }, d.autoQuality ? 0 : 1) == 0;
        // Accessibility (Access.cs): Reduce motion keeps every warning but stops the shaking, flashing, swaying and bobbing;
        // High contrast makes the cards solid, the small text darker, and the night and corners lighter.
        d.reduceMotion = Pick("Reduce motion", new[] { "Normal motion", "Reduced" }, d.reduceMotion ? 1 : 0) == 1;
        d.highContrast = Pick("High contrast", new[] { "Normal colours", "High contrast" }, d.highContrast ? 1 : 0) == 1;
        // Minimal: just the hotbar, prompts and fight bars on screen; the panels are still on their keys and in the pause menu.
        if (!Platform.Touch) d.minimalHud = Pick("HUD", new[] { "Full", "Minimal" }, d.minimalHud ? 1 : 0) == 1;
        if (changed) Settings.Save();

        y += 6;
        string note = light switch
        {
            0 => "Off is the island exactly as it was before 1.39: flat tints, lamps that wait for the dark.",
            1 => "Soft: glowing lamps and windows that come on at dusk, gentle colour through the day and weather, and a faint palette for each island.",
            _ => "Full: glowing lamps and windows that come on at dusk, colour that follows the sun and the weather, a palette for each island, and shadows that move with the sun.",
        };
        Lines(Gfx.Wrap(note + " Automatic keeps the picture smooth by switching the dearest effects off for a while if the game can't keep up.", FontKind.Note, 17, w), x, y, 23, FontKind.Note, 17, Muted);
    }
}
