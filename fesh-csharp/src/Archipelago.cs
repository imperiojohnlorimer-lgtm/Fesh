using Raylib_cs;

namespace Fesh;

// Amihan is a fictional Philippine-inspired archipelago east of the original map.
// It shares the outdoor ocean so the entire crossing is navigated, with no travel portal.
partial class Game
{
    const int EastStart = 140;
    bool chartEast;
    bool InAmihan => scene == "world" && player.X >= EastStart * T;
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
        ("tala", "Tala", 1895, 597, "#72a752")
    };

    void GenerateArchipelago()
    {
        for (int y = 1; y < ROWS - 1; y++)
            for (int x = EastStart; x < COLS - 1; x++)
            {
                double d = AmihanIslands.Min(i =>
                {
                    double dx = (x + 0.5 - i.cx) / i.rx, dy = (y + 0.5 - i.cy) / i.ry;
                    return dx * dx + dy * dy + 0.09 * Math.Sin(x * .8 + y * .6) + .06 * Math.Cos(y * 1.2 - x * .3);
                });
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
            for (int x = EastStart + 1; x < COLS - 1; x++)
            {
                if (map[y, x] != 'j') continue;
                if (Data.Spots.Any(s => s.Biome == "amihan" && Dist(x * T + 5, y * T + 5, s.X, s.Y) < s.R + 12)) continue;
                if (Islanders.Any(n => Dist(x * T + 5, y * T + 5, n.x, n.y) < 48)) continue;
                double roll = Pix.Hash(x, y, 183);
                char kind = roll < .12 ? 'h' : roll < .16 ? 'R' : '\0';
                if (kind == '\0') continue;
                string key = $"{x},{y}";
                if (state.felled.TryGetValue(key, out int day))
                {
                    if (state.day - day < (kind == 'R' ? BoulderRegrowDays : TreeRegrowDays) || RegrowBlocked(x, y))
                    { stumps[(x, y)] = kind; continue; }
                    state.felled.Remove(key);
                }
                map[y, x] = kind; trees.Add((x, y, kind));
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
            if (Dist(player.X, player.Y, n.x, n.y + 8) < 15)
                return new Target { Type = "islander", Id = n.id, Label = $"Talk to {n.name}" };
        return null;
    }

    void TalkIslander(string id)
    {
        switch (id)
        {
            case "lira":
                Talk(new()
                {
                    new("Lira", "Mabuhay! Welcome to Amihan. You've crossed a long stretch of sea to find our village. There are limestone lagoons, mangrove islands and volcanic shores beyond it."),
                    new("Lira", "I keep a pot warm for visiting fishers. Sit a while; the crossing is a long one."),
                    new("Lira", "Niko tends the nets east of here. Maya studies the lagoon on Luntian, and Tala watches the wildlife on Bakawan. Every island has a different catch.")
                }, () =>
                {
                    if (state.gifts.GetValueOrDefault("lira_meal") == state.day) { Toast("Lira: Come back tomorrow for another meal."); return; }
                    state.gifts["lira_meal"] = state.day; Give("fish_stew"); Save();
                    Toast("Lira packed you a bowl of fish stew. (+1 fish stew)");
                });
                break;
            case "niko":
                if (!state.Hinted("niko_request") && Has("bangus") >= 2)
                {
                    Take("bangus", 2); state.coins += 90; Give("cut_bait", 5); state.hinted["niko_request"] = true; Save();
                    Talk(new() { new("Niko", "Two bangus for the village supper! Salamat. Here are 90 coins and five pieces of cut bait for the reef fish.") });
                }
                else Talk(new()
                {
                    new("Niko", state.Hinted("niko_request") ? "The village loved your catch. The sea always has another story for you." : "Could you bring me two bangus from the village pond? I'll pay 90 coins and give you cut bait."),
                    new("Niko", "A boat follows <move>. Come alongside dry shore and press <act> to land; press <act> beside your boat to board again. Your chart shows where you left it."),
                    new("Niko", "The sharp-toothed reef fish fight back! When the red warning appears, release the reel and duck until it passes. Keeping the line tight means taking a hit.")
                });
                break;
            case "maya":
                Talk(new()
                {
                    new("Maya", "These sheltered waters hold lapu-lapu and maya-maya. Local fish names can change from island to island."),
                    new("Maya", "Out by Baga, talakitok and tanigue put up a fierce fight. Keep food in your bag, and watch their warning before an attack."),
                    new("Maya", "A storm slows a boat already at sea, but you can still steer it home. Wait ashore before setting out again.")
                });
                break;
            case "tala":
                Talk(new()
                {
                    new("Tala", "Bakawan means mangrove. Its roots shelter young fish; the pools here hold hito, dalag and mudskippers."),
                    new("Tala", "Watch the tiny tarsiers and the hornbills quietly. The carabao near the village is used to people, but the wild animals need their space.")
                });
                break;
        }
    }

    IEnumerable<Box> IslandSolids() => Islanders.Select(n => new Box(n.x - 16, n.y - 22, 32, 19))
        .Concat(new[] { new Box(2073, 97, 24, 10), new Box(1983, 131, 24, 10), new Box(2309, 455, 52, 25) });

    void AddArchipelagoObjects(List<(float y, Action draw)> list)
    {
        list.Add((107, () => DrawKarst(2085, 107, 41)));
        list.Add((141, () => DrawKarst(1995, 141, 32)));
        list.Add((480, DrawBagaCone));
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
            list.Add((n.y - 3, () => DrawBahay(n.x, n.y - 5, n.shirt)));
            list.Add((n.y + 2, () =>
            {
                var look = new Look { skin = 2, shirt = n.id == "niko" ? 2 : n.id == "tala" ? 3 : 1, hat = 0, hair = 1 };
                LookData.DrawPerson(pix, look, (int)n.x, (int)n.y + 2, "down", 0, bob: (int)(time % 3 / 2));
                // A woven salakot and a sash distinguish the village fishers.
                pix.Rect(n.x - 5, n.y - 12, 11, 2, "#ddbc78");
                pix.Rect(n.x - 3, n.y - 14, 7, 2, "#f0d596");
                pix.Rect(n.x - 2, n.y - 5, 5, 2, n.shirt);
            }));
        }
    }

    void DrawKarst(float x, float y, int height)
    {
        for (int row = 0; row < height; row++)
        {
            int half = 5 + row * 8 / height;
            pix.Rect(x - half, y - height + row, half * 2, 1, row % 7 < 2 ? "#aaa998" : "#c8c7ae");
            pix.Rect(x + half / 2, y - height + row, half / 2, 1, "#808f80");
        }
        pix.Rect(x - 5, y - height - 2, 10, 3, "#548b46");
        pix.Rect(x - 12, y - 5, 6, 4, "#769756");
        pix.Rect(x - 8, y - height + 12, 4, 1, "#e3dbc0");
    }

    void DrawBagaCone()
    {
        const int x = 2335, y = 480;
        for (int row = 0; row < 45; row++)
        {
            int half = 9 + row * 28 / 45;
            pix.Rect(x - half, y - 45 + row, half * 2, 1, row % 6 == 0 ? "#65736b" : "#59675f");
            pix.Rect(x + 3, y - 45 + row, half - 3, 1, "#48565a");
        }
        pix.Rect(x - 8, y - 45, 16, 3, "#303f43");
        pix.Rect(x - 4, y - 44, 8, 1, "#c98252");
        for (int i = 0; i < 3; i++)
        {
            float rise = (time * 3 + i * 9) % 28;
            pix.Rect(x - 3 + rise / 5, y - 49 - rise, 7 + (int)(rise / 4), 3, Pal.Rgba(201, 211, 197, .3f * (1 - rise / 28)));
        }
    }

    void DrawBahay(float x, float y, string trim)
    {
        // Raised timber walls, woven panels and a broad thatched roof.
        pix.Rect(x - 17, y + 3, 34, 2, "rgba(0,0,0,0.2)");
        pix.Rect(x - 14, y - 5, 2, 10, "#65452e"); pix.Rect(x + 12, y - 5, 2, 10, "#65452e");
        pix.Rect(x - 16, y - 19, 32, 19, "#b98b52");
        for (int i = -14; i < 16; i += 4) pix.Rect(x + i, y - 18, 1, 16, "#dfbc78");
        pix.Rect(x - 3, y - 11, 7, 11, "#3c342a");
        pix.Rect(x - 13, y - 12, 7, 6, "#485d56"); pix.Rect(x + 7, y - 12, 6, 6, "#485d56");
        pix.Rect(x - 16, y - 2, 32, 2, trim);
        for (int i = 0; i < 12; i++) pix.Rect(x - 9 - i, y - 31 + i, 18 + i * 2, 1, i % 3 == 0 ? "#967342" : "#ceb36b");
        pix.Rect(x - 21, y - 19, 42, 2, "#80643e");
        pix.Rect(x - 4, y + 1, 9, 2, "#c7a06a");
    }
}
