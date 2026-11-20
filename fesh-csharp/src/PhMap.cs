namespace Fesh;

// The Philippines map (1.48): a new game can be played on the real islands instead of the original world, true to scale
// (PhMapData: Natural Earth's coastlines at 1.1 km a tile, 968 x 1684 tiles). The world is the whole country, tile for tile
// (COLS x ROWS become its size; positions, trees and builds are all in the country's own coordinates), but the baked pixel
// layers (the rounded coast, the ground, the rich sea) only cover a window of PhWin x PhWin tiles round you (Scenery.bufX0):
// baking the whole country would take some 160 million pixels. When you come within PhEdge tiles of the window's edge it moves
// (PhMoveWindow): what's already baked is shifted over and the new edge is baked a little at a time (PhBake.cs).
// Nothing of the original world's stories, places or people exists on this map (PhWorld gates them off); its own towns,
// fishing spots, bosses and stories come in later steps.
partial class Game
{
    bool PhWorld => state?.world == "ph";
    string newWorld = "";   // the map the creator has picked for a new game ("" the original world, "ph" the Philippines)
    const int PhWin = 192, PhEdge = 48, PhSnap = 8;

    // The whole country, decoded once: 0 sea, 1 land, 2 lake.
    static byte[] phGrid;
    static byte[] PhGrid()
    {
        if (phGrid != null) return phGrid;
        var g = new byte[PhMapData.Cols * PhMapData.Rows];
        for (int y = 0; y < PhMapData.Rows; y++)
        {
            string row = PhMapData.Land[y];
            int x = 0, n = 0;
            foreach (char c in row)
            {
                if (c >= '0' && c <= '9') { n = n * 10 + (c - '0'); continue; }
                byte v = c == 'l' ? (byte)1 : c == 'k' ? (byte)2 : (byte)0;
                for (int i = 0; i < n && x < PhMapData.Cols; i++, x++) g[y * PhMapData.Cols + x] = v;
                n = 0;
            }
        }
        // What Natural Earth's 1:10m land leaves out: Taal Volcano's island in Taal Lake, and Pinatubo's crater lake.
        void Disc(double lat, double lon, double km, byte from, byte to)
        {
            double cx = (lon - PhMapData.Lon0) * PhMapData.Kx, cy = (PhMapData.Lat1 - lat) * PhMapData.Ky, r = km / PhMapData.KmPerTile;
            for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                    if (x >= 0 && y >= 0 && x < PhMapData.Cols && y < PhMapData.Rows && (x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy) <= r * r
                        && g[y * PhMapData.Cols + x] == from) g[y * PhMapData.Cols + x] = to;
        }
        Disc(14.002, 120.995, 2.6, 2, 1);
        Disc(15.143, 120.35, 1.3, 1, 2);
        return phGrid = g;
    }
    static byte PhCell(int gx, int gy) => gx < 0 || gy < 0 || gx >= PhMapData.Cols || gy >= PhMapData.Rows ? (byte)0 : PhGrid()[gy * PhMapData.Cols + gx];

    // Where a real place is in the world's pixels, and back.
    static (float x, float y) PhPlace(double lat, double lon) =>
        ((float)((lon - PhMapData.Lon0) * PhMapData.Kx * T), (float)((PhMapData.Lat1 - lat) * PhMapData.Ky * T));
    static (double lat, double lon) PhLatLon(float wx, float wy) =>
        (PhMapData.Lat1 - wy / T / PhMapData.Ky, PhMapData.Lon0 + wx / T / PhMapData.Kx);

    // The window's corner (in tiles) that puts a place (in tiles) in its middle, kept on the map and on the grid.
    static (int x0, int y0) PhWindowFor(double tx, double ty)
    {
        int x0 = (int)Math.Round((tx - PhWin / 2.0) / PhSnap) * PhSnap, y0 = (int)Math.Round((ty - PhWin / 2.0) / PhSnap) * PhSnap;
        return (Math.Clamp(x0, 0, Math.Max(0, PhMapData.Cols - PhWin)), Math.Clamp(y0, 0, Math.Max(0, PhMapData.Rows - PhWin)));
    }

    // The world arrays for a world of another size, and the baked layers' window (the whole world on the original map).
    void ResizeWorld(int cols, int rows, int bw, int bh)
    {
        bool same = COLS == cols && ROWS == rows && worldMap.GetLength(1) == cols && worldMap.GetLength(0) == rows && bufW == bw && bufH == bh;
        bufX0 = bufY0 = 0;
        if (same) { worldBase.CamX = worldBase.CamY = 0; return; }
        COLS = cols; ROWS = rows; bufW = bw; bufH = bh;
        worldMap = new char[rows, cols];
        if (scene == "world") map = worldMap;
        biome = new byte[rows, cols];
        depth = new float[rows, cols];
        groundCache = new char[rows, cols];
        landDist = new float[rows, cols];
        shape = new char[bw * bh];
        worldBase = new Pix(bw, bh);
        if (scene == "world") basePix = worldBase;
        relief = null; terrainAt = null;
        phTilesReady = false;
        ResetWorldCaches();
    }

    // Everything worked out from the world's tiles once and kept: made again after the world changes under it.
    void ResetWorldCaches()
    {
        worldRich = null; tintField = null; openSea = null; openSeaFor = null; shoreCells = null; seaField = null; bagaClosedTiles = null;
        mapTexDirty = true;
    }

    // Smooth value noise in 0..1 (looks only, the same everywhere for the same place).
    static double PhNoise(double x, double y)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        double a = Pix.Hash(ix, iy, 917), b = Pix.Hash(ix + 1, iy, 917), c = Pix.Hash(ix, iy + 1, 917), d = Pix.Hash(ix + 1, iy + 1, 917);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    // BuildMap on this map: the country's tiles (once), and the window round where you are, baked whole.
    void BuildPh()
    {
        ResizeWorld(PhMapData.Cols, PhMapData.Rows, PhWin * T, PhWin * T);
        PhTerrainField();   // the mountains (PhTerrain.cs)
        PhSpots();   // registers this map's fishing spots by id
        if (!phTilesReady) PhInitTiles();
        var (x0, y0) = PhWindowFor(player.X / T, player.Y / T);
        PhBakeWindow(x0, y0);
    }

    // Close to the window's edge (and not already at the country's own edge), the window moves so you're in its middle again.
    void PhRecenter()
    {
        if (!PhWorld || scene != "world") return;
        float tx = player.X / T - bufX0 / T, ty = player.Y / T - bufY0 / T;
        int wx0 = bufX0 / T, wy0 = bufY0 / T;
        bool nearEdge = tx < PhEdge && wx0 > 0 || ty < PhEdge && wy0 > 0
            || tx > PhWin - PhEdge && wx0 < PhMapData.Cols - PhWin || ty > PhWin - PhEdge && wy0 < PhMapData.Rows - PhWin;
        if (!nearEdge) return;
        var (nx0, ny0) = PhWindowFor(player.X / T, player.Y / T);
        if (nx0 != wx0 || ny0 != wy0) PhMoveWindow(nx0, ny0);
    }

    // The new game's start on the Philippines map: Marinduque, on the beach at Boac on its west coast.
    const double StartLat = 13.447, StartLon = 121.835;
    // Before the world is built: the map, and you at Boac (the window is cut round you).
    void StartPh()
    {
        state.world = "ph";
        (state.px, state.py) = PhPlace(StartLat, StartLon);
        state.tour = TourDone;   // First steps are about Saltmere's people: not on this map
    }

    // Once it's built: the nearest dry ground to Boac, and the banca on the water beside it.
    void PlacePhStart()
    {
        var (sx, sy) = PhPlace(StartLat, StartLon);
        var spot = PhStandNear(sx, sy);
        state.px = spot.x; state.py = spot.y;
        state.inv["boat"] = 1;
        var water = PhWaterNear(spot.x, spot.y);
        state.boatX = water.x; state.boatY = water.y; state.boatAt = "";
    }

    (float x, float y) PhStandNear(float x, float y)
    {
        for (int r = 0; r < 600; r += 3)
            for (int a = 0; a < 32; a++)
            {
                float px = x + MathF.Cos(a * MathF.Tau / 32) * r, py = y + MathF.Sin(a * MathF.Tau / 32) * r;
                if (CanStand(px, py)) return (px, py);
                if (r == 0) break;
            }
        return (x, y);
    }

    (float x, float y) PhWaterNear(float x, float y)
    {
        for (int r = 6; r < 300; r += 2)
            for (int a = 0; a < 32; a++)
            {
                float px = x + MathF.Cos(a * MathF.Tau / 32) * r, py = y + MathF.Sin(a * MathF.Tau / 32) * r;
                if (BoatCanStand(px, py)) return (px, py);
            }
        return (x, y);
    }

    // Straight to a real place (tests now; a voyage or fast travel later): the window round it, and the nearest dry ground.
    void PhTeleport(double lat, double lon, bool land = true)
    {
        var (px, py) = PhPlace(lat, lon);
        player.X = px; player.Y = py;
        var (x0, y0) = PhWindowFor(px / T, py / T);
        PhMoveWindow(x0, y0);
        while (phQueue.Count > 0) PhBakeStep(int.MaxValue);
        (player.X, player.Y) = land ? PhStandNear(px, py) : (px, py);
        state.px = player.X; state.py = player.Y;
    }

    // The name of the land or sea you're on, for the arrival banner and the status card: the island under you (PhIslands.cs),
    // or the sea by the nearest of the seas' middles.
    static string PhPlaceName(float wx, float wy, bool onLand)
    {
        if (onLand && PhIslandAt(wx, wy, 8) is string isle) return isle;   // (out on a pier too)
        var (lat, lon) = PhLatLon(wx, wy);
        return onLand ? "the Philippines" : PhSeaAt(lat, lon);
    }

    string phLastName;

    // A short banner as you come ashore on a different island (or sail into a different sea), like the original world's.
    void PhAnnounce()
    {
        bool onLand = !Aboard && !Swimming && !InWater;   // (on foot on a beach pixel whose tile is still the sea counts as land)
        if (!onLand && !Aboard) return;
        string name = PhPlaceName(player.X, player.Y, onLand);
        if (name == phLastName) return;
        bool first = phLastName == null;
        phLastName = name;
        if (!first) Toast(onLand ? $"{name}" : $"Sailing the {name}", 3);
    }

    // The chart of the whole country (the map panel on this map): a tile a pixel, sea in two depths, sandy coasts, lakes,
    // with the islands' and seas' names, the window you're in, and you.
    static Raylib_cs.Texture2D phChartTex;
    static Raylib_cs.Texture2D PhChartTexture()
    {
        if (phChartTex.Id != 0) return phChartTex;
        PhTerrainField();
        int w = PhMapData.Cols, h = PhMapData.Rows;
        var px = new Raylib_cs.Color[w * h];
        var deep = Pal.C("#2c5f86"); var shallow = Pal.C("#4f8fb5"); var land = Pal.C("#9fbf7a"); var coast = Pal.C("#e3cf98"); var lake = Pal.C("#6fb0c8");
        var paperish = Pal.C("#c9b48a");
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte c = PhCell(x, y);
                bool near = false;
                for (int dy = -2; dy <= 2 && !near; dy++) for (int dx = -2; dx <= 2; dx++) if (PhCell(x + dx, y + dy) != 0 && c == 0) { near = true; break; }
                bool shore = c == 1 && (PhCell(x + 1, y) == 0 || PhCell(x - 1, y) == 0 || PhCell(x, y + 1) == 0 || PhCell(x, y - 1) == 0);
                var col = c == 2 ? lake : c == 1 ? (shore ? coast : land) : near ? shallow : deep;
                if (c == 1 && !shore)
                {
                    // Mountains: darker greens going up, the bare volcanoes grey-brown, and the slopes lit from the north-west.
                    float hm = PhElevTile(x, y), sl = (PhElevTile(x - 1, y - 1) - PhElevTile(x + 1, y + 1)) / 600f;
                    col = Pix.Mix(col, Pal.C("#5c8a4a"), Math.Min(0.6f, hm / 2000f));
                    if (phRock[y * w + x] != 0) col = Pix.Mix(col, Pal.C("#8a7d6a"), 0.7f);
                    col = sl > 0 ? Pix.Mix(col, Pal.C("#fff3c4"), Math.Min(0.45f, sl)) : Pix.Mix(col, Pal.C("#20384a"), Math.Min(0.5f, -sl));
                }
                float g = ((float)Pix.Hash(x, y, 777) - 0.5f) * 0.06f;
                col = Pix.Mix(col, g > 0 ? Raylib_cs.Color.White : paperish, Math.Abs(g));
                px[y * w + x] = col;
            }
        return phChartTex = Gfx.ToTexture(px, w, h, Raylib_cs.TextureFilter.Bilinear);
    }

    // The chart zooms (the wheel, or + and -, about the pointer) and pans (drag it); a click that doesn't drag drops a pin,
    // and a click on the pin lifts it. Names are placed biggest first and skipped where they'd overlap one already drawn,
    // smaller islands, seas and the towns coming in as you zoom in, and the fishing spots closer still.
    float phZoom = 1, phCx = -1, phCy = -1;     // the zoom, and the chart's middle in tiles
    System.Numerics.Vector2? phDragFrom;
    System.Numerics.Vector2 phDownAt;
    bool phDragged;
    bool phChartPoint;                           // the texture is drawn with point filtering (zoomed in)
    const float PhZoomMax = 24;

    // (name, latitude, longitude, sea, smallest zoom it's named at, font size)
    static readonly (string name, double lat, double lon, bool sea, float zoom, float fs)[] PhChartLabels =
    {
        ("Luzon", 16.3, 121.2, false, 1, 20), ("Mindanao", 7.6, 124.8, false, 1, 20), ("Palawan", 9.55, 118.45, false, 1, 15),
        ("Mindoro", 12.85, 121.05, false, 1, 15), ("Panay", 11.15, 122.45, false, 1, 15), ("Negros", 10.1, 122.95, false, 1, 15),
        ("Cebu", 10.35, 123.85, false, 1, 15), ("Bohol", 9.85, 124.2, false, 1, 15), ("Leyte", 10.85, 124.85, false, 1, 15),
        ("Samar", 11.9, 125.0, false, 1, 15), ("Masbate", 12.2, 123.5, false, 1, 15), ("Marinduque", 13.4, 121.97, false, 1, 15),
        ("West Philippine Sea", 15.5, 118.6, true, 1, 14), ("Philippine Sea", 14.0, 125.8, true, 1, 14), ("Sulu Sea", 8.7, 120.4, true, 1, 14),
        ("Celebes Sea", 5.3, 123.2, true, 1, 14), ("Sibuyan Sea", 12.65, 122.55, true, 1, 14), ("Visayan Sea", 11.45, 123.65, true, 1.5f, 14),
        ("Luzon Strait", 20.2, 121.0, true, 1, 14),
        ("Catanduanes", 13.8, 124.25, false, 2, 14), ("Busuanga", 12.1, 120.0, false, 2, 14), ("Romblon", 12.55, 122.27, false, 2.5f, 13),
        ("Tablas", 12.4, 122.05, false, 2.5f, 13), ("Sibuyan", 12.4, 122.55, false, 2.5f, 13), ("Siquijor", 9.2, 123.55, false, 2, 13),
        ("Camiguin", 9.17, 124.72, false, 2.5f, 13), ("Basilan", 6.55, 121.95, false, 2, 13), ("Jolo", 6.0, 121.05, false, 2, 13),
        ("Tawi-Tawi", 5.2, 120.05, false, 2, 13), ("Guimaras", 10.6, 122.62, false, 2.5f, 13), ("Biliran", 11.58, 124.47, false, 3, 13),
        ("Polillo", 14.85, 121.95, false, 2.5f, 13), ("Burias", 12.95, 123.25, false, 2.5f, 13), ("Ticao", 12.55, 123.7, false, 3, 13),
        ("Batanes", 20.45, 121.97, false, 2, 13), ("Dinagat", 10.1, 125.6, false, 2.5f, 13), ("Siargao", 9.85, 126.05, false, 2.5f, 13),
        ("Samal", 7.08, 125.73, false, 3, 13), ("Lubang", 13.8, 120.1, false, 3, 13), ("Culion", 11.85, 120.0, false, 3, 13),
        ("Sabah (Malaysia)", 5.6, 117.6, false, 1.5f, 13),
        ("Bohol Sea", 9.1, 124.3, true, 2, 13), ("Tablas Strait", 12.95, 121.75, true, 2.5f, 13), ("Moro Gulf", 7.0, 123.4, true, 2, 13),
        ("Samar Sea", 12.0, 124.3, true, 2.5f, 13), ("Camotes Sea", 10.5, 124.3, true, 3, 13), ("Mindoro Strait", 12.3, 120.5, true, 2.5f, 13),
        ("Verde Island Passage", 13.62, 121.1, true, 3, 13), ("Manila Bay", 14.55, 120.75, true, 3, 13), ("Lingayen Gulf", 16.3, 120.25, true, 3, 13),
        ("Ticao Pass", 12.7, 123.85, true, 3.5f, 13), ("Davao Gulf", 6.8, 125.7, true, 2.5f, 13), ("Leyte Gulf", 10.9, 125.4, true, 2.5f, 13),
        ("Tayabas Bay", 13.6, 121.8, true, 3.5f, 13), ("Ragay Gulf", 13.6, 122.7, true, 3.5f, 13), ("Lamon Bay", 14.3, 122.0, true, 3, 13),
    };

    void OpenPhChart()
    {
        // Opens on you, zoomed in enough to see the island you're on.
        phZoom = 4;
        phCx = player.X / T; phCy = player.Y / T;
        phDragFrom = null;
    }

    void DrawPhMap()
    {
        var (x, y, w, h) = PanelFrame("Chart of the Philippines", "Scroll or press + and - to zoom, drag to move. Click to drop a pin; click the pin to lift it.", 1180, 680);
        if (PanelClose()) { ClosePanels(); return; }
        var tex = PhChartTexture();
        int C = PhMapData.Cols, R = PhMapData.Rows;
        float mx = x + 24, my = y + 96, mw = w - 344, mh = h - 112;
        float s0 = Math.Min(mh / R, mw / C);
        if (phCx < 0) OpenPhChart();
        void Clamp(float sc)
        {
            float vw = mw / sc, vh = mh / sc;
            phCx = vw >= C ? C / 2f : Math.Clamp(phCx, vw / 2, C - vw / 2);
            phCy = vh >= R ? R / 2f : Math.Clamp(phCy, vh / 2, R - vh / 2);
        }
        void ZoomTo(float z, float px, float py)
        {
            float s1 = s0 * phZoom, s2 = s0 * z;
            float tmx = phCx + (px - mx - mw / 2) / s1, tmy = phCy + (py - my - mh / 2) / s1;
            phZoom = z;
            phCx = tmx - (px - mx - mw / 2) / s2; phCy = tmy - (py - my - mh / 2) / s2;
            Clamp(s2);
        }
        // Input first, so this frame draws where it now is.
        bool over = Gfx.Hover(mx, my, mw, mh);
        Gfx.Block(mx, my, mw, mh);
        float wheel = over ? Raylib_cs.Raylib.GetMouseWheelMove() : 0;
        if (wheel != 0) ZoomTo(Math.Clamp(phZoom * MathF.Pow(1.3f, wheel), 1, PhZoomMax), Gfx.Mouse.X, Gfx.Mouse.Y);
        if (Inp.Pressed(Raylib_cs.KeyboardKey.Equal) || Inp.Pressed(Raylib_cs.KeyboardKey.KpAdd)) ZoomTo(Math.Min(PhZoomMax, phZoom * 1.5f), mx + mw / 2, my + mh / 2);
        if (Inp.Pressed(Raylib_cs.KeyboardKey.Minus) || Inp.Pressed(Raylib_cs.KeyboardKey.KpSubtract)) ZoomTo(Math.Max(1, phZoom / 1.5f), mx + mw / 2, my + mh / 2);
        float s = s0 * phZoom;
        Clamp(s);
        if ((Gfx.Pressed || Gfx.Down) && over && phDragFrom == null) { phDragFrom = Gfx.Mouse; phDownAt = Gfx.Mouse; phDragged = false; }
        bool clicked = false;
        if (phDragFrom is { } from)
        {
            if (Gfx.Down)
            {
                var m = Gfx.Mouse;
                if (Dist(m.X, m.Y, phDownAt.X, phDownAt.Y) > 5) phDragged = true;
                if (phDragged) { phCx -= (m.X - from.X) / s; phCy -= (m.Y - from.Y) / s; Clamp(s); }
                phDragFrom = m;
            }
            else { clicked = !phDragged; phDragFrom = null; }
        }
        float ox = mx + mw / 2 - phCx * s, oy = my + mh / 2 - phCy * s;   // where tile (0, 0) is on screen
        (float, float) Scr(double lat, double lon) => (ox + (float)((lon - PhMapData.Lon0) * PhMapData.Kx) * s, oy + (float)((PhMapData.Lat1 - lat) * PhMapData.Ky) * s);
        (float, float) ScrW(float wx, float wy) => (ox + wx / T * s, oy + wy / T * s);
        if (clicked)
        {
            float twx = (phDownAt.X - ox) / s * T, twy = (phDownAt.Y - oy) / s * T;
            if (HasPin && Dist(ScrW(state.pinX, state.pinY).Item1, ScrW(state.pinX, state.pinY).Item2, phDownAt.X, phDownAt.Y) < 12) { state.pinX = state.pinY = 0; Sfx.Play("ui"); }
            else if (twx > 0 && twy > 0 && twx < C * T && twy < R * T) { state.pinX = twx; state.pinY = twy; Sfx.Play("blip"); }
        }
        bool point = phZoom >= 3;
        if (point != phChartPoint) { phChartPoint = point; Raylib_cs.Raylib.SetTextureFilter(tex, point ? Raylib_cs.TextureFilter.Point : Raylib_cs.TextureFilter.Bilinear); }
        Gfx.Rect(mx, my, mw, mh, Pal.C("#2c5f86"));
        var clip = Gfx.S(mx, my, mw, mh);
        Raylib_cs.Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)MathF.Ceiling(clip.Width), (int)MathF.Ceiling(clip.Height));
        Raylib_cs.Raylib.DrawTexturePro(tex, new Raylib_cs.Rectangle(0, 0, C, R), Gfx.S(ox, oy, C * s, R * s), System.Numerics.Vector2.Zero, 0, Raylib_cs.Color.White);
        // The window (the part of the country baked round you).
        var gold = Pal.C("#f3c25b");
        {
            float wx0 = ox + bufX0 / T * s, wy0 = oy + bufY0 / T * s, ww = PhWin * s;
            Gfx.Rect(wx0, wy0, ww, 1.5f, gold); Gfx.Rect(wx0, wy0 + ww, ww, 1.5f, gold); Gfx.Rect(wx0, wy0, 1.5f, ww, gold); Gfx.Rect(wx0 + ww, wy0, 1.5f, ww + 1.5f, gold);
        }
        var placed = new List<(float x, float y, float w, float h)>();
        bool Free(float lx, float ly, float lw, float lh)
        {
            if (lx < mx || ly < my || lx + lw > mx + mw || ly + lh > my + mh) return false;
            foreach (var r in placed) if (lx < r.x + r.w && lx + lw > r.x && ly < r.y + r.h && ly + lh > r.y) return false;
            return true;
        }
        void Name(string text, float cx, float cy, float fs, Raylib_cs.Color col, FontKind font = FontKind.Ui600)
        {
            float tw = Gfx.Measure(text, font, fs), lx = cx - tw / 2, ly = cy - fs / 2;
            if (!Free(lx - 2, ly - 1, tw + 4, fs + 2)) return;
            placed.Add((lx - 2, ly - 1, tw + 4, fs + 2));
            Gfx.Text(text, lx + 1, ly + 1, font, fs, Pal.Rgba(0, 0, 0, 0.35f));
            Gfx.Text(text, lx, ly, font, fs, col);
        }
        // You and your pin go first, so nothing is written over them.
        var (ux, uy) = ScrW(player.X, player.Y);
        placed.Add((ux - 8, uy - 8, 16, 16));
        if (HasPin) { var (px, py) = ScrW(state.pinX, state.pinY); placed.Add((px - 7, py - 22, 14, 24)); }
        // Towns: a dot from 2x, the name from 3x (the ones you've been to in bold).
        state.phVisited ??= new();
        if (phZoom >= 2)
            foreach (var t in PhTowns())
            {
                var (tx, ty) = (ox + (t.Cx + 0.5f) * s, oy + (t.Cy + 0.5f) * s);
                bool been = state.phVisited.Contains(t.Def.Id);
                Gfx.Circle(tx, ty, 4.5f, Pal.Ink);
                Gfx.Circle(tx, ty, 3, been ? Pal.C("#f3c25b") : Pal.C("#f1e6c8"));
                placed.Add((tx - 5, ty - 5, 10, 10));
            }
        // Fishing spots from 6x, named from 9x.
        if (phZoom >= 6)
            foreach (var p in PhSpots())
            {
                if (p.Spot.Scene != "ph") continue;
                var (sx, sy) = ScrW(p.Spot.X, p.Spot.Y);
                if (sx < mx || sy < my || sx > mx + mw || sy > my + mh) continue;
                Gfx.Circle(sx, sy, 4, Pal.C("#1b3a4a"));
                Gfx.Circle(sx, sy, 2.5f, Pal.C("#7fd3e6"));
            }
        foreach (var l in PhChartLabels.Where(l => !l.sea && l.fs >= 15))
            if (phZoom >= l.zoom) { var (lx, ly) = Scr(l.lat, l.lon); Name(l.name, lx, ly, l.fs + (phZoom >= 3 ? 2 : 0), Pal.PaperInk); }
        if (phZoom >= 3)
            foreach (var t in PhTowns().OrderByDescending(t => state.phVisited.Contains(t.Def.Id)))
            {
                var (tx, ty) = (ox + (t.Cx + 0.5f) * s, oy + (t.Cy + 0.5f) * s);
                Name(t.Def.Name, tx, ty - 13, 13, state.phVisited.Contains(t.Def.Id) ? Pal.Ink : Pal.C("#4a3a26"), state.phVisited.Contains(t.Def.Id) ? FontKind.Ui700 : FontKind.Ui500);
            }
        foreach (var l in PhChartLabels.Where(l => l.sea || l.fs < 15))
            if (phZoom >= l.zoom) { var (lx, ly) = Scr(l.lat, l.lon); Name(l.name, lx, ly, l.fs, l.sea ? Pal.C("#d8ecf6") : Pal.PaperInk); }
        if (phZoom >= 9)
            foreach (var p in PhSpots())
            {
                if (p.Spot.Scene != "ph") continue;
                var (sx, sy) = ScrW(p.Spot.X, p.Spot.Y);
                Name(p.Spot.Label, sx, sy + 11, 12, Pal.C("#e8f6fa"), FontKind.Ui500);
            }
        // The pin, and you.
        if (HasPin)
        {
            var (px, py) = ScrW(state.pinX, state.pinY);
            Gfx.Line(px, py, px, py - 16, 2.5f, Pal.Ink);
            Gfx.Circle(px, py - 18, 6, Pal.Ink); Gfx.Circle(px, py - 18, 4.5f, Pal.C("#e04b3a"));
        }
        Gfx.Circle(ux, uy, 7, Pal.Ink); Gfx.Circle(ux, uy, 5, Pal.C("#e04b3a"));
        if (Aboard) Gfx.Circle(ux, uy, 2, Pal.C("#f1e6c8"));
        Raylib_cs.Raylib.EndScissorMode();
        Gfx.Rect(mx - 2, my - 2, mw + 4, 2, Pal.Ink); Gfx.Rect(mx - 2, my + mh, mw + 4, 2, Pal.Ink);
        Gfx.Rect(mx - 2, my, 2, mh, Pal.Ink); Gfx.Rect(mx + mw, my, 2, mh, Pal.Ink);
#if DEBUG
        Gfx.Seen["phchart"] = new Raylib_cs.Rectangle(mx, my, mw, mh);
#endif
        // The column beside it: where you are, the zoom buttons, the key.
        float kx = mx + mw + 24, ky = my, kw = x + w - 24 - kx;
        var (lat, lon) = PhLatLon(player.X, player.Y);
        Gfx.Text("You are", kx, ky, FontKind.Ui700, 20, Pal.PaperInk);
        Gfx.Text(Gfx.Ellipsize(AreaName(), FontKind.Ui600, 19, kw), kx, ky + 28, FontKind.Ui600, 19, Pal.PaperInk);
        Gfx.Text($"{lat:0.00} N, {lon:0.00} E", kx, ky + 54, FontKind.Note, 17, Muted);
        float by = ky + 92, bw = (kw - 10) / 2;
        if (Button("+", kx, by, bw, 40, FontKind.Ui700, 24, Pal.Sand, Pal.Ink, 3)) ZoomTo(Math.Min(PhZoomMax, phZoom * 1.5f), mx + mw / 2, my + mh / 2);
        if (Button("-", kx + bw + 10, by, bw, 40, FontKind.Ui700, 24, Pal.Sand, Pal.Ink, 3)) ZoomTo(Math.Max(1, phZoom / 1.5f), mx + mw / 2, my + mh / 2);
        if (Button("Find me", kx, by + 50, bw, 40, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3)) { phCx = player.X / T; phCy = player.Y / T; if (phZoom < 4) phZoom = 4; Clamp(s0 * phZoom); }
        if (Button("Whole map", kx + bw + 10, by + 50, bw, 40, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3)) { phZoom = 1; Clamp(s0); }
        Gfx.Text($"Zoom {phZoom:0.#}x", kx, by + 100, FontKind.Note, 16, Muted);
        float ly2 = by + 136;
        void Key(Action draw, string text) { draw(); Gfx.Text(text, kx + 24, ly2 - 9, FontKind.Note, 16, Muted); ly2 += 26; }
        Key(() => { Gfx.Circle(kx + 8, ly2, 6, Pal.Ink); Gfx.Circle(kx + 8, ly2, 4.5f, Pal.C("#e04b3a")); }, "You");
        Key(() => { Gfx.Circle(kx + 8, ly2, 4.5f, Pal.Ink); Gfx.Circle(kx + 8, ly2, 3, Pal.C("#f3c25b")); }, "A town you've visited");
        Key(() => { Gfx.Circle(kx + 8, ly2, 4.5f, Pal.Ink); Gfx.Circle(kx + 8, ly2, 3, Pal.C("#f1e6c8")); }, "A town not yet visited");
        Key(() => { Gfx.Circle(kx + 8, ly2, 4, Pal.C("#1b3a4a")); Gfx.Circle(kx + 8, ly2, 2.5f, Pal.C("#7fd3e6")); }, "A fishing spot (zoom in)");
        Key(() => { Gfx.Rect(kx + 1, ly2 - 7, 14, 14, gold); Gfx.Rect(kx + 3, ly2 - 5, 10, 10, Pal.C("#f1e6c8")); }, "The land drawn round you");
        ly2 += 8;
        int seen = PhTowns().Count(t => state.phVisited.Contains(t.Def.Id));
        Gfx.Text($"Towns visited: {seen} of {PhTowns().Count}", kx, ly2, FontKind.Ui600, 17, Pal.PaperInk);
        ly2 += 30;
        foreach (var line in Gfx.Wrap("Sail your banca anywhere: the land is drawn round you as you go. Every town has a market and an inn.", FontKind.Note, 16, kw))
        { Gfx.Text(line, kx, ly2, FontKind.Note, 16, Muted); ly2 += 21; }
    }
}
