using System.Collections.Concurrent;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Looping chiptune music for each island, the cave and indoors, composed by code when the game starts.
// Each track is eight bars: bass on beats one and three, an optional held pad, a melody that wanders the
// track's scale and lands on chord tones, and light percussion. Tracks are built on a background thread.
static class Music
{
    const int Rate = 22050;
    static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 }, Minor = { 0, 2, 3, 5, 7, 8, 10 },
        PhrygianDominant = { 0, 1, 4, 5, 7, 8, 10 }, PentatonicMajor = { 0, 2, 4, 7, 9 };
    static readonly int[] Maj = { 0, 4, 7 }, Min = { 0, 3, 7 };

    sealed record TrackDef(int Root, int[] Scale, int Bpm, (int semis, int[] chord)[] Prog, string Lead, string Bass, string Pad,
        bool Drums, int Seed, float Density, float LeadVol);

    static readonly Dictionary<string, TrackDef> Tracks = new()
    {
        ["saltmere"] = new(60, Major, 96, new[] { (0, Maj), (9, Min), (5, Maj), (7, Maj) }, "pulse", "tri", null, true, 1, 0.7f, 0.07f),
        ["frost"] = new(57, Minor, 70, new[] { (0, Min), (8, Maj), (3, Maj), (10, Maj) }, "bell", "sine", "pad", false, 2, 0.45f, 0.09f),
        ["dunes"] = new(62, PhrygianDominant, 84, new[] { (0, Maj), (1, Maj), (0, Maj), (10, Min) }, "pluck", "tri", null, true, 3, 0.6f, 0.07f),
        ["mire"] = new(55, PentatonicMajor, 100, new[] { (0, Maj), (9, Min), (5, Maj), (7, Maj) }, "marimba", "tri", null, true, 4, 0.75f, 0.1f),
        ["atoll"] = new(64, Major, 112, new[] { (0, Maj), (7, Maj), (9, Min), (5, Maj) }, "steel", "pluck", null, true, 5, 0.8f, 0.08f),
        ["cave"] = new(45, Minor, 60, new[] { (0, Min), (0, Min), (8, Maj), (7, Min) }, "bell", "sine", "pad", false, 6, 0.25f, 0.08f),
        ["home"] = new(65, Major, 80, new[] { (0, Maj), (5, Maj), (9, Min), (7, Maj) }, "musicbox", "sine", null, false, 7, 0.55f, 0.08f),
        // The fight with Tidemane: fast, minor and driving.
        ["boss"] = new(52, Minor, 150, new[] { (0, Min), (8, Maj), (10, Maj), (7, Maj) }, "pulse", "tri", null, true, 8, 0.9f, 0.07f)
    };

    static readonly ConcurrentDictionary<string, float[]> pcm = new();
    static readonly Dictionary<string, Sound> sounds = new();
    static string current, wanted, amb, ambWanted;
    static float vol, wantVol = 0.45f, ambVol, ambWantVol;
    public static bool Enabled = true;
    public static int ReadyCount => pcm.Count;
    public static string Current => current;
    public static IEnumerable<string> Names => Tracks.Keys;

    public static void Init()
    {
        if (!IsAudioDeviceReady()) return;
        Task.Run(() =>
        {
            foreach (var name in Tracks.Keys) pcm[name] = Compose(Tracks[name]);
            pcm["rain"] = RainLoop();
        });
    }

    public static void Want(string track, float volume) { wanted = track; wantVol = volume; }
    public static void WantAmbience(string name, float volume) { ambWanted = name; ambWantVol = volume; }

    public static void Update(float dt)
    {
        if (!IsAudioDeviceReady()) return;
        bool audible = Enabled && !Sfx.Muted;
        Step(ref current, wanted, ref vol, wantVol, dt, audible);
        Step(ref amb, ambWanted, ref ambVol, ambWantVol, dt, !Sfx.Muted);
    }

    // Fades the playing loop out before switching, and fades the new one in.
    static void Step(ref string playing, string want, ref float v, float target, float dt, bool audible)
    {
        if (playing != want)
        {
            v -= dt * 1.2f;
            if (v <= 0 || playing == null)
            {
                if (playing != null && sounds.TryGetValue(playing, out var old)) StopSound(old);
                playing = want;
                v = 0;
            }
        }
        else v += Math.Clamp(target - v, -dt * 0.8f, dt * 0.8f);
        if (playing == null || Load(playing) is not Sound s) return;
        if (!IsSoundPlaying(s)) PlaySound(s);
        SetSoundVolume(s, audible ? Math.Max(0, v) : 0);
    }

    static Sound? Load(string name)
    {
        if (sounds.TryGetValue(name, out var s)) return s;
        if (!pcm.TryGetValue(name, out var buf)) return null;
        var wave = LoadWaveFromMemory(".wav", Wav(buf));
        s = LoadSoundFromWave(wave);
        UnloadWave(wave);
        sounds[name] = s;
        return s;
    }

    public static void Shutdown()
    {
        foreach (var s in sounds.Values) { StopSound(s); UnloadSound(s); }
        sounds.Clear();
    }

    /* ---------- Composition ---------- */
    static float Hz(int midi) => 440f * MathF.Pow(2, (midi - 69) / 12f);

    static float[] Compose(TrackDef d)
    {
        var r = new Random(d.Seed * 7919);
        float beat = 60f / d.Bpm, bar = beat * 4;
        var buf = new float[(int)(bar * 8 * Rate)];
        // Melody notes available: two octaves of the scale above the root.
        var notes = new List<int>();
        for (int oct = 0; oct < 2; oct++) foreach (int s in d.Scale) notes.Add(d.Root + 12 + oct * 12 + s);
        notes.Add(d.Root + 36);
        int idx = notes.Count / 3;
        var phrase = new List<(int bar, int slot, int note, float len)>();
        for (int b = 0; b < 4; b++)
        {
            var (semis, chord) = d.Prog[b % d.Prog.Length];
            for (int slot = 0; slot < 8; slot++)
            {
                if (r.NextDouble() > d.Density) continue;
                if (slot % 2 == 0)
                {
                    // Strong beats land on the nearest chord tone.
                    var tones = chord.Select(c => (semis + c) % 12).ToHashSet();
                    idx = Enumerable.Range(0, notes.Count).Where(i => tones.Contains(((notes[i] - d.Root) % 12 + 12) % 12))
                        .OrderBy(i => Math.Abs(i - idx) + r.NextDouble() * 0.5).First();
                }
                else idx = Math.Clamp(idx + r.Next(-2, 3), 0, notes.Count - 1);
                phrase.Add((b, slot, notes[idx], r.NextDouble() < 0.3 ? beat : beat / 2));
            }
        }
        for (int b = 0; b < 8; b++)
        {
            var (semis, chord) = d.Prog[b % d.Prog.Length];
            float t0 = b * bar;
            Note(buf, t0, Hz(d.Root - 12 + semis), beat * 1.8f, d.Bass, 0.15f);
            Note(buf, t0 + beat * 2, Hz(d.Root - 12 + semis + chord[2]), beat * 1.8f, d.Bass, 0.12f);
            if (d.Pad != null) foreach (int c in chord) Note(buf, t0, Hz(d.Root + semis + c), bar, "pad", 0.03f);
            // The second half repeats the first, with a few notes changed.
            foreach (var n in phrase.Where(p => p.bar == b % 4))
            {
                int note = b >= 4 && r.NextDouble() < 0.25 ? n.note + (r.Next(2) == 0 ? 2 : -2) : n.note;
                Note(buf, t0 + n.slot * beat / 2, Hz(note), n.len * 1.6f, d.Lead, d.LeadVol);
            }
            if (d.Drums)
            {
                for (int s = 0; s < 8; s++) Hat(buf, t0 + s * beat / 2, s % 2 == 0 ? 0.03f : 0.016f, r);
                Kick(buf, t0, 0.18f);
                Kick(buf, t0 + beat * 2, 0.14f);
            }
            else if (d.Seed == 6)
            {
                // Water dripping somewhere in the cave.
                for (int k = 0; k < 2; k++) Note(buf, t0 + (float)r.NextDouble() * bar, 1800 + (float)r.NextDouble() * 900, 0.08f, "sine", 0.05f);
            }
        }
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Tanh(buf[i] * 1.3f);
        return buf;
    }

    static void Note(float[] buf, float start, float freq, float dur, string inst, float vol)
    {
        int s0 = (int)(start * Rate), n = (int)(dur * Rate);
        bool pad = inst == "pad";
        float decaySec = inst switch { "bell" => 1.6f, "pluck" => 0.25f, "marimba" => 0.35f, "steel" => 0.6f, "musicbox" => 0.9f, "pulse" => 0.45f, _ => dur };
        double decay = Math.Exp(Math.Log(0.001) / (decaySec * Rate));
        double ph = 0, g = 1, step = freq / Rate;
        int attack = pad ? (int)(0.3f * Rate) : (int)(0.005f * Rate), release = (int)(0.3f * Rate);
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            ph += step;
            double p = ph % 1.0, w = inst switch
            {
                "pulse" => p < 0.25 ? 1 : -1,
                "tri" or "pad" => 4 * Math.Abs(p - 0.5) - 1,
                "pluck" => 2 * p - 1,
                "bell" => Math.Sin(ph * Math.Tau) + 0.4 * Math.Sin(ph * Math.Tau * 2.76),
                "marimba" => Math.Sin(ph * Math.Tau) + 0.25 * Math.Sin(ph * Math.Tau * 4),
                "steel" => Math.Sin(ph * Math.Tau) + 0.5 * Math.Sin(ph * Math.Tau * 2) + 0.2 * Math.Sin(ph * Math.Tau * 3),
                "musicbox" => Math.Sin(ph * Math.Tau * 2) + 0.3 * Math.Sin(ph * Math.Tau * 4),
                _ => Math.Sin(ph * Math.Tau)
            };
            double env = i < attack ? (double)i / attack : 1;
            if (pad) { if (i > n - release) env *= (double)(n - i) / release; }
            else { g *= decay; if (i > n - release) env *= (double)(n - i) / release; }
            buf[s0 + i] += (float)(w * vol * env * (pad ? 1 : g));
        }
    }

    static void Hat(float[] buf, float start, float vol, Random r)
    {
        int s0 = (int)(start * Rate), n = (int)(0.04f * Rate);
        double prev = 0;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            double x = r.NextDouble() * 2 - 1;
            buf[s0 + i] += (float)((x - prev) * vol * (1 - (double)i / n));
            prev = x;
        }
    }

    static void Kick(float[] buf, float start, float vol)
    {
        int s0 = (int)(start * Rate), n = (int)(0.18f * Rate);
        double ph = 0;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            double t = (double)i / n;
            ph += (110 - 70 * t) / Rate;
            buf[s0 + i] += (float)(Math.Sin(ph * Math.Tau) * vol * (1 - t) * (1 - t));
        }
    }

    // Soft rain: filtered noise with the odd bright drop.
    static float[] RainLoop()
    {
        var r = new Random(99);
        var buf = new float[Rate * 4];
        double lp = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            lp += ((r.NextDouble() * 2 - 1) - lp) * 0.25;
            buf[i] = (float)(lp * 0.22);
            if (r.NextDouble() < 0.0006) for (int k = 0; k < 60 && i + k < buf.Length; k++) buf[i + k] += (float)((r.NextDouble() * 2 - 1) * 0.08 * (1 - k / 60.0));
        }
        int fade = Rate / 10;
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            buf[i] = buf[i] * k + buf[buf.Length - fade + i] * (1 - k);
        }
        return buf;
    }

    static byte[] Wav(float[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataLen = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples) w.Write((short)Math.Clamp(s * 32767, -32768, 32767));
        w.Flush();
        return ms.ToArray();
    }
}
