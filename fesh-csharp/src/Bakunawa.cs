namespace Fesh;

// The Parola lighthouse and the vanishing moon. Tatay Celso, the keeper, needs three things to light his lamp again;
// once it burns, squid and tarpon come to the pier at night and he hands over the old keeper's log. With all four clues
// (Data.MoonClues) and the lamp lit, the next full moon over Parola is an eclipse: Bakunawa, the sea serpent of the old
// stories, rises with the moon in its jaws, and the islanders bang their pots while you beat the agong on the beat until
// it gives the moon back. Nobody fights it. The eclipse runs in mode "play" like the Tidemane fight (the clock stops,
// you stay put, and E beats the gong); it isn't saved, so a reload that night starts it again.
sealed class Eclipse
{
    public string Phase = "rise"; public float T, Beat, Rise; public int Noise, Hits, Misses; public bool Judged;
    // 1.35: three verses, each faster; a combo of hits on the beat counts double; between verses it writhes; it spits
    // seawater at the beach (verse 2 on) and roars over the noise (verse 3).
    public int Verse = 1, Combo, BestCombo; public float Writhe, Roar, RoarT = 5, SpoutT = 2.5f, Flinch;
    public List<(float x, float y, float t)> Spouts = new();
    public List<(float x, float y, float t)> Splashes = new();
}

partial class Game
{
    Eclipse eclipse;
    const float BeatWindow = 0.17f;
    // The beat quickens verse by verse.
    float BeatLen => (eclipse?.Verse ?? 1) switch { 1 => 0.75f, 2 => 0.64f, _ => 0.56f };
    const int VerseNoise = 6;
    const int NoiseNeeded = VerseNoise * 3;
    const float SerpentY = 545;
    // Where Bakunawa rises, in the channel off Parola's north shore, its tail clear of the pier (the Blender serpent is
    // much longer than the old one, so it rises further east).
    static float SerpentX => OldArt ? 1200 : 1265;

    // What Tatay Celso needs for each stage of the mending: the stairs, the lamp housing, then the lens.
    static readonly (string what, (string id, int n)[] needs)[] ParolaStages =
    {
        ("mend the stairs", new[] { ("wood", 10), ("stone", 4) }),
        ("mend the lamp housing", new[] { ("iron_bar", 2), ("copper_bar", 2) }),
        ("cut a new lens", new[] { ("crystal", 2) })
    };
    bool ParolaReady => state.parola < 3 && ParolaStages[state.parola].needs.All(p => Has(p.id) >= p.n);
    string ParolaNeeds => state.parola >= 3 ? "" : string.Join(" and ", ParolaStages[state.parola].needs.Select(p => Items.Amount(p.id, p.n)));
    int MoonClueCount => Data.MoonClues.Count(c => state.Hinted(c.Key));
    bool MoonReturned => state.Hinted("moonReturned");

    void TalkCelso()
    {
        Say C(string t) => new("Tatay Celso", t);
        if (!state.Hinted("metCelso"))
        {
            state.hinted["metCelso"] = true;
            Talk(new()
            {
                C("A visitor at Parola! Mind the stairs, they're rotten through. I keep this light, or I did, until the storm took it."),
                C("Since it went dark, the fishers say the moon goes out on the water some full-moon nights. I don't like it."),
                C($"Help me light it again. First I need to {ParolaStages[0].what}: {ParolaNeeds}.")
            });
            return;
        }
        if (state.parola < 3 && ParolaReady)
        {
            var stage = ParolaStages[state.parola];
            foreach (var (id, n) in stage.needs) Take(id, n);
            state.parola++;
            Sfx.Play("build");
            mapTexDirty = true;
            if (state.parola < 3)
            {
                Save();
                Talk(new() { C($"Salamat! That's enough to {stage.what}."), C($"Next I need to {ParolaStages[state.parola].what}: {ParolaNeeds}.") });
                return;
            }
            state.hinted["parolaLit"] = true;
            state.hinted["bk:log"] = true;
            Sfx.Play("rare");
            Save();
            Talk(new()
            {
                C("The new lens! Help me set it... there. Parola burns again! You'll see the beam from Asinan tonight."),
                C("The squid will come to the light now, and the big tarpon after them. Fish off my pier after dark."),
                C("And take this: the old keeper's log. Read the last page. The night the lamp failed, the moon went out on the strait, and they beat the agong on the pier until it came back."),
                new("", "New clue on your Case board (Habagat): The keeper's log.")
            });
            return;
        }
        if (state.parola < 3) { Talk(new() { C($"To {ParolaStages[state.parola].what} I need {ParolaNeeds}. Bring them when you can.") }); return; }
        if (MoonReturned) { Talk(new() { C("The moon's safe, the lamp's lit, and the tarpon are running. Mind the king of them on a full moon; he's as big as a boat.") }); return; }
        if (EclipseReady)
        {
            Talk(new()
            {
                C("You have the scale, the story, my log and Dado's agong. Then it's true, and it's coming back."),
                C($"On the next full-moon night, come here to Parola and bring the agong. Everyone will come with their pots. {(FullMoon ? "Tonight is full. Stay close." : $"The next full moon is in {(4 - MoonPhase + 8) % 8} days.")}")
            });
            return;
        }
        Talk(new() { C($"The light's burning. But the moon still worries me. You've found {MoonClueCount} of 4 clues; your Case board has the rest of the story.") });
    }

    /* ---------- The eclipse ---------- */
    // 1.35: after the moon is saved, Bakunawa comes back for a rematch on later full-moon nights (once a night).
    bool EclipseReady => (!MoonReturned || state.gifts.GetValueOrDefault("eclipse", -1) != state.day) && state.parola >= 3 && MoonClueCount == Data.MoonClues.Length && Has("agong") > 0;
    bool OnParola => scene == "world" && !Aboard && !Riding && Dist(player.X, player.Y, LighthouseX, LighthouseY) < 150 && !WaterTile(TileUnder(player.X, player.Y));

    void CheckEclipse()
    {
        if (eclipse != null || mode != "play" || !EclipseReady || !FullMoon || Stormy || !OnParola) return;
        eclipse = new Eclipse();
        if (MoonReturned) state.gifts["eclipse"] = state.day;
        FaceToward(SerpentX, SerpentY);
        player.Moving = false;
        Talk(new()
        {
            new("", "The moon's reflection on the strait shivers... and goes out. Something enormous is rising out of the sea."),
            new("Tatay Celso", "Bakunawa! It has the moon! Everybody, make noise! Beat the agong, on the beat, with us!"),
            new("", "Press <act> each time the ring closes on the agong. Off the beat, the noise falls apart.")
        });
    }

    // Only the agong, and only while the noise is on (FindTarget returns this alone during the eclipse, so nothing else
    // nearby is offered while it rises or gives the moon back).
    Target EclipseTarget() => eclipse?.Phase == "bang" ? new Target { Type = "agong", Label = "Beat the agong on the beat!" } : null;

    void UpdateEclipse(float dt)
    {
        var e = eclipse;
        if (e == null || mode != "play") return;
        e.T += dt;
        switch (e.Phase)
        {
            case "rise":
                e.Rise = Math.Min(1, e.T / 3);
                quake = 0.2f;
                if (e.T >= 3) { e.Phase = "bang"; e.T = 0; e.Beat = 0; }
                break;
            case "bang":
            {
                e.Flinch = Math.Max(0, e.Flinch - dt);
                UpdateSpouts(e, dt);
                if (e.Writhe > 0)
                {
                    // Between verses it thrashes, half sinking and heaving up again, then the beat comes back faster.
                    e.Writhe -= dt;
                    e.Rise = 0.72f + 0.28f * MathF.Abs(MathF.Cos(e.Writhe * 3.2f));
                    quake = 0.15f;
                    if (e.Writhe <= 0)
                    {
                        e.Verse++; e.Rise = 1; e.Beat = 0; e.Judged = false; e.SpoutT = 1.5f; e.RoarT = 1.8f;
                        Floater(e.Verse == 2 ? "Faster!" : "Faster still!", player.X, player.Y - 34, "#ffd76a");
                        Sfx.Play("gong");
                    }
                    break;
                }
                // The villagers' pots clang on every beat. One strike counts per beat (the window opens again halfway
                // between beats); a beat that slips by with no strike costs nothing, a strike off the beat does.
                float before = e.Beat;
                e.Beat += dt;
                if (e.Beat >= BeatLen) { e.Beat -= BeatLen; if (e.Roar <= 0) Sfx.Play("clang"); }
                if (before < BeatLen / 2 && e.Beat >= BeatLen / 2) e.Judged = false;
                // The last verse: now and then it roars over the noise for a couple of beats. Strikes then are lost.
                if (e.Verse >= 3)
                {
                    if (e.Roar > 0) e.Roar -= dt;
                    else if ((e.RoarT -= dt) <= 0)
                    {
                        e.Roar = BeatLen * 2.5f; e.RoarT = Rand(5f, 7f);
                        quake = 0.45f; Sfx.Play("splash");
                        Floater("ROAR! Hold off!", player.X, player.Y - 34, "#ff6a5a");
                    }
                }
                break;
            }
            case "spit":
                e.Rise = Math.Max(0, 1 - e.T / 3.5f);
                if (e.T >= 3.5f) { e.Phase = "done"; ShowMoonCard(); }
                break;
        }
    }

    // E during the eclipse: on the beat (the ring closing), the noise builds; off it, it falls apart a little.
    void BeatAgong()
    {
        var e = eclipse;
        if (e?.Phase != "bang") return;
        if (e.Writhe > 0) return;   // it's thrashing: wait for the beat to come back
        float off = MathF.Min(e.Beat, BeatLen - e.Beat);
        Swing("mallet", 0.2f);
        if (e.Roar > 0)
        {
            // Its roar drowns the agong out, and the run of beats is broken.
            e.Combo = 0;
            Sfx.Play("nope");
            Floater("Drowned out!", player.X, player.Y - 30, "#ff9a8a");
            return;
        }
        if (off <= BeatWindow && !e.Judged)
        {
            e.Judged = true;
            e.Combo++; e.BestCombo = Math.Max(e.BestCombo, e.Combo); e.Hits++;
            // Four on the beat in a row and every strike after counts twice.
            int add = e.Combo >= 4 ? 2 : 1;
            e.Noise = Math.Min(NoiseNeeded, e.Noise + add);
            e.Flinch = 0.25f;
            Sfx.Play("gong");
            Floater(e.Noise >= NoiseNeeded ? "BONG!" : add == 2 ? $"THUNDER! x{e.Combo}" : "Bong!", player.X, player.Y - 30, add == 2 ? "#ffb04a" : "#ffd76a");
            quake = add == 2 ? 0.2f : 0.12f;
            if (e.Noise >= NoiseNeeded) { e.Phase = "spit"; e.T = 0; e.Spouts.Clear(); Sfx.Play("rare"); quake = 0.5f; Toast("Bakunawa rears back... and spits out the moon!", 4); }
            else if (e.Noise >= e.Verse * VerseNoise && e.Verse < 3)
            {
                // A verse done: it loses its grip for a moment and thrashes.
                e.Writhe = 2.2f; e.Spouts.Clear(); e.Combo = 0;
                quake = 0.6f; Sfx.Play("splash");
                Toast(e.Verse == 1 ? "Bakunawa writhes! Its grip on the moon slips. Get ready: it will spit seawater at the beach." : "It writhes again! The last verse: when it roars, hold off the agong.", 4);
            }
        }
        else
        {
            e.Misses++;
            e.Combo = 0;
            e.Noise = Math.Max((e.Verse - 1) * VerseNoise, e.Noise - 1);
            Sfx.Play("nope");
            Floater("Off the beat", player.X, player.Y - 30, "#ff9a8a");
        }
    }

    // From the second verse it spits seawater at the beach: rings mark where it will come down (shrinking as it falls), and
    // anyone still standing there is knocked off their feet and the noise falters. Step aside, keep the beat.
    void UpdateSpouts(Eclipse e, float dt)
    {
        for (int i = e.Splashes.Count - 1; i >= 0; i--)
        {
            var s = e.Splashes[i];
            s.t -= dt;
            if (s.t <= 0) e.Splashes.RemoveAt(i); else e.Splashes[i] = s;
        }
        for (int i = e.Spouts.Count - 1; i >= 0; i--)
        {
            var s = e.Spouts[i];
            s.t -= dt;
            if (s.t > 0) { e.Spouts[i] = s; continue; }
            e.Spouts.RemoveAt(i);
            e.Splashes.Add((s.x, s.y, 0.6f));
            Burst(s.x, s.y - 2, "#cfe8ee", 10);
            Sfx.Play("splash");
            quake = Math.Max(quake, 0.2f);
            if (Dist(player.X, player.Y, s.x, s.y) < SpoutR && iframes <= 0)
            {
                state.hp = Math.Max(0, state.hp - 8 * ArmourMul);
                iframes = 0.8f; hurtFlash = 0.35f; lastHitT = time;
                e.Combo = 0; e.Noise = Math.Max((e.Verse - 1) * VerseNoise, e.Noise - 1);
                Sfx.Play("hurt");
                Floater("Soaked!", player.X, player.Y - 30, "#9fd8ff");
                if (state.hp <= 0) { Faint(); return; }
            }
        }
        if (e.Verse < 2 || e.Writhe > 0 || e.Phase != "bang") return;
        if ((e.SpoutT -= dt) > 0) return;
        e.SpoutT = e.Verse >= 3 ? Rand(2.6f, 3.4f) : Rand(3.2f, 4.2f);
        int n = e.Verse >= 3 ? 3 : 2;
        // One right where you stand, the others round about.
        for (int k = 0; k < n; k++)
        {
            float a = Rand(0, MathF.Tau), d = k == 0 ? 0 : Rand(16, 30);
            e.Spouts.Add((player.X + MathF.Cos(a) * d, player.Y + MathF.Sin(a) * d * 0.7f, SpoutFall));
        }
    }
    const float SpoutR = 9, SpoutFall = 1.4f;

    // The spouts: a shrinking ring on the sand, and the water coming down from the serpent's jaws as it falls.
    void DrawSpouts(float t)
    {
        var e = eclipse;
        if (e == null) return;
        var (mx, my) = SerpentMouth(t);
        foreach (var (x, y, left) in e.Spouts)
        {
            float k = 1 - left / SpoutFall;
            float rr = SpoutR * (1.6f - k * 0.6f);
            Disc(x, y, SpoutR, Pal.Rgba(255, 60, 50, 0.12f + 0.18f * k));
            pix.Ring(x, y, rr, Pal.Rgba(255, 120, 90, 0.6f + 0.4f * k));
            pix.Ring(x, y, rr + 1, Pal.Rgba(255, 90, 70, 0.5f + 0.4f * k));
            pix.Ring(x, y, SpoutR * 0.4f, Pal.Rgba(255, 90, 70, 0.5f * k));
            // The water in the air, arcing from the mouth.
            float bx = mx + (x - mx) * k, by = my + (y - my) * k - MathF.Sin(k * MathF.PI) * 50;
            pix.Rect(bx - 2, by - 2, 4, 4, Pal.Rgba(170, 220, 240, 0.9f));
            pix.Rect(bx - 1, by - 3, 2, 1, "#eef9fb");
        }
        foreach (var (x, y, left) in e.Splashes)
        {
            float k = 1 - left / 0.6f;
            pix.Ring(x, y, 3 + k * 12, Pal.Rgba(220, 240, 250, 0.8f * (1 - k)));
            for (int i = 0; i < 6; i++) pix.Rect(x + (i - 3) * 2.5f, y - 8 * (1 - k) * MathF.Abs(MathF.Sin(i * 1.3f + 0.5f)), 1, 2, Pal.Rgba(220, 240, 250, 0.9f * (1 - k)));
        }
    }

    void ShowMoonCard()
    {
        catchOpenedAt = Raylib_cs.Raylib.GetTime();
        mode = "moon";
        SetPrompt("");
    }

    void CloseMoon()
    {
        if (mode != "moon" || Raylib_cs.Raylib.GetTime() - catchOpenedAt < 0.6) return;
        eclipse = null;
        mode = "play";
        if (MoonReturned)
        {
            // A rematch: the moon back again, and the islands' thanks.
            state.coins += 120;
            Sfx.Play("ui");
            Save();
            Talk(new() { new("Tatay Celso", "It came back for the moon, and we drove it off again! Here, the islands put this together for you."), new("", "You got 120 coins.") });
            return;
        }
        state.hinted["moonReturned"] = true;
        state.gifts["eclipse"] = state.day;   // no rematch the same night
        Give("moon_charm");
        Sfx.Play("ui");
        Save();
        Talk(new()
        {
            new("Tatay Celso", "Look at it, full and bright again, and the old serpent sinking home. We did it, all of us together."),
            new("Lola Pacing", "Just like my lola told it. Keep that scale it left you; it will bring you luck under the moon."),
            new("Tatay Celso", "And the king of the tarpon rises under a moon like this one. Try my pier on the next full moon, with a live tamban."),
            new("", "Case closed (Habagat). You got a Moonscale charm: rare luck +1 at night.")
        });
    }

    /* ---------- Drawing ---------- */
    // Bakunawa: a serpent of dark blue-green coils breaking the water, fins along its back, and a dragon's head holding
    // the moon in its jaws (until it spits it out).
    // The serpent is a Blender model (tools/blender/bakunawa.py renders BakunawaArt.cs) in eight frames of its humps
    // rolling forward; FESH_OLDART=1 draws the old hand-made one. Each pixel knows its height above the water, so rising
    // and sinking push the whole serpent down by up to SerpentSink rows and drop whatever is under the surface.
    const int SerpentSink = 96;
    static (Raylib_cs.Color[] col, byte[] h)[] serpentFrames;

    static (Raylib_cs.Color[] col, byte[] h) SerpentFrame(int f)
    {
        if (serpentFrames == null)
        {
            var pal = BakunawaArt.Palette.Select(Pal.C).ToArray();
            int n = BakunawaArt.Width * BakunawaArt.Height;
            serpentFrames = new (Raylib_cs.Color[], byte[])[BakunawaArt.Frames];
            for (int i = 0; i < BakunawaArt.Frames; i++)
            {
                using var src = new MemoryStream(Convert.FromBase64String(BakunawaArt.Data[i]));
                using var z = new System.IO.Compression.ZLibStream(src, System.IO.Compression.CompressionMode.Decompress);
                var raw = new byte[n * 2];
                int got = 0;
                while (got < raw.Length) { int r = z.Read(raw, got, raw.Length - got); if (r <= 0) break; got += r; }
                var col = new Raylib_cs.Color[n];
                for (int p = 0; p < n; p++) col[p] = raw[p] == 255 ? default : pal[raw[p]];
                serpentFrames[i] = (col, raw[n..]);
            }
        }
        return serpentFrames[f];
    }

    // Where the moon sits in the serpent's jaws right now (world coordinates), and how far it has sunk.
    int SerpentFrameNow(float t) => (int)(t * 5) % BakunawaArt.Frames;
    float SerpentDepth => eclipse == null ? SerpentSink : (1 - eclipse.Rise) * SerpentSink;
    (float x, float y) SerpentMouth(float t)
    {
        if (OldArt) return (SerpentX + 1, SerpentY - 22 + (1 - (eclipse?.Rise ?? 0)) * 16);
        var m = BakunawaArt.Mouth[SerpentFrameNow(t)];
        return (SerpentX + m.x, SerpentY + m.y + SerpentDepth);
    }

    void DrawEclipse(float t)
    {
        var e = eclipse;
        if (e == null) return;
        float k = e.Rise;
        if (k <= 0 && e.Phase != "rise") return;
        if (!OldArt) { DrawSerpent(e, t); return; }
        int sink = (int)MathF.Round((1 - k) * 16);
        int x = (int)SerpentX, y = (int)SerpentY + sink;
        void Coil(float cx, float r, float ph)
        {
            for (int a = 0; a <= 12; a++)
            {
                float u = a / 12f * MathF.PI;
                float px = cx + MathF.Cos(u) * r, py = y - MathF.Sin(u) * r * 0.9f + MathF.Sin(t * 2 + ph) * 1.2f;
                if (py > y + 1) continue;
                pix.Rect(px - 3, py - 3, 7, 6, "#24585e");
                pix.Rect(px - 2, py - 3, 5, 2, "#3f8a88");
                pix.Rect(px - 1, py - 3, 3, 1, "#7fd0c4");
                if (a % 3 == 1) { pix.Rect(px - 1, py - 6, 2, 3, "#5fd6c9"); }
            }
            pix.Ring(cx - r, y + 1, 3 + MathF.Sin(t * 3 + ph) * 0.6f, Pal.Rgba(235, 248, 252, 0.6f));
            pix.Ring(cx + r, y + 1, 3 + MathF.Sin(t * 3 + ph + 1) * 0.6f, Pal.Rgba(235, 248, 252, 0.6f));
        }
        Coil(x - 46, 9, 0); Coil(x - 22, 11, 1.4f); Coil(x + 30, 8, 2.6f);
        // The neck rising to the head, the head turned toward the island.
        int hy = y - 26 - (int)(MathF.Sin(t * 1.5f) * 2);
        for (int i = 0; i < 10; i++) { pix.Rect(x - 4 + i / 3, y - 2 - i * 3, 8, 4, i % 2 == 0 ? "#24585e" : "#2f6a6a"); pix.Rect(x - 4 + i / 3, y - 2 - i * 3, 2, 4, "#7fd0c4"); }
        pix.Rect(x - 7, hy - 6, 16, 9, "#2f6a6a"); pix.Rect(x - 7, hy - 6, 16, 2, "#5fb0a8"); pix.Rect(x - 9, hy - 2, 4, 6, "#24585e");
        pix.Rect(x - 6, hy - 10, 3, 4, "#5fd6c9"); pix.Rect(x + 4, hy - 10, 3, 4, "#5fd6c9");
        pix.Rect(x + 6, hy - 4, 2, 2, "#ffd76a"); pix.Rect(x - 3, hy - 4, 2, 2, "#ffd76a");
        bool jaws = e.Phase is "rise" or "bang";
        pix.Rect(x - 6, hy + 3, 14, 3, "#1a3a40");
        if (jaws)
        {
            // The moon, glowing in its mouth.
            pix.Glow(x + 1, hy + 4, 14, Pal.Rgba(255, 246, 208, 0.45f));
            pix.Rect(x - 2, hy + 1, 7, 7, "#fff6d0"); pix.Rect(x - 1, hy, 5, 9, "#fff6d0"); pix.Rect(x, hy + 2, 2, 2, "#e8dcae");
            pix.Rect(x - 5, hy + 7, 13, 2, "#1a3a40");
        }
        else if (e.Phase == "spit")
        {
            // The moon flies up and away.
            float m = Math.Min(1, e.T / 1.5f);
            float mx = x + m * 30, my = hy - m * 120;
            pix.Glow(mx, my, 16, Pal.Rgba(255, 246, 208, 0.5f * (1 - m * 0.5f)));
            pix.Rect(mx - 3, my - 3, 7, 7, "#fff6d0");
        }
    }

    void DrawSerpent(Eclipse e, float t)
    {
        int f = SerpentFrameNow(t);
        var (col, h) = SerpentFrame(f);
        int d = (int)MathF.Round(SerpentDepth);
        int ox = (int)SerpentX - BakunawaArt.AnchorX, oy = (int)SerpentY - BakunawaArt.AnchorY + d;
        int w = BakunawaArt.Width;
        for (int j = 0; j < BakunawaArt.Height; j++)
            for (int i = 0; i < w; i++)
            {
                int p = j * w + i;
                if (col[p].A == 0 || h[p] <= d) continue;
                pix.Rect(ox + i, oy + j, 1, 1, col[p]);
            }
        // Foam where the body breaks the surface, and round the neck.
        var foam = Pal.Rgba(235, 248, 252, 0.6f * Math.Min(1, e.Rise * 1.5f));
        foreach (var (sx, sy) in BakunawaArt.Splash[f].Append((22, 0)))
        {
            float r = 3 + MathF.Sin(t * 3 + sx * 0.1f) * 0.6f;
            pix.Ring(SerpentX + sx, SerpentY + sy + 1, r, foam);
            pix.Ring(SerpentX + sx, SerpentY + sy + 1, r + 3, Pal.Rgba(235, 248, 252, 0.25f * Math.Min(1, e.Rise * 1.5f)));
        }
        var (mx, my) = SerpentMouth(t);
        if (e.Phase is "rise" or "bang" && -BakunawaArt.Mouth[f].y > d - 4)
        {
            // The moon, glowing in its jaws.
            pix.Glow(mx, my, 18, Pal.Rgba(255, 246, 208, 0.45f));
            pix.Rect(mx - 4, my - 3, 9, 7, "#fff6d0"); pix.Rect(mx - 3, my - 4, 7, 9, "#fff6d0");
            pix.Rect(mx - 1, my - 2, 2, 2, "#e8dcae"); pix.Rect(mx + 2, my + 1, 1, 1, "#e8dcae");
        }
        else if (e.Phase == "spit")
        {
            // The moon flies up and away.
            float m = Math.Min(1, e.T / 1.5f);
            float fx = mx + m * 40, fy = my - m * 140;
            pix.Glow(fx, fy, 18, Pal.Rgba(255, 246, 208, 0.5f * (1 - m * 0.5f)));
            pix.Rect(fx - 4, fy - 4, 9, 9, "#fff6d0");
        }
    }

    // The agong over your head, and the beat ring closing on it: strike as they meet.
    void DrawEclipseRing()
    {
        if (eclipse?.Phase != "bang") return;
        var e = eclipse;
        float ph = e.Beat / BeatLen;
        int gx = (int)MathF.Round(player.X), gy = (int)MathF.Round(player.Y) - 26;
        pix.Rect(gx - 3, gy - 3, 7, 7, "#a8742a"); pix.Rect(gx - 2, gy - 2, 5, 5, "#c98b3a"); pix.Rect(gx, gy, 1, 1, "#ffe8a0");
        if (e.Writhe > 0) return;   // no beat while it thrashes
        float off = ph < 0.5f ? ph : 1 - ph;   // how far from the beat
        // Red while it roars (hold off); gold, and orange once a run of four makes every strike count twice.
        var ring = e.Roar > 0 ? Pal.Rgba(255, 80, 70, 0.95f - off) : e.Combo >= 3 ? Pal.Rgba(255, 170, 70, 0.95f - off) : Pal.Rgba(255, 215, 106, 0.95f - off);
        pix.Ring(gx + 0.5, gy + 0.5, 4 + off * 22, ring);
        if (e.Roar > 0) { pix.Rect(gx - 4, gy - 4, 9, 1, "#ff5040"); pix.Rect(gx - 4, gy + 4, 9, 1, "#ff5040"); }
        // The run of beats: a pip for each, up to four.
        for (int i = 0; i < Math.Min(4, e.Combo); i++) pix.Rect(gx - 5 + i * 3, gy + 7, 2, 2, e.Combo >= 4 ? "#ffb04a" : "#ffd76a");
    }

    // During the eclipse the moonlight goes out: a deep red tint over everything, lifting as the moon comes back.
    void DrawEclipseSky()
    {
        if (eclipse == null) return;
        float k = eclipse.Phase == "spit" ? Math.Max(0, 1 - eclipse.T / 2) : eclipse.Phase == "rise" ? eclipse.Rise : 1;
        if (k > 0) pix.Fill(0, 0, W, H, Pal.Rgba(110, 14, 26, 0.36f * k));
    }

    // Where the islanders stand on Parola's north beach, banging their pots, during the eclipse.
    static readonly Dictionary<string, (float x, float y)> CrowdSpot = new()
    {
        ["rosa"] = (1148, 598), ["pacing"] = (1158, 587), ["dado"] = (1198, 589), ["celso"] = (1212, 599)
    };

    void DrawCrowdPerson(string id)
    {
        var (fx, fy) = CrowdSpot[id];
        int x = (int)fx, y = (int)fy;
        var (skin, hair, shirt, pants, longHair, hat) = FolkLook(id);
        // Each one bangs on their own beat, a little after the last.
        bool up = eclipse.Phase == "bang" && (eclipse.Beat / BeatLen + id.Length * 0.13f) % 1 < 0.4f;
        LookData.DrawFigure(pix, skin, hair, shirt, pants, longHair, hat, "#e04b3a", x, y, "up", 0, arms: up ? 2 : 0);
        // A pot (or a pan) held up in both hands.
        int py = up ? y - 15 - LookData.Stature : y - 7 - LookData.Stature;
        pix.Rect(x - 2, py, 5, 2, id == "pacing" ? "#b5764a" : "#8a8f93");
        pix.Rect(x - 1, py - 1, 3, 1, "#b4b9bc");
        if (up && eclipse.Beat < 0.12f) { pix.Rect(x - 4, py - 2, 1, 1, "#ffe8a0"); pix.Rect(x + 4, py - 3, 1, 1, "#ffe8a0"); }
    }

    // At night the serpent would vanish into the dark: the moon in its jaws lights it up.
    void LightEclipse()
    {
        if (eclipse == null) return;
        if (OldArt) { LightHole(SerpentX, SerpentY - 18 + (1 - eclipse.Rise) * 16, 76, 0.8f); return; }
        var (mx, my) = SerpentMouth(time);
        LightHole(SerpentX - 50, SerpentY - 25 + SerpentDepth, 175, 1f);
        LightHole(mx, my, 40, 0.9f);
    }

    void GlowEclipse(float k)
    {
        if (eclipse?.Phase is not ("rise" or "bang")) return;
        var (mx, my) = SerpentMouth(time);
        pix.Glow(mx, my, OldArt ? 20 : 26, Pal.Rgba(255, 246, 208, 0.45f * k));
    }

    // The lighthouse beam sweeping round after dark.
    void LightParola(float t)
    {
        if (state.parola < 3 || Math.Abs(LighthouseX - player.X) > 420 || Math.Abs(LighthouseY - player.Y) > 300) return;
        float lx = LighthouseX, ly = LighthouseY - LampRise, a = t * 0.7f;
        LightHole(lx, ly, 18, 0.95f);
        for (int i = 1; i <= 8; i++)
            LightHole(lx + MathF.Cos(a) * i * 22, ly + MathF.Sin(a) * i * 13, 8 + i * 2.5f, 0.75f - i * 0.06f);
    }

    // The lamp, and the beam itself: a pale shaft sweeping out over the water.
    void GlowParola(float t, float k)
    {
        if (state.parola < 3 || Math.Abs(LighthouseX - player.X) > 420 || Math.Abs(LighthouseY - player.Y) > 300) return;
        float lx = LighthouseX, ly = LighthouseY - LampRise, a = t * 0.7f;
        pix.Glow(lx, ly, 14, Pal.Rgba(255, 226, 138, 0.45f * k));
        for (int i = 1; i <= 9; i++)
            pix.Glow(lx + MathF.Cos(a) * i * 20, ly + MathF.Sin(a) * i * 12, 4 + i * 1.6f, Pal.Rgba(255, 238, 180, (0.2f - i * 0.016f) * k));
    }
}
