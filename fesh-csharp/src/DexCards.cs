using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Cards for the Fish log's last page that aren't fish (1.18.1): Tidemane, Bakunawa, the odd catches and the animals you
// can only watch. Click a row and its card opens in place of the page, with a big picture, what you know about it, and
// the real animal behind it. One you haven't found yet shows a question mark and the page's own hint, nothing more.
partial class Game
{
    string dexExtra;    // the open card that isn't a fish: "tidemane", "bakunawa", "odd:<id>" or "sight:<id>"; null for none

    // The real animal (or story) behind each one: a scientific name and a few true things. Nothing here names a fish.
    static readonly Dictionary<string, (string sci, string text)> ExtraFacts = new()
    {
        ["tidemane"] = ("Made up for Fesh", "In ancient Greek myths, hippocamps, half horse and half fish, pulled the sea god's chariot. Scientists borrowed the name: Hippocampus is the genus of the real seahorses."),
        ["bakunawa"] = ("From Philippine stories", "In Visayan stories the Bakunawa, a sea serpent, swallows the moon, and people bang pots and gongs until it lets go. It's an old way of explaining eclipses. In a real lunar eclipse, the Earth passes between the Sun and a full moon, and the Earth's shadow falls on the moon."),
        ["dog"] = ("Canis familiaris", "Many dogs can swim, paddling with all four legs: that's where the name \"doggy paddle\" comes from."),
        ["sheep"] = ("Ovis aries", "The wool of most farm sheep keeps growing until it's sheared. A wet fleece is very heavy, so a sheep in the sea is in real trouble."),
        ["cat"] = ("Felis catus", "Most cats avoid getting wet, but they can swim when they have to."),
        ["pig"] = ("Sus domesticus", "Pigs can hardly sweat, so they roll in mud to keep cool and to keep the sun and insects off their skin."),
        ["parrot"] = ("Parrots (order Psittaciformes)", "Many parrots can copy human speech. Scientists are still studying how much of it they understand."),
        ["goat"] = ("Capra hircus", "Goats are browsers: they would rather nibble leaves, shrubs and twigs than graze on grass."),
        ["pawikan"] = ("Chelonia mydas", "The green sea turtle. Grown-ups mostly eat seagrass and algae. It's named for the greenish colour of its fat, not its shell. Like every sea turtle, it's protected by law in the Philippines."),
        ["dugong"] = ("Dugong dugon", "A sea cow: a plant-eating mammal related to manatees, and more distantly to elephants. It can only live where there are seagrass meadows to graze."),
        ["butanding"] = ("Rhincodon typus", "The whale shark, the biggest fish in the world. It swims with its huge mouth open, filtering plankton and small fish from the water. It's endangered."),
        ["walowalo"] = ("Laticauda colubrina", "The banded sea krait hunts eels in the reef, but comes ashore to rest, digest and lay its eggs. Its venom is strong, but it rarely bites people."),
        ["taklobo"] = ("Tridacna gigas", "The giant clam, the biggest clam in the world. Tiny algae living in its colourful mantle make food from sunlight and share it with the clam."),
        ["tarsier"] = ("Carlito syrichta", "The Philippine tarsier, one of the smallest primates. Each of its eyes is about as big as its brain, and it hunts insects at night."),
        ["hornbill"] = ("Buceros hydrocorax", "The rufous hornbill lives only in the Philippines. It eats mostly fruit and spreads the seeds through the forest."),
        ["alitaptap"] = ("Fireflies (such as Pteroptyx)", "Fireflies are beetles. Males of these mangrove fireflies flash together to show the females where they are, and some gather in the same trees night after night."),
        ["plankton"] = ("Dinoflagellates (such as Noctiluca)", "Single-celled plankton that give off light when the water moves. The flash may startle the tiny animals that try to eat them.")
    };

    // What a card is about: its name, tag, words, a few facts, and whether it's been found.
    (bool got, string name, string tag, Color tagCol, string about, List<(string k, string v)> facts) ExtraInfo(string key)
    {
        var facts = new List<(string, string)>();
        if (key == "tidemane")
        {
            bool got = state.tamed;
            if (got)
            {
                facts.Add(("How", Data.Tidemane.Where));
                facts.Add(("Riding", Bind.Fix("<ride> climbs on, hops off, or whistles it over from anywhere outdoors. It gallops on sand and swims the open sea.")));
            }
            return (got, got ? $"{Data.MountName}, your mount" : "???", "Legend", Pal.C("#10243a"),
                got ? Data.Tidemane.Desc : "Not found yet. " + Data.Tidemane.Hint, facts);
        }
        if (key == "bakunawa")
        {
            bool got = MoonReturned;
            if (got)
            {
                facts.Add(("Where", "Over Parola, on a full-moon night"));
                facts.Add(("After", "It left a shining scale on the pier: the Moonscale charm. The king of the tarpon rises now."));
            }
            // Nameless until the eclipse, as on the page (Codex: the card used to name it).
            return (got, got ? "Bakunawa, the moon-eater" : "???", "Legend", Pal.C("#10243a"),
                got ? "The sea serpent of the old stories, coiled like a sea dragon, with fins down its back and a mouth wide enough for a moon. It rose with the full moon in its jaws, and the whole island beat pots and gongs until it let go."
                    : "Not seen yet. Some full-moon nights, the moon goes out on the Habagat sea.", facts);
        }
        string id = key[(key.IndexOf(':') + 1)..];
        if (key.StartsWith("odd:"))
        {
            var o = Data.OddById[id];
            int n = state.odd.GetValueOrDefault(id);
            if (n > 0)
            {
                facts.Add(("Hooked", $"{Data.SpotById[o.Spot].Label}, {Data.Biomes[Array.FindIndex(Data.Biomes, b => b.Id == Data.SpotById[o.Spot].Biome)].Name}"));
                facts.Add(("Times", n == 1 ? "once" : $"{n} times"));
                facts.Add(("After", o.After));
            }
            return (n > 0, n > 0 ? o.Name : "???", "Odd catch", Pal.Buoy,
                n > 0 ? o.Desc : "Not caught yet. Now and then something that isn't a fish takes the bait.", facts);
        }
        // A sighting: the sanctuary's animals (Bantay Joy's list) or Bakawan's (Tala's).
        bool sea = SeaKinds.ContainsKey(id);
        string name = sea ? SeaKinds[id].name : BakawanKinds[id].name, about = sea ? SeaKinds[id].about : BakawanKinds[id].about;
        int seenN = state.sightings.GetValueOrDefault(id);
        if (seenN > 0)
        {
            facts.Add(("Where", sea ? "The marine sanctuary, on Bantay Joy's list" : "Bakawan Island, on Tala's list"));
            facts.Add(("Seen", seenN == 1 ? "once" : $"{seenN} times"));
        }
        facts.Add(("Rule", "Watch it, never catch it."));
        string hint = sea ? "Not seen yet. Watch the water inside the marine sanctuary's buoys."
            : id is "alitaptap" or "plankton" ? "Not seen yet. Something on Bakawan Island only shows on a dark night." : "Not seen yet. Something lives in Bakawan Island's forest.";
        return (seenN > 0, seenN > 0 ? name : "???", "Sighting: watched, never caught", Pal.C("#2f6a6a"), seenN > 0 ? about : hint, facts);
    }

    void DrawExtraCard()
    {
        var (got, name, tag, tagCol, about, facts) = ExtraInfo(dexExtra);
        string id = dexExtra[(dexExtra.IndexOf(':') + 1)..];
        const float w = DexCardW, h = 520, pad = DexCardPad, picW = DexPicW, picH = DexPicH;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        if (SmallButton("Back", x + w - pad - SmallW("Back"), y + pad - 6)) { dexExtra = null; return; }
        Gfx.Text(name, x + pad, y + pad - 2, FontKind.Ui700, 34, dexExtra is "tidemane" or "bakunawa" && got ? Pal.C("#9a6a1a") : Pal.PaperInk);

        // The picture, and under it the real animal (or story).
        float px = x + pad, py = y + pad + 50;
        Gfx.Rect(px - 3, py - 3, picW + 6, picH + 6, Pal.Ink, 4);
        if (got) DrawExtraArt(dexExtra, id, px, py, picW, picH);
        else
        {
            Gfx.Rect(px, py, picW, picH, Pal.C("#2a5a7a"), 3);
            Gfx.TextCenter("?", px + picW / 2, py + picH / 2 - 30, FontKind.Ui700, 60, Pal.C("#9fc3d1"));
        }
        float bx = px - 3, by = py + picH + 14, bw = picW + 6, bh = y + h - pad - by;
        Gfx.Box(bx, by, bw, bh, Pal.C("#eef6f2"), Pal.C("#8fb8a8"), 2, 5);
        if (got && ExtraFacts.TryGetValue(id, out var real))
        {
            bool story = real.sci.StartsWith("Made up") || real.sci.StartsWith("From ");
            Gfx.Text(story ? "The story behind it" : "The real animal", bx + FactPad, by + FactPad - 2, FontKind.Ui700, 16, story ? Pal.C("#8a6a2a") : Pal.C("#2a7d74"));
            var sci = Gfx.Wrap(real.sci, FontKind.Note, 15, bw - 2 * FactPad);
            Lines(sci, bx + FactPad, by + FactPad + 18, 19, FontKind.Note, 15, Muted);
            var body = Gfx.Wrap(real.text, FontKind.Ui500, FactFs, bw - 2 * FactPad);
            Lines(body, bx + FactPad, by + FactPad + 24 + sci.Count * 19, FactLh, FontKind.Ui500, FactFs, Pal.PaperInk);
            lastExtraBoxBottom = by + FactPad + 24 + sci.Count * 19 + body.Count * FactLh - (by + bh);
        }
        else
        {
            Lines(Gfx.Wrap(got ? "" : "Find it to learn about it.", FontKind.Note, 16, bw - 2 * FactPad), bx + FactPad, by + FactPad, 22, FontKind.Note, 16, Muted);
            lastExtraBoxBottom = 0;
        }

        // The tag, what it is, and the facts.
        float tx = px + picW + 26, ty = py, tw = w - pad * 2 - picW - 26;
        float cw = Gfx.Measure(tag, FontKind.Ui700, 15) + 16;
        Gfx.Rect(tx, ty, cw, 24, tagCol, 4);
        Gfx.Text(tag, tx + 8, ty + 3, FontKind.Ui700, 15, tag == "Legend" ? Pal.Lantern : Pal.Paper);
        ty += 34;
        var aboutLines = Gfx.Wrap(about, FontKind.Note, 19, tw);
        Lines(aboutLines, tx, ty, 26, FontKind.Note, 19, Pal.PaperInk);
        ty += aboutLines.Count * 26 + 12;
        foreach (var (k, v) in facts)
        {
            Gfx.Text(k, tx, ty, FontKind.Ui700, 16, Muted);
            var vl = Gfx.Wrap(v, FontKind.Ui500, 16, tw - 90);
            Lines(vl, tx + 90, ty, 21, FontKind.Ui500, 16, Pal.PaperInk);
            ty += Math.Max(1, vl.Count) * 21 + 7;
        }
        lastCardBottom = ty - y;
        if (Gfx.PressedOutside(x, y, w, h)) dexExtra = null;
    }

    float lastExtraBoxBottom;   // how far the real-animal note ran past its box (0 or less fits; the autotest checks)

    /* ---------- The pictures ---------- */
    // Pixel art for the animals the islands only let you watch, side on, in a scene of their own.
    static readonly Dictionary<string, (string[] rows, Dictionary<char, string> cols)> WatchSprites = new()
    {
        ["pawikan"] = (new[]
        {
            "..........SSSSSSS..........",
            "........SSsSSsSSsSS........",
            ".......SsSSSsSSSsSSSS......",
            "......SSSSsSSSSsSSSSSS.HHH.",
            ".....SSSsSSSSsSSSSsSSSHHHHe",
            "....bbbbbbbbbbbbbbbbbbbHHHH",
            "...F.bbbbbbbbbbbbbbbbb.HHh.",
            "..FF...........FFFF........",
            ".FF.............FFFFF......",
            "FF...............FFFFF.....",
            ".................FFFF......",
        }, new() { ['S'] = "#4f5a2e", ['s'] = "#7a7e3c", ['b'] = "#d8c48a", ['H'] = "#8a9a56", ['h'] = "#6f7f46", ['e'] = "#1b1b1b", ['F'] = "#6f7f46" }),
        ["dugong"] = (new[]
        {
            "...............gggggggg.........",
            "..........ggggggggggggggggg.....",
            "T......gggggggggggggggggggggg...",
            "TT..ggggggggggggggggggggggggge..",
            ".TTggggggggggggggggggggggggggggs",
            "TT...ggggggggggggggggggggggggsss",
            "T.......bbbbbbbbbbbbbbbbbbbbb.s.",
            ".............bbbbbbbbbb..ff.....",
            "..........................ff....",
        }, new() { ['g'] = "#8a8a80", ['b'] = "#b4b2a4", ['T'] = "#76766c", ['e'] = "#1b1b1b", ['s'] = "#9a968a", ['f'] = "#76766c" }),
        ["butanding"] = (new[]
        {
            "...................D................",
            "..................DD................",
            ".................DDD................",
            "T...........BBBBBBBBBBBBBBBBB.......",
            "TT.......BBBwBBBBwBBBBwBBBBwBBBB....",
            ".TT...BBBBwBBBBwBBBBwBBBBwBBBBwBBB..",
            "..TTBBBBBBBBwBBBBwBBBBwBBBBwBBBBBBBe",
            "..TTBBBBwBBBBwBBBBwBBBBwBBBBwBBBBBB.",
            ".TT...WWWWWWWWWWWWWWWWWWWWWWWWWWWWW.",
            "TT.......WWWWWWWWWWWWWWWWWWWWWWWW...",
            "T..............PP.......PP..........",
            "................P........P..........",
        }, new() { ['B'] = "#3e5a78", ['w'] = "#e8f0f4", ['W'] = "#c9d6de", ['T'] = "#344e6a", ['D'] = "#344e6a", ['P'] = "#344e6a", ['e'] = "#101820" })
    };

    void DrawExtraArt(string key, string id, float x, float y, float w, float h)
    {
        // Everything stays inside the picture's frame.
        var clip = Gfx.S(x, y, w, h);
        BeginScissorMode((int)clip.X, (int)clip.Y, (int)MathF.Ceiling(clip.Width), (int)MathF.Ceiling(clip.Height));
        try { DrawExtraScene(key, id, x, y, w, h); }
        finally { EndScissorMode(); }
    }

    void DrawExtraScene(string key, string id, float x, float y, float w, float h)
    {
        bool night = id is "alitaptap" or "plankton" || key == "bakunawa";
        if (key is "tidemane" or "bakunawa")
        {
            Gfx.Portrait(key == "tidemane" ? "tidemane" : "bakunawa", false, x, y, w, h);
            return;
        }
        if (key.StartsWith("odd:") || id is "tarsier" or "hornbill")
        {
            // A land animal: sky, then the sea for the odd catches (they came out of it) or the forest for Bakawan's.
            bool forest = id is "tarsier" or "hornbill";
            var sky = Gfx.S(x, y, w, h * 0.55f);
            DrawRectangleGradientV((int)sky.X, (int)sky.Y, (int)sky.Width, (int)sky.Height, Pal.C(forest ? "#cfe6c0" : "#bfe0ea"), Pal.C(forest ? "#9fc88a" : "#8fc6dc"));
            var ground = Gfx.S(x, y + h * 0.55f, w, h * 0.45f);
            DrawRectangleGradientV((int)ground.X, (int)ground.Y, (int)ground.Width, (int)Math.Ceiling(ground.Height), Pal.C(forest ? "#4f7a3a" : "#3a8db0"), Pal.C(forest ? "#2f5a2a" : "#1d4f78"));
            var tex = AnimalArt.Texture(id);
            var (sw, sh) = AnimalArt.Size(id);
            float k = MathF.Floor(Math.Min(w * (forest ? 0.4f : 0.55f) / sw, h * (forest ? 0.5f : 0.62f) / sh)), feet = y + h * 0.62f;
            if (forest)
            {
                // The forest behind, then the branch it's sitting on.
                for (int i = 0; i < 6; i++)
                {
                    float tx = x + 10 + i * 62;
                    Gfx.Rect(tx + 10, y, 8, h, Pal.C("#5a4028"));
                    Gfx.Circle(tx + 14, y + 10 + i % 2 * 14, 34, Pal.C(i % 2 == 0 ? "#3f7d3a" : "#2f6a2c"));
                }
                Gfx.Rect(x, feet, w, 9, Pal.C("#6b4a2b"), 4);
                Gfx.Rect(x, feet + 7, w, 3, Pal.C("#4a3420"));
                for (int i = 0; i < 5; i++) Gfx.Circle(x + 30 + i * 70, feet - 2, 6, Pal.C("#4f9a45"));
            }
            DrawTexturePro(tex, new Rectangle(0, 0, sw, sh), Gfx.S(x + w / 2 - sw * k / 2, feet - sh * k + (forest ? k : 0), sw * k, sh * k), System.Numerics.Vector2.Zero, 0, Color.White);
            return;
        }
        if (night)
        {
            Gfx.Rect(x, y, w, h, Pal.C("#0c1626"));
            for (int i = 0; i < 18; i++) Gfx.Rect(x + (float)Pix.Hash(i, 3, 71) * w, y + (float)Pix.Hash(i, 4, 71) * h * 0.45f, 2, 2, Pal.Rgba(230, 236, 250, 0.7f));
            if (id == "alitaptap")
            {
                // A pagatpat at the water's edge, its crown flashing all at once, about once a second.
                Gfx.Rect(x, y + h * 0.72f, w, h * 0.28f, Pal.C("#0a1e2c"));
                Gfx.Rect(x + w / 2 - 6, y + h * 0.36f, 12, h * 0.38f, Pal.C("#1d1610"));
                for (int r = -3; r <= 3; r++) Gfx.Line(x + w / 2, y + h * 0.66f, x + w / 2 + r * 14, y + h * 0.78f, 3, Pal.C("#1d1610"));
                foreach (var (cx, cy, cr) in new[] { (0f, 0.3f, 62f), (-60f, 0.4f, 44f), (62f, 0.38f, 46f) })
                    Gfx.Circle(x + w / 2 + cx, y + h * cy, cr, Pal.C("#12261c"));
                float flash = MathF.Max(0, MathF.Sin(time * MathF.Tau / FireflyBeat));
                for (int i = 0; i < 60; i++)
                {
                    float fx = x + w / 2 + ((float)Pix.Hash(i, 1, 9) - 0.5f) * 210, fy = y + h * (0.12f + (float)Pix.Hash(i, 2, 9) * 0.42f);
                    Gfx.Circle(fx, fy, 2.2f, Pal.Rgba(220, 255, 120, 0.15f + 0.85f * flash));
                }
            }
            else
            {
                // Dark water, lit blue-green wherever a paddle stirs it.
                Gfx.Rect(x, y + h * 0.4f, w, h * 0.6f, Pal.C("#08202e"));
                for (int i = 0; i < 90; i++)
                {
                    float u = (float)Pix.Hash(i, 5, 13), v = (float)Pix.Hash(i, 6, 13);
                    float fx = x + 30 + u * (w - 60), fy = y + h * 0.5f + MathF.Sin(u * 9 + time) * 14 + v * h * 0.35f;
                    float glow = 0.4f + 0.6f * MathF.Max(0, MathF.Sin(time * 2 + i));
                    Gfx.Circle(fx, fy, 1.6f + v * 2, Pal.Rgba(90, 240, 220, glow));
                }
                Gfx.Rect(x + w * 0.62f, y + h * 0.3f, 6, h * 0.3f, Pal.C("#5a4028"));
                Gfx.Rect(x + w * 0.62f - 6, y + h * 0.58f, 18, 12, Pal.C("#5a4028"), 3);
            }
            return;
        }
        // Under the sea: light from above, seagrass along the bottom, and the animal.
        var top = Gfx.S(x, y, w, h);
        DrawRectangleGradientV((int)top.X, (int)top.Y, (int)top.Width, (int)top.Height, Pal.C("#4aa4c4"), Pal.C("#1d4f78"));
        for (int i = 0; i < 4; i++) Gfx.Triangle(x + 40 + i * 80, y, x + 70 + i * 80, y, x + 20 + i * 90, y + h * 0.8f, Pal.Rgba(255, 255, 255, 0.06f));
        for (int i = 0; i < 40; i++)
        {
            float gx = x + 4 + i * (w - 8) / 40, sway = MathF.Sin(time * 1.5f + i) * 3;
            Gfx.Line(gx, y + h, gx + sway, y + h - 16 - (float)Pix.Hash(i, 8, 3) * 18, 2, Pal.C(i % 3 == 0 ? "#3f8a3a" : "#5fa84a"));
        }
        if (WatchSprites.TryGetValue(id, out var spr))
        {
            int sw = spr.rows.Max(r => r.Length), sh = spr.rows.Length;
            float k = MathF.Floor(Math.Min((w - 40) / sw, (h - 50) / sh)), bob = MathF.Sin(time * 1.6f) * 3;
            float sx = x + w / 2 - sw * k / 2, sy = y + h * 0.45f - sh * k / 2 + bob;
            for (int r = 0; r < sh; r++)
                for (int c = 0; c < spr.rows[r].Length; c++)
                    if (spr.rows[r][c] != '.') Gfx.Rect(sx + c * k, sy + r * k, k, k, Pal.C(spr.cols[spr.rows[r][c]]));
        }
        else if (id == "walowalo")
        {
            // A banded sea krait rippling along: black and silver-blue bands, a paddle tail, its head lifted.
            const int segs = 34;
            for (int i = 0; i < segs; i++)
            {
                float u = i / (float)segs, sx = x + 40 + u * (w - 90), sy = y + h * 0.5f + MathF.Sin(time * 4 - u * 10) * 18;
                Gfx.Circle(sx, sy, 8, i / 2 % 2 == 0 ? Pal.C("#1e222c") : Pal.C("#a8c4d6"));
            }
            float hx = x + w - 46, hy = y + h * 0.5f + MathF.Sin(time * 4 - 10) * 18;
            Gfx.Circle(hx + 8, hy - 2, 10, Pal.C("#1e222c"));
            Gfx.Rect(hx + 8, hy + 2, 12, 4, Pal.C("#e8d070"), 2);
            Gfx.Circle(hx + 12, hy - 5, 2, Pal.C("#f2f2f2"));
            Gfx.Triangle(x + 34, y + h * 0.5f - 14, x + 34, y + h * 0.5f + 14, x + 52, y + h * 0.5f + MathF.Sin(time * 4) * 18, Pal.C("#1e222c"));
        }
        else
        {
            // A giant clam on the seagrass, seen from the front: a fan of ribbed shell, its wavy lips gaping, and the
            // mantle between them shimmering blue and green, one wavy band right across.
            float cx = x + w / 2, cy = y + h * 0.82f, sh = (MathF.Sin(time * 1.7f) + 1) / 2;
            var mantle = Pal.Rgba((int)(40 + 40 * sh), (int)(150 + 50 * sh), (int)(190 - 40 * sh), 1);
            float Lip(float px) { float u = (px - cx) / 24; return cy - 96 + u * u * 3.4f; }
            for (int i = -4; i <= 4; i++)
                Gfx.Rect(cx + i * 24 - 13, Lip(cx + i * 24), 26, cy - Lip(cx + i * 24), i % 2 == 0 ? Pal.C("#d4d2bf") : Pal.C("#bcbaa6"), 4);
            for (float px = cx - 104; px <= cx + 104; px += 3) Gfx.Circle(px, Lip(px) + 9, 9, mantle);
            for (int i = 0; i < 16; i++) { float px = cx - 96 + i * 12.8f; Gfx.Circle(px, Lip(px) + 9 + (i % 2 == 0 ? -3 : 3), 2, Pal.Rgba(220, 250, 255, 0.85f)); }
            // The shell's zigzag lips over the mantle's edges, top and bottom.
            for (float px = cx - 108; px < cx + 104; px += 12)
            {
                Gfx.Triangle(px, Lip(px) - 2, px + 12, Lip(px + 12) - 2, px + 6, Lip(px + 6) + 5, Pal.C("#e2e0cf"));
                Gfx.Triangle(px, Lip(px) + 21, px + 12, Lip(px + 12) + 21, px + 6, Lip(px + 6) + 13, Pal.C("#a8a694"));
            }
        }
    }
}
