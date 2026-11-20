#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// FESH_ANIM_TEST=1 (1.48): the new animation frames. Renders the player's poses one by one into a strip (anim-poses.png,
// 8x) and checks them: the crouch into a full cast, the jolt on striking a bite, the hop holding up a catch (with the
// shadow staying on the ground), a foot tap and a head scratch when standing about, the three-quarter look while turning
// (and it ending), Tomas stroking his beard, and the tool wind-up standing above the head.
partial class Game
{
    IEnumerable<int> AnimScript()
    {
        Note("Animation frames: fishing poses, idle moves, turning and tool wind-ups");
        state = new State { created = true, look = new Look { name = "Mover", hat = 1 } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        yield return 3;
        var keep = pix;
        var strip = new List<(string name, Pix p)>();
        Pix Frame(string name, Action setup)
        {
            player.X = 160; player.Y = 115; player.Moving = false;
            pix = new Pix(40, 40) { CamX = 140, CamY = 87 };
            Array.Fill(pix.Buf, Pal.C("#86b86a"));
            setup();
            DrawPlayer();
            var p = pix; pix = keep;
            strip.Add((name, p));
            mode = "play"; fish = null; reel = null; heldT = 0; heldItem = null; heldPeak = 0; swingT = 0; idleT = 0; charge = 0;
            return p;
        }
        // The topmost drawn row of a frame (not the grass).
        int Top(Pix p) { var g = Pal.C("#86b86a"); for (int y = 0; y < p.H; y++) for (int x = 0; x < p.W; x++) { var c = p.Buf[y * p.W + x]; if (c.R != g.R || c.G != g.G || c.B != g.B) return y; } return p.H; }
        // The top of the straw hat (its crown colour), so a rod or a held-up fish above it doesn't count.
        int HatTop(Pix p) { var h = Pal.C("#e3b456"); for (int y = 0; y < p.H; y++) for (int x = 0; x < p.W; x++) { var c = p.Buf[y * p.W + x]; if (c.R == h.R && c.G == h.G && c.B == h.B) return y; } return p.H; }
        bool Shadow(Pix p, int y) { var g = Pal.C("#86b86a"); var c = p.Buf[y * p.W + 20]; return c.G < g.G - 10; }

        player.Face = "right"; turnLast = "right"; turnFrom = null;
        var stand = Frame("stand", () => { });
        var crouch = Frame("crouch", () => { mode = "charging"; charge = 1; });
        Check($"a full power cast crouches you (hat at row {HatTop(crouch)} vs {HatTop(stand)} standing)", HatTop(crouch) > HatTop(stand));
        var strike = Frame("strike", () =>
        {
            var f = Data.AllCommon.First(c => c.Style == "dart");
            TestBite("lagoon", f.Id); StartReel(); mode = "reeling"; reel.Age = 0.05f;
        });
        Check($"striking a bite jolts you up (hat at row {HatTop(strike)} vs {HatTop(stand)})", HatTop(strike) < HatTop(stand));
        var hop = Frame("hop", () => { heldItem = "tilapia"; heldT = 2.875f; heldPeak = 3f; });
        var held = Frame("held", () => { heldItem = "tilapia"; heldT = 1.0f; heldPeak = 3f; });
        Check($"holding up a catch you hop for joy, then stand (hat at row {HatTop(hop)} hopping, {HatTop(held)} after)", HatTop(hop) < HatTop(held));
        Check("and your shadow stays on the ground while you hop", Shadow(hop, 115 - 87));

        player.Face = "down"; turnLast = "down"; turnFrom = null;
        var idle0 = Frame("idle", () => { idleT = 3.5f; });
        Pix tapA = null, tapB = null;
        for (int k = 0; k < 2; k++) { float t = 0.01f + k * 0.125f; var fr = Frame($"tap{k}", () => { idleT = 6f; time = t; }); if (k == 0) tapA = fr; else tapB = fr; }
        bool differs = false;
        for (int i = 0; i < tapA.Buf.Length; i++) if (tapA.Buf[i].R != tapB.Buf[i].R || tapA.Buf[i].G != tapB.Buf[i].G) { differs = true; break; }
        Check("standing about, you tap a foot now and then", differs);
        var scratch = Frame("scratch", () => { idleT = 11.5f; });
        var skin = Pal.C(LookData.Skins[state.look.skin % LookData.Skins.Length]);
        bool handHigh = false;
        for (int y = 0; y < 115 - 87 - 12; y++) for (int x = 24; x < 30; x++) { var c = scratch.Buf[y * 40 + x]; if (c.R == skin.R && c.G == skin.G && c.B == skin.B) handHigh = true; }
        Check("and scratch your head (a hand up beside it)", handHigh);

        // Turning from facing down to facing right: a moment with the head round and the body still facing you.
        player.Face = "down"; turnLast = "down"; turnFrom = null; Frame("face down", () => { });
        player.Face = "right";
        var turning = Frame("turning", () => { time = 100f; });
        var turned = Frame("turned", () => { time = 100.2f; });
        bool turnShows = false;
        for (int i = 0; i < turning.Buf.Length; i++) if (turning.Buf[i].R != turned.Buf[i].R || turning.Buf[i].B != turned.Buf[i].B) { turnShows = true; break; }
        Check("turning shows a three-quarter look for a moment, then the new facing", turnShows && turnUntil - 100f is > 0 and < 0.1f);

        // A tool's wind-up stands above the head.
        player.Face = "right"; turnLast = "right";
        var wind = Frame("axe up", () => { Swing("axe", 0.4f); swingT = 0.4f * (1 - 0.1f); });
        var head = Frame("no tool", () => { });
        Check($"an axe raised to swing reaches above your head (top row {Top(wind)} vs {Top(head)})", Top(wind) < Top(head));
        Frame("axe down", () => { Swing("axe", 0.4f); swingT = 0.4f * (1 - 0.32f); });

        // Tomas strokes his beard now and then.
        {
            float keepT = time;
            pix = new Pix(40, 40) { CamX = (int)tomasX - 20, CamY = (int)tomasY - 28 };
            Array.Fill(pix.Buf, Pal.C("#86b86a"));
            time = 9f - 2.3f + 0.4f; DrawTomas();
            strip.Add(("tomas", pix));
            pix = keep; time = keepT;
        }

        // Villagers: a stroller remembers a change of facing for a moment, and drawing with it shows the three-quarter look.
        {
            float keepNow = LookData.Now;
            var s = new Stroller { HomeX = 0, HomeY = 0 };
            LookData.Now = 50f; s.Face = "right";
            bool turningNow = s.Turning == "down";
            LookData.Now = 50.2f;
            Check("a villager who turns remembers it for a moment, then not", turningNow && s.Turning == null);
            Pix Fig(string from, float now, bool blinkOn)
            {
                var p = new Pix(40, 40) { CamX = 0, CamY = 0 };
                Array.Fill(p.Buf, Pal.C("#86b86a"));
                LookData.Now = now; LookData.AutoBlink = blinkOn; LookData.TurnFrom = from;
                LookData.DrawFigure(p, "#c68a5a", "#2b1d14", "#3f6f9a", "#4a4038", false, 0, null, 20, 30, "right", 0);
                LookData.AutoBlink = false;
                return p;
            }
            var a = Fig("down", 50f, false); var b = Fig(null, 50f, false);
            bool diff = false; for (int i = 0; i < a.Buf.Length; i++) if (a.Buf[i].R != b.Buf[i].R || a.Buf[i].B != b.Buf[i].B) { diff = true; break; }
            Check("and is drawn mid-turn (head round, body still on its way)", diff);
            // Standing about with their idle moves on (they're off in test runs), a hand goes up to the head at some point.
            var vskin = Pal.C("#c68a5a"); bool scratched = false;
            for (float t = 0.05f; t < 13 && !scratched; t += 0.1f)
            {
                var p = Fig(null, t, true);
                for (int y = 8; y < 15 && !scratched; y++) for (int x = 10; x < 30; x++) { var c = p.Buf[y * 40 + x]; if (c.R == vskin.R && c.G == vskin.G && c.B == vskin.B) { scratched = true; break; } }   // skin above the face: a hand
            }
            Check("villagers scratch their head now and then while standing about", scratched);
            LookData.Now = keepNow; LookData.TurnFrom = null;
        }

        // The boat at the helm: a quarter turn shows the hull three-quarter on for a moment; an about-turn the middle view.
        {
            var keepP = pix; float keepTime = time;
            bool keepAboard = state.aboard; float kx = player.X, ky = player.Y; string kf = player.Face;
            state.inv["boat"] = 1; state.aboard = true;
            var boats = new List<Pix>();
            Pix Boat(string from, string to, float dt)
            {
                var p = new Pix(60, 50) { CamX = 1150 - 30, CamY = 380 - 28 };
                Array.Fill(p.Buf, Pal.C("#1d5f8c"));
                pix = p; player.X = 1150; player.Y = 380;
                boatFace = from; player.Face = from; time = 200; DrawHelmsman();   // settles on "from"
                boatFace = to; player.Face = to; time = 200.01f; DrawHelmsman();   // the turn starts
                if (dt > 0.05f) { Array.Fill(p.Buf, Pal.C("#1d5f8c")); time = 200.01f + dt; DrawHelmsman(); }   // and has finished
                pix = keepP;
                boats.Add(p);
                return p;
            }
            var quarter = Boat("right", "up", 0.05f);
            var settled = Boat("right", "up", 0.3f);
            bool qDiff = false; for (int i = 0; i < quarter.Buf.Length; i++) if (quarter.Buf[i].R != settled.Buf[i].R || quarter.Buf[i].B != settled.Buf[i].B) { qDiff = true; break; }
            Check("a quarter turn at the helm swings the hull three-quarter on for a moment", qDiff && boatDiag == null);
            Boat("left", "down", 0.05f); Boat("down", "right", 0.05f); Boat("up", "left", 0.05f); Boat("right", "left", 0.05f);
            var bimg = GenImageColor(60 * boats.Count, 50, Color.Black);
            for (int b = 0; b < boats.Count; b++)
                for (int y = 0; y < 50; y++)
                    for (int x = 0; x < 60; x++) ImageDrawPixel(ref bimg, b * 60 + x, y, boats[b].Buf[y * 60 + x]);
            ImageResizeNN(ref bimg, 60 * boats.Count * 5, 50 * 5);
            ExportImage(bimg, Path.Combine(testDir, "anim-boat-turns.png"));
            UnloadImage(bimg);
            state.aboard = keepAboard; player.X = kx; player.Y = ky; player.Face = kf; boatFace = "right"; time = keepTime;
        }

        // The strip, 8x, with the walk on the front and back drawn in order below it.
        int n = strip.Count, w = n * 40;
        var img = GenImageColor(w, 40, Color.Black);
        for (int i = 0; i < n; i++)
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++) ImageDrawPixel(ref img, i * 40 + x, y, strip[i].p.Buf[y * 40 + x]);
        ImageResizeNN(ref img, w * 6, 40 * 6);
        ExportImage(img, Path.Combine(testDir, "anim-poses.png"));
        UnloadImage(img);
        Note("poses: " + string.Join(", ", strip.Select(s => s.name)));
        yield return 1;
    }
}
#endif
