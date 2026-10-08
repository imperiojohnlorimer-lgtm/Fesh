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

    sealed record MapMark(string Label, float X, float Y, Color Dot, Color Text, float Size, bool HasDot, List<string> Tip, int Prio);

    /* ---------- Charting ---------- */
    // An island: one of the five original biomes, one of Amihan's four islands by name, or a Habagat island (Asinan,
    // Parola, or an islet of Daang Pulo by number).
    string RegionAt(int tx, int ty)
    {
        byte b = BiomeAt(tx, ty);
        if (b < 5) return Data.Biomes[b].Id;
        if (b == 6) return HabagatRegion(tx, ty);
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

    // Uncharted land on the chart texture: plain parchment with hatching and an inked coast (RefreshMapTexture).
    void FogUncharted(Pix img)
    {
        if (state.charted == null) return;
        var fog = new bool[ROWS, COLS];
        bool any = false;
        for (int ty = 0; ty < ROWS; ty++)
            for (int tx = 0; tx < COLS; tx++)
                any |= fog[ty, tx] = !Charted(RegionAt(tx, ty));
        if (!any) return;
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
        bool Open(int wx, int wy) => wx < 0 || wy < 0 || wx >= PW || wy >= PH || Wet(shape[wy * PW + wx]) && sea[wy / T, wx / T];
        Color paper = Pal.C("#d9c8a0"), hatch = Pal.C("#c2ae84"), ink = Pal.C("#6b5a3e");
        for (int wy = 0; wy < img.H; wy++)
            for (int wx = 0; wx < img.W; wx++)
            {
                if (!fog[wy / T, wx / T] || Open(wx, wy)) continue;
                bool coast = Open(wx - 1, wy) || Open(wx + 1, wy) || Open(wx, wy - 1) || Open(wx, wy + 1);
                img.Buf[wy * img.W + wx] = coast ? ink : (wx + wy) % 6 == 0 ? hatch : paper;
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
        const float k = 4;   // the world view is drawn 4x
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
        // Where the line from you to the pin leaves the view (inset from the edges and the HUD).
        float t = float.MaxValue;
        if (ux > 0) t = MathF.Min(t, (Gfx.LW - 46 - cx) / ux); else if (ux < 0) t = MathF.Min(t, (46 - cx) / ux);
        if (uy > 0) t = MathF.Min(t, (Gfx.LH - 80 - cy) / uy); else if (uy < 0) t = MathF.Min(t, (100 - cy) / uy);
        float ax = cx + ux * t, ay = cy + uy * t;
        float px = -uy, py = ux;
        Gfx.Circle(ax, ay, 20, Pal.WithAlpha(Pal.Ink, 0.75f));
        Gfx.Triangle(ax + ux * 15, ay + uy * 15, ax - ux * 6 + px * 9, ay - uy * 6 + py * 9, ax - ux * 6 - px * 9, ay - uy * 6 - py * 9, red);
        string label = $"Pin {metres} m";
        float lw = Gfx.Measure(label, FontKind.Ui700, 15);
        float lx = Math.Clamp(ax - lw / 2, 8, Gfx.LW - lw - 8), ly = ay + (uy > 0.5f ? -46 : 24);
        Gfx.Rect(lx - 6, ly - 2, lw + 12, 22, Pal.WithAlpha(Pal.Ink, 0.75f), 4);
        Gfx.Text(label, lx, ly, FontKind.Ui700, 15, Pal.Paper);
    }

    /* ---------- The chart ---------- */
    // Which chart to open: the one you're on (indoors and underground, the Saltmere one).
    void ChooseChart() => chartEast = scene == "world" && player.X >= EastStart * T;

    void DrawMap()
    {
        Backdrop();
        RefreshMapTexture();
        const float pad = 18, head = 54;
        float origin = chartEast ? EastStart * T : 0, chartW = chartEast ? (COLS - EastStart) * T : EastStart * T;
        float chartH = ROWS * T;
        float scale = Math.Min(1120f / chartW, 550f / chartH);
        float mw = chartW * scale, mh = chartH * scale, bw = mw + pad * 2, bh = mh + pad * 2 + head;
        float bx = (Gfx.LW - bw) / 2, by = (Gfx.LH - bh) / 2;
        Gfx.Box(bx, by, bw, bh, Pal.Paper, Pal.Ink, 3, 8, 6);
        string title = chartEast ? "Amihan sea chart" : "Map of the islands";
        Gfx.Text(title, bx + pad, by + pad, FontKind.Ui700, 28, Pal.PaperInk);
        // What's going on today: the weather, and the derby if it's on.
        string news = state.weather == "storm" ? $"Storm until {HourText((int)StormEnds())}" : state.weather == "rain" ? "Raining" : "Fair weather";
        if (DerbyOn) news += $"   ·   Derby: {(int)derbyT / 60}:{(int)derbyT % 60:00} left";
        Gfx.Text(news, bx + pad + Gfx.Measure(title, FontKind.Ui700, 28) + 24, by + pad + 8, FontKind.Ui600, 17, Muted);
        string other = chartEast ? "Saltmere" : "Amihan";
        if (SmallButton(other, bx + bw - pad - SmallW("Close") - SmallW(other) - 14, by + pad - 4)) { chartEast = !chartEast; return; }
        if (SmallButton("Close", bx + bw - pad - SmallW("Close"), by + pad - 4)) { ClosePanels(); return; }
        float mx = bx + pad, my = by + pad + head;
        Gfx.Rect(mx - 2, my - 2, mw + 4, mh + 4, Pal.Ink);
        DrawTexturePro(mapTex, new Rectangle(origin, 0, chartW, chartH), Gfx.S(mx, my, mw, mh), Vector2.Zero, 0, Color.White);
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
        foreach (var (region, name, wx, wy) in isles)
        {
            if (!OnChart(wx, wy)) continue;
            var p = M(wx, wy);
            marks.Add(new(Charted(region) ? name : "Uncharted island", p.X, p.Y, white, Charted(region) ? white : Pal.C("#f1e6c8"), 22, false, null, 0));
        }
        // Daang Pulo's islets share one name, with how many you've charted.
        if (OnChart(0, 0))
        {
            var p = M(68 * T, 74.5f * T);
            int n = IsletsCharted;
            marks.Add(new(n == 0 ? "Uncharted islets" : $"Daang Pulo ({n} of {IsletCount} islets)", p.X, p.Y, white, n > 0 ? white : Pal.C("#f1e6c8"), n > 0 ? 20 : 22, false, null, 0));
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
        if (Charted("frost")) Place("Frostfang Caverns", MouthDoorX, MouthDoorY - 26, new() { "Frostfang Caverns", state.caveDeepest > 0 ? $"Deepest floor reached: {state.caveDeepest}" : "Twelve floors down, they say." });
        Place("Pip's stall", PipX, PipY - 6, new() { "Pip's stall", PipOpen ? $"Open until {HourText(21 * 60)}" : $"Closed. Opens at {HourText(7 * 60)}" });
        Place("Pip's jetty", SaltJettyX, SaltJettyY, new() { "Pip's jetty", Has("boat") > 0 ? "E sails to Starfall Atoll; F takes the helm" : "You'll need a boat" });
        if (chartEast)
        {
            Place("Village landing", 1495, 217, new() { "Village landing", "Moor here for Amihan Village" });
            foreach (var n in Islanders)
                if (Charted(RegionOf(n.x, n.y + 10))) Place(n.name, n.x, n.y, new() { n.name, IslanderNote(n.id) });
            // The sanctuary's name sits in its middle, and Bantay Joy on her platform.
            var sp = M(SanctCX * T, (SanctCY + SanctRY + 1.2f) * T);
            marks.Add(new("Marine sanctuary", sp.X, sp.Y, white, Pal.C("#bff4ff"), 20, false, null, 0));
            Place("Bantay Joy", JoyX, JoyY, new() { "Bantay Joy, the sea warden", "No fishing or traps inside the buoys", $"Seen: {SeaKinds.Keys.Count(k => state.sightings.GetValueOrDefault(k) > 0)} of 3 animals" });
        }
        else if (state.Hinted("habagat"))
        {
            Place("Asinan landing", AsinanJettyX, AsinanJettyY - 16, new() { "Asinan landing", Has("boat") > 0 ? "E sails from here; F takes the helm" : "You'll need a boat" });
            foreach (var n in HabagatFolk)
                if (Charted(RegionOf(n.x, n.y + 10))) Place(n.name, n.x, n.y, new() { n.name, HabagatNote(n.id) });
            if (race != null || raceArmed)
                for (int i = 0; i < RaceGates.Length; i++)
                {
                    bool next = race != null ? (race.Next < RaceGates.Length ? race.Next : 0) == i : i == 0;
                    Place(i == 0 ? "Start" : $"Gate {i}", RaceGates[i].x, RaceGates[i].y, new() { i == 0 ? "Start and finish" : $"Regatta gate {i}", next ? "Next!" : "" }, next ? 1 : 4);
                }
        }
        foreach (var b in state.builds.Where(b => b.id == "dryrack" && Rack(b) is RackLoad r && r.fish.Count > 0))
            Place(RackDone(Rack(b)) ? "Rack (dry!)" : "Drying rack", b.x * T + 5, b.y * T + 5, new() { "Your drying rack", RackDone(Rack(b)) ? "The daing is ready" : $"{(int)((DryGoal - Rack(b).dry) / DryRate)} minutes of sun to go" }, 2);
        if (Has("boat") > 0 && !Aboard) { var bp = BoatPosition(); Place("Your boat", bp.x, bp.y, new() { "Your boat", "Moored here. R beside it to board." }, 1); }
        if (state.tamed && !state.riding) Place(Data.MountName, state.mountX, state.mountY - 6, new() { Data.MountName, "Waiting here. R whistles it over from anywhere outdoors." }, 1);
        foreach (var b in state.builds.Where(b => b.id == "crabpot"))
        {
            bool ready = PotReady(b);
            Place(ready ? "Crab pot (ready)" : "Crab pot", b.x * T + 5, b.y * T + 5, new() { "Your crab pot", ready ? "Ready to haul up" : "Soaking. Haul it up tomorrow morning." }, 2);
        }
        if (Wears("echo_sounder") && scene == "world")
            foreach (var sc in schools) Place("Feeding fish", sc.X, sc.Y, new() { "Feeding fish", "Your echo sounder hears a frenzy here" }, 4);
        if (HasPin) Place("Pin", state.pinX, state.pinY, new() { "Your pin", "Click it to take it away" }, 1);

        // Where you are, or an arrow at the edge if you're off this chart.
        var (youX, youY, youLabel) = scene == "world" ? (player.X, player.Y - 6, "You")
            : scene == "cave" ? (MouthDoorX, MouthDoorY + 2, caveFloor == AncientFloor ? "You (Ancient Floor)" : $"You (floor {caveFloor})")
            : (state.exitX, state.exitY - 9, "You");
        var placed = new List<Rectangle>();
        foreach (var m in marks.Where(m => m.HasDot)) placed.Add(new Rectangle(m.X - 6, m.Y - 6, 12, 12));
        Vector2 you = default;
        bool youOn = OnChart(youX, youY), youEast = youX >= origin + chartW;
        // Off this chart, an arrow at its edge level with you (with its label kept clear of the others).
        string youTxt = youEast ? "You (Amihan sea)" : "You (Saltmere waters)";
        float youTw = Gfx.Measure(youTxt, FontKind.Ui700, 16);
        float arrowY = Math.Clamp(my + youY * scale, my + 20, my + mh - 20), arrowX = youEast ? mx + mw - 14 : mx + 14;
        if (youOn) { you = M(youX, youY); placed.Add(new Rectangle(you.X - 10, you.Y - 10, 20, 20)); }
        else placed.Add(new Rectangle(youEast ? arrowX - 20 - youTw : arrowX - 14, arrowY - 14, youTw + 34, 28));

        // Dots first, then the labels placed so they don't overlap (with a short line when one has to move away).
        foreach (var m in marks.Where(m => m.HasDot))
        {
            Gfx.Circle(m.X, m.Y, m.Label == "Pin" ? 7 : 6.5f, Pal.Ink);
            Gfx.Circle(m.X, m.Y, m.Label == "Pin" ? 5.5f : 5, m.Label == "Pin" ? Pal.C("#e04b3a") : m.Dot);
        }
        var chart = new Rectangle(mx, my, mw, mh);
        foreach (var m in marks.OrderBy(m => m.Prio))
        {
            var font = m.Size >= 20 ? FontKind.Ui700 : FontKind.Ui600;
            float w = Gfx.Measure(m.Label, font, m.Size), h = m.Size + 4;
            var spots = m.HasDot
                ? new (float x, float y)[] { (-w / 2, 7), (-w / 2, -h - 6), (9, -h / 2), (-w - 9, -h / 2), (-w / 2, 22), (-w / 2, -h - 20), (24, -h / 2), (-w - 24, -h / 2), (12, 12), (-w - 12, -h - 10) }
                : new (float x, float y)[] { (-w / 2, 0), (-w / 2, -24), (-w / 2, 24), (-w / 2, -44), (-w / 2, 44) };
            int pick = -1;
            for (int i = 0; i < spots.Length && pick < 0; i++)
            {
                var r = new Rectangle(m.X + spots[i].x, m.Y + spots[i].y, w, h);
                if (r.X < chart.X || r.Y < chart.Y || r.X + r.Width > chart.X + chart.Width || r.Y + r.Height > chart.Y + chart.Height) continue;
                if (!placed.Any(o => CheckCollisionRecs(o, r))) pick = i;
            }
            if (pick < 0) pick = 0;
            var at = new Rectangle(m.X + spots[pick].x, m.Y + spots[pick].y, w, h);
            placed.Add(at);
            if (m.HasDot && pick >= 4)
            {
                float lx = Math.Clamp(m.X, at.X, at.X + at.Width), ly = Math.Clamp(m.Y, at.Y, at.Y + at.Height);
                Gfx.Line(m.X, m.Y, lx, ly, 1.5f, Pal.WithAlpha(m.Text, 0.7f));
            }
            Shadowed(m.Label, at.X, at.Y, font, m.Size, m.Text);
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
        else if (Gfx.Hover(mx, my, mw, mh)) Gfx.Text("Click to drop a pin", mx + 10, my + mh - 26, FontKind.Ui600, 15, Pal.C("rgba(241,230,200,0.8)"));
        if (Gfx.PressedOutside(bx, by, bw, bh)) ClosePanels();
    }

    // What's at a spot: whether it's open, how much of it you've caught, and what's biting there now.
    List<string> SpotTip(Spot s)
    {
        var tip = new List<string> { s.Label };
        if (!SpotOpen(s.Id)) tip.Add(s.Id == "wreck" ? "Closed: the tide is in" : s.Id == "deep" ? "Closed: the old dock needs repairing" : "Closed");
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
        "niko" => "Tends the nets; has a job for you",
        "maya" => "Studies the karst lagoon",
        "tala" => "Watches the mangroves' wildlife",
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
