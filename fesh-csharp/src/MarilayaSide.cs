using Raylib_cs;

namespace Fesh;

// Marilaya's extras (1.23), none of them needed for the story: Mang Ambo's guided walk out on the sandbar at low tide
// (with a time to be back by, and three tide-pool animals to look at and leave be), Lorna's spring-heated dryer (raw
// fish to daing in two hours, rain or shine), Aling Sela's cache hunt with the survey compass once the chart is inked,
// kalutang practice with Mang Iyo in the town square (call and response; the NCCA runs kalutang workshops for teachers
// and students in Marinduque, so it's an ordinary practice, not a rite), and Aling Nita's butterfly nursery (the lime
// butterfly, whose caterpillars eat citrus leaves such as calamansi; Marinduque has butterfly farms).
sealed class MlKalutang
{
    public int Round;                   // 0..Phrases.Length-1, then done
    public int Pos;                     // your notes played in this phrase
    public float T;                     // time in the current part
    public bool Listening = true;       // Mang Iyo is playing the phrase
    public bool Slow;                   // after a miss, he plays it again more slowly
    public string Note = "";
    public float FlashHi, FlashLo;
}
sealed class MlNursery { public int Step; public string Note = ""; public readonly HashSet<int> Seen = new(); public float Emerge; }

partial class Game
{
    /* ---------- Targets ---------- */
    Target SideTarget()
    {
        float x = player.X, y = player.Y;
        // The tide-pool animals on the sandbar, while it's out.
        for (int i = 0; i < MlPools.Length; i++)
        {
            var (px, py) = MlPoolAt(i);
            if (Dist(x, y, px, py + 4) < 12 && SandbarDryAt(px, py)) return new Target { Type = "marilaya", Id = $"pool{i}", Label = $"Look at the {MlPools[i].name}" };
        }
        // Sela's cache, once she's set the hunt.
        if (state.Hinted("mr:cacheAsked") && !state.Hinted("mr:cache") && Dist(x, y, CacheX, CacheY) < 12) return new Target { Type = "marilaya", Id = "cachedig", Label = "Dig here" };
        return null;
    }

    void UseSide(Target t)
    {
        if (t.Id.StartsWith("cave")) { UseCave(t.Id); return; }
        if (t.Id.StartsWith("pool")) { LookAtPool(int.Parse(t.Id[4..])); return; }
        if (t.Id == "cachedig") DigCache();
    }

    /* ---------- Mang Ambo and the sandbar ---------- */
    // The bar's out and joined to the beach all the way while the sea's below its low saddle.
    bool BarOut => SeaLevel <= barCutLevel;
    // When the low saddle near the beach goes under again, in clock minutes from now.
    float MinutesToSaddle()
    {
        float now = TideNow;
        for (float m = 0; m < 800; m += 1) if (TideAt(now + m) > barCutLevel) return m;
        return 0;
    }

    string AmboLabel() => BarOut && MinutesToSaddle() > 40 ? "Ask Mang Ambo about the tide pools" : "Talk to Mang Ambo";
    // Mang Ambo waits at the bar's beach end, by the low part, while an outing's on.
    void PlaceAmbo()
    {
        var s = MlWalk("ambo");
        var f = MlFolk.First(f => f.id == "ambo");
        var (hx, hy) = AmboWalkOn ? MlPlace("barroot") : (MlWx(f.lx), MlWy(f.ly));
        if (s.HomeX != hx || s.HomeY != hy) { s.HomeX = hx; s.HomeY = hy; s.ToX = s.ToY = null; if (standStill) { s.X = hx; s.Y = hy; } }
    }

    void TalkAmbo()
    {
        Say A(string t) => new("Mang Ambo", t);
        if (!BarOut || MinutesToSaddle() <= 40)
        {
            int next = LowIndex(TideNow);
            if (LowAt(next) < TideNow + 30) next++;
            float at = LowAt(next);
            var (_, clock) = FromTideMinute(at);
            Talk(new()
            {
                A(BarOut ? "The tide's turned: the bar's going under. We stay ashore now." : "The bar's under the water now. It comes up at low tide."),
                A($"Next low tide's about {ClockText(clock, 10)}. Come and find me then and I'll show you when to visit the tide pools. Lola Pacing's tide table tells you too."),
                A("And never walk out on a bar by yourself without knowing when the tide turns. The low part near the shore goes under first.")
            });
            return;
        }
        float back = MinutesToSaddle() - 30;
        int backAt = (int)((state.clock + back) % (24 * 60));
        state.gifts["mr_walkDay"] = state.day;
        state.gifts["mr_walkBy"] = AbsNow + (int)back;
        Save();
        Talk(new()
        {
            A("Low tide, and the bar's out! There are tide pools out there. Look, but don't take anything: everything in them lives there."),
            A($"Be back on the beach by {ClockText(backAt, 10)}. That's half an hour before the low part near the beach goes under again: always leave yourself time. I'll wait here by the low part and call you back."),
            A("And see the bar's shape? After every big storm it's a different shape: the waves move the sand about. Find me three things out there: a sea star, a sea cucumber and a hermit crab.")
        });
    }

    // While you're out on the bar after Mang Ambo's time, he calls you back: once per outing, and only once you're out
    // there to hear it (Codex: it used to be used up while you were ashore, and lost at dawn).
    bool AmboWalkOn => state.gifts.TryGetValue("mr_walkBy", out int by) && AbsNow < by + 180;
    void AmboCalls()
    {
        if (!AmboWalkOn || !state.gifts.TryGetValue("mr_walkBy", out int by) || AbsNow < by || state.gifts.GetValueOrDefault("mr_walkCalled") == by) return;
        int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
        if (!OnSandbarTile(tx, ty) || mode != "play") return;
        state.gifts["mr_walkCalled"] = by;
        Toast("Mang Ambo: \"Back to the beach now! It's time.\"", 5);
    }
    // When you have to be back by, for the location line while you're out on the bar.
    string AmboBackBy() => state.gifts.TryGetValue("mr_walkBy", out int by) && AbsNow < by + 180
        ? ClockText((int)((state.clock + (by - AbsNow)) % (24 * 60) + 24 * 60) % (24 * 60), 10) : null;

    static readonly (string id, string name, string words, float s)[] MlPools =
    {
        ("star", "sea star", "A sea star in a tide pool, holding on to the rock with hundreds of tiny tube feet. You watch it and leave it where it is.", 0.48f),
        ("cucumber", "sea cucumber", "A sea cucumber, sifting the sand for scraps of food and recycling the nutrients in it as it goes. You leave it be.", 0.68f),
        ("hermit", "hermit crab", "A hermit crab in a borrowed snail shell. As it grows it moves into a bigger empty one. You watch it and leave it where it is.", 0.88f)
    };

    (float x, float y) MlPoolAt(int i)
    {
        // Along the bar's middle line, a fraction of the way out.
        float total = 0;
        var lens = new float[MlBar.Length];
        for (int k = 1; k < MlBar.Length; k++) { total += Dist(MlBar[k - 1].x, MlBar[k - 1].y, MlBar[k].x, MlBar[k].y); lens[k] = total; }
        float want = MlPools[i].s * total;
        for (int k = 1; k < MlBar.Length; k++)
            if (lens[k] >= want)
            {
                float f = (want - lens[k - 1]) / (lens[k] - lens[k - 1]);
                return (MlWx(MlBar[k - 1].x + (MlBar[k].x - MlBar[k - 1].x) * f), MlWy(MlBar[k - 1].y + (MlBar[k].y - MlBar[k - 1].y) * f));
            }
        return (MlWx(MlBar[^1].x), MlWy(MlBar[^1].y));
    }

    void LookAtPool(int i)
    {
        var (px, py) = MlPoolAt(i);
        FaceToward(px, py);
        string key = "mr:pool:" + MlPools[i].id;
        bool first = !state.Hinted(key);
        NoteMlStep(key);
        if (first) Sfx.Play("pet");
        int n = MlPools.Count(p => state.Hinted("mr:pool:" + p.id));
        // The reward is kept before a word is said (Codex: quitting during the words used to lose it).
        bool paid = CheckPools();
        if (first && !paid && n < MlPools.Length) ToastLater($"Tide pools: {n} of {MlPools.Length}.");
        Talk(new() { new("You", MlPools[i].words) });
    }

    // All three seen: Mang Ambo's coins, once (also from StartGame, for a save made in the middle of the words).
    bool CheckPools()
    {
        if (state.Hinted("mr:pools") || !MlPools.All(p => state.Hinted("mr:pool:" + p.id))) return false;
        state.hinted["mr:pools"] = true;
        state.coins += 40;
        GainXp(10);
        Save();
        Sfx.Play("rare");
        ToastLater("All three tide-pool animals seen, and all left where they live. Mang Ambo gives you 40 coins.");
        return true;
    }

    // The tide pools on the bar, drawn while it's out.
    void DrawPools()
    {
        if (player.X < (MlStart - 20) * T) return;
        for (int i = 0; i < MlPools.Length; i++)
        {
            var (x, y) = MlPoolAt(i);
            if (!SandbarDryAt(x, y)) continue;
            int px = (int)x, py = (int)y;
            // A rounded pool left in a dip in the sand, darker at its rim, with the animal in it.
            for (int j = -3; j <= 3; j++)
                for (int k = -5; k <= 5; k++)
                {
                    float d = k * k / 25f + j * j / 9f + (float)(Pix.Hash(px + k, py + j, 977) - 0.5) * 0.25f;
                    if (d <= 1) pix.Rect(px + k, py + j, 1, 1, d > 0.7f ? "#5d9fb4" : "#86c6d8");
                }
            pix.Rect(px - 3, py - 2, 2, 1, "#b8e2ee");
            switch (MlPools[i].id)
            {
                case "star":
                    pix.Rect(px, py - 2, 1, 2, "#e0702a"); pix.Rect(px - 2, py - 1, 5, 1, "#e0702a"); pix.Rect(px - 1, py, 3, 1, "#e0702a");
                    pix.Rect(px - 2, py + 1, 1, 1, "#e0702a"); pix.Rect(px + 2, py + 1, 1, 1, "#e0702a"); pix.Rect(px, py - 1, 1, 1, "#f2a05a");
                    break;
                case "cucumber": pix.Rect(px - 2, py, 5, 2, "#4a3a2a"); pix.Rect(px - 1, py, 1, 1, "#6b5a44"); pix.Rect(px + 1, py + 1, 1, 1, "#6b5a44"); break;
                default: pix.Rect(px - 1, py - 1, 3, 2, "#e8dcc0"); pix.Rect(px, py - 1, 1, 1, "#c9b896"); pix.Rect(px - 2, py, 1, 1, "#c0392b"); pix.Rect(px + 2, py, 1, 1, "#c0392b"); break;
            }
        }
    }

    /* ---------- Lorna's dryer ---------- */
    int DryerLoad => state.gifts.GetValueOrDefault("mr_dryerN");
    bool DryerReady => DryerLoad > 0 && AbsNow - state.gifts.GetValueOrDefault("mr_dryerAt") >= 120;

    void DryerService()
    {
        if (DryerReady)
        {
            int n = DryerLoad;
            state.gifts.Remove("mr_dryerN"); state.gifts.Remove("mr_dryerAt");
            Give("daing", n);
            Save();
            Sfx.Play("pickup");
            Toast($"{Items.Amount("daing", n)} from Lorna's dryer, dried on the spring's heat.", 4);
            return;
        }
        if (DryerLoad > 0)
        {
            int left = Math.Max(1, 120 - (AbsNow - state.gifts.GetValueOrDefault("mr_dryerAt")));
            Toast($"The fish are drying: about {(left >= 60 ? $"{left / 60} h " : "")}{left % 60} min to go.", 3);
            return;
        }
        int have = Math.Min(3, FishCount());
        if (have == 0) { Toast("Lorna: \"Bring me raw fish, up to three at a time, and the dryer will have them done in two hours.\"", 4); return; }
        TakeFish(have);
        state.gifts["mr_dryerN"] = have;
        state.gifts["mr_dryerAt"] = AbsNow;
        Save();
        Sfx.Play("build");
        Toast($"Lorna lays {have} fish on the dryer's trays. Ready in two hours, rain or shine.", 4);
    }

    /* ---------- Sela's cache hunt ---------- */
    // Hidden a little way inland of the survey stone; you're given its bearing and distance from the stone.
    const float CacheX = DatumX + 118, CacheY = DatumY + 92;
    float CacheBearing => MathF.Round(BearingTo(DatumX, DatumY, CacheX, CacheY));
    int CacheMetres => (int)MathF.Round(Dist(DatumX, DatumY, CacheX, CacheY) * 0.15f);

    void OfferCacheHunt()
    {
        state.hinted["mr:cacheAsked"] = true;
        Save();
        Talk(new()
        {
            new("Aling Sela", "Oh, one more thing, a game I used to play with Jun. I hid a little cache once, measured from the survey stone."),
            new("Aling Sela", $"From the stone, it's on a bearing of {CacheBearing:0} degrees, about {CacheMetres} metres away. Your compass shows your bearing and distance from the stone while you're near it. Dig where they match.")
        });
    }

    // While the hunt's on and you're near the stone, the compass reads your bearing and distance from it.
    void DrawCacheCompass()
    {
        if (!state.Hinted("mr:cacheAsked") || state.Hinted("mr:cache") || scene != "world" || mode is not ("play" or "build")) return;
        float d = Dist(DatumX, DatumY, player.X, player.Y);
        if (d > 360) return;
        string label = d < 8 ? "At the survey stone" : $"From the stone: bearing {BearingTo(DatumX, DatumY, player.X, player.Y):0}, {d * 0.15f:0} m   (cache: {CacheBearing:0}, {CacheMetres} m)";
        float w = Gfx.Measure(label, FontKind.Ui700, 17) + 24, x = (Gfx.LW - w) / 2, y = 60;
        Gfx.Rect(x, y, w, 30, Pal.WithAlpha(Pal.Ink, 0.75f), 5);
        Gfx.Text(label, x + 12, y + 5, FontKind.Ui700, 17, Pal.Lantern);
    }

    void DigCache()
    {
        FaceToward(CacheX, CacheY);
        Swing("dig", 0.5f);
        state.hinted["mr:cache"] = true;
        state.coins += 120;
        Give("cut_bait", 4);
        GainXp(15);
        Save();
        Sfx.Play("rare");
        Talk(new() { new("You", "A tin box, wrapped in oilcloth: 120 coins, some bait, and a note in Aling Sela's writing. \"Bearing and distance from a known point: that's how every surveyor finds their way back.\"") });
    }

    /* ---------- Kalutang practice with Mang Iyo ---------- */
    // Each phrase: high (true) and low (false) bars, played in turn. Mang Iyo plays it, then you answer it.
    static readonly bool[][] KalPhrases =
    {
        new[] { true, false, true },
        new[] { true, true, false, false },
        new[] { true, false, false, true, false },
        new[] { false, true, true, false, true, false }
    };
    const float KalBeat = 0.6f;
    MlKalutang mlKalutang;

    void OpenKalutang()
    {
        if (!state.Hinted("mr:iyoMet"))
        {
            state.hinted["mr:iyoMet"] = true;
            Talk(new()
            {
                new("Mang Iyo", "Magandang hapon! These are kalutang: a pair of wooden bars you strike together. Bigger pairs sound lower, smaller ones higher, and a group plays a tune between them."),
                new("Mang Iyo", "Teachers and children come to learn them here. Want to try? I play a little phrase, then you answer it: <act> for the high bar, <alt> for the low one. Take your time.")
            }, () => { mlKalutang = new MlKalutang(); OpenMlPanel("mlkalutang"); });
            return;
        }
        mlKalutang = new MlKalutang();
        OpenMlPanel("mlkalutang");
    }

    void DrawMlKalutang()
    {
        var k = mlKalutang;
        if (k == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame("Kalutang with Mang Iyo", Bind.Fix("Listen to Mang Iyo's phrase, then play it back: <act> or click the high bar, <alt> or click the low bar."));
        if (MlClose(x, y)) return;
        float dt = Raylib.GetFrameTime();
        k.T += dt; k.FlashHi = Math.Max(0, k.FlashHi - dt * 3); k.FlashLo = Math.Max(0, k.FlashLo - dt * 3);
        bool done = k.Round >= KalPhrases.Length;
        var phrase = done ? KalPhrases[^1] : KalPhrases[k.Round];
        float beat = KalBeat * (k.Slow ? 1.5f : 1);
        // Mang Iyo plays: one note a beat, after a short count-in.
        if (!done && k.Listening)
        {
            int i = (int)((k.T - 0.6f) / beat);
            float frac = (k.T - 0.6f) / beat - i;
            if (k.T >= 0.6f && i < phrase.Length && frac < dt / beat + 0.001f) Strike(k, phrase[i]);
            if (k.T >= 0.6f + phrase.Length * beat + 0.3f) { k.Listening = false; k.Pos = 0; k.T = 0; }
        }
        // The two bars, big enough to click (a key pressed this frame wins, so a pad's A can't also click one).
        bool keyed = Bind.Pressed("act") || Bind.Pressed("alt");
        float bx = x + 120, by = y + 150;
        for (int b = 0; b < 2; b++)
        {
            bool hi = b == 0;
            float bw = hi ? 300 : 380, bh = 54, bxx = bx + (hi ? 40 : 0), byy = by + b * 110;
            float flash = hi ? k.FlashHi : k.FlashLo;
            Gfx.Box(bxx, byy, bw, bh, Lighten(Pal.C(hi ? "#c9955a" : "#8a5a32"), flash * 0.5f), Pal.Ink, 3, 10);
            Gfx.Text(hi ? "Small pair (high)" : "Big pair (low)", bxx + 16, byy + 14, FontKind.Ui700, 20, Color.White);
            Gfx.Text(Bind.Fix(hi ? "<act>" : "<alt>"), bxx + bw - 70, byy + 14, FontKind.Ui700, 18, Color.White);
#if DEBUG
            Gfx.Seen[hi ? "kal:hi" : "kal:lo"] = new Rectangle(bxx, byy, bw, bh);
#endif
            // A pair of bars struck together, drawn at the bar's end.
            float sx = bxx + bw - 120, sy = byy + bh / 2, open = flash > 0 ? 4 : 10;
            Gfx.Line(sx - 16, sy - open, sx + 16, sy - open - 2, 6, Pal.C(hi ? "#e8c08a" : "#6b4424"));
            Gfx.Line(sx - 16, sy + open, sx + 16, sy + open + 2, 6, Pal.C(hi ? "#e8c08a" : "#6b4424"));
            if (flash > 0) for (int s = 0; s < 3; s++) Gfx.Line(sx + 24 + s * 6, sy - 10 + s * 2, sx + 30 + s * 6, sy - 14 + s * 2, 2, Pal.WithAlpha(Pal.Ink, flash));
            if (!done && !k.Listening && !keyed && Gfx.Click(bxx, byy, bw, bh)) Answer(k, hi);
        }
        if (!done && !k.Listening && MlKeysLive)
        {
            if (Bind.Pressed("act")) Answer(k, true);
            else if (Bind.Pressed("alt")) Answer(k, false);
        }
        // The phrase as dots: what's been played back so far.
        float dx = bx, dy = by + 240;
        Gfx.Text(done ? "All four phrases!" : k.Listening ? $"Phrase {k.Round + 1} of {KalPhrases.Length}: listen..." : $"Phrase {k.Round + 1} of {KalPhrases.Length}: your turn", dx, dy, FontKind.Ui700, 20, Pal.PaperInk);
        for (int i = 0; i < phrase.Length && !done; i++)
        {
            bool played = !k.Listening && i < k.Pos;
            Gfx.Circle(dx + 14 + i * 40, dy + 54, 14, played ? Pal.C(phrase[i] ? "#c9955a" : "#8a5a32") : Pal.C("#e6dcc4"));
            if (played || k.Listening) Gfx.TextCenter(phrase[i] ? "H" : "L", dx + 14 + i * 40, dy + 44, FontKind.Ui700, 16, played ? Color.White : Muted);
        }
        // Mang Iyo beside it, and what he says.
        float rx = x + 640, rw = MlPanelW - (rx - x) - 24;
        MlNote(k.Note == "" ? (done ? "Mang Iyo: \"Ang galing! That's a tune. Come and play with us any afternoon.\"" : "Mang Iyo: \"Listen first, then answer. There's no hurry.\"") : k.Note, true, rx, y + 120, rw, 200);
        if (done && MlDone("Done", x + MlPanelW - 24 - (Gfx.Measure("Done", FontKind.Ui700, 22) + 48), y + MlPanelH - 24 - 50)) { ClosePanels(); return; }
    }

    void Strike(MlKalutang k, bool hi)
    {
        Sfx.Play(hi ? "kalhi" : "kallo");
        if (hi) k.FlashHi = 1; else k.FlashLo = 1;
    }

    void Answer(MlKalutang k, bool hi)
    {
        var phrase = KalPhrases[k.Round];
        Strike(k, hi);
        if (phrase[k.Pos] != hi)
        {
            // A miss: he plays it again, slower. Nothing already done is lost.
            k.Note = "Mang Iyo: \"Almost! Listen again, I'll play it slower.\"";
            k.Slow = true; k.Listening = true; k.T = 0; k.Pos = 0;
            return;
        }
        k.Pos++;
        if (k.Pos < phrase.Length) return;
        k.Round++; k.Slow = false; k.Listening = true; k.T = -0.4f; k.Note = "";
        if (k.Round < KalPhrases.Length) { Sfx.Play("right"); return; }
        Sfx.Play("rare");
        if (!state.Hinted("mr:kalutang"))
        {
            state.hinted["mr:kalutang"] = true;
            state.coins += 30;
            GainXp(10);
            Save();
            ToastLater("Mang Iyo gives you 30 coins for playing the whole tune. The town plays in the square in the late afternoon.");
        }
    }

    // In the late afternoon a few children practise with Mang Iyo in the square (looks only).
    bool KalutangPractice => state.clock >= 15 * 60 && state.clock < 19 * 60 && state.weather != "storm" && mlGather == null;
    void AddKalutangKids(List<(float y, Action draw)> list)
    {
        if (!KalutangPractice || scene != "world") return;
        var (px, py) = MlPlace("plaza");
        for (int i = 0; i < 3; i++)
        {
            int k = i, x = (int)px - 14 + i * 12, y = (int)py + 14;
            list.Add((y, () =>
            {
                DrawKid((a, b, c, d, col) => pix.Rect(a, b, c, d, col), x, y, k + 1, 1, time, stand: true);
                bool hit = (int)(time / KalBeat + k * 0.5f) % 2 == 0;
                pix.Rect(x - 4, y - 8 - (hit ? 1 : 0), 1, 4, "#8a5a32"); pix.Rect(x + 3, y - 8 - (hit ? 0 : 1), 1, 4, "#6b4424");
            }));
        }
    }

    /* ---------- Aling Nita's butterflies ---------- */
    static readonly (string name, bool ok, string why)[] NurseryLeaves =
    {
        ("Calamansi leaves", true, "The caterpillars munch straight in. Lime butterfly caterpillars eat the leaves of citrus plants, and calamansi is a citrus."),
        ("Lemon leaves", true, "Those work too: lemon's a citrus. The caterpillars are happy."),
        ("Mango leaves", false, "The caterpillars won't touch them. Each kind of caterpillar eats only certain plants; this one eats citrus."),
        ("Grass", false, "Not grass: the caterpillars just wander off it. They need citrus leaves.")
    };
    static readonly (string name, string words)[] NurseryStages =
    {
        ("Eggs", "Tiny round eggs, laid one at a time on young citrus leaves. In one study, in southern Iran, they hatched in about three days."),
        ("Caterpillars", "The caterpillars eat and grow, shedding their skins as they get bigger: about eighteen days in that same study. Timing varies with temperature and food."),
        ("Pupae", "Each caterpillar spins a silk pad and a silk belt round its middle to hold it to a stem, and becomes a pupa (a chrysalis). It changes inside: about twelve days in that study."),
        ("Butterflies", "Out comes the lime butterfly. Its wings dry and harden for a while before it can fly.")
    };
    MlNursery mlNursery;

    void OpenNursery()
    {
        mlNursery = new MlNursery { Step = state.Hinted("mr:nursery") ? 2 : 0 };
        if (!state.Hinted("mr:nitaMet"))
        {
            state.hinted["mr:nitaMet"] = true;
            Talk(new()
            {
                new("Aling Nita", "Welcome to my butterflies! Farms on Marinduque raise butterflies for gardens and for people to enjoy. These are lime butterflies."),
                new("Aling Nita", "My caterpillars are hungry, and I've four kinds of leaves in the basket. Can you feed them? Then I'll show you every stage.")
            }, () => OpenMlPanel("mlnursery"));
            return;
        }
        OpenMlPanel("mlnursery");
    }

    void DrawMlNursery()
    {
        var n = mlNursery;
        if (n == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame("Aling Nita's butterflies", n.Step == 0 ? "Click the leaves the caterpillars will eat." : n.Step == 1 ? "Click each tray to see each stage of a lime butterfly's life." : "A butterfly is coming out of its pupa!");
        if (MlClose(x, y)) return;
        float px = x + 24, py = y + 100;
        if (n.Step == 0)
        {
            for (int i = 0; i < NurseryLeaves.Length; i++)
            {
                float bx = px + i * 250, bw = 236, bh = 150;
                bool hit = n.Seen.Contains(i);
                Gfx.Box(bx, py, bw, bh, hit ? (NurseryLeaves[i].ok ? Pal.C("#e4f3e0") : Pal.C("#f8e6dc")) : Gfx.Hover(bx, py, bw, bh) ? Lighten(Pal.Sand, 0.15f) : Pal.Sand, Pal.Ink, 2, 8);
                int i2 = i;
                PixelScene(bx + 2, py + 2, 58, 28, 4, p => PaintFeedLeaf(p, i2), 10 + i2);
                Gfx.TextCenter(NurseryLeaves[i].name, bx + bw / 2, py + bh - 30, FontKind.Ui700, 18, Pal.Ink);
#if DEBUG
                Gfx.Seen[$"leaf:{i}"] = new Rectangle(bx, py, bw, bh);
#endif
                if (Gfx.Click(bx, py, bw, bh)) { n.Seen.Add(i); n.Note = NurseryLeaves[i].why; Sfx.Play(NurseryLeaves[i].ok ? "right" : "wrong"); }
            }
            bool fed = n.Seen.Contains(0) || n.Seen.Contains(1);
            MlNote(n.Note, n.Note != "" && NurseryLeaves.Any(l => l.why == n.Note && l.ok), px, py + 170, MlPanelW - 48 - (fed ? 200 : 0), 120);
            if (fed && MlDone("Next", x + MlPanelW - 24 - (Gfx.Measure("Next", FontKind.Ui700, 22) + 48), py + 190)) { n.Step = 1; n.Note = ""; n.Seen.Clear(); return; }
            return;
        }
        if (n.Step == 1)
        {
            for (int i = 0; i < NurseryStages.Length; i++)
            {
                float bx = px + i * 250, bw = 236, bh = 170;
                bool seen = n.Seen.Contains(i);
                Gfx.Box(bx, py, bw, bh, seen ? Pal.C("#e4f3e0") : Gfx.Hover(bx, py, bw, bh) ? Lighten(Pal.Sand, 0.15f) : Pal.Sand, Pal.Ink, 2, 8);
                int st = i;
                PixelScene(bx + 2, py + 2, 58, 33, 4, p => PaintStage(p, st), 20 + st);
                Gfx.TextCenter(NurseryStages[i].name, bx + bw / 2, py + bh - 32, FontKind.Ui700, 18, Pal.Ink);
#if DEBUG
                Gfx.Seen[$"stage:{i}"] = new Rectangle(bx, py, bw, bh);
#endif
                if (Gfx.Click(bx, py, bw, bh)) { n.Seen.Add(i); n.Note = NurseryStages[i].words; Sfx.Play("blip"); }
            }
            bool all = n.Seen.Count == NurseryStages.Length;
            MlNote(n.Note, true, px, py + 190, MlPanelW - 48 - (all ? 200 : 0), 130);
            if (all && MlDone("Next", x + MlPanelW - 24 - (Gfx.Measure("Next", FontKind.Ui700, 22) + 48), py + 210)) { n.Step = 2; n.Emerge = 0; n.Note = ""; return; }
            return;
        }
        // A butterfly coming out of its pupa, then the wings opening.
        // The pupa splits and the butterfly comes out, unfolds and dries: twelve seconds, slow enough to watch.
        n.Emerge = Math.Min(1, n.Emerge + Raylib.GetFrameTime() / 12);
        float em = n.Emerge;
        PixelScene(x + 24, py - 8, 120, 88, 4, p => PaintEmergence(p, em));
        if (n.Emerge >= 1 && !state.Hinted("mr:nursery"))
        {
            state.hinted["mr:nursery"] = true;
            state.coins += 30;
            GainXp(10);
            Save();
            Sfx.Play("rare");
            ToastLater("Aling Nita gives you 30 coins for your help with the caterpillars.");
        }
        MlNote(n.Emerge < 1 ? "Watch..." : "Its wings are drying. In a little while it will fly in the garden, and lay eggs on the calamansi, and it all starts again.", n.Emerge >= 1, x + MlPanelW / 2, py + 40, MlPanelW / 2 - 24, 160);
        if (n.Emerge >= 1 && MlDone("Done", x + MlPanelW - 24 - (Gfx.Measure("Done", FontKind.Ui700, 22) + 48), y + MlPanelH - 24 - 50)) { ClosePanels(); return; }
    }

    static void DrawLifeStage(int i, float x, float y)
    {
        switch (i)
        {
            case 0:
                Gfx.Circle(x, y, 36, Pal.C("#3f7a37"));
                for (int k = 0; k < 5; k++) Gfx.Circle(x - 20 + k * 10, y - 4 + (k % 2) * 8, 4, Pal.C("#f3e6a0"));
                break;
            case 1:
                for (int k = 0; k < 7; k++) Gfx.Circle(x - 36 + k * 12, y + MathF.Sin(k * 0.9f) * 4, 8, k % 2 == 0 ? Pal.C("#5a9a3a") : Pal.C("#e8e0c8"));
                Gfx.Circle(x + 40, y, 8, Pal.C("#3f6a2a"));
                break;
            case 2:
                // Upright against a stem, held by a silk belt.
                Gfx.Line(x + 16, y - 50, x + 16, y + 50, 5, Pal.C("#6b8a3a"));
                Gfx.Rect(x - 6, y - 32, 20, 62, Pal.C("#9ab05a"), 10);
                Gfx.Line(x - 6, y - 6, x + 16, y - 10, 2, Pal.C("#e8e2cc"));
                break;
            default: DrawButterfly(x, y, 44, 1); break;
        }
    }

    // A lime butterfly: black wings with yellow spots.
    static void DrawButterfly(float x, float y, float s, float open)
    {
        float w = s * (0.3f + 0.7f * open);
        Gfx.Triangle(x, y, x - w, y - s * 0.8f, x - w * 0.9f, y + s * 0.2f, Pal.C("#2b2420"));
        Gfx.Triangle(x, y, x + w * 0.9f, y + s * 0.2f, x + w, y - s * 0.8f, Pal.C("#2b2420"));
        Gfx.Triangle(x, y, x - w * 0.7f, y + s * 0.7f, x - w * 0.2f, y + s * 0.6f, Pal.C("#2b2420"));
        Gfx.Triangle(x, y, x + w * 0.2f, y + s * 0.6f, x + w * 0.7f, y + s * 0.7f, Pal.C("#2b2420"));
        if (open > 0.3f)
            foreach (float d in new[] { -1f, 1f })
                for (int k = 0; k < 3; k++) Gfx.Circle(x + d * w * (0.35f + k * 0.18f), y - s * (0.3f + k * 0.12f), Math.Max(2, s * 0.08f), Pal.C("#f3c25b"));
        Gfx.Rect(x - 2, y - s * 0.5f, 4, s * 0.9f, Pal.C("#1a1614"), 2);
    }
}
