using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The title's opening and the ending card (1.44, graphics item 28).
//
// The opening plays once each time the game starts, as a rise from the bottom of the sea: on the seabed lies the wreck of
// the smugglers' boat from story 1, and in the trench beside it two eyes open, look, blink and close; we rise past a reef with a turtle
// gliding over it, jellyfish pulsing, a school of fish and a little glowing fish darting by, up to where a banca sits on
// the surface with its line down; a fish takes the bait, the line jerks up and we follow it out of the water. The
// surface rushes down past us into the islands' tour, the four letters of "Fesh" drop in one at a time with a splash each,
// then the waves, the line about the game and the buttons arrive. Any key, click or button skips it. With Reduce motion
// it's a short fade instead.
//
// The ending card (Saltmere's mystery solved) is a sunset over the old dock with you and Tomas on it, Mara's locket (a
// Blender model, tools/blender/locket.py) catching the light, the story, what you did, and Keep exploring.
partial class Game
{
    float introT;
    bool introDone;
    const float IntroLen = 8.0f, IntroCalmLen = 1.0f;
    float IntroLength => Calm ? IntroCalmLen : IntroLen;
    bool IntroOn => !introDone && mode == "title" && titleView == "main";
    // How far through the opening we are (past the end once it's over or skipped).
    float IntroAt => introDone ? 99 : introT;

    // Phases (seconds): on the seabed, the eyes open at 0.4 and close by 1.5; we rise from `RiseAt` to `RiseEnd`; the fish
    // takes the bait at `BiteAt` and the line jerks up from `JerkAt`; the surface goes by from `SurfaceAt`; the letters
    // land from `LettersAt`; then the waves, the line about the game and the buttons.
    const float EyesAt = 0.4f, EyesEnd = 1.5f, RiseAt = 1.5f, RiseEnd = 3.6f, BiteAt = 3.9f, JerkAt = 4.2f;
    const float SurfaceAt = 4.5f, SurfaceLen = 0.9f, LettersAt = 5.2f, LetterGap = 0.22f, LetterFall = 0.45f, WavesAt = 6.2f, TagAt = 6.6f, ButtonsAt = 7.2f;
    // The sea we rise through, in pixels: the surface is 0, the seabed DeepH down; the view is 180 tall.
    const int DeepH = 540;

    void UpdateTitleIntro(float dt)
    {
        if (!IntroOn) return;
        float before = introT;
        introT += dt;
        if (!Calm)
        {
            bool Now(float at) => before < at && introT >= at;
            if (Now(EyesAt)) Sfx.Play("deep");
            if (Now(BiteAt)) Sfx.Play("bite");
            if (Now(JerkAt)) Sfx.Play("cast");
            if (Now(SurfaceAt)) Sfx.Play("splash");
            for (int i = 0; i < 4; i++)
            {
                float land = LettersAt + i * LetterGap + LetterFall;
                if (before < land && introT >= land) Sfx.Play(i == 3 ? "catch" : "blip");
            }
        }
        if (introT >= IntroLength) introDone = true;
    }

    // Anything pressed skips it (HandleKeys asks before anything else, even the fullscreen key). The buttons it reveals wait
    // a frame (introJustSkipped), so the click that skipped can't also land on one (Codex).
    bool introJustSkipped;
    static readonly GamepadButton[] AnyPad = Enum.GetValues<GamepadButton>().Where(b => b != GamepadButton.Unknown).ToArray();
    bool SkipIntro()
    {
        if (!IntroOn) return false;
        if (Inp.FirstPressed() != null || Gfx.Pressed || AnyPad.Any(Inp.PadPressed)) { introDone = true; introJustSkipped = true; Sfx.Play("ui"); return true; }
        return false;
    }

    // 0 to 1 over a span of the opening (1 once it's over).
    float IntroK(float from, float len) => Math.Clamp((IntroAt - from) / len, 0, 1);

    // The sea we start under, sliding down out of the way as we come up. With Reduce motion it just fades.
    void DrawIntroCover()
    {
        if (introDone) return;
        if (Calm)
        {
            float a = 1 - IntroK(0, IntroCalmLen);
            if (a > 0) Gfx.Rect(0, 0, Gfx.LW, Gfx.LH, Pal.Rgba(11, 35, 56, a));
            return;
        }
        float k = IntroK(SurfaceAt, SurfaceLen);
        if (k >= 1) return;
        float ease = k * k * (3 - 2 * k);
        float drop = ease * (Gfx.LH + 40);
        PixelScene(0, drop, 320, 180, 4, p => PaintUnderwater(p, introT, IntroCamY(introT)), 61);
    }

    // How deep the view's top edge is: on the seabed, then rising (eased) to the surface.
    static float IntroCamY(float t)
    {
        float k = Math.Clamp((t - RiseAt) / (RiseEnd - RiseAt), 0, 1);
        return (DeepH - 180) * (1 - k * k * (3 - 2 * k));
    }

    // The old wreck (the smugglers' boat, a Blender model in LandmarkArt), seen through deep water: its colours pulled towards the blue.
    static string[] wreckDeep;
    static string[] WreckDeepPalette => wreckDeep ??= LandmarkArt.WreckPalette.Select(h => Hex(Pix.Mix(Pal.C(h), Pal.C("#0c2a42"), 0.62f))).ToArray();
    static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    // The big "Fesh", a letter at a time during the opening: each drops in from above, overshoots a touch and settles,
    // with a ring of spray where it lands. Returns nothing; draws the three layers (shadow, outline, gold) the title uses.
    void DrawTitleLetters(float cx, float y, float size)
    {
        const string word = "Fesh";
        float total = Gfx.Measure(word, FontKind.Ui700, size), x0 = cx - total / 2;
        for (int i = 0; i < word.Length; i++)
        {
            string ch = word[i].ToString();
            float lx = x0 + Gfx.Measure(word[..i], FontKind.Ui700, size);
            float start = LettersAt + i * LetterGap;
            float k = Calm ? 1 : IntroK(start, LetterFall);
            if (k <= 0) continue;
            // Falls with a little overshoot (ease out back), from 420 px above.
            float c1 = 1.7f, back = 1 + (c1 + 1) * MathF.Pow(k - 1, 3) + c1 * MathF.Pow(k - 1, 2);
            float dy = (1 - back) * -420;
            Gfx.Text(ch, lx, y + 21 + dy, FontKind.Ui700, size, Pal.C("rgba(0,0,0,0.22)"));
            Gfx.Text(ch, lx, y + 10 + dy, FontKind.Ui700, size, Pal.Ink);
            Gfx.Text(ch, lx, y + dy, FontKind.Ui700, size, Pal.Sand);
            // Spray where it landed: drops flying out and falling, fading.
            float age = IntroAt - (start + LetterFall);
            if (!Calm && age > 0 && age < 0.7f)
            {
                float mx = lx + Gfx.Measure(ch, FontKind.Ui700, size) / 2, my = y + 150;
                for (int d = 0; d < 8; d++)
                {
                    float ang = MathF.PI * (1.05f + d * 0.13f), sp = 120 + d % 3 * 40;
                    float px = mx + MathF.Cos(ang) * sp * age, py = my + MathF.Sin(ang) * sp * age + 300 * age * age;
                    Gfx.Circle(px, py, 4, Pal.Rgba(232, 246, 251, 1 - age / 0.7f));
                }
            }
        }
    }

    /* ---------- The ending card ---------- */
    const float EndW = 860, EndSceneW = 200, EndSceneH = 55, EndScale = 4;
    float lastEndBottom;
    float endOpenedAt = -1;

    // A sunset over Saltmere's old dock: you and Tomas on it looking out, the sun low, glitter on the water, gulls.
    void PaintEndScene(Pix p, float t)
    {
        int w = (int)EndSceneW, h = (int)EndSceneH, horizon = 30;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c;
                if (y < horizon)
                {
                    float k = y / (float)horizon;
                    c = k < 0.55f ? Pix.Mix(Pal.C("#3a3f78"), Pal.C("#d8735a"), k / 0.55f) : Pix.Mix(Pal.C("#d8735a"), Pal.C("#f4b860"), (k - 0.55f) / 0.45f);
                }
                else
                {
                    float k = (y - horizon) / (float)(h - horizon);
                    c = Pix.Mix(Pal.C("#c9786a"), Pal.C("#2a3f66"), k);
                    // The sun's path on the water: broken glitter under it.
                    float near = 1 - MathF.Abs(x - 132) / (10 + k * 26);
                    if (near > 0 && MathF.Sin(x * 0.9f + y * 2.1f + (Calm ? 0 : t * 3)) > 0.35f) c = Pix.Mix(c, Pal.C("#ffd98a"), near * 0.85f);
                }
                p.Buf[y * w + x] = c;
            }
        // The sun, half under the horizon.
        for (int y = horizon - 9; y < horizon; y++)
            for (int x = 122; x < 143; x++)
                if ((x - 132) * (x - 132) + (y - horizon) * (y - horizon) < 100) p.Buf[y * w + x] = Pal.C("#ffe6a0");
        // A few clouds lit from below.
        foreach (var (cx, cy, cw) in new[] { (30f, 9f, 34f), (78f, 14f, 22f), (168f, 7f, 40f) })
            for (int x = (int)(cx - cw / 2); x < cx + cw / 2; x++) { Dot(p, x, (int)cy, Pal.C("#f2a982")); if (Math.Abs(x - cx) < cw * 0.3f) Dot(p, x, (int)cy - 1, Pal.C("#e88f74")); }
        // Gulls: little dark Vs, drifting.
        for (int i = 0; i < 3; i++)
        {
            float gx = 40 + i * 31 + (Calm ? 0 : MathF.Sin(t * 0.4f + i) * 6), gy = 11 + i * 4;
            int flap = Calm ? 0 : (int)(t * 4 + i) % 2;
            Dot(p, (int)gx, (int)gy, Pal.C("#2a2430")); Dot(p, (int)gx - 1, (int)gy - flap, Pal.C("#2a2430")); Dot(p, (int)gx + 1, (int)gy - flap, Pal.C("#2a2430"));
            Dot(p, (int)gx - 2, (int)gy - 1, Pal.C("#2a2430")); Dot(p, (int)gx + 2, (int)gy - 1, Pal.C("#2a2430"));
        }
        // The dock: planks on posts reaching out from the left.
        for (int x = 0; x < 72; x++) { Dot(p, x, 41, Pal.C("#5b3a24")); Dot(p, x, 42, x % 6 == 0 ? Pal.C("#2e1c12") : Pal.C("#7a5230")); Dot(p, x, 43, Pal.C("#3e2618")); }
        for (int px = 4; px < 72; px += 12) for (int y = 44; y < 51; y++) Dot(p, px, y, Pal.C("#2e1c12"));
        // You and Tomas, side by side at the end of it, looking out to sea (seen from behind).
        LookData.DrawPerson(p, state.look, 58, 41, "up", 0, shadow: false);
        var tomas = new Look { name = "Tomas", skin = 2, hair = 0, hairColor = 3, hat = 0, shirt = 3, pants = 2 };
        LookData.DrawPerson(p, tomas, 49, 41, "up", 0, shadow: false);
    }

    // Mara's locket, with a band of light sweeping across the gold every few seconds (still with Reduce motion).
    void PaintLocket(Pix p, float t)
    {
        BlitArtTo(p, LocketArt.Grid, LocketArt.Palette, LocketArt.AnchorX, LocketArt.AnchorY, LocketArt.AnchorX, LocketArt.AnchorY);
        if (Calm) return;
        float sweep = (t % 3.5f) / 1.1f;   // across in 1.1 s, then a rest
        if (sweep > 1) return;
        float line = -20 + sweep * (p.W + p.H + 40);
        for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
            {
                var c = p.Buf[y * p.W + x];
                if (c.A == 0 || c.R < 120 || c.B > c.R - 40) continue;   // only the gold
                float d = MathF.Abs(x + y * 0.6f - line);
                if (d < 3) p.Buf[y * p.W + x] = Pix.Mix(c, Pal.C("#fff8d8"), 0.65f * (1 - d / 3));
            }
    }

    void DrawEndCard()
    {
        if (endOpenedAt < 0) endOpenedAt = (float)GetTime();
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Pal.C("rgba(8,18,32,0.9)"));
        Gfx.Block(-5000, -5000, 10000, 10000);
        const float pad = 30, iw = EndW - 2 * pad;
        var story = Gfx.Wrap("Smugglers dragged the Lalim, the deep channel off Pulo ng Asin, and shipped what they caught abroad as farmed guppies. Mara Ilao of the Bantay Dagat got aboard in a storm and freed the creatures as the boat sank, and the guardian of the deep kept her locket for her father.",
            FontKind.Note, 21, iw);
        var more = Gfx.Wrap(Bind.Fix("Tomas has the patrol back on the water. The sea is wide: an atoll to the east, islands beyond it, and more stories waiting. Your journal (<journal>) knows where to start."),
            FontKind.Ui600, 18, iw);
        float sceneH = EndSceneH * EndScale;
        float h = pad + sceneH + 22 + 64 + 10 + story.Count * 29 + 16 + 40 + 16 + more.Count * 25 + 20 + 56 + pad;
        float x = (Gfx.LW - EndW) / 2, y = MathF.Max(6, (Gfx.LH - h) / 2);
        lastEndBottom = y + h;
        float shown = (float)GetTime() - endOpenedAt, rise = Calm ? 0 : MathF.Max(0, 1 - shown / 0.5f) * 24;
        y += rise;
        CardGlow(x, y, EndW, h, 0.8f);
        PanelBox(x, y, EndW, h);
        float sy = y + pad;
        Gfx.Rect(x + pad - 3, sy - 3, iw + 6, sceneH + 6, Pal.Ink, 4);
        PixelScene(x + pad, sy, (int)EndSceneW, (int)EndSceneH, iw / EndSceneW, p => PaintEndScene(p, time), 62);
        // The locket, hung over the corner of the picture.
        const float lk = 3;
        PixelScene(x + EndW - pad - 44 * lk + 18, sy + sceneH - 64 * lk + 70, 44, 64, lk, p => PaintLocket(p, time), 63);
        float cy = sy + sceneH + 22;
        Gfx.Text("Case closed", x + pad + 2, cy + 3, FontKind.Ui700, 60, Pal.C("rgba(0,0,0,0.18)"));
        Gfx.Text("Case closed", x + pad, cy, FontKind.Ui700, 60, Pal.C("#9a6a1a"));
        cy += 64 + 10;
        Lines(story, x + pad, cy, 29, FontKind.Note, 21, Pal.PaperInk);
        cy += story.Count * 29 + 16;
        // What you did, as little chips.
        var chips = new[]
        {
            $"Creatures 5 of 5", $"{state.commons.Where(kv => Data.FishById.ContainsKey(kv.Key)).Sum(kv => kv.Value)} fish caught",
            $"{state.casts} casts", $"{state.builds.Count} things built", $"Day {state.day}", PlayTime(state.playSecs) + " played"
        };
        float chx = x + pad;
        foreach (var c in chips)
        {
            float cw2 = Gfx.Measure(c, FontKind.Ui600, 16) + 22;
            if (chx + cw2 > x + EndW - pad) break;
            Gfx.Rect(chx, cy, cw2, 32, Pal.C("#efe0bc"), 6);
            Gfx.Text(c, chx + 11, cy + 7, FontKind.Ui600, 16, Pal.PaperInk);
            chx += cw2 + 8;
        }
        cy += 40 + 16;
        Lines(more, x + pad, cy, 25, FontKind.Ui600, 18, Pal.C("#2a6a7a"));
        cy += more.Count * 25 + 20;
        // Only one way on from here: back out to sea. (A new game is on the title screen, from the menu.)
        if (shotReq != null) return;   // (the buttons are left out of a saved image)
        if (BigButton("Keep exploring", x + pad, cy, true)) { endOpenedAt = -1; KeepFishing(); return; }
        const string save = "Save as image";
        if (shotReq == null && SmallButton(save, x + EndW - pad - SmallW(save), cy + 6))
            shotReq = new ShotRequest { Area = new Rectangle(x - 4, y - 4, EndW + 8, h + 8), Name = "case-closed", Still = () => mode == "ending" };
    }
}
