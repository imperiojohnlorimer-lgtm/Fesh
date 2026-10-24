#if DEBUG
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// FESH_TRAILER=1 (with FESH_AUTOTEST as the output folder and FESH_SAVE): records a gameplay trailer. The game is stepped
// by exactly 1/30 s a frame (Game.FixedDt) and every frame of each shot is saved to <out>/frames as a PNG, with the
// captions drawn over it; the sounds played and the music's levels are logged per frame and mixed into <out>/audio.wav
// at the end. tools/trailer/assemble.py (Blender) turns the two into an MP4. FESH_TRAILER=preview saves every 10th frame
// only (quick to look at), and FESH_TRAILER_ONLY=<shot>[,<shot>] records only those shots.
partial class Game
{
    const int TrailerFps = 30;
    bool trailerPreview, rolling;
    bool keepTalk; int skipTick;
    int rollLen, rolled, frameNo;
    string capTag, capTitle, capSub;
    string cardBig, cardSmall;   // a full black card instead of the game (the section titles and the end)
    float cutFlash;                 // a white flash on a cut (the boss shots)
    string framesDir;
    readonly List<(int frame, string name, float vol)> heard = new();
    readonly List<(string mus, float mv, string amb, float av, string nat, float nv)> levels = new();
    readonly List<(string shot, int from, int len)> shotLog = new();

    IEnumerable<int> TrailerScript()
    {
        Note("Trailer");
        trailerPreview = Environment.GetEnvironmentVariable("FESH_TRAILER") == "preview";
        var only = Environment.GetEnvironmentVariable("FESH_TRAILER_ONLY")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        framesDir = Path.Combine(testDir, "frames");
        Directory.CreateDirectory(framesDir);
        foreach (var f in Directory.GetFiles(framesDir, "*.png")) File.Delete(f);
        // The game as players see it: light and colour, banners, twists, blinking; no goal card over the action.
        EffectsOff = false; OldCelebrate = true; LookData.AutoBlink = true;
        Settings.Data.guide = false; Settings.Data.shake = true; Settings.Data.zoom = 4; MuteToasts = true; CleanHud = true;
        FixedDt = 1f / TrailerFps;
        Sfx.Heard = (n, v) => { if (rolling) heard.Add((frameNo, n, v)); };
        // Talk that turns up anyway is read straight through (a shot that wants it shown sets keepTalk).
        testEachFrame = () => { Inp.UsingPad = false; if (mode == "dialogue" && !keepTalk && (skipTick++ & 1) == 0) Inp.Tap(KeyboardKey.E); };
        // Every tune is composed on a thread at start-up; the mix needs them all.
        for (int i = 0; i < 2000 && Music.ReadyCount < Music.Names.Count() + 3; i++) yield return 0;

        foreach (var (name, shot) in TrailerShots())
        {
            if (only != null && !only.Contains(name)) continue;
            Note($"shot {name} from frame {frameNo}");
            int from = frameNo;
            var e = shot().GetEnumerator();
            while (true)
            {
                if (rolling && rolled >= rollLen) break;
                bool more;
                try { more = e.MoveNext(); }
                catch (Exception ex) { Check($"shot {name} crashed: {ex.GetType().Name}: {ex.Message} at {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}", false); more = false; }
                if (!more) { while (rolling && rolled < rollLen) yield return 0; break; }
                yield return e.Current;
            }
            Check($"shot {name}: {frameNo - from} frames ({(frameNo - from) / (float)TrailerFps:0.0} s)", frameNo - from > 0);
            shotLog.Add((name, from, frameNo - from));
            rolling = false; capTitle = capSub = capTag = cardBig = cardSmall = null; cutFlash = 0; keepTalk = false;
            ReleaseKeys();
        }
        if (MatchActive) CloseTrailerDock();
        FixedDt = 0; MuteToasts = false; CleanHud = false;
        Sfx.Heard = null;
        WriteTrailerAudio();
        File.WriteAllLines(Path.Combine(testDir, "shots.txt"), shotLog.Select(s => $"{s.shot} {s.from} {s.len}"));
        Note($"Trailer: {frameNo} frames, {frameNo / (float)TrailerFps:0.0} s");
    }

    // Starts filming: the next n seconds of the game, with a caption over them.
    void Roll(float secs, string tag = null, string title = null, string sub = null)
    {
        rolling = true; rolled = 0; rollLen = (int)MathF.Round(secs * TrailerFps);
        capTag = tag; capTitle = title; capSub = sub;
    }

    // Every frame, for n frames (the script's own yields are frames too, but yield 1 waits two).
    static IEnumerable<int> Frames(int n) { for (int i = 0; i < n; i++) yield return 0; }

    void ReleaseKeys()
    {
        foreach (var k in new[] { KeyboardKey.W, KeyboardKey.A, KeyboardKey.S, KeyboardKey.D, KeyboardKey.E, KeyboardKey.F, KeyboardKey.R,
            KeyboardKey.Up, KeyboardKey.Down, KeyboardKey.Left, KeyboardKey.Right, KeyboardKey.Space, KeyboardKey.LeftShift })
            Inp.Hold(k, false);
        Inp.ScriptStick = null;
        Inp.ScriptMouse = Offscreen;
    }

    // Holds the move keys for a direction (x, y each -1, 0 or 1).
    void Steer(int x, int y)
    {
        Inp.Hold(KeyboardKey.A, x < 0); Inp.Hold(KeyboardKey.D, x > 0);
        Inp.Hold(KeyboardKey.W, y < 0); Inp.Hold(KeyboardKey.S, y > 0);
    }

    // A fisher of our own, outdoors at Saltmere: the start of most shots.
    void TrailerGame(bool night = false, int day = 6)
    {
        state = new State { created = true, look = new Look { name = "Ana", skin = 2, hair = 1, hairColor = 0, shirt = 1, pants = 2, hat = 1 } };
        state.flags.metTomas = true;
        state.day = day;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(night);
        rng = new Random(7 + day);   // the same shot every time it is recorded
        quietWildlife = true; standStill = false;
        state.hp = 100; state.food = 100;
        // No first-visit hints or creature whispers in the middle of a shot.
        foreach (var c in Data.Creatures) foreach (var k in new[] { ":lock", ":gate", ":time" }) state.hinted[c.Id + k] = true;
        state.hinted["carving"] = true;
        Inp.ScriptMouse = Offscreen;
        ReleaseKeys();
    }

    /* ---------- Recording ---------- */
    partial void TrailerAfterDraw()
    {
        if (!rolling || rolled >= rollLen) return;
        DrawTrailerOverlay();
        levels.Add(Music.Levels);
        bool save = !trailerPreview || rolled % 10 == 0;
        if (save)
        {
            Rlgl.DrawRenderBatchActive();
            var img = LoadImageFromScreen();
            if (img.Width != 1280 || img.Height != 720) ImageResize(ref img, 1280, 720);
            ExportImage(img, Path.Combine(framesDir, $"f{frameNo:D5}.png"));
            UnloadImage(img);
        }
        frameNo++; rolled++;
    }

    void DrawTrailerOverlay()
    {
        const int Fade = 6;
        float a = Math.Min(1, Math.Min((rolled + 1) / (float)Fade, (rollLen - rolled) / (float)Fade));
        if (cardBig != null)
        {
            Gfx.Rect(0, 0, 1280, 720, new Color(6, 10, 18, 255));
            var gold = new Color(240, 196, 92, (int)(255 * a));
            Gfx.TextCenter(cardBig, 640, 296, FontKind.Note, 76, gold);
            if (cardSmall != null) Gfx.TextCenter(cardSmall, 640, 395, FontKind.Ui500, 26, new Color(230, 230, 236, (int)(230 * a)));
            return;
        }
        if (capTitle != null)
        {
            // Slides in after a beat, stays about three seconds, and gets out of the way of the play.
            float t = rolled / (float)TrailerFps, inA = Math.Clamp((t - 0.25f) / 0.4f, 0, 1);
            float outA = Math.Clamp(MathF.Min((rollLen - rolled) / 12f, (3.9f - t) / 0.45f), 0, 1);
            float ca = MathF.Min(inA, outA), dx = (1 - inA) * -30;
            // Top left, where the status card would be: clear of the hotbar, the prompt and the fight bars.
            // In a match its own cards are on the left, so it goes top right.
            bool right = MatchActive;
            float tw = MathF.Max(Gfx.Measure(capTitle, FontKind.Ui700, 44), capSub == null ? 0 : Gfx.Measure(capSub, FontKind.Ui500, 24));
            for (int i = 0; i < 20; i++)
            {
                float bw = 620 - i * 12;
                Gfx.Rect(right ? 1280 - bw : 0, i * 7, bw, 7, new Color(0, 0, 0, (int)(ca * 130 * (1 - i / 20f))));
            }
            float x = right ? 1240 - tw - dx : 40 + dx, y = 54;
            if (capTag != null)
            {
                float gw = Gfx.Measure(capTag, FontKind.Ui700, 18) + 22;
                Gfx.Rect(x, y - 34, gw, 26, new Color(196, 52, 40, (int)(235 * ca)), 4);
                Gfx.Text(capTag, x + 11, y - 31, FontKind.Ui700, 18, new Color(255, 245, 230, (int)(255 * ca)));
            }
            Gfx.Text(capTitle, x + 2, y + 2, FontKind.Ui700, 44, new Color(0, 0, 0, (int)(160 * ca)));
            Gfx.Text(capTitle, x, y, FontKind.Ui700, 44, new Color(255, 236, 190, (int)(255 * ca)));
            if (capSub != null) Gfx.Text(capSub, x + 2, y + 58, FontKind.Ui500, 24, new Color(232, 236, 240, (int)(235 * ca)));
        }
        if (cutFlash > 0) { Gfx.Rect(0, 0, 1280, 720, new Color(255, 255, 255, (int)(255 * Math.Min(1, cutFlash)))); cutFlash -= 0.18f; }
        if (a < 1) Gfx.Rect(0, 0, 1280, 720, new Color(0, 0, 0, (int)(255 * (1 - a))));
    }

    /* ---------- The sound, mixed afterwards ---------- */
    void WriteTrailerAudio()
    {
        const int Rate = 44100;
        int per = Rate / TrailerFps, n = frameNo * per;
        var mix = new float[n];
        // Each loop restarts when it comes on again (as the game does: the old one is stopped, the new one played from the top).
        void Loop(Func<int, (string name, float v)> at)
        {
            string playing = null; double head = 0;
            for (int f = 0; f < frameNo; f++)
            {
                var (name, v) = at(f);
                if (name != playing) { playing = name; head = 0; }
                var pcm = name == null ? null : Music.Loop(name);
                float v1 = f + 1 < frameNo && at(f + 1).name == name ? at(f + 1).v : v;
                for (int i = 0; i < per; i++)
                {
                    if (pcm != null && pcm.Length > 0)
                    {
                        double p = head % pcm.Length; int i0 = (int)p; double fr = p - i0;
                        float s = (float)(pcm[i0] * (1 - fr) + pcm[(i0 + 1) % pcm.Length] * fr) / 32768f;
                        mix[f * per + i] += s * (v + (v1 - v) * i / per);
                    }
                    head += Music.SampleRate / (double)Rate;
                }
            }
        }
        Loop(f => (levels[f].mus, levels[f].mv));
        Loop(f => (levels[f].amb, levels[f].av));
        Loop(f => (levels[f].nat, levels[f].nv));
        // The game has one voice per sound: playing it again starts it over, so each one stops where the next begins.
        for (int e = 0; e < heard.Count; e++)
        {
            var (frame, name, vol) = heard[e];
            if (!Sfx.Raw.TryGetValue(name, out var buf)) continue;
            int at = frame * per, end = n;
            for (int k = e + 1; k < heard.Count; k++) if (heard[k].name == name) { end = heard[k].frame * per; break; }
            for (int i = 0; i < buf.Length && at + i < end; i++) mix[at + i] += buf[i] * Sfx.MasterGain * vol * 0.8f;
        }
        // Louder (the calm island tunes are quiet next to the effects), a soft limiter on the peaks, and a fade at both ends.
        for (int i = 0; i < n; i++)
        {
            float g = Math.Min(1, Math.Min(i / (Rate * 0.05f), (n - i) / (Rate * 1.5f)));
            mix[i] = MathF.Tanh(mix[i] * 5f) * 0.92f * g;
        }
        using var w = new BinaryWriter(File.Create(Path.Combine(testDir, "audio.wav")));
        int dataLen = n * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in mix) w.Write((short)Math.Clamp(s * 32767, -32768, 32767));
    }

}
#endif
