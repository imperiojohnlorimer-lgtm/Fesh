using Raylib_cs;

namespace Fesh;

// Marilaya's places and people, drawn and solid (Marilaya.cs has the island, MarilayaStory.cs the story): Aling Sela's
// house above the harbour and the survey stone on its headland, the river town with its square (the shore model and the
// kalutang practice), the butterfly nursery, the hot springs at Tindig's foot with their gauges and the dryer, the trail
// and the summit, the limestone exposure and the cave mouth in the north-east hills, the tide post and the sandbar off
// Maniwaya, the arrowroot plots, the mark on Baltasar, and the trail signs that go up as each stop is done. Looks use
// hash and time only (no rng).
partial class Game
{
    // The town's houses: where they stand, and their trim.
    static readonly (float x, float y, string trim)[] MlHouses =
        new (float lx, float ly, string trim)[] { (2.9f, 10.2f, "#3f8fb0"), (6.3f, 10.0f, "#c0392b"), (3.2f, 13.6f, "#e6a843"), (6.6f, 12.0f, "#5a8a4a") }
            .Select(h => (MlWx(h.lx), MlWy(h.ly), h.trim)).ToArray();
    const float NurseryW = 46, NurseryH = 22;
    // The shore model's table in the town square, the dryer shed and the two gauges at the springs, the cliff's face and the
    // cave mouth (pixels, from MlPlaces).
    static (float x, float y) ModelAt => (MlPlace("plaza").x + 14, MlPlace("plaza").y - 6);
    static (float x, float y) SpringGaugeAt => (MlPlace("springs").x + 18, MlPlace("springs").y + 4);
    static (float x, float y) StreamGaugeAt => MlPlace("streamgauge");
    static (float x, float y) DryerAt => MlPlace("dryer");
    static (float x, float y) CairnAt => (TindigX + 14, TindigY - 4);
    static (float x, float y) MirrorAt => (TindigX - 16, TindigY + 2);
    static (float x, float y) TargetAt => (BaltasarX + 7, BaltasarY - 2);
    // Where a notice about Aling Sela is pinned up on Amihan Village's landing (the way the story is first heard of).
    const float NoticeX = 1572, NoticeY = 205;

    // Everything solid on Marilaya (and the notice board). It never moves, so it's made once, after the island is made.
    List<Box> mlSolids;
    List<Box> MlSolids() => mlSolids ??= MakeMlSolids();
    List<Box> MakeMlSolids()
    {
        var list = new List<Box> { new(SelaX - 16, SelaY - 22, 32, 19), new(DatumX - 3, DatumY - 3, 7, 3) };
        list.AddRange(MlHouses.Select(h => new Box(h.x - 16, h.y - 22, 32, 19)));
        var (gx, gy) = MlPlace("garden");
        list.Add(new Box(gx - NurseryW / 2, gy - NurseryH, NurseryW, NurseryH - 2));
        var (sx, sy) = MlPlace("springs");
        list.Add(new Box(sx - 16, sy - 9, 30, 11));
        list.Add(new Box(DryerAt.x - 12, DryerAt.y - 14, 24, 13));
        list.Add(new Box(SpringGaugeAt.x - 1, SpringGaugeAt.y - 2, 3, 2));
        list.Add(new Box(StreamGaugeAt.x - 1, StreamGaugeAt.y - 2, 3, 2));
        var (fx, fy) = MlPlace("cliff");
        list.Add(new Box(fx - 36, fy - 10, 72, 10));
        var (cx, cy) = MlPlace("cave");
        list.Add(new Box(cx - 20, cy - 9, 13, 9)); list.Add(new Box(cx + 8, cy - 9, 13, 9));
        list.Add(new Box(ModelAt.x - 13, ModelAt.y - 5, 26, 6));
        list.Add(new Box(CairnAt.x - 3, CairnAt.y - 3, 7, 3));
        list.Add(new Box(TargetAt.x - 1, TargetAt.y - 2, 3, 2));
        list.Add(new Box(BaltasarX - 2, BaltasarY - 2, 5, 2));
        return list;
    }

    /* ---------- The people ---------- */
    // Aling Sela, a retired surveyor who fishes; Jun, her grandson (up Tindig with the mirror for the survey); Mang Ambo,
    // who knows the sandbar; Lorna, who looks after the hot springs and the dryer; Mang Iyo, who teaches the kalutang in
    // the town square; and Aling Nita, who raises butterflies.
    static readonly (string id, string name, float lx, float ly)[] MlFolk =
    {
        ("sela", "Aling Sela", 5.9f, 4.95f), ("jun", "Jun", 15.0f, 25.9f), ("ambo", "Mang Ambo", 25.6f, 4.0f), ("lorna", "Lorna", 9.3f, 23.9f),
        ("iyo", "Mang Iyo", 4.2f, 11.3f), ("nita", "Aling Nita", 5.9f, 20.4f)
    };
    readonly Dictionary<string, Stroller> mlWalk = new();
    Stroller MlWalk(string id)
    {
        if (mlWalk.TryGetValue(id, out var s)) return s;
        var f = MlFolk.First(f => f.id == id);
        float x = MlWx(f.lx), y = MlWy(f.ly);
        return mlWalk[id] = new Stroller { HomeX = x, HomeY = y, X = x, Y = y, RangeX = id == "jun" ? 6 : 12, RangeDown = 7, Notice = 26 };
    }
    Stroller SelaWalk() => MlWalk("sela");
    // Jun is only about once the survey's in hand (MarilayaStory.cs), up at the summit with the mirror.
    bool MlFolkHere(string id) => id != "jun" || MrSorted && !MrDone || MrDone && false;

    static void DrawSela(Pix p, int x, int y, string face, int step, int bob = 0)
    {
        LookData.DrawFigure(p, "#c68a5a", "#e8e8e8", "#3f6f9a", "#4a4038", true, 1, null, x, y, face, step, bob: bob);
        if (face != "up") p.Rect(x + (face == "left" ? -3 : 1), y - 7 - bob, 2, 2, "#f2e6c8");
    }

    void DrawMlPerson(string id, int x, int y, string face, int step, int bob)
    {
        switch (id)
        {
            case "sela": DrawSela(pix, x, y, face, step, bob); break;
            case "jun": LookData.DrawFigure(pix, "#b97a4e", "#2b1d14", "#e6a843", "#2f4a6a", false, 3, "#c0392b", x, y, face, step, bob: bob); break;
            case "ambo": LookData.DrawFigure(pix, "#a86f45", "#5a5048", "#6f8f5a", "#3f4a5a", false, 1, null, x, y, face, step, bob: bob); break;
            case "lorna": LookData.DrawFigure(pix, "#c68a5a", "#2b1d14", "#f2efe6", "#2f5d8a", true, 0, null, x, y, face, step, bob: bob); break;
            case "iyo": LookData.DrawFigure(pix, "#b97a4e", "#cfcac0", "#8a5fb5", "#3f4a5a", false, 0, null, x, y, face, step, bob: bob); break;
            case "nita": LookData.DrawFigure(pix, "#c68a5a", "#3a2a1e", "#e88ab0", "#4a4038", true, 1, null, x, y, face, step, bob: bob); break;
        }
        // Mang Iyo holds his pair of kalutang bars.
        if (id == "iyo" && face != "up") { pix.Rect(x - 5, y - 8, 1, 5, "#8a5a32"); pix.Rect(x + 4, y - 8, 1, 5, "#6b4424"); }
    }

    void UpdateMarilayaFolk(float dt)
    {
        if (scene != "world" || mlGather != null || player.X < (MlStart - 30) * T) return;
        PlaceAmbo();
        foreach (var f in MlFolk)
        {
            if (!MlFolkHere(f.id)) continue;
            var s = MlWalk(f.id);
            if (Dist(s.X, s.Y, player.X, player.Y) < 400) Stroll(s, dt, (x, y) => FolkCanStand(x, y));
        }
    }

    /* ---------- Drawing ---------- */
    void AddMarilayaObjects(List<(float y, Action draw)> list, bool live = true)
    {
        // The notice on Amihan Village's landing.
        if (!live || Dist(player.X, player.Y, NoticeX, NoticeY) < 260) list.Add((NoticeY, DrawSelaNotice));
        if (live && player.X < (MlStart - 20) * T) return;
        list.Add((SelaY - 3, () => DrawBahay(SelaX, SelaY - 5, "#3f6f9a")));
        foreach (var (hx, hy, trim) in MlHouses) list.Add((hy - 3, () => DrawBahay(hx, hy - 5, trim)));
        list.Add((DatumY, DrawDatumStone));
        list.Add((BaltasarY, DrawBaltasar));
        list.Add((MlPlace("garden").y, DrawNursery));
        list.Add((TindigY - 6, DrawTindigTop));
        list.Add((CairnAt.y, DrawCairn));
        if (state.Hinted("mr:mirror") && !MrDone || MrDone) list.Add((MirrorAt.y, DrawHeliotrope));
        list.Add((MlPlace("springs").y, DrawSprings));
        list.Add((DryerAt.y, DrawDryer));
        list.Add((SpringGaugeAt.y, () => DrawGauge(SpringGaugeAt.x, SpringGaugeAt.y, true)));
        list.Add((StreamGaugeAt.y, () => DrawGauge(StreamGaugeAt.x, StreamGaugeAt.y, false)));
        list.Add((MlPlace("cliff").y, DrawCliff));
        list.Add((MlPlace("cave").y, DrawLimestoneCaveMouth));
        list.Add((tidePostY, DrawTidePost));
        list.Add((MlPlace("arrowroot").y - 6, DrawArrowroot));
        list.Add((ModelAt.y, DrawModelTable));
        foreach (var (x, y, done) in TrailSigns()) if (done || !live) list.Add((y, () => DrawTrailSign(x, y)));
        if (!live) return;
        if (mlGather == null)
            foreach (var f in MlFolk)
            {
                if (!MlFolkHere(f.id)) continue;
                var s = MlWalk(f.id);
                list.Add((s.Y, () => DrawMlPerson(f.id, (int)MathF.Round(s.X), (int)MathF.Round(s.Y), s.Face, s.Step, s.Moving ? 0 : (int)(time % 3 / 2))));
            }
        else AddGathering(list);
        AddKalutangKids(list);
    }

    // The discovery trail's signs: one at each stop, put up once that stop is done (Sela's trail for the island's children).
    IEnumerable<(float x, float y, bool done)> TrailSigns()
    {
        yield return (MlPlace("cliff").x - 44, MlPlace("cliff").y + 4, state.Hinted("mr:layers"));
        yield return (MlPlace("cave").x + 26, MlPlace("cave").y + 4, state.Hinted("mr:karst"));
        yield return (MlPlace("springs").x - 22, MlPlace("springs").y + 8, state.Hinted("mr:springs"));
        yield return (MlPlace("barroot").x + 10, MlPlace("barroot").y - 6, state.Hinted("mr:tide"));
        yield return (CairnAt.x + 10, CairnAt.y + 6, MrDone);
        yield return (MlPlace("trailhead").x - 8, MlPlace("trailhead").y - 2, MrDone);
    }

    void DrawTrailSign(float sx, float sy)
    {
        int x = (int)sx, y = (int)sy;
        pix.Rect(x - 4, y - 1, 9, 2, "rgba(0,0,0,0.16)");
        pix.Rect(x, y - 9, 1, 9, "#65452e");
        pix.Rect(x - 4, y - 12, 9, 5, "#d8c7a0");
        pix.Rect(x - 4, y - 12, 9, 1, "#d46a7e");
        pix.Rect(x - 3, y - 10, 6, 1, "#8a6a48"); pix.Rect(x - 3, y - 8, 4, 1, "#8a6a48");
    }

    // A notice on the post at Amihan Village's landing: Aling Sela is looking for a field partner with a boat.
    void DrawSelaNotice()
    {
        int x = (int)NoticeX, y = (int)NoticeY;
        pix.Rect(x - 6, y - 1, 13, 2, "rgba(0,0,0,0.16)");
        pix.Rect(x - 5, y - 12, 1, 12, "#65452e"); pix.Rect(x + 5, y - 12, 1, 12, "#65452e");
        pix.Rect(x - 6, y - 15, 13, 8, "#8a6a48");
        pix.Rect(x - 4, y - 14, 5, 6, "#f2efe6"); pix.Rect(x + 1, y - 13, 4, 4, "#f6e7b0");
        pix.Rect(x - 3, y - 12, 3, 1, "#8a8a8a"); pix.Rect(x - 3, y - 10, 2, 1, "#8a8a8a");
        // A heart drawn on Sela's note.
        pix.Rect(x + 2, y - 12, 1, 1, "#d46a7e"); pix.Rect(x + 4, y - 12, 1, 1, "#d46a7e"); pix.Rect(x + 2, y - 11, 3, 1, "#d46a7e"); pix.Rect(x + 3, y - 10, 1, 1, "#d46a7e");
    }

    // The survey stone: a squat concrete pillar on a little mound, with a brass disc on top.
    void DrawDatumStone()
    {
        int x = (int)DatumX, y = (int)DatumY;
        pix.Rect(x - 8, y - 1, 17, 2, "rgba(0,0,0,0.18)");
        pix.Rect(x - 6, y - 2, 13, 2, "#7aa85a");
        pix.Rect(x - 3, y - 9, 7, 8, "#c9c4b8");
        pix.Rect(x + 2, y - 9, 2, 8, "#a8a398");
        pix.Rect(x - 3, y - 10, 7, 1, "#e2ddd2");
        pix.Rect(x - 1, y - 11, 3, 2, "#d9a43a");
        pix.Rect(x, y - 11, 1, 1, "#f6d27a");
        pix.Rect(x - 2, y - 6, 5, 1, "#8f8a80");
        // Once the chart is finished, a little flag beside it.
        if (MrDone) { pix.Rect(x + 6, y - 14, 1, 13, "#65452e"); pix.Rect(x + 7, y - 14, 6, 4, "#d46a7e"); }
    }

    // Baltasar's survey mark, and the striped target pole beside it that the far stations sight.
    void DrawBaltasar()
    {
        int x = (int)BaltasarX, y = (int)BaltasarY;
        pix.Rect(x - 4, y - 1, 9, 2, "rgba(0,0,0,0.18)");
        pix.Rect(x - 2, y - 6, 5, 5, "#c9c4b8");
        pix.Rect(x + 1, y - 6, 2, 5, "#a8a398");
        pix.Rect(x - 1, y - 7, 3, 1, "#d9a43a");
        var (tx, ty) = TargetAt;
        pix.Rect((int)tx - 2, (int)ty - 1, 5, 2, "rgba(0,0,0,0.18)");
        for (int i = 0; i < 22; i++) pix.Rect((int)tx, (int)ty - 2 - i, 1, 1, i / 4 % 2 == 0 ? "#f2efe6" : "#c0392b");
        pix.Rect((int)tx - 3, (int)ty - 25, 7, 3, "#f2efe6"); pix.Rect((int)tx - 3, (int)ty - 24, 7, 1, "#c0392b");
    }

    // Tindig's top, seen from above: a small old crater in the rock, grassed over inside. (The mountain itself is the
    // land: its forest, the trail and the hill shading, Marilaya.cs.)
    void DrawTindigTop()
    {
        int x0 = (int)TindigX, y0 = (int)TindigY;
        for (int j = -7; j <= 7; j++)
            for (int i = -11; i <= 11; i++)
            {
                float d = i * i / 121f + j * j / 49f;
                if (d > 1) continue;
                string c = d > 0.72f ? (j < 0 ? "#cfc9bc" : "#9d978b") : d > 0.5f ? (j < 0 ? "#8a857a" : "#b3ad9f") : (Pix.Hash(x0 + i, y0 + j, 931) < .5 ? "#6f9a4e" : "#5f8a44");
                pix.Rect(x0 + i, y0 + j, 1, 1, c);
            }
        // A wisp of cloud catching on the summit some days.
        if ((state.day + (int)(time / 40)) % 3 == 0)
        {
            int dx = (int)(MathF.Sin(time * 0.2f) * 3);
            pix.Rect(x0 - 16 + dx, y0 - 16, 20, 2, "rgba(255,255,255,0.5)"); pix.Rect(x0 - 10 + dx, y0 - 18, 12, 2, "rgba(255,255,255,0.4)");
        }
    }

    // The summit cairn, where the survey's third point is marked.
    void DrawCairn()
    {
        var (cx, cy) = CairnAt;
        int x = (int)cx, y = (int)cy;
        pix.Rect(x - 4, y - 1, 9, 2, "rgba(0,0,0,0.18)");
        pix.Rect(x - 3, y - 4, 7, 3, "#8f8a80"); pix.Rect(x - 2, y - 6, 5, 2, "#a8a398"); pix.Rect(x - 1, y - 8, 3, 2, "#c9c4b8");
    }

    // The heliotrope on its tripod: a mirror that throws sunlight to the far stations, flashing by day in clear weather.
    void DrawHeliotrope()
    {
        var (mx, my) = MirrorAt;
        int x = (int)mx, y = (int)my;
        pix.Rect(x - 4, y - 1, 9, 2, "rgba(0,0,0,0.18)");
        pix.Line(x - 3, y, x, y - 7, "#65452e"); pix.Line(x + 3, y, x, y - 7, "#65452e"); pix.Line(x, y, x, y - 7, "#65452e");
        pix.Rect(x - 2, y - 11, 5, 4, "#9fb8c8"); pix.Rect(x - 1, y - 10, 2, 1, "#e8f6fb");
        if (MirrorFlashing && (int)(time * 2.5f) % 3 != 0) { pix.Glow(x, y - 9, 7, Pal.Rgba(255, 250, 220, 0.7f)); pix.Rect(x, y - 10, 1, 1, "#ffffff"); }
    }

    // The hot springs at Tindig's western foot: milky pools behind a rail, steam, and a sign.
    void DrawSprings()
    {
        var (sx, sy) = MlPlace("springs");
        int x = (int)sx, y = (int)sy;
        void Pool(int cx, int cy, int rx, int ry)
        {
            for (int j = -ry - 1; j <= ry + 1; j++)
                for (int i = -rx - 1; i <= rx + 1; i++)
                {
                    float d = i * i / (float)((rx + 1) * (rx + 1)) + j * j / (float)((ry + 1) * (ry + 1));
                    if (d > 1) continue;
                    float din = i * i / (float)(rx * rx) + j * j / (float)(ry * ry);
                    pix.Rect(cx + i, cy + j, 1, 1, din > 1 ? (Pix.Hash(cx + i, cy + j, 3) < .5 ? "#c9c1a2" : "#d8d0b0") : din > 0.55f ? "#8fcfc8" : "#b4e2da");
                }
            pix.Rect(cx - rx / 2, cy - 1, rx / 2, 1, "#d6f2ec");
        }
        Pool(x - 7, y - 4, 6, 2);
        Pool(x + 6, y - 6, 5, 2);
        Pool(x + 1, y, 4, 1);
        for (int i = 0; i < 6; i++)
        {
            float k = (time * 0.5f + i * 0.17f) % 1;
            int px = x - 10 + i * 4 + (int)(MathF.Sin(time * 1.3f + i) * 1.5f), py = y - 5 - (int)(k * 16);
            pix.Rect(px, py, 3, 2, Pal.Rgba(245, 245, 240, 0.5f * (1 - k)));
            pix.Rect(px + 1, py - 1, 1, 1, Pal.Rgba(245, 245, 240, 0.35f * (1 - k)));
        }
        // The rail round them, and the sign.
        pix.Rect(x - 16, y + 2, 30, 1, "#7a5a3a"); pix.Rect(x - 16, y - 9, 30, 1, "#7a5a3a");
        foreach (int px in new[] { -16, -6, 4, 13 }) { pix.Rect(x + px, y - 9, 1, 12, "#65452e"); }
        pix.Rect(x - 25, y - 6, 8, 5, "#d8c7a0"); pix.Rect(x - 22, y - 1, 1, 4, "#65452e");
        pix.Rect(x - 24, y - 5, 6, 1, "#c0392b"); pix.Rect(x - 24, y - 3, 4, 1, "#8a6a48");
        // The pipe from the outlet to the dryer.
        var (dx, dy) = DryerAt;
        for (float t = 0; t <= 1; t += 0.02f) pix.Rect(MathF.Round(x - 8 + (dx + 8 - (x - 8)) * t), MathF.Round(y - 7 + (dy - 4 - (y - 7)) * t), 1, 1, "#6e767b");
    }

    // A gauge on a post: a dial showing how warm the water is (the hot spring's outlet, or the plain stream).
    void DrawGauge(float gx, float gy, bool spring)
    {
        int x = (int)gx, y = (int)gy;
        pix.Rect(x - 2, y - 1, 5, 2, "rgba(0,0,0,0.16)");
        pix.Rect(x, y - 8, 1, 8, "#5e6468");
        pix.Rect(x - 2, y - 12, 5, 5, "#e8e8e2"); pix.Rect(x - 2, y - 12, 5, 1, "#8a8f93");
        pix.Rect(x, y - 10, spring ? 2 : 1, 1, spring ? "#c0392b" : "#2f5d8a");
    }

    // The dryer: a small shed with a cabinet of trays, warmed by hot water from the spring through a coil, never touching
    // the food (DOST's dryer at Malbog in Marinduque works this way).
    void DrawDryer()
    {
        var (dx, dy) = DryerAt;
        int x = (int)dx, y = (int)dy;
        pix.Rect(x - 13, y - 1, 26, 2, "rgba(0,0,0,0.18)");
        pix.Rect(x - 12, y - 14, 24, 13, "#b98b52");
        for (int i = -10; i < 12; i += 4) pix.Rect(x + i, y - 13, 1, 11, "#dfbc78");
        for (int i = 0; i < 6; i++) pix.Rect(x - 14 + i, y - 20 + i, 28 - i * 2, 1, i % 2 == 0 ? "#80643e" : "#ceb36b");
        // The cabinet with its trays, and a little warm glow when it's working.
        pix.Rect(x - 5, y - 11, 10, 9, "#8f969b"); pix.Rect(x - 4, y - 10, 8, 7, "#c7ced2");
        for (int i = 0; i < 3; i++) pix.Rect(x - 4, y - 9 + i * 2, 8, 1, "#9aa0a5");
        if (state.Hinted("mr:springs")) pix.Rect(x - 4, y - 3, 8, 1, (int)(time * 2) % 2 == 0 ? "#e6a843" : "#d0892a");
        if (DryerLoad > 0) for (int i = 0; i < Math.Min(DryerLoad, 3); i++) pix.Rect(x - 3 + i * 3, y - 9, 2, 1, "#c9a06a");
    }

    // The limestone exposure in the north-east hills: dark volcanic rock at the bottom, pale limestone with shells and
    // coral in it above, soil and grass on top, and a long groove along the limestone where waves once cut it: the same
    // shape the sea cuts along the shore today, high and dry. A loose shell lies at its foot.
    void DrawCliff()
    {
        var (fx, fy) = MlPlace("cliff");
        int x = (int)fx - 36, y = (int)fy - 42, w = 72, hgt = 42;
        pix.Rect(x, y + hgt, w, 2, "rgba(0,0,0,0.18)");
        for (int r = 0; r < hgt; r++)
        {
            // A ragged top edge, so it reads as a hillside cut, not a block.
            int inset = r < 4 ? 4 - r + (int)(Pix.Hash(r, (int)fx, 941) * 2) : 0;
            string c = r < 4 ? "#6f9a4e" : r < 8 ? "#8a6a48" : r < 31 ? ((r % 4 == 0) ? "#d2ccb6" : "#e2dcc6") : (r % 2 == 0 ? "#4f4b48" : "#5a5652");
            pix.Rect(x + inset, y + r, w - inset * 2, 1, c);
        }
        // The old wave notch: a groove with an overhang over it.
        pix.Rect(x + 2, y + 13, w - 4, 1, "#f2ecda"); pix.Rect(x + 3, y + 14, w - 6, 3, "#8f8a7e"); pix.Rect(x + 4, y + 17, w - 8, 1, "#a8a296");
        // Shells and a branch of coral set in the limestone.
        foreach (var (sx, sy) in new[] { (7, 22), (19, 26), (33, 21), (50, 27), (28, 10), (60, 20) })
        {
            pix.Rect(x + sx, y + sy, 3, 2, "#f6efe0"); pix.Rect(x + sx + 1, y + sy, 1, 1, "#b8ad94");
        }
        pix.Rect(x + 42, y + 24, 1, 4, "#c49a8a"); pix.Rect(x + 41, y + 24, 3, 1, "#c49a8a");
        pix.Rect(x + w - 5, y + 4, 2, hgt - 4, "rgba(0,0,0,0.12)");
        // A few tufts of grass and a fern clinging to it.
        pix.Rect(x + 12, y + 30, 3, 1, "#5d9b45"); pix.Rect(x + 13, y + 29, 1, 1, "#5d9b45"); pix.Rect(x + 56, y + 11, 3, 1, "#5d9b45");
        // The loose shell in front, and a small sign: look, sketch, leave it.
        pix.Rect((int)fx + 10, (int)fy + 4, 3, 2, "#f8f2e6"); pix.Rect((int)fx + 11, (int)fy + 4, 1, 1, "#d8a890");
        pix.Rect((int)fx + 38, (int)fy - 4, 7, 5, "#d8c7a0"); pix.Rect((int)fx + 41, (int)fy + 1, 1, 3, "#65452e");
        pix.Rect((int)fx + 39, (int)fy - 3, 5, 1, "#8a6a48");
    }

    // The cave mouth in the limestone hill, with the start of its wooden walkway and a lamp.
    void DrawLimestoneCaveMouth()
    {
        var (cx, cy) = MlPlace("cave");
        int x = (int)cx, y = (int)cy;
        for (int r = 0; r < 26; r++)
        {
            int half = 9 + r * 11 / 13;
            pix.Rect(x - half, y - 26 + r, half * 2, 1, r % 5 == 0 ? "#cfc9b4" : "#ddd7c2");
            pix.Rect(x + half - 3, y - 26 + r, 3, 1, "#b6b09c");
        }
        pix.Rect(x - 8, y - 2, 17, 2, "rgba(0,0,0,0.2)");
        pix.Rect(x - 6, y - 15, 12, 15, "#15110f");
        pix.Rect(x - 4, y - 17, 8, 2, "#15110f");
        foreach (int dx in new[] { -4, -1, 2, 4 }) pix.Rect(x + dx, y - 15, 1, 2 + Math.Abs(dx) % 2, "#e8e2cc");
        if ((int)(time * 2) % 3 == 0) pix.Rect(x + 1, y - 12 + (int)(time * 12 % 8), 1, 1, "#9fd0e6");
        // The walkway's first boards, and a lamp on a post.
        for (int i = 0; i < 3; i++) pix.Rect(x - 4, y - 2 - i * 3, 9, 2, i % 2 == 0 ? "#9a6a3a" : "#8a5d32");
        pix.Rect(x + 9, y - 10, 1, 10, "#65452e"); pix.Rect(x + 8, y - 13, 3, 3, "#f3c25b");
        pix.Rect(x - 6, y - 30, 12, 4, "#5d9b45");
    }

    // The shore model on its table in the town square: a tray of sand, water and a rock base (MarilayaUi.cs runs it).
    void DrawModelTable()
    {
        var (mx, my) = ModelAt;
        int x = (int)mx, y = (int)my;
        pix.Rect(x - 13, y, 26, 2, "rgba(0,0,0,0.18)");
        pix.Rect(x - 12, y - 5, 1, 5, "#65452e"); pix.Rect(x + 11, y - 5, 1, 5, "#65452e");
        pix.Rect(x - 13, y - 9, 26, 5, "#8a5d32");
        pix.Rect(x - 12, y - 9, 10, 4, "#e8cf96"); pix.Rect(x - 2, y - 9, 13, 4, "#4ea3bf"); pix.Rect(x - 4, y - 9, 3, 4, "#8f8a80");
        pix.Rect(x + 1, y - 8, 3, 1, "#d6f2ec");
    }

    // Aling Nita's butterfly nursery: a tall net over potted calamansi and flowers, with butterflies inside.
    void DrawNursery()
    {
        var (gx, gy) = MlPlace("garden");
        int x = (int)(gx - NurseryW / 2), y = (int)(gy - NurseryH), w = (int)NurseryW, h = (int)NurseryH;
        pix.Rect(x, y + h - 2, w, 2, "rgba(0,0,0,0.15)");
        pix.Rect(x + 1, y + h - 6, w - 2, 4, "#4f8a3e");
        foreach (var (px, c) in new[] { (4, "#e04b3a"), (11, "#f3c25b"), (18, "#e88ab0"), (32, "#f3c25b"), (40, "#e04b3a") })
        { pix.Rect(x + px, y + h - 9, 2, 3, "#3f7a37"); pix.Rect(x + px - 1, y + h - 10, 3, 2, c); }
        // Potted calamansi: the lime butterfly's caterpillars eat citrus leaves.
        foreach (int px in new[] { 23, 27 }) { pix.Rect(x + px, y + h - 6, 3, 3, "#a8673a"); pix.Rect(x + px - 1, y + h - 11, 5, 5, "#3f7a37"); pix.Rect(x + px, y + h - 9, 1, 1, "#9ccf55"); }
        for (int i = 0; i <= w; i += 4) pix.Rect(x + i, y, 1, h - 2, Pal.Rgba(235, 240, 235, 0.35f));
        for (int j = 0; j < h - 2; j += 4) pix.Rect(x, y + j, w, 1, Pal.Rgba(235, 240, 235, 0.35f));
        foreach (int px in new[] { 0, w / 2, w }) pix.Rect(x + px, y - 1, 1, h - 1, "#7a5a3a");
        pix.Rect(x, y - 1, w + 1, 1, "#7a5a3a");
        for (int i = 0; i < 5; i++)
        {
            float t = time * 0.7f + i * 1.7f;
            int bx = x + 4 + (int)((MathF.Sin(t) * 0.5f + 0.5f) * (w - 8)), byy = y + 3 + (int)((MathF.Cos(t * 1.3f) * 0.5f + 0.5f) * (h - 12));
            bool open = (int)(time * 8 + i) % 2 == 0;
            string c = i % 2 == 0 ? "#f3c25b" : "#2b2420";
            if (open) { pix.Rect(bx - 2, byy, 2, 2, c); pix.Rect(bx + 1, byy, 2, 2, c); }
            else pix.Rect(bx - 1, byy, 3, 2, c);
            pix.Rect(bx, byy, 1, 2, "#2b2420");
        }
    }

    // Arrowroot (uraro) in rows on Mompong: little clumps of arrow-shaped leaves.
    void DrawArrowroot()
    {
        var (ax, ay) = MlPlace("arrowroot");
        for (int r = 0; r < 3; r++)
            for (int i = 0; i < 4; i++)
            {
                int x = (int)ax - 9 + i * 6, y = (int)ay - 4 + r * 4;
                pix.Rect(x, y, 4, 1, "#7a5a3a");
                pix.Rect(x + 1, y - 3, 1, 3, "#4f8a3e");
                pix.Rect(x, y - 4, 3, 1, "#6aab55"); pix.Rect(x + 1, y - 5, 1, 1, "#6aab55");
            }
    }
    /* ---------- The sandbar (Palad's counterpart) ---------- */
    // A bar of sand running out from Maniwaya's west beach, with the tide post at its far end. The sand stands at a height
    // along it (MlBarHeight): high in the middle, a low saddle near the beach, a little lower at the end. Wherever the sea
    // is below the sand it's dry and you can walk it; the saddle floods first when the tide comes back, which is how
    // people get cut off on real sandbars. The bar's tiles are shallows in the map, ringed with deep water so the only
    // way out is along the sand, and no wading across it while it's under (the current runs hard over a bar).
    static readonly (float x, float y)[] MlBar = { (23.95f, 3.5f), (23.1f, 3.15f), (22.15f, 2.8f), (21.2f, 2.65f), (20.45f, 2.8f) };
    const float MlBarHalf = 0.5f;   // half its width, trace units
    readonly Dictionary<(int x, int y), float> sandbar = new();
    float tidePostX, tidePostY;
    // The sea level below which the whole bar joins up with the beach (the low saddle's height, in effect).
    float barCutLevel = -0.44f;

    // Where a point is along the bar (0 the beach end, 1 the post) and how far off its middle line (trace units).
    static (float s, float off) MlBarAt(float lx, float ly)
    {
        float total = 0, best = float.MaxValue, bestS = 0;
        var lens = new float[MlBar.Length];
        for (int i = 1; i < MlBar.Length; i++) { total += Dist(MlBar[i - 1].x, MlBar[i - 1].y, MlBar[i].x, MlBar[i].y); lens[i] = total; }
        for (int i = 1; i < MlBar.Length; i++)
        {
            var (ax, ay) = MlBar[i - 1]; var (bx, by) = MlBar[i];
            float vx = bx - ax, vy = by - ay, t = Math.Clamp(((lx - ax) * vx + (ly - ay) * vy) / (vx * vx + vy * vy), 0, 1);
            float d = Dist(lx, ly, ax + vx * t, ay + vy * t);
            if (d < best) { best = d; bestS = (lens[i - 1] + t * (lens[i] - lens[i - 1])) / total; }
        }
        return (bestS, best);
    }

    // The height of the sand at a point (sea level units: the sea's between -1 and 1), or -9 off the bar.
    static float MlBarHeight(float lx, float ly)
    {
        var (s, off) = MlBarAt(lx, ly);
        if (off > MlBarHalf) return -9;
        float saddle = MathF.Exp(-MathF.Pow((s - 0.26f) / 0.09f, 2));
        float end = Math.Clamp((s - 0.8f) / 0.2f, 0, 1);
        return -0.2f - 0.24f * saddle - 0.1f * end - 0.3f * MathF.Pow(off / MlBarHalf, 2);
    }

    bool OnSandbarTile(int tx, int ty) => scene == "world" && sandbar.ContainsKey((tx, ty));
    bool SandbarDry(int tx, int ty) => scene == "world" && sandbar.TryGetValue((tx, ty), out float h) && SeaLevel <= h;
    // Dry sand you can stand on (CanStand): the bar's tiles while the sea is below them, or one under your feet right
    // now, so the tide coming in can't stop you mid-step (Tides.FlatOk has the same rule).
    bool SandbarOk(int tx, int ty) => SandbarDry(tx, ty) || OnSandbarTile(tx, ty) && UnderFeet(tx, ty);
    // A boat runs aground on the bar while it's dry (BoatCanStand).
    bool SandbarDryAt(float x, float y) => SandbarDry((int)MathF.Floor(x / T), (int)MathF.Floor(y / T));

    void MakeSandbar()
    {
        sandbar.Clear();
        for (int y = 0; y < 30; y++)
            for (int x = MlStart; x < COLS - 1; x++)
            {
                float lx = (x - MlX + 0.5f) / MlS, ly = (y - MlY + 0.5f) / MlS;
                float h = MlBarHeight(lx, ly);
                if (h > -9 && WaterTile(map[y, x])) { sandbar[(x, y)] = h; map[y, x] = 'w'; }
            }
        // Deep water round it, so the bar is the only way along (no wading round the saddle).
        foreach (var (bx, by) in sandbar.Keys.ToList())
            for (int y = by - 2; y <= by + 2; y++)
                for (int x = bx - 2; x <= bx + 2; x++)
                    if (!sandbar.ContainsKey((x, y)) && map[y, x] == 'w') map[y, x] = '~';
        // How low the sea must be for the bar to join the beach all the way out: the highest level at which dry sand still
        // runs from its far end to the shore.
        var endTile = sandbar.Keys.OrderBy(k => Dist(k.x + 0.5f, k.y + 0.5f, MlX + MlBar[^1].x * MlS, MlY + MlBar[^1].y * MlS)).First();
        bool Joined(float level)
        {
            var seen = new HashSet<(int, int)> { endTile };
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(endTile);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (!seen.Add(n)) continue;
                    if (sandbar.TryGetValue(n, out float h)) { if (h >= level) queue.Enqueue(n); }
                    else if (Walkable(map[n.Item2, n.Item1])) return true;
                }
            }
            return false;
        }
        float lo = -1, hi = 0;
        for (int i = 0; i < 20; i++) { float mid = (lo + hi) / 2; if (Joined(mid)) lo = mid; else hi = mid; }
        barCutLevel = lo;
        // The tide post: in the water just off Maniwaya's beach by the bar's root, read from the sand (permanent ground).
        float bx0 = (MlX + 23.75f * MlS), by0 = (MlY + 4.3f * MlS), best = float.MaxValue;
        for (int y = (int)by0 - 4; y <= by0 + 4; y++)
            for (int x = (int)bx0 - 4; x <= bx0 + 4; x++)
            {
                if (!WaterTile(map[y, x]) || sandbar.ContainsKey((x, y)) || mlRegion[y, x] != 2) continue;
                bool shore = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(o => map[y + o.Item2, x + o.Item1] == 's');
                float d = Dist(x + 0.5f, y + 0.5f, bx0, by0);
                if (shore && d < best) { best = d; tidePostX = x * T + 5; tidePostY = y * T + 8; }
            }
    }

    // Where you stand to read the tide post: the sand beside it.
    (float x, float y) PostReadSpot()
    {
        foreach (var (ox, oy) in new[] { (0, 10), (10, 0), (-10, 0), (0, -10), (10, 10), (-10, 10) })
            if (Walkable(TileAt((int)((tidePostX + ox) / T), (int)((tidePostY + oy - 1.5f) / T)))) return (tidePostX + ox, tidePostY + oy);
        return (tidePostX, tidePostY + 10);
    }

    // Cut off on the bar by the tide: the dry sand you're on doesn't reach the beach any more. A fisher in a banca comes
    // for you (no harm done); it's how you learn that the low part near the shore floods first.
    float barCutOffT;
    void UpdateSandbar(float dt)
    {
        int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
        if (mode != "play" || Aboard || Riding || !OnSandbarTile(tx, ty)) { barCutOffT = 0; return; }
        if (BarReachesShore(tx, ty)) { barCutOffT = 0; return; }
        if (barCutOffT == 0) Toast("The tide's come in over the low part of the bar! Stay on the sand: someone's coming.", 4);
        barCutOffT += dt;
        if (barCutOffT < 2.5f) return;
        barCutOffT = 0;
        RescueFromSandbar();
    }

    // Whether dry sand (or the tile under you) joins your tile to the beach.
    bool BarReachesShore(int tx, int ty)
    {
        var seen = new HashSet<(int, int)> { (tx, ty) };
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((tx, ty));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (!seen.Add((nx, ny))) continue;
                if (sandbar.ContainsKey((nx, ny))) { if (SandbarDry(nx, ny)) queue.Enqueue((nx, ny)); }
                else if (Walkable(TileAt(nx, ny))) return true;
            }
        }
        return false;
    }

    void RescueFromSandbar()
    {
        state.hinted["mr:barRescue"] = true;
        FadeThrough(() =>
        {
            var (rx, ry) = MlPlace("barroot");
            player.X = rx; player.Y = ry; player.Face = "left";
            if (!CanStand(player.X, player.Y)) (player.X, player.Y) = PostReadSpot();
            Save();
        }, () => Talk(new()
        {
            new("Mang Ambo", "Got you! Up you come. The tide comes back over the low part of a bar first, near the shore, while the high part's still dry. That's how people get stranded."),
            new("Mang Ambo", "Always leave yourself time: come back by the time I give you, or check Lola Pacing's tide table before you go out. And never try to swim the channel: it runs fast.")
        }));
    }

    // The bar while it's out, drawn over the water: dry sand, a wet rim, and foam where the sea meets it.
    void DrawSandbar()
    {
        if (scene != "world" || player.X < (MlStart - 20) * T || sandbar.Count == 0) return;
        float sea = SeaLevel;
        var (vx0, vy0, vx1, vy1) = VisibleTiles();
        foreach (var ((tx, ty), _) in sandbar)
        {
            if (tx < vx0 || tx > vx1 || ty < vy0 || ty > vy1) continue;
            for (int py = 0; py < T; py++)
                for (int px = 0; px < T; px++)
                {
                    int wx = tx * T + px, wy = ty * T + py;
                    float h = MlBarHeight(MlLx(wx + 0.5f), MlLy(wy + 0.5f)) + (float)(Pix.Hash(wx, wy, 977) - 0.5) * 0.015f;
                    float above = h - sea;
                    if (above < -0.04f) continue;
                    var c = above < 0 ? Pal.Rgba(232, 244, 246, 0.55f) : above < 0.03f ? Pal.C("#d2b47c") : (Pix.Hash(wx / 3, wy / 2, 978) < 0.5 ? Pal.C("#ead6a2") : Pal.C("#e2cc94"));
                    pix.Rect(wx, wy, 1, 1, c);
                }
        }
    }

    // The tide post at the bar's end: painted bands on a pole set in a rock; the water's height is read off them.
    void DrawTidePost()
    {
        int x = (int)tidePostX, y = (int)tidePostY;
        pix.Rect(x - 4, y - 2, 9, 3, "#5a5652");
        pix.Rect(x - 3, y - 3, 7, 1, "#6f6a64");
        for (int i = 0; i < 18; i++) pix.Rect(x, y - 3 - i, 2, 1, i / 2 % 2 == 0 ? "#f2efe6" : "#c0392b");
        pix.Rect(x - 1, y - 21, 4, 1, "#3c342a");
        // The water against it: higher up the bands the higher the tide.
        int wl = (int)MathF.Round((SeaLevel + 1) * 7);
        if (wl > 0) pix.Rect(x - 2, y - 2 - wl, 6, 1, Pal.Rgba(220, 240, 248, 0.8f));
    }

}
