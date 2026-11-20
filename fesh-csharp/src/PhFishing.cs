namespace Fesh;

// Fishing on the Philippines map (1.48, step 2). Spots are named places at their real latitude and longitude (a reef, a bay,
// mangroves, a river mouth, a lake, a deep channel), plus each town's pier, and every one has the fish of its habitat: real
// Philippine species already in the game, each still on its own Fish log page. They live in their own list (their places are
// in the country's coordinates, not the original world's), registered in Data.SpotById under "ph:" ids with Scene "ph" so the
// original world's loops (Scene "world") leave them alone. Casting anywhere else goes by the water: the nearest spot of that
// water within reach, else the shallows', a lake's or the open sea's own list.
partial class Game
{
    // Habitat -> fish ids (their weights, conditions and fights are their own, from Data.Common).
    static readonly Dictionary<string, string[]> PhHabitats = new()
    {
        ["reef"] = new[] { "lapu_lapu", "maya_maya", "labahita", "pakol", "paru_paro", "parrotfish", "moorish_idol", "clownfish", "danggit", "suno", "talakitok" },
        ["bay"] = new[] { "asohos", "sapsap", "danggit", "pampano", "dapa", "kalaso", "tamban", "dilis", "banak", "pagi" },
        ["mangrove"] = new[] { "kitang", "banak", "amihan_mudskipper", "kanduli", "biya", "bangus", "igat", "pagi" },
        ["river"] = new[] { "biya", "kanduli", "banak", "dalag", "hito", "igat" },
        ["lake"] = new[] { "bangus", "dalag", "hito", "banak", "biya" },
        ["deep"] = new[] { "yellowfin_tuna", "tanigue", "bisugo", "gulyasan", "talang_talang", "dalupapa", "malasugi", "amihan_barracuda" },
        ["sea"] = new[] { "galunggong", "tulingan", "alumahan", "gulyasan", "hasa_hasa", "matang_baka", "dalagang_bukid", "salay_salay", "flying_fish", "mahi_mahi", "pating", "swordfish" },
    };
    static readonly Dictionary<string, string> PhHabitatWord = new()
    {
        ["reef"] = "coral reef", ["bay"] = "sandy bay", ["mangrove"] = "mangroves", ["river"] = "river mouth", ["lake"] = "lake", ["deep"] = "deep channel", ["sea"] = "open sea",
    };

    // (id, label, habitat, latitude, longitude)
    static readonly (string id, string label, string hab, double lat, double lon)[] PhSpotDefs =
    {
        ("tresreyes", "Tres Reyes reef", "reef", 13.275, 121.805), ("maniwaya", "Maniwaya shallows", "bay", 13.505, 122.105),
        ("boacriver", "Boac river mouth", "river", 13.452, 121.832), ("poctoy", "Poctoy beach", "bay", 13.335, 122.095),
        ("balanacan", "Balanacan mangroves", "mangrove", 13.530, 121.870),
        ("laguna", "Laguna de Bay", "lake", 14.380, 121.250), ("taal", "Taal Lake", "lake", 13.960, 121.000), ("anilao", "Anilao reef", "reef", 13.760, 120.920),
        ("manilabay", "Manila Bay shore", "bay", 14.560, 120.950), ("hundred", "Hundred Islands", "reef", 16.200, 120.030),
        ("verde", "Verde Island Passage", "deep", 13.580, 121.060), ("galera", "Puerto Galera reef", "reef", 13.520, 120.950),
        ("aporeef", "Apo Reef", "reef", 12.670, 120.450), ("honda", "Honda Bay", "reef", 9.880, 118.780), ("elnido", "El Nido lagoons", "reef", 11.180, 119.390),
        ("coronbay", "Coron Bay", "reef", 11.950, 120.200), ("guimaras", "Guimaras Strait", "deep", 10.450, 122.650), ("apoisland", "Apo Island reef", "reef", 9.075, 123.270),
        ("moalboal", "Moalboal reef", "reef", 9.950, 123.370), ("olango", "Olango mangroves", "mangrove", 10.250, 124.050), ("panglao", "Panglao reef", "reef", 9.560, 123.770),
        ("cancabato", "Cancabato Bay", "mangrove", 11.220, 125.030), ("ticao", "Ticao Pass", "deep", 12.600, 123.850), ("davaogulf", "Davao Gulf", "deep", 6.950, 125.700),
        ("samal", "Samal reef", "reef", 7.100, 125.760), ("stacruzisl", "Santa Cruz Islands", "reef", 6.870, 122.050), ("lanao", "Lake Lanao", "lake", 7.880, 124.250),
        ("butuan", "Agusan river mouth", "river", 8.980, 125.520), ("lingayen", "Lingayen Gulf", "bay", 16.150, 120.300),
    };

    sealed record PhSpotInfo(Spot Spot, string Hab, string Town);
    static List<PhSpotInfo> phSpots;

    // Every spot of this map: placed once on the country's tiles (the same in every game) and registered by id.
    static List<PhSpotInfo> PhSpots()
    {
        if (phSpots != null) return phSpots;
        PhFields();
        var list = new List<PhSpotInfo>();
        foreach (var (id, label, hab, lat, lon) in PhSpotDefs)
        {
            int tx = (int)((lon - PhMapData.Lon0) * PhMapData.Kx), ty = (int)((PhMapData.Lat1 - lat) * PhMapData.Ky);
            // Shore spots sit in the shallows beside land (so you can fish them from the beach); a lake's on its shore; a deep
            // channel's out in the deep, for the boat.
            (int x, int y)? at = null;
            for (int r = 0; r <= 20 && at == null; r++)
                for (int dy = -r; dy <= r && at == null; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = tx + dx, y = ty + dy;
                        byte k = PhKindAt(x, y);
                        bool beside = PhKindAt(x + 1, y) == 3 || PhKindAt(x - 1, y) == 3 || PhKindAt(x, y + 1) == 3 || PhKindAt(x, y - 1) == 3;
                        bool ok = hab switch
                        {
                            "lake" => k == 2 && beside,
                            "deep" => k == 0 && phDepth[y * PhMapData.Cols + x] > 3,
                            _ => k == 1 && beside,
                        };
                        if (ok) { at = (x, y); break; }
                    }
            if (at is not var (sx, sy)) continue;
            float r0 = hab == "deep" ? 70 : 30;
            list.Add(new PhSpotInfo(new Spot("ph:" + id, label, $"Fish the {PhHabitatWord[hab]}", sx * T + 5, sy * T + 5, r0, "ph", "ph"), hab, null));
        }
        // Each town's pier: the bay's fish off its end.
        foreach (var t in PhTowns())
        {
            if (t.PierLen == 0) continue;
            int ex = t.PierX + t.PierDx * t.PierLen, ey = t.PierY + t.PierDy * t.PierLen;
            list.Add(new PhSpotInfo(new Spot("ph:pier:" + t.Def.Id, $"{t.Def.Name} pier", "Fish off the pier", ex * T + 5, ey * T + 5, 26, "ph", "ph"), "bay", t.Def.Id));
        }
        // No place of their own: casts into the shallows, a lake or the open sea away from any spot.
        list.Add(new PhSpotInfo(new Spot("ph:shore", "the shallows", "Fish the shallows", 0, 0, 0, "ph", "phwild"), "bay", null));
        list.Add(new PhSpotInfo(new Spot("ph:lakewild", "the lake", "Fish the lake", 0, 0, 0, "ph", "phwild"), "lake", null));
        list.Add(new PhSpotInfo(new Spot("ph:sea", "the open sea", "Fish the open sea", 0, 0, 30, "ph", "sea"), "sea", null));
        foreach (var p in list) Data.SpotById[p.Spot.Id] = p.Spot;
        return phSpots = list;
    }

    static readonly Dictionary<string, CommonFish[]> phPools = new();
    // The fish of a "ph:" spot (FishWeights reads this for them).
    static CommonFish[] PhPool(string spot)
    {
        if (phPools.TryGetValue(spot, out var pool)) return pool;
        var info = PhSpots().FirstOrDefault(p => p.Spot.Id == spot);
        string hab = info?.Hab ?? "bay";
        pool = PhHabitats[hab].Where(Data.FishById.ContainsKey).Select(id => Data.FishById[id]).ToArray();
        phPools[spot] = pool;
        return pool;
    }
    static bool PhFresh(string spot) => spot.StartsWith("ph:") && PhSpots().FirstOrDefault(p => p.Spot.Id == spot)?.Hab is "lake" or "river";

    // The named spot whose water you're facing, near you.
    Target PhSpotTarget(bool helm)
    {
        if (!PhWorld) return null;
        foreach (var p in PhSpots())
        {
            var s = p.Spot;
            if (s.Scene != "ph" || Math.Abs(s.X - player.X) > 120 || Math.Abs(s.Y - player.Y) > 120) continue;
            if (Dist(player.X, player.Y, s.X, s.Y) >= s.R + (helm ? 10 : 0)) continue;
            var t = SpotTarget(s);
            if (helm) t.Alt2Type = t.Alt2Label = null;
            return t;
        }
        return null;
    }

    // Whose fish are in the water at (x, y) on this map (WildSpot's job here).
    string PhWildSpot(float x, float y)
    {
        int tx = (int)MathF.Floor(x / T), ty = (int)MathF.Floor(y / T);
        char t = TileAt(tx, ty);
        var near = PhSpots().Where(p => p.Spot.Scene == "ph" && Math.Abs(p.Spot.X - x) < 300 && Math.Abs(p.Spot.Y - y) < 300)
            .OrderBy(p => Dist(p.Spot.X, p.Spot.Y, x, y)).FirstOrDefault();
        bool lake = t is 'l';
        if (near != null && (near.Hab == "lake") == lake && Dist(near.Spot.X, near.Spot.Y, x, y) < (t == '~' ? 120 : 200)) return near.Spot.Id;
        if (lake) return "ph:lakewild";
        return t == '~' ? "ph:sea" : "ph:shore";
    }

    // The ripples that show where a spot is (they're under the fish shadows the original world draws for its own spots).
    void DrawPhSpots(float tm)
    {
        if (!PhWorld) return;
        foreach (var p in PhSpots())
        {
            var s = p.Spot;
            if (s.Scene != "ph" || s.X < camX - 40 || s.X > camX + W + 40 || s.Y < camY - 40 || s.Y > camY + H + 40) continue;
            for (int k = 0; k < 3; k++)
            {
                double ph = (tm * 0.35 + k / 3.0 + s.X * 0.001) % 1;
                float ox = MathF.Cos(k * 2.1f + s.Y) * s.R * 0.35f, oy = MathF.Sin(k * 1.7f + s.X) * s.R * 0.2f;
                if (!IsWater(s.X + ox, s.Y + oy)) continue;
                pix.Ring(s.X + ox, s.Y + oy, 1 + ph * 4, Pal.Rgba(255, 255, 255, (float)(0.45 * (1 - ph))));
            }
        }
    }
}
