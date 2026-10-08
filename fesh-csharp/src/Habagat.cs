using Raylib_cs;

namespace Fesh;

// The Habagat islands: a band of fictional Philippine-inspired islands along the bottom of the original map (rows 56 and
// down, west of Amihan). Asinan's salt beds (Saltworks.cs), the Daang Pulo islets (the regatta, gleaning and sungka:
// Regatta.cs, Sungka.cs), and the Parola lighthouse, where the moon goes missing (Bakunawa.cs). A deep channel keeps
// waders out; you sail there from the atoll's jetty, swim on Tidemane, or take the Sail route once you've found it.
partial class Game
{
    public const int HabagatTop = 56;
    bool InHabagat => scene == "world" && player.Y >= HabagatTop * T && player.X < EastStart * T;

    // Asinan and Parola, and the home islet of Daang Pulo, with its scatter of small islets.
    static readonly (string name, float cx, float cy, float rx, float ry)[] HabagatIslands =
    {
        ("Asinan", 22, 64.5f, 13, 6.2f), ("Parola", 118, 64, 10.5f, 6), ("Daang Pulo", 64, 62.5f, 5, 3.2f)
    };
    static readonly (float cx, float cy, float rx, float ry)[] Islets =
    {
        (49, 59.5f, 2.2f, 1.6f), (54, 66.5f, 2.4f, 1.8f), (47, 70, 2f, 1.6f), (58, 71, 2.6f, 1.8f), (66, 68.5f, 2f, 1.5f), (72, 59.5f, 2.4f, 1.7f),
        (75, 65, 2.2f, 1.6f), (70, 72, 2f, 1.5f), (80, 61, 2f, 1.6f), (82, 68.5f, 2.4f, 1.8f), (88, 64, 2f, 1.5f)
    };
    // Every islet you can chart: the home islet (index 0) and the small ones.
    int IsletCount => Islets.Length + 1;

    // Vertical jetties, carved over water only: Asinan's landing, Dado's on the home islet, and the Parola pier.
    static readonly (int x, int y0, int y1)[] HabagatJetties = { (22, 54, 58), (64, 56, 58), (113, 53, 58) };
    public const float AsinanJettyX = 225, AsinanJettyY = 566;
    const float AsinanBoatX = 225, AsinanBoatY = 530, AsinanWakeX = 196, AsinanWakeY = 662;

    // Fishing spots (Data.Spots reads these).
    public const float AsinanSpotX = 85, AsinanSpotY = 650, PuloSpotX = 630, PuloSpotY = 668, ParolaSpotX = 1120, ParolaSpotY = 548;

    // Landmarks, in pixels: the salt beds, the lighthouse and the islanders' houses.
    const float SaltBedX = 120, SaltBedY = 620, SaltBedW = 70, SaltBedH = 30;
    const float LighthouseX = 1175, LighthouseY = 605;
    static readonly (string id, string name, float x, float y, string shirt, bool house)[] HabagatFolk =
    {
        ("rosa", "Manang Rosa", 225, 642, "#d9734f", true),
        ("pacing", "Lola Pacing", 612, 632, "#8a5fb5", false),
        ("dado", "Dado", 655, 617, "#2f7fa3", true),
        ("celso", "Tatay Celso", 1238, 640, "#5e6468", true)
    };

    static double IsleNoise6(int x, int y, int s) => 0.09 * Math.Sin(x * .8 + y * .6 + s) + .06 * Math.Cos(y * 1.2 - x * .3 + s * 2);

    void GenerateHabagat()
    {
        for (int y = HabagatTop; y < ROWS - 1; y++)
            for (int x = 1; x < EastStart; x++)
            {
                double best = 9;
                bool islet = false;
                for (int i = 0; i < HabagatIslands.Length; i++)
                {
                    var h = HabagatIslands[i];
                    double dx = (x + 0.5 - h.cx) / h.rx, dy = (y + 0.5 - h.cy) / h.ry, d = dx * dx + dy * dy + IsleNoise6(x, y, i + 3);
                    if (d < best) { best = d; islet = false; }
                }
                for (int i = 0; i < Islets.Length; i++)
                {
                    var s = Islets[i];
                    double dx = (x + 0.5 - s.cx) / s.rx, dy = (y + 0.5 - s.cy) / s.ry, d = dx * dx + dy * dy + 0.6 * IsleNoise6(x, y, i);
                    if (d < best) { best = d; islet = true; }
                }
                map[y, x] = islet ? (best < .4 ? 'j' : best < 1 ? 's' : best < 1.45 ? 'w' : '~') : (best < .72 ? 'g' : best < 1 ? 's' : best < 1.3 ? 'w' : '~');
                biome[y, x] = 6;
            }
        // (The jetties reach up past HabagatTop into water that belongs to the islands to the north: they're Habagat's.)
        foreach (var (jx, y0, y1) in HabagatJetties)
            for (int y = y0; y <= y1; y++)
                if (map[y, jx] is '~' or 'w') { map[y, jx] = 'b'; biome[y, jx] = 6; }

        // Palms along the beaches, calamansi bushes and the odd boulder inland, kept clear of the landmarks, the
        // villagers, the jetties and the fishing spots.
        for (int y = HabagatTop; y < ROWS - 1; y++)
            for (int x = 1; x < EastStart; x++)
            {
                char t = map[y, x];
                if (t is not ('g' or 's') || HabagatClear(x, y)) continue;
                double roll = Pix.Hash(x, y, 287);
                char kind = t == 's' ? (roll < .1 ? 'h' : '\0') : roll < .05 ? 'y' : roll < .07 ? 'R' : roll < .1 ? 'h' : '\0';
                if (kind == '\0') continue;
                if (t == 'g' && (map[y - 1, x] != 'g' || map[y + 1, x] != 'g' || map[y, x - 1] != 'g' || map[y, x + 1] != 'g') && kind != 'h') continue;
                string key = $"{x},{y}";
                if (state.felled.TryGetValue(key, out int day))
                {
                    if (state.day - day < (kind == 'R' ? BoulderRegrowDays : TreeRegrowDays) || RegrowBlocked(x, y))
                    { stumps[(x, y)] = kind; continue; }
                    state.felled.Remove(key);
                }
                map[y, x] = kind; trees.Add((x, y, kind));
            }
        GenerateSanctuary();
    }

    // Tiles that must stay open: around the landmarks and people, next to jetties, and at the fishing spots.
    bool HabagatClear(int x, int y)
    {
        float px = x * T + 5, py = y * T + 5;
        if (HabagatSolids().Any(b => new Box(b.X - 14, b.Y - 14, b.W + 28, b.H + 28).Overlaps(new Box(x * T, y * T, T, T)))) return true;
        if (HabagatFolk.Any(n => Dist(px, py, n.x, n.y) < 30)) return true;
        if (HabagatJetties.Any(j => Math.Abs(x - j.x) <= 2 && y >= j.y0 - 1 && y <= j.y1 + 3)) return true;
        if (Data.Spots.Any(s => s.Scene == "world" && s.Biome == "habagat" && Dist(px, py, s.X, s.Y) < s.R + 12)) return true;
        return Islets.Any(s => Dist(px, py, s.cx * T, s.cy * T) < 20);
    }

    // The salt beds, the lighthouse, the islanders' houses, each islet's limestone rock and the sanctuary's watch hut
    // are solid. They never move, so the list is made once (on first use: static fields in other files of this partial
    // class may not be set yet while the type initialises).
    static List<Box> habagatSolids;
    static List<Box> MakeHabagatSolids()
    {
        var list = new List<Box> { new(SaltBedX, SaltBedY, SaltBedW, SaltBedH), new(LighthouseX - 8, LighthouseY - 9, 16, 9) };
        list.AddRange(HabagatFolk.Where(n => n.house).Select(n => new Box(n.x - 16, n.y - 22, 32, 19)));
        for (int i = 0; i < Islets.Length; i++) list.Add(IsletRock(i));
        list.Add(WatchHut);
        return list;
    }
    static List<Box> HabagatSolids() => habagatSolids ??= MakeHabagatSolids();

    static Box IsletRock(int i) => new(Islets[i].cx * T - 4, Islets[i].cy * T - 3, 8, 5);

    /* ---------- Older saves ---------- */
    // The Habagat islands rose out of what used to be open sea. A boat moored there (or Tidemane waiting there) in a save
    // from before them would be stuck in the land: the boat goes back to Pip's jetty (or the atoll's, if that's where
    // StartGame has put you), and Tidemane comes to you. Judged on the outdoor map whatever room you're in.
    int RescueStranded()
    {
        char W(float x, float y)
        {
            int tx = (int)MathF.Floor(x / T), ty = (int)MathF.Floor(y / T);
            return tx <= 0 || ty <= 0 || tx >= COLS - 1 || ty >= ROWS - 1 ? '#' : worldMap[ty, tx];   // the map's edge is out
        }
        int n = 0;
        if (Has("boat") > 0 && !state.aboard && state.boatX > 0 && state.boatY > 0
            && new[] { (-5f, -3f), (5f, -3f), (-5f, 3f), (5f, 3f) }.Any(o => W(state.boatX + o.Item1, state.boatY + o.Item2) is not ('~' or 'w' or 'l' or 'm' or 'o')))
        {
            // To the jetty you're at (StartGame has just put you at the atoll's or on Saltmere if you were out there too).
            state.boatAt = Dist(player.X, player.Y, AtollJettyX, AtollJettyY) < 40 ? "atoll" : "saltmere";
            state.boatX = state.boatY = 0;
            n++;
        }
        bool MountFits(float x, float y) => (Walkable(W(x, y - 1.5f)) || Swimmable(W(x, y - 1.5f)))
            && !HabagatSolids().Concat(state.builds.Select(BuildBox).OfType<Box>()).Any(b => b.Overlaps(new Box(x - 6, y - 4, 12, 4)));
        if (state.tamed && !state.riding && (state.mountX != 0 || state.mountY != 0) && !MountFits(state.mountX, state.mountY))
        {
            var spot = new[] { (12f, 0f), (-12f, 0f), (0f, 10f), (0f, -10f), (20f, 0f), (-20f, 0f), (0f, 18f) }
                .Select(o => (x: player.X + o.Item1, y: player.Y + o.Item2)).FirstOrDefault(p => MountFits(p.x, p.y), (x: player.X, y: player.Y));
            (state.mountX, state.mountY) = spot;
        }
        return n;
    }

    /* ---------- Finding it ---------- */
    void DiscoverHabagat()
    {
        if (!InHabagat || state.Hinted("habagat")) return;
        state.hinted["habagat"] = true;
        Toast("The Habagat islands! Asinan's salt beds to the west, the Daang Pulo islets, and a dark lighthouse to the east. They're on your chart now.", 7);
        mapTexDirty = true;
        Save();
    }

    // Sailing close by an islet charts it (they're too small and too many to land on every one).
    void ChartNearIslets()
    {
        if (scene != "world" || state.charted == null || player.Y < (HabagatTop - 2) * T) return;
        for (int i = 0; i <= Islets.Length; i++)
        {
            var (cx, cy) = i == 0 ? (HabagatIslands[2].cx, HabagatIslands[2].cy) : (Islets[i - 1].cx, Islets[i - 1].cy);
            string region = $"habagat:islet:{i}";
            if (state.charted.Contains(region) || Dist(player.X, player.Y, cx * T, cy * T) > (i == 0 ? 80 : 50)) continue;
            state.charted.Add(region);
            mapTexDirty = true;
            if (IsletsCharted < IsletCount) Toast($"Charted an islet of Daang Pulo ({IsletsCharted} of {IsletCount}).", 2.4f);
        }
        // (Landing on one charts it too, through ChartHere.)
        if (IsletsCharted >= IsletCount && !state.Hinted("isletsCharted"))
        {
            state.hinted["isletsCharted"] = true;
            Sfx.Play("rare");
            Toast("Every islet of Daang Pulo is on your chart! Dado will race you now.", 5);
            Save();
        }
    }

    int IsletsCharted => state.charted?.Count(r => r.StartsWith("habagat:islet:")) ?? 0;

    // The region a Habagat tile belongs to: Asinan, Parola, or one of Daang Pulo's islets (the home islet is 0).
    string HabagatRegion(int tx, int ty)
    {
        float x = tx + 0.5f, y = ty + 0.5f;
        double D(float cx, float cy, float rx, float ry) => (cx - x) * (cx - x) / (rx * rx) + (cy - y) * (cy - y) / (ry * ry);
        double best = double.MaxValue;
        string r = "habagat:Asinan";
        foreach (var h in HabagatIslands)
        {
            double d = D(h.cx, h.cy, h.rx, h.ry);
            if (d < best) { best = d; r = h.name == "Daang Pulo" ? "habagat:islet:0" : "habagat:" + h.name; }
        }
        for (int i = 0; i < Islets.Length; i++)
        {
            double d = D(Islets[i].cx, Islets[i].cy, Islets[i].rx, Islets[i].ry);
            if (d < best) { best = d; r = $"habagat:islet:{i + 1}"; }
        }
        return r;
    }

    /* ---------- Gleaning at low tide ---------- */
    // Twice a day the tide drops (around dawn and dusk) and the reef flats of Habagat's beaches are worth walking:
    // cowries, sea urchins and sea grapes turn up on the wet sand by the water ("glean" loose finds, picked up by walking
    // over them). The tide coming back in takes whatever's left.
    bool LowTide => state.clock is >= 4.5f * 60 and < 8.5f * 60 or >= 16.5f * 60 and < 20.5f * 60;
    const int GleanQuota = 10;   // finds per low tide

    // Which low tide it is: the evening one belongs to its day, and the morning one to the day it ends in (it starts
    // before the 06:00 day change).
    string GleanTide => state.clock >= 16.5f * 60 ? $"{state.day}:pm" : $"{(state.clock < DawnMin ? state.day + 1 : state.day)}:am";
    bool GleanLeft => state.gleanTide != GleanTide || state.gleaned < GleanQuota;

    bool GleanGround(int x, int y) => map[y, x] == 's' && BiomeAt(x, y) == 6 && x < EastStart
        && new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(o => TileAt(x + o.Item1, y + o.Item2) is 'w' or '~');

    void PickGlean(Loose l)
    {
        if (state.gleanTide != GleanTide) { state.gleanTide = GleanTide; state.gleaned = 0; }
        state.gleaned++;
        double r = rng.NextDouble();
        string what = r < 0.45 ? "cowrie" : r < 0.75 ? "sea_urchin" : "sea_grapes";
        Give(what);
        Sfx.Play("pickup");
        Floater($"+1 {Items.ById[what].Name.Split(' ')[0].ToLowerInvariant()}", l.x, l.y - 6, "#f2e6c8");
        if (!state.Hinted("glean"))
        {
            state.hinted["glean"] = true;
            Toast("Gleaning at low tide! The falling tide leaves cowries, sea urchins and sea grapes on the flats. They're gone when the water comes back.", 6);
        }
        // (TickLoose clears the rest of the flats once the tide's quota is used up.)
        else if (!GleanLeft) Toast("You've gleaned the flats clean for this tide. Come back at the next low tide.", 4);
    }

    /* ---------- The people ---------- */
    readonly Dictionary<string, Stroller> habagatWalk = new();

    Stroller HabagatWalk(string id)
    {
        if (habagatWalk.TryGetValue(id, out var s)) return s;
        var n = HabagatFolk.First(f => f.id == id);
        // Lola Pacing stays by her sungka board; the others stroll in front of their houses.
        s = new Stroller { HomeX = n.x, HomeY = n.y + 2, X = n.x, Y = n.y + 2, RangeX = id == "pacing" ? 0 : 12, RangeDown = id == "pacing" ? 0 : 6, Notice = 26 };
        habagatWalk[id] = s;
        return s;
    }

    void UpdateHabagatFolk(float dt)
    {
        foreach (var n in HabagatFolk)
        {
            var s = HabagatWalk(n.id);
            if (Dist(s.X, s.Y, player.X, player.Y) < 400) Stroll(s, dt, (x, y) => FolkCanStand(x, y));
        }
    }

    Target HabagatTarget()
    {
        if (scene != "world" || Aboard || player.Y < (HabagatTop - 1) * T || player.X >= EastStart * T) return null;
        foreach (var n in HabagatFolk)
        {
            var s = HabagatWalk(n.id);
            if (Dist(player.X, player.Y, s.X, s.Y + 6) < 15)
                return new Target { Type = "habagatfolk", Id = n.id, Label = n.id switch
                {
                    "pacing" => "Play sungka with Lola Pacing",
                    "celso" when ParolaReady => $"Give Tatay Celso the {ParolaNeeds}",
                    _ => $"Talk to {n.name}"
                } };
        }
        if (SaltBedsInReach()) return new Target { Type = "saltbed", Label = "Rake the salt beds" };
        return null;
    }

    void TalkHabagat(string id)
    {
        FaceToward(HabagatWalk(id).X, HabagatWalk(id).Y);
        switch (id)
        {
            case "rosa": TalkRosa(); break;
            case "pacing": TalkPacing(); break;
            case "dado": TalkDado(); break;
            case "celso": TalkCelso(); break;
        }
    }

    void TalkRosa()
    {
        Say R(string t) => new("Manang Rosa", t);
        if (!state.Hinted("metRosa"))
        {
            state.hinted["metRosa"] = true;
            Talk(new()
            {
                R("Welcome to Asinan! You came across the channel? Then you'll want salt for the way home."),
                R("We let the sea into these beds and the sun does the rest. On a dry day the salt comes up white; rain melts it back into brine."),
                R("Rake the beds once a day, if it hasn't rained since morning. Tomas, or Pip, can tell you if tomorrow will be dry."),
                R("Salt your fish and dry them on a rack in the sun: daing keeps for a whole voyage. Build a rack anywhere, and keep it out of the rain.")
            });
            return;
        }
        Talk(new()
        {
            R(Stormy || state.weather == "rain" ? "Rain on the beds again. We wait, and we hope tomorrow is dry." : RakedToday ? "You've raked today. The beds need another day of sun." : "The beds look dry. Go on, rake them!"),
            R("Calamansi grows wild here. A squeeze of it on fresh fish with a pinch of salt, and you have kinilaw.")
        });
    }

    void TalkDado()
    {
        Say D(string t) => new("Dado", t);
        if (!state.Hinted("metDado"))
        {
            state.hinted["metDado"] = true;
            Talk(new()
            {
                D("Hoy! A sailor! Welcome to Daang Pulo. A hundred islets, they say, though I've only ever counted twelve."),
                D("Every fiesta we race our boats between them. My lolo won the old agong in that race."),
                D("Chart every islet first, so you know the water. Sail close by each one and it goes on your chart. Then come and race me.")
            });
            return;
        }
        if (!state.Hinted("isletsCharted"))
        {
            Talk(new() { D($"You've charted {IsletsCharted} of the {IsletCount} islets. Sail close by the rest, then we race.") });
            return;
        }
        TalkRegatta();
    }

    /* ---------- Drawing ---------- */
    // Live is the world view (only what's near, and the people); otherwise it's the map's picture of the whole world.
    void AddHabagatObjects(List<(float y, Action draw)> list, bool live = true)
    {
        bool Near(float x, float y) => !live || Math.Abs(x - player.X) < W && Math.Abs(y - player.Y) < H;
        for (int i = 0; i < Islets.Length; i++)
        {
            int k = i;
            if (Near(Islets[i].cx * T, Islets[i].cy * T)) list.Add((Islets[i].cy * T + 2, () => DrawIsletRock(k)));
        }
        if (Near(LighthouseX, LighthouseY)) list.Add((LighthouseY, DrawLighthouse));
        foreach (var n in HabagatFolk)
        {
            var who = n;
            // During the eclipse everyone has come to Parola with their pots.
            if (live && eclipse != null) list.Add((CrowdSpot[n.id].y, () => DrawCrowdPerson(who.id)));
            if (!Near(n.x, n.y)) continue;
            if (n.house) list.Add((n.y - 3, () => DrawBahay(who.x, who.y - 5, who.shirt)));
            else list.Add((n.y - 6, () => DrawSungkaBoard(who.x + 10, who.y - 4)));
            if (!live || eclipse != null) continue;
            var s = HabagatWalk(n.id);
            list.Add((s.Y, () => DrawHabagatPerson(who.id, s)));
        }
        AddSanctuaryObjects(list, live);
    }

    static (string skin, string hair, string shirt, string pants, bool longHair, int hat) FolkLook(string id) => id switch
    {
        "rosa" => ("#a9714b", "#2b1d14", "#d9734f", "#5b3a24", true, 0),
        "pacing" => ("#a9714b", "#e8e8e8", "#8a5fb5", "#3f5d8a", true, 0),
        "dado" => ("#6b4428", "#2b1d14", "#2f7fa3", "#2b3a4a", false, 3),
        _ => ("#d9a066", "#c8c8c8", "#5e6468", "#2b3a4a", false, 1)
    };

    void DrawHabagatPerson(string id, Stroller s)
    {
        int x = (int)MathF.Round(s.X), y = (int)MathF.Round(s.Y), bob = s.Moving ? 0 : (int)(time % 3 / 2);
        var (skin, hair, shirt, pants, longHair, hat) = FolkLook(id);
        LookData.DrawFigure(pix, skin, hair, shirt, pants, longHair, hat, "#e04b3a", x, y, s.Face, s.Step, bob: bob,
            blink: (time + x * 0.01f) % 4.1f < 0.12f);
        if (id == "rosa")
        {
            // A wide salakot against the sun.
            int up = bob + (s.Step is 2 or 4 ? 1 : 0);
            pix.Rect(x - 5, y - 14 - up, 11, 2, "#ddbc78");
            pix.Rect(x - 3, y - 16 - up, 7, 2, "#f0d596");
        }
    }

    // A mushroom of limestone, undercut at the waterline, with a cap of scrub on top.
    void DrawIsletRock(int i)
    {
        var r = IsletRock(i);
        int x = (int)(r.X + r.W / 2), y = (int)(r.Y + r.H);
        int h = 16 + (int)(Pix.Hash(i, 3, 41) * 8), m = Pix.Hash(i, 4, 41) < 0.5 ? 1 : -1;
        pix.Rect(x - 6, y - 1, 13, 2, "rgba(0,0,0,0.2)");
        for (int row = 0; row < h; row++)
        {
            // A rounded crown over a stem the waves have worn thin at the bottom.
            float k = row / (float)h;
            int half = k < 0.3f ? (int)MathF.Round(4 + 3.5f * MathF.Sin(k / 0.3f * MathF.PI / 2)) : (int)MathF.Round(7.5f - 4.5f * (k - 0.3f) / 0.7f);
            pix.Rect(x - half, y - h + row, half * 2, 1, row % 5 < 1 ? "#a8a594" : "#c4c1ab");
            pix.Rect(m > 0 ? x + half - Math.Max(1, half / 2) : x - half, y - h + row, Math.Max(1, half / 2), 1, "#8f8c7c");
            if (k > 0.3f && k < 0.36f) pix.Rect(x - half, y - h + row, half * 2, 1, "#7d7a6a");   // the shadow under the crown
        }
        pix.Rect(x - 3, y - 2, 6, 2, "#6e6b5c");
        pix.Rect(x - 6, y - h - 2, 12, 3, "#4f8a42"); pix.Rect(x - 4, y - h - 4, 8, 2, "#62a04f"); pix.Rect(x - 2 + m, y - h - 5, 3, 1, "#7cb860");
        pix.Rect(x - 7 * m, y - h + 1, 2, 3, "#4f8a42");
    }

    // A white tower with a red lantern room, and the lamp itself lit after dark once it's mended.
    void DrawLighthouse()
    {
        int x = (int)LighthouseX, y = (int)LighthouseY;
        pix.Rect(x - 10, y - 1, 21, 2, "rgba(0,0,0,0.22)");
        pix.Rect(x - 8, y - 6, 16, 6, "#8a8f93"); pix.Rect(x - 8, y - 6, 16, 1, "#b4b9bc");
        for (int row = 0; row < 34; row++)
        {
            int half = 6 - row / 9;
            pix.Rect(x - half, y - 6 - row, half * 2, 1, row % 11 < 5 ? "#f2efe6" : "#e04b3a");
            pix.Rect(x + half - 2, y - 6 - row, 2, 1, row % 11 < 5 ? "#d6d1c4" : "#b5423a");
        }
        int top = y - 40;
        pix.Rect(x - 5, top - 1, 10, 2, "#3b3b3b");
        bool lit = state.parola >= 3, glow = lit && Night;
        pix.Rect(x - 3, top - 6, 6, 5, glow ? "#ffe28a" : lit ? "#f3e2a0" : "#5e6468");
        pix.Rect(x - 1, top - 6, 2, 5, "#3b3b3b");
        pix.Rect(x - 4, top - 8, 8, 2, "#b5423a"); pix.Rect(x - 2, top - 10, 4, 2, "#b5423a"); pix.Rect(x, top - 12, 1, 2, "#3b3b3b");
        if (!lit) { pix.Rect(x - 3, top - 4, 2, 1, "#2a2a2a"); pix.Rect(x + 1, top - 5, 2, 1, "#2a2a2a"); }
        // The door, and a ladder of a stair the keeper is mending.
        pix.Rect(x - 2, y - 11, 4, 5, "#5b3a24");
        if (state.parola < 1) for (int i = 0; i < 3; i++) pix.Rect(x + 9, y - 3 - i * 3, 3, 1, "#7a5230");
    }

    // A wooden sungka board on a mat, its houses full of shells.
    void DrawSungkaBoard(float bx, float by)
    {
        int x = (int)bx, y = (int)by;
        pix.Rect(x - 8, y - 1, 17, 6, "#c9b071"); pix.Rect(x - 8, y - 1, 17, 1, "#e0c88a");
        pix.Rect(x - 7, y, 15, 4, "#8a5f36"); pix.Rect(x - 7, y, 15, 1, "#a8774a");
        for (int i = 0; i < 5; i++) { pix.Rect(x - 5 + i * 3, y + 1, 2, 1, "#4a2f1d"); pix.Rect(x - 5 + i * 3, y + 3, 2, 1, "#4a2f1d"); }
        pix.Rect(x - 7, y + 1, 1, 2, "#4a2f1d"); pix.Rect(x + 7, y + 1, 1, 2, "#4a2f1d");
        pix.Rect(x - 4, y + 1, 1, 1, "#f2e6c8"); pix.Rect(x + 2, y + 3, 1, 1, "#f2e6c8");
    }

    // The salt beds: shallow pans of brine between low dikes. On a dry day salt crusts white; raked today they're grey and
    // wet; in the rain they're flooded.
    void DrawSaltBeds(bool live = true)
    {
        if (live && (Math.Abs(SaltBedX - player.X) > W + 80 || Math.Abs(SaltBedY - player.Y) > H + 60)) return;
        int X = (int)SaltBedX, Y = (int)SaltBedY;
        bool wet = state.weather != "clear" || RainedToday, raked = RakedToday;
        pix.Rect(X - 1, Y - 1, (int)SaltBedW + 2, (int)SaltBedH + 2, "#8a6a42");
        for (int py = 0; py < 3; py++)
            for (int px = 0; px < 5; px++)
            {
                int x = X + 1 + px * 14, y = Y + 1 + py * 10;
                pix.Rect(x, y, 12, 8, wet ? "#7fb0c8" : raked ? "#b8c2c4" : "#dfe6e6");
                if (!wet && !raked)
                    for (int k = 0; k < 6; k++) pix.Rect(x + 1 + (int)(Pix.Hash(px * 7 + k, py, 301) * 10), y + 1 + (int)(Pix.Hash(px, py * 5 + k, 302) * 6), 1, 1, "#ffffff");
                if (wet) pix.Rect(x + 2, y + 2 + (int)(time * 2 + px) % 4, 4, 1, "#a8d0e0");
                if (raked && !wet) { pix.Rect(x + 2, y + 2, 8, 1, "#a8b2b4"); pix.Rect(x + 2, y + 5, 8, 1, "#a8b2b4"); }
            }
        // A pile of salt and a wooden rake at the end of the beds.
        pix.Rect(X + (int)SaltBedW + 3, Y + 14, 7, 3, "#eef1f3"); pix.Rect(X + (int)SaltBedW + 4, Y + 12, 5, 2, "#f8fafb");
        pix.Line(X + (int)SaltBedW + 4, Y + 24, X + (int)SaltBedW + 11, Y + 18, "#8a6440");
    }
}
