#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // 1.26: the world's sizes and hours. Trees tall, boulders that differ, the carabao bigger than you, animals that stay
    // asleep, villagers in bed, equal case tabs, a scrolling island guide, Aling Sela's house off the jetty, and the
    // footbridge over the river. FESH_WORLD_TEST=1 runs only this.
    IEnumerable<int> WorldScript()
    {
        Note("The world's sizes and hours (1.26)");
        state = new State { created = true, look = new Look { name = "Warden" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;

        /* ---------- Trees stand tall ---------- */
        {
            var keep = pix; var keepCam = (pix.CamX, pix.CamY);
            int Height(char kind)
            {
                var sheet = new Pix(80, 100);
                pix = sheet; pix.CamX = 60 * T - 40; pix.CamY = 30 * T - 80;   // away from Pip's stall, where trees stay small
                DrawTree(60, 30, kind, live: false);
                int top = -1, bottom = -1;
                for (int y = 0; y < 100; y++)
                    for (int x = 0; x < 80; x++)
                        if (sheet.Buf[y * 80 + x].A > 0) { if (top < 0) top = y; bottom = y; }
                pix = keep;
                return top < 0 ? 0 : bottom - top + 1;
            }
            int oak = Height('t'), palm = Height('h'), fir = Height('f');
            Check($"an oak stands over three times your height ({oak} px against 14), a palm and a fir nearly as much ({palm}, {fir})", oak >= 42 && palm >= 36 && fir >= 38);
        }

        /* ---------- Boulders differ ---------- */
        {
            var keep = pix;
            var seen = new HashSet<string>(); var widths = new HashSet<int>(); var heights = new HashSet<int>();
            for (int i = 0; i < 40; i++)
            {
                int tx = 30 + i % 8, ty = 12 + i / 8;
                var sheet = new Pix(40, 40);
                pix = sheet; pix.CamX = tx * T - 15; pix.CamY = ty * T - 20;
                DrawBoulder(tx, ty, tx * T, ty * T);
                int x0 = 99, x1 = -1, y0 = 99, y1 = -1; long h = 17;
                for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 40; x++)
                    {
                        var c = sheet.Buf[y * 40 + x];
                        if (c.A == 0) continue;
                        x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                        h = h * 31 + c.R * 7 + c.G * 3 + c.B + x * 131 + y;
                    }
                pix = keep;
                seen.Add(h.ToString()); widths.Add(x1 - x0 + 1); heights.Add(y1 - y0 + 1);
            }
            Check($"forty boulders are forty different drawings, of several sizes ({seen.Count} distinct, {widths.Count} widths, {heights.Count} heights)", seen.Count >= 36 && widths.Count >= 4 && heights.Count >= 4);
        }

        /* ---------- Animals: sizes ---------- */
        var (cw, ch) = AnimalArt.Size("carabao");
        var (dw, dh) = AnimalArt.Size("dog");
        var (sw, sh) = AnimalArt.Size("sheep");
        Check($"a carabao is bigger than you (and than a sheep) ({cw}x{ch} against 14 tall, sheep {sw}x{sh})", ch >= 14 && cw >= 24 && ch > sh * 1.4f && cw > sw * 1.6f);
        Check($"the dog is no bigger than the sheep ({dw}x{dh})", dw <= sw && dh <= sh + 1);

        /* ---------- Animals: parrots on the atoll, and walking (1.31) ---------- */
        {
            var parrots = Items.AnimalSpawns.Where(sp => sp.kind == "parrot").ToList();
            Check($"parrots live on the atoll, on ground they can stand on ({parrots.Count})",
                parrots.Count >= 2 && parrots.All(sp => BiomeAt((int)(sp.x / T), (int)(sp.y / T)) == 4 && CanStand(sp.x, sp.y)));
            var parrot = new Animal { Kind = "parrot", Key = "p", X = parrots[0].x, Y = parrots[0].y };
            Check($"you observe a parrot rather than pet it ({AnimalLabel(parrot)})", AnimalLabel(parrot) == "Observe the parrot");
            // The four walking frames all differ from standing, and the steps from the passes.
            Color[] Frame(string id, int f)
            {
                var sheet = new Pix(40, 30);
                AnimalArt.Draw(sheet, id, 20, 25, false, f);
                return (Color[])sheet.Buf.Clone();
            }
            bool Same(Color[] a, Color[] b) => a.Zip(b).All(p => p.First.Equals(p.Second));
            var walkers = new[] { "carabao", "pig", "sheep", "goat", "dog", "cat", "chicken" };
            var still = walkers.Where(id => Enumerable.Range(1, 4).Any(f => Same(Frame(id, 0), Frame(id, f))) || Same(Frame(id, 1), Frame(id, 3))).ToList();
            Check($"every animal's walk moves its feet and body, frame to frame ({string.Join(", ", still)})", still.Count == 0);
        }

        /* ---------- Animals: a night's sleep ---------- */
        {
            var a = new Animal { Kind = "sheep", Key = "t1", X = 200, Y = 100, HomeX = 200, HomeY = 100, Timer = 0.1f };
            animals.Clear(); animals.Add(a);
            var (bed, wake) = AnimalHours(a);
            Check($"it beds down between 20:30 and 23:00 and gets up between 05:00 and 06:30 ({bed / 60:0.0}, {wake / 60:0.0})", bed >= 20.5f * 60 && bed <= 23 * 60 && wake >= 5 * 60 && wake <= 6.5f * 60);
            state.clock = 12 * 60; for (int i = 0; i < 30; i++) UpdateAnimals(0.5f);
            Check("by day it never lies down", !a.Asleep);
            state.clock = bed + 30;
            int woke = 0; bool wasAsleep = false; float x0 = 0, y0 = 0; bool moved = false;
            for (int i = 0; i < 200; i++)
            {
                UpdateAnimals(0.5f);
                if (a.Asleep && !wasAsleep) { x0 = a.X; y0 = a.Y; }
                if (!a.Asleep && wasAsleep) woke++;
                if (a.Asleep && (a.X != x0 || a.Y != y0)) moved = true;
                wasAsleep = a.Asleep;
            }
            Check($"after bedtime it lies down once and stays down all night, without wandering (asleep {a.Asleep}, woke {woke} times, moved {moved})", a.Asleep && woke == 0 && !moved);
            Pet(a);
            Check($"petting wakes it ({a.Asleep})", !a.Asleep);
            state.clock = 7 * 60; for (int i = 0; i < 10; i++) UpdateAnimals(0.5f);
            Check("by morning it's up and about", !a.Asleep);
            // The cat and the tarsier keep their own hours.
            var cat = new Animal { Kind = "cat", Key = "t2", X = 220, Y = 100, HomeX = 220, HomeY = 100, Timer = 0.1f };
            animals.Add(cat);
            state.clock = 23 * 60 + 40; for (int i = 0; i < 40; i++) UpdateAnimals(0.5f);
            Check("a cat is not put to bed with the rest", !cat.Asleep);
            animals.Clear();
        }

        /* ---------- Villagers keep hours ---------- */
        {
            state.clock = 12 * 60;
            Check("at noon nobody is in bed", !FolkAsleep);
            SetNight(true);
            Check("at 10 PM they're still up", !FolkAsleep);
            state.clock = 23 * 60 + 30;
            Check("late at night they're asleep", FolkAsleep);
            state.clock = 4 * 60 + 30;
            Check("and still at half past four", FolkAsleep);
            state.clock = 5 * 60 + 5;
            Check("up again after five", !FolkAsleep);
            // What you find at their doors.
            foreach (var (name, x, y, id) in new[] { ("Lira", IslanderWalk("lira").X, IslanderWalk("lira").Y, "lira"), ("Rosa", HabagatWalk("rosa").X, HabagatWalk("rosa").Y, "rosa") })
            {
                state.clock = 23 * 60 + 30; (player.X, player.Y) = StandableNear(x, y + 7); player.Face = "up"; yield return 3;
                bool asleepInfo = target?.Type == "info" && target.Label.Contains("asleep");
                state.clock = 12 * 60; yield return 3;
                bool awake = target != null && target.Type is "islander" or "habagatfolk";
                Check($"{name}'s door by night says she's asleep, by day she's there to talk to ({target?.Type}: {target?.Label})", asleepInfo && awake);
            }
            // Something that has them up keeps them up.
            state.clock = 23 * 60 + 30;
            tremor = new Quake();
            Check("a quake wakes them", !FolkAsleep);
            tremor = null;
            state.clock = 12 * 60;
        }

        /* ---------- The case board's tabs are one size ---------- */
        {
            TogglePanel("case"); yield return 4;
            var names = new[] { "The guardian of the deep", "The vanishing moon", "The restless sea", "Beneath the clouds", "The heart that rose" };
            var ws = names.Select(n => Gfx.Seen.TryGetValue(n, out var r) ? r.Width : -1).ToList();
            var hs = names.Select(n => Gfx.Seen.TryGetValue(n, out var r) ? r.Height : -1).ToList();
            Check($"the case board's five tabs are the same size ({string.Join(", ", ws.Select(w => w.ToString("0")))})", ws.All(w => w > 0 && Math.Abs(w - ws[0]) < 0.5f) && hs.All(h => Math.Abs(h - hs[0]) < 0.5f));
            Check($"and fit beside Close ({lastCaseTabRight:0})", lastCaseTabRight > 0 && lastCaseTabRight < 1180);
            pendingShot = "world-case-board"; yield return 2;
            ClosePanels(); yield return 3;
        }

        /* ---------- The island guide's list scrolls ---------- */
        {
            state.charted = Regions.Select(r => r.Id).Concat(new[] { "habagat:islet:0" }).ToList();
            state.caveDeepest = 12; state.inv["boat"] = 1; state.hinted["amihan"] = state.hinted["habagat"] = state.hinted["metJoy"] = state.hinted["marilaya"] = true;
            foreach (var r in Regions) Gfx.Seen.Remove("atlas:" + r.Id);
            atlasScroll = 0; OpenAtlas("saltmere"); yield return 3;
            int visible = Regions.Count(r => Gfx.Seen.ContainsKey("atlas:" + r.Id));   // (cleared first: Seen keeps what earlier panels drew)
            var first = Gfx.Seen["atlas:saltmere"];
            Check($"the guide's buttons are big, and not every one fits at once ({first.Height:0} px tall, {visible} of {Regions.Length} in view)", first.Height >= 32 && visible < Regions.Length && visible >= 8);
            atlasScrollTo = Regions[^1].Id; yield return 3;
            bool lastShown = Gfx.Seen.TryGetValue("atlas:" + Regions[^1].Id, out var lr);
            Check($"scrolling brings the last place into view ({atlasScroll:0} px)", lastShown && atlasScroll > 0);
            if (lastShown) { Inp.ScriptMouse = new System.Numerics.Vector2(lr.X + lr.Width / 2, lr.Y + lr.Height / 2); Inp.ScriptClickNext = true; }
            yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
            Check($"and it can be clicked there ({atlasRegion})", atlasRegion == Regions[^1].Id);
            pendingShot = "world-atlas-list"; yield return 2;
            ClosePanels(); yield return 3;
        }

        /* ---------- Aling Sela's house is off the jetty ---------- */
        {
            var jetty = new Box(MlJettyX0 * T, MlJettyRow * T, (MlJettyX1 - MlJettyX0 + 1) * T, T);
            var house = new Box(SelaX - 16, SelaY - 22, 32, 19);
            Check($"her house doesn't stand on the jetty ({house.X:0}..{house.X + house.W:0} against the jetty's end at {jetty.X + jetty.W:0})", !house.Overlaps(jetty) && house.X - (jetty.X + jetty.W) >= 8);
            Check("you can stand where the sailing arrives, and at her door", CanStand(MlJettyX + 4, MlJettyY) && CanStand(SelaX, SelaY + 8));
            Check("nothing solid sits on the arrival spot", !Solids().Any(b => b.Overlaps(new Box(MlJettyX + 4 - 3, MlJettyY - 3, 7, 3))));
            Check($"Sela's own spot is in front of her house ({MlWalk("sela").HomeX:0} vs {SelaX:0})", Math.Abs(MlWalk("sela").HomeX - SelaX) < 10);
        }

        /* ---------- The footbridge over the river, and the river's banks ---------- */
        {
            (int x, int y)? bridge = null;
            for (int y = 0; y < ROWS && bridge == null; y++)
                for (int x = MlStart; x < COLS; x++)
                    if (worldMap[y, x] == 'b') { bridge = (x, y); break; }
            Check("Marilaya has a footbridge over its river", bridge != null);
            if (bridge is var (bx, by))
            {
                var wl = worldBase.Buf[(by * T + 5) * PW + bx * T];          // the water beside the planks
                var wr = worldBase.Buf[(by * T + 5) * PW + bx * T + 9];
                var plank = worldBase.Buf[(by * T + 5) * PW + bx * T + 4];
                Check($"water either side of the planks is the river's green, not the sea's blue ({wl.R},{wl.G},{wl.B} / {wr.R},{wr.G},{wr.B})", wl.G >= wl.B - 12 && wr.G >= wr.B - 12);
                Check($"and the planks are wood ({plank.R},{plank.G},{plank.B})", plank.R > plank.B + 30);
            }
            // The river's bank takes on the green: the water pixel beside the grass isn't the brightest in the river.
            int checkedPx = 0, bright = 0;
            for (int y = 5 * T; y < PH - 5 * T && checkedPx < 400; y += 3)
                for (int x = MlStart * T; x < PW - 5 * T && checkedPx < 400; x += 3)
                    if (shape[y * PW + x] == 'l' && Shore(shape[y * PW + x + 1]))
                    {
                        checkedPx++;
                        var c = worldBase.Buf[y * PW + x];
                        if (c.G > 175) bright++;
                    }
            Check($"the river's banks aren't a bright rim ({bright} of {checkedPx} bank pixels bright)", checkedPx < 5 || bright * 4 < checkedPx);
        }


        /* ---------- 1.27: cactus, bushes, the other animals, bridges, the cave mouth, rain, the plume ---------- */
        {
            var keep = pix;
            int Height(char kind, int tx, int ty)
            {
                var sheet = new Pix(80, 100);
                pix = sheet; pix.CamX = tx * T - 40; pix.CamY = ty * T - 80;
                DrawTree(tx, ty, kind, live: false);
                int top = -1, bottom = -1;
                for (int y = 0; y < 100; y++) for (int x = 0; x < 80; x++) if (sheet.Buf[y * 80 + x].A > 0) { if (top < 0) top = y; bottom = y; }
                pix = keep;
                return top < 0 ? 0 : bottom - top + 1;
            }
            var cacti = Enumerable.Range(0, 20).Select(i => Height('c', 40 + i, 12)).ToList();
            var bushes = Enumerable.Range(0, 20).Select(i => Height('y', 40 + i, 14)).ToList();
            Check($"cacti stand taller than a person and differ ({cacti.Min()}-{cacti.Max()} px, {cacti.Distinct().Count()} heights)", cacti.Average() >= 20 && cacti.Distinct().Count() >= 4);
            Check($"bushes are bigger and differ ({bushes.Min()}-{bushes.Max()} px, {bushes.Distinct().Count()} heights)", bushes.Average() >= 11 && bushes.Distinct().Count() >= 3);
            var (shw, shh) = AnimalArt.Size("sheep"); var (pgw, pgh) = AnimalArt.Size("pig"); var (gtw, gth) = AnimalArt.Size("goat"); var (tw, th) = AnimalArt.Size("tarsier");
            // 1.31: sizes from the real animals at about 10 px a metre (you: 16 px, 1.6 m). A goat stands about a metre high.
            var (ctw, cth) = AnimalArt.Size("cat"); var (chw, chh) = AnimalArt.Size("chicken");
            Check($"farm animals are their real size beside you ({shw}x{shh}, {pgw}x{pgh}, {gtw}x{gth}), the cat, chicken and tarsier small ({ctw}x{cth}, {chw}x{chh}, {tw}x{th})", shw >= 13 && shh >= 9 && pgw >= 13 && pgh >= 8 && gtw >= 12 && gth >= 9 && gth <= 12 && cth <= 7 && chh <= 7 && tw <= 6 && th <= 6);
        }
        {
            // Bridges: boards belong to the run, so a long bridge isn't one tile repeated.
            var deck = new List<(int x, int y)>();
            for (int y = 0; y < ROWS; y++) for (int x = 0; x < COLS; x++) if (worldMap[y, x] == 'd' && worldMap[y, Math.Min(COLS - 1, x + 1)] == 'd') deck.Add((x, y));
            var patterns = new HashSet<long>();
            foreach (var (x, y) in deck.Take(30))
            {
                long h = 7;
                for (int py = 0; py < T; py++) for (int px = 0; px < T; px++) { var c = worldBase.Buf[(y * T + py) * PW + x * T + px]; h = h * 31 + c.R * 3 + c.G * 5 + c.B + px * 17 + py; }
                patterns.Add(h);
            }
            Check($"a long bridge's tiles aren't copies of one another ({patterns.Count} patterns in {Math.Min(30, deck.Count)} tiles)", deck.Count >= 10 && patterns.Count >= Math.Min(30, deck.Count) * 0.8);
        }
        {
            // The cave mouth: a dark opening in snowy rock, not a doorway.
            var keep = pix;
            var sheet = new Pix(80, 70);
            pix = sheet; pix.CamX = MouthX * T + 10 - 40; pix.CamY = MouthY * T + 20 - 50;
            DrawCaveMouth();
            pix = keep;
            var open = sheet.Buf[(50 - 6) * 80 + 40]; var crown = Enumerable.Range(0, 80).SelectMany(x => Enumerable.Range(20, 15).Select(y => sheet.Buf[y * 80 + x])).Max(c => c.R + c.G + c.B);
            Check($"the cave's mouth is dark in the middle ({open.R},{open.G},{open.B}) under a snowy crown (brightest {crown})", open.R + open.G + open.B < 120 && crown > 650);
        }
        {
            // Rain belongs to the world: walk right and it goes by to the left.
            state.weather = planned = "rain"; SnapWeather();
            (player.X, player.Y) = StandableNear(160, 115); yield return 3;
            var keep = pix; var keepCam = camX;
            List<(int, int)> Drops(int cam)
            {
                var sheet = new Pix(W, H);
                pix = sheet; camX = cam;
                DrawRain(7.3f);
                pix = keep; camX = keepCam;
                var l = new List<(int, int)>();
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (sheet.Buf[y * W + x].A > 0) l.Add((x, y));
                return l;
            }
            var a = Drops(40); var b = Drops(46);
            var bset = b.ToHashSet();
            int shifted = a.Count(p => bset.Contains((p.Item1 - 6, p.Item2)));
            Check($"moving the view 6 px carries the rain 6 px the other way ({shifted} of {a.Count} drops)", a.Count > 50 && shifted > a.Count * 0.7);
            ClearSkies(); SnapWeather();
        }
        {
            // The plume: a column of soft puffs, not stacked bars; it leans west.
            var keep = pix;
            var sheet = new Pix(160, 100);
            pix = sheet; pix.CamX = (int)ConeX - 80; pix.CamY = (int)(ConeBaseY - ConeH) - 80;
            state.hinted["mg:unrest"] = true;
            DrawPlume(ConeBaseY - ConeH, 2, false);
            pix = keep;
            int n = 0, west = 0, east = 0;
            for (int y = 0; y < 100; y++) for (int x = 0; x < 160; x++) if (sheet.Buf[y * 160 + x].A > 0) { n++; if (x < 80) west++; else east++; }
            Check($"Baga's plume is a good body of smoke leaning west ({n} pixels, {west} west of the crater, {east} east)", n > 150 && west > east);
            state.hinted.Remove("mg:unrest");
        }
        {
            // The pupa: before, the green shell; after, a butterfly with its wings out.
            var a = new Pix(120, 88); var b = new Pix(120, 88); var c = new Pix(120, 88);
            PaintEmergence(a, 0.1f); PaintEmergence(b, 0.6f); PaintEmergence(c, 1f);
            int Diff(Pix p, Pix q) { int d = 0; for (int i = 0; i < p.Buf.Length; i++) if (Math.Abs(p.Buf[i].R - q.Buf[i].R) + Math.Abs(p.Buf[i].G - q.Buf[i].G) > 40) d++; return d; }
            int blackPx = c.Buf.Count(p => p.R < 50 && p.G < 50 && p.B < 50), yellowPx = c.Buf.Count(p => p.R > 190 && p.G > 190 && p.B < 140);
            Check($"the emergence changes through its stages ({Diff(a, b)}, {Diff(b, c)} pixels), ending in black and yellow wings ({blackPx}, {yellowPx})", Diff(a, b) > 150 && Diff(b, c) > 400 && blackPx > 250 && yellowPx > 60);
            // The cave model grows: the hollow, then the stalactites.
            var m1 = new Pix(CmW, CmH); var m2 = new Pix(CmW, CmH); var m3 = new Pix(CmW, CmH);
            PaintCaveModel(m1, 0.2f); PaintCaveModel(m2, 0.6f); PaintCaveModel(m3, 0.95f);
            int dark2 = m2.Buf.Count(p => p.R < 90 && p.G < 80 && p.B < 70), dark3 = m3.Buf.Count(p => p.R < 90 && p.G < 80 && p.B < 70);
            Check($"the cave model opens a hollow and then hangs stalactites in it ({dark2} dark pixels, {dark3} after)", dark2 > 500 && Diff(m2, m3) > 150);
        }

        /* ---------- 1.28: the case board's one frame, decks over land, the lahar watch, the title ---------- */
        {
            var bottoms = new List<float>();
            foreach (var s in CaseStories())
            {
                caseTab = s.id; TogglePanel("case"); yield return 3;
                bottoms.Add(lastCaseBottom);
                ClosePanels(); yield return 2;
            }
            caseTab = "saltmere";
            Check($"every story's board is the same size, locked or not ({string.Join(", ", bottoms.Select(b => b.ToString("0")))}; layout {CaseLayout()})", bottoms.Count == 5 && bottoms.All(b => Math.Abs(b - bottoms[0]) < 0.5f) && bottoms[0] <= Gfx.LH);
        }
        {
            // A jetty that runs over a beach stands on the beach: no sea-blue showing at its edges.
            int landDecks = 0, blueEdges = 0, seaDecks = 0, seaOnLand = 0;
            for (int y = 0; y < ROWS; y++)
                for (int x = 0; x < COLS; x++)
                {
                    if (worldMap[y, x] is not ('d' or 'b') || (x >= MlStart && worldMap[y, x] == 'b')) continue;
                    bool land = Shore(GroundAt(x, y));
                    if (land)
                    {
                        landDecks++;
                        var c = worldBase.Buf[(y * T + 5) * PW + (worldMap[y, x] == 'b' ? x * T : x * T + 5)];
                        if (worldMap[y, x] == 'b' && c.B > c.R + 50 && c.B > c.G) blueEdges++;
                    }
                    else { seaDecks++; if (LandDeck(x, y)) seaOnLand++; }
                }
            Check($"decks over land stand on land ({landDecks} tiles, {blueEdges} with sea-blue edges) and decks over water on water ({seaDecks}, {seaOnLand} wrong)", landDecks >= 3 && blueEdges == 0 && seaDecks >= 20 && seaOnLand == 0);
            // Baga's reef jetty runs from the beach out: its landward tiles are sand-based.
            Check("Baga's jetty starts on the sand", Enumerable.Range(32, 8).Any(y => worldMap[y, 231] == 'b' && Shore(GroundAt(231, y))));
        }
        {
            var a = new Pix(LaharMapW, LaharMapH); var b = new Pix(LaharMapW, LaharMapH);
            PaintLaharMap(a, new LaharWatch { T = -1 }); PaintLaharMap(b, new LaharWatch { T = 5 });
            int diff = 0, land = 0;
            for (int i = 0; i < a.Buf.Length; i++) { if (Math.Abs(a.Buf[i].R - b.Buf[i].R) + Math.Abs(a.Buf[i].G - b.Buf[i].G) > 30) diff++; if (a.Buf[i].G > a.Buf[i].B + 20) land++; }
            Check($"the lahar map is Baga's real ground (land pixels {land}) and shows the mud once the lahar has come ({diff} changed)", land > 6000 && diff > 40);
            var c0 = new Pix(LcW, LcH); var c1 = new Pix(LcW, LcH); var c2 = new Pix(LcW, LcH);
            PaintLaharCam(c0, new LaharWatch { Live = true, T = -1 }); PaintLaharCam(c1, new LaharWatch { Live = true, T = 2 }); PaintLaharCam(c2, new LaharWatch { Live = true, T = 5.5f });
            int Df(Pix p, Pix q) { int d = 0; for (int i = 0; i < p.Buf.Length; i++) if (Math.Abs(p.Buf[i].R - q.Buf[i].R) + Math.Abs(p.Buf[i].G - q.Buf[i].G) + Math.Abs(p.Buf[i].B - q.Buf[i].B) > 40) d++; return d; }
            Check($"the channel camera shows the flow coming down and spilling at the mouth ({Df(c0, c1)}, {Df(c1, c2)} pixels change)", Df(c0, c1) > 250 && Df(c1, c2) > 150);
        }
        {
            var l0 = new Pix(WavesW, WavesH); var l1 = new Pix(WavesW, WavesH);
            PaintTitleWaves(l0, 1.4f); PaintTitleWaves(l1, 5.2f);
            int sea = l0.Buf.Count(c => c.A > 0), diff = 0;
            for (int i = 0; i < l0.Buf.Length; i++) if (Math.Abs(l0.Buf[i].R - l1.Buf[i].R) + Math.Abs(l0.Buf[i].G - l1.Buf[i].G) > 40) diff++;
            Check($"the title's sea is painted ({sea} pixels) and moves ({diff} pixels differ)", sea > 500 && diff > 40);
        }
        state.clock = 12 * 60;
    }
}
#endif
