using Raylib_cs;

namespace Fesh;

// Platejaw out of the Ancient pool. Phase is "rise" (surfacing in the middle of the pool), "prowl" (circling under the
// surface towards you), "aim" then "lunge" (out of the water across the stone), "beached" or "stunned" (stranded on the
// stone, or dazed against a pillar: the only times a blow gets past its armour), "slide" (back into the water),
// "snapRear" and "snap" (a bite at anyone standing at the edge), "slam" (it rams the side of the pool and rocks fall),
// "surgeRear" and "surge" (a wave rolls out of the pool; a pillar breaks it) and "sink" (beaten, going back down).
sealed class Guardian
{
    public string Phase = "rise";
    public float X, Y, T, Spirit, Hurt, Wait, Ang, AimX, AimY, Ran, FromX, FromY, ToX, ToY, Wave = -1, EdgeT;
    public int Dir = 1, Attacks;
    public bool Hit;   // already hurt you with this lunge, bite or wave
}
sealed class Rock { public float X, Y, T; public bool Down; }

// The guardian of the Ancient pool (1.19). Made up for Fesh, it's based on Dunkleosteus, an armoured fish (a placoderm)
// that died out about 359 million years ago, when the coelacanths' ancestors were young. It rises as you land the
// coelacanth (when its legend card closes) and, until you've driven it off, whenever you knock on the carved stone by the
// pool, so a lost fight, or a save from before 1.19 that already has the coelacanth, can always try again. The coelacanth
// is landed before the fight, so it's yours whatever happens. It's never saved: a save underground wakes at the mouth.
partial class Game
{
    Guardian guardian;
    readonly List<Rock> rocks = new();
    bool guardianDue;                     // the coelacanth's card has closed: it rises on the next frame of play
    List<(float x, float y)> pillarSpots; // the Ancient Floor's pillars, for the wave (made on rising)
    bool guardianUndrawn;                 // the autotest renders a frame without its sprite (lit the same) to find it
    public const string GuardianName = "Platejaw";
    const float GuardSpirit = 120, LungeRange = 70, RockFall = 1.15f, WaveStart = 58, WaveSpeed = 85, WaveEnd = 240, GuardLeash = 200;
    const int LungeHurt = 18, SnapHurt = 12, RockHurt = 10, WaveHurt = 14;
    // South of the pool, far enough back that it never gets in the way of fishing from the south shore.
    const float RuneStoneX = 305, RuneStoneY = 226;
    // A falling rock hurts anyone whose feet are inside this oval round where it lands, the same oval its warning draws.
    const float RockRX = 7, RockRY = 4.5f;
    // The pillars stand on an ellipse 11 tiles across and 7 down, so the wave is measured with y stretched by 11/7.
    const float WaveSquash = 11f / 7f;

    bool GuardianBeaten => state.Hinted("platejaw");
    bool GuardianMet => state.Hinted("platejawMet");
    bool GuardianFighting => guardian != null && guardian.Phase != "sink";
    bool CoelacanthCaught => state.commons.GetValueOrDefault("ancient_coelacanth") > 0;
    bool CanWakeGuardian => OnAncientFloor && CoelacanthCaught && !GuardianBeaten && guardian == null;

    /* ---------- Waking it ---------- */
    void GuardianRise(bool fromCatch)
    {
        // The cave's own monsters want no part of it.
        foreach (var m in monsters) if (!m.Dead) Burst(m.X, m.Y - 4, MonsterKinds[m.Kind].Color, 6);
        monsters.Clear();
        rocks.Clear();
        pillarSpots = new();
        for (int y = 0; y < CaveH; y++)
            for (int x = 0; x < CaveW; x++)
                if (map[y, x] == 'Q') pillarSpots.Add((x * T + 5, y * T + 5));
        float a = MathF.Atan2(player.Y - AncientPoolY, player.X - AncientPoolX);
        guardian = new Guardian { X = AncientPoolX, Y = AncientPoolY, Spirit = GuardSpirit, Ang = a, Dir = player.X < AncientPoolX ? -1 : 1 };
        quake = 0.6f;
        Sfx.Play("splash");
        Sfx.Play("stomp");
        Burst(AncientPoolX, AncientPoolY, "#9fd8e0", 18);
        Burst(AncientPoolX, AncientPoolY - 4, "#ffffff", 8);
        bool first = !GuardianMet;
        state.hinted["platejawMet"] = true;
        Toast(first && fromCatch
                ? $"As the coelacanth comes out, the black water heaves. A head in armour, as big as a rowing boat, rises: the fish carved on every stone down here. {GuardianName}! Keep off the edge, dodge its lunges, and strike it when it strands itself on the stone."
            : first ? $"The knocks carry through the water... A head in armour, as big as a rowing boat, rises out of the pool: {GuardianName}! Keep off the edge, dodge its lunges, and strike it when it strands itself on the stone."
            : $"{GuardianName} rises out of the pool. Dodge its lunges, and strike it when it strands itself on the stone.", 7);
        Save();
    }

    // You walked off (down the passage to the ladder, or far across the cavern): it loses interest and sinks back.
    void GuardianRetreat()
    {
        if (guardian == null) return;
        Burst(guardian.X, guardian.Y - 2, "#9fd8e0", 10);
        Sfx.Play("splash");
        guardian = null;
        rocks.Clear();
        Toast($"{GuardianName} sinks back into the deep. Knock on the carved stone by the pool to call it up again.", 5);
    }

    /* ---------- The pool's edge ---------- */
    // Where the pool meets the stone heading out from its middle at angle a (the last water pixel on the way out).
    (float x, float y) PoolEdge(float a)
    {
        float cx = MathF.Cos(a), cy = MathF.Sin(a), ex = AncientPoolX, ey = AncientPoolY;
        for (int r = 0; r < 100; r++)
        {
            float x = AncientPoolX + cx * r, y = AncientPoolY + cy * r;
            if (TileAt((int)MathF.Floor(x / T), (int)MathF.Floor(y / T)) != 'P') break;
            ex = x; ey = y;
        }
        return (ex, ey);
    }

    float AngleToPlayer => MathF.Atan2(player.Y - AncientPoolY, player.X - AncientPoolX);
    static float WrapAngle(float a) => MathF.Atan2(MathF.Sin(a), MathF.Cos(a));
    bool InPool(float x, float y) => TileAt((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1) / T)) == 'P';

    // Its head can be over the water or the open floor, never in a wall, a pillar or on the ladder.
    bool GuardCanBe(float x, float y)
    {
        foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 3, y - 3), (x - 3, y), (x + 3, y) })
        {
            char t = TileAt((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T));
            if (t is not ('P' or 'F' or 'G')) return false;
        }
        return true;
    }

    // How far out a point is from the middle of the pool, the way the wave travels.
    static float WaveDist(float x, float y)
    {
        float dx = x - AncientPoolX, dy = (y - AncientPoolY) * WaveSquash;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Right behind a pillar (it stands between you and the pool), the wave breaks before it reaches you.
    bool Sheltered(float x, float y)
    {
        if (pillarSpots == null) return false;
        float px = x - AncientPoolX, py = (y - AncientPoolY) * WaveSquash, pl = MathF.Sqrt(px * px + py * py);
        foreach (var (qx0, qy0) in pillarSpots)
        {
            float qx = qx0 - AncientPoolX, qy = (qy0 - AncientPoolY) * WaveSquash, ql = MathF.Sqrt(qx * qx + qy * qy);
            if (pl < ql + 3 || px * qx + py * qy <= 0) continue;
            if (MathF.Abs(px * qy - py * qx) / ql < 8) return true;
        }
        return false;
    }

    /* ---------- The fight ---------- */
    void UpdateGuardian(float dt)
    {
        if (guardianDue && mode == "play")
        {
            guardianDue = false;
            if (OnAncientFloor && !GuardianBeaten && guardian == null) GuardianRise(true);
        }
        var g = guardian;
        if (g == null) return;
        if (!OnAncientFloor) { guardian = null; rocks.Clear(); return; }
        if (mode != "play") return;
        g.T += dt;
        g.Hurt = Math.Max(0, g.Hurt - dt);
        if (g.Phase == "sink")
        {
            // Beaten: back into the water, and down out of sight. Then a word, and its card.
            float k = Math.Min(1, g.T / 0.6f);
            g.X = g.FromX + (g.ToX - g.FromX) * k;
            g.Y = g.FromY + (g.ToY - g.FromY) * k;
            if (g.T < 2f) return;
            guardian = null;
            Talk(new()
            {
                new("", $"{GuardianName} sinks back into the black water. The ripples spread, slow down, and stop."),
                new("", "Under the ledges round the pool, something with fins like little legs shifts and settles. The coelacanths can rest in their caves again."),
                new("", "On the stone where it lay, a piece of its armour has broken off: a plate of bone as big as a dinner plate.")
            }, ShowGuardianCard);
            return;
        }
        UpdateRocks(dt);
        if (guardian == null) return;   // knocked out by a rock
        // Off down the passage to the ladder, or right out to the far ends of the cavern.
        if (player.Y > 290 || Dist(player.X, player.Y, AncientPoolX, AncientPoolY) > GuardLeash) { GuardianRetreat(); return; }
        float left = g.Spirit / GuardSpirit;
        switch (g.Phase)
        {
            case "rise":
                if (g.T >= 1.3f) Prowl(g);
                break;
            case "prowl":
            {
                // It follows you round under the surface, a bit slower than you can run.
                float diff = WrapAngle(AngleToPlayer - g.Ang), turn = (1.1f + (1 - left) * 0.7f) * dt;
                g.Ang = WrapAngle(g.Ang + Math.Clamp(diff, -turn, turn));
                var (ex, ey) = PoolEdge(g.Ang);
                float nx = ex - MathF.Cos(g.Ang) * 9, ny = ey - MathF.Sin(g.Ang) * 9;
                g.X += (nx - g.X) * Math.Min(1, dt * 4);
                g.Y += (ny - g.Y) * Math.Min(1, dt * 4);
                g.Dir = player.X < g.X ? -1 : 1;
                // Linger right at the edge and it bites.
                g.EdgeT = Dist(player.X, player.Y, ex, ey) < 18 && MathF.Abs(diff) < 0.5f ? g.EdgeT + dt : 0;
                if (g.EdgeT > 0.6f) { SnapRear(g); break; }
                if (g.T < g.Wait) break;
                g.Attacks++;
                if (left < 0.4f && g.Attacks % 4 == 0) SurgeRear(g);
                else if (left < 0.7f && g.Attacks % 3 == 0) Slam(g);
                else Aim(g);
                break;
            }
            case "aim":
            {
                // It surfaces at the edge nearest you with its jaws opening, keeps its eyes on you, then picks its line.
                if (g.T < 0.55f)
                {
                    var (ex, ey) = PoolEdge(AngleToPlayer);
                    g.X += (ex - g.X) * Math.Min(1, dt * 10);
                    g.Y += (ey - g.Y) * Math.Min(1, dt * 10);
                    float ax = player.X - g.X, ay = player.Y - g.Y, al = MathF.Max(1, MathF.Sqrt(ax * ax + ay * ay));
                    g.AimX = ax / al; g.AimY = ay / al;
                    g.Dir = ax < 0 ? -1 : 1;
                }
                if (g.T >= (left < 0.5f ? 0.8f : 0.95f)) { g.Phase = "lunge"; g.T = 0; g.Ran = 0; g.Hit = false; Sfx.Play("splash"); }
                break;
            }
            case "lunge":
            {
                float step = (left < 0.5f ? 200 : 165) * dt, nx = g.X + g.AimX * step, ny = g.Y + g.AimY * step;
                if (!GuardCanBe(nx, ny))
                {
                    // Straight into a pillar or the cave wall: it's dazed for a good while.
                    quake = 0.35f;
                    Burst(nx, ny - 5, "#c9c2ae", 10);
                    Sfx.Play("hit");
                    Floater("Crash!", g.X, g.Y - 22, "#ffd76a");
                    if (InPool(g.X, g.Y)) Slide(g); else Stranded(g, "stunned", 2.6f);
                    break;
                }
                g.X = nx; g.Y = ny; g.Ran += step;
                if ((int)(g.T * 20) % 2 == 0)
                    particles.Add(new Particle { X = g.X - g.AimX * 10, Y = g.Y, Vx = Rand(-14, 14), Vy = Rand(-26, -10), Life = 0.35f, Color = InPool(g.X, g.Y) ? "#9fd8e0" : "#8a8378" });
                if (!g.Hit && iframes <= 0 && Dist(g.X, g.Y, player.X, player.Y) < 11)
                {
                    g.Hit = true;
                    HurtPlayer(LungeHurt, g.X, g.Y, 6);
                    if (guardian == null) return;   // knocked out
                }
                if (g.Ran < LungeRange) break;
                if (InPool(g.X, g.Y)) Slide(g); else Stranded(g, "beached", left < 0.5f ? 1.5f : 1.9f);
                break;
            }
            case "beached":
            case "stunned":
                if (g.T >= g.Wait) Slide(g);
                break;
            case "slide":
            {
                // Back into the water, tail first.
                float k = Math.Min(1, g.T / 0.6f);
                g.X = g.FromX + (g.ToX - g.FromX) * k;
                g.Y = g.FromY + (g.ToY - g.FromY) * k;
                if (k >= 1) { Burst(g.X, g.Y, "#9fd8e0", 6); Prowl(g); }
                break;
            }
            case "snapRear":
            {
                // Jaws wide at the edge, right by you: back off!
                var (ex, ey) = PoolEdge(AngleToPlayer);
                g.X += (ex - g.X) * Math.Min(1, dt * 12);
                g.Y += (ey - g.Y) * Math.Min(1, dt * 12);
                g.Dir = player.X < g.X ? -1 : 1;
                if (g.T < 0.5f) break;
                float ax = player.X - g.X, ay = player.Y - g.Y, al = MathF.Max(1, MathF.Sqrt(ax * ax + ay * ay));
                g.AimX = ax / al; g.AimY = ay / al;
                g.Phase = "snap"; g.T = 0;
                Sfx.Play("clang");
                if (iframes <= 0 && Dist(player.X, player.Y, g.X + g.AimX * 10, g.Y + g.AimY * 10) < 13)
                {
                    HurtPlayer(SnapHurt, g.X, g.Y, 5);
                    if (guardian == null) return;
                }
                break;
            }
            case "snap":
                if (g.T >= 0.4f) Prowl(g);
                break;
            case "slam":
                if (g.T >= 1.7f) Prowl(g);
                break;
            case "surgeRear":
                // Up in the middle of the pool, thrashing: a wave is coming, and the pillars will break it.
                g.X += (AncientPoolX - g.X) * Math.Min(1, dt * 3);
                g.Y += (AncientPoolY - g.Y) * Math.Min(1, dt * 3);
                if (g.T < 1.3f) break;
                g.Phase = "surge"; g.T = 0; g.Wave = WaveStart; g.Hit = false;
                quake = 0.4f;
                Sfx.Play("splash");
                Burst(AncientPoolX, AncientPoolY, "#ffffff", 14);
                break;
            case "surge":
                g.Wave += WaveSpeed * dt;
                if (!g.Hit && iframes <= 0 && MathF.Abs(WaveDist(player.X, player.Y) - g.Wave) < 5 && !Sheltered(player.X, player.Y))
                {
                    g.Hit = true;
                    HurtPlayer(WaveHurt, AncientPoolX, AncientPoolY, 6);
                    if (guardian == null) return;
                }
                if (g.Wave >= WaveEnd) { g.Wave = -1; Prowl(g); }
                break;
        }
    }

    void Prowl(Guardian g)
    {
        g.Phase = "prowl";
        g.T = 0;
        g.EdgeT = 0;
        g.Ang = MathF.Atan2(g.Y - AncientPoolY, g.X - AncientPoolX);
        // It picks its moments faster as it tires and gets angrier.
        g.Wait = Rand(1.1f, 1.9f) * (0.65f + 0.35f * g.Spirit / GuardSpirit);
    }

    void Aim(Guardian g)
    {
        g.Phase = "aim"; g.T = 0;
        Sfx.Play("splash");
    }

    void SnapRear(Guardian g)
    {
        g.Phase = "snapRear"; g.T = 0; g.EdgeT = 0;
        Sfx.Play("splash");
    }

    void Stranded(Guardian g, string phase, float secs)
    {
        g.Phase = phase; g.T = 0; g.Wait = secs;
        quake = Math.Max(quake, 0.25f);
        Sfx.Play("stomp");
        Burst(g.X - g.Dir * 8, g.Y, "#8a8378", 10);
    }

    // Back to the nearest water, 9 px in from the edge.
    void Slide(Guardian g)
    {
        g.Phase = "slide"; g.T = 0;
        g.FromX = g.X; g.FromY = g.Y;
        float a = MathF.Atan2(g.Y - AncientPoolY, g.X - AncientPoolX);
        var (ex, ey) = PoolEdge(a);
        g.ToX = ex - MathF.Cos(a) * 9; g.ToY = ey - MathF.Sin(a) * 9;
    }

    // It rams the side of the pool and the roof shakes loose: one rock comes down right where you stand, the rest round you.
    void Slam(Guardian g)
    {
        g.Phase = "slam"; g.T = 0;
        quake = 0.7f;
        Sfx.Play("stomp");
        Sfx.Play("hit");
        Burst(g.X, g.Y, "#ffffff", 10);
        rocks.Clear();
        rocks.Add(new Rock { X = player.X, Y = player.Y });
        int want = g.Spirit < GuardSpirit * 0.35f ? 7 : 5;
        for (int i = 0; i < 40 && rocks.Count < want; i++)
        {
            float a = Rand(0, MathF.Tau), r = Rand(12, 46), x = player.X + MathF.Cos(a) * r, y = player.Y + MathF.Sin(a) * r * 0.8f;
            if (!Walkable(TileAt((int)MathF.Floor(x / T), (int)MathF.Floor(y / T))) || rocks.Any(o => Dist(o.X, o.Y, x, y) < 13)) continue;
            rocks.Add(new Rock { X = x, Y = y, T = -Rand(0, 0.35f) });
        }
        Floater("Look up!", player.X, player.Y - 26, "#ffd76a");
    }

    void SurgeRear(Guardian g)
    {
        g.Phase = "surgeRear"; g.T = 0;
        Sfx.Play("splash");
        Floater("Get behind a pillar!", player.X, player.Y - 26, "#7fd36b");
    }

    // Falling rocks: a shadow that grows for a second, then the rock. Anyone in the shadow when it lands is hurt.
    void UpdateRocks(float dt)
    {
        for (int i = rocks.Count - 1; i >= 0; i--)
        {
            var r = rocks[i];
            r.T += dt;
            if (r.T < RockFall) continue;
            if (!r.Down)
            {
                r.Down = true;
                Burst(r.X, r.Y - 2, "#8a8378", 8);
                Sfx.Play("mine");
                quake = Math.Max(quake, 0.12f);
                if (iframes <= 0 && UnderRock(r, player.X, player.Y))
                {
                    HurtPlayer(RockHurt, r.X, r.Y, 3);
                    // Knocked out: Faint has cleared the rocks and the guardian, so stop walking the list.
                    if (guardian == null) return;
                }
            }
            if (r.T > RockFall + 0.45f) rocks.RemoveAt(i);
        }
    }

    static bool UnderRock(Rock r, float x, float y)
    {
        float dx = (x - r.X) / RockRX, dy = (y - r.Y) / RockRY;
        return dx * dx + dy * dy < 1;
    }

    // An oval of single pixels (rx across, ry down), filled or just its edge.
    void Oval(float cx, float cy, float rx, float ry, Color c, bool fill)
    {
        if (rx < 0.5f || ry < 0.5f) return;   // a rock still waiting to fall has no shadow yet (and 0/0 otherwise)
        if (fill)
        {
            for (int dy = (int)-ry; dy <= (int)ry; dy++)
            {
                float half = rx * MathF.Sqrt(Math.Max(0, 1 - dy * dy / (ry * ry)));
                pix.Rect(cx - half, cy + dy, (int)MathF.Round(half * 2) + 1, 1, c);
            }
            return;
        }
        int steps = Math.Max(12, (int)((rx + ry) * 3));
        for (int i = 0; i < steps; i++)
        {
            float a = i * MathF.Tau / steps;
            pix.Rect(cx + MathF.Cos(a) * rx, cy + MathF.Sin(a) * ry, 1, 1, c);
        }
    }

    /* ---------- Striking it ---------- */
    // The nearest point of its body to you: just the head while it's in the water, head to tail on the stone.
    (float x, float y) GuardianNearest()
    {
        var g = guardian;
        if (g.Phase is not ("beached" or "stunned")) return (g.X, g.Y - 3);
        float tx = g.X - g.Dir * 20, k = Math.Clamp((player.X - g.X) / (tx - g.X), 0, 1);
        return (g.X + (tx - g.X) * k, g.Y - 3);
    }

    bool GuardianInReach()
    {
        var g = guardian;
        if (g == null || g.Phase is not ("beached" or "stunned" or "aim" or "snapRear" or "snap")) return false;
        var (bx, by) = GuardianNearest();
        float fx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0, fy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
        float dx = bx - player.X, dy = by - player.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        return d < 17 && (d < 8 || (dx * fx + dy * fy) / d > 0.2f);
    }

    Target GuardianTarget()
    {
        var g = guardian;
        bool open = g.Phase is "beached" or "stunned";
        if (GuardianInReach())
            return new Target { Type = "guardian", Label = open ? $"Strike {GuardianName} ({Weapon().name})" : $"Strike {GuardianName}'s armoured head" };
        string hint = g.Phase switch
        {
            "aim" => "It's lining up a lunge! Get out of its path",
            "beached" => "It's stranded on the stone! Strike it before it slides back",
            "stunned" => "It crashed into the stone! Strike it now",
            "snapRear" or "snap" => "Back away from the edge!",
            "slam" => "Rocks are falling! Step out of the shadows",
            "surgeRear" or "surge" => "A wave is coming! Get right behind a pillar",
            "lunge" or "rise" or "sink" or "slide" => "",
            _ => "Keep off the edge. Strike it when it strands itself on the stone"
        };
        return hint == "" ? null : new Target { Type = "info", Label = hint };
    }

    // Only its body is open, and only on the stone: a blow on its head bounces off the armour.
    void StrikeGuardian()
    {
        var g = guardian;
        if (g == null || swingT > 0.12f) return;
        var (_, dmg) = Weapon();
        Swing(WeaponTool(), 0.3f);
        var (bx, by) = GuardianNearest();
        FaceToward(bx, by);
        if (g.Phase is not ("beached" or "stunned"))
        {
            Sfx.Play("clang");
            Burst(g.X, g.Y - 6, "#c9c2ae", 4);
            Floater("Clang!", g.X, g.Y - 22, "#c9c2ae");
            if (!state.Hinted("platejawArmour"))
            {
                state.hinted["platejawArmour"] = true;
                Toast("Clang! Its head and shoulders are armoured in bone. Wait for it to strand itself on the stone, then strike its body.", 5);
            }
            return;
        }
        if (g.Hurt > 0) { Sfx.Play("nope"); return; }
        bool dazed = g.Phase == "stunned";
        g.Spirit -= (3 + dmg * 0.5f) * (dazed ? 2.4f : 2f);
        g.Hurt = 0.4f;
        Sfx.Play("hit");
        Burst(bx, by - 3, dazed ? "#ffd76a" : "#ffffff", dazed ? 9 : 5);
        if (dazed) Floater("Big hit!", g.X, g.Y - 24, "#ffd76a");
        if (g.Spirit <= 0) GuardianDefeated();
    }

    // Worn out, it gives up. The win and its prize are kept straight away, before any words or card, so quitting
    // halfway through them loses nothing.
    void GuardianDefeated()
    {
        var g = guardian;
        g.Phase = "sink"; g.T = 0; g.Spirit = 0; g.Wave = -1;
        rocks.Clear();
        g.FromX = g.X; g.FromY = g.Y;
        float a = MathF.Atan2(g.Y - AncientPoolY, g.X - AncientPoolX);
        var (ex, ey) = PoolEdge(a);
        (g.ToX, g.ToY) = InPool(g.X, g.Y) ? (g.X, g.Y) : (ex - MathF.Cos(a) * 9, ey - MathF.Sin(a) * 9);
        state.hinted["platejaw"] = true;
        Give("platejaw_plate");
        GainXp(80);
        Sfx.Play("rare");
        Burst(g.X, g.Y - 8, "#ffd76a", 16);
        Save();
    }

    void ShowGuardianCard()
    {
        catchOpenedAt = Raylib.GetTime();
        mode = "platejaw";
        SetPrompt("");
    }

    void CloseGuardianCard()
    {
        if (mode != "platejaw" || Raylib.GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        heldItem = "platejaw_plate";
        heldT = 2f;
        Toast("Its broken plate is in your bag. A workbench can make armour of it (the Combat tab).", 5);
    }

    void DrawGuardianCard()
    {
        if (GoldCard("platejaw", GuardianName, "Driven off", Data.Platejaw.Desc,
            "Worn out on the stone by the Ancient pool, it has gone back down. A piece of its armour is in your bag.", "Onward!")) CloseGuardianCard();
    }

    /* ---------- The carved stone ---------- */
    Target RuneStoneTarget() => CoelacanthCaught && !GuardianBeaten
        ? new Target { Type = "runestone", Label = GuardianMet ? $"Knock on the carved stone (calls {GuardianName} up)" : "Knock on the carved stone" }
        : new Target { Type = "runestone", Label = "Read the carved stone" };

    void UseRuneStone()
    {
        FaceToward(RuneStoneX, RuneStoneY);
        if (CanWakeGuardian)
        {
            Swing("hands", 0.3f);
            Sfx.Play("clang");
            Floater("Knock, knock, knock", RuneStoneX, RuneStoneY - 20, "#c9c2ae");
            GuardianRise(false);
            return;
        }
        state.hinted["runestone"] = true;
        Talk(GuardianBeaten
            ? new()
            {
                new("You", $"The armoured fish on the stone: {GuardianName}. It still hunts down there, but it lets you be now."),
                new("You", "Under the ledges, the little fish with leg-like fins are carved resting, safe in their caves.")
            }
            : new()
            {
                new("You", "The same picture is cut into the stone, over and over: a fish with its head and shoulders in armour, and jaws like a pair of shears."),
                new("You", "Smaller fish hide under the ledges around it. Their fins are drawn like little legs."),
                new("You", "Scratched underneath, much later: \"Knock, and it hears you. A fish feels a knock through the water, all along its sides.\"")
            });
    }

    /* ---------- Drawing ---------- */
    // Platejaw side on, facing right, 34 x 13 with the bottom row on its belly. Two frames: jaws shut and jaws open.
    static readonly string[][] PlatejawFrames =
    {
        new[]
        {
            "......................hhhhh...........",
            "...................hhhaaaaahhh........",
            "............d....hhaaaaaaaaaaah.......",
            "...........dd...haaasaaaaaaaaaah......",
            "..........ddd..haaaasaaaaaaaaaaah.....",
            "t........dddd.Aaaaaasaaaaaaa.ooaaa....",
            "tt....BBBBBBBAaaaaasaaaaaaaoekaaaa....",
            ".tt.BBBbbbbbbAAaaasaaaaaaaaaoaaaaaa...",
            "..tBBbbbbbbbbbAAAAsAAAAAAAAAAAAAAAA...",
            "..tBbbbbbbbbbbbAAAAsAAAAAAAAAAAAAAJJ..",
            ".ttBllllllllllllAAAAsJJJJJJJJJJJJJ.J..",
            "tt..lllllllllll..AAAAAAAAAAAAAAAAJJ...",
            ".......pp..........ppAAAAAAAAAAAAJ....",
            "......................................"
        },
        new[]
        {
            "......................hhhhh...........",
            "...................hhhaaaaahhh........",
            "............d....hhaaaaaaaaaaah.......",
            "...........dd...haaasaaaaaaaaaah......",
            "..........ddd..haaaasaaaaaaaaaaah.....",
            "t........dddd.Aaaaaasaaaaaaa.ooaaa....",
            "tt....BBBBBBBAaaaaasaaaaaaaoekaaaaJ...",
            ".tt.BBBbbbbbbAAaaasaaaaaaaaaoaaaaJJ...",
            "..tBBbbbbbbbbbAAAAsAAAAAmmmmmmmmmJ....",
            "..tBbbbbbbbbbbbAAAAsAAAmmmmmmmmmm.....",
            ".ttBllllllllllllAAAAsAAAmmmmmmmmmJ....",
            "tt..lllllllllll..AAAAAJJJJJJJJJJJJ....",
            ".......pp..........ppAAAAAAAAAAAAJJ...",
            "......................AAAAAAAAAAAJ...."
        }
    };
    const int PlatejawHeadCol = 27, PlatejawRows = 14;

    static readonly Dictionary<char, string> PlatejawColors = new()
    {
        ['A'] = "#4a4e57", ['a'] = "#6b707c", ['h'] = "#9aa0aa", ['s'] = "#2f3238",
        ['B'] = "#34403f", ['b'] = "#4d5d5b", ['l'] = "#a7b0a6",
        ['d'] = "#2c3534", ['t'] = "#2c3534", ['p'] = "#2c3534",
        ['o'] = "#b3ab95", ['e'] = "#ffd76a", ['k'] = "#10243a",
        ['J'] = "#e8e0c8", ['m'] = "#3a0f12"
    };

    // With the bottom of its belly at (x, y) under its head, facing dir. Only the top `rows` rows show (the rest are
    // under the surface), and the part that shows sits on y.
    void DrawPlatejaw(float x, float y, int dir, bool open, bool flash, int rows = PlatejawRows)
    {
        var f = PlatejawFrames[open ? 1 : 0];
        int w = f[0].Length, top = (int)MathF.Round(y) - rows + 1, hx = (int)MathF.Round(x);
        for (int r = 0; r < rows && r < f.Length; r++)
            for (int c = 0; c < w; c++)
            {
                char ch = f[r][c];
                if (ch == '.') continue;
                int px = dir > 0 ? hx + c - PlatejawHeadCol : hx - (c - PlatejawHeadCol);
                pix.Rect(px, top + r, 1, 1, flash && ch is not ('e' or 'k') ? "#ffffff" : PlatejawColors[ch]);
            }
    }

    // Under the surface: a long dark shape in the pool, with the top of its armoured head breaking the water.
    void DrawGuardianWater(float t)
    {
        var g = guardian;
        if (g == null || guardianUndrawn) return;
        var sh = Pal.Rgba(4, 12, 18, 0.55f);
        bool under = g.Phase is "prowl" or "slam" or "surgeRear" or "surge" or "rise";
        if (under || (g.Phase == "sink" && g.T > 0.6f))
        {
            float bx = g.X - g.Dir * 10;
            pix.Rect(bx - 15, g.Y - 2, 30, 4, sh);
            pix.Rect(bx - 11, g.Y - 3, 22, 6, sh);
            int rows = g.Phase switch
            {
                // Up out of the water head and shoulders, a moment's look round, then down to prowl (Codex: it used to
                // peak at three rows, just a ripple).
                "rise" => g.T < 0.6f ? (int)(g.T / 0.06f) : g.T < 1f ? 10 : Math.Max(4, 10 - (int)((g.T - 1f) * 20)),
                "surgeRear" or "surge" => 7,
                "sink" => Math.Max(0, 5 - (int)((g.T - 0.6f) * 5)),
                _ => 4
            };
            if (rows > 0) DrawPlatejaw(g.X, g.Y - 1 + (g.Phase is "surgeRear" ? MathF.Round(MathF.Sin(t * 30)) : 0), g.Dir, g.Phase is "surgeRear", g.Hurt > 0.25f, rows);
            // Ripples where it breaks the surface.
            float ph = (t * 1.4f) % 1;
            pix.Ring(g.X, g.Y, 3 + ph * 7, Pal.Rgba(190, 235, 245, 0.5f * (1 - ph)));
        }
    }

    // On (or half out of) the stone: drawn with everything else that stands, by its feet.
    void DrawGuardianBody(float t)
    {
        var g = guardian;
        if (g == null || guardianUndrawn || g.Phase is "prowl" or "slam" or "surgeRear" or "surge" or "rise" || (g.Phase == "sink" && g.T > 0.6f)) return;
        bool flash = g.Hurt > 0.25f;
        if (g.Phase is "aim" or "snapRear" or "snap")
        {
            // Head and shoulders out of the water at the edge, jaws open.
            float jx = g.Phase is "aim" or "snapRear" ? MathF.Sin(t * 50) * 0.6f : 0;
            bool open = g.Phase == "snap" || (int)(t * 6) % 2 == 0 || g.Phase == "snapRear";
            float reach = g.Phase == "snap" ? 6 : 0;
            DrawPlatejaw(g.X + jx + g.AimX * reach, g.Y + g.AimY * reach, g.Dir, open, flash, 10);
            pix.Ring(g.X, g.Y, 6, Pal.Rgba(190, 235, 245, 0.6f));
            return;
        }
        // Out on the stone: lunging, stranded, dazed or sliding back.
        bool wet = InPool(g.X, g.Y);
        float thrash = g.Phase is "beached" ? MathF.Round(MathF.Sin(t * 18)) : 0;
        if (!wet) pix.Rect(g.Dir > 0 ? g.X - 24 : g.X - 6, g.Y + 1, 30, 1, "rgba(0,0,0,0.3)");
        DrawPlatejaw(g.X, g.Y + (g.Phase == "lunge" ? -2 : 0), g.Dir, g.Phase == "lunge", flash, wet ? 9 : PlatejawRows);
        if (g.Phase == "beached")
        {
            // Its tail slaps the stone.
            int tx = (int)MathF.Round(g.X - g.Dir * 26);
            pix.Rect(tx, g.Y - 6 + thrash * 2, 2, 3, "#2c3534");
        }
        if (g.Phase == "stunned")
            for (int i = 0; i < 3; i++)
            {
                float a = t * 5 + i * MathF.Tau / 3;
                pix.Rect(g.X + MathF.Cos(a) * 7, g.Y - 15 + MathF.Sin(a) * 2, 1, 1, "#ffd76a");
            }
    }

    // Warnings drawn over the dark so they can always be read: the line a lunge will take, where rocks will land, the wave
    // and the shelter behind each pillar.
    void DrawGuardianWarnings(float t)
    {
        var g = guardian;
        if (g == null || guardianUndrawn) return;
        // Its eyes, glowing in the dark.
        if (g.Phase is not ("prowl" or "slam" or "sink"))
            pix.Glow(g.X + g.Dir * 1, g.Y - 7, 5, Pal.Rgba(255, 215, 106, 0.35f));
        if (g.Phase == "aim")
        {
            // The lane it will lunge down: arrows marching out from its jaws, brighter once it has picked its line.
            bool locked = g.T >= 0.55f;
            float pulse = locked ? 0.75f + 0.25f * MathF.Sin(t * 24) : 0.45f + 0.2f * MathF.Sin(t * 12), march = (t * 30) % 6;
            float nx = -g.AimY, ny = g.AimX;
            for (float s = 8 + march; s < LungeRange + 6; s += 6)
            {
                float lx = g.X + g.AimX * s, ly = g.Y - 2 + g.AimY * s, a = pulse * (1 - 0.5f * s / LungeRange);
                var c = Pal.Rgba(255, 106, 90, a);
                pix.Rect(lx, ly, 1, 1, c);
                for (int k = 1; k <= 3; k++)
                {
                    pix.Rect(lx - g.AimX * k + nx * k, ly - g.AimY * k + ny * k, 1, 1, c);
                    pix.Rect(lx - g.AimX * k - nx * k, ly - g.AimY * k - ny * k, 1, 1, c);
                }
            }
            int ex = (int)MathF.Round(g.X), ey = (int)MathF.Round(g.Y) - 30;
            pix.Rect(ex - 2, ey - 1, 5, 10, "#10243a"); pix.Rect(ex - 1, ey, 3, 8, "#ff6a5a");
            pix.Rect(ex, ey + 1, 1, 4, "#ffffff"); pix.Rect(ex, ey + 6, 1, 1, "#ffffff");
        }
        if (g.Phase == "snapRear")
        {
            int ex = (int)MathF.Round(g.X), ey = (int)MathF.Round(g.Y) - 28;
            pix.Rect(ex - 2, ey - 1, 5, 10, "#10243a"); pix.Rect(ex - 1, ey, 3, 8, "#ffd76a");
            pix.Rect(ex, ey + 1, 1, 4, "#ffffff"); pix.Rect(ex, ey + 6, 1, 1, "#ffffff");
        }
        foreach (var r in rocks)
        {
            if (r.Down)
            {
                // The rock, broken on the floor.
                pix.Rect(r.X - 3, r.Y - 2, 6, 3, "#6a645c"); pix.Rect(r.X - 2, r.Y - 3, 4, 1, "#8a8378");
                continue;
            }
            // Where it will land: the whole oval it hurts, its shadow filling it as the rock comes down.
            float k = Math.Clamp(r.T / RockFall, 0, 1);
            Oval(r.X, r.Y, RockRX * k, RockRY * k, Pal.Rgba(10, 6, 4, 0.35f + 0.3f * k), true);
            Oval(r.X, r.Y, RockRX, RockRY, Pal.Rgba(255, 140, 90, 0.55f + 0.4f * MathF.Abs(MathF.Sin(t * (6 + 10 * k)))), false);
            if (k > 0.7f)
            {
                // The rock itself, dropping out of the dark.
                float fall = (1 - k) / 0.3f * 40;
                pix.Rect(r.X - 3, r.Y - 4 - fall, 6, 4, "#7a7368"); pix.Rect(r.X - 2, r.Y - 5 - fall, 4, 1, "#9a9388");
            }
        }
        if (g.Phase is "surgeRear" or "surge")
        {
            // Where you'd be safe: the shade right behind each pillar.
            float a = g.Phase == "surgeRear" ? 0.25f + 0.25f * MathF.Sin(t * 12) : 0.3f;
            foreach (var (qx, qy) in pillarSpots ?? new())
            {
                float dx = qx - AncientPoolX, dy = (qy - AncientPoolY) * WaveSquash, l = MathF.Sqrt(dx * dx + dy * dy);
                float ux = dx / l, uy = dy / l / WaveSquash;
                for (float s = 8; s <= 30; s += 3)
                {
                    float sx = qx + ux * s, sy = qy + 3 + uy * s;
                    if (Walkable(TileAt((int)MathF.Floor(sx / T), (int)MathF.Floor(sy / T)))) pix.Rect(sx - 1, sy, 2, 1, Pal.Rgba(127, 211, 107, a));
                }
            }
        }
        if (g.Wave > 0)
        {
            // The wave: a white line rolling out from the pool, broken behind every pillar.
            float k = (g.Wave - WaveStart) / (WaveEnd - WaveStart);
            int steps = (int)(g.Wave * 4);
            for (int i = 0; i < steps; i++)
            {
                float a = i * MathF.Tau / steps, x = AncientPoolX + MathF.Cos(a) * g.Wave, y = AncientPoolY + MathF.Sin(a) * g.Wave / WaveSquash;
                char tile = TileAt((int)MathF.Floor(x / T), (int)MathF.Floor(y / T));
                if (tile is 'W' or 'P' || Sheltered(x, y)) continue;
                pix.Rect(x, y, 1, 1, Pal.Rgba(225, 245, 255, 0.95f * (1 - k * 0.6f)));
                pix.Rect(x, y + 1, 1, 1, Pal.Rgba(127, 200, 230, 0.6f * (1 - k * 0.6f)));
            }
        }
    }

    // A low stone slab on the south shore, carved with the armoured fish.
    void DrawRuneStone(float t)
    {
        int X = (int)RuneStoneX, Y = (int)RuneStoneY;
        pix.Rect(X - 6, Y, 12, 2, "rgba(0,0,0,0.3)");
        pix.Rect(X - 5, Y - 11, 10, 12, "#4f6a6c");
        pix.Rect(X - 5, Y - 11, 2, 12, "#6a8a8c");
        pix.Rect(X - 4, Y - 13, 8, 2, "#5f7c7e");
        // The fish: an armoured head and a tail.
        const string ink = "#2e4448";
        pix.Rect(X - 3, Y - 8, 3, 3, ink); pix.Rect(X, Y - 7, 3, 1, ink); pix.Rect(X + 3, Y - 8, 1, 3, ink);
        pix.Rect(X - 3, Y - 4, 6, 1, ink);
        float glow = CanWakeGuardian ? 0.5f + 0.5f * MathF.Sin(t * 3) : 0.35f;
        pix.Rect(X - 2, Y - 2, 4, 1, Pal.Rgba(95, 232, 216, glow));
        if (!state.Hinted("runestone") && !GuardianMet && (time * 0.7f) % 1 < 0.15f) pix.Rect(X + 2, Y - 12, 1, 1, "#ffffff");
    }
}
