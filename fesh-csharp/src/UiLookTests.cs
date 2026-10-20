#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // One look for the panels, the icons and the chart (FESH_UILOOK_TEST=1, also in the full run): the shared paper box, an
    // outline and whole-pixel size for every icon, hand-inked coasts and place pictures on the chart.
    IEnumerable<int> UiLookScript()
    {
        Note("UI look: panels, icons, chart");
        state = new State { created = true, look = new Look { name = "Draftsman" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;

        /* ---------- the paper box ---------- */
        Color boxEdge = default, boxInside = default, boxTop = default, boxInner = default;
        screenDraw = () => { Gfx.Rect(60, 80, 520, 380, Pal.C("#404040")); PanelBox(100, 120, 400, 300); };
        screenProbe = im => { boxInner = ScreenAt(im, 108, 270); boxInside = ScreenAt(im, 140, 270); boxTop = ScreenAt(im, 200, 123); boxEdge = ScreenAt(im, 101, 270); };
        yield return 3;
        Check($"the shared box is paper with a fine ink line a little inside it ({boxInside.R},{boxInside.G},{boxInside.B} against the line {boxInner.R},{boxInner.G},{boxInner.B})",
            Math.Abs(boxInside.R - Pal.Paper.R) <= 2 && boxInner.R < boxInside.R - 8 && boxEdge.R < 80);
        Check("and a pale edge along its top", boxTop.R >= boxInside.R - 2 && boxTop.B >= boxInside.B - 2);
        // The real panels use it.
        int boxCalls0 = PanelBoxCalls;
        TogglePanel("bag"); yield return 4;
        Note($"after opening the bag: mode {mode}, panel {panel}");
        var (bx, by, bw, bh) = panelAt;
        Color inner = default, fill = default;
        screenProbe = im => { inner = ScreenAt(im, bx + 7, by + bh / 2); fill = ScreenAt(im, bx + 60, by + bh / 2); };
        yield return 3;
        pendingShot = "uilook-bag"; yield return 2;
        Note($"bag panel at {bx},{by} {bw}x{bh}: line {inner.R},{inner.G},{inner.B}, fill {fill.R}");
        Check($"the bag, a real panel, is drawn on the shared box ({PanelBoxCalls - boxCalls0} draws in a few frames)", PanelBoxCalls - boxCalls0 >= 3);
        ClosePanels(); yield return 3;

        /* ---------- icons ---------- */
        var ids = Items.ById.Keys.Concat(FishArt.Looks.Keys).Distinct().ToList();
        int bad = 0, noOutline = 0, ringBad = 0, empty = 0;
        string firstBad = null;
        foreach (var id in ids)
        {
            var tex = ItemArt.Icon(id);
            var img = LoadImageFromTexture(tex);
            if (img.Width != ItemArt.Box || img.Height != ItemArt.Box) { bad++; firstBad ??= $"{id} is {img.Width}x{img.Height}"; UnloadImage(img); continue; }
            var px = new Color[ItemArt.Box * ItemArt.Box];
            for (int y = 0; y < ItemArt.Box; y++) for (int x = 0; x < ItemArt.Box; x++) px[y * ItemArt.Box + x] = GetImageColor(img, x, y);
            UnloadImage(img);
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < ItemArt.Box && y < ItemArt.Box && px[y * ItemArt.Box + x].A >= 128;
            bool IsInk(Color c) => c.A == ItemArt.OutlineAlpha;
            int art = 0, ink = 0;
            for (int y = 0; y < ItemArt.Box; y++)
                for (int x = 0; x < ItemArt.Box; x++)
                {
                    var c = px[y * ItemArt.Box + x];
                    if (c.A < 128) continue;
                    if (IsInk(c)) ink++; else art++;
                    // The border row and column hold only outline.
                    if ((x == 0 || y == 0 || x == ItemArt.Box - 1 || y == ItemArt.Box - 1) && !IsInk(c)) { ringBad++; firstBad ??= $"{id} paints the outer ring"; }
                }
            if (art == 0) { empty++; firstBad ??= $"{id} is empty"; }
            if (ink < 4) { noOutline++; firstBad ??= $"{id} has no outline"; }
        }
        Check($"every item and fish icon ({ids.Count}) is a 14x14 picture with an outline and nothing in its outer ring ({firstBad ?? "all fine"})", bad == 0 && noOutline == 0 && ringBad == 0 && empty == 0 && ids.Count > 200);
        // Whole screen pixels per art pixel at any size asked for.
        bool whole = true; var odd = new List<string>();
        foreach (float size in new[] { 12f, 14, 18, 20, 22, 24, 28, 32, 34, 36, 40, 48, 60, 72 })
        {
            var (x, y, d) = IconBox(100, 100, size, out float k);
            if (k != MathF.Floor(k) || k < 1 || MathF.Abs(d - 14 * k) > 0.001f || MathF.Abs((x + k + 12 * k / 2) - (100 + size / 2)) > 0.001f) { whole = false; odd.Add($"{size}"); }
        }
        Check($"icons are drawn at a whole number of screen pixels per art pixel and stay centred ({string.Join(",", odd)})", whole);
        var (_, _, d48) = IconBox(0, 0, 48, out float k48);
        Check("the picture stays where it always was (48 is 4 px per art pixel, its outline spills 4 px)", k48 == 4 && d48 == 56);
        // A contact sheet to look at: tools, materials, food and fish on paper, at the bag's size.
        var sheet = new[] { "old_rod", "axe", "pickaxe", "sword", "stone", "wood", "copper_bar", "iron_bar", "gold_bar", "crystal", "coin", "berries", "bait", "worm", "pearl", "grilled_fish", "silver_tuna", "pond_perch", "bangus", "alimasag" }.Where(Items.ById.ContainsKey).Concat(FishArt.Looks.Keys.Take(20)).Take(40).ToList();
        screenDraw = () => { Gfx.Rect(40, 60, 700, 330, Pal.Paper); for (int i = 0; i < sheet.Count; i++) DrawIcon(sheet[i], 70 + i % 10 * 68, 80 + i / 10 * 76, 48); };
        screenProbe = im => { };
        pendingShot = "uilook-icons"; yield return 3;
        // On the screen the outline shows: a bag item over the paper has dark pixels round it.
        Color ring = default, middle = default; bool ringChecked = false;
        screenDraw = () => { Gfx.Rect(60, 80, 300, 200, Pal.Paper); DrawIcon("stone", 100, 100, 48); };
        screenProbe = im =>
        {
            // The stone sits in a 48 px square; one art pixel (4 px) outside it is outline wherever the stone reaches the edge.
            // The outline spills 4 px outside the 48 px square (96..100 on the left, 148..152 on the right).
            int dark = 0;
            for (int yy = 96; yy < 152; yy++) for (int xx = 96; xx < 152; xx++) { var c = ScreenAt(im, xx, yy); if (Math.Abs(c.R - Pal.Paper.R) + Math.Abs(c.G - Pal.Paper.G) + Math.Abs(c.B - Pal.Paper.B) > 90) dark++; }
            ring = new Color((byte)Math.Min(255, dark), (byte)0, (byte)0, (byte)255); ringChecked = true;
        };
        yield return 3;
        Check($"an icon on the screen is outlined in dark ({ring.R} clearly darker pixels in and round the stone)", ringChecked && ring.R > 20);

        /* ---------- the chart ---------- */
        state.charted = new List<string> { "saltmere", "frost", "mirewood" };
        mapTexDirty = true; TogglePanel("map"); yield return 6;
        pendingShot = "uilook-chart"; yield return 3;
        var mimg = LoadImageFromTexture(mapTex);
        Color Chart(int wx, int wy) => GetImageColor(mimg, wx, wy);
        // Find a Saltmere coast: a land pixel with open sea to its right and land well inside to its left.
        (int x, int y) coast = (-1, -1);
        for (int wy = 40; wy < 150 && coast.x < 0; wy++)
            for (int wx = 20; wx < 300; wx++)
            {
                if (Wet(shape[wy * PW + wx])) continue;
                if (!Wet(shape[wy * PW + wx + 1]) || !Wet(shape[wy * PW + wx + 2]) || !Wet(shape[wy * PW + wx + 6]) || !Wet(shape[wy * PW + wx + 12]) || !OpenSeaTiles()[wy / T, (wx + 6) / T]) continue;
                bool inland = shape[wy * PW + wx] is 'g' or 's'; for (int i = 1; i <= 8; i++) inland &= shape[wy * PW + wx - i] is 'g' or 's';
                if (inland && BiomeAt(wx / T, wy / T) == 0) { coast = (wx, wy); break; }
            }
        Check($"found a coast to look at ({coast.x},{coast.y})", coast.x > 0);
        if (coast.x > 0)
        {
            int Lum(Color c) => c.R + c.G + c.B;
            var onCoast = Chart(coast.x, coast.y); var inland = Chart(coast.x - 8, coast.y);
            Note($"chart image {mimg.Width}x{mimg.Height}, format {mimg.Format}: coast {onCoast.R},{onCoast.G},{onCoast.B} inland {inland.R},{inland.G},{inland.B}");
            Check($"the coast of a charted island is inked ({Lum(onCoast)} against {Lum(inland)} a little inland)", Lum(onCoast) < Lum(inland) - 90);
            var shadow = Chart(coast.x + 1, coast.y + 1); var farSea = Chart(coast.x + 30, coast.y);
            Note($"coast {Lum(onCoast)}, shadow {Lum(shadow)}, sea {Lum(farSea)}, inland {Lum(inland)}");
            var baseSh = worldBase.Buf[(coast.y + 1) * PW + coast.x + 1];
            int baseLum = baseSh.R + baseSh.G + baseSh.B, paperLum = 0xef + 0xe2 + 0xc0, expected = baseLum + (paperLum - baseLum) / 10;
            Check($"with a faint shadow on the water just past it ({Lum(shadow)} against {expected} un-inked)", Lum(shadow) < expected - 12);
        }
        // Uncharted land is still plain parchment (the old colour range) and grain stays small.
        state.charted = new List<string> { "saltmere" }; mapTexDirty = true; yield return 4;
        UnloadImage(mimg); mimg = LoadImageFromTexture(mapTex);
        var fogged = GetImageColor(mimg, 620, 90);   // Frostfang, not charted now
        Check($"uncharted land is still parchment ({fogged.R},{fogged.G},{fogged.B})", fogged.R > 180 && fogged.G > 160 && fogged.B > 110 && fogged.B < 185);
        UnloadImage(mimg);
        ClosePanels(); yield return 3;
        state.charted = null;

        // The place pictures: eight, each different by shape alone (white fill, ink ignored).
        var masks = new List<bool[,]>();
        bool[,] Mask(Image im, float cx, float cy)
        {
            var m = new bool[30, 30];
            for (int j = 0; j < 30; j++) for (int i = 0; i < 30; i++) { var c = ScreenAt(im, cx - 15 + i, cy - 15 + j); m[i, j] = c.R + c.G + c.B > 600; }
            return m;
        }
        screenDraw = () =>
        {
            Gfx.Rect(40, 300, 760, 120, Pal.C("#404040"));
            for (int g = 1; g <= 8; g++) MapGlyph(80 + g * 80, 360, g, Color.White, 1.2f);
        };
        screenProbe = im => { for (int g = 1; g <= 8; g++) masks.Add(Mask(im, 80 + g * 80, 360)); };
        yield return 3;
        int Diff(bool[,] a, bool[,] b) { int n = 0; for (int j = 0; j < 30; j++) for (int i = 0; i < 30; i++) if (a[i, j] != b[i, j]) n++; return n; }
        int minDiff = int.MaxValue; string pair = "";
        for (int a = 0; a < masks.Count; a++) for (int b = a + 1; b < masks.Count; b++) { int df = Diff(masks[a], masks[b]); if (df < minDiff) { minDiff = df; pair = $"{a + 1}-{b + 1}"; } }
        Check($"the eight place pictures are all drawn and differ from each other by shape (closest pair {pair}: {minDiff} pixels)", masks.Count == 8 && masks.All(m => m.Cast<bool>().Count(x => x) > 25) && minDiff >= 14);
        // They don't take the shapes of the goal, the pin or a closed spot (squares, diamonds, crossed dots).
        var accessMasks = new List<bool[,]>();
        screenDraw = () =>
        {
            Gfx.Rect(40, 300, 760, 120, Pal.C("#404040"));
            for (int k = 1; k <= 3; k++) MapShape(80 + k * 80, 360, k, Color.White, 6);
        };
        screenProbe = im => { for (int k = 1; k <= 3; k++) accessMasks.Add(Mask(im, 80 + k * 80, 360)); };
        yield return 3;
        int closest = int.MaxValue;
        foreach (var am in accessMasks) foreach (var gm in masks) closest = Math.Min(closest, Diff(am, gm));
        Check($"and none looks like the goal square, the pin diamond or a closed crossed dot ({closest} pixels apart at least)", accessMasks.Count == 3 && closest >= 14);

        pendingShot = "uilook-done"; yield return 2;
    }
}
#endif
