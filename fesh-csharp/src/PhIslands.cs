namespace Fesh;

// The Philippines map's islands by name (1.48, step 2). Every piece of land joined up tile to tile is one island, and each
// takes the name of a seed point inside it (PhIslandSeeds); an island no seed falls on (an islet) is named after the nearest
// named one ("an islet off Palawan"). Natural Earth's land has no borders, so the corner of Borneo inside the map's box
// (Sabah, Malaysia) is named as such, and the islets nearest it too: before this, everything was named after the nearest
// island's middle, so standing in Sabah said "Palawan".
partial class Game
{
    // (name, latitude, longitude): a point on the island. Several for a long one.
    static readonly (string name, double lat, double lon)[] PhIslandSeeds =
    {
        ("Luzon", 16.5, 121.0), ("Luzon", 14.6, 121.1), ("Luzon", 13.7, 123.3), ("Luzon", 14.0, 122.3), ("Luzon", 18.2, 121.0),
        ("Mindoro", 12.9, 121.1), ("Palawan", 9.8, 118.7), ("Palawan", 11.0, 119.4), ("Palawan", 8.8, 117.8),
        ("Busuanga", 12.1, 120.0), ("Culion", 11.85, 120.0), ("Panay", 11.1, 122.5), ("Guimaras", 10.6, 122.6),
        ("Negros", 10.1, 122.9), ("Negros", 9.4, 123.1), ("Cebu", 10.3, 123.85), ("Bohol", 9.85, 124.15), ("Siquijor", 9.2, 123.55),
        ("Leyte", 10.9, 124.85), ("Leyte", 10.3, 125.0), ("Samar", 11.9, 125.0), ("Biliran", 11.58, 124.47),
        ("Mindanao", 7.8, 124.5), ("Mindanao", 8.0, 126.0), ("Mindanao", 7.5, 122.6), ("Mindanao", 6.9, 122.1), ("Mindanao", 6.5, 124.8),
        ("Camiguin", 9.17, 124.72), ("Dinagat", 10.1, 125.6), ("Siargao", 9.85, 126.05), ("Basilan", 6.55, 121.95), ("Jolo", 6.0, 121.05),
        ("Tawi-Tawi", 5.2, 120.05), ("Sibutu", 4.78, 119.47), ("Masbate", 12.2, 123.5), ("Ticao", 12.55, 123.7), ("Burias", 12.95, 123.25),
        ("Catanduanes", 13.8, 124.25), ("Marinduque", 13.40, 121.97), ("Romblon", 12.55, 122.27), ("Tablas", 12.4, 122.05),
        ("Sibuyan", 12.4, 122.55), ("Polillo", 14.85, 121.95), ("Lubang", 13.8, 120.1), ("Batan", 20.45, 121.97), ("Itbayat", 20.78, 121.84),
        ("Calayan", 19.3, 121.45), ("Babuyan Claro", 19.52, 121.94), ("Camiguin Norte", 18.93, 121.86), ("Fuga", 18.9, 121.3),
        ("Samal", 7.08, 125.73), ("Bantayan", 11.2, 123.73), ("Camotes", 10.65, 124.35), ("Balabac", 7.95, 117.05), ("Cuyo", 10.85, 121.0),
        ("Semirara", 12.07, 121.38), ("Turtle Islands", 6.1, 118.3), ("Mapun", 7.0, 118.5), ("Alabat", 14.1, 122.0),
        ("Borneo (Sabah, Malaysia)", 5.5, 117.5), ("Borneo (Sabah, Malaysia)", 6.6, 116.95), ("Borneo (Sabah, Malaysia)", 5.0, 118.3),
    };
    const string Sabah = "Borneo (Sabah, Malaysia)";

    // Each land tile's island (0 for water), and each island's name: a seeded one, or "an islet off <the nearest named>".
    static int[] phIsle;
    static List<string> phIsleName;
    static void PhIslands()
    {
        if (phIsle != null) return;
        int W0 = PhMapData.Cols, H0 = PhMapData.Rows, n = W0 * H0;
        var grid = PhGrid();
        var isle = new int[n];
        var names = new List<string> { null };
        var q = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            if (grid[i] == 0 || isle[i] != 0) continue;
            int id = names.Count;
            names.Add(null);
            isle[i] = id; q.Enqueue(i);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % W0, y = c / W0;
                void Try(int xx, int yy)
                {
                    if (xx < 0 || yy < 0 || xx >= W0 || yy >= H0) return;
                    int j = yy * W0 + xx;
                    if (grid[j] != 0 && isle[j] == 0) { isle[j] = id; q.Enqueue(j); }
                }
                Try(x + 1, y); Try(x - 1, y); Try(x, y + 1); Try(x, y - 1);
            }
        }
        // Seeds: the island under each (or the nearest land within a few tiles).
        foreach (var (name, lat, lon) in PhIslandSeeds)
        {
            int tx = (int)((lon - PhMapData.Lon0) * PhMapData.Kx), ty = (int)((PhMapData.Lat1 - lat) * PhMapData.Ky);
            int found = 0;
            for (int r = 0; r <= 8 && found == 0; r++)
                for (int dy = -r; dy <= r && found == 0; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = tx + dx, y = ty + dy;
                        if (x < 0 || y < 0 || x >= W0 || y >= H0) continue;
                        if (isle[y * W0 + x] != 0) { found = isle[y * W0 + x]; break; }
                    }
            if (found != 0 && names[found] == null) names[found] = name;
        }
        // Everything else: from the named islands outwards over the sea, the nearest one.
        var near = new int[n];
        var dq = new Queue<int>();
        for (int i = 0; i < n; i++) if (isle[i] != 0 && names[isle[i]] != null) { near[i] = isle[i]; dq.Enqueue(i); }
        while (dq.Count > 0)
        {
            int c = dq.Dequeue(), x = c % W0, y = c / W0;
            void Try(int xx, int yy)
            {
                if (xx < 0 || yy < 0 || xx >= W0 || yy >= H0) return;
                int j = yy * W0 + xx;
                if (near[j] == 0) { near[j] = near[c]; dq.Enqueue(j); }
            }
            Try(x + 1, y); Try(x - 1, y); Try(x, y + 1); Try(x, y - 1);
        }
        var label = new string[names.Count];
        for (int i = 0; i < n; i++)
        {
            int id = isle[i];
            if (id == 0 || names[id] != null || label[id] != null) continue;
            string by = names[near[i]] ?? "the islands";
            label[id] = by == Sabah ? "an islet of Sabah, Malaysia" : $"an islet off {by}";
        }
        for (int id = 1; id < names.Count; id++) names[id] ??= label[id] ?? "an islet";
        phIsleName = names;
        phIsle = isle;
    }

    // The island at a tile (0 none).
    static int PhIsleAt(int tx, int ty) { PhIslands(); return tx < 0 || ty < 0 || tx >= PhMapData.Cols || ty >= PhMapData.Rows ? 0 : phIsle[ty * PhMapData.Cols + tx]; }
    static string PhIsleName(int id) { PhIslands(); return id > 0 && id < phIsleName.Count ? phIsleName[id] : null; }

    // The island under a world point, or the nearest one within three tiles (a beach pixel whose tile is still the sea).
    static string PhIslandAt(float wx, float wy, int reach = 3)
    {
        int tx = (int)MathF.Floor(wx / T), ty = (int)MathF.Floor(wy / T);
        for (int r = 0; r <= reach; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (PhIsleAt(tx + dx, ty + dy) is int id and > 0) return PhIsleName(id);
        return null;
    }

    // The seas and straits, by the nearest of their middles.
    static readonly (string name, double lat, double lon)[] PhSeas =
    {
        ("Sibuyan Sea", 12.6, 122.4), ("Tablas Strait", 12.9, 121.8), ("Sulu Sea", 8.5, 120.5), ("Sulu Sea", 9.5, 121.5), ("Philippine Sea", 13.0, 126.0),
        ("Philippine Sea", 16.5, 124.0), ("Philippine Sea", 9.0, 126.5), ("West Philippine Sea", 14.5, 118.8), ("West Philippine Sea", 17.5, 119.3),
        ("West Philippine Sea", 11.0, 117.5), ("Visayan Sea", 11.4, 123.6), ("Bohol Sea", 9.1, 124.3), ("Celebes Sea", 5.5, 123.0), ("Moro Gulf", 7.0, 123.4),
        ("Luzon Strait", 20.0, 121.3), ("Babuyan Channel", 18.8, 121.6), ("Manila Bay", 14.5, 120.75), ("Verde Island Passage", 13.6, 121.1),
        ("Tayabas Bay", 13.6, 121.8), ("Mindoro Strait", 12.3, 120.5), ("Balabac Strait", 7.6, 117.0), ("Camotes Sea", 10.5, 124.3),
        ("Samar Sea", 12.0, 124.3), ("Ragay Gulf", 13.6, 122.7), ("Lamon Bay", 14.3, 122.0), ("Ticao Pass", 12.7, 123.85), ("Leyte Gulf", 10.9, 125.4),
        ("Guimaras Strait", 10.3, 122.65), ("Tanon Strait", 10.3, 123.5), ("Davao Gulf", 6.8, 125.7), ("Iloilo Strait", 10.65, 122.6),
        ("Cuyo East Pass", 11.4, 121.2), ("Linapacan Strait", 11.5, 119.8), ("Lingayen Gulf", 16.3, 120.25),
    };
    static string PhSeaAt(double lat, double lon)
    {
        string best = "the sea";
        double bd = double.MaxValue;
        foreach (var p in PhSeas)
        {
            double d = (p.lat - lat) * (p.lat - lat) + (p.lon - lon) * (p.lon - lon) * 0.95;
            if (d < bd) { bd = d; best = p.name; }
        }
        return best;
    }
}
