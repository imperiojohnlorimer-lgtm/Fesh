using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Catch and release (1.17). For a few seconds after you land a fish, F lets it go. A juvenile (a catch well under the
// usual size) or a fish in its closed season is worth letting go: it counts for the Sea school's "Let it go" badge, and
// any fish you let go may turn up again at the same spot a day or more later, grown. Galunggong and tamban are in their
// closed season in the amihan, when Pip won't buy them (Pip's own rule, inspired by real closed seasons; the real ones
// are on their Fish log cards). Crabs and lobsters from a pot go on a sorting tray first: measure the shell against the
// keep line, turn it over to look for eggs, then keep it or let it go. Sort eight right in a row and you sort by eye.
sealed class Landed { public string Id, Spot; public float Kg, T, WaterX, WaterY; public bool Big, Small, Closed; }
sealed class CrabSort { public string Id; public float Width, Line; public bool Female, Berried, LateEggs, Big, Turned, Decided, Kept, Right, Fine; public string Note; }

partial class Game
{
    const float SmallM = 0.62f;                 // a catch rolled this far under the usual size is a juvenile
    const int QuickSortStreak = 8;              // crabs sorted right in a row before you sort by eye
    static readonly HashSet<string> ClosedFish = new() { "galunggong", "tamban" };
    bool ClosedSeason(string id) => Season == "amihan" && ClosedFish.Contains(id);

    Landed landed;              // the fish you've just landed, while you can still let it go
    float lastM;                // how big the last AddCatch was against its fish's usual size
    (float x0, float y0, float x1, float y1, float t, string id)? releaseFx;

    bool CanRelease => landed != null && landed.T > 0 && mode == "play" && Has(landed.Id) > 0;
    string ReleaseLabel => (landed.Small ? "Let the little one go" : landed.Closed ? "Let it go (closed season)" : "Let it go") + $" ({MathF.Ceiling(landed.T):0})";

    // A fish you let go, and the chance it's back, grown, at the same spot a day or more later.
    float ReturnChance => Wears("dehooker") ? 0.6f : 0.35f;

    LetGo Returning(string id, string spot)
    {
        var e = state.letGo.FirstOrDefault(r => r.id == id && r.spot == spot && r.day < state.day);
        if (e == null || rng.NextDouble() >= ReturnChance) return null;
        state.letGo.Remove(e);
        return e;
    }

    // After a rod catch (LandCatch): what you can let go, and a word the first time it's worth it.
    void NoteLanded(CommonFish f, string spot, float kg, float wx, float wy, bool quiet)
    {
        bool small = lastM < SmallM, closed = ClosedSeason(f.Id);
        landed = new Landed { Id = f.Id, Spot = spot, Kg = kg, Big = lastM >= 1.25f, Small = small, Closed = closed, T = small || closed ? 6 : 3.5f, WaterX = wx, WaterY = wy };
        if (small) Floater("Undersized", player.X, player.Y - 40, "#9fd8f0");
        else if (closed) Floater("Closed season", player.X, player.Y - 40, "#f0b08a");
        if (small || closed) heldT = Math.Max(heldT, 2.4f);
        if (quiet) return;
        // The first time, a word about it once the catch's own message has had its turn (and longer to decide).
        if (small && !state.Hinted("letgo:small"))
        {
            state.hinted["letgo:small"] = true;
            landed.T = 12;
            ToastLater($"A little one: {Kg(kg)}, well under a grown {f.Name.ToLowerInvariant()}. It hasn't had a chance to spawn yet. Press <alt> to let it go and grow. You might meet it again!");
        }
        else if (closed && !state.Hinted("letgo:closed"))
        {
            state.hinted["letgo:closed"] = true;
            landed.T = 12;
            ToastLater($"Pip's closed season: galunggong and tamban spawn in the amihan, so she won't buy them until the habagat (like the real closed seasons in some Philippine waters). Press <alt> to let it go.");
        }
    }

    static readonly string[] ReleaseTips =
    {
        "Back it goes. Quick is best: a fish breathes through its gills, and every second in the air costs it.",
        "A fish you let go can grow, spawn and be caught again, bigger.",
        "Wet your hands before you hold a fish you'll let go: dry hands rub off the slime that protects its skin.",
        "Big old females lay far more eggs than small ones, so letting a big one go now and then keeps the fishing good.",
        "Hold it gently under the belly, not by the gills, and let it swim off on its own."
    };

    void ReleaseCatch()
    {
        var c = landed;
        if (c == null || Has(c.Id) == 0) return;
        landed = null;
        Take(c.Id);
        // It was one of the big ones Pip pays extra for: it isn't in the bag any more.
        if (c.Big && state.big.GetValueOrDefault(c.Id) > 0) state.big[c.Id]--;
        heldT = 0;
        bool good = c.Small || c.Closed;
        state.releasedAll++;
        if (good) state.released++;
        state.letGo.RemoveAll(e => state.day - e.day > 12);
        state.letGo.Add(new LetGo { id = c.Id, spot = c.Spot, day = state.day, kg = c.Kg });
        if (state.letGo.Count > 30) state.letGo.RemoveAt(0);
        // Letting a little one or a spawning one go is worth a little experience; the dehooker makes any release worth some.
        GainXp((good ? 4 : 0) + (Wears("dehooker") ? 3 : 0));
        float hx = player.X + (player.Face == "left" ? -4 : player.Face == "right" ? 4 : 0), hy = player.Y - 16;
        releaseFx = (hx, hy, c.WaterX, c.WaterY, 0, c.Id);
        Sfx.Play("release");
        Floater(good ? "Let go to grow" : "Let go", player.X, player.Y - 30, "#9fe0b0");
        string tip = c.Closed ? $"Let go in the closed season: the shoal spawns in peace, and there'll be more {Items.ById[c.Id].Name.Split(' ')[0].ToLowerInvariant()} in the habagat."
            : ReleaseTips[(state.releasedAll - 1) % ReleaseTips.Length];
        Toast(tip, 4.5f);
        CheckBadges();
        Save();
    }

    void UpdateRelease(float dt)
    {
        if (landed != null && mode == "play") { landed.T -= dt; if (landed.T <= 0 || player.Moving) landed = null; }
        else if (landed != null && mode is not ("pause" or "panel" or "dialogue")) landed = null;
        if (releaseFx is var (x0, y0, x1, y1, t, id))
        {
            t += dt;
            if (t < 0.4f && t + dt >= 0.4f) { Burst(x1, y1, "#e8f6fb", 8); Sfx.Play("splash"); }
            releaseFx = t > 2f ? null : (x0, y0, x1, y1, t, id);
        }
    }

    // The fish flies from your hands to the water, splashes, and swims away as a shadow.
    void DrawRelease(float time)
    {
        if (releaseFx is not var (x0, y0, x1, y1, t, id)) return;
        float dx = x1 - x0, dy = y1 - y0, d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        if (t < 0.4f)
        {
            float k = t / 0.4f, x = x0 + dx * k, y = y0 + dy * k - MathF.Sin(k * MathF.PI) * 10;
            FishArt.Draw(pix, id, (int)x - 4, (int)y - 3, 9, 6, flip: dx < 0);
            return;
        }
        float s = (t - 0.4f) / 1.6f;
        float sx = x1 + dx / d * s * 22, sy = y1 + dy / d * s * 22;
        var body = Pal.Rgba(10, 30, 45, 0.5f * (1 - s));
        int dir = dx < 0 ? -1 : 1, wag = (int)(t * 10) % 2;
        pix.Rect(sx - 2, sy - 1, 5, 2, body);
        pix.Rect(sx - 2 - dir * 2, sy - 2 + wag, 1, 1, body);
        pix.Rect(sx - 2 - dir * 2, sy + wag, 1, 1, body);
        if ((t * 6) % 1 < 0.3f) pix.Rect(sx + dir * 3, sy - 3, 1, 1, Pal.Rgba(220, 255, 230, 0.8f * (1 - s)));
    }

    /* ---------- Sorting a pot ---------- */
    // The keep line: the shell width (the length, for lobsters and crayfish) a crab should reach before you keep it.
    // The alimasag's is the real Philippine legal minimum; the others are a size most have spawned by.
    // For lobsters and crayfish it's the carapace (the head shell), measured along its length.
    static readonly Dictionary<string, float> KeepLine = new()
    {
        ["alimasag_crab"] = 10.2f, ["alimango_crab"] = 12f, ["curacha_crab"] = 9f, ["shore_crab"] = 6.5f,
        ["king_crab"] = 16f, ["ghost_crab"] = 4f, ["crayfish"] = 4.5f, ["spiny_lobster"] = 8f
    };
    static bool Sortable(string id) => KeepLine.ContainsKey(id);
    static bool CrabShaped(string id) => FishArt.Looks.TryGetValue(id, out var l) && l.Shape == "crab";

    List<CrabSort> sorting;
    int sortAt;

    CrabSort NewCrab(string id)
    {
        float line = KeepLine[id];
        bool female = rng.NextDouble() < 0.5;
        return new CrabSort
        {
            Id = id, Line = line, Width = MathF.Round(line * MathF.Pow(lastM / 0.645f, 0.75f) * 10) / 10, Big = lastM >= 1.25f,
            Female = female, Berried = female && rng.NextDouble() < 0.5, LateEggs = rng.NextDouble() < 0.4
        };
    }

    bool MustGo(CrabSort c) => c.Berried || c.Width < c.Line;

    // Puts the haul on the sorting tray. Once you sort by eye (or when it isn't you at the pot) it sorts itself, and this
    // returns a few words for the haul's message.
    string StartSort(List<CrabSort> crabs)
    {
        if (crabs.Count == 0) return "";
        if (state.Hinted("quickSort") || mode != "play")
        {
            var go = crabs.Where(MustGo).ToList();
            foreach (var c in go) { Take(c.Id); BigOff(c); LetCrabGo(c, true); }
            if (go.Count == 0) return "";
            CheckBadges();
            return $" You sort it by eye and let {go.Count} go ({(go.Any(c => c.Berried) ? "carrying eggs" : "too small")}).";
        }
        // On the tray they're out of the bag until you keep them, so nothing undecided can be sold or saved (nor counted
        // among the big ones Pip pays extra for).
        foreach (var c in crabs) { Take(c.Id); BigOff(c); }
        sorting = crabs;
        sortAt = 0;
        panel = "sort";
        mode = "panel";
        SetPrompt("");
        return "";
    }

    void DecideCrab(bool keep)
    {
        var c = sorting[sortAt];
        if (c.Decided) return;
        c.Decided = true;
        bool go = MustGo(c);
        string name = Items.ById[c.Id].Name.Split(' ')[0].ToLowerInvariant();
        bool crab = CrabShaped(c.Id);
        string flap = crab ? "apron" : "tail";
        string eggs = c.Id == "alimasag_crab" ? "A blue swimming crab can carry up to about two million." : c.LateEggs ? "Dark eggs like these are nearly ready to hatch." : "Bright orange eggs are freshly laid.";
        string sex = !crab ? "" : c.Female ? " A female: see the broad, rounded apron." : " A male: see the narrow, pointed apron.";
        string law = c.Id == "alimasag_crab" ? " (the legal minimum for alimasag in the Philippines)" : "";
        if (keep && !go) { c.Kept = true; c.Right = true; c.Note = $"Good keeper: {c.Width:0.0} cm, over the {c.Line:0.0} cm line, and no eggs.{sex}"; }
        else if (!keep && go)
        {
            c.Right = true;
            c.Note = c.Berried ? $"She's carrying eggs: the spongy mass under her {flap}. {eggs} Back she goes, and they'll hatch."
                : $"It's {c.Width:0.0} cm, under the {c.Line:0.0} cm line{law}. Give it a season to grow and breed.";
        }
        else if (keep)
            c.Note = c.Berried ? (c.Turned ? $"Not this one: she's carrying eggs under her {flap}. You slip her back into the water." : $"Turn every {name} over before you keep it! She was carrying eggs. You slip her back in.")
                : $"Too small: {c.Width:0.0} cm is under the {c.Line:0.0} cm line{law}. You put it back.";
        else { c.Fine = true; c.Note = $"That one was fine to keep ({c.Width:0.0} cm, no eggs), but letting one go never hurts.{sex}"; }
        if (c.Kept) KeepCrab(c);
        else LetCrabGo(c, c.Right);
        // Whatever you decided, you get to see her eggs.
        if (c.Berried) c.Turned = true;
        if (c.Right) state.sortStreak++;
        else if (!c.Fine) state.sortStreak = 0;
        if (c.Right) state.sortRight++;
        Sfx.Play(c.Right ? "right" : c.Fine ? "ui" : "wrong");
        if (state.sortStreak >= QuickSortStreak && !state.Hinted("quickSort"))
        {
            state.hinted["quickSort"] = true;
            c.Note += $" That's {QuickSortStreak} in a row: you can sort a pot by eye now.";
        }
        CheckBadges();
        Save();
    }

    void NextCrab()
    {
        if (sorting == null) return;
        if (sortAt < sorting.Count - 1) { sortAt++; Sfx.Play("ui"); return; }
        int kept = sorting.Count(c => c.Kept);
        string id = sorting[0].Id;
        sorting = null;
        ClosePanels();
        Toast(kept > 0 ? $"Sorted: {Items.Amount(id, kept)} in your bag." : "Sorted: you let the whole haul go.", 2.4f);
    }

    // Closing the tray halfway: what's left goes the safe way (too small or carrying eggs back in, the rest kept).
    void FinishSort()
    {
        if (sorting == null) return;
        foreach (var c in sorting.Where(c => !c.Decided))
        {
            if (MustGo(c)) LetCrabGo(c, true);
            else KeepCrab(c);
        }
        sorting = null;
        CheckBadges();
    }

    // A crab back in the sea counts as let go (toward the badge, too, when it was one to let go). It's already out of the
    // bag and off Pip's big-crab count (BigOff).
    void LetCrabGo(CrabSort c, bool credit)
    {
        state.releasedAll++;
        if (credit) state.released++;
    }

    void BigOff(CrabSort c) { if (c.Big && state.big.GetValueOrDefault(c.Id) > 0) state.big[c.Id]--; }

    void KeepCrab(CrabSort c)
    {
        Give(c.Id);
        if (c.Big) state.big[c.Id] = state.big.GetValueOrDefault(c.Id) + 1;
    }

    void UpdateSort()
    {
        if (sorting == null) { ClosePanels(); return; }
        var c = sorting[sortAt];
        // Keys: 1 keep, 2 let go, 3 turn it over; Enter or the action key for the next crab.
        if (!c.Decided)
        {
            if (Inp.Pressed(KeyboardKey.One)) DecideCrab(true);
            else if (Inp.Pressed(KeyboardKey.Two)) DecideCrab(false);
            else if (Inp.Pressed(KeyboardKey.Three)) { c.Turned = !c.Turned; Sfx.Play("blip"); }
        }
        else if (Inp.Pressed(KeyboardKey.Enter) || Bind.Pressed("act")) NextCrab();
    }

    // The underside of a crab or lobster, 24 x 24: legs, the apron (narrow and pointed for a male, broad and round for a
    // female) and, if she's carrying them, her eggs.
    static readonly Dictionary<string, Texture2D> undersides = new();
    static Texture2D Underside(string id, bool female, bool berried, bool late)
    {
        string key = $"{id}:{female}:{berried}:{late}";
        if (undersides.TryGetValue(key, out var tex)) return tex;
        tex = Gfx.ToTexture(UndersidePix(id, female, berried, late).Buf, 24, 24, TextureFilter.Point);
        undersides[key] = tex;
        return tex;
    }

    static Pix UndersidePix(string id, bool female, bool berried, bool late)
    {
        var L = FishArt.Looks[id];
        var p = new Pix(24, 24);
        string shell = L.Belly, edge = L.Body, leg = L.Fin, apron = Shade(L.Belly, 0.86f), seam = Shade(L.Belly, 0.7f);
        string egg = late ? "#4a3a30" : "#e8862a", egg2 = late ? "#6a5444" : "#f2a84a";
        if (L.Shape == "crab")
        {
            // Legs and claws first, then the body over them.
            for (int i = 0; i < 4; i++) { p.Line(6, 11 + i * 2, 1, 13 + i * 3, leg); p.Line(17, 11 + i * 2, 22, 13 + i * 3, leg); }
            p.Rect(1, 4, 4, 4, edge); p.Rect(19, 4, 4, 4, edge); p.Rect(4, 7, 3, 2, edge); p.Rect(17, 7, 3, 2, edge);
            p.Rect(5, 6, 14, 12, shell); p.Rect(4, 8, 16, 8, shell); p.Rect(7, 5, 10, 1, shell); p.Rect(7, 18, 10, 1, shell);
            p.Rect(5, 6, 14, 1, edge); p.Rect(4, 8, 1, 8, edge); p.Rect(19, 8, 1, 8, edge);
            if (berried)
            {
                // The sponge bulges out from under her apron.
                for (int y = 0; y < 9; y++)
                    for (int x = 0; x < 12; x++)
                    {
                        float dx = (x - 5.5f) / 6f, dy = (y - 4f) / 4.6f;
                        if (dx * dx + dy * dy <= 1) p.Rect(6 + x, 11 + y, 1, 1, (x + y * 3) % 4 == 0 ? egg2 : egg);
                    }
                p.Rect(8, 10, 8, 2, apron); p.Rect(7, 12, 1, 4, apron); p.Rect(16, 12, 1, 4, apron);
            }
            else if (female)
            {
                // Broad and rounded, like a dome.
                for (int y = 0; y < 8; y++) { int half = y < 5 ? 5 : 5 - (y - 4); p.Rect(12 - half, 10 + y, half * 2, 1, apron); }
                p.Rect(9, 12, 6, 1, seam); p.Rect(10, 14, 4, 1, seam);
            }
            else
            {
                // Narrow and pointed, like a lighthouse.
                p.Rect(10, 10, 4, 2, apron); p.Rect(11, 12, 2, 6, apron); p.Rect(11, 18, 2, 1, seam);
                p.Rect(11, 13, 2, 1, seam); p.Rect(11, 15, 2, 1, seam);
            }
        }
        else
        {
            // A lobster or crayfish on its back: walking legs under the head, the tail curled below, with eggs under it.
            for (int i = 0; i < 4; i++) { p.Line(9, 4 + i * 2, 4, 3 + i * 3, leg); p.Line(14, 4 + i * 2, 19, 3 + i * 3, leg); }
            p.Rect(8, 1, 8, 10, shell); p.Rect(7, 3, 10, 6, shell);
            for (int s = 0; s < 5; s++) { p.Rect(8, 11 + s * 2, 8, 2, s % 2 == 0 ? apron : shell); p.Rect(8, 11 + s * 2, 8, 1, seam); }
            p.Rect(6, 21, 12, 2, edge); p.Rect(9, 20, 6, 1, edge);
            if (berried)
                for (int y = 0; y < 9; y++)
                    for (int x = 0; x < 12; x++)
                        if ((x + y) % 2 == 0 || x is > 2 and < 9) p.Rect(6 + x, 11 + y, 1, 1, (x + y) % 3 == 0 ? egg2 : egg);
        }
        return p;
    }

    static string Shade(string hex, float k)
    {
        var c = Pal.C(hex);
        return $"#{(int)Math.Min(255, c.R * k):x2}{(int)Math.Min(255, c.G * k):x2}{(int)Math.Min(255, c.B * k):x2}";
    }

    float lastSortBottom;       // where the tray's text ended (the autotest checks it fits)

    void DrawSort()
    {
        if (sorting == null) return;
        Backdrop();
        var c = sorting[sortAt];
        const float w = 1000, h = 510, pad = 26;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        string name = Items.ById[c.Id].Name;
        Gfx.Text("Sort your catch", x + pad, y + pad - 2, FontKind.Ui700, 32, Pal.PaperInk);
        string count = $"{sortAt + 1} of {sorting.Count}";
        Gfx.Text(count, x + pad + Gfx.Measure("Sort your catch", FontKind.Ui700, 32) + 18, y + pad + 10, FontKind.Ui600, 18, Muted);
        Gfx.Text(Gfx.Ellipsize(name, FontKind.Ui700, 20, 430), x + pad, y + pad + 44, FontKind.Ui700, 20, Pal.PaperInk);
        string streak = state.Hinted("quickSort") ? "You sort by eye now: next time the pot sorts itself." : $"Sorted right in a row: {state.sortStreak} of {QuickSortStreak}";
        Gfx.Text(streak, x + w - pad - Gfx.Measure(streak, FontKind.Ui600, 16), y + pad + 48, FontKind.Ui600, 16, Muted);

        // The tray: the crab, either way up, and a ruler under it.
        float tx = x + pad, ty = y + pad + 82, tw = 440, th = 300;
        Gfx.Rect(tx, ty, tw, th, Pal.C("#b98b52"), 6);
        Gfx.Rect(tx + 8, ty + 8, tw - 16, th - 16, Pal.C("#d8b47a"), 4);
        if (c.Turned)
        {
            var under = Underside(c.Id, c.Female, c.Berried, c.LateEggs);
            DrawTexturePro(under, new Rectangle(0, 0, 24, 24), Gfx.S(tx + tw / 2 - 96, ty + 18, 192, 192), System.Numerics.Vector2.Zero, 0, Color.White);
        }
        else
        {
            var top = FishArt.Picture(c.Id, false);
            DrawTexturePro(top, new Rectangle(0, 0, FishArt.BigW, FishArt.BigH), Gfx.S(tx + tw / 2 - 168, ty + 14, FishArt.BigW * 7f, FishArt.BigH * 7f), System.Numerics.Vector2.Zero, 0, Color.White);
        }
        // The ruler: one tick a centimetre, the keep line in red, and the shell's width marked against it.
        float rx = tx + 20, ry = ty + th - 74, rw = tw - 40;
        float maxCm = MathF.Ceiling(MathF.Max(c.Line * 2, c.Width + 2) / 5) * 5, cm = rw / maxCm;
        Gfx.Rect(rx, ry, rw, 30, Pal.C("#f2e2b0"), 3);
        for (int i = 0; i <= maxCm; i++)
        {
            float lx = rx + i * cm;
            Gfx.Rect(lx, ry, 1.5f, i % 5 == 0 ? 14 : 7, Pal.Ink);
            if (i % 5 == 0 && i > 0) Gfx.TextCenter($"{i}", lx, ry + 14, FontKind.Ui600, 12, Pal.Ink);
        }
        float keepX = rx + c.Line * cm, crabX = rx + c.Width * cm;
        Gfx.Rect(keepX - 1.5f, ry - 10, 3, 40, PinRed);
        Gfx.Text($"keep line {c.Line:0.0} cm", Math.Min(keepX + 5, rx + rw - 110), ry - 22, FontKind.Ui700, 13, PinRed);
        Gfx.Rect(rx, ry + 34, c.Width * cm, 4, Pal.C("#1d5a88"), 2);
        Gfx.Triangle(crabX, ry + 30, crabX - 5, ry + 40, crabX + 5, ry + 40, Pal.C("#1d5a88"));
        Gfx.Text($"{(CrabShaped(c.Id) ? "shell" : "carapace")} {c.Width:0.0} cm", Math.Min(crabX + 8, rx + rw - 90), ry + 38, FontKind.Ui700, 13, Pal.C("#1d5a88"));
        string flip = c.Turned ? "Turn it back" : "Turn it over";
        if (!c.Decided && Button(flip, tx + tw / 2 - 90, ty + th + 14, 180, 44, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 3, 2, 5)) { c.Turned = !c.Turned; Sfx.Play("blip"); }

        // What to look for, then the choice, then what was right.
        float qx = tx + tw + 30, qw = x + w - pad - qx, qy = ty;
        void Step(int n, string text, bool done)
        {
            Gfx.Circle(qx + 12, qy + 12, 12, done ? Pal.C("#3f8a4a") : Pal.C("#c9b48f"));
            Gfx.TextCenter(n.ToString(), qx + 12, qy + 2, FontKind.Ui700, 16, Pal.Paper);
            var l = Gfx.Wrap(text, FontKind.Ui500, 16, qw - 34);
            Lines(l, qx + 32, qy + 2, 21, FontKind.Ui500, 16, Pal.PaperInk);
            qy += Math.Max(30, l.Count * 21 + 10);
        }
        string legal = c.Id == "alimasag_crab" ? " In the Philippines the law says 10.2 cm for alimasag, and none carrying eggs." : "";
        Step(1, $"Measure: is the {(CrabShaped(c.Id) ? "shell wider" : "carapace (the head shell) longer")} than the red keep line?{legal}", true);
        Step(2, CrabShaped(c.Id) ? "Turn it over: a female carrying eggs has a spongy orange or dark mass under her apron." : "Turn it over: a female carrying eggs holds them under her tail.", c.Turned);
        Step(3, "Keep it, or let it go.", c.Decided);
        qy += 6;
        if (!c.Decided)
        {
            if (Button("Keep it", qx, qy, 150, 50, FontKind.Ui700, 20, Pal.Sand, Pal.Ink, 4, 2, 6)) DecideCrab(true);
            if (Button("Let it go", qx + 166, qy, 160, 50, FontKind.Ui700, 20, Pal.C("#9fd0b0"), Pal.Ink, 4, 2, 6)) DecideCrab(false);
            qy += 64;
        }
        else
        {
            var bg = c.Right ? Pal.C("#e4f3e0") : c.Fine ? Pal.C("#eef2f6") : Pal.C("#f8e6dc");
            var lines = Gfx.Wrap(c.Note, FontKind.Ui500, 16, qw - 24);
            float bh = lines.Count * 21 + 44;
            Gfx.Box(qx, qy, qw, bh, bg, c.Right ? Pal.C("#3f8a4a") : c.Fine ? Pal.C("#8aa0b8") : Rust, 2, 5);
            Gfx.Text(c.Right ? "Tama! (Right!)" : c.Fine ? "That's fine" : "Not that one", qx + 12, qy + 8, FontKind.Ui700, 17, c.Right ? Pal.C("#2f6a3a") : c.Fine ? Pal.C("#3f5a7a") : Rust);
            Lines(lines, qx + 12, qy + 32, 21, FontKind.Ui500, 16, Pal.PaperInk);
            qy += bh + 12;
            string next = sortAt < sorting.Count - 1 ? "Next" : "Done";
            if (Button(next, qx, qy, 130, 46, FontKind.Ui700, 20, Pal.Buoy, White, 4, 2, 6)) { NextCrab(); return; }
            qy += 56;
        }
        lastSortBottom = qy;
    }
}
