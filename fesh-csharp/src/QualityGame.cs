using Raylib_cs;

namespace Fesh;

// Prof.cs and Quality.cs wired into the game: the frame's numbers, the F3 overlay (and the Touch settings' debug line), and
// the automatic quality step.
partial class Game
{
    // At the end of each frame's work, before the screen is flipped.
    void UpdateQuality()
    {
        Prof.Detail = Prof.Overlay || Platform.ShowDebug;
        float interval = Raylib.GetFrameTime();
        Prof.FrameEnd(interval);
        bool on = Settings.Data.autoQuality && Quality.Enabled && !EffectsOff;
        if (!on) { if (Quality.Rung != 0) Quality.Reset(); return; }
        Quality.Say ??= m => Toast(m, 3.5f);
        Quality.Feed(Prof.WorkMs, interval, Platform.Touch ? Platform.Fps : 60, mode, RungMatters);
    }

    // Would switching this effect off change anything for this player? (What they chose, not what is already stepped down.)
    internal static bool RungMatters(int r) => r switch
    {
        1 => Settings.Data.grain,
        2 => Settings.Data.bloom && RequestedLight > 0,
        3 or 4 or 6 => RequestedLight == 2,
        5 => true,
        7 => RequestedLight >= 1,
        _ => false,
    };

    // F3, or "Debug line" in the Touch settings: where the frame's time goes. Passes nest (the caustics are inside the water).
    void DrawProfOverlay()
    {
        if (!Prof.Detail) return;
        var lines = new List<string>
        {
            $"work {Prof.WorkAvg:0.0} ms  worst {Prof.Worst:0.0}  frame {Prof.IntervalAvg:0.0} ms ({(Prof.IntervalAvg > 0 ? 1000 / Prof.IntervalAvg : 0):0} fps)",
            $"budget {Quality.Budget(Platform.Touch ? Platform.Fps : 60):0.0} ms   quality step {Quality.Rung}{(Quality.Rung > 0 ? " (" + Quality.Effect[Quality.Rung] + " off)" : "")}   light {LightMode}   view {W}x{H}",
        };
        const int fs = 15;
        const float lh = 17, nameW = 150;
        float w = nameW + 70;
        foreach (var l in lines) w = Math.Max(w, Gfx.Measure(l, FontKind.Ui600, fs));
        float x = Gfx.LW - w - 16, y = 128;   // the right edge, under the buttons and the toast
        int rows = lines.Count + Pass.Count;
        Gfx.Rect(x - 6, y - 6, w + 16, rows * lh + 12, Pal.Rgba(8, 14, 24, 0.82f), 5);
        for (int i = 0; i < lines.Count; i++) Gfx.Text(lines[i], x, y + i * lh, FontKind.Ui600, fs, Pal.Paper);
        for (int i = 0; i < Pass.Count; i++)
        {
            float ry = y + (lines.Count + i) * lh;
            Gfx.Text(Pass.Names[i], x, ry, FontKind.Ui600, fs, Pal.C("#cfe3ea"));
            Gfx.Text($"{Prof.Ms[i]:0.00} ms", x + nameW, ry, FontKind.Ui600, fs, Pal.C("#cfe3ea"));
        }
    }
}
