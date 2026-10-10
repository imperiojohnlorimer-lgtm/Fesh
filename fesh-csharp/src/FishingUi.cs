using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Fishing overlays drawn over the world (cast power, the leap timing bar, the chest arrows, the derby clock,
// the fish finder, floating text) and the tackle box panel.
partial class Game
{
    // A point in the world, in UI coordinates.
    Vector2 ScreenOf(float wx, float wy) => new((wx - camX) * Gfx.LW / W, (wy - camY) * Gfx.LH / H);

    static void Outlined(string s, float x, float y, FontKind k, float size, Color c)
    {
        var dark = Pal.WithAlpha(Pal.C("#10243a"), c.A / 255f);
        foreach (var (ox, oy) in new[] { (-2, 0), (2, 0), (0, -2), (0, 2) }) Gfx.Text(s, x + ox, y + oy, k, size, dark);
        Gfx.Text(s, x, y, k, size, c);
    }

    void DrawFishingUi()
    {
        foreach (var f in floaters)
        {
            var p = ScreenOf(f.X, f.Y - f.T * 9);
            float a = f.T > 1.1f ? 1 - (f.T - 1.1f) / 0.4f : 1, pop = f.T < 0.12f ? 0.75f + f.T * 2 : 1;
            float size = 20 * pop;
            Outlined(f.Text, p.X - Gfx.Measure(f.Text, FontKind.Ui700, size) / 2, p.Y, FontKind.Ui700, size, Pal.WithAlpha(f.C, a));
        }
        if (mode == "charging") DrawChargeMeter();
        if (mode == "drill") DrawDrillMeter();
        if (mode == "reeling" && reel != null) DrawReelUi();
        if (mode == "chest") DrawChestGame();
        if (mode == "spear") DrawSpearHud();
        if (DerbyOn) DrawDerbyChip();
        if (Wears("fish_finder") && FinderSpot() is string spot) DrawFinder(spot);
    }

    // Draw after toasts so a forecast or catch message cannot cover a combat warning.
    void DrawFishAttackWarning()
    {
        if (mode != "reeling" || reel?.AttackWarning is not > 0) return;
        const float x = 350, y = 118, w = 580;
        Gfx.Box(x, y, w, 76, Pal.C("#572b35"), Pal.C("#ff9a8a"), 3, 6);
        Gfx.Text("FISH ATTACK! Release to duck", x + 22, y + 12, FontKind.Ui700, 26, Pal.Paper);
        Gfx.Rect(x + 22, y + 53, (int)((w - 44) * reel.AttackWarning / 1.25f), 8, Pal.Lantern, 2);
    }

    // A power bar over your head, split into shallow, middle and deep water.
    void DrawChargeMeter()
    {
        var p = ScreenOf(player.X, player.Y - 30);
        const float w = 150, h = 18;
        float x = p.X - w / 2, y = p.Y - h;
        Gfx.Rect(x - 4, y - 4, w + 8, h + 8, Pal.C("rgba(16,36,58,0.9)"), 5);
        Gfx.Rect(x, y, w * 0.35f, h, Pal.C("#9fd3e6"), 3);
        Gfx.Rect(x + w * 0.35f, y, w * 0.4f, h, Pal.C("#3a8db0"));
        Gfx.Rect(x + w * 0.75f, y, w * 0.25f, h, Pal.C("#1d4f78"), 3);
        float mx = x + w * charge;
        Gfx.Rect(mx - 2, y - 5, 4, h + 10, Pal.C("#ffffff"));
        string label = CastDepth(charge) switch { 0 => "Shallow", 1 => "Middle", _ => "Deep" };
        Outlined(label, p.X - Gfx.Measure(label, FontKind.Ui700, 18) / 2, y - 30, FontKind.Ui700, 18, White);
    }

    void DrawDrillMeter()
    {
        var p = ScreenOf(605, 70);
        const float w = 140, h = 14;
        Gfx.Rect(p.X - w / 2 - 4, p.Y - 4, w + 8, h + 8, Pal.C("rgba(16,36,58,0.9)"), 5);
        Gfx.Rect(p.X - w / 2, p.Y, w * Math.Clamp(drill, 0, 1), h, Pal.C("#9fe8ff"), 3);
        string s = Bind.Fix("Mash <act>!");
        float bounce = (int)(time * 8) % 2 * 2;
        Outlined(s, p.X - Gfx.Measure(s, FontKind.Ui700, 20) / 2, p.Y - 32 - bounce, FontKind.Ui700, 20, Pal.C("#ffffff"));
    }

    // Words next to the reel for each fighting style, and the timing bar when a jumper leaps.
    void DrawReelUi()
    {
        var r = reel;
        float rx = (r.Style == "runner" ? 280 : 284) * 4 - 16;
        string tag = r.Style switch { "runner" => "Runner", "jumper" => "Jumper", "bottom" => "Bottom-hugger", _ => "" };
        if (tag != "") Outlined(tag, rx - Gfx.Measure(tag, FontKind.Ui700, 18), 104, FontKind.Ui700, 18, Pal.C("#dfe9ee"));
        if (r.Running > 0)
        {
            float k = (int)(time * 8) % 2 == 0 ? 1 : 0.7f;
            Outlined("RUN!", rx - Gfx.Measure("RUN!", FontKind.Ui700, 34), 130, FontKind.Ui700, 34, Pal.WithAlpha(Pal.C("#ff6a5a"), k));
            Outlined("Let go!", rx - Gfx.Measure("Let go!", FontKind.Ui700, 20), 170, FontKind.Ui700, 20, Pal.C("#ffffff"));
        }
        if (r.Style == "runner")
            Outlined("Tension", (280 + 27) * 4 - Gfx.Measure("Tension", FontKind.Ui600, 14) / 2 + 8, 26 * 4 + 120 * 4 + 6, FontKind.Ui600, 14, Pal.C("#dfe9ee"));
        if (r.Style == "bottom")
        {
            string s = r.Digging ? "Digging in!" : "Pump!";
            float bounce = (int)(time * 6) % 2 * 3;
            Outlined(s, rx - Gfx.Measure(s, FontKind.Ui700, 26), 140 - bounce, FontKind.Ui700, 26, r.Digging ? Pal.C("#ff9a5a") : Pal.C("#bff4ff"));
        }
        if (r.Leap > 0)
        {
            const float w = 420, h = 30;
            float x = Gfx.LW / 2 - w / 2, y = Gfx.LH - 130;
            Gfx.Rect(x - 6, y - 6, w + 12, h + 12, Pal.C("rgba(16,36,58,0.92)"), 6);
            Gfx.Rect(x, y, w, h, Pal.C("#2f7fa3"), 4);
            Gfx.Rect(x + w * 0.6f, y, w * 0.25f, h, Pal.C("#f3c25b"), 3);
            float mx = x + w * Math.Clamp(r.LeapMark, 0, 1);
            Gfx.Rect(mx - 3, y - 8, 6, h + 16, r.LeapDone ? Pal.C("#9aa0a5") : White);
            Outlined("LEAP!", Gfx.LW / 2 - Gfx.Measure("LEAP!", FontKind.Ui700, 30) / 2, y - 46, FontKind.Ui700, 30, Pal.C("#f3c25b"));
        }
        if (r.Perfect && r.Progress < 0.6f)
            Outlined("Perfect hook", rx - Gfx.Measure("Perfect hook", FontKind.Ui600, 16), 82, FontKind.Ui600, 16, Pal.C("#f3c25b"));
    }

    // The arrows to press to haul up a sunken chest, and how long you have.
    void DrawChestGame()
    {
        int n = chestSeq.Count;
        const float box = 66, gap = 12;
        float w = n * box + (n - 1) * gap, x = Gfx.LW / 2 - w / 2, y = 250;
        float shake = chestShake > 0 ? MathF.Sin(time * 80) * 6 : 0;
        Gfx.Box(x - 28 + shake, y - 74, w + 56, box + 128, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.TextCenter("A sunken chest! Press the arrows in order", Gfx.LW / 2 + shake, y - 56, FontKind.Ui700, 24, Pal.PaperInk);
        for (int i = 0; i < n; i++)
        {
            float bx = x + i * (box + gap) + shake;
            bool done = i < chestAt, now = i == chestAt;
            Gfx.Box(bx, y, box, box, done ? Pal.C("#7fd36b") : now ? Pal.Lantern : Pal.Sand, Pal.Ink, 3, 6, now ? 4 : 2);
            DrawArrow(bx + box / 2, y + box / 2, chestSeq[i], done ? Pal.C("#2f6428") : Pal.Ink);
        }
        float total = 4.5f + n * 0.35f, k = Math.Clamp(chestT / total, 0, 1);
        Gfx.Rect(x, y + box + 22, w, 14, Pal.C("rgba(0,0,0,0.15)"), 4);
        Gfx.Rect(x, y + box + 22, w * k, 14, k < 0.3f ? Pal.Buoy : Pal.C("#5fb04f"), 4);
    }

    static void DrawArrow(float cx, float cy, int dir, Color c)
    {
        // 0 up, 1 right, 2 down, 3 left
        Vector2 R(float x, float y) => dir switch { 0 => new(x, y), 1 => new(-y, x), 2 => new(-x, -y), _ => new(y, -x) };
        var a = R(0, -20); var b = R(-17, 4); var d = R(17, 4);
        DrawTriangle(Gfx.P(cx + a.X, cy + a.Y), Gfx.P(cx + b.X, cy + b.Y), Gfx.P(cx + d.X, cy + d.Y), c);
        var s0 = R(-7, 4); var s1 = R(7, 18);
        float x0 = Math.Min(s0.X, s1.X), y0 = Math.Min(s0.Y, s1.Y);
        Gfx.Rect(cx + x0, cy + y0, MathF.Abs(s1.X - s0.X), MathF.Abs(s1.Y - s0.Y), c);
    }

    void DrawSpearHud()
    {
        string s = $"Spears: {spears}    Time: {Math.Max(0, (int)MathF.Ceiling(spearT))}s    Speared: {speared}";
        float w = Gfx.Measure(s, FontKind.Ui700, 20) + 30;
        Gfx.Rect(Gfx.LW / 2 - w / 2, 62, w, 38, NavyStrong, 5);
        Gfx.TextCenter(s, Gfx.LW / 2, 70, FontKind.Ui700, 20, Pal.Paper);
    }

    void DrawDerbyChip()
    {
        int secs = (int)MathF.Ceiling(derbyT);
        var rivals = DerbyRivals(state.derbyDay);
        float target = rivals.Max(r => r.kg);
        string s = $"Derby {secs / 60}:{secs % 60:00}   ·   Your best: {(derbyFish == null ? "nothing yet" : Kg(derbyBest))}   ·   To win: over {Kg(target)}";
        float w = Gfx.Measure(s, FontKind.Ui600, 18) + 30, x = 13 + (136 + 7) * 2, y = 13 + 36 + 7;
        Gfx.Rect(x, y, w, 36, secs <= 20 && (int)(time * 3) % 2 == 0 ? Pal.C("rgba(181,82,59,0.9)") : NavyStrong, 5);
        Gfx.Text(s, x + 15, y + 8, FontKind.Ui600, 18, derbyBest > target ? Pal.C("#7fd36b") : Pal.Paper);
    }

    // The spot the fish finder reports on: the one you're fishing, or the one you're standing at.
    string FinderSpot() =>
        fish?.Wild == true ? null
        : fish != null && mode is "waiting" or "casting" or "charging" ? fish.Spot
        : mode == "charging" ? chargeSpot
        : mode == "play" && target?.Type == "spot" ? target.Id : null;

    void DrawFinder(string spot)
    {
        int depth = fish?.Depth ?? (mode == "charging" ? CastDepth(charge) : 1);
        var odds = CatchOdds(spot, depth).Take(8).ToList();
        const float w = 300, rowH = 26, x = 1280 - 13 - w;
        float y = 62, h = 44 + odds.Count * rowH + 10;
        Gfx.Box(x, y, w, h, Pal.C("rgba(16,36,58,0.9)"), Pal.C("#7fd36b"), 2, 6);
        Gfx.Text("Fish finder", x + 12, y + 8, FontKind.Ui700, 18, Pal.C("#7fd36b"));
        string sub = $"{DepthName(depth)}" + (NextBait() is string b ? $", {Items.ById[b].Name.ToLowerInvariant()}" : "");
        Gfx.Text(Gfx.Ellipsize(sub, FontKind.Ui500, 14, w - 130), x + 120, y + 11, FontKind.Ui500, 14, Pal.C("#b8d4c0"));
        float ry = y + 38;
        foreach (var (id, name, pct) in odds)
        {
            if (Items.ById.ContainsKey(id)) DrawIcon(id, x + 10, ry, 22);
            else if (id is "chest" or "tidemane") DrawIcon(id == "chest" ? "coin" : id, x + 10, ry, 22);
            Gfx.Text(Gfx.Ellipsize(name, FontKind.Ui600, 16, w - 110), x + 40, ry + 2, FontKind.Ui600, 16, Pal.Paper);
            string p = pct < 0.01 ? "<1%" : $"{pct * 100:0}%";
            Gfx.Text(p, x + w - 12 - Gfx.Measure(p, FontKind.Ui700, 16), ry + 2, FontKind.Ui700, 16, Pal.C("#7fd36b"));
            ry += rowH;
        }
    }

    /* ---------- Tackle box ---------- */
    static readonly (string slot, string name)[] TackleRows =
        { ("reel", "Reel"), ("line", "Line"), ("hook", "Hook"), ("bobber", "Bobber"), ("sinker", "Sinker"), ("bait", "Bait") };

    // Everything you could put in a slot: null means empty (or "auto" for bait).
    List<string> TackleOptions(string slot)
    {
        if (slot == "bait") return new List<string> { "auto" }.Concat(Items.Baits.Keys.Where(b => Has(b) > 0)).ToList();
        return new List<string> { null }.Concat(Items.TackleOrder[slot].Where(i => Has(i) > 0)).ToList();
    }

    string TackleChoice(string slot) => slot == "bait" ? state.tackle.GetValueOrDefault("bait") is string b && Has(b) > 0 ? b : "auto" : GearId(slot);

    void CycleTackle(string slot, int dir)
    {
        var opts = TackleOptions(slot);
        int i = opts.IndexOf(TackleChoice(slot));
        string next = opts[((i < 0 ? 0 : i) + dir + opts.Count) % opts.Count];
        state.tackle[slot] = next ?? "none";
        Sfx.Play("blip");
        Save();
    }

    string TackleSummary(string slot, string id)
    {
        if (slot == "bait")
            return id == "auto" ? (Has("glow_bait") + Has("bait") > 0 ? "Glow bait first, then plain bait" : "No bait: fish bite slowly") : Items.Baits[id].Summary;
        return id == null ? slot switch { "reel" => "The basic reel on your rod", "line" => "Plain line: snaps easily", "hook" => "A plain hook", "bobber" => "A plain float", _ => "None: casts reach the depth you throw to" }
            : Bind.Fix(Items.ById[id].Desc);
    }

    // What's in a tackle slot, in the 250 px before its description (the longest, a live tamban, is about 230).
    const float TackleLabelW = 250;
    string TackleLabel(string slot, string id)
    {
        string label = slot == "bait" ? (id == "auto" ? "Automatic" : $"{Items.ById[id].Name} ({(Items.Baits[id].Reusable ? "reusable" : $"{Has(id)} left")})")
            : id == null ? "None" : Items.ById[id].Name;
        return Gfx.Ellipsize(label, FontKind.Ui700, 21, TackleLabelW);
    }

    void DrawTackle()
    {
        Backdrop();
        const float cw = 1160, ch = 660, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Tackle box", x + pad, y + pad, FontKind.Ui700, 36, Pal.PaperInk);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) ClosePanels();
        Gfx.Text("Your best rod is used automatically. Click the arrows to change the rest.", x + pad, y + pad + 48, FontKind.Note, 18, Muted);

        // Left: the rod and the slots
        float lx = x + pad, ly = y + pad + 84, lw = 640, rowH = 70;
        string rod = BestRod();
        Gfx.Box(lx, ly, lw, rowH - 8, CardBg, Pal.C("#c9b48f"), 2, 6);
        DrawIcon(rod, lx + 10, ly + 7, 48);
        Gfx.Text("Rod", lx + 70, ly + 6, FontKind.Ui600, 15, Muted);
        Gfx.Text(Items.ById[rod].Name, lx + 70, ly + 24, FontKind.Ui700, 21, Pal.PaperInk);
        Gfx.Text(Gfx.Ellipsize(Items.Rod[rod].Summary, FontKind.Ui500, 15, lw - 300), lx + 280, ly + 26, FontKind.Ui500, 15, Muted);
        ly += rowH;
        foreach (var (slot, name) in TackleRows)
        {
            string id = TackleChoice(slot);
            Gfx.Box(lx, ly, lw, rowH - 8, CardBg, Pal.C("#c9b48f"), 2, 6);
            string icon = slot == "bait" ? (id == "auto" ? "bait" : id) : id;
            if (icon != null) DrawIcon(icon, lx + 10, ly + 7, 48);
            else Gfx.Rect(lx + 14, ly + 11, 40, 40, Pal.C("rgba(0,0,0,0.08)"), 6);
            Gfx.Text(name, lx + 70, ly + 6, FontKind.Ui600, 15, Muted);
            Gfx.Text(TackleLabel(slot, id), lx + 70, ly + 24, FontKind.Ui700, 21, Pal.PaperInk);
            float sx = lx + 330;
            Lines(Gfx.Wrap(TackleSummary(slot, id), FontKind.Ui500, 15, lw - 330 - 120).Take(2).ToList(), sx, ly + 12, 19, FontKind.Ui500, 15, Muted);
            int opts = TackleOptions(slot).Count;
            bool live = opts > 1;
            if (Button("<", lx + lw - 104, ly + 12, 42, 38, FontKind.Ui700, 20, live ? Pal.Sand : Pal.C("#e6d6b6"), Pal.Ink, 3, 2, 5, live)) CycleTackle(slot, -1);
            if (Button(">", lx + lw - 54, ly + 12, 42, 38, FontKind.Ui700, 20, live ? Pal.Sand : Pal.C("#e6d6b6"), Pal.Ink, 3, 2, 5, live)) CycleTackle(slot, 1);
            ly += rowH;
        }

        // Right: fishing level and perks, accessories and collections
        float rx = lx + lw + 24, ry = y + pad + 84, rw = x + cw - pad - rx;
        int lvl = FishLevel;
        Gfx.Text($"Fishing level {lvl}", rx, ry, FontKind.Ui700, 24, Pal.PaperInk);
        if (lvl < 10)
        {
            float k = (state.xp - XpFor(lvl)) / (float)(XpFor(lvl + 1) - XpFor(lvl));
            Gfx.Rect(rx, ry + 34, rw, 14, Pal.C("rgba(0,0,0,0.12)"), 4);
            Gfx.Rect(rx, ry + 34, Math.Max(6, rw * k), 14, Pal.C("#5fb04f"), 4);
            Gfx.Text($"{state.xp - XpFor(lvl)} / {XpFor(lvl + 1) - XpFor(lvl)} XP to level {lvl + 1}", rx, ry + 52, FontKind.Ui500, 15, Muted);
        }
        else Gfx.Text("Master angler!", rx, ry + 34, FontKind.Ui600, 17, Pal.C("#3f7d35"));
        ry += 80;
        for (int l = 2; l <= 10; l++)
        {
            bool got = lvl >= l;
            Gfx.Text($"{l}", rx, ry, FontKind.Ui700, 15, got ? Pal.C("#3f7d35") : Pal.C("#9a8a70"));
            Gfx.Text(PerkText[l], rx + 24, ry, FontKind.Ui500, 15, got ? Pal.PaperInk : Pal.C("#9a8a70"));
            ry += 20;
        }
        ry += 14;
        Gfx.Text("Accessories (worn automatically)", rx, ry, FontKind.Ui700, 18, Pal.PaperInk);
        ry += 28;
        string[] acc = { "sunglasses", "fish_finder", "waders", "lucky_charm", "headlamp", "cooler" };
        for (int i = 0; i < acc.Length; i++)
        {
            float ax = rx + (i % 3) * (rw / 3), ay = ry + (i / 3) * 52;
            bool own = Has(acc[i]) > 0;
            Gfx.Box(ax, ay, 44, 44, own ? Pal.Lantern : Pal.C("#e6d6b6"), own ? Pal.Ink : Pal.C("#c9b48f"), 2, 5);
            if (own) DrawIcon(acc[i], ax + 6, ay + 6, 32);
            else Gfx.TextCenter("?", ax + 22, ay + 10, FontKind.Ui700, 22, Pal.C("#9a8a70"));
            var nameLines = Gfx.Wrap(own ? Items.ById[acc[i]].Name : "Not made yet", FontKind.Ui500, 13, rw / 3 - 54);
            Lines(nameLines.Take(2).ToList(), ax + 50, ay + (nameLines.Count > 1 ? 5 : 13), 16, FontKind.Ui500, 13, own ? Pal.PaperInk : Pal.C("#9a8a70"));
            if (Gfx.Hover(ax, ay, 44, 44) && own) Gfx.Text(Bind.Fix(Items.ById[acc[i]].Desc), x + pad, y + ch - pad - 18, FontKind.Ui500, 15, Pal.PaperInk);
        }
        ry += 110;
        var sets = Data.AquaSets.Where(s => SetActive(s.Id)).ToList();
        Gfx.Text("Aquarium collections", rx, ry, FontKind.Ui700, 18, Pal.PaperInk);
        ry += 26;
        if (sets.Count == 0) Gfx.Text("None yet. Fill an aquarium with a set of four.", rx, ry, FontKind.Ui500, 15, Muted);
        foreach (var s in sets) { Gfx.Text($"{s.Name}: {s.Perk}", rx, ry, FontKind.Ui500, 15, Pal.C("#3f7d35")); ry += 20; }
        if (Gfx.PressedOutside(x, y, cw, ch)) ClosePanels();
    }
}
