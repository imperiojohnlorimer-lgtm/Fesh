namespace Fesh;

// Bakawan's night lights (1.15). On the island's west shore stand three pagatpat, the mangroves that fireflies favour:
// after dark, unless it's raining, the alitaptap gather in them and flash together, the whole tree at once. And on nights
// when the moon is small, the water around the island glows wherever something stirs it: behind your boat, around
// Tidemane, at a wader's feet. Both are only watched. Tala keeps a list of them, with the tarsiers and the hornbills,
// and it's the second card on the Fish log's Sightings (the sanctuary's animals are the first).
sealed class GlowSpark { public float X, Y, Life, Max; }

partial class Game
{
    // Where each pagatpat's trunk meets the shore (world pixels).
    static readonly (float x, float y)[] FireflyTrees = { (1757, 538), (1753, 563), (1756, 587) };
    const float BakawanCX = 190, BakawanCY = 56;            // the island's middle, in tiles
    const float GlowRX = 23, GlowRY = 18;                   // the glowing water around it, in tiles
    const float FireflyBeat = 0.9f;                         // they flash together about once a second
    readonly List<GlowSpark> glowTrail = new();
    float glowSpawn;

    static readonly Dictionary<string, (string name, string about)> BakawanKinds = new()
    {
        ["tarsier"] = ("Philippine tarsier", "You stay back. Two enormous eyes watch you from the shade."),
        ["hornbill"] = ("Rufous hornbill", "You watch quietly as the hornbill tilts its bright bill toward the canopy."),
        ["alitaptap"] = ("Alitaptap (fireflies)",
            "Thousands of fireflies fill the pagatpat and flash together, the whole tree at once, like a slow heartbeat. They only gather where the mangroves are left standing."),
        ["plankton"] = ("Glowing plankton",
            "The water lights up blue-green wherever it's stirred: plankton too small to see, glowing when they're disturbed. It shows best on nights when the moon is small.")
    };

    /* ---------- Sightings ---------- */
    // Watching something on Joy's or Tala's list: a count, a floater, a little XP the first time, and a nudge to tell
    // whoever keeps the list once you've seen everything on it.
    void RecordSighting(string kind, float x, float y, string text)
    {
        bool first = state.sightings.GetValueOrDefault(kind) == 0;
        state.sightings[kind] = state.sightings.GetValueOrDefault(kind) + 1;
        Floater(first ? "New sighting!" : "Sighting", x, y, "#bff4ff");
        if (first) GainXp(10);
        bool tala = BakawanKinds.ContainsKey(kind);
        var list = tala ? BakawanKinds.Keys.ToList() : SeaKinds.Keys.ToList();
        bool all = first && list.All(k => state.sightings.GetValueOrDefault(k) > 0);
        Toast(text + (all ? tala ? $" That's all {CountWord(list.Count)} on Tala's list! Tell her." : $" That's all {CountWord(list.Count)}! Tell Bantay Joy." : first && tala ? " (New on Tala's list.)" : ""), 5);
        Save();
    }

    static string CountWord(int n) => n switch { 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", _ => n.ToString() };

    bool TalaListDone => BakawanKinds.Keys.All(k => state.sightings.GetValueOrDefault(k) > 0);

    void TalkTala()
    {
        Say Ta(string t) => new("Tala", t);
        if (TalaListDone && !state.Hinted("talaReward"))
        {
            state.hinted["talaReward"] = true;
            bool bobber = Has("glow_bobber") == 0;
            state.coins += bobber ? 100 : 140;
            if (bobber) Give("glow_bobber");
            Sfx.Play("coin");
            Save();
            Talk(new()
            {
                Ta("All four! The tarsiers, the hornbills, the alitaptap and the glowing water. And you watched them the right way: quietly, from a distance."),
                Ta(bobber ? "Here: 100 coins from the village's visitor fund, and a glow bobber, so you can see your line on those dark nights."
                    : "Here: 140 coins from the village's visitor fund. Salamat for being gentle with them.")
            });
            return;
        }
        var lines = new List<Say>
        {
            Ta("Bakawan means mangrove. Its roots shelter young fish; the pools here hold hito, dalag and mudskippers."),
            Ta("Watch the tiny tarsiers and the hornbills quietly. The carabao near the village is used to people, but the wild animals need their space.")
        };
        // The first time: she shows you the bubo, a bamboo fish trap, and gives you one (Minigames.cs).
        if (!state.Hinted("bubo"))
        {
            state.hinted["bubo"] = true;
            Give("bubo");
            lines.Add(Ta("Here, take this. It's a bubo: split bamboo woven into a long basket. The fish swim in through the mouth and can't find the way out."));
            lines.Add(Ta("This kind is made for fresh water: a pond, a lake, or the pools back in the mangroves. Set it, and lift it in the morning. Out at sea, use a crab pot."));
            lines.Add(new("", "You can weave more bubo at a workbench (4 wood). It's the last piece in the build bar (<build>)."));
            Save();
        }
        // The next time: what to look for after dark.
        else if (!state.Hinted("fireflies"))
        {
            state.hinted["fireflies"] = true;
            Save();
            lines.Add(Ta("Come back after dark. On the west shore there are three pagatpat trees where the alitaptap gather: thousands of fireflies, blinking together like one heart. Not in the rain, though."));
            lines.Add(Ta("And on nights when the moon is small, the water around Bakawan glows wherever it's stirred. Sail through it and look behind your boat."));
            lines.Add(Ta("I keep a list of what visitors see here: the tarsiers, the hornbills, the alitaptap and the glowing water. Watch them all, then come and tell me."));
        }
        else
        {
            var missing = BakawanKinds.Where(k => state.sightings.GetValueOrDefault(k.Key) == 0).Select(k => k.Key switch
            {
                "tarsier" => "a tarsier", "hornbill" => "a hornbill", "alitaptap" => "the alitaptap in the pagatpat", _ => "the glowing water"
            }).ToList();
            lines.Add(Ta(missing.Count == 0 ? "The alitaptap were bright last night. Come and watch them again any time."
                : $"You've seen {BakawanKinds.Count - missing.Count} of the four on my list. Still to see: {string.Join(missing.Count == 2 ? " and " : ", ", missing)}."));
        }
        Talk(lines);
    }

    /* ---------- The alitaptap ---------- */
    bool FirefliesOut => Night && state.weather == "clear";

    int FireflyTreeNear(float reach)
    {
        if (scene != "world") return -1;
        int best = -1;
        float bd = reach;
        for (int i = 0; i < FireflyTrees.Length; i++)
        {
            float d = Dist(player.X, player.Y, FireflyTrees[i].x, FireflyTrees[i].y);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // On foot: after dark the trees offer watching; by day (or in the rain) a closer look says when to come back.
    Target FireflyTarget()
    {
        if (FirefliesOut && FireflyTreeNear(26) is int i and >= 0) return new Target { Type = "fireflies", Id = i.ToString(), Label = "Watch the alitaptap" };
        if (FireflyTreeNear(15) < 0) return null;
        return new Target { Type = "info", Label = Night ? "No alitaptap in the rain tonight" : "Pagatpat trees. Tala says the alitaptap gather here after dark" };
    }

    void WatchFireflies(int i)
    {
        var (x, y) = FireflyTrees[i];
        FaceToward(x, y);
        Sfx.Play("pet");
        // Tala's list counts them once a night.
        if (state.gifts.GetValueOrDefault("alitaptap") == state.day)
        {
            Toast("The whole tree blinks on and off together, over and over. You could watch it all night.", 3);
            return;
        }
        state.gifts["alitaptap"] = state.day;
        RecordSighting("alitaptap", x, y - 26, BakawanKinds["alitaptap"].about);
    }

    // The flash: a quick bright pulse every beat, shared by every tree.
    float FireflyFlash(float t)
    {
        float ph = t % FireflyBeat / FireflyBeat;
        return ph < 0.2f ? 1 - ph / 0.2f : 0;
    }

    // Each flash lights the crowns a little (before the dark is laid over the world).
    void LightFireflies(float t)
    {
        if (!FirefliesOut) return;
        float flash = FireflyFlash(t);
        if (flash <= 0) return;
        foreach (var (x, y) in FireflyTrees)
            if (MathF.Abs(x - player.X) < W && MathF.Abs(y - player.Y) < H) LightHole(x, y - 18, 16, 0.35f * flash);
    }

    // The fireflies themselves, drawn over the dark: most flash in step, and a few stragglers blink on their own.
    void DrawFireflies(float t, float k)
    {
        if (!FirefliesOut) return;
        float flash = FireflyFlash(t);
        for (int i = 0; i < FireflyTrees.Length; i++)
        {
            var (x, y) = FireflyTrees[i];
            if (x < camX - 30 || x > camX + W + 30 || y < camY - 10 || y > camY + H + 40) continue;
            for (int j = 0; j < 30; j++)
            {
                float a = (float)Pix.Hash(i, j, 411) * MathF.Tau, r = MathF.Sqrt((float)Pix.Hash(i, j, 412));
                float fx = x + MathF.Cos(a) * r * 12, fy = y - 18 + MathF.Sin(a) * r * 8;
                bool straggler = j % 8 == 0;
                float on = straggler ? (MathF.Sin(t * 3.1f + j * 1.7f) > 0.75f ? 0.8f : 0) : flash;
                on *= k;
                if (on < 0.05f) continue;
                pix.Rect(fx, fy, 1, 1, Pal.Rgba(232, 255, 150, on));
                pix.Glow(fx + 0.5f, fy + 0.5f, 3, Pal.Rgba(200, 255, 120, 0.28f * on));
            }
        }
    }

    // A pagatpat: arching roots in the shallows, a short grey trunk and a broad, dark, glossy crown. Like the island's
    // other trees it fades while you're standing behind it.
    void DrawPagatpat(float x, float y, bool live = true)
    {
        int bx = (int)x, by = (int)y;
        bool fade = live && player.Y < by - 1 && player.Y > by - 28 && MathF.Abs(player.X - bx) < 13;
        pix.Rect(bx - 8, by - 1, 17, 2, Pal.Rgba(18, 46, 40, 0.24f));
        float keep = pix.Alpha;
        if (fade) pix.Alpha = 0.4f;
        try
        {
            // Pencil roots poking up out of the mud all round it, and a few arching roots off the trunk.
            for (int k = 0; k < 9; k++)
            {
                int px = bx - 10 + (int)(Pix.Hash(bx, k, 421) * 21), py = by - 1 + (int)(Pix.Hash(bx, k, 422) * 4);
                if (Math.Abs(px - bx) < 2) continue;
                pix.Rect(px, py, 1, 2, "#8a7a62"); pix.Rect(px, py, 1, 1, "#a8987c");
            }
            foreach (int r in new[] { -6, -3, 3, 6 })
            {
                pix.Line(bx, by - 7, bx + r, by + 1, "#8a7660");
                pix.Rect(bx + r, by + 1, 1, 1, "#5e4c3a");
            }
            pix.Rect(bx - 1, by - 13, 3, 9, "#7a6a58");
            pix.Rect(bx - 1, by - 13, 1, 9, "#9a8a74");
            pix.Line(bx, by - 11, bx - 5, by - 15, "#7a6a58");
            int sw = live ? (int)MathF.Round(MathF.Sin(time * 0.9f + bx * 0.1f) * 0.6f) : 0;
            var leaf = new[] { Pal.C("#1d3f26"), Pal.C("#27512e"), Pal.C("#356a3a"), Pal.C("#4f8a4a") };
            var lobes = new[] { (0f, -19f, 8f), (-7f, -16f, 5.5f), (7f, -16.5f, 5.5f), (-3f, -23f, 5f), (4f, -22.5f, 4.5f) };
            foreach (var (ox, oy, r) in lobes) Disc(bx + ox + sw, by + oy + 1, r + 1, leaf[0]);
            foreach (var (ox, oy, r) in lobes) Disc(bx + ox + sw, by + oy + 0.5f, r, leaf[1]);
            foreach (var (ox, oy, r) in lobes) Disc(bx + ox + sw - 0.5f, by + oy - 0.5f, r - 1, leaf[2]);
            foreach (var (ox, oy, r) in lobes) Disc(bx + ox + sw - 1.5f, by + oy - 2, r * 0.45f, leaf[3]);
        }
        finally { pix.Alpha = keep; }
    }

    IEnumerable<Box> FireflyTreeSolids() => FireflyTrees.Select(p => new Box(p.x - 2, p.y - 3, 4, 3));

    /* ---------- The glowing water ---------- */
    // Too much moonlight drowns it out (the nights either side of the full moon), and a storm churns it away.
    bool GlowNight => Night && MoonPhase is not (3 or 4 or 5) && !Stormy;

    bool InGlowWater(float x, float y)
    {
        if (scene != "world") return false;
        float dx = (x / T - BakawanCX) / GlowRX, dy = (y / T - BakawanCY) / GlowRY;
        // The sea only: the mangrove pool inland is fresh.
        return dx * dx + dy * dy < 1 && TileUnder(x, y) is '~' or 'w';
    }

    // Whatever is moving through the water leaves sparks behind it that glow and fade: the boat's wake and bow wave,
    // Tidemane's, or the swirl at a wader's feet. The first time each night, Tala's list counts it.
    void UpdateGlow(float dt)
    {
        for (int i = glowTrail.Count - 1; i >= 0; i--)
            if ((glowTrail[i].Life -= dt) <= 0) glowTrail.RemoveAt(i);
        if (scene != "world") { glowTrail.Clear(); return; }
        bool stirring = GlowNight && (player.Moving && mode == "play" || towing) && InGlowWater(player.X, player.Y);
        if (!stirring) return;
        var (fx, fy) = player.Face switch { "left" => (-1f, 0f), "right" => (1f, 0f), "up" => (0f, -1f), _ => (0f, 1f) };
        if (Aboard && towing) (fx, fy) = boatFace switch { "left" => (-1f, 0f), "right" => (1f, 0f), "up" => (0f, -1f), _ => (0f, 1f) };
        float len = Aboard ? 11 : Riding ? 9 : 3;
        glowSpawn += dt;
        while (glowSpawn > 0.012f)
        {
            glowSpawn -= 0.012f;
            // Mostly the wake behind; now and then the bow wave off either side.
            bool bow = fxRng.Next(4) == 0;
            float along = bow ? len + FxRand(0, 3) : -len - FxRand(0, 7);
            float side = bow ? (fxRng.Next(2) == 0 ? -1 : 1) * FxRand(5, 8) : FxRand(-1, 1) * (3 + MathF.Abs(along) * 0.35f);
            float x = player.X + fx * along - fy * side, y = player.Y + (Aboard ? 3 : 0) + fy * along + fx * side;
            if (InGlowWater(x, y)) glowTrail.Add(new GlowSpark { X = x, Y = y, Life = FxRand(1.2f, 2.6f), Max = 2.6f });
        }
        if (glowTrail.Count > 500) glowTrail.RemoveRange(0, glowTrail.Count - 500);
        if (mode == "play" && state.gifts.GetValueOrDefault("plankton") != state.day)
        {
            state.gifts["plankton"] = state.day;
            RecordSighting("plankton", player.X, player.Y - 16, BakawanKinds["plankton"].about);
        }
    }

    void DrawGlowTrail(float t, float k)
    {
        foreach (var g in glowTrail)
        {
            if (g.X < camX - 4 || g.X > camX + W + 4 || g.Y < camY - 4 || g.Y > camY + H + 4) continue;
            // Not over the hull you're sitting in.
            if (Aboard && MathF.Abs(g.X - player.X) < 12 && g.Y > player.Y - 7 && g.Y < player.Y + 8) continue;
            float a = MathF.Min(1, g.Life / g.Max * 1.3f) * k * (0.75f + 0.25f * MathF.Sin(t * 11 + g.X * 1.3f));
            pix.Rect(g.X, g.Y, 1, 1, Pal.Rgba(175, 255, 240, a));
            if (a > 0.3f) pix.Glow(g.X + 0.5f, g.Y + 0.5f, 4, Pal.Rgba(60, 215, 230, 0.22f * a));
        }
    }

    // On glowing nights the surf breaking on Bakawan's shore lights up blue too.
    void DrawGlowingSurf(float t, float k)
    {
        if (!GlowNight || Dist(player.X, player.Y, BakawanCX * T, BakawanCY * T) > 520) return;
        int x0 = camX - 1, x1 = camX + W + 1, y0 = camY - 1, y1 = camY + H + 1;
        foreach (var (sx, sy, _, _, w) in shore)
        {
            if (sx < x0 || sx > x1 || sy < y0 || sy > y1 || w == 'm' || !InGlowWater(sx, sy + 1.5f)) continue;
            double ph = (Pix.Hash(sx / 4, sy / 4, 9) + t * 0.25) % 1;
            if (ph > 0.3) continue;
            pix.Rect(sx, sy, 1, 1, Pal.Rgba(140, 250, 236, (float)(0.75 * (1 - ph / 0.3)) * k));
        }
    }
}
