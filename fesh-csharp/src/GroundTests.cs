#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The ground pass (FESH_GROUND_TEST=1, also at the end of the full run after the water script): land detail in the rich
    // layer, tree tints, the seasons and trodden paths as they are added. Light is on for these checks; the other scripts'
    // off state is restored at the end.
    IEnumerable<int> GroundScript()
    {
        Note("Ground: land detail in the rich layer");
        state = new State { created = true, look = new Look { name = "Gardener" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 4; Inp.ScriptMouse = Offscreen; yield return 5;
        bool keepOff = EffectsOff;
        EffectsOff = false; Settings.Data.light = 2;

        /* ---------- Phase A: land detail ---------- */
        {
            long baseHash = PixHash(worldBase);
            groundDecorOff = true; worldRich = null; var plain = (Color[])LiveBase().Buf.Clone();
            double t0 = Raylib.GetTime();
            groundDecorOff = false; worldRich = null; var rich = LiveBase();
            double bakeMs = (Raylib.GetTime() - t0) * 1000;
            Check($"the rich layer with land decor bakes in good time ({bakeMs:0} ms in a Debug build)", bakeMs < 4000);
            Check("the ordinary baked layer is untouched by it", PixHash(worldBase) == baseHash);

            int changed = 0, badLetter = 0, outsideMiddle = 0, notOwn = 0, notPlain = 0, notCalm = 0, onSpecial = 0;
            var tilesWithDecor = new HashSet<(int, int)>(); var tileCount = new Dictionary<char, int>(); var withDecorByLetter = new Dictionary<char, int>();
            for (int y = 0; y < ROWS; y++) for (int x = 0; x < COLS; x++) { char l = worldMap[y, x]; if (l is 'g' or 'j' or 's' or 'D' or 'n' && groundCache[y, x] == l) tileCount[l] = tileCount.GetValueOrDefault(l) + 1; }
            for (int wy = 0; wy < PH; wy++)
                for (int wx = 0; wx < PW; wx++)
                {
                    int i = wy * PW + wx;
                    if (plain[i].Equals(rich.Buf[i])) continue;
                    changed++;
                    int tx = wx / T, ty = wy / T, px = wx % T, py = wy % T;
                    char l = worldMap[ty, tx];
                    if (l is not ('g' or 'j' or 's' or 'D' or 'n')) badLetter++;
                    if (px < 1 || px > 8 || py < 1 || py > 8) outsideMiddle++;
                    if (shape[i] != l) notOwn++;
                    if (!worldBase.Buf[i].Equals(GroundPixel(wx, wy, l))) notPlain++;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var b = worldBase.Buf[i]; var n = worldBase.Buf[(wy + dy) * PW + wx + dx];
                        if (Math.Abs(n.R - b.R) + Math.Abs(n.G - b.G) + Math.Abs(n.B - b.B) > 80) { notCalm++; break; }
                    }
                    if (stumps.ContainsKey((tx, ty)) || hoofprints.Contains((tx, ty))) onSpecial++;
                    if (tilesWithDecor.Add((tx, ty))) withDecorByLetter[l] = withDecorByLetter.GetValueOrDefault(l) + 1;
                }
            Check($"land decor appears ({changed} pixels in {tilesWithDecor.Count} tiles)", changed > 6000 && tilesWithDecor.Count > 2000);
            Check($"it is only on grass, forest floor, sand, dunes and snow ({badLetter} elsewhere), never on stumps or hoofprints ({onSpecial})", badLetter == 0 && onSpecial == 0);
            Check($"it keeps to the middle 8x8 ({outsideMiddle} outside), to a tile's own ground ({notOwn} not), to plain ground ({notPlain} not) and to calm ground ({notCalm} not)", outsideMiddle == 0 && notOwn == 0 && notPlain == 0 && notCalm == 0);
            string Share() => string.Join(", ", withDecorByLetter.Select(k => $"{k.Key} {100.0 * k.Value / Math.Max(1, tileCount.GetValueOrDefault(k.Key)):0}%"));
            double overall = tilesWithDecor.Count / (double)Math.Max(1, tileCount.Values.Sum());
            Check($"decor is sparse: {overall * 100:0}% of eligible tiles carry any ({Share()})", overall > 0.08 && overall < 0.4);

            // Where each kind may appear.
            var litter = LitterTones.Select(c => (c.R, c.G, c.B)).ToHashSet(); var wrack = WrackTones.Select(c => (c.R, c.G, c.B)).ToHashSet(); var cap = Pal.C("#c8473d");
            int litterFar = 0, wrackFar = 0, capFar = 0, wrackPx = 0, litterPx = 0, capPx = 0;
            for (int wy = 0; wy < PH; wy++)
                for (int wx = 0; wx < PW; wx++)
                {
                    int i = wy * PW + wx;
                    if (plain[i].Equals(rich.Buf[i])) continue;
                    var c = rich.Buf[i]; int tx = wx / T, ty = wy / T;
                    if (litter.Contains((c.R, c.G, c.B))) { litterPx++; if (TreeNeighbours(tx, ty, LeafTrees) == 0) litterFar++; }
                    if (wrack.Contains((c.R, c.G, c.B))) { wrackPx++; if (!NearWater(wx, wy)) wrackFar++; }
                    if (c.Equals(cap)) { capPx++; if (TreeNeighbours(tx, ty, LeafTrees) < 2) capFar++; }
                }
            Check($"leaf litter only lies beside trees ({litterPx} pixels, {litterFar} far from one), tide wrack only by the water ({wrackPx} pixels, {wrackFar} inland), toadstools only in groves ({capPx} caps, {capFar} alone)", litterPx > 500 && wrackPx > 200 && capPx > 5 && litterFar == 0 && wrackFar == 0 && capFar == 0);
            // Nothing is the colour of a loose pickup.
            var loose = new[] { "#8a6440", "#b08458", "#8a8f93" }.Select(Pal.C).ToList();
            int looseLike = 0; for (int i = 0; i < plain.Length; i++) if (!plain[i].Equals(rich.Buf[i]) && loose.Any(c => c.Equals(rich.Buf[i]))) looseLike++;
            Check($"no decor pixel is a loose pickup's colour (driftwood, stone) ({looseLike})", looseLike == 0);

            // The same bake twice is the same picture; light off never sees it; the chart's layer is the ordinary one.
            var again = (Color[])rich.Buf.Clone(); BakeRich();
            int differs = 0; for (int i = 0; i < again.Length; i++) if (!again[i].Equals(worldRich.Buf[i])) differs++;
            Check($"baking the rich layer again gives exactly the same picture ({differs} pixels differ)", differs == 0);
            EffectsOff = true;
            Check("with light off the view starts from the ordinary layer, with none of it", ReferenceEquals(LiveBase(), basePix));
            EffectsOff = false;

            // Felling an oak and growing it back: the litter round it repaints exactly as a whole bake gives.
            (int x, int y)? oak = null;
            for (int y = 3; y < ROWS - 3 && oak == null; y++)
                for (int x = 3; x < 40 && oak == null; x++)
                    if (worldMap[y, x] == 't' && TreeNeighbours(x, y, LeafTrees) == 0 && Shore(groundCache[y + 1, x]) && groundCache[y, x] == 'g') oak = (x, y);
            Check($"a lone oak was found ({oak})", oak != null);
            var (ox, oy) = oak ?? (3, 3);
            var snapshot = (Color[])worldRich.Buf.Clone();
            worldMap[oy, ox] = 'g'; RenderTile(ox, oy);
            var afterFell = (Color[])worldRich.Buf.Clone();
            BakeRich();
            int fellDiff = 0, fellMoved = 0;
            for (int i = 0; i < afterFell.Length; i++) { if (!afterFell[i].Equals(worldRich.Buf[i])) fellDiff++; if (!afterFell[i].Equals(snapshot[i])) fellMoved++; }
            Check($"after felling an oak RenderTile gives the same rich layer as a new bake ({fellDiff} pixels differ; {fellMoved} moved)", fellDiff == 0 && fellMoved > 5);
            worldMap[oy, ox] = 't'; RenderTile(ox, oy);
            int back = 0; for (int i = 0; i < snapshot.Length; i++) if (!snapshot[i].Equals(worldRich.Buf[i])) back++;
            Check($"and when it grows back the layer is as it was ({back} pixels differ)", back == 0);

            // Screenshots.
            foreach (var (id, x, y) in new[] { ("saltmere", 160f, 115f), ("atoll", 935f, 262f), ("mirewood", 200f, 380f), ("sunscald", 660f, 380f), ("frostfang", 620f, 90f), ("amihan", 1625f, 192f), ("luntian", 2040f, 138f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (int lm in new[] { 0, 2 })
                {
                    EffectsOff = lm == 0; Settings.Data.light = 2; state.clock = 12 * 60; ClearSkies(); yield return 4;
                    pendingPixShot = $"ground-{id}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                }
            }
            EffectsOff = false;
        }

        /* ---------- Phase B: tree tints ---------- */
        {
            Note("Ground: tree tints");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3; state.clock = 12 * 60;
            var oaks = trees.Where(t => t.kind == 't').Take(400).ToList(); var palms = trees.Where(t => t.kind == 'h').Take(400).ToList();
            var oakTints = oaks.Select(t => LeafTint(t.x, t.y, 't')).ToList();
            int distinct = oakTints.Select(p => (p.colour!.Value.R, p.colour.Value.G, p.colour.Value.B, (int)(p.amount * 50))).Distinct().Count();
            float widest = oakTints.Concat(palms.Select(t => LeafTint(t.x, t.y, 'h'))).Max(p => p.amount);
            Check($"every oak has its own shade ({distinct} different tints over {oaks.Count} oaks), kept narrow (at most {widest:0.00} of the foliage's colour)", distinct >= 8 && widest <= 0.25f);
            EffectsOff = true; var offTint = LeafTint(oaks[0].x, oaks[0].y, 't'); EffectsOff = false;
            Check("with light off a tree has no tint at all", offTint.colour == null && offTint.amount == 0);
            // Only the foliage takes the tint: bark and outline are drawn as they were.
            LoadTreeSprites();
            var sprite = oakSprites[2];
            Color[] Blit(Color? tint, float amt)
            {
                var keep = pix; pix = new Pix(120, 120); Array.Fill(pix.Buf, new Color(1, 2, 3, 255));
                BlitTree(sprite, 60, 100, 1, 0, null, null, 0, tint, amt);
                var r = pix.Buf; pix = keep; return r;
            }
            var plainTree = Blit(null, 0); var tinted = Blit(Pal.C("#e0b030"), 0.3f);
            int greenChanged = 0, otherChanged = 0, greens = 0;
            for (int i = 0; i < plainTree.Length; i++)
            {
                var c = plainTree[i]; bool green = !c.Equals(new Color(1, 2, 3, 255)) && c.G > c.R + 6 && c.G > c.B + 6;
                if (green) greens++;
                if (!c.Equals(tinted[i])) { if (green) greenChanged++; else otherChanged++; }
            }
            Check($"the tint reaches the foliage ({greenChanged} of {greens} green pixels) and nothing else ({otherChanged} bark or outline pixels changed)", greens > 300 && greenChanged > greens * 0.8 && otherChanged == 0);   // the rest of the green is outline or special pixels, left alone
            // Judged by the sprite's own data: a full-strength magenta tint lands on exactly its green body pixels, never an outline or special pixel.
            var magenta = new Color(255, 0, 255, 255); var full = Blit(magenta, 1f);
            int expected = 0; for (int k = 0; k < sprite.Px.Length; k++) { var c = sprite.Px[k]; if (c.A != 0 && sprite.Special[k] < 0 && c.G > c.R + 6 && c.G > c.B + 6) expected++; }
            int painted = full.Count(c => c.Equals(magenta));
            Check($"a full-strength tint lands on exactly the sprite's green body pixels ({painted} of {expected} expected)", painted == expected && expected > 300);
        }

        /* ---------- Phase E: two more silhouettes ---------- */
        {
            Note("Ground: tree silhouettes");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3; state.clock = 12 * 60;
            LoadTreeSprites();
            var all = trees.Where(t => t.kind is 't' or 'h').ToList();
            int sameOff = 0, offTotal = 0, variants = 0, small = 0, wrongSize = 0, onTotal = 0;
            foreach (var (tx, ty, kind) in all)
            {
                float rel = (0.8f + 0.4f * (float)Pix.Hash(tx, ty, 61));   // the tree's own size against the usual (as DrawTree works it out)
                var set = kind == 't' ? oakSprites : palmSprites;
                EffectsOff = true; var off = TreeSpriteFor(tx, ty, kind, rel); EffectsOff = false;
                offTotal++; if (ReferenceEquals(off, PickTree(set, rel, tx, ty))) sameOff++;
                var on = TreeSpriteFor(tx, ty, kind, rel); onTotal++;
                if (!ReferenceEquals(on, off))
                {
                    variants++;
                    if (TreeBand(rel, tx, ty) == 0) small++;
                    if (Math.Abs(on.Size - off.Size) > off.Size * 0.3f) wrongSize++;
                }
            }
            Check($"with light off every tree gets exactly the sprite it always had ({sameOff} of {offTotal})", sameOff == offTotal && offTotal > 200);
            double share = variants / (double)onTotal;
            Check($"with light on {variants} of {onTotal} oaks and palms ({share * 100:0}%) are a twin-trunk oak or a leaning palm, never the small sprite ({small}), each about the height of the band's own ({wrongSize} off by more than 30%)", share > 0.05 && share < 0.35 && small == 0 && wrongSize == 0);
            // A variant is anchored like the others: the foot at its anchor, the crown above, nothing below the foot.
            int lowest = 0; foreach (var sp in oakTwinSprites.Concat(palmLeanSprites)) { int bottom = 0; for (int j = 0; j < sp.H; j++) for (int i = 0; i < sp.W; i++) if (sp.Px[j * sp.W + i].A != 0) bottom = j; lowest = Math.Max(lowest, bottom - sp.AY); }
            Check($"the new silhouettes stand on their foot ({lowest} rows below it at most)", lowest <= 3);
            foreach (var (id, x, y) in new[] { ("saltmere", 160f, 115f), ("atoll", 935f, 262f), ("amihan", 1625f, 192f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (int lm in new[] { 0, 2 })
                {
                    EffectsOff = lm == 0; Settings.Data.light = 2; state.clock = 12 * 60; ClearSkies(); yield return 4;
                    pendingPixShot = $"trees-{id}-{(lm == 0 ? "off" : "full")}"; yield return 2;
                }
            }
            EffectsOff = false;
        }

        /* ---------- Phase C: the two monsoons ---------- */
        {
            Note("Ground: seasons");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies();
            float MixAt(int day, int clock) { state.day = day; state.clock = clock; return SeasonMix(); }
            Check($"the first season is fully amihan all day and night ({MixAt(1, 6 * 60):0.00}, {MixAt(1, 23 * 60):0.00}, {MixAt(1, 3 * 60):0.00}, {MixAt(5, 5 * 60 + 59):0.00})", MixAt(1, 6 * 60) == 0 && MixAt(1, 23 * 60) == 0 && MixAt(1, 3 * 60) == 0 && MixAt(5, 5 * 60 + 59) == 0);
            float turnStart = MixAt(6, 6 * 60), turnMid = MixAt(6, 12 * 60), turnEnd = MixAt(6, 18 * 60), later = MixAt(8, 12 * 60);
            Check($"the habagat eases in over its first half day ({turnStart:0.00} at 6, {turnMid:0.00} at noon, {turnEnd:0.00} at 6 PM) and stays ({later:0.00})", turnStart == 0 && Math.Abs(turnMid - 0.5f) < 0.02f && turnEnd > 0.99f && later == 1);
            float back = MixAt(11, 12 * 60);
            Check($"and the amihan eases back after it ({MixAt(11, 6 * 60):0.00}, {back:0.00}, {MixAt(11, 18 * 60):0.00})", MixAt(11, 6 * 60) == 1 && Math.Abs(back - 0.5f) < 0.02f && MixAt(11, 18 * 60) == 0);
            float worst = 0, prev = MixAt(1, 6 * 60);
            for (int minute = 0; minute < 1440 * 30; minute++)
            {
                int clock = (6 * 60 + minute) % 1440;
                int d = 1 + (minute + 0) / 1440;   // a day starts at 06:00
                float m = MixAt(d, clock);
                worst = Math.Max(worst, Math.Abs(m - prev)); prev = m;
            }
            Check($"the season changes smoothly through a month, midnight and every 06:00 included (largest step {worst:0.0000} a minute)", worst < 0.004f);

            // The grade and the tufts follow it; Soft is half as strong; light off is untouched.
            state.day = 3; state.clock = 12 * 60; var amihan = GradeNow();
            state.day = 8; var habagat = GradeNow();
            Settings.Data.light = 1; var habagatSoft = GradeNow(); Settings.Data.light = 2;
            Check($"the habagat's grade is greener and lusher than the amihan's (red {amihan.R:0.000} to {habagat.R:0.000}, green {amihan.G:0.000} to {habagat.G:0.000}, saturation {amihan.Sat:0.000} to {habagat.Sat:0.000})", habagat.R < amihan.R && habagat.G > amihan.G && habagat.Sat > amihan.Sat);
            Check("Soft keeps exactly half of the habagat's difference from none", Math.Abs((habagatSoft.G - 1) - (habagat.G - 1) / 2) < 0.002f && Math.Abs((habagatSoft.Sat - 1) - (habagat.Sat - 1) / 2) < 0.002f);
            // Trees and tufts follow the season too.
            var oak = trees.First(t => t.kind == 't');
            state.day = 3; var oakDry = LeafTint(oak.x, oak.y, 't'); state.day = 8; var oakWet = LeafTint(oak.x, oak.y, 't');
            Check($"the same oak is a warmer yellow in the amihan ({oakDry.colour!.Value.R},{oakDry.colour.Value.G},{oakDry.colour.Value.B}) and a deeper green in the habagat ({oakWet.colour!.Value.R},{oakWet.colour.Value.G},{oakWet.colour.Value.B})", oakDry.colour.Value.R > oakWet.colour.Value.R && oakWet.colour.Value.G >= oakWet.colour.Value.R);
            Settings.Data.light = 1; var oakSoft = LeafTint(oak.x, oak.y, 't'); Settings.Data.light = 2;
            Check($"Soft tints the foliage about half as much ({oakSoft.amount:0.000} against {oakWet.amount:0.000})", oakSoft.amount < oakWet.amount * 0.6f && oakSoft.amount > 0);
            (player.X, player.Y) = StandableNear(160, 115); state.clock = 12 * 60; yield return 4;
            (double rg, int flowers, int tufts) TuftLook()
            {
                var keep = pix; pix = new Pix(W, H); Array.Fill(pix.Buf, new Color(1, 2, 3, 255)); pix.CamX = camX; pix.CamY = camY;
                DrawTufts(2.0f);
                double s = 0; int n = 0, f = 0;
                foreach (var c in pix.Buf) if (!(c.R == 1 && c.G == 2 && c.B == 3)) { n++; s += c.R / (double)Math.Max(1, (int)c.G); if (c.R > 200 && c.G > 150) f++; }
                pix = keep; return (s / Math.Max(1, n), f, n);
            }
            state.day = 3; state.clock = 12 * 60; var dryTufts = TuftLook();
            state.day = 8; var wetTufts = TuftLook();
            EffectsOff = true; state.day = 8; var offWet = TuftLook(); state.day = 3; var offDry = TuftLook(); EffectsOff = false;
            Check($"the grass is drier and more golden in the amihan (red/green {dryTufts.rg:0.000}) than in the habagat ({wetTufts.rg:0.000}) and the habagat has more flowers ({wetTufts.flowers} to {dryTufts.flowers})", dryTufts.rg > wetTufts.rg && wetTufts.flowers >= dryTufts.flowers);
            Check($"with light off the tufts are the same in either season ({offDry.rg:0.0000} and {offWet.rg:0.0000})", Math.Abs(offDry.rg - offWet.rg) < 1e-9 && offDry.flowers == offWet.flowers);

            // Screenshots: both seasons, three islands.
            foreach (var (id, x, y) in new[] { ("saltmere", 160f, 115f), ("mirewood", 200f, 380f), ("amihan", 1625f, 192f) })
            {
                (player.X, player.Y) = StandableNear(x, y);
                foreach (var (tag, day) in new[] { ("amihan", 3), ("habagat", 8) })
                    foreach (int lm in new[] { 0, 1, 2 })
                    {
                        EffectsOff = lm == 0; Settings.Data.light = lm == 0 ? 2 : lm; state.day = day; state.clock = 12 * 60; ClearSkies(); yield return 4;
                        pendingPixShot = $"season-{id}-{tag}-{(lm == 0 ? "off" : lm == 1 ? "soft" : "full")}"; yield return 2;
                    }
            }
            EffectsOff = false; state.day = 3;
        }

        /* ---------- Phase D: trodden paths ---------- */
        {
            Note("Ground: wear");
            EffectsOff = false; Settings.Data.light = 2; ClearSkies(); state.day = 3; state.clock = 12 * 60; state.wear.Clear(); state.wearDay = 0;
            // Three neighbouring grass tiles with nothing built or growing on them.
            (int x, int y)? row = null;
            for (int ty = 2; ty < 18 && row == null; ty++)
                for (int tx = 8; tx < 24 && row == null; tx++)
                    if (new[] { 0, 1, 2 }.All(k => map[ty, tx + k] == 'g' && groundCache[ty, tx + k] == 'g' && BuildAt(tx + k, ty) == null)) row = (tx, ty);
            Check($"three neighbouring grass tiles were found ({row})", row != null);
            var (rx, ry) = row ?? (10, 10);
            void StandOn(int k) { player.X = (rx + k) * T + 5; player.Y = ry * T + 6; }
            string Key(int k) => $"{rx + k},{ry}";
            int Entries(int k) => state.wear.GetValueOrDefault(Key(k));
            StandOn(0); lastWearTile = (-1, -1); NoteWear(); NoteWear();
            Check($"standing on a tile counts it once ({Entries(0)})", Entries(0) == 1);
            StandOn(1); NoteWear(); StandOn(2); NoteWear(); StandOn(0); NoteWear();
            Check($"each step onto a tile counts, from either side ({Entries(0)}, {Entries(1)}, {Entries(2)})", Entries(0) == 2 && Entries(1) == 1 && Entries(2) == 1);

            // Not everywhere: afloat, riding, wading, under a build, on sand, indoors.
            int Count() => state.wear.Values.Sum();
            int c0 = Count();
            state.inv["boat"] = 1; state.aboard = true; StandOn(1); lastWearTile = (-1, -1); bool wasAboard = Aboard; NoteWear(); state.aboard = false; state.inv["boat"] = 0;
            bool aboardKept = wasAboard && Count() == c0;
            state.tamed = true; state.riding = true; lastWearTile = (-1, -1); bool wasRiding = Riding; NoteWear(); state.riding = false; state.tamed = false;
            bool ridingKept = wasRiding && Count() == c0;
            state.builds.Add(new Build { id = "path", x = rx + 1, y = ry }); ReindexBuilds();
            lastWearTile = (-1, -1); StandOn(1); NoteWear();
            bool buildKept = Count() == c0;
            state.builds.RemoveAll(b => b.id == "path" && b.x == rx + 1 && b.y == ry); ReindexBuilds();
            (int x, int y)? beach = null;
            for (int ty = 2; ty < 30 && beach == null; ty++) for (int tx = 2; tx < 40 && beach == null; tx++) if (map[ty, tx] == 's' && groundCache[ty, tx] == 's' && BuildAt(tx, ty) == null) beach = (tx, ty);
            var (bx0, by0) = beach ?? (2, 2);
            player.X = bx0 * T + 5; player.Y = by0 * T + 6; lastWearTile = (-1, -1); NoteWear();
            bool sandKept = beach != null && Count() == c0;
            // Wading in the shallows and being indoors count for nothing either.
            state.inv["waders"] = 1;
            (int x, int y)? shallow = null;
            for (int ty = 1; ty < 40 && shallow == null; ty++) for (int tx = 1; tx < 40 && shallow == null; tx++) if (map[ty, tx] == 'w' && map[ty, tx - 1] == 'w' && map[ty, tx + 1] == 'w' && map[ty + 1, tx] == 'w' && map[ty - 1, tx] == 'w' && CanStand(tx * T + 5, ty * T + 5, true)) shallow = (tx * T + 5, ty * T + 5);
            var (wx0, wy0) = shallow ?? (0, 0);
            (player.X, player.Y) = (wx0, wy0); lastWearTile = (-1, -1); bool wasWading = InWater; NoteWear();
            bool wadeKept = wasWading && Count() == c0;
            state.inv["waders"] = 0;
            var keepScene = scene; scene = "house:test"; StandOn(1); lastWearTile = (-1, -1); NoteWear(); scene = keepScene;
            bool indoorsKept = Count() == c0;
            // Out of the water and back onto the same tile counts again.
            state.wear.Clear(); StandOn(0); lastWearTile = (-1, -1); NoteWear();
            state.inv["waders"] = 1; (player.X, player.Y) = (wx0, wy0); NoteWear(); state.inv["waders"] = 0;
            StandOn(0); NoteWear();
            bool againCounted = Entries(0) == 2;
            state.wear.Clear();
            Check($"it isn't counted afloat ({aboardKept}), riding ({ridingKept}), under a build ({buildKept}), on sand ({sandKept}), wading ({wadeKept}) or indoors ({indoorsKept}), and a step back from the water counts again ({againCounted})", aboardKept && ridingKept && buildKept && sandKept && wadeKept && indoorsKept && againCounted);

            // By really walking: hold Right along the row and every tile you pass counts.
            state.wear.Clear(); StandOn(0); lastWearTile = (-1, -1); Inp.Hold(KeyboardKey.Right, true); yield return 12; Inp.Hold(KeyboardKey.Right, false); yield return 2;
            Check($"walking really does count the tiles you cross ({state.wear.Count} tiles, {Count()} steps)", state.wear.Count >= 2 && Count() >= state.wear.Count);

            // Stages and the cap.
            Check("the stages begin at 12, 40 and 100 steps", StageOf(11) == 0 && StageOf(12) == 1 && StageOf(39) == 1 && StageOf(40) == 2 && StageOf(99) == 2 && StageOf(100) == 3);
            state.wear[Key(0)] = 299; StandOn(1); NoteWear(); StandOn(0); NoteWear(); StandOn(1); NoteWear(); StandOn(0); NoteWear();
            Check($"a tile's count is capped ({Entries(0)})", Entries(0) <= WearCap);

            // What it looks like: bare earth from the tile's middle, inside the tile, nowhere else; the same as a whole bake.
            groundDecorOff = false; worldRich = null; var rich0 = LiveBase(); state.wear.Clear();
            BakeRich(); var none = (Color[])worldRich.Buf.Clone();
            int Changed(int stage)
            {
                state.wear.Clear(); state.wear[Key(0)] = WearSteps[stage - 1]; RichTile(rx, ry);
                int n = 0, outside = 0;
                for (int i = 0; i < none.Length; i++)
                    if (!none[i].Equals(worldRich.Buf[i])) { n++; int wx = i % PW, wy = i / PW; if (wx / T != rx || wy / T != ry) outside++; }
                return n * 1000 + outside;
            }
            int s1 = Changed(1), s2 = Changed(2), s3 = Changed(3);
            Check($"worn ground shows bare earth, more with every stage ({s1 / 1000}, {s2 / 1000}, {s3 / 1000} pixels) and only inside its own tile ({s1 % 1000 + s2 % 1000 + s3 % 1000} outside)", s1 / 1000 >= 3 && s2 / 1000 > s1 / 1000 && s3 / 1000 > s2 / 1000 && s1 % 1000 + s2 % 1000 + s3 % 1000 == 0);
            var incremental = (Color[])worldRich.Buf.Clone(); BakeRich();
            int diffBake = 0; for (int i = 0; i < incremental.Length; i++) if (!incremental[i].Equals(worldRich.Buf[i])) diffBake++;
            Check($"repainting the tile equals a whole new bake ({diffBake} pixels differ)", diffBake == 0);
            state.wear.Clear(); RichTile(rx, ry);
            int back = 0; for (int i = 0; i < none.Length; i++) if (!none[i].Equals(worldRich.Buf[i])) back++;
            Check($"and with the wear gone the tile is as it was ({back} pixels differ)", back == 0);
            Check("the ordinary baked layer never sees it", PixHash(worldBase) == PixHash(basePix));

            // Fading: a little each day, more when well worn; days jumped over count; zeros go away.
            state.wear.Clear(); state.wear[Key(0)] = 100; state.wear[Key(1)] = 3; state.wearDay = state.day;
            state.day++; CatchUpWear();
            int after1 = Entries(0), small = Entries(1);
            state.day += 30; CatchUpWear();
            int after31 = Entries(0), gone = state.wear.ContainsKey(Key(1)) ? 1 : 0;
            Check($"a day fades 100 steps by 1 + 100/40 ({after1}) and 3 steps to {small}; a month on it is {after31}, and the tile with a few steps is forgotten ({gone == 0})", after1 == 97 && small == 2 && after31 < after1 && after31 > 0 && gone == 0);
            state.wear[Key(0)] = 100; state.wearDay = state.day; state.day += 2; yield return 3;
            Check($"in play the days catch up by themselves ({Entries(0)})", Entries(0) < 100 && state.wearDay == state.day);

            // Saved with the game, and an old save without any of it loads.
            state.wear[Key(0)] = 42; state.wearDay = state.day;
            Save(); var reloaded = SaveFile.Read();
            Check($"wear is saved and loaded ({reloaded?.wear.GetValueOrDefault(Key(0))} steps, day {reloaded?.wearDay})", reloaded != null && reloaded.wear.GetValueOrDefault(Key(0)) == 42 && reloaded.wearDay == state.day);
            string oldSave = Path.Combine(testDir, "old-wear-save.json");
            File.WriteAllText(oldSave, "{\"created\":true,\"day\":9,\"weather\":\"clear\"}");
            var old = SaveFile.Load(oldSave);
            Check("a save from before trodden paths loads with none", old != null && old.wear != null && old.wear.Count == 0 && old.wearDay == 0);

            // Light off still counts it (it is only how it looks).
            EffectsOff = true; state.wear.Clear(); StandOn(0); lastWearTile = (-1, -1); NoteWear(); EffectsOff = false;
            Check("with light off the steps are still counted", Entries(0) == 1);

            // Screenshots: a well-trodden route.
            state.wear.Clear();
            // A route across Saltmere's meadow, worn more the closer to its middle.
            for (int tx = 8; tx <= 27; tx++) foreach (int ty in new[] { 9, 10 })
                if (map[ty, tx] == 'g' && groundCache[ty, tx] == 'g' && BuildAt(tx, ty) == null) state.wear[$"{tx},{ty}"] = 20 + (int)(140 * (1 - Math.Abs(tx - 17.5) / 10));
            worldRich = null; (player.X, player.Y) = StandableNear(17 * T + 5, 12 * T);
            foreach (int lm in new[] { 0, 2 })
            {
                EffectsOff = lm == 0; Settings.Data.light = 2; state.clock = 12 * 60; ClearSkies(); yield return 4;
                pendingPixShot = $"wear-{(lm == 0 ? "off" : "full")}"; yield return 2;
            }
            state.wear.Clear(); worldRich = null; EffectsOff = false;
        }

        EffectsOff = keepOff;
        SetNight(false);
    }
}
#endif
