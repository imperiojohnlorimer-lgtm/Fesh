using Raylib_cs;

namespace Fesh;

// Amihan is a fictional Philippine-inspired archipelago east of the original map.
// It shares the outdoor ocean so the entire crossing is navigated, with no travel portal.
partial class Game
{
    const int EastStart = 140;
    const int NikoAsohos = 3;
    // The map panel's chart: 0 the west (Saltmere), 1 Amihan, 2 Marilaya (Chart.cs).
    int chart;
    bool chartEast { get => chart == 1; set => chart = value ? 1 : 0; }
    // Marilaya sits on the eastern chart too, but it isn't Amihan (ZoneAt, Marilaya.cs).
    bool InAmihan => scene == "world" && ZoneAt(player.X, player.Y) == "amihan";
    static readonly (string name, float cx, float cy, float rx, float ry)[] AmihanIslands =
    {
        ("Amihan Village", 166, 22, 15, 13),
        ("Luntian Karsts", 205, 16, 12, 10),
        ("Bakawan Island", 190, 56, 14, 10),
        ("Baga Island", 231, 46, 11, 13)
    };
    static readonly (string id, string name, float x, float y, string shirt)[] Islanders =
    {
        ("lira", "Lira", 1595, 185, "#e6a843"),
        ("niko", "Niko", 1715, 187, "#367caa"),
        ("maya", "Maya", 2045, 127, "#b45b66"),
        ("tala", "Tala", 1895, 597, "#72a752"),
        ("isay", "Ma'am Isay", SchoolX, SchoolY, "#d9a43a"),
        // Baga (Magayon.cs): Manay Mila in her abaca garden, and Ben at the volcano station.
        ("mila", "Manay Mila", MilaX, MilaY, "#c0392b"),
        ("ben", "Ben", BenX, BenY, "#2a8a8a")
    };

    // 1.34: each island grows (a bigger ellipse, its centre nudged toward where there's room) except on the sides where
    // something must stay on the coast as it was: the village's west landing, post and sign route; Bakawan's firefly trees
    // on its west shore and the sanctuary to its south-east; Baga's reef jetty, the danger zone's lahar mouth and the
    // station on its north side, and the sanctuary to its south. Same order as AmihanIslands.
    static readonly (float cx, float cy, float rx, float ry, (int x0, int y0, int x1, int y1)[] keep)[] AmihanGrowth =
    {
        (167.5f, 22f, 17f, 15f, new[] { (140, 0, 156, 40) }),
        (206.5f, 16f, 13.5f, 12f, Array.Empty<(int, int, int, int)>()),
        (191.5f, 54.5f, 16.5f, 12f, new[] { (140, 40, 180, 76), (197, 61, 252, 76) }),
        (229.5f, 47f, 13.5f, 14.5f, new[] { (222, 0, 252, 41), (210, 59, 252, 76) })
    };

    // A grown island's distance: the old one where a keep rectangle is near, the bigger one two tiles out from them.
    static double GrownD(double old, double big, int x, int y, (int x0, int y0, int x1, int y1)[] keep)
    {
        if (big >= old) return old;
        double near = 99;
        foreach (var (x0, y0, x1, y1) in keep)
        {
            double ox = Math.Max(0, Math.Max(x0 - x, x - x1)), oy = Math.Max(0, Math.Max(y0 - y, y - y1));
            near = Math.Min(near, Math.Sqrt(ox * ox + oy * oy));
        }
        return old + (big - old) * Math.Clamp((near - 0.5) / 2, 0, 1);
    }

    void GenerateArchipelago()
    {
        for (int y = 1; y < ROWS - 1; y++)
            for (int x = EastStart; x < MlStart; x++)
            {
                double wave = 0.09 * Math.Sin(x * .8 + y * .6) + .06 * Math.Cos(y * 1.2 - x * .3), d = double.MaxValue;
                for (int k = 0; k < AmihanIslands.Length; k++)
                {
                    var i = AmihanIslands[k];
                    var g = AmihanGrowth[k];
                    double dx = (x + 0.5 - i.cx) / i.rx, dy = (y + 0.5 - i.cy) / i.ry;
                    double gx = (x + 0.5 - g.cx) / g.rx, gy = (y + 0.5 - g.cy) / g.ry;
                    d = Math.Min(d, GrownD(dx * dx + dy * dy + wave, gx * gx + gy * gy + wave, x, y, g.keep));
                }
                map[y, x] = d < .72 ? 'j' : d < 1 ? 's' : d < 1.3 ? 'w' : '~';
                biome[y, x] = 5;
            }
        // Four different fishing habitats; clear approaches prevent trees hiding the shore.
        Pool(164, 24, 3.4, 2.3, 'l');
        Pool(205, 17, 3.2, 2.5, 'l');
        Pool(190, 54, 4, 2.4, 'm');
        // Baga's offshore reef is reached from a short shore-connected jetty.
        for (int y = 32; y <= 39; y++) map[y, 231] = 'b';
        // Amihan's west landing, with a clear path to the village.
        for (int x = 149; x <= 159; x++) map[21, x] = x < 153 ? 'd' : 's';
        foreach (var n in Islanders)
            for (int y = (int)n.y / T - 3; y <= (int)n.y / T + 2; y++)
                for (int x = (int)n.x / T - 3; x <= (int)n.x / T + 3; x++)
                    map[y, x] = 's';
        // Palms, bamboo-like groves, and limestone / volcanic boulders reuse the coast-aware ground renderer.
        for (int y = 1; y < ROWS - 1; y++)
            for (int x = EastStart + 1; x < MlStart; x++)
            {
                bool beach = map[y, x] == 's' && BeachPalm(x, y, 5);
                if (map[y, x] != 'j' && !beach) continue;
                // Palms on the beach stay clear of the landing, its post and notice, the evacuation signs and the jetties.
                if (beach && (Dist(x * T + 5, y * T + 5, PostX, PostY) < 40 || Dist(x * T + 5, y * T + 5, NoticeX, NoticeY) < 30
                    || SignSpots.Any(s => Dist(x * T + 5, y * T + 5, s.x, s.y) < 30) || FireflyTrees.Any(f => Dist(x * T + 5, y * T + 5, f.x, f.y) < 30)
                    || NearPier(x, y))) continue;
                if (Data.Spots.Any(s => s.Scene == "world" && s.Biome == "amihan" && Dist(x * T + 5, y * T + 5, s.X, s.Y) < s.R + 12)) continue;
                if (Islanders.Any(n => Dist(x * T + 5, y * T + 5, n.x, n.y) < 48)) continue;
                // Baga's cone stands on bare ground (Magayon.cs), and the lahar channel is kept clear.
                float cdx = (x * T + 5 - ConeX) / 74f, cdy = (y * T + 5 - (ConeBaseY - 22)) / 40f;
                if (cdx * cdx + cdy * cdy < 1 || ChannelDist(x * T + 5, y * T + 5) < 10) continue;
                // Nothing grows where Luntian's karst towers stand, or just behind them, where a palm's fronds would poke out
                // round the rock as if it grew inside it.
                if (Karsts.Any(k => Math.Abs(x * T + 5 - k.x) < 26 && y * T + 5 - k.y > -64 && y * T + 5 - k.y < 14)) continue;
                char kind = beach ? 'h' : PlantRoll(x, y, 5, 'h');
                if (kind == '\0') continue;
                string key = $"{x},{y}";
                if (state.felled.TryGetValue(key, out int day))
                {
                    if (state.day - day < (kind == 'R' ? BoulderRegrowDays : TreeRegrowDays) || RegrowBlocked(x, y))
                    { stumps[(x, y)] = kind; continue; }
                    state.felled.Remove(key);
                }
                if (BuiltOn(x, y)) continue;
                map[y, x] = kind; trees.Add((x, y, kind));
            }
        bool NearPier(int x, int y)
        {
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < COLS && ny < ROWS && map[ny, nx] is 'd' or 'b') return true;
                }
            return false;
        }
        void Pool(double cx, double cy, double rx, double ry, char water)
        {
            for (int y = (int)(cy - ry - 2); y <= cy + ry + 2; y++)
                for (int x = (int)(cx - rx - 2); x <= cx + rx + 2; x++)
                    if (InEllipse(x, y, cx, cy, rx, ry, 1.8)) map[y, x] = InEllipse(x, y, cx, cy, rx, ry) ? water : 's';
        }
    }

    void DiscoverArchipelago()
    {
        if (!InAmihan || state.Hinted("amihan")) return;
        state.hinted["amihan"] = true;
        chartEast = true;
        Toast("Amihan Archipelago! Open <map> for its sea chart. Look for the village landing on the western island.", 7);
        Save();
    }

    Target ArchipelagoTarget()
    {
        if (!InAmihan || Aboard) return null;
        foreach (var n in Islanders)
        {
            // At the shelter, or Mila on her way to the jetty, the folk from Baga aren't at home (Magayon.cs).
            if (n.id is "mila" or "ben" && (BagaFolkAtSchool || n.id == "mila" && mgMilaWalk != null)) continue;
            var s = IslanderWalk(n.id);
            if (FolkAsleep && Dist(player.X, player.Y, s.X, s.Y + 6) < 15) return new Target { Type = "info", Label = AsleepLine(n.name) };
            if (Dist(player.X, player.Y, s.X, s.Y + 6) < 15)
                return new Target
                {
                    Type = "islander", Id = n.id,
                    Label = n.id == "maya" && CanGiveMayaGuso ? $"Give Maya {GusoHandIn} dried guso"
                        : n.id == "niko" && DerbyToJudge ? "Show Niko your best bangus"
                        : n.id == "lira" && SupperDish() is string dish ? $"Give Lira the {Items.ById[dish].Name.ToLowerInvariant()}"
                        : n.id == "lira" && LiraSeaLabel() is string liraSea ? liraSea
                        : n.id == "niko" && NikoSeaLabel() is string nikoSea ? nikoSea
                        : n.id == "isay" && KiteHeld ? "Give Ma'am Isay the kite"
                        : n.id == "isay" && IsaySeaLabel() is string isaySea ? isaySea
                        : n.id == "niko" && state.Hinted("niko_request") && !state.Hinted("niko_asohos") && Has("asohos") >= NikoAsohos ? $"Give Niko {NikoAsohos} asohos" : n.id == "tala" && TalaListDone && !state.Hinted("talaReward") ? "Tell Tala what you've seen"
                        : n.id == "isay" && state.Hinted("metIsay") ? (LessonToday ? "Today's lesson with Ma'am Isay" : "Practise with Ma'am Isay")
                        : n.id == "mila" ? MilaLabel() : n.id == "ben" ? BenLabel() : $"Talk to {n.name}"
                };
        }
        // Maya's guso lines in the lagoon, when you face one (Seaweed.cs).
        return GusoTarget();
    }

    void TalkIslander(string id)
    {
        switch (id)
        {
            case "lira":
                // Her supper request comes first (Rondalla.cs), then the restless sea's part (RestlessSea.cs).
                if (LiraSupper()) break;
                if (LiraSeaTalk()) break;
                Talk(new()
                {
                    new("Lira", "Mabuhay! Welcome to Amihan. You've crossed a long stretch of sea to find our village. There are limestone lagoons, mangrove islands and volcanic shores beyond it."),
                    new("Lira", "I keep a pot warm for visiting fishers. Sit a while; the crossing is a long one."),
                    new("Lira", "Niko tends the nets east of here. Maya studies the lagoon on Luntian, and Tala watches the wildlife on Bakawan. Every island has a different catch.")
                }, () =>
                {
                    if (state.gifts.GetValueOrDefault("lira_meal") == state.day) { Toast("Lira: Come back tomorrow for another meal."); return; }
                    LiraMeal();
                });
                break;
            case "niko":
                // On the Bangus Festival's day he judges the derby, and says so the first time you talk (Festival.cs).
                if (DerbyToJudge) { JudgeBangus(); break; }
                if (FestivalToday && FestivalBest == 0 && state.gifts.GetValueOrDefault("fest_announce") != state.day)
                {
                    state.gifts["fest_announce"] = state.day;
                    Talk(new() { new("Niko", FestivalNikoLine()) });
                    break;
                }
                // The restless sea (RestlessSea.cs): the drained pond, its bank, and later a crab pot.
                if (NikoSeaTalk()) break;
                // After the bangus, a second job (1.15): asohos for the village's breakfast, fried whole.
                if (state.Hinted("niko_request") && !state.Hinted("niko_asohos") && Has("asohos") >= NikoAsohos)
                {
                    Take("asohos", NikoAsohos); state.coins += 70; Give("cricket", 4); state.hinted["niko_asohos"] = true; Save();
                    Sfx.Play("coin");
                    Talk(new() { new("Niko", $"{NikoAsohos} asohos! The kids will fight over these at breakfast. Salamat. Here: 70 coins, and four crickets. Asohos snap them up in the shallows.") });
                    break;
                }
                if (state.Hinted("niko_request") && !state.Hinted("niko_asohos"))
                {
                    state.hinted["nikoAsohosAsked"] = true;   // for the guide's journal
                    Talk(new()
                    {
                        new("Niko", "The village loved your bangus. Now the kids want asohos for breakfast: the little silver whiting, fried whole, crunchy bones and all."),
                        new("Niko", $"They feed over the sandy bottom of Maya's lagoon on Luntian. Bring me {NikoAsohos} and I'll pay 70 coins.")
                    });
                    break;
                }
                if (!state.Hinted("niko_request") && Has("bangus") >= 2)
                {
                    Take("bangus", 2); state.coins += 90; Give("cut_bait", 5); state.hinted["niko_request"] = true; Save();
                    Talk(new() { new("Niko", "Two bangus for the village supper! Salamat. Here are 90 coins and five pieces of cut bait for the reef fish.") });
                }
                else
                {
                    // The guide's journal lists his bangus once he's asked for them.
                    if (!state.Hinted("niko_request")) state.hinted["nikoAsked"] = true;
                    Talk(new()
                    {
                        new("Niko", state.Hinted("niko_request") ? "The village loved your catch. The sea always has another story for you." : "Could you bring me two bangus from the village pond? I'll pay 90 coins and give you cut bait."),
                        new("Niko", "A boat follows <move>. Beside dry shore, press <ride> to land; press <ride> beside your boat to board again. Your chart shows where you left it."),
                        new("Niko", "The sharp-toothed reef fish fight back! When the red warning appears, release the reel and duck until it passes. Keeping the line tight means taking a hit.")
                    });
                }
                break;
            // Maya's guso farm (Seaweed.cs), and Tala's bubo and night-time list (Bakawan.cs).
            case "maya": TalkMaya(); break;
            case "tala": TalkTala(); break;
            // Ma'am Isay's class (School.cs).
            case "isay": TalkIsay(); break;
            // Beneath the clouds (Magayon.cs).
            case "mila": TalkMila(); break;
            case "ben": TalkBen(); break;
        }
    }

    IEnumerable<Box> IslandSolids() => Islanders.Select(n => n.id == "isay" ? new Box(n.x - 23 * HouseS, n.y - 24, 46 * HouseS, 21) : new Box(n.x - 16 * HouseS, n.y - 22, 32 * HouseS, 19))
        .Append(SchoolBenchBox)
        .Concat(new[] { new Box(2073, 97, 24, 10), new Box(1983, 131, 24, 10), new Box(ConeX - 52, ConeBaseY - 28, 104, 28),
            new Box(AlertBoardX - 7, AlertBoardY - 2, 15, 3), new Box(EvacTableX - 7, EvacTableY - 3, 15, 3), new Box(CrateX - 6, CrateY - 3, 13, 3) })
        .Concat(FireflyTreeSolids());

    void AddArchipelagoObjects(List<(float y, Action draw)> list)
    {
        foreach (var k in Karsts) list.Add((k.y, () => DrawKarst(k.x, k.y, k.h)));
        list.Add((ConeBaseY, DrawBagaCone));
        if (RondallaOut) AddRondalla(list);
        if (FestivalHere) AddFestival(list);
        // Bakawan's pagatpat, the firefly trees (Bakawan.cs).
        foreach (var (fx, fy) in FireflyTrees)
            if (MathF.Abs(fx - player.X) < W && MathF.Abs(fy - player.Y) < H + 30) list.Add((fy, () => DrawPagatpat(fx, fy)));
        // Root fans and small bamboo clumps make the mangrove island distinct from the palm village.
        foreach (var (x, y) in new[] { (1855, 525), (1945, 555), (1885, 575) })
        {
            int px = x, py = y;
            list.Add((y, () =>
            {
                for (int i = -2; i <= 2; i++)
                {
                    pix.Line(px, py - 10, px + i * 5, py + 2, "#7c6546");
                    pix.Rect(px + i * 3, py - 24 - Math.Abs(i) * 2, 2, 19, "#85a75d");
                    for (int j = 0; j < 3; j++) pix.Rect(px + i * 3, py - 21 + j * 5, 3, 1, "#b2c578");
                }
                pix.Rect(px - 9, py - 30, 18, 4, "#4e893f");
                pix.Rect(px - 13, py - 27, 26, 4, "#609a4a");
            }));
        }
        foreach (var n in Islanders)
        {
            if (Math.Abs(n.x - player.X) > W && pix.W == W || Math.Abs(n.y - player.Y) > H && pix.H == H) continue;
            var s = IslanderWalk(n.id);
            // Ma'am Isay's school, with its bench of children, and her (School.cs).
            if (n.id == "isay")
            {
                list.Add((n.y - 3, () => DrawSchool(n.x, n.y - 5)));
                list.Add((SchoolY + 18, () => DrawSchoolBench(time)));
                // While the ash falls she's inside the shelter with her class (Magayon.cs).
                if (!AshFalling && !FolkAsleep) list.Add((s.Y, () => DrawIsayFigure(pix, (int)MathF.Round(s.X), (int)MathF.Round(s.Y), s.Face, s.Step, s.Moving ? 0 : (int)(time % 3 / 2), (time + 3) % 4.3f < 0.12f)));
                continue;
            }
            // Baga's two (Magayon.cs): Ben's station and Manay Mila's house, and them unless they're at the shelter.
            if (n.id is "mila" or "ben")
            {
                list.Add((n.y - 3, () => { if (n.id == "ben") DrawStation(n.x, n.y - 5); else DrawBahay(n.x, n.y - 5, n.shirt); }));
                if (BagaFolkAtSchool || n.id == "mila" && mgMilaWalk != null || FolkAsleep) continue;
                list.Add((s.Y, () =>
                {
                    int bx = (int)MathF.Round(s.X), by = (int)MathF.Round(s.Y), bob = s.Moving ? 0 : (int)(time % 3 / 2);
                    if (n.id == "mila") DrawMila(pix, bx, by, s.Face, s.Step, bob); else DrawBen(pix, bx, by, s.Face, s.Step, bob);
                }));
                continue;
            }
            list.Add((n.y - 3, () => DrawBahay(n.x, n.y - 5, n.shirt)));
            // On their way up to School Rise, Lira and Niko are drawn by the evacuation instead (RestlessSea.cs).
            if (SeaEmergency && n.id is "lira" or "niko" || FolkAsleep) continue;
            list.Add((s.Y, () =>
            {
                var look = new Look { skin = 2, shirt = n.id == "niko" ? 2 : n.id == "tala" ? 3 : 1, hat = 0, hair = 1 };
                int x = (int)MathF.Round(s.X), y = (int)MathF.Round(s.Y), bob = s.Moving ? 0 : (int)(time % 3 / 2);
                // Ducked, hands over their heads, while the ground shakes (RestlessSea.cs).
                if (Shaking) { DrawDucked(look, x, y, s.Face); return; }
                // Getting on with a chore while they stand about (Ambience.cs).
                string chore = ChoreNow(n.id, x, y, s.Face, s.Moving);
                var hands = chore != null ? ChoreHands(chore, x, y, s.Face) : default;
                if (chore != null) LookData.DrawPerson(pix, look, x, y, s.Face, s.Step, bob: bob, arms: 4, hand: hands.hand, hand2: hands.hand2);
                else LookData.DrawPerson(pix, look, x, y, s.Face, s.Step, bob: bob);
                // A woven salakot and a sash distinguish the village fishers; both ride the walk's bounce.
                int up = bob + (s.Step is 2 or 4 ? 1 : 0);
                pix.Rect(x - 5, y - 14 - LookData.Stature - up, 11, 2, "#ddbc78");
                pix.Rect(x - 3, y - 16 - LookData.Stature - up, 7, 2, "#f0d596");
                pix.Rect(x - 2, y - 7 - LookData.Stature - up, 4, 2, n.shirt);
                if (chore != null) DrawChoreProp(chore, x, y, s.Face);
            }));
        }
    }

    // A limestone tower like the real karsts of El Nido: a rounded top under a cap of forest, steep fluted sides lit from
    // the west, shrubs clinging to the ledges, and fallen blocks at its foot.
    // Luntian's two limestone towers: where each stands (its foot) and how tall the old drawing made it.
    static readonly (float x, float y, int h)[] Karsts = { (2085, 107, 41), (1995, 141, 32) };

    void DrawKarst(float x, float y, int height)
    {
        int X = (int)x, Y = (int)y, top = Y - height;
        pix.Rect(X - 13, Y - 1, 28, 3, "rgba(18,46,40,0.22)");
        if (!OldArt)
        {
            // The Blender towers (tools/blender/karst.py), a third taller than the old drawings, on the same footprint.
            if (height >= 40) BlitArt(KarstArt.Tall, KarstArt.TallPalette, KarstArt.TallAnchorX, KarstArt.TallAnchorY, X, Y);
            else BlitArt(KarstArt.Short, KarstArt.ShortPalette, KarstArt.ShortAnchorX, KarstArt.ShortAnchorY, X, Y);
            return;
        }
        for (int row = 0; row < height; row++)
        {
            float k = row / (float)height;
            // Domed for the first rows, then sides that bulge a little and flare at the foot.
            float half = row < 6 ? 7.5f * MathF.Sqrt(1 - MathF.Pow((6 - row) / 6f, 2)) : 7.5f + 2.2f * MathF.Sin(k * MathF.PI * 0.9f) + (k > 0.85f ? (k - 0.85f) * 22 : 0);
            int hw = Math.Max(1, (int)MathF.Round(half)), yy = top + row;
            for (int i = -hw; i < hw; i++)
            {
                float u = (i + 0.5f) / hw;   // -1 at the lit west edge, 1 at the shaded east
                bool groove = (i + 40) % 4 == 0 && Pix.Hash(X + i, row / 5, 941) < 0.7;
                string c = u < -0.55f ? "#dcd8c4" : u < 0.1f ? "#c4c0aa" : u < 0.6f ? "#a8a592" : "#868777";
                if (groove) c = u < 0.1f ? "#a9a691" : "#74766a";
                if (row % 9 == 8 && Pix.Hash(X + i, row, 942) < 0.6) c = "#8f8d7c";
                pix.Rect(X + i, yy, 1, 1, c);
            }
            // Shrubs on the ledges, and vines hanging down the shaded side.
            if (row > 7 && row % 9 == 0) { pix.Rect(X - hw, yy - 1, 3, 2, "#4f8a3c"); pix.Rect(X - hw + 1, yy - 2, 1, 1, "#6aa84f"); }
            if (row > 4 && row < height * 0.6f && Pix.Hash(X, row, 943) < 0.35) pix.Rect(X + hw - 2, yy, 1, 1, "#3f6e35");
        }
        // The forest cap.
        foreach (var (ox, oy, r) in new[] { (-3, 2, 4.2f), (3, 1, 4f), (0, -1, 4.6f), (-6, 4, 2.6f), (6, 4, 2.6f) })
        {
            Disc(X + ox, top + oy + 1, r, Pal.C("#2f5e2c"));
            Disc(X + ox - 0.5f, top + oy, r - 0.8f, Pal.C("#4a8a3c"));
            Disc(X + ox - 1.5f, top + oy - 1, r * 0.45f, Pal.C("#6aa84f"));
        }
        // Fallen blocks and a fern at the foot.
        pix.Rect(X - 13, Y - 4, 4, 3, "#a8a592"); pix.Rect(X - 13, Y - 4, 4, 1, "#d0ccb6");
        pix.Rect(X + 10, Y - 3, 3, 2, "#868777");
        pix.Rect(X - 9, Y - 3, 3, 2, "#4f8a3c"); pix.Rect(X - 8, Y - 4, 1, 1, "#6aa84f");
    }

    // Draws a grid rendered from a Blender model (tools/blender): letters index the palette, '.' is clear, and X, Y, Z
    // (a special material's shade, base and light) take `special`. The grid's anchor lands on (x, y).
    void BlitArt(string[] grid, string[] palette, int ax, int ay, int x, int y, Color[] special = null, bool mirror = false) =>
        BlitArtTo(pix, grid, palette, ax, ay, x, y, special, mirror);

    // The same into any picture (the cave's baked floor), mirrored about the anchor's column if asked.
    static void BlitArtTo(Pix p, string[] grid, string[] palette, int ax, int ay, int x, int y, Color[] special = null, bool mirror = false)
    {
        int oy = y - ay;
        for (int j = 0; j < grid.Length; j++)
        {
            string row = grid[j];
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '.') continue;
                int g = ch switch { 'X' => 0, 'Y' => 1, 'Z' => 2, _ => -1 };
                int px = mirror ? x - (i - ax) : x + (i - ax);
                if (g >= 0) { if (special != null) p.Rect(px, oy + j, 1, 1, special[g]); }
                else p.Rect(px, oy + j, 1, 1, palette[ch - 'a']);
            }
        }
    }

    // A special material's three tones (shade, base, light) from one colour.
    static Color[] Tones3(string hex) => new[] { Pix.Mix(Pal.C(hex), Pal.C("#140f12"), 0.35f), Pal.C(hex), Pix.Mix(Pal.C(hex), Pal.C("#ffffff"), 0.3f) };

    // The Blender houses are drawn BuildingArt.Scale times the size of the old drawings, about the middle of their front
    // wall on the ground; HouseS is that scale (1 with the old art), for their collision boxes and shadows.
    static float HouseS => OldArt ? 1 : BuildingArt.Scale;

    // A window laid out on the old drawing at (x, y, w, h), moved and sized with the house round its anchor (ax, ay).
    void HouseWindow(int ax, int ay, int x, int y, int w, int h, string day)
    {
        float s = BuildingArt.Scale;
        Window(ax + (int)MathF.Round((x - ax) * s), ay + (int)MathF.Round((y - ay) * s), (int)MathF.Round(w * s), (int)MathF.Round(h * s), day);
    }

    void DrawBahay(float x, float y, string trim)
    {
        // Raised timber walls, woven panels and a broad thatched roof, lit from the west, casting its shadow east.
        pix.Rect(x - 17, y + 3, 34, 2, "rgba(0,0,0,0.2)");
        if (!OldArt)
        {
            // The Blender model (tools/blender/buildings.py), its foot band in the resident's colour.
            var tones = new[] { Pix.Mix(Pal.C(trim), Pal.C("#1a1410"), 0.3f), Pal.C(trim), Pix.Mix(Pal.C(trim), Pal.C("#fff4dc"), 0.25f) };
            BlitArt(BuildingArt.Bahay, BuildingArt.BahayPalette, BuildingArt.BahayAnchorX, BuildingArt.BahayAnchorY, (int)x, (int)y + 5, tones);
            HouseWindow((int)x, (int)y + 5, (int)x - 13, (int)y - 12, 7, 6, "#485d56"); HouseWindow((int)x, (int)y + 5, (int)x + 7, (int)y - 12, 6, 6, "#485d56");
            pix.Rect(x + 12, y + 1, 3, 2, "#a0522d"); pix.Rect(x + 12, y - 1, 3, 2, "#4f8a3c"); pix.Rect(x + 13, y - 2, 1, 1, "#6aa84f");
            return;
        }
        pix.Rect(x - 14, y - 5, 2, 10, "#65452e"); pix.Rect(x + 12, y - 5, 2, 10, "#65452e");
        pix.Rect(x - 16, y - 19, 32, 19, "#b98b52");
        for (int i = -14; i < 16; i += 4) pix.Rect(x + i, y - 18, 1, 16, "#dfbc78");
        pix.Rect(x - 3, y - 11, 7, 11, "#3c342a");
        Window((int)x - 13, (int)y - 12, 7, 6, "#485d56"); Window((int)x + 7, (int)y - 12, 6, 6, "#485d56");
        pix.Rect(x - 16, y - 2, 32, 2, trim);
        // The right-hand wall in shade, and the eaves' shadow along the top of the walls.
        pix.Rect(x + 13, y - 19, 3, 17, "rgba(40,24,10,0.22)");
        pix.Rect(x - 16, y - 17, 32, 2, "rgba(40,24,10,0.28)");
        ThatchRoof((int)x, (int)y - 31, 12, 9, 1);
        pix.Rect(x - 21, y - 19, 42, 2, "#80643e"); pix.Rect(x - 21, y - 19, 20, 1, "#9a7a4a");
        pix.Rect(x - 4, y + 1, 9, 2, "#c7a06a");
        // A potted plant by the steps.
        pix.Rect(x + 8, y - 1, 3, 2, "#a0522d"); pix.Rect(x + 8, y - 3, 3, 2, "#4f8a3c"); pix.Rect(x + 9, y - 4, 1, 1, "#6aa84f");
    }
}
