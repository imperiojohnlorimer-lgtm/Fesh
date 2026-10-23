using Raylib_cs;

namespace Fesh;

// The trophy wall (1.46): an indoor piece for your shack or bahay kubo that mounts your record fish as plaques. It reads what
// the save already keeps (state.records, with the day and size recordDay/recordM from 1.46 on; a record from before that
// shows "day unknown" and takes its tier from its weight against the fish's usual), so nothing is consumed and a fish you
// sell still hangs there. The wall itself shows the three best; pressing <act> at it opens all of them, a page at a time, and
// somebody who knows you says something about them. Furnishing a home with more kinds of piece earns the house goals below.
partial class Game
{
    sealed record Plaque(string Id, string Name, float Kg, int Tier, int Day);

    // Every record, best tier first and then the heaviest.
    List<Plaque> plaqueCache; State plaqueState; double plaqueSig;
    List<Plaque> Plaques()
    {
        // The wall is drawn every frame it is on screen: the sorted list is kept until the records change (a cheap signature).
        double sig = state.records.Count * 1e6 + state.records.Values.Sum() * 100 + state.recordM.Count + state.recordDay.Values.Sum() * 7;
        if (plaqueCache != null && ReferenceEquals(plaqueState, state) && plaqueSig == sig) return plaqueCache;
        plaqueState = state; plaqueSig = sig;
        return plaqueCache = BuildPlaques();
    }

    List<Plaque> BuildPlaques()
    {
        var list = new List<Plaque>();
        foreach (var (id, kg) in state.records)
        {
            if (!Data.FishById.TryGetValue(id, out var f) || kg <= 0) continue;
            float m = state.recordM.TryGetValue(id, out var rm) && rm > 0 ? rm : kg / MathF.Max(0.01f, f.Kg);
            list.Add(new Plaque(id, f.Name.Split(" (")[0], kg, TrophyTier(m), state.recordDay.GetValueOrDefault(id)));
        }
        return list.OrderByDescending(p => p.Tier).ThenByDescending(p => p.Kg).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    static string TierColour(int tier) => tier switch { 3 => "#e8c040", 2 => "#d9a43a", 1 => "#c2c8cc", _ => "#a88a6a" };

    /* ---------- On the wall ---------- */
    // The three best, as little mounted fish over their plaques (drawn on the piece, so also while building).
    void DrawTrophyWall(Build b, int X, int Y)
    {
        pix.Rect(X + 1, Y + 9, 18, 1, "rgba(0,0,0,0.25)");
        pix.Rect(X + 1, Y - 8, 18, 16, "#6b4a2b"); pix.Rect(X + 2, Y - 7, 16, 14, "#8a6440"); pix.Rect(X + 2, Y - 7, 16, 1, "#a07a4e");
        var top = state?.records == null ? new List<Plaque>() : Plaques().Take(3).ToList();
        for (int i = 0; i < 3; i++)
        {
            int px = X + 3 + i * 5;
            pix.Rect(px, Y + 3, 4, 2, i < top.Count ? TierColour(top[i].Tier) : "#7a5c3a");
            if (i < top.Count) FishArt.Draw(pix, top[i].Id, px, Y - 5, 4, 7);
            else pix.Rect(px + 1, Y - 3, 2, 4, "#6f5434");
        }
        pix.Rect(X + 1, Y + 6, 18, 3, "#5b3a24");
    }

    /* ---------- The panel ---------- */
    int trophyPage;
    internal float lastTrophyBottom;

    void OpenTrophies()
    {
        trophyPage = 0;
        Sfx.Play("ui");
        panel = "trophies"; mode = "panel";
        SetPrompt("");
        var list = Plaques();
        if (list.Count == 0) return;
        // Somebody who knows you looks at the best of them.
        var best = list[0];
        var known = Friends.Where(f => state.friends.ContainsKey(f.Id) && Hearts(f.Id) >= 1).ToList();
        string who = known.Count > 0 ? known[(state.day + best.Id.Length) % known.Count].Name : "Tomas";
        ToastLater($"{who} would say: a {best.Name.ToLowerInvariant()} of {Kg(best.Kg)}. {(best.Tier >= 2 ? "Now that's a catch to talk about." : "Not bad at all.")}");
    }

    void DrawTrophies()
    {
        var list = Plaques();
        var (x, y, w, h) = PanelFrame("Trophy wall", $"{list.Count} record fish, the best first. Catch a bigger one and its plaque is updated.", 1000, 660);
        if (PanelClose()) { ClosePanels(); return; }
        float lx = x + PanelPad, ly = y + 104, gap = 12;
        const int cols = 3, rows = 4, per = cols * rows;
        float cw = (w - PanelPad * 2 - gap * (cols - 1)) / cols, ch = 100;
        if (list.Count == 0) { Gfx.Text("Nothing mounted yet. Your heaviest catch of each kind will hang here.", lx, ly, FontKind.Note, 20, Muted); return; }
        int pages = (list.Count + per - 1) / per;
        trophyPage = Math.Clamp(trophyPage, 0, pages - 1);
        for (int i = 0; i < per && trophyPage * per + i < list.Count; i++)
        {
            var p = list[trophyPage * per + i];
            float cx = lx + i % cols * (cw + gap), cy = ly + i / cols * (ch + gap);
            Gfx.Box(cx, cy, cw, ch, Pal.C("#6b4a2b"), Pal.C(TierColour(p.Tier)), 3, 6);
            Gfx.Rect(cx + 6, cy + 6, 76, 88, Pal.C("#8a6440"), 4);
            DrawIcon(p.Id, cx + 14, cy + 22, 60);
            Gfx.Text(Gfx.Ellipsize(p.Name, FontKind.Ui700, 19, cw - 100), cx + 90, cy + 10, FontKind.Ui700, 19, Pal.C("#f6ecd0"));
            Gfx.Text(Kg(p.Kg), cx + 90, cy + 36, FontKind.Ui700, 24, Pal.C(TierColour(p.Tier == 0 ? 1 : p.Tier)));
            string tier = p.Tier == 0 ? "" : TierName(p.Tier) + " · ";
            Gfx.Text(Gfx.Ellipsize(tier + (p.Day > 0 ? $"day {p.Day}" : "day unknown"), FontKind.Ui500, 15, cw - 100), cx + 90, cy + 70, FontKind.Ui500, 15, Pal.C("#d9c9a4"));
            // Click a plaque to make it your favourite (what the dock's showroom shows visitors).
            bool favourite = state.favFish == p.Id;
            if (favourite) Gfx.Text("Favourite", cx + cw - 12 - Gfx.Measure("Favourite", FontKind.Ui700, 14), cy + 72, FontKind.Ui700, 14, Pal.C("#ffd76a"));
            if (Gfx.Click(cx, cy, cw, ch) && ShowcaseFish.Contains(p.Id)) { state.favFish = p.Id; dockProfileSent = false; Sfx.Play("ui"); Toast($"Your favourite is now the {p.Name.ToLowerInvariant()}. Visitors on the Tournament Dock will see it."); }
#if DEBUG
            Gfx.Seen["plaque:" + p.Id] = new Rectangle(cx, cy, cw, ch);
#endif
            lastTrophyBottom = MathF.Max(lastTrophyBottom, cy + ch);
        }
        if (pages > 1)
        {
            float by = ly + rows * (ch + gap) + 4;
            if (trophyPage > 0 && SmallButton("Back", lx, by)) { trophyPage--; Sfx.Play("ui"); return; }
            Gfx.TextCenter($"Page {trophyPage + 1} of {pages}", x + w / 2, by + 12, FontKind.Ui600, 17, Muted);
            if (trophyPage < pages - 1 && SmallButton("More", x + w - PanelPad - SmallW("More"), by)) { trophyPage++; Sfx.Play("ui"); return; }
            lastTrophyBottom = MathF.Max(lastTrophyBottom, by + 44);
        }
    }

    /* ---------- House goals ---------- */
    // The kinds of furnishing a home can have, counted once each however many of a kind there are.
    static readonly string[] Furnishings = { "rug", "table", "plant", "lamp", "bed", "trophy", "aquarium" };
    static string FurnishingName(string id) => id switch { "trophy" => "trophy wall", "plant" => "potted plant", _ => id };

    int HomeKinds(string key) => state.rooms.TryGetValue(key, out var r) && r != null ? r.Select(b => b.id).Where(Furnishings.Contains).Distinct().Count() : 0;
    int MostKinds => state.rooms.Keys.Select(HomeKinds).DefaultIfEmpty(0).Max();
    IEnumerable<string> MissingInBestHome()
    {
        string best = state.rooms.Keys.OrderByDescending(HomeKinds).FirstOrDefault();
        var have = best != null && state.rooms[best] != null ? state.rooms[best].Select(b => b.id).ToHashSet() : new HashSet<string>();
        return Furnishings.Where(f => !have.Contains(f));
    }

    void HomeGoals(List<Goal> list)
    {
        if (state == null || !state.created || !HasHome || MostKinds >= Furnishings.Length) return;
        int n = MostKinds;
        var missing = MissingInBestHome().Select(FurnishingName).ToList();
        list.Add(NewGoal("home:furnish", "explore", $"Furnish your home: {n} of {Furnishings.Length} kinds", "Still to put in: " + string.Join(", ", missing) + ". Three kinds, five and all of them earn a reward in the Ledger."));
    }

    static IEnumerable<Ach> HomeAchievements()
    {
        yield return new Ach("home3", "Home & boat", "Cosy", "Put 3 kinds of furnishing in one home.", g => g.MostKinds, 3, 40);
        yield return new Ach("home5", "Home & boat", "Comfortable", "Put 5 kinds of furnishing in one home.", g => g.MostKinds, 5, 80);
        yield return new Ach("home7", "Home & boat", "Home sweet home, in full", "Furnish one home with a rug, table, plant, lamp, bed, trophy wall and aquarium.", g => g.MostKinds, Furnishings.Length, 150, "Homebody");
    }
}
