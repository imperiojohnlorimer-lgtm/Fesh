// Things the app asks the game to do, run on the game thread at the end of a frame (the one safe moment to touch its state).
using System.Reflection;

namespace Fesh.Droid;

internal static class AppHooks
{
    static volatile bool saveRequested;
    static MethodInfo save;

    // The app is leaving the foreground: save, since Android may close it without warning once it's in the background.
    public static void RequestSave() { saveRequested = true; }

    public static void EndOfFrame()
    {
        if (!saveRequested) return;
        saveRequested = false;
        var g = TouchHud.Game;
        if (g == null || TouchHud.Mode is "title" or "create") return;   // nothing to save (the game saves nothing from those either)
        try
        {
            save ??= g.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            save?.Invoke(g, null);
        }
        catch { }
    }
}
