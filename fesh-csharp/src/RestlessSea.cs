using Raylib_cs;

namespace Fesh;

// Shaking: how long, how hard, and how you did (moved into the open, then ducked, covered and held on).
sealed class Quake { public float T, Len, Power, NextCoco; public bool Ducked; }
// A coconut shaken loose from a palm: looks only (it never hurts you; being in the open is the lesson, not a penalty).
sealed class Coco { public float X, Y, T, Fall; }
// Someone heading up to School Rise: Lira, Niko, or one of Ma'am Isay's children (Kid 0-2), along the signposted route.
sealed class Mover
{
    public string Id, Name; public int Kid = -1;
    public float X, Y, WalkT, Speed = 34;
    public (float x, float y)[] Path = Array.Empty<(float, float)>();
    public int Leg;
    public bool Going, Safe, Warned;
    public string Face = "down";
}
// The drill (Drill) or the real thing. Phase is "quake" (the shaking), "evacuate" (up to School Rise; the sea goes out
// and comes back) and "wait" (everyone up, waiting for the all-clear).
sealed class Evac
{
    public bool Drill, Ducked, Caught, Bell, Roared, Counted;
    public string Phase = "quake";
    public float T, ShoutT, UpAt = -1;   // UpAt: when you first stood on School Rise
    public int Shouted;
    public Quake Quake;
    public readonly List<Mover> People = new();
}

// The restless sea (1.19), story 3, on Amihan Village. The ground shakes, the bangus pond drains through a crack in its
// bank, and three islanders' old stories come up: Lira's lola's Bernardo Carpio (a Tagalog story of a giant trapped in
// the mountains of Montalban, now Rodriguez, Rizal), Niko's lola's berberoka (a northern Luzon story, recorded in Apayao,
// of a creature that holds back the water so the fish lie stranded, then lets it go), and a notch cut high on the old
// post at the landing. Ma'am Isay puts them together: those remembered signs fit a tsunami, and one sign is enough to go
// uphill. You put up the evacuation signs, run a drill with three of her children, and one later day the ground shakes
// hard: duck, cover and hold in the open, then everyone goes up to School Rise and waits for the official all-clear while
// the sea goes out and comes back over the low ground. Nobody fights anything, and nothing here hurts you: a player
// caught by the water is pulled up the slope. Codex's design review shaped the wording and the finale (2026-10-09).
partial class Game
{
    Quake tremor;   // the first, small quake (beat 1)
    Evac evac;      // the drill or the real thing
    readonly List<Coco> cocos = new();

    // Where things are on Amihan Village island (world pixels).
    const float RiseX = 1655, RiseY = 163, RiseR = 34;            // School Rise: the open ground in front of the school
    const float PostX = 1538, PostY = 206;                        // the old post with the bell, by the landing
    const float CrackX = 1604, CrackY = 238;                      // the crack in the bangus pond's bank (its west side)
    static readonly (float x, float y)[] SignSpots = { (1556, 197), (1620, 198), (1624, 170) };
    static readonly (float x, float y)[] KidSpots = { (1572, 214), (1590, 224), (1606, 214) };
    static readonly string[] KidNames = { "Mia", "Jun", "Bea" };
    const float ShoutR = 95;
    // When the sea goes out and comes back, in seconds from the start of the shaking (Evac.T).
    const float QuakeLen = 8, BellAt = 20, RoarAt = 22, SeaBackAt = 31, SeaOverAt = 62;

    bool SeaStoryStarted => state.Hinted("rs:tremor");
    bool SeaCaseClosed => state.Hinted("rs:done");
    int SeaClueCount => Data.SeaClues.Count(c => state.Hinted(c.Key));
    int SignsUp => Enumerable.Range(0, SignSpots.Length).Count(i => state.Hinted($"rs:sign{i}"));
    bool SeaEmergency => evac is { Drill: false };
    bool Shaking => tremor != null || evac?.Phase == "quake";
    // Ducking: holding <act> while the ground shakes (anywhere: being in the open is what the prompt is about).
    bool Ducking => Shaking && mode == "play" && Bind.Down("act");

    // On foot on Amihan Village island's dry land.
    // Every corner of your footprint on dry land, not just the middle: wading stops for the evacuation, and a foot left in
    // the shallows would then refuse every step inland (Codex).
    bool OnVillageIsland => scene == "world" && !Aboard && !Riding && !InWater && player.X >= EastStart * T
        && RegionOf(player.X, player.Y) == "amihan:Amihan Village" && Walkable(TileUnder(player.X, player.Y)) && CanStand(player.X, player.Y);
    bool OnRise => Dist(player.X, player.Y, RiseX, RiseY) < RiseR;

    // Nothing else is going on: no fight, eclipse, race, festival, storm, and it's daytime.
    bool SeaQuiet => mode == "play" && boss == null && guardian == null && eclipse == null && race == null && !raceArmed
        && !Stormy && !FestivalToday && state.clock is >= 7 * 60 and < 17 * 60 && fish == null && landed == null;

    /* ---------- Starting things ---------- */
    void CheckRestlessSea()
    {
        if (!state.Hinted("metIsay")) return;
        // The day you first count as ready (old saves too): the tremor comes on a later day.
        if (!state.gifts.ContainsKey("rs_ready")) state.gifts["rs_ready"] = state.day;
        if (tremor != null || evac != null || !SeaQuiet || !OnVillageIsland) return;
        if (!SeaStoryStarted && state.day > state.gifts["rs_ready"])
        {
            tremor = new Quake { Len = 5, Power = 0.3f };
            StartShaking("The ground is shaking!");
            return;
        }
        if (state.Hinted("rs:drill") && !SeaCaseClosed && state.day > state.gifts.GetValueOrDefault("rs_drill"))
            StartEmergency();
    }

    void StartShaking(string what)
    {
        Sfx.Play("thunder");
        player.Moving = false;
        Toast($"{what} Get into the open, away from palms and houses. Then duck, cover your head and hold on: hold <act>.", 6);
    }

    void StartEmergency()
    {
        evac = new Evac { Quake = new Quake { Len = QuakeLen, Power = 0.7f } };
        AddMovers(evac);
        cocos.Clear();
        StartShaking("A strong earthquake!");
    }

    // Lira and Niko from their houses, and the three children playing in the square, each with a path up the signposted
    // route to their place on School Rise (the children's is back on the school bench).
    void AddMovers(Evac e)
    {
        if (!e.Drill)
            foreach (var id in new[] { "lira", "niko" })
            {
                var s = IslanderWalk(id);
                var to = id == "lira" ? (1631f, 166f) : (1682f, 166f);
                e.People.Add(new Mover { Id = id, Name = Islanders.First(n => n.id == id).name, X = s.X, Y = s.Y, Speed = 36,
                    Path = id == "lira" ? new[] { SignSpots[1], SignSpots[2], to } : new[] { (1700f, 178f), to } });
            }
        for (int k = 0; k < KidSpots.Length; k++)
        {
            var (bx, by) = BenchSeat(k);
            e.People.Add(new Mover { Id = "kid" + k, Name = KidNames[k], Kid = k, X = KidSpots[k].x, Y = KidSpots[k].y, Speed = 40,
                Path = new[] { SignSpots[1], SignSpots[2], (bx, by + 7), (bx, by) } });
        }
    }

    (float x, float y) BenchSeat(int k) => (SchoolX - 18 + 5 + k * 9, SchoolY + 16 + 1);

    /* ---------- Every frame ---------- */
    void UpdateRestlessSea(float dt)
    {
        if (scene != "world") { if (evac?.Drill == true) CancelDrill(); return; }
        if (mode == "play") CheckRestlessSea();
        if (mode != "play") return;
        if (tremor != null) UpdateTremor(dt);
        if (evac != null) UpdateEvac(dt);
        UpdateCocos(dt);
    }

    void UpdateShake(Quake q, float dt)
    {
        q.T += dt;
        // Shaking hard at first, easing off towards the end.
        float k = Math.Clamp(Math.Min(q.T / 0.6f, (q.Len - q.T) / 1.5f), 0, 1);
        quake = Math.Max(quake, q.Power * k);
        if (Ducking && InTheOpen) q.Ducked = true;
        if (q.T % 2.4f < dt) Sfx.Play("thunder");
    }

    void UpdateTremor(float dt)
    {
        var q = tremor;
        UpdateShake(q, dt);
        if (q.T < q.Len) return;
        tremor = null;
        state.hinted["rs:tremor"] = true;
        state.gifts["rs_tremor"] = state.day;
        state.hinted["rs:duckedTremor"] = q.Ducked;
        Save();
        Toast(q.Ducked ? "It's over. Well done: out in the open, you ducked, covered your head and held on. Ma'am Isay will want to talk about it."
            : "It's over. Next time, get into the open, away from palms and houses, then duck, cover and hold. Ma'am Isay will want to talk about it.", 7);
    }

    // Away from palms and houses: nothing that can fall on you.
    bool InTheOpen => !trees.Any(t => t.kind == 'h' && Dist(player.X, player.Y, t.x * T + 5, t.y * T + 8) < 16)
        && !IslandSolids().Any(b => new Box(b.X - 10, b.Y - 10, b.W + 20, b.H + 20).Overlaps(new Box(player.X - 3, player.Y - 3, 6, 3)));

    void UpdateEvac(float dt)
    {
        var e = evac;
        e.T += dt;
        e.ShoutT = Math.Max(0, e.ShoutT - dt);
        if (e.Phase == "quake")
        {
            UpdateShake(e.Quake, dt);
            e.Ducked |= e.Quake.Ducked;
            ShakeLoose(dt);
            if (e.T < QuakeLen) return;
            e.Phase = "evacuate";
            foreach (var m in e.People) if (m.Kid < 0) m.Going = true;
            Toast("Ma'am Isay: \"Strong shaking by the sea: that's warning enough. Everyone up to School Rise, now! Follow the signs, and call to anyone you pass!\"", 7);
            return;
        }
        if (e.Phase == "evacuate")
        {
            if (!e.Drill)
            {
                // Isay rings the school bell for anyone still down there; then the sea answers.
                if (!e.Bell && e.T >= BellAt)
                {
                    e.Bell = true;
                    Sfx.Play("clang");
                    foreach (var m in e.People) m.Going = true;
                    Floater("Ding! Ding! Ding!", SchoolX, SchoolY - 40, "#ffd76a");
                }
                if (!e.Roared && e.T >= RoarAt)
                {
                    e.Roared = true;
                    Sfx.Play("thunder"); Sfx.Play("splash");
                    Toast("A roar from the sea, like a jet... The water that ran out past the reef is coming back.", 5);
                }
                // Down on the low ground when the water comes over it: a neighbour pulls you up the slope, every time
                // (Codex: only the first time used to work).
                if (FloodAt(player.X, player.Y))
                {
                    e.Caught = true;
                    PulledUp();
                    return;
                }
            }
            else if (e.T > 18 && e.Shouted == 0 && e.T % 8 < dt) Toast("Ma'am Isay: \"Call to the children on your way up!\" Press <act> near them.", 4);
            MoveMovers(e, dt);
            if (e.UpAt < 0 && OnRise) e.UpAt = e.T;
            bool all = e.People.All(m => m.Safe);
            if (e.Drill)
            {
                if (all && OnRise) FinishDrill();
                else if (Dist(player.X, player.Y, RiseX, RiseY) > 300) CancelDrill();
                return;
            }
            if (OnRise && !e.Counted && all) { e.Counted = true; Toast("Ma'am Isay counts heads: Lira, Niko, Mia, Jun, Bea... and you. Everyone's here. Now we wait.", 5); }
            if (e.T >= SeaOverAt && OnRise && all) WaitForAllClear();
            else if (e.T >= SeaOverAt && !OnRise && e.T % 6 < dt) Toast("Ma'am Isay: \"Come up to School Rise! We stay together up here.\"", 4);
        }
    }

    // Everyone walks their path; the children wait (crouched where they were) until someone calls to them or the bell rings.
    void MoveMovers(Evac e, float dt)
    {
        foreach (var m in e.People)
        {
            if (m.Safe || !m.Going) continue;
            if (m.Leg >= m.Path.Length) { m.Safe = true; m.Face = "down"; continue; }
            var (tx, ty) = m.Path[m.Leg];
            float dx = tx - m.X, dy = ty - m.Y, d = MathF.Sqrt(dx * dx + dy * dy), step = m.Speed * dt;
            if (d <= step) { m.X = tx; m.Y = ty; m.Leg++; continue; }
            m.X += dx / d * step; m.Y += dy / d * step;
            m.WalkT += dt;
            m.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
        }
    }

    // E near anyone still waiting: one call sends everyone in earshot up the route.
    void Shout()
    {
        var e = evac;
        if (e == null || e.Phase != "evacuate") return;
        var near = e.People.Where(m => !m.Going && Dist(m.X, m.Y, player.X, player.Y) < ShoutR).ToList();
        if (near.Count == 0) return;
        foreach (var m in near) { m.Going = true; m.Warned = true; Floater("Up we go!", m.X, m.Y - 16, "#7fd36b"); }
        e.Shouted += near.Count;
        e.ShoutT = 1.2f;
        Sfx.Play("whistle");
        Floater("Up to School Rise!", player.X, player.Y - 26, "#ffd76a");
    }

    void PulledUp()
    {
        Sfx.Play("splash");
        FadeThrough(() =>
        {
            player.X = RiseX - 12; player.Y = RiseY + 6; player.Face = "down";
        }, () => Talk(new()
        {
            new("Niko", "I've got you! Up here, quick. That's the water off the reef coming over the low ground."),
            new("Niko", "After a big shake by the sea, never go back down toward it. Straight up, and stay up.")
        }));
    }

    /* ---------- The drill ---------- */
    void StartEvacDrill()
    {
        evac = new Evac { Drill = true, Phase = "evacuate" };
        AddMovers(evac);
        Sfx.Play("clang");
        Floater("Ding! Ding! Ding!", SchoolX, SchoolY - 40, "#ffd76a");
        Toast("The drill! Mia, Jun and Bea are playing in the square. Head up the signposted route, and call to them on your way (<act> near them).", 7);
    }

    void CancelDrill()
    {
        evac = null;
        Toast("The drill's called off. Ask Ma'am Isay to run it again.", 4);
    }

    void FinishDrill()
    {
        var e = evac;
        evac = null;
        state.hinted["rs:drill"] = true;
        state.gifts["rs_drill"] = state.day;
        Sfx.Play("coin");
        state.coins += 30;
        Save();
        Talk(new()
        {
            new("Ma'am Isay", $"Everyone's up! {(e.T < 40 ? "And quickly, too." : "")} That's all a drill is: knowing the way before you need it, and calling to people as you go."),
            new("Ma'am Isay", "Here's something for the signs: 30 coins. If the ground ever shakes hard by the sea, don't wait to see what the water does. Come straight up here."),
            new("Mia", "And stay up there until the officials say it's safe!")
        });
    }

    /* ---------- The end ---------- */
    // Everyone's up and the big waves have come and gone: the win is kept at once, then time passes until the radio
    // gives the all-clear.
    void WaitForAllClear()
    {
        var e = evac;
        e.Phase = "wait";
        state.hinted["rs:done"] = true;
        state.gifts["rs_done"] = state.day;
        state.hinted["rs:ducked"] = e.Ducked;
        state.hinted["rs:dry"] = !e.Caught;
        // Up before the sea came back over the low ground, on your own (Codex: staying dry somewhere else isn't that).
        state.hinted["rs:quick"] = !e.Caught && e.UpAt >= 0 && e.UpAt < SeaBackAt;
        state.hinted["rs:called"] = e.Shouted > 0;
        state.coins += 150;
        GainXp(60);
        Save();
        FadeThrough(() =>
        {
            evac = null;
            cocos.Clear();
            Advance(60, quiet: true);
            player.X = RiseX + 10; player.Y = RiseY + 4; player.Face = "down";
        }, () => Talk(new()
        {
            new("", "An hour passes on School Rise. The sea goes out and comes in again, and again, smaller each time."),
            new("Ma'am Isay", "That was the radio: the town's disaster office says the waves have passed and it's safe to go home. Stay off the beach today; the currents are still strong."),
            new("Lira", "My lola's post, my lola's story... and all of us up here, safe."),
            new("Niko", "In Lola's story, crabs frightened the berberoka. Today our plan kept us safe. Come and see me tomorrow; I've got something for you."),
            new("Ma'am Isay", "We don't know how the berberoka story began. But the signs it remembers were worth remembering."),
            new("", SeaReport())
        }, ShowSeaCard));
    }

    string SeaReport()
    {
        var good = new List<string>();
        var tips = new List<string>();
        (state.Hinted("rs:ducked") ? good : tips).Add(state.Hinted("rs:ducked") ? "ducked, covered and held on in the open" : "next time, get into the open, then duck, cover and hold");
        if (state.Hinted("rs:quick")) good.Add("got up to School Rise before the sea came back");
        else tips.Add(state.Hinted("rs:dry") ? "next time, go up to School Rise as soon as the shaking stops" : "next time, go straight up and stay up: never back down toward the sea");
        (state.Hinted("rs:called") ? good : tips).Add(state.Hinted("rs:called") ? "called to the children on your way" : "next time, call to anyone you pass");
        string s = good.Count > 0 ? "You " + string.Join(", ", good) + "." : "";
        if (tips.Count > 0) s += (s == "" ? "" : " ") + char.ToUpperInvariant(tips[0][0]) + tips[0][1..] + (tips.Count > 1 ? "; " + string.Join("; ", tips.Skip(1)) : "") + ".";
        return s;
    }

    void ShowSeaCard()
    {
        catchOpenedAt = Raylib.GetTime();
        mode = "seacard";
        SetPrompt("");
    }

    void CloseSeaCard()
    {
        if (mode != "seacard" || Raylib.GetTime() - catchOpenedAt < 0.6) return;
        mode = "play";
        Sfx.Play("ui");
        Toast("Case closed (Amihan): the restless sea. 150 coins from the village. Niko has something for you tomorrow.", 6);
    }

    void DrawSeaCard()
    {
        if (GoldCard("tsunami", "The restless sea", "Case closed",
            "The ground shook hard, the sea ran out past the reef, and then it came back over the low ground, more than once. By then all of Amihan Village was up on School Rise, because they knew the signs.",
            SeaReport(), "Salamat!")) CloseSeaCard();
    }

    /* ---------- What you can do during it ---------- */
    Target SeaEventTarget()
    {
        if (Shaking)
            return InTheOpen ? new Target { Type = "duck", Label = "Duck, cover your head and hold on (hold)" }
                : new Target { Type = "info", Label = "Get into the open, away from palms and houses" };
        var e = evac;
        if (e?.Phase != "evacuate") return null;
        var near = e.People.Where(m => !m.Going && Dist(m.X, m.Y, player.X, player.Y) < ShoutR).ToList();
        if (near.Count > 0) return new Target { Type = "shout", Label = $"Call to {string.Join(" and ", near.Select(m => m.Name))}: \"Up to School Rise!\"" };
        if (OnRise) return new Target { Type = "info", Label = e.Drill ? "Wait for the children on School Rise" : "Stay up here on School Rise, together" };
        return new Target { Type = "info", Label = "Up to School Rise! Follow the green signs" };
    }

    /* ---------- The sea going out and coming back ---------- */
    // The waterline, in pixels from the shore (out to sea is positive): out past the reef, then the first wave over the
    // low ground, back, out again and a second, bigger one. Pixels further out than this are sea; nearer, dry seabed;
    // a negative waterline floods the land up to that far in.
    float Waterline(float t)
    {
        (float t, float w)[] keys = { (13, 0), (21, 70), (23, 70), (31, 0), (36, -60), (41, 0), (45, 45), (51, 0), (56, -80), (SeaOverAt, 0) };
        if (t <= keys[0].t || t >= keys[^1].t) return 0;
        for (int i = 1; i < keys.Length; i++)
            if (t <= keys[i].t)
            {
                var (t0, w0) = keys[i - 1]; var (t1, w1) = keys[i];
                return w0 + (w1 - w0) * (t - t0) / (t1 - t0);
            }
        return 0;
    }
    float SeaLine => SeaEmergency && evac.Phase == "evacuate" ? Waterline(evac.T) : 0;
    bool WaveComing => SeaEmergency && evac.Phase == "evacuate" && evac.T is > 23 and < 36 or > 45 and < 56;

    // Distances from the west shore (Codex: an ocean-connected mask, so the pond is never a wave source): water pixels
    // west of the island hold how far out they are, land pixels how far in from that west shore (negative). Everything
    // else is short.MaxValue, never touched. Made once, from the baked ground.
    const int SeaBoxX = 1410, SeaBoxY = 60, SeaBoxW = 360, SeaBoxH = 310;
    short[] seaField;

    short[] SeaField()
    {
        if (seaField != null) return seaField;
        var f = new short[SeaBoxW * SeaBoxH];
        Array.Fill(f, short.MaxValue);
        bool Sea(char g) => g is '~' or 'w';
        bool West(int x, int y)
        {
            float a = MathF.Atan2(y - 220, x - 1660) * 180 / MathF.PI;
            if (a < 0) a += 360;
            return a is > 150 and < 210;   // the west coast, from the landing's north to its south (not the north beach)
        }
        char G(int x, int y) => shape[(SeaBoxY + y) * PW + SeaBoxX + x];
        // Distances in tenths of a pixel, 10 straight and 14 diagonally, so they come out close to true distances (a plain
        // eight-way count makes diagonals short, and let the wave reach School Rise from the north-west).
        var dist = new int[SeaBoxW * SeaBoxH];
        var q = new PriorityQueue<int, int>();
        void Spread(Func<int, int, bool> pass)
        {
            while (q.TryDequeue(out int i, out int di))
            {
                if (di > dist[i]) continue;
                int x = i % SeaBoxW, y = i / SeaBoxW;
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if ((ox == 0 && oy == 0) || nx < 0 || ny < 0 || nx >= SeaBoxW || ny >= SeaBoxH || !pass(nx, ny)) continue;
                        int j = ny * SeaBoxW + nx, dj = di + (ox != 0 && oy != 0 ? 14 : 10);
                        if (dj >= dist[j]) continue;
                        dist[j] = dj;
                        q.Enqueue(j, dj);
                    }
            }
        }
        // Water: how far out from any land.
        Array.Fill(dist, int.MaxValue);
        for (int y = 0; y < SeaBoxH; y++)
            for (int x = 0; x < SeaBoxW; x++)
                if (!Sea(G(x, y))) { dist[y * SeaBoxW + x] = 0; q.Enqueue(y * SeaBoxW + x, 0); }
        Spread((x, y) => Sea(G(x, y)));
        for (int y = 0; y < SeaBoxH; y++)
            for (int x = 0; x < SeaBoxW; x++)
                if (Sea(G(x, y)) && West(SeaBoxX + x, SeaBoxY + y) && dist[y * SeaBoxW + x] < 1200) f[y * SeaBoxW + x] = (short)Math.Max(1, (dist[y * SeaBoxW + x] + 5) / 10);
        // Land: how far in from the west sea's edge, through land only (the pond counts as land).
        Array.Fill(dist, int.MaxValue);
        for (int y = 0; y < SeaBoxH; y++)
            for (int x = 0; x < SeaBoxW; x++)
                if (f[y * SeaBoxW + x] is >= 1 and <= 2) { dist[y * SeaBoxW + x] = 0; q.Enqueue(y * SeaBoxW + x, 0); }
        Spread((x, y) => !Sea(G(x, y)));
        for (int y = 0; y < SeaBoxH; y++)
            for (int x = 0; x < SeaBoxW; x++)
                if (!Sea(G(x, y)) && dist[y * SeaBoxW + x] is > 0 and < 1300) f[y * SeaBoxW + x] = (short)-((dist[y * SeaBoxW + x] + 5) / 10);
        return seaField = f;
    }

    short SeaFieldAt(float wx, float wy)
    {
        int x = (int)MathF.Floor(wx) - SeaBoxX, y = (int)MathF.Floor(wy) - SeaBoxY;
        if (x < 0 || y < 0 || x >= SeaBoxW || y >= SeaBoxH) return short.MaxValue;
        return SeaField()[y * SeaBoxW + x];
    }

    // Under water right now that wasn't before: the low ground the wave has come over.
    bool FloodAt(float x, float y)
    {
        float w = SeaLine;
        short d = SeaFieldAt(x, y - 1);
        return w < 0 && d <= 0 && d > w;
    }

    static readonly Color[] Seabed = { Pal.C("#7a6a4c"), Pal.C("#8a7856"), Pal.C("#968462") };
    static readonly Color SeabedPool = Pal.C("#3f7f96"), SeabedCoral = Pal.C("#c98478"), SeabedRock = Pal.C("#5f5a50");

    // Ground level, after the water: the dry seabed with its stranded fish, the foam where the sea is, and the flood.
    void DrawRestlessSea(float t)
    {
        DrawPond(t);
        float w = SeaLine;
        if (w == 0) return;
        var f = SeaField();
        bool wave = WaveComing;
        int x0 = Math.Max(SeaBoxX, camX), x1 = Math.Min(SeaBoxX + SeaBoxW, camX + W), y0 = Math.Max(SeaBoxY, camY), y1 = Math.Min(SeaBoxY + SeaBoxH, camY + H);
        for (int wy = y0; wy < y1; wy++)
            for (int wx = x0; wx < x1; wx++)
            {
                short d = f[(wy - SeaBoxY) * SeaBoxW + wx - SeaBoxX];
                if (d == short.MaxValue) continue;
                if (w > 0 && d > 0 && d <= w)
                {
                    // Seabed left dry: wet sand and mud, pools, coral heads and rocks.
                    float v = VNoise(wx, wy, 7, 311);
                    Color c = v > 0.76f ? SeabedPool : Seabed[v < 0.35f ? 0 : v < 0.6f ? 1 : 2];
                    if (Pix.Hash(wx / 3, wy / 3, 312) < 0.035) c = SeabedCoral;
                    else if (Pix.Hash(wx / 2, wy / 2, 313) < 0.02) c = SeabedRock;
                    if (d >= w - 1.5f) c = Pal.C("#e8f4f8");
                    pix.Rect(wx, wy, 1, 1, c);
                }
                else if (w < 0 && d <= 0 && d > w)
                {
                    // Sea water over the land: pale and churning near its edge, deeper blue-green behind, with streaks of
                    // foam running inland.
                    float k = Math.Clamp((d - w) / 30f, 0, 1);
                    pix.Rect(wx, wy, 1, 1, Pal.Rgba((int)(110 - 62 * k), (int)(176 - 50 * k), (int)(186 - 40 * k), 0.86f));
                    if ((VNoise(wx, wy * 2, 4, 314) + t * 0.6f + wx * 0.01f) % 1 < 0.06f) pix.Rect(wx, wy, 1, 1, Pal.Rgba(232, 246, 250, 0.75f));
                }
                // The wave's face, white and churning, wherever the water's edge is moving in.
                float edge = d - w;
                if (wave && edge > -2 && edge <= 3 + (Pix.Hash(wx, wy / 2, 315) * 3) && (d > 0 || w < 0)) pix.Rect(wx, wy, 1, 1, Pal.Rgba(245, 252, 255, 0.9f));
            }
        // Fish stranded on the dry seabed, flapping.
        if (w > 0)
            foreach (var (fx, fy) in StrandedFish())
            {
                short d = SeaFieldAt(fx, fy);
                if (d <= 0 || d > w - 2 || fx < camX - 4 || fx > camX + W + 4 || fy < camY - 4 || fy > camY + H + 4) continue;
                int flip = (int)((t * 5 + fx * 0.3f) % 2);
                pix.Rect(fx - 2, fy - flip, 4, 1, "#c8d4dc"); pix.Rect(fx - 1, fy - 1 - flip, 2, 1, "#9fb0bc");
                pix.Rect(flip == 0 ? fx + 2 : fx - 3, fy - 1, 1, 1, "#9fb0bc");
            }
    }

    List<(float x, float y)> strandedFish;
    List<(float x, float y)> StrandedFish()
    {
        if (strandedFish != null) return strandedFish;
        var list = new List<(float, float)>();
        var f = SeaField();
        for (int y = 0; y < SeaBoxH; y += 7)
            for (int x = 0; x < SeaBoxW; x += 7)
            {
                short d = f[y * SeaBoxW + x];
                if (d is >= 8 and <= 60 && Pix.Hash(x, y, 321) < 0.12) list.Add((SeaBoxX + x, SeaBoxY + y));
            }
        return strandedFish = list;
    }

    /* ---------- The bangus pond ---------- */
    // Drained through a crack in its bank after the tremor; once Niko has mended it, it refills on the tide by the next
    // morning (Codex: drained, then refilling, then fishable, each its own state).
    bool PondDry => SeaStoryStarted && !state.Hinted("rs:pondFixed");
    bool PondRefilling => state.Hinted("rs:pondFixed") && state.day <= state.gifts.GetValueOrDefault("rs_pondFixed");
    bool PondClosed => PondDry || PondRefilling;

    List<(int x, int y)> pondTiles;
    List<(int x, int y)> PondTiles()
    {
        if (pondTiles != null) return pondTiles;
        var list = new List<(int, int)>();
        var seen = new HashSet<(int, int)> { (164, 24) };
        var stack = new Stack<(int, int)>(seen);
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (worldMap[y, x] != 'l') continue;
            list.Add((x, y));
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (seen.Add((x + ox, y + oy))) stack.Push((x + ox, y + oy));
        }
        return pondTiles = list;
    }

    void DrawPond(float t)
    {
        if (!PondClosed || Math.Abs(1640 - player.X) > W || Math.Abs(240 - player.Y) > H) return;
        // Dry: mud, with the bangus crowded into the puddles left in the deepest part. Refilling: water creeping back.
        float fill = PondRefilling ? 0.5f : 0;
        foreach (var (tx, ty) in PondTiles())
            for (int py = 0; py < T; py++)
                for (int px = 0; px < T; px++)
                {
                    int wx = tx * T + px, wy = ty * T + py;
                    if (shape[wy * PW + wx] != 'l') continue;
                    float r = MathF.Sqrt(MathF.Pow((wx - 1645) / 30f, 2) + MathF.Pow((wy - 240) / 20f, 2));
                    if (r < 0.32f + fill) continue;   // still water in the middle
                    float v = VNoise(wx, wy, 5, 331);
                    pix.Rect(wx, wy, 1, 1, v > 0.7f ? "#6f5a3e" : v > 0.4f ? "#7d6646" : "#8a7250");
                }
        if (PondDry)
            for (int i = 0; i < 4; i++)
            {
                float fx = 1638 + i * 4, fy = 238 + (i % 2) * 3, flip = (int)((t * 4 + i) % 2);
                pix.Rect(fx, fy - flip, 3, 1, "#c8d4dc");
            }
    }

    /* ---------- Coconuts shaken loose (looks only) ---------- */
    void ShakeLoose(float dt)
    {
        var q = evac.Quake;
        q.NextCoco -= dt;
        if (q.NextCoco > 0) return;
        q.NextCoco = FxRand(0.25f, 0.6f);
        var palms = trees.Where(p => p.kind is 'h' && Dist(p.x * T + 5, p.y * T + 5, player.X, player.Y) < 120).ToList();
        if (palms.Count == 0) return;
        var p = palms[fxRng.Next(palms.Count)];
        cocos.Add(new Coco { X = p.x * T + 5 + FxRand(-6, 6), Y = p.y * T + 9 + FxRand(-2, 4), Fall = 22 });
    }

    void UpdateCocos(float dt)
    {
        for (int i = cocos.Count - 1; i >= 0; i--)
        {
            var c = cocos[i];
            c.T += dt;
            if (c.T > 1.6f) cocos.RemoveAt(i);
        }
    }

    void DrawCocos()
    {
        foreach (var c in cocos)
        {
            float k = Math.Min(1, c.T / 0.45f), y = c.Y - c.Fall * (1 - k * k);
            if (k >= 1) y -= MathF.Max(0, MathF.Sin((c.T - 0.45f) * 9) * 3 * (1 - (c.T - 0.45f)));
            pix.Rect(c.X - 1, c.Y, 3, 1, "rgba(0,0,0,0.25)");
            pix.Rect(c.X - 1, y - 2, 3, 3, "#6b4a2b"); pix.Rect(c.X - 1, y - 2, 2, 1, "#8a6440");
        }
    }

    /* ---------- The signs, the post, and School Rise ---------- */
    // Drawn with the standing things outdoors: the route's signs (once you've put them up), the old post, the rise's
    // board, and the people on their way up.
    void AddRestlessSeaObjects(List<(float y, Action draw)> list)
    {
        if (Math.Abs(1600 - player.X) > W + 60 || Math.Abs(190 - player.Y) > H + 60) return;
        list.Add((PostY, DrawOldPost));
        for (int i = 0; i < SignSpots.Length; i++)
        {
            int k = i;
            var (sx, sy) = SignSpots[k];
            if (state.Hinted($"rs:sign{k}")) list.Add((sy, () => DrawRouteSign(sx, sy, k)));
            else if (state.Hinted("rs:signs") && (time * 1.5f + k * 0.3f) % 1 < 0.5f) list.Add((sy, () => pix.Rect(sx - 1, sy - 1, 3, 2, "#7fd36b")));
        }
        if (state.Hinted("rs:signs")) list.Add((RiseY - 14, DrawRiseBoard));
        if (evac != null)
            foreach (var m in evac.People)
            {
                if (m.Kid >= 0 && m.Safe) continue;   // back on the bench (DrawSchoolBench)
                var mm = m;
                list.Add((m.Y, () => DrawMover(mm)));
            }
        list.Add((9999, DrawCocos));
    }

    void DrawMover(Mover m)
    {
        int x = (int)MathF.Round(m.X), y = (int)MathF.Round(m.Y);
        bool duck = Shaking;
        int step = m.Going && !m.Safe ? 1 + (int)(m.WalkT * 8) % 4 : 0;
        if (m.Kid >= 0)
        {
            DrawKid((px, py, pw, ph, c) => pix.Rect(px, py, pw, ph, c), x, y, m.Kid, duck || !m.Going ? 2 : step % 2 == 0 ? 0 : 1, time, stand: !duck);
            return;
        }
        var look = new Look { skin = 2, shirt = m.Id == "niko" ? 2 : 1, hat = 0, hair = 1 };
        if (duck) { DrawDucked(look, x, y, m.Face); return; }
        LookData.DrawPerson(pix, look, x, y, m.Face, step);
        int up = step is 2 or 4 ? 1 : 0;
        pix.Rect(x - 5, y - 14 - up, 11, 2, "#ddbc78");
        pix.Rect(x - 3, y - 16 - up, 7, 2, "#f0d596");
    }

    // Ducked down low with your forearms up round your head and your hands clasped over it, while the ground shakes.
    void DrawDucked(Look look, int x, int y, string face)
    {
        LookData.DrawPerson(pix, look, x, y, face, 0, bob: -3);
        string skin = LookData.Skins[look.skin % LookData.Skins.Length], shirt = LookData.Shirts[look.shirt % LookData.Shirts.Length];
        pix.Rect(x - 4, y - 9, 1, 3, shirt); pix.Rect(x + 3, y - 9, 1, 3, shirt);
        pix.Rect(x - 3, y - 11, 3, 1, skin); pix.Rect(x + 1, y - 11, 2, 1, skin);
    }

    // A green route sign on a post: a running figure and an arrow up the route.
    void DrawRouteSign(float sx, float sy, int i)
    {
        int x = (int)sx, y = (int)sy;
        pix.Rect(x, y - 12, 1, 12, "#6b4a2b");
        pix.Rect(x - 4, y - 17, 10, 6, "#2f8a4a");
        pix.Rect(x - 4, y - 17, 10, 1, "#5fb57a");
        // The figure, running toward the arrow.
        pix.Rect(x - 2, y - 16, 1, 1, "#ffffff"); pix.Rect(x - 2, y - 15, 2, 2, "#ffffff"); pix.Rect(x - 3, y - 13, 1, 1, "#ffffff"); pix.Rect(x, y - 13, 1, 1, "#ffffff");
        var (nx, ny) = i + 1 < SignSpots.Length ? SignSpots[i + 1] : (RiseX, RiseY);
        bool right = nx > sx + 2, up = ny < sy - 4;
        if (right) { pix.Rect(x + 2, y - 15, 3, 1, "#ffffff"); pix.Rect(x + 4, y - 16, 1, 3, "#ffffff"); }
        else if (up) { pix.Rect(x + 3, y - 16, 1, 4, "#ffffff"); pix.Rect(x + 2, y - 15, 3, 1, "#ffffff"); }
        else { pix.Rect(x + 2, y - 15, 3, 1, "#ffffff"); pix.Rect(x + 2, y - 16, 1, 3, "#ffffff"); }
    }

    // The board at the top: Evacuation area.
    void DrawRiseBoard()
    {
        int x = (int)RiseX + 28, y = (int)RiseY - 14;
        pix.Rect(x - 6, y - 10, 1, 12, "#6b4a2b"); pix.Rect(x + 6, y - 10, 1, 12, "#6b4a2b");
        pix.Rect(x - 8, y - 16, 17, 8, "#2f8a4a");
        pix.Rect(x - 8, y - 16, 17, 1, "#5fb57a");
        // A little figure on a hill, and three lines of "writing".
        pix.Rect(x - 6, y - 10, 5, 1, "#ffffff"); pix.Rect(x - 5, y - 11, 3, 1, "#ffffff"); pix.Rect(x - 4, y - 14, 1, 3, "#ffffff");
        for (int i = 0; i < 3; i++) pix.Rect(x + 1, y - 14 + i * 2, 6 - i, 1, "#d8f0e0");
    }

    // The old post by the landing: weathered wood, a bell on top, and a notch cut high up.
    void DrawOldPost()
    {
        int x = (int)PostX, y = (int)PostY;
        pix.Rect(x - 2, y, 5, 1, "rgba(0,0,0,0.25)");
        pix.Rect(x - 1, y - 26, 3, 26, "#7a5a3e");
        pix.Rect(x - 1, y - 26, 1, 26, "#9a7a56");
        pix.Rect(x - 3, y - 28, 7, 2, "#6b4a2b");
        // The bell.
        pix.Rect(x - 1, y - 32, 3, 4, "#c98b3a"); pix.Rect(x - 2, y - 29, 5, 1, "#a8742a"); pix.Rect(x, y - 33, 1, 1, "#6b4a2b");
        // The notch, and the old date scratched beside it.
        pix.Rect(x - 1, y - 21, 3, 1, "#3a2a1a");
        if (state.Hinted("rs:markTold") && !state.Hinted("rs:mark") && (time * 0.8f) % 1 < 0.15f) pix.Rect(x + 2, y - 23, 1, 1, "#ffffff");
    }

    // The rise itself: a slope of greener grass with stone steps where the route comes up (drawn on the ground).
    void DrawRiseGround()
    {
        if (Math.Abs(RiseX - player.X) > W || Math.Abs(RiseY - player.Y) > H) return;
        for (int i = 0; i < 3; i++)
        {
            int sx = (int)SignSpots[2].x + 6 + i * 6, sy = (int)SignSpots[2].y - 2 - i * 2;
            pix.Rect(sx, sy, 5, 2, "#a89a84"); pix.Rect(sx, sy, 5, 1, "#c8bca4");
        }
    }

    /* ---------- Story targets on the island ---------- */
    // The old post, the crack in the pond's bank and the sign spots, when there's something to do or see there.
    Target RestlessSeaTarget()
    {
        if (!InAmihan || Aboard || evac != null || tremor != null) return null;
        if (Dist(player.X, player.Y, PostX, PostY + 6) < 13)
            return new Target { Type = "seapost", Label = state.Hinted("rs:markTold") && !state.Hinted("rs:mark") ? "Look at the mark on the old post" : "Look at the old post" };
        if (PondDry && Dist(player.X, player.Y, CrackX, CrackY) < 14)
            return new Target { Type = "info", Label = "A crack runs through the pond's bank. The water ran out through it" };
        if (state.Hinted("rs:signs"))
            for (int i = 0; i < SignSpots.Length; i++)
                if (!state.Hinted($"rs:sign{i}") && Dist(player.X, player.Y, SignSpots[i].x, SignSpots[i].y + 5) < 13)
                    return new Target { Type = "seasign", Tx = i, Label = "Put up an evacuation route sign" };
        return null;
    }

    void LookAtPost()
    {
        FaceToward(PostX, PostY);
        if (state.Hinted("rs:markTold") && !state.Hinted("rs:mark"))
        {
            state.hinted["rs:mark"] = true;
            Save();
            Talk(new()
            {
                new("You", "High on the post, well above your head, a notch is cut into the wood. Beside it, scratched letters, worn almost smooth."),
                new("You", "Lira's lola was a girl when the sea came up this far. The beach is a long way below."),
                new("", "New clue on your Case board (Amihan): The mark on the post.")
            });
            return;
        }
        Talk(new() { new("You", state.Hinted("rs:mark")
            ? "The notch is still there, high above the beach. Once, the sea came up this far."
            : "An old post by the landing, with a bell on top. Someone cut a notch into it, high up.") });
    }

    void PutUpSign(int i)
    {
        state.hinted[$"rs:sign{i}"] = true;
        Swing("hands", 0.3f);
        Sfx.Play("build");
        mapTexDirty = true;
        Save();
        Toast(SignsUp < SignSpots.Length ? $"Sign up: {SignsUp} of {SignSpots.Length}. Each one points the way up to School Rise."
            : "All three signs are up, from the landing to School Rise. Tell Ma'am Isay.", 4);
    }

    /* ---------- The islanders' parts ---------- */
    // Lira (after her supper request): her lola's Bernardo Carpio story after the tremor, then the post.
    bool LiraSeaTalk()
    {
        Say L(string t) => new("Lira", t);
        if (state.Hinted("rs:berberoka") && !state.Hinted("rs:markTold"))
        {
            state.hinted["rs:markTold"] = true;
            Save();
            Talk(new()
            {
                L("Niko told you his lola's berberoka story? Then come and see the old post by the landing. There's a notch cut high up on it."),
                L("My lola was a girl when it was cut. After a great shaking, the sea ran out past the reef, and people went down after the fish."),
                L("Then the water came back, up as far as that notch. She never forgot it. Go and look.")
            });
            return true;
        }
        if (SeaStoryStarted && !state.Hinted("rs:carpioTale"))
        {
            state.hinted["rs:carpioTale"] = true;
            Talk(new()
            {
                L("Did you feel that? My lola, who came from Rizal, had a Tagalog story for it."),
                L("Bernardo Carpio, a giant, trapped between great rocks in the mountains of Montalban. In her telling, the ground shook whenever he struggled."),
                L("Ma'am Isay will tell you what really moves the ground. She's been teaching the children all about it.")
            });
            return true;
        }
        return false;
    }

    // Niko: his lola's berberoka story when the pond drains, then the stone and wood for its bank, then a crab pot.
    bool NikoSeaTalk()
    {
        Say N(string t) => new("Niko", t);
        if (SeaCaseClosed && state.day > state.gifts.GetValueOrDefault("rs_done") && !state.Hinted("rs:crabpot"))
        {
            state.hinted["rs:crabpot"] = true;
            Give("crab_pot");
            Save();
            Talk(new() { N("For you: a crab pot. In Lola's story the berberoka was afraid of crabs. Set it in the shallows on a calm day, and leave the berberoka to the stories.") });
            return true;
        }
        if (SeaStoryStarted && !state.Hinted("rs:berberoka"))
        {
            state.hinted["rs:berberoka"] = true;
            Save();
            Talk(new()
            {
                N("The bangus pond's nearly empty! The kids say the berberoka drank it."),
                N("My lola came from Abra, up north. She told a story from there, a northern Luzon story, recorded in Apayao too. This was her version:"),
                N("The berberoka held back the water so the fish lay stranded on the mud. When people went down to pick them up, it let the water go."),
                N("But look: the shaking cracked the pond's bank, and the water ran out through it at low tide. Strong shaking can do that to a pond bank."),
                N("Once it's safe, we'll mend it properly. Bring me 6 stone and 2 wood for the bank, and the tide will fill it again."),
                new("", "New clue on your Case board (Amihan): The berberoka's trick.")
            });
            return true;
        }
        if (PondDry)
        {
            // Something he asked for earlier, in your bag: that hand-in comes first (Codex: this reminder swallowed it).
            bool handIn = state.Hinted("niko_request") ? !state.Hinted("niko_asohos") && Has("asohos") >= NikoAsohos : Has("bangus") >= 2;
            if (handIn && !(Has("stone") >= 6 && Has("wood") >= 2)) return false;
            if (Has("stone") >= 6 && Has("wood") >= 2)
            {
                Take("stone", 6); Take("wood", 2);
                state.hinted["rs:pondFixed"] = true;
                state.gifts["rs_pondFixed"] = state.day;
                Sfx.Play("build");
                Save();
                Talk(new()
                {
                    N("Salamat! We checked the whole bank first, then packed the crack with stone and stakes. It'll fill on the tide."),
                    N("Give it till tomorrow, then fish it again. The bangus are fine in the deep part.")
                });
            }
            else Talk(new() { N($"The pond's bank needs 6 stone and 2 wood. You have {Has("stone")} stone and {Has("wood")} wood.") });
            return true;
        }
        return false;
    }

    string NikoSeaLabel() =>
        SeaCaseClosed && state.day > state.gifts.GetValueOrDefault("rs_done") && !state.Hinted("rs:crabpot") ? "Niko has something for you"
        : SeaStoryStarted && !state.Hinted("rs:berberoka") ? "Ask Niko about the bangus pond"
        : PondDry && Has("stone") >= 6 && Has("wood") >= 2 ? "Give Niko 6 stone and 2 wood for the pond" : null;

    string LiraSeaLabel() => state.Hinted("rs:berberoka") && !state.Hinted("rs:markTold") ? "Ask Lira about the old post" : null;

    // Ma'am Isay: the earthquake after the tremor; the tsunami lesson once you've the other three clues; the drill.
    bool IsaySeaTalk()
    {
        Say I(string t) => new("Ma'am Isay", t);
        if (SeaStoryStarted && !state.Hinted("rs:carpio"))
        {
            state.hinted["rs:carpio"] = true;
            Save();
            Talk(new()
            {
                I(state.Hinted("rs:duckedTremor") ? "You felt that! And you did it right: into the open, then duck, cover your head and hold on." : "You felt that! Next time: into the open, away from palms and houses, then duck, cover your head and hold on."),
                I("Earthquakes happen when rock deep underground suddenly slips along a crack called a fault. The slow movement of the Earth's plates builds up the strain."),
                I("The Philippines lies on the Pacific Ring of Fire, where earthquakes and volcanoes are common. PHIVOLCS, our institute of volcanology and seismology, records them every day; most are too small to feel."),
                I("Lira's lola had a story for it, about Bernardo Carpio. Stories are how people remembered things. That one was small; it's the strong ones by the sea we prepare for."),
                new("", "New clue on your Case board (Amihan): Bernardo Carpio's shrug.")
            });
            return true;
        }
        if (state.Hinted("rs:carpio") && state.Hinted("rs:berberoka") && state.Hinted("rs:mark") && !state.Hinted("rs:signs"))
        {
            state.hinted["rs:signs"] = true;
            mapTexDirty = true;
            Save();
            Talk(new()
            {
                I("A berberoka that holds back the water, then lets it go. A sea that ran out past the reef, then came back up to Lira's post. Those remembered signs fit a tsunami."),
                I("We don't know how the berberoka story began. But a tsunami is a series of big waves, usually set off when an undersea earthquake moves the seabed."),
                I("PHIVOLCS says: Shake, Drop, Roar. Strong shaking by the sea; the sea suddenly dropping away or rising; a roar from the sea. Any one sign is enough. Don't wait to see the others."),
                I("Ordinary tides take hours to go out: about six hours from high to low where there are two tides a day. Don't stand there timing it. Head uphill as soon as you can move safely."),
                I("On 17 August 1976, a magnitude 8.1 earthquake made a tsunami that devastated towns around the Moro Gulf in Mindanao. It struck after midnight, while many were sleeping."),
                I("If you're at the landing, leave the boat and go uphill. Don't try to sail away from a tsunami that's close."),
                I("Our evacuation area is the open ground here on School Rise. Help me mark the route: put up these three signs, from the landing to here. Then we'll run a drill."),
                new("", "New clue on your Case board (Amihan): One sign is enough. The sign spots are marked with green.")
            });
            return true;
        }
        if (state.Hinted("rs:signs") && SignsUp == SignSpots.Length && !state.Hinted("rs:drill"))
        {
            Talk(new()
            {
                I("All three signs are up! Now the drill. Mia, Jun and Bea are out in the square."),
                I("When the bell rings, walk the route up here, and call to them on your way. Ready?")
            }, StartEvacDrill);
            return true;
        }
        if (state.Hinted("rs:signs") && SignsUp < SignSpots.Length)
        {
            Talk(new() { I($"Put up the signs at the green marks between the landing and here: {SignsUp} of {SignSpots.Length} so far.") });
            return true;
        }
        return false;
    }

    string IsaySeaLabel() =>
        SeaStoryStarted && !state.Hinted("rs:carpio") ? "Ask Ma'am Isay about the shaking"
        : state.Hinted("rs:carpio") && state.Hinted("rs:berberoka") && state.Hinted("rs:mark") && !state.Hinted("rs:signs") ? "Tell Ma'am Isay what you've found"
        : state.Hinted("rs:signs") && SignsUp == SignSpots.Length && !state.Hinted("rs:drill") ? "Run the drill with Ma'am Isay" : null;
}
