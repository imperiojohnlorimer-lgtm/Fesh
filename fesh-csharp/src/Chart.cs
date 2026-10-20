using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The map panel's chart: it opens on whichever chart you're in (with an arrow at the edge when you're on the other one);
// islands are drawn in properly once you've set foot on them (until then, a rough outline); labels nudge apart instead
// of piling up; hovering a dot tells you more; and a click drops a pin that a compass at the edge of the view points to.
partial class Game
{
    string mapHover;   // the dot the mouse is over on the chart (its tooltip's title), for the autotest

    // Region: an island's name label that opens its page in the island guide when clicked (Atlas.cs).
    sealed record MapMark(string Label, float X, float Y, Color Dot, Color Text, float Size, bool HasDot, List<string> Tip, int Prio, string Region = null, int Glyph = 0);

    /* ---------- Charting ---------- */
    // An island: one of the five original biomes, one of Amihan's four islands by name, or a Habagat island (Asinan,
    // Parola, or an islet of Daang Pulo by number).
    string RegionAt(int tx, int ty)
    {
        byte b = BiomeAt(tx, ty);
        if (b < 5) return Data.Biomes[b].Id;
        if (b == 6) return HabagatRegion(tx, ty);
        if (b == 7) return MarilayaRegion(tx, ty);
        var isle = AmihanIslands.OrderBy(i => (i.cx - tx) * (i.cx - tx) / (i.rx * i.rx) + (i.cy - ty) * (i.cy - ty) / (i.ry * i.ry)).First();
        return "amihan:" + isle.name;
    }

    string RegionOf(float wx, float wy) => RegionAt((int)MathF.Floor(wx / T), (int)MathF.Floor(wy / T));
    // A Habagat spot can sit in water north of HabagatTop (the Parola pier's end), so it's asked of Habagat directly.
    string SpotRegion(Spot s) => s.Biome == "habagat" ? HabagatRegion((int)MathF.Floor(s.X / T), (int)MathF.Floor(s.Y / T))
        : s.Biome == "amihan" ? RegionOf(s.X, s.Y) : s.Biome;
    bool Charted(string region) => state.charted == null || state.charted.Contains(region);

    // Standing on an island's land charts it.
    void ChartHere()
    {
        if (scene != "world" || state.charted == null) return;
        int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
        // The sanctuary's watch platform stands in open sea: it isn't part of any island.
        if (WaterTile(TileAt(tx, ty)) || InSanctuary(player.X, player.Y)) return;
        string r = RegionAt(tx, ty);
        if (state.charted.Contains(r)) return;
        state.charted.Add(r);
        mapTexDirty = true;
        // The first island you chart after Saltmere: a word about the island guide (Atlas.cs).
        if (r != "saltmere" && !state.Hinted("atlasHint") && RegionById.ContainsKey(r))
        {
            state.hinted["atlasHint"] = true;
            ToastLater($"New on your map: {RegionById[r].Name}. Click an island's name on the map (<map>) to read about its land, water and wildlife.");
        }
        if (r.StartsWith("amihan:")) Toast($"You've charted {r[7..]}.", 2.5f);
        else if (r.StartsWith("habagat:") && !r.StartsWith("habagat:islet:")) Toast($"You've charted {r[8..]}.", 2.5f);
    }

    // Saves from before charting: Saltmere, plus anywhere you've clearly been (caught a fish there, gone down the
    // caverns, landed on the atoll, been to the village), plus wherever you're standing.
    List<string> LegacyCharted()
    {
        var c = new HashSet<string> { "saltmere" };
        foreach (var s in Data.Spots.Where(s => s.Scene != "sea"))
            if (Data.Common[s.Id].Any(f => state.commons.GetValueOrDefault(f.Id) > 0)) c.Add(s.Scene == "cave" ? "frost" : SpotRegion(s));
        foreach (var (biome, list) in Data.PotCatch)
            if (biome is not ("amihan" or "habagat") && list.Any(f => state.commons.GetValueOrDefault(f.Id) > 0)) c.Add(biome);
        if (state.caveDeepest > 0) c.Add("frost");
        if (state.Hinted("visitedAtoll")) c.Add("atoll");
        if (state.Hinted("niko_request") || state.gifts.ContainsKey("lira_meal")) c.Add("amihan:Amihan Village");
        if (state.scene == "world" && !WaterTile(TileAt((int)(state.px / T), (int)((state.py - 1.5f) / T)))) c.Add(RegionOf(state.px, state.py));
        return c.ToList();
    }

    // Uncharted land on the chart texture: plain parchment with hatching and an inked coast, and (1.43, light on or off, it is
    // the chart) hand-inked coasts round the charted islands (RefreshMapTexture).
    void FogUncharted(Pix img)
    {
        var fog = new bool[ROWS, COLS];
        for (int ty = 0; ty < ROWS; ty++)
            for (int tx = 0; tx < COLS; tx++)
                fog[ty, tx] = !Charted(RegionAt(tx, ty));
        // The open sea is whatever water joins the edge of the map; lakes, lagoons and blue holes inside an island
        // are hidden with the rest of it.
        var sea = new bool[ROWS, COLS];
        var queue = new Queue<(int x, int y)>();
        for (int x = 0; x < COLS; x++) { queue.Enqueue((x, 0)); queue.Enqueue((x, ROWS - 1)); }
        for (int y = 0; y < ROWS; y++) { queue.Enqueue((0, y)); queue.Enqueue((COLS - 1, y)); }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            // Bridges and jetties stand over the sea, so the water under them joins up too.
            if (x < 0 || y < 0 || x >= COLS || y >= ROWS || sea[y, x] || worldMap[y, x] is not ('~' or 'w' or 'T' or 'x' or 'r' or 'I' or 'b' or 'd')) continue;
            sea[y, x] = true;
            queue.Enqueue((x + 1, y)); queue.Enqueue((x - 1, y)); queue.Enqueue((x, y + 1)); queue.Enqueue((x, y - 1));
        }
        bool SeaTile(int tx, int ty) => tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS || sea[ty, tx];
        // Water in a land tile's rounded-off corner belongs to the sea beside it, so the inked coast follows the curve.
        bool Open(int wx, int wy)
        {
            if (wx < 0 || wy < 0 || wx >= PW || wy >= PH) return true;
            if (!Wet(shape[wy * PW + wx])) return false;
            int tx = wx / T, ty = wy / T, cx = wx % T < T / 2 ? -1 : 1, cy = wy % T < T / 2 ? -1 : 1;
            return sea[ty, tx] || SeaTile(tx + cx, ty) || SeaTile(tx, ty + cy) || SeaTile(tx + cx, ty + cy);
        }
        Color paper = Pal.C("#d9c8a0"), hatch = Pal.C("#c2ae84"), ink = Pal.C("#6b5a3e");
        for (int wy = 0; wy < img.H; wy++)
            for (int wx = 0; wx < img.W; wx++)
            {
                if (!fog[wy / T, wx / T] || Open(wx, wy)) continue;
                bool coast = Open(wx - 1, wy) || Open(wx + 1, wy) || Open(wx, wy - 1) || Open(wx, wy + 1);
                img.Buf[wy * img.W + wx] = coast ? ink : (wx + wy) % 6 == 0 ? hatch : paper;
            }
        InkCoasts(img, fog, Open);
    }

    // Pen-and-ink coasts for the charted islands: a dark brown line where land meets the open sea (a lighter one round lakes and
    // lagoons), a faint shadow on the water just past it to the south-east as if the pen leaned, and short dashed ripples a
    // few pixels further out. Fogged land keeps its own ink (FogUncharted), and it is all in the chart's texture only.
    void InkCoasts(Pix img, bool[,] fog, Func<int, int, bool> open)
    {
        int W0 = img.W, H0 = img.H;
        var isOpen = new bool[W0 * H0];
        for (int y = 0; y < H0; y++) for (int x = 0; x < W0; x++) isOpen[y * W0 + x] = open(x, y);
        bool Open(int x, int y) => x < 0 || y < 0 || x >= W0 || y >= H0 || isOpen[y * W0 + x];
        bool Land(int x, int y) => x >= 0 && y >= 0 && x < W0 && y < H0 && !Wet(shape[y * PW + x]);
        var inkC = Pal.C("#4d3d28");
        for (int y = 0; y < H0; y++)
            for (int x = 0; x < W0; x++)
            {
                if (fog[y / T, x / T]) continue;
                int i = y * W0 + x;
                if (!isOpen[i])
                {
                    if (!Land(x, y))   // lagoon and pond rims: the water touching their shore
                    {
                        if (Land(x - 1, y) || Land(x + 1, y) || Land(x, y - 1) || Land(x, y + 1)) img.Buf[i] = Pix.Mix(img.Buf[i], inkC, 0.35f);
                        continue;
                    }
                    bool coast = Open(x - 1, y) || Open(x + 1, y) || Open(x, y - 1) || Open(x, y + 1);
                    if (coast) img.Buf[i] = Pix.Mix(img.Buf[i], inkC, 0.82f);
                    continue;
                }
                // Open water: the shadow of the coast to its south-east, then ripples.
                if (!Open(x - 1, y - 1) && Land(x - 1, y - 1)) { img.Buf[i] = Pix.Mix(img.Buf[i], inkC, 0.24f); continue; }
                if (((x + y) & 1) == 0 && (Land(x - 3, y) || Land(x + 3, y) || Land(x, y - 3) || Land(x, y + 3)) && !Land(x - 1, y) && !Land(x + 1, y) && !Land(x, y - 1) && !Land(x, y + 1))
                    img.Buf[i] = Pix.Mix(img.Buf[i], inkC, 0.13f);
            }
    }

    /* ---------- The pin ---------- */
    bool HasPin => state.pinX != 0 || state.pinY != 0;

    // Arriving at the pin takes it away.
    void CheckPin()
    {
        if (!HasPin || scene != "world" || Dist(player.X, player.Y, state.pinX, state.pinY) > 14) return;
        state.pinX = state.pinY = 0;
        Sfx.Play("blip");
        Toast("You've reached your pin.");
    }

    // On screen, a pin where it is; off it, an arrow at the edge of the view pointing there, with the distance.
    void DrawPinCompass()
    {
        if (!HasPin || scene != "world" || mode is "title" or "create" or "ending" or "pause" or "panel") return;
        float k = ViewK;   // the world view is drawn this many times over
        float sx = (state.pinX - camX) * k, sy = (state.pinY - camY) * k;
        float cx = (player.X - camX) * k, cy = (player.Y - 6 - camY) * k;
        int metres = (int)MathF.Round(Dist(player.X, player.Y, state.pinX, state.pinY) * 0.15f);
        var red = Pal.C("#e04b3a");
        if (sx > 30 && sx < Gfx.LW - 30 && sy > 90 && sy < Gfx.LH - 70)
        {
            float bob = MathF.Sin(time * 4) * 3;
            Gfx.Line(sx, sy, sx, sy - 22 + bob, 3, Pal.Ink);
            Gfx.Circle(sx, sy - 26 + bob, 8, Pal.Ink);
            Gfx.Circle(sx, sy - 26 + bob, 6, red);
            return;
        }
        float dx = sx - cx, dy = sy - cy, len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        float ux = dx / len, uy = dy / len;
        // Where the line from you to the pin leaves the view (inset from the edges and the HUD, and below the goal card).
        var (ax, ay) = EdgePoint(cx, cy, ux, uy);
        float px = -uy, py = ux;
        Gfx.Circle(ax, ay, 20, Pal.WithAlpha(Pal.Ink, 0.75f));
        Gfx.Triangle(ax + ux * 15, ay + uy * 15, ax - ux * 6 + px * 9, ay - uy * 6 + py * 9, ax - ux * 6 - px * 9, ay - uy * 6 - py * 9, red);
        // With Aling Sela's survey compass (story 5), its bearing too, in degrees clockwise from north.
        string label = Wears("survey_compass") ? $"Pin {metres} m, bearing {BearingTo(player.X, player.Y, state.pinX, state.pinY):0}" : $"Pin {metres} m";
        float lw = Gfx.Measure(label, FontKind.Ui700, 15);
        float lx = Math.Clamp(ax - lw / 2, 8, Gfx.LW - lw - 8), ly = ay + (uy > 0.5f ? -46 : 24);
        Gfx.Rect(lx - 6, ly - 2, lw + 12, 22, Pal.WithAlpha(Pal.Ink, 0.75f), 4);
        Gfx.Text(label, lx, ly, FontKind.Ui700, 15, Pal.Paper);
    }

    /* ---------- The chart ---------- */
    // Which chart to open: the one you're on (indoors and underground, the Saltmere one).
    void ChooseChart() => chart = scene == "world" ? ChartFor(player.X) : InHouse ? ChartFor(state.exitX) : 0;
    static int ChartFor(float wx) => wx >= MlStart * T ? 2 : wx >= EastStart * T ? 1 : 0;
    // Where each chart starts and ends, in tiles: the west up to Amihan, Amihan up to Marilaya, Marilaya to the edge.
    static (int x0, int x1) ChartSpan(int c) => c == 2 ? (MlStart, COLS) : c == 1 ? (EastStart, MlStart) : (0, EastStart);
    static readonly string[] ChartNames = { "Saltmere", "Amihan", "Marilaya" };
    // Marilaya's chart is there once you've been east to Amihan (an uncharted island on it, until you find it).
    bool ChartShown(int c) => c != 2 || state.Hinted("amihan") || state.Hinted("marilaya");

    void DrawMap()
    {
        islandHover = null;
        RefreshMapTexture();
        const float pad = 18, head = 54;
        if (!ChartShown(chart)) chart = 1;
        var (cx0, cx1) = ChartSpan(chart);
        float origin = cx0 * T, chartW = (cx1 - cx0) * T;
        float chartH = ROWS * T;
        float scale = Math.Min(1180f / chartW, 590f / chartH);
        float mw = chartW * scale, mh = chartH * scale;
        string title = chart == 2 ? (state.Hinted("marilaya") ? "Marilaya sea chart" : "The far eastern sea") : chartEast ? "Amihan sea chart" : "Map of the islands";
        // The shared panel frame (Ui.PanelFrame), with a compass before the title.
        var (bx, by, bw, bh) = PanelFrame("      " + title);
        DrawCompass(bx + PanelPad + 16, by + PanelPad + 15, 15);
        // What's going on today: the weather, and the derby if it's on.
        string news = state.weather == "storm" ? $"Storm until {HourText((int)StormEnds())}" : state.weather == "rain" ? "Raining" : "Fair weather";
        if (DerbyOn) news += $"   ·   Derby: {(int)derbyT / 60}:{(int)derbyT % 60:00} left";
        float newsX = bx + PanelPad + Gfx.Measure("      " + title, FontKind.Ui700, PanelTitleSize) + 24;
        // A button for each of the other charts, then the island guide (Atlas.cs), opened on the island you're on.
        float tabX = PanelCloseX - 14;
        for (int c = 2; c >= 0; c--)
        {
            if (c == chart || !ChartShown(c)) continue;
            tabX -= SmallW(ChartNames[c]);
            if (SmallButton(ChartNames[c], tabX, by + PanelPad - 6)) { chart = c; return; }
            tabX -= 14;
        }
        float gx = tabX - SmallW("Island guide");
        if (SmallButton("Island guide", gx, by + PanelPad - 6)) { OpenAtlas(HereRegion()); return; }
        if (PanelClose()) { ClosePanels(); return; }
        // The weather between the title and the buttons, shortened if there isn't room.
        if (gx - 16 - newsX > 40) Gfx.Text(Gfx.Ellipsize(news, FontKind.Ui600, 17, gx - 16 - newsX), newsX, by + PanelPad + 8, FontKind.Ui600, 17, Muted);
        // A narrow chart sits on the left, with its key beside it.
        float mx = bw - mw > 380 ? bx + pad + 7 : bx + (bw - mw) / 2, my = by + bh - pad - mh + 4;
        // A sea chart's frame: a thin rule, a gap, then the ink border, and faint lines of a grid over the chart.
        Gfx.Rect(mx - 7, my - 7, mw + 14, mh + 14, Pal.Ink, 2);
        Gfx.Rect(mx - 5, my - 5, mw + 10, mh + 10, Pal.Paper, 1);
        Gfx.Rect(mx - 3, my - 3, mw + 6, mh + 6, Pal.Ink);
        DrawTexturePro(mapTex, new Rectangle(origin, 0, chartW, chartH), Gfx.S(mx, my, mw, mh), Vector2.Zero, 0, Color.White);
        // Aged paper (1.43): the edges of the chart darken a little, the corners a little more.
        for (int e = 0; e < 8; e++)
        {
            var shade = Pal.Rgba(70, 46, 20, 0.24f * (1 - e / 8f) * (1 - e / 8f));
            Gfx.Rect(mx, my + e * 3, mw, 3, shade); Gfx.Rect(mx, my + mh - (e + 1) * 3, mw, 3, shade);
            Gfx.Rect(mx + e * 3, my + 24, 3, mh - 48, shade); Gfx.Rect(mx + mw - (e + 1) * 3, my + 24, 3, mh - 48, shade);
        }
        var grid = Pal.C("rgba(241,230,200,0.13)");
        for (float gxw = MathF.Ceiling(origin / 200) * 200; gxw < origin + chartW; gxw += 200) Gfx.Rect(mx + (gxw - origin) * scale, my, 1, mh, grid);
        for (float gyw = 200; gyw < chartH; gyw += 200) Gfx.Rect(mx, my + gyw * scale, mw, 1, grid);
        // A narrow chart leaves room at the sides: a key to the marks.
        if (bw - mw > 380) DrawMapKey(mx + mw + 30, my + 8, bx + bw - (mx + mw + 30) - pad);
        Vector2 M(float wx, float wy) => new(mx + (wx - origin) * scale, my + wy * scale);
        bool OnChart(float wx, float wy) => wx >= origin && wx < origin + chartW && wy >= 0 && wy < chartH;

        var marks = new List<MapMark>();
        Color white = White, gold = Pal.Lantern, spotOpen = Pal.C("#7fd6f0"), spotShut = Pal.C("#8a8f93");
        // Island names: in place once charted, a question until then.
        var isles = new List<(string region, string name, float x, float y)>
        {
            ("saltmere", "Saltmere Island", 255, 182), ("frost", "Frostfang Isle", 560, 186), ("dunes", "Sunscald Dunes", 760, 292),
            ("mire", "Mirewood", 100, 236), ("atoll", "Starfall Atoll", 1120, 170)
        };
        foreach (var i in AmihanIslands) isles.Add(("amihan:" + i.name, i.name, i.cx * T, (i.cy + i.ry + 2) * T));
        foreach (var h in HabagatIslands.Where(h => h.name != "Daang Pulo")) isles.Add(("habagat:" + h.name, h.name, h.cx * T, (h.cy + h.ry + 1.5f) * T));
        // Marilaya's (Marilaya.cs): the island across its middle, and its three groups of islets.
        isles.Add(("marilaya", "Marilaya", MlWx(11.5f), MlWy(16.5f)));
        isles.Add(("marilaya:north", "North islets", MlWx(25.6f), MlWy(1.4f)));
        isles.Add(("marilaya:salomague", "Salomague", MlWx(27.6f), MlWy(15.0f)));
        isles.Add(("marilaya:tresreyes", "Tres Reyes", MlWx(3.6f), MlWy(32.2f)));
        foreach (var (region, name, wx, wy) in isles)
        {
            if (!OnChart(wx, wy)) continue;
            var p = M(wx, wy);
            marks.Add(new(Charted(region) ? name : "Uncharted island", p.X, p.Y, white, Charted(region) ? white : Pal.C("#f1e6c8"), 22, false, null, 0, Charted(region) ? region : null));
        }
        // Daang Pulo's islets share one name, with how many you've charted.
        if (OnChart(0, 0))
        {
            var p = M(68 * T, 74.5f * T);
            int n = IsletsCharted;
            marks.Add(new(n == 0 ? "Uncharted islets" : $"Daang Pulo ({n} of {IsletCount} islets)", p.X, p.Y, white, n > 0 ? white : Pal.C("#f1e6c8"), n > 0 ? 20 : 22, false, null, 0,
                Charted("habagat:islet:0") ? "habagat:islet:0" : null));
        }
        // Fishing spots, where you've charted them.
        foreach (var s in Data.Spots.Where(s => s.Scene == "world" && SpotKnown(s) && Charted(SpotRegion(s))))
        {
            if (!OnChart(s.X, s.Y)) continue;
            var p = M(s.X, s.Y);
            bool open = SpotOpen(s.Id);
            marks.Add(new(open ? s.Label : s.Label + " (closed)", p.X, p.Y, open ? spotOpen : spotShut, Pal.Paper, 14, true, SpotTip(s), 2));
        }
        // People and places.
        void Place(string label, float wx, float wy, List<string> tip, int prio = 3)
        {
            if (!OnChart(wx, wy)) return;
            var p = M(wx, wy);
            marks.Add(new(label, p.X, p.Y, gold, gold, 14, true, tip, prio));
        }
        // A place with a small picture instead of a dot (MapGlyph): 1 a stall, 2 a pier, 3 a cave, 4 a volcano, 5 a flag,
        // 6 seaweed lines, 7 a tree, 8 a boat. It still keeps the dot's place, colour and tooltip.
        void PlaceG(string label, float wx, float wy, int glyph, List<string> tip, int prio = 3)
        {
            if (!OnChart(wx, wy)) return;
            var p = M(wx, wy);
            marks.Add(new(label, p.X, p.Y, gold, gold, 14, true, tip, prio, null, glyph));
        }
        if (Charted("frost")) PlaceG("Frostfang Caverns", MouthDoorX, MouthDoorY - 26, 3, new() { "Frostfang Caverns", state.caveDeepest > 0 ? $"Deepest floor reached: {state.caveDeepest}" : "Twelve floors down, they say." });
        PlaceG("Pip's stall", PipX, PipY - 6, 1, new() { "Pip's stall", PipOpen ? $"Open until {HourText(21 * 60)}" : $"Closed. Opens at {HourText(7 * 60)}" });
        PlaceG("Pip's jetty", SaltJettyX, SaltJettyY, 2, new() { "Pip's jetty", Has("boat") > 0 ? "E sails to Starfall Atoll; F takes the helm" : "You'll need a boat" });
        if (chartEast)
        {
            PlaceG("Village landing", 1495, 217, 2, new() { "Village landing", "Moor here for Amihan Village" });
            // Once Ma'am Isay has marked the route (RestlessSea.cs).
            if (state.Hinted("rs:signs")) PlaceG("School Rise", RiseX, RiseY + 12, 5, new() { "School Rise", "Amihan Village's evacuation area", "Follow the green signs up from the landing" }, 2);
            foreach (var n in Islanders)
                if (Charted(RegionOf(n.x, n.y + 10))) Place(n.name, n.x, n.y, new() { n.name, IslanderNote(n.id) });
            // Baga's volcano (Magayon.cs): the permanent danger zone, the alert level, and the evacuation while it stands.
            if (Charted("amihan:Baga Island"))
            {
                var tip = new List<string> { "Baga's permanent danger zone", "Closed at every alert level", $"Alert level {BagaLevel}: {AlertLevels[BagaLevel].Name}" };
                if (BagaClosed) tip.Add("Baga is evacuated until the advisory reopens it");
                PlaceG("Danger zone", PdzX, PdzY + 4, 4, tip, 1);
            }
            // The sanctuary's name sits in its middle, and Bantay Joy on her platform.
            var sp = M(SanctCX * T, (SanctCY + SanctRY + 1.2f) * T);
            marks.Add(new("Marine sanctuary", sp.X, sp.Y, white, Pal.C("#bff4ff"), 20, false, null, 0));
            Place("Bantay Joy", JoyX, JoyY, new() { "Bantay Joy, the sea warden", "No fishing or traps inside the buoys", $"Seen: {SeaKinds.Keys.Count(k => state.sightings.GetValueOrDefault(k) > 0)} of {SeaKinds.Count} animals" });
            // Maya's guso farm, and Bakawan's firefly trees once Tala has told you about them (or you've found them).
            if (Charted("amihan:Luntian Karsts"))
            {
                bool ready = Enumerable.Range(0, GusoLines.Length).Any(i => GusoLineOpen(i) && GusoReady(GusoAt(i)));
                PlaceG(ready ? "Seaweed farm (ready)" : "Seaweed farm", GusoLines[0].x, GusoFarmY, 6, GusoTip(), 2);
            }
            if (Charted("amihan:Bakawan Island") && (state.Hinted("fireflies") || state.sightings.GetValueOrDefault("alitaptap") > 0))
                PlaceG("Alitaptap trees", FireflyTrees[1].x, FireflyTrees[1].y - 6, 7, new()
                {
                    "Pagatpat trees", "The alitaptap gather here after dark, but not in the rain",
                    GlowNight ? "Tonight the water glows: the moon is small" : "The water glows on nights when the moon is small"
                });
        }
        else if (state.Hinted("habagat"))
        {
            PlaceG("Asinan landing", AsinanJettyX, AsinanJettyY - 16, 2, new() { "Asinan landing", Has("boat") > 0 ? "E sails from here; F takes the helm" : "You'll need a boat" });
            foreach (var n in HabagatFolk)
                if (Charted(RegionOf(n.x, n.y + 10))) Place(n.name, n.x, n.y, new() { n.name, HabagatNote(n.id) });
            if (race != null || raceArmed)
                for (int i = 0; i < RaceGates.Length; i++)
                {
                    bool next = race != null ? (race.Next < RaceGates.Length ? race.Next : 0) == i : i == 0;
                    Place(i == 0 ? "Start" : $"Gate {i}", RaceGates[i].x, RaceGates[i].y, new() { i == 0 ? "Start and finish" : $"Regatta gate {i}", next ? "Next!" : "" }, next ? 1 : 4);
                }
        }
        // Marilaya's chart (MarilayaStory.cs): the harbour once you've found it, for sailing, and its places once charted.
        if (chart == 2) MarilayaChartMarks(Place);
        foreach (var b in state.builds.Where(b => b.id == "dryrack" && Rack(b) is RackLoad r && r.fish.Count > 0))
            Place(RackDone(Rack(b)) ? "Rack (dry!)" : "Drying rack", b.x * T + 5, b.y * T + 5, new() { "Your drying rack", RackDone(Rack(b)) ? (RackIsGuso(Rack(b)) ? "The dried guso is ready" : "The dried fish are ready") : $"{(int)((DryGoal - Rack(b).dry) / DryRate)} minutes of sun to go" }, 2);
        if (Has("boat") > 0 && !Aboard) { var bp = BoatPosition(); PlaceG("Your boat", bp.x, bp.y, 8, new() { "Your boat", "Moored here. R beside it to board." }, 1); }
        if (state.tamed && !state.riding) Place(Data.MountName, state.mountX, state.mountY - 6, new() { Data.MountName, Bind.Fix("Waiting here. <ride> whistles it over from anywhere outdoors; it comes to you.") }, 1);
        foreach (var b in state.builds.Where(b => b.id == "crabpot"))
        {
            bool ready = PotReady(b);
            Place(ready ? "Crab pot (ready)" : "Crab pot", b.x * T + 5, b.y * T + 5, new() { "Your crab pot", ready ? "Ready to haul up" : "Soaking. Haul it up tomorrow morning." }, 2);
        }
        foreach (var b in state.builds.Where(b => b.id == "bubo"))
        {
            bool ready = PotReady(b);
            Place(ready ? "Bubo (ready)" : "Bubo", b.x * T + 5, b.y * T + 5, new() { "Your bubo", ready ? "Ready to lift" : "Soaking. Lift it tomorrow morning." }, 2);
        }
        if (Wears("echo_sounder") && scene == "world")
            foreach (var sc in schools) Place("Feeding fish", sc.X, sc.Y, new() { "Feeding fish", "Your echo sounder hears a frenzy here" }, 4);
        if (HasPin) Place("Pin", state.pinX, state.pinY, new() { "Your pin", "Click it to take it away" }, 1);
        // The goal you're following (Guide.cs), in green so it never reads as the pin or a person.
        if (GoalOnChart() is { } goalAt && OnChart(goalAt.x, goalAt.y))
        {
            var gp = M(goalAt.x, goalAt.y);
            marks.Add(new("Goal", gp.X, gp.Y, Pal.C("#5fd36b"), Pal.C("#bff2b0"), 16, true, new() { tracked.Title, Bind.Fix(tracked.Text) }, 1));
        }

        // Where you are, or an arrow at the edge if you're off this chart.
        var (youX, youY, youLabel) = scene == "world" ? (player.X, player.Y - 6, "You")
            : scene == "cave" ? (MouthDoorX, MouthDoorY + 2, caveFloor == AncientFloor ? "You (Ancient Floor)" : $"You (floor {caveFloor})")
            : (state.exitX, state.exitY - 9, "You");
        var placed = new List<Rectangle>();
        foreach (var m in marks.Where(m => m.HasDot)) placed.Add(new Rectangle(m.X - 6, m.Y - 6, 12, 12));
        Vector2 you = default;
        bool youOn = OnChart(youX, youY), youEast = youX >= origin + chartW;
        // Off this chart, an arrow at its edge level with you (with its label kept clear of the others).
        string youTxt = ChartFor(youX) switch { 2 => "You (Marilaya waters)", 1 => "You (Amihan sea)", _ => "You (Saltmere waters)" };
        float youTw = Gfx.Measure(youTxt, FontKind.Ui700, 16);
        float arrowY = Math.Clamp(my + youY * scale, my + 20, my + mh - 20), arrowX = youEast ? mx + mw - 14 : mx + 14;
        if (youOn) { you = M(youX, youY); placed.Add(new Rectangle(you.X - 10, you.Y - 10, 20, 20)); }
        else placed.Add(new Rectangle(youEast ? arrowX - 20 - youTw : arrowX - 14, arrowY - 14, youTw + 34, 28));

        // Dots first, then the labels placed so they don't overlap (with a short line when one has to move away).
        foreach (var m in marks.Where(m => m.HasDot))
        {
            // A shape as well as a colour, so the key reads without telling green from red: a square is your goal, a diamond your
            // pin, a cross over the dot is a spot that is shut.
            int kind = m.Label == "Goal" ? 1 : m.Label == "Pin" ? 2 : m.Dot.Equals(Pal.C("#8a8f93")) ? 3 : 0;
            if (m.Glyph > 0) MapGlyph(m.X, m.Y, m.Glyph, m.Dot);
            else MapShape(m.X, m.Y, kind, m.Label == "Pin" ? Pal.C("#e04b3a") : m.Dot);
        }
        var chartBox = new Rectangle(mx, my, mw, mh);
        foreach (var m in marks.OrderBy(m => m.Prio))
        {
            var font = m.Size >= 20 ? FontKind.Ui700 : FontKind.Ui600;
            float w = Gfx.Measure(m.Label, font, m.Size), h = m.Size + 4;
            var spots = m.HasDot ? LabelSpots(w, h) : new List<(float x, float y)> { (-w / 2, 0), (-w / 2, -24), (-w / 2, 24), (-w / 2, -44), (-w / 2, 44) };
            int pick = -1, least = 0, leastHits = int.MaxValue;
            for (int i = 0; i < spots.Count && pick < 0; i++)
            {
                var r = new Rectangle(m.X + spots[i].x - 3, m.Y + spots[i].y - 1, w + 6, h + 2);
                if (r.X < chartBox.X || r.Y < chartBox.Y || r.X + r.Width > chartBox.X + chartBox.Width || r.Y + r.Height > chartBox.Y + chartBox.Height) continue;
                int hits = placed.Count(o => CheckCollisionRecs(o, r));
                if (hits == 0) pick = i;
                else if (hits < leastHits) { leastHits = hits; least = i; }
            }
            // Nowhere clear: a person or a small place keeps only its dot (its name shows when you point at it); a more
            // important mark takes the least crowded spot.
            bool hidden = pick < 0 && m.HasDot && m.Prio >= 3;
            if (hidden && !(Dist(Gfx.Mouse.X, Gfx.Mouse.Y, m.X, m.Y) < 10)) continue;
            if (pick < 0) pick = least;
            var at = new Rectangle(m.X + spots[pick].x, m.Y + spots[pick].y, w, h);
            placed.Add(new Rectangle(at.X - 3, at.Y - 1, at.Width + 6, at.Height + 2));
            if (m.HasDot && MathF.Abs(spots[pick].x + w / 2) + MathF.Abs(spots[pick].y + h / 2) > 26)
            {
                float lx = Math.Clamp(m.X, at.X, at.X + at.Width), ly = Math.Clamp(m.Y, at.Y, at.Y + at.Height);
                Gfx.Line(m.X, m.Y, lx, ly, 1.5f, Pal.WithAlpha(m.Text, 0.7f));
            }
            // A soft dark plate under a mark's name, so it reads over sand and forest alike.
            if (m.HasDot) Gfx.Rect(at.X - 3, at.Y - 1, at.Width + 6, at.Height + 2, Pal.C("rgba(12,26,38,0.5)"), 3);
            Shadowed(m.Label, at.X, at.Y, font, m.Size, m.Text);
            // A charted island's name opens its page in the island guide.
            if (m.Region != null)
            {
#if DEBUG
                Gfx.Seen["isle:" + m.Region] = at;
#endif
                if (Gfx.Hover(at.X, at.Y, at.Width, at.Height))
                {
                    Gfx.Rect(at.X, at.Y + at.Height - 2, at.Width, 2, m.Text);
                    islandHover = m.Label;
                }
                if (Gfx.Click(at.X, at.Y, at.Width, at.Height)) { OpenAtlas(m.Region); return; }
            }
        }
        if (youOn)
        {
            float pulse = 1 + 0.3f * MathF.Sin(time * 6);
            Gfx.Circle(you.X, you.Y, 10 * pulse, Pal.WithAlpha(Pal.Buoy, 0.35f));
            Gfx.Circle(you.X, you.Y, 6.5f, White);
            Gfx.Circle(you.X, you.Y, 5, Pal.Buoy);
            // Underground the label goes below the dot so it doesn't cover the caverns' own label.
            Shadowed(youLabel, you.X - Gfx.Measure(youLabel, FontKind.Ui700, 16) / 2, you.Y + (scene == "cave" ? 12 : -28), FontKind.Ui700, 16, White);
        }
        else
        {
            // You're on the other chart: an arrow at this one's edge, level with you.
            Gfx.Circle(arrowX, arrowY, 12, Pal.Buoy);
            float d = youEast ? 1 : -1;
            Gfx.Triangle(arrowX + d * 8, arrowY, arrowX - d * 4, arrowY - 7, arrowX - d * 4, arrowY + 7, White);
            Shadowed(youTxt, youEast ? arrowX - 20 - youTw : arrowX + 20, arrowY - 10, FontKind.Ui700, 16, White);
        }

        // Hovering a dot: what's there. Clicking the chart drops a pin (clicking the pin takes it away).
        var hovered = marks.Where(m => m.HasDot && m.Tip != null && Dist(Gfx.Mouse.X, Gfx.Mouse.Y, m.X, m.Y) < 10)
            .OrderBy(m => Dist(Gfx.Mouse.X, Gfx.Mouse.Y, m.X, m.Y)).FirstOrDefault();
        mapHover = hovered?.Tip[0];
#if DEBUG
        // So the autotest can point at a dot or click the chart.
        foreach (var m in marks.Where(m => m.HasDot)) Gfx.Seen["dot:" + m.Label] = new Rectangle(m.X - 4, m.Y - 4, 8, 8);
        Gfx.Seen["chart"] = new Rectangle(mx, my, mw, mh);
#endif
        if (Gfx.Click(mx, my, mw, mh))
        {
            float wx = origin + (Gfx.Mouse.X - mx) / scale, wy = (Gfx.Mouse.Y - my) / scale;
            if (HasPin && Dist(Gfx.Mouse.X, Gfx.Mouse.Y, M(state.pinX, state.pinY).X, M(state.pinX, state.pinY).Y) < 12)
            {
                state.pinX = state.pinY = 0;
                Sfx.Play("blip");
            }
            else
            {
                state.pinX = Math.Max(1, wx); state.pinY = Math.Max(1, wy);
                Sfx.Play("blip");
                if (!state.Hinted("pin"))
                {
                    state.hinted["pin"] = true;
                    Toast("Pin dropped. An arrow at the edge of the view points to it; click the pin again to take it away.", 5);
                }
            }
        }
        if (hovered != null) DrawMapTip(hovered.Tip, Gfx.Mouse.X + 16, Gfx.Mouse.Y + 12);
        else if (islandHover != null) Gfx.Text($"Click to read about {islandHover} in the island guide", mx + 10, my + mh - 26, FontKind.Ui600, 15, Pal.C("rgba(241,230,200,0.9)"));
        else if (Gfx.Hover(mx, my, mw, mh)) Gfx.Text("Click to drop a pin, or an island's name to read about it", mx + 10, my + mh - 26, FontKind.Ui600, 15, Pal.C("rgba(241,230,200,0.8)"));
        islandHover = null;
        if (Gfx.PressedOutside(bx, by, bw, bh)) ClosePanels();
    }

    // Where a dot's name may go, nearest first: under, over, right and left of it, then the same further out (with a
    // leader line), then the diagonals.
    static List<(float x, float y)> LabelSpots(float w, float h)
    {
        var list = new List<(float, float)>();
        foreach (float d in new[] { 7f, 20f, 34f, 50f })
        {
            list.Add((-w / 2, d)); list.Add((-w / 2, -h - d + 1)); list.Add((d + 2, -h / 2)); list.Add((-w - d - 2, -h / 2));
            list.Add((d, d * 0.6f)); list.Add((-w - d, d * 0.6f)); list.Add((d, -h - d * 0.6f)); list.Add((-w - d, -h - d * 0.6f));
        }
        return list;
    }

    // A small compass rose, north up, in ink and gold.
    static void DrawCompass(float cx, float cy, float r)
    {
        Gfx.Circle(cx, cy, r, Pal.Ink);
        Gfx.Circle(cx, cy, r - 2, Pal.Paper);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2 - MathF.PI / 2, b = a + MathF.PI / 2;
            float tx = cx + MathF.Cos(a) * (r - 1), ty = cy + MathF.Sin(a) * (r - 1);
            float lx = cx + MathF.Cos(a - MathF.PI / 4) * r * 0.28f, ly = cy + MathF.Sin(a - MathF.PI / 4) * r * 0.28f;
            float qx = cx + MathF.Cos(a + MathF.PI / 4) * r * 0.28f, qy = cy + MathF.Sin(a + MathF.PI / 4) * r * 0.28f;
            var c = i == 0 ? Pal.Buoy : Pal.Ink;
            Gfx.Triangle(tx, ty, lx, ly, cx, cy, c);
            Gfx.Triangle(tx, ty, cx, cy, qx, qy, i == 0 ? Pal.C("#e87a5f") : Pal.C("#5a4a38"));
        }
        Gfx.Circle(cx, cy, 2.2f, Pal.Lantern);
    }

    // The key beside a narrow chart: what each mark means.
    void DrawMapKey(float x, float y, float w)
    {
        Gfx.Text("Key", x, y, FontKind.Ui700, 20, Pal.PaperInk);
        float ky = y + 34;
        void Row(Color dot, string what, bool you = false, int kind = 0, int glyph = 0)
        {
            if (you) { Gfx.Circle(x + 8, ky + 9, 6.5f, White); Gfx.Circle(x + 8, ky + 9, 5, Pal.Buoy); }
            else if (glyph > 0) MapGlyph(x + 8, ky + 9, glyph, dot, 0.8f);
            else MapShape(x + 8, ky + 9, kind, dot, 4);
            foreach (var line in Gfx.Wrap(what, FontKind.Ui600, 15, w - 24)) { Gfx.Text(line, x + 22, ky, FontKind.Ui600, 15, Muted); ky += 19; }
            ky += 8;
        }
        Row(Pal.C("#7fd6f0"), "Fishing spot");
        Row(Pal.C("#8a8f93"), "Closed for now (crossed)", kind: 3);
        Row(Pal.Lantern, "Someone to meet, or a place");
        Row(Pal.Lantern, "A place with its own picture (stall, pier, cave...)", glyph: 1);
        Row(Pal.C("#7fd36b"), "Your goal (square)", kind: 1);
        Row(Pal.C("#e04b3a"), "Your pin (diamond)", kind: 2);
        Row(Pal.Buoy, "You", you: true);
        ky += 6;
        foreach (var line in Gfx.Wrap("Click the chart to drop a pin. Click an island's name to read about it.", FontKind.Ui500, 14, w))
        { Gfx.Text(line, x, ky, FontKind.Ui500, 14, Muted); ky += 18; }
    }

    // A small picture for a place on the chart (1.43), drawn outlined in the mark's colour: 1 a stall, 2 a pier, 3 a cave, 4 a
    // volcano, 5 a flag, 6 seaweed lines, 7 a tree, 8 a boat. `s` scales it. Places only: the goal, the pin and a closed spot keep
    // their own shapes (MapShape).
    static void MapGlyph(float cx, float cy, int glyph, Color fill, float s = 1)
    {
        void Draw(float e, Color c, bool inner)
        {
            switch (glyph)
            {
                case 1:
                    Gfx.Triangle(cx - 7 * s - e, cy - 1 * s + e, cx + 7 * s + e, cy - 1 * s + e, cx, cy - 8 * s - e, c);
                    Gfx.Rect(cx - 5 * s - e, cy - 1 * s, 10 * s + 2 * e, 7 * s + e, c);
                    if (inner) Gfx.Rect(cx - 1.5f * s, cy + 2 * s, 3 * s, 4 * s, Pal.Ink);
                    break;
                case 2:
                    Gfx.Rect(cx - 2 * s - e, cy - 8 * s - e, 4 * s + 2 * e, 16 * s + 2 * e, c);
                    Gfx.Rect(cx - 6 * s - e, cy - 5 * s - e, 12 * s + 2 * e, 2 * s + 2 * e, c);
                    Gfx.Rect(cx - 6 * s - e, cy + 2 * s - e, 12 * s + 2 * e, 2 * s + 2 * e, c);
                    break;
                case 3:
                    Gfx.Circle(cx, cy - 1 * s, 7 * s + e, c);
                    Gfx.Rect(cx - 7 * s - e, cy - 1 * s, 14 * s + 2 * e, 7 * s + e, c);
                    if (inner) { Gfx.Circle(cx, cy, 3 * s, Pal.Ink); Gfx.Rect(cx - 3 * s, cy, 6 * s, 5 * s, Pal.Ink); }
                    break;
                case 4:
                    Gfx.Triangle(cx - 8 * s - e, cy + 6 * s + e, cx + 8 * s + e, cy + 6 * s + e, cx, cy - 7 * s - e, c);
                    if (inner) { Gfx.Rect(cx - 2 * s, cy - 6 * s, 4 * s, 2 * s, Pal.Ink); Gfx.Rect(cx - 1 * s, cy - 7 * s, 2 * s, 2 * s, Pal.C("#e04b3a")); }
                    break;
                case 5:
                    Gfx.Rect(cx - 1 * s - e, cy - 8 * s - e, 2 * s + 2 * e, 16 * s + 2 * e, c);
                    Gfx.Triangle(cx + 1 * s, cy - 8 * s - e, cx + 8 * s + e, cy - 5 * s, cx + 1 * s, cy - 2 * s + e, c);
                    break;
                case 6:
                    for (int i = -1; i <= 1; i++) Gfx.Rect(cx + i * 4 * s - 1 * s - e, cy - (5 - i * i * 2) * s - e, 2 * s + 2 * e, (12 - i * i * 2) * s + 2 * e, c);
                    break;
                case 7:
                    Gfx.Circle(cx, cy - 3 * s, 5 * s + e, c);
                    Gfx.Rect(cx - 1 * s - e, cy, 2 * s + 2 * e, 7 * s + e, c);
                    break;
                case 8:
                    Gfx.Rect(cx - 7 * s - e, cy + 1 * s - e, 14 * s + 2 * e, 3 * s + 2 * e, c);
                    Gfx.Triangle(cx, cy - 8 * s - e, cx + 6 * s + e, cy, cx, cy, c);
                    break;
            }
        }
        Draw(1.6f * s, Pal.Ink, false);
        Draw(0, fill, true);
    }

    // A chart mark's shape (so it reads without its colour): 0 a dot, 1 a square, 2 a diamond, 3 a dot with a cross over it.
    static void MapShape(float x, float y, int kind, Color fill, float r = 5)
    {
        switch (kind)
        {
            case 1:
                Gfx.Rect(x - r - 1.5f, y - r - 1.5f, 2 * r + 3, 2 * r + 3, Pal.Ink);
                Gfx.Rect(x - r, y - r, 2 * r, 2 * r, fill);
                break;
            case 2:
                float o = r + 3.5f;
                Gfx.Triangle(x, y - o, x + o, y, x, y + o, Pal.Ink); Gfx.Triangle(x, y - o, x, y + o, x - o, y, Pal.Ink);
                float i = r + 0.5f;
                Gfx.Triangle(x, y - i, x + i, y, x, y + i, fill); Gfx.Triangle(x, y - i, x, y + i, x - i, y, fill);
                break;
            default:
                Gfx.Circle(x, y, r + 1.5f, Pal.Ink);
                Gfx.Circle(x, y, r, fill);
                if (kind == 3) { Gfx.Line(x - r * 0.7f, y - r * 0.7f, x + r * 0.7f, y + r * 0.7f, 2, Pal.Ink); Gfx.Line(x - r * 0.7f, y + r * 0.7f, x + r * 0.7f, y - r * 0.7f, 2, Pal.Ink); }
                break;
        }
    }

    // What's at a spot: whether it's open, how much of it you've caught, and what's biting there now.
    List<string> SpotTip(Spot s)
    {
        var tip = new List<string> { s.Label };
        if (!SpotOpen(s.Id)) tip.Add(s.Id == "wreck" ? "Closed: the tide is in" : s.Id == "deep" ? "Closed: the old dock needs repairing" : s.Id == "bagareef" ? "Closed: Baga's evacuation order" : "Closed");
        var fish = Data.Common[s.Id];
        int got = fish.Count(f => state.commons.GetValueOrDefault(f.Id) > 0);
        tip.Add($"Caught here: {got} of {fish.Length}");
        var biting = fish.Where(BitingNow).ToList();
        var known = biting.Where(f => state.commons.GetValueOrDefault(f.Id) > 0).Select(f => f.Name).ToList();
        int unknown = biting.Count - known.Count;
        if (biting.Count == 0) tip.Add("Nothing is biting here just now");
        else tip.Add("Biting now: " + (known.Count > 0 ? string.Join(", ", known) : "") + (unknown > 0 ? (known.Count > 0 ? $", and {unknown} you haven't caught" : $"{unknown} you haven't caught") : ""));
        return tip;
    }

    string HabagatNote(string id) => id switch
    {
        "rosa" => RakedToday ? "Makes salt; you've raked the beds today" : "Makes salt; rake the beds on a dry day",
        "pacing" => "Plays sungka, and knows the old stories",
        "dado" => state.Hinted("isletsCharted") ? "Runs the regatta round the islets" : "Wants you to chart every islet first",
        "celso" => state.parola >= 3 ? "Keeps the Parola light" : $"Needs help to light the lighthouse ({state.parola} of 3)",
        _ => ""
    };

    static string IslanderNote(string id) => id switch
    {
        "lira" => "Cooks a meal for visiting fishers, once a day",
        "niko" => "Tends the nets; has a job for you",   // (bangus, then asohos)
        "maya" => "Runs a guso farm in the karst lagoon",
        "tala" => "Watches the mangroves' wildlife, and keeps a list",
        "isay" => "Teaches the village children about fish: a lesson a day",
        "mila" => "Grows abaca and pili on Baga's north shore",
        "ben" => "Runs Baga's volcano station and its alert board",
        _ => ""
    };

    void DrawMapTip(List<string> lines, float x, float y)
    {
        float w = lines.Max(l => Gfx.Measure(l, FontKind.Ui500, 16)) + 24, h = lines.Count * 22 + 14;
        x = Math.Min(x, Gfx.LW - w - 8); y = Math.Min(y, Gfx.LH - h - 8);
        Gfx.Rect(x, y, w, h, NavyStrong, 5);
        for (int i = 0; i < lines.Count; i++)
            Gfx.Text(lines[i], x + 12, y + 7 + i * 22, i == 0 ? FontKind.Ui700 : FontKind.Ui500, 16, i == 0 ? Pal.Lantern : Pal.Paper);
    }
}
