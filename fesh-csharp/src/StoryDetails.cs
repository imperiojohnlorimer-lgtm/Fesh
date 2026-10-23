using Raylib_cs;

namespace Fesh;

// Story details (1.45.1, graphics plan item 15): small looks-only things that make the villages feel lived in. Nothing here
// blocks you or changes a map tile; each is placed once, from where the people live, on ground you could stand on and clear
// of everything solid (the autotest checks every one):
//  laundry   a line between two poles with garments swaying in the gusts (steady under Reduce motion)
//  net       a fishing net hung out to dry on poles, with a few fish drying on a line, at Luntian and Asinan
//  boat      a banca hauled up on the sand by each landing, with its outriggers laid beside it
//  smoke     a thread of smoke from a house at mealtimes (06:30-08:00, 11:30-13:00, 17:30-19:30), leaning with the wind
//  kite      a child flying a kite over the village square on clear afternoons (14:00-18:00)
sealed record Detail(string Kind, float X, float Y, float X2 = 0, float Y2 = 0, int Seed = 0);

partial class Game
{
    List<Detail> storyDetails;
    char[,] storyDetailsFor;
    int storyDetailsBuilds = -1;   // how many pieces you had built when they were placed: building near one places them again
    internal int smokeDrawn, kitesDrawn;   // what was drawn last frame (the autotest)

    // Each detail's place, found once per world: from an anchor, the first candidate offset that stands clear.
    List<Detail> StoryDetailList()
    {
        if (storyDetails != null && storyDetailsFor == worldMap && storyDetailsBuilds == state.builds.Count) return storyDetails;
        storyDetailsBuilds = state.builds.Count;
        storyDetailsFor = worldMap;
        var list = storyDetails = new List<Detail>();
        var solids = Solids();
        bool Ok(float x, float y) => CanStandBare(x, y, solids) && !IsWater(x, y) && !HarbourBoards.Any(b => Dist(b.x, b.y, x, y) < 22) && !InteractionPoints().Any(p => Dist(p.x, p.y, x, y) < 14);
        bool Span(float x1, float y1, float x2, float y2)
        {
            for (int i = 0; i <= 4; i++) if (!Ok(x1 + (x2 - x1) * i / 4f, y1 + (y2 - y1) * i / 4f)) return false;
            return true;
        }
        int seed = 0;
        // Poles must stand clear all the way up, not only at their feet.
        bool Pole(float x, float y) => Ok(x, y) && !solids.Any(s => s.Overlaps(new Box(x - 2, y - 14, 4, 13)));
        bool Line(float x1, float y1, float x2, float y2)
        {
            if (!Pole(x1, y1) || !Pole(x2, y2)) return false;
            for (int i = 1; i < 4; i++) if (!Ok(x1 + (x2 - x1) * i / 4f, y1 + (y2 - y1) * i / 4f)) return false;
            return true;
        }
        // Spots round an anchor, nearest first, never in front of its door (at least `min` px off).
        IEnumerable<(float x, float y)> Round(float ax, float ay, float min, float max)
        {
            var c = new List<(float x, float y, float d)>();
            for (float dy = -max; dy <= max; dy += 4)
                for (float dx = -max; dx <= max; dx += 4)
                {
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d >= min && d <= max && dy >= -4) c.Add((ax + dx, ay + dy, d));   // beside or in front: a house hides what is behind it
                }
            return c.OrderBy(p => p.d).ThenBy(p => p.x).ThenBy(p => p.y).Select(p => (p.x, p.y));
        }
        void Pair(string kind, float ax, float ay, float gap, float min)
        {
            foreach (var (x, y) in Round(ax, ay, min, 90))
                if (Line(x, y, x + gap, y)) { list.Add(new Detail(kind, x, y, x + gap, y, seed++)); return; }
        }
        foreach (var (ax, ay) in new[] { (1595f, 185f), (1715f, 187f), (2045f, 127f), (1895f, 597f), (225f, 642f) }) Pair("laundry", ax, ay, 26, 30);
        foreach (var f in MlFolk.Where(f => f.id is "lorna" or "nita")) Pair("laundry", MlWx(f.lx), MlWy(f.ly), 26, 30);
        // Nets dry where the fishing is: the karst lagoon and Asinan's salt beds, a little further from the houses than the washing.
        foreach (var (ax, ay) in new[] { (2045f, 127f), (225f, 642f) }) Pair("net", ax, ay, 24, 46);
        // A hauled-up banca by each landing: the nearest sandy spot where the hull fits.
        foreach (var (lx, ly) in new[] { (1495f, 217f), (AsinanJettyX, AsinanJettyY), (MlJettyX, MlJettyY) })
            foreach (var (bx, by) in Round(lx, ly, 14, 70))
                if (TileAt((int)(bx / T), (int)(by / T)) == 's' && Ok(bx, by) && Ok(bx - 10, by) && Ok(bx + 10, by) && Ok(bx, by - 4))
                {
                    list.Add(new Detail("boat", bx, by, 0, 0, seed++));
                    break;
                }
        // Smoke through the thatch of the houses of Amihan Village, Asinan and Dado's.
        foreach (var (ax, ay) in new[] { (1595f, 185f), (1715f, 187f), (225f, 642f), (655f, 617f) }) list.Add(new Detail("smoke", ax + 2, ay - 46, 0, 0, seed++));
        // A kite over the village square, its child on the open ground.
        foreach (var dx in new[] { 0f, -34f, 34f, -60f })
            if (Ok(RondallaX + dx, RondallaY + 4)) { list.Add(new Detail("kite", RondallaX + dx, RondallaY + 4, 0, 0, seed++)); break; }
        return list;
    }

    bool MealTime => !Night && (state.clock is >= 390 and < 480 || state.clock is >= 690 and < 780 || state.clock is >= 1050 and < 1170);
    bool KiteWeather => !Night && state.clock is >= 14 * 60 and < 18 * 60 && state.weather == "clear";

    void AddStoryDetails(List<(float y, Action draw)> list)
    {
        if (scene != "world" || MatchActive || state?.created != true) return;
        smokeDrawn = kitesDrawn = 0;
        foreach (var d in StoryDetailList())
        {
            if (d.X < camX - 60 || d.X > camX + W + 60 || d.Y < camY - 90 || d.Y > camY + H + 60) continue;
            var det = d;
            switch (d.Kind)
            {
                case "laundry": list.Add((d.Y, () => DrawLaundry(det))); break;
                case "net": list.Add((d.Y, () => DrawDryingNet(det))); break;
                case "boat": list.Add((d.Y, () => DrawHauledBoat(det))); break;
                case "smoke": if (MealTime && !Stormy) list.Add((d.Y + 70, () => DrawChimneySmoke(det))); break;
                case "kite": if (KiteWeather) list.Add((d.Y, () => DrawKiteFlyer(det))); break;
            }
        }
    }

    float DetailSway(Detail d, float k) => Calm ? 0 : MathF.Sin(time * 1.7f + d.Seed * 1.3f + d.X * 0.05f) * k * (0.5f + MathF.Min(1, Gust(d.X, d.Y)));

    void DrawLaundry(Detail d)
    {
        int x1 = (int)d.X, x2 = (int)d.X2, y = (int)d.Y;
        pix.Rect(x1 - 1, y - 14, 2, 15, "#6b4a2e"); pix.Rect(x2 - 1, y - 14, 2, 15, "#6b4a2e");
        pix.Rect(x1 - 1, y - 1, 3, 1, "rgba(0,0,0,0.2)"); pix.Rect(x2 - 1, y - 1, 3, 1, "rgba(0,0,0,0.2)");
        // The line sags a little between the poles; each garment hangs from it and sways.
        int span = x2 - x1;
        for (int i = 0; i <= span; i++) pix.Rect(x1 + i, y - 13 + (int)MathF.Round(MathF.Sin(i / (float)span * MathF.PI) * 2), 1, 1, "#cfc6b0");
        string[] cols = { "#e04b3a", "#f3eedd", "#3f7fd0", "#f2c94a" };
        for (int g = 0; g < 4; g++)
        {
            int gx = x1 + 3 + g * (span - 6) / 3, gy = y - 12 + (int)MathF.Round(MathF.Sin((gx - x1) / (float)span * MathF.PI) * 2);
            float sw = DetailSway(d, 1.7f + g * 0.2f);
            int len = g % 2 == 0 ? 5 : 4;
            pix.Rect(gx + (int)MathF.Round(sw * 0.4f), gy, 3, 1, cols[g]);
            pix.Rect(gx + (int)MathF.Round(sw), gy + 1, 3, len - 1, cols[g]);
            pix.Rect(gx + (int)MathF.Round(sw), gy + len, 3, 1, Shade(cols[g], 0.8f));
        }
    }

    void DrawDryingNet(Detail d)
    {
        int x1 = (int)d.X, x2 = (int)d.X2, y = (int)d.Y;
        pix.Rect(x1 - 1, y - 16, 2, 17, "#6b4a2e"); pix.Rect(x2 - 1, y - 16, 2, 17, "#6b4a2e");
        pix.Rect(x1, y - 16, x2 - x1, 1, "#7d5a38");
        // The net hangs in diamonds: a pale grid with the odd float, sagging in the middle and stirring a little.
        for (int j = 0; j < 9; j++)
        {
            float sag = MathF.Sin((j + 1) / 10f * MathF.PI) * 1.5f;
            int sw = (int)MathF.Round(DetailSway(d, 1.3f) * j / 8f);
            for (int i = 0; i <= x2 - x1; i++)
                if ((i + j) % 3 != 0) pix.Rect(x1 + i + sw, y - 15 + j + (int)sag * 0, 1, 1, (i + j) % 2 == 0 ? "#8fa3ad" : "#6f8590");
        }
        pix.Rect(x1 + 5, y - 12, 2, 2, "#e0584a"); pix.Rect(x2 - 6, y - 8, 2, 2, "#f3c25b");
        // A line of fish drying beside it.
        int fy = y - 8, fx = x2 + 5;
        pix.Rect(fx, fy - 7, 1, 8, "#6b4a2e"); pix.Rect(fx + 12, fy - 7, 1, 8, "#6b4a2e"); pix.Rect(fx, fy - 7, 13, 1, "#7d5a38");
        for (int f = 0; f < 3; f++)
        {
            int px = fx + 2 + f * 4, sw = (int)MathF.Round(DetailSway(d, 1.1f + f * 0.2f));
            pix.Rect(px + sw, fy - 6, 1, 1, "#7d5a38"); pix.Rect(px + sw - 1, fy - 5, 3, 3, "#c9d0d4"); pix.Rect(px + sw, fy - 2, 1, 1, "#8a949a");
        }
    }

    void DrawHauledBoat(Detail d)
    {
        int x = (int)d.X, y = (int)d.Y;
        pix.Rect(x - 10, y, 21, 2, "rgba(0,0,0,0.2)");
        // The hull on its side-skids: planks, a dark keel line, a lit gunwale, the bow and stern lifted.
        for (int i = -9; i <= 9; i++)
        {
            int lift = Math.Abs(i) >= 8 ? -2 : Math.Abs(i) >= 6 ? -1 : 0;
            pix.Rect(x + i, y - 3 + lift, 1, 4, i % 3 == 0 ? "#5a3d24" : "#7a5434");
            pix.Rect(x + i, y - 3 + lift, 1, 1, "#9a7048");
        }
        pix.Rect(x - 6, y + 1, 13, 1, "#4a3220");
        pix.Rect(x - 3, y - 3, 1, 1, "#e9e0c6"); pix.Rect(x + 3, y - 3, 1, 1, "#e9e0c6");
        // The outriggers laid on the sand beside it: two bamboo booms and their floats.
        pix.Rect(x - 12, y + 3, 25, 1, "#b2a15c"); pix.Rect(x - 11, y + 5, 23, 1, "#a8975a");
        pix.Rect(x - 14, y + 2, 3, 4, "#c9b070"); pix.Rect(x + 12, y + 2, 3, 4, "#c9b070");
    }

    // Smoke rising from a chimney, a puff at a time: it swells, thins and leans with the wind (steady under Reduce motion).
    void DrawChimneySmoke(Detail d)
    {
        smokeDrawn++;
        float lean = Calm ? 0.3f : Gust(d.X, d.Y) * 0.8f + 0.2f;
        for (int i = 0; i < 6; i++)
        {
            float ph = Calm ? i / 6f : (time * 0.28f + i / 6f + d.Seed * 0.13f) % 1;
            float x = d.X + MathF.Sin(ph * 4 + i * 1.7f) * 1.3f + ph * ph * 9 * lean, y = d.Y - ph * 22;
            int s = 2 + (int)(ph * 3);
            float a = 0.85f * (1 - ph * ph);
            pix.Rect(x - s / 2f + 1, y - s / 2f + 1, s, s, Pal.Rgba(110, 110, 125, 0.35f * a));   // a soft shadow under each puff, so it reads on pale thatch
            pix.Rect(x - s / 2f, y - s / 2f, s, s, Pal.Rgba(240, 240, 238, a));
        }
    }

    // A child on the open ground holding a kite string up; the kite rides the wind above, its tail trailing.
    void DrawKiteFlyer(Detail d)
    {
        kitesDrawn++;
        int x = (int)d.X, y = (int)d.Y;
        DrawKid((a, b, c, e, col) => pix.Rect(a, b, c, e, col), x, y, d.Seed, 1, time, true);
        float bob = Calm ? 0 : MathF.Sin(time * 1.4f) * 2.5f, sway = Calm ? 0 : MathF.Sin(time * 0.9f + 1) * 4;
        float kx = x + 24 + sway, ky = y - 44 + bob;
        // The string, sagging a little, from the hand to the kite.
        for (int i = 0; i <= 24; i++)
        {
            float u = i / 24f;
            pix.Rect(x + 2 + (kx - x - 2) * u, y - 8 + (ky - y + 8) * u + MathF.Sin(u * MathF.PI) * 3, 1, 1, Pal.Rgba(240, 236, 220, 0.75f));
        }
        int X = (int)kx, Y = (int)ky;
        for (int i = 0; i < 5; i++) { pix.Rect(X - i, Y - 4 + i, 2 * i + 1, 1, i % 2 == 0 ? "#e0584a" : "#f3c25b"); pix.Rect(X - i, Y + 4 - i, 2 * i + 1, 1, i % 2 == 0 ? "#3f7fd0" : "#f3c25b"); }
        pix.Rect(X, Y - 4, 1, 9, "#8a6038");
        for (int i = 0; i < 9; i++) pix.Rect(X + (Calm ? 0 : MathF.Sin(time * 3 + i * 0.8f) * 2) + i * 0.4f, Y + 5 + i, 1, 1, i % 2 == 0 ? "#e0584a" : "#4a90a8");
    }
}
