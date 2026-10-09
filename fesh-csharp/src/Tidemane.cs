using Raylib_cs;

namespace Fesh;

// Tidemane out of the water. Phase is "emerge" (leaping out of the Starwell), "stalk" (circling you), "rear" (about to
// charge), "charge", "winded" (worn out and open to big blows), "stompRear" and "stomp" (a shockwave), "dive", "under"
// and "surface" (back into the pool and out again with water bolts), and "calm" (beaten: it lies down and lets you near).
sealed class Boss
{
    public string Phase = "emerge";
    public float X, Y, T, Spirit, Hurt, Wait, Circle = 1, AimX, AimY, Ran, Ring = -1, Lift;
    public float FromX, FromY, ToX, ToY;   // a leap from one point to another
    public int Dir = 1, Attacks;
    public bool Hit;                         // already hurt you this charge or stomp
}
sealed class Bolt { public float X, Y, Vx, Vy, Life; }

// The Starwell, its secret, the fight with Tidemane, and riding it afterwards.
// The fight starts as a reel fight (Fishing.cs) that switches style every few seconds; landing it brings Tidemane
// out onto the sand, where you wear it down. Riding is outdoors only: it gallops on land and swims any water.
partial class Game
{
    Boss boss;
    readonly List<Bolt> bolts = new();
    float quake;     // the screen shakes while this is above zero
    int mountDir = 1;
    const float BossSpirit = 90, ChargeSpeed = 150, ChargeRange = 120, StompMin = 14, StompMax = 70;
    const int ChargeHurt = 16, StompHurt = 12, BoltHurt = 9;

    bool Riding => state.riding && state.tamed && scene == "world";
    bool BossFighting => boss != null && boss.Phase != "calm";
    bool BossOnLine => reel?.Roll?.Boss == true;

    // Tidemane only rises at night, for a coconut, and only until you've won it over.
    bool TidemaneBites(string bait) => !state.tamed && Night && bait == "coconut";

    bool StarwellFound => state.Hinted("starwell");
    bool SpotKnown(Spot s) => s.Id != "starwell" || StarwellFound;

    /* ---------- The secret ---------- */
    // Stepping into the glade for the first time.
    void CheckStarwell()
    {
        if (StarwellFound || scene != "world" || Dist(player.X, player.Y, StarwellX, StarwellY) > GladeR * T - 6) return;
        state.hinted["starwell"] = true;
        Sfx.Play("odd");
        Toast("A hidden pool ringed by palms: the Starwell. The hoofprints run right up to the water, and stop.", 5);
        Save();
    }

    void ReadCarving()
    {
        FaceToward(CarvingX, CarvingY);
        state.hinted["carving"] = true;
        Talk(new()
        {
            new("You", "Pictures are cut deep into the coral. A horse with a fish's tail, rising out of this pool under the moon."),
            new("You", "In the next one it bends its head to a hand holding out half a coconut. In the last, someone rides it across the waves."),
            new("You", $"One word is carved underneath: {Data.MountName.ToUpperInvariant()}. Below that, scratched in much later: \"It won't come quietly.\"")
        });
    }

    /* ---------- From the line to the sand ---------- */
    // Called when the reel fight is won: it leaps out of the pool and lands on the far side from you.
    void BossBreach()
    {
        fish = null; reel = null; pointerHold = false;
        mode = "play";
        float away = MathF.Atan2(StarwellY - player.Y, StarwellX - player.X);
        var (lx, ly) = ShorePoint(away, 22);
        boss = new Boss { X = StarwellX, Y = StarwellY, Spirit = BossSpirit, FromX = StarwellX, FromY = StarwellY, ToX = lx, ToY = ly, Dir = lx < player.X ? 1 : -1 };
        bolts.Clear();
        Burst(StarwellX, StarwellY, "#cfe8ee", 18);
        Burst(StarwellX, StarwellY - 4, "#ffffff", 10);
        Sfx.Play("splash");
        Sfx.Play("neigh");
        quake = 0.5f;
        Toast($"It won't be landed! {Data.MountName} bursts out of the Starwell. Dodge its charges and strike while it's winded.", 5.5f);
    }

    // Somewhere on the sand around the pool, about r pixels from its rim, as close to angle a as there's room for.
    (float x, float y) ShorePoint(float a, float r)
    {
        for (int i = 0; i < 24; i++)
        {
            float ang = a + (i % 2 == 0 ? 1 : -1) * (i / 2) * 0.27f;
            float x = StarwellX + MathF.Cos(ang) * (WellRim * T + r), y = StarwellY + MathF.Sin(ang) * (WellRim * T + r);
            if (BossCanBe(x, y)) return (x, y);
        }
        return (StarwellX + WellRim * T + r, StarwellY);
    }

    // Its body is about 10 pixels wide; it needs dry, open ground under all of it.
    bool BossCanBe(float x, float y)
    {
        foreach (var (ax, ay) in new[] { (x - 5, y - 4), (x + 5, y - 4), (x - 5, y), (x + 5, y) })
            if (!Walkable(TileAt((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T)))) return false;
        foreach (var r in Solids())
            if (x + 5 > r.X && x - 5 < r.X + r.W && y > r.Y && y - 4 < r.Y + r.H) return false;
        return true;
    }

    void BossRetreat(string msg)
    {
        if (boss == null) return;
        Burst(boss.X, boss.Y - 4, "#cfe8ee", 10);
        Sfx.Play("splash");
        boss = null;
        bolts.Clear();
        if (msg != null) Toast(msg, 4.5f);
    }

    /* ---------- The fight ---------- */
    void UpdateBoss(float dt)
    {
        UpdateBolts(dt);
        var b = boss;
        if (b == null || scene != "world" || mode != "play") return;
        b.T += dt;
        b.Hurt = Math.Max(0, b.Hurt - dt);
        float dx = player.X - b.X, dy = player.Y - b.Y, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        if (b.Phase != "calm" && Dist(player.X, player.Y, StarwellX, StarwellY) > 170)
        {
            BossRetreat($"{Data.MountName} loses interest and slips back into the Starwell.");
            return;
        }
        switch (b.Phase)
        {
            case "emerge":
            case "surface":
            {
                // A leap from the water to the shore.
                float k = Math.Min(1, b.T / 0.6f);
                b.X = b.FromX + (b.ToX - b.FromX) * k;
                b.Y = b.FromY + (b.ToY - b.FromY) * k;
                b.Lift = MathF.Sin(k * MathF.PI) * 18;
                if (k < 1) break;
                b.Lift = 0;
                quake = 0.25f;
                Burst(b.X, b.Y, "#e8cf96", 10);
                Sfx.Play("stomp");
                if (b.Phase == "surface") ShootBolts(b);
                Stalk(b);
                break;
            }
            case "stalk":
            {
                // Circle you at a distance, edging closer, until it picks its next move.
                float want = 46, rad = (d - want) / want, tx = -dy / d * b.Circle, ty = dx / d * b.Circle;
                float vx = dx / d * rad * 1.4f + tx, vy = dy / d * rad * 1.4f + ty, vl = MathF.Max(0.01f, MathF.Sqrt(vx * vx + vy * vy));
                float sp = 44 * dt;
                float nx = b.X + vx / vl * sp, ny = b.Y + vy / vl * sp;
                if (BossCanBe(nx, ny)) { b.X = nx; b.Y = ny; }
                else if (BossCanBe(b.X + dx / d * sp, b.Y + dy / d * sp)) { b.X += dx / d * sp; b.Y += dy / d * sp; b.Circle = -b.Circle; }
                else b.Circle = -b.Circle;
                if (MathF.Abs(vx) > 0.2f) b.Dir = vx > 0 ? 1 : -1;
                if (b.T < b.Wait) break;
                float left = b.Spirit / BossSpirit;
                if (left < 0.45f && b.Attacks % 3 == 2) StartDive(b);
                else if (left < 0.75f && d < 60 && rng.NextDouble() < 0.5) { b.Phase = "stompRear"; b.T = 0; b.Hit = false; Sfx.Play("neigh"); }
                else { b.Phase = "rear"; b.T = 0; Sfx.Play("neigh"); }
                break;
            }
            case "rear":
                // It rears up and paws the air. It keeps its eyes on you, then picks its line and goes.
                if (b.T < 0.45f) { b.AimX = dx / d; b.AimY = dy / d; b.Dir = dx > 0 ? 1 : -1; }
                if (b.T >= 0.75f) { b.Phase = "charge"; b.T = 0; b.Ran = 0; b.Hit = false; Sfx.Play("stomp"); }
                break;
            case "charge":
            {
                float step = ChargeSpeed * dt, nx = b.X + b.AimX * step, ny = b.Y + b.AimY * step;
                if (!BossCanBe(nx, ny))
                {
                    // Straight into a palm or the water's edge: it's dazed for a good while.
                    Winded(b, 2.2f);
                    quake = 0.35f;
                    Burst(nx, ny - 6, "#cfe8ee", 10);
                    Sfx.Play("hit");
                    Floater("Crash!", b.X, b.Y - 24, "#ffd76a");
                    break;
                }
                b.X = nx; b.Y = ny; b.Ran += step;
                if ((int)(b.T * 20) % 2 == 0) particles.Add(new Particle { X = b.X - b.AimX * 8, Y = b.Y, Vx = Rand(-12, 12), Vy = Rand(-25, -10), Life = 0.3f, Color = "#e8cf96" });
                if (!b.Hit && Dist(b.X, b.Y, player.X, player.Y) < 10 && iframes <= 0)
                {
                    b.Hit = true;
                    HurtPlayer(ChargeHurt, b.X, b.Y, 6);
                }
                if (b.Ran >= ChargeRange || b.T > 1.2f) Winded(b, 1.3f);
                break;
            }
            case "winded":
                if (b.T >= b.Wait) { b.Attacks++; Stalk(b); }
                break;
            case "stompRear":
                if (b.T < 0.6f) break;
                b.Phase = "stomp"; b.T = 0; b.Ring = StompMin;
                quake = 0.45f;
                Sfx.Play("stomp");
                Burst(b.X, b.Y, "#e8cf96", 14);
                break;
            case "stomp":
                // A ring of sand and water rolls out from its hooves. Right underneath it is the one safe place.
                b.Ring += 80 * dt;
                if (!b.Hit && MathF.Abs(StompDist(b) - b.Ring) < 4 && iframes <= 0)
                {
                    b.Hit = true;
                    HurtPlayer(StompHurt, b.X, b.Y, 4);
                }
                if (b.Ring < StompMax) break;
                b.Ring = -1;
                Winded(b, 0.9f);
                break;
            case "dive":
            {
                float k = Math.Min(1, b.T / 0.5f);
                b.X = b.FromX + (b.ToX - b.FromX) * k;
                b.Y = b.FromY + (b.ToY - b.FromY) * k;
                b.Lift = MathF.Sin(k * MathF.PI) * 14;
                if (k < 1) break;
                b.Lift = 0;
                Burst(b.X, b.Y, "#cfe8ee", 14);
                Sfx.Play("splash");
                b.Phase = "under"; b.T = 0;
                break;
            }
            case "under":
                // A shadow circles under the surface, then it bursts out on your side of the pool.
                if (b.T < 1.3f) break;
                var (sx, sy) = ShorePoint(MathF.Atan2(player.Y - StarwellY, player.X - StarwellX), 8);
                b.Phase = "surface"; b.T = 0;
                b.FromX = StarwellX; b.FromY = StarwellY; b.ToX = sx; b.ToY = sy;
                Burst(StarwellX, StarwellY, "#ffffff", 12);
                Sfx.Play("splash");
                break;
        }
    }

    // The shockwave rolls out as a flat ellipse on the sand (drawn with Pix.Ring), so it's measured the same way.
    float StompDist(Boss b)
    {
        float dx = player.X - b.X, dy = (player.Y - b.Y) / 0.4375f;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    void Stalk(Boss b)
    {
        b.Phase = "stalk";
        b.T = 0;
        b.Wait = Rand(1.2f, 2.2f) * (0.6f + 0.4f * b.Spirit / BossSpirit);
        if (rng.NextDouble() < 0.5) b.Circle = -b.Circle;
    }

    void Winded(Boss b, float secs)
    {
        b.Phase = "winded";
        b.T = 0;
        b.Wait = secs;
    }

    void StartDive(Boss b)
    {
        b.Phase = "dive"; b.T = 0;
        b.FromX = b.X; b.FromY = b.Y; b.ToX = StarwellX; b.ToY = StarwellY;
        b.Attacks++;
        Sfx.Play("neigh");
    }

    // Water bolts spat at you when it surfaces: three, or five once it's nearly worn out.
    void ShootBolts(Boss b)
    {
        int n = b.Spirit < BossSpirit * 0.25f ? 5 : 3;
        float a0 = MathF.Atan2(player.Y - 4 - (b.Y - 8), player.X - b.X);
        for (int i = 0; i < n; i++)
        {
            float a = a0 + (i - (n - 1) / 2f) * 0.32f;
            bolts.Add(new Bolt { X = b.X + b.Dir * 8, Y = b.Y - 10, Vx = MathF.Cos(a) * 85, Vy = MathF.Sin(a) * 85, Life = 1.8f });
        }
        Sfx.Play("bolt");
    }

    void UpdateBolts(float dt)
    {
        if (bolts.Count == 0 || mode != "play") return;
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            var o = bolts[i];
            o.Life -= dt;
            o.X += o.Vx * dt; o.Y += o.Vy * dt;
            if (iframes <= 0 && Dist(o.X, o.Y, player.X, player.Y - 5) < 6)
            {
                bolts.RemoveAt(i);
                Burst(o.X, o.Y, "#cfe8ee", 6);
                HurtPlayer(BoltHurt, o.X, o.Y, 3);
                if (boss == null) return;   // knocked out: Faint has cleared the bolts
                continue;
            }
            if (o.Life <= 0) { Burst(o.X, o.Y, "#cfe8ee", 3); bolts.RemoveAt(i); }
        }
    }

    // It can be struck while it's on the sand and in front of you (not mid-leap or under the water).
    bool BossInReach()
    {
        var b = boss;
        if (b == null || b.Phase is "emerge" or "surface" or "dive" or "under" or "calm") return false;
        float fx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0, fy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
        float dx = b.X - player.X, dy = b.Y - player.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        return d < 22 && (d < 11 || (dx * fx + dy * fy) / d > 0.2f);
    }

    Target BossTarget()
    {
        var b = boss;
        if (BossInReach()) return new Target { Type = "boss", Label = $"Strike {Data.MountName} ({Weapon().name})" };
        string hint = b.Phase switch
        {
            "rear" => "It's rearing to charge! Get out of its way",
            "stompRear" => "It's going to stomp! Get right in close, or well away",
            "winded" => "It's winded! Get close and strike",
            "under" or "dive" => "It's under the water. Watch where it comes up",
            "calm" => "",
            _ => "Dodge its charges, then strike while it's winded"
        };
        return hint == "" ? null : new Target { Type = "info", Label = hint };
    }

    // A blow tires it out; a blow while it's winded tires it out much more. It shrugs off blows for a moment after each one.
    void StrikeBoss()
    {
        var b = boss;
        if (b == null || swingT > 0.12f) return;
        var (_, dmg) = Weapon();
        Swing(WeaponTool(), 0.3f);
        FaceToward(b.X, b.Y);
        if (b.Hurt > 0) { Sfx.Play("nope"); return; }
        bool open = b.Phase == "winded";
        float tire = (4 + dmg * 0.6f) * (open ? 2.5f : 1);
        b.Spirit -= tire;
        b.Hurt = 0.4f;
        Sfx.Play("hit");
        Burst(b.X, b.Y - 8, open ? "#ffd76a" : "#cfe8ee", open ? 9 : 5);
        if (open) Floater("Big hit!", b.X, b.Y - 26, "#ffd76a");
        if (b.Spirit <= 0) { CalmBoss(); return; }
        // Hit it while it's fresh and it may well rear up and charge straight back at you.
        if (b.Phase == "stalk" && rng.NextDouble() < 0.4) { b.Phase = "rear"; b.T = 0; Sfx.Play("neigh"); }
    }

    void CalmBoss()
    {
        var b = boss;
        b.Phase = "calm"; b.T = 0; b.Spirit = 0; b.Ring = -1; b.Lift = 0;
        bolts.Clear();
        b.Dir = player.X < b.X ? -1 : 1;
        FaceToward(b.X, b.Y);
        Sfx.Play("rare");
        Burst(b.X, b.Y - 8, "#ffd76a", 16);
        Talk(new()
        {
            new("", "The creature stumbles, snorts, and folds its legs under it on the sand. Its gold eyes follow you."),
            new("", "You hold out your hand. It sniffs it, then pushes its nose into your palm. It smells of salt and coconut."),
            new("", $"It seems to have decided you're worth following. {Data.MountName} is yours to ride.")
        }, TameBoss);
    }

    void TameBoss()
    {
        state.tamed = true;
        state.riding = false;
        state.mountX = boss.X; state.mountY = boss.Y;
        boss = null;
        bolts.Clear();
        GainXp(80);
        Save();
        catchOpenedAt = Raylib.GetTime();
        mode = "tamed";
        SetPrompt("");
    }

    void CloseTamed()
    {
        if (mode != "tamed" || Raylib.GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        Toast($"Press <ride> to ride {Data.MountName}, and <ride> again to hop off. It gallops on land and swims across the open sea. From anywhere outdoors, <ride> whistles it over.", 7);
    }

    /* ---------- Riding ---------- */
    static bool Swimmable(char t) => t is '~' or 'w' or 'l' or 'o' or 'm' or 'T' or 'x';
    char TileUnder(float x, float y) => TileAt((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1.5f) / T));
    bool Swimming => Riding && Swimmable(TileUnder(player.X, player.Y));

    // Any water away from the edge of the map.
    static bool SwimOk(int tx, int ty, char t) => Swimmable(t) && tx > 0 && ty > 0 && tx < COLS - 1 && ty < ROWS - 1;

    // In a storm it won't head out into deep water. That's judged by its middle, not its hooves: climbing out of the
    // deep, its back end stays over it for a few steps, and if a storm catches you out there it must still swim you home.
    bool StormHoldsBack(float x, float y) => Stormy && TileUnder(x, y) == '~' && TileUnder(player.X, player.Y) != '~';

    void ToggleRide()
    {
        // R gets you on and off the boat: from beside it (E may belong to a fishing spot there), and at the helm it lands
        // (E fishes over deep water). In a storm the boat stays tied up, so if you have Tidemane, R calls it instead.
        if (eclipse != null) return;   // you stand your ground with the agong
        if (mode == "play" && Aboard) { LandBoat(); return; }
        if (mode == "play" && boss == null && BoatInReach() && !(Stormy && state.tamed)) { BoardBoat(); return; }
        if (!state.tamed || mode != "play" || boss != null) return;
        if (scene != "world") { Toast($"{Data.MountName} is waiting for you outside."); return; }
        if (state.riding) Dismount();
        else Mount();
    }

    // Close by, you hop on. From further away, a whistle brings it galloping (or swimming) over.
    void Mount()
    {
        if (Aboard) { Toast("Land the boat before calling Tidemane."); return; }
        bool near = Dist(player.X, player.Y, state.mountX, state.mountY) < 26;
        if (!near)
        {
            Sfx.Play("whistle");
            Burst(player.X - 8, player.Y, Swimmable(TileUnder(player.X, player.Y)) ? "#cfe8ee" : "#e8cf96", 10);
        }
        else FaceToward(state.mountX, state.mountY);
        state.riding = true;
        state.mountX = player.X; state.mountY = player.Y;
        if (player.Face is "left" or "right") mountDir = player.Face == "right" ? 1 : -1;
        Sfx.Play("neigh");
        if (!state.Hinted("rideTip"))
        {
            state.hinted["rideTip"] = true;
            Toast("In the saddle! Ride into the sea to swim. R hops off on dry land.", 4);
        }
        Save();
    }

    // You need dry ground (or shallows, in waders) to step down onto, right beside it.
    void Dismount()
    {
        float mx = player.X, my = player.Y;
        (float x, float y)? spot = null;
        foreach (var (ox, oy) in new[] { (-9f * mountDir, 0f), (9f * mountDir, 0f), (0f, 6f), (0f, -6f), (-9f * mountDir, 6f), (9f * mountDir, 6f), (0f, 0f) })
            if (CanStand(mx + ox, my + oy, Wading)) { spot = (mx + ox, my + oy); break; }
        if (spot is not (float sx, float sy)) { Sfx.Play("nope"); Toast("Nowhere dry to hop off here. Ride up onto the shore first."); return; }
        state.riding = false;
        state.mountX = mx; state.mountY = my;
        player.X = sx; player.Y = sy;
        Sfx.Play("pickup");
        Save();
    }

    // Going indoors, underground or out to sea by boat, you leave it waiting where you got off.
    void LeaveMount()
    {
        if (!state.riding) return;
        state.riding = false;
        state.mountX = player.X; state.mountY = player.Y;
    }

    bool MountNear() => state.tamed && !state.riding && scene == "world" && boss == null
        && Dist(player.X, player.Y, state.mountX, state.mountY) < 16;

    /* ---------- Drawing ---------- */
    // Tidemane with its feet at (x, y). Pose is "stand", "run", "rear", "lie" or "swim"; frame alternates the gallop.
    // Drawn facing right and mirrored for dir -1.
    void DrawTidemane(float fx, float fy, int dir, string pose, int frame, bool flash = false, float lift = 0, bool moving = false)
    {
        int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy - lift);
        bool lie = pose == "lie", rear = pose == "rear", run = pose == "run", swim = pose == "swim";
        int bob = swim ? FloatBob(fx) : run && frame == 1 ? -1 : 0, low = lie ? 4 : 0, up = rear ? -4 : 0;
        void R(int ox, int oy, int w, int h, string c, int dy = 0)
        {
            string col = flash ? "#ffffff" : c;
            pix.Rect(dir > 0 ? x + ox : x - ox - w + 1, y + oy + dy + bob + low, w, h, col);
        }
        const string body = "#2a9d8f", shade = "#1d6f68", belly = "#8fd3c4", mane = "#f2fbff", mane2 = "#bfe6f0", horn = "#ff8a7a",
            fin = "#5fd6c9", finDark = "#3fb5a8", dark = "#10243a";
        if (!swim) pix.Rect(x - 13, (int)MathF.Round(fy) + 1, 22, 1, "rgba(0,0,0,0.22)");

        if (swim)
        {
            // Swimming, the fish tail trails out behind along the surface and its fan flicks up out of the water.
            int flick = (int)MathF.Round(MathF.Sin(time * 3.2f + fx * 0.1f) * 1.3f);
            R(-12, -9, 3, 4, shade); R(-15, -8, 3, 3, body); R(-18, -7, 3, 2, shade);
            R(-21, -9 + flick, 2, 3, fin); R(-23, -11 + flick, 2, 4, fin); R(-24, -12 + flick, 1, 3, finDark); R(-20, -7, 2, 1, finDark);
        }
        else
        {
            // The fish tail: down from the haunch to the sand, then up into a fan.
            int sway = (int)MathF.Round(MathF.Sin(time * (run ? 14 : 4) + fx) * (lie ? 0 : 1));
            R(-11, -9, 4, 4, shade); R(-13, -7, 3, 4, body); R(-15, -5, 3, 4, body); R(-17, -3, 3, 3, shade);
            R(-20, -7 + sway, 2, 6, fin); R(-22, -9 + sway, 2, 5, fin); R(-23, -11 + sway, 1, 4, finDark); R(-20, -2, 3, 2, finDark);
            R(-18, -4, 2, 2, fin);
        }
        // Forelegs, with fin-edged hooves. Galloping, they reach and tuck in turn; rearing, they paw the air.
        if (lie) { R(0, -3, 6, 2, shade); R(5, -2, 3, 1, fin); }
        else if (!swim)
        {
            int a = run ? (frame == 0 ? 1 : -1) : 0;
            R(1 + a, -5, 2, 4, shade, up); R(0 + a, -1, 4, 1, fin, up);
            R(5 - a, -5, 2, 4, body, up); R(4 - a, -1, 4, 1, fin, up);
            if (rear) { R(6, -10, 2, 2, body); R(2, -10, 2, 2, shade); }
        }
        // The body, with a pale belly, a little dorsal fin and starlight speckles.
        // (Swimming, the pale belly is under the surface, so the flank just darkens into the water.)
        R(-8, -11, 12, 1, body, up / 2); R(-10, -10, 16, 4, body, up / 2); R(-9, -6, 14, 1, swim ? shade : belly, up / 2); R(-7, -5, 10, 1, shade, up / 2);
        R(-6, -13, 3, 2, fin, up / 2); R(-5, -14, 1, 1, finDark, up / 2);
        float tw = (time * 1.3f + fx * 0.1f) % 1;
        foreach (var (sx, sy, k) in new[] { (-6, -9, 0.0f), (-2, -10, 0.33f), (1, -8, 0.66f), (-8, -8, 0.5f) })
            R(sx, sy, 1, 1, MathF.Abs(tw - k) < 0.12f ? "#ffffff" : "#fff6d0", up / 2);
        // Neck and head, angled down toward the muzzle.
        int hy = up + (lie ? 2 : 0);
        R(3, -14, 4, 5, body, hy); R(5, -17, 4, 3, body, hy); R(4, -12, 2, 3, shade, hy);
        R(6, -20, 4, 3, body, hy); R(7, -19, 5, 2, body, hy); R(9, -17, 4, 2, body, hy); R(10, -15, 3, 1, shade, hy);
        R(12, -17, 1, 1, dark, hy); R(10, -15, 2, 1, "#16514b", hy);
        R(8, -19, 1, 1, flash ? "#ffffff" : "#ffd76a", hy);
        // Coral horns, and a mane of sea foam that streams back.
        R(7, -22, 1, 2, horn, hy); R(6, -23, 1, 1, horn, hy); R(9, -22, 1, 2, horn, hy); R(10, -23, 1, 1, horn, hy);
        int flow = (int)(time * (run ? 10 : 3)) % 2;
        R(5, -21, 2, 1, mane, hy); R(4, -20, 2, 2, mane, hy); R(3, -18, 2, 2, flow == 0 ? mane : mane2, hy);
        R(2, -16, 2, 2, mane, hy); R(1 - flow, -14, 2, 2, mane2, hy); R(0, -12, 2, 1, mane, hy); R(-1 - flow, -13, 1, 1, mane2, hy);
        if (swim) DrawSwimWater(x, (int)MathF.Round(fy) - 5, dir, moving);
    }

    // Floating, it rises and settles a pixel on the swell (the rider goes with it).
    int FloatBob(float fx) => MathF.Sin(time * 2.2f + fx * 0.07f) > 0.3f ? 1 : 0;

    // The water around a swimming Tidemane, with the surface at row wl. The water itself (from the ground layer) is drawn
    // back over everything below the surface, only where there really is water, so it matches the sea and stops at the
    // shore. Broken foam where its body and tail cut the surface; moving, a bow wave and a wake fanning out behind;
    // resting, little ripples spreading from either end. Every touch of foam is on water only.
    void DrawSwimWater(int x, int wl, int dir, bool moving)
    {
        void OnWater(int px, int py, Color c)
        {
            if (px >= 0 && py >= 0 && px < PW && py < PH && Wet(ShapePx(px, py))) pix.Rect(px, py, 1, 1, c);
        }
        for (int py = wl; py <= wl + 6; py++)
            for (int px = x - 16; px <= x + 16; px++)
                if (px >= 0 && py >= 0 && px < PW && py < PH && Wet(ShapePx(px, py))) pix.Rect(px, py, 1, 1, worldBase.Buf[py * PW + px]);
        int lap = (int)(time * 5);
        var foam = Pal.Rgba(232, 246, 250, 0.65f);
        var foamDim = Pal.Rgba(232, 246, 250, 0.3f);
        for (int ox = -20; ox <= 8; ox++)
        {
            int px = x + dir * ox;
            double h = Pix.Hash((px >> 1) + lap, 7, 260);
            if (h < 0.6) OnWater(px, wl, foam);
            else if (h < 0.75) OnWater(px, wl + 1, foamDim);
        }
        if (moving)
        {
            for (int i = 0; i < 3; i++) OnWater(x + dir * (9 + i), wl - (lap + i) % 2, foam);
            // The wake: two arms opening out behind it in a V.
            for (int k = 1; k <= 9; k++)
            {
                if ((k + lap) % 4 == 0) continue;
                var c = Pal.Rgba(232, 246, 250, 0.55f * (1 - k / 10f));
                int px = x - dir * (21 + k * 2), spread = 1 + k * 3 / 4;
                OnWater(px, wl - spread, c); OnWater(px - dir, wl - spread, c);
                OnWater(px, wl + spread, c); OnWater(px - dir, wl + spread, c);
            }
            return;
        }
        float ph = time * 0.7f % 1;
        var ring = Pal.Rgba(232, 246, 250, 0.45f * (1 - ph));
        foreach (int side in new[] { -1, 1 })
        {
            int cx = x + side * (int)(13 + ph * 8);
            OnWater(cx - 1, wl, ring); OnWater(cx, wl + 1, ring); OnWater(cx + 1, wl, ring);
        }
    }

    // Front and back views keep the same sea-green body, coral horns and foam mane as the side view. The rider
    // sits behind the neck coming toward us, and in front of it going away; the tail trails along the water.
    void DrawTidemaneEndOn(int x, int y, bool away, bool swim, bool moving, int frame, Action rider = null)
    {
        int bob = swim ? FloatBob(x) : moving && frame == 1 ? -1 : 0;
        const string body = "#2a9d8f", shade = "#1d6f68", belly = "#8fd3c4", mane = "#f2fbff",
            mane2 = "#bfe6f0", horn = "#ff8a7a", fin = "#5fd6c9", finDark = "#3fb5a8";
        void R(int ox, int oy, int w, int h, string c) => pix.Rect(x + ox, y + oy + bob, w, h, c);
        int sway = (int)MathF.Round(MathF.Sin(time * (moving ? 9 : 3.2f)) * (moving ? 2 : 1));
        int flow = (int)(time * (moving ? 10 : 3)) % 2;
        void Tail()
        {
            if (away)
            {
                R(-2, -7, 5, 5, shade); R(-1, -5, 3, 5, body);
                R(sway - 1, -1, 3, 4, body); R(sway, 2, 2, 3, shade);
                R(sway - 3, 3, 3, 3, fin); R(sway + 2, 3, 3, 3, fin);
                R(sway - 4, 2, 1, 3, finDark); R(sway + 5, 2, 1, 3, finDark);
                R(sway - 2, 6, 2, 1, finDark); R(sway + 2, 6, 2, 1, finDark);
            }
            else
            {
                R(-1, -16, 3, 5, shade); R(sway - 1, -20, 3, 5, body);
                R(sway - 4, -23, 3, 3, fin); R(sway + 2, -23, 3, 3, fin);
                R(sway - 5, -24, 1, 3, finDark); R(sway + 5, -24, 1, 3, finDark);
                R(sway - 2, -21, 5, 2, finDark);
            }
        }
        if (!swim) pix.Rect(x - 6, y + 1, 13, 2, Pal.Rgba(0, 0, 0, .22f));
        if (!away) Tail();
        // Paired forelegs alternate their reach. There are no legs in the swimming silhouette.
        if (!swim)
            foreach (int side in new[] { -1, 1 })
            {
                int stride = moving ? (frame == 0 ? side : -side) : 0;
                R(side < 0 ? -5 : 3, -6 + stride, 2, 5, side < 0 ? shade : body);
                R(side < 0 ? -6 : 3, -1 + stride, 3, 1, fin);
            }
        R(-3, -15, 7, 2, body); R(-5, -13, 11, 6, body);
        R(-4, -7, 9, 2, swim ? shade : belly); R(-3, -5, 7, 1, shade);
        R(-5, -12, 2, 4, shade); R(4, -11, 1, 3, finDark);
        foreach (var (sx, sy) in new[] { (-3, -11), (3, -10), (-2, -8), (2, -13) })
            R(sx, sy, 1, 1, (int)(time * 3 + sx) % 3 == 0 ? "#ffffff" : "#fff6d0");
        if (!away) rider?.Invoke();
        if (away)
        {
            // Seen from behind, a white crest falls from the horns down the neck; no eyes on the back of the head.
            R(-2, -21, 5, 9, body); R(-3, -24, 7, 4, body);
            R(-4, -25, 2, 2, body); R(3, -25, 2, 2, shade);
            R(-3, -27, 1, 3, horn); R(-4, -28, 1, 1, horn);
            R(3, -27, 1, 3, horn); R(4, -28, 1, 1, horn);
            R(-1, -25, 3, 4, mane); R(-2, -21, 4, 3, mane2);
            R(-1 + flow, -18, 3, 3, mane); R(-1, -15, 2, 3, mane2);
            rider?.Invoke();
            Tail();
        }
        else
        {
            // Coming toward us, the broad muzzle, both gold eyes and the pale chest sit in front of the rider.
            R(-3, -16, 7, 7, body); R(-2, -10, 5, 3, belly);
            R(-4, -18, 2, 3, mane); R(3, -18, 2, 3, mane2);
            R(-4 - flow, -15, 2, 4, mane2); R(4, -14, 1 + flow, 3, mane);
            R(-3, -19, 1, 3, horn); R(-4, -20, 1, 1, horn);
            R(3, -19, 1, 3, horn); R(4, -20, 1, 1, horn);
            R(-1, -17, 3, 2, mane); R(0, -15, 1, 2, mane2);
            R(-3, -14, 1, 1, "#ffd76a"); R(3, -14, 1, 1, "#ffd76a");
            R(-3, -12, 7, 3, body); R(-2, -9, 5, 1, shade);
            R(-2, -11, 1, 1, "#16514b"); R(2, -11, 1, 1, "#16514b");
        }
        if (swim) DrawEndOnSwimWater(x, y, away, moving);
    }

    void DrawEndOnSwimWater(int x, int y, bool away, bool moving)
    {
        bool IsWater(int px, int py) => px >= 0 && py >= 0 && px < PW && py < PH && Wet(ShapePx(px, py));
        void Foam(int px, int py, float alpha)
        {
            if (IsWater(px, py)) pix.Rect(px, py, 1, 1, Pal.Rgba(232, 246, 250, alpha));
        }
        // Only the bottom of the body is submerged. The up-facing tail and its fan are on the surface behind it.
        for (int py = y - 5; py <= y + 1; py++)
            for (int px = x - 6; px <= x + 6; px++)
                if ((!away || Math.Abs(px - x) > 2) && IsWater(px, py)) pix.Rect(px, py, 1, 1, worldBase.Buf[py * PW + px]);
        int lap = (int)(time * 5), ahead = away ? -1 : 1;
        for (int k = -5; k <= 5; k++)
            if ((k + lap) % 3 != 0) Foam(x + k, y - 5 + Math.Abs(k) / 3, .6f);
        for (int k = 0; k < 8; k++)
        {
            int spread = moving ? 6 + k : 6 + (int)(time * 3 % 4);
            int wy = y - 5 - ahead * (9 + k * 2);
            if ((k + lap) % 4 == 0) continue;
            Foam(x - spread, wy, .48f * (1 - k / 8f));
            Foam(x + spread, wy, .48f * (1 - k / 8f));
        }
    }

    // The saddle is the common anchor for the rider, their rod and anything held overhead.
    (int x, int y) RiderSeat(int x, int y)
    {
        bool moving = player.Moving && mode is "play" or "build";
        int bob = Swimming ? FloatBob(x) : moving && (int)(player.WalkT * 8) % 2 == 1 ? -1 : 0;
        return player.Face switch
        {
            "up" => (x, y - 7 + bob), "down" => (x, y - 10 + bob),
            _ => (x + (player.Face == "left" ? 1 : -1), y - 8 + bob)
        };
    }

    // You in the saddle, sitting up over its back.
    void DrawRider(int x, int y, bool moving, int step, int arms = 0, int pump = 0, HandPose hands = null)
    {
        if (player.Face is "left" or "right") mountDir = player.Face == "right" ? 1 : -1;
        bool swim = Swimming;
        var seat = RiderSeat(x, y);
        void Rider() => LookData.DrawPerson(pix, state.look, seat.x, seat.y, player.Face, 0, shadow: false,
            blink: time % 3.7f < 0.12f, arms: arms, swing: pump, hand: hands?.Hand, hand2: hands?.Hand2, armsBehind: hands?.ArmsBehind == true);
        if (player.Face is "up" or "down")
        {
            DrawTidemaneEndOn(x, y, player.Face == "up", swim, moving, step, Rider);
            return;
        }
        string pose = swim ? "swim" : moving ? "run" : "stand";
        DrawTidemane(x, y, mountDir, pose, step, moving: moving);
        Rider();
    }

    // Tidemane waiting for you: it shifts its weight, flicks its tail and now and then shakes out its mane.
    void DrawMountIdle()
    {
        if (!state.tamed || state.riding || boss != null) return;
        bool swim = Swimmable(TileUnder(state.mountX, state.mountY));
        if (Math.Abs(player.Y - state.mountY) > Math.Abs(player.X - state.mountX))
        {
            DrawTidemaneEndOn((int)MathF.Round(state.mountX), (int)MathF.Round(state.mountY), player.Y < state.mountY, swim, false, 0);
            return;
        }
        int dir = player.X < state.mountX ? -1 : 1;
        DrawTidemane(state.mountX, state.mountY, dir, swim ? "swim" : (time % 6 < 0.5f ? "rear" : "stand"), 0);
    }

    void DrawBoss(float t)
    {
        var b = boss;
        if (b == null) return;
        if (b.Phase == "under")
        {
            // A big shadow circling under the surface of the pool.
            float a = t * 3, sx = StarwellX + MathF.Cos(a) * 9, sy = StarwellY + MathF.Sin(a) * 4;
            var sh = Pal.Rgba(4, 18, 30, 0.6f);
            pix.Rect(sx - 8, sy - 2, 16, 4, sh); pix.Rect(sx - 6, sy - 3, 12, 6, sh);
            for (int i = 0; i < 3; i++)
            {
                double ph = (t * 1.5 + i * 0.33) % 1;
                pix.Rect(sx - 4 + i * 4, sy - 2 - ph * 8, 1, 1, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
            }
            return;
        }
        string pose = b.Phase switch
        {
            "rear" or "stompRear" => "rear", "charge" or "stalk" => "run", "calm" => "lie", "winded" => "stand",
            _ => "run"
        };
        int frame = (int)(t * (b.Phase == "charge" ? 14 : 8)) % 2;
        if (b.Lift > 0) pix.Rect(b.X - 9, b.Y + 1, 18, 1, "rgba(0,0,0,0.25)");
        // Shaking while it rears, so you can see something's coming.
        float jx = b.Phase is "rear" or "stompRear" ? MathF.Sin(t * 60) : 0;
        DrawTidemane(b.X + jx, b.Y, b.Dir, pose, frame, b.Hurt > 0.25f, b.Lift);
        if (b.Phase == "winded")
        {
            // Little stars circling its head.
            for (int i = 0; i < 3; i++)
            {
                float a = t * 5 + i * MathF.Tau / 3;
                pix.Rect(b.X + b.Dir * 8 + MathF.Cos(a) * 6, b.Y - 25 + MathF.Sin(a) * 2, 1, 1, "#ffd76a");
            }
        }
        if (b.Phase is "rear" or "stompRear")
        {
            // A "!" over its head.
            int ex = (int)MathF.Round(b.X + b.Dir * 8), ey = (int)MathF.Round(b.Y) - 36;
            pix.Rect(ex - 2, ey - 1, 5, 10, "#10243a"); pix.Rect(ex - 1, ey, 3, 8, b.Phase == "rear" ? "#ff6a5a" : "#ffd76a");
            pix.Rect(ex, ey + 1, 1, 4, "#ffffff"); pix.Rect(ex, ey + 6, 1, 1, "#ffffff");
        }
        if (b.Phase == "stompRear")
        {
            // Where the shockwave will reach, and the safe spot right under its hooves.
            pix.Ring(b.X, b.Y, StompMax / 1.6f, Pal.Rgba(255, 215, 106, 0.25f + 0.2f * MathF.Sin(t * 20)));
            pix.Ring(b.X, b.Y, StompMin / 1.6f, Pal.Rgba(127, 211, 107, 0.7f));
        }
    }

    // The shockwave and the water bolts, drawn over everything on the ground.
    void DrawBossEffects(float t)
    {
        if (boss is { Ring: > 0 } b)
        {
            float k = (b.Ring - StompMin) / (StompMax - StompMin);
            pix.Ring(b.X, b.Y, b.Ring / 1.6f, Pal.Rgba(255, 240, 200, 0.9f * (1 - k)));
            pix.Ring(b.X, b.Y, b.Ring / 1.6f - 1.5, Pal.Rgba(232, 207, 150, 0.7f * (1 - k)));
        }
        foreach (var o in bolts)
        {
            float l = MathF.Max(1, MathF.Sqrt(o.Vx * o.Vx + o.Vy * o.Vy));
            for (int i = 1; i <= 3; i++) pix.Rect(o.X - o.Vx / l * i * 2, o.Y - o.Vy / l * i * 2, 1, 1, Pal.Rgba(190, 235, 250, 0.6f - i * 0.15f));
            pix.Rect(o.X - 1, o.Y - 1, 3, 3, "#7fd6f0");
            pix.Rect(o.X - 1, o.Y - 1, 2, 2, "#ffffff");
        }
    }

    // A coral stele by the pool with the carving on it.
    void DrawCarving()
    {
        int X = (int)CarvingX, Y = (int)CarvingY;
        pix.Rect(X - 5, Y, 10, 2, "rgba(0,0,0,0.2)");
        pix.Rect(X - 4, Y - 13, 8, 14, "#e8a89a"); pix.Rect(X - 3, Y - 15, 6, 2, "#e8a89a");
        pix.Rect(X - 4, Y - 13, 2, 14, "#f4c4b8"); pix.Rect(X + 2, Y - 13, 2, 14, "#c98478");
        // The carved hippocamp, very small.
        pix.Rect(X - 2, Y - 10, 3, 2, "#9a5a50"); pix.Rect(X, Y - 12, 2, 2, "#9a5a50"); pix.Rect(X - 3, Y - 8, 1, 2, "#9a5a50");
        pix.Rect(X - 2, Y - 5, 4, 1, "#9a5a50"); pix.Rect(X - 2, Y - 3, 3, 1, "#9a5a50");
        if (!state.Hinted("carving") && (time * 0.7f) % 1 < 0.15f) pix.Rect(X + 1, Y - 14, 1, 1, "#ffffff");
    }

    // The Starwell's surface: stars reflected in it, and a soft glow at night.
    void DrawStarwell(float t)
    {
        for (int i = 0; i < 7; i++)
        {
            float a = (float)Pix.Hash(i, 3, 41) * MathF.Tau, r = (float)Pix.Hash(i, 4, 41) * (WellDeep * T - 2);
            float sx = StarwellX + MathF.Cos(a) * r * 1.3f, sy = StarwellY + MathF.Sin(a) * r * 0.8f;
            float tw = 0.5f + 0.5f * MathF.Sin(t * (1.5f + i * 0.4f) + i);
            pix.Rect(sx, sy, 1, 1, Pal.Rgba(255, 246, 208, (Night ? 0.9f : 0.35f) * tw));
        }
    }
}
