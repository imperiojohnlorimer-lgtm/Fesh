// Sound on Android: one AudioTrack fed by a small software mixer. Raylib's Sound plays once and restarts when played
// again, which is exactly what Fesh's music loops and effects expect (the game restarts a loop when it has ended).
using Android.Media;
using Raylib_cs;

namespace Fesh.Droid;

internal static class AudioBackend
{
    const int OutRate = 44100, Block = 1024;

    sealed class Clip { public short[] Data; public int Rate; }
    sealed class Voice { public Clip Clip; public double Pos; public bool Playing; public float Volume = 1; }

    static readonly List<Clip> waves = new();
    static readonly List<Voice> voices = new();
    static readonly object gate = new();
    static AudioTrack track;
    static Thread mixer;
    static volatile bool running;

    public static bool Ready => track != null;

    public static void Init()
    {
        if (track != null) return;
        int min = AudioTrack.GetMinBufferSize(OutRate, ChannelOut.Mono, Encoding.Pcm16bit);
        track = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Game).SetContentType(AudioContentType.Music).Build())
            .SetAudioFormat(new AudioFormat.Builder().SetEncoding(Encoding.Pcm16bit).SetSampleRate(OutRate).SetChannelMask(ChannelOut.Mono).Build())
            .SetBufferSizeInBytes(Math.Max(min, Block * 4))
            .SetTransferMode(AudioTrackMode.Stream)
            .Build();
        track.Play();
        running = true;
        mixer = new Thread(Mix) { IsBackground = true, Name = "fesh-audio" };
        mixer.Start();
    }

    public static void Close()
    {
        running = false;
        // Only let go of the track once the mixer has really stopped using it.
        if (mixer != null && !mixer.Join(1500)) return;
        var t = track;
        track = null;
        try { t?.Stop(); t?.Release(); } catch { }
    }

    // The app went to the background (or came back): stop feeding the speaker so nothing plays at nobody.
    public static void Pause() { try { track?.Pause(); } catch { } }
    public static void Resume() { try { track?.Play(); } catch { } }

    static void Mix()
    {
        var mix = new float[Block];
        var pcm = new short[Block];
        while (running)
        {
            Array.Clear(mix);
            lock (gate)
            {
                foreach (var v in voices)
                {
                    if (v == null || !v.Playing) continue;
                    var d = v.Clip.Data;
                    double step = v.Clip.Rate / (double)OutRate;
                    float vol = v.Volume / 32768f;
                    for (int i = 0; i < Block; i++)
                    {
                        int p = (int)v.Pos;
                        if (p + 1 >= d.Length) { v.Playing = false; break; }
                        float f = (float)(v.Pos - p);
                        mix[i] += (d[p] + (d[p + 1] - d[p]) * f) * vol;
                        v.Pos += step;
                    }
                }
            }
            for (int i = 0; i < Block; i++) pcm[i] = (short)Math.Clamp(mix[i] * 32767f, -32768f, 32767f);
            var t = track;
            if (t == null) break;
            try { t.Write(pcm, 0, Block); }   // blocks until the speaker wants more, which paces the loop
            catch (Exception) { break; }
        }
    }

    // ---------- Raylib's API ----------
    public static Wave LoadWav(byte[] file)
    {
        int pos = 12, channels = 1, rate = OutRate, bits = 16, dataAt = -1, dataLen = 0;
        while (pos + 8 <= file.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(file, pos, 4);
            int len = BitConverter.ToInt32(file, pos + 4);
            if (id == "fmt ") { channels = BitConverter.ToInt16(file, pos + 10); rate = BitConverter.ToInt32(file, pos + 12); bits = BitConverter.ToInt16(file, pos + 22); }
            else if (id == "data") { dataAt = pos + 8; dataLen = Math.Min(len, file.Length - dataAt); break; }
            pos += 8 + len + (len & 1);
        }
        if (dataAt < 0 || bits != 16) return default;
        int frames = dataLen / (2 * channels);
        var data = new short[frames];
        for (int i = 0; i < frames; i++)
        {
            int sum = 0;
            for (int c = 0; c < channels; c++) sum += BitConverter.ToInt16(file, dataAt + (i * channels + c) * 2);
            data[i] = (short)(sum / channels);
        }
        lock (gate) { waves.Add(new Clip { Data = data, Rate = rate }); return new Wave { FrameCount = (uint)frames, SampleRate = (uint)rate, SampleSize = 16, Channels = 1, Handle = waves.Count }; }
    }

    public static void UnloadWave(Wave w) { lock (gate) if (w.Handle > 0 && w.Handle <= waves.Count) waves[w.Handle - 1] = null; }

    public static Sound LoadSound(Wave w)
    {
        lock (gate)
        {
            if (w.Handle <= 0 || w.Handle > waves.Count || waves[w.Handle - 1] == null) return default;
            voices.Add(new Voice { Clip = waves[w.Handle - 1] });
            return new Sound { FrameCount = w.FrameCount, Handle = voices.Count };
        }
    }

    static Voice Get(Sound s) => s.Handle > 0 && s.Handle <= voices.Count ? voices[s.Handle - 1] : null;
    public static void Play(Sound s) { lock (gate) { var v = Get(s); if (v != null) { v.Pos = 0; v.Playing = true; } } }
    public static void Stop(Sound s) { lock (gate) { var v = Get(s); if (v != null) v.Playing = false; } }
    public static bool IsPlaying(Sound s) { lock (gate) return Get(s)?.Playing ?? false; }
    public static void SetVolume(Sound s, float vol) { lock (gate) { var v = Get(s); if (v != null) v.Volume = vol; } }
    public static void Unload(Sound s) { lock (gate) { if (s.Handle > 0 && s.Handle <= voices.Count) voices[s.Handle - 1] = null; } }
}
