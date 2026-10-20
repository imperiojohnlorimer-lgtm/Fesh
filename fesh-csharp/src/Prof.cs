using System.Diagnostics;

namespace Fesh;

// Where a frame's time goes (1.42). Frame work (everything between the start of a frame and the moment it is handed to the
// screen, so the wait for the next frame and the Android frame cap are not in it) is always measured; it costs two clock
// reads. The per-pass times are only taken while the overlay is up (F3, or the debug line in the Touch settings), when each
// pass is timed with Begin/End. Passes nest (the caustics are inside the water), so the list isn't a sum.
// No Raylib in here: the Android app compiles it as it is.
static class Pass
{
    public const int Copy = 0, Water = 1, Caustics = 2, Shadows = 3, Objects = 4, Sky = 5, Grade = 6, Night = 7, Weather = 8, Finish = 9, Update = 10, Upload = 11, Ui = 12, Wet = 13, Count = 14;
    public static readonly string[] Names = { "Base copy", "Water", "  caustics", "Shadows", "Objects", "Sky and mist", "Colour grade", "Night", "Rain and ash", "Vignette, grain", "Update", "Texture upload", "Interface", "Wet ground" };
}

static class Prof
{
    public static bool Overlay;                // F3: show the numbers
    public static bool Detail;                 // time every pass (the overlay is up, or the phone's debug line is on)

    static readonly long[] begun = new long[Pass.Count], acc = new long[Pass.Count];
    public static readonly float[] Ms = new float[Pass.Count];   // smoothed milliseconds a frame, per pass
    const double ToMs = 1000.0 / 1.0;
    static long frameStart;
    static int frames;
    static float windowWorst;

    public static float WorkMs;                // this frame
    public static float WorkAvg;               // smoothed
    public static float IntervalAvg;           // smoothed time between frames (what the player gets)
    public static float Worst;                 // the worst frame of the last 120

    public static void Begin(int id) { if (Detail) begun[id] = Stopwatch.GetTimestamp(); }
    public static void End(int id) { if (Detail) acc[id] += Stopwatch.GetTimestamp() - begun[id]; }

    public static void FrameStart() => frameStart = Stopwatch.GetTimestamp();

    // Call just before the frame is shown. `interval` is the real time since the last one, in seconds.
    public static void FrameEnd(float interval)
    {
        WorkMs = (float)((Stopwatch.GetTimestamp() - frameStart) * ToMs / Stopwatch.Frequency);
        WorkAvg += (WorkMs - WorkAvg) * 0.06f;
        IntervalAvg += (interval * 1000 - IntervalAvg) * 0.06f;
        if (WorkMs > windowWorst) windowWorst = WorkMs;
        if (++frames >= 120) { Worst = windowWorst; windowWorst = 0; frames = 0; }
        for (int i = 0; i < Pass.Count; i++)
        {
            float ms = (float)(acc[i] * ToMs / Stopwatch.Frequency);
            Ms[i] += (ms - Ms[i]) * 0.1f;
            acc[i] = 0;
        }
    }
}
