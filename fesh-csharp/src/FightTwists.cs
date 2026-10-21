using Raylib_cs;

namespace Fesh;

// Fight twists (1.44): every ordinary fish fights in its style (dart, runner, jumper, bottom), and many also have one
// short move of their own, picked by their real family (the album's scientific family), so a grouper dives for its
// hole, a jack bursts for the surface and a snapper shakes its head. A badge by the bar warns first; then for a second
// or two the fish steers differently (or the zone shrinks, or an escape costs more). Keep it inside the zone through
// most of it and you gain a little ("Held it!"). Legends, Tidemane, odd and exotic catches, chests and matches (seeded
// and fair) have none.
sealed record Twist(string Id, string Name, string Cue, float Len, string Hint);

partial class Game
{
    static readonly Dictionary<string, Twist> Twists = new()
    {
        ["dive"] = new("dive", "Dive", "DIVE!", 1.2f, "It dives for its hole! Let the bar drop with it, then lift again."),
        ["burst"] = new("burst", "Burst", "BURST!", 1.0f, "It bursts for the surface! Hold to keep the bar with it."),
        ["shake"] = new("shake", "Head-shake", "SHAKE!", 1.4f, "It shakes its head! Hold the bar steady over it."),
        ["teeth"] = new("teeth", "Teeth", "TEETH!", 1.6f, "Its teeth are on the line! Keep it in the bar: slipping out costs double."),
        ["glide"] = new("glide", "Glide", "GLIDE!", 2.0f, "It glides away wide! Follow it slowly across the bar."),
        ["roll"] = new("roll", "Roll", "ROLL!", 1.4f, "It rolls and twists the line! The bar shrinks for a moment."),
        ["feint"] = new("feint", "Feint", "FEINT!", 1.3f, "It feints up, then darts down! Don't chase the first move."),
    };

    // Scientific families (from AlbumData's family names) and the twist that suits the real animal.
    static readonly (string family, string twist)[] FamilyTwists =
    {
        ("Epinephelidae", "dive"), ("Siluriformes", "dive"), ("Siluridae", "dive"), ("Ariidae", "dive"), ("Clariidae", "dive"),
        ("Ginglymostomatidae", "dive"), ("Paralichthyidae", "dive"), ("Synodontidae", "dive"),
        ("Carangidae", "burst"), ("Scombridae", "burst"), ("Coryphaenidae", "burst"), ("Istiophoridae", "burst"), ("Xiphiidae", "burst"),
        ("Carcharhinidae", "burst"), ("Chanidae", "burst"), ("Exocoetidae", "burst"),
        ("Lutjanidae", "shake"), ("Nemipteridae", "shake"), ("Moronidae", "shake"), ("Gadidae", "shake"), ("Megalopidae", "shake"),
        ("Sillaginidae", "shake"), ("Leiognathidae", "shake"), ("Scatophagidae", "shake"),
        ("Sphyraenidae", "teeth"), ("Serrasalmidae", "teeth"), ("Esocidae", "teeth"), ("Channidae", "teeth"), ("Balistidae", "teeth"),
        ("Dasyatidae", "glide"), ("Molidae", "glide"), ("Menidae", "glide"),
        ("Anguillidae", "roll"), ("Electrophoridae", "roll"),
        ("Cyprinidae", "feint"), ("Cichlidae", "feint"), ("Salmonidae", "feint"), ("Percidae", "feint"), ("Mugilidae", "feint"),
        ("Siganidae", "feint"), ("Acanthuridae", "feint"), ("Scaridae", "feint"), ("Labridae", "feint"), ("Pomacanthidae", "feint"),
        ("Chaetodontidae", "feint"),
    };

    // The twist a fish has, if any: by the first family named in its album entry. Easy everyday fish (difficulty under 1)
    // are spared, so the bar can be learnt on them; a bottom-hugger only ever dives (a long chase up would make it dig in).
    static string TwistOf(CommonFish f)
    {
        if (f == null || f.Legend || f.Difficulty < 1 || !AlbumData.TryGetValue(f.Id, out var info)) return null;
        foreach (var (family, twist) in FamilyTwists)
            if (info.Family.Contains(family)) return f.Style == "bottom" && twist is "burst" or "feint" or "glide" or "shake" ? "dive" : twist;
        return null;
    }

    // While a twist is warned or on, a runner doesn't start a run and a jumper doesn't leap.
    static bool TwistBusy(ReelState r) => r.TwistWarnT > 0 || r.TwistOn > 0;

    // A twist never moves the fish faster than the zone can follow (it rises at up to 130 px/s), nor more than this far.
    const float TwistMaxSpeed = 110, TwistReach = 50;

    const float TwistWarn = 0.75f;

    // Off in the old test scripts (AutoTestStart), whose fights are reeled in by holding a key; TwistScript turns them on.
    internal static bool TwistsOff;

    // At the hook: which twist, and when the first comes. (No twist draws nothing from the game's rng.)
    void StartTwist(ReelState r, CommonFish f)
    {
        r.Twist = TwistsOff || MatchActive || r.Roll.Boss || r.Roll.Exotic || r.Roll.Odd ? null : TwistOf(f);
        if (r.Twist != null) r.TwistT = Rand(2.5f, 4f);
    }

    // Every frame of an ordinary fight, after its style has picked where the fish is heading: a twist may take over the
    // fish's target and speed (or the zone, or the loss) for a moment. Returns the prompt to show instead, or null.
    string StepTwist(ReelState r, float dt, ref float fs, bool leaping)
    {
        r.LossMul = 1;
        r.TwistLive = false;
        if (r.Twist == null || !Twists.TryGetValue(r.Twist, out var tw)) return null;
        // Never on top of a run or a leap: it waits until the fish is back to its usual ways.
        if (r.TwistOn <= 0 && r.TwistWarnT <= 0 && (leaping || r.Running > 0)) return null;
        if (r.TwistOn <= 0 && r.TwistWarnT <= 0)
        {
            r.TwistT -= dt;
            if (r.TwistT > 0) return null;
            r.TwistWarnT = TwistWarn;
            Sfx.Play("warn");
            // The first time, a word on what to do (not over another message: then next time).
            if (!state.Hinted("twist:" + tw.Id) && toastTimer <= 0.5f) { state.hinted["twist:" + tw.Id] = true; Toast(tw.Hint, 4.5f); }
        }
        if (r.TwistWarnT > 0)
        {
            r.TwistWarnT -= dt;
            if (r.TwistWarnT <= 0)
            {
                r.TwistOn = tw.Len; r.TwistIn = r.TwistAll = 0;
                r.TwistC = r.FishY; r.TwistZone = r.ZoneH;
                // A roll shrinks the zone about its middle (and EndTwist puts it back the same way).
                if (tw.Id == "roll") ResizeZone(r, r.TwistZone * 0.7f);
                Sfx.Play("splash");
            }
            return $"{tw.Cue} Get ready...";
        }
        // The twist itself.
        float t = tw.Len - r.TwistOn;   // seconds into it
        r.TwistOn -= dt;
        r.TwistLive = true;   // this frame counts (NoteTwist), and if it was the last, ends it after the fish has moved
        float up = MathF.Max(4, r.TwistC - TwistReach), down = MathF.Min(Bar - 4, r.TwistC + TwistReach);
        switch (tw.Id)
        {
            case "dive": r.FishTarget = down; fs = MathF.Min(fs * 2.2f, TwistMaxSpeed); break;
            case "burst": r.FishTarget = up; fs = MathF.Min(fs * 2.6f, TwistMaxSpeed); break;
            // Small and quick: the zone covers it if you hold it over the middle (you aren't meant to chase every shake).
            case "shake": r.FishTarget = Math.Clamp(Math.Clamp(r.TwistC, 18, Bar - 18) + 12 * MathF.Sin(t * MathF.Tau * 3), 4, Bar - 4); fs = TwistMaxSpeed; break;
            case "teeth": r.LossMul = 2; break;
            // A slow, smooth drift across the bar (towards its far half), easing in and out.
            case "glide":
            {
                float k = Math.Clamp(t / tw.Len, 0, 1), ease = k * k * (3 - 2 * k);
                r.FishTarget = Math.Clamp(r.TwistC + (r.TwistC < Bar / 2 ? 1 : -1) * TwistReach * ease, 4, Bar - 4);
                fs = 60;
                break;
            }
            case "roll": break;
            // A short fake upward, then the real dash down.
            // (short enough that it settles, and you can sit on it, before the twist is over)
            case "feint": r.FishTarget = t < 0.4f ? MathF.Max(4, r.TwistC - 20) : MathF.Min(Bar - 4, r.TwistC + 35); fs = MathF.Min(fs * 2.2f, t < 0.4f ? 80 : TwistMaxSpeed); break;
        }
        return tw.Id switch
        {
            "dive" => "DIVE! Let the bar drop with it!",
            "burst" => "BURST! Hold to stay with it!",
            "shake" => "SHAKE! Hold the bar steady over it!",
            "teeth" => "TEETH! Keep it inside the bar!",
            "glide" => "GLIDE! Follow it slowly!",
            "roll" => "ROLL! The bar is smaller!",
            _ => "FEINT! It turns back down!"
        };
    }

    // Counts how much of the twist the fish spent inside the zone (after the fish has moved this frame), from a quarter of
    // a second in: where the fish happened to be when it began isn't held against you.
    const float TwistGrace = 0.25f;
    void NoteTwist(ReelState r, float dt)
    {
        if (!r.TwistLive || r.Twist == null) return;
        var tw = Twists[r.Twist];
        if (tw.Len - MathF.Max(0, r.TwistOn) >= TwistGrace)
        {
            r.TwistAll += dt;
            if (r.Inside) r.TwistIn += dt;
        }
        // Its last frame: scored now, after this frame's move (and a roll's zone put back only now).
        if (r.TwistOn <= 0) EndTwist(r, tw);
    }

    void EndTwist(ReelState r, Twist tw)
    {
        if (tw.Id == "roll") ResizeZone(r, r.TwistZone);
        r.TwistOn = 0;
        r.TwistT = Rand(4.5f, 7f);
        if (r.TwistAll > 0 && r.TwistIn / r.TwistAll >= 0.6f)
        {
            r.Progress += 0.04f * r.Pull;
            r.TwistsHeld++;
            Floater("Held it!", player.X, player.Y - 30, "#7fd36b");
            Sfx.Play("coin");
        }
    }

    // Changes the zone's height keeping its middle where it is, inside the bar.
    static void ResizeZone(ReelState r, float h)
    {
        float mid = r.ZoneY + r.ZoneH / 2;
        r.ZoneH = h;
        r.ZoneY = Math.Clamp(mid - h / 2, 0, Bar - h);
    }

    // A badge by the bar: the warning (then the twist) and an arrow for the way it goes. Steady with Reduce motion.
    void DrawTwistCue(ReelState r, float rx, Func<float, float> Y)
    {
        if (r.Twist == null || !Twists.TryGetValue(r.Twist, out var tw) || r.TwistWarnT <= 0 && r.TwistOn <= 0) return;
        bool warn = r.TwistWarnT > 0;
        float blink = Calm || !warn ? 1 : (int)(time * 8) % 2 == 0 ? 1 : 0.6f;
        var col = warn ? Pal.C("#ffd76a") : Pal.C("#ff9a5a");
        string s = tw.Cue;
        float fs = 30, w = Gfx.Measure(s, FontKind.Ui700, fs);
        Outlined(s, rx - w, Y(196), FontKind.Ui700, fs, Pal.WithAlpha(col, blink));
        string sub = warn ? "Get ready" : tw.Name;
        Outlined(sub, rx - Gfx.Measure(sub, FontKind.Ui600, 16), Y(232), FontKind.Ui600, 16, Pal.C("#ffffff"));
        // Which way it goes: an arrow left of the word (up, down or both).
        int dir = tw.Id switch { "dive" => 2, "burst" => 0, "feint" => warn ? 0 : (tw.Len - r.TwistOn < 0.4f ? 0 : 2), _ => -1 };
        float ax = rx - w - 26, ay = Y(212);
        if (dir >= 0) DrawSmallArrow(ax, ay, dir, Pal.WithAlpha(col, blink));
        else if (tw.Id is "shake" or "glide") { DrawSmallArrow(ax, ay - 9, 0, Pal.WithAlpha(col, blink)); DrawSmallArrow(ax, ay + 9, 2, Pal.WithAlpha(col, blink)); }
    }

    static void DrawSmallArrow(float cx, float cy, int dir, Color c)
    {
        float s = dir == 0 ? -1 : 1;
        Gfx.Triangle(cx, cy + 9 * s, cx - 8, cy - 2 * s, cx + 8, cy - 2 * s, Pal.C("#10243a"));
        Gfx.Triangle(cx, cy + 7 * s, cx - 6, cy - 1 * s, cx + 6, cy - 1 * s, c);
    }
}
