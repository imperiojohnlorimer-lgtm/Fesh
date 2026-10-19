using Raylib_cs;

namespace Fesh;

// 1.38: the boatyard. The boat is no longer one click at the workbench: you build a slipway on the shore (a build piece) and
// make the banca on it in five stages, each its own hand-in and its own bit of work, with the hull growing on the slipway.
//   1 keel and ribs (6 wood, 2 iron bars)   2 planking (8 wood)   3 outriggers (4 wood)   4 mast and rigging (2 wood,
//   2 iron bars)   5 sail (1 sailcloth)   then launch. The total is the old recipe's.
// Pressing <act> at the slipway hands over the stage's materials (they're kept against the project from then on) and you
// hammer: hold <act> for 2.5 s (letting go pauses it; Esc puts the hammer down, and the work is kept). Then the stage's
// animation plays (2 s, skippable with a fresh press), its progress is saved first. Launching gives you the boat, moored at
// Pip's jetty, and the hull slides down the slipway. Taking the slipway down gives back what's been put in. The pictures
// are Blender frames of the same model as the finished boat (tools/blender/boat.py, BoatArt.Stage*), on a cradle.
sealed class Yard { public int stage; public bool paid; public float work; }

partial class Game
{
    static readonly (string name, string verb, (string id, int n)[] cost)[] YardStages =
    {
        ("keel and ribs", "lay the keel and raise the ribs", new[] { ("wood", 6), ("iron_bar", 2) }),
        ("planking", "plank the hull", new[] { ("wood", 8) }),
        ("outriggers", "lash on the outriggers", new[] { ("wood", 4) }),
        ("mast and rigging", "step the mast and rig it", new[] { ("wood", 2), ("iron_bar", 2) }),
        ("sail", "bend on the sail", new[] { ("sailcloth", 1) }),
    };
    const float YardWorkSecs = 2.5f, YardAnimSecs = 2.0f;

    Build yardB;                // the slipway being worked on (mode boatwork or boatanim)
    float yardT, yardBeat;      // the animation's clock; the time to the next hammer blow
    bool yardLaunching;
    int yardLaunchFace = 1;

    static string YardKey(Build b) => $"{b.x},{b.y}";
    Yard YardOf(Build b) => state.yards.TryGetValue(YardKey(b), out var y) ? y : null;
    Yard YardNew(Build b) { var y = new Yard(); state.yards[YardKey(b)] = y; return y; }

    static string YardCostText(int stage) => string.Join(", ", YardStages[stage].cost.Select(c => $"{c.n} {Items.ById[c.id].Name.ToLowerInvariant()}"));
    bool YardCanPay(int stage) => YardStages[stage].cost.All(c => Has(c.id) >= c.n);

    // Which way the boat slides: toward open sea along the slipway's row, east (1) or west (-1); 0 if there's none in sight.
    int YardFace(int tx, int ty)
    {
        for (int d = 1; d <= 10; d++)
        {
            if (TileAt(tx + 2 + d, ty) is '~' or 'w') return 1;
            if (TileAt(tx - d, ty) is '~' or 'w') return -1;
        }
        return 0;
    }

    // What it costs to take a slipway down: the materials already put into the boat, which go back to you.
    string YardRefund(Build b)
    {
        var y = YardOf(b);
        state.yards.Remove(YardKey(b));
        if (y == null) return "";
        int n = 0;
        for (int s = 0; s < y.stage && s < YardStages.Length; s++) foreach (var (id, c) in YardStages[s].cost) { Give(id, c); n += c; }
        if (y.paid && y.stage < YardStages.Length) foreach (var (id, c) in YardStages[y.stage].cost) { Give(id, c); n += c; }
        return n > 0 ? " The materials put into the boat came back too." : "";
    }

    Target YardTarget(Build b)
    {
        var y = YardOf(b);
        int stage = y?.stage ?? 0;
        if (Has("boat") > 0 && stage == 0 && !(y?.paid ?? false))
            return new Target { Type = "info", Ref = b, Label = "Your slipway. Your boat is already built" };
        if (stage >= YardStages.Length) return new Target { Type = "yard", Ref = b, Label = "Launch your boat" };
        var st = YardStages[stage];
        if (y != null && y.paid) return new Target { Type = "yard", Ref = b, Label = $"Keep working: {st.name} ({(int)(100 * y.work / YardWorkSecs)}%)" };
        if (YardCanPay(stage)) return new Target { Type = "yard", Ref = b, Label = $"Build the {st.name}: {YardCostText(stage)}" };
        return new Target { Type = "info", Ref = b, Label = $"Slipway, stage {stage + 1} of 5, the {st.name}: needs {YardCostText(stage)}" };
    }

    void OnYard(Build b)
    {
        var y = YardOf(b) ?? YardNew(b);
        if (Stormy) { Toast("Too stormy to work on the boat.", 3); return; }
        if (y.stage >= YardStages.Length) { LaunchYardBoat(b, y); return; }
        if (!y.paid)
        {
            if (!YardCanPay(y.stage)) { Toast($"The {YardStages[y.stage].name} needs {YardCostText(y.stage)}.", 3.5f); return; }
            foreach (var (id, n) in YardStages[y.stage].cost) Take(id, n);
            y.paid = true; y.work = 0;
            Save();
        }
        yardB = b; yardBeat = 0;
        FaceToward(b.x * T + 15, b.y * T + 8);
        mode = "boatwork";
        Sfx.Play("ui");
    }

    // The work: holding <act> hammers. Called every frame in mode boatwork or boatanim.
    void UpdateYard(float dt)
    {
        if (mode is not ("boatwork" or "boatanim")) return;
        var y = yardB == null ? null : YardOf(yardB);
        if (y == null) { mode = "play"; yardB = null; return; }
        if (mode == "boatwork")
        {
            if (Stormy) { StopYardWork("A storm! You put the hammer down; the work is kept."); return; }
            var st = YardStages[Math.Min(y.stage, YardStages.Length - 1)];
            SetPrompt($"Hold [<act>] to {st.verb}: {(int)(100 * Math.Min(1, y.work / YardWorkSecs))}%   ·   [Esc] to stop", null, true);
            if (!ReelHeld()) return;
            y.work += dt;
            yardBeat -= dt;
            if (yardBeat <= 0)
            {
                yardBeat = 0.42f;
                Swing("mallet", 0.32f);
                Sfx.Play(y.stage % 2 == 0 ? "chop" : "mine");
                float bx = yardB.x * T + 15 + MathF.Sin(time * 5) * 8, by = yardB.y * T - 6;
                Burst(bx, by, "#d6a369", 3);
                if ((int)(y.work / 0.42f) % 3 == 0) Burst(bx, by - 2, "#ffe28a", 2);
                shakeT = 0.08f;
            }
            if (y.work >= YardWorkSecs)
            {
                y.stage++; y.paid = false; y.work = 0;
                Save();   // the stage is committed before its animation, so a quit now keeps it
                mode = "boatanim"; yardT = 0; yardLaunching = false;
                Sfx.Play("coin");
                Burst(yardB.x * T + 15, yardB.y * T - 6, "#fff2c0", 14);
                quake = 0.2f;
            }
            return;
        }
        // The animation of the stage just finished (or of the launch).
        yardT += dt;
        float len = yardLaunching ? 2.6f : YardAnimSecs;
        SetPrompt(yardLaunching ? "Away she goes..." : "Press [<act>] to skip", null, true);
        if (yardT >= len) EndYardAnim();
    }

    void StopYardWork(string note)
    {
        mode = "play"; yardB = null;
        Toast(note, 3);
    }

    void SkipYardAnim() { if (mode == "boatanim" && yardT > 0.2f) EndYardAnim(); }

    void EndYardAnim()
    {
        var y = yardB == null ? null : YardOf(yardB);
        mode = "play";
        bool launched = yardLaunching;
        yardLaunching = false;
        if (launched) Toast("Your banca is launched and moored at Pip's jetty. [<act>] there sails; [<alt>] takes the helm.", 6);
        else if (y != null && y.stage >= YardStages.Length) Toast("The boat is finished! Press [<act>] at the slipway to launch it.", 5);
        else if (y != null) Toast($"Next: the {YardStages[y.stage].name}, {YardCostText(y.stage)}.", 4);
        yardB = null;
    }

    void LaunchYardBoat(Build b, Yard y)
    {
        if (Has("boat") > 0) { Toast("You already have a boat.", 3); return; }
        Give("boat");
        state.boatAt = "saltmere"; state.boatX = state.boatY = 0; state.aboard = false;
        y.stage = 0; y.paid = false; y.work = 0;
        Save();   // the boat is yours before it slides down
        yardB = b; yardT = 0; yardLaunching = true; yardLaunchFace = YardFace(b.x, b.y) == 0 ? 1 : YardFace(b.x, b.y);
        mode = "boatanim";
        Sfx.Play("splash");
    }

    /* ---------- The pictures ---------- */
    static (string[] g, int ax, int ay)[] YardFrames(int stage) => stage switch
    {
        1 => new[] { (BoatArt.Stage10, BoatArt.Stage10X, BoatArt.Stage10Y), (BoatArt.Stage11, BoatArt.Stage11X, BoatArt.Stage11Y), (BoatArt.Stage12, BoatArt.Stage12X, BoatArt.Stage12Y) },
        2 => new[] { (BoatArt.Stage20, BoatArt.Stage20X, BoatArt.Stage20Y), (BoatArt.Stage21, BoatArt.Stage21X, BoatArt.Stage21Y), (BoatArt.Stage22, BoatArt.Stage22X, BoatArt.Stage22Y) },
        3 => new[] { (BoatArt.Stage30, BoatArt.Stage30X, BoatArt.Stage30Y), (BoatArt.Stage31, BoatArt.Stage31X, BoatArt.Stage31Y) },
        4 => new[] { (BoatArt.Stage40, BoatArt.Stage40X, BoatArt.Stage40Y), (BoatArt.Stage41, BoatArt.Stage41X, BoatArt.Stage41Y) },
        5 => new[] { (BoatArt.Stage50, BoatArt.Stage50X, BoatArt.Stage50Y), (BoatArt.Stage51, BoatArt.Stage51X, BoatArt.Stage51Y) },
        _ => new[] { (BoatArt.Stage00, BoatArt.Stage00X, BoatArt.Stage00Y) },
    };

    // The ramp: two rails and sleepers laid down the beach from the cradle to the water, so the boat has somewhere to go.
    void DrawYardRamp(Build b, int gx, int gy, int face)
    {
        if (face == 0) return;
        int edge = face > 0 ? FirstWaterX(b.x + 3, b.y, 1) : FirstWaterX(b.x - 1, b.y, -1);
        int from = gx + face * 14, to = edge;
        for (int x = Math.Min(from, to); x <= Math.Max(from, to); x++)
        {
            float near = Math.Abs(x - to);
            bool sleeper = (x / 4) % 2 == 0, wet = near < 7;
            if (sleeper) pix.Rect(x, gy - 4, 1, 8, wet ? "#6a5638" : "#8c6a42");
            pix.Rect(x, gy - 4, 1, 1, wet ? "#5a4830" : "#6b4a2b");
            pix.Rect(x, gy + 3, 1, 1, wet ? "#5a4830" : "#6b4a2b");
            pix.Rect(x, gy - 3, 1, 1, wet ? "#8a7a58" : "#c49a58");
            pix.Rect(x, gy + 2, 1, 1, wet ? "#8a7a58" : "#c49a58");
        }
    }

    // The world x of the first sea tile reached going from tile tx along the row ty (dir 1 east, -1 west), at its near edge.
    int FirstWaterX(int tx, int ty, int dir)
    {
        for (int d = 0; d <= 10; d++)
        {
            int x = tx + d * dir;
            if (TileAt(x, ty) is '~' or 'w') return dir > 0 ? x * T : (x + 1) * T;
        }
        return (tx + 10 * dir) * T;
    }

    // The slipway at tile (b.x, b.y): its cradle and the boat as far as it's built. The ground point is the middle of its
    // three tiles.
    void DrawYard(Build b, int X, int Y, float t)
    {
        var y = scene == "world" ? YardOf(b) : null;
        int stage = y?.stage ?? 0;
        int face = YardFace(b.x, b.y);
        bool mirror = face < 0, mine = yardB != null && yardB.x == b.x && yardB.y == b.y;
        int gx = X + 15, gy = Y + 8;
        pix.Rect(gx - 17, gy - 1, 34, 3, Pal.Rgba(10, 20, 30, 0.22f));
        DrawYardRamp(b, gx, gy, face);
        if (mine && mode == "boatanim" && yardLaunching)
        {
            // The empty cradle, and the hull sliding off it into the water.
            var cr = YardFrames(0)[0];
            BlitBoat(cr.g, cr.ax, cr.ay, gx, gy, mirror);
            float p = Math.Clamp(yardT / 2.2f, 0, 1), e = p * p * (3 - 2 * p);
            int dir = yardLaunchFace;
            int bx = gx + (int)(dir * (2 + 34 * e)), by = gy - 6 + (int)(5 * e) + (int)(MathF.Sin(t * 6) * 0.5f);
            float fade = p > 0.8f ? 1 - (p - 0.8f) / 0.2f * 0.6f : 1;
            BlitBoat(BoatArt.RightBack, BoatArt.RightBackX, BoatArt.RightBackY, bx, by, dir < 0, fade);
            pix.Rect(bx - 6 * dir, by - 12, 1, 12, "#c69b61");
            pix.Rect(bx - 6 * dir + (dir > 0 ? 1 : -3), by - 6, 3, 2, "#e9dfbf");
            BlitBoat(BoatArt.RightFront, BoatArt.RightFrontX, BoatArt.RightFrontY, bx, by, dir < 0, fade);
            if (p > 0.35f)
                for (int i = 0; i < 6; i++)
                    pix.Rect(bx - dir * (14 + i * 2), by + 7 + (int)(MathF.Sin(t * 8 + i) * 1.2f), 1, 1, Pal.Rgba(235, 248, 252, 0.7f * (1 - i / 7f)));
            return;
        }
        var frames = YardFrames(stage);
        int k = frames.Length - 1;
        if (mine && mode == "boatanim") k = Math.Min(frames.Length - 1, (int)(yardT / YardAnimSecs * (frames.Length + 0.6f)));
        var (g, ax, ay) = frames[k];
        int bob = mine && mode == "boatwork" ? (int)(MathF.Sin(time * 22) * 0.6f) : 0;
        BlitBoat(g, ax, ay, gx, gy + bob, mirror);
        if (mine && mode == "boatwork")
        {
            // A little progress bar over the boat.
            float prog = y == null ? 0 : Math.Clamp(y.work / YardWorkSecs, 0, 1);
            pix.Rect(gx - 11, gy - 36, 22, 4, Pal.Rgba(10, 20, 30, 0.7f));
            pix.Rect(gx - 10, gy - 35, (int)(20 * prog), 2, "#f3c25b");
        }
    }
}
