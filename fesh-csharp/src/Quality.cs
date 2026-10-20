namespace Fesh;

// Keeping the game smooth on a slow phone (1.42). When the work of a frame stays over its budget, the effects that cost most
// are switched off one at a time, cheapest to lose first, and they come back when there's plenty of room. It never changes
// what the player chose in Settings: `Rung` is only an extra limit on top of it, and is forgotten when the game closes.
// Rung r means effects 1..r are off:
//   1 film grain, 2 bloom, 3 reflections, 4 tree dapples, 5 water caustics and wet ground, 6 full light drops to soft, 7 light and colour off.
// This class has no screen and no game state, so the controller can be tested by feeding it frame times.
static class Quality
{
    public const int Rungs = 7;
    public static readonly string[] Effect = { "", "film grain", "bloom", "reflections", "tree shade dapples", "water sparkle and wet ground", "full light", "light and colour" };

    // Off while a test run plays (a Debug build is slow, and the scripts check exact pixels); the quality tests turn it on.
    public static bool Enabled = Environment.GetEnvironmentVariable("FESH_AUTOTEST") is not { Length: > 0 };

    public static int Rung;
    public static Action<string> Say;          // a short notice for the player

    const int Window = 60;                     // frames in one average
    const float DownAfter = 2f;                // seconds over budget before one step down
    static float sum, winSec, hold, overFor, calmFor, clock, upAt = -1, needCalm = 30;
    static int count, lastFps, slow;
    static string lastMode = "";

    // What a frame may cost. The rest of the frame period is for the screen, the driver and everything else on the phone.
    public static float Budget(int fps) => 0.7f * 1000f / fps;

    public static void Reset() { Rung = 0; ResetWindow(); overFor = calmFor = 0; hold = 0; upAt = -1; needCalm = 30; lastMode = ""; lastFps = 0; slow = 0; }
    static void ResetWindow() { sum = 0; winSec = 0; count = 0; }
    // A bake, a zoom change, a loading screen: frames for a moment don't say anything about the game's steady cost.
    public static void Hold(float secs = 1.5f) { if (secs > hold) hold = secs; ResetWindow(); }

    // One frame: the work it took (ms), the real time since the last frame (s), the frame cap and the mode, and whether
    // switching rung r off would change anything for this player (some of them are already off). True if the rung changed.
    public static bool Feed(float workMs, float interval, int fps, string mode, Func<int, bool> matters)
    {
        clock += interval;
        if (mode != lastMode || fps != lastFps) { lastMode = mode; lastFps = fps; overFor = calmFor = 0; Hold(1.0f); }
        if (interval > 0.25f)
        {
            // One long frame is a stall (a bake, a resume); a run of them, each full of work, is a game too slow to play.
            if (workMs > Budget(fps) * 2 && ++slow >= 6) { slow = 0; overFor = calmFor = 0; return Down(matters); }
            Hold(1.0f);
            return false;
        }
        slow = 0;
        if (hold > 0) { hold -= interval; return false; }
        sum += workMs; winSec += interval; count++;
        if (count < Window) return false;
        float avg = sum / count, budget = Budget(fps), secs = winSec;
        ResetWindow();
        if (avg > budget) { overFor += secs; calmFor = 0; }
        else if (avg < budget * 0.55f) { calmFor += secs; overFor = 0; }   // room to spare, enough to bring something back
        else { overFor = calmFor = 0; }
        if (upAt > 0 && clock - upAt > 90) { needCalm = Math.Max(30, needCalm / 2); upAt = -1; }   // the last step back up held
        if (overFor >= DownAfter) { overFor = calmFor = 0; return Down(matters); }
        if (calmFor >= needCalm && Rung > 0)
        {
            calmFor = 0;
            int was = Rung, r = Rung - 1;
            while (r > 0 && !matters(r)) r--;
            Rung = r;
            upAt = clock;
            Hold();
            Say?.Invoke($"Plenty of room again: {Effect[was]} is back on.");
            return true;
        }
        return false;
    }

    static bool Down(Func<int, bool> matters)
    {
        int r = Rung + 1;
        while (r <= Rungs && !matters(r)) r++;
        if (r > Rungs) return false;   // nothing left that costs anything
        if (upAt > 0 && clock - upAt < 90) needCalm = Math.Min(needCalm * 2, 480);   // that step up didn't hold: wait longer next time
        upAt = -1;
        Rung = r;
        Hold();
        Say?.Invoke($"To keep things smooth: {Effect[r]} is off for now.");
        return true;
    }
}
