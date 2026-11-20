using Raylib_cs;

namespace Fesh;

// The Philippines map's land in relief (1.48, step 2b). Every land tile has a height in metres (phElev), from:
// - a gentle rise from the shore into rolling hills (by the land's distance from the water, and slow noise);
// - the real mountain ranges (PhRanges: the Cordillera, the Sierra Madre, Mindoro's spine, Palawan's, Panay's, Negros's,
//   Mindanao's...), ridges along their lines with a broken crest;
// - the real peaks and volcanoes (PhPeaks: Mayon, Taal, Pinatubo, Kanlaon, Apo, Malindig...) at their latitude and
//   longitude and their real height, volcanoes as concave cones with a crater, ash and rock on their upper slopes.
// The ground is drawn in 3/4 view from it, like Marilaya's Tindig: slopes facing the light (north-west) lighter and warmer,
// the far ones darker, and the height cut into broken steps whose faces show where the higher ground is to the north. Rock
// and ash ('e') cover a volcano's upper cone, forest thickens with height, active volcanoes smoke, and the chart is shaded
// too. Looks only: you can walk anywhere you could before.
partial class Game
{
    sealed record PhPeak(string Name, double Lat, double Lon, float M, float Rkm, bool Volcano = false, bool Active = false, float CraterKm = 0, bool Lake = false);

    static readonly PhPeak[] PhPeaks =
    {
        // Luzon
        new("Mount Pulag", 16.583, 120.887, 2926, 22), new("Mayon Volcano", 13.257, 123.685, 2463, 10, true, true, 0.4f),
        new("Mount Pinatubo", 15.13, 120.35, 1486, 16, true, false, 2.5f, true), new("Taal Volcano", 14.002, 120.995, 311, 2.6f, true, true, 0.6f),
        new("Mount Banahaw", 14.07, 121.48, 2170, 11, true), new("Mount Makiling", 14.13, 121.20, 1090, 6),
        new("Mount Arayat", 15.20, 120.74, 1026, 6, true), new("Bulusan Volcano", 12.77, 124.05, 1565, 8, true, true, 0.4f),
        new("Mount Isarog", 13.66, 123.38, 1966, 10, true), new("Mount Iriga", 13.46, 123.45, 1196, 5, true),
        new("Mount Data", 16.86, 120.86, 2310, 10), new("Mount Amuyao", 17.02, 121.08, 2702, 12),
        // Mindoro, Marinduque, Romblon, Palawan
        new("Mount Halcon", 13.27, 121.00, 2582, 14), new("Mount Baco", 12.82, 121.11, 2488, 12),
        new("Mount Malindig", 13.24, 122.02, 1157, 7, true), new("Mount Guiting-Guiting", 12.41, 122.56, 2058, 6),
        new("Mount Mantalingajan", 8.80, 117.67, 2085, 12), new("Cleopatra's Needle", 10.12, 118.95, 1593, 7),
        new("Mount Victoria", 9.38, 118.32, 1726, 9),
        // Visayas
        new("Mount Madia-as", 11.38, 122.15, 2117, 11), new("Mount Baloy", 10.95, 122.25, 1958, 8),
        new("Kanlaon Volcano", 10.41, 123.13, 2465, 12, true, true, 0.5f), new("Mount Talinis", 9.25, 123.18, 1903, 8, true),
        new("Mount Silay", 10.75, 123.23, 1535, 8, true), new("Osmena Peak", 9.83, 123.43, 1013, 6),
        new("Biliran Volcano", 11.55, 124.48, 1301, 6, true), new("Mount Nacolod", 10.35, 125.05, 1340, 8),
        // Mindanao
        new("Mount Apo", 6.99, 125.27, 2954, 18, true), new("Mount Kitanglad", 8.13, 124.92, 2899, 15),
        new("Mount Malindang", 8.22, 123.63, 2404, 12, true), new("Mount Ragang", 7.68, 124.50, 2815, 12, true),
        new("Mount Matutum", 6.36, 125.07, 2286, 10, true), new("Hibok-Hibok", 9.20, 124.67, 1332, 5, true, true, 0.3f),
        new("Mount Hamiguitan", 6.73, 126.18, 1620, 9), new("Mount Kalatungan", 7.95, 124.80, 2860, 12, true),
        // The far north
        new("Mount Iraya", 20.47, 122.01, 1009, 4, true), new("Babuyan Claro", 19.52, 121.94, 1080, 4, true),
    };

    // (height in metres, half-width in km, the line it follows)
    static readonly (float m, float wkm, (double lat, double lon)[] pts)[] PhRanges =
    {
        (2300, 20, new[] { (18.4, 120.9), (17.6, 121.0), (16.8, 120.95), (16.1, 120.75) }),                       // the Cordillera Central
        (1400, 18, new[] { (18.3, 122.15), (17.3, 122.2), (16.4, 121.85), (15.6, 121.45), (14.7, 121.4) }),      // the Sierra Madre
        (1300, 10, new[] { (16.3, 120.9), (16.1, 121.3) }),                                                         // the Caraballo
        (1500, 14, new[] { (15.9, 120.15), (15.3, 120.35), (14.85, 120.4) }),                                       // the Zambales
        (900, 14, new[] { (13.8, 122.5), (13.4, 123.1), (12.95, 123.9) }),                                          // Bicol's hills
        (1900, 13, new[] { (13.45, 120.9), (13.0, 121.05), (12.5, 121.15) }),                                       // Mindoro
        (1300, 8, new[] { (11.2, 119.5), (10.5, 119.2), (9.8, 118.65), (9.1, 118.15), (8.55, 117.6) }),             // Palawan
        (1500, 12, new[] { (11.75, 122.05), (11.1, 122.12), (10.6, 122.05) }),                                       // Panay's west
        (1000, 11, new[] { (10.9, 123.25), (10.3, 123.1), (9.5, 123.1) }),                                          // Negros
        (800, 7, new[] { (11.1, 124.0), (10.4, 123.85), (9.7, 123.45) }),                                           // Cebu
        (500, 12, new[] { (10.0, 124.0), (9.75, 124.3) }),                                                           // Bohol's hills
        (1100, 9, new[] { (11.45, 124.65), (10.8, 124.85), (10.1, 125.05) }),                                        // Leyte
        (700, 18, new[] { (12.4, 125.0), (11.8, 125.15), (11.3, 125.4) }),                                          // Samar
        (1700, 22, new[] { (8.6, 124.75), (7.7, 125.1), (6.9, 125.05) }),                                            // central Mindanao
        (1600, 13, new[] { (9.4, 125.85), (8.4, 126.1), (7.1, 126.2) }),                                             // the Diwata
        (1000, 11, new[] { (8.0, 123.1), (7.5, 122.5), (7.0, 122.15) }),                                             // the Zamboanga Peninsula
        (550, 6, new[] { (13.52, 121.95), (13.32, 121.96) }),                                                         // Marinduque's spine
        (700, 9, new[] { (12.45, 123.45), (12.1, 123.6) }),                                                           // Masbate
        (700, 8, new[] { (13.95, 124.2), (13.6, 124.3) }),                                                            // Catanduanes
    };

    static float[] phElev;    // metres, per tile (0 in the water)
    static byte[] phRock;     // 1: a volcano's bare upper cone (rock and ash)
    static (float x, float y) PhTileOf(double lat, double lon) => ((float)((lon - PhMapData.Lon0) * PhMapData.Kx), (float)((PhMapData.Lat1 - lat) * PhMapData.Ky));
    const float KmPerTile = (float)PhMapData.KmPerTile;

    static void PhTerrainField()
    {
        if (phElev != null) return;
        PhFields();
        int W0 = PhMapData.Cols, H0 = PhMapData.Rows, n = W0 * H0;
        var e = new float[n];
        var rock = new byte[n];
        // Rolling ground everywhere, rising from the shore.
        for (int y = 0; y < H0; y++)
            for (int x = 0; x < W0; x++)
            {
                int i = y * W0 + x;
                if (phKind[i] != 3) continue;
                float d = phLandDist[i];
                float hills = (float)Math.Max(0, PhNoise(x * 0.07, y * 0.07) * 0.75 + PhNoise(x * 0.21 + 9, y * 0.21 - 4) * 0.25 - 0.38) * 650 * Math.Min(1, d / 7);
                e[i] = 22 * (1 - MathF.Exp(-d / 1.6f)) + hills;
            }
        void Raise(int x, int y, float h, bool isRock = false)
        {
            if (x < 0 || y < 0 || x >= W0 || y >= H0) return;
            int i = y * W0 + x;
            if (phKind[i] != 3) return;
            if (h > e[i]) e[i] = h;
            if (isRock) rock[i] = 1;
        }
        // Ranges: a ridge along each line, its crest broken by noise.
        foreach (var (m, wkm, pts) in PhRanges)
        {
            float w = wkm / KmPerTile;
            for (int s = 0; s + 1 < pts.Length; s++)
            {
                var (ax, ay) = PhTileOf(pts[s].lat, pts[s].lon);
                var (bx, by) = PhTileOf(pts[s + 1].lat, pts[s + 1].lon);
                int x0 = (int)(Math.Min(ax, bx) - w * 2.2f), x1 = (int)(Math.Max(ax, bx) + w * 2.2f);
                int y0 = (int)(Math.Min(ay, by) - w * 2.2f), y1 = (int)(Math.Max(ay, by) + w * 2.2f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float t = SegDist(x, y, ax, ay, bx, by) / w;
                        if (t > 2.2f) continue;
                        float crest = (float)(0.55 + 0.45 * PhNoise(x * 0.12 + 50, y * 0.12 + 50)) * (float)(0.8 + 0.2 * PhNoise(x * 0.4, y * 0.4));
                        Raise(x, y, m * MathF.Exp(-t * t * 1.6f) * crest);
                    }
            }
        }
        // Peaks and volcanoes.
        foreach (var p in PhPeaks)
        {
            var (px, py) = PhTileOf(p.Lat, p.Lon);
            float r = p.Rkm / KmPerTile, rc = p.CraterKm / KmPerTile;
            for (int y = (int)(py - r - 1); y <= py + r + 1; y++)
                for (int x = (int)(px - r - 1); x <= px + r + 1; x++)
                {
                    float d = MathF.Sqrt((x + 0.5f - px - 0.5f) * (x + 0.5f - px - 0.5f) + (y - py) * (y - py));
                    if (d > r) continue;
                    float k = 1 - d / r;
                    float h = p.Volcano ? p.M * MathF.Pow(k, 1.7f) : p.M * MathF.Pow(k, 1.25f) * (float)(0.85 + 0.15 * PhNoise(x * 0.5, y * 0.5));
                    if (rc > 0 && d < rc) h = p.M * MathF.Pow(1 - rc / r, 1.7f) * 0.97f;   // the crater's floor
                    Raise(x, y, h, p.Volcano && k > 0.5f && p.M > 900);
                }
        }
        // Towns stand on low, gentle ground: the land round each plaza is brought down (Legazpi sat on Mayon's ash).
        var centres = new List<(int x, int y)>();
        foreach (var d in PhTownDefs)
            if (PhTownCentre(d, centres) is var (cx, cy)) centres.Add((cx, cy));
        const int Flat = 8;
        foreach (var (cx, cy) in centres)
            for (int y = cy - Flat; y <= cy + Flat; y++)
                for (int x = cx - Flat; x <= cx + Flat; x++)
                {
                    if (x < 0 || y < 0 || x >= W0 || y >= H0) continue;
                    float k = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Flat;
                    if (k >= 1) continue;
                    int i = y * W0 + x;
                    float keep = k * k * (3 - 2 * k);   // 0 at the plaza, 1 at the edge
                    e[i] = Math.Min(e[i], 60 + (e[i] - 60) * keep);
                    if (k < 0.8f) rock[i] = 0;
                }
        phElev = e;
        phRock = rock;
    }

    static float PhElevTile(int x, int y) => x < 0 || y < 0 || x >= PhMapData.Cols || y >= PhMapData.Rows ? 0 : phElev[y * PhMapData.Cols + x];

    // The height at a world pixel: the tiles' heights blended, with a little ground texture.
    static float PhElevAt(float wx, float wy)
    {
        float fx = wx / T - 0.5f, fy = wy / T - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float ax = fx - x0, ay = fy - y0;
        float h = (PhElevTile(x0, y0) * (1 - ax) + PhElevTile(x0 + 1, y0) * ax) * (1 - ay) + (PhElevTile(x0, y0 + 1) * (1 - ax) + PhElevTile(x0 + 1, y0 + 1) * ax) * ay;
        return h + (VNoise((int)wx, (int)wy, 9, 913) - 0.5f) * Math.Min(40, h * 0.06f);
    }

    // Light from the north-west: the slope in metres per metre, softened so low hills still show.
    static float PhShade(int wx, int wy)
    {
        const int e = 3;
        float gx = (PhElevAt(wx + e, wy) - PhElevAt(wx - e, wy)) / (2 * e), gy = (PhElevAt(wx, wy + e) - PhElevAt(wx, wy - e)) / (2 * e);
        float s = -(gx + gy) / (110f * 1.41f);   // 110 m a pixel; facing the north-west light is a negative slope that way
        return Math.Clamp(MathF.Sign(s) * MathF.Sqrt(MathF.Abs(s)) * 0.85f, -0.42f, 0.42f);
    }


    // A ground pixel on this map, given its plain colour.
    Color PhRelief(int wx, int wy, char g, Color[] tones, float v)
    {
        float shade = PhShade(wx, wy);
        float h0 = PhElevAt(wx, wy);
        v += shade * 0.6f;
        var c = tones[v < 0.38f ? 0 : v > 0.62f ? 2 : 1];
        c = shade > 0 ? Pix.Mix(c, ReliefLight, shade * 0.5f) : Pix.Mix(c, ReliefShadow, -shade * 0.75f);
        if (g == 's' || h0 < 60) return c;
        // Higher, cooler and mossier; on a volcano's cone, ash grey grading to dark rock near the top.
        if (h0 > 700 && g != 'e') c = Pix.Mix(c, Pal.C("#3f6a4a"), Math.Min(0.35f, (h0 - 700) / 4000f));
        if (g == 'e') c = Pix.Mix(c, Pal.C("#5b5550"), Math.Clamp((h0 - 1200) / 2600f, 0, 0.45f));
        return c;
    }

    // Is this tile a volcano's bare cone? (PhGround reads it.)
    static bool PhRockTile(int x, int y) { PhTerrainField(); return x >= 0 && y >= 0 && x < PhMapData.Cols && y < PhMapData.Rows && phRock[y * PhMapData.Cols + x] != 0; }

    // The named peak you're on (within half its radius), for the status card.
    static string PhPeakAt(float wx, float wy)
    {
        foreach (var p in PhPeaks)
        {
            var (px, py) = PhTileOf(p.Lat, p.Lon);
            float r = p.Rkm / KmPerTile * 0.5f;
            if (Math.Abs(wx / T - px) < r && Math.Abs(wy / T - py) < r && MathF.Sqrt((wx / T - px) * (wx / T - px) + (wy / T - py) * (wy / T - py)) < r) return p.Name;
        }
        return null;
    }

    // Each landmark peak's drawing (tools/blender/phmountains.py): which one, by name.
    static readonly Dictionary<string, string> PhPeakArt = new()
    {
        ["Mayon Volcano"] = "Mayon", ["Mount Apo"] = "Apo", ["Kanlaon Volcano"] = "Kanlaon", ["Mount Pinatubo"] = "Pinatubo", ["Taal Volcano"] = "Taal",
        ["Mount Malindig"] = "Malindig",
        ["Mount Banahaw"] = "Forested", ["Mount Isarog"] = "Forested", ["Mount Matutum"] = "Forested", ["Mount Malindang"] = "Forested",
        ["Mount Ragang"] = "Forested", ["Mount Talinis"] = "Forested", ["Mount Silay"] = "Forested",
        ["Bulusan Volcano"] = "Small", ["Mount Arayat"] = "Small", ["Mount Iriga"] = "Small", ["Mount Makiling"] = "Small", ["Hibok-Hibok"] = "Small",
        ["Mount Iraya"] = "Small", ["Babuyan Claro"] = "Small", ["Biliran Volcano"] = "Small",
        ["Mount Pulag"] = "Massif", ["Mount Amuyao"] = "Massif", ["Mount Halcon"] = "Massif", ["Mount Kitanglad"] = "Massif", ["Mount Madia-as"] = "Massif",
        ["Mount Mantalingajan"] = "Massif", ["Mount Baco"] = "Massif",
        ["Mount Guiting-Guiting"] = "Ridge", ["Cleopatra's Needle"] = "Ridge", ["Mount Victoria"] = "Ridge", ["Mount Baloy"] = "Ridge",
        ["Osmena Peak"] = "Ridge", ["Mount Nacolod"] = "Ridge", ["Mount Hamiguitan"] = "Ridge", ["Mount Data"] = "Ridge",
    };

    static (string[] grid, string[] pal, int ax, int ay, int foot, int summit, int half) PhMountain(string art) => art switch
    {
        "Mayon" => (PhMountainArt.Mayon, PhMountainArt.MayonPalette, PhMountainArt.MayonAnchorX, PhMountainArt.MayonAnchorY, PhMountainArt.MayonFoot, PhMountainArt.MayonSummit, PhMountainArt.MayonHalf),
        "Apo" => (PhMountainArt.Apo, PhMountainArt.ApoPalette, PhMountainArt.ApoAnchorX, PhMountainArt.ApoAnchorY, PhMountainArt.ApoFoot, PhMountainArt.ApoSummit, PhMountainArt.ApoHalf),
        "Kanlaon" => (PhMountainArt.Kanlaon, PhMountainArt.KanlaonPalette, PhMountainArt.KanlaonAnchorX, PhMountainArt.KanlaonAnchorY, PhMountainArt.KanlaonFoot, PhMountainArt.KanlaonSummit, PhMountainArt.KanlaonHalf),
        "Forested" => (PhMountainArt.Forested, PhMountainArt.ForestedPalette, PhMountainArt.ForestedAnchorX, PhMountainArt.ForestedAnchorY, PhMountainArt.ForestedFoot, PhMountainArt.ForestedSummit, PhMountainArt.ForestedHalf),
        "Malindig" => (PhMountainArt.Malindig, PhMountainArt.MalindigPalette, PhMountainArt.MalindigAnchorX, PhMountainArt.MalindigAnchorY, PhMountainArt.MalindigFoot, PhMountainArt.MalindigSummit, PhMountainArt.MalindigHalf),
        "Small" => (PhMountainArt.Small, PhMountainArt.SmallPalette, PhMountainArt.SmallAnchorX, PhMountainArt.SmallAnchorY, PhMountainArt.SmallFoot, PhMountainArt.SmallSummit, PhMountainArt.SmallHalf),
        "Pinatubo" => (PhMountainArt.Pinatubo, PhMountainArt.PinatuboPalette, PhMountainArt.PinatuboAnchorX, PhMountainArt.PinatuboAnchorY, PhMountainArt.PinatuboFoot, PhMountainArt.PinatuboSummit, PhMountainArt.PinatuboHalf),
        "Taal" => (PhMountainArt.Taal, PhMountainArt.TaalPalette, PhMountainArt.TaalAnchorX, PhMountainArt.TaalAnchorY, PhMountainArt.TaalFoot, PhMountainArt.TaalSummit, PhMountainArt.TaalHalf),
        "Massif" => (PhMountainArt.Massif, PhMountainArt.MassifPalette, PhMountainArt.MassifAnchorX, PhMountainArt.MassifAnchorY, PhMountainArt.MassifFoot, PhMountainArt.MassifSummit, PhMountainArt.MassifHalf),
        _ => (PhMountainArt.Ridge, PhMountainArt.RidgePalette, PhMountainArt.RidgeAnchorX, PhMountainArt.RidgeAnchorY, PhMountainArt.RidgeFoot, PhMountainArt.RidgeSummit, PhMountainArt.RidgeHalf),
    };

    // The mountains and volcanoes themselves, drawn over their real places with everything else in depth order, and the
    // smoke over the active ones (Mayon, Taal, Kanlaon, Bulusan, Hibok-Hibok). One you're behind (or on) fades, as the trees
    // do, so you're never lost under it.
    float plumeX = ConeX;
    const string MtnKeys = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVW";
    static readonly Dictionary<string[], Color[]> mtnPal = new(ReferenceEqualityComparer.Instance);   // by the palette itself: their first and last colours repeat
    internal int mountainsDrawn;   // (the autotest reads it)
    // Where each mountain's drawing stands, worked out once: on its peak, or moved a little so its foot is on its own island
    // (Malindig's ran out over the sea), or the smaller drawing if the big one doesn't fit. Towns are laid out round them
    // (PhMountainRects: Kanlaon's drawing covered houses). (peak, drawing, foot's middle in world pixels)
    static List<(PhPeak p, string art, float x, float y)> phMountains;
    static List<(PhPeak p, string art, float x, float y)> PhMountains()
    {
        if (phMountains != null) return phMountains;
        PhTerrainField();
        var list = new List<(PhPeak, string, float, float)>();
        foreach (var p in PhPeaks)
        {
            if (!PhPeakArt.TryGetValue(p.Name, out var first)) continue;
            var (tx, ty) = PhTileOf(p.Lat, p.Lon);
            foreach (var art in new[] { first, "Ridge", "Small" }.Distinct())
            {
                if (art == "Ridge" && first is not ("Massif")) continue;   // a volcano stays a volcano
                var (grid, _, ax, ay, foot, _, half) = PhMountain(art);
                // The land under the foot (an ellipse half wide by foot deep), at a few places near the peak.
                float Land(float cx, float cy)
                {
                    int land = 0, n = 0;
                    for (int j = -4; j <= 4; j++)
                        for (int i = -6; i <= 6; i++)
                        {
                            float ex = i / 6f, ey = j / 4f;
                            if (ex * ex + ey * ey > 1) continue;
                            n++;
                            if (PhKindAt((int)((cx + ex * half) / T), (int)((cy + ey * foot * 2.2f) / T)) == 3) land++;
                        }
                    return land / (float)n;
                }
                (float x, float y, float f) best = (0, 0, -1);
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        float cx = tx * T + dx * half * 0.2f, cy = ty * T + dy * foot * 0.6f;
                        float f = Land(cx, cy) - 0.02f * (Math.Abs(dx) + Math.Abs(dy));
                        if (f > best.f) best = (cx, cy, f);
                    }
                if (best.f < (art == "Taal" ? 0.15f : 0.75f)) continue;   // (Taal's island is small, in its lake)
                list.Add((p, art, best.x, best.y));
                break;
            }
        }
        return phMountains = list;
    }

    // Every mountain drawing's box in world pixels (left, top, right, front of the foot), for the towns to keep clear of.
    static List<(float x0, float y0, float x1, float y1)> phMountainRects;
    static List<(float x0, float y0, float x1, float y1)> PhMountainRects() => phMountainRects ??= PhMountains().Select(m =>
    {
        var (grid, _, ax, ay, foot, _, _) = PhMountain(m.art);
        return (m.x - ax, m.y - ay, m.x - ax + grid[0].Length, m.y + foot);
    }).ToList();

    // A mountain's drawing is solid: you walk round it, not into it (walking in, you phased through the slope). Any of your
    // footprint's corners on a painted pixel of it (above its softened last rows) blocks the step.
    bool PhMountainBlocks(float x, float y)
    {
        foreach (var (p, art, mx, my) in PhMountains())
        {
            var (grid, _, ax, ay, foot, _, half) = PhMountain(art);
            float x0 = mx - ax, y0 = my - ay;
            if (x + 3 < x0 || x - 3 > x0 + grid[0].Length || y < y0 || y - 3 > my + foot - 3) continue;
            foreach (var (cx, cy) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
            {
                int gi = (int)MathF.Floor(cx - x0), gj = (int)MathF.Floor(cy - y0);
                if (gj < 0 || gj >= grid.Length || gj > ay + foot - 4 || gi < 0 || gi >= grid[gj].Length || grid[gj][gi] == '.') continue;
                if (ShapePx((int)cx, (int)cy) is '~' or 'w') continue;   // (not drawn over the sea)
                return true;
            }
        }
        return false;
    }

    // Whether a tile is under a mountain's drawing (its trees would stand on the slope's face): PhInitTiles clears them.
    static bool PhUnderMountain(int x, int y)
    {
        foreach (var (p, art, mx, my) in PhMountains())
        {
            var (grid, _, ax, ay, foot, _, half) = PhMountain(art);
            float px = x * T + 5, py = y * T + 9;
            // (and a few rows in front of its foot, so its base isn't lost behind the treetops)
            if (py > my + foot && py < my + foot + 34 && Math.Abs(px - mx) < half * 0.75f) return true;
            if (px < mx - half || px > mx + half || py < my - ay + 4 || py > my + foot) continue;
            // inside the drawing's shape (its column has paint at this row)
            int gi = (int)(px - (mx - ax)), gj = (int)(py - (my - ay));
            if (gi >= 0 && gj >= 0 && gj < grid.Length && gi < grid[gj].Length && grid[gj][gi] != '.') return true;
        }
        return false;
    }

    void AddPhPlumes(List<(float y, Action draw)> list, Func<float, float, bool> visible)
    {
        mountainsDrawn = 0;
        foreach (var (p, art, sx, sy) in PhMountains())
        {
            {
                var (grid, pal, ax, ay, foot, summit, half) = PhMountain(art);
                float x0 = sx - ax, y0 = sy - ay, x1 = x0 + grid[0].Length, y1 = y0 + grid.Length;
                if (x1 > camX && x0 < camX + W && y1 > camY && y0 < camY + H)
                {
                    var peakArt = art;
                    mountainsDrawn++;
                    bool behind = player.X > x0 + 8 && player.X < x1 - 8 && player.Y > y0 + 10 && player.Y < sy + foot - 2;
                    list.Add((sy + foot, () => BlitMountain(grid, pal, (int)x0, (int)y0, behind ? (byte)110 : (byte)255, (int)sy, foot)));
                }
                if (p.Active && x1 > camX - 60 && x0 < camX + W + 60 && y1 > camY - 120 && y0 < camY + H)
                {
                    float top = sy - summit;
                    list.Add((sy + foot + 1, () => { plumeX = sx; DrawPlume(top, p.M > 2000 ? 1 : 0, false); plumeX = ConeX; }));
                }
            }
        }
    }

    // The drawing is a mountain on flat ground; the real coast is never quite the circle of its foot. So nothing is drawn where
    // there's only sea under it: a column whose whole foot (the ground from the back of the foot to the front) is water goes,
    // and so does any pixel of the foot itself that lands on the water (Malindig ran out over the sea).
    void BlitMountain(string[] grid, string[] pal, int x0, int y0, byte alpha, int baseY, int foot)
    {
        if (!mtnPal.TryGetValue(pal, out var cols))
            mtnPal[pal] = cols = pal.Select(Pal.C).ToArray();
        bool Land(int x, int y) => ShapePx(x, y) is not ('~' or 'w');   // (a crater lake is still the mountain's)
        var colLand = new bool[grid[0].Length];
        for (int i = 0; i < colLand.Length; i++)
            for (int y = baseY - foot; y <= baseY + foot && !colLand[i]; y += 2) colLand[i] = Land(x0 + i, y);
        for (int j = 0; j < grid.Length; j++)
        {
            int y = y0 + j;
            if (y < camY - 1 || y > camY + H) continue;
            string row = grid[j];
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '.') continue;
                int x = x0 + i;
                if (x < camX - 1 || x > camX + W || !colLand[i]) continue;
                if (y >= baseY - foot && !Land(x, y)) continue;
                // The foot fades into the ground over its last rows (a hard line read as a cardboard cut-out).
                int up = baseY + foot - y;
                if (up < 6 && Pix.Hash(x, y, 771) * 6 > up) continue;
                int k = MtnKeys.IndexOf(ch);
                if (k < 0 || k >= cols.Length) continue;
                var c = cols[k];
                pix.Rect(x, y, 1, 1, alpha == 255 ? c : new Color(c.R, c.G, c.B, alpha));
            }
        }
    }
}
