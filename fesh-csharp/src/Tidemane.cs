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
        Toast("A hidden pool ringed by palms: the Starwell. The trackPrints run right up to the water, and stop.", 5);
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

    // How it moves under you (1.20): it picks up speed over a moment and pulls up short when you let go, rather than
    // going from standing to full gallop in a frame. Top speeds are as before (92 on land, 74 swimming).
    (float x, float y) rideVel;
    float mountPhase;      // through the stride, from the distance it has really covered (so pushing at a wall doesn't gallop)
    float mountSpeed;      // how fast it has really been going, smoothed
    int mountGaitFrame = -1, hoofSide;
    const float RideAccel = 700, SwimAccel = 450, RideBrake = 900;
    // Climbing on and off: the rider swings up into the saddle (or steps down) over a moment. Only looks: you're in the
    // saddle (or off) at once, and moving finishes it.
    float hopT, hopFromX, hopFromY;
    bool hopDown;
    const float HopUp = 0.22f, HopDownTime = 0.2f;
    // Over the shoreline at speed it bounds in (or out) with a splash. Only looks: where you can go is unchanged.
    float shoreHopT, shoreHopCool;
    bool wasSwimming;
    const float ShoreHop = 0.3f;
    // Hoofprints in sand and snow, fading (looks only).
    readonly List<(float x, float y, float life, int dir)> trackPrints = new();

    void ToggleRide()
    {
        // R gets you on and off the boat: from beside it (E may belong to a fishing spot there), and at the helm it lands
        // (E fishes over deep water). In a storm the boat stays tied up, so if you have Tidemane, R calls it instead.
        if (eclipse != null) return;   // you stand your ground with the agong
        // The ground's shaking, or everyone's heading up to School Rise: no boats, no riding off (RestlessSea.cs).
        if (tremor != null || SeaEmergency) { Toast("Not now: up to School Rise, on foot!"); return; }
        // Leaving Baga goes by Niko's banca, and the ash is waited out in the shelter (Magayon.cs).
        if (VolcanoControlled) { Toast(BagaEvacuating ? "Not now: everyone leaves together on Niko's banca." : "Not now: help get the shelter ready."); return; }
        if (mode == "play" && Aboard) { LandBoat(); return; }
        if (mode == "play" && boss == null && BoatInReach() && !(Stormy && state.tamed)) { BoardBoat(); return; }
        if (!state.tamed || mode != "play" || boss != null) return;
        if (scene != "world") { Toast($"{Data.MountName} is waiting for you outside."); return; }
        if (state.riding) Dismount();
        else Mount();
    }

    // Close by, you climb on. From further away, a whistle calls it over (StartCall): it comes and waits beside you.
    void Mount()
    {
        if (Aboard) { Toast("Land the boat before calling Tidemane."); return; }
        if (call != null)
        {
            // Already coming: once it's close, R climbs on wherever it has got to.
            if (call.Phase is "run" or "arrive" && Dist(player.X, player.Y, call.X, call.Y) < 26) CommitCall(quiet: true);
            else { Toast($"{Data.MountName} is on its way."); return; }
        }
        if (Dist(player.X, player.Y, state.mountX, state.mountY) >= 26) { StartCall(); return; }
        FaceToward(state.mountX, state.mountY);
        // You climb up where it stands, if there's a clear way to it; otherwise it steps over to you.
        hopT = HopUp; hopDown = false; hopFromX = player.X; hopFromY = player.Y;
        if (ClearWay(player.X, player.Y, state.mountX, state.mountY)) { player.X = state.mountX; player.Y = state.mountY; }
        state.riding = true;
        state.mountX = player.X; state.mountY = player.Y;
        rideVel = (0, 0); mountSpeed = 0; mountPhase = 0; wasSwimming = Swimming; shoreHopT = 0;
        if (player.Face is "left" or "right") mountDir = player.Face == "right" ? 1 : -1;
        Sfx.Play("neigh");
        if (!state.Hinted("rideTip"))
        {
            state.hinted["rideTip"] = true;
            Toast("In the saddle! Ride into the sea to swim. <ride> hops off on dry land.", 4);
        }
        Save();
    }

    // Every step from one place to the other is somewhere you could ride (so you never climb on through a wall).
    bool ClearWay(float x0, float y0, float x1, float y1)
    {
        int n = Math.Max(1, (int)MathF.Ceiling(Dist(x0, y0, x1, y1) / 2));
        for (int i = 1; i <= n; i++)
            if (!CanStand(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, Wading, true)) return false;
        return true;
    }

    // You need dry ground (or shallows, in waders) to step down onto, right beside it.
    void Dismount()
    {
        float mx = player.X, my = player.Y;
        (float x, float y)? spot = null;
        foreach (var (ox, oy) in new[] { (-9f * mountDir, 0f), (9f * mountDir, 0f), (0f, 6f), (0f, -6f), (-9f * mountDir, 6f), (9f * mountDir, 6f), (0f, 0f) })
            if (CanStand(mx + ox, my + oy, Wading)) { spot = (mx + ox, my + oy); break; }
        if (spot is not (float sx, float sy)) { Sfx.Play("nope"); Toast("Nowhere dry to hop off here. Ride up onto the shore first."); return; }
        var seat = RiderSeat((int)MathF.Round(mx), (int)MathF.Round(my));
        state.riding = false;
        state.mountX = mx; state.mountY = my;
        player.X = sx; player.Y = sy;
        hopT = HopDownTime; hopDown = true; hopFromX = seat.x; hopFromY = seat.y;
        rideVel = (0, 0); mountSpeed = 0; shoreHopT = 0;
        Sfx.Play("pickup");
        Save();
    }

    // Going indoors, underground or out to sea by boat, you leave it waiting where you got off. A whistle still on its way
    // is called off first (it's not riding then, so this mustn't return before it).
    void LeaveMount()
    {
        call = null;
        rideVel = (0, 0); mountSpeed = 0; shoreHopT = 0; hopT = 0;
        if (!state.riding) return;
        state.riding = false;
        state.mountX = player.X; state.mountY = player.Y;
    }

    // In reach to climb on with <act> (kept short so it doesn't take E from a fishing spot; <ride> reaches 26 px).
    bool MountNear() => state.tamed && !state.riding && scene == "world" && boss == null && call == null
        && Dist(player.X, player.Y, state.mountX, state.mountY) < 16;

    // Riding: the move keys or the stick ask for a speed and a heading, and it gets there quickly but not at once. Moves
    // in steps of at most 2 px, each axis on its own, so it slides along a shore rather than sticking.
    void RideMove(float dx, float dy, float push, float dt)
    {
        bool swim = Swimming;
        float top = (swim ? 74 : 92) * push * (Starving ? 0.6f : 1f) * (Shaking ? 0.45f : 1f);
        float len = MathF.Sqrt(dx * dx + dy * dy);
        float wx = len > 0 ? dx / len * top : 0, wy = len > 0 ? dy / len * top : 0;
        float ax = wx - rideVel.x, ay = wy - rideVel.y, al = MathF.Sqrt(ax * ax + ay * ay);
        bool braking = len == 0 || rideVel.x * wx + rideVel.y * wy < 0 || wx * wx + wy * wy < rideVel.x * rideVel.x + rideVel.y * rideVel.y;
        float rate = (braking ? RideBrake : swim ? SwimAccel : RideAccel) * dt;
        rideVel = al <= rate ? (wx, wy) : (rideVel.x + ax / al * rate, rideVel.y + ay / al * rate);
        float mx = rideVel.x * dt, my = rideVel.y * dt, sx = player.X, sy = player.Y;
        int n = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(mx), MathF.Abs(my)) / 2));
        bool hitX = false, hitY = false;
        for (int i = 0; i < n; i++)
        {
            if (!hitX && mx != 0) { if (CanStand(player.X + mx / n, player.Y, Wading, true) && StepAllowed(player.X + mx / n, player.Y)) player.X += mx / n; else hitX = true; }
            if (!hitY && my != 0) { if (CanStand(player.X, player.Y + my / n, Wading, true) && StepAllowed(player.X, player.Y + my / n)) player.Y += my / n; else hitY = true; }
        }
        if (hitX) rideVel.x = 0;
        if (hitY) rideVel.y = 0;
        float moved = Dist(sx, sy, player.X, player.Y);
        mountSpeed += (moved / MathF.Max(dt, 0.001f) - mountSpeed) * MathF.Min(1, dt * 14);
        if (moved < 0.01f && len == 0) mountSpeed = MathF.Max(0, mountSpeed - dt * 300);
        // A full stride covers more ground at a gallop than at a trot.
        mountPhase += moved / (swim ? 36 : mountSpeed > 50 ? 46 : 24);
        if (len > 0)
        {
            // Facing changes view (side or end on), so it waits until one direction clearly wins (no flicker on a diagonal).
            bool sideNow = player.Face is "left" or "right";
            bool wantSide = sideNow ? MathF.Abs(dx) * 1.25f >= MathF.Abs(dy) : MathF.Abs(dx) > MathF.Abs(dy) * 1.25f;
            player.Face = wantSide ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
            heldT = 0;
            hopT = 0;
        }
        RideEffects(dt);
    }

    // Hoofbeats, dust and prints on the stride, spray in the water, and the bound over the shoreline.
    void RideEffects(float dt)
    {
        shoreHopT = MathF.Max(0, shoreHopT - dt);
        shoreHopCool = MathF.Max(0, shoreHopCool - dt);
        bool swim = Swimming;
        // Only while it's moving: loading a game afloat, or being set down in the water, makes no splash.
        if (swim != wasSwimming && mountSpeed < 10) wasSwimming = swim;
        if (swim != wasSwimming)
        {
            if (mountSpeed > 40 && shoreHopCool <= 0) { shoreHopT = ShoreHop; shoreHopCool = 0.5f; }
            if (swim)
            {
                Burst(player.X, player.Y - 2, "#cfe8ee", 10);
                Sfx.Play("splash");
            }
            else for (int i = 0; i < 6; i++)
                particles.Add(new Particle { X = player.X + (float)(fxRng.NextDouble() * 12 - 6), Y = player.Y - 6, Vx = (float)(fxRng.NextDouble() * 20 - 10), Vy = -(float)(fxRng.NextDouble() * 20 + 8), Life = 0.35f, Color = "#cfe8ee" });
            wasSwimming = swim;
        }
        var p = RidePose();
        int frames = p.Kind == "gallop" ? 8 : p.Kind is "trot" or "swim" ? 6 : 0;
        int f = frames == 0 || mountSpeed < 4 ? -1 : p.Frame;
        if (f == mountGaitFrame) return;
        mountGaitFrame = f;
        if (f < 0 || scene != "world") return;
        int dir = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0;
        if (p.Kind == "swim")
        {
            // A stroke throws up a little spray at its chest.
            if (f % 3 == 0)
                for (int i = 0; i < 3; i++)
                    particles.Add(new Particle { X = player.X + dir * 8 + (float)(fxRng.NextDouble() * 6 - 3), Y = player.Y - 5, Vx = (float)(fxRng.NextDouble() * 16 - 8) + dir * 10, Vy = -(float)(fxRng.NextDouble() * 18 + 6), Life = 0.3f, Color = "#e8f6fa" });
            return;
        }
        // A bound lands far hoof then near (da-dum), a trot one hoof at a time.
        bool contact = p.Kind == "gallop" ? f is 1 or 2 : f is 0 or 3;
        if (!contact) return;
        hoofSide ^= 1;
        Sfx.Play(hoofSide == 0 ? "hoof" : "hoof2");
        int ftx = (int)MathF.Floor(player.X / T), fty = (int)MathF.Floor((player.Y - 1.5f) / T);
        char g = FlatOpen(ftx, fty) ? 's' : TileAt(ftx, fty);
        string col = g switch { 's' or 'p' => "#d8bb7e", 'D' => "#d9a457", 'n' or 'i' => "#ffffff", 'e' => "#a19c90", 'w' or 'l' or 'o' or 'm' or 'x' => "#cfe8ee", _ => null };
        float hx = player.X + dir * 6, hy = player.Y;
        if (col != null)
            for (int i = 0; i < (p.Kind == "gallop" ? 3 : 2); i++)
                particles.Add(new Particle { X = hx + (float)(fxRng.NextDouble() * 4 - 2), Y = hy, Vx = (float)(fxRng.NextDouble() * 16 - 8) - dir * 10, Vy = -(float)(fxRng.NextDouble() * 14 + 8), Life = (float)(fxRng.NextDouble() * 0.12 + 0.18), Color = col });
        if (g is 's' or 'p' or 'D' or 'n' or 'i')
        {
            trackPrints.Add((hx, hy, 3f, dir));
            if (trackPrints.Count > 64) trackPrints.RemoveAt(0);
        }
    }

    // Every frame outside menus: the whistle's call, the climb on or off, the prints fading.
    void UpdateMount(float dt)
    {
        UpdateCall(dt);
        hopT = MathF.Max(0, hopT - dt);
        TickHoofprints(dt);
    }

    // Every frame, menus too: out of play (fishing from the saddle, a panel, the pause menu) it stands still, so it
    // doesn't set off again with the speed it had when you come back.
    void HoldMount()
    {
        if (mode is "play" or "build" && Riding) return;
        rideVel = (0, 0);
        mountSpeed = 0;
        mountGaitFrame = -1;
        // A bound over the shoreline ends too: start fishing mid-bound and it would hang in the air (Codex).
        shoreHopT = 0;
    }

    void TickHoofprints(float dt)
    {
        for (int i = trackPrints.Count - 1; i >= 0; i--)
        {
            var h = trackPrints[i];
            if ((h.life -= dt) <= 0) trackPrints.RemoveAt(i);
            else trackPrints[i] = h;
        }
    }

    // What the mount under you is doing, for the sprite and the saddle.
    MountPose RidePose()
    {
        var p = new MountPose { Saddle = true, Time = time, Seed = 0.37f, Ridden = true };
        if (shoreHopT > 0) { p.Kind = "leap"; p.Leap = 1 - shoreHopT / ShoreHop; return p; }
        if (Swimming)
        {
            p.Kind = "swim";
            p.Speed = mountSpeed / 74;
            p.Frame = mountSpeed > 4 ? (int)(mountPhase * 6) % 6 : (int)(time * 2.5f) % 6;
        }
        else if (mountSpeed > 50) { p.Kind = "gallop"; p.Frame = (int)(mountPhase * 8) % 8; }
        else if (mountSpeed > 4) { p.Kind = "trot"; p.Frame = (int)(mountPhase * 6) % 6; }
        else p.Toss = IdleToss(time, 0.37f);
        return p;
    }

    // Now and then, standing about, it tosses its head (every ten seconds or so, for half a second).
    static float IdleToss(float t, float seed)
    {
        float ph = (t + seed * 31) % 10.5f;
        return ph < 0.5f ? ph / 0.5f : 0;
    }

    // Bounding over the shoreline lifts it a few pixels.
    int RideLift() => shoreHopT > 0 ? (int)MathF.Round(MathF.Sin((1 - shoreHopT / ShoreHop) * MathF.PI) * 5) : 0;

    /* ---------- Whistling it over ---------- */
    // Called from afar it doesn't just appear under you any more: it comes. From where it's waiting if it can run or swim
    // to you from there, else up out of water you can see (it's a sea creature), else galloping in from off the screen,
    // and if it's shut out of everywhere, out of a swirl of sea foam beside you. It stops beside you and waits for
    // <ride>. None of this is saved: state.mountX/Y stay where it was until it arrives (so a save mid-call loads with it
    // where it was), and anything that takes you elsewhere calls it off (LeaveMount, StartGame, and UpdateCall itself).
    sealed class MountCall
    {
        public string Phase = "whistle", Arrival = "run", Face = "right";
        public float X, Y, T, Total, Stride, Lift, FromX, FromY, ToX, ToY, Replan, Speed;
        public int Dir = 1;
        public bool Storm;   // whether it was stormy when the way was worked out (a storm coming on means another way)
        public readonly List<(float x, float y)> Path = new();
    }
    MountCall call;
    float whistleT;
    const float CallLimit = 3.4f, CallRun = 125, CallSwim = 95, CallWhistle = 0.35f;
    const int CallReach = 26;   // tiles of travel it will cover

    void StartCall()
    {
        call = new MountCall { X = state.mountX, Y = state.mountY };
        whistleT = 0.7f;
        Sfx.Play("whistle");
        PlanCall(true);
    }

    // Where it may go on the way: ground it can gallop on or water it can swim, not a closed bridge or anything solid,
    // and in a storm not the deep sea (it won't head out into that, as when you ride it).
    bool CallTileOk(int tx, int ty, HashSet<(int, int)> blocked)
    {
        char t = TileAt(tx, ty);
        if (!(Walkable(t) || SwimOk(tx, ty, t)) || BridgeClosed(tx, ty) || blocked.Contains((tx, ty))) return false;
        return !(Stormy && t == '~');
    }

    // Where it can stand: the same footprint as yours, any water, and the storm rule judged by its own middle.
    bool CallCanBe(float x, float y)
    {
        foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
        {
            int tx = (int)MathF.Floor(ax / T), ty = (int)MathF.Floor(ay / T);
            char t = TileAt(tx, ty);
            if (!(Walkable(t) || SwimOk(tx, ty, t)) || BridgeClosed(tx, ty)) return false;
        }
        if (Stormy && TileUnder(x, y) == '~') return false;
        foreach (var r in Solids())
            if (x + 3 > r.X && x - 3 < r.X + r.W && y > r.Y && y - 3 < r.Y + r.H) return false;
        return true;
    }

    static int CallTX(float x) => (int)MathF.Floor(x / T);
    static int CallTY(float y) => (int)MathF.Floor((y - 1.5f) / T);

    // Steps from you to every tile within reach, eight ways (diagonals only round corners it could cut).
    Dictionary<(int, int), int> CallField(int px, int py, HashSet<(int, int)> blocked)
    {
        var dist = new Dictionary<(int, int), int> { [(px, py)] = 0 };
        var q = new Queue<(int, int)>();
        q.Enqueue((px, py));
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            int d = dist[(x, y)];
            if (d >= CallReach) continue;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = x + ox, ny = y + oy;
                    if (dist.ContainsKey((nx, ny)) || !CallTileOk(nx, ny, blocked)) continue;
                    if (ox != 0 && oy != 0 && (!CallTileOk(x + ox, y, blocked) || !CallTileOk(x, y + oy, blocked))) continue;
                    dist[(nx, ny)] = d + 1;
                    q.Enqueue((nx, ny));
                }
        }
        return dist;
    }

    // Tiles a solid thing stands on (houses, landmarks, people): it goes round them.
    HashSet<(int, int)> CallBlocked()
    {
        var set = new HashSet<(int, int)>();
        foreach (var r in Solids())
            for (int ty = (int)MathF.Floor(r.Y / T); ty <= (int)MathF.Floor((r.Y + r.H) / T); ty++)
                for (int tx = (int)MathF.Floor(r.X / T); tx <= (int)MathF.Floor((r.X + r.W) / T); tx++)
                {
                    // Only if the box covers the tile's middle, so a fence post doesn't wall off a whole tile row.
                    float cx = tx * T + T / 2f, cy = ty * T + T / 2f;
                    if (cx + 2 > r.X && cx - 2 < r.X + r.W && cy + 2 > r.Y && cy - 2 < r.Y + r.H) set.Add((tx, ty));
                }
        return set;
    }

    // Beside you, where it will stop: on the side it's coming from if there's room, else a little behind you (it's
    // tall: stood just in front of you, it would hide you), and in front only with a good gap. Null when there's no room
    // anywhere round you (Codex: it used to stop in the wall then); then it comes to where you stand, if that's open.
    (float x, float y)? CallStop(float fromX)
    {
        float s = fromX < player.X ? -1 : 1;
        foreach (var (ox, oy) in new[] { (13 * s, 0f), (-13 * s, 0f), (15 * s, -3f), (-15 * s, -3f), (12 * s, -7f), (-12 * s, -7f), (0f, -11f), (14 * s, 6f), (-14 * s, 6f), (0f, 18f) })
            if (CallCanBe(player.X + ox, player.Y + oy)) return (player.X + ox, player.Y + oy);
        return CallCanBe(player.X, player.Y) ? (player.X, player.Y) : null;
    }

    // Its footprint, at every point along a straight stretch, somewhere it can be (2 px steps; solids fetched once).
    bool CallClear(float x0, float y0, float x1, float y1, HashSet<(int, int)> blocked, List<Box> solids)
    {
        int n = Math.Max(1, (int)MathF.Ceiling(Dist(x0, y0, x1, y1) / 2));
        for (int i = 1; i <= n; i++)
        {
            float x = x0 + (x1 - x0) * i / n, y = y0 + (y1 - y0) * i / n;
            foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
                if (!CallTileOk((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T), blocked)) return false;
            foreach (var r in solids)
                if (x + 3 > r.X && x - 3 < r.X + r.W && y > r.Y && y - 3 < r.Y + r.H) return false;
        }
        return true;
    }

    // Works out the way to you. The first time, also where it comes from. The search runs out from where it will stop,
    // so the way it takes ends there.
    void PlanCall(bool first)
    {
        var c = call;
        var blocked = CallBlocked();
        var solids = Solids().ToList();
        c.Storm = Stormy;
        if (CallStop(first ? state.mountX : c.X) is not (float tx, float ty))
        {
            // Nowhere at all to stand beside you: it can't come here.
            call = null;
            Toast($"There's no room for {Data.MountName} here. Whistle again from somewhere more open.");
            return;
        }
        (c.ToX, c.ToY) = (tx, ty);
        int stx = CallTX(tx), sty = CallTY(ty);
        var dist = CallField(stx, sty, blocked);
        // In view: what the camera can see now, not just "near you" (Codex: near a map edge they differ).
        bool OnScreen((int x, int y) k) => k.x * T + 5 > camX - 12 && k.x * T + 5 < camX + W + 12 && k.y * T + 5 > camY - 12 && k.y * T + 5 < camY + H + 24;
        (int, int) start;
        if (first)
        {
            var mt = (CallTX(state.mountX), CallTY(state.mountY));
            (float x, float y) toward = (state.mountX - player.X, state.mountY - player.Y);
            float tl = MathF.Max(1, MathF.Sqrt(toward.x * toward.x + toward.y * toward.y));
            float Away((int x, int y) k) { float dx = k.x * T + 5 - player.X, dy = k.y * T + 5 - player.Y, l = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy)); return 1 - (dx * toward.x + dy * toward.y) / (l * tl); }
            if (dist.ContainsKey(mt) && CallCanBe(state.mountX, state.mountY)) { c.Arrival = "run"; start = mt; }
            else
            {
                // Water in view, a fair way off: it rises out of that.
                var water = dist.Where(kv => kv.Value >= 3 && Swimmable(TileAt(kv.Key.Item1, kv.Key.Item2)) && OnScreen(kv.Key))
                    .OrderBy(kv => MathF.Abs(kv.Value - 8) + Away(kv.Key) * 2).Select(kv => kv.Key).ToList();
                // Otherwise across land from somewhere you can't see, from its side of the island if it can. If every
                // tile it could come from is in view, it doesn't pop up on one: it forms out of sea foam beside you.
                var far = water.Count > 0 ? water : dist.Where(kv => kv.Value >= 4 && !OnScreen(kv.Key))
                    .OrderBy(kv => Away(kv.Key) * 6 - kv.Value * 0.2f).Select(kv => kv.Key).ToList();
                if (far.Count > 0) { c.Arrival = water.Count > 0 ? "breach" : "run"; start = far[0]; }
                else { c.Arrival = "foam"; start = (stx, sty); }
                c.X = start.Item1 * T + T / 2f; c.Y = start.Item2 * T + T / 2f + 3;
            }
        }
        else
        {
            start = (CallTX(c.X), CallTY(c.Y));
            if (!dist.ContainsKey(start)) { c.Phase = "foam"; c.T = 0; c.Path.Clear(); return; }
        }
        // Downhill through the field to where it stops, never cutting a corner the search wouldn't (Codex: it could
        // take a diagonal past a tree), then pull the way tight wherever its whole footprint fits the straight line.
        c.Path.Clear();
        var at = start;
        for (int guard = 0; guard < 80 && dist.TryGetValue(at, out int d) && d > 0; guard++)
        {
            (int, int) next = at;
            int best = d;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    var n = (at.Item1 + ox, at.Item2 + oy);
                    if (!dist.TryGetValue(n, out int nd) || nd >= best) continue;
                    if (ox != 0 && oy != 0 && (!CallTileOk(at.Item1 + ox, at.Item2, blocked) || !CallTileOk(at.Item1, at.Item2 + oy, blocked))) continue;
                    best = nd; next = n;
                }
            if (next == at) break;
            at = next;
            c.Path.Add((at.Item1 * T + T / 2f, at.Item2 * T + T / 2f + 3));
        }
        if (c.Path.Count > 0) c.Path.RemoveAt(c.Path.Count - 1);   // the stop's own tile: it goes to the stop itself
        c.Path.Add((c.ToX, c.ToY));
        for (int i = 0; i + 1 < c.Path.Count;)
        {
            var (ax0, ay0) = i == 0 ? (c.X, c.Y) : c.Path[i - 1];
            var (bx1, by1) = c.Path[i + 1];
            if (CallClear(ax0, ay0, bx1, by1, blocked, solids)) c.Path.RemoveAt(i); else i++;
        }
        c.Replan = 0.3f;
    }

    void UpdateCall(float dt)
    {
        whistleT = MathF.Max(0, whistleT - dt);
        var c = call;
        if (c == null) return;
        if (!state.tamed || state.riding || scene != "world" || boss != null || eclipse != null || tremor != null || SeaEmergency || VolcanoControlled || Aboard) { call = null; return; }
        if (Array.IndexOf(ActiveModes, mode) < 0) return;
        c.T += dt; c.Total += dt;
        switch (c.Phase)
        {
            case "whistle":
                if (c.T < CallWhistle) break;
                c.Phase = c.Arrival; c.T = 0;
                if (c.Phase == "breach")
                {
                    c.FromX = c.X; c.FromY = c.Y;
                    Burst(c.X, c.Y - 2, "#cfe8ee", 14);
                    Burst(c.X, c.Y - 6, "#ffffff", 6);
                    Sfx.Play("splash");
                    Sfx.Play("neigh");
                }
                else if (c.Phase == "run") Sfx.Play("neigh");
                break;
            case "breach":
            {
                // Up out of the water in an arc, and down again a little nearer you.
                float k = MathF.Min(1, c.T / 0.6f);
                c.Lift = MathF.Sin(k * MathF.PI) * 14;
                if (c.Path.Count > 0 && k < 1)
                {
                    var (nx, ny) = c.Path[0];
                    float dx = nx - c.FromX, dy = ny - c.FromY, l = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
                    float go = MathF.Min(l, 14) * k;
                    c.X = c.FromX + dx / l * go; c.Y = c.FromY + dy / l * go;
                    CallFace(dx, dy);
                }
                if (k < 1) break;
                c.Lift = 0;
                Burst(c.X, c.Y - 2, Swimmable(TileUnder(c.X, c.Y)) ? "#cfe8ee" : "#e8cf96", 10);
                Sfx.Play("splash");
                c.Phase = "run"; c.T = 0;
                break;
            }
            case "run":
            {
                if (c.Total > CallLimit) { c.Phase = "foam"; c.T = 0; break; }
                // You've moved off, or a storm has come on (or blown over) since it set out: another way.
                if ((c.Replan -= dt) <= 0 && Dist(player.X, player.Y, c.ToX, c.ToY) > 16 || Stormy != c.Storm) { PlanCall(false); if (call == null || c.Phase != "run") break; }
                bool swim = Swimmable(TileUnder(c.X, c.Y));
                float left = Dist(c.X, c.Y, c.ToX, c.ToY);
                float sp = (swim ? CallSwim : CallRun) * Math.Clamp(left / 18, 0.4f, 1) * dt;
                float sx = c.X, sy = c.Y;
                while (sp > 0 && c.Path.Count > 0)
                {
                    var (nx, ny) = c.Path[0];
                    float dx = nx - c.X, dy = ny - c.Y, l = MathF.Sqrt(dx * dx + dy * dy);
                    if (l <= sp) { c.X = nx; c.Y = ny; sp -= l; c.Path.RemoveAt(0); continue; }
                    c.X += dx / l * sp; c.Y += dy / l * sp; sp = 0;
                }
                float mdx = c.X - sx, mdy = c.Y - sy, moved = MathF.Sqrt(mdx * mdx + mdy * mdy);
                c.Speed = moved / MathF.Max(dt, 0.001f);
                c.Stride += moved / (swim ? 36 : 46);
                if (moved > 0.01f) CallFace(mdx, mdy);
                if (c.Path.Count > 0) break;
                // There: it pulls up beside you, rears and calls.
                c.Phase = "arrive"; c.T = 0; c.Speed = 0;
                CallFaceTo(player.X, player.Y);
                if (!swim) { Burst(c.X + c.Dir * 4, c.Y, "#e8cf96", 6); Sfx.Play("neigh"); }
                break;
            }
            case "foam":
                // Shut out of everywhere (or too slow): sea foam swirls up beside you and it steps out of it.
                if (c.T < 0.05f || c.T >= 0.7f && Dist(player.X, player.Y, c.ToX, c.ToY) > 16)
                {
                    // Where you are now (it was a moment ago if you've walked on while it formed).
                    if (CallStop(player.X + 1) is not (float fx, float fy)) { call = null; break; }
                    if (c.T < 0.05f) Sfx.Play("splash");
                    (c.ToX, c.ToY) = (fx, fy);
                }
                if ((int)(c.T * 30) % 2 == 0)
                {
                    float a = c.T * 14, r = 9 - c.T * 9;
                    particles.Add(new Particle { X = c.ToX + MathF.Cos(a) * r * 1.4f, Y = c.ToY - 4 + MathF.Sin(a) * r * 0.6f, Vx = 0, Vy = -14, Life = 0.4f, Color = "#e8f6fa" });
                }
                if (c.T < 0.7f) break;
                c.X = c.ToX; c.Y = c.ToY; c.Path.Clear();
                Burst(c.X, c.Y - 6, "#ffffff", 14);
                Sfx.Play("neigh");
                c.Phase = "arrive"; c.T = 0;
                CallFaceTo(player.X, player.Y);
                break;
            case "arrive":
                // Walked on while it reared: it comes after you again (Codex: it used to be left behind).
                if (Dist(player.X, player.Y, c.ToX, c.ToY) > 16) { c.Phase = "run"; c.T = 0; PlanCall(false); break; }
                if (c.T >= 0.6f) CommitCall();
                break;
        }
    }

    void CallFace(float dx, float dy)
    {
        var c = call;
        if (MathF.Abs(dx) > 0.01f) c.Dir = dx > 0 ? 1 : -1;
        bool side = c.Face is "left" or "right";
        bool wantSide = side ? MathF.Abs(dx) * 1.25f >= MathF.Abs(dy) : MathF.Abs(dx) > MathF.Abs(dy) * 1.25f;
        c.Face = wantSide ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
    }

    void CallFaceTo(float x, float y)
    {
        var c = call;
        c.Dir = x < c.X ? -1 : 1;
        c.Face = MathF.Abs(x - c.X) >= MathF.Abs(y - c.Y) ? (c.Dir > 0 ? "right" : "left") : (y > c.Y ? "down" : "up");
    }

    // It's here: it waits where it stopped (and that's saved).
    void CommitCall(bool quiet = false)
    {
        var c = call;
        if (c == null) return;
        state.mountX = c.X; state.mountY = c.Y;
        call = null;
        if (!quiet && !state.Hinted("callTip"))
        {
            state.hinted["callTip"] = true;
            Toast($"{Data.MountName} came when you whistled. <ride> climbs on.", 4);
        }
        Save();
    }

    /* ---------- Drawing ---------- */
    // Tidemane with its feet at (x, y), lifted off the ground by lift (a leap). Its shadow stays on the ground.
    void DrawTidemane(float fx, float fy, int dir, MountPose p, float lift = 0)
    {
        int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy), up = (int)MathF.Round(lift);
        bool swim = p.Kind == "swim";
        if (!swim) DrawMountShadow(x, y, up);
        MountArt.BuildSide(mountCanvas, p);
        BlitMount(mountCanvas, x, y - up, dir, p.Flash, swim ? y - 6 : int.MaxValue);
        if (swim) DrawSwimWater(x, y - 6, dir, p.Speed > 0.15f);
    }

    void DrawMountShadow(int x, int y, int lift)
    {
        int w = Math.Max(8, 24 - lift);
        pix.Rect(x - w / 2 - 2, y, w, 1, Pal.Rgba(16, 40, 44, 0.28f));
        pix.Rect(x - w / 2, y + 1, w - 4, 1, Pal.Rgba(16, 40, 44, 0.14f));
    }

    // Floating, it rises and settles a pixel on the swell (the rider goes with it).
    int FloatBob(float fx) => MathF.Sin(time * 2.2f + fx * 0.07f) > 0.3f ? 1 : 0;

    // The surface around a swimming Tidemane, at row wl (what's under it was left to the sea by BlitMount). Broken foam
    // where its body and tail cut the surface; moving, a bow wave and a wake fanning out behind; resting, little ripples
    // spreading from either end. Every touch of foam is on water only.
    void DrawSwimWater(int x, int wl, int dir, bool moving)
    {
        void OnWater(int px, int py, Color c)
        {
            if (px >= 0 && py >= 0 && px < PW && py < PH && Wet(ShapePx(px, py))) pix.Rect(px, py, 1, 1, c);
        }
        int lap = (int)(time * 5);
        var foam = Pal.Rgba(232, 246, 250, 0.65f);
        var foamDim = Pal.Rgba(232, 246, 250, 0.3f);
        for (int ox = -22; ox <= 11; ox++)
        {
            int px = x + dir * ox;
            double h = Pix.Hash((px >> 1) + lap, 7, 260);
            if (h < 0.6) OnWater(px, wl, foam);
            else if (h < 0.75) OnWater(px, wl + 1, foamDim);
        }
        if (moving)
        {
            for (int i = 0; i < 3; i++) OnWater(x + dir * (11 + i), wl - (lap + i) % 2, foam);
            // The wake: two arms opening out behind it in a V.
            for (int k = 1; k <= 9; k++)
            {
                if ((k + lap) % 4 == 0) continue;
                var c = Pal.Rgba(232, 246, 250, 0.55f * (1 - k / 10f));
                int px = x - dir * (24 + k * 2), spread = 1 + k * 3 / 4;
                OnWater(px, wl - spread, c); OnWater(px - dir, wl - spread, c);
                OnWater(px, wl + spread, c); OnWater(px - dir, wl + spread, c);
            }
            return;
        }
        float ph = time * 0.7f % 1;
        var ring = Pal.Rgba(232, 246, 250, 0.45f * (1 - ph));
        foreach (int side in new[] { -1, 1 })
        {
            int cx = x + side * (int)(15 + ph * 8);
            OnWater(cx - 1, wl, ring); OnWater(cx, wl + 1, ring); OnWater(cx + 1, wl, ring);
        }
    }

    // Front and back: the tail and chest (or, going away, the head and croup) are drawn, then the rider, then whatever is
    // nearer you than the rider (the head coming toward you, the tail going away).
    void DrawTidemaneEndOn(int x, int y, bool away, MountPose p, Action rider = null, int lift = 0, bool legs = true)
    {
        bool swim = p.Kind == "swim";
        int wl = swim ? y - 6 : int.MaxValue;
        if (!swim) DrawMountShadow(x, y, lift);
        MountArt.BuildEnd(mountCanvas, p, away, false);
        BlitMount(mountCanvas, x, y - lift, 1, p.Flash, wl);
        if (rider != null)
        {
            rider();
            // The rider's legs go down either side of it (not while they're still climbing up: Codex).
            var (sx, sy) = MountArt.SeatEnd(p, away);
            if (legs)
            {
                CoverLegs(x, y - lift, 1, wl, x + sx - 3, y - lift + sy - 3, x + sx + 3, y - lift + sy - 1);
                RiderLegsEnd(x + sx, y - lift + sy);
            }
        }
        MountArt.BuildEnd(mountCanvas, p, away, true);
        BlitMount(mountCanvas, x, y - lift, 1, p.Flash, wl);
        if (swim) DrawEndOnSwimWater(x, y, away, p.Speed > 0.15f);
    }

    // Puts the mount's own pixels back over the rider's legs (the canvas still holds what was just drawn).
    void CoverLegs(int x, int y, int dir, int wl, int x0, int y0, int x1, int y1) => BlitMount(mountCanvas, x, y, dir, false, wl, (x0, y0, x1, y1));

    void DrawEndOnSwimWater(int x, int y, bool away, bool moving)
    {
        bool IsWater(int px, int py) => px >= 0 && py >= 0 && px < PW && py < PH && Wet(ShapePx(px, py));
        void Foam(int px, int py, float alpha)
        {
            if (IsWater(px, py)) pix.Rect(px, py, 1, 1, Pal.Rgba(232, 246, 250, alpha));
        }
        int lap = (int)(time * 5), ahead = away ? -1 : 1, wl = y - 6;
        for (int k = -7; k <= 7; k++)
            if ((k + lap) % 3 != 0) Foam(x + k, wl + Math.Abs(k) / 3, .6f);
        for (int k = 0; k < 8; k++)
        {
            int spread = moving ? 7 + k : 7 + (int)(time * 3 % 4);
            int wy = wl - ahead * (9 + k * 2);
            if ((k + lap) % 4 == 0) continue;
            Foam(x - spread, wy, .48f * (1 - k / 8f));
            Foam(x + spread, wy, .48f * (1 - k / 8f));
        }
    }

    // The saddle is the common anchor for the rider, their rod and anything held overhead.
    (int x, int y) RiderSeat(int x, int y)
    {
        var p = RidePose();
        int lift = RideLift();
        if (player.Face is "up" or "down")
        {
            var (ex, ey) = MountArt.SeatEnd(p, player.Face == "up");
            return (x + ex, y + ey - lift);
        }
        var (sx, sy) = MountArt.Seat(p);
        int dir = player.Face == "left" ? -1 : 1;
        return (dir > 0 ? x + sx : x - sx, y + sy - lift);
    }

    // You in the saddle, sitting up over its back with a leg down its side.
    void DrawRider(int x, int y, int arms = 0, int pump = 0, HandPose hands = null, int lean = 0)
    {
        if (player.Face is "left" or "right") mountDir = player.Face == "right" ? 1 : -1;
        var p = RidePose();
        int lift = RideLift();
        var seat = RiderSeat(x, y);
        // Swinging up into the saddle: from where you stood, in a little arc.
        if (hopT > 0 && !hopDown)
        {
            float k = 1 - hopT / HopUp;
            seat = ((int)MathF.Round(hopFromX + (seat.x - hopFromX) * k), (int)MathF.Round(hopFromY + (seat.y - hopFromY) * k - MathF.Sin(k * MathF.PI) * 5));
        }
        void Rider() => LookData.DrawPerson(pix, state.look, seat.x, seat.y, player.Face, 0, shadow: false,
            blink: time % 3.7f < 0.12f, arms: arms, swing: pump, lean: lean, hand: hands?.Hand, hand2: hands?.Hand2, armsBehind: hands?.ArmsBehind == true);
        if (player.Face is "up" or "down")
        {
            DrawTidemaneEndOn(x, y, player.Face == "up", p, Rider, lift, legs: hopT <= 0 || hopDown);
            return;
        }
        bool swim = p.Kind == "swim";
        int wl = swim ? y - 6 : int.MaxValue;
        if (!swim) DrawMountShadow(x, y, lift);
        MountArt.BuildSide(mountCanvas, p);
        BlitMount(mountCanvas, x, y - lift, mountDir, false, wl);
        Rider();
        if (hopT <= 0 || hopDown)
        {
            CoverLegs(x, y - lift, mountDir, wl, seat.x - 3, seat.y - 3, seat.x + 3, seat.y - 1);
            RiderLegSide(seat.x, seat.y, mountDir);
        }
        if (swim) DrawSwimWater(x, y - 6, mountDir, p.Speed > 0.15f);
    }

    // Side on, the near leg bends over its flank: thigh forward along the saddle, shin down, a boot against its side.
    void RiderLegSide(int x, int y, int dir)
    {
        string pants = LookData.Pants[state.look.pants % LookData.Pants.Length];
        void S(int dx, int dy, int w, int h, string c) => pix.Rect(dir > 0 ? x + dx : x - dx - w, y + dy, w, h, c);
        S(-2, -3, 4, 1, pants);
        S(1, -2, 1, 2, pants);
        S(1, 0, 2, 1, "#2e2420");
    }

    // End on, both legs hang down either side.
    void RiderLegsEnd(int x, int y)
    {
        string pants = LookData.Pants[state.look.pants % LookData.Pants.Length];
        foreach (int s in new[] { -1, 1 })
        {
            int lx = s < 0 ? x - 4 : x + 3;
            pix.Rect(lx, y - 3, 1, 3, pants);
            pix.Rect(lx + (s < 0 ? -1 : 0), y, 2, 1, "#2e2420");
        }
    }

    // Tidemane waiting for you, or on its way after a whistle: it shifts its weight, flicks its tail and now and then
    // tosses its head.
    void DrawMountIdle()
    {
        if (!state.tamed || state.riding || boss != null) return;
        var c = call;
        float mx = c != null && c.Phase != "whistle" ? c.X : state.mountX, my = c != null && c.Phase != "whistle" ? c.Y : state.mountY;
        if (c != null && c.Phase == "foam") return;
        var p = new MountPose { Saddle = true, Time = time, Seed = 0.37f };
        bool swim = Swimmable(TileUnder(mx, my));
        string face;
        int dir;
        if (c != null && c.Phase is "run" or "breach" or "arrive")
        {
            face = c.Face; dir = c.Dir;
            if (c.Phase == "breach") { p.Kind = "leap"; p.Leap = MathF.Min(1, c.T / 0.6f); }
            else if (c.Phase == "arrive") { if (swim) p.Kind = "swim"; else { p.Kind = "rear"; p.Frame = (int)(c.T * 6) % 2; } }
            else if (swim) { p.Kind = "swim"; p.Speed = 1; p.Frame = (int)(c.Stride * 6) % 6; }
            else { p.Kind = c.Speed > 50 ? "gallop" : "trot"; p.Frame = (int)(c.Stride * (p.Kind == "gallop" ? 8 : 6)) % (p.Kind == "gallop" ? 8 : 6); }
        }
        else
        {
            if (swim) { p.Kind = "swim"; p.Frame = (int)(time * 2.5f) % 6; }
            else p.Toss = IdleToss(time, 0.37f) + (c != null ? 0.5f : 0);
            bool vertical = Math.Abs(player.Y - my) > Math.Abs(player.X - mx);
            dir = player.X < mx ? -1 : 1;
            // It faces you: north of it, you see its back as it looks up at you.
            face = vertical ? (player.Y < my ? "up" : "down") : (dir > 0 ? "right" : "left");
        }
        int lift = c?.Phase == "breach" ? (int)MathF.Round(c.Lift) : 0;
        int ix = (int)MathF.Round(mx), iy = (int)MathF.Round(my);
        if (c?.Phase == "breach")
        {
            // Rings where it broke the surface.
            float ph = MathF.Min(1, c.T / 0.6f);
            pix.Ring(c.FromX, c.FromY - 2, 3 + ph * 8, Pal.Rgba(232, 246, 250, 0.7f * (1 - ph)));
        }
        if (face is "up" or "down") DrawTidemaneEndOn(ix, iy, face == "up", p, null, lift);
        else DrawTidemane(mx, my, dir, p, lift);
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
        // No flashing once it has calmed down (the last blow would otherwise keep it white through the dialogue).
        var p = new MountPose { Time = t, Seed = 0.11f, Flash = b.Hurt > 0.25f && b.Phase != "calm" };
        switch (b.Phase)
        {
            case "emerge" or "surface": p.Kind = "leap"; p.Leap = MathF.Min(1, b.T / 0.6f); break;
            case "dive": p.Kind = "leap"; p.Leap = MathF.Min(1, b.T / 0.5f); break;
            case "rear" or "stompRear": p.Kind = "rear"; p.Frame = (int)(t * 7) % 2; break;
            case "charge": p.Kind = "gallop"; p.Frame = (int)(t * 18) % 8; break;
            case "stalk": p.Kind = "trot"; p.Frame = (int)(t * 10) % 6; break;
            case "calm": p.Kind = "lie"; break;
            default: p.Kind = "stand"; p.Toss = b.Phase == "winded" ? 0 : IdleToss(t, 0.11f); break;
        }
        // Shaking while it rears, so you can see something's coming.
        float jx = b.Phase is "rear" or "stompRear" ? MathF.Sin(t * 60) : 0;
        DrawTidemane(b.X + jx, b.Y, b.Dir, p, b.Lift);
        if (b.Phase == "winded")
        {
            // Little stars circling its head.
            for (int i = 0; i < 3; i++)
            {
                float a = t * 5 + i * MathF.Tau / 3;
                pix.Rect(b.X + b.Dir * 11 + MathF.Cos(a) * 6, b.Y - 30 + MathF.Sin(a) * 2, 1, 1, "#ffd76a");
            }
        }
        if (b.Phase is "rear" or "stompRear")
        {
            // A "!" over its head.
            int ex = (int)MathF.Round(b.X + b.Dir * 6), ey = (int)MathF.Round(b.Y) - 44;
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

    // On the ground under everything: fading trackPrints, and the whistle's note over your head. Also clears last
    // frame's glints (BlitMount collects them again as it draws).
    void DrawMountGround()
    {
        mountGlints.Clear();
        foreach (var (hx, hy, life, dir) in trackPrints)
        {
            var c = Pal.Rgba(60, 44, 24, 0.22f * Math.Min(1, life / 1.5f));
            pix.Rect(hx - 1, hy, 2, 1, c);
            pix.Rect(hx - 1 - dir * 3, hy + 1, 2, 1, c);
        }
    }

    // A note rising from you as you whistle.
    void DrawWhistle()
    {
        if (whistleT <= 0 || Riding) return;
        float k = 1 - whistleT / 0.7f;
        int x = (int)MathF.Round(player.X + 4 + k * 2), y = (int)MathF.Round(player.Y - 19 - k * 6);
        var c = Pal.Rgba(255, 255, 255, 1 - k * k);
        var o = Pal.Rgba(16, 36, 58, 0.8f * (1 - k * k));
        pix.Rect(x - 1, y + 1, 4, 3, o); pix.Rect(x, y - 3, 3, 5, o);
        pix.Rect(x + 1, y - 2, 1, 4, c); pix.Rect(x, y + 1, 2, 2, c); pix.Rect(x + 2, y - 2, 1, 1, c);
    }

    // Once everything on the ground is drawn: a speckle or eye that something has since covered (a palm, a hut, the
    // player) no longer counts, so it can't glow through it after dark (Codex).
    void CullMountGlints()
    {
        mountGlints.RemoveAll(g =>
        {
            int sx = g.x - pix.CamX, sy = g.y - pix.CamY;
            if (sx < 0 || sy < 0 || sx >= W || sy >= H) return true;
            var c = pix.Buf[sy * W + sx];
            return c.R != g.col.R || c.G != g.col.G || c.B != g.col.B;
        });
    }

    // After dark its speckles and eyes glow (DrawNight, after the dark is laid down), with a faint sea-green light
    // round it.
    void GlowMount(float k)
    {
        if (mountGlints.Count == 0) return;
        float sx = 0, sy = 0;
        foreach (var (x, y, eye, _) in mountGlints)
        {
            sx += x; sy += y;
            pix.Glow(x + 0.5, y + 0.5, eye ? 3 : 2.5, eye ? Pal.Rgba(255, 215, 106, 0.55f * k) : Pal.Rgba(255, 242, 196, 0.4f * k));
            pix.Rect(x, y, 1, 1, eye ? Pal.Rgba(255, 225, 130, k) : Pal.Rgba(255, 246, 214, 0.9f * k));
        }
        pix.Glow(sx / mountGlints.Count + 0.5, sy / mountGlints.Count + 0.5, 11, Pal.Rgba(120, 230, 220, 0.11f * k));
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
