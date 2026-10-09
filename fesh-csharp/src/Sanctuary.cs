using Raylib_cs;

namespace Fesh;

// The marine sanctuary south of Bakawan and Baga, on the Amihan chart, modelled on the community sanctuaries of the
// Philippines: buoys mark it, nothing is fished or trapped inside, and the fish that grow up in it spill over into the
// water around it (open-sea casts just outside bite quicker and turn up rare fish more). Sea turtles, a dugong on the
// seagrass and, by day, a whale shark live in it: you watch them (Bantay Joy keeps count), you never catch them.
sealed class SeaCreature { public string Kind; public float X, Y, ToX, ToY, Wait, Phase; public bool Flip; }

partial class Game
{
    const float SanctCX = 222, SanctCY = 69, SanctRX = 22, SanctRY = 5;     // the ellipse, in tiles
    const float JoyX = 2229, JoyY = 690;                                      // Bantay Joy, on the watch platform
    const float TakloboX = 2345, TakloboY = 712;                              // the giant clam, in the east seagrass
    readonly List<SeaCreature> seaLife = new();

    static readonly Dictionary<string, (string name, string label, string about)> SeaKinds = new()
    {
        ["pawikan"] = ("Pawikan (green sea turtle)", "Watch the sea turtle",
            "A green sea turtle surfaces for a breath and dips back down to the seagrass. Every sea turtle in the Philippines is protected by law."),
        ["dugong"] = ("Dugong", "Watch the dugong",
            "A dugong grazes the seagrass like a slow grey cow, leaving bare trails behind it. There are very few left, and they're protected."),
        ["butanding"] = ("Butanding (whale shark)", "Watch the whale shark",
            "A whale shark glides past, longer than your boat, its back speckled with white. It only eats plankton. Catching one has been banned in the Philippines since 1998."),
        ["walowalo"] = ("Walo-walo (banded sea krait)", "Watch the sea krait",
            "A banded sea krait ripples past, striped black and silver-blue, and lifts its head for a breath. It's venomous but shy, and hunts eels in the reef. Leave it be."),
        // 1.15: it never moves. Giant clams are protected in the Philippines, and sanctuaries are where they're grown back.
        ["taklobo"] = ("Taklobo (giant clam)", "Watch the giant clam",
            "A giant clam as wide as a basket sits in the seagrass, its mantle shimmering blue and green between the wavy shell. It's protected; sanctuaries like this one help them grow back.")
    };

    bool InSanctuary(float x, float y)
    {
        float dx = (x / T - SanctCX) / SanctRX, dy = (y / T - SanctCY) / SanctRY;
        return dx * dx + dy * dy < 1;
    }

    // Just outside the buoys, where the sanctuary's fish spill over (the ellipse grown by 16 tiles each way).
    bool InSpillover(float x, float y)
    {
        if (InSanctuary(x, y)) return false;
        float dx = (x / T - SanctCX) / (SanctRX + 16), dy = (y / T - SanctCY) / (SanctRY + 16);
        return dx * dx + dy * dy < 1;
    }

    // Two patches of seagrass, and the bamboo watch platform in the middle.
    void GenerateSanctuary()
    {
        for (int y = 63; y < ROWS - 1; y++)
            for (int x = 198; x < 247; x++)
            {
                if (map[y, x] != '~') continue;
                if (InBlob(x, y, 210.5, 70.5, 4.2, 1.7, 31) || InBlob(x, y, 234.5, 71, 4.6, 1.8, 37)) map[y, x] = 'w';
            }
        for (int y = 67; y <= 69; y++)
            for (int x = 219; x <= 223; x++) map[y, x] = 'd';
    }

    static readonly Box WatchHut = new(2192, 672, 20, 10);

    /* ---------- The animals ---------- */
    void UpdateSeaLife(float dt)
    {
        bool near = scene == "world" && Dist(player.X, player.Y, SanctCX * T, SanctCY * T) < 520;
        if (!near) { seaLife.Clear(); return; }
        if (seaLife.Count == 0)
            foreach (var (kind, x, y) in new[] { ("pawikan", 2080f, 690f), ("pawikan", 2330f, 705f), ("dugong", 2105f, 708f), ("butanding", 2270f, 670f), ("walowalo", 2180f, 700f), ("taklobo", TakloboX, TakloboY) })
                seaLife.Add(new SeaCreature { Kind = kind, X = x, Y = y, ToX = x, ToY = y, Wait = FxRand(0, 3), Phase = FxRand(0, 6) });
        foreach (var c in seaLife)
        {
            if (c.Kind == "taklobo") continue;   // it stays put
            if (c.Wait > 0) { c.Wait -= dt; continue; }
            float dx = c.ToX - c.X, dy = c.ToY - c.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < 1.5f)
            {
                c.Wait = FxRand(1, 4);
                // The dugong keeps to the seagrass; the others roam the whole sanctuary.
                for (int tries = 0; tries < 12; tries++)
                {
                    float a = FxRand(0, MathF.Tau), r = FxRand(10, c.Kind == "butanding" ? 90 : 50);
                    float tx = c.X + MathF.Cos(a) * r, ty = c.Y + MathF.Sin(a) * r * 0.5f;
                    if (!InSanctuary(tx, ty) || !SeaLifeWater(c.Kind, tx, ty)) continue;
                    c.ToX = tx; c.ToY = ty;
                    break;
                }
                continue;
            }
            float sp = c.Kind switch { "pawikan" => 9, "dugong" => 6, "walowalo" => 12, _ => 8 } * dt;
            float nx = c.X + dx / d * Math.Min(sp, d), ny = c.Y + dy / d * Math.Min(sp, d);
            if (SeaLifeWater(c.Kind, nx, ny)) { c.X = nx; c.Y = ny; }
            else c.ToX = c.ToY = 0;
            if (MathF.Abs(dx) > 0.5f) c.Flip = dx < 0;
            if (c.ToX == 0) { c.ToX = c.X; c.ToY = c.Y; }
        }
    }

    // Water deep enough for it (the whale shark keeps off the seagrass; the dugong stays on it).
    bool SeaLifeWater(string kind, float x, float y)
    {
        char t = TileAt((int)MathF.Floor(x / T), (int)MathF.Floor(y / T));
        return kind switch { "dugong" => t == 'w', "butanding" => t == '~' && TileAt((int)MathF.Floor((x + 16) / T), (int)MathF.Floor(y / T)) == '~' && TileAt((int)MathF.Floor((x - 16) / T), (int)MathF.Floor(y / T)) == '~', _ => t is '~' or 'w' };
    }

    // The whale shark is only up near the surface by day.
    IEnumerable<SeaCreature> VisibleSeaLife() => seaLife.Where(c => c.Kind != "butanding" || !Night);

    SeaCreature SeaLifeInReach() => VisibleSeaLife().Where(c => Dist(c.X, c.Y, player.X, player.Y) < (c.Kind == "butanding" ? 34 : 24))
        .OrderBy(c => Dist(c.X, c.Y, player.X, player.Y)).FirstOrDefault();

    Target SanctuaryTarget()
    {
        if (scene != "world") return null;
        if (SeaLifeInReach() is SeaCreature c) return new Target { Type = "watch", Ref = c, Label = SeaKinds[c.Kind].label };
        if (!Aboard && Dist(player.X, player.Y, JoyX, JoyY + 6) < 15) return new Target { Type = "joy", Label = "Talk to Bantay Joy" };
        return null;
    }

    void Watch(SeaCreature c)
    {
        FaceToward(c.X, c.Y);
        c.Wait = Math.Max(c.Wait, 2);
        Sfx.Play("pet");
        RecordSighting(c.Kind, c.X, c.Y - 12, SeaKinds[c.Kind].about);
    }

    void TalkJoy()
    {
        Say J(string t) => new("Bantay Joy", t);
        FaceToward(JoyX, JoyY);
        bool all = SeaKinds.Keys.All(k => state.sightings.GetValueOrDefault(k) > 0);
        if (!state.Hinted("metJoy"))
        {
            state.hinted["metJoy"] = true;
            Talk(new()
            {
                J("Welcome to the sanctuary! I'm Joy, the bantay dagat: the sea warden. The buoys mark where it starts."),
                J("Inside the buoys, nobody fishes and nobody sets traps. Sea turtles, a dugong, a butanding, a sea krait and a giant clam live here, and they're all protected."),
                J($"Watch them all you like, from a respectful distance. If you see all {CountWord(SeaKinds.Count)}, come and tell me."),
                J("Here's the secret: fish grow up safe in here, then swim out. Cast just outside the buoys and you'll see.")
            });
            return;
        }
        if (all && !state.Hinted("sanctuaryReward"))
        {
            state.hinted["sanctuaryReward"] = true;
            state.coins += 150; Give("glow_bait", 3);
            Sfx.Play("coin");
            Talk(new()
            {
                J($"You've seen the turtles, the dugong, the butanding, the walo-walo and the taklobo! Most visitors never see all {CountWord(SeaKinds.Count)}."),
                J("Salamat for keeping your distance. Here, from the village fund: 150 coins and some glow bait for fishing outside the buoys.")
            });
            return;
        }
        int seen = SeaKinds.Keys.Count(k => state.sightings.GetValueOrDefault(k) > 0);
        Talk(new() { J(all ? "The sanctuary's quiet today. The turtles are fat and happy." : $"You've seen {seen} of the {CountWord(SeaKinds.Count)} so far. The butanding comes up by day; the dugong and the taklobo stay on the seagrass.") });
    }

    /* ---------- Drawing ---------- */
    void AddSanctuaryObjects(List<(float y, Action draw)> list, bool live)
    {
        if (live && Dist(player.X, player.Y, SanctCX * T, SanctCY * T) > 420) return;
        list.Add((682, DrawWatchHut));
        if (live) list.Add((JoyY, DrawJoy));
    }

    // A bamboo watch hut on stilts at the middle of the platform, with a flag.
    void DrawWatchHut()
    {
        int x = 2202, y = 682;
        pix.Rect(x - 10, y - 1, 21, 2, "rgba(0,0,0,0.2)");
        pix.Rect(x - 9, y - 12, 18, 11, "#c9a06a");
        for (int i = -8; i < 9; i += 3) pix.Rect(x + i, y - 11, 1, 10, "#e0c088");
        pix.Rect(x - 3, y - 8, 6, 7, "#4a3a2a");
        for (int i = 0; i < 8; i++) pix.Rect(x - 6 - i, y - 20 + i, 12 + i * 2, 1, i % 3 == 0 ? "#967342" : "#ceb36b");
        pix.Line(x + 10, y - 2, x + 10, y - 24, "#6b4a2b");
        bool flap = (int)(time * 4) % 2 == 0;
        pix.Rect(x + 11, y - 24, 6, 2, "#2f7fa3"); pix.Rect(x + 11, y - 22, flap ? 5 : 6, 2, "#f2efe6");
    }

    void DrawJoy()
    {
        int x = (int)JoyX, y = (int)JoyY;
        string face = Dist(player.X, player.Y, JoyX, JoyY) < 40 ? (player.X < x - 10 ? "left" : player.X > x + 10 ? "right" : "down") : "down";
        LookData.DrawFigure(pix, "#a9714b", "#2b1d14", "#2f9a8a", "#2b3a4a", true, 3, "#2f7fa3", x, y, face, 0,
            bob: (int)(time % 2.8f / 1.8f), blink: (time + 0.9f) % 3.6f < 0.12f);
    }

    // The yellow buoys that mark the sanctuary, bobbing on the swell, and the seagrass swaying in its shallows.
    void DrawBuoys(float t)
    {
        if (Dist(player.X, player.Y, SanctCX * T, SanctCY * T) > 420) return;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        for (int y = Math.Max(vy0, 63); y <= vy1; y++)
            for (int x = Math.Max(vx0, 198); x <= Math.Min(vx1, 247); x++)
            {
                if (map[y, x] != 'w') continue;
                for (int k = 0; k < 3; k++)
                {
                    int gx = x * T + 1 + (int)(Pix.Hash(x, y, 330 + k) * 8), gy = y * T + 3 + (int)(Pix.Hash(x, y, 340 + k) * 6);
                    int s = (int)MathF.Round(MathF.Sin(t * 1.4f + gx * 0.3f) * 0.8f);
                    pix.Rect(gx, gy, 1, 3, "#3f8a5a"); pix.Rect(gx + s, gy - 1, 1, 1, "#5fae6a");
                }
            }
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * MathF.Tau;
            float bx = (SanctCX + MathF.Cos(a) * SanctRX) * T, by = (SanctCY + MathF.Sin(a) * SanctRY) * T;
            if (bx < camX - 8 || bx > camX + W + 8 || by < camY - 8 || by > camY + H + 8) continue;
            int bob = (int)MathF.Round(MathF.Sin(t * 2 + i) * 0.8f);
            pix.Ring(bx, by + 2, 3 + MathF.Sin(t * 2 + i) * 0.4f, Pal.Rgba(230, 246, 250, 0.4f));
            pix.Rect(bx - 2, by - 2 + bob, 4, 3, "#f3c25b"); pix.Rect(bx - 1, by - 4 + bob, 2, 2, "#e04b3a"); pix.Rect(bx - 2, by - 2 + bob, 1, 1, "#ffe8a0");
        }
    }

    // The buoys on the chart's picture, so you can see where the sanctuary starts.
    void DrawBuoysOnMap()
    {
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * MathF.Tau;
            float bx = (SanctCX + MathF.Cos(a) * SanctRX) * T, by = (SanctCY + MathF.Sin(a) * SanctRY) * T;
            pix.Rect(bx - 2, by - 2, 4, 4, "#f3c25b"); pix.Rect(bx - 1, by - 4, 2, 2, "#e04b3a");
        }
    }

    // Beneath the surface, a darker shape; now and then a head (or a fin) breaks it with a ring of ripples.
    void DrawSeaLife(float t)
    {
        foreach (var c in VisibleSeaLife())
        {
            if (c.X < camX - 40 || c.X > camX + W + 40 || c.Y < camY - 20 || c.Y > camY + H + 20) continue;
            int x = (int)MathF.Round(c.X), y = (int)MathF.Round(c.Y), d = c.Flip ? -1 : 1;
            float up = (t * 0.35f + c.Phase) % 3;
            bool surfacing = up < 0.6f;
            void R(int ox, int oy, int w, int h, Color col) => pix.Rect(d > 0 ? x + ox : x - ox - w + 1, y + oy, w, h, col);
            switch (c.Kind)
            {
                case "pawikan":
                {
                    var shell = Pal.Rgba(52, 70, 40, 0.75f);
                    R(-4, -2, 8, 5, shell); R(-3, -3, 6, 7, shell);
                    float flip = MathF.Sin(t * 3 + c.Phase);
                    R(-2, flip > 0 ? -5 : -4, 2, 2, Pal.Rgba(70, 92, 56, 0.7f)); R(-2, flip > 0 ? 4 : 3, 2, 2, Pal.Rgba(70, 92, 56, 0.7f));
                    if (surfacing) { R(4, -1, 3, 2, Pal.C("#7a8a4a")); R(6, -1, 1, 1, Pal.C("#1b1b1b")); pix.Ring(x + 5 * d, y, 2 + up * 5, Pal.Rgba(235, 248, 252, 0.6f * (1 - up / 0.6f))); }
                    break;
                }
                case "dugong":
                {
                    var body = Pal.Rgba(110, 112, 104, 0.7f);
                    R(-8, -2, 14, 4, body); R(-6, -3, 10, 6, body);
                    R(-11, -1, 3, 2, body); R(-12, -2, 1, 4, body);
                    if (surfacing) { R(6, -2, 3, 3, Pal.C("#8a8a80")); R(8, -1, 1, 1, Pal.C("#2a2a2a")); pix.Ring(x + 7 * d, y, 2 + up * 6, Pal.Rgba(235, 248, 252, 0.6f * (1 - up / 0.6f))); }
                    break;
                }
                case "walowalo":
                {
                    // A banded sea krait: a thin body rippling side to side in black and silver-blue bands, its head up to breathe.
                    for (int k = 0; k < 14; k++)
                    {
                        int wy = (int)MathF.Round(MathF.Sin(t * 6 + c.Phase - k * 0.7f) * 1.2f);
                        var band = k / 2 % 2 == 0 ? Pal.Rgba(30, 34, 44, 0.85f) : Pal.Rgba(170, 196, 214, 0.85f);
                        R(5 - k, wy, 1, 1, band);
                    }
                    if (surfacing) { R(6, -1, 2, 2, Pal.C("#1e222c")); pix.Ring(x + 6 * d, y, 2 + up * 4, Pal.Rgba(235, 248, 252, 0.6f * (1 - up / 0.6f))); }
                    break;
                }
                case "taklobo":
                {
                    // A giant clam: a wavy grey shell open at the top, its mantle shimmering between the lips.
                    var shell = Pal.Rgba(196, 196, 178, 0.8f);
                    pix.Rect(x - 5, y - 1, 11, 4, shell); pix.Rect(x - 4, y - 2, 9, 6, shell);
                    for (int k = -4; k <= 4; k += 2) pix.Rect(x + k, y + 2 + (k / 2 % 2 == 0 ? 0 : 1), 1, 2, Pal.Rgba(150, 150, 136, 0.8f));
                    float sh = (MathF.Sin(t * 1.7f + c.Phase) + 1) / 2;
                    pix.Rect(x - 4, y - 1, 9, 2, Pal.Rgba((byte)(40 + 40 * sh), (byte)(150 + 50 * sh), (byte)(190 - 40 * sh), 0.9f));
                    pix.Rect(x - 2, y - 1, 1, 1, Pal.Rgba(220, 250, 255, 0.8f)); pix.Rect(x + 2, y, 1, 1, Pal.Rgba(220, 250, 255, 0.6f));
                    break;
                }
                default:
                {
                    // A whale shark: long, broad-headed, dark with white spots, and its tall fin cutting the surface.
                    var body = Pal.Rgba(40, 62, 84, 0.72f);
                    R(-18, -3, 34, 6, body); R(-14, -4, 26, 8, body); R(14, -3, 4, 6, body);
                    R(-22, -4, 4, 2, body); R(-22, 2, 4, 2, body);
                    for (int k = 0; k < 9; k++) R(-14 + k * 3, -2 + (k % 3) * 2, 1, 1, Pal.Rgba(220, 235, 245, 0.7f));
                    int fin = surfacing ? 0 : 1;
                    R(-2, -7 + fin, 2, 3, Pal.C("#4a6a88")); R(-1, -9 + fin, 1, 2, Pal.C("#4a6a88"));
                    pix.Rect(x - 4, y - 4, 7, 1, Pal.Rgba(235, 248, 252, 0.5f));
                    break;
                }
            }
        }
    }
}
