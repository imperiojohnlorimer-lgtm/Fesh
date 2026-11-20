using Raylib_cs;

namespace Fesh;

// Towns on the Philippines map (1.48, step 2): two on Marinduque (Boac, its capital, and the village of Santa Cruz: at 1.1 km a tile the island is only about 30 x 33 tiles, and more towns covered it in buildings), then one or two on each main island, each at its real
// latitude and longitude. A town is laid out from its place and the coast round it, the same every time (PhTownLayout):
// a plaza and a lane down to a pier, a church, a market, houses, and the small things of a fishing town (a pump, a flagpole,
// washing lines, gardens, banana plants, a fish table, a drying rack, a boat pulled up on the sand). Its plaza, lane and
// pier are stamped into the country's tiles when they're made (PhInitTiles); the buildings and props are drawn objects,
// rendered from the Feshtry2 prototype's 3D models (PhTownArt, tools/blender/phtown.py), with solid boxes. A few villagers
// stroll by their houses (and sleep at night), a vendor keeps the market (Pip's stall, with the town's name on it), and one
// house is the inn, where you can rest.
partial class Game
{
    sealed record PhTownDef(string Id, string Name, string Island, double Lat, double Lon, int Houses, string Fact);

    static readonly PhTownDef[] PhTownDefs =
    {
        new("boac", "Boac", "Marinduque", 13.447, 121.842, 3, "Boac is Marinduque's capital. The cathedral up on the hill was built thick-walled, like a fort, and people sheltered in it from raiders long ago."),
        new("santacruz", "Santa Cruz", "Marinduque", 13.478, 122.030, 3, "Santa Cruz is the biggest town on the island. The Bathala caves are out east of here, and Maniwaya's white sand is a short boat ride off."),
        new("manila", "Manila", "Luzon", 14.595, 120.972, 6, "Stay for the sunset over Manila Bay. People come from all over to watch it."),
        new("batangas", "Batangas City", "Luzon", 13.756, 121.058, 5, "Out there is the Verde Island Passage. Scientists have called it the centre of the world's shore fish: more kinds than almost anywhere."),
        new("legazpi", "Legazpi", "Luzon", 13.139, 123.744, 5, "That's Mayon over the town, the volcano with the near-perfect cone. Mind the alert levels when it rumbles."),
        new("vigan", "Vigan", "Luzon", 17.574, 120.388, 5, "Vigan's old streets of stone houses are a World Heritage site. Ride a kalesa down Calle Crisologo."),
        new("calapan", "Calapan", "Mindoro", 13.411, 121.180, 5, "Calapan is Oriental Mindoro's capital. Boats cross to Batangas from here every day."),
        new("puertoprincesa", "Puerto Princesa", "Palawan", 9.742, 118.736, 5, "The Underground River is up the coast at Sabang: a river that runs through a cave and out into the sea."),
        new("coron", "Coron", "Busuanga", 11.998, 120.205, 4, "Ships sank in Coron Bay in 1944, in the war. Divers come from all over to see the wrecks."),
        new("romblon", "Romblon", "Romblon", 12.575, 122.270, 2, "Romblon is known for its marble. Look at the carvings in the shops."),
        new("iloilo", "Iloilo City", "Panay", 10.697, 122.564, 6, "In January the Dinagyang festival fills the streets with drums."),
        new("bacolod", "Bacolod", "Negros", 10.676, 122.951, 5, "They call Bacolod the City of Smiles, for the MassKara festival in October."),
        new("dumaguete", "Dumaguete", "Negros", 9.307, 123.308, 5, "Walk the boulevard by the sea in the evening. Apo Island's marine sanctuary is south of here."),
        new("cebu", "Cebu City", "Cebu", 10.293, 123.902, 6, "Magellan's Cross is kept in a little chapel in the city. A cross was planted here in 1521."),
        new("tagbilaran", "Tagbilaran", "Bohol", 9.648, 123.854, 5, "Inland are the Chocolate Hills, and tarsiers live in the forests. Don't touch them: they frighten easily."),
        new("tacloban", "Tacloban", "Leyte", 11.244, 125.004, 5, "The San Juanico Bridge crosses the strait to Samar. It's the longest bridge over the sea in the country."),
        new("catbalogan", "Catbalogan", "Samar", 11.776, 124.886, 5, "Samar's west coast faces the Samar Sea. Good fishing out there, on a calm day."),
        new("masbate", "Masbate City", "Masbate", 12.370, 123.620, 5, "Masbate holds a rodeo every year. It's cattle country, this."),
        new("davao", "Davao City", "Mindanao", 7.073, 125.613, 6, "Mount Apo, the highest mountain in the country, stands behind the city."),
        new("zamboanga", "Zamboanga City", "Mindanao", 6.910, 122.074, 6, "Look for the vintas, our boats with the bright striped sails."),
        new("cdo", "Cagayan de Oro", "Mindanao", 8.482, 124.647, 5, "People come to ride the rapids on the Cagayan River."),
    };

    // One building or prop of a town: which sprite, where its anchor is (world pixels: the middle of its front on the ground),
    // mirrored or not, and its solid box (none for the small things you can walk among).
    sealed class PhThing
    {
        public string Kind, Art;
        public int X, Y;
        public bool Mirror, Solid;
        public int Tint;       // 0 as rendered, 1 sun-bleached, 2 dark old wood, 3 freshly built (BlitKeyed)
        public string Owner;   // a house's villager
    }

    sealed class PhTown
    {
        public PhTownDef Def;
        public int Cx, Cy;                  // the plaza's middle (tiles)
        public int PierX, PierY, PierDx, PierDy, PierLen;   // the pier's first tile in the water, its direction and length
        public int ShoreX, ShoreY;          // the last land tile before the pier
        public readonly List<PhThing> Things = new();
        public readonly Dictionary<(int, int), char> Tiles = new();   // tiles stamped over the country's own
        public PhThing Market, Church, Inn;
        public float MinX, MinY, MaxX, MaxY;   // everything, in world pixels (with a margin), for culling
        public readonly List<string> Folk = new();
    }

    static List<PhTown> phTowns;
    static Dictionary<(int, int), char> phTownTiles;

    // Every town, laid out once (from the country's tiles: the same in every game).
    static List<PhTown> PhTowns()
    {
        if (phTowns != null) return phTowns;
        PhFields();
        var list = new List<PhTown>();
        var tiles = new Dictionary<(int, int), char>();
        // Every town's plaza first (none within PhTownGap tiles of another), so each town can keep to the ground nearer its
        // own plaza than any other's: on a small island like Marinduque, at 1.1 km a tile, the towns are close neighbours.
        var centres = new List<(int x, int y)>();
        var defs = new List<(PhTownDef d, int x, int y)>();
        foreach (var d in PhTownDefs)
            if (PhTownCentre(d, centres) is var (x, y)) { centres.Add((x, y)); defs.Add((d, x, y)); }
        var taken = new HashSet<(int, int)>();
        foreach (var (d, x, y) in defs.OrderBy(v => v.d.Id == "boac" ? 0 : PhVillages.Contains(v.d.Id) ? 1 : 2))   // Boac (where you start), then the villages (they need only a little), then the towns, which have room inland
        {
            var t = LayOutTown(d, x, y, centres, taken);
            if (t == null) continue;
            list.Add(t);
            foreach (var kv in t.Tiles) tiles[kv.Key] = kv.Value;
        }
        phTownTiles = tiles;
        return phTowns = list;
    }

    static PhTown PhTownById(string id) => PhTowns().FirstOrDefault(t => t.Def.Id == id);

    // A town's stamp over a tile (the plaza, the lane, the pier, ground cleared of trees), or none.
    static char? PhTownTile(int x, int y)
    {
        PhTowns();
        return phTownTiles.TryGetValue((x, y), out var c) ? c : null;
    }

    // Sprite sizes: (art id) -> (grid, palette, anchor, footprint W x D).
    static (string[] grid, string[] pal, int ax, int ay, int w, int d) PhArt(string art) => art switch
    {
        "Church" => (PhTownArt.Church, PhTownArt.ChurchPalette, PhTownArt.ChurchAnchorX, PhTownArt.ChurchAnchorY, PhTownArt.ChurchW, PhTownArt.ChurchD),
        "Market" => (PhTownArt.Market, PhTownArt.MarketPalette, PhTownArt.MarketAnchorX, PhTownArt.MarketAnchorY, PhTownArt.MarketW, PhTownArt.MarketD),
        "House0" => (PhTownArt.House0, PhTownArt.House0Palette, PhTownArt.House0AnchorX, PhTownArt.House0AnchorY, PhTownArt.House0W, PhTownArt.House0D),
        "House1" => (PhTownArt.House1, PhTownArt.House1Palette, PhTownArt.House1AnchorX, PhTownArt.House1AnchorY, PhTownArt.House1W, PhTownArt.House1D),
        "House2" => (PhTownArt.House2, PhTownArt.House2Palette, PhTownArt.House2AnchorX, PhTownArt.House2AnchorY, PhTownArt.House2W, PhTownArt.House2D),
        "House3" => (PhTownArt.House3, PhTownArt.House3Palette, PhTownArt.House3AnchorX, PhTownArt.House3AnchorY, PhTownArt.House3W, PhTownArt.House3D),
        "House4" => (PhTownArt.House4, PhTownArt.House4Palette, PhTownArt.House4AnchorX, PhTownArt.House4AnchorY, PhTownArt.House4W, PhTownArt.House4D),
        "House5" => (PhTownArt.House5, PhTownArt.House5Palette, PhTownArt.House5AnchorX, PhTownArt.House5AnchorY, PhTownArt.House5W, PhTownArt.House5D),
        "House6" => (PhTownArt.House6, PhTownArt.House6Palette, PhTownArt.House6AnchorX, PhTownArt.House6AnchorY, PhTownArt.House6W, PhTownArt.House6D),
        "House7" => (PhTownArt.House7, PhTownArt.House7Palette, PhTownArt.House7AnchorX, PhTownArt.House7AnchorY, PhTownArt.House7W, PhTownArt.House7D),
        "Flagpole" => (PhTownArt.Flagpole, PhTownArt.FlagpolePalette, PhTownArt.FlagpoleAnchorX, PhTownArt.FlagpoleAnchorY, PhTownArt.FlagpoleW, PhTownArt.FlagpoleD),
        "Pump" => (PhTownArt.Pump, PhTownArt.PumpPalette, PhTownArt.PumpAnchorX, PhTownArt.PumpAnchorY, PhTownArt.PumpW, PhTownArt.PumpD),
        "FishTable" => (PhTownArt.FishTable, PhTownArt.FishTablePalette, PhTownArt.FishTableAnchorX, PhTownArt.FishTableAnchorY, PhTownArt.FishTableW, PhTownArt.FishTableD),
        "Laundry0" => (PhTownArt.Laundry0, PhTownArt.Laundry0Palette, PhTownArt.Laundry0AnchorX, PhTownArt.Laundry0AnchorY, PhTownArt.Laundry0W, PhTownArt.Laundry0D),
        "Laundry1" => (PhTownArt.Laundry1, PhTownArt.Laundry1Palette, PhTownArt.Laundry1AnchorX, PhTownArt.Laundry1AnchorY, PhTownArt.Laundry1W, PhTownArt.Laundry1D),
        "Garden0" => (PhTownArt.Garden0, PhTownArt.Garden0Palette, PhTownArt.Garden0AnchorX, PhTownArt.Garden0AnchorY, PhTownArt.Garden0W, PhTownArt.Garden0D),
        "Garden1" => (PhTownArt.Garden1, PhTownArt.Garden1Palette, PhTownArt.Garden1AnchorX, PhTownArt.Garden1AnchorY, PhTownArt.Garden1W, PhTownArt.Garden1D),
        "SmallBoat" => (PhTownArt.SmallBoat, PhTownArt.SmallBoatPalette, PhTownArt.SmallBoatAnchorX, PhTownArt.SmallBoatAnchorY, PhTownArt.SmallBoatW, PhTownArt.SmallBoatD),
        "Banana0" => (PhTownArt.Banana0, PhTownArt.Banana0Palette, PhTownArt.Banana0AnchorX, PhTownArt.Banana0AnchorY, PhTownArt.Banana0W, PhTownArt.Banana0D),
        "Banana1" => (PhTownArt.Banana1, PhTownArt.Banana1Palette, PhTownArt.Banana1AnchorX, PhTownArt.Banana1AnchorY, PhTownArt.Banana1W, PhTownArt.Banana1D),
        "Dryer" => (PhTownArt.Dryer, PhTownArt.DryerPalette, PhTownArt.DryerAnchorX, PhTownArt.DryerAnchorY, PhTownArt.DryerW, PhTownArt.DryerD),
        _ => (PhTownArt.Bench, PhTownArt.BenchPalette, PhTownArt.BenchAnchorX, PhTownArt.BenchAnchorY, PhTownArt.BenchW, PhTownArt.BenchD),
    };

    static readonly string[] PhHouseArts = { "House0", "House1", "House2", "House3", "House4", "House5", "House6", "House7" };

    static bool PhLandTile(int x, int y) => PhKindAt(x, y) == 3;
    static bool PhSeaTile(int x, int y) => PhKindAt(x, y) is 0 or 1;

    const int PhTownGap = 11, PhTownReach = 14;
    // Villages: a market, an inn and a house or two by the pier, no church (one church to a town, and only in the bigger ones:
    // at 1.1 km a tile Marinduque's towns are close neighbours, and a church in each crammed it).
    static readonly HashSet<string> PhVillages = new() { "santacruz", "romblon" };

    // The plaza: the nearest land to the town's real place a few tiles in from the shore, clear of the towns placed before.
    static (int x, int y)? PhTownCentre(PhTownDef d, List<(int x, int y)> others)
    {
        int tx0 = (int)((d.Lon - PhMapData.Lon0) * PhMapData.Kx), ty0 = (int)((PhMapData.Lat1 - d.Lat) * PhMapData.Ky);
        // (Closer to a neighbour only if there's no other way: a small island's towns are tight.)
        foreach (int gap in new[] { PhTownGap, 9, 7 })
        {
            int cx = -1, cy = -1;
            double best = double.MaxValue;
            for (int y = ty0 - 30; y <= ty0 + 30; y++)
                for (int x = tx0 - 30; x <= tx0 + 30; x++)
                {
                    if (!PhLandTile(x, y)) continue;
                    float ld = phLandDist[y * PhMapData.Cols + x];
                    if (ld < 1.5f || ld > 8) continue;
                    if (others.Any(o => Math.Max(Math.Abs(o.x - x), Math.Abs(o.y - y)) < gap)) continue;
                    double dd = (x - tx0) * (x - tx0) + (y - ty0) * (y - ty0);
                    if (dd < best) { best = dd; cx = x; cy = y; }
                }
            if (cx >= 0) return (cx, cy);
        }
        return null;
    }

    static PhTown LayOutTown(PhTownDef d, int cx, int cy, List<(int x, int y)> centres, HashSet<(int, int)> taken)
    {
        int seed = d.Id.Aggregate(17, (h, c) => h * 31 + c);
        double H(int a, int b = 0) => Pix.Hash(seed + a * 7, b, 733);
        // This town's ground: within PhTownReach tiles of its plaza, not another town's already, not round another town's
        // plaza, and not much nearer another plaza than its own (a little give, so close neighbours on a small island can
        // each find room).
        bool Mine(int x, int y)
        {
            if (taken.Contains((x, y)) || Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) > PhTownReach) return false;
            double dm = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            foreach (var o in centres)
            {
                if ((o.x, o.y) == (cx, cy)) continue;
                if (Math.Abs(x - o.x) <= 4 && Math.Abs(y - o.y) <= 3) return false;   // its plaza
                if (Math.Sqrt((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y)) + 2.5 < dm) return false;
            }
            return true;
        }
        var t = new PhTown { Def = d, Cx = cx, Cy = cy };
        // The way to the sea: the nearest sea tile along a straight line in one of the four directions.
        int bestLen = int.MaxValue;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int len = 0, x = cx, y = cy;
            while (len < 14 && !PhSeaTile(x, y) && PhKindAt(x, y) != 2) { x += dx; y += dy; len++; }
            if (!PhSeaTile(x, y) || !Mine(x, y)) continue;
            // a pier needs open water beyond the beach
            int water = 0;
            while (water < 5 && PhSeaTile(x + dx * water, y + dy * water)) water++;
            if (water < 3) continue;
            // A longer pier wins a near tie.
            int score = len * 2 - water;
            if (score < bestLen) { bestLen = score; t.PierX = x; t.PierY = y; t.PierDx = dx; t.PierDy = dy; t.PierLen = water; }
        }
        if (bestLen == int.MaxValue) { t.PierDx = 0; t.PierDy = 1; t.PierLen = 0; }
        else
        {
            t.ShoreX = t.PierX - t.PierDx; t.ShoreY = t.PierY - t.PierDy;
            char deck = t.PierDx != 0 ? 'd' : 'b';
            for (int k = 0; k < t.PierLen; k++) t.Tiles[(t.PierX + t.PierDx * k, t.PierY + t.PierDy * k)] = deck;
        }
        var used = new HashSet<(int, int)>();
        void Pave(int x, int y) { if (PhLandTile(x, y) && Mine(x, y)) { t.Tiles[(x, y)] = 'p'; used.Add((x, y)); } }
        // The plaza (6 x 4) and the lane down to the pier.
        for (int y = cy - 2; y < cy + 2; y++) for (int x = cx - 3; x < cx + 3; x++) Pave(x, y);
        if (t.PierLen > 0)
        {
            int x = cx, y = cy;
            while ((x, y) != (t.ShoreX + t.PierDx, t.ShoreY + t.PierDy) && Math.Abs(x - cx) + Math.Abs(y - cy) < 30)
            {
                Pave(x, y);
                if (t.PierDx != 0) { if (y != t.ShoreY) y += Math.Sign(t.ShoreY - y); else x += t.PierDx; }
                else { if (x != t.ShoreX) x += Math.Sign(t.ShoreX - x); else y += t.PierDy; }
            }
        }
        // Places for buildings: a footprint of tiles (wide x deep) whose front row's middle is the anchor, all land at least
        // a tile from the water, none on the plaza or another building (with a tile between).
        int mg = PhVillages.Contains(d.Id) ? 1 : 2;   // the clear ground round each building (tighter in a village)
        bool Fits(int lx, int ly, int fw, int fd)
        {
            for (int y = ly - mg; y <= ly + fd; y++)
                for (int x = lx - mg; x <= lx + fw + mg - 1; x++)
                {
                    // The building itself stands on land; the gap round it only has to be clear of other things.
                    if (used.Contains((x, y)) || taken.Contains((x, y))) return false;
                    bool inside = x >= lx && x < lx + fw && y >= ly && y < ly + fd;
                    if (inside && (!Mine(x, y) || !PhLandTile(x, y))) return false;
                }
            return true;
        }
        // Where each building is drawn (world pixels): no building may stand where another's picture would cover it, or be
        // covered by it, so you can always see every one (they used to stand behind each other, hidden).
        var drawn = new List<(int x0, int y0, int x1, int y1)>();
        (int, int, int, int) Box(string art, int x, int y)
        {
            var (grid, _, ax, ay, _, _) = PhArt(art);
            int half = Math.Max(ax, grid[0].Length - ax);   // (either way round: it may be mirrored)
            return (x - half, y - ay, x + half, y - ay + grid.Length);
        }
        var mtns = PhMountainRects();   // (and never under or over a mountain's drawing)
        bool Clear((int x0, int y0, int x1, int y1) b, int slack) =>
            drawn.All(d => b.x1 + slack <= d.x0 || b.x0 >= d.x1 + slack || b.y1 + slack <= d.y0 || b.y0 >= d.y1 + slack)
            && mtns.All(m => b.x1 + 6 <= m.x0 || b.x0 >= m.x1 + 6 || b.y1 + 6 <= m.y0 || b.y0 >= m.y1 + 6);
        PhThing Place(string kind, string art, int fw, int fd, Func<int, int, double> score, bool solid = true)
        {
            int bx = 0, by = 0; double bs = double.MaxValue;
            for (int ly = cy - 16; ly <= cy + 16; ly++)
                for (int lx = cx - 18; lx <= cx + 18; lx++)
                {
                    if (!Fits(lx, ly, fw, fd) || !Clear(Box(art, lx * T + fw * T / 2, (ly + fd) * T), mg == 1 ? 1 : 4)) continue;
                    double s = score(lx + fw / 2, ly + fd);
                    if (s < bs) { bs = s; bx = lx; by = ly; }
                }
            if (bs == double.MaxValue) return null;
            for (int y = by; y < by + fd; y++) for (int x = bx; x < bx + fw; x++) used.Add((x, y));
            var th = new PhThing { Kind = kind, Art = art, X = bx * T + fw * T / 2, Y = (by + fd) * T, Solid = solid, Mirror = H(bx, by) < 0.5 };
            t.Things.Add(th);
            drawn.Add(Box(art, th.X, th.Y));
            return th;
        }
        double Dc(int x, int y) => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        // Inland is away from the pier: the church stands on the far side of the plaza, the market by the lane's sea end.
        int ix = cx - t.PierDx * 6, iy = cy - t.PierDy * 6;
        // What a visitor needs comes first (the market, then the inn), then the church, then the other houses.
        int mx = t.PierLen > 0 ? t.ShoreX - t.PierDx * 3 : cx + 4, my = t.PierLen > 0 ? t.ShoreY - t.PierDy * 3 : cy + 3;
        t.Market = Place("market", "Market", 5, 3, (x, y) => Math.Abs(x - mx) + Math.Abs(y - my))
            ?? Place("market", "Market", 4, 2, (x, y) => Math.Abs(x - mx) + Math.Abs(y - my));   // (a tight village)
        // Houses round the plaza, nearest first with a little jitter, so towns differ.
        // Every house of a town a different model (shuffled by the town), each with its own weathering (Tint).
        var models = PhHouseArts.OrderBy(a => Pix.Hash(seed, a[^1], 739)).ToArray();
        PhThing House(int i)
        {
            var h = Place("house", models[i % models.Length], 4, 2, (x, y) => Dc(x, y) + H(x * 13 + i, y) * 6);
            if (h != null) h.Tint = (int)(H(i, 17) * 4);
            return h;
        }
        if ((House(0) ?? Place("house", "House4", 3, 2, (x, y) => Dc(x, y))) is PhThing first) { t.Inn = first; first.Kind = "inn"; }   // (the narrowest house, on a tight island)
        if (!PhVillages.Contains(d.Id)) t.Church = Place("church", "Church", 6, 3, (x, y) => Math.Abs(x - ix) + Math.Abs(y - iy) * 1.2)
            ?? Place("church", "Church", 5, 3, (x, y) => Math.Abs(x - ix) + Math.Abs(y - iy) * 1.2);
        if (t.Church != null) t.Church.Mirror = false;
        for (int i = 1; i < d.Houses; i++)
            if (House(i) == null) break;
        // The small things: a pump and a flagpole on the plaza's edge, washing and gardens by houses, banana plants, and a
        // fish table, a drying rack and a boat pulled up on the sand by the pier.
        PhThing Prop(string art, int x, int y, bool solid = false)
        {
            var b = Box(art, x, y);
            if (!Clear(b, 1)) return null;   // never over (or under) a building or another prop
            var p = new PhThing { Kind = "prop", Art = art, X = x, Y = y, Solid = solid, Mirror = H(x, y) < 0.5 };
            t.Things.Add(p);
            drawn.Add(b);
            return p;
        }
        Prop("Flagpole", (cx - 3) * T + 2, (cy - 2) * T + 4, true);
        Prop("Pump", (cx + 3) * T - 4, (cy + 2) * T - 2, true);
        Prop("Bench", cx * T, (cy - 2) * T + 2);
        foreach (var h in t.Things.Where(h => h.Kind is "house" or "inn").Where((_, i) => i % 2 == 0).ToList())   // (every other house: room to breathe)
        {
            double r = H(h.X, h.Y);
            int side = r < 0.5 ? -1 : 1;
            var (_, _, _, _, w, _) = PhArt(h.Art);
            int px = h.X + side * (w / 2 + 14), py = h.Y - 4;
            int ptx = px / T, pty = py / T;
            if (!PhLandTile(ptx, pty) || !Mine(ptx, pty) || used.Contains((ptx, pty)) || used.Contains((ptx + side, pty))) continue;
            used.Add((ptx, pty)); used.Add((ptx + side, pty));
            Prop(r < 0.2 ? "Laundry0" : r < 0.4 ? "Laundry1" : r < 0.6 ? "Garden0" : r < 0.8 ? "Garden1" : (r < 0.9 ? "Banana0" : "Banana1"), px, py, r >= 0.8);
        }
        if (t.PierLen > 0)
        {
            // Sand either side of the pier's foot.
            int sx = t.PierDy != 0 ? 1 : 0, sy = t.PierDx != 0 ? 1 : 0;
            foreach (int s in new[] { -2, 2, -4 })
            {
                int x = t.ShoreX + sx * s, y = t.ShoreY + sy * s;
                if (!PhLandTile(x, y) || !Mine(x, y) || used.Contains((x, y))) continue;
                used.Add((x, y));
                string art = s == -2 ? "FishTable" : s == 2 ? "SmallBoat" : "Dryer";
                Prop(art, x * T + 5, y * T + 8, art != "SmallBoat");
            }
        }
        foreach (var u in used) taken.Add(u);
        foreach (var k in t.Tiles.Keys) taken.Add(k);
        // Clear the trees and bushes off the town's ground: everything built on, and most of what's round it.
        foreach (var (x, y) in used.ToList())
            for (int yy = y - 2; yy <= y + 1; yy++)
                for (int xx = x - 1; xx <= x + 1; xx++)
                    if (!t.Tiles.ContainsKey((xx, yy)) && PhPlant(PhTile(xx, yy))) t.Tiles[(xx, yy)] = PhGround(xx, yy);
        // (Only round what's built: clearing most of the trees within 16 tiles of each plaza left Marinduque nearly bare.)
        // And any tree or bush standing just in front of a building, whose crown would hide it (only buildings, only the few rows
        // in front: clearing round every prop too left Marinduque bare).
        foreach (var th in t.Things.Where(h => h.Kind != "prop"))
        {
            var (x0, y0, w0, h0) = PhSolidOf(th);
            for (int y = th.Y / T; y <= th.Y / T + 3; y++)
                for (int x = x0 / T - 1; x <= (x0 + w0) / T; x++)
                    if (!t.Tiles.ContainsKey((x, y)) && !taken.Contains((x, y)) && PhPlant(PhTile(x, y))) t.Tiles[(x, y)] = PhGround(x, y);
            // (and the ones growing inside its walls)
            for (int y = y0 / T; y < th.Y / T; y++)
                for (int x = x0 / T; x <= (x0 + w0) / T; x++)
                    if (!t.Tiles.ContainsKey((x, y)) && !taken.Contains((x, y)) && PhPlant(PhTile(x, y))) t.Tiles[(x, y)] = PhGround(x, y);
        }
        // The bounds, for culling.
        t.MinX = (cx - 22) * T; t.MaxX = (cx + 22) * T; t.MinY = (cy - 20) * T; t.MaxY = (cy + 20) * T;
        // Its people: one in each house.
        foreach (var h in t.Things.Where(h => h.Kind is "house" or "inn"))
        {
            h.Owner = $"{d.Id}:{t.Folk.Count}";
            t.Folk.Add(h.Owner);
        }
        return t;
    }

    /* ---------- the game's side ---------- */

    // Towns whose ground is in or near the view (or round you).
    IEnumerable<PhTown> PhTownsNear(float wx, float wy, float margin) =>
        PhTowns().Where(t => wx > t.MinX - margin && wx < t.MaxX + margin && wy > t.MinY - margin && wy < t.MaxY + margin);

    static (int x0, int y0, int w, int h) PhSolidOf(PhThing th)
    {
        var (grid, _, ax, ay, w, d) = PhArt(th.Art);
        if (th.Kind == "prop")
        {
            int depth = Math.Clamp((int)(d * 0.55f), 3, 26);
            int ww = Math.Max(4, (int)(w * 0.86f));
            return (th.X - ww / 2, th.Y - depth, ww, depth);
        }
        // A building blocks the whole ground its picture stands over, so nobody (you, a villager, a goat) can walk in behind
        // it and seem to pass through the walls: with only its front part solid, people strolled behind the roof.
        int gw = grid[0].Length;
        int left = th.Mirror ? th.X - (gw - ax) : th.X - ax, right = left + gw;
        int top = th.Y - ay + 6;
        return (left + 2, top, right - left - 4, th.Y - top);
    }

    // The solid boxes of the towns round you (Solids reads these on this map).
    List<Box> PhTownSolids()
    {
        var list = new List<Box>();
        if (!PhWorld || scene != "world") return list;
        foreach (var t in PhTownsNear(player.X, player.Y, 200))
            foreach (var th in t.Things)
                if (th.Solid) { var (x, y, w, h) = PhSolidOf(th); list.Add(new Box(x, y, w, h)); }
        return list;
    }

    // Drawing: each thing y-sorted with everything else (DrawObjects).
    void AddPhTownObjects(List<(float y, Action draw)> list, Func<float, float, bool> visible)
    {
        AddPhPlumes(list, visible);
        foreach (var t in PhTownsNear(camX + W / 2f, camY + H / 2f, Math.Max(W, H)))
        {
            foreach (var th in t.Things)
            {
                var (grid, pal, ax, ay, w, _) = PhArt(th.Art);
                if (!visible(th.X - w / 2, th.Y) && !visible(th.X + w / 2, th.Y) && !visible(th.X, th.Y - grid.Length)) continue;
                var thing = th;
                list.Add((th.Y, () => BlitKeyed(grid, pal, ax, ay, thing.X, thing.Y, thing.Mirror, thing.Tint)));
            }
            foreach (var id in t.Folk)
            {
                var s = PhFolkWalk(id);
                if (FolkAsleep || !visible(s.X, s.Y)) continue;
                string fid = id;
                list.Add((s.Y, () => DrawPhFolk(fid)));
            }
            if (t.Market != null && PhMarketOpen && visible(t.Market.X, t.Market.Y))
            {
                var m = t.Market;
                string vid = t.Def.Id;
                list.Add((m.Y + 3, () => LookData.DrawPerson(pix, PhVendorLook(vid), m.X + 10, m.Y + 3, "down", 0, bob: (int)(time % 3 / 2),
                    blink: (time + m.X * 0.01f) % 4.1f < 0.12f)));
            }
        }
    }

    // A grid from PhTownArt (keys index the palette, '.' clear), mirrored about its anchor's column if asked.
    static readonly Dictionary<string[], Color[][]> phPalCache = new(ReferenceEqualityComparer.Instance);   // by the palette itself (and the tint)
    void BlitKeyed(string[] grid, string[] pal, int ax, int ay, int x, int y, bool mirror, int tint = 0)
    {
        if (!phPalCache.TryGetValue(pal, out var tints)) phPalCache[pal] = tints = new Color[4][];
        var cols = tints[tint & 3];
        if (cols == null)
            tints[tint & 3] = cols = pal.Select(h => TintHouse(Pal.C(h), tint)).ToArray();
        int oy = y - ay;
        for (int j = 0; j < grid.Length; j++)
        {
            string row = grid[j];
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '.') continue;
                int k = ch <= '9' ? ch - '0' : ch <= 'Z' ? ch - 'A' + 36 : ch - 'a' + 10;
                if (k < 0 || k >= cols.Length) continue;
                int px = mirror ? x - (i - ax) : x + (i - ax);
                pix.Rect(px, oy + j, 1, 1, cols[k]);
            }
        }
    }

    // A house's weathering: the same model looks like another family's house.
    static Color TintHouse(Color c, int tint)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f, l = (r + g + b) / 3;
        (r, g, b) = tint switch
        {
            1 => (l + (r - l) * 0.7f + 0.05f, l + (g - l) * 0.7f + 0.05f, l + (b - l) * 0.7f + 0.06f),   // sun-bleached
            2 => (r * 0.86f, g * 0.8f, b * 0.76f),                                                          // dark old wood
            3 => (l + (r - l) * 1.15f + 0.02f, l + (g - l) * 1.15f, l + (b - l) * 1.1f - 0.02f),            // fresh, warmer
            _ => (r, g, b)
        };
        return new Color((byte)Math.Clamp(r * 255, 0, 255), (byte)Math.Clamp(g * 255, 0, 255), (byte)Math.Clamp(b * 255, 0, 255), c.A);
    }

    // A shadow under each building, with the others' (Ambience's ground pass).
    void PhTownShadows()
    {
        if (!PhWorld) return;
        foreach (var t in PhTownsNear(camX + W / 2f, camY + H / 2f, Math.Max(W, H)))
            foreach (var th in t.Things)
                if (th.Kind is "house" or "inn" or "church" or "market")
                {
                    var (x, y, w, h) = PhSolidOf(th);
                    BuildingShadow(x, th.Y - h + 3, w, h);
                }
    }

    /* ---------- people ---------- */
    static readonly string[] PhFirstNames =
    {
        "Ana", "Ben", "Carmen", "Dodong", "Elena", "Ferdie", "Gloria", "Hernan", "Inday", "Jose", "Karla", "Lito", "Marites", "Nonoy",
        "Ofelia", "Pablo", "Queenie", "Rodel", "Susan", "Tonyo", "Ursula", "Vicente", "Weng", "Yoly", "Zaldy", "Aling Baby", "Mang Caloy",
        "Aling Dory", "Mang Eddie", "Lola Fely", "Lolo Gener", "Ate Hazel", "Kuya Isko", "Manang Juana", "Tatay Kiko",
    };
    readonly Dictionary<string, Stroller> phFolkWalk = new();

    // A villager's id is "<town>:<n>"; their house is the town's nth house.
    (PhTown town, PhThing house) PhFolkHome(string id)
    {
        var parts = id.Split(':');
        var t = PhTownById(parts[0]);
        var h = t?.Things.FirstOrDefault(x => x.Owner == id);
        return (t, h);
    }

    Stroller PhFolkWalk(string id)
    {
        if (phFolkWalk.TryGetValue(id, out var s)) return s;
        var (_, h) = PhFolkHome(id);
        float hx = h.X + 9, hy = h.Y + 7;
        s = new Stroller { X = hx, Y = hy, HomeX = hx, HomeY = hy, RangeX = 16, RangeUp = 2, RangeDown = 14 };
        phFolkWalk[id] = s;
        return s;
    }

    // A well-mixed hash of a name (a plain one barely changed with the last letter, so a whole town looked alike).
    static int PhMix(string id, int salt)
    {
        uint h = 2166136261u ^ (uint)salt;
        foreach (char c in id) { h ^= c; h *= 16777619u; }
        h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
        return (int)(h & 0x7fffffff);
    }

    static string PhFolkName(string id)
    {
        int h = PhMix(id, 5);
        return PhFirstNames[h % PhFirstNames.Length];
    }

    static Look PhFolkLook(string id)
    {
        int h = PhMix(id, 11);
        return new Look
        {
            name = PhFolkName(id), skin = 1 + h % 4, hair = (h >> 3) % 2, hairColor = (h >> 5) % 9 == 0 ? 5 : (h >> 5) % 3 == 0 ? 1 : 0,
            hat = (h >> 7) % 5 == 0 ? 6 : (h >> 7) % 4 == 0 ? 1 : 0, shirt = (h >> 9) % LookData.Shirts.Length, pants = (h >> 12) % LookData.Pants.Length,
        };
    }

    static Look PhVendorLook(string town)
    {
        var l = PhFolkLook(town + ":vendor");
        l.hat = 0; l.shirt = (l.shirt + 2) % LookData.Shirts.Length;
        return l;
    }
    static string PhVendorName(string town) => PhFolkName(town + ":vendor");

    void DrawPhFolk(string id)
    {
        var s = PhFolkWalk(id);
        LookData.TurnFrom = s.Turning;
        LookData.DrawPerson(pix, PhFolkLook(id), (int)MathF.Round(s.X), (int)MathF.Round(s.Y), s.Face, s.Step,
            bob: s.Moving ? 0 : (int)(time % 3 / 2), blink: (time + s.X * 0.01f) % 4.1f < 0.12f);
    }

    void UpdatePhFolk(float dt)
    {
        if (!PhWorld || scene != "world") return;
        foreach (var t in PhTownsNear(player.X, player.Y, 300))
            foreach (var id in t.Folk)
            {
                var s = PhFolkWalk(id);
                if (FolkAsleep || standStill) { s.X = s.HomeX; s.Y = s.HomeY; s.ToX = s.ToY = null; s.Moving = false; continue; }
                Stroll(s, dt, (x, y) => FolkCanStand(x, y));
            }
    }

    bool PhMarketOpen => state.clock >= 6 * 60 && state.clock < 19 * 60;

    // What E does in a town: a villager, the market, the inn, the church.
    Target PhTownTarget()
    {
        if (!PhWorld) return null;
        float x = player.X, y = player.Y;
        // The nearest of everything in reach, so a villager strolling past the market doesn't hide it.
        Target best = null;
        float bd = float.MaxValue;
        void Offer(float d, float reach, Func<Target> make) { if (d < reach && d < bd) { bd = d; best = make(); } }
        foreach (var t in PhTownsNear(x, y, 40))
        {
            foreach (var id in t.Folk)
            {
                var s = PhFolkWalk(id);
                string fid = id;
                Offer(Dist(x, y, s.X, s.Y), 14, () => FolkAsleep ? new Target { Type = "info", Label = $"{PhFolkName(fid)} is asleep. Come back after sunrise." }
                    : new Target { Type = "phfolk", Id = fid, Label = $"Talk to {PhFolkName(fid)}" });
            }
            var town = t;
            if (t.Market is PhThing m)
                Offer(Dist(x, y, m.X + 10, m.Y + 5) - 4, 16, () => PhMarketOpen ? new Target { Type = "phmarket", Id = town.Def.Id, Label = $"Trade with {PhVendorName(town.Def.Id)} at the market" }
                    : new Target { Type = "info", Label = $"The market is shut for the night. It opens at {HourText(6 * 60)}" });
            if (t.Inn is PhThing inn)
                Offer(Dist(x, y, inn.X, inn.Y + 3) - 4, 12, () => new Target { Type = "rest", Label = Night ? "Sleep at the inn until morning" : "Rest at the inn until evening" });
            if (t.Church is PhThing c)
                Offer(Dist(x, y, c.X, c.Y + 3), 14, () => new Target { Type = "phchurch", Id = town.Def.Id, Label = $"Look at the church of {town.Def.Name}" });
        }
        return best;
    }

    void TalkPhFolk(string id)
    {
        var (t, h) = PhFolkHome(id);
        var s = PhFolkWalk(id);
        FaceToward(s.X, s.Y);
        s.Face = Math.Abs(player.X - s.X) > Math.Abs(player.Y - s.Y) ? (player.X > s.X ? "right" : "left") : (player.Y > s.Y ? "down" : "up");
        string name = PhFolkName(id);
        Say L(string text) => new(name, text);
        int n = int.Parse(id.Split(':')[1]);
        var lines = new List<Say>();
        string key = "phmet:" + id;
        if (!state.Hinted(key))
        {
            state.hinted[key] = true;
            lines.Add(L(n == 0 ? $"Welcome to {t.Def.Name}! You came in by banca? There's room at the inn, my place here, if you need a rest." : $"Magandang araw! Welcome to {t.Def.Name}, on {t.Def.Island}."));
            lines.Add(L(t.Def.Fact));
        }
        else
        {
            var tips = new[]
            {
                t.Def.Fact,
                $"{PhVendorName(t.Def.Id)} at the market buys fish and sells bait. Fair prices, mostly.",
                "The reefs off the point are full of lapu-lapu and maya-maya. Cast where the water turns blue.",
                "Out past the shallows you'll find galunggong and tulingan. In a banca, just keep going till the water's dark.",
                "Mangroves are the fishes' nursery. Leave the little ones there to grow.",
                "When the habagat blows, the sea gets rough. Watch the sky before you sail.",
                "My lolo fished these waters all his life. He said the sea gives if you don't take too much.",
                "Take your catch to the market early, before the heat.",
            };
            int pick = (int)(Pix.Hash(n, state.day, 741) * tips.Length);
            lines.Add(L(tips[pick % tips.Length]));
        }
        Talk(lines);
    }

    void TradePhMarket(string town)
    {
        var t = PhTownById(town);
        if (t?.Market is PhThing m) FaceToward(m.X + 10, m.Y + 3);
        shopName = $"{PhVendorName(town)}'s stall, {t?.Def.Name}";
        if (!state.Hinted("phmarket:" + town))
        {
            state.hinted["phmarket:" + town] = true;
            Talk(new() { new(PhVendorName(town), $"Fresh catch? I buy any fish. And I've bait, tackle and food, if you're heading out.") }, OpenShop);
            return;
        }
        OpenShop();
    }

    void LookAtChurch(string town)
    {
        var t = PhTownById(town);
        Sfx.Play("ui");
        Toast($"The church of {t.Def.Name}. The bells ring for mass in the morning.", 3);
    }

    // Arriving in a town: a banner the first time, and it goes on the chart.
    string phTownHere;
    void NotePhTown()
    {
        if (!PhWorld || scene != "world") return;
        string here = null;
        float hd = 16 * T;
        foreach (var t in PhTownsNear(player.X, player.Y, 0))
        {
            float d = Dist(player.X, player.Y, t.Cx * T + 5, t.Cy * T + 5);
            if (d < hd) { hd = d; here = t.Def.Id; }   // the nearest town, not the first one listed
        }
        if (here == phTownHere) return;
        phTownHere = here;
        if (here == null) return;
        var town = PhTownById(here);
        state.phVisited ??= new();
        if (!state.phVisited.Contains(here))
        {
            state.phVisited.Add(here);
            mapTexDirty = true;
            Toast($"{town.Def.Name}, {town.Def.Island}", 4);
            Sfx.Play("blip");
        }
    }

    // The town you're in, for the status card.
    string PhTownName() => phTownHere != null ? PhTownById(phTownHere)?.Def.Name : null;
}
