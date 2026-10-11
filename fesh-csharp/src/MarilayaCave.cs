using Raylib_cs;

namespace Fesh;

// The cave in Marilaya's north-east hills (1.23): one small chamber with a wooden walkway and lamps, inspired by the
// limestone caves of Marinduque (SEARCA's account of the Bathala caves mentions bats, birds and harm done by careless
// visitors; nothing here is claimed for any one real cave). Three things to look at along the walkway, each saved at once:
// a dripping stalactite over a little pool, shells in the cave wall, and a roost of bats watched from behind the rope.
// The column by the walkway is to be looked at, not touched, and the passage past the rope is closed for the bats. By the
// door, a model runs the cave's story while you hold the button: rain made a weak acid by carbon dioxide, cracks widening
// over a very long time, then drips leaving calcite behind. Dissolving opens a cave; deposits decorate it.
sealed class MlCaveModel { public float T; public string Note = ""; public bool Ended; }

partial class Game
{
    const string MlCaveKey = "house:mlcave";
    const int MlCaveW = 34, MlCaveH = 15;
    bool InMlCave => scene == MlCaveKey;
    MlCaveModel mlCaveModel;

    // Where things are in the chamber (room pixels).
    const float CaveDripX = 64, CaveDripY = 50, CaveShellX = 160, CaveShellY = 34, CaveBatX = 290, CaveBatY = 36, CaveRopeX = 252,
        CaveColumnX = 210, CaveColumnY = 82, CaveModelX = 120, CaveModelY = 122, CavePassageX = 326, CavePassageY = 92;
    static readonly (float x, float y)[] CaveLamps = { (110, 120), (60, 86), (150, 64), (230, 60), (240, 112) };

    // The chamber: rock all round (the back wall three rows deep), a few outcrops, the floor, and the way out at the bottom.
    void BuildCaveRoom()
    {
        int w = MlCaveW, h = MlCaveH;
        roomMap = new char[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool wall = y < 3 || x == 0 || x == w - 1 || y == h - 1;
                // Bulges of rock into the chamber, so it isn't a box.
                if (!wall && (x <= 2 && y >= 10 || x >= w - 4 && y >= 11 || y == 3 && Pix.Hash(x, y, 51) < 0.35 || x == 1 && Pix.Hash(x, y, 52) < 0.5)) wall = true;
                roomMap[y, x] = wall ? '#' : '.';
            }
        roomMap[h - 1, w / 2] = 'Y';
        map = roomMap;
        roomBase = new Pix(w * T, h * T);
        basePix = roomBase;
        var g = roomBase;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int X = x * T, Y = y * T;
                if (roomMap[y, x] == '#')
                {
                    for (int py = 0; py < T; py++)
                        for (int px = 0; px < T; px++)
                        {
                            double n = VNoise(X + px, Y + py, 7, 61) * 0.7 + Pix.Hash(X + px, Y + py, 62) * 0.3;
                            g.Rect(X + px, Y + py, 1, 1, n < 0.35 ? "#4a453d" : n < 0.6 ? "#5a544a" : "#6b6558");
                        }
                    // Pale limestone bands in the back wall.
                    if (y < 3) for (int px = 0; px < T; px += 2) if (Pix.Hash(X + px, y, 63) < 0.6) g.Rect(X + px, Y + 3 + y % 2 * 4, 2, 1, "#8a8476");
                }
                else if (roomMap[y, x] == 'Y') { g.Rect(X, Y, T, T, "#1b1612"); g.Rect(X + 1, Y, 8, 3, "#7a6a50"); }
                else
                {
                    for (int py = 0; py < T; py++)
                        for (int px = 0; px < T; px++)
                        {
                            double n = VNoise(X + px, Y + py, 9, 64);
                            g.Rect(X + px, Y + py, 1, 1, n < 0.4 ? "#6f685a" : n < 0.7 ? "#7a7364" : "#857d6c");
                        }
                }
            }
        // The walkway: boards from the door up the middle, then along under the back wall.
        void Boards(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    g.Rect(x * T, y * T + 1, T, 8, "#8a5d32");
                    g.Rect(x * T, y * T + 1, T, 1, "#a8743f");
                    if ((x + y) % 2 == 0) g.Rect(x * T + 4, y * T + 1, 1, 8, "#6b4424");
                }
        }
        Boards(15, 9, 18, 13);
        Boards(4, 6, 24, 8);
    }

    // Solid in the chamber: the pool under the drip, the column, the rope and the model's stand.
    IEnumerable<Box> MlCaveSolids() => !InMlCave ? Enumerable.Empty<Box>() : new[]
    {
        new Box(CaveDripX - 10, CaveDripY + 6, 20, 8), new Box(CaveColumnX - 6, CaveColumnY - 4, 12, 6), new Box(CaveRopeX - 1, 40, 3, 100),
        new Box(CaveModelX - 10, CaveModelY - 4, 20, 5)
    };

    Target MlCaveTarget()
    {
        float x = player.X, y = player.Y;
        if (Dist(x, y, RoomDoorWX, RoomDoorWY) < 14) return new Target { Type = "exit", Label = "Go outside" };
        if (Dist(x, y, CaveModelX, CaveModelY + 6) < 16) return new Target { Type = "marilaya", Id = "cavemodel", Label = "Run the cave model" };
        if (Dist(x, y, CaveDripX, CaveDripY + 22) < 18) return new Target { Type = "marilaya", Id = "cavedrip", Label = state.Hinted("mr:cave1") ? "Watch the drip" : "Look at the dripping stalactite" };
        if (Dist(x, y, CaveShellX, CaveShellY + 30) < 22) return new Target { Type = "marilaya", Id = "caveshells", Label = state.Hinted("mr:cave2") ? "Look at the shells in the wall" : "Look closely at the cave wall" };
        if (Dist(x, y, CaveRopeX - 8, y) < 14 && y < 100) return new Target { Type = "marilaya", Id = "cavebats", Label = state.Hinted("mr:cave3") ? "Watch the bats" : "Watch the bats quietly, from the rope" };
        if (Dist(x, y, CaveColumnX, CaveColumnY + 6) < 16) return new Target { Type = "marilaya", Id = "cavecolumn", Label = "Touch the column?" };
        return null;
    }

    void UseCave(string id)
    {
        switch (id)
        {
            case "cavemodel": mlCaveModel = new MlCaveModel { T = state.Hinted("mr:cavemodel") ? 1 : 0, Ended = state.Hinted("mr:cavemodel") }; OpenMlPanel("mlcavemodel"); return;
            case "cavedrip":
                FaceToward(CaveDripX, CaveDripY);
                CaveFind("mr:cave1", MlCaveWords["drip"]);
                return;
            case "caveshells":
                FaceToward(CaveShellX, CaveShellY);
                CaveFind("mr:cave2", MlCaveWords["shells"]);
                return;
            case "cavebats":
                FaceToward(CaveBatX, CaveBatY);
                CaveFind("mr:cave3", MlCaveWords["bats"]);
                return;
            case "cavecolumn":
                FaceToward(CaveColumnX, CaveColumnY);
                Talk(new() { new("You", MlCaveWords["column"]) });
                return;
        }
    }

    static readonly Dictionary<string, string> MlCaveWords = new()
    {
        ["drip"] = "A drop hangs from the stalactite's tip, swells and falls into the pool. Drops like this can leave a thin film of calcite behind, the same mineral as the limestone. Stalactites grow very slowly, drop by drop.",
        ["shells"] = "Shells in the cave wall, set in the rock, like the ones on the rock face outside. The whole hill is limestone: an old reef, lifted out of the sea.",
        ["bats"] = "Bats hanging from the ceiling past the rope, wings folded, resting. These are insect-eating bats: they roost here by day and hunt insects at night. Lights and noise disturb them, so the walkway stops here.",
        ["column"] = "A column, where a stalactite and a stalagmite met. The sign says: \"Please don't touch. The oils on your hands can stain it and stop it growing, and it took thousands of years. Sketch it instead.\""
    };

    // A thing seen in the cave: kept at once, then the karst clue once the model has been run too.
    void CaveFind(string key, string words)
    {
        bool first = !state.Hinted(key);
        NoteMlStep(key);
        if (first) Sfx.Play("right");
        int n = new[] { "mr:cave1", "mr:cave2", "mr:cave3" }.Count(k => state.Hinted(k));
        // The clue is kept before a word is said, so quitting during the words can't lose it (Codex).
        CheckKarst();
        if (first && n < 3) ToastLater($"Noted in the field notebook ({n} of 3 in the cave).");
        else if (first && !state.Hinted("mr:cavemodel")) ToastLater("All three noted. Run the cave model by the door to see how the cave was made.");
        Talk(new() { new("You", words) });
    }

    void CheckKarst()
    {
        if (state.Hinted("mr:karst") || !state.Hinted("mr:cavemodel") || !new[] { "mr:cave1", "mr:cave2", "mr:cave3" }.All(k => state.Hinted(k))) return;
        NoteMlClue("mr:karst", "Clue: water's patient work.");
    }

    /* ---------- The cave model ---------- */
    static readonly (float at, string text)[] CaveStages =
    {
        (0, "Rain falls on the hill. On its way down it picks up carbon dioxide from the air and the soil, and becomes a very weak acid."),
        (0.22f, "The water seeps down through cracks in the limestone, dissolving a little of the rock as it goes."),
        (0.45f, "Over a very long time, many thousands of years, the cracks widen into passages, and the passages into a cave."),
        (0.72f, "Where water drips into the cave's air, it gives up some of its carbon dioxide and leaves a little calcite behind: stalactites grow down, stalagmites grow up."),
        (0.95f, "Two different things: dissolving opened the cave, and deposits decorate it.")
    };

    void DrawMlCaveModel()
    {
        var m = mlCaveModel;
        if (m == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame("The cave model", "Hold the button (or hold <act>) to run time forward; let go to stop and look.");
        if (MlClose(x, y)) return;
        float px = x + 24, py = y + 100, pw = 560, ph = 400;
        bool holding = MlKeysLive && Bind.Down("act") || Gfx.Down && Gfx.Hover(px + pw + 24, y + MlPanelH - 24 - 60, 260, 60);
        if (holding && m.T < 1) { m.T = Math.Min(1, m.T + Raylib.GetFrameTime() / 9); if ((int)(time * 6) % 4 == 0) Sfx.Play("blip"); }
        float t = m.T;
        // The cross-section: sky and rain, soil, the limestone with its cracks, and the cave they become.
        Gfx.Rect(px, py, pw, ph, Pal.C("#bfe0ec"));
        for (int i = 0; i < 16; i++)
        {
            float fx = px + 14 + i * 34, k = (time * 1.2f + i * 0.13f) % 1;
            if (t < 0.45f || i % 3 == 0) Gfx.Line(fx, py + 6 + k * 46, fx - 3, py + 14 + k * 46, 2, Pal.C("#3a7ab8"));
        }
        Gfx.Rect(px, py + 60, pw, 8, Pal.C("#5d9b45"));
        Gfx.Rect(px, py + 68, pw, 34, Pal.C("#6b4a2b"));
        Gfx.Rect(px, py + 102, pw, ph - 102, Pal.C("#e2dcc6"));
        for (int k = 0; k < 8; k++) Gfx.Rect(px, py + 118 + k * 36, pw, 2, Pal.C("#cfc9b4"));
        for (int i = 0; i < 9; i++) DrawShell(px + 30 + i * 62, py + 150 + (i % 3) * 70, 6, false);
        // The cracks: thin, then widening.
        float widen = Math.Clamp((t - 0.2f) / 0.3f, 0, 1);
        foreach (float fx in new[] { 0.3f, 0.5f, 0.7f })
            Gfx.Line(px + pw * fx, py + 102, px + pw * (fx + 0.04f), py + 250, 2 + widen * 10, Pal.C("#7fb6cf"));
        // The cave opening up.
        float cave = Math.Clamp((t - 0.42f) / 0.3f, 0, 1);
        if (cave > 0) Gfx.Rect(px + pw * (0.5f - 0.3f * cave), py + 230 - 50 * cave, pw * 0.6f * cave, 40 + 110 * cave, Pal.C("#2a2622"), 30 * cave);
        // Stalactites and stalagmites growing.
        float grow = Math.Clamp((t - 0.7f) / 0.25f, 0, 1);
        if (grow > 0)
            foreach (float fx in new[] { 0.3f, 0.4f, 0.52f, 0.62f, 0.72f })
            {
                float top = py + 182, bot = py + 330;
                Gfx.Triangle(px + pw * fx - 6, top, px + pw * fx + 6, top, px + pw * fx, top + 30 * grow, Pal.C("#e8e2cc"));
                Gfx.Triangle(px + pw * fx - 7, bot, px + pw * fx, bot - 22 * grow, px + pw * fx + 7, bot, Pal.C("#e8e2cc"));
            }
        if (cave > 0.5f) { float dk = time * 1.5f % 1; Gfx.Circle(px + pw * 0.4f, py + 214 + dk * 100, 3, Pal.C("#9fd0e6")); }
        Gfx.Text("rain", px + 10, py + 34, FontKind.Ui600, 16, Pal.PaperInk);
        Gfx.Text("soil", px + 10, py + 74, FontKind.Ui600, 16, Color.White);
        Gfx.Text("limestone", px + 10, py + 108, FontKind.Ui600, 16, Pal.PaperInk);
        // Time passing.
        Gfx.Rect(px, py + ph + 10, pw, 10, Pal.C("#d8c7a0"), 5);
        Gfx.Rect(px, py + ph + 10, pw * t, 10, Pal.C("#b5523b"), 5);
        Gfx.Text(t < 0.05f ? "Today's rain" : t < 0.45f ? "Hundreds of years" : t < 0.75f ? "Tens of thousands of years" : "Longer still", px, py + ph + 24, FontKind.Ui600, 15, Muted);
        // What's happening now, and the button to hold.
        float rx = px + pw + 24, rw = MlPanelW - (rx - x) - 24;
        var stage = CaveStages.Last(s => t >= s.at);
        MlNote(stage.text, true, rx, py, rw, 200);
        if (t >= 1 && !m.Ended)
        {
            m.Ended = true;
            NoteMlStep("mr:cavemodel");
            Sfx.Play("right");
            CheckKarst();
            m.Note = "";
            m.Note = state.Hinted("mr:karst") ? "The cave's story is in your notebook, with the drip, the shells and the bats. Its trail sign goes up outside."
                : "Now look at the three marked spots along the walkway: the drip, the shells in the wall, and the bats.";
        }
        MlNote(m.Note, state.Hinted("mr:karst"), rx, py + 214, rw, 140);
        var hold = Pal.C(t >= 1 ? "#3f7f5a" : "#b5523b");
        float bx = rx, by = y + MlPanelH - 24 - 60;
        Gfx.Box(bx, by, 260, 60, holding ? Lighten(hold, 0.2f) : hold, Pal.Ink, 3, 6, 4);
        Gfx.TextCenter(t >= 1 ? "Run again" : "Hold to run time", bx + 130, by + 18, FontKind.Ui700, 21, Color.White);
#if DEBUG
        Gfx.Seen["cavemodel:hold"] = new Rectangle(bx, by, 260, 60);
#endif
        if (t >= 1 && Gfx.Click(bx, by, 260, 60)) { m.T = 0; m.Note = ""; m.Ended = false; }
    }

    /* ---------- Drawing the chamber ---------- */
    void AddMlCaveObjects(List<(float y, Action draw)> list)
    {
        list.Add((CaveDripY + 12, DrawCaveDrip));
        list.Add((CaveColumnY + 2, DrawCaveColumn));
        list.Add((70, DrawCaveRope));
        list.Add((CaveModelY, DrawCaveModelStand));
        foreach (var (lx, ly) in CaveLamps) list.Add((ly, () => { pix.Rect(lx, ly - 9, 1, 9, "#65452e"); pix.Rect(lx - 1, ly - 12, 3, 3, "#f3c25b"); }));
    }

    // Before the objects: the back wall's stalactites, the shells in the wall, the bats' roost and the closed passage.
    void DrawCaveWalls()
    {
        foreach (int sx in new[] { 30, 64, 92, 128, 196, 232, 270, 300 })
        {
            int len = 4 + (int)(Pix.Hash(sx, 0, 71) * 8);
            for (int i = 0; i < len; i++) pix.Rect(sx - (len - i) / 4, 30 + i, 1 + (len - i) / 2, 1, i % 3 == 0 ? "#d8d2be" : "#c9c2ac");
        }
        foreach (var (ox, oy) in new[] { (-14, -6), (-4, 2), (8, -3), (16, 4), (2, -12) })
        {
            int x = (int)CaveShellX + ox, y = (int)CaveShellY + oy;
            pix.Rect(x, y, 4, 3, "#e8dfc8"); pix.Rect(x + 1, y, 1, 3, "#b8ad94"); pix.Rect(x + 3, y + 1, 1, 1, "#b8ad94");
        }
        // Bats: little dark shapes hanging in a row, now and then one shuffling its wings.
        for (int i = 0; i < 9; i++)
        {
            int x = (int)CaveBatX - 20 + i * 5 + (i % 2) * 2, y = 28 + (i % 3);
            bool stir = (int)(time * 0.7f + i * 1.3f) % 7 == 0;
            pix.Rect(x, y, 2, 3, "#1e1a1f");
            if (stir) { pix.Rect(x - 2, y + 1, 2, 1, "#1e1a1f"); pix.Rect(x + 2, y + 1, 2, 1, "#1e1a1f"); }
        }
        // The closed passage beyond the rope, with its sign.
        pix.Rect(CavePassageX - 8, CavePassageY - 18, 12, 22, "#15110f");
        pix.Rect(CavePassageX - 26, CavePassageY - 4, 9, 6, "#d8c7a0"); pix.Rect(CavePassageX - 25, CavePassageY - 3, 7, 1, "#c0392b"); pix.Rect(CavePassageX - 25, CavePassageY - 1, 5, 1, "#8a6a48");
    }

    void DrawCaveDrip()
    {
        int x = (int)CaveDripX, y = (int)CaveDripY;
        // The stalactite above, the pool below with a stalagmite starting in it.
        for (int i = 0; i < 16; i++) pix.Rect(x - (16 - i) / 5, y - 20 + i, 1 + (16 - i) / 3, 1, "#ddd6c0");
        for (int j = -3; j <= 3; j++)
            for (int i = -9; i <= 9; i++)
                if (i * i / 81f + j * j / 9f <= 1) pix.Rect(x + i, y + 10 + j, 1, 1, i * i / 81f + j * j / 9f > 0.6f ? "#4f8aa0" : "#6fb0c4");
        pix.Rect(x - 1, y + 6, 3, 3, "#ddd6c0");
        float k = time * 0.8f % 1;
        if (k < 0.7f) pix.Rect(x, y - 4 + k * 18, 1, 2, "#bfe6f2");
        else pix.Rect(x - 2, y + 10, 5, 1, "#e8f6fb");
    }

    void DrawCaveColumn()
    {
        int x = (int)CaveColumnX, y = (int)CaveColumnY;
        pix.Rect(x - 7, y - 1, 15, 3, "rgba(0,0,0,0.2)");
        for (int i = 0; i < 52; i++)
        {
            int w = 4 + (int)(MathF.Abs(MathF.Sin(i * 0.2f)) * 3) + (i > 40 ? (i - 40) / 2 : 0) + (i < 8 ? (8 - i) / 2 : 0);
            pix.Rect(x - w, y - 52 + i, w * 2, 1, i % 4 == 0 ? "#d8d0b8" : "#e8e0c8");
        }
        pix.Rect(x + 6, y - 6, 7, 5, "#d8c7a0"); pix.Rect(x + 7, y - 5, 5, 1, "#8a6a48");
    }

    void DrawCaveRope()
    {
        for (int y = 44; y < 136; y += 30) { pix.Rect(CaveRopeX - 1, y - 8, 2, 10, "#65452e"); }
        for (int y = 40; y < 136; y++) pix.Rect(CaveRopeX + MathF.Sin(y * 0.2f) * 0.6f, y, 1, 1, "#c9a06a");
    }

    void DrawCaveModelStand()
    {
        int x = (int)CaveModelX, y = (int)CaveModelY;
        pix.Rect(x - 10, y - 1, 20, 2, "rgba(0,0,0,0.2)");
        pix.Rect(x - 9, y - 9, 18, 8, "#8a5d32");
        pix.Rect(x - 8, y - 8, 16, 3, "#e2dcc6"); pix.Rect(x - 8, y - 5, 16, 3, "#6b4a2b"); pix.Rect(x - 3, y - 8, 6, 2, "#2a2622");
    }

    // The chamber is dim: the lamps along the walkway and your own light.
    void LightMlCave()
    {
        Array.Fill(dark, 0.62f);
        LightHole(player.X, player.Y - 6, 34, 0.7f);
        foreach (var (lx, ly) in CaveLamps) LightHole(lx, ly - 10, 40, 0.8f);
        ApplyDark(new Color(10, 8, 6, 255));
        foreach (var (lx, ly) in CaveLamps) pix.Glow(lx, ly - 10, 18, Pal.Rgba(243, 170, 70, 0.25f));
    }
}
