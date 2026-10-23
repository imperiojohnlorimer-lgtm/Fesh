using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Chiptune sound effects, synthesized at startup the same way the web version built them with Web Audio.
static class Sfx
{
    const int Rate = 44100;
    const float Master = 1.6f;
    static readonly Dictionary<string, Sound> sounds = new();
    static readonly List<Sound> reels = new();
    static readonly Random rng = new();
    public static bool Muted;
    public static float Volume = 1;

    public static void Init()
    {
        InitAudioDevice();
        if (!IsAudioDeviceReady()) return;
        // A test run is silent (FESH_TEST_SOUND=1 to hear it). The master volume sits under every setting, so Settings.Reset can't undo it.
        if (Environment.GetEnvironmentVariable("FESH_AUTOTEST") is { Length: > 0 } && Environment.GetEnvironmentVariable("FESH_TEST_SOUND") != "1") SetMasterVolume(0f);
        Add("cast", 0.2f, b => Tone(b, 650, 0.16f, "triangle", 0.04f, -420));
        Add("splash", 0.3f, b => Noise(b, 0.28f, 0.14f));
        // The Mire leviathan gulping air at the surface (MireBoss.cs): a deep hollow gloop, a splash, a bubble popping.
        Add("whale", 2.2f, b => { Tone(b, 78, 1.1f, "sine", 0.3f, 95); Tone(b, 156, 1.0f, "sine", 0.1f, 190, 0.05f); Tone(b, 150, 0.95f, "sine", 0.26f, -70, 1.15f); });
        Add("gulp", 0.6f, b => { Tone(b, 150, 0.32f, "sine", 0.32f, -85); Noise(b, 0.2f, 0.08f); Tone(b, 420, 0.08f, "sine", 0.12f, 260, 0.36f); });
        Add("bite", 0.25f, b => { Tone(b, 880, 0.08f, "square", 0.05f); Tone(b, 1175, 0.1f, "square", 0.05f, 0, 0.09f); });
        for (int i = 0; i < 4; i++) { float f = 260 + i * 30; reels.Add(Make(0.05f, b => Tone(b, f, 0.03f, "square", 0.012f))); }
        Add("catch", 0.45f, b => { var fs = new[] { 523f, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(b, fs[i], 0.14f, "square", 0.04f, 0, i * 0.08f); });
        Add("rare", 0.75f, b => { var fs = new[] { 392f, 523, 659, 784, 1047, 1319 }; for (int i = 0; i < 6; i++) Tone(b, fs[i], 0.2f, "triangle", 0.06f, 0, i * 0.1f); });
        Add("fail", 0.35f, b => Tone(b, 320, 0.3f, "sawtooth", 0.035f, -200));
        Add("odd", 0.6f, b => { Tone(b, 220, 0.18f, "triangle", 0.06f, 440); Tone(b, 330, 0.16f, "square", 0.035f, -150, 0.2f); Tone(b, 660, 0.2f, "triangle", 0.05f, 0, 0.36f); });
        Add("blip", 0.04f, b => Tone(b, 640, 0.025f, "square", 0.012f));
        Add("ui", 0.07f, b => Tone(b, 520, 0.05f, "triangle", 0.03f));
        Add("pickup", 0.16f, b => { Tone(b, 740, 0.06f, "triangle", 0.035f); Tone(b, 988, 0.08f, "triangle", 0.035f, 0, 0.06f); });
        Add("build", 0.2f, b => { Tone(b, 190, 0.07f, "square", 0.05f, -70); Tone(b, 250, 0.06f, "square", 0.04f, -80, 0.1f); });
        Add("remove", 0.16f, b => Tone(b, 330, 0.14f, "triangle", 0.04f, -190));
        Add("nope", 0.1f, b => Tone(b, 200, 0.08f, "square", 0.025f));
        Add("warn", 0.22f, b => { Tone(b, 440, 0.09f, "square", 0.03f, 220); Tone(b, 660, 0.09f, "square", 0.03f, 220, 0.1f); });
        Add("chop", 0.16f, b => { Noise(b, 0.08f, 0.16f); Tone(b, 140, 0.1f, "square", 0.04f, -50); });
        Add("mine", 0.16f, b => { Tone(b, 1400, 0.06f, "square", 0.03f, -300); Tone(b, 900, 0.1f, "triangle", 0.03f, 0, 0.03f); });
        Add("door", 0.3f, b => { Tone(b, 180, 0.25f, "triangle", 0.035f, 90); Noise(b, 0.1f, 0.05f); });
        Add("eat", 0.3f, b => { Tone(b, 400, 0.07f, "triangle", 0.04f, -150); Tone(b, 380, 0.07f, "triangle", 0.04f, -150, 0.12f); });
        Add("craft", 0.45f, b => { var fs = new[] { 523f, 784, 1047 }; for (int i = 0; i < 3; i++) Tone(b, fs[i], 0.12f, "triangle", 0.045f, 0, i * 0.09f); Noise(b, 0.05f, 0.05f); });
        Add("pet", 0.3f, b => { Tone(b, 880, 0.08f, "sine", 0.05f, 200); Tone(b, 1175, 0.12f, "sine", 0.05f, 0, 0.1f); });
        Add("coin", 0.25f, b => { Tone(b, 1319, 0.06f, "square", 0.03f); Tone(b, 1760, 0.14f, "square", 0.03f, 0, 0.06f); });
        Add("thunder", 2f, Rumble);
        Add("hit", 0.14f, b => { Noise(b, 0.06f, 0.2f); Tone(b, 520, 0.08f, "square", 0.04f, -260); });
        Add("hurt", 0.3f, b => { Tone(b, 300, 0.22f, "sawtooth", 0.05f, -160); Noise(b, 0.08f, 0.1f); });
        Add("neigh", 0.9f, Whinny);
        Add("stomp", 0.3f, b => { Tone(b, 95, 0.26f, "sine", 0.14f, -45); Noise(b, 0.14f, 0.12f); });
        // Tidemane's hoofbeats (1.20): a soft knock, two pitches so the bound's da-dum doesn't cut itself off.
        // A tool swung at nothing (Hotbar.cs): a quick rush of air.
        Add("swish", 0.14f, b => { Noise(b, 0.1f, 0.05f); Tone(b, 900, 0.1f, "triangle", 0.012f, -600); });
        Add("hoof", 0.09f, b => { Tone(b, 150, 0.06f, "triangle", 0.05f, -70); Noise(b, 0.03f, 0.035f); });
        Add("hoof2", 0.09f, b => { Tone(b, 125, 0.06f, "triangle", 0.05f, -60); Noise(b, 0.03f, 0.035f); });
        Add("whistle", 0.4f, b => { Tone(b, 1500, 0.12f, "sine", 0.05f, 500); Tone(b, 2000, 0.2f, "sine", 0.05f, -350, 0.15f); });
        Add("bolt", 0.25f, b => { Noise(b, 0.12f, 0.1f); Tone(b, 760, 0.14f, "triangle", 0.035f, -380); });
        // The agong (a deep, ringing bong) and the villagers' pots and pans (a bright clank).
        Add("gong", 1.2f, b => { Tone(b, 196, 1.1f, "sine", 0.12f, -6); Tone(b, 472, 0.6f, "sine", 0.05f, -10); Tone(b, 770, 0.3f, "triangle", 0.025f); Noise(b, 0.04f, 0.12f); });
        // The quiz and the sorting tray (1.17): a bright rising pair for right, a soft falling one for wrong, and a
        // little splash with a twinkle for a fish let go.
        Add("right", 0.3f, b => { Tone(b, 784, 0.09f, "triangle", 0.05f); Tone(b, 1175, 0.16f, "triangle", 0.05f, 0, 0.09f); });
        // Kalutang (Marilaya): a struck pair of wooden bars, high and low.
        Add("kalhi", 0.14f, b => { Tone(b, 1245, 0.09f, "triangle", 0.06f); Noise(b, 0.012f, 0.05f); });
        Add("kallo", 0.16f, b => { Tone(b, 831, 0.11f, "triangle", 0.065f); Noise(b, 0.014f, 0.05f); });
        Add("wrong", 0.3f, b => { Tone(b, 392, 0.12f, "triangle", 0.045f); Tone(b, 294, 0.18f, "triangle", 0.045f, -40, 0.11f); });
        Add("release", 0.5f, b => { Noise(b, 0.16f, 0.09f); Tone(b, 1319, 0.08f, "sine", 0.035f, 0, 0.2f); Tone(b, 1760, 0.14f, "sine", 0.03f, 0, 0.28f); });
        Add("clang", 0.25f, b => { Tone(b, 1250, 0.18f, "square", 0.02f, -80); Tone(b, 1900, 0.1f, "triangle", 0.025f); Noise(b, 0.05f, 0.1f); });
        // The celebrations (Celebrate.cs, 1.44): each moment has its own sting, so a new species doesn't sound like a level.
        // A new species: a bright rising three-note call with a sparkle on top.
        Add("newfish", 0.7f, b => { var fs = new[] { 784f, 988, 1319 }; for (int i = 0; i < 3; i++) Tone(b, fs[i], 0.16f, "triangle", 0.055f, 0, i * 0.09f); Tone(b, 2637, 0.3f, "sine", 0.025f, 0, 0.3f); Tone(b, 1976, 0.3f, "sine", 0.025f, 0, 0.36f); });
        // A new record: ding-ding-DING.
        Add("record", 0.7f, b => { Tone(b, 1047, 0.08f, "square", 0.03f); Tone(b, 1047, 0.08f, "square", 0.03f, 0, 0.11f); Tone(b, 1568, 0.38f, "square", 0.034f, 0, 0.22f); Tone(b, 784, 0.38f, "triangle", 0.04f, 0, 0.22f); });
        // A trophy: a short brass chord that swells, then a high note.
        Add("trophy", 0.95f, b => { foreach (float f in new[] { 523f, 659, 784 }) Tone(b, f, 0.42f, "sawtooth", 0.018f); foreach (float f in new[] { 698f, 880, 1047 }) Tone(b, f, 0.5f, "sawtooth", 0.02f, 0, 0.36f); Tone(b, 2093, 0.3f, "triangle", 0.035f, 0, 0.6f); });
        // A fishing level: a quick run up the scale and a held chord.
        Add("levelup", 1.25f, b => { var fs = new[] { 523f, 587, 659, 698, 784, 880, 988 }; for (int i = 0; i < fs.Length; i++) Tone(b, fs[i], 0.09f, "square", 0.026f, 0, i * 0.055f); foreach (float f in new[] { 523f, 659, 784, 1047 }) Tone(b, f, 0.75f, "triangle", 0.04f, 0, 0.42f); });
        // A Fish log page done: a page turning and a gentle chime.
        Add("page", 1.1f, b => { Noise(b, 0.14f, 0.05f); var fs = new[] { 659f, 988, 1319, 1760 }; for (int i = 0; i < 4; i++) Tone(b, fs[i], 0.5f, "sine", 0.05f, 0, 0.12f + i * 0.12f); });
        // The title's opening (Opening.cs): something enormous opening its eyes in the dark, a low swell of sound.
        Add("deep", 1.5f, b => { Tone(b, 55, 1.3f, "sine", 0.22f, -12); Tone(b, 82, 1.0f, "triangle", 0.05f, -20, 0.12f); Noise(b, 0.5f, 0.02f); });
        // A badge or an album chapter: a coin and a little bell.
        Add("badge", 0.6f, b => { Tone(b, 1319, 0.06f, "square", 0.028f); Tone(b, 1760, 0.12f, "square", 0.028f, 0, 0.06f); Tone(b, 2637, 0.35f, "sine", 0.03f, 0, 0.18f); });
    }

    // A horse's whinny: a bright, wobbling cry that slides down and breaks into a snort.
    static void Whinny(float[] buf)
    {
        int n = (int)(0.7f * Rate);
        double phase = 0;
        for (int i = 0; i < n && i < buf.Length; i++)
        {
            double t = (double)i / n, s = (double)i / Rate;
            double f = 980 * (1 - 0.45 * t) + 70 * Math.Sin(Math.Tau * 17 * s) * (0.4 + t);
            phase = (phase + f / Rate) % 1.0;
            double w = 0.6 * (2 * phase - 1) + 0.4 * Math.Sin(phase * Math.Tau);
            double env = Math.Min(1, t / 0.05) * Math.Pow(1 - t, 0.8);
            buf[i] += (float)(w * 0.045 * env);
        }
        int start = (int)(0.68f * Rate), len = (int)(0.16f * Rate);
        for (int i = 0; i < len && start + i < buf.Length; i++)
            buf[start + i] += (float)((rng.NextDouble() * 2 - 1) * 0.07 * (1 - (double)i / len));
    }

    public static void Play(string name)
    {
        if (Muted || !sounds.TryGetValue(name, out var s)) return;
        SetSoundVolume(s, Volume);
        PlaySound(s);
    }

    public static void Reel()
    {
        if (Muted || reels.Count == 0) return;
        var s = reels[rng.Next(reels.Count)];
        SetSoundVolume(s, Volume);
        PlaySound(s);
    }

    public static void Shutdown()
    {
        foreach (var s in sounds.Values) UnloadSound(s);
        foreach (var s in reels) UnloadSound(s);
        if (IsAudioDeviceReady()) CloseAudioDevice();
    }

    static void Add(string name, float dur, Action<float[]> fill) => sounds[name] = Make(dur, fill);

    static Sound Make(float dur, Action<float[]> fill)
    {
        var buf = new float[(int)(Rate * dur) + 1];
        fill(buf);
        var wave = LoadWaveFromMemory(".wav", Wav(buf));
        var sound = LoadSoundFromWave(wave);
        UnloadWave(wave);
        return sound;
    }

    // An oscillator with an exponential pitch slide and an exponential fade, like Web Audio's ramps.
    static void Tone(float[] buf, float freq, float dur, string type, float vol, float slide = 0, float delay = 0)
    {
        int start = (int)(delay * Rate), n = (int)(dur * Rate);
        float end = Math.Max(40, freq + slide);
        double phase = 0;
        for (int i = 0; i < n && start + i < buf.Length; i++)
        {
            double t = (double)i / n;
            double f = slide != 0 ? freq * Math.Pow(end / freq, t) : freq;
            phase = (phase + f / Rate) % 1.0;
            double w = type switch
            {
                "square" => phase < 0.5 ? 1 : -1,
                "triangle" => 4 * Math.Abs(phase - 0.5) - 1,
                "sawtooth" => 2 * phase - 1,
                _ => Math.Sin(phase * Math.PI * 2)
            };
            buf[start + i] += (float)(w * vol * Math.Pow(0.0001 / vol, t));
        }
    }

    // Fading white noise through a band-pass filter at 900 Hz.
    static void Noise(float[] buf, float dur, float vol)
    {
        int n = Math.Min(buf.Length, (int)(dur * Rate));
        double w0 = 2 * Math.PI * 900 / Rate, alpha = Math.Sin(w0) / (2 * 0.8);
        double b0 = alpha, b2 = -alpha, a0 = 1 + alpha, a1 = -2 * Math.Cos(w0), a2 = 1 - alpha;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < n; i++)
        {
            double x = (rng.NextDouble() * 2 - 1) * (1 - (double)i / n);
            double y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            buf[i] += (float)(y * vol);
        }
    }

    // Thunder: brown noise that cracks, then rolls away.
    static void Rumble(float[] buf)
    {
        double b = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            double t = (double)i / buf.Length;
            b = Math.Clamp(b + (rng.NextDouble() * 2 - 1) * 0.08, -1, 1) * 0.995;
            double crack = t < 0.06 ? (rng.NextDouble() * 2 - 1) * (1 - t / 0.06) * 0.3 : 0;
            buf[i] += (float)((b * 0.5 + crack) * Math.Pow(1 - t, 1.6) * 0.6);
        }
    }

    static byte[] Wav(float[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataLen = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples) w.Write((short)Math.Clamp(s * Master * 32767, -32768, 32767));
        w.Flush();
        return ms.ToArray();
    }
}
