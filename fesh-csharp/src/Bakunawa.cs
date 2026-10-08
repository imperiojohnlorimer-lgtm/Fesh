namespace Fesh;

// The Parola lighthouse and the vanishing moon. Tatay Celso, the keeper, needs three things to light his lamp again;
// once it burns, squid and tarpon come to the pier at night and he hands over the old keeper's log. With all four clues
// (Data.MoonClues) and the lamp lit, the next full moon over Parola is an eclipse: Bakunawa, the sea serpent of the old
// stories, rises with the moon in its jaws, and the islanders bang their pots while you beat the agong on the beat until
// it gives the moon back. Nobody fights it. The eclipse runs in mode "play" like the Tidemane fight (the clock stops,
// you stay put, and E beats the gong); it isn't saved, so a reload that night starts it again.
sealed class Eclipse { public string Phase = "rise"; public float T, Beat, Rise; public int Noise, Hits, Misses; public bool Judged; }

partial class Game
{
    Eclipse eclipse;
    const float BeatLen = 0.75f, BeatWindow = 0.17f;
    const int NoiseNeeded = 14;
    const float SerpentX = 1200, SerpentY = 545;     // where Bakunawa rises, in the channel just off Parola's north shore (clear of the pier)

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
    bool EclipseReady => !MoonReturned && state.parola >= 3 && MoonClueCount == Data.MoonClues.Length && Has("agong") > 0;
    bool OnParola => scene == "world" && !Aboard && !Riding && Dist(player.X, player.Y, LighthouseX, LighthouseY) < 150 && !WaterTile(TileUnder(player.X, player.Y));

    void CheckEclipse()
    {
        if (eclipse != null || mode != "play" || !EclipseReady || !FullMoon || Stormy || !OnParola) return;
        eclipse = new Eclipse();
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
                // The villagers' pots clang on every beat. One strike counts per beat (the window opens again halfway
                // between beats); a beat that slips by with no strike costs nothing, a strike off the beat does.
                float before = e.Beat;
                e.Beat += dt;
                if (e.Beat >= BeatLen) { e.Beat -= BeatLen; Sfx.Play("clang"); }
                if (before < BeatLen / 2 && e.Beat >= BeatLen / 2) e.Judged = false;
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
        float off = MathF.Min(e.Beat, BeatLen - e.Beat);
        swingT = 0.2f;
        if (off <= BeatWindow && !e.Judged)
        {
            e.Judged = true;
            e.Noise++; e.Hits++;
            Sfx.Play("gong");
            Floater(e.Noise >= NoiseNeeded ? "BONG!" : "Bong!", player.X, player.Y - 30, "#ffd76a");
            quake = 0.12f;
            if (e.Noise >= NoiseNeeded) { e.Phase = "spit"; e.T = 0; Sfx.Play("rare"); quake = 0.5f; Toast("Bakunawa rears back... and spits out the moon!", 4); }
        }
        else
        {
            e.Misses++;
            e.Noise = Math.Max(0, e.Noise - 1);
            Sfx.Play("nope");
            Floater("Off the beat", player.X, player.Y - 30, "#ff9a8a");
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
        state.hinted["moonReturned"] = true;
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
    void DrawEclipse(float t)
    {
        var e = eclipse;
        if (e == null) return;
        float k = e.Rise;
        if (k <= 0 && e.Phase != "rise") return;
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

    // The agong over your head, and the beat ring closing on it: strike as they meet.
    void DrawEclipseRing()
    {
        if (eclipse?.Phase != "bang") return;
        float ph = eclipse.Beat / BeatLen;
        int gx = (int)MathF.Round(player.X), gy = (int)MathF.Round(player.Y) - 26;
        pix.Rect(gx - 3, gy - 3, 7, 7, "#a8742a"); pix.Rect(gx - 2, gy - 2, 5, 5, "#c98b3a"); pix.Rect(gx, gy, 1, 1, "#ffe8a0");
        float off = ph < 0.5f ? ph : 1 - ph;   // how far from the beat
        pix.Ring(gx + 0.5, gy + 0.5, 4 + off * 22, Pal.Rgba(255, 215, 106, 0.95f - off));
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
        int py = up ? y - 15 : y - 7;
        pix.Rect(x - 2, py, 5, 2, id == "pacing" ? "#b5764a" : "#8a8f93");
        pix.Rect(x - 1, py - 1, 3, 1, "#b4b9bc");
        if (up && eclipse.Beat < 0.12f) { pix.Rect(x - 4, py - 2, 1, 1, "#ffe8a0"); pix.Rect(x + 4, py - 3, 1, 1, "#ffe8a0"); }
    }

    // At night the serpent would vanish into the dark: the moon in its jaws lights it up.
    void LightEclipse()
    {
        if (eclipse == null) return;
        float sink = (1 - eclipse.Rise) * 16;
        LightHole(SerpentX, SerpentY - 18 + sink, 76, 0.8f);
    }

    void GlowEclipse(float k)
    {
        if (eclipse?.Phase is not ("rise" or "bang")) return;
        float sink = (1 - eclipse.Rise) * 16;
        pix.Glow(SerpentX + 1, SerpentY - 22 + sink, 20, Pal.Rgba(255, 246, 208, 0.45f * k));
    }

    // The lighthouse beam sweeping round after dark.
    void LightParola(float t)
    {
        if (state.parola < 3 || Math.Abs(LighthouseX - player.X) > 420 || Math.Abs(LighthouseY - player.Y) > 300) return;
        float lx = LighthouseX, ly = LighthouseY - 44, a = t * 0.7f;
        LightHole(lx, ly, 18, 0.95f);
        for (int i = 1; i <= 8; i++)
            LightHole(lx + MathF.Cos(a) * i * 22, ly + MathF.Sin(a) * i * 13, 8 + i * 2.5f, 0.75f - i * 0.06f);
    }

    // The lamp, and the beam itself: a pale shaft sweeping out over the water.
    void GlowParola(float t, float k)
    {
        if (state.parola < 3 || Math.Abs(LighthouseX - player.X) > 420 || Math.Abs(LighthouseY - player.Y) > 300) return;
        float lx = LighthouseX, ly = LighthouseY - 44, a = t * 0.7f;
        pix.Glow(lx, ly, 14, Pal.Rgba(255, 226, 138, 0.45f * k));
        for (int i = 1; i <= 9; i++)
            pix.Glow(lx + MathF.Cos(a) * i * 20, ly + MathF.Sin(a) * i * 12, 4 + i * 1.6f, Pal.Rgba(255, 238, 180, (0.2f - i * 0.016f) * k));
    }
}
