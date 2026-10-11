using System.Numerics;
using Raylib_cs;

namespace Fesh;

// The heart that rose's panels (MarilayaStory.cs has the story): brushing the rock face clean (its fossils, the old wave
// notch and the layers' order), reading the tide post, Lorna's night record and the dryer at the springs, Sela's shore
// model, sighting from a survey station, and plotting the chart. Each is something you do, not a quiz: a wrong move shows
// you why and leaves everything you'd already done in place, and every step is saved the moment it's earned (Codex).
sealed class MlRock
{
    public const int Cols = 60, Rows = 40;
    public readonly bool[,] Dust = new bool[Cols, Rows];
    public int Step;                    // 0 brushing, 1 the layers oldest to youngest, 2 done
    public readonly List<int> Order = new();
    public string Note = "";
    public float Sparkle; public int SparkleAt = -1;
}

sealed class MlPost { public string Note = ""; public bool Read; }
sealed class MlSprings { public int Step; public string Note = ""; public int Pipe = -1; public float Flow; public bool LineOk; }
sealed class MlModel
{
    public int Held = -1;               // the observation picked up
    public readonly int[] Placed = { -1, -1, -1 };
    public string Note = "";
    public string Playing;              // the mechanism animating: "tide", "waves", "land", "quake"
    public float T;
}
sealed class MlScope
{
    public string Station = "A", Want = "B";
    public float Bearing, Turn;
    public string Note = "";
    public bool Good;
    public float DragFrom = float.NaN, DragBearing;
}
sealed class MlPlot { public int Step; public float T; }

partial class Game
{
    const float MlPanelW = 1040, MlPanelH = 600;
    float lastMlPanelBottom;    // where the last Marilaya panel ended, checked against the screen
    string lastMlPanelSub = "";  // and its subtitle, as drawn
    MlRock mlRock;
    MlPost mlPost;
    MlSprings mlSprings;
    MlModel mlModel;
    MlScope mlScope;
    MlPlot mlPlot;
#if DEBUG
    float lastMlScopeBearing;   // the bearing used to draw the panorama this frame
#endif

    // Every Marilaya panel, so ClosePanels, Esc and a new game put them all down.
    static readonly string[] MlPanels = { "mlrock", "mlpost", "mlsprings", "mlmodel", "mlscope", "mlplot", "mlcavemodel", "mlkalutang", "mlnursery" };
    void DropMlPanels() { mlRock = null; mlPost = null; mlSprings = null; mlModel = null; mlScope = null; mlPlot = null; mlCaveModel = null; mlKalutang = null; mlNursery = null; }

    (float x, float y) MlPanelFrame(string title, string sub, float w = MlPanelW, float h = MlPanelH)
    {
        Backdrop();
        float x = (Gfx.LW - w) / 2, y = Math.Max(8, (Gfx.LH - h) / 2);
        lastMlPanelBottom = y + h;
        sub = Bind.Fix(sub);
        lastMlPanelSub = sub;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text(title, x + 24, y + 20, FontKind.Ui700, 30, Pal.PaperInk);
        if (sub != "") Lines(Gfx.Wrap(sub, FontKind.Note, 17, w - 48 - SmallW("Close") - 20), x + 24, y + 58, 21, FontKind.Note, 17, Muted);
        return (x, y);
    }

    int lastMlNoteCut, mlNoteCutMost;   // lines of a Marilaya panel's note that didn't fit its box, now and at most (checked: none)
    void MlNote(string note, bool good, float x, float y, float w, float h = 96)
    {
        if (note == "") return;
        Gfx.Rect(x, y, w, h, good ? Pal.C("#e4f3e0") : Pal.C("#f8e6dc"), 6);
        var lines = Gfx.Wrap(note, FontKind.Ui600, 17, w - 24);
        int room = (int)((h - 12) / 22);
        lastMlNoteCut = Math.Max(0, lines.Count - room);
        mlNoteCutMost = Math.Max(mlNoteCutMost, lastMlNoteCut);
        Lines(lines.Take(room).ToList(), x + 12, y + 8, 22, FontKind.Ui600, 17, Pal.PaperInk);
    }

    bool MlClose(float x, float y, float w = MlPanelW)
    {
        if (!SmallButton("Close", x + w - 24 - SmallW("Close"), y + 18)) return false;
        ClosePanels();
        return true;
    }

    static bool MlDone(string label, float x, float y) => Button(label, x, y, Gfx.Measure(label, FontKind.Ui700, 22) + 48, 50, FontKind.Ui700, 22, Pal.Buoy, Color.White, 5);

    /* ---------- The rock face ---------- */
    // The beds, top to bottom: A soil and ash (youngest), B limestone with shells and coral, C dark volcanic rock (oldest).
    static readonly (string label, string name)[] MlBeds = { ("A", "Soil and volcanic ash"), ("B", "Limestone with shells and coral"), ("C", "Dark volcanic rock") };
    // The fossils set in the limestone (fractions of the picture), and the old notch's band.
    static readonly (float fx, float fy, string kind)[] MlFossils = { (0.16f, 0.52f, "shell"), (0.37f, 0.62f, "coral"), (0.58f, 0.5f, "shell"), (0.8f, 0.64f, "urchin"), (0.67f, 0.32f, "shell") };
    const float MlNotchTop = 0.3f, MlNotchBot = 0.38f;
    const int MlFossilsNeeded = 3;
    int MlFossilsFound => Enumerable.Range(0, MlFossils.Length).Count(i => state.Hinted($"mr:fos:{i}"));

    void OpenCliff()
    {
        if (state.Hinted("mr:layers"))
        {
            Talk(new() { new("You", "Shells and coral in the limestone, an old wave notch high and dry, and the dark volcanic rock underneath. This rock was sea floor once, and it's been lifted.") });
            return;
        }
        var r = mlRock = new MlRock { Step = state.Hinted("mr:fossil") && state.Hinted("mr:notch") ? 1 : 0 };
        // Dust over the face, except where you've already cleaned (what you've sketched stays sketched).
        for (int i = 0; i < MlRock.Cols; i++)
            for (int j = 0; j < MlRock.Rows; j++)
            {
                float fx = (i + 0.5f) / MlRock.Cols, fy = (j + 0.5f) / MlRock.Rows;
                bool keep = r.Step > 0 || Enumerable.Range(0, MlFossils.Length).Any(k => state.Hinted($"mr:fos:{k}") && Dist(fx * 1.5f, fy, MlFossils[k].fx * 1.5f, MlFossils[k].fy) < 0.09f)
                    || state.Hinted("mr:notch") && fy > MlNotchTop - 0.02f && fy < MlNotchBot + 0.02f;
                r.Dust[i, j] = !keep && fy > 0.12f && fy < 0.9f;
            }
        OpenMlPanel("mlrock");
    }

    void DrawMlRock()
    {
        var r = mlRock;
        if (r == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame("The rock face", r.Step == 0 ? "Hold the mouse button (or pad A) and sweep to brush the dust off. Sketch what's part of the rock; leave everything where it is."
            : r.Step == 1 ? "Now the layers: click them from the oldest to the youngest." : "Recorded in the notebook.");
        if (MlClose(x, y)) return;
        float px = x + 24, py = y + 102, pw = 600, ph = 420;
        // The face in its three beds, then the sand at its foot.
        float faceTop = py + ph * 0.12f, faceBot = py + ph * 0.9f, fh = faceBot - faceTop;
        float[] bedTop = { faceTop, faceTop + fh * 0.12f, faceTop + fh * 0.78f }, bedBot = { faceTop + fh * 0.12f, faceTop + fh * 0.78f, faceBot };
        string[] bedCol = { "#8a6a48", "#e2dcc6", "#55514d" };
        Gfx.Rect(px, py, pw, faceTop - py, Pal.C("#9fd0e6"));
        Gfx.Rect(px, faceTop - 10, pw, 12, Pal.C("#6f9a4e"));
        for (int i = 0; i < 3; i++)
        {
            bool hover = r.Step == 1 && Gfx.Hover(px, bedTop[i], pw, bedBot[i] - bedTop[i]);
            bool done = r.Step == 1 && r.Order.Contains(2 - i) || r.Step == 2;
            Gfx.Rect(px, bedTop[i], pw, bedBot[i] - bedTop[i], hover ? Lighten(Pal.C(bedCol[i]), 0.2f) : Pal.C(bedCol[i]));
            for (int k = 1; k < 5; k++) Gfx.Rect(px, bedTop[i] + (bedBot[i] - bedTop[i]) * k / 5, pw, 2, Pal.WithAlpha(Pal.Ink, 0.1f));
            if (r.Step >= 1)
            {
                Gfx.Box(px + pw - 56, bedTop[i] + 6, 44, 34, done ? Pal.C("#3f7f5a") : Pal.Sand, Pal.Ink, 2, 4);
                Gfx.TextCenter(MlBeds[i].label, px + pw - 34, bedTop[i] + 11, FontKind.Ui700, 22, done ? Color.White : Pal.Ink);
            }
#if DEBUG
            Gfx.Seen[$"mlbed:{MlBeds[i].label}"] = new Rectangle(px, bedTop[i], pw, bedBot[i] - bedTop[i]);
#endif
            if (r.Step == 1 && Gfx.Click(px, bedTop[i], pw, bedBot[i] - bedTop[i])) PickBed(r, 2 - i);
        }
        // The old wave notch: a groove along the limestone with an overhang over it.
        float ny0 = py + ph * MlNotchTop, ny1 = py + ph * MlNotchBot;
        Gfx.Rect(px, ny0, pw, 5, Pal.C("#f2ecda"));
        Gfx.Rect(px, ny0 + 5, pw, ny1 - ny0 - 5, Pal.C("#b3ac98"));
        Gfx.Rect(px, ny1 - 6, pw, 6, Pal.C("#9c9584"));
        // The fossils.
        for (int k = 0; k < MlFossils.Length; k++)
        {
            var (fx, fy, kind) = MlFossils[k];
            DrawFossil(px + pw * fx, py + ph * fy, kind, 1);
        }
        // The sand at the foot, and a loose shell lying on it.
        Gfx.Rect(px, faceBot, pw, py + ph - faceBot, Pal.C("#e8cf96"));
        float lx = px + pw * 0.3f, ly = faceBot + (py + ph - faceBot) / 2;
        DrawShell(lx, ly, 12, false);
        // The dust you brush away (blown off the rest once the finds are all in).
        float cw = pw / MlRock.Cols, chh = ph / MlRock.Rows;
        if (r.Step == 0)
        for (int i = 0; i < MlRock.Cols; i++)
            for (int j = 0; j < MlRock.Rows; j++)
                if (r.Dust[i, j])
                {
                    double h = Pix.Hash(i, j, 991);
                    Gfx.Rect(px + i * cw - 0.5f, py + j * chh - 0.5f, cw + 1, chh + 1, h < .33 ? Pal.C("#b49a74") : h < .66 ? Pal.C("#a88d68") : Pal.C("#bfa680"));
                }
#if DEBUG
        Gfx.Seen["mlrock:face"] = new Rectangle(px, faceTop, pw, faceBot - faceTop);
        Gfx.Seen["mlrock:sand"] = new Rectangle(lx - 24, ly - 18, 48, 36);
        for (int k = 0; k < MlFossils.Length; k++) Gfx.Seen[$"mlrock:fossil{k}"] = new Rectangle(px + pw * MlFossils[k].fx - 20, py + ph * MlFossils[k].fy - 20, 40, 40);
        Gfx.Seen["mlrock:notch"] = new Rectangle(px, ny0, pw, ny1 - ny0);
#endif
        // Brushing: the button held down over the face sweeps the dust off where the pointer is.
        if (r.Step == 0 && Gfx.Down && Gfx.Hover(px, py, pw, ph))
        {
            int ci = (int)((Gfx.Mouse.X - px) / cw), cj = (int)((Gfx.Mouse.Y - py) / chh);
            bool any = false;
            for (int i = ci - 2; i <= ci + 2; i++)
                for (int j = cj - 2; j <= cj + 2; j++)
                    if (i >= 0 && j >= 0 && i < MlRock.Cols && j < MlRock.Rows && (i - ci) * (i - ci) + (j - cj) * (j - cj) <= 5 && r.Dust[i, j]) { r.Dust[i, j] = false; any = true; }
            if (any && (int)(time * 12) % 3 == 0) Sfx.Play("swish");
            CheckRockFinds(r);
        }
        // The loose shell on the sand: a fine thing to notice, but not the rock's story.
        if (r.Step == 0 && Gfx.Click(lx - 24, ly - 18, 48, 36))
        {
            Sfx.Play("wrong");
            r.Note = "A useful beach observation! But a loose shell could have washed up yesterday, or been dropped by a gull. It doesn't tell us when this rock formed. Look for shells that are part of the rock itself.";
        }
        if (r.SparkleAt >= 0 && r.Sparkle > 0)
        {
            r.Sparkle -= Raylib.GetFrameTime();
            var (sfx, sfy, _) = r.SparkleAt < MlFossils.Length ? MlFossils[r.SparkleAt] : (0.5f, (MlNotchTop + MlNotchBot) / 2, "");
            Gfx.Circle(px + pw * sfx, py + ph * sfy, 30 * (1.4f - r.Sparkle), Pal.Rgba(255, 240, 160, 0.4f * r.Sparkle));
        }

        // The notebook: what's sketched, today's shore for comparison, the key and the order.
        float rx = px + pw + 24, rw = MlPanelW - (rx - x) - 24, ry = py;
        Gfx.Text("Field notebook", rx, ry, FontKind.Ui700, 22, Pal.PaperInk);
        ry += 32;
        int found = MlFossilsFound;
        Gfx.Text($"Fossils sketched: {Math.Min(found, MlFossilsNeeded)} of {MlFossilsNeeded}", rx, ry, FontKind.Ui600, 17, found >= MlFossilsNeeded ? Pal.C("#3f7f5a") : Pal.PaperInk);
        ry += 24;
        Gfx.Text(state.Hinted("mr:notch") ? "The old notch: found" : $"The old notch: {NotchCleanPct(r)}% brushed", rx, ry, FontKind.Ui600, 17, state.Hinted("mr:notch") ? Pal.C("#3f7f5a") : Pal.PaperInk);
        ry += 30;
        // Today's shore: the waves cut a notch at the waterline.
        float ix = rx, iy = ry, iw = rw, ih = 64;
        Gfx.Rect(ix, iy, iw, ih, Pal.C("#9fd0e6"));
        Gfx.Rect(ix, iy + 10, iw * 0.55f, ih - 10, Pal.C("#e2dcc6"));
        Gfx.Rect(ix, iy + 38, iw * 0.55f, 8, Pal.C("#b3ac98"));
        Gfx.Rect(ix + iw * 0.5f, iy + 40, iw * 0.5f, ih - 40, Pal.C("#3f8fb0"));
        for (int i = 0; i < 4; i++) Gfx.Rect(ix + iw * 0.52f + i * 22 + MathF.Sin(time * 2 + i) * 3, iy + 40, 12, 2, Pal.C("#e8f6fb"));
        Gfx.Text("Today, at the shore: waves cut a notch", ix + 6, iy + ih + 4, FontKind.Ui500, 15, Muted);
        ry = iy + ih + 28;
        if (r.Step >= 1)
            for (int i = 0; i < 3; i++)
            {
                var name = Gfx.Wrap($"{MlBeds[i].label}: {MlBeds[i].name}", FontKind.Ui500, 16, rw);
                Lines(name, rx, ry, 20, FontKind.Ui500, 16, Pal.PaperInk);
                ry += name.Count * 20 + 2;
            }
        if (r.Step >= 1)
        {
            string order = r.Order.Count == 0 ? "Oldest first: ..." : "Oldest first: " + string.Join(", then ", r.Order.Select(o => MlBeds[2 - o].label));
            Lines(Gfx.Wrap(order, FontKind.Ui600, 16, rw), rx, ry + 2, 20, FontKind.Ui600, 16, Pal.C("#3f7f5a"));
            ry += 26;
        }
        float noteH = y + MlPanelH - 24 - (r.Step == 2 ? 62 : 0) - (ry + 6);
        MlNote(r.Note, !r.Note.StartsWith("A useful") && !r.Note.StartsWith("Not ") && !r.Note.StartsWith("The soil") && !r.Note.StartsWith("The limestone"), rx, ry + 6, rw, noteH);
        if (r.Step == 2 && MlDone("Done", x + MlPanelW - 24 - (Gfx.Measure("Done", FontKind.Ui700, 22) + 48), y + MlPanelH - 24 - 50)) { ClosePanels(); return; }
    }

    // How much of the old notch's band you've brushed clean (it's found at 70).
    static int NotchCleanPct(MlRock r)
    {
        int all = 0, clean = 0;
        for (int i = 0; i < MlRock.Cols; i++)
            for (int j = 0; j < MlRock.Rows; j++)
            {
                float fx = (i + 0.5f) / MlRock.Cols, fy = (j + 0.5f) / MlRock.Rows;
                if (fy <= MlNotchTop || fy >= MlNotchBot || fx <= 0.1f || fx >= 0.9f) continue;
                all++;
                if (!r.Dust[i, j]) clean++;
            }
        return all == 0 ? 100 : clean * 100 / all;
    }

    // What's been uncovered: a fossil or the notch counts once most of it is clean, and is kept at once.
    void CheckRockFinds(MlRock r)
    {
        float Clean(Func<float, float, bool> inside)
        {
            int all = 0, clean = 0;
            for (int i = 0; i < MlRock.Cols; i++)
                for (int j = 0; j < MlRock.Rows; j++)
                {
                    float fx = (i + 0.5f) / MlRock.Cols, fy = (j + 0.5f) / MlRock.Rows;
                    if (!inside(fx, fy)) continue;
                    all++;
                    if (!r.Dust[i, j]) clean++;
                }
            return all == 0 ? 1 : clean / (float)all;
        }
        for (int k = 0; k < MlFossils.Length; k++)
        {
            if (state.Hinted($"mr:fos:{k}")) continue;
            var (fx, fy, kind) = MlFossils[k];
            if (Clean((a, b) => Dist(a * 1.5f, b, fx * 1.5f, fy) < 0.06f) < 0.75f) continue;
            NoteMlStep($"mr:fos:{k}");
            Sfx.Play("right");
            r.Sparkle = 1; r.SparkleAt = k;
            r.Note = MlFossilNotes[kind];
            if (MlFossilsFound >= MlFossilsNeeded && !state.Hinted("mr:fossil")) NoteMlStep("mr:fossil");
        }
        if (!state.Hinted("mr:notch") && Clean((a, b) => b > MlNotchTop && b < MlNotchBot && a > 0.1f && a < 0.9f) > 0.7f)
        {
            NoteMlStep("mr:notch");
            Sfx.Play("right");
            r.Sparkle = 1; r.SparkleAt = MlFossils.Length;
            r.Note = MlNotchNote;
        }
        if (r.Step == 0 && state.Hinted("mr:fossil") && state.Hinted("mr:notch"))
        {
            r.Step = 1;
            r.Note = (r.Note == "" ? "" : r.Note + " ") + "Now the layers: which was laid down first?";
        }
    }

    static readonly Dictionary<string, string> MlFossilNotes = new()
    {
        ["coral"] = "Sketched: a branch of coral, set solid in the limestone. Reef-building corals like this lived in warm, shallow, sunlit water.",
        ["urchin"] = "Sketched: the hard skeleton of a sea urchin (its test), fossilised in the rock. Sea urchins live on the sea floor.",
        ["shell"] = "Sketched: a seashell set solid in the rock, part of it. The limestone was laid down under the sea, with the shells in it."
    };
    const string MlNotchNote = "A long groove all along the rock, with an overhang: the same shape the waves cut along the shore today, at the waterline. This one is high and dry, far above the sea: the sea once stood here, against this rock. Geologists date notches and shells like these to work out how much the land has risen since; Aling Sela's old survey notes say this one has.";

    // Beds are numbered by age: 0 the oldest (C, at the bottom), 2 the youngest (A, on top).
    void PickBed(MlRock r, int age)
    {
        if (r.Order.Contains(age)) return;
        int want = r.Order.Count;
        if (age != want)
        {
            Sfx.Play("wrong");
            r.Note = want == 0 ? (age == 2 ? "The soil lies on top of everything, so it was laid down last. Which layer is at the very bottom?"
                    : "The limestone rests on the dark rock, so the dark rock was there first.")
                : "Not that one yet: it's resting on top of the one you still need. Each layer was laid down on the one below it.";
            return;
        }
        r.Order.Add(age);
        Sfx.Play("blip");
        if (r.Order.Count < 3) { r.Note = ""; return; }
        r.Step = 2;
        Sfx.Play("right");
        NoteMlClue("mr:layers", "Clue: the sea in the stone.");
        r.Note = "Oldest at the bottom, youngest on top, where nothing has disturbed the layers. Volcanic rock first; a reef grew on it under the sea; soil came last, after it was lifted out of the water.";
    }

    static void DrawShell(float x, float y, float r, bool hi)
    {
        Gfx.Circle(x, y, r, Pal.C("#b8ad94"));
        Gfx.Circle(x, y, r - 2, Pal.C("#f6efe0"));
        for (int i = -2; i <= 2; i++) Gfx.Line(x, y + r * 0.7f, x + i * r * 0.35f, y - r * 0.75f, 1.5f, Pal.C("#c9bda2"));
        Gfx.Rect(x - r * 0.35f, y + r * 0.55f, r * 0.7f, r * 0.35f, Pal.C("#d8cdb4"));
    }

    static void DrawFossil(float x, float y, string kind, float k)
    {
        switch (kind)
        {
            case "coral":
                foreach (var (a, l) in new[] { (-0.5f, 22f), (0f, 28f), (0.5f, 20f), (-0.9f, 14f), (0.9f, 14f) })
                    Gfx.Line(x, y + 14, x + MathF.Sin(a) * l, y + 14 - MathF.Cos(a) * l, 5, Pal.C("#d8b4a4"));
                Gfx.Circle(x, y + 14, 5, Pal.C("#c49a8a"));
                break;
            case "urchin":
                Gfx.Circle(x, y, 13, Pal.C("#c9b8a0"));
                for (int i = 0; i < 10; i++) { float a = i * MathF.PI / 5; Gfx.Circle(x + MathF.Cos(a) * 9, y + MathF.Sin(a) * 9, 2, Pal.C("#a8957c")); }
                Gfx.Circle(x, y, 3, Pal.C("#a8957c"));
                break;
            default: DrawShell(x, y, 14 * k, false); break;
        }
    }

    /* ---------- The tide post ---------- */
    void OpenTidePost()
    {
        mlPost = new MlPost();
        OpenMlPanel("mlpost");
    }

    // A tide reading's mark on the post (0 at the foot, 20 at the top), from the sea level (-1..1).
    static int PostMark(float level) => (int)MathF.Round((level + 1) * 10);

    void DrawMlPost()
    {
        var p = mlPost;
        if (p == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame("The tide post off Maniwaya", "Click where the water meets the post, and write the mark down. Sela wants one reading at low tide and one at high tide.");
        if (MlClose(x, y)) return;
        float level = SeaLevel;
        // The view from the sand: the post in the water, Maniwaya's beach, and the sandbar out beyond when the tide's down.
        float px = x + 24, py = y + 102, pw = 520, ph = 420;
        Gfx.Rect(px, py, pw, ph, Pal.C("#bfe0ec"));
        float postX = px + pw * 0.62f, postTop = py + 40, postBot = py + ph - 40, band = (postBot - postTop) / 20;
        float waterY = postBot - (level + 1) * 10 * band;
        // The sandbar, out on the right: only where the sea's below it.
        float barY = postBot - (-0.25f + 1) * 10 * band;
        if (level < -0.22f) Gfx.Rect(px + pw * 0.72f, barY - 6, pw * 0.28f, 12, Pal.C("#ead6a2"), 6);
        Gfx.Rect(px, waterY, pw, py + ph - waterY, Pal.C("#3f8fb0"));
        for (int i = 0; i < 7; i++) Gfx.Rect(px + 10 + i * 74 + MathF.Sin(time * 1.6f + i) * 4, waterY, 26, 2, Pal.C("#e8f6fb"));
        // The beach on the left, the post's rock and the post with its bands.
        Gfx.Triangle(px, py + ph * 0.55f, px + pw * 0.38f, py + ph, px, py + ph, Pal.C("#e8cf96"));
        Gfx.Rect(postX - 24, postBot - 6, 48, 30, Pal.C("#5a5652"), 6);
        for (int i = 0; i < 20; i++)
        {
            Gfx.Rect(postX - 8, postBot - (i + 1) * band, 16, band, i % 2 == 0 ? Pal.C("#f2efe6") : Pal.C("#c0392b"));
            if (i % 2 == 0) Gfx.Text($"{i}", postX + 14, postBot - i * band - 12, FontKind.Ui600, 14, Pal.PaperInk);
        }
        Gfx.Text("20", postX + 14, postTop - 12, FontKind.Ui600, 14, Pal.PaperInk);
        // Water over the post's lower bands.
        Gfx.Rect(postX - 8, waterY, 16, postBot - waterY, Pal.WithAlpha(Pal.C("#3f8fb0"), 0.55f));
#if DEBUG
        Gfx.Seen["mlpost:water"] = new Rectangle(postX - 30, waterY - 4, 60, 8);
        Gfx.Seen["mlpost:post"] = new Rectangle(postX - 30, postTop, 60, postBot - postTop);
        Gfx.Seen["mlpost:bar"] = new Rectangle(px + pw * 0.72f, barY - 8, pw * 0.28f, 16);
#endif
        if (!p.Read && Gfx.Click(px + pw * 0.72f, barY - 10, pw * 0.28f, 20) && level < -0.22f)
        {
            Sfx.Play("wrong");
            p.Note = "The sand's edge shifts with every wave and every storm, so it's no good as a mark. The post stays put: read the water against the post.";
        }
        else if (!p.Read && Gfx.Click(postX - 40, postTop - 10, 80, postBot - postTop + 30))
        {
            if (MathF.Abs(Gfx.Mouse.Y - waterY) > 16)
            {
                Sfx.Play("wrong");
                p.Note = Gfx.Mouse.Y < waterY ? "That's dry post, above the water. Look for where the bands disappear under the water." : "That's under the water. Look for the line where the water meets the post.";
            }
            else RecordTide(p, level);
        }

        // The notebook: the two readings side by side, lined up by the post.
        float rx = px + pw + 24, rw = MlPanelW - (rx - x) - 24, ry = py;
        Gfx.Text("Field notebook", rx, ry, FontKind.Ui700, 22, Pal.PaperInk);
        ry += 32;
        float sw = (rw - 16) / 2, sh = 190;
        for (int k = 0; k < 2; k++)
        {
            string key = k == 0 ? "mr:low" : "mr:high";
            float sx = rx + k * (sw + 16);
            Gfx.Box(sx, ry, sw, sh, Pal.C("#f6efdc"), Pal.Ink, 2, 4);
            Gfx.Text(k == 0 ? "Low tide" : "High tide", sx + 10, ry + 8, FontKind.Ui700, 17, Pal.PaperInk);
            if (!state.Hinted(key)) { Gfx.Text("not yet", sx + 10, ry + 36, FontKind.Ui500, 16, Muted); continue; }
            int mark = state.gifts.GetValueOrDefault(k == 0 ? "mr_lowMark" : "mr_highMark");
            float sy(int m) => ry + sh - 16 - m * (sh - 50) / 20f;
            float bb = sy(0), bt = sy(20);
            Gfx.Rect(sx + sw / 2 - 3, bt, 6, bb - bt, Pal.C("#c0392b"));
            Gfx.Rect(sx + 6, sy(mark), sw - 12, bb - sy(mark) + 8, Pal.WithAlpha(Pal.C("#3f8fb0"), 0.7f));
            if (k == 0) Gfx.Rect(sx + sw - 40, sy(mark) - 4, 34, 6, Pal.C("#ead6a2"), 3);
            Gfx.Text($"mark {mark}", sx + 10, ry + 32, FontKind.Ui600, 16, Pal.PaperInk);
        }
        ry += sh + 14;
        float noteH = y + MlPanelH - 24 - (p.Read ? 62 : 0) - ry;
        MlNote(p.Note, p.Read, rx, ry, rw, noteH);
        if (p.Read && MlDone("Done", x + MlPanelW - 24 - (Gfx.Measure("Done", FontKind.Ui700, 22) + 48), y + MlPanelH - 24 - 50)) { ClosePanels(); return; }
    }

    void RecordTide(MlPost p, float level)
    {
        int mark = PostMark(level);
        p.Read = true;
        Sfx.Play("blip");
        if (level <= TideLowMark)
        {
            state.gifts["mr_lowMark"] = mark;
            NoteMlStep("mr:low");
            p.Note = $"Low tide: the water's down at mark {mark}, and the sandbar's out past the post. ";
        }
        else if (level >= TideHighMark)
        {
            state.gifts["mr_highMark"] = mark;
            NoteMlStep("mr:high");
            p.Note = $"High tide: the water's up at mark {mark}, and the sandbar's gone under. ";
        }
        else
        {
            p.Note = Bind.Fix($"Mark {mark}: the tide's about halfway. Written down, but Sela wants a low tide and a high tide. The clock's tooltip tells the tide, or wait on the sand (<alt>).");
            p.Read = false;
            return;
        }
        if (state.Hinted("mr:low") && state.Hinted("mr:high"))
        {
            NoteMlClue("mr:tide", "Clue: the sandbar that comes and goes.");
            p.Note += "Same post, same rock: the water moved, the post didn't. Here the tide comes in and goes out about twice a day, and the sandbar comes and goes with it. That's the sea rising and falling, not the island.";
        }
        else p.Note += $"Now come back at {(state.Hinted("mr:low") ? "high" : "low")} tide and read it again, from the same spot.";
    }

    /* ---------- The springs: Lorna's night record and the dryer ---------- */
    void OpenSprings()
    {
        mlSprings = new MlSprings { Step = state.Hinted("mr:log") ? 1 : 0 };
        OpenMlPanel("mlsprings");
    }

    // Last night's record from the gauges, hour by hour from 18:00 to 06:00 (the spring outlet, the stream, the air).
    static readonly float[] NightSpring = { 56, 56, 55, 56, 55, 55, 56, 55, 55, 56, 55, 55, 56 };
    static readonly float[] NightStream = { 28, 27.5f, 27, 26.5f, 26, 25.5f, 25, 25, 24.5f, 24.5f, 24, 24, 24.5f };
    static readonly float[] NightAir = { 30, 29, 27.5f, 26.5f, 25.5f, 25, 24.5f, 24, 23.5f, 23.5f, 23, 23, 24 };

    void DrawMlSprings()
    {
        var s = mlSprings;
        if (s == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame(s.Step == 0 ? "Lorna's record from last night" : "The dryer",
            s.Step == 0 ? "Lorna's gauges write down the temperature every hour. Click the line that stayed warm all night, with no sun on it."
            : "Lorna's dryer has a coil of pipe inside it. Connect the coil to one of the two pipes, and see which warms the drying trays.");
        if (MlClose(x, y)) return;
        float gx = x + 74, gy = y + 110, gw = 600, gh = 340;
        if (s.Step == 0)
        {
            // The graph: hours across, degrees up.
            Gfx.Rect(gx, gy, gw, gh, Pal.C("#f6efdc"));
            for (int t = 20; t <= 60; t += 10)
            {
                float ly = gy + gh - (t - 20) / 40f * gh;
                Gfx.Rect(gx, ly, gw, 1, Pal.WithAlpha(Pal.Ink, 0.15f));
                Gfx.Text($"{t} C", gx - 52, ly - 9, FontKind.Ui500, 15, Muted);
            }
            for (int h = 0; h <= 12; h += 3)
            {
                float lx = gx + h / 12f * gw;
                Gfx.Text(HourText((18 * 60 + h * 60) % (24 * 60)), lx - 24, gy + gh + 6, FontKind.Ui500, 14, Muted);
            }
            Gfx.Rect(gx + gw * 2 / 12f, gy, gw * 10 / 12f, gh, Pal.WithAlpha(Pal.C("#1d2f45"), 0.08f));
            Gfx.Text("night", gx + gw * 0.5f, gy + 6, FontKind.Ui600, 15, Muted);
            (string name, float[] v, Color c)[] lines = { ("Hot spring", NightSpring, Pal.C("#c0392b")), ("Stream", NightStream, Pal.C("#2f5d8a")), ("Air", NightAir, Pal.C("#6e767b")) };
            // The nearest line to the pointer is the one clicked (Codex: the last one drawn used to win).
            int hit = -1;
            float hitD = 10;
            for (int k = 0; k < lines.Length; k++)
            {
                var (name, v, c) = lines[k];
                for (int h = 1; h < v.Length; h++)
                {
                    float x0 = gx + (h - 1) / 12f * gw, y0 = gy + gh - (v[h - 1] - 20) / 40 * gh, x1 = gx + h / 12f * gw, y1 = gy + gh - (v[h] - 20) / 40 * gh;
                    Gfx.Line(x0, y0, x1, y1, 4, c);
                    float d = SegDist(Gfx.Mouse.X, Gfx.Mouse.Y, x0, y0, x1, y1);
                    if (d < hitD) { hitD = d; hit = k; }
                }
                Gfx.Text(name, gx + gw + 10, gy + gh - (v[^1] - 20) / 40 * gh - (k == 2 ? -4 : 12), FontKind.Ui700, 15, c);
#if DEBUG
                Gfx.Seen[$"mlsprings:line{k}"] = new Rectangle(gx + gw * 0.5f - 6, gy + gh - (v[6] - 20) / 40 * gh - 6, 12, 12);
#endif
            }
            if (hit >= 0 && Gfx.Click(gx, gy, gw, gh))
            {
                if (hit == 0)
                {
                    Sfx.Play("right");
                    s.LineOk = true;
                    NoteMlStep("mr:log");
                    s.Note = $"The hot spring stayed near {SpringC} degrees all night, while the stream and the air cooled after sunset. No sun is warming it: the heat comes from below, from hot rock deep under Tindig warming the water underground.";
                }
                else { Sfx.Play("wrong"); s.Note = hit == 1 ? "The stream cooled after dark, along with the air. Find the one that didn't." : "The air cooled after sunset. Find the line that stayed warm all night."; }
            }
            // Your own readings beside it.
            float rx = gx + gw + 104, ry = gy;
            Gfx.Text("Your readings", rx, ry, FontKind.Ui700, 18, Pal.PaperInk);
            Gfx.Text($"Spring: {SpringC} C", rx, ry + 30, FontKind.Ui600, 16, Pal.C("#c0392b"));
            Gfx.Text($"Stream: {StreamC} C", rx, ry + 54, FontKind.Ui600, 16, Pal.C("#2f5d8a"));
            float ny = gy + gh + 34, noteW = MlPanelW - 48 - (s.LineOk ? 240 : 0);
            MlNote(s.Note, s.LineOk, x + 24, ny, noteW, y + MlPanelH - 24 - ny);
            if (s.LineOk && MlDone("Next: the dryer", x + MlPanelW - 24 - (Gfx.Measure("Next: the dryer", FontKind.Ui700, 22) + 48), y + MlPanelH - 24 - 50)) { s.Step = 1; s.Note = ""; return; }
            return;
        }
        // The dryer: two pipes, the coil, the cabinet with its trays, and a thermometer.
        float dx = x + 24, dy = y + 110;
        Gfx.Rect(dx, dy, 640, 380, Pal.C("#eef3f0"));
        float cabX = dx + 380, cabY = dy + 70, cabW = 200, cabH = 260;
        Gfx.Box(cabX, cabY, cabW, cabH, Pal.C("#c7ced2"), Pal.Ink, 3, 6);
        for (int i = 0; i < 4; i++) { Gfx.Rect(cabX + 16, cabY + 30 + i * 46, cabW - 32, 6, Pal.C("#8f969b")); for (int k = 0; k < 4; k++) Gfx.Rect(cabX + 26 + k * 40, cabY + 22 + i * 46, 26, 8, Pal.C("#d8a26a"), 3); }
        // The coil at the bottom, and the air rising past the trays when it's warm.
        float coilY = cabY + cabH - 40;
        for (int k = 0; k < 5; k++) Gfx.Circle(cabX + 28 + k * 36, coilY, 12, Pal.C("#8a6a48"));
        bool hot = s.Pipe == 0, connected = s.Pipe >= 0;
        if (connected) s.Flow = Math.Min(1, s.Flow + Raylib.GetFrameTime() * 0.5f);
        float temp = 27 + (hot ? 18 * s.Flow : 0);
        if (hot && s.Flow > 0.3f)
            for (int i = 0; i < 7; i++)
            {
                float k = (time * 0.6f + i / 7f) % 1;
                Gfx.Circle(cabX + 26 + i * 25, coilY - 20 - k * (cabH - 80), 5 * (1 - k) + 2, Pal.Rgba(230, 140, 60, 0.6f * (1 - k)));
            }
        // The two pipes: the hot spring's and the stream's.
        (string name, float py, Color c)[] pipes = { ("From the hot spring", dy + 120, Pal.C("#c0392b")), ("From the stream", dy + 250, Pal.C("#2f5d8a")) };
        for (int k = 0; k < 2; k++)
        {
            var (name, ppy, c) = pipes[k];
            Gfx.Line(dx + 20, ppy, dx + 260, ppy, 12, c);
            bool on = s.Pipe == k || k == 0 && state.Hinted("mr:springs") && s.Pipe < 0;
            Gfx.Text(name, dx + 20, ppy - 36, FontKind.Ui700, 17, c);
            if (on)
            {
                Gfx.Line(dx + 260, ppy, dx + 320, ppy, 12, c);
                Gfx.Line(dx + 320, ppy, dx + 320, coilY, 12, c);
                Gfx.Line(dx + 320, coilY, cabX + 16, coilY, 12, c);
                for (int i = 0; i < 6; i++) { float t = (time * 0.8f + i / 6f) % 1; Gfx.Circle(dx + 20 + t * 240, ppy, 4, Color.White); }
            }
            // The valve to click.
            bool hover = Gfx.Hover(dx + 238, ppy - 22, 44, 44);
            Gfx.Circle(dx + 260, ppy, hover ? 20 : 18, on ? Pal.C("#3f7f5a") : Pal.Sand);
            Gfx.Circle(dx + 260, ppy, 8, Pal.Ink);
#if DEBUG
            Gfx.Seen[$"mlsprings:valve{k}"] = new Rectangle(dx + 238, ppy - 22, 44, 44);
#endif
            if (!state.Hinted("mr:springs") && Gfx.Click(dx + 238, ppy - 22, 44, 44))
            {
                s.Pipe = k; s.Flow = 0;
                if (k == 0)
                {
                    Sfx.Play("right");
                    NoteMlClue("mr:springs", "Clue: heat under Tindig.");
                    s.Note = "The trays warm up: hot water from the spring runs through the coil, the coil warms the air, and the warm air dries the food. The water stays in its pipes and never touches the food. Lorna can dry fish for you now, rain or shine.";
                }
                else { Sfx.Play("wrong"); s.Note = $"Water's flowing through the coil, but the trays stay at about {StreamC} degrees: the stream is only as warm as the day, so it can't dry anything faster than the open air. Try the other pipe."; }
            }
        }
        // The thermometer in the cabinet.
        Gfx.Rect(cabX + cabW + 14, cabY + 20, 18, 200, Pal.C("#f2efe6"), 8);
        Gfx.Rect(cabX + cabW + 19, cabY + 210 - (temp - 20) / 40 * 190, 8, (temp - 20) / 40 * 190, Pal.C("#d8323a"), 3);
        Gfx.Text($"{temp:0} C", cabX + cabW + 4, cabY + 226, FontKind.Ui700, 16, Pal.PaperInk);
        float nx = dx + 660, nw = MlPanelW - (nx - x) - 24;
        MlNote(s.Note, s.Pipe == 0, nx, dy, nw, 320);
        if (state.Hinted("mr:springs") && MlDone("Done", x + MlPanelW - 24 - (Gfx.Measure("Done", FontKind.Ui700, 22) + 48), y + MlPanelH - 24 - 50)) { ClosePanels(); return; }
    }

    /* ---------- Sela's shore model ---------- */
    // What moves a shore, each with how long it usually takes (broad, honest timescales).
    static readonly (string id, string name, string time)[] MlMechanisms =
    {
        ("tide", "The tide", "Hours: here, in and out about twice a day"),
        ("waves", "Waves and storms move sand", "A single storm, or over years"),
        ("land", "The land itself rises", "Usually over thousands of years or more; sometimes all at once, in an earthquake")
    };
    // Your observations, each belonging to one mechanism.
    static readonly (string text, int mech)[] MlObservations =
    {
        ("The water climbed the tide post between my readings, and the sandbar went under", 0),
        ("Mang Ambo: after a storm, the sandbar has a new shape", 1),
        ("Shells and an old wave notch, high on the rock face, far above the sea", 2)
    };

    void OpenModel()
    {
        mlModel = new MlModel();
        // Cards already placed stay placed (each kept as it's placed).
        for (int i = 0; i < 3; i++) if (MrSorted || state.Hinted($"mr:obs:{i}")) mlModel.Placed[i] = MlObservations[i].mech;
        OpenMlPanel("mlmodel");
        if (!MrSorted)
            mlModel.Note = "Aling Sela: \"Try each handle and watch what it moves. Then put each thing you saw beside the handle that explains it.\"";
    }

    void DrawMlModel()
    {
        var m = mlModel;
        if (m == null) { ClosePanels(); return; }
        var (x, y) = MlPanelFrame("Aling Sela's shore model", "Three handles move the model shore. Click one of your notebook cards, then the handle that explains it.");
        if (MlClose(x, y)) return;
        m.T += Raylib.GetFrameTime();
        // The model: a rock base with the old reef in it, sand on top, water to the right, a post and a sandbar.
        float px = x + 24, py = y + 102, pw = 560, ph = 300;
        Gfx.Rect(px, py, pw, ph, Pal.C("#d8ecf2"));
        float lift = m.Playing == "land" ? Math.Min(1, m.T / 3) * 40 : m.Playing == "quake" ? (m.T < 0.25f ? m.T / 0.25f : 1) * 40 : 0;
        float shake = m.Playing == "quake" && m.T < 0.8f ? MathF.Sin(m.T * 80) * 4 * (0.8f - m.T) : 0;
        float sea = py + ph * 0.55f + (m.Playing == "tide" ? MathF.Sin(m.T * 1.6f) * 26 : 0);
        // Lifting the land carries everything on it: the rock, the beach, the post and the bar (Codex).
        float liftNow = m.Playing == "land" ? Math.Min(1, m.T / 3) * 40 : m.Playing == "quake" ? (m.T < 0.25f ? m.T / 0.25f : 1) * 40 : 0;
        float baseTop = py + ph * 0.62f - lift;
        Gfx.Rect(px + shake, baseTop, pw * 0.62f, py + ph - baseTop, Pal.C("#8f8a80"));
        Gfx.Rect(px + shake, baseTop, pw * 0.62f, 26, Pal.C("#e2dcc6"));
        for (int i = 0; i < 6; i++) DrawShell(px + 30 + i * 52 + shake, baseTop + 13, 7, false);
        Gfx.Rect(px + shake, baseTop - 20, pw * 0.5f, 20, Pal.C("#e8cf96"));
        // The sandbar: waves push it about.
        float wave = m.Playing == "waves" ? Math.Min(1, m.T / 2.5f) : 0;
        Gfx.Rect(px + pw * (0.7f + wave * 0.12f), py + ph * 0.6f - liftNow, pw * (0.16f + wave * 0.06f), 14, Pal.C("#ead6a2"), 7);
        Gfx.Rect(px + pw * 0.55f, sea, pw * 0.45f, py + ph - sea, Pal.WithAlpha(Pal.C("#3f8fb0"), 0.85f));
        if (m.Playing == "waves") for (int i = 0; i < 4; i++) { float k = (m.T * 0.8f + i / 4f) % 1; Gfx.Rect(px + pw * (1 - k * 0.45f), sea - 4, 20, 4, Pal.C("#e8f6fb")); }
        // The post.
        float postX = px + pw * 0.66f;
        for (int i = 0; i < 10; i++) Gfx.Rect(postX + shake, py + ph * 0.3f + i * 14 - liftNow, 8, 14, i % 2 == 0 ? Pal.C("#f2efe6") : Pal.C("#c0392b"));
        if (m.Playing == "quake" && m.T > 0.25f) Gfx.Text("Lifted in a moment!", px + 20, py + 16, FontKind.Ui700, 18, Pal.C("#b5523b"));
        if (m.Playing != null && m.T > 4) m.Playing = null;

        // The handles, each with what it moves and how long that takes, and a place for a card.
        float hx = px + pw + 24, hw = MlPanelW - (hx - x) - 24, hy = py;
        for (int k = 0; k < 3; k++)
        {
            var (id, name, when) = MlMechanisms[k];
            float bh = 108;
            bool hot = m.Held >= 0 && Gfx.Hover(hx, hy, hw, bh);
            Gfx.Box(hx, hy, hw, bh, hot ? Lighten(Pal.Sand, 0.2f) : Pal.C("#efe4c8"), Pal.Ink, 2, 6);
            Gfx.Text(name, hx + 12, hy + 8, FontKind.Ui700, 18, Pal.PaperInk);
            int placed = Array.IndexOf(m.Placed, k);
            if (placed >= 0) Lines(Gfx.Wrap("Yours: " + MlObservations[placed].text, FontKind.Ui600, 14, hw - 140).Take(3).ToList(), hx + 12, hy + 34, 17, FontKind.Ui600, 14, Pal.C("#3f6f3a"));
            else Lines(Gfx.Wrap(when, FontKind.Ui500, 15, hw - 140).Take(3).ToList(), hx + 12, hy + 34, 18, FontKind.Ui500, 15, Muted);
            float bx = hx + hw - 116;
            // With a card picked up, the handle's button puts it here (Codex); without, it shows what the handle does.
            if (Button(k == 0 ? "Tide" : k == 1 ? "Waves" : "Lift", bx, hy + 8, 104, 36, FontKind.Ui700, 16, Pal.Sand, Pal.Ink, 3))
            {
                if (m.Held >= 0) PlaceObservation(m, k);
                else { m.Playing = k == 0 ? "tide" : k == 1 ? "waves" : "land"; m.T = 0; Sfx.Play("blip"); }
                return;
            }
            if (k == 2 && Button("Quake", bx, hy + 52, 104, 34, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 3)) { m.Playing = "quake"; m.T = 0; quake = 0.25f; Sfx.Play("clang"); m.Note = "Most of the time the land rises far too slowly to see. But an earthquake can lift a coast in a moment: in 2013 one lifted part of Bohol's coast by more than a metre."; return; }
#if DEBUG
            Gfx.Seen[$"mlmech:{k}"] = new Rectangle(hx, hy + 30, hw - 130, bh - 30);
#endif
            if (m.Held >= 0 && Gfx.Click(hx, hy, hw - 124, bh)) PlaceObservation(m, k);
            hy += bh + 10;
        }
        // Your notebook cards.
        float cy = py + ph + 14, cw = (pw - 20) / 3;
        for (int i = 0; i < 3; i++)
        {
            float cx = px + i * (cw + 10);
            bool placed = m.Placed[i] >= 0, held = m.Held == i;
            Gfx.Box(cx, cy, cw, 92, placed ? Pal.C("#e4f3e0") : held ? Pal.Lantern : Gfx.Hover(cx, cy, cw, 92) ? Lighten(NoteBg, 0.3f) : NoteBg, Pal.Ink, held ? 3 : 2, 6);
            Lines(Gfx.Wrap(MlObservations[i].text, FontKind.Ui600, 15, cw - 20).Take(4).ToList(), cx + 10, cy + 8, 19, FontKind.Ui600, 15, Pal.PaperInk);
#if DEBUG
            Gfx.Seen[$"mlobs:{i}"] = new Rectangle(cx, cy, cw, 92);
#endif
            if (!placed && Gfx.Click(cx, cy, cw, 92)) { m.Held = held ? -1 : i; Sfx.Play("blip"); }
        }
        bool all = m.Placed.All(p => p >= 0);
        float ny = cy + 102;
        MlNote(all ? "All three placed. The tide comes and goes in hours; waves move the sand in a storm or over years; and the rock with the reef in it was lifted out of the sea, mostly slowly, sometimes all at once." : m.Note,
            all, px, ny, MlPanelW - 48 - (all ? 220 : 0), y + MlPanelH - 24 - ny);
        if (all && Button("Show Aling Sela", x + MlPanelW - 24 - 200, y + MlPanelH - 24 - 50, 200, 50, FontKind.Ui700, 20, Pal.Buoy, Color.White, 5))
        {
            ClosePanels();
            Say S(string t) => new("Aling Sela", t);
            Talk(new()
            {
                S("Hours, years, ages. The tide comes and goes about twice a day; waves shift the sand in a storm or over years; the rock itself was lifted from the sea floor, mostly slowly."),
                S("Mostly: an earthquake can lift a coast in a moment. In 2013 one lifted part of Bohol's coast by more than a metre."),
                S("So Lola's story got something right: this island did rise from the sea. Just not in one night."),
                S("Now, the last job, and my favourite: the chart. My grandson Jun has gone up Tindig with my heliotrope. Climb the trail from the hot springs and help him set it up.")
            });
            return;
        }
    }

    void PlaceObservation(MlModel m, int k)
    {
        int o = m.Held;
        if (MlObservations[o].mech == k)
        {
            m.Placed[o] = k; m.Held = -1; Sfx.Play("right"); m.Note = "";
            NoteMlStep($"mr:obs:{o}");
            // All three placed is kept at once, whether or not you then show Aling Sela.
            if (m.Placed.All(p => p >= 0) && !MrSorted) { NoteMlStep("mr:theory"); GainXp(15); }
            return;
        }
        m.Playing = MlMechanisms[k].id; m.T = 0; m.Held = -1; Sfx.Play("wrong");
        m.Note = k switch
        {
            0 => "Watch: the tide lifts the water and lets it fall, but it doesn't reshape the sand or lift any rock.",
            1 => "Watch: waves push the loose sand about, but the post's bands and the rock stay where they are.",
            _ => "Watch: lifting the land carries the rock, the shells, the beach and the post up together. That's not what you saw there."
        };
    }

    /* ---------- Sighting from a station ---------- */
    // What you can see from a station: the far station's striped target, the islets, Tindig with Jun's flash, and so on,
    // each at its true bearing and with its real angular size.
    IEnumerable<(string id, string name, float x, float y, float r, string kind)> MlLandmarks(string from)
    {
        if (from == "A") yield return ("B", "the striped target on Baltasar", TargetAt.x, TargetAt.y, 2, "target");
        else yield return ("A", "the survey stone's hill", DatumX, DatumY, 30, "hill");
        yield return ("C", "Jun's flash on Tindig's top", TindigX, TindigY, 2, "flash");
        yield return ("tindig", "Bundok Tindig", TindigX, TindigY + 10, TindigRX * MlS * T, "mountain");
        string[] tres = { "Gaspar", "Melchor", "Baltasar" };
        for (int i = 4; i < MlIslets.Length; i++)
        {
            var s = MlIslets[i];
            if (from == "B" && tres[i - 4] == "Baltasar") continue;
            yield return (tres[i - 4].ToLowerInvariant(), tres[i - 4], MlWx(s.cx), MlWy(s.cy), s.rx * MlS * T, "islet");
        }
        foreach (var (name, i) in new[] { ("Polo", 0), ("Maniwaya", 1), ("Mompong", 2), ("Salomague", 3) })
            yield return (name.ToLowerInvariant(), name, MlWx(MlIslets[i].cx), MlWy(MlIslets[i].cy), MlIslets[i].rx * MlS * T, "islet");
    }

    (float x, float y) StationAt(string s) => s == "A" ? (DatumX, DatumY) : (BaltasarX, BaltasarY);
    float TrueBearing(string from, string to)
    {
        var (fx, fy) = StationAt(from);
        var (tx, ty) = to == "B" ? TargetAt : to == "A" ? (DatumX, DatumY) : (TindigX, TindigY);
        return BearingTo(fx, fy, tx, ty);
    }
    const float SightTolerance = 1.5f, ScopeFov = 40;

    void OpenScope(string station)
    {
        string want = station == "A" ? (!state.Hinted("mr:ab") ? "B" : "C") : "C";
        // Start pointing off to one side, never straight at the answer.
        mlScope = new MlScope { Station = station, Want = want, Bearing = (TrueBearing(station, want) + (station == "A" ? 35 : -40) + 360) % 360 };
        OpenMlPanel("mlscope");
    }

    void DrawMlScope()
    {
        var s = mlScope;
        if (s == null) { ClosePanels(); return; }
        string from = s.Station == "A" ? "the survey stone (A)" : "Baltasar's mark (B)";
        string want = s.Want == "B" ? "the striped target on Baltasar (B)" : "Jun's flash on Tindig's top (C)";
        var (x, y) = MlPanelFrame($"Sighting from {from}", Inp.UsingPad ? $"Turn the sight (the right stick, or the buttons) until {want} sits on the red line, then record the bearing."
            : $"Turn the sight (arrow keys, the buttons, or drag the view) until {want} sits on the red line, then record the bearing.");
        if (MlClose(x, y)) return;
        // Turning comes first, so what's on the line is judged at the bearing you see.
        float dt = Raylib.GetFrameTime();
        // On a pad the left stick steers the pointer, so the right stick turns the sight; on a keyboard, the arrows.
        bool turnL = Inp.UsingPad ? Inp.RightX < -0.3f : Bind.Down("left"), turnR = Inp.UsingPad ? Inp.RightX > 0.3f : Bind.Down("right");
        if (turnL) { s.Turn = Math.Min(30, s.Turn + dt * 40); s.Bearing = (s.Bearing - (2 + s.Turn) * dt + 360) % 360; s.Note = ""; }
        else if (turnR) { s.Turn = Math.Min(30, s.Turn + dt * 40); s.Bearing = (s.Bearing + (2 + s.Turn) * dt) % 360; s.Note = ""; }
        else s.Turn = 0;
        float vx = x + 24, vy = y + 110, vw = MlPanelW - 48, vh = 280;
        if (Gfx.Pressed && Gfx.Hover(vx, vy, vw, vh - 26)) { s.DragFrom = Gfx.Mouse.X; s.DragBearing = s.Bearing; }
        if (!Gfx.Down) s.DragFrom = float.NaN;
        if (!float.IsNaN(s.DragFrom)) s.Bearing = (s.DragBearing - (Gfx.Mouse.X - s.DragFrom) / vw * ScopeFov + 720) % 360;
        float by = vy + vh + 16, bx = vx + 330;
        bool adjusted = false;
        foreach (var (lab, step) in new[] { ("-5", -5f), ("-1", -1f), ("-0.1", -0.1f), ("+0.1", 0.1f), ("+1", 1f), ("+5", 5f) })
        {
            if (Button(lab, bx, by, 60, 40, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3)) { s.Bearing = (s.Bearing + step + 360) % 360; s.Note = ""; adjusted = true; }
            bx += 66;
        }
#if DEBUG
        lastMlScopeBearing = s.Bearing;
#endif
        var (sx, sy) = StationAt(s.Station);
        // The view: sky, the far sea, and the land in front where your line of sight crosses Marilaya (clipped to its frame).
        var clip = Gfx.S(vx, vy, vw, vh);
        Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)MathF.Ceiling(clip.Width), (int)MathF.Ceiling(clip.Height));
        Gfx.Rect(vx, vy, vw, vh, Pal.C(SightingLight ? "#a8d6ec" : "#2a3a58"));
        float horizon = vy + vh * 0.55f;
        Gfx.Rect(vx, horizon, vw, vy + vh - horizon, Pal.C("#3f8fb0"));
        float PxOf(float bearing) => vx + vw / 2 + AngleDiff(bearing, s.Bearing) / ScopeFov * vw;
        for (int i = 0; i < vw; i += 4)
        {
            float b = s.Bearing + (i - vw / 2) / vw * ScopeFov;
            float rad = b * MathF.PI / 180;
            // How far the land runs towards the horizon on that bearing (the nearest 40 tiles).
            float landTo = 0;
            for (float d = 8; d < 400; d += 8)
            {
                float wx = sx + MathF.Sin(rad) * d, wy = sy - MathF.Cos(rad) * d;
                int tx = (int)(wx / T), ty = (int)(wy / T);
                if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS) break;
                if (!WaterTile(worldMap[ty, tx])) landTo = d;
            }
            if (landTo > 0) Gfx.Rect(vx + i, horizon + 6 + (1 - Math.Min(1, landTo / 400)) * 60, 4, vy + vh - horizon - 6 - (1 - Math.Min(1, landTo / 400)) * 60, Pal.C("#5d9b45"));
        }
        // The far things on the horizon, smaller the further they are: the land first, then the target and the flash on top.
        string onCross = null;
        foreach (var (id, name, lx, ly, r, kind) in MlLandmarks(s.Station).OrderBy(l => l.kind is "target" or "flash" ? 1 : 0))
        {
            float b = BearingTo(sx, sy, lx, ly), dist = Dist(sx, sy, lx, ly);
            float half = MathF.Max(0.6f, MathF.Atan2(r, dist) * 180 / MathF.PI);
            float cx = PxOf(b), hw = half / ScopeFov * vw, near = 300 / Math.Max(300, dist);
            if (cx + hw < vx || cx - hw > vx + vw) continue;
            switch (kind)
            {
                case "islet": Gfx.Rect(cx - hw, horizon - 14 * near, hw * 2, 16 * near + 2, Pal.C("#4f8a3e"), 8); break;
                case "hill": Gfx.Triangle(cx - hw, horizon, cx, horizon - 30, cx + hw, horizon, Pal.C("#4f8a3e")); break;
                case "mountain": Gfx.Triangle(cx - hw, horizon + 4, cx, horizon - 120 * (400 / Math.Max(400, dist)), cx + hw, horizon + 4, Pal.C("#3f7d3a")); break;
                case "target":
                    for (int i = 0; i < 6; i++) Gfx.Rect(cx - 2, horizon - 26 + i * 4, 4, 4, i % 2 == 0 ? Pal.C("#f2efe6") : Pal.C("#c0392b"));
                    Gfx.Rect(cx - 5, horizon - 30, 10, 4, Pal.C("#f2efe6"));
                    break;
                case "flash":
                    if (MirrorFlashing && (int)(time * 2.5f) % 3 != 0)
                    {
                        float my = horizon - 120 * (400 / Math.Max(400, dist)) + 2;
                        Gfx.Circle(cx, my, 9, Pal.Rgba(255, 252, 220, 0.9f)); Gfx.Circle(cx, my, 4, Color.White);
                    }
                    break;
            }
            float off = MathF.Abs(AngleDiff(b, s.Bearing));
            if (kind is "target" or "flash" ? off <= SightTolerance : off <= half && onCross == null) onCross = id;
        }
        Raylib.EndScissorMode();
        // The cross, and the compass strip.
        Gfx.Line(vx + vw / 2, vy, vx + vw / 2, vy + vh, 2, Pal.C("#b5523b"));

        Gfx.Rect(vx, vy + vh - 26, vw, 26, Pal.WithAlpha(Pal.Ink, 0.55f));
        for (int d = 0; d < 360; d += 5)
        {
            float tx = PxOf(d);
            if (tx < vx + 12 || tx > vx + vw - 12) continue;
            Gfx.Rect(tx, vy + vh - (d % 15 == 0 ? 14 : 8), 1, d % 15 == 0 ? 14 : 8, Pal.Paper);
            string lab = d switch { 0 => "N", 90 => "E", 180 => "S", 270 => "W", _ => d % 15 == 0 ? $"{d}" : null };
            if (lab != null) Gfx.TextCenter(lab, tx, vy + vh - 26, FontKind.Ui600, 13, Pal.Paper);
        }
#if DEBUG
        Gfx.Seen["mlscope:view"] = new Rectangle(vx, vy, vw, vh);
#endif
        Gfx.Text($"Bearing {s.Bearing:0.0} degrees", vx, by + 4, FontKind.Ui700, 26, Pal.PaperInk);
        Gfx.Text("clockwise from north", vx, by + 36, FontKind.Ui500, 16, Muted);
        // E records too, on a keyboard; on a pad, A is the pointer's click, so it only records through the button.
        bool keyRecord = MlKeysLive && !Inp.UsingPad && !adjusted && Bind.Pressed("act");
        if (MlDone("Record bearing", x + MlPanelW - 24 - (Gfx.Measure("Record bearing", FontKind.Ui700, 22) + 48), by) || keyRecord) { RecordSight(s, onCross); return; }
        float ny = by + 62;
        bool waitSun = s.Want == "C" && state.Hinted("mr:mirror") && !MirrorFlashing && SightingLight;
        MlNote(s.Note, s.Good, vx, ny, vw - (waitSun ? 230 : 0), y + MlPanelH - 24 - ny);
        if (waitSun && Button("Wait for the sun", x + MlPanelW - 24 - 210, ny + 4, 210, 46, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 4)) { WaitForSun(); return; }
    }

    void RecordSight(MlScope s, string onCross)
    {
        float truth = TrueBearing(s.Station, s.Want), err = AngleDiff(s.Bearing, truth);
        if (s.Want == "C" && !state.Hinted("mr:mirror"))
        {
            Sfx.Play("wrong"); s.Good = false;
            s.Note = "There's nothing on Tindig's top to sight yet. Climb the trail from the hot springs and help Jun set up the heliotrope first.";
            return;
        }
        if (s.Want == "C" && !MirrorFlashing)
        {
            Sfx.Play("wrong"); s.Good = false;
            s.Note = !SightingLight ? "It's too dark to see Jun's flash. Come back by daylight." : "No flash on Tindig: Jun's mirror needs the sun, and it's cloudy. Wait for a clear sky.";
            return;
        }
        if (MathF.Abs(err) > SightTolerance)
        {
            Sfx.Play("wrong"); s.Good = false;
            var names = MlLandmarks(s.Station).ToDictionary(l => l.id, l => l.name);
            string what = onCross != null && onCross != s.Want && names.TryGetValue(onCross, out var nm) ? $"That's {nm}, not {(s.Want == "B" ? "the target on Baltasar" : "Jun's flash")}. " : "";
            if (s.Want == "B" && onCross is "gaspar" or "melchor") what += "The station's on Baltasar, the smallest and farthest of the three, with the striped pole. ";
            s.Note = what + $"Your sight is {MathF.Abs(err):0.0} degrees {(err > 0 ? "clockwise of" : "anticlockwise of")} it: turn {(err > 0 ? "left" : "right")} a little and record again.";
            return;
        }
        Sfx.Play("right"); s.Good = true;
        string key = s.Station == "A" ? (s.Want == "B" ? "mr:ab" : "mr:ac") : "mr:bc";
        state.gifts[key.Replace(":", "_")] = (int)MathF.Round(s.Bearing * 10);
        NoteMlStep(key);
        if (key == "mr:ab")
        {
            s.Note = $"Recorded: Baltasar's target at {s.Bearing:0.0} degrees. That fixes which way the chart points. Now find Jun's flash on Tindig (C).";
            s.Want = "C"; s.Bearing = (TrueBearing("A", "C") - 30 + 360) % 360;
        }
        else if (key == "mr:ac") s.Note = $"Recorded: Tindig's top at {s.Bearing:0.0} degrees. One bearing gives a line the summit is somewhere on, but not where. Now sail to Baltasar (B) and sight it from there.";
        else s.Note = $"Recorded: Tindig's top at {s.Bearing:0.0} degrees from Baltasar. Take your bearings to Aling Sela at the harbour, and plot them.";
    }

    // Waiting for a clear, sunny sky at a station: the clock runs forward the ordinary way, half an hour at a time.
    void WaitForSun()
    {
        float waited = 0;
        ClosePanels();
        FadeThrough(() =>
        {
            for (int i = 0; i < 60 && !SunReady; i++) { Advance(30, quiet: true); waited += 30; }
            state.food = Math.Max(0, state.food - Math.Min(10, waited / 60));
            Save();
        }, () => Toast(!SunReady ? "Still no sun. Try again later." : MirrorFlashing ? $"You wait {(int)(waited / 60)} h {(int)(waited % 60)} min. The sun's out: Jun's mirror is flashing."
            : $"You wait {(int)(waited / 60)} h {(int)(waited % 60)} min. It's light enough to sight now.", 5));
    }
    // Light to sight by, and the flash too once the heliotrope's up.
    bool SunReady => SightingLight && (!state.Hinted("mr:mirror") || MirrorFlashing);

    /* ---------- Plotting the chart with Sela ---------- */
    void OpenPlot()
    {
        mlPlot = new MlPlot();
        OpenMlPanel("mlplot");
    }

    static Vector2 MlChartPt(float wx, float wy, float cx, float cy, float k) => new(cx + (wx / T - MlBoxX0) * k, cy + (wy / T - MlBoxY0) * k);

    void DrawMlPlot()
    {
        var p = mlPlot;
        if (p == null) { ClosePanels(); return; }
        const float w = 1140, h = 640;
        var (x, y) = MlPanelFrame("Plotting the chart", "With Aling Sela: draw each bearing as a line from its station.", w, h);
        if (MlClose(x, y, w)) return;
        p.T += Raylib.GetFrameTime();
        float k = Math.Min(560f / (MlBoxX1 - MlBoxX0), 510f / (MlBoxY1 - MlBoxY0)), cx = x + 24, cy = y + 100, cw = (MlBoxX1 - MlBoxX0) * k, ch = (MlBoxY1 - MlBoxY0) * k;
        Gfx.Rect(cx, cy, cw, ch, Pal.C("#2f6f95"));
        for (int ty = MlBoxY0; ty < MlBoxY1; ty++)
            for (int tx = MlBoxX0; tx < MlBoxX1; tx++)
            {
                char t = worldMap[ty, tx];
                if (t is '~') continue;
                var col = t == 'w' ? Pal.C("#4ea3bf") : t is 's' or 'd' ? Pal.C("#e8cf96") : t == 'l' ? Pal.C("#2a9d8f") : t == 'e' ? Pal.C("#c1bcb0") : Pal.C("#5d9b45");
                Gfx.Rect(cx + (tx - MlBoxX0) * k, cy + (ty - MlBoxY0) * k, k + 0.5f, k + 0.5f, col);
            }
        var A = MlChartPt(DatumX, DatumY, cx, cy, k); var B = MlChartPt(BaltasarX, BaltasarY, cx, cy, k);
        float ab = state.gifts.GetValueOrDefault("mr_ab") / 10f, ac = state.gifts.GetValueOrDefault("mr_ac") / 10f, bc = state.gifts.GetValueOrDefault("mr_bc") / 10f;
        void Ray(Vector2 from, float bearing, Color col, float grow)
        {
            float r = bearing * MathF.PI / 180, len = 900 * Math.Clamp(grow, 0, 1);
            var dir = new Vector2(MathF.Sin(r), -MathF.Cos(r));
            for (float t = 0; t < len; t += 4)
            {
                var a = from + dir * t;
                if (a.X < cx || a.Y < cy || a.X > cx + cw || a.Y > cy + ch) break;
                Gfx.Rect(a.X - 1.5f, a.Y - 1.5f, 3, 3, col);
            }
        }
        Ray(A, ab, Pal.C("#f3c25b"), 1);
        if (p.Step >= 1) Ray(A, ac, Pal.C("#ff8a7a"), p.Step == 1 ? p.T * 1.5f : 1);
        if (p.Step >= 2) Ray(B, bc, Pal.C("#ff8a7a"), p.T * 1.5f);
        foreach (var (pt, lab) in new[] { (A, "A"), (B, "B") })
        {
            Gfx.Circle(pt.X, pt.Y, 7, Pal.Ink); Gfx.Circle(pt.X, pt.Y, 5, Pal.C("#f3c25b"));
            Gfx.Text(lab, pt.X + 9, pt.Y - 10, FontKind.Ui700, 18, Color.White);
        }
        if (p.Step >= 2 && p.T > 0.8f && Intersect(DatumX, DatumY, ac, BaltasarX, BaltasarY, bc) is var (ix, iy))
        {
            var I = MlChartPt(ix, iy, cx, cy, k);
            float pulse = 1 + 0.2f * MathF.Sin(time * 5);
            Gfx.Circle(I.X, I.Y, 11 * pulse, Pal.C("#ff8a7a")); Gfx.Circle(I.X, I.Y, 6, Color.White);
            Gfx.Text("C", I.X + 12, I.Y - 12, FontKind.Ui700, 20, Color.White);
        }
        Gfx.Text("N", cx + cw - 22, cy + 6, FontKind.Ui700, 20, Color.White);
        Gfx.Triangle(cx + cw - 22, cy + 34, cx + cw - 16, cy + 26, cx + cw - 10, cy + 34, Color.White);
        Gfx.Line(cx + cw - 16, cy + 30, cx + cw - 16, cy + 52, 3, Color.White);
        float rx = cx + cw + 24, rw = w - (rx - x) - 24, ry = cy;
        string[] say =
        {
            $"The stone (A) and Baltasar (B) are where we know they are. Your first bearing, {ab:0.0} degrees from A to B, sets which way the chart points.",
            $"Your bearing to Tindig's top from the stone: {ac:0.0} degrees. One bearing gives a whole line: the summit is somewhere along it, but where?",
            $"And from Baltasar: {bc:0.0} degrees. Two lines from two known stations cross at one place: that's the summit. That's triangulation."
        };
        var lines = Gfx.Wrap("Aling Sela: \"" + say[p.Step] + "\"", FontKind.Note, 19, rw);
        Lines(lines, rx, ry, 26, FontKind.Note, 19, Pal.PaperInk);
        ry += lines.Count * 26 + 20;
        string next = p.Step == 0 ? "Draw the line from A" : p.Step == 1 ? "Draw the line from B" : "Ink the chart";
        if (MlDone(next, rx, ry) && (p.Step < 2 || p.T > 0.8f))
        {
            if (p.Step < 2) { p.Step++; p.T = 0; Sfx.Play("blip"); return; }
            ClosePanels();
            FinishMarilaya();
            return;
        }
    }
}
